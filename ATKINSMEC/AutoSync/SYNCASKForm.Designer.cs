
namespace ATKINSMEC
{
    partial class SYNCASKForm
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblBody = new System.Windows.Forms.Label();
            this.lblEvery = new System.Windows.Forms.Label();
            this.numMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblMinutes = new System.Windows.Forms.Label();
            this.lblOffline = new System.Windows.Forms.Label();
            this.btnOn = new System.Windows.Forms.Button();
            this.btnOff = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numMinutes)).BeginInit();
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.Font = new System.Drawing.Font(
                "Microsoft Sans Serif", 11F,
                System.Drawing.FontStyle.Bold);
            this.lblTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTitle.Location = new System.Drawing.Point(16, 16);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(448, 24);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Turn AutoSync on for this session?";
            //
            // lblBody
            //
            this.lblBody.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblBody.Location = new System.Drawing.Point(16, 46);
            this.lblBody.Name = "lblBody";
            this.lblBody.Size = new System.Drawing.Size(448, 56);
            this.lblBody.TabIndex = 1;
            this.lblBody.Text = "";
            //
            // lblEvery
            //
            this.lblEvery.Location = new System.Drawing.Point(16, 112);
            this.lblEvery.Name = "lblEvery";
            this.lblEvery.Size = new System.Drawing.Size(44, 18);
            this.lblEvery.TabIndex = 2;
            this.lblEvery.Text = "Every";
            //
            // numMinutes
            //
            this.numMinutes.Location = new System.Drawing.Point(64, 109);
            this.numMinutes.Maximum = new decimal(new int[] { 480, 0, 0, 0 });
            this.numMinutes.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numMinutes.Name = "numMinutes";
            this.numMinutes.Size = new System.Drawing.Size(56, 20);
            this.numMinutes.TabIndex = 3;
            this.numMinutes.Value = new decimal(new int[] { 15, 0, 0, 0 });
            //
            // lblMinutes
            //
            this.lblMinutes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblMinutes.Location = new System.Drawing.Point(126, 112);
            this.lblMinutes.Name = "lblMinutes";
            this.lblMinutes.Size = new System.Drawing.Size(338, 18);
            this.lblMinutes.TabIndex = 4;
            this.lblMinutes.Text = "minutes   (five is the shortest it will accept)";
            //
            // lblOffline
            //
            this.lblOffline.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblOffline.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblOffline.Location = new System.Drawing.Point(16, 140);
            this.lblOffline.Name = "lblOffline";
            this.lblOffline.Size = new System.Drawing.Size(448, 34);
            this.lblOffline.TabIndex = 5;
            this.lblOffline.Text = "If central cannot be reached when the time comes, it saves your work locally inste" +
                "ad and says so.";
            //
            // btnOn
            //
            this.btnOn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOn.Location = new System.Drawing.Point(196, 186);
            this.btnOn.Name = "btnOn";
            this.btnOn.Size = new System.Drawing.Size(130, 30);
            this.btnOn.TabIndex = 6;
            this.btnOn.Text = "Turn it on";
            this.btnOn.UseVisualStyleBackColor = true;
            this.btnOn.Click += new System.EventHandler(this.btnOn_Click);
            //
            // btnOff
            //
            this.btnOff.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOff.Location = new System.Drawing.Point(334, 186);
            this.btnOff.Name = "btnOff";
            this.btnOff.Size = new System.Drawing.Size(130, 30);
            this.btnOff.TabIndex = 7;
            this.btnOff.Text = "Not this time";
            this.btnOff.UseVisualStyleBackColor = true;
            this.btnOff.Click += new System.EventHandler(this.btnOff_Click);
            //
            // SYNCASKForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(480, 230);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblBody);
            this.Controls.Add(this.lblEvery);
            this.Controls.Add(this.numMinutes);
            this.Controls.Add(this.lblMinutes);
            this.Controls.Add(this.lblOffline);
            this.Controls.Add(this.btnOn);
            this.Controls.Add(this.btnOff);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "SYNCASKForm";
            // It has a minimise box now, and a window that can be
            // minimised with no taskbar button to bring it back is
            // a window somebody loses
            this.ShowInTaskbar = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Atkins MEP Toolkit - AutoSync";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.SYNCASKForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.numMinutes)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblBody;
        private System.Windows.Forms.Label lblEvery;
        private System.Windows.Forms.NumericUpDown numMinutes;
        private System.Windows.Forms.Label lblMinutes;
        private System.Windows.Forms.Label lblOffline;
        private System.Windows.Forms.Button btnOn;
        private System.Windows.Forms.Button btnOff;
    }
}
