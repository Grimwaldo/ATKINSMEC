using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // Shared grid geometry for the presentation checks.
    //
    // Everything here works in the coordinates of one view, using the
    // view's own right and up directions rather than world X and Y, so a
    // rotated plan is handled the same as a square one.
    public class QAGRIDS
    {
        public enum Run
        {
            // Runs across the view: its two ends are left and right
            Horizontal,

            // Runs up the view: its two ends are top and bottom
            Vertical,

            // Neither, so top/left has no meaning for it
            Skew
        }

        public class InView
        {
            public Grid Grid;
            public string Name;

            public Curve Curve;

            public XYZ Start;
            public XYZ End;

            public Run Direction;

            // Where the two ends sit once the view is taken into
            // account: how far along right, and how far up
            public double StartAcross;
            public double EndAcross;
            public double StartUp;
            public double EndUp;
        }

        public static List<Grid> All(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();
        }

        // Every grid actually drawn in this view, with its ends worked
        // out in the view's own frame
        public static List<InView> Drawn(Document doc, View view)
        {
            var found = new List<InView>();

            XYZ right = view.RightDirection;
            XYZ up = view.UpDirection;

            var grids =
                new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>();

            foreach (Grid grid in grids)
            {
                Curve curve = CurveInView(grid, view);

                if (curve == null)
                    continue;

                XYZ a = curve.GetEndPoint(0);
                XYZ b = curve.GetEndPoint(1);

                var entry = new InView
                {
                    Grid = grid,
                    Name = grid.Name,
                    Curve = curve,
                    Start = a,
                    End = b,
                    StartAcross = a.DotProduct(right),
                    EndAcross = b.DotProduct(right),
                    StartUp = a.DotProduct(up),
                    EndUp = b.DotProduct(up)
                };

                entry.Direction = DirectionOf(entry);

                found.Add(entry);
            }

            return found;
        }

        private static Curve CurveInView(Grid grid, View view)
        {
            try
            {
                IList<Curve> curves =
                    grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);

                if (curves != null && curves.Count > 0)
                    return curves[0];
            }
            catch
            {
                // not drawn in this view at all
            }

            try
            {
                return grid.Curve;
            }
            catch
            {
                return null;
            }
        }

        private static Run DirectionOf(InView entry)
        {
            double across = Math.Abs(entry.EndAcross - entry.StartAcross);
            double up = Math.Abs(entry.EndUp - entry.StartUp);

            // A grid three times longer one way than the other is
            // clearly running that way; anything else is skew and the
            // top/left idea does not apply to it
            if (across > up * 3.0)
                return Run.Horizontal;

            if (up > across * 3.0)
                return Run.Vertical;

            return Run.Skew;
        }

        // ==================================================
        // Ends
        // ==================================================

        // Which DatumEnd is the top one for a vertical grid, or the left
        // one for a horizontal grid
        public static DatumEnds WantedEnd(InView entry, bool topOrLeft)
        {
            if (entry.Direction == Run.Vertical)
            {
                bool startIsTop = entry.StartUp > entry.EndUp;

                if (topOrLeft)
                    return startIsTop ? DatumEnds.End0 : DatumEnds.End1;

                return startIsTop ? DatumEnds.End1 : DatumEnds.End0;
            }

            bool startIsLeft = entry.StartAcross < entry.EndAcross;

            if (topOrLeft)
                return startIsLeft ? DatumEnds.End0 : DatumEnds.End1;

            return startIsLeft ? DatumEnds.End1 : DatumEnds.End0;
        }

        public static DatumEnds Other(DatumEnds end)
        {
            return end == DatumEnds.End0 ? DatumEnds.End1 : DatumEnds.End0;
        }

        public static bool HasBubble(Grid grid, View view, DatumEnds end)
        {
            try
            {
                return grid.HasBubbleInView(end, view);
            }
            catch
            {
                return false;
            }
        }

        // ==================================================
        // Extents
        // ==================================================

        // How far the grid reaches along its own run, as a low and high
        // pair in the view's frame
        public static void Span(
            InView entry, out double low, out double high)
        {
            double a;
            double b;

            if (entry.Direction == Run.Horizontal)
            {
                a = entry.StartAcross;
                b = entry.EndAcross;
            }
            else
            {
                a = entry.StartUp;
                b = entry.EndUp;
            }

            low = Math.Min(a, b);
            high = Math.Max(a, b);
        }

        // ==================================================
        // Matching against a link
        // ==================================================

        public class Linked
        {
            public string Name;
            public XYZ Start;
            public XYZ End;
            public string LinkName;
        }

        public static List<Linked> FromLinks(
            Document doc, IEnumerable<long> linkIds)
        {
            var found = new List<Linked>();

            foreach (long linkId in linkIds)
            {
                var link = doc.GetElement(REVITCOMPAT.NewId(linkId))
                    as RevitLinkInstance;

                if (link == null)
                    continue;

                Document linkDoc = link.GetLinkDocument();

                if (linkDoc == null)
                    continue;

                Transform transform = link.GetTransform();

                foreach (Grid grid in All(linkDoc))
                {
                    Curve curve = null;

                    try
                    {
                        curve = grid.Curve;
                    }
                    catch
                    {
                        continue;
                    }

                    if (curve == null)
                        continue;

                    found.Add(new Linked
                    {
                        Name = grid.Name,
                        // The link has its own placement
                        Start = transform.OfPoint(curve.GetEndPoint(0)),
                        End = transform.OfPoint(curve.GetEndPoint(1)),
                        LinkName = linkDoc.Title
                    });
                }
            }

            return found;
        }

        // A grid is the same grid wherever its line lies, so the
        // comparison is of the infinite line, not the drawn ends
        public static double LineOffsetFeet(
            XYZ aStart, XYZ aEnd, XYZ bStart, XYZ bEnd)
        {
            double one = PointToLine(aStart, bStart, bEnd);
            double two = PointToLine(aEnd, bStart, bEnd);

            return Math.Max(one, two);
        }

        private static double PointToLine(XYZ point, XYZ a, XYZ b)
        {
            XYZ along = b - a;

            double length = along.GetLength();

            if (length < 1e-9)
                return point.DistanceTo(a);

            XYZ unit = along / length;

            XYZ offset = point - a;

            double projected = offset.DotProduct(unit);

            XYZ nearest = a + unit * projected;

            // Plan distance: a grid at another level is not "moved"
            double dx = point.X - nearest.X;
            double dy = point.Y - nearest.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double ToFeet(double millimetres)
        {
            return UnitUtils.ConvertToInternalUnits(
                millimetres, UnitTypeId.Millimeters);
        }

        public static double FromFeet(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(
                feet, UnitTypeId.Millimeters);
        }
    }
}
