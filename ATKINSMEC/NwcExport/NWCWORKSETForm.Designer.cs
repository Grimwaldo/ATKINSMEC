namespace ATKINSMEC
{
    partial class NWCWORKSETForm
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
            this.lblModel = new System.Windows.Forms.Label();
            this.chkInherit = new System.Windows.Forms.CheckBox();
            this.grpPick = new System.Windows.Forms.GroupBox();
            this.clbWorksets = new System.Windows.Forms.CheckedListBox();
            this.btnAll = new System.Windows.Forms.Button();
            this.btnNone = new System.Windows.Forms.Button();
            this.lblCount = new System.Windows.Forms.Label();
            this.lblNote = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpPick.SuspendLayout();
            this.SuspendLayout();
            //
            // lblModel
            //
            this.lblModel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblModel.AutoSize = false;
            this.lblModel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblModel.Location = new System.Drawing.Point(12, 12);
            this.lblModel.Name = "lblModel";
            this.lblModel.Size = new System.Drawing.Size(536, 20);
            this.lblModel.TabIndex = 0;
            this.lblModel.Text = "model";
            //
            // chkInherit
            //
            this.chkInherit.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.chkInherit.Location = new System.Drawing.Point(12, 38);
            this.chkInherit.Name = "chkInherit";
            this.chkInherit.Size = new System.Drawing.Size(536, 20);
            this.chkInherit.TabIndex = 1;
            this.chkInherit.Text = "Open the worksets the main window says (untick to pick them here)";
            this.chkInherit.UseVisualStyleBackColor = true;
            this.chkInherit.CheckedChanged += new System.EventHandler(this.chkInherit_CheckedChanged);
            //
            // grpPick
            //
            this.grpPick.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpPick.Controls.Add(this.clbWorksets);
            this.grpPick.Controls.Add(this.btnAll);
            this.grpPick.Controls.Add(this.btnNone);
            this.grpPick.Controls.Add(this.lblCount);
            this.grpPick.Location = new System.Drawing.Point(12, 64);
            this.grpPick.Name = "grpPick";
            this.grpPick.Size = new System.Drawing.Size(536, 320);
            this.grpPick.TabIndex = 2;
            this.grpPick.TabStop = false;
            this.grpPick.Text = "Worksets to open in this model";
            //
            // clbWorksets
            //
            this.clbWorksets.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.clbWorksets.CheckOnClick = true;
            this.clbWorksets.FormattingEnabled = true;
            this.clbWorksets.HorizontalScrollbar = true;
            this.clbWorksets.IntegralHeight = false;
            this.clbWorksets.Location = new System.Drawing.Point(12, 24);
            this.clbWorksets.Name = "clbWorksets";
            this.clbWorksets.Size = new System.Drawing.Size(380, 282);
            this.clbWorksets.TabIndex = 0;
            this.clbWorksets.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.clbWorksets_ItemCheck);
            //
            // btnAll
            //
            this.btnAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAll.Location = new System.Drawing.Point(404, 24);
            this.btnAll.Name = "btnAll";
            this.btnAll.Size = new System.Drawing.Size(118, 28);
            this.btnAll.TabIndex = 1;
            this.btnAll.Text = "Tick all";
            this.btnAll.UseVisualStyleBackColor = true;
            this.btnAll.Click += new System.EventHandler(this.btnAll_Click);
            //
            // btnNone
            //
            this.btnNone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNone.Location = new System.Drawing.Point(404, 58);
            this.btnNone.Name = "btnNone";
            this.btnNone.Size = new System.Drawing.Size(118, 28);
            this.btnNone.TabIndex = 2;
            this.btnNone.Text = "None";
            this.btnNone.UseVisualStyleBackColor = true;
            this.btnNone.Click += new System.EventHandler(this.btnNone_Click);
            //
            // lblCount
            //
            this.lblCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblCount.AutoSize = false;
            this.lblCount.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.lblCount.Location = new System.Drawing.Point(404, 96);
            this.lblCount.Name = "lblCount";
            this.lblCount.Size = new System.Drawing.Size(118, 140);
            this.lblCount.TabIndex = 3;
            this.lblCount.Text = "";
            //
            // lblNote
            //
            this.lblNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblNote.AutoSize = false;
            this.lblNote.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
            this.lblNote.Location = new System.Drawing.Point(12, 392);
            this.lblNote.Name = "lblNote";
            this.lblNote.Size = new System.Drawing.Size(536, 32);
            this.lblNote.TabIndex = 3;
            this.lblNote.Text = "Worksets are remembered by name AND by their id, so a renamed one is still picked. Anything not opened is not in the .nwc, which is the point - but it is also how a file ends up missing what somebody expected.";
            //
            // btnOk
            //
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(328, 430);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(105, 30);
            this.btnOk.TabIndex = 4;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(443, 430);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(105, 30);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // NWCWORKSETForm
            //
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(560, 472);
            this.Controls.Add(this.lblModel);
            this.Controls.Add(this.chkInherit);
            this.Controls.Add(this.grpPick);
            this.Controls.Add(this.lblNote);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "NWCWORKSETForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Worksets for one model";
            this.Load += new System.EventHandler(this.NWCWORKSETForm_Load);
            this.grpPick.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.CheckBox chkInherit;
        private System.Windows.Forms.GroupBox grpPick;
        private System.Windows.Forms.CheckedListBox clbWorksets;
        private System.Windows.Forms.Button btnAll;
        private System.Windows.Forms.Button btnNone;
        private System.Windows.Forms.Label lblCount;
        private System.Windows.Forms.Label lblNote;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
