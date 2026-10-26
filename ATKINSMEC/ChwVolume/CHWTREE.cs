using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // CHW VOLUME'S MODEL LIST AS A TREE, and its search (user, 2026-10-19:
    // "the CHW volume addin should list the linked instances of main model
    // below it and the linked instances (nested links) of linked models and
    // add a search bar for the models").
    //
    //   - the open model, each link PLACEMENT in it below, each nested link
    //     below the placement it is in - depth first, by name;
    //   - a model placed more than once is listed per placement; the first
    //     placement met counts, the rest are Repeat - offered unticked, so
    //     the total is what it was until a person ticks one in;
    //   - the search shows the rows whose name holds every word typed and
    //     every row above them, the open model included, so the tree still
    //     reads. It only hides: what is ticked is still measured.
    //
    // Revit-free (CHWVOLUME reads the placements); tests\chwtreetest.
    public static class CHWTREE
    {
        // A placement as read: its parent placement (0: the open model),
        // its name, and which model it is (one key per model file)
        public class Placement
        {
            public long Id;
            public long Parent;
            public string Label = string.Empty;
            public string Model = string.Empty;
        }

        public class Line
        {
            public long Id;
            public long Parent;
            public string ParentName = string.Empty;
            public string Label = string.Empty;
            public int Depth;
            public bool Repeat;
        }

        public static List<Line> Order(IEnumerable<Placement> placements, string hostName)
        {
            List<Placement> all = (placements ?? Enumerable.Empty<Placement>()).Where(p => p != null).ToList();
            var ids = new HashSet<long>(all.Select(p => p.Id));

            // A parent not among them (unloaded, say): under the open model
            Func<Placement, long> parentOf = p => p.Parent != 0 && p.Parent != p.Id && ids.Contains(p.Parent) ? p.Parent : 0;

            var lines = new List<Line>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var placed = new HashSet<long>();

            void Under(long parent, string parentName, int depth)
            {
                if (depth > 8)
                    return;

                foreach (Placement p in all
                             .Where(x => parentOf(x) == parent)
                             .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(x => x.Id))
                {
                    // One parent each, so nothing from the open model down
                    // comes round twice; a loop never reaches here (below)
                    placed.Add(p.Id);

                    lines.Add(new Line
                    {
                        Id = p.Id,
                        Parent = parent,
                        ParentName = parentName ?? string.Empty,
                        Label = p.Label ?? string.Empty,
                        Depth = depth,
                        Repeat = !seen.Add(p.Model ?? string.Empty)
                    });

                    Under(p.Id, p.Label, depth + 1);
                }
            }

            Under(0, hostName, 1);

            // Anything a loop kept out: at the top, so nothing is lost
            foreach (Placement p in all.Where(x => !placed.Contains(x.Id)).OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase))
            {
                placed.Add(p.Id);

                lines.Add(new Line
                {
                    Id = p.Id,
                    ParentName = hostName ?? string.Empty,
                    Label = p.Label ?? string.Empty,
                    Depth = 1,
                    Repeat = !seen.Add(p.Model ?? string.Empty)
                });
            }

            return lines;
        }

        // THE PLACEMENT A NESTED LINK IS IN, among its parent model's
        // placements: the one whose origin is nearest its own (0: none)
        public static long Nearest(IEnumerable<KeyValuePair<long, double[]>> candidates, double[] origin)
        {
            long best = 0;
            double bestDistance = double.MaxValue;

            foreach (KeyValuePair<long, double[]> c in candidates ?? Enumerable.Empty<KeyValuePair<long, double[]>>())
            {
                if (c.Value == null || origin == null || c.Value.Length < 3 || origin.Length < 3)
                    continue;

                double dx = c.Value[0] - origin[0], dy = c.Value[1] - origin[1], dz = c.Value[2] - origin[2];
                double d = dx * dx + dy * dy + dz * dz;

                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c.Key;
                }
            }

            return best;
        }

        // A row of the list as the search sees it
        public class Row
        {
            public string Text = string.Empty;
            public long Id;          // its placement - 0 for the open model or an ACC model
            public long Parent;      // the placement above it - 0: the open model
            public bool Host;        // the open model
            public bool Local;       // the open model or one of its links (not ACC)
        }

        // THE ROWS TO SHOW, by index: those the search finds and every row
        // above them - for a link of the open model, the open model too
        public static HashSet<int> Shown(IList<Row> rows, string query)
        {
            var shown = new HashSet<int>();

            if (rows == null)
                return shown;

            var at = new Dictionary<long, int>();
            int host = -1;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null)
                    continue;

                if (rows[i].Host)
                    host = i;
                else if (rows[i].Local && rows[i].Id != 0 && !at.ContainsKey(rows[i].Id))
                    at[rows[i].Id] = i;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];

                if (row == null || !VENTLINKFILTER.Matches(row.Text, query))
                    continue;

                shown.Add(i);

                if (!row.Local || row.Host)
                    continue;

                long parent = row.Parent;
                int above;

                for (int up = 0; parent != 0 && up < 10 && at.TryGetValue(parent, out above); up++)
                {
                    shown.Add(above);
                    parent = rows[above].Parent;
                }

                if (host >= 0)
                    shown.Add(host);
            }

            return shown;
        }
    }
}
