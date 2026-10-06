using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Grid bubbles on the wrong end. Which end they belong on is an
    // office convention rather than anything Revit enforces, so the
    // wanted corner is chosen in the window and this reports whatever
    // disagrees with it.
    //
    // Top/bottom and left/right are read in the VIEW's own frame, using
    // its right and up directions, so a rotated plan is judged the way
    // it is actually drawn rather than by world coordinates.
    public class QAGRIDBUBBLECHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Grids; }
        }

        public string Name
        {
            get { return "Grid bubbles on the wrong end"; }
        }

        public string Explanation
        {
            get
            {
                return "Reports grids whose bubble is not at the corner " +
                       "chosen on the right, or that show one at both " +
                       "ends, or none at all.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            foreach (View view in context.Views)
            {
                if (!Plan(view))
                    continue;

                ViewSheet sheet = context.SheetFor(view);

                foreach (QAGRIDS.InView grid in QAGRIDS.Drawn(doc, view))
                {
                    // A grid running diagonally has no top or left end
                    // to speak of, so the convention cannot be applied
                    if (grid.Direction == QAGRIDS.Run.Skew)
                        continue;

                    bool topOrLeft = grid.Direction == QAGRIDS.Run.Vertical
                        ? settings.BubbleTop
                        : settings.BubbleLeft;

                    DatumEnds wanted = QAGRIDS.WantedEnd(grid, topOrLeft);
                    DatumEnds other = QAGRIDS.Other(wanted);

                    bool onWanted =
                        QAGRIDS.HasBubble(grid.Grid, view, wanted);

                    bool onOther =
                        QAGRIDS.HasBubble(grid.Grid, view, other);

                    string problem = null;

                    if (!onWanted && !onOther)
                        problem = "no bubble shown";
                    else if (!onWanted)
                        problem = "bubble on the wrong end";
                    else if (onOther)
                        problem = "bubble at both ends";

                    if (problem == null)
                        continue;

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
                        FixKind = QAGRIDFIX.Bubble,
                        Problem = problem,
                        Value = "want " + settings.BubbleDescription
                    });
                }
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
    }
}
