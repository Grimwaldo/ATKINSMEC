using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Small things about an element that more than one CHW file wants,
    // kept in one place so they cannot drift apart.
    public static class QAEQUIP
    {
        // The PIPING end connectors of anything - a fan coil, a pipe, a
        // fitting. Domain and type both checked: a fan coil has duct
        // connectors and electrical ones too, and its piping side is
        // the only part of it this page is about.
        public static List<Connector> PipeEnds(Element element)
        {
            var ends = new List<Connector>();

            ConnectorManager manager = QADUCTS.Manager(element);

            if (manager == null)
                return ends;

            try
            {
                foreach (Connector connector in manager.Connectors)
                {
                    if (connector.Domain == Domain.DomainPiping &&
                        connector.ConnectorType == ConnectorType.End)
                        ends.Add(connector);
                }
            }
            catch
            {
            }

            return ends;
        }

        // What to call it in a report: the Mark if it has one, since
        // that is what the drawing says, and the type name otherwise
        public static string Describe(Element element)
        {
            if (element == null)
                return string.Empty;

            try
            {
                Parameter mark = element.get_Parameter(
                    BuiltInParameter.ALL_MODEL_MARK);

                string said = mark == null
                    ? string.Empty
                    : (mark.AsString() ?? string.Empty);

                if (said.Length > 0)
                    return said;

                string kind = element.Category == null
                    ? string.Empty
                    : element.Category.Name;

                string name = element.Name ?? string.Empty;

                return (kind.Length > 0 ? kind + " - " : string.Empty) + name;
            }
            catch
            {
                return element.Id.ToValue().ToString();
            }
        }

        // One mechanical equipment family, for the picker.
        public class FamilyItem
        {
            public long Id { get; set; }
            public string Name { get; set; }

            // How many of them are placed. A family with none in the
            // model is not offered at all - it would be a line nobody
            // can act on.
            public int Placed { get; set; }

            public override string ToString()
            {
                return Name + "   (" + Placed + ")";
            }
        }

        // The mechanical equipment families PLACED in this model.
        //
        // Read from the instances rather than from the family list,
        // because a project can carry a hundred loaded families and
        // have six of them in the model. The count comes free and is
        // worth showing.
        public static List<FamilyItem> Families(Document doc)
        {
            var found = new Dictionary<long, FamilyItem>();

            if (doc == null)
                return new List<FamilyItem>();

            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                var instance = element as FamilyInstance;

                if (instance == null || instance.Symbol == null ||
                    instance.Symbol.Family == null)
                    continue;

                long id = instance.Symbol.Family.Id.ToValue();

                FamilyItem item;

                if (!found.TryGetValue(id, out item))
                {
                    item = new FamilyItem
                    {
                        Id = id,
                        Name = instance.Symbol.Family.Name ?? string.Empty
                    };

                    found[id] = item;
                }

                item.Placed++;
            }

            var list = new List<FamilyItem>(found.Values);

            list.Sort(delegate (FamilyItem a, FamilyItem b)
            {
                return string.Compare(a.Name, b.Name,
                    StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        // Is this a fan coil, rather than any other equipment? Used
        // only to word a finding - the fix does not care, because a
        // pipe belongs on whatever connector carries its system.
        public static bool LooksLikeFcu(Element element)
        {
            try
            {
                string name = (element.Name ?? string.Empty).ToUpperInvariant();

                if (name.Contains("FCU") || name.Contains("FAN COIL"))
                    return true;

                var instance = element as FamilyInstance;

                if (instance != null && instance.Symbol != null &&
                    instance.Symbol.Family != null)
                {
                    string family = (instance.Symbol.Family.Name ??
                                     string.Empty).ToUpperInvariant();

                    return family.Contains("FCU") ||
                           family.Contains("FAN COIL");
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
