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

namespace AptProbe
{
    // Stage B for 2BDR-A1 (TA-03-09): ductwork and CHW pipework onto what
    // AptPlace put there, following 1BDR-C. One transaction (one Ctrl+Z);
    // every route its own SubTransaction, so one refusal costs that route only.
    // Log: Desktop\ATKINSMEC_aptroute.txt.
    [Transaction(TransactionMode.Manual)]
    public class Route : IExternalCommand
    {
        const double Ft = 304.8;
        const double Z0 = 33620; // LEVEL 03, mm, internal
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Stage A elements (ids from ATKINSMEC_aptplace.txt)
        const long FcuMaster = 4061096, SgMaster = 4061097, RgMaster = 4061098, SldDressing = 4061103, BmMaster = 4061104;
        const long FcuBed2 = 4061107, SgBed2 = 4061108, RgBed2 = 4061109, BmBed2 = 4061114;
        const long FcuLiving = 4061116, SgLiving = 4061117, RgLiving = 4061118, SldKitchen = 4061119, EldKitchen = 4061120, EcLiving = 4061121, ApLiving = 4061123;
        const long ExMasterEns = 4061125, ExEns2 = 4061126, ExPowder = 4061127, ExUtility = 4061128;
        const long MsfdFa = 4061129, MsfdEa = 4061130;
        // Existing in 2BDR-A1
        const long RiserFa = 3465722, RiserEa = 3465724;
        const long ChwsPipe = 3278668, ChwrPipe = 3278670, ChwsCap = 3902087, ChwrCap = 3901905;
        // 1BDR-C insulation to copy type and thickness from
        const long TmplDuctIns = 3825813, TmplPipeIns = 3427650;

        Document _doc;
        ElementId _lvl, _rect, _rectTap, _round, _flex, _pipeType;
        ElementId _sysSa, _sysRa, _sysEa, _sysFa, _sysChws, _sysChwr;
        readonly List<ElementId> _made = new List<ElementId>();
        readonly StringBuilder _o = new StringBuilder();
        void L(string s = "") => _o.AppendLine(s);

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            _doc = data.Application.ActiveUIDocument.Document;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ATKINSMEC_aptroute.txt");
            string verdict;
            try { verdict = Run(); }
            catch (Exception e) { verdict = "FAILED: " + e.Message; L("FAILED: " + e); }
            File.WriteAllText(path, _o.ToString());
            TaskDialog.Show("AptRoute", verdict + "\n\nLog: " + path);
            return Result.Succeeded;
        }

        // ------------------------------------------------------------ setup
        static XYZ P(double x, double y, double z) => new XYZ(x / Ft, y / Ft, z / Ft);
        static string S(XYZ p) => p == null ? "-" : $"({(p.X * Ft).ToString("0", Inv)},{(p.Y * Ft).ToString("0", Inv)},{(p.Z * Ft).ToString("0", Inv)})";
        T Get<T>(long id) where T : Element => _doc.GetElement(new ElementId(id)) as T;

        ElementId TypeId<T>(Func<T, bool> f) where T : Element
            => new FilteredElementCollector(_doc).OfClass(typeof(T)).Cast<T>().First(f).Id;

        string Run()
        {
            if (!_doc.Title.Contains("HVC-000100")) return "Not the HVAC model - nothing done.";
            if (Get<FamilyInstance>(FcuLiving) == null || Get<FamilyInstance>(FcuMaster) == null) return "Stage A elements not found - nothing done.";
            var refuse = Guard();
            if (refuse != null) return refuse;

            _lvl = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().First(v => v.Name == "LEVEL 03").Id;
            _rect = TypeId<DuctType>(t => t.Name == "ATR_ME_Flanged Radius Bend / Shoe Branch" && t.Shape == ConnectorProfileType.Rectangular);
            _rectTap = TypeId<DuctType>(t => t.Name == "ATR_ME_Slip Joint - Tap");
            _round = TypeId<DuctType>(t => t.Name == "ATR_ME_Pressed Radius Bend / Shoe Branch");
            _flex = TypeId<FlexDuctType>(t => t.Name == "ATR_ME_Flex - Round");
            _pipeType = TypeId<PipeType>(t => t.Name == "ATR_ME_Butt Weld - Tee");
            _sysSa = TypeId<MechanicalSystemType>(t => t.Name == "ATR_ME_Supply Air");
            _sysRa = TypeId<MechanicalSystemType>(t => t.Name == "ATR_ME_Return Air");
            _sysEa = TypeId<MechanicalSystemType>(t => t.Name == "ATR_ME_General Extract");
            _sysFa = TypeId<MechanicalSystemType>(t => t.Name == "ATR_ME_Fresh Air");
            _sysChws = TypeId<PipingSystemType>(t => t.Name == "ATR_ME_Chilled Water Supply");
            _sysChwr = TypeId<PipingSystemType>(t => t.Name == "ATR_ME_Chilled Water Return");

            // Revit processes failures when a TRANSACTION commits, not a
            // SubTransaction - so each route is its own Transaction (an error
            // rolls back that route alone) inside one group (one Ctrl+Z).
            int ok = 0, bad = 0;
            using (var g = new TransactionGroup(_doc, "2BDR-A1 stage B (ducts and CHW)"))
            {
                g.Start();
                foreach (var (name, act) in Steps())
                {
                    int before = _made.Count;
                    var q = new Quiet(this);
                    using (var t = new Transaction(_doc, name))
                    {
                        string why = null;
                        try
                        {
                            // after Start, or Revit ignores it (EQUIPCHANGE does the same)
                            t.Start();
                            Handle(t, q);
                            act();
                            _doc.Regenerate();
                            if (t.Commit() != TransactionStatus.Committed) why = "rolled back at commit: " + q.Errors;
                        }
                        catch (Exception e)
                        {
                            if (t.HasStarted() && !t.HasEnded()) t.RollBack();
                            why = e.GetType().Name + ": " + e.Message;
                        }
                        if (why == null) { L($"OK   {name}"); ok++; }
                        else { _made.RemoveRange(before, _made.Count - before); L($"FAIL {name}: {why}"); bad++; }
                    }
                }
                using (var t = new Transaction(_doc, "insulation"))
                {
                    t.Start();
                    Handle(t, new Quiet(this));
                    Insulate();
                    t.Commit();
                }
                g.Assimilate();
            }
            Report();
            return $"Routes done {ok}, refused {bad}. One Ctrl+Z undoes stage B.";
        }

