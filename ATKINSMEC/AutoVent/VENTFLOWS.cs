using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ATKINSMEC
{
    // THE AIRFLOWS IN ARRANGE ROOMS... - at the user's request (2026-10-19):
    // "allow the addin the option to read and write the airflows of the
    // rooms in 'arrange rooms' window, it should show the flowrates in a
    // form of columns inside the window and detect which rooms that are
    // merged". Asked: read from the sheet AND the terminals, side by side;
    // write to the terminals; merges in the sheet shown and adopted.
    //
    //   - the SHEET's air: each room's row as the airflow run finds it
    //     (VENTSHEET.Resolve - the window does that), with its merged cells;
    //   - what the TERMINALS carry now: read by the airflow run's own walk
    //     (REVITROOMS, Mode.Survey) - a room's own terminals, and its part
    //     of every endcap on the units serving it (Attribute) - so the
    //     column is what a run would have put there, read back;
    //   - what to WRITE: the window's values, onto the rooms picked only
    //     (REVITROOMS, Mode.Write; Overrides).
    //
    // A MERGED GROUP IS ONE LINE OF AIR - the sheet's merged cells or the
    // arrangement's Merge, as the run treats them (VENTFCUCHECK.Groups).
    //
    // Revit-free; tests\arrangeflowtest.
    public static class VENTFLOWS
    {
        // Litres per second, per side. NaN: none - no row in the sheet, no
        // terminal, or a side not to be written.
        public class Air
        {
            public double Fresh = double.NaN;
            public double Extract = double.NaN;

            public Air Copy()
            {
                return new Air { Fresh = Fresh, Extract = Extract };
            }
        }

        // A room's row in the sheet: its air, and the merged cells it is in
        // ("fresh <address>", "extract <address>" - one per side merged)
        public class Sheet
        {
            public Air Air = new Air();
            public List<string> Merges = new List<string>();

            // What explains its air (VENTSHEET.AirData), read only: the two
            // occupancy categories ("" blank) - and, where one is blank,
            // what an export would fill it with (VENTOCCUPANCY; null none) -
            // its occupants, units and area (NaN none)
            public string FreshCategory = string.Empty;
            public string ExhaustCategory = string.Empty;
            public string FreshSuggested;
            public string ExhaustSuggested;
            public double Occupants = double.NaN;
            public double Units = double.NaN;
            public double AreaM2 = double.NaN;

            // ...its nature of use ("" blank), and what the occupancy rule
            // for its name adds when its exhaust category is the rule's
            // (VENTOCCUPANCY.Fill's extras): a nature of use, units (0 none)
            public string Use = string.Empty;
            public string RuleExhaust;
            public string RuleUse;
            public double RuleUnits;
        }

        // ==================================================
        // The occupancy chosen, and its new airflows
        // ==================================================
        //
        // THE OCCUPANCY OF A ROOM, CHOSEN AS IN THE SHEET (user, 2026-10-26:
        // "select the occupancy of the space just like the excel sheet ...
        // put the new values that are about to be changed in another columns
        // called 'new values'"): its two categories, nature of use,
        // occupants and units - the arrangement's (VENTARRANGE.RoomOccupancy,
        // saved), never the workbook's - worked through the sheet's own
        // formulas (VENTWHATIF, the main window's) into its NEW air. Model
        // writes the new air; Sheet the sheet's.

        public const string FreshCategoryField = "fresh-category";
        public const string ExhaustCategoryField = "extract-category";
        public const string UseField = "use";
        public const string OccupantsField = "occupants";
        public const string UnitsField = "units";

        // Another column of the sheet's calculation, as the sheet heads it
        public class CalcColumn
        {
            public int Col;
            public string Letter = string.Empty;
            public string Label = string.Empty;
            public string Units = string.Empty;
            public string Heading = string.Empty;
        }

        // A room's row worked out: its new air (its final columns - 0 where
        // blank, as the sheet's are read), the error there if any, and its
        // calculation columns' text by sheet column
        public class Calc
        {
            public Air Air = new Air { Fresh = 0, Extract = 0 };
            public string FreshError;
            public string ExtractError;
            public Dictionary<int, string> Cells = new Dictionary<int, string>();

            // ...and, worked out with a choice, those cells as the sheet has
            // them (null: as the sheet)
            public Dictionary<int, string> SheetCells;

            public Calc Copy()
            {
                return new Calc
                {
                    Air = Air.Copy(),
                    FreshError = FreshError,
                    ExtractError = ExtractError,
                    Cells = new Dictionary<int, string>(Cells),
                    SheetCells = SheetCells == null ? null : new Dictionary<int, string>(SheetCells)
                };
            }
        }

        // WHAT THE NEW AIR IS WORKED OUT WITH - handed to the window by the
        // main window: the calculation's columns, the lists a cell offers
        // (null: none - typed), and the sheet's formulas over the choices
        // as they stand, every room's row back
        public class Formulas
        {
            public List<CalcColumn> Columns = new List<CalcColumn>();
            public List<string> FreshChoices;
            public List<string> ExhaustChoices;
            public List<string> UseChoices;
            public Func<IDictionary<string, VENTARRANGE.RoomOccupancy>, Dictionary<string, Calc>> Calculate;
        }

        // A field as the sheet has it, as text ("" blank)
        public static string SheetText(Sheet sheet, string field)
        {
            if (sheet == null)
                return string.Empty;

            switch (field)
            {
                case FreshCategoryField: return sheet.FreshCategory ?? string.Empty;
                case ExhaustCategoryField: return sheet.ExhaustCategory ?? string.Empty;
                case UseField: return sheet.Use ?? string.Empty;
                case OccupantsField: return Figure(sheet.Occupants);
                case UnitsField: return Figure(sheet.Units);
                default: return string.Empty;
            }
        }

        // ...as chosen: null where it follows the sheet
        public static string ChosenText(VENTARRANGE.RoomOccupancy chosen, string field)
        {
            if (chosen == null)
                return null;

            switch (field)
            {
                case FreshCategoryField: return chosen.FreshCategory;
                case ExhaustCategoryField: return chosen.ExhaustCategory;
                case UseField: return chosen.Use;
                case OccupantsField: return chosen.Occupants.HasValue ? Figure(chosen.Occupants.Value) : null;
                case UnitsField: return chosen.Units.HasValue ? Figure(chosen.Units.Value) : null;
                default: return null;
            }
        }

        // A count as the window shows it: "" for none
        public static string Figure(double value)
        {
            return double.IsNaN(value) ? string.Empty : Math.Round(value, 2).ToString("0.##", CultureInfo.CurrentCulture);
        }

        // ONE FIELD OF A ROOM CHOSEN: null = as the sheet, "" = blank, else
        // the value (a number for occupants and units). A choice that is
        // what the sheet has follows the sheet, so a later change to the
        // sheet comes through. False for a number that is not one.
        public static bool Choose(VENTARRANGE.Arrangement arrangement, string key, string field, string text, Sheet sheet)
        {
            if (arrangement == null || key == null)
                return false;

            VENTARRANGE.RoomOccupancy chosen;

            chosen = arrangement.Occupancy.TryGetValue(key, out chosen) ? chosen.Copy() : new VENTARRANGE.RoomOccupancy();

            string value = text == null ? null : text.Trim();

            if (value != null && Same(value, SheetText(sheet, field), field))
                value = null;

            switch (field)
            {
                case FreshCategoryField:
                    chosen.FreshCategory = value;
                    break;

                case ExhaustCategoryField:
                    chosen.ExhaustCategory = value;
                    break;

                case UseField:
                    chosen.Use = value;
                    break;

                case OccupantsField:
                case UnitsField:
                    double? number = null;

                    if (value != null)
                    {
                        double? read = Read(value);

                        if (read == null)
                            return false;

                        number = read.Value;
                    }

                    if (field == OccupantsField)
                        chosen.Occupants = number;
                    else
                        chosen.Units = number;

                    break;

                default:
                    return false;
            }

            if (chosen.IsEmpty)
                arrangement.Occupancy.Remove(key);
            else
                arrangement.Occupancy[key] = chosen;

            return true;
        }

        private static bool Same(string a, string b, string field)
        {
            if (field == OccupantsField || field == UnitsField)
            {
                double? x = Read(a);
                double? y = Read(b);

                return x.HasValue && y.HasValue &&
                       ((double.IsNaN(x.Value) && double.IsNaN(y.Value)) || Math.Abs(x.Value - y.Value) < 1e-9);
            }

            return Key(a) == Key(b);
        }

        // A category as a list spells it - case, spaces and the kind of
        // dash aside (VENTOCCUPANCY.Pick) - or null when the list has none.
        // With no list, as given.
        public static string PickFrom(string wanted, IEnumerable<string> choices)
        {
            string key = Key(wanted);

            if (key.Length == 0)
                return null;

            if (choices == null)
                return wanted.Trim();

            return choices.FirstOrDefault(c => Key(c) == key);
        }

        private static string Key(string text)
        {
            string t = (text ?? string.Empty).Replace('\u2013', '-').Replace('\u2014', '-').ToLowerInvariant();

            return System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
        }

        // The field as it stands: chosen, else the sheet's
        public static string Effective(VENTARRANGE.Arrangement arrangement, string key, string field, Sheet sheet)
        {
            VENTARRANGE.RoomOccupancy chosen = null;

            if (arrangement != null && key != null)
                arrangement.Occupancy.TryGetValue(key, out chosen);

            return ChosenText(chosen, field) ?? SheetText(sheet, field);
        }

        // OCCUPANCY FROM THE RULES, BLANK ONES ONLY (the user's choice): each
        // room's blank category given what an export would fill it with -
        // only one its list has - and, where its exhaust category is then
        // the rule's, the rule's nature of use and units in blank cells (as
        // VENTOCCUPANCY.Fill). Returns how many rooms took any.
        public static int FromRules(VENTARRANGE.Arrangement arrangement, IEnumerable<string> keys,
                                    IDictionary<string, Sheet> sheet, Formulas formulas)
        {
            int rooms = 0;

            if (arrangement == null || sheet == null)
                return 0;

            foreach (string key in keys ?? Enumerable.Empty<string>())
            {
                Sheet row;

                if (key == null || !sheet.TryGetValue(key, out row) || row == null)
                    continue;

                bool took = false;

                Func<string, bool> blank = field => Effective(arrangement, key, field, row).Length == 0;

                if (blank(FreshCategoryField) && row.FreshSuggested != null)
                {
                    string fresh = PickFrom(row.FreshSuggested, formulas == null ? null : formulas.FreshChoices);

                    if (fresh != null)
                        took |= Choose(arrangement, key, FreshCategoryField, fresh, row);
                }

                if (blank(ExhaustCategoryField) && row.ExhaustSuggested != null)
                {
                    string exhaust = PickFrom(row.ExhaustSuggested, formulas == null ? null : formulas.ExhaustChoices);

                    if (exhaust != null)
                        took |= Choose(arrangement, key, ExhaustCategoryField, exhaust, row);
                }

                if (row.RuleExhaust != null && Key(Effective(arrangement, key, ExhaustCategoryField, row)) == Key(row.RuleExhaust))
                {
                    if (blank(UseField) && !string.IsNullOrWhiteSpace(row.RuleUse))
                    {
                        string use = PickFrom(row.RuleUse, formulas == null ? null : formulas.UseChoices);

                        if (use != null)
                            took |= Choose(arrangement, key, UseField, use, row);
                    }

                    if (blank(UnitsField) && row.RuleUnits > 0)
                        took |= Choose(arrangement, key, UnitsField, row.RuleUnits.ToString(CultureInfo.InvariantCulture), row);
                }

                if (took)
                    rooms++;
            }

            return rooms;
        }

        // THE SHEET'S AREA IS NOT THE MODEL'S: the architect changed the room
        // since the sheet was written. Rounding is not a change - 0.5 m2.
        public static bool AreaChanged(double sheetM2, double modelM2)
        {
            return !double.IsNaN(sheetM2) && modelM2 > 0 && Math.Abs(sheetM2 - modelM2) > 0.5;
        }

        // ==================================================
        // Merges
        // ==================================================

        // The rooms sharing a merged cell in the sheet - each set once, the
        // rooms in the order given
        // `side`: "fresh" or "extract" - that side's merged cells alone (a
        // merge is one side's since 2026-10-22); null, both.
        public static List<List<string>> SheetMerges(IEnumerable<string> keys, IDictionary<string, Sheet> sheet, string side = null)
        {
            var sets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (string key in keys ?? Enumerable.Empty<string>())
            {
                Sheet row;

                if (key == null || sheet == null || !sheet.TryGetValue(key, out row) || row == null)
                    continue;

                foreach (string token in row.Merges ?? new List<string>())
                {
                    if (side != null && !token.StartsWith(side + " ", StringComparison.Ordinal))
                        continue;

                    List<string> set;

                    if (!sets.TryGetValue(token, out set))
                    {
                        sets[token] = set = new List<string>();
                        order.Add(token);
                    }

                    if (!set.Contains(key))
                        set.Add(key);
                }
            }

            // Rooms merged on both sides are one set, not two
            var seen = new HashSet<string>(StringComparer.Ordinal);

            return order.Select(t => sets[t])
                        .Where(s => s.Count > 1 && seen.Add(string.Join("\n", s)))
                        .ToList();
        }

        // Every room's group on one side: the sheet's merges of that side
        // and the arrangement's
        public static Dictionary<string, string> GroupsOf(
            IList<string> keys, IDictionary<string, Sheet> sheet, IEnumerable<IEnumerable<string>> arranged, string side)
        {
            return VENTFCUCHECK.Groups(
                keys,
                SheetMerges(keys, sheet, side).Cast<IEnumerable<string>>()
                    .Concat(arranged ?? Enumerable.Empty<IEnumerable<string>>()));
        }

        public const string FreshSide = "fresh";
        public const string ExtractSide = "extract";

        // THE SHEET'S MERGES MADE THE ARRANGEMENT'S (the user's choice), so
        // the next export keeps them: each set of rooms sharing a merged
        // cell that the arrangement does not already hold together is
        // merged there - if it may be (VENTARRANGE.Merge: one container's
        // units, or rooms outside containers). Returns how many were
        // adopted; `refused` how many could not be.
        public static int Adopt(
            VENTARRANGE.Arrangement arrangement, IList<VENTARRANGE.Row> rows,
            IDictionary<string, Sheet> sheet, out int refused)
        {
            refused = 0;

            if (arrangement == null || rows == null)
                return 0;

            int adopted = 0;

            // Each side into its own merges: the sheet's fresh air cells
            // into Merges, its extract cells into ExtractMerges
            foreach (bool extract in new[] { false, true })
            {
                foreach (List<string> set in SheetMerges(rows.Select(r => r.Key).ToList(), sheet, extract ? ExtractSide : FreshSide))
                {
                    List<string> held = extract ? arrangement.ExtractMergeOf(set[0]) : arrangement.MergeOf(set[0]);

                    if (held != null && set.All(held.Contains))
                        continue;

                    List<VENTARRANGE.Row> members = rows.Where(r => set.Contains(r.Key)).ToList();

                    if (VENTARRANGE.Merge(arrangement, members, extract) == null)
                        adopted++;
                    else
                        refused++;
                }
            }

            return adopted;
        }

        // ==================================================
        // Merges the FCUs suggest
        // ==================================================
        //
        // THE ROOMS ONE FCU SUPPLIES, WITH ITS FRESH AIR CONNECTION (user,
        // 2026-10-19: "in this apartment the FCU is serving the foyer, the
        // kitchen and the living/dining, but it doesnt detect the foyer as
        // merged" - then "yes add the FCU merge detection"). A merge is the
        // user's own definition (2026-10-12): spaces on one FCU fed by one
        // fresh air connection. What the airflow run reads (REVITROOMS,
        // Mode.Survey): each unit's supplied rooms - every terminal in the
        // Revit system on its supply connector, standing or facing into a
        // room - and the fresh air endcaps / bellmouths bound to it.
        //
        // That system can hold more than the unit's own ductwork (KIFAF:
        // one FCU's system reached the mirror apartment's KITCHEN 121.126;
        // a unit with no fresh endcap "supplied" rooms in eight apartments),
        // so a suggestion is only:
        //   - a unit WITH a fresh air connection;
        //   - its rooms in the ONE container - or a level's own rooms -
        //     holding most of them: two or more, more than half; the
        //     rest are left out and said;
        //   - no room claimed by two units' suggestions - both are left
        //     for a person to merge by hand;
        //   - not already merged together.

        // A unit as the run read it
        public class Unit
        {
            public long Id;
            public List<string> Rooms = new List<string>();
            public int Fresh;            // fresh air endcaps / bellmouths bound to it
        }

        // A suggestion: the unit, its level, the rooms to merge (as the
        // window shows them) and those of its rooms left out
        public class FcuGroup
        {
            public long Unit;
            public string Level = string.Empty;
            public List<string> Rooms = new List<string>();
            public List<string> Left = new List<string>();
        }

        public static List<FcuGroup> FcuGroups(
            IEnumerable<Unit> units,
            IDictionary<string, List<VENTARRANGE.Row>> levels,
            VENTARRANGE.Arrangement arrangement,
            out int contested)
        {
            contested = 0;

            var found = new List<FcuGroup>();

            if (units == null || levels == null)
                return found;

            // Each room's level, container and place in the window
            var where = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
            var place = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, List<VENTARRANGE.Row>> level in levels)
            {
                foreach (VENTARRANGE.Row row in level.Value ?? new List<VENTARRANGE.Row>())
                {
                    where[row.Key] = new KeyValuePair<string, string>(
                        level.Key, string.IsNullOrEmpty(row.ApartmentKey) ? string.Empty : row.ApartmentKey);
                    place[row.Key] = place.Count;
                }
            }

            foreach (Unit unit in units)
            {
                if (unit == null || unit.Fresh <= 0 || unit.Rooms == null)
                    continue;

                List<string> rooms = unit.Rooms.Where(where.ContainsKey).Distinct().ToList();

                if (rooms.Count < 2)
                    continue;

                var buckets = rooms
                    .GroupBy(r => where[r].Key + "\n" + where[r].Value)
                    .OrderByDescending(b => b.Count())
                    .ToList();

                List<string> most = buckets[0].ToList();

                // A clear home: more than half of its two or more rooms - so
                // two at least, and never a tie with another container
                if (most.Count * 2 <= rooms.Count)
                    continue;

                found.Add(new FcuGroup
                {
                    Unit = unit.Id,
                    Level = where[most[0]].Key,
                    Rooms = most.OrderBy(r => place[r]).ToList(),
                    Left = rooms.Except(most).OrderBy(r => place[r]).ToList()
                });
            }

            // A room two units claim: neither suggestion stands
            var claimed = found.SelectMany(g => g.Rooms).GroupBy(r => r).Where(g => g.Count() > 1)
                               .Select(g => g.Key).ToList();

            if (claimed.Count > 0)
            {
                int before = found.Count;
                found = found.Where(g => !g.Rooms.Any(claimed.Contains)).ToList();
                contested = before - found.Count;
            }

            // Already one group in the arrangement: nothing to suggest
            if (arrangement != null)
            {
                found = found.Where(g =>
                {
                    List<string> held = arrangement.MergeOf(g.Rooms[0]);
                    return held == null || !g.Rooms.All(held.Contains);
                }).ToList();
            }

            return found;
        }

        // THE MODEL'S MERGES (the window's Model switch, user 2026-10-19):
        // every FCU group merged, and the merges given that no FCU group
        // touches kept as they are - where the model says nothing, what was
        // chosen stands. A room an FCU group takes leaves any other merge.
        public static List<List<string>> ByFcu(
            IEnumerable<List<string>> merges, IEnumerable<Unit> units,
            IDictionary<string, List<VENTARRANGE.Row>> levels, out int contested)
        {
            List<FcuGroup> groups = FcuGroups(units, levels, null, out contested);

            var taken = new HashSet<string>(groups.SelectMany(g => g.Rooms), StringComparer.Ordinal);

            var result = (merges ?? Enumerable.Empty<List<string>>())
                .Where(m => m != null && !m.Any(taken.Contains))
                .Select(m => m.ToList())
                .ToList();

            result.AddRange(groups.Select(g => g.Rooms.ToList()));

            return result;
        }

        // ROOM NUMBERS USED MORE THAN ONCE among these rooms - a sheet row
        // is found by name, number and level, so two rooms of one number
        // on a level can take each other's air (shown in red). Only a
        // number shared with a DIFFERENT room: the same room of one model
        // placed again (link id:room id - the same room id) is not (user,
        // 2026-10-19: "do not highlight the rooms of repeated link
        // instances").
        public static HashSet<string> Repeated(IEnumerable<VENTARRANGE.Row> rows)
        {
            var repeated = new HashSet<string>(StringComparer.Ordinal);

            foreach (IGrouping<string, VENTARRANGE.Row> number in (rows ?? Enumerable.Empty<VENTARRANGE.Row>())
                         .Where(r => !string.IsNullOrWhiteSpace(r.Number))
                         .GroupBy(r => r.Number.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                List<VENTARRANGE.Row> rooms = number.ToList();

                foreach (VENTARRANGE.Row room in rooms)
                {
                    if (rooms.Any(other => RoomIdOf(other.Key) != RoomIdOf(room.Key)))
                        repeated.Add(room.Key);
                }
            }

            return repeated;
        }

        // The room's own id in its model - the part after the link's
        private static string RoomIdOf(string key)
        {
            string k = key ?? string.Empty;
            int at = k.LastIndexOf(':');

            return at < 0 ? k : k.Substring(at + 1);
        }

        // ==================================================
        // What the terminals carry
        // ==================================================

        // AN ENDCAP'S AIR BACK TO THE GROUPS IT CARRIES: each takes the part
        // a run would have given it (VENTFCUCHECK.Share of its sheet value
        // between the endcaps serving it), scaled to what the endcap
        // carries now - so a run's own numbers read back as themselves.
        // With nothing in the sheet to weigh by, equal parts.
        public static Dictionary<string, double> Attribute(
            IEnumerable<KeyValuePair<List<string>, double>> endcaps,
            IDictionary<string, double> groupValues,
            IDictionary<string, VENTFCUCHECK.Room> tally,
            bool fresh)
        {
            var into = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (KeyValuePair<List<string>, double> endcap in endcaps ?? Enumerable.Empty<KeyValuePair<List<string>, double>>())
            {
                if (endcap.Key == null || double.IsNaN(endcap.Value))
                    continue;

                List<string> groups = endcap.Key.Where(g => g != null).Distinct().ToList();

                if (groups.Count == 0)
                    continue;

                var weights = new List<double>();

                foreach (string group in groups)
                {
                    double value = 0;

                    if (groupValues != null)
                        groupValues.TryGetValue(group, out value);

                    VENTFCUCHECK.Room room;

                    int endcapsOn = tally != null && tally.TryGetValue(group, out room)
                        ? (fresh ? room.FreshEndcaps : room.ExtractEndcaps)
                        : 1;

                    weights.Add(VENTFCUCHECK.Share(value, endcapsOn));
                }

                double sum = weights.Sum();

                for (int i = 0; i < groups.Count; i++)
                {
                    double part = sum > 0 ? endcap.Value * weights[i] / sum : endcap.Value / groups.Count;

                    double had;
                    into.TryGetValue(groups[i], out had);
                    into[groups[i]] = had + part;
                }
            }

            return into;
        }

        // A group's value over its rooms, by their own values - the sheet's
        // - or in equal parts when they have none
        public static Dictionary<string, double> Split(double value, IList<string> rooms, IDictionary<string, double> weights)
        {
            var parts = new Dictionary<string, double>(StringComparer.Ordinal);

            if (rooms == null || rooms.Count == 0)
                return parts;

            double sum = 0;

            foreach (string room in rooms)
            {
                double w;

                if (weights != null && weights.TryGetValue(room, out w) && w > 0)
                    sum += w;
            }

            foreach (string room in rooms)
            {
                double w = 0;

                if (weights != null)
                    weights.TryGetValue(room, out w);

                parts[room] = sum > 0 ? value * Math.Max(0, w) / sum : value / rooms.Count;
            }

            return parts;
        }

        // A value added up over rooms - NaN when none of them has one
        public static double Total(IEnumerable<string> rooms, Func<string, double> valueOf)
        {
            double total = 0;
            bool any = false;

            foreach (string room in rooms ?? Enumerable.Empty<string>())
            {
                double v = valueOf(room);

                if (double.IsNaN(v))
                    continue;

                total += v;
                any = true;
            }

            return any ? total : double.NaN;
        }

        // ==================================================
        // What is written
        // ==================================================

        // THE WINDOW'S VALUES AS THE RUN TAKES THEM: a group's value on its
        // first room and 0 on the rest - the run adds a group's rooms up
        // (VENTFCUCHECK.Totals), so the value is written once. A side that
        // is NaN is not written.
        public static Dictionary<string, Air> Overrides(IEnumerable<KeyValuePair<List<string>, Air>> groups)
        {
            var rooms = new Dictionary<string, Air>(StringComparer.Ordinal);

            foreach (KeyValuePair<List<string>, Air> group in groups ?? Enumerable.Empty<KeyValuePair<List<string>, Air>>())
            {
                if (group.Key == null || group.Key.Count == 0 || group.Value == null)
                    continue;

                for (int i = 0; i < group.Key.Count; i++)
                {
                    rooms[group.Key[i]] = new Air
                    {
                        Fresh = double.IsNaN(group.Value.Fresh) ? double.NaN : (i == 0 ? group.Value.Fresh : 0),
                        Extract = double.IsNaN(group.Value.Extract) ? double.NaN : (i == 0 ? group.Value.Extract : 0)
                    };
                }
            }

            return rooms;
        }

        // ==================================================
        // Numbers as the window shows and reads them
        // ==================================================

        public static string Say(double value)
        {
            return double.IsNaN(value)
                ? "-"
                : Math.Round(value, 1).ToString("0.#", CultureInfo.CurrentCulture);
        }

        // As typed: NaN for blank; null for something that is not a number
        public static double? Read(string text)
        {
            string t = (text ?? string.Empty).Trim();

            if (t.Length == 0 || t == "-")
                return double.NaN;

            double value;

            if ((double.TryParse(t, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                 double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && value >= 0)
                return value;

            return null;
        }

        // Two values the same, as the window rounds them - none and 0 are
        // the same air: a room the sheet gives 0, with no terminal
        public static bool Same(double a, double b)
        {
            return Math.Abs((double.IsNaN(a) ? 0 : a) - (double.IsNaN(b) ? 0 : b)) < 0.05;
        }
    }
}
