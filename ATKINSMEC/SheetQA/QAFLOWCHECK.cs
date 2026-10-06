using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Tags that annotate an air terminal, duct or pipe carrying no flow.
    //
    // It works from the tags rather than from the elements, because the
    // question is what the drawing says: an untagged element with no flow
    // is a modelling matter, but a tag reading 0 l/s is on a sheet going
    // out of the door.
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
                return "Air terminal, duct and pipe tags whose element " +
                       "has no flow. These print as 0 on the sheet.";
            }
        }

        public List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            // A tag can appear in more than one view; each appearance is
            // its own tag element, so nothing is double counted. The same
            // ELEMENT tagged twice is reported twice on purpose - both
            // sheets are wrong.
            foreach (View view in context.Views)
            {
                ViewSheet sheet = context.SheetFor(view);

                foreach (IndependentTag tag in QACHECKS.TagsIn(doc, view))
                {
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

                        findings.Add(new QAFinding
                        {
                            Check = Name,
                            Severity = QASeverity.Issue,
                            SheetNumber = sheet == null
                                ? string.Empty
                                : sheet.SheetNumber,
                            SheetName = sheet == null
                                ? string.Empty
                                : sheet.Name,
                            ViewName = view.Name,
                            ViewId = view.Id.ToValue(),

                            // The tag is what is wrong on the drawing
                            ElementId = tag.Id.ToValue(),

                            Element = QACHECKS.DescribeElement(tagged),

                            // Zero and unset print the same on the
                            // sheet but are put right differently: a
                            // zero means the system calculated nothing,
                            // an unset one usually means the element is
                            // in no system at all
                            Problem = assigned
                                ? "flow is zero"
                                : "flow is not set - is it on a system?",

                            Value = units
                        });
                    }
                }
            }

            return findings;
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
