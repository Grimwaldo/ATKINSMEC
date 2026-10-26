using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Resizing chilled water pipe to the size its flow calls for.
    //
    // THE PIPES ARE WRITTEN FIRST AND THE FITTINGS ARE THEN DRIVEN TO
    // MATCH THEM. The first version wrote only the pipes, on the
    // reasonable belief that a fitting takes its size from the pipes it
    // joins - which is what happens when a person changes a size on the
    // drawing. Through the API it does not: reported from a real model,
    // where the pipes resized and the fittings stayed as they were.
    // QACHWFITTINGS does that second half, and the rule it follows is
    // still "a fitting is the size of its pipes" - the pipes are never
    // made to follow a fitting.
    //
    // Three things guard against leaving the model in pieces, in the
    // order they matter:
    //
    //   1. WHOLE RUNS AT ONCE. Resizing one pipe out of a continuous
    //      run puts a reducer in the middle of a straight length for no
    //      reason, and asks a joint to survive a change its neighbour
    //      did not make. QACHWRUNS groups the pipes that must move
    //      together; this sets them in one step.
    //
    //   2. ASK THE TYPE FIRST. A size that is not in the pipe type's
    //      routing preferences has no segment behind it and no fitting
    //      to join it with. That is the commonest way a resize fails,
    //      and it is knowable beforehand, so the run is refused with the
    //      reason rather than attempted and broken.
    //
    //   3. WRITE THE JOINTS DOWN. Every joint these pipes have is
    //      recorded before anything moves and checked afterwards.
    //      Whatever came apart is joined back up - directly where the
    //      sizes now match, with a reducer where they do not.
    //
    //   4. THEN THE FITTINGS, and only then: a fitting is asked to
    //      match pipes that have already stopped moving. The joints are
    //      mended after that, because a fitting made the right size is
    //      a joint that can be remade properly rather than packed with
    //      a reducer - and anything still not agreeing gets one more go
    //      afterwards, since mending a joint can hand a fitting back
    //      the pipe it needs to take its size from.
    //
    // What none of it changes: resizing a pipe changes the hydraulics of
    // everything downstream. This puts the drawing in step with the
    // table. It does not redesign the system.
    public class QACHWFIX
    {
        public const string Resize = "chw-resize";

        public class Result
        {
            public int Runs { get; set; }
            public int Pipes { get; set; }

            public int Skipped { get; set; }
            public int Already { get; set; }

            // Joints that came apart and were put back
            public int Repaired { get; set; }

            // Joints that came apart and could not be
            public int Broken { get; set; }

            // Fittings made to agree with their pipes: the ones that
            // took the size, the ones that had to be replaced to get
            // it, and the ones that would do neither
            public int FittingsResized { get; set; }
            public int FittingsReplaced { get; set; }
            public int FittingsStubborn { get; set; }

            // Short pipes that were stretched to leave room for the
            // bigger fittings, rather than being deleted by Revit
            public int Stretched { get; set; }

            // What the final sweep found: pipes that are gone, and
            // pipes left too short for Revit to keep. Both should be
            // zero, and both are worth shouting about if they are not.
            public int Vanished { get; set; }
            public int Squashed { get; set; }

            public int Fittings
            {
                get { return FittingsResized + FittingsReplaced; }
            }

            public List<string> Notes { get; set; } = new List<string>();

            public void Note(string text)
            {
                if (!Notes.Contains(text))
                    Notes.Add(text);
            }
        }

        // Must be called inside a transaction
        public static Result Fix(
            Document doc,
            IEnumerable<QAFinding> findings,
            QASETTINGS settings,
            QASCOPE scope)
        {
            var result = new Result();

            QACHWLOG.Start(
                "Sliding fittings to keep short pipes: " +
                (settings.ChwKeepShortPipes ? "ON" : "OFF"));

            if (settings.ChwTable == null
                || settings.ChwTable.Sizes.Count == 0)
            {
                result.Note(
                    "No sizing table is loaded, so there is nothing to " +
                    "resize towards.");

                QACHWLOG.Save();

                return result;
            }

            // Which pipes the list is pointing at, and which fittings.
            // A fitting carries no FixKind because it has no size of
            // its own to be told - it takes the size of its pipes - but
            // it is still in the list and still has to end up right.
            var asked = new HashSet<long>();
            var fittings = new HashSet<long>();

            foreach (QAFinding finding in findings)
            {
                if (finding.FixKind == Resize)
                {
                    asked.Add(finding.ElementId);

                    continue;
                }

                Element listed = doc.GetElement(
                    REVITCOMPAT.NewId(finding.ElementId));

                if (listed != null
                    && (QACHWRUNS.IsFitting(listed)
                        || QACHWRUNS.IsAccessory(listed)))
                    fittings.Add(finding.ElementId);
                else
                    result.Skipped++;
            }

            if (asked.Count == 0 && fittings.Count == 0)
            {
                QACHWLOG.Say("nothing in the list to act on");

                QACHWLOG.Save();

                return result;
            }

            // 1. What has to move together
            List<QACHWRUNS.Run> runs =
                QACHWRUNS.Group(doc, asked, settings);

            QACHWLOG.Say(
                asked.Count + " pipe(s) asked for, " + fittings.Count +
                " fitting(s) listed, grouped into " + runs.Count + " run(s)");

            var jobs = new List<Job>();

            foreach (QACHWRUNS.Run run in runs)
                Consider(doc, run, settings, jobs, result);

            // Nothing to resize does not mean nothing to do: the pipes
            // may be right and a fitting between them wrong.
            if (jobs.Count == 0)
            {
                QACHWLOG.Head("no run to resize - fittings only");

                Follow(doc, fittings, result);

                Sweep(doc, jobs, result);

                QACHWLOG.Save();

                return result;
            }

            // 3. What is joined to what, before anything moves
            var pipes = new List<long>();

            foreach (Job job in jobs)
                pipes.AddRange(job.Run.PipeIds);

            List<QACHWRUNS.Joint> joints =
                QACHWRUNS.JointsOf(doc, pipes);

            // Biggest first: a main grows before its branches, so the
            // fittings between them have less to argue about
            jobs.Sort(delegate (Job a, Job b)
            {
                return b.To.DN.CompareTo(a.To.DN);
            });

            foreach (Job job in jobs)
                Apply(doc, job, settings, result);

            bool moved = result.Pipes > 0;

            if (moved)
            {
                try
                {
                    doc.Regenerate();
                }
                catch
                {
                    // the commit regenerates anyway
                }

                // The fittings do NOT follow on their own - see
                // QACHWFITTINGS. Everything joined to a pipe that moved
                // is asked to match it, whether or not it was listed:
                // until the pipe moved it may have been right.
                foreach (long id in QACHWFITTINGS.Touching(doc, pipes))
                    fittings.Add(id);
            }

            // Outside that guard on purpose. Every run can be refused -
            // no size in the routing preferences, say - and the listed
            // fittings still have to be put right; they are a finding in
            // their own right, not a consequence of the pipes moving.
            List<long> gone = Follow(doc, fittings, result);

            if (moved)
            {
                Mend(doc, joints, settings, result, gone);

                // Mending a joint can hand a fitting back the pipe it
                // takes its size from, so anything still not agreeing
                // is worth one more go - but only if a joint actually
                // was mended, or nothing has changed since.
                if (result.FittingsStubborn > 0 && result.Repaired > 0)
                    Follow(doc, fittings, result);
            }

            // The last word, and it has to be AFTER the fittings: they
            // are what actually grow, and they grow once every run has
            // committed.
            Sweep(doc, jobs, result);

            QACHWLOG.Save();

            return result;
        }

        // Did anything die? Every pipe any of this touched or weighed,
        // checked against the model at the very end.
        private static void Sweep(
            Document doc, List<Job> jobs, Result result)
        {
            QACHWLOG.Head("final sweep");

            var seen = new HashSet<long>();

            foreach (Job job in jobs)
            {
                var watch = new List<long>(job.Run.PipeIds);

                if (job.Room != null)
                    watch.AddRange(job.Room.Watched);

                foreach (long id in watch)
                {
                    if (!seen.Add(id))
                        continue;

                    if (doc.GetElement(REVITCOMPAT.NewId(id)) == null)
                    {
                        result.Vanished++;

                        QACHWLOG.Say("   GONE: pipe " + id);

                        continue;
                    }

                    if (QACHWROOM.TooShort(doc, id))
                    {
                        result.Squashed++;

                        QACHWLOG.Say("   TOO SHORT: pipe " + id);
                    }
                }
            }

            QACHWLOG.Say(
                "   " + seen.Count + " pipe(s) checked, " +
                result.Vanished + " gone, " + result.Squashed +
                " too short");

            if (result.Vanished == 0 && result.Squashed == 0)
                return;

            result.Note(
                "AFTER ALL THAT, " + result.Vanished + " pipe(s) are gone " +
                "and " + result.Squashed + " are too short for Revit to " +
                "keep. That should not happen and it is worth telling " +
                "whoever maintains this add-in: " + QACHWLOG.Where + " " +
                "says which. Ctrl+Z puts the whole lot back.");
        }

        // Making the fittings agree with their pipes. Gives back the
        // ones that no longer exist, because a replaced fitting's old
        // joints are not broken joints - they went with it.
        private static List<long> Follow(
            Document doc, HashSet<long> fittings, Result result)
        {
            var outcome = new QACHWFITTINGS.Result();

            if (fittings.Count > 0)
            {
                QACHWFITTINGS.Reconcile(doc, fittings, outcome);

                try
                {
                    doc.Regenerate();
                }
                catch
                {
                    // the commit regenerates anyway
                }
            }

            QACHWLOG.Head(
                "fittings: " + fittings.Count + " asked, " +
                outcome.Followed + " already right, " + outcome.Resized +
                " took the size, " + outcome.Replaced + " replaced, " +
                outcome.Stubborn + " would not");

            result.FittingsResized += outcome.Resized;
            result.FittingsReplaced += outcome.Replaced;
            result.Stretched += outcome.Stretched;

            // Set rather than added: each pass looks at the same
            // fittings, so the last pass is the true count of the ones
            // still not agreeing with their pipes.
            result.FittingsStubborn = outcome.Stubborn;

            foreach (string note in outcome.Notes)
                result.Note(note);

            return outcome.Gone;
        }

        private class Job
        {
            public QACHWRUNS.Run Run;
            public CHWSIZESHEET.Size To;

            // Pipes that will have nothing left of them once their
            // fittings grow, worked out before anything is written
            public QACHWROOM.Verdict Room;
        }

        // ==================================================
        // 2. Deciding, and asking the type first
        // ==================================================

        private static void Consider(
            Document doc,
            QACHWRUNS.Run run,
            QASETTINGS settings,
            List<Job> jobs,
            Result result)
        {
            if (run.PipeIds.Count == 0)
                return;

            CHWSIZESHEET.Size needed =
                settings.ChwTable.SmallestFor(run.Litres);

            if (needed == null)
            {
                result.Skipped++;

                QACHWLOG.Head("run REFUSED: past the largest size in the table");

                result.Note(
                    "Some runs carry more than the largest size in the " +
                    "table, so there is no size to move them to.");

                return;
            }

            // Already right, and all of one size
            if (!run.Mixed
                && Math.Abs(run.DnMm - needed.DN) <= settings.ChwSizeToleranceMm)
            {
                QACHWLOG.Head(
                    "run already DN" + run.DnMm.ToString("N0") +
                    ", nothing to do  (" + Ids(run.PipeIds) + ")");

                result.Already++;

                return;
            }

            Element first = doc.GetElement(
                REVITCOMPAT.NewId(run.PipeIds[0]));

            if (first == null)
            {
                result.Skipped++;

                return;
            }

            List<double> offered = QACHWRUNS.SizesFor(doc, first);

            if (!QACHWRUNS.Offers(offered, needed.DN,
                    settings.ChwSizeToleranceMm))
            {
                result.Skipped++;

                QACHWLOG.Head(
                    "run REFUSED: DN" + needed.DN.ToString("N0") +
                    " is not in the pipe type's routing preferences");

                result.Note(
                    "DN" + needed.DN.ToString("N0") + " is not in the " +
                    "routing preferences of " + TypeNameOf(doc, first) +
                    ", so Revit has no segment for it and no fitting to " +
                    "join it with. Add the size to the pipe type, or " +
                    "resize those by hand.");

                return;
            }

            QACHWLOG.Head(
                "run of " + run.PipeIds.Count + " pipe(s), DN" +
                run.DnMm.ToString("N0") + (run.Mixed ? " (mixed)" : "") +
                ", " + run.Litres.ToString("N2") + " L/s  ->  DN" +
                needed.DN.ToString("N0"));

            QACHWLOG.Say("   pipes: " + Ids(run.PipeIds));

            QACHWROOM.Verdict room = QACHWROOM.Room(doc, run, needed.DN);

            QACHWLOG.Say(
                "   room: weighed " + room.Watched.Count + " pipe(s) on " +
                "the fittings round it, " + room.Shorts.Count +
                " would be left too short");

            foreach (QACHWROOM.Short shortfall in room.Shorts)
            {
                QACHWLOG.Say(
                    "      SHORT " + shortfall.PipeId + "  " +
                    shortfall.NowMm.ToString("N0") + " mm  ->  " +
                    shortfall.AfterMm.ToString("N0") + " mm  (needs " +
                    QAGRIDS.FromFeet(shortfall.NeedFeet).ToString("N0") +
                    " mm more)");
            }

            jobs.Add(new Job
            {
                Run = run,
                To = needed,

                // Asked while nothing has moved: a fitting that is about
                // to double in size eats into the pipe either side of
                // it, and a short jog between two elbows can be left
                // with no length at all
                Room = room
            });
        }

        private static string Ids(List<long> ids)
        {
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < ids.Count && i < 12; i++)
            {
                if (i > 0)
                    text.Append(", ");

                text.Append(ids[i]);
            }

            if (ids.Count > 12)
                text.Append(", ... (" + ids.Count + " in all)");

            return text.ToString();
        }

        private static string TypeNameOf(Document doc, Element pipe)
        {
            try
            {
                Element type = doc.GetElement(pipe.GetTypeId());

                return type == null
                    ? "that pipe type"
                    : "\"" + type.Name + "\"";
            }
            catch
            {
                return "that pipe type";
            }
        }

        // ==================================================
        // 1. The whole run, in one step
        // ==================================================

        private static void Apply(
            Document doc, Job job, QASETTINGS settings, Result result)
        {
            double internalUnits = UnitUtils.ConvertToInternalUnits(
                job.To.DN, UnitTypeId.Millimeters);

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    // Room first. It is far easier to stretch a jog
                    // while the fittings are still small than to put a
                    // pipe back after Revit has decided it is too short
                    // to exist and deleted it.
                    if (!MakeRoom(doc, job, settings, result))
                    {
                        sub.RollBack();

                        result.Skipped++;

                        return;
                    }

                    int written = 0;

                    foreach (long id in job.Run.PipeIds)
                    {
                        Element pipe = doc.GetElement(
                            REVITCOMPAT.NewId(id));

                        if (pipe == null)
                            continue;

                        Parameter size = pipe.get_Parameter(
                            BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);

                        if (size == null || size.IsReadOnly)
                        {
                            result.Note(
                                "Some pipes have a read-only diameter. " +
                                "That is usually a workshared model " +
                                "where somebody else owns the element, " +
                                "or a pipe inside a group.");

                            continue;
                        }

                        if (Math.Abs(
                                UnitUtils.ConvertFromInternalUnits(
                                    size.AsDouble(), UnitTypeId.Millimeters)
                                - job.To.DN)
                            <= settings.ChwSizeToleranceMm)
                            continue;

                        size.Set(internalUnits);

                        written++;
                    }

                    if (written == 0)
                    {
                        QACHWLOG.Say("   nothing written, run rolled back");

                        sub.RollBack();

                        result.Skipped++;

                        return;
                    }

                    // Here rather than at commit, so a refusal surfaces
                    // while this one run can still be rolled back alone
                    doc.Regenerate();

                    // And did they all survive it? The estimate of how
                    // much room the new fittings need is an estimate;
                    // this is the model's own answer, and a run that
                    // lost a pipe is not worth having at any size.
                    string lost = Lost(doc, job);

                    if (lost != null)
                    {
                        QACHWLOG.Say("   ROLLED BACK after writing: " + lost);

                        sub.RollBack();

                        result.Skipped++;

                        result.Note(lost);

                        return;
                    }

                    sub.Commit();

                    QACHWLOG.Say(
                        "   wrote " + written + " pipe(s) to DN" +
                        job.To.DN.ToString("N0"));

                    result.Runs++;
                    result.Pipes += written;
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Skipped++;

                    result.Note(
                        "A run would not take the new size: " + Short(ex) +
                        ". Nothing was lost - each run is undone on its " +
                        "own when it fails.");
                }
            }
        }

        // ==================================================
        // Room for the bigger fittings
        // ==================================================

        // True when the run can go ahead: either every pipe already has
        // the room, or the room was made for it.
        private static bool MakeRoom(
            Document doc, Job job, QASETTINGS settings, Result result)
        {
            if (job.Room == null || job.Room.Enough)
                return true;

            var room = new QACHWROOM.Result();

            bool every = true;

            foreach (QACHWROOM.Short shortfall in job.Room.Shorts)
            {
                // Sliding a fitting moves pipework, so it is the user's
                // call. Switched off, the run is simply refused - which
                // still means nothing is deleted.
                if (settings.ChwKeepShortPipes
                    && QACHWROOM.Make(doc, shortfall, room))
                {
                    QACHWLOG.Say(
                        "   made room for pipe " + shortfall.PipeId +
                        " by sliding a fitting");

                    continue;
                }

                QACHWLOG.Say(
                    "   COULD NOT make room for pipe " + shortfall.PipeId +
                    (settings.ChwKeepShortPipes
                        ? " - run refused"
                        : " - sliding is switched off, run refused"));

                every = false;

                result.Note(
                    shortfall.Name + " is " +
                    shortfall.NowMm.ToString("N0") +
                    " mm long, and fittings for DN" +
                    job.To.DN.ToString("N0") + " would leave it " +
                    (shortfall.AfterMm <= 0
                        ? "with nothing at all"
                        : shortfall.AfterMm.ToString("N0") + " mm") +
                    ". Revit deletes a pipe that short - that is the " +
                    "\"Line is too short\" message - so the whole run was " +
                    "left alone." +
                    (settings.ChwKeepShortPipes
                        ? "  The room could not be made here either, so " +
                          "it needs a route change rather than a resize."
                        : "  Tick \"Slide fittings along to keep pipes " +
                          "Revit would delete\" to have it try to make " +
                          "the room."));
            }

            result.Stretched += room.Stretched;

            foreach (string note in room.Notes)
                result.Note(note);

            return every;
        }

        // The model's own verdict, after the fact
        private static string Lost(Document doc, Job job)
        {
            var watch = new List<long>(job.Run.PipeIds);

            // Not only the pipes that moved. A fitting grows on the
            // biggest thing joined to it, so it eats into the pipe on
            // its far side as well - and where two runs share a tee,
            // neither one on its own predicts what the pair of them
            // will do to the pipe between them.
            if (job.Room != null)
                watch.AddRange(job.Room.Watched);

            foreach (long id in watch)
            {
                if (doc.GetElement(REVITCOMPAT.NewId(id)) == null)
                {
                    return "A pipe did not survive the new size: Revit " +
                           "deletes one left too short by its own " +
                           "fittings. That run was put back exactly as " +
                           "it was, so nothing is missing.";
                }

                if (QACHWROOM.TooShort(doc, id))
                {
                    return "A pipe would have been left too short to " +
                           "keep once its fittings grew. That run was " +
                           "put back exactly as it was.";
                }
            }

            return null;
        }

        // ==================================================
        // 3. Putting back what came apart
        // ==================================================

        private static void Mend(
            Document doc,
            List<QACHWRUNS.Joint> joints,
            QASETTINGS settings,
            Result result,
            List<long> gone)
        {
            var replaced = new HashSet<long>(gone);

            foreach (QACHWRUNS.Joint joint in joints)
            {
                // A joint to a fitting that was replaced is not a
                // broken joint. The fitting went, and the new one made
                // its own connections on the way in.
                if (replaced.Contains(joint.OneId)
                    || replaced.Contains(joint.TwoId))
                    continue;

                if (QACHWRUNS.StillJoined(doc, joint))
                    continue;

                if (Rejoin(doc, joint, result))
                    result.Repaired++;
                else
                    result.Broken++;
            }

            if (result.Repaired > 0)
            {
                result.Note(
                    result.Repaired + " joint(s) came apart when the " +
                    "sizes changed and were put back - with a reducer " +
                    "where the two ends are now different sizes.");
            }

            if (result.Broken > 0)
            {
                result.Note(
                    result.Broken + " joint(s) came apart and could not " +
                    "be put back. They are open connectors now, and the " +
                    "list below will not show them - run the duct page's " +
                    "kind of check by eye, or Ctrl+Z and resize those by " +
                    "hand.");
            }
        }

        private static bool Rejoin(
            Document doc, QACHWRUNS.Joint joint, Result result)
        {
            Connector one = QADUCTS.Lookup(
                doc, joint.OneId, joint.OneConnector);

            Connector two = QADUCTS.Lookup(
                doc, joint.TwoId, joint.TwoConnector);

            if (one == null || two == null)
                return false;

            try
            {
                // Something else may have taken the place - Revit often
                // inserts its own reducer, which is the joint being made
                // properly rather than broken
                if (one.IsConnected || two.IsConnected)
                    return false;
            }
            catch
            {
                return false;
            }

            double apart;
            double radiusOne;
            double radiusTwo;

            try
            {
                apart = one.Origin.DistanceTo(two.Origin);

                radiusOne = one.Radius;
                radiusTwo = two.Radius;
            }
            catch
            {
                return false;
            }

            // A resize does not move a pipe's centreline, so the two
            // ends should still be in the same place. If they are not,
            // something bigger happened and guessing would be worse than
            // saying so.
            if (apart > QAGRIDS.ToFeet(2))
            {
                result.Note(
                    "Some joints came apart AND moved, so they were left " +
                    "alone rather than forced back together.");

                return false;
            }

            bool sameSize = Math.Abs(radiusOne - radiusTwo) < 1e-9;

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    if (sameSize)
                        one.ConnectTo(two);
                    else
                        doc.Create.NewTransitionFitting(one, two);

                    sub.Commit();

                    return true;
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Note(
                        sameSize
                            ? "Some ends would not rejoin: " + Short(ex)
                            : "Some ends needed a reducer and would not " +
                              "take one: " + Short(ex) + ". The pipe " +
                              "type may have no transition family in its " +
                              "routing preferences.");

                    return false;
                }
            }
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
