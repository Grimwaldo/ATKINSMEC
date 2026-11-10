using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Colours the offending elements in the view they were found in, so
    // they can be seen on the drawing as well as read in the list.
    //
    // This uses a per-element graphic override rather than revision
    // clouds or detail lines: it creates nothing, so there is nothing to
    // hunt down afterwards, and clearing it puts the view back exactly as
    // it was. It is the one part of SHEET QA that writes to the model.
    //
    // The override IS saved with the model, though - it is not a
    // temporary view mode - so Clear matters. QAMARKS remembers what was
    // marked so clearing is exact.
    public class QAMARK
    {
        // One marked element, in one view
        public struct Spot
        {
            public long ViewId;
            public long ElementId;
        }

        public class Result
        {
            public int Marked { get; set; }
            public int Skipped { get; set; }
            public List<Spot> Spots { get; set; } = new List<Spot>();
        }

        private static OverrideGraphicSettings Highlight()
        {
            var settings = new OverrideGraphicSettings();

            var red = new Color(220, 30, 30);

            settings.SetProjectionLineColor(red);
            settings.SetProjectionLineWeight(6);
            settings.SetSurfaceForegroundPatternColor(red);

            return settings;
        }

        // Must be called inside a transaction
        public static Result Mark(
            Document doc, IEnumerable<QAFinding> findings)
        {
            var result = new Result();

            var done = new HashSet<string>();

            OverrideGraphicSettings highlight = Highlight();

            foreach (QAFinding finding in findings)
            {
                if (finding.ViewId == 0 || finding.ElementId == 0)
                {
                    result.Skipped++;
                    continue;
                }

                // Both halves of a pair (two tags on top of each other)
                foreach (long elementId in Ids(finding))
                    MarkOne(doc, finding.ViewId, elementId, highlight, done, result);
            }

            return result;
        }

        // What a finding marks: its element, and the other half of a pair
        private static IEnumerable<long> Ids(QAFinding finding)
        {
            yield return finding.ElementId;

            if (finding.OtherElementId > 0 && finding.OtherElementId != finding.ElementId)
                yield return finding.OtherElementId;
        }

        private static void MarkOne(
            Document doc, long viewId, long elementId,
            OverrideGraphicSettings highlight, HashSet<string> done, Result result)
        {
            // The same tag can be reported by two checks at once
            if (!done.Add(viewId + ":" + elementId))
                return;

            var view = doc.GetElement(REVITCOMPAT.NewId(viewId)) as View;

            if (view == null || !view.AreGraphicsOverridesAllowed())
            {
                result.Skipped++;
                return;
            }

            ElementId id = REVITCOMPAT.NewId(elementId);

            if (doc.GetElement(id) == null)
            {
                result.Skipped++;
                return;
            }

            try
            {
                view.SetElementOverrides(id, highlight);

                result.Marked++;

                result.Spots.Add(new Spot
                {
                    ViewId = viewId,
                    ElementId = elementId
                });
            }
            catch
            {
                result.Skipped++;
            }
        }

        // Must be called inside a transaction. Putting an empty override
        // back is what "no override" means.
        public static int Clear(Document doc, IEnumerable<Spot> spots)
        {
            int cleared = 0;

            var plain = new OverrideGraphicSettings();

            var done = new HashSet<string>();

            foreach (Spot spot in spots)
            {
                if (!done.Add(spot.ViewId + ":" + spot.ElementId))
                    continue;

                var view = doc.GetElement(REVITCOMPAT.NewId(spot.ViewId))
                    as View;

                if (view == null || !view.AreGraphicsOverridesAllowed())
                    continue;

                ElementId id = REVITCOMPAT.NewId(spot.ElementId);

                if (doc.GetElement(id) == null)
                    continue;

                try
                {
                    view.SetElementOverrides(id, plain);

                    cleared++;
                }
                catch
                {
                    // a view that will not take it is left alone
                }
            }

            return cleared;
        }

        public static List<Spot> SpotsOf(IEnumerable<QAFinding> findings)
        {
            var spots = new List<Spot>();

            foreach (QAFinding finding in findings)
            {
                if (finding.ViewId == 0 || finding.ElementId == 0)
                    continue;

                foreach (long elementId in Ids(finding))
                {
                    spots.Add(new Spot
                    {
                        ViewId = finding.ViewId,
                        ElementId = elementId
                    });
                }
            }

            return spots;
        }
    }
}
