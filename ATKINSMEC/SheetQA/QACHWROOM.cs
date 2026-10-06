using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Is there ROOM for the bigger fittings, and can it be made?
    //
    // A fitting takes up length along the pipe. A DN20 elbow turns the
    // corner in a few millimetres; a DN80 one needs far more. Resize a
    // short piece of pipe between two elbows and both ends eat into it
    // from opposite directions until there is nothing left, and Revit
    // says "Line is too short" and deletes it - reported from a real
    // model, where a jog between two elbows disappeared and left the
    // two fittings sitting against each other.
    //
    // Two answers, and the first one matters most:
    //
    //   1. KNOW BEFOREHAND. The room each fitting will need is worked
    //      out from the fitting that is already there - measure what it
    //      takes up now, scale it by how much the size is growing - so
    //      a run that cannot survive is refused whole, with nothing
    //      changed and nothing deleted.
    //
    //   2. MAKE THE ROOM by SLIDING THE FITTING AWAY from the pipe
    //      that would be lost, along that pipe's own line, so the pipe
    //      gets longer instead of shorter. The fitting is unhooked,
    //      moved and hooked back on - never deleted - so it keeps its
    //      id and whatever is written on it.
    //
    //      The length it takes has to come from somewhere. Either the
    //      first pipe IN LINE with the short one gives it up by being
    //      that much shorter, and nothing past that point moves at
    //      all; or, where there is no such pipe, a DEAD-END BRANCH is
    //      pushed along bodily, which moves nothing but itself.
    //
    //      Afterwards the model is asked, not trusted: every joint
    //      that existed before must still exist, and nothing may have
    //      been left too short. Either one fails and the whole thing
    //      goes back.
    //
    // Nothing here guesses at a route. If the shift cannot be absorbed
    // within a few elements, or it would drag a branch, a valve or a
    // piece of equipment, it is refused and the run is left alone.
    public class QACHWROOM
    {
        // How long a pipe has to end up.
        //
        // This was 5 mm, and a real model showed that was far too
        // strict: FOURTEEN runs in one pass were refused because a
        // 12 mm pipe would have ended at 4 mm - legal in Revit, whose
        // own minimum is about 1.2 mm, but under my floor. Refusing
        // that much work to avoid a millimetre is the wrong trade,
        // especially now that growing a fitting is guarded at the
        // moment it happens (QACHWFITTINGS.Squashed) rather than only
        // predicted beforehand.
        //
        // So: twice Revit's own minimum, or 2.5 mm, whichever is more.
        // Enough margin for the estimate to be slightly out, not so
        // much that it blocks the job.
        public static double Floor(Document doc)
        {
            double revits;

            try
            {
                revits = doc.Application.ShortCurveTolerance * 2.0;
            }
            catch
            {
                revits = 0;
            }

            return Math.Max(revits, QAGRIDS.ToFeet(2.5));
        }

        // Is this pipe now too short to be worth keeping - which is
        // Revit's cue to delete it?
        public static bool TooShort(Document doc, long pipeId)
        {
            var pipe = doc.GetElement(
                REVITCOMPAT.NewId(pipeId)) as MEPCurve;

            if (pipe == null)
                return false;

            double length = LengthOf(pipe);

            return length > 0 && length < Floor(doc);
        }

        // How far the walk will go looking for a pipe in line to give
        // up the length. A plain jog finds one in three elements; a
        // valve or an extra bend in the leg costs a couple more. Eight
        // covers those without an unbounded walk shifting half a floor.
        private const int Reach = 8;

        // And how much may be pushed along when there is nothing in
        // line to give up the length instead. A dead-end branch of a
        // dozen elements is a drop; a hundred is somebody's system.
        private const int Bundle = 12;

        // Two axes count as in line at cos(2 degrees) - the same number
        // QACHWFITTINGS uses, and Revit's own limit for an elbow
        private const double InLine = 0.9994;

        // ==================================================
        // 1. Knowing beforehand
        // ==================================================

        // One pipe that will not survive the resize
        public class Short
        {
            public long PipeId;
            public string Name = string.Empty;

            public double NowMm;
            public double AfterMm;

            // How much longer the pipe has to be made
            public double NeedFeet;
        }

        public class Verdict
        {
            public List<Short> Shorts = new List<Short>();

            // Every pipe this looked at, short or not. The fix checks
            // them all again after the resize, because a pipe can be
            // eaten from both ends by two different runs and neither
            // one on its own would predict it.
            public List<long> Watched = new List<long>();

            public bool Enough
            {
                get { return Shorts.Count == 0; }
            }
        }

        // Read-only. What every pipe AROUND this run will be left with
        // once the fittings have grown.
        //
        // NOT just the pipes in the run, and that distinction cost a
        // pipe in a real model. A run STOPS at a tee, so a short piece
        // between two tees is a run of its own and may not be changing
        // size at all - but resizing the main either side makes both
        // tees grow, and they eat it from both ends. Looking only at
        // the pipes being resized never sees it coming.
        //
        // So this is driven by the FITTINGS that are about to grow, and
        // it weighs every pipe on them.
        public static Verdict Room(
            Document doc, QACHWRUNS.Run run, double newDnMm)
        {
            var verdict = new Verdict();

            // Each fitting once, however many of the run's pipes touch
            // it - an elbow inside a run is reached from both sides
            var fittings = new HashSet<long>();

            foreach (long id in run.PipeIds)
            {
                Element pipe = doc.GetElement(REVITCOMPAT.NewId(id));

                if (pipe == null)
                    continue;

                foreach (Connector end in QACHWRUNS.Ends(pipe))
                {
                    Element fitting = Beyond(end, id);

                    if (fitting != null && QACHWRUNS.IsFitting(fitting))
                        fittings.Add(fitting.Id.ToValue());
                }
            }

            var eaten = new Dictionary<long, double>();

            foreach (long id in fittings)
                Eats(doc, id, newDnMm, eaten);

            double floor = Floor(doc);

            foreach (KeyValuePair<long, double> pair in eaten)
            {
                verdict.Watched.Add(pair.Key);

                var pipe = doc.GetElement(
                    REVITCOMPAT.NewId(pair.Key)) as MEPCurve;

                if (pipe == null)
                    continue;

                double nowFeet = LengthOf(pipe);

                if (nowFeet <= 0)
                    continue;

                double afterFeet = nowFeet - pair.Value;

                if (afterFeet >= floor)
                    continue;

                verdict.Shorts.Add(new Short
                {
                    PipeId = pair.Key,
                    Name = QADUCTS.Name(pipe),
                    NowMm = QAGRIDS.FromFeet(nowFeet),
                    AfterMm = QAGRIDS.FromFeet(afterFeet),
                    NeedFeet = floor - afterFeet
                });
            }

            return verdict;
        }

        // What one fitting takes away from each pipe on it, once the
        // run it sits on becomes newDnMm.
        private static void Eats(
            Document doc,
            long fittingId,
            double newDnMm,
            Dictionary<long, double> eaten)
        {
            Element fitting = doc.GetElement(REVITCOMPAT.NewId(fittingId));

            if (fitting == null)
                return;

            double before = Biggest(fitting);

            // A fitting is as long as the BIGGEST thing on it: a
            // DN80 x DN15 tee is a DN80 tee with a small branch, not a
            // small tee. So the whole fitting scales on the new size,
            // and every pipe on it loses room - including the ones that
            // are not changing size themselves.
            double after = Math.Max(before, newDnMm);

            if (before <= 0 || after <= before)
                return;

            double ratio = after / before;

            var point = fitting.Location as LocationPoint;

            foreach (Connector face in QACHWRUNS.Ends(fitting))
            {
                Element next = Beyond(face, fittingId);

                if (!(next is MEPCurve))
                    continue;

                double takesUpNow = point == null
                    ? 0
                    : Away(point.Point, face);

                // Nothing measurable to scale: a long-radius elbow
                // turns in about one and a half diameters, so the
                // growth is about one and a half times the change.
                double growth = takesUpNow > 0
                    ? takesUpNow * ratio - takesUpNow
                    : QAGRIDS.ToFeet((after - before) * 1.5);

                long id = next.Id.ToValue();

                eaten[id] = (eaten.ContainsKey(id) ? eaten[id] : 0)
                            + Math.Max(0, growth);
            }
        }

        // The biggest size on this fitting now, which is what its
        // length follows
        private static double Biggest(Element fitting)
        {
            double biggest = 0;

            foreach (Connector face in QACHWRUNS.Ends(fitting))
            {
                try
                {
                    double mm = QAGRIDS.FromFeet(face.Radius * 2.0);

                    if (mm > biggest)
                        biggest = mm;
                }
                catch
                {
                    // not round, so not a size this table knows about
                }
            }

            return biggest;
        }

        // How far the fitting reaches back down the pipe: from the
        // corner it turns about to the face it presents.
        private static double Away(XYZ from, Connector face)
        {
            try
            {
                return from.DistanceTo(face.Origin);
            }
            catch
            {
                return 0;
            }
        }

        // Called just before a fitting is GROWN, which is the moment
        // its neighbours are actually at risk.
        //
        // The pass over the runs predicts all this beforehand, but by
        // the time the fittings are sized the model has moved on: other
        // runs have been written, so a fitting can be about to grow
        // more than anything predicted. Asking again here, and making
        // the room here, is what stops a pipe being deleted rather than
        // merely noticing that it was.
        public static void Ensure(
            Document doc, long fittingId, double newDnMm, Result result)
        {
            var eaten = new Dictionary<long, double>();

            Eats(doc, fittingId, newDnMm, eaten);

            double floor = Floor(doc);

            foreach (KeyValuePair<long, double> pair in eaten)
            {
                var pipe = doc.GetElement(
                    REVITCOMPAT.NewId(pair.Key)) as MEPCurve;

                if (pipe == null)
                    continue;

                double nowFeet = LengthOf(pipe);

                if (nowFeet <= 0)
                    continue;

                double afterFeet = nowFeet - pair.Value;

                if (afterFeet >= floor)
                    continue;

                var shortfall = new Short
                {
                    PipeId = pair.Key,
                    Name = QADUCTS.Name(pipe),
                    NowMm = QAGRIDS.FromFeet(nowFeet),
                    AfterMm = QAGRIDS.FromFeet(afterFeet),
                    NeedFeet = floor - afterFeet
                };

                QACHWLOG.Say(
                    "      fitting " + fittingId + " is about to squash " +
                    "pipe " + pair.Key + "  (" +
                    shortfall.NowMm.ToString("N0") + " mm -> " +
                    shortfall.AfterMm.ToString("N0") +
                    " mm): making room first");

                QACHWLOG.Say(
                    Make(doc, shortfall, result)
                        ? "         room made"
                        : "         could not make room");
            }
        }

        // ==================================================
        // 2. Making the room
        // ==================================================

        public class Result
        {
            public int Stretched { get; set; }
            public int Refused { get; set; }

            public List<string> Notes { get; set; } = new List<string>();

            public void Note(string text)
            {
                if (!Notes.Contains(text))
                    Notes.Add(text);
            }
        }

        // What moves, and which joints have to be let go of and remade
        // to let it.
        //
        // NOTHING IS DELETED. The fitting on the end of the short pipe
        // is unhooked, slid along the pipe's own line, and hooked back
        // on - which is what a person does by dragging it. An earlier
        // version cut the fittings out and made new ones; this keeps
        // the fitting, whatever is written on it, and its id.
        private class Shift
        {
            public List<long> Moving = new List<long>();

            public List<Seam> Seams = new List<Seam>();
        }

        // One joint at the edge of what moves: the element on the far
        // side stays where it is, so its end has to follow along.
        private class Seam
        {
            public long StayId;
            public int StayConnector;

            public long MovingId;
            public int MovingConnector;
        }

        // Must be called inside a transaction, BEFORE the pipes are
        // resized: it is far easier to make room while the fittings are
        // still small than to put a pipe back after Revit has deleted
        // it. False means nothing was changed.
        public static bool Make(
            Document doc, Short shortfall, Result result)
        {
            var pipe = doc.GetElement(
                REVITCOMPAT.NewId(shortfall.PipeId)) as MEPCurve;

            if (pipe == null)
                return false;

            Line line = LineOf(pipe);

            if (line == null)
                return false;

            QACHWLOG.Say(
                "      making " +
                QAGRIDS.FromFeet(shortfall.NeedFeet).ToString("N0") +
                " mm for pipe " + shortfall.PipeId + "; its ends are on " +
                Neighbours(doc, pipe));

            // Either end will do, so try both. The GENTLE route is
            // tried on both ends before the other is tried on either:
            // letting the next pipe in line give up the length moves
            // one leg and nothing else, while pushing a branch along
            // moves everything hanging off it.
            foreach (Connector end in QACHWRUNS.Ends(pipe))
            {
                if (Push(doc, pipe, line, end, shortfall, result, true))
                {
                    result.Stretched++;

                    return true;
                }
            }

            foreach (Connector end in QACHWRUNS.Ends(pipe))
            {
                if (Push(doc, pipe, line, end, shortfall, result, false))
                {
                    result.Stretched++;

                    return true;
                }
            }

            result.Refused++;

            return false;
        }

        private static string List(List<long> ids)
        {
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < ids.Count && i < 10; i++)
            {
                if (i > 0)
                    text.Append(", ");

                text.Append(ids[i]);
            }

            if (ids.Count > 10)
                text.Append(", ...");

            return text.ToString();
        }

        // What is on the ends of this pipe, for the log: the commonest
        // reason a slide is impossible is what it is joined to, and
        // that is not something a harness can tell me.
        private static string Neighbours(Document doc, MEPCurve pipe)
        {
            var said = new List<string>();

            long id = pipe.Id.ToValue();

            foreach (Connector end in QACHWRUNS.Ends(pipe))
            {
                Element next = Beyond(end, id);

                if (next == null)
                {
                    said.Add("nothing");

                    continue;
                }

                string kind = QACHWRUNS.IsFitting(next)
                    ? "fitting"
                    : QACHWRUNS.IsAccessory(next) ? "accessory" : "other";

                said.Add(kind + " " + next.Id.ToValue() + " (" +
                         QACHWRUNS.Ends(next).Count + " ends, " +
                         QADUCTS.Name(next) + ")");
            }

            return said.Count == 0
                ? "nothing"
                : string.Join(" and ", said.ToArray());
        }

        private static bool Push(
            Document doc,
            MEPCurve pipe,
            Line line,
            Connector end,
            Short shortfall,
            Result result,
            bool gentle)
        {
            XYZ here;

            try
            {
                here = end.Origin;
            }
            catch
            {
                return false;
            }

            // Which way this end faces, away from the rest of the pipe:
            // the fitting is pushed OUTWARD, so the pipe gets longer
            XYZ outward = Outward(line, here);

            if (outward == null)
                return false;

            XYZ by = outward.Multiply(shortfall.NeedFeet);

            Shift shift = gentle
                ? Leg(doc, pipe, outward, end, result)
                : Branch(doc, pipe, end, result);

            if (shift == null)
                return false;

            if (!Survives(doc, shift, by, result))
                return false;

            return Slide(doc, shift, by, result);
        }

        // ==================================================
        // Where the room comes from
        // ==================================================

        // The gentle one: slide the leg beyond the short pipe, and let
        // the first pipe IN LINE with it take up the same length by
        // being that much shorter. Nothing past that pipe moves at all.
        private static Shift Leg(
            Document doc,
            MEPCurve pipe,
            XYZ axis,
            Connector end,
            Result result)
        {
            long pipeId = pipe.Id.ToValue();

            Element near = Beyond(end, pipeId);

            if (near == null)
                return null;

            // A TEE can be slid too, and this is the case that matters
            // most in practice: a short piece between two tees, on a
            // main that is being sized up. It only works if whatever
            // hangs off the tee is a dead end that can come along.
            if (QACHWRUNS.IsFitting(near) && QACHWRUNS.Ends(near).Count > 2)
                return Teed(doc, pipe, axis, end, near, result);

            if (!Movable(near))
            {
                QACHWLOG.Say(
                    "         leg: " + near.Id.ToValue() + " is not a " +
                    "two-ended piece of pipework, so it cannot be slid");

                return null;
            }

            var shift = new Shift();

            Seam first = SeamBetween(doc, pipeId, end, near);

            if (first == null)
                return null;

            shift.Seams.Add(first);
            shift.Moving.Add(near.Id.ToValue());

            Element from = near;
            long cameFrom = pipeId;

            for (int step = 0; step < Reach; step++)
            {
                Element next = Onward(doc, from, cameFrom);

                if (next == null)
                {
                    QACHWLOG.Say(
                        "         leg: ran out of pipework after " +
                        (step + 1) + " step(s) without finding one in line");

                    return null;
                }

                // Round the houses and back to the pipe being saved.
                // Without this it would be taken for a pipe in line
                // with itself and asked to give up its own length.
                if (next.Id.ToValue() == pipeId)
                {
                    QACHWLOG.Say(
                        "         leg: came back round to the pipe itself, " +
                        "so it is a ring rather than a length that can " +
                        "give anything up");

                    return null;
                }

                Line run = LineOf(next as MEPCurve);

                if (run != null
                    && Math.Abs(run.Direction.DotProduct(axis)) > InLine)
                {
                    // Found it. This pipe stays where it is and simply
                    // changes length.
                    Connector facing = Facing(next, from.Id.ToValue());

                    Connector mine = Facing(from, next.Id.ToValue());

                    if (facing == null || mine == null)
                        return null;

                    shift.Seams.Add(new Seam
                    {
                        StayId = next.Id.ToValue(),
                        StayConnector = facing.Id,
                        MovingId = from.Id.ToValue(),
                        MovingConnector = mine.Id
                    });

                    return shift;
                }

                if (!(next is MEPCurve) && !Movable(next))
                {
                    // A TEE can be crossed, as long as what hangs off
                    // it is a dead end that can slide along with it.
                    // Without this the walk gives up at the first tee,
                    // which on a branch to a piece of equipment is
                    // usually the only thing between the short pipe and
                    // a pipe in line that could give up the length.
                    if (!Bring(doc, next, from.Id.ToValue(), pipeId,
                            shift, result))
                    {
                        QACHWLOG.Say(
                            "         leg: blocked by " + next.Id.ToValue() +
                            " (" + QACHWRUNS.Ends(next).Count + " ends, " +
                            QADUCTS.Name(next) + ")");

                        result.Note(
                            "Some short pipes could not be saved by " +
                            "sliding a fitting, because a tee or a piece " +
                            "of equipment would have had to move with it. " +
                            "Nothing was changed there.");

                        return null;
                    }
                }

                shift.Moving.Add(next.Id.ToValue());

                cameFrom = from.Id.ToValue();
                from = next;
            }

            QACHWLOG.Say(
                "         leg: nothing in line within " + Reach +
                " elements");

            return null;
        }

        // Can this tee be crossed on the way to a pipe in line?
        //
        // Only if everything hanging off it is a dead end that will
        // slide along with it. The through path - the connector in line
        // with the one the walk came in on - carries on being walked;
        // every other branch has to come too, or it would be left
        // behind and torn off.
        private static bool Bring(
            Document doc,
            Element tee,
            long cameFrom,
            long pipeId,
            Shift shift,
            Result result)
        {
            if (!QACHWRUNS.IsFitting(tee) && !QACHWRUNS.IsAccessory(tee))
                return false;

            long teeId = tee.Id.ToValue();

            Connector inward = Facing(tee, cameFrom);

            if (inward == null)
                return false;

            XYZ backwards = Facing(inward);

            if (backwards == null)
                return false;

            // The way through: the connector most nearly opposite the
            // one we came in on
            Connector through = null;

            double straightest = -1;

            foreach (Connector face in QACHWRUNS.Ends(tee))
            {
                if (face.Id == inward.Id)
                    continue;

                XYZ way = Facing(face);

                if (way == null)
                    continue;

                double how = Math.Abs(way.DotProduct(backwards));

                if (how > straightest)
                {
                    straightest = how;

                    through = face;
                }
            }

            if (through == null)
                return false;

            var blocked = new HashSet<long>(shift.Moving) { pipeId, cameFrom };

            foreach (Connector face in QACHWRUNS.Ends(tee))
            {
                if (face.Id == inward.Id || face.Id == through.Id)
                    continue;

                Element hanging = Beyond(face, teeId);

                if (hanging == null)
                    continue;

                if (!DeadEnd(doc, hanging, teeId, blocked,
                        shift.Moving, result))
                    return false;
            }

            QACHWLOG.Say(
                "         leg: crossing tee " + teeId +
                ", bringing its branch along");

            return true;
        }

        // Sliding a TEE along the main it sits on.
        //
        // The short pipe is on one side of it, the main carries on out
        // of the other, and something hangs off the branch. Slide the
        // tee along the main: the short pipe grows, the main gives up
        // the same length, and the branch comes along for the ride.
        //
        // The branch is the whole question. A drop to nothing is fine.
        // A drop to a piece of equipment is not - the equipment stays
        // where it was put, so the tee cannot move away from it.
        private static Shift Teed(
            Document doc,
            MEPCurve pipe,
            XYZ axis,
            Connector end,
            Element near,
            Result result)
        {
            long pipeId = pipe.Id.ToValue();
            long nearId = near.Id.ToValue();

            Connector mine = Facing(near, pipeId);

            if (mine == null)
                return null;

            // The main: a pipe out of the tee in line with the short
            // one, which is what gives up the length
            Connector giverFace = null;
            Element giver = null;

            foreach (Connector face in QACHWRUNS.Ends(near))
            {
                if (face.Id == mine.Id)
                    continue;

                Element next = Beyond(face, nearId);

                Line along = LineOf(next as MEPCurve);

                if (along == null
                    || Math.Abs(along.Direction.DotProduct(axis)) <= InLine)
                    continue;

                giverFace = face;
                giver = next;

                break;
            }

            if (giver == null)
            {
                QACHWLOG.Say(
                    "         tee " + nearId + ": nothing out of it in " +
                    "line with the short pipe, so there is nothing to " +
                    "slide along");

                return null;
            }

            Connector giverEnd = Facing(giver, nearId);

            if (giverEnd == null)
                return null;

            var shift = new Shift();

            shift.Moving.Add(nearId);

            Seam first = SeamBetween(doc, pipeId, end, near);

            if (first == null)
                return null;

            shift.Seams.Add(first);

            shift.Seams.Add(new Seam
            {
                StayId = giver.Id.ToValue(),
                StayConnector = giverEnd.Id,
                MovingId = nearId,
                MovingConnector = giverFace.Id
            });

            // Everything else on the tee has to come with it
            var blocked = new HashSet<long> { pipeId, giver.Id.ToValue() };

            foreach (Connector face in QACHWRUNS.Ends(near))
            {
                if (face.Id == mine.Id || face.Id == giverFace.Id)
                    continue;

                Element hanging = Beyond(face, nearId);

                if (hanging == null)
                    continue;

                if (!DeadEnd(doc, hanging, nearId, blocked,
                        shift.Moving, result))
                {
                    QACHWLOG.Say(
                        "         tee " + nearId + ": what hangs off it (" +
                        hanging.Id.ToValue() + ") cannot come along");

                    return null;
                }
            }

            return shift;
        }

        // Collect everything hanging off here. False if it is not a
        // dead end that can move: equipment stays put, too much of it
        // is a route change, and anything that leads back to the pipes
        // being held still would tear.
        private static bool DeadEnd(
            Document doc,
            Element start,
            long from,
            HashSet<long> blocked,
            List<long> into,
            Result result)
        {
            var seen = new HashSet<long> { from, start.Id.ToValue() };

            var queue = new Queue<Element>();

            queue.Enqueue(start);

            int count = 0;

            while (queue.Count > 0)
            {
                Element one = queue.Dequeue();

                long oneId = one.Id.ToValue();

                if (blocked.Contains(oneId))
                {
                    QACHWLOG.Say(
                        "         branch: it loops back to " + oneId +
                        ", which has to stay where it is");

                    return false;
                }

                if (++count > Bundle)
                {
                    result.Note(
                        "Some short pipes could not be saved: the fitting " +
                        "on the end has too much hanging off it to bring " +
                        "along, which is a route change rather than a " +
                        "resize. Nothing was moved.");

                    return false;
                }

                // Anything that is pipework can move, whatever shape
                // it is: a cap has ONE end, an elbow two, a tee three,
                // and all of them slide along quite happily when the
                // whole branch goes together. Only EQUIPMENT stays put.
                //
                // This used to demand exactly two ends or more than
                // two, so a CAP - the definition of a dead end - was
                // turned away as though it were a chiller, and blocked
                // a branch that was otherwise free to move.
                if (!(one is MEPCurve)
                    && !QACHWRUNS.IsFitting(one)
                    && !QACHWRUNS.IsAccessory(one))
                {
                    QACHWLOG.Say(
                        "         branch: runs to " + oneId + " (" +
                        QADUCTS.Name(one) + "), which stays where it is");

                    result.Note(
                        "Some short pipes could not be saved because the " +
                        "branch off the fitting runs to equipment, which " +
                        "stays where it was put. Nothing was changed " +
                        "there.");

                    return false;
                }

                into.Add(oneId);

                foreach (Connector door in QACHWRUNS.Ends(one))
                {
                    Element next = Beyond(door, oneId);

                    if (next == null)
                        continue;

                    long nextId = next.Id.ToValue();

                    if (nextId == from)
                        continue;

                    // Round to something that is staying put
                    if (blocked.Contains(nextId))
                        return false;

                    if (seen.Add(nextId))
                        queue.Enqueue(next);
                }
            }

            return true;
        }

        // The other one: where nothing in line turns up, the whole
        // branch beyond the fitting can be pushed along instead - but
        // only if it is a dead end. Anything that carries on into the
        // rest of the system, or holds a piece of equipment, stays put.
        private static Shift Branch(
            Document doc, MEPCurve pipe, Connector end, Result result)
        {
            long pipeId = pipe.Id.ToValue();

            Element near = Beyond(end, pipeId);

            if (!Movable(near))
                return null;

            var shift = new Shift();

            Seam first = SeamBetween(doc, pipeId, end, near);

            if (first == null)
                return null;

            shift.Seams.Add(first);

            var found = new List<long>();
            var seen = new HashSet<long>();
            var queue = new Queue<Element>();

            queue.Enqueue(near);
            seen.Add(near.Id.ToValue());

            while (queue.Count > 0)
            {
                Element one = queue.Dequeue();

                found.Add(one.Id.ToValue());

                if (found.Count > Bundle)
                {
                    result.Note(
                        "Some short pipes could not be saved: keeping " +
                        "them would have meant pushing a large part of " +
                        "the system along, which is a route change " +
                        "rather than a resize. Nothing was moved.");

                    return null;
                }

                // Equipment stays where it was put. A pump or a chiller
                // is not something to shove along to make room.
                if (!(one is MEPCurve) && !Movable(one) && !IsTee(one))
                {
                    result.Note(
                        "Some short pipes could not be saved because a " +
                        "piece of equipment would have had to move. " +
                        "Nothing was changed there.");

                    return null;
                }

                foreach (Connector door in QACHWRUNS.Ends(one))
                {
                    Element next = Beyond(door, one.Id.ToValue());

                    if (next == null)
                        continue;

                    long nextId = next.Id.ToValue();

                    if (nextId == pipeId)
                    {
                        // The fitting on the end of the short pipe is
                        // joined to it - that is the whole point, and
                        // that joint is the one being let go of. But
                        // reaching the short pipe from anywhere ELSE
                        // means the branch comes back round to it: a
                        // ring, not a dead end, and pushing it would
                        // pull the far side apart.
                        if (one.Id.ToValue() != near.Id.ToValue())
                            return null;

                        continue;
                    }

                    if (seen.Add(nextId))
                        queue.Enqueue(next);
                }
            }

            shift.Moving.AddRange(found);

            return shift;
        }

        // A joint at the edge of what moves
        private static Seam SeamBetween(
            Document doc, long stayId, Connector stay, Element moving)
        {
            Connector theirs = Facing(moving, stayId);

            if (theirs == null)
                return null;

            try
            {
                return new Seam
                {
                    StayId = stayId,
                    StayConnector = stay.Id,
                    MovingId = moving.Id.ToValue(),
                    MovingConnector = theirs.Id
                };
            }
            catch
            {
                return null;
            }
        }

        // ==================================================
        // Doing it
        // ==================================================

        // Would every pipe that has to change length still be long
        // enough afterwards? Saving one pipe by making the next one too
        // short only moves the problem along.
        private static bool Survives(
            Document doc, Shift shift, XYZ by, Result result)
        {
            double floor = Floor(doc);

            foreach (Seam seam in shift.Seams)
            {
                var stay = doc.GetElement(
                    REVITCOMPAT.NewId(seam.StayId)) as MEPCurve;

                if (stay == null)
                    return false;

                Line line = LineOf(stay);

                Connector moving = QADUCTS.Lookup(
                    doc, seam.StayId, seam.StayConnector);

                if (line == null || moving == null)
                    return false;

                XYZ was;

                try
                {
                    was = moving.Origin;
                }
                catch
                {
                    return false;
                }

                XYZ other = Other(line, was);

                if (other == null)
                    return false;

                if (other.DistanceTo(was.Add(by)) < floor)
                {
                    QACHWLOG.Say(
                        "         the giver " + seam.StayId + " is only " +
                        QAGRIDS.FromFeet(line.Length).ToString("N0") +
                        " mm long and cannot give that up");

                    result.Note(
                        "Some short pipes could not be saved without " +
                        "making the next pipe along too short instead, " +
                        "so they were left alone. Those want a route " +
                        "change rather than a resize.");

                    return false;
                }
            }

            return true;
        }

        // Unhook, slide, stretch the ends that stayed, hook back up -
        // and then check the model agrees that nothing came apart.
        private static bool Slide(
            Document doc, Shift shift, XYZ by, Result result)
        {
            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    // Every joint anything involved in this has now,
                    // written down before it moves. Whatever this does,
                    // the model must end up joined the way it started.
                    var involved = new List<long>(shift.Moving);

                    foreach (Seam seam in shift.Seams)
                        involved.Add(seam.StayId);

                    List<QACHWRUNS.Joint> joints =
                        QACHWRUNS.JointsOf(doc, involved);

                    // Which way every pipe points, before anything
                    // moves. A slide must not turn anything.
                    var aimed = new Dictionary<long, XYZ>();

                    foreach (long id in involved)
                    {
                        Line was = LineOf(doc.GetElement(
                            REVITCOMPAT.NewId(id)) as MEPCurve);

                        if (was != null)
                            aimed[id] = was.Direction;
                    }

                    foreach (Seam seam in shift.Seams)
                    {
                        if (!Unhook(doc, seam))
                        {
                            QACHWLOG.Say(
                                "         could not unhook the joint at " +
                                seam.StayId);

                            sub.RollBack();

                            return false;
                        }
                    }

                    var moving = new List<ElementId>();

                    foreach (long id in shift.Moving)
                        moving.Add(REVITCOMPAT.NewId(id));

                    ElementTransformUtils.MoveElements(doc, moving, by);

                    // The ends that stayed behind follow their fitting,
                    // which is the whole point: the short pipe gets
                    // longer by exactly what was moved.
                    foreach (Seam seam in shift.Seams)
                    {
                        if (!Restretch(doc, seam.StayId,
                                seam.StayConnector, by))
                        {
                            QACHWLOG.Say(
                                "         could not restretch pipe " +
                                seam.StayId);

                            sub.RollBack();

                            return false;
                        }
                    }

                    doc.Regenerate();

                    foreach (Seam seam in shift.Seams)
                    {
                        if (!Hook(doc, seam))
                        {
                            QACHWLOG.Say(
                                "         moved, but the joint at " +
                                seam.StayId + " would not go back on");

                            sub.RollBack();

                            result.Note(
                                "A fitting was slid along to save a short " +
                                "pipe and would not hook back on, so it " +
                                "was put back where it was. Nothing is " +
                                "left disconnected.");

                            return false;
                        }
                    }

                    doc.Regenerate();

                    if (!Sound(doc, joints, involved, aimed, shift, result))
                    {
                        sub.RollBack();

                        return false;
                    }

                    sub.Commit();

                    QACHWLOG.Say(
                        "         slid " + shift.Moving.Count +
                        " element(s): " + List(shift.Moving));

                    return true;
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Note(
                        "Making room for the bigger fittings did not " +
                        "work: " + Why(ex) + ". That run was left " +
                        "exactly as it was.");

                    return false;
                }
            }
        }

        // The two things asked of this: no new disconnections, and no
        // new pipe too short to live. Checked against the model rather
        // than assumed from the arithmetic.
        private static bool Sound(
            Document doc,
            List<QACHWRUNS.Joint> joints,
            List<long> involved,
            Dictionary<long, XYZ> aimed,
            Shift shift,
            Result result)
        {
            // NOTHING MAY HAVE TURNED. A slide moves things along a
            // line; it never rotates anything. If a pipe is pointing
            // somewhere new, Revit has bent something to keep up -
            // which is how a slide can pass every other check and
            // still leave a visibly skewed elbow on the drawing.
            foreach (KeyValuePair<long, XYZ> pair in aimed)
            {
                Line now = LineOf(doc.GetElement(
                    REVITCOMPAT.NewId(pair.Key)) as MEPCurve);

                if (now == null)
                    continue;

                double straight;

                try
                {
                    straight = Math.Abs(now.Direction.DotProduct(pair.Value));
                }
                catch
                {
                    continue;
                }

                if (straight > 0.99999)
                    continue;

                QACHWLOG.Say(
                    "         pipe " + pair.Key + " was turned by the " +
                    "slide, so it was put back");

                result.Note(
                    "Sliding a fitting would have turned a pipe rather " +
                    "than just moving it, which leaves a bent-looking " +
                    "fitting on the drawing. It was put back exactly as " +
                    "it was.");

                return false;
            }

            // AND EVERY JOINT IT REMADE MUST BE A PROPER BUTT JOINT:
            // the two ends in the same place, facing each other. A
            // joint that is held but skewed is what draws a stretched
            // elbow.
            foreach (Seam seam in shift.Seams)
            {
                Connector stay = QADUCTS.Lookup(
                    doc, seam.StayId, seam.StayConnector);

                Connector moving = QADUCTS.Lookup(
                    doc, seam.MovingId, seam.MovingConnector);

                if (stay == null || moving == null)
                    continue;

                try
                {
                    double apart = stay.Origin.DistanceTo(moving.Origin);

                    double facing = stay.CoordinateSystem.BasisZ
                        .DotProduct(moving.CoordinateSystem.BasisZ);

                    if (apart <= QAGRIDS.ToFeet(0.5) && facing < -0.999)
                        continue;
                }
                catch
                {
                    continue;
                }

                QACHWLOG.Say(
                    "         the joint at " + seam.StayId + " came back " +
                    "together crooked, so it was put back");

                result.Note(
                    "Sliding a fitting left a joint meeting at an angle " +
                    "rather than square, which draws a stretched-looking " +
                    "fitting. It was put back exactly as it was.");

                return false;
            }

            foreach (QACHWRUNS.Joint joint in joints)
            {
                if (QACHWRUNS.StillJoined(doc, joint))
                    continue;

                result.Note(
                    "Sliding a fitting to save a short pipe would have " +
                    "left " + joint.Where + " disconnected, so it was " +
                    "put back exactly as it was.");

                return false;
            }

            foreach (long id in involved)
            {
                if (!TooShort(doc, id))
                    continue;

                result.Note(
                    "Sliding a fitting to save a short pipe would have " +
                    "made another one too short instead, so it was put " +
                    "back exactly as it was.");

                return false;
            }

            return true;
        }

        private static bool Unhook(Document doc, Seam seam)
        {
            Connector stay = QADUCTS.Lookup(
                doc, seam.StayId, seam.StayConnector);

            Connector moving = QADUCTS.Lookup(
                doc, seam.MovingId, seam.MovingConnector);

            if (stay == null || moving == null)
                return false;

            try
            {
                if (stay.IsConnectedTo(moving))
                    stay.DisconnectFrom(moving);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool Hook(Document doc, Seam seam)
        {
            Connector stay = QADUCTS.Lookup(
                doc, seam.StayId, seam.StayConnector);

            Connector moving = QADUCTS.Lookup(
                doc, seam.MovingId, seam.MovingConnector);

            if (stay == null || moving == null)
                return false;

            try
            {
                if (stay.IsConnectedTo(moving))
                    return true;

                stay.ConnectTo(moving);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Move one end of a pipe, keeping the other where it is
        private static bool Restretch(
            Document doc, long pipeId, int connectorId, XYZ by)
        {
            Connector moving = QADUCTS.Lookup(doc, pipeId, connectorId);

            if (moving == null)
                return false;

            var pipe = doc.GetElement(
                REVITCOMPAT.NewId(pipeId)) as MEPCurve;

            Line line = LineOf(pipe);

            if (line == null)
                return false;

            XYZ was;

            try
            {
                was = moving.Origin;
            }
            catch
            {
                return false;
            }

            XYZ start;
            XYZ finish;

            try
            {
                start = line.GetEndPoint(0);
                finish = line.GetEndPoint(1);
            }
            catch
            {
                return false;
            }

            var located = pipe.Location as LocationCurve;

            if (located == null)
                return false;

            // REWRITING A LINE MUST KEEP ITS DIRECTION. This used to
            // build (far end, moved end), which reverses the pipe
            // whenever the moving end was endpoint 0 - and a reversed
            // pipe is what Revit means by "modified to be in the
            // opposite direction causing the connections to be
            // invalid". Sound caught it here as "a pipe has turned" and
            // rolled the slide back, so it read as a route that simply
            // never worked. Found from the equipment changer's log,
            // where the same mistake threw out a whole transaction.
            XYZ moved = was.Add(by);

            located.Curve = EQUIPLINE.MovingIsStart(
                                start.DistanceTo(was),
                                finish.DistanceTo(was))
                ? Line.CreateBound(moved, finish)
                : Line.CreateBound(start, moved);

            return true;
        }

        // Pipework in its own right with exactly two ends - an elbow, a
        // union, a valve in line. Something with three has a branch
        // that would be left behind, and equipment does not move.
        private static bool Movable(Element element)
        {
            if (element == null)
                return false;

            if (!QACHWRUNS.IsFitting(element)
                && !QACHWRUNS.IsAccessory(element))
                return false;

            return QACHWRUNS.Ends(element).Count == 2;
        }

        private static bool IsTee(Element element)
        {
            if (element == null)
                return false;

            if (!QACHWRUNS.IsFitting(element)
                && !QACHWRUNS.IsAccessory(element))
                return false;

            return QACHWRUNS.Ends(element).Count > 2;
        }


        // ==================================================
        // Small geometry
        // ==================================================

        private static Line LineOf(MEPCurve pipe)
        {
            if (pipe == null)
                return null;

            var located = pipe.Location as LocationCurve;

            if (located == null)
                return null;

            try
            {
                return located.Curve as Line;
            }
            catch
            {
                return null;
            }
        }

        private static double LengthOf(MEPCurve pipe)
        {
            var located = pipe.Location as LocationCurve;

            if (located == null)
                return 0;

            try
            {
                return located.Curve.Length;
            }
            catch
            {
                return 0;
            }
        }

        // The end of the line that is not this one
        private static XYZ Other(Line line, XYZ one)
        {
            try
            {
                XYZ start = line.GetEndPoint(0);
                XYZ finish = line.GetEndPoint(1);

                return start.DistanceTo(one) <= finish.DistanceTo(one)
                    ? finish
                    : start;
            }
            catch
            {
                return null;
            }
        }

        // Which way this end faces, away from the rest of the pipe
        private static XYZ Outward(Line line, XYZ end)
        {
            XYZ other = Other(line, end);

            if (other == null)
                return null;

            try
            {
                XYZ along = end.Subtract(other);

                return along.GetLength() < 1e-9 ? null : along.Normalize();
            }
            catch
            {
                return null;
            }
        }

        // THE Domain TEST BELOW IS LOAD-BEARING, not tidiness.
        // Insulation is in AllRefs and PipeInsulation derives from
        // MEPCurve, so a cast cannot turn a sleeve away - see
        // QACHWJOINTFIX.Pipework, and EQUIPWALK.Across for the duct
        // side of the same thing. This walk escapes it only because it
        // asks the DOMAIN first, exactly as QACHWRUNS does. Take that
        // line out and this measures itself against fibreglass.
        private static Element Beyond(Connector connector, long from)
        {
            try
            {
                if (!connector.IsConnected)
                    return null;

                foreach (Connector other in connector.AllRefs)
                {
                    if (other.Owner == null
                        || other.Owner.Id.ToValue() == from
                        || other.Domain != Domain.DomainPiping
                        || other.ConnectorType != ConnectorType.End)
                        continue;

                    return other.Owner;
                }
            }
            catch
            {
                // nothing usable on this one
            }

            return null;
        }

        // What is on the other side of this fitting.
        //
        // With three connectors there is no single "other side", so it
        // takes the one most nearly opposite the way it came in - the
        // through path of a tee, rather than its branch. Anything else
        // would wander off down a branch while the walk believes it is
        // going straight on.
        private static Element Onward(
            Document doc, Element fitting, long cameFrom)
        {
            long id = fitting.Id.ToValue();

            Connector inward = Facing(fitting, cameFrom);

            XYZ backwards = inward == null ? null : Facing(inward);

            Element best = null;

            double straightest = -1;

            foreach (Connector end in QACHWRUNS.Ends(fitting))
            {
                Element next = Beyond(end, id);

                if (next == null || next.Id.ToValue() == cameFrom)
                    continue;

                if (backwards == null)
                    return next;

                XYZ way = Facing(end);

                double how = way == null
                    ? 0
                    : Math.Abs(way.DotProduct(backwards));

                if (how > straightest)
                {
                    straightest = how;

                    best = next;
                }
            }

            return best;
        }

        // Which way a connector faces
        private static XYZ Facing(Connector connector)
        {
            try
            {
                return connector.CoordinateSystem.BasisZ;
            }
            catch
            {
                return null;
            }
        }

        // The connector on this element that faces the given one
        private static Connector Facing(Element element, long towards)
        {
            foreach (Connector end in QACHWRUNS.Ends(element))
            {
                Element next = Beyond(end, element.Id.ToValue());

                if (next != null && next.Id.ToValue() == towards)
                    return end;
            }

            return null;
        }

        // Plain pipework in its own right, with two ends: an elbow or a
        // union. Not a tee, not a valve, not a pump.
        private static bool Crossable(Element element)
        {
            if (element == null || !QACHWRUNS.IsFitting(element))
                return false;

            return QACHWRUNS.Ends(element).Count == 2;
        }

        private static PartType KindOf(Element fitting)
        {
            var instance = fitting as FamilyInstance;

            if (instance == null)
                return PartType.Undefined;

            var mechanical = instance.MEPModel as MechanicalFitting;

            if (mechanical == null)
                return PartType.Undefined;

            try
            {
                return mechanical.PartType;
            }
            catch
            {
                return PartType.Undefined;
            }
        }

        private static string Why(Exception ex)
        {
            string message = (ex.Message ?? string.Empty).Trim();

            if (message.Length > 140)
                message = message.Substring(0, 137) + "...";

            return message.Length == 0 ? ex.GetType().Name : message;
        }
    }
}
