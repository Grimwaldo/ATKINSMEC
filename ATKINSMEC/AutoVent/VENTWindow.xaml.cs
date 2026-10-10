using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ATKINSMEC
{
    // AUTOVENT's window, in WPF.
    //
    // It replaced a WinForms one at the user's request. The MOVE WAS
    // CHEAP because none of the work was ever in the form: VENTSHEET
    // reads the workbook, REVITROOMS writes the terminals and
    // REVITEXPORT writes the sheet back, and the window only ever
    // orchestrated them. That is worth keeping true.
    //
    // MODELESS, like the WinForms one was, so the model stays usable -
    // which means every Revit call still goes through ExtEventHan. The
    // window raises the external event and Revit runs it when idle.
    public partial class VENTWindow : Window
    {
        private readonly Document _doc;

        // Taken in the constructor, which runs inside the command's API
        // context. Reading doc.Title later would be the trap QAForm's
        // _docTitle exists for.
        private readonly string _docTitle;

        private readonly ObservableCollection<Tick> _links =
            new ObservableCollection<Tick>();

        private readonly ObservableCollection<LevelRow> _levels =
            new ObservableCollection<LevelRow>();

        // Suppresses the worksheet-changed handler while the list is
        // filled, so choosing the default does not read the workbook
        // twice
        private bool _filling;

        public VENTWindow(Document doc, string docTitle)
        {
            InitializeComponent();

            // No bigger than the screen - before it is shown, so it still
            // opens centred (VENTFIT)
            VENTFIT.Screen(this);

            _doc = doc;
            _docTitle = docTitle ?? string.Empty;

            // HOW THIS MODEL WAS LAST SET UP (VENTSAVED). The key needs
            // the document, and the constructor is the one place inside
            // the command's API context. A model never set up gets the
            // DEFAULTS - not what the last model opened left in the
            // statics.
            _modelKey = ModelKey(doc);
            _saved = VENTSAVED.Recall(_modelKey);
            _recalled = _saved != null;

            Restore(_saved);

            ListLinks.ItemsSource = _links;
            ListLevels.ItemsSource = _levels;

            LstFresh.ItemsSource = _fresh;
            LstExtract.ItemsSource = _extract;

            TxtEndcapReach.Text =
                VENTSETTINGS.EndcapReachMm.ToString("0");

            TxtEndcapNames.Text = VENTSETTINGS.EndcapNames;
            TxtBellmouthNames.Text = VENTSETTINGS.BellmouthNames;

            // The duct system types belong to THIS model and do not
            // change with what is ticked anywhere, so this is read once
            RefreshSystemTypes();

            // The links list, read HERE while the command's API context
            // is still open - so the window opens with its links rather
            // than waiting on a round trip through the external event.
            // Shown in Window_Loaded.
            _first = VENTREAD.Do(doc, new VENTREAD.Ask { Links = true });
        }

        // ==================================================
        // What was read from the model (VENTREAD)
        // ==================================================
        //
        // THE WINDOW NEVER READS THE MODEL FROM ITS OWN HANDLERS. Every
        // read goes through VENTREAD - in the constructor, or in the
        // external event - and lands here as plain data. A tick then only
        // rearranges what is held; it asks Revit for nothing but what has
        // not been read yet. Refresh lets go of all of it.

        // The model the window was opened on, for ExtEventHan's reads
        public Document Document
        {
            get { return _doc; }
        }

        private VENTREAD.Answer _first;

        private List<VENTLEVELMAP.Storey> _storeys = new List<VENTLEVELMAP.Storey>();

        // Every room read, per link - all of them, before any level or
        // apartment is applied (RVTLINKSELECTION.RoomsOn does that)
        private readonly Dictionary<long, List<RVTLINKSELECTION.LinkedRoom>> _roomsOf =
            new Dictionary<long, List<RVTLINKSELECTION.LinkedRoom>>();

        // The apartments map, and the Apartments ticks it was built for
        private VENTAPARTMENTS.Map _map;
        private string _mapFor;

        // Each model's fixture families, read when the dialog is first
        // opened; the dialog opens once they arrive
        private Dictionary<long, Dictionary<string, int>> _fixturesOf;
        private bool _openFixtures;

        private bool _reading;

        // ExtEventHan's answer, on Revit's thread - which is this
        // window's thread too
        public void Take(VENTREAD.Answer answer)
        {
            if (answer == null)
                return;

            _reading = false;

            if (answer.Storeys != null && answer.Storeys.Count > 0)
                _storeys = answer.Storeys;

            foreach (KeyValuePair<long, List<RVTLINKSELECTION.LinkedRoom>> rooms in answer.Rooms)
                _roomsOf[rooms.Key] = rooms.Value;

            if (answer.Apartments != null)
            {
                _map = answer.Apartments;
                _mapFor = answer.ApartmentsFor;
            }

            if (answer.Fixtures != null)
                _fixturesOf = answer.Fixtures;

            if (answer.Links != null)
                ShowLinks(answer.Links);     // which refreshes the levels
            else
                RefreshLevels();

            // "Ready." replaces only the reading message - anything said
            // since (a set-up restored, an Apartments tick) stays
            if (answer.Trouble.Count > 0)
                Status("Could not read " + string.Join("; ", answer.Trouble), false);
            else if (!_reading && TxtStatus.Text == ReadingSaid)
                Status("Ready.", false);

            if (_openFixtures && _fixturesOf != null)
            {
                _openFixtures = false;

                // After the event handler has returned, not inside it: a
                // modal dialog there would hold Revit's API context open
                Dispatcher.BeginInvoke(new Action(OpenFixtures));
            }

            if (_openInteriors && InteriorCandidates().All(_roomsOf.ContainsKey))
            {
                _openInteriors = false;

                Dispatcher.BeginInvoke(new Action(OpenInteriors));
            }
        }

        // Asks the external event for what is missing. The levels list is
        // left as it is until the answer comes back and rebuilds it.
        private void Ask(VENTREAD.Ask ask)
        {
            if (Command.extEventHan == null)
                return;

            _reading = true;

            Status(ReadingSaid, true);

            Command.extEventHan.Read(ask);
        }

        private const string ReadingSaid = "Reading the linked models...";

        // What RefreshLevels would need that has not been read: the rooms
        // of a link ticked for the first time, the apartments map when
        // the Apartments ticks have changed, and the rooms of the interior
        // placements that map finds (read in the same trip - VENTREAD.Do)
        private VENTREAD.Ask Missing(List<long> ticked)
        {
            var ask = new VENTREAD.Ask();

            string key = VENTREAD.ApartmentsKey();

            bool wantMap = VENTSETTINGS.ApartmentLinkIds.Count > 0;

            if (wantMap && _mapFor != key)
            {
                ask.Apartments = true;
                ask.Have.UnionWith(_roomsOf.Keys);
            }

            VENTAPARTMENTS.Map map = wantMap && _mapFor == key ? _map : null;

            IEnumerable<long> wanted = map != null ? map.RoomLinks(ticked) : ticked;

            foreach (long linkId in wanted)
            {
                if (!_roomsOf.ContainsKey(linkId))
                    ask.Rooms.Add(linkId);
            }

            return ask;
        }

        // ==================================================
        // This model's set-up, remembered (VENTSAVED)
        // ==================================================

        // Null for a model never saved to disk: it has nowhere stable to
        // be remembered under, so its set-up lasts while the window does
        private readonly string _modelKey;

        // The set-up as it stands - read from the file on opening, then
        // kept up to date by every change (Capture), so a Refresh of the
        // links list keeps what was ticked rather than going back to the
        // file
        private VENTSAVED.Model _saved;

        private readonly bool _recalled;

        // Nothing is saved until the window has finished filling itself:
        // a save from halfway through would write an empty links list
        // over the one that was remembered
        private bool _ready;

        private bool _saveFailed;

        // WHERE THE MODEL LIVES, so every local copy of one central -
        // and every Revit version - finds the same set-up:
        //
        //   - an ACC model: its model GUID (the path names the hub and
        //     project, which can be renamed under it);
        //   - a workshared model: its CENTRAL, not the local file, which
        //     is a different file for every person and every day;
        //   - otherwise the file itself.
        //
        // Document.CreationGUID would be neater but only exists from 2024,
        // and a key that differed between versions would split one
        // project's set-up in two.
        private static string ModelKey(Document doc)
        {
            try
            {
                if (doc.IsModelInCloud)
                {
                    ModelPath cloud = doc.GetCloudModelPath();

                    if (cloud != null)
                        return "cloud:" + cloud.GetModelGUID().ToString("D");
                }

                if (doc.IsWorkshared)
                {
                    ModelPath central = doc.GetWorksharingCentralModelPath();

                    string visible = central == null
                        ? string.Empty
                        : ModelPathUtils.ConvertModelPathToUserVisiblePath(central);

                    if (!string.IsNullOrEmpty(visible))
                        return "central:" + visible.Trim().ToLowerInvariant();
                }

                if (!string.IsNullOrEmpty(doc.PathName))
                    return "file:" + System.IO.Path.GetFullPath(doc.PathName).ToLowerInvariant();
            }
            catch
            {
                // Unreadable is the same as never saved
            }

            return null;
        }

        // What does not depend on the links. The links themselves are
        // settled in LoadLinks, which has the list to resolve them
        // against.
        private static void Restore(VENTSAVED.Model saved)
        {
            VENTSETTINGS.EndcapReachMm = saved == null
                ? VENTSETTINGS.DefaultEndcapReachMm
                : saved.ReachMm;

            VENTSETTINGS.EndcapNames = saved == null || saved.EndcapNames == null
                ? VENTSETTINGS.DefaultEndcapNames
                : saved.EndcapNames;

            VENTSETTINGS.BellmouthNames = saved == null || saved.BellmouthNames == null
                ? VENTSETTINGS.DefaultBellmouthNames
                : saved.BellmouthNames;

            VENTSETTINGS.CountFixtures = saved == null || saved.CountFixtures;

            VENTSETTINGS.FixtureFamilies.Clear();

            if (saved != null)
                VENTSETTINGS.FixtureFamilies.UnionWith(saved.FixtureFamilies);

            // Read from the window at the press; never carried over
            VENTSETTINGS.FreshSystemTypeIds.Clear();
            VENTSETTINGS.ExtractSystemTypeIds.Clear();

            // Element ids of ANOTHER model's links mean nothing here
            VENTSETTINGS.FixtureSources.Clear();
            VENTSETTINGS.ApartmentLinkIds.Clear();

            // The interior models: the keyword now, the picks once the
            // links are known (ShowLinks) - they name placements
            VENTSETTINGS.InteriorKeyword = saved == null || saved.InteriorKeyword == null
                ? VENTSETTINGS.DefaultInteriorKeyword
                : saved.InteriorKeyword;

            VENTSETTINGS.InteriorChosen = saved != null && saved.InteriorPicks != null;
            VENTSETTINGS.InteriorPicks.Clear();

            // The rooms arranged by hand (VENTARRANGE) - by the keys they
            // were saved under; ShowLinks re-points any whose link has been
            // loaded again under a new id
            VENTARRANGE.Current = new VENTARRANGE.Arrangement();

            if (saved != null)
            {
                VENTARRANGE.Current.Ungrouped.UnionWith(saved.Ungrouped);

                foreach (KeyValuePair<string, List<string>> order in saved.Orders)
                    VENTARRANGE.Current.Orders[order.Key] = new List<string>(order.Value);

                foreach (KeyValuePair<string, string> name in saved.Names)
                    VENTARRANGE.Current.Names[name.Key] = name.Value;
            }
        }

        private static VENTSAVED.LinkRef Ref(Tick link)
        {
            return new VENTSAVED.LinkRef { Id = link.Id, Label = link.Label ?? string.Empty };
        }

        // The set-up as the window shows it now
        private VENTSAVED.Model Capture()
        {
            var now = new VENTSAVED.Model
            {
                Key = _modelKey ?? string.Empty,
                Title = _docTitle,
                When = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                Fresh = Chosen(_fresh, VENTSETTINGS.LooksFresh),
                Extract = Chosen(_extract, VENTSETTINGS.LooksExtract),
                ReachMm = ReachInBox(),
                EndcapNames = VENTSETTINGS.EndcapChoice(TxtEndcapNames == null ? null : TxtEndcapNames.Text),
                BellmouthNames = TxtBellmouthNames == null
                    ? VENTSETTINGS.BellmouthChoice(VENTSETTINGS.BellmouthNames)
                    : VENTSETTINGS.BellmouthChoice(TxtBellmouthNames.Text),
                CountFixtures = VENTSETTINGS.CountFixtures,
                FixtureFamilies = VENTSETTINGS.FixtureFamilies
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            now.RoomLinks = _links.Where(l => l.IsOn).Select(Ref).ToList();

            now.Apartments = _links.Where(l => VENTSETTINGS.ApartmentLinkIds.Contains(l.Id)).Select(Ref).ToList();

            foreach (long id in VENTSETTINGS.FixtureSources.OrderBy(i => i))
            {
                Tick link = _links.FirstOrDefault(l => l.Id == id);

                now.FixtureSources.Add(id == 0
                    ? new VENTSAVED.LinkRef { Id = 0, Label = "This model" }
                    : link != null ? Ref(link) : new VENTSAVED.LinkRef { Id = id });
            }

            // The default keyword is remembered as no keyword, so a change
            // of default reaches a model nobody set up
            now.InteriorKeyword = VENTSETTINGS.InteriorKeyword == VENTSETTINGS.DefaultInteriorKeyword
                ? null
                : VENTSETTINGS.InteriorKeyword;

            now.InteriorPicks = VENTSETTINGS.InteriorChosen ? InteriorPicksNow() : null;

            // The arrangement by hand, and every link its keys name with
            // its label - a link not loaded now keeps the label it had
            now.Ungrouped = VENTARRANGE.Current.Ungrouped.OrderBy(k => k, StringComparer.Ordinal).ToList();

            now.Orders = VENTARRANGE.Current.Orders
                .OrderBy(o => o.Key, StringComparer.OrdinalIgnoreCase)
                .Select(o => new KeyValuePair<string, List<string>>(o.Key, o.Value.ToList()))
                .ToList();

            now.Names = VENTARRANGE.Current.Names
                .OrderBy(n => n.Key, StringComparer.Ordinal)
                .ToList();

            foreach (long id in VENTARRANGE.Links(VENTARRANGE.Current).OrderBy(i => i))
            {
                Tick link = _links.FirstOrDefault(l => l.Id == id);

                VENTSAVED.LinkRef before = _saved == null
                    ? null
                    : _saved.ArrangeLinks.FirstOrDefault(l => l.Id == id);

                now.ArrangeLinks.Add(link != null ? Ref(link) : before ?? new VENTSAVED.LinkRef { Id = id });
            }

            // A link not loaded right now keeps what was chosen for it
            if (_saved != null)
            {
                List<VENTSAVED.LinkRef> loaded = _links.Select(Ref).ToList();

                VENTSAVED.KeepUnloaded(now.RoomLinks, _saved.RoomLinks, loaded);
                VENTSAVED.KeepUnloaded(now.Apartments, _saved.Apartments, loaded);
                VENTSAVED.KeepUnloaded(now.FixtureSources, _saved.FixtureSources, loaded);
            }

            return now;
        }

        // The interior picks as they stand, each placement named by id AND
        // label (VENTSAVED.Resolve) - plus any remembered for a placement
        // not loaded right now, as KeepUnloaded does for the other lists
        private List<VENTSAVED.InteriorPick> InteriorPicksNow()
        {
            var picks = new List<VENTSAVED.InteriorPick>();

            foreach (KeyValuePair<string, HashSet<long>> storey in VENTSETTINGS.InteriorPicks
                         .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                foreach (long id in (storey.Value ?? new HashSet<long>()).OrderBy(i => i))
                {
                    Tick link = _links.FirstOrDefault(l => l.Id == id);

                    picks.Add(new VENTSAVED.InteriorPick
                    {
                        Storey = storey.Key,
                        Link = link != null ? Ref(link) : new VENTSAVED.LinkRef { Id = id }
                    });
                }
            }

            if (_saved != null && _saved.InteriorPicks != null)
            {
                List<VENTSAVED.LinkRef> loaded = _links.Select(Ref).ToList();

                foreach (VENTSAVED.InteriorPick before in _saved.InteriorPicks)
                {
                    if (before == null || before.Link == null || before.Link.Id == 0)
                        continue;

                    bool here = VENTSAVED.Resolve(new[] { before.Link }, loaded).Count > 0;

                    if (!here && !picks.Any(p => p.Link.Id == before.Link.Id &&
                                                 string.Equals(p.Storey, before.Storey, StringComparison.OrdinalIgnoreCase)))
                        picks.Add(before);
                }
            }

            return picks;
        }

        // The ticks as names, for VENTSAVED.ChoiceOf
        private static List<string> Chosen(IEnumerable<Tick> ticks, Func<string, bool> names)
        {
            return VENTSAVED.ChoiceOf(
                ticks.Select(t => new KeyValuePair<string, bool>(t.Name, t.IsOn)), names);
        }

        // What is typed in the reach box, or what is in force
        private double ReachInBox()
        {
            double mm;

            return TxtEndcapReach != null && double.TryParse(TxtEndcapReach.Text, out mm) && mm > 0
                ? mm
                : VENTSETTINGS.EndcapReachMm;
        }

        // Called on every change. A file that cannot be written says so
        // once and is not a reason to stop anything.
        private void SaveSettings()
        {
            if (!_ready)
                return;

            _saved = Capture();

            if (string.IsNullOrEmpty(_modelKey))
                return;

            if (!VENTSAVED.Remember(_saved) && !_saveFailed)
            {
                _saveFailed = true;

                Status("This model's set-up could not be saved to " + VENTSAVED.DefaultPath +
                       " - it applies until AUTOVENT is closed.", false);
            }
        }

        // One ticked row. WPF binds to it, so a tick changes the model
        // rather than the control - which is the whole reason the level
        // list can be rebuilt without losing what was ticked.
        public class Tick : INotifyPropertyChanged
        {
            private bool _on;

            public bool IsOn
            {
                get { return _on; }
                set
                {
                    if (_on == value)
                        return;

                    _on = value;

                    Changed("IsOn");
                    Changed("Tip");

                    if (Toggled != null)
                        Toggled();
                }
            }

            public string Label { get; set; }

            // Which of this model's levels the placement covers
            // (RVTLINKSELECTION.LevelSpan) - read once, when the list is
            // filled
            public string Span { get; set; }

            // What the apartment ticks found for this placement: "in the
            // apartments of ARC-000020.rvt : 1 on L04 to L10", or empty
            private string _paired = string.Empty;

            public string Paired
            {
                get { return _paired; }
                set
                {
                    value = value ?? string.Empty;

                    if (_paired == value)
                        return;

                    _paired = value;

                    Changed("Detail");
                    Changed("Tip");
                }
            }

            // A second, quieter line. An interior placement found inside
            // apartments says WHERE - that is the thing to check, floor by
            // floor, against this model's levels; any other says its span.
            public string Detail
            {
                get { return _paired.Length > 0 ? _paired : Span; }
            }

            // Everything in full, for the row's tooltip - either line can
            // be too long for the list
            public string Tip
            {
                get
                {
                    string tip = Label;

                    if (!string.IsNullOrEmpty(Span))
                        tip += Environment.NewLine + Span;

                    if (_paired.Length > 0)
                    {
                        tip += Environment.NewLine + Environment.NewLine +
                               "Rooms found " + _paired + "." + Environment.NewLine +
                               (IsOn
                                   ? "Ticked, so all its rooms are read; those inside an apartment go under it."
                                   : "Not ticked, so only those rooms are read - each under its apartment.");
                    }

                    return tip;
                }
            }

            // The links list's second tick: this placement's rooms are
            // APARTMENTS (VENTAPARTMENTS). Any number of placements.
            private bool _apartments;

            public bool IsApartments
            {
                get { return _apartments; }
                set
                {
                    if (_apartments == value)
                        return;

                    _apartments = value;

                    Changed("IsApartments");

                    if (ApartmentsToggled != null)
                        ApartmentsToggled(this);
                }
            }

            public Action<Tick> ApartmentsToggled;

            public long Id { get; set; }

            public string Name { get; set; }

            public Action Toggled;

            public event PropertyChangedEventHandler PropertyChanged;

            private void Changed(string what)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(what));
            }
        }

        // The level and room rows the tree binds to are in
        // VENTLEVELROWS.cs - Revit-free, so their tick logic is tested on
        // its own and the preview renders them for real.

        // What a PERSON chose for each room, by RoomKey. A rebuild (ticking
        // another link, opening a workbook) keeps these; a room nobody has
        // touched follows its level's pre-tick as before.
        private readonly Dictionary<string, bool> _roomChoice =
            new Dictionary<string, bool>();

        // ==================================================
        // Opening
        // ==================================================

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtDocument.Text = _docTitle;

            // The links read in the constructor
            Take(_first);
            _first = null;

            ChkFixtures.IsChecked = VENTSETTINGS.CountFixtures;
            ShowFixtures();

            // Saving starts only now that the window holds the whole
            // set-up - see _ready
            _ready = true;

            if (_recalled)
            {
                Status("Set-up restored" +
                       (string.IsNullOrEmpty(_saved.When) ? "." : " (" + _saved.When + ")."), false);
            }

            // The body to the window, once its rows have their real heights
            Dispatcher.BeginInvoke(new Action(() => VENTFIT.Body(BodyScroll, BodyGrid)),
                                   System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void BodyScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            VENTFIT.Body(BodyScroll, BodyGrid);
        }

        // ==================================================
        // Fixtures into "No. of Units"
        // ==================================================

        private void ShowFixtures()
        {
            TxtFixtures.Text = VENTEXPORTNOTE.Fixtures(
                VENTSETTINGS.CountFixtures,
                VENTSETTINGS.FixtureFamilies.Count,
                VENTSETTINGS.FixtureSources.Count);

            ChkFixtures.ToolTip = VENTEXPORTNOTE.FixturesTip(
                VENTSETTINGS.CountFixtures,
                VENTSETTINGS.FixtureFamilies.Count,
                VENTSETTINGS.FixtureSources.Count);
        }

        private void ChkFixtures_Click(object sender, RoutedEventArgs e)
        {
            VENTSETTINGS.CountFixtures = ChkFixtures.IsChecked == true;
            ShowFixtures();
            SaveSettings();
        }

        // The Fixtures... dialog: which models, which families
        private void BtnFixtures_Click(object sender, RoutedEventArgs e)
        {
            // Each model's families are read once, through the external
            // event, and the dialog opens when they arrive (Take). It
            // cannot ask for them itself: an external event does not run
            // while a modal dialog is up.
            if (_fixturesOf == null)
            {
                _openFixtures = true;

                Ask(new VENTREAD.Ask { Fixtures = true });

                return;
            }

            OpenFixtures();
        }

        private void OpenFixtures()
        {
            var sources = new List<VENTFIXTUREDIALOG.Source>
            {
                new VENTFIXTUREDIALOG.Source { Id = 0, Label = "This model" }
            };

            foreach (Tick link in _links)
                sources.Add(new VENTFIXTUREDIALOG.Source { Id = link.Id, Label = link.Label });

            var dialog = new VENTFIXTUREDIALOG(
                sources,
                TickedLinks(),
                ids => VENTSETTINGS.FamiliesFrom(_fixturesOf, ids))
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                Status("Fixtures will be counted as the line under the hint says, at the next export.", false);
            }

            ShowFixtures();
            SaveSettings();
        }

        // The links list, from links already read (VENTREAD.LinkRow - each
        // placement's label and its span of THIS model's levels)
        private void ShowLinks(List<VENTREAD.LinkRow> links)
        {
            // A Refresh keeps what is ticked now: take it before the
            // list is emptied, then settle it against the new list below
            if (_ready)
                _saved = Capture();

            _links.Clear();

            // WHAT WAS CHOSEN FOR THESE LINKS, settled against the links
            // loaded now - by id, then by a label naming just one of them
            // (VENTSAVED.Resolve). A link that has gone is simply not
            // ticked; it is still remembered (Capture keeps it).
            links = links ?? new List<VENTREAD.LinkRow>();

            var loaded = links
                .Select(l => new VENTSAVED.LinkRef { Id = l.Id, Label = l.Label })
                .ToList();

            var roomLinks = new HashSet<long>();

            VENTSETTINGS.ApartmentLinkIds.Clear();
            VENTSETTINGS.FixtureSources.Clear();

            if (_saved != null)
            {
                roomLinks = VENTSAVED.Resolve(_saved.RoomLinks, loaded);

                VENTSETTINGS.ApartmentLinkIds.UnionWith(VENTSAVED.Resolve(_saved.Apartments, loaded));

                VENTSETTINGS.FixtureSources.UnionWith(VENTSAVED.Resolve(
                    _saved.FixtureSources,
                    loaded.Concat(new[] { new VENTSAVED.LinkRef { Id = 0, Label = "This model" } })));
            }

            // The interior picks, storey by storey, settled the same way. A
            // storey whose placements have all gone keeps an empty set: it
            // was a choice, and "none" must not turn back into "all found"
            VENTSETTINGS.InteriorPicks.Clear();

            if (VENTSETTINGS.InteriorChosen && _saved != null && _saved.InteriorPicks != null)
            {
                foreach (IGrouping<string, VENTSAVED.InteriorPick> storey in _saved.InteriorPicks
                             .Where(p => p != null && p.Link != null)
                             .GroupBy(p => p.Storey ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    VENTSETTINGS.InteriorPicks[storey.Key] = VENTSAVED.Resolve(storey.Select(p => p.Link), loaded);
                }
            }

            // The rooms arranged by hand name their link by id: one loaded
            // again under a new id is found by its label, as every other
            // remembered link is, and its rooms' keys are re-pointed
            if (_saved != null && _saved.ArrangeLinks.Count > 0)
            {
                var moved = new Dictionary<long, long>();

                foreach (VENTSAVED.LinkRef link in _saved.ArrangeLinks)
                {
                    HashSet<long> now = VENTSAVED.Resolve(new[] { link }, loaded);

                    if (now.Count == 1 && !now.Contains(link.Id))
                        moved[link.Id] = now.First();
                }

                VENTARRANGE.Remap(VENTARRANGE.Current, moved);
            }

            foreach (VENTREAD.LinkRow link in links)
            {
                // Set before the callbacks are attached, so filling the
                // list is not taken for somebody ticking
                var tick = new Tick
                {
                    Id = link.Id,
                    Label = link.Label,
                    Span = link.Span,
                    IsOn = roomLinks.Contains(link.Id),
                    IsApartments = VENTSETTINGS.ApartmentLinkIds.Contains(link.Id)
                };

                tick.Toggled = () =>
                {
                    RefreshLevels();
                    SaveSettings();
                };

                tick.ApartmentsToggled = ApartmentsToggled;

                _links.Add(tick);
            }

            TxtLinksInfo.Text = links.Count == 0
                ? "No loaded Revit links found in this project."
                : links.Count + " loaded link(s). Tick the architectural " +
                  "model(s) to read rooms from; Apartments on one whose rooms are apartments.";

            _linksSaid = TxtLinksInfo.Text;

            // A Refresh keeps the search: the same words narrow the new list
            ApplyLinkSearch();

            RefreshLevels();
        }

        // ==================================================
        // Searching the links list (VENTLINKFILTER)
        // ==================================================

        // What the card says with no search on, put back when it is cleared
        private string _linksSaid;

        private void TxtLinkSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Raised while the XAML is still being parsed - the controls
            // declared after the box are not there yet (the radio trap)
            if (TxtLinkSearchHint == null || ListLinks == null || TxtLinksInfo == null)
                return;

            // Visibility in full: Autodesk.Revit.DB has one too
            TxtLinkSearchHint.Visibility = string.IsNullOrEmpty(TxtLinkSearch.Text)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            ApplyLinkSearch();
        }

        private void TxtLinkSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && TxtLinkSearch.Text.Length > 0)
            {
                TxtLinkSearch.Clear();
                e.Handled = true;
            }
        }

        // ONLY WHAT IS SHOWN. The filter sits on the list's view, not on the
        // links: a ticked link the search hides is still in _links, still
        // ticked, and still read by every run - and the card says so.
        private void ApplyLinkSearch()
        {
            string query = TxtLinkSearch == null ? string.Empty : TxtLinkSearch.Text;

            ICollectionView view = System.Windows.Data.CollectionViewSource.GetDefaultView(_links);

            if (string.IsNullOrWhiteSpace(query))
            {
                view.Filter = null;

                if (_linksSaid != null)
                    TxtLinksInfo.Text = _linksSaid;

                return;
            }

            view.Filter = item =>
            {
                var link = item as Tick;
                return link != null && VENTLINKFILTER.Matches(link.Label, query);
            };

            int shown = _links.Count(l => VENTLINKFILTER.Matches(l.Label, query));
            int hiddenTicked = _links.Count(l => l.IsOn && !VENTLINKFILTER.Matches(l.Label, query));

            TxtLinksInfo.Text = VENTLINKFILTER.Said(shown, _links.Count, hiddenTicked);
        }

        // ==================================================
        // Which links hold apartments
        // ==================================================

        // SEVERAL ARCHITECTS' MODELS MAY HOLD APARTMENTS, and the interior
        // models are FOUND - the placements the keyword names or that were
        // ticked level by level (Interior models...), on the same level of
        // this model (VENTAPARTMENTS). Ticking Apartments ticks the row as well:
        // the architect's model has the corridors and lobbies too, and an
        // apartments model whose own rooms were never read would leave the
        // run gated on a list that shows rooms. Unticking the row after is
        // still allowed.
        private void ApartmentsToggled(Tick link)
        {
            if (link.IsApartments)
                VENTSETTINGS.ApartmentLinkIds.Add(link.Id);
            else
                VENTSETTINGS.ApartmentLinkIds.Remove(link.Id);

            if (link.IsApartments && !link.IsOn)
                link.IsOn = true;        // refreshes the levels itself
            else
                RefreshLevels();

            Status(VENTSETTINGS.ApartmentLinkIds.Count == 0
                       ? "No apartments: every room is a row of its own."
                       : "Rooms of interior models standing inside the apartments, on the same level of " +
                         "this model, are listed and exported under their apartment.",
                   false);

            SaveSettings();
        }

        // ==================================================
        // Arranging the rooms for the sheet (VENTARRANGE)
        // ==================================================

        // A room's tooltip line when it was taken out of its apartment
        private static string TakenOutTip(VENTAPARTMENTS.Map map, string key)
        {
            VENTAPARTMENTS.Apartment home = map == null ? null : map.Of(key);

            return home == null || !VENTARRANGE.Current.Ungrouped.Contains(key)
                ? string.Empty
                : Environment.NewLine + "Stands in apartment " + (home.Number + " " + home.Name).Trim() +
                  ", but taken out of it in Arrange rooms... - a row of its own in the sheet.";
        }

        // Every level's rows as the dialog shows them, worked out with the
        // arrangement it is trying - the SAME RoomsOn the levels list uses
        // (arithmetic over what has been read), so the dialog, the list and
        // the sheet cannot disagree. All levels in one go: the dialog's
        // search counts the matches on the others.
        private Dictionary<string, List<VENTARRANGE.Row>> ArrangeRows(VENTARRANGE.Arrangement arrangement, ISet<string> unticked)
        {
            VENTAPARTMENTS.Map map = VENTSETTINGS.ApartmentLinkIds.Count > 0 ? _map : null;

            Dictionary<string, List<RVTLINKSELECTION.LinkedRoom>> on =
                RVTLINKSELECTION.RoomsOn(TickedLinks(), _roomsOf, map, _storeys, arrangement);

            bool manyLinks = on.Values.SelectMany(r => r).Select(r => r.LinkId).Distinct().Count() > 1;

            var all = new Dictionary<string, List<VENTARRANGE.Row>>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<RVTLINKSELECTION.LinkedRoom>> level in on)
            {
                all[level.Key] = level.Value.Select(r =>
                {
                    VENTAPARTMENTS.Apartment home = map == null ? null : map.Of(r.Key);

                    return new VENTARRANGE.Row
                    {
                        Key = r.Key,
                        Name = r.Name ?? string.Empty,
                        ModelName = r.ModelName ?? string.Empty,
                        Number = r.Number ?? string.Empty,
                        LinkName = manyLinks ? r.LinkName ?? string.Empty : string.Empty,
                        ApartmentKey = r.ApartmentKey,
                        Apartment = r.Apartment,
                        ModelApartment = r.ModelApartment,
                        ApartmentNumber = r.ApartmentNumber,
                        HomeKey = home == null ? null : home.Key,
                        Home = home == null ? null : arrangement.NameOf(home.Key, home.Name),
                        HomeNumber = home == null ? null : home.Number,
                        Exported = !unticked.Contains(r.Key)
                    };
                }).ToList();
            }

            return all;
        }

        private void BtnArrange_Click(object sender, RoutedEventArgs e)
        {
            if (_reading)
            {
                Status("Still reading the linked models - Arrange rooms... once they are in.", false);
                return;
            }

            // The levels that have rooms, in this model's order - the
            // levels list, as it stands
            List<string> levels = _levels.Where(l => l.Rooms.Count > 0).Select(l => l.Name).ToList();

            if (levels.Count == 0)
            {
                Status("No level has rooms to arrange - tick a linked model first.", false);
                return;
            }

            // Open on the level of the first row picked in the list, if any
            LevelRow pickedLevel = _levels.FirstOrDefault(l => l.IsPicked || l.Rooms.Any(r => r.IsPicked));

            var unticked = new HashSet<string>(
                _levels.SelectMany(l => l.Rooms).Where(r => !r.IsOn).Select(r => r.Key),
                StringComparer.Ordinal);

            var dialog = new VENTARRANGEDIALOG(
                levels,
                pickedLevel == null ? null : pickedLevel.Name,
                arrangement => ArrangeRows(arrangement, unticked),
                VENTARRANGE.Current)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true || dialog.Result == null)
                return;

            VENTARRANGE.Current = dialog.Result;

            SaveSettings();
            RefreshLevels();

            Status("Arrangement saved.", false);
        }

        // ==================================================
        // Which placements are interior models (VENTINTERIOR)
        // ==================================================

        // Set by the button when rooms had to be read first; the dialog
        // opens when they arrive (Take), as the Fixtures one does
        private bool _openInteriors;

        // Every loaded placement but the apartment links: what the dialog
        // can list under a keyword - any keyword, as it is typed
        private List<long> InteriorCandidates()
        {
            return _links
                .Where(l => !VENTSETTINGS.ApartmentLinkIds.Contains(l.Id))
                .Select(l => l.Id)
                .ToList();
        }

        private void BtnInteriors_Click(object sender, RoutedEventArgs e)
        {
            if (_links.Count == 0)
            {
                Status("No loaded Revit links found in this project - there is nothing to pick from.", false);
                return;
            }

            // Each placement's rooms are what say which levels it is on. The
            // dialog cannot read them itself: an external event does not run
            // while a modal dialog is up.
            var ask = new VENTREAD.Ask();

            foreach (long linkId in InteriorCandidates())
            {
                if (!_roomsOf.ContainsKey(linkId))
                    ask.Rooms.Add(linkId);
            }

            if (ask.Any)
            {
                _openInteriors = true;

                Ask(ask);

                return;
            }

            OpenInteriors();
        }

        private void OpenInteriors()
        {
            List<long> candidates = InteriorCandidates();

            var links = _links
                .Where(l => candidates.Contains(l.Id))
                .Select(l => new KeyValuePair<long, string>(l.Id, l.Label))
                .ToList();

            // Each room's floor in this model, as the apartments map places
            // it (RVTLINKSELECTION.HostElevationOf) - so a placement is
            // offered on exactly the levels the run will search it on
            var floors = new Dictionary<long, List<double>>();

            foreach (long linkId in candidates)
            {
                List<RVTLINKSELECTION.LinkedRoom> rooms;

                floors[linkId] = _roomsOf.TryGetValue(linkId, out rooms) && rooms != null
                    ? rooms.Select(r => r.Floor).ToList()
                    : new List<double>();
            }

            string before = VENTREAD.ApartmentsKey();

            var dialog = new VENTINTERIORDIALOG(links, floors, _storeys, VENTSETTINGS.ApartmentLinkIds.ToList())
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            SaveSettings();

            if (VENTREAD.ApartmentsKey() == before)
                return;

            Status(VENTSETTINGS.ApartmentLinkIds.Count == 0
                       ? "Interior models set. They are searched once a link is ticked Apartments."
                       : VENTSETTINGS.InteriorChosen
                           ? "Interior models set level by level - the apartments are found again."
                           : "Interior models follow the keyword - the apartments are found again.",
                   false);

            // A new keyword or a changed tick builds the apartments map
            // again (Missing compares ApartmentsKey)
            RefreshLevels();
        }

        // What the apartment ticks found: each placement's own line says
        // where it was found, the line beside Refresh sums it up, and its
        // tooltip has every pairing in full
        private void ShowApartments(VENTAPARTMENTS.Map map)
        {
            foreach (Tick link in _links)
            {
                var said = new List<string>();

                Dictionary<long, SortedSet<int>> pairs;

                if (map != null && map.Pairs.TryGetValue(link.Id, out pairs))
                {
                    // The FLOORS FIRST: the row cuts the line off, and
                    // the floors are what is being checked - the
                    // apartments model's name is long and in the tooltip
                    foreach (KeyValuePair<long, SortedSet<int>> pair in pairs)
                    {
                        said.Add("in apartments on " + VENTLEVELMAP.Floors(pair.Value, map.Storeys) +
                                 "  -  " + map.LabelOf(pair.Key));
                    }
                }

                link.Paired = string.Join("; ", said);
            }

            TxtApartments.Text = VENTLEVELNOTE.Apartments(
                map == null ? 0 : map.Sources.Count,
                map == null ? 0 : map.Pairs.Count,
                map == null ? 0 : map.Holding.Count,
                map == null ? 0 : map.ByRoom.Count,
                map == null ? 0 : map.OtherStorey,
                map == null ? 0 : map.Outside);

            TxtApartments.ToolTip = map == null || !map.On
                ? "Tick Apartments on an architect's model whose rooms are apartments. The interior " +
                  "models' rooms inside them are found on the same level of this model - the interior " +
                  "models are named by a keyword, or ticked level by level (Interior models...)."
                : VENTLEVELNOTE.ApartmentsTip(
                    map.Label, map.Describe(), map.OtherStorey, map.Outside,
                    string.Join(", ", map.OutsideOn.Select(o =>
                        (o.Key >= 0 && o.Key < map.Storeys.Count ? map.Storeys[o.Key].Name : "?") + " " + o.Value)),
                    map.DescribeSpread(),
                    VENTAPTLOG.Where);
        }

        private List<long> TickedLinks()
        {
            var ids = new List<long>();

            foreach (Tick tick in _links)
            {
                if (tick.IsOn)
                    ids.Add(tick.Id);
            }

            return ids;
        }

        // The links are still what the ROOMS come from, so nothing
        // downstream works without one - the levels no longer depend on
        // them, but reading a sheet before there is anything to match it
        // against only moves the confusion later
        private void Gate()
        {
            bool hasLinks = TickedLinks().Count > 0;

            BtnImport.IsEnabled = hasLinks;

            CboWorksheet.IsEnabled = hasLinks && CboWorksheet.Items.Count > 0;

            // WHAT THE EXPORT NEEDS DEPENDS ON WHICH WAY IT IS DOING.
            //
            // Filling an imported worksheet needs a workbook AND a
            // worksheet in it. A NEW workbook needs neither - that is
            // the whole point of it, and gating it on an import would
            // put the feature behind the very step it removes.
            bool imported = !string.IsNullOrEmpty(VENTSHEET.sheetpath);

            // WITH NOTHING IMPORTED, THE DEFAULT IS THE ONE THAT CANNOT
            // RUN. Leaving it selected but greyed reads as the export
            // being broken, so the window opens on the way that works -
            // which is also the answer for somebody starting a job.
            //
            // Setting IsChecked raises Checked, which comes straight
            // back here, so the flag stops it deciding twice.
            if (!imported && !_gating &&
                RdoIntoImported != null && RdoNewWorkbook != null &&
                RdoIntoImported.IsChecked == true)
            {
                _gating = true;

                try
                {
                    RdoNewWorkbook.IsChecked = true;
                }
                finally
                {
                    _gating = false;
                }
            }

            bool canExport = hasLinks &&
                (Chosen() == REVITEXPORT.Way.NewWorkbook
                     ? true
                     : imported &&
                       (Chosen() == REVITEXPORT.Way.AddWorksheet ||
                        CboExportSheet.Items.Count > 0));

            BtnExport.IsEnabled = canExport;

            CboExportSheet.IsEnabled =
                Chosen() == REVITEXPORT.Way.IntoImported &&
                imported && CboExportSheet.Items.Count > 0;

            TxtNewSheet.IsEnabled =
                Chosen() != REVITEXPORT.Way.IntoImported;

            // The two that build a sheet are useless without a workbook
            // to add to, so say so rather than just grey the button
            RdoAddWorksheet.IsEnabled = imported;
            RdoIntoImported.IsEnabled = imported;

            TxtExportHint.Text = Explain();

            if (string.IsNullOrEmpty(VENTSHEET.sheetpath))
            {
                TxtSheetPath.Text = hasLinks
                    ? "No file chosen"
                    : "Tick the linked model(s) first";
            }
        }

        // ==================================================
        // Which system types are fresh, and which are extract
        // ==================================================

        private readonly ObservableCollection<Tick> _fresh =
            new ObservableCollection<Tick>();

        private readonly ObservableCollection<Tick> _extract =
            new ObservableCollection<Tick>();

        private readonly Dictionary<string, long> _systemTypeIds =
            new Dictionary<string, long>();

        // PRE-TICKED BY THE SAME RULE THE FALLBACK USES, so out of the
        // box the lists say exactly what AUTOVENT would have decided on
        // its own and nothing changes until somebody changes it.
        private void RefreshSystemTypes()
        {
            _fresh.Clear();
            _extract.Clear();
            _systemTypeIds.Clear();

            List<VENTSETTINGS.SystemTypeItem> types =
                REVITROOMS.AirSystemTypes(_doc);

            // WHAT THIS MODEL WAS LAST RUN WITH, where a choice was made
            // (VENTSAVED: null means it followed the names). Ticked by
            // name; a remembered type the model no longer has is passed
            // over.
            var freshChosen = _saved != null && _saved.Fresh != null
                ? new HashSet<string>(_saved.Fresh, StringComparer.OrdinalIgnoreCase)
                : null;

            var extractChosen = _saved != null && _saved.Extract != null
                ? new HashSet<string>(_saved.Extract, StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (VENTSETTINGS.SystemTypeItem type in types)
            {
                string name = type.Name ?? string.Empty;

                _systemTypeIds[name] = type.Id;

                var fresh = new Tick
                {
                    Name = name,
                    Label = name,
                    IsOn = freshChosen != null
                        ? freshChosen.Contains(name)
                        : VENTSETTINGS.LooksFresh(name)
                };

                var extract = new Tick
                {
                    Name = name,
                    Label = name,
                    IsOn = extractChosen != null
                        ? extractChosen.Contains(name)
                        : VENTSETTINGS.LooksExtract(name)
                };

                // After the first tick is set, so filling is not saving
                fresh.Toggled = SystemTypeToggled;
                extract.Toggled = SystemTypeToggled;

                _fresh.Add(fresh);
                _extract.Add(extract);
            }

            CountSystemTypes();
        }

        private void SystemTypeToggled()
        {
            CountSystemTypes();
            SaveSettings();
        }

        // The headings count what is TICKED - which is the name guess
        // until somebody changes it, and what they chose after
        private void CountSystemTypes()
        {
            int types = _fresh.Count;

            // Short enough to fit the heading, because the render
            // showed both of these truncated with a longer wording
            TxtFreshTypes.Text = types == 0
                ? "Fresh air  -  no system types"
                : "Fresh air  -  " + _fresh.Count(t => t.IsOn) + " of " + types;

            TxtExtractTypes.Text = types == 0
                ? "Extract / exhaust  -  none"
                : "Extract / exhaust  -  " + _extract.Count(t => t.IsOn) +
                  " of " + types;
        }

        private void ReadSystemTypes()
        {
            VENTSETTINGS.FreshSystemTypeIds.Clear();
            VENTSETTINGS.ExtractSystemTypeIds.Clear();

            Ticked(_fresh, VENTSETTINGS.FreshSystemTypeIds);
            Ticked(_extract, VENTSETTINGS.ExtractSystemTypeIds);

            // WHAT WAS TYPED, OR THE DEFAULT - never a zero. An empty or
            // nonsense box would otherwise mean "no endcap may be more
            // than 0 mm from its FCU", which silently writes nothing on
            // every endcap in the job.
            double mm;

            if (!double.TryParse(TxtEndcapReach.Text, out mm) || mm <= 0)
            {
                mm = VENTSETTINGS.DefaultEndcapReachMm;

                TxtEndcapReach.Text = mm.ToString("0");
            }

            VENTSETTINGS.EndcapReachMm = mm;

            // Likewise an empty names box is the default, never "no
            // endcaps at all"
            VENTSETTINGS.EndcapNames =
                VENTSETTINGS.EndcapChoice(TxtEndcapNames.Text) ?? VENTSETTINGS.DefaultEndcapNames;

            TxtEndcapNames.Text = VENTSETTINGS.EndcapNames;

            // An empty bellmouth box IS a choice: none, every bellmouth
            // serving its room
            VENTSETTINGS.BellmouthNames = (TxtBellmouthNames.Text ?? string.Empty).Trim();
        }

        private void Ticked(
            IEnumerable<Tick> list, HashSet<long> into)
        {
            foreach (Tick tick in list)
            {
                long id;

                if (tick.IsOn &&
                    _systemTypeIds.TryGetValue(tick.Name, out id))
                    into.Add(id);
            }
        }

        // ==================================================
        // Levels
        // ==================================================

        private void RefreshLevels()
        {
            Gate();

            // ANYTHING NOT READ YET is asked for, and the list is left as
            // it is until it arrives - Take calls back in here, and by then
            // it has all been read. Nothing below touches the model.
            VENTREAD.Ask missing = Missing(TickedLinks());

            if (missing.Any)
            {
                Ask(missing);
                return;
            }

            // The levels somebody had opened stay open when the list is
            // rebuilt - ticking a link should not fold the tree away - and
            // what they had picked stays picked
            var open = new HashSet<string>(
                _levels.Where(l => l.IsExpanded).Select(l => l.Name),
                StringComparer.OrdinalIgnoreCase);

            var pickedLevels = new HashSet<string>(
                _levels.Where(l => l.IsPicked).Select(l => l.Name),
                StringComparer.OrdinalIgnoreCase);

            var pickedRooms = new HashSet<string>(
                _levels.SelectMany(l => l.Rooms)
                       .Where(r => r.IsPicked)
                       .Select(r => r.Key));

            _levels.Clear();

            // The rows it pointed at are gone
            _pickAnchor = null;

            ShowButton();

            List<long> linkIds = TickedLinks();

            HashSet<string> sheetLevels = VENTSHEET.GetSheetLevels();

            // THE LEVELS ARE THIS MODEL'S, NOT THE LINKS'.
            //
            // They used to be whatever levels the ticked links happened
            // to have rooms on, which meant the list was named to the
            // architect's convention and changed every time somebody
            // ticked a different link. The rooms still come from the
            // links; each one lands on the storey of THIS model it sits
            // on - see VENTLEVELMAP. Read with the links (VENTREAD).
            List<VENTLEVELMAP.Storey> storeys = _storeys;

            if (storeys.Count == 0)
            {
                ShowLevelNote(string.Empty,
                    VENTLEVELNOTE.Summary(0, 0, 0, 0, 0, 0));

                return;
            }

            // Which linked rooms land on each - the count beside the level
            // and the list under it. A level that will do nothing says so
            // instead of being found out by running it. Arithmetic over
            // the rooms already read (RVTLINKSELECTION.RoomsOn).
            VENTAPARTMENTS.Map apartments =
                VENTSETTINGS.ApartmentLinkIds.Count > 0 ? _map : null;

            // In the order the sheet will have them, rooms taken out of
            // their apartments as rows of their own (Arrange rooms...)
            Dictionary<string, List<RVTLINKSELECTION.LinkedRoom>> roomsOn =
                RVTLINKSELECTION.RoomsOn(linkIds, _roomsOf, apartments, storeys, VENTARRANGE.Current);

            // What the apartment ticks find is worth saying even with no
            // link ticked
            ShowApartments(apartments);

            // The link's name goes on a room only when there is more than
            // one link it could have come from
            // - which, with apartments, includes interior placements nobody
            // ticked, so it is counted from the rooms rather than the ticks
            bool manyLinks = roomsOn.Values
                                    .SelectMany(r => r)
                                    .Select(r => r.LinkId)
                                    .Distinct()
                                    .Count() > 1;

            var known =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (VENTLEVELMAP.Storey storey in storeys)
                known.Add((storey.Name ?? string.Empty).Trim());

            int matched = 0;
            int withRooms = 0;

            for (int at = 0; at < storeys.Count; at++)
            {
                VENTLEVELMAP.Storey storey = storeys[at];

                string name = storey.Name ?? string.Empty;

                bool inSheet = sheetLevels.Contains(name.Trim());

                // Heights are compared in internal coordinates but SHOWN
                // as Revit shows this level, so they can be checked
                // against the level heads on a drawing
                double shift = storey.Elevation - storey.Shown;

                List<RVTLINKSELECTION.LinkedRoom> found;

                bool counted = roomsOn.TryGetValue(name, out found);

                int rooms = counted ? found.Count : 0;

                // Levels the sheet knows about are pre-ticked; with no
                // sheet loaded, the ones that have rooms are - which is
                // what somebody starting a job wants and used to have to
                // tick by hand. A level's rooms start as it does.
                bool preTicked = sheetLevels.Count > 0
                    ? inSheet
                    : rooms > 0;

                var roomRows = new List<RoomRow>();

                if (counted)
                {
                    foreach (RVTLINKSELECTION.LinkedRoom room in found)
                    {
                        roomRows.Add(new RoomRow
                        {
                            LinkId = room.LinkId,
                            RoomId = room.RoomId,
                            Level = name,
                            Number = room.Number,

                            // Inside an apartment: "APARTMENT  /  room",
                            // as the sheet will group it
                            Name = (string.IsNullOrEmpty(room.Apartment)
                                       ? room.Name
                                       : room.Apartment + "  /  " + room.Name) +
                                   (manyLinks ? "     (" + room.LinkName + ")" : string.Empty),
                            Tip = "Room " + (room.Number + " " + room.Name).Trim() +
                                  " in " + room.LinkName +
                                  (string.IsNullOrEmpty(room.LinkLevel)
                                      ? "."
                                      : ", on " + room.LinkLevel + " there.") +
                                  (string.IsNullOrEmpty(room.ApartmentKey)
                                      ? TakenOutTip(apartments, room.Key)
                                      : Environment.NewLine + "Inside apartment " +
                                        (room.ApartmentNumber + " " + room.Apartment).Trim() +
                                        " - written under it in the sheet.") +
                                  Environment.NewLine +
                                  "Its floor is at " + Metres(room.Floor - shift) +
                                  " by this model's levels; " + name + " is at " +
                                  Metres(storey.Shown) + "." +
                                  Environment.NewLine +
                                  "Unticked, it is left out of the export and the airflows." +
                                  Environment.NewLine +
                                  "Double-click to show it in its plan."
                        });
                    }
                }

                // Each room as a person last chose for it, or as the level
                // is pre-ticked; later choices go back into _roomChoice
                LevelRow row = LevelRow.Build(
                    name,
                    inSheet ? "     (in the sheet)" : string.Empty,
                    linkIds.Count > 0,
                    preTicked,
                    roomRows,
                    _roomChoice);

                row.IsExpanded = open.Contains(name);

                VENTLEVELMAP.Storey next = at + 1 < storeys.Count
                    ? storeys[at + 1]
                    : null;

                row.Tip = name + " is at " + Metres(storey.Shown) + "." +
                          Environment.NewLine +
                          "It takes the rooms whose floor is from " +
                          Metres(storey.Shown - VENTLEVELMAP.Allowance) +
                          (next == null
                              ? " upwards."
                              : " up to 300 mm below " + next.Name +
                                " (" + Metres(next.Shown) + ").");

                row.IsPicked = pickedLevels.Contains(name);

                foreach (RoomRow room in row.Rooms)
                    room.IsPicked = pickedRooms.Contains(room.Key);

                _levels.Add(row);

                if (inSheet)
                    matched++;

                if (rooms > 0)
                    withRooms++;
            }

            // Sheet levels with no counterpart in THIS MODEL: these are
            // the ones that would silently process nothing
            var sheetOnly = new List<string>();

            foreach (string name in sheetLevels)
            {
                if (!known.Contains(name))
                    sheetOnly.Add(name);
            }

            sheetOnly.Sort(StringComparer.OrdinalIgnoreCase);

            ShowLevelNote(
                VENTLEVELNOTE.Warning(sheetOnly),
                VENTLEVELNOTE.Summary(
                    storeys.Count, withRooms, linkIds.Count,
                    sheetLevels.Count, matched, sheetOnly.Count));

            // A rebuilt list keeps the search typed over it
            Search.Apply(_levels, LevelQuery, _openBeforeSearch);

            ShowButton();
        }

        // ==================================================
        // Searching the levels list (VENTLEVELROWS.Search)
        // ==================================================

        // How each level stood before the search opened it, by name - put
        // back when the search is cleared, and kept here so it outlives the
        // list being rebuilt in the middle of a search
        private readonly Dictionary<string, bool> _openBeforeSearch =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private string LevelQuery
        {
            get { return TxtLevelSearch == null ? string.Empty : TxtLevelSearch.Text ?? string.Empty; }
        }

        private void TxtLevelSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Raised while the XAML is still being parsed - the controls
            // declared after the box are not there yet (the radio trap)
            if (TxtLevelSearchHint == null || ListLevels == null)
                return;

            // Visibility in full: Autodesk.Revit.DB has one too
            TxtLevelSearchHint.Visibility = string.IsNullOrEmpty(TxtLevelSearch.Text)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            string query = LevelQuery.Trim();

            int found = Search.Apply(_levels, LevelQuery, _openBeforeSearch);

            ShowButton();

            if (query.Length == 0)
            {
                Status("Ready.", false);
                return;
            }

            int levels = _levels.Count(l => l.IsShown);

            Status(found == 0
                       ? "No room matches \"" + query + "\"."
                       : found + " room(s) on " + levels + " level(s) match.",
                   false);
        }

        private void TxtLevelSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && TxtLevelSearch.Text.Length > 0)
            {
                TxtLevelSearch.Clear();
                e.Handled = true;
            }
        }

        // Feet to "+4.500 m"
        private static string Metres(double feet)
        {
            return (feet * 0.3048).ToString(
                       "+0.000;-0.000;0.000",
                       System.Globalization.CultureInfo.InvariantCulture) + " m";
        }

        // The warning has a TextBlock of its own, ABOVE the counts, so it
        // is the first thing in the card rather than the last - see
        // VENTLEVELNOTE for why that matters at 150% scaling
        private void ShowLevelNote(string warning, string summary)
        {
            TxtLevelsWarning.Text = warning;

            TxtLevelsWarning.Visibility = string.IsNullOrEmpty(warning)
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;

            TxtLevelsInfo.Text = summary;
        }

        // EVERYTHING READ IS LET GO and read again: the links, and then
        // each ticked link's rooms and the apartments as the levels list
        // asks for them. The one way to pick up a link reloaded, a room
        // drawn or a level added since the window opened.
        private void BtnRefreshLinks_Click(object sender, RoutedEventArgs e)
        {
            _roomsOf.Clear();
            _map = null;
            _mapFor = null;
            _fixturesOf = null;

            // A dialog asked for before the Refresh is not opened later,
            // out of nowhere, when some other read completes the rooms
            _openInteriors = false;

            Ask(new VENTREAD.Ask { Links = true });
        }

        // A level's click ticks every room under it, and so do these -
        // every room SHOWING, while a search hides some (TickShown); with
        // no search on that is every room, as it always was
        private void BtnAllLevels_Click(object sender, RoutedEventArgs e)
        {
            foreach (LevelRow level in _levels.Where(l => l.IsShown))
                level.TickShown(true);
        }

        private void BtnNoLevels_Click(object sender, RoutedEventArgs e)
        {
            foreach (LevelRow level in _levels.Where(l => l.IsShown))
                level.TickShown(false);
        }

        // ==================================================
        // Picking rows, and showing rooms in their plan
        // ==================================================
        //
        // SEVERAL ROWS CAN BE PICKED, at the user's request, the way a
        // file list picks them: a click picks one, Ctrl+click adds or
        // takes one away, Shift+click picks a range, Ctrl+A picks every
        // row showing. The rules are VENTLEVELROWS.Picks, Revit-free and
        // tested; these handlers only turn clicks and keys into calls.
        //
        // What is picked is what the ACTIONS act on - ticking any picked
        // row ticks them all, and Show in plan frames every picked room.
        // The ticks themselves still decide what a run does.

        // Where the next Shift+click counts from
        private object _pickAnchor;

        // The row an element is in (a room finds its OWN row before its
        // level's), and whether the click was on a button of some kind
        // on the way up: a tick, the arrow that opens a level, or the
        // scroll bar - all of which do their own thing
        private static TreeViewItem RowOf(DependencyObject at, out bool onButton)
        {
            onButton = false;

            while (at != null && !(at is TreeViewItem))
            {
                if (at is ToggleButton || at is ScrollBar)
                    onButton = true;

                at = at is Visual || at is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(at)
                    : LogicalTreeHelper.GetParent(at);
            }

            return at as TreeViewItem;
        }

        // PREVIEW, so the picks are settled before the TreeView selects
        // the row itself. Not handled: the tree still takes focus and
        // moves its own (invisible) selection, which the arrow keys
        // then carry on from.
        private void ListLevels_PreviewMouseLeftButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            // The second click of a double-click: the first one picked
            if (e.ClickCount > 1)
                return;

            bool onButton;

            TreeViewItem item = RowOf(e.OriginalSource as DependencyObject, out onButton);

            if (onButton)
                return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            if (item == null)
            {
                // The empty space under the rows lets go of everything
                if (!ctrl && !shift)
                {
                    Picks.Clear(_levels);
                    _pickAnchor = null;
                    PicksChanged();
                }

                return;
            }

            _pickAnchor = Picks.Click(_levels, _pickAnchor, item.DataContext, ctrl, shift);

            PicksChanged();
        }

        // The TreeView's own selection. After a click it only follows
        // what the handler above has already picked; a KEY moving it
        // (the arrows) is a pick of its own - Shift with it, a range.
        private void ListLevels_SelectedItemChanged(
            object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue == null ||
                !ListLevels.IsKeyboardFocusWithin ||
                Mouse.LeftButton == MouseButtonState.Pressed)
            {
                return;
            }

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            _pickAnchor = Picks.Click(_levels, _pickAnchor, e.NewValue, false, shift);

            PicksChanged();
        }

        private void ListLevels_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                Picks.PickShown(_levels);
                PicksChanged();

                e.Handled = true;
                return;
            }

            // Space ticks the row the keyboard is on - and every picked
            // row with it, if that one is picked. A focused tick box
            // toggles itself, and its Click handler does the rest.
            if (e.Key != Key.Space ||
                Keyboard.Modifiers != ModifierKeys.None ||
                e.OriginalSource is ToggleButton)
            {
                return;
            }

            object row = ListLevels.SelectedItem;

            var level = row as LevelRow;
            var room = row as RoomRow;

            if (level == null && room == null)
                return;

            bool on = level != null
                ? level.ClickMeans()
                : !room.IsOn;

            if (Picks.IsPicked(row))
            {
                Picks.Tick(_levels, on);
            }
            else if (level != null)
            {
                level.TickShown(on);
            }
            else
            {
                room.IsOn = on;
            }

            e.Handled = true;
        }

        // Ticking a PICKED row ticks every picked row the same way; an
        // unpicked row's box ticks that row alone, as it always did.
        //
        // The level's box is bound ONE-WAY, so the row still holds what
        // it was before the click and this decides what the click means:
        // ticked goes to unticked, and unticked OR PART-TICKED goes to
        // ticked. WPF's own toggle sends the third state to unticked -
        // tested, not assumed - which would throw away every room of a
        // level somebody had only partly ticked.
        private void LevelTick_Click(object sender, RoutedEventArgs e)
        {
            var box = sender as FrameworkElement;
            var level = box == null ? null : box.DataContext as LevelRow;

            if (level == null)
                return;

            // While a search hides some of its rooms, the click is about the
            // rooms showing (ClickMeans, TickShown); otherwise exactly as above
            bool on = level.ClickMeans();

            if (level.IsPicked)
                Picks.Tick(_levels, on);
            else
                level.TickShown(on);
        }

        // A room's box has no third state, so WPF's toggle is the right
        // answer and the two-way binding has already set the row
        private void RoomTick_Click(object sender, RoutedEventArgs e)
        {
            var box = sender as FrameworkElement;
            var room = box == null ? null : box.DataContext as RoomRow;

            if (room != null && room.IsPicked)
                Picks.Tick(_levels, room.IsOn);
        }

        private void ShowButton()
        {
            BtnShowRoom.IsEnabled = Picks.Rooms(_levels).Count > 0;
        }

        // The button, and a line saying what the picks will do
        private void PicksChanged()
        {
            ShowButton();

            List<RoomRow> rooms = Picks.Rooms(_levels);

            int rows = Picks.Count(_levels);

            if (rows > 1)
            {
                Status(rows + " rows picked - " + rooms.Count + " room(s). " +
                       "Tick any of them to tick them all, or press Show in plan.",
                       false);
            }
            else if (rooms.Count == 1)
            {
                Status("Double-click the room, or press Show in plan, to " +
                       "see it in a plan of " + rooms[0].Level + ".", false);
            }
            else if (rooms.Count > 1)
            {
                Status("Show in plan frames all " + rooms.Count + " rooms of " +
                       rooms[0].Level + ". Ctrl+click or Shift+click to pick more.",
                       false);
            }
        }

        // Only a double-click ON A ROOM shows it, and only that room. The
        // same gesture on a level opens or closes it, as a tree always
        // does, and on empty space it must not show whatever happened to
        // be picked last.
        private void ListLevels_MouseDoubleClick(
            object sender, MouseButtonEventArgs e)
        {
            bool onButton;

            TreeViewItem item = RowOf(e.OriginalSource as DependencyObject, out onButton);

            if (item == null || onButton)
                return;

            var room = item.DataContext as RoomRow;

            if (room == null)
                return;

            e.Handled = true;

            ShowRooms(new List<RoomRow> { room }, 0);
        }

        // ONE PLAN SHOWS ONE LEVEL. Picks spread over several levels show
        // the level most of them are on (the first in the list, on a
        // tie), and the answer says how many were left out rather than
        // quietly framing some of what was asked for.
        private void BtnShowRoom_Click(object sender, RoutedEventArgs e)
        {
            List<RoomRow> rooms = Picks.Rooms(_levels);

            if (rooms.Count == 0)
                return;

            // GroupBy keeps the tree's order and OrderByDescending is
            // stable, so a tie goes to the level higher in the list
            List<RoomRow> most = rooms
                .GroupBy(r => r.Level, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .First()
                .ToList();

            ShowRooms(most, rooms.Count - most.Count);
        }

        // Changing the view and zooming are API calls, so they go through
        // the external event like everything else that touches Revit -
        // VENTSHOW does the work and answers through Shown
        private void ShowRooms(List<RoomRow> rooms, int elsewhere)
        {
            Command.extEventHan.ShowRooms = rooms
                .Select(r => new[] { r.LinkId, r.RoomId })
                .ToList();

            Command.extEventHan.ShowLevel = rooms[0].Level;
            Command.extEventHan.ShowElsewhere = elsewhere;
            Command.extEventHan.Request = 3;
            Command.ExtEvent.Raise();

            Status(rooms.Count == 1
                       ? "Showing " + (rooms[0].Number + " " + rooms[0].Name).Trim() + "..."
                       : "Showing " + rooms.Count + " rooms...",
                   true);
        }

        // ExtEventHan's answer, on Revit's thread - which is this
        // window's thread, so it can be put straight on screen
        public void Shown(string what)
        {
            Status(what, false);
        }

        private HashSet<string> TickedLevels()
        {
            var names =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Partly ticked counts: some of its rooms are wanted
            foreach (LevelRow level in _levels)
            {
                if (level.Checked != false && !string.IsNullOrEmpty(level.Name))
                    names.Add(level.Name.Trim());
            }

            return names;
        }

        // The rooms a person unticked - what both runs leave out
        private HashSet<string> UntickedRooms()
        {
            var keys = new HashSet<string>();

            foreach (LevelRow level in _levels)
            {
                foreach (RoomRow room in level.Rooms)
                {
                    if (!room.IsOn)
                        keys.Add(room.Key);
                }
            }

            return keys;
        }

        // ==================================================
        // The workbook
        // ==================================================

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (dialog.ShowDialog(this) != true)
                return;

            LoadWorkbook(dialog.FileName);
        }

        private void LoadWorkbook(string path)
        {
            ClearSheet();

            List<string> worksheets;

            try
            {
                worksheets = VENTSHEET.GetWorksheetNames(path);
            }
            catch (Exception ex)
            {
                Told("Could not open the workbook:" +
                     Environment.NewLine + Environment.NewLine + ex.Message,
                     MessageBoxImage.Warning);

                return;
            }

            VENTSHEET.sheetpath = path;

            TxtSheetPath.Text = Path.GetFileName(path);

            if (worksheets.Count == 0)
            {
                Told("The workbook contains no worksheets.",
                     MessageBoxImage.Warning);

                return;
            }

            _filling = true;

            CboWorksheet.Items.Clear();
            CboExportSheet.Items.Clear();

            foreach (string name in worksheets)
            {
                CboWorksheet.Items.Add(name);
                CboExportSheet.Items.Add(name);
            }

            CboWorksheet.IsEnabled = true;
            CboExportSheet.IsEnabled = true;

            // Pre-select the usual sheet when it is there
            int index = CboWorksheet.Items.IndexOf("MLCP");

            _filling = false;

            CboWorksheet.SelectedIndex = index >= 0 ? index : 0;

            // The export defaults to the same sheet, but any other
            // worksheet of the workbook can be picked instead
            CboExportSheet.SelectedIndex = index >= 0 ? index : 0;
        }

        private void CboWorksheet_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (_filling)
                return;

            LoadWorksheet(CboWorksheet.SelectedItem as string);
        }

        private void LoadWorksheet(string worksheetName)
        {
            if (string.IsNullOrEmpty(worksheetName))
                return;

            VENTSHEET.sheetname = worksheetName;

            try
            {
                var vent = new VENTSHEET();

                var data = vent.excelsheet();

                HashSet<string> levels = VENTSHEET.GetSheetLevels();

                TxtSheetInfo.Text =
                    data.Count + " room record(s)." +
                    Environment.NewLine + Environment.NewLine +
                    "Levels in the sheet:" + Environment.NewLine + "    " +
                    string.Join(Environment.NewLine + "    ",
                        levels.OrderBy(l => l)) +
                    Environment.NewLine + Environment.NewLine +
                    "Columns found:" + Environment.NewLine +
                    VENTSHEET.LayoutDescription;

                Status(data.Count + " room record(s) read from " +
                       worksheetName + ".", true);
            }
            catch (Exception ex)
            {
                VENTSHEET.LoadedData = null;

                TxtSheetInfo.Text = string.Empty;

                Told("Could not read worksheet \"" + worksheetName + "\":" +
                     Environment.NewLine + Environment.NewLine + ex.Message +
                     Environment.NewLine + Environment.NewLine +
                     "Pick another worksheet from the list.",
                     MessageBoxImage.Warning);
            }

            RefreshLevels();
        }

        private void ClearSheet()
        {
            _filling = true;

            CboWorksheet.Items.Clear();
            CboWorksheet.IsEnabled = false;
            CboExportSheet.Items.Clear();
            CboExportSheet.IsEnabled = false;

            _filling = false;

            VENTSHEET.LoadedData = null;
            VENTSHEET.sheetname = null;
            VENTSHEET.sheetpath = null;

            TxtSheetPath.Text = "No file chosen";
            TxtSheetInfo.Text = string.Empty;

            RefreshLevels();
        }

        // ==================================================
        // Doing the work - through the external event, always
        // ==================================================

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(VENTSHEET.sheetpath) ||
                string.IsNullOrEmpty(VENTSHEET.sheetname) ||
                VENTSHEET.LoadedData == null)
            {
                Told("Open a ventilation workbook and choose a worksheet " +
                     "first.", MessageBoxImage.Information);

                return;
            }

            List<long> linkIds = TickedLinks();

            if (linkIds.Count == 0)
            {
                Told("Tick at least one linked architectural model.",
                     MessageBoxImage.Information);

                return;
            }

            HashSet<string> levels = TickedLevels();

            if (levels.Count == 0)
            {
                Told("Tick at least one level to process.",
                     MessageBoxImage.Information);

                return;
            }

            RVTLINKSELECTION.SelectedLinkIds = linkIds;
            RVTLINKSELECTION.SelectedLevels = levels;
            RVTLINKSELECTION.ExcludedRooms = UntickedRooms();

            // The ticks and the endcap reach are read HERE, at the
            // press, not as they are changed - so a half-finished edit
            // cannot be picked up by a run somebody started before it
            ReadSystemTypes();

            // ...and remembered for this model, as they were run
            SaveSettings();

            int left = RVTLINKSELECTION.ExcludedRooms.Count;

            Status("Running on " + linkIds.Count + " model(s), " +
                   levels.Count + " level(s)" +
                   (left > 0 ? ", " + left + " room(s) unticked" : string.Empty) +
                   "...", true);

            Command.extEventHan.Request = 1;
            Command.ExtEvent.Raise();
        }

        // ==================================================
        // Which of the three ways
        // ==================================================

        private REVITEXPORT.Way Chosen()
        {
            if (RdoNewWorkbook != null && RdoNewWorkbook.IsChecked == true)
                return REVITEXPORT.Way.NewWorkbook;

            if (RdoAddWorksheet != null && RdoAddWorksheet.IsChecked == true)
                return REVITEXPORT.Way.AddWorksheet;

            return REVITEXPORT.Way.IntoImported;
        }

        // The wording is VENTEXPORTNOTE's, so the preview renders the
        // real text - one paragraph each now, to leave the fixtures line
        // room under it
        private string Explain()
        {
            switch (Chosen())
            {
                case REVITEXPORT.Way.NewWorkbook:
                    return VENTEXPORTNOTE.Hint(VENTEXPORTNOTE.NewWorkbook);

                case REVITEXPORT.Way.AddWorksheet:
                    return VENTEXPORTNOTE.Hint(VENTEXPORTNOTE.AddWorksheet);

                default:
                    return VENTEXPORTNOTE.Hint(VENTEXPORTNOTE.Fill);
            }
        }

        // Stops the "nothing imported, so pick the new workbook" rule
        // in Gate from deciding twice - see there
        private bool _gating;

        private void ExportWay_Changed(object sender, RoutedEventArgs e)
        {
            // A radio with IsChecked="True" in the XAML raises Checked
            // while the window is still being PARSED, so every control
            // declared after it is still null. This is the same shape as
            // the CheckedListBox that killed NWCForm in its constructor:
            // a window that throws while it is being built never
            // appears at all.
            if (TxtNewSheet == null || BtnExport == null ||
                TxtExportHint == null || RdoAddWorksheet == null ||
                RdoNewWorkbook == null || RdoIntoImported == null)
                return;

            Gate();
        }

        // Rewrite the worksheet's level/room rows from the Revit models
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            List<long> linkIds = TickedLinks();

            if (linkIds.Count == 0)
            {
                Told("Tick at least one linked architectural model.",
                     MessageBoxImage.Information);

                return;
            }

            HashSet<string> levels = TickedLevels();

            if (levels.Count == 0)
            {
                Told("Tick the levels to export.",
                     MessageBoxImage.Information);

                return;
            }

            REVITEXPORT.Way way = Chosen();

            string target = CboExportSheet.SelectedItem as string;

            if (way == REVITEXPORT.Way.IntoImported &&
                string.IsNullOrEmpty(target))
            {
                Told("Choose the worksheet to export into.",
                     MessageBoxImage.Information);

                return;
            }

            string sheetName = VENTTEMPLATE.CleanName(
                TxtNewSheet.Text, "Ventilation");

            if (way != REVITEXPORT.Way.IntoImported)
            {
                // Say it once, here, rather than letting a silently
                // cleaned name turn up in the workbook as a surprise
                if (sheetName != (TxtNewSheet.Text ?? string.Empty).Trim())
                {
                    Told("Excel will not take that worksheet name, so it " +
                         "will be called \"" + sheetName + "\" instead.",
                         MessageBoxImage.Information);
                }

                TxtNewSheet.Text = sheetName;
            }

            // A NEW workbook has no imported file to sit beside, so it
            // is suggested next to the model instead
            string suggested = way == REVITEXPORT.Way.NewWorkbook &&
                               string.IsNullOrEmpty(VENTSHEET.sheetpath)
                ? Path.Combine(
                      Environment.GetFolderPath(
                          Environment.SpecialFolder.DesktopDirectory),
                      "Ventilation sheet - from Revit.xlsx")
                : Path.Combine(
                      Path.GetDirectoryName(VENTSHEET.sheetpath),
                      Path.GetFileNameWithoutExtension(VENTSHEET.sheetpath) +
                      " - from Revit.xlsx");

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                FileName = Path.GetFileName(suggested),
                InitialDirectory = Path.GetDirectoryName(suggested),
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
                return;

            REVITEXPORT.TargetPath = dialog.FileName;

            string question;

            switch (way)
            {
                case REVITEXPORT.Way.NewWorkbook:
                    question =
                        "Write a new workbook with worksheet \"" +
                        sheetName + "\" in the office layout, holding " +
                        levels.Count + " level(s) from " + linkIds.Count +
                        " model(s)?" +
                        Environment.NewLine + Environment.NewLine +
                        "Every office formula is kept and every typed " +
                        "value left blank; the three ASHRAE worksheets " +
                        "come with it.";
                    break;

                case REVITEXPORT.Way.AddWorksheet:
                    question =
                        "Add worksheet \"" + sheetName + "\" in the " +
                        "office layout to the imported workbook, holding " +
                        levels.Count + " level(s) from " + linkIds.Count +
                        " model(s)?" +
                        Environment.NewLine + Environment.NewLine +
                        "Everything already in the workbook is left as " +
                        "it is; the ASHRAE worksheets are added only if " +
                        "it has not got them.";
                    break;

                default:
                    question =
                        "Replace the level and room rows of worksheet \"" +
                        target + "\" with " + levels.Count +
                        " level(s) from " + linkIds.Count + " model(s)?" +
                        Environment.NewLine + Environment.NewLine +
                        "Other columns keep their formulas; cells that " +
                        "held a typed-in value are left blank. A Room " +
                        "Number column is added if the worksheet has " +
                        "none.";
                    break;
            }

            // Unticked rooms are said before anybody agrees, not
            // discovered in the sheet afterwards
            HashSet<string> left = UntickedRooms();

            if (left.Count > 0)
            {
                question += Environment.NewLine + Environment.NewLine +
                            left.Count + " room(s) unticked in the levels " +
                            "list will be left out.";
            }

            MessageBoxResult go = MessageBox.Show(
                this,
                question,
                "AUTOVENT",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (go != MessageBoxResult.OK)
                return;

            RVTLINKSELECTION.SelectedLinkIds = linkIds;
            RVTLINKSELECTION.SelectedLevels = levels;
            RVTLINKSELECTION.ExcludedRooms = left;
            REVITEXPORT.TargetWorksheet = target;
            REVITEXPORT.How = way;
            REVITEXPORT.NewWorksheetName = sheetName;

            Status("Exporting...", true);

            Command.extEventHan.Request = 2;
            Command.ExtEvent.Raise();
        }

        // ==================================================

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Told(string what, MessageBoxImage how)
        {
            MessageBox.Show(this, what, "AUTOVENT", MessageBoxButton.OK, how);
        }

        private void Status(string what, bool busy)
        {
            TxtStatus.Text = what;

            // Color is ambiguous here: Autodesk.Revit.DB has one and so
            // does System.Windows.Media. The same clash as Application
            // in NWCForm, and the same answer - say which.
            StatusDot.Background = busy
                ? new SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x15, 0x62, 0x9B))
                : new SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xDC, 0xE3, 0xEA));
        }

        // A modeless window must let go of itself, or the next press of
        // the button finds a dead one in the static
        protected override void OnClosed(EventArgs e)
        {
            // The reach box and anything not yet saved by a change -
            // a closed window is the last chance
            SaveSettings();

            base.OnClosed(e);

            Command.Window = null;
        }

        private void TabActions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
