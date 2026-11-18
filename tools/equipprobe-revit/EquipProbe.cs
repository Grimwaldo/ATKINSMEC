using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace EquipProbe
{
    // Looks at the SELECTED units for the Equipment Changer and writes
    // %AppData%\ATKINSMEC\ATKINSMEC_equipprobe.txt:
    //
    //   - every connector of the unit (and of its nested parts): domain,
    //     system, size, where, which way, what it is joined to;
    //   - every pipe / duct end within 600 mm of a unit connector, joined
    //     or not ("touching but never joined" shows up here);
    //   - the network behind each connector, piece by piece;
    //   - where EVERY type of the family puts its connectors, read inside
    //     a transaction that is ALWAYS rolled back.
    //
    // Changes nothing in the model.
    [Transaction(TransactionMode.Manual)]
    public class Probe : IExternalCommand
    {
        const double Ft = 304.8;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly StringBuilder _o = new StringBuilder();
        void L(string s = "") => _o.AppendLine(s);
        static string N(double feet) => (feet * Ft).ToString("0", Inv);
        static string P(XYZ p) => p == null ? "-" : $"({N(p.X)},{N(p.Y)},{N(p.Z)})";
        static string D(XYZ p) => p == null ? "-" : $"({p.X.ToString("0.###", Inv)},{p.Y.ToString("0.###", Inv)},{p.Z.ToString("0.###", Inv)})";

        static readonly BuiltInCategory[] Network =
        {
            BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctTerminal, BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_PlumbingFixtures,
        };

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument ui = data.Application.ActiveUIDocument;
            Document doc = ui.Document;

            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATKINSMEC");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "ATKINSMEC_equipprobe.txt");

            L("EQUIPMENT CHANGER probe - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv));
            L("Model: " + doc.Title + "   (mm, model coordinates)");
            L("ShortCurveTolerance " + N(doc.Application.ShortCurveTolerance) + " mm");

            var units = ui.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<FamilyInstance>()
                .ToList();

            if (units.Count == 0)
            {
                TaskDialog.Show("EquipProbe", "Select the unit(s) first, then run it again.");
                return Result.Cancelled;
            }

            foreach (FamilyInstance unit in units)
            {
                try { Unit(doc, unit); }
                catch (Exception e) { L("FAILED on " + unit.Id.Value + ": " + e); }
            }

            File.WriteAllText(path, _o.ToString());

            TaskDialog.Show("EquipProbe", units.Count + " unit(s) written to" +
                Environment.NewLine + path + Environment.NewLine + Environment.NewLine +
                "Nothing in the model was changed.");

            return Result.Succeeded;
        }

        void Unit(Document doc, FamilyInstance unit)
        {
            L();
            L("================================================================");
            string mark = unit.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";
            L($"UNIT {unit.Id.Value}  Mark '{mark}'  {unit.Symbol?.Family?.Name} : {unit.Symbol?.Name}");
            L($"  category {unit.Category?.Name}; pinned {unit.Pinned}; mirrored {unit.Mirrored}; " +
              $"hand flipped {unit.HandFlipped}; facing flipped {unit.FacingFlipped}");
            Element group = unit.GroupId == ElementId.InvalidElementId ? null : doc.GetElement(unit.GroupId);
            L($"  group {(group == null ? "none" : group.Name + " " + group.Id.Value)}; host {(unit.Host == null ? "none" : unit.Host.Name + " " + unit.Host.Id.Value)}; " +
              $"super {(unit.SuperComponent == null ? "none" : unit.SuperComponent.Id.Value.ToString())}");

            if (doc.IsWorkshared)
            {
                try
                {
                    var ws = doc.GetWorksetTable().GetWorkset(unit.WorksetId);
                    L($"  workset {ws?.Name}; owner '{WorksharingUtils.GetWorksharingTooltipInfo(doc, unit.Id).Owner}'");
                }
                catch { }
            }

            Transform t = unit.GetTransform();
            L($"  origin {P(t.Origin)}  X {D(t.BasisX)}  Y {D(t.BasisY)}  Z {D(t.BasisZ)}");
            L($"  facing {D(unit.FacingOrientation)}  hand {D(unit.HandOrientation)}");

            BoundingBoxXYZ box = unit.get_BoundingBox(null);
            if (box != null)
                L($"  box {P(box.Min)} .. {P(box.Max)}  size {P(box.Max - box.Min)}");

            L();
            L("  CONNECTORS (local = in the unit's own frame)");
            var mine = Connectors(unit);
            foreach (Connector c in mine)
                L("    " + Describe(c, t));

            foreach (ElementId sub in unit.GetSubComponentIds())
            {
                var part = doc.GetElement(sub) as FamilyInstance;
                if (part == null) continue;
                var cs = Connectors(part);
                L($"  NESTED {sub.Value} {part.Symbol?.Family?.Name} : {part.Symbol?.Name} - {cs.Count} connector(s)");
                foreach (Connector c in cs)
                    L("    " + Describe(c, t));
                mine.AddRange(cs);
            }

            Near(doc, unit, mine, box);

            L();
            L("  WHAT IS BEHIND EACH CONNECTOR (up to 40 pieces, 10 deep)");
            foreach (Connector c in mine)
            {
                if (c.ConnectorType != ConnectorType.End) continue;
                if (c.Domain != Domain.DomainHvac && c.Domain != Domain.DomainPiping) continue;
                L($"    from connector {c.Id} ({c.Domain}, {SystemOf(c)}):");
                Walk(doc, unit, c);
            }

            Types(doc, unit, t);
        }

        static List<Connector> Connectors(FamilyInstance fi)
        {
            var list = new List<Connector>();
            try
            {
                ConnectorManager m = fi.MEPModel?.ConnectorManager;
                if (m == null) return list;
                foreach (Connector c in m.Connectors) list.Add(c);
            }
            catch { }
            return list;
        }

        static string SystemOf(Connector c)
        {
            try
            {
                if (c.Domain == Domain.DomainPiping) return c.PipeSystemType.ToString();
                if (c.Domain == Domain.DomainHvac) return c.DuctSystemType.ToString();
            }
            catch { }
            return "-";
        }

        static string Size(Connector c)
        {
            try
            {
                if (c.Shape == ConnectorProfileType.Round) return "dia " + N(c.Radius * 2);
                if (c.Shape == ConnectorProfileType.Rectangular) return N(c.Width) + "x" + N(c.Height);
                return c.Shape.ToString();
            }
            catch { return "?"; }
        }

        static XYZ Facing(Connector c)
        {
            try { return c.CoordinateSystem?.BasisZ; } catch { return null; }
        }

        string Describe(Connector c, Transform t)
        {
            var sb = new StringBuilder();
            sb.Append($"#{c.Id} {c.Domain} {c.ConnectorType} {SystemOf(c)} {Size(c)}");
            try { if (!string.IsNullOrEmpty(c.Description)) sb.Append($" '{c.Description}'"); } catch { }
            XYZ o = null;
            try { o = c.Origin; } catch { }
            sb.Append($" at {P(o)}");
            if (o != null && t != null) sb.Append($" local {P(t.Inverse.OfPoint(o))}");
            XYZ f = Facing(c);
            sb.Append($" facing {D(f)}");
            if (f != null && t != null) sb.Append($" local {D(t.Inverse.OfVector(f))}");
            bool joined = false;
            try { joined = c.IsConnected; } catch { }
            sb.Append(joined ? " JOINED to" : " open");
            try
            {
                foreach (Connector r in c.AllRefs)
                {
                    if (r.Owner == null || r.Owner.Id == c.Owner.Id) continue;
                    sb.Append($" [{r.Owner.Id.Value} {r.Owner.Category?.Name} '{r.Owner.Name}' #{r.Id} {r.ConnectorType}]");
                }
            }
            catch { }
            return sb.ToString();
        }

        // Every connector of network elements near the unit's connectors,
        // within 600 mm, whether joined or not
        void Near(Document doc, FamilyInstance unit, List<Connector> mine, BoundingBoxXYZ box)
        {
            L();
            L("  NETWORK ENDS WITHIN 600 mm OF A UNIT CONNECTOR");
            if (box == null) { L("    (no box)"); return; }
            double grow = 1000 / Ft;
            var outline = new Outline(box.Min - new XYZ(grow, grow, grow), box.Max + new XYZ(grow, grow, grow));
            var filter = new LogicalAndFilter(
                new ElementMulticategoryFilter(Network.ToList()),
                new BoundingBoxIntersectsFilter(outline));
            var near = new FilteredElementCollector(doc).WhereElementIsNotElementType().WherePasses(filter).ToList();
            int shown = 0;
            foreach (Element e in near)
            {
                if (e.Id == unit.Id) continue;
                ConnectorManager m = (e as MEPCurve)?.ConnectorManager ?? (e as FamilyInstance)?.MEPModel?.ConnectorManager;
                if (m == null) continue;
                foreach (Connector c in m.Connectors)
                {
                    XYZ o; try { o = c.Origin; } catch { continue; }
                    double best = double.MaxValue; Connector at = null;
                    foreach (Connector u in mine)
                    {
                        try
                        {
                            double d = u.Origin.DistanceTo(o);
                            if (d < best) { best = d; at = u; }
                        }
                        catch { }
                    }
                    if (at == null || best > 600 / Ft) continue;
                    shown++;
                    L($"    {e.Id.Value} {e.Category?.Name} '{e.Name}': {N(best)} mm from unit #{at.Id}: {Describe(c, null)}");
                }
            }
            if (shown == 0) L("    none");
        }

        // Breadth first from one unit connector, through whatever it is
        // joined to
        void Walk(Document doc, FamilyInstance unit, Connector start)
        {
            var seen = new HashSet<long> { unit.Id.Value };
            var queue = new Queue<(Element el, int depth)>();
            try
            {
                foreach (Connector r in start.AllRefs)
                {
                    if (r.Owner == null || seen.Contains(r.Owner.Id.Value)) continue;
                    if (!Net(r.Owner)) continue;
                    seen.Add(r.Owner.Id.Value);
                    queue.Enqueue((r.Owner, 1));
                }
            }
            catch { }
            if (queue.Count == 0) { L("      nothing joined"); return; }
            int count = 0;
            while (queue.Count > 0 && count < 40)
            {
                var (el, depth) = queue.Dequeue();
                count++;
                string pad = new string(' ', 4 + depth * 2);
                L(pad + Piece(el));
                if (depth >= 10) continue;
                ConnectorManager m = (el as MEPCurve)?.ConnectorManager ?? (el as FamilyInstance)?.MEPModel?.ConnectorManager;
                if (m == null) continue;
                foreach (Connector c in m.Connectors)
                {
                    try
                    {
                        foreach (Connector r in c.AllRefs)
                        {
                            if (r.Owner == null || seen.Contains(r.Owner.Id.Value)) continue;
                            if (!Net(r.Owner)) continue;
                            seen.Add(r.Owner.Id.Value);
                            queue.Enqueue((r.Owner, depth + 1));
                        }
                    }
                    catch { }
                }
            }
            if (queue.Count > 0) L("      ... more beyond");
        }

        static bool Net(Element e)
        {
            if (e?.Category == null) return false;
            long id = e.Category.Id.Value;
            return Network.Any(b => (long)b == id);
        }

        string Piece(Element el)
        {
            var sb = new StringBuilder();
            sb.Append($"{el.Id.Value} {el.Category?.Name} '{el.Name}'");
            if (el.Pinned) sb.Append(" PINNED");
            if (el.GroupId != ElementId.InvalidElementId) sb.Append(" IN GROUP " + el.GroupId.Value);
            var curve = el as MEPCurve;
            if (curve != null)
            {
                var line = (curve.Location as LocationCurve)?.Curve as Line;
                if (line != null)
                    sb.Append($" line {P(line.GetEndPoint(0))} -> {P(line.GetEndPoint(1))} dir {D(line.Direction)} len {N(line.Length)}");
                else
                    sb.Append(" (not a straight line)");
                try
                {
                    double dia = curve.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0;
                    if (dia > 0) sb.Append(" dia " + N(dia));
                }
                catch { }
            }
            var fi = el as FamilyInstance;
            if (fi != null)
            {
                string part = "";
                try { part = (fi.MEPModel as MechanicalFitting)?.PartType.ToString() ?? ""; } catch { }
                sb.Append($" {fi.Symbol?.Family?.Name}{(part.Length > 0 ? " part " + part : "")}");
            }
            ConnectorManager m = curve?.ConnectorManager ?? fi?.MEPModel?.ConnectorManager;
            if (m != null)
            {
                foreach (Connector c in m.Connectors)
                {
                    string to = "";
                    try
                    {
                        foreach (Connector r in c.AllRefs)
                            if (r.Owner != null && r.Owner.Id != el.Id && Net(r.Owner)) to += " " + r.Owner.Id.Value;
                    }
                    catch { }
                    XYZ o = null; try { o = c.Origin; } catch { }
                    sb.Append($" | #{c.Id} {c.ConnectorType} {P(o)} f{D(Facing(c))} ->{(to.Length == 0 ? " open" : to)}");
                }
            }
            return sb.ToString();
        }

        // Where every type of the family puts its connectors: swapped in a
        // transaction with the unit unhooked first, then ROLLED BACK
        void Types(Document doc, FamilyInstance unit, Transform t)
        {
            L();
            L("  EVERY TYPE OF THE FAMILY (read in a transaction that is rolled back)");
            Family family = unit.Symbol?.Family;
            if (family == null) { L("    no family"); return; }
            if (unit.GroupId != ElementId.InvalidElementId) { L("    skipped - the unit is in a group"); return; }

            using (var tx = new Transaction(doc, "EquipProbe (rolled back)"))
            {
                var options = tx.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new Quiet());
                options.SetClearAfterRollback(true);
                tx.SetFailureHandlingOptions(options);
                tx.Start();
                try
                {
                    foreach (Connector c in Connectors(unit))
                    {
                        try
                        {
                            foreach (Connector r in c.AllRefs.Cast<Connector>().ToList())
                                if (r.Owner != null && r.Owner.Id != unit.Id && c.IsConnectedTo(r)) c.DisconnectFrom(r);
                        }
                        catch { }
                    }
                    doc.Regenerate();

                    foreach (ElementId sid in family.GetFamilySymbolIds())
                    {
                        var s = doc.GetElement(sid) as FamilySymbol;
                        if (s == null) continue;
                        try
                        {
                            if (!s.IsActive) s.Activate();
                            unit.Symbol = s;
                            doc.Regenerate();
                            BoundingBoxXYZ b = unit.get_BoundingBox(null);
                            L($"    {s.Name}: box {(b == null ? "-" : P(b.Max - b.Min))}");
                            foreach (Connector c in Connectors(unit))
                            {
                                XYZ o = c.Origin;
                                L($"      #{c.Id} {c.Domain} {SystemOf(c)} {Size(c)} local {P(t.Inverse.OfPoint(o))} facing local {D(t.Inverse.OfVector(Facing(c) ?? XYZ.Zero))}");
                            }
                        }
                        catch (Exception e) { L($"    {s.Name}: {e.GetType().Name} {e.Message}"); }
                    }
                }
                finally
                {
                    tx.RollBack();
                }
            }
        }

        class Quiet : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor f)
            {
                foreach (var m in f.GetFailureMessages())
                    if (m.GetSeverity() == FailureSeverity.Warning) f.DeleteWarning(m);
                return FailureProcessingResult.Continue;
            }
        }
    }
}
