using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Linq;

// Getting a LINKED room onto a storey of the MAIN model.
//
// The real VENTLEVELMAP is compiled in, not mirrored. Everything here
// is in FEET, because that is what Revit stores and what the mapping
// is handed - 1 ft = 304.8 mm. Every elevation handed in is the room's
// own FLOOR (its level plus its base offset) in the main model's
// coordinates.
//
// The rule, at the user's request: a room belongs to the level of the
// main model whose BAND its floor stands in - from that level up to
// the next. Level names play no part. It replaced "the nearest
// storey", and the first block below is the case that tells the two
// apart.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static double Mm(double mm)
    {
        return mm / 304.8;
    }

    private static VENTLEVELMAP.Storey At(string name, double metres)
    {
        return new VENTLEVELMAP.Storey
        {
            Name = name,
            Elevation = Mm(metres * 1000.0)
        };
    }

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("AUTOVENT - linked rooms onto the main model's storeys, by band");
        Console.WriteLine();

        // A normal building, in the MAIN model's own words
        var storeys = new List<VENTLEVELMAP.Storey>
        {
            At("B1_BASEMENT", -4.0),
            At("GR_GROUND FLOOR", 0.0),
            At("P1_PODIUM 1", 4.5),
            At("P2_PODIUM 2", 9.0),
            At("P3_PODIUM 3", 13.5)
        };

        VENTLEVELMAP.InOrder(storeys);

        Check("the storeys come out lowest first",
            storeys[0].Name == "B1_BASEMENT"
            && storeys[4].Name == "P3_PODIUM 3");

        // ---- THE BAND, which "nearest" got wrong --------------------
        Check("a mezzanine at +2.5 m is in the GROUND band (nearest said PODIUM 1)",
            VENTLEVELMAP.NameAt(storeys, Mm(2500)) == "GR_GROUND FLOOR");

        Check("so is a floor at +3.0 m - still under PODIUM 1's level",
            VENTLEVELMAP.NameAt(storeys, Mm(3000)) == "GR_GROUND FLOOR");

        Check("and at +4.1 m, just under PODIUM 1, it is still GROUND's band minus the allowance",
            VENTLEVELMAP.NameAt(storeys, Mm(4100)) == "GR_GROUND FLOOR");

        Check("halfway up is decided by the band, not by a tie",
            VENTLEVELMAP.NameAt(storeys, Mm(2250)) == "GR_GROUND FLOOR");

        // ---- on the level ------------------------------------------
        Check("a floor exactly on a level lands on it",
            VENTLEVELMAP.NameAt(storeys, Mm(0)) == "GR_GROUND FLOOR");

        Check("and so does one four and a half metres up",
            VENTLEVELMAP.NameAt(storeys, Mm(4500)) == "P1_PODIUM 1");

        Check("the basement is not forgotten",
            VENTLEVELMAP.NameAt(storeys, Mm(-4000)) == "B1_BASEMENT");

        Check("a floor 50 mm ABOVE its level (screed on slab) stays on it",
            VENTLEVELMAP.NameAt(storeys, Mm(50)) == "GR_GROUND FLOOR");

        // ---- THE ALLOWANCE -----------------------------------------
        //
        // The arch floor 50 mm BELOW the main model's level. A bare "at or
        // below" would drop the whole floor into the BASEMENT's band.
        Check("a floor 50 mm BELOW its level stays on that level",
            VENTLEVELMAP.NameAt(storeys, Mm(-50)) == "GR_GROUND FLOOR");

        Check("150 mm below a podium level (a sunken wet area) is still that podium",
            VENTLEVELMAP.NameAt(storeys, Mm(4350)) == "P1_PODIUM 1");

        Check("299 mm below is still inside the allowance",
            VENTLEVELMAP.NameAt(storeys, Mm(8701)) == "P2_PODIUM 2");

        Check("301 mm below is the storey beneath - the allowance is 300 mm, no more",
            VENTLEVELMAP.NameAt(storeys, Mm(8699)) == "P1_PODIUM 1");

        Check("the allowance is 300 mm",
            Math.Abs(VENTLEVELMAP.Allowance * 304.8 - 300) < 1e-9);

        // ---- past the ends -----------------------------------------
        Check("above everything is in the highest level's band",
            VENTLEVELMAP.NameAt(storeys, Mm(40000)) == "P3_PODIUM 3");

        Check("200 mm under the LOWEST level is still on it (allowance)",
            VENTLEVELMAP.NameAt(storeys, Mm(-4200)) == "B1_BASEMENT");

        Check("400 mm under the lowest level is in NO band - -1, not the lowest",
            VENTLEVELMAP.StoreyOf(storeys, Mm(-4400)) == -1);

        Check("and it names nothing rather than inventing a heading",
            VENTLEVELMAP.NameAt(storeys, Mm(-20000)).Length == 0);

        // ---- names play no part ------------------------------------
        {
            // The main model calls its levels whatever it likes; the
            // architect's names never enter into it
            var odd = new List<VENTLEVELMAP.Storey>
            {
                At("MEP-00", 0.0),
                At("MEP-01", 3.6),
                At("ROOF PLANT", 7.2)
            };

            VENTLEVELMAP.InOrder(odd);

            Check("levels named nothing like the architect's still take their rooms",
                VENTLEVELMAP.NameAt(odd, Mm(3650)) == "MEP-01"
                && VENTLEVELMAP.NameAt(odd, Mm(7000)) == "ROOF PLANT");
        }

        // ---- order is not trusted ----------------------------------
        {
            var shuffled = new List<VENTLEVELMAP.Storey>
            {
                At("P2", 9.0),
                At("GR", 0.0),
                At("P1", 4.5)
            };

            Check("an unsorted list gives the same answer",
                VENTLEVELMAP.NameAt(shuffled, Mm(6000)) == "P1"
                && VENTLEVELMAP.NameAt(shuffled, Mm(100)) == "GR");
        }

        // ---- nothing to map onto -----------------------------------
        {
            var none = new List<VENTLEVELMAP.Storey>();

            Check("a model with no levels answers -1, not a crash",
                VENTLEVELMAP.StoreyOf(none, 0) == -1);

            Check("and names nothing",
                VENTLEVELMAP.NameAt(none, 0).Length == 0);

            Check("null is survivable too",
                VENTLEVELMAP.StoreyOf(null, 0) == -1);

            VENTLEVELMAP.InOrder(null);

            Check("sorting nothing does not throw", true);
        }

        // ---- two levels at one elevation ---------------------------
        {
            // Real models carry these - a plan level and a ceiling
            // level sharing a height. The sort must still be settled,
            // or the sheet's level order changes between runs.
            var twins = new List<VENTLEVELMAP.Storey>
            {
                At("L2_CEILING", 4.5),
                At("L1_FLOOR", 0.0),
                At("L2_FLOOR", 4.5)
            };

            VENTLEVELMAP.InOrder(twins);

            Check("levels at the same height are sorted by NAME",
                twins[0].Name == "L1_FLOOR"
                && twins[1].Name == "L2_CEILING"
                && twins[2].Name == "L2_FLOOR");

            string first = VENTLEVELMAP.NameAt(twins, Mm(4600));

            Check("a room on the shared height takes the first of them, every run",
                first == "L2_CEILING"
                && VENTLEVELMAP.NameAt(twins, Mm(4600)) == first);
        }

        // ---- a null in the list ------------------------------------
        {
            var holed = new List<VENTLEVELMAP.Storey>
            {
                At("GR", 0.0),
                null,
                At("L1", 4.0)
            };

            Check("a null entry is stepped over, not thrown on",
                VENTLEVELMAP.NameAt(holed, Mm(3900)) == "L1"
                && VENTLEVELMAP.NameAt(holed, Mm(3500)) == "GR");
        }

        // ---- Floors: the window's "L04 to L10, L12" -------------------
        {
            var tower = new List<VENTLEVELMAP.Storey>();

            for (int i = 0; i < 14; i++)
                tower.Add(At("L" + i.ToString("00"), i * 3.5));

            Check("consecutive storeys read as one range",
                VENTLEVELMAP.Floors(new[] { 4, 5, 6, 7, 8, 9, 10 }, tower) == "L04 to L10");

            Check("a gap starts a new part, a lone storey stands alone",
                VENTLEVELMAP.Floors(new[] { 4, 5, 6, 12 }, tower) == "L04 to L06, L12");

            Check("unsorted and repeated indices read the same",
                VENTLEVELMAP.Floors(new[] { 12, 6, 4, 5, 5, 6 }, tower) == "L04 to L06, L12");

            Check("an index off the list is ignored, not thrown on",
                VENTLEVELMAP.Floors(new[] { -1, 3, 99 }, tower) == "L03");

            Check("nothing reads as nothing",
                VENTLEVELMAP.Floors(new int[0], tower) == ""
                && VENTLEVELMAP.Floors(null, tower) == "");
        }

        // ---- apartments: verified on THIS model's levels -----------------
        //
        // An interior model placed once per band of floors, architects'
        // models holding the apartments. Each room is offered what each
        // apartments model said about it; the REAL Map.Settle decides.
        {
            var tower = new List<VENTLEVELMAP.Storey>();

            for (int i = 0; i < 12; i++)
                tower.Add(At("L" + i.ToString("00"), i * 3.5));

            const long ArcA = 100, ArcB = 200, Ind1 = 301, Ind2 = 302;

            VENTAPARTMENTS.Apartment Apt(long source, int id, string name)
            {
                return new VENTAPARTMENTS.Apartment { Key = source + ":" + id, Name = name, Number = id.ToString() };
            }

            int StoreyAt(double metres)
            {
                return VENTLEVELMAP.StoreyOf(tower, Mm(metres * 1000.0));
            }

            VENTAPARTMENTS.Candidate From(long source, VENTAPARTMENTS.Apartment apt, double floorMetres)
            {
                return new VENTAPARTMENTS.Candidate { Source = source, Apartment = apt, Storey = StoreyAt(floorMetres) };
            }

            var map = new VENTAPARTMENTS.Map
            {
                Storeys = tower,
                Labels = new Dictionary<long, string>
                {
                    { ArcA, "ARC : 1 : Tower A" }, { ArcB, "ARC : 2 : Tower B" },
                    { Ind1, "IND : 1" }, { Ind2, "IND : 2" }
                }
            };

            map.Sources.Add(ArcA);
            map.Sources.Add(ArcB);

            // A bedroom on L04, its slab 50 mm under the architect's
            // finished floor: the allowance keeps both on L04
            var a401 = Apt(ArcA, 401, "APT 401");
            var got = map.Settle(Ind1, "301:1", StoreyAt(14.0 - 0.05),
                new[] { From(ArcA, a401, 14.0) });

            Check("a room inside an apartment on the same storey joins it",
                got != null && map.Of("301:1") == a401 && map.Holding.Contains(a401.Key));

            Check("...and its placement is now read, ticked or not",
                map.FoundIn.Contains(Ind1)
                && map.RoomLinks(new long[] { ArcA }).SequenceEqual(new long[] { ArcA, Ind1 }));

            // A DUPLEX: the upper bedroom (L05) stands inside the apartment
            // drawn on L04 - in space, not by this model's levels
            got = map.Settle(Ind1, "301:2", StoreyAt(17.5),
                new[] { From(ArcA, a401, 14.0) });

            Check("a room inside an apartment on ANOTHER storey is not grouped",
                got == null && map.Of("301:2") == null);

            Check("...and is counted, so the window can say so",
                map.OtherStorey == 1);

            Check("...and an unticked placement does not give it to the run",
                !map.Takes(Ind1, "301:2", new long[] { ArcA }));

            // Two apartments models answer, the first on the wrong storey:
            // the one on the right storey wins, whichever was asked first
            var b501 = Apt(ArcB, 501, "APT 501");
            got = map.Settle(Ind2, "302:1", StoreyAt(17.5),
                new[] { From(ArcA, a401, 14.0), From(ArcB, b501, 17.5) });

            Check("of two models, the apartment on the room's own storey wins",
                got != null && got.Source == ArcB && map.Of("302:1") == b501);

            Check("...and a wrong-storey answer beside a right one is not counted",
                map.OtherStorey == 1);

            // Both wrong: counted ONCE, not once per model
            got = map.Settle(Ind2, "302:2", StoreyAt(28.0),
                new[] { From(ArcA, a401, 14.0), From(ArcB, b501, 17.5) });

            Check("a room every model places on another storey is counted once",
                got == null && map.OtherStorey == 2);

            // Both right: the first model asked, every run
            var b402 = Apt(ArcB, 402, "APT 402 B");
            got = map.Settle(Ind1, "301:3", StoreyAt(14.0),
                new[] { From(ArcA, a401, 14.0), From(ArcB, b402, 14.0) });

            Check("two models both right: the first asked takes it",
                got != null && got.Source == ArcA && !map.Holding.Contains(b402.Key));

            // Below every level: in no band, so in no apartment
            got = map.Settle(Ind1, "301:4", StoreyAt(-5.0),
                new[] { From(ArcA, a401, -5.0) });

            Check("a room below every level of this model joins nothing, and is not counted",
                got == null && map.Of("301:4") == null && map.OtherStorey == 2);

            Check("...nor is it, or the duplex, counted as standing in NO apartment",
                map.Outside == 0 && map.OutsideOn.Count == 0);

            // IN NO APARTMENT AT ALL - user-reported as interior rooms that
            // "are not exported", with nothing on screen to say why. Not
            // grouped, not read - and COUNTED, by storey.
            got = map.Settle(Ind1, "301:5", StoreyAt(14.0), new VENTAPARTMENTS.Candidate[0]);

            Check("a room no apartments model has a room around joins nothing",
                got == null && map.Of("301:5") == null && !map.Takes(Ind1, "301:5", new long[] { ArcA }));

            Check("...and is counted as in NO apartment, on its own storey - not as another level",
                map.Outside == 1 && map.OutsideOn.Count == 1 && map.OutsideOn[StoreyAt(14.0)] == 1 &&
                map.OtherStorey == 2);

            map.Settle(Ind2, "302:5", StoreyAt(17.5), null);
            map.Settle(Ind2, "302:6", StoreyAt(17.5),
                new[] { new VENTAPARTMENTS.Candidate { Source = ArcA, Apartment = null, Storey = StoreyAt(17.5) } });

            Check("no answers, or answers naming no apartment, count the same way",
                map.Outside == 3 && map.OutsideOn[StoreyAt(17.5)] == 2 && map.OtherStorey == 2);

            Check("...and a room in no apartment pairs its placement with nothing",
                !map.Pairs.ContainsKey(Ind1) || !map.Pairs[Ind1].Values.Any(s => s.Contains(StoreyAt(17.5))));

            // The same interior model placed again, higher up, inside the
            // other tower's apartments - each placement is its own pairing
            for (int floor = 5; floor <= 7; floor++)
            {
                map.Settle(Ind2, "302:" + (10 + floor), floor,
                    new[] { From(ArcB, Apt(ArcB, 500 + floor, "APT"), floor * 3.5) });
            }

            List<string> said = map.Describe();

            // "Redundant rooms" on screen since 2026-10-26 ("Containers"
            // from 2026-10-19; the user)
            Check("each placement says which containers it was found in, on which floors",
                said.Count == 2
                && said.Contains("IND : 1  ->  ARC : 1 : Tower A redundant rooms on L04")
                && said.Contains("IND : 2  ->  ARC : 2 : Tower B redundant rooms on L05 to L07"));

            // What a run reads
            var ticked = new List<long> { ArcA, ArcB };

            Check("a ticked link gives every room",
                map.Takes(ArcA, "100:77", ticked));

            Check("an unticked placement gives only the rooms inside apartments",
                map.Takes(Ind1, "301:1", ticked) && !map.Takes(Ind1, "301:9", ticked));

            Check("the run reads the ticked links, then the placements found - nothing else",
                map.RoomLinks(ticked).SequenceEqual(new long[] { ArcA, ArcB, Ind1, Ind2 }));

            Check("a ticked interior placement is not listed twice",
                map.RoomLinks(new long[] { Ind2, ArcA }).SequenceEqual(new long[] { Ind2, ArcA, Ind1 }));

            Check("the label names every apartments model",
                map.Label == "ARC : 1 : Tower A, ARC : 2 : Tower B");

            // ONE APARTMENT, ROOMS FROM SEVERAL PLACEMENTS - user-reported
            // as "2BDR-A1 showing rooms from other apartments": one
            // architect's room ran across four apartments and took the
            // rooms of four interior placements. Not refused (it cannot be
            // told from a real case for sure), but named.
            {
                var spreadMap = new VENTAPARTMENTS.Map { Storeys = tower };
                var a1 = Apt(ArcA, 409, "2BDR-A1");
                var a2 = Apt(ArcA, 404, "2BDR-A2");

                spreadMap.Settle(Ind1, "301:21", 4, new[] { From(ArcA, a1, 14.0) });
                spreadMap.Settle(Ind1, "301:22", 4, new[] { From(ArcA, a1, 14.0) });
                spreadMap.Settle(Ind1, "301:23", 4, new[] { From(ArcA, a2, 14.0) });
                spreadMap.Settle(Ind2, "302:21", 4, new[] { From(ArcA, a2, 14.0) });

                Check("one placement giving rooms to several apartments is NOT flagged - an interior model per floor",
                    spreadMap.PlacementsIn[a1.Key].Count == 1 && !spreadMap.Spread().Contains(a1));

                Check("an apartment taking rooms from two placements IS flagged, by name and number",
                    spreadMap.Spread().Count == 1 && spreadMap.Spread()[0] == a2 &&
                    spreadMap.DescribeSpread().SequenceEqual(new[] { "2BDR-A2 (404) - rooms of 2 placements" }));

                spreadMap.Settle(Ind2, "302:22", 4, new VENTAPARTMENTS.Candidate[0]);

                Check("...and a room in no apartment takes part in no apartment's count",
                    spreadMap.PlacementsIn.Count == 2 && spreadMap.Spread().Count == 1);
            }

            var none = new VENTAPARTMENTS.Map();

            Check("no apartments models: off, and reads exactly the ticked links",
                !none.On && none.RoomLinks(ticked).SequenceEqual(ticked)
                && none.Settle(1, "1:1", 0, null) == null && none.Outside == 1);
        }

        // ---- LevelError: a model's levels measured on its own rooms -----
        //
        // User-reported on the kifaf job (2026-10-10): "LEVEL 03 showing no
        // rooms". Every room of the architect's models was one level up -
        // the level arithmetic 3.49 m high against the rooms' own geometry.
        {
            double m = Mm(1000.0);          // a metre, in feet

            Check("no rooms measured is no error, and nothing at all is no error",
                VENTLEVELMAP.LevelError(new double[0]) == 0 && VENTLEVELMAP.LevelError(null) == 0);

            Check("rooms agreeing with their levels to within 50 mm are no error - a finish, not a fault",
                VENTLEVELMAP.LevelError(new[] { 0.02 * m, -0.03 * m, 0.0, 0.049 * m }) == 0);

            Check("every room 3.49 m under its level's arithmetic: the error is -3.49 m",
                Math.Abs(VENTLEVELMAP.LevelError(Enumerable.Repeat(-3.49 * m, 12)) + 3.49 * m) < 1e-9);

            Check("...and two rooms with odd boxes cannot move the model - the median",
                Math.Abs(VENTLEVELMAP.LevelError(
                    Enumerable.Repeat(-3.49 * m, 9).Concat(new[] { 10 * m, -20 * m })) + 3.49 * m) < 1e-9);

            Check("an even count takes the middle two, and nonsense is left out",
                Math.Abs(VENTLEVELMAP.LevelError(new[] { -3.0 * m, -4.0 * m, double.NaN, double.PositiveInfinity })
                         + 3.5 * m) < 1e-9);

            // The kifaf levels, from the apartments log
            var kifaf = new List<VENTLEVELMAP.Storey>
            {
                At("LEVEL 03", 33.62), At("LEVEL 04A", 37.12), At("LEVEL 05A", 40.62), At("LEVEL 06A", 44.12)
            };

            // TA-05-04: its box began at 40.620 m; its level said 44.110
            double byBox = 40.62 * m, byLevel = 44.11 * m;

            Check("the kifaf room by its level arithmetic lands a level UP - the reported fault",
                kifaf[VENTLEVELMAP.StoreyOf(kifaf, byLevel)].Name == "LEVEL 06A");

            Check("...by its geometry, on the level its number says (TA-05 on LEVEL 05A)",
                kifaf[VENTLEVELMAP.StoreyOf(kifaf, byBox)].Name == "LEVEL 05A");

            double error = VENTLEVELMAP.LevelError(Enumerable.Repeat(byBox - byLevel, 20));

            Check("...and a LEVEL of that model, corrected by the error its rooms measured, lands there too",
                kifaf[VENTLEVELMAP.StoreyOf(kifaf, byLevel + error)].Name == "LEVEL 05A");

            Check("...so a TA-03 room, two storeys (7.0 m) lower, is on LEVEL 03 - which used to show no rooms",
                kifaf[VENTLEVELMAP.StoreyOf(kifaf, byBox - 7.0 * m)].Name == "LEVEL 03");

            Check("...where by its level arithmetic it was on LEVEL 04A, leaving LEVEL 03 empty",
                kifaf[VENTLEVELMAP.StoreyOf(kifaf, byLevel - 7.0 * m)].Name == "LEVEL 04A");
        }

        // ---- RoomsOn: the levels list, from rooms already read ----------
        //
        // AUTOVENT's window reads each link's rooms once, inside the
        // external event, and a tick only runs RoomsOn over them. So this
        // is now everything a tick does - and what the list shows has to
        // be exactly what a run would process.
        {
            var bands = new List<VENTLEVELMAP.Storey>
            {
                At("GR", 0.0), At("L01", 4.0), At("L02", 8.0)
            };

            const long Arc = 10, Ind = 20, Pod = 30;

            RVTLINKSELECTION.LinkedRoom R(long link, long id, string number, string name, double floorMetres)
            {
                return new RVTLINKSELECTION.LinkedRoom
                {
                    LinkId = link, RoomId = id, Number = number, Name = name,
                    LinkName = "link " + link, LinkLevel = "Level " + floorMetres,
                    Floor = Mm(floorMetres * 1000.0)
                };
            }

            var roomsOf = new Dictionary<long, List<RVTLINKSELECTION.LinkedRoom>>
            {
                // The architect's model: a lobby, an apartment with rooms
                // inside it, and a basement room below every level here
                [Arc] = new List<RVTLINKSELECTION.LinkedRoom>
                {
                    R(Arc, 1, "G-001", "LOBBY", 0.0),
                    R(Arc, 2, "G-002", "DRIVER/STAFF APT", 0.0),
                    R(Arc, 3, "B-001", "STORE", -4.0),
                    R(Arc, 4, "101", "CORRIDOR", 4.0)
                },

                // The interior model, NOT ticked: two rooms inside the
                // apartment, one standing outside every apartment
                [Ind] = new List<RVTLINKSELECTION.LinkedRoom>
                {
                    R(Ind, 51, "G-02-2", "Toilet", 0.0),
                    R(Ind, 52, "G-02-1", "Bed room", 0.0),
                    R(Ind, 53, "X-1", "Loose room", 0.0)
                }
            };

            string K(long link, long room) => RVTLINKSELECTION.RoomKey(link, room);

            var apt = new VENTAPARTMENTS.Apartment { Key = K(Arc, 2), Name = "DRIVER/STAFF APT", Number = "G-002" };

            var map = new VENTAPARTMENTS.Map();
            map.Sources.Add(Arc);
            map.ByRoom[K(Ind, 51)] = apt;
            map.ByRoom[K(Ind, 52)] = apt;
            map.Holding.Add(apt.Key);
            map.FoundIn.Add(Ind);

            var on = RVTLINKSELECTION.RoomsOn(new[] { Arc, Pod }, roomsOf, map, bands);

            List<RVTLINKSELECTION.LinkedRoom> gr = on.ContainsKey("GR") ? on["GR"] : new List<RVTLINKSELECTION.LinkedRoom>();

            Check("each room lands under the storey its floor stands in",
                gr.Exists(r => r.RoomId == 1) && on.ContainsKey("L01") && on["L01"].Count == 1 && on["L01"][0].RoomId == 4);

            Check("a room below every level of this model is in no level",
                !on.Values.Any(l => l.Exists(r => r.RoomId == 3)));

            Check("an apartment with rooms inside is not a row - its rooms are",
                !gr.Exists(r => r.LinkId == Arc && r.RoomId == 2) &&
                gr.Exists(r => r.RoomId == 51) && gr.Exists(r => r.RoomId == 52));

            Check("an interior placement nobody ticked gives ONLY the rooms inside apartments",
                !gr.Exists(r => r.RoomId == 53));

            Check("the rooms inside carry their apartment",
                gr.Where(r => r.LinkId == Ind).All(r => r.ApartmentKey == apt.Key && r.Apartment == "DRIVER/STAFF APT"));

            Check("an apartment's rooms sit together, in number order inside it",
                gr.Count == 3 && gr[0].RoomId == 1 && gr[1].RoomId == 52 && gr[2].RoomId == 51);

            Check("a link ticked but not read yet gives nothing, and nothing throws",
                !on.Values.Any(l => l.Exists(r => r.LinkId == Pod)));

            Check("the rooms handed back are copies - what was read keeps no apartment",
                roomsOf[Ind].All(r => r.ApartmentKey == null));

            var plain = RVTLINKSELECTION.RoomsOn(new[] { Arc }, roomsOf, null, bands);

            Check("with no apartments the interior model, unticked, gives nothing at all",
                !plain.Values.Any(l => l.Exists(r => r.LinkId == Ind)) &&
                plain["GR"].Exists(r => r.RoomId == 2));

            var both = RVTLINKSELECTION.RoomsOn(new[] { Arc, Ind }, roomsOf, map, bands);

            Check("ticked, the interior model gives all its rooms - the loose one too",
                both["GR"].Exists(r => r.RoomId == 53) && both["GR"].Exists(r => r.RoomId == 51));

            Check("nothing ticked, or nothing read, is an empty list rather than an error",
                RVTLINKSELECTION.RoomsOn(new long[0], roomsOf, map, bands).Count == 0 &&
                RVTLINKSELECTION.RoomsOn(new[] { Arc }, null, map, bands).Count == 0 &&
                RVTLINKSELECTION.RoomsOn(null, roomsOf, map, bands).Count == 0);
        }

        // ---- VENTARRANGE: the rooms arranged by hand for the sheet ------
        //
        // User (2026-10-10): take any interior room out of its apartment so
        // it is written as a row of its own; drag the rooms - inside an
        // apartment, and the architect's rooms - into the order the sheet
        // should have; "keep everything under its level under its level".
        Arranging();

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    private static void Arranging()
    {
        Console.WriteLine();
        Console.WriteLine("  arranged by hand (VENTARRANGE)");

        var bands = new List<VENTLEVELMAP.Storey> { At("GR", 0.0), At("L01", 4.0) };

        const long Arc = 10, Ind = 20;

        RVTLINKSELECTION.LinkedRoom R(long link, long id, string number, string name, double floorMetres)
        {
            return new RVTLINKSELECTION.LinkedRoom
            {
                LinkId = link, RoomId = id, Number = number, Name = name,
                LinkName = "link " + link, Floor = Mm(floorMetres * 1000.0)
            };
        }

        string K(long link, long room) => RVTLINKSELECTION.RoomKey(link, room);

        var roomsOf = new Dictionary<long, List<RVTLINKSELECTION.LinkedRoom>>
        {
            [Arc] = new List<RVTLINKSELECTION.LinkedRoom>
            {
                R(Arc, 1, "G-001", "LOBBY", 0.0),
                R(Arc, 2, "G-002", "APT A", 0.0),
                R(Arc, 3, "G-003", "APT B", 0.0),
                R(Arc, 5, "G-005", "CORRIDOR", 0.0),
                R(Arc, 9, "101", "CORRIDOR", 4.0)
            },
            [Ind] = new List<RVTLINKSELECTION.LinkedRoom>
            {
                R(Ind, 21, "G-02-1", "Bed room", 0.0),
                R(Ind, 22, "G-02-2", "Toilet", 0.0),
                R(Ind, 23, "G-02-3", "Kitchen", 0.0),
                R(Ind, 31, "G-03-1", "Bed room", 0.0)
            }
        };

        var aptA = new VENTAPARTMENTS.Apartment { Key = K(Arc, 2), Name = "APT A", Number = "G-002" };
        var aptB = new VENTAPARTMENTS.Apartment { Key = K(Arc, 3), Name = "APT B", Number = "G-003" };

        var map = new VENTAPARTMENTS.Map();
        map.Sources.Add(Arc);
        map.FoundIn.Add(Ind);
        map.Holding.Add(aptA.Key);
        map.Holding.Add(aptB.Key);

        foreach (long id in new long[] { 21, 22, 23 })
            map.ByRoom[K(Ind, id)] = aptA;

        map.ByRoom[K(Ind, 31)] = aptB;

        List<string> Keys(VENTARRANGE.Arrangement a, string level = "GR")
        {
            var on = RVTLINKSELECTION.RoomsOn(new[] { Arc }, roomsOf, map, bands, a);
            return on.ContainsKey(level) ? on[level].Select(r => r.Key).ToList() : new List<string>();
        }

        // The rows as the dialog sees them, with their apartment at home
        List<VENTARRANGE.Row> Rows(VENTARRANGE.Arrangement a)
        {
            var on = RVTLINKSELECTION.RoomsOn(new[] { Arc }, roomsOf, map, bands, a);

            return on["GR"].Select(r =>
            {
                VENTAPARTMENTS.Apartment home = map.Of(r.Key);

                return new VENTARRANGE.Row
                {
                    Key = r.Key, Name = r.Name, ModelName = r.ModelName, Number = r.Number,
                    ModelApartment = r.ModelApartment,
                    ApartmentKey = r.ApartmentKey, Apartment = r.Apartment, ApartmentNumber = r.ApartmentNumber,
                    HomeKey = home == null ? null : home.Key, Home = home == null ? null : home.Name,
                    HomeNumber = home == null ? null : home.Number
                };
            }).ToList();
        }

        List<string> plain = Keys(null);

        var defaults = new List<string>
        {
            K(Arc, 1), K(Ind, 21), K(Ind, 22), K(Ind, 23), K(Ind, 31), K(Arc, 5)
        };

        Check("nothing arranged: exactly the default - by number, each apartment's rooms together",
            plain.SequenceEqual(defaults) && Keys(new VENTARRANGE.Arrangement()).SequenceEqual(defaults));

        // ---- the level's top rows in a chosen order
        var a = new VENTARRANGE.Arrangement();
        string level = VENTARRANGE.LevelContainer("GR");

        List<VENTARRANGE.Row> rows = Rows(a);

        Check("the level's top rows: each room of its own, each apartment ONCE",
            VENTARRANGE.SequenceOf(rows, level).SequenceEqual(new[] { K(Arc, 1), aptA.Key, aptB.Key, K(Arc, 5) }));

        Check("...and an apartment's own rooms",
            VENTARRANGE.SequenceOf(rows, VENTARRANGE.ApartmentContainer(aptA.Key))
                .SequenceEqual(new[] { K(Ind, 21), K(Ind, 22), K(Ind, 23) }));

        VENTARRANGE.SetOrder(a, level, new[] { K(Arc, 5), aptB.Key, K(Arc, 1), aptA.Key },
            VENTARRANGE.SequenceOf(rows, level));

        Check("a level put in a chosen order comes out in it - an apartment moving with all its rooms",
            Keys(a).SequenceEqual(new[] { K(Arc, 5), K(Ind, 31), K(Arc, 1), K(Ind, 21), K(Ind, 22), K(Ind, 23) }));

        // ---- an apartment's own rooms in a chosen order
        string inA = VENTARRANGE.ApartmentContainer(aptA.Key);

        VENTARRANGE.SetOrder(a, inA, new[] { K(Ind, 23), K(Ind, 21), K(Ind, 22) },
            VENTARRANGE.SequenceOf(Rows(new VENTARRANGE.Arrangement()), inA));

        Check("an apartment's rooms in their chosen order, still together and still where the apartment is",
            Keys(a).SequenceEqual(new[] { K(Arc, 5), K(Ind, 31), K(Arc, 1), K(Ind, 23), K(Ind, 21), K(Ind, 22) }));

        // ---- the default is not a choice
        var b = new VENTARRANGE.Arrangement();

        VENTARRANGE.SetOrder(b, level, defaults.Take(1).Concat(new[] { aptA.Key, aptB.Key, K(Arc, 5) }).ToList(),
            VENTARRANGE.SequenceOf(Rows(b), level));

        Check("an order that is just the default is not kept - a room drawn later still lands by number",
            b.IsEmpty);

        var bb = new VENTARRANGE.Arrangement();
        List<string> bDefault = VENTARRANGE.SequenceOf(Rows(bb), level);

        VENTARRANGE.SetOrder(bb, level, VENTARRANGE.Moved(bDefault, K(Arc, 5), 0), bDefault);
        bool changed = bb.Orders.ContainsKey(level);

        VENTARRANGE.SetOrder(bb, level, bDefault, bDefault);

        Check("...and one dragged away and back again is forgotten, not frozen",
            changed && bb.IsEmpty);

        // ---- nothing leaves its level
        var c = new VENTARRANGE.Arrangement();
        c.Orders[VENTARRANGE.LevelContainer("L01")] = new List<string> { K(Arc, 1), K(Arc, 9) };

        Check("an order naming another level's room cannot move it there",
            Keys(c, "L01").SequenceEqual(new[] { K(Arc, 9) }) && Keys(c).SequenceEqual(defaults));

        // ---- an order with a key that has gone, and a room it never knew
        var d = new VENTARRANGE.Arrangement();
        d.Orders[level] = new List<string> { "10:999", K(Arc, 5), aptA.Key };

        Check("a saved key nothing has any more is passed over; what the order never knew follows, by number",
            Keys(d).SequenceEqual(new[] { K(Arc, 5), K(Ind, 21), K(Ind, 22), K(Ind, 23), K(Arc, 1), K(Ind, 31) }));

        // ---- taking an interior room out of its apartment, and back
        var e = new VENTARRANGE.Arrangement();
        VENTARRANGE.Row toilet = Rows(e).First(r => r.Key == K(Ind, 22));

        VENTARRANGE.TakeOut(e, toilet, "GR");

        var taken = RVTLINKSELECTION.RoomsOn(new[] { Arc }, roomsOf, map, bands, e)["GR"];

        Check("taken out: a row of its own, with no apartment on it",
            taken.Any(r => r.Key == K(Ind, 22) && r.ApartmentKey == null) &&
            taken.Where(r => r.ApartmentKey == aptA.Key).Select(r => r.Key).SequenceEqual(new[] { K(Ind, 21), K(Ind, 23) }));

        Check("...the apartments map untouched - the airflow write and fixtures do what they did",
            map.Of(K(Ind, 22)) == aptA && map.Holding.Contains(aptA.Key));

        Check("...and with no order chosen it lands by its own number among the rooms",
            Keys(e).Last() == K(Ind, 22));

        var f = new VENTARRANGE.Arrangement();
        f.Orders[level] = new List<string> { K(Arc, 5), aptA.Key, aptB.Key, K(Arc, 1) };

        VENTARRANGE.TakeOut(f, Rows(f).First(r => r.Key == K(Ind, 22)), "GR");

        Check("with an order chosen, it is put straight under the apartment it came out of",
            Keys(f).SequenceEqual(new[] { K(Arc, 5), K(Ind, 21), K(Ind, 23), K(Ind, 22), K(Ind, 31), K(Arc, 1) }));

        VENTARRANGE.Row back = Rows(f).First(r => r.Key == K(Ind, 22));

        Check("the dialog's row knows it was taken out, and of which apartment",
            back.IsTakenOut && back.Home == "APT A");

        VENTARRANGE.PutBack(f, back, "GR");

        Check("put back: under its apartment again, and out of the level's own order",
            !f.Ungrouped.Contains(K(Ind, 22)) && !f.Orders[level].Contains(K(Ind, 22)) &&
            Keys(f).SequenceEqual(new[] { K(Arc, 5), K(Ind, 21), K(Ind, 22), K(Ind, 23), K(Ind, 31), K(Arc, 1) }));

        Check("an architect's room cannot be 'taken out' - it is in no apartment",
            !Rows(new VENTARRANGE.Arrangement()).First(r => r.Key == K(Arc, 1)).IsInterior);

        // ---- reset a level
        var g = a.Clone();
        VENTARRANGE.TakeOut(g, Rows(g).First(r => r.Key == K(Ind, 31)), "GR");
        VENTARRANGE.Reset(g, Rows(g), "GR");

        Check("Reset this level forgets its order, its apartments' orders and its rooms taken out",
            g.IsEmpty && Keys(g).SequenceEqual(defaults));

        Check("...and the arrangement it was cloned from is untouched",
            a.Orders.Count == 2);

        // ---- moving one key
        Check("Moved: one key to a new place, the rest in order; past either end goes to that end",
            VENTARRANGE.Moved(new[] { "a", "b", "c", "d" }, "d", 1).SequenceEqual(new[] { "a", "d", "b", "c" }) &&
            VENTARRANGE.Moved(new[] { "a", "b", "c" }, "a", 99).SequenceEqual(new[] { "b", "c", "a" }) &&
            VENTARRANGE.Moved(new[] { "a", "b", "c" }, "c", -5).SequenceEqual(new[] { "c", "a", "b" }) &&
            VENTARRANGE.Moved(new[] { "a", "b" }, "x", 0).SequenceEqual(new[] { "a", "b" }));

        // ---- searching the dialog's list ("look for rooms using keyword")
        List<VENTARRANGE.Row> all = Rows(new VENTARRANGE.Arrangement());

        HashSet<string> bed = VENTARRANGE.Shown(all, "bed");

        Check("a search shows the rooms it finds - and each one's apartment heading with it",
            bed.SetEquals(new[] { K(Ind, 21), aptA.Key, K(Ind, 31), aptB.Key }));

        Check("...every word, any order, any case - and by number as well as name",
            VENTARRANGE.Shown(all, "ROOM g-03").SetEquals(new[] { K(Ind, 31), aptB.Key }) &&
            VENTARRANGE.Shown(all, "g-005").SetEquals(new[] { K(Arc, 5) }));

        // Words are found INSIDE other words, as the links search finds
        // them - "apt a" would find APT B too (its "APT" holds an "a")
        Check("an apartment's number finds all its rooms",
            VENTARRANGE.Shown(all, "g-002").SetEquals(new[] { K(Ind, 21), K(Ind, 22), K(Ind, 23), aptA.Key }));

        var outA = new VENTARRANGE.Arrangement();
        VENTARRANGE.TakeOut(outA, Rows(outA).First(r => r.Key == K(Ind, 22)), "GR");

        Check("...and a room taken OUT of it too - by the apartment it stands in",
            VENTARRANGE.Shown(Rows(outA), "g-002").SetEquals(new[] { K(Ind, 21), K(Ind, 23), aptA.Key, K(Ind, 22) }) &&
            VENTARRANGE.Words(Rows(outA).First(r => r.Key == K(Ind, 22))).Contains("APT A"));

        Check("nothing typed shows everything; a word nothing has shows nothing",
            VENTARRANGE.Shown(all, "").Count == all.Count + 2 && VENTARRANGE.Shown(all, "kitchenette").Count == 0);

        // ---- renaming for the sheet ("rename rooms before exporting")
        var named = new VENTARRANGE.Arrangement();

        VENTARRANGE.Rename(named, K(Ind, 21), "  MASTER BEDROOM ", "Bed room");
        VENTARRANGE.Rename(named, aptA.Key, "2BDR-A1", "APT A");

        var renamedRooms = RVTLINKSELECTION.RoomsOn(new[] { Arc }, roomsOf, map, bands, named)["GR"];
        var bedroom = renamedRooms.First(r => r.Key == K(Ind, 21));

        Check("a renamed room is listed by its new name, trimmed, and keeps the model's",
            bedroom.Name == "MASTER BEDROOM" && bedroom.ModelName == "Bed room");

        Check("a renamed apartment heads its rooms by the new name",
            renamedRooms.Where(r => r.ApartmentKey == aptA.Key).All(r => r.Apartment == "2BDR-A1" && r.ModelApartment == "APT A"));

        Check("...the name the export and the airflow write both use (NameOf by the room key)",
            named.NameOf(RVTLINKSELECTION.RoomKey(Ind, 21), "Bed room") == "MASTER BEDROOM" &&
            named.NameOf(K(Arc, 1), "LOBBY") == "LOBBY");

        Check("still found by its old name",
            VENTARRANGE.Shown(Rows(named), "bed room").Contains(K(Ind, 21)));

        VENTARRANGE.Rename(named, K(Ind, 21), "", "Bed room");
        VENTARRANGE.Rename(named, aptA.Key, "APT A", "APT A");

        Check("emptied, or given the model's own name back, it is no rename at all",
            named.IsEmpty);

        // ---- find and replace across names
        var typo = new VENTARRANGE.Arrangement();

        int replaced = VENTARRANGE.Replace(typo, Rows(typo), "bed ROOM", "BEDROOM");

        Check("find and replace: every name holding it, any case - rooms only, here two",
            replaced == 2 && typo.Names[K(Ind, 21)] == "BEDROOM" && typo.Names[K(Ind, 31)] == "BEDROOM");

        Check("...and an apartment heading once, however many rooms are under it",
            VENTARRANGE.Replace(typo, Rows(typo), "APT", "FLAT") == 2 &&
            typo.Names[aptA.Key] == "FLAT A" && typo.Names[aptB.Key] == "FLAT B");

        Check("...the replacement is taken literally - a $ is a $",
            VENTARRANGE.Replace(typo, Rows(typo), "LOBBY", "$0 HALL") == 1 && typo.Names[K(Arc, 1)] == "$0 HALL");

        Check("...replaced back to the model's own name, it is no rename",
            VENTARRANGE.Replace(typo, Rows(typo), "FLAT", "APT") == 2 &&
            !typo.Names.ContainsKey(aptA.Key) && !typo.Names.ContainsKey(aptB.Key));

        Check("...and nothing to find changes nothing",
            VENTARRANGE.Replace(typo, Rows(typo), "", "X") == 0 && VENTARRANGE.Replace(typo, Rows(typo), "kitchenette", "X") == 0);

        var renamedLevel = new VENTARRANGE.Arrangement();
        VENTARRANGE.Rename(renamedLevel, K(Arc, 5), "LOBBY 2", "CORRIDOR");
        VENTARRANGE.Rename(renamedLevel, aptB.Key, "1BDR-C", "APT B");
        VENTARRANGE.Reset(renamedLevel, Rows(renamedLevel), "GR");

        Check("Reset this level forgets its rooms' and apartments' names too",
            renamedLevel.IsEmpty);

        // ---- moving while a search hides rows
        var seq = new List<string> { "a", "b", "c", "d", "e" };
        var showing = new HashSet<string> { "a", "c", "e" };

        Check("Move up with rows hidden: past the next row SHOWING, not swapped with an unseen one",
            VENTARRANGE.Stepped(seq, "e", -1, showing).SequenceEqual(new[] { "a", "b", "e", "c", "d" }));

        Check("Move down likewise",
            VENTARRANGE.Stepped(seq, "c", +1, showing).SequenceEqual(new[] { "a", "b", "d", "e", "c" }));

        Check("nothing showing that way: nothing moves",
            VENTARRANGE.Stepped(seq, "a", -1, showing).SequenceEqual(seq) &&
            VENTARRANGE.Stepped(seq, "e", +1, showing).SequenceEqual(seq));

        Check("no search: one place, as Move up / Move down always did",
            VENTARRANGE.Stepped(seq, "c", -1, null).SequenceEqual(new[] { "a", "c", "b", "d", "e" }) &&
            VENTARRANGE.Stepped(seq, "c", +1, null).SequenceEqual(new[] { "a", "b", "d", "c", "e" }));

        // ---- a link loaded again under a new id
        var h = new VENTARRANGE.Arrangement();
        h.Ungrouped.Add("20:22");
        h.Orders["level:GR"] = new List<string> { "10:5", "apt:10:2".Substring(4), "20:22" };
        h.Orders["apt:10:2"] = new List<string> { "20:23", "20:21" };
        h.Names["30:7"] = "KITCHEN";

        Check("the arrangement names its links, to be saved with their labels - renamed rooms' included",
            VENTARRANGE.Links(h).OrderBy(i => i).SequenceEqual(new long[] { 10, 20, 30 }));

        VENTARRANGE.Remap(h, new Dictionary<long, long> { [10] = 110, [20] = 120, [30] = 130 });

        Check("a renamed room follows its link to the new id",
            h.Names.Count == 1 && h.Names["130:7"] == "KITCHEN");

        Check("re-pointed to the new ids: rooms taken out, every order and the apartment containers",
            h.Ungrouped.SetEquals(new[] { "120:22" }) &&
            h.Orders["level:GR"].SequenceEqual(new[] { "110:5", "110:2", "120:22" }) &&
            h.Orders.ContainsKey("apt:110:2") && !h.Orders.ContainsKey("apt:10:2") &&
            h.Orders["apt:110:2"].SequenceEqual(new[] { "120:23", "120:21" }));

        Check("...a level's container is not a key and is left alone",
            VENTARRANGE.LinkOf("level:GR") == 0 && VENTARRANGE.LinkOf("apt:7:3") == 7 && VENTARRANGE.LinkOf("7:3") == 7);

        // ---- merging rooms one FCU serves through one fresh air connection
        var m = new VENTARRANGE.Arrangement();
        VENTARRANGE.Row Of(VENTARRANGE.Arrangement on, long link, long room) =>
            Rows(on).First(r => r.Key == K(link, room));

        Check("Merge needs two rooms",
            VENTARRANGE.Merge(m, new[] { Of(m, Arc, 1) }) != null && m.IsEmpty);

        Check("...of one apartment, or outside apartments - not one of each",
            VENTARRANGE.Merge(m, new[] { Of(m, Arc, 1), Of(m, Ind, 21) }) != null && m.IsEmpty);

        Check("two rooms merged: one group, named by the first",
            VENTARRANGE.Merge(m, new[] { Of(m, Arc, 1), Of(m, Arc, 5) }) == null &&
            m.Merges.Count == 1 && !m.IsEmpty &&
            m.MergeIdOf(K(Arc, 5)) == K(Arc, 1) && m.MergeIdOf(K(Arc, 1)) == K(Arc, 1) &&
            m.MergeIdOf(K(Ind, 21)) == null);

        Check("...and written next to each other, where the first stands - their cells merge down a column",
            Keys(m).SequenceEqual(new[] { K(Arc, 1), K(Arc, 5), K(Ind, 21), K(Ind, 22), K(Ind, 23), K(Ind, 31) }));

        var split = m.Clone();
        split.Orders[level] = new List<string> { K(Arc, 5), aptA.Key, K(Arc, 1), aptB.Key };

        Check("...even when the level's order puts an apartment between them",
            Keys(split).SequenceEqual(new[] { K(Arc, 5), K(Arc, 1), K(Ind, 21), K(Ind, 22), K(Ind, 23), K(Ind, 31) }));

        Check("Together leaves a list with no merges as it is",
            VENTARRANGE.Together(new[] { "a", "b" }, s => s, new VENTARRANGE.Arrangement()).SequenceEqual(new[] { "a", "b" }) &&
            VENTARRANGE.Together(new[] { "a", "b" }, s => s, null).SequenceEqual(new[] { "a", "b" }));

        var inApt = new VENTARRANGE.Arrangement();
        VENTARRANGE.Merge(inApt, new[] { Of(inApt, Ind, 21), Of(inApt, Ind, 23) });

        Check("rooms of one apartment merge within it, kept together under it",
            Keys(inApt).SequenceEqual(new[] { K(Arc, 1), K(Ind, 21), K(Ind, 23), K(Ind, 22), K(Ind, 31), K(Arc, 5) }));

        VENTARRANGE.Merge(inApt, new[] { Of(inApt, Ind, 22), Of(inApt, Ind, 23) });

        Check("a room merged with one already merged joins its group - one group, not two",
            inApt.Merges.Count == 1 && inApt.Merges[0].Count == 3 &&
            new[] { 21L, 22L, 23L }.All(r => inApt.MergeIdOf(K(Ind, r)) == inApt.Merges[0][0]));

        VENTARRANGE.Unmerge(inApt, new[] { K(Ind, 22) });

        Check("Unmerge one of three: the other two stay merged",
            inApt.Merges.Count == 1 && inApt.MergeOf(K(Ind, 22)) == null &&
            inApt.MergeOf(K(Ind, 21)) != null && inApt.MergeOf(K(Ind, 23)) != null);

        VENTARRANGE.Unmerge(inApt, new[] { K(Ind, 23) });

        Check("...one of two: a group of one is no group",
            inApt.Merges.Count == 0 && inApt.IsEmpty);

        // EACH SIDE ITS OWN MERGES (2026-10-22): an extract merge (the
        // sheet's merged extract cells) keeps its rooms together too, apart
        // from the fresh air merges, and Unmerge parts both
        var sides = new VENTARRANGE.Arrangement();
        VENTARRANGE.Merge(sides, new[] { Of(sides, Ind, 21), Of(sides, Ind, 23) }, extract: true);

        Check("an extract merge is not a fresh air merge, and keeps its rooms together under the apartment",
            sides.Merges.Count == 0 && sides.ExtractMerges.Count == 1 && sides.ExtractMergeOf(K(Ind, 23)) != null &&
            sides.MergeOf(K(Ind, 23)) == null &&
            Keys(sides).SequenceEqual(new[] { K(Arc, 1), K(Ind, 21), K(Ind, 23), K(Ind, 22), K(Ind, 31), K(Arc, 5) }));

        VENTARRANGE.Merge(sides, new[] { Of(sides, Ind, 22), Of(sides, Ind, 23) });

        List<string> both = Keys(sides).ToList();
        int[] at = new[] { 21L, 22L, 23L }.Select(r => both.IndexOf(K(Ind, r))).ToArray();

        Check("a fresh air merge beside it: the rows of both stand together - 21 and 23 on extract, 22 and 23 on fresh air: " +
              string.Join(", ", both),
            at.All(i => i >= 0) && at.Max() - at.Min() == 2);

        VENTARRANGE.Unmerge(sides, new[] { K(Ind, 23) });

        Check("Unmerge parts both sides", sides.Merges.Count == 0 && sides.ExtractMerges.Count == 0 && sides.IsEmpty);

        var outM = new VENTARRANGE.Arrangement();
        VENTARRANGE.Merge(outM, new[] { Of(outM, Ind, 21), Of(outM, Ind, 22) });
        VENTARRANGE.TakeOut(outM, Of(outM, Ind, 22), "GR");

        Check("a merged room ungrouped leaves the merge - its cells no longer sit with the others'",
            outM.MergeOf(K(Ind, 22)) == null && outM.Merges.Count == 0);

        var backM = new VENTARRANGE.Arrangement();
        VENTARRANGE.Merge(backM, new[] { Of(backM, Arc, 1), Of(backM, Arc, 5) });
        VENTARRANGE.Reset(backM, Rows(backM), "GR");

        Check("Reset this level forgets its merges",
            backM.IsEmpty && Keys(backM).SequenceEqual(defaults));

        var cloned = m.Clone();
        cloned.Merges[0].Add("99:1");

        Check("a clone's merges are its own",
            m.Merges[0].Count == 2);

        var hm = new VENTARRANGE.Arrangement();
        hm.Merges.Add(new List<string> { "40:1", "40:2" });

        Check("merged rooms' links are named too, and follow a link to its new id",
            VENTARRANGE.Links(hm).SetEquals(new long[] { 40 }) &&
            VENTARRANGE.Repointed("40:1", new Dictionary<long, long> { [40] = 140 }) == "140:1" &&
            VENTARRANGE.Repointed("apt:40:1", new Dictionary<long, long> { [40] = 140 }) == "apt:140:1" &&
            VENTARRANGE.Repointed("41:1", new Dictionary<long, long> { [40] = 140 }) == "41:1");

        VENTARRANGE.Remap(hm, new Dictionary<long, long> { [40] = 140 });

        Check("...re-pointed with the rest",
            hm.Merges[0].SequenceEqual(new[] { "140:1", "140:2" }));
    }
}
