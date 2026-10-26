using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // Water volume held by two chosen piping system types - one taken
    // as the chilled water supply, one as the return - across the
    // current model and any linked models.
    public class CHWVOLUME
    {
        public class ModelResult
        {
            public string ModelName { get; set; }
            public bool IsHost { get; set; }

            // Set when an ACC model could not be opened
            public string Failure { get; set; }

            // The two types this model was measured against, and a note
            // when only part of the model was loaded
            public string SupplyTypeName { get; set; }
            public string ReturnTypeName { get; set; }
            public string WorksetsNote { get; set; }

            public int SupplyPipes { get; set; }
            public int ReturnPipes { get; set; }

            public double SupplyLitres { get; set; }
            public double ReturnLitres { get; set; }

            public int SupplyFittings { get; set; }
            public int ReturnFittings { get; set; }

            public double SupplyFittingLitres { get; set; }
            public double ReturnFittingLitres { get; set; }

            public int TotalFittings
            {
                get { return SupplyFittings + ReturnFittings; }
            }

            public double PipeLitres
            {
                get { return SupplyLitres + ReturnLitres; }
            }

            public double FittingLitres
            {
                get { return SupplyFittingLitres + ReturnFittingLitres; }
            }

            // Pipes or fittings whose size could not be read
            public int SkippedNoSize { get; set; }

            public double TotalLitres
            {
                get { return PipeLitres + FittingLitres; }
            }

            public int TotalPipes
            {
                get { return SupplyPipes + ReturnPipes; }
            }
        }

        public class Result
        {
            public List<ModelResult> Models { get; set; }

            public double SupplyLitres { get; set; }
            public double ReturnLitres { get; set; }

            public int SupplyPipes { get; set; }
            public int ReturnPipes { get; set; }

            public int SupplyFittings { get; set; }
            public int ReturnFittings { get; set; }

            public double SupplyFittingLitres { get; set; }
            public double ReturnFittingLitres { get; set; }

            public int SkippedNoSize { get; set; }

            // Set when the user pressed Stop part way through
            public bool Stopped { get; set; }
            public int NotMeasured { get; set; }

            public int TotalFittings
            {
                get { return SupplyFittings + ReturnFittings; }
            }

            public double PipeLitres
            {
                get { return SupplyLitres + ReturnLitres; }
            }

            public double FittingLitres
            {
                get { return SupplyFittingLitres + ReturnFittingLitres; }
            }

            public double TotalLitres
            {
                get { return PipeLitres + FittingLitres; }
            }

            public double TotalCubicMetres
            {
                get { return TotalLitres / 1000.0; }
            }

            public int TotalPipes
            {
                get { return SupplyPipes + ReturnPipes; }
            }
        }

        public class ModelItem
        {
            public string Name { get; set; }
            public bool IsHost { get; set; }
            public long LinkInstanceId { get; set; }

            // Where it sits in the list (user, 2026-10-19): 0 the open
            // model, 1 a link placed in it, 2 and on a link nested in that
            // placement - and the placement it is under (0: the open model)
            public int Depth { get; set; }
            public long ParentInstanceId { get; set; }
            public string ParentName { get; set; }

            // Another placement of a model listed above it: offered, not
            // ticked - its pipes are the same pipes again
            public bool Repeat { get; set; }

            // Chosen per model: two models can name their chilled water
            // systems differently, so one pair for all of them will not
            // do. For an ACC model these live on the Cloud entry so they
            // survive between sessions.
            private string _supplyType = string.Empty;
            private string _returnType = string.Empty;

            public string SupplyType
            {
                get { return IsCloud ? (Cloud.SupplyType ?? string.Empty) : _supplyType; }
                set
                {
                    if (IsCloud)
                        Cloud.SupplyType = value;
                    else
                        _supplyType = value ?? string.Empty;
                }
            }

            public string ReturnType
            {
                get { return IsCloud ? (Cloud.ReturnType ?? string.Empty) : _returnType; }
                set
                {
                    if (IsCloud)
                        Cloud.ReturnType = value;
                    else
                        _returnType = value ?? string.Empty;
                }
            }

            // Only an ACC model is opened by the add-in, so only it has
            // a workset choice to make
            public CHWCLOUD.WorksetMode Worksets
            {
                get
                {
                    return IsCloud
                        ? Cloud.Worksets
                        : CHWCLOUD.WorksetMode.All;
                }
                set
                {
                    if (IsCloud)
                        Cloud.Worksets = value;
                }
            }

            // An ACC model measured without ever being opened in the
            // Revit window; null for the host model and its links
            public CHWCLOUD.CloudModel Cloud { get; set; }

            public bool IsCloud
            {
                get { return Cloud != null; }
            }

            public override string ToString()
            {
                if (IsCloud)
                    return Cloud.Name + "   (ACC, not opened)";

                return IsHost ? Name + "   (current model)" : Name;
            }
        }

        // THE CURRENT MODEL, EVERY PLACEMENT OF A LINK BELOW IT, AND EVERY
        // NESTED LINK BELOW THE PLACEMENT IT IS IN (user, 2026-10-19: "list
        // the linked instances of main model below it and the linked
        // instances (nested links) of linked models"). Until then one row
        // per linked model, its other placements folded in.
        //
        // A model placed more than once is listed once per placement, the
        // first ticked and the rest Repeat (offered unticked) - so the
        // total is what it was unless a person ticks a repeat in.
        public static List<ModelItem> GetModels(Document hostDoc)
        {
            var items = new List<ModelItem>();

            // No model open: only saved ACC models can be measured, and
            // the form adds those itself
            if (hostDoc == null)
                return items;

            items.Add(new ModelItem
            {
                Name = hostDoc.Title,
                IsHost = true,
                LinkInstanceId = 0
            });

            List<RevitLinkInstance> links =
                new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(l => l.GetLinkDocument() != null)
                .ToList();

            // Each placement's name - the model, and which placement when
            // there is more than one
            Dictionary<long, string> labels = RVTLINKSELECTION.LinkLabels(hostDoc);

            string LabelOf(RevitLinkInstance link)
            {
                string label;

                return labels.TryGetValue(link.Id.ToValue(), out label)
                    ? label
                    : link.GetLinkDocument().Title;
            }

            // The order, the nesting and the repeats: CHWTREE (Revit-free)
            List<CHWTREE.Line> lines = CHWTREE.Order(
                links.Select(l => new CHWTREE.Placement
                {
                    Id = l.Id.ToValue(),
                    Parent = ParentPlacement(hostDoc, l, links),
                    Label = LabelOf(l),
                    Model = DocKey(l.GetLinkDocument())
                }),
                hostDoc.Title);

            foreach (CHWTREE.Line line in lines)
            {
                items.Add(new ModelItem
                {
                    Name = line.Label,
                    IsHost = false,
                    LinkInstanceId = line.Id,
                    Depth = line.Depth,
                    ParentInstanceId = line.Parent,
                    ParentName = line.ParentName,
                    Repeat = line.Repeat
                });
            }

            return items;
        }

        // One model, however many times it is placed
        private static string DocKey(Document doc)
        {
            string path = doc.PathName;

            return string.IsNullOrEmpty(path) ? doc.Title : path;
        }

        // THE PLACEMENT A NESTED LINK IS IN - 0 for a link placed in the open
        // model itself. The nesting is known by TYPE (RevitLinkType.
        // GetParentId); with the parent model placed more than once, the
        // placement whose origin is nearest - a nested link sits with its
        // own parent, as a tower's links with their tower.
        private static long ParentPlacement(Document hostDoc, RevitLinkInstance link, List<RevitLinkInstance> links)
        {
            try
            {
                var type = hostDoc.GetElement(link.GetTypeId()) as RevitLinkType;

                if (type == null || !type.IsNestedLink)
                    return 0;

                ElementId parentType = type.GetParentId();

                List<RevitLinkInstance> parents = links
                    .Where(l => l.Id != link.Id && l.GetTypeId() == parentType)
                    .ToList();

                double[] P(RevitLinkInstance l)
                {
                    XYZ o = l.GetTotalTransform().Origin;
                    return new[] { o.X, o.Y, o.Z };
                }

                return CHWTREE.Nearest(
                    parents.Select(p => new KeyValuePair<long, double[]>(p.Id.ToValue(), P(p))),
                    P(link));
            }
            catch
            {
                return 0;
            }
        }

        // Every piping system type found in the given models, by name
        public static List<string> GetSystemTypeNames(
            Document hostDoc,
            IEnumerable<ModelItem> models)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ModelItem item in models)
            {
                // A closed ACC model cannot be read, so its names come
                // from the last time it was opened
                if (item.IsCloud)
                {
                    foreach (string remembered in item.Cloud.SystemTypes)
                    {
                        if (!string.IsNullOrEmpty(remembered) &&
                            seen.Add(remembered))
                            names.Add(remembered);
                    }

                    continue;
                }

                Document doc = DocumentFor(hostDoc, item);

                if (doc == null)
                    continue;

                foreach (string name in SystemTypeNamesIn(doc))
                {
                    if (seen.Add(name))
                        names.Add(name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);

            return names;
        }

        // Only the system types actually carried by pipework in this
        // model. Listing every PipingSystemType instead would show the
        // whole template - dozens of names, most of them never drawn -
        // and bury the two that matter.
        public static List<string> SystemTypeNamesIn(Document doc)
        {
            var names = new List<string>();

            if (doc == null)
                return names;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_FlexPipeCurves
            };

            var pipes =
                new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(categories))
                .WhereElementIsNotElementType()
                .OfClass(typeof(MEPCurve))
                .Cast<MEPCurve>();

            foreach (MEPCurve pipe in pipes)
                Remember(names, seen, SystemTypeNameOf(pipe, doc));

            // Fittings too: a type could in principle be worn only by
            // fittings, and it costs one more pass to be sure
            var fittings =
                new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeFitting)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();

            foreach (FamilyInstance fitting in fittings)
            {
                if (fitting.MEPModel == null)
                    continue;

                List<Connector> connectors = ConnectorsOf(fitting);

                if (connectors.Count == 0)
                    continue;

                Remember(names, seen,
                    FittingSystemTypeName(fitting, doc, connectors));
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);

            return names;
        }

        private static void Remember(
            List<string> names, HashSet<string> seen, string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string trimmed = name.Trim();

            if (trimmed.Length > 0 && seen.Add(trimmed))
                names.Add(trimmed);
        }

        // Opens each ACC model just long enough to list its piping system
        // types, then closes it. Nothing can read them from a closed
        // model, so this is the one-off price of filling the dropdowns;
        // afterwards the names are remembered on the model entry.
        public static int ReadCloudSystemTypes(
            Application app,
            IEnumerable<ModelItem> models,
            CHWSTOP stop,
            out string errors)
        {
            var failures = new StringBuilder();
            int read = 0;

            foreach (ModelItem item in models)
            {
                if (!item.IsCloud || app == null)
                    continue;

                if (stop != null)
                {
                    stop.Breathe();

                    if (stop.Stopped)
                        break;
                }

                Document doc = null;

                try
                {
                    doc = CHWCLOUD.Open(app, item.Cloud, stop);

                    item.Cloud.SystemTypes = SystemTypeNamesIn(doc);

                    read++;
                }
                catch (Exception ex)
                {
                    failures.AppendLine(
                        item.Cloud.Name + ": " + ex.Message);
                }
                finally
                {
                    if (doc != null)
                    {
                        try
                        {
                            doc.Close(false);
                        }
                        catch
                        {
                            // nothing useful to do about it
                        }
                    }
                }
            }

            errors = failures.ToString();

            return read;
        }

        private static Document DocumentFor(Document hostDoc, ModelItem item)
        {
            if (hostDoc == null)
                return null;

            if (item.IsHost)
                return hostDoc;

            RevitLinkInstance link =
                hostDoc.GetElement(REVITCOMPAT.NewId(item.LinkInstanceId))
                as RevitLinkInstance;

            return link == null ? null : link.GetLinkDocument();
        }

        // The two system types are no longer arguments: each model
        // carries its own pair
        // The stop signal is checked before each model. A model already
        // being opened can only be abandoned if Revit allows that stage
        // to be cancelled, so the reliable check sits between models.
        public static Result Calculate(
            Document hostDoc,
            Application app,
            IEnumerable<ModelItem> models,
            CHWSTOP stop)
        {
            var result = new Result
            {
                Models = new List<ModelResult>()
            };

            var queue = new List<ModelItem>(models);

            // A model ticked in two placements is measured once and counted
            // twice - its pipes walked again would give the same figure
            var measured = new Dictionary<string, ModelResult>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < queue.Count; i++)
            {
                if (stop != null)
                {
                    // Let the window repaint and the Stop button work
                    stop.Breathe();

                    if (stop.Stopped)
                    {
                        result.Stopped = true;
                        result.NotMeasured = queue.Count - i;

                        break;
                    }
                }

                ModelItem item = queue[i];

                Document doc;
                bool opened = false;

                if (item.IsCloud)
                {
                    if (app == null)
                        continue;

                    try
                    {
                        doc = CHWCLOUD.Open(app, item.Cloud, stop);
                        opened = doc != null;

                        // The model is open anyway, so the type names
                        // cost nothing to refresh here
                        if (opened)
                            item.Cloud.SystemTypes = SystemTypeNamesIn(doc);
                    }
                    catch (Exception ex)
                    {
                        result.Models.Add(new ModelResult
                        {
                            ModelName = item.Cloud.Name,
                            SupplyTypeName = item.SupplyType,
                            ReturnTypeName = item.ReturnType,
                            Failure = ex.Message
                        });

                        continue;
                    }
                }
                else
                {
                    doc = DocumentFor(hostDoc, item);
                }

                if (doc == null)
                    continue;

                ModelResult modelResult;

                try
                {
                    string again = item.IsCloud
                        ? null
                        : DocKey(doc) + "\n" + item.SupplyType + "\n" + item.ReturnType;

                    ModelResult before;

                    // Each model is measured against its own two types
                    modelResult = again != null && measured.TryGetValue(again, out before)
                        ? Again(before, item)
                        : Measure(
                            doc,
                            item,
                            item.SupplyType,
                            item.ReturnType);

                    if (again != null && !measured.ContainsKey(again))
                        measured[again] = modelResult;

                    modelResult.SupplyTypeName = item.SupplyType;
                    modelResult.ReturnTypeName = item.ReturnType;

                    if (item.IsCloud &&
                        item.Worksets == CHWCLOUD.WorksetMode.Editable)
                        modelResult.WorksetsNote = "editable worksets only";
                }
                finally
                {
                    // Background documents are read only and closed
                    // again without saving
                    if (opened)
                    {
                        try
                        {
                            doc.Close(false);
                        }
                        catch
                        {
                            // nothing useful to do about it
                        }
                    }
                }

                result.Models.Add(modelResult);

                result.SupplyLitres += modelResult.SupplyLitres;
                result.ReturnLitres += modelResult.ReturnLitres;
                result.SupplyPipes += modelResult.SupplyPipes;
                result.ReturnPipes += modelResult.ReturnPipes;
                result.SupplyFittings += modelResult.SupplyFittings;
                result.ReturnFittings += modelResult.ReturnFittings;
                result.SupplyFittingLitres += modelResult.SupplyFittingLitres;
                result.ReturnFittingLitres += modelResult.ReturnFittingLitres;
                result.SkippedNoSize += modelResult.SkippedNoSize;
            }

            return result;
        }

        // The same model's figures under another placement's name
        private static ModelResult Again(ModelResult before, ModelItem item)
        {
            return new ModelResult
            {
                ModelName = item.Depth >= 2 && !string.IsNullOrEmpty(item.ParentName)
                    ? item.Name + "  (in " + item.ParentName + ")"
                    : item.Name,
                IsHost = item.IsHost,
                SupplyPipes = before.SupplyPipes,
                ReturnPipes = before.ReturnPipes,
                SupplyLitres = before.SupplyLitres,
                ReturnLitres = before.ReturnLitres,
                SupplyFittings = before.SupplyFittings,
                ReturnFittings = before.ReturnFittings,
                SupplyFittingLitres = before.SupplyFittingLitres,
                ReturnFittingLitres = before.ReturnFittingLitres,
                SkippedNoSize = before.SkippedNoSize
            };
        }

        private static ModelResult Measure(
            Document doc,
            ModelItem item,
            string supplyTypeName,
            string returnTypeName)
        {
            // A nested link says which placement it is in
            var modelResult = new ModelResult
            {
                ModelName = item.Depth >= 2 && !string.IsNullOrEmpty(item.ParentName)
                    ? item.Name + "  (in " + item.ParentName + ")"
                    : item.Name,
                IsHost = item.IsHost
            };

            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_FlexPipeCurves
            };

            var pipes =
                new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(categories))
                .WhereElementIsNotElementType()
                .OfClass(typeof(MEPCurve))
                .Cast<MEPCurve>();

            foreach (MEPCurve pipe in pipes)
            {
                string systemType = SystemTypeNameOf(pipe, doc);

                bool isSupply = Same(systemType, supplyTypeName);
                bool isReturn = Same(systemType, returnTypeName);

                if (!isSupply && !isReturn)
                    continue;

                double litres = VolumeLitres(pipe);

                if (litres <= 0)
                {
                    modelResult.SkippedNoSize++;
                    continue;
                }

                if (isSupply)
                {
                    modelResult.SupplyPipes++;
                    modelResult.SupplyLitres += litres;
                }
                else
                {
                    modelResult.ReturnPipes++;
                    modelResult.ReturnLitres += litres;
                }
            }

            MeasureFittings(doc, modelResult, supplyTypeName, returnTypeName);

            return modelResult;
        }

        // ==================================================
        // Fittings
        //
        // A fitting has no length to work from, so its water content is
        // taken as the sum of its legs: from the middle of the fitting
        // out to each connector, using that connector's own bore. For an
        // elbow or tee that is the wetted path through it. It is an
        // approximation - the legs overlap slightly at the centre, so a
        // sharp bend reads a little high.
        // ==================================================

        private static void MeasureFittings(
            Document doc,
            ModelResult modelResult,
            string supplyTypeName,
            string returnTypeName)
        {
            var fittings =
                new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeFitting)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();

            foreach (FamilyInstance fitting in fittings)
            {
                List<Connector> connectors = ConnectorsOf(fitting);

                if (connectors.Count == 0)
                    continue;

                string systemType =
                    FittingSystemTypeName(fitting, doc, connectors);

                bool isSupply = Same(systemType, supplyTypeName);
                bool isReturn = Same(systemType, returnTypeName);

                if (!isSupply && !isReturn)
                    continue;

                double litres = FittingLitres(connectors);

                if (litres <= 0)
                {
                    modelResult.SkippedNoSize++;
                    continue;
                }

                if (isSupply)
                {
                    modelResult.SupplyFittings++;
                    modelResult.SupplyFittingLitres += litres;
                }
                else
                {
                    modelResult.ReturnFittings++;
                    modelResult.ReturnFittingLitres += litres;
                }
            }
        }

        private static double FittingLitres(List<Connector> connectors)
        {
            // Middle of the fitting, as the average of its connectors
            double x = 0;
            double y = 0;
            double z = 0;

            foreach (Connector c in connectors)
            {
                x += c.Origin.X;
                y += c.Origin.Y;
                z += c.Origin.Z;
            }

            var centre = new XYZ(
                x / connectors.Count,
                y / connectors.Count,
                z / connectors.Count);

            double cubicFeet = 0;

            foreach (Connector c in connectors)
            {
                double area = ConnectorArea(c);

                if (area <= 0)
                    continue;

                cubicFeet += area * centre.DistanceTo(c.Origin);
            }

            if (cubicFeet <= 0)
                return 0;

            return UnitUtils.ConvertFromInternalUnits(
                cubicFeet,
                UnitTypeId.Liters);
        }

        private static double ConnectorArea(Connector connector)
        {
            try
            {
                if (connector.Shape == ConnectorProfileType.Round)
                    return Math.PI * connector.Radius * connector.Radius;

                return connector.Width * connector.Height;
            }
            catch
            {
                return 0;
            }
        }

        // The piping connectors of a fitting, empty when it has none
        private static List<Connector> ConnectorsOf(FamilyInstance fitting)
        {
            var connectors = new List<Connector>();

            if (fitting.MEPModel == null ||
                fitting.MEPModel.ConnectorManager == null)
                return connectors;

            foreach (Connector c in
                     fitting.MEPModel.ConnectorManager.Connectors)
            {
                if (c.Domain == Domain.DomainPiping)
                    connectors.Add(c);
            }

            return connectors;
        }

        private static string FittingSystemTypeName(
            FamilyInstance fitting,
            Document doc,
            List<Connector> connectors)
        {
            // The system the fitting sits on is the authority
            foreach (Connector c in connectors)
            {
                try
                {
                    MEPSystem system = c.MEPSystem;

                    if (system == null)
                        continue;

                    ElementId typeId = system.GetTypeId();

                    if (typeId == ElementId.InvalidElementId)
                        continue;

                    Element systemType = doc.GetElement(typeId);

                    if (systemType != null)
                        return systemType.Name;
                }
                catch
                {
                    // an unconnected connector, try the next
                }
            }

            return ParameterText(
                fitting,
                BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
        }

        private static bool Same(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;

            return string.Equals(a.Trim(), b.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        // The name of the piping system type a pipe belongs to
        private static string SystemTypeNameOf(MEPCurve pipe, Document doc)
        {
            MEPSystem system = pipe.MEPSystem;

            if (system != null)
            {
                ElementId typeId = system.GetTypeId();

                if (typeId != ElementId.InvalidElementId)
                {
                    Element systemType = doc.GetElement(typeId);

                    if (systemType != null)
                        return systemType.Name;
                }
            }

            // A pipe not joined into a system still names its type
            return ParameterText(
                pipe,
                BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
        }

        // Water volume of the bore: pi * r^2 * length
        private static double VolumeLitres(MEPCurve pipe)
        {
            double diameter = ParameterValue(
                pipe,
                BuiltInParameter.RBS_PIPE_INNER_DIAM_PARAM);

            if (diameter <= 0)
            {
                // Flex pipes may only publish the nominal size
                diameter = ParameterValue(
                    pipe,
                    BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            }

            if (diameter <= 0)
                return 0;

            double length = ParameterValue(
                pipe,
                BuiltInParameter.CURVE_ELEM_LENGTH);

            if (length <= 0)
                return 0;

            double radius = diameter / 2.0;

            double cubicFeet = Math.PI * radius * radius * length;

            return UnitUtils.ConvertFromInternalUnits(
                cubicFeet,
                UnitTypeId.Liters);
        }

        private static string ParameterText(
            Element element,
            BuiltInParameter builtInParameter)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);

            if (parameter == null)
                return string.Empty;

            string value = parameter.AsString();

            if (string.IsNullOrEmpty(value))
                value = parameter.AsValueString();

            return value ?? string.Empty;
        }

        private static double ParameterValue(
            Element element,
            BuiltInParameter builtInParameter)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);

            if (parameter == null ||
                parameter.StorageType != StorageType.Double)
                return 0;

            return parameter.AsDouble();
        }
    }
}
