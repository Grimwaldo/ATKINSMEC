using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ATKINSMEC
{
    // Second add-in of this project: chilled water volume. The form is
    // modal, so it runs inside the command's API context and can read
    // the model directly - no external event needed, as nothing is
    // written back to the document.
    [Transaction(TransactionMode.ReadOnly)]
    public class CHWCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // Runs with no model open too, to measure ACC models. There
            // is no active document in that case, and the form works
            // from the saved ACC list alone.
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            Document doc = uiDoc == null ? null : uiDoc.Document;

            using (CHWForm form = new CHWForm(
                doc,
                commandData.Application.Application))
            {
                form.ShowDialog();
            }

            return Result.Succeeded;
        }
    }

    // Keeps the CHW VOLUME button enabled in the zero-document state,
    // where Revit disables add-in buttons by default.
    public class CHWAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(
            UIApplication applicationData,
            CategorySet selectedCategories)
        {
            return true;
        }
    }
}
