using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // One joint between a piece of equipment and the duct on it,
    // written down so it can be taken apart and put back.
    //
    // A Connector cannot be held across a change - the same reason
    // QADUCTFIX carries a connector ID on its findings rather than the
    // connector. So this keeps ELEMENT IDS AND INDICES and looks the
    // connectors up again each time.
    public class EQUIPJOINT
    {
        // The equipment, and which of its connectors this is. Index is
        // the position in the list (1, 2, 3) and is what a report says;
        // MyIndex is the connector's own id, which is what a lookup
        // needs. They are not the same number.
        public long OwnerId { get; set; }

        public int MyIndex { get; set; }

        public int Index { get; set; }

        // What it was joined to
        public long OtherId { get; set; }

        public int OtherIndex { get; set; }

        // Where the equipment's connector was before the type changed,
        // so the duct can be brought to where it has moved to
        public XYZ Was { get; set; }

        public static EQUIPJOINT Of(Connector end, int index)
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
                    // IS an MEPCurve, and so is DuctLining. Compile
                    // verified. So the sleeve has to be turned away
                    // HERE, by category; see EQUIPWALK.Ductwork.
                    //
                    // Getting it wrong costs more here than in the
                    // walk. Undo() would unhook the SLEEVE and leave
                    // the real duct joined, so the type swap raises the
                    // very "cannot keep the connectivity" error these
                    // five steps exist to prevent - and Redo() would
                    // then rewrite the insulation's LocationCurve,
                    // moving a sleeve of fibreglass while the duct it
                    // wraps stayed where it was.
                    if (!EQUIPWALK.Ductwork(other.Owner))
                        continue;

                    return new EQUIPJOINT
                    {
                        OwnerId = end.Owner.Id.ToValue(),
                        MyIndex = end.Id,
                        Index = index,
                        OtherId = other.Owner.Id.ToValue(),
                        OtherIndex = other.Id,
                        Was = end.Origin
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

                    EQUIPLOG.Say("   unhooked connector " + Index);
                }

                return true;
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("unhooking connector " + Index, ex);

                return false;
            }
        }

        // Put it back. The new type's connector may not be where the
        // old one was, so the DUCT is brought to it rather than the
        // equipment being nudged: the equipment is where somebody put
        // it, and the duct is the thing that serves it.
        public bool Redo(Document doc, Connector mine)
        {
            Connector theirs = Theirs(doc);

            if (mine == null || theirs == null)
                return false;

            try
            {
                if (mine.IsConnectedTo(theirs))
                    return true;

                XYZ now = mine.Origin;

                if (now != null && !Bring(doc, theirs, now))
                {
                    // Could not be brought over; ConnectTo is still
                    // worth trying, since Revit joins some connectors
                    // that are not quite coincident
                    EQUIPLOG.Say("   connector " + Index +
                                 ": the duct end could not be moved");
                }

                mine.ConnectTo(theirs);

                EQUIPLOG.Say("   hooked connector " + Index + " back up");

                return true;
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("hooking connector " + Index + " back up", ex);

                return false;
            }
        }

        // WHICH of the new type's connectors this joint belongs to.
        //
        // NOT the one in the same position in the list. A different
        // family can order its connectors differently, and getting this
        // wrong would join the supply duct to the return connector -
        // a mistake that looks perfectly fine on the drawing and is
        // wrong in every calculation afterwards.
        //
        // The nearest one to where this joint's connector USED to be is
        // a far better guess: a replacement unit is the same shape of
        // thing in the same place, so its supply connector is near the
        // old supply connector. Each is claimed once, nearest first,
        // which is the same greedy matching ALIGNTERMINALS uses.
        public static Connector Nearest(
            EQUIPJOINT joint, List<Connector> among, HashSet<int> taken)
        {
            Connector best = null;

            double closest = double.MaxValue;

            foreach (Connector one in among)
            {
                if (one == null || taken.Contains(one.Id))
                    continue;

                double gap;

                try
                {
                    gap = joint.Was == null
                        ? 0.0
                        : joint.Was.DistanceTo(one.Origin);
                }
                catch
                {
                    continue;
                }

                if (gap < closest)
                {
                    closest = gap;

                    best = one;
                }
            }

            if (best != null)
                taken.Add(best.Id);

            return best;
        }

        // Move the duct's end onto a point, leaving its other end alone
        private static bool Bring(Document doc, Connector end, XYZ to)
        {
            try
            {
                if (end.Owner == null)
                    return false;

                var curve = doc.GetElement(end.Owner.Id) as MEPCurve;

                if (curve == null)
                    return false;

                var located = curve.Location as LocationCurve;

                if (located == null)
                    return false;

                var line = located.Curve as Line;

                if (line == null)
                    return false;

                XYZ here = end.Origin;

                if (here == null || here.DistanceTo(to) < 1e-7)
                    return true;

                XYZ a = line.GetEndPoint(0);
                XYZ b = line.GetEndPoint(1);

                // Whichever end of the duct this connector is
                bool movingStart = EQUIPLINE.MovingIsStart(
                    a.DistanceTo(here), b.DistanceTo(here));

                XYZ far = movingStart ? b : a;

                if (far.DistanceTo(to) < doc.Application.ShortCurveTolerance)
                {
                    // Moving it would leave a duct too short to exist,
                    // which Revit deletes - the "Line is too short" the
                    // CHW page spent so long on. Refused instead.
                    EQUIPLOG.Say("   moving that duct end would leave it " +
                                 "too short to exist");

                    return false;
                }

                // KEEPING THE ORIGINAL DIRECTION IS NOT COSMETIC - see
                // EQUIPLINE. Writing (far, to) reverses the curve
                // whenever the moving end was endpoint 0, and Revit
                // throws the whole transaction out at the commit.
                located.Curve = movingStart
                    ? Line.CreateBound(to, b)
                    : Line.CreateBound(a, to);

                EQUIPLOG.Say("   moved a duct end to meet connector " +
                             (movingStart ? "(its start)" : "(its finish)"));

                return true;
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("moving a duct end", ex);

                return false;
            }
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
