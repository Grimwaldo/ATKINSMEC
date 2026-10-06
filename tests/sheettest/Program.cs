using ATKINSMEC;
using System;
using System.Collections.Generic;

// Runs the real FCUSHEET against the real spreadsheet. No Revit needed -
// that is the whole reason the reader has none in it.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static int Main(string[] args)
    {
        string path = args.Length > 0
            ? args[0]
            : RepoPath.Find(@"tests\data\FCU_Schedule.xlsx");

        Console.WriteLine("FCUSHEET harness");
        Console.WriteLine(path);
        Console.WriteLine();

        List<string> sheets = FCUSHEET.WorksheetNames(path);

        Console.WriteLine("worksheets: " + string.Join(", ", sheets));
        Console.WriteLine();

        List<FCUSHEET.Unit> units = FCUSHEET.Read(path, sheets[0]);

        foreach (FCUSHEET.Unit unit in units)
        {
            Console.WriteLine(
                "  row " + unit.Row.ToString().PadLeft(2) + "   " +
                unit.Number.PadRight(10) +
                unit.FlowLitresPerSecond.ToString("N0").PadLeft(6) + " L/s   " +
                (unit.FreeDischarge ? "free discharge  " : "ducted          ") +
                unit.Model);
        }

        Console.WriteLine();

        // What the schedule actually says, read off the image and the
        // file by eye
        Check("18 units read", units.Count == 18);

        Check("the header and notes rows are not units",
            units.TrueForAll(u => u.Row >= 4 && u.Row <= 21));

        Check("FCU -02 is 160 L/s",
            units[0].Number == "FCU -02"
            && Math.Abs(units[0].FlowLitresPerSecond - 160) < 0.001);

        Check("FCU -14 is 1107 L/s",
            units.Find(u => u.Number == "FCU -14") != null
            && Math.Abs(
                units.Find(u => u.Number == "FCU -14").FlowLitresPerSecond
                - 1107) < 0.001);

        Check("FCU -E is 329 L/s and the last row",
            units[units.Count - 1].Number == "FCU -E"
            && Math.Abs(
                units[units.Count - 1].FlowLitresPerSecond - 329) < 0.001);

        int ducted = units.FindAll(u => !u.FreeDischarge).Count;
        int free = units.FindAll(u => u.FreeDischarge).Count;

        Check("13 ducted, 5 free discharge  (got " + ducted + " and " +
              free + ")", ducted == 13 && free == 5);

        Check("the SFP column was not mistaken for the flow",
            units.TrueForAll(u => u.FlowLitresPerSecond > 1));

        Check("models came through",
            units[0].Model == "TFC M-045 4D CSD5");

        double total = 0;

        foreach (FCUSHEET.Unit unit in units)
            total += unit.FlowLitresPerSecond;

        Console.WriteLine();
        Console.WriteLine("  total " + total.ToString("N0") + " L/s");

        // Names are matched loosely on purpose
        Check("\"FCU-02\" matches \"FCU -02\"",
            FCUSHEET.Key("FCU-02") == FCUSHEET.Key("FCU -02"));

        Check("\"fcu_02\" matches \"FCU -02\"",
            FCUSHEET.Key("fcu_02") == FCUSHEET.Key("FCU -02"));

        Check("\"FCU-2\" does NOT match \"FCU -02\"",
            FCUSHEET.Key("FCU-2") != FCUSHEET.Key("FCU -02"));

        Check("an empty name keys to nothing",
            FCUSHEET.Key(null) == "" && FCUSHEET.Key("  ") == "");

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
