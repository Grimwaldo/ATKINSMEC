using System;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // One model's own export settings.
    //
    // Either a model uses the window's settings or it uses ITS OWN
    // WHOLE SET - there is no half-overridden state. A per-field
    // override would mean every value on the main window having two
    // meanings, and somebody having to work out which one won.
    //
    // No Revit in this form at all, so it renders in the preview
    // harness like the rest.
    public partial class NWCMODELForm : System.Windows.Forms.Form
    {
        private readonly string _model;

        // What the main window is set to, used as the starting point
        // when a model is first given its own
        private readonly NWCSETTINGS _window;

        // Null when this model follows the window
        public NWCSETTINGS Chosen { get; private set; }

        public NWCMODELForm(
            string model, NWCSETTINGS window, NWCSETTINGS already)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _model = model ?? string.Empty;
            _window = window ?? new NWCSETTINGS();

            Chosen = already;
        }

        private void NWCMODELForm_Load(object sender, EventArgs e)
        {
            lblModel.Text = _model;

            cboParams.Items.Add("All");
            cboParams.Items.Add("Elements only");
            cboParams.Items.Add("None");

            cboWorksets.Items.Add("Every workset");
            cboWorksets.Items.Add("Only the ones I own");

            // Starting from the window's settings when there are none
            // yet is the kind thing: a model that wants one thing
            // changed should not have to set the other twelve
            Fill(Chosen ?? _window);

            chkInherit.Checked = Chosen == null;

            Enable();
        }

        private void Fill(NWCSETTINGS settings)
        {
            chkShared.Checked = settings.SharedCoordinates;
            chkLinks.Checked = settings.ExportLinks;
            chkLevels.Checked = settings.DivideIntoLevels;
            chkIds.Checked = settings.ExportElementIds;
            chkRooms.Checked = settings.ExportRoomAsAttribute;
            chkParts.Checked = settings.ExportParts;
            chkProperties.Checked = settings.ConvertElementProperties;
            chkSkip.Checked = settings.SkipUnchanged;

            cboParams.SelectedIndex =
                Math.Max(0, Math.Min(2, settings.Parameters));

            cboWorksets.SelectedIndex =
                Math.Max(0, Math.Min(1, settings.Worksets));

            numFacet.Value = (decimal)Math.Max(0.0,
                Math.Min(10.0, settings.FacetingFactor));

            txtPrefix.Text = settings.Prefix;
            txtSuffix.Text = settings.Suffix;
        }

        private void chkInherit_CheckedChanged(object sender, EventArgs e)
        {
            Enable();
        }

        private void Enable()
        {
            grpOwn.Enabled = !chkInherit.Checked;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (chkInherit.Checked)
            {
                Chosen = null;

                return;
            }

            var mine = new NWCSETTINGS
            {
                SharedCoordinates = chkShared.Checked,
                ExportLinks = chkLinks.Checked,
                DivideIntoLevels = chkLevels.Checked,
                ExportElementIds = chkIds.Checked,
                ExportRoomAsAttribute = chkRooms.Checked,
                ExportParts = chkParts.Checked,
                ConvertElementProperties = chkProperties.Checked,
                SkipUnchanged = chkSkip.Checked,

                Parameters = Math.Max(0, cboParams.SelectedIndex),
                Worksets = Math.Max(0, cboWorksets.SelectedIndex),

                FacetingFactor = (double)numFacet.Value,

                Prefix = txtPrefix.Text.Trim(),
                Suffix = txtSuffix.Text.Trim(),

                // The folder is the window's for every model in the
                // run, and the view is set in the row - neither belongs
                // to an override
                OutputFolder = string.Empty,
                ViewName = string.Empty
            };

            Chosen = mine;
        }
    }
}
