using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // INSULATOR's rules: one set for ducts, one for pipes, each in its own
    // small text file so they survive between sessions and can be shared
    // with the team. Revit-free (tests\insulatortest).
    //
    // The duct file is the one DUCT INSULATION always wrote
    // (duct_insulation.txt) - saved rules carry straight over; a file
    // without an "accessories=" line insulates duct accessories, as that
    // tool always did. Pipe accessories are off until ticked.
    public class INSULATIONSETTINGS
    {
        public class Rule
        {
            public double ThicknessMm { get; set; }

            // Blank means the system type is not insulated
            public string InsulationTypeName { get; set; }
        }

        public static readonly INSULATIONSETTINGS Ducts =
            new INSULATIONSETTINGS("duct_insulation.txt", "duct", true);

        public static readonly INSULATIONSETTINGS Pipes =
            new INSULATIONSETTINGS("pipe_insulation.txt", "pipe", false);

        // %AppData%\ATKINSMEC; tests point it at a folder of their own
        public static string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ATKINSMEC");

        private readonly string _file;
        private readonly bool _accessoriesByDefault;

        // "duct" / "pipe" - for the file's own heading
        public string Noun { get; private set; }

        public bool Enabled { get; set; }

        // Accessories insulated as well as the runs and fittings
        public bool Accessories { get; set; }

        // Only read from older settings files, where one type applied to
        // every system type. It is folded into the rules on load and is
        // no longer written out.
        public string LegacyDefaultTypeName { get; set; }

        public Dictionary<string, Rule> RulesBySystemType =
            new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase);

        private bool _loaded;

        private INSULATIONSETTINGS(string file, string noun, bool accessoriesByDefault)
        {
            _file = file;
            Noun = noun;
            _accessoriesByDefault = accessoriesByDefault;
            Accessories = accessoriesByDefault;
            LegacyDefaultTypeName = string.Empty;
        }

        public string SettingsPath
        {
            get { return Path.Combine(Folder, _file); }
        }

        // Whether an element of this kind is insulated at all: the side is
        // switched on, and an accessory only when accessories are ticked
        public bool Covers(bool accessory)
        {
            Load();

            return Enabled && (!accessory || Accessories);
        }

        public Rule RuleFor(string systemTypeName)
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
        public string TypeNameFor(string systemTypeName)
        {
            Rule rule = RuleFor(systemTypeName);

            return rule == null
                ? string.Empty
                : (rule.InsulationTypeName ?? string.Empty);
        }

        public void Load()
        {
            if (_loaded)
                return;

            _loaded = true;

            Reload();
        }

        // Re-reads the file even when it has been read before
        public void Reload()
        {
            _loaded = true;

            RulesBySystemType.Clear();
            Enabled = false;
            Accessories = _accessoriesByDefault;
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

                    if (text.StartsWith("accessories=", StringComparison.OrdinalIgnoreCase))
                    {
                        Accessories = text.Substring(12).Trim() == "1";
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

        public void Save()
        {
            string folder = Path.GetDirectoryName(SettingsPath);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var text = new StringBuilder();

            text.AppendLine("# ATKINSMEC " + Noun + " insulation rules (INSULATOR)");
            text.AppendLine("# <system type><TAB><thickness mm><TAB><insulation type>");
            text.AppendLine("# a system type with no insulation type is not insulated");
            text.AppendLine("enabled=" + (Enabled ? "1" : "0"));
            text.AppendLine("accessories=" + (Accessories ? "1" : "0"));

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
