using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // HOW THE ROOMS OF A LEVEL ARE ARRANGED IN THE SHEET, by hand - at the
    // user's request (2026-10-10): "make option for user to list the IND
    // rooms and he can select any IND room and make it ungrouped from ARCH
    // rooms so it can be added directly to the excel sheet, also make an
    // option for the user to arrange the IND rooms grouped under ARCH room
    // by dragging in the list so it will come with same arrangement in the
    // excel sheet, do the same also for ARCH rooms but keep everything
    // under its level under its level".
    //
    //   - UNGROUPED: an interior room taken out of its apartment is written
    //     as a row of its own, by its own name. It changes the SHEET only:
    //     the apartments map is untouched, so the airflow write and the
    //     fixture count do exactly what they did (a room is keyed by its own
    //     name either way, grouped or not).
    //   - ORDERS: per CONTAINER - a level's top rows ("level:<name>": its
    //     ordinary rooms and its apartments, each apartment one unit), or
    //     an apartment's own rooms ("apt:<apartment key>"). Nothing moves
    //     between containers, so nothing leaves its level or its apartment.
    //   - AN ORDER THAT IS ONLY THE DEFAULT IS NOT A CHOICE (SetOrder), the
    //     rule the system-type lists and the dialogs follow - so a room drawn
    //     later still lands by its number. Keys in an order that no longer
    //     exist are passed over; rooms not in it follow, in default order.
    //
    // Applied on top of the default sort in BOTH places, so the levels list
    // shows exactly what the sheet will get: RVTLINKSELECTION.RoomsOn (the
    // list) and REVITEXPORT.CollectRooms (every export). Revit-free; tested
    // in tests/ventleveltest, saved per model by VENTSAVED.
    public static class VENTARRANGE
    {
        public class Arrangement
        {
            // Room keys ("link id:room id") of interior rooms written as
            // rows of their own
            public HashSet<string> Ungrouped = new HashSet<string>(StringComparer.Ordinal);

            // Container -> keys in the order chosen
            public Dictionary<string, List<string>> Orders =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // RENAMED FOR THE SHEET, at the user's request ("allow option
            // for user to rename rooms before exporting"): room or apartment
            // key -> the name written. The airflow write matches a sheet row
            // by name, so it applies these too - a renamed row still finds
            // its room.
            public Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal);

            public bool IsEmpty
            {
                get { return Ungrouped.Count == 0 && Orders.Count == 0 && Names.Count == 0; }
            }

            public Arrangement Clone()
            {
                var copy = new Arrangement();

                copy.Ungrouped.UnionWith(Ungrouped);

                foreach (KeyValuePair<string, List<string>> order in Orders)
                    copy.Orders[order.Key] = new List<string>(order.Value);

                foreach (KeyValuePair<string, string> name in Names)
                    copy.Names[name.Key] = name.Value;

                return copy;
            }

            // The name a room or apartment is written by: its new one, or
            // the model's
            public string NameOf(string key, string modelName)
            {
                string name;
                return key != null && Names.TryGetValue(key, out name) ? name : modelName;
            }

            public List<string> OrderOf(string container)
            {
                List<string> order;
                return Orders.TryGetValue(container ?? string.Empty, out order) ? order : null;
            }
        }

        // What this model's window has set - read by the list and the export
        public static Arrangement Current = new Arrangement();

        public static string LevelContainer(string level)
        {
            return "level:" + (level ?? string.Empty).Trim();
        }

        public static string ApartmentContainer(string apartmentKey)
        {
            return "apt:" + (apartmentKey ?? string.Empty);
        }

        // One top row of a level: an ordinary room, or an apartment with its
        // rooms - which are one merged group in the sheet and move together
        public class Unit<T>
        {
            public string Key;              // the room's key, or the apartment's
            public bool IsApartment;
            public List<T> Members = new List<T>();
        }

        // The top rows of a level, in the order the rooms came in. An
        // apartment's rooms are gathered into one unit where it first
        // appears - they are contiguous in any default order anyway.
        public static List<Unit<T>> Units<T>(IEnumerable<T> rooms, Func<T, string> key, Func<T, string> apartmentKey)
        {
            var units = new List<Unit<T>>();
            var byApartment = new Dictionary<string, Unit<T>>(StringComparer.Ordinal);

            foreach (T room in rooms ?? Enumerable.Empty<T>())
            {
                string apartment = apartmentKey(room);

                if (string.IsNullOrEmpty(apartment))
                {
                    units.Add(new Unit<T> { Key = key(room), Members = { room } });
                    continue;
                }

                Unit<T> unit;

                if (!byApartment.TryGetValue(apartment, out unit))
                {
                    unit = new Unit<T> { Key = apartment, IsApartment = true };
                    byApartment.Add(apartment, unit);
                    units.Add(unit);
                }

                unit.Members.Add(room);
            }

            return units;
        }

        // THE ARRANGEMENT ON TOP OF A DEFAULT ORDER: each apartment's rooms
        // in their chosen order, then the level's top rows in theirs. The
        // input must already be in default order; ungrouping is the
        // caller's (it changes the caller's own type).
        public static List<T> Order<T>(
            IEnumerable<T> defaultOrdered, string level, Arrangement arrangement,
            Func<T, string> key, Func<T, string> apartmentKey)
        {
            List<Unit<T>> units = Units(defaultOrdered, key, apartmentKey);

            if (arrangement == null)
                return units.SelectMany(u => u.Members).ToList();

            foreach (Unit<T> unit in units.Where(u => u.IsApartment))
                unit.Members = Sequence(unit.Members, arrangement.OrderOf(ApartmentContainer(unit.Key)), key);

            return Sequence(units, arrangement.OrderOf(LevelContainer(level)), u => u.Key)
                .SelectMany(u => u.Members)
                .ToList();
        }

        // The keys in `saved` first, in that order; everything else after,
        // as it came. A saved key nothing has any more is passed over.
        public static List<T> Sequence<T>(IList<T> items, IList<string> saved, Func<T, string> key)
        {
            if (items == null)
                return new List<T>();

            if (saved == null || saved.Count == 0)
                return items.ToList();

            var rank = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < saved.Count; i++)
            {
                if (saved[i] != null && !rank.ContainsKey(saved[i]))
                    rank.Add(saved[i], i);
            }

            return items
                .Select((item, i) => new { item, i, r = rank.TryGetValue(key(item) ?? string.Empty, out int at) ? at : int.MaxValue })
                .OrderBy(x => x.r)
                .ThenBy(x => x.i)
                .Select(x => x.item)
                .ToList();
        }

        // A CHOSEN ORDER IS KEPT ONLY WHERE IT DIFFERS FROM THE DEFAULT
        public static void SetOrder(Arrangement arrangement, string container, IList<string> sequence, IList<string> defaults)
        {
            if (arrangement == null || string.IsNullOrEmpty(container))
                return;

            if (sequence == null || sequence.Count == 0 ||
                (defaults != null && sequence.SequenceEqual(defaults, StringComparer.Ordinal)))
            {
                arrangement.Orders.Remove(container);
                return;
            }

            arrangement.Orders[container] = sequence.ToList();
        }

        // One key moved to a new place in a sequence, the others keeping
        // their order. An index past either end goes to that end.
        public static List<string> Moved(IList<string> sequence, string key, int to)
        {
            var list = (sequence ?? new List<string>()).ToList();

            int from = list.IndexOf(key);

            if (from < 0)
                return list;

            list.RemoveAt(from);
            list.Insert(Math.Max(0, Math.Min(to, list.Count)), key);

            return list;
        }

        // ==================================================
        // As the arrange dialog sees a level
        // ==================================================

        // One room as the dialog shows it - plain data, made by the window
        // from what the levels list holds
        public class Row
        {
            public string Key = string.Empty;
            public string Name = string.Empty;          // as written - renamed or not
            public string ModelName = string.Empty;     // the model's own
            public string Number = string.Empty;
            public string LinkName = string.Empty;

            // The apartment it is written under NOW - null: a row of its own
            public string ApartmentKey;
            public string Apartment;                    // as written
            public string ModelApartment;               // the model's own
            public string ApartmentNumber;

            // The apartment it STANDS IN, grouped or not - null for a room
            // that is in no apartment (an architect's room)
            public string HomeKey;
            public string Home;
            public string HomeNumber;

            // Unticked in the levels list: listed, but not exported
            public bool Exported = true;

            public bool IsInterior
            {
                get { return !string.IsNullOrEmpty(HomeKey); }
            }

            public bool IsTakenOut
            {
                get { return IsInterior && string.IsNullOrEmpty(ApartmentKey); }
            }
        }

        // A container's keys as the rows show them now: the level's top
        // rows (an apartment once, by its key), or one apartment's rooms
        public static List<string> SequenceOf(IList<Row> rows, string container)
        {
            rows = rows ?? new List<Row>();

            if ((container ?? string.Empty).StartsWith("apt:", StringComparison.Ordinal))
            {
                string apartment = container.Substring(4);

                return rows.Where(r => r.ApartmentKey == apartment).Select(r => r.Key).ToList();
            }

            return Units(rows, r => r.Key, r => r.ApartmentKey).Select(u => u.Key).ToList();
        }

        // TAKE AN INTERIOR ROOM OUT OF ITS APARTMENT: a row of its own, and
        // - where the level has an order of its own - put straight under
        // its apartment rather than at the end of the level
        public static void TakeOut(Arrangement arrangement, Row row, string level)
        {
            if (arrangement == null || row == null || !row.IsInterior)
                return;

            arrangement.Ungrouped.Add(row.Key);

            List<string> inApartment = arrangement.OrderOf(ApartmentContainer(row.HomeKey));

            if (inApartment != null)
                inApartment.Remove(row.Key);

            List<string> top = arrangement.OrderOf(LevelContainer(level));

            if (top != null && !top.Contains(row.Key))
            {
                int at = top.IndexOf(row.HomeKey);
                top.Insert(at < 0 ? top.Count : at + 1, row.Key);
            }
        }

        // ...AND PUT IT BACK: under its apartment again, last among its rooms
        // unless the apartment's own order says otherwise
        public static void PutBack(Arrangement arrangement, Row row, string level)
        {
            if (arrangement == null || row == null)
                return;

            arrangement.Ungrouped.Remove(row.Key);

            List<string> top = arrangement.OrderOf(LevelContainer(level));

            if (top != null)
                top.Remove(row.Key);
        }

        // A NEW NAME FOR THE SHEET. Empty, or the model's own name, is no
        // rename at all - the model's name is written again.
        public static void Rename(Arrangement arrangement, string key, string name, string modelName)
        {
            if (arrangement == null || string.IsNullOrEmpty(key))
                return;

            string clean = (name ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

            if (clean.Length == 0 || clean == (modelName ?? string.Empty).Trim())
                arrangement.Names.Remove(key);
            else
                arrangement.Names[key] = clean;
        }

        // EVERYTHING CHOSEN FOR ONE LEVEL forgotten: its order, its
        // apartments' orders and names, its rooms taken out and renamed
        public static void Reset(Arrangement arrangement, IEnumerable<Row> rows, string level)
        {
            if (arrangement == null)
                return;

            arrangement.Orders.Remove(LevelContainer(level));

            foreach (Row row in rows ?? Enumerable.Empty<Row>())
            {
                arrangement.Ungrouped.Remove(row.Key);
                arrangement.Names.Remove(row.Key);

                if (row.IsInterior)
                {
                    arrangement.Orders.Remove(ApartmentContainer(row.HomeKey));
                    arrangement.Names.Remove(row.HomeKey);
                }
            }
        }

        // ==================================================
        // Searching the dialog's list, at the user's request: "add a
        // search bar inside the Arrange rooms for the sheet window to look
        // for rooms using keyword"
        // ==================================================

        // Everything a search may find a room by: its number and name, its
        // model, the apartment it is written under and the one it stands in
        // - so searching an apartment's name finds its rooms, and the rooms
        // taken out of it
        public static string Words(Row row)
        {
            if (row == null)
                return string.Empty;

            // The model's names too: a renamed room is still found by its old one
            return string.Join(" ", new[]
            {
                row.Number, row.Name, row.ModelName, row.LinkName, row.Apartment, row.ModelApartment,
                row.ApartmentNumber, row.Home, row.HomeNumber
            }.Where(s => !string.IsNullOrEmpty(s)));
        }

        // THE KEYS A SEARCH LEAVES SHOWING: every room it finds, and the
        // apartment each found room is written under - a room is never
        // shown without its heading. VENTLINKFILTER's rule: every word,
        // any order, any case; nothing typed shows everything.
        public static HashSet<string> Shown(IEnumerable<Row> rows, string query)
        {
            var shown = new HashSet<string>(StringComparer.Ordinal);

            foreach (Row row in rows ?? Enumerable.Empty<Row>())
            {
                if (!VENTLINKFILTER.Matches(Words(row), query))
                    continue;

                shown.Add(row.Key);

                if (!string.IsNullOrEmpty(row.ApartmentKey))
                    shown.Add(row.ApartmentKey);
            }

            return shown;
        }

        // ONE STEP UP OR DOWN AMONG WHAT IS SHOWING: past the next key the
        // search shows, however many hidden ones lie between - so a
        // filtered list moves the way it looks. Null `shown` is everything
        // showing: one place, as Move up / Move down always did. Nothing
        // showing that way: nothing moves.
        public static List<string> Stepped(IList<string> sequence, string key, int by, ICollection<string> shown)
        {
            var list = (sequence ?? new List<string>()).ToList();

            int at = list.IndexOf(key);

            if (at < 0 || by == 0)
                return list;

            int step = by < 0 ? -1 : 1;
            int next = at + step;

            while (next >= 0 && next < list.Count && shown != null && !shown.Contains(list[next]))
                next += step;

            if (next < 0 || next >= list.Count)
                return list;

            string past = list[next];

            list.RemoveAt(at);

            int to = list.IndexOf(past);

            list.Insert(step < 0 ? to : to + 1, key);

            return list;
        }

        // ==================================================
        // Links loaded again under a new id
        // ==================================================

        // The link id a key names: "link:room", or "apt:link:room"
        public static long LinkOf(string key)
        {
            string body = key ?? string.Empty;

            if (body.StartsWith("apt:", StringComparison.Ordinal))
                body = body.Substring(4);

            int colon = body.IndexOf(':');

            long id;

            return colon > 0 && long.TryParse(body.Substring(0, colon), out id) ? id : 0;
        }

        // Every link id the arrangement names, to be saved with its label
        public static HashSet<long> Links(Arrangement arrangement)
        {
            var ids = new HashSet<long>();

            if (arrangement == null)
                return ids;

            foreach (string key in arrangement.Ungrouped)
                ids.Add(LinkOf(key));

            foreach (KeyValuePair<string, List<string>> order in arrangement.Orders)
            {
                ids.Add(LinkOf(order.Key));

                foreach (string key in order.Value)
                    ids.Add(LinkOf(key));
            }

            foreach (string key in arrangement.Names.Keys)
                ids.Add(LinkOf(key));

            ids.Remove(0);

            return ids;
        }

        // Every key re-pointed from an old link id to its new one
        public static void Remap(Arrangement arrangement, IDictionary<long, long> ids)
        {
            if (arrangement == null || ids == null || ids.Count == 0)
                return;

            Func<string, string> moved = key =>
            {
                long old = LinkOf(key);
                long now;

                if (old == 0 || !ids.TryGetValue(old, out now) || now == old)
                    return key;

                string prefix = key.StartsWith("apt:", StringComparison.Ordinal) ? "apt:" : string.Empty;
                string body = key.Substring(prefix.Length);

                return prefix + now + body.Substring(body.IndexOf(':'));
            };

            var ungrouped = arrangement.Ungrouped.Select(moved).ToList();
            arrangement.Ungrouped.Clear();
            arrangement.Ungrouped.UnionWith(ungrouped);

            var orders = arrangement.Orders.ToList();
            arrangement.Orders.Clear();

            foreach (KeyValuePair<string, List<string>> order in orders)
                arrangement.Orders[moved(order.Key)] = order.Value.Select(moved).ToList();

            var names = arrangement.Names.ToList();
            arrangement.Names.Clear();

            foreach (KeyValuePair<string, string> name in names)
                arrangement.Names[moved(name.Key)] = name.Value;
        }
    }
}
