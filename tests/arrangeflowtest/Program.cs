using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

// THE AIRFLOWS IN LEVELS AND ROOMS (user, 2026-10-19): the sheet's air and
// what the terminals carry, side by side; the sheet's merges shown and
// adopted; values set in the window written onto the rooms picked. And the
// window itself (the levels list and Arrange rooms... in one, 2026-10-19):
// the ticks, the picks, the search, a level closed, Show in plan, every
// change the main window's at once.
//
// VENTFLOWS's arithmetic first - above all that a run's own endcap values
// read back as the sheet's - then the real window, shown off-screen.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static readonly double None = double.NaN;

    private static VENTFLOWS.Air A(double fresh, double extract) => new VENTFLOWS.Air { Fresh = fresh, Extract = extract };

    private static VENTFLOWS.Sheet S(double fresh, double extract, params string[] merges) =>
        new VENTFLOWS.Sheet { Air = A(fresh, extract), Merges = merges.ToList() };

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

    // ---- one level of an apartments job --------------------------------

    private const string Level = "LEVEL 03";

    private static VENTARRANGE.Row Room(string key, string number, string name, string apt = null)
    {
        return new VENTARRANGE.Row
        {
            Key = key, Number = number, Name = name, ModelName = name,
            ApartmentKey = apt, Apartment = apt, ModelApartment = apt, ApartmentNumber = apt,
            HomeKey = apt, Home = apt, HomeNumber = apt
        };
    }

    // A room's area and height, as the export reads them
    private static VENTARRANGE.Row Measured(VENTARRANGE.Row row, double area, double height, bool toCeiling)
    {
        row.AreaM2 = area;
        row.HeightM = height;
        row.HeightToCeiling = toCeiling;

        return row;
    }

    private static List<VENTARRANGE.Row> Rows()
    {
        return new List<VENTARRANGE.Row>
        {
            Room("1:1", "101", "CORRIDOR"),
            Measured(Room("1:2", "102", "LOBBY"), 20.359, 2.7, true),
            Measured(Room("2:1", "201", "LIVING", "A1"), 35.04, 3.1, false),
            Room("2:2", "202", "KITCHEN", "A1"),
            Room("2:3", "203", "BEDROOM", "A1"),
            Room("3:1", "301", "LIVING", "A2"),
            Room("3:2", "201", "STUDY", "A2")          // the living's number again
        };
    }

    // A second level with two rooms of its own and no air
    private const string Other = "LEVEL 04";

    private static Dictionary<string, List<VENTARRANGE.Row>> RowsOf(VENTARRANGE.Arrangement a)
    {
        return new Dictionary<string, List<VENTARRANGE.Row>>(StringComparer.OrdinalIgnoreCase)
        {
            [Level] = VENTARRANGE.Order(Rows(), Level, a, r => r.Key, r => r.ApartmentKey),
            [Other] = VENTARRANGE.Order(new List<VENTARRANGE.Row> { Room("4:1", "401", "OFFICE"), Room("4:2", "402", "STORE") },
                                        Other, a, r => r.Key, r => r.ApartmentKey)
        };
    }

    // The sheet: A1's living and kitchen merged (one FCU); the corridor and
    // A2's living merged across containers - which cannot be adopted
    private static Dictionary<string, VENTFLOWS.Sheet> Sheet()
    {
        return new Dictionary<string, VENTFLOWS.Sheet>
        {
            ["1:1"] = S(10, None, "fresh AA9:AA14"),
            ["1:2"] = S(30, None),
            ["2:1"] = S(25, 0, "fresh AA11:AA12", "extract AM11:AM12"),
            ["2:2"] = S(0, 50, "fresh AA11:AA12", "extract AM11:AM12"),
            ["2:3"] = S(15, None),
            ["3:1"] = S(20, None, "fresh AA9:AA14")
        };
    }

    private static Dictionary<string, VENTFLOWS.Air> Carrying()
    {
        return new Dictionary<string, VENTFLOWS.Air>
        {
            ["1:1"] = A(10, None),
            ["1:2"] = A(22, None),
            ["2:1"] = A(25, None),
            ["2:2"] = A(None, 50),
            ["2:3"] = A(0, None)
        };
    }

    // ---- driving the window ----------------------------------------------

    private static VENTFLOWS.Unit U(long id, int fresh, params string[] rooms) =>
        new VENTFLOWS.Unit { Id = id, Fresh = fresh, Rooms = rooms.ToList() };

    // What the main window holds and is told, for the window made last
    private static List<VENTWindow.LevelRow> _levels;
    private static VENTARRANGE.Arrangement _arrangement;
    private static int _arranged;
    private static int _ticked;
    private static List<VENTWindow.RoomRow> _shownRooms;
    private static int _shownElsewhere;
    private static bool _written;

    // The main window's levels: each level's rooms as RoomsOn has them, all
    // ticked, open
    private static List<VENTWindow.LevelRow> Levels()
    {
        var choices = new Dictionary<string, bool>();
        var levels = new List<VENTWindow.LevelRow>();

        foreach (KeyValuePair<string, List<VENTARRANGE.Row>> level in RowsOf(new VENTARRANGE.Arrangement()))
        {
            IEnumerable<VENTWindow.RoomRow> rooms = level.Value.Select(r => new VENTWindow.RoomRow
            {
                LinkId = long.Parse(r.Key.Split(':')[0]),
                RoomId = long.Parse(r.Key.Split(':')[1]),
                Level = level.Key,
                Number = r.Number,
                Name = r.Name
            }).ToList();

            VENTWindow.LevelRow row = VENTWindow.LevelRow.Build(level.Key, string.Empty, true, true, rooms, choices);
            row.IsExpanded = true;
            levels.Add(row);
        }

        return levels;
    }

    private static VENTROOMSWINDOW Make(Dictionary<string, VENTFLOWS.Air> carrying, List<VENTFLOWS.Unit> units = null, bool sheet = true,
                                        VENTARRANGE.Arrangement arrangement = null, Dictionary<string, VENTFLOWS.Sheet> rows = null,
                                        VENTFLOWS.Formulas formulas = null)
    {
        _levels = Levels();
        _arrangement = arrangement ?? new VENTARRANGE.Arrangement();
        _arranged = 0;
        _ticked = 0;
        _shownRooms = null;
        _shownElsewhere = -1;
        _written = false;

        return new VENTROOMSWINDOW(() => _levels, RowsOf, _arrangement, sheet ? rows ?? Sheet() : null, carrying, units, formulas)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = -4000,
            ShowActivated = false,
            Arranged = () => _arranged++,
            Ticked = () => _ticked++,
            ShowInPlan = (rooms, elsewhere) => { _shownRooms = rooms; _shownElsewhere = elsewhere; },
            Write = (writes, fresh, extract, model) => _written = true
        };
    }

    private static bool? Run(VENTROOMSWINDOW dialog, Action<VENTROOMSWINDOW> body)
    {
        dialog.Loaded += (s, e) => dialog.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                body(dialog);
            }
            catch (Exception ex)
            {
                Check("no exception: " + (ex.InnerException ?? ex), false);
            }

            if (dialog.IsVisible)
                dialog.Close();
        }), DispatcherPriority.ContextIdle);

        return dialog.ShowDialog();
    }

    private static object Call(VENTROOMSWINDOW dialog, string method, params object[] args)
    {
        return typeof(VENTROOMSWINDOW).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .First(m => m.Name == method && m.GetParameters().Length == args.Length)
            .Invoke(dialog, args);
    }

    private static T Field<T>(VENTROOMSWINDOW dialog, string name)
    {
        return (T)typeof(VENTROOMSWINDOW).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog);
    }

    private static ListBox ListOf(DependencyObject from)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(from); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(from, i);

            if (child is ListBox list)
                return list;

            ListBox deeper = ListOf(child);

            if (deeper != null)
                return deeper;
        }

        return null;
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject from) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(from); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(from, i);

            if (child is T found)
                yield return found;

            foreach (T deeper in FindAll<T>(child))
                yield return deeper;
        }
    }

    private static string KeyOf(ListBoxItem item)
    {
        object line = item.Tag;
        return (string)line.GetType().GetField("Key").GetValue(line);
    }

    private static bool Is(ListBoxItem item, string field)
    {
        return (bool)item.Tag.GetType().GetField(field).GetValue(item.Tag);
    }

    // A room's line
    private static ListBoxItem Item(ListBox list, string key)
    {
        return list.Items.Cast<ListBoxItem>().FirstOrDefault(i =>
            KeyOf(i) == key && i.Tag.GetType().GetField("Row").GetValue(i.Tag) != null);
    }

    private static ListBoxItem LevelItem(ListBox list, string name)
    {
        return list.Items.Cast<ListBoxItem>().FirstOrDefault(i => Is(i, "IsLevel") && KeyOf(i) == name);
    }

    private static ListBoxItem ContainerItem(ListBox list, string key)
    {
        return list.Items.Cast<ListBoxItem>().FirstOrDefault(i => Is(i, "IsApartment") && KeyOf(i) == key);
    }

    // Every line showing: "level X", "apt Y", or a room's key
    private static string Showing(ListBox list)
    {
        return string.Join(",", list.Items.Cast<ListBoxItem>().Select(i =>
            (Is(i, "IsLevel") ? "level " : Is(i, "IsApartment") ? "apt " : string.Empty) + KeyOf(i)));
    }

    private static CheckBox BoxOf(ListBoxItem item)
    {
        return Within<CheckBox>(item.Content).First();
    }

    // Everything of a kind inside a row's content - the rows are cells
    // now, a sheet's (2026-10-25), so nothing sits directly in the row
    private static IEnumerable<T> Within<T>(object root) where T : DependencyObject
    {
        var at = root as DependencyObject;

        if (at == null)
            yield break;

        foreach (object child in LogicalTreeHelper.GetChildren(at))
        {
            if (child is T found)
                yield return found;

            foreach (T deeper in Within<T>(child))
                yield return deeper;
        }
    }

    // A row's text, its notes with it, run by run
    private static IEnumerable<System.Windows.Documents.Run> RunsOf(ListBoxItem item)
    {
        return Within<TextBlock>(item.Content).SelectMany(t => t.Inlines.OfType<System.Windows.Documents.Run>());
    }

    // The text cells of a tagged block of cells (Tag "flow" or "room") - a
    // list's text beside its arrow
    private static TextBlock[] CellsIn(StackPanel cells)
    {
        return cells == null
            ? new TextBlock[0]
            : cells.Children.OfType<Border>().Select(b => b.Child as TextBlock ?? (b.Child is Grid ? Within<TextBlock>(b.Child).FirstOrDefault() : null))
                .Where(t => t != null).ToArray();
    }

    // A click as a person makes it: the box toggles, then Click is raised
    private static void Press(System.Windows.Controls.Primitives.ButtonBase button)
    {
        typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(button, null);
    }

    private static VENTWindow.RoomRow Tick(string key)
    {
        return _levels.SelectMany(l => l.Rooms).First(r => r.Key == key);
    }

    private static VENTFLOWS.Sheet Row(double fresh, double extract, string freshCategory = "", string exhaustCategory = "",
                                       string freshSuggested = null, string exhaustSuggested = null,
                                       double occupants = double.NaN, double units = double.NaN, double area = double.NaN)
    {
        return new VENTFLOWS.Sheet
        {
            Air = A(fresh, extract), FreshCategory = freshCategory, ExhaustCategory = exhaustCategory,
            FreshSuggested = freshSuggested, ExhaustSuggested = exhaustSuggested,
            Occupants = occupants, Units = units, AreaM2 = area
        };
    }

    private static void SheetColumns()
    {
        Console.WriteLine();
        Console.WriteLine("the sheet's columns, Differences only, Columns...");

        // ---- the columns chosen, remembered
        Check("by default: notes, the room's m² and m, the occupancy, occupants and units, the calculation",
            VENTCOLUMNS.Defaults().SetEquals(new[] { VENTCOLUMNS.Notes, VENTCOLUMNS.Room, VENTCOLUMNS.Categories, VENTCOLUMNS.People, VENTCOLUMNS.Calculation }));

        HashSet<string> parsed = VENTCOLUMNS.Parse("people\toff\nnotes\toff\nnonsense\ton\n");

        Check("a saved choice: people off, notes off; a column it does not name keeps its default, a name it does not know is ignored",
            parsed.SetEquals(new[] { VENTCOLUMNS.Room, VENTCOLUMNS.Categories, VENTCOLUMNS.Calculation }));

        Check("round trip", VENTCOLUMNS.Parse(VENTCOLUMNS.Compose(parsed)).SetEquals(parsed));

        // ---- the search with a filter
        List<VENTWindow.LevelRow> levels = Levels();
        var open = new Dictionary<string, bool>();

        int kept = VENTWindow.Search.Apply(levels, string.Empty, open, null, (l, r) => r.Number == "102" || r.Number == "401");

        Check("a filter alone shows what it keeps and opens their levels: " + kept,
            kept == 2 && levels.SelectMany(l => l.Rooms).Where(r => r.IsShown).Select(r => r.Number).OrderBy(n => n).SequenceEqual(new[] { "102", "401" }));

        kept = VENTWindow.Search.Apply(levels, "lobby", open, null, (l, r) => r.Number == "102" || r.Number == "401");

        Check("with a query too: both", kept == 1 && levels.SelectMany(l => l.Rooms).Single(r => r.IsShown).Number == "102");

        VENTWindow.Search.Apply(levels, string.Empty, open, null, (l, r) => false);

        Check("a filter keeping nothing hides every level", levels.All(l => !l.IsShown));

        VENTWindow.Search.Apply(levels, string.Empty, open);

        Check("cleared: everything back", levels.All(l => l.IsShown) && levels.SelectMany(l => l.Rooms).All(r => r.IsShown));

        // ---- the real window, over a sheet of its own
        var rows = new Dictionary<string, VENTFLOWS.Sheet>
        {
            ["1:1"] = Row(10, None, "Corridors"),
            ["1:2"] = Row(30, None, freshSuggested: "Main entry lobbies", occupants: 3, area: 20.4),
            ["2:1"] = Row(25, 0, "Dwelling Unit", area: 30),                       // the model has 35.04 m²
            ["2:2"] = Row(0, 50, exhaustCategory: "Residential kitchens- Apartments", units: 1),
            ["2:3"] = Row(15, None),
            ["3:1"] = Row(20, None)
        };

        var carrying = new Dictionary<string, VENTFLOWS.Air>
        {
            ["1:1"] = A(10, None),
            ["1:2"] = A(22, None),          // the sheet has 30
            ["2:1"] = A(25, None),
            ["2:2"] = A(None, 50),
            ["2:3"] = A(15, None),
            ["3:1"] = A(20, None)
        };

        Run(Make(carrying, rows: rows), d =>
        {
            d.Terminals(carrying, null, new Dictionary<string, List<long>> { ["2:2"] = new List<long> { 77 } });

            ListBox list = ListOf(d);

            TextBlock[] SheetCellsOf(string key) => BlockOf(ListOf(d), key, "occupancy");

            TextBlock[] lobby = SheetCellsOf("1:2");
            TextBlock[] kitchen = SheetCellsOf("2:2");

            Check("the occupancy as the sheet has it: a blank category shows, grey, what an export fills in - '" + lobby[0].Text + "'",
                lobby.Length == 5 && lobby[0].Text == "Main entry lobbies" && lobby[0].FontStyle == FontStyles.Italic &&
                ((string)lobby[0].ToolTip).StartsWith("Blank in the sheet") && lobby[1].Text == string.Empty);

            Check("...a filled one as the sheet has it: " + kitchen[1].Text,
                kitchen[1].Text == "Residential kitchens- Apartments" && kitchen[1].FontStyle != FontStyles.Italic);

            Check("the model's area flagged where the sheet's differs: " + RoomCells(list, "2:1")[0].ToolTip,
                "The sheet has 30.0 m² - changed since".Replace("30.0", 30.0.ToString("0.0")).Equals(RoomCells(list, "2:1")[0].ToolTip) &&
                RoomCells(list, "1:2")[0].ToolTip == null);

            // ---- Columns...
            Call(d, "ShowColumn", VENTCOLUMNS.People, true);

            TextBlock[] people = SheetCellsOf("1:2");

            Check("Columns...: occupants and units shown - " + string.Join(" | ", people.Select(t => t.Text)),
                people.Length == 5 && people[3].Text == "3" && people[4].Text == "-" &&
                HeadsShowing(d).Contains("Occupants") && HeadsShowing(d).Contains("Units"));

            Check("...and remembered for the person", VENTCOLUMNS.Load().Contains(VENTCOLUMNS.People));

            Call(d, "ShowColumn", VENTCOLUMNS.Notes, false);

            Grid middle = ((DockPanel)Item(ListOf(d), "2:1").Content).Children.OfType<Grid>().First();

            Check("Notes hidden: the name takes the room, no Notes heading",
                middle.Children.Count == 1 && !HeadsShowing(d).Contains("Notes"));

            Call(d, "ShowColumn", VENTCOLUMNS.Notes, true);
            Call(d, "ShowColumn", VENTCOLUMNS.People, false);

            // ---- Sheet | Model said for what they are
            Check("Sheet | Model under \"Merge and write as\"", FindAll<TextBlock>(d).Any(t => t.Text == "Merge and write as"));

            // ---- Differences only
            var only = (CheckBox)typeof(VENTROOMSWINDOW).GetField("_onlyDifferences", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(d);

            Press(only);

            List<string> shown = ListOf(d).Items.Cast<ListBoxItem>().Where(i => !Is(i, "IsLevel") && !Is(i, "IsApartment")).Select(KeyOf).ToList();

            Check("Differences only: what needs attention and nothing else - " + string.Join(", ", shown),
                shown.OrderBy(k => k).SequenceEqual(new[] { "1:2", "2:1", "2:2", "3:2", "4:1", "4:2" }));

            string said = ((TextBlock)typeof(VENTROOMSWINDOW).GetField("_said", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(d)).Text;

            Check("...counted: " + said.Split('.')[0], said.StartsWith("6 room(s) on 2 level(s) need attention."));

            string tip = Item(ListOf(d), "2:1").ToolTip as string;

            Check("...each row says why: " + tip,
                tip != null && tip.Contains("Needs attention: its number is used twice on the level; its area changed since the sheet"));

            Check("...a terminal on no FCU, no row in the sheet, sheet and model apart, each its reason",
                ((string)Item(ListOf(d), "2:2").ToolTip).Contains("a terminal on no FCU") &&
                ((string)Item(ListOf(d), "4:1").ToolTip).Contains("no row in the sheet") &&
                ((string)Item(ListOf(d), "1:2").ToolTip).Contains("sheet and model airflows differ"));

            var search = (TextBox)typeof(VENTROOMSWINDOW).GetField("_search", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(d);

            search.Text = "living";

            shown = ListOf(d).Items.Cast<ListBoxItem>().Where(i => !Is(i, "IsLevel") && !Is(i, "IsApartment")).Select(KeyOf).ToList();

            Check("...with a search too: both - " + string.Join(", ", shown), shown.SequenceEqual(new[] { "2:1" }));

            search.Text = string.Empty;
            Press(only);

            Check("off again: every row back", ListOf(d).Items.Cast<ListBoxItem>().Count(i => !Is(i, "IsLevel") && !Is(i, "IsApartment")) == 9);
        });
    }

    // Lets the window's queued work run (a choice is kept after the focus
    // has gone)
    private static void Pump()
    {
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
    }

    // THE SHEET'S FORMULAS, as the main window hands them over (VENTWHATIF
    // there): a stand-in that works a room's fresh air out of its
    // occupants - 2.5 a person + 7 - and gives #N/A for "Nonsense"; one
    // calculation column, "T"
    private static int _calculated;

    private static VENTFLOWS.Formulas Formulas(Dictionary<string, VENTFLOWS.Sheet> rows)
    {
        return new VENTFLOWS.Formulas
        {
            Columns = new List<VENTFLOWS.CalcColumn>
            {
                new VENTFLOWS.CalcColumn { Col = 20, Letter = "T", Label = "Total Outside Air", Units = "l/s", Heading = "Total Outside Air / l/s" }
            },
            FreshChoices = new List<string> { "Office Space", "Corridors", "Main entry lobbies", "Dwelling Unit" },
            ExhaustChoices = new List<string> { "Toilets - public", "Toilets - private (residential)" },
            UseChoices = new List<string> { "Heavy Use / Cont. Oper.", "Light Use / Cont. Oper." },
            Calculate = chosen =>
            {
                _calculated++;

                var calc = new Dictionary<string, VENTFLOWS.Calc>();

                foreach (KeyValuePair<string, VENTFLOWS.Sheet> row in rows)
                {
                    VENTARRANGE.RoomOccupancy mine;
                    chosen.TryGetValue(row.Key, out mine);

                    var one = new VENTFLOWS.Calc { Air = row.Value.Air.Copy() };
                    one.Cells[20] = "17";

                    if (mine != null)
                    {
                        one.SheetCells = new Dictionary<int, string> { [20] = "17" };

                        if (mine.Occupants.HasValue && !double.IsNaN(mine.Occupants.Value))
                        {
                            one.Air.Fresh = 2.5 * mine.Occupants.Value + 7;
                            one.Cells[20] = one.Air.Fresh.ToString();
                        }

                        if (mine.FreshCategory == "Nonsense")
                        {
                            one.Air.Fresh = 0;
                            one.FreshError = "#N/A";
                        }
                    }

                    calc[row.Key] = one;
                }

                return calc;
            }
        };
    }

    private static void Occupancy()
    {
        Console.WriteLine();
        Console.WriteLine("the occupancy chosen, its new values, Model writes them");

        // ---- one field chosen (VENTFLOWS.Choose)
        var sheetRow = Row(30, None, occupants: 3);
        var a = new VENTARRANGE.Arrangement();

        Check("a number chosen is kept", VENTFLOWS.Choose(a, "1:2", VENTFLOWS.OccupantsField, "10", sheetRow) &&
            a.Occupancy["1:2"].Occupants == 10 && a.Occupancy["1:2"].FreshCategory == null);

        Check("...the sheet's own value chosen follows the sheet - the room's choice gone",
            VENTFLOWS.Choose(a, "1:2", VENTFLOWS.OccupantsField, "3", sheetRow) && !a.Occupancy.ContainsKey("1:2"));

        Check("...empty is blank, not the sheet's",
            VENTFLOWS.Choose(a, "1:2", VENTFLOWS.OccupantsField, "", sheetRow) && double.IsNaN(a.Occupancy["1:2"].Occupants.Value));

        Check("...not a number: refused, nothing changed", !VENTFLOWS.Choose(a, "1:2", VENTFLOWS.UnitsField, "two", sheetRow) &&
            !a.Occupancy["1:2"].Units.HasValue);

        Check("a category the same as the sheet's but for case and spaces follows the sheet",
            VENTFLOWS.Choose(a, "1:3", VENTFLOWS.FreshCategoryField, "office  space", Row(10, None, "Office Space")) &&
            !a.Occupancy.ContainsKey("1:3"));

        Check("null is as the sheet", VENTFLOWS.Choose(a, "1:2", VENTFLOWS.OccupantsField, null, sheetRow) && !a.Occupancy.ContainsKey("1:2"));

        // ---- a per-unit exhaust category on a room with no units: 1 unit
        // (user, 2026-11-05: toilets read 0 L/s)
        var perUnit = new HashSet<string> { "toilets - private", "toilets - public" };
        var t = new VENTARRANGE.Arrangement();

        Check("Toilets - public chosen, no units in the sheet: 1 unit",
            VENTFLOWS.Choose(t, "5:1", VENTFLOWS.ExhaustCategoryField, "Toilets - public", Row(0, 0), perUnit) &&
            t.Occupancy["5:1"].ExhaustCategory == "Toilets - public" && t.Occupancy["5:1"].Units == 1);

        Check("...the sheet's 3 units kept",
            VENTFLOWS.Choose(t, "5:2", VENTFLOWS.ExhaustCategoryField, "Toilets - private", Row(0, 0, units: 3), perUnit) &&
            !t.Occupancy["5:2"].Units.HasValue);

        Check("...units chosen first kept",
            VENTFLOWS.Choose(t, "5:3", VENTFLOWS.UnitsField, "4", Row(0, 0), perUnit) &&
            VENTFLOWS.Choose(t, "5:3", VENTFLOWS.ExhaustCategoryField, "toilets – PRIVATE", Row(0, 0), perUnit) &&
            t.Occupancy["5:3"].Units == 4);

        Check("...a category rated by area: no units given",
            VENTFLOWS.Choose(t, "5:4", VENTFLOWS.ExhaustCategoryField, "Kitchenettes", Row(0, 0), perUnit) &&
            !t.Occupancy["5:4"].Units.HasValue);

        Check("...per-unit categories not known (no workbook): no units given",
            VENTFLOWS.Choose(t, "5:5", VENTFLOWS.ExhaustCategoryField, "Toilets - public", Row(0, 0)) &&
            !t.Occupancy["5:5"].Units.HasValue);

        // ---- from the rules, blank ones only
        var rules = new Dictionary<string, VENTFLOWS.Sheet>
        {
            ["1:1"] = Row(10, None, "Corridors", freshSuggested: "Main entry lobbies"),
            ["1:2"] = Row(30, None, freshSuggested: "main entry LOBBIES"),
            ["2:1"] = Row(0, 0, freshSuggested: "Plant Room"),
            ["2:2"] = Row(0, 0, exhaustCategory: "Toilets - public")
        };

        rules["2:2"].RuleExhaust = "Toilets - public";
        rules["2:2"].RuleUse = "light use / cont. oper.";
        rules["2:2"].RuleUnits = 1;

        var fromRules = new VENTARRANGE.Arrangement();
        int took = VENTFLOWS.FromRules(fromRules, rules.Keys, rules, Formulas(rules));

        Check("from the rules: a blank category given the suggestion, in the list's spelling - " + took + " room(s)",
            took == 2 && fromRules.Occupancy["1:2"].FreshCategory == "Main entry lobbies");

        Check("...a category already there kept; one the list has not got left blank",
            !fromRules.Occupancy.ContainsKey("1:1") && !fromRules.Occupancy.ContainsKey("2:1"));

        Check("...the rule's nature of use and units where its exhaust category is the rule's - " +
              (fromRules.Occupancy.ContainsKey("2:2") ? fromRules.Occupancy["2:2"].Use + ", " + fromRules.Occupancy["2:2"].Units : "none"),
            fromRules.Occupancy["2:2"].Use == "Light Use / Cont. Oper." && fromRules.Occupancy["2:2"].Units == 1 &&
            fromRules.Occupancy["2:2"].ExhaustCategory == null);

        Check("...run again: nothing more to fill", VENTFLOWS.FromRules(fromRules, rules.Keys, rules, Formulas(rules)) == 0);

        // ---- the real window, with the stand-in formulas
        Dictionary<string, VENTFLOWS.Sheet> rows = Sheet();
        rows["1:2"] = Row(30, None, freshSuggested: "Main entry lobbies", occupants: double.NaN, area: 20.4);

        _calculated = 0;

        // Every column as it comes - an earlier test hid some
        VENTCOLUMNS.Save(VENTCOLUMNS.Defaults());

        Run(Make(Carrying(), rows: rows, formulas: Formulas(rows)), d =>
        {
            ListBox list = ListOf(d);
            List<string> heads = HeadsShowing(d);

            Check("headed: the occupancy, the new values - Fresh air, Exhaust air - and the calculation's own column",
                heads.Contains("Occupancy") && heads.Contains("New values") && heads.Contains("Fresh air") && heads.Contains("Exhaust air") &&
                heads.Contains("Calculation") && heads.Contains("Total Outside Air") && heads.Contains("T · l/s") && _calculated > 0);

            TextBlock[] lobbyNew = BlockOf(list, "1:2", "new");

            Check("nothing chosen: the new values are the sheet's, plain - " + string.Join(" | ", lobbyNew.Select(t => t.Text)),
                lobbyNew.Length == 2 && lobbyNew[0].Text == "30" && lobbyNew[0].FontWeight != FontWeights.SemiBold);

            Check("the occupancy cells and the Model's open to a double-click",
                "edit:occupants".Equals(CellOf(list, "1:2", "occupancy", 3).Tag) &&
                "edit:fresh-category".Equals(CellOf(list, "1:2", "occupancy", 0).Tag) &&
                "write:fresh".Equals(CellOf(list, "1:2", "model", 0).Tag) && "write:extract".Equals(CellOf(list, "1:2", "model", 1).Tag) &&
                CellOf(list, "1:2", "sheet", 0).Tag == null);

            // ---- 10 people in the lobby
            int arranged = _arranged;

            Call(d, "Chosen", new List<string> { "1:2" }, VENTFLOWS.OccupantsField, "10");

            list = ListOf(d);
            lobbyNew = BlockOf(list, "1:2", "new");
            Border occupants = CellOf(list, "1:2", "occupancy", 3);

            Check("10 people chosen: kept in the arrangement and saved - " + _arranged,
                _arrangement.Occupancy["1:2"].Occupants == 10 && _arranged == arranged + 1);

            Check("...the cell says so, the sheet's on hover - " + ((TextBlock)occupants.Child).Text + ", " + ((TextBlock)occupants.Child).ToolTip,
                ((TextBlock)occupants.Child).Text == "10" && "Sheet: blank".Equals(((TextBlock)occupants.Child).ToolTip) &&
                occupants.Background != null && occupants.Background != Brushes.Transparent);

            Check("...the new fresh air worked out, amber, the sheet's on hover: " + lobbyNew[0].Text,
                lobbyNew[0].Text == "32" && lobbyNew[0].FontWeight == FontWeights.SemiBold && "Sheet: 30".Equals(lobbyNew[0].ToolTip));

            TextBlock[] calc = BlockOf(list, "1:2", "calc");

            Check("...and the calculation's column with it, the sheet's on hover - " + calc[0].Text,
                calc.Length == 1 && calc[0].Text == "32" && "Sheet: 17".Equals(calc[0].ToolTip) && BlockOf(list, "1:1", "calc")[0].Text == "17");

            Check("...needing attention: its new values are not the sheet's",
                ((List<string>)Call(d, "Attention", Item(list, "1:2").Tag.GetType().GetField("Row").GetValue(Item(list, "1:2").Tag)))
                    .Contains("new values differ from the sheet"));

            // ---- written: the Sheet's on Sheet, the new values on Model
            Pick(list, "1:2");
            Call(d, "WriteNow");

            Check("Sheet: the sheet's 30 written", d.Writes != null && Near(d.Writes["1:2"].Fresh, 30) && !d.WroteModel);

            Call(d, "View", true);
            Pick(ListOf(d), "1:2");
            Call(d, "WriteNow");

            Check("Model: the new 32 written", d.Writes != null && Near(d.Writes["1:2"].Fresh, 32) && d.WroteModel);

            Call(d, "View", false);

            // ---- a category the sheet's table has not got
            Call(d, "Chosen", new List<string> { "1:1" }, VENTFLOWS.FreshCategoryField, "Nonsense");

            TextBlock corridor = BlockOf(ListOf(d), "1:1", "new")[0];

            Check("a category the table has not got: the formula's error in the new value - " + corridor.Text,
                corridor.Text == "#N/A" && ((string)corridor.ToolTip).Contains("#N/A"));

            // ---- picked rooms: back to the sheet
            Pick(ListOf(d), "1:1", "1:2");
            Call(d, "AsSheetPicked");

            Check("Occupancy as the sheet: the picked rooms' choices gone, the sheet's values back - the corridor's merge 10 + 20",
                _arrangement.Occupancy.Count == 0 && BlockOf(ListOf(d), "1:2", "new")[0].Text == "30" &&
                BlockOf(ListOf(d), "1:1", "new")[0].Text == "30");

            // ---- from the rules, none picked: every room showing
            ListOf(d).SelectedItems.Clear();
            Call(d, "FromRulesPicked");

            Check("Occupancy from rules, nothing picked: every room showing - the lobby's blank category filled",
                _arrangement.Occupancy.ContainsKey("1:2") && _arrangement.Occupancy["1:2"].FreshCategory == "Main entry lobbies" &&
                _arrangement.Occupancy.Count == 1);

            // ---- a list in a cell, as a person picks from it
            _arrangement.Occupancy.Clear();
            Call(d, "Changed", (object)null);

            ListBoxItem item = Item(ListOf(d), "1:1");
            Border category = CellOf(ListOf(d), "1:1", "occupancy", 0);

            Call(d, "BeginEdit", item.Tag, VENTFLOWS.FreshCategoryField, category);

            var combo = category.Child as ComboBox;

            Check("a double-click on a category: the sheet's list, after As the sheet and (blank) - " +
                  (combo == null ? "none" : combo.Items.Count + " item(s)"),
                combo != null && combo.Items.Count == 6 && combo.SelectedIndex == 0);

            if (combo != null)
            {
                // Open once loaded, as a double-click leaves it
                Pump();

                combo.SelectedIndex = 4;
                combo.IsDropDownOpen = false;
                Pump();
            }

            Check("...one picked: kept - " + (_arrangement.Occupancy.ContainsKey("1:1") ? _arrangement.Occupancy["1:1"].FreshCategory : "none"),
                _arrangement.Occupancy.ContainsKey("1:1") && _arrangement.Occupancy["1:1"].FreshCategory == "Main entry lobbies");

            category = CellOf(ListOf(d), "1:1", "occupancy", 0);
            Call(d, "BeginEdit", Item(ListOf(d), "1:1").Tag, VENTFLOWS.FreshCategoryField, category);
            combo = category.Child as ComboBox;

            if (combo != null)
            {
                // Open once loaded, as a double-click leaves it
                Pump();

                combo.SelectedIndex = 0;
                combo.IsDropDownOpen = false;
                Pump();
            }

            Check("...\"As the sheet\" picked: the room follows the sheet again", !_arrangement.Occupancy.ContainsKey("1:1"));

            // ---- several rooms at once: picked, then one of them edited
            Pick(ListOf(d), "1:1", "1:2");

            Border lobbyPeople = CellOf(ListOf(d), "1:2", "occupancy", 3);
            Call(d, "BeginEdit", Item(ListOf(d), "1:2").Tag, VENTFLOWS.OccupantsField, lobbyPeople);

            var box = lobbyPeople.Child as TextBox;

            if (box != null)
            {
                box.Text = "4";
                box.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(box), 0, System.Windows.Input.Key.Enter) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });
                Pump();
            }

            Check("a number typed in a picked row: every picked room takes it - " + string.Join(", ", _arrangement.Occupancy.Keys.OrderBy(k => k)),
                box != null && _arrangement.Occupancy.Count == 2 && _arrangement.Occupancy.Values.All(o => o.Occupants == 4));

            // ---- a Model airflow written at once
            _written = false;

            Call(d, "WriteGroup", new List<string> { "2:1", "2:2" }, false, 45.0);

            Check("a Model cell's airflow written to its group's terminals at once: 45 on the first room, 0 on the other, fresh air only",
                _written && Near(d.Writes["2:1"].Fresh, 45) && Near(d.Writes["2:2"].Fresh, 0) &&
                d.WriteFresh.SetEquals(new[] { "2:1", "2:2" }) && d.WriteExtract.Count == 0 && double.IsNaN(d.Writes["2:1"].Extract));

            // ---- A DROP-DOWN, AS IN EXCEL (user, 2026-10-28: "its not a
            // droplist like excel"): one click opens the list, the picks stay
            _arrangement.Occupancy.Clear();
            Call(d, "Changed", (object)null);

            Pick(ListOf(d), "1:1", "1:2");

            // Pressed where it is drawn
            d.UpdateLayout();

            Border lobbyCategory = CellOf(ListOf(d), "1:2", "occupancy", 0);

            Check("a list's cell shows its arrow", Within<TextBlock>(lobbyCategory).Any(t => t.Text == "▾"));

            PressOn(Within<TextBlock>(lobbyCategory).First());
            Pump();

            var dropped = lobbyCategory.Child as ComboBox;

            Check("one click on a category: its list open, both rows still picked - " +
                  (dropped == null ? "no list" : "open " + dropped.IsDropDownOpen) + ", " + ListOf(d).SelectedItems.Count + " picked",
                dropped != null && dropped.IsDropDownOpen && ListOf(d).SelectedItems.Count == 2);

            if (dropped != null)
            {
                var again = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseDownEvent
                };

                typeof(System.Windows.Input.MouseButtonEventArgs).GetProperty("ClickCount").GetSetMethod(true).Invoke(again, new object[] { 2 });
                dropped.RaiseEvent(again);

                Check("...a double-click's second press does not close it", again.Handled);

                dropped.SelectedIndex = 2;
                dropped.IsDropDownOpen = false;
                Pump();
            }

            Check("...picked from it: every picked room takes it - " + string.Join(", ", _arrangement.Occupancy.Select(o => o.Key + " " + o.Value.FreshCategory)),
                _arrangement.Occupancy.Count == 2 && _arrangement.Occupancy.Values.All(o => o.FreshCategory == "Office Space"));

            Pick(ListOf(d), "1:1", "1:2");
            d.UpdateLayout();

            Border other = CellOf(ListOf(d), "2:3", "occupancy", 2);
            PressOn(Within<TextBlock>(other).First());
            Pump();

            Check("a click on a row not picked: that row alone picked, its list open",
                other.Child is ComboBox && ListOf(d).SelectedItems.Count == 1 &&
                ((ListBoxItem)ListOf(d).SelectedItems[0]).Tag == Item(ListOf(d), "2:3").Tag);

            if (other.Child is ComboBox left)
            {
                left.IsDropDownOpen = false;
                Pump();
            }

            Check("...closed with nothing picked: nothing chosen", !_arrangement.Occupancy.ContainsKey("2:3"));

            // ---- scrolled sideways (user, 2026-10-28: "many columns are
            // empty without headers"): the headings past the window there
            d.Width = d.MinWidth;
            d.UpdateLayout();

            ScrollViewer sideways = FindAll<ScrollViewer>(ListOf(d)).First();
            sideways.ScrollToRightEnd();
            d.UpdateLayout();
            Pump();

            var headGrid = Field<Grid>(d, "_heads");
            var lastHead = (Border)((FrameworkElement)Within<TextBlock>(headGrid).First(t => t.Text == "Total Outside Air").Parent).Parent;
            Border lastCell = CellOf(ListOf(d), "1:2", "calc", 0);

            double X(FrameworkElement e) => e.TranslatePoint(new Point(0, 0), d).X;

            Check("scrolled to the right: the last heading over its column, laid out whole - " +
                  $"offset {sideways.HorizontalOffset:0}, {X(lastHead):0.#}/{X(lastCell):0.#}",
                sideways.HorizontalOffset > 0 && Math.Abs(X(lastHead) - X(lastCell)) < 0.6 &&
                System.Windows.Controls.Primitives.LayoutInformation.GetLayoutClip(headGrid) == null);
        });
    }

    // A press as a person makes it, on a cell: the mouse's own event down
    // the tree - each element raises its left-button one from it
    private static void PressOn(UIElement on)
    {
        on.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseDownEvent
        });
    }

    private static VENTUNENCLOSED.Room Unenclosed(long link, long id, string number, string name, string model,
                                                  string level, double levelZ, string inside = null)
    {
        return new VENTUNENCLOSED.Room
        {
            LinkId = link, RoomId = id, Number = number, Name = name, LinkName = model,
            Level = level, LevelZ = levelZ, Inside = inside
        };
    }

    private static void NotEnclosed()
    {
        Console.WriteLine();
        Console.WriteLine("rooms with no area");

        var rooms = new List<VENTUNENCLOSED.Room>
        {
            Unenclosed(7, 3, "BP01.B1.10", "STORE", "ARC-B", "B1-BASEMENT", -1.35),
            Unenclosed(5, 9, "BP01.00.4", "LOBBY", "ARC-A", "00-GROUND FLOOR", 4.05),
            Unenclosed(7, 2, "BP01.B1.2", "PUMP ROOM", "ARC-B", "B1-BASEMENT", -1.35, "BP01.B1.001 PLANT"),
            Unenclosed(7, 4, "BP01.01.1", "OFFICE", "ARC-B", "01-FIRST FLOOR", 9.55),
            Unenclosed(5, 8, "BP01.B1.7", "STAIR", "ARC-A", "B1-BASEMENT", -1.35)
        };

        List<string> order = VENTUNENCLOSED.InOrder(rooms).Select(r => r.Number).ToList();

        Check("by model, up the building, by number as read (B1.2 before B1.10): " + string.Join(", ", order),
            order.SequenceEqual(new[] { "BP01.B1.7", "BP01.00.4", "BP01.B1.2", "BP01.B1.10", "BP01.01.1" }));

        Check("not enclosed, or redundant inside another room",
            rooms[0].Why == "Not enclosed" && rooms[2].Why == "Inside another room - BP01.B1.001 PLANT");

        string[] lines = VENTUNENCLOSED.Text(rooms).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        Check("copied as a heading and one tab-separated line a room, in order: " + lines.Length + " line(s)",
            lines.Length == 6 && lines[0] == "Model\tLevel\tNumber\tName\tWhy" &&
            lines[1] == "ARC-A\tB1-BASEMENT\tBP01.B1.7\tSTAIR\tNot enclosed" &&
            lines[3] == "ARC-B\tB1-BASEMENT\tBP01.B1.2\tPUMP ROOM\tInside another room - BP01.B1.001 PLANT");

        Check("a tab in a name does not break its line",
            VENTUNENCLOSED.Text(new[] { Unenclosed(1, 1, "1", "A\tB", "M", "L", 0) }).Contains("\t1\tA B\t"));

        Check("the button: a count, and ... only when there is a list",
            VENTUNENCLOSED.Said(0) == "Not enclosed (0)" && VENTUNENCLOSED.Said(5) == "Not enclosed (5)...");

        // The real window: the button counts what the main window hands it,
        // asked again at each fill
        var held = new List<VENTUNENCLOSED.Room>();

        VENTROOMSWINDOW made = Make(null);
        made.NotEnclosed = () => held;

        Run(made, d =>
        {
            var button = (Button)typeof(VENTROOMSWINDOW).GetField("_unenclosed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(d);

            Check("none read: the button says 0 and is off", "Not enclosed (0)".Equals(button.Content) && !button.IsEnabled);

            held.AddRange(rooms);
            d.Refill(null);

            Check("rooms read since it opened: counted at the next fill - " + button.Content,
                "Not enclosed (5)...".Equals(button.Content) && button.IsEnabled);

            var dialog = (Window)typeof(VENTROOMSWINDOW).GetMethod("NotEnclosedList", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(d, null);

            ListView list = null;

            foreach (object part in ((DockPanel)dialog.Content).Children)
            {
                if (part is ListView view)
                    list = view;
            }

            List<VENTUNENCLOSED.Room> shown = list == null
                ? new List<VENTUNENCLOSED.Room>()
                : list.ItemsSource.Cast<VENTUNENCLOSED.Room>().ToList();

            var columns = list == null ? new List<string>() : ((GridView)list.View).Columns.Select(c => (string)c.Header).ToList();

            Check("the list: every room, as handed over, under Model, Level, Number, Name, Why - " + string.Join(", ", columns),
                shown.Count == 5 && columns.SequenceEqual(new[] { "Model", "Level", "Number", "Name", "Why" }));

            // Every column bound to a PROPERTY - WPF shows a field as an
            // empty cell (the first render: only Why, a property, showed)
            List<string> paths = list == null
                ? new List<string>()
                : ((GridView)list.View).Columns.Select(c => ((System.Windows.Data.Binding)c.DisplayMemberBinding).Path.Path).ToList();

            Check("...each column bound to a property of the room, so its cell is not empty: " + string.Join(", ", paths),
                paths.Count == 5 && paths.All(path => typeof(VENTUNENCLOSED.Room).GetProperty(path) != null));
        });
    }

    // A room's area and height cells, as shown
    private static TextBlock[] RoomCells(ListBox list, string key)
    {
        var panel = (DockPanel)Item(list, key).Content;
        return CellsIn(panel.Children.OfType<StackPanel>().FirstOrDefault(p => "room".Equals(p.Tag)));
    }

    // The window's column headings showing, by text
    private static List<string> HeadsShowing(VENTROOMSWINDOW d)
    {
        var heads = (Grid)typeof(VENTROOMSWINDOW).GetField("_heads", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(d);

        bool Showing(DependencyObject at)
        {
            for (; at != null && at != heads; at = LogicalTreeHelper.GetParent(at))
            {
                if (at is UIElement element && element.Visibility != Visibility.Visible)
                    return false;
            }

            return true;
        }

        // In no particular order - a sheet's headings, left to right and
        // right to left
        return Within<TextBlock>(heads).Where(t => !string.IsNullOrEmpty(t.Text) && Showing(t))
            .Select(t => t.Text).OrderBy(t => t, StringComparer.Ordinal).ToList();
    }

    // A room's four airflow cells, as shown: the Sheet's fresh and extract
    // (its m² aside), the Model's fresh and extract
    private static string[] Cells(ListBox list, string key)
    {
        return FlowCellsOf(list, key).Select(t => t.Text).ToArray();
    }

    private static TextBlock[] FlowCellsOf(ListBox list, string key)
    {
        var panel = (DockPanel)Item(list, key).Content;

        TextBlock[] sheet = CellsIn(panel.Children.OfType<StackPanel>().First(p => "sheet".Equals(p.Tag)));
        TextBlock[] model = CellsIn(panel.Children.OfType<StackPanel>().First(p => "model".Equals(p.Tag)));

        return sheet.Skip(sheet.Length - 2).Concat(model).ToArray();
    }

    // A block of a room's cells, by its tag
    private static TextBlock[] BlockOf(ListBox list, string key, string tag)
    {
        var panel = (DockPanel)Item(list, key).Content;
        return CellsIn(panel.Children.OfType<StackPanel>().FirstOrDefault(p => tag.Equals(p.Tag)));
    }

    private static Border CellOf(ListBox list, string key, string tag, int nth)
    {
        var panel = (DockPanel)Item(list, key).Content;
        return panel.Children.OfType<StackPanel>().First(p => tag.Equals(p.Tag)).Children.OfType<Border>().ElementAt(nth);
    }

    private static void Pick(ListBox list, params string[] keys)
    {
        PickLines(list, keys.Select(k => Item(list, k)).ToArray());
    }

    private static void PickLines(ListBox list, params ListBoxItem[] items)
    {
        list.SelectedItems.Clear();

        foreach (ListBoxItem item in items)
            list.SelectedItems.Add(item);
    }

    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        // The columns chosen are the PERSON's file - never this test's
        VENTCOLUMNS.SettingsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arrangeflowtest-columns-" + Guid.NewGuid().ToString("N") + ".txt");

        Console.WriteLine();
        Console.WriteLine("AUTOVENT - Levels and rooms: the airflows, the ticks, the arranging");
        Console.WriteLine();

        // ---- the sheet's merges -------------------------------------------
        List<List<string>> merges = VENTFLOWS.SheetMerges(Rows().Select(r => r.Key), Sheet());

        Check("rooms sharing a merged cell are one set each, a side's merge once",
            merges.Count == 2 && merges.Any(m => m.SequenceEqual(new[] { "2:1", "2:2" })) &&
            merges.Any(m => m.SequenceEqual(new[] { "1:1", "3:1" })));

        var arrangement = new VENTARRANGE.Arrangement();
        int refused;
        int adopted = VENTFLOWS.Adopt(arrangement, Rows(), Sheet(), out refused);

        Check("a container's merge is adopted, each side into its own merges; one across containers is not",
            adopted == 2 && refused == 1 && arrangement.MergeOf("2:1") != null &&
            arrangement.MergeOf("2:1").Contains("2:2") && arrangement.ExtractMergeOf("2:1") != null &&
            arrangement.ExtractMergeOf("2:1").Contains("2:2") && arrangement.MergeOf("1:1") == null);

        Check("one already held is not adopted twice",
            VENTFLOWS.Adopt(arrangement, Rows(), Sheet(), out refused) == 0 && arrangement.Merges.Count == 1 &&
            arrangement.ExtractMerges.Count == 1);

        // A side's merges alone (2026-10-22): the corridor and A2's living
        // share a fresh air cell only
        Check("the sheet's merges of one side: fresh air has the corridor pair, extract has not",
            VENTFLOWS.SheetMerges(Rows().Select(r => r.Key), Sheet(), VENTFLOWS.FreshSide).Any(m => m.Contains("1:1")) &&
            !VENTFLOWS.SheetMerges(Rows().Select(r => r.Key), Sheet(), VENTFLOWS.ExtractSide).Any(m => m.Contains("1:1")));

        // ---- an endcap's air back to its rooms ------------------------------
        // Group A: 30 l/s, one endcap; group B: 10 l/s, two endcaps (5 each).
        // Endcap 1 serves A and B, endcap 2 serves B - what a run writes:
        var values = new Dictionary<string, double> { ["A"] = 30, ["B"] = 10 };
        var tally = new Dictionary<string, VENTFCUCHECK.Room>
        {
            ["A"] = new VENTFCUCHECK.Room { Key = "A", FreshEndcaps = 1 },
            ["B"] = new VENTFCUCHECK.Room { Key = "B", FreshEndcaps = 2 }
        };

        double one = VENTFCUCHECK.Carried(new[] { "A", "B" }, values, tally, true);
        double two = VENTFCUCHECK.Carried(new[] { "B" }, values, tally, true);

        Dictionary<string, double> back = VENTFLOWS.Attribute(new[]
        {
            new KeyValuePair<List<string>, double>(new List<string> { "A", "B" }, one),
            new KeyValuePair<List<string>, double>(new List<string> { "B" }, two)
        }, values, tally, true);

        Check($"a run's own endcap values read back as the sheet's: A {back["A"]}, B {back["B"]}",
            Near(one, 35) && Near(two, 5) && Near(back["A"], 30) && Near(back["B"], 10));

        Dictionary<string, double> halved = VENTFLOWS.Attribute(new[]
        {
            new KeyValuePair<List<string>, double>(new List<string> { "A", "B" }, 70)
        }, values, tally, true);

        Check("an endcap carrying double gives each its part, doubled - and the whole is kept",
            Near(halved["A"], 60) && Near(halved["B"], 10) && Near(halved.Values.Sum(), 70));

        Dictionary<string, double> equal = VENTFLOWS.Attribute(new[]
        {
            new KeyValuePair<List<string>, double>(new List<string> { "X", "Y" }, 40)
        }, new Dictionary<string, double>(), null, true);

        Check("nothing in the sheet to weigh by: equal parts", Near(equal["X"], 20) && Near(equal["Y"], 20));

        // ---- a group's value over its rooms, and back ----------------------
        Dictionary<string, double> split = VENTFLOWS.Split(30, new[] { "a", "b" }, new Dictionary<string, double> { ["a"] = 20, ["b"] = 10 });
        Dictionary<string, double> even = VENTFLOWS.Split(30, new[] { "a", "b", "c" }, null);

        Check("split by the rooms' own values, else evenly",
            Near(split["a"], 20) && Near(split["b"], 10) && Near(even["c"], 10));

        Dictionary<string, VENTFLOWS.Air> overrides = VENTFLOWS.Overrides(new[]
        {
            new KeyValuePair<List<string>, VENTFLOWS.Air>(new List<string> { "a", "b" }, A(40, None))
        });

        Check("a group's value on its first room, 0 on the rest - the run adds them up once",
            Near(overrides["a"].Fresh, 40) && Near(overrides["b"].Fresh, 0) &&
            double.IsNaN(overrides["a"].Extract) && double.IsNaN(overrides["b"].Extract));

        Check("a total over rooms with none is none",
            double.IsNaN(VENTFLOWS.Total(new[] { "a" }, k => None)) && Near(VENTFLOWS.Total(new[] { "a", "b" }, k => k == "a" ? 5 : None), 5));

        Check("typed values read: a number, blank, not a number",
            VENTFLOWS.Read("12.5") == 12.5 && double.IsNaN(VENTFLOWS.Read(" ").Value) && VENTFLOWS.Read("abc") == null &&
            VENTFLOWS.Read("-3") == null);

        Check("none and 0 are the same air; 15 and 0 are not",
            VENTFLOWS.Same(None, 0) && !VENTFLOWS.Same(15, 0) && VENTFLOWS.Same(10, 10.01));

        // ---- the merges the FCUs suggest (user, 2026-10-19) -----------------
        Dictionary<string, List<VENTARRANGE.Row>> levels = RowsOf(new VENTARRANGE.Arrangement());
        int contested;

        // KIFAF's: one FCU with its fresh endcap supplies A1's living, kitchen
        // and bedroom - and, through its Revit system, A2's living as well
        List<VENTFLOWS.FcuGroup> kifaf = VENTFLOWS.FcuGroups(
            new[] { U(7, 1, "2:1", "2:2", "2:3", "3:1") }, levels, new VENTARRANGE.Arrangement(), out contested);

        Check("an FCU's rooms in one container are suggested; a stray room elsewhere is left out and named",
            kifaf.Count == 1 && kifaf[0].Unit == 7 && kifaf[0].Level == Level &&
            kifaf[0].Rooms.SequenceEqual(new[] { "2:1", "2:2", "2:3" }) && kifaf[0].Left.SequenceEqual(new[] { "3:1" }));

        Check("a unit with no fresh air connection suggests nothing",
            VENTFLOWS.FcuGroups(new[] { U(8, 0, "1:1", "1:2") }, levels, null, out contested).Count == 0);

        Check("a unit spread over several containers suggests nothing (one room in each)",
            VENTFLOWS.FcuGroups(new[] { U(9, 1, "1:1", "2:1", "3:1") }, levels, null, out contested).Count == 0);

        Check("an even split between two containers suggests nothing",
            VENTFLOWS.FcuGroups(new[] { U(10, 1, "1:1", "1:2", "2:1", "2:2") }, levels, null, out contested).Count == 0);

        Check("two rooms of four in one place, one each elsewhere: not more than half, nothing",
            VENTFLOWS.FcuGroups(new[] { U(15, 1, "1:1", "1:2", "2:1", "3:1") }, levels, null, out contested).Count == 0);

        Check("rooms outside containers, on one FCU: suggested",
            VENTFLOWS.FcuGroups(new[] { U(11, 1, "1:1", "1:2") }, levels, null, out contested).Count == 1);

        List<VENTFLOWS.FcuGroup> both = VENTFLOWS.FcuGroups(
            new[] { U(12, 1, "1:1", "1:2"), U(13, 1, "1:2", "1:1") }, levels, null, out contested);

        Check("two units claiming one room: neither suggestion stands, and it is counted",
            both.Count == 0 && contested == 2);

        var held = new VENTARRANGE.Arrangement();
        held.Merges.Add(new List<string> { "1:1", "1:2" });

        Check("rooms already merged together: nothing to suggest",
            VENTFLOWS.FcuGroups(new[] { U(11, 1, "1:1", "1:2") }, levels, held, out contested).Count == 0);

        var part = new VENTARRANGE.Arrangement();
        part.Merges.Add(new List<string> { "2:1", "2:2" });

        Check("two of three merged (the foyer left out): the three are suggested",
            VENTFLOWS.FcuGroups(new[] { U(7, 1, "2:1", "2:2", "2:3") }, levels, part, out contested).Count == 1);

        Check("a unit reaching rooms the window has not got: those are ignored",
            VENTFLOWS.FcuGroups(new[] { U(14, 1, "2:1", "9:9", "9:8") }, levels, null, out contested).Count == 0);

        // ---- the window -------------------------------------------------------
        Run(Make(Carrying()), d =>
        {
            ListBox list = ListOf(d);

            string[] living = Cells(list, "2:1");
            string[] kitchen = Cells(list, "2:2");

            // Sheet: fresh, extract | Model: fresh, extract
            Check("a merged group's air on its first row, sheet then model: 25 + 0, 0 + 50 | 25, 50 - " + string.Join(" | ", living),
                living[0] == "25" && living[1] == "50" && living[2] == "25" && living[3] == "50");

            Check("its other row points up to it", kitchen.All(c => c == "↑"));

            Check("the sheet's merge was made the arrangement's on opening - and the main window told to save it",
                _arrangement.MergeOf("2:2") != null && _arranged == 1);

            TextBlock Now(string key) => FlowCellsOf(list, key)[2];

            string[] lobby = Cells(list, "1:2");
            Check("the model beside the sheet: 30 in the sheet, 22 on the terminals - marked, where 25 / 25 is not",
                lobby[0] == "30" && lobby[2] == "22" && Now("1:2").Foreground != Now("2:1").Foreground);

            // Not adopted - but the run still takes the sheet's merge, so the
            // window shows the pair as the run will: 10 + 20 on the corridor
            string[] corridor = Cells(list, "1:1");
            Check("a sheet merge across containers: not adopted, shown as the run takes it - " + string.Join(" | ", corridor),
                corridor[0] == "30" && corridor[2] == "10" && Cells(list, "3:1")[0] == "↑" &&
                _arrangement.MergeOf("1:1") == null);

            // That pair shares a FRESH AIR cell only: its extract is each
            // room's own (2026-10-22)
            Check("a sheet merge of fresh air cells only: the extract columns stay each room's own - " +
                  string.Join(" | ", Cells(list, "3:1")),
                Cells(list, "3:1")[0] == "↑" && Cells(list, "3:1")[1] != "↑" && Cells(list, "3:1")[3] != "↑");

            // The rows' colours (user, 2026-10-19)
            var differs =(Brush)typeof(VENTROOMSWINDOW).GetField("Differs", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var twice = (Brush)typeof(VENTROOMSWINDOW).GetField("Twice", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

            Check("sheet and model differ (lobby 30 / 22, bedroom 15 / 0): yellow",
                Item(list, "1:2").Background == differs && Item(list, "2:3").Background == differs);

            Check("a merged group that differs is yellow on all its rows (corridor 10 + 20 / 10)",
                Item(list, "1:1").Background == differs && Item(list, "3:1").Background == differs);

            Check("the same on both sides: no colour (kitchen 25 / 50 both ways)",
                Item(list, "2:2").Background != differs && Item(list, "2:2").Background != twice);

            Check("a room number used twice on the level: red on both rooms, over yellow",
                Item(list, "2:1").Background == twice && Item(list, "3:2").Background == twice);

            // Write the picked group
            Pick(list, "2:2");
            Call(d, "WriteNow");

            Check("writing a picked merged room writes its whole group: 25 / 50 on the first, 0 / 0 on the other",
                d.Writes != null && Near(d.Writes["2:1"].Fresh, 25) && Near(d.Writes["2:1"].Extract, 50) &&
                Near(d.Writes["2:2"].Fresh, 0) && Near(d.Writes["2:2"].Extract, 0) &&
                d.WriteFresh.SetEquals(new[] { "2:1", "2:2" }) && d.WriteExtract.SetEquals(new[] { "2:1", "2:2" }));

            Check("...handed to the main window, the window left open, the adopted merge in the arrangement",
                _written && d.IsVisible && _arrangement.MergeOf("2:1") != null);
        });

        // THE SHEET'S AIR IS READ ONLY (user, 2026-10-20: "the flowrate from
        // the sheet should be read only"): nothing to set it with, and nothing
        // picked writes nothing
        Run(Make(Carrying()), d =>
        {
            ListBox list = ListOf(d);

            Check("no Set airflow... - the sheet's values cannot be changed here",
                !FindAll<Button>(d).Any(b => "Set airflow...".Equals(b.Content)) &&
                typeof(VENTROOMSWINDOW).GetField("_set", BindingFlags.NonPublic | BindingFlags.Instance) == null);

            list.SelectedItems.Clear();
            Call(d, "WriteNow");

            Check("Sheet, nothing picked: nothing written", d.Writes == null && !_written);
        });

        // A room with no air anywhere picked: nothing to write
        Run(Make(Carrying()), d =>
        {
            ListBox list = ListOf(d);

            Pick(list, "1:2");

            Field<IDictionary<string, VENTFLOWS.Sheet>>(d, "_sheet")["1:2"].Air = A(None, None);
            Call(d, "WriteNow");

            Check("a room with no airflow to write: nothing handed back", d.Writes == null && !_written);
        });

        // No terminals read (no link ticked): the sheet's values still show
        Run(Make(null), d =>
        {
            ListBox list = ListOf(d);
            string[] lobby = Cells(list, "1:2");

            Check("terminals not read: the sheet's air, '-' beside it", lobby[0] == "30" && lobby[2] == "-");

            // ...and once they are read, they show - the window open throughout
            d.Terminals(Carrying(), null);

            Check("terminals read while it is open: the Model columns filled in", Cells(ListOf(d), "1:2")[2] == "22");

            // A supply terminal whose ducts reach no unit: said on its room
            d.Terminals(Carrying(), null, new Dictionary<string, List<long>> { ["2:3"] = new List<long> { 4012345 } });

            System.Windows.Documents.Run Note(string key) =>
                RunsOf(Item(ListOf(d), key)).FirstOrDefault(r => r.Text.Contains("terminal on no FCU"));

            Check("a supply terminal on no FCU is said on its room, its id on hover - and only there",
                Note("2:3") != null && ((string)Note("2:3").ToolTip).Contains("4012345") && Note("2:1") == null);
        });

        // SHEET / MODEL (user, 2026-10-19/20: pressing Sheet "should be
        // showing the merge as per sheet", Model "according to model"). Each
        // press works its merges out afresh: Model the FCUs' and nothing
        // else, Sheet the sheet's and nothing else - a merge made by hand
        // lasts until the next press
        Run(Make(Carrying(), new List<VENTFLOWS.Unit> { U(7, 1, "2:1", "2:2", "2:3", "3:1"), U(8, 0, "1:1", "1:2") }), d =>
        {
            ListBox list = ListOf(d);

            string Line(string key) =>
                string.Concat(RunsOf(Item(list, key)).Select(r => r.Text));

            void View(bool model) =>
                typeof(VENTROOMSWINDOW).GetMethod("View", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(d, new object[] { model });

            List<string> MergeOf(string key) => _arrangement.MergeOf(key);

            Check("on Sheet the bedroom says it is on one FCU with the living and the kitchen: " + Line("2:3").Trim(),
                Line("2:3").Contains("one FCU with 201, 202") && MergeOf("2:3") == null);

            Check("the corridor, on a unit with no fresh air connection, says nothing", !Line("1:1").Contains("one FCU"));

            // A merge made by hand on Sheet: the corridor and the lobby
            Pick(list, "1:1", "1:2");
            Call(d, "MergePicked");

            Check("on Sheet: the sheet's merge and the one made by hand", MergeOf("1:2") != null && MergeOf("2:1") != null);

            // Where each merge comes from, on hover
            string Tip(string key) =>
                RunsOf(Item(ListOf(d), key))
                    .Where(r => r.Text.StartsWith("fresh air merged with")).Select(r => r.ToolTip as string).FirstOrDefault();

            Check("on Sheet the hover says which merge is the sheet's and which made by hand - " + Tip("2:1") + " / " + Tip("1:2"),
                Tip("2:1") == "Merged in the sheet" && Tip("1:2") != null && Tip("1:2").StartsWith("Merged by hand"));

            View(true);
            list = ListOf(d);
            Pick(list, "2:3");

            Check("Model: the FCU's three rooms merged, the other container's room left out - " +
                  (MergeOf("2:3") == null ? "none" : string.Join(", ", MergeOf("2:3"))),
                MergeOf("2:3") != null && MergeOf("2:3").Contains("2:1") && MergeOf("2:3").Contains("2:2") &&
                !MergeOf("2:3").Contains("3:1"));

            Check("Model: the model's merges ONLY - not the one made by hand on Sheet",
                MergeOf("1:2") == null && _arrangement.Merges.Count == 1 && _arrangement.ByModel);

            Check("Model: the hover names the FCU whose ducts reach the rooms - " + Tip("2:1"),
                Tip("2:1") != null && Tip("2:1").StartsWith("FCU 7 "));

            Check("Model: no note, Write on",
                !Line("2:3").Contains("one FCU") && Field<Button>(d, "_write").IsEnabled);

            // FRESH AIR ONLY (user, 2026-10-22): the FCU's group on fresh air,
            // every room its own extract - the kitchen's 50 on the kitchen
            string[] group = Cells(list, "2:1");
            string[] kitchenOwn = Cells(list, "2:2");

            Check("Model: fresh air as the FCU's group - its terminals' 25 on the first row, ↑ below; extract room by room, " +
                  "the kitchen's 50 on the kitchen: " + string.Join(" | ", group) + " / " + string.Join(" | ", kitchenOwn),
                group[2] == "25" && group[3] == "-" &&
                kitchenOwn[0] == "↑" && kitchenOwn[2] == "↑" && kitchenOwn[1] == "50" && kitchenOwn[3] == "50");

            Check("Model: no extract merged - the FCU's merge is fresh air only",
                _arrangement.ExtractMerges.Count == 0 && !Line("2:2").Contains("extract merged"));

            Check("Model: the corridor and A2's living are not one group (that merge is the sheet's)",
                Cells(list, "3:1")[0] != "↑" && Cells(list, "1:2")[0] != "↑");

            // Unmerged by hand on Model, then Model pressed again: the FCUs'
            // merges afresh
            Pick(list, "2:3");
            Call(d, "UnmergePicked");

            Check("Model, the bedroom unmerged by hand", MergeOf("2:3") == null);

            View(true);

            Check("Model pressed again: the model's merges afresh - the bedroom back with its FCU",
                MergeOf("2:3") != null && MergeOf("2:3").Count == 3);

            View(false);
            list = ListOf(d);

            Check("Sheet: the merges as per sheet and nothing else - the living and kitchen, the corridor pair " +
                  "from the sheet itself; the bedroom alone, the hand merge gone",
                MergeOf("2:3") == null && MergeOf("2:1") != null && MergeOf("2:1").Count == 2 && MergeOf("1:2") == null &&
                Cells(list, "3:1")[0] == "↑" && !_arrangement.ByModel);

            int before = _arranged;

            View(true);

            Check("Model again: the arrangement's at once, the main window told to save it",
                MergeOf("2:3") != null && MergeOf("2:3").Count == 3 && _arranged == before + 1);
        });

        // THE USER'S CASE (2026-10-20): merges saved as the window was left
        // on Model by an older version - the FCU's group in force, nothing
        // saying so. It opens on Sheet showing them; pressing Sheet - Sheet
        // already down - shows the merges as per sheet
        var olderSave = new VENTARRANGE.Arrangement();
        olderSave.Merges.Add(new List<string> { "2:1", "2:2", "2:3" });

        Run(Make(Carrying(), new List<VENTFLOWS.Unit> { U(7, 1, "2:1", "2:2", "2:3", "3:1") }, arrangement: olderSave), d =>
        {
            Check("opened on Sheet: the merges as per sheet AT ONCE - the older save's FCU group gone, the bedroom alone",
                Field<System.Windows.Controls.Primitives.ToggleButton>(d, "_sheetView").IsChecked == true &&
                olderSave.MergeOf("2:3") == null && olderSave.MergeOf("2:1") != null && olderSave.MergeOf("2:1").Count == 2 &&
                Cells(ListOf(d), "2:3")[0] != "↑" && _arranged == 1);

            // A merge made by hand, then Sheet pressed with Sheet down
            Pick(ListOf(d), "1:1", "1:2");
            Call(d, "MergePicked");

            typeof(VENTROOMSWINDOW).GetMethod("View", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(d, new object[] { false });

            Check("...a hand merge, then Sheet pressed with Sheet down: as per sheet again - the hand merge gone",
                olderSave.MergeOf("1:2") == null && olderSave.MergeOf("2:3") == null &&
                olderSave.MergeOf("2:1") != null && olderSave.MergeOf("2:1").Count == 2);
        });

        // LEFT ON MODEL, opened again: on Model, the MODEL's merges at once -
        // not the ones saved (a merge made by hand), not the sheet's
        // (A2's two rooms on one FCU; the sheet merges A1's living and kitchen)
        var leftOnModel = new VENTARRANGE.Arrangement { ByModel = true };
        leftOnModel.Merges.Add(new List<string> { "1:1", "1:2" });

        Run(Make(Carrying(), new List<VENTFLOWS.Unit> { U(9, 1, "3:1", "3:2") }, arrangement: leftOnModel), d =>
        {
            Check("opened as it was left: Model down, the model's merges shown - not the saved hand merge, nothing of the sheet's",
                Field<System.Windows.Controls.Primitives.ToggleButton>(d, "_modelView").IsChecked == true &&
                Field<System.Windows.Controls.Primitives.ToggleButton>(d, "_sheetView").IsChecked == false &&
                Cells(ListOf(d), "3:2")[0] == "↑" && leftOnModel.Merges.Count == 1 && leftOnModel.MergeOf("3:1") != null &&
                leftOnModel.MergeOf("1:2") == null && leftOnModel.MergeOf("2:1") == null);
        });

        // ...opened on Model before the terminals are read: the saved merges
        // stand until they arrive, then the model's
        var waiting = new VENTARRANGE.Arrangement { ByModel = true };
        waiting.Merges.Add(new List<string> { "1:1", "1:2" });

        Run(Make(null, null, arrangement: waiting), d =>
        {
            Check("Model, terminals not read yet: the saved merges stand", waiting.MergeOf("1:2") != null);

            d.Terminals(Carrying(), new List<VENTFLOWS.Unit> { U(9, 1, "3:1", "3:2") });

            Check("...the terminals read: the model's merges",
                waiting.MergeOf("1:2") == null && waiting.MergeOf("3:1") != null && Cells(ListOf(d), "3:2")[0] == "↑");
        });

        // THE SWITCH DECIDES WHAT IS WRITTEN (user, 2026-10-19): on Model, the
        // Model columns - the terminals' own air, grouped as the FCUs serve
        var units = new List<VENTFLOWS.Unit> { U(7, 1, "2:1", "2:2", "2:3", "3:1"), U(8, 0, "1:1", "1:2") };

        Run(Make(Carrying(), units), d =>
        {
            typeof(VENTROOMSWINDOW).GetMethod("View", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(d, new object[] { true });

            ListBox list = ListOf(d);
            Pick(list, "2:3");
            Call(d, "WriteNow");

            // (user, 2026-10-26: "from the new occupancy ... when 'model' is
            // selected") - nothing chosen here, so the new values are the
            // sheet's: 25 + 0 + 15 on the group's first room
            Check("Model, a room picked: its FCU group's NEW fresh air written, the group's 25 + 0 + 15 - not the terminals' 25",
                d.Writes != null && d.WroteModel &&
                Near(d.Writes["2:1"].Fresh, 40) && Near(d.Writes["2:3"].Fresh, 0) &&
                d.WriteFresh.SetEquals(new[] { "2:1", "2:2", "2:3" }));

            Check("...its extract by the room's own group: the bedroom carries none, so none is written - not the kitchen's 50",
                d.WriteExtract.Count == 0 && double.IsNaN(d.Writes["2:1"].Extract));

            Check("...with the Model's merges kept", _arrangement.MergeOf("2:3") != null);
        });

        Run(Make(Carrying(), units), d =>
        {
            typeof(VENTROOMSWINDOW).GetMethod("View", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(d, new object[] { true });

            ListBox list = ListOf(d);
            list.SelectedItems.Clear();
            Call(d, "WriteNow");

            Check("Model, nothing picked: nothing written - every level is too much to guess at", d.Writes == null && !_written);

            PickLines(list, LevelItem(list, Level));
            Call(d, "WriteNow");

            Check("Model, a level picked: every room on it with new air - the lobby's 30, not the terminals' 22 - " +
                  (d.Writes == null ? "none" : string.Join(", ", d.Writes.Keys.OrderBy(k => k))),
                d.Writes != null && d.Writes.ContainsKey("1:2") && Near(d.Writes["1:2"].Fresh, 30) &&
                d.Writes.ContainsKey("1:1") && d.WriteFresh.Contains("2:3") && !d.Writes.ContainsKey("3:2") &&
                !d.Writes.ContainsKey("4:1"));
        });

        Run(Make(Carrying(), units), d =>
        {
            Pick(ListOf(d), "1:2");
            Call(d, "WriteNow");

            Check("Sheet: the sheet's 30 for the lobby - not the terminals' 22",
                d.Writes != null && !d.WroteModel && Near(d.Writes["1:2"].Fresh, 30));
        });

        // No units read (no link ticked): no switch, no notes
        Run(Make(Carrying()), d =>
        {
            Check("no units read: no Sheet / Model switch",
                !Field<System.Windows.Controls.Primitives.ToggleButton>(d, "_modelView").IsVisible);
        });

        // ---- THE TICKS (the levels list's, in the window) ----------------------
        Run(Make(Carrying()), d =>
        {
            ListBox list = ListOf(d);

            Check("every level, every container and room under the open ones: " + Showing(list),
                Showing(list) == "level LEVEL 03,1:1,1:2,apt A1,2:1,2:2,2:3,apt A2,3:1,3:2,level LEVEL 04,4:1,4:2");

            Press(BoxOf(Item(list, "1:2")));

            Check("a room's box unticks the room - its level part-ticked, the main window told",
                !Tick("1:2").IsOn && _levels[0].Checked == null && _ticked == 1);

            Press(BoxOf(Item(list, "1:2")));
            Press(BoxOf(ContainerItem(list, "A1")));

            Check("a container's box unticks its units - and shows them unticked",
                !Tick("2:1").IsOn && !Tick("2:2").IsOn && !Tick("2:3").IsOn && Tick("3:1").IsOn &&
                BoxOf(ContainerItem(list, "A1")).IsChecked == false);

            Press(BoxOf(Item(list, "2:1")));

            Check("one unit ticked again: its container in the third state", BoxOf(ContainerItem(list, "A1")).IsChecked == null);

            Press(BoxOf(ContainerItem(list, "A1")));

            Check("...and its box from there ticks them all",
                Tick("2:2").IsOn && Tick("2:3").IsOn && BoxOf(ContainerItem(list, "A1")).IsChecked == true);

            // A box on a PICKED row ticks every picked row; a picked level
            // stands for its rooms
            PickLines(list, Item(list, "1:1"), Item(list, "3:2"), LevelItem(list, Other));
            Press(BoxOf(Item(list, "1:1")));

            Check("a box on a picked row unticks every picked row - a picked level all its rooms",
                !Tick("1:1").IsOn && !Tick("3:2").IsOn && !Tick("4:1").IsOn && !Tick("4:2").IsOn &&
                Tick("1:2").IsOn && _levels[1].Checked == false);

            list.SelectedItems.Clear();
            Press(BoxOf(LevelItem(list, Level)));

            Check("a part-ticked level's box ticks every room on it - never the third state's untick",
                _levels[0].Rooms.All(r => r.IsOn) && _levels[0].Checked == true && BoxOf(LevelItem(list, Level)).IsChecked == true);

            // A level closed: its heading, none of its rooms
            Call(d, "Toggle", _levels[1]);
            list = ListOf(d);

            Check("a closed level shows its heading and none of its rooms",
                LevelItem(list, Other) != null && Item(list, "4:1") == null && !_levels[1].IsExpanded);

            // Show in plan: one plan, one level - the one most picks are on,
            // the rest counted; the closed level picked stands for its two
            // rooms, which outnumber the one picked on LEVEL 03
            PickLines(list, Item(list, "1:1"), LevelItem(list, Other));
            Call(d, "ShowPicked");

            Check("Show in plan: the level most picks are on, the rest counted",
                _shownRooms != null && _shownRooms.Count == 2 && _shownRooms.All(r => r.Level == Other) && _shownElsewhere == 1);

            PickLines(list, Item(list, "1:1"), Item(list, "1:2"), LevelItem(list, Other));
            Call(d, "ShowPicked");

            Check("...and on a tie, the level higher in the list",
                _shownRooms.Count == 2 && _shownRooms.All(r => r.Level == Level) && _shownElsewhere == 2);
        });

        // THE SEARCH: hides rows only; a level's tick and All act on the rows
        // showing
        Run(Make(Carrying()), d =>
        {
            TextBox search = FindAll<TextBox>(d).First();
            search.Text = "kitchen";

            ListBox list = ListOf(d);

            Check("searching: the room found, under its container and level - the rest hidden: " + Showing(list),
                Showing(list) == "level LEVEL 03,apt A1,2:2");

            Press(BoxOf(LevelItem(list, Level)));

            Check("...a level's box acts on the rooms showing: the kitchen unticked, the rest left ticked",
                !Tick("2:2").IsOn && Tick("2:1").IsOn && Tick("1:1").IsOn && Tick("4:1").IsOn);

            Press(FindAll<Button>(d).First(b => "All".Equals(b.Content)));

            Check("...and All ticks the rows showing", Tick("2:2").IsOn);

            PickLines(list, LevelItem(list, Level));
            Call(d, "ShowPicked");

            Check("...and a picked level stands for its rooms showing - Show in plan frames the kitchen alone",
                _shownRooms != null && _shownRooms.Count == 1 && _shownRooms[0].Key == "2:2");

            search.Text = "level 04 store";

            Check("the level's name finds its rooms: " + Showing(ListOf(d)), Showing(ListOf(d)) == "level LEVEL 04,4:2");

            search.Text = string.Empty;

            Check("cleared: everything shows again",
                Showing(ListOf(d)) == "level LEVEL 03,1:1,1:2,apt A1,2:1,2:2,2:3,apt A2,3:1,3:2,level LEVEL 04,4:1,4:2");
        });

        // EVERY CHANGE AT ONCE: no OK - the main window saves it
        Run(Make(Carrying()), d =>
        {
            ListBox list = ListOf(d);
            int before = _arranged;

            Pick(list, "1:1", "1:2");
            Call(d, "MergePicked");

            Check("a merge is the arrangement's at once, the main window told, the two still picked",
                _arrangement.MergeOf("1:2") != null && _arranged == before + 1 &&
                ListOf(d).SelectedItems.Count == 2);

            Pick(ListOf(d), "4:2");
            Call(d, "MovePicked", -1);

            Check("a move up, within its level: " + Showing(ListOf(d)),
                Showing(ListOf(d)).EndsWith("level LEVEL 04,4:2,4:1") && _arranged == before + 2);
        });

        // The Model's merges: an FCU group takes its rooms out of any other
        // merge; merges it does not touch are kept
        int notUsed;
        List<List<string>> byFcu = VENTFLOWS.ByFcu(
            new List<List<string>> { new List<string> { "1:1", "1:2" }, new List<string> { "2:1", "2:2" } },
            new[] { U(7, 1, "2:1", "2:2", "2:3") }, levels, out notUsed);

        Check("Model's merges: the FCU's group in place of the merge it overlaps; the others kept",
            byFcu.Count == 2 && byFcu.Any(m => m.SequenceEqual(new[] { "1:1", "1:2" })) &&
            byFcu.Any(m => m.SequenceEqual(new[] { "2:1", "2:2", "2:3" })));

        HashSet<string> repeated = VENTFLOWS.Repeated(Rows());

        Check("room numbers used twice: both rooms, and only them",
            repeated.SetEquals(new[] { "2:1", "3:2" }));

        // One interior model placed twice: its room twice, under two link ids
        // - not a repeat (user, 2026-10-19)
        HashSet<string> placedTwice = VENTFLOWS.Repeated(new[]
        {
            Room("40:7", "121.126", "KITCHEN", "A1"), Room("41:7", "121.126", "KITCHEN", "A2"),
            Room("40:8", "121.127", "FOYER", "A1"), Room("41:8", "121.127", "FOYER", "A2"),
            Room("41:9", "121.127", "STORE", "A2")
        });

        Check("the same room of a model placed twice is not red; a different room on its number is - all three of them",
            !placedTwice.Contains("40:7") && !placedTwice.Contains("41:7") &&
            placedTwice.SetEquals(new[] { "40:8", "41:8", "41:9" }));

        // No sheet and no terminals: no airflow columns
        Run(Make(null, null, sheet: false), d =>
        {
            ListBox list = ListOf(d);
            var panel = (DockPanel)Item(list, "1:2").Content;

            Check("no sheet, no terminals: no airflow columns, no occupancy",
                !panel.Children.OfType<StackPanel>().Any(p => new[] { "sheet", "new", "model", "occupancy", "calc" }.Contains((string)p.Tag)));

            // The room's area and height (user, 2026-10-24) - with no sheet too
            TextBlock[] lobby = RoomCells(list, "1:2");
            TextBlock[] corridor = RoomCells(list, "1:1");

            Check("no sheet: the room's area and height still shown - " + string.Join(" | ", lobby.Select(t => t.Text)),
                lobby.Length == 2 && lobby[0].Text == 20.4.ToString("0.0") && lobby[1].Text == 2.7.ToString("0.00"));

            Check("...the height says it is to the false ceiling", "To the false ceiling".Equals(lobby[1].ToolTip));

            Check("...a room with nothing read shows -", corridor.Length == 2 && corridor.All(t => t.Text == "-"));

            List<string> heads = HeadsShowing(d);

            Check("...headed Number, Name, Notes, Room, m², m; no airflow headings: " + string.Join(", ", heads),
                heads.SequenceEqual(new[] { "m", "m²", "Name", "Notes", "Number", "Room" }.OrderBy(t => t, StringComparer.Ordinal)));
        });

        // ROOMS WITH NO AREA (user, 2026-10-24: "add the list of not
        // enclosed rooms")
        NotEnclosed();

        // THE FOUR (user, 2026-10-25: "yes build all four") - Differences
        // only, Sheet / Model said for what they are, the sheet's columns,
        // a column chooser
        SheetColumns();

        // With a sheet: the room's columns left of the airflows, all headed
        Run(Make(null), d =>
        {
            ListBox list = ListOf(d);
            var panel = (DockPanel)Item(list, "1:2").Content;

            List<StackPanel> right = panel.Children.OfType<StackPanel>().ToList();

            Check("with a sheet: right to left the occupancy, the Model, the new values, the Sheet, the room - " +
                  string.Join(", ", right.Select(p => p.Tag)),
                right.Select(p => (string)p.Tag).SequenceEqual(new[] { "occupancy", "model", "new", "sheet", "room" }) &&
                right.All(p => DockPanel.GetDock(p) == Dock.Right));

            // AS A SHEET (user, 2026-10-25: "make the rooms and level window
            // look more like excel view")
            var merged = (DockPanel)Item(list, "2:1").Content;
            Grid middle = merged.Children.OfType<Grid>().First();
            TextBlock nameCell = (TextBlock)((Border)middle.Children[0]).Child;
            TextBlock notesCell = (TextBlock)((Border)middle.Children[1]).Child;

            string Said(TextBlock t) => string.Concat(t.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text));

            Check("a room's notes in their own column, not after its name - name '" + Said(nameCell) + "', notes '" + Said(notesCell) + "'",
                Said(nameCell) == "LIVING" && Said(notesCell).StartsWith("fresh air merged with"));

            List<Border> cellsOf = Within<Border>(merged).Where(b => b.Child == null || !(b.Child is Border)).ToList();

            Check("every cell gridded - a line on its right and under it: " + cellsOf.Count + " cell(s)",
                cellsOf.Count == 16 && cellsOf.All(b => b.BorderThickness == new Thickness(0, 0, 1, 1)));

            var heads = (Grid)typeof(VENTROOMSWINDOW).GetField("_heads", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(d);

            double X(FrameworkElement e) => e.TranslatePoint(new Point(0, 0), d).X;
            double Right(FrameworkElement e) => X(e) + e.ActualWidth;

            Border HeadOf(string text, int nth = 0) =>
                Within<TextBlock>(heads).Where(t => t.Text == text).Select(t => (Border)t.Parent).ElementAt(nth);

            List<Border> flowCells = new List<Border> { CellOf(list, "2:1", "sheet", 0), null, null, CellOf(list, "2:1", "model", 1) };

            // The headings left to right: the Sheet's Fresh first, the
            // Model's Extract second
            Border sheetFresh = HeadOf("Fresh", 0);
            Border modelExtract = HeadOf("Extract", 1);

            Check("the headings over their cells: Number, Name, Notes, the Sheet's Fresh and the Model's Extract - " +
                  $"{X(HeadOf("Number")):0.#}/{X((Border)merged.Children[2]):0.#}, {X(HeadOf("Notes")):0.#}/{X((Border)middle.Children[1]):0.#}, " +
                  $"{X(sheetFresh):0.#}/{X(flowCells[0]):0.#}, {Right(modelExtract):0.#}/{Right(flowCells[3]):0.#}",
                Math.Abs(X(HeadOf("Number")) - X((Border)merged.Children[2])) < 0.6 &&
                Math.Abs(X(HeadOf("Name")) - X((Border)middle.Children[0])) < 0.6 &&
                Math.Abs(X(HeadOf("Notes")) - X((Border)middle.Children[1])) < 0.6 &&
                Math.Abs(X(sheetFresh) - X(flowCells[0])) < 0.6 &&
                Math.Abs(Right(modelExtract) - Right(flowCells[3])) < 0.6);

            Check("...and every heading showing: " + string.Join(", ", HeadsShowing(d)),
                HeadsShowing(d).SequenceEqual(new[] { "Number", "Name", "Notes", "Room", "m²", "m", "Sheet", "Fresh", "Extract",
                        "New values", "Fresh air", "Exhaust air", "Model", "Fresh", "Extract", "Occupancy", "Fresh category", "Extract category", "Use" }
                    .OrderBy(t => t, StringComparer.Ordinal)));

            TextBlock[] living = RoomCells(list, "2:1");

            Check("...a room with no ceiling over it: its own height, and says so - " + string.Join(" | ", living.Select(t => t.Text)),
                living.Length == 2 && living[0].Text == 35.0.ToString("0.0") && living[1].Text == 3.1.ToString("0.00") &&
                "No ceiling found - the room's own height".Equals(living[1].ToolTip));
        });

        // THE OCCUPANCY CHOSEN HERE (user, 2026-10-26) - last: it shows every
        // column again
        Occupancy();

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "all checks passed" : _failed + " check(s) FAILED");

        app.Shutdown();

        return _failed == 0 ? 0 : 1;
    }
}
