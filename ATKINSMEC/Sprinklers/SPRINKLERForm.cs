using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ATKINSMEC
{
    public partial class SPRINKLERForm : System.Windows.Forms.Form
    {
        private readonly Document _doc;

        private List<SPRINKLERPLAN.RoomPlan> _plans =
            new List<SPRINKLERPLAN.RoomPlan>();

        private List<FamilySymbol> _symbols = new List<FamilySymbol>();

        private bool _loading;

        private readonly SPRINKLEREVENT _handler;
        private readonly Autodesk.Revit.UI.ExternalEvent _event;

        public SPRINKLERForm(
            Document doc,
            SPRINKLEREVENT handler,
            Autodesk.Revit.UI.ExternalEvent externalEvent)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _doc = doc;
            _handler = handler;
            _event = externalEvent;

            this.Text = "SPRINKLERS - " + doc.Title;

            // READ HERE, inside the command's API context. Load would
            // happen to work too - WinForms raises it inside Show() - but
            // that is an accident of timing, not a promise.
            _links = RVTLINKSELECTION.GetLinks(doc);
            _symbols = SPRINKLERPLAN.GetSprinklerSymbols(doc);

            // What the window says about each family, read now - the
            // family list and the Place confirmation used to ask the
            // FamilySymbol from their own handlers
            foreach (FamilySymbol symbol in _symbols)
            {
                _facts.Add(new SymbolFacts
                {
                    Label = symbol.FamilyName + " : " + symbol.Name,
                    Placement = SPRINKLERPLAN.PlacementDescription(symbol),
                    NeedsHost = SPRINKLERPLAN.NeedsHost(symbol)
                });
            }
        }

        // One per _symbols entry, in the same order
        private class SymbolFacts
        {
            public string Label;
            public string Placement;
            public bool NeedsHost;
        }

        private readonly List<SymbolFacts> _facts = new List<SymbolFacts>();

        // The model the window was opened on, for SPRINKLEREVENT's reads
        public Document Document
        {
            get { return _doc; }
        }

        private readonly List<RVTLINKSELECTION.LinkItem> _links;

        // EACH LINK'S ROOM LEVELS, read once through the external event
        // (SPRINKLEREVENT.ReadLevels) - never from the window's own
        // handlers, which are outside any API context
        private readonly Dictionary<long, List<string>> _levelsOf =
            new Dictionary<long, List<string>>();

        private const string ReadingLevels = "Reading the link's floors...";

        // SPRINKLEREVENT's answer, on Revit's thread - the window's too
        public void ReportLevels(Dictionary<long, List<string>> read)
        {
            foreach (KeyValuePair<long, List<string>> link in read)
                _levelsOf[link.Key] = link.Value ?? new List<string>();

            if (lblSourceInfo.Text == ReadingLevels)
                lblSourceInfo.Text = string.Empty;

            RefreshLevels();
        }

        private void SPRINKLERForm_FormClosed(
            object sender,
            FormClosedEventArgs e)
        {
            SPRINKLERCommand.Form = null;
        }

        // ==================================================
        // Called back from the external event
        // ==================================================

        public void ReportRooms(List<SPRINKLERPLAN.RoomPlan> plans)
        {
            _plans = plans ?? new List<SPRINKLERPLAN.RoomPlan>();

            FillGrid();

            btnPlace.Enabled = _plans.Count > 0 && _symbols.Count > 0;
            btnLoadRooms.Enabled = true;
            this.Cursor = Cursors.Default;

            int unrecognised = _plans.Count(p => !p.HazardRecognised);

            lblSourceInfo.Text =
                $"{_plans.Count} room(s) listed." +
                (unrecognised > 0
                    ? Environment.NewLine +
                      $"{unrecognised} name(s) not recognised - " +
                      "check their hazard."
                    : string.Empty);
        }

        public void ReportPlaced(int placed, int hosted, int failed, string note)
        {
            this.Cursor = Cursors.Default;
            btnPlace.Enabled = true;
            MessageBox.Show(
                $"Placed {placed} sprinkler(s)" +
                (hosted > 0 ? $", {hosted} hosted on a ceiling." : ".") +
                (failed > 0
                    ? Environment.NewLine +
                      $"{failed} could not be placed."
                    : string.Empty) +
                (string.IsNullOrEmpty(note)
                    ? string.Empty
                    : Environment.NewLine + Environment.NewLine + note),
                "SPRINKLERS");
        }

        public void ReportError(string message)
        {
            this.Cursor = Cursors.Default;
            btnLoadRooms.Enabled = true;
            btnPlace.Enabled = _plans.Count > 0 && _symbols.Count > 0;

            MessageBox.Show(message, "SPRINKLERS");
        }

        private void SPRINKLERForm_Load(object sender, EventArgs e)
        {
            foreach (RVTLINKSELECTION.LinkItem link in _links)
                clbLinks.Items.Add(link);

            foreach (SPRINKLERRULES.HazardRule rule in SPRINKLERRULES.Rules)
            {
                colHazard.Items.Add(rule.Name);
                cmbSetAll.Items.Add(rule.Name);
            }

            cmbSetAll.SelectedIndex = 0;

            foreach (SymbolFacts facts in _facts)
                cmbFamily.Items.Add(facts.Label);

            cmbHosting.Items.Add("Automatic (from the family)");
            cmbHosting.Items.Add("Host on the ceiling face");
            cmbHosting.Items.Add("Do not host");
            cmbHosting.SelectedIndex = 0;

            if (_symbols.Count > 0)
            {
                cmbFamily.SelectedIndex = 0;
            }
            else
            {
                MessageBox.Show(
                    "This model has no sprinkler families loaded, so nothing " +
                    "can be placed.\r\n\r\nLoad a pendent concealed sprinkler " +
                    "family first; the layout can still be reviewed.",
                    "SPRINKLERS");
            }

            UpdateMode();
        }

        private int Col(string name)
        {
            return dgvRooms.Columns[name].Index;
        }

        // ==================================================
        // Which rooms take part
        // ==================================================

        private bool IsChecked(DataGridViewRow row)
        {
            object value = row.Cells[Col("colUse")].Value;

            return value != null && Convert.ToBoolean(value);
        }

        private List<SPRINKLERPLAN.RoomPlan> CheckedPlans()
        {
            var plans = new List<SPRINKLERPLAN.RoomPlan>();

            foreach (DataGridViewRow row in dgvRooms.Rows)
            {
                var plan = row.Tag as SPRINKLERPLAN.RoomPlan;

                if (plan != null && IsChecked(row))
                    plans.Add(plan);
            }

            return plans;
        }

        private void SetAllChecked(bool value)
        {
            _loading = true;

            foreach (DataGridViewRow row in dgvRooms.Rows)
                row.Cells[Col("colUse")].Value = value;

            _loading = false;

            UpdateTotals();
        }

        private void btnAllRooms_Click(object sender, EventArgs e)
        {
            SetAllChecked(true);
        }

        private void btnNoRooms_Click(object sender, EventArgs e)
        {
            SetAllChecked(false);
        }

        // Rows highlighted in the grid, however they were selected
        private List<DataGridViewRow> SelectedRows()
        {
            var rows = new List<DataGridViewRow>();

            foreach (DataGridViewRow row in dgvRooms.SelectedRows)
                rows.Add(row);

            // Selecting single cells still counts as picking their rows
            if (rows.Count == 0)
            {
                foreach (DataGridViewCell cell in dgvRooms.SelectedCells)
                {
                    if (!rows.Contains(cell.OwningRow))
                        rows.Add(cell.OwningRow);
                }
            }

            return rows;
        }

        private List<SPRINKLERPLAN.RoomPlan> SelectedPlans()
        {
            var plans = new List<SPRINKLERPLAN.RoomPlan>();

            foreach (DataGridViewRow row in SelectedRows())
            {
                var plan = row.Tag as SPRINKLERPLAN.RoomPlan;

                if (plan != null && !plans.Contains(plan))
                    plans.Add(plan);
            }

            return plans;
        }

        private void SetSelectedChecked(bool value)
        {
            List<DataGridViewRow> rows = SelectedRows();

            if (rows.Count == 0)
            {
                MessageBox.Show(
                    "Select the rows first - click a row, then shift-click " +
                    "or ctrl-click to add more.",
                    "SPRINKLERS");

                return;
            }

            _loading = true;

            foreach (DataGridViewRow row in rows)
                row.Cells[Col("colUse")].Value = value;

            _loading = false;

            UpdateTotals();
        }

        private void btnCheckSel_Click(object sender, EventArgs e)
        {
            SetSelectedChecked(true);
        }

        private void btnUncheckSel_Click(object sender, EventArgs e)
        {
            SetSelectedChecked(false);
        }

        private void dgvRooms_SelectionChanged(object sender, EventArgs e)
        {
            UpdateSetAllLabel();
        }

        // Says plainly what the hazard Apply button will act on
        private void UpdateSetAllLabel()
        {
            int selected = SelectedRows().Count;

            lblSetAll.Text = selected > 0
                ? $"Apply to {selected} selected:"
                : "Apply to checked rooms:";
        }

        private void cmbFamily_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePlacementInfo();
        }

        private void UpdatePlacementInfo()
        {
            if (cmbFamily.SelectedIndex < 0 ||
                cmbFamily.SelectedIndex >= _symbols.Count)
            {
                lblPlacementInfo.Text = string.Empty;
                return;
            }

            SymbolFacts facts = _facts[cmbFamily.SelectedIndex];

            string text = "This family is " + facts.Placement + ".";

            if (facts.NeedsHost)
                text += " It will not place without a ceiling.";

            lblPlacementInfo.Text = text;
        }

        private SPRINKLERPLAN.Hosting SelectedHosting()
        {
            switch (cmbHosting.SelectedIndex)
            {
                case 1: return SPRINKLERPLAN.Hosting.CeilingFace;
                case 2: return SPRINKLERPLAN.Hosting.NonHosted;
                default: return SPRINKLERPLAN.Hosting.Automatic;
            }
        }

        // ==================================================
        // Source selection
        // ==================================================

        private void clbLinks_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            this.BeginInvoke(new Action(RefreshLevels));
        }

        private List<long> CheckedLinkIds()
        {
            var ids = new List<long>();

            foreach (object item in clbLinks.CheckedItems)
            {
                var link = item as RVTLINKSELECTION.LinkItem;

                if (link != null)
                    ids.Add(link.Id);
            }

            return ids;
        }

        // The ticked links' floors that have rooms, from what was read.
        // A link not read yet is asked for, and the list is rebuilt when
        // its floors arrive (ReportLevels).
        private void RefreshLevels()
        {
            List<long> ids = CheckedLinkIds();

            List<long> missing = ids.Where(id => !_levelsOf.ContainsKey(id)).ToList();

            if (missing.Count > 0)
            {
                lblSourceInfo.Text = ReadingLevels;

                _handler.ReadLevels(missing);
                _event.Raise();

                return;
            }

            clbLevels.Items.Clear();

            // The same list one read of all of them gives: each floor
            // once, sorted by name
            foreach (string level in ids
                         .SelectMany(id => _levelsOf[id])
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                clbLevels.Items.Add(level, true);
            }
        }

        private HashSet<string> CheckedLevels()
        {
            var levels =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (object item in clbLevels.CheckedItems)
                levels.Add(Convert.ToString(item).Trim());

            return levels;
        }

        // ==================================================
        // Listing the rooms
        // ==================================================

        private void btnLoadRooms_Click(object sender, EventArgs e)
        {
            List<long> ids = CheckedLinkIds();

            if (ids.Count == 0)
            {
                MessageBox.Show(
                    "Check the linked architectural model first.",
                    "SPRINKLERS");

                return;
            }

            HashSet<string> levels = CheckedLevels();

            if (levels.Count == 0)
            {
                MessageBox.Show(
                    "Check at least one floor.",
                    "SPRINKLERS");

                return;
            }

            this.Cursor = Cursors.WaitCursor;
            btnLoadRooms.Enabled = false;
            lblSourceInfo.Text = "Reading rooms...";

            SPRINKLERRULES.WallClearanceMm = WallClearance();

            _handler.Action = SPRINKLEREVENT.Request.ListRooms;
            _handler.LinkIds = ids;
            _handler.Levels = levels;
            _handler.FallbackCeilingM = FallbackCeiling();

            _event.Raise();
        }

        // Brings the chosen room into view in Revit
        private void dgvRooms_CellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex != dgvRooms.Columns["colShow"].Index)
                return;

            var plan = dgvRooms.Rows[e.RowIndex].Tag
                as SPRINKLERPLAN.RoomPlan;

            if (plan == null || plan.BoxMin == null)
                return;

            _handler.Action = SPRINKLEREVENT.Request.Zoom;
            _handler.BoxMin = plan.BoxMin;
            _handler.BoxMax = plan.BoxMax;
            _handler.ZoomLevelName = plan.LevelName;

            _event.Raise();
        }

        private double WallClearance()
        {
            double value;

            if (double.TryParse(
                    txtClearance.Text.Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out value) &&
                value >= 0)
                return value;

            return 150;
        }

        private double FallbackCeiling()
        {
            double value;

            if (double.TryParse(
                    txtFallback.Text.Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out value) &&
                value > 0)
                return value;

            return 2.7;
        }

        private void FillGrid()
        {
            FillGrid(null);
        }

        // keepChecked = the room keys to tick; null ticks every room
        private void FillGrid(HashSet<string> keepChecked)
        {
            _loading = true;

            dgvRooms.Rows.Clear();

            foreach (SPRINKLERPLAN.RoomPlan plan in _plans)
            {
                bool use = keepChecked == null ||
                           keepChecked.Contains(plan.Key);

                int row = dgvRooms.Rows.Add(
                    use,
                    plan.LevelName,
                    plan.RoomNumber,
                    plan.RoomName,
                    plan.AreaM2.ToString("N1"),
                    plan.FreeAreaM2.ToString("N1"),
                    SPRINKLERRULES.RuleFor(plan.Hazard).Name,
                    plan.SprinklerCount.ToString(),
                    $"{plan.SpacingXM:N2} x {plan.SpacingYM:N2}",
                    plan.CoveragePerSprinklerM2.ToString("N1"),
                    plan.Coverage.ToString("P0"),
                    plan.CeilingHeightM.ToString("N2"),
                    plan.Note);

                dgvRooms.Rows[row].Tag = plan;

                dgvRooms.Rows[row].Cells[Col("colNote")].ToolTipText = NoteTip(plan.Note);

                if (!plan.HazardRecognised)
                {
                    dgvRooms.Rows[row].Cells[Col("colHazard")].Style.BackColor =
                        System.Drawing.Color.LightGoldenrodYellow;
                }

                if (plan.Coverage < 0.99 && plan.SprinklerCount > 0)
                {
                    dgvRooms.Rows[row].Cells[Col("colCoverage")].Style.BackColor =
                        System.Drawing.Color.MistyRose;
                }

                // Obstacles taking more than a tenth of the room shifts
                // where the heads end up, so make that visible
                if (plan.AreaM2 > 0 &&
                    plan.FreeAreaM2 < plan.AreaM2 * 0.9)
                {
                    dgvRooms.Rows[row].Cells[Col("colFree")].Style.BackColor =
                        System.Drawing.Color.MistyRose;

                    dgvRooms.Rows[row].Cells[Col("colFree")].ToolTipText =
                        $"{plan.WallObstacles} wall obstacle(s) applied";
                }
            }

            _loading = false;

            UpdateTotals();
            UpdateSetAllLabel();
        }

        // THE NOTES, one per line and each with what it means. The cell
        // wraps and the column takes the window's spare width, but four
        // notes and their reasons are more than any cell has room for -
        // it used to be a fixed 140 px that cut them off mid-word.
        private static string NoteTip(string note)
        {
            if (string.IsNullOrEmpty(note))
                return string.Empty;

            var lines = new List<string>();

            foreach (string part in note.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
            {
                string why =
                    part.EndsWith("pulled into the room")
                        ? "its grid point fell outside the room, or in a column or wall, so it went to the nearest clear spot of its cell"
                    : part.EndsWith("set off the wall")
                        ? "it was nearer a wall than the clearance allows, so it was moved out"
                    : part.EndsWith("added to close gaps")
                        ? "the grid left part of the room out of reach, so heads were added there"
                    : part.Contains("dropped")
                        ? "its cell had no clear spot left for it"
                    : part == "no ceiling found"
                        ? "no ceiling over this room in the linked model - heads go at the fallback height below"
                    : null;

                lines.Add(why == null ? part : part + " - " + why);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void UpdateTotals()
        {
            List<SPRINKLERPLAN.RoomPlan> chosen = CheckedPlans();

            int heads = chosen.Sum(p => p.SprinklerCount);
            double area = chosen.Sum(p => p.AreaM2);

            int gaps = chosen.Count(
                p => p.SprinklerCount > 0 && p.Coverage < 0.99);

            lblChecked.Text =
                $"{chosen.Count} of {_plans.Count} room(s) checked.";

            lblTotals.Text =
                $"Checked: {chosen.Count} room(s), {area:N0} m2, " +
                $"{heads} sprinkler(s)." +
                (gaps > 0
                    ? $"  {gaps} below 99% coverage - shown in red."
                    : string.Empty);

            btnPlace.Enabled = heads > 0 && _symbols.Count > 0;
        }

        // ==================================================
        // Hazard
        // ==================================================

        private void rdoMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateMode();
        }

        private void UpdateMode()
        {
            bool manual = rdoManual.Checked;

            colHazard.ReadOnly = !manual;
            lblSetAll.Enabled = manual;
            cmbSetAll.Enabled = manual;
            btnSetAll.Enabled = manual;

            // Grey the column out in automatic mode, so it is obvious the
            // hazard follows the room name and is not to be edited here
            colHazard.HeaderText = manual ? "Hazard" : "Hazard (auto)";

            colHazard.DisplayStyle = manual
                ? DataGridViewComboBoxDisplayStyle.ComboBox
                : DataGridViewComboBoxDisplayStyle.Nothing;

            colHazard.DefaultCellStyle.BackColor = manual
                ? System.Drawing.SystemColors.Window
                : System.Drawing.SystemColors.Control;

            colHazard.DefaultCellStyle.ForeColor = manual
                ? System.Drawing.SystemColors.ControlText
                : System.Drawing.SystemColors.GrayText;

            colHazard.ToolTipText = manual
                ? "Pick the NFPA 13 hazard for this room"
                : "Taken from the room name. Switch to \"Set manually per room\" to change it.";

            if (!manual && _plans.Count > 0)
            {
                // Back to the guess from each room's name
                foreach (SPRINKLERPLAN.RoomPlan plan in _plans)
                {
                    bool recognised;

                    plan.Hazard = SPRINKLERRULES.Guess(
                        plan.RoomName,
                        out recognised);

                    plan.HazardRecognised = recognised;

                    Recalculate(plan);
                }

                FillGrid();
            }
        }

        private void btnSetAll_Click(object sender, EventArgs e)
        {
            string name = cmbSetAll.SelectedItem as string;

            if (string.IsNullOrEmpty(name) || _plans.Count == 0)
                return;

            SPRINKLERRULES.Hazard hazard =
                SPRINKLERRULES.Rules.First(r => r.Name == name).Hazard;

            List<SPRINKLERPLAN.RoomPlan> chosen = SelectedPlans();

            if (chosen.Count == 0)
                chosen = CheckedPlans();

            if (chosen.Count == 0)
            {
                MessageBox.Show(
                    "Select some rows, or check the rooms to apply this to.",
                    "SPRINKLERS");

                return;
            }

            Cursor previous = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                var selected = new HashSet<string>(
                    chosen.Select(p => p.Key));

                foreach (SPRINKLERPLAN.RoomPlan plan in chosen)
                {
                    plan.Hazard = hazard;
                    plan.HazardRecognised = true;

                    Recalculate(plan);
                }

                FillGrid(selected);
            }
            finally
            {
                this.Cursor = previous;
            }
        }

        // Commit a combo box change as soon as it is picked
        private void dgvRooms_CurrentCellDirtyStateChanged(
            object sender,
            EventArgs e)
        {
            if (dgvRooms.IsCurrentCellDirty)
                dgvRooms.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dgvRooms_CellValueChanged(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (_loading || e.RowIndex < 0)
                return;

            if (e.ColumnIndex == Col("colUse"))
            {
                UpdateTotals();
                return;
            }

            if (e.ColumnIndex != Col("colHazard"))
                return;

            var plan = dgvRooms.Rows[e.RowIndex].Tag
                as SPRINKLERPLAN.RoomPlan;

            if (plan == null)
                return;

            string name = Convert.ToString(
                dgvRooms.Rows[e.RowIndex].Cells[Col("colHazard")].Value);

            SPRINKLERRULES.HazardRule rule =
                SPRINKLERRULES.Rules.FirstOrDefault(r => r.Name == name);

            if (rule == null)
                return;

            plan.Hazard = rule.Hazard;
            plan.HazardRecognised = true;

            Recalculate(plan);

            _loading = true;

            DataGridViewRow row = dgvRooms.Rows[e.RowIndex];

            row.Cells[Col("colCount")].Value = plan.SprinklerCount.ToString();
            row.Cells[Col("colSpacing")].Value = $"{plan.SpacingXM:N2} x {plan.SpacingYM:N2}";
            row.Cells[Col("colPerHead")].Value = plan.CoveragePerSprinklerM2.ToString("N1");
            row.Cells[Col("colCoverage")].Value = plan.Coverage.ToString("P0");
            row.Cells[Col("colNote")].Value = plan.Note;
            row.Cells[Col("colHazard")].Style.BackColor = System.Drawing.Color.Empty;

            row.Cells[Col("colCoverage")].Style.BackColor =
                plan.Coverage < 0.99 && plan.SprinklerCount > 0
                    ? System.Drawing.Color.MistyRose
                    : System.Drawing.Color.Empty;

            _loading = false;

            UpdateTotals();
        }

        private void Recalculate(SPRINKLERPLAN.RoomPlan plan)
        {
            SPRINKLERPLAN.Calculate(plan);
        }

        // ==================================================
        // Placing
        // ==================================================

        private void btnPlace_Click(object sender, EventArgs e)
        {
            if (_symbols.Count == 0 || cmbFamily.SelectedIndex < 0)
            {
                MessageBox.Show(
                    "Choose a sprinkler family first.",
                    "SPRINKLERS");

                return;
            }

            List<SPRINKLERPLAN.RoomPlan> chosen = CheckedPlans();

            if (chosen.Count == 0)
            {
                MessageBox.Show(
                    "No rooms are checked.",
                    "SPRINKLERS");

                return;
            }

            int heads = chosen.Sum(p => p.SprinklerCount);

            if (heads == 0)
            {
                MessageBox.Show(
                    "The checked rooms have no sprinklers to place.",
                    "SPRINKLERS");

                return;
            }

            FamilySymbol symbol = _symbols[cmbFamily.SelectedIndex];
            SymbolFacts facts = _facts[cmbFamily.SelectedIndex];

            SPRINKLERPLAN.Hosting hosting = SelectedHosting();

            string hostingText;

            switch (hosting)
            {
                case SPRINKLERPLAN.Hosting.CeilingFace:
                    hostingText = "hosted on the ceiling face";
                    break;
                case SPRINKLERPLAN.Hosting.NonHosted:
                    hostingText = "unhosted, at ceiling height";
                    break;
                default:
                    hostingText = facts.NeedsHost
                        ? "hosted on the ceiling face (this family needs a host)"
                        : "unhosted, at ceiling height";
                    break;
            }

            if (MessageBox.Show(
                    $"Place {heads} sprinkler(s) across {chosen.Count} room(s) " +
                    $"using {facts.Label}?\r\n\r\n" +
                    $"They will be {hostingText}, and can be removed with a " +
                    "single undo.",
                    "SPRINKLERS",
                    MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            Cursor previous = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                this.Cursor = Cursors.WaitCursor;
                btnPlace.Enabled = false;

                _handler.Action = SPRINKLEREVENT.Request.Place;
                _handler.Plans = chosen;
                _handler.Symbol = symbol;
                _handler.Hosting = hosting;
                _handler.LinkIds = CheckedLinkIds();

                _event.Raise();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not place the sprinklers:\r\n\r\n" + ex.Message,
                    "SPRINKLERS");
            }
            finally
            {
                this.Cursor = previous;
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
