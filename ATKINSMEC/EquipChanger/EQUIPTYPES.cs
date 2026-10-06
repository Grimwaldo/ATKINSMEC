using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Which types a selection can be changed to.
    //
    // The family FIRST, and the category only if asked. A fan coil has
    // a dozen sizes in its own family and that is nearly always what
    // somebody means by "change the type"; offering every piece of
    // mechanical equipment in the model alongside them buries the
    // twelve that matter.
    //
    // A swap across families works in the API and is sometimes what is
    // wanted - a different manufacturer's unit - but it is far more
    // likely to move the connectors, so it is behind a tick rather than
    // mixed in.
    public static class EQUIPTYPES
    {
        public static List<FamilySymbol> For(
            Document doc, List<long> sources, bool wholeCategory)
        {
            var found = new List<FamilySymbol>();

            if (doc == null || sources == null || sources.Count == 0)
                return found;

            var families = new HashSet<long>();
            var categories = new HashSet<long>();

            foreach (long id in sources)
            {
                var instance = doc.GetElement(
                    REVITCOMPAT.NewId(id)) as FamilyInstance;

                if (instance == null || instance.Symbol == null)
                    continue;

                if (instance.Symbol.Family != null)
                    families.Add(instance.Symbol.Family.Id.ToValue());

                if (instance.Category != null)
                    categories.Add(instance.Category.Id.ToValue());
            }

            var seen = new HashSet<long>();

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol));

            foreach (FamilySymbol symbol in collector)
            {
                if (symbol == null || symbol.Category == null)
                    continue;

                long category = symbol.Category.Id.ToValue();

                if (!categories.Contains(category))
                    continue;

                if (!wholeCategory)
                {
                    if (symbol.Family == null ||
                        !families.Contains(symbol.Family.Id.ToValue()))
                        continue;
                }

                if (seen.Add(symbol.Id.ToValue()))
                    found.Add(symbol);
            }

            found.Sort(delegate (FamilySymbol a, FamilySymbol b)
            {
                int byFamily = string.Compare(
                    FamilyName(a), FamilyName(b),
                    StringComparison.OrdinalIgnoreCase);

                return byFamily != 0
                    ? byFamily
                    : string.Compare(a.Name, b.Name,
                        StringComparison.OrdinalIgnoreCase);
            });

            return found;
        }

        public static string Label(FamilySymbol symbol)
        {
            if (symbol == null)
                return string.Empty;

            string family = FamilyName(symbol);

            return string.IsNullOrEmpty(family)
                ? symbol.Name
                : family + " :  " + symbol.Name;
        }

        private static string FamilyName(FamilySymbol symbol)
        {
            try
            {
                return symbol.Family == null
                    ? string.Empty
                    : symbol.Family.Name;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
