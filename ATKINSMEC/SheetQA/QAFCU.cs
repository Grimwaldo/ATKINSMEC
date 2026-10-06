using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // The model side of the FCU flowrate check.
    //
    // For each fan coil unit: which duct leaves its supply connector,
    // what that duct says it is carrying, and which air terminals sit
    // downstream of it. The terminals matter because they are what can
    // be changed - a duct's flow is worked out by Revit from the
    // terminals it feeds, so putting the terminals right is what puts
    // the duct right.
    //
    // Walked once per run and kept on QACONTEXT, like the duct checks.
    public class QAFCU
    {
        private const string ScanKey = "fcu-scan";

        // A safety net, not a real limit: a network that keeps giving
        // new elements past this is looping, and no FCU serves 20,000
        private const int Cap = 20000;

        public const string ByMark = "Mark";
        public const string ByTypeName = "Type Name";

        public class Terminal
        {
            public long Id;
            public string Name;
            public double Litres;
            public bool Writable;

            // A family with more than one duct connector does not offer
            // one writable Flow - the air belongs to a CONNECTOR. This
            // is the one actually carrying it: the connected one where
            // only one of two is used.
            public int ConnectorId = -1;
            public int Connectors;

            // Preset, Calculated or System - set in the family, and only
            // Preset can be given a flow
            public string Configuration = string.Empty;

            // Written through the connector rather than the instance
            // parameter
            public bool ByConnector;

            // Why it cannot be written, in words that can be acted on
            public string Locked = string.Empty;
        }

        public class Unit
        {
            public FamilyInstance Fcu;
            public long FcuId;

            public string Number = string.Empty;
            public string TypeName = string.Empty;

            public bool HasSupplyConnector;

            public long SupplyDuctId;
            public string SupplyDuctName = string.Empty;
            public double DuctLitres;
            public bool DuctFlowKnown;

            public List<Terminal> Terminals = new List<Terminal>();
            public double TerminalLitres;

            public bool Looped;

            public bool Writable
            {
                get
                {
                    foreach (Terminal terminal in Terminals)
                    {
                        if (!terminal.Writable)
                            return false;
                    }

                    return Terminals.Count > 0;
                }
            }

            // The ones standing in the way, so the finding can name
            // them instead of just saying something is locked
            public List<Terminal> Locked
            {
                get
                {
                    var locked = new List<Terminal>();

                    foreach (Terminal terminal in Terminals)
                    {
                        if (!terminal.Writable)
                            locked.Add(terminal);
                    }

                    return locked;
                }
            }
        }

        public class Scan
        {
            public List<Unit> Units = new List<Unit>();
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

            string parameter = string.IsNullOrEmpty(settings.FcuParameter)
                ? ByMark
                : settings.FcuParameter;

            foreach (FamilyInstance fcu in Equipment(doc, scope))
            {
                var unit = new Unit
                {
                    Fcu = fcu,
                    FcuId = fcu.Id.ToValue(),
                    Number = Read(fcu, parameter),
                    TypeName = TypeNameOf(fcu)
                };

                Downstream(doc, unit);

                scan.Units.Add(unit);
            }

            return scan;
        }

        private static List<FamilyInstance> Equipment(
            Document doc, QASCOPE scope)
        {
            var found = new List<FamilyInstance>();

            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                var instance = element as FamilyInstance;

                if (instance != null && InScope(instance, scope))
                    found.Add(instance);
            }

            return found;
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
        // Out of the supply connector and down the branch
        // ==================================================

        private static void Downstream(Document doc, Unit unit)
        {
            ConnectorManager manager = QADUCTS.Manager(unit.Fcu);

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

            var queue = new Queue<Element>();
            var seen = new HashSet<long> { unit.FcuId };

            foreach (Connector connector in connectors)
            {
                if (!Supply(connector))
                    continue;

                unit.HasSupplyConnector = true;

                foreach (Element next in Across(connector, unit.FcuId))
                {
                    // The duct hanging off the supply connector is the
                    // one the schedule is compared against
                    if (unit.SupplyDuctId == 0)
                    {
                        var duct = next as MEPCurve;

                        if (duct != null)
                        {
                            unit.SupplyDuctId = duct.Id.ToValue();
                            unit.SupplyDuctName = QADUCTS.Name(duct);

                            double litres;

                            unit.DuctFlowKnown = Flow(duct, out litres);
                            unit.DuctLitres = litres;
                        }
                    }

                    if (seen.Add(next.Id.ToValue()))
                        queue.Enqueue(next);
                }
            }

            int walked = 0;

            while (queue.Count > 0)
            {
                if (++walked > Cap)
                {
                    unit.Looped = true;
                    break;
                }

                Element element = queue.Dequeue();

                // A terminal is where the branch ends - and it is the
                // only thing here whose flow can be written
                if (IsTerminal(element))
                {
                    unit.Terminals.Add(TerminalOf(element));
                    continue;
                }

                // Stop at another unit rather than wandering into the
                // network it serves
                if (IsEquipment(element))
                    continue;

                ConnectorManager onward = QADUCTS.Manager(element);

                if (onward == null)
                    continue;

                ConnectorSet others;

                try
                {
                    others = onward.Connectors;
                }
                catch
                {
                    continue;
                }

                long here = element.Id.ToValue();

                foreach (Connector connector in others)
                {
                    foreach (Element next in Across(connector, here))
                    {
                        if (seen.Add(next.Id.ToValue()))
                            queue.Enqueue(next);
                    }
                }
            }

            foreach (Terminal terminal in unit.Terminals)
                unit.TerminalLitres += terminal.Litres;
        }

        // Whatever is on the other side of this connector.
        //
        // THE Domain TEST BELOW IS LOAD-BEARING. Insulation is in
        // AllRefs and DuctInsulation derives from MEPCurve - so does
        // DuctLining - which means no cast can turn a sleeve away; see
        // EQUIPWALK.Ductwork, where the same walk had to be filtered by
        // category. This one escapes only by asking the DOMAIN first.
        // Take that line out and a sleeve joins the queue as though it
        // were a duct.
        private static List<Element> Across(Connector connector, long from)
        {
            var found = new List<Element>();

            try
            {
                if (connector.Domain != Domain.DomainHvac
                    || !connector.IsConnected)
                    return found;
            }
            catch
            {
                return found;
            }

            ConnectorSet refs;

            try
            {
                refs = connector.AllRefs;
            }
            catch
            {
                return found;
            }

            foreach (Connector other in refs)
            {
                try
                {
                    if (other.Owner == null
                        || other.Owner.Id.ToValue() == from
                        || other.Domain != Domain.DomainHvac)
                        continue;

                    found.Add(other.Owner);
                }
                catch
                {
                    // that reference is not usable; try the next
                }
            }

            return found;
        }

        private static bool Supply(Connector connector)
        {
            try
            {
                return connector.Domain == Domain.DomainHvac
                       && connector.ConnectorType == ConnectorType.End
                       && connector.Direction == FlowDirectionType.Out;
            }
            catch
            {
                return false;
            }
        }

        // ==================================================
        // Flow
        // ==================================================

        public static bool Flow(Element element, out double litres)
        {
            litres = 0;

            Parameter flow = element.get_Parameter(
                BuiltInParameter.RBS_DUCT_FLOW_PARAM);

            if (flow == null || !flow.HasValue)
                return false;

            litres = UnitUtils.ConvertFromInternalUnits(
                flow.AsDouble(), UnitTypeId.LitersPerSecond);

            return true;
        }

        // The connector actually carrying the air. With two connectors
        // and only one used, that is the connected one; with one, it is
        // that one whether or not anything is joined to it yet.
        public static Connector Used(Element element, out int hvac)
        {
            hvac = 0;

            ConnectorManager manager = QADUCTS.Manager(element);

            if (manager == null)
                return null;

            ConnectorSet connectors;

            try
            {
                connectors = manager.Connectors;
            }
            catch
            {
                return null;
            }

            Connector only = null;
            Connector connected = null;

            foreach (Connector connector in connectors)
            {
                try
                {
                    if (connector.Domain != Domain.DomainHvac)
                        continue;
                }
                catch
                {
                    continue;
                }

                hvac++;

                if (only == null)
                    only = connector;

                try
                {
                    if (connected == null && connector.IsConnected)
                        connected = connector;
                }
                catch
                {
                    // a connector that will not say is no use as the
                    // chosen one, but still counts towards the total
                }
            }

            return connected ?? only;
        }

        public static string ConfigurationOf(Connector connector)
        {
            if (connector == null)
                return string.Empty;

            try
            {
                DuctFlowConfigurationType how =
                    connector.AssignedDuctFlowConfiguration;

                if (how == DuctFlowConfigurationType.Preset)
                    return "Preset";

                if (how == DuctFlowConfigurationType.Calculated)
                    return "Calculated";

                if (how == DuctFlowConfigurationType.System)
                    return "System";

                return how.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        // What this terminal is carrying now, read again from the model
        public static bool Reading(
            Document doc, Terminal terminal, out double litres)
        {
            litres = 0;

            Element element = doc.GetElement(
                REVITCOMPAT.NewId(terminal.Id));

            if (element == null)
                return false;

            litres = LitresOf(element);

            return true;
        }

        private static double LitresOf(Element element)
        {
            double fromParameter = 0;

            Parameter flow = element.get_Parameter(
                BuiltInParameter.RBS_DUCT_FLOW_PARAM);

            if (flow != null && flow.HasValue)
            {
                fromParameter = UnitUtils.ConvertFromInternalUnits(
                    flow.AsDouble(), UnitTypeId.LitersPerSecond);
            }

            if (fromParameter > 0)
                return fromParameter;

            // A two-connector family often leaves the instance parameter
            // at nothing; the connector is where the air actually is
            int hvac;

            Connector used = Used(element, out hvac);

            if (used == null)
                return fromParameter;

            try
            {
                return UnitUtils.ConvertFromInternalUnits(
                    used.Flow, UnitTypeId.LitersPerSecond);
            }
            catch
            {
                return fromParameter;
            }
        }

        // The instance parameter where the family offers one - the same
        // write AUTOVENT does - and otherwise the used connector's
        // AssignedFlow, which is what a multi-connector family exposes
        // instead.
        public static bool Write(
            Document doc, Terminal terminal, double litres)
        {
            Element element = doc.GetElement(
                REVITCOMPAT.NewId(terminal.Id));

            if (element == null)
                return false;

            double internalUnits = UnitUtils.ConvertToInternalUnits(
                litres, UnitTypeId.LitersPerSecond);

            Parameter flow = element.get_Parameter(
                BuiltInParameter.RBS_DUCT_FLOW_PARAM);

            if (flow != null && !flow.IsReadOnly)
            {
                flow.Set(internalUnits);

                return true;
            }

            int hvac;

            Connector used = Used(element, out hvac);

            if (used == null)
                return false;

            try
            {
                used.AssignedFlow = internalUnits;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Terminal TerminalOf(Element element)
        {
            int hvac;

            Connector used = Used(element, out hvac);

            var terminal = new Terminal
            {
                Id = element.Id.ToValue(),
                Name = QADUCTS.Name(element),
                Litres = LitresOf(element),
                Connectors = hvac,
                Configuration = ConfigurationOf(used)
            };

            if (used != null)
            {
                try
                {
                    terminal.ConnectorId = used.Id;
                }
                catch
                {
                    terminal.ConnectorId = -1;
                }
            }

            Parameter flow = element.get_Parameter(
                BuiltInParameter.RBS_DUCT_FLOW_PARAM);

            // One connector, an editable Flow: the ordinary case
            if (flow != null && !flow.IsReadOnly)
            {
                terminal.Writable = true;

                return terminal;
            }

            // Two connectors, only one used: the air belongs to that
            // connector, and Preset is what lets it be assigned
            if (used != null && terminal.Configuration == "Preset")
            {
                terminal.Writable = true;
                terminal.ByConnector = true;

                return terminal;
            }

            terminal.Locked = Why(flow, used, terminal);

            return terminal;
        }

        private static string Why(
            Parameter flow, Connector used, Terminal terminal)
        {
            if (used == null)
                return "it has no duct connector to carry air";

            if (terminal.Configuration.Length > 0
                && terminal.Configuration != "Preset")
            {
                return "its connector is set to " + terminal.Configuration +
                       ", and only Preset can be given a flow";
            }

            if (flow == null)
                return "it has no Flow parameter and no Preset connector";

            return "its Flow is read-only and its connector is not Preset";
        }

        // ==================================================
        // Categories
        // ==================================================

        public static bool IsTerminal(Element element)
        {
            return Category(element) == BuiltInCategory.OST_DuctTerminal;
        }

        public static bool IsEquipment(Element element)
        {
            return Category(element)
                   == BuiltInCategory.OST_MechanicalEquipment;
        }

        private static BuiltInCategory Category(Element element)
        {
            if (element.Category == null)
                return BuiltInCategory.INVALID;

            return (BuiltInCategory)element.Category.Id.ToValue();
        }

        // ==================================================
        // Which parameter holds the unit's name
        // ==================================================

        public static string Read(Element element, string parameter)
        {
            if (element == null)
                return string.Empty;

            if (string.Equals(parameter, ByTypeName,
                    StringComparison.OrdinalIgnoreCase))
                return TypeNameOf(element);

            if (string.Equals(parameter, ByMark,
                    StringComparison.OrdinalIgnoreCase))
            {
                Parameter mark = element.get_Parameter(
                    BuiltInParameter.ALL_MODEL_MARK);

                return mark == null
                    ? string.Empty
                    : (mark.AsString() ?? string.Empty).Trim();
            }

            string onInstance = Value(element.LookupParameter(parameter));

            if (onInstance.Length > 0)
                return onInstance;

            // Offices put the unit reference on the type as often as on
            // the instance
            Element type = element.Document.GetElement(element.GetTypeId());

            return type == null
                ? string.Empty
                : Value(type.LookupParameter(parameter));
        }

        private static string Value(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue)
                return string.Empty;

            try
            {
                if (parameter.StorageType == StorageType.String)
                    return (parameter.AsString() ?? string.Empty).Trim();

                return (parameter.AsValueString() ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string TypeNameOf(Element element)
        {
            var instance = element as FamilyInstance;

            if (instance != null && instance.Symbol != null)
                return instance.Symbol.Name ?? string.Empty;

            Element type = element.Document.GetElement(element.GetTypeId());

            return type == null ? string.Empty : (type.Name ?? string.Empty);
        }

        // Everything the window could offer as "the FCU number is in
        // this parameter", gathered off the equipment actually in the
        // model rather than from a guess
        public static List<string> ParameterNames(Document doc)
        {
            var names = new List<string> { ByMark, ByTypeName };

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ByMark,
                ByTypeName
            };

            int looked = 0;

            foreach (FamilyInstance fcu in Equipment(doc, null))
            {
                if (++looked > 50)
                    break;

                Gather(fcu, names, seen);

                Element type = doc.GetElement(fcu.GetTypeId());

                if (type != null)
                    Gather(type, names, seen);
            }

            names.Sort(delegate (string a, string b)
            {
                // The two built-in choices stay at the top
                int one = a == ByMark ? 0 : a == ByTypeName ? 1 : 2;
                int two = b == ByMark ? 0 : b == ByTypeName ? 1 : 2;

                return one != two
                    ? one.CompareTo(two)
                    : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            return names;
        }

        private static void Gather(
            Element element, List<string> names, HashSet<string> seen)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                try
                {
                    if (parameter.StorageType != StorageType.String
                        || parameter.Definition == null)
                        continue;

                    string name = parameter.Definition.Name;

                    if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                        names.Add(name);
                }
                catch
                {
                    // a parameter that will not say its own name is no
                    // use as a choice anyway
                }
            }
        }
    }
}
