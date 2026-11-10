using System;
using System.Drawing;
using System.Windows.Forms;

namespace ATKINSMEC
{
    public partial class QAHELPForm : System.Windows.Forms.Form
    {
        public QAHELPForm()
        {
            InitializeComponent();

            FORMSIZE.Remember(this);
        }

        private void QAHELPForm_Load(object sender, EventArgs e)
        {
            rtbHelp.Clear();

            Heading("What it is for");
            Body(
                "Checking what the sheets actually say before they go out. " +
                "Running the checks only reads, so it is safe to leave " +
                "open while you work. The one thing that writes is " +
                "marking the findings on the drawings, and that undoes " +
                "in one step.");
            Body(
                "The window is five pages, chosen by the tabs along " +
                "the top: Flowrate check, Grids, Duct disconnection " +
                "check, FCU flowrate check and CHW check. Each page is " +
                "a whole job on its own - what to " +
                "check, which checks, and what they found - and they " +
                "never have to agree. Checking flowrate across every " +
                "sheet while the grids are checked on the view open now " +
                "is a perfectly normal thing to do.");
            Body(
                "More checks will be added over time. A new one appears " +
                "on the page its group names, and everything below - the " +
                "list, Show, the export - works for it with no further " +
                "change.");

            Heading("1.  What to check");
            Body(
                "Every sheet is the usual choice before an issue. Picking " +
                "sheets is quicker while you are working on one drawing, " +
                "and the view open now is quickest of all.");
            Body(
                "Only views actually placed on a sheet are looked at. A " +
                "view nobody has put on a sheet is not going out, so it " +
                "is not checked - unless you choose the view open now.");
            Body(
                "This is set per page, so what the flowrate check looks " +
                "at and what the grids check looks at are two separate " +
                "answers.");
            Body(
                "The duct and FCU pages ask a different question, " +
                "because a disconnected duct, or an FCU carrying the " +
                "wrong air, is wrong whether or not anybody has " +
                "put it on a sheet yet. They offer the whole model, or " +
                "levels rather than sheets, or the view open now. " +
                "Anything with no level of its own - a fitting, usually " +
                "- is kept rather than quietly dropped.");

            Heading("2.  Checks to run");
            Body(
                "Tick what you want on this page and press Run. Clicking " +
                "a check shows what it looks for underneath.");
            Body(
                "Each page keeps its own findings, so running one leaves " +
                "the other page's list exactly as it was. The buttons " +
                "along the bottom of the window - Mark, Clear, Export - " +
                "act on the page in front.");

            Heading("Tags reading zero flow");
            Body(
                "Finds air terminal, duct and pipe tags whose element has " +
                "no flow. Those print as 0 on the sheet, which is either " +
                "an unconnected system, a system that was never " +
                "calculated, or a terminal nobody gave a flow to.");
            Body(
                "It works from the tags, not from the elements. An " +
                "untagged terminal with no flow is a modelling matter; a " +
                "tag reading zero is on a drawing going out of the door, " +
                "and that is what this add-in is for.");
            Body(
                "Only tags that SHOW a flow are listed. A tag showing a " +
                "size and a system (\"500x400 EAD\") on a duct with no " +
                "flow is left out. A tag counts as showing a flow when " +
                "its text has a flow unit or a 0 on its own, or when " +
                "most tags of the same type on ducts and pipes that do " +
                "carry flow read that flow. Each row says what the tag " +
                "reads.");
            Body(
                "The same element tagged on two sheets is reported twice, " +
                "on purpose - both sheets are wrong.");
            Body(
                "Tags pointing into a linked model are skipped. That flow " +
                "belongs to the other model and is not yours to fix.");
            Body(
                "Ducts, pipes, fittings and accessories are all included, " +
                "as well as air terminals. Anything with no flow " +
                "parameter at all is not this check's business and is " +
                "passed over.");

            Heading("Grids: grids match the link");
            Body(
                "Compares this model's grids with the model they were " +
                "copied from. Tick that model on the right; usually the " +
                "architectural link.");
            Body(
                "Three things get reported: a grid that has moved from " +
                "where the link has it, a grid here that the link does " +
                "not have, and a grid the link has that never arrived " +
                "here. \"Moved over\" sets how far it may drift first - " +
                "50 mm catches a nudge without complaining about " +
                "rounding.");
            Body(
                "It compares the grid LINE, in plan, not the drawn ends. " +
                "A grid is the same grid however far it is drawn and " +
                "whatever level it sits at - ragged ends are the next " +
                "check's business.");
            Body(
                "These findings belong to the model rather than to one " +
                "sheet, so they sit at the end of the list under no " +
                "sheet.");

            Heading("Grids: grid ends are ragged");
            Body(
                "On a finished drawing every grid running the same way " +
                "should stop on the same line, so the bubbles sit in a " +
                "row. This reports any that fall short.");
            Body(
                "The standard is taken from the grids themselves: the " +
                "furthest any of them reaches in that view is treated as " +
                "the extent that covers the drawing, and anything short " +
                "of it by more than \"Short over\" is reported, saying " +
                "which end and by how much.");
            Body(
                "Vertical and horizontal grids are judged separately - a " +
                "vertical grid ending short of a horizontal one means " +
                "nothing. Only plan views are looked at.");

            Heading("Grids: grid bubbles");
            Body(
                "Which end the bubbles belong on is a drawing " +
                "convention, not something Revit enforces, so choose the " +
                "corner on the right: top or bottom for the grids running " +
                "up the sheet, left or right for those running across.");
            Body(
                "Reported are grids with the bubble on the wrong end, " +
                "grids showing one at both ends, and grids showing none " +
                "at all.");
            Body(
                "Top, bottom, left and right are read in the VIEW's own " +
                "frame, so a rotated plan is judged the way it is " +
                "actually drawn rather than by north. A grid running " +
                "diagonally has no top or left end to speak of and is " +
                "passed over.");

            Heading("Grids: putting them right");
            Body(
                "Put the grids right, on the Grids tab, fixes what the " +
                "three grid checks found. It is the only thing in this " +
                "add-in that changes the drawing itself, so it asks " +
                "first and says exactly what it is about to do.");
            Body(
                "Ragged ends are stretched out to the furthest grid in " +
                "that view - along the grid's own line, so nothing shifts " +
                "sideways. Bubbles are shown on the corner you chose and " +
                "hidden on the other. Grids that drifted are moved back " +
                "onto the line the link has, sideways only, never up or " +
                "down a level.");
            Body(
                "Pinned grids are left alone and said so. So is a grid " +
                "the link does not have, and one the link has that is " +
                "missing here: drawing a grid and deleting a grid are not " +
                "decisions for an add-in.");
            Body(
                "Pick out rows first to fix only those; with nothing " +
                "picked out it does the whole list. Everything is worked " +
                "out again from the model at the moment of fixing, so a " +
                "list left open while somebody else moved a grid cannot " +
                "apply a stale correction.");
            Body(
                "It is one undo step. Ctrl+Z puts the lot back. The " +
                "checks run again straight afterwards, so what is left in " +
                "the list is what is still wrong.");

            Heading("Ducts: what counts as disconnected");
            Body(
                "An end connector in the ductwork with nothing on the " +
                "other side. That is what Revit itself calls a " +
                "disconnect, and it is what leaves a takeoff hanging off " +
                "nothing, a terminal with no air, and a system that will " +
                "not calculate.");
            Body(
                "Ducts, flex, fittings, accessories, air terminals and " +
                "mechanical equipment are all walked. The four checks " +
                "then divide the open ends between them, so nothing is " +
                "reported twice.");

            Heading("Ducts: taps and takeoffs left hanging");
            Body(
                "Tap and spud fittings with an open end - the duct they " +
                "were tapped into was moved or deleted and the tap stayed " +
                "behind. This is the one the page is named for.");

            Heading("Ducts: branches never tapped into a duct");
            Body(
                "Terminals, flex, branches and equipment with an open end " +
                "that has a duct within reach. The connection was drawn " +
                "and never made. \"Look for a duct within\" sets how far " +
                "counts as within reach.");

            Heading("Ducts: ends that meet but are not joined");
            Body(
                "Two open ends sitting on the same spot and facing each " +
                "other. It looks connected on the drawing and is not. " +
                "Facing each other is part of the test, so two ends that " +
                "merely pass close by are left alone.");

            Heading("Ducts: open ends with nothing near them");
            Body(
                "Everything else - an open end with no duct within reach. " +
                "Very often a stub left on purpose, which is why these " +
                "are reported as warnings and never put right.");

            Heading("Ducts: joining them up");
            Body(
                "Join them up, on the duct page, is the second thing in " +
                "this add-in that changes the model, and the only one " +
                "that adds and removes elements. It asks first and says " +
                "exactly what it is about to do.");
            Body(
                "An end that belongs to a system is joined into THAT " +
                "system: the nearest duct carrying it. An end with no " +
                "system of its own goes to the nearest duct, whatever " +
                "system that is. If an end has a system but none of its " +
                "ducts are within reach, nothing happens - unless you " +
                "tick the box that lets it fall back to the nearest duct " +
                "of any system.");
            Body(
                "Two ends that meet are simply joined to each other. An " +
                "open end near a duct is tapped into it, and Revit makes " +
                "the takeoff fitting itself. A hanging tap is REPLACED " +
                "rather than repaired - the old fitting is deleted and a " +
                "fresh takeoff made onto the duct - because a tap's main " +
                "side has no connector to rejoin: it is hosted on the " +
                "body of the duct.");
            Body(
                "Every single change sits in its own step, so one that " +
                "Revit refuses rolls back by itself and the rest still go " +
                "through. The whole lot is still one undo. Nothing is " +
                "ever half done.");
            Body(
                "As with the grids, everything is worked out again from " +
                "the model at the moment of joining. An end somebody has " +
                "connected since the check ran is left alone and counted " +
                "separately, not connected twice.");
            Body(
                "A tap with nothing connected on either side is left for " +
                "you. So is anything with no duct within reach. Deciding " +
                "that a spare fitting is rubbish is not an add-in's job.");

            Heading("FCUs: what it compares");
            Body(
                "Load the FCU schedule with Browse, pick the worksheet, " +
                "and the add-in reads the unit names and their flow " +
                "rates out of it. The headings are found by what they " +
                "say rather than by where they are, so a schedule laid " +
                "out differently still reads - it needs a column naming " +
                "the FCU and a column giving the flow.");
            Body(
                "Each fan coil unit in the model is then matched to a row " +
                "by name, and the duct leaving its supply connector is " +
                "compared with what the schedule says it should carry.");
            Body(
                "\"FCU number is in\" says which parameter on the " +
                "equipment holds the unit's name - Mark, the type name, " +
                "or any text parameter your families carry. Matching " +
                "ignores spaces, dashes and case, so FCU-02, FCU -02 and " +
                "fcu_02 are the same unit. The digits are not ignored: " +
                "FCU-2 and FCU-02 stay different.");
            Body(
                "Out by more than the percentage OR the litres, whichever " +
                "is larger, counts as wrong. The percentage stops small " +
                "units being judged too harshly and the litres stop large " +
                "ones being judged too finely.");
            Body(
                "Free discharge units are noted and passed over - they " +
                "blow straight into the room, so there is no supply duct " +
                "on them to check.");

            Heading("FCUs: correcting the airflows");
            Body(
                "Correct the airflows writes the AIR TERMINALS, not the " +
                "duct. Revit works a duct's flow out from the terminals " +
                "it feeds, so putting the terminals right is what puts " +
                "the duct right - writing the duct directly is not " +
                "possible and would not hold anyway.");
            Body(
                "Each unit's terminals are scaled by ONE ratio, so they " +
                "keep their shares. Four terminals reading 40/40/40/40 " +
                "against a schedule of 200 all become 50. Reading " +
                "10/10/10/130 they become 12.5/12.5/12.5/162.5 - whoever " +
                "balanced that branch had a reason, and a ratio keeps it " +
                "where an even split would throw it away.");
            Body(
                "A unit with no terminals downstream, or whose terminals " +
                "all read zero, is left alone: there is no ratio to scale " +
                "by, and inventing one would be guessing.");
            Body(
                "Where a family has MORE THAN ONE duct connector it does " +
                "not offer a single writable Flow at all - in Revit the " +
                "air belongs to a connector, not to the element. The " +
                "add-in handles that: it finds the connector actually " +
                "carrying the air, which is the connected one where only " +
                "one of two is used, and assigns the flow there instead.");
            Body(
                "\"N of M terminal(s) cannot be written\" means neither " +
                "way was open, and the finding says which reason applies " +
                "to which terminal, with element ids you can paste into " +
                "Manage, Select by ID. Three things cause it:");
            Body(
                "   1.  The connector's Flow Configuration is Calculated " +
                "or System rather than Preset. Only Preset can be given " +
                "a flow - the other two mean Revit works it out - and it " +
                "is set in the family, not the project. This is the " +
                "commonest cause, and the finding names it.");
            Body(
                "   2.  The family drives Flow from a formula or an " +
                "associated family parameter, which locks it in every " +
                "project it is used in.");
            Body(
                "   3.  In a workshared model, the element belongs to " +
                "somebody else. Borrow it or have them relinquish, and " +
                "run the check again.");
            Body(
                "The terminals are found by walking out of the unit's " +
                "supply connector and following the ductwork, stopping at " +
                "each terminal and at any other piece of equipment, so " +
                "one unit never corrects another's branch.");
            Body(
                "As everywhere else here, it is worked out again from the " +
                "model at the moment of correcting, each unit is its own " +
                "step so one that will not take leaves the rest alone, " +
                "and the whole lot is one undo.");

            Heading("CHW: pipe size against flow");
            Body(
                "The sizing table is BUILT IN - an ASHRAE " +
                "90.1 sheet for ASTM A53, seventeen sizes from DN20 to " +
                "DN600. There is nothing to load and nowhere for it to " +
                "go missing, and every machine checks against the same " +
                "numbers.");
            Body(
                "All you say is which of the model's piping system types " +
                "are the chilled water SUPPLY and which are the RETURN. " +
                "Both lists offer every system type in the model, and " +
                "more than one may be ticked on either side - a job with " +
                "primary and secondary circuits has several.");
            Body(
                "A guess is pre-ticked where the system type's NAME says " +
                "both chilled water and which side it is. A name that " +
                "only says chilled water, with no side to it, is left " +
                "for you to place - guessing there would be worse than " +
                "asking.");
            Body(
                "The two sides carry the same flow, so the sizing itself " +
                "is identical either way. Keeping them apart is so the " +
                "findings say which side each one is on, and so you can " +
                "check one side without the other by leaving the other " +
                "list empty.");
            Body(
                "Each size in the table carries the greatest flow it is " +
                "allowed to take, in litres per second. Below DN65 that " +
                "is a 1.2 m/s speed cap trimmed to hold 250 Pa/m; from " +
                "DN65 up it is ASHRAE 90.1's own flow, likewise trimmed; " +
                "from DN350 it is 90.1's 2.286 m/s (7.5 ft/s) cap.");
            Body(
                "A pipe is checked as one thing: its own size against " +
                "its own flow. A fitting or accessory is checked at each " +
                "CONNECTOR, because a tee has three sizes and three " +
                "flows and judging it as one would mean nothing. Only " +
                "round connectors are judged - the table is about DN.");
            Body(
                "\"Pipe too small\" is the one that matters: the flow " +
                "exceeds what that size allows, so the velocity or the " +
                "pressure drop is over. The finding says what it carries, " +
                "what its size allows, and which size it needs.");
            Body(
                "\"Pipe larger than the flow needs\" is the other " +
                "direction, and noisy by nature - a branch at its " +
                "practical minimum size is oversized for its flow almost " +
                "everywhere. It only reports what is clear of the two " +
                "settings on the left: how many sizes over, and from " +
                "what size upwards to bother.");
            Body(
                "A size the table has never heard of - a 125 mm pipe " +
                "against a table that jumps 100 to 150 - is reported " +
                "ONCE with a count, rather than once per pipe.");
            Body(
                "Press Show on any row to find that pipe in the model.");

            Heading("CHW: clearing the Additional Flow");
            Body(
                "Additional Flow is Revit's way of adding flow to a pipe " +
                "segment on top of what the system works out. It is " +
                "useful while sizing by hand and a menace afterwards: it " +
                "does not show on the drawing, it survives every later " +
                "change, and it inflates what the pipe reports carrying " +
                "- so the size check reads a flow the system does not " +
                "actually produce and calls a perfectly good pipe " +
                "undersized.");
            Body(
                "Clear the Additional Flow off these pipes sets it to " +
                "zero. It counts first and asks second, telling you how " +
                "many pipes carry it and how much there is in total, so " +
                "there is no pressing it blind.");
            Body(
                "It works on the same pipes the checks look at: the " +
                "system types ticked above, inside whatever scope is set " +
                "at the top - the whole model, or only the levels " +
                "ticked, or the view open now. To do one floor, tick " +
                "that level and press it.");
            Body(
                "Only chilled water pipe is touched. A pipe in a system " +
                "you have not ticked is left alone, whatever it carries. " +
                "It is one undo step, and the checks run again straight " +
                "afterwards because the sizes are now judged against " +
                "what the system really produces.");

            Heading("CHW: resizing the pipes");
            Body(
                "Resize the pipes sets each listed pipe to the smallest " +
                "size its flow allows, straight from the table. It asks " +
                "first, and lists how many pipes are going to each size " +
                "so the scale of it is clear before you agree.");
            Body(
                "Only the PIPES are written. A fitting takes its size " +
                "from the pipes it joins - that is how Revit works - so " +
                "the pipes are set, the model regenerates, and the " +
                "fittings follow. Anything that did not follow shows up " +
                "when the checks run again straight afterwards.");
            Body(
                "Worth being plain about what this is: changing a pipe " +
                "size changes the hydraulics of everything downstream of " +
                "it - the balance, the pump head, the valve authority. " +
                "It puts the drawing in step with the table it was " +
                "given. It does not redesign the system, and what it " +
                "writes still wants an engineer's eye.");
            Body(
                "WHOLE RUNS move together, and this is the main thing " +
                "that stops the model coming apart. A pipe joined to " +
                "another through a plain elbow or union is the same " +
                "length of pipe and wants the same size; resizing one of " +
                "them on its own is what puts a reducer in the middle of " +
                "a straight run and asks a joint to survive a change its " +
                "neighbour did not make. So more pipes may change than " +
                "the ones listed, and the question says so before you " +
                "agree.");
            Body(
                "A run stops where the pipework genuinely changes: at a " +
                "tee or a cross, where the flow divides; at a reducer, " +
                "which is there on purpose; and at equipment, whose own " +
                "size cannot follow. A valve in line IS crossed - the " +
                "pipe either side of it is one length - but it is noted, " +
                "because the valve's own size may not follow it.");
            Body(
                "THE PIPE TYPE IS ASKED FIRST. Every pipe type carries " +
                "the sizes it can be made in, in its routing preferences. " +
                "A size that is not on that list has no segment behind it " +
                "and no fitting to join it with, and asking for it is the " +
                "commonest way a resize fails. The run is refused with " +
                "the reason - naming the size and the type - rather than " +
                "attempted and broken.");
            Body(
                "THE JOINTS ARE WRITTEN DOWN. Every joint these pipes " +
                "have is recorded before anything moves and checked " +
                "afterwards. Anything that came apart is joined back up: " +
                "directly where the two ends now match, and with a " +
                "reducer where they do not. The message says how many " +
                "were put back, and warns plainly if any could not be.");
            Body(
                "PICK ROWS TO DO ONLY THOSE. Select one or more rows in " +
                "the list and the buttons act on just those; select " +
                "nothing and they act on the whole list. The question " +
                "says which before anything happens, so it is never a " +
                "surprise. Picking a single row used to be ignored and " +
                "the whole model done instead - that is fixed, and one " +
                "row now means one row.");
            Body(
                "A BIGGER FITTING NEEDS MORE ROOM, and this is what " +
                "deletes pipes. A DN20 elbow turns the corner in a few " +
                "millimetres; a DN80 one needs far more. A short jog " +
                "between two elbows gets eaten from both ends at once, " +
                "and when there is nothing left Revit deletes it and " +
                "says \"Line is too short\".");
            Body(
                "So the room is worked out BEFORE anything is written, " +
                "from the fittings already there: measure what each one " +
                "takes up now, scale it by how much the size is growing. " +
                "Where a pipe would be left too short, the FITTING ON " +
                "ITS END IS SLID AWAY along that pipe's own line, so the " +
                "pipe gets longer instead of shorter - which is what you " +
                "would do by dragging it. The fitting is unhooked, moved " +
                "and hooked back on. It is never deleted, so it keeps " +
                "whatever is written on it.");
            Body(
                "The length it takes has to come from somewhere, and " +
                "there are only two honest answers. Usually the first " +
                "pipe IN LINE with the short one gives it up by being " +
                "that much shorter, and nothing past that point moves at " +
                "all. Where there is no such pipe, a DEAD-END BRANCH can " +
                "be pushed along bodily instead, which moves nothing but " +
                "itself.");
            Body(
                "A TEE SLIDES TOO, which is the case that matters most " +
                "on a main: a short piece between two tees, with a drop " +
                "off each. The tee moves along the main, the short pipe " +
                "grows, the main gives up the same length, and the drop " +
                "comes along with it. That only works if the drop is a " +
                "DEAD END - a drop that feeds an FCU or a pump cannot " +
                "move, because the unit stays where it was put.");
            Body(
                "Which pipes are at risk is worked out from the FITTINGS " +
                "rather than from the pipes being resized, and that " +
                "matters more than it sounds. A run stops at a tee, so a " +
                "short piece between two tees is a run of its own and may " +
                "not be changing size at all - but sizing the main up " +
                "grows both tees, and they eat it from both ends. Looking " +
                "only at the pipes being resized never sees it coming.");
            Body(
                "Afterwards the model is asked rather than trusted: " +
                "every joint that existed before must still exist, and " +
                "nothing may have been left too short. If either is not " +
                "so, the whole thing goes back as though it never " +
                "happened - so this cannot trade one \"Line is too " +
                "short\" for another, or leave something disconnected.");
            Body(
                "EQUIPMENT IS NEVER MOVED. A pipe running into a WSHP, " +
                "a pump or a chiller is held at that end, so the room " +
                "is taken from the OTHER end: the fitting there is slid " +
                "away, and the length comes from the first pipe in line " +
                "further along - which may itself run into equipment, " +
                "because only its near end moves and its far end stays " +
                "put. The walk will cross a tee on the way if what " +
                "hangs off that tee is a dead end that can slide along " +
                "with it.");
            Body(
                "It is refused, and that whole run left exactly as it " +
                "was, where the room has nowhere to come from: a tee " +
                "that would drag its branch, a pump or other equipment " +
                "that stays where it was put, a ring that would be " +
                "pulled apart from the far side, a giver that would " +
                "itself end up too short, or more than a dozen elements " +
                "that would have to be pushed along. The message names " +
                "the pipe and says how long it is, because a pipe that " +
                "short usually wants a route change rather than a " +
                "bigger size.");
            Body(
                "Untick \"Slide fittings along to keep pipes Revit would " +
                "delete\" and none of that is attempted: a run with a " +
                "pipe that cannot survive is simply refused. Either way " +
                "nothing is deleted - the difference is only whether the " +
                "add-in tries to save the pipe or leaves it to you.");
            Body(
                "THE FITTINGS ARE THEN MADE TO MATCH, and they have to " +
                "be driven - they do not follow on their own. Changing " +
                "a pipe size on the drawing resizes the elbow next to " +
                "it; changing it through the API does not, which was " +
                "reported from a real model where the pipes resized and " +
                "the fittings stayed as they were. So each fitting is " +
                "set to the size of the pipes on its ends - a tee of " +
                "three sizes stays a tee of three sizes.");
            Body(
                "Most fittings cannot simply be told a size: they are " +
                "driven by a lookup table, and will either refuse the " +
                "number or take it and go straight back. One that will " +
                "not take it is REPLACED - the old one is deleted and " +
                "Revit makes a fresh elbow, tee, cross, reducer or " +
                "union, which comes in at the size of the pipes it is " +
                "given. Anything written on that particular fitting goes " +
                "with it, which is why the question says so first.");
            Body(
                "THE VIEW OPEN NOW means what Revit means by it: the " +
                "pipes that view actually shows, with its crop region, " +
                "view range, hidden elements and view filters all having " +
                "their say. It used to be ignored on this page and the " +
                "whole project was checked instead. If a SHEET is the " +
                "open view it still checks the whole model, because no " +
                "pipe is in a sheet - a sheet holds viewports - and an " +
                "empty list would be a worse answer than a complete one.");
            Body(
                "A REDUCER IS NEVER LISTED. It exists precisely because " +
                "the size changes across it, so its small end always " +
                "carries the flow of its big end and always reads as " +
                "undersized - it would be a false finding on every " +
                "single one. Reducers are still put right: the fix " +
                "reaches them from the pipes they join and gives them " +
                "those sizes. They just do not clutter the list.");
            Body(
                "AN EQUAL FITTING TAKES THE LARGEST PIPE ON IT. Setting " +
                "each end to the pipe on its own side is right for a " +
                "reducing tee, but many families have ONE nominal size " +
                "driving every end - an equal tee cannot be a reducing " +
                "tee - and no per-connector size can satisfy those. They " +
                "are given the size of the biggest pipe joined to them, " +
                "which is what the fitting physically is; the reduction " +
                "then belongs to the branch pipe, where you can see it.");
            Body(
                "A VALVE IS NEVER REPLACED. Its size is usually a choice " +
                "of family type rather than a number, and picking a " +
                "different valve is not a decision for an add-in. It is " +
                "offered the size, and if it will not take it it stays " +
                "as it is and stays on the list.");
            Body(
                "THE FITTINGS ARE THEN MADE TO MATCH, and they have to " +
                "be driven - they do not follow on their own. Changing " +
                "a pipe size on the drawing resizes the elbow next to " +
                "it; changing it through the API does not, which was " +
                "reported from a real model where the pipes resized and " +
                "the fittings stayed as they were. So each fitting is " +
                "set to the size of the pipes on its ends - a tee of " +
                "three sizes stays a tee of three sizes.");
            Body(
                "Most fittings cannot simply be told a size: they are " +
                "driven by a lookup table, and will either refuse the " +
                "number or take it and go straight back. One that will " +
                "not take it is REPLACED - the old one is deleted and " +
                "Revit makes a fresh elbow, tee, cross, reducer or " +
                "union, which comes in at the size of the pipes it is " +
                "given. Anything written on that particular fitting goes " +
                "with it, which is why the question says so first.");
            Body(
                "A VALVE IS NEVER REPLACED. Its size is usually a choice " +
                "of family type rather than a number, and picking a " +
                "different valve is not a decision for an add-in. It is " +
                "offered the size, and if it will not take it it stays " +
                "as it is and stays on the list.");
            Body(
                "Runs are done biggest first, so a main grows before its " +
                "branches. Each run is its own step, so one Revit refuses " +
                "rolls back alone and the rest still go through. A run " +
                "carrying more than the largest size in the table has " +
                "nowhere to go and is left alone. The whole lot is one " +
                "undo.");

            Heading("3.  Reading the results");
            Body(
                "Findings are grouped by sheet, one drawing at a time, " +
                "which is how a QA list is usually worked through. " +
                "Alternate sheets are shaded lightly so you can see where " +
                "one ends and the next begins. Within a sheet the worst " +
                "come first.");
            Body(
                "Sheet numbers sort the way you read them, so M-9 comes " +
                "before M-10 rather than after it. Anything with no sheet " +
                "at all is listed at the end.");
            Body(
                "The line under the list names the sheets carrying the " +
                "most, which is usually where to start.");
            Body(
                "On the duct page the Sheet and View columns carry the " +
                "system and the kind of element instead, and on the FCU " +
                "page the unit and what the schedule says for it, since " +
                "neither belongs to any one sheet.");
            Body(
                "That is also why the marking buttons go grey on that " +
                "page: colouring something red means colouring it in a " +
                "view, and these findings have no view to be coloured in. " +
                "Show still works - it finds the element in the model.");
            Body(
                "Press Show on any row and that tag is selected and " +
                "brought into view.");
            Body(
                "Columns can be dragged into whatever order suits you, " +
                "and the whole list can be sent to Excel with the filter " +
                "row already switched on - handy for an issue register.");

            Heading("Marking them on the drawings");
            Body(
                "\"Mark tags now\" colours the offending " +
                "tags red - the tag itself, not the duct or terminal " +
                "behind it - each in the view it was found in, so they " +
                "can " +
                "be seen while panning around the sheet rather than only " +
                "read in the list. Select some rows first to mark just " +
                "those.");
            Body(
                "It uses a graphic override, so nothing is added to the " +
                "model - no clouds, no lines, nothing to hunt down " +
                "afterwards. Clearing puts the view back exactly as it " +
                "was.");
            Body(
                "But the colour IS saved with the model, like any " +
                "override. It is not a temporary view mode and it does " +
                "not go away when you close the window. Press \"Clear " +
                "marks\" when you are done, or Ctrl+Z straight after " +
                "marking.");
            Body(
                "Clear removes everything marked in this session, and " +
                "everything currently in the list. If you mark, close " +
                "Revit, and come back, run the same check again before " +
                "clearing so the add-in knows what to clear.");
            Body(
                "Running the checks never changes the model. Marking, " +
                "clearing and the markers are the only things here that " +
                "do, and each is a single undo step.");

            Heading("Red markers, live, touching nothing");
            Body(
                "The tick box at the bottom floats a red marker over " +
                "every finding, and keeps them up to date as you work - " +
                "a marker vanishes as soon as its flow is filled in. It " +
                "is the least intrusive of the three: it puts nothing on " +
                "the elements and adds no filters, so a view template " +
                "cannot fight it and there is nothing to unpick " +
                "afterwards.");
            Body(
                "It uses Revit's analysis display, the same machinery " +
                "that draws heat maps. The markers sit on top of the " +
                "drawing rather than restyling it, so the sheet still " +
                "reads the way it will print.");
            Body(
                "Unlike the filters, this one does need the add-in " +
                "running: the window watches the model and repaints. " +
                "Close the window and the watching stops. The markers " +
                "left behind are cleared by turning the tick box on and " +
                "off again, or they simply go when the view is next " +
                "repainted by the add-in.");
            Body(
                "Repainting only happens when something that could " +
                "change a flow is edited - a terminal, a duct, a pipe, a " +
                "fitting, a tag or a system. Moving a wall does not set " +
                "it off.");
            Body(
                "Which of the two to use: markers while fixing things, " +
                "because they are live and leave no trace; Mark tags now " +
                "when the colour should stay in the model for other " +
                "people to see.");

            Heading("If a check itself fails");
            Body(
                "It is reported as a warning row saying so, and the other " +
                "checks still run. One broken check does not cost you the " +
                "rest of the results.");

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
