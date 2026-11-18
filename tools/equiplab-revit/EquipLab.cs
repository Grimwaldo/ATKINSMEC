using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ATKINSMEC;

namespace EquipLab
{
    // A DRY RUN of the Equipment Changer on the real model: for each line
    // of %AppData%\ATKINSMEC\equiplab.txt ("<unit id> <type name>") the
    // changer's own EQUIPCHANGE.Run is called inside a TransactionGroup
    // that is ALWAYS ROLLED BACK, and what it left joined is written to
    // %AppData%\ATKINSMEC\ATKINSMEC_equiplab.txt before the roll back.
    // Nothing is kept in the model.
    [Transaction(TransactionMode.Manual)]
    public class Lab : IExternalCommand
    {
        const double Ft = 304.8;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly StringBuilder _o = new StringBuilder();
        void L(string s = "") => _o.AppendLine(s);
        static string N(double feet) => (feet * Ft).ToString("0", Inv);
        static string P(XYZ p) => p == null ? "-" : $"({N(p.X)},{N(p.Y)},{N(p.Z)})";

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            Document doc = data.Application.ActiveUIDocument.Document;

            string folder = LOGS.Folder;
            string input = Path.Combine(folder, "equiplab.txt");
            string output = Path.Combine(folder, "ATKINSMEC_equiplab.txt");

            L("EQUIPMENT CHANGER dry run - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv));
            L("Model: " + doc.Title + "   (mm; everything below was ROLLED BACK)");

            if (!File.Exists(input))
            {
                TaskDialog.Show("EquipLab", "No " + input);
                return Result.Cancelled;
            }

            int cases = 0, joinedAll = 0;

            foreach (string raw in File.ReadAllLines(input))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int space = line.IndexOf(' ');
                if (space < 0) continue;
                long id;
                if (!long.TryParse(line.Substring(0, space), out id)) continue;
                string typeName = line.Substring(space + 1).Trim();
                cases++;
                try
                {
                    if (Case(doc, id, typeName)) joinedAll++;
                }
                catch (Exception e)
                {
                    L("FAILED: " + e);
                }
            }

            File.WriteAllText(output, _o.ToString());

            TaskDialog.Show("EquipLab", cases + " dry run(s), " + joinedAll +
                " with everything joined." + Environment.NewLine + output +
                Environment.NewLine + Environment.NewLine + "Nothing was kept in the model.");

            return Result.Succeeded;
        }

        bool Case(Document doc, long id, string typeName)
        {
            var unit = doc.GetElement(new ElementId(id)) as FamilyInstance;
            L();
            L("================================================================");
            if (unit == null) { L($"{id}: not a family instance"); return false; }

            FamilySymbol symbol = unit.Symbol.Family.GetFamilySymbolIds()
                .Select(s => doc.GetElement(s) as FamilySymbol)
                .FirstOrDefault(s => s != null && s.Name == typeName);

            L($"UNIT {id} {unit.Symbol.Name} -> {typeName}");
            if (symbol == null) { L("  no such type"); return false; }

            var before = Joins(unit);
            L("  joined before: " + string.Join("; ", before));

            bool all = false;

            using (var group = new TransactionGroup(doc, "EquipLab dry run (rolled back)"))
            {
                group.Start();
                try
                {
                    EQUIPCHANGE.Result result = EQUIPCHANGE.Run(
                        doc, new List<long> { id }, symbol.Id.Value, true);

                    foreach (EQUIPCHANGE.Outcome o in result.Outcomes)
                    {
                        L($"  changed {o.Changed}; resized {o.Resized}; said '{o.Said}'");
                        foreach (string s in o.Notes) L("    moved: " + s);
                        foreach (string s in o.LeftOpen) L("    LEFT OPEN: " + s);
                        foreach (string s in o.Branches) L("    " + s);
                    }

                    unit = doc.GetElement(new ElementId(id)) as FamilyInstance;

                    L("  type now " + unit.Symbol.Name + ", origin " + P((unit.Location as LocationPoint)?.Point));

                    int wanted = 0, joined = 0;

                    foreach (Connector c in unit.MEPModel.ConnectorManager.Connectors)
                    {
                        if (c.Domain != Domain.DomainHvac && c.Domain != Domain.DomainPiping) continue;
                        string to = Refs(c);
                        bool isJoined = to.Length > 0;
                        L($"  #{c.Id} {c.Domain} {Sys(c)} at {P(c.Origin)}: {(isJoined ? "JOINED to" + to : "open")}");
                        if (isJoined) joined++;
                        Walk(doc, unit, c);
                    }

                    // Everything joined before is joined after
                    wanted = before.Count;
                    all = joined >= wanted;
                    L($"  {joined} joined after, {wanted} before - {(all ? "ALL JOINED" : "SOMETHING LEFT OPEN")}");
                }
                finally
                {
                    group.RollBack();
                }
            }

            return all;
        }

