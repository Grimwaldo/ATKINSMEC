using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // ROOM TAGS - which rooms of the ticked linked models are shown with no
    // room tag in the plans placed on sheets, and what tagging them means,
    // never with a second tag (user, 2026-10-23: "make a new addin to check
    // the missing room tags in sheets and annotate them for selected links
    // with search bar for links and make them annotate without
    // duplication").
    //
    //   - a room counts as tagged in a view when a tag of it is in the view
    //     its annotation belongs to - the view itself, or the PRIMARY view
    //     of a dependent one (dependents share their primary's tags);
    //   - a room missing in several views of one owner is tagged ONCE there
    //     (ToTag), so two dependents of one plan never get a tag each;
    //   - one room of a model placed twice is two rooms (link id : room id)
    //     and is tagged once per placement - not a duplicate.
    //
    // AND REMOVES DUPLICATES (user, 2026-10-24: "add option in the addin to
    // remove duplicate tags") - Doubles has the rule.
    //
    // Revit-free: TAGREAD reads the model into these rows, TAGWRITE places
    // the tags. Tested by tests\roomtagtest.
    public static class TAGPLAN
    {
        public static string KeyOf(long link, long room)
        {
            return link + ":" + room;
        }

        // A link placement as the window lists it
        public class Link
        {
            public long Id;
            public string Label = string.Empty;
            public int Rooms;

            public override string ToString()
            {
                return Label + "   (" + Rooms + (Rooms == 1 ? " room)" : " rooms)");
            }
        }

        // What TAGREAD read
        public class Read
        {
            public List<View> Views = new List<View>();

            // Each owner view's linked rooms already tagged there
            public Dictionary<long, HashSet<string>> Tagged = new Dictionary<long, HashSet<string>>();

            // The tags of the ticked links' rooms, for Doubles
            public List<Tag> Tags = new List<Tag>();

            public int Sheets;
        }

        // A room tag already in the model
        public class Tag
        {
            public long Id;

            // The view its annotation belongs to (a dependent's primary)
            public long Owner;

            // The room's placement, and the room as KeyOf has it
            public long Link;
            public string Key = string.Empty;
            public long Type;

            // Pinned, or in a group: never removed
            public bool Fixed;

            // The plans on sheets whose crop its head is in
            public List<long> SeenIn = new List<long>();
        }

        // What TAGWRITE removed, as the model has it after the commit
        public class Removal
        {
            public int Asked;
            public int Removed;
            public List<string> Failed = new List<string>();
            public string Error;
        }

        // What TAGWRITE did, as the model has it after the commit
        public class Done
        {
            public int Asked;
            public int Placed;
            public int Views;
            public List<string> Failed = new List<string>();
            public string Error;
        }

        // A linked room as a view shows it: where its tag goes, in this
        // model's coordinates
        public class Room
        {
            public long Link;
            public long Id;
            public string Number = string.Empty;
            public string Name = string.Empty;
            public double X;
            public double Y;

            public string Key
            {
                get { return KeyOf(Link, Id); }
            }
        }

        // One plan placed on a sheet, and the rooms of the ticked links it
        // shows
        public class View
        {
            public long Sheet;
            public string SheetNumber = string.Empty;
            public string SheetName = string.Empty;

            public long Id;
            public string Name = string.Empty;

            // Where its annotation lives: itself, or a dependent's primary
            public long Owner;

            // A reflected ceiling plan, not a floor plan
            public bool Ceiling;

            public List<Room> Shown = new List<Room>();

            // Rooms it shows whose tag point is outside its crop - left alone
            public int OutsideCrop;

            // What the read could not do, said under the grid and in the log
            public List<string> Notes = new List<string>();
        }

        public class Row
        {
            public View View;
            public int Shown;
            public int Tagged;
            public List<Room> Missing = new List<Room>();

            // Duplicate tags seen in this plan that Doubles would remove, by id
            public List<long> Duplicates = new List<long>();

            // Something for Tag missing or Remove duplicates to do
            public bool Fixable
            {
                get { return Missing.Count > 0 || Duplicates.Count > 0; }
            }
        }

        public static long OwnerOf(View view)
        {
            return view == null ? 0 : view.Owner != 0 ? view.Owner : view.Id;
        }

        // THE CHECK: for each view, its rooms tagged in the view its tags
        // belong to, and the rest. `tagged`: each owner view's rooms already
        // tagged there, by key. A room shown twice in one view counts once.
        public static List<Row> Check(IEnumerable<View> views, IDictionary<long, HashSet<string>> tagged)
        {
            var rows = new List<Row>();

            foreach (View view in views ?? Enumerable.Empty<View>())
            {
                if (view == null)
                    continue;

                HashSet<string> there;

                if (tagged == null || !tagged.TryGetValue(OwnerOf(view), out there))
                    there = new HashSet<string>(StringComparer.Ordinal);

                var row = new Row { View = view };
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (Room room in view.Shown ?? new List<Room>())
                {
                    if (room == null || !seen.Add(room.Key))
                        continue;

                    row.Shown++;

                    if (there.Contains(room.Key))
                        row.Tagged++;
                    else
                        row.Missing.Add(room);
                }

                rows.Add(row);
            }

            rows.Sort((a, b) =>
            {
                int bySheet = CompareNumbers(a.View.SheetNumber, b.View.SheetNumber);

                return bySheet != 0 ? bySheet : string.Compare(a.View.Name, b.View.Name, StringComparison.OrdinalIgnoreCase);
            });

            return rows;
        }

        // WHAT TO TAG: each owner view's missing rooms, ONCE - a dependent
        // and its primary, or two dependents of one plan, share one set of
        // tags, so a room missing in both is tagged once. In the rows'
        // order.
        public static Dictionary<long, List<Room>> ToTag(IEnumerable<Row> rows)
        {
            var plan = new Dictionary<long, List<Room>>();
            var seen = new Dictionary<long, HashSet<string>>();

            foreach (Row row in rows ?? Enumerable.Empty<Row>())
            {
                if (row == null || row.View == null)
                    continue;

                long owner = OwnerOf(row.View);

                HashSet<string> keys;

                if (!seen.TryGetValue(owner, out keys))
                {
                    seen[owner] = keys = new HashSet<string>(StringComparer.Ordinal);
                    plan[owner] = new List<Room>();
                }

                foreach (Room room in row.Missing)
                {
                    if (keys.Add(room.Key))
                        plan[owner].Add(room);
                }
            }

            foreach (long empty in plan.Where(p => p.Value.Count == 0).Select(p => p.Key).ToList())
                plan.Remove(empty);

            return plan;
        }

        // Tags the rows would place, all told
        public static int Missing(IEnumerable<Row> rows)
        {
            return ToTag(rows).Sum(p => p.Value.Count);
        }

        // Each owner view's rooms tagged there, from the tags read
        public static Dictionary<long, HashSet<string>> TaggedOf(IEnumerable<Tag> tags)
        {
            var tagged = new Dictionary<long, HashSet<string>>();

            foreach (Tag tag in tags ?? Enumerable.Empty<Tag>())
            {
                HashSet<string> keys;

                if (!tagged.TryGetValue(tag.Owner, out keys))
                    tagged[tag.Owner] = keys = new HashSet<string>(StringComparer.Ordinal);

                keys.Add(tag.Key);
            }

            return tagged;
        }

        // DUPLICATES: two or more tags of one room in one owner view - of one
        // tag type, or of any type when `anyType`. Which go: as few tags stay
        // as still leave EVERY plan on a sheet that showed a tag of the room
        // showing one - so a room tagged once in each of two dependents'
        // areas keeps both, and only a tag another one makes redundant goes.
        // Pinned and grouped tags stay; the oldest (lowest id) is kept first;
        // a tag no plan on a sheet shows is left alone.
        //
        // Fills each row's Duplicates with those its plan shows, and returns
        // every one.
        public static HashSet<long> Doubles(IList<Row> rows, IEnumerable<Tag> tags, bool anyType)
        {
            var gone = new HashSet<long>();

            List<Tag> seen = (tags ?? Enumerable.Empty<Tag>())
                .Where(t => t != null && t.SeenIn.Count > 0)
                .ToList();

            foreach (IGrouping<string, Tag> group in seen.GroupBy(
                         t => t.Owner + "|" + t.Key + "|" + (anyType ? string.Empty : t.Type.ToString()),
                         StringComparer.Ordinal))
            {
                List<Tag> all = group.OrderBy(t => t.Id).ToList();

                if (all.Count < 2)
                    continue;

                var kept = new HashSet<Tag>(all.Where(t => t.Fixed));
                var covered = new HashSet<long>(kept.SelectMany(t => t.SeenIn));
                var views = new HashSet<long>(all.SelectMany(t => t.SeenIn));

                // The tag showing in the most plans not yet showing one, the
                // oldest on a tie, until every plan shows one
                while (!views.IsSubsetOf(covered))
                {
                    Tag best = all
                        .Where(t => !kept.Contains(t))
                        .OrderByDescending(t => t.SeenIn.Count(v => !covered.Contains(v)))
                        .ThenBy(t => t.Id)
                        .First();

                    kept.Add(best);
                    covered.UnionWith(best.SeenIn);
                }

                foreach (Tag tag in all.Where(t => !kept.Contains(t)))
                    gone.Add(tag.Id);
            }

            foreach (Row row in rows ?? new List<Row>())
            {
                row.Duplicates = seen
                    .Where(t => gone.Contains(t.Id) && t.SeenIn.Contains(row.View.Id))
                    .Select(t => t.Id)
                    .OrderBy(id => id)
                    .ToList();
            }

            return gone;
        }

        // WHAT TO REMOVE: the rows' duplicates, each once
        public static List<long> ToRemove(IEnumerable<Row> rows)
        {
            return (rows ?? Enumerable.Empty<Row>())
                .Where(r => r != null)
                .SelectMany(r => r.Duplicates)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        // A point inside a rectangular crop, both in the crop's own
        // coordinates (TAGREAD puts the point there)
        public static bool InCrop(double x, double y, double minX, double minY, double maxX, double maxY)
        {
            const double Slack = 1e-6;

            return x >= minX - Slack && x <= maxX + Slack && y >= minY - Slack && y <= maxY + Slack;
        }

        // SHEET NUMBERS AS A PERSON READS THEM: the digits as numbers, so
        // M-2 comes before M-10
        public static int CompareNumbers(string a, string b)
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

        // ==================================================
        // The log (ATKINSMEC_roomtags_log.txt), every check and every tagging
        // ==================================================

        // Set elsewhere only by tests\roomtagtest
        private static string _logPath;

        public static string LogPath
        {
            get { return _logPath ?? LOGS.PathOf("ATKINSMEC_roomtags_log.txt"); }
            set { _logPath = value; }
        }

        public static string Report(string title, IEnumerable<string> links, Read read, List<Row> rows, Done done, Removal removal = null)
        {
            var text = new StringBuilder();

            text.Append("Atkins MEP Toolkit - room tags - ").Append(title).Append('\n');
            text.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
            text.Append('\n');
            text.Append("Linked models: ").Append(string.Join(", ", (links ?? Enumerable.Empty<string>()).ToArray())).Append('\n');

            if (done != null)
            {
                text.Append('\n');
                text.Append("TAGGED: ").Append(done.Placed).Append(" of ").Append(done.Asked)
                    .Append(" tag(s) in ").Append(done.Views).Append(" view(s)\n");

                if (!string.IsNullOrEmpty(done.Error))
                    text.Append("  ").Append(done.Error).Append('\n');

                foreach (string failed in done.Failed)
                    text.Append("  not placed: ").Append(failed).Append('\n');
            }

            if (removal != null)
            {
                text.Append('\n');
                text.Append("REMOVED: ").Append(removal.Removed).Append(" of ").Append(removal.Asked)
                    .Append(" duplicate tag(s)\n");

                if (!string.IsNullOrEmpty(removal.Error))
                    text.Append("  ").Append(removal.Error).Append('\n');

                foreach (string failed in removal.Failed)
                    text.Append("  not removed: ").Append(failed).Append('\n');
            }

            if (read != null && rows != null)
            {
                text.Append('\n');
                text.Append(rows.Count).Append(" plan(s) on ").Append(read.Sheets).Append(" sheet(s); ")
                    .Append(Missing(rows)).Append(" room tag(s) missing, ")
                    .Append(ToRemove(rows).Count).Append(" duplicate(s)\n");

                foreach (Row row in rows)
                {
                    View v = row.View;

                    text.Append('\n');
                    text.Append(v.SheetNumber).Append(" - ").Append(v.SheetName).Append(" | ").Append(v.Name)
                        .Append(v.Ceiling ? " (ceiling)" : string.Empty);

                    if (OwnerOf(v) != v.Id)
                        text.Append(" | dependent, tags in view ").Append(OwnerOf(v));

                    text.Append('\n');
                    text.Append("  rooms ").Append(row.Shown).Append(", tagged ").Append(row.Tagged)
                        .Append(", missing ").Append(row.Missing.Count);

                    if (v.OutsideCrop > 0)
                        text.Append(", outside the crop ").Append(v.OutsideCrop);

                    if (row.Duplicates.Count > 0)
                    {
                        text.Append(", duplicates ").Append(row.Duplicates.Count).Append(" (tag ids ")
                            .Append(string.Join(" ", row.Duplicates.Select(id => id.ToString()).ToArray())).Append(')');
                    }

                    text.Append('\n');

                    foreach (string note in v.Notes)
                        text.Append("  NOTE ").Append(note).Append('\n');

                    foreach (Room room in row.Missing)
                    {
                        text.Append("  missing ").Append(room.Number).Append(' ').Append(room.Name)
                            .Append("  [").Append(room.Key).Append("]\n");
                    }
                }
            }

            return text.ToString();
        }

        // ==================================================
        // What is remembered, per model (%AppData%\ATKINSMEC\roomtags.txt)
        // ==================================================

        // Set elsewhere only by tests\roomtagtest
        private static string _settingsPath;

        public static string SettingsPath
        {
            get { return _settingsPath ?? Path.Combine(LOGS.Folder, "roomtags.txt"); }
            set { _settingsPath = value; }
        }

        public static Choice Recall(string model)
        {
            if (string.IsNullOrEmpty(model))
                return null;

            try
            {
                if (!File.Exists(SettingsPath))
                    return null;

                return Parse(File.ReadAllText(SettingsPath))
                    .LastOrDefault(c => string.Equals(c.Model, model, StringComparison.Ordinal));
            }
            catch
            {
                return null;
            }
        }

        // Replaces this model's lines, keeps every other model's
        public static void Keep(Choice choice)
        {
            if (choice == null || string.IsNullOrEmpty(choice.Model))
                return;

            try
            {
                List<Choice> all = File.Exists(SettingsPath)
                    ? Parse(File.ReadAllText(SettingsPath))
                    : new List<Choice>();

                all.RemoveAll(c => string.Equals(c.Model, choice.Model, StringComparison.Ordinal));
                all.Add(choice);

                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, Compose(all));
            }
            catch
            {
                // not remembered: the next opening starts from the defaults
            }
        }

        // THE LINKS TICKED ON OPENING: the saved labels that name a link
        // here, or - nothing saved - every link with rooms
        public static HashSet<long> Ticked(IList<Link> links, Choice choice)
        {
            var ticked = new HashSet<long>();

            foreach (Link link in links ?? new List<Link>())
            {
                bool on = choice == null || choice.Links == null
                    ? link.Rooms > 0
                    : choice.Links.Contains(link.Label, StringComparer.OrdinalIgnoreCase);

                if (on)
                    ticked.Add(link.Id);
            }

            return ticked;
        }

        // What to save: null when the ticks are the default, so a link
        // added later is ticked like any other. A saved label naming no
        // link here (unloaded today) keeps its tick.
        public static List<string> Saved(IList<Link> links, ICollection<long> ticked, Choice before)
        {
            links = links ?? new List<Link>();
            ticked = ticked ?? new List<long>();

            List<string> away = before == null || before.Links == null
                ? new List<string>()
                : before.Links
                    .Where(label => !links.Any(l => string.Equals(l.Label, label, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            if (away.Count == 0 && Ticked(links, null).SetEquals(ticked))
                return null;

            return links.Where(l => ticked.Contains(l.Id)).Select(l => l.Label).Concat(away).ToList();
        }

        // The links ticked, by label - none saved: the links with rooms -
        // and the tag type, by name
        public class Choice
        {
            public string Model = string.Empty;
            public List<string> Links;
            public string TagType = string.Empty;
        }

        public static string Compose(IEnumerable<Choice> choices)
        {
            var text = new StringBuilder();

            foreach (Choice c in choices ?? Enumerable.Empty<Choice>())
            {
                if (c == null || string.IsNullOrEmpty(c.Model))
                    continue;

                text.Append("model\t").Append(Clean(c.Model)).Append('\n');

                if (c.Links != null)
                {
                    text.Append("links\n");

                    foreach (string link in c.Links)
                        text.Append("link\t").Append(Clean(link)).Append('\n');
                }

                if (!string.IsNullOrEmpty(c.TagType))
                    text.Append("type\t").Append(Clean(c.TagType)).Append('\n');
            }

            return text.ToString();
        }

        public static List<Choice> Parse(string text)
        {
            var choices = new List<Choice>();
            Choice current = null;

            foreach (string raw in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                string[] f = raw.Split('\t');
                string value = f.Length > 1 ? f[1] : string.Empty;

                switch (f[0])
                {
                    case "model":
                        current = new Choice { Model = value };
                        choices.Add(current);
                        break;

                    case "links":
                        if (current != null)
                            current.Links = current.Links ?? new List<string>();
                        break;

                    case "link":
                        if (current != null && value.Length > 0)
                            (current.Links = current.Links ?? new List<string>()).Add(value);
                        break;

                    case "type":
                        if (current != null)
                            current.TagType = value;
                        break;
                }
            }

            return choices;
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
