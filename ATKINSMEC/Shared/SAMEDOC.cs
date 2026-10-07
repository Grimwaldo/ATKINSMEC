using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ATKINSMEC
{
    // IS THE MODEL A MODELESS WINDOW WAS OPENED ON STILL THE ONE IN FRONT?
    //
    // Every modeless window (AUTOVENT, SPRINKLERS, ALIGN TERMINALS, SHEET
    // QA, EQUIPMENT CHANGER) acts through an external event, and every one
    // of those handlers worked on app.ActiveUIDocument - WHICHEVER model
    // was in front when Revit got round to it. Open SHEET QA on model A,
    // switch to model B, press Fix, and the fix ran in B with element ids
    // that were collected in A: in B those ids are other elements, or
    // nothing. A QA fix creates and deletes; EQUIPMENT CHANGER swaps types;
    // SPRINKLERS places heads. Found in a review of the suite (2026-10-07)
    // rather than reported, and closed before it was.
    //
    // So each handler is told its window's model when the window opens
    // (Home), and asks here before it does anything. The answer is null
    // when all is well, otherwise a sentence for the window to show.
    // Nothing is ever done in the wrong model - the window says so and
    // waits for its own model to come back to the front.
    //
    // Document.Equals is Revit's own test: "Two document instances are
    // considered equal if they represent the same document currently
    // opened in the Revit session."
    public static class SAMEDOC
    {
        public static string Problem(UIApplication app, Document home, string homeTitle)
        {
            string name = string.IsNullOrEmpty(homeTitle) ? "its model" : homeTitle;

            // Read nothing off a closed document - not even its title
            if (home == null || !home.IsValidObject)
            {
                return "The model this window was opened on (" + name + ") has been closed. " +
                       "Close this window and start again from the model.";
            }

            Document front = null;

            try
            {
                UIDocument ui = app == null ? null : app.ActiveUIDocument;

                front = ui == null ? null : ui.Document;
            }
            catch
            {
                front = null;
            }

            if (front == null)
                return "No model is in front. Bring " + name + " to the front and try again.";

            if (!front.Equals(home))
            {
                string other;

                try
                {
                    other = front.Title;
                }
                catch
                {
                    other = "another model";
                }

                return "This window works on " + name + ", but " + other + " is in front. " +
                       "Nothing was done - bring " + name + " to the front and try again.";
            }

            return null;
        }
    }
}
