using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace ATKINSMEC
{
    // The linked rooms as data, and the arithmetic on them, are the
    // Revit-free half of this class - RVTLINKROOMS.cs
    public partial class RVTLINKSELECTION
    {
        // Set by the form, read by REVITROOMS when the run is raised
        public static List<long> SelectedLinkIds = new List<long>();

        public static HashSet<string> SelectedLevels =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // THE ROOMS UNTICKED in AUTOVENT's levels list, by RoomKey. Both
        // runs - the export and the airflow write - leave these out.
        //
        // The UNticked ones rather than the ticked ones on purpose: a
        // room the list never saw (drawn in the link after the window
        // was filled) is then processed as it always was, instead of
        // silently dropping out because nobody could tick it.
        public static HashSet<string> ExcludedRooms = new HashSet<string>();

        public class LinkItem
        {
            public long Id { get; set; }

            // The model's title - or, for a model placed more than once,
            // the title AND which placement this is (LinkLabels)
            public string Name { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        public class LevelItem
        {
            public string Name { get; set; }
            public bool InSheet { get; set; }

            public override string ToString()
            {
                return Name + (InSheet
                    ? "        [in sheet]"
                    : "        [not in sheet]");
            }
        }

        // ONE ROW PER PLACEMENT, not per model: every RevitLinkInstance
        // with a loaded document. ALIGN TERMINALS, SHEET QA and SPRINKLERS
        // list these too, and all of them match by Id, never by name.
        public static List<LinkItem> GetLinks(Document doc)
        {
            var result = new List<LinkItem>();

            Dictionary<long, string> labels = LinkLabels(doc);

            foreach (RevitLinkInstance linkInstance in Placements(doc))
            {
                long id = linkInstance.Id.ToValue();

                string label;

                if (!labels.TryGetValue(id, out label))
                    continue;

                result.Add(new LinkItem
                {
                    Id = id,
                    Name = label
                });
            }

            result.Sort((a, b) =>
                string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            return result;
        }

        private static IEnumerable<RevitLinkInstance> Placements(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(l => l.GetLinkDocument() != null);
        }

        // WHICH PLACEMENT IS WHICH, at the user's request. A model linked
        // more than once - one tower model placed for both towers - used
        // to give two rows with the same title and no way to tell them
        // apart. A title that occurs more than once now carries the rest
        // of Revit's own name for the instance ("ARC.rvt : 2 : location
        // Tower B" in Manage Links and the Properties palette), without
        // the word "location":
        //
        //     ARC-000010.rvt : 2 : Tower B
        //
        // KEPT SHORT ON PURPOSE. Office file names run to forty
        // characters, and the first render cut "location Tower B" off
        // the end of the row - the one part that tells the two apart.
        //
        // The instance part is the instance's "Name" parameter (Revit
        // numbers them 1, 2, ... and people rename them), then the part
        // of Element.Name after the title, then the element id - the
        // first that says anything. "location <Not Shared>" is Revit's
        // way of saying there is no shared site, and is left off.
        //
        // A title that occurs once is just the title, as before.
        public static Dictionary<long, string> LinkLabels(Document doc)
        {
            var labels = new Dictionary<long, string>();

            if (doc == null)
                return labels;

            List<RevitLinkInstance> placements = Placements(doc).ToList();

            var titles = placements
                .GroupBy(l => l.GetLinkDocument().Title, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (RevitLinkInstance link in placements)
            {
                string title = link.GetLinkDocument().Title;
                long id = link.Id.ToValue();

                if (titles[title] < 2)
                {
                    labels[id] = title;
                    continue;
                }

                string[] parts = (link.Name ?? string.Empty)
                    .Split(new[] { " : " }, StringSplitOptions.None);

                string which = string.Empty;

                try
                {
                    Parameter name = link.get_Parameter(
                        BuiltInParameter.RVT_LINK_INSTANCE_NAME);

                    if (name != null)
                        which = (name.AsString() ?? string.Empty).Trim();
                }
                catch
                {
                    // fall back on the element name below
                }

                if (which.Length == 0 && parts.Length >= 2)
                    which = parts[1].Trim();

                if (which.Length == 0)
                    which = "id " + id;

                string where = parts.Length >= 3 ? parts[2].Trim() : string.Empty;

                if (where.IndexOf("<Not Shared>", StringComparison.OrdinalIgnoreCase) >= 0)
                    where = string.Empty;

                if (where.StartsWith("location ", StringComparison.OrdinalIgnoreCase))
                    where = where.Substring("location ".Length).Trim();

                labels[id] = title + " : " + which +
                             (where.Length > 0 ? " : " + where : string.Empty);
            }

            return labels;
        }

        // WHICH OF THIS MODEL'S LEVELS A PLACEMENT COVERS, at the user's
        // request - "from which level they are starting to level ending
        // based on the main model levels":
        //
        //     B1_BASEMENT  to  P3_PODIUM 3   -   412 room(s)
        //
        // THE SPAN IS THE LINK'S MODEL CONTENT, its rooms are only
        // counted. It was worked out from the rooms at first, and then
        // from the link's levels for a link with none; the user asked for
        // shaft openings to be ignored, which only means anything when
        // the span is read from what is MODELLED - see Content. Shaft
        // openings are the reason it is asked for: a shaft is usually
        // constrained to some level far above or below the building, and
        // would stretch the span to reach it.
        //
        // The lowest and the highest of the link's levels that something
        // modelled STANDS ON (Element.LevelId), each put on a storey of
        // this model by the same band rule the rooms use
        // (VENTLEVELMAP.StoreyOf). A level only a wall's TOP reaches, with
        // nothing standing on it, is not counted - nearly always the roof
        // is on it, so this costs nothing in a real model and keeps the
        // test to one cheap question per level.
        //
        // The rooms are still read, for the count and for how many fall
        // below every level of this model - what AUTOVENT will skip. Only
        // AUTOVENT asks.
        public static string LevelSpan(
            Document doc, long linkId, List<VENTLEVELMAP.Storey> storeys)
        {
            var link = doc == null
                ? null
                : doc.GetElement(REVITCOMPAT.NewId(linkId)) as RevitLinkInstance;

            Document linked = link == null ? null : link.GetLinkDocument();

            if (linked == null || storeys == null || storeys.Count == 0)
                return string.Empty;

            // ---- the rooms: counted, and how many are below every level
            int rooms = 0;
            int below = 0;

            var placed = new FilteredElementCollector(linked)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0);

            foreach (Room room in placed)
            {
                rooms++;

                if (VENTLEVELMAP.StoreyOf(storeys, HostElevationOf(link, linked, room)) < 0)
                    below++;
            }

            // ---- the span: the lowest and highest level anything stands on
            Transform transform = link.GetTotalTransform();

            // The link's levels corrected on its own rooms (LevelError) -
            // the rooms above are placed by their geometry, and the span
            // must say the same floors they do
            double error = LevelError(linked);

            // Lowest first, at the height each one is IN THIS MODEL
            var levels = new FilteredElementCollector(linked)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(l => new KeyValuePair<Level, double>(
                    l, transform.OfPoint(new XYZ(0, 0, InternalZ(l) + error)).Z))
                .OrderBy(l => l.Value)
                .ToList();

            ElementMulticategoryFilter content = Content(linked);

            // ElementLevelFilter is a SLOW filter, so an empty level costs a
            // scan of the whole link. Hence inwards from each end, stopping
            // at the first level that carries something, rather than every
            // level in turn: only the empty levels at the very top and
            // bottom are ever paid for.
            Func<KeyValuePair<Level, double>, bool> carries = l =>
                content != null &&
                new FilteredElementCollector(linked)
                    .WhereElementIsNotElementType()
                    .WherePasses(content)
                    .WherePasses(new ElementLevelFilter(l.Key.Id))
                    .FirstElementId() != ElementId.InvalidElementId;

            int first = levels.FindIndex(l => carries(l));

            if (first < 0)
                return "nothing modelled on its levels   -   " + Counted(rooms, below, false);

            int last = levels.FindLastIndex(l => carries(l));

            int low = VENTLEVELMAP.StoreyOf(storeys, levels[first].Value);
            int high = VENTLEVELMAP.StoreyOf(storeys, levels[last].Value);

            string span = high < 0
                ? "all below this model's lowest level"
                : low < 0
                    ? "below " + storeys[0].Name + "  to  " + storeys[high].Name
                    : Span(storeys, low, high);

            return span + "   -   " + Counted(rooms, below, low < 0);
        }

        // "412 room(s)", and how many will be skipped for standing below
        // every level of this model. Short where the span has just said
        // "below B1_BASEMENT": the two usually come together - rooms down
        // there stand on a slab down there - and the long form ran off
        // the end of the row at 100% scaling.
        private static string Counted(int rooms, int below, bool spanSaysBelow)
        {
            if (rooms == 0)
                return "no rooms";

            if (below == 0)
                return rooms + " room(s)";

            return rooms + " room(s), " + below +
                   (spanSaysBelow ? " below" : " below this model's levels");
        }

        // WHAT COUNTS AS THE LINK'S CONTENT: every MODEL category it has,
        // less those that are not the building itself.
        //
        // - SHAFT OPENINGS, at the user's request.
        // - Rooms, areas and spaces: spaces, not things. The rooms are
        //   counted separately.
        // - Masses, model and sketch lines, reference planes, scope boxes,
        //   section boxes, cameras, levels and grids: setting-out, not
        //   building - and a level must not count as standing on itself.
        // - Nested links: another model, with a span of its own.
        // - Imported CAD (a category with a POSITIVE id, made by the
        //   import): reference, and often placed somewhere arbitrary.
        //
        // An allow-list built from the link's own categories, so anything
        // that is not a model category (views, annotation, tags) is out
        // without having to be named.
        private static readonly BuiltInCategory[] NotContent =
        {
            BuiltInCategory.OST_ShaftOpening,
            BuiltInCategory.OST_Rooms,
            BuiltInCategory.OST_Areas,
            BuiltInCategory.OST_MEPSpaces,
            BuiltInCategory.OST_Mass,
            BuiltInCategory.OST_Lines,
            BuiltInCategory.OST_SketchLines,
            BuiltInCategory.OST_CLines,
            BuiltInCategory.OST_VolumeOfInterest,
            BuiltInCategory.OST_SectionBox,
            BuiltInCategory.OST_Cameras,
            BuiltInCategory.OST_Levels,
            BuiltInCategory.OST_Grids,
            BuiltInCategory.OST_RvtLinks
        };

        private static ElementMulticategoryFilter Content(Document linked)
        {
            var skip = new HashSet<long>(NotContent.Select(c => (long)(int)c));

            var ids = new List<ElementId>();

            foreach (Category category in linked.Settings.Categories)
            {
                if (category == null || category.CategoryType != CategoryType.Model)
                    continue;

                long id = category.Id.ToValue();

                if (id >= 0 || skip.Contains(id))
                    continue;

                ids.Add(category.Id);
            }

            return ids.Count == 0 ? null : new ElementMulticategoryFilter(ids);
        }

        private static string Span(List<VENTLEVELMAP.Storey> storeys, int low, int high)
        {
            return low == high
                ? storeys[low].Name + " only"
                : storeys[low].Name + "  to  " + storeys[high].Name;
        }

        // Only levels that actually carry placed rooms are worth offering
        // ==================================================
        // The MAIN model's storeys, and getting a linked room onto one
        // ==================================================

        // AUTOVENT groups by these now rather than by the linked
        // models' own level names - see VENTLEVELMAP for why.
        //
        // Note this takes NO link ids: the levels of the main model are
        // the same whichever links are ticked, which is most of the
        // point. GetLevelNamesWithRooms below is untouched, because
        // SPRINKLERS still wants exactly what it does.
        public static List<VENTLEVELMAP.Storey> GetHostLevels(Document doc)
        {
            var storeys = new List<VENTLEVELMAP.Storey>();

            if (doc == null)
                return storeys;

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(Level));

            // Corrected on this model's own rooms and spaces, as every
            // linked level is (LevelError) - a room's floor is now its
            // geometry, and the storeys have to be in the same system
            double error = LevelError(doc);

            foreach (Element element in collector)
            {
                var level = element as Level;

                if (level == null)
                    continue;

                storeys.Add(new VENTLEVELMAP.Storey
                {
                    Name = level.Name ?? string.Empty,
                    Elevation = InternalZ(level) + error,
                    Shown = level.Elevation
                });
            }

            VENTLEVELMAP.InOrder(storeys);

            return storeys;
        }

        // A LEVEL'S HEIGHT IN ITS OWN MODEL'S INTERNAL COORDINATES - the
        // ones every point, bounding box and link transform is in.
        //
        // NOT Level.Elevation, which is what all of this used, and which
        // put a link's rooms under the level ABOVE the one they are on -
        // user-reported. Autodesk's own words: "If the Elevation Base
        // parameter is set to Project, the elevation is relative to
        // project origin. If ... Shared, the elevation is relative to
        // shared origin", and "The project base point represents the
        // origin of the project coordinate system". So Elevation is
        // measured from the base point or from the survey point, as the
        // LEVEL TYPE says - and architects very often type their levels
        // to the survey point so the heads read the site datum. A datum
        // height put through a link transform, which goes internal to
        // internal, lands the whole datum too high: with storeys about
        // that tall, exactly one level up.
        //
        // ProjectElevation is from the project base point whatever the
        // Elevation Base says, and the base point's Position is where it
        // stands in the model, so the two together are the height in
        // the model. Used for BOTH sides - this model's levels and every
        // linked level and room - so the two are always in one system.
        public static double InternalZ(Level level)
        {
            double z = level.ProjectElevation;

            try
            {
                BasePoint basePoint = BasePoint.GetProjectBasePoint(level.Document);

                if (basePoint != null)
                    z += basePoint.Position.Z;
            }
            catch
            {
                // no base point to read: the base point is the origin
            }

            return z;
        }

        // WHERE THE ROOM'S FLOOR IS, in the MAIN model's coordinates.
        //
        // The room's own floor - its level plus its base offset - rather
        // than its level alone: a room lowered 150 mm for a wet area, or
        // raised onto a mezzanine, sits where it sits, whatever level the
        // architect hung it from.
        //
        // A LINKED LEVEL'S ELEVATION IS NOT IN THE HOST'S COORDINATES
        // until the link's transform is applied to it. A link set up by
        // shared coordinates is usually a plain translation, and it is
        // the Z of that translation that would otherwise put every room
        // in the building on the wrong floor.
        //
        // AND FROM ITS GEOMETRY FIRST: the bottom of the room's own box,
        // through the link's transform. A box is in its model's internal
        // coordinates whatever any level says, so no question of which
        // origin a level is measured from can put it on the wrong floor -
        // which the level arithmetic did, ONE LEVEL UP, for every room of a
        // real job's architect's models (VENTLEVELMAP.LevelError has the
        // story). The level is only used for a room with no box.
        public static double HostElevationOf(
            RevitLinkInstance linkInstance,
            Document linkedDoc,
            Room room)
        {
            try
            {
                BoundingBoxXYZ box = room.get_BoundingBox(null);

                if (box != null)
                    return LowestZ(box, linkInstance.GetTotalTransform());
            }
            catch
            {
                // no geometry to read: the level below decides
            }

            double z = 0;

            try
            {
                var level = linkedDoc.GetElement(room.LevelId) as Level;

                if (level != null)
                    z = InternalZ(level);
            }
            catch
            {
            }

            try
            {
                z += room.BaseOffset;
            }
            catch
            {
                // no offset to read: the level is the floor
            }

            try
            {
                Transform transform = linkInstance.GetTotalTransform();

                if (transform != null)
                    return transform.OfPoint(new XYZ(0, 0, z)).Z;
            }
            catch
            {
            }

            return z;
        }

        // The lowest corner of a box once through a transform - every
        // corner, since a link turned about anything but Z tilts the box
        private static double LowestZ(BoundingBoxXYZ box, Transform transform)
        {
            double lowest = double.MaxValue;

            foreach (double x in new[] { box.Min.X, box.Max.X })
            foreach (double y in new[] { box.Min.Y, box.Max.Y })
            foreach (double z in new[] { box.Min.Z, box.Max.Z })
            {
                var corner = new XYZ(x, y, z);

                lowest = Math.Min(lowest, transform == null ? corner.Z : transform.OfPoint(corner).Z);
            }

            return lowest;
        }

        // HOW FAR THIS MODEL'S LEVELS ARE OUT, in feet, measured on its own
        // rooms and spaces (VENTLEVELMAP.LevelError): add it to InternalZ
        // to get a level's real height. Zero for a model with no rooms or
        // spaces, or whose rooms agree with its levels. Read only, and up to
        // sixty of them - a model's levels are out by one amount or not at
        // all.
        public static double LevelError(Document doc)
        {
            var deltas = new List<double>();

            if (doc == null)
                return 0;

            try
            {
                foreach (Element element in new FilteredElementCollector(doc)
                             .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory>
                             {
                                 BuiltInCategory.OST_Rooms, BuiltInCategory.OST_MEPSpaces
                             }))
                             .WhereElementIsNotElementType())
                {
                    if (deltas.Count >= 60)
                        break;

                    var spatial = element as SpatialElement;

                    if (spatial == null || spatial.Area <= 0)
                        continue;

                    var level = doc.GetElement(spatial.LevelId) as Level;
                    BoundingBoxXYZ box = spatial.get_BoundingBox(null);

                    if (level == null || box == null)
                        continue;

                    double offset = 0;

                    var room = spatial as Room;
                    var space = spatial as Autodesk.Revit.DB.Mechanical.Space;

                    if (room != null)
                        offset = room.BaseOffset;
                    else if (space != null)
                        offset = space.BaseOffset;

                    deltas.Add(box.Min.Z - (InternalZ(level) + offset));
                }
            }
            catch
            {
                // what was measured before it failed still counts
            }

            return VENTLEVELMAP.LevelError(deltas);
        }

        // EVERY ROOM OF ONE LINK, as plain data and with no apartment on
        // it - what the window caches per link and runs RoomsOn over
        // (RVTLINKROOMS.cs). Read inside the external event (VENTREAD),
        // never from a window's own handler. A link that is not loaded
        // gives an empty list, so it is not asked for again and again.
        public static List<LinkedRoom> ReadRooms(
            Document doc, long linkId, IDictionary<long, string> labels)
        {
            var read = new List<LinkedRoom>();

            var linkInstance = doc == null
                ? null
                : doc.GetElement(REVITCOMPAT.NewId(linkId)) as RevitLinkInstance;

            Document linkedDoc = linkInstance == null ? null : linkInstance.GetLinkDocument();

            if (linkedDoc == null)
                return read;

            string label;

            // The same label the links list shows, so a room from the
            // second placement of a model says so
            if (labels == null || !labels.TryGetValue(linkId, out label))
                label = linkedDoc.Title;

            foreach (Room room in new FilteredElementCollector(linkedDoc)
                         .OfCategory(BuiltInCategory.OST_Rooms)
                         .WhereElementIsNotElementType()
                         .Cast<Room>()
                         .Where(r => r.Area > 0))
            {
                try
                {
                    var own = linkedDoc.GetElement(room.LevelId) as Level;

                    Parameter name = room.get_Parameter(BuiltInParameter.ROOM_NAME);

                    read.Add(new LinkedRoom
                    {
                        LinkId = linkId,
                        RoomId = room.Id.ToValue(),
                        Name = name == null ? string.Empty : name.AsString() ?? string.Empty,
                        Number = room.Number ?? string.Empty,
                        LinkName = label,
                        LinkLevel = own == null ? string.Empty : own.Name,
                        Floor = HostElevationOf(linkInstance, linkedDoc, room)
                    });
                }
                catch
                {
                    // One unreadable room is left out, not the link
                }
            }

            return read;
        }

        public static List<string> GetLevelNamesWithRooms(
            Document doc,
            IEnumerable<long> linkIds)
        {
            var names = new List<string>();

            var seen =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (long linkId in linkIds)
            {
                RevitLinkInstance linkInstance =
                    doc.GetElement(REVITCOMPAT.NewId(linkId)) as RevitLinkInstance;

                if (linkInstance == null)
                    continue;

                Document linkDoc = linkInstance.GetLinkDocument();

                if (linkDoc == null)
                    continue;

                var rooms =
                    new FilteredElementCollector(linkDoc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0);

                foreach (Room room in rooms)
                {
                    Element level = linkDoc.GetElement(room.LevelId);

                    if (level == null)
                        continue;

                    if (seen.Add(level.Name))
                        names.Add(level.Name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);

            return names;
        }
    }
}
