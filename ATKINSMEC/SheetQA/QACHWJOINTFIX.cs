using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Putting the CHW disconnections right.
    //
    // THE UNIT IS NEVER MOVED. A fan coil is where somebody put it, on
    // a ceiling grid, in a coordinated position; the pipework is the
    // thing that was drawn to it and missed. So every move here is a
    // move of PIPE.
    //
    // Each change is its own SubTransaction inside one Transaction -
    // what Revit refuses rolls back alone and the rest still go
    // through, which is QADUCTFIX's rule and the one that makes a
    // twenty-finding fix worth pressing.
    public static class QACHWJOINTFIX
    {
        public const string None = "";
        public const string Join = "chwjoin";
        public const string ToUnit = "chwunit";

        public class Result
        {
            public int Joined;
            public int Aligned;
            public int Refused;

            public List<string> Said = new List<string>();

            public void Note(string what)
            {
                if (!Said.Contains(what))
                    Said.Add(what);
            }
        }

        // ==================================================

        public static Result Fix(
            Document doc, List<QAFinding> findings, QASETTINGS settings)
        {
            var result = new Result();

            if (doc == null || findings == null)
                return result;

            QACHWLOG.Start("CHW disconnections: " + findings.Count +
                           " finding(s)");

            // THE CALLER OWNS THE TRANSACTION, exactly as QADUCTFIX
            // expects. QAEVENT opens one round this so the whole fix is
            // a single undo step; opening another here would be a
            // transaction inside a transaction, which Revit refuses.
            //
            // Joining two ends that already meet is cheaper and can
            // make a later alignment unnecessary, so it goes first -
            // the same order QADUCTFIX uses.
            foreach (QAFinding finding in findings)
            {
                if (finding.FixKind == Join)
                    One(doc, finding, result, settings);
            }

            foreach (QAFinding finding in findings)
            {
                if (finding.FixKind == ToUnit)
                    One(doc, finding, result, settings);
            }

            QACHWLOG.Head("Done");
            QACHWLOG.Say("   " + result.Joined + " joined, " +
                         result.Aligned + " aligned and joined, " +
                         result.Refused + " refused");

            QACHWLOG.Save();

            return result;
        }

        private static void One(
            Document doc,
            QAFinding finding,
            Result result,
            QASETTINGS settings)
        {
            using (var step = new SubTransaction(doc))
            {
                step.Start();

                try
                {
                    if (Mend(doc, finding, result, settings))
                    {
                        step.Commit();

                        return;
                    }
                }
                catch (Exception ex)
                {
                    QACHWLOG.Say("   threw: " + ex.GetType().Name +
                                 " - " + Flat(ex.Message));

                    result.Note(Flat(ex.Message));
                }

                step.RollBack();

                result.Refused++;
            }
        }

        // ==================================================

        private static bool Mend(
            Document doc,
            QAFinding finding,
            Result result,
            QASETTINGS settings)
        {
            int connectorId;

            if (!int.TryParse(finding.FixData, out connectorId))
                return false;

            Connector mine = QADUCTS.Lookup(
                doc, finding.ElementId, connectorId);

            if (mine == null)
            {
                result.Note("A pipe end has gone since the list was made.");

                return false;
            }

            QACHWLOG.Head(finding.Element + "  (" + finding.Problem + ")");

            // The scan is re-run rather than trusted: a list left open
            // while somebody moved a pipe must not apply a stale
            // correction. QAGRIDFIX makes the same call.
            QACHWJOINTS.LooseEnd end = Find(doc, finding, settings);

            if (end == null)
            {
                QACHWLOG.Say("   it is no longer an open end");

                result.Note(
                    "Some ends were already joined by the time the fix ran.");

                return false;
            }

            if (finding.FixKind == Join)
                return Meet(doc, mine, end, result);

            return Unit(doc, mine, end, result, settings);
        }

        // ---- two ends that meet ---------------------------------
        private static bool Meet(
            Document doc,
            Connector mine,
            QACHWJOINTS.LooseEnd end,
            Result result)
        {
            if (end.Meets == null)
                return false;

            Connector theirs = QADUCTS.Lookup(
                doc, end.Meets.OwnerId, end.Meets.ConnectorId);

            if (theirs == null)
                return false;

            XYZ to = Origin(theirs);

            if (to == null)
                return false;

            Connector moved = Align(doc, mine, to, result);

            if (moved == null)
                return false;

            // BOTH ends are read again. Moving one pipe can invalidate
            // the other end's connector too, and a dead one throws
            // rather than failing quietly.
            Connector target = QADUCTS.Lookup(
                doc, end.Meets.OwnerId, end.Meets.ConnectorId);

            if (target == null)
            {
                QACHWLOG.Say("   REFUSED: the other end has gone");

                return false;
            }

            if (target.IsConnected)
            {
                QACHWLOG.Say("   that end is taken now");

                return false;
            }

            moved.ConnectTo(target);

            QACHWLOG.Say("   joined to " + end.Meets.What);

            result.Joined++;

            return true;
        }

        // ---- a pipe that belongs on a unit ----------------------
        private static bool Unit(
            Document doc,
            Connector mine,
            QACHWJOINTS.LooseEnd end,
            Result result,
            QASETTINGS settings)
        {
            if (end.Unit == null)
                return false;

            Connector theirs = QADUCTS.Lookup(
                doc, end.Unit.EquipmentId, end.Unit.ConnectorId);

            if (theirs == null)
                return false;

            // THE SYSTEM IS CHECKED AGAIN, at the moment of joining.
            // The scan matched supply to supply, but a list can be
            // minutes old and a system can be reassigned in that time -
            // and joining flow to return is the one mistake here that
            // nothing downstream would catch.
            if (!QACHWSIDE.Match(
                    end.Side,
                    QACHWJOINTS.SideOf(theirs),
                    settings == null || settings.ChwJoinUnknownSystem))
            {
                QACHWLOG.Say("   REFUSED: the systems no longer match");

                result.Note(
                    "Some pipes no longer carry the same system as the " +
                    "connector they were matched to, and were left alone.");

                return false;
            }

            if (theirs.IsConnected)
            {
                QACHWLOG.Say("   that connector is taken now");

                return false;
            }

            XYZ to = Origin(theirs);

            if (to == null)
                return false;

            Connector moved = Align(doc, mine, to, result);

            if (moved == null)
                return false;

            // The unit's connector is read again as well - the pipe
            // moved, and anything taken before that is dead
            theirs = QADUCTS.Lookup(
                doc, end.Unit.EquipmentId, end.Unit.ConnectorId);

            if (theirs == null || theirs.IsConnected)
            {
                QACHWLOG.Say("   REFUSED: that connector is taken now");

                return false;
            }

            moved.ConnectTo(theirs);

            QACHWLOG.Say("   joined to " + end.Unit.Equipment);

            result.Aligned++;

            return true;
        }

        // ==================================================
        // Bringing the pipe to the connector
        // ==================================================

        // RETURNS THE CONNECTOR AS IT IS AFTERWARDS, and that is the
        // whole reason it is not a bool.
        //
        // A Connector belongs to the geometry it was read from. Rewrite
        // the pipe's LocationCurve, or move the pipe, and every
        // Connector taken before that is DEAD: using one throws "The
        // referenced object is not valid, possibly because it has been
        // deleted from the database, or its creation was undone."
        //
        // USER-REPORTED, and the log named it exactly. Four pipes read
        //
        //     stretched 662 mm onto the connector
        //     threw: InvalidObjectExceptionProxy - The referenced
        //     object is not valid ...
        //
        // - the move had already worked and the JOIN was what failed.
        // The ones that slid were worse: Origin() on a dead connector
        // is caught and returns null, so they gave up without a word.
        // Nothing was ever joined: 0 joined, 25 refused.
        //
        // So every geometry change below is followed by a fresh
        // Lookup, and the caller is handed the live connector to join
        // with rather than the one it passed in.
        private static Connector Align(
            Document doc, Connector mine, XYZ to, Result result)
        {
            long pipeId = mine.Owner.Id.ToValue();
            int myId = mine.Id;

            var pipe = doc.GetElement(mine.Owner.Id) as MEPCurve;

            if (pipe == null)
            {
                // A fitting cannot be stretched; it has to be where its
                // pipes put it
                return Near(mine, to) ? mine : null;
            }

            LocationCurve located = pipe.Location as LocationCurve;

            Line line = located == null ? null : located.Curve as Line;

            if (line == null)
                return Near(mine, to) ? mine : null;

            XYZ here = Origin(mine);

            if (here == null)
                return null;

            XYZ a = line.GetEndPoint(0);
            XYZ b = line.GetEndPoint(1);

            bool movingStart = EQUIPLINE.MovingIsStart(
                a.DistanceTo(here), b.DistanceTo(here));

            XYZ far = movingStart ? b : a;

            // Re-read after a slide, which moves the pipe bodily
            XYZ was = here;

            bool farEndFree = FreeAt(pipe, far);

            QACHWALIGN.Plan plan = QACHWALIGN.Make(
                Vec(here, to),
                Vec(far, here),
                Neighbour(doc, pipe, far),
                farEndFree,
                QACHWALIGN.Touching);

            QACHWLOG.Say("   " + plan.Why);

            if (plan.What == QACHWALIGN.Kind.Meet)
                return mine;

            if (plan.What == QACHWALIGN.Kind.Refuse)
            {
                QACHWLOG.Say("   REFUSED: " + plan.Why + ", " +
                             QAGRIDS.FromFeet(plan.LeftOver)
                                 .ToString("N0") + " mm left over");

                // WHY it could not move matters more than that it
                // could not. A pipe loose at its far end would simply
                // have been moved; one that is held can only be moved
                // by dragging whatever holds it, and naming that is
                // what turns a refusal into something actionable.
                QACHWLOG.Say("      its far end is " +
                             (farEndFree
                                  ? "FREE, so this should not happen"
                                  : "joined to " +
                                    QAEQUIP.Describe(
                                        Beyond(doc, pipe, far))));

                QACHWLOG.Say("      the pipe runs " + Told(Vec(far, here)));
                QACHWLOG.Say("      the connector is " + Told(Vec(here, to)) +
                             " from its end");

                result.Note(
                    "Some pipes are offset in a direction neither they " +
                    "nor the pipe above them runs in. Moving those would " +
                    "bend the pipework, so they were left for somebody " +
                    "to move by hand.");

                return null;
            }

            // A SHIFT moves the whole pipe onto the connector's line -
            // to its elevation, usually. Nothing is joined to its far
            // end, so nothing else is disturbed and no joint has to
            // survive the move. User-reported: pipes drawn at the wrong
            // height beside the unit they serve, which no amount of
            // stretching along their own axis can reach.
            if (plan.What == QACHWALIGN.Kind.Shift)
            {
                var by = new XYZ(
                    plan.Across.X, plan.Across.Y, plan.Across.Z);

                QACHWLOG.Say(
                    "   moving the whole pipe " +
                    QAGRIDS.FromFeet(by.GetLength()).ToString("N0") +
                    " mm onto the connector's line");

                ElementTransformUtils.MoveElement(
                    doc, pipe.Id, by);

                doc.Regenerate();

                // IT HAS MOVED, SO THE CONNECTOR IS DEAD. Read it back
                // out of the model rather than reusing the object.
                mine = QADUCTS.Lookup(doc, pipeId, myId);

                if (mine == null)
                    return null;

                here = Origin(mine);

                located = pipe.Location as LocationCurve;

                line = located == null ? null : located.Curve as Line;

                if (here == null || line == null)
                    return null;

                a = line.GetEndPoint(0);
                b = line.GetEndPoint(1);

                movingStart = EQUIPLINE.MovingIsStart(
                    a.DistanceTo(here), b.DistanceTo(here));

                far = movingStart ? b : a;
            }

            // A SLIDE moves the fitting above and the pipe together,
            // sideways, and lets the pipe BEYOND that fitting give up
            // the length. This is the case in the picture that prompted
            // the page: a drop offset from the connector it serves.
            if (plan.What == QACHWALIGN.Kind.Slide)
            {
                if (!Slide(doc, pipe, far, plan, result))
                    return null;

                // Sliding moved the pipe, so the connector AND the
                // LocationCurve are both dead - the old code re-read
                // the curve and kept the connector, which is exactly
                // the half that silently failed.
                mine = QADUCTS.Lookup(doc, pipeId, myId);

                if (mine == null)
                    return null;

                here = Origin(mine);

                located = pipe.Location as LocationCurve;

                line = located == null ? null : located.Curve as Line;

                if (here == null || line == null)
                    return null;

                a = line.GetEndPoint(0);
                b = line.GetEndPoint(1);

                movingStart = EQUIPLINE.MovingIsStart(
                    a.DistanceTo(here), b.DistanceTo(here));

                far = movingStart ? b : a;
            }

            if (far.DistanceTo(to) < doc.Application.ShortCurveTolerance)
            {
                QACHWLOG.Say("   REFUSED: it would leave the pipe too " +
                             "short to exist");

                result.Note(
                    "Some pipes would have been left too short to exist, " +
                    "which Revit deletes, so they were left alone.");

                return null;
            }

            // KEEPING THE LINE'S DIRECTION IS NOT COSMETIC. Building it
            // far-end-first reverses the pipe, and Revit throws the
            // whole transaction out with "modified to be in the
            // opposite direction causing the connections to be
            // invalid" - see EQUIPLINE.
            located.Curve = movingStart
                ? Line.CreateBound(to, b)
                : Line.CreateBound(a, to);

            QACHWLOG.Say("   stretched " +
                         QAGRIDS.FromFeet(Math.Abs(plan.AlongPipe))
                             .ToString("N0") + " mm onto the connector");

            doc.Regenerate();

            // The stretch rewrote the curve, so the connector the
            // caller is about to join with has to be read again
            return QADUCTS.Lookup(doc, pipeId, myId);
        }

        // Is the pipe loose at the end we are NOT moving?
        private static bool FreeAt(MEPCurve pipe, XYZ far)
        {
            Connector end = EndAt(pipe, far);

            if (end == null)
                return false;

            try
            {
                return !end.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        // Move the fitting above this pipe, and the pipe with it,
        // sideways along the pipe BEYOND that fitting.
        //
        // THE UNIT NEVER MOVES - only pipework does, and only the
        // pipework between this pipe and the first thing that can
        // absorb the shift by changing length.
        //
        // The same five steps QACHWROOM.Make uses, and for the same
        // reason: a joint that is asked to survive a move it cannot is
        // a joint Revit throws out. Unhook, move, restretch, hook back
        // up, then check the model rather than the arithmetic.
        private static bool Slide(
            Document doc,
            MEPCurve pipe,
            XYZ far,
            QACHWALIGN.Plan plan,
            Result result)
        {
            Element fitting = Beyond(doc, pipe, far);

            if (fitting == null)
            {
                QACHWLOG.Say("   REFUSED: nothing above it to slide");

                return false;
            }

            Element next = Past(doc, fitting, pipe);

            var giver = next as MEPCurve;

            if (giver == null)
            {
                QACHWLOG.Say(
                    "   REFUSED: the fitting above has no pipe beyond it " +
                    "to give up the length");

                result.Note(
                    "Some pipes need the fitting above them to slide, and " +
                    "there is nothing beyond that fitting to absorb it.");

                return false;
            }

            // Which way, and how far
            var line = (giver.Location as LocationCurve) == null
                ? null
                : (giver.Location as LocationCurve).Curve as Line;

            if (line == null)
                return false;

            XYZ along = Vector(
                line.GetEndPoint(0), line.GetEndPoint(1)).Normalize();

            XYZ shift = along.Multiply(plan.AlongNeighbour);

            QACHWLOG.Say(
                "   sliding the fitting above " +
                QAGRIDS.FromFeet(Math.Abs(plan.AlongNeighbour))
                    .ToString("N0") + " mm along " +
                QAEQUIP.Describe(giver));

            // ---- 1. what is joined to what, before anything moves ---
            Connector mine = EndAt(fitting, far);

            Connector theirs = mine == null ? null : Joined(mine, giver);

            if (mine == null || theirs == null)
            {
                QACHWLOG.Say("   REFUSED: could not read that joint");

                return false;
            }

            int mineId = mine.Id;
            int theirsId = theirs.Id;

            long fittingId = fitting.Id.ToValue();
            long giverId = giver.Id.ToValue();

            XYZ giverEnd = Origin(theirs);

            if (giverEnd == null)
                return false;

            // ---- 2. unhook, so Revit is not asked to keep it --------
            mine.DisconnectFrom(theirs);

            // ---- 3. move the fitting and this pipe together ---------
            var moving = new List<ElementId>
            {
                REVITCOMPAT.NewId(fittingId),
                REVITCOMPAT.NewId(pipe.Id.ToValue())
            };

            ElementTransformUtils.MoveElements(doc, moving, shift);

            doc.Regenerate();

            // ---- 4. the pipe beyond gives up the length -------------
            //
            // Its far end stays exactly where it was; only the end that
            // met the fitting follows. Keeping the line's DIRECTION is
            // not cosmetic - see EQUIPLINE.
            var located = giver.Location as LocationCurve;

            var now = located == null ? null : located.Curve as Line;

            if (now == null)
                return false;

            XYZ start = now.GetEndPoint(0);
            XYZ finish = now.GetEndPoint(1);

            XYZ moved = giverEnd.Add(shift);

            bool movingStart = EQUIPLINE.MovingIsStart(
                start.DistanceTo(giverEnd), finish.DistanceTo(giverEnd));

            XYZ anchor = movingStart ? finish : start;

            if (anchor.DistanceTo(moved) <
                doc.Application.ShortCurveTolerance)
            {
                QACHWLOG.Say(
                    "   REFUSED: it would leave " + QAEQUIP.Describe(giver) +
                    " too short to exist");

                result.Note(
                    "Some pipes could not be slid because the pipe above " +
                    "them would have been left too short to exist.");

                return false;
            }

            located.Curve = movingStart
                ? Line.CreateBound(moved, finish)
                : Line.CreateBound(start, moved);

            doc.Regenerate();

            // ---- 5. hook it back up ---------------------------------
            Connector again = QADUCTS.Lookup(doc, fittingId, mineId);
            Connector theirsAgain = QADUCTS.Lookup(doc, giverId, theirsId);

            if (again == null || theirsAgain == null)
                return false;

            if (!again.IsConnectedTo(theirsAgain))
                again.ConnectTo(theirsAgain);

            doc.Regenerate();

            return true;
        }

        // The fitting on the far end of this pipe
        // INSULATION IS IN AllRefs, AND PipeInsulation DERIVES FROM
        // MEPCurve. Both halves of that matter, and together they are
        // why this page joined nothing at all in a real model.
        //
        // A walk that takes the first owner it finds gets the sleeve
        // rather than the fitting, and the usual "as MEPCurve" guard
        // does not catch it - `MEPCurve curve = insulation;` COMPILES,
        // verified on 2022. So the slide measured itself against a
        // sleeve of fibreglass instead of the pipe inside it, and the
        // joint it then looked for did not exist.
        //
        // The log said so in as many words: "its far end is joined to
        // Pipe Insulations - Fiberglass", and "sliding the fitting
        // above 33 mm along Pipe Insulations - Fiberglass".
        //
        // Asked by CATEGORY, which no class hierarchy can confuse.
        private static bool Pipework(Element element)
        {
            if (element == null || element.Category == null)
                return false;

            long id = element.Category.Id.ToValue();

            return id == (long)BuiltInCategory.OST_PipeCurves
                || id == (long)BuiltInCategory.OST_FlexPipeCurves
                || id == (long)BuiltInCategory.OST_PipeFitting
                || id == (long)BuiltInCategory.OST_PipeAccessory;
        }

        private static Element Beyond(
            Document doc, MEPCurve pipe, XYZ far)
        {
            foreach (Connector end in QAEQUIP.PipeEnds(pipe))
            {
                XYZ origin = Origin(end);

                if (origin == null || origin.DistanceTo(far) > 1e-6)
                    continue;

                try
                {
                    foreach (Connector other in end.AllRefs)
                    {
                        if (other.Owner == null)
                            continue;

                        long id = other.Owner.Id.ToValue();

                        if (id == pipe.Id.ToValue())
                            continue;

                        Element found = doc.GetElement(other.Owner.Id);

                        if (!Pipework(found))
                            continue;

                        return found;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        // The element on the far side of a fitting, not counting the
        // pipe we came from. Named Past rather than Through because
        // Neighbour() already has a Through that answers a different
        // question - which way that element RUNS.
        private static Element Past(
            Document doc, Element fitting, MEPCurve notThis)
        {
            try
            {
                foreach (Connector end in QAEQUIP.PipeEnds(fitting))
                {
                    foreach (Connector other in end.AllRefs)
                    {
                        if (other.Owner == null)
                            continue;

                        long id = other.Owner.Id.ToValue();

                        if (id == notThis.Id.ToValue() ||
                            id == fitting.Id.ToValue())
                            continue;

                        Element found = doc.GetElement(other.Owner.Id);

                        if (!Pipework(found))
                            continue;

                        return found;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static Connector EndAt(Element element, XYZ point)
        {
            foreach (Connector end in QAEQUIP.PipeEnds(element))
            {
                XYZ origin = Origin(end);

                if (origin != null && origin.DistanceTo(point) < 1e-6)
                    return end;
            }

            return null;
        }

        private static Connector Joined(Connector mine, Element to)
        {
            try
            {
                foreach (Connector other in mine.AllRefs)
                {
                    if (other.Owner != null &&
                        other.Owner.Id.ToValue() == to.Id.ToValue())
                        return other;
                }
            }
            catch
            {
            }

            return null;
        }

        private static XYZ Vector(XYZ from, XYZ to)
        {
            return new XYZ(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
        }

        // The pipe the fitting on the far end sits on, which is what a
        // sideways offset would have to come from
        private static QAVEC Neighbour(
            Document doc, MEPCurve pipe, XYZ far)
        {
            try
            {
                foreach (Connector end in QAEQUIP.PipeEnds(pipe))
                {
                    XYZ origin = Origin(end);

                    if (origin == null || origin.DistanceTo(far) > 1e-6)
                        continue;

                    foreach (Connector other in end.AllRefs)
                    {
                        if (other.Owner == null ||
                            other.Owner.Id.ToValue() == pipe.Id.ToValue())
                            continue;

                        Element fitting = doc.GetElement(other.Owner.Id);

                        if (!Pipework(fitting))
                            continue;

                        QAVEC found = Through(doc, fitting, pipe);

                        if (found.Length > 0.5)
                            return found;
                    }
                }
            }
            catch
            {
            }

            return new QAVEC(0, 0, 0);
        }

        private static QAVEC Through(
            Document doc, Element fitting, MEPCurve notThis)
        {
            try
            {
                foreach (Connector end in QAEQUIP.PipeEnds(fitting))
                {
                    foreach (Connector other in end.AllRefs)
                    {
                        if (other.Owner == null)
                            continue;

                        if (other.Owner.Id.ToValue() ==
                            notThis.Id.ToValue())
                            continue;

                        if (other.Owner.Id.ToValue() ==
                            fitting.Id.ToValue())
                            continue;

                        Element onward = doc.GetElement(other.Owner.Id);

                        // The cast alone is NOT a guard here - see
                        // Pipework. Insulation passes it.
                        if (!Pipework(onward))
                            continue;

                        var next = onward as MEPCurve;

                        if (next == null)
                            continue;

                        var line = (next.Location as LocationCurve)
                            ?.Curve as Line;

                        if (line == null)
                            continue;

                        return Vec(line.GetEndPoint(0), line.GetEndPoint(1));
                    }
                }
            }
            catch
            {
            }

            return new QAVEC(0, 0, 0);
        }

        // ==================================================

        private static QACHWJOINTS.LooseEnd Find(
            Document doc, QAFinding finding, QASETTINGS settings)
        {
            QACHWJOINTS.Scan scan = QACHWJOINTS.Of(doc, null, settings);

            foreach (QACHWJOINTS.LooseEnd end in scan.Ends)
            {
                if (end.OwnerId == finding.ElementId &&
                    end.ConnectorId.ToString() == finding.FixData)
                    return end;
            }

            return null;
        }

        private static bool Near(Connector mine, XYZ to)
        {
            XYZ here = Origin(mine);

            return here != null && to != null &&
                   here.DistanceTo(to) <= QACHWALIGN.Touching;
        }

        private static XYZ Origin(Connector connector)
        {
            try
            {
                return connector.Origin;
            }
            catch
            {
                return null;
            }
        }

        private static QAVEC Vec(XYZ from, XYZ to)
        {
            if (from == null || to == null)
                return new QAVEC(0, 0, 0);

            return new QAVEC(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
        }

        // A direction in millimetres, so a log can be read against a
        // drawing rather than against feet
        private static string Told(QAVEC vector)
        {
            return "(" +
                   QAGRIDS.FromFeet(vector.X).ToString("N0") + ", " +
                   QAGRIDS.FromFeet(vector.Y).ToString("N0") + ", " +
                   QAGRIDS.FromFeet(vector.Z).ToString("N0") + ") mm";
        }

        private static string Flat(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