        static string Sys(Connector c)
        {
            try
            {
                return c.Domain == Domain.DomainPiping ? c.PipeSystemType.ToString() : c.DuctSystemType.ToString();
            }
            catch { return "-"; }
        }

        static bool Net(Element e)
        {
            if (e?.Category == null) return false;
            var id = (BuiltInCategory)(int)e.Category.Id.Value;
            return id == BuiltInCategory.OST_PipeCurves || id == BuiltInCategory.OST_FlexPipeCurves ||
                   id == BuiltInCategory.OST_PipeFitting || id == BuiltInCategory.OST_PipeAccessory ||
                   id == BuiltInCategory.OST_DuctCurves || id == BuiltInCategory.OST_FlexDuctCurves ||
                   id == BuiltInCategory.OST_DuctFitting || id == BuiltInCategory.OST_DuctAccessory ||
                   id == BuiltInCategory.OST_DuctTerminal || id == BuiltInCategory.OST_MechanicalEquipment;
        }

        static string Refs(Connector c)
        {
            string to = "";
            try
            {
                foreach (Connector r in c.AllRefs)
                    if (r.Owner != null && r.Owner.Id != c.Owner.Id && Net(r.Owner))
                        to += $" [{r.Owner.Id.Value} {r.Owner.Category.Name} '{r.Owner.Name}']";
            }
            catch { }
            return to;
        }

        static List<string> Joins(FamilyInstance unit)
        {
            var list = new List<string>();
            foreach (Connector c in unit.MEPModel.ConnectorManager.Connectors)
            {
                if (c.Domain != Domain.DomainHvac && c.Domain != Domain.DomainPiping) continue;
                string to = Refs(c);
                if (to.Length > 0) list.Add("#" + c.Id + to);
            }
            return list;
        }

        // The first pieces behind a connector, after the change
        void Walk(Document doc, FamilyInstance unit, Connector start)
        {
            var seen = new HashSet<long> { unit.Id.Value };
            var queue = new Queue<(Element, int)>();
            foreach (Connector r in start.AllRefs)
                if (r.Owner != null && Net(r.Owner) && seen.Add(r.Owner.Id.Value)) queue.Enqueue((r.Owner, 1));
            int count = 0;
            while (queue.Count > 0 && count < 10)
            {
                var (el, depth) = queue.Dequeue();
                count++;
                var sb = new StringBuilder(new string(' ', 4 + depth * 2));
                sb.Append($"{el.Id.Value} {el.Category?.Name} '{el.Name}'");
                var line = ((el as MEPCurve)?.Location as LocationCurve)?.Curve as Line;
                if (line != null)
                    sb.Append($" {P(line.GetEndPoint(0))} -> {P(line.GetEndPoint(1))} len {N(line.Length)}");
                ConnectorManager m = (el as MEPCurve)?.ConnectorManager ?? (el as FamilyInstance)?.MEPModel?.ConnectorManager;
                if (m != null)
                {
                    foreach (Connector c in m.Connectors)
                    {
                        if (c.ConnectorType != ConnectorType.End) continue;
                        string to = Refs(c);
                        sb.Append($" | #{c.Id} {P(c.Origin)}{(to.Length == 0 ? " OPEN" : "")}");
                        foreach (Connector r in c.AllRefs)
                            if (r.Owner != null && Net(r.Owner) && seen.Add(r.Owner.Id.Value) && depth < 5)
                                queue.Enqueue((r.Owner, depth + 1));
                    }
                }
                L(sb.ToString());
            }
        }
    }
}
