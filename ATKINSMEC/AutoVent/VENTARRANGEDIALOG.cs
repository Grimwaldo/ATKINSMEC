using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ATKINSMEC
{
    // ARRANGE ROOMS... - one level of the sheet at a time, as it will be
    // written: its ordinary rooms and its apartments, each apartment with
    // its interior rooms under it. The rules are VENTARRANGE's; this only
    // shows them and takes the drags and the presses.
    //
    //   - drag a row (or Move up / Move down, or Ctrl+Up / Ctrl+Down) to
    //     move it WITHIN ITS LEVEL; an apartment moves with its rooms, and a
    //     room inside an apartment moves only within that apartment;
    //   - "Ungroup" gives an interior room a row of its own, "Regroup"
    //     returns it under its apartment;
    //   - "Merge" makes the rooms picked one total of fresh air and extract
    //     (one FCU, one fresh air connection), "Unmerge" parts them;
    //   - "Reset this level" forgets everything chosen for it;
    //   - the SEARCH box narrows the list to the rooms whose number, name,
    //     model or apartment hold every word typed (VENTARRANGE.Shown), each
    //     found room with its apartment's heading; the line under the list
    //     names the other levels with matches, and a click opens one. It
    //     only hides rows: a move steps past the next row SHOWING
    //     (VENTARRANGE.Stepped), and nothing hidden is changed.
    //
    // REVIT-FREE, like VENTINTERIORDIALOG: the window hands in a function
    // that works every level's rows out of what it has already read - the
    // same RoomsOn the levels list uses, so this shows exactly what the
    // list and the sheet will get. Nothing is kept until OK.
    // tests/ventpreview renders it.
    public class VENTARRANGEDIALOG : Window
    {
        private readonly Func<VENTARRANGE.Arrangement, Dictionary<string, List<VENTARRANGE.Row>>> _rowsOf;
        private readonly VENTARRANGE.Arrangement _work;

        private string _level;

        // Every level's rows as the arrangement now stands, and this level's
        private Dictionary<string, List<VENTARRANGE.Row>> _all =
            new Dictionary<string, List<VENTARRANGE.Row>>(StringComparer.OrdinalIgnoreCase);

        private List<VENTARRANGE.Row> _rows = new List<VENTARRANGE.Row>();

        // What the search leaves showing on this level - null: everything
        private HashSet<string> _shown;

        private readonly IList<string> _levels;

        private readonly ComboBox _levelBox;
        private readonly TextBox _search;
        private readonly TextBlock _searchHint;
        private readonly ListBox _list;
        private readonly TextBlock _said;

        private readonly Button _up;
        private readonly Button _down;
        private readonly Button _takeOut;
        private readonly Button _putBack;
        private readonly Button _merge;
        private readonly Button _unmerge;
        private readonly Button _reset;
        private readonly Button _rename;
        private readonly Button _replace;

        // What OK hands back
        public VENTARRANGE.Arrangement Result { get; private set; }

        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85));
        private static readonly Brush Faint = new SolidColorBrush(Color.FromRgb(0xA0, 0xA8, 0xB2));
        private static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xEA));
        private static readonly Brush Band = new SolidColorBrush(Color.FromRgb(0xEE, 0xF3, 0xF8));
        private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x16, 0x5E, 0x9B));
        private static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0x9A, 0x5B, 0x00));

        // One line of the list
        private class Line
        {
            public bool IsApartment;
            public string Key = string.Empty;        // the room's key, or the apartment's
            public string Container = string.Empty;  // what it moves within
            public string UnitKey = string.Empty;    // its top row: itself, or its apartment
            public VENTARRANGE.Row Row;              // null on an apartment's heading
        }

        public VENTARRANGEDIALOG(
            IList<string> levels, string level,
            Func<VENTARRANGE.Arrangement, Dictionary<string, List<VENTARRANGE.Row>>> rowsOf,
            VENTARRANGE.Arrangement arrangement)
        {
            _rowsOf = rowsOf ?? (a => new Dictionary<string, List<VENTARRANGE.Row>>());
            _work = (arrangement ?? new VENTARRANGE.Arrangement()).Clone();
            _levels = levels ?? new List<string>();

            Title = "AUTOVENT - arrange rooms for the sheet";
            Width = 760;
            Height = 700;
            MinWidth = 600;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF8));
            UseLayoutRounding = true;

            var grid = new Grid { Margin = new Thickness(18) };

            foreach (GridLength height in new[]
                     {
                         GridLength.Auto, GridLength.Auto, GridLength.Auto,
                         new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto
                     })
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = height });
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Put(grid, 0, 0, 2, new TextBlock
            {
                Text = "Arrange rooms for the sheet",
                FontWeight = FontWeights.SemiBold,
                Foreground = Ink,
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Short, at the user's request: "do not overexplain things in the
            // autovent addin"
            Put(grid, 1, 0, 2, Hint("Drag to reorder. Rows stay within their level and apartment."));

            var levelRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

            levelRow.Children.Add(new TextBlock
            {
                Text = "Level",
                Foreground = Ink,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });

            _levelBox = new ComboBox { MinWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };

            foreach (string name in levels ?? new List<string>())
            {
                // A TextBlock, never a bare string: an underscore in a
                // ComboBoxItem's content is an access key
                _levelBox.Items.Add(new ComboBoxItem { Content = new TextBlock { Text = name }, Tag = name });
            }

            levelRow.Children.Add(_levelBox);

            // THE SEARCH, on the right of the same row - the links card's
            // box, the same placeholder trick: a hint laid over the box,
            // seen through it while it is empty
            var searchCell = new Grid { Width = 240, Margin = new Thickness(12, 0, 0, 0) };

            _search = new TextBox
            {
                Padding = new Thickness(6, 3, 6, 3),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Number, name, model or apartment (Ctrl+F)"
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
            levelRow.LastChildFill = false;
            levelRow.Children.Add(searchCell);

            Put(grid, 2, 0, 2, levelRow);

            _list = new ListBox
            {
                SelectionMode = SelectionMode.Extended,
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                AllowDrop = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);

            Put(grid, 3, 0, 1, _list);

            var side = new StackPanel { Margin = new Thickness(12, 0, 0, 0), Width = 190 };

            _up = SideButton("Move up", side);
            _down = SideButton("Move down", side);
            _rename = SideButton("Rename...", side);
            _rename.Margin = new Thickness(0, 18, 0, 0);
            _replace = SideButton("Find and replace...", side);
            // "Ungroup" / "Regroup" - the user's words (2026-10-12) for
            // what were "Take out of apartment" / "Put back in apartment"
            _takeOut = SideButton("Ungroup", side);
            _takeOut.Margin = new Thickness(0, 18, 0, 0);
            _takeOut.ToolTip = "Out of its apartment: a row of its own";
            _putBack = SideButton("Regroup", side);
            _putBack.ToolTip = "Back under its apartment";
            // Rooms one FCU serves through one fresh air connection (the
            // user, 2026-10-12)
            _merge = SideButton("Merge", side);
            _merge.Margin = new Thickness(0, 18, 0, 0);
            _merge.ToolTip = "One total of fresh air and extract";
            _unmerge = SideButton("Unmerge", side);
            _unmerge.ToolTip = "Rows of their own again";
            _reset = SideButton("Reset this level", side);
            _reset.Margin = new Thickness(0, 18, 0, 0);

            _up.ToolTip = "Ctrl+Up";
            _down.ToolTip = "Ctrl+Down";
            _rename.ToolTip = "F2, or double-click";

            // Scrolls rather than clips when the window is short
            Put(grid, 3, 1, 1, new ScrollViewer
            {
                Content = side,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });

            _said = Hint(string.Empty);
            _said.Margin = new Thickness(0, 8, 0, 0);
            Put(grid, 4, 0, 2, _said);

            var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };

            var cancel = new Button
            {
                Content = "Cancel",
                Width = 90,
                Padding = new Thickness(0, 4, 0, 4),
                Margin = new Thickness(8, 0, 0, 0),
                IsCancel = true
            };

            var ok = new Button { Content = "OK", Width = 90, Padding = new Thickness(0, 4, 0, 4), IsDefault = true };
            ok.Click += (s, e) => { Result = _work; DialogResult = true; };

            DockPanel.SetDock(cancel, Dock.Right);
            DockPanel.SetDock(ok, Dock.Right);

            bottom.Children.Add(cancel);
            bottom.Children.Add(ok);

            Put(grid, 5, 0, 2, bottom);

            Content = grid;

            // ---- what the presses do
            _up.Click += (s, e) => MovePicked(-1);
            _down.Click += (s, e) => MovePicked(+1);
            _takeOut.Click += (s, e) => TakeOutPicked();
            _putBack.Click += (s, e) => PutBackPicked();
            _merge.Click += (s, e) => MergePicked();
            _unmerge.Click += (s, e) => UnmergePicked();
            _reset.Click += (s, e) => { VENTARRANGE.Reset(_work, _rows, _level); Fill(null); };
            _rename.Click += (s, e) => RenamePicked();
            _replace.Click += (s, e) => ReplaceNames();

            _list.SelectionChanged += (s, e) => Enable();

            _list.MouseDoubleClick += (s, e) =>
            {
                if (ItemAt(e.OriginalSource as DependencyObject) != null)
                    RenamePicked();
            };

            _list.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F2)
                {
                    RenamePicked();
                    e.Handled = true;
                    return;
                }

                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                    return;

                if (e.Key == Key.Up) { MovePicked(-1); e.Handled = true; }
                else if (e.Key == Key.Down) { MovePicked(+1); e.Handled = true; }
            };

            Dragging();

            // Another level is only another slice of what is worked out
            // already; the search stays, so one keyword can be followed
            // from level to level
            _levelBox.SelectionChanged += (s, e) =>
            {
                var item = _levelBox.SelectedItem as ComboBoxItem;

                _level = item == null ? null : item.Tag as string;

                Show(null);
            };

            _search.TextChanged += (s, e) =>
            {
                _searchHint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                Show(null);
            };

            // Esc clears the search rather than closing the window - Cancel
            // answers Esc everywhere else
            _search.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && _search.Text.Length > 0)
                {
                    _search.Clear();
                    e.Handled = true;
                }
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    _search.Focus();
                    _search.SelectAll();
                    e.Handled = true;
                }
            };

            // Every level worked out once before the first is shown - a
            // level change only shows another slice of it
            _all = _rowsOf(_work) ?? new Dictionary<string, List<VENTARRANGE.Row>>(StringComparer.OrdinalIgnoreCase);

            // The level asked for, else the first
            int start = (levels ?? new List<string>()).ToList().FindIndex(
                l => string.Equals(l, level, StringComparison.OrdinalIgnoreCase));

            if (_levelBox.Items.Count > 0)
                _levelBox.SelectedIndex = Math.Max(0, start);
            else
                Fill(null);
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

        // ==================================================
        // The list
        // ==================================================

        // Every level's rows worked out afresh with the arrangement as it
        // now stands - after any change to it - and the list shown again
        private void Fill(IEnumerable<string> pick)
        {
            _all = _rowsOf(_work) ?? new Dictionary<string, List<VENTARRANGE.Row>>(StringComparer.OrdinalIgnoreCase);

            Show(pick);
        }

        private static List<VENTARRANGE.Row> RowsOn(Dictionary<string, List<VENTARRANGE.Row>> all, string level)
        {
            List<VENTARRANGE.Row> rows;

            return level != null && all != null && all.TryGetValue(level, out rows) && rows != null
                ? rows
                : new List<VENTARRANGE.Row>();
        }

        private string Query
        {
            get { return _search == null ? string.Empty : _search.Text ?? string.Empty; }
        }

        // The list rebuilt for this level and the search as they stand -
        // keeping the rows that were picked, or picking `pick` if given
        private void Show(IEnumerable<string> pick)
        {
            var picked = new HashSet<string>(pick ?? Picked().Select(l => (l.IsApartment ? "apt " : string.Empty) + l.Key));

            _rows = RowsOn(_all, _level);

            _shown = string.IsNullOrWhiteSpace(Query) ? null : VENTARRANGE.Shown(_rows, Query);

            _list.Items.Clear();

            foreach (Line line in Lines(_rows))
            {
                if (_shown != null && !_shown.Contains(line.Key))
                    continue;

                var item = new ListBoxItem
                {
                    Content = Draw(line),
                    Tag = line,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0, 2, 0, 2),
                    BorderBrush = Brushes.Transparent
                };

                _list.Items.Add(item);

                if (picked.Contains((line.IsApartment ? "apt " : string.Empty) + line.Key))
                    item.IsSelected = true;
            }

            Count();
            Enable();
        }

        private string LevelContainer
        {
            get { return VENTARRANGE.LevelContainer(_level); }
        }

        private List<Line> Lines(IList<VENTARRANGE.Row> rows)
        {
            var lines = new List<Line>();

            foreach (VENTARRANGE.Unit<VENTARRANGE.Row> unit in VENTARRANGE.Units(rows, r => r.Key, r => r.ApartmentKey))
            {
                if (!unit.IsApartment)
                {
                    lines.Add(new Line
                    {
                        Key = unit.Key,
                        Container = LevelContainer,
                        UnitKey = unit.Key,
                        Row = unit.Members[0]
                    });

                    continue;
                }

                lines.Add(new Line
                {
                    IsApartment = true,
                    Key = unit.Key,
                    Container = LevelContainer,
                    UnitKey = unit.Key
                });

                foreach (VENTARRANGE.Row room in unit.Members)
                {
                    lines.Add(new Line
                    {
                        Key = room.Key,
                        Container = VENTARRANGE.ApartmentContainer(unit.Key),
                        UnitKey = unit.Key,
                        Row = room
                    });
                }
            }

            return lines;
        }

        // What one line looks like. Text in TextBlocks only: an underscore
        // in Content is an access key, and Revit names are full of them.
        private UIElement Draw(Line line)
        {
            var panel = new DockPanel { Margin = new Thickness(4, 3, 6, 3) };

            panel.Children.Add(new TextBlock
            {
                Text = "≡",
                Foreground = Faint,
                Width = 18,
                ToolTip = "Drag to move",
                VerticalAlignment = VerticalAlignment.Center
            });

            if (line.IsApartment)
            {
                VENTARRANGE.Row first = _rows.FirstOrDefault(r => r.ApartmentKey == line.Key);
                int rooms = _rows.Count(r => r.ApartmentKey == line.Key);

                var head = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Ink };

                head.Inlines.Add(new System.Windows.Documents.Run(first == null ? line.Key : first.Apartment)
                {
                    FontWeight = FontWeights.SemiBold
                });

                if (first != null && first.Apartment != first.ModelApartment)
                {
                    head.Inlines.Add(Renamed());
                    head.ToolTip = "Model: " + first.ModelApartment;
                }

                head.Inlines.Add(new System.Windows.Documents.Run(
                    "   " + (first == null ? string.Empty : first.ApartmentNumber) + "   -   apartment, " + rooms + " room(s)")
                {
                    Foreground = Muted,
                    FontSize = 12
                });

                panel.Children.Add(head);

                return new Border { Background = Band, Child = panel };
            }

            VENTARRANGE.Row row = line.Row;

            bool inside = !string.IsNullOrEmpty(row.ApartmentKey);

            var number = new TextBlock
            {
                Text = row.Number,
                Width = 110,
                Margin = new Thickness(inside ? 22 : 0, 0, 0, 0),
                Foreground = row.Exported ? Muted : Faint,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(number);

            var name = new TextBlock
            {
                Foreground = row.Exported ? Ink : Faint,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            name.Inlines.Add(new System.Windows.Documents.Run(row.Name));

            if (row.Name != row.ModelName)
            {
                name.Inlines.Add(Renamed());
                name.ToolTip = "Model: " + row.ModelName;
            }

            if (row.IsTakenOut)
            {
                name.Inlines.Add(new System.Windows.Documents.Run(
                    "   ungrouped from " + (row.Home + " " + row.HomeNumber).Trim())
                {
                    Foreground = Warn,
                    FontStyle = FontStyles.Italic,
                    FontSize = 12
                });
            }

            List<string> merged = _work.MergeOf(row.Key);

            if (merged != null)
            {
                name.Inlines.Add(new System.Windows.Documents.Run(
                    "   merged with " + string.Join(", ", merged.Where(k => k != row.Key).Select(NumberOf)))
                {
                    Foreground = Accent,
                    FontSize = 12
                });
            }

            if (!row.Exported)
            {
                name.Inlines.Add(new System.Windows.Documents.Run("   unticked - not exported")
                {
                    Foreground = Faint,
                    FontSize = 12
                });
            }

            if (!string.IsNullOrEmpty(row.LinkName))
            {
                name.Inlines.Add(new System.Windows.Documents.Run("   " + row.LinkName)
                {
                    Foreground = Faint,
                    FontSize = 11
                });
            }

            panel.Children.Add(name);

            return panel;
        }

        // A room by its number, else its name - as the list shows it
        private string NumberOf(string key)
        {
            VENTARRANGE.Row row = _rows.FirstOrDefault(r => r.Key == key);

            if (row == null)
                return "?";

            return string.IsNullOrWhiteSpace(row.Number) ? row.Name : row.Number;
        }

        private static System.Windows.Documents.Run Renamed()
        {
            return new System.Windows.Documents.Run("  renamed") { Foreground = Accent, FontSize = 11 };
        }

        private List<Line> Picked()
        {
            return _list.SelectedItems
                .OfType<ListBoxItem>()
                .Select(i => i.Tag as Line)
                .Where(l => l != null)
                .ToList();
        }

        private void Enable()
        {
            List<Line> picked = Picked();

            _up.IsEnabled = _down.IsEnabled = _rename.IsEnabled = picked.Count == 1;
            _takeOut.IsEnabled = picked.Any(l => l.Row != null && !string.IsNullOrEmpty(l.Row.ApartmentKey));
            _putBack.IsEnabled = picked.Any(l => l.Row != null && l.Row.IsTakenOut);
            _merge.IsEnabled = picked.Count(l => l.Row != null) > 1;
            _unmerge.IsEnabled = picked.Any(l => l.Row != null && _work.MergeOf(l.Row.Key) != null);
            _reset.IsEnabled = _rows.Count > 0;
        }

        private void Count()
        {
            if (_level == null)
            {
                _said.Text = "No rooms - tick a linked model first.";
                return;
            }

            int apartments = _rows.Where(r => !string.IsNullOrEmpty(r.ApartmentKey)).Select(r => r.ApartmentKey).Distinct().Count();
            int takenOut = _rows.Count(r => r.IsTakenOut);
            int unticked = _rows.Count(r => !r.Exported);

            int merged = _rows.Select(r => _work.MergeIdOf(r.Key)).Where(m => m != null).Distinct().Count();

            int renamed = _rows.Count(r => r.Name != r.ModelName) +
                          _rows.Where(r => !string.IsNullOrEmpty(r.ApartmentKey) && r.Apartment != r.ModelApartment)
                               .Select(r => r.ApartmentKey).Distinct().Count();

            string summary = _rows.Count + " room(s)" +
                             (apartments > 0 ? ", " + apartments + " apartment(s)" : string.Empty) +
                             (takenOut > 0 ? ", " + takenOut + " ungrouped" : string.Empty) +
                             (merged > 0 ? ", " + merged + " merged group(s)" : string.Empty) +
                             (renamed > 0 ? ", " + renamed + " renamed" : string.Empty) +
                             (unticked > 0 ? ", " + unticked + " unticked" : string.Empty) + ".";

            if (_shown == null)
            {
                _said.Text = summary;
                return;
            }

            // SEARCHING: what it found here, and where else - each other
            // level with a match a click away
            string query = Query.Trim();

            int found = _rows.Count(r => _shown.Contains(r.Key));

            _said.Inlines.Clear();

            _said.Inlines.Add(new System.Windows.Documents.Run(found == 0
                ? "No match on " + _level
                : found + " of " + _rows.Count + " match"));

            var elsewhere = _levels
                .Where(l => !string.Equals(l, _level, StringComparison.OrdinalIgnoreCase))
                .Select(l => new { Level = l, Found = RowsOn(_all, l).Count(r => VENTLINKFILTER.Matches(VENTARRANGE.Words(r), query)) })
                .Where(x => x.Found > 0)
                .ToList();

            if (elsewhere.Count == 0)
            {
                _said.Inlines.Add(new System.Windows.Documents.Run("."));
            }
            else
            {
                _said.Inlines.Add(new System.Windows.Documents.Run(found == 0 ? " - found on " : " - also on "));

                for (int i = 0; i < elsewhere.Count; i++)
                {
                    if (i > 0)
                        _said.Inlines.Add(new System.Windows.Documents.Run(", "));

                    string level = elsewhere[i].Level;

                    var link = new System.Windows.Documents.Hyperlink(
                        new System.Windows.Documents.Run(level + " (" + elsewhere[i].Found + ")"))
                    {
                        Foreground = Accent,
                        ToolTip = "Show " + level
                    };

                    link.Click += (s, e) => OpenLevel(level);

                    _said.Inlines.Add(link);
                }

                _said.Inlines.Add(new System.Windows.Documents.Run("."));
            }
        }

        // Another level in the picker - the search stays as it is
        private void OpenLevel(string level)
        {
            for (int i = 0; i < _levelBox.Items.Count; i++)
            {
                var item = _levelBox.Items[i] as ComboBoxItem;

                if (item != null && string.Equals(item.Tag as string, level, StringComparison.OrdinalIgnoreCase))
                {
                    _levelBox.SelectedIndex = i;
                    return;
                }
            }
        }

        // ==================================================
        // Moving, taking out, putting back
        // ==================================================

        // A container's order with one key moved, kept only where it is not
        // simply the default (VENTARRANGE.SetOrder)
        private void Reorder(string container, List<string> sequence)
        {
            VENTARRANGE.Arrangement plain = _work.Clone();
            plain.Orders.Remove(container);

            List<string> defaults = VENTARRANGE.SequenceOf(RowsOn(_rowsOf(plain), _level), container);

            VENTARRANGE.SetOrder(_work, container, sequence, defaults);
        }

        // One step past the next row SHOWING - with a search on, the rows it
        // hides are stepped over rather than swapped with unseen
        private void MovePicked(int by)
        {
            List<Line> picked = Picked();

            if (picked.Count != 1)
                return;

            Line line = picked[0];

            List<string> sequence = VENTARRANGE.SequenceOf(_rows, line.Container);
            List<string> moved = VENTARRANGE.Stepped(sequence, line.Key, by, _shown);

            if (moved.SequenceEqual(sequence, StringComparer.Ordinal))
                return;

            Reorder(line.Container, moved);

            Fill(new[] { (line.IsApartment ? "apt " : string.Empty) + line.Key });
        }

        // A new name for the one row picked - a room, or an apartment's
        // heading. Emptied, it goes back to the model's name.
        private void RenamePicked()
        {
            List<Line> picked = Picked();

            if (picked.Count != 1)
                return;

            Line line = picked[0];

            string current, model;

            if (line.IsApartment)
            {
                VENTARRANGE.Row first = _rows.FirstOrDefault(r => r.ApartmentKey == line.Key);

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

            Fill(new[] { (line.IsApartment ? "apt " : string.Empty) + line.Key });
        }

        // Find and replace across names - this level, or every level
        private void ReplaceNames()
        {
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
            var every = new CheckBox { Content = new TextBlock { Text = "All levels" }, IsChecked = true };

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
            at(every, 2, 1);

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

            IEnumerable<VENTARRANGE.Row> rows = every.IsChecked == true
                ? _all.Values.SelectMany(r => r)
                : _rows;

            int changed = VENTARRANGE.Replace(_work, rows.ToList(), find.Text, with.Text);

            Fill(null);

            // Said first on the line under the list, until the next change
            var said = new System.Windows.Documents.Run(changed + " name(s) changed. ") { Foreground = Accent };

            if (_said.Inlines.FirstInline == null)
                _said.Inlines.Add(said);
            else
                _said.Inlines.InsertBefore(_said.Inlines.FirstInline, said);
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
                VENTARRANGE.TakeOut(_work, line.Row, _level);

            Fill(picked.Select(l => l.Key));
        }

        private void PutBackPicked()
        {
            List<Line> picked = Picked().Where(l => l.Row != null && l.Row.IsTakenOut).ToList();

            foreach (Line line in picked)
                VENTARRANGE.PutBack(_work, line.Row, _level);

            Fill(picked.Select(l => l.Key));
        }

        private void MergePicked()
        {
            List<Line> picked = Picked().Where(l => l.Row != null).ToList();

            string why = VENTARRANGE.Merge(_work, picked.Select(l => l.Row).ToList());

            Fill(picked.Select(l => l.Key));

            if (why != null)
            {
                var said = new System.Windows.Documents.Run(why + " ") { Foreground = Warn };

                if (_said.Inlines.FirstInline == null)
                    _said.Inlines.Add(said);
                else
                    _said.Inlines.InsertBefore(_said.Inlines.FirstInline, said);
            }
        }

        private void UnmergePicked()
        {
            List<Line> picked = Picked().Where(l => l.Row != null).ToList();

            VENTARRANGE.Unmerge(_work, picked.Select(l => l.Key).ToList());

            Fill(picked.Select(l => l.Key));
        }

        // ==================================================
        // Dragging
        // ==================================================

        private Point _from;
        private Line _pressed;
        private readonly List<ListBoxItem> _marked = new List<ListBoxItem>();

        private void Dragging()
        {
            _list.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _from = e.GetPosition(_list);
                ListBoxItem item = ItemAt(e.OriginalSource as DependencyObject);
                _pressed = item == null ? null : item.Tag as Line;
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

                List<string> sequence = VENTARRANGE.SequenceOf(_rows, dragged.Container);

                string onto = dragged.Container == LevelContainer ? target.UnitKey : target.Key;

                if (onto == dragged.Key)
                    return;

                sequence.Remove(dragged.Key);

                int at = sequence.IndexOf(onto);

                if (at < 0)
                    return;

                sequence.Insert(after ? at + 1 : at, dragged.Key);

                Reorder(dragged.Container, sequence);

                Fill(new[] { (dragged.IsApartment ? "apt " : string.Empty) + dragged.Key });

                e.Handled = true;
            };
        }

        private static ListBoxItem ItemAt(DependencyObject from)
        {
            while (from != null && !(from is ListBoxItem))
            {
                from = from is Visual || from is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(from)
                    : LogicalTreeHelper.GetParent(from);
            }

            return from as ListBoxItem;
        }

        // WHERE A DROP WOULD GO, and whether it is allowed there: a top row
        // (a room of its own, or an apartment) lands before or after
        // another top row of the level; a room inside an apartment only
        // before or after another room of the same apartment. Anything
        // else is refused.
        private Line TargetOf(Line dragged, DragEventArgs e, out bool after)
        {
            after = false;

            if (dragged == null)
                return null;

            ListBoxItem item = ItemAt(_list.InputHitTest(e.GetPosition(_list)) as DependencyObject);
            Line target = item == null ? null : item.Tag as Line;

            if (target == null)
                return null;

            after = e.GetPosition(item).Y > item.ActualHeight / 2;

            if (dragged.Container == LevelContainer)
            {
                // Onto any line of a unit is onto the unit; past its heading
                // is after the whole apartment
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

            if (dragged.Container == LevelContainer)
            {
                List<ListBoxItem> unit = items.Where(i => ((Line)i.Tag).UnitKey == target.UnitKey).ToList();
                edge = after ? unit.LastOrDefault() : unit.FirstOrDefault();
            }
            else
            {
                edge = items.FirstOrDefault(i => i.Tag == target);
            }

            if (edge == null)
                return;

            // One edge only, and the same 4 px in all as an unmarked row's
            // 2 + 2, so nothing below jumps while dragging
            edge.BorderBrush = Accent;
            edge.BorderThickness = after ? new Thickness(0, 0, 0, 4) : new Thickness(0, 4, 0, 0);

            _marked.Add(edge);
        }

        private void Unmark()
        {
            foreach (ListBoxItem item in _marked)
            {
                item.BorderBrush = Brushes.Transparent;
                item.BorderThickness = new Thickness(0, 2, 0, 2);
            }

            _marked.Clear();
        }
    }
}
