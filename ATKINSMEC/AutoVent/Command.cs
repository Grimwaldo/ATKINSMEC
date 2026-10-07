using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;

namespace ATKINSMEC
{
    // AUTOVENT. Reads an Excel ventilation schedule and writes the
    // fresh and extract airflows onto the air terminals; also exports
    // the model's rooms back into the sheet.
    //
    // The window is WPF (VENTWindow), at the user's request. It is
    // MODELESS, so the model stays usable while it is open - which
    // means everything that touches the API still goes through
    // ExtEventHan. A WPF window is no different from a WinForms one on
    // that score: calling Revit straight from a button handler is a
    // latent crash either way.
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public static UIDocument UIDoc { get; set; }
        public static Document Doc { get; set; }
        public static ExternalEvent ExtEvent { get; set; }
        public static ExtEventHan extEventHan { get; set; }

        // Held so the window survives the command returning, and so a
        // second click brings the open one forward rather than opening
        // a second
        public static VENTWindow Window { get; set; }

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDoc = commandData.Application.ActiveUIDocument;

            if (UIDoc == null || UIDoc.Document == null)
            {
                message = "Open a model first.";

                return Result.Failed;
            }

            Doc = UIDoc.Document;

            if (Window != null && Window.IsLoaded)
            {
                Window.Activate();

                return Result.Succeeded;
            }

            extEventHan = new ExtEventHan
            {
                // The window's model: nothing is done in any other (SAMEDOC)
                Home = Doc,
                HomeTitle = Doc.Title
            };

            ExtEvent = ExternalEvent.Create(extEventHan);

            Window = new VENTWindow(Doc, Doc.Title);

            // OWNED BY REVIT. Without this a modeless WPF window drops
            // behind the main window the moment somebody clicks the
            // model, and there is nothing in the taskbar to bring it
            // back - it reads as the add-in having crashed.
            new WindowInteropHelper(Window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            Window.Show();

            return Result.Succeeded;
        }
    }
}
