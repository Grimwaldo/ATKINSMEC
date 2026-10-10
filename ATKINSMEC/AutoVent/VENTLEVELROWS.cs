using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ATKINSMEC
{
    // The rows AUTOVENT's levels tree binds to: a level, and the linked
    // rooms standing on it, each with a tick.
    //
    // In a file of their own and REVIT-FREE for two reasons. The tick
    // logic has real states - all, none, some, a person's choice
    // outliving a rebuild - and scratchpad/levelrowtest exercises it with
    // no model open. And scratchpad/ventpreview compiles this file, so a
    // render of the window shows these rows behaving as they really do,
    // not a stub's copy of them.
    public partial class VENTWindow
    {
        // A LEVEL IN THE TREE, AND ITS TICK FOLLOWS ITS ROOMS. Each room
        // has a tick of its own, at the user's request - a ticked room is
        // exported to the sheet and has its airflow written, an unticked
        // one is left out of both. The level's tick says what its rooms
        // are: ticked when all of them are, empty when none are, and the
        // third state when only some are. Clicking it ticks or unticks
        // every room under it.
        //
        // A level with no rooms has only its own tick, as before.
        public class LevelRow : INotifyPropertyChanged
        {
            private bool? _checked;

            // While the tick is being worked out FROM the rooms, setting
            // it must not push back down onto them
            private bool _settling;

            // While a click on the level is pushing down onto the rooms,
            // each room's change must not settle the level halfway
            private bool _pushing;

            public string Name { get; set; }

            // "(in the sheet)", or nothing
            public string Note { get; set; }

            // False until a link is ticked - there is nothing to count
            public bool Counted { get; set; }

            // Its height and which floors it takes, on hover - so a room
            // listed under the wrong level can be seen to be, and why
            public string Tip { get; set; }

            public ObservableCollection<RoomRow> Rooms { get; } =
                new ObservableCollection<RoomRow>();

            private bool _expanded;

            // Bound two-way to the tree's IsExpanded. Set before a row is
            // added (to keep a rebuilt list open where it was) AND by the
            // search (Search.Apply), which opens a level to show what it
            // found - so the tree has to be told.
            public bool IsExpanded
            {
                get { return _expanded; }
                set
                {
                    if (_expanded == value)
                        return;

                    _expanded = value;

                    Changed("IsExpanded");
                }
            }

            private bool _shown = true;

            // False while a search (Search.Apply) hides it - it has no room
            // the search finds. Hidden is not unticked: its rooms' ticks
            // still count in every run.
            public bool IsShown
            {
                get { return _shown; }
                set
                {
                    if (_shown == value)
                        return;

                    _shown = value;

                    Changed("IsShown");
                }
            }

            // ITS ROOMS SHOWING - all of them with no search on. While a
            // search IS on, what a person does to a level acts on these
            // alone: the rooms it hides keep their ticks.
            public List<RoomRow> ShownRooms
            {
                get { return Rooms.Where(r => r.IsShown).ToList(); }
            }

            // What a click on the level's box means. Every room showing:
            // as it always did - ticked goes to unticked, unticked or
            // part-ticked to ticked. Some hidden by a search: the same
            // question asked of the rooms showing, since they are what it
            // acts on.
            public bool ClickMeans()
            {
                if (Rooms.All(r => r.IsShown))
                    return Checked != true;

                return !ShownRooms.All(r => r.IsOn);
            }

            // Ticks or unticks the level - its rooms showing. With every
            // room showing this is exactly the level's own tick.
            public void TickShown(bool on)
            {
                if (Rooms.All(r => r.IsShown))
                {
                    Checked = on;
                    return;
                }

                foreach (RoomRow room in ShownRooms)
                    room.IsOn = on;
            }

            private bool _picked;

            // Picked in the tree (several can be - see Picks). Nothing to
            // do with the tick: picking says what an ACTION applies to.
            public bool IsPicked
            {
                get { return _picked; }
                set
                {
                    if (_picked == value)
                        return;

                    _picked = value;

                    Changed("IsPicked");
                }
            }

            // Told of every room a PERSON ticked or unticked - directly,
            // or through this level, All or None - so the choice outlives
            // the list being rebuilt
            public Action<RoomRow> RoomChosen;

            public bool? Checked
            {
                get { return _checked; }
                set
                {
                    if (_checked == value)
                        return;

                    _checked = value;

                    Changed("Checked");

                    if (_settling || !value.HasValue || Rooms.Count == 0)
                        return;

                    // A person clicked the level: every room follows
                    _pushing = true;

                    try
                    {
                        foreach (RoomRow room in Rooms)
                            room.IsOn = value.Value;
                    }
                    finally
                    {
                        _pushing = false;
                    }

                    Changed("Label");
                }
            }

            public int TickedRooms
            {
                get { return Rooms.Count(r => r.IsOn); }
            }

            public string Label
            {
                get
                {
                    string count = string.Empty;

                    if (Counted)
                    {
                        int on = TickedRooms;

                        count = Rooms.Count == 0
                            ? "     no rooms"
                            : on == Rooms.Count
                                ? "     " + Rooms.Count + " room(s)"
                                : "     " + on + " of " + Rooms.Count + " room(s)";
                    }

                    return Name + count + (Note ?? string.Empty);
                }
            }

            // Called by a room whose tick changed
            internal void RoomToggled(RoomRow room)
            {
                if (RoomChosen != null)
                    RoomChosen(room);

                if (!_pushing)
                    Settle();
            }

            // The level's tick, worked out from its rooms
            public void Settle()
            {
                if (Rooms.Count > 0)
                {
                    int on = TickedRooms;

                    _settling = true;

                    try
                    {
                        Checked = on == 0
                            ? false
                            : on == Rooms.Count
                                ? true
                                : (bool?)null;
                    }
                    finally
                    {
                        _settling = false;
                    }
                }

                Changed("Label");
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void Changed(string what)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(what));
            }

            // A level, put together the way the window needs it:
            //
            //   - each room ticked as a PERSON last chose for it, or else
            //     as the level is pre-ticked;
            //   - the rooms attached to the level only AFTER that, so the
            //     building is not mistaken for somebody choosing;
            //   - every later choice written back into `choices`, which the
            //     window keeps, so it survives the list being rebuilt;
            //   - the level's tick worked out from its rooms.
            public static LevelRow Build(
                string name, string note, bool counted, bool preTicked,
                IEnumerable<RoomRow> rooms, IDictionary<string, bool> choices)
            {
                var level = new LevelRow
                {
                    Name = name,
                    Note = note,
                    Counted = counted,
                    Checked = preTicked
                };

                foreach (RoomRow room in rooms ?? Enumerable.Empty<RoomRow>())
                {
                    bool chosen;

                    room.IsOn = choices != null && choices.TryGetValue(room.Key, out chosen)
                        ? chosen
                        : preTicked;

                    level.Rooms.Add(room);
                }

                foreach (RoomRow room in level.Rooms)
                    room.Parent = level;

                if (choices != null)
                    level.RoomChosen = r => choices[r.Key] = r.IsOn;

                level.Settle();

                return level;
            }
        }

        // ==================================================
        // PICKING SEVERAL, at the user's request
        //
        // WPF's TreeView picks one item at a time, so the tree keeps its
        // own pick on the rows (IsPicked) and draws it itself. The rules
        // are a file list's: a click picks one, Ctrl+click adds or takes
        // away one, Shift+click picks everything from the last click to
        // this one AS THE TREE SHOWS IT - levels and the rooms of open
        // levels, top to bottom.
        //
        // What is picked is what the actions act on: ticking any picked
        // row ticks them all, and Show in plan frames every picked room.
        // A picked LEVEL stands for all its rooms there - its rooms
        // SHOWING, while a search hides some (Search).
        // ==================================================
        public static class Picks
        {
            // Every row the tree shows, in the order it shows them - a row
            // a search hides is not shown
            public static List<object> Visible(IEnumerable<LevelRow> levels)
            {
                var rows = new List<object>();

                foreach (LevelRow level in levels)
                {
                    if (!level.IsShown)
                        continue;

                    rows.Add(level);

                    if (level.IsExpanded)
                        rows.AddRange(level.Rooms.Where(r => r.IsShown));
                }

                return rows;
            }

            public static IEnumerable<object> All(IEnumerable<LevelRow> levels)
            {
                foreach (LevelRow level in levels)
                {
                    yield return level;

                    foreach (RoomRow room in level.Rooms)
                        yield return room;
                }
            }

            public static bool IsPicked(object row)
            {
                var level = row as LevelRow;

                if (level != null)
                    return level.IsPicked;

                var room = row as RoomRow;

                return room != null && room.IsPicked;
            }

            public static void Set(object row, bool picked)
            {
                var level = row as LevelRow;

                if (level != null)
                    level.IsPicked = picked;

                var room = row as RoomRow;

                if (room != null)
                    room.IsPicked = picked;
            }

            public static void Clear(IEnumerable<LevelRow> levels)
            {
                foreach (object row in All(levels))
                    Set(row, false);
            }

            // A click: plain, with Ctrl, or with Shift. Returns the row the
            // next Shift+click counts from.
            public static object Click(
                IList<LevelRow> levels, object anchor, object row,
                bool ctrl, bool shift)
            {
                if (row == null)
                    return anchor;

                if (shift && anchor != null)
                {
                    List<object> shown = Visible(levels);

                    int from = shown.IndexOf(anchor);
                    int to = shown.IndexOf(row);

                    // The last click's row has gone out of sight (its level
                    // was closed): count from this one instead
                    if (from < 0)
                        from = to;

                    if (!ctrl)
                        Clear(levels);

                    for (int i = Math.Min(from, to); i <= Math.Max(from, to); i++)
                        Set(shown[i], true);

                    // Shift keeps where the range started
                    return anchor;
                }

                if (ctrl)
                {
                    Set(row, !IsPicked(row));
                    return row;
                }

                Clear(levels);
                Set(row, true);

                return row;
            }

            // Ctrl+A: everything the tree shows
            public static void PickShown(IList<LevelRow> levels)
            {
                Clear(levels);

                foreach (object row in Visible(levels))
                    Set(row, true);
            }

            // Ticking one picked row ticks every picked row the same way.
            // Levels first: a level pushes onto all its rooms, and a room
            // picked on its own then still gets the value asked for.
            public static void Tick(IEnumerable<LevelRow> levels, bool on)
            {
                List<LevelRow> all = levels.ToList();

                foreach (LevelRow level in all.Where(l => l.IsPicked))
                    level.TickShown(on);

                foreach (RoomRow room in all.SelectMany(l => l.Rooms).Where(r => r.IsPicked))
                    room.IsOn = on;
            }

            // The rooms the picks stand for: every picked room, and every
            // room SHOWING of a picked level, once each, in the tree's order
            public static List<RoomRow> Rooms(IEnumerable<LevelRow> levels)
            {
                var rooms = new List<RoomRow>();

                foreach (LevelRow level in levels)
                {
                    foreach (RoomRow room in level.Rooms)
                    {
                        if ((level.IsPicked && room.IsShown) || room.IsPicked)
                            rooms.Add(room);
                    }
                }

                return rooms;
            }

            public static int Count(IEnumerable<LevelRow> levels)
            {
                return All(levels).Count(IsPicked);
            }
        }

        // ==================================================
        // SEARCHING THE LEVELS LIST, at the user's request: "add search
        // bar also into the rooms to process"
        //
        // The links search's rule (VENTLINKFILTER: every word, any order,
        // any case) over each room's LEVEL, number and name - the name as
        // the list shows it, apartment and model included - so "level 03
        // bedroom" finds the bedrooms of LEVEL 03, and "level 03" alone
        // the whole level.
        //
        // IT ONLY HIDES ROWS. Every room keeps its tick, the level's count
        // still counts them all, and every run does what the ticks say -
        // the links search's rule. What a PERSON does while it is on acts
        // on what is SHOWING: a level's tick, All and None (TickShown),
        // a range or Ctrl+A (Picks.Visible), a picked level (Picks.Rooms,
        // Picks.Tick) - so nothing hidden is changed by surprise.
        // ==================================================
        public static class Search
        {
            // Everything a search may find a room by
            public static string Words(LevelRow level, RoomRow room)
            {
                return (level == null ? string.Empty : level.Name) + " " +
                       (room == null ? string.Empty : room.Number + " " + room.Name);
            }

            // Shows the rows the query finds and hides the rest; returns how
            // many rooms it found (0 with no query).
            //
            // A level showing a room it found is OPENED, so what was found
            // can be seen, and put back as it was once the search is
            // cleared: `openBefore` remembers, by level name, how each level
            // stood before - kept by the window, so it outlives the list
            // being rebuilt in the middle of a search. A row the search
            // hides is unpicked: what is picked is what the actions act on,
            // and nobody can see a hidden pick.
            public static int Apply(IEnumerable<LevelRow> levels, string query, IDictionary<string, bool> openBefore)
            {
                bool searching = !string.IsNullOrWhiteSpace(query);
                int found = 0;

                foreach (LevelRow level in levels ?? Enumerable.Empty<LevelRow>())
                {
                    int shown = 0;

                    foreach (RoomRow room in level.Rooms)
                    {
                        room.IsShown = !searching || VENTLINKFILTER.Matches(Words(level, room), query);

                        if (room.IsShown)
                            shown++;
                        else
                            room.IsPicked = false;
                    }

                    level.IsShown = !searching || shown > 0 ||
                                    (level.Rooms.Count == 0 && VENTLINKFILTER.Matches(level.Name ?? string.Empty, query));

                    if (!level.IsShown)
                        level.IsPicked = false;

                    if (searching)
                    {
                        found += shown;

                        if (openBefore != null && level.Name != null && !openBefore.ContainsKey(level.Name))
                            openBefore[level.Name] = level.IsExpanded;

                        if (shown > 0)
                            level.IsExpanded = true;
                    }
                    else if (openBefore != null && level.Name != null)
                    {
                        bool was;

                        if (openBefore.TryGetValue(level.Name, out was))
                            level.IsExpanded = was;
                    }
                }

                if (!searching && openBefore != null)
                    openBefore.Clear();

                return found;
            }
        }

        // One linked room under a level: which link, which room, and the
        // level of THIS model it was put under - plain data, so the window
        // holds nothing of Revit's - and whether it is ticked.
        public class RoomRow : INotifyPropertyChanged
        {
            private bool _on;

            public long LinkId { get; set; }
            public long RoomId { get; set; }
            public string Level { get; set; }
            public string Number { get; set; }
            public string Name { get; set; }
            public string Tip { get; set; }

            // Set once the row is in the list; while it is null, setting
            // the tick is the list being BUILT, not a person choosing
            public LevelRow Parent { get; set; }

            private bool _shown = true;

            // False while a search hides it (Search.Apply). Its tick stands.
            public bool IsShown
            {
                get { return _shown; }
                set
                {
                    if (_shown == value)
                        return;

                    _shown = value;

                    if (PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("IsShown"));
                }
            }

            private bool _picked;

            public bool IsPicked
            {
                get { return _picked; }
                set
                {
                    if (_picked == value)
                        return;

                    _picked = value;

                    if (PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("IsPicked"));
                }
            }

            // "link id:room id" - the one definition of the key, used by
            // the runs through RVTLINKSELECTION.RoomKey as well, so the
            // rooms the window unticks are the rooms the runs skip
            public static string KeyOf(long linkId, long roomId)
            {
                return linkId + ":" + roomId;
            }

            public string Key
            {
                get { return KeyOf(LinkId, RoomId); }
            }

            public bool IsOn
            {
                get { return _on; }
                set
                {
                    if (_on == value)
                        return;

                    _on = value;

                    if (PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("IsOn"));

                    if (Parent != null)
                        Parent.RoomToggled(this);
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
