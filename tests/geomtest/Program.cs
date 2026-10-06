using ATKINSMEC;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

// SPRINKLERS' layout: where the heads go in a room. The REAL
// SPRINKLERLAYOUT and SPRINKLERRULES are compiled in, against stand-ins
// for the few Revit types they name (RevitStub.cs).
//
// Rewritten 2026-10-06. What was here was a copy of the layout from
// 2026-08-31 that printed one head's position and asserted nothing.
//
// Everything is in feet, as Revit holds it; M() turns metres into feet.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static double M(double metres)
    {
        return metres / 0.3048;
    }

    private static List<UV> Ring(params double[] metres)
    {
        var ring = new List<UV>();

        for (int i = 0; i + 1 < metres.Length; i += 2)
            ring.Add(new UV(M(metres[i]), M(metres[i + 1])));

        return ring;
    }

    private static SPRINKLERLAYOUT.RoomShape Shape(List<UV> outer, params List<UV>[] holes)
    {
        return new SPRINKLERLAYOUT.RoomShape { Outer = outer, Holes = holes.ToList() };
    }

    private static List<UV> Moved(List<UV> ring, double dxMetres, double dyMetres)
    {
        return ring.Select(p => new UV(p.U + M(dxMetres), p.V + M(dyMetres))).ToList();
    }

    private static List<UV> Turned(List<UV> ring, double degrees)
    {
        double a = degrees * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);

        return ring.Select(p => new UV(p.U * c - p.V * s, p.U * s + p.V * c)).ToList();
    }

    // ---- the test's OWN geometry, independent of the layout's ----------

    private static bool Inside(List<UV> ring, double x, double y)
    {
        bool inside = false;

        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            if ((ring[i].V > y) != (ring[j].V > y) &&
                x < (ring[j].U - ring[i].U) * (y - ring[i].V) / (ring[j].V - ring[i].V) + ring[i].U)
                inside = !inside;
        }

        return inside;
    }

    private static double ToEdges(List<UV> ring, double x, double y)
    {
        double best = double.MaxValue;

        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            double ax = ring[j].U, ay = ring[j].V, bx = ring[i].U, by = ring[i].V;
            double dx = bx - ax, dy = by - ay;
            double t = Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
            double px = ax + t * dx - x, py = ay + t * dy - y;

            best = Math.Min(best, Math.Sqrt(px * px + py * py));
        }

        return best;
    }

    // Every head inside the room, out of every hole, and at least the
    // wall clearance off every wall - the outline's and the holes'
    private static bool Sound(SPRINKLERLAYOUT.RoomShape shape, SPRINKLERLAYOUT.Result result)
    {
        double clearance = M(SPRINKLERRULES.WallClearanceM) - 1e-6;

        foreach (XYZ p in result.Points)
        {
            if (!Inside(shape.Outer, p.X, p.Y) || ToEdges(shape.Outer, p.X, p.Y) < clearance)
                return false;

            foreach (List<UV> hole in shape.Holes)
            {
                if (Inside(hole, p.X, p.Y) || ToEdges(hole, p.X, p.Y) < clearance)
                    return false;
            }
        }

        return true;
    }

    private static int Main()
    {
        Console.WriteLine("SPRINKLERS layout harness");
        Console.WriteLine();

        SPRINKLERRULES.HazardRule light = SPRINKLERRULES.RuleFor(SPRINKLERRULES.Hazard.Light);
        SPRINKLERRULES.HazardRule extra = SPRINKLERRULES.RuleFor(SPRINKLERRULES.Hazard.ExtraI);

        SPRINKLERRULES.WallClearanceMm = 150;

        // ---- a plain 10 x 6 m office ------------------------------------
        List<UV> office = Ring(0, 0, 10, 0, 10, 6, 0, 6);

        SPRINKLERLAYOUT.Result r = SPRINKLERLAYOUT.Layout(Shape(office), light);

        // Light hazard: 4.6 m and 20.9 m2 at most. 10 m needs 3 across,
        // 6 m needs 2 - 3.33 x 3.0 m = 10 m2 a head, inside both limits.
        Check("10 x 6 m at light hazard: 6 heads, 3 x 2", r.Points.Count == 6);

        Check("spacing within the hazard's 4.6 m both ways",
            r.SpacingXFt <= M(light.MaxSpacingM) + 1e-9 && r.SpacingYFt <= M(light.MaxSpacingM) + 1e-9);

        Check("area per head within the hazard's 20.9 m2",
            r.SpacingXFt * r.SpacingYFt <= M(1) * M(1) * light.MaxCoverageM2 + 1e-6);

        Check("every head inside, and at least the wall clearance off every wall",
            Sound(Shape(office), r));

        Check("the whole room is within reach", r.Coverage >= 0.999);

        Check("the free area is the room's, 60 m2", Math.Abs(r.FreeAreaM2 - 60) < 0.01);

        // ---- a stricter hazard needs more heads --------------------------
        SPRINKLERLAYOUT.Result rx = SPRINKLERLAYOUT.Layout(Shape(office), extra);

        // Extra I: 3.7 m and 9.3 m2. 3 x 2 gives 10 m2 a head - too much -
        // so the coarser way is split again: 4 x 2, 2.5 x 3.0 = 7.5 m2.
        Check("the same room at extra hazard I: 8 heads, the coarser way split",
            rx.Points.Count == 8 && rx.SpacingXFt * rx.SpacingYFt <= M(1) * M(1) * extra.MaxCoverageM2 + 1e-6);

        Check("...and every head still sound", Sound(Shape(office), rx));

        // ---- the grid follows the room, not the page ----------------------
        // The same room turned 30 degrees and moved far into negative
        // coordinates - where model origins often put rooms - lays out the
        // same: the grid is oriented to the room's own walls
        List<UV> turned = Moved(Turned(office, 30), -812.5, -307.25);

        SPRINKLERLAYOUT.Result rt = SPRINKLERLAYOUT.Layout(Shape(turned), light);

        Check("turned 30 degrees and moved below zero: the same 6 heads",
            rt.Points.Count == 6);

        Check("...at the same spacing",
            Math.Abs(Math.Max(rt.SpacingXFt, rt.SpacingYFt) - Math.Max(r.SpacingXFt, r.SpacingYFt)) < 1e-6 &&
            Math.Abs(Math.Min(rt.SpacingXFt, rt.SpacingYFt) - Math.Min(r.SpacingXFt, r.SpacingYFt)) < 1e-6);

        Check("...all of them sound, and the room still fully reached",
            Sound(Shape(turned), rt) && rt.Coverage >= 0.999);

        // ---- never closer than the standard allows ------------------------
        SPRINKLERLAYOUT.Result corridor = SPRINKLERLAYOUT.Layout(Shape(Ring(0, 0, 20, 0, 20, 2.0, 0, 2.0)), light);

        Check("a 20 x 2 m corridor: one row, and no two heads closer than 1.8 m",
            corridor.Points.Count > 0 &&
            corridor.Points.All(a => corridor.Points.All(b => ReferenceEquals(a, b) ||
                Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)) >= M(SPRINKLERRULES.MinSpacingM) - 1e-6)));

        Check("...and every head sound", Sound(Shape(Ring(0, 0, 20, 0, 20, 2.0, 0, 2.0)), corridor));

        // ---- a column in the middle -----------------------------------
        // An 8 x 8 m room with a 0.6 m column at its centre: the middle
        // head would land in it, so it is moved off - never into a hole
        SPRINKLERLAYOUT.RoomShape pillared = Shape(
            Ring(0, 0, 8, 0, 8, 8, 0, 8),
            Ring(3.7, 3.7, 4.3, 3.7, 4.3, 4.3, 3.7, 4.3));

        SPRINKLERLAYOUT.Result rp = SPRINKLERLAYOUT.Layout(pillared, light);

        Check("no head inside the column, and none within the clearance of its faces",
            rp.Points.Count > 0 && Sound(pillared, rp));

        // The free area is SAMPLED on a grid (CoverageRatio), so it is
        // close rather than exact: within 1% of 63.64 m2
        Check("the column's area is not counted as room (" + rp.FreeAreaM2.ToString("0.00") + " m2)",
            Math.Abs(rp.FreeAreaM2 - (64 - 0.36)) < 0.64 && rp.FreeAreaM2 < 64 - 0.1);

        // ---- an L-shaped room ---------------------------------------------
        // The cells that only clip the inside corner are lost to the grid;
        // the gap filling has to put the cover back
        List<UV> ell = Ring(0, 0, 12, 0, 12, 4, 4, 4, 4, 10, 0, 10);

        SPRINKLERLAYOUT.Result rl = SPRINKLERLAYOUT.Layout(Shape(ell), light);

        Check("an L-shaped room: every head sound", rl.Points.Count > 0 && Sound(Shape(ell), rl));

        Check("...and nearly all of it within reach (" + (rl.Coverage * 100).ToString("0.0") + "%)",
            rl.Coverage >= 0.97);

        // ---- small and impossible ------------------------------------------
        SPRINKLERLAYOUT.Result small = SPRINKLERLAYOUT.Layout(Shape(Ring(0, 0, 1.2, 0, 1.2, 1.5, 0, 1.5)), light);

        Check("a 1.2 x 1.5 m store still gets its one head, sound",
            small.Points.Count == 1 && Sound(Shape(Ring(0, 0, 1.2, 0, 1.2, 1.5, 0, 1.5)), small));

        SPRINKLERLAYOUT.Result none = SPRINKLERLAYOUT.Layout(Shape(Ring(0, 0, 1, 0)), light);

        Check("a boundary of fewer than three points places nothing and says why",
            none.Points.Count == 0 && none.Note == "no usable room boundary");

        Check("no shape at all is the same, not an error",
            SPRINKLERLAYOUT.Layout(null, light).Points.Count == 0);

        // ---- the same answer every time --------------------------------------
        SPRINKLERLAYOUT.Result again = SPRINKLERLAYOUT.Layout(Shape(ell), light);

        Check("laying the same room out twice gives the same heads",
            again.Points.Count == rl.Points.Count &&
            again.Points.Zip(rl.Points, (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)).All(d => d < 1e-9));

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
