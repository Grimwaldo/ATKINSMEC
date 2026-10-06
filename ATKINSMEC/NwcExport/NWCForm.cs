using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // The NWC batch exporter's window.
    //
    // MODAL, exactly as CHW VOLUME is, and for the same reason: opening
    // another model is a Revit API call, so the work has to run inside
    // the command's API context. A modeless window would need an
    // external event for every step and could not show progress between
    // models at all.
    //
    // Nothing here is written back to the model anybody has open. The
    // one document that might be open is exported where it sits in the
    // list and is never closed - see NWCEXPORT.
    public partial class NWCForm : System.Windows.Forms.Form
    {
        private readonly Document _doc;
        private readonly Autodesk.Revit.ApplicationServices.Application _app;

        // Only for QUIET: answering a dialog is a UI-side event
        private readonly Autodesk.Revit.UI.UIApplication _ui;

        // Read in the constructor, which runs inside the command's API
        // context - a button handler does not
        private readonly string _docTitle;
        private readonly string _docPath;
        private readonly List<string> _docViews;

        private NWCSETTINGS _settings;

        private CHWSTOP _stop;
        private bool _running;

        // Set while the grid is being filled, so its own change events
        // do not read half-built rows back into the jobs
        private bool _filling;

        public NWCForm(
            Document doc,
            Autodesk.Revit.ApplicationServices.Application app,
            Autodesk.Revit.UI.UIApplication ui)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _doc = doc;
            _app = app;
            _ui = ui;

            _docTitle = doc == null ? string.Empty : doc.Title;

            _docPath = doc == null ? string.Empty : SafePath(doc);

            _docViews = NWCEXPORT.ViewNames(doc);
        }

        private static string SafePath(Document doc)
        {
            try
            {
                return doc.PathName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // ==================================================
        // Opening
        // ==================================================

        private void NWCForm_Load(object sender, EventArgs e)
        {
            ModelColumns();
            Columns();

            cboParams.Items.Add("All");
            cboParams.Items.Add("Elements only");
            cboParams.Items.Add("None");

            cboWorksets.Items.Add("Every workset");
            cboWorksets.Items.Add("Only the ones I own");

            _settings = NWCSETTINGS.Load();

            Fill();

            // The 3D views of whatever is open are the only view names
            // that can be known without opening something. They are a
            // suggestion: the name is matched in every model, and a
            // model that has not got it is reported rather than guessed
            // around.
            cboView.Items.Add("(the whole model)");

            foreach (string name in _docViews)
                cboView.Items.Add(name);

            cboView.Text = string.IsNullOrEmpty(_settings.ViewName)
                ? "(the whole model)"
                : _settings.ViewName;

            btnAddOpen.Enabled = _doc != null;

            if (_doc == null)
            {
                lblProgress.Text =
                    "No model is open, which is the usual way to run this.";
            }

            if (!NWCEXPORT.Available())
            {
                lblProgress.Text =
                    "The Navisworks NWC exporter is not installed - " +
                    "nothing can be written until it is.";
            }

            Rebuild();
        }

        // The model list is a grid rather than a tick list because it
        // carries per-model data now: its own 3D view, and whether it
        // has settings of its own.
        private void ModelColumns()
        {
            dgvModels.Columns.Clear();

            var tick = new DataGridViewCheckBoxColumn
            {
                HeaderText = string.Empty,
                Name = "colWanted",
                Width = 30,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            dgvModels.Columns.Add(tick);

            ModelColumn("Model", 190, true);
            ModelColumn("3D view", 130, false);
            ModelColumn("Settings", 70, true);
            ModelColumn("Worksets", 70, true);
            ModelColumn("Where it is", 190, true);

            dgvModels.Columns[dgvModels.Columns.Count - 1].AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;

            dgvModels.CurrentCellDirtyStateChanged +=
                dgvModels_CurrentCellDirtyStateChanged;

            dgvModels.CellValueChanged += dgvModels_CellValueChanged;

            dgvModels.Columns[2].ToolTipText =
                "Type a 3D view name to export THIS model from. " +
                "Leave it blank to use the one on the window.";
        }

        private void ModelColumn(string header, int width, bool locked)
        {
            dgvModels.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                ReadOnly = locked,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private void Columns()
        {
            dgvResults.Columns.Clear();

            Column("Model", 180);
            Column("From", 50);
            Column("3D view", 120);
            Column("Settings", 65);
            Column("Worksets", 65);
            Column("Written", 55);
            Column("Size", 65);
            Column("Seconds", 60);
            Column("What happened", 270);

            dgvResults.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.None;

            dgvResults.Columns[dgvResults.Columns.Count - 1].AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;
        }

        private void Column(string header, int width)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            };

            dgvResults.Columns.Add(column);
        }

        private void Fill()
        {
            txtFolder.Text = _settings.OutputFolder;
            txtPrefix.Text = _settings.Prefix;
            txtSuffix.Text = _settings.Suffix;

            chkShared.Checked = _settings.SharedCoordinates;
            chkLinks.Checked = _settings.ExportLinks;
            chkLevels.Checked = _settings.DivideIntoLevels;
            chkIds.Checked = _settings.ExportElementIds;
            chkRooms.Checked = _settings.ExportRoomAsAttribute;
            chkParts.Checked = _settings.ExportParts;
            chkProperties.Checked = _settings.ConvertElementProperties;
            chkSkip.Checked = _settings.SkipUnchanged;
            chkSilent.Checked = _settings.Silent;

            cboParams.SelectedIndex =
                Math.Max(0, Math.Min(2, _settings.Parameters));

            cboWorksets.SelectedIndex =
                Math.Max(0, Math.Min(1, _settings.Worksets));

            numFacet.Value = (decimal)Math.Max(0.0,
                Math.Min(10.0, _settings.FacetingFactor));
        }

        private void Read()
        {
            _settings.OutputFolder = txtFolder.Text.Trim();
            _settings.Prefix = txtPrefix.Text.Trim();
            _settings.Suffix = txtSuffix.Text.Trim();

            string view = cboView.Text.Trim();

            _settings.ViewName =
                view == "(the whole model)" ? string.Empty : view;

            _settings.SharedCoordinates = chkShared.Checked;
            _settings.ExportLinks = chkLinks.Checked;
            _settings.DivideIntoLevels = chkLevels.Checked;
            _settings.ExportElementIds = chkIds.Checked;
            _settings.ExportRoomAsAttribute = chkRooms.Checked;
            _settings.ExportParts = chkParts.Checked;
            _settings.ConvertElementProperties = chkProperties.Checked;
            _settings.SkipUnchanged = chkSkip.Checked;
            _settings.Silent = chkSilent.Checked;

            _settings.Parameters = Math.Max(0, cboParams.SelectedIndex);
            _settings.Worksets = Math.Max(0, cboWorksets.SelectedIndex);

            _settings.FacetingFactor = (double)numFacet.Value;

            ReadGrid();
        }

        // ==================================================
        // The list
        // ==================================================

        // The grid is the display; _settings.Jobs is the truth. Every
        // edit is read back before anything acts on it, so a view name
        // typed and not tabbed out of is not lost.
        private void ReadGrid()
        {
            for (int i = 0;
                 i < dgvModels.Rows.Count && i < _settings.Jobs.Count;
                 i++)
            {
                DataGridViewRow row = dgvModels.Rows[i];

                _settings.Jobs[i].Wanted =
                    row.Cells[0].Value is bool && (bool)row.Cells[0].Value;

                object view = row.Cells[2].Value;

                _settings.Jobs[i].ViewName = view == null
                    ? string.Empty
                    : view.ToString().Trim();
            }
        }

        private void Rebuild()
        {
            _filling = true;

            dgvModels.Rows.Clear();

            foreach (NWCJOB job in _settings.Jobs)
            {
                int index = dgvModels.Rows.Add();

                DataGridViewRow row = dgvModels.Rows[index];

                row.Cells[0].Value = job.Wanted;
                row.Cells[1].Value = job.Name;
                row.Cells[2].Value = job.ViewName;
                row.Cells[3].Value = job.HasOwnSettings
                    ? "its own"
                    : "(window)";
                row.Cells[4].Value = job.HasOwnWorksets
                    ? job.Worksets.Count + " picked"
                    : "(window)";
                row.Cells[5].Value = job.Where;

                row.Cells[1].ToolTipText = job.Where;
                row.Cells[5].ToolTipText = job.Where;

                if (job.HasOwnWorksets)
                {
                    var said = new List<string>();

                    foreach (WORKSETPICK pick in job.Worksets)
                        said.Add(pick.Name);

                    row.Cells[4].ToolTipText =
                        string.Join(", ", said.ToArray());
                }

                if (job.HasOwnSettings)
                {
                    row.Cells[3].Style.ForeColor =
                        System.Drawing.Color.FromArgb(0, 90, 160);
                }

                if (job.HasOwnWorksets)
                {
                    row.Cells[4].Style.ForeColor =
                        System.Drawing.Color.FromArgb(0, 90, 160);
                }
            }

            Unpick();

            _filling = false;

            Caption();
        }

        private void Unpick()
        {
            dgvModels.ClearSelection();

            try
            {
                dgvModels.CurrentCell = null;
            }
            catch
            {
                // A grid mid-edit refuses, which is harmless here
            }
        }

        private void Caption()
        {
            grpModels.Text = _settings.Jobs.Count == 0
                ? "1. Models to export"
                : "1. Models to export  -  " + _settings.Jobs.Count +
                  " in the list, " + Ticked().Count + " ticked";
        }

        // A tick box does not commit its new value until the cell loses
        // focus, so without this the count in the caption lags a click
        // behind and Export could read yesterday's ticks
        private void dgvModels_CurrentCellDirtyStateChanged(
            object sender, EventArgs e)
        {
            if (dgvModels.IsCurrentCellDirty)
            {
                dgvModels.CommitEdit(
                    DataGridViewDataErrorContexts.Commit);
            }
        }

        private void dgvModels_CellValueChanged(
            object sender, DataGridViewCellEventArgs e)
        {
            if (_filling || e.RowIndex < 0)
                return;

            ReadGrid();

            Caption();
        }

        private List<NWCJOB> Ticked()
        {
            var wanted = new List<NWCJOB>();

            for (int i = 0;
                 i < dgvModels.Rows.Count && i < _settings.Jobs.Count;
                 i++)
            {
                object ticked = dgvModels.Rows[i].Cells[0].Value;

                if (ticked is bool && (bool)ticked)
                    wanted.Add(_settings.Jobs[i]);
            }

            return wanted;
        }

        // Which row the buttons act on
        private int Picked()
        {
            // Nothing picked is NOT "the first one" - see Unpick
            if (dgvModels.CurrentRow == null ||
                dgvModels.SelectedRows.Count == 0)
                return -1;

            int index = dgvModels.CurrentRow.Index;

            return index >= 0 && index < _settings.Jobs.Count
                ? index
                : -1;
        }

        // This model's own settings, or back to the window's
        private void btnJobSettings_Click(object sender, EventArgs e)
        {
            int index = Picked();

            if (index < 0)
            {
                lblProgress.Text =
                    "Click the model you want to set up first.";

                return;
            }

            NWCJOB job = _settings.Jobs[index];

            // Read the window first, so a model given its own settings
            // starts from what is on screen rather than from what was
            // last saved
            Read();

            using (var dialog = new NWCMODELForm(
                       job.Name, _settings, job.Overrides))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                job.Overrides = dialog.Chosen;
            }

            Rebuild();

            lblProgress.Text = job.HasOwnSettings
                ? job.Name + " now has its own export settings."
                : job.Name + " follows the settings on this window.";
        }

        private void btnAddFiles_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Revit models to export";
                dialog.Filter = "Revit models (*.rvt)|*.rvt";
                dialog.Multiselect = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                int added = 0;

                foreach (string file in dialog.FileNames)
                {
                    if (Has(file))
                        continue;

                    _settings.Jobs.Add(new NWCJOB
                    {
                        What = NWCJOB.Kind.Local,
                        Name = Path.GetFileNameWithoutExtension(file),
                        File = file,
                        Wanted = true
                    });

                    added++;
                }

                Rebuild();

                lblProgress.Text = added == 0
                    ? "Those were already in the list."
                    : "Added " + added + " model(s).";
            }
        }

        private void btnAddAcc_Click(object sender, EventArgs e)
        {
            // The ACC picker is CHW VOLUME's, reused whole. It reads
            // Revit's own collaboration cache, so a model that has been
            // opened on this machine can be picked rather than having
            // its two GUIDs typed in.
            var already = new List<CHWCLOUD.CloudModel>();

            foreach (NWCJOB job in _settings.Jobs)
            {
                if (job.What == NWCJOB.Kind.Cloud)
                    already.Add(job.ToCloudModel());
            }

            using (var dialog = new CHWCACHEForm(_doc, already))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK ||
                    dialog.Results.Count == 0)
                    return;

                foreach (CHWCLOUD.CloudModel model in dialog.Results)
                {
                    if (Has(model.ModelGuid))
                        continue;

                    _settings.Jobs.Add(new NWCJOB
                    {
                        What = NWCJOB.Kind.Cloud,
                        Name = model.Name,
                        Region = model.Region,
                        ProjectGuid = model.ProjectGuid,
                        ModelGuid = model.ModelGuid,
                        Wanted = true
                    });
                }

                Rebuild();
            }
        }

        private void btnAddOpen_Click(object sender, EventArgs e)
        {
            if (_doc == null)
                return;

            if (!string.IsNullOrEmpty(_docPath))
            {
                if (Has(_docPath))
                {
                    lblProgress.Text = "That one is already in the list.";

                    return;
                }

                _settings.Jobs.Add(new NWCJOB
                {
                    What = NWCJOB.Kind.Local,
                    Name = Path.GetFileNameWithoutExtension(_docPath),
                    File = _docPath,
                    Wanted = true
                });

                Rebuild();

                return;
            }

            // A cloud model has no path on disk, so it goes in by its
            // own identity
            string error;

            CHWCLOUD.CloudModel model =
                CHWCLOUD.FromDocument(_doc, out error);

            if (model == null)
            {
                MessageBox.Show(
                    this,
                    string.IsNullOrEmpty(error)
                        ? "That model could not be identified."
                        : error,
                    "NWC BATCH EXPORTER");

                return;
            }

            if (Has(model.ModelGuid))
            {
                lblProgress.Text = "That one is already in the list.";

                return;
            }

            _settings.Jobs.Add(new NWCJOB
            {
                What = NWCJOB.Kind.Cloud,
                Name = model.Name,
                Region = model.Region,
                ProjectGuid = model.ProjectGuid,
                ModelGuid = model.ModelGuid,
                Wanted = true
            });

            Rebuild();
        }

        private bool Has(string file)
        {
            foreach (NWCJOB job in _settings.Jobs)
            {
                if (job.What == NWCJOB.Kind.Local &&
                    string.Equals(job.File, file,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool Has(Guid model)
        {
            foreach (NWCJOB job in _settings.Jobs)
            {
                if (job.What == NWCJOB.Kind.Cloud &&
                    job.ModelGuid == model)
                    return true;
            }

            return false;
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            // Removing never falls back to "everything": an empty
            // selection here would otherwise wipe the whole list
            int picked = Picked();

            if (picked < 0)
            {
                lblProgress.Text = "Click the row to remove first.";

                return;
            }

            ReadGrid();

            _settings.Jobs.RemoveAt(picked);

            Rebuild();
        }

        private void btnTickAll_Click(object sender, EventArgs e)
        {
            Tick(true);
        }

        private void btnTickNone_Click(object sender, EventArgs e)
        {
            Tick(false);
        }

        private void Tick(bool on)
        {
            ReadGrid();

            foreach (NWCJOB job in _settings.Jobs)
                job.Wanted = on;

            Rebuild();
        }

        // Which worksets this one model opens with
        private void btnJobWorksets_Click(object sender, EventArgs e)
        {
            int index = Picked();

            if (index < 0)
            {
                lblProgress.Text =
                    "Click the model you want to set up first.";

                return;
            }

            ReadGrid();

            NWCJOB job = _settings.Jobs[index];

            string trouble;

            List<WORKSETPICK> available = Worksets(job, out trouble);

            if (available.Count == 0)
            {
                lblProgress.Text =
                    "Nothing came back - what was tried is written to " +
                    NWCLOG.Where;
            }

            using (var dialog = new NWCWORKSETForm(
                       job.Name, available,
                       NWCEXPORT.Explain(trouble), job.Worksets))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    // Reading the worksets is how a cloud model's region
                    // gets discovered, so the row has something new to
                    // show even when nothing was picked
                    Rebuild();

                    return;
                }

                job.Worksets = dialog.Chosen;
            }

            Rebuild();

            lblProgress.Text = job.HasOwnWorksets
                ? job.Name + " opens " + job.Worksets.Count +
                  " workset(s) of its own."
                : job.Name + " follows the workset choice on this window.";
        }

        // What worksets a model has, read once per window.
        //
        // TWO WAYS, and the second is not a rare fallback. The quick
        // call asks the model's CENTRAL even when it is handed a path
        // to a file on disk, so a local copy whose central is off the
        // network, a detached model, or an ACC model can all answer
        // "the central server could not be reached" - user-reported,
        // with an empty list and no way to pick anything. Opening the
        // model reads the file itself and needs no central at all.
        //
        // So it falls through on its own rather than asking. Asking was
        // a wall: for a model whose central cannot be reached the quick
        // way NEVER works, so the same question came back every time
        // the button was pressed and the worksets never appeared.
        private List<WORKSETPICK> Worksets(NWCJOB job, out string trouble)
        {
            trouble = string.Empty;

            NWCLOG.Start("Worksets for " + job.Name);

            // Which Revit this is matters more than anything else here:
            // a cloud model saved in a newer release cannot be opened at
            // all, and without this the log does not say which version
            // was even asking
            try
            {
                NWCLOG.Say("Revit " + _app.VersionNumber +
                           "  (" + _app.VersionName + ")");
            }
            catch
            {
                NWCLOG.Say("Revit version could not be read");
            }


            NWCLOG.Say(job.What == NWCJOB.Kind.Cloud
                ? "a model in ACC"
                : "a file on disk: " + job.File);

            if (_worksets.ContainsKey(job.Key))
            {
                trouble = _troubles.ContainsKey(job.Key)
                    ? _troubles[job.Key]
                    : string.Empty;

                return _worksets[job.Key];
            }

            // The model open in Revit answers for itself, with no
            // central and no waiting - and must NOT be opened again,
            // which would throw
            List<WORKSETPICK> here = NWCEXPORT.WorksetsOfOpen(_doc, job);

            if (here != null)
            {
                NWCLOG.Say("this model is the one open in Revit: " +
                           here.Count + " workset(s)");

                NWCLOG.Save();

                _worksets[job.Key] = here;
                _troubles[job.Key] = string.Empty;

                return here;
            }

            List<WORKSETPICK> available;

            Busy("Reading the worksets in " + job.Name + "...");

            NWCLOG.Say("the quick way: no open");

            try
            {
                available = NWCEXPORT.WorksetsOf(job, out trouble);
            }
            finally
            {
                Idle();
            }

            if (available.Count == 0 && _app != null)
            {
                // Every workset is CLOSED on the way in, so this loads
                // the workset table and almost no geometry, and the
                // model is detached and closed again without saving
                Busy("Opening " + job.Name + " to read its worksets - " +
                     "this can take a moment...");

                try
                {
                    available = NWCEXPORT.WorksetsByOpening(
                        _app, job, out trouble);
                }
                finally
                {
                    Idle();
                }
            }

            NWCLOG.Head("What the window will show");
            NWCLOG.Say("   " + available.Count + " workset(s)");
            NWCLOG.Say("   trouble: " +
                       (string.IsNullOrEmpty(trouble)
                            ? "(none)"
                            : trouble));

            NWCLOG.Save();

            // Remembered either way, including an empty answer: a model
            // that has no worksets must not be opened again on every
            // click for the rest of the session
            _worksets[job.Key] = available;
            _troubles[job.Key] = trouble;

            return available;
        }

        // Cleared when the list changes, since a row can be removed and
        // a different model put in its place
        private readonly Dictionary<string, List<WORKSETPICK>> _worksets =
            new Dictionary<string, List<WORKSETPICK>>();

        private readonly Dictionary<string, string> _troubles =
            new Dictionary<string, string>();

        private void Busy(string saying)
        {
            Cursor = Cursors.WaitCursor;

            lblProgress.Text = saying;

            System.Windows.Forms.Application.DoEvents();
        }

        private void Idle()
        {
            Cursor = Cursors.Default;

            lblProgress.Text = string.Empty;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Where the .nwc files go";

                if (Directory.Exists(txtFolder.Text.Trim()))
                    dialog.SelectedPath = txtFolder.Text.Trim();

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    txtFolder.Text = dialog.SelectedPath;
            }
        }

        // ==================================================
        // Exporting
        // ==================================================

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (_running)
                return;

            Read();

            List<NWCJOB> wanted = Ticked();

            if (wanted.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Nothing is ticked, so there is nothing to export.",
                    "NWC BATCH EXPORTER");

                return;
            }

            if (string.IsNullOrEmpty(_settings.OutputFolder))
            {
                MessageBox.Show(
                    this,
                    "Choose a folder for the .nwc files first.",
                    "NWC BATCH EXPORTER");

                return;
            }

            if (MessageBox.Show(
                    this,
                    Describe(wanted),
                    "NWC BATCH EXPORTER",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _settings.Save();

            Running(true);

            NWCLOG.Start("Exporting " + wanted.Count + " model(s)");

            try
            {
                NWCLOG.Say("Revit " + _app.VersionNumber +
                           "  (" + _app.VersionName + ")");
            }
            catch
            {
            }

            // Silent mode must come off again whatever happens: a
            // dialog handler left subscribed would answer Revit's
            // questions for the rest of the session
            if (_settings.Silent)
                QUIET.Start(_ui, _app);

            NWCEXPORT.Result result;

            try
            {
                result = NWCEXPORT.Run(
                    _app, _doc, wanted, _settings, _stop);
            }
            catch (Exception ex)
            {
                NWCLOG.Blew("the run", ex);

                MessageBox.Show(
                    this,
                    "The run stopped on something unexpected:" +
                    Environment.NewLine + Environment.NewLine +
                    ex.Message,
                    "NWC BATCH EXPORTER",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }
            finally
            {
                int answered = QUIET.Answered;

                QUIET.Stop();

                if (answered > 0)
                {
                    NWCLOG.Head("Silent mode answered " + answered +
                                " message(s)");
                }

                NWCLOG.Save();

                Running(false);
            }

            Show(result);
        }

        private string Describe(List<NWCJOB> wanted)
        {
            string where = string.IsNullOrEmpty(_settings.ViewName)
                ? "the WHOLE MODEL"
                : "the 3D view called \"" + _settings.ViewName + "\"";

            // A model with its own view or its own settings is NOT
            // covered by the sentence above, so the count of those is
            // said out loud rather than left for the results table
            int ownView = 0;
            int ownSettings = 0;
            int ownWorksets = 0;

            foreach (NWCJOB job in wanted)
            {
                if (!string.IsNullOrEmpty(job.ViewName))
                    ownView++;

                if (job.HasOwnSettings)
                    ownSettings++;

                if (job.HasOwnWorksets)
                    ownWorksets++;
            }

            string apart = string.Empty;

            if (ownView > 0)
            {
                apart += Environment.NewLine + "   -  " + ownView +
                         " name their own 3D view.";
            }

            if (ownSettings > 0)
            {
                apart += Environment.NewLine + "   -  " + ownSettings +
                         " have export settings of their own.";
            }

            if (ownWorksets > 0)
            {
                apart += Environment.NewLine + "   -  " + ownWorksets +
                         " open only the worksets picked for them.";
            }

            if (apart.Length > 0)
                apart = Environment.NewLine + apart;

            return "About to export " + wanted.Count +
                   " model(s) as .nwc, from " + where + "." + apart +
                   Environment.NewLine + Environment.NewLine +
                   "They go into:" + Environment.NewLine +
                   "   " + _settings.OutputFolder +
                   Environment.NewLine + Environment.NewLine +
                   "Each model is opened in the background, exported and " +
                   "closed again - none of them appears in the Revit " +
                   "window, and a file already there is overwritten. " +
                   "A model open in Revit right now is exported where it " +
                   "is and is never closed." +
                   Environment.NewLine + Environment.NewLine +
                   "This can take a while. Go ahead?";
        }

        private void Running(bool on)
        {
            _running = on;

            btnExport.Enabled = !on;
            btnStop.Enabled = on;

            btnAddFiles.Enabled = !on;
            btnAddAcc.Enabled = !on;
            btnAddOpen.Enabled = !on && _doc != null;
            btnRemove.Enabled = !on;
            btnJobSettings.Enabled = !on;
            btnJobWorksets.Enabled = !on;
            chkSilent.Enabled = !on;
            btnClose.Enabled = !on;

            Cursor = on ? Cursors.WaitCursor : Cursors.Default;

            if (on)
            {
                dgvResults.Rows.Clear();

                lblSummary.Text = string.Empty;

                prgExport.Value = 0;
                prgExport.Maximum = Math.Max(1, Ticked().Count);
                prgExport.Visible = true;

                _stop = new CHWSTOP
                {
                    // Between models only - never inside a Revit
                    // operation, where pumping messages invites trouble
                    Pump = delegate
                    {
                        System.Windows.Forms.Application.DoEvents();
                    },

                    Step = Along
                };
            }
            else
            {
                lblProgress.Text = string.Empty;

                prgExport.Visible = false;
            }
        }

        // Called between models, so the bar and the line underneath it
        // say the same thing: how many are done, and what is being
        // worked on now. A model can take minutes, which is exactly why
        // the count matters more than a spinner.
        private void Along(int done, int total, string what)
        {
            if (prgExport.Maximum != total && total > 0)
                prgExport.Maximum = total;

            prgExport.Value = Math.Max(0, Math.Min(prgExport.Maximum, done));

            lblProgress.Text = string.IsNullOrEmpty(what)
                ? done + " of " + total + " done."
                : done + " of " + total + " done - now " + what +
                  (_settings.Silent
                       ? "   (silent: Revit's messages are being answered)"
                       : string.Empty);

            System.Windows.Forms.Application.DoEvents();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_stop != null)
                _stop.Stop();

            lblProgress.Text = "Stopping after this model...";
        }

        private void Show(NWCEXPORT.Result result)
        {
            if (!string.IsNullOrEmpty(result.Blocked))
            {
                MessageBox.Show(
                    this,
                    result.Blocked,
                    "NWC BATCH EXPORTER",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            dgvResults.Rows.Clear();

            foreach (NWCEXPORT.Outcome outcome in result.Outcomes)
            {
                int index = dgvResults.Rows.Add();

                DataGridViewRow row = dgvResults.Rows[index];

                row.Cells[0].Value = outcome.Model;
                row.Cells[1].Value = outcome.Kind;
                row.Cells[2].Value = outcome.View;
                row.Cells[3].Value = outcome.Whose;
                row.Cells[4].Value = outcome.Worksets;
                row.Cells[5].Value = outcome.Exported ? "yes" : "no";
                row.Cells[6].Value = outcome.Size;
                row.Cells[7].Value = outcome.Seconds.ToString("N1");
                row.Cells[8].Value = outcome.Exported
                    ? outcome.File
                    : outcome.Said;

                row.Cells[2].ToolTipText = outcome.View;

                row.Cells[8].ToolTipText = outcome.Exported
                    ? outcome.File
                    : outcome.Said;

                if (!outcome.Exported)
                {
                    row.Cells[5].Style.ForeColor =
                        System.Drawing.Color.Firebrick;
                }
            }

            dgvResults.ClearSelection();

            lblSummary.Text =
                result.Exported + " written, " + result.Failed +
                " failed, " + result.Skipped + " skipped" +
                (result.Stopped ? "  -  stopped part way." : ".");

            MessageBox.Show(
                this,
                result.Exported + " model(s) written to" +
                Environment.NewLine + "   " + _settings.OutputFolder +
                Environment.NewLine + Environment.NewLine +
                (result.Failed == 0
                    ? "Nothing failed."
                    : result.Failed + " did not export - the table says " +
                      "what happened to each one.") +
                (result.Stopped
                    ? Environment.NewLine + Environment.NewLine +
                      "Stopped before the end of the list."
                    : string.Empty),
                "NWC BATCH EXPORTER",
                MessageBoxButtons.OK,
                result.Failed == 0
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);
        }

        // ==================================================

        // ==================================================
        // Handing the settings round
        // ==================================================

        private const string SettingsFilter =
            "NWC export settings (*.nwcset)|*.nwcset|" +
            "Text files (*.txt)|*.txt|All files (*.*)|*.*";

        private void btnExportSettings_Click(object sender, EventArgs e)
        {
            Read();

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save these export settings";
                dialog.Filter = SettingsFilter;
                dialog.DefaultExt = "nwcset";
                dialog.FileName = "nwc-export-settings.nwcset";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                if (!_settings.SaveTo(dialog.FileName))
                {
                    MessageBox.Show(
                        this,
                        "That file could not be written. It may be open " +
                        "in something else, or the folder may be " +
                        "read-only.",
                        "NWC BATCH EXPORTER",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                lblProgress.Text =
                    "Settings saved to " +
                    Path.GetFileName(dialog.FileName) +
                    "  -  the model list is not in it.";
            }
        }

        private void btnImportSettings_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Open export settings";
                dialog.Filter = SettingsFilter;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                // Read the window first, so anything the file does not
                // mention keeps what is on screen rather than falling
                // back to a default nobody chose
                Read();

                if (!_settings.LoadFrom(dialog.FileName))
                {
                    MessageBox.Show(
                        this,
                        "That file could not be read as export settings.",
                        "NWC BATCH EXPORTER",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                Fill();

                // The view name lives in the combo rather than in Fill,
                // since the combo also carries the open model's views
                cboView.Text = string.IsNullOrEmpty(_settings.ViewName)
                    ? "(the whole model)"
                    : _settings.ViewName;

                lblProgress.Text =
                    "Settings read from " +
                    Path.GetFileName(dialog.FileName) +
                    "  -  your model list is untouched.";
            }
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            using (var help = new NWCHELPForm())
            {
                help.ShowDialog(this);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            if (_running)
                return;

            Read();

            _settings.Save();

            this.Close();
        }
    }
}
