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
