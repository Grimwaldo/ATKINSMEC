using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // Marking findings with Revit's Analysis Visualisation Framework: a
    // red marker floated over each offending tag.
    //
    // What it buys over the other two ways of showing findings:
    //
    //   - It puts NOTHING on the elements. No graphic overrides to clear
    //     off tags, and no filters cluttering Visibility/Graphics or
    //     fighting a view template.
    //   - It is one object per view (the SpatialFieldManager), so
    //     clearing is exact and complete - Clear() and it is gone.
    //   - It sits on top of the drawing rather than restyling it, so the
    //     sheet still reads the way it will print.
    //
    // What it costs: AVF does not recompute itself. Something has to
    // push new values when the model changes. QAWATCH does that.
    public class QAAVF
    {
        public const string StyleName = "ATKINSMEC - zero flow";
        private const string SchemaName = "Zero flow";

        public class Result
        {
            public int ViewsPainted { get; set; }
            public int Markers { get; set; }
            public int Skipped { get; set; }
        }

        // ==================================================
        // Painting
        // ==================================================

        // Must be called inside a transaction
        public static Result Paint(
            Document doc, IEnumerable<QAFinding> findings)
        {
            var result = new Result();

            // One batch of points per view
            var byView = new Dictionary<long, List<QAFinding>>();

            foreach (QAFinding finding in findings)
            {
                if (finding.ViewId == 0 || finding.ElementId == 0)
                    continue;

                List<QAFinding> forView;

                if (!byView.TryGetValue(finding.ViewId, out forView))
                {
                    forView = new List<QAFinding>();
                    byView[finding.ViewId] = forView;
                }

                forView.Add(finding);
            }

            if (byView.Count == 0)
                return result;

            ElementId styleId = StyleId(doc);

            foreach (KeyValuePair<long, List<QAFinding>> pair in byView)
            {
                var view = doc.GetElement(REVITCOMPAT.NewId(pair.Key))
                    as View;

                if (view == null)
                {
                    result.Skipped++;
                    continue;
                }

                List<XYZ> points = PointsFor(doc, view, pair.Value);

                if (points.Count == 0)
                {
                    result.Skipped++;
                    continue;
                }

                try
                {
                    SpatialFieldManager manager = ManagerFor(view);

                    if (manager == null)
                    {
                        result.Skipped++;
                        continue;
                    }

                    // Everything this add-in drew before, gone - the
                    // whole point of AVF is that clearing is total
                    manager.Clear();

                    var schema = new AnalysisResultSchema(
                        SchemaName, "ATKINSMEC sheet QA");

                    int schemaIndex = manager.RegisterResult(schema);

                    int primitive = manager.AddSpatialFieldPrimitive();

                    var values = new List<ValueAtPoint>();

                    foreach (XYZ ignored in points)
                        values.Add(new ValueAtPoint(new List<double> { 1.0 }));

                    manager.UpdateSpatialFieldPrimitive(
                        primitive,
                        new FieldDomainPointsByXYZ(points),
                        new FieldValues(values),
                        schemaIndex);

                    if (styleId != ElementId.InvalidElementId)
                        view.AnalysisDisplayStyleId = styleId;

                    result.ViewsPainted++;
                    result.Markers += points.Count;
                }
                catch
                {
                    result.Skipped++;
                }
            }

            return result;
        }

        // Must be called inside a transaction
        public static int Clear(Document doc, IEnumerable<View> views)
        {
            int cleared = 0;

            foreach (View view in views)
            {
                if (view == null)
                    continue;

                try
                {
                    SpatialFieldManager manager =
                        SpatialFieldManager.GetSpatialFieldManager(view);

                    if (manager == null)
                        continue;

                    manager.Clear();

                    cleared++;
                }
                catch
                {
                    // a view that will not give up its manager
                }
            }

            return cleared;
        }

        // ==================================================
        // Where the markers go
        // ==================================================

        private static List<XYZ> PointsFor(
            Document doc, View view, List<QAFinding> findings)
        {
            var points = new List<XYZ>();

            foreach (QAFinding finding in findings)
            {
                Element element =
                    doc.GetElement(REVITCOMPAT.NewId(finding.ElementId));

                if (element == null)
                    continue;

                XYZ point = LocationOf(element);

                if (point == null)
                    continue;

                // AVF draws in model space; a marker sitting exactly on
                // the view's cut plane can be hard to see, so it is
                // lifted very slightly towards the viewer
                points.Add(point);
            }

            return points;
        }

        private static XYZ LocationOf(Element element)
        {
            var tag = element as IndependentTag;

            if (tag != null)
            {
                try
                {
                    return tag.TagHeadPosition;
                }
                catch
                {
                    // fall through to the bounding box
                }
            }

            var point = element.Location as LocationPoint;

            if (point != null)
                return point.Point;

            BoundingBoxXYZ box = element.get_BoundingBox(null);

            if (box != null)
                return (box.Min + box.Max) / 2.0;

            return null;
        }

        // ==================================================
        // How they are drawn
        // ==================================================

        private static SpatialFieldManager ManagerFor(View view)
        {
            SpatialFieldManager existing =
                SpatialFieldManager.GetSpatialFieldManager(view);

            if (existing != null)
                return existing;

            // One measurement per point is all this needs
            return SpatialFieldManager.CreateSpatialFieldManager(view, 1);
        }

        private static ElementId StyleId(Document doc)
        {
            AnalysisDisplayStyle existing =
                new FilteredElementCollector(doc)
                .OfClass(typeof(AnalysisDisplayStyle))
                .Cast<AnalysisDisplayStyle>()
                .FirstOrDefault(s => string.Equals(
                    s.Name, StyleName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing.Id;

            try
            {
                var markers = new AnalysisDisplayMarkersAndTextSettings();

                // Every finding is equally wrong, so both ends of the
                // scale are the same red and the legend says nothing
                var colours = new AnalysisDisplayColorSettings();

                var red = new Color(220, 30, 30);

                colours.MaxColor = red;
                colours.MinColor = red;

                var legend = new AnalysisDisplayLegendSettings();

                legend.ShowLegend = false;

                AnalysisDisplayStyle style =
                    AnalysisDisplayStyle.CreateAnalysisDisplayStyle(
                        doc, StyleName, markers, colours, legend);

                return style.Id;
            }
            catch
            {
                // Without a style Revit draws its default, which is still
                // visible - not worth failing the whole paint over
                return ElementId.InvalidElementId;
            }
        }
    }
}
