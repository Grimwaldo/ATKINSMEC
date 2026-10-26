using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ATKINSMEC
{
    // OCCUPANCY... - the rules that give a room its occupancy categories
    // from its name (VENTOCCUPANCY): what the name contains or equals, for which
    // rooms, and the Table 6-1 and Table 6-4 categories it gets. The
    // user chose to have them editable (2026-10-14). Office-wide: saved on
    // this PC, not per model.
    //
    // Built in code and REVIT-FREE, like VENTSYSTEMDIALOG: the lists come
    // from the add-in's own office workbook; tests/ventpreview renders it.
    // Cancel changes nothing.
    public class VENTOCCUPANCYDIALOG : Window
    {
        // The rules, once OK is pressed - null for Cancel
        public List<VENTOCCUPANCY.Rule> Result { get; private set; }

        // True when Reset was pressed and nothing changed after: the file
        // goes and the built-in rules apply again
        public bool BackToDefaults { get; private set; }

        private class Row
        {
            public ComboBox Scope;
            public ComboBox Match;
            public TextBox Words;
            public ComboBox Fresh;
            public ComboBox Exhaust;
            public ComboBox Use;
            public TextBox Units;
            public Grid Panel;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly StackPanel _list = new StackPanel();
        private readonly List<string> _fresh;
        private readonly List<string> _exhaust;
        private readonly List<string> _uses;
        private readonly TextBox _try;
        private readonly CheckBox _tryApartment;
        private readonly TextBlock _tryResult;
        private bool _reset;

        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85));
        private static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xEA));
        private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x16, 0x5E, 0x9B));

        private static readonly string[] Scopes = { "Any room", "Units", "Not units" };
        private static readonly string[] Matches = { "contains", "equals" };

        private const double ScopeWidth = 112;
        private const double MatchWidth = 84;
        private const double FreshWidth = 230;
        private const double ExhaustWidth = 248;
        private const double UseWidth = 150;
        private const double UnitsWidth = 48;

        public VENTOCCUPANCYDIALOG(IEnumerable<VENTOCCUPANCY.Rule> rules, List<string> fresh, List<string> exhaust,
                                   List<string> uses = null)
        {
            _fresh = fresh ?? new List<string>();
            _exhaust = exhaust ?? new List<string>();
            _uses = uses ?? new List<string> { VENTOCCUPANCY.HeavyUse, VENTOCCUPANCY.LightUse };

            Title = "AUTOVENT - occupancy from room names";
            Width = 1234;
            Height = 640;
            MinWidth = 1024;
            MinHeight = 440;
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
                         new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto
                     })
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = height });
            }

            Add(grid, 0, new TextBlock
            {
                Text = "Occupancy categories from room names",
                FontWeight = FontWeights.SemiBold,
                Foreground = Ink,
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Short, at the user's request: "do not overexplain things in the
            // autovent addin"
            Add(grid, 1, new TextBlock
            {
                Text = "\"Equals\" wins, then the most specific rule a name fits. Only blank cells are filled.",
                Foreground = Muted,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            });

            Add(grid, 2, Columns(new UIElement[]
                                 {
                                     Heading("For"), Heading("Match"), Heading("Name"), Heading("Outside air (Table 6-1)"),
                                     Heading("Exhaust (Table 6-4)"), Heading("Nature of use"),
                                     Heading("Units", "No. of Units, when none were counted"), null
                                 }, new Thickness(7, 0, 24, 4)));

            foreach (VENTOCCUPANCY.Rule rule in rules ?? Enumerable.Empty<VENTOCCUPANCY.Rule>())
                AddRow(rule);

            Add(grid, 3, new Border
            {
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = _list,
                    Padding = new Thickness(6, 4, 6, 4)
                }
            });

            // Add / Reset
            var edit = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

            var add = new Button { Content = "Add rule", Padding = new Thickness(12, 3, 12, 3) };
            add.Click += (s, e) =>
            {
                Row row = AddRow(new VENTOCCUPANCY.Rule());
                row.Words.Focus();
                Changed();
            };

            var reset = new Button { Content = "Reset to defaults", Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(8, 0, 0, 0) };
            reset.Click += (s, e) =>
            {
                _list.Children.Clear();
                _rows.Clear();

                foreach (VENTOCCUPANCY.Rule rule in VENTOCCUPANCY.Defaults())
                    AddRow(rule);

                Changed();
                _reset = true;
            };

            edit.Children.Add(add);
            edit.Children.Add(reset);
            Add(grid, 4, edit);

            // Try a name: which rule it takes, and what it gets
            var test = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = true };

            test.Children.Add(Docked(new TextBlock
            {
                Text = "Try a name",
                Foreground = Ink,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            }));

            _try = new TextBox { Width = 200, Padding = new Thickness(4, 3, 4, 3), Text = "MASTER BEDROOM" };
            test.Children.Add(Docked(_try));

            _tryApartment = new CheckBox
            {
                Content = new TextBlock { Text = "a unit" },
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 14, 0)
            };
            test.Children.Add(Docked(_tryApartment));

            _tryResult = new TextBlock
            {
                Foreground = Accent,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            test.Children.Add(_tryResult);

            Add(grid, 5, test);

            _try.TextChanged += (s, e) => Changed();
            _tryApartment.Click += (s, e) => Changed();

            // OK / Cancel
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var ok = new Button { Content = "OK", Width = 90, Padding = new Thickness(0, 4, 0, 4), IsDefault = true };
            ok.Click += (s, e) =>
            {
                Result = Rules();
                BackToDefaults = _reset;
                DialogResult = true;
            };

            var cancel = new Button { Content = "Cancel", Width = 90, Padding = new Thickness(0, 4, 0, 4), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            Add(grid, 6, buttons);

            Content = grid;

            Changed();
        }

        // The rules as the rows stand - a row naming nothing is left out
        public List<VENTOCCUPANCY.Rule> Rules()
        {
            return _rows
                .Where(r => VENTOCCUPANCY.Tokens(r.Words.Text).Length > 0)
                .Select(r => new VENTOCCUPANCY.Rule
                {
                    Scope = r.Scope.SelectedIndex == 1 ? VENTOCCUPANCY.Scope.Apartment
                          : r.Scope.SelectedIndex == 2 ? VENTOCCUPANCY.Scope.Other
                          : VENTOCCUPANCY.Scope.Any,
                    Exact = r.Match.SelectedIndex == 1,
                    Words = r.Words.Text.Trim(),
                    Fresh = (r.Fresh.SelectedItem as string ?? string.Empty).Trim(),
                    Exhaust = (r.Exhaust.SelectedItem as string ?? string.Empty).Trim(),
                    Use = (r.Use.SelectedItem as string ?? string.Empty).Trim(),
                    Units = (int)Math.Max(0, Math.Round(Number(r.Units.Text)))
                })
                .ToList();
        }

        // A number as typed here, or as written in the file; 0 for none
        private static double Number(string text)
        {
            double value;

            if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out value) ||
                double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                return value;

            return 0;
        }

        private Row AddRow(VENTOCCUPANCY.Rule rule)
        {
            var row = new Row
            {
                Scope = new ComboBox { ItemsSource = Scopes, VerticalAlignment = VerticalAlignment.Center },
                Match = new ComboBox { ItemsSource = Matches, VerticalAlignment = VerticalAlignment.Center },
                Words = Box(rule.Words ?? string.Empty),
                Fresh = Pick(_fresh, rule.Fresh),
                Exhaust = Pick(_exhaust, rule.Exhaust),
                Use = Pick(_uses, rule.Use),
                Units = Box(rule.Units > 0 ? rule.Units.ToString() : string.Empty)
            };

            row.Scope.SelectedIndex = rule.Scope == VENTOCCUPANCY.Scope.Apartment ? 1
                                    : rule.Scope == VENTOCCUPANCY.Scope.Other ? 2 : 0;
            row.Match.SelectedIndex = rule.Exact ? 1 : 0;

            var remove = new Button
            {
                Content = "✕",
                Width = 24,
                Padding = new Thickness(0),
                ToolTip = "Remove this rule",
                VerticalAlignment = VerticalAlignment.Center
            };

            row.Panel = Columns(new UIElement[]
            {
                row.Scope, row.Match, row.Words, row.Fresh, row.Exhaust, row.Use, row.Units, remove
            }, new Thickness(0, 2, 0, 2));

            remove.Click += (s, e) =>
            {
                _list.Children.Remove(row.Panel);
                _rows.Remove(row);
                Changed();
            };

            row.Scope.SelectionChanged += (s, e) => Changed();
            row.Match.SelectionChanged += (s, e) => Changed();
            row.Words.TextChanged += (s, e) => Changed();
            row.Fresh.SelectionChanged += (s, e) => Changed();
            row.Exhaust.SelectionChanged += (s, e) => Changed();
            row.Use.SelectionChanged += (s, e) => Changed();
            row.Units.TextChanged += (s, e) => Changed();

            _rows.Add(row);
            _list.Children.Add(row.Panel);

            return row;
        }

        private static TextBox Box(string text)
        {
            return new TextBox { Text = text, Padding = new Thickness(4, 2, 4, 2), VerticalAlignment = VerticalAlignment.Center };
        }

        private static TextBlock Heading(string text, string tip = null)
        {
            return new TextBlock { Text = text, Foreground = Muted, FontSize = 12, ToolTip = tip, TextTrimming = TextTrimming.CharacterEllipsis };
        }

        // One line's cells - for, match, name, outside air, exhaust, use,
        // units, remove - the same widths as the headings over them
        private static Grid Columns(UIElement[] cells, Thickness margin)
        {
            var line = new Grid { Margin = margin };

            foreach (GridLength width in new[]
                     {
                         new GridLength(ScopeWidth), new GridLength(MatchWidth), new GridLength(1, GridUnitType.Star),
                         new GridLength(FreshWidth),
                         new GridLength(ExhaustWidth), new GridLength(UseWidth), new GridLength(UnitsWidth),
                         new GridLength(30)
                     })
            {
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            }

            for (int c = 0; c < cells.Length; c++)
            {
                if (cells[c] == null)
                    continue;

                var element = cells[c] as FrameworkElement;

                if (element != null && c < cells.Length - 1)
                    element.Margin = new Thickness(0, 0, 6, 0);

                Grid.SetColumn(cells[c], c);
                line.Children.Add(cells[c]);
            }

            return line;
        }

        // A list with "" first (nothing written), the category picked - and
        // a category the list has not got kept, so it is not lost silently
        private static ComboBox Pick(List<string> choices, string chosen)
        {
            var items = new List<string> { string.Empty };
            items.AddRange(choices);

            string found = VENTOCCUPANCY.Pick(chosen, choices);

            if (found == null && !string.IsNullOrWhiteSpace(chosen))
            {
                found = chosen.Trim();
                items.Add(found);
            }

            return new ComboBox
            {
                ItemsSource = items,
                SelectedItem = found ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void Changed()
        {
            _reset = false;

            if (_tryResult == null)
                return;

            string name = _try.Text ?? string.Empty;

            if (name.Trim().Length == 0)
            {
                _tryResult.Text = string.Empty;
                return;
            }

            VENTOCCUPANCY.Rule rule = VENTOCCUPANCY.RuleFor(name, _tryApartment.IsChecked == true, Rules());

            _tryResult.Text = rule == null
                ? "no rule - left blank"
                : "→  " + Or(rule.Fresh) + "  /  " + Or(rule.Exhaust) + Extras(rule) +
                  "     (" + (rule.Exact ? "= " : string.Empty) + "\"" + rule.Words + "\")";
        }

        private static string Extras(VENTOCCUPANCY.Rule rule)
        {
            var said = new List<string>();

            if (!string.IsNullOrWhiteSpace(rule.Use))
                said.Add(rule.Use.Trim());

            if (rule.Units > 0)
                said.Add(rule.Units + " unit" + (rule.Units == 1 ? string.Empty : "s"));

            return said.Count == 0 ? string.Empty : "  (" + string.Join(", ", said) + ")";
        }

        private static string Or(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? "-" : category;
        }

        private static UIElement Docked(UIElement element)
        {
            DockPanel.SetDock(element, Dock.Left);
            return element;
        }

        private static void Add(Grid grid, int row, UIElement element)
        {
            Grid.SetRow(element, row);
            grid.Children.Add(element);
        }
    }
}
