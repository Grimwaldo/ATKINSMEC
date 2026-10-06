using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // The Stop button, handed down to the long-running work. Kept as an
    // object rather than a delegate so the same instance can be read
    // from inside Revit's progress callback and set from the window.
    public class CHWSTOP
    {
        private volatile bool _stopped;

        public bool Stopped
        {
            get { return _stopped; }
        }

        public void Stop()
        {
            _stopped = true;
        }

        // Lets the window repaint and its Stop button be pressed. Called
        // only between models - never inside a Revit operation, where
        // pumping messages would be asking for trouble.
        public Action Pump { get; set; }

        public void Breathe()
        {
            if (Pump != null)
                Pump();
        }

        // How far along a batch is: how many are finished, how many
        // there are, and what is being worked on now. Set by the
        // window, called between models like Pump.
        public Action<int, int, string> Step { get; set; }

        public void Went(int done, int total, string what)
        {
            if (Step != null)
                Step(done, total, what);
        }
    }

    // Opens an ACC (cloud) model as a background document, so its volume
    // can be measured without the model ever being opened in the Revit
    // window. Revit itself must be running and signed in to an account
    // with access to the project.
    //
    // The Revit API has no ACC browser, so a model is identified by its
    // region plus two GUIDs. Those are captured from a model that is
    // open once, or pasted in, and then remembered.
    public class CHWCLOUD
    {
        // How much of a workshared model to load when it is opened in
        // the background. All is the only setting that measures the whole
        // model; Editable loads just the worksets this user owns, which
        // is quicker but counts only part of the pipework.
        public enum WorksetMode
        {
            All = 0,
            Editable = 1
        }

        public class CloudModel
        {
            private List<string> _systemTypes = new List<string>();

            public string Name { get; set; }
            public string Region { get; set; }
            public Guid ProjectGuid { get; set; }
            public Guid ModelGuid { get; set; }

            public WorksetMode Worksets { get; set; }

            // Named worksets to open, which BEATS the mode above when
            // it has anything in it. CHW VOLUME never fills this in -
            // it is NWC BATCH asking for a particular set per model -
            // so an empty list leaves the old two-mode behaviour
            // exactly as it was.
            public List<WORKSETPICK> WorksetPicks { get; set; }
                = new List<WORKSETPICK>();

            // The two piping system types chosen for this model, kept so
            // the choice survives between sessions
            public string SupplyType { get; set; }
            public string ReturnType { get; set; }

            // Piping system type names seen the last time this model was
            // opened. Nothing can read them out of a closed model, so they
            // are remembered from one open and reused from then on.
            public List<string> SystemTypes
            {
                get { return _systemTypes; }
                set { _systemTypes = value ?? new List<string>(); }
            }

            public override string ToString()
            {
                return Name + "   (ACC)";
            }
        }

        // Revit forbids this character in a type name, so it is safe as
        // a separator inside the saved list's last column
        public const char TypeSeparator = '|';

        public static string SettingsPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC");

                return Path.Combine(folder, "acc_models.txt");
            }
        }

        // ==================================================
        // The saved list
        // ==================================================

        public static List<CloudModel> Load()
        {
            var models = new List<CloudModel>();

            try
            {
                if (!File.Exists(SettingsPath))
                    return models;

                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    string text = line.Trim();

                    if (text.Length == 0 || text.StartsWith("#"))
                        continue;

                    string[] parts = text.Split('\t');

                    if (parts.Length < 4)
                        continue;

                    Guid project;
                    Guid model;

                    if (!Guid.TryParse(parts[2].Trim(), out project) ||
                        !Guid.TryParse(parts[3].Trim(), out model))
                        continue;

                    var entry = new CloudModel
                    {
                        Name = parts[0].Trim(),
                        Region = parts[1].Trim(),
                        ProjectGuid = project,
                        ModelGuid = model
                    };

                    // Older files are shorter; anything missing simply
                    // takes its default and is relearnt or re-chosen
                    if (parts.Length >= 5)
                    {
                        foreach (string name in
                                 parts[4].Split(TypeSeparator))
                        {
                            if (name.Trim().Length > 0)
                                entry.SystemTypes.Add(name.Trim());
                        }
                    }

                    if (parts.Length >= 6 &&
                        parts[5].Trim() == WorksetMode.Editable.ToString())
                        entry.Worksets = WorksetMode.Editable;

                    if (parts.Length >= 8)
                    {
                        entry.SupplyType = parts[6].Trim();
                        entry.ReturnType = parts[7].Trim();
                    }

                    models.Add(entry);
                }
            }
            catch
            {
                // a damaged list must not stop the add-in opening
            }

            return models;
        }

        public static void Save(List<CloudModel> models)
        {
            string folder = Path.GetDirectoryName(SettingsPath);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var text = new StringBuilder();

            text.AppendLine("# ATKINSMEC ACC models");
            text.AppendLine(
                "# name<TAB>region<TAB>project GUID<TAB>model GUID" +
                "<TAB>piping system types separated by " + TypeSeparator +
                "<TAB>worksets<TAB>supply type<TAB>return type");

            foreach (CloudModel model in models)
            {
                var types = new List<string>();

                foreach (string type in model.SystemTypes)
                {
                    // Revit does not allow the separator in a type name,
                    // but a damaged list must not be able to shift columns
                    types.Add(type
                        .Replace(TypeSeparator, ' ')
                        .Replace('\t', ' '));
                }

                text.AppendLine(
                    model.Name + "\t" +
                    model.Region + "\t" +
                    model.ProjectGuid + "\t" +
                    model.ModelGuid + "\t" +
                    string.Join(TypeSeparator.ToString(), types.ToArray()) +
                    "\t" + model.Worksets +
                    "\t" + Clean(model.SupplyType) +
                    "\t" + Clean(model.ReturnType));
            }

            File.WriteAllText(SettingsPath, text.ToString());
        }

        // Keeps a stray tab from shifting every later column
        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Replace('\t', ' ');
        }

        // ==================================================
        // Identifying a model
        // ==================================================

        public static string[] Regions
        {
            get
            {
                return new[]
                {
                    ModelPathUtils.CloudRegionUS,
                    ModelPathUtils.CloudRegionEMEA
                };
            }
        }

        // Reads the region and GUIDs back off a cloud model that is open
        public static CloudModel FromDocument(Document doc, out string error)
        {
            error = string.Empty;

            if (doc == null)
            {
                error = "No document.";
                return null;
            }

            if (!doc.IsModelInCloud)
            {
                error = "This model is not saved in ACC, so it has no " +
                        "project and model GUID to read.";
                return null;
            }

            try
            {
                ModelPath path = doc.GetCloudModelPath();

                return new CloudModel
                {
                    Name = doc.Title,

                    // The region is not exposed on the path, so it is
                    // asked for rather than guessed
                    Region = string.Empty,
                    ProjectGuid = path.GetProjectGUID(),
                    ModelGuid = path.GetModelGUID()
                };
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        // ==================================================
        // Models opened before on this machine
        // ==================================================

        // Every cloud model Revit has opened here leaves a local cache
        // at CollaborationCache\<account>\<project guid>\<model guid>.rvt,
        // so both GUIDs are in the path itself and the readable name is
        // in the cached file's header. That is the whole picker: no
        // GUIDs to type and nothing asked of ACC.
        //
        // The layout is not documented, so every step is defensive and
        // the manual dialog stays available.
        public class CachedModel
        {
            public string ModelName { get; set; }
            public string ProjectName { get; set; }
            public Guid ProjectGuid { get; set; }
            public Guid ModelGuid { get; set; }
            public string RevitVersion { get; set; }

            public override string ToString()
            {
                string name = string.IsNullOrEmpty(ModelName)
                    ? ModelGuid.ToString()
                    : ModelName;

                return string.IsNullOrEmpty(ProjectName)
                    ? name
                    : name + "   -   " + ProjectName;
            }

            public CloudModel ToCloudModel()
            {
                return new CloudModel
                {
                    Name = ModelName,
                    Region = string.Empty,
                    ProjectGuid = ProjectGuid,
                    ModelGuid = ModelGuid
                };
            }
        }

        public static List<CachedModel> ScanCache()
        {
            var found = new Dictionary<string, CachedModel>();

            try
            {
                string root = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Autodesk",
                    "Revit");

                if (!Directory.Exists(root))
                    return new List<CachedModel>();

                foreach (string versionFolder in Directory.GetDirectories(root))
                {
                    string cache =
                        Path.Combine(versionFolder, "CollaborationCache");

                    if (!Directory.Exists(cache))
                        continue;

                    string version = VersionFrom(versionFolder);

                    foreach (string account in Directory.GetDirectories(cache))
                        ScanAccount(account, version, found);
                }
            }
            catch
            {
                // an unreadable cache just means an empty list
            }

            var models = new List<CachedModel>(found.Values);

            models.Sort((a, b) =>
            {
                int byProject = string.Compare(
                    a.ProjectName, b.ProjectName,
                    StringComparison.OrdinalIgnoreCase);

                return byProject != 0
                    ? byProject
                    : string.Compare(
                        a.ModelName, b.ModelName,
                        StringComparison.OrdinalIgnoreCase);
            });

            return models;
        }

        private static void ScanAccount(
            string accountFolder,
            string version,
            Dictionary<string, CachedModel> found)
        {
            foreach (string projectFolder in
                     Directory.GetDirectories(accountFolder))
            {
                Guid projectGuid;

                if (!Guid.TryParse(
                        Path.GetFileName(projectFolder), out projectGuid))
                    continue;

                // Only the cache files sitting directly in the project
                // folder; CentralCache below it repeats the same models
                foreach (string file in
                         Directory.GetFiles(projectFolder, "*.rvt"))
                {
                    Guid modelGuid;

                    if (!Guid.TryParse(
                            Path.GetFileNameWithoutExtension(file),
                            out modelGuid))
                        continue;

                    string key = projectGuid + "/" + modelGuid;

                    CachedModel existing;

                    // The same model cached under two Revit versions is
                    // one model; the newest cache wins
                    if (found.TryGetValue(key, out existing) &&
                        string.CompareOrdinal(
                            existing.RevitVersion, version) >= 0)
                        continue;

                    string projectName;
                    string modelName;

                    ReadNames(file, out projectName, out modelName);

                    found[key] = new CachedModel
                    {
                        ModelName = modelName,
                        ProjectName = projectName,
                        ProjectGuid = projectGuid,
                        ModelGuid = modelGuid,
                        RevitVersion = version
                    };
                }
            }
        }

        // The cached file's header carries the ACC path it came from,
        // in the form "Autodesk Docs://<project>/<folders>/<model>.rvt"
        private static void ReadNames(
            string file,
            out string projectName,
            out string modelName)
        {
            projectName = string.Empty;
            modelName = string.Empty;

            string central = null;

            try
            {
                using (BasicFileInfo info = BasicFileInfo.Extract(file))
                {
                    if (info != null)
                        central = info.CentralPath;
                }
            }
            catch
            {
                // a cache being written, or locked by a running Revit
            }

            if (string.IsNullOrEmpty(central))
                return;

            int scheme = central.IndexOf("://", StringComparison.Ordinal);

            string tail = scheme < 0
                ? central
                : central.Substring(scheme + 3);

            string[] parts = tail.Split('/');

            if (parts.Length > 0)
            {
                modelName = parts[parts.Length - 1];

                if (modelName.EndsWith(
                        ".rvt", StringComparison.OrdinalIgnoreCase))
                {
                    modelName =
                        modelName.Substring(0, modelName.Length - 4);
                }
            }

            if (parts.Length > 1)
                projectName = parts[0];
        }

        private static string VersionFrom(string folder)
        {
            // "Autodesk Revit 2025" -> "2025", anything else -> as is
            string name = Path.GetFileName(folder);

            string[] words = name.Split(' ');

            return words.Length == 0 ? name : words[words.Length - 1];
        }

        public static ModelPath PathFor(CloudModel model)
        {
            return ModelPathUtils.ConvertCloudGUIDsToCloudPath(
                model.Region,
                model.ProjectGuid,
                model.ModelGuid);
        }

        // ==================================================
        // Opening in the background
        // ==================================================

        // Opens without showing the model. Detached, so Revit does not
        // try to make this a local of the central; the document is only
        // read and then closed without saving.
        public static Document Open(Application app, CloudModel model)
        {
            return Open(app, model, null);
        }

        // The stop signal is polled through Revit's own progress
        // reporting, so a long download can be interrupted. Revit decides
        // whether a given stage may be cancelled at all - when it says
        // no, the open runs to completion and the caller's between-models
        // check is what actually ends the run.
        public static Document Open(
            Application app,
            CloudModel model,
            CHWSTOP stop)
        {
            EventHandler<ProgressChangedEventArgs> onProgress = null;

            if (stop != null)
            {
                onProgress = (sender, e) =>
                {
                    try
                    {
                        if (e.Cancellable && stop.Stopped)
                            e.Cancel();
                    }
                    catch
                    {
                        // never let the progress handler break the open
                    }
                };

                app.ProgressChanged += onProgress;
            }

            try
            {
                return OpenWithRegions(app, model, stop);
            }
            finally
            {
                if (onProgress != null)
                    app.ProgressChanged -= onProgress;
            }
        }

        private static Document OpenWithRegions(
            Application app,
            CloudModel model,
            CHWSTOP stop)
        {
            return OpenWithRegions(app, model, stop, null);
        }

        // forced is the workset configuration to open with, when the
        // caller wants one of its own rather than the model's - reading
        // the workset list wants everything CLOSED
        private static Document OpenWithRegions(
            Application app,
            CloudModel model,
            CHWSTOP stop,
            WorksetConfiguration forced)
        {
            Exception firstFailure = null;

            // The region is part of the cloud path but is not reported by
            // anything readable, so a model whose region is unknown is
            // simply tried in each one. Whichever opens is remembered, so
            // this costs a failed attempt once and never again.
            foreach (string region in RegionsToTry(model))
            {
                ModelPath path = ModelPathUtils.ConvertCloudGUIDsToCloudPath(
                    region,
                    model.ProjectGuid,
                    model.ModelGuid);

                try
                {
                    Document doc = forced == null
                        ? OpenPath(
                            app, path, model.Worksets, model.WorksetPicks)
                        : OpenPathWith(app, path, forced);

                    model.Region = region;

                    return doc;
                }
                catch (Exception ex)
                {
                    NWCLOG.Blew("opening in " + region, ex);

                    firstFailure = Blame(firstFailure, ex);

                    // A cancelled open must not be retried in the other
                    // region: the user asked for it to stop, and trying
                    // again would start a second download
                    if (stop != null && stop.Stopped)
                        throw new OperationCanceledException(
                            "Stopped before " + model.Name +
                            " finished opening.");
                }
            }

            throw firstFailure ?? new InvalidOperationException(
                "The model could not be opened from ACC.");
        }

        // WHICH failure to report when every region fails.
        //
        // Not the first one. Probing regions means the WRONG region
        // always throws CentralModelMissingException - "the central
        // model is missing" - which is noise: of course it is missing,
        // the model is not in that region. Reporting it hides the real
        // answer from the region that does exist.
        //
        // A real model showed this exactly: US said "The cloud model is
        // not saved in current release of Revit", EMEA said "the central
        // model is missing", and the window showed the second one - so
        // the message on screen said nothing about the actual problem,
        // which was that the model belongs to a newer Revit.
        private static Exception Blame(Exception best, Exception fresh)
        {
            if (best == null)
                return fresh;

            // Anything at all beats "not in this region"
            if (Missing(best) && !Missing(fresh))
                return fresh;

            return best;
        }

        // By NAME, because CentralModelMissingException is not public -
        // the compiler refuses to name the type even though Revit
        // throws it. The name is stable across every version here.
        private static bool Missing(Exception ex)
        {
            return ex != null &&
                   ex.GetType().Name == "CentralModelMissingException";
        }

        private static List<string> RegionsToTry(CloudModel model)
        {
            var order = new List<string>();

            // A known region first: it is the one that worked before
            if (!string.IsNullOrEmpty(model.Region))
                order.Add(model.Region);

            foreach (string region in Regions)
            {
                if (!order.Contains(region))
                    order.Add(region);
            }

            return order;
        }

        private static Document OpenPath(
            Application app,
            ModelPath path,
            WorksetMode worksets,
            List<WORKSETPICK> named)
        {
            try
            {
                return app.OpenDocumentFile(
                    path, DetachedOptions(app, path, worksets, named));
            }
            catch
            {
                // A model that is not workshared refuses to be detached,
                // and has no worksets to choose from either
                return app.OpenDocumentFile(path, PlainOptions());
            }
        }

        private static OpenOptions DetachedOptions(
            Application app,
            ModelPath path,
            WorksetMode worksets,
            List<WORKSETPICK> named)
        {
            var options = new OpenOptions
            {
                DetachFromCentralOption =
                    DetachFromCentralOption.DetachAndPreserveWorksets,
                Audit = false
            };

            options.SetOpenWorksetsConfiguration(
                WorksetsFor(app, path, worksets, named));

            return options;
        }

        // "Editable" means the worksets this user owns. Which those are
        // can be read from the central without opening the model, so the
        // set is worked out first and only those are loaded.
        private static WorksetConfiguration WorksetsFor(
            Application app,
            ModelPath path,
            WorksetMode worksets,
            List<WORKSETPICK> named)
        {
            // A named set is an explicit instruction and beats the mode
            if (named != null && named.Count > 0)
                return NamedWorksets(path, named);

            if (worksets != WorksetMode.Editable)
            {
                return new WorksetConfiguration(
                    WorksetConfigurationOption.OpenAllWorksets);
            }

            var mine = new List<WorksetId>();

            try
            {
                string user = app.Username;

                foreach (WorksetPreview preview in
                         WorksharingUtils.GetUserWorksetInfo(path))
                {
                    if (!string.IsNullOrEmpty(preview.Owner) &&
                        string.Equals(preview.Owner, user,
                            StringComparison.OrdinalIgnoreCase))
                        mine.Add(preview.Id);
                }
            }
            catch
            {
                // Not workshared, or the central cannot be reached; the
                // caller's fallback open deals with it
            }

            var config = new WorksetConfiguration(
                WorksetConfigurationOption.CloseAllWorksets);

            // Opening an empty set is honest rather than helpful: it is
            // what "only the worksets I own" means when none are owned,
            // and the result row says so
            config.Open(mine);

            return config;
        }

        // The worksets somebody picked for this model, matched by
        // UniqueId first and by name second - see WORKSETPICK.
        //
        // The WorksetIds are resolved HERE, from the model, immediately
        // before the open. They are never persisted: the API says an
        // id "may be changed while synchronizing with central", so a
        // stored one would quietly start meaning a different workset.
        //
        // A pick the model has not got is simply not opened; the caller
        // reports what was asked for against what was found.
        public static WorksetConfiguration NamedWorksets(
            ModelPath path, List<WORKSETPICK> named)
        {
            var wanted = new List<WorksetId>();

            try
            {
                var have = new List<WORKSETPICK>();
                var ids = new List<WorksetId>();

                foreach (WorksetPreview preview in
                         WorksharingUtils.GetUserWorksetInfo(path))
                {
                    have.Add(new WORKSETPICK(
                        preview.Name, preview.UniqueId.ToString()));

                    ids.Add(preview.Id);
                }

                foreach (WORKSETPICK pick in named)
                {
                    WORKSETPICK found = WORKSETPICK.Find(have, pick);

                    if (found == null)
                        continue;

                    int at = have.IndexOf(found);

                    if (at >= 0 && !wanted.Contains(ids[at]))
                        wanted.Add(ids[at]);
                }
            }
            catch
            {
                // Not workshared, or the central cannot be reached; the
                // caller's fallback open deals with it
            }

            var config = new WorksetConfiguration(
                WorksetConfigurationOption.CloseAllWorksets);

            config.Open(wanted);

            return config;
        }

        // What a model's worksets are called, without opening it.
        // Autodesk's own words for GetUserWorksetInfo: it gives "a
        // preview of the user worksets available in a file, allowing an
        // application to look up the necessary workset ids and
        // information to properly fill out a WorksetConfiguration
        // structure before opening" - which is exactly this.
        public static List<WORKSETPICK> WorksetsOf(
            ModelPath path, out string trouble)
        {
            trouble = string.Empty;

            var found = new List<WORKSETPICK>();

            try
            {
                foreach (WorksetPreview preview in
                         WorksharingUtils.GetUserWorksetInfo(path))
                {
                    found.Add(new WORKSETPICK(
                        preview.Name, preview.UniqueId.ToString()));
                }
            }
            catch (Exception ex)
            {
                // The commonest answer here is "the model is not
                // workshared", which is not an error worth an icon.
                // For an ACC model this is also where the preview call
                // gives up - see WorksetsByOpening.
                trouble = ex.Message;
            }

            return found;
        }

        // ==================================================
        // Worksets of a CLOUD model
        // ==================================================
        //
        // A cloud path needs a REGION, and the region "is not reported
        // by anything readable" - the collaboration cache does not
        // carry it, so a model picked from there has none until
        // something works it out by trying. OpenWithRegions has always
        // done that; reading worksets has to do it too.
        //
        // Without this, a cloud path built from an empty region cannot
        // resolve and BOTH ways of reading the worksets fail with "the
        // central server could not be reached" - user-reported, with an
        // empty picker on every ACC model.
        //
        // Whichever region answers is written back onto the model, so
        // the caller can keep it and nothing pays for the probe twice.
        public static List<WORKSETPICK> WorksetsOfCloud(
            CloudModel model, out string trouble)
        {
            trouble = string.Empty;

            Exception firstFailure = null;

            NWCLOG.Head("GetUserWorksetInfo, region by region");
            NWCLOG.Say("   project " + model.ProjectGuid);
            NWCLOG.Say("   model   " + model.ModelGuid);
            NWCLOG.Say("   region on the job: " +
                       (string.IsNullOrEmpty(model.Region)
                            ? "(none - it will be probed)"
                            : model.Region));

            foreach (string region in RegionsToTry(model))
            {
                NWCLOG.Say("   trying region " + region);

                try
                {
                    ModelPath path =
                        ModelPathUtils.ConvertCloudGUIDsToCloudPath(
                            region, model.ProjectGuid, model.ModelGuid);

                    NWCLOG.Say("      path " + Told(path));

                    var found = new List<WORKSETPICK>();

                    foreach (WorksetPreview preview in
                             WorksharingUtils.GetUserWorksetInfo(path))
                    {
                        found.Add(new WORKSETPICK(
                            preview.Name, preview.UniqueId.ToString()));
                    }

                    // An empty answer from a region that did not throw
                    // is still an answer - the model is simply not
                    // workshared - so the region is remembered either way
                    model.Region = region;

                    NWCLOG.Say("      ANSWERED with " + found.Count +
                               " workset(s)");

                    foreach (WORKSETPICK one in found)
                        NWCLOG.Say("         " + one.Name);

                    return found;
                }
                catch (Exception ex)
                {
                    NWCLOG.Blew("GetUserWorksetInfo in " + region, ex);

                    firstFailure = Blame(firstFailure, ex);
                }
            }

            if (firstFailure != null)
                trouble = firstFailure.Message;

            NWCLOG.Say("   no region answered");

            return new List<WORKSETPICK>();
        }

        // The same, the certain way: open it in whichever region
        // answers, with every workset CLOSED, and read the table.
        public static List<WORKSETPICK> WorksetsByOpeningCloud(
            Application app, CloudModel model, out string trouble)
        {
            trouble = string.Empty;

            var found = new List<WORKSETPICK>();

            Document doc = null;

            NWCLOG.Head("Opening the model to read its worksets");

            try
            {
                doc = OpenWithRegions(app, model, null,
                    new WorksetConfiguration(
                        WorksetConfigurationOption.CloseAllWorksets));

                if (doc == null)
                {
                    trouble = "The model would not open.";

                    NWCLOG.Say("   opened nothing, and threw nothing");

                    return found;
                }

                NWCLOG.Say("   opened as \"" + doc.Title +
                           "\", region " + model.Region);
                NWCLOG.Say("   IsWorkshared = " + doc.IsWorkshared);

                foreach (Workset workset in
                         new FilteredWorksetCollector(doc)
                             .OfKind(WorksetKind.UserWorkset))
                {
                    found.Add(new WORKSETPICK(
                        workset.Name, workset.UniqueId.ToString()));
                }

                NWCLOG.Say("   " + found.Count + " user workset(s)");

                foreach (WORKSETPICK one in found)
                    NWCLOG.Say("      " + one.Name);
            }
            catch (Exception ex)
            {
                NWCLOG.Blew("the open", ex);

                trouble = ex.Message;
            }
            finally
            {
                if (doc != null)
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
            }

            return found;
        }

        // The worksets of a model that will NOT give up its preview.
        //
        // GetUserWorksetInfo talks to a CENTRAL, and every exception it
        // documents is a file-or-Revit-Server one; nothing in the API
        // says whether an ACC path works, and it is not to be relied on.
        // So this is the honest fallback: open the model and ask it.
        //
        // The open costs far less than it looks. Every workset is
        // CLOSED, so what comes back is the workset table and almost no
        // geometry - and the document is detached, so no central is
        // touched and nobody's worksets are taken. It is still slow
        // enough that the caller has to say it is happening.
        public static List<WORKSETPICK> WorksetsByOpening(
            Application app, ModelPath path, out string trouble)
        {
            trouble = string.Empty;

            var found = new List<WORKSETPICK>();

            Document doc = null;

            try
            {
                var options = new OpenOptions
                {
                    DetachFromCentralOption =
                        DetachFromCentralOption.DetachAndPreserveWorksets,
                    Audit = false
                };

                var shut = new WorksetConfiguration(
                    WorksetConfigurationOption.CloseAllWorksets);

                options.SetOpenWorksetsConfiguration(shut);

                doc = app.OpenDocumentFile(path, options);

                if (doc == null)
                {
                    trouble = "The model would not open.";

                    return found;
                }

                foreach (Workset workset in
                         new FilteredWorksetCollector(doc)
                             .OfKind(WorksetKind.UserWorkset))
                {
                    found.Add(new WORKSETPICK(
                        workset.Name, workset.UniqueId.ToString()));
                }
            }
            catch (Exception ex)
            {
                trouble = ex.Message;
            }
            finally
            {
                if (doc != null)
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
            }

            return found;
        }

        private static Document OpenPathWith(
            Application app, ModelPath path, WorksetConfiguration config)
        {
            var options = new OpenOptions
            {
                DetachFromCentralOption =
                    DetachFromCentralOption.DetachAndPreserveWorksets,
                Audit = false
            };

            options.SetOpenWorksetsConfiguration(config);

            try
            {
                return app.OpenDocumentFile(path, options);
            }
            catch
            {
                // A model that is not workshared refuses to be detached,
                // and has no worksets to choose from either
                return app.OpenDocumentFile(path, PlainOptions());
            }
        }

        // A cloud ModelPath has no useful ToString, so it is asked for
        // its user-visible form - which is also the thing to compare
        // against what ACC says the model is
        private static string Told(ModelPath path)
        {
            try
            {
                return ModelPathUtils.ConvertModelPathToUserVisiblePath(
                    path);
            }
            catch (Exception ex)
            {
                return "(could not be shown: " + ex.GetType().Name + ")";
            }
        }

        private static OpenOptions PlainOptions()
        {
            return new OpenOptions
            {
                DetachFromCentralOption =
                    DetachFromCentralOption.DoNotDetach,
                Audit = false
            };
        }
    }
}
