using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // WHICH WAY A BELLMOUTH'S AIR GOES - into the room it stands in, or
    // into an FCU. At the user's request (2026-10-10), four cases:
    //
    //   1. a bellmouth serving its room directly, an FCU in that room too;
    //   2. a bellmouth serving its room directly, no FCU there;
    //   3. an endcap feeding an FCU that serves another room;
    //   4. a bellmouth feeding an FCU, both standing in a room the FCU
    //      does not serve (the kifaf master bedroom: unit and bellmouth
    //      over the ensuite).
    //
    // 1 and 4 look alike from above - a bellmouth near an FCU. The mouth
    // was meant to tell them apart on its own and could not in a real
    // model (see Bellmouth below): it is now the first of several tests.
    // An ENDCAP is a blank cap with no mouth, and is traced to the
    // nearest unit as it always was.
    //
    // Revit-free, so the geometry is tested (tests/ventfcutest); any
    // unit of length, as long as it is one unit.
    public static class VENTFACING
    {
        // What a terminal does with its air
        public enum Role
        {
            InRoom,     // its own room's air - cases 1 and 2
            Endcap,     // the nearest unit's rooms - case 3
            Feeds       // the rooms of the unit it faces - case 4
        }

        // The decision, from the family name and what its mouth hits.
        // An endcap name wins over a bellmouth name: an endcap has always
        // been traced, and nothing here may change that.
        public static Role RoleOf(string familyName, bool facesUnit)
        {
            if (VENTSETTINGS.IsEndcap(familyName))
                return Role.Endcap;

            if (facesUnit && VENTSETTINGS.IsBellmouth(familyName))
                return Role.Feeds;

            return Role.InRoom;
        }

        // ==================================================
        // A bellmouth near an FCU: does it feed it?
        // ==================================================
        //
        // "FACES THE UNIT" ALONE WAS TOO STRICT. The first real run
        // (kifaf LEVEL 04A, 2026-10-11) found 8 of 32 bellmouths feeding a
        // unit - most of those only because they sat inside its box - and
        // four rooms got no fresh air: bellmouths drawn BESIDE their FCU's
        // plenum, pointing past it, were taken for terminals of the
        // ensuite or kitchen they stood in. So the mouth is now one test
        // of several, tried in this order - among the units it CAN feed
        // (CanFeed: not on its own duct system, supplying a room in scope):
        //
        //   faces a unit within the reach          -> feeds it
        //   within `Touching` of a unit's casing   -> feeds it
        //   no unit within the reach               -> its room   (case 2)
        //   mouth points down into the room        -> its room   (case 1)
        //   stands in no room in scope             -> feeds the nearest
        //   the nearest unit supplies its room     -> feeds it   (same air)
        //   its room's ROW gives none of this air  -> feeds it   (case 4)
        //   its room has its own terminal for this
        //   air, and a room the unit supplies has
        //   none, nor an endcap on the unit        -> feeds it
        //   otherwise                              -> its room
        //
        // YANM3's STOREs (user, 2026-10-24: "this store fresh air was not
        // detected", BP01.B1.019 then BP01.B1.076): a fresh air bellmouth
        // went to a unit 1.49 m away as "its room needs none" - the store had
        // no row at all, the workbook being another job's - and another to
        // the unit it faces 1.22 m away; both units supply no room in scope,
        // so both stores' air went nowhere. A room with no row is not known
        // to need none, and a unit passing air to no room is fed nothing,
        // faced, touched or near.
        //
        // The last three use the SHEET: a bellmouth over an ensuite that
        // needs no fresh air, beside an FCU, is that FCU's fresh air; one
        // in a corridor that needs fresh air, beside an FCU that does not
        // supply the corridor, is the corridor's - unless the corridor has
        // a grille of its own and the FCU's living room has nothing
        // bringing it any (the gap the user's list named, 2026-10-15).
        public enum Reason
        {
            Faces,
            Touching,
            NoUnit,
            IntoRoomBelow,
            InNoRoom,
            UnitServesItsRoom,
            ItsRoomNeedsNone,
            UnitNeedsIt,
            ItsRoomNeedsAir,
            NotInSheet
        }

        // A UNIT A BELLMOUTH MAY FEED: not on the bellmouth's own duct
        // system (an AHU's intake runs into it), and supplying a room in
        // scope - air handed to a unit that passes it to no room is lost
        public static bool CanFeed(bool onItsOwnSystem, bool suppliesRoomInScope)
        {
            return !onItsOwnSystem && suppliesRoomInScope;
        }

        // A mouth pointing more than ~45 degrees down discharges into the
        // room under it
        public const double DownZ = -0.7;

        public static Role Bellmouth(
            bool faces,
            bool touching,
            bool unitInReach,
            double mouthZ,
            bool inRoom,
            bool unitServesItsRoom,
            double itsRoomAir,
            out Reason why,
            bool roomHasOwn = false,
            bool unitStarved = false,
            bool roomInSheet = true)
        {
            if (faces)
            {
                why = Reason.Faces;
                return Role.Feeds;
            }

            if (touching)
            {
                why = Reason.Touching;
                return Role.Feeds;
            }

            if (!unitInReach)
            {
                why = Reason.NoUnit;
                return Role.InRoom;
            }

            if (mouthZ < DownZ)
            {
                why = Reason.IntoRoomBelow;
                return Role.InRoom;
            }

            if (!inRoom)
            {
                why = Reason.InNoRoom;
                return Role.Feeds;
            }

            if (unitServesItsRoom)
            {
                why = Reason.UnitServesItsRoom;
                return Role.Feeds;
            }

            if (roomInSheet && itsRoomAir <= 0)
            {
                why = Reason.ItsRoomNeedsNone;
                return Role.Feeds;
            }

            // Both need this air: its room has a terminal of its own for
            // it, the unit's room has nothing - so this is the unit's
            if (roomHasOwn && unitStarved)
            {
                why = Reason.UnitNeedsIt;
                return Role.Feeds;
            }

            why = roomInSheet ? Reason.ItsRoomNeedsAir : Reason.NotInSheet;
            return Role.InRoom;
        }

        public static string Say(Reason why)
        {
            switch (why)
            {
                case Reason.Faces: return "faces the unit";
                case Reason.Touching: return "beside the unit's casing";
                case Reason.NoUnit: return "no unit within the reach";
                case Reason.IntoRoomBelow: return "mouth points down into its room";
                case Reason.InNoRoom: return "stands in no room in scope";
                case Reason.UnitServesItsRoom: return "the unit supplies the room it stands in";
                case Reason.ItsRoomNeedsNone: return "the room it stands in needs none of this air";
                case Reason.UnitNeedsIt: return "its room has its own terminal for this air, and a room the unit supplies has none";
                case Reason.NotInSheet: return "the room it stands in has no row in the sheet";
                default: return "the room it stands in needs this air and the unit does not supply it";
            }
        }

        // How close to a unit's casing counts as beside it, whichever
        // way the mouth points (metres; REVITROOMS converts)
        public const double TouchingM = 0.3;

        // ==================================================
        // Endcaps: the nearest unit, or a unit's RETURN DUCT
        // ==================================================
        //
        // User, 2026-10-24 (YANM3, an EMCC room): "instead of using endcap
        // reach tolerance, we can detect the nearest freshair endcap to
        // the return air duct connected to the FCU serving a room located
        // outside of it". The room's FCUs hang in the corridor outside it;
        // the fresh air endcap is let into the ceiling by the return duct
        // coming back from the room, 3.4 m from the unit itself - beyond
        // the reach, and the room had no fresh air.
        //
        // THE REACH STILL HOLDS - TO THE RETURN DUCT (2026-10-25: "this
        // room fresh air is showing 3660"). With no limit at all, MCC ROOM
        // BP01.B1.179's two units took endcaps 2.9 m and 8.5 m from their
        // return ducts as well as their own at 0.2 m. Every true match on
        // YANM3 was within 1.2 m of its return duct (the EMCC rooms' at
        // 0.48 and 0.50 m), so the reach is measured to the duct instead
        // of the unit.

        public enum EndcapBy
        {
            None,       // nothing near enough
            Reach,      // the nearest unit, within the reach - as always
            ReturnDuct  // within the reach of a unit's return duct, no further than any unit
        }

        // `toUnit`: to the nearest unit's connectors; `toReturn`: to the
        // nearest return ductwork of a unit standing outside the rooms it
        // supplies (Outside). double.MaxValue = none. The return duct wins
        // when it is within the reach and no further than any unit; a unit
        // nearer still keeps the reach, so an endcap just beyond its own
        // unit is not handed to a return duct across the floor.
        public static EndcapBy Endcap(double toUnit, double toReturn, double reach)
        {
            if (toReturn <= reach && toReturn <= toUnit)
                return EndcapBy.ReturnDuct;

            return toUnit < double.MaxValue && toUnit <= reach ? EndcapBy.Reach : EndcapBy.None;
        }

        // A unit that supplies rooms and stands in none of them: its return
        // air comes back to it along a duct, and its fresh air with it.
        // `standingIn` null: in no room in scope.
        public static bool Outside(string standingIn, ICollection<string> supplies)
        {
            return supplies != null && supplies.Count > 0 &&
                   (standingIn == null || !supplies.Contains(standingIn));
        }

        // A box, as Revit's bounding box gives it
        public class Box
        {
            public double[] Min;
            public double[] Max;
        }

        // How far a point is from a box - 0 inside it
        public static double Distance(double[] point, Box box)
        {
            if (point == null || box == null || box.Min == null || box.Max == null)
                return double.MaxValue;

            double sum = 0;

            for (int axis = 0; axis < 3; axis++)
            {
                double low = Math.Min(box.Min[axis], box.Max[axis]);
                double high = Math.Max(box.Min[axis], box.Max[axis]);
                double d = point[axis] < low ? low - point[axis]
                         : point[axis] > high ? point[axis] - high
                         : 0;

                sum += d * d;
            }

            return Math.Sqrt(sum);
        }

        // Where a terminal's air goes in or out: a step in front of its
        // face. A sidewall grille's insertion point is often in the wall,
        // on whichever side the family put it, so the room it blows into
        // is found from here rather than from where it stands.
        public static double[] InFront(double[] at, double[] mouth, double step)
        {
            if (at == null)
                return null;

            if (mouth == null)
                return at;

            return new[] { at[0] + mouth[0] * step, at[1] + mouth[1] * step, at[2] + mouth[2] * step };
        }

        // THE DIRECTION THE MOUTH FACES, from the duct connector: away
        // from the duct, i.e. opposite the connector's own Z. The API does
        // not say which way that Z points, so the sign is checked against
        // the terminal's BODY, which lies on the mouth side of the
        // connector. A body centred on the connector says nothing and the
        // connector is trusted. Null when there is no direction at all.
        public static double[] Mouth(double[] connectorZ, double[] connectorAt, double[] bodyCentre)
        {
            if (connectorZ == null || Length(connectorZ) < 1e-9)
                return null;

            double[] d = Unit(new[] { -connectorZ[0], -connectorZ[1], -connectorZ[2] });

            if (connectorAt != null && bodyCentre != null)
            {
                double[] body =
                {
                    bodyCentre[0] - connectorAt[0],
                    bodyCentre[1] - connectorAt[1],
                    bodyCentre[2] - connectorAt[2]
                };

                if (Length(body) > 1e-6 && Dot(body, d) < 0)
                    d = new[] { -d[0], -d[1], -d[2] };
            }

            return d;
        }

        // How far along the ray from `from` in `direction` it enters the
        // box grown by `margin` on every side - or -1 if it does not
        // within `reach`. Starting inside the box is 0.
        public static double Hit(double[] from, double[] direction, Box box, double reach, double margin)
        {
            if (from == null || direction == null || box == null ||
                box.Min == null || box.Max == null || reach <= 0)
                return -1;

            if (Length(direction) < 1e-9)
                return -1;

            double[] d = Unit(direction);

            double near = 0;
            double far = reach;

            for (int axis = 0; axis < 3; axis++)
            {
                double low = Math.Min(box.Min[axis], box.Max[axis]) - margin;
                double high = Math.Max(box.Min[axis], box.Max[axis]) + margin;

                if (Math.Abs(d[axis]) < 1e-12)
                {
                    if (from[axis] < low || from[axis] > high)
                        return -1;

                    continue;
                }

                double t1 = (low - from[axis]) / d[axis];
                double t2 = (high - from[axis]) / d[axis];

                near = Math.Max(near, Math.Min(t1, t2));
                far = Math.Min(far, Math.Max(t1, t2));

                if (near > far)
                    return -1;
            }

            return near;
        }

        // Which of the boxes the mouth faces first - its index, or -1 -
        // and how far. Two at one distance: the first in the list, every
        // run.
        public static int Faced(
            double[] from,
            double[] direction,
            IList<Box> boxes,
            double reach,
            double margin,
            out double distance)
        {
            distance = -1;

            int best = -1;

            if (boxes == null)
                return best;

            for (int i = 0; i < boxes.Count; i++)
            {
                double t = Hit(from, direction, boxes[i], reach, margin);

                if (t >= 0 && (best < 0 || t < distance))
                {
                    best = i;
                    distance = t;
                }
            }

            return best;
        }

        private static double Dot(double[] a, double[] b)
        {
            return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        private static double Length(double[] a)
        {
            return Math.Sqrt(Dot(a, a));
        }

        private static double[] Unit(double[] a)
        {
            double length = Length(a);

            return new[] { a[0] / length, a[1] / length, a[2] / length };
        }
    }
}
