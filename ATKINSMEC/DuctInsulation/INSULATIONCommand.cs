using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ATKINSMEC
{
    // Third add-in of this project: the rules window for the automatic
    // duct insulation. The insulating itself is done by the updater in
    // DUCTINSULATIONUPDATER while ducts are drawn.
    [Transaction(TransactionMode.ReadOnly)]
    public class INSULATIONCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (INSULATIONForm form = new INSULATIONForm(doc))
            {
                form.ShowDialog();
            }

            return Result.Succeeded;
        }
    }
}
