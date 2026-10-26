using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // THE COLUMNS LEVELS AND ROOMS SHOWS, chosen as Excel hides columns
    // (user, 2026-10-25: a column chooser, so the sheet's columns do not
    // push the names off a laptop). Number, name and the airflows always
    // show; these can go. Remembered for the person, not the model, in
    // %AppData%\ATKINSMEC\autovent_columns.txt - each as on or off, so a
    // column added later takes its own default.
    //
    // Revit-free: tested by tests\arrangeflowtest.
    public static class VENTCOLUMNS
    {
        public const string Notes = "notes";
        public const string Room = "room";            // the model's m2, m
        public const string Categories = "category";  // the two occupancy categories and the nature of use
        public const string People = "people";        // occupants and units
        public const string SheetArea = "area";       // the sheet's m2
        public const string Calculation = "calc";     // the sheet's other columns, worked out again

        public class Choice
        {
            public string Key;
            public string Label;
            public bool ByDefault;
        }

        public static readonly Choice[] All =
        {
            new Choice { Key = Notes, Label = "Notes", ByDefault = true },
            new Choice { Key = Room, Label = "Room m², m", ByDefault = true },
            new Choice { Key = SheetArea, Label = "Sheet m²", ByDefault = false },
            new Choice { Key = Categories, Label = "Occupancy categories, use", ByDefault = true },
            new Choice { Key = People, Label = "Occupants and units", ByDefault = true },
            new Choice { Key = Calculation, Label = "Calculation columns", ByDefault = true }
        };

        public static HashSet<string> Defaults()
        {
            return new HashSet<string>(All.Where(c => c.ByDefault).Select(c => c.Key), StringComparer.Ordinal);
        }

        // Set elsewhere only by tests\arrangeflowtest
        private static string _path;

        public static string SettingsPath
        {
            get
            {
                return _path ?? System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATKINSMEC", "autovent_columns.txt");
            }
            set { _path = value; }
        }

        public static HashSet<string> Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return Parse(File.ReadAllText(SettingsPath));
            }
            catch
            {
                // the defaults, then
            }

            return Defaults();
        }

        public static void Save(ICollection<string> shown)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, Compose(shown));
            }
            catch
            {
                // not remembered: the next opening shows the defaults
            }
        }

        public static string Compose(ICollection<string> shown)
        {
            var text = new StringBuilder();

            foreach (Choice column in All)
                text.Append(column.Key).Append(shown != null && shown.Contains(column.Key) ? "\ton" : "\toff").Append('\n');

            return text.ToString();
        }

        // A column the text does not name takes its default
        public static HashSet<string> Parse(string text)
        {
            HashSet<string> shown = Defaults();

            foreach (string line in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                string[] f = line.Split('\t');

                if (f.Length < 2 || !All.Any(c => c.Key == f[0]))
                    continue;

                if (f[1] == "on")
                    shown.Add(f[0]);
                else if (f[1] == "off")
                    shown.Remove(f[0]);
            }

            return shown;
        }
    }
}
