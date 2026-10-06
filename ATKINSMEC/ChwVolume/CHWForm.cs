using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Two tabs over one list of models: the ones open in Revit, and the
    // ones kept in ACC. They are measured together - Calculate takes
    // whatever is ticked on both - but they are managed quite
    // differently, so they are kept apart on screen.
    public partial class CHWForm : System.Windows.Forms.Form
    {
        private const string WorksetsAll = "All worksets";
        private const string WorksetsEditable = "Editable only";

        // Both grids share these; only the ACC grid has the fifth
        private const int ColCheck = 0;
        private const int ColName = 1;
        private const int ColSupply = 2;
        private const int ColReturn = 3;
        private const int ColWorksets = 4;

        private readonly Document _doc;

        private readonly Autodesk.Revit.ApplicationServices.Application _app;

        private CHWVOLUME.Result _lastResult;

        private List<CHWCLOUD.CloudModel> _cloudModels =
            new List<CHWCLOUD.CloudModel>();

        private List<string> _typeNames = new List<string>();

        // Set while rows are being built, so the grids' own change
        // events do not write half-filled values back into the models
        private bool _loading;

        // Non-null only while a measurement or a type read is running.
        // Doubles as the "busy" flag.
        private CHWSTOP _stop;

        public CHWForm(
            Document doc,
            Autodesk.Revit.ApplicationServices.Application app)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _doc = doc;
            _app = app;

            // _doc is null when the command was started with no model
            // open, which is allowed: ACC models are still measurable
            this.Text = doc == null
                ? "CHW VOLUME - no model open"
                : "CHW VOLUME - " + doc.Title;

            HookGrid(dgvLocal);
            HookGrid(dgvCloud);

            colCloudWorksets.Items.Add(WorksetsAll);
            colCloudWorksets.Items.Add(WorksetsEditable);
        }

        // What the report is named after. With no model open the run was
        // driven from the ACC list alone, so there is no host to name.
        private string HostTitle
        {
            get { return _doc == null ? "ACC models" : _doc.Title; }
        }

        private void HookGrid(DataGridView grid)
        {
            grid.CellValueChanged += Grid_CellValueChanged;
            grid.CurrentCellDirtyStateChanged +=
                Grid_CurrentCellDirtyStateChanged;
            grid.EditingControlShowing += Grid_EditingControlShowing;
            grid.DataError += Grid_DataError;
        }

        private void CHWForm_Load(object sender, EventArgs e)
        {
            _cloudModels = CHWCLOUD.Load();

            Rebuild();

            // The ACC tab is the interesting one when there is no model
            // open, since it holds the only measurable models
            if (_doc == null)
                tabModels.SelectedTab = tabCloud;
        }

        private void Rebuild()
        {
            List<CHWVOLUME.ModelItem> local = CHWVOLUME.GetModels(_doc);

            var cloud = new List<CHWVOLUME.ModelItem>();

            foreach (CHWCLOUD.CloudModel model in _cloudModels)
            {
                cloud.Add(new CHWVOLUME.ModelItem
                {
                    Name = model.Name,
                    Cloud = model
                });
            }

            var all = new List<CHWVOLUME.ModelItem>(local);
            all.AddRange(cloud);

            LoadSystemTypes(all);

            SeedEmptyChoices(all);

            BuildRows(dgvLocal, local);
            BuildRows(dgvCloud, cloud);

            ShowInfo(local.Count);

            UpdateActionLabels();
        }

        // ==================================================
        // The two grids
        // ==================================================

        private DataGridViewComboBoxColumn SupplyColumn(DataGridView grid)
        {
            return grid == dgvCloud ? colCloudSupply : colLocalSupply;
        }

        private DataGridViewComboBoxColumn ReturnColumn(DataGridView grid)
        {
            return grid == dgvCloud ? colCloudReturn : colLocalReturn;
        }

        private void BuildRows(
            DataGridView grid, List<CHWVOLUME.ModelItem> models)
        {
            _loading = true;

            try
            {
                grid.Rows.Clear();

                foreach (CHWVOLUME.ModelItem model in models)
                {
                    int index = grid.Rows.Add();

                    DataGridViewRow row = grid.Rows[index];

                    row.Tag = model;

                    row.Cells[ColCheck].Value = true;
                    row.Cells[ColName].Value = model.ToString();

                    EnsureItem(SupplyColumn(grid), model.SupplyType);
                    EnsureItem(ReturnColumn(grid), model.ReturnType);

                    row.Cells[ColSupply].Value = model.SupplyType;
                    row.Cells[ColReturn].Value = model.ReturnType;

                    if (grid == dgvCloud)
                    {
                        row.Cells[ColWorksets].Value =
                            model.Worksets == CHWCLOUD.WorksetMode.Editable
                                ? WorksetsEditable
                                : WorksetsAll;
                    }

                    UpdateRowLook(row);
                }
            }
            finally
            {
                _loading = false;
            }
        }

        // A combo cell refuses a value that is not in its list, so every
        // remembered or typed name has to be added first
        private static void EnsureItem(
            DataGridViewComboBoxColumn column, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (!column.Items.Contains(value))
                column.Items.Add(value);
        }

        private static void UpdateRowLook(DataGridViewRow row)
        {
            var model = row.Tag as CHWVOLUME.ModelItem;

            if (model == null)
                return;

            bool ticked = row.Cells[ColCheck].Value is bool &&
                          (bool)row.Cells[ColCheck].Value;

            bool incomplete =
                string.IsNullOrEmpty(model.SupplyType) ||
                string.IsNullOrEmpty(model.ReturnType);

            row.DefaultCellStyle.ForeColor = incomplete && ticked
                ? System.Drawing.Color.Firebrick
                : System.Drawing.SystemColors.WindowText;
        }

        // Committing as soon as a box is ticked or a name picked, rather
        // than when the cell loses focus
        private void Grid_CurrentCellDirtyStateChanged(
            object sender, EventArgs e)
        {
            var grid = sender as DataGridView;

            if (grid != null && grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Grid_CellValueChanged(
            object sender, DataGridViewCellEventArgs e)
        {
            var grid = sender as DataGridView;

            if (_loading || grid == null || e.RowIndex < 0)
                return;

            DataGridViewRow row = grid.Rows[e.RowIndex];

            var model = row.Tag as CHWVOLUME.ModelItem;

            if (model == null)
                return;

            if (e.ColumnIndex == ColSupply)
                model.SupplyType = ValueOf(row, ColSupply);

            if (e.ColumnIndex == ColReturn)
                model.ReturnType = ValueOf(row, ColReturn);

            if (e.ColumnIndex == ColWorksets && model.IsCloud)
                WorksetsChanged(model, ValueOf(row, ColWorksets));

            UpdateRowLook(row);

            if (e.ColumnIndex != ColCheck)
                SaveCloudList();
        }

        private void WorksetsChanged(CHWVOLUME.ModelItem model, string value)
        {
            bool editable = value == WorksetsEditable;

            CHWCLOUD.WorksetMode wanted = editable
                ? CHWCLOUD.WorksetMode.Editable
                : CHWCLOUD.WorksetMode.All;

            if (model.Worksets == wanted)
                return;

            model.Worksets = wanted;

            if (editable)
            {
                MessageBox.Show(
                    "Only the worksets you own will be loaded, so the " +
                    "volume will count that part of the model alone." +
                    Environment.NewLine + Environment.NewLine +
                    "Use All worksets for a complete figure.",
                    "CHW VOLUME");
            }
        }

        private static string ValueOf(DataGridViewRow row, int column)
        {
            object value = row.Cells[column].Value;

            return value == null ? string.Empty : value.ToString().Trim();
        }

        // Lets a system type be typed as well as picked, for a name that
        // only exists inside a model the add-in has not opened
        private void Grid_EditingControlShowing(
            object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            var grid = sender as DataGridView;
            var combo = e.Control as ComboBox;

            if (grid == null || combo == null || grid.CurrentCell == null)
                return;

            int column = grid.CurrentCell.ColumnIndex;

            // The workset choice is a fixed pair; only the two type
            // columns accept anything typed
            bool typeColumn = column == ColSupply || column == ColReturn;

            combo.DropDownStyle = typeColumn
                ? ComboBoxStyle.DropDown
                : ComboBoxStyle.DropDownList;

            combo.Validating -= TypedNameValidating;

            if (typeColumn)
                combo.Validating += TypedNameValidating;
        }

        private void TypedNameValidating(object sender, CancelEventArgs e)
        {
            var combo = sender as ComboBox;

            if (combo == null)
                return;

            DataGridView grid = ActiveGrid();

            if (grid == null || grid.CurrentCell == null)
                return;

            string typed = combo.Text.Trim();

            if (typed.Length == 0)
                return;

            var column =
                grid.Columns[grid.CurrentCell.ColumnIndex]
                as DataGridViewComboBoxColumn;

            // Adding it to the list is what makes the cell accept it
            EnsureItem(column, typed);

            if (!combo.Items.Contains(typed))
                combo.Items.Add(typed);
        }

        private void Grid_DataError(
            object sender, DataGridViewDataErrorEventArgs e)
        {
            // A value not yet in a combo list would otherwise throw at
            // the user; the validating handler puts it there instead
            e.ThrowException = false;
        }

        private DataGridView ActiveGrid()
        {
            return tabModels.SelectedTab == tabCloud ? dgvCloud : dgvLocal;
        }

        // ==================================================
        // Selection
        // ==================================================

        // Rows the user has picked out, in the order they appear. An
        // action works on these when there are two or more; with one row
        // or none it works on the whole tab, which is what the button
        // labels say at the time.
        private static List<DataGridViewRow> SelectedRows(DataGridView grid)
        {
            var rows = new List<DataGridViewRow>();

            foreach (DataGridViewRow row in grid.SelectedRows)
                rows.Add(row);

            rows.Sort((a, b) => a.Index.CompareTo(b.Index));

            return rows;
        }

        private static bool HasMultiSelection(DataGridView grid)
        {
            return grid.SelectedRows.Count > 1;
        }

        // The rows an action should touch: the selection when there is a
        // real one, otherwise everything on the tab
        private static List<DataGridViewRow> TargetRows(DataGridView grid)
        {
            if (HasMultiSelection(grid))
                return SelectedRows(grid);

            var all = new List<DataGridViewRow>();

            foreach (DataGridViewRow row in grid.Rows)
                all.Add(row);

            return all;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateActionLabels();
        }

        // The buttons say what they will actually do, so "all" never
        // quietly means "the three you highlighted"
        private void UpdateActionLabels()
        {
            Relabel(dgvLocal, btnLocalAll, btnLocalNone);
            Relabel(dgvCloud, btnCloudAll, btnCloudNone);

            btnRemoveCloud.Text = HasMultiSelection(dgvCloud)
                ? $"Remove {dgvCloud.SelectedRows.Count} selected"
                : "Remove";
        }

        private static void Relabel(
            DataGridView grid,
            Button tick,
            Button untick)
        {
            if (HasMultiSelection(grid))
            {
                int count = grid.SelectedRows.Count;

                tick.Text = $"Tick {count} selected";
                untick.Text = $"Untick {count} selected";

                return;
            }

            tick.Text = "Tick all";
            untick.Text = "Untick all";
        }

        private static List<CHWVOLUME.ModelItem> ModelsIn(DataGridView grid)
        {
            var all = new List<CHWVOLUME.ModelItem>();

            foreach (DataGridViewRow row in grid.Rows)
            {
                var model = row.Tag as CHWVOLUME.ModelItem;

                if (model != null)
                    all.Add(model);
            }

            return all;
        }

        private static List<CHWVOLUME.ModelItem> TickedIn(DataGridView grid)
        {
            var picked = new List<CHWVOLUME.ModelItem>();

            foreach (DataGridViewRow row in grid.Rows)
            {
                var model = row.Tag as CHWVOLUME.ModelItem;

                bool ticked = row.Cells[ColCheck].Value is bool &&
                              (bool)row.Cells[ColCheck].Value;

                if (model != null && ticked)
                    picked.Add(model);
            }

            return picked;
        }

        private List<CHWVOLUME.ModelItem> AllModelItems()
        {
            var all = ModelsIn(dgvLocal);

            all.AddRange(ModelsIn(dgvCloud));

            return all;
        }

        private static DataGridViewRow CurrentRow(DataGridView grid)
        {
            if (grid.CurrentCell == null)
                return null;

            return grid.Rows[grid.CurrentCell.RowIndex];
        }

        private void ShowInfo(int localCount)
        {
            lblLocalInfo.Text = _doc == null
                ? "No model is open in Revit, so there is nothing to list " +
                  "here. The ACC models tab holds the models that can " +
                  "still be measured."
                : $"{localCount} model(s): the one you have open and every " +
                  "linked model loaded inside it. A link placed several " +
                  "times is counted once. Click a system type cell to " +
                  "pick or type the name." + Environment.NewLine +
                  "Select several rows with Ctrl or Shift and the buttons " +
                  "act on just those.";

            lblCloudInfo.Text =
                $"{_cloudModels.Count} saved ACC model(s). Each is opened " +
                "in the background for the measurement and closed again, " +
                "never shown in the Revit window." +
                Environment.NewLine +
                "Their system types cannot be read until the model has " +
                "been opened once, so type a name in or press Read type " +
                "names. Links inside an ACC model are not loaded - add " +
                "each one here separately if you want its water too." +
                Environment.NewLine +
                "Select several rows with Ctrl or Shift and the buttons " +
                "act on just those.";
        }

        // ==================================================
        // System type names
        // ==================================================

        private void LoadSystemTypes(List<CHWVOLUME.ModelItem> models)
        {
            _typeNames = CHWVOLUME.GetSystemTypeNames(_doc, models);

            var columns = new DataGridViewComboBoxColumn[]
            {
                colLocalSupply, colLocalReturn,
                colCloudSupply, colCloudReturn
            };

            foreach (DataGridViewComboBoxColumn column in columns)
            {
                column.Items.Clear();

                foreach (string name in _typeNames)
                    column.Items.Add(name);
            }

            // Anything already chosen has to stay selectable, even if the
            // model it came from is not open to list it
            foreach (CHWVOLUME.ModelItem model in models)
            {
                if (model.IsCloud)
                {
                    EnsureItem(colCloudSupply, model.SupplyType);
                    EnsureItem(colCloudReturn, model.ReturnType);
                }
                else
                {
                    EnsureItem(colLocalSupply, model.SupplyType);
                    EnsureItem(colLocalReturn, model.ReturnType);
                }
            }

            if (_typeNames.Count == 0 && _doc != null)
            {
                // The list is built from pipework actually drawn, so an
                // empty one means no pipes, not a missing setting
                MessageBox.Show(
                    "No pipes or pipe fittings were found in these " +
                    "models, so there are no system types to list. You " +
                    "can still type a name in.",
                    "CHW VOLUME");
            }
        }

        // A first guess for any model with nothing chosen yet, so the
        // common case of one naming convention needs no typing
        private void SeedEmptyChoices(List<CHWVOLUME.ModelItem> models)
        {
            if (_typeNames.Count == 0)
                return;

            string supply = _typeNames[Guess(_typeNames, "supply", 0)];
            string returnType = _typeNames[Guess(
                _typeNames, "return", Math.Min(1, _typeNames.Count - 1))];

            foreach (CHWVOLUME.ModelItem model in models)
            {
                if (string.IsNullOrEmpty(model.SupplyType))
                    model.SupplyType = supply;

                if (string.IsNullOrEmpty(model.ReturnType))
                    model.ReturnType = returnType;
            }
        }

        // Prefers a chilled water name, then any name with the word
        private static int Guess(List<string> names, string word, int fallback)
        {
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i].ToLowerInvariant();

                bool chilled = name.Contains("chilled") ||
                               name.Contains("chw");

                if (chilled && name.Contains(word))
                    return i;
            }

            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].ToLowerInvariant().Contains(word))
                    return i;
            }

            return fallback;
        }

        private void RefreshCells(DataGridView grid)
        {
            _loading = true;

            try
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    var model = row.Tag as CHWVOLUME.ModelItem;

                    if (model == null)
                        continue;

                    EnsureItem(SupplyColumn(grid), model.SupplyType);
                    EnsureItem(ReturnColumn(grid), model.ReturnType);

                    row.Cells[ColSupply].Value = model.SupplyType;
                    row.Cells[ColReturn].Value = model.ReturnType;

                    if (grid == dgvCloud)
                    {
                        row.Cells[ColWorksets].Value =
                            model.Worksets == CHWCLOUD.WorksetMode.Editable
                                ? WorksetsEditable
                                : WorksetsAll;
                    }

                    UpdateRowLook(row);
                }
            }
            finally
            {
                _loading = false;
            }
        }

        // Fills the lists for ACC models. Their types cannot be read
        // while they are closed, so each is opened once in the background
        // and the names are then remembered for good.
        private void btnReadTypes_Click(object sender, EventArgs e)
        {
            if (_stop != null)
                return;

            dgvCloud.EndEdit();

            List<CHWVOLUME.ModelItem> cloud = TickedIn(dgvCloud);

            if (cloud.Count == 0)
            {
                MessageBox.Show(
                    "Tick the ACC models whose system types you want " +
                    "listed.",
                    "CHW VOLUME");

                return;
            }

            BeginRun("Stop reading");

            string errors;
            int read;

            try
            {
                read = CHWVOLUME.ReadCloudSystemTypes(
                    _app, cloud, _stop, out errors);
            }
            finally
            {
                EndRun("2.  Calculate volume  (both tabs)");
            }

            SaveCloudList();

            LoadSystemTypes(AllModelItems());

            RefreshCells(dgvLocal);
            RefreshCells(dgvCloud);

            if (!string.IsNullOrEmpty(errors))
            {
                MessageBox.Show(
                    $"Read {read} of {cloud.Count} model(s)." +
                    Environment.NewLine + Environment.NewLine + errors,
                    "CHW VOLUME");
            }
        }

        // ==================================================
        // ACC models
        // ==================================================

        private void btnAddCloud_Click(object sender, EventArgs e)
        {
            // Starts from the models Revit has opened here before, so the
            // ids are picked rather than typed; the dialog offers typing
            // them for a model that has never been opened on this machine
            using (CHWCACHEForm dialog =
                   new CHWCACHEForm(_doc, _cloudModels))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK ||
                    dialog.Results.Count == 0)
                    return;

                _cloudModels.AddRange(dialog.Results);

                SaveCloudList();

                Rebuild();
            }
        }

        private void btnRemoveCloud_Click(object sender, EventArgs e)
        {
            // Removing never falls back to "all": an empty selection here
            // would otherwise wipe the whole list
            List<DataGridViewRow> rows = SelectedRows(dgvCloud);

            if (rows.Count == 0)
            {
                DataGridViewRow current = CurrentRow(dgvCloud);

                if (current != null)
                    rows.Add(current);
            }

            var going = new List<CHWCLOUD.CloudModel>();

            foreach (DataGridViewRow row in rows)
            {
                var model = row.Tag as CHWVOLUME.ModelItem;

                if (model != null && model.IsCloud)
                    going.Add(model.Cloud);
            }

            if (going.Count == 0)
            {
                MessageBox.Show(
                    "Select the ACC models to remove.",
                    "CHW VOLUME");

                return;
            }

            if (going.Count > 1)
            {
                var names = new System.Text.StringBuilder();

                foreach (CHWCLOUD.CloudModel model in going)
                    names.AppendLine("   " + model.Name);

                DialogResult sure = MessageBox.Show(
                    $"Remove these {going.Count} ACC models from the list?" +
                    Environment.NewLine + Environment.NewLine + names +
                    Environment.NewLine +
                    "Nothing is deleted from ACC itself.",
                    "CHW VOLUME",
                    MessageBoxButtons.YesNo);

                if (sure != DialogResult.Yes)
                    return;
            }

            foreach (CHWCLOUD.CloudModel model in going)
                _cloudModels.Remove(model);

            SaveCloudList();

            Rebuild();
        }

        private void SaveCloudList()
        {
            try
            {
                CHWCLOUD.Save(_cloudModels);
            }
            catch
            {
                // the list is still correct for this session
            }
        }

        // ==================================================
        // Ticking and calculating
        // ==================================================

        private void btnLocalAll_Click(object sender, EventArgs e)
        {
            SetTicks(dgvLocal, true);
        }

        private void btnLocalNone_Click(object sender, EventArgs e)
        {
            SetTicks(dgvLocal, false);
        }

        private void btnCloudAll_Click(object sender, EventArgs e)
        {
            SetTicks(dgvCloud, true);
        }

        private void btnCloudNone_Click(object sender, EventArgs e)
        {
            SetTicks(dgvCloud, false);
        }

        private static void SetTicks(DataGridView grid, bool ticked)
        {
            grid.EndEdit();

            foreach (DataGridViewRow row in TargetRows(grid))
            {
                row.Cells[ColCheck].Value = ticked;

                UpdateRowLook(row);
            }
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            // While a run is going the same button is the Stop button
            if (_stop != null)
            {
                _stop.Stop();

                btnCalculate.Text = "Stopping...";
                btnCalculate.Enabled = false;

                return;
            }

            dgvLocal.EndEdit();
            dgvCloud.EndEdit();

            // Both tabs at once: they are one measurement, split in two
            // only for the sake of managing them
            List<CHWVOLUME.ModelItem> models = TickedIn(dgvLocal);

            models.AddRange(TickedIn(dgvCloud));

            if (models.Count == 0)
            {
                MessageBox.Show(
                    "Tick at least one model, on either tab.",
                    "CHW VOLUME");

                return;
            }

            if (!TypesAreUsable(models))
                return;

            BeginRun("Stop measuring");

            try
            {
                _lastResult = CHWVOLUME.Calculate(_doc, _app, models, _stop);

                ShowResult(_lastResult);

                btnExport.Enabled = true;

                // Opening an ACC model works out which region it lives in;
                // saving here means the next run goes straight to it
                SaveCloudList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not calculate the volume:" +
                    Environment.NewLine + Environment.NewLine + ex.Message,
                    "CHW VOLUME");
            }
            finally
            {
                EndRun("2.  Calculate volume  (both tabs)");
            }
        }

        // ==================================================
        // Running, and stopping
        // ==================================================

        // Everything but Stop is switched off for the duration, so the
        // message pump we open up between models cannot be used to
        // re-enter the run or close the window under it.
        private void BeginRun(string stopCaption)
        {
            _stop = new CHWSTOP
            {
                Pump = Application.DoEvents
            };

            tabModels.Enabled = false;
            btnExport.Enabled = false;
            btnHelp.Enabled = false;
            btnClose.Enabled = false;

            btnCalculate.Text = stopCaption;
            btnCalculate.Enabled = true;

            this.Cursor = Cursors.WaitCursor;
        }

        private void EndRun(string caption)
        {
            _stop = null;

            tabModels.Enabled = true;
            btnHelp.Enabled = true;
            btnClose.Enabled = true;

            btnExport.Enabled = _lastResult != null;

            btnCalculate.Text = caption;
            btnCalculate.Enabled = true;

            this.Cursor = Cursors.Default;
        }

        private void CHWForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_stop == null)
                return;

            // Closing mid-run would leave a background document open
            e.Cancel = true;

            MessageBox.Show(
                "A measurement is still running. Press Stop and wait for " +
                "it to finish before closing.",
                "CHW VOLUME");
        }

        private bool TypesAreUsable(List<CHWVOLUME.ModelItem> models)
        {
            foreach (CHWVOLUME.ModelItem model in models)
            {
                string where = model.IsCloud
                    ? "ACC models"
                    : "Open in Revit";

                if (string.IsNullOrEmpty(model.SupplyType) ||
                    string.IsNullOrEmpty(model.ReturnType))
                {
                    MessageBox.Show(
                        "This model has no supply or return system type " +
                        "set:" + Environment.NewLine +
                        Environment.NewLine + model +
                        Environment.NewLine + Environment.NewLine +
                        $"Fill both cells on the {where} tab, or untick it.",
                        "CHW VOLUME");

                    return false;
                }

                if (string.Equals(model.SupplyType, model.ReturnType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "Supply and return are the same system type for:" +
                        Environment.NewLine + Environment.NewLine + model +
                        Environment.NewLine + Environment.NewLine +
                        "The two cannot be told apart that way.",
                        "CHW VOLUME");

                    return false;
                }
            }

            return true;
        }

        // ==================================================
        // Results
        // ==================================================

        private void ShowResult(CHWVOLUME.Result result)
        {
            lvResults.Items.Clear();

            foreach (CHWVOLUME.ModelResult model in result.Models)
            {
                if (!string.IsNullOrEmpty(model.Failure))
                {
                    // An ACC model that would not open is shown as such,
                    // not as a row of zeroes
                    var failed = new ListViewItem(
                        model.ModelName + "  -  could not open: " +
                        model.Failure);

                    failed.ForeColor = System.Drawing.Color.Firebrick;

                    lvResults.Items.Add(failed);

                    continue;
                }

                string name = string.IsNullOrEmpty(model.WorksetsNote)
                    ? model.ModelName
                    : model.ModelName + "  (" + model.WorksetsNote + ")";

                var row = new ListViewItem(name);

                row.SubItems.Add(model.SupplyTypeName);
                row.SubItems.Add(model.ReturnTypeName);
                row.SubItems.Add(
                    (model.SupplyLitres + model.SupplyFittingLitres).ToString("N1"));
                row.SubItems.Add(
                    (model.ReturnLitres + model.ReturnFittingLitres).ToString("N1"));
                row.SubItems.Add(model.TotalPipes.ToString("N0"));
                row.SubItems.Add(model.TotalFittings.ToString("N0"));
                row.SubItems.Add(model.TotalLitres.ToString("N1"));

                // A partly loaded model is flagged: its figure is not the
                // whole model
                if (!string.IsNullOrEmpty(model.WorksetsNote))
                    row.ForeColor = System.Drawing.Color.DarkGoldenrod;

                lvResults.Items.Add(row);
            }

            var totalRow = new ListViewItem("TOTAL");

            totalRow.SubItems.Add(string.Empty);
            totalRow.SubItems.Add(string.Empty);
            totalRow.SubItems.Add(
                (result.SupplyLitres + result.SupplyFittingLitres).ToString("N1"));
            totalRow.SubItems.Add(
                (result.ReturnLitres + result.ReturnFittingLitres).ToString("N1"));
            totalRow.SubItems.Add(result.TotalPipes.ToString("N0"));
            totalRow.SubItems.Add(result.TotalFittings.ToString("N0"));
            totalRow.SubItems.Add(result.TotalLitres.ToString("N1"));
            totalRow.Font = new System.Drawing.Font(
                lvResults.Font,
                System.Drawing.FontStyle.Bold);

            lvResults.Items.Add(totalRow);

            lblTotal.ForeColor = result.Stopped
                ? System.Drawing.Color.Firebrick
                : System.Drawing.SystemColors.ControlText;

            string skipped = result.SkippedNoSize == 0
                ? string.Empty
                : Environment.NewLine +
                  $"{result.SkippedNoSize} pipe(s) or fitting(s) skipped - " +
                  "size could not be read.";

            // A stopped run is a partial figure and has to say so
            string stopped = !result.Stopped
                ? string.Empty
                : Environment.NewLine +
                  $"STOPPED - {result.NotMeasured} model(s) were not " +
                  "measured, so this total is incomplete.";

            lblTotal.Text =
                $"Total chilled water volume:  " +
                $"{result.TotalLitres:N1} litres   " +
                $"({result.TotalCubicMetres:N3} m3)" +
                Environment.NewLine +
                $"Pipes {result.PipeLitres:N1} L from {result.TotalPipes:N0}, " +
                $"fittings {result.FittingLitres:N1} L from {result.TotalFittings:N0}." +
                skipped + stopped;
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (_lastResult == null)
                return;

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                saveFileDialog.FileName = CHWEXPORT.SuggestedFileName(HostTitle);
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.OverwritePrompt = true;

                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    CHWEXPORT.Write(
                        saveFileDialog.FileName,
                        HostTitle,
                        _lastResult);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Could not write the file:" + Environment.NewLine +
                        Environment.NewLine + ex.Message +
                        Environment.NewLine + Environment.NewLine +
                        "If it is already open in Excel, close it and try again.",
                        "CHW VOLUME");

                    return;
                }

                DialogResult open = MessageBox.Show(
                    "Saved to:" + Environment.NewLine +
                    saveFileDialog.FileName + Environment.NewLine +
                    Environment.NewLine + "Open it now?",
                    "CHW VOLUME",
                    MessageBoxButtons.YesNo);

                if (open == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(saveFileDialog.FileName);
                    }
                    catch
                    {
                        // the file is written either way
                    }
                }
            }
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            using (CHWHELPForm help = new CHWHELPForm())
            {
                help.ShowDialog(this);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
