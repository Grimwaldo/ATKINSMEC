using System.Collections.Generic;

namespace ATKINSMEC
{
    // What the export tab says, in one Revit-free place - so
    // scratchpad/ventpreview renders the REAL wording rather than a copy
    // of it that drifts, the lesson VENTLEVELNOTE was made for.
    //
    // EACH HINT IS ONE PARAGRAPH, and short. They used to be two, with a
    // blank line between, and already ran close to the card's bottom edge
    // at 150% scaling; the fixtures line now sits under them in the same
    // column, so they had to give up the room.
    public static class VENTEXPORTNOTE
    {
        public const string Fill = "fill";
        public const string NewWorkbook = "new";
        public const string AddWorksheet = "add";

        public static string Hint(string way)
        {
            switch (way)
            {
                case NewWorkbook:
                    return "The office calculation sheet plus a Room Number column: the ticked " +
                           "levels, their rooms' name, number, area and height, and every office " +
                           "formula. Nothing has to be imported first.";

                case AddWorksheet:
                    return "The same sheet, added to the imported workbook with the ASHRAE " +
                           "sheets if it lacks them. Everything in it is left alone, and a name " +
                           "already in use is refused.";

                default:
                    return "Replaces that worksheet's level and room rows with the models' " +
                           "rooms; other columns keep their formulas. Needs a workbook imported " +
                           "- it copies the row formatting from what is there.";
            }
        }

        // The line beside the fixtures tick: WHAT an export will count.
        // Short - it shares the column with the hint and a button, and
        // the first version ran off the end at "from 1...". Where from is
        // in FixturesTip, on hover.
        public static string Fixtures(bool on, int families, int sources)
        {
            return families == 0
                ? "Count WCs and urinals into No. of Units"
                : "Count " + families + " ticked famil(ies) into No. of Units";
        }

        public static string FixturesTip(bool on, int families, int sources)
        {
            if (!on)
                return "Off: the sheet's \"No. of Units\" is left as it is.";

            return "Counts the " +
                   (families == 0
                       ? "fixtures whose family name says water closet, WC, toilet or urinal"
                       : families + " fixture famil(ies) ticked under Fixtures...") +
                   " standing in each room, from " +
                   (sources == 0 ? "the linked models the rooms come from" : sources + " chosen model(s)") +
                   ", into the sheet's \"No. of Units\" - what Table 6-4's per-fixture exhaust multiplies. " +
                   "Occupants are not touched.";
        }
    }
}
