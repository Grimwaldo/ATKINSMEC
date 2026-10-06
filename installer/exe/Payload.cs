using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ATKINSMEC.Setup
{
    // The add-in files, carried inside this executable as one embedded
    // zip. Compressed rather than stored file by file: the same handful
    // of DLLs repeats across six Revit versions, and uncompressed that
    // came to 32 MB against 13 MB for the archive.
    //
    // Each entry is named <version>/<relative path>, which is all the
    // routing information needed.
    internal static class Payload
    {
        private const string ResourceName = "PAYLOAD.zip";

        internal class Entry
        {
            public string FullName;
            public string Version;
            public string RelativePath;
        }

        private static Stream OpenArchive()
        {
            Assembly self = Assembly.GetExecutingAssembly();

            Stream stream = self.GetManifestResourceStream(ResourceName);

            if (stream == null)
                throw new IOException(
                    "This installer is missing its payload. Rebuild it " +
                    "with build-installer.ps1.");

            return stream;
        }

        internal static List<Entry> All()
        {
            var entries = new List<Entry>();

            using (Stream stream = OpenArchive())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Folder entries have no name
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    string full = entry.FullName.Replace('\\', '/');

                    int cut = full.IndexOf('/');

                    if (cut <= 0 || cut == full.Length - 1)
                        continue;

                    entries.Add(new Entry
                    {
                        FullName = entry.FullName,
                        Version = full.Substring(0, cut),

                        // Windows wants backslashes from here on
                        RelativePath = full
                            .Substring(cut + 1)
                            .Replace('/', Path.DirectorySeparatorChar)
                    });
                }
            }

            return entries;
        }

        internal static List<string> Versions()
        {
            var versions = new List<string>();

            foreach (Entry entry in All())
            {
                if (!versions.Contains(entry.Version))
                    versions.Add(entry.Version);
            }

            versions.Sort(StringComparer.Ordinal);

            return versions;
        }

        // Writes every file of one version, returning their relative
        // paths. Done in a single pass over the archive rather than
        // reopening it per file.
        internal static List<string> Extract(string version, string target)
        {
            var written = new List<string>();

            using (Stream stream = OpenArchive())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    string full = entry.FullName.Replace('\\', '/');

                    int cut = full.IndexOf('/');

                    if (cut <= 0)
                        continue;

                    if (!string.Equals(full.Substring(0, cut), version,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    string relative = full
                        .Substring(cut + 1)
                        .Replace('/', Path.DirectorySeparatorChar);

                    string path = Path.Combine(target, relative);

                    string folder = Path.GetDirectoryName(path);

                    if (!string.IsNullOrEmpty(folder) &&
                        !Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    entry.ExtractToFile(path, true);

                    written.Add(relative);
                }
            }

            return written;
        }
    }
}
