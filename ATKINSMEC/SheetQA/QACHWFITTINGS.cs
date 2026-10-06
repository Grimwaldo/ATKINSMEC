using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // Making the FITTINGS follow the pipes.
    //
    // This page was built on "set the pipes and the fittings follow",
    // which is how Revit behaves when a person changes a size on the
    // drawing. IT IS NOT WHAT HAPPENS THROUGH THE API - reported from a
    // real model, where the pipes resized and the fittings stayed as
    // they were. There is no call anywhere in the API that means "make
    // the fittings follow", so it has to be driven, fitting by fitting.
    //
    // A fitting should be the size of the pipes it joins. That is the
    // whole rule, and it handles a tee or a reducer without a special
    // case: each connector is set to the pipe on ITS side, so three
    // pipes of three sizes give a tee of three sizes.
    //
    // Two routes, gentlest first:
    //
    //   1. SET THE CONNECTOR. Autodesk's own words on Connector.Radius:
    //      "In order to set this property, it must be mapped to a
    //      writable instance parameter in the family definition." Many
    //      fittings are driven by a lookup table instead, and will
    //      either refuse the write or take it and snap back - so it is
    //      set, regenerated, and then READ AGAIN to see whether it
    //      really took.
    //
    //   2. REPLACE IT. Revit sizes a NEW fitting from the pipes it is
    //      given and the pipe type's routing preferences, which is
    //      exactly what is wanted. So the old one is deleted and a
    //      fresh one made in its place - the same thing QADUCTFIX does
    //      with a hanging tap, and for the same reason.
    //
    // An ACCESSORY is never replaced. A valve is a thing somebody chose
    // and specified; deleting it to get the size right would throw that
    // away. It gets route 1 and is reported if it will not take it.
    public class QACHWFITTINGS
    {
        // How close two connectors have to be before they count as
        // agreeing. Half a millimetre - this is "are these the same
        // size", not "is this the right size".
        private const double AgreeMm = QACHWSHAPE.AgreeMm;

        // When two pipes count as one straight line rather than a turn -
        // cos(2 degrees), Revit's own limit: QACHWSHAPE.Straight

        public class Result
        {
            // Already the size of its pipes - nothing to do
            public int Followed { get; set; }

            // Route 1: it took the size
            public int Resized { get; set; }

            // Route 2: it would not, so a fresh one was made
            public int Replaced { get; set; }

            // Neither worked
            public int Stubborn { get; set; }

            // Pipes saved by sliding a fitting out of the way before
            // this one was grown
            public int Stretched { get; set; }

            // Replaced fittings no longer exist. The joint repair has
            // to be told, or it finds a joint to a deleted element and
            // reports it as broken.
            public List<long> Gone { get; set; } = new List<long>();

            public List<string> Notes { get; set; } = new List<string>();

            public int Touched
            {
                get { return Resized + Replaced; }
            }

            public void Note(string text)
            {
                if (!Notes.Contains(text))
                    Notes.Add(text);
            }
        }

        // ==================================================
        // Which fittings are in question
        // ==================================================

        // Everything joined to these pipes that takes its size from
        // them. Deliberately from the MODEL rather than from the
        // findings: a fitting between two resized pipes may never have
        // been listed, because until the pipes moved it was right.
        public static List<long> Touching(
            Document doc, IEnumerable<long> pipeIds)
        {
            var found = new List<long>();
            var seen = new HashSet<long>();

            foreach (long id in pipeIds)
            {
                Element pipe = doc.GetElement(REVITCOMPAT.NewId(id));

                if (pipe == null)
                    continue;

                foreach (Connector end in QACHWRUNS.Ends(pipe))
                {
                    foreach (Connector other in Refs(end))
                    {
                        try
                        {
                            Element owner = other.Owner;

                            if (owner == null)
                                continue;

                            if (!QACHWRUNS.IsFitting(owner)
                                && !QACHWRUNS.IsAccessory(owner))
                                continue;

                            long ownerId = owner.Id.ToValue();

                            if (seen.Add(ownerId))
                                found.Add(ownerId);
                        }
                        catch
                        {
                            // that reference is not usable
                        }
                    }
                }
            }

            return found;
        }

        // Must be called inside a transaction, after the pipes have
        // been written and the document regenerated.
        public static void Reconcile(
            Document doc, IEnumerable<long> fittingIds, Result result)
        {
            foreach (long id in new HashSet<long>(fittingIds))
                One(doc, id, result);
        }

        // ==================================================
        // One fitting
        // ==================================================

        // What one connector on the fitting should be, and what it is
        private class Want
        {
            public int ConnectorId;

            public double NowMm;
            public double WantMm;
            public double WantRadiusFeet;

            // The pipe on the other side of it
            public long PipeId;
            public int PipeConnectorId;

            public bool Agrees
            {
                get { return Math.Abs(NowMm - WantMm) <= AgreeMm; }
            }
        }

        private static void One(Document doc, long id, Result result)
        {
            Element fitting = doc.GetElement(REVITCOMPAT.NewId(id));

            if (fitting == null)
                return;

            if (!QACHWRUNS.IsFitting(fitting)
                && !QACHWRUNS.IsAccessory(fitting))
                return;

            List<Want> wants = WantsOf(doc, fitting);

            if (wants.Count == 0)
                return;

            bool agreed = true;

            foreach (Want want in wants)
            {
                if (!want.Agrees)
                    agreed = false;
            }

            if (agreed)
            {
                result.Followed++;

                return;
            }

            // MAKE THE ROOM FIRST, THEN SIZE IT. Growing a fitting is
            // what eats the pipe beside it, so this is the moment that
            // matters - not the earlier pass over the runs, which
            // measured sizes the model has since moved on from. Slide
            // whatever has to slide, and only then set the size.
            Room(doc, id, wants, result);

            if (TryRadius(doc, id, wants, result))
            {
                result.Resized++;

                return;
            }

            // AN EQUAL FITTING TAKES THE LARGEST PIPE ON IT.
            //
            // Setting each connector to the pipe on its own side is
            // right for a reducing tee, and impossible for a family
            // with ONE nominal size driving every end - M_Tee Generic
            // is one, and no per-connector write can ever satisfy all
            // three, so the check above rolls back and the fitting is
            // left at its old size next to a pipe four times its bore.
            // Reported from a real model: a 40-40-40 tee on a DN100
            // main.
            //
            // So: ask it to be the biggest thing joined to it. That is
            // what the fitting physically is, and the reduction then
            // belongs to the pipe on the branch, where it can be seen.
            if (TryBiggest(doc, id, wants, result))
            {
                result.Resized++;

                return;
            }

            if (QACHWRUNS.IsAccessory(fitting))
            {
                result.Stubborn++;

                result.Note(
                    "A valve or other accessory would not take the size " +
                    "of its pipes. For most of them the size is a choice " +
                    "of family type rather than a number, and picking a " +
                    "different valve is not a decision for an add-in - " +
                    "so it was left, and it is still on the list.");

                return;
            }

            if (TryReplace(doc, id, wants, result))
            {
                result.Replaced++;

                result.Gone.Add(id);

                return;
            }

            result.Stubborn++;
        }

        // Slide something along so this fitting has room to grow into,
        // before it is grown. The size it is about to become is the
        // biggest thing that will be on it.
        private static void Room(
            Document doc, long id, List<Want> wants, Result result)
        {
            double biggest = 0;

            foreach (Want want in wants)
            {
                if (want.WantMm > biggest)
                    biggest = want.WantMm;
            }

            if (biggest <= 0)
                return;

            var room = new QACHWROOM.Result();

            QACHWROOM.Ensure(doc, id, biggest, room);

            result.Stretched += room.Stretched;

            foreach (string note in room.Notes)
                result.Note(note);
        }

        private static List<Want> WantsOf(Document doc, Element fitting)
        {
            var wants = new List<Want>();

            long id = fitting.Id.ToValue();

            foreach (Connector mine in QACHWRUNS.Ends(fitting))
            {
                Connector theirs = PipeSide(mine, id);

                if (theirs == null)
                    continue;

                try
                {
                    wants.Add(new Want
                    {
                        ConnectorId = mine.Id,
                        NowMm = QAGRIDS.FromFeet(mine.Radius * 2.0),
                        WantMm = QAGRIDS.FromFeet(theirs.Radius * 2.0),
                        WantRadiusFeet = theirs.Radius,
                        PipeId = theirs.Owner.Id.ToValue(),
                        PipeConnectorId = theirs.Id
                    });
                }
                catch
                {
                    // A connector that will not give a radius is not
                    // round, and this table is about DN
                }
            }

            return wants;
        }

        // The pipe on the other side of this connector, if there is one.
        //
        // THE Domain TEST IS WHAT MAKES THE `is MEPCurve` BELOW SAFE,
        // and on its own that cast would not be: insulation is in
        // AllRefs and PipeInsulation derives from MEPCurve, so a sleeve
        // passes it. See QACHWJOINTFIX.Pipework. Refs() hands back
        // everything unfiltered, so the filtering has to happen here.
        private static Connector PipeSide(Connector mine, long from)
        {
            foreach (Connector other in Refs(mine))
            {
                try
                {
                    if (other.Owner == null
                        || other.Owner.Id.ToValue() == from
                        || other.Domain != Domain.DomainPiping
                        || other.ConnectorType != ConnectorType.End)
                        continue;

                    if (other.Owner is MEPCurve)
                        return other;
                }
                catch
                {
                    // that reference is not usable
                }
            }

            return null;
        }

        private static List<Connector> Refs(Connector connector)
        {
            var found = new List<Connector>();

            try
            {
                if (!connector.IsConnected)
                    return found;

                foreach (Connector other in connector.AllRefs)
                    found.Add(other);
            }
            catch
            {
                // nothing usable on this one
            }

            return found;
        }

        // ==================================================
        // 1. Set the connector
        // ==================================================

        private static bool TryRadius(
            Document doc, long id, List<Want> wants, Result result)
        {
            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    foreach (Want want in wants)
                    {
                        if (want.Agrees)
                            continue;

                        Connector mine = QADUCTS.Lookup(
                            doc, id, want.ConnectorId);

                        if (mine == null)
                        {
                            sub.RollBack();

                            return false;
                        }

                        // Throws unless the family maps this to a
                        // writable instance parameter
                        mine.Radius = want.WantRadiusFeet;
                    }

                    doc.Regenerate();

                    // Writing it is not the same as it taking. A family
                    // driven by a lookup table can accept the write and
                    // then go straight back to what the table says, so
                    // the only honest test is to read it again.
                    foreach (Want want in wants)
                    {
                        Connector mine = QADUCTS.Lookup(
                            doc, id, want.ConnectorId);

                        if (mine == null
                            || Math.Abs(
                                   QAGRIDS.FromFeet(mine.Radius * 2.0)
                                   - want.WantMm) > AgreeMm)
                        {
                            sub.RollBack();

                            return false;
                        }
                    }

                    // THIS is where a pipe actually dies. Growing the
                    // fitting is what eats the pipe next to it, and it
                    // happens here rather than when the pipes were
                    // written - so the length has to be checked HERE,
                    // or nothing is looking when it matters.
                    if (Squashed(doc, id, wants, result))
                    {
                        sub.RollBack();

                        return false;
                    }

                    sub.Commit();

                    return true;
                }
                catch
                {
                    sub.RollBack();

                    return false;
                }
            }
        }

        // Every connector to the largest pipe on the fitting, for a
        // family that has only one size to give.
        private static bool TryBiggest(
            Document doc, long id, List<Want> wants, Result result)
        {
            double biggestMm = 0;
            double biggestFeet = 0;

            foreach (Want want in wants)
            {
                if (want.WantMm <= biggestMm)
                    continue;

                biggestMm = want.WantMm;
                biggestFeet = want.WantRadiusFeet;
            }

            if (biggestMm <= 0)
                return false;

            // Every end already at the biggest is the per-connector
            // case, which has just been tried and failed
            bool anySmaller = false;

            foreach (Want want in wants)
            {
                if (want.WantMm < biggestMm - AgreeMm)
                    anySmaller = true;
            }

            if (!anySmaller)
                return false;

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    foreach (Want want in wants)
                    {
                        Connector mine = QADUCTS.Lookup(
                            doc, id, want.ConnectorId);

                        if (mine == null)
                        {
                            sub.RollBack();

                            return false;
                        }

                        mine.Radius = biggestFeet;
                    }

                    doc.Regenerate();

                    foreach (Want want in wants)
                    {
                        Connector mine = QADUCTS.Lookup(
                            doc, id, want.ConnectorId);

                        if (mine == null
                            || Math.Abs(
                                   QAGRIDS.FromFeet(mine.Radius * 2.0)
                                   - biggestMm) > AgreeMm)
                        {
                            sub.RollBack();

                            return false;
                        }
                    }

                    if (Squashed(doc, id, wants, result))
                    {
                        sub.RollBack();

                        return false;
                    }

                    sub.Commit();

                    QACHWLOG.Say(
                        "      fitting " + id + " is an equal fitting, so " +
                        "it took DN" + biggestMm.ToString("N0") +
                        " - the largest pipe on it");

                    result.Note(
                        "Some fittings have a single size for every end - " +
                        "an equal tee cannot be a reducing tee - so they " +
                        "were made the size of the LARGEST pipe joined to " +
                        "them. The reduction then belongs to the branch " +
                        "pipe, where it can be seen.");

                    return true;
                }
                catch
                {
                    sub.RollBack();

                    return false;
                }
            }
        }

        // ==================================================
        // 2. Replace it
        // ==================================================

        private static bool TryReplace(
            Document doc, long id, List<Want> wants, Result result)
        {
            Element fitting = doc.GetElement(REVITCOMPAT.NewId(id));

            if (fitting == null)
                return false;

            // Every end has to have a pipe on it. A fitting with a
            // spare open end would come back with that end missing,
            // which is a worse model than a fitting of the wrong size.
            if (QACHWRUNS.Ends(fitting).Count != wants.Count
                || wants.Count < 2
                || wants.Count > 4)
            {
                result.Note(
                    "Some fittings would not take the size and could not " +
                    "be replaced either, because an end of them has no " +
                    "pipe on it. They were left exactly as they were.");

                return false;
            }

            PartType kind = KindOf(fitting);

            var axes = new List<XYZ>();

            foreach (Want want in wants)
            {
                XYZ axis = Facing(QADUCTS.Lookup(
                    doc, want.PipeId, want.PipeConnectorId));

                if (axis == null)
                    return false;

                axes.Add(axis);
            }

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    doc.Delete(REVITCOMPAT.NewId(id));

                    doc.Regenerate();

                    var pipes = new List<Connector>();

                    foreach (Want want in wants)
                    {
                        Connector pipe = QADUCTS.Lookup(
                            doc, want.PipeId, want.PipeConnectorId);

                        if (pipe == null)
                        {
                            sub.RollBack();

                            return false;
                        }

                        pipes.Add(pipe);
                    }

                    if (!Make(doc, kind, pipes, axes, wants))
                    {
                        sub.RollBack();

                        result.Note(
                            "Some fittings are of a kind this cannot make " +
                            "again - anything other than an elbow, tee, " +
                            "cross, reducer or union. They were left as " +
                            "they were rather than removed.");

                        return false;
                    }

                    doc.Regenerate();

                    if (Squashed(doc, id, wants, result))
                    {
                        sub.RollBack();

                        return false;
                    }

                    sub.Commit();

                    return true;
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Note(
                        "A fitting would not take the size and a new one " +
                        "would not go in either: " + Short(ex) + ". The " +
                        "old one is still there - a replacement that " +
                        "fails is undone on its own.");

                    return false;
                }
            }
        }

        // Revit makes the new one the size of the pipes it is given, so
        // all this has to get right is WHICH fitting to ask for.
        private static bool Make(
            Document doc,
            PartType kind,
            List<Connector> pipes,
            List<XYZ> axes,
            List<Want> wants)
        {
            if (pipes.Count == 2)
                return Join(doc, kind, pipes[0], pipes[1]);

            if (pipes.Count == 3)
            {
                int branch = Odd(axes);

                var main = new List<Connector>();

                for (int i = 0; i < 3; i++)
                {
                    if (i != branch)
                        main.Add(pipes[i]);
                }

                doc.Create.NewTeeFitting(
                    main[0], main[1], pipes[branch]);

                return true;
            }

            if (pipes.Count == 4)
            {
                int partner = Partner(axes, 0);

                var rest = new List<int>();

                for (int i = 1; i < 4; i++)
                {
                    if (i != partner)
                        rest.Add(i);
                }

                doc.Create.NewCrossFitting(
                    pipes[0], pipes[partner], pipes[rest[0]], pipes[rest[1]]);

                return true;
            }

            return false;
        }

        // Putting a two-ended fitting between two pipe ends: an elbow
        // where they turn, a union where they meet in line at the same
        // size, a reducer where they meet in line at different sizes.
        // Shared with QACHWROOM, which cuts two joints to make room and
        // then has to put them back.
        public static bool Join(
            Document doc, PartType kind, Connector a, Connector b)
        {
            XYZ one = Facing(a);
            XYZ two = Facing(b);

            if (one == null || two == null)
                return false;

            bool sameSize;

            try
            {
                sameSize = QACHWSHAPE.SameSize(
                    QAGRIDS.FromFeet(a.Radius * 2.0),
                    QAGRIDS.FromFeet(b.Radius * 2.0));
            }
            catch
            {
                // A connector that will not give a radius is not round,
                // and a union is the gentler guess
                sameSize = true;
            }

            // WHICH FITTING is QACHWSHAPE's, Revit-free, so the test
            // compiles the real rule
            switch (QACHWSHAPE.Join(SaysOf(kind), one.DotProduct(two), sameSize))
            {
                case QACHWSHAPE.Two.Elbow:
                    doc.Create.NewElbowFitting(a, b);
                    break;

                case QACHWSHAPE.Two.Union:
                    doc.Create.NewUnionFitting(a, b);
                    break;

                default:
                    doc.Create.NewTransitionFitting(a, b);
                    break;
            }

            return true;
        }

        private static QACHWSHAPE.Says SaysOf(PartType kind)
        {
            switch (kind)
            {
                case PartType.Elbow: return QACHWSHAPE.Says.Elbow;
                case PartType.Transition: return QACHWSHAPE.Says.Transition;
                case PartType.Union: return QACHWSHAPE.Says.Union;
                default: return QACHWSHAPE.Says.Nothing;
            }
        }

        private static List<QAVEC> Vecs(List<XYZ> axes)
        {
            return axes.Select(a => new QAVEC(a.X, a.Y, a.Z)).ToList();
        }

        // Of three pipes into a tee, the branch is the one that is not
        // in line with the other two - QACHWSHAPE.Odd
        private static int Odd(List<XYZ> axes)
        {
            return QACHWSHAPE.Odd(Vecs(axes));
        }

        // Which of the others is most nearly in line with this one -
        // QACHWSHAPE.Partner
        private static int Partner(List<XYZ> axes, int of)
        {
            return QACHWSHAPE.Partner(Vecs(axes), of);
        }

        // Which way a connector faces. NOT Connector.Direction - that
        // is a FlowDirectionType, and it has cost time before.
        private static XYZ Facing(Connector connector)
        {
            if (connector == null)
                return null;

            try
            {
                return connector.CoordinateSystem.BasisZ;
            }
            catch
            {
                return null;
            }
        }

        private static PartType KindOf(Element fitting)
        {
            var instance = fitting as FamilyInstance;

            if (instance == null)
                return PartType.Undefined;

            var mechanical = instance.MEPModel as MechanicalFitting;

            if (mechanical == null)
                return PartType.Undefined;

            try
            {
                return mechanical.PartType;
            }
            catch
            {
                return PartType.Undefined;
            }
        }

        // Did growing this fitting leave any pipe on it too short to
        // live? Revit deletes one that is, and says "Line is too
        // short" - so a fitting that would cause that is put straight
        // back to the size it was. A fitting of the wrong size is
        // visible and fixable; a deleted pipe is neither.
        private static bool Squashed(
            Document doc, long fittingId, List<Want> wants, Result result)
        {
            foreach (Want want in wants)
            {
                if (doc.GetElement(REVITCOMPAT.NewId(want.PipeId)) == null)
                {
                    QACHWLOG.Say(
                        "      SQUASHED: pipe " + want.PipeId +
                        " is GONE after growing fitting " + fittingId);

                    Blame(result);

                    return true;
                }

                if (!QACHWROOM.TooShort(doc, want.PipeId))
                    continue;

                QACHWLOG.Say(
                    "      SQUASHED: pipe " + want.PipeId +
                    " went under the floor after growing fitting " +
                    fittingId);

                Blame(result);

                return true;
            }

            return false;
        }

        private static void Blame(Result result)
        {
            result.Note(
                "Some fittings were left at their old size because " +
                "growing them would have left the pipe next to them " +
                "too short to exist - Revit deletes one that short and " +
                "says \"Line is too short\". The fitting is the wrong " +
                "size now, which you can see and put right; the pipe is " +
                "still there, which you could not have got back.");
        }

        private static string Short(Exception ex)
        {
            string message = (ex.Message ?? string.Empty).Trim();

            if (message.Length > 140)
                message = message.Substring(0, 137) + "...";

            return message.Length == 0 ? ex.GetType().Name : message;
        }
    }
}
