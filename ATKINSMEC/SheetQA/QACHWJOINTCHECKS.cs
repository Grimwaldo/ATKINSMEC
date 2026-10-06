using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // The three CHW disconnection checks. They share one walk
    // (QACHWJOINTS.Of) and divide the loose ends between them, so
    // nothing is reported twice:
    //
    //   belongs on a fan coil connector   -> QACHWFCUJOINTCHECK
    //   two ends meeting, not joined      -> QACHWMEETCHECK
    //   anything else                     -> QACHWSTRANDEDCHECK
    //
    // Like the duct page, a finding belongs to the MODEL rather than to
    // a sheet, so it carries no view and cannot be marked in red - the
    // marking needs a view to colour it in.

    // ==================================================

    public class QACHWFCUJOINTCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.ChwJoints; }
        }

        public string Name
        {
            get { return "Pipes not connected to the unit they serve"; }
        }

        public string Explanation
        {
            get
            {
                return "A hydronic pipe - supply or return, both are " +
                       "checked - with an open end near a free connector " +
                       "of the SAME side on a fan coil or other " +
                       "equipment. The pipe was drawn to it but never " +
                       "joined, so the unit reads as having no water on " +
                       "it.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            QACHWJOINTS.Scan scan = QACHWJOINTS.Of(doc, context, settings);

            foreach (QACHWJOINTS.LooseEnd end in scan.Ends)
            {
                if (end.Unit == null)
                    continue;

                findings.Add(QACHWJOINTFINDING.For(
                    end,
                    Name,
                    "open end, " + QAGRIDS.FromFeet(end.Unit.Gap)
                        .ToString("N0") + " mm from " +
                    end.Unit.Equipment +
                    (end.Unit.Unclassified
                        ? "  -  that connector has NO system set, so it " +
                          "was matched on position"
                        : string.Empty) +
                    (end.Unit.Ambiguous
                        ? "  -  CHECK: that unit has more than one free " +
                          "connector of this system and only the distance " +
                          "chose between them"
                        : string.Empty),
                    QACHWJOINTFIX.ToUnit));
            }

            return findings;
        }
    }

    // ==================================================

    public class QACHWMEETCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.ChwJoints; }
        }

        public string Name
        {
            get { return "Ends that meet but are not joined"; }
        }

        public string Explanation
        {
            get
            {
                return "Two open chilled water ends facing each other " +
                       "within reach. They look joined on the drawing " +
                       "and are not, so nothing flows between them.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            QACHWJOINTS.Scan scan = QACHWJOINTS.Of(doc, context, settings);

            var told = new HashSet<long>();

            foreach (QACHWJOINTS.LooseEnd end in scan.Ends)
            {
                if (end.Unit != null || end.Meets == null)
                    continue;

                // One finding per PAIR, not one per end
                long key = Key(end);

                if (!told.Add(key))
                    continue;

                told.Add(Key(end.Meets));

                findings.Add(QACHWJOINTFINDING.For(
                    end,
                    Name,
                    "meets " + end.Meets.What + " but is not joined",
                    QACHWJOINTFIX.Join));
            }

            return findings;
        }

        private static long Key(QACHWJOINTS.LooseEnd end)
        {
            return end.OwnerId * 1000 + end.ConnectorId;
        }
    }

    // ==================================================

    public class QACHWSTRANDEDCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.ChwJoints; }
        }

        public string Name
        {
            get { return "Open ends with nothing near them"; }
        }

        public string Explanation
        {
            get
            {
                return "A chilled water end with nothing within reach. " +
                       "Often a deliberate stub for a future connection, " +
                       "which is why this is a warning and is never put " +
                       "right automatically. Where there IS a unit of " +
                       "the same system further off, the finding names " +
                       "it and says how far - raise the reach above " +
                       "that figure and the pipe becomes joinable.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            QACHWJOINTS.Scan scan = QACHWJOINTS.Of(doc, context, settings);

            foreach (QACHWJOINTS.LooseEnd end in scan.Ends)
            {
                if (end.Unit != null || end.Meets != null)
                    continue;

                // "NOTHING WITHIN REACH" ON ITS OWN IS NOT ACTIONABLE.
                // The scan already knows the nearest unit of the right
                // system and how far off it was, so saying it costs
                // nothing and turns a shrug into a number to type into
                // the reach box.
                double reach = QACHWJOINTS.Reach(settings);

                string why;

                if (end.NearestUnit.Length > 0 && end.NearestGap > reach)
                    why = "open end, nearest is " + end.NearestUnit +
                          " at " + QAGRIDS.FromFeet(end.NearestGap)
                              .ToString("N0") + " mm  -  beyond the " +
                          QAGRIDS.FromFeet(reach).ToString("N0") +
                          " mm reach";
                else if (end.NearestUnit.Length > 0)
                    why = "open end, nearest is " + end.NearestUnit +
                          " at " + QAGRIDS.FromFeet(end.NearestGap)
                              .ToString("N0") + " mm  -  but its " +
                          "connector of this system has already gone " +
                          "to a nearer pipe";
                else if (scan.Equipment == 0)
                    why = "open end, and there is no mechanical " +
                          "equipment in the scope at all  -  widen the " +
                          "scope, or check the ticked families";

                // THE ONE A PERSON CANNOT SEE ON THE DRAWING. The pipe
                // is touching the unit and still will not join, because
                // the two disagree about what system they carry. Naming
                // BOTH classifications is the whole value of this line -
                // it is the difference between "the add-in is broken"
                // and "the family's connector says OtherPipe".
                else if (end.NearestAny.Length > 0)
                    why = "open end, nearest is " + end.NearestAny +
                          " at " + QAGRIDS.FromFeet(end.NearestAnyGap)
                              .ToString("N0") + " mm  -  but that " +
                          "connector carries " +
                          QACHWJOINTS.SideName(end.NearestAnySide) +
                          " and this pipe carries " +
                          QACHWJOINTS.SideName(end.Side) +
                          ", so they are never joined";
                else if (end.Side == QACHWSIDE.Undefined)
                    why = "open end, and the pipe is on no system  -  " +
                          "nothing can be matched to it until it is";
                else if (scan.EquipmentFree == 0)
                    why = "open end, and none of the " + scan.Equipment +
                          " unit(s) in scope has a free pipe connector " +
                          "left  -  everything is already joined to " +
                          "something";
                else
                    why = "open end, no free connector of this system " +
                          "on any of the ticked equipment";

                QAFinding finding = QACHWJOINTFINDING.For(
                    end,
                    Name,
                    why,
                    QACHWJOINTFIX.None);

                // A stub is a thing somebody meant, so this never
                // shouts - the same call QADUCTSTRANDEDCHECK makes
                finding.Severity = QASeverity.Warning;

                findings.Add(finding);
            }

            return findings;
        }
    }

    // ==================================================

    // One finding out of one loose end, so the three checks cannot
    // describe the same thing differently
    public static class QACHWJOINTFINDING
    {
        public static QAFinding For(
            QACHWJOINTS.LooseEnd end,
            string check,
            string problem,
            string fixKind)
        {
            return new QAFinding
            {
                Check = check,
                Severity = QASeverity.Issue,

                // No view: a pipe belongs to the model, not to a sheet.
                // The two columns carry the system and what it is
                // instead - exactly what the duct page does.
                SheetNumber = end.SystemName,
                SheetName = end.What,

                ElementId = end.OwnerId,

                Element = end.What + " " + end.OwnerId,
                Problem = problem,

                Value = Where(end),

                FixKind = fixKind,
                FixData = end.ConnectorId.ToString()
            };
        }

        private static string Where(QACHWJOINTS.LooseEnd end)
        {
            if (end.Point == null)
                return string.Empty;

            return QAGRIDS.FromFeet(end.Point.X).ToString("N0") + ", " +
                   QAGRIDS.FromFeet(end.Point.Y).ToString("N0") + ", " +
                   QAGRIDS.FromFeet(end.Point.Z).ToString("N0");
        }
    }
}
