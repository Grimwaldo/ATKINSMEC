using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // WHICH STOREY OF THE MAIN MODEL A LINKED ROOM BELONGS TO.
    //
    // AUTOVENT reads its rooms out of the linked architectural model and
    // used to group them by the LINKED model's own level names. That is
    // the wrong organising structure and it showed:
    //
    //   - the architect's levels are named to the architect's
    //     convention, so the sheet came out saying "Level 01" while
    //     every drawing, schedule and view in the MEP model says
    //     "GR_GROUND FLOOR";
    //   - two links with two different conventions produced two sets of
    //     level headings for one floor;
    //   - the levels offered in the window changed depending on which
    //     links were ticked, so the list was never the same twice.
    //
    // At the user's request the LEVELS now come from the main model and
    // the ROOMS still come from the links, each room landing on the
    // storey of the main model whose band its floor stands in - see
    // StoreyOf. The linked model's level NAMES play no part at all.
    //
    // Deliberately REVIT-FREE: matching an elevation to a storey is
    // arithmetic, and arithmetic can be tested without a model open -
    // harness at scratchpad/ventleveltest.
    public class VENTLEVELMAP
    {
        // One level of the MAIN model
        public class Storey
        {
            public string Name { get; set; }

            // Feet, Revit's internal units, in the MAIN model's
            // INTERNAL coordinates - RVTLINKSELECTION.InternalZ, never
            // Level.Elevation (see there). Whatever a link's transform
            // does has to be undone before anything is compared against
            // this.
            public double Elevation { get; set; }

            // The same level's height as Revit SHOWS it in this model -
            // from the base point or the survey point, as its type says.
            // Only for telling a person; nothing is compared against it.
            public double Shown { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        // A ROOM BELONGS TO THE STOREY WHOSE BAND ITS FLOOR STANDS IN -
        // from that level of the main model up to the next one. The
        // user's words: "the rooms only need to be in the level
        // boundaries of the main model". The linked model's level names
        // play no part, and nor do its level heights beyond where the
        // room's floor actually is.
        //
        // IT REPLACED "THE NEAREST STOREY", which put a room on whichever
        // level of the main model was closest to the linked room's LEVEL.
        // A mezzanine at +2.5 m between storeys at 0 and 4 m went to the
        // 4 m one, and a main model whose levels are not at the
        // architect's heights had a floor's rooms split between two
        // headings by whichever happened to be closer.
        //
        // THE ALLOWANCE: a floor up to 300 mm BELOW a level still counts
        // as on it. The architect's floor is finished floor level and the
        // main model's is often slab level, and a sunken wet area sits
        // 50-150 mm down - without this, a floor sitting a few
        // millimetres low drops onto the storey beneath it, silently, all
        // of it. No storey is 300 mm tall, so the allowance cannot take a
        // room off the floor it is really on.
        public static readonly double Allowance = 300.0 / 304.8;

        // Returns the index, or -1 when the floor is below every storey -
        // a room the main model has no level for - or there are none.
        public static int StoreyOf(IList<Storey> storeys, double floor)
        {
            if (storeys == null)
                return -1;

            int best = -1;

            for (int i = 0; i < storeys.Count; i++)
            {
                Storey storey = storeys[i];

                if (storey == null || storey.Elevation > floor + Allowance)
                    continue;

                // The HIGHEST level at or under the floor. Strictly
                // higher, so of two levels at one height the first in
                // the list wins - and the list arrives sorted by
                // elevation then name, so it is the same one every run.
                if (best < 0 || storey.Elevation > storeys[best].Elevation)
                    best = i;
            }

            return best;
        }

        public static string NameAt(
            IList<Storey> storeys, double floor)
        {
            int at = StoreyOf(storeys, floor);

            return at < 0 ? string.Empty : storeys[at].Name;
        }

        // HOW FAR A MODEL'S LEVEL ARITHMETIC IS OUT, from its own rooms
        // and spaces: each one's floor by its GEOMETRY (the bottom of its
        // box) less its floor by its LEVEL (RVTLINKSELECTION.InternalZ +
        // its base offset), in feet. The median, so a room with an odd box
        // cannot move a whole model; nothing at all, or less than Settled,
        // is no error.
        //
        // User-reported on the first real job (kifaf, 2026-10-10): "LEVEL
        // 03 showing no rooms despite having rooms in the models". Every
        // room of the architect's models had been put ONE LEVEL UP. The
        // proof was in the apartments log: interior rooms with their
        // floors at 37.12 m stood physically INSIDE the architect's TA-04
        // rooms, while the level arithmetic put those rooms on the level
        // at 40.62 m; and TA-05-04's box began at exactly 40.620 m, the
        // level the arithmetic said was one below it. Level.ProjectElevation
        // plus the base point's height came out 3.49 m high in that model
        // - the height its links were moved down by - and Autodesk's
        // "relative to project origin" does not say which origin. Geometry
        // does not have that question, so the model's own rooms decide.
        public static double LevelError(IEnumerable<double> deltas)
        {
            List<double> all = (deltas ?? new double[0])
                .Where(d => !double.IsNaN(d) && !double.IsInfinity(d))
                .OrderBy(d => d)
                .ToList();

            if (all.Count == 0)
                return 0;

            double median = all.Count % 2 == 1
                ? all[all.Count / 2]
                : (all[all.Count / 2 - 1] + all[all.Count / 2]) / 2;

            return Math.Abs(median) < Settled ? 0 : median;
        }

        // Below this the two ways agree, and nothing is corrected: a
        // finish or a rounding, not a model whose levels are out (50 mm)
        public static readonly double Settled = 50.0 / 304.8;

        // Sorted by elevation, lowest first - which is the order the
        // sheet comes out in, and the order somebody reads a building.
        public static void InOrder(List<Storey> storeys)
        {
            if (storeys == null)
                return;

            storeys.Sort(delegate (Storey a, Storey b)
            {
                int by = a.Elevation.CompareTo(b.Elevation);

                // Two levels at one elevation happen (a plan level and a
                // ceiling level), so the name settles it and two runs of
                // the same model cannot disagree
                return by != 0
                    ? by
                    : string.Compare(
                          a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        // Storeys by their index in a sorted list, as a person reads them:
        // a run of consecutive storeys as a range, the rest one by one -
        // "L04_LEVEL 04 to L10_LEVEL 10, L12_LEVEL 12". What the window says
        // about which floors an interior model's placement was found on.
        public static string Floors(IEnumerable<int> indices, IList<Storey> storeys)
        {
            var sorted = new List<int>();

            foreach (int i in indices ?? new int[0])
            {
                if (storeys != null && i >= 0 && i < storeys.Count && !sorted.Contains(i))
                    sorted.Add(i);
            }

            sorted.Sort();

            var parts = new List<string>();

            for (int k = 0; k < sorted.Count; k++)
            {
                int start = sorted[k];
                int end = start;

                while (k + 1 < sorted.Count && sorted[k + 1] == end + 1)
                {
                    k++;
                    end = sorted[k];
                }

                parts.Add(start == end
                    ? storeys[start].Name
                    : storeys[start].Name + " to " + storeys[end].Name);
            }

            return string.Join(", ", parts);
        }
    }
}
