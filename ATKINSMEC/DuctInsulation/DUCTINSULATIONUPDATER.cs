using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // Adds insulation to every duct, duct fitting and duct accessory as
    // it is drawn. Being an updater it runs inside the transaction that
    // created the element, so the insulation appears with it and undoes
    // with it in one step.
    public class DUCTINSULATIONUPDATER : IUpdater
    {
        private readonly UpdaterId _updaterId;

        // Fixed identity: the same updater across sessions
        private static readonly Guid UpdaterGuid =
            new Guid("6C1A6E32-4F1D-4C0B-9C33-6A3B5B5B2E71");

        public DUCTINSULATIONUPDATER(AddInId addInId)
        {
            _updaterId = new UpdaterId(addInId, UpdaterGuid);
        }

        public UpdaterId GetUpdaterId()
        {
            return _updaterId;
        }

        public string GetUpdaterName()
        {
            return "Atkins MEP Toolkit - duct insulation";
        }

        public string GetAdditionalInformation()
        {
            return "Insulates new ducts, duct fittings and duct accessories " +
                   "using the thickness set for their system type in the " +
                   "DUCT INSULATION window (Atkins MEP Toolkit).";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.MEPSystems;
        }

        public void Execute(UpdaterData data)
        {
            try
            {
                INSULATIONSETTINGS.Load();

                if (!INSULATIONSETTINGS.Enabled)
                    return;

                Document doc = data.GetDocument();

                // Insulation types resolved once per run, not per element
                var typeCache = new Dictionary<string, ElementId>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (ElementId id in data.GetAddedElementIds())
                {
                    Insulate(doc, id, typeCache);
                }
            }
            catch
            {
                // An updater must never throw: that would abort the
                // user's own duct-drawing transaction.
            }
        }

        private static void Insulate(
            Document doc,
            ElementId ductId,
            Dictionary<string, ElementId> typeCache)
        {
            try
            {
                Element element = doc.GetElement(ductId);

                if (element == null || !IsInsulatable(element))
                    return;

                // Never insulate twice
                ICollection<ElementId> existing =
                    InsulationLiningBase.GetInsulationIds(doc, ductId);

                if (existing != null && existing.Count > 0)
                    return;

                string systemType = SystemTypeName(doc, element);

                INSULATIONSETTINGS.Rule rule =
                    INSULATIONSETTINGS.RuleFor(systemType);

                if (rule == null || rule.ThicknessMm <= 0)
                    return;

                double thickness = UnitUtils.ConvertToInternalUnits(
                    rule.ThicknessMm,
                    UnitTypeId.Millimeters);

                if (!InsulationLiningBase.IsValidThickness(thickness))
                    return;

                // The type set for this system type, or the default
                string typeName =
                    INSULATIONSETTINGS.TypeNameFor(systemType) ?? string.Empty;

                ElementId insulationTypeId;

                if (!typeCache.TryGetValue(typeName, out insulationTypeId))
                {
                    insulationTypeId = FindInsulationType(doc, typeName);
                    typeCache[typeName] = insulationTypeId;
                }

                if (insulationTypeId == ElementId.InvalidElementId)
                    return;

                DuctInsulation.Create(
                    doc,
                    ductId,
                    insulationTypeId,
                    thickness);
            }
            catch
            {
                // one duct failing must not stop the others
            }
        }

        // Ducts, duct fittings and duct accessories
        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctAccessory
        };

        private static bool IsInsulatable(Element element)
        {
            if (element.Category == null)
                return false;

            long id = element.Category.Id.ToValue();

            foreach (BuiltInCategory category in Categories)
            {
                if (id == (long)category)
                    return true;
            }

            return false;
        }

        public static string SystemTypeName(Document doc, Element element)
        {
            // A duct carries the system type directly
            string name = SystemTypeFromParameter(doc, element);

            if (!string.IsNullOrEmpty(name))
                return name;

            // A fitting or accessory drawn before its system is assigned
            // takes it from its connectors, or from the duct it joins
            ConnectorManager manager = GetConnectorManager(element);

            if (manager == null)
                return string.Empty;

            foreach (Connector connector in manager.Connectors)
            {
                if (connector.Domain != Domain.DomainHvac)
                    continue;

                try
                {
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

                        string neighbour =
                            SystemTypeFromParameter(doc, owner);

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
            Element element)
        {
            Parameter parameter =
                element.get_Parameter(
                    BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);

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
            string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return ElementId.InvalidElementId;

            DuctInsulationType named =
                new FilteredElementCollector(doc)
                .OfClass(typeof(DuctInsulationType))
                .Cast<DuctInsulationType>()
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

        private static DUCTINSULATIONUPDATER _instance;

        public static void Register(AddInId addInId)
        {
            try
            {
                _instance = new DUCTINSULATIONUPDATER(addInId);

                if (UpdaterRegistry.IsUpdaterRegistered(_instance.GetUpdaterId()))
                    return;

                UpdaterRegistry.RegisterUpdater(_instance, true);

                UpdaterRegistry.AddTrigger(
                    _instance.GetUpdaterId(),
                    new ElementMulticategoryFilter(
                        new List<BuiltInCategory>(Categories)),
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
