using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Follows the ductwork out of one connector and collects everything
    // that should follow its size.
    //
    // It decides nothing itself: each element is described as an
    // EQUIPNODE and handed to EQUIPSTOP, which has no Revit in it and
    // is tested at scratchpad/equiptest.
    public static class EQUIPWALK
    {
        // How far a branch may run before this gives up. A duct off a
        // fan coil reaches its takeoff in two or three elements; ten is
        // already a long tail of dampers and bends, and a hundred would
        // mean the walk has escaped onto a main.
        public const int Reach = 40;

        public class Branch
        {
            // Everything to resize, nearest first, NOT including the
            // equipment itself
            public List<long> Ids = new List<long>();

            // What ended it
            public EQUIPSTOPKIND Stopped = EQUIPSTOPKIND.Cross;

            // The element it stopped at, for the report
            public long StoppedAt;

            public string StoppedName = string.Empty;

            // The connector on the equipment this came out of
            public int Index;

            public string Said
            {
                get
                {
                    return EQUIPSTOP.Why(Stopped) +
                           (string.IsNullOrEmpty(StoppedName)
                                ? string.Empty
                                : " (" + StoppedName + ")");
                }
            }
        }

        // Everything connected to one connector of the equipment, up to
        // the first thing that ends a resize.
        public static Branch From(Document doc, Connector start, int index)
        {
            var branch = new Branch { Index = index };

            if (doc == null || start == null)
                return branch;

            var seen = new HashSet<long>();

            Connector here = start;

            while (branch.Ids.Count < Reach)
            {
                Element next = Across(doc, here);

                if (next == null)
                {
                    // An open end: nothing more to resize, and nothing
                    // wrong with that
                    branch.Stopped = EQUIPSTOPKIND.Cross;

                    return branch;
                }

                long id = next.Id.ToValue();

                if (!seen.Add(id))
                {
                    // Back where it started: a loop, which is not a
                    // branch off a piece of equipment
                    branch.Stopped = EQUIPSTOPKIND.Unknown;
                    branch.StoppedAt = id;
                    branch.StoppedName = Describe(next);

                    return branch;
                }

                EQUIPNODE node = Describe(doc, next);

                EQUIPSTOPKIND verdict = EQUIPSTOP.Decide(node);

                if (verdict != EQUIPSTOPKIND.Cross)
                {
                    branch.Stopped = verdict;
                    branch.StoppedAt = id;
                    branch.StoppedName = Describe(next);

                    return branch;
                }

                branch.Ids.Add(id);

                Connector onward = Onward(next, here);

                if (onward == null)
                {
                    // A duct with nothing on its far end, or an elbow
                    // hanging open. The end of the branch.
                    return branch;
                }

                here = onward;
            }

            // Reach exhausted. Refused rather than resized: a walk this
            // long has almost certainly escaped onto a main, and
            // resizing a main because somebody swapped a fan coil is
            // the worst thing this add-in could do.
            branch.Stopped = EQUIPSTOPKIND.Unknown;
            branch.StoppedName = "more than " + Reach + " elements";

            return branch;
        }

        // The element on the other side of a connector.
        //
        // INSULATION IS IN AllRefs, AND DuctInsulation DERIVES FROM
        // MEPCurve. Both halves of that matter, and together they are
        // the same trap that cost the CHW disconnection page every
        // join it made in a real model.
        //
        // `MEPCurve curve = insulation;` COMPILES - verified against
        // the 2022 reference assemblies - and so does the same line
        // for DuctLining, which pipework has no equivalent of. So the
        // duct side has TWO sleeves to walk into rather than one, and
        // the usual "as MEPCurve" cast is no more a guard here than it
        // was there.
        //
        // Taking the first owner out of AllRefs hands back the sleeve
        // rather than the duct inside it, and the rest follows on its
        // own: Describe() finds a category that is neither duct nor
        // fitting and calls it Sort.Other, EQUIPSTOP turns that into
        // Unknown, and EQUIPCHANGE refuses the WHOLE type change when
        // Unknown comes back on the first hop with nothing resized. An
        // insulated duct on a fan coil is the ordinary case, so that is
        // a button that quietly does nothing at all.
        //
        // Asked by CATEGORY, which no class hierarchy can confuse. The
        // scan CARRIES ON past a sleeve rather than giving up, because
        // the duct it wraps is in the same list - usually right behind
        // it.
        private static Element Across(Document doc, Connector from)
        {
            try
            {
                foreach (Connector other in from.AllRefs)
                {
                    if (other.ConnectorType != ConnectorType.End &&
                        other.ConnectorType != ConnectorType.Curve)
                        continue;

                    if (other.Owner == null)
                        continue;

                    long id = other.Owner.Id.ToValue();

                    if (id == from.Owner.Id.ToValue())
                        continue;

                    Element found = doc.GetElement(other.Owner.Id);

                    if (!Ductwork(found))
                        continue;

                    return found;
                }
            }
            catch
            {
            }

            return null;
        }

        // What may be part of a duct branch.
        //
        // An allowlist rather than a list of sleeves to avoid: a
        // category nobody has thought of is then skipped too, which is
        // the right way round to be wrong on a walk that drives a
        // resize.
        //
        // THE LIST ITSELF LIVES IN QADUCTS, beside the one the duct
        // scan collects by, because two copies would be two things to
        // keep in step - and this walk and that scan have to agree
        // about what a duct network is made of. This file already
        // leans on QADUCTS for Manager and Lookup, so it is no new
        // dependency.
        //
        // Equipment and air terminals are in that list ON PURPOSE, and
        // it matters here specifically: the walk has to REACH them for
        // EQUIPSTOP to say "stopped at an air terminal" or "stopped at
        // other equipment". Leaving them out would walk straight past
        // a terminal and report the branch as having simply run out.
        public static bool Ductwork(Element element)
        {
            return QADUCTS.Carries(element);
        }

        // The connector to carry on from: the one this element has that
        // is NOT the one we arrived through
        private static Connector Onward(Element element, Connector arrived)
        {
            ConnectorManager manager = QADUCTS.Manager(element);

            if (manager == null)
                return null;

            long cameFrom = arrived.Owner == null
                ? 0
                : arrived.Owner.Id.ToValue();

            try
            {
                foreach (Connector connector in manager.Connectors)
                {
                    if (connector.Domain != Domain.DomainHvac)
                        continue;

                    if (connector.ConnectorType != ConnectorType.End)
                        continue;

                    // The one we came in by is the one joined to the
                    // element behind us
                    if (Joins(connector, cameFrom))
                        continue;

                    return connector;
                }
            }
            catch
            {
            }

            return null;
        }

        // Needs no category filter, unlike Across above: this asks
        // whether one KNOWN id is in the list, so a sleeve sitting in
        // it simply fails the comparison and can neither be returned
        // nor mistaken for the element being looked for. Same reason
        // QACHWJOINTFIX.Joined was left alone when the pipe walks were
        // filtered.
        private static bool Joins(Connector connector, long owner)
        {
            try
            {
                foreach (Connector other in connector.AllRefs)
                {
                    if (other.Owner != null &&
                        other.Owner.Id.ToValue() == owner)
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        // ==================================================
        // Describing an element for EQUIPSTOP
        // ==================================================

        public static EQUIPNODE Describe(Document doc, Element element)
        {
            var node = new EQUIPNODE();

            if (element == null || element.Category == null)
            {
                node.What = EQUIPNODE.Sort.Other;

                return node;
            }

            long category = element.Category.Id.ToValue();

            if (category == (long)BuiltInCategory.OST_DuctCurves ||
                category == (long)BuiltInCategory.OST_FlexDuctCurves)
            {
                node.What = EQUIPNODE.Sort.Duct;

                return node;
            }

            if (category == (long)BuiltInCategory.OST_DuctTerminal)
            {
                node.What = EQUIPNODE.Sort.Terminal;

                return node;
            }

            if (category == (long)BuiltInCategory.OST_MechanicalEquipment)
            {
                node.What = EQUIPNODE.Sort.Equipment;

                return node;
            }

            if (category == (long)BuiltInCategory.OST_DuctFitting)
                node.What = EQUIPNODE.Sort.Fitting;
            else if (category == (long)BuiltInCategory.OST_DuctAccessory)
                node.What = EQUIPNODE.Sort.Accessory;
            else
            {
                node.What = EQUIPNODE.Sort.Other;

                return node;
            }

            node.PartType = PartTypeOf(element);

            var ends = new List<Connector>();

            ConnectorManager manager = QADUCTS.Manager(element);

            if (manager != null)
            {
                try
                {
                    foreach (Connector connector in manager.Connectors)
                    {
                        if (connector.Domain == Domain.DomainHvac &&
                            connector.ConnectorType == ConnectorType.End)
                            ends.Add(connector);
                    }
                }
                catch
                {
                }
            }

            node.Ends = ends.Count;

            if (ends.Count == 2)
            {
                EQUIPSIZE one = SizeOf(ends[0]);
                EQUIPSIZE two = SizeOf(ends[1]);

                node.EndsDiffer =
                    one.Known && two.Known && !one.Same(two);
            }

            return node;
        }

        // MechanicalFitting.PartType works for duct fittings as well as
        // pipe ones - the same call QACHWFITTINGS leans on. A family
        // that will not say leaves this empty and the geometry decides.
        public static string PartTypeOf(Element element)
        {
            try
            {
                var instance = element as FamilyInstance;

                if (instance == null)
                    return string.Empty;

                var fitting = instance.MEPModel as MechanicalFitting;

                if (fitting == null)
                    return string.Empty;

                return fitting.PartType.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static EQUIPSIZE SizeOf(Connector connector)
        {
            var size = new EQUIPSIZE();

            if (connector == null)
                return size;

            try
            {
                if (connector.Shape == ConnectorProfileType.Round)
                {
                    size.Round = true;
                    size.Diameter = connector.Radius * 2.0;
                }
                else
                {
                    size.Round = false;
                    size.Width = connector.Width;
                    size.Height = connector.Height;
                }
            }
            catch
            {
                // An oval or a family that will not answer: left
                // unknown, and the caller refuses rather than guesses
            }

            return size;
        }

        public static EQUIPSIZE SizeOf(Element element)
        {
            var size = new EQUIPSIZE();

            var curve = element as MEPCurve;

            if (curve == null)
                return size;

            try
            {
                Parameter round = element.get_Parameter(
                    BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);

                if (round != null && round.HasValue &&
                    round.AsDouble() > 1e-9)
                {
                    size.Round = true;
                    size.Diameter = round.AsDouble();

                    return size;
                }

                Parameter width = element.get_Parameter(
                    BuiltInParameter.RBS_CURVE_WIDTH_PARAM);

                Parameter height = element.get_Parameter(
                    BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

                if (width != null && height != null)
                {
                    size.Round = false;
                    size.Width = width.AsDouble();
                    size.Height = height.AsDouble();
                }
            }
            catch
            {
            }

            return size;
        }

        public static string Describe(Element element)
        {
            if (element == null)
                return string.Empty;

            try
            {
                string kind = element.Category == null
                    ? string.Empty
                    : element.Category.Name;

                string name = element.Name ?? string.Empty;

                return string.IsNullOrEmpty(name)
                    ? kind
                    : kind + " - " + name;
            }
            catch
            {
                return string.Empty;
            }
        }

        // The HVAC end connectors of a piece of equipment or a terminal,
        // in a stable order so a report can name "connector 1"
        public static List<Connector> EndsOf(Element element)
        {
            var ends = new List<Connector>();

            ConnectorManager manager = QADUCTS.Manager(element);

            if (manager == null)
                return ends;

            try
            {
                foreach (Connector connector in manager.Connectors)
                {
                    if (connector.Domain == Domain.DomainHvac &&
                        connector.ConnectorType == ConnectorType.End)
                        ends.Add(connector);
                }
            }
            catch
            {
            }

            return ends;
        }
    }
}
