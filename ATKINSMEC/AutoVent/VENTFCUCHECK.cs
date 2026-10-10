using System;
using System.Collections.Generic;

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
            // What the log and the report call it
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
