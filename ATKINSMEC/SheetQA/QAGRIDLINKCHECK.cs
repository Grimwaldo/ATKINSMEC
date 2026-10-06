using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Do this model's grids still agree with the model they were copied
    // from? Grids get nudged, renamed and deleted long after they are
    // copy/monitored or copied in, and nothing tells you.
    //
    // The comparison is of the grid LINE, not the drawn ends, and in
    // plan only: a grid is the same grid however far it is drawn and
    // whatever level it sits at. Ragged ends are the extents check's
    // business, not this one's.
    public class QAGRIDLINKCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Grids; }
        }

        public string Name
        {
            get { return "Grids match the model they came from"; }
        }

        public string Explanation
        {
            get
            {
                return "Compares this model's grids with a linked " +
                       "model's, by name and position. Reports grids " +
                       "that moved, went missing, or were added here.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            if (settings.GridLinkIds.Count == 0)
            {
                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Info,
                    Problem = "no linked model chosen to compare against"
                });

                return findings;
            }

            List<QAGRIDS.Linked> theirs =
                QAGRIDS.FromLinks(doc, settings.GridLinkIds);

            if (theirs.Count == 0)
            {
                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Warning,
                    Problem = "the chosen link has no grids in it"
                });

                return findings;
            }

            double tolerance = QAGRIDS.ToFeet(settings.GridToleranceMm);

            var matched = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (Grid grid in QAGRIDS.All(doc))
            {
                Curve curve;

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

                string name = (grid.Name ?? string.Empty).Trim();

                QAGRIDS.Linked twin = Find(theirs, name);

                if (twin == null)
                {
                    findings.Add(Finding(
                        grid,
                        QASeverity.Issue,
                        "not in the linked model",
                        string.Empty));

                    continue;
                }

                matched.Add(name);

                double offFeet = QAGRIDS.LineOffsetFeet(
                    curve.GetEndPoint(0), curve.GetEndPoint(1),
                    twin.Start, twin.End);

                if (offFeet > tolerance)
                {
                    QAFinding drifted = Finding(
                        grid,
                        QASeverity.Issue,
                        "moved from where the link has it",
                        QAGRIDS.FromFeet(offFeet).ToString("N0") + " mm");

                    // Only a drifted grid can be put back. A missing one
                    // has to be drawn and an extra one deleted, and
                    // neither is a decision for an add-in.
                    drifted.FixKind = QAGRIDFIX.Moved;

                    findings.Add(drifted);
                }
            }

            // Anything in the link that never turned up here
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (QAGRIDS.Linked their in theirs)
            {
                string name = (their.Name ?? string.Empty).Trim();

                if (matched.Contains(name) || !seen.Add(name))
                    continue;

                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Issue,
                    Element = "Grid " + name + "   (" + their.LinkName + ")",
                    Problem = "in the link but missing here",
                    Value = string.Empty
                });
            }

            return findings;
        }

        private static QAGRIDS.Linked Find(
            List<QAGRIDS.Linked> grids, string name)
        {
            foreach (QAGRIDS.Linked grid in grids)
            {
                if (string.Equals((grid.Name ?? string.Empty).Trim(), name,
                        StringComparison.OrdinalIgnoreCase))
                    return grid;
            }

            return null;
        }

        // A grid belongs to the model, not to one sheet, so these
        // findings carry no view - they sort to the end of the list and
        // Show finds them through the model rather than a view
        private QAFinding Finding(
            Grid grid, QASeverity severity, string problem, string value)
        {
            return new QAFinding
            {
                Check = Name,
                Severity = severity,
                ElementId = grid.Id.ToValue(),
                Element = "Grid " + grid.Name,
                Problem = problem,
                Value = value
            };
        }
    }
}
