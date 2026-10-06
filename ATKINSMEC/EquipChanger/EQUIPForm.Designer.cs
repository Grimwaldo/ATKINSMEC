namespace ATKINSMEC
{
    partial class EQUIPForm
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
            this.grpWhat = new System.Windows.Forms.GroupBox();
            this.dgvSources = new System.Windows.Forms.DataGridView();
            this.grpType = new System.Windows.Forms.GroupBox();
            this.lblType = new System.Windows.Forms.Label();
            this.cboType = new System.Windows.Forms.ComboBox();
            this.chkAnyFamily = new System.Windows.Forms.CheckBox();
            this.chkResize = new System.Windows.Forms.CheckBox();
            this.lblTypeInfo = new System.Windows.Forms.Label();
            this.btnRead = new System.Windows.Forms.Button();
            this.btnPreview = new System.Windows.Forms.Button();
            this.btnApply = new System.Windows.Forms.Button();
            this.lblProgress = new System.Windows.Forms.Label();
            this.grpResults = new System.Windows.Forms.GroupBox();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.lblSummary = new System.Windows.Forms.Label();
            this.btnHelp = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.grpWhat.SuspendLayout();
            this.grpType.SuspendLayout();
            this.grpResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSources)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).BeginInit();
            this.SuspendLayout();
            //
            // grpWhat
            //
            this.grpWhat.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpWhat.Controls.Add(this.dgvSources);
            this.grpWhat.Location = new System.Drawing.Point(10, 10);
            this.grpWhat.Name = "grpWhat";
            this.grpWhat.Size = new System.Drawing.Size(878, 184);
            this.grpWhat.TabIndex = 0;
            this.grpWhat.TabStop = false;
            this.grpWhat.Text = "1. What is selected";
            //
            // dgvSources
            //
            this.dgvSources.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvSources.AllowUserToAddRows = false;
            this.dgvSources.AllowUserToDeleteRows = false;
            this.dgvSources.AllowUserToResizeRows = false;
            this.dgvSources.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSources.Location = new System.Drawing.Point(12, 24);
            this.dgvSources.MultiSelect = false;
            this.dgvSources.Name = "dgvSources";
            this.dgvSources.ReadOnly = true;
            this.dgvSources.RowHeadersVisible = false;
            this.dgvSources.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvSources.Size = new System.Drawing.Size(854, 148);
            this.dgvSources.TabIndex = 0;
            this.dgvSources.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvSources_CellDoubleClick);
            //
            // grpType
            //
            this.grpType.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpType.Controls.Add(this.lblType);
            this.grpType.Controls.Add(this.cboType);
            this.grpType.Controls.Add(this.chkAnyFamily);
            this.grpType.Controls.Add(this.chkResize);
            this.grpType.Controls.Add(this.lblTypeInfo);
            this.grpType.Location = new System.Drawing.Point(10, 202);
            this.grpType.Name = "grpType";
            this.grpType.Size = new System.Drawing.Size(878, 112);
            this.grpType.TabIndex = 1;
            this.grpType.TabStop = false;
            this.grpType.Text = "2. The type to change to";
            //
            // lblType
            //
            this.lblType.AutoSize = false;
            this.lblType.Location = new System.Drawing.Point(12, 28);
            this.lblType.Name = "lblType";
            this.lblType.Size = new System.Drawing.Size(80, 20);
            this.lblType.TabIndex = 0;
            this.lblType.Text = "Change to";
            //
            // cboType
            //
            this.cboType.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.cboType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboType.FormattingEnabled = true;
            this.cboType.Location = new System.Drawing.Point(96, 25);
            this.cboType.Name = "cboType";
            this.cboType.Size = new System.Drawing.Size(500, 23);
            this.cboType.TabIndex = 1;
            //
            // chkAnyFamily
            //
            this.chkAnyFamily.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkAnyFamily.Location = new System.Drawing.Point(612, 26);
            this.chkAnyFamily.Name = "chkAnyFamily";
            this.chkAnyFamily.Size = new System.Drawing.Size(254, 20);
            this.chkAnyFamily.TabIndex = 2;
            this.chkAnyFamily.Text = "Offer every family in the category";
            this.chkAnyFamily.UseVisualStyleBackColor = true;
            this.chkAnyFamily.CheckedChanged += new System.EventHandler(this.chkAnyFamily_CheckedChanged);
            //
            // chkResize
            //
            this.chkResize.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.chkResize.Location = new System.Drawing.Point(96, 54);
            this.chkResize.Name = "chkResize";
            this.chkResize.Size = new System.Drawing.Size(770, 20);
            this.chkResize.TabIndex = 3;
            this.chkResize.Text = "Resize the ductwork on it to match the new connectors";
            this.chkResize.UseVisualStyleBackColor = true;
            //
            // lblTypeInfo
            //
            this.lblTypeInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTypeInfo.AutoSize = false;
            this.lblTypeInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblTypeInfo.Location = new System.Drawing.Point(96, 80);
            this.lblTypeInfo.Name = "lblTypeInfo";
            this.lblTypeInfo.Size = new System.Drawing.Size(770, 20);
            this.lblTypeInfo.TabIndex = 4;
            this.lblTypeInfo.Text = "";
            //
            // btnRead
            //
            this.btnRead.Location = new System.Drawing.Point(10, 324);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(230, 32);
            this.btnRead.TabIndex = 2;
            this.btnRead.Text = "Read the selection";
            this.btnRead.UseVisualStyleBackColor = true;
            this.btnRead.Click += new System.EventHandler(this.btnRead_Click);
            //
            // btnPreview
            //
            this.btnPreview.Location = new System.Drawing.Point(250, 324);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(230, 32);
            this.btnPreview.TabIndex = 2;
            this.btnPreview.Text = "Look - what would follow?";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            //
            // btnApply
            //
            this.btnApply.Location = new System.Drawing.Point(490, 324);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(230, 32);
            this.btnApply.TabIndex = 3;
            this.btnApply.Text = "Change the type...";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            //
            // lblProgress
            //
            this.lblProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblProgress.AutoSize = false;
            this.lblProgress.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblProgress.Location = new System.Drawing.Point(10, 362);
            this.lblProgress.Name = "lblProgress";
            this.lblProgress.Size = new System.Drawing.Size(878, 20);
            this.lblProgress.TabIndex = 4;
            this.lblProgress.Text = "";
            //
            // grpResults
            //
            this.grpResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpResults.Controls.Add(this.dgvResults);
            this.grpResults.Controls.Add(this.lblSummary);
            this.grpResults.Location = new System.Drawing.Point(10, 388);
            this.grpResults.Name = "grpResults";
            this.grpResults.Size = new System.Drawing.Size(878, 200);
            this.grpResults.TabIndex = 5;
            this.grpResults.TabStop = false;
            this.grpResults.Text = "3. What happened";
            //
            // dgvResults
            //
            this.dgvResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvResults.AllowUserToAddRows = false;
            this.dgvResults.AllowUserToDeleteRows = false;
            this.dgvResults.AllowUserToResizeRows = false;
            this.dgvResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvResults.Location = new System.Drawing.Point(12, 24);
            this.dgvResults.MultiSelect = false;
            this.dgvResults.Name = "dgvResults";
            this.dgvResults.ReadOnly = true;
            this.dgvResults.RowHeadersVisible = false;
            this.dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvResults.Size = new System.Drawing.Size(854, 144);
            this.dgvResults.TabIndex = 0;
            //
            // lblSummary
            //
            this.lblSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummary.AutoSize = false;
            this.lblSummary.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblSummary.Location = new System.Drawing.Point(12, 174);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(854, 20);
            this.lblSummary.TabIndex = 1;
            this.lblSummary.Text = "";
            //
            // btnHelp
            //
            this.btnHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnHelp.Location = new System.Drawing.Point(10, 598);
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(160, 30);
            this.btnHelp.TabIndex = 6;
            this.btnHelp.Text = "How to use this";
            this.btnHelp.UseVisualStyleBackColor = true;
            this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(758, 598);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(130, 30);
            this.btnClose.TabIndex = 7;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // EQUIPForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(898, 640);
            this.Controls.Add(this.grpWhat);
            this.Controls.Add(this.grpType);
            this.Controls.Add(this.btnRead);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.lblProgress);
            this.Controls.Add(this.grpResults);
            this.Controls.Add(this.btnHelp);
            this.Controls.Add(this.btnClose);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "EQUIPForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "EQUIPMENT CHANGER";
            this.Load += new System.EventHandler(this.EQUIPForm_Load);
            this.grpWhat.ResumeLayout(false);
            this.grpType.ResumeLayout(false);
            this.grpResults.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSources)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.GroupBox grpWhat;
        private System.Windows.Forms.DataGridView dgvSources;
        private System.Windows.Forms.GroupBox grpType;
        private System.Windows.Forms.Label lblType;
        private System.Windows.Forms.ComboBox cboType;
        private System.Windows.Forms.CheckBox chkAnyFamily;
        private System.Windows.Forms.CheckBox chkResize;
        private System.Windows.Forms.Label lblTypeInfo;
        private System.Windows.Forms.Button btnRead;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Label lblProgress;
        private System.Windows.Forms.GroupBox grpResults;
        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.Button btnClose;
    }
}
