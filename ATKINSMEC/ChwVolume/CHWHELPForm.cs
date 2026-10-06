using System;
using System.Drawing;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Plain explanation of CHW VOLUME, shown from the Help button. The
    // text is built here rather than shipped as a file so it cannot go
    // missing from the add-ins folder, and so it stays next to the code
    // it describes.
    public partial class CHWHELPForm : System.Windows.Forms.Form
    {
        public CHWHELPForm()
        {
            InitializeComponent();

            FORMSIZE.Remember(this);
        }

        private void CHWHELPForm_Load(object sender, EventArgs e)
        {
            rtbHelp.Clear();

            Heading("What it measures");
            Body(
                "The water held inside the pipes and fittings of two " +
                "piping system types - one you name as the chilled water " +
                "supply, one as the return. Pipes are measured from their " +
                "inner diameter and length; a fitting is measured from " +
                "its centre out to each of its connectors. Nothing in the " +
                "model is changed: the add-in only reads.");

            Heading("The two tabs");
            Body(
                "Open in Revit lists the model you have open and every " +
                "linked model loaded inside it. A link placed several " +
                "times is counted once.");
            Body(
                "ACC models lists models kept in the cloud, which the " +
                "add-in opens in the background purely to measure, then " +
                "closes. They never appear in the Revit window.");
            Body(
                "They are two lists only because they are managed " +
                "differently. Calculate measures everything ticked on " +
                "both tabs together, and the results are one table.");

            Heading("Setting the two system types");
            Body(
                "Every model has its own pair, set in its own row. Click " +
                "the Supply or Return cell and pick a name from the drop " +
                "down, or type one in. This matters because two models " +
                "rarely name their systems the same way - one may say " +
                "Chilled Water Supply where another says CHWS.");
            Body(
                "The drop down lists only the system types that pipes or " +
                "fittings in the model are actually on, not every type " +
                "the template happens to define. If a name you expect is " +
                "missing, nothing has been drawn on it - and it would " +
                "measure zero anyway.");
            Body(
                "Rows can be selected together - Ctrl for one at a time, " +
                "Shift for a run of them. While more than one row is " +
                "selected the buttons say so, and act on the selection " +
                "instead of the whole tab: ticking, unticking, and " +
                "removing ACC models all follow what is highlighted.");
            Body(
                "The two must be different types, otherwise supply and " +
                "return cannot be told apart. A model shown in red has " +
                "one of them missing and will stop the calculation until " +
                "it is filled in or the model is unticked.");

            Heading("How much of an ACC model to load");
            Body(
                "The Worksets column is on the ACC tab only, since those " +
                "are the models the add-in opens for you. A model already " +
                "open in Revit is loaded however you opened it.");
            Body(
                "All worksets loads the whole model, and is what you want " +
                "for a real figure.");
            Body(
                "Editable only loads the worksets you own and nothing " +
                "else. It is quicker, but the volume then covers your " +
                "part of the model alone - everyone else's pipework is " +
                "left out. A model measured that way is marked in the " +
                "results so the number is not mistaken for the whole " +
                "model. If you own no worksets in that model, it will " +
                "find nothing at all.");

            Heading("1.  Calculate");
            Body(
                "Everything ticked on both tabs is measured in turn. A " +
                "large model, or one in ACC, takes a while - the window " +
                "will be busy until it finishes.");
            Body(
                "Each ACC model is opened afresh every time you press " +
                "Calculate, since a closed model cannot be read. Press it " +
                "once you have the tabs set up the way you want them.");

            Heading("Stopping a run");
            Body(
                "While it is working the Calculate button becomes Stop, " +
                "and the rest of the window is switched off so nothing " +
                "can be changed underneath the run.");
            Body(
                "Stop takes effect between models: the one being measured " +
                "finishes first, then the rest are left alone. If Revit " +
                "is part way through downloading a large ACC model the " +
                "wait can be a long one, and Revit alone decides whether " +
                "that download can be cut short - so Stop is a request, " +
                "not an instant halt.");
            Body(
                "What has been measured so far is still shown, with a red " +
                "line saying how many models were left out. That total is " +
                "a partial figure - do not read it as the whole job.");
            Body(
                "The window will not close while a run is going, because " +
                "that would leave a model open in the background.");

            Heading("2.  Read the results, and export");
            Body(
                "One row per model, showing the two system types it was " +
                "measured against, supply and return litres, the number " +
                "of pipes and fittings, and the total. The bold TOTAL row " +
                "adds them up. Export to Excel writes the same table to a " +
                "spreadsheet.");

            Heading("Adding an ACC model");
            Body(
                "Add ACC model lists every cloud model Revit has opened " +
                "on this computer before, by name. Tick the ones you " +
                "want; their project and model ids are filled in for you. " +
                "A model nobody has opened on this computer will not be " +
                "listed - use \"Type the ids instead\" for that one.");
            Body(
                "Revit has to be signed in to an account with access to " +
                "the project. The model is never saved or altered.");
            Body(
                "The first measurement of an ACC model is slower, because " +
                "the add-in works out which region the project lives in " +
                "and downloads the model. Both are remembered afterwards.");

            Heading("System type names for ACC models");
            Body(
                "A model that is closed cannot be read, so the type names " +
                "of an ACC model are not known until it has been opened " +
                "once. Press \"Read type names from ticked models\" to " +
                "fetch them, or simply run the calculation with the names " +
                "typed in - either way they are remembered and the drop " +
                "downs fill in by themselves from then on.");

            Heading("Running with no model open");
            Body(
                "CHW VOLUME works in an empty Revit, with no project " +
                "open, so an ACC model can be measured on its own. The " +
                "window opens on the ACC models tab, the other tab is " +
                "empty, and the system type names have to be typed or " +
                "read first, since there is no open model to list them " +
                "from.");

            Heading("If something looks wrong");
            Body(
                "A red row saying a model could not be opened usually " +
                "means Revit is not signed in, or the account has no " +
                "access to that project. It can also mean the model was " +
                "moved or deleted in ACC.");
            Body(
                "A total of zero almost always means that model's two " +
                "system type names do not match anything inside it. Check " +
                "the spelling against that model's own piping system " +
                "types - remember each model has its own pair. A model " +
                "set to editable worksets only can also come back empty " +
                "if you own none of its worksets.");
            Body(
                "A note about skipped pipes or fittings means their size " +
                "could not be read; those are left out of the total " +
                "rather than guessed at.");
            Body(
                "An ACC model reports its own pipework only. Links inside " +
                "it are not loaded when it is opened in the background, " +
                "so add each linked model to the ACC tab separately if " +
                "you want its water too.");

            rtbHelp.SelectionStart = 0;
            rtbHelp.ScrollToCaret();
        }

        private void Heading(string text)
        {
            rtbHelp.SelectionFont = new Font(
                rtbHelp.Font.FontFamily,
                rtbHelp.Font.Size + 1.5f,
                FontStyle.Bold);

            rtbHelp.SelectionColor = Color.FromArgb(0, 70, 130);

            rtbHelp.AppendText(
                (rtbHelp.TextLength == 0 ? string.Empty : Environment.NewLine) +
                text + Environment.NewLine);
        }

        private void Body(string text)
        {
            rtbHelp.SelectionFont = new Font(
                rtbHelp.Font.FontFamily,
                rtbHelp.Font.Size,
                FontStyle.Regular);

            rtbHelp.SelectionColor = Color.FromArgb(30, 30, 30);

            // A blank line after each paragraph; a heading adds one more
            // of its own, so sections stay clearly apart
            rtbHelp.AppendText(
                text + Environment.NewLine + Environment.NewLine);
        }
    }
}
