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

            // Bound two-way to the tree's IsExpanded. It is set only
            // BEFORE a row is added (to keep a rebuilt list open where it
            // was), so nothing has to be told when it changes.
            public bool IsExpanded { get; set; }

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
        // A picked LEVEL stands for all its rooms there.
        // ==================================================
        public static class Picks
        {
            // Every row the tree shows, in the order it shows them
            public static List<object> Visible(IEnumerable<LevelRow> levels)
            {
                var rows = new List<object>();

                foreach (LevelRow level in levels)
                {
                    rows.Add(level);

                    if (level.IsExpanded)
                        rows.AddRange(level.Rooms);
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
                    level.Checked = on;

                foreach (RoomRow room in all.SelectMany(l => l.Rooms).Where(r => r.IsPicked))
                    room.IsOn = on;
            }

            // The rooms the picks stand for: every picked room, and every
            // room of a picked level, once each, in the tree's order
            public static List<RoomRow> Rooms(IEnumerable<LevelRow> levels)
            {
                var rooms = new List<RoomRow>();

                foreach (LevelRow level in levels)
                {
                    foreach (RoomRow room in level.Rooms)
                    {
                        if (level.IsPicked || room.IsPicked)
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
