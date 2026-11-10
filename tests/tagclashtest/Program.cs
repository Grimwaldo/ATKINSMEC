using ATKINSMEC;
using System;
using System.Collections.Generic;

// SHEET QA's Tags checker: the REAL QATAGCLASH compiled in.
//
// What it decides, with no Revit: the box a tag head or a text note takes
// on paper (from a text note's corner, or from Revit's model-square box
// round a tag, turned back to the view), how
// far two boxes overlap - turned ones too - and which pairs in a view
// overlap by more than the tolerance.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static bool Near(double a, double b, double within = 1e-6)
    {
        return Math.Abs(a - b) <= within;
    }

    private static QATAGCLASH.Box Square(long id, double x, double y, double w, double h,
                                         double degrees = 0,
                                         QATAGCLASH.Kind what = QATAGCLASH.Kind.Tag)
    {
        return QATAGCLASH.Turned(id, what, x, y, degrees * Math.PI / 180, w, h);
    }

    private static double[] V(double x, double y, double z)
    {
        return new[] { x, y, z };
    }

    private static double[] Unit(double[] v)
    {
        double l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);

        return V(v[0] / l, v[1] / l, v[2] / l);
    }

    // What Revit's box round a w x h rectangle turned by t in a view with
    // these right / up directions measures along model x, y and z - from
    // its four corners, NOT from the formula the code solves
    private static double[] ModelExtents(double w, double h, double degrees,
                                         double[] right, double[] up, double pad = 0)
    {
        double t = degrees * Math.PI / 180;

        double c = Math.Cos(t), s = Math.Sin(t);

        var min = V(double.MaxValue, double.MaxValue, double.MaxValue);
        var max = V(double.MinValue, double.MinValue, double.MinValue);

        foreach (double a in new[] { -0.5, 0.5 })
        {
            foreach (double b in new[] { -0.5, 0.5 })
            {
                // a along the width, b along the height, in view axes
                double vx = a * w * c - b * h * s;
                double vy = a * w * s + b * h * c;

                for (int k = 0; k < 3; k++)
                {
                    double p = vx * right[k] + vy * up[k];

                    min[k] = Math.Min(min[k], p);
                    max[k] = Math.Max(max[k], p);
                }
            }
        }

        return V(max[0] - min[0] + 2 * pad, max[1] - min[1] + 2 * pad, max[2] - min[2] + 2 * pad);
    }

    private static double[] Plan(double degrees)
    {
        double t = degrees * Math.PI / 180;

        return V(Math.Cos(t), Math.Sin(t), 0);
    }

    private static double[] PlanUp(double degrees)
    {
        double t = degrees * Math.PI / 180;

        return V(-Math.Sin(t), Math.Cos(t), 0);
    }

    private static int Main()
    {
        Console.WriteLine("SHEET QA tags checker harness");
        Console.WriteLine();

        // ---- overlap of two boxes -----------------------------------------
        {
            var a = Square(1, 0, 0, 10, 3);
            var b = Square(2, 8, 0, 10, 3);

            Check("side by side, 2 mm into each other: depth 2", Near(QATAGCLASH.Overlap(a, b), 2));
            Check("the same either way round", Near(QATAGCLASH.Overlap(b, a), 2));

            var c = Square(3, 11, 0, 10, 3);

            Check("1 mm apart: depth -1", Near(QATAGCLASH.Overlap(a, c), -1));

            var d = Square(4, 0, 2.5, 10, 3);

            Check("one above the other, 0.5 mm into each other: depth 0.5",
                  Near(QATAGCLASH.Overlap(a, d), 0.5));

            var inside = Square(5, 1, 0, 2, 1);

            Check("a small tag right inside a big one overlaps",
                  QATAGCLASH.Overlap(a, inside) > 0.9);
        }

        // ---- turned boxes: the separating axes, not the boxes round them ---
        {
            var square = Square(1, 0, 0, 4, 4);

            // A square turned 45 degrees off the first one's corner: the
            // boxes ROUND them overlap (its reach is 2.83 each way), the
            // squares themselves are 0.85 mm apart along the diagonal
            var diamond = Square(2, 4.6, 4.6, 4, 4, 45);

            Check("boxes round them overlap", diamond.X - diamond.ReachX < 2 && diamond.Y - diamond.ReachY < 2);
            Check("a turned tag off a corner does not overlap", QATAGCLASH.Overlap(square, diamond) < 0);
            Check("... and is not a clash", QATAGCLASH.Find(new[] { square, diamond }, 0).Count == 0);

            // Moved in until the first one's corner is 0.5 mm into its
            // flat side (reach along the diagonal: 2.83 and 2)
            double inward = (2 * Math.Sqrt(2) + 2 - 0.5) / Math.Sqrt(2);
            var into = Square(3, inward, inward, 4, 4, 45);

            Check("a corner 0.5 mm into its side: depth 0.5",
                  Near(QATAGCLASH.Overlap(square, into), 0.5, 1e-9));

            // Two vertical tags (90 degrees) side by side
            var up1 = Square(4, 0, 0, 10, 2, 90);
            var up2 = Square(5, 1.5, 0, 10, 2, 90);

            Check("two vertical tags 1.5 mm apart centre to centre: depth 0.5",
                  Near(QATAGCLASH.Overlap(up1, up2), 0.5, 1e-9));
        }

        // ---- a text note from its top edge ---------------------------------
        {
            var left = QATAGCLASH.FromText(1, 0, 0, 1, 0, 0, 1, 20, 4, QATAGCLASH.Align.Left);

            Check("left-aligned text: the point is its top left", Near(left.X, 10) && Near(left.Y, -2));
            Check("text size", Near(left.HalfW, 10) && Near(left.HalfH, 2) && left.What == QATAGCLASH.Kind.Text);

            var right = QATAGCLASH.FromText(2, 0, 0, 1, 0, 0, 1, 20, 4, QATAGCLASH.Align.Right);

            Check("right-aligned: top right", Near(right.X, -10) && Near(right.Y, -2));

            var centre = QATAGCLASH.FromText(3, 0, 0, 1, 0, 0, 1, 20, 4, QATAGCLASH.Align.Centre);

            Check("centred: top middle", Near(centre.X, 0) && Near(centre.Y, -2));

            // Reading up the sheet: baseline up, letters' up to the left
            var vertical = QATAGCLASH.FromText(4, 0, 0, 0, 1, -1, 0, 20, 4, QATAGCLASH.Align.Left);

            Check("vertical text", Near(vertical.X, 2) && Near(vertical.Y, 10) && Near(vertical.Uy, 1));

            // A view's own scale is not here: the directions come in as they
            // are, not quite unit length
            var unscaled = QATAGCLASH.FromText(5, 0, 0, 3, 0, 0, 3, 20, 4, QATAGCLASH.Align.Left);

            Check("directions made unit length", Near(unscaled.X, 10) && Near(unscaled.Y, -2));
        }

        // ---- Revit's model-square box, turned back to the view ------------
        {
            // A 10 x 2 mm tag: square to everything
            var plan0 = QATAGCLASH.FromModelBox(1, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 0, Plan(0), PlanUp(0)), Plan(0), PlanUp(0), 0);

            Check("plain plan: as it is", !plan0.Rough && Near(plan0.HalfW, 5) && Near(plan0.HalfH, 1));

            var vertical = QATAGCLASH.FromModelBox(2, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 90, Plan(0), PlanUp(0)), Plan(0), PlanUp(0), Math.PI / 2);

            Check("vertical tag", !vertical.Rough && Near(vertical.HalfW, 5) && Near(vertical.HalfH, 1));

            var tagTurned = QATAGCLASH.FromModelBox(3, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 20, Plan(0), PlanUp(0)), Plan(0), PlanUp(0), 20 * Math.PI / 180);

            Check("tag turned 20 degrees in a plain plan", !tagTurned.Rough && Near(tagTurned.HalfW, 5) && Near(tagTurned.HalfH, 1));

            var viewTurned = QATAGCLASH.FromModelBox(4, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 0, Plan(25), PlanUp(25)), Plan(25), PlanUp(25), 0);

            Check("plan turned 25 degrees: the real size, not the box round it",
                  !viewTurned.Rough && Near(viewTurned.HalfW, 5) && Near(viewTurned.HalfH, 1));

            var both = QATAGCLASH.FromModelBox(5, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 10, Plan(-20), PlanUp(-20)), Plan(-20), PlanUp(-20), 10 * Math.PI / 180);

            Check("view -20, tag +10", !both.Rough && Near(both.HalfW, 5) && Near(both.HalfH, 1));

            double[] across = Unit(V(Math.Cos(0.65), Math.Sin(0.65), 0));
            double[] z = V(0, 0, 1);

            var section = QATAGCLASH.FromModelBox(6, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 0, across, z), across, z, 0);

            Check("section cut at 37 degrees", !section.Rough && Near(section.HalfW, 5) && Near(section.HalfH, 1));

            double[] isoRight = Unit(V(1, -1, 0));
            double[] isoUp = Unit(V(1, 1, 2));

            var iso = QATAGCLASH.FromModelBox(7, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 0, isoRight, isoUp), isoRight, isoUp, 0);

            Check("3D view", !iso.Rough && Near(iso.HalfW, 5) && Near(iso.HalfH, 1));

            var padded = QATAGCLASH.FromModelBox(8, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 0, Plan(25), PlanUp(25), 0.1), Plan(25), PlanUp(25), 0);

            Check("Revit's box 0.1 mm loose all round: within 0.5 mm",
                  !padded.Rough && Near(padded.HalfW, 5, 0.25) && Near(padded.HalfH, 1, 0.25));

            // Revit's box round a tag in a plan may have depth (z): it
            // says nothing about the tag's size on paper
            double[] deep = ModelExtents(10, 2, 0, Plan(25), PlanUp(25));
            deep[2] = 50;

            var depth = QATAGCLASH.FromModelBox(11, QATAGCLASH.Kind.Tag, 0, 0,
                deep, Plan(25), PlanUp(25), 0);

            Check("a plan's box with depth: still exact", !depth.Rough && Near(depth.HalfW, 5) && Near(depth.HalfH, 1));

            // A 3D view, the tag turned 60 degrees but reported square: no
            // width and height reproduce its box, so it is rough - and
            // covers the tag - rather than a confident wrong size
            double[] tiltRight = Unit(V(1, -1, 0));
            double[] tiltUp = Unit(V(1, 1, 2));

            var misread = QATAGCLASH.FromModelBox(12, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 60, tiltRight, tiltUp), tiltRight, tiltUp, 0);

            double t60 = 60 * Math.PI / 180;

            Check("3D view, a turn not reported: rough, and never smaller than the tag",
                  misread.Rough
                  && misread.ReachX >= 0.5 * (10 * Math.Cos(t60) + 2 * Math.Sin(t60)) - 1e-9
                  && misread.ReachY >= 0.5 * (10 * Math.Sin(t60) + 2 * Math.Cos(t60)) - 1e-9);

            // 45 degrees: width and height cannot be told apart
            var rough = QATAGCLASH.FromModelBox(9, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 0, Plan(45), PlanUp(45)), Plan(45), PlanUp(45), 0);

            Check("plan at 45 degrees: rough", rough.Rough);
            Check("... and never smaller than the tag", rough.HalfW >= 5 && rough.HalfH >= 1);

            // A turn Revit did not report (a tag turning with its element,
            // say) in a plain plan: the box round it, which covers it
            var wrong = QATAGCLASH.FromModelBox(10, QATAGCLASH.Kind.Tag, 0, 0,
                ModelExtents(10, 2, 70, Plan(0), PlanUp(0)), Plan(0), PlanUp(0), 0);

            double t70 = 70 * Math.PI / 180;

            Check("a turn not reported: the box round the tag, never smaller",
                  wrong.ReachX >= 0.5 * (10 * Math.Cos(t70) + 2 * Math.Sin(t70)) - 1e-9
                  && wrong.ReachY >= 0.5 * (10 * Math.Sin(t70) + 2 * Math.Cos(t70)) - 1e-9);
        }

        // ---- finding the pairs ---------------------------------------------
        {
            var a = Square(10, 0, 0, 10, 3);
            var b = Square(11, 9.8, 0, 10, 3);

            Check("0.2 mm into each other, tolerance 0.5: not a clash", QATAGCLASH.Find(new[] { a, b }, 0.5).Count == 0);
            Check("tolerance 0.1: a clash", QATAGCLASH.Find(new[] { a, b }, 0.1).Count == 1);

            var later = Square(30, 0, 0, 10, 3);
            var earlier = Square(20, 5, 0, 10, 3);

            List<QATAGCLASH.Clash> one = QATAGCLASH.Find(new[] { later, earlier }, 0);

            Check("the older annotation first", one.Count == 1 && one[0].A.Id == 20 && one[0].B.Id == 30);
            // 5 mm into each other along x, 3 up or down: clearing them is 3
            Check("depth = the shorter way clear", one.Count == 1 && Near(one[0].Depth, 3));

            var tag1 = Square(40, 0, 0, 10, 3);
            var tag2 = Square(41, 1, 0, 10, 3);
            var text = Square(42, 2, 0, 10, 3, 0, QATAGCLASH.Kind.Text);

            tag1.Tagged.Add("h:500");
            tag2.Tagged.Add("h:501");
            tag2.Tagged.Add("h:500");

            // Never so for text, whatever it carries
            text.Tagged.Add("h:500");

            List<QATAGCLASH.Clash> three = QATAGCLASH.Find(new[] { tag1, tag2, text }, 0.5);

            Check("three on top of each other: three pairs", three.Count == 3);

            QATAGCLASH.Clash same = three.Find(x => x.A.Id == 40 && x.B.Id == 41);

            Check("two tags of one element: same element", same != null && same.SameElement);
            Check("a tag and a text note: never the same element",
                  three.TrueForAll(x => x.B.Id != 42 || !x.SameElement));

            Check("one box, or none: nothing", QATAGCLASH.Find(new[] { a }, 0).Count == 0
                                               && QATAGCLASH.Find(new QATAGCLASH.Box[0], 0).Count == 0
                                               && QATAGCLASH.Find(null, 0).Count == 0);

            Check("a pair is one key either way round",
                  QATAGCLASH.Pair(5, 3) == QATAGCLASH.Pair(3, 5) && QATAGCLASH.Pair(3, 5) == "3:5");
        }

        // ---- the sweep finds what comparing every pair finds --------------
        {
            var random = new Random(20261110);

            bool same = true;
            int pairs = 0;

            for (int round = 0; round < 20 && same; round++)
            {
                var boxes = new List<QATAGCLASH.Box>();

                for (int i = 0; i < 300; i++)
                {
                    boxes.Add(Square(1000 + i,
                        random.NextDouble() * 400 - 200,
                        random.NextDouble() * 300 - 150,
                        2 + random.NextDouble() * 25,
                        2 + random.NextDouble() * 6,
                        random.Next(4) == 0 ? random.NextDouble() * 360 : 0));
                }

                var expected = new HashSet<string>();

                for (int i = 0; i < boxes.Count; i++)
                {
                    for (int j = i + 1; j < boxes.Count; j++)
                    {
                        if (QATAGCLASH.Overlap(boxes[i], boxes[j]) > 0.3)
                            expected.Add(QATAGCLASH.Pair(boxes[i].Id, boxes[j].Id));
                    }
                }

                var found = new HashSet<string>();

                foreach (QATAGCLASH.Clash clash in QATAGCLASH.Find(boxes, 0.3))
                    found.Add(QATAGCLASH.Pair(clash.A.Id, clash.B.Id));

                pairs += expected.Count;

                same = expected.SetEquals(found);
            }

            Check("20 rounds of 300 boxes, some turned: the sweep = every pair (" + pairs + " pairs)", same && pairs > 100);
        }

        Console.WriteLine();

        if (_failed == 0)
        {
            Console.WriteLine("All passed.");
            return 0;
        }

        Console.WriteLine(_failed + " FAILED");
        return 1;
    }
}
