using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace ATKINSMEC
{
    // WHICH ROOM STANDS IN WHICH APARTMENT, when architects' models hold
    // apartments ("DRIVER/STAFF APT") and interior models the rooms inside
    // them ("Bed room", "Toilet"). At the user's request, to write them as
    // the office's Tower-1 sheet does - see VENTROWS.Apartments.
    //
    // SEVERAL APARTMENT LINKS, AND THE INTERIOR ONES ARE FOUND, NOT TICKED
    // - the user's second request: "apartments can be extracted from
    // multiple linked arch models, so when selecting arch model it has to
    // find the IND model that lies in the same floors of that arch model
    // and verified from the main model that may have different IND model
    // instances in different floors for same ARCH model or different arch
    // models". An interior model is usually placed once PER FLOOR, each
    // placement its own link instance, so the INTERIOR placements are
    // searched whether ticked on the links list or not - those the
    // keyword names, or the user ticked level by level (VENTINTERIOR) -
    // and each of their rooms is asked of every apartment link:
    // Document.GetRoomAtPoint, the room's point put through its own
    // placement's transform and back through the apartment link's
    // inverse (the move VENTFIXTURES makes for a WC).
    //
    // VERIFIED ON THIS MODEL'S LEVELS. A room joins an apartment only when
    // the two stand on the SAME storey of the main model, by the band rule
    // everything else uses (VENTLEVELMAP.StoreyOf). Being inside it in
    // space is not enough: a double-height apartment, or a placement
    // dropped onto the wrong floor, would otherwise put a room under an
    // apartment on another level - and the sheet groups by level.
    //
    // ONE MAP, FOUR USERS, and they must agree or the round trip breaks:
    // the levels tree, the export, the airflow write and the fixture count
    // all build it the same way from the same setting.
    //
    // EVERY BUILD WRITES ITSELF DOWN (VENTAPTLOG, Desktop\ATKINSMEC_
    // apartments_log.txt): what each apartments model has on each level,
    // and what each interior room was found in - so a run that groups
    // nothing says why.
    //
    // The decision and the record are in VENTAPARTMENTMAP.cs, Revit-free;
    // this half only asks the model.
    public static partial class VENTAPARTMENTS
    {
        // purpose: what the map is for, for the log - the levels list, the
        // export or the airflow write
        public static Map Build(Document doc, string purpose)
        {
            var map = new Map();

            if (doc == null || VENTSETTINGS.ApartmentLinkIds.Count == 0)
                return map;

            map.Storeys = RVTLINKSELECTION.GetHostLevels(doc);
            map.Labels = RVTLINKSELECTION.LinkLabels(doc);

            VENTAPTLOG.Start(purpose, Title(doc));

            try
            {
                Build(doc, map);
            }
            finally
            {
                VENTAPTLOG.Save();
            }

            return map;
        }

        private static void Build(Document doc, Map map)
        {
            string keyword = VENTSETTINGS.InteriorKeyword;
            bool chosen = VENTSETTINGS.InteriorChosen;

            LogSettings(map, keyword, chosen);

            // ---- the apartment links that can be read
            var sources = new List<Source>();

            foreach (long id in VENTSETTINGS.ApartmentLinkIds.OrderBy(i => i))
            {
                Source source = Read(doc, id);

                if (source != null)
                {
                    sources.Add(source);
                    map.Sources.Add(id);
                }
                else
                {
                    VENTAPTLOG.Say("Apartments model " + map.LabelOf(id) + " [" + id + "] is NOT LOADED - not read.");
                }
            }

            if (sources.Count == 0 || map.Storeys.Count == 0)
            {
                VENTAPTLOG.Say(sources.Count == 0
                    ? "No apartments model could be read - nothing to group."
                    : "This model has no levels - nothing to group.");
                return;
            }

            foreach (Source source in sources)
                LogSource(map, source);

            // ---- the INTERIOR placements, searched for rooms inside them:
            // those the keyword names, or the user ticked level by level
            // (VENTINTERIOR). An empty keyword, not chosen, is every
            // placement - the way it worked before.
            var skipped = new List<string>();
            var searched = new HashSet<long>();
            var probes = new List<Probe>();

            foreach (RevitLinkInstance link in new FilteredElementCollector(doc)
                         .OfClass(typeof(RevitLinkInstance))
                         .Cast<RevitLinkInstance>()
                         .OrderBy(l => l.Id.ToValue()))
            {
                long linkId = link.Id.ToValue();

                if (map.Sources.Contains(linkId))
                    continue;

                string label = map.LabelOf(linkId);

                // Not an interior model on any storey: not read at all
                if (chosen
                        ? !VENTSETTINGS.InteriorPicks.Values.Any(set => set != null && set.Contains(linkId))
                        : !VENTLINKFILTER.Matches(label, keyword))
                {
                    skipped.Add(label);
                    continue;
                }

                Document linked = link.GetLinkDocument();

                if (linked == null)
                {
                    VENTAPTLOG.Head("Interior placement " + label + " [" + linkId + "]");
                    VENTAPTLOG.Say("NOT LOADED - not searched.");
                    continue;
                }

                Transform toHost = link.GetTotalTransform();

                searched.Add(linkId);

                var tallies = new SortedDictionary<int, Tally>();
                int below = 0, failed = 0;

                foreach (Room room in new FilteredElementCollector(linked)
                             .OfCategory(BuiltInCategory.OST_Rooms)
                             .WhereElementIsNotElementType()
                             .Cast<Room>()
                             .Where(r => r.Area > 0))
                {
                    Seen seen;

                    try
                    {
                        seen = Place(map, sources, link, linked, toHost, room);
                    }
                    catch
                    {
                        // a room that cannot be placed stays an ordinary one
                        failed++;
                        continue;
                    }

                    if (seen.Storey < 0)
                    {
                        below++;
                        continue;
                    }

                    Tally tally;

                    if (!tallies.TryGetValue(seen.Storey, out tally))
                    {
                        tally = new Tally();
                        tallies.Add(seen.Storey, tally);
                    }

                    tally.Add(map, sources, room, seen);

                    if (seen.Fate != Fate.NotPicked && seen.HostPoints.Count > 0)
                        probes.Add(new Probe { Storey = seen.Storey, Placement = linkId, Points = seen.HostPoints });
                }

                LogPlacement(map, doc, link, label, tallies, below, failed);
            }

            VENTAPTLOG.Head("Not searched");

            VENTAPTLOG.Say(skipped.Count == 0
                ? "Every other placement was searched."
                : skipped.Count + " placement(s) are not interior models " +
                  (chosen ? "on any level (Interior models...)" : "by the keyword \"" + keyword + "\"") + ":");

            // The same label placed many times is one line with a count
            foreach (IGrouping<string, string> label in skipped
                         .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                VENTAPTLOG.Say("   " + label.Key + (label.Count() > 1 ? "   x" + label.Count() : string.Empty));
            }

            try
            {
                LogOthers(doc, map, probes, searched, keyword);
            }
            catch (Exception ex)
            {
                VENTAPTLOG.Say("The other models could not be asked: " + ex.GetType().Name + " - " + ex.Message);
            }

            VENTAPTLOG.Head("Result");
            VENTAPTLOG.Say(map.Holding.Count + " apartment(s) hold " + map.ByRoom.Count + " interior room(s).");
            VENTAPTLOG.Say(map.OtherStorey + " interior room(s) stood inside an apartment on ANOTHER level - not grouped.");
            VENTAPTLOG.Say(map.Outside + " interior room(s) stood in NO apartment - not grouped, and in no run.");

            foreach (string line in map.Describe())
                VENTAPTLOG.Say("   " + line);

            List<string> spread = map.DescribeSpread();

            if (spread.Count > 0)
            {
                VENTAPTLOG.Say(spread.Count + " apartment(s) took rooms from MORE THAN ONE interior placement - " +
                               "an architect's room drawn across several apartments?");

                foreach (string line in spread)
                    VENTAPTLOG.Say("   " + line);
            }
        }

        // One searched interior room, as the other models are asked about it
        private class Probe
        {
            public int Storey;
            public long Placement;
            public List<XYZ> Points;          // this model's coordinates, in the order asked
        }

        // WHAT EVERY OTHER LOADED MODEL HAS WHERE THE INTERIOR ROOMS STAND.
        // The first real job had apartments ticked on two architects'
        // models, and on the level exported one held 3 apartment rooms and
        // the other none - the nine apartments were somewhere else. This
        // asks every other model with rooms (not an apartments model, not
        // an interior placement, not one the keyword names) at the same
        // points, so the log says WHICH model has them. A sample of up to
        // 40 rooms per level, spread over the placements; read only.
        private static void LogOthers(Document doc, Map map, List<Probe> probes, HashSet<long> searched, string keyword)
        {
            VENTAPTLOG.Head("What OTHER models have where the interior rooms stand");

            if (probes.Count == 0)
            {
                VENTAPTLOG.Say("No interior room was searched.");
                return;
            }

            const int PerLevel = 40;

            var sample = new List<Probe>();

            foreach (IGrouping<int, Probe> level in probes.GroupBy(p => p.Storey).OrderBy(g => g.Key))
            {
                List<Probe> all = level.ToList();

                for (int i = 0; i < Math.Min(PerLevel, all.Count); i++)
                    sample.Add(all[(int)((long)i * all.Count / Math.Min(PerLevel, all.Count))]);
            }

            var asked = sample.GroupBy(p => p.Storey).ToDictionary(g => g.Key, g => g.Count());

            VENTAPTLOG.Say(sample.Count + " interior room(s) asked of every other loaded model that has rooms:");

            var nothing = new List<string>();

            foreach (RevitLinkInstance link in new FilteredElementCollector(doc)
                         .OfClass(typeof(RevitLinkInstance))
                         .Cast<RevitLinkInstance>()
                         .OrderBy(l => l.Id.ToValue()))
            {
                long id = link.Id.ToValue();

                if (map.Sources.Contains(id) || searched.Contains(id))
                    continue;

                string label = map.LabelOf(id);

                if (!string.IsNullOrWhiteSpace(keyword) && VENTLINKFILTER.Matches(label, keyword))
                    continue;

                Document linked = link.GetLinkDocument();

                if (linked == null)
                    continue;

                if (new FilteredElementCollector(linked)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .FirstElement() == null)
                    continue;

                Transform into = link.GetTotalTransform().Inverse;

                var found = new SortedDictionary<int, Dictionary<string, int>>();
                var hits = new Dictionary<int, int>();

                foreach (Probe probe in sample)
                {
                    Room room = null;

                    foreach (XYZ point in probe.Points)
                    {
                        Room at = linked.GetRoomAtPoint(into.OfPoint(point));

                        if (at != null && at.Area > 0)
                        {
                            room = at;
                            break;
                        }
                    }

                    if (room == null)
                        continue;

                    string what = Describe(room);

                    int on = VENTLEVELMAP.StoreyOf(map.Storeys, RVTLINKSELECTION.HostElevationOf(link, linked, room));

                    if (on != probe.Storey)
                        what += " on " + StoreyName(map, on);

                    Dictionary<string, int> names;

                    if (!found.TryGetValue(probe.Storey, out names))
                    {
                        names = new Dictionary<string, int>();
                        found.Add(probe.Storey, names);
                    }

                    int n;
                    names.TryGetValue(what, out n);
                    names[what] = n + 1;

                    hits.TryGetValue(probe.Storey, out n);
                    hits[probe.Storey] = n + 1;
                }

                if (found.Count == 0)
                {
                    nothing.Add(label);
                    continue;
                }

                VENTAPTLOG.Say(label + " [" + id + "]");

                foreach (KeyValuePair<int, Dictionary<string, int>> level in found)
                {
                    VENTAPTLOG.Say("   " + StoreyName(map, level.Key) + ": " + hits[level.Key] + " of " +
                                   asked[level.Key] + " inside one of its rooms - " + string.Join(", ", level.Value
                                       .OrderByDescending(v => v.Value)
                                       .ThenBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
                                       .Take(16)
                                       .Select(v => v.Value > 1 ? v.Key + " x" + v.Value : v.Key)));
                }
            }

            if (nothing.Count > 0)
                VENTAPTLOG.Say("No room at those points in: " + string.Join(", ", nothing));
        }

        private class Source
        {
            public long Id;
            public RevitLinkInstance Link;
            public Document Doc;
            public Transform Into;
            public Transform ToHost;

            // Every room of it, in this model's coordinates - read only for
            // the log, to say what a room found in nothing was NEAR
            public List<Footprint> Rooms;
        }

        // An apartments model's room as the log needs it: its box in THIS
        // model's coordinates and the storey its floor is on
        private class Footprint
        {
            public string Name = string.Empty;
            public string Number = string.Empty;
            public int Storey = -1;
            public XYZ Min;
            public XYZ Max;
        }

        private static Source Read(Document doc, long id)
        {
            var link = doc.GetElement(REVITCOMPAT.NewId(id)) as RevitLinkInstance;
            Document linked = link == null ? null : link.GetLinkDocument();

            if (linked == null)
                return null;

            Transform toHost = link.GetTotalTransform();

            return new Source { Id = id, Link = link, Doc = linked, Into = toHost.Inverse, ToHost = toHost };
        }

        // What became of one interior room
        private enum Fate
        {
            NotPicked,      // its placement is not an interior model on this storey
            Joined,         // inside an apartment on the same storey
            Elsewhere,      // inside apartments, but only on other storeys
            Outside         // inside no apartment at all
        }

        private class Seen
        {
            public int Storey = -1;
            public Fate Fate;
            public double Floor;                   // this model's internal Z
            public XYZ Point;                      // where it was first asked, this model's coordinates
            public List<XYZ> HostPoints = new List<XYZ>();   // every point asked, this model's coordinates
            public Candidate Joined;
            public List<Candidate> Candidates = new List<Candidate>();
        }

        private static Seen Place(
            Map map, List<Source> sources,
            RevitLinkInstance link, Document linked, Transform toHost, Room room)
        {
            var seen = new Seen
            {
                Floor = RVTLINKSELECTION.HostElevationOf(link, linked, room)
            };

            seen.Storey = VENTLEVELMAP.StoreyOf(map.Storeys, seen.Floor);

            // Below every level of this model: in no band, so in no
            // apartment either - the export leaves it out anyway
            if (seen.Storey < 0)
                return seen;

            // An interior model ON THIS STOREY? A placement ticked on L04
            // only gives its L04 rooms - VENTINTERIOR.Allowed
            if (!VENTINTERIOR.Allowed(
                    map.Storeys[seen.Storey].Name, link.Id.ToValue(), map.LabelOf(link.Id.ToValue()),
                    VENTSETTINGS.InteriorKeyword, VENTSETTINGS.InteriorChosen, VENTSETTINGS.InteriorPicks))
            {
                seen.Fate = Fate.NotPicked;
                return seen;
            }

            List<XYZ> points = PointsOf(room);

            seen.HostPoints = points.Select(p => toHost.OfPoint(p)).ToList();
            seen.Point = seen.HostPoints.Count == 0 ? null : seen.HostPoints[0];

            // What each apartments model says about this room - the
            // decision is Map.Settle's, verified on THIS model's levels
            foreach (Source source in sources)
            {
                Room apartment = ApartmentOf(points, toHost, source);

                if (apartment == null)
                    continue;

                Parameter name = apartment.get_Parameter(BuiltInParameter.ROOM_NAME);

                seen.Candidates.Add(new Candidate
                {
                    Source = source.Id,
                    Storey = VENTLEVELMAP.StoreyOf(
                        map.Storeys, RVTLINKSELECTION.HostElevationOf(source.Link, source.Doc, apartment)),
                    Apartment = new Apartment
                    {
                        Key = RVTLINKSELECTION.RoomKey(source.Id, apartment.Id.ToValue()),
                        Name = name == null ? string.Empty : name.AsString() ?? string.Empty,
                        Number = apartment.Number ?? string.Empty
                    }
                });
            }

            seen.Joined = map.Settle(
                link.Id.ToValue(),
                RVTLINKSELECTION.RoomKey(link.Id.ToValue(), room.Id.ToValue()),
                seen.Storey,
                seen.Candidates);

            seen.Fate = seen.Joined != null
                ? Fate.Joined
                : seen.Candidates.Count > 0 ? Fate.Elsewhere : Fate.Outside;

            return seen;
        }

        // WHERE A ROOM IS ASKED ABOUT, in its own model's coordinates, in
        // order: its own point (where it was placed, which Revit keeps
        // inside it), then the middle of its box - each first 150 mm above
        // its floor, inside the volume rather than on the boundary.
        //
        // THEN HIGHER, 1.0 m and 1.5 m up. GetRoomAtPoint answers only
        // inside a room's VOLUME, and two models' floors need not agree:
        // an architect's apartment drawn from finished floor level holds
        // nothing 150 mm above an interior model drawn from the slab 200
        // mm lower. Higher points find it; the same-storey rule
        // (Map.Settle) still decides whether it counts, so a point that
        // reaches the floor above cannot put a room under it.
        private static List<XYZ> PointsOf(Room room)
        {
            const double Low = 0.5;                      // 152 mm
            double[] higher = { 1.0 / 0.3048, 1.5 / 0.3048 };

            var plan = new List<XYZ>();

            var location = room.Location as LocationPoint;
            BoundingBoxXYZ box = room.get_BoundingBox(null);

            if (location != null)
                plan.Add(location.Point);

            if (box != null)
            {
                plan.Add(new XYZ(
                    (box.Min.X + box.Max.X) / 2,
                    (box.Min.Y + box.Max.Y) / 2,
                    box.Min.Z));
            }

            var points = new List<XYZ>();

            // Exactly what was always asked, first
            foreach (XYZ p in plan)
                points.Add(new XYZ(p.X, p.Y, p.Z + Low));

            double floor = box != null ? box.Min.Z : location != null ? location.Point.Z : 0;

            foreach (double up in higher)
            {
                foreach (XYZ p in plan)
                    points.Add(new XYZ(p.X, p.Y, floor + up));
            }

            return points;
        }

        private static Room ApartmentOf(List<XYZ> points, Transform toHost, Source source)
        {
            foreach (XYZ point in points)
            {
                Room found = source.Doc.GetRoomAtPoint(source.Into.OfPoint(toHost.OfPoint(point)));

                if (found != null && found.Area > 0)
                    return found;
            }

            return null;
        }

        // ==================================================
        // The log
        // ==================================================

        private static string Title(Document doc)
        {
            try
            {
                return "Model: " + doc.Title;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void LogSettings(Map map, string keyword, bool chosen)
        {
            VENTAPTLOG.Head("Set-up");

            VENTAPTLOG.Say("Apartments from: " + string.Join(", ",
                VENTSETTINGS.ApartmentLinkIds.OrderBy(i => i).Select(i => map.LabelOf(i) + " [" + i + "]")));

            VENTAPTLOG.Say("Interior models: keyword \"" + (keyword ?? string.Empty) + "\"" +
                           (string.IsNullOrWhiteSpace(keyword) ? " (every placement)" : string.Empty) +
                           (chosen ? " - but CHOSEN level by level, the keyword no longer decides:" : " - not chosen by hand."));

            if (chosen)
            {
                foreach (KeyValuePair<string, HashSet<long>> pick in VENTSETTINGS.InteriorPicks
                             .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    VENTAPTLOG.Say("   " + pick.Key + ": " + (pick.Value == null || pick.Value.Count == 0
                        ? "none"
                        : string.Join(", ", pick.Value.OrderBy(i => i).Select(i => map.LabelOf(i) + " [" + i + "]"))));
                }
            }

            VENTAPTLOG.Head("Levels of this model (internal height)");

            for (int i = 0; i < map.Storeys.Count; i++)
                VENTAPTLOG.Say("   " + map.Storeys[i].Name + "   " + VENTAPTLOG.M(map.Storeys[i].Elevation));
        }

        // Every room of an apartments model, level by level - so a level
        // with only corridors and stairs on it shows up as exactly that
        private static void LogSource(Map map, Source source)
        {
            VENTAPTLOG.Head("Apartments model " + map.LabelOf(source.Id) + " [" + source.Id + "]");
            VENTAPTLOG.Say("Origin in this model " + Where(source.ToHost.Origin) + Nested(source.Link));

            source.Rooms = new List<Footprint>();

            try
            {
                foreach (Room room in new FilteredElementCollector(source.Doc)
                             .OfCategory(BuiltInCategory.OST_Rooms)
                             .WhereElementIsNotElementType()
                             .Cast<Room>()
                             .Where(r => r.Area > 0))
                {
                    Parameter name = room.get_Parameter(BuiltInParameter.ROOM_NAME);

                    var print = new Footprint
                    {
                        Name = name == null ? string.Empty : name.AsString() ?? string.Empty,
                        Number = room.Number ?? string.Empty,
                        Storey = VENTLEVELMAP.StoreyOf(
                            map.Storeys, RVTLINKSELECTION.HostElevationOf(source.Link, source.Doc, room))
                    };

                    BoundingBoxXYZ box = room.get_BoundingBox(null);

                    if (box != null)
                        Box(box, source.ToHost, out print.Min, out print.Max);

                    source.Rooms.Add(print);
                }
            }
            catch (Exception ex)
            {
                VENTAPTLOG.Say("Its rooms could not all be read: " + ex.GetType().Name + " - " + ex.Message);
            }

            VENTAPTLOG.Say(source.Rooms.Count + " room(s). By this model's levels:");

            foreach (IGrouping<int, Footprint> storey in source.Rooms.GroupBy(r => r.Storey).OrderBy(g => g.Key))
            {
                string names = string.Join(", ", storey
                    .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(14)
                    .Select(g => g.Count() > 1 ? g.Key + " x" + g.Count() : g.Key));

                VENTAPTLOG.Say("   " + StoreyName(map, storey.Key) + ": " + storey.Count() + " - " + names);
            }
        }

        // What one placement's rooms were found in, level by level
        private class Tally
        {
            public int Rooms, Joined, Elsewhere, Outside;
            public bool Picked = true;
            public readonly List<string> Examples = new List<string>();
            public readonly Dictionary<string, int> Apartments = new Dictionary<string, int>();

            public void Add(Map map, List<Source> sources, Room room, Seen seen)
            {
                Rooms++;

                string it = Describe(room);

                switch (seen.Fate)
                {
                    case Fate.NotPicked:
                        Picked = false;
                        return;

                    case Fate.Joined:
                        Joined++;

                        string apt = seen.Joined.Apartment.Name + " (" + seen.Joined.Apartment.Number + ")";
                        int n;
                        Apartments.TryGetValue(apt, out n);
                        Apartments[apt] = n + 1;
                        return;

                    case Fate.Elsewhere:
                        Elsewhere++;

                        if (Examples.Count < 4)
                        {
                            Examples.Add(it + " floor " + VENTAPTLOG.M(seen.Floor) + " - inside " + string.Join(", ",
                                seen.Candidates.Select(c => c.Apartment.Name + " (" + c.Apartment.Number + ") on " +
                                                            StoreyName(map, c.Storey))));
                        }

                        return;

                    default:
                        Outside++;

                        if (Examples.Count < 4)
                            Examples.Add(it + " floor " + VENTAPTLOG.M(seen.Floor) + " - in NO room; " + Near(map, sources, seen));

                        return;
                }
            }
        }

        private static string Describe(Room room)
        {
            Parameter name = room.get_Parameter(BuiltInParameter.ROOM_NAME);

            return (name == null ? string.Empty : name.AsString() ?? string.Empty) + " (" + (room.Number ?? string.Empty) + ")";
        }

        // FOR A ROOM FOUND IN NOTHING: does an apartments model have a room
        // on the SAME level whose box covers it in plan? Then the two
        // models' heights do not overlap there. If not, that model simply
        // has no room on that level where it stands. (A box, not the room's
        // outline: an L-shaped room's box covers more than the room.)
        private static string Near(Map map, List<Source> sources, Seen seen)
        {
            if (seen.Point == null)
                return "it has no point to ask at";

            var said = new List<string>();

            foreach (Source source in sources)
            {
                Footprint over = (source.Rooms ?? new List<Footprint>())
                    .Where(r => r.Min != null && r.Storey == seen.Storey &&
                                seen.Point.X >= r.Min.X && seen.Point.X <= r.Max.X &&
                                seen.Point.Y >= r.Min.Y && seen.Point.Y <= r.Max.Y)
                    .OrderBy(r => (r.Max.X - r.Min.X) * (r.Max.Y - r.Min.Y))
                    .FirstOrDefault();

                said.Add(over == null
                    ? map.LabelOf(source.Id) + " has no room on this level there"
                    : "the box of " + over.Name + " (" + over.Number + ") of " + map.LabelOf(source.Id) +
                      " covers it in plan, spanning " + VENTAPTLOG.M(over.Min.Z) + " to " + VENTAPTLOG.M(over.Max.Z));
            }

            return string.Join("; ", said);
        }

        private static void LogPlacement(
            Map map, Document doc, RevitLinkInstance link, string label,
            SortedDictionary<int, Tally> tallies, int below, int failed)
        {
            VENTAPTLOG.Head("Interior placement " + label + " [" + link.Id.ToValue() + "]");

            try
            {
                VENTAPTLOG.Say("Origin in this model " + Where(link.GetTotalTransform().Origin) + Nested(link));
            }
            catch
            {
            }

            if (tallies.Count == 0)
                VENTAPTLOG.Say("No room of it is on a level of this model.");

            foreach (KeyValuePair<int, Tally> at in tallies)
            {
                Tally t = at.Value;

                string head = "   " + StoreyName(map, at.Key) + ": " + t.Rooms + " room(s) - ";

                if (!t.Picked)
                {
                    VENTAPTLOG.Say(head + "not an interior model on this level, not searched");
                    continue;
                }

                VENTAPTLOG.Say(head + t.Joined + " in an apartment, " + t.Elsewhere + " in one on another level, " +
                               t.Outside + " in none");

                if (t.Apartments.Count > 0)
                {
                    VENTAPTLOG.Say("      into " + string.Join(", ", t.Apartments
                        .OrderByDescending(a => a.Value)
                        .Take(8)
                        .Select(a => a.Key + " x" + a.Value)));
                }

                foreach (string example in t.Examples)
                    VENTAPTLOG.Say("      e.g. " + example);
            }

            if (below > 0)
                VENTAPTLOG.Say("   " + below + " room(s) below every level of this model - in no band.");

            if (failed > 0)
                VENTAPTLOG.Say("   " + failed + " room(s) could not be placed at all.");
        }

        private static string StoreyName(Map map, int storey)
        {
            return storey >= 0 && storey < map.Storeys.Count ? map.Storeys[storey].Name : "below every level";
        }

        private static string Where(XYZ p)
        {
            return p == null
                ? "?"
                : "(" + VENTAPTLOG.M(p.X) + ", " + VENTAPTLOG.M(p.Y) + ", " + VENTAPTLOG.M(p.Z) + ")";
        }

        private static string Nested(RevitLinkInstance link)
        {
            try
            {
                var type = link.Document.GetElement(link.GetTypeId()) as RevitLinkType;

                return type != null && type.IsNestedLink ? " - NESTED inside another link" : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // A box through a transform: every corner, since a rotated link
        // turns an axis-aligned box into one that is not
        private static void Box(BoundingBoxXYZ box, Transform t, out XYZ min, out XYZ max)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
            double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;

            foreach (double x in new[] { box.Min.X, box.Max.X })
            foreach (double y in new[] { box.Min.Y, box.Max.Y })
            foreach (double z in new[] { box.Min.Z, box.Max.Z })
            {
                XYZ p = t.OfPoint(new XYZ(x, y, z));

                x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z);
                x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
            }

            min = new XYZ(x0, y0, z0);
            max = new XYZ(x1, y1, z1);
        }
    }
}
