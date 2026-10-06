using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Shared ground for the duct disconnection checks.
    //
    // "Disconnected" here means an END connector in the HVAC domain with
    // nothing on the other side. That is what Revit itself calls a
    // disconnect, and it is what leaves a takeoff hanging off nothing, a
    // terminal with no air, and a system that will not calculate.
    //
    // The model is walked ONCE per run and the answer kept on QACONTEXT,
    // so four checks do not each collect every duct in the job.
    public class QADUCTS
    {
        private const string ScanKey = "duct-scan";

        // Everything that can carry a duct connector
        private static readonly BuiltInCategory[] Carriers =
        {
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctTerminal,
            BuiltInCategory.OST_MechanicalEquipment
        };

        // Is this element one of them?
        //
        // The scan COLLECTS these categories, so anything walking the
        // network afterwards must accept the same ones - otherwise the
        // scan and the fix disagree about what the duct network is
        // made of.
        //
        // ASKED BY CATEGORY, AND THAT IS THE WHOLE POINT. Insulation
        // is in Connector.AllRefs, and DuctInsulation derives from
        // MEPCurve - so does DuctLining, both compile-verified against
        // the 2022 reference assemblies. So `element as MEPCurve`
        // ACCEPTS a sleeve of fibreglass and is not a guard, while a
        // category cannot be confused that way. Found for real on the
        // pipe side (QACHWJOINTFIX.Pipework); EQUIPWALK.Ductwork asks
        // this, and so does QADUCTFIX.Branching.
        public static bool Carries(Element element)
        {
            if (element == null || element.Category == null)
                return false;

            long id = element.Category.Id.ToValue();

            foreach (BuiltInCategory carrier in Carriers)
            {
                if (id == (long)carrier)
                    return true;
            }

            return false;
        }

        // A connector with nothing on the other side
        public class LooseEnd
        {
            public Element Owner;
            public long OwnerId;

            // A Connector object cannot be carried out of the run it was
            // made in, so the pair (owner, connector id) is what the fix
            // looks the connector up by
            public int ConnectorId;

            public XYZ Point;
            public XYZ Facing;

            public long SystemId;
            public string SystemName;

            public bool OnTap;
            public string What;

            // Worked out by the scan
            public Target Nearest;
            public LooseEnd Meets;

            public bool HasSystem
            {
                get { return SystemId != 0; }
            }
        }

        // A duct this loose end could be joined into
        public class Target
        {
            public MEPCurve Duct;
            public long DuctId;
            public string DuctName;
            public double Feet;
            public bool SameSystem;
        }

        public class Scan
        {
            public List<LooseEnd> Ends = new List<LooseEnd>();
            public int Walked;
            public int Ducts;
        }

        // ==================================================
        // The one walk
        // ==================================================

        public static Scan Of(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            object cached;

            if (context != null
                && context.Shared.TryGetValue(ScanKey, out cached))
            {
                var already = cached as Scan;

                if (already != null)
                    return already;
            }

            Scan scan = Walk(
                doc,
                context == null ? null : context.Scope,
                settings);

            if (context != null)
                context.Shared[ScanKey] = scan;

            return scan;
        }

        private static Scan Walk(
            Document doc, QASCOPE scope, QASETTINGS settings)
        {
            var scan = new Scan();

            List<MEPCurve> ducts = Ducts(doc, scope);

            scan.Ducts = ducts.Count;

            var index = new DuctGrid(ducts, QAGRIDS.ToFeet(settings.DuctReachMm));

            foreach (Element element in Carrying(doc, scope))
            {
                scan.Walked++;

                ConnectorManager manager = Manager(element);

                if (manager == null)
                    continue;

                bool tap = IsTap(element);

                ConnectorSet connectors;

                try
                {
                    connectors = manager.Connectors;
                }
                catch
                {
                    continue;
                }

                foreach (Connector connector in connectors)
                {
                    if (!Loose(connector))
                        continue;

                    LooseEnd end = Describe(connector, element, tap);

                    if (end == null)
                        continue;

                    scan.Ends.Add(end);
                }
            }

            // Nearest duct for each, then the pairs that meet
            double reach = QAGRIDS.ToFeet(settings.DuctReachMm);

            foreach (LooseEnd end in scan.Ends)
                end.Nearest = index.Nearest(end, reach, settings);

            Pair(scan.Ends, QAGRIDS.ToFeet(settings.DuctJoinMm));

            return scan;
        }

        private static LooseEnd Describe(
            Connector connector, Element element, bool tap)
        {
            XYZ point;
            XYZ facing;
            int id;

            try
            {
                point = connector.Origin;

                // Connector.Direction is a flow direction, not a vector -
                // the way it faces is the Z of its coordinate system
                facing = connector.CoordinateSystem == null
                    ? XYZ.BasisZ
                    : connector.CoordinateSystem.BasisZ;

                id = connector.Id;
            }
            catch
            {
                return null;
            }

            if (point == null)
                return null;

            MEPSystem system = SystemOf(connector, element);

            return new LooseEnd
            {
                Owner = element,
                OwnerId = element.Id.ToValue(),
                ConnectorId = id,
                Point = point,
                Facing = facing,
                SystemId = system == null ? 0 : system.Id.ToValue(),
                SystemName = system == null ? string.Empty : system.Name,
                OnTap = tap,
                What = Kind(element)
            };
        }

        // ==================================================
        // Connectors
        // ==================================================

        public static ConnectorManager Manager(Element element)
        {
            var curve = element as MEPCurve;

            if (curve != null)
            {
                try
                {
                    return curve.ConnectorManager;
                }
                catch
                {
                    return null;
                }
            }

            var instance = element as FamilyInstance;

            if (instance == null || instance.MEPModel == null)
                return null;

            try
            {
                return instance.MEPModel.ConnectorManager;
            }
            catch
            {
                return null;
            }
        }

        public static bool Loose(Connector connector)
        {
            try
            {
                return connector.Domain == Domain.DomainHvac
                       && connector.ConnectorType == ConnectorType.End
                       && !connector.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        // Finding the same connector again at fix time
        public static Connector Lookup(
            Document doc, long ownerId, int connectorId)
        {
            Element owner = doc.GetElement(REVITCOMPAT.NewId(ownerId));

            if (owner == null)
                return null;

            ConnectorManager manager = Manager(owner);

            if (manager == null)
                return null;

            try
            {
                return manager.Lookup(connectorId);
            }
            catch
            {
                return null;
            }
        }

        // ==================================================
        // What kind of thing it is
        // ==================================================

        public static bool IsTap(Element element)
        {
            var instance = element as FamilyInstance;

            if (instance == null)
                return false;

            var fitting = instance.MEPModel as MechanicalFitting;

            if (fitting == null)
                return false;

            PartType part;

            try
            {
                part = fitting.PartType;
            }
            catch
            {
                return false;
            }

            return part == PartType.TapPerpendicular
                || part == PartType.TapAdjustable
                || part == PartType.SpudPerpendicular
                || part == PartType.SpudAdjustable;
        }

        public static string Kind(Element element)
        {
            if (element.Category == null)
                return "Element";

            return element.Category.Name;
        }

        public static string Name(Element element)
        {
            string kind = Kind(element);

            return kind + "  [" + element.Id.ToValue() + "]";
        }

        // ==================================================
        // Systems
        // ==================================================

        public static MEPSystem SystemOf(Connector connector, Element element)
        {
            try
            {
                if (connector.MEPSystem != null)
                    return connector.MEPSystem;
            }
            catch
            {
                // some connectors refuse to say
            }

            var duct = element as Duct;

            if (duct != null)
            {
                try
                {
                    return duct.MEPSystem;
                }
                catch
                {
                    return null;
                }
            }

            var flex = element as FlexDuct;

            if (flex != null)
            {
                try
                {
                    return flex.MEPSystem;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static long SystemOf(MEPCurve curve)
        {
            var duct = curve as Duct;

            if (duct != null)
            {
                try
                {
                    return duct.MEPSystem == null
                        ? 0 : duct.MEPSystem.Id.ToValue();
                }
                catch
                {
                    return 0;
                }
            }

            var flex = curve as FlexDuct;

            if (flex == null)
                return 0;

            try
            {
                return flex.MEPSystem == null
                    ? 0 : flex.MEPSystem.Id.ToValue();
            }
            catch
            {
                return 0;
            }
        }

        // ==================================================
        // Collecting
        // ==================================================

        public static List<Element> Carrying(Document doc, QASCOPE scope)
        {
            var found = new List<Element>();

            var filter = new ElementMulticategoryFilter(
                new List<BuiltInCategory>(Carriers));

            var collector = QACHECKS.Look(doc, scope)
                .WherePasses(filter)
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (InScope(element, scope))
                    found.Add(element);
            }

            return found;
        }

        public static List<MEPCurve> Ducts(Document doc, QASCOPE scope)
        {
            var found = new List<MEPCurve>();

            var filter = new ElementMulticategoryFilter(
                new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_DuctCurves
                });

            var collector = QACHECKS.Look(doc, scope)
                .WherePasses(filter)
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                var duct = element as MEPCurve;

                if (duct != null && InScope(element, scope))
                    found.Add(duct);
            }

            return found;
        }

        // A level filter, when the window asked for one. Anything with no
        // level to speak of is kept rather than silently dropped.
        private static bool InScope(Element element, QASCOPE scope)
        {
            if (scope == null
                || scope.What != QASCOPE.Kind.PickedLevels
                || scope.LevelIds.Count == 0)
                return true;

            ElementId level = element.LevelId;

            if (level == null || level == ElementId.InvalidElementId)
                return true;

            return scope.LevelIds.Contains(level.ToValue());
        }

        public static Curve LineOf(MEPCurve curve)
        {
            var location = curve.Location as LocationCurve;

            return location == null ? null : location.Curve;
        }

        // ==================================================
        // Ends that meet but were never joined
        // ==================================================

        private static void Pair(List<LooseEnd> ends, double tolerance)
        {
            // Sorted along X, so the sweep only has to look at the ends
            // that could still be within reach
            ends.Sort((a, b) => a.Point.X.CompareTo(b.Point.X));

            for (int i = 0; i < ends.Count; i++)
            {
                LooseEnd one = ends[i];

                if (one.Meets != null)
                    continue;

                for (int j = i + 1; j < ends.Count; j++)
                {
                    LooseEnd two = ends[j];

                    if (two.Point.X - one.Point.X > tolerance)
                        break;

                    if (two.Meets != null || two.OwnerId == one.OwnerId)
                        continue;

                    if (one.Point.DistanceTo(two.Point) > tolerance)
                        continue;

                    // Two ends of the same run face each other; two ends
                    // that merely pass close by do not
                    if (one.Facing.DotProduct(two.Facing) > -0.5)
                        continue;

                    one.Meets = two;
                    two.Meets = one;

                    break;
                }
            }
        }

        // ==================================================
        // Which duct to join into
        // ==================================================

        // Ducts bucketed into coarse cells, so a loose end only measures
        // against the ducts that could possibly be near it. Without this
        // the search is every end against every duct in the job.
        private class DuctGrid
        {
            private const long Offset = 1L << 20;
            private const long Span = 1L << 21;

            private readonly double _cell;

            private readonly Dictionary<long, List<MEPCurve>> _cells =
                new Dictionary<long, List<MEPCurve>>();

            // Ducts too long to bucket sensibly - always measured
            private readonly List<MEPCurve> _oversized = new List<MEPCurve>();

            public DuctGrid(List<MEPCurve> ducts, double reach)
            {
                // Cells comfortably bigger than the reach, so only the
                // 27 around a point ever have to be looked at
                _cell = Math.Max(reach * 2.0, 10.0);

                foreach (MEPCurve duct in ducts)
                {
                    Curve line = LineOf(duct);

                    if (line == null)
                        continue;

                    XYZ a = line.GetEndPoint(0);
                    XYZ b = line.GetEndPoint(1);

                    // A duct is put in every cell its run passes through,
                    // walked along its length
                    double length = a.DistanceTo(b);

                    int steps = (int)Math.Ceiling(length / _cell) + 1;

                    // The 27-cell lookup only works while the samples
                    // are no further apart than one cell. A duct too
                    // long to sample that finely goes in the list that
                    // is always looked at, rather than being quietly
                    // missed.
                    if (steps > 4096)
                    {
                        _oversized.Add(duct);
                        continue;
                    }

                    var seen = new HashSet<long>();

                    for (int i = 0; i <= steps; i++)
                    {
                        double t = steps == 0 ? 0 : (double)i / steps;

                        XYZ at = a + (b - a) * t;

                        long key = Key(at);

                        if (!seen.Add(key))
                            continue;

                        List<MEPCurve> bucket;

                        if (!_cells.TryGetValue(key, out bucket))
                        {
                            bucket = new List<MEPCurve>();
                            _cells[key] = bucket;
                        }

                        bucket.Add(duct);
                    }
                }
            }

            private long Key(XYZ point)
            {
                return Key(
                    Cell(point.X), Cell(point.Y), Cell(point.Z));
            }

            private long Cell(double value)
            {
                return (long)Math.Floor(value / _cell);
            }

            private static long Key(long x, long y, long z)
            {
                // Shifted into a positive range before packing, or a
                // negative coordinate would collide with a positive one
                long px = Clamp(x + Offset);
                long py = Clamp(y + Offset);
                long pz = Clamp(z + Offset);

                return (px << 42) | (py << 21) | pz;
            }

            private static long Clamp(long value)
            {
                if (value < 0)
                    return 0;

                return value >= Span ? Span - 1 : value;
            }

            public Target Nearest(
                LooseEnd end, double reach, QASETTINGS settings)
            {
                Target sameSystem = null;
                Target any = null;

                long cx = Cell(end.Point.X);
                long cy = Cell(end.Point.Y);
                long cz = Cell(end.Point.Z);

                var done = new HashSet<long>();

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        for (long dz = -1; dz <= 1; dz++)
                        {
                            List<MEPCurve> bucket;

                            if (!_cells.TryGetValue(
                                    Key(cx + dx, cy + dy, cz + dz),
                                    out bucket))
                                continue;

                            foreach (MEPCurve duct in bucket)
                            {
                                long id = duct.Id.ToValue();

                                if (id == end.OwnerId || !done.Add(id))
                                    continue;

                                Weigh(end, duct, reach,
                                    ref sameSystem, ref any);
                            }
                        }
                    }
                }

                foreach (MEPCurve duct in _oversized)
                {
                    long id = duct.Id.ToValue();

                    if (id == end.OwnerId || !done.Add(id))
                        continue;

                    Weigh(end, duct, reach, ref sameSystem, ref any);
                }

                // The system it belongs to comes first; the nearest duct
                // of any system is the fallback, and only when the window
                // says so or the end has no system of its own
                if (sameSystem != null)
                    return sameSystem;

                if (!end.HasSystem || settings.DuctFallBackToNearest)
                    return any;

                return null;
            }

            private static void Weigh(
                LooseEnd end,
                MEPCurve duct,
                double reach,
                ref Target sameSystem,
                ref Target any)
            {
                Curve line = LineOf(duct);

                if (line == null)
                    return;

                double feet;

                try
                {
                    feet = line.Distance(end.Point);
                }
                catch
                {
                    return;
                }

                if (feet > reach)
                    return;

                long system = QADUCTS.SystemOf(duct);

                var target = new Target
                {
                    Duct = duct,
                    DuctId = duct.Id.ToValue(),
                    DuctName = Name(duct),
                    Feet = feet,
                    SameSystem = system != 0 && system == end.SystemId
                };

                if (any == null || feet < any.Feet)
                    any = target;

                if (target.SameSystem
                    && (sameSystem == null || feet < sameSystem.Feet))
                    sameSystem = target;
            }
        }
    }
}
