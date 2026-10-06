using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Everything the resize needs in order NOT to break the model.
    //
    // Three separate jobs, all read-only:
    //
    //   Runs      - which pipes must change together. Resizing one pipe
    //               out of a continuous run is what creates a reducer in
    //               the middle of a straight length, for no reason, and
    //               a joint that may not survive it.
    //
    //   Sizes     - which sizes a pipe type can actually be made in.
    //               Asking for one that is not in its routing
    //               preferences is the commonest way a resize fails:
    //               Revit has no segment for it and no fitting to make
    //               the joint with.
    //
    //   Joints    - what was connected to what, written down before
    //               anything moves, so afterwards it can be checked and
    //               put back.
    public class QACHWRUNS
    {
        // ==================================================
        // Runs
        // ==================================================

        public class Run
        {
            public List<long> PipeIds = new List<long>();

            // The most any pipe on the run carries. A run is meant to be
            // one flow throughout, but a model is not always tidy, and
            // sizing to the largest is the safe reading.
            public double Litres;

            // What they are now. Set when they all agree.
            public double DnMm;
            public bool Mixed;

            // Things on the ends that cannot follow a size change -
            // equipment, valves, tees. Named so the report can say where
            // a transition is going to appear.
            public List<string> Boundaries = new List<string>();
        }

        // Groups the given pipes into runs. A run continues through a
        // direct pipe-to-pipe joint, and through a fitting that has
        // exactly TWO piping ends of the SAME size - which is what an
        // elbow or a union is, without having to interrogate the family.
        //
        // It stops at a tee or cross (three ends or more: the flow
        // changes there), at a transition (two ends of different sizes:
        // it exists precisely because the sizes differ), and at anything
        // that is not a fitting at all.
        public static List<Run> Group(
            Document doc, IEnumerable<long> pipeIds, QASETTINGS settings)
        {
            var wanted = new HashSet<long>(pipeIds);

            var done = new HashSet<long>();

            var runs = new List<Run>();

            foreach (long id in wanted)
            {
                if (done.Contains(id))
                    continue;

                Run run = Walk(doc, id, done, settings);

                if (run != null && run.PipeIds.Count > 0)
                    runs.Add(run);
            }

            return runs;
        }

        private static Run Walk(
            Document doc, long from, HashSet<long> done, QASETTINGS settings)
        {
            var run = new Run();

            var queue = new Queue<long>();

            queue.Enqueue(from);
            done.Add(from);

            var sizes = new List<double>();

            while (queue.Count > 0)
            {
                long id = queue.Dequeue();

                var pipe = doc.GetElement(REVITCOMPAT.NewId(id)) as MEPCurve;

                if (pipe == null)
                    continue;

                run.PipeIds.Add(id);

                double litres;
                double dn;

                if (QACHW.Flow(pipe, out litres) && litres > run.Litres)
                    run.Litres = litres;

                if (QACHW.Bore(pipe, out dn))
                    sizes.Add(dn);

                foreach (Connector connector in Ends(pipe))
                {
                    foreach (Element next in Across(connector, id))
                    {
                        long nextId = next.Id.ToValue();

                        if (next is MEPCurve)
                        {
                            if (done.Add(nextId))
                                queue.Enqueue(nextId);

                            continue;
                        }

                        // A fitting: cross it only if it is a plain
                        // elbow or union
                        long onward;

                        if (Crossable(next, id, out onward))
                        {
                            if (onward != 0 && done.Add(onward))
                                queue.Enqueue(onward);

                            // An in-line accessory has two equal ends
                            // just like an elbow, so the geometry cannot
                            // tell them apart - but the category can.
                            // The run still crosses it, since the pipe
                            // either side of a valve is one length and
                            // wants one size; it is noted because the
                            // valve's own size may not follow, and a
                            // transition either side of it is then where
                            // the change will show up.
                            if (IsAccessory(next))
                                Note(run, QADUCTS.Name(next) + "  (in line)");

                            continue;
                        }

                        Note(run, QADUCTS.Name(next));
                    }
                }
            }

            // Do they all agree on a size now?
            double first = sizes.Count > 0 ? sizes[0] : 0;

            run.DnMm = first;

            foreach (double size in sizes)
            {
                if (Math.Abs(size - first) > settings.ChwSizeToleranceMm)
                    run.Mixed = true;
            }

            return run;
        }

        // Two piping ends, the same size: an elbow or a union. Gives
        // back the pipe on the far side, when there is one.
        private static bool Crossable(
            Element fitting, long cameFrom, out long onward)
        {
            onward = 0;

            // Only pipework in its own right. A pump or a chiller can
            // have two connectors of equal size and would otherwise be
            // crossed as though it were an elbow - and a pump's size
            // very much does not follow the pipe.
            if (!IsFitting(fitting) && !IsAccessory(fitting))
                return false;

            ConnectorManager manager = QADUCTS.Manager(fitting);

            if (manager == null)
                return false;

            var ends = new List<Connector>();

            try
            {
                foreach (Connector connector in manager.Connectors)
                {
                    if (connector.Domain == Domain.DomainPiping
                        && connector.ConnectorType == ConnectorType.End)
                        ends.Add(connector);
                }
            }
            catch
            {
                return false;
            }

            // Three or more is a tee or a cross: the flow changes there
            if (ends.Count != 2)
                return false;

            double one;
            double two;

            try
            {
                one = ends[0].Radius;
                two = ends[1].Radius;
            }
            catch
            {
                return false;
            }

            // Different sizes means it is a transition, and a transition
            // is there on purpose
            if (Math.Abs(one - two) > 1e-9)
                return false;

            foreach (Connector connector in ends)
            {
                foreach (Element next in Across(connector, fitting.Id.ToValue()))
                {
                    if (next.Id.ToValue() == cameFrom)
                        continue;

                    if (next is MEPCurve)
                        onward = next.Id.ToValue();
                }
            }

            return true;
        }

        private static void Note(Run run, string what)
        {
            if (!run.Boundaries.Contains(what))
                run.Boundaries.Add(what);
        }

        public static bool IsAccessory(Element element)
        {
            return Is(element, BuiltInCategory.OST_PipeAccessory);
        }

        public static bool IsFitting(Element element)
        {
            return Is(element, BuiltInCategory.OST_PipeFitting);
        }

        private static bool Is(Element element, BuiltInCategory what)
        {
            if (element == null || element.Category == null)
                return false;

            return element.Category.Id.ToValue() == (long)what;
        }

        public static List<Connector> Ends(Element element)
        {
            var ends = new List<Connector>();

            ConnectorManager manager = QADUCTS.Manager(element);

            if (manager == null)
                return ends;

            try
            {
                foreach (Connector connector in manager.Connectors)
                {
                    if (connector.Domain == Domain.DomainPiping
                        && connector.ConnectorType == ConnectorType.End)
                        ends.Add(connector);
                }
            }
            catch
            {
                // nothing usable on this one
            }

            return ends;
        }

        private static List<Element> Across(Connector connector, long from)
        {
            var found = new List<Element>();

            try
            {
                if (!connector.IsConnected)
                    return found;
            }
            catch
            {
                return found;
            }

            ConnectorSet refs;

            try
            {
                refs = connector.AllRefs;
            }
            catch
            {
                return found;
            }

            foreach (Connector other in refs)
            {
                try
                {
                    if (other.Owner == null
                        || other.Owner.Id.ToValue() == from
                        || other.Domain != Domain.DomainPiping)
                        continue;

                    found.Add(other.Owner);
                }
                catch
                {
                    // that reference is not usable
                }
            }

            return found;
        }

        // ==================================================
        // Sizes this pipe type can actually be made in
        // ==================================================

        // Straight from the type's routing preferences. A size that is
        // not here has no segment behind it, so Revit cannot draw the
        // pipe and has no fitting to join it with - asking for it is the
        // commonest way a resize fails.
        public static List<double> SizesFor(Document doc, Element pipe)
        {
            var found = new List<double>();

            var type = doc.GetElement(pipe.GetTypeId()) as PipeType;

            if (type == null)
                return found;

            RoutingPreferenceManager rules;

            try
            {
                rules = type.RoutingPreferenceManager;
            }
            catch
            {
                return found;
            }

            if (rules == null)
                return found;

            try
            {
                int count = rules.GetNumberOfRules(
                    RoutingPreferenceRuleGroupType.Segments);

                for (int i = 0; i < count; i++)
                {
                    RoutingPreferenceRule rule = rules.GetRule(
                        RoutingPreferenceRuleGroupType.Segments, i);

                    var segment = doc.GetElement(rule.MEPPartId) as Segment;

                    if (segment == null)
                        continue;

                    foreach (MEPSize size in segment.GetSizes())
                    {
                        double mm = UnitUtils.ConvertFromInternalUnits(
                            size.NominalDiameter, UnitTypeId.Millimeters);

                        found.Add(mm);
                    }
                }
            }
            catch
            {
                // a type whose preferences cannot be read tells us
                // nothing; the caller treats an empty list as "unknown"
            }

            return found;
        }

        public static bool Offers(
            List<double> sizes, double dnMm, double toleranceMm)
        {
            // An empty list means the type would not say. Refusing on
            // that would block every resize on a model this cannot read,
            // so silence is taken as consent and Revit gets the final
            // word.
            if (sizes.Count == 0)
                return true;

            foreach (double size in sizes)
            {
                if (Math.Abs(size - dnMm) <= toleranceMm)
                    return true;
            }

            return false;
        }

        // ==================================================
        // Joints, written down before anything moves
        // ==================================================

        public class Joint
        {
            public long OneId;
            public int OneConnector;

            public long TwoId;
            public int TwoConnector;

            public string Where = string.Empty;

            public string Key
            {
                get
                {
                    return OneId < TwoId
                        ? OneId + ":" + OneConnector + "|" + TwoId + ":" + TwoConnector
                        : TwoId + ":" + TwoConnector + "|" + OneId + ":" + OneConnector;
                }
            }
        }

        // Every piping joint these pipes currently have. Recorded before
        // the resize so what came apart can be found afterwards.
        public static List<Joint> JointsOf(
            Document doc, IEnumerable<long> pipeIds)
        {
            var joints = new List<Joint>();

            var seen = new HashSet<string>();

            foreach (long id in pipeIds)
            {
                Element pipe = doc.GetElement(REVITCOMPAT.NewId(id));

                if (pipe == null)
                    continue;

                foreach (Connector connector in Ends(pipe))
                {
                    int mine;

                    try
                    {
                        mine = connector.Id;
                    }
                    catch
                    {
                        continue;
                    }

                    ConnectorSet refs;

                    try
                    {
                        if (!connector.IsConnected)
                            continue;

                        refs = connector.AllRefs;
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (Connector other in refs)
                    {
                        try
                        {
                            if (other.Owner == null
                                || other.Owner.Id.ToValue() == id
                                || other.Domain != Domain.DomainPiping
                                || other.ConnectorType != ConnectorType.End)
                                continue;

                            var joint = new Joint
                            {
                                OneId = id,
                                OneConnector = mine,
                                TwoId = other.Owner.Id.ToValue(),
                                TwoConnector = other.Id,
                                Where = QADUCTS.Name(pipe) + " to " +
                                        QADUCTS.Name(other.Owner)
                            };

                            if (seen.Add(joint.Key))
                                joints.Add(joint);
                        }
                        catch
                        {
                            // that reference is not usable
                        }
                    }
                }
            }

            return joints;
        }

        // Is this joint still made?
        public static bool StillJoined(Document doc, Joint joint)
        {
            Connector one = QADUCTS.Lookup(
                doc, joint.OneId, joint.OneConnector);

            Connector two = QADUCTS.Lookup(
                doc, joint.TwoId, joint.TwoConnector);

            if (one == null || two == null)
                return false;

            try
            {
                return one.IsConnectedTo(two);
            }
            catch
            {
                return false;
            }
        }
    }
}
