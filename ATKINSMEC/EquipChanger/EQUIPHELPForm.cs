using System;
using System.Drawing;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Plain explanation of the equipment changer, shown from the Help
    // button. Built here rather than shipped as a file so it cannot go
    // missing from the add-ins folder, and so it stays next to the code
    // it describes.
    public partial class EQUIPHELPForm : System.Windows.Forms.Form
    {
        public EQUIPHELPForm()
        {
            InitializeComponent();

            FORMSIZE.Remember(this);
        }

        private void EQUIPHELPForm_Load(object sender, EventArgs e)
        {
            rtbHelp.Clear();

            Heading("What it does");
            Body(
                "Changes the TYPE of mechanical equipment or air " +
                "terminals, and brings the ductwork on them with it. A " +
                "fan coil swapped for a bigger one usually has bigger " +
                "connectors, and without this you are left resizing the " +
                "duct, the damper and the bend by hand.");
            Body(
                "Select the units in the model first, then press the " +
                "button. Anything else in the selection - ducts, walls, " +
                "whatever you swept up - is ignored.");

            Heading("How far the resize goes");
            Body(
                "Out of each connector, following the ductwork, until it " +
                "reaches something that ends it. Ducts, elbows, unions " +
                "and in-line accessories like dampers all follow.");
            Body(
                "IT STOPS AT, AND NEVER TOUCHES: a reducer, a tap-off or " +
                "spud, a tee or cross, an air terminal, and any other " +
                "piece of equipment. Each of those is there on purpose. " +
                "A reducer in particular EXISTS because the size changes " +
                "at that point - going past it would undo somebody's " +
                "design, and the whole branch beyond it belongs to the " +
                "main, not to this unit.");
            Body(
                "A fitting whose two ends are different sizes is treated " +
                "as a reducer whatever its family calls itself, and a " +
                "fitting with more than two ends is treated as a tee. " +
                "That way a family that will not say what it is still " +
                "gets the right answer.");
            Body(
                "Press LOOK first. It walks the ductwork and says how " +
                "many elements would follow each connector and where it " +
                "would stop, without changing anything.");

            Heading("The error this avoids");
            Body(
                "Changing a type while the family is still joined to " +
                "ducts it no longer fits makes Revit raise \"The family " +
                "is connected to a network and can no longer keep the " +
                "connectivity\" - an error it labels CANNOT BE IGNORED, " +
                "several at a time, whose only offer is to disconnect the " +
                "family and leave your ductwork hanging.");
            Body(
                "So the joints are taken apart FIRST, deliberately and " +
                "under this add-in's control, then the type is changed, " +
                "then the ducts are resized, then the joints are put back " +
                "and the duct ends moved to wherever the new connectors " +
                "are. Revit is never asked to keep a connection through a " +
                "change it cannot survive.");
            Body(
                "PIPES ON THE UNIT - chilled water, condensate - are taken " +
                "off and put back the same way. They are not resized.");
            Body(
                "Each joint goes back on the SAME connector of the family. " +
                "A bigger unit moves its connectors, so the nearest one " +
                "could be the wrong side; only a change to another family " +
                "matches by system (supply to supply) and then by distance.");

            Heading("What it will not do");
            Body(
                "It will not change a duct's SHAPE. A round connector on " +
                "a rectangular duct needs a different duct type and " +
                "different fittings, so that branch is left alone and " +
                "reported rather than forced.");
            Body(
                "It will not fight a fitting that refuses its new size. " +
                "Many families are driven by a lookup table and will take " +
                "the instruction and quietly go back to what the table " +
                "says, so every one is read back afterwards. One that " +
                "will not change is reported - it is visible on the " +
                "drawing and can be swapped by hand, and replacing it " +
                "here would throw away whatever is written on it.");
            Body(
                "It will not bend a duct or pipe, or stretch one past its " +
                "far end. A run is only slid along its own line, and only " +
                "while some of it is left; one with nothing on its far end " +
                "is moved whole.");
            Body(
                "A BIGGER UNIT MOVES ITS CONNECTIONS. When its main duct " +
                "connection lands to the side of the duct, the UNIT is " +
                "moved sideways until it is back on the duct's line. When " +
                "a pipe connection lands away from the elbow or pipe on " +
                "it, that elbow - and the pipework behind it - is carried " +
                "over, as far as a straight pipe lying the way it moved, " +
                "which stretches to follow. Never another unit, a " +
                "terminal, a tap on a main, or anything pinned.");
            Body(
                "What cannot be reached that way is LEFT OPEN: the unit is " +
                "still changed, the table says \"yes, 1 open\" and names " +
                "the duct or pipe, and you join it by hand. If Revit " +
                "refuses the moves, it is done again moving nothing. " +
                "Ctrl+Z puts everything back.");

            Heading("If something goes wrong");
            Body(
                "EACH ELEMENT IS DONE ON ITS OWN, in its own transaction. " +
                "One that Revit refuses - even at the very end - is put " +
                "back exactly the way it was, and the rest still go " +
                "through - so a single awkward unit does not cost you the " +
                "whole selection.");
            Body(
                "The whole run is one undo step: Ctrl+Z puts everything " +
                "back.");
            Body(
                "Every step is written to ATKINSMEC_equip_log.txt in " +
                LOGS.Folder + " - which connector, how far the walk went, what " +
                "each duct was resized from and to, and what Revit said " +
                "about anything it refused. Read that before guessing.");
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
