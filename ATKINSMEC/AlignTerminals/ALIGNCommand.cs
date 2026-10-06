using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace ATKINSMEC
{
    // Only air terminals may be picked
    public class ALIGNSELECTIONFILTER : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element == null || element.Category == null)
                return false;

            return element.Category.Id.ToValue() ==
                   (long)BuiltInCategory.OST_DuctTerminal;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    // Fifth add-in: moves this model's air terminals onto the ones in a
    // linked architectural model, in plan and in height.
    //
    // The window is modeless, so the model stays usable while it is open,
    // a terminal can be shown in the view from the list, and picking
    // happens without the window getting in the way. Everything that
    // touches the API goes through ALIGNEVENT.
    [Transaction(TransactionMode.Manual)]
    public class ALIGNCommand : IExternalCommand
    {
        // Held so the form and its event survive the command returning
        public static ALIGNForm Form { get; set; }
        public static ALIGNEVENT Handler { get; set; }
        public static ExternalEvent Event { get; set; }

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "Open the model with the air terminals first.";

                return Result.Failed;
            }

            // A second click brings the open window forward
            if (Form != null && !Form.IsDisposed)
            {
                Form.Activate();

                return Result.Succeeded;
            }

            Handler = new ALIGNEVENT();
            Event = ExternalEvent.Create(Handler);

            Form = new ALIGNForm(uiDoc.Document, Handler, Event);
            Handler.Form = Form;

            Form.Show();

            return Result.Succeeded;
        }
    }
}
