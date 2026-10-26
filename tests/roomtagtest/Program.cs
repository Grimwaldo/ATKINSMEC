using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// ROOM TAGS (user, 2026-10-23): the rooms of the ticked linked models shown
// untagged in the plans placed on sheets, tagged "without duplication" -
// a dependent's tags are its primary's, two dependents of one plan share
// one tag, a model placed twice is two rooms, a second run places nothing.
// Duplicates (user, 2026-10-24: "add option in the addin to remove
// duplicate tags"): one tag of a room in one owner view stays per plan on
// a sheet - a room tagged once in each of two dependents keeps both.
// Then the real window, off-screen: the links search (ticks stand while
// hidden), the check, the plan ticks, what Tag missing and Remove
// duplicates hand over, what is remembered.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    // ==================================================
    // A model: four placements, four plans on sheets
    // ==================================================

    private const long Arc = 11, TowerA = 12, TowerB = 13, Str = 14;

    private static TAGPLAN.Room R(long link, long id, string number, string name)
    {
        return new TAGPLAN.Room { Link = link, Id = id, Number = number, Name = name, X = id, Y = link };
    }

    private static readonly List<TAGPLAN.Link> Links = new List<TAGPLAN.Link>
    {
        new TAGPLAN.Link { Id = Arc, Label = "ARC-000010", Rooms = 3 },
        new TAGPLAN.Link { Id = TowerA, Label = "ARC-000020 : 1 : Tower A", Rooms = 2 },
        new TAGPLAN.Link { Id = TowerB, Label = "ARC-000020 : 2 : Tower B", Rooms = 2 },
        new TAGPLAN.Link { Id = Str, Label = "STR-000300", Rooms = 0 }
    };

    // Plan 1001 on M-10; its dependents 1002 (M-2) and 1003 (M-3), which
    // overlap on room 101; a ceiling plan 1004 on M-20. Tower A and Tower
    // B are one model placed twice: room 201 is in both.
    private static List<TAGPLAN.View> Views(ICollection<long> links)
    {
        var views = new List<TAGPLAN.View>
        {
            new TAGPLAN.View
            {
                Sheet = 1, SheetNumber = "M-10", SheetName = "LEVEL 01 MECHANICAL", Id = 1001, Name = "LEVEL 01 - MECH",
                Shown = { R(Arc, 101, "101", "OFFICE"), R(Arc, 102, "102", "CORRIDOR"), R(TowerA, 201, "201", "LOBBY"), R(TowerB, 201, "201", "LOBBY") }
            },
            new TAGPLAN.View
            {
                Sheet = 2, SheetNumber = "M-2", SheetName = "LEVEL 01 PART A", Id = 1002, Name = "LEVEL 01 - MECH - A", Owner = 1001,
                Shown = { R(Arc, 101, "101", "OFFICE"), R(TowerA, 201, "201", "LOBBY") }
            },
            new TAGPLAN.View
            {
                Sheet = 3, SheetNumber = "M-3", SheetName = "LEVEL 01 PART B", Id = 1003, Name = "LEVEL 01 - MECH - B", Owner = 1001,
                Shown = { R(Arc, 101, "101", "OFFICE"), R(Arc, 103, "103", "STORE") },
                OutsideCrop = 2,
                Notes = { "STR-000300 is hidden in this view." }
            },
            new TAGPLAN.View
            {
                Sheet = 4, SheetNumber = "M-20", SheetName = "LEVEL 02 CEILING", Id = 1004, Name = "LEVEL 02 RCP", Ceiling = true,
                Shown = { R(Arc, 102, "102", "CORRIDOR") }
            }
        };

        foreach (TAGPLAN.View view in views)
            view.Shown.RemoveAll(r => !links.Contains(r.Link));

        return views;
    }

    // Already tagged: 102 in plan 1001 and in the ceiling plan; 101 in a
    // view on no sheet (1005), which tags nothing in these
    private static Dictionary<long, HashSet<string>> Tagged()
    {
        return new Dictionary<long, HashSet<string>>
        {
            { 1001, new HashSet<string> { TAGPLAN.KeyOf(Arc, 102) } },
            { 1004, new HashSet<string> { TAGPLAN.KeyOf(Arc, 102) } },
            { 1005, new HashSet<string> { TAGPLAN.KeyOf(Arc, 101) } }
        };
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "preview")
            return Preview(args.Length > 1 ? args[1] : "out", args.Length > 2 ? float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 1f);

        Console.WriteLine();
        Console.WriteLine("ROOM TAGS - missing room tags on sheets, tagged once");
        Console.WriteLine();

        string temp = Path.Combine(Path.GetTempPath(), "roomtagtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        TAGPLAN.SettingsPath = Path.Combine(temp, "roomtags.txt");
        TAGPLAN.LogPath = Path.Combine(temp, "roomtags_log.txt");

        try
        {
            PlanTests();
            DuplicateTests();
            OrderTests();
            SettingsTests();
            WindowTests();
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "all checks passed" : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    // ==================================================
    // The check and what to tag
    // ==================================================

    private static void PlanTests()
    {
        Console.WriteLine("the check");

        var all = new List<long> { Arc, TowerA, TowerB };

        List<TAGPLAN.Row> rows = TAGPLAN.Check(Views(all), Tagged());

        Func<long, TAGPLAN.Row> row = id => rows.Single(r => r.View.Id == id);
        Func<TAGPLAN.Row, string> missing = r => string.Join(" ", r.Missing.Select(m => m.Key).ToArray());

        Check("a room tagged in the plan counts as tagged there: 1001 shows 4, 1 tagged",
            row(1001).Shown == 4 && row(1001).Tagged == 1);

        Check("a DEPENDENT reads its primary's tags: 1002 and 1003 own no tags, yet 102 is not asked of them " +
              "(1002 missing " + missing(row(1002)) + ")",
            row(1002).Missing.Count == 2 && row(1003).Missing.Count == 2);

        Check("a tag in a view on no sheet tags nothing here: 101 missing in 1001",
            row(1001).Missing.Any(m => m.Key == TAGPLAN.KeyOf(Arc, 101)));

        Check("a model placed twice is two rooms: 201 of Tower A and of Tower B both missing in 1001",
            row(1001).Missing.Count(m => m.Id == 201) == 2);

        Check("the ceiling plan's own tag counts: nothing missing in 1004", row(1004).Missing.Count == 0 && row(1004).Tagged == 1);

        var twice = new TAGPLAN.View { Id = 1, SheetNumber = "X", Shown = { R(Arc, 101, "101", "OFFICE"), R(Arc, 101, "101", "OFFICE") } };

        TAGPLAN.Row once = TAGPLAN.Check(new[] { twice }, null)[0];

        Check("a room the read gives twice counts once", once.Shown == 1 && once.Missing.Count == 1);

        Console.WriteLine();
        Console.WriteLine("what to tag - never twice");

        Dictionary<long, List<TAGPLAN.Room>> plan = TAGPLAN.ToTag(rows);

        Check("every tag goes in the view that owns the annotation: only 1001 (" + string.Join(", ", plan.Keys.Select(k => k.ToString()).ToArray()) + ")",
            plan.Count == 1 && plan.ContainsKey(1001));

        string keys = string.Join(" ", plan[1001].Select(r => r.Key).ToArray());

        Check("101, missing in the plan and both its dependents, is tagged ONCE: " + keys,
            plan[1001].Count(r => r.Key == TAGPLAN.KeyOf(Arc, 101)) == 1);

        Check("the four tags are 101, 201 (Tower A), 103, 201 (Tower B), in the rows' order",
            keys == "11:101 12:201 11:103 13:201");

        Check("Missing counts the tags to place, not the rows' sum (7)", TAGPLAN.Missing(rows) == 4);

        // Place them, read again: nothing left, and nothing to place
        Dictionary<long, HashSet<string>> after = Tagged();

        foreach (KeyValuePair<long, List<TAGPLAN.Room>> owner in plan)
            foreach (TAGPLAN.Room room in owner.Value)
                after[owner.Key].Add(room.Key);

        List<TAGPLAN.Row> again = TAGPLAN.Check(Views(all), after);

        Check("A SECOND RUN PLACES NOTHING: every row tagged, ToTag empty",
            again.All(r => r.Missing.Count == 0) && TAGPLAN.ToTag(again).Count == 0);

        List<TAGPLAN.Row> withoutB = TAGPLAN.Check(Views(new List<long> { Arc, TowerA }), Tagged());

        Check("an unticked link is not asked about: Tower B's 201 not missing without it",
            withoutB.All(r => r.Missing.All(m => m.Link != TowerB)));

        Console.WriteLine();
        Console.WriteLine("the crop");

        Check("inside", TAGPLAN.InCrop(5, 5, 0, 0, 10, 10));
        Check("on the edge", TAGPLAN.InCrop(10, 0, 0, 0, 10, 10));
        Check("outside in x", !TAGPLAN.InCrop(10.01, 5, 0, 0, 10, 10));
        Check("outside in y", !TAGPLAN.InCrop(5, -0.01, 0, 0, 10, 10));

        Console.WriteLine();
    }

    // ==================================================
    // Duplicates
    // ==================================================

    private static TAGPLAN.Tag T(long id, long owner, long link, long room, long type, params long[] seenIn)
    {
        var tag = new TAGPLAN.Tag { Id = id, Owner = owner, Link = link, Key = TAGPLAN.KeyOf(link, room), Type = type };

        tag.SeenIn.AddRange(seenIn);

        return tag;
    }

    private static void DuplicateTests()
    {
        Console.WriteLine("duplicates");

        var all = new List<long> { Arc, TowerA, TowerB };

        // 1001 is the primary, 1002 and 1003 its dependents; 1004 alone
        var tags = new List<TAGPLAN.Tag>
        {
            T(10, 1004, Arc, 102, 501, 1004),
            T(11, 1004, Arc, 102, 501, 1004),

            // 101 tagged once in each dependent's area: both stay
            T(20, 1001, Arc, 101, 501, 1001, 1002),
            T(21, 1001, Arc, 101, 501, 1001, 1003),

            // 102: the newer tag shows in more plans - it stays
            T(30, 1001, Arc, 102, 501, 1001),
            T(31, 1001, Arc, 102, 501, 1001, 1002),
            T(32, 1001, Arc, 102, 501, 1001, 1002),

            // 201 of Tower A: two tag types
            T(40, 1001, TowerA, 201, 501, 1001),
            T(41, 1001, TowerA, 201, 502, 1001),

            // 103: the newer one pinned
            T(51, 1001, Arc, 103, 501, 1001),
            T(52, 1001, Arc, 103, 501, 1001),

            // 201 of Tower B: one shown, one in no plan on a sheet
            T(60, 1001, TowerB, 201, 501, 1001),
            T(61, 1001, TowerB, 201, 501),

            // 101 in the ceiling plan: another owner, not a duplicate of 20
            T(70, 1004, Arc, 101, 501, 1004)
        };

        tags.Single(t => t.Id == 52).Fixed = true;

        List<TAGPLAN.Row> rows = TAGPLAN.Check(Views(all), TAGPLAN.TaggedOf(tags));

        HashSet<long> gone = TAGPLAN.Doubles(rows, tags, false);

        Func<long, List<long>> dupOf = id => rows.Single(r => r.View.Id == id).Duplicates;

        Check("two tags of one room, one type, in one plan: the newer goes (11)", gone.Contains(11) && !gone.Contains(10));

        Check("a room tagged once in each dependent's area keeps both (20, 21)", !gone.Contains(20) && !gone.Contains(21));

        Check("the tag showing in more plans stays though newer (31); the others go (30, 32)",
            !gone.Contains(31) && gone.Contains(30) && gone.Contains(32));

        Check("two tag types of one room are not duplicates by default (40, 41)", !gone.Contains(40) && !gone.Contains(41));

        Check("a pinned tag stays, the older one goes (52 kept, 51 gone)", !gone.Contains(52) && gone.Contains(51));

        Check("a tag in no plan on a sheet is left alone (61), and so is its pair (60)", !gone.Contains(61) && !gone.Contains(60));

        Check("one room in two owner views is not a duplicate (70)", !gone.Contains(70));

        Check("all told: 11, 30, 32, 51 - " + string.Join(" ", gone.OrderBy(i => i).Select(i => i.ToString()).ToArray()),
            gone.OrderBy(i => i).SequenceEqual(new long[] { 11, 30, 32, 51 }));

        Check("each row lists the duplicates its plan shows: 1001 [30 32 51], 1002 [32], 1003 none, 1004 [11]",
            dupOf(1001).SequenceEqual(new long[] { 30, 32, 51 }) && dupOf(1002).SequenceEqual(new long[] { 32 }) &&
            dupOf(1003).Count == 0 && dupOf(1004).SequenceEqual(new long[] { 11 }));

        Check("ToRemove takes each once", TAGPLAN.ToRemove(rows).SequenceEqual(new long[] { 11, 30, 32, 51 }));

        Check("ToRemove of one dependent's row: only what it shows (32)",
            TAGPLAN.ToRemove(rows.Where(r => r.View.Id == 1002)).SequenceEqual(new long[] { 32 }));

        Check("a row with only duplicates has something to fix", rows.Single(r => r.View.Id == 1004).Fixable);

        HashSet<long> any = TAGPLAN.Doubles(rows, tags, true);

        Check("Any tag type: the second type goes too (41)", any.Contains(41) && !any.Contains(40) && dupOf(1001).Contains(41));

        TAGPLAN.Doubles(rows, tags, false);

        Check("and back: counted again from the same tags", !dupOf(1001).Contains(41));

        var removedAll = tags.Where(t => !gone.Contains(t.Id)).ToList();

        Check("REMOVED, READ AGAIN: no duplicates left", TAGPLAN.Doubles(TAGPLAN.Check(Views(all), TAGPLAN.TaggedOf(removedAll)), removedAll, false).Count == 0);

        Dictionary<long, HashSet<string>> tagged = TAGPLAN.TaggedOf(tags);

        Check("TaggedOf: every tag's room under its owner view",
            tagged[1001].SetEquals(new[] { "11:101", "11:102", "12:201", "11:103", "13:201" }) &&
            tagged[1004].SetEquals(new[] { "11:102", "11:101" }));

        Console.WriteLine();
    }

    private static void OrderTests()
    {
        Console.WriteLine("sheet order");

        Check("M-2 before M-10", TAGPLAN.CompareNumbers("M-2", "M-10") < 0);
        Check("M-010 is M-10", TAGPLAN.CompareNumbers("M-010", "M-10") == 0);
        Check("any case", TAGPLAN.CompareNumbers("m-101", "M-101") == 0);
        Check("M-101 before M-101A", TAGPLAN.CompareNumbers("M-101", "M-101A") < 0);
        Check("letters by letters: A-200 before M-100", TAGPLAN.CompareNumbers("A-200", "M-100") < 0);

        List<TAGPLAN.Row> rows = TAGPLAN.Check(Views(new List<long> { Arc }), null);

        Check("rows by sheet number as read: M-2, M-3, M-10, M-20 (" +
              string.Join(", ", rows.Select(r => r.View.SheetNumber).ToArray()) + ")",
            rows.Select(r => r.View.SheetNumber).SequenceEqual(new[] { "M-2", "M-3", "M-10", "M-20" }));

        Console.WriteLine();
    }

    // ==================================================
    // What is remembered
    // ==================================================

    private static void SettingsTests()
    {
        Console.WriteLine("what is remembered");

        HashSet<long> start = TAGPLAN.Ticked(Links, null);

        Check("nothing saved: the links with rooms ticked", start.SetEquals(new[] { Arc, TowerA, TowerB }));

        Check("the default ticks are saved as 'follow the default' (null)", TAGPLAN.Saved(Links, start, null) == null);

        List<string> two = TAGPLAN.Saved(Links, new HashSet<long> { TowerA, Str }, null);

        Check("other ticks are saved by label", two != null && two.SequenceEqual(new[] { "ARC-000020 : 1 : Tower A", "STR-000300" }));

        Check("none ticked is an empty list, not the default", TAGPLAN.Saved(Links, new HashSet<long>(), null).Count == 0);

        var before = new TAGPLAN.Choice { Model = "m", Links = new List<string> { "ARC-000010", "OLD-LINK" } };

        List<string> kept = TAGPLAN.Saved(Links, start, before);

        Check("a saved link not loaded today keeps its tick, even when the rest is the default",
            kept != null && kept.Contains("OLD-LINK") && kept.Contains("ARC-000010"));

        Check("saved labels tick their links, any case",
            TAGPLAN.Ticked(Links, new TAGPLAN.Choice { Links = new List<string> { "arc-000020 : 2 : tower b" } }).SetEquals(new[] { TowerB }));

        var choices = new List<TAGPLAN.Choice>
        {
            new TAGPLAN.Choice { Model = "file:a.rvt", Links = null, TagType = "M_Room Tag : Name\tand number" },
            new TAGPLAN.Choice { Model = "cloud:b", Links = new List<string>(), TagType = "" },
            new TAGPLAN.Choice { Model = "central:c", Links = new List<string> { "ARC-000010", "ARC-000020 : 1 : Tower A" } }
        };

        List<TAGPLAN.Choice> back = TAGPLAN.Parse(TAGPLAN.Compose(choices));

        Check("round trip: three models", back.Count == 3);
        Check("round trip: the default stays the default (null)", back[0].Links == null);
        Check("round trip: a tab in a name does not break the line", back[0].TagType == "M_Room Tag : Name and number");
        Check("round trip: none ticked stays none", back[1].Links != null && back[1].Links.Count == 0);
        Check("round trip: labels with ' : ' in them", back[2].Links.SequenceEqual(choices[2].Links));

        TAGPLAN.Keep(new TAGPLAN.Choice { Model = "file:a.rvt", TagType = "first" });
        TAGPLAN.Keep(new TAGPLAN.Choice { Model = "file:b.rvt", TagType = "other" });
        TAGPLAN.Keep(new TAGPLAN.Choice { Model = "file:a.rvt", TagType = "second" });

        Check("one model's save replaces its own lines and keeps the others'",
            TAGPLAN.Recall("file:a.rvt").TagType == "second" && TAGPLAN.Recall("file:b.rvt").TagType == "other" &&
            TAGPLAN.Parse(File.ReadAllText(TAGPLAN.SettingsPath)).Count == 2);

        Check("a model never saved (no key) is never remembered", TAGPLAN.Recall(null) == null);

        File.Delete(TAGPLAN.SettingsPath);

        Console.WriteLine();
    }

    // ==================================================
    // The real window, off-screen
    // ==================================================

    private static T Field<T>(object owner, string name)
    {
        return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }

    private static Button ButtonOf(Control root, string text)
    {
        foreach (Control c in root.Controls)
        {
            var button = c as Button;

            if (button != null && button.Text == text)
                return button;

            Button inside = ButtonOf(c, text);

            if (inside != null)
                return inside;
        }

        return null;
    }

    // The tag type picked - the window's own item, by reflection
    private static long TypeId(ComboBox types)
    {
        object item = types.SelectedItem;

        return item == null ? 0 : (long)item.GetType().GetField("Id").GetValue(item);
    }

    private static void Settle()
    {
        for (int i = 0; i < 5; i++)
            Application.DoEvents();
    }

    private static List<KeyValuePair<long, string>> Types()
    {
        return new List<KeyValuePair<long, string>>
        {
            new KeyValuePair<long, string>(501, "M_Room Tag : Name"),
            new KeyValuePair<long, string>(502, "M_Room Tag : Number and name")
        };
    }

    private class Model
    {
        public Dictionary<long, HashSet<string>> Tags = Tagged();
        public List<List<long>> Checks = new List<List<long>>();
        public List<Dictionary<long, List<TAGPLAN.Room>>> Placed = new List<Dictionary<long, List<TAGPLAN.Room>>>();
        public List<long> TypesUsed = new List<long>();

        // The tags already there - one each, as Tagged() has them, until
        // Duplicated adds more
        public List<TAGPLAN.Tag> TagList = new List<TAGPLAN.Tag>
        {
            T(30, 1001, Arc, 102, 501, 1001),
            T(10, 1004, Arc, 102, 501, 1004),
            T(90, 1005, Arc, 101, 501)
        };

        public List<List<long>> Removed = new List<List<long>>();

        public void Duplicated()
        {
            TagList.Add(T(11, 1004, Arc, 102, 501, 1004));
            TagList.Add(T(12, 1004, Arc, 102, 502, 1004));
            TagList.Add(T(31, 1001, Arc, 102, 501, 1001, 1002));
        }

        public TAGPLAN.Read Read(List<long> links)
        {
            Checks.Add(links);

            return new TAGPLAN.Read
            {
                Views = Views(links),
                Tagged = Tags.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value)),
                Tags = TagList.Where(t => links.Contains(t.Link)).ToList(),
                Sheets = 4
            };
        }

        public TAGPLAN.Removal Remove(List<long> ids)
        {
            Removed.Add(ids);

            var removal = new TAGPLAN.Removal { Asked = ids.Count };

            removal.Removed = TagList.RemoveAll(t => ids.Contains(t.Id));

            return removal;
        }

        public TAGPLAN.Done Tag(Dictionary<long, List<TAGPLAN.Room>> plan, long type)
        {
            Placed.Add(plan);
            TypesUsed.Add(type);

            var done = new TAGPLAN.Done { Views = plan.Count };

            foreach (KeyValuePair<long, List<TAGPLAN.Room>> owner in plan)
            {
                HashSet<string> keys;

                if (!Tags.TryGetValue(owner.Key, out keys))
                    Tags[owner.Key] = keys = new HashSet<string>();

                foreach (TAGPLAN.Room room in owner.Value)
                {
                    done.Asked++;

                    if (keys.Add(room.Key))
                        done.Placed++;
                }
            }

            return done;
        }
    }

    private static TAGForm Open(Model model, string key)
    {
        var form = new TAGForm("KIFAF-MEP", key, Links, Types(), 502, model.Read, model.Tag, model.Remove)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000),
            ShowInTaskbar = false
        };

        form.Show();
        Settle();

        return form;
    }

    private static void WindowTests()
    {
        Console.WriteLine("the window - links");

        const string Key = "file:c:\\jobs\\kifaf-mep.rvt";

        var model = new Model();
        TAGForm form = Open(model, Key);

        var links = Field<CheckedListBox>(form, "_lstLinks");
        var search = Field<TextBox>(form, "_txtLinks");
        var said = Field<Label>(form, "_lblLinks");
        var grid = Field<DataGridView>(form, "_grid");
        var rowsSaid = Field<Label>(form, "_lblRows");
        var tag = Field<Button>(form, "_btnTag");
        var types = Field<ComboBox>(form, "_cmbType");
        var status = Field<Label>(form, "_lblStatus");
        var picked = Field<ListBox>(form, "_lstMissing");
        var pickedSaid = Field<Label>(form, "_lblPicked");
        var sheets = Field<TextBox>(form, "_txtSheets");

        Func<List<long>> ticked = () => Field<HashSet<long>>(form, "_ticked").OrderBy(i => i).ToList();

        Check("every link listed, the ones with rooms ticked: " + said.Text,
            links.Items.Count == 4 && links.CheckedItems.Count == 3 && said.Text == "3 of 4 ticked.");

        Check("the tag type: Revit's default for room tags (502)", TypeId(types) == 502);

        Check("before a check: nothing to tag, and it says what to do",
            !tag.Enabled && rowsSaid.Text.StartsWith("Tick the linked models"));

        search.Text = "tower b";
        Settle();

        Check("the search narrows the list: " + links.Items.Count + " row(s)", links.Items.Count == 1 && links.Items[0].ToString().StartsWith("ARC-000020 : 2 : Tower B"));

        ButtonOf(form, "None").PerformClick();
        Settle();

        Check("None unticks only the links showing", ticked().SequenceEqual(new[] { Arc, TowerA }));
        Check("the line says ticked links are hidden: " + said.Text, said.Text.Contains("2 ticked model(s) hidden"));

        search.Text = string.Empty;
        Settle();

        Check("cleared: every link back, ticks as they were", links.Items.Count == 4 && links.CheckedItems.Count == 2);

        search.Text = "tower";
        Settle();
        ButtonOf(form, "All").PerformClick();
        search.Text = string.Empty;
        Settle();

        Check("All ticks the links showing", ticked().SequenceEqual(new[] { Arc, TowerA, TowerB }));

        int strAt = Enumerable.Range(0, links.Items.Count).Single(i => links.Items[i].ToString().StartsWith("STR"));

        links.SetItemChecked(strAt, true);
        Settle();

        Check("a tick in the list counts: " + said.Text, ticked().Contains(Str) && said.Text == "4 of 4 ticked.");

        links.SetItemChecked(strAt, false);
        Settle();

        Console.WriteLine();
        Console.WriteLine("the window - the check");

        ButtonOf(form, "Check sheets").PerformClick();
        Settle();

        Check("the check reads the ticked links", model.Checks.Count == 1 && model.Checks[0].SequenceEqual(new[] { Arc, TowerA, TowerB }));

        Func<int, string> sheetOf = i => (string)grid.Rows[i].Cells[1].Value;
        Func<int, int> missingOf = i => (int)grid.Rows[i].Cells[6].Value;
        Func<int, bool> tickOf = i => true.Equals(grid.Rows[i].Cells[0].Value);

        Check("a row a plan, by sheet number: " + string.Join(", ", Enumerable.Range(0, grid.Rows.Count).Select(sheetOf).ToArray()),
            grid.Rows.Count == 4 && Enumerable.Range(0, 4).Select(sheetOf).SequenceEqual(new[] { "M-2", "M-3", "M-10", "M-20" }));

        Check("missing per row 2, 2, 3, 0", Enumerable.Range(0, 4).Select(missingOf).SequenceEqual(new[] { 2, 2, 3, 0 }));

        Check("rows with something missing ticked; the complete one not, and not tickable",
            tickOf(0) && tickOf(1) && tickOf(2) && !tickOf(3) && grid.Rows[3].Cells[0].ReadOnly);

        Check("the ceiling plan says so", ((string)grid.Rows[3].Cells[3].Value).EndsWith("(ceiling)"));

        Check("the line counts tags to place once (4, not 7): " + rowsSaid.Text,
            rowsSaid.Text == "4 plan(s) on 4 sheet(s). 3 ticked, 4 tag(s) to place, 0 duplicate(s) to remove." && tag.Enabled);

        Check("the status line: " + status.Text, status.Text == "4 room tag(s) missing, 0 duplicate(s).");

        Check("the log is written on a check", File.Exists(TAGPLAN.LogPath) &&
            File.ReadAllText(TAGPLAN.LogPath).Contains("missing 103 STORE"));

        grid.CurrentCell = grid.Rows[1].Cells[1];
        Settle();

        List<string> lines = picked.Items.Cast<string>().ToList();

        Check("a picked plan lists what is missing in it, its notes and what the crop left out: " + string.Join(" | ", lines.ToArray()),
            lines.Any(l => l.StartsWith("103")) && lines.Any(l => l.Contains("STR-000300 is hidden")) &&
            lines.Any(l => l.Contains("2 room(s) outside the crop")));

        Check("a dependent says its tags go in its primary: " + pickedSaid.Text, pickedSaid.Text.Contains("dependent"));

        grid.Rows[1].Cells[0].Value = false;
        Settle();

        Check("an unticked plan drops its own rooms only - 101 still asked by M-2: " + rowsSaid.Text,
            rowsSaid.Text.EndsWith("2 ticked, 3 tag(s) to place, 0 duplicate(s) to remove."));

        sheets.Text = "M-1";
        Settle();

        int showing = grid.Rows.Cast<DataGridViewRow>().Count(r => r.Visible);

        Check("the sheets search hides rows (M-10 only): " + showing, showing == 1 && grid.Rows[2].Visible);

        ButtonOf(form, "Tick all").PerformClick();
        Settle();

        Check("Tick all acts on the rows showing: M-3 still unticked", !tickOf(1));

        sheets.Text = string.Empty;
        Settle();
        ButtonOf(form, "Tick all").PerformClick();
        Settle();

        Check("cleared, Tick all ticks it again: " + rowsSaid.Text, tickOf(1) && rowsSaid.Text.EndsWith("3 ticked, 4 tag(s) to place, 0 duplicate(s) to remove."));

        // A link changed after the check: the rows no longer say what is missing
        links.SetItemChecked(Enumerable.Range(0, links.Items.Count).Single(i => links.Items[i].ToString().StartsWith("ARC-000010")), false);
        Settle();

        Check("a link changed: the rows go, Tag is off, it says check again",
            grid.Rows.Count == 0 && !tag.Enabled && status.Text.Contains("Check sheets again"));

        links.SetItemChecked(Enumerable.Range(0, links.Items.Count).Single(i => links.Items[i].ToString().StartsWith("ARC-000010")), true);
        Settle();
        ButtonOf(form, "Check sheets").PerformClick();
        Settle();

        Console.WriteLine();
        Console.WriteLine("the window - tagging");

        form.Confirm = q => false;
        tag.PerformClick();
        Settle();

        Check("answered No: nothing placed", model.Placed.Count == 0);

        string asked = null;

        form.Confirm = q => { asked = q; return true; };
        types.SelectedIndex = 0;
        tag.PerformClick();
        Settle();

        Check("the question counts tags and views: " + asked, asked == "Place 4 room tag(s) in 1 view(s)?");

        Check("Tag missing hands over each room once, in the view owning the tags",
            model.Placed.Count == 1 && model.Placed[0].Count == 1 && model.Placed[0][1001].Count == 4 &&
            model.Placed[0][1001].Select(r => r.Key).Distinct().Count() == 4);

        Check("with the tag type picked (501)", model.TypesUsed.Single() == 501);

        Check("and reads the model again: nothing missing, Tag off: " + rowsSaid.Text,
            Enumerable.Range(0, grid.Rows.Count).All(i => missingOf(i) == 0) && !tag.Enabled);

        Check("the status line says what the model has: " + status.Text, status.Text == "Placed 4 room tag(s).");

        Check("the log has the tagging", File.ReadAllText(TAGPLAN.LogPath).Contains("TAGGED: 4 of 4 tag(s) in 1 view(s)"));

        typeof(TAGForm).GetMethod("TagMissing", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
        Settle();

        Check("pressed again: nothing to place, nothing handed over", model.Placed.Count == 1 && status.Text == "Nothing to tag.");

        Console.WriteLine();
        Console.WriteLine("the window - duplicates");

        var remove = Field<Button>(form, "_btnRemove");
        var anyType = Field<CheckBox>(form, "_chkAnyType");

        Func<int, int> dupsOf = i => (int)grid.Rows[i].Cells[7].Value;

        Check("none yet: Remove duplicates off", !remove.Enabled && Enumerable.Range(0, 4).All(i => dupsOf(i) == 0));

        model.Duplicated();
        ButtonOf(form, "Check sheets").PerformClick();
        Settle();

        Check("checked again: duplicates per row 0, 0, 1, 1 (M-10: 30 - 31 shows in more plans; M-20: 11)",
            Enumerable.Range(0, 4).Select(dupsOf).SequenceEqual(new[] { 0, 0, 1, 1 }));

        Check("a plan with only duplicates is ticked: " + rowsSaid.Text,
            tickOf(2) && tickOf(3) && !grid.Rows[3].Cells[0].ReadOnly &&
            rowsSaid.Text.EndsWith("2 ticked, 0 tag(s) to place, 2 duplicate(s) to remove.") && remove.Enabled && !tag.Enabled);

        Check("the status line: " + status.Text, status.Text == "0 room tag(s) missing, 2 duplicate(s).");

        Check("the log lists them", File.ReadAllText(TAGPLAN.LogPath).Contains("duplicates 1 (tag ids 30)"));

        anyType.Checked = true;
        Settle();

        Check("Any tag type: the other type counts too - M-20 has 2: " + rowsSaid.Text,
            dupsOf(3) == 2 && rowsSaid.Text.EndsWith("3 duplicate(s) to remove.") && model.Checks.Count == 4);

        anyType.Checked = false;
        Settle();

        Check("off again: 1", dupsOf(3) == 1);

        grid.CurrentCell = grid.Rows[3].Cells[1];
        Settle();

        lines = picked.Items.Cast<string>().ToList();

        Check("the picked plan names the room with duplicates: " + string.Join(" | ", lines.ToArray()),
            lines.Any(l => l.StartsWith("102") && l.Contains("CORRIDOR") && l.EndsWith("1 duplicate tag(s)")));

        grid.Rows[2].Cells[0].Value = false;
        Settle();

        form.Confirm = q => false;
        remove.PerformClick();
        Settle();

        Check("answered No: nothing removed", model.Removed.Count == 0);

        form.Confirm = q => { asked = q; return true; };
        remove.PerformClick();
        Settle();

        Check("the question: " + asked, asked == "Delete 1 duplicate room tag(s)? Every plan keeps a tag of each room.");

        Check("only the ticked plan's duplicate is handed over (11)",
            model.Removed.Count == 1 && model.Removed[0].SequenceEqual(new long[] { 11 }));

        Check("read again: M-20 clean, M-10 still 1 and still unticked; the status: " + status.Text,
            dupsOf(3) == 0 && dupsOf(2) == 1 && !tickOf(2) && status.Text == "Removed 1 duplicate tag(s).");

        Check("the log has the removal", File.ReadAllText(TAGPLAN.LogPath).Contains("REMOVED: 1 of 1 duplicate tag(s)"));

        grid.Rows[2].Cells[0].Value = true;
        Settle();
        remove.PerformClick();
        Settle();

        Check("M-10 ticked: the older tag goes (30), the one in two plans stays (31)",
            model.Removed.Count == 2 && model.Removed[1].SequenceEqual(new long[] { 30 }) && model.TagList.Any(t => t.Id == 31));

        Check("nothing left: Remove duplicates off", Enumerable.Range(0, 4).All(i => dupsOf(i) == 0) && !remove.Enabled);

        typeof(TAGForm).GetMethod("RemoveDuplicates", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
        Settle();

        Check("pressed again: nothing handed over", model.Removed.Count == 2 && status.Text == "No duplicates to remove.");

        form.Close();
        Settle();

        TAGPLAN.Choice saved = TAGPLAN.Recall(Key);

        Check("closed: the default ticks saved as the default, the type by name",
            saved != null && saved.Links == null && saved.TagType == "M_Room Tag : Name");

        Console.WriteLine();
        Console.WriteLine("the window - opened again");

        form = Open(new Model(), Key);

        types = Field<ComboBox>(form, "_cmbType");
        links = Field<CheckedListBox>(form, "_lstLinks");

        Check("the tag type comes back", TypeId(types) == 501);

        links.SetItemChecked(0, false);
        Settle();

        string unticked = links.Items[0].ToString();

        form.Close();
        Settle();

        saved = TAGPLAN.Recall(Key);

        Check("a link unticked is remembered: " + string.Join(", ", (saved.Links ?? new List<string>()).ToArray()),
            saved.Links != null && saved.Links.Count == 2 && !saved.Links.Any(l => unticked.StartsWith(l + " ")));

        form = Open(new Model(), Key);

        links = Field<CheckedListBox>(form, "_lstLinks");

        Check("and ticked as it was left", links.CheckedItems.Count == 2 && !links.GetItemChecked(0));

        form.Close();
        Settle();

        form = Open(new Model(), "file:c:\\jobs\\other.rvt");

        links = Field<CheckedListBox>(form, "_lstLinks");

        Check("another model starts from the defaults", links.CheckedItems.Count == 3);

        form.Close();
        Settle();

        Console.WriteLine();
    }

    // ==================================================
    // roomtagtest preview <folder> [scale] - LOOK at the window: after a
    // check, a dependent plan picked. Scale 1.5 = a screen at 150%, as
    // esppreview does it.
    // ==================================================

    private static int Preview(string folder, float scale)
    {
        Directory.CreateDirectory(folder);

        Application.EnableVisualStyles();

        TAGPLAN.SettingsPath = Path.Combine(folder, "roomtags.txt");
        TAGPLAN.LogPath = Path.Combine(folder, "roomtags_log.txt");

        var model = new Model();
        model.Duplicated();

        var links = new List<TAGPLAN.Link>(Links)
        {
            new TAGPLAN.Link { Id = 15, Label = "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 3 : location Tower C", Rooms = 412 }
        };

        var form = new TAGForm("5232535-ATR-TRA-ZZZ-3DM-HVC-000100", "file:preview", links, Types(), 502, model.Read, model.Tag, model.Remove)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(20, 20)
        };

        if (scale != 1f)
        {
            var own = new List<KeyValuePair<Control, Font>>();
            Action<Control> find = null;

            find = c =>
            {
                if (c.Parent == null || !c.Font.Equals(c.Parent.Font))
                    own.Add(new KeyValuePair<Control, Font>(c, c.Font));

                foreach (Control child in c.Controls)
                    find(child);
            };

            find(form);

            foreach (KeyValuePair<Control, Font> pair in own)
                pair.Key.Font = new Font(pair.Value.FontFamily, pair.Value.Size * scale, pair.Value.Style);

            form.Scale(new SizeF(scale, scale));
        }

        form.Show();
        Settle();

        ButtonOf(form, "Check sheets").PerformClick();
        Settle();

        var grid = Field<DataGridView>(form, "_grid");
        grid.CurrentCell = grid.Rows[1].Cells[1];
        Settle();

        string tag = scale == 1f ? string.Empty : "-" + scale.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + "x";
        string path = Path.Combine(folder, "roomtags" + tag + ".png");

        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(path);
        }

        Console.WriteLine("  " + path + "  " + form.Width + "x" + form.Height);

        form.Close();

        return 0;
    }
}
