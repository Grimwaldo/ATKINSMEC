using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Modeless, so the model stays usable and a terminal can be shown in
    // the view from the list. Nothing here touches the Revit API - every
    // request goes through ALIGNEVENT and comes back through the Show*
    // methods below, which run on Revit's own thread.
    public partial class ALIGNForm : System.Windows.Forms.Form
    {
        private const string RuleAny = "any air terminal";
        private const string RuleFamily = "the same family";
        private const string RuleType = "the same family and type";

        private const int ColCheckIndex = 0;
        private const int ColShowIndex = 1;

        private readonly Document _doc;
        private readonly ALIGNEVENT _handler;
        private readonly ExternalEvent _event;

        private List<ALIGNTERMINALS.Match> _matches =
            new List<ALIGNTERMINALS.Match>();

        // Terminals picked in the model; empty means the whole model
        private HashSet<long> _picked = new HashSet<long>();

        private int _terminalsHere;

        public ALIGNForm(
            Document doc, ALIGNEVENT handler, ExternalEvent externalEvent)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _doc = doc;
            _handler = handler;
            _event = externalEvent;

            this.Text = "ALIGN TERMINALS - " + doc.Title;
        }

        private void ALIGNForm_Load(object sender, EventArgs e)
        {
            cmbRule.Items.Add(RuleAny);
            cmbRule.Items.Add(RuleFamily);
            cmbRule.Items.Add(RuleType);
            cmbRule.SelectedIndex = 0;

            dgvMatches.CellContentClick += dgvMatches_CellContentClick;

            Raise(ALIGNEVENT.Request.Refresh);
        }

        private void Raise(ALIGNEVENT.Request what)
        {
            if (_handler == null || _event == null)
                return;

            _handler.Action = what;

            _event.Raise();
        }

        // ==================================================
        // Coming back from ALIGNEVENT
        // ==================================================

        public void ShowLinks(
            List<RVTLINKSELECTION.LinkItem> links,
            int terminalsHere,
            HashSet<long> alreadySelected)
        {
            _terminalsHere = terminalsHere;

            clbLinks.Items.Clear();

            foreach (RVTLINKSELECTION.LinkItem link in links)
                clbLinks.Items.Add(link, true);

            if (alreadySelected != null && alreadySelected.Count > 0)
                _picked = alreadySelected;

            if (clbLinks.Items.Count == 0)
            {
                lblLinksInfo.Text =
                    "This model has no loaded linked models, so there is " +
                    "nothing to align to.";

                btnFind.Enabled = false;
            }
            else
            {
                lblLinksInfo.Text =
                    $"{terminalsHere} air terminal(s) in this model. Tick " +
                    "the links that hold the architectural terminals - " +
                    "ticking one without any does no harm.";
            }

            ShowScope();
        }

        public void ShowMatches(
            List<ALIGNTERMINALS.Match> matches, int inLinks)
        {
            _matches = matches ?? new List<ALIGNTERMINALS.Match>();

            FillTable();

            lblLinksInfo.Text =
                $"{_terminalsHere} air terminal(s) in this model, " +
                $"{inLinks} in the ticked link(s).";

            Cursor = Cursors.Default;
        }

        public void SetPicked(HashSet<long> picked)
        {
            _picked = picked ?? new HashSet<long>();

            rbSelected.Checked = _picked.Count > 0;

            ShowScope();

            ClearTable();
        }

        public void Applied(int moved, int attempted)
        {
            Cursor = Cursors.Default;

            FillTable();

            int refused = attempted - moved;

            string tail = refused == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  $"{refused} would not move. The Note column says why - " +
                  "usually a terminal hosted on a ceiling, whose height " +
                  "its host controls.";

            MessageBox.Show(
                this,
                $"Moved {moved} of {attempted} air terminal(s)." + tail,
                "ALIGN TERMINALS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        public void ReportError(string message)
        {
            Cursor = Cursors.Default;

            MessageBox.Show(
                this,
                message,
                "ALIGN TERMINALS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        // ==================================================
        // Scope
        // ==================================================

        private void ShowScope()
        {
            rbAll.Text =
                $"Every air terminal in this model   ({_terminalsHere})";

            rbSelected.Text = _picked.Count == 0
                ? "Only the ones picked in the model   (none picked yet)"
                : $"Only the ones picked in the model   ({_picked.Count} picked)";

            rbSelected.Enabled = _picked.Count > 0;

            if (_picked.Count == 0)
                rbAll.Checked = true;
        }

        private void Scope_CheckedChanged(object sender, EventArgs e)
        {
            ClearTable();
        }

        private void btnPick_Click(object sender, EventArgs e)
        {
            Raise(ALIGNEVENT.Request.Pick);
        }

        // ==================================================
        // Options
        // ==================================================

        private ALIGNTERMINALS.Options ReadOptions()
        {
            var options = new ALIGNTERMINALS.Options
            {
                MaxDistanceMm = (double)numDistance.Value,
                MatchElevation = chkElevation.Checked
            };

            string rule = cmbRule.SelectedItem as string;

            options.SameFamily = rule == RuleFamily || rule == RuleType;
            options.SameType = rule == RuleType;

            foreach (object item in clbLinks.CheckedItems)
            {
                var link = item as RVTLINKSELECTION.LinkItem;

                if (link != null)
                    options.LinkIds.Add(link.Id);
            }

            if (rbSelected.Checked)
                options.OnlyTerminals = new HashSet<long>(_picked);

            return options;
        }

        private void btnFind_Click(object sender, EventArgs e)
        {
            ALIGNTERMINALS.Options options = ReadOptions();

            if (options.LinkIds.Count == 0)
            {
                MessageBox.Show(
                    "Tick at least one linked model.",
                    "ALIGN TERMINALS");

                return;
            }

            Cursor = Cursors.WaitCursor;

            _handler.Options = options;

            Raise(ALIGNEVENT.Request.Find);
        }

        // ==================================================
        // The table
        // ==================================================

        private void ClearTable()
        {
            dgvMatches.Rows.Clear();

            _matches.Clear();

            btnAlign.Enabled = false;

            lblSummary.Text = string.Empty;
        }

        private void FillTable()
        {
            dgvMatches.Rows.Clear();

            int movable = 0;
            double furthest = 0;

            foreach (ALIGNTERMINALS.Match match in _matches)
            {
                int index = dgvMatches.Rows.Add();

                DataGridViewRow row = dgvMatches.Rows[index];

                row.Tag = match;

                bool can = match.CanMove;

                row.Cells[ColCheckIndex].Value = can && !match.Moved;
                row.Cells[ColCheckIndex].ReadOnly = !can;

                row.Cells[ColShowIndex].Value = "Show";

                row.Cells[2].Value = match.TerminalName;

                row.Cells[3].Value = can
                    ? match.TargetName + "   (" + match.LinkName + ")"
                    : string.Empty;

                row.Cells[4].Value = can
                    ? match.PlanDistanceMm.ToString("N0")
                    : string.Empty;

                row.Cells[5].Value = can
                    ? match.RiseMm.ToString("N0")
                    : string.Empty;

                row.Cells[6].Value = match.Moved
                    ? "moved"
                    : match.Problem ?? string.Empty;

                row.Cells[6].ToolTipText =
                    row.Cells[6].Value as string ?? string.Empty;

                row.Cells[3].ToolTipText =
                    row.Cells[3].Value as string ?? string.Empty;

                if (match.Moved)
                {
                    row.DefaultCellStyle.ForeColor =
                        System.Drawing.Color.SeaGreen;
                }
                else if (!can)
                {
                    row.DefaultCellStyle.ForeColor =
                        System.Drawing.Color.Firebrick;
                }
                else
                {
                    movable++;

                    if (match.PlanDistanceMm > furthest)
                        furthest = match.PlanDistanceMm;
                }
            }

            btnAlign.Enabled = movable > 0;

            int stranded = _matches.Count - movable;

            lblSummary.Text =
                $"{movable} terminal(s) can be aligned, furthest move " +
                $"{furthest:N0} mm. {stranded} left where they are." +
                Environment.NewLine +
                "Press Show on a row to find that terminal in the model. " +
                "A big move usually means it matched the wrong diffuser.";
        }

        // The Show column is a button; every other cell click is normal
        private void dgvMatches_CellContentClick(
            object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColShowIndex)
                return;

            var match = dgvMatches.Rows[e.RowIndex].Tag
                as ALIGNTERMINALS.Match;

            if (match == null)
                return;

            _handler.ZoomTo = match.TerminalId;

            Raise(ALIGNEVENT.Request.Zoom);
        }

        // ==================================================
        // Aligning
        // ==================================================

        private void btnAlign_Click(object sender, EventArgs e)
        {
            dgvMatches.EndEdit();

            var wanted = new List<ALIGNTERMINALS.Match>();

            foreach (DataGridViewRow row in dgvMatches.Rows)
            {
                var match = row.Tag as ALIGNTERMINALS.Match;

                bool ticked = row.Cells[ColCheckIndex].Value is bool &&
                              (bool)row.Cells[ColCheckIndex].Value;

                if (match != null && ticked && match.CanMove)
                    wanted.Add(match);
            }

            if (wanted.Count == 0)
            {
                MessageBox.Show(
                    "Tick at least one terminal to align.",
                    "ALIGN TERMINALS");

                return;
            }

            DialogResult sure = MessageBox.Show(
                this,
                $"Move {wanted.Count} air terminal(s) onto their " +
                "architectural counterparts?" + Environment.NewLine +
                Environment.NewLine +
                "This changes the model. One Ctrl+Z afterwards undoes the " +
                "whole lot.",
                "ALIGN TERMINALS",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (sure != DialogResult.Yes)
                return;

            Cursor = Cursors.WaitCursor;

            _handler.ToApply = wanted;

            Raise(ALIGNEVENT.Request.Apply);
        }

        // ==================================================
        // Small handlers
        // ==================================================

        private void btnAllLinks_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbLinks.Items.Count; i++)
                clbLinks.SetItemChecked(i, true);
        }

        private void btnNoLinks_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbLinks.Items.Count; i++)
                clbLinks.SetItemChecked(i, false);
        }

        private void btnTickAll_Click(object sender, EventArgs e)
        {
            SetTicks(true);
        }

        private void btnUntickAll_Click(object sender, EventArgs e)
        {
            SetTicks(false);
        }

        private void SetTicks(bool ticked)
        {
            dgvMatches.EndEdit();

            // A row with nothing to move stays unticked whatever
            foreach (DataGridViewRow row in Target())
            {
                var match = row.Tag as ALIGNTERMINALS.Match;

                if (match != null && match.CanMove)
                    row.Cells[ColCheckIndex].Value = ticked;
            }
        }

        // The selection when there is a real one, otherwise every row
        private List<DataGridViewRow> Target()
        {
            var rows = new List<DataGridViewRow>();

            if (dgvMatches.SelectedRows.Count > 1)
            {
                foreach (DataGridViewRow row in dgvMatches.SelectedRows)
                    rows.Add(row);

                return rows;
            }

            foreach (DataGridViewRow row in dgvMatches.Rows)
                rows.Add(row);

            return rows;
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            using (ALIGNHELPForm help = new ALIGNHELPForm())
            {
                help.ShowDialog(this);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Let the command open a fresh window next time
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            ALIGNCommand.Form = null;
        }
    }
}
