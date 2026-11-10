using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // Tags that annotate an air terminal, duct or pipe carrying no flow.
    //
    // It works from the tags rather than from the elements, because the
    // question is what the drawing says: an untagged element with no flow
    // is a modelling matter, but a tag reading 0 l/s is on a sheet going
    // out of the door.
    //
    // ONLY A TAG THAT SHOWS A FLOW (user, 2026-11-10): a riser tag reading
    // "500x400 EAD FA/TB" - size and system - on a duct with no flow is
    // not wrong on the sheet. What a tag shows is decided in QAFLOWTEXT,
    // from its own text and from the other tags of its type.
    public class QAFLOWCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Flowrate; }
        }

        public string Name
        {
            get { return "Tags reading zero flow"; }
        }

        public string Explanation
        {
            get
            {
                return "Air terminal, duct and pipe tags showing a flow " +
                       "whose element has none. These print as 0 on the " +
                       "sheet. A tag showing no flow (a size, a system) " +
                       "is left out.";
            }
        }

        // A tag on an element with no flow, before it is known whether
        // the tag shows that flow
        private class Candidate
        {
            public View View;
            public ViewSheet Sheet;
            public IndependentTag Tag;
            public Element Tagged;
            public string Units;
            public bool Assigned;

            // What the tag reads; null when Revit would not say
            public string Text;
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var candidates = new List<Candidate>();

            // A tag can appear in more than one view; each appearance is
            // its own tag element, so nothing is double counted. The same
            // ELEMENT tagged twice is reported twice on purpose - both
            // sheets are wrong.
            foreach (View view in context.Views)
            {
                ViewSheet sheet = context.SheetFor(view);

                foreach (IndependentTag tag in QACHECKS.TagsIn(doc, view))
                {
                    string text = null;
                    bool read = false;

                    foreach (ElementId taggedId in TaggedIds(tag))
                    {
                        Element tagged = doc.GetElement(taggedId);

                        if (tagged == null)
                            continue;

                        string units;
                        bool assigned;

                        double? flow = FlowOf(
                            tagged, out units, out assigned);

                        // Not a flow-carrying element, so not this
                        // check's business
                        if (flow == null)
                            continue;

                        if (Math.Abs(flow.Value) > 1e-9)
                            continue;

                        if (!read)
                        {
                            text = TextOf(tag);
                            read = true;
                        }

                        candidates.Add(new Candidate
                        {
                            View = view,
                            Sheet = sheet,
                            Tag = tag,
                            Tagged = tagged,
                            Units = units,
                            Assigned = assigned,
                            Text = text
                        });
                    }
                }
            }

            // The other tags of a type are read only where the tag's own
            // text did not settle it
            var unsettled = new HashSet<long>();

            foreach (Candidate c in candidates)
            {
                if (c.Text == null || !QAFLOWTEXT.ReadsZero(c.Text))
                    unsettled.Add(c.Tag.GetTypeId().ToValue());
            }

            Dictionary<long, QAFLOWTEXT.Evidence> evidence =
                Learn(doc, unsettled);

            var findings = new List<QAFinding>();

            foreach (Candidate c in candidates)
            {
                QAFLOWTEXT.Evidence ofType;

                evidence.TryGetValue(
                    c.Tag.GetTypeId().ToValue(), out ofType);

                if (!QAFLOWTEXT.Report(c.Text, ofType))
                    continue;

                // Zero and unset print the same on the sheet but are
                // put right differently: a zero means the system
                // calculated nothing, an unset one usually means the
                // element is in no system at all
                string problem = c.Assigned
                    ? "flow is zero"
                    : "flow is not set - is it on a system?";

                if (c.Text != null)
                {
                    string reads = c.Text.Replace("\r", " ").Replace("\n", " ").Trim();

                    problem += reads.Length == 0
                        ? " (the tag reads nothing)"
                        : " (the tag reads \"" + reads + "\")";
                }

                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Issue,
                    SheetNumber = c.Sheet == null
                        ? string.Empty
                        : c.Sheet.SheetNumber,
                    SheetName = c.Sheet == null
                        ? string.Empty
                        : c.Sheet.Name,
                    ViewName = c.View.Name,
                    ViewId = c.View.Id.ToValue(),

                    // The tag is what is wrong on the drawing
                    ElementId = c.Tag.Id.ToValue(),

                    Element = QACHECKS.DescribeElement(c.Tagged),

                    Problem = problem,

                    Value = c.Units
                });
            }

            return findings;
        }

        // What the tag prints, every label run together (TagText), or
        // null when Revit will not say - it throws for a tag with no
        // valid text
        private static string TextOf(IndependentTag tag)
        {
            try
            {
                return tag.TagText ?? string.Empty;
            }
            catch
            {
                return null;
            }
        }

        // For each tag type asked about: its tags anywhere in the model
        // on ONE element of this model that carries a flow, and how many
        // of them read it. At most QAFLOWTEXT.EvidenceCap each.
        private static Dictionary<long, QAFLOWTEXT.Evidence> Learn(
            Document doc, HashSet<long> typeIds)
        {
            var result = new Dictionary<long, QAFLOWTEXT.Evidence>();

            foreach (long id in typeIds)
                result[id] = new QAFLOWTEXT.Evidence();

            if (result.Count == 0)
                return result;

            int open = result.Count;

            var tags = new FilteredElementCollector(doc)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>();

            foreach (IndependentTag tag in tags)
            {
                QAFLOWTEXT.Evidence ofType;

                if (!result.TryGetValue(tag.GetTypeId().ToValue(), out ofType) ||
                    ofType.Seen >= QAFLOWTEXT.EvidenceCap)
                {
                    continue;
                }

                List<ElementId> ids = TaggedIds(tag).ToList();

                if (ids.Count != 1)
                    continue;

                string units;
                bool assigned;

                double? flow = FlowOf(
                    doc.GetElement(ids[0]), out units, out assigned);

                if (flow == null || !assigned || Math.Abs(flow.Value) <= 1e-9)
                    continue;

                string text = TextOf(tag);

                if (text == null)
                    continue;

                ofType.Seen++;

                if (QAFLOWTEXT.Shows(text, flow.Value, units))
                    ofType.Showed++;

                if (ofType.Seen >= QAFLOWTEXT.EvidenceCap && --open == 0)
                    break;
            }

            return result;
        }

        // Only local elements. A tag pointing into a linked model reads
        // that model's value and is not this model's defect to fix.
        private static IEnumerable<ElementId> TaggedIds(IndependentTag tag)
        {
            try
            {
                return tag.GetTaggedLocalElementIds();
            }
            catch
            {
                return new List<ElementId>();
            }
        }

        // The flow on an air terminal, duct or pipe, or null when the
        // element carries no flow parameter at all.
        //
        // assigned says whether Revit had a value for it. See the note
        // in the body: unset is NOT the same as "not this check's
        // business", and treating it as such is what hid a whole class
        // of zero-reading tags.
        private static double? FlowOf(
            Element element, out string shown, out bool assigned)
        {
            shown = string.Empty;
            assigned = false;

            if (element == null || element.Category == null)
                return null;

            long category = element.Category.Id.ToValue();

            BuiltInParameter which;

            if (category == (long)BuiltInCategory.OST_DuctTerminal ||
                category == (long)BuiltInCategory.OST_DuctCurves ||
                category == (long)BuiltInCategory.OST_FlexDuctCurves ||
                category == (long)BuiltInCategory.OST_DuctFitting ||
                category == (long)BuiltInCategory.OST_DuctAccessory)
            {
                which = BuiltInParameter.RBS_DUCT_FLOW_PARAM;
            }
            else if (category == (long)BuiltInCategory.OST_PipeCurves ||
                     category == (long)BuiltInCategory.OST_FlexPipeCurves ||
                     category == (long)BuiltInCategory.OST_PipeFitting ||
                     category == (long)BuiltInCategory.OST_PipeAccessory)
            {
                which = BuiltInParameter.RBS_PIPE_FLOW_PARAM;
            }
            else
            {
                return null;
            }

            Parameter parameter = element.get_Parameter(which);

            if (parameter == null)
                return null;

            if (parameter.StorageType != StorageType.Double)
                return null;

            // A PARAMETER WITH NO ASSIGNED VALUE IS STILL A ZERO HERE.
            //
            // Parameter.HasValue is "identifies if the parameter has an
            // assigned value", and a pipe or duct that belongs to no
            // system has none - which is the commonest way of all to
            // end up with a tag printing 0 on a sheet. Skipping those
            // as though the element carried no flow at all is what kept
            // PIPE tags out of this list while duct tags appeared:
            // user-reported. The category test above has already
            // established that this element is supposed to carry flow,
            // so an unassigned value is a finding, not a reason to look
            // away.
            if (!parameter.HasValue)
            {
                shown = "not set";

                return 0.0;
            }

            assigned = true;

            // AsValueString respects the project's own units, so the
            // report reads the way the sheet does
            try
            {
                shown = parameter.AsValueString() ?? string.Empty;
            }
            catch
            {
                shown = string.Empty;
            }

            return parameter.AsDouble();
        }
    }
}
