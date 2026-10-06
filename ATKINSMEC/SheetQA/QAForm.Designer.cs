
namespace ATKINSMEC
{
    // Two tabs at the top of the window, each a whole page: its own
    // scope, its own checks, its own buttons and its own findings. The
    // two never have to agree - flowrate across every sheet while the
    // grids are checked on the view open now is a normal thing to want.
    //
    // The findings tables get their columns in code (QAForm.Columns), so
    // the two cannot drift apart.
    partial class QAForm
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
            this.tabChecks = new System.Windows.Forms.TabControl();
            this.tabFlow = new System.Windows.Forms.TabPage();
            this.tabGrids = new System.Windows.Forms.TabPage();
            this.tabDucts = new System.Windows.Forms.TabPage();
            this.tabChwJ = new System.Windows.Forms.TabPage();
            this.grpScopeChwJ = new System.Windows.Forms.GroupBox();
            this.rbAllChwJ = new System.Windows.Forms.RadioButton();
            this.rbPickedChwJ = new System.Windows.Forms.RadioButton();
            this.rbActiveChwJ = new System.Windows.Forms.RadioButton();
            this.clbLevelsChwJ = new System.Windows.Forms.CheckedListBox();
            this.btnAllLevelsChwJ = new System.Windows.Forms.Button();
            this.btnNoLevelsChwJ = new System.Windows.Forms.Button();
            this.grpChecksChwJ = new System.Windows.Forms.GroupBox();
            this.lblChwJReach = new System.Windows.Forms.Label();
            this.numChwJReach = new System.Windows.Forms.NumericUpDown();
            this.lblChwJReachUnit = new System.Windows.Forms.Label();
            this.lblChwJNote = new System.Windows.Forms.Label();
            this.chkChwJUnknown = new System.Windows.Forms.CheckBox();
            this.lblChwJSystems = new System.Windows.Forms.Label();
            this.clbChwJSystems = new System.Windows.Forms.CheckedListBox();
            this.btnAllChwJSystems = new System.Windows.Forms.Button();
            this.btnNoChwJSystems = new System.Windows.Forms.Button();
            this.lblChwJFamilies = new System.Windows.Forms.Label();
            this.clbChwJFamilies = new System.Windows.Forms.CheckedListBox();
            this.btnAllChwJFamilies = new System.Windows.Forms.Button();
            this.btnNoChwJFamilies = new System.Windows.Forms.Button();
            this.clbChwJ = new System.Windows.Forms.CheckedListBox();
            this.btnCheckChwJ = new System.Windows.Forms.Button();
            this.btnFixChwJ = new System.Windows.Forms.Button();
            this.lblChwJInfo = new System.Windows.Forms.Label();
            this.grpFindingsChwJ = new System.Windows.Forms.GroupBox();
            this.dgvChwJ = new System.Windows.Forms.DataGridView();
            this.lblSummaryChwJ = new System.Windows.Forms.Label();
            this.tabFcu = new System.Windows.Forms.TabPage();
            this.tabChw = new System.Windows.Forms.TabPage();

            this.grpScopeFlow = new System.Windows.Forms.GroupBox();
            this.rbAllFlow = new System.Windows.Forms.RadioButton();
            this.rbPickedFlow = new System.Windows.Forms.RadioButton();
            this.rbActiveFlow = new System.Windows.Forms.RadioButton();
            this.clbSheetsFlow = new System.Windows.Forms.CheckedListBox();
            this.btnAllSheetsFlow = new System.Windows.Forms.Button();
            this.btnNoSheetsFlow = new System.Windows.Forms.Button();
            this.grpChecksFlow = new System.Windows.Forms.GroupBox();
            this.clbFlow = new System.Windows.Forms.CheckedListBox();
            this.btnRunFlow = new System.Windows.Forms.Button();
            this.lblFlowInfo = new System.Windows.Forms.Label();
            this.grpFindingsFlow = new System.Windows.Forms.GroupBox();
            this.dgvFlow = new System.Windows.Forms.DataGridView();
            this.lblSummaryFlow = new System.Windows.Forms.Label();

            this.grpScopeGrids = new System.Windows.Forms.GroupBox();
            this.rbAllGrids = new System.Windows.Forms.RadioButton();
            this.rbPickedGrids = new System.Windows.Forms.RadioButton();
            this.rbActiveGrids = new System.Windows.Forms.RadioButton();
            this.clbSheetsGrids = new System.Windows.Forms.CheckedListBox();
            this.btnAllSheetsGrids = new System.Windows.Forms.Button();
            this.btnNoSheetsGrids = new System.Windows.Forms.Button();
            this.grpChecksGrids = new System.Windows.Forms.GroupBox();
            this.clbGrids = new System.Windows.Forms.CheckedListBox();
            this.btnCheckGrids = new System.Windows.Forms.Button();
            this.btnFixGrids = new System.Windows.Forms.Button();
            this.lblGridsInfo = new System.Windows.Forms.Label();
            this.lblGridLink = new System.Windows.Forms.Label();
            this.clbGridLinks = new System.Windows.Forms.CheckedListBox();
            this.lblBubbles = new System.Windows.Forms.Label();
            this.cmbBubbles = new System.Windows.Forms.ComboBox();
            this.lblGridTol = new System.Windows.Forms.Label();
            this.numGridTol = new System.Windows.Forms.NumericUpDown();
            this.lblGridTolUnit = new System.Windows.Forms.Label();
            this.lblExtTol = new System.Windows.Forms.Label();
            this.numExtTol = new System.Windows.Forms.NumericUpDown();
            this.lblExtTolUnit = new System.Windows.Forms.Label();
            this.grpFindingsGrids = new System.Windows.Forms.GroupBox();
            this.dgvGrids = new System.Windows.Forms.DataGridView();
            this.lblSummaryGrids = new System.Windows.Forms.Label();

            this.grpScopeDucts = new System.Windows.Forms.GroupBox();
            this.rbAllDucts = new System.Windows.Forms.RadioButton();
            this.rbPickedDucts = new System.Windows.Forms.RadioButton();
            this.rbActiveDucts = new System.Windows.Forms.RadioButton();
            this.clbLevelsDucts = new System.Windows.Forms.CheckedListBox();
            this.btnAllLevelsDucts = new System.Windows.Forms.Button();
            this.btnNoLevelsDucts = new System.Windows.Forms.Button();
            this.grpChecksDucts = new System.Windows.Forms.GroupBox();
            this.clbDucts = new System.Windows.Forms.CheckedListBox();
            this.btnCheckDucts = new System.Windows.Forms.Button();
            this.btnFixDucts = new System.Windows.Forms.Button();
            this.lblDuctsInfo = new System.Windows.Forms.Label();
            this.lblDuctReach = new System.Windows.Forms.Label();
            this.numDuctReach = new System.Windows.Forms.NumericUpDown();
            this.lblDuctReachUnit = new System.Windows.Forms.Label();
            this.lblDuctJoin = new System.Windows.Forms.Label();
            this.numDuctJoin = new System.Windows.Forms.NumericUpDown();
            this.lblDuctJoinUnit = new System.Windows.Forms.Label();
            this.chkDuctFallBack = new System.Windows.Forms.CheckBox();
            this.grpFindingsDucts = new System.Windows.Forms.GroupBox();
            this.dgvDucts = new System.Windows.Forms.DataGridView();
            this.lblSummaryDucts = new System.Windows.Forms.Label();

            this.grpScopeFcu = new System.Windows.Forms.GroupBox();
            this.rbAllFcu = new System.Windows.Forms.RadioButton();
            this.rbPickedFcu = new System.Windows.Forms.RadioButton();
            this.rbActiveFcu = new System.Windows.Forms.RadioButton();
            this.clbLevelsFcu = new System.Windows.Forms.CheckedListBox();
            this.btnAllLevelsFcu = new System.Windows.Forms.Button();
            this.btnNoLevelsFcu = new System.Windows.Forms.Button();
            this.grpChecksFcu = new System.Windows.Forms.GroupBox();
            this.clbFcu = new System.Windows.Forms.CheckedListBox();
            this.btnCheckFcu = new System.Windows.Forms.Button();
            this.btnFixFcu = new System.Windows.Forms.Button();
            this.lblFcuInfo = new System.Windows.Forms.Label();
            this.lblFcuFile = new System.Windows.Forms.Label();
            this.txtFcuFile = new System.Windows.Forms.TextBox();
            this.btnFcuBrowse = new System.Windows.Forms.Button();
            this.lblFcuSheet = new System.Windows.Forms.Label();
            this.cmbFcuSheet = new System.Windows.Forms.ComboBox();
            this.lblFcuLoaded = new System.Windows.Forms.Label();
            this.lblFcuParam = new System.Windows.Forms.Label();
            this.cmbFcuParam = new System.Windows.Forms.ComboBox();
            this.lblFcuTol = new System.Windows.Forms.Label();
            this.numFcuPct = new System.Windows.Forms.NumericUpDown();
            this.lblFcuPctUnit = new System.Windows.Forms.Label();
            this.lblFcuOr = new System.Windows.Forms.Label();
            this.numFcuAbs = new System.Windows.Forms.NumericUpDown();
            this.lblFcuAbsUnit = new System.Windows.Forms.Label();
            this.lblFcuWhich = new System.Windows.Forms.Label();
            this.grpFindingsFcu = new System.Windows.Forms.GroupBox();
            this.dgvFcu = new System.Windows.Forms.DataGridView();
            this.lblSummaryFcu = new System.Windows.Forms.Label();

            this.grpScopeChw = new System.Windows.Forms.GroupBox();
            this.rbAllChw = new System.Windows.Forms.RadioButton();
            this.rbPickedChw = new System.Windows.Forms.RadioButton();
            this.rbActiveChw = new System.Windows.Forms.RadioButton();
            this.clbLevelsChw = new System.Windows.Forms.CheckedListBox();
            this.btnAllLevelsChw = new System.Windows.Forms.Button();
            this.btnNoLevelsChw = new System.Windows.Forms.Button();
            this.grpChecksChw = new System.Windows.Forms.GroupBox();
            this.clbChw = new System.Windows.Forms.CheckedListBox();
            this.btnCheckChw = new System.Windows.Forms.Button();
            this.btnFixChw = new System.Windows.Forms.Button();
            this.btnChwFlow = new System.Windows.Forms.Button();
            this.chkChwKeepShort = new System.Windows.Forms.CheckBox();
            this.lblChwTol = new System.Windows.Forms.Label();
            this.numChwSteps = new System.Windows.Forms.NumericUpDown();
            this.lblChwStepsUnit = new System.Windows.Forms.Label();
            this.numChwFrom = new System.Windows.Forms.NumericUpDown();
            this.lblChwFromUnit = new System.Windows.Forms.Label();
            this.lblChwInfo = new System.Windows.Forms.Label();
            this.lblChwLoaded = new System.Windows.Forms.Label();
            this.btnChwExport = new System.Windows.Forms.Button();
            this.btnChwImport = new System.Windows.Forms.Button();
            this.btnChwBuiltin = new System.Windows.Forms.Button();
            this.lblChwSupply = new System.Windows.Forms.Label();
            this.clbChwSupply = new System.Windows.Forms.CheckedListBox();
            this.lblChwReturn = new System.Windows.Forms.Label();
            this.clbChwReturn = new System.Windows.Forms.CheckedListBox();
            this.grpFindingsChw = new System.Windows.Forms.GroupBox();
            this.dgvChw = new System.Windows.Forms.DataGridView();
            this.lblSummaryChw = new System.Windows.Forms.Label();

            this.chkMarkers = new System.Windows.Forms.CheckBox();
            this.lblMarkerInfo = new System.Windows.Forms.Label();
            this.btnMark = new System.Windows.Forms.Button();
            this.btnClearMarks = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnHelp = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();

