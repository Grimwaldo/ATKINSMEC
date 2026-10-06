using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Does the model agree with the FCU schedule?
    //
    // The schedule is read in the window (FCUSHEET, no Revit in it) and
    // arrives here on QASETTINGS, so a check never touches a file.

    // ==================================================

    public class QAFCUFLOWCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Fcu; }
        }

        public string Name
        {
            get { return "Supply airflow does not match the schedule"; }
        }

        public string Explanation
        {
            get
            {
                return "Compares the duct leaving each FCU's supply " +
                       "connector with the flow rate the schedule gives " +
                       "for that unit.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            if (settings.FcuUnits.Count == 0)
            {
                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Info,
                    Problem = "no schedule loaded - choose the spreadsheet " +
                              "on the right"
                });

                return findings;
            }

            Dictionary<string, FCUSHEET.Unit> sheet = QAFCUMATCH.BySheet(settings);

            QAFCU.Scan scan = QAFCU.Of(doc, context, settings);

            foreach (QAFCU.Unit unit in scan.Units)
            {
                FCUSHEET.Unit wanted;

                if (!sheet.TryGetValue(
                        FCUSHEET.Key(unit.Number), out wanted))
                    continue;

                findings.Add(Compare(unit, wanted, settings));
            }

            findings.RemoveAll(delegate (QAFinding f) { return f == null; });

            return findings;
        }

        private QAFinding Compare(
            QAFCU.Unit unit, FCUSHEET.Unit wanted, QASETTINGS settings)
        {
            var finding = new QAFinding
            {
                Check = Name,
                ElementId = unit.FcuId,
                Element = Label(unit),
                SheetName = unit.Number,
                ViewName = wanted.FlowLitresPerSecond.ToString("N0") + " L/s"
            };

            // Nothing ducted to check
            if (wanted.FreeDischarge || !unit.HasSupplyConnector)
            {
                finding.Severity = QASeverity.Info;

                finding.Problem = wanted.FreeDischarge
                    ? "free discharge unit - no supply duct to check"
                    : "no supply connector on this unit";

                finding.Value = string.Empty;

                return finding;
            }

            if (unit.SupplyDuctId == 0)
            {
                finding.Severity = QASeverity.Warning;
                finding.Problem = "nothing connected to the supply outlet";
                finding.Value = string.Empty;

                return finding;
            }

            if (!unit.DuctFlowKnown)
            {
                finding.Severity = QASeverity.Warning;
                finding.Problem = "the supply duct has no flow to read";
                finding.Value = string.Empty;

                return finding;
            }

            double difference =
                unit.DuctLitres - wanted.FlowLitresPerSecond;

            double allowed = Math.Max(
                wanted.FlowLitresPerSecond * settings.FcuTolerancePercent / 100.0,
                settings.FcuToleranceLitres);

            if (Math.Abs(difference) <= allowed)
                return null;

            finding.Severity = QASeverity.Issue;

            finding.Problem =
                "duct carries " + unit.DuctLitres.ToString("N0") +
                " L/s, schedule says " +
                wanted.FlowLitresPerSecond.ToString("N0");

            finding.Value =
                (difference > 0 ? "+" : "") +
                difference.ToString("N0") + " L/s";

            // Only scalable when there is something to scale
            if (unit.Terminals.Count == 0)
            {
                finding.Problem += " - no air terminals downstream";

                return finding;
            }

            if (unit.TerminalLitres <= 0)
            {
                finding.Problem +=
                    " - its terminals read zero, so there is no ratio to " +
                    "scale by";

                return finding;
            }

            List<QAFCU.Terminal> locked = unit.Locked;

            if (locked.Count > 0)
            {
                // Naming them matters: a read-only Flow is fixed in the
                // family or the workset, and neither can be found from
                // "a terminal is read-only"
                finding.Problem +=
                    " - " + locked.Count + " of " + unit.Terminals.Count +
                    " terminal(s) cannot be written: " + Reasons(locked);

                return finding;
            }

            finding.FixKind = QAFCUFIX.Scale;
            finding.FixData = FCUSHEET.Key(unit.Number);

            return finding;
        }

        // Grouped by reason, each with the element ids behind it, so
        // they can be pasted straight into Revit's Select by ID
        private static string Reasons(List<QAFCU.Terminal> terminals)
        {
            var order = new List<string>();

            var byReason = new Dictionary<string, List<QAFCU.Terminal>>();

            foreach (QAFCU.Terminal terminal in terminals)
            {
                string reason = string.IsNullOrEmpty(terminal.Locked)
                    ? "it cannot be written"
                    : terminal.Locked;

                if (!byReason.ContainsKey(reason))
                {
                    byReason[reason] = new List<QAFCU.Terminal>();
                    order.Add(reason);
                }

                byReason[reason].Add(terminal);
            }

            var parts = new List<string>();

            foreach (string reason in order)
                parts.Add(reason + " (" + Ids(byReason[reason]) + ")");

            return string.Join("; ", parts.ToArray());
        }

        private static string Ids(List<QAFCU.Terminal> terminals)
        {
            var parts = new List<string>();

            for (int i = 0; i < terminals.Count && i < 6; i++)
                parts.Add(terminals[i].Id.ToString());

            string tail = terminals.Count > 6
                ? ", and " + (terminals.Count - 6) + " more"
                : string.Empty;

            return string.Join(", ", parts.ToArray()) + tail;
        }

        private static string Label(QAFCU.Unit unit)
        {
            string name = string.IsNullOrWhiteSpace(unit.Number)
                ? "(no number)"
                : unit.Number;

            return name + "   [" + unit.FcuId + "]";
        }
    }

    // ==================================================

    public class QAFCUMATCHCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Fcu; }
        }

        public string Name
        {
            get { return "FCUs and schedule rows that do not pair up"; }
        }

        public string Explanation
        {
            get
            {
                return "Units in the model with no row in the schedule, " +
                       "and rows in the schedule with no unit in the " +
                       "model. Neither can be checked for flow.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            if (settings.FcuUnits.Count == 0)
                return findings;

            Dictionary<string, FCUSHEET.Unit> sheet =
                QAFCUMATCH.BySheet(settings);

            QAFCU.Scan scan = QAFCU.Of(doc, context, settings);

            var matched = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (QAFCU.Unit unit in scan.Units)
            {
                string key = FCUSHEET.Key(unit.Number);

                if (key.Length > 0 && sheet.ContainsKey(key))
                {
                    matched.Add(key);
                    continue;
                }

                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Warning,
                    ElementId = unit.FcuId,
                    Element = (string.IsNullOrWhiteSpace(unit.Number)
                                  ? "(no number)"
                                  : unit.Number) +
                              "   [" + unit.FcuId + "]",
                    SheetName = unit.TypeName,
                    Problem = key.Length == 0
                        ? "this unit has nothing in the chosen parameter"
                        : "no row in the schedule for this unit",
                    Value = string.Empty
                });
            }

            foreach (FCUSHEET.Unit row in settings.FcuUnits)
            {
                if (matched.Contains(FCUSHEET.Key(row.Number)))
                    continue;

                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Warning,
                    Element = row.Number,
                    SheetName = row.Number,
                    ViewName =
                        row.FlowLitresPerSecond.ToString("N0") + " L/s",
                    Problem = "in the schedule, no such unit in the model",
                    Value = "row " + row.Row
                });
            }

            return findings;
        }
    }

    // ==================================================

    public class QAFCUMATCH
    {
        // The schedule keyed the way names are compared, built once
        public static Dictionary<string, FCUSHEET.Unit> BySheet(
            QASETTINGS settings)
        {
            var byKey = new Dictionary<string, FCUSHEET.Unit>(
                StringComparer.OrdinalIgnoreCase);

            foreach (FCUSHEET.Unit unit in settings.FcuUnits)
            {
                string key = FCUSHEET.Key(unit.Number);

                if (key.Length > 0 && !byKey.ContainsKey(key))
                    byKey[key] = unit;
            }

            return byKey;
        }
    }
}
