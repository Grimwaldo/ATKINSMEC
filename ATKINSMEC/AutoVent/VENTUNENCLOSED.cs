using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // ROOMS WITH NO AREA IN THE LINKED MODELS (user, 2026-10-24: "add the
    // list of not enclosed rooms"). A room placed in the architect's model
    // whose boundary is not closed has an area of 0 and no volume: AUTOVENT
    // reads only rooms with an area (RVTLINKSELECTION.ReadRooms), and no
    // terminal could be found inside one anyway. The fix is the
    // architect's, so Levels and rooms lists them to be sent back - read
    // with the rooms (RVTLINKSELECTION.ReadUnenclosed), per link.
    //
    // Revit-free: tested by tests\arrangeflowtest.
    public static class VENTUNENCLOSED
    {
        // PROPERTIES, not fields: the list binds to them, and WPF binds to
        // properties only (a field shows as an empty cell)
        public class Room
        {
            public long LinkId { get; set; }
            public long RoomId { get; set; }
            public string Number { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string LinkName { get; set; } = string.Empty;

            // The level it hangs from in its own model - the architect's
            // name for it - and that level's height, for the order
            public string Level { get; set; } = string.Empty;
            public double LevelZ { get; set; }

            // A REDUNDANT room: placed inside another room's boundary - that
            // room ("number name"). Null: not enclosed.
            public string Inside { get; set; }

            public string Key
            {
                get { return LinkId + ":" + RoomId; }
            }

            public string Why
            {
                get { return Inside == null ? "Not enclosed" : "Inside another room - " + Inside; }
            }
        }

        // By model, up the building, by number as a person reads it
        public static List<Room> InOrder(IEnumerable<Room> rooms)
        {
            var list = (rooms ?? Enumerable.Empty<Room>()).Where(r => r != null).ToList();

            list.Sort((a, b) =>
            {
                int byModel = string.Compare(a.LinkName, b.LinkName, StringComparison.OrdinalIgnoreCase);

                if (byModel != 0)
                    return byModel;

                int byLevel = a.LevelZ.CompareTo(b.LevelZ);

                if (byLevel != 0)
                    return byLevel;

                int byNumber = Natural(a.Number, b.Number);

                return byNumber != 0 ? byNumber : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        // For an email or Excel: a heading line, then one room a line,
        // tab-separated
        public static string Text(IEnumerable<Room> rooms)
        {
            var text = new StringBuilder();

            text.Append("Model\tLevel\tNumber\tName\tWhy\r\n");

            foreach (Room room in InOrder(rooms))
            {
                text.Append(Clean(room.LinkName)).Append('\t')
                    .Append(Clean(room.Level)).Append('\t')
                    .Append(Clean(room.Number)).Append('\t')
                    .Append(Clean(room.Name)).Append('\t')
                    .Append(Clean(room.Why)).Append("\r\n");
            }

            return text.ToString();
        }

        // The button in Levels and rooms
        public static string Said(int count)
        {
            return "Not enclosed (" + count + ")" + (count > 0 ? "..." : string.Empty);
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        // Digits as numbers: B1.2 before B1.10
        private static int Natural(string a, string b)
        {
            a = a ?? string.Empty;
            b = b ?? string.Empty;

            int i = 0, j = 0;

            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int si = i, sj = j;

                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;

                    string na = a.Substring(si, i - si).TrimStart('0');
                    string nb = b.Substring(sj, j - sj).TrimStart('0');

                    int byLength = na.Length.CompareTo(nb.Length);

                    if (byLength != 0)
                        return byLength;

                    int byDigits = string.CompareOrdinal(na, nb);

                    if (byDigits != 0)
                        return byDigits;

                    continue;
                }

                int byChar = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));

                if (byChar != 0)
                    return byChar;

                i++;
                j++;
            }

            return (a.Length - i).CompareTo(b.Length - j);
        }
    }
}
