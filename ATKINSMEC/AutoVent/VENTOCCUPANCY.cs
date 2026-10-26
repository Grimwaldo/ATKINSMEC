using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ATKINSMEC
{
    // OCCUPANCY CATEGORIES FROM ROOM NAMES - at the user's request
    // (2026-10-14): "automatically detect Occupancy Category and assign it
    // to the room for both fresh and exhaust".
    //
    //   - a RULE is "name contains" words -> a Table 6-1 category (fresh
    //     air, the sheet's column O) and a Table 6-4 category (exhaust,
    //     column AC), for rooms in an apartment, rooms not in one, or any.
    //     Either side may be blank: nothing is written there.
    //   - a room takes the MOST SPECIFIC rule that fits ("LIFT LOBBY"
    //     before "LOBBY"), a rule for its kind of room before one for any.
    //     A rule may instead say the name EQUALS its words (2026-10-19):
    //     that one wins over every "contains" rule.
    //   - only BLANK cells are filled - never one somebody chose - and
    //     only with a category the sheet's own list has (VLOOKUP needs it
    //     exactly). An update first follows the sheet: a room named like
    //     one already given a category gets that one (Learn).
    //   - the rules are the office's: built in (Defaults), edited in the
    //     Occupancy... window (VENTOCCUPANCYDIALOG) and kept on this PC,
    //     %AppData%\ATKINSMEC\autovent_occupancy.txt.
    //
    // ALSO, "Dwelling Unit" (the user's choice for apartments): the office
    // sheets decide a dwelling's occupants with IF(O=$H$470,"NA",...), and
    // $H$470 points 43 rows below the helper cell holding "Dwelling Unit"
    // in the office's own workbook (every pattern, by different amounts),
    // so the category gave #VALUE!. DwellingCheck points the test at the
    // Table 6-1 cell itself.
    //
    // Revit-free; tested in tests\officetest and tests\ventsettingstest.
    public static class VENTOCCUPANCY
    {
        public enum Scope { Any, Apartment, Other }

        public class Rule
        {
            public Scope Scope = Scope.Any;
            public string Words = string.Empty;
            public string Fresh = string.Empty;
            public string Exhaust = string.Empty;

            // "Equals" rather than "contains" (user, 2026-10-19): the name IS
            // the words - "LOBBY" then never takes LIFT LOBBY or LOBBY 2
            public bool Exact;

            // With the exhaust category (user, 2026-10-15 - Table 6-4's own
            // rates gave 0 L/s for apartment toilets and kitchens):
            //   Use      - "Nature of use": which of a per-unit category's
            //              two rates; the residential toilet has only the
            //              light-use one
            //   Units    - "No. of Units" when the fixture count left it blank
            // An override L/s ("Over-ride ASHRAE Value?" + "Airflow for
            // Balancing") was here for the apartment kitchen's 50 L/s until
            // 2026-10-19; the user had it undone - the kitchen is per unit
            // now, as the private toilet is (KitchenCheck).
            public string Use = string.Empty;
            public int Units;

            public Rule Clone()
            {
                return new Rule
                {
                    Scope = Scope, Words = Words, Fresh = Fresh, Exhaust = Exhaust, Exact = Exact,
                    Use = Use, Units = Units
                };
            }
        }

        public const string LightUse = "Light Use / Cont. Oper.";
        public const string HeavyUse = "Heavy Use / Cont. Oper.";

        // ==================================================
        // Which rule a room takes
        // ==================================================

        // A name's words: letters and digits, upper case - "G.RM" is G, RM
        public static string[] Tokens(string text)
        {
            return Regex.Split((text ?? string.Empty).ToUpperInvariant(), "[^A-Z0-9]+")
                        .Where(t => t.Length > 0)
                        .ToArray();
        }

        // Every word of the rule starts a word of the name - "BATH" finds
        // BATHROOM - except a word of one or two letters, which must BE
        // one ("WC", "G RM"), or "SP" would find every SPA and DISPLAY.
        // An "equals" rule: the same words in the same order, nothing more
        // - any case, any spaces or punctuation between them.
        public static bool Fits(Rule rule, string[] name)
        {
            string[] words = Tokens(rule.Words);

            if (words.Length == 0)
                return false;

            if (rule.Exact)
                return words.SequenceEqual(name ?? new string[0]);

            return words.All(w => name.Any(n => w.Length >= 3 ? n.StartsWith(w, StringComparison.Ordinal) : n == w));
        }

        // An "equals" rule before every "contains" rule
        private const int ExactFirst = 1000000;

        // The rule a room takes, or null: an "equals" rule first; else the
        // one saying the most about it (its words' length); one for its own
        // kind before one for any room; then the first in the list
        public static Rule RuleFor(string name, bool inApartment, IEnumerable<Rule> rules)
        {
            string[] tokens = Tokens(name);

            Rule best = null;
            int bestScore = -1;

            foreach (Rule rule in rules ?? Enumerable.Empty<Rule>())
            {
                if (rule == null)
                    continue;

                if ((rule.Scope == Scope.Apartment && !inApartment) || (rule.Scope == Scope.Other && inApartment))
                    continue;

                if (!Fits(rule, tokens))
                    continue;

                int score = (rule.Exact ? ExactFirst : Tokens(rule.Words).Sum(w => w.Length) * 2) +
                            (rule.Scope == Scope.Any ? 0 : 1);

                if (score > bestScore)
                {
                    best = rule;
                    bestScore = score;
                }
            }

            return best;
        }

        // ==================================================
        // The built-in rules
        // ==================================================

        private const string Dwelling = "Dwelling Unit";
        private const string ResidentialToilet = "Toilets - private (residential)";
        public const string ApartmentKitchen = "Residential kitchens- Apartments";

        public static List<Rule> Defaults()
        {
            var rules = new List<Rule>();

            Action<Scope, string, string, string> add = (scope, words, fresh, exhaust) =>
            {
                foreach (string w in words.Split('|'))
                    rules.Add(new Rule { Scope = scope, Words = w, Fresh = fresh, Exhaust = exhaust });
            };

            // The extras for the rules just added - the user (2026-10-15):
            // the office's Table 6-4 gave apartment toilets and kitchens 0
            Action<int, string, int> extra = (count, use, units) =>
            {
                foreach (Rule rule in rules.Skip(rules.Count - count))
                {
                    rule.Use = use;
                    rule.Units = units;
                }
            };

            // ---- inside an apartment: the dwelling unit (the user's
            // choice, 2026-10-14), its kitchen and toilets exhausted
            add(Scope.Apartment, "BEDROOM|BREDROOM|LIVING|DINING|FAMILY|MAJLIS|FOYER|DRESSING|STUDY|MAID|HALL|CORRIDOR",
                Dwelling, "");

            // One kitchen: per unit, as a private toilet (KitchenCheck) -
            // 50 L/s, or 25 L/s for light use
            add(Scope.Apartment, "KITCHEN|PANTRY", Dwelling, ApartmentKitchen);
            extra(2, string.Empty, 1);

            // 10 L/s a fixture - the residential toilet has only the light,
            // continuous rate; one fixture where none was counted
            add(Scope.Apartment, "ENSUITE|BATH|TOILET|POWDER|WC|W C|SHOWER", "", ResidentialToilet);
            extra(7, LightUse, 1);

            add(Scope.Apartment, "UTILITY|LAUNDRY", "Laundry rooms within dwelling units", "");

            // ---- anywhere else. A public toilet's fixtures are counted
            // from the model (Fixtures); with none counted it gets 0 L/s,
            // said in the export log
            add(Scope.Other, "TOILET|WC|W C|RESTROOM|LADIES|GENTS|LAVATORY|POWDER", "", "Toilets - public");

            // Shower rooms, too, have only the light rate
            add(Scope.Other, "SHOWER", "", "Shower rooms");
            extra(1, LightUse, 1);
            add(Scope.Other, "KITCHEN", "Kitchen (Cooking)", "Kitchen-Commercial");
            add(Scope.Other, "PANTRY|KITCHENETTE", "Break Room", "Kitchenettes");
            add(Scope.Other, "BEDROOM|GUEST ROOM", "Bedroom / Living Room", "");
            add(Scope.Other, "LAUNDRY", "Laundry rooms, Central", "");
            add(Scope.Other, "STORE|STORAGE", "Occupiable Storage Rooms for dry material", "");

            add(Scope.Any, "OFFICE", "Office Space", "");
            add(Scope.Any, "MEETING|CONFERENCE|BOARD ROOM|BOARDROOM", "Conference / Meeting", "");
            add(Scope.Any, "RECEPTION|WAITING", "Reception Areas", "");
            add(Scope.Any, "LOBBY|ENTRANCE", "Main Entry Lobbies", "");
            add(Scope.Any, "LIFT LOBBY|ELEVATOR LOBBY", "Common Corridors", "");
            // A fireman lobby is circulation in normal use, as a lift lobby
            // is - it was left blank until the user found FIREMAN LOBBY
            // with no category (2026-10-16). Kept as its own rule so the
            // plain LOBBY one does not make it a main entry lobby.
            add(Scope.Any, "FIRE LOBBY", "Common Corridors", "");
            add(Scope.Any, "CORRIDOR", "Corridors", "");
            add(Scope.Any, "STAIR", "", "");
            add(Scope.Any, "GARBAGE|TRASH|REFUSE|BIN|G RM|JANITOR|CLEANER", "", "Janitor closets, trash rooms, recycling");
            add(Scope.Any, "PARKING|CAR PARK|GARAGE", "", "Parking garages");
            add(Scope.Any, "GYM|FITNESS", "Health Club / Weight Rooms", "");
            add(Scope.Any, "PRAYER|MUSALLA|MOSQUE", "Places of religious workship", "");
            add(Scope.Any, "RESTAURANT", "Resturant Dining Rooms", "");
            add(Scope.Any, "CAFE|CAFETERIA", "Cafeteria / Fast Food Dining", "");
            add(Scope.Any, "SHOP|RETAIL", "Sales", "");
            add(Scope.Any, "CLASSROOM|CLASS", "Classrooms (age 9 plus)", "");
            add(Scope.Any, "LIBRARY", "Libraries", "");
            add(Scope.Any, "COPY|PRINT", "", "Copy, printing rooms");
            add(Scope.Any, "POOL", "Swimming (pool & deck)", "");
            add(Scope.Any, "MULTIPURPOSE|MULTI PURPOSE", "Multi-Purpose Assembly", "");
            add(Scope.Any, "LOCKER|CHANGING", "", "Locker/dressing rooms");
            add(Scope.Any, "SERVER|DATA", "Computer (not printing)", "");

            return rules;
        }

        // ==================================================
        // The rules on this PC
        // ==================================================

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC", "autovent_occupancy.txt");
            }
        }

        // The office's rules - or the built-in ones while none were saved.
        // A file that cannot be read is the same as none.
        public static List<Rule> Load(string path = null)
        {
            try
            {
                path = path ?? DefaultPath;

                if (File.Exists(path))
                    return WithBuiltIn(Parse(File.ReadAllText(path)));
            }
            catch
            {
                // the built-in rules, then
            }

            return Defaults();
        }

        // A SAVED FILE STILL GETS THE BUILT-IN VALUES ADDED SINCE IT WAS
        // SAVED (user, 2026-10-19). The file replaces the built-in rules
        // whole, so the apartment kitchen's 50 L/s, added on 2026-10-15,
        // never reached a file saved before it - KIFAF's kitchens came out
        // at 0 L/s. Now a saved rule with the same scope, words and both
        // categories as a built-in one takes the built-in nature of use
        // and units for each it leaves BLANK. A value typed in the file
        // always stands; blank cannot mean "none" for a rule the built-in
        // list gives one to.
        public static List<Rule> WithBuiltIn(List<Rule> saved)
        {
            if (saved == null)
                return new List<Rule>();

            List<Rule> builtIn = Defaults();

            foreach (Rule rule in saved)
            {
                if (rule == null)
                    continue;

                Rule same = builtIn.FirstOrDefault(b =>
                    b.Scope == rule.Scope && b.Exact == rule.Exact &&
                    Tokens(b.Words).SequenceEqual(Tokens(rule.Words)) &&
                    string.Equals((b.Fresh ?? string.Empty).Trim(), (rule.Fresh ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((b.Exhaust ?? string.Empty).Trim(), (rule.Exhaust ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

                if (same == null)
                    continue;

                if (string.IsNullOrWhiteSpace(rule.Use))
                    rule.Use = same.Use;

                if (rule.Units <= 0)
                    rule.Units = same.Units;
            }

            return saved;
        }

        public static bool Save(IList<Rule> rules, string path = null)
        {
            try
            {
                path = path ?? DefaultPath;

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Compose(rules), new UTF8Encoding(false));

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Back to the built-in rules: the file goes
        public static void Forget(string path = null)
        {
            try
            {
                path = path ?? DefaultPath;

                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // nothing to forget
            }
        }

        public static string Compose(IEnumerable<Rule> rules)
        {
            var text = new StringBuilder();

            text.Append("# ATKINSMEC AUTOVENT - occupancy categories from room names.\n");
            text.Append("# rule <tab> any|apartment|other <tab> name contains <tab> Table 6-1 <tab> Table 6-4\n");
            text.Append("#      <tab> nature of use <tab> units <tab> equals (blank: contains)\n");

            foreach (Rule rule in rules ?? Enumerable.Empty<Rule>())
            {
                if (rule == null || Tokens(rule.Words).Length == 0)
                    continue;

                text.Append("rule\t")
                    .Append(rule.Scope == Scope.Apartment ? "apartment" : rule.Scope == Scope.Other ? "other" : "any")
                    .Append('\t').Append(Clean(rule.Words))
                    .Append('\t').Append(Clean(rule.Fresh))
                    .Append('\t').Append(Clean(rule.Exhaust))
                    .Append('\t').Append(Clean(rule.Use))
                    .Append('\t').Append(rule.Units > 0 ? rule.Units.ToString(CultureInfo.InvariantCulture) : string.Empty)
                    .Append('\t').Append(rule.Exact ? "equals" : string.Empty)
                    .Append('\n');
            }

            return text.ToString();
        }

        public static List<Rule> Parse(string text)
        {
            var rules = new List<Rule>();

            foreach (string raw in (text ?? string.Empty).Split('\n'))
            {
                string[] f = raw.TrimEnd('\r').Split('\t');

                if (f.Length < 3 || f[0].Trim() != "rule" || Tokens(f[2]).Length == 0)
                    continue;

                string scope = f[1].Trim().ToLowerInvariant();

                int units;

                // A file from before the extras has five fields: none set.
                // The eighth is "equals"; from 2026-10-15 to 10-19 it was
                // an override L/s, and a number there reads as "contains".
                rules.Add(new Rule
                {
                    Scope = scope == "apartment" ? Scope.Apartment : scope == "other" ? Scope.Other : Scope.Any,
                    Words = f[2].Trim(),
                    Fresh = f.Length > 3 ? f[3].Trim() : string.Empty,
                    Exhaust = f.Length > 4 ? f[4].Trim() : string.Empty,
                    Use = f.Length > 5 ? f[5].Trim() : string.Empty,
                    Units = f.Length > 6 && int.TryParse(f[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out units) && units > 0
                        ? units : 0,
                    Exact = f.Length > 7 && string.Equals(f[7].Trim(), "equals", StringComparison.OrdinalIgnoreCase)
                });
            }

            return rules;
        }

        private static string Clean(string text)
        {
            return (text ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        // ==================================================
        // The sheet's own lists
        // ==================================================

        // A category as the list spells it - case, spaces and the kind of
        // dash aside - or null when the list has no such category
        public static string Pick(string wanted, IEnumerable<string> choices)
        {
            string key = Same(wanted);

            if (key.Length == 0 || choices == null)
                return null;

            return choices.FirstOrDefault(c => Same(c) == key);
        }

        private static string Same(string text)
        {
            string t = (text ?? string.Empty).Replace('–', '-').Replace('—', '-').ToLowerInvariant();

            return Regex.Replace(t, @"\s+", " ").Trim();
        }

        // What a category cell may hold: the list its drop-down offers, or
        // else the ASHRAE table its heading names (6-1 or 6-4). Null when
        // neither is there to check against.
        public static List<string> Choices(ExcelWorksheet ws, int col, int row, bool exhaust)
        {
            if (col <= 0)
                return null;

            return ListAt(ws, col, row) ?? Table(ws.Workbook, exhaust);
        }

        // The list a cell's drop-down offers, or null
        public static List<string> ListAt(ExcelWorksheet ws, int col, int row)
        {
            if (col <= 0)
                return null;

            try
            {
                foreach (var validation in ws.DataValidations)
                {
                    var list = validation as OfficeOpenXml.DataValidation.Contracts.IExcelDataValidationList;

                    if (list == null || !Covers(validation.Address.Address, row, col))
                        continue;

                    if (list.Formula.Values != null && list.Formula.Values.Count > 0)
                        return list.Formula.Values.ToList();

                    List<string> found = Range(ws.Workbook, list.Formula.ExcelFormula);

                    if (found != null && found.Count > 0)
                        return found;
                }
            }
            catch
            {
                // a drop-down EPPlus cannot read: whatever stands in for it
            }

            return null;
        }

        // Table 6-4's "nature of use" choices (its A43:A44), or the two
        // the office's workbook has
        public static List<string> Uses(ExcelWorkbook book)
        {
            ExcelWorksheet table = book.Worksheets.FirstOrDefault(w =>
                w.Name.IndexOf("Table 6-4", StringComparison.OrdinalIgnoreCase) >= 0);

            var found = new List<string>();

            if (table != null && table.Dimension != null)
            {
                for (int r = 30; r <= Math.Min(80, table.Dimension.End.Row); r++)
                {
                    string text = table.Cells[r, 1].Text.Trim();

                    if (text.IndexOf("Use /", StringComparison.OrdinalIgnoreCase) >= 0)
                        found.Add(text);
                }
            }

            return found.Count > 0 ? found : new List<string> { HeavyUse, LightUse };
        }

        // The Table 6-4 categories rated PER FIXTURE (its L/s * unit
        // columns, G and I) - the sheet multiplies them by "No. of Units"
        public static HashSet<string> PerUnit(ExcelWorkbook book)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);

            ExcelWorksheet table = book.Worksheets.FirstOrDefault(w =>
                w.Name.IndexOf("Table 6-4", StringComparison.OrdinalIgnoreCase) >= 0);

            if (table == null || table.Dimension == null)
                return found;

            for (int r = 4; r <= Math.Min(29, table.Dimension.End.Row); r++)
            {
                if (table.Cells[r, 7].Text.Trim().Length > 0 || table.Cells[r, 9].Text.Trim().Length > 0)
                    found.Add(Same(table.Cells[r, 1].Text));
            }

            return found;
        }

        private static bool Covers(string addresses, int row, int col)
        {
            foreach (string part in (addresses ?? string.Empty).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var a = new ExcelAddress(part);

                if (row >= a.Start.Row && row <= a.End.Row && col >= a.Start.Column && col <= a.End.Column)
                    return true;
            }

            return false;
        }

        // The text of every cell in "'Sheet'!$A$5:$A$103"
        private static List<string> Range(ExcelWorkbook book, string formula)
        {
            Match m = Regex.Match(formula ?? string.Empty, @"^=?'?(?<sheet>[^'!]+)'?!(?<range>\$?[A-Z]+\$?\d+:\$?[A-Z]+\$?\d+)$");

            if (!m.Success)
                return null;

            ExcelWorksheet sheet = book.Worksheets[m.Groups["sheet"].Value];

            if (sheet == null)
                return null;

            return sheet.Cells[m.Groups["range"].Value.Replace("$", string.Empty)]
                .Select(c => c.Text.Trim())
                .Where(t => t.Length > 0)
                .ToList();
        }

        // Table 6-1's categories (not its section headings - a row with no
        // rates at all), or Table 6-4's
        public static List<string> Table(ExcelWorkbook book, bool exhaust)
        {
            ExcelWorksheet table = book.Worksheets.FirstOrDefault(w =>
                w.Name.IndexOf(exhaust ? "Table 6-4" : "Table 6-1", StringComparison.OrdinalIgnoreCase) >= 0);

            if (table == null || table.Dimension == null)
                return null;

            var found = new List<string>();
            int first = exhaust ? 4 : 5;
            int last = exhaust ? 29 : 103;

            for (int r = first; r <= Math.Min(last, table.Dimension.End.Row); r++)
            {
                string name = table.Cells[r, 1].Text.Trim();

                if (name.Length == 0)
                    continue;

                bool heading = !exhaust && Enumerable.Range(2, 9).All(c => table.Cells[r, c].Text.Trim().Length == 0);

                if (!heading)
                    found.Add(name);
            }

            return found;
        }

        // The two lists from the add-in's own office workbook - what the
        // Occupancy... window offers
        public static void TemplateChoices(out List<string> fresh, out List<string> exhaust, out List<string> uses)
        {
            fresh = new List<string>();
            exhaust = new List<string>();
            uses = new List<string> { HeavyUse, LightUse };

            try
            {
                // Opened before any import or export has set it
                ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");

                using (var package = new ExcelPackage(new MemoryStream(VENTOFFICE.Template())))
                {
                    fresh = Table(package.Workbook, false) ?? new List<string>();
                    exhaust = Table(package.Workbook, true) ?? new List<string>();
                    uses = Uses(package.Workbook);
                }
            }
            catch
            {
                // the window still opens, with the rules' own names
            }
        }

        // ==================================================
        // "Dwelling Unit"
        // ==================================================

        // IF(RC[-3]=R470C8,"NA",...VLOOKUP(...'ASHRAE 62.1 - Table 6-1'...))
        // - the occupancy compared with a cell meant to hold "Dwelling
        // Unit" - is pointed at that Table 6-1 cell itself. Returns how many
        // formulas changed.
        public static int DwellingCheck(ExcelWorkbook book, string[] formulasR1C1)
        {
            if (formulasR1C1 == null)
                return 0;

            int changed = 0;

            for (int c = 0; c < formulasR1C1.Length; c++)
            {
                string f = formulasR1C1[c];

                if (string.IsNullOrEmpty(f) || f.IndexOf("\"NA\"", StringComparison.Ordinal) < 0)
                    continue;

                Match sheet = Regex.Match(f, @"'([^']*Table 6-1[^']*)'!");

                if (!sheet.Success)
                    continue;

                int row = DwellingRow(book, sheet.Groups[1].Value);

                if (row <= 0)
                    continue;

                string target = "'" + sheet.Groups[1].Value + "'!R" + row + "C1";

                string fixedFormula = Regex.Replace(f, @"=R\d+C\d+(?=,""NA"")", "=" + target);

                if (fixedFormula != f)
                {
                    formulasR1C1[c] = fixedFormula;
                    changed++;
                }
            }

            return changed;
        }

        // ==================================================
        // "Residential kitchens- Apartments", per unit
        // ==================================================

        // THE APARTMENT KITCHEN CALCULATED AS THE PRIVATE TOILET IS (user,
        // 2026-10-19: "adjust the excel sheet so it can calculate
        // residential kitchen apartment same as toilet private"). In the
        // office's workbook it came out at 0 L/s: Table 6-4's row for it
        // is empty (62.1 leaves dwellings to 62.2), and the exhaust-rate
        // formula gives a per-unit rate only to the categories its OR(...)
        // names - not this one - so the per-area rate, an empty cell, was
        // used. Two changes, each only where not made already:
        //
        //   - the empty row gets 62.1's own residential kitchen rates, as
        //     the "Other than Residences" row has them: 50 / 100 cfm, 25 /
        //     50 L/s a unit - the lower for light use (continuous), the
        //     higher otherwise (note G);
        //   - every OR(...) in the room row's formulas naming "Toilets -
        //     private" names the kitchen beside it: the per-unit rate then
        //     reads it, and the per-area rate leaves it alone, exactly as
        //     for the toilet.
        //
        // Its "No. of Units" is the rule's (1).
        public class Kitchen
        {
            public bool Rates;       // Table 6-4's empty row given its rates
            public int Formulas;     // room formulas now naming the kitchen
        }

        public static Kitchen KitchenCheck(ExcelWorkbook book, string[] formulasR1C1)
        {
            var done = new Kitchen();

            ExcelWorksheet table = book == null ? null : book.Worksheets.FirstOrDefault(w =>
                w.Name.IndexOf("Table 6-4", StringComparison.OrdinalIgnoreCase) >= 0);

            if (table == null || table.Dimension == null)
                return done;

            int row = 0;

            for (int r = 1; r <= Math.Min(100, table.Dimension.End.Row) && row == 0; r++)
            {
                if (Same(table.Cells[r, 1].Text) == Same(ApartmentKitchen))
                    row = r;
            }

            if (row == 0)
                return done;

            // The rates: cfm in B..D, L/s in G..I, as every per-unit row
            if (new[] { 2, 4, 5, 7, 9, 10 }.All(c => table.Cells[row, c].Text.Trim().Length == 0))
            {
                table.Cells[row, 2].Value = 50d;
                table.Cells[row, 3].Value = "/";
                table.Cells[row, 4].Value = 100d;
                table.Cells[row, 7].Value = 25d;
                table.Cells[row, 8].Value = "/";
                table.Cells[row, 9].Value = 50d;
                done.Rates = true;
            }

            // The formulas, by the table's own spelling of the name
            string name = table.Cells[row, 1].Text.Trim();

            for (int c = 0; formulasR1C1 != null && c < formulasR1C1.Length; c++)
            {
                string f = formulasR1C1[c];

                if (string.IsNullOrEmpty(f) || f.IndexOf("\"" + name + "\"", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string with = Regex.Replace(f, @"\((?<cell>[^()=,""]+)=""Toilets - private""\)",
                    m => m.Value + ",(" + m.Groups["cell"].Value + "=\"" + name + "\")");

                if (with != f)
                {
                    formulasR1C1[c] = with;
                    done.Formulas++;
                }
            }

            return done;
        }

        private static int DwellingRow(ExcelWorkbook book, string sheetName)
        {
            ExcelWorksheet table = book.Worksheets[sheetName];

            if (table == null || table.Dimension == null)
                return 0;

            for (int r = 1; r <= Math.Min(300, table.Dimension.End.Row); r++)
            {
                if (Same(table.Cells[r, 1].Text) == Same(Dwelling))
                    return r;
            }

            return 0;
        }

        // ==================================================
        // Filling the sheet
        // ==================================================

        public class Filled
        {
            public int Fresh;
            public int Exhaust;

            // Rules naming a category the sheet's list has not got
            public HashSet<string> Missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // "BEDROOM x11 -> Dwelling Unit / -", for the log
            public Dictionary<string, int> Given = new Dictionary<string, int>(StringComparer.Ordinal);

            // Rows that took a rule's nature of use or units
            public int Extras;

            // Rooms whose exhaust is per fixture with no "No. of Units" -
            // 0 L/s until the fixtures are counted or typed
            public List<string> NoUnits = new List<string>();

            // Apartments given their bedrooms and occupants, and how many
            // people in all (Dwellings)
            public int Apartments;
            public int Occupants;

            public int Total
            {
                get { return Fresh + Exhaust; }
            }
        }

        // Where the sheet already gave a category to rooms of a name, the
        // one it gave most - what an update follows first
        public class Learned
        {
            public Dictionary<string, string> Fresh = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, string> Exhaust = new Dictionary<string, string>(StringComparer.Ordinal);

            public static string Key(string name, bool inApartment)
            {
                return (inApartment ? "A|" : "O|") + string.Join(" ", Tokens(name));
            }

            public static Learned From(IEnumerable<Tuple<string, bool, string, string>> rooms)
            {
                var learned = new Learned();

                foreach (var side in new[] { true, false })
                {
                    var counts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

                    foreach (var room in rooms ?? Enumerable.Empty<Tuple<string, bool, string, string>>())
                    {
                        string category = ((side ? room.Item3 : room.Item4) ?? string.Empty).Trim();

                        if (category.Length == 0)
                            continue;

                        string key = Key(room.Item1, room.Item2);
                        Dictionary<string, int> mine;

                        if (!counts.TryGetValue(key, out mine))
                            counts[key] = mine = new Dictionary<string, int>(StringComparer.Ordinal);

                        int n;
                        mine.TryGetValue(category, out n);
                        mine[category] = n + 1;
                    }

                    foreach (var kv in counts)
                    {
                        string most = kv.Value.OrderByDescending(c => c.Value).First().Key;

                        if (side)
                            learned.Fresh[kv.Key] = most;
                        else
                            learned.Exhaust[kv.Key] = most;
                    }
                }

                return learned;
            }
        }

        // Every BLANK category cell of these rooms given its category: what
        // the sheet gave rooms of that name (an update), else the rule's -
        // and only one the sheet's list has
        public static Filled Fill(
            ExcelWorksheet ws, VENTSHEET.SheetLayout layout,
            IList<KeyValuePair<int, VENTTEMPLATE.Room>> rows,
            IList<Rule> rules, Learned learned)
        {
            var filled = new Filled();

            if (rules == null || rows == null || rows.Count == 0 ||
                (layout.FreshCategoryCol <= 0 && layout.ExhaustCategoryCol <= 0))
                return filled;

            int sample = rows[0].Key;

            List<string> freshList = Choices(ws, layout.FreshCategoryCol, sample, false);
            List<string> exhaustList = Choices(ws, layout.ExhaustCategoryCol, sample, true);
            List<string> useList = ListAt(ws, layout.UseCol, sample) ?? Uses(ws.Workbook);
            HashSet<string> perUnit = PerUnit(ws.Workbook);

            foreach (KeyValuePair<int, VENTTEMPLATE.Room> row in rows)
            {
                VENTTEMPLATE.Room room = row.Value;

                if (room == null || room.Keep)
                    continue;

                Rule rule = RuleFor(room.Name, room.InDwelling, rules);
                string key = Learned.Key(room.Name, room.InDwelling);

                string fresh = Put(ws, row.Key, layout.FreshCategoryCol, freshList,
                                   Followed(learned == null ? null : learned.Fresh, key) ?? (rule == null ? null : rule.Fresh),
                                   filled);

                string exhaust = Put(ws, row.Key, layout.ExhaustCategoryCol, exhaustList,
                                     Followed(learned == null ? null : learned.Exhaust, key) ?? (rule == null ? null : rule.Exhaust),
                                     filled);

                // The rule's nature of use and units - when the row's
                // exhaust category IS the rule's, whoever wrote it
                string extras = string.Empty;

                if (rule != null && layout.ExhaustCategoryCol > 0 && Same(rule.Exhaust).Length > 0 &&
                    Same(ws.Cells[row.Key, layout.ExhaustCategoryCol].Text) == Same(rule.Exhaust))
                {
                    extras = Extras(ws, layout, row.Key, rule, useList);

                    if (extras.Length > 0)
                        filled.Extras++;
                }

                if (fresh != null)
                    filled.Fresh++;

                if (exhaust != null)
                    filled.Exhaust++;

                if (fresh != null || exhaust != null || extras.Length > 0)
                {
                    string said = (room.Name ?? string.Empty).Trim() + " -> " + (fresh ?? "-") + " / " + (exhaust ?? "-") + extras;
                    int n;
                    filled.Given.TryGetValue(said, out n);
                    filled.Given[said] = n + 1;
                }

                // Per fixture and still no fixtures: 0 L/s - said, not hidden
                if (layout.ExhaustCategoryCol > 0 &&
                    perUnit.Contains(Same(ws.Cells[row.Key, layout.ExhaustCategoryCol].Text)) &&
                    (layout.UnitsCol <= 0 || Blank(ws.Cells[row.Key, layout.UnitsCol])) &&
                    (layout.BalancingCol <= 0 || Blank(ws.Cells[row.Key, layout.BalancingCol])))
                {
                    filled.NoUnits.Add((room.Number + " " + room.Name).Trim());
                }
            }

            return filled;
        }

        // A rule's extras into the row's blank cells; said briefly, or ""
        private static string Extras(
            ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int row, Rule rule,
            List<string> useList)
        {
            var said = new List<string>();

            if (layout.UseCol > 0 && !string.IsNullOrWhiteSpace(rule.Use) && Blank(ws.Cells[row, layout.UseCol]))
            {
                string use = useList == null ? rule.Use.Trim() : Pick(rule.Use, useList);

                if (use != null)
                {
                    ws.Cells[row, layout.UseCol].Value = use;
                    said.Add(Same(use).StartsWith("light") ? "light use" : Same(use).StartsWith("heavy") ? "heavy use" : use);
                }
            }

            if (layout.UnitsCol > 0 && rule.Units > 0 && Blank(ws.Cells[row, layout.UnitsCol]))
            {
                ws.Cells[row, layout.UnitsCol].Value = rule.Units;
                said.Add(rule.Units + " unit" + (rule.Units == 1 ? string.Empty : "s"));
            }

            return said.Count == 0 ? string.Empty : " (" + string.Join(", ", said) + ")";
        }

        private static bool Blank(ExcelRange cell)
        {
            return string.IsNullOrEmpty(cell.Formula) && (cell.Value == null || cell.Text.Trim().Length == 0);
        }

        // ==================================================
        // An apartment's bedrooms and occupants
        // ==================================================

        // A bedroom by its name: BEDROOM, MASTER BEDROOM, BEDROOM 2, BED RM
        public static bool IsBedroom(string name)
        {
            string[] tokens = Tokens(name);

            return tokens.Any(t => t.StartsWith("BEDROOM", StringComparison.Ordinal) ||
                                   t.StartsWith("BREDROOM", StringComparison.Ordinal)) ||
                   (tokens.Contains("BED") && (tokens.Contains("ROOM") || tokens.Contains("RM")));
        }

        // EVERY APARTMENT'S "No of Bedrooms" - its rooms named bedroom - and
        // its people, bedrooms + 1 (the user, 2026-10-15): 2 in the master
        // bedroom (else the first), 1 in each other; an apartment with none,
        // 1 in its first Dwelling Unit room. Blank cells only, never an
        // unticked room's. Returns how many apartments were given any.
        public static int Dwellings(
            ExcelWorksheet ws, VENTSHEET.SheetLayout layout,
            IList<KeyValuePair<int, VENTTEMPLATE.Room>> rows, Filled filled)
        {
            if (rows == null || (layout.BedroomsCol <= 0 && layout.OccupantsCol <= 0))
                return 0;

            int given = 0;

            foreach (var apartment in rows
                         .Where(r => r.Value != null && !string.IsNullOrEmpty(r.Value.ApartmentKey))
                         .GroupBy(r => r.Value.ApartmentKey))
            {
                List<KeyValuePair<int, VENTTEMPLATE.Room>> mine = apartment.OrderBy(r => r.Key).ToList();
                List<KeyValuePair<int, VENTTEMPLATE.Room>> bedrooms = mine.Where(r => IsBedroom(r.Value.Name)).ToList();

                bool wrote = false;

                // On the apartment's first row - merged down over the rest
                int top = mine[0].Key;

                if (layout.BedroomsCol > 0 && !mine[0].Value.Keep && Blank(ws.Cells[top, layout.BedroomsCol]))
                {
                    ws.Cells[top, layout.BedroomsCol].Value = bedrooms.Count;
                    wrote = true;
                }

                if (layout.OccupantsCol > 0)
                {
                    var people = new List<KeyValuePair<KeyValuePair<int, VENTTEMPLATE.Room>, int>>();

                    if (bedrooms.Count == 0)
                    {
                        KeyValuePair<int, VENTTEMPLATE.Room> living = mine.FirstOrDefault(r =>
                            layout.FreshCategoryCol > 0 &&
                            Same(ws.Cells[r.Key, layout.FreshCategoryCol].Text) == Same(Dwelling));

                        people.Add(new KeyValuePair<KeyValuePair<int, VENTTEMPLATE.Room>, int>(
                            living.Value == null ? mine[0] : living, 1));
                    }
                    else
                    {
                        KeyValuePair<int, VENTTEMPLATE.Room> master = bedrooms.FirstOrDefault(r => Tokens(r.Value.Name).Contains("MASTER"));

                        if (master.Value == null)
                            master = bedrooms[0];

                        foreach (KeyValuePair<int, VENTTEMPLATE.Room> bedroom in bedrooms)
                        {
                            people.Add(new KeyValuePair<KeyValuePair<int, VENTTEMPLATE.Room>, int>(
                                bedroom, bedroom.Key == master.Key ? 2 : 1));
                        }
                    }

                    foreach (var person in people)
                    {
                        if (person.Key.Value.Keep || !Blank(ws.Cells[person.Key.Key, layout.OccupantsCol]))
                            continue;

                        ws.Cells[person.Key.Key, layout.OccupantsCol].Value = person.Value;
                        filled.Occupants += person.Value;
                        wrote = true;
                    }
                }

                if (wrote)
                    given++;
            }

            filled.Apartments += given;

            return given;
        }

        private static string Followed(Dictionary<string, string> learned, string key)
        {
            string category;

            return learned != null && learned.TryGetValue(key, out category) ? category : null;
        }

        // The category in a blank cell; what was written, or null
        private static string Put(
            ExcelWorksheet ws, int row, int col, List<string> choices, string wanted, Filled filled)
        {
            if (col <= 0 || string.IsNullOrWhiteSpace(wanted))
                return null;

            ExcelRange cell = ws.Cells[row, col];

            if (!string.IsNullOrEmpty(cell.Formula) || (cell.Value != null && cell.Text.Trim().Length > 0))
                return null;

            string category = choices == null ? wanted.Trim() : Pick(wanted, choices);

            if (category == null)
            {
                filled.Missing.Add(wanted.Trim());
                return null;
            }

            cell.Value = category;

            return category;
        }
    }
}
