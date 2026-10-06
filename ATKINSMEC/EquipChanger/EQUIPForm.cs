using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // The equipment changer's window.
    //
    // MODELESS, so the model stays usable while it is open and the
    // selection can be changed without closing anything. NOTHING HERE
    // TOUCHES THE REVIT API - it raises EQUIPEVENT and is handed plain
    // data back. Even the document's title is taken in the constructor,
    // which runs inside the command's API context; reading it later
    // would be the same trap QAForm._docTitle exists for.
    public partial class EQUIPForm : System.Windows.Forms.Form
    {
        private readonly EQUIPEVENT _handler;
        private readonly ExternalEvent _event;

        private readonly string _docTitle;

        private List<EQUIPROW> _rows = new List<EQUIPROW>();
        private List<EQUIPTYPEROW> _types = new List<EQUIPTYPEROW>();

        private bool _filling;

        public EQUIPForm(
            string docTitle, EQUIPEVENT handler, ExternalEvent raiser)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _docTitle = docTitle ?? string.Empty;
            _handler = handler;
            _event = raiser;
        }

        // ==================================================
        // Opening
        // ==================================================

        private void EQUIPForm_Load(object sender, EventArgs e)
        {
            Text = "EQUIPMENT CHANGER  -  " + _docTitle;

            SourceColumns();
            ResultColumns();

            chkResize.Checked = true;

            lblProgress.Text =
                "Select mechanical equipment or air terminals in the " +
                "model, then press Read the selection.";

            Ask(EQUIPEVENT.Request.Read);
        }

        private void SourceColumns()
        {
            dgvSources.Columns.Clear();

            Column(dgvSources, "Name", 150);
            Column(dgvSources, "Category", 130);
            Column(dgvSources, "Type now", 200);
            Column(dgvSources, "Ends", 55);
            Column(dgvSources, "What would follow it", 300);

            dgvSources.Columns[dgvSources.Columns.Count - 1].AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;
        }

        private void ResultColumns()
        {
            dgvResults.Columns.Clear();

            Column(dgvResults, "Name", 150);
            Column(dgvResults, "Type now", 180);
            Column(dgvResults, "Changed", 70);
            Column(dgvResults, "Resized", 70);
            Column(dgvResults, "What happened", 380);

            dgvResults.Columns[dgvResults.Columns.Count - 1].AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;
        }

        private static void Column(
            DataGridView grid, string header, int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            });
        }

        // ==================================================
        // Asking Revit
        // ==================================================

        // Raising is the ONLY thing a modeless form may do. Revit runs
        // the handler when it is next idle, which is also what stops
        // this interrupting somebody mid-command.
        private void Ask(EQUIPEVENT.Request what)
        {
            if (_handler == null || _event == null)
                return;

            _handler.WholeCategory = chkAnyFamily.Checked;
            _handler.ResizeDucts = chkResize.Checked;
            _handler.TypeId = Picked();

            _handler.Action = what;

            _event.Raise();
        }

        private void btnRead_Click(object sender, EventArgs e)
        {
            lblProgress.Text = "Reading the selection...";

            Ask(EQUIPEVENT.Request.Read);
        }

        private void chkAnyFamily_CheckedChanged(object sender, EventArgs e)
        {
            if (_filling)
                return;

            Ask(EQUIPEVENT.Request.Read);
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (_rows.Count == 0)
            {
                lblProgress.Text = "Nothing is in the list to look at.";

                return;
            }

            lblProgress.Text = "Following the ductwork...";

            Ask(EQUIPEVENT.Request.Look);
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            if (_rows.Count == 0)
            {
                lblProgress.Text = "Nothing is in the list to change.";

                return;
            }

            EQUIPTYPEROW type = PickedRow();

            if (type == null)
            {
                MessageBox.Show(
                    this,
                    "Choose the type to change to first.",
                    "EQUIPMENT CHANGER");

                return;
            }

            // The commonest way for this to look broken: every selected
            // unit is already that type, so the change succeeds and
            // nothing moves
            if (type.Current && AllAlready(type))
            {
                MessageBox.Show(
                    this,
                    "Everything in the list is ALREADY " + type.Label +
                    "." + Environment.NewLine + Environment.NewLine +
                    "Changing it to the type it already has would do " +
                    "nothing. Pick a different type - or tick \"Offer " +
                    "every family in the category\" if the one you want " +
                    "is in another family.",
                    "EQUIPMENT CHANGER",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            if (MessageBox.Show(
                    this,
                    Describe(type),
                    "EQUIPMENT CHANGER",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            lblProgress.Text = "Changing...";

            Working(true);

            Ask(EQUIPEVENT.Request.Apply);
        }

        private bool AllAlready(EQUIPTYPEROW type)
        {
            foreach (EQUIPROW row in _rows)
            {
                if (!row.TypeNow.EndsWith(
                        Tail(type.Label), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return _rows.Count > 0;
        }

        private static string Tail(string label)
        {
            int at = label.LastIndexOf(":", StringComparison.Ordinal);

            return at < 0 ? label : label.Substring(at + 1).Trim();
        }

        // Show the picked row in the model - worth having now that the
        // window is modeless and the model is right there behind it
        private void dgvSources_CellDoubleClick(
            object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count)
                return;

            _handler.ZoomTo = _rows[e.RowIndex].Id;

            Ask(EQUIPEVENT.Request.Zoom);
        }

        // ==================================================
        // What Revit said - all called from the event handler
        // ==================================================

        public void Picture(EQUIPPICTURE picture)
        {
            if (picture == null)
                return;

            _rows = picture.Rows;
            _types = picture.Types;

            Fill();

            _filling = true;

            cboType.Items.Clear();

            int current = -1;

            foreach (EQUIPTYPEROW type in _types)
            {
                int at = cboType.Items.Add(
                    type.Current ? type.Label + "   (the current type)"
                                 : type.Label);

                if (type.Current && current < 0)
                    current = at;
            }

            _filling = false;

            // Opening on the type it already is would be the one choice
            // that does nothing, so the first OTHER type is offered
            if (cboType.Items.Count > 0)
            {
                cboType.SelectedIndex =
                    First(current, cboType.Items.Count);
            }

            lblTypeInfo.Text = cboType.Items.Count == 0
                ? "No type was found to change to."
                : cboType.Items.Count + " type(s) to choose from" +
                  (current >= 0
                      ? ", one of which is what it already is."
                      : ".");

            btnApply.Enabled = cboType.Items.Count > 0 && _rows.Count > 0;
            btnPreview.Enabled = _rows.Count > 0;

            lblProgress.Text = picture.Said;

            Working(false);
        }

        private static int First(int current, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (i != current)
                    return i;
            }

            return 0;
        }

        public void Looked(List<EQUIPROW> rows, string said)
        {
            if (rows != null)
                _rows = rows;

            Fill();

            lblProgress.Text = said ?? string.Empty;

            Working(false);
        }

        public void Applied(EQUIPCHANGE.Result result)
        {
            Working(false);

            if (result == null)
                return;

            if (!string.IsNullOrEmpty(result.Blocked))
            {
                MessageBox.Show(
                    this,
                    result.Blocked,
                    "EQUIPMENT CHANGER",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            dgvResults.Rows.Clear();

            foreach (EQUIPCHANGE.Outcome outcome in result.Outcomes)
            {
                int index = dgvResults.Rows.Add();

                DataGridViewRow row = dgvResults.Rows[index];

                row.Cells[0].Value = outcome.Name;
                row.Cells[1].Value = outcome.Changed
                    ? outcome.NowType
                    : outcome.WasType;
                row.Cells[2].Value = outcome.Changed
                    ? "yes"
                    : (outcome.AlreadyThat ? "-" : "no");
                row.Cells[3].Value = outcome.Resized;
                row.Cells[4].Value = outcome.Changed
                    ? string.Join("   |   ", outcome.Branches.ToArray())
                    : outcome.Said;

                row.Cells[4].ToolTipText = outcome.Changed
                    ? string.Join(Environment.NewLine,
                        outcome.Branches.ToArray())
                    : outcome.Said;

                if (!outcome.Changed && !outcome.AlreadyThat)
                {
                    row.Cells[2].Style.ForeColor =
                        System.Drawing.Color.Firebrick;
                }
            }

            dgvResults.ClearSelection();

            lblSummary.Text =
                result.Changed + " changed, " + result.Refused +
                " refused, " + result.Already + " already that type.  " +
                "Every step is in " + EQUIPLOG.Where;

            MessageBox.Show(
                this,
                Told(result),
                "EQUIPMENT CHANGER",
                MessageBoxButtons.OK,
                result.Refused == 0
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);

            // The types on the units have moved on, so the list is read
            // again rather than left showing what used to be true
            Ask(EQUIPEVENT.Request.Read);
        }

        private static string Told(EQUIPCHANGE.Result result)
        {
            string said = result.Changed + " element(s) changed.";

            if (result.Already > 0)
            {
                said += Environment.NewLine + Environment.NewLine +
                        result.Already + " were ALREADY that type, so " +
                        "there was nothing to do to them.";
            }

            if (result.Refused > 0)
            {
                said += Environment.NewLine + Environment.NewLine +
                        result.Refused + " were refused and put back the " +
                        "way they were - the table says why for each one.";
            }

            return said + Environment.NewLine + Environment.NewLine +
                   "Every step is in " + EQUIPLOG.Where;
        }

        public void Trouble(string said)
        {
            Working(false);

            lblProgress.Text = string.Empty;

            MessageBox.Show(
                this,
                "It stopped on something unexpected:" +
                Environment.NewLine + Environment.NewLine + said +
                Environment.NewLine + Environment.NewLine +
                "What was tried is in " + EQUIPLOG.Where,
                "EQUIPMENT CHANGER",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        // ==================================================

        private void Fill()
        {
            _filling = true;

            dgvSources.Rows.Clear();

            foreach (EQUIPROW row in _rows)
            {
                int index = dgvSources.Rows.Add();

                DataGridViewRow line = dgvSources.Rows[index];

                line.Cells[0].Value = row.Name;
                line.Cells[1].Value = row.Category;
                line.Cells[2].Value = row.TypeNow;
                line.Cells[3].Value = row.Connectors;
                line.Cells[4].Value = row.Follows;

                line.Cells[4].ToolTipText = string.IsNullOrEmpty(row.Detail)
                    ? row.Follows
                    : row.Detail;
            }

            dgvSources.ClearSelection();

            _filling = false;

            grpWhat.Text = _rows.Count == 0
                ? "1. What is selected"
                : "1. What is selected  -  " + _rows.Count +
                  " element(s).  Double-click a row to show it in the model.";
        }

        private void Working(bool on)
        {
            btnApply.Enabled = !on && _types.Count > 0 && _rows.Count > 0;
            btnPreview.Enabled = !on && _rows.Count > 0;
            btnRead.Enabled = !on;

            Cursor = on ? Cursors.WaitCursor : Cursors.Default;
        }

        private EQUIPTYPEROW PickedRow()
        {
            int at = cboType.SelectedIndex;

            return at >= 0 && at < _types.Count ? _types[at] : null;
        }

        private long Picked()
        {
            EQUIPTYPEROW row = PickedRow();

            return row == null ? 0 : row.Id;
        }

        private string Describe(EQUIPTYPEROW type)
        {
            return "About to change " + _rows.Count +
                   " element(s) to " + type.Label + "." +
                   Environment.NewLine + Environment.NewLine +
                   (chkResize.Checked
                       ? "The ductwork on each one will be resized to " +
                         "match the new connectors, as far as the first " +
                         "reducer, tap-off, tee or air terminal - which " +
                         "are left exactly as they are."
                       : "The ductwork will be left at its current size, " +
                         "so a connector that has changed size will show " +
                         "as a mismatch.") +
                   Environment.NewLine + Environment.NewLine +
                   "Each element is done on its own: one that Revit " +
                   "refuses goes back the way it was and the rest still " +
                   "go through. THIS CHANGES THE MODEL - it is undoable " +
                   "with Ctrl+Z, as one step." +
                   Environment.NewLine + Environment.NewLine +
                   "Go ahead?";
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            using (var help = new EQUIPHELPForm())
            {
                help.ShowDialog(this);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        // A modeless form must let go of itself, or the next press of
        // the button finds a dead window in the static and does nothing
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            EQUIPCommand.Form = null;
            EQUIPCommand.Handler = null;
            EQUIPCommand.Event = null;
        }
    }
}
