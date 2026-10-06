using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

// ESP calculation with the REAL ESPAIR, ESPNET and ESPSETTINGS compiled
// in. The air figures are checked against an independent Colebrook
// solution; the networks are built by hand so the flows, the index run,
// the joints between models and the flags can be asserted exactly.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static bool Near(double a, double b, double tol = 1e-6)
    {
        return Math.Abs(a - b) <= tol * Math.Max(1, Math.Abs(b));
    }

    // ---- building networks ----------------------------------------------

    private static long _id = 100;

    private static ESPNET.Part Part(ESPNET.Kind kind, long link = 0, string family = "")
    {
        return new ESPNET.Part
        {
            What = kind,
            LinkId = link,
            ElementId = _id++,
            Model = link == 0 ? "this model" : "link " + link,
            Family = family.Length > 0 ? family : kind.ToString()
        };
    }

    private static ESPNET.Port Port(
        ESPNET.Part part, double x, double y, double z, double dx, double dy, double dz,
        double diameter, string system = "Supply", string direction = "")
    {
        var port = new ESPNET.Port
        {
            Owner = part,
            ConnectorId = part.Ports.Count,
            X = x, Y = y, Z = z,
            DX = dx, DY = dy, DZ = dz,
            Section = ESPNET.Shape.Round,
            Diameter = diameter,
            System = system,
            Direction = direction
        };

        part.Ports.Add(port);

        return port;
    }

    // A straight round duct along X from x0 to x1 at y, ends joined later
    private static ESPNET.Part Duct(double x0, double x1, double y, double d, long link = 0)
    {
        ESPNET.Part duct = Part(ESPNET.Kind.Duct, link, "Round Duct");

        duct.Section = ESPNET.Shape.Round;
        duct.Diameter = d;

        double dir = Math.Sign(x1 - x0);

        ESPNET.Port a = Port(duct, x0, y, 0, -dir, 0, 0, d);
        ESPNET.Port b = Port(duct, x1, y, 0, dir, 0, 0, d);

        a.Along = 0;
        b.Along = Math.Abs(x1 - x0);

        return duct;
    }

    private static double Friction(double flow, double d, double length, ESPSETTINGS s, double roughnessMm = -1)
    {
        double v;

        return ESPAIR.FrictionPerMetre(
                   flow, ESPNET.Shape.Round, d, 0, 0,
                   (roughnessMm < 0 ? s.RoughnessMm : roughnessMm) / 1000, s.Density, out v)
               * length;
    }

    private static double Pv(double flow, double d, ESPSETTINGS s)
    {
        double v = flow / (Math.PI * d * d / 4);

        return ESPAIR.VelocityPressure(v, s.Density);
    }

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("ESP - air, networks, joints between models, flags");
        Console.WriteLine();

        Air();
        Simple();
        Taps();
        Bridges();
        Flags();
        Sides();
        Loops();
        Settings();

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "all checks passed" : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    // ---- the air side, against an independent Colebrook ----------------------

    private static void Air()
    {
        Console.WriteLine("  air");

        Check("friction factor Re 1e5, e/D 1e-4 = 0.0185139",
            Near(ESPAIR.FrictionFactor(1e5, 1e-4), 0.0185138661, 1e-7));
        Check("friction factor Re 5e5, e/D 1e-3 = 0.0202355",
            Near(ESPAIR.FrictionFactor(5e5, 1e-3), 0.0202354638, 1e-7));
        Check("friction factor smooth Re 2e4 = 0.0258831",
            Near(ESPAIR.FrictionFactor(2e4, 0), 0.0258830785, 1e-7));
        Check("friction factor Re 3e6, e/D 5e-5 = 0.0114582",
            Near(ESPAIR.FrictionFactor(3e6, 5e-5), 0.0114582254, 1e-7));
        Check("laminar below Re 2300 is 64/Re",
            Near(ESPAIR.FrictionFactor(1000, 1e-3), 0.064));

        double v;

        Check("400 mm round at 500 L/s, 0.09 mm: 0.4502 Pa/m (the duct chart says about 0.45)",
            Near(ESPAIR.FrictionPerMetre(0.5, ESPNET.Shape.Round, 0.4, 0, 0, 0.09e-3, 1.2, out v),
                 0.4501584641, 1e-6) && Near(v, 3.978874, 1e-6));

        Check("600x300 at 600 L/s: hydraulic diameter 0.4, 0.3254 Pa/m",
            Near(ESPAIR.HydraulicDiameter(ESPNET.Shape.Rectangular, 0, 0.6, 0.3), 0.4) &&
            Near(ESPAIR.FrictionPerMetre(0.6, ESPNET.Shape.Rectangular, 0, 0.6, 0.3, 0.09e-3, 1.2, out v),
                 0.3254022150, 1e-6));

        Check("flat oval 600x250: area and hydraulic diameter, either way round",
            Near(ESPAIR.Area(ESPNET.Shape.Oval, 0, 0.6, 0.25), 0.1365873852) &&
            Near(ESPAIR.Area(ESPNET.Shape.Oval, 0, 0.25, 0.6), 0.1365873852) &&
            Near(ESPAIR.HydraulicDiameter(ESPNET.Shape.Oval, 0, 0.6, 0.25), 0.3678135293));

        Check("no flow, no loss - and no division by zero",
            ESPAIR.FrictionPerMetre(0, ESPNET.Shape.Round, 0.4, 0, 0, 0.09e-3, 1.2, out v) == 0 &&
            ESPAIR.FrictionPerMetre(0.5, ESPNET.Shape.Round, 0, 0, 0, 0.09e-3, 1.2, out v) == 0);
    }

    // ---- unit -> duct -> tee -> two terminals -------------------------------------

    // Unit at x=0 blowing +X. Main 10 m Ø400 to a tee at x=10. Through:
    // 8 m Ø300 to an elbow, 3 m Ø300 to terminal T2 (0.3 m3/s). Branch:
    // 5 m Ø250 off in +Y to terminal T1 (0.2 m3/s).
    private class Job
    {
        public ESPNET.Part Unit, Main, Tee, Through, Elbow, Last, Branch, T1, T2;
        public List<ESPNET.Part> Parts = new List<ESPNET.Part>();
    }

    private static Job Build()
    {
        var job = new Job();

        job.Unit = Part(ESPNET.Kind.Equipment, 0, "FCU : 500");
        ESPNET.Port unitOut = Port(job.Unit, 0, 0, 0, 1, 0, 0, 0.4, "Supply", "Out");
        unitOut.Flow = 0.5;

        job.Main = Duct(0, 10, 0, 0.4);
        ESPNET.Join(unitOut, job.Main.Ports[0]);

        job.Tee = Part(ESPNET.Kind.Fitting, 0, "Tee : Standard");
        job.Tee.PartType = "Tee";
        ESPNET.Port tIn = Port(job.Tee, 10, 0, 0, -1, 0, 0, 0.4);
        ESPNET.Port tRun = Port(job.Tee, 10, 0, 0, 1, 0, 0, 0.3);
        ESPNET.Port tBranch = Port(job.Tee, 10, 0, 0, 0, 1, 0, 0.25);
        ESPNET.Join(job.Main.Ports[1], tIn);

        job.Through = Duct(10, 18, 0, 0.3);
        ESPNET.Join(tRun, job.Through.Ports[0]);

        job.Elbow = Part(ESPNET.Kind.Fitting, 0, "Elbow : Standard");
        job.Elbow.PartType = "Elbow";
        ESPNET.Port eIn = Port(job.Elbow, 18, 0, 0, -1, 0, 0, 0.3);
        ESPNET.Port eOut = Port(job.Elbow, 18, 0, 0, 0, 1, 0, 0.3);
        ESPNET.Join(job.Through.Ports[1], eIn);

        // 3 m up the Y axis from the elbow
        job.Last = Part(ESPNET.Kind.Duct, 0, "Round Duct");
        job.Last.Section = ESPNET.Shape.Round;
        job.Last.Diameter = 0.3;
        ESPNET.Port l0 = Port(job.Last, 18, 0, 0, 0, -1, 0, 0.3);
        ESPNET.Port l1 = Port(job.Last, 18, 3, 0, 0, 1, 0, 0.3);
        l1.Along = 3;
        ESPNET.Join(eOut, l0);

        job.T2 = Part(ESPNET.Kind.Terminal, 0, "Grille : 600");
        job.T2.Flow = 0.3;
        ESPNET.Join(l1, Port(job.T2, 18, 3, 0, 0, -1, 0, 0.3));

        job.Branch = Part(ESPNET.Kind.Duct, 0, "Round Duct");
        job.Branch.Section = ESPNET.Shape.Round;
        job.Branch.Diameter = 0.25;
        ESPNET.Port b0 = Port(job.Branch, 10, 0, 0, 0, -1, 0, 0.25);
        ESPNET.Port b1 = Port(job.Branch, 10, 5, 0, 0, 1, 0, 0.25);
        b1.Along = 5;
        ESPNET.Join(tBranch, b0);

        job.T1 = Part(ESPNET.Kind.Terminal, 0, "Grille : 400");
        job.T1.Flow = 0.2;
        ESPNET.Join(b1, Port(job.T1, 10, 5, 0, 0, -1, 0, 0.25));

        job.Parts.AddRange(new[] { job.Unit, job.Main, job.Tee, job.Through, job.Elbow, job.Last, job.T2, job.Branch, job.T1 });

        return job;
    }

    private static void Simple()
    {
        Console.WriteLine();
        Console.WriteLine("  one unit, a tee, two terminals");

        var s = new ESPSETTINGS();
        Job job = Build();

        ESPNET.Result result = ESPNET.Calculate(job.Unit, s);
        ESPNET.Run run = result.Runs.Single();

        Check("the run reaches both terminals and their flows add up at the unit",
            run.Connected && run.Terminals == 2 && Near(run.FlowAtEnds, 0.5));

        // Each path worked by hand from the same air functions
        double main = Friction(0.5, 0.4, 10, s);

        double tCthrough, tCbranch, eC;
        s.Coefficient("Tee - through", false, out tCthrough);
        s.Coefficient("Tee - branch", false, out tCbranch);
        s.Coefficient("Elbow", false, out eC);

        double toT2 = main
                      + tCthrough * Pv(0.3, 0.3, s)      // through, on the leaving leg
                      + Friction(0.3, 0.3, 8, s)
                      + eC * Math.Max(Pv(0.3, 0.3, s), Pv(0.3, 0.3, s))
                      + Friction(0.3, 0.3, 3, s)
                      + s.TerminalDropPa;

        double toT1 = main
                      + tCbranch * Pv(0.2, 0.25, s)      // branch, on the branch
                      + Friction(0.2, 0.25, 5, s)
                      + s.TerminalDropPa;

        Check("the index run is the worse of the two paths, to the pascal",
            Near(run.Index, Math.Max(toT1, toT2), 1e-9));

        Check("and it ends at the terminal on that path",
            run.EndsAt.StartsWith(toT2 >= toT1 ? "Grille : 600" : "Grille : 400"));

        Check("the main carries the whole flow, the through leg only its own",
            Near(run.Steps.First(st => st.What == "Duct").Flow, 0.5) &&
            run.Steps.Where(st => st.What == "Duct").Skip(1).All(st => st.Flow < 0.5));

        Check("the tee is read as 'through' or 'branch' from the path, not the family",
            run.Steps.Any(st => st.What == "Tee - through") || run.Steps.Any(st => st.What == "Tee - branch"));

        Check("the running total ends at the index figure",
            Near(run.Steps.Last().Running, run.Index, 1e-9));

        Check("a terminal with no pressure drop of its own is FLAGGED, at the default",
            run.Steps.Last().Flagged && Near(run.Steps.Last().Loss, s.TerminalDropPa));

        Check("ESP is that one outlet plus the margin",
            Near(result.Esp, run.Index) && Near(result.EspWithMargin, run.Index * 1.10));
    }

    // ---- a main with two taps along it ------------------------------------------

    private static void Taps()
    {
        Console.WriteLine();
        Console.WriteLine("  taps along a main");

        var s = new ESPSETTINGS();

        ESPNET.Part unit = Part(ESPNET.Kind.Equipment, 0, "AHU");
        ESPNET.Port u = Port(unit, 0, 0, 0, 1, 0, 0, 0.5, "Supply", "Out");

        // 20 m main with taps at 5 m and 12 m, capped at the end
        ESPNET.Part main = Part(ESPNET.Kind.Duct, 0, "Round Duct");
        main.Section = ESPNET.Shape.Round;
        main.Diameter = 0.5;
        ESPNET.Port m0 = Port(main, 0, 0, 0, -1, 0, 0, 0.5); m0.Along = 0;
        ESPNET.Port m1 = Port(main, 20, 0, 0, 1, 0, 0, 0.5); m1.Along = 20;
        ESPNET.Port c5 = Port(main, 5, 0, 0, 0, 1, 0, 0.5); c5.Along = 5; c5.IsCurve = true;
        ESPNET.Port c12 = Port(main, 12, 0, 0, 0, 1, 0, 0.5); c12.Along = 12; c12.IsCurve = true;
        ESPNET.Join(u, m0);

        var parts = new List<ESPNET.Part> { unit, main };

        Func<ESPNET.Port, double, double, ESPNET.Part> tapTo = (curve, x, flow) =>
        {
            ESPNET.Part tap = Part(ESPNET.Kind.Fitting, 0, "Takeoff");
            tap.PartType = "TapPerpendicular";
            ESPNET.Port onMain = Port(tap, x, 0, 0, 0, -1, 0, 0.2);
            ESPNET.Port outlet = Port(tap, x, 0, 0, 0, 1, 0, 0.2);
            ESPNET.Join(curve, onMain);

            ESPNET.Part terminal = Part(ESPNET.Kind.Terminal, 0, "Diffuser");
            terminal.Flow = flow;
            terminal.PressureDrop = 15;
            ESPNET.Join(outlet, Port(terminal, x, 0, 0, 0, -1, 0, 0.2));

            parts.Add(tap);
            parts.Add(terminal);

            return terminal;
        };

        tapTo(c5, 5, 0.25);
        tapTo(c12, 12, 0.15);

        ESPNET.Part cap = Part(ESPNET.Kind.Fitting, 0, "Cap");
        cap.PartType = "Cap";
        ESPNET.Join(m1, Port(cap, 20, 0, 0, -1, 0, 0, 0.5));

        ESPNET.Run run = ESPNET.Calculate(unit, s).Runs.Single();

        List<ESPNET.Step> ducts = run.Steps.Where(st => st.What == "Duct").ToList();

        Check("both taps are reached, and the flows add up",
            run.Terminals == 2 && Near(run.FlowAtEnds, 0.40));

        Check("the main is costed in pieces between the taps, each with what is still in it",
            ducts.Count >= 1 && Near(ducts[0].Length, 5) && Near(ducts[0].Flow, 0.40));

        double tapC;
        s.Coefficient("Tap", false, out tapC);

        double toFirst = Friction(0.40, 0.5, 5, s) + tapC * Pv(0.25, 0.2, s) + 15;
        double toSecond = Friction(0.40, 0.5, 5, s) + Friction(0.15, 0.5, 7, s) + tapC * Pv(0.15, 0.2, s) + 15;

        Check("the index run is the worse tap, to the pascal",
            Near(run.Index, Math.Max(toFirst, toSecond), 1e-9));

        Check("the family's own pressure drop is used, and not flagged",
            run.Steps.Last().Source == "the family's own pressure drop" && !run.Steps.Last().Flagged);

        Check("the capped end beyond the last tap is a dead end, not the index",
            run.DeadEnds == 1 && !run.EndsAt.Contains("open end"));
    }

    // ---- joints between models ----------------------------------------------------

    private static void Bridges()
    {
        Console.WriteLine();
        Console.WriteLine("  joints between models");

        var s = new ESPSETTINGS();

        // The AHU and its riser in link 7; the floor duct and terminal in
        // this model, starting 60 mm off the riser's open end
        ESPNET.Part ahu = Part(ESPNET.Kind.Equipment, 7, "AHU");
        ESPNET.Port up = Port(ahu, 0, 0, 0, 0, 0, 1, 0.5, "Supply", "Out");

        ESPNET.Part riser = Part(ESPNET.Kind.Duct, 7, "Riser");
        riser.Section = ESPNET.Shape.Round;
        riser.Diameter = 0.5;
        ESPNET.Port r0 = Port(riser, 0, 0, 0, 0, 0, -1, 0.5); r0.Along = 0;
        ESPNET.Port r1 = Port(riser, 0, 0, 30, 0, 0, 1, 0.5); r1.Along = 30;
        ESPNET.Join(up, r0);

        ESPNET.Part floor = Part(ESPNET.Kind.Duct, 0, "Floor duct");
        floor.Section = ESPNET.Shape.Round;
        floor.Diameter = 0.5;
        ESPNET.Port f0 = Port(floor, 0, 0, 30.06, 0, 0, -1, 0.5); f0.Along = 0;
        ESPNET.Port f1 = Port(floor, 0, 0, 34, 0, 0, 1, 0.5); f1.Along = 3.94;

        ESPNET.Part grille = Part(ESPNET.Kind.Terminal, 0, "Grille");
        grille.Flow = 0.6;
        ESPNET.Join(f1, Port(grille, 0, 0, 34, 0, 0, -1, 0.5));

        // A decoy in link 7 too, 40 mm off - same model as the riser, so it
        // must NOT be joined to it, however close
        ESPNET.Part decoy = Duct(1000, 1001, 0, 0.5, 7);
        decoy.Ports[0].X = 0; decoy.Ports[0].Y = 0; decoy.Ports[0].Z = 30.04;
        decoy.Ports[0].DX = 0; decoy.Ports[0].DY = 0; decoy.Ports[0].DZ = -1;

        var parts = new List<ESPNET.Part> { ahu, riser, floor, grille, decoy };

        List<ESPNET.Bridge> misses;
        List<ESPNET.Bridge> bridges = ESPNET.BridgeModels(parts, 0.150, out misses);

        Check("the riser's open end is joined to the floor duct in the other model",
            bridges.Count == 1 && r1.Bridged == f0 && f0.Bridged == r1 && Near(bridges[0].Gap, 0.06, 1e-9));

        Check("an end in the SAME model is never joined, however close",
            decoy.Ports[0].Bridged == null);

        ESPNET.Run run = ESPNET.Calculate(ahu, s).Runs.Single();

        Check("the run crosses into the other model and reaches the terminal there",
            run.Terminals == 1 && Near(run.FlowAtEnds, 0.6) && run.Bridges == 1);

        Check("the crossing is a step of its own, naming the model it goes into",
            run.Steps.Any(st => st.What == "Joint between models" && st.Source.Contains("this model")));

        Check("and it costs nothing - the riser and the floor duct are costed on their own lengths",
            Near(run.Index, Friction(0.6, 0.5, 30, s) + Friction(0.6, 0.5, 3.94, s) + s.TerminalDropPa, 1e-9));

        // Beyond the tolerance: not joined, but reported as a near miss
        f0.Z = 30.30;
        bridges = ESPNET.BridgeModels(parts, 0.150, out misses);

        Check("300 mm apart is beyond a 150 mm tolerance: not joined",
            bridges.Count == 0 && r1.Bridged == null);

        Check("but it is reported as a near miss, with its gap",
            misses.Any(m => (m.A == r1 || m.B == r1) && Near(m.Gap, 0.30, 1e-9)));

        ESPNET.Run cut = ESPNET.Calculate(ahu, s).Runs.Single();

        Check("and the run stops at the riser's open end, saying so",
            cut.Terminals == 0 && cut.DeadEnds == 1 && cut.EndsAt.Contains("open end"));

        // Facing the same way is not a joint
        f0.Z = 30.06;
        f0.DZ = 1;
        bridges = ESPNET.BridgeModels(parts, 0.150, out misses);

        // (The flipped end now faces the decoy, which is in another
        // model from it and rightly joined - the riser is what matters)
        Check("two ends pointing the same way are not joined",
            r1.Bridged == null && f0.Bridged != r1);

        f0.DZ = -1;

        // Two candidates for one end: the nearer wins
        ESPNET.Part rival = Part(ESPNET.Kind.Duct, 3, "Rival");
        rival.Section = ESPNET.Shape.Round;
        rival.Diameter = 0.5;
        ESPNET.Port rv = Port(rival, 0, 0, 30.10, 0, 0, -1, 0.5);
        parts.Add(rival);

        bridges = ESPNET.BridgeModels(parts, 0.150, out misses);

        Check("two ends in reach of one: the nearer is joined, the other left alone",
            r1.Bridged == f0 && rv.Bridged == null);

        Check("and the joints are made the same way every time",
            ESPNET.BridgeModels(parts, 0.150, out misses).Single().B == ESPNET.BridgeModels(parts, 0.150, out misses).Single().B);
    }

    // ---- the flags and what beats what ------------------------------------------------

    private static void Flags()
    {
        Console.WriteLine();
        Console.WriteLine("  where each figure comes from");

        var s = new ESPSETTINGS();

        Job job = Build();

        // A value typed for the family beats everything
        s.Families["Grille : 600"] = 40;
        s.Families["Grille : 400"] = 40;

        ESPNET.Run run = ESPNET.Calculate(job.Unit, s).Runs.Single();

        Check("a value typed for a family beats the default, and is not flagged",
            run.Steps.Last().Source == "your value for this family" && Near(run.Steps.Last().Loss, 40) &&
            !run.Steps.Last().Flagged);

        // The family's own coefficient beats the table
        job.Elbow.Coefficient = 0.9;
        run = ESPNET.Calculate(job.Unit, s).Runs.Single();

        ESPNET.Step elbow = run.Steps.FirstOrDefault(st => st.Part == job.Elbow);

        Check("a fitting's own coefficient beats the table",
            elbow == null || (elbow.Source == "the family's own coefficient" && Near(elbow.Coefficient, 0.9)));

        // A fitting that does not say what it is: read from its geometry
        job.Elbow.Coefficient = 0;
        job.Elbow.PartType = string.Empty;
        job.Tee.PartType = string.Empty;
        run = ESPNET.Calculate(job.Unit, s).Runs.Single();

        Check("an unnamed 2-ended fitting that turns is an elbow, an unnamed 3-ended one a tee",
            ESPNET.KindOf(job.Elbow, job.Elbow.Ports[0], job.Elbow.Ports[1]) == "Elbow" &&
            ESPNET.KindOf(job.Tee, job.Tee.Ports[0], job.Tee.Ports[1]) == "Tee - through" &&
            ESPNET.KindOf(job.Tee, job.Tee.Ports[0], job.Tee.Ports[2]) == "Tee - branch");

        Check("an unnamed fitting the geometry can place is NOT flagged",
            run.Steps.Where(st => st.Part == job.Elbow || st.Part == job.Tee).All(st => !st.Flagged));

        ESPNET.Part odd = Part(ESPNET.Kind.Fitting);
        for (int i = 0; i < 5; i++)
            Port(odd, 0, 0, 0, Math.Cos(i), Math.Sin(i), 0, 0.3);

        Check("a 5-ended fitting nobody can place is the flagged fallback",
            ESPNET.KindOf(odd, odd.Ports[0], odd.Ports[1]) == ESPSETTINGS.UnknownFitting);

        Check("an unnamed 2-ended fitting changing size is a transition",
            ESPNET.KindOf(Mixed(), Mixed().Ports[0], Mixed().Ports[1]) == "Transition" ||
            KindOfMixed() == "Transition");

        // An accessory with nothing: the flagged default; with a pressure
        // drop of its own: that
        ESPNET.Part damper = Part(ESPNET.Kind.Accessory, 0, "Fire Damper : 400");
        ESPNET.Port d0 = Port(damper, 5, 0, 0, -1, 0, 0, 0.4);
        ESPNET.Port d1 = Port(damper, 5, 0, 0, 1, 0, 0, 0.4);

        // Cut the main at 5 m and put the damper in it
        Job withDamper = Build();
        ESPNET.Port mainEnd = withDamper.Main.Ports[1];
        ESPNET.Port teeIn = withDamper.Tee.Ports[0];
        mainEnd.Joined.Clear();
        teeIn.Joined.Clear();
        mainEnd.X = 5; mainEnd.Along = 5;
        ESPNET.Join(mainEnd, d0);
        ESPNET.Part rest = Duct(5, 10, 0, 0.4);
        ESPNET.Join(d1, rest.Ports[0]);
        ESPNET.Join(rest.Ports[1], teeIn);

        run = ESPNET.Calculate(withDamper.Unit, new ESPSETTINGS()).Runs.Single();

        ESPNET.Step ds = run.Steps.First(st => st.Part == damper);

        Check("an accessory stating nothing is FLAGGED, at the fallback coefficient",
            ds.Flagged && ds.Source.StartsWith("DEFAULT"));

        Check("and the run still reaches both terminals through it",
            run.Terminals == 2 && Near(run.FlowAtEnds, 0.5));

        damper.PressureDrop = 22;
        run = ESPNET.Calculate(withDamper.Unit, new ESPSETTINGS()).Runs.Single();
        ds = run.Steps.First(st => st.Part == damper);

        Check("an accessory's own pressure drop is used as it stands",
            !ds.Flagged && Near(ds.Loss, 22));

        Check("the flag count on the index run counts what came from defaults",
            run.FlaggedOnIndex == run.Steps.Count(st => st.Flagged));
    }

    private static ESPNET.Part Mixed()
    {
        ESPNET.Part p = Part(ESPNET.Kind.Fitting);
        Port(p, 0, 0, 0, -1, 0, 0, 0.4);
        Port(p, 0, 0, 0, 1, 0, 0, 0.3);
        return p;
    }

    private static string KindOfMixed()
    {
        ESPNET.Part p = Mixed();
        return ESPNET.KindOf(p, p.Ports[0], p.Ports[1]);
    }

    // ---- which ports make the ESP ---------------------------------------------------

    private static void Sides()
    {
        Console.WriteLine();
        Console.WriteLine("  outlets, inlets and the ESP");

        var s = new ESPSETTINGS();

        // An AHU: supply out, return in, fresh air in - each to one grille
        // through a plain duct of its own length
        ESPNET.Part ahu = Part(ESPNET.Kind.Equipment, 0, "AHU");

        Func<string, string, double, double, ESPNET.Port> side = (system, dir, length, x) =>
        {
            ESPNET.Port port = Port(ahu, x, 0, 0, 0, 1, 0, 0.4, system, dir);

            ESPNET.Part duct = Part(ESPNET.Kind.Duct, 0, "Duct");
            duct.Section = ESPNET.Shape.Round;
            duct.Diameter = 0.4;
            ESPNET.Port a = Port(duct, x, 0, 0, 0, -1, 0, 0.4); a.Along = 0;
            ESPNET.Port b = Port(duct, x, length, 0, 0, 1, 0, 0.4); b.Along = length;
            ESPNET.Join(port, a);

            ESPNET.Part grille = Part(ESPNET.Kind.Terminal, 0, "Grille " + system);
            grille.Flow = 0.4;
            grille.PressureDrop = 20;
            ESPNET.Join(b, Port(grille, x, length, 0, 0, -1, 0, 0.4));

            return port;
        };

        side("Supply", "Out", 40, 0);
        side("Return", "In", 25, 5);
        side("Other", "In", 10, 10);

        ESPNET.Result result = ESPNET.Calculate(ahu, s);

        double sup = Friction(0.4, 0.4, 40, s) + 20;
        double ret = Friction(0.4, 0.4, 25, s) + 20;

        Check("each port gets its own run",
            result.Runs.Count == 3 && result.Runs.All(r => r.Connected && r.Terminals == 1));

        Check("return and fresh air arrive in parallel: only the worse of them counts",
            Near(result.InSide, ret, 1e-9) && Near(result.OutSide, sup, 1e-9));

        Check("ESP = the worst outlet + the worst inlet, then the margin",
            Near(result.Esp, sup + ret, 1e-9) && Near(result.EspWithMargin, (sup + ret) * 1.10, 1e-9));

        Check("a return run reads the RETURN column of the table",
            result.Runs.Single(r => r.System == "Return").Converging &&
            !result.Runs.Single(r => r.System == "Supply").Converging);

        Check("with no direction on the connector, the system decides",
            ESPNET.IsIn(new ESPNET.Port { System = "Return" }) &&
            ESPNET.IsIn(new ESPNET.Port { System = "Other" }) &&
            !ESPNET.IsIn(new ESPNET.Port { System = "Supply" }) &&
            !ESPNET.IsIn(new ESPNET.Port { System = "Exhaust" }));

        // A port joined to nothing is said, and costs nothing
        ESPNET.Port loose = Port(ahu, 20, 0, 0, 0, 1, 0, 0.4, "Exhaust", "Out");
        result = ESPNET.Calculate(ahu, s);

        Check("a connector joined to nothing is reported, not walked",
            result.Runs.Any(r => r.Start == loose && !r.Connected && r.Notes.Count > 0));
    }

    // ---- a ring ----------------------------------------------------------------------

    private static void Loops()
    {
        Console.WriteLine();
        Console.WriteLine("  a ring main");

        var s = new ESPSETTINGS();

        // Unit -> tee A; A -> B along two routes (a ring); B -> terminal
        ESPNET.Part unit = Part(ESPNET.Kind.Equipment, 0, "Unit");
        ESPNET.Port u = Port(unit, 0, 0, 0, 1, 0, 0, 0.3, "Supply", "Out");

        ESPNET.Part a = Part(ESPNET.Kind.Fitting, 0, "Tee A"); a.PartType = "Tee";
        ESPNET.Port a0 = Port(a, 1, 0, 0, -1, 0, 0, 0.3);
        ESPNET.Port a1 = Port(a, 1, 0, 0, 1, 0, 0, 0.3);
        ESPNET.Port a2 = Port(a, 1, 0, 0, 0, 1, 0, 0.3);
        ESPNET.Join(u, a0);

        ESPNET.Part b = Part(ESPNET.Kind.Fitting, 0, "Cross B"); b.PartType = "Cross";
        ESPNET.Port b0 = Port(b, 5, 0, 0, -1, 0, 0, 0.3);
        ESPNET.Port b1 = Port(b, 5, 0, 0, 0, 1, 0, 0.3);
        ESPNET.Port b2 = Port(b, 5, 0, 0, 1, 0, 0, 0.3);
        ESPNET.Port b3 = Port(b, 5, 0, 0, 0, -1, 0, 0.3);

        ESPNET.Part low = Duct(1, 5, 0, 0.3);
        ESPNET.Join(a1, low.Ports[0]);
        ESPNET.Join(low.Ports[1], b0);

        ESPNET.Part high = Part(ESPNET.Kind.Duct, 0, "High");
        high.Section = ESPNET.Shape.Round; high.Diameter = 0.3;
        ESPNET.Port h0 = Port(high, 1, 0, 0, 0, -1, 0, 0.3); h0.Along = 0;
        ESPNET.Port h1 = Port(high, 5, 0, 0, 0, 1, 0, 0.3); h1.Along = 6;
        ESPNET.Join(a2, h0);
        ESPNET.Join(h1, b1);

        ESPNET.Part t = Part(ESPNET.Kind.Terminal, 0, "Grille"); t.Flow = 0.2;
        ESPNET.Join(b2, Port(t, 5, 0, 0, -1, 0, 0, 0.3));

        ESPNET.Run run = ESPNET.Calculate(unit, s).Runs.Single();

        Check("a ring main finishes, and the loop is counted and said",
            run.Loops >= 1 && run.Notes.Any(n => n.Contains("loops back")));

        Check("the terminal beyond the ring is still reached, once",
            run.Terminals == 1 && Near(run.FlowAtEnds, 0.2));
    }

    // ---- the settings file -------------------------------------------------------------

    private static void Settings()
    {
        Console.WriteLine();
        Console.WriteLine("  the settings file");

        CultureInfo was = Thread.CurrentThread.CurrentCulture;

        try
        {
            // A machine that writes 1,2 for 1.2
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var s = new ESPSETTINGS
            {
                ToleranceMm = 87.5,
                MarginPercent = 12.5,
                Density = 1.163,
                RoughnessMm = 0.15,
                FlexRoughnessMm = 2.25,
                TerminalDropPa = 18.5,
                Parameter = "ESP (Pa)"
            };

            s.Table[0].Supply = 0.31;
            s.Table[0].Return = 0.33;
            s.Families["Fire Damper : 600x400"] = 12.5;
            s.Families["Attenuator : 1200"] = 45;

            ESPSETTINGS back = ESPSETTINGS.Parse(s.Compose());

            Check("every number survives, on a de-DE machine",
                back.ToleranceMm == 87.5 && back.MarginPercent == 12.5 && back.Density == 1.163 &&
                back.RoughnessMm == 0.15 && back.FlexRoughnessMm == 2.25 && back.TerminalDropPa == 18.5);

            Check("the parameter name survives",
                back.Parameter == "ESP (Pa)");

            Check("an edited coefficient survives, both columns",
                back.Table[0].Supply == 0.31 && back.Table[0].Return == 0.33);

            Check("the family values survive, matched without regard to case",
                back.Families.Count == 2 && back.FamilyValue("fire damper : 600X400", out double fd) && fd == 12.5);

            Check("an edited table is no longer the built-in one; a fresh one is",
                !back.TableIsBuiltIn() && new ESPSETTINGS().TableIsBuiltIn());

            // A file whose table lost its fallback rows gets them back
            ESPSETTINGS cut = ESPSETTINGS.Parse("kind\tElbow\t0.2\t0.2\t\n");

            Check("a table missing its fallback rows gets them back",
                cut.Table.Any(r => r.Kind == ESPSETTINGS.UnknownFitting) &&
                cut.Table.Any(r => r.Kind == ESPSETTINGS.UnknownAccessory));

            Check("nonsense and an empty file give the defaults rather than throwing",
                ESPSETTINGS.Parse("garbage\nmargin\tabc\n").MarginPercent == 10 &&
                ESPSETTINGS.Parse(string.Empty).TableIsBuiltIn());

            Check("a negative or zero tolerance or density is refused",
                ESPSETTINGS.Parse("tolerance\t-5\ndensity\t0\n").ToleranceMm == 150 &&
                ESPSETTINGS.Parse("tolerance\t-5\ndensity\t0\n").Density == 1.2);

            double c;

            Check("a kind the table has not got falls back to the unknown-fitting row, and says so",
                !s.Coefficient("Banana", false, out c) && c == 0.5);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = was;
        }
    }
}
