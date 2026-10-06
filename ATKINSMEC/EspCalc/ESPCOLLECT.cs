using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace ATKINSMEC
{
    // READS THIS MODEL AND THE TICKED LINKS INTO ESPNET - the one file of
    // the ESP add-in, beside the command, that touches Revit. Everything
    // is converted to metres, m3/s and Pa here, and every point and
    // direction is put through its link's TOTAL transform, so ESPNET
    // works in this model's coordinates whichever model a duct is in.
    //
    // Nothing here writes to a linked model - it cannot - and the only
    // write at all is Write, onto units in this model.
    public static class ESPCOLLECT
    {
        // Everything a duct network is made of. Asked BY CATEGORY: duct
        // insulation and lining are in Connector.AllRefs and both derive
        // from MEPCurve, so `as MEPCurve` would take a sleeve of
        // fibreglass for a duct - see QADUCTS.Carries, which every join
        // here is also tested against.
        private static readonly List<BuiltInCategory> Ductwork = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctTerminal,
            BuiltInCategory.OST_MechanicalEquipment
        };

        private static double M(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);
        }

        private static double Pa(double value)
        {
            return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Pascals);
        }

        private static double M3s(double value)
        {
            return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.CubicMetersPerSecond);
        }

        // ==================================================
        // The models in play
        // ==================================================

        private class Source
        {
            public Document Doc;
            public long LinkId;
            public string Label;
            public Transform ToHost;
        }

        private static List<Source> Sources(Document doc, IEnumerable<long> linkIds)
        {
            var sources = new List<Source>
            {
                new Source { Doc = doc, LinkId = 0, Label = "This model", ToHost = Transform.Identity }
            };

            Dictionary<long, string> labels = RVTLINKSELECTION.LinkLabels(doc);

            foreach (long id in linkIds ?? new List<long>())
            {
                var link = doc.GetElement(REVITCOMPAT.NewId(id)) as RevitLinkInstance;
                Document linked = link == null ? null : link.GetLinkDocument();

                if (linked == null)
                    continue;

                string label;

                sources.Add(new Source
                {
                    Doc = linked,
                    LinkId = id,
                    Label = labels.TryGetValue(id, out label) ? label : linked.Title,
                    ToHost = link.GetTotalTransform()
                });
            }

            return sources;
        }

        // ==================================================
        // The units to choose from
        // ==================================================

        public static List<ESPUNITROW> Units(
            Document doc, IEnumerable<long> linkIds, ICollection<long> pickedHere)
        {
            var rows = new List<ESPUNITROW>();

            foreach (Source source in Sources(doc, linkIds))
            {
                foreach (Element element in new FilteredElementCollector(source.Doc)
                             .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                             .WhereElementIsNotElementType())
                {
                    try
                    {
                        ConnectorManager manager = QADUCTS.Manager(element);

                        if (manager == null)
                            continue;

                        var systems = new List<string>();

                        foreach (Connector c in manager.Connectors)
                        {
                            if (c.Domain != Domain.DomainHvac || c.ConnectorType != ConnectorType.End)
                                continue;

                            string system = SystemOf(c);

                            systems.Add(system.Length == 0 ? "?" : system == "Other" ? "Other air" : system);
                        }

                        if (systems.Count == 0)
                            continue;

                        long id = element.Id.ToValue();

                        rows.Add(new ESPUNITROW
                        {
                            Key = source.LinkId + ":" + id,
                            LinkId = source.LinkId,
                            ElementId = id,
                            Name = TypeName(source.Doc, element),
                            Mark = MarkOf(element),
                            Model = source.Label,
                            Ports = string.Join(", ", systems.Distinct()),
                            Picked = source.LinkId == 0 && pickedHere != null && pickedHere.Contains(id)
                        });
                    }
                    catch
                    {
                        // a unit that cannot be read is not offered
                    }
                }
            }

            return rows
                .OrderBy(r => r.LinkId == 0 ? 0 : 1)
                .ThenBy(r => r.Model, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Mark, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ==================================================
        // The whole network of every model in play
        // ==================================================

        public static List<ESPNET.Part> Read(
            Document doc, IEnumerable<long> linkIds, List<string> report)
        {
            var parts = new List<ESPNET.Part>();

            foreach (Source source in Sources(doc, linkIds))
            {
                // A connector is found again by (element, connector id)
                // within its own model - the pair QADUCTFIX relies on too
                var byConnector = new Dictionary<string, ESPNET.Port>();
                var pending = new List<Pending>();
                var byElement = new Dictionary<long, ESPNET.Part>();

                var counts = new Dictionary<ESPNET.Kind, int>();
                int skipped = 0;

                foreach (Element element in new FilteredElementCollector(source.Doc)
                             .WherePasses(new ElementMulticategoryFilter(Ductwork))
                             .WhereElementIsNotElementType())
                {
                    try
                    {
                        ESPNET.Part part = PartOf(element, source);

                        if (part == null)
                            continue;

                        ReadPorts(element, part, source, byConnector, pending);

                        if (part.Ports.Count == 0)
                            continue;

                        parts.Add(part);
                        byElement[part.ElementId] = part;

                        int n;
                        counts.TryGetValue(part.What, out n);
                        counts[part.What] = n + 1;
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                int made = 0;

                foreach (Pending join in pending)
                {
                    ESPNET.Port other;

                    if (!byConnector.TryGetValue(join.Key, out other))
                        other = TapOnDuct(join, byElement, byConnector);

                    if (other != null)
                    {
                        ESPNET.Join(join.Port, other);
                        made++;
                    }
                }

                if (report != null)
                {
                    report.Add(source.Label + ": " +
                               string.Join(", ", counts.OrderBy(c => c.Key)
                                   .Select(c => c.Value + " " + c.Key.ToString().ToLowerInvariant())) +
                               "; " + made + " joint end(s) matched within it" +
                               (skipped > 0 ? "; " + skipped + " element(s) could not be read" : string.Empty));
                }
            }

            return parts;
        }

        private static ESPNET.Part PartOf(Element element, Source source)
        {
            if (element.Category == null)
                return null;

            long category = element.Category.Id.ToValue();

            ESPNET.Kind kind;

            if (category == (long)BuiltInCategory.OST_DuctCurves) kind = ESPNET.Kind.Duct;
            else if (category == (long)BuiltInCategory.OST_FlexDuctCurves) kind = ESPNET.Kind.Flex;
            else if (category == (long)BuiltInCategory.OST_DuctFitting) kind = ESPNET.Kind.Fitting;
            else if (category == (long)BuiltInCategory.OST_DuctAccessory) kind = ESPNET.Kind.Accessory;
            else if (category == (long)BuiltInCategory.OST_DuctTerminal) kind = ESPNET.Kind.Terminal;
            else if (category == (long)BuiltInCategory.OST_MechanicalEquipment) kind = ESPNET.Kind.Equipment;
            else return null;

            var part = new ESPNET.Part
            {
                LinkId = source.LinkId,
                ElementId = element.Id.ToValue(),
                Model = source.Label,
                What = kind,
                Family = TypeName(source.Doc, element),
                Mark = MarkOf(element)
            };

            if (kind == ESPNET.Kind.Fitting || kind == ESPNET.Kind.Accessory)
                part.PartType = PartTypeName(element);

            if (kind == ESPNET.Kind.Duct || kind == ESPNET.Kind.Flex)
            {
                // MEPCurveType.Roughness, not a parameter: the parameter
                // was RBS_CURVETYPE_ROUGHNESS_PARAM up to 2023 and
                // DUCT_ROUGHNESS from 2024, while the property is on all
                // six versions under one name
                try
                {
                    var type = source.Doc.GetElement(element.GetTypeId()) as MEPCurveType;

                    if (type != null && type.Roughness > 0)
                        part.Roughness = M(type.Roughness);
                }
                catch
                {
                    // the setting's roughness is used instead
                }

                if (kind == ESPNET.Kind.Flex)
                {
                    Parameter length = element.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);

                    if (length != null && length.HasValue)
                        part.Length = M(length.AsDouble());
                }
            }

            if (kind == ESPNET.Kind.Fitting || kind == ESPNET.Kind.Accessory)
            {
                Parameter coefficient = element.get_Parameter(BuiltInParameter.RBS_LOSS_COEFFICIENT);

                if (coefficient != null && coefficient.HasValue && coefficient.AsDouble() > 0)
                    part.Coefficient = coefficient.AsDouble();
            }

            if (kind == ESPNET.Kind.Terminal)
            {
                Parameter flow = element.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);

                if (flow != null && flow.HasValue && flow.AsDouble() > 0)
                    part.Flow = M3s(flow.AsDouble());
            }

            return part;
        }

        private static void ReadPorts(
            Element element, ESPNET.Part part, Source source,
            Dictionary<string, ESPNET.Port> byConnector,
            List<Pending> pending)
        {
            ConnectorManager manager = QADUCTS.Manager(element);

            if (manager == null)
                return;

            bool readsSystem = part.What == ESPNET.Kind.Equipment || part.What == ESPNET.Kind.Terminal;
            bool readsDrop = part.What == ESPNET.Kind.Accessory || part.What == ESPNET.Kind.Terminal;

            foreach (Connector c in manager.Connectors)
            {
                if (c.Domain != Domain.DomainHvac)
                    continue;

                ConnectorType type = c.ConnectorType;

                if (type != ConnectorType.End && type != ConnectorType.Curve)
                    continue;

                var port = new ESPNET.Port
                {
                    Owner = part,
                    ConnectorId = c.Id,
                    IsCurve = type == ConnectorType.Curve
                };

                XYZ origin = source.ToHost.OfPoint(c.Origin);

                port.X = M(origin.X);
                port.Y = M(origin.Y);
                port.Z = M(origin.Z);

                try
                {
                    XYZ out_ = source.ToHost.OfVector(c.CoordinateSystem.BasisZ);

                    port.DX = out_.X;
                    port.DY = out_.Y;
                    port.DZ = out_.Z;
                }
                catch
                {
                    // a connector with no direction can still be joined
                    // within its model; it just cannot face another one
                }

                Size(c, port);

                if (readsSystem)
                {
                    port.System = SystemOf(c);
                    port.Direction = DirectionOf(c);
                    port.Flow = FlowOf(c);
                }

                if (readsDrop)
                {
                    try
                    {
                        double drop = Pa(c.AssignedPressureDrop);

                        if (drop > part.PressureDrop)
                            part.PressureDrop = drop;
                    }
                    catch
                    {
                        // not a family connector, or none stated
                    }
                }

                byConnector[part.ElementId + ":" + c.Id] = port;

                foreach (Connector other in c.AllRefs)
                {
                    try
                    {
                        Element owner = other.Owner;

                        if (owner == null || owner.Id == element.Id)
                            continue;

                        // A sleeve of insulation is in AllRefs too
                        if (!QADUCTS.Carries(owner) || other.Domain != Domain.DomainHvac)
                            continue;

                        if (other.ConnectorType != ConnectorType.End &&
                            other.ConnectorType != ConnectorType.Curve)
                        {
                            continue;
                        }

                        XYZ at = source.ToHost.OfPoint(other.Origin);

                        pending.Add(new Pending
                        {
                            Port = port,
                            OwnerId = owner.Id.ToValue(),
                            Key = owner.Id.ToValue() + ":" + other.Id,
                            ConnectorId = other.Id,
                            Curve = other.ConnectorType == ConnectorType.Curve,
                            X = M(at.X),
                            Y = M(at.Y),
                            Z = M(at.Z)
                        });
                    }
                    catch
                    {
                    }
                }

                part.Ports.Add(port);
            }

            // A duct's own section, and where each of its connectors sits
            // along it - what orders the taps on its body
            if (part.What == ESPNET.Kind.Duct || part.What == ESPNET.Kind.Flex)
            {
                ESPNET.Port end = part.Ports.FirstOrDefault(p => !p.IsCurve);

                if (end != null)
                {
                    part.Section = end.Section;
                    part.Diameter = end.Diameter;
                    part.Width = end.Width;
                    part.Height = end.Height;
                }

                var curve = element.Location as LocationCurve;

                if (part.What == ESPNET.Kind.Duct && curve != null && curve.Curve != null)
                {
                    XYZ a = source.ToHost.OfPoint(curve.Curve.GetEndPoint(0));
                    XYZ b = source.ToHost.OfPoint(curve.Curve.GetEndPoint(1));

                    double ax = M(a.X), ay = M(a.Y), az = M(a.Z);
                    double dx = M(b.X) - ax, dy = M(b.Y) - ay, dz = M(b.Z) - az;
                    double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    foreach (ESPNET.Port port in part.Ports)
                    {
                        port.Along = length <= 0
                            ? 0
                            : ((port.X - ax) * dx + (port.Y - ay) * dy + (port.Z - az) * dz) / length;
                    }
                }
            }
        }

        // A joint read from one side, waiting for the other side's port
        private class Pending
        {
            public ESPNET.Port Port;
            public long OwnerId;
            public string Key;
            public int ConnectorId;
            public bool Curve;
            public double X, Y, Z;
        }

        // A TAKEOFF JOINED TO A DUCT CONNECTOR THE DUCT DID NOT LIST.
        // The tap's main side sits on the duct's body, on a connector of
        // type Curve; a duct is expected to list those among its own, and
        // ReadPorts takes them when it does. If one were ever missing, the
        // tap - and every terminal past it - would silently drop out of
        // the network and its air out of the totals. So the position the
        // tap gives is made into a tap position on that duct here.
        private static ESPNET.Port TapOnDuct(
            Pending join, Dictionary<long, ESPNET.Part> byElement,
            Dictionary<string, ESPNET.Port> byConnector)
        {
            ESPNET.Part duct;

            if (!join.Curve || !byElement.TryGetValue(join.OwnerId, out duct) ||
                duct.What != ESPNET.Kind.Duct)
            {
                return null;
            }

            List<ESPNET.Port> ends = duct.Ports.Where(p => !p.IsCurve).OrderBy(p => p.Along).ToList();

            if (ends.Count < 2)
                return null;

            ESPNET.Port a = ends.First();
            ESPNET.Port b = ends.Last();

            double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            var port = new ESPNET.Port
            {
                Owner = duct,
                ConnectorId = join.ConnectorId,
                IsCurve = true,
                X = join.X,
                Y = join.Y,
                Z = join.Z,
                Section = duct.Section,
                Diameter = duct.Diameter,
                Width = duct.Width,
                Height = duct.Height,
                Along = length <= 0
                    ? a.Along
                    : a.Along + ((join.X - a.X) * dx + (join.Y - a.Y) * dy + (join.Z - a.Z) * dz) / length
            };

            duct.Ports.Add(port);
            byConnector[join.Key] = port;

            return port;
        }

        private static void Size(Connector c, ESPNET.Port port)
        {
            try
            {
                switch (c.Shape)
                {
                    case ConnectorProfileType.Round:
                        port.Section = ESPNET.Shape.Round;
                        port.Diameter = M(c.Radius * 2);
                        break;

                    case ConnectorProfileType.Rectangular:
                        port.Section = ESPNET.Shape.Rectangular;
                        port.Width = M(c.Width);
                        port.Height = M(c.Height);
                        break;

                    case ConnectorProfileType.Oval:
                        port.Section = ESPNET.Shape.Oval;
                        port.Width = M(c.Width);
                        port.Height = M(c.Height);
                        break;
                }
            }
            catch
            {
                port.Section = ESPNET.Shape.Unknown;
            }
        }

        private static string SystemOf(Connector c)
        {
            try
            {
                switch (c.DuctSystemType)
                {
                    case DuctSystemType.SupplyAir: return "Supply";
                    case DuctSystemType.ReturnAir: return "Return";
                    case DuctSystemType.ExhaustAir: return "Exhaust";
                    case DuctSystemType.OtherAir: return "Other";
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string DirectionOf(Connector c)
        {
            try
            {
                switch (c.Direction)
                {
                    case FlowDirectionType.In: return "In";
                    case FlowDirectionType.Out: return "Out";
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        // The flow the FAMILY states first - an FCU's fresh air connector
        // is preset - and what its own model calculated otherwise
        private static double FlowOf(Connector c)
        {
            try
            {
                double assigned = M3s(c.AssignedFlow);

                if (assigned > 0)
                    return assigned;
            }
            catch
            {
            }

            try
            {
                return Math.Max(0, M3s(c.Flow));
            }
            catch
            {
                return 0;
            }
        }

        private static string TypeName(Document doc, Element element)
        {
            try
            {
                var type = doc.GetElement(element.GetTypeId()) as ElementType;

                if (type == null)
                    return element.Name ?? string.Empty;

                return string.IsNullOrEmpty(type.FamilyName)
                    ? type.Name
                    : type.FamilyName + " : " + type.Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string MarkOf(Element element)
        {
            Parameter mark = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);

            return mark == null ? string.Empty : (mark.AsString() ?? string.Empty);
        }

        // What a fitting or accessory calls itself: MechanicalFitting's
        // PartType (EQUIPWALK reads it the same way), else the family's
        // own Part Type parameter, which accessories carry too - a damper
        // says "Damper" there
        private static string PartTypeName(Element element)
        {
            string name = EQUIPWALK.PartTypeOf(element);

            if (name.Length > 0 && name != "Normal" && name != "Undefined")
                return name;

            try
            {
                var instance = element as FamilyInstance;
                Family family = instance == null || instance.Symbol == null ? null : instance.Symbol.Family;

                Parameter partType = family == null
                    ? null
                    : family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);

                if (partType != null && partType.HasValue)
                {
                    string said = ((PartType)partType.AsInteger()).ToString();

                    if (said != "Normal" && said != "Undefined")
                        return said;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        // ==================================================
        // The unit's ESP parameter: read, and written
        // ==================================================

        private static Document DocOf(Document doc, long linkId)
        {
            if (linkId == 0)
                return doc;

            var link = doc.GetElement(REVITCOMPAT.NewId(linkId)) as RevitLinkInstance;

            return link == null ? null : link.GetLinkDocument();
        }

        // In Pa, or null where the unit has no such parameter or it says
        // nothing that reads as a number
        public static double? InModel(Document doc, long linkId, long elementId, string name)
        {
            try
            {
                Document source = DocOf(doc, linkId);
                Element element = source == null ? null : source.GetElement(REVITCOMPAT.NewId(elementId));
                Parameter p = element == null || string.IsNullOrWhiteSpace(name) ? null : element.LookupParameter(name);

                if (p == null || !p.HasValue)
                    return null;

                switch (p.StorageType)
                {
                    case StorageType.Double:
                        return p.Definition.GetDataType() == SpecTypeId.HvacPressure
                            ? Pa(p.AsDouble())
                            : p.AsDouble();

                    case StorageType.Integer:
                        return p.AsInteger();

                    case StorageType.String:
                        {
                            string text = (p.AsString() ?? string.Empty).Trim();
                            int end = 0;

                            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
                                end++;

                            double value;

                            return end > 0 && double.TryParse(
                                       text.Substring(0, end), NumberStyles.Float,
                                       CultureInfo.InvariantCulture, out value)
                                ? value
                                : (double?)null;
                        }
                }
            }
            catch
            {
            }

            return null;
        }

        // Onto units in THIS model only - a linked model cannot be written
        public static string Write(Document doc, IList<KeyValuePair<string, double>> values, string name)
        {
            int written = 0, linked = 0, missing = 0, readOnly = 0, wrongType = 0;

            using (var t = new Transaction(doc, "ESP - write to units"))
            {
                t.Start();

                foreach (KeyValuePair<string, double> value in values)
                {
                    string[] key = value.Key.Split(':');

                    long linkId, id;

                    if (key.Length != 2 || !long.TryParse(key[0], out linkId) || !long.TryParse(key[1], out id))
                        continue;

                    if (linkId != 0)
                    {
                        linked++;
                        continue;
                    }

                    Element element = doc.GetElement(REVITCOMPAT.NewId(id));
                    Parameter p = element == null ? null : element.LookupParameter(name);

                    if (p == null)
                    {
                        missing++;
                        continue;
                    }

                    if (p.IsReadOnly)
                    {
                        readOnly++;
                        continue;
                    }

                    try
                    {
                        double pa = Math.Round(value.Value, 1);

                        switch (p.StorageType)
                        {
                            case StorageType.Double:
                                {
                                    ForgeTypeId spec = p.Definition.GetDataType();

                                    if (spec == SpecTypeId.HvacPressure)
                                        p.Set(UnitUtils.ConvertToInternalUnits(pa, UnitTypeId.Pascals));
                                    else if (spec == SpecTypeId.Number)
                                        p.Set(pa);
                                    else
                                    {
                                        // a length or a flow would take the
                                        // number and mean something else
                                        wrongType++;
                                        continue;
                                    }
                                }
                                break;

                            case StorageType.Integer:
                                p.Set((int)Math.Round(pa));
                                break;

                            case StorageType.String:
                                p.Set(Math.Round(pa).ToString(CultureInfo.InvariantCulture) + " Pa");
                                break;

                            default:
                                wrongType++;
                                continue;
                        }

                        written++;
                    }
                    catch
                    {
                        readOnly++;
                    }
                }

                t.Commit();
            }

            var said = new List<string> { "Wrote the ESP to " + written + " unit(s)." };

            if (linked > 0) said.Add(linked + " in linked models cannot be written from here.");
            if (missing > 0) said.Add(missing + " have no parameter called '" + name + "'.");
            if (readOnly > 0) said.Add(readOnly + " would not take it (read-only).");
            if (wrongType > 0) said.Add(wrongType + " have it as a type that is not a pressure, a number or text.");

            return string.Join(" ", said);
        }
    }
}
