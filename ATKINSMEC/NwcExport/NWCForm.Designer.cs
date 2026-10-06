namespace ATKINSMEC
{
    partial class NWCForm
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

        private void InitializeComponent()
        {
            this.grpModels = new System.Windows.Forms.GroupBox();
            this.dgvModels = new System.Windows.Forms.DataGridView();
            this.btnAddFiles = new System.Windows.Forms.Button();
            this.btnAddAcc = new System.Windows.Forms.Button();
            this.btnAddOpen = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnTickAll = new System.Windows.Forms.Button();
            this.btnTickNone = new System.Windows.Forms.Button();
            this.btnJobSettings = new System.Windows.Forms.Button();
            this.btnJobWorksets = new System.Windows.Forms.Button();
            this.grpWhere = new System.Windows.Forms.GroupBox();
            this.lblFolder = new System.Windows.Forms.Label();
            this.txtFolder = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.lblView = new System.Windows.Forms.Label();
            this.cboView = new System.Windows.Forms.ComboBox();
            this.lblPrefix = new System.Windows.Forms.Label();
            this.txtPrefix = new System.Windows.Forms.TextBox();
            this.lblSuffix = new System.Windows.Forms.Label();
            this.txtSuffix = new System.Windows.Forms.TextBox();
            this.grpOptions = new System.Windows.Forms.GroupBox();
            this.chkShared = new System.Windows.Forms.CheckBox();
            this.chkLinks = new System.Windows.Forms.CheckBox();
            this.chkLevels = new System.Windows.Forms.CheckBox();
            this.chkIds = new System.Windows.Forms.CheckBox();
            this.chkRooms = new System.Windows.Forms.CheckBox();
            this.chkParts = new System.Windows.Forms.CheckBox();
            this.chkProperties = new System.Windows.Forms.CheckBox();
            this.chkSkip = new System.Windows.Forms.CheckBox();
            this.lblParams = new System.Windows.Forms.Label();
            this.cboParams = new System.Windows.Forms.ComboBox();
            this.lblWorksets = new System.Windows.Forms.Label();
            this.cboWorksets = new System.Windows.Forms.ComboBox();
            this.lblFacet = new System.Windows.Forms.Label();
            this.numFacet = new System.Windows.Forms.NumericUpDown();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.prgExport = new System.Windows.Forms.ProgressBar();
            this.chkSilent = new System.Windows.Forms.CheckBox();
            this.lblProgress = new System.Windows.Forms.Label();
            this.grpResults = new System.Windows.Forms.GroupBox();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.lblSummary = new System.Windows.Forms.Label();
            this.btnImportSettings = new System.Windows.Forms.Button();
            this.btnExportSettings = new System.Windows.Forms.Button();
            this.btnHelp = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.grpModels.SuspendLayout();
            this.grpWhere.SuspendLayout();
            this.grpOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvModels)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFacet)).BeginInit();
            this.grpResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).BeginInit();
            this.SuspendLayout();
            //
            // grpModels
            //
            this.grpModels.Controls.Add(this.dgvModels);
            this.grpModels.Controls.Add(this.btnAddFiles);
            this.grpModels.Controls.Add(this.btnAddAcc);
            this.grpModels.Controls.Add(this.btnAddOpen);
            this.grpModels.Controls.Add(this.btnRemove);
            this.grpModels.Controls.Add(this.btnTickAll);
            this.grpModels.Controls.Add(this.btnTickNone);
            this.grpModels.Controls.Add(this.btnJobSettings);
            this.grpModels.Controls.Add(this.btnJobWorksets);
            this.grpModels.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpModels.Location = new System.Drawing.Point(10, 10);
            this.grpModels.Name = "grpModels";
            this.grpModels.Size = new System.Drawing.Size(950, 240);
            this.grpModels.TabIndex = 0;
            this.grpModels.TabStop = false;
            this.grpModels.Text = "1. Models to export";
            //
            // dgvModels
            //
            this.dgvModels.AllowUserToAddRows = false;
            this.dgvModels.AllowUserToDeleteRows = false;
            this.dgvModels.AllowUserToResizeRows = false;
            this.dgvModels.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvModels.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvModels.Location = new System.Drawing.Point(12, 24);
            this.dgvModels.MultiSelect = false;
            this.dgvModels.Name = "dgvModels";
            this.dgvModels.RowHeadersVisible = false;
            this.dgvModels.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvModels.Size = new System.Drawing.Size(700, 204);
            this.dgvModels.TabIndex = 0;
            //
            // btnAddFiles
            //
            this.btnAddFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddFiles.Location = new System.Drawing.Point(722, 24);
            this.btnAddFiles.Name = "btnAddFiles";
            this.btnAddFiles.Size = new System.Drawing.Size(214, 28);
            this.btnAddFiles.TabIndex = 1;
            this.btnAddFiles.Text = "Add Revit files...";
            this.btnAddFiles.UseVisualStyleBackColor = true;
            this.btnAddFiles.Click += new System.EventHandler(this.btnAddFiles_Click);
            //
            // btnAddAcc
            //
            this.btnAddAcc.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddAcc.Location = new System.Drawing.Point(722, 58);
            this.btnAddAcc.Name = "btnAddAcc";
            this.btnAddAcc.Size = new System.Drawing.Size(214, 28);
            this.btnAddAcc.TabIndex = 2;
            this.btnAddAcc.Text = "Add from ACC...";
            this.btnAddAcc.UseVisualStyleBackColor = true;
            this.btnAddAcc.Click += new System.EventHandler(this.btnAddAcc_Click);
            //
            // btnAddOpen
            //
            this.btnAddOpen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddOpen.Location = new System.Drawing.Point(722, 92);
            this.btnAddOpen.Name = "btnAddOpen";
            this.btnAddOpen.Size = new System.Drawing.Size(214, 28);
            this.btnAddOpen.TabIndex = 3;
            this.btnAddOpen.Text = "Add the model open now";
            this.btnAddOpen.UseVisualStyleBackColor = true;
            this.btnAddOpen.Click += new System.EventHandler(this.btnAddOpen_Click);
            //
            // btnRemove
            //
            this.btnRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemove.Location = new System.Drawing.Point(722, 132);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(214, 28);
            this.btnRemove.TabIndex = 4;
            this.btnRemove.Text = "Remove the picked row";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            //
            // btnTickAll
            //
            this.btnTickAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnTickAll.Location = new System.Drawing.Point(722, 170);
            this.btnTickAll.Name = "btnTickAll";
            this.btnTickAll.Size = new System.Drawing.Size(104, 28);
            this.btnTickAll.TabIndex = 5;
            this.btnTickAll.Text = "Tick all";
            this.btnTickAll.UseVisualStyleBackColor = true;
            this.btnTickAll.Click += new System.EventHandler(this.btnTickAll_Click);
            //
            // btnTickNone
            //
            this.btnTickNone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnTickNone.Location = new System.Drawing.Point(832, 170);
            this.btnTickNone.Name = "btnTickNone";
            this.btnTickNone.Size = new System.Drawing.Size(104, 28);
            this.btnTickNone.TabIndex = 6;
            this.btnTickNone.Text = "None";
            this.btnTickNone.UseVisualStyleBackColor = true;
            this.btnTickNone.Click += new System.EventHandler(this.btnTickNone_Click);
            //
            // btnJobSettings
            //
            this.btnJobSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnJobSettings.Location = new System.Drawing.Point(722, 204);
            this.btnJobSettings.Name = "btnJobSettings";
            this.btnJobSettings.Size = new System.Drawing.Size(104, 28);
            this.btnJobSettings.TabIndex = 7;
            this.btnJobSettings.Text = "Settings...";
            this.btnJobSettings.UseVisualStyleBackColor = true;
            this.btnJobSettings.Click += new System.EventHandler(this.btnJobSettings_Click);
            //
            // btnJobWorksets
            //
            this.btnJobWorksets.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnJobWorksets.Location = new System.Drawing.Point(832, 204);
            this.btnJobWorksets.Name = "btnJobWorksets";
            this.btnJobWorksets.Size = new System.Drawing.Size(104, 28);
            this.btnJobWorksets.TabIndex = 8;
            this.btnJobWorksets.Text = "Worksets...";
            this.btnJobWorksets.UseVisualStyleBackColor = true;
            this.btnJobWorksets.Click += new System.EventHandler(this.btnJobWorksets_Click);
            //
            // grpWhere
            //
            this.grpWhere.Controls.Add(this.lblFolder);
            this.grpWhere.Controls.Add(this.txtFolder);
            this.grpWhere.Controls.Add(this.btnBrowse);
            this.grpWhere.Controls.Add(this.lblView);
            this.grpWhere.Controls.Add(this.cboView);
            this.grpWhere.Controls.Add(this.lblPrefix);
            this.grpWhere.Controls.Add(this.txtPrefix);
            this.grpWhere.Controls.Add(this.lblSuffix);
            this.grpWhere.Controls.Add(this.txtSuffix);
            this.grpWhere.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpWhere.Location = new System.Drawing.Point(10, 258);
            this.grpWhere.Name = "grpWhere";
            this.grpWhere.Size = new System.Drawing.Size(950, 96);
            this.grpWhere.TabIndex = 1;
            this.grpWhere.TabStop = false;
            this.grpWhere.Text = "2. Where it goes, and what is in it";
            //
            // lblFolder
            //
            this.lblFolder.Location = new System.Drawing.Point(12, 28);
            this.lblFolder.Name = "lblFolder";
            this.lblFolder.Size = new System.Drawing.Size(90, 20);
            this.lblFolder.TabIndex = 0;
            this.lblFolder.Text = "Folder";
            //
            // txtFolder
            //
            this.txtFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtFolder.Location = new System.Drawing.Point(105, 25);
            this.txtFolder.Name = "txtFolder";
            this.txtFolder.Size = new System.Drawing.Size(618, 23);
            this.txtFolder.TabIndex = 1;
            //
            // btnBrowse
            //
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.Location = new System.Drawing.Point(733, 24);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(120, 26);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            //
            // lblView
            //
            this.lblView.Location = new System.Drawing.Point(12, 62);
            this.lblView.Name = "lblView";
            this.lblView.Size = new System.Drawing.Size(90, 20);
            this.lblView.TabIndex = 3;
            this.lblView.Text = "3D view";
            //
            // cboView
            //
            this.cboView.FormattingEnabled = true;
            this.cboView.Location = new System.Drawing.Point(105, 59);
            this.cboView.Name = "cboView";
            this.cboView.Size = new System.Drawing.Size(300, 23);
            this.cboView.TabIndex = 4;
            //
            // lblPrefix
            //
            this.lblPrefix.Location = new System.Drawing.Point(425, 62);
            this.lblPrefix.Name = "lblPrefix";
            this.lblPrefix.Size = new System.Drawing.Size(48, 20);
            this.lblPrefix.TabIndex = 5;
            this.lblPrefix.Text = "Prefix";
            //
            // txtPrefix
            //
            this.txtPrefix.Location = new System.Drawing.Point(475, 59);
            this.txtPrefix.Name = "txtPrefix";
            this.txtPrefix.Size = new System.Drawing.Size(120, 23);
            this.txtPrefix.TabIndex = 6;
            //
            // lblSuffix
            //
            this.lblSuffix.Location = new System.Drawing.Point(610, 62);
            this.lblSuffix.Name = "lblSuffix";
            this.lblSuffix.Size = new System.Drawing.Size(48, 20);
            this.lblSuffix.TabIndex = 7;
            this.lblSuffix.Text = "Suffix";
            //
            // txtSuffix
            //
            this.txtSuffix.Location = new System.Drawing.Point(660, 59);
            this.txtSuffix.Name = "txtSuffix";
            this.txtSuffix.Size = new System.Drawing.Size(120, 23);
            this.txtSuffix.TabIndex = 8;
            //
            // grpOptions
            //
            this.grpOptions.Controls.Add(this.chkShared);
            this.grpOptions.Controls.Add(this.chkLinks);
            this.grpOptions.Controls.Add(this.chkLevels);
            this.grpOptions.Controls.Add(this.chkIds);
            this.grpOptions.Controls.Add(this.chkRooms);
            this.grpOptions.Controls.Add(this.chkParts);
            this.grpOptions.Controls.Add(this.chkProperties);
            this.grpOptions.Controls.Add(this.chkSkip);
            this.grpOptions.Controls.Add(this.lblParams);
            this.grpOptions.Controls.Add(this.cboParams);
            this.grpOptions.Controls.Add(this.lblWorksets);
            this.grpOptions.Controls.Add(this.cboWorksets);
            this.grpOptions.Controls.Add(this.lblFacet);
            this.grpOptions.Controls.Add(this.numFacet);
            this.grpOptions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpOptions.Location = new System.Drawing.Point(10, 360);
            this.grpOptions.Name = "grpOptions";
            this.grpOptions.Size = new System.Drawing.Size(950, 110);
            this.grpOptions.TabIndex = 2;
            this.grpOptions.TabStop = false;
            this.grpOptions.Text = "3. How to write it";
            //
            // chkShared
            //
            this.chkShared.Location = new System.Drawing.Point(12, 24);
            this.chkShared.Name = "chkShared";
            this.chkShared.Size = new System.Drawing.Size(220, 20);
            this.chkShared.TabIndex = 0;
            this.chkShared.Text = "Shared coordinates";
            this.chkShared.UseVisualStyleBackColor = true;
            //
            // chkLinks
            //
            this.chkLinks.Location = new System.Drawing.Point(12, 50);
            this.chkLinks.Name = "chkLinks";
            this.chkLinks.Size = new System.Drawing.Size(220, 20);
            this.chkLinks.TabIndex = 1;
            this.chkLinks.Text = "Include linked models";
            this.chkLinks.UseVisualStyleBackColor = true;
            //
            // chkLevels
            //
            this.chkLevels.Location = new System.Drawing.Point(12, 76);
            this.chkLevels.Name = "chkLevels";
            this.chkLevels.Size = new System.Drawing.Size(220, 20);
            this.chkLevels.TabIndex = 2;
            this.chkLevels.Text = "Divide the file into levels";
            this.chkLevels.UseVisualStyleBackColor = true;
            //
            // chkIds
            //
            this.chkIds.Location = new System.Drawing.Point(240, 24);
            this.chkIds.Name = "chkIds";
            this.chkIds.Size = new System.Drawing.Size(210, 20);
            this.chkIds.TabIndex = 3;
            this.chkIds.Text = "Element ids";
            this.chkIds.UseVisualStyleBackColor = true;
            //
            // chkRooms
            //
            this.chkRooms.Location = new System.Drawing.Point(240, 50);
            this.chkRooms.Name = "chkRooms";
            this.chkRooms.Size = new System.Drawing.Size(210, 20);
            this.chkRooms.TabIndex = 4;
            this.chkRooms.Text = "Rooms as attributes";
            this.chkRooms.UseVisualStyleBackColor = true;
            //
            // chkParts
            //
            this.chkParts.Location = new System.Drawing.Point(240, 76);
            this.chkParts.Name = "chkParts";
            this.chkParts.Size = new System.Drawing.Size(210, 20);
            this.chkParts.TabIndex = 5;
            this.chkParts.Text = "Parts instead of originals";
            this.chkParts.UseVisualStyleBackColor = true;
            //
            // chkProperties
            //
            this.chkProperties.Location = new System.Drawing.Point(458, 24);
            this.chkProperties.Name = "chkProperties";
            this.chkProperties.Size = new System.Drawing.Size(230, 20);
            this.chkProperties.TabIndex = 6;
            this.chkProperties.Text = "Convert element properties";
            this.chkProperties.UseVisualStyleBackColor = true;
            //
            // chkSkip
            //
            this.chkSkip.Location = new System.Drawing.Point(458, 50);
            this.chkSkip.Name = "chkSkip";
            this.chkSkip.Size = new System.Drawing.Size(300, 20);
            this.chkSkip.TabIndex = 7;
            this.chkSkip.Text = "Skip a file already newer than its model";
            this.chkSkip.UseVisualStyleBackColor = true;
            //
            // lblParams
            //
            this.lblParams.Location = new System.Drawing.Point(458, 79);
            this.lblParams.Name = "lblParams";
            this.lblParams.Size = new System.Drawing.Size(80, 20);
            this.lblParams.TabIndex = 8;
            this.lblParams.Text = "Parameters";
            //
            // cboParams
            //
            this.cboParams.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboParams.FormattingEnabled = true;
            this.cboParams.Location = new System.Drawing.Point(540, 76);
            this.cboParams.Name = "cboParams";
            this.cboParams.Size = new System.Drawing.Size(148, 23);
            this.cboParams.TabIndex = 9;
            //
            // lblWorksets
            //
            this.lblWorksets.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblWorksets.Location = new System.Drawing.Point(712, 27);
            this.lblWorksets.Name = "lblWorksets";
            this.lblWorksets.Size = new System.Drawing.Size(70, 20);
            this.lblWorksets.TabIndex = 10;
            this.lblWorksets.Text = "Worksets";
            //
            // cboWorksets
            //
            this.cboWorksets.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboWorksets.FormattingEnabled = true;
            this.cboWorksets.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cboWorksets.Location = new System.Drawing.Point(788, 24);
            this.cboWorksets.Name = "cboWorksets";
            this.cboWorksets.Size = new System.Drawing.Size(148, 23);
            this.cboWorksets.TabIndex = 11;
            //
            // lblFacet
            //
            this.lblFacet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFacet.Location = new System.Drawing.Point(712, 53);
            this.lblFacet.Name = "lblFacet";
            this.lblFacet.Size = new System.Drawing.Size(70, 20);
            this.lblFacet.TabIndex = 12;
            this.lblFacet.Text = "Faceting";
            //
            // numFacet
            //
            this.numFacet.DecimalPlaces = 1;
            this.numFacet.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            this.numFacet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.numFacet.Location = new System.Drawing.Point(788, 51);
            this.numFacet.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numFacet.Name = "numFacet";
            this.numFacet.Size = new System.Drawing.Size(70, 23);
            this.numFacet.TabIndex = 13;
            this.numFacet.Value = new decimal(new int[] { 1, 0, 0, 0 });
            //
            // btnExport
            //
            this.btnExport.Location = new System.Drawing.Point(10, 480);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(250, 32);
            this.btnExport.TabIndex = 3;
            this.btnExport.Text = "Export the ticked models...";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            //
            // btnStop
            //
            this.btnStop.Enabled = false;
            this.btnStop.Location = new System.Drawing.Point(270, 480);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(120, 32);
            this.btnStop.TabIndex = 4;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            //
            // lblProgress
            //
            this.lblProgress.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblProgress.Location = new System.Drawing.Point(10, 516);
            this.lblProgress.Name = "lblProgress";
            this.lblProgress.Size = new System.Drawing.Size(950, 20);
            this.lblProgress.TabIndex = 5;
            this.lblProgress.Text = "";
            //
            // prgExport
            //
            this.prgExport.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.prgExport.Location = new System.Drawing.Point(706, 486);
            this.prgExport.Name = "prgExport";
            this.prgExport.Size = new System.Drawing.Size(254, 22);
            this.prgExport.TabIndex = 3;
            this.prgExport.Visible = false;
            //
            // chkSilent
            //
            this.chkSilent.Location = new System.Drawing.Point(400, 486);
            this.chkSilent.Name = "chkSilent";
            this.chkSilent.Size = new System.Drawing.Size(296, 22);
            this.chkSilent.TabIndex = 2;
            this.chkSilent.Text = "Run silently - answer Revit's own messages";
            this.chkSilent.UseVisualStyleBackColor = true;
            //
            // grpResults
            //
            this.grpResults.Controls.Add(this.dgvResults);
            this.grpResults.Controls.Add(this.lblSummary);
            this.grpResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpResults.Location = new System.Drawing.Point(10, 546);
            this.grpResults.Name = "grpResults";
            this.grpResults.Size = new System.Drawing.Size(950, 200);
            this.grpResults.TabIndex = 6;
            this.grpResults.TabStop = false;
            this.grpResults.Text = "4. What happened";
            //
            // dgvResults
            //
            this.dgvResults.AllowUserToAddRows = false;
            this.dgvResults.AllowUserToDeleteRows = false;
            this.dgvResults.AllowUserToResizeRows = false;
            this.dgvResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvResults.Location = new System.Drawing.Point(12, 24);
            this.dgvResults.MultiSelect = true;
            this.dgvResults.Name = "dgvResults";
            this.dgvResults.ReadOnly = true;
            this.dgvResults.RowHeadersVisible = false;
            this.dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvResults.Size = new System.Drawing.Size(924, 144);
            this.dgvResults.TabIndex = 0;
            //
            // lblSummary
            //
            this.lblSummary.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummary.Location = new System.Drawing.Point(12, 172);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(924, 20);
            this.lblSummary.TabIndex = 1;
            this.lblSummary.Text = "";
            //
            // btnImportSettings
            //
            this.btnImportSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnImportSettings.Location = new System.Drawing.Point(190, 756);
            this.btnImportSettings.Name = "btnImportSettings";
            this.btnImportSettings.Size = new System.Drawing.Size(200, 30);
            this.btnImportSettings.TabIndex = 9;
            this.btnImportSettings.Text = "Import settings...";
            this.btnImportSettings.UseVisualStyleBackColor = true;
            this.btnImportSettings.Click += new System.EventHandler(this.btnImportSettings_Click);
            //
            // btnExportSettings
            //
            this.btnExportSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnExportSettings.Location = new System.Drawing.Point(400, 756);
            this.btnExportSettings.Name = "btnExportSettings";
            this.btnExportSettings.Size = new System.Drawing.Size(200, 30);
            this.btnExportSettings.TabIndex = 10;
            this.btnExportSettings.Text = "Export settings...";
            this.btnExportSettings.UseVisualStyleBackColor = true;
            this.btnExportSettings.Click += new System.EventHandler(this.btnExportSettings_Click);
            //
            // btnHelp
            //
            this.btnHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnHelp.Location = new System.Drawing.Point(10, 756);
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(160, 30);
            this.btnHelp.TabIndex = 7;
            this.btnHelp.Text = "How to use this";
            this.btnHelp.UseVisualStyleBackColor = true;
            this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(820, 756);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(140, 30);
            this.btnClose.TabIndex = 8;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // NWCForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(972, 798);
            this.Controls.Add(this.grpModels);
            this.Controls.Add(this.grpWhere);
            this.Controls.Add(this.grpOptions);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.prgExport);
            this.Controls.Add(this.chkSilent);
            this.Controls.Add(this.lblProgress);
            this.Controls.Add(this.grpResults);
            this.Controls.Add(this.btnImportSettings);
            this.Controls.Add(this.btnExportSettings);
            this.Controls.Add(this.btnHelp);
            this.Controls.Add(this.btnClose);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "NWCForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NWC BATCH EXPORTER";
            this.Load += new System.EventHandler(this.NWCForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvModels)).EndInit();
            this.grpModels.ResumeLayout(false);
            this.grpWhere.ResumeLayout(false);
            this.grpWhere.PerformLayout();
            this.grpOptions.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numFacet)).EndInit();
            this.grpResults.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.GroupBox grpModels;
        private System.Windows.Forms.DataGridView dgvModels;
        private System.Windows.Forms.Button btnAddFiles;
        private System.Windows.Forms.Button btnAddAcc;
        private System.Windows.Forms.Button btnAddOpen;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnTickAll;
        private System.Windows.Forms.Button btnTickNone;
        private System.Windows.Forms.Button btnJobSettings;
        private System.Windows.Forms.Button btnJobWorksets;
        private System.Windows.Forms.GroupBox grpWhere;
        private System.Windows.Forms.Label lblFolder;
        private System.Windows.Forms.TextBox txtFolder;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label lblView;
        private System.Windows.Forms.ComboBox cboView;
        private System.Windows.Forms.Label lblPrefix;
        private System.Windows.Forms.TextBox txtPrefix;
        private System.Windows.Forms.Label lblSuffix;
        private System.Windows.Forms.TextBox txtSuffix;
        private System.Windows.Forms.GroupBox grpOptions;
        private System.Windows.Forms.CheckBox chkShared;
        private System.Windows.Forms.CheckBox chkLinks;
        private System.Windows.Forms.CheckBox chkLevels;
        private System.Windows.Forms.CheckBox chkIds;
        private System.Windows.Forms.CheckBox chkRooms;
        private System.Windows.Forms.CheckBox chkParts;
        private System.Windows.Forms.CheckBox chkProperties;
        private System.Windows.Forms.CheckBox chkSkip;
        private System.Windows.Forms.Label lblParams;
        private System.Windows.Forms.ComboBox cboParams;
        private System.Windows.Forms.Label lblWorksets;
        private System.Windows.Forms.ComboBox cboWorksets;
        private System.Windows.Forms.Label lblFacet;
        private System.Windows.Forms.NumericUpDown numFacet;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.ProgressBar prgExport;
        private System.Windows.Forms.CheckBox chkSilent;
        private System.Windows.Forms.Label lblProgress;
        private System.Windows.Forms.GroupBox grpResults;
        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.Button btnImportSettings;
        private System.Windows.Forms.Button btnExportSettings;
        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.Button btnClose;
    }
}
