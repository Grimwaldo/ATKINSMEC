using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Linq;
using System.Windows.Controls;

namespace ATKINSMEC
{
    // Just enough of VENTWindow to let the REAL XAML draw itself, with
    // no Revit anywhere near it. Every handler the XAML wires up has to
    // exist or the window will not load.
    public partial class VENTWindow : Window
    {
        public class Tick : INotifyPropertyChanged
        {
            private bool _on;

            public bool IsOn
            {
                get { return _on; }
                set { _on = value; Changed("IsOn"); }
            }

            public string Label { get; set; }

            public string Span { get; set; }

            public string Paired { get; set; }

            public bool IsApartments { get; set; }

            // As VENTWindow.Tick: a placement found inside apartments says
            // where, any other its span
            public string Detail
            {
                get { return string.IsNullOrEmpty(Paired) ? Span : Paired; }
            }

            public string Tip
            {
                get { return Label + Environment.NewLine + Detail; }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void Changed(string what)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(what));
            }
        }

        // A level built by the REAL LevelRow.Build (VENTLEVELROWS is
        // compiled in), so the render shows its real label and ticks
        private ObservableCollection<LevelRow> _levels;

        private readonly System.Collections.Generic.Dictionary<string, bool> _choices =
            new System.Collections.Generic.Dictionary<string, bool>();

        private LevelRow Level(string name, bool inSheet, bool on, bool open, params string[] rooms)
        {
            var rows = new System.Collections.Generic.List<RoomRow>();

            for (int i = 0; i + 1 < rooms.Length; i += 2)
            {
                rows.Add(new RoomRow
                {
                    LinkId = 1,
                    RoomId = i,
                    Level = name,
                    Number = rooms[i],
                    Name = rooms[i + 1],
                    Tip = "Room " + rooms[i] + " " + rooms[i + 1]
                });
            }

            LevelRow level = LevelRow.Build(
                name, inSheet ? "     (in the sheet)" : string.Empty,
                true, on, rows, _choices);

            level.IsExpanded = open;

            return level;
        }