        protected virtual string Guard()
            => Get<Element>(ChwsCap) == null ? "The CHW caps are already gone - stage B looks done already. Nothing done." : null;

        // ------------------------------------------------------------ the plan
        protected virtual IEnumerable<(string, Action)> Steps()
        {
            // What the previous, partly failed run left behind.
            yield return ("remove what the previous stage B run made", () => Cleanup());

            // Stage A positions that the routes need changed (see the log).
            // FCU-07's connectors are on its +X side: supply in line with the supply grille.
            yield return ("FCU-07 in line with the living supply grille", () => MoveTo(FcuLiving, -96695, 77176, 0, 1));
            yield return ("swap living grilles (supply east under the FCU, return west)", () => { MoveTo(SgLiving, -96000, 76101, 0, 1); MoveTo(RgLiving, -97200, 76101, 0, 1); });
            yield return ("move kitchen supply slot clear of the ducts", () => MoveTo(SldKitchen, -96960, 76700, -1, 0));
            yield return ("move kitchen exhaust slot clear of the ducts", () => MoveTo(EldKitchen, -95700, 78400, -1, 0));
            yield return ("living fresh air endcap at +2850 beside FCU-07", () => MoveTo(EcLiving, -95000, 78700, 0, -1, 2850));
            yield return ("living access panel under FCU-07", () => MoveTo(ApLiving, -96000, 77500, 0, 1));
            yield return ("move dressing slot clear of the master supply duct", () => MoveTo(SldDressing, -89700, 78800, 1, 0));
            // Fresh air at +2850 and extract at +3100 so they cross the CHW and each other.
            yield return ("MSFDs off the riser faces, fresh air +2850, extract +3100", () => { MoveTo(MsfdFa, -92874, 76800, 1, 0, 2850); MoveTo(MsfdEa, -94899, 77100, 1, 0, 3100); });
            yield return ("bellmouths at +2850 facing their ducts", () => { MoveTo(BmMaster, -91247, 76108, -1, 0, 2850); MoveTo(BmBed2, -93450, 81050, 0, -1, 2850); });

            // Supply: FCU -> stub -> transition -> straight to the supply grille.
            yield return ("supply master bedroom", () => Supply(FcuMaster, SgMaster));
            yield return ("supply bedroom 2", () => Supply(FcuBed2, SgBed2));
            yield return ("supply living", () => Supply(FcuLiving, SgLiving));
            // Linear slots off the supply ducts: takeoff, round branch, flex.
            yield return ("dressing slot off master supply", () => SlotOff(SupplyDuctOf(SgMaster), SldDressing, 78800, true, 200));
            yield return ("kitchen slot off living supply", () => SlotOff(SupplyDuctOf(SgLiving), SldKitchen, 76500, false, 50));

            // Returns: master and bedroom 2 to the ceiling plenum (as 1BDR-C's
            // FCU-03); living ducted back round to FCU-07 (as 1BDR-C's FCU-07).
            yield return ("return master bedroom (plenum)", () => PlenumReturn(RgMaster));
            yield return ("return bedroom 2 (plenum)", () => PlenumReturn(RgBed2));
            yield return ("return living (ducted to FCU-07)", () => LivingReturn());

            // Extract and fresh air.
            yield return ("extract main, MSFD and riser", () => Extract());
            yield return ("fresh air trunk, MSFD and riser", () => FreshAir());

            // Chilled water.
            yield return ("CHW flow and return to the three FCUs", () => Chw());
        }

        // ------------------------------------------------------------ helpers
        static void Handle(Transaction t, IFailuresPreprocessor q)
        {
            var fo = t.GetFailureHandlingOptions();
            fo.SetFailuresPreprocessor(q);
            fo.SetClearAfterRollback(true);
            t.SetFailureHandlingOptions(fo);
        }

