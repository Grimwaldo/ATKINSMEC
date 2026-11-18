using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // INSULATOR: adds insulation to every duct and pipe, their fittings
    // and - when ticked - their accessories, as they are drawn. Being an
    // updater it runs inside the transaction that created the element, so
    // the insulation appears with it and undoes with it in one step.
    //
    // MUST NEVER THROW: that would abort the user's own drawing
    // transaction. Everything is swallowed, element by element.
    public class INSULATIONUPDATER : IUpdater
    {
        private readonly UpdaterId _updaterId;

        // Fixed identity, the one DUCT INSULATION registered: the same
        // updater across sessions and versions
        private static readonly Guid UpdaterGuid =
            new Guid("6C1A6E32-4F1D-4C0B-9C33-6A3B5B5B2E71");

        public INSULATIONUPDATER(AddInId addInId)
        {
            _updaterId = new UpdaterId(addInId, UpdaterGuid);
        }

        public UpdaterId GetUpdaterId()
        {
            return _updaterId;
        }

        public string GetUpdaterName()
        {
            return "Atkins MEP Toolkit - Insulator";
        }

        public string GetAdditionalInformation()
        {
            return "Insulates new ducts, pipes and their fittings (and " +
                   "accessories when ticked) using the thickness set for " +
                   "their system type in the INSULATOR window " +
                   "(Atkins MEP Toolkit).";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.MEPSystems;
        }

        // What an added element is, for the insulator
        private enum Part { None, Run, Accessory }

        public void Execute(UpdaterData data)
        {
            try
            {
                INSULATIONSETTINGS.Ducts.Load();
                INSULATIONSETTINGS.Pipes.Load();

                if (!INSULATIONSETTINGS.Ducts.Enabled &&
                    !INSULATIONSETTINGS.Pipes.Enabled)
                    return;

                Document doc = data.GetDocument();

                // Insulation types resolved once per run, not per element
                var ductTypes = new Dictionary<string, ElementId>(
                    StringComparer.OrdinalIgnoreCase);

                var pipeTypes = new Dictionary<string, ElementId>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (ElementId id in data.GetAddedElementIds())
                {
                    Insulate(doc, id, ductTypes, pipeTypes);
                }
            }
            catch
            {
                // An updater must never throw: that would abort the
                // user's own drawing transaction.
            }
        }

        private static void Insulate(
            Document doc,
            ElementId id,
            Dictionary<string, ElementId> ductTypes,
            Dictionary<string, ElementId> pipeTypes)
        {
            try
            {
                Element element = doc.GetElement(id);

                if (element == null || element.Category == null)
                    return;

                bool pipes;
                Part part = PartOf(element.Category.Id.ToValue(), out pipes);

                if (part == Part.None)
                    return;

                INSULATIONSETTINGS settings = pipes
                    ? INSULATIONSETTINGS.Pipes
                    : INSULATIONSETTINGS.Ducts;

                if (!settings.Covers(part == Part.Accessory))
                    return;

                // Never insulate twice
                ICollection<ElementId> existing =
                    InsulationLiningBase.GetInsulationIds(doc, id);

                if (existing != null && existing.Count > 0)
                    return;

                string systemType = SystemTypeName(doc, element, pipes);

                INSULATIONSETTINGS.Rule rule = settings.RuleFor(systemType);

                if (rule == null || rule.ThicknessMm <= 0)
                    return;

                double thickness = UnitUtils.ConvertToInternalUnits(
                    rule.ThicknessMm,
                    UnitTypeId.Millimeters);

                if (!InsulationLiningBase.IsValidThickness(thickness))
                    return;

                string typeName = settings.TypeNameFor(systemType);

                Dictionary<string, ElementId> cache = pipes ? pipeTypes : ductTypes;

                ElementId insulationTypeId;

                if (!cache.TryGetValue(typeName, out insulationTypeId))
                {
                    insulationTypeId = FindInsulationType(doc, typeName, pipes);
                    cache[typeName] = insulationTypeId;
                }

                // Never InvalidElementId into Create: that means "the
                // document's default type", which nobody chose
                if (insulationTypeId == ElementId.InvalidElementId)
                    return;

                if (pipes)
                    PipeInsulation.Create(doc, id, insulationTypeId, thickness);
                else
                    DuctInsulation.Create(doc, id, insulationTypeId, thickness);
            }
            catch
            {
                // one element failing must not stop the others
            }
        }

        private static readonly BuiltInCategory[] DuctRuns =
        {
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_DuctFitting
        };

        private static readonly BuiltInCategory[] PipeRuns =
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeFitting
        };

        // Every category the updater is triggered by; accessories are
        // always watched and left alone in Execute when not ticked, so
        // ticking them needs no restart
        private static IEnumerable<BuiltInCategory> Watched()
        {
            return DuctRuns
                .Concat(PipeRuns)
                .Concat(new[]
                {
                    BuiltInCategory.OST_DuctAccessory,
                    BuiltInCategory.OST_PipeAccessory
                });
        }

        private static Part PartOf(long category, out bool pipes)
        {
            pipes = false;

            if (DuctRuns.Any(c => (long)c == category))
                return Part.Run;

            if (category == (long)BuiltInCategory.OST_DuctAccessory)
                return Part.Accessory;

            pipes = true;

            if (PipeRuns.Any(c => (long)c == category))
                return Part.Run;

            if (category == (long)BuiltInCategory.OST_PipeAccessory)
                return Part.Accessory;

            pipes = false;

            return Part.None;
        }

        public static string SystemTypeName(Document doc, Element element, bool pipes)
        {
            // A duct or pipe carries the system type directly
            string name = SystemTypeFromParameter(doc, element, pipes);

            if (!string.IsNullOrEmpty(name))
                return name;

            // A fitting or accessory drawn before its system is assigned
            // takes it from its connectors, or from what it joins
            ConnectorManager manager = GetConnectorManager(element);

            if (manager == null)
                return string.Empty;

            Domain domain = pipes ? Domain.DomainPiping : Domain.DomainHvac;

            foreach (Connector connector in manager.Connectors)
            {
                try
                {
                    if (connector.Domain != domain)
                        continue;

                    MEPSystem system = connector.MEPSystem;

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

                    foreach (Connector reference in connector.AllRefs)
                    {
                        Element owner = reference.Owner;

                        if (owner == null || owner.Id == element.Id)
                            continue;

                        // Insulation and lining sit in AllRefs too
                        if (owner is InsulationLiningBase)
                            continue;

                        string neighbour =
                            SystemTypeFromParameter(doc, owner, pipes);

                        if (!string.IsNullOrEmpty(neighbour))
                            return neighbour;
                    }
                }
                catch
                {
                    // an unconnected connector, carry on
                }
            }

            return string.Empty;
        }

        private static string SystemTypeFromParameter(
            Document doc,
            Element element,
            bool pipes)
        {
            Parameter parameter =
                element.get_Parameter(pipes
                    ? BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM
                    : BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);

            if (parameter == null)
                return string.Empty;

            if (parameter.StorageType == StorageType.ElementId)
            {
                ElementId id = parameter.AsElementId();

                if (id != ElementId.InvalidElementId)
                {
                    Element systemType = doc.GetElement(id);

                    if (systemType != null)
                        return systemType.Name;
                }
            }

            string value = parameter.AsValueString();

            return value ?? string.Empty;
        }

        private static ConnectorManager GetConnectorManager(Element element)
        {
            MEPCurve curve = element as MEPCurve;

            if (curve != null)
                return curve.ConnectorManager;

            FamilyInstance instance = element as FamilyInstance;

            if (instance != null && instance.MEPModel != null)
                return instance.MEPModel.ConnectorManager;

            return null;
        }

        // Only the type named for that system type is used. Guessing a
        // type would risk insulating with the wrong material, so an
        // unnamed or missing type means the element is left alone.
        public static ElementId FindInsulationType(
            Document doc,
            string typeName,
            bool pipes)
        {
            if (string.IsNullOrEmpty(typeName))
                return ElementId.InvalidElementId;

            Element named =
                new FilteredElementCollector(doc)
                .OfClass(pipes ? typeof(PipeInsulationType) : typeof(DuctInsulationType))
                .FirstOrDefault(t => string.Equals(
                    t.Name,
                    typeName,
                    StringComparison.OrdinalIgnoreCase));

            return named == null
                ? ElementId.InvalidElementId
                : named.Id;
        }

        // ==================================================
        // Registration, done once when Revit starts
        // ==================================================

        private static INSULATIONUPDATER _instance;

        public static void Register(AddInId addInId)
        {
            try
            {
                _instance = new INSULATIONUPDATER(addInId);

                if (UpdaterRegistry.IsUpdaterRegistered(_instance.GetUpdaterId()))
                    return;

                UpdaterRegistry.RegisterUpdater(_instance, true);

                UpdaterRegistry.AddTrigger(
                    _instance.GetUpdaterId(),
                    new ElementMulticategoryFilter(
                        new List<BuiltInCategory>(Watched())),
                    Element.GetChangeTypeElementAddition());
            }
            catch
            {
                // without the updater the add-in still opens, it just
                // will not insulate automatically
            }
        }

        public static void Unregister()
        {
            try
            {
                if (_instance == null)
                    return;

                if (UpdaterRegistry.IsUpdaterRegistered(_instance.GetUpdaterId()))
                    UpdaterRegistry.UnregisterUpdater(_instance.GetUpdaterId());
            }
            catch
            {
                // shutting down anyway
            }
        }
    }
}
