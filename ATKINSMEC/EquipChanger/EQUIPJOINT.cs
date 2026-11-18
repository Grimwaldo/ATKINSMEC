using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // One joint between a piece of equipment and the duct or pipe on
    // it, written down so it can be taken apart and put back.
    //
    // A Connector cannot be held across a change - the same reason
    // QADUCTFIX carries a connector ID on its findings rather than the
    // connector. So this keeps ELEMENT IDS AND INDICES and looks the
    // connectors up again each time.
    public class EQUIPJOINT
    {
        // The equipment, and which of its connectors this is. MyIndex is
        // the connector's own id, which is what a lookup needs.
        public long OwnerId { get; set; }

        public int MyIndex { get; set; }

        // What a report calls it: "duct connector 2", "pipe connector 1"
        public string Label { get; set; }

        // Supply Air, SupplyHydronic... empty when it does not say
        public string SystemName { get; set; }

        // As numbers, for EQUIPMATCH
        public int Domain { get; set; }

        public int System { get; set; }

        public bool Pipe { get; set; }

        // What it was joined to
        public long OtherId { get; set; }

        public int OtherIndex { get; set; }

        public string OtherName { get; set; }

        // Where the equipment's connector was before the type changed,
        // and which way it faced, so the duct can be brought to where it
        // has moved to - and refused if it turned
        public XYZ Was { get; set; }

        public XYZ Facing { get; set; }

        // "pipe connector 1 (SupplyHydronic)"
        public string Called
        {
            get
            {
                return string.IsNullOrEmpty(SystemName)
                    ? Label
                    : Label + " (" + SystemName + ")";
            }
        }

        public EQUIPPORT Port
        {
            get
            {
                return new EQUIPPORT
                {
                    Id = MyIndex,
                    Domain = Domain,
                    System = System,
                    At = EQUIPWALK.V(Was)
                };
            }
        }

        public static EQUIPJOINT Of(Connector end, string label)
        {
            if (end == null)
                return null;

            try
            {
                if (!end.IsConnected)
                    return null;

                foreach (Connector other in end.AllRefs)
                {
                    if (other.Owner == null)
                        continue;

                    if (other.Owner.Id.ToValue() ==
                        end.Owner.Id.ToValue())
                        continue;

                    if (other.ConnectorType != ConnectorType.End)
                        continue;

                    // INSULATION IS IN AllRefs, and the cast in Bring()
                    // below is not a guard against it - DuctInsulation
                    // IS an MEPCurve, and so are DuctLining and
                    // PipeInsulation. Compile verified. So the sleeve
                    // has to be turned away HERE, by category; see
                    // EQUIPWALK.Ductwork / Pipework.
                    //
                    // Getting it wrong costs more here than in the
                    // walk. Undo() would unhook the SLEEVE and leave
                    // the real duct joined, so the type swap raises the
                    // very "cannot keep the connectivity" error these
                    // five steps exist to prevent - and Redo() would
                    // then rewrite the insulation's LocationCurve,
                    // moving a sleeve of fibreglass while the duct it
                    // wraps stayed where it was.
                    if (!EQUIPWALK.Network(end, other.Owner))
                        continue;

                    bool pipe = end.Domain == Autodesk.Revit.DB.Domain.DomainPiping;

                    return new EQUIPJOINT
                    {
                        OwnerId = end.Owner.Id.ToValue(),
                        MyIndex = end.Id,
                        Label = label,
                        SystemName = EQUIPWALK.SystemName(end),
                        Domain = (int)end.Domain,
                        System = EQUIPWALK.SystemOf(end),
                        Pipe = pipe,
                        OtherId = other.Owner.Id.ToValue(),
                        OtherIndex = other.Id,
                        OtherName = EQUIPWALK.Describe(other.Owner) + " " +
                                    other.Owner.Id.ToValue(),
                        Was = end.Origin,
                        Facing = EQUIPWALK.Facing(end)
                    };
                }
            }
            catch
            {
            }

            return null;
        }

        // Take it apart. Deliberate, and the whole reason the type swap
        // does not raise "cannot keep the connectivity".
        public bool Undo(Document doc)
        {
            Connector mine = Mine(doc);
            Connector theirs = Theirs(doc);

            if (mine == null || theirs == null)
                return false;

            try
            {
                if (mine.IsConnectedTo(theirs))
                {
                    mine.DisconnectFrom(theirs);

                    EQUIPLOG.Say("   unhooked " + Called);
                }

                return true;
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("unhooking " + Called, ex);

                return false;
            }
        }

        // Put it back onto the new type's connector `nowIndex`. The new
        // connector may not be where the old one was, so the DUCT or
        // PIPE is brought to it.
        //
        // `carry`: when it cannot be brought straight, move the run or
        // fitting on it - and whatever hangs off that - over to the new
        // connector, as far as a straight run lying the way it moves,
        // which stretches to follow (Carry). Off on the second try, when
        // Revit refused the first at the commit.
        //
        // Returns null when it is joined again, else why it was LEFT
        // OPEN. Never a refusal of the whole change: a duct or pipe
        // that cannot reach the new connector is a job for a person,
        // and the window says which one.
        public string Redo(Document doc, int nowIndex, bool carry, List<string> notes)
        {
            Connector mine = QADUCTS.Lookup(doc, OwnerId, nowIndex);
            Connector theirs = Theirs(doc);

            if (mine == null)
                return "the new type has no connection for it";

            if (theirs == null)
                return "the " + Noun + " it was on could not be found again";

            try
            {
                if (mine.IsConnectedTo(theirs))
                    return null;

                bool joined;

                string why = Bring(doc, theirs, mine, carry, notes, out joined);

                if (why != null)
                {
                    EQUIPLOG.Say("   " + Called + " LEFT OPEN: " + why);

                    return why;
                }

                // An offset was built between them, and joined at both
                // ends: the unit is NOT joined straight to the pipe
                if (joined)
                    return null;

                // A Connector dies with the geometry it was read from,
                // and Bring has just moved that geometry
                mine = QADUCTS.Lookup(doc, OwnerId, nowIndex);
                theirs = Theirs(doc);

                if (mine == null || theirs == null)
                    return "the " + Noun + " could not be found again after moving it";

                mine.ConnectTo(theirs);

                EQUIPLOG.Say("   hooked " + Called + " back up");

                return null;
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("hooking " + Called + " back up", ex);

                return "it would not join back up: " + EQUIPCHANGE.Short(ex);
            }
        }

        private string Noun
        {
            get { return Pipe ? "pipe" : "duct"; }
        }

        // The least a run may be left with
        public static double Shortest(Document doc)
        {
            double tolerance = 0.0;

            try
            {
                tolerance = doc.Application.ShortCurveTolerance;
            }
            catch
            {
            }

            return Math.Max(tolerance, 5.0 / 304.8);
        }

        // Bring the duct or pipe on `end` to `mine`, or say why not.
        //
        // NEVER BACKWARDS, NEVER BENT. Moving one end of a straight run
        // to an arbitrary point is only safe when the point is on the
        // run's own line and short of its far end: past the far end the
        // run turns inside out ("modified to be in the opposite
        // direction"), off the line it turns, and the fitting on its far
        // end "can no longer keep the connectivity". Either is posted at
        // the commit and throws the whole unit out. EQUIPREACH decides.
        private string Bring(
            Document doc, Connector end, Connector mine, bool carry,
            List<string> notes, out bool joined)
        {
            joined = false;

            // By id: a carry that is tried and rolled back leaves these
            // Connector objects dead (dry run, 2026-11-18: "the connectors
            // will not say which way they face")
            long endOwner = end.Owner.Id.ToValue();
            int endId = end.Id;
            int mineId = mine.Id;

            XYZ to = mine.Origin;
            XYZ here = end.Origin;

            if (to == null || here == null)
                return "its connector would not say where it is";

            XYZ facing = EQUIPWALK.Facing(mine);

            Element owner = doc.GetElement(end.Owner.Id);

            var curve = owner as MEPCurve;

            if (curve == null)
            {
                // A fitting or an accessory straight on the unit (FCU-10's
                // CHW elbows, 2026-11-18). Asked as a free run, which is
                // There, Open for a turn, or Shift for anything that
                // would need it moved.
                EQUIPREACH still = EQUIPREACH.Decide(
                    EQUIPWALK.V(here), EQUIPWALK.V(here), EQUIPWALK.V(to),
                    Facing == null ? (EQUIPV?)null : EQUIPWALK.V(Facing),
                    facing == null ? (EQUIPV?)null : EQUIPWALK.V(facing),
                    true, 0.0, Noun);

                EQUIPLOG.Say("   " + Called + ": the connection " +
                             Moved(to - here) + "; " + OtherName +
                             " sits on it");

                if (still.Kind == EQUIPREACHKIND.There)
                    return null;

                if (still.Kind == EQUIPREACHKIND.Open)
                    return still.Why;

                string stuck = OtherName + " sits straight on it, and the " +
                               "new connection is " +
                               EQUIPREACH.Mm(still.Moved) + " mm away";

                if (!carry)
                    return stuck;

                string blocked = Carry(doc, owner, end.Id, to - here, notes);

                if (blocked == null)
                    return null;

                string stepped = Pipe ? Offset(doc, endOwner, endId, mineId, notes) : "";

                if (stepped == null)
                {
                    joined = true;

                    return null;
                }

                return stuck + "; carrying it over " + blocked +
                       (Pipe ? "; an offset: " + stepped : "");
            }

            var located = curve.Location as LocationCurve;

            var line = located == null ? null : located.Curve as Line;

            if (line == null)
            {
                // Flex: it bends, so joining it is what brings its end
                // over
                EQUIPLOG.Say("   " + Called + ": flexible, joined where " +
                             "the new connector is");

                return null;
            }

            XYZ a = line.GetEndPoint(0);
            XYZ b = line.GetEndPoint(1);

            // Whichever end of the run this connector is
            bool movingStart = EQUIPLINE.MovingIsStart(
                a.DistanceTo(here), b.DistanceTo(here));

            XYZ far = movingStart ? b : a;

            EQUIPREACH reach = EQUIPREACH.Decide(
                EQUIPWALK.V(here),
                EQUIPWALK.V(far),
                EQUIPWALK.V(to),
                Facing == null ? (EQUIPV?)null : EQUIPWALK.V(Facing),
                facing == null ? (EQUIPV?)null : EQUIPWALK.V(facing),
                FreeBeyond(curve, end.Id),
                Shortest(doc),
                Noun);

            EQUIPLOG.Say("   " + Called + ": the connection " +
                         reach.Described + "; " + Noun + " " +
                         curve.Id.ToValue() + " is " +
                         EQUIPREACH.Mm(reach.Length) + " mm");

            switch (reach.Kind)
            {
                case EQUIPREACHKIND.There:
                    return null;

                case EQUIPREACHKIND.Open:
                {
                    string stuck = reach.Why + " (" + Noun + " " +
                                   curve.Id.ToValue() + ")";

                    if (!carry || reach.Turned)
                        return stuck;

                    // The run itself moves over whole, and what is on
                    // its far end after it
                    string blocked = Carry(doc, curve, end.Id, to - here, notes);

                    if (blocked == null)
                        return null;

                    string stepped = Pipe ? Offset(doc, endOwner, endId, mineId, notes) : "";

                    if (stepped == null)
                    {
                        joined = true;

                        return null;
                    }

                    return stuck + "; carrying it over " + blocked +
                           (Pipe ? "; an offset: " + stepped : "");
                }

                case EQUIPREACHKIND.Shift:
                    ElementTransformUtils.MoveElement(
                        doc, curve.Id, to - here);

                    EQUIPLOG.Say("   moved the whole " + Noun +
                                 " - nothing on its far end");

                    return null;
            }

            // KEEPING THE ORIGINAL DIRECTION IS NOT COSMETIC - see
            // EQUIPLINE. Writing (far, to) reverses the curve whenever
            // the moving end was endpoint 0, and Revit throws the whole
            // transaction out at the commit.
            located.Curve = movingStart
                ? Line.CreateBound(to, b)
                : Line.CreateBound(a, to);

            EQUIPLOG.Say("   moved a " + Noun + " end to meet the connector " +
                         (movingStart ? "(its start)" : "(its finish)"));

            return null;
        }

        // How many pieces a carry may move before it is not a carry any
        // more: an elbow, a valve or two, the drop to the main
        public const int CarryLimit = 12;

        // CARRY the pipework over to the new connector: move `first` -
        // the fitting or run that was on the unit - by `move`, and with
        // it everything joined beyond it, out along every way until each
        // meets a STRAIGHT RUN LYING THE WAY IT MOVES, which is
        // stretched or shortened to follow (EQUIPREACH says whether it
        // can without turning inside out). An open end needs nothing.
        //
        // User, 2026-11-18: FCU-10's CHW connections came out 644 and
        // 653 mm from the elbows sitting on FCU-02's. By hand: drag the
        // elbows over, the pipes behind them follow.
        //
        // A SLANTED MOVE IS DONE IN PARTS. Once the unit had moved onto
        // its duct the elbows were 322 mm out AND 55 mm down from their
        // connections; no straight pipe lies along that, so one rigid
        // move walked the whole CHW network and gave up (same day). So:
        // the whole move at once, else split along the unit's own sides,
        // else along the model's axes - each part taken up by the run
        // lying its way (the drop takes the 55, the branch the 322).
        // Each try is a SubTransaction: a part that fails takes the
        // parts before it back with it.
        //
        // Refused - and the joint left open, saying why - when it would
        // move another unit or terminal, slide something tapped into a
        // main, move a pinned element, or more than CarryLimit pieces.
        // Returns null when it moved them.
        private string Carry(
            Document doc, Element first, int cameIn, XYZ move,
            List<string> notes)
        {
            long firstId = first.Id.ToValue();

            var tries = new List<KeyValuePair<string, List<XYZ>>>();

            tries.Add(new KeyValuePair<string, List<XYZ>>(
                "in one move", new List<XYZ> { move }));

            XYZ[] sides = UnitAxes(doc);

            if (sides != null)
            {
                tries.Add(new KeyValuePair<string, List<XYZ>>(
                    "along the unit's sides", Split(move, sides)));
            }

            tries.Add(new KeyValuePair<string, List<XYZ>>(
                "along the model's axes",
                Split(move, new[] { XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ })));

            string told = null;

            var done = new List<List<XYZ>>();

            foreach (KeyValuePair<string, List<XYZ>> attempt in tries)
            {
                if (attempt.Value.Count == 0 || Repeats(done, attempt.Value))
                    continue;

                done.Add(attempt.Value);

                var moved = new HashSet<long>();
                var stretched = new List<string>();

                string why = null;

                using (var step = new SubTransaction(doc))
                {
                    step.Start();

                    try
                    {
                        foreach (XYZ part in attempt.Value)
                        {
                            why = CarryOnce(doc, firstId, cameIn, part,
                                moved, stretched);

                            if (why != null)
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        EQUIPLOG.Blew("carrying " + Called + " over", ex);

                        why = "failed: " + EQUIPCHANGE.Short(ex);
                    }

                    if (why != null)
                    {
                        step.RollBack();

                        EQUIPLOG.Say("   carrying " + Called + " " +
                                     attempt.Key + ": " + why);

                        // The unit's sides are the likeliest way round,
                        // so theirs is the reason given
                        if (told == null || attempt.Key.StartsWith("along the unit"))
                            told = why;

                        continue;
                    }

                    step.Commit();
                }

                string said = "carried " + moved.Count + " piece(s) of " +
                              Noun + "work " +
                              EQUIPREACH.Mm(move.GetLength()) + " mm to " +
                              Called + " (" + attempt.Key + ")";

                EQUIPLOG.Say("   " + said +
                             (stretched.Count == 0
                                 ? string.Empty
                                 : "; stretched " +
                                   string.Join(", ", stretched.ToArray())));

                var ids = new List<string>();

                foreach (long id in moved)
                    ids.Add(id.ToString());

                EQUIPLOG.Say("      moved: " + string.Join(", ", ids.ToArray()));

                if (notes != null)
                    notes.Add(said);

                return null;
            }

            return told ?? "nothing could be moved";
        }

        // One rigid move `move` of `firstId` and what is beyond it: plan
        // the walk, then unhook the runs that take it up, move the rest,
        // stretch those runs' ends along their own lines to meet, and
        // join them again. Not left to Revit's dragging: that turns a
        // run whenever the move is not along it.
        private string CarryOnce(
            Document doc, long firstId, int cameIn, XYZ move,
            HashSet<long> moved, List<string> stretched)
        {
            double shortest = Shortest(doc);

            Element first = doc.GetElement(REVITCOMPAT.NewId(firstId));

            if (first == null)
                return "what was on it could not be found again";

            if (first.Pinned)
                return "it is pinned";

            var moving = new List<ElementId> { first.Id };

            var seen = new HashSet<long> { firstId };

            // The runs that take the move up: run id -> its joint to the
            // piece that moves
            var takers = new Dictionary<long, Taker>();

            var queue = new Queue<KeyValuePair<Element, int>>();

            queue.Enqueue(new KeyValuePair<Element, int>(first, cameIn));

            var walked = new List<string>();

            while (queue.Count > 0)
            {
                KeyValuePair<Element, int> item = queue.Dequeue();

                ConnectorManager manager = QADUCTS.Manager(item.Key);

                if (manager == null)
                    return "the connectors of " + EQUIPWALK.Describe(item.Key) +
                           " could not be read";

                foreach (Connector connector in manager.Connectors)
                {
                    if (connector.Id == item.Value)
                        continue;

                    if (connector.Domain != Autodesk.Revit.DB.Domain.DomainHvac &&
                        connector.Domain != Autodesk.Revit.DB.Domain.DomainPiping)
                        continue;

                    foreach (Connector other in connector.AllRefs)
                    {
                        Element next = other.Owner;

                        if (next == null)
                            continue;

                        long id = next.Id.ToValue();

                        if (id == item.Key.Id.ToValue() || id == OwnerId)
                            continue;

                        // Sleeves and anything that is not pipe or duct
                        // network, by category
                        if (!EQUIPWALK.Ductwork(next) && !EQUIPWALK.Pipework(next))
                            continue;

                        if (seen.Contains(id))
                        {
                            // Reached again from another moving piece: a
                            // run with BOTH ends moving moves whole
                            Taker both;

                            if (takers.TryGetValue(id, out both))
                            {
                                takers.Remove(id);

                                moving.Add(next.Id);

                                walked.Add(EQUIPWALK.Describe(next) + " " + id +
                                           " moves (both ends)");
                            }

                            continue;
                        }

                        seen.Add(id);

                        string named = EQUIPWALK.Describe(next) + " " + id;

                        if (Fixed(next))
                            return Trail(walked, "would move " + named);

                        if (next.Pinned)
                            return Trail(walked, "would move " + named +
                                                 ", which is pinned");

                        var run = next as MEPCurve;

                        if (run == null)
                        {
                            moving.Add(next.Id);

                            walked.Add(named + " moves");

                            queue.Enqueue(new KeyValuePair<Element, int>(next, other.Id));
                        }
                        else
                        {
                            if (other.ConnectorType != ConnectorType.End)
                                return Trail(walked, "would slide something " +
                                                     "tapped into " + named);

                            var located = run.Location as LocationCurve;

                            var line = located == null ? null : located.Curve as Line;

                            if (line == null)
                            {
                                // Flex bends to follow: left joined, Revit
                                // brings its end along
                                stretched.Add(named);

                                walked.Add(named + " bends");

                                continue;
                            }

                            XYZ here = other.Origin;

                            XYZ a = line.GetEndPoint(0);
                            XYZ b = line.GetEndPoint(1);

                            bool nearIsStart = a.DistanceTo(here) <= b.DistanceTo(here);

                            XYZ far = nearIsStart ? b : a;

                            bool free = FreeBeyond(run, other.Id);

                            EQUIPREACH takes = EQUIPREACH.Decide(
                                EQUIPWALK.V(here), EQUIPWALK.V(far),
                                EQUIPWALK.V(here + move), null, null,
                                free, shortest, Noun);

                            if (takes.Kind == EQUIPREACHKIND.Stretch ||
                                takes.Kind == EQUIPREACHKIND.There)
                            {
                                takers[id] = new Taker
                                {
                                    RunId = id,
                                    RunEnd = other.Id,
                                    PieceId = item.Key.Id.ToValue(),
                                    PieceEnd = connector.Id,
                                    NearIsStart = nearIsStart,
                                    Named = named + " " +
                                            EQUIPREACH.Mm(takes.Length) + " -> " +
                                            EQUIPREACH.Mm(takes.Left) + " mm"
                                };

                                walked.Add(named + " takes it (" +
                                           EQUIPREACH.Mm(takes.Length) + " -> " +
                                           EQUIPREACH.Mm(takes.Left) + " mm)");

                                continue;
                            }

                            // Across the move, or too short to take it: it
                            // moves whole, and what is beyond it must
                            // take it instead
                            moving.Add(next.Id);

                            walked.Add(named + " moves (" +
                                       (takes.Kind == EQUIPREACHKIND.Shift
                                           ? "nothing beyond it"
                                           : EQUIPREACH.Mm(takes.Aside) +
                                             " mm across it") + ")");

                            if (takes.Kind != EQUIPREACHKIND.Shift)
                                queue.Enqueue(new KeyValuePair<Element, int>(next, other.Id));
                        }

                        if (moving.Count > CarryLimit)
                            return Trail(walked, "would move more than " +
                                                 CarryLimit + " pieces of " +
                                                 Noun + "work");
                    }
                }
            }

            // ---- doing it ----------------------------------------------
            foreach (Taker taker in takers.Values)
            {
                Connector theirs = QADUCTS.Lookup(doc, taker.RunId, taker.RunEnd);
                Connector piece = QADUCTS.Lookup(doc, taker.PieceId, taker.PieceEnd);

                if (theirs != null && piece != null && theirs.IsConnectedTo(piece))
                    theirs.DisconnectFrom(piece);
            }

            ElementTransformUtils.MoveElements(doc, moving, move);

            foreach (ElementId id in moving)
                moved.Add(id.ToValue());

            foreach (Taker taker in takers.Values)
            {
                var run = doc.GetElement(REVITCOMPAT.NewId(taker.RunId)) as MEPCurve;

                var located = run == null ? null : run.Location as LocationCurve;

                var line = located == null ? null : located.Curve as Line;

                if (line == null)
                    return "pipe " + taker.RunId + " could not be read again";

                XYZ a = line.GetEndPoint(0);
                XYZ b = line.GetEndPoint(1);

                // KEEPING ITS DIRECTION - EQUIPLINE
                located.Curve = taker.NearIsStart
                    ? Line.CreateBound(a + move, b)
                    : Line.CreateBound(a, b + move);

                stretched.Add(taker.Named);
            }

            doc.Regenerate();

            foreach (Taker taker in takers.Values)
            {
                Connector theirs = QADUCTS.Lookup(doc, taker.RunId, taker.RunEnd);
                Connector piece = QADUCTS.Lookup(doc, taker.PieceId, taker.PieceEnd);

                if (theirs == null || piece == null)
                    return "pipe " + taker.RunId + " could not be joined again";

                if (!theirs.IsConnectedTo(piece))
                    theirs.ConnectTo(piece);
            }

            EQUIPLOG.Say("      " + EQUIPREACH.Mm(move.GetLength()) + " mm: " +
                         string.Join("; ", walked.ToArray()));

            return null;
        }

        // A run that takes a carry up, and the joint it is unhooked at
        private class Taker
        {
            public long RunId;
            public int RunEnd;
            public long PieceId;
            public int PieceEnd;
            public bool NearIsStart;
            public string Named = string.Empty;
        }

        // The reason, with what was walked to get there, for the log
        private string Trail(List<string> walked, string why)
        {
            if (walked.Count > 0)
                EQUIPLOG.Say("      walked: " + string.Join("; ", walked.ToArray()));

            return why;
        }

        // The unit's own three directions, or null
        private XYZ[] UnitAxes(Document doc)
        {
            try
            {
                var unit = doc.GetElement(REVITCOMPAT.NewId(OwnerId)) as FamilyInstance;

                if (unit == null)
                    return null;

                Transform placed = unit.GetTransform();

                return new[]
                {
                    placed.BasisX.Normalize(),
                    placed.BasisY.Normalize(),
                    placed.BasisZ.Normalize()
                };
            }
            catch
            {
                return null;
            }
        }

        // `move` as its parts along three directions (EQUIPREACH.Parts)
        private static List<XYZ> Split(XYZ move, XYZ[] axes)
        {
            var along = new EQUIPV[axes.Length];

            for (int i = 0; i < axes.Length; i++)
                along[i] = EQUIPWALK.V(axes[i]);

            var parts = new List<XYZ>();

            foreach (EQUIPV part in EQUIPREACH.Parts(EQUIPWALK.V(move), along))
                parts.Add(new XYZ(part.X, part.Y, part.Z));

            return parts;
        }

        // The same parts as a try already made
        private static bool Repeats(List<List<XYZ>> done, List<XYZ> parts)
        {
            foreach (List<XYZ> one in done)
            {
                if (one.Count != parts.Count)
                    continue;

                bool same = true;

                for (int i = 0; i < one.Count && same; i++)
                    same = one[i].IsAlmostEqualTo(parts[i]);

                if (same)
                    return true;
            }

            return false;
        }

        // THE STEP NOTHING CAN STRETCH TO, MADE: an offset of two elbows
        // between the unit's new connector and the pipe's open end
        // (EQUIPDOGLEG plans, EQUIPOFFSET builds). If the open end is not
        // far enough out from the unit for the offset to fit - FCU-10's
        // connections land INSIDE where FCU-02's pipework was - the
        // pipework is first carried straight out to make the room.
        // Null when built and joined.
        private string Offset(
            Document doc, long endOwner, int endIndex, int mineIndex,
            List<string> notes)
        {
            // Read again: the carries tried before this have rolled back
            Connector mine = QADUCTS.Lookup(doc, OwnerId, mineIndex);
            Connector end = QADUCTS.Lookup(doc, endOwner, endIndex);

            if (mine == null || end == null)
                return "its connectors could not be read again";

            XYZ facing = EQUIPWALK.Facing(mine);

            XYZ back = EQUIPWALK.Facing(end);

            if (facing == null || back == null)
                return "the connectors will not say which way they face";

            facing = facing.Normalize();

            if (back.Normalize().DotProduct(facing) > -EQUIPREACH.Square)
                return "the pipe's end does not face the unit";

            double diameter = EQUIPWALK.SizeOf(end).Round
                ? EQUIPWALK.SizeOf(end).Diameter
                : 20.0 / 304.8;

            EQUIPDOGLEG fit = EQUIPDOGLEG.Fitting(
                EQUIPWALK.V(mine.Origin), EQUIPWALK.V(facing),
                EQUIPWALK.V(end.Origin), diameter);

            if (fit == null)
                return "no elbows fit a step that small";

            long owner = end.Owner.Id.ToValue();
            int endId = end.Id;

            if (!fit.Ok)
            {
                double more = fit.Room - fit.Gap + 2.0 / 304.8;

                Element piece = doc.GetElement(end.Owner.Id);

                EQUIPLOG.Say("   " + Called + ": making " + EQUIPREACH.Mm(more) +
                             " mm of room for an offset");

                string blocked = Carry(doc, piece, endId, facing.Multiply(more), notes);

                if (blocked != null)
                    return "making room for it " + blocked;

                doc.Regenerate();
            }

            return EQUIPOFFSET.Build(doc, OwnerId, mineIndex, owner, endId, notes, Called);
        }

        // A PIPE THAT TOUCHES THE UNIT BUT WAS NEVER JOINED to it (probe,
        // 2026-11-18: unit 164's CHW pipes stop 100 mm off FCU-04's
        // connections, open). Taken as this connector's joint, so the
        // change brings it and joins it: its open end within 300 mm,
        // facing back at the unit, in front of the unit's face, of the
        // SAME system (a silent one is not guessed at), nearest first,
        // each end once.
        public static EQUIPJOINT Loose(
            Document doc, Connector end, string label, HashSet<string> claimed)
        {
            try
            {
                if (end == null || end.IsConnected ||
                    end.Domain != Autodesk.Revit.DB.Domain.DomainPiping)
                    return null;

                int system = EQUIPWALK.SystemOf(end);

                XYZ facing = EQUIPWALK.Facing(end);

                if (system < 0 || facing == null)
                    return null;

                facing = facing.Normalize();

                XYZ at = end.Origin;

                double reach = 300.0 / 304.8;

                var near = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                    {
                        new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
                        new ElementCategoryFilter(BuiltInCategory.OST_PipeFitting),
                        new ElementCategoryFilter(BuiltInCategory.OST_PipeAccessory)
                    }))
                    .WherePasses(new BoundingBoxIntersectsFilter(new Outline(
                        at - new XYZ(reach, reach, reach),
                        at + new XYZ(reach, reach, reach))));

                Connector best = null;

                double closest = reach;

                foreach (Element element in near)
                {
                    ConnectorManager manager = QADUCTS.Manager(element);

                    if (manager == null)
                        continue;

                    foreach (Connector other in manager.Connectors)
                    {
                        if (other.ConnectorType != ConnectorType.End ||
                            other.Domain != Autodesk.Revit.DB.Domain.DomainPiping ||
                            other.IsConnected)
                            continue;

                        if (claimed.Contains(element.Id.ToValue() + ":" + other.Id))
                            continue;

                        if (EQUIPWALK.SystemOf(other) != system)
                            continue;

                        XYZ back = EQUIPWALK.Facing(other);

                        if (back == null || back.Normalize().DotProduct(facing) > -0.7)
                            continue;

                        // In front of the unit's face, not inside it
                        if ((other.Origin - at).DotProduct(facing) < -50.0 / 304.8)
                            continue;

                        double gap = other.Origin.DistanceTo(at);

                        if (gap < closest)
                        {
                            closest = gap;
                            best = other;
                        }
                    }
                }

                if (best == null)
                    return null;

                claimed.Add(best.Owner.Id.ToValue() + ":" + best.Id);

                var joint = new EQUIPJOINT
                {
                    OwnerId = end.Owner.Id.ToValue(),
                    MyIndex = end.Id,
                    Label = label,
                    SystemName = EQUIPWALK.SystemName(end),
                    Domain = (int)end.Domain,
                    System = system,
                    Pipe = true,
                    OtherId = best.Owner.Id.ToValue(),
                    OtherIndex = best.Id,
                    OtherName = EQUIPWALK.Describe(best.Owner) + " " +
                                best.Owner.Id.ToValue(),
                    Was = end.Origin,
                    Facing = facing,
                    WasLoose = true
                };

                EQUIPLOG.Say("   " + joint.Called + " was NEVER JOINED: " +
                             joint.OtherName + "'s open end is " +
                             EQUIPREACH.Mm(closest) + " mm from it - joining it");

                return joint;
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("looking for a pipe by " + label, ex);

                return null;
            }
        }

        // It was not joined when the change started (Loose)
        public bool WasLoose { get; set; }

        // What a carry must never move
        private static bool Fixed(Element element)
        {
            if (element == null || element.Category == null)
                return true;

            long id = element.Category.Id.ToValue();

            return id == (long)BuiltInCategory.OST_MechanicalEquipment
                || id == (long)BuiltInCategory.OST_DuctTerminal
                || id == (long)BuiltInCategory.OST_PlumbingFixtures
                || id == (long)BuiltInCategory.OST_Sprinklers;
        }

        // For the log: how far the connection moved, along the way the
        // old connector faced and across it
        private string Moved(XYZ move)
        {
            if (move == null)
                return string.Empty;

            if (Facing == null)
                return "moved " + EQUIPREACH.Mm(move.GetLength()) + " mm";

            XYZ facing = Facing.Normalize();

            double ahead = move.DotProduct(facing);

            double aside = (move - facing.Multiply(ahead)).GetLength();

            return "moved " + EQUIPREACH.Mm(move.GetLength()) + " mm (" +
                   EQUIPREACH.Mm(ahead) + " out, " + EQUIPREACH.Mm(aside) +
                   " across)";
        }

        // The straight run this joint was on, from the unit's end. False
        // when it is not one, or nothing is joined beyond it - that one
        // is simply moved whole.
        public bool Run(Document doc, out XYZ here, out XYZ far)
        {
            here = null;
            far = null;

            try
            {
                Connector theirs = Theirs(doc);

                if (theirs == null)
                    return false;

                var curve = doc.GetElement(theirs.Owner.Id) as MEPCurve;

                if (curve == null)
                    return false;

                var located = curve.Location as LocationCurve;

                var line = located == null ? null : located.Curve as Line;

                if (line == null)
                    return false;

                here = theirs.Origin;

                XYZ a = line.GetEndPoint(0);
                XYZ b = line.GetEndPoint(1);

                far = a.DistanceTo(here) <= b.DistanceTo(here) ? b : a;

                return !FreeBeyond(curve, theirs.Id);
            }
            catch
            {
                return false;
            }
        }

        // The new connector faces another way than this joint's did
        public bool TurnedOn(Connector mine)
        {
            XYZ now = EQUIPWALK.Facing(mine);

            if (Facing == null || now == null)
                return false;

            return Facing.Normalize().DotProduct(now.Normalize()) <
                   EQUIPREACH.Square;
        }

        // Nothing is joined to this run anywhere but at `except` - not
        // its far end, and not a tap on its side
        private static bool FreeBeyond(MEPCurve curve, int except)
        {
            ConnectorManager manager = QADUCTS.Manager(curve);

            if (manager == null)
                return false;

            try
            {
                foreach (Connector connector in manager.Connectors)
                {
                    if (connector.Id == except)
                        continue;

                    foreach (Connector other in connector.AllRefs)
                    {
                        if (other.Owner == null ||
                            other.Owner.Id.ToValue() == curve.Id.ToValue())
                            continue;

                        if (EQUIPWALK.Ductwork(other.Owner) ||
                            EQUIPWALK.Pipework(other.Owner))
                            return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private Connector Mine(Document doc)
        {
            return QADUCTS.Lookup(doc, OwnerId, MyIndex);
        }

        private Connector Theirs(Document doc)
        {
            return QADUCTS.Lookup(doc, OtherId, OtherIndex);
        }
    }
}
