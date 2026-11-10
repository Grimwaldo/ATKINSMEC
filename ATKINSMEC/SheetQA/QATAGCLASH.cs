using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // The Tags checker's arithmetic: which annotations in one view sit on
    // top of each other. Revit-free (test: tagclashtest) - QATAGS measures
    // the model and hands every tag head and text note over as a box.
    //
    // Everything here is in PAPER MILLIMETRES, in the view's own axes (x
    // to the right on the sheet, y up). Annotation is drawn at paper size
    // whatever the view's scale, so "overlapping by 0.5 mm" means the
    // same thing on a 1:50 plan as on a 1:200 one.
    public static class QATAGCLASH
    {
        public enum Kind
        {
            Tag,
            Text
        }

        // One annotation as it reads on the sheet: a rectangle, turned if
        // the annotation is turned
        public class Box
        {
            public long Id;
            public Kind What;

            // Centre
            public double X;
            public double Y;

            // Unit vector along its width (its height is square to it)
            public double Ux = 1;
            public double Uy;

            public double HalfW;
            public double HalfH;

            // Measured as Revit's box around it, which is square to the
            // MODEL, not to a turned view - so bigger than it really is
            public bool Rough;

            // What it tags (tags only), so a tag sitting on another tag
            // of the same element can be called what it is
            public List<string> Tagged = new List<string>();

            // Half the size of the box around it, square to the view
            public double ReachX
            {
                get { return HalfW * Math.Abs(Ux) + HalfH * Math.Abs(Uy); }
            }

            public double ReachY
            {
                get { return HalfW * Math.Abs(Uy) + HalfH * Math.Abs(Ux); }
            }
        }

        public class Clash
        {
            public Box A;
            public Box B;

            // How far one would have to move to clear the other, mm on
            // paper
            public double Depth;

            // Both tag the same element: one tag on top of another
            public bool SameElement;
        }

        // ==================================================
        // Making boxes
        // ==================================================

        // A rectangle turned by angle (radians, anticlockwise from the
        // view's x)
        public static Box Turned(long id, Kind what, double x, double y,
                                 double angle, double width, double height)
        {
            return new Box
            {
                Id = id,
                What = what,
                X = x,
                Y = y,
                Ux = Math.Cos(angle),
                Uy = Math.Sin(angle),
                HalfW = Math.Abs(width) / 2,
                HalfH = Math.Abs(height) / 2
            };
        }

        // A text note: Revit gives the middle, left or right of its TOP
        // edge (by its alignment), the way its baseline and its letters'
        // up run in the view, and its width and height on paper
        public enum Align
        {
            Left,
            Centre,
            Right
        }

        public static Box FromText(long id, double topX, double topY,
                                   double baseX, double baseY, double upX, double upY,
                                   double width, double height, Align align)
        {
            Unit(ref baseX, ref baseY, 1, 0);
            Unit(ref upX, ref upY, -baseY, baseX);

            double along = align == Align.Left ? width / 2
                         : align == Align.Right ? -width / 2
                         : 0;

            return new Box
            {
                Id = id,
                What = Kind.Text,
                X = topX + baseX * along - upX * height / 2,
                Y = topY + baseY * along - upY * height / 2,
                Ux = baseX,
                Uy = baseY,
                HalfW = Math.Abs(width) / 2,
                HalfH = Math.Abs(height) / 2
            };
        }

        // From Revit's box around a tag (Element.get_BoundingBox), which is
        // ALWAYS square to the model's axes: in a turned view - or for a
        // turned tag - it is bigger than the tag. Knowing which way the
        // tag lies (the view's right and up in model axes, and the tag's
        // own turn in the view), the size of the box along each model axis
        // is w|u| + h|v| of the tag's width and height; three axes, two
        // unknowns, solved by least squares.
        //
        // Where that cannot be solved - near 45 degrees the width and the
        // height cannot be told apart - the box around it is used as it
        // is, square to the view, and called Rough.
        //
        // extents: the model box's size along model x, y, z, already in
        // paper mm; (x, y): its middle in the view.
        public static Box FromModelBox(long id, Kind what, double x, double y,
                                       double[] extents, double[] right, double[] up,
                                       double angle)
        {
            double c = Math.Cos(angle);
            double s = Math.Sin(angle);

            var u = new double[3];
            var v = new double[3];

            for (int k = 0; k < 3; k++)
            {
                u[k] = Math.Abs(c * right[k] + s * up[k]);
                v[k] = Math.Abs(-s * right[k] + c * up[k]);
            }

            double uu = 0, vv = 0, uv = 0, ue = 0, ve = 0, size = 0;

            for (int k = 0; k < 3; k++)
            {
                uu += u[k] * u[k];
                vv += v[k] * v[k];
                uv += u[k] * v[k];
                ue += u[k] * extents[k];
                ve += v[k] * extents[k];
                size = Math.Max(size, extents[k]);
            }

            double det = uu * vv - uv * uv;

            // det / (uu vv) is the square of the sine between |u| and |v|:
            // 1 square to the model, 0 at 45 degrees in plan. Under a
            // quarter, an error of a fraction of a millimetre in Revit's
            // box comes back several times over.
            if (uu > 1e-12 && vv > 1e-12 && det / (uu * vv) >= 0.25)
            {
                double w = (vv * ue - uv * ve) / det;
                double h = (uu * ve - uv * ue) / det;

                bool fits = w >= -1e-6 && h >= -1e-6;

                for (int k = 0; k < 3 && fits; k++)
                {
                    // An axis the view's plane does not run along (z in a
                    // plan): what the box measures there is its depth into
                    // the view, nothing of its width or height
                    if (u[k] + v[k] < 1e-9)
                        continue;

                    double predicted = w * u[k] + h * v[k];

                    if (Math.Abs(predicted - extents[k]) > 0.25 + 0.05 * size)
                        fits = false;
                }

                if (fits)
                {
                    return new Box
                    {
                        Id = id,
                        What = what,
                        X = x,
                        Y = y,
                        Ux = c,
                        Uy = s,
                        HalfW = Math.Max(0, w) / 2,
                        HalfH = Math.Max(0, h) / 2
                    };
                }
            }

            double reachX = 0, reachY = 0;

            for (int k = 0; k < 3; k++)
            {
                reachX += Math.Abs(right[k]) * extents[k];
                reachY += Math.Abs(up[k]) * extents[k];
            }

            return new Box
            {
                Id = id,
                What = what,
                X = x,
                Y = y,
                Ux = 1,
                Uy = 0,
                HalfW = reachX / 2,
                HalfH = reachY / 2,
                Rough = true
            };
        }

        private static void Unit(ref double x, ref double y, double fallX, double fallY)
        {
            double length = Math.Sqrt(x * x + y * y);

            if (length < 1e-9)
            {
                x = fallX;
                y = fallY;

                length = Math.Sqrt(x * x + y * y);

                if (length < 1e-9)
                {
                    x = 1;
                    y = 0;

                    return;
                }
            }

            x /= length;
            y /= length;
        }

        // ==================================================
        // Overlaps
        // ==================================================

        // How far the two overlap - the least they would have to move
        // apart, along any of their four edges' directions, to clear each
        // other. Zero or less: they do not touch.
        public static double Overlap(Box a, Box b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            double least = double.MaxValue;

            // The two rectangles' edge directions are the only ones that
            // can separate them (separating axes)
            double[][] axes =
            {
                new[] { a.Ux, a.Uy },
                new[] { -a.Uy, a.Ux },
                new[] { b.Ux, b.Uy },
                new[] { -b.Uy, b.Ux }
            };

            foreach (double[] n in axes)
            {
                double reachA = Reach(a, n[0], n[1]);
                double reachB = Reach(b, n[0], n[1]);

                double apart = Math.Abs(dx * n[0] + dy * n[1]);

                double overlap = reachA + reachB - apart;

                if (overlap < least)
                    least = overlap;
            }

            return least;
        }

        private static double Reach(Box box, double nx, double ny)
        {
            return box.HalfW * Math.Abs(box.Ux * nx + box.Uy * ny)
                 + box.HalfH * Math.Abs(-box.Uy * nx + box.Ux * ny);
        }

        // Every pair overlapping by more than the tolerance (mm on paper).
        // Swept along x, so a plan with two thousand tags is not four
        // million comparisons.
        public static List<Clash> Find(IList<Box> boxes, double tolerance)
        {
            var clashes = new List<Clash>();

            if (boxes == null || boxes.Count < 2)
                return clashes;

            var order = new List<Box>(boxes);

            order.Sort((p, q) =>
            {
                int byLeft = (p.X - p.ReachX).CompareTo(q.X - q.ReachX);

                return byLeft != 0 ? byLeft : p.Id.CompareTo(q.Id);
            });

            for (int i = 0; i < order.Count; i++)
            {
                Box a = order[i];

                double right = a.X + a.ReachX;

                for (int j = i + 1; j < order.Count; j++)
                {
                    Box b = order[j];

                    if (b.X - b.ReachX > right)
                        break;

                    if (Math.Abs(b.Y - a.Y) > a.ReachY + b.ReachY)
                        continue;

                    double depth = Overlap(a, b);

                    if (depth <= tolerance)
                        continue;

                    // The older annotation first, whichever came first in
                    // the sweep: the same pair reads the same way every run
                    Box first = a.Id <= b.Id ? a : b;
                    Box second = first == a ? b : a;

                    clashes.Add(new Clash
                    {
                        A = first,
                        B = second,
                        Depth = depth,
                        SameElement = Shares(first, second)
                    });
                }
            }

            clashes.Sort((p, q) =>
            {
                int byA = p.A.Id.CompareTo(q.A.Id);

                return byA != 0 ? byA : p.B.Id.CompareTo(q.B.Id);
            });

            return clashes;
        }

        private static bool Shares(Box a, Box b)
        {
            if (a.What != Kind.Tag || b.What != Kind.Tag)
                return false;

            foreach (string tagged in a.Tagged)
            {
                if (b.Tagged.Contains(tagged))
                    return true;
            }

            return false;
        }

        // The same two annotations, whichever way round: one finding for
        // them, though they show in a view and in its dependents
        public static string Pair(long a, long b)
        {
            return Math.Min(a, b) + ":" + Math.Max(a, b);
        }
    }
}
