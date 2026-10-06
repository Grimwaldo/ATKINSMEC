using System;
using System.Collections.Generic;

// Mirrors the run-grouping in QACHWRUNS - which pipes must be resized
// together - with no Revit present. Keep the two in step.
//
// The rule under test: a run continues through a direct pipe-to-pipe
// joint, and through a FITTING OR ACCESSORY that has exactly two piping
// ends of the same size. It stops at a tee or cross (three ends), at a
// transition (two ends, different sizes), and at equipment whatever its
// connectors look like.
//
// The case that matters most is the last one: seeding from two pipes in
// the same length must give ONE run, not two. Getting that wrong is
// what puts a reducer in the middle of a straight pipe.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    // ==================================================
    // A tiny model
    // ==================================================

    private enum Kind { Pipe, Fitting, Accessory, Equipment }

    private class End
    {
        public double Radius = 0.05;
        public int ToElement = -1;
        public int ToEnd = -1;
    }

    private class Thing
    {
        public int Id;
        public Kind What;
        public double Flow;
        public double Dn = 50;
        public List<End> Ends = new List<End>();
    }

    private class Model
    {
        public Dictionary<int, Thing> Things = new Dictionary<int, Thing>();

        public Thing Add(Kind what, int ends, double flow = 0,
            double dn = 50, double radius = 0.05)
        {
            var thing = new Thing
            {
                Id = Things.Count + 1,
                What = what,
                Flow = flow,
                Dn = dn
            };

            for (int i = 0; i < ends; i++)
                thing.Ends.Add(new End { Radius = radius });

            Things[thing.Id] = thing;

            return thing;
        }

        public void Join(Thing a, int ae, Thing b, int be)
        {
            a.Ends[ae].ToElement = b.Id;
            a.Ends[ae].ToEnd = be;

            b.Ends[be].ToElement = a.Id;
            b.Ends[be].ToEnd = ae;
        }
    }

    private class Run
    {
        public List<int> PipeIds = new List<int>();
        public double Litres;
        public double Dn;
        public bool Mixed;
        public List<int> Boundaries = new List<int>();
    }

    // ==================================================
    // The mirrored algorithm
    // ==================================================

    private static List<Run> Group(
        Model model, IEnumerable<int> seeds, double tolerance)
    {
        var done = new HashSet<int>();
        var runs = new List<Run>();

        foreach (int seed in seeds)
        {
            if (done.Contains(seed))
                continue;

            runs.Add(Walk(model, seed, done, tolerance));
        }

        return runs;
    }

    private static Run Walk(
        Model model, int from, HashSet<int> done, double tolerance)
    {
        var run = new Run();
        var queue = new Queue<int>();
        var sizes = new List<double>();

        queue.Enqueue(from);
        done.Add(from);

        while (queue.Count > 0)
        {
            int id = queue.Dequeue();

            Thing pipe = model.Things[id];

            run.PipeIds.Add(id);

            if (pipe.Flow > run.Litres)
                run.Litres = pipe.Flow;

            sizes.Add(pipe.Dn);

            foreach (End end in pipe.Ends)
            {
                if (end.ToElement < 0)
                    continue;

                Thing next = model.Things[end.ToElement];

                if (next.What == Kind.Pipe)
                {
                    if (done.Add(next.Id))
                        queue.Enqueue(next.Id);

                    continue;
                }

                int onward;

                if (Crossable(model, next, id, out onward))
                {
                    if (onward > 0 && done.Add(onward))
                        queue.Enqueue(onward);

                    if (next.What == Kind.Accessory
                        && !run.Boundaries.Contains(next.Id))
                        run.Boundaries.Add(next.Id);

                    continue;
                }

                if (!run.Boundaries.Contains(next.Id))
                    run.Boundaries.Add(next.Id);
            }
        }

        run.Dn = sizes.Count > 0 ? sizes[0] : 0;

        foreach (double size in sizes)
        {
            if (Math.Abs(size - run.Dn) > tolerance)
                run.Mixed = true;
        }

        return run;
    }

    private static bool Crossable(
        Model model, Thing fitting, int cameFrom, out int onward)
    {
        onward = 0;

        if (fitting.What != Kind.Fitting && fitting.What != Kind.Accessory)
            return false;

        if (fitting.Ends.Count != 2)
            return false;

        if (Math.Abs(fitting.Ends[0].Radius - fitting.Ends[1].Radius) > 1e-9)
            return false;

        foreach (End end in fitting.Ends)
        {
            if (end.ToElement < 0 || end.ToElement == cameFrom)
                continue;

            if (model.Things[end.ToElement].What == Kind.Pipe)
                onward = end.ToElement;
        }

        return true;
    }

    // ==================================================

    private static int Main()
    {
        Console.WriteLine("CHW run-grouping harness");
        Console.WriteLine();

        // --- a straight length through two elbows ---------------------
        {
            var m = new Model();

            Thing p1 = m.Add(Kind.Pipe, 2, 3.0);
            Thing e1 = m.Add(Kind.Fitting, 2);
            Thing p2 = m.Add(Kind.Pipe, 2, 3.0);
            Thing e2 = m.Add(Kind.Fitting, 2);
            Thing p3 = m.Add(Kind.Pipe, 2, 3.0);

            m.Join(p1, 1, e1, 0);
            m.Join(e1, 1, p2, 0);
            m.Join(p2, 1, e2, 0);
            m.Join(e2, 1, p3, 0);

            List<Run> runs = Group(m, new[] { p1.Id }, 2);

            Check("three pipes through two elbows are one run",
                runs.Count == 1 && runs[0].PipeIds.Count == 3);

            // The whole point
            List<Run> both = Group(m, new[] { p1.Id, p3.Id }, 2);

            Check("seeding from BOTH ends of that length still gives one "
                  + "run", both.Count == 1 && both[0].PipeIds.Count == 3);
        }

        // --- a tee splits it ------------------------------------------
        {
            var m = new Model();

            Thing p1 = m.Add(Kind.Pipe, 2, 10.0);
            Thing tee = m.Add(Kind.Fitting, 3);
            Thing p2 = m.Add(Kind.Pipe, 2, 7.0);
            Thing p3 = m.Add(Kind.Pipe, 2, 3.0);

            m.Join(p1, 1, tee, 0);
            m.Join(tee, 1, p2, 0);
            m.Join(tee, 2, p3, 0);

            List<Run> runs = Group(m, new[] { p1.Id, p2.Id, p3.Id }, 2);

            Check("a tee splits three pipes into three runs",
                runs.Count == 3);

            Check("and the tee is recorded as a boundary on each",
                runs.TrueForAll(r => r.Boundaries.Contains(tee.Id)));
        }

        // --- a transition splits it -----------------------------------
        {
            var m = new Model();

            Thing p1 = m.Add(Kind.Pipe, 2, 3.0, 50, 0.05);
            Thing red = m.Add(Kind.Fitting, 2);
            Thing p2 = m.Add(Kind.Pipe, 2, 3.0, 40, 0.04);

            red.Ends[0].Radius = 0.05;
            red.Ends[1].Radius = 0.04;

            m.Join(p1, 1, red, 0);
            m.Join(red, 1, p2, 0);

            List<Run> runs = Group(m, new[] { p1.Id, p2.Id }, 2);

            Check("a reducer is a deliberate size change, so it splits "
                  + "the run", runs.Count == 2);
        }

        // --- equipment is never crossed, even with two equal ends -----
        {
            var m = new Model();

            Thing p1 = m.Add(Kind.Pipe, 2, 20.0);
            Thing pump = m.Add(Kind.Equipment, 2);
            Thing p2 = m.Add(Kind.Pipe, 2, 20.0);

            m.Join(p1, 1, pump, 0);
            m.Join(pump, 1, p2, 0);

            List<Run> runs = Group(m, new[] { p1.Id, p2.Id }, 2);

            Check("a pump has two equal ends but is NOT crossed",
                runs.Count == 2);
        }

        // --- an in-line valve IS crossed, and noted -------------------
        {
            var m = new Model();

            Thing p1 = m.Add(Kind.Pipe, 2, 6.0);
            Thing valve = m.Add(Kind.Accessory, 2);
            Thing p2 = m.Add(Kind.Pipe, 2, 6.0);

            m.Join(p1, 1, valve, 0);
            m.Join(valve, 1, p2, 0);

            List<Run> runs = Group(m, new[] { p1.Id, p2.Id }, 2);

            Check("an in-line valve is crossed, so the pipe either side "
                  + "is one run", runs.Count == 1
                  && runs[0].PipeIds.Count == 2);

            Check("and the valve is noted, because its own size may not "
                  + "follow", runs[0].Boundaries.Contains(valve.Id));
        }

        // --- a ring main is a cycle and must terminate ----------------
        {
            var m = new Model();

            var pipes = new List<Thing>();
            var elbows = new List<Thing>();

            for (int i = 0; i < 4; i++)
            {
                pipes.Add(m.Add(Kind.Pipe, 2, 12.0));
                elbows.Add(m.Add(Kind.Fitting, 2));
            }

            for (int i = 0; i < 4; i++)
            {
                m.Join(pipes[i], 1, elbows[i], 0);
                m.Join(elbows[i], 1, pipes[(i + 1) % 4], 0);
            }

            List<Run> runs = Group(m, new[] { pipes[0].Id }, 2);

            Check("a ring main closes on itself and the walk terminates",
                runs.Count == 1 && runs[0].PipeIds.Count == 4);
        }

        // --- flow and mixed sizes -------------------------------------
        {
            var m = new Model();

            Thing p1 = m.Add(Kind.Pipe, 2, 4.0, 50);
            Thing e1 = m.Add(Kind.Fitting, 2);
            Thing p2 = m.Add(Kind.Pipe, 2, 9.5, 65);

            m.Join(p1, 1, e1, 0);
            m.Join(e1, 1, p2, 0);

            List<Run> runs = Group(m, new[] { p1.Id }, 2);

            Check("the run takes the LARGEST flow on it  (9.5, not 4.0)",
                Math.Abs(runs[0].Litres - 9.5) < 1e-9);

            Check("and notices the pipes do not currently agree on a size",
                runs[0].Mixed);
        }

        // --- a lone pipe ----------------------------------------------
        {
            var m = new Model();

            Thing p1 = m.Add(Kind.Pipe, 2, 1.0);

            List<Run> runs = Group(m, new[] { p1.Id }, 2);

            Check("a pipe joined to nothing is a run of one",
                runs.Count == 1 && runs[0].PipeIds.Count == 1
                && runs[0].Boundaries.Count == 0);
        }

        // --- no pipe is ever in two runs ------------------------------
        {
            var m = new Model();

            var all = new List<int>();

            Thing previous = null;

            for (int i = 0; i < 10; i++)
            {
                Thing pipe = m.Add(Kind.Pipe, 2, 5.0);

                all.Add(pipe.Id);

                if (previous != null)
                {
                    Thing elbow = m.Add(Kind.Fitting, 2);

                    m.Join(previous, 1, elbow, 0);
                    m.Join(elbow, 1, pipe, 0);
                }

                previous = pipe;
            }

            List<Run> runs = Group(m, all, 2);

            var seen = new HashSet<int>();
            bool once = true;

            foreach (Run run in runs)
            {
                foreach (int id in run.PipeIds)
                {
                    if (!seen.Add(id))
                        once = false;
                }
            }

            Check("seeding from all ten pipes of one length gives one run",
                runs.Count == 1 && runs[0].PipeIds.Count == 10);

            Check("and no pipe appears in two runs", once);
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
