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

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
