using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Modeless, so it can sit open while you work and show what
    // AutoSync has been doing. It holds no Revit API call of its own:
    // Sync now goes through SYNCSERVICE, which raises the external
    // event, exactly as the other modeless windows here do.
    //
    // Closing this window does NOT stop AutoSync. The timer lives in
    // SYNCSERVICE and outlives every window - which is the point.
    public partial class SYNCForm : System.Windows.Forms.Form
    {
        // The one open window, so the command can bring it forward and
        // SYNCEVENT can refresh it after a run
        public static SYNCForm Open;

        private bool _loadingUi;

        public SYNCForm()
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            Open = this;
        }

        private void SYNCForm_Load(object sender, EventArgs e)
        {
            SYNCSETTINGS.Load();

            Columns();

            _loadingUi = true;

            numMinutes.Value = Clamp(SYNCSETTINGS.Minutes);
            chkRelinquish.Checked = SYNCSETTINGS.Relinquish;
            txtComment.Text = SYNCSETTINGS.Comment ?? string.Empty;
            txtHost.Text = SYNCSETTINGS.Host ?? SYNCNET.DefaultHost;

            _loadingUi = false;

            lblHostHint.Text =
                "A cloud model is tested by reaching that host. A model " +
                "with a central file on a server is tested by looking " +
                "for the file itself, so the host does not matter to it." +
                Environment.NewLine +
                "Either way, if central cannot be reached the model is " +
                "saved locally instead and the list below says so.";

            ticker.Start();

            Refreshed();
        }

        private static decimal Clamp(int minutes)
        {
            int wanted = SYNCSETTINGS.Floor(minutes);

            return wanted > 480 ? 480 : wanted;
        }

        private void Columns()
        {
            dgvLog.Columns.Add(Column("colWhen", "Time", 70));
            dgvLog.Columns.Add(Column("colWhat", "", 62));
            dgvLog.Columns.Add(Column("colModel", "Model", 220));

            DataGridViewTextBoxColumn detail =
                Column("colSaid", "What happened", 340);

            detail.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dgvLog.Columns.Add(detail);
        }

        // NOT named Text: Form.Text is an inherited property, and a
        // helper named after it hides the property so the designer's
        // own "this.Text = ..." stops compiling
        private static DataGridViewTextBoxColumn Column(
            string name, string header, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width
            };
        }

        // ==================================================
        // Saying where things stand
        // ==================================================

        // Called from SYNCEVENT when a run finishes. That runs on
        // Revit's own thread, which is this window's thread too, but the
        // guard costs nothing and covers a future caller that is not.
        public void Refreshed()
        {
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(Refreshed));
                }
                catch
                {
                    // the window is going away; nothing to refresh
                }

                return;
            }

            State();
            Rows();
        }

        private void State()
        {
            bool on = SYNCSERVICE.On;

            btnToggle.Text = on ? "Turn it off" : "Turn it on";

            if (!SYNCSERVICE.Ready)
            {
                lblState.Text = "Not ready - restart Revit";
                lblState.ForeColor = System.Drawing.Color.Firebrick;
            }
            else if (on)
            {
                lblState.Text = "Running, every " +
                                SYNCSETTINGS.Floor(SYNCSETTINGS.Minutes) +
                                " minute(s)" + Countdown();

                lblState.ForeColor = System.Drawing.Color.ForestGreen;
            }
            else
            {
                lblState.Text = "Off";
                lblState.ForeColor = System.Drawing.SystemColors.ControlText;
            }

            lblLastRun.Text = SYNCLOG.LastRun == DateTime.MinValue
                ? "It has not run yet this session."
                : "Last run " + SYNCLOG.LastRun.ToString("HH:mm:ss") + ".";

            if (SYNCSERVICE.Busy)
                lblLastRun.Text = "Running now...";
        }

        private static string Countdown()
        {
            DateTime next = SYNCSERVICE.NextRun;

            if (next == DateTime.MinValue)
                return string.Empty;

            TimeSpan left = next - DateTime.Now;

            if (left.TotalSeconds < 0)
                return "  -  due now";

            return "  -  next in " +
                   ((int)left.TotalMinutes).ToString() + "m " +
                   left.Seconds.ToString("00") + "s";
        }

        private void Rows()
        {
            List<SYNCLOG.Entry> entries = SYNCLOG.Recent();

            dgvLog.Rows.Clear();

            int synced = 0;
            int saved = 0;
            int failed = 0;

            foreach (SYNCLOG.Entry entry in entries)
            {
                if (entry.Did == SYNCLOG.What.Synced) synced++;
                else if (entry.Did == SYNCLOG.What.Saved) saved++;
                else if (entry.Did == SYNCLOG.What.Failed) failed++;

                int index = dgvLog.Rows.Add();

                DataGridViewRow row = dgvLog.Rows[index];

                row.Cells[0].Value = entry.When.ToString("HH:mm:ss");
                row.Cells[1].Value = entry.Label;
                row.Cells[2].Value = entry.Model;
                row.Cells[3].Value = entry.Said;

                row.Cells[2].ToolTipText = entry.Model;
                row.Cells[3].ToolTipText = entry.Said;

                if (entry.Did == SYNCLOG.What.Failed)
                {
                    row.Cells[1].Style.ForeColor =
                        System.Drawing.Color.Firebrick;
                }
                else if (entry.Did == SYNCLOG.What.Saved)
                {
                    row.Cells[1].Style.ForeColor =
                        System.Drawing.Color.DarkGoldenrod;
                }
                else if (entry.Did == SYNCLOG.What.Synced)
                {
                    row.Cells[1].Style.ForeColor =
                        System.Drawing.Color.ForestGreen;
                }
            }

            lblModels.Text = entries.Count == 0
                ? string.Empty
                : "This session: " + synced + " synced, " + saved +
                  " saved locally, " + failed + " failed.";

            btnClearLog.Enabled = entries.Count > 0;
        }

        private void ticker_Tick(object sender, EventArgs e)
        {
            // Only the countdown changes second to second; rebuilding
            // the whole list every second would fight the scrollbar
            State();
        }

        // ==================================================
        // Buttons
        // ==================================================

        private void btnToggle_Click(object sender, EventArgs e)
        {
            if (!SYNCSERVICE.Ready)
            {
                MessageBox.Show(this,
                    "AutoSync could not start this session. Restart " +
                    "Revit and it will offer itself again.",
                    "Atkins MEP Toolkit - AutoSync");

                return;
            }

            if (SYNCSERVICE.On)
            {
                SYNCSERVICE.Stop();
            }
            else
            {
                Remember();

                SYNCSERVICE.Start(SYNCSETTINGS.Minutes);
            }

            Refreshed();
        }

        private void numMinutes_ValueChanged(object sender, EventArgs e)
        {
            if (_loadingUi)
                return;

            Remember();

            // Already running: take the new interval from now rather
            // than making somebody switch it off and on again
            SYNCSERVICE.Restart(SYNCSETTINGS.Minutes);

            Refreshed();
        }

        private void Setting_Changed(object sender, EventArgs e)
        {
            if (_loadingUi)
                return;

            Remember();
        }

        private void Remember()
        {
            SYNCSETTINGS.Minutes = (int)numMinutes.Value;
            SYNCSETTINGS.Relinquish = chkRelinquish.Checked;
            SYNCSETTINGS.Comment = txtComment.Text;
            SYNCSETTINGS.Host = txtHost.Text;

            SYNCSETTINGS.Save();
        }

        private void btnSyncNow_Click(object sender, EventArgs e)
        {
            if (!SYNCSERVICE.Ready)
                return;

            if (SYNCSERVICE.Busy)
            {
                MessageBox.Show(this,
                    "It is already running.", "Atkins MEP Toolkit - AutoSync");

                return;
            }

            Remember();

            // Queued, not done here: Revit runs it when it is idle
            SYNCSERVICE.Raise(SYNCEVENT.Why.Manual);

            lblLastRun.Text = "Asked Revit to sync; it runs as soon as " +
                              "Revit is idle.";
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            SYNCLOG.Clear();

            Refreshed();
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                this,
                "AutoSync saves and synchronises every model you have " +
                "open, on a timer." + Environment.NewLine +
                Environment.NewLine +
                "It asks each time Revit starts, and is off unless you " +
                "say otherwise. Five minutes is the shortest interval it " +
                "accepts." + Environment.NewLine +
                Environment.NewLine +
                "Before each sync it checks that central can be reached: " +
                "a cloud model by reaching the host on the right, a model " +
                "with a central file by looking for that file. If it " +
                "cannot be reached, your work is SAVED LOCALLY instead " +
                "and the list says why. Nothing is lost either way." +
                Environment.NewLine + Environment.NewLine +
                "A model that is not workshared, or is detached, has " +
                "nothing to sync to and is simply saved." +
                Environment.NewLine + Environment.NewLine +
                "It only runs when Revit is idle, so it will not " +
                "interrupt you mid-command - it waits until you are " +
                "between things." + Environment.NewLine +
                Environment.NewLine +
                "Closing this window does not switch it off.",
                "Atkins MEP Toolkit - AutoSync",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            ticker.Stop();

            Open = null;
        }
    }
}
