using System;
using System.Drawing;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Plain explanation of the NWC batch exporter, shown from the Help
    // button. Built here rather than shipped as a file so it cannot go
    // missing from the add-ins folder, and so it stays next to the code
    // it describes.
    public partial class NWCHELPForm : System.Windows.Forms.Form
    {
        public NWCHELPForm()
        {
            InitializeComponent();

            FORMSIZE.Remember(this);
        }

        private void NWCHELPForm_Load(object sender, EventArgs e)
        {
            rtbHelp.Clear();

            Heading("What it does");
            Body(
                "Writes a Navisworks .nwc out of a list of models, one " +
                "after another. Each one is opened in the background, " +
                "exported and closed again - none of them appears in the " +
                "Revit window, and nothing in any model is changed.");
            Body(
                "It is meant to be run with NO MODEL OPEN, on a machine " +
                "that can be left to it. Twenty models take as long as " +
                "twenty models take.");

            Heading("The exporter is not part of Revit");
            Body(
                "Revit cannot write an NWC on its own: the exporter is a " +
                "separate free Autodesk add-in that comes with " +
                "Navisworks, and there is a standalone installer for it. " +
                "This checks for it before starting, so a missing " +
                "exporter is one clear sentence rather than the same " +
                "error on every model in the list.");

            Heading("Building the list");
            Body(
                "Add Revit files picks .rvt files from disk - any " +
                "number at once. Add from ACC picks cloud models out of " +
                "Revit's own collaboration cache, so anything opened on " +
                "this machine before can be chosen from a list rather " +
                "than by typing GUIDs. Add the model open now puts " +
                "whatever is in front of you into the list.");
            Body(
                "The list and every setting on this window are " +
                "remembered between sessions, so a weekly export is " +
                "open-the-window-and-press-the-button. The tick box says " +
                "whether a model is in this run; Remove takes it out of " +
                "the list altogether, and acts only on rows you have " +
                "actually clicked.");

            Heading("Which view gets exported");
            Body(
                "A 3D view is named, and that NAME is looked for in " +
                "every model - it is the only thing models have in " +
                "common, since each has its own views. Keep a view " +
                "called Navisworks in every model for exactly this " +
                "reason.");
            Body(
                "A MODEL WITHOUT THAT VIEW IS REPORTED, NOT GUESSED AT. " +
                "Exporting the whole model instead would quietly swap " +
                "one deliverable for another, which is worse than saying " +
                "nothing was written. Choose (the whole model) when that " +
                "really is what you want.");
            Body(
                "The drop-down offers the 3D views of whatever is open " +
                "now, purely as a convenience for typing - the name is " +
                "matched in each model as it is opened.");

            Heading("When one model is different");
            Body(
                "Everything above is what the whole run does. Three of " +
                "those choices can be made for ONE model instead, from " +
                "its row in the list.");
            Body(
                "3D VIEW - type a view name straight into the row's 3D " +
                "view box. Leave it blank and the model follows the " +
                "window. Type (the whole model) to export all of it " +
                "while the rest of the run uses a view.");
            Body(
                "SETTINGS... - gives the picked model its own copy of " +
                "every option on this window. It is all of them or none " +
                "of them: a half-overridden set would mean every box " +
                "here having two meanings and nobody knowing which won. " +
                "The folder is never overridden - one run, one folder.");
            Body(
                "WORKSETS... - picks which worksets that model opens " +
                "with. Anything not opened is not in the .nwc. The list " +
                "is READ FROM THE MODEL each time, so it is never a " +
                "stale copy; a model that is not workshared says so.");
            Body(
                "Each workset is remembered by its name AND by its id. " +
                "The id survives a rename, so a workset somebody renamed " +
                "is still picked and the list says it was renamed. The " +
                "name is what makes the settings file readable, and is " +
                "what an older settings file has to go on.");
            Body(
                "There are two ways of reading them and it uses " +
                "whichever works, without asking. The quick one asks the " +
                "model's CENTRAL, and that fails whenever the central " +
                "cannot be reached - a local copy off the network, a " +
                "detached model, often a model in ACC. It then opens the " +
                "model in the background instead, which reads the file " +
                "itself and needs no central. That open loads every " +
                "workset CLOSED, so it takes the workset list and almost " +
                "no geometry, and it changes nothing.");
            Body(
                "The model you have open in Revit answers instantly, and " +
                "is never reopened. Everything else is read once and " +
                "remembered while the window is open, so setting up ten " +
                "models costs ten reads rather than one per click.");
            Body(
                "The list says which models are set up this way, and the " +
                "results table says what each one actually used - so a " +
                "file that came out wrong can be traced to the choice " +
                "that made it.");

            Heading("Naming and overwriting");
            Body(
                "Each file is named after its model, with whatever " +
                "prefix and suffix you give, and lands in the one " +
                "folder. A file already there IS OVERWRITTEN - that is " +
                "what makes a weekly re-export work.");
            Body(
                "Skip a file already newer than its model leaves alone " +
                "anything whose .nwc is newer than the .rvt it came " +
                "from, which on a re-run skips everything nobody has " +
                "touched. It only applies to models on disk: a cloud " +
                "model's date cannot be read without opening it, which " +
                "is the very thing being avoided.");

            Heading("Worksets, and what gets opened");
            Body(
                "Models are opened DETACHED, so a workshared model is " +
                "never opened against its central and nobody's worksets " +
                "are taken. Every workset is loaded by default; Only the " +
                "ones I own loads just those, which is faster and is " +
                "usually not what you want for an export. A model with " +
                "worksets picked for it ignores both and opens exactly " +
                "those.");
            Body(
                "A model you have open in Revit right now is exported " +
                "where it sits and is NEVER closed afterwards. Closing " +
                "the model somebody is working in is not a thing a batch " +
                "job may do.");

            Heading("Running it unattended");
            Body(
                "RUN SILENTLY answers Revit's own messages for you. A " +
                "batch of twenty models opens twenty models, and any one " +
                "of them can put up \"this was made in an earlier " +
                "version\", \"there are unresolved references\" or a " +
                "worksharing message - each of which waits for somebody. " +
                "Left overnight the batch stops at the first one and does " +
                "nothing until morning.");
            Body(
                "It presses OK, or the first button, or Close - whichever " +
                "the message will accept - and clears warnings raised " +
                "while a model opens. ERRORS ARE LEFT ALONE: resolving " +
                "one can delete elements, and this add-in reads models " +
                "rather than repairing them.");
            Body(
                "SILENT IS NOT INVISIBLE. Every message answered is " +
                "written to ATKINSMEC_nwc_log.txt in " + LOGS.Folder + ", with " +
                "its text and the button that was pressed, so a run that " +
                "quietly skipped something can still be read afterwards. " +
                "The log also holds every model, what it wrote and what " +
                "went wrong.");
            Body(
                "The bar beside the tick fills one model at a time and " +
                "the line underneath says which one is being worked on. " +
                "A single model can take minutes, so the count is worth " +
                "more than a spinner. Stop still ends the run after the " +
                "model in hand.");

            Heading("The settings that go into the file");
            Body(
                "Shared coordinates is what makes several models line up " +
                "with each other in Navisworks - leave it on unless you " +
                "know why you are turning it off. Include linked models " +
                "puts the links inside each .nwc, which duplicates " +
                "geometry if you are exporting those links separately " +
                "too.");
            Body(
                "Parameters decides how much property data travels: All " +
                "is everything, Elements is less, None is smallest and " +
                "fastest. Faceting controls how finely curves are " +
                "tessellated - higher is smoother and bigger.");

            Heading("Handing the settings round");
            Body(
                "Export settings writes everything on this window except " +
                "the model list into a small .nwcset file, and Import " +
                "settings reads one back. That is how a team agrees one " +
                "way of writing an NWC: somebody sets it up once and " +
                "sends the file round.");
            Body(
                "THE MODEL LIST IS NOT IN IT, deliberately. The list is " +
                "file paths and cloud ids belonging to one machine and " +
                "one job - a shared file carrying those would drop " +
                "somebody else's models into your window. Importing " +
                "leaves your own list exactly as it was.");
            Body(
                "A setting the file does not mention keeps whatever is " +
                "on screen, rather than falling back to a default nobody " +
                "chose. The file is plain text, so it can be read, " +
                "edited, or kept beside a project's standards.");

            Heading("Stopping");
            Body(
                "Stop takes effect BETWEEN models, not in the middle of " +
                "one: Revit decides whether an open or an export may be " +
                "interrupted, and half an .nwc would be worse than " +
                "either finishing or not starting. The table shows what " +
                "was written before it stopped.");

            Heading("What the table says");
            Body(
                "One row per model: whether it was written, how big the " +
                "file is, how long it took, and where it went. A row " +
                "that failed says why in the same column - a missing " +
                "view, a model that would not open, or whatever Revit " +
                "said about it.");

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

            rtbHelp.AppendText(
                text + Environment.NewLine + Environment.NewLine);
        }
    }
}
