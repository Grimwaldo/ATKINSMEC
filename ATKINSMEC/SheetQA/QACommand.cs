using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ATKINSMEC
{
    // Sixth add-in: QA/QC of what the sheets actually say.
    //
    // Running the checks only reads. The one thing that writes is
    // marking the findings on the drawings, which is a single
    // transaction and undoes in one Ctrl+Z - hence Manual rather than
    // ReadOnly.
    //
    // The window is modeless so a finding can be shown in the model while
    // the list stays up; everything touching the API goes through QAEVENT.
    [Transaction(TransactionMode.Manual)]
    public class QACommand : IExternalCommand
    {
        // Held so the form and its event survive the command returning
        public static QAForm Form { get; set; }
        public static QAEVENT Handler { get; set; }
        public static ExternalEvent Event { get; set; }

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "Open the model to be checked first.";

                return Result.Failed;
            }

            // A second click brings the open window forward
            if (Form != null && !Form.IsDisposed)
            {
                Form.Activate();

                return Result.Succeeded;
            }

            Handler = new QAEVENT();
            Event = ExternalEvent.Create(Handler);

            Form = new QAForm(
                uiDoc.Document,
                commandData.Application.Application,
                Handler,
                Event);
            Handler.Form = Form;

            Form.Show();

            return Result.Succeeded;
        }
    }
}
