using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ATKINSMEC
{
    // THE DUCT NETWORK OF ONE OR MORE MODELS, AND THE WALK THAT FINDS A
    // UNIT'S INDEX RUN. Revit-free: ESPCOLLECT reads the models into
    // these classes once, and everything after that - joining the models,
    // the flows, every loss, the index run - is arithmetic that
    // scratchpad/esptest runs with no Revit at all.
    //
    // WHY ITS OWN CALCULATION AND NOT REVIT'S. Revit already works out
    // each system's sections and critical path (MEPSystem.
    // GetCriticalPathSectionNumbers, PressureLossOfCriticalPath) - but
    // only INSIDE one model. A riser in a linked model whose terminals
    // are in this one carries no flow there, so Revit gives it no loss,
    // and an AHU in another model is not even on the system. The user
    // asked for exactly that case - "duct risers and AHUs are located in
    // different model and the duct networks in other models" - so the
    // network is built here, across the models, and calculated here.
    //
    // HOW THE MODELS MEET (Bridge). A duct in one model can never be
    // connected to a duct in another, so two OPEN ends from different
    // models that sit within a tolerance of each other and face each
    // other are taken as one joint. Each end is used once, the closest
    // pairs first - the greedy matching ALIGNTERMINALS uses. Every bridge
    // is reported, and so are the near misses just outside the
    // tolerance, so a run that stops short can be explained.
    public static class ESPNET
    {
        public enum Kind { Duct, Flex, Fitting, Accessory, Terminal, Equipment }

        public enum Shape { Unknown, Round, Rectangular, Oval }

        // ==================================================
        // What the models are read into
        // ==================================================

        public class Part
        {
            public long LinkId;              // 0 = this model
            public long ElementId;
            public string Model = string.Empty;
            public Kind What;
            public string Family = string.Empty;   // "Family : Type", or the duct type
            public string Mark = string.Empty;
            public string PartType = string.Empty; // what a fitting calls itself

            // A duct's own section, metres
            public Shape Section;
            public double Diameter, Width, Height;

            public double Roughness;         // m; 0 = use the setting
            public double Length;            // m; a FLEX duct's own length

            // What the family itself states, 0 = nothing
            public double Coefficient;
            public double PressureDrop;      // Pa
            public double Flow;              // m3/s, an air terminal's flow

            public List<Port> Ports = new List<Port>();

            public string Key
            {
                get { return LinkId + ":" + ElementId; }
            }

            public string Label
            {
                get { return Family + (Mark.Length > 0 ? "  [" + Mark + "]" : string.Empty); }
            }
        }

        public class Port
        {
            public Part Owner;
            public int ConnectorId;

            // In THIS model's coordinates, metres. D is a unit vector
            // pointing OUT of the part.
            public double X, Y, Z;
            public double DX, DY, DZ;

            public Shape Section;
            public double Diameter, Width, Height;

            public string System = string.Empty;     // Supply, Return, Exhaust, Other
            public string Direction = string.Empty;  // In, Out, or not known
            public double Flow;                      // m3/s, the connector's own

            // A tap's place on a duct's body, rather than an end
            public bool IsCurve;

            // Metres along a duct from its start - what orders the taps
            public double Along;

            public List<Port> Joined = new List<Port>();

            public Port Bridged;
            public double BridgeGap;

            internal int Index;

            public double Area
            {
                get { return ESPAIR.Area(Section, Diameter, Width, Height); }
            }

            public bool Open
            {
                get { return Joined.Count == 0 && Bridged == null; }
            }
        }

        public class Bridge
        {
            public Port A;
            public Port B;
            public double Gap;               // m
        }

        public static void Join(Port a, Port b)
        {
            if (a == null || b == null || a == b)
                return;

            if (!a.Joined.Contains(b))
                a.Joined.Add(b);

            if (!b.Joined.Contains(a))
                b.Joined.Add(a);
        }

        private static double Distance(Port a, Port b)
        {
            double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;

            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static double Dot(Port a, Port b)
        {
            return a.DX * b.DX + a.DY * b.DY + a.DZ * b.DZ;
        }

        // ==================================================
        // Joining the models
        // ==================================================

        // Two ends face each other when their outward directions are
        // within 60 degrees of opposite
        public const double Facing = -0.5;

        // Clears any earlier bridges (the tolerance may have changed) and
        // makes them again. Only ends in DIFFERENT placements are joined:
        // two open ends in one model that nearly meet are a drawing fault
        // for SHEET QA's duct page, not something to paper over here.
        public static List<Bridge> BridgeModels(
            IList<Part> parts, double tolerance, out List<Bridge> nearMisses)
        {
            nearMisses = new List<Bridge>();

            var open = new List<Port>();

            foreach (Part part in parts)
            {
                foreach (Port port in part.Ports)
                {
                    port.Bridged = null;
                    port.BridgeGap = 0;

                    if (port.Joined.Count == 0 && !port.IsCurve)
                        open.Add(port);
                }
            }

            for (int i = 0; i < open.Count; i++)
                open[i].Index = i;

            var bridges = new List<Bridge>();

            if (tolerance <= 0 || open.Count < 2)
                return bridges;

            // Near misses are looked for out to three tolerances, so the
            // cells are that big and only the 27 round a point are read
            double reach = tolerance * 3;

            var cells = new Dictionary<(int, int, int), List<Port>>();

            Func<double, int> cell = v => (int)Math.Floor(v / reach);

            foreach (Port port in open)
            {
                var key = (cell(port.X), cell(port.Y), cell(port.Z));

                List<Port> list;

                if (!cells.TryGetValue(key, out list))
                {
                    list = new List<Port>();
                    cells.Add(key, list);
                }

                list.Add(port);
            }

            var candidates = new List<Bridge>();
            var misses = new List<Bridge>();

            foreach (Port a in open)
            {
                int cx = cell(a.X), cy = cell(a.Y), cz = cell(a.Z);

                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    List<Port> list;

                    if (!cells.TryGetValue((cx + dx, cy + dy, cz + dz), out list))
                        continue;

                    foreach (Port b in list)
                    {
                        // Each pair once, and never within one placement
                        if (b.Index <= a.Index || b.Owner.LinkId == a.Owner.LinkId)
                            continue;

                        double gap = Distance(a, b);

                        if (gap > reach || Dot(a, b) > Facing)
                            continue;

                        var pair = new Bridge { A = a, B = b, Gap = gap };

                        if (gap <= tolerance)
                            candidates.Add(pair);
                        else
                            misses.Add(pair);
                    }
                }
            }

            // Closest first; ties settled by the elements so two runs of
            // one model cannot disagree
            Comparison<Bridge> order = (p, q) =>
            {
                int byGap = p.Gap.CompareTo(q.Gap);

                if (byGap != 0)
                    return byGap;

                int byA = string.CompareOrdinal(p.A.Owner.Key, q.A.Owner.Key);

                return byA != 0
                    ? byA
                    : string.CompareOrdinal(p.B.Owner.Key, q.B.Owner.Key);
            };

            candidates.Sort(order);

            foreach (Bridge pair in candidates)
            {
                if (pair.A.Bridged != null || pair.B.Bridged != null)
                    continue;

                pair.A.Bridged = pair.B;
                pair.B.Bridged = pair.A;
                pair.A.BridgeGap = pair.Gap;
                pair.B.BridgeGap = pair.Gap;

                bridges.Add(pair);
            }

            misses.Sort(order);

            nearMisses = misses
                .Where(m => m.A.Bridged == null && m.B.Bridged == null)
                .Take(50)
                .ToList();

            return bridges;
        }

        // ==================================================
        // The answer
        // ==================================================

        public class Step
        {
            public Part Part;
            public string What = string.Empty;
            public string Size = string.Empty;
            public double Flow;              // m3/s
            public double Velocity;          // m/s
            public double Length;            // m
            public double PerMetre;          // Pa/m
            public double Coefficient;
            public double Loss;              // Pa
            public double Running;           // Pa, from the unit to here
            public string Source = string.Empty;

            // The figure came from a DEFAULT because nothing said better
            public bool Flagged;
        }

        // One port of a unit and the network on it
        public class Run
        {
            public Port Start;
            public string System = string.Empty;
            public string Direction = string.Empty;

            // Air coming back to the unit: the return column of the table
            public bool Converging;

            public bool Connected;

            public double FlowAtUnit;        // m3/s, the unit's own connector
            public double FlowAtEnds;        // m3/s, what the terminals add up to

            public double Index;             // Pa, the worst path
            public List<Step> Steps = new List<Step>();
            public string EndsAt = string.Empty;

            public int Terminals;
            public int Equipment;
            public int DeadEnds;
            public int Loops;
            public int Bridges;
            public int ZeroFlowTerminals;

            // Items anywhere on this network, and on the index run, whose
            // loss came from a default
            public int Flagged;
            public int FlaggedOnIndex;

            // The families those defaults were for, once each - what the
            // window offers to give values to
            public List<string> FlaggedFamilies = new List<string>();

            public List<string> Notes = new List<string>();
        }

        public class Result
        {
            public Part Unit;
            public List<Run> Runs = new List<Run>();

            // The worst port blowing out of the unit, and the worst
            // drawing into it
            public double OutSide;
            public double InSide;

            public double Esp;
            public double EspWithMargin;
        }

        // Which way the air goes at a unit's port. The connector says, if
        // the family set it; otherwise the system type decides - supply
        // and exhaust leave, return and outside air arrive.
        public static bool IsIn(Port port)
        {
            if (port.Direction == "In")
                return true;

            if (port.Direction == "Out")
                return false;

            return port.System == "Return" || port.System == "Other";
        }

        // ESP = the worst outlet + the worst inlet, plus the margin.
        //
        // That one rule is right for the common units: a fan coil is its
        // supply plus its return; an exhaust fan is its suction plus its
        // discharge; an AHU's return and fresh air arrive in parallel at
        // the mixing box, so the WORSE of the two counts, not both. An
        // AHU with a separate return fan needs two figures, and every
        // port's own loss is in the results for exactly that.
        public static Result Calculate(Part unit, ESPSETTINGS settings)
        {
            var result = new Result { Unit = unit };

            foreach (Port port in unit.Ports.Where(p => !p.IsCurve))
            {
                Run run = port.Open
                    ? new Run
                    {
                        Start = port,
                        System = port.System,
                        Direction = port.Direction,
                        Converging = IsIn(port),
                        FlowAtUnit = port.Flow,
                        Notes = { "this connector is not joined to anything" }
                    }
                    : Walk(unit, port, settings);

                result.Runs.Add(run);
            }

            foreach (Run run in result.Runs.Where(r => r.Connected))
            {
                if (run.Converging)
                    result.InSide = Math.Max(result.InSide, run.Index);
                else
                    result.OutSide = Math.Max(result.OutSide, run.Index);
            }

            result.Esp = result.OutSide + result.InSide;
            result.EspWithMargin = result.Esp * (1 + settings.MarginPercent / 100.0);

            return result;
        }

        // ==================================================
        // The walk
        // ==================================================

        private class Node
        {
            public Port Port;
            public Node Parent;

            // The edge from the parent runs INSIDE a part (a length of
            // duct, or through a fitting) rather than across a joint
            public bool Inside;

            public bool Bridge;

            public List<Node> Children = new List<Node>();

            public double Flow;
            public Step Step;        // what the edge from the parent costs
            public Step Drop;        // a terminal's own drop, at a leaf
            public double Running;

            public bool AtTerminal;
            public bool AtEquipment;
            public bool DeadEnd;
        }

        public static Run Walk(Part unit, Port start, ESPSETTINGS settings)
        {
            var run = new Run
            {
                Start = start,
                System = start.System,
                Direction = start.Direction,
                Converging = IsIn(start),
                FlowAtUnit = start.Flow,
                Connected = true
            };

            var root = new Node { Port = start };

            var visited = new HashSet<Port> { start };
            var order = new List<Node>();
            var stack = new Stack<Node>();

            stack.Push(root);

            // ---- the tree, depth first, without recursion: a long riser
            // is thousands of elements deep
            while (stack.Count > 0)
            {
                Node node = stack.Pop();
                order.Add(node);

                Port port = node.Port;
                Part part = port.Owner;

                var next = new List<Node>();

                Action<Port, bool, bool> visit = (target, inside, bridge) =>
                {
                    if (target == null)
                        return;

                    if (visited.Contains(target))
                    {
                        // The way back is visited too, and is not a loop
                        if (node.Parent == null || target != node.Parent.Port)
                            run.Loops++;

                        return;
                    }

                    visited.Add(target);

                    var child = new Node
                    {
                        Port = target,
                        Parent = node,
                        Inside = inside,
                        Bridge = bridge
                    };

                    node.Children.Add(child);
                    next.Add(child);
                };

                if (node.Parent == null)
                {
                    // The unit's own port: out across its joint
                    foreach (Port joined in port.Joined)
                        visit(joined, false, false);

                    visit(port.Bridged, false, true);
                }
                else if (!node.Inside)
                {
                    // Arrived at a part from outside it
                    if (part == unit)
                    {
                        run.Notes.Add("the network comes back to the unit itself");
                        node.DeadEnd = true;
                    }
                    else if (part.What == Kind.Terminal)
                    {
                        node.AtTerminal = true;
                    }
                    else if (part.What == Kind.Equipment)
                    {
                        // Another unit takes over from here - an FCU fed
                        // with fresh air by an AHU, say
                        node.AtEquipment = true;
                    }
                    else
                    {
                        foreach (Port inner in Inside(part, port))
                            visit(inner, true, false);
                    }
                }
                else
                {
                    // Arrived at a port from inside its own part: out
                    // across its joint, and a duct carries on past a tap
                    foreach (Port joined in port.Joined)
                        visit(joined, false, false);

                    visit(port.Bridged, false, true);

                    if (part.What == Kind.Duct)
                    {
                        foreach (Port along in Chain(part, port))
                            visit(along, true, false);
                    }
                }

                if (node.Parent != null && node.Children.Count == 0 &&
                    !node.AtTerminal && !node.AtEquipment)
                {
                    node.DeadEnd = true;
                }

                // Pushed in reverse so the walk goes in the parts' own
                // order - the answer does not depend on it, the log reads
                // better for it
                for (int i = next.Count - 1; i >= 0; i--)
                    stack.Push(next[i]);
            }

            // ---- flows, from the leaves back: a pre-order list read
            // backwards has every child before its parent
            for (int i = order.Count - 1; i >= 0; i--)
            {
                Node node = order[i];

                if (node.AtTerminal)
                {
                    Part terminal = node.Port.Owner;

                    node.Flow = terminal.Flow > 0 ? terminal.Flow : node.Port.Flow;

                    run.Terminals++;

                    if (node.Flow <= 0)
                        run.ZeroFlowTerminals++;
                }
                else if (node.AtEquipment)
                {
                    node.Flow = node.Port.Flow;
                    run.Equipment++;
                }
                else
                {
                    node.Flow = node.Children.Sum(c => c.Flow);

                    if (node.DeadEnd)
                        run.DeadEnds++;
                }
            }

            run.FlowAtEnds = root.Flow;

            // ---- losses, from the unit out
            foreach (Node node in order)
            {
                if (node.Parent == null)
                    continue;

                if (node.Bridge)
                    run.Bridges++;

                node.Step = Cost(node, run.Converging, settings);
                node.Drop = LeafDrop(node, settings);

                node.Running = node.Parent.Running +
                               (node.Step == null ? 0 : node.Step.Loss) +
                               (node.Drop == null ? 0 : node.Drop.Loss);

                foreach (Step step in new[] { node.Step, node.Drop })
                {
                    if (step == null || !step.Flagged)
                        continue;

                    run.Flagged++;

                    if (!run.FlaggedFamilies.Contains(step.Part.Family))
                        run.FlaggedFamilies.Add(step.Part.Family);
                }
            }

            // ---- the index run: the worst path to anything that takes
            // air. Dead ends only count when there is nothing else.
            List<Node> ends = order.Where(n => n.AtTerminal || n.AtEquipment).ToList();

            if (ends.Count == 0)
                ends = order.Where(n => n.DeadEnd).ToList();

            Node worst = ends.OrderByDescending(n => n.Running).FirstOrDefault();

            if (worst == null)
            {
                run.Notes.Add("nothing found beyond the unit's connector");
                return run;
            }

            run.Index = worst.Running;
            run.EndsAt = worst.Port.Owner.Label +
                         (worst.AtTerminal || worst.AtEquipment ? string.Empty : "  (an open end)");

            var path = new List<Node>();

            for (Node at = worst; at != null && at.Parent != null; at = at.Parent)
                path.Add(at);

            path.Reverse();

            foreach (Node at in path)
            {
                foreach (Step step in new[] { at.Step, at.Drop })
                {
                    if (step == null)
                        continue;

                    run.Steps.Add(step);

                    if (step.Flagged)
                        run.FlaggedOnIndex++;
                }
            }

            // The running total on every step, in order from the unit
            double running = 0;

            foreach (Step step in run.Steps)
            {
                running += step.Loss;
                step.Running = running;
            }

            if (run.ZeroFlowTerminals > 0)
            {
                run.Notes.Add(run.ZeroFlowTerminals +
                              " terminal(s) on this network carry no flow, so their branches cost nothing");
            }

            if (run.DeadEnds > 0)
            {
                run.Notes.Add(run.DeadEnds +
                              " branch(es) end without a terminal - an open end, a cap, or a joint to another model that was not made");
            }

            if (run.Loops > 0)
                run.Notes.Add("the network loops back on itself " + run.Loops + " time(s); each loop was cut where it closed");

            return run;
        }

        // The ports reached from one by passing THROUGH its part
        private static IEnumerable<Port> Inside(Part part, Port entry)
        {
            if (part.What == Kind.Duct)
                return Chain(part, entry);

            return part.Ports.Where(p => p != entry);
        }

        // A duct is a chain: its two ends, and any taps on its body in
        // between, in order along it. From any one of them the next ones
        // are its neighbours in that order.
        private static IEnumerable<Port> Chain(Part part, Port at)
        {
            List<Port> ordered = part.Ports.OrderBy(p => p.Along).ToList();

            int i = ordered.IndexOf(at);

            if (i < 0)
                yield break;

            if (i > 0)
                yield return ordered[i - 1];

            if (i < ordered.Count - 1)
                yield return ordered[i + 1];
        }

        // ==================================================
        // What each piece costs
        // ==================================================

        private static double Velocity(double flow, Port port)
        {
            double area = port.Area;

            return area > 0 ? flow / area : 0;
        }

        public static string SizeOf(Shape shape, double diameter, double width, double height)
        {
            Func<double, string> mm = v => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

            switch (shape)
            {
                case Shape.Round:
                    return "Ø" + mm(diameter);
                case Shape.Rectangular:
                    return mm(width) + "x" + mm(height);
                case Shape.Oval:
                    return mm(width) + "x" + mm(height) + " oval";
            }

            return string.Empty;
        }

        private static Step Cost(Node node, bool converging, ESPSETTINGS settings)
        {
            Port from = node.Parent.Port;
            Port to = node.Port;
            Part part = to.Owner;

            if (!node.Inside)
            {
                if (!node.Bridge)
                    return null;

                return new Step
                {
                    Part = part,
                    What = "Joint between models",
                    Flow = node.Flow,
                    Source = "open ends " + Math.Round(to.BridgeGap * 1000) +
                             " mm apart, taken as one joint - into " + part.Model
                };
            }

            switch (part.What)
            {
                case Kind.Duct:
                case Kind.Flex:
                    return Friction(node, from, to, part, settings);

                case Kind.Fitting:
                    return Fitting(node, from, to, part, converging, settings);

                case Kind.Accessory:
                    return Accessory(node, from, to, part, converging, settings);
            }

            return null;
        }

        private static Step Friction(Node node, Port from, Port to, Part part, ESPSETTINGS settings)
        {
            bool flex = part.What == Kind.Flex;

            double length = flex && part.Length > 0
                ? part.Length
                : flex
                    ? Distance(from, to)
                    : Math.Abs(to.Along - from.Along);

            Shape shape = part.Section;
            double d = part.Diameter, w = part.Width, h = part.Height;

            if (shape == Shape.Unknown)
            {
                shape = to.Section;
                d = to.Diameter;
                w = to.Width;
                h = to.Height;
            }

            double roughness = part.Roughness > 0
                ? part.Roughness
                : (flex ? settings.FlexRoughnessMm : settings.RoughnessMm) / 1000.0;

            double velocity;

            double perMetre = ESPAIR.FrictionPerMetre(
                node.Flow, shape, d, w, h, roughness, settings.Density, out velocity);

            return new Step
            {
                Part = part,
                What = flex ? "Flexible duct" : "Duct",
                Size = SizeOf(shape, d, w, h),
                Flow = node.Flow,
                Velocity = velocity,
                Length = length,
                PerMetre = perMetre,
                Loss = perMetre * length,
                Source = "calculated, roughness " +
                         (roughness * 1000).ToString("0.###", CultureInfo.InvariantCulture) + " mm"
            };
        }

        private static Step Fitting(
            Node node, Port from, Port to, Part part, bool converging, ESPSETTINGS settings)
        {
            string kind = KindOf(part, from, to);

            double vIn = Velocity(node.Parent.Flow, from);
            double vOut = Velocity(node.Flow, to);

            // A branch, a run through a tee and a takeoff are all taken on
            // the leg the path leaves by; anything else on its faster side
            bool onLeaving = kind.EndsWith("branch") || kind.EndsWith("through") || kind == "Tap";

            double v = onLeaving && vOut > 0 ? vOut : Math.Max(vIn, vOut);

            var step = new Step
            {
                Part = part,
                What = kind,
                Size = SizeOf(to.Section, to.Diameter, to.Width, to.Height),
                Flow = node.Flow,
                Velocity = v
            };

            double pa;

            if (settings.FamilyValue(part.Family, out pa))
            {
                step.Loss = pa;
                step.Source = "your value for this family";
                return step;
            }

            if (part.Coefficient > 0)
            {
                step.Coefficient = part.Coefficient;
                step.Source = "the family's own coefficient";
            }
            else
            {
                double c;

                bool known = settings.Coefficient(kind, converging, out c);

                step.Coefficient = c;
                step.Flagged = !known || kind == ESPSETTINGS.UnknownFitting;
                step.Source = known && !step.Flagged
                    ? "table: " + kind + (converging ? " (return)" : " (supply)")
                    : "DEFAULT - the family does not say what kind of fitting it is";
            }

            step.Loss = step.Coefficient * ESPAIR.VelocityPressure(v, settings.Density);

            return step;
        }

        private static Step Accessory(
            Node node, Port from, Port to, Part part, bool converging, ESPSETTINGS settings)
        {
            double v = Math.Max(Velocity(node.Parent.Flow, from), Velocity(node.Flow, to));

            var step = new Step
            {
                Part = part,
                What = part.PartType == "Damper" ? "Damper" : "Accessory",
                Size = SizeOf(to.Section, to.Diameter, to.Width, to.Height),
                Flow = node.Flow,
                Velocity = v
            };

            double pa;

            if (settings.FamilyValue(part.Family, out pa))
            {
                step.Loss = pa;
                step.Source = "your value for this family";
                return step;
            }

            if (part.PressureDrop > 0)
            {
                step.Loss = part.PressureDrop;
                step.Source = "the family's own pressure drop";
                return step;
            }

            double c;

            if (part.Coefficient > 0)
            {
                c = part.Coefficient;
                step.Source = "the family's own coefficient";
            }
            else if (part.PartType == "Damper")
            {
                settings.Coefficient("Damper", converging, out c);
                step.Source = "table: Damper" + (converging ? " (return)" : " (supply)");
            }
            else
            {
                settings.Coefficient(ESPSETTINGS.UnknownAccessory, converging, out c);
                step.Flagged = true;
                step.Source = "DEFAULT - the family states no pressure drop";
            }

            step.Coefficient = c;
            step.Loss = c * ESPAIR.VelocityPressure(v, settings.Density);

            return step;
        }

        // What a branch's END costs: a terminal's own pressure drop, or
        // nothing for another unit, which takes over from its connector
        private static Step LeafDrop(Node node, ESPSETTINGS settings)
        {
            if (!node.AtTerminal && !node.AtEquipment)
                return null;

            Part part = node.Port.Owner;

            var step = new Step
            {
                Part = part,
                What = node.AtTerminal ? "Air terminal" : "Equipment",
                Size = SizeOf(node.Port.Section, node.Port.Diameter, node.Port.Width, node.Port.Height),
                Flow = node.Flow,
                Velocity = Velocity(node.Flow, node.Port)
            };

            double pa;

            if (settings.FamilyValue(part.Family, out pa))
            {
                step.Loss = pa;
                step.Source = "your value for this family";
            }
            else if (part.PressureDrop > 0)
            {
                step.Loss = part.PressureDrop;
                step.Source = "the family's own pressure drop";
            }
            else if (node.AtTerminal)
            {
                step.Loss = settings.TerminalDropPa;
                step.Flagged = true;
                step.Source = "DEFAULT - the family states no pressure drop";
            }
            else
            {
                step.Loss = 0;
                step.Source = "the unit here takes over from its connector";
            }

            return step;
        }

        // ==================================================
        // What kind of fitting the path passes through
        // ==================================================
        //
        // What the family CALLS itself first (MechanicalFitting.PartType,
        // read by ESPCOLLECT as a name), then its geometry - the same
        // order EQUIPSTOP uses. A tee's two runs are told apart by the
        // path: in one side and straight out of the other is "through",
        // anything else is "branch".

        public static string KindOf(Part part, Port from, Port to)
        {
            bool straight = Dot(from, to) < -0.9;

            string through = straight ? " - through" : " - branch";

            switch (part.PartType)
            {
                case "Elbow":
                    return "Elbow";
                case "Transition":
                    return "Transition";
                case "Tee":
                case "LateralTee":
                    return "Tee" + through;
                case "Wye":
                case "Pants":
                    return "Wye" + through;
                case "Cross":
                case "LateralCross":
                    return "Cross" + through;
                case "TapPerpendicular":
                case "TapAdjustable":
                case "SpudPerpendicular":
                case "SpudAdjustable":
                    return "Tap";
                case "Union":
                    return "Union";
                case "Offset":
                    return "Offset";
                case "Cap":
                case "EndCap":
                    return "Cap";
                case "Damper":
                    return "Damper";
            }

            // Nothing said: the geometry
            int ends = part.Ports.Count;

            if (ends == 2)
            {
                bool sameSize = Math.Abs(from.Area - to.Area) <= 0.02 * Math.Max(from.Area, to.Area);

                if (!sameSize)
                    return "Transition";

                return straight ? "Union" : "Elbow";
            }

            if (ends == 3)
                return "Tee" + through;

            if (ends == 4)
                return "Cross" + through;

            return ESPSETTINGS.UnknownFitting;
        }
    }
}
