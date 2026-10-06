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

        Saved();

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
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
