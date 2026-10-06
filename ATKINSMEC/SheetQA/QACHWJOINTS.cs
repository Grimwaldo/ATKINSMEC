using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // One walk of the chilled water pipework, looking for ends that are
    // not joined to anything.
    //
    // The same shape as QADUCTS: one scan cached on QACONTEXT.Shared so
    // the checks on this page do not each collect every pipe in the
    // job, and the loose ends divided between the checks afterwards so
    // nothing is reported twice.
    public class QACHWJOINTS
    {
        private const string ScanKey = "chwjoints.scan";

        // How far away something still counts as "meant to join this".
        // A metre is generous for pipework and still small enough that
        // it cannot reach across a plant room.
        public static double Reach(QASETTINGS settings)
        {
            return QAGRIDS.ToFeet(
                settings == null || settings.ChwJointReachMm <= 0
                    ? 300.0
                    : settings.ChwJointReachMm);
        }

        // One open pipe end
        public class LooseEnd
        {
            public long OwnerId;

            // A Connector cannot be carried out of the run that made
            // it, so the pair (owner, connector id) is what the fix
            // looks it up by - QADUCTFIX's rule
            public int ConnectorId;

            public XYZ Point;
            public XYZ Facing;

            // Revit's PipeSystemType, as an int
            public int Side;

            public string SystemName = string.Empty;

            public string What = string.Empty;

            public bool OnPipe;

            // Another loose end facing this one
            public LooseEnd Meets;

            // A fan coil connector of the SAME system within reach
            public OnUnit Unit;

            // The nearest unit of the right system that was TOO FAR
            // to claim. NOT a match - a reason. "Nothing within reach"
            // is useless on its own; "the nearest FCU is 640 mm away
            // and the reach is 300" can be acted on without guessing.
            public string NearestUnit = string.Empty;

            public double NearestGap;

            // The nearest unit with a free pipe connector of ANY
            // system. Never a candidate - a DIAGNOSIS. When a pipe is
            // sitting against a unit and still reports nothing to join
            // to, the reason is almost always that the two carry
            // different system classifications, and only naming both
            // of them makes that visible.
            public string NearestAny = string.Empty;

            public double NearestAnyGap;

            public int NearestAnySide;
        }

        // A connector on a piece of equipment that a pipe should be on
        public class OnUnit
        {
            public long EquipmentId;
            public int ConnectorId;

            public string Equipment = string.Empty;

            public XYZ Point;

            public int Side;

            public double Gap;

            // Several free connectors of the same system were on this
            // unit and only the distance separated them. Said out loud
            // rather than hidden - see QACHWALIGN.Pick.
            public bool Ambiguous;

            // The connector carries no system classification at all,
            // so it was matched on position rather than on system.
            // Said in the finding, never hidden.
            public bool Unclassified;
        }

        public class Scan
        {
            public List<LooseEnd> Ends = new List<LooseEnd>();

            public int Pipes;
            public int Equipment;

            // Of the equipment found, how many had a free piping
            // connector at all. "18 units in scope" and "18 units with
            // somewhere to plug into" are very different facts, and
            // the difference is most of the answer when nothing joins.
            public int EquipmentFree;

            // How many open ends carry each system, and how many free
            // equipment connectors carry each system. Side by side
            // these two answer "why did nothing match?" in one look.
            public Dictionary<int, int> PipeSides =
                new Dictionary<int, int>();

            public Dictionary<int, int> UnitSides =
                new Dictionary<int, int>();
        }

        // ==================================================

        public static Scan Of(
            Document doc, QACONTEXT context, QASETTINGS settings)
        {
            object cached;

            if (context != null
                && context.Shared.TryGetValue(ScanKey, out cached))
            {
                var already = cached as Scan;

                if (already != null)
                    return already;
            }

            Scan scan = Walk(
                doc,
                context == null ? null : context.Scope,
                settings);

            if (context != null)
                context.Shared[ScanKey] = scan;

            return scan;
        }

        private static Scan Walk(
            Document doc, QASCOPE scope, QASETTINGS settings)
        {
            var scan = new Scan();

            var loose = new List<LooseEnd>();

            // ---- every open end on the chilled water pipework -------
            foreach (Element element in Pipework(doc, scope, settings))
            {
                scan.Pipes++;

                ConnectorManager manager = QADUCTS.Manager(element);

                if (manager == null)
                    continue;

                bool isPipe = element is MEPCurve;

                try
                {
                    foreach (Connector connector in manager.Connectors)
                    {
                        if (connector.Domain != Domain.DomainPiping)
                            continue;

                        if (connector.ConnectorType != ConnectorType.End)
                            continue;

                        if (connector.IsConnected)
                            continue;

                        loose.Add(new LooseEnd
                        {
                            OwnerId = element.Id.ToValue(),
                            ConnectorId = connector.Id,
                            Point = connector.Origin,
                            Facing = Facing(connector),
                            Side = SideOf(connector),
                            SystemName = SystemNameOf(element),
                            What = QAEQUIP.Describe(element),
                            OnPipe = isPipe
                        });
                    }
                }
                catch
                {
                    // A family that will not enumerate its connectors is
                    // skipped rather than losing the whole scan
                }
            }

            // ---- which of them face each other ----------------------
            double reach = Reach(settings);

            Meeting(loose, reach);

            // ---- and which belong on a piece of equipment -----------
            OnEquipment(doc, scope, loose, reach, scan, settings);

            scan.Ends = loose;

            foreach (LooseEnd end in loose)
                Tally(scan.PipeSides, end.Side);

            Report(scan, reach);

            return scan;
        }

        // WHY NOTHING MATCHED IS NOT A GUESSING GAME.
        //
        // The fix has had a log from the start and it settled two rounds
        // of theorising in one read each. The SCAN had none - so a run
        // that found 246 open ends and joined nothing left no trace at
        // all of what it had looked at. It does now.
        //
        // The two tallies are the whole point: put the systems carried
        // by the open PIPE ends beside the systems carried by the free
        // EQUIPMENT connectors and a mismatch is visible instantly,
        // which is the one cause nothing on the drawing reveals.
        private static void Report(Scan scan, double reach)
        {
            try
            {
                QACHWLOG.Start("CHW disconnections: the scan");

                QACHWLOG.Say("looking for a unit within " +
                             QAGRIDS.FromFeet(reach).ToString("N0") + " mm");

                QACHWLOG.Say(scan.Pipes + " pipework element(s) in scope");
                QACHWLOG.Say(scan.Ends.Count + " open end(s)");

                QACHWLOG.Say(scan.Equipment + " equipment in scope, " +
                             scan.EquipmentFree +
                             " with a free pipe connector");

                QACHWLOG.Head("systems on the open PIPE ends");
                Listing(scan.PipeSides);

                QACHWLOG.Head("systems on the free EQUIPMENT connectors");
                Listing(scan.UnitSides);

                QACHWLOG.Head("open ends that found no unit");

                int said = 0;

                foreach (LooseEnd end in scan.Ends)
                {
                    if (end.Unit != null || said >= 20)
                        continue;

                    said++;

                    QACHWLOG.Say(
                        end.What + " " + end.OwnerId + "  -  " +
                        SideName(end.Side) +
                        (end.NearestAny.Length == 0
                             ? "  -  no equipment connector anywhere"
                             : "  -  nearest is " + end.NearestAny +
                               " at " +
                               QAGRIDS.FromFeet(end.NearestAnyGap)
                                   .ToString("N0") + " mm carrying " +
                               SideName(end.NearestAnySide)));
                }

                if (said == 0)
                    QACHWLOG.Say("   none - every open end found a unit");

                QACHWLOG.Save();
            }
            catch
            {
                // A log that cannot be written is not worth an error
            }
        }

        private static void Listing(Dictionary<int, int> tally)
        {
            if (tally.Count == 0)
            {
                QACHWLOG.Say("   none");

                return;
            }

            foreach (KeyValuePair<int, int> each in tally)
                QACHWLOG.Say("   " + SideName(each.Key) + "  -  " +
                             each.Value);
        }

        // Pipes, fittings and accessories on the ticked CHW systems
        private static List<Element> Pipework(
            Document doc, QASCOPE scope, QASETTINGS settings)
        {
            var found = new List<Element>();

            var categories = new[]
            {
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_FlexPipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory
            };

            foreach (BuiltInCategory category in categories)
            {
                var collector = QACHECKS.Look(doc, scope)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();

                foreach (Element element in collector)
                {
                    if (Ours(element, settings))
                        found.Add(element);
                }
            }

            return found;
        }

        // WHICH SYSTEM TYPES TO CHECK IS THE USER'S CALL, AND THIS
        // PAGE ASKS FOR ITSELF.
        //
        // It used to borrow the CHW SIZING page's ticked system types,
        // which was wrong twice over. A circuit is a flow AND a return,
        // and that page pre-ticks by NAME: a model whose return system
        // type is not called "return" left the return list empty, so
        // this page saw supply pipes only and silently did half the job
        // - user-reported. It was also a coupling that should never
        // have existed; the tabs are separate pages with separate
        // scopes and separate checks.
        //
        // So the page has its own list, pre-ticked on Revit's own
        // CLASSIFICATION rather than on names, and NOTHING TICKED means
        // every hydronic type - which is exactly the behaviour it had
        // before the list existed.
        public static bool Ours(Element element, QASETTINGS settings)
        {
            if (element == null)
                return false;

            if (settings != null &&
                settings.ChwJointSystemTypeIds.Count > 0)
            {
                long id = SystemTypeIdOf(element);

                if (id != 0)
                    return settings.ChwJointSystemTypeIds.Contains(id);

                // AN ELEMENT ON NO SYSTEM HAS NO TYPE TO MATCH, and on
                // THIS page that is the normal case rather than a rare
                // one: a fitting whose pipes were never joined up is
                // precisely what the page exists to find, and Revit
                // gives it no system type at all. Dropping it would
                // hide the disconnections nearest the break. So it
                // falls through to the classification test below, which
                // is the widest honest answer available for it.
            }

            return IsHydronic(SideOfElement(element));
        }

        // BOTH SIDES. Asked of Revit's own enum rather than of a number
        // copied into a Revit-free file, which is an assumption with no
        // way to check it.
        public static bool IsHydronic(int system)
        {
            return system == (int)PipeSystemType.SupplyHydronic ||
                   system == (int)PipeSystemType.ReturnHydronic;
        }

        // ==================================================
        // Two ends that were meant to be one joint
        // ==================================================

        private static void Meeting(List<LooseEnd> loose, double reach)
        {
            for (int i = 0; i < loose.Count; i++)
            {
                if (loose[i].Meets != null)
                    continue;

                for (int j = i + 1; j < loose.Count; j++)
                {
                    if (loose[j].Meets != null)
                        continue;

                    if (loose[i].OwnerId == loose[j].OwnerId)
                        continue;

                    if (!Near(loose[i], loose[j], reach))
                        continue;

                    // They must FACE each other. Two ends side by side
                    // pointing the same way are two different runs, not
                    // a joint somebody forgot.
                    if (!Facing(loose[i], loose[j]))
                        continue;

                    loose[i].Meets = loose[j];
                    loose[j].Meets = loose[i];

                    break;
                }
            }
        }

        private static bool Near(LooseEnd a, LooseEnd b, double reach)
        {
            try
            {
                return a.Point != null && b.Point != null &&
                       a.Point.DistanceTo(b.Point) <= reach;
            }
            catch
            {
                return false;
            }
        }

        private static bool Facing(LooseEnd a, LooseEnd b)
        {
            try
            {
                if (a.Facing == null || b.Facing == null)
                    return false;

                return a.Facing.DotProduct(b.Facing) < -0.7;
            }
            catch
            {
                return false;
            }
        }

        // ==================================================
        // Ends that belong on a fan coil
        // ==================================================

        // Only the equipment families the page was told to care about.
        // Nothing ticked means every family, so a model nobody has set
        // up still reports something.
        public static bool WantedFamily(Element element, QASETTINGS settings)
        {
            if (settings == null || settings.ChwEquipFamilyIds.Count == 0)
                return true;

            try
            {
                var instance = element as FamilyInstance;

                if (instance == null || instance.Symbol == null ||
                    instance.Symbol.Family == null)
                    return false;

                return settings.ChwEquipFamilyIds.Contains(
                    instance.Symbol.Family.Id.ToValue());
            }
            catch
            {
                return false;
            }
        }

        // One free connector on one unit, read ONCE.
        //
        // Everything below measures distances in a double loop over
        // ends and units, and a Connector's Origin, PipeSystemType and
        // Description are each an API read. Caching them here turns the
        // inner loop into plain arithmetic - without it a model with a
        // thousand loose ends and five hundred units would make
        // millions of interop calls to answer a question about points.
        private class Spot
        {
            public int ConnectorId;
            public XYZ Point;
            public int Side;
            public string Description = string.Empty;
        }

        private class Unit
        {
            public long Id;
            public string Name = string.Empty;
            public List<Spot> Free = new List<Spot>();
        }

        // A pipe end and a unit it could go on, with the distance
        // between them
        private class Pairing
        {
            public LooseEnd End;
            public Unit Unit;
            public double Gap;
        }

        private static void OnEquipment(
            Document doc,
            QASCOPE scope,
            List<LooseEnd> loose,
            double reach,
            Scan scan,
            QASETTINGS settings)
        {
            var units = new List<Unit>();

            var collector = QACHECKS.Look(doc, scope)
                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                .WhereElementIsNotElementType();

            int found = 0;

            foreach (Element element in collector)
            {
                if (!WantedFamily(element, settings))
                    continue;

                found++;

                Unit unit = Read(element);

                if (unit.Free.Count > 0)
                {
                    units.Add(unit);

                    foreach (Spot spot in unit.Free)
                        Tally(scan.UnitSides, spot.Side);
                }
            }

            scan.Equipment = found;
            scan.EquipmentFree = units.Count;

            if (units.Count == 0)
                return;

            // ---- every (pipe end, unit) worth considering -----------
            //
            // EACH PIPE GOES TO ITS NEAREST UNIT, and that is the whole
            // point of building this list before claiming anything.
            //
            // This used to be a loop over UNITS, each one taking any
            // unclaimed end within reach - so a pipe 250 mm from one
            // fan coil and 40 mm from another went to whichever the
            // collector happened to return first. Nothing about that
            // order means anything, and the pipe was silently joined to
            // the wrong unit. USER-REPORTED as "it should connect the
            // disconnected pipes to their nearest FCU".
            bool unknownOk =
                settings == null || settings.ChwJoinUnknownSystem;

            var pairs = new List<Pairing>();

            foreach (LooseEnd end in loose)
            {
                if (!end.OnPipe || end.Point == null)
                    continue;

                foreach (Unit unit in units)
                {
                    // THE SYSTEM DECIDES WHETHER THIS UNIT IS A
                    // CANDIDATE AT ALL. Measuring to a return connector
                    // would let a supply pipe pick, and be held by, a
                    // unit it can never actually be joined to - so the
                    // pipe would report "nothing within reach" with a
                    // fan coil sitting next to it.
                    double gap = double.MaxValue;

                    // ...but the nearest unit of ANY system is worth
                    // knowing even though it is not a candidate, because
                    // that is the case a person cannot diagnose from
                    // the drawing: the pipe is touching the unit and
                    // the two simply do not agree about what they are.
                    double anyGap = double.MaxValue;
                    int anySide = QACHWSIDE.Undefined;

                    foreach (Spot spot in unit.Free)
                    {
                        double each = Length(end.Point, spot.Point);

                        if (each < anyGap)
                        {
                            anyGap = each;
                            anySide = spot.Side;
                        }

                        if (!QACHWSIDE.Match(
                                end.Side, spot.Side, unknownOk))
                            continue;

                        if (each < gap)
                            gap = each;
                    }

                    if (anyGap < double.MaxValue &&
                        (end.NearestAny.Length == 0 ||
                         anyGap < end.NearestAnyGap))
                    {
                        end.NearestAny = unit.Name;
                        end.NearestAnyGap = anyGap;
                        end.NearestAnySide = anySide;
                    }

                    if (gap == double.MaxValue)
                        continue;

                    // The nearest unit of the right system is worth
                    // remembering WHETHER OR NOT it is in reach. Out of
                    // reach it explains the refusal; in reach it is
                    // still the answer when that unit's connectors all
                    // go to nearer pipes and this one is left over.
                    if (end.NearestUnit.Length == 0 ||
                        gap < end.NearestGap)
                    {
                        end.NearestUnit = unit.Name;
                        end.NearestGap = gap;
                    }

                    if (gap > reach)
                        continue;

                    pairs.Add(new Pairing
                    {
                        End = end,
                        Unit = unit,
                        Gap = gap
                    });
                }
            }

            // ---- nearest first, and each side claimed once ----------
            //
            // ALIGNTERMINALS' greedy matching, which this page has used
            // from the start: the closest pipe-to-unit pairing in the
            // whole model is settled first, then the next closest of
            // what is left.
            pairs.Sort(delegate (Pairing a, Pairing b)
            {
                int by = a.Gap.CompareTo(b.Gap);

                // A deterministic tie-break, so two runs of the same
                // model cannot disagree
                return by != 0 ? by : a.Unit.Id.CompareTo(b.Unit.Id);
            });

            var taken = new Dictionary<long, HashSet<int>>();

            foreach (Pairing pair in pairs)
            {
                if (pair.End.Unit != null)
                    continue;

                HashSet<int> already;

                if (!taken.TryGetValue(pair.Unit.Id, out already))
                {
                    already = new HashSet<int>();
                    taken[pair.Unit.Id] = already;
                }

                Claim(pair.Unit, pair.End, already, unknownOk);
            }
        }

        private static void Tally(Dictionary<int, int> into, int side)
        {
            int already;

            into[side] = into.TryGetValue(side, out already)
                ? already + 1
                : 1;
        }

        private static Unit Read(Element element)
        {
            var unit = new Unit
            {
                Id = element.Id.ToValue(),
                Name = QAEQUIP.Describe(element)
            };

            foreach (Connector connector in QAEQUIP.PipeEnds(element))
            {
                if (connector.IsConnected)
                    continue;

                XYZ origin = Origin(connector);

                if (origin == null)
                    continue;

                unit.Free.Add(new Spot
                {
                    ConnectorId = connector.Id,
                    Point = origin,
                    Side = SideOf(connector),
                    Description = DescriptionOf(connector)
                });
            }

            return unit;
        }

        // WHICH UNIT is settled above, by distance. WHICH CONNECTOR ON
        // IT is this, and it is a different question with a different
        // answer: system first, then the connector's description, then
        // the distance - QACHWALIGN.Pick.
        //
        // Sending a supply pipe to a return connector because it was
        // two centimetres nearer is the one mistake here that nothing
        // downstream would ever catch.
        private static void Claim(
            Unit unit, LooseEnd end, HashSet<int> taken, bool unknownOk)
        {
            var among = new List<QACHWALIGN.Candidate>();

            foreach (Spot spot in unit.Free)
            {
                among.Add(new QACHWALIGN.Candidate
                {
                    System = spot.Side,
                    Distance = Length(end.Point, spot.Point),

                    // What the family author called this connector,
                    // against what the pipe's system is called. The
                    // only thing that tells a chilled coil from a
                    // heating one on a four-pipe unit.
                    Hint = QACHWALIGN.Hint(
                        spot.Description, end.SystemName)
                });
            }

            QACHWALIGN.Choice choice =
                QACHWALIGN.Pick(end.Side, among, taken, unknownOk);

            if (!choice.Found)
                return;

            end.Unit = new OnUnit
            {
                EquipmentId = unit.Id,
                ConnectorId = unit.Free[choice.At].ConnectorId,
                Equipment = unit.Name,
                Point = unit.Free[choice.At].Point,
                Side = among[choice.At].System,
                Gap = among[choice.At].Distance,
                Ambiguous = choice.Ambiguous,
                Unclassified =
                    among[choice.At].System == QACHWSIDE.Undefined
            };
        }

        public static string DescriptionOf(Connector connector)
        {
            try
            {
                return connector.Description ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static double Length(XYZ from, XYZ to)
        {
            try
            {
                return from == null || to == null
                    ? double.MaxValue
                    : from.DistanceTo(to);
            }
            catch
            {
                return double.MaxValue;
            }
        }

        private static XYZ Origin(Connector connector)
        {
            try
            {
                return connector.Origin;
            }
            catch
            {
                return null;
            }
        }

        // ==================================================
        // Reading a system off Revit
        // ==================================================

        public static int SideOf(Connector connector)
        {
            try
            {
                return (int)connector.PipeSystemType;
            }
            catch
            {
                return QACHWSIDE.Undefined;
            }
        }

        public static int SideOfElement(Element element)
        {
            try
            {
                var curve = element as MEPCurve;

                if (curve != null && curve.MEPSystem != null)
                {
                    var piping = curve.MEPSystem as PipingSystem;

                    if (piping != null)
                        return (int)piping.SystemType;
                }

                foreach (Connector connector in QAEQUIP.PipeEnds(element))
                {
                    int side = SideOf(connector);

                    if (side != QACHWSIDE.Undefined)
                        return side;
                }
            }
            catch
            {
            }

            return QACHWSIDE.Undefined;
        }

        public static long SystemTypeIdOf(Element element)
        {
            try
            {
                Parameter parameter = element.get_Parameter(
                    BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);

                if (parameter != null && parameter.HasValue &&
                    parameter.StorageType == StorageType.ElementId)
                    return parameter.AsElementId().ToValue();
            }
            catch
            {
            }

            return 0;
        }

        public static string SystemNameOf(Element element)
        {
            try
            {
                Parameter parameter = element.get_Parameter(
                    BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);

                if (parameter != null && parameter.HasValue)
                    return parameter.AsValueString() ?? string.Empty;
            }
            catch
            {
            }

            return string.Empty;
        }

        // Revit's own name for a connector's system classification,
        // used ONLY to explain a refusal. "SupplyHydronic does not
        // match OtherPipe" is something somebody can act on;
        // "system 11 does not match system 7" is not.
        public static string SideName(int side)
        {
            try
            {
                return side == QACHWSIDE.Undefined
                    ? "no system"
                    : ((PipeSystemType)side).ToString();
            }
            catch
            {
                return "system " + side;
            }
        }

        public static XYZ Facing(Connector connector)
        {
            try
            {
                return connector.CoordinateSystem.BasisZ;
            }
            catch
            {
                return null;
            }
        }
    }
}
