using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // "SHOW IN PLAN" for a room picked in the levels list, at the user's
    // request: open a plan of the level the room was put under, and frame
    // the room in it.
    //
    // Runs from ExtEventHan (request 3), never from the window: changing
    // the active view and zooming are API calls, and the window is
    // modeless. It returns a sentence for the window's status bar and
    // never throws - a room it cannot show is said so, not crashed on.
    //
    // THE ROOM IS IN A LINK, which shapes all three steps:
    //
    //   - its bounding box is in the LINK's coordinates, so every corner
    //     goes through the link's total transform before it means
    //     anything in this model's views;
    //   - the plan is THIS model's plan of the level the room was placed
    //     under (VENTLEVELMAP's band rule), not the link's own level;
    //   - selecting an element inside a link needs Selection.SetReferences,
    //     which arrived in Revit 2023. In 2022 the room is framed and not
    //     selected, and the status bar says so.
    public static class VENTSHOW
    {
        // Around the room, so it is seen in its surroundings rather than
        // edge to edge: a quarter of its size, and never under a metre
        private const double MinimumMarginFeet = 1000.0 / 304.8;

        // One or several rooms, all under ONE level of this model - the
        // window sends the level most of the picks are on, and says how
        // many were on others (elsewhere), since one plan shows one level.
        // Each room is { link id, room id }.
        public static string Show(
            UIApplication app, IList<long[]> rooms, string levelName, int elsewhere)
        {
            UIDocument uidoc = app.ActiveUIDocument;

            if (uidoc == null)
                return "No model is open.";

            Document doc = uidoc.Document;

            // The link and level ids belong to the model AUTOVENT was
            // opened from; another document in front has neither
            if (Command.Doc != null && !doc.Equals(Command.Doc))
            {
                return "Switch back to " + Command.Doc.Title +
                       " - the rooms are in its links.";
            }

            // Every room found, and the box round all of them in THIS
            // model's coordinates
            var found = new List<KeyValuePair<Room, RevitLinkInstance>>();
            int missing = 0;

            XYZ min = null;
            XYZ max = null;

            foreach (long[] pair in rooms ?? new List<long[]>())
            {
                var link = doc.GetElement(REVITCOMPAT.NewId(pair[0])) as RevitLinkInstance;
                Document linked = link == null ? null : link.GetLinkDocument();

                var room = linked == null
                    ? null
                    : linked.GetElement(REVITCOMPAT.NewId(pair[1])) as Room;

                BoundingBoxXYZ box = room == null || room.Area <= 0
                    ? null
                    : room.get_BoundingBox(null);

                if (box == null)
                {
                    missing++;
                    continue;
                }

                XYZ lo;
                XYZ hi;

                InHost(box, link.GetTotalTransform(), out lo, out hi);

                min = min == null ? lo : new XYZ(Math.Min(min.X, lo.X), Math.Min(min.Y, lo.Y), Math.Min(min.Z, lo.Z));
                max = max == null ? hi : new XYZ(Math.Max(max.X, hi.X), Math.Max(max.Y, hi.Y), Math.Max(max.Z, hi.Z));

                found.Add(new KeyValuePair<Room, RevitLinkInstance>(room, link));
            }

            if (found.Count == 0)
            {
                return (rooms == null || rooms.Count <= 1
                           ? "That room is"
                           : "Those rooms are") +
                       " no longer in the linked model - press Refresh.";
            }

            Level level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => string.Equals(
                    l.Name, levelName, StringComparison.OrdinalIgnoreCase));

            if (level == null)
                return "This model has no level called " + levelName + " any more - press Refresh.";

            string what = found.Count == 1
                ? Label(found[0].Key)
                : found.Count + " rooms";

            View view = PlanOf(doc, uidoc.ActiveView, level, min, max);

            if (view == null)
            {
                return "This model has no floor or ceiling plan of " +
                       levelName + ", so there is nowhere to show " + what + ".";
            }

            // Allowed here: an external event runs with no transaction
            // open, which is the one thing the setter asks for
            if (uidoc.ActiveView == null || uidoc.ActiveView.Id != view.Id)
                uidoc.ActiveView = view;

            UIView ui = uidoc.GetOpenUIViews()
                .FirstOrDefault(v => v.ViewId == view.Id);

            if (ui == null)
                return "Opened " + view.Name + " but could not zoom it.";

            double margin = Math.Max(
                MinimumMarginFeet,
                0.25 * Math.Max(max.X - min.X, max.Y - min.Y));

            ui.ZoomAndCenterRectangle(
                new XYZ(min.X - margin, min.Y - margin, min.Z),
                new XYZ(max.X + margin, max.Y + margin, max.Z));

            string said = "Showing " + what + " in " + view.Name + ".";

            if (!Inside(view, min, max))
            {
                said += (found.Count == 1 ? " It is" : " They are") +
                        " outside this view's crop region - turn the crop off to see " +
                        (found.Count == 1 ? "it." : "them.");
            }

            if (missing > 0)
                said += " " + missing + " could not be found - press Refresh.";

            if (elsewhere > 0)
            {
                said += " " + elsewhere + " picked room(s) on other levels " +
                        "are not in this plan.";
            }

            return said + Select(uidoc, found);
        }

        // Every corner of the link's box through the link's transform:
        // a rotated link turns an axis-aligned box into one that is not,
        // and taking only Min and Max would cut the corners off
        private static void InHost(
            BoundingBoxXYZ box, Transform link, out XYZ min, out XYZ max)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
            double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;

            Transform own = box.Transform ?? Transform.Identity;

            foreach (double x in new[] { box.Min.X, box.Max.X })
            foreach (double y in new[] { box.Min.Y, box.Max.Y })
            foreach (double z in new[] { box.Min.Z, box.Max.Z })
            {
                XYZ p = link.OfPoint(own.OfPoint(new XYZ(x, y, z)));

                x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z);
                x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
            }

            min = new XYZ(x0, y0, z0);
            max = new XYZ(x1, y1, z1);
        }

        // WHICH PLAN. The one open now if it is a plan of this level and
        // the room is in it - somebody who has already set a view up is
        // not moved off it. Otherwise the best of this level's plans:
        // one that can actually show the room, then a floor plan over a
        // ceiling plan, then a mechanical one, then a main view over a
        // dependent one (dependents are cropped by design), then by name
        // so the choice is the same every time.
        private static View PlanOf(
            Document doc, View active, Level level, XYZ min, XYZ max)
        {
            if (IsPlanOf(active, level) && Inside(active, min, max))
                return active;

            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<View>()
                .Where(v => IsPlanOf(v, level))
                .OrderBy(v => Inside(v, min, max) ? 0 : 1)
                .ThenBy(v => v.ViewType == ViewType.FloorPlan ? 0 : 1)
                .ThenBy(v => v.Discipline == ViewDiscipline.Mechanical ? 0 : 1)
                .ThenBy(v => v.GetPrimaryViewId() == ElementId.InvalidElementId ? 0 : 1)
                .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static bool IsPlanOf(View view, Level level)
        {
            if (view == null || view.IsTemplate || !(view is ViewPlan))
                return false;

            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.CeilingPlan)
                return false;

            Level of = view.GenLevel;

            return of != null && of.Id == level.Id;
        }

        // Is the room's middle inside the view's crop? A view with the
        // crop off shows everything.
        private static bool Inside(View view, XYZ min, XYZ max)
        {
            try
            {
                if (!view.CropBoxActive)
                    return true;

                BoundingBoxXYZ crop = view.CropBox;

                XYZ middle = (min + max) / 2;
                XYZ p = crop.Transform.Inverse.OfPoint(middle);

                return p.X >= crop.Min.X && p.X <= crop.Max.X &&
                       p.Y >= crop.Min.Y && p.Y <= crop.Max.Y;
            }
            catch
            {
                // an unreadable crop is not a reason to refuse the view
                return true;
            }
        }

        private static string Select(
            UIDocument uidoc, List<KeyValuePair<Room, RevitLinkInstance>> rooms)
        {
#if REVIT2023_OR_NEWER
            try
            {
                uidoc.Selection.SetReferences(rooms
                    .Select(r => new Reference(r.Key).CreateLinkReference(r.Value))
                    .ToList());

                return string.Empty;
            }
            catch
            {
                return " (They could not be selected - they are in a link.)";
            }
#else
            return " (Revit 2022 cannot select rooms inside a link, " +
                   "so they are framed but not selected.)";
#endif
        }

        private static string Label(Room room)
        {
            Parameter name = room.get_Parameter(BuiltInParameter.ROOM_NAME);

            string text = ((room.Number ?? string.Empty) + " " +
                           (name == null ? string.Empty : name.AsString() ?? string.Empty)).Trim();

            return text.Length == 0 ? "the room" : text;
        }
    }
}
