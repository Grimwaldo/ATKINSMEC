using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Picks ACC models from the ones Revit has already opened on this
    // computer, so the project and model ids never have to be typed.
    // Falls back to CHWCLOUDForm for a model that has never been opened
    // here.
    public partial class CHWCACHEForm : System.Windows.Forms.Form
    {
        private readonly Document _openDoc;

        private readonly List<CHWCLOUD.CloudModel> _alreadyAdded;

        public List<CHWCLOUD.CloudModel> Results { get; private set; }

        public CHWCACHEForm(
            Document openDoc,
            List<CHWCLOUD.CloudModel> alreadyAdded)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _openDoc = openDoc;

            _alreadyAdded = alreadyAdded ??
                            new List<CHWCLOUD.CloudModel>();

            Results = new List<CHWCLOUD.CloudModel>();
        }

        private void CHWCACHEForm_Load(object sender, EventArgs e)
        {
            Cursor previous = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            List<CHWCLOUD.CachedModel> cached;

            try
            {
                cached = CHWCLOUD.ScanCache();
            }
            finally
            {
                this.Cursor = previous;
            }

            int skipped = 0;

            foreach (CHWCLOUD.CachedModel model in cached)
            {
                if (IsAlreadyAdded(model))
                {
                    skipped++;
                    continue;
                }

                clbModels.Items.Add(model, false);
            }

            if (clbModels.Items.Count == 0)
            {
                lblCount.Text = skipped > 0
                    ? $"All {skipped} model(s) found are already in the list."
                    : "No ACC model has been opened on this computer yet.";

                btnOk.Enabled = false;
                btnAll.Enabled = false;
                btnNone.Enabled = false;

                return;
            }

            lblCount.Text = skipped == 0
                ? $"{clbModels.Items.Count} model(s) found."
                : $"{clbModels.Items.Count} model(s) found, " +
                  $"{skipped} already in the list.";
        }

        private bool IsAlreadyAdded(CHWCLOUD.CachedModel model)
        {
            foreach (CHWCLOUD.CloudModel existing in _alreadyAdded)
            {
                if (existing.ProjectGuid == model.ProjectGuid &&
                    existing.ModelGuid == model.ModelGuid)
                    return true;
            }

            return false;
        }

        private void btnAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbModels.Items.Count; i++)
                clbModels.SetItemChecked(i, true);
        }

        private void btnNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbModels.Items.Count; i++)
                clbModels.SetItemChecked(i, false);
        }

        // The escape hatch: a model never opened on this computer is not
        // in the cache, so its ids are entered by hand
        private void btnManual_Click(object sender, EventArgs e)
        {
            using (CHWCLOUDForm dialog = new CHWCLOUDForm(_openDoc))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK ||
                    dialog.Result == null)
                    return;

                Results.Add(dialog.Result);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            foreach (object item in clbModels.CheckedItems)
            {
                var model = item as CHWCLOUD.CachedModel;

                if (model != null)
                    Results.Add(model.ToCloudModel());
            }

            if (Results.Count == 0)
            {
                MessageBox.Show(
                    "Check at least one model, or cancel.",
                    "Add ACC model");

                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
