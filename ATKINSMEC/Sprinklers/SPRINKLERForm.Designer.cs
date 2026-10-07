
namespace ATKINSMEC
{
    partial class SPRINKLERForm
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
            this.grpSource = new System.Windows.Forms.GroupBox();
            this.clbLinks = new System.Windows.Forms.CheckedListBox();
            this.lblLinks = new System.Windows.Forms.Label();
            this.clbLevels = new System.Windows.Forms.CheckedListBox();
            this.lblLevels = new System.Windows.Forms.Label();
            this.btnLoadRooms = new System.Windows.Forms.Button();
            this.lblSourceInfo = new System.Windows.Forms.Label();
            this.grpHazard = new System.Windows.Forms.GroupBox();
            this.rdoAuto = new System.Windows.Forms.RadioButton();
            this.rdoManual = new System.Windows.Forms.RadioButton();
            this.lblSetAll = new System.Windows.Forms.Label();
            this.cmbSetAll = new System.Windows.Forms.ComboBox();
            this.btnSetAll = new System.Windows.Forms.Button();
            this.grpRooms = new System.Windows.Forms.GroupBox();
            this.dgvRooms = new System.Windows.Forms.DataGridView();
            this.btnAllRooms = new System.Windows.Forms.Button();
            this.btnNoRooms = new System.Windows.Forms.Button();
            this.btnCheckSel = new System.Windows.Forms.Button();
            this.btnUncheckSel = new System.Windows.Forms.Button();
            this.lblChecked = new System.Windows.Forms.Label();
            this.colUse = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colArea = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFree = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHazard = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSpacing = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPerHead = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCoverage = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCeiling = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colShow = new System.Windows.Forms.DataGridViewButtonColumn();
            this.lblFamily = new System.Windows.Forms.Label();
            this.cmbFamily = new System.Windows.Forms.ComboBox();
            this.lblHosting = new System.Windows.Forms.Label();
            this.cmbHosting = new System.Windows.Forms.ComboBox();
            this.lblPlacementInfo = new System.Windows.Forms.Label();
            this.lblClearance = new System.Windows.Forms.Label();
            this.txtClearance = new System.Windows.Forms.TextBox();
            this.lblFallback = new System.Windows.Forms.Label();
            this.txtFallback = new System.Windows.Forms.TextBox();
            this.btnPlace = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.lblTotals = new System.Windows.Forms.Label();
            this.grpSource.SuspendLayout();
            this.grpHazard.SuspendLayout();
            this.grpRooms.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRooms)).BeginInit();
            this.SuspendLayout();
            //
            // grpSource
            //
            this.grpSource.Controls.Add(this.lblLinks);
            this.grpSource.Controls.Add(this.clbLinks);
            this.grpSource.Controls.Add(this.lblLevels);
            this.grpSource.Controls.Add(this.clbLevels);
            this.grpSource.Controls.Add(this.btnLoadRooms);
            this.grpSource.Controls.Add(this.lblSourceInfo);
            this.grpSource.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpSource.Location = new System.Drawing.Point(12, 12);
            this.grpSource.Name = "grpSource";
            this.grpSource.Size = new System.Drawing.Size(940, 176);
            this.grpSource.TabIndex = 0;
            this.grpSource.TabStop = false;
            this.grpSource.Text = "1.  Linked architectural model and levels";
            //
            // lblLinks
            //
            this.lblLinks.Location = new System.Drawing.Point(12, 22);
            this.lblLinks.Name = "lblLinks";
            this.lblLinks.Size = new System.Drawing.Size(200, 18);
            this.lblLinks.TabIndex = 0;
            this.lblLinks.Text = "Linked models:";
            //
            // clbLinks
            //
            this.clbLinks.CheckOnClick = true;
            this.clbLinks.FormattingEnabled = true;
            this.clbLinks.Location = new System.Drawing.Point(12, 42);
            this.clbLinks.Name = "clbLinks";
            this.clbLinks.Size = new System.Drawing.Size(360, 94);
            this.clbLinks.TabIndex = 1;
            this.clbLinks.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.clbLinks_ItemCheck);
            //
            // lblLevels
            //
            this.lblLevels.Location = new System.Drawing.Point(392, 22);
            this.lblLevels.Name = "lblLevels";
            this.lblLevels.Size = new System.Drawing.Size(200, 18);
            this.lblLevels.TabIndex = 2;
            this.lblLevels.Text = "Floors:";
            //
            // clbLevels
            //
            this.clbLevels.CheckOnClick = true;
            this.clbLevels.FormattingEnabled = true;
            this.clbLevels.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbLevels.Location = new System.Drawing.Point(392, 42);
            this.clbLevels.Name = "clbLevels";
            this.clbLevels.Size = new System.Drawing.Size(360, 94);
            this.clbLevels.TabIndex = 3;
            //
            // btnLoadRooms
            //
            this.btnLoadRooms.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLoadRooms.Location = new System.Drawing.Point(772, 42);
            this.btnLoadRooms.Name = "btnLoadRooms";
            this.btnLoadRooms.Size = new System.Drawing.Size(156, 32);
            this.btnLoadRooms.TabIndex = 4;
            this.btnLoadRooms.Text = "2.  List rooms";
            this.btnLoadRooms.UseVisualStyleBackColor = true;
            this.btnLoadRooms.Click += new System.EventHandler(this.btnLoadRooms_Click);
            //
            // lblSourceInfo
            //
            this.lblSourceInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSourceInfo.Location = new System.Drawing.Point(772, 82);
            this.lblSourceInfo.Name = "lblSourceInfo";
            this.lblSourceInfo.Size = new System.Drawing.Size(156, 54);
            this.lblSourceInfo.TabIndex = 5;
            this.lblSourceInfo.Text = "";
            //
            // grpHazard
            //
            this.grpHazard.Controls.Add(this.rdoAuto);
            this.grpHazard.Controls.Add(this.rdoManual);
            this.grpHazard.Controls.Add(this.lblSetAll);
            this.grpHazard.Controls.Add(this.cmbSetAll);
            this.grpHazard.Controls.Add(this.btnSetAll);
            this.grpHazard.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpHazard.Location = new System.Drawing.Point(12, 194);
            this.grpHazard.Name = "grpHazard";
            this.grpHazard.Size = new System.Drawing.Size(940, 62);
            this.grpHazard.TabIndex = 1;
            this.grpHazard.TabStop = false;
            this.grpHazard.Text = "3.  NFPA 13 occupancy hazard";
            //
            // rdoAuto
            //
            this.rdoAuto.Checked = true;
            this.rdoAuto.Location = new System.Drawing.Point(12, 24);
            this.rdoAuto.Name = "rdoAuto";
            this.rdoAuto.Size = new System.Drawing.Size(260, 22);
            this.rdoAuto.TabIndex = 0;
            this.rdoAuto.TabStop = true;
            this.rdoAuto.Text = "Determine automatically from room name";
            this.rdoAuto.UseVisualStyleBackColor = true;
            this.rdoAuto.CheckedChanged += new System.EventHandler(this.rdoMode_CheckedChanged);
            //
            // rdoManual
            //
            this.rdoManual.Location = new System.Drawing.Point(284, 24);
            this.rdoManual.Name = "rdoManual";
            this.rdoManual.Size = new System.Drawing.Size(180, 22);
            this.rdoManual.TabIndex = 1;
            this.rdoManual.Text = "Set manually per room";
            this.rdoManual.UseVisualStyleBackColor = true;
            this.rdoManual.CheckedChanged += new System.EventHandler(this.rdoMode_CheckedChanged);
            //
            // lblSetAll
            //
            this.lblSetAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSetAll.Location = new System.Drawing.Point(494, 27);
            this.lblSetAll.Name = "lblSetAll";
            this.lblSetAll.Size = new System.Drawing.Size(130, 18);
            this.lblSetAll.TabIndex = 2;
            this.lblSetAll.Text = "Apply to every room:";
            //
            // cmbSetAll
            //
            this.cmbSetAll.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSetAll.FormattingEnabled = true;
            this.cmbSetAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbSetAll.Location = new System.Drawing.Point(624, 24);
            this.cmbSetAll.Name = "cmbSetAll";
            this.cmbSetAll.Size = new System.Drawing.Size(180, 21);
            this.cmbSetAll.TabIndex = 3;
            //
            // btnSetAll
            //
            this.btnSetAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSetAll.Location = new System.Drawing.Point(814, 22);
            this.btnSetAll.Name = "btnSetAll";
            this.btnSetAll.Size = new System.Drawing.Size(114, 26);
            this.btnSetAll.TabIndex = 4;
            this.btnSetAll.Text = "Apply";
            this.btnSetAll.UseVisualStyleBackColor = true;
            this.btnSetAll.Click += new System.EventHandler(this.btnSetAll_Click);
            //
            // grpRooms
            //
            this.grpRooms.Controls.Add(this.dgvRooms);
            this.grpRooms.Controls.Add(this.btnAllRooms);
            this.grpRooms.Controls.Add(this.btnNoRooms);
            this.grpRooms.Controls.Add(this.btnCheckSel);
            this.grpRooms.Controls.Add(this.btnUncheckSel);
            this.grpRooms.Controls.Add(this.lblChecked);
            this.grpRooms.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpRooms.Location = new System.Drawing.Point(12, 262);
            this.grpRooms.Name = "grpRooms";
            this.grpRooms.Size = new System.Drawing.Size(940, 340);
            this.grpRooms.TabIndex = 2;
            this.grpRooms.TabStop = false;
            this.grpRooms.Text = "4.  Rooms and the layout that will be placed";
            //
            // dgvRooms
            //
            this.dgvRooms.AllowUserToAddRows = false;
            this.dgvRooms.AllowUserToDeleteRows = false;
            this.dgvRooms.AllowUserToResizeRows = false;
            this.dgvRooms.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dgvRooms.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRooms.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colUse,
            this.colLevel,
            this.colNumber,
            this.colName,
            this.colArea,
            this.colFree,
            this.colHazard,
            this.colCount,
            this.colSpacing,
            this.colPerHead,
            this.colCoverage,
            this.colCeiling,
            this.colNote,
            this.colShow});
            this.dgvRooms.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvRooms.Location = new System.Drawing.Point(12, 22);
            this.dgvRooms.MultiSelect = true;
            this.dgvRooms.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvRooms.Name = "dgvRooms";
            this.dgvRooms.RowHeadersVisible = false;
            this.dgvRooms.Size = new System.Drawing.Size(916, 274);
            this.dgvRooms.TabIndex = 0;
            this.dgvRooms.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvRooms_CellValueChanged);
            this.dgvRooms.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvRooms_CellContentClick);
            this.dgvRooms.SelectionChanged += new System.EventHandler(this.dgvRooms_SelectionChanged);
            this.dgvRooms.CurrentCellDirtyStateChanged += new System.EventHandler(this.dgvRooms_CurrentCellDirtyStateChanged);
            //
            // columns
            //
            this.colUse.HeaderText = "";
            this.colUse.Name = "colUse";
            this.colUse.Width = 30;
            this.colUse.ToolTipText = "Include this room when placing";
            this.colLevel.HeaderText = "Floor";
            this.colLevel.Name = "colLevel";
            this.colLevel.ReadOnly = true;
            this.colLevel.Width = 120;
            this.colNumber.HeaderText = "No.";
            this.colNumber.Name = "colNumber";
            this.colNumber.ReadOnly = true;
            this.colNumber.Width = 55;
            this.colName.HeaderText = "Room";
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            this.colName.Width = 150;
            this.colArea.HeaderText = "Area m2";
            this.colArea.Name = "colArea";
            this.colArea.ReadOnly = true;
            this.colArea.Width = 65;
            this.colFree.HeaderText = "Free m2";
            this.colFree.Name = "colFree";
            this.colFree.ReadOnly = true;
            this.colFree.Width = 65;
            this.colFree.ToolTipText = "Room area left after columns and walls are removed. Well below the room area means obstacles are cutting into it.";
            this.colHazard.HeaderText = "Hazard";
            this.colHazard.Name = "colHazard";
            this.colHazard.Width = 95;
            this.colCount.HeaderText = "Heads";
            this.colCount.Name = "colCount";
            this.colCount.ReadOnly = true;
            this.colCount.Width = 50;
            this.colSpacing.HeaderText = "Spacing m";
            this.colSpacing.Name = "colSpacing";
            this.colSpacing.ReadOnly = true;
            this.colSpacing.Width = 85;
            this.colPerHead.HeaderText = "m2/head";
            this.colPerHead.Name = "colPerHead";
            this.colPerHead.ReadOnly = true;
            this.colPerHead.Width = 65;
            this.colCoverage.HeaderText = "Covered";
            this.colCoverage.Name = "colCoverage";
            this.colCoverage.ReadOnly = true;
            this.colCoverage.Width = 60;
            this.colCeiling.HeaderText = "Ceiling m";
            this.colCeiling.Name = "colCeiling";
            this.colCeiling.ReadOnly = true;
            this.colCeiling.Width = 65;
            this.colNote.HeaderText = "Notes";
            this.colNote.Name = "colNote";
            this.colNote.ReadOnly = true;
            this.colNote.Width = 140;
            this.colNote.MinimumWidth = 140;
            this.colNote.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colNote.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.colShow.HeaderText = "";
            this.colShow.Name = "colShow";
            this.colShow.Text = "Show";
            this.colShow.UseColumnTextForButtonValue = true;
            this.colShow.Width = 55;
            this.colShow.ToolTipText = "Open this room in the Revit view";
            //
            // lblFamily
            //
            this.btnAllRooms.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAllRooms.Location = new System.Drawing.Point(12, 302);
            this.btnAllRooms.Name = "btnAllRooms";
            this.btnAllRooms.Size = new System.Drawing.Size(110, 25);
            this.btnAllRooms.TabIndex = 1;
            this.btnAllRooms.Text = "Check all";
            this.btnAllRooms.UseVisualStyleBackColor = true;
            this.btnAllRooms.Click += new System.EventHandler(this.btnAllRooms_Click);
            this.btnNoRooms.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnNoRooms.Location = new System.Drawing.Point(128, 302);
            this.btnNoRooms.Name = "btnNoRooms";
            this.btnNoRooms.Size = new System.Drawing.Size(110, 25);
            this.btnNoRooms.TabIndex = 2;
            this.btnNoRooms.Text = "Uncheck all";
            this.btnNoRooms.UseVisualStyleBackColor = true;
            this.btnNoRooms.Click += new System.EventHandler(this.btnNoRooms_Click);
            this.btnCheckSel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnCheckSel.Location = new System.Drawing.Point(244, 302);
            this.btnCheckSel.Name = "btnCheckSel";
            this.btnCheckSel.Size = new System.Drawing.Size(124, 25);
            this.btnCheckSel.TabIndex = 4;
            this.btnCheckSel.Text = "Check selected";
            this.btnCheckSel.UseVisualStyleBackColor = true;
            this.btnCheckSel.Click += new System.EventHandler(this.btnCheckSel_Click);
            this.btnUncheckSel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnUncheckSel.Location = new System.Drawing.Point(374, 302);
            this.btnUncheckSel.Name = "btnUncheckSel";
            this.btnUncheckSel.Size = new System.Drawing.Size(124, 25);
            this.btnUncheckSel.TabIndex = 5;
            this.btnUncheckSel.Text = "Uncheck selected";
            this.btnUncheckSel.UseVisualStyleBackColor = true;
            this.btnUncheckSel.Click += new System.EventHandler(this.btnUncheckSel_Click);
            this.lblChecked.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblChecked.Location = new System.Drawing.Point(508, 307);
            this.lblChecked.Name = "lblChecked";
            this.lblChecked.Size = new System.Drawing.Size(420, 18);
            this.lblChecked.TabIndex = 3;
            this.lblChecked.Text = "";
            this.lblFamily.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblFamily.Location = new System.Drawing.Point(12, 618);
            this.lblFamily.Name = "lblFamily";
            this.lblFamily.Size = new System.Drawing.Size(104, 18);
            this.lblFamily.TabIndex = 3;
            this.lblFamily.Text = "Sprinkler family:";
            //
            // cmbFamily
            //
            this.cmbFamily.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFamily.FormattingEnabled = true;
            this.cmbFamily.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cmbFamily.Location = new System.Drawing.Point(116, 615);
            this.cmbFamily.Name = "cmbFamily";
            this.cmbFamily.Size = new System.Drawing.Size(360, 21);
            this.cmbFamily.TabIndex = 4;
            this.cmbFamily.SelectedIndexChanged += new System.EventHandler(this.cmbFamily_SelectedIndexChanged);
            //
            // lblHosting
            //
            this.lblHosting.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblHosting.Location = new System.Drawing.Point(496, 618);
            this.lblHosting.Name = "lblHosting";
            this.lblHosting.Size = new System.Drawing.Size(54, 18);
            this.lblHosting.TabIndex = 5;
            this.lblHosting.Text = "Hosting:";
            //
            // cmbHosting
            //
            this.cmbHosting.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbHosting.FormattingEnabled = true;
            this.cmbHosting.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cmbHosting.Location = new System.Drawing.Point(552, 615);
            this.cmbHosting.Name = "cmbHosting";
            this.cmbHosting.Size = new System.Drawing.Size(180, 21);
            this.cmbHosting.TabIndex = 6;
            //
            // lblPlacementInfo
            //
            this.lblPlacementInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblPlacementInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblPlacementInfo.Location = new System.Drawing.Point(116, 638);
            this.lblPlacementInfo.Name = "lblPlacementInfo";
            this.lblPlacementInfo.Size = new System.Drawing.Size(360, 18);
            this.lblPlacementInfo.TabIndex = 10;
            this.lblPlacementInfo.Text = "";
            //
            // lblFallback
            //
            this.lblClearance.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lblClearance.Location = new System.Drawing.Point(748, 618);
            this.lblClearance.Name = "lblClearance";
            this.lblClearance.Size = new System.Drawing.Size(140, 18);
            this.lblClearance.TabIndex = 13;
            this.lblClearance.Text = "Clear of walls (mm):";
            this.txtClearance.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.txtClearance.Location = new System.Drawing.Point(890, 615);
            this.txtClearance.Name = "txtClearance";
            this.txtClearance.Size = new System.Drawing.Size(56, 20);
            this.txtClearance.TabIndex = 14;
            this.txtClearance.Text = "150";
            this.lblFallback.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblFallback.Location = new System.Drawing.Point(496, 641);
            this.lblFallback.Name = "lblFallback";
            this.lblFallback.Size = new System.Drawing.Size(178, 18);
            this.lblFallback.TabIndex = 11;
            this.lblFallback.Text = "Ceiling height if none found (m):";
            //
            // txtFallback
            //
            this.txtFallback.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.txtFallback.Location = new System.Drawing.Point(676, 638);
            this.txtFallback.Name = "txtFallback";
            this.txtFallback.Size = new System.Drawing.Size(56, 20);
            this.txtFallback.TabIndex = 12;
            this.txtFallback.Text = "2.7";
            //
            // btnPlace
            //
            this.btnPlace.Enabled = false;
            this.btnPlace.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPlace.Location = new System.Drawing.Point(740, 688);
            this.btnPlace.Name = "btnPlace";
            this.btnPlace.Size = new System.Drawing.Size(100, 30);
            this.btnPlace.TabIndex = 7;
            this.btnPlace.Text = "5.  Place";
            this.btnPlace.UseVisualStyleBackColor = true;
            this.btnPlace.Click += new System.EventHandler(this.btnPlace_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(852, 688);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 30);
            this.btnClose.TabIndex = 8;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // lblTotals
            //
            this.lblTotals.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTotals.Location = new System.Drawing.Point(12, 664);
            this.lblTotals.Name = "lblTotals";
            this.lblTotals.Size = new System.Drawing.Size(940, 20);
            this.lblTotals.TabIndex = 9;
            this.lblTotals.Text = "";
            //
            // SPRINKLERForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(964, 730);
            this.Controls.Add(this.lblTotals);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnPlace);
            this.Controls.Add(this.txtFallback);
            this.Controls.Add(this.lblFallback);
            this.Controls.Add(this.lblPlacementInfo);
            this.Controls.Add(this.txtClearance);
            this.Controls.Add(this.lblClearance);
            this.Controls.Add(this.cmbHosting);
            this.Controls.Add(this.lblHosting);
            this.Controls.Add(this.cmbFamily);
            this.Controls.Add(this.lblFamily);
            this.Controls.Add(this.grpRooms);
            this.Controls.Add(this.grpHazard);
            this.Controls.Add(this.grpSource);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "SPRINKLERForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "SPRINKLERS";
            this.Load += new System.EventHandler(this.SPRINKLERForm_Load);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.SPRINKLERForm_FormClosed);
            this.grpSource.ResumeLayout(false);
            this.grpHazard.ResumeLayout(false);
            this.grpRooms.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvRooms)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpSource;
        private System.Windows.Forms.Label lblLinks;
        private System.Windows.Forms.CheckedListBox clbLinks;
        private System.Windows.Forms.Label lblLevels;
        private System.Windows.Forms.CheckedListBox clbLevels;
        private System.Windows.Forms.Button btnLoadRooms;
        private System.Windows.Forms.Label lblSourceInfo;
        private System.Windows.Forms.GroupBox grpHazard;
        private System.Windows.Forms.RadioButton rdoAuto;
        private System.Windows.Forms.RadioButton rdoManual;
        private System.Windows.Forms.Label lblSetAll;
        private System.Windows.Forms.ComboBox cmbSetAll;
        private System.Windows.Forms.Button btnSetAll;
        private System.Windows.Forms.GroupBox grpRooms;
        private System.Windows.Forms.DataGridView dgvRooms;
        private System.Windows.Forms.Button btnAllRooms;
        private System.Windows.Forms.Button btnNoRooms;
        private System.Windows.Forms.Button btnCheckSel;
        private System.Windows.Forms.Button btnUncheckSel;
        private System.Windows.Forms.Label lblChecked;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colUse;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLevel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colArea;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFree;
        private System.Windows.Forms.DataGridViewComboBoxColumn colHazard;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSpacing;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPerHead;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCoverage;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCeiling;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNote;
        private System.Windows.Forms.DataGridViewButtonColumn colShow;
        private System.Windows.Forms.Label lblFamily;
        private System.Windows.Forms.ComboBox cmbFamily;
        private System.Windows.Forms.Label lblHosting;
        private System.Windows.Forms.ComboBox cmbHosting;
        private System.Windows.Forms.Label lblPlacementInfo;
        private System.Windows.Forms.Label lblClearance;
        private System.Windows.Forms.TextBox txtClearance;
        private System.Windows.Forms.Label lblFallback;
        private System.Windows.Forms.TextBox txtFallback;
        private System.Windows.Forms.Button btnPlace;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblTotals;
    }
}
