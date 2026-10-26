using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ATKINSMEC
{
    // ROOM TAGS - the rooms of the ticked linked models shown untagged in
    // the plans placed on sheets, and a tag for each, never a second one;
    // and the duplicates already there, removed (TAGPLAN has the rules).
    //
    // The window is MODAL, like DUCT ESP, so every callback runs inside
    // this command's API context. Manual, because tagging changes the model.
    [Transaction(TransactionMode.Manual)]
    public class TAGCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            if (uidoc == null)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Room tags", "Open a model first.");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;

            long preferred;

            List<KeyValuePair<long, string>> types = TAGREAD.TagTypes(doc, out preferred);

            using (var form = new TAGForm(
                       doc.Title,
                       TAGREAD.ModelKey(doc),
                       TAGREAD.Links(doc),
                       types,
                       preferred,
                       linkIds => TAGREAD.Read(doc, linkIds),
                       (plan, typeId) => TAGWRITE.Tag(doc, plan, typeId),
                       ids => TAGWRITE.Remove(doc, ids)))
            {
                form.ShowDialog();
            }

            return Result.Succeeded;
        }
    }

    // Live whenever a model is open
    public class TAGAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return applicationData.ActiveUIDocument != null;
        }
    }
}
