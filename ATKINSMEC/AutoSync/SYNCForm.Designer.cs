
namespace ATKINSMEC
{
    partial class SYNCForm
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
            // The countdown timer is handed this container, so it must
            // exist before anything asks for it - passing the null it
            // starts as would throw
            this.components = new System.ComponentModel.Container();

            this.grpState = new System.Windows.Forms.GroupBox();
            this.lblState = new System.Windows.Forms.Label();
            this.btnToggle = new System.Windows.Forms.Button();
            this.lblLastRun = new System.Windows.Forms.Label();
            this.lblModels = new System.Windows.Forms.Label();

            this.grpSettings = new System.Windows.Forms.GroupBox();
            this.lblEvery = new System.Windows.Forms.Label();
            this.numMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblMinutes = new System.Windows.Forms.Label();
            this.chkRelinquish = new System.Windows.Forms.CheckBox();
            this.lblComment = new System.Windows.Forms.Label();
            this.txtComment = new System.Windows.Forms.TextBox();
            this.lblHost = new System.Windows.Forms.Label();
            this.txtHost = new System.Windows.Forms.TextBox();
            this.lblHostHint = new System.Windows.Forms.Label();

            this.grpLog = new System.Windows.Forms.GroupBox();
            this.dgvLog = new System.Windows.Forms.DataGridView();
            this.btnSyncNow = new System.Windows.Forms.Button();
            this.btnClearLog = new System.Windows.Forms.Button();

            this.btnHelp = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.ticker = new System.Windows.Forms.Timer(this.components);

