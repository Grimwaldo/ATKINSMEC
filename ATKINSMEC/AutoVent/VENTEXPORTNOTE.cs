using System.Collections.Generic;

namespace ATKINSMEC
{
    // What the export tab says, in one Revit-free place - so
    // tests/ventpreview renders the REAL wording rather than a copy of it
    // that drifts, the lesson VENTLEVELNOTE was made for.
    //
    // A paragraph explaining each of the three ways used to sit above the
    // fixtures line; removed at the user's request (2026-10-11) - the
    // radio labels say enough.
    public static class VENTEXPORTNOTE
    {
        // The line beside the fixtures tick: WHAT an export will count.
        // Short - it shares the column with a button, and the first
        // version ran off the end at "from 1...". Where from is in
        // FixturesTip, on hover.
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
