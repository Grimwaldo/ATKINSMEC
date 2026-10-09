using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ventpreview
{
    // Renders the REAL VENTWindow.xaml to a PNG with no Revit present -
    // the WPF version of scratchpad/nwcpreview, and for the same
    // reason: a hand-written window should be looked at before it is
    // shipped, not after somebody presses the button in Revit.
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string folder = args.Length > 0
                ? args[0]
                : Environment.CurrentDirectory;

            var app = new Application();

            var window = new ATKINSMEC.VENTWindow();

            if (args.Length > 1 && args[1] == "tutorial")
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -4000;
                window.Top = -4000;

                window.Show();

                Tutorial.Shoot(window, folder);

                window.Close();
                app.Shutdown();

                Console.WriteLine("done");

                return;
            }

            // Off-screen: it only has to lay out, not be looked at
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -4000;
            window.Top = -4000;

            window.Show();

            window.Fill();

            window.UpdateLayout();

            window.PickRoom();

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent.png"));
            Shoot(window, Path.Combine(folder, "autovent-1.5x.png"), 1.5);

            // The interior placements, found inside the apartments
            window.ScrollLinks();

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent-links-apt.png"));
            Shoot(window, Path.Combine(folder, "autovent-links-apt-1.5x.png"), 1.5);

            // The search box narrowing the list: "tower" leaves the two
            // tower placements and hides the ticked podium model - which
            // the card has to say still counts
            window.SearchLinks("tower");

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent-links-search.png"));
            Shoot(window, Path.Combine(folder, "autovent-links-search-1.5x.png"), 1.5);

            window.SearchLinks("");

            window.UpdateLayout();

            // The worst case for the levels card: a sheet naming ten
            // levels this model has not got
            window.Levels(9, 2, 1, 12, 2, "B1_BASEMENT", "B2_BASEMENT",
                "MEZZANINE", "P5_PODIUM 5", "P6_PODIUM 6", "P7_PODIUM 7",
                "ROOF", "TOP OF PARAPET", "PLANT DECK", "LIFT OVERRUN");

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent-levels-many.png"));

            window.Levels(9, 4, 1, 6, 4, "B1_BASEMENT", "MEZZANINE");

            // The other half of the window
            window.ShowExport();

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent-export.png"));

            Fixtures(folder);

            Interiors(folder);

            window.ShowAssign();

            // And bigger, since it resizes
            window.Width = 1320;
            window.Height = 860;

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent-big.png"));

            window.Close();

            app.Shutdown();

            Console.WriteLine("done");
        }

        // The Fixtures... dialog, as it opens over a job with an
        // architect's model and a plumbing model linked - at 1x and 1.5x
        private static void Fixtures(string folder)
        {
            var sources = new System.Collections.Generic.List<ATKINSMEC.VENTFIXTUREDIALOG.Source>
            {
                new ATKINSMEC.VENTFIXTUREDIALOG.Source { Id = 0, Label = "This model" },
                new ATKINSMEC.VENTFIXTUREDIALOG.Source { Id = 11, Label = "5232535-ATR-POD-ZZZ-3DM-ARC-000010.rvt" },
                new ATKINSMEC.VENTFIXTUREDIALOG.Source { Id = 12, Label = "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 1 : Tower A" },
                new ATKINSMEC.VENTFIXTUREDIALOG.Source { Id = 13, Label = "5232535-ATR-POD-ZZZ-3DM-PLU-000030.rvt" }
            };

            string[] names =
            {
                "M_Water Closet - Flush Valve - Wall Mounted", "M_Urinal - Wall Hung", "M_Lavatory - Vanity",
                "ATK_PL_Accessible WC_Pack", "Sink - Kitchen - Double", "Shower Tray - Square", "Floor Drain - Round"
            };

            int[] counts = { 86, 34, 120, 12, 18, 40, 64 };

            var dialog = new ATKINSMEC.VENTFIXTUREDIALOG(
                sources,
                new System.Collections.Generic.List<long> { 11 },
                ids => names.Select((n, i) => new ATKINSMEC.VENTSETTINGS.FixtureFamily
                {
                    Name = n,
                    Count = counts[i],
                    Counted = ATKINSMEC.VENTSETTINGS.CountsFamily(n)
                }).ToList())
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000
            };

            dialog.Show();
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-fixtures.png"));
            Shoot(dialog, Path.Combine(folder, "autovent-fixtures-1.5x.png"), 1.5);

            dialog.Close();
        }

        // The Interior models... dialog over a tower whose interior model
        // is placed once per floor, plus a second interior model on two
        // floors and an MEP model the keyword passes over - at 1x and 1.5x.
        // Then a choice made by hand: one placement unticked on one level.
        private static void Interiors(string folder)
        {
            const double M = 1 / 0.3048;

            var storeys = new System.Collections.Generic.List<ATKINSMEC.VENTLEVELMAP.Storey>();

            string[] levels = { "GR_GROUND FLOOR", "L01_LEVEL 01", "L02_LEVEL 02", "L03_LEVEL 03", "L04_LEVEL 04", "RF_ROOF" };

            for (int i = 0; i < levels.Length; i++)
                storeys.Add(new ATKINSMEC.VENTLEVELMAP.Storey { Name = levels[i], Elevation = i * 4 * M });

            var links = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, string>>();
            var floors = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<double>>();

            void Placement(long id, string label, params int[] roomsPerStorey)
            {
                links.Add(new System.Collections.Generic.KeyValuePair<long, string>(id, label));

                var its = new System.Collections.Generic.List<double>();

                for (int s = 0; s < roomsPerStorey.Length; s++)
                    for (int r = 0; r < roomsPerStorey[s]; r++)
                        its.Add(s * 4 * M);

                floors[id] = its;
            }

            Placement(11, "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 1 : Tower A", 30, 30, 30, 30, 30);
            Placement(21, "5232535-ATR-TWR-ZZZ-3DM-IND-000030.rvt : 1", 0, 24);
            Placement(22, "5232535-ATR-TWR-ZZZ-3DM-IND-000030.rvt : 2", 0, 0, 24);
            Placement(23, "5232535-ATR-TWR-ZZZ-3DM-IND-000030.rvt : 3", 0, 0, 0, 24);
            Placement(24, "5232535-ATR-TWR-ZZZ-3DM-IND-000030.rvt : 4", 0, 0, 0, 0, 22);
            Placement(31, "5232535-ATR-TWR-ZZZ-3DM-IND-000040.rvt : 1 : Penthouse", 0, 0, 0, 6, 9);
            Placement(41, "5232535-ATR-TWR-ZZZ-3DM-MEP-000050.rvt", 2, 2, 2, 2, 2);

            ATKINSMEC.VENTSETTINGS.InteriorKeyword = "IND";
            ATKINSMEC.VENTSETTINGS.InteriorChosen = false;
            ATKINSMEC.VENTSETTINGS.InteriorPicks.Clear();

            Window Open()
            {
                var dialog = new ATKINSMEC.VENTINTERIORDIALOG(links, floors, storeys, new long[] { 11 })
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -4000,
                    Top = -4000
                };

                dialog.Show();
                dialog.UpdateLayout();

                return dialog;
            }

            Window first = Open();

            Shoot(first, Path.Combine(folder, "autovent-interiors.png"));
            Shoot(first, Path.Combine(folder, "autovent-interiors-1.5x.png"), 1.5);

            first.Close();

            // As it reopens after somebody unticked the penthouse model on L03
            ATKINSMEC.VENTSETTINGS.InteriorChosen = true;
            ATKINSMEC.VENTSETTINGS.InteriorPicks["L01_LEVEL 01"] = new System.Collections.Generic.HashSet<long> { 21 };
            ATKINSMEC.VENTSETTINGS.InteriorPicks["L02_LEVEL 02"] = new System.Collections.Generic.HashSet<long> { 22 };
            ATKINSMEC.VENTSETTINGS.InteriorPicks["L03_LEVEL 03"] = new System.Collections.Generic.HashSet<long> { 23 };
            ATKINSMEC.VENTSETTINGS.InteriorPicks["L04_LEVEL 04"] = new System.Collections.Generic.HashSet<long> { 24, 31 };

            Window chosen = Open();

            Shoot(chosen, Path.Combine(folder, "autovent-interiors-chosen.png"));

            chosen.Close();

            ATKINSMEC.VENTSETTINGS.InteriorKeyword = ATKINSMEC.VENTSETTINGS.DefaultInteriorKeyword;
            ATKINSMEC.VENTSETTINGS.InteriorChosen = false;
            ATKINSMEC.VENTSETTINGS.InteriorPicks.Clear();
        }

        private static void Shoot(Window window, string path, double scale)
        {
            window.Dispatcher.Invoke(
                new Action(delegate { }),
                System.Windows.Threading.DispatcherPriority.ContextIdle);

            var bitmap = new RenderTargetBitmap(
                (int)(window.ActualWidth * scale), (int)(window.ActualHeight * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);

            bitmap.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (FileStream file = File.Create(path))
                encoder.Save(file);
        }

        private static void Shoot(Window window, string path)
        {
            // Dispatcher has to drain or the visual tree is still
            // half-arranged when the bitmap is taken
            window.Dispatcher.Invoke(
                new Action(delegate { }),
                System.Windows.Threading.DispatcherPriority.ContextIdle);

            int width = (int)window.ActualWidth;
            int height = (int)window.ActualHeight;

            if (width <= 0 || height <= 0)
                return;

            var bitmap = new RenderTargetBitmap(
                width, height, 96, 96, PixelFormats.Pbgra32);

            bitmap.Render(window);

            var encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (FileStream file = File.Create(path))
                encoder.Save(file);
        }
    }
}
