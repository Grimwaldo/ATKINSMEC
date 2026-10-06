using System;
using System.Collections.Generic;

// Mirrors the spatial index and the meeting-ends sweep out of
// QADUCTS.cs, with no Revit anywhere near it. Keep the two in step.
//
// The thing under test is the cell key: three signed cell indices are
// packed into one long, and a model whose coordinates run negative is
// exactly where that goes wrong silently. ALIGNTERMINALS had that bug.
internal static class Program
{
    private static int _failed;

    private struct V
    {
        public double X, Y, Z;

        public V(double x, double y, double z) { X = x; Y = y; Z = z; }

        public double To(V o)
        {
            double dx = X - o.X, dy = Y - o.Y, dz = Z - o.Z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    private class Seg
    {
        public int Id;
        public V A, B;
    }

    // Distance from a point to a segment - what Curve.Distance does
    private static double Distance(Seg s, V p)
    {
        double vx = s.B.X - s.A.X, vy = s.B.Y - s.A.Y, vz = s.B.Z - s.A.Z;
        double wx = p.X - s.A.X, wy = p.Y - s.A.Y, wz = p.Z - s.A.Z;

        double vv = vx * vx + vy * vy + vz * vz;

        if (vv < 1e-12)
            return p.To(s.A);

        double t = (wx * vx + wy * vy + wz * vz) / vv;

        if (t < 0) t = 0;
        if (t > 1) t = 1;

        var on = new V(s.A.X + vx * t, s.A.Y + vy * t, s.A.Z + vz * t);

        return p.To(on);
    }

    // ==================================================
    // The index, mirrored
    // ==================================================

    private class Grid
    {
        private const long Offset = 1L << 20;
        private const long Span = 1L << 21;

        private readonly double _cell;

        private readonly Dictionary<long, List<Seg>> _cells =
            new Dictionary<long, List<Seg>>();

        private readonly List<Seg> _oversized = new List<Seg>();

        public int Buckets { get { return _cells.Count; } }
        public int Oversized { get { return _oversized.Count; } }

        public Grid(List<Seg> segments, double reach, int cap = 4096)
        {
            _cell = Math.Max(reach * 2.0, 10.0);

            foreach (Seg s in segments)
            {
                double length = s.A.To(s.B);

                int steps = (int)Math.Ceiling(length / _cell) + 1;

                if (steps > cap)
                {
                    _oversized.Add(s);
                    continue;
                }

                var seen = new HashSet<long>();

                for (int i = 0; i <= steps; i++)
                {
                    double t = steps == 0 ? 0 : (double)i / steps;

                    var at = new V(
                        s.A.X + (s.B.X - s.A.X) * t,
                        s.A.Y + (s.B.Y - s.A.Y) * t,
                        s.A.Z + (s.B.Z - s.A.Z) * t);

                    long key = Key(at);

                    if (!seen.Add(key))
                        continue;

                    List<Seg> bucket;

                    if (!_cells.TryGetValue(key, out bucket))
                    {
                        bucket = new List<Seg>();
                        _cells[key] = bucket;
                    }

                    bucket.Add(s);
                }
            }
        }

        private long Key(V p)
        {
            return Key(Cell(p.X), Cell(p.Y), Cell(p.Z));
        }

        private long Cell(double v)
        {
            return (long)Math.Floor(v / _cell);
        }

        public static long Key(long x, long y, long z)
        {
            long px = Clamp(x + Offset);
            long py = Clamp(y + Offset);
            long pz = Clamp(z + Offset);

            return (px << 42) | (py << 21) | pz;
        }

        private static long Clamp(long v)
        {
            if (v < 0) return 0;

            return v >= Span ? Span - 1 : v;
        }

        public Seg Nearest(V p, double reach, out double best)
        {
            Seg found = null;

            best = double.MaxValue;

            long cx = Cell(p.X), cy = Cell(p.Y), cz = Cell(p.Z);

            var done = new HashSet<int>();

            for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
            for (long dz = -1; dz <= 1; dz++)
            {
                List<Seg> bucket;

                if (!_cells.TryGetValue(
                        Key(cx + dx, cy + dy, cz + dz), out bucket))
                    continue;

                foreach (Seg s in bucket)
                {
                    if (!done.Add(s.Id))
                        continue;

                    double d = Distance(s, p);

                    if (d <= reach && d < best)
                    {
                        best = d;
                        found = s;
                    }
                }
            }

            foreach (Seg s in _oversized)
            {
                if (!done.Add(s.Id))
                    continue;

                double d = Distance(s, p);

                if (d <= reach && d < best)
                {
                    best = d;
                    found = s;
                }
            }

            return found;
        }
    }

    private static Seg Brute(
        List<Seg> segments, V p, double reach, out double best)
    {
        Seg found = null;

        best = double.MaxValue;

        foreach (Seg s in segments)
        {
            double d = Distance(s, p);

            if (d <= reach && d < best)
            {
                best = d;
                found = s;
            }
        }

        return found;
    }

    // ==================================================
    // Ends that meet, mirrored
    // ==================================================

    private class End
    {
        public int Owner;
        public V P;
        public V Facing;
        public End Meets;
    }

    private static void Pair(List<End> ends, double tolerance)
    {
        ends.Sort((a, b) => a.P.X.CompareTo(b.P.X));

        for (int i = 0; i < ends.Count; i++)
        {
            End one = ends[i];

            if (one.Meets != null)
                continue;

            for (int j = i + 1; j < ends.Count; j++)
            {
                End two = ends[j];

                if (two.P.X - one.P.X > tolerance)
                    break;

                if (two.Meets != null || two.Owner == one.Owner)
                    continue;

                if (one.P.To(two.P) > tolerance)
                    continue;

                if (Dot(one.Facing, two.Facing) > -0.5)
                    continue;

                one.Meets = two;
                two.Meets = one;

                break;
            }
        }
    }

    private static double Dot(V a, V b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    // ==================================================

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static int Main()
    {
        Console.WriteLine("Duct index harness");
        Console.WriteLine();

        // 1. the packing itself
        var seen = new Dictionary<long, string>();
        bool unique = true;

        for (long x = -40; x <= 40; x++)
        for (long y = -40; y <= 40; y++)
        for (long z = -8; z <= 8; z++)
        {
            long key = Grid.Key(x, y, z);
            string name = x + "," + y + "," + z;

            string already;

            if (seen.TryGetValue(key, out already))
            {
                Console.WriteLine("        " + name + " collides with " + already);
                unique = false;
            }
            else
            {
                seen[key] = name;
            }
        }

        Check("cell keys are unique across negative and positive indices",
            unique);

        // 2. and 3. the search, in four regions
        var random = new Random(20260904);

        string[] names = { "all positive", "all negative", "straddling zero",
                           "far from the origin" };

        double[][] regions =
        {
            new[] { 0.0, 400.0 },
            new[] { -400.0, 0.0 },
            new[] { -200.0, 200.0 },
            new[] { -90000.0, -89600.0 }
        };

        double reach = 5.0;

        for (int r = 0; r < regions.Length; r++)
        {
            var segments = new List<Seg>();

            for (int i = 0; i < 400; i++)
            {
                var a = new V(
                    Span(random, regions[r]),
                    Span(random, regions[r]),
                    Span(random, regions[r]) / 20.0);

                var b = new V(
                    a.X + random.NextDouble() * 60 - 30,
                    a.Y + random.NextDouble() * 60 - 30,
                    a.Z + random.NextDouble() * 4 - 2);

                segments.Add(new Seg { Id = i, A = a, B = b });
            }

            var grid = new Grid(segments, reach);

            int agreed = 0;
            int hits = 0;
            bool same = true;

            for (int q = 0; q < 4000; q++)
            {
                var p = new V(
                    Span(random, regions[r]),
                    Span(random, regions[r]),
                    Span(random, regions[r]) / 20.0);

                double fast;
                double slow;

                Seg one = grid.Nearest(p, reach, out fast);
                Seg two = Brute(segments, p, reach, out slow);

                bool ok = (one == null) == (two == null)
                          && (one == null || Math.Abs(fast - slow) < 1e-9);

                if (ok)
                    agreed++;
                else
                    same = false;

                if (two != null)
                    hits++;
            }

            Check(names[r] + ": grid agrees with brute force on " +
                  agreed + "/4000 (" + hits + " within reach)", same);
        }

        // 4. a duct too long to bucket is still found
        var longRun = new List<Seg>
        {
            new Seg
            {
                Id = 0,
                A = new V(-500000, 0, 0),
                B = new V(500000, 0, 0)
            }
        };

        var capped = new Grid(longRun, reach, 16);

        double where;

        Check("an over-long duct falls back to always being measured",
            capped.Oversized == 1
            && capped.Nearest(new V(1234, 2, 0), reach, out where) != null);

        // 5. the meeting sweep
        bool sweep = true;

        for (int round = 0; round < 200; round++)
        {
            var ends = new List<End>();

            for (int i = 0; i < 60; i++)
            {
                var p = new V(
                    random.NextDouble() * 200 - 100,
                    random.NextDouble() * 200 - 100,
                    random.NextDouble() * 20 - 10);

                var f = new V(
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1);

                double len = Math.Sqrt(Dot(f, f));

                if (len < 1e-9)
                    len = 1;

                f = new V(f.X / len, f.Y / len, f.Z / len);

                ends.Add(new End { Owner = i, P = p, Facing = f });

                // Half of them get a partner sitting on the same spot,
                // facing back the other way
                if (random.NextDouble() < 0.5)
                {
                    ends.Add(new End
                    {
                        Owner = 1000 + i,
                        P = new V(p.X + 0.01, p.Y, p.Z),
                        Facing = new V(-f.X, -f.Y, -f.Z)
                    });
                }
            }

            Pair(ends, 0.1);

            foreach (End e in ends)
            {
                if (e.Meets == null)
                    continue;

                // A pair must be mutual, close, and facing each other
                if (e.Meets.Meets != e
                    || e.P.To(e.Meets.P) > 0.1
                    || Dot(e.Facing, e.Meets.Facing) > -0.5
                    || e.Owner == e.Meets.Owner)
                    sweep = false;
            }
        }

        Check("every pair the sweep makes is mutual, close and opposed",
            sweep);

        // 6. the sweep must not miss a pair it should have found
        var missed = new List<End>
        {
            new End { Owner = 1, P = new V(-50.0, 3, 1),
                      Facing = new V(1, 0, 0) },
            new End { Owner = 2, P = new V(-50.02, 3, 1),
                      Facing = new V(-1, 0, 0) }
        };

        Pair(missed, 0.1);

        Check("a pair at negative coordinates is still found",
            missed[0].Meets != null);

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    private static double Span(Random random, double[] region)
    {
        return region[0] + random.NextDouble() * (region[1] - region[0]);
    }
}
