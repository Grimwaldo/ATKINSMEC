using Autodesk.Revit.DB;

namespace ATKINSMEC
{
    // THE THREE QADUCTS HELPERS THE EQUIPMENT CHANGER USES, copied from
    // ATKINSMEC\SheetQA\QADUCTS.cs so the lab can compile the changer's
    // own files without the whole SHEET QA framework. Keep in step with
    // that file: Carries (by category), Manager, Lookup.
    public class QADUCTS
    {
        private static readonly BuiltInCategory[] Carriers =
        {
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctTerminal,
            BuiltInCategory.OST_MechanicalEquipment
        };

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

        public static Connector Lookup(Document doc, long ownerId, int connectorId)
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
    }
}
