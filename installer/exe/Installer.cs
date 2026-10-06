using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ATKINSMEC.Setup
{
    // Copies the add-in into the per-user Revit add-ins folder. That
    // folder is under %AppData%, so nothing here needs administrator
    // rights - see app.manifest for why the manifest matters too.
    //
    // The folder is shared with other tools (RevitLookup, the Add-In
    // Manager), so only our own files are ever written or removed. What
    // was installed is recorded in ATKINSMEC.files.txt beside them, and
    // the uninstall reads that list rather than clearing the folder.
    internal static class Installer
    {
        internal const string Manifest = "ATKINSMEC.files.txt";

        internal static string AddinsFolder(string version)
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", version);
        }

        internal static string SettingsFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC");
            }
        }

        // A version is offered when Revit is installed, or when its
        // add-ins folder is already there - which covers Revit installed
        // somewhere other than the default place.
        internal static bool RevitPresent(string version)
        {
            if (Directory.Exists(AddinsFolder(version)))
                return true;

            string programFiles = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);

            return File.Exists(Path.Combine(
                programFiles, "Autodesk", "Revit " + version, "Revit.exe"));
        }

        internal static List<string> RunningRevits()
        {
            var found = new List<string>();

            try
            {
                foreach (Process process in Process.GetProcessesByName("Revit"))
                {
                    using (process)
                    {
                        string title = string.Empty;

                        try
                        {
                            title = process.MainWindowTitle;
                        }
                        catch
                        {
                            // a process we cannot query still counts
                        }

                        found.Add(string.IsNullOrEmpty(title)
                            ? "Revit (PID " + process.Id + ")"
                            : title);
                    }
                }
            }
            catch
            {
                // if the process list cannot be read, do not block
            }

            return found;
        }

        // ==================================================

        internal static int Install(string version)
        {
            string target = AddinsFolder(version);

            if (!Directory.Exists(target))
                Directory.CreateDirectory(target);

            // Clear out anything a previous install left that this one
            // no longer ships
            Uninstall(version);

            List<string> written = Payload.Extract(version, target);

            if (written.Count == 0)
                return 0;

            File.WriteAllLines(
                Path.Combine(target, Manifest),
                written.ToArray(),
                Encoding.UTF8);

            return written.Count;
        }

        internal static int Uninstall(string version)
        {
            string target = AddinsFolder(version);

            if (!Directory.Exists(target))
                return 0;

            string listFile = Path.Combine(target, Manifest);

            if (!File.Exists(listFile))
                return 0;

            int removed = 0;

            foreach (string relative in File.ReadAllLines(listFile))
            {
                if (string.IsNullOrEmpty(relative.Trim()))
                    continue;

                string path = Path.Combine(target, relative.Trim());

                if (!File.Exists(path))
                    continue;

                File.Delete(path);

                removed++;
            }

            File.Delete(listFile);

            RemoveEmptyFolders(target);

            return removed;
        }

        // Only folders that are now empty, and never the add-ins folder
        // itself - it belongs to Revit and is shared
        private static void RemoveEmptyFolders(string root)
        {
            try
            {
                var all = new List<string>(Directory.GetDirectories(
                    root, "*", SearchOption.AllDirectories));

                // Deepest first, so a parent is empty by the time it is
                // looked at
                all.Sort((a, b) => b.Length.CompareTo(a.Length));

                foreach (string folder in all)
                {
                    if (Directory.GetFileSystemEntries(folder).Length == 0)
                        Directory.Delete(folder);
                }
            }
            catch
            {
                // an empty folder left behind harms nothing
            }
        }

        internal static bool SettingsExist
        {
            get { return Directory.Exists(SettingsFolder); }
        }

        internal static void RemoveSettings()
        {
            if (Directory.Exists(SettingsFolder))
                Directory.Delete(SettingsFolder, true);
        }
    }
}
