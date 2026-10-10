using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // THE REVIT-FREE HALF OF RVTLINKSELECTION: the linked rooms as plain
    // data, and the arithmetic that puts them under this model's levels.
    //
    // AUTOVENT's window used to read every ticked link's rooms through the
    // API on every tick, from its own handlers - outside any API context,
    // which Autodesk does not support, and slow once apartments made each
    // tick search every room of every loaded link. Now each link's rooms
    // are read ONCE, inside the external event (VENTREAD), and a tick only
    // runs RoomsOn over what was read. Being arithmetic, it is tested
    // directly - tests\ventleveltest.
    public partial class RVTLINKSELECTION
    {
        // "link id:room id" - the key REVITROOMS already files rooms by.
        // Defined once, beside the window's room rows, so the rooms the
        // window unticks are exactly the rooms the runs skip.
        public static string RoomKey(long linkId, long roomId)
        {
            return VENTWindow.RoomRow.KeyOf(linkId, roomId);
        }

        public class LinkedRoom
        {
            public long LinkId { get; set; }
            public long RoomId { get; set; }
            public string Name { get; set; }
            public string Number { get; set; }
            public string LinkName { get; set; }

            // The level it hangs from IN THE LINK, and its floor in this
            // model's internal coordinates - so the window can say why it
            // was put where it was
            public string LinkLevel { get; set; }
            public double Floor { get; set; }

            // The apartment it stands in (VENTAPARTMENTS), or null
            public string ApartmentKey { get; set; }
            public string Apartment { get; set; }
            public string ApartmentNumber { get; set; }

            public string Key
            {
                get { return RoomKey(LinkId, RoomId); }
            }
        }

        // THE TICKED LINKS' ROOMS, GROUPED BY THE STOREY OF THIS MODEL each
        // one stands on - the band rule the export and the airflow write
        // use (VENTLEVELMAP.StoreyOf), so what the list shows under a level
        // is exactly what a run would process there.
        //
        // roomsOf holds every room READ for each link (ReadRooms), with no
        // apartment on it; this decides which of them the list shows:
        //
        //   - a ticked link gives all its rooms; an interior placement
        //     nobody ticked gives only the rooms inside an apartment
        //     (VENTAPARTMENTS.Map.Takes) - the export does the same;
        //   - an apartment with rooms inside is not a row, its rooms are;
        //   - a room below every level of this model is in no band.
        //
        // A link not in roomsOf has not been read yet, and gives nothing.
        // The rooms handed back are COPIES, so marking one with its
        // apartment never touches what was read.
        //
        // ARRANGED BY HAND when `arrangement` is given (VENTARRANGE): an
        // interior room taken out of its apartment is listed as a room of
        // its own, and each level comes in the order chosen for the sheet -
        // the export applies the same arrangement, so the list still shows
        // exactly what the sheet will get. Null: the default order.
        public static Dictionary<string, List<LinkedRoom>> RoomsOn(
            IEnumerable<long> tickedLinks,
            IDictionary<long, List<LinkedRoom>> roomsOf,
            VENTAPARTMENTS.Map apartments,
            IList<VENTLEVELMAP.Storey> storeys,
            VENTARRANGE.Arrangement arrangement = null)
        {
            var byLevel = new Dictionary<string, List<LinkedRoom>>(
                StringComparer.OrdinalIgnoreCase);

            if (tickedLinks == null || roomsOf == null || storeys == null)
                return byLevel;

            apartments = apartments ?? new VENTAPARTMENTS.Map();

            var ticked = new HashSet<long>(tickedLinks);

            // NOTHING TICKED IS NOTHING LISTED, apartments or not: no run
            // starts without a ticked link, and the list shows what a run
            // would do. (The Apartments tick ticks its row, but the row
            // can be unticked again.)
            if (ticked.Count == 0)
                return byLevel;

            foreach (long linkId in apartments.RoomLinks(ticked))
            {
                List<LinkedRoom> rooms;

                if (!roomsOf.TryGetValue(linkId, out rooms) || rooms == null)
                    continue;

                foreach (LinkedRoom room in rooms)
                {
                    string key = RoomKey(linkId, room.RoomId);

                    if (!apartments.Takes(linkId, key, ticked))
                        continue;

                    if (apartments.Holding.Contains(key))
                        continue;

                    string level = VENTLEVELMAP.NameAt(storeys, room.Floor);

                    if (level.Length == 0)
                        continue;

                    VENTAPARTMENTS.Apartment apartment = apartments.Of(key);

                    // Taken out of its apartment by hand: a row of its own
                    if (apartment != null && arrangement != null && arrangement.Ungrouped.Contains(key))
                        apartment = null;

                    List<LinkedRoom> list;

                    if (!byLevel.TryGetValue(level, out list))
                    {
                        list = new List<LinkedRoom>();
                        byLevel.Add(level, list);
                    }

                    list.Add(new LinkedRoom
                    {
                        LinkId = linkId,
                        RoomId = room.RoomId,
                        Name = room.Name ?? string.Empty,
                        Number = room.Number ?? string.Empty,
                        LinkName = room.LinkName ?? string.Empty,
                        LinkLevel = room.LinkLevel ?? string.Empty,
                        Floor = room.Floor,
                        ApartmentKey = apartment == null ? null : apartment.Key,
                        Apartment = apartment == null ? null : apartment.Name,
                        ApartmentNumber = apartment == null ? null : apartment.Number
                    });
                }
            }

            foreach (List<LinkedRoom> list in byLevel.Values)
                list.Sort(ByApartmentThenNumber);

            if (arrangement != null)
            {
                foreach (string level in byLevel.Keys.ToList())
                {
                    byLevel[level] = VENTARRANGE.Order(
                        byLevel[level], level, arrangement, r => r.Key, r => r.ApartmentKey);
                }
            }

            return byLevel;
        }

        // An apartment's rooms together, as the export writes them: an
        // apartment sorts among the rooms by its own number and name, its
        // rooms by theirs inside it
        private static int ByApartmentThenNumber(LinkedRoom a, LinkedRoom b)
        {
            LinkedRoom ua = a.ApartmentKey == null ? a : new LinkedRoom { Number = a.ApartmentNumber ?? string.Empty, Name = a.Apartment ?? string.Empty };
            LinkedRoom ub = b.ApartmentKey == null ? b : new LinkedRoom { Number = b.ApartmentNumber ?? string.Empty, Name = b.Apartment ?? string.Empty };

            int byUnit = ByNumber(ua, ub);

            if (byUnit != 0)
                return byUnit;

            // Two apartments numbered and named alike stay apart
            int byKey = string.CompareOrdinal(a.ApartmentKey ?? string.Empty, b.ApartmentKey ?? string.Empty);

            return byKey != 0 ? byKey : ByNumber(a, b);
        }

        // Room numbers are usually numeric but may carry letters; the
        // same order the export writes them in
        private static int ByNumber(LinkedRoom a, LinkedRoom b)
        {
            double na, nb;

            if (double.TryParse(a.Number, out na) &&
                double.TryParse(b.Number, out nb) &&
                na != nb)
                return na.CompareTo(nb);

            int byNumber = string.Compare(
                a.Number, b.Number, StringComparison.OrdinalIgnoreCase);

            return byNumber != 0
                ? byNumber
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
