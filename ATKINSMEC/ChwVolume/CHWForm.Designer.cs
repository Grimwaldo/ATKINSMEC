
namespace ATKINSMEC
{
    partial class CHWForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.tabModels = new System.Windows.Forms.TabControl();
            this.tabLocal = new System.Windows.Forms.TabPage();
            this.dgvLocal = new System.Windows.Forms.DataGridView();
            this.colLocalCheck = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colLocalName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLocalSupply = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colLocalReturn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.btnLocalAll = new System.Windows.Forms.Button();
            this.btnLocalNone = new System.Windows.Forms.Button();
            this.lblLocalInfo = new System.Windows.Forms.Label();
            this.tabCloud = new System.Windows.Forms.TabPage();
            this.dgvCloud = new System.Windows.Forms.DataGridView();
            this.colCloudCheck = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colCloudName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCloudSupply = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colCloudReturn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colCloudWorksets = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.btnCloudAll = new System.Windows.Forms.Button();
            this.btnCloudNone = new System.Windows.Forms.Button();
            this.btnAddCloud = new System.Windows.Forms.Button();
            this.btnRemoveCloud = new System.Windows.Forms.Button();
            this.btnReadTypes = new System.Windows.Forms.Button();
            this.lblCloudInfo = new System.Windows.Forms.Label();
            this.grpResults = new System.Windows.Forms.GroupBox();
            this.lvResults = new System.Windows.Forms.ListView();
            this.colModel = new System.Windows.Forms.ColumnHeader();
            this.colSupplyType = new System.Windows.Forms.ColumnHeader();
            this.colReturnType = new System.Windows.Forms.ColumnHeader();
            this.colSupply = new System.Windows.Forms.ColumnHeader();
            this.colReturn = new System.Windows.Forms.ColumnHeader();
            this.colSupplyPipes = new System.Windows.Forms.ColumnHeader();
            this.colReturnPipes = new System.Windows.Forms.ColumnHeader();
            this.colTotal = new System.Windows.Forms.ColumnHeader();
            this.lblTotal = new System.Windows.Forms.Label();
            this.btnCalculate = new System.Windows.Forms.Button();
            this.btnHelp = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.tabModels.SuspendLayout();
            this.tabLocal.SuspendLayout();
            this.tabCloud.SuspendLayout();
            this.grpResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLocal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCloud)).BeginInit();
            this.SuspendLayout();
            //
            // tabModels
            //
            this.tabModels.Controls.Add(this.tabLocal);
            this.tabModels.Controls.Add(this.tabCloud);
            this.tabModels.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tabModels.Location = new System.Drawing.Point(12, 12);
            this.tabModels.Name = "tabModels";
            this.tabModels.SelectedIndex = 0;
            this.tabModels.Size = new System.Drawing.Size(876, 306);
            this.tabModels.TabIndex = 0;
            //
            // tabLocal
            //
            this.tabLocal.Controls.Add(this.dgvLocal);
            this.tabLocal.Controls.Add(this.btnLocalAll);
            this.tabLocal.Controls.Add(this.btnLocalNone);
            this.tabLocal.Controls.Add(this.lblLocalInfo);
            this.tabLocal.Location = new System.Drawing.Point(4, 22);
            this.tabLocal.Name = "tabLocal";
            this.tabLocal.Padding = new System.Windows.Forms.Padding(3);
            this.tabLocal.Size = new System.Drawing.Size(868, 280);
            this.tabLocal.TabIndex = 0;
            this.tabLocal.Text = "Open in Revit";
            this.tabLocal.UseVisualStyleBackColor = true;
            //
            // dgvLocal
            //
            this.dgvLocal.AllowUserToAddRows = false;
            this.dgvLocal.AllowUserToDeleteRows = false;
            this.dgvLocal.AllowUserToResizeRows = false;
            this.dgvLocal.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvLocal.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvLocal.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colLocalCheck,
            this.colLocalName,
            this.colLocalSupply,
            this.colLocalReturn});
            this.dgvLocal.EditMode =
                System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dgvLocal.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvLocal.Location = new System.Drawing.Point(12, 12);
            this.dgvLocal.MultiSelect = true;
            this.dgvLocal.Name = "dgvLocal";
            this.dgvLocal.RowHeadersVisible = false;
            this.dgvLocal.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvLocal.Size = new System.Drawing.Size(690, 210);
            this.dgvLocal.TabIndex = 0;
            this.dgvLocal.SelectionChanged += new System.EventHandler(this.Grid_SelectionChanged);
            //
            // colLocalCheck
            //
            this.colLocalCheck.HeaderText = "";
            this.colLocalCheck.Name = "colLocalCheck";
            this.colLocalCheck.Width = 30;
            //
            // colLocalName
            //
            this.colLocalName.HeaderText = "Model";
            this.colLocalName.Name = "colLocalName";
            this.colLocalName.ReadOnly = true;
            this.colLocalName.Width = 330;
            //
            // colLocalSupply
            //
            this.colLocalSupply.HeaderText = "Supply system type";
            this.colLocalSupply.Name = "colLocalSupply";
            this.colLocalSupply.Width = 160;
            //
            // colLocalReturn
            //
            this.colLocalReturn.HeaderText = "Return system type";
            this.colLocalReturn.Name = "colLocalReturn";
            this.colLocalReturn.Width = 160;
            //
            // btnLocalAll
            //
            this.btnLocalAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLocalAll.Location = new System.Drawing.Point(714, 12);
            this.btnLocalAll.Name = "btnLocalAll";
            this.btnLocalAll.Size = new System.Drawing.Size(142, 26);
            this.btnLocalAll.TabIndex = 1;
            this.btnLocalAll.Text = "Tick all";
            this.btnLocalAll.UseVisualStyleBackColor = true;
            this.btnLocalAll.Click += new System.EventHandler(this.btnLocalAll_Click);
            //
            // btnLocalNone
            //
            this.btnLocalNone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLocalNone.Location = new System.Drawing.Point(714, 44);
            this.btnLocalNone.Name = "btnLocalNone";
            this.btnLocalNone.Size = new System.Drawing.Size(142, 26);
            this.btnLocalNone.TabIndex = 2;
            this.btnLocalNone.Text = "Untick all";
            this.btnLocalNone.UseVisualStyleBackColor = true;
            this.btnLocalNone.Click += new System.EventHandler(this.btnLocalNone_Click);
            //
            // lblLocalInfo
            //
            this.lblLocalInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblLocalInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblLocalInfo.Location = new System.Drawing.Point(12, 230);
            this.lblLocalInfo.Name = "lblLocalInfo";
            this.lblLocalInfo.Size = new System.Drawing.Size(690, 40);
            this.lblLocalInfo.TabIndex = 4;
            this.lblLocalInfo.Text = "";
            //
            // tabCloud
            //
            this.tabCloud.Controls.Add(this.dgvCloud);
            this.tabCloud.Controls.Add(this.btnCloudAll);
            this.tabCloud.Controls.Add(this.btnCloudNone);
            this.tabCloud.Controls.Add(this.btnAddCloud);
            this.tabCloud.Controls.Add(this.btnRemoveCloud);
            this.tabCloud.Controls.Add(this.btnReadTypes);
            this.tabCloud.Controls.Add(this.lblCloudInfo);
            this.tabCloud.Location = new System.Drawing.Point(4, 22);
            this.tabCloud.Name = "tabCloud";
            this.tabCloud.Padding = new System.Windows.Forms.Padding(3);
            this.tabCloud.Size = new System.Drawing.Size(868, 280);
            this.tabCloud.TabIndex = 1;
            this.tabCloud.Text = "ACC models";
            this.tabCloud.UseVisualStyleBackColor = true;
            //
            // dgvCloud
            //
            this.dgvCloud.AllowUserToAddRows = false;
            this.dgvCloud.AllowUserToDeleteRows = false;
            this.dgvCloud.AllowUserToResizeRows = false;
            this.dgvCloud.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvCloud.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvCloud.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colCloudCheck,
            this.colCloudName,
            this.colCloudSupply,
            this.colCloudReturn,
            this.colCloudWorksets});
            this.dgvCloud.EditMode =
                System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dgvCloud.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvCloud.Location = new System.Drawing.Point(12, 12);
            this.dgvCloud.MultiSelect = true;
            this.dgvCloud.Name = "dgvCloud";
            this.dgvCloud.RowHeadersVisible = false;
            this.dgvCloud.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvCloud.Size = new System.Drawing.Size(690, 186);
            this.dgvCloud.TabIndex = 0;
            this.dgvCloud.SelectionChanged += new System.EventHandler(this.Grid_SelectionChanged);
            //
            // colCloudCheck
            //
            this.colCloudCheck.HeaderText = "";
            this.colCloudCheck.Name = "colCloudCheck";
            this.colCloudCheck.Width = 30;
            //
            // colCloudName
            //
            this.colCloudName.HeaderText = "ACC model";
            this.colCloudName.Name = "colCloudName";
            this.colCloudName.ReadOnly = true;
            this.colCloudName.Width = 250;
            //
            // colCloudSupply
            //
            this.colCloudSupply.HeaderText = "Supply system type";
            this.colCloudSupply.Name = "colCloudSupply";
            this.colCloudSupply.Width = 145;
            //
            // colCloudReturn
            //
            this.colCloudReturn.HeaderText = "Return system type";
            this.colCloudReturn.Name = "colCloudReturn";
            this.colCloudReturn.Width = 145;
            //
            // colCloudWorksets
            //
            this.colCloudWorksets.HeaderText = "Worksets";
            this.colCloudWorksets.Name = "colCloudWorksets";
            this.colCloudWorksets.Width = 110;
            //
            // btnCloudAll
            //
            this.btnCloudAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCloudAll.Location = new System.Drawing.Point(714, 12);
            this.btnCloudAll.Name = "btnCloudAll";
            this.btnCloudAll.Size = new System.Drawing.Size(142, 26);
            this.btnCloudAll.TabIndex = 1;
            this.btnCloudAll.Text = "Tick all";
            this.btnCloudAll.UseVisualStyleBackColor = true;
            this.btnCloudAll.Click += new System.EventHandler(this.btnCloudAll_Click);
            //
            // btnCloudNone
            //
            this.btnCloudNone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCloudNone.Location = new System.Drawing.Point(714, 44);
            this.btnCloudNone.Name = "btnCloudNone";
            this.btnCloudNone.Size = new System.Drawing.Size(142, 26);
            this.btnCloudNone.TabIndex = 2;
            this.btnCloudNone.Text = "Untick all";
            this.btnCloudNone.UseVisualStyleBackColor = true;
            this.btnCloudNone.Click += new System.EventHandler(this.btnCloudNone_Click);
            //
            // btnAddCloud
            //
            this.btnAddCloud.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddCloud.Location = new System.Drawing.Point(714, 84);
            this.btnAddCloud.Name = "btnAddCloud";
            this.btnAddCloud.Size = new System.Drawing.Size(142, 26);
            this.btnAddCloud.TabIndex = 3;
            this.btnAddCloud.Text = "Add ACC model...";
            this.btnAddCloud.UseVisualStyleBackColor = true;
            this.btnAddCloud.Click += new System.EventHandler(this.btnAddCloud_Click);
            //
            // btnRemoveCloud
            //
            this.btnRemoveCloud.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemoveCloud.Location = new System.Drawing.Point(714, 116);
            this.btnRemoveCloud.Name = "btnRemoveCloud";
            this.btnRemoveCloud.Size = new System.Drawing.Size(142, 26);
            this.btnRemoveCloud.TabIndex = 4;
            this.btnRemoveCloud.Text = "Remove";
            this.btnRemoveCloud.UseVisualStyleBackColor = true;
            this.btnRemoveCloud.Click += new System.EventHandler(this.btnRemoveCloud_Click);
            //
            // btnReadTypes
            //
            this.btnReadTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnReadTypes.Location = new System.Drawing.Point(714, 156);
            this.btnReadTypes.Name = "btnReadTypes";
            this.btnReadTypes.Size = new System.Drawing.Size(142, 44);
            this.btnReadTypes.TabIndex = 5;
            this.btnReadTypes.Text = "Read type names from ticked models";
            this.btnReadTypes.UseVisualStyleBackColor = true;
            this.btnReadTypes.Click += new System.EventHandler(this.btnReadTypes_Click);
            //
            // lblCloudInfo
            //
            this.lblCloudInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblCloudInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblCloudInfo.Location = new System.Drawing.Point(12, 206);
            this.lblCloudInfo.Name = "lblCloudInfo";
            this.lblCloudInfo.Size = new System.Drawing.Size(690, 64);
            this.lblCloudInfo.TabIndex = 7;
            this.lblCloudInfo.Text = "";
            //
            // grpResults
            //
            this.grpResults.Controls.Add(this.lvResults);
            this.grpResults.Controls.Add(this.lblTotal);
            this.grpResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpResults.Location = new System.Drawing.Point(12, 372);
            this.grpResults.Name = "grpResults";
            this.grpResults.Size = new System.Drawing.Size(876, 262);
            this.grpResults.TabIndex = 2;
            this.grpResults.TabStop = false;
            this.grpResults.Text = "2.  Chilled water volume  (pipes and fittings of each model's chosen system types)";
            //
            // lvResults
            //
            this.lvResults.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colModel,
            this.colSupplyType,
            this.colReturnType,
            this.colSupply,
            this.colReturn,
            this.colSupplyPipes,
            this.colReturnPipes,
            this.colTotal});
            this.lvResults.FullRowSelect = true;
            this.lvResults.GridLines = true;
            this.lvResults.HideSelection = false;
            this.lvResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lvResults.Location = new System.Drawing.Point(12, 24);
            this.lvResults.Name = "lvResults";
            this.lvResults.Size = new System.Drawing.Size(852, 172);
            this.lvResults.TabIndex = 0;
            this.lvResults.UseCompatibleStateImageBehavior = false;
            this.lvResults.View = System.Windows.Forms.View.Details;
            //
            // colModel
            //
            this.colModel.Text = "Model";
            this.colModel.Width = 235;
            //
            // colSupplyType
            //
            this.colSupplyType.Text = "Supply type";
            this.colSupplyType.Width = 120;
            //
            // colReturnType
            //
            this.colReturnType.Text = "Return type";
            this.colReturnType.Width = 120;
            //
            // colSupply
            //
            this.colSupply.Text = "Supply (L)";
            this.colSupply.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colSupply.Width = 80;
            //
            // colReturn
            //
            this.colReturn.Text = "Return (L)";
            this.colReturn.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colReturn.Width = 80;
            //
            // colSupplyPipes
            //
            this.colSupplyPipes.Text = "Pipes";
            this.colSupplyPipes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colSupplyPipes.Width = 60;
            //
            // colReturnPipes
            //
            this.colReturnPipes.Text = "Fittings";
            this.colReturnPipes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colReturnPipes.Width = 60;
            //
            // colTotal
            //
            this.colTotal.Text = "Total (L)";
            this.colTotal.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colTotal.Width = 90;
            //
            // lblTotal
            //
            this.lblTotal.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTotal.Location = new System.Drawing.Point(12, 204);
            this.lblTotal.Name = "lblTotal";
            this.lblTotal.Size = new System.Drawing.Size(852, 50);
            this.lblTotal.TabIndex = 1;
            this.lblTotal.Text = "";
            //
            // btnCalculate
            //
            this.btnCalculate.Location = new System.Drawing.Point(12, 328);
            this.btnCalculate.Name = "btnCalculate";
            this.btnCalculate.Size = new System.Drawing.Size(280, 34);
            this.btnCalculate.TabIndex = 1;
            this.btnCalculate.Text = "1.  Calculate volume  (both tabs)";
            this.btnCalculate.UseVisualStyleBackColor = true;
            this.btnCalculate.Click += new System.EventHandler(this.btnCalculate_Click);
            //
            // btnHelp
            //
            this.btnHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnHelp.Location = new System.Drawing.Point(12, 644);
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(130, 28);
            this.btnHelp.TabIndex = 3;
            this.btnHelp.Text = "How to use this";
            this.btnHelp.UseVisualStyleBackColor = true;
            this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
            //
            // btnExport
            //
            this.btnExport.Enabled = false;
            this.btnExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExport.Location = new System.Drawing.Point(598, 644);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(170, 28);
            this.btnExport.TabIndex = 4;
            this.btnExport.Text = "Export to Excel...";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(778, 644);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(110, 28);
            this.btnClose.TabIndex = 5;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // CHWForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 684);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.btnHelp);
            this.Controls.Add(this.btnCalculate);
            this.Controls.Add(this.grpResults);
            this.Controls.Add(this.tabModels);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "CHWForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CHW VOLUME";
            this.Load += new System.EventHandler(this.CHWForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CHWForm_FormClosing);
            this.tabModels.ResumeLayout(false);
            this.tabLocal.ResumeLayout(false);
            this.tabCloud.ResumeLayout(false);
            this.grpResults.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvLocal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCloud)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabModels;
        private System.Windows.Forms.TabPage tabLocal;
        private System.Windows.Forms.DataGridView dgvLocal;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colLocalCheck;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLocalName;
        private System.Windows.Forms.DataGridViewComboBoxColumn colLocalSupply;
        private System.Windows.Forms.DataGridViewComboBoxColumn colLocalReturn;
        private System.Windows.Forms.Button btnLocalAll;
        private System.Windows.Forms.Button btnLocalNone;
        private System.Windows.Forms.Label lblLocalInfo;
        private System.Windows.Forms.TabPage tabCloud;
        private System.Windows.Forms.DataGridView dgvCloud;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colCloudCheck;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCloudName;
        private System.Windows.Forms.DataGridViewComboBoxColumn colCloudSupply;
        private System.Windows.Forms.DataGridViewComboBoxColumn colCloudReturn;
        private System.Windows.Forms.DataGridViewComboBoxColumn colCloudWorksets;
        private System.Windows.Forms.Button btnCloudAll;
        private System.Windows.Forms.Button btnCloudNone;
        private System.Windows.Forms.Button btnAddCloud;
        private System.Windows.Forms.Button btnRemoveCloud;
        private System.Windows.Forms.Button btnReadTypes;
        private System.Windows.Forms.Label lblCloudInfo;
        private System.Windows.Forms.GroupBox grpResults;
        private System.Windows.Forms.ListView lvResults;
        private System.Windows.Forms.ColumnHeader colModel;
        private System.Windows.Forms.ColumnHeader colSupplyType;
        private System.Windows.Forms.ColumnHeader colReturnType;
        private System.Windows.Forms.ColumnHeader colSupply;
        private System.Windows.Forms.ColumnHeader colReturn;
        private System.Windows.Forms.ColumnHeader colSupplyPipes;
        private System.Windows.Forms.ColumnHeader colReturnPipes;
        private System.Windows.Forms.ColumnHeader colTotal;
        private System.Windows.Forms.Label lblTotal;
        private System.Windows.Forms.Button btnCalculate;
        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnClose;
    }
}
