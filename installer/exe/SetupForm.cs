using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ATKINSMEC.Setup
{
    // The installer window. Built in code rather than with a designer -
    // it is one screen and this keeps the whole installer to four files.
    internal class SetupForm : Form
    {
        private readonly CheckedListBox _versions = new CheckedListBox();
        private readonly TextBox _log = new TextBox();
        private readonly Button _install = new Button();
        private readonly Button _uninstall = new Button();
        private readonly Button _close = new Button();
        private readonly Label _heading = new Label();
        private readonly Label _where = new Label();

        internal SetupForm()
        {
            Text = Edition.Title;
            ClientSize = new Size(560, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            _heading.SetBounds(16, 14, 528, 20);
            _heading.Text = "AUTOVENT  |  CHW VOLUME  |  DUCT INSULATION  |  SPRINKLERS";
            _heading.Font = new Font(Font, FontStyle.Bold);

            _where.SetBounds(16, 38, 528, 34);
            _where.ForeColor = SystemColors.GrayText;
            _where.Text =
                "Installs into your own Windows profile, so no " +
                "administrator rights are needed." + Environment.NewLine +
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData) +
                @"\Autodesk\Revit\Addins\";

            var pick = new Label();
            pick.SetBounds(16, 82, 528, 18);
            pick.Text = "Revit versions found on this computer:";

            _versions.SetBounds(16, 102, 528, 108);
            _versions.CheckOnClick = true;
            _versions.IntegralHeight = false;

            _install.SetBounds(16, 222, 150, 32);
            _install.Text = "Install";
            _install.Click += Install_Click;

            _uninstall.SetBounds(176, 222, 150, 32);
            _uninstall.Text = "Uninstall";
            _uninstall.Click += Uninstall_Click;

            _close.SetBounds(394, 222, 150, 32);
            _close.Text = "Close";
            _close.Click += delegate { Close(); };

            _log.SetBounds(16, 266, 528, 188);
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = SystemColors.Window;

            Controls.Add(_heading);
            Controls.Add(_where);
            Controls.Add(pick);
            Controls.Add(_versions);
            Controls.Add(_install);
            Controls.Add(_uninstall);
            Controls.Add(_close);
            Controls.Add(_log);

            LoadVersions();
        }

        private void LoadVersions()
        {
            _versions.Items.Clear();

            var found = 0;

            foreach (string version in Payload.Versions())
            {
                bool present = Installer.RevitPresent(version);

                string label = present
                    ? "Revit " + version
                    : "Revit " + version + "   (not installed here)";

                _versions.Items.Add(label, present);

                if (present)
                    found++;
            }

            if (found == 0)
            {
                Say("No Revit installation was found on this computer.");
                Say("");
                Say("Tick a version anyway if Revit is installed somewhere");
                Say("unusual, and the add-in will be put in place for it.");

                return;
            }

            Say("Ready. " + found + " Revit version(s) found.");
            Say("");
            Say("Close Revit before installing - Windows will not replace");
            Say("files that Revit has open.");
        }

        private List<string> Ticked()
        {
            var wanted = new List<string>();

            for (int i = 0; i < _versions.Items.Count; i++)
            {
                if (_versions.GetItemChecked(i))
                    wanted.Add(Payload.Versions()[i]);
            }

            return wanted;
        }

        // Replacing a DLL Revit has open silently fails, so refuse
        // rather than half-install
        private bool RevitIsClosed()
        {
            List<string> running = Installer.RunningRevits();

            if (running.Count == 0)
                return true;

            MessageBox.Show(
                this,
                "Revit is running:" + Environment.NewLine +
                Environment.NewLine +
                "    " + string.Join(
                    Environment.NewLine + "    ", running.ToArray()) +
                Environment.NewLine + Environment.NewLine +
                "Windows will not let its add-in files be replaced while " +
                "it is open, so this would appear to work and change " +
                "nothing." + Environment.NewLine + Environment.NewLine +
                "Close Revit and try again.",
                Edition.Name,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return false;
        }

        private void Install_Click(object sender, EventArgs e)
        {
            List<string> versions = Ticked();

            if (versions.Count == 0)
            {
                MessageBox.Show(this, "Tick at least one Revit version.",
                    Edition.Name);

                return;
            }

            if (!RevitIsClosed())
                return;

            Busy(true);

            try
            {
                Say("");

                foreach (string version in versions)
                {
                    int count = Installer.Install(version);

                    Say("Revit " + version + " - installed " + count + " file(s)");
                }

                Say("");
                Say("Done. Start Revit and look for the Atkins MEP Toolkit tab,");
                Say("Mechanical panel.");

                MessageBox.Show(
                    this,
                    "Installed for " + versions.Count +
                    " Revit version(s)." + Environment.NewLine +
                    Environment.NewLine +
                    "Start Revit and look for the Atkins MEP Toolkit tab.",
                    Edition.Name,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Say("");
                Say("FAILED: " + ex.Message);

                MessageBox.Show(this, ex.Message, Edition.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Busy(false);
            }
        }

        private void Uninstall_Click(object sender, EventArgs e)
        {
            if (!RevitIsClosed())
                return;

            DialogResult sure = MessageBox.Show(
                this,
                "Remove " + Edition.Name + " from every Revit version it was " +
                "installed into?" + Environment.NewLine +
                Environment.NewLine +
                "Other add-ins sharing the same folder are left alone.",
                Edition.Name,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (sure != DialogResult.Yes)
                return;

            Busy(true);

            try
            {
                Say("");

                int total = 0;

                foreach (string version in Payload.Versions())
                {
                    int count = Installer.Uninstall(version);

                    if (count > 0)
                    {
                        Say("Revit " + version + " - removed " + count + " file(s)");

                        total += count;
                    }
                }

                if (total == 0)
                {
                    Say("Nothing was installed by this installer.");

                    return;
                }

                Say("");

                OfferToRemoveSettings();
            }
            catch (Exception ex)
            {
                Say("");
                Say("FAILED: " + ex.Message);

                MessageBox.Show(this, ex.Message, Edition.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Busy(false);
            }
        }

        // The settings are the user's own work, so they are kept unless
        // asked for
        private void OfferToRemoveSettings()
        {
            if (!Installer.SettingsExist)
            {
                Say("Removed.");

                return;
            }

            DialogResult also = MessageBox.Show(
                this,
                "Remove your settings as well?" + Environment.NewLine +
                Environment.NewLine +
                "That is " + Edition.SettingsSaid + ", in" + Environment.NewLine +
                Installer.SettingsFolder + Environment.NewLine +
                Environment.NewLine +
                "Choose No to keep them for a later reinstall.",
                Edition.Name,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (also == DialogResult.Yes)
            {
                Installer.RemoveSettings();

                Say("Settings removed.");
            }
            else
            {
                Say("Settings kept in " + Installer.SettingsFolder);
            }
        }

        private void Busy(bool busy)
        {
            _install.Enabled = !busy;
            _uninstall.Enabled = !busy;
            _versions.Enabled = !busy;

            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            Application.DoEvents();
        }

        private void Say(string text)
        {
            _log.AppendText(text + Environment.NewLine);
        }
    }
}
