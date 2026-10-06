using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Correcting an FCU's airflow to what the schedule says.
    //
    // The terminals are what get written, not the duct: a duct's flow is
    // worked out by Revit from the terminals it feeds, so putting the
    // terminals right is what puts the duct right.
    //
    // Every terminal keeps its share. If four terminals read 40/40/40/40
    // and the schedule says 200, they all become 50; if they read
    // 10/10/10/130 they become 12.5/12.5/12.5/162.5. Whoever balanced
    // that branch had a reason, and a ratio keeps it while a flat split
    // would throw it away.
    //
    // Like the other fixes, everything is worked out again from the
    // model at the moment of correcting, and each unit sits in its own
    // SubTransaction so one that will not take rolls back alone.
    public class QAFCUFIX
    {
        public const string Scale = "fcu-scale";

        public class Result
        {
            public int Fixed { get; set; }
            public int Terminals { get; set; }
            public int Skipped { get; set; }

            // Units somebody had already put right since the check ran
            public int Already { get; set; }

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
            QASETTINGS settings,
            QASCOPE scope)
        {
            var result = new Result();

            // The model as it is now, not as the list remembers it
            QAFCU.Scan scan = QAFCU.Of(
                doc, new QACONTEXT { Scope = scope }, settings);

            var byId = new Dictionary<long, QAFCU.Unit>();

            foreach (QAFCU.Unit unit in scan.Units)
                byId[unit.FcuId] = unit;

            Dictionary<string, FCUSHEET.Unit> sheet =
                QAFCUMATCH.BySheet(settings);

            foreach (QAFinding finding in findings)
            {
                if (finding.FixKind != Scale)
                {
                    result.Skipped++;
                    continue;
                }

                QAFCU.Unit unit;

                if (!byId.TryGetValue(finding.ElementId, out unit))
                {
                    result.Skipped++;
                    continue;
                }

                FCUSHEET.Unit wanted;

                if (!sheet.TryGetValue(
                        finding.FixData ?? string.Empty, out wanted))
                {
                    result.Skipped++;

                    result.Note(
                        "Some units are no longer in the schedule that is " +
                        "loaded. Load the same spreadsheet the check ran " +
                        "against.");

                    continue;
                }

                Apply(doc, unit, wanted, settings, result);
            }

            if (result.Skipped > 0)
            {
                result.Note(
                    "Some findings cannot be corrected automatically - a " +
                    "unit with no terminals downstream, or whose " +
                    "terminals all read zero, has no ratio to scale by.");
            }

            return result;
        }

        private static void Apply(
            Document doc,
            QAFCU.Unit unit,
            FCUSHEET.Unit wanted,
            QASETTINGS settings,
            Result result)
        {
            // Worked out again: the terminals may have moved on since
            // the list was made
            if (unit.Terminals.Count == 0 || unit.TerminalLitres <= 0)
            {
                result.Skipped++;
                return;
            }

            double allowed = Math.Max(
                wanted.FlowLitresPerSecond * settings.FcuTolerancePercent / 100.0,
                settings.FcuToleranceLitres);

            // Already right - somebody got there first, or an earlier
            // pass in this same run did
            if (unit.DuctFlowKnown
                && Math.Abs(unit.DuctLitres - wanted.FlowLitresPerSecond)
                   <= allowed)
            {
                result.Already++;
                return;
            }

            double ratio = wanted.FlowLitresPerSecond / unit.TerminalLitres;

            if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio <= 0)
            {
                result.Skipped++;
                return;
            }

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    int written = 0;

                    foreach (QAFCU.Terminal terminal in unit.Terminals)
                    {
                        // Read again rather than trusting the number on
                        // the list, then scale what is actually there
                        double now;

                        if (!QAFCU.Reading(doc, terminal, out now))
                            continue;

                        if (!QAFCU.Write(doc, terminal, now * ratio))
                        {
                            sub.RollBack();

                            result.Skipped++;

                            result.Note(
                                "Some terminals would not take a new flow, " +
                                "through either their Flow parameter or " +
                                "the connector carrying the air. A " +
                                "connector set to Calculated or System " +
                                "rather than Preset cannot be given one - " +
                                "that is set in the family.");

                            return;
                        }

                        written++;
                    }

                    if (written == 0)
                    {
                        sub.RollBack();

                        result.Skipped++;

                        return;
                    }

                    sub.Commit();

                    result.Fixed++;
                    result.Terminals += written;
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Skipped++;

                    result.Note(
                        "Some units would not take a new flow: " +
                        Short(ex) + ". Nothing was lost - each unit is " +
                        "undone on its own when it fails.");
                }
            }
        }

        private static string Short(Exception ex)
        {
            string message = (ex.Message ?? string.Empty).Trim();

            if (message.Length > 120)
                message = message.Substring(0, 117) + "...";

            return message;
        }
    }
}
