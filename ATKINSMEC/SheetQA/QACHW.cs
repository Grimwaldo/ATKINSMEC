using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // The model side of the chilled water pipe size check.
    //
    // Every place in the chilled water system where a flow and a size
    // meet. For a pipe that is the pipe itself; for a fitting or an
    // accessory it is each CONNECTOR, because a tee has three sizes and
    // three flows and checking it as one thing would be meaningless.
    //
    // Walked once per run and kept on QACONTEXT, like the duct checks.
    public class QACHW
    {
        private const string ScanKey = "chw-scan";

        private static readonly BuiltInCategory[] Carriers =
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory
        };

        // One flow meeting one size
        public class Run
        {
            public long ElementId;
            public string What = string.Empty;
            public string Name = string.Empty;

            // -1 when the size belongs to the element itself, which is
            // the case for a pipe
            public int ConnectorId = -1;

            public double Litres;
            public double DnMm;

            public long SystemTypeId;
            public string SystemName = string.Empty;

            // "Supply" or "Return", from what the window was told
            public string Side = string.Empty;

            public string Where
            {
                get
                {
                    return ConnectorId < 0
                        ? Name
                        : Name + "  connector " + ConnectorId;
                }
            }
        }

        public class Scan
        {
            public List<Run> Runs = new List<Run>();

            public int Pipes;
            public int Fittings;
            public int OutsideChosenSystems;
        }

        // What the window offers as "these system types are chilled
        // water"
        public class SystemTypeItem
        {
            public long Id { get; set; }
            public string Name { get; set; }

            // WHAT REVIT CALLS IT, not what somebody named it.
            //
            // The sizing page above pre-ticks by NAME, and that has
            // already gone wrong once: a model whose return system type
            // is not called "return" left that list empty and half the
            // job went unchecked. The disconnection page pre-ticks on
            // this instead, which no naming convention can defeat.
            public bool Hydronic { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

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

            var filter = new ElementMulticategoryFilter(
                new List<BuiltInCategory>(Carriers));

            // QACHECKS.Look, not a bare collector: "the view open now"
            // is answered by collecting FROM the view
            var collector = QACHECKS.Look(doc, scope)
                .WherePasses(filter)
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (!InScope(element, scope))
                    continue;

                var curve = element as MEPCurve;

                if (curve != null)
                {
                    Straight(scan, curve, settings);

                    continue;
                }

                Fitting(scan, element, settings);
            }

            return scan;
        }

        // ==================================================
        // A pipe: one size, one flow
        // ==================================================

        private static void Straight(
            Scan scan, MEPCurve pipe, QASETTINGS settings)
        {
            long systemType = SystemTypeOf(pipe, null);

            string side = SideOf(systemType, settings);

            if (side == null)
            {
                scan.OutsideChosenSystems++;

                return;
            }

            scan.Pipes++;

            double litres;
            double dn;

            if (!Flow(pipe, out litres) || !Bore(pipe, out dn))
                return;

            scan.Runs.Add(new Run
            {
                ElementId = pipe.Id.ToValue(),
                What = QADUCTS.Kind(pipe),
                Name = QADUCTS.Name(pipe),
                Litres = litres,
                DnMm = dn,
                SystemTypeId = systemType,
                SystemName = SystemNameOf(pipe, null),
                Side = side
            });
        }

        // ==================================================
        // A fitting: one size and one flow PER CONNECTOR
        // ==================================================

        private static void Fitting(
            Scan scan, Element element, QASETTINGS settings)
        {
            // A REDUCER IS NOT A FINDING. It exists precisely because
            // the size changes across it, so its small end is always
            // carrying the flow of the big end and always reads as
            // undersized - a false positive on every single one, by
            // construction. Skipped at the user's request.
            //
            // It is still put right: the fittings pass reaches it from
            // the pipes it joins and gives it their sizes. It just does
            // not clutter the list.
            if (IsReducer(element))
                return;

            ConnectorManager manager = QADUCTS.Manager(element);

            if (manager == null)
                return;

            ConnectorSet connectors;

            try
            {
                connectors = manager.Connectors;
            }
            catch
            {
                return;
            }

            bool counted = false;

            foreach (Connector connector in connectors)
            {
                double litres;
                double dn;
                int id;

                try
                {
                    if (connector.Domain != Domain.DomainPiping
                        || connector.ConnectorType != ConnectorType.End)
                        continue;

                    // Only a round connector has a DN to compare; an
                    // oval or rectangular one is not what this table is
                    // about
                    if (connector.Shape != ConnectorProfileType.Round)
                        continue;

                    id = connector.Id;

                    litres = UnitUtils.ConvertFromInternalUnits(
                        connector.Flow, UnitTypeId.LitersPerSecond);

                    dn = UnitUtils.ConvertFromInternalUnits(
                        connector.Radius * 2.0, UnitTypeId.Millimeters);
                }
                catch
                {
                    continue;
                }

                long systemType = SystemTypeOf(element, connector);

                string side = SideOf(systemType, settings);

                if (side == null)
                    continue;

                if (!counted)
                {
                    scan.Fittings++;
                    counted = true;
                }

                scan.Runs.Add(new Run
                {
                    ElementId = element.Id.ToValue(),
                    What = QADUCTS.Kind(element),
                    Name = QADUCTS.Name(element),
                    ConnectorId = id,
                    Litres = litres,
                    DnMm = dn,
                    SystemTypeId = systemType,
                    SystemName = SystemNameOf(element, connector),
                    Side = side
                });
            }
        }

        // ==================================================
        // Flow, size and system
        // ==================================================

        public static bool Flow(Element element, out double litres)
        {
            litres = 0;

            Parameter flow = element.get_Parameter(
                BuiltInParameter.RBS_PIPE_FLOW_PARAM);

            if (flow == null || !flow.HasValue)
                return false;

            litres = UnitUtils.ConvertFromInternalUnits(
                flow.AsDouble(), UnitTypeId.LitersPerSecond);

            return true;
        }

        public static bool Bore(MEPCurve pipe, out double millimetres)
        {
            millimetres = 0;

            // The nominal diameter is what a DN table is about; the
            // parameter carries it for pipe and flex alike
            Parameter size = pipe.get_Parameter(
                BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);

            if (size != null && size.HasValue)
            {
                millimetres = UnitUtils.ConvertFromInternalUnits(
                    size.AsDouble(), UnitTypeId.Millimeters);

                return millimetres > 0;
            }

            try
            {
                millimetres = UnitUtils.ConvertFromInternalUnits(
                    pipe.Diameter, UnitTypeId.Millimeters);

                return millimetres > 0;
            }
            catch
            {
                return false;
            }
        }

        // A transition - a reducer. The part type is the reliable
        // answer; the geometry (two ends of different sizes) is the
        // fallback for a family that will not say what it is.
        public static bool IsReducer(Element element)
        {
            var instance = element as FamilyInstance;

            if (instance == null)
                return false;

            var fitting =
                instance.MEPModel as Autodesk.Revit.DB.Mechanical.MechanicalFitting;

            if (fitting != null)
            {
                try
                {
                    if (fitting.PartType == PartType.Transition)
                        return true;
                }
                catch
                {
                    // fall through to the geometry
                }
            }

            List<Connector> ends = QACHWRUNS.Ends(element);

            if (ends.Count != 2)
                return false;

            try
            {
                return Math.Abs(ends[0].Radius - ends[1].Radius) > 1e-9;
            }
            catch
            {
                return false;
            }
        }

        // Which side this system type was put on, or null when it was
        // not chosen at all
        public static string SideOf(long systemType, QASETTINGS settings)
        {
            if (settings.ChwSupplyTypeIds.Contains(systemType))
                return "Supply";

            if (settings.ChwReturnTypeIds.Contains(systemType))
                return "Return";

            return null;
        }

        public static long SystemTypeOf(Element element, Connector connector)
        {
            // The parameter names the system TYPE directly, which is
            // what the window offers
            try
            {
                Parameter type = element.get_Parameter(
                    BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);

                if (type != null && type.HasValue)
                {
                    ElementId id = type.AsElementId();

                    if (id != null && id != ElementId.InvalidElementId)
                        return id.ToValue();
                }
            }
            catch
            {
                // fall through to the system itself
            }

            try
            {
                MEPSystem system = connector != null
                    ? connector.MEPSystem
                    : null;

                if (system != null)
                    return system.GetTypeId().ToValue();
            }
            catch
            {
                // no system on this connector
            }

            return 0;
        }

        private static string SystemNameOf(
            Element element, Connector connector)
        {
            try
            {
                MEPSystem system = connector != null
                    ? connector.MEPSystem
                    : (element as MEPCurve) == null
                        ? null
                        : ((MEPCurve)element).MEPSystem;

                if (system != null)
                    return system.Name ?? string.Empty;
            }
            catch
            {
                // not in a system
            }

            return string.Empty;
        }

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

        // ==================================================
        // What the window offers
        // ==================================================

        public static List<SystemTypeItem> SystemTypes(Document doc)
        {
            var found = new List<SystemTypeItem>();

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(PipingSystemType));

            foreach (Element element in collector)
            {
                if (element == null)
                    continue;

                found.Add(new SystemTypeItem
                {
                    Id = element.Id.ToValue(),
                    Name = element.Name ?? string.Empty,
                    Hydronic = IsHydronic(element)
                });
            }

            found.Sort(delegate (SystemTypeItem a, SystemTypeItem b)
            {
                return string.Compare(a.Name, b.Name,
                    StringComparison.OrdinalIgnoreCase);
            });

            return found;
        }

        // SystemClassification IS NOT ON PipingSystemType. It is
        // declared on the base MEPSystemType and inherited, which is
        // why the cast is here rather than the obvious property access
        // - verified against the API xml on 2022 and 2027.
        private static bool IsHydronic(Element element)
        {
            try
            {
                var type = element as MEPSystemType;

                if (type == null)
                    return false;

                MEPSystemClassification what = type.SystemClassification;

                return what == MEPSystemClassification.SupplyHydronic ||
                       what == MEPSystemClassification.ReturnHydronic;
            }
            catch
            {
                return false;
            }
        }
    }
}
