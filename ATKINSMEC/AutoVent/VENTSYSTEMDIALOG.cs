using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ATKINSMEC
{
    // WHICH DUCT SYSTEM TYPES ARE FRESH AIR, OR EXTRACT - one dialog per
    // side, opened from the assign tab's two buttons. At the user's
    // request (2026-10-12) they replaced two small lists on the tab, two
    // rows tall, scrolled through twenty types.
    //
    // Built in code and REVIT-FREE: the window hands in the names and
    // their ticks, takes back the ticked names on OK, and
    // tests/ventpreview renders it with no model. Cancel changes nothing.
    public class VENTSYSTEMDIALOG : Window
    {
        public class Item
        {
            public string Name = string.Empty;
            public bool On;
        }

        // The ticked names, once OK is pressed
        public HashSet<string> Ticked { get; private set; }

        private readonly StackPanel _list = new StackPanel();
        private readonly TextBox _search;
        private readonly TextBlock _searchHint;
        private readonly TextBlock _count;

        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85));
        private static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xEA));

        // side: "Fresh air" or "Extract"; names: what the names rule
        // looks for when nothing is ticked
        public VENTSYSTEMDIALOG(string side, IEnumerable<Item> items, string names)
        {
            Title = "AUTOVENT - " + side.ToLowerInvariant() + " system types";
            Width = 460;
            Height = 560;
            MinWidth = 360;
            MinHeight = 380;
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

            Add(grid, 0, new TextBlock
            {
                Text = side + " system types",
                FontWeight = FontWeights.SemiBold,
                Foreground = Ink,
                Margin = new Thickness(0, 0, 0, 6)
            });

            Add(grid, 1, new TextBlock
            {
                Text = "Tick none and names with " + names + " decide.",
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // The search box, with its placeholder laid over it
            _search = new TextBox { Padding = new Thickness(4, 3, 4, 3) };
            _searchHint = new TextBlock
            {
                Text = "Search system types",
                Foreground = Muted,
                Margin = new Thickness(8, 4, 0, 0),
                IsHitTestVisible = false
            };

            var searchBox = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            searchBox.Children.Add(_search);
            searchBox.Children.Add(_searchHint);
            Add(grid, 2, searchBox);

            foreach (Item item in items ?? Enumerable.Empty<Item>())
                _list.Children.Add(Tick(item.Name, item.On));

            Add(grid, 3, new Border
            {
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _list,
                    Padding = new Thickness(6, 4, 6, 4)
                }
            });

            // All / None for the rows showing, and the count beside them
            var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };

            var all = new Button { Content = "All", Width = 64, Padding = new Thickness(0, 3, 0, 3) };
            all.Click += (s, e) => TickShowing(true);

            var none = new Button { Content = "None", Width = 64, Padding = new Thickness(0, 3, 0, 3), Margin = new Thickness(6, 0, 0, 0) };
            none.Click += (s, e) => TickShowing(false);

            DockPanel.SetDock(all, Dock.Left);
            DockPanel.SetDock(none, Dock.Left);
            row.Children.Add(all);
            row.Children.Add(none);

            _count = new TextBlock
            {
                Foreground = Muted,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            row.Children.Add(_count);
            Add(grid, 4, row);

            var bottom = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var ok = new Button { Content = "OK", Width = 90, Padding = new Thickness(0, 4, 0, 4), IsDefault = true };
            ok.Click += (s, e) => Keep();

            var cancel = new Button
            {
                Content = "Cancel",
                Width = 90,
                Padding = new Thickness(0, 4, 0, 4),
                Margin = new Thickness(8, 0, 0, 0),
                IsCancel = true
            };

            bottom.Children.Add(ok);
            bottom.Children.Add(cancel);
            Add(grid, 5, bottom);

            Content = grid;

            _search.TextChanged += (s, e) => Filter();

            // Esc in the search box clears it first; only then does it
            // cancel the dialog
            _search.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && _search.Text.Length > 0)
                {
                    _search.Text = string.Empty;
                    e.Handled = true;
                }
            };

            Count();
        }

        // THE LINE UNDER EACH BUTTON on the window: what is ticked, so
        // nobody has to open the dialog to know
        public static string Summary(IList<string> ticked, int total, string names)
        {
            if (total == 0)
                return "No duct system types in this model.";

            if (ticked == null || ticked.Count == 0)
                return "None ticked - names with " + names + " decide.";

            return ticked.Count + " of " + total + ":  " + string.Join(", ", ticked);
        }

        // The search typed so far - for the preview
        public string Search
        {
            get { return _search.Text; }
            set { _search.Text = value ?? string.Empty; }
        }

        private static void Add(Grid grid, int row, UIElement element)
        {
            Grid.SetRow(element, row);
            grid.Children.Add(element);
        }

        // A TextBlock inside, never Content: an underscore in Content is
        // an access key, and system type names are full of them
        private CheckBox Tick(string name, bool on)
        {
            var box = new CheckBox
            {
                Content = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis },
                IsChecked = on,
                Tag = name,
                Margin = new Thickness(2, 3, 2, 3),
                ToolTip = name
            };

            box.Click += (s, e) => Count();

            return box;
        }

        private IEnumerable<CheckBox> Boxes()
        {
            return _list.Children.OfType<CheckBox>();
        }

        // Every word typed, any order, any case - the links search's rule
        private void Filter()
        {
            string query = _search.Text;

            _searchHint.Visibility = query.Length == 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            foreach (CheckBox box in Boxes())
            {
                box.Visibility = VENTLINKFILTER.Matches((string)box.Tag, query)
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }

            Count();
        }

        private void TickShowing(bool on)
        {
            foreach (CheckBox box in Boxes().Where(b => b.Visibility == System.Windows.Visibility.Visible))
                box.IsChecked = on;

            Count();
        }

        private void Count()
        {
            int total = Boxes().Count();
            int ticked = Boxes().Count(b => b.IsChecked == true);
            int showing = Boxes().Count(b => b.Visibility == System.Windows.Visibility.Visible);

            _count.Text = total == 0
                ? "This model has no duct system types."
                : ticked + " of " + total + " ticked" +
                  (showing < total ? "  -  " + showing + " showing" : string.Empty);
        }

        private void Keep()
        {
            Ticked = new HashSet<string>(
                Boxes().Where(b => b.IsChecked == true).Select(b => (string)b.Tag),
                StringComparer.OrdinalIgnoreCase);

            DialogResult = true;
        }
    }
}
