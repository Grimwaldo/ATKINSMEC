using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // The two decisions a type swap makes about the joints on a unit,
    // with no Revit in them (test: tests/equiptest):
    //
    //   EQUIPMATCH - which of the NEW type's connectors each old joint
    //                belongs to;
    //   EQUIPREACH - whether the duct or pipe on it can be brought to
    //                where that connector is now, and how.
    //
    // USER-REPORTED, 2026-11-18: FCU-02 -> FCU-10 in a real model was
    // refused with "The duct/pipe has been modified to be in the
    // opposite direction causing the connections to be invalid". The
    // swap only took the DUCTS off the unit: its two CHW pipes stayed
    // joined, FCU-10's much bigger body put their connections further
    // out, and Revit dragged the short pipe stubs inside out (the
    // journal names both pipes). Pipes are now taken off and put back
    // like the ducts, and nothing is ever stretched backwards or bent.

    // A point or a direction, in feet
    public struct EQUIPV
    {
        public double X;
        public double Y;
        public double Z;

        public EQUIPV(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static EQUIPV operator +(EQUIPV a, EQUIPV b)
        {
            return new EQUIPV(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static EQUIPV operator -(EQUIPV a, EQUIPV b)
        {
            return new EQUIPV(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static EQUIPV operator *(EQUIPV a, double k)
        {
            return new EQUIPV(a.X * k, a.Y * k, a.Z * k);
        }

        public double Dot(EQUIPV other)
        {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        public double Length
        {
            get { return Math.Sqrt(Dot(this)); }
        }

        public EQUIPV Unit
        {
            get
            {
                double length = Length;

                return length < 1e-12
                    ? new EQUIPV(0, 0, 0)
                    : this * (1.0 / length);
            }
        }
    }

    // ==================================================
    // Bringing a duct or pipe to the new connector
    // ==================================================

    public enum EQUIPREACHKIND
    {
        // Already where the new connector is
        There = 0,

        // Slide the near end along the run's own line
        Stretch,

        // Nothing on its far end: move the whole piece
        Shift,

        // It cannot get there without bending or turning inside out,
        // so the joint is LEFT OPEN and said so
        Open
    }

    public class EQUIPREACH
    {
        // Half a millimetre: as good as on it
        public const double Coincident = 0.5 / 304.8;

        // A millimetre off the run's line is a bend, which the fitting
        // on its far end cannot follow - Revit's "The family is
        // connected in a network and can no longer keep the
        // connectivity"
        public const double Straight = 1.0 / 304.8;

        // cos 2 degrees: the new connection must face the way the old
        // one did
        public const double Square = 0.99939;

        public EQUIPREACHKIND Kind;

        // How far the connection moved, in feet
        public double Moved;

        // Along the run (+ = into it, shortening it) and across it
        public double Along;
        public double Aside;

        // The run now, and what a stretch would leave of it
        public double Length;
        public double Left;

        // Why it was left open, for the report
        public string Why = string.Empty;

        // Open because the new connection FACES another way - nothing
        // moved sideways can mend that
        public bool Turned;

        // How far to move the UNIT so the run on `anchor` stays straight
        // (user, 2026-11-18: FCU-10's supply spigot came out 327 mm to
        // the side of a 42 mm stub, FCU-02's 890 mm spigot centred on a
        // 1534 mm one). Only the sideways part, so the run still takes
        // whatever it can along its own line; the whole offset when the
        // run is too short to take that too. Zero when it reaches as it
        // is.
        //
        // here - the run's end on the unit (the old connector), far - its
        // other end, now - the new connector
        public static EQUIPV Anchor(
            EQUIPV here, EQUIPV far, EQUIPV now, double shortest)
        {
            EQUIPV run = far - here;

            EQUIPV along = run.Unit;

            EQUIPV move = now - here;

            double ahead = move.Dot(along);

            EQUIPV aside = move - along * ahead;

            bool straight = aside.Length <= Straight;

            bool room = run.Length - ahead >= shortest;

            if (straight && room)
                return new EQUIPV(0, 0, 0);

            return room
                ? aside * -1.0
                : move * -1.0;
        }

        // here   - the run's end on the unit (where the old connector was)
        // far    - its other end
        // to     - the new connector
        // was/now facing - the unit's connector direction before and
        //          after; null when the family will not say
        // farFree - nothing is joined to the run anywhere but here
        // shortest - the least length a run may be left with
        // noun    - "duct" or "pipe", for the words
        public static EQUIPREACH Decide(
            EQUIPV here,
            EQUIPV far,
            EQUIPV to,
            EQUIPV? wasFacing,
            EQUIPV? nowFacing,
            bool farFree,
            double shortest,
            string noun)
        {
            var reach = new EQUIPREACH();

            noun = string.IsNullOrEmpty(noun) ? "duct" : noun;

            EQUIPV move = to - here;

            reach.Moved = move.Length;

            EQUIPV run = far - here;

            reach.Length = run.Length;

            EQUIPV along = run.Unit;

            reach.Along = move.Dot(along);
            reach.Aside = (move - along * reach.Along).Length;
            reach.Left = reach.Length - reach.Along;

            if (wasFacing.HasValue && nowFacing.HasValue)
            {
                double turn = wasFacing.Value.Unit.Dot(nowFacing.Value.Unit);

                if (turn < Square)
                {
                    reach.Kind = EQUIPREACHKIND.Open;

                    reach.Turned = true;

                    reach.Why = "the new connection faces another way (" +
                                Degrees(turn) + " degrees round)";

                    return reach;
                }
            }

            if (reach.Moved < Coincident)
            {
                reach.Kind = EQUIPREACHKIND.There;

                return reach;
            }

            if (farFree)
            {
                // Nothing on its far end to keep in place: it moves as a
                // whole, so it keeps its length and its direction
                reach.Kind = EQUIPREACHKIND.Shift;

                return reach;
            }

            if (reach.Length < 1e-9)
            {
                reach.Kind = EQUIPREACHKIND.Open;

                reach.Why = "the " + noun + " has no length";

                return reach;
            }

            if (reach.Aside > Straight)
            {
                reach.Kind = EQUIPREACHKIND.Open;

                reach.Why = "the new connection is " + Mm(reach.Aside) +
                            " mm to the side of the " + noun +
                            " - it would have to bend";

                return reach;
            }

            if (reach.Left < shortest)
            {
                reach.Kind = EQUIPREACHKIND.Open;

                reach.Why = reach.Along >= reach.Length
                    ? "the new connection is " + Mm(reach.Along) +
                      " mm further out, past the end of the " +
                      Mm(reach.Length) + " mm " + noun
                    : "the new connection is " + Mm(reach.Along) +
                      " mm further out, which would leave the " +
                      Mm(reach.Length) + " mm " + noun + " " +
                      Mm(reach.Left) + " mm long";

                return reach;
            }

            reach.Kind = EQUIPREACHKIND.Stretch;

            return reach;
        }

        // For the log: what the move was
        public string Described
        {
            get
            {
                return "moved " + Mm(Moved) + " mm (" + Mm(Along) +
                       " along, " + Mm(Aside) + " aside)";
            }
        }

        public static string Mm(double feet)
        {
            return Math.Round(feet * 304.8).ToString("0");
        }

        private static string Degrees(double cosine)
        {
            double clamped = Math.Max(-1.0, Math.Min(1.0, cosine));

            return Math.Round(Math.Acos(clamped) * 180.0 / Math.PI)
                .ToString("0");
        }
    }

    // ==================================================
    // Which new connector each joint belongs to
    // ==================================================

    // One connector, as the match needs it
    public class EQUIPPORT
    {
        // The connector's own id on its owner
        public int Id;

        // Revit's Domain as a number: a duct joint never goes to a pipe
        // connector
        public int Domain;

        // Its system classification as a number, -1 when it does not
        // say (undefined, global, fitting)
        public int System = -1;

        public EQUIPV At;
    }

    public static class EQUIPMATCH
    {
        // Old connector id -> new connector id. A joint with nowhere to
        // go is missing from the answer.
        //
        // SAME FAMILY: THE SAME CONNECTOR. Every type of a family has
        // the family's own connectors under the same ids, so that is
        // exact. Position is NOT: the old rule was "nearest to where it
        // used to be", which in the user's FCU-02 -> FCU-10 swap is a
        // guess between a supply and a return 100 mm apart after the
        // whole unit grew by 600 mm.
        //
        // ACROSS FAMILIES (behind a tick in the window): same domain,
        // same system before any system that does not say, nearest
        // first, each claimed once - supply never goes to return.
        public static Dictionary<int, int> Assign(
            List<EQUIPPORT> was, List<EQUIPPORT> now, bool sameFamily)
        {
            var map = new Dictionary<int, int>();

            var taken = new HashSet<int>();

            if (was == null || now == null)
                return map;

            if (sameFamily)
            {
                foreach (EQUIPPORT old in was)
                {
                    foreach (EQUIPPORT one in now)
                    {
                        if (one.Id != old.Id || one.Domain != old.Domain)
                            continue;

                        if (taken.Contains(one.Id))
                            continue;

                        map[old.Id] = one.Id;

                        taken.Add(one.Id);

                        break;
                    }
                }
            }

            var pairs = new List<Pair>();

            foreach (EQUIPPORT old in was)
            {
                if (map.ContainsKey(old.Id))
                    continue;

                foreach (EQUIPPORT one in now)
                {
                    if (taken.Contains(one.Id) || one.Domain != old.Domain)
                        continue;

                    int rank = Rank(old.System, one.System);

                    if (rank < 0)
                        continue;

                    pairs.Add(new Pair
                    {
                        Was = old.Id,
                        Now = one.Id,
                        Rank = rank,
                        Gap = (one.At - old.At).Length
                    });
                }
            }

            pairs.Sort(delegate (Pair a, Pair b)
            {
                if (a.Rank != b.Rank)
                    return a.Rank.CompareTo(b.Rank);

                if (Math.Abs(a.Gap - b.Gap) > 1e-9)
                    return a.Gap.CompareTo(b.Gap);

                return a.Was != b.Was
                    ? a.Was.CompareTo(b.Was)
                    : a.Now.CompareTo(b.Now);
            });

            foreach (Pair pair in pairs)
            {
                if (map.ContainsKey(pair.Was) || taken.Contains(pair.Now))
                    continue;

                map[pair.Was] = pair.Now;

                taken.Add(pair.Now);
            }

            return map;
        }

        // 0 = the same system, 1 = one side does not say, -1 = never
        public static int Rank(int was, int now)
        {
            if (was < 0 || now < 0)
                return 1;

            return was == now ? 0 : -1;
        }

        private class Pair
        {
            public int Was;
            public int Now;
            public int Rank;
            public double Gap;
        }
    }
}
