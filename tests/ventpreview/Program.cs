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
            // Levels and rooms' columns as they come, not as this PC chose
            ATKINSMEC.VENTCOLUMNS.SettingsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ventpreview-columns-none.txt");
            try { System.IO.File.Delete(ATKINSMEC.VENTCOLUMNS.SettingsPath); } catch { }

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
            Shoot(window, Path.Combine(folder, "autovent-export-1.5x.png"), 1.5);

            Fixtures(folder);

            SystemTypes(folder);

            Interiors(folder);

            Arrange(folder);

            Occupancy(folder);

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
        // The Occupancy... window with the built-in rules, the lists read
        // from the office workbook in the repository as the add-in reads
        // its embedded copy
        private static void Occupancy(string folder)
        {
            System.Collections.Generic.List<string> fresh, exhaust, uses;

            OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("preview");

            using (var package = new OfficeOpenXml.ExcelPackage(
                       new FileInfo(RepoPath.Find(@"ATKINSMEC\Resources\VentTemplate.xlsx"))))
            {
                fresh = ATKINSMEC.VENTOCCUPANCY.Table(package.Workbook, false);
                exhaust = ATKINSMEC.VENTOCCUPANCY.Table(package.Workbook, true);
                uses = ATKINSMEC.VENTOCCUPANCY.Uses(package.Workbook);
            }

            // One "equals" rule at the top, so the Match column shows both
            var rules = ATKINSMEC.VENTOCCUPANCY.Defaults();
            rules.Insert(0, new ATKINSMEC.VENTOCCUPANCY.Rule { Words = "LOBBY", Exact = true, Fresh = "Reception Areas" });

            var dialog = new ATKINSMEC.VENTOCCUPANCYDIALOG(rules, fresh, exhaust, uses)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000
            };

            dialog.Show();
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-occupancy.png"));
            Shoot(dialog, Path.Combine(folder, "autovent-occupancy-1.5x.png"), 1.5);

            dialog.Close();
        }

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
                new ATKINSMEC.VENTFIXTUREDIALOG.Source { Id = 14, Label = "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 2 : Tower B" },
                new ATKINSMEC.VENTFIXTUREDIALOG.Source { Id = 13, Label = "5232535-ATR-POD-ZZZ-3DM-PLU-000030.rvt" }
            };

            // An interior model placed on forty floors - one row
            for (int floor = 1; floor <= 40; floor++)
            {
                sources.Add(new ATKINSMEC.VENTFIXTUREDIALOG.Source
                {
                    Id = 100 + floor,
                    Label = "5232535-ATR-TWR-ZZZ-3DM-IND-000040.rvt : " + floor
                });
            }

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

            // Two models picked, the families searched
            var models = (System.Collections.IList)dialog.ModelList.ItemsSource;

            dialog.ModelList.SelectedItems.Add(models[3]);
            dialog.ModelList.SelectedItems.Add(models[4]);
            dialog.Search = "water";
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-fixtures-search.png"));

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

        // LEVELS AND ROOMS (VENTROOMSWINDOW) over a tower: LEVEL 03 open - two
        // architect's containers with their interior rooms, a third with one
        // room taken out, the cores, one room unticked, the large container
        // moved to the top and its rooms reordered, as a person would have
        // left it - LEVEL 04A open below it, LEVEL 05A closed
        private static void Arrange(string folder)
        {
            const string Level = "LEVEL 03";

            ATKINSMEC.VENTARRANGE.Row Room(string key, string number, string name, string aptKey = null,
                                           string apt = null, string aptNumber = null)
            {
                // An area and a height that look like the room's - the
                // number's digits, so a render is the same every time
                int seed = key.Sum(c => (int)c) % 97;

                return new ATKINSMEC.VENTARRANGE.Row
                {
                    Key = key, Number = number, Name = name,
                    ApartmentKey = aptKey, Apartment = apt, ApartmentNumber = aptNumber,
                    HomeKey = aptKey, Home = apt, HomeNumber = aptNumber,
                    AreaM2 = 4 + (seed % 40) * 0.73, HeightM = 2.6 + (seed % 5) * 0.1, HeightToCeiling = seed % 3 != 0
                };
            }

            var all = new System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>
            {
                Room("10:31", "TA-03-31", "CORRIDOR"),
                Room("10:33", "TA-03-33", "STAIR 01"),
                Room("10:34", "TA-03-34", "ELEVATOR LOBBY"),
                Room("20:55", "121.055", "LIVING/DINING", "11:9", "2BDR-A1", "TA-03-09"),
                Room("20:56", "121.056", "MASTER BEDROOM", "11:9", "2BDR-A1", "TA-03-09"),
                Room("20:57", "121.057", "DRESSING", "11:9", "2BDR-A1", "TA-03-09"),
                Room("20:58", "121.058", "ENSUITE", "11:9", "2BDR-A1", "TA-03-09"),
                Room("21:5", "121.105", "MASTER BEDROOM", "11:4", "2BDR-A2", "TA-03-04"),
                Room("21:6", "121.106", "LIVING/DINING", "11:4", "2BDR-A2", "TA-03-04"),
                Room("21:7", "121.107", "KITCHEN", "11:4", "2BDR-A2", "TA-03-04"),
                Room("22:15", "121.315", "LIVING/DINING", "11:6", "1BDR-C", "TA-03-06"),
                Room("22:17", "121.315", "BALCONY", "11:6", "1BDR-C", "TA-03-06")
            };

            int Compare(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

            // Two more levels, so the list has more than one
            var other = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>>
            {
                ["LEVEL 04A"] = new System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>
                {
                    Room("10:41", "TA-04-31", "CORRIDOR"),
                    Room("23:1", "121.401", "MASTER BEDROOM", "11:19", "2BDR-A1", "TA-04-09"),
                    Room("23:2", "121.402", "BEDROOM", "11:19", "2BDR-A1", "TA-04-09")
                },
                ["LEVEL 05A"] = new System.Collections.Generic.List<ATKINSMEC.VENTARRANGE.Row>
                {
                    Room("10:51", "TA-05-31", "CORRIDOR"),
                    Room("24:1", "121.501", "MASTER BEDROOM", "11:29", "2BDR-A2", "TA-05-04")
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
                    Key = r.Key, Number = r.Number,
                    Name = a.NameOf(r.Key, r.Name), ModelName = r.Name,
                    AreaM2 = r.AreaM2, HeightM = r.HeightM, HeightToCeiling = r.HeightToCeiling,
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
            arrangement.Merges.Add(new System.Collections.Generic.List<string> { "21:6", "21:7" });

            // THE TICKS, as the main window holds them (LevelRow.Build): every
            // room ticked but the elevator lobby; LEVEL 05A closed
            var choices = new System.Collections.Generic.Dictionary<string, bool>();
            var ticks = new System.Collections.Generic.List<ATKINSMEC.VENTWindow.LevelRow>();

            foreach (var level in RowsOf(arrangement))
            {
                var rooms = level.Value.Select(r => new ATKINSMEC.VENTWindow.RoomRow
                {
                    LinkId = long.Parse(r.Key.Split(':')[0]),
                    RoomId = long.Parse(r.Key.Split(':')[1]),
                    Level = level.Key,
                    Number = r.Number,
                    Name = r.Name,
                    Tip = "Room " + r.Number + " " + r.Name
                }).ToList();

                var row = ATKINSMEC.VENTWindow.LevelRow.Build(level.Key, "     (in the sheet)", true, true, rooms, choices);
                row.IsExpanded = level.Key != "LEVEL 05A";
                ticks.Add(row);
            }

            ticks[0].Rooms.First(r => r.Key == "10:34").IsOn = false;

            // THE AIRFLOWS (VENTFLOWS): the sheet's - the master bedroom and
            // its dressing merged in the sheet only, so adopted on opening -
            // and what the terminals carry, two of them not what the sheet says
            ATKINSMEC.VENTFLOWS.Sheet S(double fresh, double extract, params string[] merges) =>
                new ATKINSMEC.VENTFLOWS.Sheet
                {
                    Air = new ATKINSMEC.VENTFLOWS.Air { Fresh = fresh, Extract = extract },
                    Merges = merges.ToList()
                };

            ATKINSMEC.VENTFLOWS.Air A(double fresh, double extract) =>
                new ATKINSMEC.VENTFLOWS.Air { Fresh = fresh, Extract = extract };

            double none = double.NaN;

            var sheet = new System.Collections.Generic.Dictionary<string, ATKINSMEC.VENTFLOWS.Sheet>
            {
                ["10:31"] = S(10, none),
                ["20:55"] = S(25, none),
                ["20:56"] = S(15, none, "fresh AA14:AA15"),
                ["20:57"] = S(0, none, "fresh AA14:AA15"),
                ["20:58"] = S(0, 10),
                ["21:5"] = S(15, none),
                ["21:6"] = S(20, 0),
                ["21:7"] = S(0, 50),
                ["22:15"] = S(20, none),
                ["10:41"] = S(10, none),
                ["23:1"] = S(15, none),
                ["23:2"] = S(10, none)
            };

            // The sheet's own columns (2026-10-25): categories as the office
            // fills them, one blank with what an export would put there, the
            // bedrooms' occupants, a kitchen's unit, one area since changed
            void Explain(string key, string fresh, string exhaust, string suggested = null, double people = double.NaN, double units = double.NaN)
            {
                sheet[key].FreshCategory = fresh;
                sheet[key].ExhaustCategory = exhaust;
                sheet[key].FreshSuggested = suggested;
                sheet[key].Occupants = people;
                sheet[key].Units = units;
            }

            Explain("10:31", "Common Corridors", "");
            Explain("20:55", "Dwelling Unit", "");
            Explain("20:56", "Dwelling Unit", "", people: 2);
            Explain("20:57", "", "", suggested: "Dwelling Unit");
            Explain("20:58", "", "Toilets - private (residential)", units: 1);
            Explain("21:5", "Dwelling Unit", "", people: 2);
            Explain("21:6", "Dwelling Unit", "");
            Explain("21:7", "Dwelling Unit", "Residential kitchens- Apartments", units: 1);
            Explain("22:15", "Dwelling Unit", "");
            Explain("10:41", "Common Corridors", "");
            Explain("23:1", "Dwelling Unit", "", people: 2);
            Explain("23:2", "Dwelling Unit", "", people: 1);
            sheet["21:6"].AreaM2 = 14.0;

            var carrying = new System.Collections.Generic.Dictionary<string, ATKINSMEC.VENTFLOWS.Air>
            {
                ["10:31"] = A(10, none),
                ["20:55"] = A(30, none),
                ["20:56"] = A(12, none),
                ["20:57"] = A(3, none),
                ["20:58"] = A(none, 10),
                ["21:5"] = A(0, none),
                ["21:6"] = A(20, none),
                ["21:7"] = A(none, 50),
                ["22:15"] = A(20, none),
                ["10:41"] = A(10, none),
                ["23:1"] = A(15, none),
                ["23:2"] = A(10, none)
            };

            // KIFAF's FCU: 2BDR-A2's bedroom, living and kitchen - the last two
            // merged already - and, through its Revit system, a room of
            // 2BDR-A1, which is left out; a fresh air unit with no endcap
            // reaching rooms everywhere suggests nothing
            var units = new System.Collections.Generic.List<ATKINSMEC.VENTFLOWS.Unit>
            {
                new ATKINSMEC.VENTFLOWS.Unit { Id = 3678148, Fresh = 1, Rooms = { "21:5", "21:6", "21:7", "20:55" } },
                new ATKINSMEC.VENTFLOWS.Unit { Id = 3092885, Fresh = 0, Rooms = { "20:55", "21:6", "22:15", "10:31" } }
            };

            // Three rooms the architect's model has with no area
            var unenclosed = new System.Collections.Generic.List<ATKINSMEC.VENTUNENCLOSED.Room>
            {
                new ATKINSMEC.VENTUNENCLOSED.Room { LinkId = 10, RoomId = 91, Number = "TA-03-40", Name = "RISER", LinkName = "5232535-ATR-TWR-ZZZ-3DM-ARC-000010", Level = "LEVEL 03", LevelZ = 12 },
                new ATKINSMEC.VENTUNENCLOSED.Room { LinkId = 10, RoomId = 92, Number = "TA-03-41", Name = "STORE", LinkName = "5232535-ATR-TWR-ZZZ-3DM-ARC-000010", Level = "LEVEL 03", LevelZ = 12, Inside = "TA-03-31 CORRIDOR" },
                new ATKINSMEC.VENTUNENCLOSED.Room { LinkId = 10, RoomId = 93, Number = "TA-04-40", Name = "RISER", LinkName = "5232535-ATR-TWR-ZZZ-3DM-ARC-000010", Level = "LEVEL 04A", LevelZ = 15.6 }
            };

            var dialog = new ATKINSMEC.VENTROOMSWINDOW(() => ticks, RowsOf, arrangement, sheet, carrying, units)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000,
                NotEnclosed = () => unenclosed
            };

            dialog.Show();
            dialog.UpdateLayout();


            // Two rows picked, so the buttons and the pick show as they would
            var list = FindChild<System.Windows.Controls.ListBox>(dialog);

            if (list != null && list.Items.Count > 3)
            {
                ((System.Windows.Controls.ListBoxItem)list.Items[2]).IsSelected = true;
                ((System.Windows.Controls.ListBoxItem)list.Items[3]).IsSelected = true;
            }

            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-arrange.png"));
            Shoot(dialog, Path.Combine(folder, "autovent-arrange-1.5x.png"), 1.5);

            // Model: grouped as the FCUs serve - then back to Sheet
            System.Reflection.MethodInfo view = typeof(ATKINSMEC.VENTROOMSWINDOW)
                .GetMethod("View", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            view.Invoke(dialog, new object[] { true });
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-arrange-model.png"));

            view.Invoke(dialog, new object[] { false });
            dialog.UpdateLayout();

            // At its smallest the side buttons scroll rather than clip
            double tall = dialog.Height;
            dialog.Height = dialog.MinHeight;
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-arrange-short.png"));

            dialog.Height = tall;
            dialog.UpdateLayout();

            // Searching: "bedroom" - the rooms found, each under its container
            // and level, the closed level opened to show its own
            var search = FindChild<System.Windows.Controls.TextBox>(dialog);

            if (search != null)
                search.Text = "bedroom";

            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-arrange-search.png"));
            Shoot(dialog, Path.Combine(folder, "autovent-arrange-search-1.5x.png"), 1.5);

            // Differences only: what needs attention
            search = FindChild<System.Windows.Controls.TextBox>(dialog);

            if (search != null)
                search.Text = string.Empty;

            var only = (System.Windows.Controls.CheckBox)typeof(ATKINSMEC.VENTROOMSWINDOW)
                .GetField("_onlyDifferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(dialog);

            // A click as a person makes it: it ticks the box, then Click
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(only, new object[0]);
            dialog.UpdateLayout();

            Shoot(dialog, Path.Combine(folder, "autovent-arrange-differences.png"));

            // The rooms with no area, listed
            var notEnclosed = (Window)typeof(ATKINSMEC.VENTROOMSWINDOW)
                .GetMethod("NotEnclosedList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(dialog, null);

            notEnclosed.Owner = null;
            notEnclosed.WindowStartupLocation = WindowStartupLocation.Manual;
            notEnclosed.Left = -4000;
            notEnclosed.Top = -4000;
            notEnclosed.Show();
            notEnclosed.UpdateLayout();

            Shoot(notEnclosed, Path.Combine(folder, "autovent-notenclosed.png"));
            Shoot(notEnclosed, Path.Combine(folder, "autovent-notenclosed-1.5x.png"), 1.5);

            notEnclosed.Close();

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