            this.grpState.SuspendLayout();
            this.grpSettings.SuspendLayout();
            this.grpLog.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLog)).BeginInit();
            this.SuspendLayout();
            //
            // grpState
            //
            this.grpState.Controls.Add(this.lblState);
            this.grpState.Controls.Add(this.btnToggle);
            this.grpState.Controls.Add(this.lblLastRun);
            this.grpState.Controls.Add(this.lblModels);
            this.grpState.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpState.Location = new System.Drawing.Point(12, 12);
            this.grpState.Name = "grpState";
            this.grpState.Size = new System.Drawing.Size(736, 96);
            this.grpState.TabIndex = 0;
            this.grpState.TabStop = false;
            this.grpState.Text = "1.  AutoSync";
            //
            // lblState
            //
            this.lblState.Font = new System.Drawing.Font(
                "Microsoft Sans Serif", 9.75F,
                System.Drawing.FontStyle.Bold);
            this.lblState.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblState.Location = new System.Drawing.Point(12, 24);
            this.lblState.Name = "lblState";
            this.lblState.Size = new System.Drawing.Size(560, 20);
            this.lblState.TabIndex = 0;
            this.lblState.Text = "";
            //
            // btnToggle
            //
            this.btnToggle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnToggle.Location = new System.Drawing.Point(588, 22);
            this.btnToggle.Name = "btnToggle";
            this.btnToggle.Size = new System.Drawing.Size(136, 30);
            this.btnToggle.TabIndex = 1;
            this.btnToggle.Text = "Turn it on";
            this.btnToggle.UseVisualStyleBackColor = true;
            this.btnToggle.Click += new System.EventHandler(this.btnToggle_Click);
            //
            // lblLastRun
            //
            this.lblLastRun.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblLastRun.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblLastRun.Location = new System.Drawing.Point(12, 48);
            this.lblLastRun.Name = "lblLastRun";
            this.lblLastRun.Size = new System.Drawing.Size(560, 18);
            this.lblLastRun.TabIndex = 2;
            this.lblLastRun.Text = "";
            //
            // lblModels
            //
            this.lblModels.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblModels.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblModels.Location = new System.Drawing.Point(12, 68);
            this.lblModels.Name = "lblModels";
            this.lblModels.Size = new System.Drawing.Size(712, 18);
            this.lblModels.TabIndex = 3;
            this.lblModels.Text = "";
            //
            // grpSettings
            //
            this.grpSettings.Controls.Add(this.lblEvery);
            this.grpSettings.Controls.Add(this.numMinutes);
            this.grpSettings.Controls.Add(this.lblMinutes);
            this.grpSettings.Controls.Add(this.chkRelinquish);
            this.grpSettings.Controls.Add(this.lblComment);
            this.grpSettings.Controls.Add(this.txtComment);
            this.grpSettings.Controls.Add(this.lblHost);
            this.grpSettings.Controls.Add(this.txtHost);
            this.grpSettings.Controls.Add(this.lblHostHint);
            this.grpSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpSettings.Location = new System.Drawing.Point(12, 116);
            this.grpSettings.Name = "grpSettings";
            this.grpSettings.Size = new System.Drawing.Size(736, 150);
            this.grpSettings.TabIndex = 1;
            this.grpSettings.TabStop = false;
            this.grpSettings.Text = "2.  How it should run";
            //
            // lblEvery
            //
            this.lblEvery.Location = new System.Drawing.Point(12, 28);
            this.lblEvery.Name = "lblEvery";
            this.lblEvery.Size = new System.Drawing.Size(44, 18);
            this.lblEvery.TabIndex = 0;
            this.lblEvery.Text = "Every";
            //
            // numMinutes
            //
            this.numMinutes.Location = new System.Drawing.Point(60, 25);
            this.numMinutes.Maximum = new decimal(new int[] { 480, 0, 0, 0 });
            this.numMinutes.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numMinutes.Name = "numMinutes";
            this.numMinutes.Size = new System.Drawing.Size(56, 20);
            this.numMinutes.TabIndex = 1;
            this.numMinutes.Value = new decimal(new int[] { 15, 0, 0, 0 });
            this.numMinutes.ValueChanged += new System.EventHandler(this.numMinutes_ValueChanged);
            //
            // lblMinutes
            //
            this.lblMinutes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblMinutes.Location = new System.Drawing.Point(122, 28);
            this.lblMinutes.Name = "lblMinutes";
            this.lblMinutes.Size = new System.Drawing.Size(330, 18);
            this.lblMinutes.TabIndex = 2;
            this.lblMinutes.Text = "minutes   (five is the shortest it will accept)";
            //
            // chkRelinquish
            //
            this.chkRelinquish.Location = new System.Drawing.Point(12, 54);
            this.chkRelinquish.Name = "chkRelinquish";
            this.chkRelinquish.Size = new System.Drawing.Size(560, 20);
            this.chkRelinquish.TabIndex = 3;
            this.chkRelinquish.Text = "Give back everything I have borrowed each time it syncs";
            this.chkRelinquish.UseVisualStyleBackColor = true;
            this.chkRelinquish.CheckedChanged += new System.EventHandler(this.Setting_Changed);
            //
            // lblComment
            //
            this.lblComment.Location = new System.Drawing.Point(12, 86);
            this.lblComment.Name = "lblComment";
            this.lblComment.Size = new System.Drawing.Size(64, 18);
            this.lblComment.TabIndex = 4;
            this.lblComment.Text = "Comment";
            //
            // txtComment
            //
            this.txtComment.Location = new System.Drawing.Point(80, 83);
            this.txtComment.Name = "txtComment";
            this.txtComment.Size = new System.Drawing.Size(280, 20);
            this.txtComment.TabIndex = 5;
            this.txtComment.TextChanged += new System.EventHandler(this.Setting_Changed);
            //
            // lblHost
            //
            this.lblHost.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblHost.Location = new System.Drawing.Point(380, 86);
            this.lblHost.Name = "lblHost";
            this.lblHost.Size = new System.Drawing.Size(116, 18);
            this.lblHost.TabIndex = 6;
            this.lblHost.Text = "Cloud reach test";
            //
            // txtHost
            //
            this.txtHost.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.txtHost.Location = new System.Drawing.Point(500, 83);
            this.txtHost.Name = "txtHost";
            this.txtHost.Size = new System.Drawing.Size(224, 20);
            this.txtHost.TabIndex = 7;
            this.txtHost.TextChanged += new System.EventHandler(this.Setting_Changed);
            //
            // lblHostHint
            //
            this.lblHostHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHostHint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblHostHint.Location = new System.Drawing.Point(12, 110);
            this.lblHostHint.Name = "lblHostHint";
            this.lblHostHint.Size = new System.Drawing.Size(712, 32);
            this.lblHostHint.TabIndex = 8;
            this.lblHostHint.Text = "";
            //
            // grpLog
            //
            this.grpLog.Controls.Add(this.dgvLog);
            this.grpLog.Controls.Add(this.btnSyncNow);
            this.grpLog.Controls.Add(this.btnClearLog);
            this.grpLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpLog.Location = new System.Drawing.Point(12, 274);
            this.grpLog.Name = "grpLog";
            this.grpLog.Size = new System.Drawing.Size(736, 230);
            this.grpLog.TabIndex = 2;
            this.grpLog.TabStop = false;
            this.grpLog.Text = "3.  What it has done";
            //
            // dgvLog
            //
            this.dgvLog.AllowUserToAddRows = false;
            this.dgvLog.AllowUserToDeleteRows = false;
            this.dgvLog.AllowUserToResizeRows = false;
            this.dgvLog.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvLog.ColumnHeadersHeightSizeMode =
                System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvLog.Location = new System.Drawing.Point(12, 24);
            this.dgvLog.MultiSelect = false;
            this.dgvLog.Name = "dgvLog";
            this.dgvLog.ReadOnly = true;
            this.dgvLog.RowHeadersVisible = false;
            this.dgvLog.SelectionMode =
                System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvLog.Size = new System.Drawing.Size(712, 160);
            this.dgvLog.TabIndex = 0;
            //
            // btnSyncNow
            //
            this.btnSyncNow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnSyncNow.Location = new System.Drawing.Point(12, 192);
            this.btnSyncNow.Name = "btnSyncNow";
            this.btnSyncNow.Size = new System.Drawing.Size(140, 28);
            this.btnSyncNow.TabIndex = 1;
            this.btnSyncNow.Text = "Sync now";
            this.btnSyncNow.UseVisualStyleBackColor = true;
            this.btnSyncNow.Click += new System.EventHandler(this.btnSyncNow_Click);
            //
            // btnClearLog
            //
            this.btnClearLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnClearLog.Location = new System.Drawing.Point(162, 192);
            this.btnClearLog.Name = "btnClearLog";
            this.btnClearLog.Size = new System.Drawing.Size(140, 28);
            this.btnClearLog.TabIndex = 2;
            this.btnClearLog.Text = "Clear the list";
            this.btnClearLog.UseVisualStyleBackColor = true;
            this.btnClearLog.Click += new System.EventHandler(this.btnClearLog_Click);
            //
            // btnHelp
            //
            this.btnHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnHelp.Location = new System.Drawing.Point(12, 516);
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(140, 30);
            this.btnHelp.TabIndex = 3;
            this.btnHelp.Text = "How to use this";
            this.btnHelp.UseVisualStyleBackColor = true;
            this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(628, 516);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(120, 30);
            this.btnClose.TabIndex = 4;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // ticker
            //
            this.ticker.Interval = 1000;
            this.ticker.Tick += new System.EventHandler(this.ticker_Tick);
            //
            // SYNCForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 560);
            this.Controls.Add(this.grpState);
            this.Controls.Add(this.grpSettings);
            this.Controls.Add(this.grpLog);
            this.Controls.Add(this.btnHelp);
            this.Controls.Add(this.btnClose);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "SYNCForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Atkins MEP Toolkit - AutoSync";
            this.Load += new System.EventHandler(this.SYNCForm_Load);
            this.grpState.ResumeLayout(false);
            this.grpSettings.ResumeLayout(false);
            this.grpSettings.PerformLayout();
            this.grpLog.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLog)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpState;
        private System.Windows.Forms.Label lblState;
        private System.Windows.Forms.Button btnToggle;
        private System.Windows.Forms.Label lblLastRun;
        private System.Windows.Forms.Label lblModels;

        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.Label lblEvery;
        private System.Windows.Forms.NumericUpDown numMinutes;
        private System.Windows.Forms.Label lblMinutes;
        private System.Windows.Forms.CheckBox chkRelinquish;
        private System.Windows.Forms.Label lblComment;
        private System.Windows.Forms.TextBox txtComment;
        private System.Windows.Forms.Label lblHost;
        private System.Windows.Forms.TextBox txtHost;
        private System.Windows.Forms.Label lblHostHint;

        private System.Windows.Forms.GroupBox grpLog;
        private System.Windows.Forms.DataGridView dgvLog;
        private System.Windows.Forms.Button btnSyncNow;
        private System.Windows.Forms.Button btnClearLog;

        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Timer ticker;
    }
}
