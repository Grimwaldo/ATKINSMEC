
namespace ATKINSMEC
{
    partial class ALIGNForm
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
            this.grpLinks = new System.Windows.Forms.GroupBox();
            this.clbLinks = new System.Windows.Forms.CheckedListBox();
            this.btnAllLinks = new System.Windows.Forms.Button();
            this.btnNoLinks = new System.Windows.Forms.Button();
            this.lblLinksInfo = new System.Windows.Forms.Label();
            this.grpScope = new System.Windows.Forms.GroupBox();
            this.rbAll = new System.Windows.Forms.RadioButton();
            this.rbSelected = new System.Windows.Forms.RadioButton();
            this.btnPick = new System.Windows.Forms.Button();
            this.grpHow = new System.Windows.Forms.GroupBox();
            this.lblDistance = new System.Windows.Forms.Label();
            this.numDistance = new System.Windows.Forms.NumericUpDown();
            this.lblDistanceUnit = new System.Windows.Forms.Label();
            this.chkElevation = new System.Windows.Forms.CheckBox();
            this.lblRule = new System.Windows.Forms.Label();
            this.cmbRule = new System.Windows.Forms.ComboBox();
            this.btnFind = new System.Windows.Forms.Button();
            this.grpMatches = new System.Windows.Forms.GroupBox();
            this.dgvMatches = new System.Windows.Forms.DataGridView();
            this.colCheck = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colShow = new System.Windows.Forms.DataGridViewButtonColumn();
            this.colTerminal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTarget = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMove = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRise = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnTickAll = new System.Windows.Forms.Button();
            this.btnUntickAll = new System.Windows.Forms.Button();
            this.lblSummary = new System.Windows.Forms.Label();
            this.btnAlign = new System.Windows.Forms.Button();
            this.btnHelp = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.grpLinks.SuspendLayout();
            this.grpScope.SuspendLayout();
            this.grpHow.SuspendLayout();
            this.grpMatches.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numDistance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMatches)).BeginInit();
            this.SuspendLayout();
            //
            // grpLinks
            //
            this.grpLinks.Controls.Add(this.clbLinks);
            this.grpLinks.Controls.Add(this.btnAllLinks);
            this.grpLinks.Controls.Add(this.btnNoLinks);
            this.grpLinks.Controls.Add(this.lblLinksInfo);
            this.grpLinks.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpLinks.Location = new System.Drawing.Point(12, 12);
            this.grpLinks.Name = "grpLinks";
            this.grpLinks.Size = new System.Drawing.Size(876, 128);
            this.grpLinks.TabIndex = 0;
            this.grpLinks.TabStop = false;
            this.grpLinks.Text = "1.  Linked models holding the air terminals to align to";
            //
            // clbLinks
            //
            this.clbLinks.CheckOnClick = true;
            this.clbLinks.FormattingEnabled = true;
            this.clbLinks.IntegralHeight = false;
            this.clbLinks.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbLinks.Location = new System.Drawing.Point(12, 24);
            this.clbLinks.Name = "clbLinks";
            this.clbLinks.Size = new System.Drawing.Size(700, 64);
            this.clbLinks.TabIndex = 0;
            //
            // btnAllLinks
            //
            this.btnAllLinks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAllLinks.Location = new System.Drawing.Point(724, 24);
            this.btnAllLinks.Name = "btnAllLinks";
            this.btnAllLinks.Size = new System.Drawing.Size(140, 26);
            this.btnAllLinks.TabIndex = 1;
            this.btnAllLinks.Text = "Tick all";
            this.btnAllLinks.UseVisualStyleBackColor = true;
            this.btnAllLinks.Click += new System.EventHandler(this.btnAllLinks_Click);
            //
            // btnNoLinks
            //
            this.btnNoLinks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoLinks.Location = new System.Drawing.Point(724, 56);
            this.btnNoLinks.Name = "btnNoLinks";
            this.btnNoLinks.Size = new System.Drawing.Size(140, 26);
            this.btnNoLinks.TabIndex = 2;
            this.btnNoLinks.Text = "Untick all";
            this.btnNoLinks.UseVisualStyleBackColor = true;
            this.btnNoLinks.Click += new System.EventHandler(this.btnNoLinks_Click);
            //
            // lblLinksInfo
            //
            this.lblLinksInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblLinksInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblLinksInfo.Location = new System.Drawing.Point(12, 94);
            this.lblLinksInfo.Name = "lblLinksInfo";
            this.lblLinksInfo.Size = new System.Drawing.Size(852, 28);
            this.lblLinksInfo.TabIndex = 3;
            this.lblLinksInfo.Text = "";
            //
            // grpScope
            //
            this.grpScope.Controls.Add(this.rbAll);
            this.grpScope.Controls.Add(this.rbSelected);
            this.grpScope.Controls.Add(this.btnPick);
            this.grpScope.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpScope.Location = new System.Drawing.Point(12, 148);
            this.grpScope.Name = "grpScope";
            this.grpScope.Size = new System.Drawing.Size(876, 72);
            this.grpScope.TabIndex = 1;
            this.grpScope.TabStop = false;
            this.grpScope.Text = "2.  Which air terminals to move";
            //
            // rbAll
            //
            this.rbAll.Checked = true;
            this.rbAll.Location = new System.Drawing.Point(12, 22);
            this.rbAll.Name = "rbAll";
            this.rbAll.Size = new System.Drawing.Size(560, 20);
            this.rbAll.TabIndex = 0;
            this.rbAll.TabStop = true;
            this.rbAll.Text = "Every air terminal in this model";
            this.rbAll.UseVisualStyleBackColor = true;
            this.rbAll.CheckedChanged += new System.EventHandler(this.Scope_CheckedChanged);
            //
            // rbSelected
            //
            this.rbSelected.Location = new System.Drawing.Point(12, 44);
            this.rbSelected.Name = "rbSelected";
            this.rbSelected.Size = new System.Drawing.Size(560, 20);
            this.rbSelected.TabIndex = 1;
            this.rbSelected.Text = "Only the ones picked in the model";
            this.rbSelected.UseVisualStyleBackColor = true;
            this.rbSelected.CheckedChanged += new System.EventHandler(this.Scope_CheckedChanged);
            //
            // btnPick
            //
            this.btnPick.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPick.Location = new System.Drawing.Point(600, 30);
            this.btnPick.Name = "btnPick";
            this.btnPick.Size = new System.Drawing.Size(264, 30);
            this.btnPick.TabIndex = 2;
            this.btnPick.Text = "Pick air terminals in the model...";
            this.btnPick.UseVisualStyleBackColor = true;
            this.btnPick.Click += new System.EventHandler(this.btnPick_Click);
            //
            // grpHow
            //
            this.grpHow.Controls.Add(this.lblDistance);
            this.grpHow.Controls.Add(this.numDistance);
            this.grpHow.Controls.Add(this.lblDistanceUnit);
            this.grpHow.Controls.Add(this.chkElevation);
            this.grpHow.Controls.Add(this.lblRule);
            this.grpHow.Controls.Add(this.cmbRule);
            this.grpHow.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpHow.Location = new System.Drawing.Point(12, 230);
            this.grpHow.Name = "grpHow";
            this.grpHow.Size = new System.Drawing.Size(876, 80);
            this.grpHow.TabIndex = 1;
            this.grpHow.TabStop = false;
            this.grpHow.Text = "3.  How to match them";
            //
            // lblDistance
            //
            this.lblDistance.Location = new System.Drawing.Point(12, 30);
            this.lblDistance.Name = "lblDistance";
            this.lblDistance.Size = new System.Drawing.Size(160, 18);
            this.lblDistance.TabIndex = 0;
            this.lblDistance.Text = "Look no further away than";
            //
            // numDistance
            //
            this.numDistance.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            this.numDistance.Location = new System.Drawing.Point(176, 27);
            this.numDistance.Maximum = new decimal(new int[] { 20000, 0, 0, 0 });
            this.numDistance.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numDistance.Name = "numDistance";
            this.numDistance.Size = new System.Drawing.Size(80, 20);
            this.numDistance.TabIndex = 1;
            this.numDistance.Value = new decimal(new int[] { 1500, 0, 0, 0 });
            //
            // lblDistanceUnit
            //
            this.lblDistanceUnit.Location = new System.Drawing.Point(262, 30);
            this.lblDistanceUnit.Name = "lblDistanceUnit";
            this.lblDistanceUnit.Size = new System.Drawing.Size(120, 18);
            this.lblDistanceUnit.TabIndex = 2;
            this.lblDistanceUnit.Text = "mm, on plan";
            //
            // chkElevation
            //
            this.chkElevation.Checked = true;
            this.chkElevation.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkElevation.Location = new System.Drawing.Point(12, 52);
            this.chkElevation.Name = "chkElevation";
            this.chkElevation.Size = new System.Drawing.Size(400, 20);
            this.chkElevation.TabIndex = 3;
            this.chkElevation.Text = "Take the elevation from the linked terminal as well";
            this.chkElevation.UseVisualStyleBackColor = true;
            //
            // lblRule
            //
            this.lblRule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblRule.Location = new System.Drawing.Point(430, 30);
            this.lblRule.Name = "lblRule";
            this.lblRule.Size = new System.Drawing.Size(100, 18);
            this.lblRule.TabIndex = 4;
            this.lblRule.Text = "Match against";
            //
            // cmbRule
            //
            this.cmbRule.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRule.FormattingEnabled = true;
            this.cmbRule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbRule.Location = new System.Drawing.Point(534, 27);
            this.cmbRule.Name = "cmbRule";
            this.cmbRule.Size = new System.Drawing.Size(330, 21);
            this.cmbRule.TabIndex = 5;
            //
            // btnFind
            //
            this.btnFind.Location = new System.Drawing.Point(12, 320);
            this.btnFind.Name = "btnFind";
            this.btnFind.Size = new System.Drawing.Size(200, 32);
            this.btnFind.TabIndex = 2;
            this.btnFind.Text = "4.  Find matches";
            this.btnFind.UseVisualStyleBackColor = true;
            this.btnFind.Click += new System.EventHandler(this.btnFind_Click);
            //
            // grpMatches
            //
            this.grpMatches.Controls.Add(this.dgvMatches);
            this.grpMatches.Controls.Add(this.btnTickAll);
            this.grpMatches.Controls.Add(this.btnUntickAll);
            this.grpMatches.Controls.Add(this.lblSummary);
            this.grpMatches.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpMatches.Location = new System.Drawing.Point(12, 362);
            this.grpMatches.Name = "grpMatches";
            this.grpMatches.Size = new System.Drawing.Size(876, 312);
            this.grpMatches.TabIndex = 3;
            this.grpMatches.TabStop = false;
            this.grpMatches.Text = "5.  What will move  (nothing is changed until you press Align)";
            //
            // dgvMatches
            //
            this.dgvMatches.AllowUserToAddRows = false;
            this.dgvMatches.AllowUserToDeleteRows = false;
            this.dgvMatches.AllowUserToResizeRows = false;
            this.dgvMatches.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvMatches.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvMatches.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colCheck,
            this.colShow,
            this.colTerminal,
            this.colTarget,
            this.colMove,
            this.colRise,
            this.colNote});
            this.dgvMatches.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvMatches.Location = new System.Drawing.Point(12, 24);
            this.dgvMatches.MultiSelect = true;
            this.dgvMatches.Name = "dgvMatches";
            this.dgvMatches.RowHeadersVisible = false;
            this.dgvMatches.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvMatches.Size = new System.Drawing.Size(700, 246);
            this.dgvMatches.TabIndex = 0;
            //
            // colCheck
            //
            this.colCheck.HeaderText = "";
            this.colCheck.Name = "colCheck";
            this.colCheck.Width = 30;
            //
            // colShow
            //
            this.colShow.HeaderText = "";
            this.colShow.Name = "colShow";
            this.colShow.Text = "Show";
            this.colShow.UseColumnTextForButtonValue = true;
            this.colShow.Width = 55;
            //
            // colTerminal
            //
            this.colTerminal.HeaderText = "Air terminal in this model";
            this.colTerminal.Name = "colTerminal";
            this.colTerminal.ReadOnly = true;
            this.colTerminal.Width = 175;
            //
            // colTarget
            //
            this.colTarget.HeaderText = "Aligning to";
            this.colTarget.Name = "colTarget";
            this.colTarget.ReadOnly = true;
            this.colTarget.Width = 155;
            //
            // colMove
            //
            this.colMove.HeaderText = "Move (mm)";
            this.colMove.Name = "colMove";
            this.colMove.ReadOnly = true;
            this.colMove.Width = 75;
            //
            // colRise
            //
            this.colRise.HeaderText = "Rise (mm)";
            this.colRise.Name = "colRise";
            this.colRise.ReadOnly = true;
            this.colRise.Width = 70;
            //
            // colNote
            //
            this.colNote.HeaderText = "Note";
            this.colNote.Name = "colNote";
            this.colNote.ReadOnly = true;
            this.colNote.Width = 120;
            //
            // btnTickAll
            //
            this.btnTickAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnTickAll.Location = new System.Drawing.Point(724, 24);
            this.btnTickAll.Name = "btnTickAll";
            this.btnTickAll.Size = new System.Drawing.Size(140, 26);
            this.btnTickAll.TabIndex = 1;
            this.btnTickAll.Text = "Tick all";
            this.btnTickAll.UseVisualStyleBackColor = true;
            this.btnTickAll.Click += new System.EventHandler(this.btnTickAll_Click);
            //
            // btnUntickAll
            //
            this.btnUntickAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUntickAll.Location = new System.Drawing.Point(724, 56);
            this.btnUntickAll.Name = "btnUntickAll";
            this.btnUntickAll.Size = new System.Drawing.Size(140, 26);
            this.btnUntickAll.TabIndex = 2;
            this.btnUntickAll.Text = "Untick all";
            this.btnUntickAll.UseVisualStyleBackColor = true;
            this.btnUntickAll.Click += new System.EventHandler(this.btnUntickAll_Click);
            //
            // lblSummary
            //
            this.lblSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummary.Location = new System.Drawing.Point(12, 276);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(852, 30);
            this.lblSummary.TabIndex = 3;
            this.lblSummary.Text = "";
            //
            // btnAlign
            //
            this.btnAlign.Enabled = false;
            this.btnAlign.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAlign.Location = new System.Drawing.Point(12, 684);
            this.btnAlign.Name = "btnAlign";
            this.btnAlign.Size = new System.Drawing.Size(220, 34);
            this.btnAlign.TabIndex = 4;
            this.btnAlign.Text = "6.  Align the ticked terminals";
            this.btnAlign.UseVisualStyleBackColor = true;
            this.btnAlign.Click += new System.EventHandler(this.btnAlign_Click);
            //
            // btnHelp
            //
            this.btnHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnHelp.Location = new System.Drawing.Point(626, 686);
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(140, 30);
            this.btnHelp.TabIndex = 5;
            this.btnHelp.Text = "How to use this";
            this.btnHelp.UseVisualStyleBackColor = true;
            this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(778, 686);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(110, 30);
            this.btnClose.TabIndex = 6;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // ALIGNForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 730);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnHelp);
            this.Controls.Add(this.btnAlign);
            this.Controls.Add(this.grpMatches);
            this.Controls.Add(this.btnFind);
            this.Controls.Add(this.grpHow);
            this.Controls.Add(this.grpScope);
            this.Controls.Add(this.grpLinks);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "ALIGNForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ALIGN TERMINALS";
            this.Load += new System.EventHandler(this.ALIGNForm_Load);
            this.grpLinks.ResumeLayout(false);
            this.grpScope.ResumeLayout(false);
            this.grpHow.ResumeLayout(false);
            this.grpMatches.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numDistance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMatches)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpLinks;
        private System.Windows.Forms.CheckedListBox clbLinks;
        private System.Windows.Forms.Button btnAllLinks;
        private System.Windows.Forms.Button btnNoLinks;
        private System.Windows.Forms.Label lblLinksInfo;
        private System.Windows.Forms.GroupBox grpScope;
        private System.Windows.Forms.RadioButton rbAll;
        private System.Windows.Forms.RadioButton rbSelected;
        private System.Windows.Forms.Button btnPick;
        private System.Windows.Forms.GroupBox grpHow;
        private System.Windows.Forms.Label lblDistance;
        private System.Windows.Forms.NumericUpDown numDistance;
        private System.Windows.Forms.Label lblDistanceUnit;
        private System.Windows.Forms.CheckBox chkElevation;
        private System.Windows.Forms.Label lblRule;
        private System.Windows.Forms.ComboBox cmbRule;
        private System.Windows.Forms.Button btnFind;
        private System.Windows.Forms.GroupBox grpMatches;
        private System.Windows.Forms.DataGridView dgvMatches;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colCheck;
        private System.Windows.Forms.DataGridViewButtonColumn colShow;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTerminal;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTarget;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMove;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRise;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNote;
        private System.Windows.Forms.Button btnTickAll;
        private System.Windows.Forms.Button btnUntickAll;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.Button btnAlign;
        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.Button btnClose;
    }
}