        // Deletes ductwork and pipework newer than stage A's last element and
        // inside 2BDR-A1 - what a previous stage B run made. Never older ids.
        void Cleanup()
        {
            var box = new Outline(P(-97849, 71800, Z0 + 1000), P(-89149, 83650, Z0 + 3400));
            var cats = new[] { BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_FlexDuctCurves,
                               BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_DuctAccessory };
            var gone = new List<ElementId>();
            foreach (var c in cats)
                gone.AddRange(new FilteredElementCollector(_doc).OfCategory(c).WhereElementIsNotElementType()
                    .WherePasses(new BoundingBoxIntersectsFilter(box)).ToElementIds()
                    .Where(i => i.Value > MsfdEa));
            L($"   removing {gone.Count} element(s) from the previous run: {string.Join(",", gone.Select(i => i.Value))}");
            if (gone.Count > 0) _doc.Delete(gone);
        }

        void MoveTo(long id, double x, double y, double fx, double fy, double elev = double.NaN)
        {
            var fi = Get<FamilyInstance>(id) ?? throw new Exception($"{id} not found");
            if (!double.IsNaN(elev)) { fi.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(elev / Ft); _doc.Regenerate(); }
            var lp = (LocationPoint)fi.Location;
            var to = new XYZ(x / Ft, y / Ft, lp.Point.Z);
            var d = to - lp.Point;
            if (d.GetLength() > 1e-6) ElementTransformUtils.MoveElement(_doc, fi.Id, d);
            _doc.Regenerate();
            var want = new XYZ(fx, fy, 0).Normalize();
            var f0 = new XYZ(fi.FacingOrientation.X, fi.FacingOrientation.Y, 0).Normalize();
            double ang = Math.Atan2(f0.X * want.Y - f0.Y * want.X, f0.X * want.X + f0.Y * want.Y);
            if (Math.Abs(ang) > 1e-6) ElementTransformUtils.RotateElement(_doc, fi.Id, Line.CreateBound(to, to + XYZ.BasisZ), ang);
            _doc.Regenerate();
        }

        static IEnumerable<Connector> Conns(Element e)
        {
            ConnectorManager cm = (e as FamilyInstance)?.MEPModel?.ConnectorManager ?? (e as MEPCurve)?.ConnectorManager;
            if (cm == null) yield break;
            foreach (Connector c in cm.Connectors)
                if (c.ConnectorType == ConnectorType.End || c.ConnectorType == ConnectorType.Curve) yield return c;
        }