            this.tabChecks.SuspendLayout();
            this.tabFlow.SuspendLayout();
            this.tabGrids.SuspendLayout();
            this.grpScopeFlow.SuspendLayout();
            this.grpChecksFlow.SuspendLayout();
            this.grpFindingsFlow.SuspendLayout();
            this.grpScopeGrids.SuspendLayout();
            this.grpChecksGrids.SuspendLayout();
            this.grpFindingsGrids.SuspendLayout();
            this.tabDucts.SuspendLayout();
            this.tabChwJ.SuspendLayout();
            this.grpScopeChwJ.SuspendLayout();
            this.grpChecksChwJ.SuspendLayout();
            this.grpFindingsChwJ.SuspendLayout();
            this.grpScopeDucts.SuspendLayout();
            this.grpChecksDucts.SuspendLayout();
            this.grpFindingsDucts.SuspendLayout();
            this.tabFcu.SuspendLayout();
            this.grpScopeFcu.SuspendLayout();
            this.grpChecksFcu.SuspendLayout();
            this.grpFindingsFcu.SuspendLayout();
            this.tabChw.SuspendLayout();
            this.grpScopeChw.SuspendLayout();
            this.grpChecksChw.SuspendLayout();
            this.grpFindingsChw.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFlow)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvGrids)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDucts)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChwJ)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChwJReach)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDuctReach)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDuctJoin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFcu)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFcuPct)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFcuAbs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChw)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChwSteps)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChwFrom)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGridTol)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numExtTol)).BeginInit();
            this.SuspendLayout();
            //
            // tabChecks
            //
            this.tabChecks.Controls.Add(this.tabFlow);
            this.tabChecks.Controls.Add(this.tabGrids);
            this.tabChecks.Controls.Add(this.tabDucts);
            this.tabChecks.Controls.Add(this.tabFcu);
            this.tabChecks.Controls.Add(this.tabChw);
            this.tabChecks.Controls.Add(this.tabChwJ);
            this.tabChecks.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tabChecks.Location = new System.Drawing.Point(12, 12);
            this.tabChecks.Name = "tabChecks";
            this.tabChecks.SelectedIndex = 0;
            this.tabChecks.Size = new System.Drawing.Size(956, 732);
            this.tabChecks.TabIndex = 0;
            this.tabChecks.SelectedIndexChanged += new System.EventHandler(this.tabChecks_SelectedIndexChanged);
            //
            // ==================================================
            // Flowrate page
            // ==================================================
            //
            // tabFlow
            //
            this.tabFlow.Controls.Add(this.grpScopeFlow);
            this.tabFlow.Controls.Add(this.grpChecksFlow);
            this.tabFlow.Controls.Add(this.grpFindingsFlow);
            this.tabFlow.Location = new System.Drawing.Point(4, 22);
            this.tabFlow.Name = "tabFlow";
            this.tabFlow.Size = new System.Drawing.Size(948, 706);
            this.tabFlow.TabIndex = 0;
            this.tabFlow.Text = "Flowrate check";
            this.tabFlow.UseVisualStyleBackColor = true;
            //
            // grpScopeFlow
            //
            this.grpScopeFlow.Controls.Add(this.rbAllFlow);
            this.grpScopeFlow.Controls.Add(this.rbPickedFlow);
            this.grpScopeFlow.Controls.Add(this.rbActiveFlow);
            this.grpScopeFlow.Controls.Add(this.clbSheetsFlow);
            this.grpScopeFlow.Controls.Add(this.btnAllSheetsFlow);
            this.grpScopeFlow.Controls.Add(this.btnNoSheetsFlow);
            this.grpScopeFlow.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpScopeFlow.Location = new System.Drawing.Point(10, 8);
            this.grpScopeFlow.Name = "grpScopeFlow";
            this.grpScopeFlow.Size = new System.Drawing.Size(928, 152);
            this.grpScopeFlow.TabIndex = 0;
            this.grpScopeFlow.TabStop = false;
            this.grpScopeFlow.Text = "1.  What to check";
            //
            // rbAllFlow
            //
            this.rbAllFlow.Checked = true;
            this.rbAllFlow.Location = new System.Drawing.Point(12, 24);
            this.rbAllFlow.Name = "rbAllFlow";
            this.rbAllFlow.Size = new System.Drawing.Size(280, 20);
            this.rbAllFlow.TabIndex = 0;
            this.rbAllFlow.TabStop = true;
            this.rbAllFlow.Text = "Every sheet";
            this.rbAllFlow.UseVisualStyleBackColor = true;
            this.rbAllFlow.CheckedChanged += new System.EventHandler(this.ScopeFlow_CheckedChanged);
            //
            // rbPickedFlow
            //
            this.rbPickedFlow.Location = new System.Drawing.Point(12, 48);
            this.rbPickedFlow.Name = "rbPickedFlow";
            this.rbPickedFlow.Size = new System.Drawing.Size(280, 20);
            this.rbPickedFlow.TabIndex = 1;
            this.rbPickedFlow.Text = "Only the sheets ticked here";
            this.rbPickedFlow.UseVisualStyleBackColor = true;
            this.rbPickedFlow.CheckedChanged += new System.EventHandler(this.ScopeFlow_CheckedChanged);
            //
            // rbActiveFlow
            //
            this.rbActiveFlow.Location = new System.Drawing.Point(12, 72);
            this.rbActiveFlow.Name = "rbActiveFlow";
            this.rbActiveFlow.Size = new System.Drawing.Size(280, 20);
            this.rbActiveFlow.TabIndex = 2;
            this.rbActiveFlow.Text = "The view open now";
            this.rbActiveFlow.UseVisualStyleBackColor = true;
            this.rbActiveFlow.CheckedChanged += new System.EventHandler(this.ScopeFlow_CheckedChanged);
            //
            // clbSheetsFlow
            //
            this.clbSheetsFlow.CheckOnClick = true;
            this.clbSheetsFlow.Enabled = false;
            this.clbSheetsFlow.FormattingEnabled = true;
            this.clbSheetsFlow.IntegralHeight = false;
            this.clbSheetsFlow.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbSheetsFlow.Location = new System.Drawing.Point(300, 24);
            this.clbSheetsFlow.Name = "clbSheetsFlow";
            this.clbSheetsFlow.Size = new System.Drawing.Size(470, 116);
            this.clbSheetsFlow.TabIndex = 3;
            //
            // btnAllSheetsFlow
            //
            this.btnAllSheetsFlow.Enabled = false;
            this.btnAllSheetsFlow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllSheetsFlow.Location = new System.Drawing.Point(786, 24);
            this.btnAllSheetsFlow.Name = "btnAllSheetsFlow";
            this.btnAllSheetsFlow.Size = new System.Drawing.Size(130, 26);
            this.btnAllSheetsFlow.TabIndex = 4;
            this.btnAllSheetsFlow.Text = "Tick all";
            this.btnAllSheetsFlow.UseVisualStyleBackColor = true;
            this.btnAllSheetsFlow.Click += new System.EventHandler(this.btnAllSheetsFlow_Click);
            //
            // btnNoSheetsFlow
            //
            this.btnNoSheetsFlow.Enabled = false;
            this.btnNoSheetsFlow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoSheetsFlow.Location = new System.Drawing.Point(786, 56);
            this.btnNoSheetsFlow.Name = "btnNoSheetsFlow";
            this.btnNoSheetsFlow.Size = new System.Drawing.Size(130, 26);
            this.btnNoSheetsFlow.TabIndex = 5;
            this.btnNoSheetsFlow.Text = "Untick all";
            this.btnNoSheetsFlow.UseVisualStyleBackColor = true;
            this.btnNoSheetsFlow.Click += new System.EventHandler(this.btnNoSheetsFlow_Click);
            //
            // grpChecksFlow
            //
            this.grpChecksFlow.Controls.Add(this.clbFlow);
            this.grpChecksFlow.Controls.Add(this.btnRunFlow);
            this.grpChecksFlow.Controls.Add(this.lblFlowInfo);
            this.grpChecksFlow.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpChecksFlow.Location = new System.Drawing.Point(10, 168);
            this.grpChecksFlow.Name = "grpChecksFlow";
            this.grpChecksFlow.Size = new System.Drawing.Size(928, 186);
            this.grpChecksFlow.TabIndex = 1;
            this.grpChecksFlow.TabStop = false;
            this.grpChecksFlow.Text = "2.  Checks to run";
            //
            // clbFlow
            //
            this.clbFlow.CheckOnClick = true;
            this.clbFlow.FormattingEnabled = true;
            this.clbFlow.IntegralHeight = false;
            this.clbFlow.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbFlow.Location = new System.Drawing.Point(12, 24);
            this.clbFlow.Name = "clbFlow";
            this.clbFlow.Size = new System.Drawing.Size(900, 88);
            this.clbFlow.TabIndex = 0;
            this.clbFlow.SelectedIndexChanged += new System.EventHandler(this.clbFlow_SelectedIndexChanged);
            //
            // btnRunFlow
            //
            this.btnRunFlow.Location = new System.Drawing.Point(12, 120);
            this.btnRunFlow.Name = "btnRunFlow";
            this.btnRunFlow.Size = new System.Drawing.Size(210, 30);
            this.btnRunFlow.TabIndex = 1;
            this.btnRunFlow.Text = "Run the ticked checks";
            this.btnRunFlow.UseVisualStyleBackColor = true;
            this.btnRunFlow.Click += new System.EventHandler(this.btnRunFlow_Click);
            //
            // lblFlowInfo
            //
            this.lblFlowInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblFlowInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFlowInfo.Location = new System.Drawing.Point(12, 156);
            this.lblFlowInfo.Name = "lblFlowInfo";
            this.lblFlowInfo.Size = new System.Drawing.Size(900, 24);
            this.lblFlowInfo.TabIndex = 2;
            this.lblFlowInfo.Text = "";
            //
            // grpFindingsFlow
            //
            this.grpFindingsFlow.Controls.Add(this.dgvFlow);
            this.grpFindingsFlow.Controls.Add(this.lblSummaryFlow);
            this.grpFindingsFlow.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFindingsFlow.Location = new System.Drawing.Point(10, 362);
            this.grpFindingsFlow.Name = "grpFindingsFlow";
            this.grpFindingsFlow.Size = new System.Drawing.Size(928, 340);
            this.grpFindingsFlow.TabIndex = 2;
            this.grpFindingsFlow.TabStop = false;
            this.grpFindingsFlow.Text = "3.  What was found";
            //
            // dgvFlow
            //
            this.dgvFlow.AllowUserToAddRows = false;
            this.dgvFlow.AllowUserToDeleteRows = false;
            this.dgvFlow.AllowUserToResizeRows = false;
            this.dgvFlow.AllowUserToOrderColumns = true;
            this.dgvFlow.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvFlow.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvFlow.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvFlow.Location = new System.Drawing.Point(12, 24);
            this.dgvFlow.MultiSelect = true;
            this.dgvFlow.Name = "dgvFlow";
            this.dgvFlow.ReadOnly = true;
            this.dgvFlow.RowHeadersVisible = false;
            this.dgvFlow.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvFlow.Size = new System.Drawing.Size(904, 270);
            this.dgvFlow.TabIndex = 0;
            //
            // lblSummaryFlow
            //
            this.lblSummaryFlow.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummaryFlow.Location = new System.Drawing.Point(12, 300);
            this.lblSummaryFlow.Name = "lblSummaryFlow";
            this.lblSummaryFlow.Size = new System.Drawing.Size(904, 32);
            this.lblSummaryFlow.TabIndex = 1;
            this.lblSummaryFlow.Text = "";
            //
            // ==================================================
            // Grids page
            // ==================================================
            //
            // tabGrids
            //
            this.tabGrids.Controls.Add(this.grpScopeGrids);
            this.tabGrids.Controls.Add(this.grpChecksGrids);
            this.tabGrids.Controls.Add(this.grpFindingsGrids);
            this.tabGrids.Location = new System.Drawing.Point(4, 22);
            this.tabGrids.Name = "tabGrids";
            this.tabGrids.Size = new System.Drawing.Size(948, 706);
            this.tabGrids.TabIndex = 1;
            this.tabGrids.Text = "Grids";
            this.tabGrids.UseVisualStyleBackColor = true;
            //
            // grpScopeGrids
            //
            this.grpScopeGrids.Controls.Add(this.rbAllGrids);
            this.grpScopeGrids.Controls.Add(this.rbPickedGrids);
            this.grpScopeGrids.Controls.Add(this.rbActiveGrids);
            this.grpScopeGrids.Controls.Add(this.clbSheetsGrids);
            this.grpScopeGrids.Controls.Add(this.btnAllSheetsGrids);
            this.grpScopeGrids.Controls.Add(this.btnNoSheetsGrids);
            this.grpScopeGrids.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpScopeGrids.Location = new System.Drawing.Point(10, 8);
            this.grpScopeGrids.Name = "grpScopeGrids";
            this.grpScopeGrids.Size = new System.Drawing.Size(928, 152);
            this.grpScopeGrids.TabIndex = 0;
            this.grpScopeGrids.TabStop = false;
            this.grpScopeGrids.Text = "1.  What to check";
            //
            // rbAllGrids
            //
            this.rbAllGrids.Checked = true;
            this.rbAllGrids.Location = new System.Drawing.Point(12, 24);
            this.rbAllGrids.Name = "rbAllGrids";
            this.rbAllGrids.Size = new System.Drawing.Size(280, 20);
            this.rbAllGrids.TabIndex = 0;
            this.rbAllGrids.TabStop = true;
            this.rbAllGrids.Text = "Every sheet";
            this.rbAllGrids.UseVisualStyleBackColor = true;
            this.rbAllGrids.CheckedChanged += new System.EventHandler(this.ScopeGrids_CheckedChanged);
            //
            // rbPickedGrids
            //
            this.rbPickedGrids.Location = new System.Drawing.Point(12, 48);
            this.rbPickedGrids.Name = "rbPickedGrids";
            this.rbPickedGrids.Size = new System.Drawing.Size(280, 20);
            this.rbPickedGrids.TabIndex = 1;
            this.rbPickedGrids.Text = "Only the sheets ticked here";
            this.rbPickedGrids.UseVisualStyleBackColor = true;
            this.rbPickedGrids.CheckedChanged += new System.EventHandler(this.ScopeGrids_CheckedChanged);
            //
            // rbActiveGrids
            //
            this.rbActiveGrids.Location = new System.Drawing.Point(12, 72);
            this.rbActiveGrids.Name = "rbActiveGrids";
            this.rbActiveGrids.Size = new System.Drawing.Size(280, 20);
            this.rbActiveGrids.TabIndex = 2;
            this.rbActiveGrids.Text = "The view open now";
            this.rbActiveGrids.UseVisualStyleBackColor = true;
            this.rbActiveGrids.CheckedChanged += new System.EventHandler(this.ScopeGrids_CheckedChanged);
            //
            // clbSheetsGrids
            //
            this.clbSheetsGrids.CheckOnClick = true;
            this.clbSheetsGrids.Enabled = false;
            this.clbSheetsGrids.FormattingEnabled = true;
            this.clbSheetsGrids.IntegralHeight = false;
            this.clbSheetsGrids.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbSheetsGrids.Location = new System.Drawing.Point(300, 24);
            this.clbSheetsGrids.Name = "clbSheetsGrids";
            this.clbSheetsGrids.Size = new System.Drawing.Size(470, 116);
            this.clbSheetsGrids.TabIndex = 3;
            //
            // btnAllSheetsGrids
            //
            this.btnAllSheetsGrids.Enabled = false;
            this.btnAllSheetsGrids.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllSheetsGrids.Location = new System.Drawing.Point(786, 24);
            this.btnAllSheetsGrids.Name = "btnAllSheetsGrids";
            this.btnAllSheetsGrids.Size = new System.Drawing.Size(130, 26);
            this.btnAllSheetsGrids.TabIndex = 4;
            this.btnAllSheetsGrids.Text = "Tick all";
            this.btnAllSheetsGrids.UseVisualStyleBackColor = true;
            this.btnAllSheetsGrids.Click += new System.EventHandler(this.btnAllSheetsGrids_Click);
            //
            // btnNoSheetsGrids
            //
            this.btnNoSheetsGrids.Enabled = false;
            this.btnNoSheetsGrids.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoSheetsGrids.Location = new System.Drawing.Point(786, 56);
            this.btnNoSheetsGrids.Name = "btnNoSheetsGrids";
            this.btnNoSheetsGrids.Size = new System.Drawing.Size(130, 26);
            this.btnNoSheetsGrids.TabIndex = 5;
            this.btnNoSheetsGrids.Text = "Untick all";
            this.btnNoSheetsGrids.UseVisualStyleBackColor = true;
            this.btnNoSheetsGrids.Click += new System.EventHandler(this.btnNoSheetsGrids_Click);
            //
            // grpChecksGrids
            //
            this.grpChecksGrids.Controls.Add(this.clbGrids);
            this.grpChecksGrids.Controls.Add(this.btnCheckGrids);
            this.grpChecksGrids.Controls.Add(this.btnFixGrids);
            this.grpChecksGrids.Controls.Add(this.lblGridsInfo);
            this.grpChecksGrids.Controls.Add(this.lblGridLink);
            this.grpChecksGrids.Controls.Add(this.clbGridLinks);
            this.grpChecksGrids.Controls.Add(this.lblBubbles);
            this.grpChecksGrids.Controls.Add(this.cmbBubbles);
            this.grpChecksGrids.Controls.Add(this.lblGridTol);
            this.grpChecksGrids.Controls.Add(this.numGridTol);
            this.grpChecksGrids.Controls.Add(this.lblGridTolUnit);
            this.grpChecksGrids.Controls.Add(this.lblExtTol);
            this.grpChecksGrids.Controls.Add(this.numExtTol);
            this.grpChecksGrids.Controls.Add(this.lblExtTolUnit);
            this.grpChecksGrids.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpChecksGrids.Location = new System.Drawing.Point(10, 168);
            this.grpChecksGrids.Name = "grpChecksGrids";
            this.grpChecksGrids.Size = new System.Drawing.Size(928, 186);
            this.grpChecksGrids.TabIndex = 1;
            this.grpChecksGrids.TabStop = false;
            this.grpChecksGrids.Text = "2.  Checks to run";
            //
            // clbGrids
            //
            this.clbGrids.CheckOnClick = true;
            this.clbGrids.FormattingEnabled = true;
            this.clbGrids.IntegralHeight = false;
            this.clbGrids.Location = new System.Drawing.Point(12, 24);
            this.clbGrids.Name = "clbGrids";
            this.clbGrids.Size = new System.Drawing.Size(420, 88);
            this.clbGrids.TabIndex = 0;
            this.clbGrids.SelectedIndexChanged += new System.EventHandler(this.clbGrids_SelectedIndexChanged);
            //
            // btnCheckGrids
            //
            this.btnCheckGrids.Location = new System.Drawing.Point(12, 120);
            this.btnCheckGrids.Name = "btnCheckGrids";
            this.btnCheckGrids.Size = new System.Drawing.Size(200, 30);
            this.btnCheckGrids.TabIndex = 1;
            this.btnCheckGrids.Text = "Run the ticked checks";
            this.btnCheckGrids.UseVisualStyleBackColor = true;
            this.btnCheckGrids.Click += new System.EventHandler(this.btnCheckGrids_Click);
            //
            // btnFixGrids
            //
            this.btnFixGrids.Enabled = false;
            this.btnFixGrids.Location = new System.Drawing.Point(222, 120);
            this.btnFixGrids.Name = "btnFixGrids";
            this.btnFixGrids.Size = new System.Drawing.Size(200, 30);
            this.btnFixGrids.TabIndex = 2;
            this.btnFixGrids.Text = "Put the grids right";
            this.btnFixGrids.UseVisualStyleBackColor = true;
            this.btnFixGrids.Click += new System.EventHandler(this.btnFixGrids_Click);
            //
            // lblGridsInfo
            //
            this.lblGridsInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblGridsInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblGridsInfo.Location = new System.Drawing.Point(12, 156);
            this.lblGridsInfo.Name = "lblGridsInfo";
            this.lblGridsInfo.Size = new System.Drawing.Size(900, 24);
            this.lblGridsInfo.TabIndex = 3;
            this.lblGridsInfo.Text = "";
            //
            // lblGridLink
            //
            this.lblGridLink.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblGridLink.Location = new System.Drawing.Point(448, 24);
            this.lblGridLink.Name = "lblGridLink";
            this.lblGridLink.Size = new System.Drawing.Size(460, 16);
            this.lblGridLink.TabIndex = 4;
            this.lblGridLink.Text = "The model the grids were copied from:";
            //
            // clbGridLinks
            //
            this.clbGridLinks.CheckOnClick = true;
            this.clbGridLinks.FormattingEnabled = true;
            this.clbGridLinks.IntegralHeight = false;
            this.clbGridLinks.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbGridLinks.Location = new System.Drawing.Point(448, 42);
            this.clbGridLinks.Name = "clbGridLinks";
            this.clbGridLinks.Size = new System.Drawing.Size(464, 50);
            this.clbGridLinks.TabIndex = 5;
            //
            // lblBubbles
            //
            this.lblBubbles.Location = new System.Drawing.Point(448, 100);
            this.lblBubbles.Name = "lblBubbles";
            this.lblBubbles.Size = new System.Drawing.Size(96, 18);
            this.lblBubbles.TabIndex = 6;
            this.lblBubbles.Text = "Bubbles belong:";
            //
            // cmbBubbles
            //
            this.cmbBubbles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBubbles.FormattingEnabled = true;
            this.cmbBubbles.Location = new System.Drawing.Point(546, 97);
            this.cmbBubbles.Name = "cmbBubbles";
            this.cmbBubbles.Size = new System.Drawing.Size(170, 21);
            this.cmbBubbles.TabIndex = 7;
            //
            // lblGridTol
            //
            this.lblGridTol.Location = new System.Drawing.Point(448, 128);
            this.lblGridTol.Name = "lblGridTol";
            this.lblGridTol.Size = new System.Drawing.Size(66, 18);
            this.lblGridTol.TabIndex = 8;
            this.lblGridTol.Text = "Moved over";
            //
            // numGridTol
            //
            this.numGridTol.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this.numGridTol.Location = new System.Drawing.Point(518, 125);
            this.numGridTol.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numGridTol.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numGridTol.Name = "numGridTol";
            this.numGridTol.Size = new System.Drawing.Size(60, 20);
            this.numGridTol.TabIndex = 9;
            this.numGridTol.Value = new decimal(new int[] { 50, 0, 0, 0 });
            //
            // lblGridTolUnit
            //
            this.lblGridTolUnit.Location = new System.Drawing.Point(584, 128);
            this.lblGridTolUnit.Name = "lblGridTolUnit";
            this.lblGridTolUnit.Size = new System.Drawing.Size(30, 18);
            this.lblGridTolUnit.TabIndex = 10;
            this.lblGridTolUnit.Text = "mm";
            //
            // lblExtTol
            //
            this.lblExtTol.Location = new System.Drawing.Point(630, 128);
            this.lblExtTol.Name = "lblExtTol";
            this.lblExtTol.Size = new System.Drawing.Size(62, 18);
            this.lblExtTol.TabIndex = 11;
            this.lblExtTol.Text = "Short over";
            //
            // numExtTol
            //
            this.numExtTol.Increment = new decimal(new int[] { 50, 0, 0, 0 });
            this.numExtTol.Location = new System.Drawing.Point(698, 125);
            this.numExtTol.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            this.numExtTol.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numExtTol.Name = "numExtTol";
            this.numExtTol.Size = new System.Drawing.Size(60, 20);
            this.numExtTol.TabIndex = 12;
            this.numExtTol.Value = new decimal(new int[] { 200, 0, 0, 0 });
            //
            // lblExtTolUnit
            //
            this.lblExtTolUnit.Location = new System.Drawing.Point(764, 128);
            this.lblExtTolUnit.Name = "lblExtTolUnit";
            this.lblExtTolUnit.Size = new System.Drawing.Size(30, 18);
            this.lblExtTolUnit.TabIndex = 13;
            this.lblExtTolUnit.Text = "mm";
            //
            // grpFindingsGrids
            //
            this.grpFindingsGrids.Controls.Add(this.dgvGrids);
            this.grpFindingsGrids.Controls.Add(this.lblSummaryGrids);
            this.grpFindingsGrids.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFindingsGrids.Location = new System.Drawing.Point(10, 362);
            this.grpFindingsGrids.Name = "grpFindingsGrids";
            this.grpFindingsGrids.Size = new System.Drawing.Size(928, 340);
            this.grpFindingsGrids.TabIndex = 2;
            this.grpFindingsGrids.TabStop = false;
            this.grpFindingsGrids.Text = "3.  What was found";
            //
            // dgvGrids
            //
            this.dgvGrids.AllowUserToAddRows = false;
            this.dgvGrids.AllowUserToDeleteRows = false;
            this.dgvGrids.AllowUserToResizeRows = false;
            this.dgvGrids.AllowUserToOrderColumns = true;
            this.dgvGrids.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvGrids.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvGrids.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvGrids.Location = new System.Drawing.Point(12, 24);
            this.dgvGrids.MultiSelect = true;
            this.dgvGrids.Name = "dgvGrids";
            this.dgvGrids.ReadOnly = true;
            this.dgvGrids.RowHeadersVisible = false;
            this.dgvGrids.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvGrids.Size = new System.Drawing.Size(904, 270);
            this.dgvGrids.TabIndex = 0;
            //
            // lblSummaryGrids
            //
            this.lblSummaryGrids.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummaryGrids.Location = new System.Drawing.Point(12, 300);
            this.lblSummaryGrids.Name = "lblSummaryGrids";
            this.lblSummaryGrids.Size = new System.Drawing.Size(904, 32);
            this.lblSummaryGrids.TabIndex = 1;
            this.lblSummaryGrids.Text = "";
            //
            // ==================================================
            // Ducts page
            // ==================================================
            //
            // tabDucts
            //
            this.tabDucts.Controls.Add(this.grpScopeDucts);
            this.tabDucts.Controls.Add(this.grpChecksDucts);
            this.tabDucts.Controls.Add(this.grpFindingsDucts);
            this.tabDucts.Location = new System.Drawing.Point(4, 22);
            this.tabDucts.Name = "tabDucts";
            this.tabDucts.Size = new System.Drawing.Size(948, 706);
            this.tabDucts.TabIndex = 2;
            this.tabDucts.Text = "Duct disconnection check";
            this.tabDucts.UseVisualStyleBackColor = true;
            //
            // grpScopeDucts
            //
            this.grpScopeDucts.Controls.Add(this.rbAllDucts);
            this.grpScopeDucts.Controls.Add(this.rbPickedDucts);
            this.grpScopeDucts.Controls.Add(this.rbActiveDucts);
            this.grpScopeDucts.Controls.Add(this.clbLevelsDucts);
            this.grpScopeDucts.Controls.Add(this.btnAllLevelsDucts);
            this.grpScopeDucts.Controls.Add(this.btnNoLevelsDucts);
            this.grpScopeDucts.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpScopeDucts.Location = new System.Drawing.Point(10, 8);
            this.grpScopeDucts.Name = "grpScopeDucts";
            this.grpScopeDucts.Size = new System.Drawing.Size(928, 152);
            this.grpScopeDucts.TabIndex = 0;
            this.grpScopeDucts.TabStop = false;
            this.grpScopeDucts.Text = "1.  What to check";
            //
            // rbAllDucts
            //
            this.rbAllDucts.Checked = true;
            this.rbAllDucts.Location = new System.Drawing.Point(12, 24);
            this.rbAllDucts.Name = "rbAllDucts";
            this.rbAllDucts.Size = new System.Drawing.Size(280, 20);
            this.rbAllDucts.TabIndex = 0;
            this.rbAllDucts.TabStop = true;
            this.rbAllDucts.Text = "Everywhere in the model";
            this.rbAllDucts.UseVisualStyleBackColor = true;
            this.rbAllDucts.CheckedChanged += new System.EventHandler(this.ScopeDucts_CheckedChanged);
            //
            // rbPickedDucts
            //
            this.rbPickedDucts.Location = new System.Drawing.Point(12, 48);
            this.rbPickedDucts.Name = "rbPickedDucts";
            this.rbPickedDucts.Size = new System.Drawing.Size(280, 20);
            this.rbPickedDucts.TabIndex = 1;
            this.rbPickedDucts.Text = "Only the levels ticked here";
            this.rbPickedDucts.UseVisualStyleBackColor = true;
            this.rbPickedDucts.CheckedChanged += new System.EventHandler(this.ScopeDucts_CheckedChanged);
            //
            // rbActiveDucts
            //
            this.rbActiveDucts.Location = new System.Drawing.Point(12, 72);
            this.rbActiveDucts.Name = "rbActiveDucts";
            this.rbActiveDucts.Size = new System.Drawing.Size(280, 20);
            this.rbActiveDucts.TabIndex = 2;
            this.rbActiveDucts.Text = "The view open now";
            this.rbActiveDucts.UseVisualStyleBackColor = true;
            this.rbActiveDucts.CheckedChanged += new System.EventHandler(this.ScopeDucts_CheckedChanged);
            //
            // clbLevelsDucts
            //
            this.clbLevelsDucts.CheckOnClick = true;
            this.clbLevelsDucts.Enabled = false;
            this.clbLevelsDucts.FormattingEnabled = true;
            this.clbLevelsDucts.IntegralHeight = false;
            this.clbLevelsDucts.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbLevelsDucts.Location = new System.Drawing.Point(300, 24);
            this.clbLevelsDucts.Name = "clbLevelsDucts";
            this.clbLevelsDucts.Size = new System.Drawing.Size(470, 116);
            this.clbLevelsDucts.TabIndex = 3;
            //
            // btnAllLevelsDucts
            //
            this.btnAllLevelsDucts.Enabled = false;
            this.btnAllLevelsDucts.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllLevelsDucts.Location = new System.Drawing.Point(786, 24);
            this.btnAllLevelsDucts.Name = "btnAllLevelsDucts";
            this.btnAllLevelsDucts.Size = new System.Drawing.Size(130, 26);
            this.btnAllLevelsDucts.TabIndex = 4;
            this.btnAllLevelsDucts.Text = "Tick all";
            this.btnAllLevelsDucts.UseVisualStyleBackColor = true;
            this.btnAllLevelsDucts.Click += new System.EventHandler(this.btnAllLevelsDucts_Click);
            //
            // btnNoLevelsDucts
            //
            this.btnNoLevelsDucts.Enabled = false;
            this.btnNoLevelsDucts.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoLevelsDucts.Location = new System.Drawing.Point(786, 56);
            this.btnNoLevelsDucts.Name = "btnNoLevelsDucts";
            this.btnNoLevelsDucts.Size = new System.Drawing.Size(130, 26);
            this.btnNoLevelsDucts.TabIndex = 5;
            this.btnNoLevelsDucts.Text = "Untick all";
            this.btnNoLevelsDucts.UseVisualStyleBackColor = true;
            this.btnNoLevelsDucts.Click += new System.EventHandler(this.btnNoLevelsDucts_Click);
            //
            // grpChecksDucts
            //
            this.grpChecksDucts.Controls.Add(this.clbDucts);
            this.grpChecksDucts.Controls.Add(this.btnCheckDucts);
            this.grpChecksDucts.Controls.Add(this.btnFixDucts);
            this.grpChecksDucts.Controls.Add(this.lblDuctsInfo);
            this.grpChecksDucts.Controls.Add(this.lblDuctReach);
            this.grpChecksDucts.Controls.Add(this.numDuctReach);
            this.grpChecksDucts.Controls.Add(this.lblDuctReachUnit);
            this.grpChecksDucts.Controls.Add(this.lblDuctJoin);
            this.grpChecksDucts.Controls.Add(this.numDuctJoin);
            this.grpChecksDucts.Controls.Add(this.lblDuctJoinUnit);
            this.grpChecksDucts.Controls.Add(this.chkDuctFallBack);
            this.grpChecksDucts.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpChecksDucts.Location = new System.Drawing.Point(10, 168);
            this.grpChecksDucts.Name = "grpChecksDucts";
            this.grpChecksDucts.Size = new System.Drawing.Size(928, 186);
            this.grpChecksDucts.TabIndex = 1;
            this.grpChecksDucts.TabStop = false;
            this.grpChecksDucts.Text = "2.  Checks to run";
            //
            // clbDucts
            //
            this.clbDucts.CheckOnClick = true;
            this.clbDucts.FormattingEnabled = true;
            this.clbDucts.IntegralHeight = false;
            this.clbDucts.Location = new System.Drawing.Point(12, 24);
            this.clbDucts.Name = "clbDucts";
            this.clbDucts.Size = new System.Drawing.Size(420, 88);
            this.clbDucts.TabIndex = 0;
            this.clbDucts.SelectedIndexChanged += new System.EventHandler(this.clbDucts_SelectedIndexChanged);
            //
            // btnCheckDucts
            //
            this.btnCheckDucts.Location = new System.Drawing.Point(12, 120);
            this.btnCheckDucts.Name = "btnCheckDucts";
            this.btnCheckDucts.Size = new System.Drawing.Size(200, 30);
            this.btnCheckDucts.TabIndex = 1;
            this.btnCheckDucts.Text = "Run the ticked checks";
            this.btnCheckDucts.UseVisualStyleBackColor = true;
            this.btnCheckDucts.Click += new System.EventHandler(this.btnCheckDucts_Click);
            //
            // btnFixDucts
            //
            this.btnFixDucts.Enabled = false;
            this.btnFixDucts.Location = new System.Drawing.Point(222, 120);
            this.btnFixDucts.Name = "btnFixDucts";
            this.btnFixDucts.Size = new System.Drawing.Size(200, 30);
            this.btnFixDucts.TabIndex = 2;
            this.btnFixDucts.Text = "Join them up";
            this.btnFixDucts.UseVisualStyleBackColor = true;
            this.btnFixDucts.Click += new System.EventHandler(this.btnFixDucts_Click);
            //
            // lblDuctsInfo
            //
            this.lblDuctsInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblDuctsInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDuctsInfo.Location = new System.Drawing.Point(12, 156);
            this.lblDuctsInfo.Name = "lblDuctsInfo";
            this.lblDuctsInfo.Size = new System.Drawing.Size(900, 24);
            this.lblDuctsInfo.TabIndex = 3;
            this.lblDuctsInfo.Text = "";
            //
            // lblDuctReach
            //
            this.lblDuctReach.Location = new System.Drawing.Point(448, 27);
            this.lblDuctReach.Name = "lblDuctReach";
            this.lblDuctReach.Size = new System.Drawing.Size(146, 18);
            this.lblDuctReach.TabIndex = 4;
            this.lblDuctReach.Text = "Look for a duct within";
            //
            // numDuctReach
            //
            this.numDuctReach.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            this.numDuctReach.Location = new System.Drawing.Point(598, 24);
            this.numDuctReach.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            this.numDuctReach.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numDuctReach.Name = "numDuctReach";
            this.numDuctReach.Size = new System.Drawing.Size(64, 20);
            this.numDuctReach.TabIndex = 5;
            this.numDuctReach.Value = new decimal(new int[] { 1500, 0, 0, 0 });
            //
            // lblDuctReachUnit
            //
            this.lblDuctReachUnit.Location = new System.Drawing.Point(668, 27);
            this.lblDuctReachUnit.Name = "lblDuctReachUnit";
            this.lblDuctReachUnit.Size = new System.Drawing.Size(30, 18);
            this.lblDuctReachUnit.TabIndex = 6;
            this.lblDuctReachUnit.Text = "mm";
            //
            // lblDuctJoin
            //
            this.lblDuctJoin.Location = new System.Drawing.Point(448, 55);
            this.lblDuctJoin.Name = "lblDuctJoin";
            this.lblDuctJoin.Size = new System.Drawing.Size(146, 18);
            this.lblDuctJoin.TabIndex = 7;
            this.lblDuctJoin.Text = "Ends meet if closer than";
            //
            // numDuctJoin
            //
            this.numDuctJoin.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            this.numDuctJoin.Location = new System.Drawing.Point(598, 52);
            this.numDuctJoin.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numDuctJoin.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numDuctJoin.Name = "numDuctJoin";
            this.numDuctJoin.Size = new System.Drawing.Size(64, 20);
            this.numDuctJoin.TabIndex = 8;
            this.numDuctJoin.Value = new decimal(new int[] { 25, 0, 0, 0 });
            //
            // lblDuctJoinUnit
            //
            this.lblDuctJoinUnit.Location = new System.Drawing.Point(668, 55);
            this.lblDuctJoinUnit.Name = "lblDuctJoinUnit";
            this.lblDuctJoinUnit.Size = new System.Drawing.Size(30, 18);
            this.lblDuctJoinUnit.TabIndex = 9;
            this.lblDuctJoinUnit.Text = "mm";
            //
            // chkDuctFallBack
            //
            this.chkDuctFallBack.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.chkDuctFallBack.Location = new System.Drawing.Point(448, 84);
            this.chkDuctFallBack.Name = "chkDuctFallBack";
            this.chkDuctFallBack.Size = new System.Drawing.Size(464, 36);
            this.chkDuctFallBack.TabIndex = 10;
            this.chkDuctFallBack.Text = "If its own system has nothing within reach, use the nearest duct of any system";
            this.chkDuctFallBack.UseVisualStyleBackColor = true;
            //
            // grpFindingsDucts
            //
            this.grpFindingsDucts.Controls.Add(this.dgvDucts);
            this.grpFindingsDucts.Controls.Add(this.lblSummaryDucts);
            this.grpFindingsDucts.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFindingsDucts.Location = new System.Drawing.Point(10, 362);
            this.grpFindingsDucts.Name = "grpFindingsDucts";
            this.grpFindingsDucts.Size = new System.Drawing.Size(928, 340);
            this.grpFindingsDucts.TabIndex = 2;
            this.grpFindingsDucts.TabStop = false;
            this.grpFindingsDucts.Text = "3.  What was found";
            //
            // dgvDucts
            //
            this.dgvDucts.AllowUserToAddRows = false;
            this.dgvDucts.AllowUserToDeleteRows = false;
            this.dgvDucts.AllowUserToResizeRows = false;
            this.dgvDucts.AllowUserToOrderColumns = true;
            this.dgvDucts.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvDucts.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvDucts.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvDucts.Location = new System.Drawing.Point(12, 24);
            this.dgvDucts.MultiSelect = true;
            this.dgvDucts.Name = "dgvDucts";
            this.dgvDucts.ReadOnly = true;
            this.dgvDucts.RowHeadersVisible = false;
            this.dgvDucts.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvDucts.Size = new System.Drawing.Size(904, 270);
            this.dgvDucts.TabIndex = 0;
            //
            // lblSummaryDucts
            //
            this.lblSummaryDucts.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummaryDucts.Location = new System.Drawing.Point(12, 300);
            this.lblSummaryDucts.Name = "lblSummaryDucts";
            this.lblSummaryDucts.Size = new System.Drawing.Size(904, 32);
            this.lblSummaryDucts.TabIndex = 1;
            this.lblSummaryDucts.Text = "";
            //
            // ==================================================
            // FCU page
            // ==================================================
            //
            // tabFcu
            //
            this.tabFcu.Controls.Add(this.grpScopeFcu);
            this.tabFcu.Controls.Add(this.grpChecksFcu);
            this.tabFcu.Controls.Add(this.grpFindingsFcu);
            this.tabFcu.Location = new System.Drawing.Point(4, 22);
            this.tabFcu.Name = "tabFcu";
            this.tabFcu.Size = new System.Drawing.Size(948, 706);
            this.tabFcu.TabIndex = 3;
            this.tabFcu.Text = "FCU flowrate check";
            this.tabFcu.UseVisualStyleBackColor = true;
            //
            // grpScopeFcu
            //
            this.grpScopeFcu.Controls.Add(this.rbAllFcu);
            this.grpScopeFcu.Controls.Add(this.rbPickedFcu);
            this.grpScopeFcu.Controls.Add(this.rbActiveFcu);
            this.grpScopeFcu.Controls.Add(this.clbLevelsFcu);
            this.grpScopeFcu.Controls.Add(this.btnAllLevelsFcu);
            this.grpScopeFcu.Controls.Add(this.btnNoLevelsFcu);
            this.grpScopeFcu.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpScopeFcu.Location = new System.Drawing.Point(10, 8);
            this.grpScopeFcu.Name = "grpScopeFcu";
            this.grpScopeFcu.Size = new System.Drawing.Size(928, 152);
            this.grpScopeFcu.TabIndex = 0;
            this.grpScopeFcu.TabStop = false;
            this.grpScopeFcu.Text = "1.  What to check";
            //
            // rbAllFcu
            //
            this.rbAllFcu.Checked = true;
            this.rbAllFcu.Location = new System.Drawing.Point(12, 24);
            this.rbAllFcu.Name = "rbAllFcu";
            this.rbAllFcu.Size = new System.Drawing.Size(280, 20);
            this.rbAllFcu.TabIndex = 0;
            this.rbAllFcu.TabStop = true;
            this.rbAllFcu.Text = "Everywhere in the model";
            this.rbAllFcu.UseVisualStyleBackColor = true;
            this.rbAllFcu.CheckedChanged += new System.EventHandler(this.ScopeFcu_CheckedChanged);
            //
            // rbPickedFcu
            //
            this.rbPickedFcu.Location = new System.Drawing.Point(12, 48);
            this.rbPickedFcu.Name = "rbPickedFcu";
            this.rbPickedFcu.Size = new System.Drawing.Size(280, 20);
            this.rbPickedFcu.TabIndex = 1;
            this.rbPickedFcu.Text = "Only the levels ticked here";
            this.rbPickedFcu.UseVisualStyleBackColor = true;
            this.rbPickedFcu.CheckedChanged += new System.EventHandler(this.ScopeFcu_CheckedChanged);
            //
            // rbActiveFcu
            //
            this.rbActiveFcu.Location = new System.Drawing.Point(12, 72);
            this.rbActiveFcu.Name = "rbActiveFcu";
            this.rbActiveFcu.Size = new System.Drawing.Size(280, 20);
            this.rbActiveFcu.TabIndex = 2;
            this.rbActiveFcu.Text = "The view open now";
            this.rbActiveFcu.UseVisualStyleBackColor = true;
            this.rbActiveFcu.CheckedChanged += new System.EventHandler(this.ScopeFcu_CheckedChanged);
            //
            // clbLevelsFcu
            //
            this.clbLevelsFcu.CheckOnClick = true;
            this.clbLevelsFcu.Enabled = false;
            this.clbLevelsFcu.FormattingEnabled = true;
            this.clbLevelsFcu.IntegralHeight = false;
            this.clbLevelsFcu.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbLevelsFcu.Location = new System.Drawing.Point(300, 24);
            this.clbLevelsFcu.Name = "clbLevelsFcu";
            this.clbLevelsFcu.Size = new System.Drawing.Size(470, 116);
            this.clbLevelsFcu.TabIndex = 3;
            //
            // btnAllLevelsFcu
            //
            this.btnAllLevelsFcu.Enabled = false;
            this.btnAllLevelsFcu.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllLevelsFcu.Location = new System.Drawing.Point(786, 24);
            this.btnAllLevelsFcu.Name = "btnAllLevelsFcu";
            this.btnAllLevelsFcu.Size = new System.Drawing.Size(130, 26);
            this.btnAllLevelsFcu.TabIndex = 4;
            this.btnAllLevelsFcu.Text = "Tick all";
            this.btnAllLevelsFcu.UseVisualStyleBackColor = true;
            this.btnAllLevelsFcu.Click += new System.EventHandler(this.btnAllLevelsFcu_Click);
            //
            // btnNoLevelsFcu
            //
            this.btnNoLevelsFcu.Enabled = false;
            this.btnNoLevelsFcu.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoLevelsFcu.Location = new System.Drawing.Point(786, 56);
            this.btnNoLevelsFcu.Name = "btnNoLevelsFcu";
            this.btnNoLevelsFcu.Size = new System.Drawing.Size(130, 26);
            this.btnNoLevelsFcu.TabIndex = 5;
            this.btnNoLevelsFcu.Text = "Untick all";
            this.btnNoLevelsFcu.UseVisualStyleBackColor = true;
            this.btnNoLevelsFcu.Click += new System.EventHandler(this.btnNoLevelsFcu_Click);
            //
            // grpChecksFcu
            //
            this.grpChecksFcu.Controls.Add(this.clbFcu);
            this.grpChecksFcu.Controls.Add(this.btnCheckFcu);
            this.grpChecksFcu.Controls.Add(this.btnFixFcu);
            this.grpChecksFcu.Controls.Add(this.lblFcuInfo);
            this.grpChecksFcu.Controls.Add(this.lblFcuFile);
            this.grpChecksFcu.Controls.Add(this.txtFcuFile);
            this.grpChecksFcu.Controls.Add(this.btnFcuBrowse);
            this.grpChecksFcu.Controls.Add(this.lblFcuSheet);
            this.grpChecksFcu.Controls.Add(this.cmbFcuSheet);
            this.grpChecksFcu.Controls.Add(this.lblFcuLoaded);
            this.grpChecksFcu.Controls.Add(this.lblFcuParam);
            this.grpChecksFcu.Controls.Add(this.cmbFcuParam);
            this.grpChecksFcu.Controls.Add(this.lblFcuTol);
            this.grpChecksFcu.Controls.Add(this.numFcuPct);
            this.grpChecksFcu.Controls.Add(this.lblFcuPctUnit);
            this.grpChecksFcu.Controls.Add(this.lblFcuOr);
            this.grpChecksFcu.Controls.Add(this.numFcuAbs);
            this.grpChecksFcu.Controls.Add(this.lblFcuAbsUnit);
            this.grpChecksFcu.Controls.Add(this.lblFcuWhich);
            this.grpChecksFcu.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpChecksFcu.Location = new System.Drawing.Point(10, 168);
            this.grpChecksFcu.Name = "grpChecksFcu";
            this.grpChecksFcu.Size = new System.Drawing.Size(928, 186);
            this.grpChecksFcu.TabIndex = 1;
            this.grpChecksFcu.TabStop = false;
            this.grpChecksFcu.Text = "2.  Checks to run";
            //
            // clbFcu
            //
            this.clbFcu.CheckOnClick = true;
            this.clbFcu.FormattingEnabled = true;
            this.clbFcu.IntegralHeight = false;
            this.clbFcu.Location = new System.Drawing.Point(12, 24);
            this.clbFcu.Name = "clbFcu";
            this.clbFcu.Size = new System.Drawing.Size(420, 88);
            this.clbFcu.TabIndex = 0;
            this.clbFcu.SelectedIndexChanged += new System.EventHandler(this.clbFcu_SelectedIndexChanged);
            //
            // btnCheckFcu
            //
            this.btnCheckFcu.Location = new System.Drawing.Point(12, 120);
            this.btnCheckFcu.Name = "btnCheckFcu";
            this.btnCheckFcu.Size = new System.Drawing.Size(200, 30);
            this.btnCheckFcu.TabIndex = 1;
            this.btnCheckFcu.Text = "Run the ticked checks";
            this.btnCheckFcu.UseVisualStyleBackColor = true;
            this.btnCheckFcu.Click += new System.EventHandler(this.btnCheckFcu_Click);
            //
            // btnFixFcu
            //
            this.btnFixFcu.Enabled = false;
            this.btnFixFcu.Location = new System.Drawing.Point(222, 120);
            this.btnFixFcu.Name = "btnFixFcu";
            this.btnFixFcu.Size = new System.Drawing.Size(200, 30);
            this.btnFixFcu.TabIndex = 2;
            this.btnFixFcu.Text = "Correct the airflows";
            this.btnFixFcu.UseVisualStyleBackColor = true;
            this.btnFixFcu.Click += new System.EventHandler(this.btnFixFcu_Click);
            //
            // lblFcuInfo
            //
            this.lblFcuInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblFcuInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFcuInfo.Location = new System.Drawing.Point(12, 156);
            this.lblFcuInfo.Name = "lblFcuInfo";
            this.lblFcuInfo.Size = new System.Drawing.Size(900, 24);
            this.lblFcuInfo.TabIndex = 3;
            this.lblFcuInfo.Text = "";
            //
            // lblFcuFile
            //
            this.lblFcuFile.Location = new System.Drawing.Point(448, 25);
            this.lblFcuFile.Name = "lblFcuFile";
            this.lblFcuFile.Size = new System.Drawing.Size(62, 18);
            this.lblFcuFile.TabIndex = 4;
            this.lblFcuFile.Text = "Schedule";
            //
            // txtFcuFile
            //
            this.txtFcuFile.Location = new System.Drawing.Point(512, 22);
            this.txtFcuFile.Name = "txtFcuFile";
            this.txtFcuFile.ReadOnly = true;
            this.txtFcuFile.Size = new System.Drawing.Size(316, 20);
            this.txtFcuFile.TabIndex = 5;
            //
            // btnFcuBrowse
            //
            this.btnFcuBrowse.Location = new System.Drawing.Point(834, 21);
            this.btnFcuBrowse.Name = "btnFcuBrowse";
            this.btnFcuBrowse.Size = new System.Drawing.Size(78, 23);
            this.btnFcuBrowse.TabIndex = 6;
            this.btnFcuBrowse.Text = "Browse...";
            this.btnFcuBrowse.UseVisualStyleBackColor = true;
            this.btnFcuBrowse.Click += new System.EventHandler(this.btnFcuBrowse_Click);
            //
            // lblFcuSheet
            //
            this.lblFcuSheet.Location = new System.Drawing.Point(448, 53);
            this.lblFcuSheet.Name = "lblFcuSheet";
            this.lblFcuSheet.Size = new System.Drawing.Size(62, 18);
            this.lblFcuSheet.TabIndex = 7;
            this.lblFcuSheet.Text = "Worksheet";
            //
            // cmbFcuSheet
            //
            this.cmbFcuSheet.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFcuSheet.FormattingEnabled = true;
            this.cmbFcuSheet.Location = new System.Drawing.Point(512, 50);
            this.cmbFcuSheet.Name = "cmbFcuSheet";
            this.cmbFcuSheet.Size = new System.Drawing.Size(200, 21);
            this.cmbFcuSheet.TabIndex = 8;
            this.cmbFcuSheet.SelectedIndexChanged += new System.EventHandler(this.cmbFcuSheet_SelectedIndexChanged);
            //
            // lblFcuLoaded
            //
            this.lblFcuLoaded.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblFcuLoaded.Location = new System.Drawing.Point(720, 53);
            this.lblFcuLoaded.Name = "lblFcuLoaded";
            this.lblFcuLoaded.Size = new System.Drawing.Size(192, 18);
            this.lblFcuLoaded.TabIndex = 9;
            this.lblFcuLoaded.Text = "no schedule loaded";
            //
            // lblFcuParam
            //
            this.lblFcuParam.Location = new System.Drawing.Point(448, 81);
            this.lblFcuParam.Name = "lblFcuParam";
            this.lblFcuParam.Size = new System.Drawing.Size(110, 18);
            this.lblFcuParam.TabIndex = 10;
            this.lblFcuParam.Text = "FCU number is in";
            //
            // cmbFcuParam
            //
            this.cmbFcuParam.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFcuParam.FormattingEnabled = true;
            this.cmbFcuParam.Location = new System.Drawing.Point(560, 78);
            this.cmbFcuParam.Name = "cmbFcuParam";
            this.cmbFcuParam.Size = new System.Drawing.Size(200, 21);
            this.cmbFcuParam.TabIndex = 11;
            //
            // lblFcuTol
            //
            this.lblFcuTol.Location = new System.Drawing.Point(448, 109);
            this.lblFcuTol.Name = "lblFcuTol";
            this.lblFcuTol.Size = new System.Drawing.Size(110, 18);
            this.lblFcuTol.TabIndex = 12;
            this.lblFcuTol.Text = "Wrong if out by";
            //
            // numFcuPct
            //
            this.numFcuPct.DecimalPlaces = 1;
            this.numFcuPct.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            this.numFcuPct.Location = new System.Drawing.Point(560, 106);
            this.numFcuPct.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numFcuPct.Name = "numFcuPct";
            this.numFcuPct.Size = new System.Drawing.Size(52, 20);
            this.numFcuPct.TabIndex = 13;
            this.numFcuPct.Value = new decimal(new int[] { 2, 0, 0, 0 });
            //
            // lblFcuPctUnit
            //
            this.lblFcuPctUnit.Location = new System.Drawing.Point(616, 109);
            this.lblFcuPctUnit.Name = "lblFcuPctUnit";
            this.lblFcuPctUnit.Size = new System.Drawing.Size(16, 18);
            this.lblFcuPctUnit.TabIndex = 14;
            this.lblFcuPctUnit.Text = "%";
            //
            // lblFcuOr
            //
            this.lblFcuOr.Location = new System.Drawing.Point(636, 109);
            this.lblFcuOr.Name = "lblFcuOr";
            this.lblFcuOr.Size = new System.Drawing.Size(18, 18);
            this.lblFcuOr.TabIndex = 15;
            this.lblFcuOr.Text = "or";
            //
            // numFcuAbs
            //
            this.numFcuAbs.Location = new System.Drawing.Point(658, 106);
            this.numFcuAbs.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numFcuAbs.Name = "numFcuAbs";
            this.numFcuAbs.Size = new System.Drawing.Size(52, 20);
            this.numFcuAbs.TabIndex = 16;
            this.numFcuAbs.Value = new decimal(new int[] { 5, 0, 0, 0 });
            //
            // lblFcuAbsUnit
            //
            this.lblFcuAbsUnit.Location = new System.Drawing.Point(714, 109);
            this.lblFcuAbsUnit.Name = "lblFcuAbsUnit";
            this.lblFcuAbsUnit.Size = new System.Drawing.Size(30, 18);
            this.lblFcuAbsUnit.TabIndex = 17;
            this.lblFcuAbsUnit.Text = "L/s";
            //
            // lblFcuWhich
            //
            this.lblFcuWhich.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblFcuWhich.Location = new System.Drawing.Point(746, 109);
            this.lblFcuWhich.Name = "lblFcuWhich";
            this.lblFcuWhich.Size = new System.Drawing.Size(166, 18);
            this.lblFcuWhich.TabIndex = 18;
            this.lblFcuWhich.Text = "whichever is the larger";
            //
            // grpFindingsFcu
            //
            this.grpFindingsFcu.Controls.Add(this.dgvFcu);
            this.grpFindingsFcu.Controls.Add(this.lblSummaryFcu);
            this.grpFindingsFcu.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFindingsFcu.Location = new System.Drawing.Point(10, 362);
            this.grpFindingsFcu.Name = "grpFindingsFcu";
            this.grpFindingsFcu.Size = new System.Drawing.Size(928, 340);
            this.grpFindingsFcu.TabIndex = 2;
            this.grpFindingsFcu.TabStop = false;
            this.grpFindingsFcu.Text = "3.  What was found";
            //
            // dgvFcu
            //
            this.dgvFcu.AllowUserToAddRows = false;
            this.dgvFcu.AllowUserToDeleteRows = false;
            this.dgvFcu.AllowUserToResizeRows = false;
            this.dgvFcu.AllowUserToOrderColumns = true;
            this.dgvFcu.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvFcu.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvFcu.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvFcu.Location = new System.Drawing.Point(12, 24);
            this.dgvFcu.MultiSelect = true;
            this.dgvFcu.Name = "dgvFcu";
            this.dgvFcu.ReadOnly = true;
            this.dgvFcu.RowHeadersVisible = false;
            this.dgvFcu.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvFcu.Size = new System.Drawing.Size(904, 270);
            this.dgvFcu.TabIndex = 0;
            //
            // lblSummaryFcu
            //
            this.lblSummaryFcu.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummaryFcu.Location = new System.Drawing.Point(12, 300);
            this.lblSummaryFcu.Name = "lblSummaryFcu";
            this.lblSummaryFcu.Size = new System.Drawing.Size(904, 32);
            this.lblSummaryFcu.TabIndex = 1;
            this.lblSummaryFcu.Text = "";
            //
            // ==================================================
            // CHW page
            // ==================================================
            //
            // tabChwJ
            //
            this.tabChwJ.Controls.Add(this.grpScopeChwJ);
            this.tabChwJ.Controls.Add(this.grpChecksChwJ);
            this.tabChwJ.Controls.Add(this.grpFindingsChwJ);
            this.tabChwJ.Location = new System.Drawing.Point(4, 22);
            this.tabChwJ.Name = "tabChwJ";
            this.tabChwJ.Size = new System.Drawing.Size(948, 706);
            this.tabChwJ.TabIndex = 5;
            this.tabChwJ.Text = "CHW pipe disconnection check";
            this.tabChwJ.UseVisualStyleBackColor = true;
            //
            // grpScopeChwJ
            //
            this.grpScopeChwJ.Controls.Add(this.rbAllChwJ);
            this.grpScopeChwJ.Controls.Add(this.rbPickedChwJ);
            this.grpScopeChwJ.Controls.Add(this.rbActiveChwJ);
            this.grpScopeChwJ.Controls.Add(this.clbLevelsChwJ);
            this.grpScopeChwJ.Controls.Add(this.btnAllLevelsChwJ);
            this.grpScopeChwJ.Controls.Add(this.btnNoLevelsChwJ);
            this.grpScopeChwJ.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpScopeChwJ.Location = new System.Drawing.Point(10, 8);
            this.grpScopeChwJ.Name = "grpScopeChwJ";
            this.grpScopeChwJ.Size = new System.Drawing.Size(928, 152);
            this.grpScopeChwJ.TabIndex = 0;
            this.grpScopeChwJ.TabStop = false;
            this.grpScopeChwJ.Text = "1.  What to check";
            //
            // rbAllChwJ
            //
            this.rbAllChwJ.Checked = true;
            this.rbAllChwJ.Location = new System.Drawing.Point(12, 24);
            this.rbAllChwJ.Name = "rbAllChwJ";
            this.rbAllChwJ.Size = new System.Drawing.Size(280, 20);
            this.rbAllChwJ.TabIndex = 0;
            this.rbAllChwJ.TabStop = true;
            this.rbAllChwJ.Text = "Everywhere in the model";
            this.rbAllChwJ.UseVisualStyleBackColor = true;
            this.rbAllChwJ.CheckedChanged += new System.EventHandler(this.ScopeChwJ_CheckedChanged);
            //
            // rbPickedChwJ
            //
            this.rbPickedChwJ.Location = new System.Drawing.Point(12, 48);
            this.rbPickedChwJ.Name = "rbPickedChwJ";
            this.rbPickedChwJ.Size = new System.Drawing.Size(280, 20);
            this.rbPickedChwJ.TabIndex = 1;
            this.rbPickedChwJ.Text = "Only the levels ticked here";
            this.rbPickedChwJ.UseVisualStyleBackColor = true;
            this.rbPickedChwJ.CheckedChanged += new System.EventHandler(this.ScopeChwJ_CheckedChanged);
            //
            // rbActiveChwJ
            //
            this.rbActiveChwJ.Location = new System.Drawing.Point(12, 72);
            this.rbActiveChwJ.Name = "rbActiveChwJ";
            this.rbActiveChwJ.Size = new System.Drawing.Size(280, 20);
            this.rbActiveChwJ.TabIndex = 2;
            this.rbActiveChwJ.Text = "The view open now";
            this.rbActiveChwJ.UseVisualStyleBackColor = true;
            this.rbActiveChwJ.CheckedChanged += new System.EventHandler(this.ScopeChwJ_CheckedChanged);
            //
            // clbLevelsChwJ
            //
            this.clbLevelsChwJ.CheckOnClick = true;
            this.clbLevelsChwJ.Enabled = false;
            this.clbLevelsChwJ.FormattingEnabled = true;
            this.clbLevelsChwJ.IntegralHeight = false;
            this.clbLevelsChwJ.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbLevelsChwJ.Location = new System.Drawing.Point(300, 24);
            this.clbLevelsChwJ.Name = "clbLevelsChwJ";
            this.clbLevelsChwJ.Size = new System.Drawing.Size(470, 116);
            this.clbLevelsChwJ.TabIndex = 3;
            //
            // btnAllLevelsChwJ
            //
            this.btnAllLevelsChwJ.Enabled = false;
            this.btnAllLevelsChwJ.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllLevelsChwJ.Location = new System.Drawing.Point(786, 24);
            this.btnAllLevelsChwJ.Name = "btnAllLevelsChwJ";
            this.btnAllLevelsChwJ.Size = new System.Drawing.Size(130, 26);
            this.btnAllLevelsChwJ.TabIndex = 4;
            this.btnAllLevelsChwJ.Text = "Tick all";
            this.btnAllLevelsChwJ.UseVisualStyleBackColor = true;
            this.btnAllLevelsChwJ.Click += new System.EventHandler(this.btnAllLevelsChwJ_Click);
            //
            // btnNoLevelsChwJ
            //
            this.btnNoLevelsChwJ.Enabled = false;
            this.btnNoLevelsChwJ.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoLevelsChwJ.Location = new System.Drawing.Point(786, 56);
            this.btnNoLevelsChwJ.Name = "btnNoLevelsChwJ";
            this.btnNoLevelsChwJ.Size = new System.Drawing.Size(130, 26);
            this.btnNoLevelsChwJ.TabIndex = 5;
            this.btnNoLevelsChwJ.Text = "Untick all";
            this.btnNoLevelsChwJ.UseVisualStyleBackColor = true;
            this.btnNoLevelsChwJ.Click += new System.EventHandler(this.btnNoLevelsChwJ_Click);
            //
            // grpChecksChwJ
            //
            this.grpChecksChwJ.Controls.Add(this.clbChwJ);
            this.grpChecksChwJ.Controls.Add(this.btnCheckChwJ);
            this.grpChecksChwJ.Controls.Add(this.btnFixChwJ);
            this.grpChecksChwJ.Controls.Add(this.lblChwJInfo);
            this.grpChecksChwJ.Controls.Add(this.lblChwJReach);
            this.grpChecksChwJ.Controls.Add(this.numChwJReach);
            this.grpChecksChwJ.Controls.Add(this.lblChwJReachUnit);
            this.grpChecksChwJ.Controls.Add(this.lblChwJNote);
            this.grpChecksChwJ.Controls.Add(this.chkChwJUnknown);
            this.grpChecksChwJ.Controls.Add(this.lblChwJSystems);
            this.grpChecksChwJ.Controls.Add(this.clbChwJSystems);
            this.grpChecksChwJ.Controls.Add(this.btnAllChwJSystems);
            this.grpChecksChwJ.Controls.Add(this.btnNoChwJSystems);
            this.grpChecksChwJ.Controls.Add(this.lblChwJFamilies);
            this.grpChecksChwJ.Controls.Add(this.clbChwJFamilies);
            this.grpChecksChwJ.Controls.Add(this.btnAllChwJFamilies);
            this.grpChecksChwJ.Controls.Add(this.btnNoChwJFamilies);
            this.grpChecksChwJ.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpChecksChwJ.Location = new System.Drawing.Point(10, 168);
            this.grpChecksChwJ.Name = "grpChecksChwJ";
            this.grpChecksChwJ.Size = new System.Drawing.Size(928, 304);
            this.grpChecksChwJ.TabIndex = 1;
            this.grpChecksChwJ.TabStop = false;
            this.grpChecksChwJ.Text = "2.  Checks to run";
            //
            // clbChwJ
            //
            this.clbChwJ.CheckOnClick = true;
            this.clbChwJ.FormattingEnabled = true;
            this.clbChwJ.IntegralHeight = false;
            this.clbChwJ.Location = new System.Drawing.Point(12, 24);
            this.clbChwJ.Name = "clbChwJ";
            this.clbChwJ.Size = new System.Drawing.Size(420, 150);
            this.clbChwJ.TabIndex = 0;
            this.clbChwJ.SelectedIndexChanged += new System.EventHandler(this.clbChwJ_SelectedIndexChanged);
            //
            // btnCheckChwJ
            //
            this.btnCheckChwJ.Location = new System.Drawing.Point(12, 184);
            this.btnCheckChwJ.Name = "btnCheckChwJ";
            this.btnCheckChwJ.Size = new System.Drawing.Size(200, 30);
            this.btnCheckChwJ.TabIndex = 1;
            this.btnCheckChwJ.Text = "Run the ticked checks";
            this.btnCheckChwJ.UseVisualStyleBackColor = true;
            this.btnCheckChwJ.Click += new System.EventHandler(this.btnCheckChwJ_Click);
            //
            // btnFixChwJ
            //
            this.btnFixChwJ.Enabled = false;
            this.btnFixChwJ.Location = new System.Drawing.Point(222, 184);
            this.btnFixChwJ.Name = "btnFixChwJ";
            this.btnFixChwJ.Size = new System.Drawing.Size(200, 30);
            this.btnFixChwJ.TabIndex = 2;
            this.btnFixChwJ.Text = "Join the pipes up";
            this.btnFixChwJ.UseVisualStyleBackColor = true;
            this.btnFixChwJ.Click += new System.EventHandler(this.btnFixChwJ_Click);
            //
            // lblChwJInfo
            //
            this.lblChwJInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblChwJInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblChwJInfo.Location = new System.Drawing.Point(12, 224);
            this.lblChwJInfo.Name = "lblChwJInfo";
            this.lblChwJInfo.Size = new System.Drawing.Size(430, 28);
            this.lblChwJInfo.TabIndex = 3;
            this.lblChwJInfo.Text = "";
            //
            // lblChwJReach
            //
            this.lblChwJReach.Location = new System.Drawing.Point(448, 27);
            this.lblChwJReach.Name = "lblChwJReach";
            this.lblChwJReach.Size = new System.Drawing.Size(146, 18);
            this.lblChwJReach.TabIndex = 5;
            this.lblChwJReach.Text = "Look for a pipe within";
            //
            // numChwJReach
            //
            this.numChwJReach.Increment = new decimal(new int[] { 50, 0, 0, 0 });
            this.numChwJReach.Location = new System.Drawing.Point(598, 24);
            this.numChwJReach.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            this.numChwJReach.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numChwJReach.Name = "numChwJReach";
            this.numChwJReach.Size = new System.Drawing.Size(64, 20);
            this.numChwJReach.TabIndex = 6;
            this.numChwJReach.Value = new decimal(new int[] { 300, 0, 0, 0 });
            //
            // lblChwJReachUnit
            //
            this.lblChwJReachUnit.Location = new System.Drawing.Point(668, 27);
            this.lblChwJReachUnit.Name = "lblChwJReachUnit";
            this.lblChwJReachUnit.Size = new System.Drawing.Size(30, 18);
            this.lblChwJReachUnit.TabIndex = 7;
            this.lblChwJReachUnit.Text = "mm";
            //
            // lblChwJNote
            //
            this.lblChwJNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblChwJNote.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblChwJNote.Location = new System.Drawing.Point(12, 254);
            this.lblChwJNote.Name = "lblChwJNote";
            this.lblChwJNote.Size = new System.Drawing.Size(430, 42);
            this.lblChwJNote.TabIndex = 8;
            this.lblChwJNote.Text = "Pipes only - never ductwork. Supply joins supply and return joins return; a connector with no system set is matched on position instead. The unit is never moved; the pipe is.";
            //
            // chkChwJUnknown
            //
            this.chkChwJUnknown.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.chkChwJUnknown.Checked = true;
            this.chkChwJUnknown.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkChwJUnknown.Location = new System.Drawing.Point(448, 266);
            this.chkChwJUnknown.Name = "chkChwJUnknown";
            this.chkChwJUnknown.Size = new System.Drawing.Size(464, 20);
            this.chkChwJUnknown.TabIndex = 17;
            this.chkChwJUnknown.Text = "Also join where the unit's connector has no system set (VRV families)";
            this.chkChwJUnknown.UseVisualStyleBackColor = true;
            //
            // lblChwJSystems
            //
            this.lblChwJSystems.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblChwJSystems.Location = new System.Drawing.Point(448, 50);
            this.lblChwJSystems.Name = "lblChwJSystems";
            this.lblChwJSystems.Size = new System.Drawing.Size(464, 16);
            this.lblChwJSystems.TabIndex = 9;
            this.lblChwJSystems.Text = "Piping system types to check";
            //
            // clbChwJSystems
            //
            this.clbChwJSystems.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbChwJSystems.CheckOnClick = true;
            this.clbChwJSystems.FormattingEnabled = true;
            this.clbChwJSystems.HorizontalScrollbar = true;
            this.clbChwJSystems.IntegralHeight = false;
            this.clbChwJSystems.Location = new System.Drawing.Point(448, 68);
            this.clbChwJSystems.Name = "clbChwJSystems";
            this.clbChwJSystems.Size = new System.Drawing.Size(330, 84);
            this.clbChwJSystems.TabIndex = 10;
            //
            // btnAllChwJSystems
            //
            this.btnAllChwJSystems.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllChwJSystems.Location = new System.Drawing.Point(786, 68);
            this.btnAllChwJSystems.Name = "btnAllChwJSystems";
            this.btnAllChwJSystems.Size = new System.Drawing.Size(126, 26);
            this.btnAllChwJSystems.TabIndex = 11;
            this.btnAllChwJSystems.Text = "Tick all";
            this.btnAllChwJSystems.UseVisualStyleBackColor = true;
            this.btnAllChwJSystems.Click += new System.EventHandler(this.btnAllChwJSystems_Click);
            //
            // btnNoChwJSystems
            //
            this.btnNoChwJSystems.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoChwJSystems.Location = new System.Drawing.Point(786, 100);
            this.btnNoChwJSystems.Name = "btnNoChwJSystems";
            this.btnNoChwJSystems.Size = new System.Drawing.Size(126, 26);
            this.btnNoChwJSystems.TabIndex = 12;
            this.btnNoChwJSystems.Text = "None";
            this.btnNoChwJSystems.UseVisualStyleBackColor = true;
            this.btnNoChwJSystems.Click += new System.EventHandler(this.btnNoChwJSystems_Click);
            //
            // lblChwJFamilies
            //
            this.lblChwJFamilies.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblChwJFamilies.Location = new System.Drawing.Point(448, 158);
            this.lblChwJFamilies.Name = "lblChwJFamilies";
            this.lblChwJFamilies.Size = new System.Drawing.Size(464, 16);
            this.lblChwJFamilies.TabIndex = 13;
            this.lblChwJFamilies.Text = "Equipment families a pipe may be joined to";
            //
            // clbChwJFamilies
            //
            this.clbChwJFamilies.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbChwJFamilies.CheckOnClick = true;
            this.clbChwJFamilies.FormattingEnabled = true;
            this.clbChwJFamilies.HorizontalScrollbar = true;
            this.clbChwJFamilies.IntegralHeight = false;
            this.clbChwJFamilies.Location = new System.Drawing.Point(448, 176);
            this.clbChwJFamilies.Name = "clbChwJFamilies";
            this.clbChwJFamilies.Size = new System.Drawing.Size(330, 84);
            this.clbChwJFamilies.TabIndex = 14;
            //
            // btnAllChwJFamilies
            //
            this.btnAllChwJFamilies.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllChwJFamilies.Location = new System.Drawing.Point(786, 176);
            this.btnAllChwJFamilies.Name = "btnAllChwJFamilies";
            this.btnAllChwJFamilies.Size = new System.Drawing.Size(126, 26);
            this.btnAllChwJFamilies.TabIndex = 15;
            this.btnAllChwJFamilies.Text = "Tick all";
            this.btnAllChwJFamilies.UseVisualStyleBackColor = true;
            this.btnAllChwJFamilies.Click += new System.EventHandler(this.btnAllChwJFamilies_Click);
            //
            // btnNoChwJFamilies
            //
            this.btnNoChwJFamilies.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoChwJFamilies.Location = new System.Drawing.Point(786, 208);
            this.btnNoChwJFamilies.Name = "btnNoChwJFamilies";
            this.btnNoChwJFamilies.Size = new System.Drawing.Size(126, 26);
            this.btnNoChwJFamilies.TabIndex = 16;
            this.btnNoChwJFamilies.Text = "None";
            this.btnNoChwJFamilies.UseVisualStyleBackColor = true;
            this.btnNoChwJFamilies.Click += new System.EventHandler(this.btnNoChwJFamilies_Click);
            //
            // grpFindingsChwJ
            //
            this.grpFindingsChwJ.Controls.Add(this.dgvChwJ);
            this.grpFindingsChwJ.Controls.Add(this.lblSummaryChwJ);
            this.grpFindingsChwJ.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFindingsChwJ.Location = new System.Drawing.Point(10, 480);
            this.grpFindingsChwJ.Name = "grpFindingsChwJ";
            this.grpFindingsChwJ.Size = new System.Drawing.Size(928, 222);
            this.grpFindingsChwJ.TabIndex = 2;
            this.grpFindingsChwJ.TabStop = false;
            this.grpFindingsChwJ.Text = "3.  What was found";
            //
            // dgvChwJ
            //
            this.dgvChwJ.AllowUserToAddRows = false;
            this.dgvChwJ.AllowUserToDeleteRows = false;
            this.dgvChwJ.AllowUserToResizeRows = false;
            this.dgvChwJ.AllowUserToOrderColumns = true;
            this.dgvChwJ.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvChwJ.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvChwJ.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvChwJ.Location = new System.Drawing.Point(12, 24);
            this.dgvChwJ.MultiSelect = true;
            this.dgvChwJ.Name = "dgvChwJ";
            this.dgvChwJ.ReadOnly = true;
            this.dgvChwJ.RowHeadersVisible = false;
            this.dgvChwJ.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvChwJ.Size = new System.Drawing.Size(904, 152);
            this.dgvChwJ.TabIndex = 0;
            //
            // lblSummaryChwJ
            //
            this.lblSummaryChwJ.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummaryChwJ.Location = new System.Drawing.Point(12, 182);
            this.lblSummaryChwJ.Name = "lblSummaryChwJ";
            this.lblSummaryChwJ.Size = new System.Drawing.Size(904, 32);
            this.lblSummaryChwJ.TabIndex = 1;
            this.lblSummaryChwJ.Text = "";
            //
            // ==================================================
            // FCU page
            // ==================================================
            //
            // tabChw
            //
            this.tabChw.Controls.Add(this.grpScopeChw);
            this.tabChw.Controls.Add(this.grpChecksChw);
            this.tabChw.Controls.Add(this.grpFindingsChw);
            this.tabChw.Location = new System.Drawing.Point(4, 22);
            this.tabChw.Name = "tabChw";
            this.tabChw.Size = new System.Drawing.Size(948, 706);
            this.tabChw.TabIndex = 4;
            this.tabChw.Text = "CHW check";
            this.tabChw.UseVisualStyleBackColor = true;
            //
            // grpScopeChw
            //
            this.grpScopeChw.Controls.Add(this.rbAllChw);
            this.grpScopeChw.Controls.Add(this.rbPickedChw);
            this.grpScopeChw.Controls.Add(this.rbActiveChw);
            this.grpScopeChw.Controls.Add(this.clbLevelsChw);
            this.grpScopeChw.Controls.Add(this.btnAllLevelsChw);
            this.grpScopeChw.Controls.Add(this.btnNoLevelsChw);
            this.grpScopeChw.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpScopeChw.Location = new System.Drawing.Point(10, 8);
            this.grpScopeChw.Name = "grpScopeChw";
            this.grpScopeChw.Size = new System.Drawing.Size(928, 152);
            this.grpScopeChw.TabIndex = 0;
            this.grpScopeChw.TabStop = false;
            this.grpScopeChw.Text = "1.  What to check";
            //
            // rbAllChw
            //
            this.rbAllChw.Checked = true;
            this.rbAllChw.Location = new System.Drawing.Point(12, 24);
            this.rbAllChw.Name = "rbAllChw";
            this.rbAllChw.Size = new System.Drawing.Size(280, 20);
            this.rbAllChw.TabIndex = 0;
            this.rbAllChw.TabStop = true;
            this.rbAllChw.Text = "Everywhere in the model";
            this.rbAllChw.UseVisualStyleBackColor = true;
            this.rbAllChw.CheckedChanged += new System.EventHandler(this.ScopeChw_CheckedChanged);
            //
            // rbPickedChw
            //
            this.rbPickedChw.Location = new System.Drawing.Point(12, 48);
            this.rbPickedChw.Name = "rbPickedChw";
            this.rbPickedChw.Size = new System.Drawing.Size(280, 20);
            this.rbPickedChw.TabIndex = 1;
            this.rbPickedChw.Text = "Only the levels ticked here";
            this.rbPickedChw.UseVisualStyleBackColor = true;
            this.rbPickedChw.CheckedChanged += new System.EventHandler(this.ScopeChw_CheckedChanged);
            //
            // rbActiveChw
            //
            this.rbActiveChw.Location = new System.Drawing.Point(12, 72);
            this.rbActiveChw.Name = "rbActiveChw";
            this.rbActiveChw.Size = new System.Drawing.Size(280, 20);
            this.rbActiveChw.TabIndex = 2;
            this.rbActiveChw.Text = "The view open now";
            this.rbActiveChw.UseVisualStyleBackColor = true;
            this.rbActiveChw.CheckedChanged += new System.EventHandler(this.ScopeChw_CheckedChanged);
            //
            // clbLevelsChw
            //
            this.clbLevelsChw.CheckOnClick = true;
            this.clbLevelsChw.Enabled = false;
            this.clbLevelsChw.FormattingEnabled = true;
            this.clbLevelsChw.IntegralHeight = false;
            this.clbLevelsChw.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbLevelsChw.Location = new System.Drawing.Point(300, 24);
            this.clbLevelsChw.Name = "clbLevelsChw";
            this.clbLevelsChw.Size = new System.Drawing.Size(470, 116);
            this.clbLevelsChw.TabIndex = 3;
            //
            // btnAllLevelsChw
            //
            this.btnAllLevelsChw.Enabled = false;
            this.btnAllLevelsChw.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllLevelsChw.Location = new System.Drawing.Point(786, 24);
            this.btnAllLevelsChw.Name = "btnAllLevelsChw";
            this.btnAllLevelsChw.Size = new System.Drawing.Size(130, 26);
            this.btnAllLevelsChw.TabIndex = 4;
            this.btnAllLevelsChw.Text = "Tick all";
            this.btnAllLevelsChw.UseVisualStyleBackColor = true;
            this.btnAllLevelsChw.Click += new System.EventHandler(this.btnAllLevelsChw_Click);
            //
            // btnNoLevelsChw
            //
            this.btnNoLevelsChw.Enabled = false;
            this.btnNoLevelsChw.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoLevelsChw.Location = new System.Drawing.Point(786, 56);
            this.btnNoLevelsChw.Name = "btnNoLevelsChw";
            this.btnNoLevelsChw.Size = new System.Drawing.Size(130, 26);
            this.btnNoLevelsChw.TabIndex = 5;
            this.btnNoLevelsChw.Text = "Untick all";
            this.btnNoLevelsChw.UseVisualStyleBackColor = true;
            this.btnNoLevelsChw.Click += new System.EventHandler(this.btnNoLevelsChw_Click);
            //
            // grpChecksChw
            //
            this.grpChecksChw.Controls.Add(this.clbChw);
            this.grpChecksChw.Controls.Add(this.btnCheckChw);
            this.grpChecksChw.Controls.Add(this.btnFixChw);
            this.grpChecksChw.Controls.Add(this.btnChwFlow);
            this.grpChecksChw.Controls.Add(this.chkChwKeepShort);
            this.grpChecksChw.Controls.Add(this.lblChwTol);
            this.grpChecksChw.Controls.Add(this.numChwSteps);
            this.grpChecksChw.Controls.Add(this.lblChwStepsUnit);
            this.grpChecksChw.Controls.Add(this.numChwFrom);
            this.grpChecksChw.Controls.Add(this.lblChwFromUnit);
            this.grpChecksChw.Controls.Add(this.lblChwInfo);
            this.grpChecksChw.Controls.Add(this.lblChwLoaded);
            this.grpChecksChw.Controls.Add(this.btnChwExport);
            this.grpChecksChw.Controls.Add(this.btnChwImport);
            this.grpChecksChw.Controls.Add(this.btnChwBuiltin);
            this.grpChecksChw.Controls.Add(this.lblChwSupply);
            this.grpChecksChw.Controls.Add(this.clbChwSupply);
            this.grpChecksChw.Controls.Add(this.lblChwReturn);
            this.grpChecksChw.Controls.Add(this.clbChwReturn);
            this.grpChecksChw.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpChecksChw.Location = new System.Drawing.Point(10, 168);
            this.grpChecksChw.Name = "grpChecksChw";
            this.grpChecksChw.Size = new System.Drawing.Size(928, 256);
            this.grpChecksChw.TabIndex = 1;
            this.grpChecksChw.TabStop = false;
            this.grpChecksChw.Text = "2.  Checks to run";
            //
            // clbChw
            //
            this.clbChw.CheckOnClick = true;
            this.clbChw.FormattingEnabled = true;
            this.clbChw.IntegralHeight = false;
            this.clbChw.Location = new System.Drawing.Point(12, 24);
            this.clbChw.Name = "clbChw";
            this.clbChw.Size = new System.Drawing.Size(420, 56);
            this.clbChw.TabIndex = 0;
            this.clbChw.SelectedIndexChanged += new System.EventHandler(this.clbChw_SelectedIndexChanged);
            //
            // btnCheckChw
            //
            this.btnCheckChw.Location = new System.Drawing.Point(12, 88);
            this.btnCheckChw.Name = "btnCheckChw";
            this.btnCheckChw.Size = new System.Drawing.Size(200, 30);
            this.btnCheckChw.TabIndex = 1;
            this.btnCheckChw.Text = "Run the ticked checks";
            this.btnCheckChw.UseVisualStyleBackColor = true;
            this.btnCheckChw.Click += new System.EventHandler(this.btnCheckChw_Click);
            //
            // btnFixChw
            //
            this.btnFixChw.Enabled = false;
            this.btnFixChw.Location = new System.Drawing.Point(222, 88);
            this.btnFixChw.Name = "btnFixChw";
            this.btnFixChw.Size = new System.Drawing.Size(210, 30);
            this.btnFixChw.TabIndex = 16;
            this.btnFixChw.Text = "Resize the pipes";
            this.btnFixChw.UseVisualStyleBackColor = true;
            this.btnFixChw.Click += new System.EventHandler(this.btnFixChw_Click);
            //
            // btnChwFlow
            //
            this.btnChwFlow.Location = new System.Drawing.Point(12, 124);
            this.btnChwFlow.Name = "btnChwFlow";
            this.btnChwFlow.Size = new System.Drawing.Size(420, 30);
            this.btnChwFlow.TabIndex = 19;
            this.btnChwFlow.Text = "Clear the Additional Flow off EVERY pipe in the project...";
            this.btnChwFlow.UseVisualStyleBackColor = true;
            this.btnChwFlow.Click += new System.EventHandler(this.btnChwFlow_Click);
            //
            // chkChwKeepShort
            //
            this.chkChwKeepShort.Checked = true;
            this.chkChwKeepShort.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkChwKeepShort.Location = new System.Drawing.Point(12, 160);
            this.chkChwKeepShort.Name = "chkChwKeepShort";
            this.chkChwKeepShort.Size = new System.Drawing.Size(420, 20);
            this.chkChwKeepShort.TabIndex = 20;
            this.chkChwKeepShort.Text = "Slide fittings along to keep pipes Revit would delete";
            this.chkChwKeepShort.UseVisualStyleBackColor = true;
            //
            // lblChwTol
            //
            this.lblChwTol.Location = new System.Drawing.Point(448, 198);
            this.lblChwTol.Name = "lblChwTol";
            this.lblChwTol.Size = new System.Drawing.Size(80, 18);
            this.lblChwTol.TabIndex = 2;
            this.lblChwTol.Text = "Oversized if";
            //
            // numChwSteps
            //
            this.numChwSteps.Location = new System.Drawing.Point(530, 195);
            this.numChwSteps.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numChwSteps.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numChwSteps.Name = "numChwSteps";
            this.numChwSteps.Size = new System.Drawing.Size(44, 20);
            this.numChwSteps.TabIndex = 3;
            this.numChwSteps.Value = new decimal(new int[] { 2, 0, 0, 0 });
            //
            // lblChwStepsUnit
            //
            this.lblChwStepsUnit.Location = new System.Drawing.Point(578, 198);
            this.lblChwStepsUnit.Name = "lblChwStepsUnit";
            this.lblChwStepsUnit.Size = new System.Drawing.Size(96, 18);
            this.lblChwStepsUnit.TabIndex = 4;
            this.lblChwStepsUnit.Text = "sizes over, on";
            //
            // numChwFrom
            //
            this.numChwFrom.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            this.numChwFrom.Location = new System.Drawing.Point(676, 195);
            this.numChwFrom.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numChwFrom.Name = "numChwFrom";
            this.numChwFrom.Size = new System.Drawing.Size(52, 20);
            this.numChwFrom.TabIndex = 5;
            this.numChwFrom.Value = new decimal(new int[] { 50, 0, 0, 0 });
            //
            // lblChwFromUnit
            //
            this.lblChwFromUnit.Location = new System.Drawing.Point(732, 198);
            this.lblChwFromUnit.Name = "lblChwFromUnit";
            this.lblChwFromUnit.Size = new System.Drawing.Size(184, 18);
            this.lblChwFromUnit.TabIndex = 6;
            this.lblChwFromUnit.Text = "mm pipe and bigger";
            //
            // lblChwInfo
            //
            this.lblChwInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblChwInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblChwInfo.Location = new System.Drawing.Point(12, 224);
            this.lblChwInfo.Name = "lblChwInfo";
            this.lblChwInfo.Size = new System.Drawing.Size(900, 26);
            this.lblChwInfo.TabIndex = 7;
            this.lblChwInfo.Text = "";
            //
            // lblChwLoaded
            //
            this.lblChwLoaded.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblChwLoaded.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblChwLoaded.Location = new System.Drawing.Point(448, 26);
            this.lblChwLoaded.Name = "lblChwLoaded";
            this.lblChwLoaded.Size = new System.Drawing.Size(468, 32);
            this.lblChwLoaded.TabIndex = 13;
            this.lblChwLoaded.Text = "";
            //
            // btnChwExport
            //
            this.btnChwExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChwExport.Location = new System.Drawing.Point(448, 62);
            this.btnChwExport.Name = "btnChwExport";
            this.btnChwExport.Size = new System.Drawing.Size(150, 26);
            this.btnChwExport.TabIndex = 14;
            this.btnChwExport.Text = "Export table...";
            this.btnChwExport.UseVisualStyleBackColor = true;
            this.btnChwExport.Click += new System.EventHandler(this.btnChwExport_Click);
            //
            // btnChwImport
            //
            this.btnChwImport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChwImport.Location = new System.Drawing.Point(606, 62);
            this.btnChwImport.Name = "btnChwImport";
            this.btnChwImport.Size = new System.Drawing.Size(150, 26);
            this.btnChwImport.TabIndex = 15;
            this.btnChwImport.Text = "Import table...";
            this.btnChwImport.UseVisualStyleBackColor = true;
            this.btnChwImport.Click += new System.EventHandler(this.btnChwImport_Click);
            //
            // btnChwBuiltin
            //
            this.btnChwBuiltin.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChwBuiltin.Enabled = false;
            this.btnChwBuiltin.Location = new System.Drawing.Point(764, 62);
            this.btnChwBuiltin.Name = "btnChwBuiltin";
            this.btnChwBuiltin.Size = new System.Drawing.Size(152, 26);
            this.btnChwBuiltin.TabIndex = 16;
            this.btnChwBuiltin.Text = "Use the built-in one";
            this.btnChwBuiltin.UseVisualStyleBackColor = true;
            this.btnChwBuiltin.Click += new System.EventHandler(this.btnChwBuiltin_Click);
            //
            // lblChwSupply
            //
            this.lblChwSupply.Location = new System.Drawing.Point(448, 98);
            this.lblChwSupply.Name = "lblChwSupply";
            this.lblChwSupply.Size = new System.Drawing.Size(228, 18);
            this.lblChwSupply.TabIndex = 14;
            this.lblChwSupply.Text = "Chilled water SUPPLY system type(s)";
            //
            // clbChwSupply
            //
            this.clbChwSupply.CheckOnClick = true;
            this.clbChwSupply.FormattingEnabled = true;
            this.clbChwSupply.IntegralHeight = false;
            this.clbChwSupply.Location = new System.Drawing.Point(448, 118);
            this.clbChwSupply.Name = "clbChwSupply";
            this.clbChwSupply.Size = new System.Drawing.Size(228, 66);
            this.clbChwSupply.TabIndex = 15;
            //
            // lblChwReturn
            //
            this.lblChwReturn.Location = new System.Drawing.Point(688, 98);
            this.lblChwReturn.Name = "lblChwReturn";
            this.lblChwReturn.Size = new System.Drawing.Size(228, 18);
            this.lblChwReturn.TabIndex = 17;
            this.lblChwReturn.Text = "Chilled water RETURN system type(s)";
            //
            // clbChwReturn
            //
            this.clbChwReturn.CheckOnClick = true;
            this.clbChwReturn.FormattingEnabled = true;
            this.clbChwReturn.IntegralHeight = false;
            this.clbChwReturn.Location = new System.Drawing.Point(688, 118);
            this.clbChwReturn.Name = "clbChwReturn";
            this.clbChwReturn.Size = new System.Drawing.Size(228, 66);
            this.clbChwReturn.TabIndex = 18;
            //
            // grpFindingsChw
            //
            this.grpFindingsChw.Controls.Add(this.dgvChw);
            this.grpFindingsChw.Controls.Add(this.lblSummaryChw);
            this.grpFindingsChw.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFindingsChw.Location = new System.Drawing.Point(10, 432);
            this.grpFindingsChw.Name = "grpFindingsChw";
            this.grpFindingsChw.Size = new System.Drawing.Size(928, 270);
            this.grpFindingsChw.TabIndex = 2;
            this.grpFindingsChw.TabStop = false;
            this.grpFindingsChw.Text = "3.  What was found";
            //
            // dgvChw
            //
            this.dgvChw.AllowUserToAddRows = false;
            this.dgvChw.AllowUserToDeleteRows = false;
            this.dgvChw.AllowUserToResizeRows = false;
            this.dgvChw.AllowUserToOrderColumns = true;
            this.dgvChw.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvChw.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvChw.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvChw.Location = new System.Drawing.Point(12, 24);
            this.dgvChw.MultiSelect = true;
            this.dgvChw.Name = "dgvChw";
            this.dgvChw.ReadOnly = true;
            this.dgvChw.RowHeadersVisible = false;
            this.dgvChw.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvChw.Size = new System.Drawing.Size(904, 200);
            this.dgvChw.TabIndex = 0;
            //
            // lblSummaryChw
            //
            this.lblSummaryChw.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummaryChw.Location = new System.Drawing.Point(12, 230);
            this.lblSummaryChw.Name = "lblSummaryChw";
            this.lblSummaryChw.Size = new System.Drawing.Size(904, 32);
            this.lblSummaryChw.TabIndex = 1;
            this.lblSummaryChw.Text = "";
            //
            // ==================================================
            // Shared along the bottom
            // ==================================================
            //
            // chkMarkers
            //
            this.chkMarkers.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkMarkers.Location = new System.Drawing.Point(14, 754);
            this.chkMarkers.Name = "chkMarkers";
            this.chkMarkers.Size = new System.Drawing.Size(330, 20);
            this.chkMarkers.TabIndex = 1;
            this.chkMarkers.Text = "Float red markers over the findings, kept live";
            this.chkMarkers.UseVisualStyleBackColor = true;
            this.chkMarkers.CheckedChanged += new System.EventHandler(this.chkMarkers_CheckedChanged);
            //
            // lblMarkerInfo
            //
            this.lblMarkerInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblMarkerInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblMarkerInfo.Location = new System.Drawing.Point(352, 756);
            this.lblMarkerInfo.Name = "lblMarkerInfo";
            this.lblMarkerInfo.Size = new System.Drawing.Size(616, 18);
            this.lblMarkerInfo.TabIndex = 2;
            this.lblMarkerInfo.Text = "";
            //
            // btnHelp
            //
            this.btnHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnHelp.Location = new System.Drawing.Point(12, 784);
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(130, 30);
            this.btnHelp.TabIndex = 3;
            this.btnHelp.Text = "How to use this";
            this.btnHelp.UseVisualStyleBackColor = true;
            this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
            //
            // btnMark
            //
            this.btnMark.Enabled = false;
            this.btnMark.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnMark.Location = new System.Drawing.Point(152, 784);
            this.btnMark.Name = "btnMark";
            this.btnMark.Size = new System.Drawing.Size(150, 30);
            this.btnMark.TabIndex = 4;
            this.btnMark.Text = "Mark tags now";
            this.btnMark.UseVisualStyleBackColor = true;
            this.btnMark.Click += new System.EventHandler(this.btnMark_Click);
            //
            // btnClearMarks
            //
            this.btnClearMarks.Enabled = false;
            this.btnClearMarks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnClearMarks.Location = new System.Drawing.Point(312, 784);
            this.btnClearMarks.Name = "btnClearMarks";
            this.btnClearMarks.Size = new System.Drawing.Size(120, 30);
            this.btnClearMarks.TabIndex = 5;
            this.btnClearMarks.Text = "Clear marks";
            this.btnClearMarks.UseVisualStyleBackColor = true;
            this.btnClearMarks.Click += new System.EventHandler(this.btnClearMarks_Click);
            //
            // btnExport
            //
            this.btnExport.Enabled = false;
            this.btnExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExport.Location = new System.Drawing.Point(676, 784);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(160, 30);
            this.btnExport.TabIndex = 6;
            this.btnExport.Text = "Export to Excel...";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(846, 784);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(122, 30);
            this.btnClose.TabIndex = 7;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // QAForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(980, 840);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnHelp);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.btnMark);
            this.Controls.Add(this.btnClearMarks);
            this.Controls.Add(this.chkMarkers);
            this.Controls.Add(this.lblMarkerInfo);
            this.Controls.Add(this.tabChecks);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "QAForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "SHEET QA";
            this.Load += new System.EventHandler(this.QAForm_Load);
            this.tabChecks.ResumeLayout(false);
            this.tabFlow.ResumeLayout(false);
            this.tabGrids.ResumeLayout(false);
            this.grpScopeFlow.ResumeLayout(false);
            this.grpChecksFlow.ResumeLayout(false);
            this.grpFindingsFlow.ResumeLayout(false);
            this.grpScopeGrids.ResumeLayout(false);
            this.grpChecksGrids.ResumeLayout(false);
            this.grpFindingsGrids.ResumeLayout(false);
            this.tabChwJ.ResumeLayout(false);
            this.grpScopeChwJ.ResumeLayout(false);
            this.grpChecksChwJ.ResumeLayout(false);
            this.grpFindingsChwJ.ResumeLayout(false);
            this.tabDucts.ResumeLayout(false);
            this.grpScopeDucts.ResumeLayout(false);
            this.grpChecksDucts.ResumeLayout(false);
            this.grpFindingsDucts.ResumeLayout(false);
            this.tabFcu.ResumeLayout(false);
            this.grpScopeFcu.ResumeLayout(false);
            this.grpChecksFcu.ResumeLayout(false);
            this.grpChecksFcu.PerformLayout();
            this.grpFindingsFcu.ResumeLayout(false);
            this.tabChw.ResumeLayout(false);
            this.grpScopeChw.ResumeLayout(false);
            this.grpChecksChw.ResumeLayout(false);
            this.grpChecksChw.PerformLayout();
            this.grpFindingsChw.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvFlow)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvGrids)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDucts)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChwJ)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChwJReach)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDuctReach)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDuctJoin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFcu)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFcuPct)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFcuAbs)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChw)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChwSteps)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChwFrom)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGridTol)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numExtTol)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabChecks;
        private System.Windows.Forms.TabPage tabFlow;
        private System.Windows.Forms.TabPage tabGrids;
        private System.Windows.Forms.TabPage tabDucts;
        private System.Windows.Forms.TabPage tabChwJ;
        private System.Windows.Forms.GroupBox grpScopeChwJ;
        private System.Windows.Forms.RadioButton rbAllChwJ;
        private System.Windows.Forms.RadioButton rbPickedChwJ;
        private System.Windows.Forms.RadioButton rbActiveChwJ;
        private System.Windows.Forms.CheckedListBox clbLevelsChwJ;
        private System.Windows.Forms.Button btnAllLevelsChwJ;
        private System.Windows.Forms.Button btnNoLevelsChwJ;
        private System.Windows.Forms.GroupBox grpChecksChwJ;
        private System.Windows.Forms.Label lblChwJReach;
        private System.Windows.Forms.NumericUpDown numChwJReach;
        private System.Windows.Forms.Label lblChwJReachUnit;
        private System.Windows.Forms.Label lblChwJNote;
        private System.Windows.Forms.CheckBox chkChwJUnknown;
        private System.Windows.Forms.Label lblChwJSystems;
        private System.Windows.Forms.CheckedListBox clbChwJSystems;
        private System.Windows.Forms.Button btnAllChwJSystems;
        private System.Windows.Forms.Button btnNoChwJSystems;
        private System.Windows.Forms.Label lblChwJFamilies;
        private System.Windows.Forms.CheckedListBox clbChwJFamilies;
        private System.Windows.Forms.Button btnAllChwJFamilies;
        private System.Windows.Forms.Button btnNoChwJFamilies;
        private System.Windows.Forms.CheckedListBox clbChwJ;
        private System.Windows.Forms.Button btnCheckChwJ;
        private System.Windows.Forms.Button btnFixChwJ;
        private System.Windows.Forms.Label lblChwJInfo;
        private System.Windows.Forms.GroupBox grpFindingsChwJ;
        private System.Windows.Forms.DataGridView dgvChwJ;
        private System.Windows.Forms.Label lblSummaryChwJ;
        private System.Windows.Forms.TabPage tabFcu;
        private System.Windows.Forms.TabPage tabChw;

        private System.Windows.Forms.GroupBox grpScopeFlow;
        private System.Windows.Forms.RadioButton rbAllFlow;
        private System.Windows.Forms.RadioButton rbPickedFlow;
        private System.Windows.Forms.RadioButton rbActiveFlow;
        private System.Windows.Forms.CheckedListBox clbSheetsFlow;
        private System.Windows.Forms.Button btnAllSheetsFlow;
        private System.Windows.Forms.Button btnNoSheetsFlow;
        private System.Windows.Forms.GroupBox grpChecksFlow;
        private System.Windows.Forms.CheckedListBox clbFlow;
        private System.Windows.Forms.Button btnRunFlow;
        private System.Windows.Forms.Label lblFlowInfo;
        private System.Windows.Forms.GroupBox grpFindingsFlow;
        private System.Windows.Forms.DataGridView dgvFlow;
        private System.Windows.Forms.Label lblSummaryFlow;

        private System.Windows.Forms.GroupBox grpScopeGrids;
        private System.Windows.Forms.RadioButton rbAllGrids;
        private System.Windows.Forms.RadioButton rbPickedGrids;
        private System.Windows.Forms.RadioButton rbActiveGrids;
        private System.Windows.Forms.CheckedListBox clbSheetsGrids;
        private System.Windows.Forms.Button btnAllSheetsGrids;
        private System.Windows.Forms.Button btnNoSheetsGrids;
        private System.Windows.Forms.GroupBox grpChecksGrids;
        private System.Windows.Forms.CheckedListBox clbGrids;
        private System.Windows.Forms.Button btnCheckGrids;
        private System.Windows.Forms.Button btnFixGrids;
        private System.Windows.Forms.Label lblGridsInfo;
        private System.Windows.Forms.Label lblGridLink;
        private System.Windows.Forms.CheckedListBox clbGridLinks;
        private System.Windows.Forms.Label lblBubbles;
        private System.Windows.Forms.ComboBox cmbBubbles;
        private System.Windows.Forms.Label lblGridTol;
        private System.Windows.Forms.NumericUpDown numGridTol;
        private System.Windows.Forms.Label lblGridTolUnit;
        private System.Windows.Forms.Label lblExtTol;
        private System.Windows.Forms.NumericUpDown numExtTol;
        private System.Windows.Forms.Label lblExtTolUnit;
        private System.Windows.Forms.GroupBox grpFindingsGrids;
        private System.Windows.Forms.DataGridView dgvGrids;
        private System.Windows.Forms.Label lblSummaryGrids;

        private System.Windows.Forms.GroupBox grpScopeDucts;
        private System.Windows.Forms.RadioButton rbAllDucts;
        private System.Windows.Forms.RadioButton rbPickedDucts;
        private System.Windows.Forms.RadioButton rbActiveDucts;
        private System.Windows.Forms.CheckedListBox clbLevelsDucts;
        private System.Windows.Forms.Button btnAllLevelsDucts;
        private System.Windows.Forms.Button btnNoLevelsDucts;
        private System.Windows.Forms.GroupBox grpChecksDucts;
        private System.Windows.Forms.CheckedListBox clbDucts;
        private System.Windows.Forms.Button btnCheckDucts;
        private System.Windows.Forms.Button btnFixDucts;
        private System.Windows.Forms.Label lblDuctsInfo;
        private System.Windows.Forms.Label lblDuctReach;
        private System.Windows.Forms.NumericUpDown numDuctReach;
        private System.Windows.Forms.Label lblDuctReachUnit;
        private System.Windows.Forms.Label lblDuctJoin;
        private System.Windows.Forms.NumericUpDown numDuctJoin;
        private System.Windows.Forms.Label lblDuctJoinUnit;
        private System.Windows.Forms.CheckBox chkDuctFallBack;
        private System.Windows.Forms.GroupBox grpFindingsDucts;
        private System.Windows.Forms.DataGridView dgvDucts;
        private System.Windows.Forms.Label lblSummaryDucts;

        private System.Windows.Forms.GroupBox grpScopeFcu;
        private System.Windows.Forms.RadioButton rbAllFcu;
        private System.Windows.Forms.RadioButton rbPickedFcu;
        private System.Windows.Forms.RadioButton rbActiveFcu;
        private System.Windows.Forms.CheckedListBox clbLevelsFcu;
        private System.Windows.Forms.Button btnAllLevelsFcu;
        private System.Windows.Forms.Button btnNoLevelsFcu;
        private System.Windows.Forms.GroupBox grpChecksFcu;
        private System.Windows.Forms.CheckedListBox clbFcu;
        private System.Windows.Forms.Button btnCheckFcu;
        private System.Windows.Forms.Button btnFixFcu;
        private System.Windows.Forms.Label lblFcuInfo;
        private System.Windows.Forms.Label lblFcuFile;
        private System.Windows.Forms.TextBox txtFcuFile;
        private System.Windows.Forms.Button btnFcuBrowse;
        private System.Windows.Forms.Label lblFcuSheet;
        private System.Windows.Forms.ComboBox cmbFcuSheet;
        private System.Windows.Forms.Label lblFcuLoaded;
        private System.Windows.Forms.Label lblFcuParam;
        private System.Windows.Forms.ComboBox cmbFcuParam;
        private System.Windows.Forms.Label lblFcuTol;
        private System.Windows.Forms.NumericUpDown numFcuPct;
        private System.Windows.Forms.Label lblFcuPctUnit;
        private System.Windows.Forms.Label lblFcuOr;
        private System.Windows.Forms.NumericUpDown numFcuAbs;
        private System.Windows.Forms.Label lblFcuAbsUnit;
        private System.Windows.Forms.Label lblFcuWhich;
        private System.Windows.Forms.GroupBox grpFindingsFcu;
        private System.Windows.Forms.DataGridView dgvFcu;
        private System.Windows.Forms.Label lblSummaryFcu;

        private System.Windows.Forms.GroupBox grpScopeChw;
        private System.Windows.Forms.RadioButton rbAllChw;
        private System.Windows.Forms.RadioButton rbPickedChw;
        private System.Windows.Forms.RadioButton rbActiveChw;
        private System.Windows.Forms.CheckedListBox clbLevelsChw;
        private System.Windows.Forms.Button btnAllLevelsChw;
        private System.Windows.Forms.Button btnNoLevelsChw;
        private System.Windows.Forms.GroupBox grpChecksChw;
        private System.Windows.Forms.CheckedListBox clbChw;
        private System.Windows.Forms.Button btnCheckChw;
        private System.Windows.Forms.Button btnFixChw;
        private System.Windows.Forms.Button btnChwFlow;
        private System.Windows.Forms.CheckBox chkChwKeepShort;
        private System.Windows.Forms.Label lblChwTol;
        private System.Windows.Forms.NumericUpDown numChwSteps;
        private System.Windows.Forms.Label lblChwStepsUnit;
        private System.Windows.Forms.NumericUpDown numChwFrom;
        private System.Windows.Forms.Label lblChwFromUnit;
        private System.Windows.Forms.Label lblChwInfo;
        private System.Windows.Forms.Label lblChwLoaded;
        private System.Windows.Forms.Button btnChwExport;
        private System.Windows.Forms.Button btnChwImport;
        private System.Windows.Forms.Button btnChwBuiltin;
        private System.Windows.Forms.Label lblChwSupply;
        private System.Windows.Forms.CheckedListBox clbChwSupply;
        private System.Windows.Forms.Label lblChwReturn;
        private System.Windows.Forms.CheckedListBox clbChwReturn;
        private System.Windows.Forms.GroupBox grpFindingsChw;
        private System.Windows.Forms.DataGridView dgvChw;
        private System.Windows.Forms.Label lblSummaryChw;

        private System.Windows.Forms.CheckBox chkMarkers;
        private System.Windows.Forms.Label lblMarkerInfo;
        private System.Windows.Forms.Button btnMark;
        private System.Windows.Forms.Button btnClearMarks;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.Button btnClose;
    }
}
