using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ATKINSMEC
{
    // WHICH MODELS THE FIXTURES ARE COUNTED FROM, AND WHICH FAMILIES COUNT.
    // Opened from the export tab's "Fixtures..." button.
    //
    // Built in code rather than XAML - it is two tick lists and a button
    // - and REVIT-FREE: the families are read by a function the window
    // hands in, so scratchpad/ventpreview renders it with no model.
    //
    // NOTHING CHANGES BY OPENING IT. The lists arrive ticked exactly as an
    // export would count (VENTSETTINGS.CountsFamily, and the room links as
    // the sources), and OK stores "follow the defaults" again whenever the
    // ticks still say what the defaults say - so a model nobody set up
    // keeps following its room links and the name rule as they change.
    public class VENTFIXTUREDIALOG : Window
    {
        public class Source
        {
            public long Id;
            public string Label = string.Empty;
        }

        private readonly Func<List<long>, List<VENTSETTINGS.FixtureFamily>> _read;
        private readonly List<long> _roomLinks;

        private readonly StackPanel _sources = new StackPanel();
        private readonly StackPanel _families = new StackPanel();
        private readonly TextBlock _found;

        private List<VENTSETTINGS.FixtureFamily> _list = new List<VENTSETTINGS.FixtureFamily>();
        private bool _read_;

        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85));
        private static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xEA));

        public VENTFIXTUREDIALOG(
            List<Source> sources,
            List<long> roomLinks,
            Func<List<long>, List<VENTSETTINGS.FixtureFamily>> read)
        {
            _read = read;
            _roomLinks = roomLinks ?? new List<long>();

            Title = "AUTOVENT - fixtures into No. of Units";
            Width = 600;
            Height = 640;
            MinWidth = 460;
            MinHeight = 460;
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
                         new GridLength(130), GridLength.Auto, GridLength.Auto,
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

            Add(grid, 1, Hint(
                "Each room gets the number of ticked fixtures standing in it, written to the " +
                "sheet's \"No. of Units\" - what Table 6-4's per-fixture exhaust multiplies " +
                "(so many L/s per water closet or urinal). Occupants are not touched."));

            Add(grid, 2, Label("Count the fixtures from"));

            for (int i = 0; i < (sources ?? new List<Source>()).Count; i++)
            {
                Source source = sources[i];

                bool on = VENTSETTINGS.FixtureSources.Count > 0
                    ? VENTSETTINGS.FixtureSources.Contains(source.Id)
                    : _roomLinks.Contains(source.Id);

                _sources.Children.Add(Tick(source.Label, on, source.Id));
            }

            Add(grid, 3, List(_sources));

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

            var readButton = new Button { Content = "Read the fixture families", Padding = new Thickness(12, 4, 12, 4) };
            readButton.Click += (s, e) => ReadFamilies();
            buttons.Children.Add(readButton);

            buttons.Children.Add(new TextBlock
            {
                Text = "A plumbing model linked as well as the architect's would count every WC twice - tick one.",
                Foreground = Muted,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                MaxWidth = 320
            });

            Add(grid, 4, buttons);

            Add(grid, 5, Label("Fixture families that count"));

            Add(grid, 6, List(_families));

            _found = Hint("Tick none to count by name: water closets, WCs, toilets and urinals.");
            Add(grid, 7, _found);

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

            Add(grid, 8, bottom);

            Content = grid;

            // The list is read as the dialog opens, so it is never empty
            // for want of a click
            Loaded += (s, e) => ReadFamilies();
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
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Foreground = Ink, Margin = new Thickness(0, 4, 0, 6) };
        }

        private static Border List(StackPanel panel)
        {
            return new Border
            {
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = panel,
                    Padding = new Thickness(6, 4, 6, 4)
                }
            };
        }

        // A TextBlock inside, never Content: an underscore in Content is
        // an access key, and family names are full of them
        private static CheckBox Tick(string text, bool on, object tag)
        {
            return new CheckBox
            {
                Content = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis },
                IsChecked = on,
                Tag = tag,
                Margin = new Thickness(2, 3, 2, 3),
                ToolTip = text
            };
        }

        private List<long> TickedSources()
        {
            return _sources.Children.OfType<CheckBox>()
                .Where(c => c.IsChecked == true)
                .Select(c => (long)c.Tag)
                .ToList();
        }

        private void ReadFamilies()
        {
            _families.Children.Clear();

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
                _found.Text = "Could not read the fixtures: " + ex.Message;
                return;
            }

            foreach (VENTSETTINGS.FixtureFamily family in _list)
                _families.Children.Add(Tick(family.Name + "     (" + family.Count + ")", family.Counted, family.Name));

            int counted = _list.Where(f => f.Counted).Sum(f => f.Count);

            _found.Text = _list.Count == 0
                ? "No plumbing fixtures in the ticked models."
                : _list.Count + " fixture famil(ies) found; the ticked ones hold " + counted +
                  " fixture(s). Tick none to count by name: water closets, WCs, toilets and urinals.";
        }

        // What the ticks say - stored as "follow the defaults" whenever
        // they still say what the defaults say
        private void Keep()
        {
            List<long> sources = TickedSources();

            VENTSETTINGS.FixtureSources.Clear();

            if (!(sources.Count == _roomLinks.Count && sources.All(_roomLinks.Contains)))
            {
                foreach (long id in sources)
                    VENTSETTINGS.FixtureSources.Add(id);
            }

            if (_read_)
            {
                var ticked = new HashSet<string>(
                    _families.Children.OfType<CheckBox>()
                        .Where(c => c.IsChecked == true)
                        .Select(c => (string)c.Tag),
                    StringComparer.OrdinalIgnoreCase);

                var byRule = new HashSet<string>(
                    _list.Where(f => VENTSETTINGS.LooksLikeWcOrUrinal(f.Name)).Select(f => f.Name),
                    StringComparer.OrdinalIgnoreCase);

                VENTSETTINGS.FixtureFamilies.Clear();

                if (!ticked.SetEquals(byRule))
                {
                    foreach (string name in ticked)
                        VENTSETTINGS.FixtureFamilies.Add(name);
                }
            }

            DialogResult = true;
        }
    }
}
