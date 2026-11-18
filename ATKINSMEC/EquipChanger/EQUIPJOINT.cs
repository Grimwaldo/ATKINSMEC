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
        // PIPE is brought to it rather than the equipment being nudged:
        // the equipment is where somebody put it.
        //
        // Returns null when it is joined again, else why it was LEFT
        // OPEN. Never a refusal of the whole change: a duct or pipe
        // that cannot reach the new connector straight is a job for a
        // person, and the window says which one.
        public string Redo(Document doc, int nowIndex)
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

                string why = Bring(doc, theirs, mine);

                if (why != null)
                {
                    EQUIPLOG.Say("   " + Called + " LEFT OPEN: " + why);

                    return why;
                }

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
        private static double Shortest(Document doc)
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
        private string Bring(Document doc, Connector end, Connector mine)
        {
            XYZ to = mine.Origin;
            XYZ here = end.Origin;

            if (to == null || here == null)
                return "its connector would not say where it is";

            XYZ facing = EQUIPWALK.Facing(mine);

            Element owner = doc.GetElement(end.Owner.Id);

            var curve = owner as MEPCurve;

            if (curve == null)
            {
                // A fitting or an accessory straight on the unit. It is
                // joined on its other side too, so it is not moved:
                // joined again only if the new connector is right on it
                // (asked as a free run, which is There, Open for a turn,
                // or Shift for anything that would need it moved)
                EQUIPREACH still = EQUIPREACH.Decide(
                    EQUIPWALK.V(here), EQUIPWALK.V(here), EQUIPWALK.V(to),
                    Facing == null ? (EQUIPV?)null : EQUIPWALK.V(Facing),
                    facing == null ? (EQUIPV?)null : EQUIPWALK.V(facing),
                    true, 0.0, Noun);

                if (still.Kind == EQUIPREACHKIND.There)
                    return null;

                return still.Kind == EQUIPREACHKIND.Open
                    ? still.Why
                    : OtherName + " sits straight on it, and the new " +
                      "connection is " + EQUIPREACH.Mm(still.Moved) +
                      " mm away from it";
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
                    return reach.Why + " (" + Noun + " " +
                           curve.Id.ToValue() + ")";

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
