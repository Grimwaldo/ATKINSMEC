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

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
