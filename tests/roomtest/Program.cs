using System;
using System.Collections.Generic;

// Mirrors QACHWROOM with no Revit: will the pipe survive the bigger
// fittings, and if not, where does the room come from? A HAND-KEPT COPY -
// QACHWROOM walks Revit's own connectors, so the real code cannot be
// compiled in. KEEP IT IN STEP: change one, change the other.
//
// Brought back in step on 2026-10-06, after the copy recovered from the
// session transcripts turned out to predate six fixes in the add-in: the
// 2.5 mm floor (was 5), a reach of 8 (was 4), the gentle route tried on
// BOTH ends before the branch route on either, Leg refusing a walk that
// comes back round to the short pipe, a dead end that lets any pipework
// move (a cap included - only equipment stays), and Branch letting go
// of the short pipe's own joint instead of calling it a ring.
//
// The case that started it: a 60 mm jog between two DN20 elbows, sized
// up to DN80. The elbows grow from either side, the jog is left with
// nothing, and Revit deletes it - "Line is too short".
//
// The fitting is SLID along the short pipe's own line so the pipe gets
// longer. The length it takes has to come from somewhere, and there are
// only two honest answers: the first pipe in line gives it up, or a
// dead-end branch is pushed along bodily.
//
// Everything here is in millimetres, which the add-in does in feet.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    // QACHWROOM's numbers. Floor there is max(2 x Revit's short curve
    // tolerance, 2.5 mm) - about 2.4 and 2.5, so 2.5. It was 5 until a
    // real model showed that far too strict.
    private const double Floor = 2.5;     // shortest pipe worth keeping
    private const double InLine = 0.9994; // cos(2 degrees)
    private const int Reach = 8;          // how far to look for a giver
    private const int Bundle = 12;        // most that may be pushed along

    // ==================================================
    // A tiny model
    // ==================================================

    private enum Kind { Pipe, Elbow, Tee, Valve, Equipment, Cap }

    private class Thing
    {
        public int Id;
        public Kind What;

        public double Ax, Ay, Az;

        public double Length;
        public double Dn = 20;

        // What a fitting takes up along the pipe, at its current size
        public double TakeOff;

        public List<int> Joined = new List<int>();

        public double Dot(Thing other)
        {
            return Ax * other.Ax + Ay * other.Ay + Az * other.Az;
        }
    }

    private class Model
    {
        public Dictionary<int, Thing> Things = new Dictionary<int, Thing>();

        public Thing Add(Kind what, double ax, double ay, double az,
            double length = 1000, double takeOff = 0, double dn = 20)
        {
            var thing = new Thing
            {
                Id = Things.Count + 1,
                What = what,
                Ax = ax,
                Ay = ay,
                Az = az,
                Length = length,
                TakeOff = takeOff,
                Dn = dn
            };

            Things[thing.Id] = thing;

            return thing;
        }

        public void Join(Thing a, Thing b)
        {
            a.Joined.Add(b.Id);
            b.Joined.Add(a.Id);
        }
    }

    // ==================================================
    // The mirrored decisions
    // ==================================================

    private static double Growth(
        double takeOffNow, double dnNow, double dnNew)
    {
        if (takeOffNow <= 0)
            return Math.Max(0, dnNew - dnNow) * 1.5;

        double grown = takeOffNow * (dnNew / dnNow);

        return Math.Max(0, grown - takeOffNow);
    }

    // The biggest size on a fitting now, which is what its length
    // follows: a DN80 x DN15 tee is a DN80 tee with a small branch.
    private static double Biggest(Model m, Thing fitting)
    {
        double biggest = 0;

        foreach (int id in fitting.Joined)
        {
            Thing n = m.Things[id];

            if (n.What == Kind.Pipe && n.Dn > biggest)
                biggest = n.Dn;
        }

        return biggest;
    }

    // What every pipe AROUND this run loses. Driven by the fittings
    // that grow, so it sees pipes that are in no run at all.
    private static Dictionary<int, double> Eaten(
        Model m, List<Thing> run, double dnNew)
    {
        var fittings = new HashSet<int>();

        foreach (Thing pipe in run)
        {
            foreach (int id in pipe.Joined)
            {
                Thing n = m.Things[id];

                if (n.What == Kind.Elbow || n.What == Kind.Tee)
                    fittings.Add(n.Id);
            }
        }

        var eaten = new Dictionary<int, double>();

        foreach (int id in fittings)
        {
            Thing f = m.Things[id];

            double before = Biggest(m, f);
            double after = Math.Max(before, dnNew);

            if (before <= 0 || after <= before)
                continue;

            double ratio = after / before;

            foreach (int joined in f.Joined)
            {
                Thing n = m.Things[joined];

                if (n.What != Kind.Pipe)
                    continue;

                double growth = f.TakeOff > 0
                    ? f.TakeOff * ratio - f.TakeOff
                    : (after - before) * 1.5;

                eaten[n.Id] = (eaten.ContainsKey(n.Id) ? eaten[n.Id] : 0)
                              + Math.Max(0, growth);
            }
        }

        return eaten;
    }

    // What one pipe is left short by, when it is the one being resized
    private static double? Need(Model m, Thing pipe, double dnNew)
    {
        Dictionary<int, double> eaten =
            Eaten(m, new List<Thing> { pipe }, dnNew);

        double gone = eaten.ContainsKey(pipe.Id) ? eaten[pipe.Id] : 0;

        double after = pipe.Length - gone;

        return after >= Floor ? (double?)null : Floor - after;
    }

    private class Shift
    {
        public List<int> Moving = new List<int>();

        // Each one is a pipe that STAYS and has to stretch to follow
        public List<int> Seams = new List<int>();

        public string Route = string.Empty;
    }

    // Two ends, and pipework in its own right: an elbow, a union, a
    // valve in line. Three ends has a branch that would be left behind.
    private static bool Movable(Thing thing)
    {
        return (thing.What == Kind.Elbow || thing.What == Kind.Valve)
               && thing.Joined.Count == 2;
    }

    private static bool IsTee(Thing thing)
    {
        return (thing.What == Kind.Tee || thing.What == Kind.Elbow
                || thing.What == Kind.Valve)
               && thing.Joined.Count > 2;
    }

    // The gentle route: slide the leg, and let the first pipe in line
    // with the short one give up the same length.
    private static Shift Leg(Model m, Thing pipe, int throughId)
    {
        Thing near = m.Things[throughId];

        // A tee slides too, if its branch can come along
        if ((near.What == Kind.Elbow || near.What == Kind.Tee)
            && near.Joined.Count > 2)
            return Teed(m, pipe, near);

        if (!Movable(near))
            return null;

        var shift = new Shift { Route = "leg" };

        shift.Seams.Add(pipe.Id);
        shift.Moving.Add(near.Id);

        Thing from = near;
        int cameFrom = pipe.Id;

        for (int step = 0; step < Reach; step++)
        {
            Thing next = Onward(m, from, cameFrom);

            if (next == null)
                return null;

            // Round the houses and back to the pipe being saved: a ring,
            // not a pipe in line with itself
            if (next.Id == pipe.Id)
                return null;

            if (next.What == Kind.Pipe
                && Math.Abs(next.Dot(pipe)) > InLine)
            {
                shift.Seams.Add(next.Id);

                return shift;
            }

            if (next.What != Kind.Pipe && !Movable(next))
            {
                // A tee can be crossed if its branch comes along
                if (!Bring(m, next, from.Id, pipe, shift))
                    return null;
            }

            shift.Moving.Add(next.Id);

            cameFrom = from.Id;
            from = next;
        }

        return null;
    }

    // Sliding a TEE along the main it sits on: the short pipe grows,
    // the main gives up the same length, and the branch comes along.
    private static Shift Teed(Model m, Thing pipe, Thing near)
    {
        Thing giver = null;

        foreach (int id in near.Joined)
        {
            Thing n = m.Things[id];

            if (n.Id == pipe.Id)
                continue;

            if (n.What == Kind.Pipe && Math.Abs(n.Dot(pipe)) > InLine)
            {
                giver = n;

                break;
            }
        }

        if (giver == null)
            return null;

        var shift = new Shift { Route = "tee" };

        shift.Moving.Add(near.Id);

        shift.Seams.Add(pipe.Id);
        shift.Seams.Add(giver.Id);

        var blocked = new HashSet<int> { pipe.Id, giver.Id };

        foreach (int id in near.Joined)
        {
            Thing hanging = m.Things[id];

            if (hanging.Id == pipe.Id || hanging.Id == giver.Id)
                continue;

            if (!DeadEnd(m, hanging, near.Id, blocked, shift.Moving))
                return null;
        }

        return shift;
    }

    // Everything hanging off here has to be a dead end that can move
    private static bool DeadEnd(
        Model m, Thing start, int from, HashSet<int> blocked, List<int> into)
    {
        var seen = new HashSet<int> { from, start.Id };
        var queue = new Queue<Thing>();

        queue.Enqueue(start);

        int count = 0;

        while (queue.Count > 0)
        {
            Thing one = queue.Dequeue();

            if (blocked.Contains(one.Id))
                return false;

            if (++count > Bundle)
                return false;

            // Anything that is pipework moves, whatever shape - a cap
            // has ONE end and is the very definition of a dead end. Only
            // equipment stays put. (This used to demand two ends or
            // more, and turned a cap away as though it were a chiller.)
            if (one.What == Kind.Equipment)
                return false;

            into.Add(one.Id);

            foreach (int id in one.Joined)
            {
                Thing next = m.Things[id];

                if (next.Id == from)
                    continue;

                if (blocked.Contains(next.Id))
                    return false;

                if (seen.Add(next.Id))
                    queue.Enqueue(next);
            }
        }

        return true;
    }

    // The other route: push a dead-end branch along bodily
    private static Shift Branch(Model m, Thing pipe, int throughId)
    {
        Thing near = m.Things[throughId];

        if (!Movable(near))
            return null;

        var shift = new Shift { Route = "branch" };

        shift.Seams.Add(pipe.Id);

        var found = new List<int>();
        var seen = new HashSet<int> { near.Id };
        var queue = new Queue<Thing>();

        queue.Enqueue(near);

        while (queue.Count > 0)
        {
            Thing one = queue.Dequeue();

            found.Add(one.Id);

            if (found.Count > Bundle)
                return null;

            // Equipment stays where it was put
            if (one.What != Kind.Pipe && !Movable(one) && !IsTee(one))
                return null;

            foreach (int id in one.Joined)
            {
                Thing next = m.Things[id];

                // The fitting on the end of the short pipe is joined to
                // it - that is the joint being let go of. Reaching the
                // short pipe from anywhere ELSE is a ring, not a dead end,
                // and pushing it would pull the far side apart. (The copy
                // used to refuse both, which made this route dead code.)
                if (next.Id == pipe.Id)
                {
                    if (one.Id != near.Id)
                        return null;

                    continue;
                }

                if (seen.Add(next.Id))
                    queue.Enqueue(next);
            }
        }

        shift.Moving.AddRange(found);

        return shift;
    }

    // With three connectors there is no single "other side", so it
    // takes the one most nearly in line with the way it came in: the
    // through path of a tee rather than its branch.
    private static Thing Onward(Model m, Thing from, int cameFrom)
    {
        Thing came = m.Things[cameFrom];

        Thing best = null;

        double straightest = -1;

        foreach (int id in from.Joined)
        {
            if (id == cameFrom)
                continue;

            Thing next = m.Things[id];

            double how = Math.Abs(next.Dot(came));

            if (how > straightest)
            {
                straightest = how;

                best = next;
            }
        }

        return best;
    }

    // Can this tee be crossed on the way to a pipe in line? Only if
    // what hangs off it is a dead end that will slide along with it.
    private static bool Bring(
        Model m, Thing tee, int cameFrom, Thing pipe, Shift shift)
    {
        if (tee.What == Kind.Equipment)
            return false;

        Thing came = m.Things[cameFrom];

        // The way through: most nearly in line with the way we came in
        Thing through = null;

        double straightest = -1;

        foreach (int id in tee.Joined)
        {
            if (id == cameFrom)
                continue;

            Thing next = m.Things[id];

            double how = Math.Abs(next.Dot(came));

            if (how > straightest)
            {
                straightest = how;

                through = next;
            }
        }

        if (through == null)
            return false;

        var blocked = new HashSet<int>(shift.Moving) { pipe.Id, cameFrom };

        foreach (int id in tee.Joined)
        {
            if (id == cameFrom || id == through.Id)
                continue;

            if (!DeadEnd(m, m.Things[id], tee.Id, blocked, shift.Moving))
                return false;
        }

        return true;
    }

    // Every pipe that has to change length must still be long enough
    private static bool Survives(Model m, Shift shift, Thing pipe, double by)
    {
        foreach (int id in shift.Seams)
        {
            // The short pipe itself only ever gets longer
            if (id == pipe.Id)
                continue;

            if (m.Things[id].Length - by < Floor)
                return false;
        }

        return true;
    }

    // The GENTLE route on both ends before the other on either: letting
    // the next pipe in line give up the length moves one leg and nothing
    // else, while pushing a branch along moves everything hanging off it
    private static Shift Make(Model m, Thing pipe, double by)
    {
        foreach (int id in pipe.Joined)
        {
            Shift shift = Leg(m, pipe, id);

            if (shift != null && Survives(m, shift, pipe, by))
                return shift;
        }

        foreach (int id in pipe.Joined)
        {
            Shift shift = Branch(m, pipe, id);

            if (shift != null && Survives(m, shift, pipe, by))
                return shift;
        }

        return null;
    }

    private static bool Same(List<int> got, params int[] want)
    {
        if (got.Count != want.Length)
            return false;

        var left = new List<int>(got);

        foreach (int one in want)
        {
            if (!left.Remove(one))
                return false;
        }

        return true;
    }

    // ==================================================

    private static int Main()
    {
        Console.WriteLine("CHW room-for-the-fittings harness");
        Console.WriteLine();

        // --- the case from the model ---------------------------------
        // A --e1-- B(60mm jog) --e2-- C --e3-- D
        // A and C run east; B and D run up.
        {
            var m = new Model();

            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 2000);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 60);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 800);
            Thing e3 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing d = m.Add(Kind.Pipe, 0, 0, 1, 1500);

            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, e2);
            m.Join(e2, c);
            m.Join(c, e3);
            m.Join(e3, d);

            double? need = Need(m, b, 80);

            Check("a 60 mm jog cannot survive DN20 going to DN80",
                need.HasValue);

            // 30 mm of take-off at DN20 becomes 120 at DN80: 90 of
            // growth at each end, so 60 - 180 = -120, and it needs 122.5
            // to reach the 2.5 mm floor (125 when the floor was 5)
            Check("and the shortfall is worked out, not guessed  (122.5 mm)",
                need.HasValue && Math.Abs(need.Value - 122.5) < 0.001);

            Check("the same jog is fine going to DN25",
                !Need(m, b, 25).HasValue);

            Shift shift = Make(m, b, need.Value);

            Check("the fitting is slid, with its leg, to save the pipe",
                shift != null && shift.Route == "leg"
                && Same(shift.Moving, e2.Id, c.Id, e3.Id));

            Check("the short pipe and the pipe in line with it are the "
                  + "two that stretch",
                shift != null && Same(shift.Seams, b.Id, d.Id));
        }

        // THE FAR SIDE ENDS AT EQUIPMENT in the models below. A pipe
        // hanging in mid-air is itself a dead end, so the branch route
        // would push IT along and "succeed" through something no real
        // job has - which is how these checks failed once the walk was
        // put right. A pump at the end of A is the system it belongs to.

        // --- a dead end, with nothing in line ------------------------
        {
            var m = new Model();

            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 2000);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 60);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 800);
            Thing system = m.Add(Kind.Equipment, 1, 0, 0, 0);

            m.Join(system, a);
            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, e2);
            m.Join(e2, c);

            Shift shift = Make(m, b, 125);

            Check("with nothing in line, the dead end is pushed along "
                  + "instead",
                shift != null && shift.Route == "branch"
                && Same(shift.Moving, e2.Id, c.Id));

            Check("and only the short pipe stretches",
                shift != null && Same(shift.Seams, b.Id));
        }

        // --- what must never be pushed along -------------------------
        {
            var m = new Model();

            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 2000);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 60);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 800);
            Thing pump = m.Add(Kind.Equipment, 1, 0, 0, 0);
            Thing system = m.Add(Kind.Equipment, 1, 0, 0, 0);

            m.Join(system, a);
            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, e2);
            m.Join(e2, c);
            m.Join(c, pump);

            Check("a branch that ends at equipment is refused - the pump "
                  + "stays where it was put",
                Make(m, b, 125) == null);
        }

        {
            var m = new Model();

            // A ring: the branch comes back round to the short pipe
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 60);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 800);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing d = m.Add(Kind.Pipe, 0, 1, 0, 800);
            Thing e3 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);

            m.Join(b, e1);
            m.Join(e1, c);
            m.Join(c, e2);
            m.Join(e2, d);
            m.Join(d, e3);
            m.Join(e3, b);

            Check("a ring is refused - pushing it would pull its far "
                  + "side apart",
                Make(m, b, 125) == null);
        }

        {
            var m = new Model();

            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 60);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);

            m.Join(b, e1);

            Thing last = e1;

            // Twenty elements hanging off it, all perpendicular: more
            // than may be shoved along to save one pipe
            for (int i = 0; i < 10; i++)
            {
                Thing leg = i % 2 == 0
                    ? m.Add(Kind.Pipe, 1, 0, 0, 500)
                    : m.Add(Kind.Pipe, 0, 1, 0, 500);

                Thing elbow = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);

                m.Join(last, leg);
                m.Join(leg, elbow);

                last = elbow;
            }

            Check("pushing a large part of the system along is refused",
                Make(m, b, 125) == null);
        }

        // --- the giver has to be able to give ------------------------
        {
            var m = new Model();

            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 2000);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 60);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 800);
            Thing e3 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);

            // Only 100 mm to give, and 125 is wanted
            Thing d = m.Add(Kind.Pipe, 0, 0, 1, 100);

            // Both far ends at equipment: with either one loose, pushing
            // that dead end along would be a perfectly good answer
            Thing system = m.Add(Kind.Equipment, 1, 0, 0, 0);
            Thing unit = m.Add(Kind.Equipment, 0, 0, 1, 0);

            m.Join(system, a);
            m.Join(d, unit);
            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, e2);
            m.Join(e2, c);
            m.Join(c, e3);
            m.Join(e3, d);

            Check("saving one pipe by making the next too short is "
                  + "refused",
                Make(m, b, 125) == null);
        }

        // --- a valve moves with its pipes, a tee does not ------------
        {
            var m = new Model();

            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 2000);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 60);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 400);
            Thing valve = m.Add(Kind.Valve, 1, 0, 0, 0, 20);
            Thing c2 = m.Add(Kind.Pipe, 1, 0, 0, 400);
            Thing e3 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing d = m.Add(Kind.Pipe, 0, 0, 1, 1500);

            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, e2);
            m.Join(e2, c);
            m.Join(c, valve);
            m.Join(valve, c2);
            m.Join(c2, e3);
            m.Join(e3, d);

            Shift shift = Make(m, b, 125);

            Check("a valve in line slides along with its own pipes",
                shift != null && shift.Moving.Contains(valve.Id));
        }

        {
            var m = new Model();

            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 2000);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 60);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 800);
            Thing tee = m.Add(Kind.Tee, 0, 0, 0, 0, 40);
            Thing d = m.Add(Kind.Pipe, 0, 0, 1, 1500);
            Thing branch = m.Add(Kind.Pipe, 0, 1, 0, 3000);
            Thing pump = m.Add(Kind.Equipment, 0, 1, 0, 0);
            Thing system = m.Add(Kind.Equipment, 1, 0, 0, 0);

            m.Join(system, a);
            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, e2);
            m.Join(e2, c);
            m.Join(c, tee);
            m.Join(tee, d);
            m.Join(tee, branch);
            m.Join(branch, pump);

            // The leg route stops at the tee, and the branch route
            // reaches the pump behind it
            Check("a tee whose branch runs off to equipment is refused",
                Make(m, b, 125) == null);
        }

        // --- a cap is a dead end, not an obstacle ---------------------
        // A short pipe off a tee; the tee's drop ends in a CAP. Sliding
        // the tee along the main brings the drop - cap and all. The walk
        // used to demand two ends or more and turned the cap away as
        // though it were a chiller (found in a real model).
        {
            var m = new Model();

            Thing system = m.Add(Kind.Equipment, 1, 0, 0, 0);
            Thing mainW = m.Add(Kind.Pipe, 1, 0, 0, 3000);
            Thing tee = m.Add(Kind.Tee, 0, 0, 0, 0, 40);
            Thing p = m.Add(Kind.Pipe, 1, 0, 0, 60);
            Thing e = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing up = m.Add(Kind.Pipe, 0, 0, 1, 1500);
            Thing unit = m.Add(Kind.Equipment, 0, 0, 1, 0);
            Thing drop = m.Add(Kind.Pipe, 0, 1, 0, 600);
            Thing cap = m.Add(Kind.Cap, 0, 0, 0, 0, 10);

            m.Join(system, mainW);
            m.Join(mainW, tee);
            m.Join(tee, p);
            m.Join(tee, drop);
            m.Join(drop, cap);
            m.Join(p, e);
            m.Join(e, up);
            m.Join(up, unit);

            Shift shift = Make(m, p, 20);

            Check("a cap on the end of a tee's drop slides along with it",
                shift != null && shift.Route == "tee"
                && shift.Moving.Contains(cap.Id) && shift.Moving.Contains(drop.Id));

            Check("...and the main in line with the short pipe gives up the length",
                shift != null && Same(shift.Seams, p.Id, mainW.Id));
        }

        // --- a pipe between two tees, in NEITHER run ------------------
        // mainW --Tee1-- P --Tee2-- mainE, with a drop off each tee.
        // Runs stop at tees, so P is a run of its own and is not being
        // resized at all - but sizing the main up grows both tees and
        // they eat P from both ends. This is what was reported.
        {
            var m = new Model();

            Thing mainW = m.Add(Kind.Pipe, 1, 0, 0, 3000);
            Thing tee1 = m.Add(Kind.Tee, 0, 0, 0, 0, 40);
            Thing p = m.Add(Kind.Pipe, 1, 0, 0, 100);
            Thing tee2 = m.Add(Kind.Tee, 0, 0, 0, 0, 40);
            Thing mainE = m.Add(Kind.Pipe, 1, 0, 0, 3000);

            Thing drop1 = m.Add(Kind.Pipe, 0, 0, 1, 600);
            Thing drop2 = m.Add(Kind.Pipe, 0, 0, 1, 600);

            m.Join(mainW, tee1);
            m.Join(tee1, p);
            m.Join(tee1, drop1);
            m.Join(p, tee2);
            m.Join(tee2, mainE);
            m.Join(tee2, drop2);

            // Resizing the WEST main only
            Dictionary<int, double> eaten =
                Eaten(m, new List<Thing> { mainW }, 80);

            Check("the pipe between two tees is seen, though it is in "
                  + "neither run and is not being resized",
                eaten.ContainsKey(p.Id) && p.Length - eaten[p.Id] < Floor);

            Check("and so is the drop hanging off the tee",
                eaten.ContainsKey(drop1.Id));

            // 40 mm of take-off at DN20 becomes 160 at DN80: 120 gone
            Check("with the loss worked out from the tee, not the pipe "
                  + "being resized  (120 mm)",
                Math.Abs(eaten[p.Id] - 120) < 0.001);

            // And it can be saved: slide tee1 west along the main
            Shift shift = Make(m, p, 25);

            Check("the tee slides along the main, bringing its drop",
                shift != null && shift.Route == "tee"
                && shift.Moving.Contains(tee1.Id)
                && shift.Moving.Contains(drop1.Id));

            Check("the short pipe grows and the main gives up the same",
                shift != null && Same(shift.Seams, p.Id, mainW.Id));
        }

        // --- but not when the drop goes somewhere that must stay -----
        {
            var m = new Model();

            Thing mainW = m.Add(Kind.Pipe, 1, 0, 0, 3000);
            Thing tee1 = m.Add(Kind.Tee, 0, 0, 0, 0, 40);
            Thing p = m.Add(Kind.Pipe, 1, 0, 0, 100);
            Thing tee2 = m.Add(Kind.Tee, 0, 0, 0, 0, 40);
            Thing mainE = m.Add(Kind.Pipe, 1, 0, 0, 3000);

            Thing drop1 = m.Add(Kind.Pipe, 0, 0, 1, 600);
            Thing fcu1 = m.Add(Kind.Equipment, 0, 0, 1, 0);
            Thing drop2 = m.Add(Kind.Pipe, 0, 0, 1, 600);
            Thing fcu2 = m.Add(Kind.Equipment, 0, 0, 1, 0);

            m.Join(mainW, tee1);
            m.Join(tee1, p);
            m.Join(tee1, drop1);
            m.Join(drop1, fcu1);
            m.Join(p, tee2);
            m.Join(tee2, mainE);
            m.Join(tee2, drop2);
            m.Join(drop2, fcu2);

            Check("a tee whose drop feeds equipment cannot be slid - the "
                  + "unit stays where it was put",
                Make(m, p, 25) == null);
        }

        // --- anchored at equipment ------------------------------------
        // WSHP -- A(short) --e1-- B(drop) --e2-- C ------- plant
        // A and C run east, B runs down. The unit cannot move, and it
        // does not have to: sliding e1 away lengthens A, and C gives up
        // the same length while its far end stays on the plant.
        {
            var m = new Model();

            Thing wshp = m.Add(Kind.Equipment, 1, 0, 0, 0);
            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 40);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 800);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 1500);
            Thing plant = m.Add(Kind.Equipment, 1, 0, 0, 0);

            m.Join(wshp, a);
            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, e2);
            m.Join(e2, c);
            m.Join(c, plant);

            Shift shift = Make(m, a, 60);

            Check("a pipe anchored at equipment is lengthened from its "
                  + "other end",
                shift != null && shift.Route == "leg");

            Check("the equipment itself is never moved",
                shift != null && !shift.Moving.Contains(wshp.Id)
                && !shift.Moving.Contains(plant.Id));

            Check("and the pipe that gives up the length is the one in "
                  + "line, whose far end stays on the plant",
                shift != null && Same(shift.Seams, a.Id, c.Id));
        }

        // --- crossing a tee to reach the giver ------------------------
        {
            var m = new Model();

            Thing wshp = m.Add(Kind.Equipment, 1, 0, 0, 0);
            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 40);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 400);
            Thing tee = m.Add(Kind.Tee, 0, 0, 0, 0, 40);
            Thing stub = m.Add(Kind.Pipe, 1, 0, 0, 200);
            Thing b2 = m.Add(Kind.Pipe, 0, 0, 1, 400);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 1500);
            Thing plant = m.Add(Kind.Equipment, 1, 0, 0, 0);

            m.Join(wshp, a);
            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, tee);
            m.Join(tee, stub);
            m.Join(tee, b2);
            m.Join(b2, e2);
            m.Join(e2, c);
            m.Join(c, plant);

            Shift shift = Make(m, a, 60);

            Check("the walk crosses a tee whose branch is a dead end",
                shift != null && shift.Moving.Contains(tee.Id)
                && shift.Moving.Contains(stub.Id));
        }

        {
            var m = new Model();

            Thing wshp = m.Add(Kind.Equipment, 1, 0, 0, 0);
            Thing a = m.Add(Kind.Pipe, 1, 0, 0, 40);
            Thing e1 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing b = m.Add(Kind.Pipe, 0, 0, 1, 400);
            Thing tee = m.Add(Kind.Tee, 0, 0, 0, 0, 40);
            Thing stub = m.Add(Kind.Pipe, 1, 0, 0, 200);
            Thing other = m.Add(Kind.Equipment, 1, 0, 0, 0);
            Thing b2 = m.Add(Kind.Pipe, 0, 0, 1, 400);
            Thing e2 = m.Add(Kind.Elbow, 0, 0, 0, 0, 30);
            Thing c = m.Add(Kind.Pipe, 1, 0, 0, 1500);
            Thing plant = m.Add(Kind.Equipment, 1, 0, 0, 0);

            m.Join(wshp, a);
            m.Join(a, e1);
            m.Join(e1, b);
            m.Join(b, tee);
            m.Join(tee, stub);
            m.Join(stub, other);
            m.Join(tee, b2);
            m.Join(b2, e2);
            m.Join(e2, c);
            m.Join(c, plant);

            Check("but not one whose branch runs to another unit",
                Make(m, a, 60) == null);
        }

        // --- the estimate when the fitting cannot be measured --------
        {
            Check("an unmeasurable fitting falls back on 1.5 times the "
                  + "change in diameter",
                Math.Abs(Growth(0, 20, 80) - 90) < 0.001);

            Check("a fitting that is already the right size grows by "
                  + "nothing",
                Math.Abs(Growth(30, 80, 80)) < 0.001);

            Check("going DOWN a size never counts as growth",
                Math.Abs(Growth(120, 80, 20)) < 0.001);
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
