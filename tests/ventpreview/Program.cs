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

            // The levels search: "bed room" - the rooms found under their
            // levels, every level with one opened, the rest hidden
            window.SearchLevels("bed room");

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent-levels-search.png"));
            Shoot(window, Path.Combine(folder, "autovent-levels-search-1.5x.png"), 1.5);

            window.SearchLevels("");

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
            Shoot(window, Path.Combine(folder, "autovent-export-1.5x.png"), 1.5);

            Fixtures(folder);

            SystemTypes(folder);

            Interiors(folder);

            Arrange(folder);

            window.ShowAssign();

            // And bigger, since it resizes
            window.Width = 1320;
            window.Height = 860;

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent-big.png"));

            // A 1080p laptop at 150%: about 690 above the taskbar - the body
            // scrolls, at the top and at the bottom
            window.Width = 1060;
            window.Height = 690;

            window.UpdateLayout();

            Shoot(window, Path.Combine(folder, "autovent-short.png"));
            Shoot(window, Path.Combine(folder, "autovent-short-1.5x.png"), 1.5);

            var body = window.FindName("BodyScroll") as System.Windows.Controls.ScrollViewer;

            if (body != null)
            {
                body.ScrollToBottom();
                window.UpdateLayout();
            }

            Shoot(window, Path.Combine(folder, "autovent-short-bottom.png"));

            window.Close();

            app.Shutdown();

            Console.WriteLine("done");
        }

        // The Fixtures... dialog, as it opens over a job with an
        // architect's model and a plumbing model linked - at 1x and 1.5x
        // The system types dialog behind the assign tab's two buttons:
        // twenty types, as the user's model has, then searched
        private static void SystemTypes(string folder)
        {
            string[] names =
            {
                "ATK_ME_Fresh Air", "ATK_ME_Extract", "ATK_ME_Supply Air", "ATK_ME_Return Air",
                "ATK_ME_Toilet Exhaust", "ATK_ME_Kitchen Exhaust", "ATK_ME_Transfer Air",
                "ATK_ME_Car Park Exhaust", "ATK_ME_Car Park Makeup Air", "ATK_ME_Battery Exhaust",
                "ATK_ME_Smoke Exhaust", "ATK_ME_Stair Pressurisation", "ATK_ME_Lift Pressurisation",
                "ATK_ME_OA Supply", "ATK_ME_General Extract", "ATK_ME_Kitchen Makeup Air",
                "ATK_ME_Relief Air", "ATK_ME_Plant Room Ventilation", "ATK_ME_Generator Exhaust",
                "ATK_ME_Bin Store Extract"
            };

            var dialog = new ATKINSMEC.VENTSYSTEMDIALOG(
                "Extract",
                names.Select(n => new ATKINSMEC.VENTSYSTEMDIALOG.Item
                {
                    Name = n,
                    On = ATKINSMEC.VENTSETTINGS.LooksExtract(n)
                }),
                "Extract or Exhaust")
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000
            };

            dialog.Show();
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-systems.png"));
            Shoot(dialog, Path.Combine(folder, "autovent-systems-1.5x.png"), 1.5);

            dialog.Search = "car park";
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-systems-search.png"));

            dialog.Close();
        }

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

        // The Arrange rooms... dialog on LEVEL 03 of a tower: two architect's
        // apartments with their interior rooms, a third with one room taken
        // out, the cores, and one room unticked - the large apartment moved
        // to the top and its rooms reordered, as a person would have left it
        private static void Arrange(string folder)
        {
            const string Level = "LEVEL 03";

            ATKINSMEC.VENTARRANGE.Row Room(string key, string number, string name, string aptKey = null,
                                           string apt = null, string aptNumber = null, bool exported = true)
            {
                return new ATKINSMEC.VENTARRANGE.Row
                {
                    Key = key, Number = number, Name = name,
                    ApartmentKey = aptKey, Apartment = apt, ApartmentNumber = aptNumber,
                    HomeKey = aptKey, Home = apt, HomeNumber = aptNumber, Exported = exported
                };
            }

            var all = new System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>
            {
                Room("10:31", "TA-03-31", "CORRIDOR"),
                Room("10:33", "TA-03-33", "STAIR 01"),
                Room("10:34", "TA-03-34", "ELEVATOR LOBBY", exported: false),
                Room("20:55", "121.055", "LIVING/DINING", "11:9", "2BDR-A1", "TA-03-09"),
                Room("20:56", "121.056", "MASTER BEDROOM", "11:9", "2BDR-A1", "TA-03-09"),
                Room("20:57", "121.057", "DRESSING", "11:9", "2BDR-A1", "TA-03-09"),
                Room("20:58", "121.058", "ENSUITE", "11:9", "2BDR-A1", "TA-03-09"),
                Room("21:05", "121.105", "MASTER BEDROOM", "11:4", "2BDR-A2", "TA-03-04"),
                Room("21:06", "121.106", "LIVING/DINING", "11:4", "2BDR-A2", "TA-03-04"),
                Room("21:07", "121.107", "KITCHEN", "11:4", "2BDR-A2", "TA-03-04"),
                Room("22:15", "121.315", "LIVING/DINING", "11:6", "1BDR-C", "TA-03-06"),
                Room("22:17", "121.317", "BALCONY", "11:6", "1BDR-C", "TA-03-06")
            };

            int Compare(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

            // Two more levels, so a search has somewhere else to point at
            var other = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>>
            {
                ["LEVEL 04A"] = new System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>
                {
                    Room("10:41", "TA-04-31", "CORRIDOR"),
                    Room("23:01", "121.401", "MASTER BEDROOM", "11:19", "2BDR-A1", "TA-04-09"),
                    Room("23:02", "121.402", "BEDROOM", "11:19", "2BDR-A1", "TA-04-09")
                },
                ["LEVEL 05A"] = new System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>
                {
                    Room("10:51", "TA-05-31", "CORRIDOR"),
                    Room("24:01", "121.501", "MASTER BEDROOM", "11:29", "2BDR-A2", "TA-05-04")
                }
            };

            // Every level, the way RoomsOn has them
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>> RowsOf(
                ATKINSMEC.VENTARRANGE.Arrangement a)
            {
                var levels = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>>
                {
                    [Level] = LevelRows(Level, all, a)
                };

                foreach (var level in other)
                    levels[level.Key] = LevelRows(level.Key, level.Value, a);

                return levels;
            }

            // The default order, the way RoomsOn has it: units by number,
            // an apartment's rooms by theirs - then the arrangement
            System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row> LevelRows(
                string level, System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row> source, ATKINSMEC.VENTARRANGE.Arrangement a)
            {
                var rows = source.Select(r => new ATKINSMEC.VENTARRANGE.Row
                {
                    Key = r.Key, Number = r.Number, Exported = r.Exported,
                    Name = a.NameOf(r.Key, r.Name), ModelName = r.Name,
                    HomeKey = r.HomeKey, Home = r.Home, HomeNumber = r.HomeNumber,
                    ApartmentKey = a.Ungrouped.Contains(r.Key) ? null : r.ApartmentKey,
                    Apartment = a.Ungrouped.Contains(r.Key) || r.ApartmentKey == null ? null : a.NameOf(r.ApartmentKey, r.Apartment),
                    ModelApartment = a.Ungrouped.Contains(r.Key) ? null : r.Apartment,
                    ApartmentNumber = a.Ungrouped.Contains(r.Key) ? null : r.ApartmentNumber
                }).ToList();

                rows.Sort((x, y) =>
                {
                    int unit = Compare(x.ApartmentNumber ?? x.Number, y.ApartmentNumber ?? y.Number);
                    return unit != 0 ? unit : Compare(x.Number, y.Number);
                });

                return ATKINSMEC.VENTARRANGE.Order(rows, level, a, r => r.Key, r => r.ApartmentKey);
            }

            var arrangement = new ATKINSMEC.VENTARRANGE.Arrangement();
            arrangement.Ungrouped.Add("22:17");
            arrangement.Names["20:58"] = "MASTER ENSUITE";
            arrangement.Names["11:4"] = "2BDR-A2 (FLAT 2)";
            arrangement.Orders[ATKINSMEC.VENTARRANGE.LevelContainer(Level)] =
                new System.Collections.Generic.List<string> { "11:9", "10:31", "10:33", "11:4", "11:6", "22:17", "10:34" };
            arrangement.Orders[ATKINSMEC.VENTARRANGE.ApartmentContainer("11:9")] =
                new System.Collections.Generic.List<string> { "20:56", "20:57", "20:58", "20:55" };

            // The kitchen and living of 2BDR-A2 on one FCU, one fresh air
            // connection
            arrangement.Merges.Add(new System.Collections.Generic.List<string> { "21:06", "21:07" });

            var dialog = new ATKINSMEC.VENTARRANGEDIALOG(
                new[] { Level, "LEVEL 04A", "LEVEL 05A" }, Level, RowsOf, arrangement)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000
            };

            dialog.Show();
            dialog.UpdateLayout();

            // One room picked, so the buttons show as they would
            var list = FindChild<System.Windows.Controls.ListBox>(dialog);

            if (list != null && list.Items.Count > 2)
                ((System.Windows.Controls.ListBoxItem)list.Items[2]).IsSelected = true;

            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-arrange.png"));
            Shoot(dialog, Path.Combine(folder, "autovent-arrange-1.5x.png"), 1.5);

            // At its smallest the side buttons scroll rather than clip
            double tall = dialog.Height;
            dialog.Height = dialog.MinHeight;
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-arrange-short.png"));

            dialog.Height = tall;
            dialog.UpdateLayout();

            // Searching: "bedroom" - the rooms found, each under its
            // apartment heading, and the other levels with matches named
            var search = FindChild<System.Windows.Controls.TextBox>(dialog);

            if (search != null)
                search.Text = "bedroom";

            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-arrange-search.png"));
            Shoot(dialog, Path.Combine(folder, "autovent-arrange-search-1.5x.png"), 1.5);

            dialog.Close();
        }

        private static T FindChild<T>(DependencyObject from) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(from, i);

                if (child is T found)
                    return found;

                T deeper = FindChild<T>(child);

                if (deeper != null)
                    return deeper;
            }

            return null;
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
