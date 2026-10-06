using System;
using System.Drawing;
using System.Windows.Forms;

namespace nwcpreview
{
    internal static class Program
    {
        private static string _folder;

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _folder = args.Length > 0
                ? args[0]
                : Environment.CurrentDirectory;

            using (var form = new ATKINSMEC.NWCForm())
            {
                form.Show();
                form.Fill();

                Shoot(form, "nwc.png");

                // The whole point of the anchors: a bigger window has
                // to give the space to the grids, not to grey
                Grow(form, 300, 260);

                Shoot(form, "nwc-big.png");
            }

            using (var help = new ATKINSMEC.NWCHELPForm())
            {
                help.Show();

                Shoot(help, "nwchelp.png");
            }

            var window = new ATKINSMEC.NWCSETTINGS
            {
                SharedCoordinates = true,
                DivideIntoLevels = true,
                ExportElementIds = true,
                ExportRoomAsAttribute = true,
                ConvertElementProperties = true,
                Parameters = 0,
                Worksets = 0,
                FacetingFactor = 1.0,
                Prefix = string.Empty,
                Suffix = "-NWC"
            };

            using (var model = new ATKINSMEC.NWCMODELForm(
                       "1234-ATK-ZZ-XX-M3-E-0001", window, null))
            {
                model.Show();

                Shoot(model, "nwcmodel.png");
            }

            var own = new ATKINSMEC.NWCSETTINGS
            {
                ExportLinks = true,
                ExportParts = true,
                Parameters = 1,
                Worksets = 1,
                FacetingFactor = 2.5,
                Prefix = "MEP-",
                Suffix = string.Empty
            };

            using (var model = new ATKINSMEC.NWCMODELForm(
                       "1234-ATK-ZZ-XX-M3-E-0001", window, own))
            {
                model.Show();

                Shoot(model, "nwcmodel-own.png");
            }

            // ---- the new workset picker ----

            var available = new System.Collections.Generic.List<string>
            {
                "Workset1", "Shared Levels and Grids",
                "MEP - Ductwork", "MEP - Pipework",
                "MEP - Equipment", "MEP - Containment",
                "Links - Architecture", "Links - Structure"
            };

            var picked = new System.Collections.Generic.List<string>
            {
                "MEP - Ductwork", "MEP - Pipework",
                "MEP - Equipment", "Fire - Sprinklers"
            };

            using (var ws = new ATKINSMEC.NWCWORKSETForm(
                       "1234-ATK-ZZ-XX-M3-E-0001", available,
                       string.Empty, picked))
            {
                ws.Show();

                Shoot(ws, "nwcworksets.png");
            }

            // A model that is not workshared at all
            using (var ws = new ATKINSMEC.NWCWORKSETForm(
                       "1234-ATK-ZZ-XX-M3-A-0001",
                       new System.Collections.Generic.List<string>(),
                       string.Empty,
                       new System.Collections.Generic.List<string>()))
            {
                ws.Show();

                Shoot(ws, "nwcworksets-none.png");
            }

            Console.WriteLine("done");
        }

        private static void Grow(Form form, int wider, int taller)
        {
            form.Size = new Size(
                form.Width + wider, form.Height + taller);

            form.Refresh();

            Application.DoEvents();
        }

        private static void Shoot(Form form, string name)
        {
            form.Refresh();

            Application.DoEvents();

            using (var shot = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(shot,
                    new Rectangle(0, 0, form.Width, form.Height));

                shot.Save(System.IO.Path.Combine(_folder, name));
            }
        }
    }
}
