using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // One model to export: a file on disk, or a model in ACC.
    //
    // Deliberately NOT a CHWCLOUD.CloudModel, though the cloud ones are
    // turned into one to be opened. That type carries the chilled water
    // page's own fields, and two add-ins sharing a settings file would
    // mean one of them writing the other's choices away.
    public class NWCJOB
    {
        public enum Kind
        {
            Local = 0,
            Cloud = 1
        }

        public Kind What { get; set; }

        // What the row says, and what the .nwc is named after
        public string Name { get; set; }

        // Local only
        public string File { get; set; }

        // Cloud only
        public string Region { get; set; }
        public Guid ProjectGuid { get; set; }
        public Guid ModelGuid { get; set; }

        // Ticked in the list
        public bool Wanted { get; set; }

        // The 3D view to export THIS model from. Empty means "whatever
        // the window says", which is the usual case - one name, every
        // model. It is here rather than only on the settings because
        // the common need is the same options with a different view
        // name in one awkward model.
        public string ViewName { get; set; }

        // The worksets to open in THIS model. Empty means "whatever
        // the window's workset choice says", which is the usual case.
        //
        // Each carries a name AND a UniqueId - see WORKSETPICK for why
        // neither alone is enough. Never a WorksetId: the API says that
        // one changes when the model is synchronised.
        public List<WORKSETPICK> Worksets { get; set; }

        public bool HasOwnWorksets
        {
            get { return Worksets != null && Worksets.Count > 0; }
        }

        // This model's own export settings, or null to use the
        // window's. A whole settings object rather than a few fields,
        // so "different settings for this one" means all of them and
        // there is no half-overridden state to reason about.
        public NWCSETTINGS Overrides { get; set; }

        public bool HasOwnSettings
        {
            get { return Overrides != null; }
        }

        public NWCJOB()
        {
            Name = string.Empty;
            File = string.Empty;
            Region = string.Empty;
            ViewName = string.Empty;
            Wanted = true;
            Worksets = new List<WORKSETPICK>();
        }

        // What this job IS, for looking its worksets up again. Not the
        // display name: two models can be called the same thing.
        public string Key
        {
            get
            {
                return What == Kind.Cloud
                    ? "cloud:" + ModelGuid.ToString()
                    : "file:" + (File ?? string.Empty).ToLowerInvariant();
            }
        }

        public string Where
        {
            get
            {
                return What == Kind.Local
                    ? File
                    : "ACC  -  " + Region;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public CHWCLOUD.CloudModel ToCloudModel()
        {
            return new CHWCLOUD.CloudModel
            {
                Name = Name,
                Region = Region,
                ProjectGuid = ProjectGuid,
                ModelGuid = ModelGuid
            };
        }
    }

    // What the window was set to last time, and which models were in
    // the list. Kept as tab-separated text next to the other add-ins'
    // settings - no Revit in this file, so nothing here needs an API
    // context to read or write.
    public class NWCSETTINGS
    {
        public string OutputFolder { get; set; }

        // The 3D view to export from, by NAME, because every model has
        // its own views and only the name is common between them. Empty
        // means the whole model.
        public string ViewName { get; set; }

        // Put in front of the model's name, so one folder can hold a
        // set of exports without them overwriting each other
        public string Prefix { get; set; }
        public string Suffix { get; set; }

        public bool SharedCoordinates { get; set; }
        public bool ExportLinks { get; set; }
        public bool DivideIntoLevels { get; set; }
        public bool ExportElementIds { get; set; }
        public bool ExportRoomAsAttribute { get; set; }
        public bool ExportParts { get; set; }
        public bool ConvertElementProperties { get; set; }

        // 0 = all, 1 = elements, 2 = none. An int rather than the Revit
        // enum so this file stays free of Revit.
        public int Parameters { get; set; }

        public double FacetingFactor { get; set; }

        // 0 = every workset, 1 = only the ones this user owns
        public int Worksets { get; set; }

        // Skip a model whose .nwc is already there and newer than the
        // model itself - the usual case on a re-run
        public bool SkipUnchanged { get; set; }

        // Answer Revit's own dialogs while the batch runs, so an
        // unattended job cannot stall on one
        public bool Silent { get; set; }

        public List<NWCJOB> Jobs { get; set; }

        public NWCSETTINGS()
        {
            OutputFolder = string.Empty;
            ViewName = "Navisworks";
            Prefix = string.Empty;
            Suffix = string.Empty;

            SharedCoordinates = true;
            ExportLinks = false;
            DivideIntoLevels = true;
            ExportElementIds = true;
            ExportRoomAsAttribute = true;
            ExportParts = false;
            ConvertElementProperties = true;

            Parameters = 0;
            FacetingFactor = 1.0;
            Worksets = 0;
            SkipUnchanged = false;
            Silent = true;

            Jobs = new List<NWCJOB>();
        }

        // ==================================================
        // Where it lives
        // ==================================================

        public static string SettingsPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC");

                return Path.Combine(folder, "nwc_export.txt");
            }
        }

        // ==================================================
        // Reading and writing
        // ==================================================

        public static NWCSETTINGS Load()
        {
            var settings = new NWCSETTINGS();

            try
            {
                if (!File.Exists(SettingsPath))
                    return settings;

                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    string text = line.Trim();

                    if (text.Length == 0 || text.StartsWith("#"))
                        continue;

                    string[] parts = text.Split('\t');

                    if (parts.Length < 2)
                        continue;

                    if (parts[0] == "job")
                        ReadJob(settings, parts);
                    else if (parts[0] == "jobview" ||
                             parts[0] == "jobset" ||
                             parts[0] == "jobwork")
                        ReadJobExtra(settings, parts);
                    else
                        ReadOne(settings, parts[0], parts[1]);
                }
            }
            catch
            {
                // A settings file that cannot be read is not worth an
                // error: the window opens on its defaults
            }

            return settings;
        }

        private static void ReadOne(
            NWCSETTINGS settings, string key, string value)
        {
            switch (key)
            {
                case "folder": settings.OutputFolder = value; break;
                case "view": settings.ViewName = value; break;
                case "prefix": settings.Prefix = value; break;
                case "suffix": settings.Suffix = value; break;

                case "shared":
                    settings.SharedCoordinates = Yes(value); break;
                case "links":
                    settings.ExportLinks = Yes(value); break;
                case "levels":
                    settings.DivideIntoLevels = Yes(value); break;
                case "ids":
                    settings.ExportElementIds = Yes(value); break;
                case "rooms":
                    settings.ExportRoomAsAttribute = Yes(value); break;
                case "parts":
                    settings.ExportParts = Yes(value); break;
                case "properties":
                    settings.ConvertElementProperties = Yes(value); break;
                case "skip":
                    settings.SkipUnchanged = Yes(value); break;
                case "silent":
                    settings.Silent = Yes(value); break;

                case "parameters":
                    settings.Parameters = Number(value, 0); break;
                case "worksets":
                    settings.Worksets = Number(value, 0); break;

                case "faceting":
                    settings.FacetingFactor = Real(value, 1.0); break;
            }
        }

        // job <local|cloud> <wanted> <name> <file-or-region> <project> <model>
        private static void ReadJob(NWCSETTINGS settings, string[] parts)
        {
            if (parts.Length < 5)
                return;

            var job = new NWCJOB
            {
                What = parts[1] == "cloud"
                    ? NWCJOB.Kind.Cloud
                    : NWCJOB.Kind.Local,
                Wanted = Yes(parts[2]),
                Name = parts[3]
            };

            if (job.What == NWCJOB.Kind.Local)
            {
                job.File = parts[4];
            }
            else
            {
                if (parts.Length < 7)
                    return;

                job.Region = parts[4];

                Guid project;
                Guid model;

                if (!Guid.TryParse(parts[5], out project) ||
                    !Guid.TryParse(parts[6], out model))
                    return;

                job.ProjectGuid = project;
                job.ModelGuid = model;
            }

            settings.Jobs.Add(job);
        }

        // What this window was set to, remembered for next time. The
        // model list goes in as well, because that is this machine's
        // own working state.
        // jobview and jobset belong to the job LAST read, which is how
        // a per-model view name and a per-model settings block are kept
        // beside their model without inventing a nested format.
        private static void ReadJobExtra(NWCSETTINGS settings, string[] parts)
        {
            if (settings.Jobs.Count == 0)
                return;

            NWCJOB job = settings.Jobs[settings.Jobs.Count - 1];

            if (parts[0] == "jobview")
            {
                job.ViewName = parts[1];

                return;
            }

            if (parts[0] == "jobwork")
            {
                if (job.Worksets == null)
                    job.Worksets = new List<WORKSETPICK>();

                // jobwork <name> [uniqueid]
                //
                // One line per workset, so a name with anything in it
                // cannot run into the next one. The id is the third
                // field and may be absent: a file written before this
                // existed still reads, and those picks match by name
                // as they always did.
                if (!string.IsNullOrEmpty(parts[1]))
                {
                    job.Worksets.Add(new WORKSETPICK(
                        parts[1],
                        parts.Length > 2 ? parts[2] : string.Empty));
                }

                return;
            }

            if (parts.Length < 3)
                return;

            if (job.Overrides == null)
            {
                // Starts from the defaults, not from the window's
                // settings: an override is a whole set in its own right
                job.Overrides = new NWCSETTINGS();
            }

            ReadOne(job.Overrides, parts[1], parts[2]);
        }

        public void Save()
        {
            try
            {
                string folder = Path.GetDirectoryName(SettingsPath);

                if (!string.IsNullOrEmpty(folder) &&
                    !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(SettingsPath, Compose(true));
            }
            catch
            {
                // Settings that cannot be saved are not worth an error
            }
        }

        // This model's own settings, written under its job line. The
        // same keys as everywhere else, so one reader serves both.
        private void Beside(StringBuilder text)
        {
            Under(text, "folder", OutputFolder);
            Under(text, "view", ViewName);
            Under(text, "prefix", Prefix);
            Under(text, "suffix", Suffix);

            Under(text, "shared", SharedCoordinates);
            Under(text, "links", ExportLinks);
            Under(text, "levels", DivideIntoLevels);
            Under(text, "ids", ExportElementIds);
            Under(text, "rooms", ExportRoomAsAttribute);
            Under(text, "parts", ExportParts);
            Under(text, "properties", ConvertElementProperties);
            Under(text, "skip", SkipUnchanged);
            Under(text, "silent", Silent);

            Under(text, "parameters",
                Parameters.ToString(CultureInfo.InvariantCulture));
            Under(text, "worksets",
                Worksets.ToString(CultureInfo.InvariantCulture));
            Under(text, "faceting",
                FacetingFactor.ToString(CultureInfo.InvariantCulture));
        }

        private static void Under(
            StringBuilder text, string key, string value)
        {
            text.Append("jobset\t");
            text.Append(key);
            text.Append('\t');
            text.AppendLine(Clean(value));
        }

        private static void Under(
            StringBuilder text, string key, bool value)
        {
            Under(text, key, value ? "yes" : "no");
        }

        // A separate set of the same choices, for a model that wants
        // its own. The job list is deliberately NOT carried over - an
        // override says how to write ONE model, not which models there
        // are, and a list inside a list is a thing to reason about at
        // three in the morning.
        public NWCSETTINGS Copy()
        {
            return new NWCSETTINGS
            {
                OutputFolder = OutputFolder,
                ViewName = ViewName,
                Prefix = Prefix,
                Suffix = Suffix,

                Silent = Silent,

                SharedCoordinates = SharedCoordinates,
                ExportLinks = ExportLinks,
                DivideIntoLevels = DivideIntoLevels,
                ExportElementIds = ExportElementIds,
                ExportRoomAsAttribute = ExportRoomAsAttribute,
                ExportParts = ExportParts,
                ConvertElementProperties = ConvertElementProperties,

                Parameters = Parameters,
                FacetingFactor = FacetingFactor,
                Worksets = Worksets,
                SkipUnchanged = SkipUnchanged
            };
        }

        // ==================================================
        // Handing the settings to somebody else
        // ==================================================

        // The EXPORT SETTINGS only - how to write an NWC - with no
        // model list in it.
        //
        // The list is paths and cloud GUIDs, which belong to one
        // machine and one job; a shared file carrying them would put
        // somebody else's models into your window. What a team actually
        // wants to agree on is the settings, so that is what travels.
        public bool SaveTo(string path)
        {
            try
            {
                File.WriteAllText(path, Compose(false));

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Reads the settings out of a file and applies them to this
        // one, LEAVING THE MODEL LIST ALONE. A settings file that
        // happens to carry job lines is ignored on that score rather
        // than trusted - importing a colleague's settings must not
        // empty your list.
        public bool LoadFrom(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                foreach (string line in File.ReadAllLines(path))
                {
                    string text = line.Trim();

                    if (text.Length == 0 || text.StartsWith("#"))
                        continue;

                    string[] parts = text.Split('\t');

                    if (parts.Length < 2 || parts[0] == "job")
                        continue;

                    ReadOne(this, parts[0], parts[1]);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ==================================================

        private string Compose(bool withJobs)
        {
            {
                var text = new StringBuilder();

                text.AppendLine("# ATKINSMEC - NWC batch exporter");
                text.AppendLine("# " + (withJobs
                    ? "this machine's saved state"
                    : "export settings, safe to hand to anybody"));
                text.AppendLine("# written " +
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                text.AppendLine();

                Line(text, "folder", OutputFolder);
                Line(text, "view", ViewName);
                Line(text, "prefix", Prefix);
                Line(text, "suffix", Suffix);

                Line(text, "shared", SharedCoordinates);
                Line(text, "links", ExportLinks);
                Line(text, "levels", DivideIntoLevels);
                Line(text, "ids", ExportElementIds);
                Line(text, "rooms", ExportRoomAsAttribute);
                Line(text, "parts", ExportParts);
                Line(text, "properties", ConvertElementProperties);
                Line(text, "skip", SkipUnchanged);
                Line(text, "silent", Silent);

                Line(text, "parameters",
                    Parameters.ToString(CultureInfo.InvariantCulture));
                Line(text, "worksets",
                    Worksets.ToString(CultureInfo.InvariantCulture));
                Line(text, "faceting",
                    FacetingFactor.ToString(CultureInfo.InvariantCulture));

                if (!withJobs)
                    return text.ToString();

                text.AppendLine();

                foreach (NWCJOB job in Jobs)
                {
                    text.Append("job\t");
                    text.Append(job.What == NWCJOB.Kind.Cloud
                        ? "cloud\t"
                        : "local\t");
                    text.Append(job.Wanted ? "yes\t" : "no\t");
                    text.Append(Clean(job.Name));
                    text.Append('\t');

                    if (job.What == NWCJOB.Kind.Local)
                    {
                        text.AppendLine(Clean(job.File));
                    }
                    else
                    {
                        text.Append(Clean(job.Region));
                        text.Append('\t');
                        text.Append(job.ProjectGuid.ToString());
                        text.Append('\t');
                        text.AppendLine(job.ModelGuid.ToString());
                    }

                    if (!string.IsNullOrEmpty(job.ViewName))
                    {
                        text.Append("jobview\t");
                        text.AppendLine(Clean(job.ViewName));
                    }

                    if (job.Worksets != null)
                    {
                        foreach (WORKSETPICK workset in job.Worksets)
                        {
                            text.Append("jobwork\t");
                            text.Append(Clean(workset.Name));
                            text.Append('\t');
                            text.AppendLine(Clean(workset.UniqueId));
                        }
                    }

                    if (job.Overrides != null)
                        job.Overrides.Beside(text);
                }

                return text.ToString();
            }
        }

        // ==================================================

        private static void Line(
            StringBuilder text, string key, string value)
        {
            text.Append(key);
            text.Append('\t');
            text.AppendLine(Clean(value));
        }

        private static void Line(
            StringBuilder text, string key, bool value)
        {
            Line(text, key, value ? "yes" : "no");
        }

        // A tab in a value would split the line into the wrong fields
        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace('\t', ' ').Replace('\r', ' ')
                        .Replace('\n', ' ');
        }

        private static bool Yes(string value)
        {
            return string.Equals(value, "yes",
                StringComparison.OrdinalIgnoreCase);
        }

        private static int Number(string value, int fallback)
        {
            int found;

            return int.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out found)
                ? found
                : fallback;
        }

        private static double Real(string value, double fallback)
        {
            double found;

            return double.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out found)
                ? found
                : fallback;
        }
    }
}
