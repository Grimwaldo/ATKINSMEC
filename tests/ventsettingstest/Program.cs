using ATKINSMEC;
using System;

// Which duct system types AUTOVENT treats as fresh and which as
// extract, and how far an endcap may look for its FCU.
//
// The real VENTSETTINGS is compiled in. The property that matters most
// is not "ticking works" but "NOT ticking still behaves exactly as it
// did" - this decides what gets written onto every air terminal in a
// job, and a change nobody asked for would be found out in a model.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static void Clear()
    {
        VENTSETTINGS.FreshSystemTypeIds.Clear();
        VENTSETTINGS.ExtractSystemTypeIds.Clear();
    }

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("AUTOVENT - fresh, extract and the endcap reach");
        Console.WriteLine();

        // ---- nothing ticked: the old behaviour, unchanged -----------
        {
            Clear();

            Check("a system TYPE named fresh is fresh",
                VENTSETTINGS.IsFresh(11, "OAD 40", "ATK_ME_Fresh Air"));

            Check("a SYSTEM named fresh is fresh too",
                VENTSETTINGS.IsFresh(11, "Fresh Air 3", "ATK_ME_Supply"));

            Check("extract is extract",
                VENTSETTINGS.IsExtract(12, "EX 21", "ATK_ME_Extract"));

            Check("and so is exhaust",
                VENTSETTINGS.IsExtract(12, "EX 21", "ATK_ME_Exhaust Air"));

            Check("case does not matter",
                VENTSETTINGS.IsFresh(11, "", "atk_me_FRESH air"));

            Check("plain supply is neither",
                !VENTSETTINGS.IsFresh(13, "SA 01", "ATK_ME_Supply Air")
                && !VENTSETTINGS.IsExtract(13, "SA 01", "ATK_ME_Supply Air"));

            // THE GAP THAT MADE THIS A SETTING. A perfectly ordinary
            // name for outside air that the keyword search never saw,
            // so every terminal on it got nothing written.
            Check("'OA Supply' is NOT matched by the name rule  -  " +
                  "which is why ticking exists",
                !VENTSETTINGS.IsFresh(14, "OA 2", "ATK_ME_OA Supply"));
        }

        // ---- ticked: the guessing stops ----------------------------
        {
            Clear();

            VENTSETTINGS.FreshSystemTypeIds.Add(14);

            Check("the ticked type is fresh however it is named",
                VENTSETTINGS.IsFresh(14, "OA 2", "ATK_ME_OA Supply"));

            // The one that catches a half-done job: a type that LOOKS
            // fresh but was not ticked must not sneak back in, or
            // ticking would only ever add and never narrow
            Check("a type that LOOKS fresh but was not ticked is NOT",
                !VENTSETTINGS.IsFresh(11, "OAD 40", "ATK_ME_Fresh Air"));

            // EACH SIDE IS DECIDED SEPARATELY. Ticking fresh must not
            // quietly make extract strict as well - somebody setting up
            // one side at a time would watch the other stop working.
            Check("ticking FRESH leaves EXTRACT on the names",
                VENTSETTINGS.IsExtract(12, "EX 21", "ATK_ME_Extract"));

            Clear();

            VENTSETTINGS.ExtractSystemTypeIds.Add(99);

            Check("and the other way round",
                VENTSETTINGS.IsFresh(11, "OAD 40", "ATK_ME_Fresh Air")
                && !VENTSETTINGS.IsExtract(12, "EX 21", "ATK_ME_Extract"));

            Check("the ticked extract type is extract",
                VENTSETTINGS.IsExtract(99, "anything", "anything"));
        }

        // ---- a system whose type cannot be read ---------------------
        {
            Clear();

            VENTSETTINGS.FreshSystemTypeIds.Add(14);

            // Id 0 means Revit gave no type. Dropping it would silently
            // ignore a system the name rule would have caught, and the
            // fallback cannot contradict an explicit tick - a system
            // called "Fresh" is not one of the ticked ids by definition.
            Check("no readable type falls back to the name",
                VENTSETTINGS.IsFresh(0, "Fresh Air 3", ""));

            Check("and still says no when the name says nothing",
                !VENTSETTINGS.IsFresh(0, "SA 01", "ATK_ME_Supply Air"));
        }

        // ---- the pre-tick is the SAME rule as the fallback ----------
        //
        // If these two ever drift, opening the window would silently
        // change what a model does - the lists would arrive ticked for
        // something other than what the add-in was already doing.
        {
            Clear();

            string[] names =
            {
                "ATK_ME_Fresh Air", "ATK_ME_Extract", "ATK_ME_Supply Air",
                "ATK_ME_Exhaust Air", "ATK_ME_OA Supply", ""
            };

            bool same = true;

            foreach (string name in names)
            {
                if (VENTSETTINGS.LooksFresh(name) !=
                    VENTSETTINGS.IsFresh(0, string.Empty, name))
                    same = false;

                if (VENTSETTINGS.LooksExtract(name) !=
                    VENTSETTINGS.IsExtract(0, string.Empty, name))
                    same = false;
            }

            Check("what the window PRE-TICKS is what the add-in would " +
                  "have decided on its own",
                same);
        }

        // ---- nulls -------------------------------------------------
        {
            Clear();

            Check("a null name is not fresh and does not throw",
                !VENTSETTINGS.IsFresh(0, null, null)
                && !VENTSETTINGS.IsExtract(0, null, null));

            Check("and LooksFresh survives one too",
                !VENTSETTINGS.LooksFresh(null)
                && !VENTSETTINGS.LooksExtract(null));
        }

        // ---- the endcap reach --------------------------------------
        {
            VENTSETTINGS.EndcapReachMm =
                VENTSETTINGS.DefaultEndcapReachMm;

            // It replaced a hard-coded 4.92 ft, and the default has to
            // mean the same thing or every endcap in every job moves
            Check("the default is the constant it replaced  (4.92 ft)",
                Math.Abs(VENTSETTINGS.EndcapReachFeet - 4.92) < 0.005);

            VENTSETTINGS.EndcapReachMm = 3048;

            Check("what is typed is what is used",
                Math.Abs(VENTSETTINGS.EndcapReachFeet - 10.0) < 1e-9);

            // A ZERO WOULD WRITE NOTHING ON EVERY ENDCAP IN THE JOB,
            // silently - no FCU is ever 0 mm away
            VENTSETTINGS.EndcapReachMm = 0;

            Check("zero falls back to the default rather than " +
                  "matching nothing",
                Math.Abs(VENTSETTINGS.EndcapReachFeet - 4.92) < 0.005);

            VENTSETTINGS.EndcapReachMm = -500;

            Check("and so does a negative",
                VENTSETTINGS.EndcapReachFeet > 0);

            VENTSETTINGS.EndcapReachMm =
                VENTSETTINGS.DefaultEndcapReachMm;
        }

        // ---- which plumbing fixture families count into No. of Units
        Console.WriteLine();
        Console.WriteLine("  fixtures");

        foreach (string yes in new[]
                 {
                     "M_Water Closet - Flush Valve - Wall Mounted", "Water Closet", "WC - Back to Wall",
                     "ATK_PL_WC", "Toilet Pan", "M_Urinal - Wall Hung", "Urinal - Flush Valve",
                     "Squatting Pan", "Accessible WC"
                 })
        {
            Check("counts: " + yes, VENTSETTINGS.LooksLikeWcOrUrinal(yes));
        }

        foreach (string no in new[]
                 {
                     "M_Lavatory - Vanity", "Lavatory - Wall Mounted", "Wash Basin", "Sink - Kitchen",
                     "Shower Stall", "Bathtub", "Floor Drain", "Bidet", "BWC Cabinet", "", null
                 })
        {
            Check("does not count: " + (no ?? "(nothing)"), !VENTSETTINGS.LooksLikeWcOrUrinal(no));
        }

        Check("nothing ticked: the name rule decides",
            VENTSETTINGS.FixtureFamilies.Count == 0 &&
            VENTSETTINGS.CountsFamily("M_Urinal - Wall Hung") &&
            !VENTSETTINGS.CountsFamily("M_Lavatory - Vanity"));

        VENTSETTINGS.FixtureFamilies.Add("M_Lavatory - Vanity");

        Check("anything ticked: ONLY the ticked families count - a name that looks right does not",
            VENTSETTINGS.CountsFamily("m_lavatory - vanity") &&
            !VENTSETTINGS.CountsFamily("M_Urinal - Wall Hung"));

        VENTSETTINGS.FixtureFamilies.Clear();

        // ---- the Fixtures dialog's list, from what was read ----------
        var read = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.Dictionary<string, int>>
        {
            [0] = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["M_Urinal - Wall Hung"] = 2 },
            [11] = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["M_Water Closet - Flush Valve - Wall Mounted"] = 86,
                ["M_Lavatory - Vanity"] = 120,
                ["M_Urinal - Wall Hung"] = 34
            }
        };

        var families = VENTSETTINGS.FamiliesFrom(read, new long[] { 0, 11, 11, 99 });

        Check("the dialog's families add up across the models ticked, by name",
            families.Count == 3 &&
            families.Find(f => f.Name == "M_Urinal - Wall Hung").Count == 36 &&
            families.Find(f => f.Name == "M_Lavatory - Vanity").Count == 120);

        Check("...a model named twice is counted once, and one never read gives nothing",
            families.Find(f => f.Name == "M_Water Closet - Flush Valve - Wall Mounted").Count == 86);

        Check("...sorted by name and pre-ticked by the rule an export counts by",
            families[0].Name == "M_Lavatory - Vanity" && !families[0].Counted &&
            families.Find(f => f.Name == "M_Urinal - Wall Hung").Counted);

        Check("...and nothing read, or nothing ticked, is an empty list",
            VENTSETTINGS.FamiliesFrom(null, new long[] { 0 }).Count == 0 &&
            VENTSETTINGS.FamiliesFrom(read, null).Count == 0);

        // ---- the search box over the linked models ---------------------
        const string towerB = "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 2 : Tower B";

        Check("searching: nothing typed shows every model",
            VENTLINKFILTER.Matches(towerB, "") && VENTLINKFILTER.Matches(towerB, "   ") &&
            VENTLINKFILTER.Matches(towerB, null));

        Check("...part of the name finds it, in any case",
            VENTLINKFILTER.Matches(towerB, "arc-000020") && VENTLINKFILTER.Matches(towerB, "TOWER"));

        Check("...every word must appear, in any order - so the second placement can be picked out",
            VENTLINKFILTER.Matches(towerB, "tower b") && VENTLINKFILTER.Matches(towerB, "b arc 20") &&
            !VENTLINKFILTER.Matches("5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 1 : Tower A", "tower b"));

        Check("...a word the name lacks hides it",
            !VENTLINKFILTER.Matches(towerB, "plumbing") && !VENTLINKFILTER.Matches(null, "arc"));

        Check("...the card says how many show, and that hidden ticked models still count",
            VENTLINKFILTER.Said(2, 6, 0) == "Showing 2 of 6 linked model(s)." &&
            VENTLINKFILTER.Said(2, 6, 1).Contains("1 ticked model(s) hidden by the search still count") &&
            VENTLINKFILTER.Said(0, 6, 0) == "No linked model matches the search.");

        Interior();

        Saved();

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    // ==================================================================
    // VENTINTERIOR - which placements are interior (IND) models, found
    // by a keyword and ticked level by level of THIS model.
    // ==================================================================

    private static void Interior()
    {
        Console.WriteLine();
        Console.WriteLine("VENTINTERIOR - interior models by keyword, level by level");

        const double M = 1 / 0.3048;   // feet in a metre

        // This model's levels: GR at 0, L01 at 4 m, L02 at 8 m
        var storeys = new System.Collections.Generic.List<VENTLEVELMAP.Storey>
        {
            new VENTLEVELMAP.Storey { Name = "GR_GROUND FLOOR", Elevation = 0 },
            new VENTLEVELMAP.Storey { Name = "L01_LEVEL 01", Elevation = 4 * M },
            new VENTLEVELMAP.Storey { Name = "L02_LEVEL 02", Elevation = 8 * M }
        };

        var links = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, string>>();

        void Link(long id, string label)
        {
            links.Add(new System.Collections.Generic.KeyValuePair<long, string>(id, label));
        }

        Link(11, "ATR-TWR-ARC-000010.rvt");            // the apartments model
        Link(22, "ATR-TWR-IND-000030.rvt : 2");        // placed on L01 (and one room on L02)
        Link(21, "ATR-TWR-IND-000030.rvt : 1");        // placed on GR
        Link(23, "ATR-TWR-MEP-000040.rvt");            // not an interior model
        Link(24, "ATR-TWR-IND-000050.rvt");            // never read: no rooms known
        Link(25, "ATR-TWR-IND-000060.rvt");            // below every level

        var floors = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<double>>
        {
            [11] = new System.Collections.Generic.List<double> { 0, 4 * M, 8 * M },
            [21] = new System.Collections.Generic.List<double> { 0, 0.1 },
            // 274 mm below L01 is still L01 (the 300 mm allowance)
            [22] = new System.Collections.Generic.List<double> { 4 * M, 4 * M, 4 * M - 0.9, 8 * M + 0.2 },
            [23] = new System.Collections.Generic.List<double> { 4 * M },
            [25] = new System.Collections.Generic.List<double> { -6 * M }
        };

        var exclude = new[] { 11L };

        var found = VENTINTERIOR.Find(links, floors, storeys, "IND", exclude);

        Check("the keyword finds each IND placement on each level it has rooms on - three",
            found.Count == 3);

        Check("...lowest level first: GR has placement 1 with its 2 rooms",
            found.Count == 3 && found[0].StoreyName == "GR_GROUND FLOOR" && found[0].LinkId == 21 && found[0].Rooms == 2);

        Check("...L01 has placement 2 with 3 rooms - one 274 mm low still counts there",
            found.Count == 3 && found[1].StoreyName == "L01_LEVEL 01" && found[1].LinkId == 22 && found[1].Rooms == 3);

        Check("...and placement 2's one room on L02 offers it on L02 as well",
            found.Count == 3 && found[2].StoreyName == "L02_LEVEL 02" && found[2].LinkId == 22 && found[2].Rooms == 1);

        Check("a placement never read, or with rooms only below every level, is not offered",
            !found.Exists(f => f.LinkId == 24 || f.LinkId == 25));

        Check("a model whose name lacks the keyword is not offered",
            !found.Exists(f => f.LinkId == 23));

        var every = VENTINTERIOR.Find(links, floors, storeys, "", exclude);

        Check("an EMPTY keyword offers every placement with rooms - the old behaviour",
            every.Exists(f => f.LinkId == 23) && every.Count == 4);

        Check("...but never the apartments model itself",
            !every.Exists(f => f.LinkId == 11));

        Check("the keyword is matched in any case, and by every word",
            VENTINTERIOR.Find(links, floors, storeys, "ind 000030", exclude).Count == 3 &&
            VENTINTERIOR.Find(links, floors, storeys, "ind : 1", exclude).Count == 1);

        var shuffled = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, string>>(links);
        shuffled.Reverse();

        var again = VENTINTERIOR.Find(shuffled, floors, storeys, "IND", exclude);

        Check("the order the links come in changes nothing",
            again.Count == 3 && again[0].LinkId == 21 && again[1].LinkId == 22 && again[2].LinkId == 22);

        Check("nothing read is nothing found, not a crash",
            VENTINTERIOR.Find(null, floors, storeys, "IND", exclude).Count == 0 &&
            VENTINTERIOR.Find(links, null, storeys, "IND", null).Count == 0 &&
            VENTINTERIOR.Find(links, floors, null, "IND", null).Count == 0);

        // ---- what the run searches: Allowed ---------------------------
        Check("following the keyword: a matching placement is searched on every level",
            VENTINTERIOR.Allowed("GR_GROUND FLOOR", 22, "ATR-TWR-IND-000030.rvt : 2", "IND", false, null) &&
            VENTINTERIOR.Allowed("L02_LEVEL 02", 22, "ATR-TWR-IND-000030.rvt : 2", "IND", false, null));

        Check("...and one that does not match is not",
            !VENTINTERIOR.Allowed("L01_LEVEL 01", 23, "ATR-TWR-MEP-000040.rvt", "IND", false, null));

        Check("...an empty keyword searches everything, as before",
            VENTINTERIOR.Allowed("L01_LEVEL 01", 23, "ATR-TWR-MEP-000040.rvt", "", false, null));

        var picks = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<long>>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["L01_LEVEL 01"] = new System.Collections.Generic.HashSet<long> { 22, 23 },
            ["GR_GROUND FLOOR"] = new System.Collections.Generic.HashSet<long>()
        };

        Check("chosen: a placement ticked on L01 is searched on L01",
            VENTINTERIOR.Allowed("L01_LEVEL 01", 22, "ATR-TWR-IND-000030.rvt : 2", "IND", true, picks));

        Check("...but NOT on L02, where it was not ticked - though its name matches",
            !VENTINTERIOR.Allowed("L02_LEVEL 02", 22, "ATR-TWR-IND-000030.rvt : 2", "IND", true, picks));

        Check("...a placement ticked by hand counts though the keyword would not find it",
            VENTINTERIOR.Allowed("L01_LEVEL 01", 23, "ATR-TWR-MEP-000040.rvt", "IND", true, picks));

        Check("...a level where NONE was ticked searches none",
            !VENTINTERIOR.Allowed("GR_GROUND FLOOR", 21, "ATR-TWR-IND-000030.rvt : 1", "IND", true, picks));

        Check("...and chosen with no picks at all searches nothing - no crash",
            !VENTINTERIOR.Allowed("L01_LEVEL 01", 22, "IND", "IND", true, null));

        // ---- what OK keeps: Choice -------------------------------------
        System.Collections.Generic.KeyValuePair<string, long> T(string storey, long id)
        {
            return new System.Collections.Generic.KeyValuePair<string, long>(storey, id);
        }

        Check("everything found still ticked is NOT a choice - it follows the keyword (null)",
            VENTINTERIOR.Choice(found, new[] { T("L02_LEVEL 02", 22), T("gr_ground floor", 21), T("L01_LEVEL 01", 22) }) == null);

        var choice = VENTINTERIOR.Choice(found, new[] { T("GR_GROUND FLOOR", 21), T("L01_LEVEL 01", 22) });

        Check("one unticked makes it a choice of exactly the rest",
            choice != null && choice.Count == 2 &&
            choice["GR_GROUND FLOOR"].Contains(21) && choice["L01_LEVEL 01"].Contains(22) &&
            !choice.ContainsKey("L02_LEVEL 02"));

        Check("...so the placement unticked on L02 is no longer searched there",
            choice != null &&
            !VENTINTERIOR.Allowed("L02_LEVEL 02", 22, "ATR-TWR-IND-000030.rvt : 2", "IND", true, choice) &&
            VENTINTERIOR.Allowed("L01_LEVEL 01", 22, "ATR-TWR-IND-000030.rvt : 2", "IND", true, choice));

        choice = VENTINTERIOR.Choice(found, new System.Collections.Generic.KeyValuePair<string, long>[0]);

        Check("nothing ticked is a choice too - of NONE, not 'follow'",
            choice != null && choice.Count == 0);

        // ---- the apartments map is built again when any of it changes ---
        string follow = VENTINTERIOR.Key(new long[] { 12, 11 }, "IND", false, null);

        Check("the map's key ignores the order of the apartment links and the keyword's case",
            follow == VENTINTERIOR.Key(new long[] { 11, 12 }, " ind ", false, picks));

        Check("...but a new keyword is a new key",
            follow != VENTINTERIOR.Key(new long[] { 11, 12 }, "IND TWR", false, null));

        Check("...and so is a choice, and a different choice",
            follow != VENTINTERIOR.Key(new long[] { 11, 12 }, "IND", true, picks) &&
            VENTINTERIOR.Key(new long[] { 11, 12 }, "IND", true, picks) !=
            VENTINTERIOR.Key(new long[] { 11, 12 }, "IND", true, VENTINTERIOR.Choice(found, new[] { T("GR_GROUND FLOOR", 21) })));

        Check("...while the same picks in another order are the same key",
            VENTINTERIOR.Key(new long[] { 11 }, "IND", true, picks) ==
            VENTINTERIOR.Key(new long[] { 11 }, "IND", true,
                new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<long>>
                {
                    ["gr_ground floor"] = new System.Collections.Generic.HashSet<long>(),
                    ["L01_LEVEL 01"] = new System.Collections.Generic.HashSet<long> { 23, 22 }
                }));

        Check("the default keyword is IND",
            VENTSETTINGS.DefaultInteriorKeyword == "IND" && !VENTSETTINGS.InteriorChosen);
    }

    // ==================================================================
    // VENTSAVED - each model's set-up, remembered between sessions.
    // Everything goes through a file in TEMP, never the real one in
    // AppData, so running this cannot touch anybody's set-up.
    // ==================================================================

    private static VENTSAVED.LinkRef L(long id, string label)
    {
        return new VENTSAVED.LinkRef { Id = id, Label = label };
    }

    private static VENTSAVED.Model plainModel()
    {
        return new VENTSAVED.Model { Key = "file:c:\\jobs\\plain.rvt" };
    }

    private static void Saved()
    {
        Console.WriteLine();
        Console.WriteLine("VENTSAVED - each model's set-up");

        string file = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ventsaved-" + Guid.NewGuid().ToString("N") + ".txt");

        try
        {
            // ---- a round trip, everything set ---------------------------
            var tower = new VENTSAVED.Model
            {
                Key = "central:\\\\server\\jobs\\5232535-hvc-000050.rvt",
                Title = "5232535-ATR-POD-ZZZ-3DM-HVC-000050",
                When = "2026-10-06 18:00",
                Fresh = new System.Collections.Generic.List<string> { "ATK_ME_OA Supply", "ATK_ME_Fresh Air" },
                Extract = null,
                ReachMm = 2250,
                CountFixtures = false,
                RoomLinks = { L(1001, "ARC-000010.rvt"), L(1002, "ARC-000020.rvt : 1 : Tower A") },
                Apartments = { L(1002, "ARC-000020.rvt : 1 : Tower A") },
                FixtureSources = { L(0, "This model"), L(1003, "PLU-000030.rvt") },
                FixtureFamilies = { "ATK_PL_Accessible WC_Pack", "M_Urinal - Wall Hung" }
            };

            var podium = new VENTSAVED.Model
            {
                Key = "file:c:\\jobs\\podium.rvt",
                Title = "Podium",
                Fresh = new System.Collections.Generic.List<string>(),   // chose NONE
                Extract = new System.Collections.Generic.List<string> { "ATK_ME_Toilet Exhaust" }
            };

            var back = VENTSAVED.Parse(VENTSAVED.Compose(new[] { tower, podium }));

            Check("two models written, two read back", back.Count == 2);

            VENTSAVED.Model t = back[0];

            Check("the key and title survive",
                t.Key == tower.Key && t.Title == tower.Title && t.When == tower.When);

            Check("the ticked fresh types survive, by name",
                t.Fresh != null && t.Fresh.Count == 2 && t.Fresh.Contains("ATK_ME_OA Supply"));

            Check("an extract side that followed the names stays NULL - not an empty choice",
                t.Extract == null);

            Check("the reach and the fixture switch survive",
                t.ReachMm == 2250 && !t.CountFixtures);

            Check("the room links, apartments and fixture sources survive with their labels",
                t.RoomLinks.Count == 2 && t.RoomLinks[1].Id == 1002 &&
                t.RoomLinks[1].Label == "ARC-000020.rvt : 1 : Tower A" &&
                t.Apartments.Count == 1 && t.Apartments[0].Id == 1002 &&
                t.FixtureSources.Count == 2 && t.FixtureSources[0].Id == 0);

            Check("the fixture families survive",
                t.FixtureFamilies.Count == 2 && t.FixtureFamilies.Contains("M_Urinal - Wall Hung"));

            VENTSAVED.Model p = back[1];

            Check("CHOSE NONE is kept apart from FOLLOW THE NAMES",
                p.Fresh != null && p.Fresh.Count == 0);

            Check("the second model's lines do not leak onto the first, or the first's onto it",
                p.RoomLinks.Count == 0 && p.FixtureFamilies.Count == 0 &&
                p.Extract.Count == 1 && t.Extract == null && p.ReachMm == VENTSETTINGS.DefaultEndcapReachMm);

            Check("a model never given a fixture switch counts fixtures, as the default does",
                p.CountFixtures);

            // ---- the interior models: keyword and picks per level ---------
            var picked = new VENTSAVED.Model
            {
                Key = "file:c:\\jobs\\tower.rvt",
                InteriorKeyword = "IND TWR",
                InteriorPicks = new System.Collections.Generic.List<VENTSAVED.InteriorPick>
                {
                    new VENTSAVED.InteriorPick { Storey = "GR_GROUND FLOOR", Link = L(2002, "IND-000030.rvt : 1") },
                    new VENTSAVED.InteriorPick { Storey = "L01_LEVEL 01", Link = L(2003, "IND-000030.rvt : 2") },
                    new VENTSAVED.InteriorPick { Storey = "L01_LEVEL 01", Link = L(2004, "IND-000040.rvt") }
                }
            };

            var none = new VENTSAVED.Model
            {
                Key = "file:c:\\jobs\\none.rvt",
                InteriorKeyword = "",                                                   // every placement
                InteriorPicks = new System.Collections.Generic.List<VENTSAVED.InteriorPick>()   // chose NONE
            };

            var plain = new VENTSAVED.Model { Key = "file:c:\\jobs\\plain.rvt" };

            var trip = VENTSAVED.Parse(VENTSAVED.Compose(new[] { picked, none, plain }));

            Check("the interior keyword and the picks survive, each on its own level with its label",
                trip.Count == 3 && trip[0].InteriorKeyword == "IND TWR" &&
                trip[0].InteriorPicks != null && trip[0].InteriorPicks.Count == 3 &&
                trip[0].InteriorPicks[0].Storey == "GR_GROUND FLOOR" && trip[0].InteriorPicks[0].Link.Id == 2002 &&
                trip[0].InteriorPicks[2].Storey == "L01_LEVEL 01" && trip[0].InteriorPicks[2].Link.Label == "IND-000040.rvt");

            Check("an EMPTY keyword (every placement) is kept apart from no keyword (the default)",
                trip.Count == 3 && trip[1].InteriorKeyword == "" && trip[2].InteriorKeyword == null);

            Check("CHOSE NONE is kept apart from FOLLOW THE KEYWORD",
                trip.Count == 3 && trip[1].InteriorPicks != null && trip[1].InteriorPicks.Count == 0 &&
                trip[2].InteriorPicks == null);

            Check("...and a model's picks do not leak onto the next",
                trip.Count == 3 && trip[1].InteriorPicks.Count == 0 && trip[2].InteriorPicks == null);

            Check("an interior line with no level or a nonsense id is passed over",
                VENTSAVED.Parse("model\tk\ninteriorchosen\ninterior\t\t5\tx\ninterior\tL01\tfar\tx\ninterior\tL01\t7\ty\n")[0]
                    .InteriorPicks is var bad &&
                bad != null && bad.Count == 1 && bad[0].Link.Id == 7);

            Check("...and bad lines alone do not turn 'follow the keyword' into a choice",
                VENTSAVED.Parse("model\tk\ninterior\t\t5\tx\n")[0].InteriorPicks == null);

            // ---- the rooms arranged by hand ---------------------------------
            var arranged = new VENTSAVED.Model
            {
                Key = "file:c:\\jobs\\arranged.rvt",
                Ungrouped = { "3589272:121055", "3589274:121106" },
                Orders =
                {
                    new System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.List<string>>(
                        "level:LEVEL 03", new System.Collections.Generic.List<string> { "3001198:7", "3589272:121055", "3001202:41" }),
                    new System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.List<string>>(
                        "apt:3001198:7", new System.Collections.Generic.List<string> { "3589274:121107", "3589274:121106" })
                },
                ArrangeLinks = { L(3001198, "5232535-ATR-TRA-ZZZ-3DM-ARC-000100"), L(3589272, "IND-001120 : TOWER A - TIER 259") }
            };

            var arrangedBack = VENTSAVED.Parse(VENTSAVED.Compose(new[] { arranged, plainModel() }));

            Check("the rooms taken out of their apartments survive",
                arrangedBack[0].Ungrouped.Count == 2 && arrangedBack[0].Ungrouped.Contains("3589274:121106"));

            Check("each order survives in sequence, a level's name with its space, an apartment's key with its colons",
                arrangedBack[0].Orders.Count == 2 &&
                arrangedBack[0].Orders[0].Key == "level:LEVEL 03" &&
                string.Join(",", arrangedBack[0].Orders[0].Value) == "3001198:7,3589272:121055,3001202:41" &&
                arrangedBack[0].Orders[1].Key == "apt:3001198:7" &&
                string.Join(",", arrangedBack[0].Orders[1].Value) == "3589274:121107,3589274:121106");

            Check("the links the keys name survive with their labels",
                arrangedBack[0].ArrangeLinks.Count == 2 && arrangedBack[0].ArrangeLinks[1].Label == "IND-001120 : TOWER A - TIER 259");

            Check("...and none of it leaks onto the next model",
                arrangedBack[1].Ungrouped.Count == 0 && arrangedBack[1].Orders.Count == 0 && arrangedBack[1].ArrangeLinks.Count == 0);

            Check("an order line with no keys, or no container, is passed over",
                VENTSAVED.Parse("model\tk\norder\tlevel:GR\norder\t\t1:2\n")[0].Orders.Count == 0);

            var renamedModel = new VENTSAVED.Model
            {
                Key = "file:c:\\jobs\\renamed.rvt",
                Names =
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("3001198:7", "2BDR-A1 (FLAT 3)"),
                    new System.Collections.Generic.KeyValuePair<string, string>("3589274:121106", "MASTER\tBEDROOM")
                }
            };

            var renamedBack = VENTSAVED.Parse(VENTSAVED.Compose(new[] { renamedModel, plainModel() }));

            Check("room names given for the sheet survive - a tab in one cannot break the file",
                renamedBack[0].Names.Count == 2 && renamedBack[0].Names[0].Value == "2BDR-A1 (FLAT 3)" &&
                renamedBack[0].Names[1].Value == "MASTER BEDROOM" && renamedBack[1].Names.Count == 0);

            Check("a rename line with no key or no name is passed over",
                VENTSAVED.Parse("model\tk\nrename\t1:2\nrename\t\tX\n")[0].Names.Count == 0);

            // ---- what the file must survive -------------------------------
            var awkward = new VENTSAVED.Model
            {
                Key = "file:c:\\x.rvt",
                Title = "Tab\there and\r\na line break"
            };

            var read = VENTSAVED.Parse(VENTSAVED.Compose(new[] { awkward, tower }));

            Check("a tab or line break in a title cannot break the file",
                read.Count == 2 && read[0].Title == "Tab here and  a line break" &&
                read[1].RoomLinks.Count == 2);

            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;

            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("de-DE");

                var odd = new VENTSAVED.Model { Key = "k", ReachMm = 1500.5 };
                string text = VENTSAVED.Compose(new[] { odd });

                Check("a de-DE machine writes the reach as 1500.5, not 1500,5",
                    text.Contains("reach\t1500.5") && VENTSAVED.Parse(text)[0].ReachMm == 1500.5);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }

            Check("a zero, negative or nonsense reach reads as the default",
                VENTSAVED.Parse("model\tk\nreach\t0\n")[0].ReachMm == VENTSETTINGS.DefaultEndcapReachMm &&
                VENTSAVED.Parse("model\tk\nreach\t-5\n")[0].ReachMm == VENTSETTINGS.DefaultEndcapReachMm &&
                VENTSAVED.Parse("model\tk\nreach\tfar\n")[0].ReachMm == VENTSETTINGS.DefaultEndcapReachMm);

            Check("lines before any model, and lines a later version added, are passed over",
                VENTSAVED.Parse("fresh\tstray\nmodel\tk\nwhatever\t1\nfresh\tA\n") is var later &&
                later.Count == 1 && later[0].Fresh.Count == 1 && later[0].Fresh[0] == "A");

            Check("an empty or missing text reads as no models, not an error",
                VENTSAVED.Parse(null).Count == 0 && VENTSAVED.Parse("").Count == 0 &&
                VENTSAVED.Read(file).Count == 0);

            // ---- remembering one model leaves the others alone ------------
            Check("a model is remembered", VENTSAVED.Remember(tower, file));
            Check("and another beside it", VENTSAVED.Remember(podium, file));

            var changed = VENTSAVED.Recall(tower.Key, file);
            changed.ReachMm = 900;
            VENTSAVED.Remember(changed, file);

            var all = VENTSAVED.Read(file);

            Check("remembering a model again REPLACES its block - one each, not two",
                all.Count == 2 && VENTSAVED.Recall(tower.Key, file).ReachMm == 900);

            Check("and the other model is exactly as it was",
                VENTSAVED.Recall(podium.Key, file) is var pod &&
                pod.Fresh.Count == 0 && pod.Extract.Count == 1);

            Check("the key is matched whatever its case",
                VENTSAVED.Recall(tower.Key.ToUpperInvariant(), file) != null);

            Check("a model never set up recalls NOTHING - so the window uses the defaults",
                VENTSAVED.Recall("file:c:\\never.rvt", file) == null &&
                VENTSAVED.Recall(null, file) == null &&
                VENTSAVED.Recall("k", file + ".missing") == null);

            Check("a model with no key is never written",
                !VENTSAVED.Remember(new VENTSAVED.Model(), file) && VENTSAVED.Read(file).Count == 2);

            Check("no temporary file is left behind",
                !System.IO.File.Exists(file + ".tmp"));

            // ---- which loaded link a remembered one means ------------------
            var loaded = new[]
            {
                L(2001, "ARC-000020.rvt : 1 : Tower A"),
                L(1001, "ARC-000010.rvt"),
                L(2002, "IND-000030.rvt : 1"),
                L(2003, "IND-000030.rvt : 1")    // two placements, one label
            };

            var found = VENTSAVED.Resolve(new[] { L(1001, "renamed since"), }, loaded);

            Check("a link still loaded is found by its id, whatever its label says now",
                found.Count == 1 && found.Contains(1001));

            found = VENTSAVED.Resolve(new[] { L(1002, "ARC-000020.rvt : 1 : Tower A") }, loaded);

            Check("a link removed and linked again is found by its label, under its new id",
                found.Count == 1 && found.Contains(2001));

            found = VENTSAVED.Resolve(new[] { L(1500, "IND-000030.rvt : 1") }, loaded);

            Check("a label two placements share says nothing - neither is ticked",
                found.Count == 0);

            found = VENTSAVED.Resolve(new[] { L(1001, "x"), L(1777, "ARC-000010.rvt") }, loaded);

            Check("a label is not used to take a link another entry already has by id",
                found.Count == 1 && found.Contains(1001));

            found = VENTSAVED.Resolve(new[] { L(9999, "gone.rvt") }, loaded);

            Check("a link no longer loaded at all ticks nothing",
                found.Count == 0);

            Check("nothing in, nothing out - and no crash on nulls",
                VENTSAVED.Resolve(null, loaded).Count == 0 &&
                VENTSAVED.Resolve(new VENTSAVED.LinkRef[] { null }, null).Count == 0);

            found = VENTSAVED.Resolve(new[] { L(0, "This model") },
                new[] { L(0, "This model"), L(1001, "ARC-000010.rvt") });

            Check("this model (0) is found as a fixture source like any link",
                found.Count == 1 && found.Contains(0));

            // ---- a link not loaded today is not forgotten ------------------
            var nowTicked = new System.Collections.Generic.List<VENTSAVED.LinkRef> { L(1001, "ARC-000010.rvt") };

            VENTSAVED.KeepUnloaded(nowTicked,
                new[] { L(1001, "ARC-000010.rvt"), L(5005, "MEP-UNLOADED.rvt"), L(1002, "ARC-000020.rvt : 1 : Tower A") },
                loaded);

            Check("a remembered link that is not loaded right now is kept for when it comes back",
                nowTicked.Count == 2 && nowTicked.Exists(n => n.Id == 5005));

            Check("but one that IS loaded and was unticked stays unticked",
                !nowTicked.Exists(n => n.Id == 1002) && !nowTicked.Exists(n => n.Id == 2001));

            // ---- ticks that only repeat the name guess are not a choice ----
            System.Collections.Generic.KeyValuePair<string, bool> T(string name, bool on)
            {
                return new System.Collections.Generic.KeyValuePair<string, bool>(name, on);
            }

            Check("ticks exactly as the names have them are remembered as FOLLOW THE NAMES",
                VENTSAVED.ChoiceOf(new[] { T("ATK_ME_Fresh Air", true), T("ATK_ME_Supply Air", false) },
                                   VENTSETTINGS.LooksFresh) == null);

            var chose = VENTSAVED.ChoiceOf(
                new[] { T("ATK_ME_Fresh Air", true), T("ATK_ME_OA Supply", true), T("ATK_ME_Supply Air", false) },
                VENTSETTINGS.LooksFresh);

            Check("a type ticked by hand makes it a choice, by name",
                chose != null && chose.Count == 2 && chose.Contains("ATK_ME_OA Supply"));

            chose = VENTSAVED.ChoiceOf(new[] { T("ATK_ME_Fresh Air", false) }, VENTSETTINGS.LooksFresh);

            Check("unticking the guess is a choice too - of nothing",
                chose != null && chose.Count == 0);

            Check("a model with no system types follows the names",
                VENTSAVED.ChoiceOf(null, VENTSETTINGS.LooksFresh) == null);
        }
        finally
        {
            try { System.IO.File.Delete(file); } catch { }
        }
    }
}
