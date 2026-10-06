using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // What AutoSync was told to do, kept in a small text file beside the
    // duct insulation rules so it survives between sessions.
    //
    // Whether it is RUNNING is not in here. That is answered fresh every
    // time Revit starts, because leaving a thing that writes to the
    // central model switched on across sessions is not a decision to
    // make on somebody's behalf.
    public class SYNCSETTINGS
    {
        // The floor the user asked for. Anything under this is refused
        // rather than quietly raised, so nobody believes they set 1.
        public const int MinimumMinutes = 5;

        public static int Minutes { get; set; }
        public static bool Relinquish { get; set; }
        public static string Comment { get; set; }
        public static string Host { get; set; }

        // What was chosen last time Revit asked, so the question opens
        // on the answer that is usually wanted
        public static bool LastAnswer { get; set; }

        private static bool _loaded;

        public static string SettingsPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC");

                return Path.Combine(folder, "autosync.txt");
            }
        }

        public static void Load()
        {
            if (_loaded)
                return;

            _loaded = true;

            Defaults();

            try
            {
                if (!File.Exists(SettingsPath))
                    return;

                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    int cut = line.IndexOf('=');

                    if (cut <= 0)
                        continue;

                    string key = line.Substring(0, cut).Trim();
                    string value = line.Substring(cut + 1).Trim();

                    Apply(key, value);
                }
            }
            catch
            {
                // An unreadable settings file must not stop Revit
                // starting; the defaults are perfectly usable
                Defaults();
            }

            Minutes = Floor(Minutes);
        }

        private static void Defaults()
        {
            Minutes = 15;
            Relinquish = false;
            Comment = "Auto sync";
            Host = SYNCNET.DefaultHost;
            LastAnswer = false;
        }

        private static void Apply(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "minutes":
                    int minutes;

                    if (int.TryParse(value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out minutes))
                        Minutes = Floor(minutes);

                    break;

                case "relinquish":
                    Relinquish = Truthy(value);
                    break;

                case "comment":
                    Comment = value;
                    break;

                case "host":
                    Host = value;
                    break;

                case "lastanswer":
                    LastAnswer = Truthy(value);
                    break;
            }
        }

        private static bool Truthy(string value)
        {
            return string.Equals(value, "true",
                StringComparison.OrdinalIgnoreCase)
                || value == "1";
        }

        public static int Floor(int minutes)
        {
            return minutes < MinimumMinutes ? MinimumMinutes : minutes;
        }

        public static void Save()
        {
            try
            {
                string folder = Path.GetDirectoryName(SettingsPath);

                if (!string.IsNullOrEmpty(folder)
                    && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var text = new StringBuilder();

                text.AppendLine("# ATKINSMEC AutoSync");
                text.AppendLine("minutes=" + Floor(Minutes));
                text.AppendLine("relinquish=" + (Relinquish ? "true" : "false"));
                text.AppendLine("comment=" + (Comment ?? string.Empty));
                text.AppendLine("host=" + (Host ?? string.Empty));
                text.AppendLine("lastanswer=" + (LastAnswer ? "true" : "false"));

                File.WriteAllText(SettingsPath, text.ToString());
            }
            catch
            {
                // Nothing here is worth interrupting somebody's work for
            }
        }
    }
}
