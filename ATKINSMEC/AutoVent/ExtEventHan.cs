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

        public void Execute(UIApplication app)
        {
            Document doc =
                app.ActiveUIDocument.Document;

            if (Request == 1)
            {
                REVITROOMS rooms = new REVITROOMS();
                rooms.Rooms(doc);
            }
            else if (Request == 2)
            {
                REVITEXPORT export = new REVITEXPORT();
                export.Export(doc);
            }
            else if (Request == 3)
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
