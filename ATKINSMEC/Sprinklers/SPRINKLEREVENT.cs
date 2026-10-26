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

        // The model the window was opened on, set by the command that
        // opened it: nothing is done in any other (SAMEDOC)
        public Document Home { get; set; }
        public string HomeTitle { get; set; }

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

        // THE LINKS WHOSE ROOM LEVELS THE WINDOW WANTS, kept apart from
        // Action. The window used to read them straight from a link tick,
        // outside any API context; it now asks here. A separate slot,
        // because a tick can come while a listing or a placement is
        // waiting, and the one Action slot would throw one of them away -
        // a lost listing leaves its button greyed for good.
        private readonly HashSet<long> _levelsToRead = new HashSet<long>();

        public void ReadLevels(IEnumerable<long> linkIds)
        {
            _levelsToRead.UnionWith(linkIds);
        }

        public void Execute(UIApplication app)
        {
            if (_levelsToRead.Count > 0)
            {
                List<long> ids = _levelsToRead.ToList();
                _levelsToRead.Clear();

                DoReadLevels(ids);
            }

            try
            {
                // NOT IN SOME OTHER MODEL (SAMEDOC): the window's ids belong
                // to its own model, and in another one they are other
                // elements or nothing
                string wrong = Action == Request.None
                    ? null
                    : SAMEDOC.Problem(app, Home, HomeTitle);

                if (wrong != null)
                {
                    if (Form != null && !Form.IsDisposed)
                        Form.ReportError(wrong);

                    return;
                }

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
            return "Atkins MEP Toolkit - sprinklers";
        }

        // Each link on its own, so the window can keep them per link and
        // only ask for a link it has not read. From the model the WINDOW
        // was opened on, not the one in front. EVERY link asked for is
        // answered - with nothing, if it cannot be read - or the window
        // would ask for it again on every tick.
        private void DoReadLevels(List<long> ids)
        {
            var read = new Dictionary<long, List<string>>();

            Document doc = Form == null ? null : Form.Document;

            foreach (long id in ids)
            {
                try
                {
                    read[id] = doc != null && doc.IsValidObject
                        ? RVTLINKSELECTION.GetLevelNamesWithRooms(doc, new[] { id })
                        : new List<string>();
                }
                catch
                {
                    read[id] = new List<string>();
                }
            }

            if (Form != null && !Form.IsDisposed)
                Form.ReportLevels(read);
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
