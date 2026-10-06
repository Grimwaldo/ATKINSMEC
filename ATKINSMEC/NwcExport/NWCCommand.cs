using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ATKINSMEC
{
    // NWC BATCH EXPORTER. Writes a Navisworks .nwc out of a list of
    // models, one after another, without any of them being opened in
    // the Revit window.
    //
    // Built on the CHW VOLUME pattern, which is the one add-in here
    // that already opens other models: the form is MODAL, so it runs
    // inside the command's API context and can open and close documents
    // directly - no external event, because nothing is written back to
    // the model somebody is working in.
    [Transaction(TransactionMode.ReadOnly)]
    public class NWCCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // Runs with no model open, like CHW VOLUME: a batch of
            // models on disk or in ACC needs no active document, and
            // the usual way to use this is on a machine with Revit
            // open on nothing at all.
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            Document doc = uiDoc == null ? null : uiDoc.Document;

            // The UIApplication as well as the Application: answering
            // Revit's own dialogs is a UI-side event (QUIET)
            using (NWCForm form = new NWCForm(
                doc,
                commandData.Application.Application,
                commandData.Application))
            {
                form.ShowDialog();
            }

            return Result.Succeeded;
        }
    }

    // Keeps the button alive in the zero-document state, where Revit
    // disables add-in buttons by default.
    public class NWCAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(
            UIApplication applicationData,
            CategorySet selectedCategories)
        {
            return true;
        }
    }
}
