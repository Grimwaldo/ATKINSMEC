using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace ATKINSMEC
{
    // ROOM TAGS' READ OF THE MODEL: the plans placed on sheets, the rooms
    // of the ticked links each one shows, and the linked rooms already
    // tagged in each view - as TAGPLAN rows. Read only; runs in the
    // command's context (the window is modal).
    public static class TAGREAD
    {
        // Every loaded link placement, with how many placed rooms it has
        public static List<TAGPLAN.Link> Links(Document doc)
        {
            var links = new List<TAGPLAN.Link>();

            foreach (RVTLINKSELECTION.LinkItem item in RVTLINKSELECTION.GetLinks(doc))
            {
                int rooms = 0;

                try
                {
                    var instance = doc.GetElement(REVITCOMPAT.NewId(item.Id)) as RevitLinkInstance;
                    Document linked = instance == null ? null : instance.GetLinkDocument();

                    if (linked != null)
                    {
                        rooms = new FilteredElementCollector(linked)
                            .OfCategory(BuiltInCategory.OST_Rooms)
                            .WhereElementIsNotElementType()
                            .Cast<Room>()
                            .Count(r => r.Area > 0);
                    }
                }
                catch
                {
                    // counted as none: still listed, still tickable
                }

                links.Add(new TAGPLAN.Link { Id = item.Id, Label = item.Name, Rooms = rooms });
            }

            return links;
        }

        // The room tag types, "Family : Type", by id
        public static List<KeyValuePair<long, string>> TagTypes(Document doc, out long preferred)
        {
            preferred = 0;

            var types = new List<KeyValuePair<long, string>>();

            foreach (FamilySymbol symbol in new FilteredElementCollector(doc)
                         .OfClass(typeof(FamilySymbol))
                         .OfCategory(BuiltInCategory.OST_RoomTags)
                         .Cast<FamilySymbol>())
            {
                string family = symbol.Family == null ? symbol.FamilyName : symbol.Family.Name;

                types.Add(new KeyValuePair<long, string>(symbol.Id.ToValue(), family + " : " + symbol.Name));
            }

            types.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase));

            try
            {
                ElementId id = doc.GetDefaultFamilyTypeId(new ElementId(BuiltInCategory.OST_RoomTags));

                if (id != null && id != ElementId.InvalidElementId)
                    preferred = id.ToValue();
            }
            catch
            {
                // no default: the first in the list
            }

            return types;
        }

        // THE READ. `linkIds`: the ticked placements.
        public static TAGPLAN.Read Read(Document doc, IList<long> linkIds)
        {
            var result = new TAGPLAN.Read();

            var links = new List<RevitLinkInstance>();

            // The notes name a link as the window lists it
            Dictionary<long, string> labels = RVTLINKSELECTION.LinkLabels(doc);

            Func<RevitLinkInstance, string> labelOf = l =>
            {
                string label;

                return labels.TryGetValue(l.Id.ToValue(), out label) ? label : l.Name;
            };

            foreach (long id in linkIds ?? new List<long>())
            {
                var instance = doc.GetElement(REVITCOMPAT.NewId(id)) as RevitLinkInstance;

                if (instance != null && instance.GetLinkDocument() != null)
                    links.Add(instance);
            }

            // Every linked room's tag, and where its head is - the ticked
            // links' ones by the view their annotation belongs to, for the
            // duplicates
            List<KeyValuePair<TAGPLAN.Tag, XYZ>> tags = TagsOf(doc);

            var ticked = new HashSet<long>(links.Select(l => l.Id.ToValue()));

            Dictionary<long, List<KeyValuePair<TAGPLAN.Tag, XYZ>>> tagsIn = tags
                .Where(t => ticked.Contains(t.Key.Link))
                .GroupBy(t => t.Key.Owner)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Revit 2022 cannot say which linked elements a view shows: a
            // room is in a plan when its floor is in the band of the plan's
            // level (VENTLEVELMAP.StoreyOf, as AUTOVENT's levels). Later
            // versions ask the view, and use the band only to say why a plan
            // shows none of a link's rooms.
            List<VENTLEVELMAP.Storey> storeys = RVTLINKSELECTION.GetHostLevels(doc);
            var floors = new Dictionary<long, List<KeyValuePair<Room, int>>>();

#if !REVIT2023_OR_NEWER
            bool linksHidden = false;
#endif

            foreach (ViewSheet sheet in new FilteredElementCollector(doc)
                         .OfClass(typeof(ViewSheet))
                         .Cast<ViewSheet>())
            {
                if (sheet.IsPlaceholder)
                    continue;

                bool any = false;

                foreach (ElementId placed in sheet.GetAllPlacedViews())
                {
                    var plan = doc.GetElement(placed) as ViewPlan;

                    if (plan == null || plan.IsTemplate ||
                        (plan.ViewType != ViewType.FloorPlan && plan.ViewType != ViewType.CeilingPlan))
                        continue;

                    any = true;

                    var view = new TAGPLAN.View
                    {
                        Sheet = sheet.Id.ToValue(),
                        SheetNumber = sheet.SheetNumber ?? string.Empty,
                        SheetName = sheet.Name ?? string.Empty,
                        Id = plan.Id.ToValue(),
                        Name = plan.Name ?? string.Empty,
                        Owner = OwnerOf(plan),
                        Ceiling = plan.ViewType == ViewType.CeilingPlan
                    };

#if !REVIT2023_OR_NEWER
                    try
                    {
                        linksHidden = plan.GetCategoryHidden(new ElementId(BuiltInCategory.OST_RvtLinks));
                    }
                    catch
                    {
                        linksHidden = false;
                    }
#endif

                    Seen(plan, view, tagsIn);

                    foreach (RevitLinkInstance link in links)
                    {
                        try
                        {
                            if (link.IsHidden(plan))
                            {
                                view.Notes.Add(labelOf(link) + " is hidden in this view.");
                                continue;
                            }

#if REVIT2023_OR_NEWER
                            List<Room> rooms = new FilteredElementCollector(doc, plan.Id, link.Id)
                                .OfCategory(BuiltInCategory.OST_Rooms)
                                .WhereElementIsNotElementType()
                                .OfType<Room>()
                                .ToList();

                            if (rooms.Count == 0)
                            {
                                int onLevel = OnLevel(link, plan, storeys, floors).Count();

                                if (onLevel > 0)
                                    view.Notes.Add(labelOf(link) + ": none of its rooms show here, " + onLevel +
                                                   " stand on this level. Rooms hidden in the view?");
                            }
#else
                            if (linksHidden)
                            {
                                view.Notes.Add("Revit links are hidden in this view.");
                                break;
                            }

                            IEnumerable<Room> rooms = OnLevel(link, plan, storeys, floors);
#endif

                            Shown(view, plan, link, rooms);
                        }
                        catch (Exception ex)
                        {
                            view.Notes.Add(labelOf(link) + ": " + ex.Message);
                        }
                    }

                    result.Views.Add(view);
                }

                if (any)
                    result.Sheets++;
            }

            result.Tagged = TAGPLAN.TaggedOf(tags.Select(t => t.Key));
            result.Tags = tagsIn.Values.SelectMany(t => t).Select(t => t.Key).ToList();

            return result;
        }

        // Which of its owner view's tags a plan shows: their heads inside
        // its crop
        private static void Seen(ViewPlan plan, TAGPLAN.View view, Dictionary<long, List<KeyValuePair<TAGPLAN.Tag, XYZ>>> tagsIn)
        {
            List<KeyValuePair<TAGPLAN.Tag, XYZ>> tags;

            if (!tagsIn.TryGetValue(TAGPLAN.OwnerOf(view), out tags))
                return;

            BoundingBoxXYZ crop = plan.CropBoxActive ? plan.CropBox : null;
            Transform toCrop = crop == null || crop.Transform == null ? null : crop.Transform.Inverse;

            foreach (KeyValuePair<TAGPLAN.Tag, XYZ> tag in tags)
            {
                if (tag.Value == null || InCrop(crop, toCrop, tag.Value))
                    tag.Key.SeenIn.Add(view.Id);
            }
        }

        private static bool InCrop(BoundingBoxXYZ crop, Transform toCrop, XYZ point)
        {
            if (crop == null)
                return true;

            XYZ local = toCrop == null ? point : toCrop.OfPoint(point);

            return TAGPLAN.InCrop(local.X, local.Y, crop.Min.X, crop.Min.Y, crop.Max.X, crop.Max.Y);
        }

        // A dependent view's annotation belongs to its primary
        private static long OwnerOf(View view)
        {
            try
            {
                ElementId primary = view.GetPrimaryViewId();

                if (primary != null && primary != ElementId.InvalidElementId)
                    return primary.ToValue();
            }
            catch
            {
            }

            return view.Id.ToValue();
        }

        // The rooms a plan shows of one link, where their tags would go:
        // the room's own point, in this model, inside the plan's crop
        private static void Shown(TAGPLAN.View view, ViewPlan plan, RevitLinkInstance link, IEnumerable<Room> rooms)
        {
            Transform toHost = link.GetTotalTransform();

            BoundingBoxXYZ crop = plan.CropBoxActive ? plan.CropBox : null;
            Transform toCrop = crop == null || crop.Transform == null ? null : crop.Transform.Inverse;

            foreach (Room room in rooms)
            {
                if (room == null || room.Area <= 0)
                    continue;

                var location = room.Location as LocationPoint;

                if (location == null)
                    continue;

                XYZ point = toHost == null ? location.Point : toHost.OfPoint(location.Point);

                if (!InCrop(crop, toCrop, point))
                {
                    view.OutsideCrop++;
                    continue;
                }

                Parameter name = room.get_Parameter(BuiltInParameter.ROOM_NAME);

                view.Shown.Add(new TAGPLAN.Room
                {
                    Link = link.Id.ToValue(),
                    Id = room.Id.ToValue(),
                    Number = room.Number ?? string.Empty,
                    Name = name == null ? string.Empty : name.AsString() ?? string.Empty,
                    X = point.X,
                    Y = point.Y
                });
            }
        }

        private static IEnumerable<Room> OnLevel(
            RevitLinkInstance link,
            ViewPlan plan,
            List<VENTLEVELMAP.Storey> storeys,
            Dictionary<long, List<KeyValuePair<Room, int>>> floors)
        {
            long id = link.Id.ToValue();

            List<KeyValuePair<Room, int>> rooms;

            if (!floors.TryGetValue(id, out rooms))
            {
                Document linked = link.GetLinkDocument();

                rooms = new List<KeyValuePair<Room, int>>();

                foreach (Room room in new FilteredElementCollector(linked)
                             .OfCategory(BuiltInCategory.OST_Rooms)
                             .WhereElementIsNotElementType()
                             .Cast<Room>()
                             .Where(r => r.Area > 0))
                {
                    int storey = VENTLEVELMAP.StoreyOf(storeys, RVTLINKSELECTION.HostElevationOf(link, linked, room));

                    rooms.Add(new KeyValuePair<Room, int>(room, storey));
                }

                floors[id] = rooms;
            }

            Level level = plan.GenLevel;

            if (level == null)
                return Enumerable.Empty<Room>();

            int band = storeys.FindIndex(s => string.Equals(s.Name, level.Name, StringComparison.Ordinal));

            return band < 0 ? Enumerable.Empty<Room>() : rooms.Where(r => r.Value == band).Select(r => r.Key);
        }

        // EVERY LINKED ROOM'S TAG, by the view its annotation belongs to (a
        // tag in a dependent is its primary's), with where its head is - any
        // type counts as tagging. An orphaned tag (its room gone) tags nothing.
        public static List<KeyValuePair<TAGPLAN.Tag, XYZ>> TagsOf(Document doc)
        {
            var tags = new List<KeyValuePair<TAGPLAN.Tag, XYZ>>();
            var owners = new Dictionary<long, long>();

            foreach (RoomTag tag in new FilteredElementCollector(doc)
                         .OfCategory(BuiltInCategory.OST_RoomTags)
                         .WhereElementIsNotElementType()
                         .OfType<RoomTag>())
            {
                try
                {
                    if (tag.IsOrphaned)
                        continue;

                    LinkElementId room = tag.TaggedRoomId;

                    if (room == null || room.LinkInstanceId == null || room.LinkInstanceId == ElementId.InvalidElementId)
                        continue;

                    long viewId = tag.OwnerViewId.ToValue();
                    long owner;

                    if (!owners.TryGetValue(viewId, out owner))
                    {
                        var view = doc.GetElement(tag.OwnerViewId) as View;

                        owners[viewId] = owner = view == null ? viewId : OwnerOf(view);
                    }

                    long link = room.LinkInstanceId.ToValue();

                    tags.Add(new KeyValuePair<TAGPLAN.Tag, XYZ>(
                        new TAGPLAN.Tag
                        {
                            Id = tag.Id.ToValue(),
                            Owner = owner,
                            Link = link,
                            Key = TAGPLAN.KeyOf(link, room.LinkedElementId.ToValue()),
                            Type = tag.GetTypeId().ToValue(),
                            Fixed = tag.Pinned || (tag.GroupId != null && tag.GroupId != ElementId.InvalidElementId)
                        },
                        HeadOf(tag)));
                }
                catch
                {
                    // an unreadable tag is not counted as tagging anything
                }
            }

            return tags;
        }

        // Where a tag reads: its head, else its point; null - shown wherever
        // its owner view is
        private static XYZ HeadOf(RoomTag tag)
        {
            try
            {
                return tag.TagHeadPosition;
            }
            catch
            {
            }

            try
            {
                var location = tag.Location as LocationPoint;

                return location == null ? null : location.Point;
            }
            catch
            {
                return null;
            }
        }

        // Where the model lives - the same key as AUTOVENT's settings
        // (VENTWindow.ModelKey): ACC GUID, central, or file; null if never
        // saved
        public static string ModelKey(Document doc)
        {
            try
            {
                if (doc.IsModelInCloud)
                {
                    ModelPath cloud = doc.GetCloudModelPath();

                    if (cloud != null)
                        return "cloud:" + cloud.GetModelGUID().ToString("D");
                }

                if (doc.IsWorkshared)
                {
                    ModelPath central = doc.GetWorksharingCentralModelPath();

                    string visible = central == null
                        ? string.Empty
                        : ModelPathUtils.ConvertModelPathToUserVisiblePath(central);

                    if (!string.IsNullOrEmpty(visible))
                        return "central:" + visible.Trim().ToLowerInvariant();
                }

                if (!string.IsNullOrEmpty(doc.PathName))
                    return "file:" + System.IO.Path.GetFullPath(doc.PathName).ToLowerInvariant();
            }
            catch
            {
            }

            return null;
        }
    }
}
