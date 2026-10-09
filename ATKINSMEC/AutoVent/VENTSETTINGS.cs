using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // What AUTOVENT was guessing, now asked for.
    //
    // Two things, both at the user's request, and both previously
    // constants or keyword searches buried in REVITROOMS:
    //
    //   1. WHICH DUCT SYSTEM TYPES ARE FRESH AIR AND WHICH ARE EXTRACT.
    //   2. HOW FAR AN ENDCAP MAY BE FROM THE FCU IT FEEDS.
    //
    // Deliberately REVIT-FREE, so the decision is testable with no model
    // open - harness at scratchpad/ventsettingstest.
    public class VENTSETTINGS
    {
        // ==================================================
        // Fresh and extract
        // ==================================================

        // EMPTY MEANS FALL BACK TO THE NAMES, which is exactly what
        // AUTOVENT did before these lists existed. A model nobody has
        // set up behaves as it always did, and ticking only ever makes
        // the answer more certain.
        public static HashSet<long> FreshSystemTypeIds =
            new HashSet<long>();

        public static HashSet<long> ExtractSystemTypeIds =
            new HashSet<long>();

        // What the window offers
        public class SystemTypeItem
        {
            public long Id { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        // MATCHING BY NAME IS A GUESS, AND IT IS THE GUESS THAT KEEPS
        // FAILING IN THIS SUITE.
        //
        // `ATK_ME_Fresh Air` reads fine and `ATK_ME_OA Supply` does not,
        // and the second one is just as common - so every terminal on it
        // was silently getting nothing written. That is the same shape
        // as the CHW sizing page pre-ticking "return" by name and
        // missing a return system that was not called one.
        //
        // The names are still the FALLBACK, unchanged, because taking
        // them away would change what every existing model does. Tick a
        // system type and the guessing stops for that side.
        public static bool IsFresh(
            long systemTypeId, string systemName, string systemTypeName)
        {
            if (FreshSystemTypeIds.Count > 0)
            {
                // A SYSTEM WHOSE TYPE CANNOT BE READ falls back rather
                // than being dropped. Ticking says which types count; it
                // does not say "ignore anything Revit will not name",
                // and the fallback can only match a system called fresh,
                // which is not one of the ticked types by definition.
                if (systemTypeId != 0)
                    return FreshSystemTypeIds.Contains(systemTypeId);
            }

            return Says(systemName, "Fresh")
                || Says(systemTypeName, "Fresh");
        }

        public static bool IsExtract(
            long systemTypeId, string systemName, string systemTypeName)
        {
            if (ExtractSystemTypeIds.Count > 0)
            {
                if (systemTypeId != 0)
                    return ExtractSystemTypeIds.Contains(systemTypeId);
            }

            return Says(systemName, "Extract")
                || Says(systemTypeName, "Extract")
                || Says(systemName, "Exhaust")
                || Says(systemTypeName, "Exhaust");
        }

        // THE TWO SIDES ARE DECIDED SEPARATELY, on purpose. Ticking the
        // fresh types must not silently turn the extract side strict as
        // well - somebody setting up one side at a time would otherwise
        // watch the other stop working for no visible reason.

        // The pre-tick, which is the SAME rule as the fallback: out of
        // the box the lists say exactly what AUTOVENT would have done
        // anyway, so nothing changes until somebody changes it
        public static bool LooksFresh(string name)
        {
            return Says(name, "Fresh");
        }

        public static bool LooksExtract(string name)
        {
            return Says(name, "Extract") || Says(name, "Exhaust");
        }

        // ==================================================
        // The endcap trace
        // ==================================================

        // An endcap does not serve the space it stands in - it feeds an
        // FCU that serves somewhere else, so AUTOVENT walks from the
        // terminal to the nearest FCU connector and takes the rooms that
        // FCU serves.
        //
        // HOW FAR THAT WALK MAY REACH WAS A CONSTANT: 4.92 ft, about
        // 1.5 m, chosen to cover an endcap aligned to a plenum face. It
        // is a property of how somebody draws, not of the add-in, so it
        // is asked for now.
        //
        // Too small and an endcap finds no FCU and gets nothing written.
        // Too large and it finds the WRONG FCU - a unit in the next
        // ceiling void - and writes another room's air onto it, which
        // nothing downstream would catch. That is why it is a number on
        // the window rather than something generous baked in.
        public const double DefaultEndcapReachMm = 1500;

        public static double EndcapReachMm = DefaultEndcapReachMm;

        // Revit stores length in feet
        public static double EndcapReachFeet
        {
            get
            {
                double mm = EndcapReachMm > 0
                    ? EndcapReachMm
                    : DefaultEndcapReachMm;

                return mm / 304.8;
            }
        }

        // ==================================================
        // Fixtures into "No. of Units"
        // ==================================================
        //
        // At the user's request - "fixtures FROM LINK, no occupants".
        // The office sheet's Table 6-4 exhaust multiplies a per-fixture
        // rate (a water closet, a urinal) by its "No. of Units" column,
        // which somebody used to count off the drawings. The export now
        // counts the fixtures standing in each room and writes that.
        // Occupants are deliberately NOT counted - "No. of Occupants" is
        // left as the office sheet has it.
        //
        // THE SAME SHAPE AS THE SYSTEM TYPES ABOVE: empty means the
        // fallback, and the fallback is what the window pre-ticks, so
        // opening the dialog never changes what an export does.

        public static bool CountFixtures = true;

        // ==================================================
        // Apartments
        // ==================================================
        //
        // THE LINKS WHOSE ROOMS ARE APARTMENTS - at the user's request:
        // "in some projects we have apartments in the ARCH model and the
        // rooms inside the apartments are in the IND model", and then
        // "apartments can be extracted from multiple linked arch models".
        // Link INSTANCES, so one architect's model placed twice (two
        // towers) is two entries.
        //
        // The rooms INSIDE them are found, not chosen: every loaded link
        // is searched for rooms standing in these links' apartments, ON
        // THE SAME LEVEL OF THIS MODEL - an interior model is usually
        // placed once per floor, each floor its own instance - and those
        // rooms are written under their apartment in the office's
        // Tower-1 pattern (VENTAPARTMENTS, VENTROWS.Apartments). An
        // apartment link's rooms with nothing inside stay ordinary rooms.
        // Empty, the default, is exactly what AUTOVENT always did.
        public static HashSet<long> ApartmentLinkIds = new HashSet<long>();

        // WHICH PLACEMENTS ARE INTERIOR MODELS (VENTINTERIOR): named by a
        // keyword, picked level by level in the Interior models dialog.
        // Not chosen = every placement matching the keyword, on every level
        // it has rooms - which an empty keyword makes every placement.
        public const string DefaultInteriorKeyword = "IND";

        public static string InteriorKeyword = DefaultInteriorKeyword;

        public static bool InteriorChosen;

        // Storey name (this model's level) -> the placements ticked there
        public static Dictionary<string, HashSet<long>> InteriorPicks =
            new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);

        // Which models the fixtures are read from; 0 is this model.
        // EMPTY MEANS THE LINKS THE ROOMS COME FROM - an architectural
        // model usually draws its WCs, and a plumbing model linked as
        // well would count every one twice, so choosing it is a person's
        // decision, not a default.
        public static HashSet<long> FixtureSources = new HashSet<long>();

        // Which fixture FAMILIES count, by family name - ids differ
        // between models. EMPTY MEANS THE NAME RULE BELOW.
        public static HashSet<string> FixtureFamilies =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // What the dialog offers: a family and how many there are
        public class FixtureFamily
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public bool Counted { get; set; }
        }

        // THE FIXTURES DIALOG'S LIST for the sources ticked in it, from
        // each model's families as read (VENTREAD, source -> family ->
        // count; 0 is this model) - so the dialog never touches the model
        // itself. The same list VENTFIXTURES.Families gives for those
        // sources read together: counts added across them, by name,
        // pre-ticked by the rule an export would count by.
        public static List<FixtureFamily> FamiliesFrom(
            IDictionary<long, Dictionary<string, int>> fixtures, IEnumerable<long> sources)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (long source in new HashSet<long>(sources ?? new long[0]))
            {
                Dictionary<string, int> of;

                if (fixtures == null || !fixtures.TryGetValue(source, out of) || of == null)
                    continue;

                foreach (KeyValuePair<string, int> family in of)
                {
                    int n;
                    counts.TryGetValue(family.Key, out n);
                    counts[family.Key] = n + family.Value;
                }
            }

            var list = new List<FixtureFamily>();

            foreach (KeyValuePair<string, int> c in counts)
                list.Add(new FixtureFamily { Name = c.Key, Count = c.Value, Counted = CountsFamily(c.Key) });

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            return list;
        }

        public static bool CountsFamily(string familyName)
        {
            return FixtureFamilies.Count > 0
                ? FixtureFamilies.Contains((familyName ?? string.Empty).Trim())
                : LooksLikeWcOrUrinal(familyName);
        }

        // THE NAME RULE, and only a starting point. Table 6-4 counts
        // water closets and urinals; a basin is not a unit of anything.
        //
        // "Lavatory" is refused on purpose: in Revit's own US families it
        // is a WASH BASIN ("Lavatory - Vanity"), though in British English
        // it can mean a toilet - so a name like that is left for a person
        // to tick rather than guessed either way. "WC" must be a word of
        // its own: "BWC" or "Wcabinet" are not water closets.
        public static bool LooksLikeWcOrUrinal(string familyName)
        {
            string name = (familyName ?? string.Empty).ToLowerInvariant();

            if (name.Length == 0)
                return false;

            foreach (string never in new[] { "lavatory", "basin", "sink", "bidet", "shower", "bath", "drain" })
            {
                if (name.Contains(never))
                    return false;
            }

            if (name.Contains("water closet") || name.Contains("watercloset") ||
                name.Contains("toilet") || name.Contains("urinal") || name.Contains("squat"))
            {
                return true;
            }

            foreach (string word in name.Split(
                         new[] { ' ', '-', '_', '.', ',', '(', ')', ':', '/', '\\' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (word == "wc" || word == "wcs")
                    return true;
            }

            return false;
        }

        // ==================================================

        private static bool Says(string text, string word)
        {
            return !string.IsNullOrEmpty(text) &&
                   text.IndexOf(
                       word, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
