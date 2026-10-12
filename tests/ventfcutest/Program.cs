using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Linq;

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

    // ==================================================================
    // VENTFACING - a bellmouth into its room, or into an FCU. The user's
    // four cases (2026-10-10), in metres: floor at 0, ceiling at 2.7, the
    // FCU in the void above it.
    // ==================================================================

    private static double[] V(double x, double y, double z)
    {
        return new[] { x, y, z };
    }

    private static readonly VENTFACING.Box Fcu = new VENTFACING.Box
    {
        Min = V(4.0, 2.0, 2.9),
        Max = V(5.2, 2.6, 3.3)
    };

    private const double Reach = 1.5;
    private const double Margin = 0.15;

    // Where a bellmouth's air goes, as REVITROOMS asks: the mouth from
    // its connector and body, then the first unit that mouth hits
    private static int Aim(double[] connectorAt, double[] connectorZ, double[] body,
                           IList<VENTFACING.Box> units, out double distance)
    {
        double[] mouth = VENTFACING.Mouth(connectorZ, connectorAt, body);

        return VENTFACING.Faced(connectorAt, mouth, units, Reach, Margin, out distance);
    }

    // ==================================================================
    // MERGED ROOMS - one FCU, one fresh air connection, several spaces
    // (the user, 2026-10-12): merged in the sheet, or in Arrange rooms,
    // their air is one total, given to their connection once.
    // ==================================================================

    private static void Merged()
    {
        string[] rooms = { "KITCHEN", "LIVING", "BED", "BATH" };

        Dictionary<string, string> none = VENTFCUCHECK.Groups(rooms, null);

        Check("no merges: every room its own group",
            rooms.All(r => none[r] == r));

        // The sheet merges LIVING's cell over KITCHEN's; Arrange merges
        // BED with LIVING - the two sources overlap into one group
        Dictionary<string, string> groups = VENTFCUCHECK.Groups(rooms, new[]
        {
            new[] { "LIVING", "KITCHEN" },
            new[] { "BED", "LIVING" }
        });

        Check("merges that share a room are one group, led by the earliest room",
            groups["KITCHEN"] == "KITCHEN" && groups["LIVING"] == "KITCHEN" &&
            groups["BED"] == "KITCHEN" && groups["BATH"] == "BATH");

        Check("a room the model has not got merges nothing",
            VENTFCUCHECK.Groups(rooms, new[] { new[] { "STORE", "BATH" } })["BATH"] == "BATH" &&
            !VENTFCUCHECK.Groups(rooms, new[] { new[] { "STORE", "BATH" } }).ContainsKey("STORE"));

        var fresh = new Dictionary<string, double>
        {
            ["KITCHEN"] = 30, ["LIVING"] = 50, ["BED"] = 20, ["BATH"] = 0
        };

        Dictionary<string, double> totals = VENTFCUCHECK.Totals(fresh, groups);

        Check("a group's air is its rooms' added, once",
            Near(totals["KITCHEN"], 100) && Near(totals["BATH"], 0) && totals.Count == 2);

        Check("...and nothing is gained or lost by merging",
            Near(totals.Values.Sum(), fresh.Values.Sum()));

        Check("rooms as groups: each group once, in the order first met",
            VENTFCUCHECK.AsGroups(new[] { "LIVING", "BATH", "KITCHEN", "BED" }, groups)
                .SequenceEqual(new[] { "KITCHEN", "BATH" }));

        // The case: KITCHEN and LIVING on one unit with ONE fresh endcap.
        // As groups, the endcap carries the total - once
        var job = new Job();

        Dictionary<string, string> pair = VENTFCUCHECK.Groups(rooms, new[] { new[] { "KITCHEN", "LIVING" } });

        job.RoomsOfFcu[1] = VENTFCUCHECK.AsGroups(new[] { "KITCHEN", "LIVING" }, pair);
        job.FreshOn[1] = 1;

        foreach (KeyValuePair<string, double> group in VENTFCUCHECK.Totals(fresh, pair))
            job.Fresh[group.Key] = group.Value;

        Check("one unit, one endcap, two merged rooms: the endcap is written their total",
            Near(job.EndcapOn(1), 80) && Near(job.Written(), 80));

        // ...and the same pair with a second unit, also with an endcap,
        // serving the merged group: the total split, not doubled
        job.RoomsOfFcu[2] = VENTFCUCHECK.AsGroups(new[] { "LIVING" }, pair);
        job.FreshOn[2] = 1;

        Check("a second unit on the group: the total shared between the endcaps, never doubled",
            Near(job.Written(), 80) && Near(job.EndcapOn(1), 40));
    }

    private static void Facing()
    {
        Console.WriteLine();
        Console.WriteLine("  bellmouths - into the room, or into an FCU");

        const string bellmouth = "ATR_ME_MEQ_BellMouth-Fresh Air";
        const string endcap = "ATR_ME_AIT_Rectangular Blank Endcap_SAD";

        VENTSETTINGS.EndcapNames = VENTSETTINGS.DefaultEndcapNames;
        VENTSETTINGS.BellmouthNames = VENTSETTINGS.DefaultBellmouthNames;

        var units = new List<VENTFACING.Box> { Fcu };
        double d;

        // 4. In the void at the unit's height, duct behind it, mouth on
        //    the unit - the kifaf master bedroom over the ensuite
        int four = Aim(V(4.6, 1.2, 3.1), V(0, -1, 0), V(4.6, 1.35, 3.1), units, out d);

        Check("case 4: a bellmouth in the void, mouth on the unit, feeds it (0.65 m away)",
            four == 0 && Near(Math.Round(d, 6), 0.65) &&
            VENTFACING.RoleOf(bellmouth, four >= 0) == VENTFACING.Role.Feeds);

        // 1. The same unit in the room, the bellmouth discharging DOWN
        //    into the room through the ceiling
        int oneDown = Aim(V(4.6, 1.4, 2.95), V(0, 0, 1), V(4.6, 1.4, 2.8), units, out d);

        // ...or through a bulkhead, sideways, under the ceiling - aimed
        //    at the unit in plan, but at the room's height, not the void's
        int oneSide = Aim(V(4.6, 1.0, 2.4), V(0, -1, 0), V(4.6, 1.15, 2.4), units, out d);

        Check("case 1: discharging down into the room, an FCU beside it - serves its room",
            oneDown < 0 && VENTFACING.RoleOf(bellmouth, oneDown >= 0) == VENTFACING.Role.InRoom);

        Check("case 1: through a bulkhead toward the unit in plan, but below the ceiling - serves its room",
            oneSide < 0 && VENTFACING.RoleOf(bellmouth, oneSide >= 0) == VENTFACING.Role.InRoom);

        // 2. No unit anywhere near
        int two = Aim(V(12, 8, 3.1), V(0, -1, 0), V(12, 8.15, 3.1), units, out d);

        Check("case 2: no FCU in front of it - serves its room",
            two < 0 && VENTFACING.RoleOf(bellmouth, two >= 0) == VENTFACING.Role.InRoom);

        // 3. An endcap is traced whichever way it points - a blank cap
        //    has no mouth
        Check("case 3: an endcap is traced to its unit, facing it or not",
            VENTFACING.RoleOf(endcap, false) == VENTFACING.Role.Endcap &&
            VENTFACING.RoleOf(endcap, true) == VENTFACING.Role.Endcap);

        // ---- the geometry's edges -------------------------------------
        Check("a bellmouth with its BACK to the unit does not feed it",
            Aim(V(4.6, 1.2, 3.1), V(0, 1, 0), V(4.6, 1.05, 3.1), units, out d) < 0);

        Check("a unit beyond the reach is not fed (2 m away, reach 1.5 m)",
            Aim(V(4.6, 0.0, 3.1), V(0, -1, 0), V(4.6, 0.15, 3.1), units, out d) < 0);

        // THE API DOES NOT SAY WHICH WAY A CONNECTOR'S Z POINTS. Either
        // way, the body - on the mouth side - settles it
        double[] onePointing = VENTFACING.Mouth(V(0, -1, 0), V(4.6, 1.2, 3.1), V(4.6, 1.35, 3.1));
        double[] otherPointing = VENTFACING.Mouth(V(0, 1, 0), V(4.6, 1.2, 3.1), V(4.6, 1.35, 3.1));

        Check("the mouth is the same whichever way the connector's Z points - the body decides",
            Near(onePointing[1], 1) && Near(otherPointing[1], 1));

        Check("...and a body centred on the connector leaves it to the connector",
            Near(VENTFACING.Mouth(V(0, 0, 1), V(1, 1, 1), V(1, 1, 1))[2], -1) &&
            VENTFACING.Mouth(V(0, 0, 0), V(1, 1, 1), null) == null);

        Check("a mouth 100 mm under the unit's edge still feeds it; 200 mm under does not",
            Aim(V(4.6, 1.2, 2.8), V(0, -1, 0), V(4.6, 1.35, 2.8), units, out d) == 0 &&
            Aim(V(4.6, 1.2, 2.7), V(0, -1, 0), V(4.6, 1.35, 2.7), units, out d) < 0);

        // Two units in line, both within the reach, the farther listed
        // first: the first the air reaches
        var two_units = new List<VENTFACING.Box>
        {
            new VENTFACING.Box { Min = V(4.0, 2.8, 2.9), Max = V(5.2, 3.0, 3.3) },
            Fcu,
            null
        };

        Check("two units in line: the nearer is fed; an unreadable box is passed over",
            Aim(V(4.6, 1.2, 3.1), V(0, -1, 0), V(4.6, 1.35, 3.1), two_units, out d) == 1 && Near(Math.Round(d, 6), 0.65) &&
            VENTFACING.Hit(V(4.6, 1.2, 3.1), V(0, 1, 0), two_units[0], Reach, Margin) > 0);

        Check("a mouth already inside the unit's box feeds it, at 0",
            VENTFACING.Hit(V(4.5, 2.2, 3.0), V(1, 0, 0), Fcu, Reach, Margin) == 0);

        Check("no direction, no box or no reach is no hit, and does not throw",
            VENTFACING.Hit(V(4.6, 1.2, 3.1), V(0, 0, 0), Fcu, Reach, Margin) < 0 &&
            VENTFACING.Hit(V(4.6, 1.2, 3.1), V(0, 1, 0), null, Reach, Margin) < 0 &&
            VENTFACING.Hit(V(4.6, 1.2, 3.1), V(0, 1, 0), Fcu, 0, Margin) < 0);

        // ---- the names ------------------------------------------------
        Check("a diffuser facing a unit is still in-room - only bellmouths are aimed",
            VENTFACING.RoleOf("M_Supply Diffuser", true) == VENTFACING.Role.InRoom);

        Check("a family named both ways is an endcap - endcaps have always been traced",
            VENTFACING.RoleOf("BellMouth Endcap", false) == VENTFACING.Role.Endcap);

        VENTSETTINGS.BellmouthNames = "";

        Check("no bellmouth names: every bellmouth serves its room, as before either list existed",
            VENTFACING.RoleOf(bellmouth, true) == VENTFACING.Role.InRoom);

        VENTSETTINGS.BellmouthNames = VENTSETTINGS.DefaultBellmouthNames;

        Decisions();
    }

    // ==================================================================
    // VENTFACING.Bellmouth - the whole decision, after the first real run
    // (kifaf LEVEL 04A, 2026-10-11): facing alone found 8 of 32, and four
    // rooms got no fresh air.
    // ==================================================================

    private static VENTFACING.Role Decide(bool faces, bool touching, bool inReach, double mouthZ,
                                          bool inRoom, bool serves, double air, out VENTFACING.Reason why)
    {
        return VENTFACING.Bellmouth(faces, touching, inReach, mouthZ, inRoom, serves, air, out why);
    }

    private static void Decisions()
    {
        Console.WriteLine();
        Console.WriteLine("  bellmouths - the decision");

        VENTFACING.Reason why;

        // The kifaf cases, as the log described them
        Check("an ensuite bellmouth beside the bedroom's FCU, pointing past it: the ensuite needs no fresh air, so it feeds the FCU",
            Decide(false, false, true, 0, true, false, 0, out why) == VENTFACING.Role.Feeds &&
            why == VENTFACING.Reason.ItsRoomNeedsNone);

        Check("the living FCU's bellmouth in the kitchen, the FCU supplying the kitchen too: feeds it - living and kitchen air both",
            Decide(false, false, true, 0, true, true, 70, out why) == VENTFACING.Role.Feeds &&
            why == VENTFACING.Reason.UnitServesItsRoom);

        Check("...and touching the FCU's casing, it feeds it whatever the room needs",
            Decide(false, true, true, 0, true, false, 70, out why) == VENTFACING.Role.Feeds &&
            why == VENTFACING.Reason.Touching);

        Check("a corridor bellmouth that needs fresh air, beside an FCU that does not supply the corridor: the corridor's",
            Decide(false, false, true, 0, true, false, 40, out why) == VENTFACING.Role.InRoom &&
            why == VENTFACING.Reason.ItsRoomNeedsAir);

        // The user's four cases
        Check("case 1: discharging DOWN into its room, an FCU beside it - the room's",
            Decide(false, false, true, -1, true, true, 60, out why) == VENTFACING.Role.InRoom &&
            why == VENTFACING.Reason.IntoRoomBelow);

        Check("case 2: no unit within the reach - the room's",
            Decide(false, false, false, 0, true, false, 60, out why) == VENTFACING.Role.InRoom &&
            why == VENTFACING.Reason.NoUnit);

        Check("case 4: facing the unit - feeds it, before anything else is asked",
            Decide(true, false, true, -1, true, false, 60, out why) == VENTFACING.Role.Feeds &&
            why == VENTFACING.Reason.Faces);

        Check("a bellmouth in no room in scope, a unit within the reach: feeds it",
            Decide(false, false, true, 0, false, false, 0, out why) == VENTFACING.Role.Feeds &&
            why == VENTFACING.Reason.InNoRoom);

        Check("a mouth only a little down (30 degrees) is not 'into the room below'",
            Decide(false, false, true, -0.5, true, false, 0, out why) == VENTFACING.Role.Feeds);

        Check("every reason has words for the log",
            ((VENTFACING.Reason[])Enum.GetValues(typeof(VENTFACING.Reason)))
                .All(r => !string.IsNullOrEmpty(VENTFACING.Say(r))));

        // ---- the two geometry helpers --------------------------------
        Check("distance to a box: 0 inside, along one axis outside, and diagonally past a corner",
            VENTFACING.Distance(V(4.5, 2.2, 3.0), Fcu) == 0 &&
            Near(Math.Round(VENTFACING.Distance(V(4.5, 1.8, 3.0), Fcu), 9), 0.2) &&
            Near(Math.Round(VENTFACING.Distance(V(3.7, 1.6, 3.0), Fcu), 9), 0.5));

        Check("...and a bellmouth 200 mm off the casing is beside it (300 mm), 400 mm is not",
            VENTFACING.Distance(V(4.5, 1.8, 3.0), Fcu) <= VENTFACING.TouchingM &&
            VENTFACING.Distance(V(4.5, 1.6, 3.0), Fcu) > VENTFACING.TouchingM);

        // A sidewall grille in a wall at x = 0, facing +x: its room is the
        // one it blows into, not whichever side its insertion point is on
        double[] front = VENTFACING.InFront(V(0, 5, 2.6), V(1, 0, 0), 0.3);
        double[] down = VENTFACING.InFront(V(3, 4, 2.7), V(0, 0, -1), 0.3);

        Check("a sidewall grille's room is judged 300 mm in front of its face",
            Near(front[0], 0.3) && Near(front[1], 5));

        Check("...a ceiling diffuser's in the same place in plan, as before",
            Near(down[0], 3) && Near(down[1], 4) &&
            VENTFACING.InFront(V(1, 1, 1), null, 0.3)[0] == 1);
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

        // ---- AIR WITH NOWHERE TO GO --------------------------------
        //
        // The kifaf 1BDR-C: MASTER BEDROOM 50 l/s, its unit's bellmouth
        // taken for an ensuite terminal, so nothing carried it - and the
        // run said nothing. Exactly REVITROOMS' calls.
        {
            var fresh = new Dictionary<string, double>
            {
                ["BED"] = 50, ["ENSUITE"] = 0, ["LIVING"] = 10, ["KITCHEN"] = 20, ["DRESSING"] = 0
            };

            Check("an endcap carries the rooms of its unit that have air - not those with none, not others",
                string.Join(",", VENTFCUCHECK.CarriedRooms(new[] { "LIVING", "DRESSING", "LIVING", "NOTINSHEET" }, fresh))
                == "LIVING");

            var landed = new HashSet<string>();

            landed.UnionWith(VENTFCUCHECK.CarriedRooms(new[] { "LIVING" }, fresh));   // the endcap
            landed.Add("KITCHEN");                                                       // a terminal inside

            List<string> nowhere = VENTFCUCHECK.Nowhere(fresh, landed, k => "121.046 MASTER " + k);

            Check("the bedroom is the one room said - a room with no air is never one",
                nowhere.Count == 1 && nowhere[0] == "121.046 MASTER BED: 50 l/s");

            landed.UnionWith(VENTFCUCHECK.CarriedRooms(new[] { "BED", "ENSUITE" }, fresh));   // the bellmouth, named

            Check("...and with the bellmouth taken as an endcap, nothing is",
                VENTFCUCHECK.Nowhere(fresh, landed, null).Count == 0);

            Check("...several are sorted, and a part litre shows",
                string.Join("|", VENTFCUCHECK.Nowhere(
                    new Dictionary<string, double> { ["b"] = 12.5, ["A"] = 7 }, new HashSet<string>(), null))
                == "A: 7 l/s|b: 12.5 l/s");

            Check("...nothing given is nothing said, and does not throw",
                VENTFCUCHECK.Nowhere(null, null, null).Count == 0 &&
                VENTFCUCHECK.CarriedRooms(null, fresh).Count == 0);
        }

        // ---- ONLY ROOMS WITH AIR ARE WORTH A LINE --------------------
        //
        // kifaf (2026-10-11): KITCHEN 121.126, no fresh air in the sheet,
        // listed as "1 unit(s) have NO fresh endcap".
        {
            var job = new Job();

            job.Unit(1, 1, "KITCHEN", "LIVING");
            job.Unit(2, 0, "KITCHEN");
            job.ExtractOn[1] = 1;
            job.ExtractOn[2] = 0;

            Dictionary<string, VENTFCUCHECK.Room> tally = job.Tally();

            var fresh = new Dictionary<string, double> { ["KITCHEN"] = 0, ["LIVING"] = 80 };
            var extract = new Dictionary<string, double> { ["KITCHEN"] = 0 };

            Check("a room flagged on the fresh side but with no fresh air in the sheet is not listed",
                VENTFCUCHECK.Worth(tally.Values).Count == 1 &&
                VENTFCUCHECK.Worth(tally, fresh, extract).Count == 0);

            fresh["KITCHEN"] = 70;

            List<VENTFCUCHECK.Room> worth = VENTFCUCHECK.Worth(tally, fresh, extract);

            Check("...with fresh air it is, and the line speaks of fresh only - its extract side has no air",
                worth.Count == 1 && worth[0].Key == "KITCHEN" &&
                VENTFCUCHECK.Line(worth[0], fresh, extract).Contains("NO fresh endcap") &&
                !VENTFCUCHECK.Line(worth[0], fresh, extract).Contains("extract"));

            Check("...a room the sheet has not got counts as none",
                VENTFCUCHECK.Worth(tally, null, null).Count == 0);
        }

        Merged();

        Facing();

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
