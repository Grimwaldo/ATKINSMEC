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
    //   - "Take out of apartment" gives an interior room a row of its own,
    //     "Put back" returns it;
    //   - "Reset this level" forgets everything chosen for it.
    //
    // REVIT-FREE, like VENTINTERIORDIALOG: the window hands in a function
    // that works a level's rows out of what it has already read - the same
    // RoomsOn the levels list uses, so this shows exactly what the list and
    // the sheet will get. Nothing is kept until OK. tests/ventpreview
    // renders it.
    public class VENTARRANGEDIALOG : Window
    {
        private readonly Func<string, VENTARRANGE.Arrangement, List<VENTARRANGE.Row>> _rowsOf;
        private readonly VENTARRANGE.Arrangement _work;

        private string _level;
        private List<VENTARRANGE.Row> _rows = new List<VENTARRANGE.Row>();

        private readonly ComboBox _levelBox;
        private readonly ListBox _list;
        private readonly TextBlock _said;

        private readonly Button _up;
        private readonly Button _down;
        private readonly Button _takeOut;
        private readonly Button _putBack;
        private readonly Button _reset;

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
            Func<string, VENTARRANGE.Arrangement, List<VENTARRANGE.Row>> rowsOf,
            VENTARRANGE.Arrangement arrangement)
        {
            _rowsOf = rowsOf ?? ((l, a) => new List<VENTARRANGE.Row>());
            _work = (arrangement ?? new VENTARRANGE.Arrangement()).Clone();

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

            Put(grid, 1, 0, 2, Hint(
                "One level at a time, in the order it will be written. Drag a row to move it within its level - " +
                "an apartment moves with its rooms, and a room inside an apartment moves within that apartment. " +
                "Take an interior (IND) room out of its apartment to give it a row of its own. The levels list " +
                "and every export follow this."));

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
            _takeOut = SideButton("Take out of apartment", side);
            _takeOut.Margin = new Thickness(0, 18, 0, 0);
            _putBack = SideButton("Put back in apartment", side);
            _reset = SideButton("Reset this level", side);
            _reset.Margin = new Thickness(0, 18, 0, 0);

            _up.ToolTip = "Ctrl+Up";
            _down.ToolTip = "Ctrl+Down";
            _takeOut.ToolTip = "Write the picked interior rooms as rows of their own, by their own names";
            _putBack.ToolTip = "Write the picked rooms under their apartment again";
            _reset.ToolTip = "Forget the order and every room taken out on this level";

            Put(grid, 3, 1, 1, side);

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
            _reset.Click += (s, e) => { VENTARRANGE.Reset(_work, _rows, _level); Fill(null); };

            _list.SelectionChanged += (s, e) => Enable();

            _list.PreviewKeyDown += (s, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                    return;

                if (e.Key == Key.Up) { MovePicked(-1); e.Handled = true; }
                else if (e.Key == Key.Down) { MovePicked(+1); e.Handled = true; }
            };

            Dragging();

            _levelBox.SelectionChanged += (s, e) =>
            {
                var item = _levelBox.SelectedItem as ComboBoxItem;

                _level = item == null ? null : item.Tag as string;

                Fill(null);
            };

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

        // The level's rows worked out afresh with the arrangement as it now
        // stands, and the list rebuilt - keeping the rows that were picked,
        // or picking `pick` if given
        private void Fill(IEnumerable<string> pick)
        {
            var picked = new HashSet<string>(pick ?? Picked().Select(l => (l.IsApartment ? "apt " : string.Empty) + l.Key));

            _rows = _level == null ? new List<VENTARRANGE.Row>() : _rowsOf(_level, _work) ?? new List<VENTARRANGE.Row>();

            _list.Items.Clear();

            foreach (Line line in Lines(_rows))
            {
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

            if (row.IsTakenOut)
            {
                name.Inlines.Add(new System.Windows.Documents.Run(
                    "   taken out of " + (row.Home + " " + row.HomeNumber).Trim())
                {
                    Foreground = Warn,
                    FontStyle = FontStyles.Italic,
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

            _up.IsEnabled = _down.IsEnabled = picked.Count == 1;
            _takeOut.IsEnabled = picked.Any(l => l.Row != null && !string.IsNullOrEmpty(l.Row.ApartmentKey));
            _putBack.IsEnabled = picked.Any(l => l.Row != null && l.Row.IsTakenOut);
            _reset.IsEnabled = _rows.Count > 0;
        }

        private void Count()
        {
            if (_level == null)
            {
                _said.Text = "No level has rooms to arrange - tick a linked model first.";
                return;
            }

            int apartments = _rows.Where(r => !string.IsNullOrEmpty(r.ApartmentKey)).Select(r => r.ApartmentKey).Distinct().Count();
            int takenOut = _rows.Count(r => r.IsTakenOut);
            int unticked = _rows.Count(r => !r.Exported);

            bool chosen = _work.OrderOf(LevelContainer) != null ||
                          _rows.Any(r => !string.IsNullOrEmpty(r.ApartmentKey) &&
                                         _work.OrderOf(VENTARRANGE.ApartmentContainer(r.ApartmentKey)) != null);

            _said.Text = _rows.Count + " room(s) on " + _level +
                         (apartments > 0 ? ", " + apartments + " apartment(s)" : string.Empty) +
                         (takenOut > 0 ? ", " + takenOut + " taken out of their apartment" : string.Empty) +
                         (unticked > 0 ? ", " + unticked + " unticked" : string.Empty) + ". " +
                         (chosen ? "In the order chosen here." : "In the default order - by number.");
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

            List<string> defaults = VENTARRANGE.SequenceOf(_rowsOf(_level, plain) ?? new List<VENTARRANGE.Row>(), container);

            VENTARRANGE.SetOrder(_work, container, sequence, defaults);
        }

        private void MovePicked(int by)
        {
            List<Line> picked = Picked();

            if (picked.Count != 1)
                return;

            Line line = picked[0];

            List<string> sequence = VENTARRANGE.SequenceOf(_rows, line.Container);

            int at = sequence.IndexOf(line.Key);

            if (at < 0 || at + by < 0 || at + by >= sequence.Count)
                return;

            Reorder(line.Container, VENTARRANGE.Moved(sequence, line.Key, at + by));

            Fill(new[] { (line.IsApartment ? "apt " : string.Empty) + line.Key });
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
