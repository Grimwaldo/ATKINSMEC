using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ATKINSMEC
{
    // Writing an NWC out of one model after another.
    //
    // The same shape as CHW VOLUME's measuring run, and for the same
    // reason: a model is opened as a BACKGROUND document, worked on, and
    // closed again, so a batch of twenty never appears in the Revit
    // window. The command's form is modal, so all of this runs inside a
    // valid API context and needs no external event.
    //
    // THE EXPORTER IS NOT PART OF REVIT. It ships with Navisworks (or
    // the free standalone NWC exporter) and is optional, so the first
    // thing this does is ask whether it is there at all -
    // OptionalFunctionalityUtils.IsNavisworksExporterAvailable(). Asking
    // beforehand turns "an exception on every one of your twenty models"
    // into one sentence saying what to install.
    public class NWCEXPORT
    {
        // What happened to one model
        public class Outcome
        {
            public string Model = string.Empty;
            public string Kind = string.Empty;

            // Which 3D view it actually came from, and whether it used
            // the window's settings or its own - with per-model options
            // in the list, "it exported" is no longer the whole story
            public string View = string.Empty;
            public string Whose = string.Empty;

            // Which worksets it opened with
            public string Worksets = string.Empty;

            public bool Exported;

            public string File = string.Empty;
            public long Bytes;

            public double Seconds;

            // Why not, or what was odd about it
            public string Said = string.Empty;

            public string Size
            {
                get
                {
                    if (!Exported || Bytes <= 0)
                        return string.Empty;

                    double mb = Bytes / 1024.0 / 1024.0;

                    return mb < 1
                        ? (Bytes / 1024.0).ToString("N0") + " KB"
                        : mb.ToString("N1") + " MB";
                }
            }
        }

        public class Result
        {
            public List<Outcome> Outcomes = new List<Outcome>();

            public int Exported;
            public int Failed;
            public int Skipped;

            public bool Stopped;

            // Set when the exporter itself is missing, which is not a
            // per-model failure but a reason nothing can run at all
            public string Blocked = string.Empty;
        }

        // Is the Navisworks exporter installed in this Revit?
        public static bool Available()
        {
            try
            {
                return OptionalFunctionalityUtils
                    .IsNavisworksExporterAvailable();
            }
            catch
            {
                // If even the question fails, let the export try and
                // report whatever Revit says
                return true;
            }
        }

        // Must be called from an API context - the modal form has one.
        //
        // `open` is the document already open in Revit, if any: it is
        // exported where it appears in the list, and NEVER closed
        // afterwards, because closing the model somebody is working in
        // would be an unforgivable thing for a batch job to do.
        public static Result Run(
            Application app,
            Document open,
            List<NWCJOB> jobs,
            NWCSETTINGS settings,
            CHWSTOP stop)
        {
            var result = new Result();

            if (!Available())
            {
                result.Blocked =
                    "The Navisworks NWC exporter is not installed in " +
                    "this Revit. It is a free Autodesk add-in that comes " +
                    "with Navisworks, and there is a standalone " +
                    "installer for it - until it is there, Revit itself " +
                    "cannot write an NWC at all.";

                return result;
            }

            if (string.IsNullOrEmpty(settings.OutputFolder))
            {
                result.Blocked = "No folder has been chosen to write to.";

                return result;
            }

            try
            {
                if (!Directory.Exists(settings.OutputFolder))
                    Directory.CreateDirectory(settings.OutputFolder);
            }
            catch (Exception ex)
            {
                result.Blocked =
                    "That folder could not be made: " + Short(ex);

                return result;
            }

            int done = 0;

            foreach (NWCJOB job in jobs)
            {
                if (stop != null && stop.Stopped)
                {
                    result.Stopped = true;

                    break;
                }

                if (stop != null)
                {
                    stop.Went(done, jobs.Count, job.Name);

                    stop.Breathe();
                }

                NWCLOG.Head(job.Name);

                Outcome outcome = One(app, open, job, settings, stop);

                result.Outcomes.Add(outcome);

                NWCLOG.Say("   " + (outcome.Exported
                    ? "written to " + outcome.File
                    : "NOT written - " + outcome.Said));

                done++;
            }

            if (stop != null)
                stop.Went(done, jobs.Count, string.Empty);

            foreach (Outcome outcome in result.Outcomes)
            {
                if (outcome.Exported)
                    result.Exported++;
                else if (outcome.Said.StartsWith("Skipped"))
                    result.Skipped++;
                else
                    result.Failed++;
            }

            return result;
        }

        // ==================================================
        // One model
        // ==================================================

        // Which settings this model is written with, and from which
        // view. A model may carry its own whole set of settings, and -
        // separately, because it is the commoner need - its own view
        // name. The folder is NEVER taken from an override: everything
        // in one run goes to one place, or the window is lying about
        // where the files went.
        public static NWCSETTINGS For(NWCJOB job, NWCSETTINGS settings)
        {
            if (job == null || job.Overrides == null)
                return settings;

            NWCSETTINGS mine = job.Overrides.Copy();

            // The folder and the view are NOT the override's to give.
            // Every file in a run goes to the one folder the window
            // names, and the view comes from the model's own row or
            // from the window - so an override that carried an empty
            // view name would silently mean "the whole model".
            mine.OutputFolder = settings.OutputFolder;
            mine.ViewName = settings.ViewName;

            return mine;
        }

        public static string ViewFor(NWCJOB job, NWCSETTINGS effective)
        {
            if (job != null && !string.IsNullOrEmpty(job.ViewName))
            {
                // "(the whole model)" typed into a row means exactly
                // that, and has to beat the window's view name
                return job.ViewName == WholeModel
                    ? string.Empty
                    : job.ViewName;
            }

            return effective.ViewName;
        }

        // What a row says when it means "no view, the whole model"
        public const string WholeModel = "(the whole model)";

        // Turn what Revit said into what to do about it.
        //
        // These are the answers that are not about worksets at all, and
        // that no amount of retrying will get past - so they are named
        // rather than shown raw. A real model gave the first one: an
        // ACC model saved in a newer Revit simply cannot be opened by
        // an older one, and the window said "the central model is
        // missing", which sent the search in the wrong direction.
        public static string Explain(string trouble)
        {
            if (string.IsNullOrEmpty(trouble))
                return string.Empty;

            string said = trouble.ToLowerInvariant();

            if (said.Contains("not saved in current release"))
            {
                return trouble + Environment.NewLine + Environment.NewLine +
                       "THIS MODEL BELONGS TO A NEWER REVIT THAN THE ONE " +
                       "YOU ARE RUNNING. Nothing can read it here - open " +
                       "it, and run this export, from the Revit version " +
                       "the model was saved in.";
            }

            if (said.Contains("central model is missing"))
            {
                return trouble + Environment.NewLine + Environment.NewLine +
                       "Usually this means the model is not in the ACC " +
                       "region that was tried, or it has been moved or " +
                       "removed since it was added to the list.";
            }

            if (said.Contains("access") && said.Contains("denied"))
            {
                return trouble + Environment.NewLine + Environment.NewLine +
                       "Your Autodesk account can see the model in the " +
                       "list but is not allowed to open it. That is a " +
                       "permission on the ACC project, not something this " +
                       "add-in can get past.";
            }

            return trouble;
        }

        // Where a model's worksets live, whichever kind it is
        public static ModelPath PathOf(NWCJOB job)
        {
            return job.What == NWCJOB.Kind.Cloud
                ? CHWCLOUD.PathFor(job.ToCloudModel())
                : ModelPathUtils.ConvertUserVisiblePathToModelPath(
                    job.File);
        }

        // The worksets in one model, read WITHOUT opening it. Called
        // from the window, which is modal and therefore inside the
        // command's API context.
        //
        // This is the quick way and it does not always work. It asks a
        // CENTRAL, and whether an ACC path is answerable at all is not
        // stated anywhere in the API - so an empty list here is a real
        // possibility rather than a bug, and WorksetsByOpening is the
        // answer to it.
        public static List<WORKSETPICK> WorksetsOf(
            NWCJOB job, out string trouble)
        {
            trouble = string.Empty;

            if (job == null)
                return new List<WORKSETPICK>();

            try
            {
                // A cloud model has to find its region first - see
                // CHWCLOUD.WorksetsOfCloud
                if (job.What == NWCJOB.Kind.Cloud)
                    return Cloudy(job, false, null, out trouble);

                return CHWCLOUD.WorksetsOf(PathOf(job), out trouble);
            }
            catch (Exception ex)
            {
                trouble = Short(ex);

                return new List<WORKSETPICK>();
            }
        }

        // Both cloud reads, with the region that answered kept on the
        // job so the probe is paid for once
        private static List<WORKSETPICK> Cloudy(
            NWCJOB job, bool byOpening, Application app, out string trouble)
        {
            CHWCLOUD.CloudModel model = job.ToCloudModel();

            List<WORKSETPICK> found = byOpening
                ? CHWCLOUD.WorksetsByOpeningCloud(app, model, out trouble)
                : CHWCLOUD.WorksetsOfCloud(model, out trouble);

            if (!string.IsNullOrEmpty(model.Region))
                job.Region = model.Region;

            return found;
        }

        // The slow, certain way: open the model with every workset
        // closed, read the table, close it again. Only ever run because
        // somebody was asked and said yes.
        public static List<WORKSETPICK> WorksetsByOpening(
            Application app, NWCJOB job, out string trouble)
        {
            trouble = string.Empty;

            if (job == null || app == null)
                return new List<WORKSETPICK>();

            try
            {
                if (job.What == NWCJOB.Kind.Cloud)
                    return Cloudy(job, true, app, out trouble);

                return CHWCLOUD.WorksetsByOpening(
                    app, PathOf(job), out trouble);
            }
            catch (Exception ex)
            {
                trouble = Short(ex);

                return new List<WORKSETPICK>();
            }
        }

        private static Outcome One(
            Application app,
            Document open,
            NWCJOB job,
            NWCSETTINGS given,
            CHWSTOP stop)
        {
            NWCSETTINGS settings = For(job, given);

            string from = ViewFor(job, settings);

            var outcome = new Outcome
            {
                Model = job.Name,
                Kind = job.What == NWCJOB.Kind.Cloud ? "ACC" : "File",

                View = string.IsNullOrEmpty(from) ? WholeModel : from,

                Worksets = job.HasOwnWorksets
                    ? job.Worksets.Count + " picked"
                    : (settings.Worksets == 1 ? "mine" : "all"),

                Whose = job.HasOwnSettings ? "its own" : "window"
            };

            var clock = Stopwatch.StartNew();

            string name = NameFor(job, settings);

            if (settings.SkipUnchanged && Newer(job, settings, name))
            {
                outcome.Said =
                    "Skipped - the .nwc there is newer than the model";

                outcome.Seconds = clock.Elapsed.TotalSeconds;

                return outcome;
            }

            Document doc = null;

            // A document already open in Revit is used where it is, and
            // must not be closed
            bool ours = true;

            try
            {
                doc = Already(open, job);

                if (doc != null)
                {
                    ours = false;
                }
                else
                {
                    doc = job.What == NWCJOB.Kind.Cloud
                        ? CHWCLOUD.Open(app, Cloud(job, settings), stop)
                        : OpenLocal(app, job.File, settings, job);
                }

                if (doc == null)
                {
                    outcome.Said = "The model would not open.";

                    return outcome;
                }

                Write(doc, job, settings, name, outcome);
            }
            catch (Exception ex)
            {
                outcome.Exported = false;

                outcome.Said = Short(ex);
            }
            finally
            {
                if (doc != null && ours)
                {
                    try
                    {
                        doc.Close(false);
                    }
                    catch
                    {
                        // nothing useful to do about it
                    }
                }

                clock.Stop();

                outcome.Seconds = clock.Elapsed.TotalSeconds;
            }

            return outcome;
        }

        private static void Write(
            Document doc,
            NWCJOB job,
            NWCSETTINGS settings,
            string name,
            Outcome outcome)
        {
            NavisworksExportOptions options = OptionsFor(settings);

            string wanted = ViewFor(job, settings);

            // A named view has to BE there. Exporting the whole model
            // instead would be a quiet substitution of one deliverable
            // for another, so a model without that view is reported and
            // left alone.
            if (!string.IsNullOrEmpty(wanted))
            {
                View3D view = ViewNamed(doc, wanted);

                if (view == null)
                {
                    outcome.Said =
                        "No 3D view called \"" + wanted +
                        "\" in this model, so nothing was written.";

                    return;
                }

                options.ExportScope = NavisworksExportScope.View;
                options.ViewId = view.Id;
            }
            else
            {
                options.ExportScope = NavisworksExportScope.Model;
            }

            doc.Export(settings.OutputFolder, name, options);

            string written = Path.Combine(
                settings.OutputFolder, name + ".nwc");

            outcome.File = written;

            try
            {
                var file = new FileInfo(written);

                if (file.Exists)
                {
                    outcome.Exported = true;

                    outcome.Bytes = file.Length;
                }
                else
                {
                    // Revit raised nothing and wrote nothing: say so
                    // rather than report a success with no file
                    outcome.Said =
                        "Revit reported no error but no .nwc appeared.";
                }
            }
            catch (Exception ex)
            {
                outcome.Said = Short(ex);
            }
        }

        // ==================================================
        // The options
        // ==================================================

        public static NavisworksExportOptions OptionsFor(
            NWCSETTINGS settings)
        {
            var options = new NavisworksExportOptions
            {
                Coordinates = settings.SharedCoordinates
                    ? NavisworksCoordinates.Shared
                    : NavisworksCoordinates.Internal,

                ExportLinks = settings.ExportLinks,
                DivideFileIntoLevels = settings.DivideIntoLevels,
                ExportElementIds = settings.ExportElementIds,
                ExportRoomAsAttribute = settings.ExportRoomAsAttribute,
                ExportParts = settings.ExportParts,
                ConvertElementProperties = settings.ConvertElementProperties,

                Parameters = Which(settings.Parameters),

                // Revit takes 0 to 10; anything outside would be refused
                FacetingFactor = Math.Max(0.0,
                    Math.Min(10.0, settings.FacetingFactor))
            };

            return options;
        }

        private static NavisworksParameters Which(int chosen)
        {
            switch (chosen)
            {
                case 1: return NavisworksParameters.Elements;
                case 2: return NavisworksParameters.None;
                default: return NavisworksParameters.All;
            }
        }

        // ==================================================
        // Finding things
        // ==================================================

        public static View3D ViewNamed(Document doc, string name)
        {
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D));

            foreach (View3D view in collector)
            {
                if (view == null || view.IsTemplate)
                    continue;

                if (string.Equals(view.Name, name,
                        StringComparison.OrdinalIgnoreCase))
                    return view;
            }

            return null;
        }

        // Every 3D view in a model, for the window to offer
        public static List<string> ViewNames(Document doc)
        {
            var found = new List<string>();

            if (doc == null)
                return found;

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D));

            foreach (View3D view in collector)
            {
                if (view == null || view.IsTemplate)
                    continue;

                if (!found.Contains(view.Name))
                    found.Add(view.Name);
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);

            return found;
        }

        // The document already open in Revit, when this job IS that
        // document. Matched on the path, since a cloud model and its
        // local cache are the same thing by two names.
        // The worksets of a document that is ALREADY OPEN in Revit.
        //
        // This one matters: "Add the model open now" is a button, so
        // the open model is often in the list - and asking Revit to
        // open a document it already has open throws. It is also the
        // one case that needs no central and no waiting.
        public static List<WORKSETPICK> WorksetsOfOpen(
            Document open, NWCJOB job)
        {
            Document doc = Already(open, job);

            if (doc == null)
                return null;

            var found = new List<WORKSETPICK>();

            try
            {
                foreach (Workset workset in
                         new FilteredWorksetCollector(doc)
                             .OfKind(WorksetKind.UserWorkset))
                {
                    found.Add(new WORKSETPICK(
                        workset.Name, workset.UniqueId.ToString()));
                }
            }
            catch
            {
                // Not workshared: an empty list is the right answer
            }

            return found;
        }

        private static Document Already(Document open, NWCJOB job)
        {
            if (open == null)
                return null;

            try
            {
                if (job.What == NWCJOB.Kind.Local)
                {
                    if (!string.IsNullOrEmpty(open.PathName) &&
                        string.Equals(open.PathName, job.File,
                            StringComparison.OrdinalIgnoreCase))
                        return open;

                    return null;
                }

                if (!open.IsModelInCloud)
                    return null;

                return open.GetCloudModelPath() != null &&
                       open.Title.StartsWith(
                           Path.GetFileNameWithoutExtension(job.Name),
                           StringComparison.OrdinalIgnoreCase)
                    ? open
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static CHWCLOUD.CloudModel Cloud(
            NWCJOB job, NWCSETTINGS settings)
        {
            CHWCLOUD.CloudModel model = job.ToCloudModel();

            model.Worksets = settings.Worksets == 1
                ? CHWCLOUD.WorksetMode.Editable
                : CHWCLOUD.WorksetMode.All;

            // A named set beats the mode - see CHWCLOUD.WorksetsFor
            if (job.Worksets != null)
                model.WorksetPicks = job.Worksets;

            return model;
        }

        private static Document OpenLocal(
            Application app, string file, NWCSETTINGS settings, NWCJOB job)
        {
            ModelPath path =
                ModelPathUtils.ConvertUserVisiblePathToModelPath(file);

            // Detached, so a workshared model is never opened against
            // its central and nobody's worksets are taken
            var options = new OpenOptions
            {
                DetachFromCentralOption =
                    DetachFromCentralOption.DetachAndPreserveWorksets,
                Audit = false
            };

            try
            {
                // A model with worksets picked for it opens those and
                // nothing else; otherwise everything, as before
                options.SetOpenWorksetsConfiguration(
                    job != null && job.HasOwnWorksets
                        ? CHWCLOUD.NamedWorksets(path, job.Worksets)
                        : new WorksetConfiguration(
                            WorksetConfigurationOption.OpenAllWorksets));
            }
            catch
            {
                // Not workshared: the plain open below still works
            }

            try
            {
                return app.OpenDocumentFile(path, options);
            }
            catch
            {
                // A model that is not workshared refuses to be detached
                return app.OpenDocumentFile(
                    path,
                    new OpenOptions
                    {
                        DetachFromCentralOption =
                            DetachFromCentralOption.DoNotDetach,
                        Audit = false
                    });
            }
        }

        // ==================================================
        // Naming and skipping
        // ==================================================

        public static string NameFor(NWCJOB job, NWCSETTINGS settings)
        {
            string bare = job.What == NWCJOB.Kind.Local &&
                          !string.IsNullOrEmpty(job.File)
                ? Path.GetFileNameWithoutExtension(job.File)
                : job.Name;

            string name = (settings.Prefix ?? string.Empty) +
                          bare +
                          (settings.Suffix ?? string.Empty);

            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, '_');

            return name.Trim();
        }

        // Is the .nwc already there and newer than the model? Only a
        // local model can be judged this way - a cloud model's date is
        // not readable without opening it, which is the very thing this
        // would be saving.
        private static bool Newer(
            NWCJOB job, NWCSETTINGS settings, string name)
        {
            if (job.What != NWCJOB.Kind.Local)
                return false;

            try
            {
                var model = new FileInfo(job.File);

                var nwc = new FileInfo(Path.Combine(
                    settings.OutputFolder, name + ".nwc"));

                return model.Exists && nwc.Exists &&
                       nwc.LastWriteTimeUtc > model.LastWriteTimeUtc;
            }
            catch
            {
                return false;
            }
        }

        private static string Short(Exception ex)
        {
            string message = (ex.Message ?? string.Empty).Trim();

            if (message.Length > 200)
                message = message.Substring(0, 197) + "...";

            return message.Length == 0 ? ex.GetType().Name : message;
        }
    }
}
