using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // WHICH LINKED PLACEMENTS ARE INTERIOR (IND) MODELS, LEVEL BY LEVEL.
    //
    // At the user's request (2026-10-09): "instead of identifying IND model
    // where their rooms sit, you can identify the instance links in each
    // level defined by keyword from user, and the user selects the links
    // found in each level". Before this, EVERY loaded placement was
    // searched for rooms standing inside an apartment, whatever it was.
    //
    //   - KEYWORDS name them (default "IND, ID" - "ID" added 2026-10-19,
    //     the user: "can the container also detect models with 'ID'
    //     instead of 'IND'"): any one of them, commas between, as WORDS of
    //     the label (Named) - "ID" finds ...-3DM-ID-001220, never GRID or
    //     SIDE. Until then one keyword, VENTLINKFILTER's substring rule.
    //   - Each candidate is offered under every storey of THIS model where
    //     it has ROOMS (each room's floor by VENTLEVELMAP.StoreyOf, the
    //     rule everything else uses) - so an interior model placed once
    //     per floor appears once per floor, and the user ticks which to
    //     use on each.
    //   - EVERYTHING FOUND STARTS TICKED, and ticks that still say "all
    //     found" are kept as "follow the keyword" (Chosen == false) - so a
    //     placement added later is picked up. Once the user changes a
    //     tick, exactly what is ticked is kept, NONE included.
    //   - AN EMPTY KEYWORD MATCHES EVERY PLACEMENT: the way it worked
    //     before, kept as the fallback.
    //
    // The rooms still join an apartment only by standing in it on the same
    // storey (VENTAPARTMENTMAP.Settle); this decides which placements are
    // searched for them. Revit-free - tests/ventsettingstest.
    public static class VENTINTERIOR
    {
        // AN INTERIOR MODEL BY ITS NAME: any of the keywords (commas or
        // semicolons between), each a word of the label - a word of three
        // letters or more STARTS one ("IND" finds IND-001220), one of one or
        // two IS one ("ID" finds ID-001220, not GRID, SIDE or IND). A keyword
        // of several words needs them all, in any order. No keyword at all:
        // every placement, as before.
        public static bool Named(string label, string keywords)
        {
            string[] name = Words(label);
            bool any = false;

            foreach (string keyword in (keywords ?? string.Empty).Split(',', ';'))
            {
                string[] words = Words(keyword);

                if (words.Length == 0)
                    continue;

                any = true;

                if (words.All(w => name.Any(n => w.Length >= 3 ? n.StartsWith(w, StringComparison.Ordinal) : n == w)))
                    return true;
            }

            return !any;
        }

        private static string[] Words(string text)
        {
            return System.Text.RegularExpressions.Regex.Split((text ?? string.Empty).ToUpperInvariant(), "[^A-Z0-9]+")
                .Where(w => w.Length > 0)
                .ToArray();
        }

        // One candidate placement on one storey of this model
        public class Found
        {
            public int Storey;                       // index into the storeys
            public string StoreyName = string.Empty;
            public long LinkId;
            public string Label = string.Empty;
            public int Rooms;                        // its rooms on this storey
        }

        // THE CANDIDATES, storey by storey (lowest first), then by label.
        //
        // links: every loaded placement, id -> label. floors: link id -> the
        // floor (feet, this model's internal Z) of each of its rooms.
        // exclude: the apartment links themselves, never interior.
        public static List<Found> Find(
            IEnumerable<KeyValuePair<long, string>> links,
            IDictionary<long, List<double>> floors,
            IList<VENTLEVELMAP.Storey> storeys,
            string keyword,
            ICollection<long> exclude)
        {
            var found = new List<Found>();

            if (links == null || floors == null || storeys == null)
                return found;

            foreach (KeyValuePair<long, string> link in links)
            {
                if (exclude != null && exclude.Contains(link.Key))
                    continue;

                if (!Named(link.Value, keyword))
                    continue;

                List<double> its;

                if (!floors.TryGetValue(link.Key, out its) || its == null)
                    continue;

                var counts = new Dictionary<int, int>();

                foreach (double floor in its)
                {
                    int storey = VENTLEVELMAP.StoreyOf(storeys, floor);

                    // Below every level of this model: on no storey
                    if (storey < 0)
                        continue;

                    int n;
                    counts.TryGetValue(storey, out n);
                    counts[storey] = n + 1;
                }

                foreach (KeyValuePair<int, int> on in counts)
                {
                    found.Add(new Found
                    {
                        Storey = on.Key,
                        StoreyName = storeys[on.Key].Name ?? string.Empty,
                        LinkId = link.Key,
                        Label = link.Value ?? string.Empty,
                        Rooms = on.Value
                    });
                }
            }

            return found
                .OrderBy(f => f.Storey)
                .ThenBy(f => f.Label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.LinkId)
                .ToList();
        }

        // IS THIS PLACEMENT AN INTERIOR MODEL ON THIS STOREY? - what the
        // apartments map asks for every room it considers.
        //
        // chosen false: follow the keyword - every matching placement, on
        // every storey. chosen true: exactly the picks, storey by storey.
        public static bool Allowed(
            string storeyName, long linkId, string label,
            string keyword, bool chosen, IDictionary<string, HashSet<long>> picks)
        {
            if (!chosen)
                return Named(label, keyword);

            HashSet<long> on;

            return picks != null &&
                   picks.TryGetValue(storeyName ?? string.Empty, out on) &&
                   on != null && on.Contains(linkId);
        }

        // WHAT TO KEEP from the dialog's ticks: null when they are exactly
        // everything found ("follow the keyword"), otherwise the ticked
        // placements per storey - possibly none at all.
        public static Dictionary<string, HashSet<long>> Choice(
            IEnumerable<Found> found, IEnumerable<KeyValuePair<string, long>> ticked)
        {
            var all = new HashSet<string>(
                (found ?? Enumerable.Empty<Found>()).Select(f => Pair(f.StoreyName, f.LinkId)),
                StringComparer.OrdinalIgnoreCase);

            var picks = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
            var on = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, long> t in ticked ?? Enumerable.Empty<KeyValuePair<string, long>>())
            {
                string storey = t.Key ?? string.Empty;

                HashSet<long> set;

                if (!picks.TryGetValue(storey, out set))
                {
                    set = new HashSet<long>();
                    picks[storey] = set;
                }

                set.Add(t.Value);
                on.Add(Pair(storey, t.Value));
            }

            return on.SetEquals(all) ? null : picks;
        }

        // What the cached apartments map was built for - the apartment
        // links, the keyword and the picks. A change to any of them builds
        // it again.
        public static string Key(
            IEnumerable<long> sources, string keyword, bool chosen,
            IDictionary<string, HashSet<long>> picks)
        {
            string key = string.Join(",", (sources ?? Enumerable.Empty<long>()).OrderBy(i => i)) +
                         "|" + (keyword ?? string.Empty).Trim().ToLowerInvariant();

            if (!chosen)
                return key + "|follow";

            return key + "|" + string.Join(";",
                (picks ?? new Dictionary<string, HashSet<long>>())
                    .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(p => p.Key.ToLowerInvariant() + "=" +
                                 string.Join(",", (p.Value ?? new HashSet<long>()).OrderBy(i => i))));
        }

        private static string Pair(string storey, long id)
        {
            return (storey ?? string.Empty).ToLowerInvariant() + "\u0001" + id;
        }
    }
}
