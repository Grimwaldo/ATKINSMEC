using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Linq;

// ALIGN TERMINALS: which linked terminal each of this model's terminals
// is moved onto. The REAL ALIGNMATCH is compiled in.
//
// Rewritten 2026-10-06. The harness before it mirrored the matching and
// was lost when Windows cleaned %TEMP%; what it covered is on record in
// WORKPLAN.md - the grid against brute force on negative coordinates and
// the cell key's uniqueness - and is covered again here, with more.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static ALIGNMATCH.Point P(long id, double x, double y, double z = 0,
                                      string family = "Diffuser", string type = "600x600")
    {
        return new ALIGNMATCH.Point { Id = id, X = x, Y = y, Z = z, Family = family, Type = type };
    }

    private static int Main()
    {
        Console.WriteLine("ALIGN TERMINALS matching harness");
        Console.WriteLine();

        // ---- the cell key ------------------------------------------------
        // Two signed 32 bit indices packed into one long. Negative
        // coordinates are where packing bites: a model's origin is often in
        // the middle of the building.
        {
            bool roundTrips = true;
            long probes = 0;

            for (long cx = -2000; cx < 2000 && roundTrips; cx++)
            {
                for (long cy = -2000; cy < 2000; cy++)
                {
                    long key = ALIGNMATCH.Key(cx, cy);

                    // Unpacked: the high half is cx, the low half cy
                    if ((key >> 32) != cx || (int)(key & 0xFFFFFFFFL) != cy)
                    {
                        roundTrips = false;
                        break;
                    }

                    probes++;
                }
            }

            Check("every cell key across 4000 x 4000 cells, negatives included, unpacks to its own cell ("
                  + probes.ToString("N0") + ")", roundTrips && probes == 16000000);

            Check("the cells either side of zero are different keys",
                ALIGNMATCH.CellKey(-0.5, 0.5, 1) != ALIGNMATCH.CellKey(0.5, 0.5, 1) &&
                ALIGNMATCH.CellKey(0.5, -0.5, 1) != ALIGNMATCH.CellKey(0.5, 0.5, 1) &&
                ALIGNMATCH.CellKey(-0.5, -0.5, 1) != ALIGNMATCH.CellKey(0.5, 0.5, 1));
        }

        // ---- the grid finds what brute force finds -----------------------
        // In four regions - one straddling the origin, one all negative,
        // one far positive, one far negative in x only. Every target within
        // the reach of a probe must come back from the nine cells round it.
        {
            var random = new Random(20261006);

            var regions = new[]
            {
                new[] { -50.0, 50.0, -50.0, 50.0 },
                new[] { -900.0, -700.0, -650.0, -450.0 },
                new[] { 4000.0, 4200.0, 3000.0, 3200.0 },
                new[] { -6000.0, -5800.0, 100.0, 300.0 }
            };

            const double reach = 4.92;    // the add-in's default, 1500 mm in feet

            int probes = 0;
            int missed = 0;

            foreach (double[] r in regions)
            {
                var targets = new List<ALIGNMATCH.Point>();

                for (int i = 0; i < 1500; i++)
                    targets.Add(P(0, r[0] + random.NextDouble() * (r[1] - r[0]),
                                     r[2] + random.NextDouble() * (r[3] - r[2])));

                Dictionary<long, List<int>> grid = ALIGNMATCH.Grid(targets, reach);

                for (int k = 0; k < 1500; k++)
                {
                    var probe = P(k, r[0] + random.NextDouble() * (r[1] - r[0]),
                                     r[2] + random.NextDouble() * (r[3] - r[2]));

                    var fromGrid = new HashSet<int>(
                        ALIGNMATCH.Near(grid, probe.X, probe.Y, reach)
                                  .Where(t => ALIGNMATCH.PlanDistance(probe, targets[t]) <= reach));

                    var brute = new HashSet<int>(
                        Enumerable.Range(0, targets.Count)
                                  .Where(t => ALIGNMATCH.PlanDistance(probe, targets[t]) <= reach));

                    if (!fromGrid.SetEquals(brute))
                        missed++;

                    probes++;
                }
            }

            Check("the grid finds exactly what brute force finds, " + probes.ToString("N0") +
                  " probes in four regions, two of them negative", missed == 0 && probes == 6000);
        }

        // ---- plan distance only -----------------------------------------
        {
            var ours = new List<ALIGNMATCH.Point> { P(1, 0, 0, 0) };

            // Right overhead but 3 m up, against one 5 cm away at the same
            // height: the elevation is what is being put right, so it does
            // not count
            var theirs = new List<ALIGNMATCH.Point> { P(0, 0.05, 0, 0), P(0, 0.001, 0, 9.8) };

            List<ALIGNMATCH.Pair> got = ALIGNMATCH.Match(ours, theirs, 4.92, false, false);

            Check("matching is by PLAN distance - the one overhead wins, whatever its height",
                got.Count == 1 && got[0].Target == 1);
        }

        // ---- nearest first, each target claimed once ---------------------
        {
            // Two terminals near one diffuser: the nearer takes it, and the
            // other goes to its own next-nearest rather than stacking
            var ours = new List<ALIGNMATCH.Point> { P(1, 0, 0), P(2, 1.0, 0) };
            var theirs = new List<ALIGNMATCH.Point> { P(0, 0.8, 0), P(0, 3.0, 0) };

            List<ALIGNMATCH.Pair> got = ALIGNMATCH.Match(ours, theirs, 4.92, false, false);

            ALIGNMATCH.Pair one = got.FirstOrDefault(p => p.TerminalId == 1);
            ALIGNMATCH.Pair two = got.FirstOrDefault(p => p.TerminalId == 2);

            Check("the NEARER terminal takes the diffuser they both want",
                two != null && two.Target == 0);

            Check("...and the other goes to its next-nearest, not onto the same one",
                one != null && one.Target == 1);

            var crowd = new List<ALIGNMATCH.Point>();
            var spots = new List<ALIGNMATCH.Point>();
            var random = new Random(7);

            for (int i = 0; i < 400; i++)
            {
                crowd.Add(P(i, random.NextDouble() * 60 - 30, random.NextDouble() * 60 - 30));
                spots.Add(P(0, random.NextDouble() * 60 - 30, random.NextDouble() * 60 - 30));
            }

            List<ALIGNMATCH.Pair> all = ALIGNMATCH.Match(crowd, spots, 4.92, false, false);

            Check("across 400 terminals no target is claimed twice and no terminal moves twice",
                all.Select(p => p.Target).Distinct().Count() == all.Count &&
                all.Select(p => p.TerminalId).Distinct().Count() == all.Count);

            Check("...and every match is within the reach",
                all.All(p => p.Distance <= 4.92));
        }

        // ---- the reach ---------------------------------------------------
        {
            var ours = new List<ALIGNMATCH.Point> { P(1, 0, 0) };

            Check("a target beyond the reach is not matched",
                ALIGNMATCH.Match(ours, new List<ALIGNMATCH.Point> { P(0, 5.0, 0) }, 4.92, false, false).Count == 0);

            Check("one exactly at the reach is",
                ALIGNMATCH.Match(ours, new List<ALIGNMATCH.Point> { P(0, 3, 4) }, 5.0, false, false).Count == 1);
        }

        // ---- same family, same type --------------------------------------
        {
            var ours = new List<ALIGNMATCH.Point> { P(1, 0, 0, 0, "Diffuser", "600x600") };

            var theirs = new List<ALIGNMATCH.Point>
            {
                P(0, 0.1, 0, 0, "Grille", "600x600"),
                P(0, 0.5, 0, 0, "DIFFUSER", "300x300"),
                P(0, 0.9, 0, 0, "diffuser", "600X600")
            };

            Check("with no restriction the nearest of any family is taken",
                ALIGNMATCH.Match(ours, theirs, 4.92, false, false)[0].Target == 0);

            Check("same family: the nearest diffuser, whatever its type - names compared ignoring case",
                ALIGNMATCH.Match(ours, theirs, 4.92, true, false)[0].Target == 1);

            Check("same type: family AND type must agree",
                ALIGNMATCH.Match(ours, theirs, 4.92, false, true)[0].Target == 2);
        }

        // ---- the same answer every time -----------------------------------
        {
            // Two terminals exactly as far from one target: a tie, settled
            // by terminal id - however the lists arrive
            var a = P(5, -1, 0);
            var b = P(3, 1, 0);
            var target = P(0, 0, 0);

            List<ALIGNMATCH.Pair> first = ALIGNMATCH.Match(new[] { a, b }, new[] { target }, 4.92, false, false);
            List<ALIGNMATCH.Pair> second = ALIGNMATCH.Match(new[] { b, a }, new[] { target }, 4.92, false, false);

            Check("a tie goes to the lower terminal id, whichever order they come in",
                first.Count == 1 && second.Count == 1 &&
                first[0].TerminalId == 3 && second[0].TerminalId == 3);
        }

        // ---- nothing to do --------------------------------------------------
        Check("no terminals, no targets, or nulls - an empty answer, not an error",
            ALIGNMATCH.Match(new List<ALIGNMATCH.Point>(), new List<ALIGNMATCH.Point> { P(0, 0, 0) }, 4.92, false, false).Count == 0 &&
            ALIGNMATCH.Match(new List<ALIGNMATCH.Point> { P(1, 0, 0) }, new List<ALIGNMATCH.Point>(), 4.92, false, false).Count == 0 &&
            ALIGNMATCH.Match(null, null, 4.92, false, false).Count == 0);

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
