using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Builds the pipe offset EQUIPDOGLEG plans: three new pipes of the
    // run's own type, system, level and size, two elbows from its routing
    // preferences, the run's insulation copied on, joined to the unit's
    // connector at one end and the pipe's open end at the other.
    //
    // Square, then 45, then 30 degrees, each its own SubTransaction and
    // READ BACK - the elbows Revit chooses decide what fits, not the plan.
    // Null when built and joined; else why not, with nothing left of it.
    public static class EQUIPOFFSET
    {
        public static string Build(
            Document doc, long unitId, int unitEnd, long pipeEndOwner,
            int pipeEnd, List<string> notes, string called)
        {
            string told = "no elbow angle fitted";

            foreach (double angle in EQUIPDOGLEG.Angles)
            {
                string why;

                using (var step = new SubTransaction(doc))
                {
                    step.Start();

                    try
                    {
                        why = Once(doc, unitId, unitEnd, pipeEndOwner, pipeEnd,
                            angle, notes, called);
                    }
                    catch (Exception ex)
                    {
                        why = ex.GetType().Name + ": " + EQUIPCHANGE.Short(ex);
                    }

                    if (why == null)
                    {
                        step.Commit();

                        return null;
                    }

                    step.RollBack();
                }

                EQUIPLOG.Say("   offset at " + angle.ToString("0") + " degrees: " + why);

                told = why;
            }

            return told;
        }

        private static string Once(
            Document doc, long unitId, int unitEnd, long pipeEndOwner,
            int pipeEnd, double angle, List<string> notes, string called)
        {
            Connector a = QADUCTS.Lookup(doc, unitId, unitEnd);
            Connector b = QADUCTS.Lookup(doc, pipeEndOwner, pipeEnd);

            if (a == null || b == null)
                return "its connectors could not be read";

            Pipe model = Model(doc, b.Owner);

            if (model == null)
                return "there is no pipe beyond it to take a type from";

            double diameter = Diameter(model);

            XYZ facing = EQUIPWALK.Facing(a);

            if (facing == null)
                return "the unit's connector will not say which way it faces";

            EQUIPDOGLEG plan = EQUIPDOGLEG.Plan(
                EQUIPWALK.V(a.Origin), EQUIPWALK.V(facing),
                EQUIPWALK.V(b.Origin), diameter, angle);

            if (!plan.Ok)
                return plan.Why;

            ElementId system = model.get_Parameter(
                BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId();

            ElementId level = model.ReferenceLevel == null
                ? model.LevelId
                : model.ReferenceLevel.Id;

            XYZ p1 = new XYZ(plan.P1.X, plan.P1.Y, plan.P1.Z);
            XYZ p2 = new XYZ(plan.P2.X, plan.P2.Y, plan.P2.Z);

            Pipe first = Make(doc, system, model.GetTypeId(), level, a.Origin, p1, diameter);
            Pipe across = Make(doc, system, model.GetTypeId(), level, p1, p2, diameter);
            Pipe last = Make(doc, system, model.GetTypeId(), level, p2, b.Origin, diameter);

            doc.Regenerate();

            FamilyInstance bend1 = doc.Create.NewElbowFitting(
                At(first, p1), At(across, p1));

            FamilyInstance bend2 = doc.Create.NewElbowFitting(
                At(across, p2), At(last, p2));

            if (bend1 == null || bend2 == null)
                return "Revit would not put an elbow there";

            doc.Regenerate();

            // The ends: the unit, and the pipe's open end
            a = QADUCTS.Lookup(doc, unitId, unitEnd);
            b = QADUCTS.Lookup(doc, pipeEndOwner, pipeEnd);

            Connector start = Nearest(first, a.Origin);
            Connector finish = Nearest(last, b.Origin);

            if (start == null || finish == null ||
                start.Origin.DistanceTo(a.Origin) > EQUIPREACH.Coincident ||
                finish.Origin.DistanceTo(b.Origin) > EQUIPREACH.Coincident)
                return "the new pipe's ends did not land on the connectors";

            start.ConnectTo(a);
            finish.ConnectTo(b);

            doc.Regenerate();

            // Read back: every new pipe still there and long enough, both
            // elbows joined both ways
            foreach (Pipe pipe in new[] { first, across, last })
            {
                if (!pipe.IsValidObject)
                    return "Revit removed one of the new pipes";

                var line = (pipe.Location as LocationCurve)?.Curve;

                if (line == null || line.Length < EQUIPDOGLEG.Shortest * 0.5)
                    return "the elbows leave a pipe too short (" +
                           EQUIPREACH.Mm(line == null ? 0 : line.Length) + " mm)";
            }

            foreach (FamilyInstance bend in new[] { bend1, bend2 })
            {
                foreach (Connector c in QADUCTS.Manager(bend).Connectors)
                {
                    if (!c.IsConnected)
                        return "an elbow was left unjoined";
                }
            }

            Insulate(doc, model, new ElementId[]
            {
                first.Id, across.Id, last.Id, bend1.Id, bend2.Id
            });

            string said = "offset " + called + " " + EQUIPREACH.Mm(plan.Offset) +
                          " mm across with two " + angle.ToString("0") +
                          "-degree elbows (new pipes " + first.Id.ToValue() +
                          ", " + across.Id.ToValue() + ", " + last.Id.ToValue() + ")";

            EQUIPLOG.Say("   " + said);

            if (notes != null)
                notes.Add(said);

            return null;
        }

        // The pipe to copy: the open end's own, or the first one beyond
        // the fitting it is on (a reducer straight on the unit, as
        // FCU-02's were)
        private static Pipe Model(Document doc, Element owner)
        {
            var pipe = owner as Pipe;

            if (pipe != null)
                return pipe;

            ConnectorManager manager = QADUCTS.Manager(owner);

            if (manager == null)
                return null;

            foreach (Connector c in manager.Connectors)
            {
                foreach (Connector other in c.AllRefs)
                {
                    var beyond = other.Owner as Pipe;

                    if (beyond != null && beyond.Id != owner.Id &&
                        EQUIPWALK.Pipework(beyond))
                        return beyond;
                }
            }

            return null;
        }

        private static double Diameter(Pipe pipe)
        {
            Parameter p = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);

            return p == null ? 20.0 / 304.8 : p.AsDouble();
        }

        private static Pipe Make(
            Document doc, ElementId system, ElementId type, ElementId level,
            XYZ from, XYZ to, double diameter)
        {
            Pipe pipe = Pipe.Create(doc, system, type, level, from, to);

            Parameter p = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);

            if (p != null && !p.IsReadOnly)
                p.Set(diameter);

            return pipe;
        }

        private static Connector At(Pipe pipe, XYZ point)
        {
            return Nearest(pipe, point);
        }

        private static Connector Nearest(MEPCurve curve, XYZ point)
        {
            Connector best = null;

            double closest = double.MaxValue;

            foreach (Connector c in curve.ConnectorManager.Connectors)
            {
                if (c.ConnectorType != ConnectorType.End)
                    continue;

                double d = c.Origin.DistanceTo(point);

                if (d < closest)
                {
                    closest = d;
                    best = c;
                }
            }

            return best;
        }

        // The run's insulation, on the new pieces too
        private static void Insulate(Document doc, Pipe model, IEnumerable<ElementId> on)
        {
            try
            {
                ICollection<ElementId> wraps =
                    InsulationLiningBase.GetInsulationIds(doc, model.Id);

                if (wraps == null || wraps.Count == 0)
                    return;

                PipeInsulation wrap = null;

                foreach (ElementId id in wraps)
                {
                    wrap = doc.GetElement(id) as PipeInsulation;

                    if (wrap != null)
                        break;
                }

                if (wrap == null)
                    return;

                foreach (ElementId id in on)
                {
                    try
                    {
                        PipeInsulation.Create(doc, id, wrap.GetTypeId(), wrap.Thickness);
                    }
                    catch
                    {
                        // A fitting that will not take it is still a
                        // joined fitting
                    }
                }
            }
            catch
            {
            }
        }
    }
}
