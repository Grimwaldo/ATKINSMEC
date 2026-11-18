using System;
using ATKINSMEC;

// Where an equipment resize STOPS. The real EQUIPSTOP is compiled in,
// not mirrored, because this one decision is the whole add-in: get it
// wrong in the "keep going" direction and a swapped fan coil resizes
// somebody's main.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static EQUIPNODE Node(
        EQUIPNODE.Sort what, string partType, int ends, bool differ)
    {
        return new EQUIPNODE
        {
            What = what,
            PartType = partType,
            Ends = ends,
            EndsDiffer = differ
        };
    }

    // Mirrors what EQUIPJOINT.Bring and QACHWROOM.Restretch do with the
    // answer: build the new line, keeping its direction
    private static string[] Rebuilt(
        bool movingStart, string start, string finish, string moved)
    {
        return movingStart
            ? new[] { moved, finish }
            : new[] { start, moved };
    }

    // Millimetres to feet, Revit's units
    private static double M(double mm)
    {
        return mm / 304.8;
    }

    private static EQUIPV P(double x, double y, double z)
    {
        return new EQUIPV(M(x), M(y), M(z));
    }

    private static bool Near(double feet, double mm)
    {
        return Math.Abs(feet * 304.8 - mm) < 0.01;
    }

    // --- bringing a duct or pipe to the new connector ----------------
    //
    // USER-REPORTED, 2026-11-18: FCU-02 -> FCU-10 was thrown out with
    // "The duct/pipe has been modified to be in the opposite
    // direction". The bigger unit's connections were further out than
    // the stubs on them were long. A run is only ever slid along its
    // own line, short of its far end; anything else is left open.
    private static void Reach()
    {
        Console.WriteLine();
        Console.WriteLine("Bringing a run to the new connector");
        Console.WriteLine();

        // A 150 mm pipe stub along +X off the unit; the unit's
        // connector faces +X, into it
        EQUIPV here = P(0, 0, 0);
        EQUIPV far = P(150, 0, 0);
        EQUIPV? faces = new EQUIPV(1, 0, 0);
        double shortest = M(5);

        EQUIPREACH r;

        r = EQUIPREACH.Decide(here, far, P(0.2, 0, 0), faces, faces,
            false, shortest, "pipe");
        Check("a connection that stayed put needs nothing",
            r.Kind == EQUIPREACHKIND.There);

        r = EQUIPREACH.Decide(here, far, P(320, 0, 0), faces, faces,
            false, shortest, "pipe");
        Check("320 mm further out past a 150 mm stub is LEFT OPEN, " +
              "never turned inside out",
            r.Kind == EQUIPREACHKIND.Open && r.Why.Contains("past the end"));
        Check("...and says by how much and how long the pipe is",
            r.Why.Contains("320 mm") && r.Why.Contains("150 mm pipe"));

        r = EQUIPREACH.Decide(here, far, P(100, 0, 0), faces, faces,
            false, shortest, "pipe");
        Check("further out but inside the run slides the end along it",
            r.Kind == EQUIPREACHKIND.Stretch && Near(r.Left, 50));

        r = EQUIPREACH.Decide(here, far, P(-80, 0, 0), faces, faces,
            false, shortest, "pipe");
        Check("a connection that moved back lengthens the run",
            r.Kind == EQUIPREACHKIND.Stretch && Near(r.Left, 230));

        r = EQUIPREACH.Decide(here, far, P(147, 0, 0), faces, faces,
            false, shortest, "pipe");
        Check("leaving 3 mm of pipe is left open, not a sliver",
            r.Kind == EQUIPREACHKIND.Open && r.Why.Contains("3 mm long"));

        r = EQUIPREACH.Decide(here, far, P(150, 0, 0), faces, faces,
            false, shortest, "pipe");
        Check("exactly at the far end is left open",
            r.Kind == EQUIPREACHKIND.Open);

        r = EQUIPREACH.Decide(here, far, P(50, 35, 0), faces, faces,
            false, shortest, "pipe");
        Check("35 mm to the side is left open - it would have to bend",
            r.Kind == EQUIPREACHKIND.Open && r.Why.Contains("to the side") &&
            r.Why.Contains("35 mm"));

        r = EQUIPREACH.Decide(here, far, P(50, 0, 0.5), faces, faces,
            false, shortest, "pipe");
        Check("half a millimetre off the line is still straight",
            r.Kind == EQUIPREACHKIND.Stretch);

        r = EQUIPREACH.Decide(here, far, P(320, 35, 0), faces, faces,
            true, shortest, "pipe");
        Check("a stub with nothing on its far end is moved whole",
            r.Kind == EQUIPREACHKIND.Shift);

        r = EQUIPREACH.Decide(here, far, P(100, 0, 0), faces,
            new EQUIPV(0, 1, 0), true, shortest, "pipe");
        Check("a connection facing another way is left open, even free",
            r.Kind == EQUIPREACHKIND.Open && r.Why.Contains("another way"));

        double one = Math.PI / 180.0;

        r = EQUIPREACH.Decide(here, far, P(100, 0, 0), faces,
            new EQUIPV(Math.Cos(one), Math.Sin(one), 0), false, shortest,
            "pipe");
        Check("a degree round is the same way",
            r.Kind == EQUIPREACHKIND.Stretch);

        r = EQUIPREACH.Decide(here, far, P(100, 0, 0), null, null,
            false, shortest, "pipe");
        Check("a family that will not say which way it faces is not " +
              "refused for it", r.Kind == EQUIPREACHKIND.Stretch);

        // Any direction, not just +X: a duct running down -Y
        r = EQUIPREACH.Decide(P(0, 0, 0), P(0, -200, 0), P(0, -60, 0),
            new EQUIPV(0, -1, 0), new EQUIPV(0, -1, 0), false, shortest,
            "duct");
        Check("a run in any direction slides along its own line",
            r.Kind == EQUIPREACHKIND.Stretch && Near(r.Left, 140));

        r = EQUIPREACH.Decide(P(0, 0, 0), P(0, -200, 0), P(0, 60, 0),
            null, null, false, shortest, "duct");
        Check("...and moving back along it lengthens it",
            r.Kind == EQUIPREACHKIND.Stretch && Near(r.Left, 260));

        r = EQUIPREACH.Decide(P(0, 0, 0), P(0, -200, 0), P(0, -60, 55),
            null, null, false, shortest, "duct");
        Check("a taller unit's spigot 55 mm up is left open on a joined duct",
            r.Kind == EQUIPREACHKIND.Open && r.Why.Contains("55 mm") &&
            r.Why.Contains("duct"));

        Check("...and not as a turn, so it may still be carried over",
            !r.Turned);

        r = EQUIPREACH.Decide(here, far, P(100, 0, 0), faces,
            new EQUIPV(0, 1, 0), false, shortest, "pipe");
        Check("a turned connection says it is turned", r.Turned);

        // --- moving the UNIT onto its duct ---------------------------
        //
        // The user's FCU-02 -> FCU-10, 2026-11-18: a 42 mm stub along +Y
        // to a transition, and FCU-10's spigot 327 mm to the side of it
        EQUIPV stub = P(0, 42, 0);

        EQUIPV m = EQUIPREACH.Anchor(here, stub, P(327, 0, 0), shortest);
        Check("the user's spigot 327 mm aside: the unit moves 327 mm back",
            Near(m.X, -327) && Near(m.Y, 0) && Near(m.Z, 0));

        m = EQUIPREACH.Anchor(here, stub, P(327, 20, 55), shortest);
        Check("only the sideways part, while the stub can take the rest",
            Near(m.X, -327) && Near(m.Y, 0) && Near(m.Z, -55));

        m = EQUIPREACH.Anchor(here, stub, P(327, 60, 0), shortest);
        Check("the whole offset when the stub is too short for the rest",
            Near(m.X, -327) && Near(m.Y, -60) && Near(m.Z, 0));

        m = EQUIPREACH.Anchor(here, stub, P(0, -100, 0), shortest);
        Check("a spigot that moved back along the duct moves nothing - " +
              "the duct stretches", m.Length < 1e-9);

        m = EQUIPREACH.Anchor(here, stub, P(0, 20, 0), shortest);
        Check("one the stub reaches as it is moves nothing",
            m.Length < 1e-9);

        m = EQUIPREACH.Anchor(here, stub, P(0.4, 0, 0), shortest);
        Check("under a millimetre aside moves nothing", m.Length < 1e-9);

        // After the move the duct reaches: decided again, it is There
        EQUIPV moved = P(327, 0, 0) + EQUIPREACH.Anchor(here, stub,
            P(327, 0, 0), shortest);

        r = EQUIPREACH.Decide(here, stub, moved, faces, faces, false,
            shortest, "duct");
        Check("moved onto its duct, the stub joins as it is",
            r.Kind == EQUIPREACHKIND.There);

        // --- a slanted carry, in parts --------------------------------
        //
        // The user's next run: with the unit on its duct, each CHW
        // elbow was 322 mm out and 55 mm down from its connection
        EQUIPV slant = P(0, 322, -55);

        var sides = new[]
        {
            new EQUIPV(1, 0, 0), new EQUIPV(0, 1, 0), new EQUIPV(0, 0, 1)
        };

        var parts = EQUIPREACH.Parts(slant, sides);

        Check("322 out and 55 down is two parts, one along each side",
            parts.Count == 2 &&
            Near(parts[0].Y, 322) && Near(parts[0].X, 0) && Near(parts[0].Z, 0) &&
            Near(parts[1].Z, -55) && Near(parts[1].X, 0) && Near(parts[1].Y, 0));

        // The drop takes the 55, the branch the 322 - neither takes the
        // whole slant
        EQUIPV elbow = P(0, 0, 0);

        r = EQUIPREACH.Decide(elbow, P(0, 0, 400), elbow + slant, null, null,
            false, shortest, "pipe");
        Check("a 400 mm drop cannot take the whole slant",
            r.Kind == EQUIPREACHKIND.Open);

        r = EQUIPREACH.Decide(elbow, P(0, 0, 400), elbow + parts[1], null,
            null, false, shortest, "pipe");
        Check("...but takes its 55 mm down part",
            r.Kind == EQUIPREACHKIND.Stretch && Near(r.Left, 455));

        r = EQUIPREACH.Decide(elbow, P(0, 1500, 0), elbow + parts[0], null,
            null, false, shortest, "pipe");
        Check("...and a branch along the unit takes the 322 mm",
            r.Kind == EQUIPREACHKIND.Stretch && Near(r.Left, 1178));

        // A building turned 30 degrees: split along the unit's own sides
        double c = Math.Cos(Math.PI / 6), s = Math.Sin(Math.PI / 6);

        var turned = new[]
        {
            new EQUIPV(c, s, 0), new EQUIPV(-s, c, 0), new EQUIPV(0, 0, 1)
        };

        parts = EQUIPREACH.Parts(new EQUIPV(-s, c, 0) * M(322) +
                                 new EQUIPV(0, 0, 1) * M(-55), turned);
        Check("a unit turned 30 degrees splits along its own sides",
            parts.Count == 2 && Near(parts[0].Length, 322) &&
            Near(parts[1].Length, 55));

        Check("a move along one side is one part",
            EQUIPREACH.Parts(P(0, 322, 0), sides).Count == 1);

        // --- an offset, where nothing can stretch to the step ---------
        //
        // The probe (2026-11-18): unit 231's CHW runs are horizontal for
        // pieces on end, and FCU-10 puts each connection 55 mm off them
        // vertically. DN20, threaded elbows 30-35 mm centre to end.
        EQUIPV unitEnd = P(0, 0, 0);
        EQUIPV outward = new EQUIPV(0, 1, 0);
        double dn20 = M(20);

        EQUIPDOGLEG plan = EQUIPDOGLEG.Plan(unitEnd, outward, P(0, 200, 55), dn20, 90);
        Check("a 55 mm step is too small for two square DN20 elbows",
            !plan.Ok && plan.Why.Contains("too small"));

        plan = EQUIPDOGLEG.Plan(unitEnd, outward, P(0, 200, 55), dn20, 45);
        Check("...but takes two 45-degree ones with 200 mm of room",
            plan.Ok && Near(plan.Offset, 55) && Near(plan.P1.Y, 38) &&
            Near(plan.P2.Y, 93) && Near(plan.P2.Z, 55) && Near(plan.P1.Z, 0));

        plan = EQUIPDOGLEG.Fitting(unitEnd, outward, P(0, -321, 55), dn20);
        Check("FCU-10's connection inside where the pipework was: the 45 " +
              "plan says how much room to make",
            plan != null && plan.Angle == 45 && !plan.Ok &&
            Near(plan.Room, 131) && Near(plan.Gap, -321));

        // Unit 164: pipes never joined, 100 mm across and 122 mm out
        plan = EQUIPDOGLEG.Fitting(unitEnd, outward, P(100, 122, 2), dn20);
        Check("unit 164's 100 mm step fits square elbows in 122 mm",
            plan != null && plan.Ok && plan.Angle == 90 &&
            Near(plan.P1.Y, 45) && Near(plan.P2.X, 100));

        plan = EQUIPDOGLEG.Plan(unitEnd, outward, P(0, 200, 0), dn20, 90);
        Check("in line is no offset", !plan.Ok && plan.Why.Contains("in line"));

        Check("a step too small for every angle has no offset",
            EQUIPDOGLEG.Fitting(unitEnd, outward, P(0, 300, 12), dn20) == null);
    }

    // --- which new connector each joint belongs to --------------------
    private static EQUIPPORT Port(int id, int domain, int system,
        double x, double y)
    {
        return new EQUIPPORT
        {
            Id = id,
            Domain = domain,
            System = system,
            At = P(x, y, 0)
        };
    }

    private static bool Goes(
        System.Collections.Generic.Dictionary<int, int> map, int was, int now)
    {
        int got;

        return map.TryGetValue(was, out got) && got == now;
    }

    private static void Match()
    {
        Console.WriteLine();
        Console.WriteLine("Which new connector each joint goes on");
        Console.WriteLine();

        const int Hvac = 1, Piping = 3;
        const int SupplyAir = 1, ReturnAir = 2, Flow = 7, Back = 8;

        // FCU-02: supply air, return air, CHW flow and return 100 mm apart
        var was = new System.Collections.Generic.List<EQUIPPORT>
        {
            Port(1, Hvac, SupplyAir, 0, 0),
            Port(2, Hvac, ReturnAir, -600, 0),
            Port(3, Piping, Flow, 300, 100),
            Port(4, Piping, Back, 300, 200)
        };

        // FCU-10, same family: everything has moved, and the CHW RETURN
        // now sits 50 mm from where the FLOW used to be
        var now = new System.Collections.Generic.List<EQUIPPORT>
        {
            Port(1, Hvac, SupplyAir, 320, 0),
            Port(2, Hvac, ReturnAir, -920, 0),
            Port(3, Piping, Flow, 300, 350),
            Port(4, Piping, Back, 300, 150)
        };

        var map = EQUIPMATCH.Assign(was, now, true);

        Check("same family: every joint goes on the SAME connector, " +
              "though another is nearer",
            Goes(map, 1, 1) && Goes(map, 2, 2) && Goes(map, 3, 3) &&
            Goes(map, 4, 4));

        // The rule that was replaced, to show the test can tell
        Check("(nearest-by-position would have put the flow on the return)",
            (now[3].At - was[2].At).Length < (now[2].At - was[2].At).Length);

        // Two supply spigots: the system cannot tell them apart, only
        // the family's own connector can
        map = EQUIPMATCH.Assign(
            new System.Collections.Generic.List<EQUIPPORT>
            {
                Port(1, Hvac, SupplyAir, 0, 0),
                Port(5, Hvac, SupplyAir, 0, 400)
            },
            new System.Collections.Generic.List<EQUIPPORT>
            {
                Port(1, Hvac, SupplyAir, 0, 500),
                Port(5, Hvac, SupplyAir, 0, 100)
            },
            true);

        Check("same family, two supply spigots: each keeps its own " +
              "though they passed each other",
            Goes(map, 1, 1) && Goes(map, 5, 5));

        // Another family: its own connector ids
        var other = new System.Collections.Generic.List<EQUIPPORT>
        {
            Port(11, Hvac, SupplyAir, 320, 0),
            Port(12, Hvac, ReturnAir, -920, 0),
            Port(13, Piping, Flow, 300, 350),
            Port(14, Piping, Back, 300, 150)
        };

        map = EQUIPMATCH.Assign(was, other, true);

        Check("another family: flow to flow and return to return, by system",
            Goes(map, 1, 11) && Goes(map, 2, 12) && Goes(map, 3, 13) &&
            Goes(map, 4, 14));

        var silent = new System.Collections.Generic.List<EQUIPPORT>
        {
            Port(13, Piping, -1, 300, 350),
            Port(14, Piping, -1, 300, 150)
        };

        map = EQUIPMATCH.Assign(was, silent, false);

        Check("connectors that will not say: nearest first, each once",
            Goes(map, 3, 14) && Goes(map, 4, 13));

        Check("a duct joint never goes on a pipe connector",
            !map.ContainsKey(1) && !map.ContainsKey(2));

        var onlyReturn = new System.Collections.Generic.List<EQUIPPORT>
        {
            Port(14, Piping, Back, 300, 100)
        };

        map = EQUIPMATCH.Assign(was, onlyReturn, false);

        Check("supply never goes on a return, however close",
            !map.ContainsKey(3) && Goes(map, 4, 14));

        var missing = new System.Collections.Generic.List<EQUIPPORT>
        {
            Port(1, Hvac, SupplyAir, 320, 0),
            Port(9, Hvac, ReturnAir, -920, 0)
        };

        map = EQUIPMATCH.Assign(
            new System.Collections.Generic.List<EQUIPPORT> { was[0], was[1] },
            missing, true);

        Check("same family, a connector not there: matched by system",
            Goes(map, 1, 1) && Goes(map, 2, 9));

        Check("the rank: same system first, a silent one next, a " +
              "different one never",
            EQUIPMATCH.Rank(Flow, Flow) == 0 &&
            EQUIPMATCH.Rank(-1, Back) == 1 &&
            EQUIPMATCH.Rank(Flow, -1) == 1 &&
            EQUIPMATCH.Rank(Flow, Back) < 0);
    }

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("EQUIPMENT CHANGER - where a resize stops");
        Console.WriteLine();

        // --- the things that follow ----------------------------------
        {
            Check("a duct follows",
                EQUIPSTOP.Decide(Node(EQUIPNODE.Sort.Duct, "", 2, false))
                    == EQUIPSTOPKIND.Cross);

            Check("an elbow follows",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "Elbow", 2, false))
                    == EQUIPSTOPKIND.Cross);

            Check("a union follows",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "Union", 2, false))
                    == EQUIPSTOPKIND.Cross);

            Check("an in-line accessory follows",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Accessory, "", 2, false))
                    == EQUIPSTOPKIND.Cross);

            Check("a two-ended fitting that says nothing follows",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "", 2, false))
                    == EQUIPSTOPKIND.Cross);
        }

        // --- a reducer ends it ---------------------------------------
        {
            Check("a Transition is a reducer",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "Transition", 2, false))
                    == EQUIPSTOPKIND.Reducer);

            // The one that matters most: a family that will not say
            // what it is, whose two ends are simply different sizes
            Check("two ends of different sizes IS a reducer, unnamed",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "", 2, true))
                    == EQUIPSTOPKIND.Reducer);

            // And the other way round: what it calls itself wins, so a
            // transition whose ends happen to measure the same today is
            // still left alone
            Check("a Transition with equal ends is still a reducer",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "Transition", 2, false))
                    == EQUIPSTOPKIND.Reducer);

            Check("an accessory with different ends is a reducer too",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Accessory, "", 2, true))
                    == EQUIPSTOPKIND.Reducer);
        }

        // --- a tap ends it -------------------------------------------
        {
            foreach (string named in new[]
                     {
                         "TapAdjustable", "TapPerpendicular",
                         "SpudAdjustable", "SpudPerpendicular"
                     })
            {
                Check("a " + named + " is a tap-off",
                    EQUIPSTOP.Decide(
                        Node(EQUIPNODE.Sort.Fitting, named, 1, false))
                        == EQUIPSTOPKIND.Tap);
            }

            // A tap's main side is hosted on the duct's body and is not
            // an end connector at all, so it has ONE - the same thing
            // that made QADUCTFIX replace a hanging tap rather than
            // repair it
            Check("one end alone is a tap, whatever it is called",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "", 1, false))
                    == EQUIPSTOPKIND.Tap);

            Check("no ends at all is a tap rather than a crossing",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "", 0, false))
                    == EQUIPSTOPKIND.Tap);
        }

        // --- a tee ends it -------------------------------------------
        {
            Check("a Tee is a branch",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "Tee", 3, false))
                    == EQUIPSTOPKIND.Branch);

            Check("a Cross is a branch",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "Cross", 4, false))
                    == EQUIPSTOPKIND.Branch);

            Check("three ends is a branch, unnamed",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "", 3, false))
                    == EQUIPSTOPKIND.Branch);

            // The fitting in the user's screenshot:
            // ATR_ME_DUF_Branch_RectangularSquareSlipJoint, a takeoff
            Check("a branch takeoff off a main is never crossed",
                EQUIPSTOP.Decide(
                    Node(EQUIPNODE.Sort.Fitting, "TapPerpendicular", 1, false))
                    != EQUIPSTOPKIND.Cross);
        }

        // --- the ends of the line ------------------------------------
        {
            Check("an air terminal ends it",
                EQUIPSTOP.Decide(Node(EQUIPNODE.Sort.Terminal, "", 1, false))
                    == EQUIPSTOPKIND.Terminal);

            Check("other equipment ends it",
                EQUIPSTOP.Decide(Node(EQUIPNODE.Sort.Equipment, "", 2, false))
                    == EQUIPSTOPKIND.Equipment);

            Check("anything else ends it rather than being guessed at",
                EQUIPSTOP.Decide(Node(EQUIPNODE.Sort.Other, "", 2, false))
                    == EQUIPSTOPKIND.Unknown);

            Check("nothing at all ends it",
                EQUIPSTOP.Decide(null) == EQUIPSTOPKIND.Unknown);

            // WHY EQUIPWALK.Ductwork EXISTS, in one line.
            //
            // A duct insulation sleeve is in Connector.AllRefs and
            // DuctInsulation derives from MEPCurve, so an unfiltered
            // walk hands one back instead of the duct. Describe() then
            // reads a category that is neither duct nor fitting and
            // calls it Other - and Other lands HERE, on Unknown, which
            // EQUIPCHANGE turns into a refusal of the whole type change
            // when it comes back on the first hop. An insulated duct on
            // a fan coil is the ordinary case, so this assertion is the
            // difference between a working button and a dead one.
            //
            // The category test itself cannot be asserted here: naming
            // Revit's enum values in a Revit-free file would be a guess
            // with no way to check it, the same reason QACHWALIGN
            // carries no PipeSystemType numbers.
            Check("a sleeve reads as Other, which refuses the change",
                EQUIPSTOP.Decide(Node(EQUIPNODE.Sort.Other, "", 2, false))
                    == EQUIPSTOPKIND.Unknown &&
                EQUIPSTOP.Why(EQUIPSTOPKIND.Unknown)
                    .Contains("could not read"));
        }

        // --- Crosses agrees with Decide ------------------------------
        {
            Check("Crosses is true only for a crossing",
                EQUIPSTOP.Crosses(Node(EQUIPNODE.Sort.Duct, "", 2, false)) &&
                !EQUIPSTOP.Crosses(
                    Node(EQUIPNODE.Sort.Fitting, "Tee", 3, false)) &&
                !EQUIPSTOP.Crosses(
                    Node(EQUIPNODE.Sort.Terminal, "", 1, false)));

            Check("every stop has something to say",
                EQUIPSTOP.Why(EQUIPSTOPKIND.Reducer).Length > 0 &&
                EQUIPSTOP.Why(EQUIPSTOPKIND.Tap).Length > 0 &&
                EQUIPSTOP.Why(EQUIPSTOPKIND.Branch).Length > 0 &&
                EQUIPSTOP.Why(EQUIPSTOPKIND.Terminal).Length > 0 &&
                EQUIPSTOP.Why(EQUIPSTOPKIND.Equipment).Length > 0 &&
                EQUIPSTOP.Why(EQUIPSTOPKIND.Unknown).Length > 0);
        }

        // --- sizes ---------------------------------------------------
        {
            // Everything is in feet, Revit's internal unit
            const double mm = 1.0 / 304.8;

            var small = new EQUIPSIZE
            { Round = false, Width = 400 * mm, Height = 200 * mm };

            var same = new EQUIPSIZE
            { Round = false, Width = 400 * mm, Height = 200 * mm };

            var bigger = new EQUIPSIZE
            { Round = false, Width = 500 * mm, Height = 200 * mm };

            var round = new EQUIPSIZE { Round = true, Diameter = 400 * mm };

            Check("the same size is the same size", small.Same(same));

            Check("a different width is a different size",
                !small.Same(bigger));

            Check("round is never the same as rectangular",
                !round.Same(small) && !small.Same(round));

            // A duct size is a round number of millimetres; anything
            // finer is noise out of the unit conversion
            var hair = new EQUIPSIZE
            {
                Round = false,
                Width = 400 * mm + 1e-6,
                Height = 200 * mm - 1e-6
            };

            Check("a thousandth of a millimetre is not a resize",
                small.Same(hair));

            var halfMm = new EQUIPSIZE
            { Round = false, Width = 401 * mm, Height = 200 * mm };

            Check("a whole millimetre IS a resize", !small.Same(halfMm));

            Check("an unknown size is known to be unknown",
                !new EQUIPSIZE().Known && small.Known && round.Known);

            Check("a size reads the way a duct is spoken about",
                small.ToString() == "400 x 200" &&
                round.ToString() == "400 dia" &&
                new EQUIPSIZE().ToString() == "(unknown)");

            Check("nothing is the same as nothing it cannot measure",
                !small.Same(null));
        }

        // --- moving one end of a duct --------------------------------
        //
        // The rule that was wrong. Every step of a real equipment change
        // succeeded and the whole transaction was thrown out at the
        // commit with "The duct/pipe has been modified to be in the
        // opposite direction causing the connections to be invalid",
        // because the new line was built far-end-first and so ran
        // backwards. A line is (start, finish) and must stay that way.
        {
            // A duct from x=0 to x=10. Its start is the end being moved.
            Check("the end nearest the start IS the start",
                EQUIPLINE.MovingIsStart(0.2, 9.8));

            // The same duct, but it is the finish that is moving
            Check("the end nearest the finish is NOT the start",
                !EQUIPLINE.MovingIsStart(9.8, 0.2));

            // Exactly in the middle cannot happen on a real duct, but
            // it must still answer rather than throw
            Check("a dead heat picks the start and does not throw",
                EQUIPLINE.MovingIsStart(5.0, 5.0));

            // What the caller does with it, written out, because the
            // answer alone is not the thing that was broken
            string[] a = Rebuilt(true, "start", "finish", "moved");
            string[] b = Rebuilt(false, "start", "finish", "moved");

            Check("moving the start gives (moved, finish) - not reversed",
                a[0] == "moved" && a[1] == "finish");

            Check("moving the finish gives (start, moved) - not reversed",
                b[0] == "start" && b[1] == "moved");

            Check("neither case ever puts the far end first",
                a[0] != "finish" && b[1] != "start");
        }

        Reach();
        Match();

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
