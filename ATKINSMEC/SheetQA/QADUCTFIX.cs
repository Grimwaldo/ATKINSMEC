using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Joining up what the duct checks found.
    //
    // Like QAGRIDFIX, everything is worked out again from the model at
    // the moment of fixing: the whole network is walked afresh and the
    // findings are matched to it by (element, connector). A finding whose
    // end is no longer loose is left alone rather than acted on twice.
    //
    // Every single change sits in its own SubTransaction, so one that
    // Revit refuses rolls back by itself and the rest still go through.
    // That matters more here than anywhere else in the add-in: this is
    // the only fix that creates and deletes elements.
    public class QADUCTFIX
    {
        public const string None = "";
        public const string Tap = "duct-tap";
        public const string Branch = "duct-branch";
        public const string Join = "duct-join";

        public class Result
        {
            public int Fixed { get; set; }
            public int Skipped { get; set; }

            // Ends somebody had already joined up since the check ran
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
            QADUCTS.Scan scan = QADUCTS.Of(
                doc, new QACONTEXT { Scope = scope }, settings);

            var byEnd = new Dictionary<string, QADUCTS.LooseEnd>();

            foreach (QADUCTS.LooseEnd end in scan.Ends)
                byEnd[Key(end.OwnerId, end.ConnectorId)] = end;

            // Joining two ends that already meet is the cheapest and
            // safest, and doing it first can make later ones unnecessary
            Pass(doc, findings, Join, byEnd, settings, result);
            Pass(doc, findings, Tap, byEnd, settings, result);
            Pass(doc, findings, Branch, byEnd, settings, result);

            foreach (QAFinding finding in findings)
            {
                if (string.IsNullOrEmpty(finding.FixKind))
                    result.Skipped++;
            }

            if (result.Skipped > 0)
            {
                result.Note(
                    "Some findings cannot be joined up automatically - an " +
                    "open end with no duct within reach is usually a stub " +
                    "left on purpose, and guessing what it should reach " +
                    "is not an add-in's decision.");
            }

            return result;
        }

        private static string Key(long ownerId, int connectorId)
        {
            return ownerId + ":" + connectorId;
        }

        private static void Pass(
            Document doc,
            IEnumerable<QAFinding> findings,
            string kind,
            Dictionary<string, QADUCTS.LooseEnd> byEnd,
            QASETTINGS settings,
            Result result)
        {
            foreach (QAFinding finding in findings)
            {
                if (finding.FixKind != kind)
                    continue;

                int connectorId;

                if (!First(finding.FixData, out connectorId))
                {
                    result.Skipped++;
                    continue;
                }

                QADUCTS.LooseEnd end;

                if (!byEnd.TryGetValue(
                        Key(finding.ElementId, connectorId), out end))
                {
                    // Not loose any more - somebody got there first, or
                    // an earlier fix in this same run joined it up
                    result.Already++;
                    continue;
                }

                bool done;

                if (kind == Join)
                    done = DoJoin(doc, finding, end, result);
                else if (kind == Tap)
                    done = DoTap(doc, end, settings, result);
                else
                    done = DoBranch(doc, end, result);

                if (done)
                    result.Fixed++;
                else
                    result.Skipped++;
            }
        }

        // ==================================================
        // Two ends that meet
        // ==================================================

        private static bool DoJoin(
            Document doc,
            QAFinding finding,
            QADUCTS.LooseEnd end,
            Result result)
        {
            long otherOwner;
            int otherConnector;

            if (!Other(finding.FixData, out otherOwner, out otherConnector))
                return false;

            Connector one = QADUCTS.Lookup(
                doc, end.OwnerId, end.ConnectorId);

            Connector two = QADUCTS.Lookup(
                doc, otherOwner, otherConnector);

            if (one == null || two == null)
                return false;

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    one.ConnectTo(two);

                    sub.Commit();

                    return true;
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Note(
                        "Some ends would not join: " + Short(ex) + ". Two " +
                        "ends can sit on the same spot and still be the " +
                        "wrong size or shape for each other.");

                    return false;
                }
            }
        }

        // ==================================================
        // A branch that was never tapped in
        // ==================================================

        private static bool DoBranch(
            Document doc, QADUCTS.LooseEnd end, Result result)
        {
            if (end.Nearest == null)
            {
                result.Note(
                    "Some ends no longer have a duct within reach - the " +
                    "reach on the right decides how far to look.");

                return false;
            }

            Connector connector = QADUCTS.Lookup(
                doc, end.OwnerId, end.ConnectorId);

            if (connector == null)
                return false;

            return TakeOff(doc, connector, end.Nearest.Duct, result);
        }

        // ==================================================
        // A tap left hanging
        // ==================================================

        // An orphan tap is replaced rather than repaired: the branch it
        // still holds is remembered, the tap is deleted, and Revit is
        // asked for a fresh takeoff onto the duct. Repairing in place is
        // not on offer - a tap's main side has no connector to join to,
        // it is hosted on the duct's body.
        private static bool DoTap(
            Document doc,
            QADUCTS.LooseEnd end,
            QASETTINGS settings,
            Result result)
        {
            if (end.Nearest == null)
            {
                result.Note(
                    "Some taps have no duct within reach to tap back into.");

                return false;
            }

            long branchOwner;
            int branchConnector;

            if (!Branching(doc, end, out branchOwner, out branchConnector))
            {
                result.Note(
                    "Some taps have nothing connected on either side. A " +
                    "spare tap is deleted by hand - it is not for an " +
                    "add-in to decide it is rubbish.");

                return false;
            }

            long ductId = end.Nearest.DuctId;

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    doc.Delete(REVITCOMPAT.NewId(end.OwnerId));

                    doc.Regenerate();

                    var duct = doc.GetElement(REVITCOMPAT.NewId(ductId))
                        as MEPCurve;

                    Connector branch = QADUCTS.Lookup(
                        doc, branchOwner, branchConnector);

                    if (duct == null || branch == null || branch.IsConnected)
                    {
                        sub.RollBack();

                        return false;
                    }

                    doc.Create.NewTakeoffFitting(branch, duct);

                    sub.Commit();

                    return true;
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Note(
                        "Some taps could not be replaced: " + Short(ex) +
                        ". Nothing was lost - each one is undone on its " +
                        "own when it fails.");

                    return false;
                }
            }
        }

        // What the tap still holds on its other side
        private static bool Branching(
            Document doc,
            QADUCTS.LooseEnd end,
            out long ownerId,
            out int connectorId)
        {
            ownerId = 0;
            connectorId = 0;

            Element tap = doc.GetElement(REVITCOMPAT.NewId(end.OwnerId));

            if (tap == null)
                return false;

            ConnectorManager manager = QADUCTS.Manager(tap);

            if (manager == null)
                return false;

            ConnectorSet connectors;

            try
            {
                connectors = manager.Connectors;
            }
            catch
            {
                return false;
            }

            foreach (Connector connector in connectors)
            {
                bool connected;

                try
                {
                    connected = connector.IsConnected
                                && connector.Domain == Domain.DomainHvac;
                }
                catch
                {
                    continue;
                }

                if (!connected)
                    continue;

                ConnectorSet refs;

                try
                {
                    refs = connector.AllRefs;
                }
                catch
                {
                    continue;
                }

                foreach (Connector other in refs)
                {
                    try
                    {
                        if (other.Owner == null
                            || other.Owner.Id.ToValue() == end.OwnerId
                            || other.ConnectorType != ConnectorType.End)
                            continue;

                        // INSULATION IS IN AllRefs, and nothing above
                        // catches it: the Domain test on the way in is
                        // on the TAP's own connector, not on what comes
                        // back, and a cast would be no help either -
                        // DuctInsulation derives from MEPCurve, and so
                        // does DuctLining. Compile-verified. Asked by
                        // CATEGORY instead; see QADUCTS.Carries.
                        //
                        // What this pair becomes is the reason it
                        // matters. DoTap hands it to NewTakeoffFitting
                        // as the BRANCH connector, having already
                        // deleted the tap - so a sleeve here means
                        // Revit is asked to build a takeoff onto
                        // fibreglass. The SubTransaction rolls that
                        // back, which is the good case: the tap simply
                        // reads as "could not be replaced" when it was
                        // perfectly fixable.
                        if (!QADUCTS.Carries(other.Owner))
                            continue;

                        ownerId = other.Owner.Id.ToValue();
                        connectorId = other.Id;

                        return true;
                    }
                    catch
                    {
                        // that reference is not usable; try the next
                    }
                }
            }

            return false;
        }

        // ==================================================

        private static bool TakeOff(
            Document doc, Connector connector, MEPCurve duct, Result result)
        {
            if (duct == null)
                return false;

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    doc.Create.NewTakeoffFitting(connector, duct);

                    sub.Commit();

                    return true;
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Note(
                        "Some ends would not take a takeoff: " + Short(ex) +
                        ". Revit refuses when the end does not face the " +
                        "duct, or the duct type has no tap in its routing " +
                        "preferences.");

                    return false;
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

        // FixData is "connectorId" or "connectorId:ownerId:connectorId"
        private static bool First(string data, out int connectorId)
        {
            connectorId = 0;

            if (string.IsNullOrEmpty(data))
                return false;

            string[] parts = data.Split(':');

            return int.TryParse(parts[0], out connectorId);
        }

        private static bool Other(
            string data, out long ownerId, out int connectorId)
        {
            ownerId = 0;
            connectorId = 0;

            if (string.IsNullOrEmpty(data))
                return false;

            string[] parts = data.Split(':');

            return parts.Length >= 3
                   && long.TryParse(parts[1], out ownerId)
                   && int.TryParse(parts[2], out connectorId);
        }
    }
}
