using System;

namespace ATKINSMEC
{
    // THE SEARCH BOX OVER AUTOVENT'S LINKED MODELS - at the user's request
    // (2026-10-07): type part of a model's name and the list narrows as you
    // type, with no Refresh.
    //
    // It only changes what the list SHOWS. A link ticked and then hidden by
    // a search is still ticked and still read by every run - the card says
    // so (Said) - because a search box that quietly dropped links from a
    // run would be a trap.
    //
    // Revit-free, so the rule is tested (tests/ventsettingstest) and the
    // preview filters exactly as the window does.
    public static class VENTLINKFILTER
    {
        // EVERY WORD TYPED must appear in the label, in any order and any
        // case: "tower b" finds "ARC-000020.rvt : 2 : Tower B", "arc 20"
        // finds "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt". Nothing typed
        // matches everything.
        public static bool Matches(string label, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string text = label ?? string.Empty;

            foreach (string word in query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }

        // What the card says while a search is on
        public static string Said(int shown, int total, int hiddenTicked)
        {
            string said = shown == 0
                ? "No linked model matches the search."
                : "Showing " + shown + " of " + total + " linked model(s).";

            if (hiddenTicked > 0)
            {
                said += " " + hiddenTicked + " ticked model(s) hidden by the search still count - " +
                        "clear it (Esc) to see them.";
            }

            return said;
        }
    }
}
