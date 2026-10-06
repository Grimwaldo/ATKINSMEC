using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace ATKINSMEC
{
    // The button only opens the window. AutoSync itself is started by
    // SYNCSERVICE when Revit comes up and asks, and keeps running
    // whether or not this window is open.
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class SYNCCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                // Already up: bring it forward rather than opening a
                // second one, like the other modeless windows here
                if (SYNCForm.Open != null && !SYNCForm.Open.IsDisposed)
                {
                    SYNCForm.Open.Activate();

                    return Result.Succeeded;
                }

                // Normally done at startup, but a session that somehow
                // missed it should still get a working window
                SYNCSERVICE.Prepare();

                SYNCSETTINGS.Load();

                var form = new SYNCForm();

                form.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;

                return Result.Failed;
            }
        }
    }

    // AutoSync is about Revit rather than about one model, so the button
    // is live with nothing open - the same reasoning as CHW VOLUME
    public class SYNCAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(
            UIApplication applicationData, CategorySet selectedCategories)
        {
            return true;
        }
    }
}
