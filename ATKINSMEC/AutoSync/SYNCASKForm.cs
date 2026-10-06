using System;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Asked once, every time Revit starts. Nothing that writes to a
    // central model should switch itself on because it was on last
    // Tuesday, which is why the answer is not remembered as a setting -
    // only as the button this opens on.
    public partial class SYNCASKForm : System.Windows.Forms.Form
    {
        public bool TurnOn { get; private set; }

        public int Minutes
        {
            get { return (int)numMinutes.Value; }
        }

        public SYNCASKForm()
        {
            InitializeComponent();

            FORMSIZE.Remember(this);
        }

        private void SYNCASKForm_Load(object sender, EventArgs e)
        {
            SYNCSETTINGS.Load();

            numMinutes.Value = Clamp(SYNCSETTINGS.Minutes);

            lblBody.Text =
                "AutoSync saves and synchronises every model you have " +
                "open, on a timer, so a long session is never far from " +
                "the last good copy." + Environment.NewLine +
                Environment.NewLine +
                "It is off unless you say otherwise, and you are asked " +
                "each time Revit starts.";

            // Open on whatever was chosen last time - the usual answer
            // is the same answer
            if (SYNCSETTINGS.LastAnswer)
                AcceptButton = btnOn;
            else
                AcceptButton = btnOff;
        }

        private static decimal Clamp(int minutes)
        {
            int wanted = SYNCSETTINGS.Floor(minutes);

            if (wanted > 480)
                wanted = 480;

            return wanted;
        }

        private void btnOn_Click(object sender, EventArgs e)
        {
            TurnOn = true;

            Remember();

            DialogResult = DialogResult.OK;

            Close();
        }

        private void btnOff_Click(object sender, EventArgs e)
        {
            TurnOn = false;

            Remember();

            DialogResult = DialogResult.Cancel;

            Close();
        }

        private void Remember()
        {
            SYNCSETTINGS.Minutes = Minutes;
            SYNCSETTINGS.LastAnswer = TurnOn;

            SYNCSETTINGS.Save();
        }
    }
}
