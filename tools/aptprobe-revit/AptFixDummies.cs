using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AptProbe
{
    // Sets the length of the six dummy grilles AptPlace put in 2BDR-A1.
    // "Duct Width" is read-only on that family; try the parameters that drive it.
    [Transaction(TransactionMode.Manual)]
    public class FixDummies : IExternalCommand
    {
        static readonly Dictionary<long, double> Width = new Dictionary<long, double>
        {
            { 4061099, 200 }, { 4061101, 150 }, { 4061102, 200 },
            { 4061111, 200 }, { 4061112, 150 }, { 4061113, 450 },
        };

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument.Document;
            var o = new StringBuilder();
            using (var t = new Transaction(doc, "2BDR-A1 dummy grille lengths"))
            {
                t.Start();
                foreach (var kv in Width)
                {
                    var e = doc.GetElement(new ElementId(kv.Key));
                    if (e == null) { o.AppendLine($"{kv.Key}: gone"); continue; }
                    // WI drives the length and H the height (145 in 1BDR-C).
                    string done = null;
                    var wi = e.LookupParameter("WI");
                    var h = e.LookupParameter("H");
                    if (wi != null && !wi.IsReadOnly) { wi.Set(kv.Value / 304.8); done = "WI"; }
                    if (h != null && !h.IsReadOnly) h.Set(145 / 304.8);
                    doc.Regenerate();
                    var dw = e.LookupParameter("Duct Width")?.AsDouble() * 304.8;
                    o.AppendLine($"{kv.Key}: set via {done ?? "NOTHING WRITABLE"} -> Duct Width now {dw:0}");
                }
                t.Commit();
            }
            TaskDialog.Show("FixDummies", o.ToString());
            return Result.Succeeded;
        }
    }

    // Read only: every parameter of a 1BDR-C dummy grille and of a new one,
    // with whether it is writable and what drives it.
    [Transaction(TransactionMode.ReadOnly)]
    public class DumpDummy : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument.Document;
            var o = new StringBuilder();
            foreach (long id in new long[] { 3921240, 4061099 })
            {
                var e = doc.GetElement(new ElementId(id)) as FamilyInstance;
                if (e == null) { o.AppendLine($"{id}: not found"); continue; }
                o.AppendLine($"== {id} {e.Symbol.FamilyName} : {e.Name}  super {e.SuperComponent?.Id.Value}  subs {e.GetSubComponentIds().Count}");
                foreach (Parameter p in e.Parameters)
                    o.AppendLine($"  inst {(p.IsReadOnly ? "RO" : "rw")} {p.Definition?.Name} = {(p.StorageType == StorageType.String ? p.AsString() : p.AsValueString())}");
                foreach (Parameter p in e.Symbol.Parameters)
                    o.AppendLine($"  type {(p.IsReadOnly ? "RO" : "rw")} {p.Definition?.Name} = {(p.StorageType == StorageType.String ? p.AsString() : p.AsValueString())}");
                var cm = e.MEPModel?.ConnectorManager;
                if (cm != null) foreach (Connector c in cm.Connectors)
                    {
                        string to = ""; foreach (Connector r in c.AllRefs) to += $" {r.Owner.Id.Value}({r.Owner.Category?.Name})";
                        o.AppendLine($"  conn {c.Id} {c.Shape} w {c.Width * 304.8:0} h {c.Height * 304.8:0} -> {to}");
                    }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ATKINSMEC_dummy.txt"), o.ToString());
            TaskDialog.Show("DumpDummy", "Written to Desktop\\ATKINSMEC_dummy.txt");
            return Result.Succeeded;
        }
    }
}
