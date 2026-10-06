using System;
using System.Collections.Generic;
using ATKINSMEC;

// Reconnecting a CHW pipe to a fan coil. The real QACHWALIGN is
// compiled in, not mirrored.
//
// Two rules, and the first one is the dangerous one: send a supply
// pipe to a return connector and the drawing looks perfectly correct
// while every calculation downstream is wrong.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    // Revit's own PipeSystemType values, as ints
    private const int Undefined = 0;
    private const int Supply = 11;
    private const int Return = 12;

    private const double Tol = 0.0016;   // 0.5 mm in feet

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("CHW pipe disconnection - joining back to an FCU");
        Console.WriteLine();

        // --- supply goes to supply -----------------------------------
        {
            Check("the same system matches",
                QACHWSIDE.Match(Supply, Supply) &&
                QACHWSIDE.Match(Return, Return));

            Check("supply NEVER matches return",
                !QACHWSIDE.Match(Supply, Return) &&
                !QACHWSIDE.Match(Return, Supply));

            // The one that must not be guessed at. A pipe on no system
            // could go on either connector, and the near one is a coin
            // toss with flow and return.
            Check("an unknown system on the pipe is refused",
                !QACHWSIDE.Match(Undefined, Supply));

            Check("an unknown system on the connector is refused",
                !QACHWSIDE.Match(Supply, Undefined));

            Check("two unknowns are still refused",
                !QACHWSIDE.Match(Undefined, Undefined));
        }

        // --- a connector Revit has no opinion about ------------------
        //
        // The case from a real VRV model: the Daikin family leaves its
        // refrigerant connectors at UndefinedSystemType, so 233 free
        // equipment connectors read "no system" while every pipe read
        // SupplyHydronic or ReturnHydronic. Pipe ends sitting 0 mm off
        // a connector were turned away.
        //
        // THE RELAXATION MUST NOT TOUCH THE RULE THAT MATTERS, and
        // that is what most of these assert.
        {
            const int Sanitary = 7;   // a condensate drain connector

            Check("strictly, an unknown connector is still refused",
                !QACHWSIDE.Match(Supply, Undefined));

            Check("allowing unknown lets it through",
                QACHWSIDE.Match(Supply, Undefined, true));

            Check("an unknown PIPE is allowed too",
                QACHWSIDE.Match(Undefined, Supply, true));

            Check("two unknowns are allowed",
                QACHWSIDE.Match(Undefined, Undefined, true));

            // The whole point: what it does NOT do
            Check("SUPPLY STILL NEVER REACHES RETURN",
                !QACHWSIDE.Match(Supply, Return, true) &&
                !QACHWSIDE.Match(Return, Supply, true));

            Check("a refrigerant pipe still cannot take the DRAIN " +
                  "connector beside it",
                !QACHWSIDE.Match(Supply, Sanitary, true) &&
                !QACHWSIDE.Match(Return, Sanitary, true));

            Check("two known systems that agree still match",
                QACHWSIDE.Match(Supply, Supply, true) &&
                QACHWSIDE.Match(Sanitary, Sanitary, true));

            Check("passing false is the old behaviour exactly",
                QACHWSIDE.Match(Supply, Undefined, false) ==
                QACHWSIDE.Match(Supply, Undefined));

            // A VRV indoor unit: two unclassified refrigerant
            // connectors and one Sanitary drain. The supply pipe is
            // nearest connector 1.
            var unit = new List<QACHWALIGN.Candidate>
            {
                new QACHWALIGN.Candidate
                    { System = Sanitary, Distance = 0.01 },
                new QACHWALIGN.Candidate
                    { System = Undefined, Distance = 0.05 },
                new QACHWALIGN.Candidate
                    { System = Undefined, Distance = 0.60 }
            };

            var taken = new HashSet<int>();

            QACHWALIGN.Choice first =
                QACHWALIGN.Pick(Supply, unit, taken, true);

            Check("the drain is skipped even though it is NEAREST",
                first.At == 1);

            Check("and the choice says it was a guess",
                first.Ambiguous);

            QACHWALIGN.Choice second =
                QACHWALIGN.Pick(Return, unit, taken, true);

            Check("the return pipe takes the other refrigerant connector",
                second.At == 2);

            Check("neither was claimed twice", taken.Count == 2);

            Check("a third pipe finds nothing left",
                !QACHWALIGN.Pick(Supply, unit, taken, true).Found);

            // With the option OFF, that unit offers nothing at all
            Check("OFF, the same unit offers nothing",
                !QACHWALIGN.Pick(
                    Supply, unit, new HashSet<int>(), false).Found);

            // A description still beats the distance among unknowns
            var labelled = new List<QACHWALIGN.Candidate>
            {
                new QACHWALIGN.Candidate
                {
                    System = Undefined,
                    Distance = 0.05,
                    Hint = QACHWALIGN.Hint("Refrigerant Gas", "Refrigerant Supply")
                },
                new QACHWALIGN.Candidate
                {
                    System = Undefined,
                    Distance = 0.60,
                    Hint = QACHWALIGN.Hint("Refrigerant Supply", "Refrigerant Supply")
                }
            };

            QACHWALIGN.Choice named =
                QACHWALIGN.Pick(Supply, labelled, new HashSet<int>(), true);

            Check("a description still beats the distance among unknowns",
                named.At == 1);

            Check("and that choice is NOT reported as a guess",
                !named.Ambiguous);
        }

        // --- which connector, when both are right there --------------
        {
            // A fan coil: connector 0 is return, connector 1 is supply.
            // The RETURN connector is nearer, but this is a supply pipe.
            var among = new List<QACHWALIGN.Candidate>
            {
                new QACHWALIGN.Candidate { System = Return, Distance = 0.05 },
                new QACHWALIGN.Candidate { System = Supply, Distance = 0.40 }
            };

            var taken = new HashSet<int>();

            QACHWALIGN.Choice picked =
                QACHWALIGN.Pick(Supply, among, taken);

            Check("a supply pipe takes the supply connector, not the near one",
                picked.At == 1);

            Check("one of each system is not ambiguous", !picked.Ambiguous);

            // And the return pipe then takes the other
            QACHWALIGN.Choice second =
                QACHWALIGN.Pick(Return, among, taken);

            Check("the return pipe takes the return connector",
                second.At == 0);

            Check("neither connector was claimed twice", taken.Count == 2);

            // A third pipe has nothing left
            Check("a third pipe finds nothing free",
                !QACHWALIGN.Pick(Supply, among, taken).Found);
        }

        {
            // Nothing on the unit carries this pipe's system
            var among = new List<QACHWALIGN.Candidate>
            {
                new QACHWALIGN.Candidate { System = Return, Distance = 0.05 },
                new QACHWALIGN.Candidate { System = Return, Distance = 0.10 }
            };

            Check("no connector of the right system means no answer",
                !QACHWALIGN.Pick(Supply, among, new HashSet<int>()).Found);
        }

        // --- a FOUR-PIPE unit ----------------------------------------
        //
        // The case the description exists for. A fan coil with a
        // chilled coil AND a heating coil has TWO connectors that both
        // say SupplyHydronic - Revit gives a free connector nothing
        // else - so a chilled water pipe could be sent to the heating
        // connector on distance alone.
        {
            // 0 is the HEATING supply and is NEARER. 1 is the chilled
            // one, and its description says so.
            var among = new List<QACHWALIGN.Candidate>
            {
                new QACHWALIGN.Candidate
                {
                    System = Supply,
                    Distance = 0.05,
                    Hint = QACHWALIGN.Hint("HHW Supply", "CHW Supply")
                },
                new QACHWALIGN.Candidate
                {
                    System = Supply,
                    Distance = 0.40,
                    Hint = QACHWALIGN.Hint("CHW Supply", "CHW Supply")
                }
            };

            QACHWALIGN.Choice picked =
                QACHWALIGN.Pick(Supply, among, new HashSet<int>());

            Check("the description beats the distance on a four-pipe unit",
                picked.At == 1);

            Check("and a choice made on a description is not ambiguous",
                !picked.Ambiguous);

            // The same unit, with NO descriptions to go on
            var blind = new List<QACHWALIGN.Candidate>
            {
                new QACHWALIGN.Candidate { System = Supply, Distance = 0.05 },
                new QACHWALIGN.Candidate { System = Supply, Distance = 0.40 }
            };

            QACHWALIGN.Choice guess =
                QACHWALIGN.Pick(Supply, blind, new HashSet<int>());

            Check("with nothing to go on it takes the nearest",
                guess.At == 0);

            Check("AND SAYS IT WAS AMBIGUOUS", guess.Ambiguous);
        }

        // --- reading a connector description -------------------------
        {
            Check("the same wording scores",
                QACHWALIGN.Hint("CHW Supply", "CHW Supply") > 0);

            Check("chilled and heating do not look alike",
                QACHWALIGN.Hint("HHW Return", "CHW Supply") == 0);

            Check("supply and supply share a word even across systems",
                QACHWALIGN.Hint("HHW Supply", "CHW Supply") == 1);

            Check("but the right one scores higher",
                QACHWALIGN.Hint("CHW Supply", "CHW Supply") >
                QACHWALIGN.Hint("HHW Supply", "CHW Supply"));

            Check("case and punctuation do not matter",
                QACHWALIGN.Hint("chw-supply", "CHW  Supply") ==
                QACHWALIGN.Hint("CHW Supply", "CHW Supply"));

            Check("nothing said scores nothing",
                QACHWALIGN.Hint("", "CHW Supply") == 0 &&
                QACHWALIGN.Hint("CHW Supply", "") == 0 &&
                QACHWALIGN.Hint(null, null) == 0);

            // Two-letter noise must not count as a match
            Check("short words are not matched on",
                QACHWALIGN.Hint("to of in", "to of in") == 0);
        }

        // --- can the pipe be brought onto it? ------------------------
        {
            var down = new QAVEC(0, 0, -1);      // a drop
            var across = new QAVEC(1, 0, 0);     // the pipe above it
            var none = new QAVEC(0, 0, 0);

            // Already there
            QACHWALIGN.Plan meet = QACHWALIGN.Make(
                new QAVEC(0, 0, -0.0005), down, across, false, Tol);

            Check("ends that already meet are just joined",
                meet.What == QACHWALIGN.Kind.Meet);

            // Short of the connector, straight down its own line
            QACHWALIGN.Plan stretch = QACHWALIGN.Make(
                new QAVEC(0, 0, -0.5), down, across, false, Tol);

            Check("a connector on the pipe's own line is a stretch",
                stretch.What == QACHWALIGN.Kind.Stretch);

            Check("and the stretch is the whole gap",
                Math.Abs(stretch.AlongPipe - 0.5) < 1e-9);

            // The case in the picture: the drop is offset sideways, in
            // the direction the pipe above it runs
            QACHWALIGN.Plan slide = QACHWALIGN.Make(
                new QAVEC(0.25, 0, -0.5), down, across, false, Tol);

            Check("an offset along the pipe above is a slide",
                slide.What == QACHWALIGN.Kind.Slide);

            Check("the drop takes the part along itself",
                Math.Abs(slide.AlongPipe - 0.5) < 1e-9);

            Check("and the fitting above slides the rest",
                Math.Abs(slide.AlongNeighbour - 0.25) < 1e-9);

            Check("with nothing left over", slide.LeftOver <= Tol);

            // Offset in a direction NEITHER pipe runs in. Nothing can
            // absorb it, so it must be refused rather than bent.
            QACHWALIGN.Plan refuse = QACHWALIGN.Make(
                new QAVEC(0.25, 0.25, -0.5), down, across, false, Tol);

            Check("an offset nothing runs in is REFUSED, not bent",
                refuse.What == QACHWALIGN.Kind.Refuse);

            Check("and it says why", refuse.Why.Length > 0);

            Check("the left over is reported, not hidden",
                refuse.LeftOver > Tol);

            // A drop with nothing above it can only stretch
            QACHWALIGN.Plan alone = QACHWALIGN.Make(
                new QAVEC(0.25, 0, -0.5), down, none, false, Tol);

            Check("an offset with nothing above to slide is refused",
                alone.What == QACHWALIGN.Kind.Refuse);

            // A pipe whose direction cannot be read
            QACHWALIGN.Plan lost = QACHWALIGN.Make(
                new QAVEC(0, 0, -0.5), none, across, false, Tol);

            Check("a pipe with no direction is refused",
                lost.What == QACHWALIGN.Kind.Refuse);

            // The connector is BEHIND the end - the pipe gets shorter
            QACHWALIGN.Plan back = QACHWALIGN.Make(
                new QAVEC(0, 0, 0.5), down, across, false, Tol);

            Check("a connector behind the end shortens the pipe",
                back.What == QACHWALIGN.Kind.Stretch &&
                back.AlongPipe < 0);
        }

        // --- the vector arithmetic itself ----------------------------
        {
            var a = new QAVEC(3, 4, 0);

            Check("length is length", Math.Abs(a.Length - 5) < 1e-12);

            Check("a unit vector is one long",
                Math.Abs(a.Unit().Length - 1) < 1e-12);

            Check("a zero vector does not divide by zero",
                new QAVEC(0, 0, 0).Unit().Length < 1e-12);

            Check("dot works",
                Math.Abs(new QAVEC(1, 0, 0).Dot(new QAVEC(0, 1, 0))) < 1e-12);
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
