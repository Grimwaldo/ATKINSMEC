namespace ATKINSMEC
{
    partial class NWCMODELForm
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
            this.lblModel = new System.Windows.Forms.Label();
            this.chkInherit = new System.Windows.Forms.CheckBox();
            this.grpOwn = new System.Windows.Forms.GroupBox();
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
            this.lblPrefix = new System.Windows.Forms.Label();
            this.txtPrefix = new System.Windows.Forms.TextBox();
            this.lblSuffix = new System.Windows.Forms.Label();
            this.txtSuffix = new System.Windows.Forms.TextBox();
            this.lblNote = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpOwn.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFacet)).BeginInit();
            this.SuspendLayout();
            //
            // lblModel
            //
            this.lblModel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblModel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblModel.Location = new System.Drawing.Point(12, 12);
            this.lblModel.Name = "lblModel";
            this.lblModel.Size = new System.Drawing.Size(640, 20);
            this.lblModel.TabIndex = 0;
            this.lblModel.Text = "";
            //
            // chkInherit
            //
            this.chkInherit.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.chkInherit.Location = new System.Drawing.Point(12, 38);
            this.chkInherit.Name = "chkInherit";
            this.chkInherit.Size = new System.Drawing.Size(640, 20);
            this.chkInherit.TabIndex = 1;
            this.chkInherit.Text = "Use the settings on the main window (untick to give this model its own)";
            this.chkInherit.UseVisualStyleBackColor = true;
            this.chkInherit.CheckedChanged += new System.EventHandler(this.chkInherit_CheckedChanged);
            //
            // grpOwn
            //
            this.grpOwn.Controls.Add(this.chkShared);
            this.grpOwn.Controls.Add(this.chkLinks);
            this.grpOwn.Controls.Add(this.chkLevels);
            this.grpOwn.Controls.Add(this.chkIds);
            this.grpOwn.Controls.Add(this.chkRooms);
            this.grpOwn.Controls.Add(this.chkParts);
            this.grpOwn.Controls.Add(this.chkProperties);
            this.grpOwn.Controls.Add(this.chkSkip);
            this.grpOwn.Controls.Add(this.lblParams);
            this.grpOwn.Controls.Add(this.cboParams);
            this.grpOwn.Controls.Add(this.lblWorksets);
            this.grpOwn.Controls.Add(this.cboWorksets);
            this.grpOwn.Controls.Add(this.lblFacet);
            this.grpOwn.Controls.Add(this.numFacet);
            this.grpOwn.Controls.Add(this.lblPrefix);
            this.grpOwn.Controls.Add(this.txtPrefix);
            this.grpOwn.Controls.Add(this.lblSuffix);
            this.grpOwn.Controls.Add(this.txtSuffix);
            this.grpOwn.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpOwn.Location = new System.Drawing.Point(12, 64);
            this.grpOwn.Name = "grpOwn";
            this.grpOwn.Size = new System.Drawing.Size(640, 176);
            this.grpOwn.TabIndex = 2;
            this.grpOwn.TabStop = false;
            this.grpOwn.Text = "This model only";
            //
            // chkShared
            //
            this.chkShared.Location = new System.Drawing.Point(12, 24);
            this.chkShared.Name = "chkShared";
            this.chkShared.Size = new System.Drawing.Size(200, 20);
            this.chkShared.TabIndex = 0;
            this.chkShared.Text = "Shared coordinates";
            this.chkShared.UseVisualStyleBackColor = true;
            //
            // chkLinks
            //
            this.chkLinks.Location = new System.Drawing.Point(12, 50);
            this.chkLinks.Name = "chkLinks";
            this.chkLinks.Size = new System.Drawing.Size(200, 20);
            this.chkLinks.TabIndex = 1;
            this.chkLinks.Text = "Include linked models";
            this.chkLinks.UseVisualStyleBackColor = true;
            //
            // chkLevels
            //
            this.chkLevels.Location = new System.Drawing.Point(12, 76);
            this.chkLevels.Name = "chkLevels";
            this.chkLevels.Size = new System.Drawing.Size(200, 20);
            this.chkLevels.TabIndex = 2;
            this.chkLevels.Text = "Divide the file into levels";
            this.chkLevels.UseVisualStyleBackColor = true;
            //
            // chkIds
            //
            this.chkIds.Location = new System.Drawing.Point(222, 24);
            this.chkIds.Name = "chkIds";
            this.chkIds.Size = new System.Drawing.Size(200, 20);
            this.chkIds.TabIndex = 3;
            this.chkIds.Text = "Element ids";
            this.chkIds.UseVisualStyleBackColor = true;
            //
            // chkRooms
            //
            this.chkRooms.Location = new System.Drawing.Point(222, 50);
            this.chkRooms.Name = "chkRooms";
            this.chkRooms.Size = new System.Drawing.Size(200, 20);
            this.chkRooms.TabIndex = 4;
            this.chkRooms.Text = "Rooms as attributes";
            this.chkRooms.UseVisualStyleBackColor = true;
            //
            // chkParts
            //
            this.chkParts.Location = new System.Drawing.Point(222, 76);
            this.chkParts.Name = "chkParts";
            this.chkParts.Size = new System.Drawing.Size(200, 20);
            this.chkParts.TabIndex = 5;
            this.chkParts.Text = "Parts instead of originals";
            this.chkParts.UseVisualStyleBackColor = true;
            //
            // chkProperties
            //
            this.chkProperties.Location = new System.Drawing.Point(430, 24);
            this.chkProperties.Name = "chkProperties";
            this.chkProperties.Size = new System.Drawing.Size(200, 20);
            this.chkProperties.TabIndex = 6;
            this.chkProperties.Text = "Convert element properties";
            this.chkProperties.UseVisualStyleBackColor = true;
            //
            // chkSkip
            //
            this.chkSkip.Location = new System.Drawing.Point(430, 50);
            this.chkSkip.Name = "chkSkip";
            this.chkSkip.Size = new System.Drawing.Size(200, 20);
            this.chkSkip.TabIndex = 7;
            this.chkSkip.Text = "Skip if the file is newer";
            this.chkSkip.UseVisualStyleBackColor = true;
            //
            // lblParams
            //
            this.lblParams.Location = new System.Drawing.Point(12, 108);
            this.lblParams.Name = "lblParams";
            this.lblParams.Size = new System.Drawing.Size(80, 20);
            this.lblParams.TabIndex = 8;
            this.lblParams.Text = "Parameters";
            //
            // cboParams
            //
            this.cboParams.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboParams.FormattingEnabled = true;
            this.cboParams.Location = new System.Drawing.Point(96, 105);
            this.cboParams.Name = "cboParams";
            this.cboParams.Size = new System.Drawing.Size(130, 23);
            this.cboParams.TabIndex = 9;
            //
            // lblWorksets
            //
            this.lblWorksets.Location = new System.Drawing.Point(240, 108);
            this.lblWorksets.Name = "lblWorksets";
            this.lblWorksets.Size = new System.Drawing.Size(70, 20);
            this.lblWorksets.TabIndex = 10;
            this.lblWorksets.Text = "Worksets";
            this.lblWorksets.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // cboWorksets
            //
            this.cboWorksets.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboWorksets.FormattingEnabled = true;
            this.cboWorksets.Location = new System.Drawing.Point(316, 105);
            this.cboWorksets.Name = "cboWorksets";
            this.cboWorksets.Size = new System.Drawing.Size(150, 23);
            this.cboWorksets.TabIndex = 11;
            //
            // lblFacet
            //
            this.lblFacet.Location = new System.Drawing.Point(480, 108);
            this.lblFacet.Name = "lblFacet";
            this.lblFacet.Size = new System.Drawing.Size(60, 20);
            this.lblFacet.TabIndex = 12;
            this.lblFacet.Text = "Faceting";
            this.lblFacet.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // numFacet
            //
            this.numFacet.DecimalPlaces = 1;
            this.numFacet.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            this.numFacet.Location = new System.Drawing.Point(548, 105);
            this.numFacet.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numFacet.Name = "numFacet";
            this.numFacet.Size = new System.Drawing.Size(70, 23);
            this.numFacet.TabIndex = 13;
            this.numFacet.Value = new decimal(new int[] { 1, 0, 0, 0 });
            //
            // lblPrefix
            //
            this.lblPrefix.Location = new System.Drawing.Point(12, 142);
            this.lblPrefix.Name = "lblPrefix";
            this.lblPrefix.Size = new System.Drawing.Size(80, 20);
            this.lblPrefix.TabIndex = 14;
            this.lblPrefix.Text = "Prefix";
            //
            // txtPrefix
            //
            this.txtPrefix.Location = new System.Drawing.Point(96, 139);
            this.txtPrefix.Name = "txtPrefix";
            this.txtPrefix.Size = new System.Drawing.Size(130, 23);
            this.txtPrefix.TabIndex = 15;
            //
            // lblSuffix
            //
            this.lblSuffix.Location = new System.Drawing.Point(240, 142);
            this.lblSuffix.Name = "lblSuffix";
            this.lblSuffix.Size = new System.Drawing.Size(70, 20);
            this.lblSuffix.TabIndex = 16;
            this.lblSuffix.Text = "Suffix";
            this.lblSuffix.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // txtSuffix
            //
            this.txtSuffix.Location = new System.Drawing.Point(316, 139);
            this.txtSuffix.Name = "txtSuffix";
            this.txtSuffix.Size = new System.Drawing.Size(150, 23);
            this.txtSuffix.TabIndex = 17;
            //
            // lblNote
            //
            this.lblNote.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblNote.Location = new System.Drawing.Point(12, 248);
            this.lblNote.Name = "lblNote";
            this.lblNote.Size = new System.Drawing.Size(640, 34);
            this.lblNote.TabIndex = 3;
            this.lblNote.Text = "The 3D view is set in the list, in this model\'s own row. Every file still goes to the one folder on the main window.";
            //
            // btnOk
            //
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(432, 290);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(105, 30);
            this.btnOk.TabIndex = 4;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(547, 290);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(105, 30);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // NWCMODELForm
            //
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(664, 332);
            this.Controls.Add(this.lblModel);
            this.Controls.Add(this.chkInherit);
            this.Controls.Add(this.grpOwn);
            this.Controls.Add(this.lblNote);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "NWCMODELForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Settings for one model";
            this.Load += new System.EventHandler(this.NWCMODELForm_Load);
            this.grpOwn.ResumeLayout(false);
            this.grpOwn.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFacet)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.CheckBox chkInherit;
        private System.Windows.Forms.GroupBox grpOwn;
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
        private System.Windows.Forms.Label lblPrefix;
        private System.Windows.Forms.TextBox txtPrefix;
        private System.Windows.Forms.Label lblSuffix;
        private System.Windows.Forms.TextBox txtSuffix;
        private System.Windows.Forms.Label lblNote;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
