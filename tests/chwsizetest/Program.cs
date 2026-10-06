using ATKINSMEC;
using System;
using System.Collections.Generic;

// Two jobs.
//
// 1. The table that SHIPS with the add-in (CHWSIZESHEET.Builtin) must
//    still match the workbook it was taken from, row for row. The
//    constants are unreadable by eye; this is what keeps them honest.
//
// 2. The lookups must land on the right size at every boundary.
//
// The spreadsheet reader stays in the product only to serve (1). If the
// office revises the standard: update the workbook, run this, and it
// will name every row that no longer agrees.
internal static class Program
{
    private static int _failed;

    private static readonly string File =
        RepoPath.Find(@"tests\data\Chilled Water Pipe Sizing - ASHRAE 90.xlsx");

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : File;

        Console.WriteLine("CHW pipe sizing harness");
        Console.WriteLine();

        CHWSIZESHEET.Table built = CHWSIZESHEET.Builtin();

        Console.WriteLine("built in: " + built.Sizes.Count + " sizes");

        foreach (CHWSIZESHEET.Size size in built.Sizes)
        {
            Console.WriteLine(
                "   DN" + size.DN.ToString("N0").PadRight(5) +
                "  bore " + size.BoreMm.ToString("N2").PadLeft(7) + " mm" +
                "   max " + size.MaxLitresPerSecond.ToString("N4").PadLeft(12) +
                " L/s   at " + size.MaxSpeed.ToString("N3") + " m/s");
        }

        Console.WriteLine();

        // ==================================================
        // 1. Built in versus the workbook
        // ==================================================

        CHWSIZESHEET.Table sheet = null;

        try
        {
            List<string> names = CHWSIZESHEET.WorksheetNames(path);

            sheet = CHWSIZESHEET.Read(path, names[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  could not read the workbook: " + ex.Message);
            Console.WriteLine("  (the built-in table is what ships; this " +
                              "comparison is the audit trail)");
        }

        if (sheet != null)
        {
            Check("the built-in table has the same number of sizes as the "
                  + "workbook", built.Sizes.Count == sheet.Sizes.Count);

            bool same = built.Sizes.Count == sheet.Sizes.Count;

            for (int i = 0; same && i < built.Sizes.Count; i++)
            {
                CHWSIZESHEET.Size a = built.Sizes[i];
                CHWSIZESHEET.Size b = sheet.Sizes[i];

                bool row = Math.Abs(a.DN - b.DN) < 1e-9
                    && Math.Abs(a.MaxLitresPerSecond
                                - b.MaxLitresPerSecond) < 1e-9
                    && Math.Abs(a.BoreMm - b.BoreMm) < 1e-6
                    && Math.Abs(a.MaxSpeed - b.MaxSpeed) < 1e-9;

                if (!row)
                {
                    Console.WriteLine(
                        "        DN" + a.DN.ToString("N0") +
                        ": built in " + a.MaxLitresPerSecond.ToString("R") +
                        " / " + a.BoreMm.ToString("R") +
                        ", workbook " + b.MaxLitresPerSecond.ToString("R") +
                        " / " + b.BoreMm.ToString("R"));

                    same = false;
                }
            }

            Check("every built-in row still matches the workbook exactly",
                same);
        }

        Console.WriteLine();

        // ==================================================
        // 2. The lookups, against what actually ships
        // ==================================================

        Check("smallest is DN20 at 0.1898 L/s",
            built.Sizes[0].DN == 20
            && Math.Abs(built.Sizes[0].MaxLitresPerSecond - 0.1898) < 0.0001);

        Check("largest is DN600 at 626.9788 L/s",
            built.Largest.DN == 600
            && Math.Abs(built.Largest.MaxLitresPerSecond - 626.9788) < 0.0001);

        Check("0.10 L/s needs DN20", built.SmallestFor(0.10).DN == 20);

        Check("0.19 L/s is just over DN20's 0.1898, so DN25",
            built.SmallestFor(0.19).DN == 25);

        Check("2.00 L/s fits DN50  (limit 2.1415)",
            built.SmallestFor(2.00).DN == 50);

        Check("2.20 L/s needs DN65",
            built.SmallestFor(2.20).DN == 65);

        Check("12.00 L/s fits DN100  (limit 12.4016)",
            built.SmallestFor(12.00).DN == 100);

        Check("12.50 L/s needs DN150",
            built.SmallestFor(12.50).DN == 150);

        Check("700 L/s is past the end of the table",
            built.SmallestFor(700) == null);

        Check("49.9999 mm matches DN50 within 2 mm",
            built.Match(49.9999, 2) != null
            && built.Match(49.9999, 2).DN == 50);

        Check("a 125 mm pipe matches nothing in this table",
            built.Match(125, 2) == null);

        Check("the outer diameters are not in the table  (26.7 is DN25's "
              + "OD, and must not match)",
            built.Match(26.7, 0.5) == null);

        CHWSIZESHEET.Size dn50 = built.Match(50, 1);
        CHWSIZESHEET.Size dn100 = built.Match(100, 1);

        Check("DN50 to DN100 is three sizes up  (50, 65, 80, 100)",
            built.StepsBetween(dn50, dn100) == 3);

        Check("the same size is zero steps",
            built.StepsBetween(dn50, dn50) == 0);

        Check("every row is ascending and has a real limit",
            Ascending(built));

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    private static bool Ascending(CHWSIZESHEET.Table table)
    {
        for (int i = 1; i < table.Sizes.Count; i++)
        {
            if (table.Sizes[i].DN <= table.Sizes[i - 1].DN)
                return false;

            if (table.Sizes[i].MaxLitresPerSecond
                <= table.Sizes[i - 1].MaxLitresPerSecond)
                return false;
        }

        foreach (CHWSIZESHEET.Size size in table.Sizes)
        {
            if (size.DN <= 0 || size.MaxLitresPerSecond <= 0)
                return false;
        }

        return true;
    }
}
