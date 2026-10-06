using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // The sprinkler window is modeless so the model stays usable while
    // it is open. Everything that touches the Revit API is funnelled
    // through here, where the context is valid.
    public class SPRINKLEREVENT : IExternalEventHandler
    {
        public enum Request
        {
            None,
            ListRooms,
            Zoom,
            Place
        }

        public Request Action { get; set; }

        public SPRINKLERForm Form { get; set; }

        // ListRooms
        public List<long> LinkIds { get; set; }
        public HashSet<string> Levels { get; set; }
        public double FallbackCeilingM { get; set; }

        // Zoom
        public XYZ BoxMin { get; set; }
        public XYZ BoxMax { get; set; }
        public string ZoomLevelName { get; set; }

        // Place
        public List<SPRINKLERPLAN.RoomPlan> Plans { get; set; }
        public FamilySymbol Symbol { get; set; }
        public SPRINKLERPLAN.Hosting Hosting { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                switch (Action)
                {
                    case Request.ListRooms:
                        DoListRooms(app);
                        break;

                    case Request.Zoom:
                        DoZoom(app);
                        break;

                    case Request.Place:
                        DoPlace(app);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (Form != null)
                    Form.ReportError(ex.Message);
            }
            finally
            {
                Action = Request.None;
            }
        }

        public string GetName()
        {
            return "ATKINS sprinklers";
        }

        private void DoListRooms(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            List<SPRINKLERPLAN.RoomPlan> plans = SPRINKLERPLAN.BuildPlans(
                doc,
                LinkIds,
                Levels,
                FallbackCeilingM);

            if (Form != null)
                Form.ReportRooms(plans);
        }

        // Brings the room into view. When the active view is a plan of a
        // different floor, a plan of the room's own level is opened first.
        private void DoZoom(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;

            if (BoxMin == null || BoxMax == null)
                return;

            TryActivateLevelPlan(uidoc);

            foreach (UIView view in uidoc.GetOpenUIViews())
            {
                if (view.ViewId != uidoc.ActiveView.Id)
                    continue;

                // A little air around the room reads better than a tight fit
                XYZ margin = new XYZ(3, 3, 0);

                view.ZoomAndCenterRectangle(
                    BoxMin - margin,
                    BoxMax + margin);

                break;
            }
        }

        private void TryActivateLevelPlan(UIDocument uidoc)
        {
            if (string.IsNullOrEmpty(ZoomLevelName))
                return;

            try
            {
                ViewPlan current = uidoc.ActiveView as ViewPlan;

                if (current != null &&
                    current.GenLevel != null &&
                    string.Equals(
                        current.GenLevel.Name.Trim(),
                        ZoomLevelName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    return;

                ViewPlan match =
                    new FilteredElementCollector(uidoc.Document)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v =>
                        !v.IsTemplate &&
                        v.GenLevel != null &&
                        string.Equals(
                            v.GenLevel.Name.Trim(),
                            ZoomLevelName.Trim(),
                            StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    uidoc.ActiveView = match;
            }
            catch
            {
                // Staying in the current view is fine
            }
        }

        private void DoPlace(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            int failed;
            int hosted;
            string note;

            int placed = SPRINKLERPLAN.Place(
                doc,
                Plans,
                Symbol,
                Hosting,
                LinkIds,
                out failed,
                out hosted,
                out note);

            if (Form != null)
                Form.ReportPlaced(placed, hosted, failed, note);
        }
    }
}
