using ATKINSMEC;
using System;
using System.Collections.Generic;

// A room's air divided equally between the ENDCAPS that serve it.
//
// The real VENTFCUCHECK is compiled in - the tally, the share and what
// each endcap is finally written - and this builds small JOBS out of
// units, endcaps and rooms and runs them through exactly the calls
// REVITROOMS makes.
//
// The property everything hangs on is CONSERVATION: the endcaps serving
// a room are written, between them, exactly that room's air. Not twice
// (the original behaviour, one endcap per unit each writing the lot)
// and not half (the split by FCUs, which dropped the share of a unit
// with no endcap drawn).
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static bool Near(double a, double b)
    {
        return Math.Abs(a - b) < 1e-9;
    }

    // ---- a small job ------------------------------------------------

    private class Job
    {
        // unit id -> the rooms its supply serves
        public Dictionary<long, List<string>> RoomsOfFcu =
            new Dictionary<long, List<string>>();

        // unit id -> fresh endcaps bound to it
        public Dictionary<long, int> FreshOn = new Dictionary<long, int>();

        public Dictionary<long, int> ExtractOn = new Dictionary<long, int>();

        // room -> the sheet's fresh value
        public Dictionary<string, double> Fresh =
            new Dictionary<string, double>();

        public Dictionary<string, double> Extract =
            new Dictionary<string, double>();

        public void Unit(long id, int freshEndcaps, params string[] rooms)
        {
            RoomsOfFcu[id] = new List<string>(rooms);
            FreshOn[id] = freshEndcaps;
        }

        public Dictionary<string, VENTFCUCHECK.Room> Tally()
        {
            return VENTFCUCHECK.Tally(RoomsOfFcu, FreshOn, ExtractOn, k => k);
        }

        // What ONE fresh endcap on this unit is written - exactly the
        // call REVITROOMS makes for it
        public double EndcapOn(long unit)
        {
            return VENTFCUCHECK.Carried(
                RoomsOfFcu[unit], Fresh, Tally(), true);
        }

        // Everything written onto every fresh endcap in the job
        public double Written()
        {
            double total = 0;

            foreach (var unit in RoomsOfFcu)
            {
                int endcaps;

                FreshOn.TryGetValue(unit.Key, out endcaps);

                total += endcaps * EndcapOn(unit.Key);
            }

            return total;
        }
    }

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("AUTOVENT - a room's air split between its endcaps");
        Console.WriteLine();

        // ---- THE ORDINARY ROOM MUST NOT MOVE -----------------------
        {
            var job = new Job();

            job.Unit(1, 1, "R1");
            job.Fresh["R1"] = 100;

            Check("one unit, one endcap is written the WHOLE value",
                Near(job.EndcapOn(1), 100));

            Check("and nothing about it is worth a line",
                !VENTFCUCHECK.Worth(job.Tally()["R1"]));
        }

        // ---- THE CASE THAT PROMPTED THIS ---------------------------
        {
            // Two units serve the room, only the first has an endcap
            var job = new Job();

            job.Unit(1, 1, "R2");
            job.Unit(2, 0, "R2");
            job.Fresh["R2"] = 100;

            Check("two units, ONE endcap: that endcap carries ALL of it",
                Near(job.EndcapOn(1), 100));

            Check("so none of the room's air is lost",
                Near(job.Written(), 100));

            VENTFCUCHECK.Room room = job.Tally()["R2"];

            Check("the unit with no endcap IS reported",
                VENTFCUCHECK.UnitWithoutFresh(room));

            Check("and the line says the others carry it",
                VENTFCUCHECK.Line(room).Contains("the others carry all of it"));

            Check("but it is not called divided - one endcap has it all",
                !VENTFCUCHECK.Divided(room));
        }

        // ---- an endcap on each unit --------------------------------
        {
            var job = new Job();

            job.Unit(1, 1, "R3");
            job.Unit(2, 1, "R3");
            job.Fresh["R3"] = 100;

            Check("two units with an endcap each carry half",
                Near(job.EndcapOn(1), 50) && Near(job.EndcapOn(2), 50));

            Check("and it is written ONCE, not twice as it used to be",
                Near(job.Written(), 100));

            Check("the line says it was divided between 2 endcaps",
                VENTFCUCHECK.Line(job.Tally()["R3"]).Contains(
                    "divided between 2 endcaps"));
        }

        // ---- uneven: the ENDCAPS decide, not the units -------------
        {
            // Unit 1 carries two endcaps, unit 2 carries one. By units
            // it would be halves; by endcaps it is thirds.
            var job = new Job();

            job.Unit(1, 2, "R4");
            job.Unit(2, 1, "R4");
            job.Fresh["R4"] = 90;

            Check("three endcaps on two units carry a THIRD each",
                Near(job.EndcapOn(1), 30) && Near(job.EndcapOn(2), 30));

            Check("and the three add back to the whole",
                Near(job.Written(), 90));
        }

        // ---- a unit serving more than one room ---------------------
        {
            // Unit 1 serves R5 and R6. Unit 2 also serves R5. One
            // endcap each. Unit 1's endcap is written half of R5 and
            // all of R6; unit 2's is written the other half of R5.
            var job = new Job();

            job.Unit(1, 1, "R5", "R6");
            job.Unit(2, 1, "R5");
            job.Fresh["R5"] = 60;
            job.Fresh["R6"] = 40;

            Check("a unit's endcap sums its share of EACH room it serves",
                Near(job.EndcapOn(1), 30 + 40));

            Check("the other unit takes only its share of the shared room",
                Near(job.EndcapOn(2), 30));

            Check("and across both rooms nothing is lost or doubled",
                Near(job.Written(), 60 + 40));
        }

        // ---- CONSERVATION ACROSS A WHOLE MIXED JOB -----------------
        {
            var job = new Job();

            job.Unit(1, 1, "A");               // ordinary
            job.Unit(2, 1, "B");               // B shared,
            job.Unit(3, 0, "B");               //   one unit bare
            job.Unit(4, 2, "C", "D");          // two endcaps, two rooms
            job.Unit(5, 1, "C");               // C shared unevenly
            job.Unit(6, 1, "E", "F");          // one endcap, two rooms

            job.Fresh["A"] = 37.5;
            job.Fresh["B"] = 100;
            job.Fresh["C"] = 75;
            job.Fresh["D"] = 12;
            job.Fresh["E"] = 20;
            job.Fresh["F"] = 5;

            double sheet = 37.5 + 100 + 75 + 12 + 20 + 5;

            Check("a whole job writes EXACTLY what the sheet says",
                Near(job.Written(), sheet));
        }

        // ---- rooms and keys that are not what they seem ------------
        {
            var job = new Job();

            // A unit with two grilles in the same room lists it twice
            job.Unit(1, 1, "R7", "R7");
            job.Fresh["R7"] = 100;

            Check("a room listed twice by one unit is ONE room to it",
                job.Tally()["R7"].Fcus == 1);

            Check("and is not written twice either",
                Near(job.EndcapOn(1), 100));

            var missing = new Job();

            missing.Unit(1, 1, "R8", "R9");
            missing.Fresh["R8"] = 50;     // R9 is not in the sheet

            Check("a room the sheet does not have contributes nothing",
                Near(missing.EndcapOn(1), 50));
        }

        // ---- the guards --------------------------------------------
        {
            Check("a count of zero is treated as one, not a crash",
                Near(VENTFCUCHECK.Share(100, 0), 100));

            Check("so is a negative",
                Near(VENTFCUCHECK.Share(100, -2), 100));

            Check("no air is no air",
                Near(VENTFCUCHECK.Share(0, 3), 0)
                && Near(VENTFCUCHECK.Share(-5, 3), 0));

            Check("a room missing from the tally divides by one",
                Near(VENTFCUCHECK.Carried(
                    new[] { "X" },
                    new Dictionary<string, double> { { "X", 80 } },
                    new Dictionary<string, VENTFCUCHECK.Room>(),
                    true), 80));

            Check("null inputs give nothing rather than throwing",
                Near(VENTFCUCHECK.Carried(null, null, null, true), 0)
                && VENTFCUCHECK.Tally(null, null, null, null).Count == 0);
        }

        // ---- the two sides are counted apart -----------------------
        {
            // One unit with one fresh AND one extract endcap. Adding the
            // sides together would halve both.
            var job = new Job();

            job.Unit(1, 1, "R10");
            job.ExtractOn[1] = 1;
            job.Fresh["R10"] = 100;
            job.Extract["R10"] = 80;

            var tally = job.Tally();

            Check("a unit with one of each side is written whole on both",
                Near(VENTFCUCHECK.Carried(
                         job.RoomsOfFcu[1], job.Fresh, tally, true), 100)
                && Near(VENTFCUCHECK.Carried(
                         job.RoomsOfFcu[1], job.Extract, tally, false), 80));
        }

        // ---- the quiet ones ----------------------------------------
        {
            var job = new Job();

            // Two units, no endcaps: the room's air comes from the
            // in-room terminals. Saying anything would bury the rest.
            job.Unit(1, 0, "Q");
            job.Unit(2, 0, "Q");

            Check("two units and no endcaps anywhere says nothing",
                !VENTFCUCHECK.Worth(job.Tally()["Q"]));

            Check("and null says nothing without throwing",
                !VENTFCUCHECK.Worth((VENTFCUCHECK.Room)null)
                && VENTFCUCHECK.Line(null) == string.Empty);
        }

        // ---- the listing -------------------------------------------
        {
            var job = new Job();

            job.Unit(1, 1, "2.09");
            job.Unit(2, 0, "2.09");        // a unit with none
            job.Unit(3, 1, "1.01");        // quiet
            job.Unit(4, 1, "1.02");
            job.Unit(5, 1, "1.02");        // divided
            job.Unit(6, 0, "3.00");
            job.Unit(7, 0, "3.00");        // quiet, no endcaps
            job.Unit(8, 2, "1.50");        // divided on one unit

            List<VENTFCUCHECK.Room> worth =
                VENTFCUCHECK.Worth(job.Tally().Values);

            Check("only the rooms worth a line come back",
                worth.Count == 3);

            Check("in a settled order",
                worth.Count == 3
                && worth[0].Name == "1.02"
                && worth[1].Name == "1.50"
                && worth[2].Name == "2.09");
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
