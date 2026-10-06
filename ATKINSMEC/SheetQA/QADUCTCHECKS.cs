using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // The four duct disconnection checks. They share one walk of the
    // model (QADUCTS.Of) and then divide the loose ends between them, so
    // nothing is reported twice:
    //
    //   a tap or takeoff                 -> QADUCTTAPCHECK
    //   anything else, a duct in reach   -> QADUCTBRANCHCHECK
    //   two ends meeting but not joined  -> QADUCTJOINCHECK
    //   anything else, nothing in reach  -> QADUCTSTRANDEDCHECK
    //
    // A duct finding belongs to the model rather than to a sheet, so it
    // carries no view. That is also why it cannot be marked in red - the
    // marking needs a view to colour it in.

    // ==================================================

    public class QADUCTTAPCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Ducts; }
        }

        public string Name
        {
            get { return "Taps and takeoffs left hanging"; }
        }

        public string Explanation
        {
            get
            {
                return "Tap and spud fittings with an open end. Usually " +
                       "the duct they were tapped into was moved or " +
                       "deleted and the tap stayed behind.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            QADUCTS.Scan scan = QADUCTS.Of(doc, context, settings);

            foreach (QADUCTS.LooseEnd end in scan.Ends)
            {
                if (!end.OnTap || end.Meets != null)
                    continue;

                findings.Add(QADUCTFINDING.For(
                    end,
                    Name,
                    "tap has an open end",
                    end.Nearest == null
                        ? QADUCTFIX.None
                        : QADUCTFIX.Tap,
                    settings));
            }

            return findings;
        }
    }

    // ==================================================

    public class QADUCTBRANCHCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Ducts; }
        }

        public string Name
        {
            get { return "Branches never tapped into a duct"; }
        }

        public string Explanation
        {
            get
            {
                return "Terminals, flex, branches and equipment with an " +
                       "open end that has a duct within reach - the " +
                       "connection was drawn but never made.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            QADUCTS.Scan scan = QADUCTS.Of(doc, context, settings);

            foreach (QADUCTS.LooseEnd end in scan.Ends)
            {
                if (end.OnTap || end.Meets != null || end.Nearest == null)
                    continue;

                findings.Add(QADUCTFINDING.For(
                    end,
                    Name,
                    "open end, a duct within reach",
                    QADUCTFIX.Branch,
                    settings));
            }

            return findings;
        }
    }

    // ==================================================

    public class QADUCTJOINCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Ducts; }
        }

        public string Name
        {
            get { return "Ends that meet but are not joined"; }
        }

        public string Explanation
        {
            get
            {
                return "Two open ends sitting on the same spot and facing " +
                       "each other. It looks connected on the drawing and " +
                       "is not, which is the one Revit itself warns about.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            QADUCTS.Scan scan = QADUCTS.Of(doc, context, settings);

            // A pair is one finding, not two - reported against the
            // lower id so a re-run always words it the same way
            foreach (QADUCTS.LooseEnd end in scan.Ends)
            {
                if (end.Meets == null || end.OwnerId > end.Meets.OwnerId)
                    continue;

                QAFinding finding = QADUCTFINDING.For(
                    end,
                    Name,
                    "meets " + QADUCTS.Name(end.Meets.Owner) +
                    " but is not joined to it",
                    QADUCTFIX.Join,
                    settings);

                finding.Value = QAGRIDS.FromFeet(
                    end.Point.DistanceTo(end.Meets.Point)).ToString("N0") +
                    " mm apart";

                // The other half of the pair, so the fix can find it
                finding.FixData = end.ConnectorId + ":" +
                                  end.Meets.OwnerId + ":" +
                                  end.Meets.ConnectorId;

                findings.Add(finding);
            }

            return findings;
        }
    }

    // ==================================================

    public class QADUCTSTRANDEDCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Ducts; }
        }

        public string Name
        {
            get { return "Open ends with nothing near them"; }
        }

        public string Explanation
        {
            get
            {
                return "Open ends with no duct within reach. Often a stub " +
                       "left on purpose, so these are reported rather " +
                       "than put right.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            QADUCTS.Scan scan = QADUCTS.Of(doc, context, settings);

            foreach (QADUCTS.LooseEnd end in scan.Ends)
            {
                if (end.OnTap || end.Meets != null || end.Nearest != null)
                    continue;

                findings.Add(QADUCTFINDING.For(
                    end,
                    Name,
                    "open end, nothing within reach",
                    QADUCTFIX.None,
                    settings));
            }

            return findings;
        }
    }

    // ==================================================

    // One finding out of one loose end, worded the same way whichever
    // check is asking
    public class QADUCTFINDING
    {
        public static QAFinding For(
            QADUCTS.LooseEnd end,
            string check,
            string problem,
            string fixKind,
            QASETTINGS settings)
        {
            var finding = new QAFinding
            {
                Check = check,
                Severity = fixKind == QADUCTFIX.None
                    ? QASeverity.Warning
                    : QASeverity.Issue,
                ElementId = end.OwnerId,
                Element = QADUCTS.Name(end.Owner),
                Problem = problem,
                FixKind = fixKind,
                FixData = end.ConnectorId.ToString()
            };

            if (end.Nearest != null)
            {
                finding.Value =
                    QAGRIDS.FromFeet(end.Nearest.Feet).ToString("N0") +
                    " mm to " +
                    (end.Nearest.SameSystem
                        ? "its own system"
                        : "the nearest duct");
            }
            else if (end.HasSystem)
            {
                finding.Value = "system " + end.SystemName;
            }

            // The system is worth seeing at a glance; the sheet columns
            // are free on this page, since a duct belongs to no sheet
            finding.SheetName = string.IsNullOrEmpty(end.SystemName)
                ? "(no system)"
                : end.SystemName;

            finding.ViewName = end.What;

            return finding;
        }
    }
}
