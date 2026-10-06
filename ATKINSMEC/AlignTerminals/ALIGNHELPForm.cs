using System;
using System.Drawing;
using System.Windows.Forms;

namespace ATKINSMEC
{
    public partial class ALIGNHELPForm : System.Windows.Forms.Form
    {
        public ALIGNHELPForm()
        {
            InitializeComponent();

            FORMSIZE.Remember(this);
        }

        private void ALIGNHELPForm_Load(object sender, EventArgs e)
        {
            rtbHelp.Clear();

            Heading("What it does");
            Body(
                "Moves the air terminals in this model onto the air " +
                "terminals of a linked architectural model, so the " +
                "diffusers sit exactly where the reflected ceiling plan " +
                "puts them - across the plan, and at the same height.");
            Body(
                "Nothing else is touched. Ducts and flex connections are " +
                "not moved with the terminal, so check the connections " +
                "afterwards where a terminal moved a long way.");

            Heading("1.  Choose the linked models");
            Body(
                "Every loaded link is listed. Ticking one that holds no " +
                "air terminals does no harm; it simply contributes " +
                "nothing.");

            Heading("2.  Which terminals to move");
            Body(
                "Every air terminal in the model, or only ones you pick. " +
                "Picking is the safer way in: try a handful in one room " +
                "first, see what the moves look like, then widen it.");
            Body(
                "If terminals were already selected in Revit when you " +
                "pressed the button, they are picked up automatically and " +
                "the second option is ready to use.");
            Body(
                "\"Pick air terminals in the model\" hands control back " +
                "to Revit so you can select. The window stays open " +
                "throughout. Nothing but air terminals can be picked; " +
                "press Escape to keep the previous selection.");

            Heading("3.  How matching works");
            Body(
                "Each terminal here is paired with the nearest terminal " +
                "in the ticked links, measured on plan only. Height " +
                "is deliberately left out of that search: the elevation " +
                "in the MEP model is usually the thing that is wrong, and " +
                "including it would let a badly levelled terminal match a " +
                "diffuser on the floor above.");
            Body(
                "\"Look no further away than\" caps how far a terminal " +
                "may be from its counterpart and still count as the same " +
                "one. 1500 mm suits a ceiling grid; lower it if the model " +
                "is dense and terminals are pairing with their neighbours.");
            Body(
                "Each architectural terminal is claimed once. Without " +
                "that, two MEP terminals near the same diffuser would " +
                "both be dragged onto it and end up stacked.");
            Body(
                "\"Match against\" narrows what may pair with what. Any " +
                "air terminal is the loosest; the same family, or the " +
                "same family and type, is safer where supply and extract " +
                "diffusers sit close together.");
            Body(
                "Untick \"Take the elevation from the linked terminal\" " +
                "to shift terminals across the plan while leaving their " +
                "heights exactly as they are.");

            Heading("4.  Find matches");
            Body(
                "Reads both models and works out what would move. " +
                "Nothing is changed at this point.");

            Heading("5.  Check before aligning");
            Body(
                "The window is modeless, so the model stays usable while " +
                "it is open. Press Show on any row and that terminal is " +
                "selected and brought into view - the quickest way to " +
                "judge whether a match is right before moving anything.");
            Body(
                "The table lists the largest moves first, because those " +
                "are the ones worth looking at. A move of a metre or more " +
                "usually means the terminal matched the wrong diffuser - " +
                "untick it, or lower the search distance and look again.");
            Body(
                "Rows in red cannot be aligned. The Note says why: either " +
                "nothing was near enough, or the terminal has no " +
                "insertion point to move.");
            Body(
                "Untick anything you would rather leave alone. Selecting " +
                "several rows first makes Tick all and Untick all apply " +
                "to just those.");

            Heading("6.  Align");
            Body(
                "Moves everything ticked, in one go. It is a single undo " +
                "step - one Ctrl+Z puts every terminal back.");

            Heading("Terminals that will not move");
            Body(
                "A terminal hosted on a ceiling takes its height from " +
                "that ceiling, so it can be moved across the plan but not " +
                "up or down. Those are reported as \"aligned in plan; " +
                "elevation held by its host\" rather than counted as a " +
                "success.");
            Body(
                "To change their height, move the ceiling they are hosted " +
                "on, or rehost them. This add-in does not do that: " +
                "rehosting silently changes which room a terminal belongs " +
                "to, which is too much to do to a model without being " +
                "asked.");

            Heading("Worth knowing");
            Body(
                "The linked model is read through its own placement, so a " +
                "link that is rotated or moved from the origin is handled " +
                "correctly.");
            Body(
                "Aligned rows turn green and read \"moved\", so a second " +
                "pass over a long list is easy to follow.");
            Body(
                "Run it again after aligning and the moves should all " +
                "read zero. If they do not, something is pairing " +
                "differently each time, which points to two terminals " +
                "competing for the same diffuser.");

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
