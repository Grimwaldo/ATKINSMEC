using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using System.Reflection;
using Autodesk.Revit.DB;
using System.Windows.Forms;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;

namespace ATKINSMEC
{
    public class ExtEventHan : IExternalEventHandler
    {
        // 1 = write the airflows, 2 = export the rooms, 3 = show a room
        public int Request { get; set; }

        // Request 3: which rooms ({ link id, room id } each), the level of
        // THIS model they were put under, and how many picked rooms on
        // other levels could not go in the same plan - set by the window
        // before it raises
        public List<long[]> ShowRooms { get; set; }
        public string ShowLevel { get; set; }
        public int ShowElsewhere { get; set; }

        // WHAT THE WINDOW WANTS READ (VENTREAD) - kept apart from Request,
        // because a read and a run can both be waiting: a tick asks for a
        // link's rooms and Run is pressed before Revit gets round to it.
        // Asks raised before the event runs are merged into one.
        private VENTREAD.Ask _reads;

        public void Read(VENTREAD.Ask ask)
        {
            if (ask == null || !ask.Any)
                return;

            if (_reads == null)
                _reads = new VENTREAD.Ask();

            _reads.Add(ask);

            Command.ExtEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            // TAKEN AND CLEARED AT ONCE. Request was never reset, so any
            // later Raise - a read, say - would have run the last write
            // or export again
            int request = Request;
            Request = 0;

            VENTREAD.Ask reads = _reads;
            _reads = null;

            // From the model the WINDOW was opened on - not the one in
            // front, and not Command.Doc, which the button resets even
            // when it only brings the open window forward
            if (reads != null && Command.Window != null)
            {
                VENTREAD.Answer answer = VENTREAD.Do(Command.Window.Document, reads);

                // Revit's own thread is the window's thread too
                Command.Window.Take(answer);
            }

            if (request == 0)
                return;

            Document doc =
                app.ActiveUIDocument.Document;

            if (request == 1)
            {
                REVITROOMS rooms = new REVITROOMS();
                rooms.Rooms(doc);
            }
            else if (request == 2)
            {
                REVITEXPORT export = new REVITEXPORT();
                export.Export(doc);
            }
            else if (request == 3)
            {
                string said;

                try
                {
                    said = VENTSHOW.Show(app, ShowRooms, ShowLevel, ShowElsewhere);
                }
                catch (Exception ex)
                {
                    said = "Could not show the room: " + ex.Message;
                }

                // This runs on Revit's own thread, which is the window's
                // thread too - so the window can be told directly
                if (Command.Window != null)
                    Command.Window.Shown(said);
            }
        }

        public string GetName()
        {
            return "ATKINS MEP";
        }
    }
}
