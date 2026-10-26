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

        // ---- a column ON a grid point (user-reported 2026-10-07) ----------
        // 9 x 16 m at light hazard is a 2 x 4 grid, 4.5 x 4.0 m: heads at
        // x 2.25 / 6.75, y 2 / 6 / 10 / 14. A 0.5 m column sits exactly on
        // (2.25, 10). Reported as one head of a room coming out "like
        // this one" - off its column of heads, against the wall.
        {
            var room = Ring(0, 0, 9, 0, 9, 16, 0, 16);
            var column = Ring(2.0, 9.75, 2.5, 9.75, 2.5, 10.25, 2.0, 10.25);

            SPRINKLERLAYOUT.RoomShape shape = Shape(room, column);
            SPRINKLERLAYOUT.Result rc = SPRINKLERLAYOUT.Layout(shape, light);

            foreach (XYZ p in rc.Points)
                Console.WriteLine("        head at (" + (p.X * 0.3048).ToString("0.00") + ", " + (p.Y * 0.3048).ToString("0.00") + ") m");

            XYZ nearest = rc.Points
                .OrderBy(p => Math.Pow(p.X - M(2.25), 2) + Math.Pow(p.Y - M(10), 2))
                .FirstOrDefault();

            double off = nearest == null
                ? double.MaxValue
                : Math.Sqrt(Math.Pow(nearest.X - M(2.25), 2) + Math.Pow(nearest.Y - M(10), 2)) * 0.3048;

            Check("a column on a grid point: the head is moved just off the column (" + off.ToString("0.00") + " m), not away to a wall",
                off <= 1.0);

            Check("...still 8 heads, all sound, the room fully reached",
                rc.Points.Count == 8 && Sound(shape, rc) && rc.Coverage >= 0.999);
        }

        // ---- a grid point squeezed between two columns -------------------
        // The same room; two columns 0.2 m apart either side of the grid
        // point (2.25, 10) - it is in neither, but nearer than the 150 mm
        // clearance to both, so walking it off one face walks it onto the
        // other
        {
            var room = Ring(0, 0, 9, 0, 9, 16, 0, 16);
            var west = Ring(1.65, 9.7, 2.15, 9.7, 2.15, 10.3, 1.65, 10.3);
            var east = Ring(2.35, 9.7, 2.85, 9.7, 2.85, 10.3, 2.35, 10.3);

            SPRINKLERLAYOUT.RoomShape shape = Shape(room, west, east);
            SPRINKLERLAYOUT.Result rs = SPRINKLERLAYOUT.Layout(shape, light);

            XYZ nearest = rs.Points
                .OrderBy(p => Math.Pow(p.X - M(2.25), 2) + Math.Pow(p.Y - M(10), 2))
                .First();

            double off = Math.Sqrt(Math.Pow(nearest.X - M(2.25), 2) + Math.Pow(nearest.Y - M(10), 2)) * 0.3048;

            Check("a grid point squeezed between two columns: moved clear of both, still near (" + off.ToString("0.00") + " m)",
                off <= 1.0 && rs.Dropped == 0);

            Check("...still 8 heads, all sound, the room fully reached",
                rs.Points.Count == 8 && Sound(shape, rs) && rs.Coverage >= 0.999);
        }

        // ---- a dodging head stays in line (user-reported 2026-10-07) --------
        // Same 9 x 16 m room, grid point (2.25, 10)
        {
            var room = Ring(0, 0, 9, 0, 9, 16, 0, 16);

            bool OnALine(XYZ p) =>
                Math.Abs(p.X - M(2.25)) < 1e-6 || Math.Abs(p.Y - M(10)) < 1e-6;

            XYZ Nearest(SPRINKLERLAYOUT.Result res) => res.Points
                .OrderBy(p => Math.Pow(p.X - M(2.25), 2) + Math.Pow(p.Y - M(10), 2))
                .First();

            double Off(XYZ p) =>
                Math.Sqrt(Math.Pow(p.X - M(2.25), 2) + Math.Pow(p.Y - M(10), 2)) * 0.3048;

            // A 0.6 m column turned 45 degrees - a diamond - on the grid
            // point. Its nearest clear point is DIAGONAL, off both lines,
            // about 0.48 m away; along the row or column it is about
            // 0.64 m, inside the allowance. Checked to FAIL with the
            // preference switched off. (A thin wall at 45 degrees did not
            // discriminate: the nearest sample happened to fall on a line.)
            double h = 0.3 * Math.Sqrt(2);

            var diamond = Ring(2.25 - h, 10, 2.25, 10 - h, 2.25 + h, 10, 2.25, 10 + h);

            SPRINKLERLAYOUT.RoomShape withDiamond = Shape(room, diamond);
            SPRINKLERLAYOUT.Result ra = SPRINKLERLAYOUT.Layout(withDiamond, light);
            XYZ moved = Nearest(ra);

            Check("a column turned 45 degrees on a grid point: the head stays on its row or column (" + Off(moved).ToString("0.00") + " m)",
                OnALine(moved) && Off(moved) <= 1.0 && Sound(withDiamond, ra) && ra.Points.Count == 8);

            // The column from before: straight off one face, so on its line
            var column = Ring(2.0, 9.75, 2.5, 9.75, 2.5, 10.25, 2.0, 10.25);

            SPRINKLERLAYOUT.Result rk = SPRINKLERLAYOUT.Layout(Shape(room, column), light);

            Check("the column on a grid point: the head stays on its line too",
                OnALine(Nearest(rk)));

            // A "+" of walls 3 m each way on the grid point: staying in
            // line would mean sliding past the end of an arm, 1.5 m+ away;
            // a corner of the cross clears in about 0.35 m. The allowance
            // keeps it close rather than in line.
            var across = Ring(0.75, 9.9, 3.75, 9.9, 3.75, 10.1, 0.75, 10.1);
            var along = Ring(2.15, 8.5, 2.35, 8.5, 2.35, 11.5, 2.15, 11.5);

            SPRINKLERLAYOUT.RoomShape withCross = Shape(room, across, along);
            SPRINKLERLAYOUT.Result rx2 = SPRINKLERLAYOUT.Layout(withCross, light);

            Check("a cross of walls on a grid point: close by in a corner of it (" + Off(Nearest(rx2)).ToString("0.00") + " m), not slid 1.5 m to stay in line",
                Off(Nearest(rx2)) <= 0.6 && Sound(withCross, rx2));
        }

        // ---- walls count only on the room's own storey --------------------
        // Floor at 0, ceiling at 2.7 m, the next level at 4 m
        {
            double floor = 0, ceiling = M(2.7);

            Check("a wall on this storey stands in the room",
                SPRINKLERLAYOUT.StandsInRoom(0, M(4), floor, ceiling));

            Check("a wall from the storey BELOW does not - even drawn 200 mm into this slab",
                !SPRINKLERLAYOUT.StandsInRoom(M(-4), 0, floor, ceiling) &&
                !SPRINKLERLAYOUT.StandsInRoom(M(-4), M(0.2), floor, ceiling));

            Check("a wall from the storey ABOVE does not - nor one that stops just above the ceiling",
                !SPRINKLERLAYOUT.StandsInRoom(M(4), M(8), floor, ceiling) &&
                !SPRINKLERLAYOUT.StandsInRoom(M(2.6), M(8), floor, ceiling));

            Check("a shaft wall through several storeys does",
                SPRINKLERLAYOUT.StandsInRoom(M(-8), M(16), floor, ceiling));

            Check("a 1 m upstand in the room does",
                SPRINKLERLAYOUT.StandsInRoom(0, M(1.0), floor, ceiling));

            Check("a room with no height to judge by keeps every wall, as before",
                SPRINKLERLAYOUT.StandsInRoom(M(4), M(8), floor, floor));
        }

        // ---- an irregular room: Live/Work Unit 205 -------------------------
        // Traced from the user's screenshot (2026-10-07), inner finish line,
        // scale estimated from the column grid: about 13.4 x 6.7 m, the
        // left wall chamfered at the top, the right wall stepped in above
        // and angled out below. Not to the millimetre - a room of this
        // SHAPE, not that room.
        {
            var unit = Ring(0, 0, 13.40, 0, 11.40, 2.76, 11.40, 6.65, 2.375, 6.65, 0, 2.565);

            SPRINKLERLAYOUT.RoomShape shape = Shape(unit);

            double Reach(SPRINKLERLAYOUT.Result res) =>
                0.5 * Math.Sqrt(res.SpacingXFt * res.SpacingXFt + res.SpacingYFt * res.SpacingYFt);

            // How many rows (or columns): values within 1 mm are one. Not
            // rounding to the millimetre - the rows here sit at 4.9875 m,
            // exactly on a rounding midpoint, and a hair of floating point
            // either side split one row into two
            int Distinct(IEnumerable<double> values)
            {
                List<double> sorted = values.Select(v => v * 0.3048).OrderBy(v => v).ToList();

                int groups = sorted.Count == 0 ? 0 : 1;

                for (int i = 1; i < sorted.Count; i++)
                {
                    if (sorted[i] - sorted[i - 1] > 0.001)
                        groups++;
                }

                return groups;
            }

            // Light hazard: a plain 3 x 2 grid, nothing needing to move
            SPRINKLERLAYOUT.Result lu = SPRINKLERLAYOUT.Layout(shape, light);

            Check("Live/Work 205 at light hazard: 6 heads in a tidy 3 x 2 grid, none moved",
                lu.Points.Count == 6 &&
                Distinct(lu.Points.Select(p => p.X)) == 3 && Distinct(lu.Points.Select(p => p.Y)) == 2 &&
                lu.Relocated == 0 && lu.WallAdjusted == 0 && lu.Dropped == 0 && lu.Added == 0);

            Check("...every head inside and clear of the angled walls, the room fully reached",
                Sound(shape, lu) && lu.Coverage >= 0.999);

            Check("...spacing and area per head within light hazard's limits",
                lu.SpacingXFt <= M(light.MaxSpacingM) + 1e-9 && lu.SpacingYFt <= M(light.MaxSpacingM) + 1e-9 &&
                lu.SpacingXFt * lu.SpacingYFt <= M(1) * M(1) * light.MaxCoverageM2 + 1e-6);

            // The sharp bottom-right corner is the tightest point: the
            // farthest floor from any head. It has to stay within reach.
            double corner = lu.Points.Min(p => Math.Sqrt(Math.Pow(p.X - M(13.40), 2) + Math.Pow(p.Y, 2)));

            Check("...the sharp bottom-right corner is still within reach (" + (corner * 0.3048).ToString("0.00") +
                  " of " + (Reach(lu) * 0.3048).ToString("0.00") + " m)",
                corner <= Reach(lu) + 1e-9);

            // Ordinary I: 4 x 2. The top-right grid point falls OUTSIDE the
            // room, where the right wall steps in, so that head is re-aimed
            // into the part of its cell that is inside - off its column,
            // which is outside the room there, but on its row
            SPRINKLERLAYOUT.Result ou = SPRINKLERLAYOUT.Layout(shape, SPRINKLERRULES.RuleFor(SPRINKLERRULES.Hazard.OrdinaryI));

            Check("Live/Work 205 at ordinary hazard I: 8 heads, the one whose grid point is outside the room re-aimed in",
                ou.Points.Count == 8 && ou.Relocated == 1 && ou.Dropped == 0);

            Check("...the re-aimed head keeps its row",
                Distinct(ou.Points.Select(p => p.Y)) == 2);

            Check("...every head sound, the room fully reached, area per head within 12.1 m2",
                Sound(shape, ou) && ou.Coverage >= 0.999 &&
                ou.SpacingXFt * ou.SpacingYFt <= M(1) * M(1) * 12.1 + 1e-6);
        }

        // ---- an L-shaped room ---------------------------------------------
        // The cells that only clip the inside corner are lost to the grid;
        // the gap filling has to put the cover back
        List<UV> ell = Ring(0, 0, 12, 0, 12, 4, 4, 4, 4, 10, 0, 10);

        SPRINKLERLAYOUT.Result rl = SPRINKLERLAYOUT.Layout(Shape(ell), light);

        Check("an L-shaped room: every head sound", rl.Points.Count > 0 && Sound(Shape(ell), rl));

        Check("...and nearly all of it within reach (" + (rl.Coverage * 100).ToString("0.0") + "%)",
            rl.Coverage >= 0.97);

        // ---- one head, in the middle of the room's main part ---------------
        // User, 2026-10-25: a store's one head well off its middle, placed by
        // SPRINKLERS - its outline takes in a nook, and the centre of the
        // whole outline is pulled towards it
        XYZ OnlyHead(SPRINKLERLAYOUT.Result r) => r.Points.Count > 0 ? r.Points[0] : new XYZ(double.NaN, double.NaN, 0);

        bool Near(XYZ p, double xMetres, double yMetres, double withinMetres) =>
            Math.Abs(p.X - M(xMetres)) <= M(withinMetres) && Math.Abs(p.Y - M(yMetres)) <= M(withinMetres);

        List<UV> plain = Ring(0, 0, 2.2, 0, 2.2, 2.8, 0, 2.8);
        SPRINKLERLAYOUT.Result centred = SPRINKLERLAYOUT.Layout(Shape(plain), light);

        Check("a plain store: its one head at its exact centre",
            centred.Points.Count == 1 && Near(OnlyHead(centred), 1.1, 1.4, 1e-6));

        // The store 2.2 x 2.8 m, a nook 1.2 x 1.2 m off its top left
        List<UV> nook = Ring(0, 0, 2.2, 0, 2.2, 2.8, -1.2, 2.8, -1.2, 1.6, 0, 1.6);
        SPRINKLERLAYOUT.Result inStore = SPRINKLERLAYOUT.Layout(Shape(nook), light);

        Check("a store with a nook: its head in the middle of the store, not pulled towards the nook - " +
              $"({OnlyHead(inStore).X * 0.3048:0.00}, {OnlyHead(inStore).Y * 0.3048:0.00}) m",
            Near(OnlyHead(inStore), 1.1, 1.4, 0.04) && Sound(Shape(nook), inStore));

        Check("...the nook beyond its reach gets a head of its own (" + inStore.Points.Count + " heads, " +
              (inStore.Coverage * 100).ToString("0.0") + "% within reach)",
            inStore.Points.Count == 2 && inStore.Added == 1 && inStore.Coverage >= 0.97 &&
            inStore.Points[1].X < 0);

        // A door recess 0.9 x 0.3 m under a 2.4 x 3 m store: one head covers
        // it all, and it stays in the middle of the store
        List<UV> recess = Ring(0, 0, 0.8, 0, 0.8, -0.3, 1.7, -0.3, 1.7, 0, 2.4, 0, 2.4, 3, 0, 3);
        SPRINKLERLAYOUT.Result withRecess = SPRINKLERLAYOUT.Layout(Shape(recess), light);

        Check("a store with a door recess: one head, in the middle of the store, not dragged to the door - " +
              $"({OnlyHead(withRecess).X * 0.3048:0.00}, {OnlyHead(withRecess).Y * 0.3048:0.00}) m",
            withRecess.Points.Count == 1 && Near(OnlyHead(withRecess), 1.2, 1.5, 0.04) && Sound(Shape(recess), withRecess));

        // A column in the middle of a one-head store: just off it, as before
        List<UV> post = Ring(1.05, 1.35, 1.35, 1.35, 1.35, 1.65, 1.05, 1.65);
        SPRINKLERLAYOUT.Result offPost = SPRINKLERLAYOUT.Layout(Shape(Ring(0, 0, 2.4, 0, 2.4, 3, 0, 3), post), light);

        // (The far corner it then leaves beyond reach gets a second head from
        // the gap filling - as it did before this change, checked both ways)
        Check("a column in the middle of a one-head store: its head just off it, sound, as before",
            offPost.Points.Count >= 1 && offPost.Relocated == 1 && Near(OnlyHead(offPost), 1.2, 1.5, 0.35) &&
            Sound(Shape(Ring(0, 0, 2.4, 0, 2.4, 3, 0, 3), post), offPost));

        // The same in a store turned 30 degrees: in its own middle still
        List<UV> turnedNook = Turned(nook, 30);
        SPRINKLERLAYOUT.Result turnedStore = SPRINKLERLAYOUT.Layout(Shape(turnedNook), light);
        UV middle = Turned(new List<UV> { new UV(M(1.1), M(1.4)) }, 30)[0];

        Check("...turned 30 degrees, in the store's own middle still",
            turnedStore.Points.Count >= 1 &&
            Math.Abs(OnlyHead(turnedStore).X - middle.U) <= M(0.05) && Math.Abs(OnlyHead(turnedStore).Y - middle.V) <= M(0.05));

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
