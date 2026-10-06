using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Which worksets to open in ONE model.
    //
    // The worksets are read from the model before this window opens
    // (NWCEXPORT.WorksetsOf, or WorksetsByOpening when that will not
    // answer), so there is no Revit in this form at all and it renders
    // in the preview harness like the rest.
    //
    // Empty means "follow the main window" - every workset, or only the
    // ones I own. That is the usual case and stays the default.
    public partial class NWCWORKSETForm : System.Windows.Forms.Form
    {
        private readonly string _model;

        // What the model actually has, read live
        private readonly List<WORKSETPICK> _available;

        // Why the list is empty, when it is
        private readonly string _trouble;

        // What each row stands for, by row index - so a tick can be
        // turned back into a workset without parsing the text shown
        private readonly List<WORKSETPICK> _rows =
            new List<WORKSETPICK>();

        // Empty means "whatever the window says"
        public List<WORKSETPICK> Chosen { get; private set; }

        public NWCWORKSETForm(
            string model,
            List<WORKSETPICK> available,
            string trouble,
            List<WORKSETPICK> already)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _model = model ?? string.Empty;
            _available = available ?? new List<WORKSETPICK>();
            _trouble = trouble ?? string.Empty;

            Chosen = WORKSETPICK.Copy(already);
        }

        private void NWCWORKSETForm_Load(object sender, EventArgs e)
        {
            lblModel.Text = _model;

            Rows();

            chkInherit.Checked = Chosen.Count == 0;

            if (_available.Count == 0)
            {
                chkInherit.Checked = true;
                chkInherit.Enabled = false;

                lblCount.Text = string.IsNullOrEmpty(_trouble)
                    ? "This model has no worksets - it is not workshared."
                    : "The worksets could not be read:" +
                      Environment.NewLine + _trouble;
            }

            Enable();

            Count();
        }

        // One row per workset the model has, plus a row for anything
        // that was picked before and is no longer there
        private void Rows()
        {
            _rows.Clear();

            clbWorksets.Items.Clear();

            foreach (WORKSETPICK had in _available)
            {
                _rows.Add(had);

                clbWorksets.Items.Add(Label(had));
            }

            foreach (WORKSETPICK want in Chosen)
            {
                if (WORKSETPICK.Find(_available, want) != null)
                    continue;

                _rows.Add(want);

                clbWorksets.Items.Add(want.Name + "   (not in this model)");
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                clbWorksets.SetItemChecked(
                    i, WORKSETPICK.Find(Chosen, _rows[i]) != null);
            }
        }

        // A workset whose id was picked before but whose NAME has
        // changed since says so. That is the whole reason the id is
        // stored: the pick follows the rename instead of quietly
        // opening nothing, and somebody gets told it happened.
        private string Label(WORKSETPICK had)
        {
            if (!had.HasId)
                return had.Name;

            foreach (WORKSETPICK want in Chosen)
            {
                if (!want.HasId)
                    continue;

                if (!string.Equals(want.UniqueId, had.UniqueId,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(want.Name, had.Name,
                        StringComparison.OrdinalIgnoreCase))
                    return had.Name + "   (renamed, was " + want.Name + ")";

                return had.Name;
            }

            return had.Name;
        }

        private void chkInherit_CheckedChanged(object sender, EventArgs e)
        {
            Enable();
        }

        private void Enable()
        {
            grpPick.Enabled = !chkInherit.Checked && _available.Count > 0;
        }

        private void btnAll_Click(object sender, EventArgs e)
        {
            Tick(true);
        }

        private void btnNone_Click(object sender, EventArgs e)
        {
            Tick(false);
        }

        private void Tick(bool on)
        {
            for (int i = 0; i < clbWorksets.Items.Count; i++)
                clbWorksets.SetItemChecked(i, on);

            Count();
        }

        private void clbWorksets_ItemCheck(
            object sender, ItemCheckEventArgs e)
        {
            // The tick has not landed yet when this fires, so the count
            // is done after the message has been dealt with
            BeginInvoke((MethodInvoker)Count);
        }

        private List<WORKSETPICK> Ticked()
        {
            var picked = new List<WORKSETPICK>();

            foreach (int i in clbWorksets.CheckedIndices)
            {
                if (i >= 0 && i < _rows.Count)
                    picked.Add(_rows[i]);
            }

            return picked;
        }

        private void Count()
        {
            if (_available.Count == 0)
                return;

            int real = 0;
            int missing = 0;

            foreach (WORKSETPICK pick in Ticked())
            {
                if (WORKSETPICK.Find(_available, pick) != null)
                    real++;
                else
                    missing++;
            }

            if (real == 0)
            {
                lblCount.Text =
                    "Nothing ticked that this model has - it would open " +
                    "EMPTY and nothing would be exported.";

                return;
            }

            // A ticked workset the model has not got opens nothing, so
            // it is not counted in "will be opened" - saying 4 when 3
            // will open is the kind of small lie that costs a deadline
            lblCount.Text =
                real + " of " + _available.Count +
                " worksets will be opened." +
                (missing == 0
                    ? string.Empty
                    : Environment.NewLine + Environment.NewLine +
                      missing + " ticked name(s) are not in this model " +
                      "and will do nothing.");
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (chkInherit.Checked)
            {
                Chosen = new List<WORKSETPICK>();

                return;
            }

            List<WORKSETPICK> picked = Ticked();

            // Ticking nothing is the same instruction as "follow the
            // window" only by accident - it would open a model with no
            // geometry in it. Said out loud rather than exported.
            if (picked.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Nothing is ticked, so this model would open with no " +
                    "geometry and the .nwc would be empty." +
                    Environment.NewLine + Environment.NewLine +
                    "Tick at least one workset, or tick the box at the " +
                    "top to follow the main window.",
                    "NWC BATCH EXPORTER",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                DialogResult = DialogResult.None;

                return;
            }

            Chosen = picked;
        }
    }
}
