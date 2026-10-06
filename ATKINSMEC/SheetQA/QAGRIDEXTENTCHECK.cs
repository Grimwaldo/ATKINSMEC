using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Ragged grid ends. On a finished drawing every grid running the
    // same way should stop on the same line, so the bubbles sit in a
    // row; one grid short or long is the commonest thing to notice on a
    // printed sheet and the easiest to miss on screen.
    //
    // The standard is taken from the grids themselves: the furthest any
    // of them reaches is the extent that covers the drawing, so anything
    // falling short of that is what gets reported.
    public class QAGRIDEXTENTCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Grids; }
        }

        public string Name
        {
            get { return "Grid ends are ragged"; }
        }

        public string Explanation
        {
            get
            {
                return "Grids running the same way should all stop on " +
                       "the same line. Reports any that fall short of " +
                       "the furthest one in that view.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            double tolerance = QAGRIDS.ToFeet(settings.ExtentToleranceMm);

            foreach (View view in context.Views)
            {
                if (!Plan(view))
                    continue;

                List<QAGRIDS.InView> grids = QAGRIDS.Drawn(doc, view);

                if (grids.Count < 2)
                    continue;

                ViewSheet sheet = context.SheetFor(view);

                // Each direction is its own family; a vertical grid
                // ending short of a horizontal one means nothing
                Check(findings, view, sheet, grids,
                    QAGRIDS.Run.Vertical, tolerance);

                Check(findings, view, sheet, grids,
                    QAGRIDS.Run.Horizontal, tolerance);
            }

            return findings;
        }

        private static bool Plan(View view)
        {
            return view.ViewType == ViewType.FloorPlan
                || view.ViewType == ViewType.CeilingPlan
                || view.ViewType == ViewType.EngineeringPlan
                || view.ViewType == ViewType.AreaPlan;
        }

        private void Check(
            List<QAFinding> findings,
            View view,
            ViewSheet sheet,
            List<QAGRIDS.InView> grids,
            QAGRIDS.Run direction,
            double tolerance)
        {
            var family = new List<QAGRIDS.InView>();

            foreach (QAGRIDS.InView grid in grids)
            {
                if (grid.Direction == direction)
                    family.Add(grid);
            }

            if (family.Count < 2)
                return;

            // The furthest reach in each direction is the standard
            double lowest = double.MaxValue;
            double highest = double.MinValue;

            foreach (QAGRIDS.InView grid in family)
            {
                double low;
                double high;

                QAGRIDS.Span(grid, out low, out high);

                if (low < lowest)
                    lowest = low;

                if (high > highest)
                    highest = high;
            }

            foreach (QAGRIDS.InView grid in family)
            {
                double low;
                double high;

                QAGRIDS.Span(grid, out low, out high);

                double shortLow = low - lowest;
                double shortHigh = highest - high;

                double worst = Math.Max(shortLow, shortHigh);

                if (worst <= tolerance)
                    continue;

                string which = shortLow > shortHigh
                    ? (direction == QAGRIDS.Run.Vertical
                        ? "bottom" : "left")
                    : (direction == QAGRIDS.Run.Vertical
                        ? "top" : "right");

                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Issue,
                    SheetNumber = sheet == null
                        ? string.Empty : sheet.SheetNumber,
                    SheetName = sheet == null
                        ? string.Empty : sheet.Name,
                    ViewName = view.Name,
                    ViewId = view.Id.ToValue(),
                    ElementId = grid.Grid.Id.ToValue(),
                    Element = "Grid " + grid.Name,
                    FixKind = QAGRIDFIX.Extent,
                    Problem = "short at the " + which,
                    Value = QAGRIDS.FromFeet(worst).ToString("N0") + " mm"
                });
            }
        }
    }
}
