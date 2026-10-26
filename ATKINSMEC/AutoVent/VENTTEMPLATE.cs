using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // What the export collects out of the model, shared by the three
    // ways of writing it out.
    //
    // This used to BUILD a sheet as well - a six-column layout of its own
    // design, for when there was no workbook to fill. At the user's
    // request the export now starts from the office's real calculation
    // workbook instead (VENTOFFICE), which has every formula the office
    // uses and needed none of this re-typed. What is left is the data and
    // the worksheet-name rule, both Revit-free.
    public class VENTTEMPLATE
    {
        // ==================================================
        // What goes in it
        // ==================================================

        // Shared with REVITEXPORT rather than mirrored - it collects
        // these straight out of the model, and a second set of the same
        // four fields would be two things to keep in step
        public class Room
        {
            // "link id:room id" (RVTLINKSELECTION.RoomKey) - what an
            // arrangement by hand (VENTARRANGE) names the room by
            public string Key { get; set; }

            public string Name { get; set; }
            public string Number { get; set; }
            public double AreaM2 { get; set; }
            public double HeightM { get; set; }

            // Water closets and urinals standing in it (VENTFIXTURES),
            // for the sheet's "No. of Units"; 0 writes nothing
            public int Fixtures { get; set; }

            // THE APARTMENT IT STANDS IN, when one link holds apartments
            // and another the rooms inside them (VENTAPARTMENTS). Rooms
            // sharing a key are written as one group: the apartment's
            // name down the Space column, each room's own name beside it.
            // Empty for a room in no apartment.
            public string ApartmentKey { get; set; }
            public string Apartment { get; set; }
            public string ApartmentNumber { get; set; }

            // MERGED with other rooms: rooms sharing this id have their
            // FRESH AIR cells merged into one total (VENTARRANGE.Merges,
            // VENTROWS.MergeAir); rooms sharing ExtractMerge, their EXTRACT
            // cells (VENTARRANGE.ExtractMerges) - one side each since
            // 2026-10-22. Empty for a room on its own on that side.
            public string Merge { get; set; }
            public string ExtractMerge { get; set; }

            // UNTICKED in the levels list: an update leaves its row as it is
            // and never adds it (VENTUPDATE); the other exports leave it out
            public bool Keep { get; set; }

            // It stands in an apartment - grouped under it or not. What
            // its occupancy category is guessed by (VENTOCCUPANCY).
            public bool InDwelling { get; set; }
        }

        // Rooms by number - numerically where both are numbers - then by
        // name. Moved here from REVITEXPORT so the grouped order below,
        // and the harness, use the same rule.
        public static int Compare(Room a, Room b)
        {
            double na, nb;

            bool aNum = double.TryParse(a.Number, out na);
            bool bNum = double.TryParse(b.Number, out nb);

            if (aNum && bNum && na != nb)
                return na.CompareTo(nb);

            int byNumber = string.Compare(
                a.Number ?? string.Empty,
                b.Number ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);

            if (byNumber != 0)
                return byNumber;

            return string.Compare(
                a.Name ?? string.Empty,
                b.Name ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        // THE ORDER A LEVEL'S ROOMS ARE WRITTEN IN, with each apartment's
        // rooms kept TOGETHER - they are one merged group in the sheet, so
        // nothing may come between them. An apartment sorts among the
        // other rooms by its own number and name; its rooms sort by
        // theirs, inside it.
        public static List<Room> Ordered(IEnumerable<Room> rooms)
        {
            var units = new List<KeyValuePair<Room, List<Room>>>();
            var byApartment = new Dictionary<string, List<Room>>();

            foreach (Room room in rooms)
            {
                if (string.IsNullOrEmpty(room.ApartmentKey))
                {
                    units.Add(new KeyValuePair<Room, List<Room>>(room, new List<Room> { room }));
                    continue;
                }

                List<Room> members;

                if (!byApartment.TryGetValue(room.ApartmentKey, out members))
                {
                    members = new List<Room>();
                    byApartment.Add(room.ApartmentKey, members);

                    // What the apartment sorts by among the other rooms
                    var stand = new Room { Number = room.ApartmentNumber, Name = room.Apartment };

                    units.Add(new KeyValuePair<Room, List<Room>>(stand, members));
                }

                members.Add(room);
            }

            foreach (List<Room> members in byApartment.Values)
                members.Sort(Compare);

            // Stable: two units that compare equal keep the order they
            // were found in, every run
            return units
                .Select((u, i) => new { u, i })
                .OrderBy(x => x.u.Key, Comparer<Room>.Create(Compare))
                .ThenBy(x => x.i)
                .SelectMany(x => x.u.Value)
                .ToList();
        }

        public class Level
        {
            public string LevelName { get; set; }
            public double Elevation { get; set; }
            public List<Room> Rooms { get; set; }
        }

        // ==================================================
        // Naming the worksheet
        // ==================================================

        // Excel's own rules, applied here rather than letting EPPlus
        // throw halfway through a run: 31 characters, and none of
        // : \ / ? * [ ]
        public static string CleanName(string wanted, string fallback)
        {
            string name = (wanted ?? string.Empty).Trim();

            if (name.Length == 0)
                return fallback;

            foreach (char bad in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(bad, ' ');

            name = name.Trim();

            if (name.Length == 0)
                return fallback;

            return name.Length > 31 ? name.Substring(0, 31) : name;
        }
    }
}
