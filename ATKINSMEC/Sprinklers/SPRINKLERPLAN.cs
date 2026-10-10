using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // Collects the rooms of the selected linked model, works out a
    // sprinkler layout for each, and places the family instances.
    public class SPRINKLERPLAN
    {
        public class RoomPlan
        {
            public string LevelName { get; set; }
            public string RoomName { get; set; }
            public string RoomNumber { get; set; }
            public double AreaM2 { get; set; }

            public SPRINKLERRULES.Hazard Hazard { get; set; }

            // False when the name matched no keyword and the default
            // hazard was applied
            public bool HazardRecognised { get; set; }

            public double CeilingHeightM { get; set; }
            public bool CeilingFound { get; set; }

            public int SprinklerCount { get; set; }
            public double SpacingXM { get; set; }
            public double SpacingYM { get; set; }
            public double CoveragePerSprinklerM2 { get; set; }
            public double Coverage { get; set; }

            // Area the layout actually had to work with, after holes and
            // wall obstacles were removed. Far below AreaM2 means the
            // obstacles are eating into the room.
            public double FreeAreaM2 { get; set; }

            public int WallObstacles { get; set; }

            public string Note { get; set; }

            // Placement points in host coordinates, Z already set
            public List<XYZ> Points { get; set; }

            public string Key { get; set; }

            // Captured once so hazard changes need no Revit API access
            public SPRINKLERLAYOUT.RoomShape Shape { get; set; }

            public double CeilingZ { get; set; }

            // Room extents in host coordinates, for the Show button
            public XYZ BoxMin { get; set; }
            public XYZ BoxMax { get; set; }
        }

        private class CeilingBox
        {
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
            public double UnderZ { get; set; }
        }

        // How a family wants to be placed
        public enum Hosting
        {
            // Decide from the family's own placement type
            Automatic,

            // Point placement on a level, no host
            NonHosted,

            // On the underside face of the ceiling above the point,
            // whether the ceiling is in this model or in a link
            CeilingFace
        }

        public static bool NeedsHost(FamilySymbol symbol)
        {
            if (symbol == null || symbol.Family == null)
                return false;

            return symbol.Family.FamilyPlacementType ==
                   FamilyPlacementType.OneLevelBasedHosted;
        }

        public static bool CanHost(FamilySymbol symbol)
        {
            if (symbol == null || symbol.Family == null)
                return false;

            FamilyPlacementType placement =
                symbol.Family.FamilyPlacementType;

            return placement == FamilyPlacementType.OneLevelBasedHosted
                || placement == FamilyPlacementType.WorkPlaneBased;
        }

        public static string PlacementDescription(FamilySymbol symbol)
        {
            if (symbol == null || symbol.Family == null)
                return "unknown";

            switch (symbol.Family.FamilyPlacementType)
            {
                case FamilyPlacementType.OneLevelBasedHosted:
                    return "face hosted - needs a ceiling";
                case FamilyPlacementType.WorkPlaneBased:
                    return "work plane based - can host or stand alone";
                case FamilyPlacementType.OneLevelBased:
                    return "level based - no host";
                default:
                    return symbol.Family.FamilyPlacementType.ToString();
            }
        }

        // ==================================================
        // Gathering and calculating
        // ==================================================

        public static List<RoomPlan> BuildPlans(
            Document hostDoc,
            IEnumerable<long> linkIds,
            HashSet<string> levels,
            double fallbackCeilingM)
        {
            var plans = new List<RoomPlan>();

            foreach (long linkId in linkIds)
            {
                RevitLinkInstance link =
                    hostDoc.GetElement(REVITCOMPAT.NewId(linkId))
                    as RevitLinkInstance;

                if (link == null)
                    continue;

                Document linkDoc = link.GetLinkDocument();

                if (linkDoc == null)
                    continue;

                Transform linkToHost = link.GetTransform();

                List<CeilingBox> ceilings = CollectCeilings(linkDoc, linkToHost);
                List<WallRect> walls = CollectWalls(linkDoc, linkToHost);

                var rooms =
                    new FilteredElementCollector(linkDoc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0);

                foreach (Room room in rooms)
                {
                    Element level = linkDoc.GetElement(room.LevelId);

                    string levelName = level == null
                        ? string.Empty
                        : level.Name;

                    if (levels != null &&
                        levels.Count > 0 &&
                        !levels.Contains(levelName.Trim()))
                        continue;

                    Parameter nameParam =
                        room.get_Parameter(BuiltInParameter.ROOM_NAME);

                    string roomName = nameParam == null
                        ? string.Empty
                        : nameParam.AsString();

                    bool recognised;

                    SPRINKLERRULES.Hazard hazard =
                        SPRINKLERRULES.Guess(roomName, out recognised);

                    var plan = new RoomPlan
                    {
                        LevelName = levelName,
                        RoomName = roomName,
                        RoomNumber = room.Number,
                        AreaM2 = SPRINKLERLAYOUT.SquareFeetToSquareMetres(room.Area),
                        Hazard = hazard,
                        HazardRecognised = recognised,
                        Key = linkId + ":" + room.Id.ToValue(),
                        Points = new List<XYZ>(),
                        Note = string.Empty
                    };

                    // Height is only used to sit the sprinklers at the
                    // ceiling; the layout itself is a plan problem
                    bool fromCeiling;

                    double ceilingZ = CeilingElevation(
                        room,
                        linkToHost,
                        ceilings,
                        fallbackCeilingM,
                        out fromCeiling);

                    plan.CeilingFound = fromCeiling;

                    BoundingBoxXYZ bbox = room.get_BoundingBox(null);

                    double floorZ = bbox == null
                        ? 0
                        : linkToHost.OfPoint(bbox.Min).Z;

                    plan.CeilingHeightM =
                        SPRINKLERLAYOUT.FeetToMetres(ceilingZ - floorZ);

                    plan.CeilingZ = ceilingZ;
                    plan.Shape = SPRINKLERLAYOUT.ExtractShape(room, linkToHost);

                    // Re-extract with the nearby walls as obstacles, so a
                    // head can never end up inside one - only walls that
                    // stand in this room's storey (SPRINKLERLAYOUT.
                    // StandsInRoom), not the floors above and below
                    List<List<UV>> nearby = WallsNear(
                        walls, plan.Shape, bbox != null, floorZ, ceilingZ);

                    plan.WallObstacles = nearby.Count;

                    plan.Shape = SPRINKLERLAYOUT.ExtractShape(
                        room,
                        linkToHost,
                        nearby);

                    if (bbox != null)
                    {
                        XYZ a = linkToHost.OfPoint(bbox.Min);
                        XYZ b = linkToHost.OfPoint(bbox.Max);

                        plan.BoxMin = new XYZ(
                            Math.Min(a.X, b.X),
                            Math.Min(a.Y, b.Y),
                            Math.Min(a.Z, b.Z));

                        plan.BoxMax = new XYZ(
                            Math.Max(a.X, b.X),
                            Math.Max(a.Y, b.Y),
                            Math.Max(a.Z, b.Z));
                    }

                    Calculate(plan);

                    plans.Add(plan);
                }
            }

            plans = plans
                .OrderBy(p => p.LevelName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.RoomNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return plans;
        }

        // Recalculated whenever the user changes a room's hazard. Works
        // only on the captured shape, so it is safe to call from the
        // form without a Revit API context.
        public static void Calculate(RoomPlan plan)
        {
            SPRINKLERRULES.HazardRule rule =
                SPRINKLERRULES.RuleFor(plan.Hazard);

            SPRINKLERLAYOUT.Result layout =
                SPRINKLERLAYOUT.Layout(plan.Shape, rule);

            plan.Points = layout.Points
                .Select(p => new XYZ(p.X, p.Y, plan.CeilingZ))
                .ToList();

            plan.SprinklerCount = plan.Points.Count;
            plan.SpacingXM = SPRINKLERLAYOUT.FeetToMetres(layout.SpacingXFt);
            plan.SpacingYM = SPRINKLERLAYOUT.FeetToMetres(layout.SpacingYFt);
            plan.CoveragePerSprinklerM2 = layout.CoveragePerSprinklerM2;
            plan.Coverage = layout.Coverage;
            plan.FreeAreaM2 = layout.FreeAreaM2;

            var notes = new List<string>();

            if (!string.IsNullOrEmpty(layout.Note))
                notes.Add(layout.Note);

            if (layout.Relocated > 0)
                notes.Add(layout.Relocated + " pulled into the room");

            if (layout.WallAdjusted > 0)
                notes.Add(layout.WallAdjusted + " set off the wall");

            if (layout.Added > 0)
                notes.Add(layout.Added + " added to close gaps");

            if (layout.Dropped > 0)
                notes.Add(layout.Dropped + " dropped, too close to a wall");

            if (!plan.CeilingFound)
                notes.Add("no ceiling found");

            plan.Note = string.Join("; ", notes);
        }

        // Finds the room again so a hazard change can be recalculated
        public static Room FindRoom(
            Document hostDoc,
            string key,
            out Transform linkToHost)
        {
            linkToHost = Transform.Identity;

            string[] parts = key.Split(':');

            if (parts.Length != 2)
                return null;

            long linkId;
            long roomId;

            if (!long.TryParse(parts[0], out linkId) ||
                !long.TryParse(parts[1], out roomId))
                return null;

            RevitLinkInstance link =
                hostDoc.GetElement(REVITCOMPAT.NewId(linkId))
                as RevitLinkInstance;

            if (link == null)
                return null;

            Document linkDoc = link.GetLinkDocument();

            if (linkDoc == null)
                return null;

            linkToHost = link.GetTransform();

            return linkDoc.GetElement(REVITCOMPAT.NewId(roomId)) as Room;
        }

        // ==================================================
        // Walls, used as obstacles the sprinklers must clear
        // ==================================================

        private class WallRect
        {
            public List<UV> Poly { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }

            // Its height range in this model, when it could be read
            public bool HasHeight { get; set; }
            public double MinZ { get; set; }
            public double MaxZ { get; set; }
        }

        private static List<WallRect> CollectWalls(
            Document linkDoc,
            Transform toHost)
        {
            var rects = new List<WallRect>();

            var walls =
                new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Wall))
                .Cast<Wall>();

            foreach (Wall wall in walls)
            {
                LocationCurve location = wall.Location as LocationCurve;

                if (location == null || location.Curve == null)
                    continue;

                double half;

                try
                {
                    half = wall.Width / 2.0;
                }
                catch
                {
                    continue;
                }

                if (half <= 0)
                    continue;

                // HOW HIGH IT STANDS, in this model: every corner of its
                // box through the link's transform, so a rotated link
                // still gives the right range
                bool hasHeight = false;
                double minZ = 0;
                double maxZ = 0;

                try
                {
                    BoundingBoxXYZ box = wall.get_BoundingBox(null);

                    if (box != null)
                    {
                        minZ = double.MaxValue;
                        maxZ = double.MinValue;

                        foreach (double x in new[] { box.Min.X, box.Max.X })
                        foreach (double y in new[] { box.Min.Y, box.Max.Y })
                        foreach (double z in new[] { box.Min.Z, box.Max.Z })
                        {
                            double hz = toHost.OfPoint(new XYZ(x, y, z)).Z;

                            minZ = Math.Min(minZ, hz);
                            maxZ = Math.Max(maxZ, hz);
                        }

                        hasHeight = true;
                    }
                }
                catch
                {
                    // Unreadable: counted wherever it stands in plan, as
                    // every wall used to be
                }

                // Curved walls become a run of straight pieces
                IList<XYZ> points = location.Curve.Tessellate();

                for (int i = 0; i < points.Count - 1; i++)
                {
                    XYZ a = toHost.OfPoint(points[i]);
                    XYZ b = toHost.OfPoint(points[i + 1]);

                    double dx = b.X - a.X;
                    double dy = b.Y - a.Y;
                    double length = Math.Sqrt(dx * dx + dy * dy);

                    if (length < 1e-6)
                        continue;

                    // Perpendicular, scaled to half the wall thickness
                    double px = -dy / length * half;
                    double py = dx / length * half;

                    var poly = new List<UV>
                    {
                        new UV(a.X + px, a.Y + py),
                        new UV(b.X + px, b.Y + py),
                        new UV(b.X - px, b.Y - py),
                        new UV(a.X - px, a.Y - py)
                    };

                    rects.Add(new WallRect
                    {
                        Poly = poly,
                        MinX = poly.Min(q => q.U),
                        MaxX = poly.Max(q => q.U),
                        MinY = poly.Min(q => q.V),
                        MaxY = poly.Max(q => q.V),
                        HasHeight = hasHeight,
                        MinZ = minZ,
                        MaxZ = maxZ
                    });
                }
            }

            return rects;
        }

        // Only the walls that actually reach this room are worth testing
        private static List<List<UV>> WallsNear(
            List<WallRect> walls,
            SPRINKLERLAYOUT.RoomShape shape,
            bool roomHasHeight,
            double floorZ,
            double ceilingZ)
        {
            var result = new List<List<UV>>();

            if (walls.Count == 0 || shape == null)
                return result;

            double minX;
            double minY;
            double maxX;
            double maxY;

            SPRINKLERLAYOUT.ShapeBounds(shape, out minX, out minY, out maxX, out maxY);

            if (minX > maxX)
                return result;

            const double margin = 1.0;

            foreach (WallRect wall in walls)
            {
                if (wall.MaxX < minX - margin ||
                    wall.MinX > maxX + margin ||
                    wall.MaxY < minY - margin ||
                    wall.MinY > maxY + margin)
                    continue;

                // On another storey: not in this room, whatever its plan
                if (roomHasHeight && wall.HasHeight &&
                    !SPRINKLERLAYOUT.StandsInRoom(wall.MinZ, wall.MaxZ, floorZ, ceilingZ))
                    continue;

                result.Add(wall.Poly);
            }

            return result;
        }

        // ==================================================
        // Ceilings
        // ==================================================

        private static List<CeilingBox> CollectCeilings(
            Document linkDoc,
            Transform linkToHost)
        {
            var result = new List<CeilingBox>();

            var ceilings =
                new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType();

            foreach (Element ceiling in ceilings)
            {
                BoundingBoxXYZ bbox = ceiling.get_BoundingBox(null);

                if (bbox == null)
                    continue;

                XYZ min = linkToHost.OfPoint(bbox.Min);
                XYZ max = linkToHost.OfPoint(bbox.Max);

                result.Add(new CeilingBox
                {
                    MinX = Math.Min(min.X, max.X),
                    MaxX = Math.Max(min.X, max.X),
                    MinY = Math.Min(min.Y, max.Y),
                    MaxY = Math.Max(min.Y, max.Y),
                    UnderZ = Math.Min(min.Z, max.Z)
                });
            }

            return result;
        }

        private static double CeilingElevation(
            Room room,
            Transform linkToHost,
            List<CeilingBox> ceilings,
            double fallbackCeilingM,
            out bool fromCeiling)
        {
            fromCeiling = false;

            BoundingBoxXYZ bbox = room.get_BoundingBox(null);

            if (bbox == null)
                return 0;

            XYZ min = linkToHost.OfPoint(bbox.Min);
            XYZ max = linkToHost.OfPoint(bbox.Max);

            double floorZ = Math.Min(min.Z, max.Z);
            double topZ = Math.Max(min.Z, max.Z);

            XYZ point;

            LocationPoint location = room.Location as LocationPoint;

            point = location != null
                ? linkToHost.OfPoint(location.Point)
                : new XYZ((min.X + max.X) / 2, (min.Y + max.Y) / 2, floorZ);

            const double tolerance = 0.5;

            double lowest = double.MaxValue;

            foreach (CeilingBox ceiling in ceilings)
            {
                if (point.X < ceiling.MinX - 0.1 ||
                    point.X > ceiling.MaxX + 0.1 ||
                    point.Y < ceiling.MinY - 0.1 ||
                    point.Y > ceiling.MaxY + 0.1)
                    continue;

                if (ceiling.UnderZ <= floorZ + tolerance)
                    continue;

                if (ceiling.UnderZ > topZ + tolerance)
                    continue;

                if (ceiling.UnderZ < lowest)
                    lowest = ceiling.UnderZ;
            }

            if (lowest < double.MaxValue)
            {
                fromCeiling = true;
                return lowest;
            }

            return floorZ + SPRINKLERLAYOUT.MetresToFeet(fallbackCeilingM);
        }

        // ==================================================
        // Placing
        // ==================================================

        public static int Place(
            Document hostDoc,
            IEnumerable<RoomPlan> plans,
            FamilySymbol symbol,
            Hosting hosting,
            IEnumerable<long> linkIds,
            out int failed,
            out int hosted,
            out string note)
        {
            failed = 0;
            hosted = 0;
            int placed = 0;

            note = string.Empty;

            bool wantHost =
                hosting == Hosting.CeilingFace ||
                (hosting == Hosting.Automatic && NeedsHost(symbol));

            bool mustHost = NeedsHost(symbol);

            // Each level with its height IN THE MODEL, lowest first - the
            // same coordinates the sprinkler points are in. Never
            // Level.Elevation: see LevelUnder. Corrected on the model's own
            // rooms and spaces (RVTLINKSELECTION.LevelError), the way
            // AUTOVENT's storeys are.
            double levelError = RVTLINKSELECTION.LevelError(hostDoc);

            List<KeyValuePair<Level, double>> levels =
                new FilteredElementCollector(hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(l => new KeyValuePair<Level, double>(
                    l, RVTLINKSELECTION.InternalZ(l) + levelError))
                .OrderBy(l => l.Value)
                .ToList();

            List<CeilingFace> faces = wantHost
                ? CollectCeilingFaces(hostDoc, linkIds)
                : new List<CeilingFace>();

            if (wantHost && faces.Count == 0)
            {
                note = mustHost
                    ? "No ceiling faces were found, and this family must be hosted."
                    : "No ceiling faces were found, so the sprinklers were placed unhosted.";

                if (mustHost)
                    return 0;
            }

            using (Transaction t = new Transaction(hostDoc, "Place sprinklers"))
            {
                t.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    hostDoc.Regenerate();
                }

                foreach (RoomPlan plan in plans)
                {
                    foreach (XYZ point in plan.Points)
                    {
                        FamilyInstance instance = null;

                        if (wantHost && faces.Count > 0)
                        {
                            instance = PlaceOnFace(hostDoc, point, symbol, faces);

                            if (instance != null)
                            {
                                hosted++;
                                placed++;
                                continue;
                            }

                            // A family that must be hosted cannot fall back
                            if (mustHost)
                            {
                                failed++;
                                continue;
                            }
                        }

                        try
                        {
                            Level level = LevelUnder(levels, point.Z);

                            instance = level == null
                                ? hostDoc.Create.NewFamilyInstance(
                                    point,
                                    symbol,
                                    StructuralType.NonStructural)
                                : hostDoc.Create.NewFamilyInstance(
                                    point,
                                    symbol,
                                    level,
                                    StructuralType.NonStructural);

                            if (instance != null)
                                placed++;
                            else
                                failed++;
                        }
                        catch
                        {
                            failed++;
                        }
                    }
                }

                t.Commit();
            }

            if (string.IsNullOrEmpty(note) &&
                wantHost &&
                placed > hosted)
            {
                note =
                    (placed - hosted) +
                    " sprinkler(s) found no ceiling face and were placed unhosted.";
            }

            return placed;
        }

        // ==================================================
        // Ceiling faces, from this model and from the links
        // ==================================================

        private class CeilingFace
        {
            public Reference Reference { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
            public double Z { get; set; }
        }

        private static List<CeilingFace> CollectCeilingFaces(
            Document hostDoc,
            IEnumerable<long> linkIds)
        {
            var faces = new List<CeilingFace>();

            // Ceilings modelled in the MEP file itself
            AddFaces(hostDoc, null, Transform.Identity, faces);

            if (linkIds == null)
                return faces;

            foreach (long linkId in linkIds)
            {
                RevitLinkInstance link =
                    hostDoc.GetElement(REVITCOMPAT.NewId(linkId))
                    as RevitLinkInstance;

                if (link == null)
                    continue;

                Document linkDoc = link.GetLinkDocument();

                if (linkDoc == null)
                    continue;

                AddFaces(linkDoc, link, link.GetTransform(), faces);
            }

            return faces;
        }

        private static void AddFaces(
            Document doc,
            RevitLinkInstance link,
            Transform toHost,
            List<CeilingFace> faces)
        {
            var options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            var ceilings =
                new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType();

            foreach (Element ceiling in ceilings)
            {
                GeometryElement geometry = ceiling.get_Geometry(options);

                if (geometry == null)
                    continue;

                foreach (GeometryObject item in geometry)
                {
                    Solid solid = item as Solid;

                    if (solid == null)
                        continue;

                    foreach (Face face in solid.Faces)
                    {
                        PlanarFace planar = face as PlanarFace;

                        if (planar == null)
                            continue;

                        // Only the underside of the ceiling
                        XYZ normal = toHost.OfVector(planar.FaceNormal);

                        if (normal.Z > -0.9)
                            continue;

                        if (planar.Reference == null)
                            continue;

                        Reference reference = link == null
                            ? planar.Reference
                            : planar.Reference.CreateLinkReference(link);

                        if (reference == null)
                            continue;

                        BoundingBoxUV bounds = planar.GetBoundingBox();

                        if (bounds == null)
                            continue;

                        XYZ a = toHost.OfPoint(planar.Evaluate(bounds.Min));
                        XYZ b = toHost.OfPoint(planar.Evaluate(bounds.Max));

                        faces.Add(new CeilingFace
                        {
                            Reference = reference,
                            MinX = Math.Min(a.X, b.X),
                            MaxX = Math.Max(a.X, b.X),
                            MinY = Math.Min(a.Y, b.Y),
                            MaxY = Math.Max(a.Y, b.Y),
                            Z = toHost.OfPoint(
                                planar.Evaluate(bounds.Min)).Z
                        });
                    }
                }
            }
        }

        private static FamilyInstance PlaceOnFace(
            Document hostDoc,
            XYZ point,
            FamilySymbol symbol,
            List<CeilingFace> faces)
        {
            // The face whose elevation is closest to the point we planned
            CeilingFace best = null;
            double bestDelta = double.MaxValue;

            foreach (CeilingFace face in faces)
            {
                if (point.X < face.MinX - 0.1 ||
                    point.X > face.MaxX + 0.1 ||
                    point.Y < face.MinY - 0.1 ||
                    point.Y > face.MaxY + 0.1)
                    continue;

                double delta = Math.Abs(face.Z - point.Z);

                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = face;
                }
            }

            if (best == null)
                return null;

            try
            {
                return hostDoc.Create.NewFamilyInstance(
                    best.Reference,
                    new XYZ(point.X, point.Y, best.Z),
                    XYZ.BasisX,
                    symbol);
            }
            catch
            {
                return null;
            }
        }

        // THE LEVEL OF THE FLOOR UNDER THE SPRINKLER: the highest level at
        // or below it. The placement keeps the point exactly where it is
        // ("the physical location where the instance is to be placed"),
        // so the level is only the sprinkler's reference level and
        // offset - but that is what schedules, views and tags by level
        // go on. Below every level, the lowest one.
        //
        // It was NearestLevel, and that was wrong twice:
        //
        // - IT COMPARED Level.Elevation WITH A POINT. A point is in the
        //   model's internal coordinates; Level.Elevation is measured
        //   from the project base point or the SURVEY POINT, as the level
        //   type says - see RVTLINKSELECTION.InternalZ, which AUTOVENT
        //   needed for the same reason (its rooms landed a level up).
        // - "NEAREST" IS USUALLY THE LEVEL ABOVE. A ceiling at 2.7 m in a
        //   4 m storey is 1.3 m from the slab over it and 2.7 m from its
        //   floor, so nearly every head was tied to the floor above with
        //   a negative offset. Revit itself gives a sprinkler placed in a
        //   plan view that plan's level - the floor below. On a model
        //   typed to the survey point the first fault happened to undo
        //   the second, which is why fixing one alone would have moved
        //   those models' heads onto the level above.
        private static Level LevelUnder(
            List<KeyValuePair<Level, double>> levels, double z)
        {
            if (levels.Count == 0)
                return null;

            // A head sitting exactly on a level counts as on it
            const double Tolerance = 1e-6;

            Level under = levels[0].Key;

            foreach (KeyValuePair<Level, double> level in levels)
            {
                if (level.Value > z + Tolerance)
                    break;

                under = level.Key;
            }

            return under;
        }

        public static List<FamilySymbol> GetSprinklerSymbols(Document hostDoc)
        {
            return new FilteredElementCollector(hostDoc)
                .OfCategory(BuiltInCategory.OST_Sprinklers)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .OrderBy(s => s.FamilyName)
                .ThenBy(s => s.Name)
                .ToList();
        }
    }
}
