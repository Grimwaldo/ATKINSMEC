using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // DIVIDING A ROOM'S AIR BETWEEN THE ENDCAPS THAT SERVE IT.
    //
    // AUTOVENT's endcap trace runs over ENDCAPS, not over FCUs: each
    // endcap finds the unit it feeds, asks which rooms that unit's
    // supply serves, and used to be written the WHOLE of those rooms'
    // sheet values. Nothing iterated the FCUs, so nothing knew a room
    // had two of them - a room with an endcap on each of two units had
    // its fresh air written twice.
    //
    // How it is divided has changed twice, both at the user's request:
    //
    //   1. REPORT ONLY - leave the numbers, list the rooms.
    //   2. SPLIT BY THE FCUs - half per unit. That wrote the right
    //      number per unit but lost air: a room whose second unit had no
    //      endcap drawn got half its fresh air written and the other half
    //      went nowhere.
    //   3. SPLIT BY THE ENDCAPS - what it does now. Collect every unit
    //      serving the room, count the endcaps on them, and divide the
    //      room's air equally between THOSE. Every endcap serving the
    //      room gets the same share, and the shares always add back up
    //      to the whole: nothing is written twice and nothing is lost.
    //
    // "An endcap on a unit" means one the trace BOUND to it - the
    // nearest equipment within the endcap reach. Not a fresh search
    // around each unit: an endcap standing between two units would then
    // be counted against both, and the double-write this exists to stop
    // would come straight back.
    //
    // Deliberately REVIT-FREE - the tally, the share and what each
    // endcap is finally written are all here, so the whole of it can be
    // tested with no model open. Harness at scratchpad/ventfcutest.
    public class VENTFCUCHECK
    {
        public class Room
        {
            // The room key ("link id:room id"), and what the log and the
            // report call it
            public string Key { get; set; }

            public string Name { get; set; }

            // Equipment whose supply system reaches a terminal standing
            // in this room
            public int Fcus { get; set; }

            // Endcaps bound to ANY of those units, counted PER SIDE. This
            // is the divisor. A correctly drawn unit usually has one of
            // each side, so adding the two together would halve every
            // room in the job.
            public int FreshEndcaps { get; set; }

            public int ExtractEndcaps { get; set; }

            // How many of the units serving this room have an endcap on
            // that side AT ALL - one unit can carry two, so this is not
            // the same number. It is what says a unit is drawn with no
            // endcap, so the others carry its part.
            public int FcusWithFresh { get; set; }

            public int FcusWithExtract { get; set; }
        }

        // ==================================================
        // The tally
        // ==================================================

        // One room per key, with its units and the endcaps on them.
        //
        //   roomsOfFcu  - each unit, and the rooms its supply serves
        //   freshOn     - each unit, and how many fresh endcaps the
        //                 trace bound to it
        //   extractOn   - the same, extract
        //   nameOf      - what to call a room key in the report
        public static Dictionary<string, Room> Tally(
            IDictionary<long, List<string>> roomsOfFcu,
            IDictionary<long, int> freshOn,
            IDictionary<long, int> extractOn,
            Func<string, string> nameOf)
        {
            var tally = new Dictionary<string, Room>();

            if (roomsOfFcu == null)
                return tally;

            foreach (KeyValuePair<long, List<string>> unit in roomsOfFcu)
            {
                if (unit.Value == null)
                    continue;

                int fresh = Lookup(freshOn, unit.Key);
                int extract = Lookup(extractOn, unit.Key);

                // A unit that serves one room twice over - two grilles of
                // its own in the same room - is still ONE unit in it
                var seen = new HashSet<string>();

                foreach (string roomKey in unit.Value)
                {
                    if (roomKey == null || !seen.Add(roomKey))
                        continue;

                    Room room;

                    if (!tally.TryGetValue(roomKey, out room))
                    {
                        room = new Room
                        {
                            Key = roomKey,
                            Name = nameOf == null
                                ? roomKey
                                : nameOf(roomKey)
                        };

                        tally[roomKey] = room;
                    }

                    room.Fcus++;

                    room.FreshEndcaps += fresh;
                    room.ExtractEndcaps += extract;

                    if (fresh > 0)
                        room.FcusWithFresh++;

                    if (extract > 0)
                        room.FcusWithExtract++;
                }
            }

            return tally;
        }

        // ==================================================
        // The split
        // ==================================================

        // WHAT ONE ENDCAP CARRIES OF ONE ROOM'S AIR: the room's value
        // divided equally between every endcap, on that side, bound to
        // any unit serving the room.
        //
        // A COUNT OF ZERO MEANS "NOT KNOWN", AND IS TREATED AS ONE.
        // Dividing by it would be a crash or an infinity; treating it as
        // none would write a zero over a real number. One reproduces
        // what this did before any of it existed. In practice it cannot
        // be zero here - the endcap asking is itself one of the count -
        // so this is a guard, not a rule.
        //
        // One unit with one endcap divides by one, so the ordinary room,
        // which is nearly all of them, is untouched.
        public static double Share(double roomValue, int endcapsServingRoom)
        {
            if (roomValue <= 0)
                return 0;

            int endcaps = endcapsServingRoom > 0 ? endcapsServingRoom : 1;

            return roomValue / endcaps;
        }

        // WHAT ONE ENDCAP IS FINALLY WRITTEN: its share of every room its
        // unit serves, added up. REVITROOMS calls this rather than doing
        // the sum itself, so the harness tests the number that actually
        // goes onto the terminal and not a copy of how it is worked out.
        //
        //   servedRooms  - the rooms this endcap's unit serves
        //   roomValues   - each room's sheet value, on this side; a room
        //                  the sheet does not have contributes nothing
        //   tally        - from Tally
        //   fresh        - which side
        public static double Carried(
            IEnumerable<string> servedRooms,
            IDictionary<string, double> roomValues,
            IDictionary<string, Room> tally,
            bool fresh)
        {
            double total = 0;

            if (servedRooms == null || roomValues == null)
                return 0;

            var seen = new HashSet<string>();

            foreach (string roomKey in servedRooms)
            {
                // A room counted once per endcap, however many of the
                // unit's grilles stand in it
                if (roomKey == null || !seen.Add(roomKey))
                    continue;

                double value;

                if (!roomValues.TryGetValue(roomKey, out value))
                    continue;

                Room room;

                int endcaps =
                    tally != null && tally.TryGetValue(roomKey, out room)
                        ? (fresh ? room.FreshEndcaps : room.ExtractEndcaps)
                        : 1;

                total += Share(value, endcaps);
            }

            return total;
        }

        // ==================================================
        // Saying what was done
        // ==================================================

        // More than one endcap on a side: the number written onto each
        // is no longer the number in the sheet, so it is worth a line
        public static bool Divided(Room room)
        {
            return room != null
                && (room.FreshEndcaps > 1 || room.ExtractEndcaps > 1);
        }

        // A UNIT DRAWN WITH NO ENDCAP. Its part of the room's air is not
        // lost - the endcaps on the other units now carry it, which is
        // the point of dividing by endcaps - but the unit carries none of
        // it in the model, and that is a modelling gap worth seeing.
        //
        // A side with no endcaps ANYWHERE is not this. Plenty of jobs draw
        // none and take the room's air from the in-room terminals;
        // complaining there would bury the real ones.
        public static bool UnitWithoutFresh(Room room)
        {
            return room != null
                && room.FcusWithFresh > 0
                && room.FcusWithFresh < room.Fcus;
        }

        public static bool UnitWithoutExtract(Room room)
        {
            return room != null
                && room.FcusWithExtract > 0
                && room.FcusWithExtract < room.Fcus;
        }

        public static bool Worth(Room room)
        {
            return Divided(room)
                || UnitWithoutFresh(room)
                || UnitWithoutExtract(room);
        }

        // "1.02 Meeting Room:  2 FCU(s)  -  fresh divided between 3
        //  endcaps"
        public static string Line(Room room)
        {
            if (room == null)
                return string.Empty;

            string text = room.Name + ":  " + room.Fcus + " FCU(s)";

            var parts = new List<string>();

            if (room.FreshEndcaps > 1)
                parts.Add("fresh divided between " + room.FreshEndcaps +
                          " endcaps");

            if (room.ExtractEndcaps > 1)
                parts.Add("extract divided between " + room.ExtractEndcaps +
                          " endcaps");

            if (UnitWithoutFresh(room))
                parts.Add((room.Fcus - room.FcusWithFresh) +
                          " unit(s) have NO fresh endcap, so the others " +
                          "carry all of it");

            if (UnitWithoutExtract(room))
                parts.Add((room.Fcus - room.FcusWithExtract) +
                          " unit(s) have NO extract endcap, so the others " +
                          "carry all of it");

            return parts.Count == 0
                ? text
                : text + "  -  " + string.Join("; ", parts.ToArray());
        }

        // ONLY A SIDE THE SHEET GIVES AIR TO IS WORTH A LINE. A room with
        // no fresh air in the sheet is not short of a fresh endcap -
        // nothing is written for it either way - and listing it was
        // noise: user-reported (kifaf, 2026-10-11), kitchens with no fresh
        // air in the sheet in the result. Room keys to their sheet values;
        // a room the sheet has not got counts as none.
        public static List<Room> Worth(
            IDictionary<string, Room> tally,
            IDictionary<string, double> fresh,
            IDictionary<string, double> extract)
        {
            var found = new List<Room>();

            if (tally == null)
                return found;

            foreach (KeyValuePair<string, Room> each in tally)
            {
                if (Sides(each.Value, Air(fresh, each.Key), Air(extract, each.Key)) != Side.None)
                    found.Add(each.Value);
            }

            found.Sort(delegate (Room a, Room b)
            {
                return string.Compare(a.Name ?? string.Empty, b.Name ?? string.Empty,
                                      StringComparison.OrdinalIgnoreCase);
            });

            return found;
        }

        // The line for a room, saying only the sides with air
        public static string Line(Room room, IDictionary<string, double> fresh,
                                  IDictionary<string, double> extract)
        {
            if (room == null)
                return string.Empty;

            Side sides = Sides(room, Air(fresh, room.Key), Air(extract, room.Key));

            var parts = new List<string>();

            if ((sides & Side.Fresh) != 0)
            {
                if (room.FreshEndcaps > 1)
                    parts.Add("fresh divided between " + room.FreshEndcaps + " endcaps");

                if (UnitWithoutFresh(room))
                    parts.Add((room.Fcus - room.FcusWithFresh) +
                              " unit(s) have NO fresh endcap, so the others carry all of it");
            }

            if ((sides & Side.Extract) != 0)
            {
                if (room.ExtractEndcaps > 1)
                    parts.Add("extract divided between " + room.ExtractEndcaps + " endcaps");

                if (UnitWithoutExtract(room))
                    parts.Add((room.Fcus - room.FcusWithExtract) +
                              " unit(s) have NO extract endcap, so the others carry all of it");
            }

            string text = room.Name + ":  " + room.Fcus + " FCU(s)";

            return parts.Count == 0 ? text : text + "  -  " + string.Join("; ", parts.ToArray());
        }

        [Flags]
        private enum Side { None = 0, Fresh = 1, Extract = 2 }

        private static Side Sides(Room room, double fresh, double extract)
        {
            if (room == null)
                return Side.None;

            Side sides = Side.None;

            if (fresh > 0 && (room.FreshEndcaps > 1 || UnitWithoutFresh(room)))
                sides |= Side.Fresh;

            if (extract > 0 && (room.ExtractEndcaps > 1 || UnitWithoutExtract(room)))
                sides |= Side.Extract;

            return sides;
        }

        private static double Air(IDictionary<string, double> values, string key)
        {
            double value;

            return values != null && key != null && values.TryGetValue(key, out value) ? value : 0;
        }

        // The rooms worth a line, in a settled order so two runs of one
        // model cannot disagree
        public static List<Room> Worth(IEnumerable<Room> rooms)
        {
            var found = new List<Room>();

            if (rooms == null)
                return found;

            foreach (Room room in rooms)
            {
                if (Worth(room))
                    found.Add(room);
            }

            found.Sort(delegate (Room a, Room b)
            {
                return string.Compare(
                    a.Name ?? string.Empty,
                    b.Name ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });

            return found;
        }

        // ==================================================
        // Merged rooms
        // ==================================================
        //
        // ROOMS ONE FCU SERVES THROUGH ONE FRESH AIR CONNECTION (user,
        // 2026-10-12) are ONE for the airflow write: their air added up and
        // given once - shared by the fresh terminals standing in any of
        // them, or carried by the connection on the unit serving any of
        // them. A group is rooms whose fresh air or extract cell is merged
        // down in the sheet, or rooms merged in Arrange rooms; groups
        // sharing a room are one. Returns each room key's group key - the
        // group's first room in `rooms` - and a room in no group maps to
        // itself.
        public static Dictionary<string, string> Groups(
            IEnumerable<string> rooms,
            IEnumerable<IEnumerable<string>> sets)
        {
            var order = new List<string>();
            var index = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string room in rooms ?? Enumerable.Empty<string>())
            {
                if (room != null && !index.ContainsKey(room))
                {
                    index[room] = order.Count;
                    order.Add(room);
                }
            }

            var parent = Enumerable.Range(0, order.Count).ToArray();

            Func<int, int> find = null;
            find = i => parent[i] == i ? i : (parent[i] = find(parent[i]));

            foreach (IEnumerable<string> set in sets ?? Enumerable.Empty<IEnumerable<string>>())
            {
                int first = -1;

                foreach (string room in set ?? Enumerable.Empty<string>())
                {
                    int at;

                    if (room == null || !index.TryGetValue(room, out at))
                        continue;

                    if (first < 0)
                    {
                        first = at;
                        continue;
                    }

                    int a = find(first), b = find(at);

                    // the earlier room leads, so the group's key is stable
                    if (a != b)
                    {
                        parent[Math.Max(a, b)] = Math.Min(a, b);
                        first = Math.Min(a, b);
                    }
                }
            }

            var groups = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < order.Count; i++)
                groups[order[i]] = order[find(i)];

            return groups;
        }

        // Each group's air: its rooms' values added. Keys are group keys.
        public static Dictionary<string, double> Totals(
            IDictionary<string, double> roomValues,
            IDictionary<string, string> groups)
        {
            var totals = new Dictionary<string, double>(StringComparer.Ordinal);

            if (roomValues == null)
                return totals;

            foreach (KeyValuePair<string, double> room in roomValues)
            {
                string group;

                if (groups == null || !groups.TryGetValue(room.Key, out group))
                    group = room.Key;

                double sum;
                totals.TryGetValue(group, out sum);
                totals[group] = sum + room.Value;
            }

            return totals;
        }

        // Room keys as their groups' keys, each group once, in first-seen
        // order
        public static List<string> AsGroups(IEnumerable<string> roomKeys, IDictionary<string, string> groups)
        {
            var result = new List<string>();

            foreach (string room in roomKeys ?? Enumerable.Empty<string>())
            {
                string group;

                if (room == null)
                    continue;

                if (groups == null || !groups.TryGetValue(room, out group))
                    group = room;

                if (!result.Contains(group))
                    result.Add(group);
            }

            return result;
        }

        // ==================================================
        // Air that went nowhere
        // ==================================================

        // THE ROOMS AN ENDCAP CARRIED: those of its unit's rooms with air
        // on this side - exactly the ones Carried added a share of.
        public static List<string> CarriedRooms(
            IEnumerable<string> servedRooms,
            IDictionary<string, double> roomValues)
        {
            var found = new List<string>();

            if (servedRooms == null || roomValues == null)
                return found;

            foreach (string roomKey in servedRooms)
            {
                double value;

                if (roomKey != null &&
                    !found.Contains(roomKey) &&
                    roomValues.TryGetValue(roomKey, out value) &&
                    value > 0)
                {
                    found.Add(roomKey);
                }
            }

            return found;
        }

        // A ROOM WHOSE AIR WENT NOWHERE: the sheet gives it some, and no
        // terminal in it and no endcap took any of it. That passed without
        // a word - user-reported (2026-10-10): a master bedroom's 50 l/s,
        // because its unit's fresh air bellmouth was not named like an
        // endcap. One line each, "121.046 MASTER BEDROOM: 50 l/s", sorted.
        public static List<string> Nowhere(
            IDictionary<string, double> roomValues,
            ICollection<string> landed,
            Func<string, string> label)
        {
            var lines = new List<string>();

            if (roomValues == null)
                return lines;

            foreach (KeyValuePair<string, double> room in roomValues)
            {
                if (room.Value <= 0 ||
                    (landed != null && landed.Contains(room.Key)))
                    continue;

                string name = label == null ? room.Key : label(room.Key);

                lines.Add(name + ": " + room.Value.ToString(
                              "0.#", System.Globalization.CultureInfo.InvariantCulture) + " l/s");
            }

            lines.Sort(StringComparer.OrdinalIgnoreCase);

            return lines;
        }

        private static int Lookup(IDictionary<long, int> of, long key)
        {
            int found;

            return of != null && of.TryGetValue(key, out found) ? found : 0;
        }
    }
}