        Connector Air(long id, DuctSystemType s)
            => Conns(Get<Element>(id)).First(c => c.Domain == Domain.DomainHvac && c.DuctSystemType == s);
        Connector OnlyDuct(long id)
            => Conns(Get<Element>(id)).Where(c => c.Domain == Domain.DomainHvac).OrderBy(c => c.IsConnected ? 1 : 0).First();
        Connector Water(long id, PipeSystemType s)
            => Conns(Get<Element>(id)).First(c => c.Domain == Domain.DomainPiping && c.PipeSystemType == s);
        static Connector End(Element e, XYZ near)
            => Conns(e).Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(near)).First();
        static XYZ Dir(Connector c) => c.CoordinateSystem.BasisZ;

        Duct Rect(XYZ a, XYZ b, double w, double h, ElementId sys, ElementId type)
        {
            var d = Duct.Create(_doc, sys, type, _lvl, a, b);
            d.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(w / Ft);
            d.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(h / Ft);
            _made.Add(d.Id);
            return d;
        }
        Duct Round(XYZ a, XYZ b, double dia, ElementId sys)
        {
            var d = Duct.Create(_doc, sys, _round, _lvl, a, b);
            d.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM).Set(dia / Ft);
            _made.Add(d.Id);
            return d;
        }
        Duct FromConn(Connector c, XYZ b, ElementId type)
        {
            var d = Duct.Create(_doc, type, _lvl, c, b);
            _made.Add(d.Id);
            return d;
        }
        void Fit(FamilyInstance f) { if (f != null) _made.Add(f.Id); }

        // Joins two coincident open ends: same line -> connect or transition,
        // a turn -> elbow. Connectors are fetched fresh by the caller.
        void Join(Connector a, Connector b)
        {
            _doc.Regenerate();
            double dot = Dir(a).DotProduct(Dir(b));
            if (dot < -0.999)
            {
                if (SameSize(a, b)) a.ConnectTo(b);
                else Fit(_doc.Create.NewTransitionFitting(a, b));
            }
            else Fit(_doc.Create.NewElbowFitting(a, b));
            _doc.Regenerate();
        }
        static bool SameSize(Connector a, Connector b)
        {
            if (a.Shape != b.Shape) return false;
            if (a.Shape == ConnectorProfileType.Round) return Math.Abs(a.Radius - b.Radius) < 1e-4;
            return Math.Abs(a.Width - b.Width) < 1e-4 && Math.Abs(a.Height - b.Height) < 1e-4;
        }

        // A chain of straight ducts through the points, elbows between.
        List<Duct> Chain(IList<XYZ> pts, Func<XYZ, XYZ, Duct> make)
        {
            var l = new List<Duct>();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var d = make(pts[i], pts[i + 1]);
                if (l.Count > 0) Join(End(l[l.Count - 1], pts[i]), End(d, pts[i]));
                l.Add(d);
            }
            return l;
        }

        // A round branch from a point on the main's centreline, tapped in,
        // and a flex from its end to the terminal.
        void TapAndFlex(MEPCurve main, XYZ onMain, XYZ branchEnd, double dia, ElementId sys, Connector terminal)
        {
            var b = Round(onMain, branchEnd, dia, sys);
            _doc.Regenerate();
            Fit(_doc.Create.NewTakeoffFitting(End(b, onMain), main));
            _doc.Regenerate();
            var fx = _doc.Create.NewFlexDuct(End(b, branchEnd), terminal, _doc.GetElement(_flex) as FlexDuctType);
            _made.Add(fx.Id);
        }

        // ------------------------------------------------------------ supply
        void Supply(long fcu, long grille)
        {
            var cf = Air(fcu, DuctSystemType.SupplyAir);
            var cg = OnlyDuct(grille);
            var dir = Dir(cf);
            if (Math.Abs((cg.Origin - cf.Origin).CrossProduct(dir).GetLength()) * Ft > 20)
                throw new Exception($"grille {S(cg.Origin)} is not in line with the FCU supply {S(cf.Origin)} along {S(dir * Ft)}");
            var a = cf.Origin + dir * (200 / Ft);
            var stub = FromConn(cf, a, _rect);
            var main = Rect(a, cg.Origin, cg.Width * Ft, cg.Height * Ft, _sysSa, _rect);
            Join(End(stub, a), End(main, a));
            _doc.Regenerate();
            End(main, cg.Origin).ConnectTo(OnlyDuct(grille));
        }

        Duct SupplyDuctOf(long grille)
        {
            var cg = OnlyDuct(grille);
            foreach (Connector r in cg.AllRefs)
                if (r.Owner is Duct d) return d;
            throw new Exception("supply duct to the grille not found");
        }

        void SlotOff(Duct main, long slot, double alongMm, bool mainRunsY, double branchMm)
        {
            var cs = OnlyDuct(slot);
            var line = (Line)((LocationCurve)main.Location).Curve;
            double z = line.GetEndPoint(0).Z;
            XYZ onMain = mainRunsY ? new XYZ(line.GetEndPoint(0).X, alongMm / Ft, z) : new XYZ(line.GetEndPoint(0).X, alongMm / Ft, z);
            // branch heads towards the slot, ending short of it
            var toward = new XYZ(Math.Sign(cs.Origin.X - onMain.X), 0, 0);
            double half = main.Width / 2;
            var end = onMain + toward * (half + branchMm / Ft);
            TapAndFlex(main, onMain, end, cs.Radius * 2 * Ft, _sysSa, cs);
        }

        // ------------------------------------------------------------ returns
        // An open 155 mm stub behind the grille into the ceiling void (1BDR-C
        // turns it up as well; a 600 mm bend on a 155 mm stub flipped it).
        void PlenumReturn(long grille)
        {
            var cg = OnlyDuct(grille);
            FromConn(cg, cg.Origin + Dir(cg) * (155 / Ft), _rect);
        }

        // Grille -> north -> east -> south into FCU-07's return, 400x250 with a
        // transition straight onto the grille and onto the FCU.
        void LivingReturn()
        {
            var cg = OnlyDuct(RgLiving);
            var cr = Air(FcuLiving, DuctSystemType.ReturnAir);
            double z = cg.Origin.Z, yRun = 78800 / Ft;
            var pts = new List<XYZ> { cg.Origin, new XYZ(cg.Origin.X, yRun, z), new XYZ(cr.Origin.X, yRun, z), cr.Origin };
            var run = Chain(pts, (p, q) => Rect(p, q, 400, 250, _sysRa, _rect));
            Join(End(run[0], cg.Origin), OnlyDuct(RgLiving));
            Join(End(run[run.Count - 1], cr.Origin), Air(FcuLiving, DuctSystemType.ReturnAir));
        }

        // ------------------------------------------------------------ extract
        // A duct from an MSFD's riser-side connector into the riser's centre
        // line, tapped into the riser.
        void IntoRiser(Connector c, Duct riser, double alongY)
        {
            var rc = ((LocationCurve)riser.Location).Curve.GetEndPoint(0);
            var at = double.IsNaN(alongY) ? new XYZ(rc.X, rc.Y, c.Origin.Z) : new XYZ(rc.X, alongY, c.Origin.Z);
            var link = FromConn(c, at, _rectTap);
            _doc.Regenerate();
            Fit(_doc.Create.NewTakeoffFitting(End(link, at), riser));
        }

        void Extract()
        {
            const double Zea = Z0 + 3100;
            var riser = Get<Duct>(RiserEa);
            var msfd = Get<FamilyInstance>(MsfdEa);
            var cs = Conns(msfd).OrderBy(c => c.Origin.Y).First();      // south, towards the riser
            var cn = Conns(msfd).OrderBy(c => c.Origin.Y).Last();       // north
            IntoRiser(cs, riser, double.NaN);

            // north leg from the MSFD, then the east main tapped off it
            var nEnd = new XYZ(cn.Origin.X, 78600 / Ft, cn.Origin.Z);
            var north = FromConn(cn, nEnd, _rectTap);
            var eStart = new XYZ(cn.Origin.X, 77500 / Ft, cn.Origin.Z);
            var east = Chain(new List<XYZ> { eStart, P(-89700, 77500, Zea), P(-89700, 74500, Zea) },
                             (p, q) => Rect(p, q, 200, 150, _sysEa, _rectTap));
            _doc.Regenerate();
            Fit(_doc.Create.NewTakeoffFitting(End(east[0], eStart), north));
            _doc.Regenerate();

            var eastMain = east[0];
            var southLeg = east[1];
            TapAndFlex(eastMain, P(-92400, 77500, Zea), P(-92400, 76850, Zea), 150, _sysEa, OnlyDuct(ExUtility));
            TapAndFlex(southLeg, P(-89700, 76350, Zea), P(-90000, 76350, Zea), 150, _sysEa, OnlyDuct(ExMasterEns));
            TapAndFlex(southLeg, P(-89700, 74700, Zea), P(-90100, 74700, Zea), 150, _sysEa, OnlyDuct(ExPowder));
            TapAndFlex(north, P(-94899, 78400, Zea), P(-95400, 78400, Zea), 200, _sysEa, OnlyDuct(EldKitchen));

            // Ensuite 2: east of the fresh air trunk and clear of bedroom 2's
            // FCU, north then west over it, flex down.
            var e0 = P(-92700, 77500, Zea);
            var ens = Chain(new List<XYZ> { e0, P(-92700, 82800, Zea), P(-93400, 82800, Zea) }, (p, q) => Round(p, q, 150, _sysEa));
            _doc.Regenerate();
            Fit(_doc.Create.NewTakeoffFitting(End(ens[0], e0), eastMain));
            _doc.Regenerate();
            var fx = _doc.Create.NewFlexDuct(End(ens[ens.Count - 1], P(-93400, 82800, Zea)), OnlyDuct(ExEns2), _doc.GetElement(_flex) as FlexDuctType);
            _made.Add(fx.Id);
        }

        // ------------------------------------------------------------ fresh air
        void FreshAir()
        {
            var riser = Get<Duct>(RiserFa);
            var msfd = Get<FamilyInstance>(MsfdFa);
            var cs = Conns(msfd).OrderBy(c => c.Origin.Y).First();
            var cn = Conns(msfd).OrderBy(c => c.Origin.Y).Last();
            IntoRiser(cs, riser, double.NaN);
            double z = cn.Origin.Z;

            // trunk north, west, then north onto bedroom 2's bellmouth
            var cb2 = OnlyDuct(BmBed2);
            var a = new XYZ(cn.Origin.X, 80600 / Ft, z);
            var first = FromConn(cn, a, _rectTap);
            double w = first.Width * Ft, h = first.Height * Ft;
            var rest = Chain(new List<XYZ> { a, new XYZ(cb2.Origin.X, a.Y, z), new XYZ(cb2.Origin.X, cb2.Origin.Y, z) },
                             (p, q) => Rect(p, q, w, h, _sysFa, _rectTap));
            Join(End(first, a), End(rest[0], a));
            Join(End(rest[rest.Count - 1], cb2.Origin), OnlyDuct(BmBed2));

            // living endcap: tap west off the trunk, straight onto it
            var cec = OnlyDuct(EcLiving);
            var t1 = new XYZ(cn.Origin.X, cec.Origin.Y, z);
            var wl = Rect(t1, new XYZ(cec.Origin.X, cec.Origin.Y, z), cec.Width * Ft, cec.Height * Ft, _sysFa, _rectTap);
            _doc.Regenerate();
            Fit(_doc.Create.NewTakeoffFitting(End(wl, t1), first));
            _doc.Regenerate();
            Join(End(wl, cec.Origin), OnlyDuct(EcLiving));

            // master bellmouth: its own MSFD on the riser's east face, as
            // 1BDR-C leaves its shaft through one.
            var cbm = OnlyDuct(BmMaster);
            var sym = (Get<FamilyInstance>(MsfdFa)).Symbol;
            var lvl = _doc.GetElement(_lvl) as Level;
            var m2 = _doc.Create.NewFamilyInstance(new XYZ(-92300 / Ft, cbm.Origin.Y, lvl.ProjectElevation), sym, lvl,
                                                   Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            _made.Add(m2.Id);
            m2.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(2850 / Ft);
            m2.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set("MSFD");
            var ws = Get<FamilyInstance>(MsfdFa).get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).AsInteger();
            m2.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM)?.Set(ws);
            _doc.Regenerate();
            // connectors along X: turn it until they are
            var cc = Conns(m2).ToList();
            if (Math.Abs(cc[0].Origin.X - cc[1].Origin.X) < 1e-3)
            {
                var lp = ((LocationPoint)m2.Location).Point;
                ElementTransformUtils.RotateElement(_doc, m2.Id, Line.CreateBound(lp, lp + XYZ.BasisZ), Math.PI / 2);
                _doc.Regenerate();
            }
            var mw = Conns(m2).OrderBy(c => c.Origin.X).First();
            var me = Conns(m2).OrderBy(c => c.Origin.X).Last();
            IntoRiser(mw, riser, cbm.Origin.Y);
            var toBm = FromConn(Conns(m2).OrderBy(c => c.Origin.X).Last(), cbm.Origin, _rectTap);
            _doc.Regenerate();
            Join(End(toBm, cbm.Origin), OnlyDuct(BmMaster));
        }

        // ------------------------------------------------------------ follow-up (RouteFix)
        // 1BDR-C's FCU-07 return: a 400 mm stub off the unit, the grille's
        // 300x300 duct tapped into its side - no 1290 -> 300 transition.
        protected void LivingReturn2()
        {
            var cr = Air(FcuLiving, DuctSystemType.ReturnAir);
            var sEnd = cr.Origin + Dir(cr) * (400 / Ft);
            var stub = FromConn(cr, sEnd, _rect);
            var cg = OnlyDuct(RgLiving);
            double z = cg.Origin.Z;
            var meet = new XYZ(cr.Origin.X, cr.Origin.Y + 200 / Ft, z);
            var run = Chain(new List<XYZ> { cg.Origin, new XYZ(cg.Origin.X, meet.Y, z), meet }, (p, q) => Rect(p, q, 300, 300, _sysRa, _rect));
            Join(End(run[0], cg.Origin), OnlyDuct(RgLiving));
            _doc.Regenerate();
            Fit(_doc.Create.NewTakeoffFitting(End(run[run.Count - 1], meet), stub));
        }

        // The fresh air trunk drawn explicitly at 150x200 (a duct taken off the
        // MSFD's connector came out on its side and the elbow would not go in).
        protected void FreshAir2()
        {
            var riser = Get<Duct>(RiserFa);
            var msfd = Get<FamilyInstance>(MsfdFa);
            var cs = Conns(msfd).OrderBy(c => c.Origin.Y).First();
            var cn = Conns(msfd).OrderBy(c => c.Origin.Y).Last();
            IntoRiser(cs, riser, double.NaN);
            double z = cn.Origin.Z;
            var start = cn.Origin;

            var cb2 = OnlyDuct(BmBed2);
            var a = new XYZ(start.X, 80600 / Ft, z);
            var trunk = Chain(new List<XYZ> { start, a, new XYZ(cb2.Origin.X, a.Y, z), new XYZ(cb2.Origin.X, cb2.Origin.Y, z) },
                              (p, q) => Rect(p, q, 150, 200, _sysFa, _rectTap));
            Join(End(trunk[0], start), Conns(msfd).OrderBy(c => c.Origin.Y).Last());
            Join(End(trunk[trunk.Count - 1], cb2.Origin), OnlyDuct(BmBed2));
            var first = trunk[0];

            var cec = OnlyDuct(EcLiving);
            var t1 = new XYZ(start.X, cec.Origin.Y, z);
            var wl = Rect(t1, new XYZ(cec.Origin.X, cec.Origin.Y, z), 150, 200, _sysFa, _rectTap);
            _doc.Regenerate();
            Fit(_doc.Create.NewTakeoffFitting(End(wl, t1), first));
            _doc.Regenerate();
            Join(End(wl, cec.Origin), OnlyDuct(EcLiving));

            // master bellmouth through its own MSFD on the riser's east face
            var cbm = OnlyDuct(BmMaster);
            var sym = msfd.Symbol;
            var lvl = _doc.GetElement(_lvl) as Level;
            var m2 = _doc.Create.NewFamilyInstance(new XYZ(-92300 / Ft, cbm.Origin.Y, lvl.ProjectElevation), sym, lvl,
                                                   Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            _made.Add(m2.Id);
            m2.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(2850 / Ft);
            m2.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set("MSFD");
            m2.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM)?.Set(msfd.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).AsInteger());
            _doc.Regenerate();
            var cc = Conns(m2).ToList();
            if (Math.Abs(cc[0].Origin.X - cc[1].Origin.X) < 1e-3)
            {
                var lp = ((LocationPoint)m2.Location).Point;
                ElementTransformUtils.RotateElement(_doc, m2.Id, Line.CreateBound(lp, lp + XYZ.BasisZ), Math.PI / 2);
                _doc.Regenerate();
            }
            IntoRiser(Conns(m2).OrderBy(c => c.Origin.X).First(), riser, cbm.Origin.Y);
            var me = Conns(m2).OrderBy(c => c.Origin.X).Last();
            var toBm = Rect(me.Origin, cbm.Origin, 150, 200, _sysFa, _rectTap);
            Join(End(toBm, me.Origin), Conns(m2).OrderBy(c => c.Origin.X).Last());
            Join(End(toBm, cbm.Origin), OnlyDuct(BmMaster));
        }

        // ------------------------------------------------------------ chilled water
        Pipe Pp(XYZ a, XYZ b, double dia, ElementId sys)
        {
            var p = Pipe.Create(_doc, sys, _pipeType, _lvl, a, b);
            p.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(dia / Ft);
            _made.Add(p.Id);
            return p;
        }
        List<Pipe> PChain(IList<XYZ> pts, double dia, ElementId sys)
        {
            var l = new List<Pipe>();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var p = Pp(pts[i], pts[i + 1], dia, sys);
                if (l.Count > 0) { _doc.Regenerate(); Fit(_doc.Create.NewElbowFitting(End(l[l.Count - 1], pts[i]), End(p, pts[i]))); }
                l.Add(p);
            }
            return l;
        }

        void Chw()
        {
            _doc.Delete(new[] { new ElementId(ChwsCap), new ElementId(ChwrCap) });
            _doc.Regenerate();
            var sPipe = Get<Pipe>(ChwsPipe);
            var rPipe = Get<Pipe>(ChwrPipe);
            double zMain = ((LocationCurve)sPipe.Location).Curve.GetEndPoint(0).Z;
            double yS = ((LocationCurve)sPipe.Location).Curve.GetEndPoint(0).Y;
            double yR = ((LocationCurve)rPipe.Location).Curve.GetEndPoint(0).Y;
            double mainDia = sPipe.Diameter * Ft;

            var mS = Water(FcuMaster, PipeSystemType.SupplyHydronic); var mR = Water(FcuMaster, PipeSystemType.ReturnHydronic);
            var bS = Water(FcuBed2, PipeSystemType.SupplyHydronic); var bR = Water(FcuBed2, PipeSystemType.ReturnHydronic);
            var lS = Water(FcuLiving, PipeSystemType.SupplyHydronic); var lR = Water(FcuLiving, PipeSystemType.ReturnHydronic);
            L($"   CHW conns: master S {S(mS.Origin)} R {S(mR.Origin)}; bed2 S {S(bS.Origin)} R {S(bR.Origin)}; living S {S(lS.Origin)} R {S(lR.Origin)}");

            // Supply rises at each tee to clear the return main beside it;
            // return branches leave level and rise at the unit.
            // FCU-07's connectors face east (+X): its branches come up east of
            // it and turn back west onto them.
            Main(sPipe, yS, zMain, mainDia, _sysChws, new[] { -91700 / Ft, bS.Origin.X }, lS.Origin.X + 180 / Ft, true,
                new Func<XYZ, List<XYZ>>[]
                {
                    t => new List<XYZ> { t, new XYZ(t.X, t.Y, mS.Origin.Z), new XYZ(t.X, mS.Origin.Y, mS.Origin.Z), mS.Origin },
                    t => new List<XYZ> { t, new XYZ(t.X, t.Y, bS.Origin.Z), bS.Origin },
                    t => new List<XYZ> { t, new XYZ(t.X, lS.Origin.Y, t.Z), new XYZ(t.X, lS.Origin.Y, lS.Origin.Z), lS.Origin },
                },
                new[] { mS, bS, lS }, new long[] { FcuMaster, FcuBed2, FcuLiving });
            Main(rPipe, yR, zMain, mainDia, _sysChwr, new[] { -91600 / Ft, bR.Origin.X }, lR.Origin.X + 280 / Ft, false,
                new Func<XYZ, List<XYZ>>[]
                {
                    t => new List<XYZ> { t, new XYZ(t.X, mR.Origin.Y, t.Z), new XYZ(t.X, mR.Origin.Y, mR.Origin.Z), mR.Origin },
                    t => new List<XYZ> { t, new XYZ(t.X, bR.Origin.Y - 300 / Ft, t.Z), new XYZ(t.X, bR.Origin.Y - 300 / Ft, bR.Origin.Z), bR.Origin },
                    t => new List<XYZ> { t, new XYZ(t.X, lR.Origin.Y, t.Z), new XYZ(t.X, lR.Origin.Y, lR.Origin.Z), lR.Origin },
                },
                new[] { mR, bR, lR }, new long[] { FcuMaster, FcuBed2, FcuLiving });
        }

        // Extends the existing main to the first tee, adds the segments
        // between tees, and runs each branch onto its FCU connector.
        void Main(Pipe existing, double y, double z, double mainDia, ElementId sys, double[] teeX, double endX, bool supply,
                  Func<XYZ, List<XYZ>>[] branch, Connector[] at, long[] fcu)
        {
            var lc = (LocationCurve)existing.Location;
            var c = lc.Curve;
            // keep the line's direction (EQUIPLINE.MovingIsStart's rule)
            var tee0 = new XYZ(teeX[0], y, z);
            bool startIsFar = c.GetEndPoint(0).X > c.GetEndPoint(1).X;
            lc.Curve = startIsFar ? Line.CreateBound(c.GetEndPoint(0), tee0) : Line.CreateBound(tee0, c.GetEndPoint(1));
            _doc.Regenerate();

            Pipe prev = existing;
            var pts = teeX.Select(x => new XYZ(x, y, z)).Concat(new[] { new XYZ(endX, y, z) }).ToList();
            for (int i = 0; i < pts.Count; i++)
            {
                var here = pts[i];
                double bdia = at[i].Radius * 2 * Ft;
                var route = branch[i](here);
                var br = PChain(route, bdia, sys);
                _doc.Regenerate();
                if (i < pts.Count - 1)
                {
                    var next = Pp(here, pts[i + 1], mainDia, sys);
                    _doc.Regenerate();
                    Fit(_doc.Create.NewTeeFitting(End(prev, here), End(next, here), End(br[0], here)));
                    prev = next;
                }
                else
                {
                    Fit(_doc.Create.NewElbowFitting(End(prev, here), End(br[0], here)));
                }
                _doc.Regenerate();
                var last = br[br.Count - 1];
                var tgt = Water(fcu[i], supply ? PipeSystemType.SupplyHydronic : PipeSystemType.ReturnHydronic);
                End(last, tgt.Origin).ConnectTo(tgt);
            }
        }

        // ------------------------------------------------------------ insulation
        void Insulate()
        {
            var di = Get<DuctInsulation>(TmplDuctIns);
            var pi = Get<PipeInsulation>(TmplPipeIns);
            int n = 0, f = 0;
            foreach (var id in _made.Distinct().ToList())
            {
                var e = _doc.GetElement(id);
                if (e == null) continue;
                var cat = (BuiltInCategory)e.Category.Id.Value;
                using (var st = new SubTransaction(_doc))
                {
                    try
                    {
                        st.Start();
                        if (di != null && (cat == BuiltInCategory.OST_DuctCurves || cat == BuiltInCategory.OST_DuctFitting || cat == BuiltInCategory.OST_FlexDuctCurves))
                        { DuctInsulation.Create(_doc, id, di.GetTypeId(), di.Thickness); n++; }
                        else if (pi != null && (cat == BuiltInCategory.OST_PipeCurves || cat == BuiltInCategory.OST_PipeFitting))
                        { PipeInsulation.Create(_doc, id, pi.GetTypeId(), pi.Thickness); n++; }
                        st.Commit();
                    }
                    catch { if (st.HasStarted() && !st.HasEnded()) st.RollBack(); f++; }
                }
            }
            L($"Insulation: {n} added (duct wrap {(di?.Thickness * Ft):0} mm, pipe {(pi?.Thickness * Ft):0} mm), {f} refused");
        }

        // ------------------------------------------------------------ report
        void Report()
        {
            L();
            L("== AS BUILT (read back after commit) ==");
            var alive = _made.Distinct().Select(i => _doc.GetElement(i)).Where(e => e != null).ToList();
            foreach (var g in alive.GroupBy(e => e.Category?.Name ?? "?"))
                L($"  {g.Count(),3}  {g.Key}");
            L($"  {_made.Distinct().Count() - alive.Count} created element(s) gone after commit");
            L();
            L("Open ends on what was made:");
            foreach (var e in alive)
                foreach (var c in Conns(e))
                    if (c.ConnectorType == ConnectorType.End && !c.IsConnected)
                        L($"  {e.Id.Value} {e.Category?.Name} open at {S(c.Origin)}");
            L();
            L("Stage A elements still unconnected:");
            foreach (var id in new[] { FcuMaster, SgMaster, RgMaster, SldDressing, BmMaster, FcuBed2, SgBed2, RgBed2, BmBed2, FcuLiving, SgLiving, RgLiving, SldKitchen, EldKitchen, EcLiving, ExMasterEns, ExEns2, ExPowder, ExUtility, MsfdFa, MsfdEa })
            {
                var e = Get<Element>(id);
                if (e == null) { L($"  {id} GONE"); continue; }
                foreach (var c in Conns(e))
                    if ((c.Domain == Domain.DomainHvac || c.Domain == Domain.DomainPiping) && !c.IsConnected)
                        L($"  {id} {(e as FamilyInstance)?.Symbol.FamilyName} {c.Domain} {(c.Domain == Domain.DomainHvac ? c.DuctSystemType.ToString() : c.PipeSystemType.ToString())} at {S(c.Origin)}");
            }
        }

        protected bool FollowUpDone()
            => Air(FcuLiving, DuctSystemType.ReturnAir).IsConnected || OnlyDuct(BmBed2).IsConnected;

        // Warnings are cleared; an error rolls back only the SubTransaction
        // that caused it (it is reported, never resolved by deleting).
        class Quiet : IFailuresPreprocessor
        {
            readonly Route _r;
            public string Errors = "";
            public Quiet(Route r) { _r = r; }
            public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
            {
                bool error = false;
                foreach (var m in a.GetFailureMessages())
                {
                    if (m.GetSeverity() == FailureSeverity.Warning) { a.DeleteWarning(m); continue; }
                    error = true;
                    Errors += m.GetDescriptionText() + " ";
                }
                return error ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
            }
        }
    }

    // The two routes the second stage B run refused, on their own; everything
    // that went in stays as it is.
    [Transaction(TransactionMode.Manual)]
    public class RouteFix : Route
    {
        protected override string Guard()
            => FollowUpDone() ? "The living return or the fresh air is already connected - nothing done." : null;

        protected override IEnumerable<(string, Action)> Steps()
        {
            yield return ("return living (stub and side tap, as 1BDR-C's FCU-07)", () => LivingReturn2());
            yield return ("fresh air trunk, both MSFDs, risers, bellmouths and endcap", () => FreshAir2());
        }
    }
}
