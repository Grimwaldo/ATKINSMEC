using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AptProbe
{
    // Stage A for 2BDR-A1 (TA-03-09), LEVEL 03: places the FCUs, terminals,
    // MSFDs and access panels following 1BDR-C (TA-03-06). No ducts or pipes.
    // One transaction, so one Ctrl+Z. Log: Desktop\ATKINSMEC_aptplace.txt.
    [Transaction(TransactionMode.Manual)]
    public class Place : IExternalCommand
    {
        const double Ft = 304.8;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const string WsEquip = "ATR_HVC_Equipment_FCU_Non Typical Floor - Level 03 & Service Floor 16_Apartment";
        const string WsDucts = "ATR_HVC_Ducts_Non Typical Floor - Level 03 & Service Floor 16_Apartment";

        class Item
        {
            public string What, Family, Type, Ws, Comments;
            public double X, Y, Elev; public double Fx, Fy; public double? Flow;
            public Dictionary<string, double> Mm = new Dictionary<string, double>();
        }

        static Item I(string what, string fam, string type, double x, double y, double elev, double fx, double fy, double? flow, string ws, string comments = null)
            => new Item { What = what, Family = fam, Type = type, X = x, Y = y, Elev = elev, Fx = fx, Fy = fy, Flow = flow, Ws = ws, Comments = comments };

        static List<Item> Plan()
        {
            const string FCU = "ATR_ME_MEQ_FCU";
            const string SG = "ATR_ME_AIT_Supply Grille - Rectangular Sidewall_WO flange_new";
            const string RG = "ATR_ME_AIT_Return Grille - Rectangular Sidewall_WO flange_new";
            const string DG = "ATK_ME_AIT_DummyGrille_WallSquareOrRectangularVerticalRectangularConnection_w & h 1 1";
            const string DGT = "2200x150mm Neck";
            const string SLD = "ATK_ME_AIT_Diffuser_LinearSlotSupply";
            const string ELD = "ATK_H_AIT_LinearSlotDiffuser_Exhaust";
            const string EXR = "ATK_ME_AIT_Exhaust_Round_Diffuser";
            const string EXT = "300 Face 150 Connection";
            const string BM = "ATR_ME_MEQ_BellMouth-Fresh Air";
            const string EC = "ATR_ME_AIT_Rectangular Blank Endcap_SAD";
            const string MSFD = "ATR_ME_DUC_Damper_MotorisedRectangular";
            const string AP = "ATR_GEN_Access Panel";
            var l = new List<Item>();

            // --- Master bedroom: FCU-03 over the master ensuite, throwing north
            // through the dressing/bedroom wall (y 79500), as 1BDR-C's FCU-03.
            l.Add(I("FCU master bedroom", FCU, "FCU-03", -90272, 77450, 2600, 0, -1, null, WsEquip));
            l.Add(I("Supply grille master bedroom", SG, "SG-90_600x150_200 LPS", -90864, 79500, 2750, 0, -1, 200, WsDucts));
            l.Add(I("Return grille master bedroom", RG, "RG-90_600x150_200 LPS", -89914, 79500, 2750, 0, -1, 230, WsDucts));
            l.Add(Dummy(DG, DGT, -89414, 79500, 2750, 0, -1, 200));
            l.Add(Dummy(DG, DGT, -90389, 79500, 2750, 0, -1, 150));
            l.Add(Dummy(DG, DGT, -91364, 79500, 2750, 0, -1, 200));
            l.Add(I("Linear slot dressing", SLD, "SLD1_500L x 80W x 100D 2", -90846, 78800, 2500, 1, 0, 30, WsDucts));
            l.Add(I("Fresh air bellmouth master FCU", BM, "Standard", -91247, 76108, 2720, 0, -1, 50, WsDucts));
            l.Add(I("Access panel master FCU", AP, "Standard", -90572, 77150, 2395, 1, 0, null, WsDucts, "AP"));

            // --- Bedroom 2: FCU-03 over its ensuite, throwing west through the
            // ensuite/bedroom wall (x -94449).
            l.Add(I("FCU bedroom 2", FCU, "FCU-03", -93450, 82378, 2600, 1, 0, null, WsEquip));
            l.Add(I("Supply grille bedroom 2", SG, "SG-90_600x150_200 LPS", -94449, 81786, 2750, 1, 0, 230, WsDucts));
            l.Add(I("Return grille bedroom 2", RG, "RG-90_600x150_200 LPS", -94449, 82736, 2750, 1, 0, 230, WsDucts));
            l.Add(Dummy(DG, DGT, -94449, 83236, 2750, 1, 0, 200));
            l.Add(Dummy(DG, DGT, -94449, 82261, 2750, 1, 0, 150));
            l.Add(Dummy(DG, DGT, -94449, 81161, 2750, 1, 0, 450));
            l.Add(I("Fresh air bellmouth bedroom 2 FCU", BM, "Standard", -92850, 81403, 2720, 1, 0, 50, WsDucts));
            l.Add(I("Access panel bedroom 2 FCU", AP, "Standard", -93150, 82100, 2395, 1, 0, null, WsDucts, "AP"));

            // --- Living/dining: FCU-07 over the kitchen, throwing south through
            // the kitchen/living wall (y 76101), as 1BDR-C's FCU-07.
            l.Add(I("FCU living/dining", FCU, "FCU-07", -96505, 77176, 2550, 0, 1, null, WsEquip));
            l.Add(I("Supply grille living", SG, "SG-94_1100x150_380 LPS", -97200, 76101, 2745, 0, 1, 380, WsDucts));
            l.Add(I("Return grille living", RG, "RG-94_1100x150_380 LPS", -96000, 76101, 2745, 0, 1, 200, WsDucts));
            l.Add(I("Linear slot supply kitchen", SLD, "SLD3_500L x 150W x 200D 2", -95945, 77351, 2400, 0, 1, 30, WsDucts));
            l.Add(I("Linear slot exhaust kitchen", ELD, "ELD3_500L x 130W x 150D 2", -95945, 78636, 2400, 0, -1, 35, WsDucts));
            l.Add(I("Fresh air endcap living FCU", EC, "Type 1", -97600, 78206, 2720, 0, 1, 10, WsDucts));
            l.Add(I("Access panel living FCU", AP, "Standard", -96800, 77500, 2395, 0, 1, null, WsDucts, "AP"));

            // --- Wet rooms: round exhaust diffusers, 10 L/s each.
            l.Add(I("Exhaust master ensuite", EXR, EXT, -89700, 76600, 2395, 1, 0, 10, WsDucts));
            l.Add(I("Exhaust ensuite 2", EXR, EXT, -93524, 83000, 2395, 1, 0, 10, WsDucts));
            l.Add(I("Exhaust powder room", EXR, EXT, -89899, 74400, 2395, 1, 0, 10, WsDucts));
            l.Add(I("Exhaust utility", EXR, EXT, -92646, 77025, 2395, 1, 0, 10, WsDucts));

            // --- MSFDs where the branches leave the shafts.
            l.Add(I("MSFD fresh air (shaft north face)", MSFD, "Motorised Smoke & Fire Damper", -92874, 76480, 2720, 1, 0, null, WsDucts, "MSFD"));
            l.Add(I("MSFD extract (shaft north face)", MSFD, "Motorised Smoke & Fire Damper", -94899, 76760, 3000, 1, 0, null, WsDucts, "MSFD"));
            return l;
        }

        static Item Dummy(string fam, string type, double x, double y, double elev, double fx, double fy, double width)
        {
            var d = I("Dummy grille " + width.ToString("0", Inv), fam, type, x, y, elev, fx, fy, null, WsDucts);
            d.Mm["Duct Width"] = width;
            return d;
        }

        StringBuilder _o = new StringBuilder();
        void L(string s = "") => _o.AppendLine(s);

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument.Document;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ATKINSMEC_aptplace.txt");
            string verdict;
            try { verdict = Run(doc); }
            catch (Exception e) { verdict = "FAILED: " + e.Message; L("FAILED: " + e); }
            File.WriteAllText(path, _o.ToString());
            TaskDialog.Show("AptPlace", verdict + "\n\nLog: " + path);
            return Result.Succeeded;
        }

        string Run(Document doc)
        {
            if (!doc.Title.Contains("HVC-000100")) return "Not the HVAC model - nothing placed.";
            var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(v => v.Name == "LEVEL 03");
            if (level == null) return "LEVEL 03 not found - nothing placed.";

            // Refuse a second run: any FCU already inside 2BDR-A1.
            var box = new Outline(new XYZ(-97849 / Ft, 71800 / Ft, (33620 + 1000) / Ft), new XYZ(-89149 / Ft, 83650 / Ft, (33620 + 3400) / Ft));
            var already = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                .WhereElementIsNotElementType().WherePasses(new BoundingBoxIntersectsFilter(box)).ToElementIds();
            if (already.Count > 0) return $"2BDR-A1 already has {already.Count} mechanical equipment - nothing placed (ids {string.Join(",", already.Select(i => i.Value))}).";

            var symbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();
            var worksets = doc.IsWorkshared
                ? new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToDictionary(w => w.Name, w => w.Id)
                : new Dictionary<string, WorksetId>();

            int ok = 0, bad = 0;
            using (var t = new Transaction(doc, "2BDR-A1 stage A (FCUs and terminals)"))
            {
                t.Start();
                foreach (var it in Plan())
                {
                    var sym = symbols.FirstOrDefault(s => s.FamilyName == it.Family && s.Name == it.Type);
                    if (sym == null) { L($"SKIP {it.What}: type {it.Family} : {it.Type} not loaded"); bad++; continue; }
                    try
                    {
                        if (!sym.IsActive) { sym.Activate(); doc.Regenerate(); }
                        var p = new XYZ(it.X / Ft, it.Y / Ft, level.ProjectElevation);
                        var fi = doc.Create.NewFamilyInstance(p, sym, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        Set(fi, BuiltInParameter.INSTANCE_ELEVATION_PARAM, it.Elev / Ft);
                        doc.Regenerate();
                        var want = new XYZ(it.Fx, it.Fy, 0).Normalize();
                        var f0 = new XYZ(fi.FacingOrientation.X, fi.FacingOrientation.Y, 0);
                        if (f0.GetLength() > 1e-6)
                        {
                            f0 = f0.Normalize();
                            double ang = Math.Atan2(f0.X * want.Y - f0.Y * want.X, f0.X * want.X + f0.Y * want.Y);
                            if (Math.Abs(ang) > 1e-6)
                                ElementTransformUtils.RotateElement(doc, fi.Id, Line.CreateBound(p, p + XYZ.BasisZ), ang);
                        }
                        foreach (var kv in it.Mm)
                        {
                            var prm = fi.LookupParameter(kv.Key);
                            if (prm != null && !prm.IsReadOnly) prm.Set(kv.Value / Ft);
                            else L($"   note {it.What}: '{kv.Key}' not settable");
                        }
                        if (it.Flow.HasValue)
                        {
                            var fp = fi.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
                            if (fp != null && !fp.IsReadOnly) fp.Set(UnitUtils.ConvertToInternalUnits(it.Flow.Value, UnitTypeId.LitersPerSecond));
                            else L($"   note {it.What}: Flow not settable");
                        }
                        if (it.Comments != null) fi.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(it.Comments);
                        if (worksets.TryGetValue(it.Ws, out var ws))
                        {
                            var wp = fi.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                            if (wp != null && !wp.IsReadOnly) wp.Set(ws.IntegerValue);
                        }
                        else if (doc.IsWorkshared) L($"   note {it.What}: workset '{it.Ws}' not found, left on the active one");
                        L($"OK   {fi.Id.Value}  {it.What}  [{it.Family} : {it.Type}]");
                        ok++;
                    }
                    catch (Exception e) { L($"FAIL {it.What}: {e.GetType().Name}: {e.Message}"); bad++; }
                }
                t.Commit();
            }

            // Report from the model, not from what was attempted.
            L();
            L("== AS PLACED (read back after commit) ==");
            foreach (var line in _o.ToString().Split('\n').Where(s => s.StartsWith("OK ")).ToList())
            {
                var id = long.Parse(line.Substring(5, line.IndexOf(' ', 5) - 5));
                var fi = doc.GetElement(new ElementId(id)) as FamilyInstance;
                if (fi == null) { L($"  {id} GONE after commit"); continue; }
                var lp = (fi.Location as LocationPoint)?.Point;
                var el = fi.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM)?.AsDouble() * Ft;
                var ws = doc.IsWorkshared ? doc.GetWorksetTable().GetWorkset(fi.WorksetId)?.Name : "";
                L($"  {id} {fi.Symbol.FamilyName} : {fi.Name}  at ({(lp.X * Ft).ToString("0", Inv)},{(lp.Y * Ft).ToString("0", Inv)}) elev {el?.ToString("0", Inv)} facing ({fi.FacingOrientation.X.ToString("0.##", Inv)},{fi.FacingOrientation.Y.ToString("0.##", Inv)}) ws {ws}");
            }
            return $"Placed {ok}, failed or skipped {bad}. One Ctrl+Z undoes it all.";
        }

        static void Set(Element e, BuiltInParameter b, double v)
        {
            var p = e.get_Parameter(b);
            if (p != null && !p.IsReadOnly) p.Set(v);
        }
    }
}
