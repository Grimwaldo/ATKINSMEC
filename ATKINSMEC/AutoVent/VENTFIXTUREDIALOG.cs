using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ATKINSMEC
{
    // WHICH MODELS THE FIXTURES ARE COUNTED FROM, AND WHICH FAMILIES COUNT.
    // Opened from the export tab's "Fixtures..." button.
    //
    // Built in code rather than XAML - it is two tick lists and a button
    // - and REVIT-FREE: the families are read by a function the window
    // hands in, so tests/ventpreview renders it with no model.
    //
    // ONE ROW PER MODEL, not per placement (user, 2026-10-17: "list the
    // linked models themselves instead of listing all the instances").
    // An interior model placed on forty floors was forty rows to tick. The
    // window still hands in placements; a model is every placement whose
    // label starts with the same title (ModelOf - RVTLINKSELECTION's
    // labels are "title" or "title : which : where"), and a ticked model
    // stores all its placements. The export adds any placed since
    // (VENTFIXTURES.EveryPlacement).
    //
    // BOTH LISTS PICK SEVERAL ROWS (user, same day), a file list's rules:
    // click, Ctrl+click, Shift+click, Ctrl+A. Ticking a picked row ticks
    // every picked row the same way; Space does it from the keyboard. The
    // families have a search box that only hides rows - ticks stand, and
    // picks, Ctrl+A and a tick act on the rows SHOWING (the levels list's
    // rule, VENTLEVELROWS.Search).
    //
    // NOTHING CHANGES BY OPENING IT. The lists arrive ticked exactly as an
    // export would count (VENTSETTINGS.CountsFamily, and the room links'
    // models as the sources), and OK stores "follow the defaults" again
    // whenever the ticks still say what the defaults say - so a model
    // nobody set up keeps following its room links and the name rule as
    // they change.
    public class VENTFIXTUREDIALOG : Window
    {
        public class Source
        {
            public long Id;
            public string Label = string.Empty;
        }

        // A row of either list: a model (its placements) or a family
        private class Row
        {
            public string Key = string.Empty;
            public List<long> Ids = new List<long>();
            public int Count;
            public bool Searched;        // a family: the search can hide it
            public CheckBox Box;
            public ListBoxItem Item;

            public bool On
            {
                get { return Box.IsChecked == true; }
                set { Box.IsChecked = value; }
            }
        }

        private readonly Func<List<long>, List<VENTSETTINGS.FixtureFamily>> _read;
        private readonly List<long> _roomLinks;

        private readonly List<Row> _models = new List<Row>();
        private readonly List<Row> _families = new List<Row>();

        // Each family's tick as it was last left here, so unticking a
        // model and ticking it again gives its families back as they were
        private readonly Dictionary<string, bool> _familyOn =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly TextBox _search;
        private readonly TextBlock _searchHint;
        private readonly TextBlock _found;

        private ICollectionView _view;

        private List<VENTSETTINGS.FixtureFamily> _list = new List<VENTSETTINGS.FixtureFamily>();
        private bool _read_;

        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85));
        private static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xEA));

        // The two lists and the search - for tests/fixturedialogtest
        public ListBox ModelList { get; private set; }
        public ListBox FamilyList { get; private set; }

        public string Search
        {
            get { return _search.Text; }
            set { _search.Text = value ?? string.Empty; }
        }

        public VENTFIXTUREDIALOG(
            List<Source> sources,
            List<long> roomLinks,
            Func<List<long>, List<VENTSETTINGS.FixtureFamily>> read)
        {
            _read = read;
            _roomLinks = roomLinks ?? new List<long>();

            Title = "AUTOVENT - fixtures into No. of Units";
            Width = 600;
            Height = 660;
            MinWidth = 460;
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
                         new GridLength(140), GridLength.Auto, GridLength.Auto, GridLength.Auto,
                         new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto
                     })
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = height });
            }

            Add(grid, 0, new TextBlock
            {
                Text = "Fixtures counted into \"No. of Units\"",
                FontWeight = FontWeights.SemiBold,
                Foreground = Ink,
                Margin = new Thickness(0, 0, 0, 6)
            });

            Add(grid, 1, Hint("Ctrl or Shift picks several rows; ticking one ticks them all."));

            Add(grid, 2, Label("Count the fixtures from"));

            ModelList = List(_models);

            foreach (KeyValuePair<string, List<long>> model in Models(sources))
            {
                bool on = VENTSETTINGS.FixtureSources.Count > 0
                    ? model.Value.Any(VENTSETTINGS.FixtureSources.Contains)
                    : model.Value.Any(_roomLinks.Contains);

                Row row = MakeRow(_models, model.Key,
                    model.Value.Count > 1 ? model.Value.Count + " placements" : string.Empty, on);

                row.Ids = model.Value;

                _models.Add(row);
            }

            ModelList.ItemsSource = _models.Select(r => r.Item).ToList();

            Add(grid, 3, ModelList);

            Add(grid, 4, new TextBlock
            {
                Text = "A plumbing model and the architect's together count every WC twice.",
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });

            Add(grid, 5, Label("Fixture families that count"));

            // The search box, with its placeholder laid over it
            _search = new TextBox { Padding = new Thickness(4, 3, 4, 3) };
            _searchHint = new TextBlock
            {
                Text = "Search fixture families",
                Foreground = Muted,
                Margin = new Thickness(8, 4, 0, 0),
                IsHitTestVisible = false
            };

            var searchBox = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            searchBox.Children.Add(_search);
            searchBox.Children.Add(_searchHint);
            Add(grid, 6, searchBox);

            FamilyList = List(_families);
            Add(grid, 7, FamilyList);

            _found = new TextBlock
            {
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };

            Add(grid, 8, _found);

            var bottom = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
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

            Add(grid, 9, bottom);

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

            // Read from what the window already has (never the model), so
            // the list follows every model tick and is never empty for
            // want of a click
            ReadFamilies();
        }

        // THE MODEL a placement belongs to: the title its label starts with
        public static string ModelOf(string label)
        {
            string text = label ?? string.Empty;
            int cut = text.IndexOf(" : ", StringComparison.Ordinal);

            return (cut > 0 ? text.Substring(0, cut) : text).Trim();
        }

        // The placements of each model, in the order the models first appear
        public static List<KeyValuePair<string, List<long>>> Models(IEnumerable<Source> sources)
        {
            var models = new List<KeyValuePair<string, List<long>>>();
            var at = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (Source source in sources ?? Enumerable.Empty<Source>())
            {
                if (source == null)
                    continue;

                string model = ModelOf(source.Label);
                int i;

                if (!at.TryGetValue(model, out i))
                {
                    i = models.Count;
                    at[model] = i;
                    models.Add(new KeyValuePair<string, List<long>>(model, new List<long>()));
                }

                if (!models[i].Value.Contains(source.Id))
                    models[i].Value.Add(source.Id);
            }

            return models;
        }

        // How many MODELS these placements are - the window's line says
        // models, and forty placements of one model are one
        public static int ModelCount(IEnumerable<Source> sources, IEnumerable<long> ids)
        {
            var wanted = new HashSet<long>(ids ?? Enumerable.Empty<long>());

            return Models(sources).Count(m => m.Value.Any(wanted.Contains));
        }

        private static void Add(Grid grid, int row, UIElement element)
        {
            Grid.SetRow(element, row);
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
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Foreground = Ink, Margin = new Thickness(0, 4, 0, 6) };
        }

        // A list that picks several rows. Space ticks the picked rows (all
        // on unless all are on already)
        private ListBox List(List<Row> rows)
        {
            var list = new ListBox
            {
                SelectionMode = SelectionMode.Extended,
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(2)
            };

            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);

            list.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Space)
                    return;

                List<Row> picked = Picked(rows);

                if (picked.Count == 0)
                    return;

                bool on = !picked.All(r => r.On);

                foreach (Row row in picked)
                    row.On = on;

                Changed(rows);

                e.Handled = true;
            };

            return list;
        }

        // The box and the name side by side, not the name as the box's
        // content: clicking the NAME picks the row, clicking the BOX ticks
        // it. A TextBlock, never Content - an underscore in Content is an
        // access key, and family names are full of them.
        private Row MakeRow(List<Row> rows, string text, string aside, bool on)
        {
            var row = new Row { Key = text };

            row.Box = new CheckBox
            {
                IsChecked = on,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 8, 0)
            };

            AutomationProperties.SetName(row.Box, text);

            var name = new TextBlock
            {
                Text = text,
                Foreground = Ink,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = text
            };

            var side = new TextBlock
            {
                Text = aside,
                Foreground = Muted,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 4, 0)
            };

            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(name, 1);
            Grid.SetColumn(side, 2);

            line.Children.Add(row.Box);
            line.Children.Add(name);
            line.Children.Add(side);

            row.Item = new ListBoxItem { Content = line, Tag = row, Padding = new Thickness(2, 3, 2, 3) };

            row.Box.Click += (s, e) => Clicked(rows, row);

            return row;
        }

        private bool Shows(Row row)
        {
            return !row.Searched || VENTLINKFILTER.Matches(row.Key, _search.Text);
        }

        // The picked rows showing. The list's own SelectedItems, not each
        // row's IsSelected - a virtualising list need not set that on a
        // row scrolled out of sight. (WPF already unpicks a row the search
        // hides; Shows says the rule anyway.)
        private List<Row> Picked(List<Row> rows)
        {
            ListBox list = rows == _models ? ModelList : FamilyList;

            var picked = new HashSet<object>(list.SelectedItems.Cast<object>());

            return rows.Where(r => picked.Contains(r.Item) && Shows(r)).ToList();
        }

        // A box clicked: a picked row takes every picked row with it
        private void Clicked(List<Row> rows, Row row)
        {
            List<Row> picked = Picked(rows);

            if (picked.Contains(row))
            {
                foreach (Row other in picked)
                    other.On = row.On;
            }

            Changed(rows);
        }

        private void Changed(List<Row> rows)
        {
            if (rows == _models)
            {
                ReadFamilies();
                return;
            }

            foreach (Row row in _families)
                _familyOn[row.Key] = row.On;

            Count();
        }

        private List<long> TickedSources()
        {
            return _models.Where(r => r.On).SelectMany(r => r.Ids).Distinct().ToList();
        }

        private void ReadFamilies()
        {
            try
            {
                _list = _read == null
                    ? new List<VENTSETTINGS.FixtureFamily>()
                    : _read(TickedSources()) ?? new List<VENTSETTINGS.FixtureFamily>();

                _read_ = true;
            }
            catch (Exception ex)
            {
                _list = new List<VENTSETTINGS.FixtureFamily>();
                _read_ = false;
                Fill();
                _found.Text = "Could not read the fixtures: " + ex.Message;
                return;
            }

            Fill();
        }

        // The families list, each keeping the tick it was last left with
        private void Fill()
        {
            _families.Clear();

            foreach (VENTSETTINGS.FixtureFamily family in _list)
            {
                bool on;

                if (!_familyOn.TryGetValue(family.Name, out on))
                    on = family.Counted;

                Row row = MakeRow(_families, family.Name, family.Count.ToString(), on);
                row.Count = family.Count;
                row.Searched = true;

                _families.Add(row);
            }

            List<ListBoxItem> items = _families.Select(r => r.Item).ToList();

            FamilyList.ItemsSource = items;

            _view = CollectionViewSource.GetDefaultView(items);
            _view.Filter = item => Shows((Row)((ListBoxItem)item).Tag);

            Count();
        }

        // Every word typed, any order, any case - the links search's rule.
        // A picked row the search hides is no longer picked: WPF drops a
        // row its filter hides from SelectedItems (fixturedialogtest).
        private void Filter()
        {
            _searchHint.Visibility = _search.Text.Length == 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            if (_view != null)
                _view.Refresh();

            Count();
        }

        private void Count()
        {
            if (_list.Count == 0)
            {
                _found.Text = "No plumbing fixtures in the ticked models.";
                return;
            }

            int ticked = _families.Count(r => r.On);
            int showing = _families.Count(Shows);

            _found.Text = ticked + " of " + _families.Count + " ticked, " +
                          _families.Where(r => r.On).Sum(r => r.Count) + " fixture(s)" +
                          (showing < _families.Count ? "  -  " + showing + " showing" : string.Empty) +
                          ". None ticked: WCs and urinals, by name.";
        }

        // What the ticks say - stored as "follow the defaults" whenever
        // they still say what the defaults say
        private void Keep()
        {
            var ticked = new HashSet<Row>(_models.Where(r => r.On));
            var byDefault = new HashSet<Row>(_models.Where(r => r.Ids.Any(_roomLinks.Contains)));

            VENTSETTINGS.FixtureSources.Clear();

            if (!ticked.SetEquals(byDefault))
            {
                foreach (long id in TickedSources())
                    VENTSETTINGS.FixtureSources.Add(id);
            }

            if (_read_)
            {
                var on = new HashSet<string>(
                    _families.Where(r => r.On).Select(r => r.Key),
                    StringComparer.OrdinalIgnoreCase);

                var byRule = new HashSet<string>(
                    _list.Where(f => VENTSETTINGS.LooksLikeWcOrUrinal(f.Name)).Select(f => f.Name),
                    StringComparer.OrdinalIgnoreCase);

                VENTSETTINGS.FixtureFamilies.Clear();

                if (!on.SetEquals(byRule))
                {
                    foreach (string name in on)
                        VENTSETTINGS.FixtureFamilies.Add(name);
                }
            }

            DialogResult = true;
        }
    }
}
