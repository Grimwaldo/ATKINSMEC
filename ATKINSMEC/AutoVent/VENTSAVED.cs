using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // HOW EACH MODEL WAS LAST SET UP IN AUTOVENT, remembered between
    // sessions - at the user's request, after it was pointed out that
    // AUTOVENT forgot all of it:
    //
    //   - the fresh / extract system-type ticks went back to the name
    //     guess every time the window opened, so a system ticked by hand
    //     (ATK_ME_OA Supply) was silently guessed again the next time -
    //     the very miss the ticks exist to stop;
    //   - the endcap reach, the fixture choices, the Apartments ticks and
    //     the links ticked for rooms lasted only until Revit closed;
    //   - and they were STATICS, so within one session they leaked into
    //     the next model opened: link ids from one project, applied to
    //     another, can count no fixtures at all.
    //
    // ONE BLOCK PER MODEL, keyed by where the model lives (VENTWindow.
    // ModelKey: the ACC model's GUID, a workshared model's central, or the
    // file), so every local copy of a central shares one set-up and every
    // Revit version reads the same one. A model never set up gets the
    // DEFAULTS - not whatever the last model left behind.
    //
    // Deliberately REVIT-FREE, so the file round trip is tested with no
    // model open - tests\ventsettingstest.
    public static class VENTSAVED
    {
        // A link placement, remembered by its element id AND its label
        // (RVTLINKSELECTION.LinkLabels). The id is what it is in this
        // model now; the label is what survives the link being removed
        // and linked again under a new id - see Resolve.
        public class LinkRef
        {
            public long Id;
            public string Label = string.Empty;
        }

        public class Model
        {
            public string Key = string.Empty;
            public string Title = string.Empty;
            public string When = string.Empty;

            // The system types ticked as fresh / extract, by NAME - ids
            // are per model and the names are what a person ticked. NULL
            // MEANS "FOLLOW THE NAMES": nothing was chosen, or the ticks
            // were exactly the name guess, so a system type added or
            // renamed later is guessed like any other rather than frozen
            // out by an old list.
            public List<string> Fresh;
            public List<string> Extract;

            public double ReachMm = VENTSETTINGS.DefaultEndcapReachMm;

            // Which terminal families are endcaps; null = the default
            public string EndcapNames;

            // ...and bellmouths; null = the default, "" = none
            public string BellmouthNames;

            public bool CountFixtures = true;

            // The links ticked to read rooms from
            public List<LinkRef> RoomLinks = new List<LinkRef>();

            public List<LinkRef> Apartments = new List<LinkRef>();

            // Empty = the defaults, exactly as VENTSETTINGS has them
            public List<LinkRef> FixtureSources = new List<LinkRef>();
            public List<string> FixtureFamilies = new List<string>();

            // The interior models (VENTINTERIOR). The keyword: null = the
            // default ("IND"); "" = every placement. The picks: NULL =
            // follow the keyword; an empty list = chose none.
            public string InteriorKeyword;
            public List<InteriorPick> InteriorPicks;

            // The rooms arranged by hand for the sheet (VENTARRANGE): room
            // keys taken out of their apartment, and each container's
            // chosen order. Keys carry a link id, so every link they name
            // is kept with its label too (ArrangeLinks) - a link loaded
            // again under a new id is found by its label (Resolve).
            public List<string> Ungrouped = new List<string>();
            public List<KeyValuePair<string, List<string>>> Orders = new List<KeyValuePair<string, List<string>>>();
            public List<KeyValuePair<string, string>> Names = new List<KeyValuePair<string, string>>();
            public List<LinkRef> ArrangeLinks = new List<LinkRef>();
        }

        // One placement ticked as an interior model on one storey
        public class InteriorPick
        {
            public string Storey = string.Empty;
            public LinkRef Link = new LinkRef();
        }

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC", "autovent_settings.txt");
            }
        }

        // ==================================================
        // One model
        // ==================================================

        // The model's last set-up, or null when it has never had one.
        // A file that cannot be read is the same as no file: the window
        // opens on the defaults rather than failing to open.
        public static Model Recall(string key, string path = null)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            try
            {
                return Read(path ?? DefaultPath)
                    .FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return null;
            }
        }

        // Writes this model's block, leaving every other model's as it
        // was. Read, replace, write - so two Revit sessions saving
        // different models do not throw each other's away (the same
        // model in two at once: the last one wins). False if it could not
        // be written; a set-up that was not remembered must never stop a
        // run, so nothing here throws.
        public static bool Remember(Model model, string path = null)
        {
            if (model == null || string.IsNullOrEmpty(model.Key))
                return false;

            path = path ?? DefaultPath;

            try
            {
                List<Model> all = File.Exists(path) ? Read(path) : new List<Model>();

                all.RemoveAll(m => string.Equals(m.Key, model.Key, StringComparison.OrdinalIgnoreCase));
                all.Add(model);

                Directory.CreateDirectory(Path.GetDirectoryName(path));

                // Whole or not at all: a half-written file would lose
                // every model's set-up, not just this one's
                string temp = path + ".tmp";

                File.WriteAllText(temp, Compose(all), new UTF8Encoding(false));
                File.Copy(temp, path, true);
                File.Delete(temp);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ==================================================
        // The links
        // ==================================================

        // WHICH OF THE LINKS NOW IN THE MODEL a remembered list means.
        //
        // The ID FIRST, across the whole list: it is exact while the link
        // stays loaded. Only then the LABEL, and only a label that names
        // exactly ONE link not already taken by an id - a link removed
        // and linked again comes back with a new id and the same label,
        // but a label shared by two placements says nothing about which
        // one was meant, and guessing there would tick the wrong tower.
        // The order is WORKSETPICK.Find's, for the same reason: match by
        // name first and a link that was renamed is matched to whatever
        // now carries its old name.
        public static HashSet<long> Resolve(IEnumerable<LinkRef> saved, IEnumerable<LinkRef> present)
        {
            var found = new HashSet<long>();

            List<LinkRef> now = (present ?? Enumerable.Empty<LinkRef>()).Where(p => p != null).ToList();
            var ids = new HashSet<long>(now.Select(p => p.Id));

            var unmatched = new List<LinkRef>();

            foreach (LinkRef link in saved ?? Enumerable.Empty<LinkRef>())
            {
                if (link == null)
                    continue;

                if (ids.Contains(link.Id))
                    found.Add(link.Id);
                else
                    unmatched.Add(link);
            }

            foreach (LinkRef link in unmatched)
            {
                string label = (link.Label ?? string.Empty).Trim();

                if (label.Length == 0)
                    continue;

                List<LinkRef> same = now
                    .Where(p => string.Equals((p.Label ?? string.Empty).Trim(), label,
                                              StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (same.Count == 1 && !found.Contains(same[0].Id))
                    found.Add(same[0].Id);
            }

            return found;
        }

        // A LINK THAT IS NOT LOADED RIGHT NOW KEEPS WHAT WAS CHOSEN FOR
        // IT: somebody unloading a heavy link for an afternoon should
        // find it ticked again when it comes back. Adds to `now` every
        // remembered link that names no link loaded at the moment.
        public static void KeepUnloaded(
            List<LinkRef> now, IEnumerable<LinkRef> before, IList<LinkRef> loaded)
        {
            foreach (LinkRef link in before ?? Enumerable.Empty<LinkRef>())
            {
                if (link == null || link.Id == 0)
                    continue;

                bool here = Resolve(new[] { link }, loaded).Count > 0;

                if (!here && !now.Any(n => n.Id == link.Id))
                    now.Add(link);
            }
        }

        // THE TICKS EXACTLY AS THE NAMES WOULD HAVE THEM ARE NOT A CHOICE,
        // and are remembered as "follow the names" (null) - so a system
        // type added to the model later is guessed like any other rather
        // than frozen out by an old list. Otherwise the ticked names,
        // which may be none at all. The rule the Fixtures dialog already
        // follows.
        public static List<string> ChoiceOf(
            IEnumerable<KeyValuePair<string, bool>> ticks, Func<string, bool> names)
        {
            List<KeyValuePair<string, bool>> all =
                (ticks ?? Enumerable.Empty<KeyValuePair<string, bool>>()).ToList();

            if (all.All(t => t.Value == names(t.Key)))
                return null;

            return all.Where(t => t.Value).Select(t => t.Key).ToList();
        }

        // ==================================================
        // The file
        // ==================================================
        //
        // Text, one line per value, tab between fields - the shape of
        // NWCSETTINGS. Every line after "model" belongs to that model.
        // Numbers in the INVARIANT culture: a de-DE machine would
        // otherwise write "1500,5" and read it back as something else.

        public static List<Model> Read(string path)
        {
            return File.Exists(path)
                ? Parse(File.ReadAllText(path))
                : new List<Model>();
        }

        public static string Compose(IEnumerable<Model> models)
        {
            var text = new StringBuilder();

            text.Append("# ATKINSMEC AUTOVENT - how each model was last set up. Written by the\n");
            text.Append("# add-in; every line after \"model\" belongs to that model.\n");

            foreach (Model m in models ?? Enumerable.Empty<Model>())
            {
                if (m == null || string.IsNullOrEmpty(m.Key))
                    continue;

                text.Append('\n');

                Line(text, "model", m.Key);
                Line(text, "title", m.Title);
                Line(text, "saved", m.When);

                if (m.Fresh != null)
                {
                    Line(text, "freshchosen");

                    foreach (string name in m.Fresh)
                        Line(text, "fresh", name);
                }

                if (m.Extract != null)
                {
                    Line(text, "extractchosen");

                    foreach (string name in m.Extract)
                        Line(text, "extract", name);
                }

                Line(text, "reach", m.ReachMm.ToString("R", CultureInfo.InvariantCulture));

                if (m.EndcapNames != null)
                    Line(text, "endcapnames", m.EndcapNames);

                if (m.BellmouthNames != null)
                    Line(text, "bellmouthnames", m.BellmouthNames);

                Line(text, "fixtures", m.CountFixtures ? "on" : "off");

                foreach (LinkRef link in m.RoomLinks)
                    Line(text, "roomlink", link.Id.ToString(CultureInfo.InvariantCulture), link.Label);

                foreach (LinkRef link in m.Apartments)
                    Line(text, "apartments", link.Id.ToString(CultureInfo.InvariantCulture), link.Label);

                foreach (LinkRef link in m.FixtureSources)
                    Line(text, "fixturesource", link.Id.ToString(CultureInfo.InvariantCulture), link.Label);

                foreach (string name in m.FixtureFamilies)
                    Line(text, "fixturefamily", name);

                if (m.InteriorKeyword != null)
                    Line(text, "interiorkeyword", m.InteriorKeyword);

                if (m.InteriorPicks != null)
                {
                    Line(text, "interiorchosen");

                    foreach (InteriorPick pick in m.InteriorPicks)
                    {
                        Line(text, "interior", pick.Storey,
                             pick.Link.Id.ToString(CultureInfo.InvariantCulture), pick.Link.Label);
                    }
                }

                foreach (string key in m.Ungrouped)
                    Line(text, "ungrouped", key);

                // One line per container: its name, then its keys in order
                foreach (KeyValuePair<string, List<string>> order in m.Orders)
                    Line(text, "order", new[] { order.Key }.Concat(order.Value ?? new List<string>()).ToArray());

                foreach (KeyValuePair<string, string> name in m.Names)
                    Line(text, "rename", name.Key, name.Value);

                foreach (LinkRef link in m.ArrangeLinks)
                    Line(text, "arrangelink", link.Id.ToString(CultureInfo.InvariantCulture), link.Label);
            }

            return text.ToString();
        }

        public static List<Model> Parse(string text)
        {
            var models = new List<Model>();

            Model m = null;

            foreach (string raw in (text ?? string.Empty).Split('\n'))
            {
                string line = raw.TrimEnd('\r');

                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] f = line.Split('\t');

                string tag = f[0].Trim().ToLowerInvariant();
                string a = f.Length > 1 ? f[1] : string.Empty;
                string b = f.Length > 2 ? f[2] : string.Empty;
                string c = f.Length > 3 ? f[3] : string.Empty;

                if (tag == "model")
                {
                    m = a.Length == 0 ? null : new Model { Key = a };

                    if (m != null)
                        models.Add(m);

                    continue;
                }

                // A line before any model, or after one with no key, has
                // nothing to belong to
                if (m == null)
                    continue;

                switch (tag)
                {
                    case "title":
                        m.Title = a;
                        break;

                    case "saved":
                        m.When = a;
                        break;

                    case "freshchosen":
                        m.Fresh = m.Fresh ?? new List<string>();
                        break;

                    case "fresh":
                        (m.Fresh = m.Fresh ?? new List<string>()).Add(a);
                        break;

                    case "extractchosen":
                        m.Extract = m.Extract ?? new List<string>();
                        break;

                    case "extract":
                        (m.Extract = m.Extract ?? new List<string>()).Add(a);
                        break;

                    case "reach":
                        double mm;

                        // Zero, negative or nonsense is the default, as
                        // the window treats a box typed that way
                        m.ReachMm = double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out mm) && mm > 0
                            ? mm
                            : VENTSETTINGS.DefaultEndcapReachMm;
                        break;

                    case "endcapnames":
                        m.EndcapNames = a.Trim().Length == 0 ? null : a.Trim();
                        break;

                    // Kept even when empty: that is "no bellmouths"
                    case "bellmouthnames":
                        m.BellmouthNames = a.Trim();
                        break;

                    case "fixtures":
                        m.CountFixtures = !string.Equals(a.Trim(), "off", StringComparison.OrdinalIgnoreCase);
                        break;

                    case "roomlink":
                        Add(m.RoomLinks, a, b);
                        break;

                    case "apartments":
                        Add(m.Apartments, a, b);
                        break;

                    case "fixturesource":
                        Add(m.FixtureSources, a, b);
                        break;

                    case "fixturefamily":
                        if (a.Trim().Length > 0)
                            m.FixtureFamilies.Add(a.Trim());
                        break;

                    case "interiorkeyword":
                        m.InteriorKeyword = a;
                        break;

                    case "interiorchosen":
                        m.InteriorPicks = m.InteriorPicks ?? new List<InteriorPick>();
                        break;

                    case "interior":
                        long pickId;

                        if (a.Length > 0 &&
                            long.TryParse(b.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out pickId))
                        {
                            (m.InteriorPicks = m.InteriorPicks ?? new List<InteriorPick>()).Add(new InteriorPick
                            {
                                Storey = a,
                                Link = new LinkRef { Id = pickId, Label = c }
                            });
                        }

                        break;

                    case "ungrouped":
                        if (a.Trim().Length > 0)
                            m.Ungrouped.Add(a.Trim());
                        break;

                    case "order":
                        // A container with no keys orders nothing
                        if (a.Trim().Length > 0 && f.Length > 2)
                        {
                            m.Orders.Add(new KeyValuePair<string, List<string>>(
                                a, f.Skip(2).Select(k => k.Trim()).Where(k => k.Length > 0).ToList()));
                        }

                        break;

                    case "arrangelink":
                        Add(m.ArrangeLinks, a, b);
                        break;

                    case "rename":
                        if (a.Trim().Length > 0 && b.Trim().Length > 0)
                            m.Names.Add(new KeyValuePair<string, string>(a.Trim(), b.Trim()));
                        break;

                    // Anything else is from a later version: kept out
                    // rather than refused, so an older add-in still reads
                    // what it understands
                }
            }

            return models;
        }

        private static void Add(List<LinkRef> into, string id, string label)
        {
            long value;

            if (long.TryParse(id.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                into.Add(new LinkRef { Id = value, Label = label ?? string.Empty });
        }

        // A tab or a line break inside a value would split it into fields
        // or lines of its own; a Revit name cannot hold either, but a
        // model's title or a file path just might
        private static void Line(StringBuilder text, string tag, params string[] values)
        {
            text.Append(tag);

            foreach (string value in values)
            {
                text.Append('\t').Append(
                    (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '));
            }

            text.Append('\n');
        }
    }
}
