
namespace ATKINSMEC
{
    partial class INSULATIONForm
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
            this.chkEnabled = new System.Windows.Forms.CheckBox();
            this.grpRules = new System.Windows.Forms.GroupBox();
            this.dgvRules = new System.Windows.Forms.DataGridView();
            this.colSystemType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colThickness = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colInsulationType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.lblRulesInfo = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.lblPath = new System.Windows.Forms.Label();
            this.grpRules.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRules)).BeginInit();
            this.SuspendLayout();
            //
            // chkEnabled
            //
            this.chkEnabled.Location = new System.Drawing.Point(16, 14);
            this.chkEnabled.Name = "chkEnabled";
            this.chkEnabled.Size = new System.Drawing.Size(420, 24);
            this.chkEnabled.TabIndex = 0;
            this.chkEnabled.Text = "Insulate ducts, fittings and accessories automatically as they are drawn";
            this.chkEnabled.UseVisualStyleBackColor = true;
            //
            // grpRules
            //
            this.grpRules.Controls.Add(this.dgvRules);
            this.grpRules.Controls.Add(this.lblRulesInfo);
            this.grpRules.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpRules.Location = new System.Drawing.Point(12, 44);
            this.grpRules.Name = "grpRules";
            this.grpRules.Size = new System.Drawing.Size(660, 340);
            this.grpRules.TabIndex = 3;
            this.grpRules.TabStop = false;
            this.grpRules.Text = "Thickness and insulation type per duct system type";
            //
            // dgvRules
            //
            this.dgvRules.AllowUserToAddRows = false;
            this.dgvRules.AllowUserToDeleteRows = false;
            this.dgvRules.AllowUserToResizeRows = false;
            this.dgvRules.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRules.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSystemType,
            this.colThickness,
            this.colInsulationType});
            this.dgvRules.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvRules.Location = new System.Drawing.Point(12, 24);
            this.dgvRules.MultiSelect = false;
            this.dgvRules.Name = "dgvRules";
            this.dgvRules.RowHeadersVisible = false;
            this.dgvRules.Size = new System.Drawing.Size(636, 268);
            this.dgvRules.TabIndex = 0;
            //
            // colSystemType
            //
            this.colSystemType.HeaderText = "Duct system type";
            this.colSystemType.Name = "colSystemType";
            this.colSystemType.ReadOnly = true;
            this.colSystemType.Width = 250;
            //
            // colThickness
            //
            this.colThickness.HeaderText = "Thickness (mm)";
            this.colThickness.Name = "colThickness";
            this.colThickness.Width = 110;
            //
            // colInsulationType
            //
            this.colInsulationType.HeaderText = "Insulation type";
            this.colInsulationType.Name = "colInsulationType";
            this.colInsulationType.Width = 250;
            this.colInsulationType.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            //
            // lblRulesInfo
            //
            this.lblRulesInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblRulesInfo.Location = new System.Drawing.Point(12, 300);
            this.lblRulesInfo.Name = "lblRulesInfo";
            this.lblRulesInfo.Size = new System.Drawing.Size(636, 32);
            this.lblRulesInfo.TabIndex = 1;
            this.lblRulesInfo.Text = "Leave a thickness at 0 to draw that system type without insulation.";
            //
            // btnSave
            //
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(422, 396);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(140, 30);
            this.btnSave.TabIndex = 4;
            this.btnSave.Text = "Save rules";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(572, 396);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 30);
            this.btnClose.TabIndex = 5;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // lblPath
            //
            this.lblPath.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblPath.Location = new System.Drawing.Point(12, 392);
            this.lblPath.Name = "lblPath";
            this.lblPath.Size = new System.Drawing.Size(400, 40);
            this.lblPath.TabIndex = 6;
            this.lblPath.Text = "";
            //
            // INSULATIONForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(684, 440);
            this.Controls.Add(this.lblPath);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.grpRules);
            this.Controls.Add(this.chkEnabled);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "INSULATIONForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DUCT INSULATION";
            this.Load += new System.EventHandler(this.INSULATIONForm_Load);
            this.grpRules.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvRules)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox chkEnabled;
        private System.Windows.Forms.GroupBox grpRules;
        private System.Windows.Forms.DataGridView dgvRules;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSystemType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colThickness;
        private System.Windows.Forms.DataGridViewComboBoxColumn colInsulationType;
        private System.Windows.Forms.Label lblRulesInfo;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblPath;
    }
}
