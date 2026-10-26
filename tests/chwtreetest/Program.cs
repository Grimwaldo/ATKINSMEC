using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Linq;

// CHW VOLUME's model list as a tree, and its search (user, 2026-10-19):
// the open model, each link placement below it, each nested link below
// the placement it is in; a model placed again offered unticked; a
// search that shows what it finds with everything above it.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static CHWTREE.Placement P(long id, long parent, string label, string model) =>
        new CHWTREE.Placement { Id = id, Parent = parent, Label = label, Model = model };

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("CHW VOLUME - links as a tree, and the search");
        Console.WriteLine();

        const string Host = "5232535-ATR-TRA-ZZZ-3DM-HVC-000100";

        // Two towers from one architect's model, each with the interior
        // model nested in it; a structural model; a furniture model nested
        // in the interior one; a nested link whose parent is not loaded
        var placements = new List<CHWTREE.Placement>
        {
            P(6, 0, "STR-000300", "str"),
            P(3, 0, "ARC-000020 : 2 : Tower B", "arc20"),
            P(5, 3, "IND-001220 : TIER 36", "ind"),
            P(1, 0, "ARC-000010", "arc10"),
            P(2, 0, "ARC-000020 : 1 : Tower A", "arc20"),
            P(4, 2, "IND-001220 : TIER 35", "ind"),
            P(7, 4, "FURN-000500", "furn"),
            P(8, 99, "MEP-000400", "mep")
        };

        List<CHWTREE.Line> lines = CHWTREE.Order(placements, Host);

        string Say(CHWTREE.Line l) => new string(' ', l.Depth * 2) + l.Label + (l.Repeat ? " (again)" : "");

        Console.WriteLine(string.Join(Environment.NewLine, lines.Select(l => "        " + Say(l))));

        Check("each placement below the open model, each nested link below its placement, by name",
            lines.Select(l => l.Id).SequenceEqual(new long[] { 1, 2, 4, 7, 3, 5, 8, 6 }));

        Check("depths: a link 1, nested 2, nested in nested 3",
            lines.Single(l => l.Id == 2).Depth == 1 && lines.Single(l => l.Id == 4).Depth == 2 &&
            lines.Single(l => l.Id == 7).Depth == 3);

        Check("a nested link knows the placement it is in, by name",
            lines.Single(l => l.Id == 4).Parent == 2 && lines.Single(l => l.Id == 4).ParentName == "ARC-000020 : 1 : Tower A" &&
            lines.Single(l => l.Id == 2).ParentName == Host);

        Check("a model placed again is a repeat - the second tower, and its nested interior model - the first is not",
            lines.Single(l => l.Id == 3).Repeat && lines.Single(l => l.Id == 5).Repeat &&
            !lines.Single(l => l.Id == 2).Repeat && !lines.Single(l => l.Id == 4).Repeat &&
            lines.Count(l => l.Repeat) == 2);

        Check("a nested link whose parent is not loaded: under the open model, not lost",
            lines.Single(l => l.Id == 8).Depth == 1 && lines.Single(l => l.Id == 8).Parent == 0);

        List<CHWTREE.Line> loop = CHWTREE.Order(new[] { P(1, 2, "A", "a"), P(2, 1, "B", "b") }, Host);

        Check("parents in a loop: each listed once, nothing lost, no hang",
            loop.Count == 2 && loop.Select(l => l.Id).OrderBy(i => i).SequenceEqual(new long[] { 1, 2 }));

        Check("nothing placed: nothing listed", CHWTREE.Order(null, Host).Count == 0);

        // ---- which placement a nested link is in ----------------------
        var towers = new[]
        {
            new KeyValuePair<long, double[]>(2, new[] { 0.0, 0.0, 0.0 }),
            new KeyValuePair<long, double[]>(3, new[] { 120.0, 0.0, 0.0 })
        };

        Check("the parent placement nearest the nested link: Tower B's",
            CHWTREE.Nearest(towers, new[] { 118.0, 4.0, 10.0 }) == 3 && CHWTREE.Nearest(towers, new[] { 2.0, 0.0, 10.0 }) == 2);

        Check("no candidates: none", CHWTREE.Nearest(new KeyValuePair<long, double[]>[0], new[] { 0.0, 0.0, 0.0 }) == 0);

        // ---- the search -------------------------------------------------
        var rows = new List<CHWTREE.Row>
        {
            new CHWTREE.Row { Text = Host + "   (current model)", Host = true, Local = true }
        };

        rows.AddRange(lines.Select(l => new CHWTREE.Row { Text = l.Label, Id = l.Id, Parent = l.Parent, Local = true }));
        rows.Add(new CHWTREE.Row { Text = "ACC-ARC-000900   (ACC, not opened)" });

        List<string> Found(string query) =>
            CHWTREE.Shown(rows, query).OrderBy(i => i).Select(i => rows[i].Text).ToList();

        Check("nothing typed: every row", Found("").Count == rows.Count);

        Check("a nested link found shows with its placement and the open model: " + string.Join(" | ", Found("tier 36")),
            Found("tier 36").SequenceEqual(new[] { rows[0].Text, "ARC-000020 : 2 : Tower B", "IND-001220 : TIER 36" }));

        Check("three levels down: the furniture model, the interior model, the tower and the open model",
            Found("furn").Count == 4 && Found("furn").Contains("IND-001220 : TIER 35") &&
            Found("furn").Contains("ARC-000020 : 1 : Tower A") && !Found("furn").Contains("ARC-000020 : 2 : Tower B"));

        Check("every word, any order, any case", Found("TOWER arc-000020 b").Count == 2);

        Check("an ACC model found shows alone", Found("ACC-ARC").SequenceEqual(new[] { "ACC-ARC-000900   (ACC, not opened)" }));

        Check("nothing found: nothing shown", Found("zzzz").Count == 0);

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "all checks passed" : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
