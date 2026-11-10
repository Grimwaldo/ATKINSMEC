using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // The Tags checker's one walk (user, 2026-11-10: "checks for
    // annotations or tags clashing or overlapping each other"): every tag,
    // room / space / area tag and text note in the views on the sheets in
    // scope, measured as a box on paper, and which of them overlap. Done
    // once per run and left in QACONTEXT.Shared, so its three checks do
    // not each measure the model again. The overlap arithmetic is
    // QATAGCLASH (Revit-free, tested).
    //
    // WHAT IS MEASURED IS A TAG'S HEAD, never its leader: a leader running
    // past another tag is how tags are drawn, not a clash.
    //  - Text notes: their own corner, width and height (exact, turned or
    //    not).
    //  - Tags, room / space / area tags: Revit's box round the element,
    //    which takes in the leader. Their leaders are turned off in a
    //    transaction that is ROLLED BACK - nothing is ever saved, nothing
    //    goes on the undo list - and the boxes read in between. (The
    //    head's own box, IndependentTag.GetTagBoxMiddlePoints, is in the
    //    2025-2027 API docs but not public in any of them.) That box is
    //    square to the MODEL, so in a turned view it is turned back
    //    (QATAGCLASH.FromModelBox); near 45 degrees it cannot be, and those
    //    findings say they were measured roughly.
    public static class QATAGS
    {
        private const string SharedKey = "tags checker";

        // One view on a sheet and what overlaps in it
        public class Seen
        {
            public View View;
            public string SheetNumber = string.Empty;
            public string SheetName = string.Empty;

            public List<QATAGCLASH.Clash> Clashes = new List<QATAGCLASH.Clash>();

            // What each annotation is called in the findings, by id
            public Dictionary<long, string> Labels = new Dictionary<long, string>();

            // For the log: what the view held, how many were measured,
            // how many the annotation crop hid, how many were rough
            public int Collected;
            public int Measured;
            public int Outside;
            public int Rough;
        }

        public class Survey
        {
            public List<Seen> Views = new List<Seen>();

            // Annotations measured, and of those measured roughly
            public int Measured;
            public int Rough;

            // Tags whose leader could not be set aside, so their box
            // takes in the leader too
            public int LeadersKept;

            // Why the leaders could not be set aside at all, if not
            public string Trouble = string.Empty;

            // The warning about either of those is given once, by
            // whichever check comes first
            public bool Told;
        }

        public static Survey Of(Document doc, QACONTEXT context, QASETTINGS settings)
        {
            object cached;

            if (context.Shared.TryGetValue(SharedKey, out cached) && cached is Survey)
                return (Survey)cached;

            Survey survey = Walk(doc, context, settings ?? new QASETTINGS());

            context.Shared[SharedKey] = survey;

            return survey;
        }

        // ==================================================
        // The walk
        // ==================================================

        // A tag whose box has to come from Revit's box round it
        private class Pending
        {
            public Element Element;
            public View View;
            public Paper Paper;
            public double Angle;
            public List<string> Tagged;
            public List<QATAGCLASH.Box> Into;
        }

        private static Survey Walk(Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var survey = new Survey();

            var boxesOf = new List<KeyValuePair<Seen, List<QATAGCLASH.Box>>>();
            var pending = new List<Pending>();

            // Tags with a leader, by id: set aside once however many views
            // show them
            var leaders = new Dictionary<long, Element>();

            foreach (KeyValuePair<View, ViewSheet> placed in ViewsOf(doc, context))
            {
                View view = placed.Key;

                List<Element> found;

                try
                {
                    found = AnnotationsIn(doc, view);
                }
                catch
                {
                    // a view Revit will not collect from has nothing to say
                    continue;
                }

                var seen = new Seen { View = view, Collected = found.Count };

                SheetOf(view, placed.Value, seen);

                var paper = new Paper(view);
                var boxes = new List<QATAGCLASH.Box>();

                survey.Views.Add(seen);
                boxesOf.Add(new KeyValuePair<Seen, List<QATAGCLASH.Box>>(seen, boxes));

                foreach (Element element in found)
                {
                    long id = element.Id.ToValue();

                    seen.Labels[id] = Label(element);

                    try
                    {
                        var text = element as TextNote;

                        if (text != null)
                        {
                            QATAGCLASH.Box box = TextBox(text, paper);

                            if (box != null)
                                boxes.Add(box);

                            continue;
                        }

                        var tag = element as IndependentTag;

                        if (tag != null)
                        {
                            pending.Add(new Pending
                            {
                                Element = tag,
                                View = view,
                                Paper = paper,
                                Angle = AngleOf(tag),
                                Tagged = TaggedOf(tag),
                                Into = boxes
                            });

                            if (Leader(tag))
                                leaders[id] = tag;

                            continue;
                        }

                        var spatial = element as SpatialElementTag;

                        if (spatial != null)
                        {
                            pending.Add(new Pending
                            {
                                Element = spatial,
                                View = view,
                                Paper = paper,
                                Angle = AngleOf(spatial),
                                Tagged = TaggedOf(spatial),
                                Into = boxes
                            });

                            if (Leader(spatial))
                                leaders[id] = spatial;
                        }
                    }
                    catch
                    {
                        // an annotation that cannot be measured is left out
                    }
                }
            }

            MeasureAside(doc, pending, leaders, survey);

            double tolerance = Math.Max(0, settings.TagOverlapMm);

            foreach (KeyValuePair<Seen, List<QATAGCLASH.Box>> pair in boxesOf)
            {
                Func<QATAGCLASH.Box, bool> shown = CropOf(pair.Key.View, new Paper(pair.Key.View));

                List<QATAGCLASH.Box> boxes = shown == null
                    ? pair.Value
                    : pair.Value.Where(shown).ToList();

                survey.Measured += boxes.Count;
                survey.Rough += boxes.Count(b => b.Rough);

                pair.Key.Measured = boxes.Count;
                pair.Key.Outside = pair.Value.Count - boxes.Count;
                pair.Key.Rough = boxes.Count(b => b.Rough);

                pair.Key.Clashes = QATAGCLASH.Find(boxes, tolerance);
            }

            Log(survey, leaders.Count, tolerance);

            return survey;
        }

        // ATKINSMEC_tags_log.txt (LOGS): every view looked in, what it held
        // and what overlapped - for a real model that disagrees with what
        // the window says. Overwritten each run; never allowed to stop it.
        private static void Log(Survey survey, int withLeaders, double tolerance)
        {
            try
            {
                var lines = new System.Text.StringBuilder();

                lines.AppendLine("Atkins MEP Toolkit - SHEET QA tags checker");
                lines.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                lines.AppendLine("Overlapping by more than " + tolerance.ToString("0.0") + " mm on paper");
                lines.AppendLine(survey.Views.Count + " view(s), " + survey.Measured + " annotation(s) measured, " +
                                 survey.Rough + " roughly (turned views)");
                lines.AppendLine(withLeaders + " tag(s) with a leader set aside to measure the head; " +
                                 survey.LeadersKept + " measured leader and all" +
                                 (survey.Trouble.Length > 0 ? " - " + survey.Trouble : string.Empty));

                foreach (Seen seen in survey.Views)
                {
                    lines.AppendLine();

                    string turned = string.Empty;

                    try
                    {
                        XYZ right = seen.View.RightDirection;

                        double degrees = Math.Atan2(right.Y, right.X) * 180 / Math.PI;

                        if (Math.Abs(degrees) > 0.01 && Math.Abs(right.Z) < 1e-9)
                            turned = ", turned " + degrees.ToString("0.#") + " deg";
                    }
                    catch
                    {
                    }

                    lines.AppendLine("=== " + (seen.SheetNumber.Length > 0 ? seen.SheetNumber + " - " : string.Empty) +
                                     seen.View.Name + "  (" + seen.View.ViewType + ", 1:" + seen.View.Scale + turned + ")");
                    lines.AppendLine("    " + seen.Collected + " annotation(s), " + seen.Measured + " measured, " +
                                     seen.Outside + " outside the annotation crop, " + seen.Rough + " rough, " +
                                     seen.Clashes.Count + " overlap(s)");

                    foreach (QATAGCLASH.Clash clash in seen.Clashes)
                    {
                        string a, b;

                        seen.Labels.TryGetValue(clash.A.Id, out a);
                        seen.Labels.TryGetValue(clash.B.Id, out b);

                        lines.AppendLine("    " + clash.Depth.ToString("0.00") + " mm  " +
                                         clash.A.Id + " " + a + "  /  " + clash.B.Id + " " + b +
                                         (clash.SameElement ? "  [same element]" : string.Empty) +
                                         (clash.A.Rough || clash.B.Rough ? "  [rough]" : string.Empty));
                    }
                }

                System.IO.File.WriteAllText(LOGS.PathOf(LogName), lines.ToString());
            }
            catch
            {
                // a log that cannot be written is not worth an error
            }
        }

        public const string LogName = "ATKINSMEC_tags_log.txt";

        // Revit's boxes round the tags that need them, with the leaders put
        // aside first in a transaction that is never committed
        private static void MeasureAside(
            Document doc, List<Pending> pending, Dictionary<long, Element> leaders, Survey survey)
        {
            if (pending.Count == 0)
                return;

            Transaction aside = null;

            var heads = new Dictionary<long, XYZ>();

            try
            {
                if (leaders.Count > 0)
                {
                    try
                    {
                        aside = new Transaction(doc, "Tags checker - measuring tag heads");

                        if (aside.Start() != TransactionStatus.Started)
                        {
                            aside.Dispose();
                            aside = null;

                            survey.Trouble = "Revit would not start a transaction to set the leaders aside";
                        }
                    }
                    catch (Exception ex)
                    {
                        if (aside != null)
                            aside.Dispose();

                        aside = null;

                        survey.Trouble = ex.Message;
                    }

                    if (aside == null)
                    {
                        survey.LeadersKept = leaders.Count;
                    }
                    else
                    {
                        foreach (KeyValuePair<long, Element> leader in leaders)
                        {
                            Element element = leader.Value;

                            try
                            {
                                // Where the head is now: turning a leader
                                // off can move it (a room tag outside its
                                // room goes back in), and the box is then
                                // read where it went
                                XYZ head = HeadOf(element);

                                SetLeader(element, false);

                                if (head != null)
                                    heads[leader.Key] = head;
                            }
                            catch
                            {
                                // a pinned room tag whose head stands
                                // outside its room, say
                                survey.LeadersKept++;
                            }
                        }

                        try
                        {
                            doc.Regenerate();
                        }
                        catch (Exception ex)
                        {
                            survey.Trouble = ex.Message;
                        }
                    }
                }

                foreach (Pending item in pending)
                {
                    try
                    {
                        // A head that moved when its leader went is put back
                        // where it was, box and all - the same text, moved
                        XYZ back = XYZ.Zero;
                        XYZ before;

                        if (heads.TryGetValue(item.Element.Id.ToValue(), out before))
                        {
                            XYZ after = HeadOf(item.Element);

                            if (after != null)
                                back = before - after;
                        }

                        QATAGCLASH.Box box = FromModelBox(item, back);

                        if (box == null)
                            continue;

                        box.Tagged = item.Tagged;

                        item.Into.Add(box);
                    }
                    catch
                    {
                        // a tag with no box is left out
                    }
                }
            }
            finally
            {
                if (aside != null)
                {
                    try
                    {
                        if (aside.HasStarted() && !aside.HasEnded())
                            aside.RollBack();
                    }
                    catch
                    {
                        // nothing was ever committed either way
                    }

                    aside.Dispose();
                }
            }
        }

        // ==================================================
        // Which views
        // ==================================================

        // The views in scope; a sheet stands for the views placed on it
        // (the view open now may be a sheet)
        private static List<KeyValuePair<View, ViewSheet>> ViewsOf(Document doc, QACONTEXT context)
        {
            var views = new List<KeyValuePair<View, ViewSheet>>();
            var done = new HashSet<long>();

            foreach (View view in context.Views)
            {
                var sheet = view as ViewSheet;

                if (sheet != null)
                {
                    foreach (ElementId id in sheet.GetAllPlacedViews())
                    {
                        var placed = doc.GetElement(id) as View;

                        if (Checkable(placed) && done.Add(placed.Id.ToValue()))
                            views.Add(new KeyValuePair<View, ViewSheet>(placed, sheet));
                    }

                    continue;
                }

                if (Checkable(view) && done.Add(view.Id.ToValue()))
                    views.Add(new KeyValuePair<View, ViewSheet>(view, context.SheetFor(view)));
            }

            return views;
        }

        // Views that draw annotation on a sheet. Schedules have none of
        // these elements; a perspective has no paper size.
        private static bool Checkable(View view)
        {
            if (view == null || view.IsTemplate)
                return false;

            var view3D = view as View3D;

            if (view3D != null && view3D.IsPerspective)
                return false;

            switch (view.ViewType)
            {
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.EngineeringPlan:
                case ViewType.AreaPlan:
                case ViewType.Section:
                case ViewType.Elevation:
                case ViewType.Detail:
                case ViewType.DraftingView:
                case ViewType.Legend:
                case ViewType.ThreeD:
                    return true;

                default:
                    return false;
            }
        }

        private static void SheetOf(View view, ViewSheet sheet, Seen seen)
        {
            if (sheet != null)
            {
                seen.SheetNumber = sheet.SheetNumber ?? string.Empty;
                seen.SheetName = sheet.Name ?? string.Empty;

                return;
            }

            // The view open now: whatever sheet it is on, if it is on one
            seen.SheetNumber = Text(view, BuiltInParameter.VIEWPORT_SHEET_NUMBER);
            seen.SheetName = Text(view, BuiltInParameter.VIEWPORT_SHEET_NAME);
        }

        private static string Text(Element element, BuiltInParameter which)
        {
            try
            {
                Parameter parameter = element.get_Parameter(which);

                string value = parameter == null ? null : parameter.AsString();

                return value == null || value == "---" ? string.Empty : value;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static List<Element> AnnotationsIn(Document doc, View view)
        {
            var found = new List<Element>();

            found.AddRange(new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .ToElements());

            found.AddRange(new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(TextNote))
                .ToElements());

            // Room, space and area tags are not IndependentTags, and their
            // own classes cannot be collected by class
            var spatial = new ElementMulticategoryFilter(new List<BuiltInCategory>
            {
                BuiltInCategory.OST_RoomTags,
                BuiltInCategory.OST_MEPSpaceTags,
                BuiltInCategory.OST_AreaTags
            });

            found.AddRange(new FilteredElementCollector(doc, view.Id)
                .WherePasses(spatial)
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(e => e is SpatialElementTag));

            return found;
        }

        // ==================================================
        // Measuring
        // ==================================================

        // A view's paper: model feet to millimetres on the sheet, along the
        // view's own right and up
        private class Paper
        {
            public readonly XYZ Origin;
            public readonly XYZ Right;
            public readonly XYZ Up;
            public readonly double Mm;

            public Paper(View view)
            {
                Origin = view.Origin;
                Right = view.RightDirection;
                Up = view.UpDirection;

                int scale = 1;

                try
                {
                    scale = view.Scale;
                }
                catch
                {
                }

                Mm = 304.8 / (scale > 0 ? scale : 1);
            }

            public double X(XYZ point)
            {
                return (point - Origin).DotProduct(Right) * Mm;
            }

            public double Y(XYZ point)
            {
                return (point - Origin).DotProduct(Up) * Mm;
            }
        }

        private static QATAGCLASH.Box TextBox(TextNote text, Paper paper)
        {
            XYZ top = text.Coord;
            XYZ along = text.BaseDirection;
            XYZ up = text.UpDirection;

            if (top == null || along == null || up == null)
                return null;

            QATAGCLASH.Align align;

            switch (text.HorizontalAlignment)
            {
                case HorizontalTextAlignment.Right:
                    align = QATAGCLASH.Align.Right;
                    break;

                case HorizontalTextAlignment.Center:
                    align = QATAGCLASH.Align.Centre;
                    break;

                default:
                    align = QATAGCLASH.Align.Left;
                    break;
            }

            // Width and height are paper feet already (sheet units)
            return QATAGCLASH.FromText(
                text.Id.ToValue(),
                paper.X(top), paper.Y(top),
                along.DotProduct(paper.Right), along.DotProduct(paper.Up),
                up.DotProduct(paper.Right), up.DotProduct(paper.Up),
                text.Width * 304.8, text.Height * 304.8,
                align);
        }

        private static QATAGCLASH.Box FromModelBox(Pending item, XYZ back)
        {
            BoundingBoxXYZ box = item.Element.get_BoundingBox(item.View);

            if (box == null || box.Min == null || box.Max == null)
                return null;

            Paper paper = item.Paper;

            // Square to the model's axes, always (Element.BoundingBox)
            XYZ middle = (box.Min + box.Max) / 2 + back;
            XYZ size = box.Max - box.Min;

            return QATAGCLASH.FromModelBox(
                item.Element.Id.ToValue(), QATAGCLASH.Kind.Tag,
                paper.X(middle), paper.Y(middle),
                new[] { Math.Abs(size.X) * paper.Mm, Math.Abs(size.Y) * paper.Mm, Math.Abs(size.Z) * paper.Mm },
                new[] { paper.Right.X, paper.Right.Y, paper.Right.Z },
                new[] { paper.Up.X, paper.Up.Y, paper.Up.Z },
                item.Angle);
        }

        // Which way a tag's head reads in its view, radians anticlockwise.
        // RotationAngle (2022 on) where Revit gives one; a vertical tag
        // reading 0 there is still vertical.
        private static double AngleOf(IndependentTag tag)
        {
            bool vertical = false;

            try
            {
                vertical = tag.TagOrientation == TagOrientation.Vertical;
            }
            catch
            {
            }

            return Angle(() => tag.RotationAngle, vertical);
        }

        private static double AngleOf(SpatialElementTag tag)
        {
            bool vertical = false;

            try
            {
                vertical = tag.TagOrientation == SpatialElementTagOrientation.Vertical;
            }
            catch
            {
            }

            return Angle(() => tag.RotationAngle, vertical);
        }

        private static double Angle(Func<double> rotation, bool vertical)
        {
            double angle = 0;

            try
            {
                angle = rotation();
            }
            catch
            {
                angle = 0;
            }

            if (double.IsNaN(angle) || double.IsInfinity(angle))
                angle = 0;

            if (vertical && Math.Abs(Math.Sin(angle)) < 1e-6)
                angle = Math.PI / 2;

            return angle;
        }

        private static XYZ HeadOf(Element element)
        {
            try
            {
                var tag = element as IndependentTag;

                if (tag != null)
                    return tag.TagHeadPosition;

                var spatial = element as SpatialElementTag;

                return spatial == null ? null : spatial.TagHeadPosition;
            }
            catch
            {
                return null;
            }
        }

        private static bool Leader(Element element)
        {
            try
            {
                var tag = element as IndependentTag;

                if (tag != null)
                    return tag.HasLeader;

                var spatial = element as SpatialElementTag;

                return spatial != null && spatial.HasLeader;
            }
            catch
            {
                return false;
            }
        }

        private static void SetLeader(Element element, bool on)
        {
            var tag = element as IndependentTag;

            if (tag != null)
            {
                tag.HasLeader = on;

                return;
            }

            var spatial = element as SpatialElementTag;

            if (spatial != null)
                spatial.HasLeader = on;
        }

        // Where the annotation crop is on, an annotation whose middle is
        // outside it does not print; null = no crop to go by. Revit's view
        // collector can pass elements a crop hides.
        private static Func<QATAGCLASH.Box, bool> CropOf(View view, Paper paper)
        {
            try
            {
                if (!view.CropBoxActive)
                    return null;

                Parameter on = view.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);

                if (on == null || on.AsInteger() != 1)
                    return null;

                BoundingBoxXYZ crop = view.CropBox;

                if (crop == null)
                    return null;

                ViewCropRegionShapeManager shape = view.GetCropRegionShapeManager();

                if (shape == null || !shape.CanHaveAnnotationCrop)
                    return null;

                Transform toModel = crop.Transform ?? Transform.Identity;

                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;

                foreach (double cx in new[] { crop.Min.X, crop.Max.X })
                {
                    foreach (double cy in new[] { crop.Min.Y, crop.Max.Y })
                    {
                        XYZ corner = toModel.OfPoint(new XYZ(cx, cy, crop.Min.Z));

                        minX = Math.Min(minX, paper.X(corner));
                        maxX = Math.Max(maxX, paper.X(corner));
                        minY = Math.Min(minY, paper.Y(corner));
                        maxY = Math.Max(maxY, paper.Y(corner));
                    }
                }

                // The annotation crop's offsets are paper feet
                double left = shape.LeftAnnotationCropOffset * 304.8;
                double right = shape.RightAnnotationCropOffset * 304.8;
                double top = shape.TopAnnotationCropOffset * 304.8;
                double bottom = shape.BottomAnnotationCropOffset * 304.8;

                return box => box.X >= minX - left && box.X <= maxX + right
                           && box.Y >= minY - bottom && box.Y <= maxY + top;
            }
            catch
            {
                return null;
            }
        }

        // ==================================================
        // What it is and what it tags
        // ==================================================

        // What a tag tags, so two tags of one element can be told apart
        // from two tags that merely touch. A material tag tags a material,
        // not the element, so it tags nothing here.
        private static List<string> TaggedOf(IndependentTag tag)
        {
            var keys = new List<string>();

            try
            {
                if (tag.IsMaterialTag)
                    return keys;

                foreach (LinkElementId id in tag.GetTaggedElementIds())
                    keys.Add(Key(id));
            }
            catch
            {
            }

            return keys;
        }

        private static List<string> TaggedOf(SpatialElementTag tag)
        {
            var keys = new List<string>();

            try
            {
                var room = tag as RoomTag;

                if (room != null)
                {
                    keys.Add(Key(room.TaggedRoomId));

                    return keys;
                }

                var space = tag as Autodesk.Revit.DB.Mechanical.SpaceTag;

                if (space != null && space.Space != null)
                {
                    keys.Add("h" + space.Space.Id.ToValue());

                    return keys;
                }

                var area = tag as AreaTag;

                if (area != null && area.Area != null)
                    keys.Add("h" + area.Area.Id.ToValue());
            }
            catch
            {
            }

            return keys;
        }

        private static string Key(LinkElementId id)
        {
            if (id == null)
                return string.Empty;

            if (id.LinkInstanceId != null && id.LinkInstanceId != ElementId.InvalidElementId)
                return "l" + id.LinkInstanceId.ToValue() + ":" + id.LinkedElementId.ToValue();

            return "h" + id.HostElementId.ToValue();
        }

        // "Duct Tags: 600x300", "Text Notes: SUPPLY AIR FROM ...": the
        // category and what it reads
        private static string Label(Element element)
        {
            string category = element.Category == null ? "Annotation" : element.Category.Name;

            string reads = null;

            try
            {
                var text = element as TextNote;

                if (text != null)
                    reads = text.Text;

                var tag = element as IndependentTag;

                if (tag != null)
                    reads = tag.TagText;

                var spatial = element as SpatialElementTag;

                if (spatial != null)
                    reads = spatial.TagText;
            }
            catch
            {
                reads = null;
            }

            reads = Tidy(reads);

            return string.IsNullOrEmpty(reads)
                ? category + " id " + element.Id.ToValue()
                : category + ": " + reads;
        }

        private static string Tidy(string reads)
        {
            if (string.IsNullOrEmpty(reads))
                return string.Empty;

            string[] lines = reads.Split(new[] { '\r', '\n', '\v' }, StringSplitOptions.RemoveEmptyEntries);

            string one = string.Join(" / ", lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToArray());

            return one.Length > 40 ? one.Substring(0, 39).TrimEnd() + "..." : one;
        }
    }
}
