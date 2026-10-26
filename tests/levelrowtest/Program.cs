using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Linq;

// The levels tree's ticks, with the REAL VENTLEVELROWS compiled in.
//
// A ticked room is exported and has its airflow written; an unticked one
// is left out of both. The level's tick follows its rooms - all, none,
// or the third state - and a click on it sets all of them. What a person
// chose must survive the list being rebuilt, and building the list must
// never be mistaken for somebody choosing.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static List<VENTWindow.RoomRow> Rooms(long link, params long[] ids)
    {
        return ids.Select(id => new VENTWindow.RoomRow
        {
            LinkId = link,
            RoomId = id,
            Number = "R" + id,
            Name = "ROOM " + id
        }).ToList();
    }

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("AUTOVENT - level and room ticks");
        Console.WriteLine();

        // ---- building --------------------------------------------------
        var choices = new Dictionary<string, bool>();

        var level = VENTWindow.LevelRow.Build(
            "GR", "     (in the sheet)", true, true, Rooms(7, 1, 2, 3, 4), choices);

        Check("a pre-ticked level starts with every room ticked",
            level.Rooms.All(r => r.IsOn) && level.Checked == true);

        Check("and building it is not taken for anybody choosing",
            choices.Count == 0);

        Check("its label counts them all",
            level.Label == "GR     4 room(s)     (in the sheet)");

        // ---- a room unticked -------------------------------------------
        level.Rooms[1].IsOn = false;

        Check("one room unticked puts the level in the third state",
            level.Checked == null);

        Check("the label says how many of them are ticked",
            level.Label == "GR     3 of 4 room(s)     (in the sheet)");

        Check("and that one choice is remembered, and only it",
            choices.Count == 1 && choices["7:2"] == false);

        level.Rooms[0].IsOn = false;
        level.Rooms[2].IsOn = false;
        level.Rooms[3].IsOn = false;

        Check("every room unticked leaves the level unticked, not in between",
            level.Checked == false);

        // ---- the level clicked -------------------------------------------
        level.Checked = true;

        Check("ticking the level ticks every room",
            level.Rooms.All(r => r.IsOn) && level.Checked == true);

        Check("and remembers each of them as chosen",
            choices.Count == 4 && choices.Values.All(v => v));

        level.Rooms[3].IsOn = false;

        Check("then a room unticked goes back to the third state",
            level.Checked == null);

        level.Checked = false;

        Check("unticking the level from there unticks every room",
            level.Rooms.All(r => !r.IsOn) && level.Checked == false);

        // A click on a three-state box that is in the third state lands
        // on TICKED (WPF: anything but true goes to true)
        level.Rooms[0].IsOn = true;
        level.Checked = true;

        Check("ticking a level in the third state ticks all its rooms",
            level.Rooms.All(r => r.IsOn));

        // ---- rebuilt ------------------------------------------------------
        level.Rooms[2].IsOn = false;

        var rebuilt = VENTWindow.LevelRow.Build(
            "GR", "     (in the sheet)", true, true, Rooms(7, 1, 2, 3, 4, 5), choices);

        Check("a rebuild keeps what a person chose for each room",
            rebuilt.Rooms.Single(r => r.RoomId == 3).IsOn == false &&
            rebuilt.Rooms.Single(r => r.RoomId == 1).IsOn);

        Check("a room new since then follows the level's pre-tick",
            rebuilt.Rooms.Single(r => r.RoomId == 5).IsOn);

        Check("and the level's tick is worked out from them",
            rebuilt.Checked == null && rebuilt.TickedRooms == 4);

        var notInSheet = VENTWindow.LevelRow.Build(
            "P1", string.Empty, true, false, Rooms(7, 1, 2, 3), choices);

        Check("an untouched room follows its level's pre-tick even when a " +
              "choice exists for the same room id in ANOTHER link",
            VENTWindow.LevelRow.Build("P2", string.Empty, true, false, Rooms(8, 1), choices)
                .Rooms[0].IsOn == false);

        Check("a choice made under one pre-tick survives a rebuild under another",
            notInSheet.Rooms.Single(r => r.RoomId == 1).IsOn &&
            notInSheet.Rooms.Single(r => r.RoomId == 3).IsOn == false);

        // ---- a level with no rooms -----------------------------------------
        var empty = VENTWindow.LevelRow.Build(
            "ROOF", string.Empty, true, true, Rooms(7), choices);

        Check("a level with no rooms keeps its own tick",
            empty.Checked == true);

        empty.Checked = false;

        Check("and a click still turns it off",
            empty.Checked == false);

        Check("its label says it has no rooms",
            empty.Label == "ROOF     no rooms");

        var nothingTicked = VENTWindow.LevelRow.Build(
            "GR", string.Empty, false, true, Rooms(7), null);

        Check("with no link ticked there is nothing to count",
            nothingTicked.Label == "GR");

        // ---- what the runs are handed ---------------------------------------
        Check("the key is link and room, the one REVITROOMS files rooms by",
            VENTWindow.RoomRow.KeyOf(12345678901, 42) == "12345678901:42");

        Searching();

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    // ---- searching the levels list --------------------------------------
    //
    // User (2026-10-10): "add search bar also into the rooms to process".
    // It only HIDES rows - ticks stand and still count - and what a person
    // does while it is on acts on the rows SHOWING.
    private static List<VENTWindow.LevelRow> Tower(Dictionary<string, bool> choices, bool l03Open = false)
    {
        VENTWindow.RoomRow R(long id, string number, string name) =>
            new VENTWindow.RoomRow { LinkId = 9, RoomId = id, Number = number, Name = name };

        var l03 = VENTWindow.LevelRow.Build("LEVEL 03", "", true, true, new[]
        {
            R(1, "TA-03-31", "CORRIDOR"),
            R(2, "121.056", "2BDR-A1  /  MASTER BEDROOM"),
            R(3, "121.057", "2BDR-A1  /  DRESSING"),
            R(4, "121.105", "2BDR-A2  /  MASTER BEDROOM")
        }, choices);

        var l04 = VENTWindow.LevelRow.Build("LEVEL 04A", "", true, true, new[]
        {
            R(11, "TA-04-31", "CORRIDOR"),
            R(12, "121.401", "2BDR-A1  /  BEDROOM")
        }, choices);

        var roof = VENTWindow.LevelRow.Build("ROOF", "", true, false, new VENTWindow.RoomRow[0], choices);

        l03.IsExpanded = l03Open;
        l04.IsExpanded = true;

        return new List<VENTWindow.LevelRow> { l03, l04, roof };
    }

    // What Levels and rooms shows: each level showing, and the rooms
    // showing of each open one
    private static string Showing(List<VENTWindow.LevelRow> tree)
    {
        var rows = new List<string>();

        foreach (VENTWindow.LevelRow level in tree.Where(l => l.IsShown))
        {
            rows.Add(level.Name);

            if (level.IsExpanded)
                rows.AddRange(level.Rooms.Where(r => r.IsShown).Select(r => r.Number));
        }

        return string.Join(",", rows);
    }

    private static void Searching()
    {
        Console.WriteLine();
        Console.WriteLine("  searching the levels list");
        Console.WriteLine();

        var choices = new Dictionary<string, bool>();
        var tree = Tower(choices);
        var l03 = tree[0];
        var l04 = tree[1];
        var open = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        int found = VENTWindow.Search.Apply(tree, "bedroom", open);

        Check("a search shows the rooms it finds, under their levels, and hides the rest",
            found == 3 && Showing(tree) == "LEVEL 03,121.056,121.105,LEVEL 04A,121.401");

        Check("...a level with nothing found is hidden",
            !tree[2].IsShown);

        Check("...a level showing what was found is OPENED - and how it stood is remembered",
            l03.IsExpanded && open.Count == 3 && open["LEVEL 03"] == false && open["LEVEL 04A"] == true);

        Check("hidden is not unticked: every room still ticked, every count still whole",
            tree.SelectMany(l => l.Rooms).All(r => r.IsOn) && l03.Label == "LEVEL 03     4 room(s)" &&
            choices.Count == 0);

        Check("each room is found with its LEVEL's name: 'level 03 bedroom' leaves LEVEL 03's alone",
            VENTWindow.Search.Apply(tree, "level 03 bedroom", open) == 2 &&
            Showing(tree) == "LEVEL 03,121.056,121.105");

        Check("...a level's name alone finds the whole level - and a level with no rooms by its name",
            VENTWindow.Search.Apply(tree, "level 04a", open) == 2 && Showing(tree) == "LEVEL 04A,TA-04-31,121.401" &&
            VENTWindow.Search.Apply(tree, "roof", open) == 0 && Showing(tree) == "ROOF");

        // ---- a level's tick while the search is on
        VENTWindow.Search.Apply(tree, "bedroom", open);

        bool means = l03.ClickMeans();

        l03.TickShown(means);

        Check("a level's tick acts on its rooms SHOWING: both bedrooms unticked, the corridor and dressing left ticked",
            !means && !l03.Rooms[1].IsOn && !l03.Rooms[3].IsOn && l03.Rooms[0].IsOn && l03.Rooms[2].IsOn);

        Check("...the level now part-ticked, and only those two remembered as chosen",
            l03.Checked == null && choices.Count == 2 && choices["9:2"] == false && choices["9:4"] == false);

        Check("...and the next click ticks the rooms showing again - never stuck on the third state",
            l03.ClickMeans() && Act(l03) && l03.Rooms[1].IsOn && l03.Rooms[3].IsOn && l03.Checked == true);

        // ---- All / None (what the window's buttons do)
        foreach (VENTWindow.LevelRow level in tree.Where(l => l.IsShown))
            level.TickShown(false);

        Check("None with a search on unticks the rooms showing - only",
            tree.SelectMany(l => l.Rooms).Count(r => !r.IsOn) == 3 &&
            !l04.Rooms[1].IsOn && l04.Rooms[0].IsOn && l03.Rooms[0].IsOn);

        foreach (VENTWindow.LevelRow level in tree.Where(l => l.IsShown))
            level.TickShown(true);

        // ---- what a room is found by, when told (Levels and rooms hands in
        // the container, the model and the names before a rename)
        Func<VENTWindow.LevelRow, VENTWindow.RoomRow, string> words = (level, room) =>
            level.Name + " " + room.Number + (room.RoomId == 3 ? " WALK-IN WARDROBE" : string.Empty);

        Check("found by the words handed in, and not by the name it was not given",
            VENTWindow.Search.Apply(tree, "wardrobe", open, words) == 1 && Showing(tree) == "LEVEL 03,121.057" &&
            VENTWindow.Search.Apply(tree, "dressing", open, words) == 0);

        // ---- clearing it
        Check("cleared: everything shows again, and the levels it opened are put back as they were",
            VENTWindow.Search.Apply(tree, "", open) == 0 &&
            tree.All(l => l.IsShown && l.Rooms.All(r => r.IsShown)) &&
            !l03.IsExpanded && l04.IsExpanded && open.Count == 0);

        // ---- no search: exactly as before
        var plain = Tower(new Dictionary<string, bool>())[0];
        plain.Rooms[0].IsOn = false;

        Check("no search: a level's click means what it always did - part-ticked goes to ticked, all of it",
            plain.ClickMeans() && Act(plain) && plain.Rooms.All(r => r.IsOn) && plain.Checked == true);

        // ---- the list rebuilt in the middle of a search
        var memory = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var before = Tower(new Dictionary<string, bool>());

        VENTWindow.Search.Apply(before, "bedroom", memory);

        // Rebuilt the way the window rebuilds it: open where it was open
        var rebuilt = Tower(new Dictionary<string, bool>(), l03Open: true);

        VENTWindow.Search.Apply(rebuilt, "bedroom", memory);
        VENTWindow.Search.Apply(rebuilt, "", memory);

        Check("rebuilt mid-search, the levels still go back to how they stood BEFORE the search",
            !rebuilt[0].IsExpanded && rebuilt[1].IsExpanded);
    }

    // A click on the level's box, as the window makes it
    private static bool Act(VENTWindow.LevelRow level)
    {
        level.TickShown(level.ClickMeans());
        return true;
    }
}
