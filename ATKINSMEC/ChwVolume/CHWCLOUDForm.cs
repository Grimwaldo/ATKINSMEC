using Autodesk.Revit.DB;
using System;
using System.Windows.Forms;

namespace ATKINSMEC
{
    public partial class CHWCLOUDForm : System.Windows.Forms.Form
    {
        private const string AutoRegion = "(work it out on the first run)";

        private readonly Document _openDoc;

        public CHWCLOUD.CloudModel Result { get; private set; }

        public CHWCLOUDForm(Document openDoc)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _openDoc = openDoc;
        }

        private void CHWCLOUDForm_Load(object sender, EventArgs e)
        {
            // The region is no longer asked for: an unknown one is worked
            // out by trying each on the first run and then remembered.
            // It stays selectable for anyone who already knows it.
            cmbRegion.Items.Add(AutoRegion);

            foreach (string region in CHWCLOUD.Regions)
                cmbRegion.Items.Add(region);

            cmbRegion.SelectedIndex = 0;

            btnFromCurrent.Enabled =
                _openDoc != null && _openDoc.IsModelInCloud;

            if (!btnFromCurrent.Enabled)
            {
                btnFromCurrent.Text =
                    "The open model is not in ACC";
            }
        }

        private void btnFromCurrent_Click(object sender, EventArgs e)
        {
            string error;

            CHWCLOUD.CloudModel model =
                CHWCLOUD.FromDocument(_openDoc, out error);

            if (model == null)
            {
                MessageBox.Show(error, "ACC model");
                return;
            }

            txtName.Text = model.Name;
            txtProject.Text = model.ProjectGuid.ToString();
            txtModel.Text = model.ModelGuid.ToString();

            MessageBox.Show(
                "Read from the open model.",
                "ACC model");
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            string region = cmbRegion.Text.Trim();

            if (region == AutoRegion)
                region = string.Empty;

            Guid project;
            Guid model;

            if (!Guid.TryParse(txtProject.Text.Trim(), out project))
            {
                MessageBox.Show(
                    "The project GUID is not a valid GUID.",
                    "ACC model");

                return;
            }

            if (!Guid.TryParse(txtModel.Text.Trim(), out model))
            {
                MessageBox.Show(
                    "The model GUID is not a valid GUID.",
                    "ACC model");

                return;
            }

            string name = txtName.Text.Trim();

            if (name.Length == 0)
                name = model.ToString();

            Result = new CHWCLOUD.CloudModel
            {
                Name = name,
                Region = region,
                ProjectGuid = project,
                ModelGuid = model
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
