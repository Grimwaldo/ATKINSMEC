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

        Picking();

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    // Three levels: A open with four rooms, B closed with two, C open
    // with none. Shown in the tree: A, a1..a4, B, C.
    private static List<VENTWindow.LevelRow> Tree()
    {
        var choices = new Dictionary<string, bool>();

        var a = VENTWindow.LevelRow.Build("A", "", true, true, Rooms(1, 1, 2, 3, 4), choices);
        var b = VENTWindow.LevelRow.Build("B", "", true, true, Rooms(1, 5, 6), choices);
        var c = VENTWindow.LevelRow.Build("C", "", true, false, Rooms(1), choices);

        a.IsExpanded = true;
        c.IsExpanded = true;

        return new List<VENTWindow.LevelRow> { a, b, c };
    }

    private static string Picked(List<VENTWindow.LevelRow> tree)
    {
        return string.Join(",", VENTWindow.Picks.All(tree)
            .Where(VENTWindow.Picks.IsPicked)
            .Select(r => r is VENTWindow.LevelRow
                ? ((VENTWindow.LevelRow)r).Name
                : ((VENTWindow.RoomRow)r).Number));
    }

    private static void Picking()
    {
        Console.WriteLine();
        Console.WriteLine("  picking several");
        Console.WriteLine();

        var tree = Tree();
        var a = tree[0];
        var b = tree[1];
        var c = tree[2];

        Check("the tree shows open levels' rooms and not closed ones'",
            string.Join(",", VENTWindow.Picks.Visible(tree).Select(r =>
                r is VENTWindow.LevelRow ? ((VENTWindow.LevelRow)r).Name
                                         : ((VENTWindow.RoomRow)r).Number))
            == "A,R1,R2,R3,R4,B,C");

        // ---- a plain click ----------------------------------------------
        object anchor = VENTWindow.Picks.Click(tree, null, a.Rooms[1], false, false);

        Check("a click picks that one row",
            Picked(tree) == "R2");

        anchor = VENTWindow.Picks.Click(tree, anchor, a.Rooms[3], false, false);

        Check("another click picks that one instead",
            Picked(tree) == "R4" && anchor == a.Rooms[3]);

        // ---- Ctrl ---------------------------------------------------------
        anchor = VENTWindow.Picks.Click(tree, anchor, b, true, false);

        Check("Ctrl+click adds a row, and a level can be picked beside a room",
            Picked(tree) == "R4,B");

        anchor = VENTWindow.Picks.Click(tree, anchor, a.Rooms[3], true, false);

        Check("Ctrl+click on a picked row takes it away again",
            Picked(tree) == "B");

        // ---- Shift ----------------------------------------------------------
        anchor = VENTWindow.Picks.Click(tree, null, a.Rooms[0], false, false);
        anchor = VENTWindow.Picks.Click(tree, anchor, a.Rooms[2], false, true);

        Check("Shift+click picks everything between, both ends included",
            Picked(tree) == "R1,R2,R3");

        Check("and keeps the row the range counts from",
            anchor == a.Rooms[0]);

        VENTWindow.Picks.Click(tree, anchor, a, false, true);

        Check("a range can run upwards, onto the level above",
            Picked(tree) == "A,R1");

        VENTWindow.Picks.Click(tree, anchor, c, false, true);

        Check("a range passes over a closed level's rooms, which are not shown",
            Picked(tree) == "R1,R2,R3,R4,B,C");

        anchor = VENTWindow.Picks.Click(tree, null, b, false, false);
        VENTWindow.Picks.Click(tree, anchor, c, true, false);
        VENTWindow.Picks.Click(tree, anchor, a.Rooms[3], true, true);

        Check("Ctrl+Shift adds a range to what is already picked",
            Picked(tree) == "R4,B,C");

        // The anchor's level is closed, so it has gone out of sight
        anchor = VENTWindow.Picks.Click(tree, null, a.Rooms[1], false, false);
        a.IsExpanded = false;
        VENTWindow.Picks.Click(tree, anchor, b, false, true);

        Check("a range from a row that has been folded away starts at the click",
            Picked(tree) == "B");

        a.IsExpanded = true;

        // ---- Ctrl+A ----------------------------------------------------------
        VENTWindow.Picks.PickShown(tree);

        Check("Ctrl+A picks every row showing, and not a closed level's rooms",
            Picked(tree) == "A,R1,R2,R3,R4,B,C");

        VENTWindow.Picks.Clear(tree);

        Check("and clearing lets go of all of them",
            VENTWindow.Picks.Count(tree) == 0);

        // ---- what the picks stand for -----------------------------------------
        anchor = VENTWindow.Picks.Click(tree, null, a.Rooms[2], false, false);
        VENTWindow.Picks.Click(tree, anchor, b, true, false);

        Check("a picked level stands for all its rooms, even closed",
            string.Join(",", VENTWindow.Picks.Rooms(tree).Select(r => r.Number))
            == "R3,R5,R6");

        VENTWindow.Picks.Click(tree, anchor, a, true, false);

        Check("a room picked on a picked level is counted once",
            string.Join(",", VENTWindow.Picks.Rooms(tree).Select(r => r.Number))
            == "R1,R2,R3,R4,R5,R6");

        // ---- ticking ------------------------------------------------------------
        tree = Tree();
        a = tree[0];
        b = tree[1];
        c = tree[2];

        anchor = VENTWindow.Picks.Click(tree, null, a.Rooms[0], false, false);
        VENTWindow.Picks.Click(tree, anchor, a.Rooms[2], true, false);
        VENTWindow.Picks.Click(tree, anchor, b, true, false);

        VENTWindow.Picks.Tick(tree, false);

        Check("unticking the picks unticks every picked room",
            !a.Rooms[0].IsOn && !a.Rooms[2].IsOn);

        Check("and leaves the rooms nobody picked alone",
            a.Rooms[1].IsOn && a.Rooms[3].IsOn);

        Check("and a picked level takes all its rooms with it",
            b.Checked == false && b.Rooms.All(r => !r.IsOn));

        Check("the partly-picked level shows the third state",
            a.Checked == null);

        // A level AND one of its own rooms picked: the room must end up
        // as asked, whichever order they are done in
        tree = Tree();
        a = tree[0];

        a.Rooms[1].IsOn = false;
        anchor = VENTWindow.Picks.Click(tree, null, a, false, false);
        VENTWindow.Picks.Click(tree, anchor, a.Rooms[1], true, false);

        VENTWindow.Picks.Tick(tree, true);

        Check("ticking a picked level and one of its picked rooms ticks them all",
            a.Checked == true && a.Rooms.All(r => r.IsOn));

        Check("picking alone ticks nothing",
            tree[1].Rooms.All(r => r.IsOn) && tree[2].Checked == false);
    }
}
