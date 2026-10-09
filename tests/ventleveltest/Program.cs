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

            Check("each placement says which apartments it was found in, on which floors",
                said.Count == 2
                && said.Contains("IND : 1  ->  ARC : 1 : Tower A apartments on L04")
                && said.Contains("IND : 2  ->  ARC : 2 : Tower B apartments on L05 to L07"));

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

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
