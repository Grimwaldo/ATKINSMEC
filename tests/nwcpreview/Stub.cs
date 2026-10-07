using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Just enough of CHWCLOUD for NWCSETTINGS to compile with no Revit
    // present. NWCJOB.ToCloudModel is the only thing that reaches for
    // it, and nothing here calls that.
    public class CHWCLOUD
    {
        public enum WorksetMode
        {
            All = 0,
            Editable = 1
        }

        public class CloudModel
        {
            public string Name { get; set; }
            public string Region { get; set; }
            public Guid ProjectGuid { get; set; }
            public Guid ModelGuid { get; set; }

            public WorksetMode Worksets { get; set; }

            public List<string> SystemTypes { get; set; }
                = new List<string>();
        }
    }

    // Just enough of NWCForm to let the real designer file draw itself,
    // with no Revit anywhere near it. The handlers have to exist
    // because the designer wires them up.
    public partial class NWCForm : Form
    {
        public NWCForm()
        {
            InitializeComponent();
        }

        public void Fill()
        {
            cboParams.Items.Add("All");
            cboParams.Items.Add("Elements only");
            cboParams.Items.Add("None");
            cboParams.SelectedIndex = 0;

            cboWorksets.Items.Add("Every workset");
            cboWorksets.Items.Add("Only the ones I own");
            cboWorksets.SelectedIndex = 0;

            cboView.Items.Add("(the whole model)");
            cboView.Items.Add("Navisworks");
            cboView.Text = "Navisworks";

            txtFolder.Text = @"C:\Projects\1234\Navisworks\Issue";

            chkShared.Checked = true;
            chkLevels.Checked = true;
            chkIds.Checked = true;
            chkRooms.Checked = true;
            chkProperties.Checked = true;

            ModelColumns();

            Model(true, "1234-ATK-ZZ-XX-M3-M-0001", "",
                "(window)",
                @"C:\Projects\1234\MEP\1234-ATK-ZZ-XX-M3-M-0001.rvt");

            Model(true, "1234-ATK-ZZ-XX-M3-E-0001", "NW-Export",
                "its own",
                @"C:\Projects\1234\Elec\1234-ATK-ZZ-XX-M3-E-0001.rvt");

            Model(false, "1234-ATK-ZZ-XX-M3-A-0001", "(the whole model)",
                "(window)", "ACC  -  EMEA");

            grpModels.Text =
                "1. Models to export  -  3 in the list, 2 ticked";

            Columns();

            Row("1234-ATK-ZZ-XX-M3-M-0001", "File", "Navisworks",
                "window", "yes", "48.2 MB", "112.4",
                @"C:\Projects\1234\Navisworks\Issue\1234-ATK-ZZ-XX-M3-M-0001.nwc");

            Row("1234-ATK-ZZ-XX-M3-E-0001", "File", "NW-Export",
                "its own", "no", "", "8.1",
                "No 3D view called \"NW-Export\" in this model, so nothing was written.");

            lblSummary.Text = "1 written, 1 failed, 0 skipped.";
            lblProgress.Text = "";
        }

        // ---- the models grid, mirroring NWCForm.ModelColumns ----

        private void ModelColumns()
        {
            dgvModels.Columns.Clear();

            dgvModels.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = string.Empty,
                Name = "colWanted",
                Width = 30,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            ModelColumn("Model", 210, true);
            ModelColumn("3D view", 150, false);
            ModelColumn("Settings", 90, true);
            ModelColumn("Where it is", 200, true);
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

        private void Model(
            bool wanted, string name, string view, string whose,
            string where)
        {
            int index = dgvModels.Rows.Add();

            DataGridViewRow row = dgvModels.Rows[index];

            row.Cells[0].Value = wanted;
            row.Cells[1].Value = name;
            row.Cells[2].Value = view;
            row.Cells[3].Value = whose;
            row.Cells[4].Value = where;

            if (whose == "its own")
            {
                row.Cells[3].Style.ForeColor =
                    System.Drawing.Color.FromArgb(0, 90, 160);
            }
        }

        // ---- the results grid ----

        private void Columns()
        {
            Column("Model", 200);
            Column("From", 55);
            Column("3D view", 130);
            Column("Settings", 70);
            Column("Written", 60);
            Column("Size", 70);
            Column("Seconds", 65);
            Column("What happened", 300);
        }

        private void Column(string header, int width)
        {
            dgvResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            });
        }

        private void Row(params string[] cells)
        {
            int index = dgvResults.Rows.Add();

            for (int i = 0; i < cells.Length; i++)
                dgvResults.Rows[index].Cells[i].Value = cells[i];

            if (cells[4] == "no")
            {
                dgvResults.Rows[index].Cells[4].Style.ForeColor =
                    System.Drawing.Color.Firebrick;
            }
        }

        private void NWCForm_Load(object sender, EventArgs e) { }
        private void btnAddFiles_Click(object sender, EventArgs e) { }
        private void btnAddAcc_Click(object sender, EventArgs e) { }
        private void btnAddOpen_Click(object sender, EventArgs e) { }
        private void btnRemove_Click(object sender, EventArgs e) { }
        private void btnTickAll_Click(object sender, EventArgs e) { }
        private void btnTickNone_Click(object sender, EventArgs e) { }
        private void btnJobSettings_Click(object sender, EventArgs e) { }
        private void btnJobWorksets_Click(object sender, EventArgs e) { }
        private void btnBrowse_Click(object sender, EventArgs e) { }
        private void btnExport_Click(object sender, EventArgs e) { }
        private void btnStop_Click(object sender, EventArgs e) { }
        private void btnImportSettings_Click(object sender, EventArgs e) { }
        private void btnExportSettings_Click(object sender, EventArgs e) { }
        private void btnHelp_Click(object sender, EventArgs e) { }
        private void btnClose_Click(object sender, EventArgs e) { }
    }
}
