using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ATKINSMEC
{
    // WHICH LINKED PLACEMENTS ARE INTERIOR (IND) MODELS, LEVEL BY LEVEL -
    // the "Interior models..." button on the links card. The rules are
    // VENTINTERIOR's; this only shows them and takes the ticks.
    //
    // Built in code and REVIT-FREE, like VENTFIXTUREDIALOG: the window hands
    // in each placement's room floors, read beforehand through the external
    // event - an external event cannot run while a modal dialog is up, so
    // nothing here may ask the model for anything. tests/ventpreview renders
    // it.
    //
    // NOTHING CHANGES BY OPENING IT: everything found arrives ticked unless
    // a choice was made before, and OK keeps "follow the keyword" whenever
    // the ticks still say everything found.
    public class VENTINTERIORDIALOG : Window
    {
        private readonly List<KeyValuePair<long, string>> _links;
        private readonly IDictionary<long, List<double>> _floors;
        private readonly IList<VENTLEVELMAP.Storey> _storeys;
        private readonly ICollection<long> _exclude;

        private readonly bool _wasChosen;
        private readonly Dictionary<string, HashSet<long>> _wasPicked;

        // What the user has made of each (storey, placement) seen so far,
        // so a keyword typed and taken back does not forget a tick
        private readonly Dictionary<string, bool> _state =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly TextBox _keyword;
        private readonly StackPanel _list = new StackPanel();
        private readonly TextBlock _found;

        private List<VENTINTERIOR.Found> _shown = new List<VENTINTERIOR.Found>();

        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x33));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85));
        private static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xEA));

        public VENTINTERIORDIALOG(
            List<KeyValuePair<long, string>> links,
            IDictionary<long, List<double>> floors,
            IList<VENTLEVELMAP.Storey> storeys,
            ICollection<long> exclude)
        {
            _links = links ?? new List<KeyValuePair<long, string>>();
            _floors = floors ?? new Dictionary<long, List<double>>();
            _storeys = storeys ?? new List<VENTLEVELMAP.Storey>();
            _exclude = exclude ?? new List<long>();

            _wasChosen = VENTSETTINGS.InteriorChosen;
            _wasPicked = VENTSETTINGS.InteriorPicks.ToDictionary(
                p => p.Key, p => new HashSet<long>(p.Value ?? new HashSet<long>()), StringComparer.OrdinalIgnoreCase);

            Title = "AUTOVENT - interior models";
            Width = 640;
            Height = 640;
            MinWidth = 480;
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
                         new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto
                     })
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = height });
            }

            Add(grid, 0, new TextBlock
            {
                Text = "Interior models, level by level",
                FontWeight = FontWeights.SemiBold,
                Foreground = Ink,
                Margin = new Thickness(0, 0, 0, 6)
            });

            Add(grid, 1, Hint(
                "Rooms of an interior (IND) model are written as units under the redundant room they stand in. " +
                "Every linked model whose name holds one of the keywords is listed under each level of THIS " +
                "model where it has rooms - tick the ones to use on that level."));

            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

            row.Children.Add(new TextBlock
            {
                Text = "Keywords",
                Foreground = Ink,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });

            _keyword = new TextBox
            {
                Text = VENTSETTINGS.InteriorKeyword ?? string.Empty,
                Width = 200,
                Padding = new Thickness(6, 3, 6, 3),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Commas between keywords, e.g. IND, ID - each a word of the model's name."
            };

            _keyword.TextChanged += (s, e) => Fill();

            row.Children.Add(_keyword);

            row.Children.Add(new TextBlock
            {
                Text = "Empty: every linked model is searched, as before.",
                Foreground = Muted,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 0, 0, 0)
            });

            Add(grid, 2, row);

            Add(grid, 3, new Border
            {
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _list,
                    Padding = new Thickness(8, 4, 8, 6)
                }
            });

            _found = Hint(string.Empty);
            _found.Margin = new Thickness(0, 8, 0, 0);
            Add(grid, 4, _found);

            var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };

            var all = new Button { Content = "Tick all", Width = 90, Padding = new Thickness(0, 4, 0, 4) };
            all.Click += (s, e) => TickShown(true);

            var none = new Button { Content = "None", Width = 90, Padding = new Thickness(0, 4, 0, 4), Margin = new Thickness(8, 0, 0, 0) };
            none.Click += (s, e) => TickShown(false);

            bottom.Children.Add(all);
            bottom.Children.Add(none);

            var cancel = new Button
            {
                Content = "Cancel",
                Width = 90,
                Padding = new Thickness(0, 4, 0, 4),
                Margin = new Thickness(8, 0, 0, 0),
                IsCancel = true
            };

            var ok = new Button { Content = "OK", Width = 90, Padding = new Thickness(0, 4, 0, 4), IsDefault = true };
            ok.Click += (s, e) => Keep();

            DockPanel.SetDock(cancel, Dock.Right);
            DockPanel.SetDock(ok, Dock.Right);

            bottom.Children.Add(cancel);
            bottom.Children.Add(ok);
            bottom.LastChildFill = false;

            Add(grid, 5, bottom);

            Content = grid;

            Fill();
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

        private static string KeyOf(VENTINTERIOR.Found f)
        {
            return (f.StoreyName ?? string.Empty) + "\u0001" + f.LinkId;
        }

        // A placement's tick before the user has touched it: as chosen last
        // time, or - nothing chosen - ticked, because it was found
        private bool StartsTicked(VENTINTERIOR.Found f)
        {
            if (!_wasChosen)
                return true;

            HashSet<long> on;

            return _wasPicked.TryGetValue(f.StoreyName ?? string.Empty, out on) && on.Contains(f.LinkId);
        }

        // The list for the keyword as it stands: one heading per level of
        // this model that has a candidate, the candidates under it
        private void Fill()
        {
            _list.Children.Clear();

            _shown = VENTINTERIOR.Find(_links, _floors, _storeys, _keyword.Text, _exclude);

            int? storey = null;

            foreach (VENTINTERIOR.Found f in _shown)
            {
                if (storey != f.Storey)
                {
                    storey = f.Storey;

                    _list.Children.Add(new TextBlock
                    {
                        Text = f.StoreyName,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Ink,
                        Margin = new Thickness(0, _list.Children.Count == 0 ? 2 : 10, 0, 2)
                    });
                }

                string key = KeyOf(f);

                bool on;

                if (!_state.TryGetValue(key, out on))
                    on = StartsTicked(f);

                // A TextBlock inside, never Content: an underscore in
                // Content is an access key, and level and model names are
                // full of them
                var tick = new CheckBox
                {
                    Content = new TextBlock
                    {
                        Text = f.Label + "     (" + f.Rooms + " room(s) on this level)",
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    IsChecked = on,
                    Tag = f,
                    Margin = new Thickness(14, 3, 2, 3),
                    ToolTip = f.Label
                };

                tick.Checked += (s, e) => { _state[key] = true; Count(); };
                tick.Unchecked += (s, e) => { _state[key] = false; Count(); };

                _list.Children.Add(tick);
            }

            Count();
        }

        private IEnumerable<CheckBox> Ticks()
        {
            return _list.Children.OfType<CheckBox>();
        }

        private void TickShown(bool on)
        {
            foreach (CheckBox tick in Ticks())
                tick.IsChecked = on;
        }

        private void Count()
        {
            if (_shown.Count == 0)
            {
                _found.Text = string.IsNullOrWhiteSpace(_keyword.Text)
                    ? "No linked model has rooms on this model's levels."
                    : "No linked model's name holds \"" + _keyword.Text.Trim() + "\" and has rooms on this model's levels.";

                return;
            }

            int levels = _shown.Select(f => f.Storey).Distinct().Count();
            int models = _shown.Select(f => f.LinkId).Distinct().Count();
            int ticked = Ticks().Count(t => t.IsChecked == true);

            _found.Text = models + " model placement(s) found on " + levels + " level(s); " +
                          ticked + " of " + _shown.Count + " ticked. An unticked one is not searched on that " +
                          "level - its rooms there are not grouped under a redundant room.";
        }

        // What the ticks say - kept as "follow the keyword" whenever they
        // still tick everything found (VENTINTERIOR.Choice)
        private void Keep()
        {
            var ticked = Ticks()
                .Where(t => t.IsChecked == true)
                .Select(t => (VENTINTERIOR.Found)t.Tag)
                .Select(f => new KeyValuePair<string, long>(f.StoreyName, f.LinkId))
                .ToList();

            Dictionary<string, HashSet<long>> choice = VENTINTERIOR.Choice(_shown, ticked);

            VENTSETTINGS.InteriorKeyword = (_keyword.Text ?? string.Empty).Trim();
            VENTSETTINGS.InteriorPicks.Clear();
            VENTSETTINGS.InteriorChosen = choice != null;

            if (choice != null)
            {
                foreach (KeyValuePair<string, HashSet<long>> pick in choice)
                    VENTSETTINGS.InteriorPicks[pick.Key] = pick.Value;
            }

            DialogResult = true;
        }
    }
}
