using System.Collections.Generic;
using System.Text;

namespace ATKINSMEC
{
    // What the levels card says beside the list.
    //
    // It is in a file of its own, and Revit-free, for one reason: so
    // scratchpad/ventpreview can compile it and render the REAL wording.
    // The preview's stub used to carry its own copy of this text, and the
    // copy had drifted two versions behind the window - a render of the
    // stub said nothing about whether the real text fitted.
    //
    // THE WARNING COMES FIRST AND STANDS APART. The summary used to open
    // with three lines of counts and end with "In the sheet, not in this
    // model:", and at 150% Windows scaling the card showed only the
    // counts - the one part that names levels which will silently
    // process nothing was the part that fell off the bottom. It now has
    // its own TextBlock above the counts, in the warning colour, and the
    // pair sit in a scroll viewer that actually has a height to scroll in.
    public static class VENTLEVELNOTE
    {
        // More than this and the rest are counted rather than named
        public const int Named = 5;

        // Sheet levels this model has no level for: every row under them
        // writes nothing. Empty when there are none.
        public static string Warning(IList<string> sheetOnly)
        {
            if (sheetOnly == null || sheetOnly.Count == 0)
                return string.Empty;

            var text = new StringBuilder("In the sheet, not in this model:");

            for (int i = 0; i < sheetOnly.Count && i < Named; i++)
                text.Append("\n    ").Append(sheetOnly[i]);

            if (sheetOnly.Count > Named)
                text.Append("\n    +").Append(sheetOnly.Count - Named).Append(" more");

            return text.ToString();
        }

        // The counts - two lines, where they used to be three and a gap
        public static string Summary(
            int modelLevels, int withRooms, int links,
            int sheetLevels, int matched, int sheetOnly)
        {
            if (modelLevels <= 0)
                return "This model has no levels.";

            // The levels are THIS model's, the rooms are the links'
            string first = links == 0
                ? modelLevels + " level(s) here. Tick a link to count its rooms."
                : modelLevels + " level(s) here, " + withRooms + " with rooms.";

            if (sheetLevels == 0)
                return first + "\nOpen a workbook to match it against them.";

            return first + "\n" + matched + " matched the sheet" +
                   (sheetOnly == 0 ? " - all of it." : ".");
        }

        // The line beside Refresh on the links card: what the Apartments
        // ticks found. Short - it shares a row with a button in half the
        // window; the tooltip has the pairings in full.
        //
        // outside: interior rooms that stood in NO apartment - said here
        // too, because they are in no run and nothing else would show it
        public static string Apartments(
            int sources, int placements, int apartments, int rooms, int otherStorey, int outside)
        {
            if (sources <= 0)
                return string.Empty;

            if (rooms <= 0)
            {
                return outside > 0
                    ? "Apartments: " + outside + " interior room(s), none inside an apartment - hover."
                    : otherStorey > 0
                        ? "Apartments: rooms inside only on other levels - hover."
                        : "Apartments: no interior rooms found inside them.";
            }

            return apartments + " apartment(s) hold " + rooms + " room(s), from " +
                   placements + " placement(s)" +
                   (otherStorey > 0 ? ", " + otherStorey + " skipped" : string.Empty) +
                   (outside > 0 ? ", " + outside + " in none" : string.Empty) + ".";
        }

        // outsideOn: where the rooms in no apartment were, "LEVEL 04A 160";
        // log: the file that says what each room was found in
        // spread: apartments that took rooms from more than one interior
        // placement - an architect's room drawn across several apartments
        public static string ApartmentsTip(
            string sources, IList<string> pairings, int otherStorey,
            int outside, string outsideOn, IList<string> spread, string log)
        {
            var text = new StringBuilder("Apartments from " + sources + ".");

            if (pairings == null || pairings.Count == 0)
            {
                text.Append("\nNo interior model's rooms stand inside them on the same level of this model.");
            }
            else
            {
                text.Append("\nInterior placements found inside them, by this model's levels:");

                foreach (string line in pairings)
                    text.Append("\n    ").Append(line);
            }

            if (otherStorey > 0)
            {
                text.Append("\n").Append(otherStorey)
                    .Append(" room(s) stood inside an apartment but on ANOTHER level of this model, ")
                    .Append("and were not put under it - a placement on the wrong floor, or a double-height apartment.");
            }

            if (outside > 0)
            {
                text.Append("\n").Append(outside)
                    .Append(" room(s) of the interior models stood in NO apartment")
                    .Append(string.IsNullOrEmpty(outsideOn) ? string.Empty : " (" + outsideOn + ")")
                    .Append(" and are in no run. Either the apartments are drawn in another architect's model - ")
                    .Append("tick Apartments on it - or the two models do not line up there.");
            }

            if (spread != null && spread.Count > 0)
            {
                text.Append("\nCHECK - took rooms from more than one interior placement, so probably drawn across ")
                    .Append("several apartments:");

                foreach (string line in spread)
                    text.Append("\n    ").Append(line);
            }

            text.Append("\nThe interior models are the placements the keyword names, or those ticked level by ")
                .Append("level (Interior models...); one not ticked on the links list gives only the rooms inside apartments.");

            if (!string.IsNullOrEmpty(log) &&
                (outside > 0 || otherStorey > 0 || pairings == null || pairings.Count == 0 || (spread != null && spread.Count > 0)))
            {
                text.Append("\nWhat each room was found in, and what the other models have there: ").Append(log);
            }

            return text.ToString();
        }
    }
}
