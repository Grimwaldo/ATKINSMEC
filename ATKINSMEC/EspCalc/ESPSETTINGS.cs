using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // Everything the ESP calculation is told rather than reads from the
    // model, and the one file it is kept in. Revit-free.
    //
    // THE COEFFICIENT TABLE IS A STARTING POINT, NOT A STANDARD. The
    // built-in values are rough, typical figures chosen for this add-in
    // - NOT copied from the ASHRAE Duct Fitting Database or CIBSE Guide
    // C, whose tables are copyrighted. The window says so and lets the
    // office put its own in; a value typed there is saved and used from
    // then on.
    public class ESPSETTINGS
    {
        // How far apart two open duct ends in DIFFERENT models may be and
        // still be taken as one joint
        public double ToleranceMm = 150;

        // Added to the total, as a percentage
        public double MarginPercent = 10;

        public double Density = 1.2;

        // Galvanised steel, and flexible duct drawn straight
        public double RoughnessMm = 0.09;
        public double FlexRoughnessMm = 3.0;

        // An air terminal whose family states no pressure drop
        public double TerminalDropPa = 25;

        // The unit parameter the ESP is compared with and written to
        public string Parameter = "External Static Pressure";

        public List<Row> Table = DefaultTable();

        // "Family : Type" -> Pa, typed by a person. Beats anything the
        // model says, because typing it was a decision.
        public Dictionary<string, double> Families =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public class Row
        {
            public string Kind;

            // Diverging (air leaving the unit) and converging (air coming
            // back to it). A converging tee loses more than a diverging
            // one, which is why there are two.
            public double Supply;
            public double Return;

            public string Note = string.Empty;

            public Row Copy()
            {
                return new Row { Kind = Kind, Supply = Supply, Return = Return, Note = Note };
            }
        }

        // The kinds the calculation asks for. Two of them are what it
        // falls back on, and a step that lands on either is FLAGGED.
        public const string UnknownFitting = "Fitting (not recognised)";
        public const string UnknownAccessory = "Accessory (no data)";

        public static List<Row> DefaultTable()
        {
            return new List<Row>
            {
                new Row { Kind = "Elbow",          Supply = 0.25, Return = 0.25, Note = "smooth radius; a mitred elbow without vanes is far higher" },
                new Row { Kind = "Offset",         Supply = 0.50, Return = 0.50, Note = "about two elbows" },
                new Row { Kind = "Transition",     Supply = 0.15, Return = 0.15, Note = "on the faster side" },
                new Row { Kind = "Tee - through",  Supply = 0.10, Return = 0.30, Note = "on the downstream main" },
                new Row { Kind = "Tee - branch",   Supply = 0.80, Return = 0.90, Note = "on the branch" },
                new Row { Kind = "Wye - through",  Supply = 0.10, Return = 0.25, Note = "" },
                new Row { Kind = "Wye - branch",   Supply = 0.40, Return = 0.50, Note = "" },
                new Row { Kind = "Cross - through",Supply = 0.15, Return = 0.35, Note = "" },
                new Row { Kind = "Cross - branch", Supply = 1.00, Return = 1.10, Note = "" },
                new Row { Kind = "Tap",            Supply = 1.00, Return = 1.00, Note = "square-edged takeoff, on the branch; a bellmouth is lower" },
                new Row { Kind = "Union",          Supply = 0.00, Return = 0.00, Note = "" },
                new Row { Kind = "Cap",            Supply = 0.00, Return = 0.00, Note = "" },
                new Row { Kind = "Damper",         Supply = 0.20, Return = 0.20, Note = "open volume control damper" },
                new Row { Kind = UnknownAccessory, Supply = 0.50, Return = 0.50, Note = "FLAGGED - give the family a value" },
                new Row { Kind = UnknownFitting,   Supply = 0.50, Return = 0.50, Note = "FLAGGED - the family does not say what it is" }
            };
        }

        public static ESPSETTINGS Defaults()
        {
            return new ESPSETTINGS();
        }

        // The coefficient for a kind; false when the kind is not in the
        // table at all (the unknown-fitting row is used instead)
        public bool Coefficient(string kind, bool converging, out double c)
        {
            Row row = Table.FirstOrDefault(r =>
                string.Equals(r.Kind, kind, StringComparison.OrdinalIgnoreCase));

            if (row == null)
            {
                row = Table.FirstOrDefault(r => r.Kind == UnknownFitting);
                c = row == null ? 0.5 : (converging ? row.Return : row.Supply);
                return false;
            }

            c = converging ? row.Return : row.Supply;
            return true;
        }

        public bool FamilyValue(string family, out double pa)
        {
            pa = 0;

            return !string.IsNullOrEmpty(family) &&
                   Families.TryGetValue(family.Trim(), out pa);
        }

        // Is the table still the built-in one? Said in the window and the
        // export, so nobody mistakes the starting values for the office's.
        public bool TableIsBuiltIn()
        {
            List<Row> builtIn = DefaultTable();

            if (builtIn.Count != Table.Count)
                return false;

            for (int i = 0; i < builtIn.Count; i++)
            {
                Row a = builtIn[i];
                Row b = Table[i];

                if (!string.Equals(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase) ||
                    Math.Abs(a.Supply - b.Supply) > 1e-9 ||
                    Math.Abs(a.Return - b.Return) > 1e-9)
                {
                    return false;
                }
            }

            return true;
        }

        // ==================================================
        // The file
        // ==================================================
        //
        // One line per value, TAB-separated, numbers in the invariant
        // culture - a de-DE machine must not write "1,2" and read back 12.
        // The same shape as NWCSETTINGS.

        public static string SettingsPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC");

                return Path.Combine(folder, "esp_settings.txt");
            }
        }

        private static string N(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Clean(string text)
        {
            return (text ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        public string Compose()
        {
            var sb = new StringBuilder();

            sb.AppendLine("tolerance\t" + N(ToleranceMm));
            sb.AppendLine("margin\t" + N(MarginPercent));
            sb.AppendLine("density\t" + N(Density));
            sb.AppendLine("roughness\t" + N(RoughnessMm));
            sb.AppendLine("flexroughness\t" + N(FlexRoughnessMm));
            sb.AppendLine("terminal\t" + N(TerminalDropPa));
            sb.AppendLine("parameter\t" + Clean(Parameter));

            foreach (Row row in Table)
            {
                sb.AppendLine("kind\t" + Clean(row.Kind) + "\t" + N(row.Supply) + "\t" +
                              N(row.Return) + "\t" + Clean(row.Note));
            }

            foreach (KeyValuePair<string, double> family in Families)
                sb.AppendLine("family\t" + Clean(family.Key) + "\t" + N(family.Value));

            return sb.ToString();
        }

        private static bool Number(string text, out double value)
        {
            return double.TryParse(
                text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static ESPSETTINGS Parse(string text)
        {
            var settings = new ESPSETTINGS();

            if (string.IsNullOrEmpty(text))
                return settings;

            var table = new List<Row>();

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');

                if (line.Length == 0)
                    continue;

                string[] f = line.Split('\t');

                double v;

                switch (f[0])
                {
                    case "tolerance":
                        if (f.Length > 1 && Number(f[1], out v) && v > 0) settings.ToleranceMm = v;
                        break;
                    case "margin":
                        if (f.Length > 1 && Number(f[1], out v) && v >= 0) settings.MarginPercent = v;
                        break;
                    case "density":
                        if (f.Length > 1 && Number(f[1], out v) && v > 0) settings.Density = v;
                        break;
                    case "roughness":
                        if (f.Length > 1 && Number(f[1], out v) && v >= 0) settings.RoughnessMm = v;
                        break;
                    case "flexroughness":
                        if (f.Length > 1 && Number(f[1], out v) && v >= 0) settings.FlexRoughnessMm = v;
                        break;
                    case "terminal":
                        if (f.Length > 1 && Number(f[1], out v) && v >= 0) settings.TerminalDropPa = v;
                        break;
                    case "parameter":
                        if (f.Length > 1 && f[1].Trim().Length > 0) settings.Parameter = f[1].Trim();
                        break;
                    case "kind":
                        {
                            double s, r;

                            if (f.Length > 3 && f[1].Trim().Length > 0 &&
                                Number(f[2], out s) && Number(f[3], out r))
                            {
                                table.Add(new Row
                                {
                                    Kind = f[1].Trim(),
                                    Supply = s,
                                    Return = r,
                                    Note = f.Length > 4 ? f[4] : string.Empty
                                });
                            }
                        }
                        break;
                    case "family":
                        if (f.Length > 2 && f[1].Trim().Length > 0 && Number(f[2], out v) && v >= 0)
                            settings.Families[f[1].Trim()] = v;
                        break;
                }
            }

            if (table.Count > 0)
            {
                // The two fallback rows must exist whatever the file says,
                // or an unrecognised fitting would have nothing to use
                foreach (Row must in DefaultTable().Where(
                             d => d.Kind == UnknownFitting || d.Kind == UnknownAccessory))
                {
                    if (!table.Any(t => string.Equals(t.Kind, must.Kind, StringComparison.OrdinalIgnoreCase)))
                        table.Add(must);
                }

                settings.Table = table;
            }

            return settings;
        }

        public static ESPSETTINGS Load()
        {
            try
            {
                return File.Exists(SettingsPath)
                    ? Parse(File.ReadAllText(SettingsPath))
                    : new ESPSETTINGS();
            }
            catch
            {
                return new ESPSETTINGS();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, Compose());
            }
            catch
            {
                // a settings file that cannot be written costs the next
                // session its choices, not this one its answer
            }
        }
    }
}
