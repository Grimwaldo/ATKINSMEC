using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ATKINSMEC
{
    // Fourth add-in of this project: NFPA 13 sprinkler distribution.
    //
    // The window is modeless, so the model stays usable while it is open
    // and a room can be shown in the view from the room list. Everything
    // that touches the API goes through SPRINKLEREVENT.
    [Transaction(TransactionMode.Manual)]
    public class SPRINKLERCommand : IExternalCommand
    {
        // Held so the form and its event survive the command returning
        public static SPRINKLERForm Form { get; set; }
        public static SPRINKLEREVENT Handler { get; set; }
        public static ExternalEvent Event { get; set; }

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            // A second click brings the open window forward
            if (Form != null && !Form.IsDisposed)
            {
                Form.Activate();
                return Result.Succeeded;
            }

            Handler = new SPRINKLEREVENT();
            Event = ExternalEvent.Create(Handler);

            Form = new SPRINKLERForm(doc, Handler, Event);
            Handler.Form = Form;

            // The window's model: nothing is done in any other (SAMEDOC)
            Handler.Home = doc;
            Handler.HomeTitle = doc.Title;

            Form.Show();

            return Result.Succeeded;
        }
    }
}
