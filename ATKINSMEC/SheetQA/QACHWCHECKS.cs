using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Does the chilled water pipework match the sizing table?
    //
    // The table is read in the window (CHWSIZESHEET, no Revit in it) and
    // arrives here on QASETTINGS, so a check never touches a file.
    //
    // A pipe finding carries a FixKind and a size to go to; a fitting
    // finding carries neither, because a fitting has no size of its own
    // to be told - it is the size of the pipes on its ends. The fix
    // still puts it right (QACHWFITTINGS), it just works it out from
    // the pipes rather than from the finding.
    //
    // Resizing was added at the user's request after these checks were
    // first written report-only. It is worth knowing why the hesitation:
    // changing a pipe size changes the hydraulics of everything
    // downstream of it. The button puts the drawing in step with the
    // table it was given; it does not redesign the system.

    // ==================================================

    public class QACHWSIZECHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Chw; }
        }

        public string Name
        {
            get { return "Pipe too small for the flow it carries"; }
        }

        public string Explanation
        {
            get
            {
                return "Compares each pipe and fitting connector with " +
                       "the sizing table: anything carrying more than " +
                       "its size allows.";
            }
        }

        // Only a PIPE gets a size on the finding. A fitting is the
        // size of the pipes on its ends, so the fix reads that from
        // the model at the time rather than carrying a number here -
        // by then the pipes may have just moved.
        public static void Resizable(
            QAFinding finding, QACHW.Run run, CHWSIZESHEET.Size needed)
        {
            if (run.ConnectorId >= 0)
            {
                finding.Problem +=
                    "  (a fitting has no size of its own to be given - " +
                    "the fix sets it to the size of the pipes on its " +
                    "ends, or replaces it with one that is)";

                return;
            }

            if (needed == null)
                return;

            finding.FixKind = QACHWFIX.Resize;
            finding.FixData = needed.DN.ToString("N0");
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            if (QACHWREADY.Complain(settings, Name, findings))
                return findings;

            CHWSIZESHEET.Table table = settings.ChwTable;

            QACHW.Scan scan = QACHW.Of(doc, context, settings);

            // A size the table has never heard of is worth saying once,
            // not once per pipe
            var unknown = new Dictionary<string, int>();

            foreach (QACHW.Run run in scan.Runs)
            {
                if (run.Litres <= 0)
                    continue;

                CHWSIZESHEET.Size drawn = table.Match(
                    run.DnMm, settings.ChwSizeToleranceMm);

                if (drawn == null)
                {
                    string key = run.DnMm.ToString("N0");

                    unknown[key] = unknown.ContainsKey(key)
                        ? unknown[key] + 1
                        : 1;

                    continue;
                }

                if (run.Litres <= drawn.MaxLitresPerSecond)
                    continue;

                CHWSIZESHEET.Size needed = table.SmallestFor(run.Litres);

                var finding = new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Issue,
                    ElementId = run.ElementId,
                    Element = run.Where,
                    SheetName = QACHWWORDS.Side(run),
                    ViewName = "DN" + drawn.DN.ToString("N0"),
                    Value = run.Litres.ToString("N2") + " L/s"
                };

                Resizable(finding, run, needed);

                finding.Problem = needed == null
                    ? "carries " + run.Litres.ToString("N2") +
                      " L/s, past the largest size in the table (DN" +
                      table.Largest.DN.ToString("N0") + ", " +
                      table.Largest.MaxLitresPerSecond.ToString("N1") +
                      " L/s)"
                    : "carries " + run.Litres.ToString("N2") +
                      " L/s; DN" + drawn.DN.ToString("N0") + " allows " +
                      drawn.MaxLitresPerSecond.ToString("N2") +
                      " - needs DN" + needed.DN.ToString("N0");

                findings.Add(finding);
            }

            foreach (KeyValuePair<string, int> odd in unknown)
            {
                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Warning,
                    Element = odd.Key + " mm",
                    Problem = "this size is not in the sizing table, so " +
                              odd.Value + " run(s) could not be judged",
                    Value = odd.Value + " run(s)"
                });
            }

            return findings;
        }
    }

    // ==================================================

    public class QACHWOVERSIZECHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Chw; }
        }

        public string Name
        {
            get { return "Pipe larger than the flow needs"; }
        }

        public string Explanation
        {
            get
            {
                return "The other direction: pipe carrying far less than " +
                       "its size allows. Noisy by nature, so it only " +
                       "reports what is well oversized.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            if (QACHWREADY.Complain(settings, Name, findings))
                return findings;

            CHWSIZESHEET.Table table = settings.ChwTable;

            QACHW.Scan scan = QACHW.Of(doc, context, settings);

            foreach (QACHW.Run run in scan.Runs)
            {
                // No flow means no opinion. A dry branch would otherwise
                // look oversized at every size there is.
                if (run.Litres <= 0)
                    continue;

                if (run.DnMm < settings.ChwOversizeFromMm)
                    continue;

                CHWSIZESHEET.Size drawn = table.Match(
                    run.DnMm, settings.ChwSizeToleranceMm);

                CHWSIZESHEET.Size needed = table.SmallestFor(run.Litres);

                if (drawn == null || needed == null)
                    continue;

                int steps = table.StepsBetween(needed, drawn);

                if (steps < settings.ChwOversizeSteps)
                    continue;

                var finding = new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Warning,
                    ElementId = run.ElementId,
                    Element = run.Where,
                    SheetName = QACHWWORDS.Side(run),
                    ViewName = "DN" + drawn.DN.ToString("N0"),
                    Problem = "carries " + run.Litres.ToString("N2") +
                              " L/s, which DN" +
                              needed.DN.ToString("N0") + " would take - " +
                              steps + " size(s) larger than needed",
                    Value = run.Litres.ToString("N2") + " L/s"
                };

                QACHWSIZECHECK.Resizable(finding, run, needed);

                findings.Add(finding);
            }

            return findings;
        }
    }

    // ==================================================

    // The findings group by this column, so putting the side first
    // sorts supply away from return - which is how a chilled water list
    // is read
    public class QACHWWORDS
    {
        public static string Side(QACHW.Run run)
        {
            if (string.IsNullOrEmpty(run.SystemName))
                return run.Side;

            return run.Side + "  -  " + run.SystemName;
        }
    }

    // Both checks need the same two things before they can say anything
    public class QACHWREADY
    {
        public static bool Complain(
            QASETTINGS settings, string check, List<QAFinding> findings)
        {
            if (settings.ChwTable == null
                || settings.ChwTable.Sizes.Count == 0)
            {
                findings.Add(new QAFinding
                {
                    Check = check,
                    Severity = QASeverity.Info,
                    Problem = "no sizing table loaded - choose the " +
                              "spreadsheet on the right"
                });

                return true;
            }

            if (settings.ChwSystemTypeCount == 0)
            {
                findings.Add(new QAFinding
                {
                    Check = check,
                    Severity = QASeverity.Info,
                    Problem = "no system types ticked - say which of them " +
                              "are the chilled water supply and return"
                });

                return true;
            }

            return false;
        }
    }
}
