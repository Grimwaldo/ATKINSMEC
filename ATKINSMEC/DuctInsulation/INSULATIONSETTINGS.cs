using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // Insulation rules per duct system type, kept in a small text file
    // so they survive between sessions and can be shared with the team.
    public class INSULATIONSETTINGS
    {
        public class Rule
        {
            public double ThicknessMm { get; set; }

            // Blank means "use the default type below"
            public string InsulationTypeName { get; set; }
        }

        public static bool Enabled { get; set; }

        // Only read from older settings files, where one type applied to
        // every system type. It is folded into the rules on load and is
        // no longer written out.
        public static string LegacyDefaultTypeName { get; set; }

        public static Dictionary<string, Rule> RulesBySystemType =
            new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase);

        private static bool _loaded;

        public static string SettingsPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC");

                return Path.Combine(folder, "duct_insulation.txt");
            }
        }

        public static Rule RuleFor(string systemTypeName)
        {
            Load();

            if (string.IsNullOrEmpty(systemTypeName))
                return null;

            Rule rule;

            if (RulesBySystemType.TryGetValue(systemTypeName.Trim(), out rule))
                return rule;

            return null;
        }

        // Each system type names its own insulation type; a blank one
        // means the system type is not insulated
        public static string TypeNameFor(string systemTypeName)
        {
            Rule rule = RuleFor(systemTypeName);

            return rule == null
                ? string.Empty
                : (rule.InsulationTypeName ?? string.Empty);
        }

        public static void Load()
        {
            if (_loaded)
                return;

            _loaded = true;

            Reload();
        }

        // Re-reads the file even when it has been read before
        public static void Reload()
        {
            RulesBySystemType.Clear();
            Enabled = false;
            LegacyDefaultTypeName = string.Empty;

            try
            {
                if (!File.Exists(SettingsPath))
                    return;

                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    string text = line.Trim();

                    if (text.Length == 0 || text.StartsWith("#"))
                        continue;

                    if (text.StartsWith("enabled=", StringComparison.OrdinalIgnoreCase))
                    {
                        Enabled = text.Substring(8).Trim() == "1";
                        continue;
                    }

                    if (text.StartsWith("type=", StringComparison.OrdinalIgnoreCase))
                    {
                        LegacyDefaultTypeName = text.Substring(5).Trim();
                        continue;
                    }

                    // <system type><TAB><mm>[<TAB><insulation type>]
                    string[] parts = text.Split('\t');

                    if (parts.Length < 2)
                        continue;

                    string systemType = parts[0].Trim();

                    double thickness;

                    if (!double.TryParse(
                            parts[1].Trim(),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out thickness))
                        continue;

                    RulesBySystemType[systemType] = new Rule
                    {
                        ThicknessMm = thickness,
                        InsulationTypeName = parts.Length > 2
                            ? parts[2].Trim()
                            : string.Empty
                    };
                }
                // Files written before the type became per system type
                // carried one default; bake it into the blank rules so
                // nothing silently stops being insulated
                if (!string.IsNullOrEmpty(LegacyDefaultTypeName))
                {
                    foreach (Rule rule in RulesBySystemType.Values)
                    {
                        if (string.IsNullOrEmpty(rule.InsulationTypeName))
                            rule.InsulationTypeName = LegacyDefaultTypeName;
                    }
                }
            }
            catch
            {
                // a damaged settings file must not break Revit
            }
        }

        public static void Save()
        {
            string folder = Path.GetDirectoryName(SettingsPath);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var text = new StringBuilder();

            text.AppendLine("# ATKINSMEC duct insulation rules");
            text.AppendLine("# <system type><TAB><thickness mm><TAB><insulation type>");
            text.AppendLine("# a system type with no insulation type is not insulated");
            text.AppendLine("enabled=" + (Enabled ? "1" : "0"));

            foreach (var pair in RulesBySystemType)
            {
                text.AppendLine(
                    pair.Key + "\t" +
                    pair.Value.ThicknessMm.ToString(CultureInfo.InvariantCulture) +
                    "\t" +
                    (pair.Value.InsulationTypeName ?? string.Empty));
            }

            File.WriteAllText(SettingsPath, text.ToString());

            _loaded = true;
        }
    }
}
