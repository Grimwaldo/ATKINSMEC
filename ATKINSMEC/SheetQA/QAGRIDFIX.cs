using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Putting right what the grid checks found.
    //
    // Everything here is worked out again from the model at the moment
    // of fixing, rather than trusting numbers carried on the findings.
    // The findings may be minutes old and somebody may have moved a grid
    // since; recomputing costs almost nothing and cannot apply a stale
    // correction.
    public class QAGRIDFIX
    {
        // What a finding can be put right by, if anything. A string on
        // the finding rather than a switch on the check's display name,
        // so renaming a check cannot quietly break fixing.
        public const string Extent = "grid-extent";
        public const string Bubble = "grid-bubble";
        public const string Moved = "grid-moved";

        public class Result
        {
            public int Fixed { get; set; }
            public int Skipped { get; set; }
            public List<string> Notes { get; set; } = new List<string>();

            public void Note(string text)
            {
                if (!Notes.Contains(text))
                    Notes.Add(text);
            }
        }

        // Must be called inside a transaction
        public static Result Fix(
            Document doc,
            IEnumerable<QAFinding> findings,
            QASETTINGS settings)
        {
            var result = new Result();

            var extents = new List<QAFinding>();
            var bubbles = new List<QAFinding>();
            var moved = new List<QAFinding>();

            foreach (QAFinding finding in findings)
            {
                if (finding.FixKind == Extent)
                    extents.Add(finding);
                else if (finding.FixKind == Bubble)
                    bubbles.Add(finding);
                else if (finding.FixKind == Moved)
                    moved.Add(finding);
                else
                    result.Skipped++;
            }

            FixExtents(doc, extents, settings, result);
            FixBubbles(doc, bubbles, settings, result);
            FixMoved(doc, moved, settings, result);

            if (result.Skipped > 0)
            {
                result.Note(
                    result.Skipped + " finding(s) cannot be put right " +
                    "automatically - a missing grid has to be drawn, and " +
                    "a grid the link does not have has to be deleted by " +
                    "somebody who knows why it is there.");
            }

            return result;
        }

        // ==================================================
        // Ragged ends
        // ==================================================

        private static void FixExtents(
            Document doc,
            List<QAFinding> findings,
            QASETTINGS settings,
            Result result)
        {
            if (findings.Count == 0)
                return;

            // One pass per view: the standard has to be recomputed from
            // the grids as they are now, not as they were when the
            // check ran
            var byView = new Dictionary<long, List<long>>();

            foreach (QAFinding finding in findings)
            {
                List<long> grids;

                if (!byView.TryGetValue(finding.ViewId, out grids))
                {
                    grids = new List<long>();
                    byView[finding.ViewId] = grids;
                }

                grids.Add(finding.ElementId);
            }

            foreach (KeyValuePair<long, List<long>> pair in byView)
            {
                var view = doc.GetElement(REVITCOMPAT.NewId(pair.Key))
                    as View;

                if (view == null)
                {
                    result.Skipped += pair.Value.Count;
                    continue;
                }

                List<QAGRIDS.InView> drawn = QAGRIDS.Drawn(doc, view);

                foreach (long gridId in pair.Value)
                {
                    QAGRIDS.InView entry = Find(drawn, gridId);

                    if (entry == null || entry.Direction == QAGRIDS.Run.Skew)
                    {
                        result.Skipped++;
                        continue;
                    }

                    double lowest;
                    double highest;

                    Standard(drawn, entry.Direction, out lowest, out highest);

                    if (!Stretch(view, entry, lowest, highest))
                    {
                        result.Skipped++;

                        result.Note(
                            "Some grids would not take a new extent. That " +
                            "is usually a grid driven by a scope box - " +
                            "the scope box decides how far it reaches, so " +
                            "change that instead.");

                        continue;
                    }

                    result.Fixed++;
                }
            }
        }

        private static void Standard(
            List<QAGRIDS.InView> drawn,
            QAGRIDS.Run direction,
            out double lowest,
            out double highest)
        {
            lowest = double.MaxValue;
            highest = double.MinValue;

            foreach (QAGRIDS.InView entry in drawn)
            {
                if (entry.Direction != direction)
                    continue;

                double low;
                double high;

                QAGRIDS.Span(entry, out low, out high);

                if (low < lowest)
                    lowest = low;

                if (high > highest)
                    highest = high;
            }
        }

        // Stretches the drawn line out to the standard span, along the
        // grid's own line so it does not shift sideways
        private static bool Stretch(
            View view, QAGRIDS.InView entry, double lowest, double highest)
        {
            XYZ axis = entry.Direction == QAGRIDS.Run.Horizontal
                ? view.RightDirection
                : view.UpDirection;

            XYZ p0 = entry.Start;
            XYZ p1 = entry.End;

            double s0 = p0.DotProduct(axis);
            double s1 = p1.DotProduct(axis);

            double spread = s1 - s0;

            // A grid with no reach along its own axis cannot be
            // stretched by this arithmetic
            if (Math.Abs(spread) < 1e-9)
                return false;

            XYZ along = p1 - p0;

            double tLow = (lowest - s0) / spread;
            double tHigh = (highest - s0) / spread;

            XYZ a = p0 + along * tLow;
            XYZ b = p0 + along * tHigh;

            if (a.DistanceTo(b) < 1e-6)
                return false;

            try
            {
                // Most grids carry a 3D extent shared by every view, and
                // SetCurveInView will only take a view-specific one.
                // Switching this view to 2D first is what a drafter does
                // by dragging the grip, and it leaves the grid's other
                // views exactly as they were.
                entry.Grid.SetDatumExtentType(
                    DatumEnds.End0, view, DatumExtentType.ViewSpecific);

                entry.Grid.SetDatumExtentType(
                    DatumEnds.End1, view, DatumExtentType.ViewSpecific);

                entry.Grid.SetCurveInView(
                    DatumExtentType.ViewSpecific, view, Line.CreateBound(a, b));

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ==================================================
        // Bubbles
        // ==================================================

        private static void FixBubbles(
            Document doc,
            List<QAFinding> findings,
            QASETTINGS settings,
            Result result)
        {
            if (findings.Count == 0)
                return;

            var byView = new Dictionary<long, List<long>>();

            foreach (QAFinding finding in findings)
            {
                List<long> grids;

                if (!byView.TryGetValue(finding.ViewId, out grids))
                {
                    grids = new List<long>();
                    byView[finding.ViewId] = grids;
                }

                grids.Add(finding.ElementId);
            }

            foreach (KeyValuePair<long, List<long>> pair in byView)
            {
                var view = doc.GetElement(REVITCOMPAT.NewId(pair.Key))
                    as View;

                if (view == null)
                {
                    result.Skipped += pair.Value.Count;
                    continue;
                }

                List<QAGRIDS.InView> drawn = QAGRIDS.Drawn(doc, view);

                foreach (long gridId in pair.Value)
                {
                    QAGRIDS.InView entry = Find(drawn, gridId);

                    if (entry == null || entry.Direction == QAGRIDS.Run.Skew)
                    {
                        result.Skipped++;
                        continue;
                    }

                    bool topOrLeft = entry.Direction == QAGRIDS.Run.Vertical
                        ? settings.BubbleTop
                        : settings.BubbleLeft;

                    DatumEnds want = QAGRIDS.WantedEnd(entry, topOrLeft);
                    DatumEnds other = QAGRIDS.Other(want);

                    try
                    {
                        entry.Grid.ShowBubbleInView(want, view);
                        entry.Grid.HideBubbleInView(other, view);

                        result.Fixed++;
                    }
                    catch
                    {
                        result.Skipped++;
                    }
                }
            }
        }

        // ==================================================
        // Grids that drifted from the link
        // ==================================================

        private static void FixMoved(
            Document doc,
            List<QAFinding> findings,
            QASETTINGS settings,
            Result result)
        {
            if (findings.Count == 0)
                return;

            List<QAGRIDS.Linked> theirs =
                QAGRIDS.FromLinks(doc, settings.GridLinkIds);

            if (theirs.Count == 0)
            {
                result.Skipped += findings.Count;

                result.Note(
                    "Moved grids need the linked model ticked to know " +
                    "where to put them back.");

                return;
            }

            foreach (QAFinding finding in findings)
            {
                var grid = doc.GetElement(REVITCOMPAT.NewId(finding.ElementId))
                    as Grid;

                if (grid == null)
                {
                    result.Skipped++;
                    continue;
                }

                if (grid.Pinned)
                {
                    result.Skipped++;

                    result.Note(
                        "Some grids are pinned and were left alone. " +
                        "Unpin them first if they really should move.");

                    continue;
                }

                Curve curve;

                try
                {
                    curve = grid.Curve;
                }
                catch
                {
                    result.Skipped++;
                    continue;
                }

                QAGRIDS.Linked twin = FindByName(theirs, grid.Name);

                if (twin == null || curve == null)
                {
                    result.Skipped++;
                    continue;
                }

                XYZ shift = Offset(curve.GetEndPoint(0), twin.Start, twin.End);

                if (shift == null || shift.GetLength() < 1e-9)
                {
                    result.Skipped++;
                    continue;
                }

                try
                {
                    ElementTransformUtils.MoveElement(doc, grid.Id, shift);

                    result.Fixed++;
                }
                catch
                {
                    result.Skipped++;
                }
            }
        }

        // The sideways shift that lands the grid on the link's line, in
        // plan - a grid must not be dragged up or down a level
        private static XYZ Offset(XYZ point, XYZ a, XYZ b)
        {
            XYZ along = b - a;

            double length = along.GetLength();

            if (length < 1e-9)
                return null;

            XYZ unit = along / length;

            XYZ from = point - a;

            XYZ nearest = a + unit * from.DotProduct(unit);

            return new XYZ(
                nearest.X - point.X,
                nearest.Y - point.Y,
                0);
        }

        // ==================================================

        private static QAGRIDS.InView Find(
            List<QAGRIDS.InView> drawn, long gridId)
        {
            foreach (QAGRIDS.InView entry in drawn)
            {
                if (entry.Grid.Id.ToValue() == gridId)
                    return entry;
            }

            return null;
        }

        private static QAGRIDS.Linked FindByName(
            List<QAGRIDS.Linked> grids, string name)
        {
            string wanted = (name ?? string.Empty).Trim();

            foreach (QAGRIDS.Linked grid in grids)
            {
                if (string.Equals((grid.Name ?? string.Empty).Trim(), wanted,
                        StringComparison.OrdinalIgnoreCase))
                    return grid;
            }

            return null;
        }
    }
}
