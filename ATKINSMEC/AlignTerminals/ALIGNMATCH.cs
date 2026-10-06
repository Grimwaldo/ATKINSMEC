using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // WHICH LINKED TERMINAL EACH OF THIS MODEL'S TERMINALS GOES TO - the
    // Revit-free half of ALIGNTERMINALS.Plan, so the matching is tested
    // as it is rather than as a copy (tests\aligntest). Moved out of
    // ALIGNTERMINALS unchanged on 2026-10-06, after the copy that tested
    // it was lost to Windows cleaning %TEMP%.
    //
    //   - PLAN DISTANCE ONLY. The elevation is the thing being put right,
    //     so counting it would let a badly levelled terminal match the
    //     wrong ceiling.
    //   - NEAREST FIRST, and each linked terminal CLAIMED ONCE - otherwise
    //     two terminals can be dragged onto one diffuser and stack.
    //   - BUCKETED by the search radius, so each terminal looks at the
    //     nine cells round it rather than every candidate in the job.
    //
    // Coordinates and the distance are in one unit (Revit's feet in the
    // add-in); nothing here cares which.
    public static class ALIGNMATCH
    {
        public class Point
        {
            public long Id;              // this model's terminals; unused on targets
            public double X, Y, Z;
            public string Family = string.Empty;
            public string Type = string.Empty;
        }

        public class Pair
        {
            public long TerminalId;
            public int Target;           // index into the targets given
            public double Distance;      // in plan
        }

        public static List<Pair> Match(
            IList<Point> terminals,
            IList<Point> targets,
            double maxDistance,
            bool sameFamily,
            bool sameType)
        {
            var matched = new List<Pair>();

            if (terminals == null || targets == null)
                return matched;

            Dictionary<long, List<int>> grid = Grid(targets, maxDistance);

            var pairs = new List<Pair>();

            foreach (Point terminal in terminals)
            {
                if (terminal == null)
                    continue;

                foreach (int t in Near(grid, terminal.X, terminal.Y, maxDistance))
                {
                    Point target = targets[t];

                    if (!Allowed(terminal, target, sameFamily, sameType))
                        continue;

                    double distance = PlanDistance(terminal, target);

                    if (distance > maxDistance)
                        continue;

                    pairs.Add(new Pair { TerminalId = terminal.Id, Target = t, Distance = distance });
                }
            }

            // Nearest first. A TIE is settled by terminal, then target,
            // so two runs on one model cannot disagree - the plain sort
            // this replaced left ties in whatever order it liked.
            pairs.Sort((a, b) =>
            {
                int by = a.Distance.CompareTo(b.Distance);

                if (by != 0)
                    return by;

                by = a.TerminalId.CompareTo(b.TerminalId);

                return by != 0 ? by : a.Target.CompareTo(b.Target);
            });

            var used = new HashSet<long>();
            var taken = new HashSet<int>();

            foreach (Pair pair in pairs)
            {
                if (used.Contains(pair.TerminalId) || taken.Contains(pair.Target))
                    continue;

                used.Add(pair.TerminalId);
                taken.Add(pair.Target);

                matched.Add(pair);
            }

            return matched;
        }

        // Two signed 32 bit cell indices packed into one key. NEGATIVE
        // coordinates are where this would bite - a model's origin is
        // often in the middle of the building.
        public static long CellKey(double x, double y, double cell)
        {
            if (cell <= 0)
                cell = 1;

            return Key((long)Math.Floor(x / cell), (long)Math.Floor(y / cell));
        }

        public static long Key(long cx, long cy)
        {
            return (cx << 32) ^ (cy & 0xFFFFFFFFL);
        }

        public static Dictionary<long, List<int>> Grid(IList<Point> targets, double cell)
        {
            var grid = new Dictionary<long, List<int>>();

            if (cell <= 0)
                cell = 1;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null)
                    continue;

                long key = CellKey(targets[i].X, targets[i].Y, cell);

                List<int> bucket;

                if (!grid.TryGetValue(key, out bucket))
                {
                    bucket = new List<int>();
                    grid[key] = bucket;
                }

                bucket.Add(i);
            }

            return grid;
        }

        // The targets in the nine cells round a point - every one within a
        // cell's width of it, and some further, which the caller measures
        public static IEnumerable<int> Near(
            Dictionary<long, List<int>> grid, double x, double y, double cell)
        {
            if (cell <= 0)
                cell = 1;

            long cx = (long)Math.Floor(x / cell);
            long cy = (long)Math.Floor(y / cell);

            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    List<int> bucket;

                    if (!grid.TryGetValue(Key(cx + dx, cy + dy), out bucket))
                        continue;

                    foreach (int i in bucket)
                        yield return i;
                }
            }
        }

        // Restricted to the same family, or the same family and type
        public static bool Allowed(Point terminal, Point target, bool sameFamily, bool sameType)
        {
            if (sameType)
                return Same(terminal.Family, target.Family) && Same(terminal.Type, target.Type);

            if (sameFamily)
                return Same(terminal.Family, target.Family);

            return true;
        }

        public static double PlanDistance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
