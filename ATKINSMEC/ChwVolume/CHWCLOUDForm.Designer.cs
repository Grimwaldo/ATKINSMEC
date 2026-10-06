
namespace ATKINSMEC
{
    partial class CHWCLOUDForm
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
            this.lblHelp = new System.Windows.Forms.Label();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.lblRegion = new System.Windows.Forms.Label();
            this.cmbRegion = new System.Windows.Forms.ComboBox();
            this.lblProject = new System.Windows.Forms.Label();
            this.txtProject = new System.Windows.Forms.TextBox();
            this.lblModel = new System.Windows.Forms.Label();
            this.txtModel = new System.Windows.Forms.TextBox();
            this.btnFromCurrent = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblHelp
            //
            this.lblHelp.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHelp.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblHelp.Location = new System.Drawing.Point(12, 10);
            this.lblHelp.Name = "lblHelp";
            this.lblHelp.Size = new System.Drawing.Size(540, 44);
            this.lblHelp.TabIndex = 0;
            this.lblHelp.Text = "Revit has no ACC browser, so a cloud model is named by its region and two GUIDs. O" +
                "pen the model once and press the button below to read them, or paste them from th" +
                "e ACC url.";
            //
            // lblName
            //
            this.lblName.Location = new System.Drawing.Point(12, 66);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(90, 18);
            this.lblName.TabIndex = 1;
            this.lblName.Text = "Name:";
            //
            // txtName
            //
            this.txtName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtName.Location = new System.Drawing.Point(108, 63);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(444, 20);
            this.txtName.TabIndex = 2;
            //
            // lblRegion
            //
            this.lblRegion.Location = new System.Drawing.Point(12, 94);
            this.lblRegion.Name = "lblRegion";
            this.lblRegion.Size = new System.Drawing.Size(90, 18);
            this.lblRegion.TabIndex = 3;
            this.lblRegion.Text = "Region:";
            //
            // cmbRegion
            //
            this.cmbRegion.FormattingEnabled = true;
            this.cmbRegion.Location = new System.Drawing.Point(108, 91);
            this.cmbRegion.Name = "cmbRegion";
            this.cmbRegion.Size = new System.Drawing.Size(160, 21);
            this.cmbRegion.TabIndex = 4;
            //
            // lblProject
            //
            this.lblProject.Location = new System.Drawing.Point(12, 122);
            this.lblProject.Name = "lblProject";
            this.lblProject.Size = new System.Drawing.Size(90, 18);
            this.lblProject.TabIndex = 5;
            this.lblProject.Text = "Project GUID:";
            //
            // txtProject
            //
            this.txtProject.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtProject.Location = new System.Drawing.Point(108, 119);
            this.txtProject.Name = "txtProject";
            this.txtProject.Size = new System.Drawing.Size(444, 20);
            this.txtProject.TabIndex = 6;
            //
            // lblModel
            //
            this.lblModel.Location = new System.Drawing.Point(12, 150);
            this.lblModel.Name = "lblModel";
            this.lblModel.Size = new System.Drawing.Size(90, 18);
            this.lblModel.TabIndex = 7;
            this.lblModel.Text = "Model GUID:";
            //
            // txtModel
            //
            this.txtModel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtModel.Location = new System.Drawing.Point(108, 147);
            this.txtModel.Name = "txtModel";
            this.txtModel.Size = new System.Drawing.Size(444, 20);
            this.txtModel.TabIndex = 8;
            //
            // btnFromCurrent
            //
            this.btnFromCurrent.Location = new System.Drawing.Point(108, 180);
            this.btnFromCurrent.Name = "btnFromCurrent";
            this.btnFromCurrent.Size = new System.Drawing.Size(240, 28);
            this.btnFromCurrent.TabIndex = 9;
            this.btnFromCurrent.Text = "Read from the model open now";
            this.btnFromCurrent.UseVisualStyleBackColor = true;
            this.btnFromCurrent.Click += new System.EventHandler(this.btnFromCurrent_Click);
            //
            // btnOk
            //
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(352, 224);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(96, 28);
            this.btnOk.TabIndex = 10;
            this.btnOk.Text = "Add";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(456, 224);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(96, 28);
            this.btnCancel.TabIndex = 11;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // CHWCLOUDForm
            //
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(564, 264);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnFromCurrent);
            this.Controls.Add(this.txtModel);
            this.Controls.Add(this.lblModel);
            this.Controls.Add(this.txtProject);
            this.Controls.Add(this.lblProject);
            this.Controls.Add(this.cmbRegion);
            this.Controls.Add(this.lblRegion);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.lblName);
            this.Controls.Add(this.lblHelp);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "CHWCLOUDForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ACC model";
            this.Load += new System.EventHandler(this.CHWCLOUDForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblHelp;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Label lblRegion;
        private System.Windows.Forms.ComboBox cmbRegion;
        private System.Windows.Forms.Label lblProject;
        private System.Windows.Forms.TextBox txtProject;
        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.TextBox txtModel;
        private System.Windows.Forms.Button btnFromCurrent;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
