using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace AptProbe
{
    // READ ONLY. Writes Desktop\ATKINSMEC_aptprobe.txt: every apartment-like
    // room in the links, and for the target apartments everything modelled
    // in this model inside them (and the rooms inside them).
    [Transaction(TransactionMode.ReadOnly)]
    public class Probe : IExternalCommand
    {
        static readonly string[] Targets = { "1BDR-C", "2BDR-A1" };
        const double Ft = 304.8;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static readonly BuiltInCategory[] Mep =
        {
            BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_DuctTerminal,
            BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_ShaftOpening, BuiltInCategory.OST_MEPSpaces,
            BuiltInCategory.OST_DuctInsulations, BuiltInCategory.OST_PipeInsulations,
            BuiltInCategory.OST_MechanicalControlDevices, BuiltInCategory.OST_Sprinklers,
        };

        StringBuilder _o = new StringBuilder();
        void L(string s = "") => _o.AppendLine(s);
        static string N(double feet) => (feet * Ft).ToString("0", Inv);
        static string P(XYZ p) => p == null ? "-" : $"({N(p.X)},{N(p.Y)},{N(p.Z)})";
        static string D(XYZ p) => p == null ? "-" : $"({p.X.ToString("0.###", Inv)},{p.Y.ToString("0.###", Inv)},{p.Z.ToString("0.###", Inv)})";
        static string Key(string s) => Regex.Replace(s ?? "", @"\s", "").ToUpperInvariant();

        class Apt
        {
            public string Label; public Room Room; public Transform T;
            public List<XYZ> Poly = new List<XYZ>(); public double Floor;
        }

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument.Document;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ATKINSMEC_aptprobe.txt");
            try { Run(doc, data.Application.ActiveUIDocument.ActiveView); }
            catch (Exception e) { L("FAILED: " + e); }
            File.WriteAllText(path, _o.ToString());
            TaskDialog.Show("AptProbe", "Written to " + path);
            return Result.Succeeded;
        }

        void Run(Document doc, View active)
        {
            L("Model: " + doc.Title + "   active view: " + active.Name);
            L("Units: mm, host internal coordinates.");
            L();
            L("== HOST LEVELS ==");
            foreach (var lv in new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation))
                L($"  {lv.Id.Value}  {lv.Name}  projElev={N(lv.ProjectElevation)}");

            var apts = new List<Apt>();
            var allRooms = new List<(Room r, Transform t, string link)>();
            L();
            L("== LINKS AND APARTMENT-LIKE ROOMS ==");
            foreach (RevitLinkInstance li in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)))
            {
                var ld = li.GetLinkDocument();
                if (ld == null) { L($"  link {li.Name}: not loaded"); continue; }
                var t = li.GetTotalTransform();
                var rooms = new FilteredElementCollector(ld).OfCategory(BuiltInCategory.OST_Rooms).OfType<Room>().Where(r => r.Area > 0).ToList();
                L($"  link {li.Id.Value} {li.Name}: {rooms.Count} rooms");
                foreach (var r in rooms)
                {
                    allRooms.Add((r, t, li.Name));
                    if (Regex.IsMatch(r.Name ?? "", @"BDR|STUDIO|APT", RegexOptions.IgnoreCase))
                        L($"     room {r.Id.Value}  '{r.Name}'  no '{r.Number}'  level {r.Level?.Name}  area {(r.Area * 0.092903).ToString("0.0", Inv)} m2");
                    foreach (var target in Targets)
                        if (Key(r.Name).Contains(Key(target)) && (r.Number ?? "").Contains("-03-"))
                            apts.Add(MakeApt(r, t, target + " [" + li.Name + " room " + r.Id.Value + " " + r.Number + "]"));
                }
            }

            L();
            L("== HOST SYSTEM TYPES ==");
            foreach (var st in new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>())
                L($"  duct  {st.Id.Value}  {st.Name}  [{st.SystemClassification}]");
            foreach (var st in new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>())
                L($"  pipe  {st.Id.Value}  {st.Name}  [{st.SystemClassification}]");
            foreach (var dt in new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>())
                L($"  ducttype  {dt.Id.Value}  {dt.FamilyName} : {dt.Name}  shape {dt.Shape}");
            foreach (var ft in new FilteredElementCollector(doc).OfClass(typeof(FlexDuctType)).Cast<FlexDuctType>())
                L($"  flexducttype  {ft.Id.Value}  {ft.FamilyName} : {ft.Name}");
            foreach (var pt in new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>())
                L($"  pipetype  {pt.Id.Value}  {pt.Name}");

            L();
            L("== LOADED SYMBOLS (equipment, terminals, accessories) ==");
            foreach (var bic in new[] { BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_DuctTerminal, BuiltInCategory.OST_DuctAccessory, BuiltInCategory.OST_PipeAccessory })
                foreach (var fs in new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(bic).Cast<FamilySymbol>().OrderBy(s => s.FamilyName))
                    L($"  [{bic.ToString().Replace("OST_", "")}] {fs.Id.Value}  {fs.FamilyName} : {fs.Name}  active={fs.IsActive}");

            var host = new List<Element>();
            foreach (var bic in Mep)
                host.AddRange(new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType().ToElements());

            foreach (var a in apts)
            {
                L();
                L("################################################################");
                L("== APARTMENT " + a.Label + " ==");
                L($"  floor z {N(a.Floor)}   level {a.Room.Level?.Name}");
                L("  outline: " + string.Join(" ", a.Poly.Select(p => $"({N(p.X)},{N(p.Y)})")));

                L("  -- rooms inside (any link) --");
                foreach (var (r, t, link) in allRooms)
                {
                    if (r.Id == a.Room.Id) continue;
                    var lp = (r.Location as LocationPoint)?.Point;
                    if (lp == null) continue;
                    var hp = t.OfPoint(lp);
                    var bb = r.get_BoundingBox(null);
                    double fz = bb != null ? t.OfPoint(bb.Min).Z : hp.Z;
                    if (Math.Abs(fz - a.Floor) > 1.5 / 0.3048 || !Inside(a.Poly, hp)) continue;
                    var outl = Outline(r, t);
                    L($"    room {r.Id.Value} '{r.Name}' no '{r.Number}' [{link}] area {(r.Area * 0.092903).ToString("0.0", Inv)} m2 floor {N(fz)} height {N(r.UnboundedHeight)} pt {P(hp)}");
                    L("       outline: " + string.Join(" ", outl.Select(p => $"({N(p.X)},{N(p.Y)})")));
                }

                L("  -- this model's elements inside --");
                foreach (var e in host)
                {
                    var pts = Points(e);
                    if (pts.Count == 0) continue;
                    if (!pts.Any(p => p.Z > a.Floor - 1.0 / 0.3048 && p.Z < a.Floor + 6.0 / 0.3048 && Inside(a.Poly, p))) continue;
                    try { Dump(doc, e); } catch (Exception x) { L($"    !! {e.Id.Value}: {x.Message}"); }
                }
            }
            if (apts.Count == 0) L("NO TARGET APARTMENT ROOMS FOUND for " + string.Join(", ", Targets));
        }

        Apt MakeApt(Room r, Transform t, string label)
        {
            var a = new Apt { Label = label, Room = r, T = t };
            a.Poly = Outline(r, t);
            var bb = r.get_BoundingBox(null);
            a.Floor = bb != null ? t.OfPoint(bb.Min).Z : 0;
            return a;
        }

        static List<XYZ> Outline(Room r, Transform t)
        {
            var o = new List<XYZ>();
            var loops = r.GetBoundarySegments(new SpatialElementBoundaryOptions());
            if (loops == null || loops.Count == 0) return o;
            // the outer loop is the longest
            var outer = loops.OrderByDescending(l => l.Sum(s => s.GetCurve().Length)).First();
            foreach (var s in outer)
            {
                var c = s.GetCurve();
                foreach (var p in c.Tessellate().Take(c.Tessellate().Count - 1)) o.Add(t.OfPoint(p));
            }
            return o;
        }

        static bool Inside(List<XYZ> poly, XYZ p)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                    (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)) c = !c;
            return c;
        }

        static List<XYZ> Points(Element e)
        {
            var l = new List<XYZ>();
            if (e.Location is LocationPoint lp) l.Add(lp.Point);
            else if (e.Location is LocationCurve lc) { var c = lc.Curve; l.Add(c.GetEndPoint(0)); l.Add(c.GetEndPoint(1)); l.Add(c.Evaluate(0.5, true)); }
            else { var bb = e.get_BoundingBox(null); if (bb != null) l.Add((bb.Min + bb.Max) / 2); }
            return l;
        }

        void Dump(Document doc, Element e)
        {
            string cat = e.Category?.Name ?? "?";
            string fam = "", typ = doc.GetElement(e.GetTypeId())?.Name ?? "";
            if (e is FamilyInstance fi) fam = fi.Symbol.FamilyName;
            else if (doc.GetElement(e.GetTypeId()) is ElementType et) fam = et.FamilyName;
            L($"    [{cat}] {e.Id.Value}  {fam} : {typ}");
            string lvl = doc.GetElement(e.LevelId)?.Name;
            if (lvl == null && e is MEPCurve mc0) lvl = mc0.ReferenceLevel?.Name;
            if (lvl != null) L($"       level {lvl}");
            if (e.Location is LocationPoint lp)
                L($"       at {P(lp.Point)} rot {(lp.Rotation * 180 / Math.PI).ToString("0.#", Inv)}deg");
            if (e.Location is LocationCurve lc)
                L($"       from {P(lc.Curve.GetEndPoint(0))} to {P(lc.Curve.GetEndPoint(1))} len {N(lc.Curve.Length)}");
            if (e is FamilyInstance f)
            {
                L($"       facing {D(f.FacingOrientation)} hand {D(f.HandOrientation)} host {f.Host?.Id.Value} ({f.Host?.Category?.Name}) hostface {(f.HostFace != null ? "yes" : "no")} workplane-based {f.Symbol.Family.FamilyPlacementType}");
                if (f.SuperComponent != null) L($"       nested in {f.SuperComponent.Id.Value}");
            }
            if (e is MEPCurve m)
            {
                var sys = m.MEPSystem;
                L($"       size {Pv(e, BuiltInParameter.RBS_CALCULATED_SIZE)}  sys {sys?.Name} type {Pv(e, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)}{Pv(e, BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)}");
                L($"       offset {Pv(e, BuiltInParameter.RBS_OFFSET_PARAM)} start {Pv(e, BuiltInParameter.RBS_START_OFFSET_PARAM)} end {Pv(e, BuiltInParameter.RBS_END_OFFSET_PARAM)}");
            }
            foreach (Parameter p in e.Parameters)
            {
                if (!p.HasValue || p.Definition == null) continue;
                string v = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
                if (string.IsNullOrWhiteSpace(v)) continue;
                string n = p.Definition.Name;
                if (Regex.IsMatch(n, "Flow|Offset|Elevation|Size|Width|Height|Diameter|Mark|Comments|System|Level|Workset|Length|Velocity|Angle|Phase|Schedule|Host|Area|Type", RegexOptions.IgnoreCase))
                    L($"       . {n} = {v}");
            }
            ConnectorManager cm = null;
            if (e is FamilyInstance fx) cm = fx.MEPModel?.ConnectorManager;
            else if (e is MEPCurve mx) cm = mx.ConnectorManager;
            if (cm == null) return;
            foreach (Connector c in cm.Connectors)
            {
                string to = "";
                try
                {
                    foreach (Connector o in c.AllRefs)
                        if (o.Owner.Id != e.Id) to += $" {o.Owner.Id.Value}({o.Owner.Category?.Name})";
                }
                catch { }
                string shape = "";
                try { shape = c.Shape == ConnectorProfileType.Round ? "D" + N(c.Radius * 2) : N(c.Width) + "x" + N(c.Height); } catch { }
                string dir = ""; try { dir = c.Direction.ToString(); } catch { }
                string sys = ""; try { sys = c.Domain == Domain.DomainHvac ? c.DuctSystemType.ToString() : c.Domain == Domain.DomainPiping ? c.PipeSystemType.ToString() : c.Domain.ToString(); } catch { }
                string flow = ""; try { flow = (c.Flow * 28.3168).ToString("0.#", Inv) + "L/s"; } catch { }
                string at = "", z = "";
                try { at = P(c.Origin); z = D(c.CoordinateSystem.BasisZ); } catch { at = "(logical)"; }
                L($"       conn {c.Id} {c.ConnectorType} {shape} {sys} {dir} {flow} at {at} z {z} ->{to}");
            }
        }

        static string Pv(Element e, BuiltInParameter b)
        {
            var p = e.get_Parameter(b);
            if (p == null || !p.HasValue) return "";
            return p.StorageType == StorageType.String ? p.AsString() : p.StorageType == StorageType.ElementId ? (e.Document.GetElement(p.AsElementId())?.Name ?? "") : p.AsValueString();
        }
    }
}