        // The search box, filtering with the REAL rule (VENTLINKFILTER), as
        // VENTWindow.ApplyLinkSearch does
        private void TxtLinkSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtLinkSearchHint == null || ListLinks == null || ListLinks.ItemsSource == null)
                return;

            string query = TxtLinkSearch.Text;

            TxtLinkSearchHint.Visibility = string.IsNullOrEmpty(query)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            var all = ((System.Collections.IEnumerable)ListLinks.ItemsSource).Cast<Tick>().ToList();

            System.Windows.Data.CollectionViewSource.GetDefaultView(ListLinks.ItemsSource).Filter =
                string.IsNullOrWhiteSpace(query)
                    ? (Predicate<object>)null
                    : o => VENTLINKFILTER.Matches(((Tick)o).Label, query);

            if (!string.IsNullOrWhiteSpace(query))
            {
                TxtLinksInfo.Text = VENTLINKFILTER.Said(
                    all.Count(l => VENTLINKFILTER.Matches(l.Label, query)),
                    all.Count,
                    all.Count(l => l.IsOn && !VENTLINKFILTER.Matches(l.Label, query)));
            }
        }

        private void TxtLinkSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { }

        public void SearchLinks(string text)
        {
            TxtLinkSearch.Text = text;
        }

        // The links list scrolled to its end, where the interior
        // placements are
        public void ScrollLinks()
        {
            var items = (System.Collections.IList)ListLinks.ItemsSource;

            ListLinks.ScrollIntoView(items[items.Count - 1]);
            ListLinks.ScrollIntoView(items[3]);
        }

        public VENTWindow()
        {
            InitializeComponent();
        }

        // What a real run looks like, so the window can be judged full
        // rather than empty
        public void Fill()
        {
            TxtDocument.Text = "5232535-ATR-POD-ZZZ-3DM-HVC-000050.rvt";

            // One tower model placed twice, both placements holding
            // apartments; the interior model placed once per band of
            // floors, found inside them; a podium model and a structural
            // link
            var links = new ObservableCollection<Tick>
            {
                new Tick { Label = "5232535-ATR-POD-ZZZ-3DM-ARC-000010.rvt", IsOn = true,
                           Span = "B1_BASEMENT  to  P3_PODIUM 3   -   412 room(s)" },
                new Tick { Label = "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 1 : Tower A", IsOn = true,
                           IsApartments = true,
                           Span = "below B1_BASEMENT  to  L48_ROOF   -   1386 room(s), 12 below" },
                new Tick { Label = "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 2 : Tower B", IsOn = true,
                           IsApartments = true,
                           Span = "L04_LEVEL 04  to  L48_ROOF   -   1386 room(s)" },
                new Tick { Label = "5232535-ATR-TWR-ZZZ-3DM-IND-000030.rvt : 1",
                           Span = "L04_LEVEL 04  to  L10_LEVEL 10   -   168 room(s)",
                           Paired = "in apartments on L04_LEVEL 04 to L10_LEVEL 10  -  5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 1 : Tower A" },
                new Tick { Label = "5232535-ATR-TWR-ZZZ-3DM-IND-000030.rvt : 2",
                           Span = "L11_LEVEL 11  to  L20_LEVEL 20   -   240 room(s)",
                           Paired = "in apartments on L11_LEVEL 11 to L20_LEVEL 20  -  5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt : 2 : Tower B" },
                new Tick { Label = "5232535-ATR-POD-ZZZ-3DM-STR-000004.rvt",
                           Span = "B1_BASEMENT  to  P3_PODIUM 3   -   no rooms" }
            };

            ListLinks.ItemsSource = links;

            // The REAL wording, fed what that job would find
            TxtApartments.Text = VENTLEVELNOTE.Apartments(2, 2, 136, 408, 0);
            TxtApartments.ToolTip = VENTLEVELNOTE.ApartmentsTip(
                "ARC-000020.rvt : 1 : Tower A, ARC-000020.rvt : 2 : Tower B",
                new System.Collections.Generic.List<string>
                {
                    "IND-000030.rvt : 1  ->  ARC-000020.rvt : 1 : Tower A apartments on L04_LEVEL 04 to L10_LEVEL 10",
                    "IND-000030.rvt : 2  ->  ARC-000020.rvt : 2 : Tower B apartments on L11_LEVEL 11 to L20_LEVEL 20"
                }, 0);

            TxtLinksInfo.Text =
                "6 loaded link(s). Tick the architectural model(s) to " +
                "read rooms from; Apartments on one whose rooms are apartments.";

            // The ground floor open, the way somebody looking for a
            // room would leave it
            var levels = new ObservableCollection<LevelRow>
            {
                // Two apartments from the architect's model, with the
                // interior model's rooms inside them - as the window
                // names them (VENTWindow.RefreshLevels)
                Level("GR_GROUND FLOOR", true, true, true,
                    "G-001", "MAIN LOBBY", "G-002", "RECEPTION",
                    "G-03-01", "DRIVER/STAFF APT  /  Bed room", "G-03-02", "DRIVER/STAFF APT  /  Toilet",
                    "G-04-01", "DRIVER/STAFF APT LARGE  /  Bed room", "G-04-02", "DRIVER/STAFF APT LARGE  /  Living",
                    "G-04-03", "DRIVER/STAFF APT LARGE  /  Toilet", "G-008", "RETAIL UNIT 01"),
                Level("P1_PODIUM 1", true, true, false,
                    "P1-001", "LIFT LOBBY", "P1-002", "OFFICE"),
                Level("P2_PODIUM 2", true, true, false,
                    "P2-001", "LIFT LOBBY"),
                Level("P3_PODIUM 3", true, true, false,
                    "P3-001", "PLANT"),
                Level("P4_PODIUM 4", false, false, false),
                Level("L1 / OFFICES", false, false, false),
                Level("ROOF PLANT", false, false, false)
            };

            // Two ground floor rooms unticked, as a person would - the
            // level goes to the third state and its label counts them
            levels[0].Rooms[1].IsOn = false;
            levels[0].Rooms[6].IsOn = false;

            ListLevels.ItemsSource = levels;

            _levels = levels;

            LstFresh.ItemsSource = new System.Collections.Generic.List<Tick>
            {
                new Tick { Label = "ATK_ME_Fresh Air", IsOn = true },
                new Tick { Label = "ATK_ME_Extract" },
                new Tick { Label = "ATK_ME_Supply Air" },
                new Tick { Label = "ATK_ME_OA Supply" },
                new Tick { Label = "ATK_ME_Exhaust Air" }
            };

            LstExtract.ItemsSource = new System.Collections.Generic.List<Tick>
            {
                new Tick { Label = "ATK_ME_Fresh Air" },
                new Tick { Label = "ATK_ME_Extract", IsOn = true },
                new Tick { Label = "ATK_ME_Supply Air" },
                new Tick { Label = "ATK_ME_OA Supply" },
                new Tick { Label = "ATK_ME_Exhaust Air", IsOn = true }
            };

            TxtFreshTypes.Text = "Fresh air  -  1 of 5";
            TxtExtractTypes.Text = "Extract / exhaust  -  2 of 5";

            // The REAL wording (VENTLEVELNOTE is compiled in), for a
            // sheet that names two levels this model has not got
            Levels(9, 4, 1, 6, 4, "B1_BASEMENT", "MEZZANINE");

            TxtSheetPath.Text = "5232535 - Ventilation Schedule Rev C.xlsx";

            CboWorksheet.IsEnabled = true;
            CboWorksheet.Items.Add("MLCP");
            CboWorksheet.Items.Add("OFFICES");
            CboWorksheet.Items.Add("NOTES");
            CboWorksheet.SelectedIndex = 0;

            CboExportSheet.IsEnabled = true;
            CboExportSheet.Items.Add("MLCP");
            CboExportSheet.Items.Add("OFFICES");
            CboExportSheet.SelectedIndex = 0;

            BtnExport.IsEnabled = true;

            TxtSheetInfo.Text =
                "412 room record(s)." + Environment.NewLine +
                Environment.NewLine +
                "Levels in the sheet:" + Environment.NewLine +
                "    B1_BASEMENT" + Environment.NewLine +
                "    GR_GROUND FLOOR" + Environment.NewLine +
                "    MEZZANINE" + Environment.NewLine +
                "    P1_PODIUM 1" + Environment.NewLine +
                "    P2_PODIUM 2" + Environment.NewLine +
                "    P3_PODIUM 3" + Environment.NewLine +
                Environment.NewLine +
                "Columns found:" + Environment.NewLine +
                "    Level, Room Name, Room Number, Fresh Air (l/s), " +
                "Extract (l/s)";

            TxtStatus.Text = "412 room record(s) read from MLCP.";

            StatusDot.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x15, 0x62, 0x9B));
        }

        public void ShowExport()
        {
            TabActions.SelectedIndex = 1;

            // The REAL wording (VENTEXPORTNOTE is compiled in), and
            // the longest of the three hints, so the render shows the
            // worst case rather than the best
            TxtExportHint.Text = new[]
                {
                    VENTEXPORTNOTE.Hint(VENTEXPORTNOTE.Fill),
                    VENTEXPORTNOTE.Hint(VENTEXPORTNOTE.NewWorkbook),
                    VENTEXPORTNOTE.Hint(VENTEXPORTNOTE.AddWorksheet)
                }
                .OrderByDescending(h => h.Length).First();

            // A family ticked from a plumbing model: the longer line
            TxtFixtures.Text = VENTEXPORTNOTE.Fixtures(true, 2, 1);
        }
        public void ShowAssign() { TabActions.SelectedIndex = 0; }

        // Several rows picked by the REAL Picks rules, as a person would:
        // a click on one room, Shift+click two further down, and
        // Ctrl+click on another level - so the render shows the pick
        // drawn on rooms AND on a level, and the button live
        public void PickRoom()
        {
            object anchor = Picks.Click(_levels, null, _levels[0].Rooms[2], false, false);
            anchor = Picks.Click(_levels, anchor, _levels[0].Rooms[4], false, true);
            Picks.Click(_levels, anchor, _levels[0], true, false);

            BtnShowRoom.IsEnabled = Picks.Rooms(_levels).Count > 0;

            TxtStatus.Text = Picks.Count(_levels) + " rows picked - " +
                             Picks.Rooms(_levels).Count + " room(s). Tick any of " +
                             "them to tick them all, or press Show in plan.";
        }

        // What VENTWindow.ShowLevelNote does, fed the same numbers
        public void Levels(
            int model, int withRooms, int links,
            int sheet, int matched, params string[] sheetOnly)
        {
            string warning = VENTLEVELNOTE.Warning(sheetOnly);

            TxtLevelsWarning.Text = warning;
            TxtLevelsWarning.Visibility = warning.Length == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            TxtLevelsInfo.Text = VENTLEVELNOTE.Summary(
                model, withRooms, links, sheet, matched, sheetOnly.Length);
        }

        // The export tab as it looks with a NEW workbook chosen: the
        // worksheet box off, the name box on, and that way's own hint
        public void TutorialNewWorkbook()
        {
            TabActions.SelectedIndex = 1;

            RdoNewWorkbook.IsChecked = true;
            CboExportSheet.IsEnabled = false;
            TxtNewSheet.IsEnabled = true;
            TxtNewSheet.Text = "Ventilation";

            TxtExportHint.Text = VENTEXPORTNOTE.Hint(VENTEXPORTNOTE.NewWorkbook);
            TxtFixtures.Text = VENTEXPORTNOTE.Fixtures(true, 0, 0);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) { }
        private void BtnRefreshLinks_Click(object sender, RoutedEventArgs e) { }
        private void BtnImport_Click(object sender, RoutedEventArgs e) { }
        private void CboWorksheet_SelectionChanged(
            object sender, SelectionChangedEventArgs e) { }
        private void BtnAllLevels_Click(object sender, RoutedEventArgs e) { }
        private void BtnShowRoom_Click(object sender, RoutedEventArgs e) { }
        private void TabActions_SelectionChanged(
            object sender, SelectionChangedEventArgs e) { }
        private void ListLevels_SelectedItemChanged(
            object sender, RoutedPropertyChangedEventArgs<object> e) { }
        private void ListLevels_MouseDoubleClick(
            object sender, System.Windows.Input.MouseButtonEventArgs e) { }
        private void ListLevels_PreviewMouseLeftButtonDown(
            object sender, System.Windows.Input.MouseButtonEventArgs e) { }
        private void ListLevels_PreviewKeyDown(
            object sender, System.Windows.Input.KeyEventArgs e) { }
        private void LevelTick_Click(object sender, RoutedEventArgs e) { }
        private void RoomTick_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoLevels_Click(object sender, RoutedEventArgs e) { }
        private void BtnRun_Click(object sender, RoutedEventArgs e) { }
        private void BtnExport_Click(object sender, RoutedEventArgs e) { }
        private void ExportWay_Changed(object sender, RoutedEventArgs e) { }
        private void BtnClose_Click(object sender, RoutedEventArgs e) { }
        private void ChkFixtures_Click(object sender, RoutedEventArgs e) { }
        private void BtnFixtures_Click(object sender, RoutedEventArgs e) { }
    }
}
