using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ATKINSMEC
{
    // LEVELS AND ROOMS - the levels list and Arrange rooms... in one window
    // (user, 2026-10-19: "combine 'levels to process' and arrange rooms
    // window"; asked, chose a window of its own). Every level of this
    // model, each opening to its rooms as the sheet will have them: the
    // level's own rooms and its containers, each container's units under
    // it - a tick on every row, the airflows beside them.
    //
    //   - the TICKS are the main window's (VENTLEVELROWS, bound here): a
    //     ticked room is exported and has its airflow written; a level's
    //     and a container's tick follow their rooms; a box on a PICKED row
    //     ticks every picked row;
    //   - picks are a file list's: click, Ctrl, Shift, Ctrl+A. A picked
    //     level or container stands for its rooms showing - for Show in
    //     plan, Set airflow and Write to terminals;
    //   - drag a row (or Move up / Move down, Ctrl+Up / Ctrl+Down) WITHIN
    //     ITS LEVEL AND CONTAINER; Rename (F2), Find and replace, Ungroup /
    //     Regroup, Merge / Unmerge, Reset level (VENTARRANGE);
    //   - Sheet | Model, Write to terminals (VENTFLOWS);
    //   - THE OCCUPANCY of a room chosen in its cells, as in the sheet - a
    //     double-click on a category, use, occupants or units - and its NEW
    //     air worked out by the sheet's own formulas (user, 2026-10-26);
    //     a double-click on a Model airflow writes it to the terminals;
    //   - Show in plan, or double-click a room's name;
    //   - the search (Ctrl+F) only hides rows: ticks stand, and All, None
    //     and a level's tick act on the rows showing.
    //
    // MODELESS, so Show in plan and Write to terminals reach Revit through
    // the main window's external event while it is open. Everything takes
    // effect at once and the main window saves it (Arranged, Ticked) -
    // there is no OK or Cancel; Reset level asks first.
    //
    // REVIT-FREE: the main window hands in its levels, a function working
    // every level's rows out of what it has read - the RoomsOn its runs
    // use, so this shows exactly what the sheet and the airflows get - and
    // what to do for the presses that reach Revit. tests/arrangeflowtest
    // drives it off-screen; tests/ventpreview renders it.
    public class VENTROOMSWINDOW : Window
    {
        private readonly Func<IList<VENTWindow.LevelRow>> _levelsOf;
        private readonly Func<VENTARRANGE.Arrangement, Dictionary<string, List<VENTARRANGE.Row>>> _rowsOf;
        private readonly VENTARRANGE.Arrangement _work;

        // The levels as the main window holds them now; every level's rows
        // as the arrangement stands; each room's row, level and tick by key
        private IList<VENTWindow.LevelRow> _levels = new List<VENTWindow.LevelRow>();

        private Dictionary<string, List<VENTARRANGE.Row>> _all =
            new Dictionary<string, List<VENTARRANGE.Row>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, VENTARRANGE.Row> _rowOf = new Dictionary<string, VENTARRANGE.Row>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _levelOf = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, VENTWindow.RoomRow> _tickOf = new Dictionary<string, VENTWindow.RoomRow>(StringComparer.Ordinal);

        // The lines showing, top to bottom
        private List<Line> _lines = new List<Line>();

        // How each level stood before the search opened it (Search.Apply)
        private readonly Dictionary<string, bool> _openBefore = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly TextBox _search;
        private readonly TextBlock _searchHint;
        private readonly ListBox _list;
        private readonly Grid _heads;

        // The columns showing, as chosen (VENTCOLUMNS - Columns...)
        private readonly HashSet<string> _columns = VENTCOLUMNS.Load();

        // Differences only: the rows needing attention and nothing else
        private bool _differences;
        private CheckBox _onlyDifferences;
        private Button _columnsButton;
        private readonly TextBlock _said;

        private readonly Button _allOn;
        private readonly Button _noneOn;
        private readonly Button _show;
        private readonly Button _up;
        private readonly Button _down;
        private readonly Button _rename;
        private readonly Button _replace;
        private readonly Button _takeOut;
        private readonly Button _putBack;
        private readonly Button _merge;
        private readonly Button _unmerge;
        private readonly Button _reset;
        private readonly Button _write;
        private readonly Button _unenclosed;

        // WHAT THE MAIN WINDOW DOES for the presses that reach Revit, and is
        // told of every change
        public Action Arranged;                                     // save it, rebuild the levels
        public Action Ticked;
        public Action<List<VENTWindow.RoomRow>, int> ShowInPlan;    // the rooms, and how many picked elsewhere
        public Action<Dictionary<string, VENTFLOWS.Air>, HashSet<string>, HashSet<string>, bool> Write;

        // THE AIRFLOWS (VENTFLOWS, user 2026-10-19): each room's row in the
        // sheet and what its terminals carry now, by room key - READ ONLY
        // (user: "the flowrate from the sheet should be read only"). No
        // columns while there are neither.
        private IDictionary<string, VENTFLOWS.Sheet> _sheet;
        private IDictionary<string, VENTFLOWS.Air> _carrying;
        private IList<VENTFLOWS.Unit> _units;
        private bool _readingTerminals;

        // Each room's supply terminals whose ducts reach no unit, as the
        // survey traced them (REVITROOMS.TraceDucts) - marked on its row
        private IDictionary<string, List<long>> _unreached;

        public IDictionary<string, List<long>> Unreached
        {
            set { _unreached = value; }
        }

        // THE ROOMS WITH NO AREA in the models shown (user, 2026-10-24:
        // "add the list of not enclosed rooms") - asked again at every fill,
        // so rooms read since the window opened are counted
        private Func<List<VENTUNENCLOSED.Room>> _notEnclosedOf;

        public Func<List<VENTUNENCLOSED.Room>> NotEnclosed
        {
            set
            {
                _notEnclosedOf = value;

                if (IsLoaded)
                    SayNotEnclosed();
            }
        }

        // Each level's rooms in the order shown, and each room's group - a
        // merged group's key is its first room (VENTFCUCHECK.Groups)
        private Dictionary<string, List<string>> _orderOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // ...per SIDE since 2026-10-22: a fresh air merge is not an extract
        // merge (TA-04-04's kitchen exhaust is the kitchen's alone)
        private Dictionary<string, string> _freshOf = new Dictionary<string, string>(StringComparer.Ordinal);
        private Dictionary<string, string> _extractOf = new Dictionary<string, string>(StringComparer.Ordinal);
        private HashSet<string> _repeated = new HashSet<string>(StringComparer.Ordinal);

        // THE MERGES OF THE VIEW SHOWING, worked out once the window has
        // what they come from (Derive) - and the sheet's merges that cannot
        // be the arrangement's (across containers)
        private bool _derived;
        private int _refused;

        // THE MERGES THE FCUs SUGGEST (VENTFLOWS.FcuGroups) with the
        // arrangement as it stands
        private List<VENTFLOWS.FcuGroup> _fcu = new List<VENTFLOWS.FcuGroup>();
        private int _contested;

        // SHEET OR MODEL (user, 2026-10-19 - in place of a Merge by FCU
        // button; then: Sheet "should show the merged rooms according to
        // sheet", Model "according to model"). Sheet groups rooms as the
        // sheet merges them and writes the Sheet columns; Model groups them
        // as the FCUs serve them and writes the Model columns - what the
        // terminals carry, so grouped. Each worked out afresh when pressed
        // (View). The view showing is the arrangement's (ByModel, saved).
        private bool _byModel;
        private readonly Grid _views;
        private readonly ToggleButton _sheetView;
        private readonly ToggleButton _modelView;

        // What Write to terminals handed the main window last
        public Dictionary<string, VENTFLOWS.Air> Writes { get; private set; }
        public HashSet<string> WriteFresh { get; private set; }
        public HashSet<string> WriteExtract { get; private set; }

        // The write was the Model's: the terminals' own airflows, grouped as
        // the FCUs serve - the run is to leave the sheet's merges out
        public bool WroteModel { get; private set; }

        // THE OCCUPANCY AND ITS NEW AIRFLOWS (user, 2026-10-26): each room's
        // air with its occupancy as chosen here (_work.Occupancy - saved; a
        // room never touched follows the sheet), worked through the sheet's
        // own formulas by the main window (VENTWHATIF). Nothing here is
        // written to the workbook.
        private VENTFLOWS.Formulas _formulas;
        private Dictionary<string, VENTFLOWS.Calc> _calc = new Dictionary<string, VENTFLOWS.Calc>(StringComparer.Ordinal);
        private string _calcTrouble;

        private readonly Button _fromRules;
        private readonly Button _asSheet;

        // The editor open in a cell, and how to leave it as it was
        private FrameworkElement _editor;
        private Action _cancelEdit;

        // Every row as wide as the list or its columns (ApplyWidths), and
        // the headings moved with the list when it scrolls sideways
        private readonly TranslateTransform _headsShift = new TranslateTransform();
        private double _minRowWidth;

        private static readonly Brush Fcu = new SolidColorBrush(Color.FromRgb(0x6A, 0x4C, 0x93));

        // Rows whose sheet and model air differ; room numbers used twice
        private static readonly Brush Differs = new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xB8));
        private static readonly Brush Twice = new SolidColorBrush(Color.FromRgb(0xF9, 0xD0, 0xD0));

        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85));
        private static readonly Brush Faint = new SolidColorBrush(Color.FromRgb(0xA0, 0xA8, 0xB2));
        private static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xEA));
        private static readonly Brush Band = new SolidColorBrush(Color.FromRgb(0xEE, 0xF3, 0xF8));
        private static readonly Brush LevelBand = new SolidColorBrush(Color.FromRgb(0xDD, 0xE7, 0xF1));
        private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x16, 0x5E, 0x9B));
        private static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0x9A, 0x5B, 0x00));

        private const double FlowWidth = 56;

        // The room's own area and height, left of the airflows (user,
        // 2026-10-24: "show a column for the room area and height")
        private const double RoomWidth = 58;

        // THE LIST AS A SHEET (user, 2026-10-25: "make the rooms and level
        // window look more like excel view"): a grey gutter where Excel has
        // its row numbers - here the handle a row is dragged by - then one
        // cell a column, gridlines between, under a grey heading row built
        // from the same columns (ColumnsShowing, Header):
        //
        //   tick | Number | Name | Notes | Room | Occupancy | Sheet | New values | Model | Calculation
        //
        // Name and Notes share what is left, 4 : 5 - never less than
        // NameNotesWidth: past that the list scrolls sideways, and its
        // headings with it (ApplyWidths). A room's notes - merged with, one
        // FCU with, ungrouped from, terminal on no FCU, its model - are
        // their own column, not trailing its name.
        private const double GutterWidth = 24;
        private const double TickWidth = 28;
        private const double NumberWidth = 104;
        private const double NameShare = 4;
        private const double NotesShare = 5;
        private const double NameNotesWidth = 340;
        private const double RowHeight = 24;

        // The occupancy (user, 2026-10-26: chosen here as in the sheet), the
        // new values and the sheet's calculation
        private const double CategoryWidth = 128;
        private const double UseWidth = 96;
        private const double PeopleWidth = 66;
        private const double NewWidth = 64;
        private const double CalcWidth = 84;

        // Each block of cells, by what it holds
        private const string RoomTag = "room";
        private const string OccupancyTag = "occupancy";
        private const string SheetTag = "sheet";
        private const string NewTag = "new";
        private const string ModelTag = "model";
        private const string CalcTag = "calc";

        // A cell a double-click edits: an occupancy field, or a Model
        // airflow written to the terminals
        private const string EditTag = "edit:";
        private const string WriteTag = "write:";

        private static readonly Brush GridLine = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
        private static readonly Brush HeadFill = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));
        private static readonly Brush HeadLine = new SolidColorBrush(Color.FromRgb(0xBF, 0xBF, 0xBF));
        private static readonly Brush HeadInk = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
        private static readonly Brush LevelLine = new SolidColorBrush(Color.FromRgb(0xA9, 0xBC, 0xD0));
        private static readonly Brush ChosenFill = new SolidColorBrush(Color.FromRgb(0xE3, 0xEE, 0xF9));
        private const string ArrowTag = "arrow";

        // ONE COLUMN SHOWING (ColumnsShowing): the block it is in, the group
        // heading over it, what its cells hold and its heading
        private class Column
        {
            public string Tag;
            public string Group;
            public string Key;
            public string Head;
            public string Units;     // a second heading line - the calculation's
            public string Tip;
            public double Width;
            public int Col;          // a calculation column's in the sheet
        }

        private List<Column> _shown = new List<Column>();

        private static readonly Dictionary<string, string> GroupTips = new Dictionary<string, string>
        {
            ["Occupancy"] = "As the sheet has it until changed here - double-click a cell",
            ["Sheet"] = "As the sheet has it - read only",
            ["New values"] = "The sheet's own formulas with the occupancy here - what Model writes",
            ["Model"] = "L/s - on its terminals now; double-click to write",
            ["Calculation"] = "The sheet's columns, worked out with the occupancy here"
        };

        // "As the sheet" in a cell's list
        private static readonly object AsTheSheet = new object();

        // One line of the list: a level, a container, or a room
        private class Line
        {
            public bool IsLevel;
            public bool IsApartment;
            public string Key = string.Empty;        // the room's key, the container's, or the level's name
            public string Level = string.Empty;      // the level it is on
            public string Container = string.Empty;  // what it moves within
            public string UnitKey = string.Empty;    // its top row: itself, or its container
            public VENTARRANGE.Row Row;              // a room's row
            public VENTWindow.LevelRow LevelRow;     // a level's
            public VENTWindow.RoomRow Room;          // a room's tick
            public List<VENTWindow.RoomRow> Units = new List<VENTWindow.RoomRow>();   // a container's
            public CheckBox Box;                     // a container's box, set by hand
        }

        public VENTROOMSWINDOW(
            Func<IList<VENTWindow.LevelRow>> levels,
            Func<VENTARRANGE.Arrangement, Dictionary<string, List<VENTARRANGE.Row>>> rowsOf,
            VENTARRANGE.Arrangement arrangement,
            IDictionary<string, VENTFLOWS.Sheet> sheet = null,
            IDictionary<string, VENTFLOWS.Air> carrying = null,
            IList<VENTFLOWS.Unit> units = null,
            VENTFLOWS.Formulas formulas = null)
        {
            _levelsOf = levels ?? (() => new List<VENTWindow.LevelRow>());
            _rowsOf = rowsOf ?? (a => new Dictionary<string, List<VENTARRANGE.Row>>());
            _work = arrangement ?? new VENTARRANGE.Arrangement();
            _sheet = sheet;
            _carrying = carrying;
            _units = units;
            _formulas = formulas;

            Title = "AUTOVENT - levels and rooms";
            Width = 1600;
            Height = 720;
            MinWidth = 860;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF8));
            UseLayoutRounding = true;

            // No bigger than the screen - a laptop at 150%
            VENTFIT.Screen(this);

            var grid = new Grid { Margin = new Thickness(18) };

            foreach (GridLength height in new[]
                     {
                         GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto
                     })
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = height });
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Short, at the user's request: "do not overexplain things in the
            // autovent addin"
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10), LastChildFill = false };

            // THE SEARCH - the links card's box, the same placeholder trick:
            // a hint laid over the box, seen through it while it is empty
            var searchCell = new Grid { Width = 260 };

            _search = new TextBox
            {
                Padding = new Thickness(6, 3, 6, 3),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Level, number, name, model or redundant room (Ctrl+F). Esc clears it."
            };

            _searchHint = new TextBlock
            {
                Text = "Search rooms",
                Foreground = Faint,
                Margin = new Thickness(9, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            searchCell.Children.Add(_search);
            searchCell.Children.Add(_searchHint);

            DockPanel.SetDock(searchCell, Dock.Right);
            top.Children.Add(searchCell);

            // ONLY WHAT NEEDS ATTENTION (user, 2026-10-25) - hides rows, as
            // the search does; the two together: both
            _onlyDifferences = new CheckBox
            {
                Content = new TextBlock { Text = "Differences only" },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
                ToolTip = "Rows where sheet and model differ, a number used twice, a terminal on no FCU, no row in the sheet, or the area changed"
            };

            _onlyDifferences.Click += (s, e) =>
            {
                _differences = _onlyDifferences.IsChecked == true;
                Filter();
                Show(null);
            };

            DockPanel.SetDock(_onlyDifferences, Dock.Right);
            top.Children.Add(_onlyDifferences);

            // The columns, chosen as Excel hides them
            _columnsButton = new Button
            {
                Content = "Columns...",
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            _columnsButton.Click += (s, e) =>
            {
                ContextMenu menu = ColumnsMenu();
                menu.PlacementTarget = _columnsButton;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            };

            DockPanel.SetDock(_columnsButton, Dock.Right);
            top.Children.Add(_columnsButton);

            TextBlock hint = Hint("Tick what to process. Drag to reorder within a level or redundant room.");
            hint.Margin = new Thickness(0);
            hint.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(hint);

            Put(grid, 0, 0, 2, top);

            _list = new ListBox
            {
                SelectionMode = SelectionMode.Extended,
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                AllowDrop = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Auto);
            VirtualizingPanel.SetIsVirtualizing(_list, true);

            // THE HEADINGS, a sheet's: a row of groups - Room, Occupancy,
            // Sheet, New values, Model, Calculation - over a row of columns,
            // every cell as wide as the rows' cells: in by the list's border
            // and the 1 its template pads inside it (2), as wide as the rows
            // (ApplyWidths), and moved with them when the list scrolls
            // sideways
            _heads = new Grid
            {
                Margin = new Thickness(2, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                SnapsToDevicePixels = true,
                RenderTransform = _headsShift
            };

            _heads.RowDefinitions.Add(new RowDefinition());
            _heads.RowDefinitions.Add(new RowDefinition());

            // The list's own scroll viewer only - an editor's box has one too
            _list.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) =>
            {
                if (e.OriginalSource != FindViewer(_list))
                    return;

                _headsShift.X = -e.HorizontalOffset;

                if (e.ViewportWidthChange != 0)
                    ApplyWidths();
            }));

            Header(false);

            var withHeads = new DockPanel();
            var headsClip = new Border { ClipToBounds = true, Child = _heads };
            DockPanel.SetDock(headsClip, Dock.Top);
            withHeads.Children.Add(headsClip);
            withHeads.Children.Add(_list);

            Put(grid, 1, 0, 1, withHeads);

            var side = new StackPanel { Margin = new Thickness(12, 0, 0, 0), Width = 220 };

            Pair(side, "All", "None", out _allOn, out _noneOn);
            _allOn.ToolTip = "Tick every row showing";
            _noneOn.ToolTip = "Untick every row showing";

            _show = SideButton("Show in plan", side);
            _show.ToolTip = "The picked rooms in a plan of their level - or double-click a room";

            Pair(side, "Move up", "Move down", out _up, out _down).Margin = new Thickness(0, 12, 0, 0);
            _up.ToolTip = "Ctrl+Up";
            _down.ToolTip = "Ctrl+Down";

            _rename = SideButton("Rename...", side);
            _rename.ToolTip = "F2";
            _replace = SideButton("Find and replace...", side);

            // "Ungroup" / "Regroup" - the user's words (2026-10-12) for what
            // were "Take out of apartment" / "Put back in apartment"
            Pair(side, "Ungroup", "Regroup", out _takeOut, out _putBack).Margin = new Thickness(0, 12, 0, 0);
            _takeOut.ToolTip = "Out of its redundant room: a row of its own";
            _putBack.ToolTip = "Back under its redundant room";

            // Rooms one FCU serves through one fresh air connection (the
            // user, 2026-10-12)
            Pair(side, "Merge", "Unmerge", out _merge, out _unmerge);
            _merge.ToolTip = "One total of fresh air and extract";
            _unmerge.ToolTip = "Rows of their own again";

            // THE OCCUPANCY (user, 2026-10-26) - chosen in its cells; these
            // for many rooms at once: blank ones from the rules, or back to
            // the sheet's
            _fromRules = SideButton("Occupancy from rules", side);
            _fromRules.Margin = new Thickness(0, 12, 0, 6);
            _fromRules.ToolTip = "Blank categories of the picked rooms - or of every room showing - from the occupancy rules";
            _asSheet = SideButton("Occupancy as the sheet", side);
            _asSheet.ToolTip = "The picked rooms' occupancy back to the sheet's";

            // Sheet | Model - two buttons, one of them down. What they switch
            // is the GROUPING and what Write writes - both columns show
            // either way - so they say so (user, 2026-10-25)
            _views = new Grid { Margin = new Thickness(0, 12, 0, 6) };
            _views.ColumnDefinitions.Add(new ColumnDefinition());
            _views.ColumnDefinitions.Add(new ColumnDefinition());
            _views.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _views.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var mergeAs = new TextBlock
            {
                Text = "Merge and write as",
                Foreground = Muted,
                FontSize = 12,
                Margin = new Thickness(1, 0, 0, 3)
            };

            Grid.SetColumnSpan(mergeAs, 2);
            _views.Children.Add(mergeAs);

            // As the window was left (ByModel, saved)
            _byModel = _work.ByModel;

            _sheetView = new ToggleButton
            {
                Content = "Sheet",
                IsChecked = !_byModel,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 3, 0),
                ToolTip = "Merged as in the sheet; writes the sheet's airflows"
            };

            _modelView = new ToggleButton
            {
                Content = "Model",
                IsChecked = _byModel,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(3, 0, 0, 0),
                ToolTip = "Merged as the FCUs serve them; writes the new values"
            };

            Grid.SetColumn(_modelView, 1);
            Grid.SetRow(_sheetView, 1);
            Grid.SetRow(_modelView, 1);
            _views.Children.Add(_sheetView);
            _views.Children.Add(_modelView);
            side.Children.Add(_views);

            _reset = SideButton("Reset level", side);
            _reset.Margin = new Thickness(0, 12, 0, 6);
            _reset.ToolTip = "Forget the order, names, ungroups and merges of the picked rows' levels";

            // The airflows, written onto the terminals - the Sheet's or the
            // Model's, as the switch stands
            _write = SideButton("Write to terminals", side);
            _write.Margin = new Thickness(0, 12, 0, 6);
            _write.ToolTip = _byModel ? "The picked rooms' new values" : "The picked rooms' Sheet airflows";

            // Rooms the linked models have with no area - left out, listed
            // to be sent back
            _unenclosed = SideButton(VENTUNENCLOSED.Said(0), side);
            _unenclosed.Margin = new Thickness(0, 12, 0, 6);
            _unenclosed.ToolTip = "Rooms with no area in the linked models - not read, no airflows";
            _unenclosed.IsEnabled = false;
            _unenclosed.Click += (s, e) => NotEnclosedList().ShowDialog();

            // Scrolls rather than clips when the window is short
            Put(grid, 1, 1, 1, new ScrollViewer
            {
                Content = side,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });

            _said = Hint(string.Empty);
            _said.Margin = new Thickness(0, 8, 0, 0);
            Put(grid, 2, 0, 2, _said);

            var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };

            var close = new Button { Content = "Close", Width = 90, Padding = new Thickness(0, 4, 0, 4) };
            close.Click += (s, e) => Close();

            DockPanel.SetDock(close, Dock.Right);
            bottom.Children.Add(close);

            Put(grid, 3, 0, 2, bottom);

            Content = grid;

            // ---- what the presses do
            _allOn.Click += (s, e) => TickShowing(true);
            _noneOn.Click += (s, e) => TickShowing(false);
            _show.Click += (s, e) => ShowPicked();
            _up.Click += (s, e) => MovePicked(-1);
            _down.Click += (s, e) => MovePicked(+1);
            _takeOut.Click += (s, e) => TakeOutPicked();
            _putBack.Click += (s, e) => PutBackPicked();
            _merge.Click += (s, e) => MergePicked();
            _unmerge.Click += (s, e) => UnmergePicked();
            _reset.Click += (s, e) => ResetPicked();
            _rename.Click += (s, e) => RenamePicked();
            _replace.Click += (s, e) => ReplaceNames();
            _write.Click += (s, e) => WriteNow();
            _sheetView.Click += (s, e) => View(false);
            _modelView.Click += (s, e) => View(true);
            _fromRules.Click += (s, e) => FromRulesPicked();
            _asSheet.Click += (s, e) => AsSheetPicked();

            _list.SelectionChanged += (s, e) =>
            {
                if (!_showing)
                    Enable();
            };

            // A double-click on a level opens or closes it; on an occupancy
            // cell, chooses it; on a Model airflow, writes it; elsewhere on a
            // room or container, shows it in plan
            _list.MouseDoubleClick += (s, e) =>
            {
                var from = e.OriginalSource as DependencyObject;
                ListBoxItem item = ItemAt(from);

                if (item == null || _editor != null || Inside<ToggleButton>(from) || OnTag(from, ArrowTag) ||
                    Inside<TextBox>(from) || Inside<ComboBox>(from))
                    return;

                var line = (Line)item.Tag;

                e.Handled = true;

                if (line.IsLevel)
                {
                    Toggle(line.LevelRow);
                    return;
                }

                Border cell = EditableAt(from);

                if (cell != null && line.Row != null)
                {
                    string tag = (string)cell.Tag;

                    if (tag.StartsWith(EditTag, StringComparison.Ordinal))
                        BeginEdit(line, tag.Substring(EditTag.Length), cell);
                    else
                        BeginWrite(line, tag.Substring(WriteTag.Length) == VENTFLOWS.ExtractSide, cell);

                    return;
                }

                List<VENTWindow.RoomRow> rooms = RoomsOf(line);

                if (rooms.Count > 0 && ShowInPlan != null)
                {
                    _said.Text = "Showing " + rooms.Count + " room(s)...";
                    ShowInPlan(rooms, 0);
                }
            };

            _list.PreviewKeyDown += (s, e) =>
            {
                // An editor open in a cell has the keys
                if (_editor != null)
                    return;

                if (e.Key == Key.F2)
                {
                    RenamePicked();
                    e.Handled = true;
                    return;
                }

                // Space ticks the picks - as a box on a picked row does. A
                // focused box toggles itself.
                if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None && !(e.OriginalSource is ToggleButton))
                {
                    List<Line> picked = Picked();

                    if (picked.Count > 0)
                    {
                        TickPicked(ClickMeans(picked[0]));
                        Ticks();
                    }

                    e.Handled = true;
                    return;
                }

                // Right opens the picked levels, Left closes them
                if ((e.Key == Key.Right || e.Key == Key.Left) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    List<VENTWindow.LevelRow> levels = Picked().Where(l => l.IsLevel).Select(l => l.LevelRow).ToList();

                    if (levels.Count > 0)
                    {
                        foreach (VENTWindow.LevelRow level in levels)
                            level.IsExpanded = e.Key == Key.Right;

                        Show(null);
                        e.Handled = true;
                    }

                    return;
                }

                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                    return;

                if (e.Key == Key.Up) { MovePicked(-1); e.Handled = true; }
                else if (e.Key == Key.Down) { MovePicked(+1); e.Handled = true; }
            };

            Dragging();

            _search.TextChanged += (s, e) =>
            {
                _searchHint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

                Filter();
                Show(null);
            };

            // Esc clears the search; with nothing typed it closes the window.
            // On the WINDOW: a preview key reaches it before the box
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    _search.Focus();
                    _search.SelectAll();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    if (_cancelEdit != null)
                        _cancelEdit();
                    else if (_search.Text.Length > 0)
                        _search.Clear();
                    else
                        Close();

                    e.Handled = true;
                }
            };

            // A level open to start with: the first with rooms, unless the
            // list was left with one open
            IList<VENTWindow.LevelRow> first = _levelsOf() ?? new List<VENTWindow.LevelRow>();

            if (!first.Any(l => l.IsExpanded))
            {
                VENTWindow.LevelRow open = first.FirstOrDefault(l => l.Rooms.Count > 0);

                if (open != null)
                    open.IsExpanded = true;
            }

            Fill(null);

            // The merges of the button that is down, worked out - once the
            // main window is listening
            Loaded += (s, e) => Derive();
        }

        private static void Put(Grid grid, int row, int column, int span, UIElement element)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            Grid.SetColumnSpan(element, span);
            grid.Children.Add(element);
        }

        private static TextBlock Hint(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

        private static Button SideButton(string text, Panel into)
        {
            var button = new Button
            {
                Content = text,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            into.Children.Add(button);

            return button;
        }

        // Two buttons side by side, half the column each
        private static Grid Pair(Panel into, string left, string right, out Button first, out Button second)
        {
            var pair = new Grid { Margin = new Thickness(0) };

            pair.ColumnDefinitions.Add(new ColumnDefinition());
            pair.ColumnDefinitions.Add(new ColumnDefinition());

            var a = new StackPanel();
            var b = new StackPanel();

            first = SideButton(left, a);
            second = SideButton(right, b);

            first.Margin = new Thickness(0, 0, 3, 6);
            second.Margin = new Thickness(3, 0, 0, 6);

            Grid.SetColumn(b, 1);
            pair.Children.Add(a);
            pair.Children.Add(b);

            into.Children.Add(pair);

            return pair;
        }

        // ==================================================
        // What the main window tells it
        // ==================================================

        // THE LEVELS REBUILT (a link ticked, a sheet loaded, an arrangement
        // saved): shown again on them, with the sheet's rows as they now
        // match
        public void Refill(IDictionary<string, VENTFLOWS.Sheet> sheet)
        {
            _sheet = sheet;

            // A change made here is shown once it is saved (Changed)
            if (_arranging)
                return;

            Fill(null);

            // A sheet loaded since the window opened, on Sheet: its merges now
            if (IsLoaded && !_derived)
                Dispatcher.BeginInvoke(new Action(Derive));
        }

        // ...and what the new air is worked out with now: a sheet read again
        // is another workbook (VENTWHATIF)
        public void Refill(IDictionary<string, VENTFLOWS.Sheet> sheet, VENTFLOWS.Formulas formulas)
        {
            _formulas = formulas;

            Refill(sheet);
        }

        // WHAT THE TERMINALS CARRY, and the units, as the run reads them
        public void Terminals(IDictionary<string, VENTFLOWS.Air> carrying, IList<VENTFLOWS.Unit> units,
                              IDictionary<string, List<long>> unreached = null)
        {
            _carrying = carrying;
            _units = units;
            _unreached = unreached;
            _readingTerminals = false;

            Fill(null);

            // Opened on Model: its merges, now they can be worked out
            if (IsLoaded && !_derived)
                Derive();
        }

        // ...asked for: the Model columns wait for them
        public void ReadingTerminals()
        {
            _readingTerminals = true;

            Show(null);
        }

        // A line from Revit (Show in plan's answer, a write's)
        public void Said(string what)
        {
            _said.Text = what ?? string.Empty;
        }

        protected override void OnClosed(EventArgs e)
        {
            // Nothing hidden once nobody can see the search
            VENTWindow.Search.Apply(_levels, string.Empty, _openBefore, Words);

            base.OnClosed(e);
        }

        // ==================================================
        // The list
        // ==================================================

        private bool _showing;
        private bool _arranging;

        private string Query
        {
            get { return _search == null ? string.Empty : _search.Text ?? string.Empty; }
        }

        private bool Searching
        {
            get { return !string.IsNullOrWhiteSpace(Query) || _differences; }
        }

        // The search typed, and Differences only, over the rows
        private void Filter()
        {
            VENTWindow.Search.Apply(_levels, Query, _openBefore, Words,
                _differences ? (Func<VENTWindow.LevelRow, VENTWindow.RoomRow, bool>)NeedsAttention : null);
        }

        private bool NeedsAttention(VENTWindow.LevelRow level, VENTWindow.RoomRow room)
        {
            VENTARRANGE.Row row;

            return room != null && _rowOf.TryGetValue(room.Key, out row) && Attention(row).Count > 0;
        }

        // WHAT A ROW NEEDS LOOKING AT - "Differences only" shows these and
        // its tooltip says which
        private List<string> Attention(VENTARRANGE.Row row)
        {
            var why = new List<string>();

            if (row == null)
                return why;

            if (_repeated.Contains(row.Key))
                why.Add("its number is used twice on the level");

            if (_sheet != null && _carrying != null && RowColour(row, true) == Differs)
                why.Add(_byModel ? "new values and model airflows differ" : "sheet and model airflows differ");

            if (_sheet != null && Changes(row.Key))
                why.Add("new values differ from the sheet");

            List<long> unreached;

            if (_unreached != null && _unreached.TryGetValue(row.Key, out unreached) && unreached.Count > 0)
                why.Add("a terminal on no FCU");

            VENTFLOWS.Sheet sheet = SheetRow(row.Key);

            if (_sheet != null && sheet == null)
                why.Add("no row in the sheet");

            if (sheet != null && VENTFLOWS.AreaChanged(sheet.AreaM2, row.AreaM2))
                why.Add("its area changed since the sheet");

            return why;
        }

        private VENTFLOWS.Sheet SheetRow(string key)
        {
            VENTFLOWS.Sheet row;

            return _sheet != null && key != null && _sheet.TryGetValue(key, out row) ? row : null;
        }

        private bool On(string column)
        {
            return _columns.Contains(column);
        }

        // Columns...: each optional column on or off, remembered
        private ContextMenu ColumnsMenu()
        {
            var menu = new ContextMenu();

            foreach (VENTCOLUMNS.Choice column in VENTCOLUMNS.All)
            {
                var item = new MenuItem
                {
                    Header = new TextBlock { Text = column.Label },
                    IsCheckable = true,
                    IsChecked = On(column.Key),
                    StaysOpenOnClick = true
                };

                string key = column.Key;

                item.Click += (s, e) => ShowColumn(key, item.IsChecked);

                menu.Items.Add(item);
            }

            return menu;
        }

        private void ShowColumn(string key, bool on)
        {
            if (on)
                _columns.Add(key);
            else
                _columns.Remove(key);

            VENTCOLUMNS.Save(_columns);

            Show(null);
        }

        // Columns while there is a sheet, terminals, or terminals coming
        private bool Flows
        {
            get { return _sheet != null || _carrying != null || _readingTerminals; }
        }

        // What a search finds a room by: its level and everything the
        // arrange list knew it by (VENTARRANGE.Words)
        private string Words(VENTWindow.LevelRow level, VENTWindow.RoomRow room)
        {
            VENTARRANGE.Row row;

            return room != null && _rowOf.TryGetValue(room.Key, out row)
                ? (level == null ? string.Empty : level.Name) + " " + VENTARRANGE.Words(row)
                : VENTWindow.Search.Words(level, room);
        }

        private static List<VENTARRANGE.Row> RowsOn(Dictionary<string, List<VENTARRANGE.Row>> all, string level)
        {
            List<VENTARRANGE.Row> rows;

            return level != null && all != null && all.TryGetValue(level, out rows) && rows != null
                ? rows
                : new List<VENTARRANGE.Row>();
        }

        private VENTWindow.RoomRow TickOf(string key)
        {
            VENTWindow.RoomRow room;

            return key != null && _tickOf.TryGetValue(key, out room) ? room : null;
        }

        // Everything worked out afresh - the levels as the main window holds
        // them, every level's rows with the arrangement as it now stands -
        // and shown again
        private void Fill(IEnumerable<string> pick)
        {
            _levels = _levelsOf() ?? new List<VENTWindow.LevelRow>();
            _all = _rowsOf(_work) ?? new Dictionary<string, List<VENTARRANGE.Row>>(StringComparer.OrdinalIgnoreCase);

            _rowOf.Clear();
            _levelOf.Clear();
            _tickOf.Clear();

            foreach (KeyValuePair<string, List<VENTARRANGE.Row>> level in _all)
            {
                foreach (VENTARRANGE.Row row in level.Value)
                {
                    _rowOf[row.Key] = row;
                    _levelOf[row.Key] = level.Key;
                }
            }

            foreach (VENTWindow.LevelRow level in _levels)
            {
                foreach (VENTWindow.RoomRow room in level.Rooms)
                    _tickOf[room.Key] = room;
            }

            Group();
            Suggest();
            Recalculate();

            // A rebuilt list keeps the search typed over it, and the filter
            Filter();

            Show(pick);

            SayNotEnclosed();
        }

        // THE NEW AIR with the occupancy as it stands - every room's row back
        // from the sheet's formulas; none without a workbook to work it with
        private void Recalculate()
        {
            _calc = new Dictionary<string, VENTFLOWS.Calc>(StringComparer.Ordinal);
            _calcTrouble = null;

            if (_formulas == null || _formulas.Calculate == null || _sheet == null)
                return;

            try
            {
                _calc = _formulas.Calculate(_work.Occupancy) ?? _calc;
            }
            catch (Exception ex)
            {
                _calcTrouble = ex.Message;
            }
        }

        // Each level's groups, per side, as the run makes them: the sheet's
        // merged cells of that side and the arrangement's merges of it - or,
        // on Model, the FCUs' fresh air merges alone (extract: none)
        private void Group()
        {
            _orderOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _freshOf = new Dictionary<string, string>(StringComparer.Ordinal);
            _extractOf = new Dictionary<string, string>(StringComparer.Ordinal);
            _repeated = new HashSet<string>(StringComparer.Ordinal);

            IDictionary<string, VENTFLOWS.Sheet> sheet = _byModel ? null : _sheet;

            foreach (KeyValuePair<string, List<VENTARRANGE.Row>> level in _all)
            {
                List<string> order = LevelLines(level.Key, level.Value).Where(l => l.Row != null).Select(l => l.Key).ToList();

                _orderOf[level.Key] = order;

                foreach (KeyValuePair<string, string> group in VENTFLOWS.GroupsOf(order, sheet, _work.Merges, VENTFLOWS.FreshSide))
                    _freshOf[group.Key] = group.Value;

                foreach (KeyValuePair<string, string> group in VENTFLOWS.GroupsOf(order, sheet, _work.ExtractMerges, VENTFLOWS.ExtractSide))
                    _extractOf[group.Key] = group.Value;

                _repeated.UnionWith(VENTFLOWS.Repeated(level.Value));
            }
        }

        // What the FCUs suggest, with the arrangement as it now stands
        private void Suggest()
        {
            _fcu = _units == null
                ? new List<VENTFLOWS.FcuGroup>()
                : VENTFLOWS.FcuGroups(_units, _all, _work, out _contested);
        }

        // SHEET OR MODEL - THE MERGES SHOWN ARE ALWAYS THE BUTTON'S, worked
        // out afresh (user, 2026-10-19/20: "when pressing on the model
        // button, it should be showing the spaces from the model that are
        // merged, when pressing on the sheet button ... from the sheet"):
        //
        //   - SHEET: the sheet's merged rooms (FromSheet), each side as the
        //     sheet merges its cells;
        //   - MODEL: the FCUs' groups and nothing else, from the terminals as
        //     last read (VENTFLOWS.ByFcu over no merges) - FRESH AIR ONLY
        //     (user, 2026-10-22): extract is the sheet's to merge, not an
        //     FCU's.
        //
        // On every press, and when the window opens (Derive) - so merges
        // saved by an older version, or made by hand (Merge / Unmerge, which
        // last until then), never stand in for the sheet's or the model's.
        private void View(bool model)
        {
            _sheetView.IsChecked = !model;
            _modelView.IsChecked = model;

            _write.ToolTip = model ? "The picked rooms' new values" : "The picked rooms' Sheet airflows";

            Take(MergesOf(model));

            _work.ByModel = _byModel = model;
            _derived = true;

            Changed(null);
        }

        // The view's merges, both sides - in an arrangement of their own
        private VENTARRANGE.Arrangement MergesOf(bool model)
        {
            if (!model)
                return FromSheet();

            int contested;

            var fcus = new VENTARRANGE.Arrangement();
            fcus.Merges.AddRange(VENTFLOWS.ByFcu(null, _units, _all, out contested));

            return fcus;
        }

        private void Take(VENTARRANGE.Arrangement merges)
        {
            _work.Merges.Clear();
            _work.Merges.AddRange(merges.Merges);

            _work.ExtractMerges.Clear();
            _work.ExtractMerges.AddRange(merges.ExtractMerges);
        }

        // The sheet's merged rooms as merges, each side - those within one
        // container; those across containers are grouped from the sheet
        // itself (Group)
        private VENTARRANGE.Arrangement FromSheet()
        {
            var sheet = new VENTARRANGE.Arrangement();

            if (_sheet != null)
                VENTFLOWS.Adopt(sheet, _all.Values.SelectMany(r => r).ToList(), _sheet, out _refused);

            return sheet;
        }

        // ON OPENING, the merges of the button that is down - once what they
        // come from is here: the sheet on Sheet, the terminals on Model.
        // Until then the merges saved stand. Saved only if they changed.
        private void Derive()
        {
            if (_derived || (_byModel ? _units == null : _sheet == null))
                return;

            _derived = true;

            VENTARRANGE.Arrangement merges = MergesOf(_byModel);

            if (Same(merges.Merges, _work.Merges) && Same(merges.ExtractMerges, _work.ExtractMerges))
            {
                Count();
                return;
            }

            Take(merges);

            Changed(null);
        }

        // The same groups, in any order
        private static bool Same(IList<List<string>> a, IList<List<string>> b)
        {
            Func<IList<List<string>>, HashSet<string>> keys = groups => new HashSet<string>(
                groups.Select(g => string.Join("|", g.OrderBy(k => k, StringComparer.Ordinal))), StringComparer.Ordinal);

            return a.Count == b.Count && keys(a).SetEquals(keys(b));
        }

        // A CHANGE TO THE ARRANGEMENT: the main window saves it and rebuilds
        // its levels (Arranged), then the list is shown again - picking
        // `pick`, else what was picked
        private void Changed(IEnumerable<string> pick)
        {
            List<string> keep = pick == null ? null : pick.ToList();

            _arranging = true;

            try
            {
                if (Arranged != null)
                    Arranged();
            }
            finally
            {
                _arranging = false;
            }

            Fill(keep);
        }

        // A line's name for keeping it picked across a rebuild
        private static string IdOf(Line line)
        {
            return line.IsLevel ? "level " + line.Key
                 : line.IsApartment ? "apt " + line.Key
                 : line.Key;
        }

        // The list rebuilt from what is held - keeping the rows that were
        // picked, or picking `pick` if given, and where it was scrolled to
        private void Show(IEnumerable<string> pick)
        {
            var picked = new HashSet<string>(pick ?? Picked().Select(IdOf), StringComparer.Ordinal);

            // An editor open is gone with its row
            EditorGone();

            ScrollViewer viewer = FindViewer(_list);
            double offset = viewer == null ? 0 : viewer.VerticalOffset;

            bool flows = Flows;

            // The headings for the columns showing now
            Header(flows);
            _views.Visibility = _units == null ? Visibility.Collapsed : Visibility.Visible;

            _showing = true;

            try
            {
                _list.Items.Clear();

                _lines = Lines();

                foreach (Line line in _lines)
                {
                    var item = new ListBoxItem
                    {
                        Content = Draw(line, flows),
                        Tag = line,
                        Padding = new Thickness(0),
                        BorderThickness = new Thickness(0),
                        BorderBrush = Brushes.Transparent
                    };

                    // Colours on the ITEM, under its content, so a picked row
                    // still shows picked
                    string tip = null;

                    if (line.IsLevel)
                    {
                        item.Background = LevelBand;
                        tip = line.LevelRow.Tip;
                    }
                    else if (line.IsApartment)
                    {
                        item.Background = Band;
                    }
                    else
                    {
                        Brush colour = RowColour(line.Row, flows);

                        if (colour != null)
                        {
                            item.Background = colour;

                            if (colour == Twice)
                                tip = "Room number " + line.Row.Number + " used twice on " + line.Level;
                        }

                        if (line.Room != null && !string.IsNullOrEmpty(line.Room.Tip))
                            tip = tip == null ? line.Room.Tip : tip + Environment.NewLine + line.Room.Tip;

                        // Differences only: why it is showing
                        if (_differences)
                        {
                            List<string> why = Attention(line.Row);

                            if (why.Count > 0)
                                tip = (tip == null ? string.Empty : tip + Environment.NewLine) + "Needs attention: " + string.Join("; ", why);
                        }
                    }

                    item.ToolTip = tip;

                    _list.Items.Add(item);

                    if (picked.Contains(IdOf(line)))
                        item.IsSelected = true;
                }
            }
            finally
            {
                _showing = false;
            }

            ApplyWidths();

            if (viewer != null)
                viewer.ScrollToVerticalOffset(offset);

            Count();
            Enable();
        }

        // EVERY LINE SHOWING: each level the search leaves, and under each
        // open one its rooms and containers as the sheet will have them
        private List<Line> Lines()
        {
            var lines = new List<Line>();

            foreach (VENTWindow.LevelRow level in _levels)
            {
                if (!level.IsShown)
                    continue;

                lines.Add(new Line { IsLevel = true, Key = level.Name ?? string.Empty, Level = level.Name ?? string.Empty, LevelRow = level });

                if (!level.IsExpanded)
                    continue;

                List<Line> mine = LevelLines(level.Name, RowsOn(_all, level.Name));

                foreach (Line line in mine)
                {
                    if (line.Row != null)
                    {
                        line.Room = TickOf(line.Key);

                        if (line.Room != null && !line.Room.IsShown)
                            continue;
                    }
                    else if (line.IsApartment)
                    {
                        line.Units = mine
                            .Where(l => l.Row != null && l.UnitKey == line.Key)
                            .Select(l => TickOf(l.Key))
                            .Where(r => r != null)
                            .ToList();

                        if (line.Units.Count > 0 && !line.Units.Any(r => r.IsShown))
                            continue;
                    }

                    lines.Add(line);
                }
            }

            return lines;
        }

        // One level's rooms and containers, in the order the sheet has them
        private static List<Line> LevelLines(string level, IList<VENTARRANGE.Row> rows)
        {
            var lines = new List<Line>();

            string own = VENTARRANGE.LevelContainer(level);

            foreach (VENTARRANGE.Unit<VENTARRANGE.Row> unit in VENTARRANGE.Units(rows, r => r.Key, r => r.ApartmentKey))
            {
                if (!unit.IsApartment)
                {
                    lines.Add(new Line
                    {
                        Key = unit.Key,
                        Level = level,
                        Container = own,
                        UnitKey = unit.Key,
                        Row = unit.Members[0]
                    });

                    continue;
                }

                lines.Add(new Line
                {
                    IsApartment = true,
                    Key = unit.Key,
                    Level = level,
                    Container = own,
                    UnitKey = unit.Key
                });

                foreach (VENTARRANGE.Row room in unit.Members)
                {
                    lines.Add(new Line
                    {
                        Key = room.Key,
                        Level = level,
                        Container = VENTARRANGE.ApartmentContainer(unit.Key),
                        UnitKey = unit.Key,
                        Row = room
                    });
                }
            }

            return lines;
        }

        // A room's text: Ink while ticked, faint while not - bound, so a tick
        // changes it without the list being rebuilt
        private class Dim : IValueConverter
        {
            private readonly Brush _on;

            public Dim(Brush on)
            {
                _on = on;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return value is bool && !(bool)value ? Faint : _on;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        }

        private static readonly Dim InkUnlessOff = new Dim(Ink);
        private static readonly Dim MutedUnlessOff = new Dim(Muted);

        private static void Dimmed(TextBlock text, VENTWindow.RoomRow room, Dim dim, Brush plain)
        {
            if (room == null)
            {
                text.Foreground = plain;
                return;
            }

            text.SetBinding(TextBlock.ForegroundProperty, new Binding("IsOn") { Source = room, Mode = BindingMode.OneWay, Converter = dim });
        }

        // ==================================================
        // The sheet's cells
        // ==================================================

        // One cell: a gridline on its right and under it. NaN wide fills.
        private static Border Cell(UIElement child, double width = double.NaN, bool centred = false)
        {
            return new Border
            {
                Width = width,
                MinHeight = RowHeight,
                BorderBrush = GridLine,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = centred ? new Thickness(0) : new Thickness(6, 0, 6, 0),
                Child = child,
                SnapsToDevicePixels = true
            };
        }

        // The gutter: Excel's row-number column - the handle here
        private static Border Gutter(UIElement child)
        {
            return new Border
            {
                Width = GutterWidth,
                MinHeight = RowHeight,
                Background = HeadFill,
                BorderBrush = HeadLine,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = child,
                SnapsToDevicePixels = true
            };
        }

        private static TextBlock CellText(string text, Brush ink)
        {
            return new TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = ink,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // Name and Notes, sharing what the fixed columns leave - the name
        // alone when Notes is hidden (null)
        private static Grid Shared(UIElement name, UIElement notes)
        {
            var middle = new Grid();

            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NameShare, GridUnitType.Star) });

            Grid.SetColumn(name, 0);
            middle.Children.Add(name);

            if (notes != null)
            {
                middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NotesShare, GridUnitType.Star) });

                Grid.SetColumn(notes, 1);
                middle.Children.Add(notes);
            }

            return middle;
        }

        // A note in the Notes column: the notes before it parted by a dot
        private static void Note(TextBlock notes, System.Windows.Documents.Run run)
        {
            if (notes.Inlines.Count > 0)
                notes.Inlines.Add(new System.Windows.Documents.Run("  ·  ") { Foreground = Faint });

            notes.Inlines.Add(run);
        }

        // THE COLUMNS SHOWING, left to right after the name and notes - as
        // chosen (Columns...) and as there is something to show in them
        private List<Column> ColumnsShowing(bool flows)
        {
            var columns = new List<Column>();

            void Add(string tag, string group, string key, string head, double width, string tip = null)
            {
                columns.Add(new Column { Tag = tag, Group = group, Key = key, Head = head, Width = width, Tip = tip });
            }

            bool sheet = _sheet != null;

            if (On(VENTCOLUMNS.Room))
            {
                Add(RoomTag, "Room", "area", "m²", RoomWidth, "Area");
                Add(RoomTag, "Room", "height", "m", RoomWidth, "Height - to the false ceiling, else the room's own");
            }

            if (sheet && On(VENTCOLUMNS.Categories))
            {
                Add(OccupancyTag, "Occupancy", VENTFLOWS.FreshCategoryField, "Fresh category", CategoryWidth, "Occupancy category, Table 6-1 - double-click to change");
                Add(OccupancyTag, "Occupancy", VENTFLOWS.ExhaustCategoryField, "Extract category", CategoryWidth, "Occupancy category, Table 6-4 - double-click to change");
                Add(OccupancyTag, "Occupancy", VENTFLOWS.UseField, "Use", UseWidth, "Nature of use / operation - double-click to change");
            }

            if (sheet && On(VENTCOLUMNS.People))
            {
                Add(OccupancyTag, "Occupancy", VENTFLOWS.OccupantsField, "Occupants", PeopleWidth, "No. of occupants, N1 - double-click to change");
                Add(OccupancyTag, "Occupancy", VENTFLOWS.UnitsField, "Units", PeopleWidth, "No. of units - double-click to change");
            }

            if (sheet && On(VENTCOLUMNS.SheetArea))
                Add(SheetTag, "Sheet", "sheet-area", "m²", RoomWidth, "Area in the sheet");

            if (flows)
            {
                Add(SheetTag, "Sheet", "sheet-fresh", "Fresh", FlowWidth);
                Add(SheetTag, "Sheet", "sheet-extract", "Extract", FlowWidth);
            }

            if (sheet && flows)
            {
                Add(NewTag, "New values", "new-fresh", "Fresh air", NewWidth, "L/s - with the occupancy here, by the sheet's formulas");
                Add(NewTag, "New values", "new-extract", "Exhaust air", NewWidth, "L/s - with the occupancy here, by the sheet's formulas");
            }

            if (flows)
            {
                Add(ModelTag, "Model", "now-fresh", "Fresh", FlowWidth, "On its terminals now - double-click to write");
                Add(ModelTag, "Model", "now-extract", "Extract", FlowWidth, "On its terminals now - double-click to write");
            }

            if (sheet && On(VENTCOLUMNS.Calculation) && _formulas != null && _formulas.Columns != null)
            {
                foreach (VENTFLOWS.CalcColumn calc in _formulas.Columns)
                {
                    columns.Add(new Column
                    {
                        Tag = CalcTag,
                        Group = "Calculation",
                        Key = "calc",
                        Head = calc.Label,
                        Units = calc.Letter + (string.IsNullOrEmpty(calc.Units) ? string.Empty : " · " + calc.Units),
                        Tip = "Column " + calc.Letter + ": " + calc.Heading,
                        Width = CalcWidth,
                        Col = calc.Col
                    });
                }
            }

            return columns;
        }

        // THE HEADINGS (see RowHeight): the same cells as a row, grey -
        // built again whenever the list is, for the columns showing
        private void Header(bool flows)
        {
            _heads.Children.Clear();

            _shown = ColumnsShowing(flows);

            Border Head(string text, double width, bool group, string tip = null, string second = null)
            {
                var line = new TextBlock
                {
                    Text = text,
                    Foreground = group ? Ink : HeadInk,
                    FontWeight = group ? FontWeights.SemiBold : FontWeights.Normal,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                UIElement child = line;

                if (second != null)
                {
                    var two = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                    two.Children.Add(line);
                    two.Children.Add(new TextBlock
                    {
                        Text = second,
                        Foreground = Faint,
                        FontSize = 10,
                        TextAlignment = TextAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    child = two;
                }

                return new Border
                {
                    Width = width,
                    MinHeight = 22,
                    Background = HeadFill,
                    BorderBrush = HeadLine,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    SnapsToDevicePixels = true,
                    ToolTip = tip,
                    Child = child
                };
            }

            // Groups: over the number, name and notes nothing; over each run
            // of columns its group - Room, Occupancy, Sheet, ...
            var groups = new DockPanel();

            groups.Children.Add(Head(string.Empty, GutterWidth, true));
            groups.Children.Add(Head(string.Empty, TickWidth, true));
            groups.Children.Add(Head(string.Empty, NumberWidth, true));

            var groupCells = new StackPanel { Orientation = Orientation.Horizontal };

            for (int i = 0; i < _shown.Count;)
            {
                int j = i;
                double width = 0;

                for (; j < _shown.Count && _shown[j].Group == _shown[i].Group; j++)
                    width += _shown[j].Width;

                string tip;
                GroupTips.TryGetValue(_shown[i].Group, out tip);

                groupCells.Children.Add(Head(_shown[i].Group, width, true, tip));

                i = j;
            }

            DockPanel.SetDock(groupCells, Dock.Right);
            groups.Children.Add(groupCells);

            groups.Children.Add(Head(string.Empty, double.NaN, true));

            // Columns, left to right after the name and notes
            var columns = new DockPanel();

            columns.Children.Add(Head(string.Empty, GutterWidth, false));
            columns.Children.Add(Head(string.Empty, TickWidth, false));
            columns.Children.Add(Head("Number", NumberWidth, false));

            var columnCells = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (Column column in _shown)
                columnCells.Children.Add(Head(column.Head, column.Width, false, column.Tip, column.Units));

            DockPanel.SetDock(columnCells, Dock.Right);
            columns.Children.Add(columnCells);

            columns.Children.Add(Shared(Head("Name", double.NaN, false), On(VENTCOLUMNS.Notes) ? Head("Notes", double.NaN, false) : null));

            Grid.SetRow(groups, 0);
            Grid.SetRow(columns, 1);

            _heads.Children.Add(groups);
            _heads.Children.Add(columns);

            _minRowWidth = GutterWidth + TickWidth + NumberWidth + _shown.Sum(c => c.Width) +
                           (On(VENTCOLUMNS.Notes) ? NameNotesWidth : NameNotesWidth / 2);
        }

        // What one line looks like. Text in TextBlocks only: an underscore
        // in Content is an access key, and Revit names are full of them.
        private UIElement Draw(Line line, bool flows)
        {
            if (line.IsLevel)
                return DrawLevel(line);

            var panel = new DockPanel();

            panel.Children.Add(Gutter(new TextBlock
            {
                Text = "≡",
                Foreground = Faint,
                ToolTip = "Drag to move",
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }));

            if (line.IsApartment)
            {
                List<VENTARRANGE.Row> rows = RowsOn(_all, line.Level);
                VENTARRANGE.Row first = rows.FirstOrDefault(r => r.ApartmentKey == line.Key);
                int units = rows.Count(r => r.ApartmentKey == line.Key);

                var box = new CheckBox
                {
                    IsThreeState = false,
                    IsChecked = State(line.Units),
                    IsEnabled = line.Units.Count > 0,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                box.Click += (s, e) => ContainerClicked(line);
                line.Box = box;
                panel.Children.Add(Cell(box, TickWidth, true));

                TextBlock itsNumber = CellText(first == null ? string.Empty : first.ApartmentNumber, Muted);
                itsNumber.FontWeight = FontWeights.SemiBold;
                panel.Children.Add(Cell(itsNumber, NumberWidth));

                // Its blocks, empty - the gridlines go on
                Blocks(panel, null);

                var head = CellText(null, Ink);

                head.Inlines.Add(new System.Windows.Documents.Run(first == null ? line.Key : first.Apartment)
                {
                    FontWeight = FontWeights.SemiBold
                });

                if (first != null && first.Apartment != first.ModelApartment)
                {
                    head.Inlines.Add(Renamed());
                    head.ToolTip = "Model: " + first.ModelApartment;
                }

                TextBlock about = CellText("redundant room, " + units + " unit(s)", Muted);
                about.FontSize = 12;

                panel.Children.Add(Shared(Cell(head), On(VENTCOLUMNS.Notes) ? Cell(about) : null));

                return panel;
            }

            VENTARRANGE.Row row = line.Row;

            bool inside = !string.IsNullOrEmpty(row.ApartmentKey);

            var tick = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            if (line.Room != null)
            {
                tick.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsOn") { Source = line.Room, Mode = BindingMode.TwoWay });
                tick.Click += (s, e) => RoomClicked(line);
            }
            else
            {
                tick.IsChecked = true;
                tick.IsEnabled = false;
            }

            panel.Children.Add(Cell(tick, TickWidth, true));

            // A container's units indented under it, as an outline
            TextBlock number = CellText(row.Number, Muted);
            number.Padding = new Thickness(inside ? 14 : 0, 0, 0, 0);

            Dimmed(number, line.Room, MutedUnlessOff, Muted);
            panel.Children.Add(Cell(number, NumberWidth));

            TextBlock name = CellText(null, Ink);

            Dimmed(name, line.Room, InkUnlessOff, Ink);

            name.Inlines.Add(new System.Windows.Documents.Run(row.Name));

            if (row.Name != row.ModelName)
            {
                name.Inlines.Add(Renamed());
                name.ToolTip = "Model: " + row.ModelName;
            }

            TextBlock notes = CellText(null, Muted);
            notes.FontSize = 12;

            if (row.IsTakenOut)
            {
                Note(notes, new System.Windows.Documents.Run(
                    "ungrouped from " + (row.Home + " " + row.HomeNumber).Trim())
                {
                    Foreground = Warn,
                    FontStyle = FontStyles.Italic
                });
            }

            // Each side's merge (fresh air / extract, 2026-10-22)
            foreach (bool extract in new[] { false, true })
            {
                List<string> merged = extract ? _work.ExtractMergeOf(row.Key) : _work.MergeOf(row.Key);

                if (merged == null)
                    continue;

                Note(notes, new System.Windows.Documents.Run(
                    (extract ? "extract merged with " : "fresh air merged with ") +
                    string.Join(", ", merged.Where(k => k != row.Key).Select(NumberOf)))
                {
                    Foreground = Accent,
                    ToolTip = MergeTip(merged, extract)
                });
            }

            // On one FCU with these, not merged yet (VENTFLOWS.FcuGroups).
            // On Model the FCU groups are merged already - a note there is
            // one unmerged by hand
            VENTFLOWS.FcuGroup fcu = _fcu.FirstOrDefault(g => g.Rooms.Contains(row.Key));

            if (fcu != null)
            {
                Note(notes, new System.Windows.Documents.Run(
                    "one FCU with " + string.Join(", ", fcu.Rooms.Where(k => k != row.Key).Select(NumberOf)))
                {
                    Foreground = Fcu,
                    FontStyle = FontStyles.Italic,
                    ToolTip = "FCU " + fcu.Unit + (fcu.Left.Count == 0
                        ? string.Empty
                        : " - also reaches " + string.Join(", ", fcu.Left.Select(NumberOf)) + ", left out")
                });
            }

            // A supply terminal whose ducts reach no unit in Revit - the
            // room cannot be merged with the FCU it was drawn for
            List<long> unreached;

            if (_unreached != null && _unreached.TryGetValue(row.Key, out unreached) && unreached.Count > 0)
            {
                Note(notes, new System.Windows.Documents.Run("terminal on no FCU")
                {
                    Foreground = Warn,
                    FontStyle = FontStyles.Italic,
                    ToolTip = "Terminal " + string.Join(", ", unreached) + " - its ducts reach no FCU in Revit"
                });
            }

            if (!string.IsNullOrEmpty(row.LinkName))
            {
                Note(notes, new System.Windows.Documents.Run(row.LinkName)
                {
                    Foreground = Faint,
                    FontSize = 11
                });
            }

            // Its blocks of cells, on the right - before the name and notes,
            // which share the rest
            Blocks(panel, row);

            panel.Children.Add(Shared(Cell(name), On(VENTCOLUMNS.Notes) ? Cell(notes) : null));

            return panel;
        }

        // WHERE A MERGE COMES FROM, on hover: the FCU whose supply ducts reach
        // the rooms, the sheet, or a hand
        private string MergeTip(List<string> merged, bool extract)
        {
            VENTFLOWS.Unit unit = _units == null || extract
                ? null
                : _units.FirstOrDefault(u => u != null && u.Rooms != null && merged.All(u.Rooms.Contains));

            if (_byModel && unit != null)
                return "FCU " + unit.Id + " - its supply ducts reach these rooms";

            string side = extract ? VENTFLOWS.ExtractSide : VENTFLOWS.FreshSide;

            bool sheet = _sheet != null && merged.All(k =>
            {
                VENTFLOWS.Sheet row;
                VENTFLOWS.Sheet first;

                return _sheet.TryGetValue(k, out row) && row != null && _sheet.TryGetValue(merged[0], out first) && first != null &&
                       row.Merges.Intersect(first.Merges).Any(t => t.StartsWith(side + " ", StringComparison.Ordinal));
            });

            return !_byModel && sheet ? "Merged in the sheet" : "Merged by hand - until Sheet or Model is pressed";
        }

        // A level: open / closed, its tick (all, none or some of its rooms),
        // its name and count
        private UIElement DrawLevel(Line line)
        {
            VENTWindow.LevelRow level = line.LevelRow;

            // A GROUP ROW: the arrow in the gutter, as Excel's outline
            // buttons are, the tick, and the level across every column
            var panel = new DockPanel();

            var arrow = new TextBlock
            {
                Text = level.Rooms.Count == 0 ? string.Empty : level.IsExpanded ? "▾" : "▸",
                TextAlignment = TextAlignment.Center,
                Foreground = Muted,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Tag = ArrowTag
            };

            // The first click of a double-click only: the second is the
            // list's double-click, which leaves the arrow alone
            arrow.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 1 && level.Rooms.Count > 0)
                    Toggle(level);

                e.Handled = true;
            };

            Border gutter = Gutter(arrow);
            gutter.MinHeight = RowHeight + 2;
            gutter.BorderBrush = LevelLine;

            panel.Children.Add(gutter);

            // ONE-WAY, and LevelClicked sets the row: WPF sends a click on the
            // third state to UNTICKED, which would untick every room of a
            // level somebody had only partly ticked (tests/toggletest)
            var box = new CheckBox
            {
                IsThreeState = false,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            box.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Checked") { Source = level, Mode = BindingMode.OneWay });
            box.Click += (s, e) => LevelClicked(line);

            Border tick = Cell(box, TickWidth, true);
            tick.BorderBrush = LevelLine;
            tick.BorderThickness = new Thickness(0, 0, 0, 1);

            panel.Children.Add(tick);

            var label = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = Ink,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            label.SetBinding(TextBlock.TextProperty, new Binding("Label") { Source = level, Mode = BindingMode.OneWay });

            Border across = Cell(label);
            across.BorderBrush = LevelLine;
            across.BorderThickness = new Thickness(0, 0, 0, 1);

            panel.Children.Add(across);

            return panel;
        }

        private void Toggle(VENTWindow.LevelRow level)
        {
            level.IsExpanded = !level.IsExpanded;

            Show(null);
        }

        // ==================================================
        // Ticks
        // ==================================================

        // All, some or none of these rooms ticked
        private static bool? State(IList<VENTWindow.RoomRow> rooms)
        {
            int on = rooms.Count(r => r.IsOn);

            return on == 0 ? false : on == rooms.Count ? true : (bool?)null;
        }

        // What a click on a line's box means: a level's as the levels list
        // always had it (ClickMeans - ticked goes to unticked, unticked or
        // part-ticked to ticked, its rooms showing); a container's the
        // same over its units showing; a room's, the other way
        private static bool ClickMeans(Line line)
        {
            if (line.IsLevel)
                return line.LevelRow.ClickMeans();

            if (line.IsApartment)
                return !line.Units.Where(r => r.IsShown).All(r => r.IsOn);

            return line.Room != null && !line.Room.IsOn;
        }

        // Ticking a PICKED row ticks every picked row the same way - a
        // level its rooms showing, a container its units showing
        private void TickPicked(bool on)
        {
            foreach (Line line in Picked())
                TickLine(line, on);
        }

        private static void TickLine(Line line, bool on)
        {
            if (line.IsLevel)
            {
                line.LevelRow.TickShown(on);
            }
            else if (line.IsApartment)
            {
                foreach (VENTWindow.RoomRow room in line.Units.Where(r => r.IsShown))
                    room.IsOn = on;
            }
            else if (line.Room != null)
            {
                line.Room.IsOn = on;
            }
        }

        private bool IsPicked(Line line)
        {
            return _list.SelectedItems.OfType<ListBoxItem>().Any(i => i.Tag == line);
        }

        private void RoomClicked(Line line)
        {
            // The two-way binding has set the room already
            if (IsPicked(line) && _list.SelectedItems.Count > 1)
                TickPicked(line.Room.IsOn);

            Ticks();
        }

        private void LevelClicked(Line line)
        {
            bool on = line.LevelRow.ClickMeans();

            if (IsPicked(line) && _list.SelectedItems.Count > 1)
                TickPicked(on);
            else
                line.LevelRow.TickShown(on);

            Ticks();
        }

        private void ContainerClicked(Line line)
        {
            bool on = ClickMeans(line);

            if (IsPicked(line) && _list.SelectedItems.Count > 1)
                TickPicked(on);
            else
                TickLine(line, on);

            Ticks();
        }

        // All and None: every level showing - its rooms showing
        private void TickShowing(bool on)
        {
            foreach (VENTWindow.LevelRow level in _levels.Where(l => l.IsShown))
                level.TickShown(on);

            Ticks();
        }

        // After any tick: the containers' boxes (the rooms' and levels' are
        // bound), the count, and the main window told
        private void Ticks()
        {
            foreach (Line line in _lines.Where(l => l.IsApartment && l.Box != null))
                line.Box.IsChecked = State(line.Units);

            Count();

            if (Ticked != null)
                Ticked();
        }

        // ==================================================
        // The airflows
        // ==================================================

        private static TextBlock FlowText(string text, Brush ink, bool bold, string tip)
        {
            return new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Right,
                Foreground = ink,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                ToolTip = tip,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // THE BLOCKS OF CELLS right of the name and notes, in the order of
        // the columns showing - docked right, so the last block first. A
        // container's are empty (row null).
        private void Blocks(DockPanel panel, VENTARRANGE.Row row)
        {
            foreach (string tag in _shown.Select(c => c.Tag).Distinct().Reverse().ToList())
            {
                var cells = new StackPanel { Orientation = Orientation.Horizontal, Tag = tag, Background = Brushes.Transparent };

                foreach (Column column in _shown.Where(c => c.Tag == tag))
                    cells.Children.Add(row == null ? Cell(null, column.Width) : CellOf(column, row));

                DockPanel.SetDock(cells, Dock.Right);
                panel.Children.Add(cells);
            }
        }

        private Border CellOf(Column column, VENTARRANGE.Row row)
        {
            switch (column.Tag)
            {
                case RoomTag:
                    return RoomCell(column, row);

                case OccupancyTag:
                    return OccupancyCell(column, row);

                case CalcTag:
                    return CalcCell(column, row);

                default:
                    return FlowCell(column, row);
            }
        }

        // THE AIRFLOWS - the sheet's, the new values, what the terminals
        // carry now - EACH SIDE BY ITS OWN GROUP (2026-10-22): a merged
        // group's on its first row; under it, ↑ - so a room can be under its
        // fresh air group and have its own extract. The new values amber
        // where they are not the sheet's; the Model's where they are not
        // what Write would write (the Sheet's, or on Model the new values).
        private Border FlowCell(Column column, VENTARRANGE.Row row)
        {
            if (column.Key == "sheet-area")
            {
                VENTFLOWS.Sheet own = SheetRow(row.Key);

                return Cell(FlowText(own == null || double.IsNaN(own.AreaM2) ? "-" : own.AreaM2.ToString("0.0", CultureInfo.CurrentCulture),
                                     Ink, false, null), column.Width);
            }

            bool extract = column.Key.EndsWith("extract", StringComparison.Ordinal);
            List<string> rooms = GroupRooms(row.Key, extract);

            if (rooms.Count > 1 && rooms[0] != row.Key)
                return Cell(FlowText("↑", Faint, false, "Merged - on " + NumberOf(rooms[0])), column.Width);

            Func<VENTFLOWS.Air, double> side = a => extract ? a.Extract : a.Fresh;

            double sheet = side(SheetOf(rooms));

            if (column.Tag == SheetTag)
                return Cell(FlowText(VENTFLOWS.Say(sheet), Ink, false, null), column.Width);

            if (column.Tag == NewTag)
            {
                double value = side(NewOf(rooms));
                string error = ErrorOf(rooms, extract);
                bool changed = !VENTFLOWS.Same(value, sheet);

                return Cell(FlowText(error ?? VENTFLOWS.Say(value), error != null || changed ? Warn : Muted, changed,
                                     error != null ? "The sheet's formula gives " + error
                                     : changed ? "Sheet: " + VENTFLOWS.Say(sheet) : null), column.Width);
            }

            double now = side(NowOf(rooms));

            Border cell = Cell(FlowText(VENTFLOWS.Say(now), VENTFLOWS.Same(now, side(SourceOf(rooms))) ? Muted : Warn, false, null), column.Width);

            cell.Tag = WriteTag + (extract ? VENTFLOWS.ExtractSide : VENTFLOWS.FreshSide);
            cell.Background = Brushes.Transparent;

            return cell;
        }

        // A ROOM'S OCCUPANCY, as the sheet has it or as chosen here - a
        // choice in blue, the sheet's own on hover; a blank category grey
        // with what an export would fill it with. Double-clicked, chosen.
        private Border OccupancyCell(Column column, VENTARRANGE.Row row)
        {
            VENTFLOWS.Sheet sheet = SheetRow(row.Key);
            string field = column.Key;

            bool text = field == VENTFLOWS.FreshCategoryField || field == VENTFLOWS.ExhaustCategoryField || field == VENTFLOWS.UseField;

            TextBlock Shown(string value, Brush ink)
            {
                TextBlock shown = text ? CellText(value, ink) : FlowText(value.Length == 0 ? "-" : value, ink, false, null);

                if (text)
                    shown.FontSize = 12;

                return shown;
            }

            if (sheet == null)
                return Cell(Shown("-", Faint), column.Width);

            VENTARRANGE.RoomOccupancy chosen;
            _work.Occupancy.TryGetValue(row.Key, out chosen);

            string own = VENTFLOWS.SheetText(sheet, field);
            string mine = VENTFLOWS.ChosenText(chosen, field);

            TextBlock cellText;
            Border cell;

            if (mine != null)
            {
                cellText = Shown(mine.Length == 0 && text ? "(blank)" : mine, Accent);
                cellText.FontWeight = FontWeights.SemiBold;
                cellText.ToolTip = "Sheet: " + (own.Length == 0 ? "blank" : own);

                cell = Cell(cellText, column.Width);
                cell.Background = ChosenFill;
            }
            else
            {
                string suggested = field == VENTFLOWS.FreshCategoryField ? sheet.FreshSuggested
                                 : field == VENTFLOWS.ExhaustCategoryField ? sheet.ExhaustSuggested
                                 : null;

                if (own.Length == 0 && suggested != null)
                {
                    cellText = Shown(suggested, Faint);
                    cellText.FontStyle = FontStyles.Italic;
                    cellText.ToolTip = "Blank in the sheet - an export fills in " + suggested;
                }
                else
                {
                    cellText = Shown(own, Ink);

                    if (text && own.Length > 0)
                        cellText.ToolTip = own;
                }

                cell = Cell(cellText, column.Width);
                cell.Background = Brushes.Transparent;
            }

            cell.Tag = EditTag + field;

            return cell;
        }

        // A column of the sheet's calculation, worked out with the occupancy
        // here - amber where it is not the sheet's
        private Border CalcCell(Column column, VENTARRANGE.Row row)
        {
            VENTFLOWS.Calc calc;
            string value = null;

            if (_calc.TryGetValue(row.Key, out calc) && calc != null)
                calc.Cells.TryGetValue(column.Col, out value);

            if (value == null)
                return Cell(FlowText("-", Faint, false, null), column.Width);

            string was = null;

            if (calc.SheetCells != null)
                calc.SheetCells.TryGetValue(column.Col, out was);

            bool changed = was != null && was != value;

            double number;
            bool figure = double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out number);

            Brush ink = value.StartsWith("#", StringComparison.Ordinal) || changed ? Warn : Muted;

            TextBlock shown = figure ? FlowText(value, ink, changed, null) : CellText(value, ink);

            shown.FontSize = 12;
            shown.ToolTip = changed ? "Sheet: " + (was.Length == 0 ? "blank" : was) : value.Length > 0 && !figure ? value : null;

            return Cell(shown, column.Width);
        }

        // ==================================================
        // Rooms with no area
        // ==================================================

        private List<VENTUNENCLOSED.Room> NotEnclosedRooms()
        {
            return _notEnclosedOf == null ? new List<VENTUNENCLOSED.Room>() : _notEnclosedOf() ?? new List<VENTUNENCLOSED.Room>();
        }

        private void SayNotEnclosed()
        {
            int count = NotEnclosedRooms().Count;

            _unenclosed.Content = VENTUNENCLOSED.Said(count);
            _unenclosed.IsEnabled = count > 0;
        }

        // The list: model, level, number, name, why - and Copy, for an email
        // or Excel
        private Window NotEnclosedList()
        {
            List<VENTUNENCLOSED.Room> rooms = NotEnclosedRooms();

            var list = new ListView { BorderBrush = Hairline, BorderThickness = new Thickness(1) };
            var columns = new GridView();

            GridViewColumn Column(string head, string path, double width) =>
                new GridViewColumn { Header = head, DisplayMemberBinding = new Binding(path), Width = width };

            columns.Columns.Add(Column("Model", "LinkName", 300));
            columns.Columns.Add(Column("Level", "Level", 120));
            columns.Columns.Add(Column("Number", "Number", 110));
            columns.Columns.Add(Column("Name", "Name", 150));
            columns.Columns.Add(Column("Why", "Why", 260));

            list.View = columns;
            list.ItemsSource = rooms;

            var said = new TextBlock
            {
                Text = rooms.Count + " room(s) with no area - fix in their model, then Refresh.",
                Foreground = Muted,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var copy = new Button { Content = "Copy", Padding = new Thickness(14, 4, 14, 4), Margin = new Thickness(0, 0, 8, 0) };
            var close = new Button { Content = "Close", Padding = new Thickness(14, 4, 14, 4), IsCancel = true };

            var dialog = new Window
            {
                Title = "AUTOVENT - rooms not enclosed",
                Owner = IsLoaded ? this : null,
                Width = 1000,
                Height = 460,
                MinWidth = 520,
                MinHeight = 260,
                WindowStartupLocation = IsLoaded ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                FontFamily = FontFamily,
                FontSize = FontSize,
                Background = Background
            };

            copy.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(VENTUNENCLOSED.Text(rooms));
                    said.Text = "Copied - paste it into an email or Excel.";
                }
                catch (Exception ex)
                {
                    said.Text = "Could not copy: " + ex.Message;
                }
            };

            close.Click += (s, e) => dialog.Close();

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(copy);
            buttons.Children.Add(close);

            var body = new DockPanel { Margin = new Thickness(16) };
            DockPanel.SetDock(said, Dock.Top);
            DockPanel.SetDock(buttons, Dock.Bottom);
            body.Children.Add(said);
            body.Children.Add(buttons);
            body.Children.Add(list);

            dialog.Content = body;

            // No bigger than the screen - a laptop at 150%
            VENTFIT.Screen(dialog);

            return dialog;
        }

        // A room's area (m2) or height (m), as the export writes them - the
        // area amber where the sheet's is not the model's
        private Border RoomCell(Column column, VENTARRANGE.Row row)
        {
            Border Figure(double value, string format, string tip, Brush ink = null) =>
                Cell(FlowText(value > 0 ? value.ToString(format, CultureInfo.CurrentCulture) : "-", ink ?? Muted, false, tip), column.Width);

            if (column.Key == "height")
            {
                return Figure(row.HeightM, "0.00", row.HeightM <= 0
                    ? null
                    : row.HeightToCeiling ? "To the false ceiling" : "No ceiling found - the room's own height");
            }

            VENTFLOWS.Sheet sheet = SheetRow(row.Key);

            return sheet != null && VENTFLOWS.AreaChanged(sheet.AreaM2, row.AreaM2)
                ? Figure(row.AreaM2, "0.0", "The sheet has " + sheet.AreaM2.ToString("0.0", CultureInfo.CurrentCulture) + " m² - changed since", Warn)
                : Figure(row.AreaM2, "0.0", null);
        }

        // The list's own scroll viewer
        private static ScrollViewer FindViewer(DependencyObject from)
        {
            for (int i = 0; from != null && i < VisualTreeHelper.GetChildrenCount(from); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(from, i);

                var viewer = child as ScrollViewer ?? FindViewer(child);

                if (viewer != null)
                    return viewer;
            }

            return null;
        }

        // A ROW'S COLOUR (user, 2026-10-19): red for a room number used
        // twice on its level by another room, else yellow where the sheet's
        // air and the model's differ - for every row of a merged group
        private Brush RowColour(VENTARRANGE.Row row, bool flows)
        {
            if (_repeated.Contains(row.Key))
                return Twice;

            if (!flows || _carrying == null)
                return null;

            List<string> freshRooms = GroupRooms(row.Key, false);
            List<string> extractRooms = GroupRooms(row.Key, true);

            return VENTFLOWS.Same(SourceOf(freshRooms).Fresh, NowOf(freshRooms).Fresh) &&
                   VENTFLOWS.Same(SourceOf(extractRooms).Extract, NowOf(extractRooms).Extract)
                ? null
                : Differs;
        }

        // Inside an element of this kind, or one tagged so, on the way up to
        // the row
        private static bool Inside<T>(DependencyObject from) where T : DependencyObject
        {
            for (DependencyObject at = from; at != null && !(at is ListBoxItem); at = Parent(at))
            {
                if (at is T)
                    return true;
            }

            return false;
        }

        // The cell a double-click edits (EditTag, WriteTag), on the way up to
        // the row - or null
        private static Border EditableAt(DependencyObject from)
        {
            for (DependencyObject at = from; at != null && !(at is ListBoxItem); at = Parent(at))
            {
                var tag = (at as Border) == null ? null : ((Border)at).Tag as string;

                if (tag != null && (tag.StartsWith(EditTag, StringComparison.Ordinal) || tag.StartsWith(WriteTag, StringComparison.Ordinal)))
                    return (Border)at;
            }

            return null;
        }

        private static bool OnTag(DependencyObject from, string tag)
        {
            for (DependencyObject at = from; at != null && !(at is ListBoxItem); at = Parent(at))
            {
                var element = at as FrameworkElement;

                if (element != null && tag.Equals(element.Tag))
                    return true;
            }

            return false;
        }

        private static DependencyObject Parent(DependencyObject at)
        {
            return at is Visual || at is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(at)
                : LogicalTreeHelper.GetParent(at);
        }

        // A room's group on its level, on one side, its first room first
        private List<string> GroupRooms(string key, bool extract)
        {
            Dictionary<string, string> groupOf = extract ? _extractOf : _freshOf;

            string group;
            string level;
            List<string> order;

            if (!groupOf.TryGetValue(key, out group) || !_levelOf.TryGetValue(key, out level) ||
                !_orderOf.TryGetValue(level, out order))
            {
                return new List<string> { key };
            }

            return order.Where(k => groupOf.TryGetValue(k, out string g) && g == group).ToList();
        }

        private VENTFLOWS.Air SheetOf(IList<string> rooms)
        {
            Func<string, VENTFLOWS.Air> of = k =>
            {
                VENTFLOWS.Sheet row;
                return _sheet != null && _sheet.TryGetValue(k, out row) && row != null ? row.Air : new VENTFLOWS.Air();
            };

            return new VENTFLOWS.Air
            {
                Fresh = VENTFLOWS.Total(rooms, k => of(k).Fresh),
                Extract = VENTFLOWS.Total(rooms, k => of(k).Extract)
            };
        }

        private VENTFLOWS.Air NowOf(IList<string> rooms)
        {
            Func<string, VENTFLOWS.Air> of = k =>
            {
                VENTFLOWS.Air air;
                return _carrying != null && _carrying.TryGetValue(k, out air) && air != null ? air : new VENTFLOWS.Air();
            };

            return new VENTFLOWS.Air
            {
                Fresh = VENTFLOWS.Total(rooms, k => of(k).Fresh),
                Extract = VENTFLOWS.Total(rooms, k => of(k).Extract)
            };
        }

        // ...with the occupancy here - a room the sheet's formulas have not
        // worked out (no workbook) as the sheet has it
        private VENTFLOWS.Air NewOf(IList<string> rooms)
        {
            Func<string, VENTFLOWS.Air> of = k =>
            {
                VENTFLOWS.Calc calc;

                if (_calc.TryGetValue(k, out calc) && calc != null)
                    return calc.Air;

                VENTFLOWS.Sheet row;
                return _sheet != null && _sheet.TryGetValue(k, out row) && row != null ? row.Air : new VENTFLOWS.Air();
            };

            return new VENTFLOWS.Air
            {
                Fresh = VENTFLOWS.Total(rooms, k => of(k).Fresh),
                Extract = VENTFLOWS.Total(rooms, k => of(k).Extract)
            };
        }

        // What Write to terminals writes for a group: the Sheet's, or on
        // Model the new values (user, 2026-10-26)
        private VENTFLOWS.Air SourceOf(IList<string> rooms)
        {
            return _byModel ? NewOf(rooms) : SheetOf(rooms);
        }

        // The error a group's formula gives, if any
        private string ErrorOf(IList<string> rooms, bool extract)
        {
            foreach (string key in rooms)
            {
                VENTFLOWS.Calc calc;

                if (_calc.TryGetValue(key, out calc) && calc != null)
                {
                    string error = extract ? calc.ExtractError : calc.FreshError;

                    if (error != null)
                        return error;
                }
            }

            return null;
        }

        // The room's new air is not the sheet's, either side
        private bool Changes(string key)
        {
            List<string> fresh = GroupRooms(key, false);
            List<string> extract = GroupRooms(key, true);

            return !VENTFLOWS.Same(NewOf(fresh).Fresh, SheetOf(fresh).Fresh) ||
                   !VENTFLOWS.Same(NewOf(extract).Extract, SheetOf(extract).Extract) ||
                   ErrorOf(fresh, false) != null || ErrorOf(extract, true) != null;
        }

        // THE ROOMS A LINE STANDS FOR: a level's and a container's showing,
        // or the room itself
        private static List<VENTWindow.RoomRow> RoomsOf(Line line)
        {
            if (line.IsLevel)
                return line.LevelRow.ShownRooms;

            if (line.IsApartment)
                return line.Units.Where(r => r.IsShown).ToList();

            return line.Room == null ? new List<VENTWindow.RoomRow>() : new List<VENTWindow.RoomRow> { line.Room };
        }

        // ...and the room keys - a room with no tick held is still a row
        private List<string> KeysOf(Line line)
        {
            if (line.Row != null)
                return new List<string> { line.Key };

            if (line.IsApartment)
            {
                return RowsOn(_all, line.Level)
                    .Where(r => r.ApartmentKey == line.Key && (TickOf(r.Key) == null || TickOf(r.Key).IsShown))
                    .Select(r => r.Key)
                    .ToList();
            }

            List<string> order;

            return line.IsLevel && _orderOf.TryGetValue(line.Level, out order)
                ? order.Where(k => TickOf(k) == null || TickOf(k).IsShown).ToList()
                : new List<string>();
        }

        // Every room the picks stand for, once each, top to bottom
        private List<VENTWindow.RoomRow> PickedRooms()
        {
            var rooms = new List<VENTWindow.RoomRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Line line in Picked())
            {
                foreach (VENTWindow.RoomRow room in RoomsOf(line))
                {
                    if (seen.Add(room.Key))
                        rooms.Add(room);
                }
            }

            return rooms;
        }

        // The groups picked on one side - a level or a container stands for
        // its rooms
        private List<List<string>> PickedGroups(bool extract)
        {
            var groups = new List<List<string>>();

            foreach (string key in Picked().SelectMany(KeysOf))
            {
                List<string> rooms = GroupRooms(key, extract);

                if (!groups.Any(g => g[0] == rooms[0]))
                    groups.Add(rooms);
            }

            return groups;
        }

        // WRITE TO TERMINALS: the picked rooms' airflows, through the main
        // window (REVITROOMS, Mode.Write), the arrangement as it stands. The
        // window stays open; the Model columns follow once the terminals
        // are read again.
        //
        // THE SWITCH DECIDES WHAT IS WRITTEN (user, 2026-10-19): on Sheet the
        // Sheet columns - the sheet's; on Model the NEW VALUES - the sheet's
        // formulas with the occupancy chosen here (user, 2026-10-26: "write
        // the flow to terminal from sheet when 'sheet' button is selected or
        // from the new occupancy selected from the addin when 'model' is
        // selected"), each group's as the FCUs group the rooms. For the
        // rooms picked, a level or container standing for its rooms.
        private void WriteNow()
        {
            List<List<string>> freshGroups = PickedGroups(false);
            List<List<string>> extractGroups = PickedGroups(true);

            if (freshGroups.Count == 0)
            {
                _said.Text = "Pick the rooms or levels to write.";
                return;
            }

            // EACH SIDE BY ITS OWN GROUPS (2026-10-22): a fresh air group's
            // value on its first room, an extract group's on its first
            Func<IList<string>, VENTFLOWS.Air> of = SourceOf;

            var freshValues = freshGroups
                .Select(rooms => new KeyValuePair<List<string>, VENTFLOWS.Air>(rooms, new VENTFLOWS.Air { Fresh = of(rooms).Fresh }))
                .Where(v => !double.IsNaN(v.Value.Fresh))
                .ToList();

            var extractValues = extractGroups
                .Select(rooms => new KeyValuePair<List<string>, VENTFLOWS.Air>(rooms, new VENTFLOWS.Air { Extract = of(rooms).Extract }))
                .Where(v => !double.IsNaN(v.Value.Extract))
                .ToList();

            Dictionary<string, VENTFLOWS.Air> writes = VENTFLOWS.Overrides(freshValues);

            foreach (KeyValuePair<string, VENTFLOWS.Air> room in VENTFLOWS.Overrides(extractValues))
            {
                VENTFLOWS.Air air;

                if (!writes.TryGetValue(room.Key, out air))
                    writes[room.Key] = air = new VENTFLOWS.Air();

                air.Extract = room.Value.Extract;
            }

            var fresh = new HashSet<string>(freshValues.SelectMany(v => v.Key), StringComparer.Ordinal);
            var extract = new HashSet<string>(extractValues.SelectMany(v => v.Key), StringComparer.Ordinal);

            if (fresh.Count == 0 && extract.Count == 0)
            {
                _said.Text = _byModel
                    ? "No new airflow for those rooms."
                    : "No airflow in the sheet for those rooms.";
                return;
            }

            Writes = writes;
            WriteFresh = fresh;
            WriteExtract = extract;
            WroteModel = _byModel;

            _said.Text = "Writing the airflows of " + new HashSet<string>(fresh.Concat(extract)).Count + " room(s)...";

            if (Write != null)
                Write(writes, fresh, extract, _byModel);
        }

        // ONE GROUP'S AIRFLOW WRITTEN NOW, on one side - a Model cell's (the
        // user's choice: "write it to the terminals now"): the value on its
        // first room, as Write to terminals gives it (VENTFLOWS.Overrides)
        private void WriteGroup(List<string> group, bool extract, double value)
        {
            var air = extract ? new VENTFLOWS.Air { Extract = value } : new VENTFLOWS.Air { Fresh = value };

            Dictionary<string, VENTFLOWS.Air> writes = VENTFLOWS.Overrides(new[] { new KeyValuePair<List<string>, VENTFLOWS.Air>(group, air) });

            var fresh = new HashSet<string>(extract ? Enumerable.Empty<string>() : group, StringComparer.Ordinal);
            var exhaust = new HashSet<string>(extract ? group : Enumerable.Empty<string>(), StringComparer.Ordinal);

            Writes = writes;
            WriteFresh = fresh;
            WriteExtract = exhaust;
            WroteModel = _byModel;

            _said.Text = "Writing " + VENTFLOWS.Say(value) + " L/s " + (extract ? "extract" : "fresh air") + " to " +
                         NumberOf(group[0]) + (group.Count > 1 ? " and its group" : string.Empty) + "...";

            if (Write != null)
                Write(writes, fresh, exhaust, _byModel);
        }

        // ==================================================
        // The occupancy, chosen in its cells
        // ==================================================

        // Every room the picks stand for, once each
        private List<string> PickedKeys()
        {
            return Picked().SelectMany(KeysOf).Distinct(StringComparer.Ordinal).ToList();
        }

        // The rooms an edit of this row is for: every room picked, when it is
        // one of several picks; else itself
        private List<string> EditedKeys(Line line)
        {
            return IsPicked(line) && _list.SelectedItems.Count > 1 ? PickedKeys() : new List<string> { line.Key };
        }

        // An editor in the cell, focused - Esc (the window's PreviewKeyDown)
        // leaves the cell as it was
        private void Open(Border cell, Control editor, Action cancel)
        {
            cell.Padding = new Thickness(0);
            cell.Child = editor;

            _editor = editor;
            _cancelEdit = () =>
            {
                cancel();
                EditorGone();
                Show(null);
            };

            editor.Loaded += (s, e) =>
            {
                editor.Focus();

                if (editor is TextBox box)
                    box.SelectAll();

                if (editor is ComboBox list && !list.IsEditable)
                    list.IsDropDownOpen = true;
            };
        }

        private void EditorGone()
        {
            _editor = null;
            _cancelEdit = null;
        }

        // A DOUBLE-CLICK ON AN OCCUPANCY CELL: the sheet's own list there -
        // "As the sheet", blank, its categories - or a box for a number. Kept
        // for every picked room when this one is picked with others.
        private void BeginEdit(Line line, string field, Border cell)
        {
            VENTFLOWS.Sheet sheet = SheetRow(line.Key);

            if (sheet == null)
            {
                _said.Text = "No row in the sheet for " + NumberOf(line.Key) + ".";
                return;
            }

            List<string> keys = EditedKeys(line);
            string own = VENTFLOWS.SheetText(sheet, field);
            string now = VENTFLOWS.Effective(_work, line.Key, field, sheet);

            bool done = false;

            // Kept once the focus has gone - not while WPF is moving it. A
            // value null is "as the sheet": each room's own.
            Action<string, bool> keep = (value, unchanged) =>
            {
                if (done)
                    return;

                done = true;
                EditorGone();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (unchanged)
                        Show(null);
                    else
                        Chosen(keys, field, value);
                }));
            };

            if (field == VENTFLOWS.OccupantsField || field == VENTFLOWS.UnitsField)
            {
                var box = new TextBox
                {
                    Text = now,
                    TextAlignment = TextAlignment.Right,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(4, 0, 6, 0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    ToolTip = "Enter keeps it, Esc leaves it; empty is blank - the sheet has " + (own.Length == 0 ? "none" : own)
                };

                box.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        keep(box.Text.Trim(), keys.Count == 1 && box.Text.Trim() == now);
                    }
                };

                box.LostKeyboardFocus += (s, e) => keep(box.Text.Trim(), keys.Count == 1 && box.Text.Trim() == now);

                Open(cell, box, () => done = true);

                return;
            }

            List<string> choices = _formulas == null ? null
                : field == VENTFLOWS.FreshCategoryField ? _formulas.FreshChoices
                : field == VENTFLOWS.ExhaustCategoryField ? _formulas.ExhaustChoices
                : _formulas.UseChoices;

            var combo = new ComboBox
            {
                IsEditable = choices == null,
                FontSize = 12,
                BorderThickness = new Thickness(0),
                MaxDropDownHeight = 360,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            ComboBoxItem Item(string text, object tag, bool italic)
            {
                var item = new ComboBoxItem
                {
                    Content = new TextBlock { Text = text, FontStyle = italic ? FontStyles.Italic : FontStyles.Normal },
                    Tag = tag
                };

                TextSearch.SetText(item, tag is string ? (string)tag : string.Empty);

                return item;
            }

            combo.Items.Add(Item("As the sheet" + (own.Length == 0 ? " (blank)" : ": " + own), AsTheSheet, true));
            combo.Items.Add(Item("(blank)", string.Empty, true));

            VENTARRANGE.RoomOccupancy chosen;
            _work.Occupancy.TryGetValue(line.Key, out chosen);

            string mine = VENTFLOWS.ChosenText(chosen, field);

            ComboBoxItem current = mine == null ? (ComboBoxItem)combo.Items[0] : mine.Length == 0 ? (ComboBoxItem)combo.Items[1] : null;

            foreach (string choice in choices ?? new List<string>())
            {
                ComboBoxItem item = Item(choice, choice, false);
                combo.Items.Add(item);

                if (current == null && string.Equals(choice, mine, StringComparison.OrdinalIgnoreCase))
                    current = item;
            }

            combo.SelectedItem = current;

            if (current == null && combo.IsEditable)
                combo.Text = mine ?? string.Empty;

            // What is picked: null for "As the sheet"; nothing picked, as it
            // was
            Func<string> picked = () =>
            {
                var item = combo.SelectedItem as ComboBoxItem;

                if (item != null)
                    return item.Tag == AsTheSheet ? null : (string)item.Tag;

                return combo.IsEditable ? combo.Text.Trim() : mine;
            };

            Action finish = () =>
            {
                string value = picked();
                keep(value, keys.Count == 1 && value == mine);
            };

            if (combo.IsEditable)
            {
                combo.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        finish();
                    }
                };

                combo.LostKeyboardFocus += (s, e) =>
                {
                    if (!combo.IsKeyboardFocusWithin && !combo.IsDropDownOpen)
                        finish();
                };
            }
            else
            {
                combo.DropDownClosed += (s, e) => finish();
            }

            Open(cell, combo, () =>
            {
                done = true;
                combo.IsDropDownOpen = false;
            });
        }

        // THE CHOICE KEPT on each room - saved with the arrangement, and the
        // new air worked out again (Changed)
        private void Chosen(IEnumerable<string> keys, string field, string text)
        {
            int kept = 0;

            foreach (string key in keys)
            {
                VENTFLOWS.Sheet sheet = SheetRow(key);

                if (sheet != null && VENTFLOWS.Choose(_work, key, field, text, sheet))
                    kept++;
            }

            if (kept == 0)
            {
                Show(null);
                _said.Text = "Not a number: " + text;
                return;
            }

            Changed(null);
        }

        // A DOUBLE-CLICK ON A MODEL AIRFLOW: typed, and written to its
        // group's terminals at once (the user's choice) - Enter writes it
        private void BeginWrite(Line line, bool extract, Border cell)
        {
            List<string> group = GroupRooms(line.Key, extract);
            double now = extract ? NowOf(group).Extract : NowOf(group).Fresh;

            var box = new TextBox
            {
                Text = double.IsNaN(now) ? string.Empty : VENTFLOWS.Say(now),
                TextAlignment = TextAlignment.Right,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2, 0, 4, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "L/s " + (extract ? "extract" : "fresh air") + " for the terminals" +
                          (group.Count > 1 ? " of its " + group.Count + " merged rooms" : string.Empty) + " - Enter writes it, Esc leaves it"
            };

            bool done = false;

            box.KeyDown += (s, e) =>
            {
                if (e.Key != Key.Enter)
                    return;

                e.Handled = true;

                double? value = VENTFLOWS.Read(box.Text);

                if (value == null || double.IsNaN(value.Value))
                {
                    _said.Text = "Type the airflow in L/s.";
                    return;
                }

                done = true;
                EditorGone();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Show(null);
                    WriteGroup(group, extract, value.Value);
                }));
            };

            // Clicked away: nothing written
            box.LostKeyboardFocus += (s, e) =>
            {
                if (done)
                    return;

                done = true;
                EditorGone();
                Dispatcher.BeginInvoke(new Action(() => Show(null)));
            };

            Open(cell, box, () => done = true);
        }

        // OCCUPANCY FROM RULES, BLANK ONES ONLY (the user's choice): the
        // picked rooms' - none picked, every room showing - from the rules
        // an export fills the sheet's blanks with (VENTFLOWS.FromRules)
        private void FromRulesPicked()
        {
            List<string> keys = PickedKeys();

            if (keys.Count == 0)
            {
                keys = _all.Values.SelectMany(r => r).Select(r => r.Key)
                           .Where(k => TickOf(k) == null || TickOf(k).IsShown)
                           .ToList();
            }

            int took = VENTFLOWS.FromRules(_work, keys, _sheet, _formulas);

            if (took == 0)
            {
                _said.Text = "No blank category the rules can fill.";
                return;
            }

            Changed(null);

            Say(took + " room(s) given their occupancy from the rules.", Accent);
        }

        // The picked rooms' occupancy back to the sheet's
        private void AsSheetPicked()
        {
            List<string> keys = PickedKeys().Where(_work.Occupancy.ContainsKey).ToList();

            if (keys.Count == 0)
                return;

            foreach (string key in keys)
                _work.Occupancy.Remove(key);

            Changed(null);

            Say(keys.Count + " room(s) back to the sheet's occupancy.", Accent);
        }

        // EVERY ROW AS WIDE AS THE LIST, never narrower than its columns
        // need - past that the list scrolls sideways, the headings with it
        private void ApplyWidths()
        {
            ScrollViewer viewer = FindViewer(_list);

            double width = Math.Max(_minRowWidth, viewer == null ? 0 : viewer.ViewportWidth);

            _heads.Width = width;

            foreach (ListBoxItem item in _list.Items.OfType<ListBoxItem>())
            {
                var content = item.Content as FrameworkElement;

                if (content != null && (double.IsNaN(content.Width) || Math.Abs(content.Width - width) > 0.1))
                    content.Width = width;
            }
        }

        // A room by its number, else its name - and its container, when the
        // number alone is ambiguous across levels
        private string NumberOf(string key)
        {
            VENTARRANGE.Row row;

            if (key == null || !_rowOf.TryGetValue(key, out row))
                return "?";

            return string.IsNullOrWhiteSpace(row.Number) ? row.Name : row.Number;
        }

        private static System.Windows.Documents.Run Renamed()
        {
            return new System.Windows.Documents.Run("  renamed") { Foreground = Accent, FontSize = 11 };
        }

        // The picked lines, top to bottom
        private List<Line> Picked()
        {
            return _list.SelectedItems
                .OfType<ListBoxItem>()
                .Select(i => new { Item = i, At = _list.Items.IndexOf(i) })
                .OrderBy(x => x.At)
                .Select(x => x.Item.Tag as Line)
                .Where(l => l != null)
                .ToList();
        }

        private void Enable()
        {
            List<Line> picked = Picked();
            List<Line> rows = picked.Where(l => !l.IsLevel).ToList();

            bool one = picked.Count == 1 && rows.Count == 1;

            _allOn.IsEnabled = _noneOn.IsEnabled = _levels.Count > 0;
            _show.IsEnabled = PickedRooms().Count > 0;
            _up.IsEnabled = _down.IsEnabled = _rename.IsEnabled = one;
            _replace.IsEnabled = _all.Values.Any(r => r.Count > 0);
            _takeOut.IsEnabled = rows.Any(l => l.Row != null && !string.IsNullOrEmpty(l.Row.ApartmentKey));
            _putBack.IsEnabled = rows.Any(l => l.Row != null && l.Row.IsTakenOut);
            _merge.IsEnabled = rows.Count(l => l.Row != null) > 1;
            _unmerge.IsEnabled = rows.Any(l => l.Row != null && (_work.MergeOf(l.Row.Key) != null || _work.ExtractMergeOf(l.Row.Key) != null));
            _reset.IsEnabled = picked.Any(l => RowsOn(_all, l.Level).Count > 0);

            bool any = picked.Any(l => KeysOf(l).Count > 0);

            // The Sheet's air from the sheet, the new values from it too
            _write.IsEnabled = any && _sheet != null;

            _fromRules.IsEnabled = _sheet != null;
            _asSheet.IsEnabled = PickedKeys().Any(_work.Occupancy.ContainsKey);
        }

        // THE LINE UNDER THE LIST: what is ticked, and what was done to the
        // rooms - or, searching, what the search found
        private void Count()
        {
            List<VENTARRANGE.Row> rows = _all.Values.SelectMany(r => r).ToList();

            if (rows.Count == 0)
            {
                _said.Text = _levels.Count == 0
                    ? "No levels read yet."
                    : "No rooms - tick a linked model first.";
                return;
            }

            int ticked = rows.Count(r => TickOf(r.Key) == null || TickOf(r.Key).IsOn);
            int levelsOn = _levels.Count(l => l.Checked != false);

            int apartments = rows.Where(r => !string.IsNullOrEmpty(r.ApartmentKey)).Select(r => r.ApartmentKey).Distinct().Count();
            int takenOut = rows.Count(r => r.IsTakenOut);
            int merged = rows.Select(r => _work.MergeIdOf(r.Key)).Where(m => m != null).Distinct().Count() +
                         rows.Select(r => _work.ExtractMergeIdOf(r.Key)).Where(m => m != null).Distinct().Count();

            int renamed = rows.Count(r => r.Name != r.ModelName) +
                          rows.Where(r => !string.IsNullOrEmpty(r.ApartmentKey) && r.Apartment != r.ModelApartment)
                              .Select(r => r.ApartmentKey).Distinct().Count();

            int unmerged = _byModel ? 0 : _fcu.Count;
            int chosen = rows.Count(r => _work.Occupancy.ContainsKey(r.Key));

            string summary = levelsOn + " of " + _levels.Count + " level(s), " + ticked + " of " + rows.Count + " room(s) ticked" +
                             (apartments > 0 ? ", " + apartments + " redundant room(s)" : string.Empty) +
                             (takenOut > 0 ? ", " + takenOut + " ungrouped" : string.Empty) +
                             (merged > 0 ? ", " + merged + " merged group(s)" : string.Empty) +
                             (renamed > 0 ? ", " + renamed + " renamed" : string.Empty) +
                             (chosen > 0 ? ", " + chosen + " with occupancy chosen" : string.Empty) +
                             (unmerged > 0 ? ", " + unmerged + " grouped differently on Model" : string.Empty) + "." +
                             (_contested > 0 ? " " + _contested + " FCU suggestion(s) overlap - merge by hand." : string.Empty) +
                             (_refused > 0 ? " " + _refused + " sheet merge(s) across redundant rooms left apart." : string.Empty) +
                             (_readingTerminals ? " Reading the terminals..." :
                              Flows && _carrying == null ? " Terminals not read." : string.Empty) +
                             (_calcTrouble != null ? " New values: " + _calcTrouble : string.Empty);

            if (!Searching)
            {
                _said.Text = summary;
                return;
            }

            int found = _levels.Sum(l => l.Rooms.Count(r => r.IsShown));
            int on = _levels.Count(l => l.IsShown && l.Rooms.Any(r => r.IsShown));

            string what = _differences ? (string.IsNullOrWhiteSpace(Query) ? "need attention" : "match and need attention") : "match";

            _said.Text = (found == 0
                             ? (_differences ? "Nothing needs attention" + (string.IsNullOrWhiteSpace(Query) ? ". " : " that matches \"" + Query.Trim() + "\". ")
                                             : "No room matches \"" + Query.Trim() + "\". ")
                             : found + " room(s) on " + on + " level(s) " + what + ". ") + summary;
        }

        // A line said first, before the count, until the next change
        private void Say(string what, Brush ink)
        {
            var said = new System.Windows.Documents.Run(what + " ") { Foreground = ink };

            if (_said.Inlines.FirstInline == null)
                _said.Inlines.Add(said);
            else
                _said.Inlines.InsertBefore(_said.Inlines.FirstInline, said);
        }

        // SHOW IN PLAN: ONE PLAN SHOWS ONE LEVEL. Picks spread over several
        // levels show the level most of them are on (the first in the list,
        // on a tie), and the answer says how many were left out
        private void ShowPicked()
        {
            List<VENTWindow.RoomRow> rooms = PickedRooms();

            if (rooms.Count == 0 || ShowInPlan == null)
                return;

            // GroupBy keeps the list's order and OrderByDescending is stable,
            // so a tie goes to the level higher in the list
            List<VENTWindow.RoomRow> most = rooms
                .GroupBy(r => r.Level ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .First()
                .ToList();

            _said.Text = "Showing " + most.Count + " room(s)...";

            ShowInPlan(most, rooms.Count - most.Count);
        }

        // ==================================================
        // Moving, renaming, taking out, putting back, merging
        // ==================================================

        // A container's order with one key moved, kept only where it is not
        // simply the default (VENTARRANGE.SetOrder)
        private void Reorder(string level, string container, List<string> sequence)
        {
            VENTARRANGE.Arrangement plain = _work.Clone();
            plain.Orders.Remove(container);

            List<string> defaults = VENTARRANGE.SequenceOf(RowsOn(_rowsOf(plain), level), container);

            VENTARRANGE.SetOrder(_work, container, sequence, defaults);
        }

        // The keys showing on a level - null with no search: everything
        private HashSet<string> ShownKeys(string level)
        {
            if (!Searching)
                return null;

            return new HashSet<string>(
                _lines.Where(l => !l.IsLevel && string.Equals(l.Level, level, StringComparison.OrdinalIgnoreCase)).Select(l => l.Key),
                StringComparer.Ordinal);
        }

        // One step past the next row SHOWING - with a search on, the rows it
        // hides are stepped over rather than swapped with unseen
        private void MovePicked(int by)
        {
            List<Line> picked = Picked();

            if (picked.Count != 1 || picked[0].IsLevel)
                return;

            Line line = picked[0];

            List<string> sequence = VENTARRANGE.SequenceOf(RowsOn(_all, line.Level), line.Container);
            List<string> moved = VENTARRANGE.Stepped(sequence, line.Key, by, ShownKeys(line.Level));

            if (moved.SequenceEqual(sequence, StringComparer.Ordinal))
                return;

            Reorder(line.Level, line.Container, moved);

            Changed(new[] { IdOf(line) });
        }

        // A new name for the one row picked - a room, or a container's
        // heading. Emptied, it goes back to the model's name.
        private void RenamePicked()
        {
            List<Line> picked = Picked();

            if (picked.Count != 1 || picked[0].IsLevel)
                return;

            Line line = picked[0];

            string current, model;

            if (line.IsApartment)
            {
                VENTARRANGE.Row first = RowsOn(_all, line.Level).FirstOrDefault(r => r.ApartmentKey == line.Key);

                if (first == null)
                    return;

                current = first.Apartment;
                model = first.ModelApartment;
            }
            else
            {
                current = line.Row.Name;
                model = line.Row.ModelName;
            }

            string name = AskName(current, model);

            if (name == null)
                return;

            VENTARRANGE.Rename(_work, line.Key, name, model);

            Changed(new[] { IdOf(line) });
        }

        // Find and replace across names - every level, or the picked rows'
        private void ReplaceNames()
        {
            List<string> pickedLevels = Picked().Select(l => l.Level).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var ask = new Window
            {
                Title = "Find and replace",
                Owner = this,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                FontFamily = FontFamily,
                FontSize = FontSize,
                Background = Background
            };

            var grid = new Grid { Margin = new Thickness(16) };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 4; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var find = new TextBox { Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 0, 8) };
            var with = new TextBox { Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 0, 8) };
            var picked = new CheckBox
            {
                Content = new TextBlock { Text = "Only the picked rows' levels" },
                IsChecked = false,
                IsEnabled = pickedLevels.Count > 0
            };

            Action<UIElement, int, int> at = (element, row, column) =>
            {
                Grid.SetRow(element, row);
                Grid.SetColumn(element, column);
                grid.Children.Add(element);
            };

            at(new TextBlock { Text = "Find", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) }, 0, 0);
            at(find, 0, 1);
            at(new TextBlock { Text = "Replace with", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) }, 1, 0);
            at(with, 1, 1);
            at(picked, 2, 1);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var ok = new Button { Content = "Replace", Width = 80, Padding = new Thickness(0, 3, 0, 3), IsDefault = true };
            var cancel = new Button { Content = "Cancel", Width = 80, Padding = new Thickness(0, 3, 0, 3), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };

            ok.Click += (s, e) => ask.DialogResult = true;

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            Grid.SetColumnSpan(buttons, 2);
            at(buttons, 3, 0);

            ask.Content = grid;
            ask.Loaded += (s, e) => find.Focus();

            if (ask.ShowDialog() != true || string.IsNullOrEmpty(find.Text))
                return;

            IEnumerable<VENTARRANGE.Row> rows = picked.IsChecked == true
                ? pickedLevels.SelectMany(l => RowsOn(_all, l))
                : _all.Values.SelectMany(r => r);

            int changed = VENTARRANGE.Replace(_work, rows.ToList(), find.Text, with.Text);

            Changed(null);

            Say(changed + " name(s) changed.", Accent);
        }

        // The name asked for, or null for Cancel
        private string AskName(string current, string model)
        {
            var ask = new Window
            {
                Title = "Rename",
                Owner = this,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                FontFamily = FontFamily,
                FontSize = FontSize,
                Background = Background
            };

            var panel = new StackPanel { Margin = new Thickness(16) };

            var box = new TextBox { Text = current ?? string.Empty, Padding = new Thickness(6, 3, 6, 3) };

            panel.Children.Add(box);

            panel.Children.Add(new TextBlock
            {
                Text = "Model: " + model + "  (empty to keep it)",
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var ok = new Button { Content = "OK", Width = 80, Padding = new Thickness(0, 3, 0, 3), IsDefault = true };
            var cancel = new Button { Content = "Cancel", Width = 80, Padding = new Thickness(0, 3, 0, 3), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };

            ok.Click += (s, e) => ask.DialogResult = true;

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            ask.Content = panel;
            ask.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };

            return ask.ShowDialog() == true ? box.Text : null;
        }

        private void TakeOutPicked()
        {
            List<Line> picked = Picked().Where(l => l.Row != null && !string.IsNullOrEmpty(l.Row.ApartmentKey)).ToList();

            foreach (Line line in picked)
                VENTARRANGE.TakeOut(_work, line.Row, line.Level);

            Changed(picked.Select(IdOf));
        }

        private void PutBackPicked()
        {
            List<Line> picked = Picked().Where(l => l.Row != null && l.Row.IsTakenOut).ToList();

            foreach (Line line in picked)
                VENTARRANGE.PutBack(_work, line.Row, line.Level);

            Changed(picked.Select(IdOf));
        }

        private void MergePicked()
        {
            List<Line> picked = Picked().Where(l => l.Row != null).ToList();

            string why = VENTARRANGE.Merge(_work, picked.Select(l => l.Row).ToList());

            Changed(picked.Select(IdOf));

            if (why != null)
                Say(why, Warn);
        }

        private void UnmergePicked()
        {
            List<Line> picked = Picked().Where(l => l.Row != null).ToList();

            VENTARRANGE.Unmerge(_work, picked.Select(l => l.Key).ToList());

            Changed(picked.Select(IdOf));
        }

        // EVERYTHING CHOSEN FOR THE PICKED ROWS' LEVELS forgotten - asked
        // first, there being no Cancel to come back from it
        private void ResetPicked()
        {
            List<string> levels = Picked()
                .Select(l => l.Level)
                .Where(l => RowsOn(_all, l).Count > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (levels.Count == 0)
                return;

            if (MessageBox.Show(this, "Forget the order, names, ungroups and merges on " + string.Join(", ", levels) + "?",
                    "AUTOVENT", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (string level in levels)
                VENTARRANGE.Reset(_work, RowsOn(_all, level), level);

            Changed(null);
        }

        // ==================================================
        // Dragging - a room or container, within its level and container
        // ==================================================

        private Point _from;
        private Line _pressed;
        private readonly List<ListBoxItem> _marked = new List<ListBoxItem>();

        private void Dragging()
        {
            _list.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _from = e.GetPosition(_list);

                var from = e.OriginalSource as DependencyObject;
                ListBoxItem item = ItemAt(from);
                var line = item == null ? null : item.Tag as Line;

                // A box ticks, an arrow opens, an editor edits; a level stays
                // where it is
                _pressed = line == null || line.IsLevel || _editor != null || Inside<ToggleButton>(from) ||
                           Inside<TextBox>(from) || Inside<ComboBox>(from) ? null : line;
            };

            _list.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || _pressed == null)
                    return;

                Vector moved = e.GetPosition(_list) - _from;

                if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
                    return;

                Line dragged = _pressed;
                _pressed = null;

                DragDrop.DoDragDrop(_list, new DataObject(typeof(Line), dragged), DragDropEffects.Move);

                Unmark();
            };

            _list.DragOver += (s, e) =>
            {
                Line dragged = e.Data.GetData(typeof(Line)) as Line;

                bool after;
                Line target = TargetOf(dragged, e, out after);

                e.Effects = target == null ? DragDropEffects.None : DragDropEffects.Move;
                Mark(dragged, target, after);
                e.Handled = true;
            };

            _list.DragLeave += (s, e) => Unmark();

            _list.Drop += (s, e) =>
            {
                Unmark();

                Line dragged = e.Data.GetData(typeof(Line)) as Line;

                bool after;
                Line target = TargetOf(dragged, e, out after);

                if (dragged == null || target == null)
                    return;

                List<string> sequence = VENTARRANGE.SequenceOf(RowsOn(_all, dragged.Level), dragged.Container);

                bool top = dragged.Container == VENTARRANGE.LevelContainer(dragged.Level);
                string onto = top ? target.UnitKey : target.Key;

                if (onto == dragged.Key)
                    return;

                sequence.Remove(dragged.Key);

                int at = sequence.IndexOf(onto);

                if (at < 0)
                    return;

                sequence.Insert(after ? at + 1 : at, dragged.Key);

                Reorder(dragged.Level, dragged.Container, sequence);

                Changed(new[] { IdOf(dragged) });

                e.Handled = true;
            };
        }

        private static ListBoxItem ItemAt(DependencyObject from)
        {
            while (from != null && !(from is ListBoxItem))
                from = Parent(from);

            return from as ListBoxItem;
        }

        // WHERE A DROP WOULD GO, and whether it is allowed there: a top row
        // (a room of its own, or a container) lands before or after another
        // top row of its level; a unit only before or after another unit of
        // the same container. Anything else - another level - is refused.
        private Line TargetOf(Line dragged, DragEventArgs e, out bool after)
        {
            after = false;

            if (dragged == null)
                return null;

            ListBoxItem item = ItemAt(_list.InputHitTest(e.GetPosition(_list)) as DependencyObject);
            Line target = item == null ? null : item.Tag as Line;

            if (target == null || target.IsLevel ||
                !string.Equals(target.Level, dragged.Level, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            after = e.GetPosition(item).Y > item.ActualHeight / 2;

            if (dragged.Container == VENTARRANGE.LevelContainer(dragged.Level))
            {
                // Onto any line of a unit is onto the unit; past its heading
                // is after the whole container
                if (target.UnitKey == dragged.UnitKey)
                    return null;

                if (target.UnitKey != target.Key)
                    after = true;

                return target;
            }

            return target.Container == dragged.Container && target.Key != dragged.Key ? target : null;
        }

        // A line where the drop would land: above the first line of what it
        // lands before, or under the last line of what it lands after
        private void Mark(Line dragged, Line target, bool after)
        {
            Unmark();

            if (dragged == null || target == null)
                return;

            List<ListBoxItem> items = _list.Items.OfType<ListBoxItem>().ToList();

            ListBoxItem edge;

            if (dragged.Container == VENTARRANGE.LevelContainer(dragged.Level))
            {
                List<ListBoxItem> unit = items
                    .Where(i => !((Line)i.Tag).IsLevel && ((Line)i.Tag).Level == target.Level && ((Line)i.Tag).UnitKey == target.UnitKey)
                    .ToList();

                edge = after ? unit.LastOrDefault() : unit.FirstOrDefault();
            }
            else
            {
                edge = items.FirstOrDefault(i => i.Tag == target);
            }

            if (edge == null)
                return;

            // One edge only, laid over its neighbour (a margin as deep as the
            // mark), so nothing below jumps while dragging
            edge.BorderBrush = Accent;
            edge.BorderThickness = after ? new Thickness(0, 0, 0, 3) : new Thickness(0, 3, 0, 0);
            edge.Margin = after ? new Thickness(0, 0, 0, -3) : new Thickness(0, -3, 0, 0);

            _marked.Add(edge);
        }

        private void Unmark()
        {
            foreach (ListBoxItem item in _marked)
            {
                item.BorderBrush = Brushes.Transparent;
                item.BorderThickness = new Thickness(0);
                item.Margin = new Thickness(0);
            }

            _marked.Clear();
        }
    }
}
