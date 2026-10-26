using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace ATKINSMEC
{
    // PLUMBING FIXTURES COUNTED INTO THE ROOMS THEY STAND IN, for the
    // sheet's "No. of Units" (Table 6-4: so many litres per second per
    // water closet or urinal). At the user's request - fixtures from the
    // link, occupants not.
    //
    // The fixtures may be in a different model from the rooms - an
    // architectural model's rooms and a plumbing model's WCs - so each
    // fixture is put through its own model's transform into this model,
    // and back through each room link's inverse, and the room link
    // itself is asked which room is there (Document.GetRoomAtPoint, which
    // is Revit's own answer and uses its own index - not a loop over
    // every room).
    public static class VENTFIXTURES
    {
        private class Source
        {
            public long Id;              // 0 = this model
            public Document Doc;
            public Transform ToHost;
        }

        private static List<Source> Sources(Document doc, IEnumerable<long> ids)
        {
            var sources = new List<Source>();

            foreach (long id in ids.Distinct())
            {
                if (id == 0)
                {
                    sources.Add(new Source { Id = 0, Doc = doc, ToHost = Transform.Identity });
                    continue;
                }

                var link = doc.GetElement(REVITCOMPAT.NewId(id)) as RevitLinkInstance;
                Document linked = link == null ? null : link.GetLinkDocument();

                if (linked != null)
                    sources.Add(new Source { Id = id, Doc = linked, ToHost = link.GetTotalTransform() });
            }

            return sources;
        }

        // Where the fixtures are read from: what the window chose, or the
        // room links when nothing was chosen
        public static List<long> SourcesFor(IEnumerable<long> roomLinks)
        {
            return VENTSETTINGS.FixtureSources.Count > 0
                ? VENTSETTINGS.FixtureSources.ToList()
                : roomLinks.ToList();
        }

        // A MODEL TICKED IN THE DIALOG IS EVERY PLACEMENT OF IT (one row
        // per model since 2026-10-17), a placement made after OK included:
        // the dialog stores the placements it saw, and the rest of the same
        // link type are added here. Only for a choice made in the dialog -
        // the default stays the room links themselves.
        private static List<long> EveryPlacement(Document doc, List<long> ids)
        {
            var types = new HashSet<long>();

            foreach (long id in ids)
            {
                if (id == 0)
                    continue;

                var link = doc.GetElement(REVITCOMPAT.NewId(id)) as RevitLinkInstance;

                if (link != null)
                    types.Add(link.GetTypeId().ToValue());
            }

            var all = new List<long>(ids);

            if (types.Count == 0)
                return all;

            foreach (RevitLinkInstance link in new FilteredElementCollector(doc)
                         .OfClass(typeof(RevitLinkInstance))
                         .Cast<RevitLinkInstance>())
            {
                long id = link.Id.ToValue();

                if (types.Contains(link.GetTypeId().ToValue()) && !all.Contains(id))
                    all.Add(id);
            }

            return all;
        }

        private static string FamilyOf(FamilyInstance fixture)
        {
            try
            {
                return fixture.Symbol == null ? string.Empty : (fixture.Symbol.FamilyName ?? string.Empty);
            }
            catch
            {
                return string.Empty;
            }
        }

        // The fixture families in those models, and how many of each -
        // what the dialog lists. Pre-ticked as the export would count.
        public static List<VENTSETTINGS.FixtureFamily> Families(Document doc, IEnumerable<long> sourceIds)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (Source source in Sources(doc, sourceIds))
            {
                foreach (FamilyInstance fixture in new FilteredElementCollector(source.Doc)
                             .OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                             .WhereElementIsNotElementType()
                             .OfType<FamilyInstance>())
                {
                    string family = FamilyOf(fixture);

                    if (family.Length == 0)
                        continue;

                    int n;
                    counts.TryGetValue(family, out n);
                    counts[family] = n + 1;
                }
            }

            return counts
                .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .Select(c => new VENTSETTINGS.FixtureFamily
                {
                    Name = c.Key,
                    Count = c.Value,
                    Counted = VENTSETTINGS.CountsFamily(c.Key)
                })
                .ToList();
        }

        public class Tally
        {
            // RVTLINKSELECTION.RoomKey -> how many counted fixtures stand in it
            public Dictionary<string, int> ByRoom = new Dictionary<string, int>();

            public int Counted;          // put in a room
            public int Outside;          // counted kind, but in no room
            public int Ignored;          // a family that does not count
            public int Models;           // models, not placements
            public int Placements;
            public List<string> ModelNames = new List<string>();
            public List<string> Families = new List<string>();
        }

        // holding: apartments with rooms inside (VENTAPARTMENTS) - a WC
        // inside one belongs to the toilet inside it, which is the room
        // with a row, so the apartment is never the answer
        // roomDocsIn: the links whose rooms a fixture may stand in - the
        // ticked ones and every interior placement found holding an
        // apartment's rooms. The FIXTURES themselves still come from the
        // ticked links by default: an interior model drawing its own WCs
        // as well as the architect's would count every one twice.
        public static Tally Count(
            Document doc, IEnumerable<long> roomLinkIds,
            ICollection<string> holding = null, IEnumerable<long> roomDocsIn = null)
        {
            var tally = new Tally();

            List<long> roomLinks = roomLinkIds.ToList();

            // The room models, and the way back into each one
            var roomDocs = new List<KeyValuePair<long, KeyValuePair<Document, Transform>>>();

            foreach (long id in (roomDocsIn ?? roomLinks).Distinct())
            {
                var link = doc.GetElement(REVITCOMPAT.NewId(id)) as RevitLinkInstance;
                Document linked = link == null ? null : link.GetLinkDocument();

                if (linked != null)
                {
                    roomDocs.Add(new KeyValuePair<long, KeyValuePair<Document, Transform>>(
                        id, new KeyValuePair<Document, Transform>(linked, link.GetTotalTransform().Inverse)));
                }
            }

            List<long> chosen = SourcesFor(roomLinks);

            if (VENTSETTINGS.FixtureSources.Count > 0)
                chosen = EveryPlacement(doc, chosen);

            List<Source> sources = Sources(doc, chosen);

            tally.Placements = sources.Count;

            foreach (Source source in sources)
            {
                string name;

                try
                {
                    name = source.Id == 0 ? "this model" : source.Doc.Title;
                }
                catch
                {
                    name = "link " + source.Id;
                }

                if (!tally.ModelNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    tally.ModelNames.Add(name);
            }

            tally.Models = tally.ModelNames.Count;

            var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Source source in sources)
            {
                foreach (FamilyInstance fixture in new FilteredElementCollector(source.Doc)
                             .OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                             .WhereElementIsNotElementType()
                             .OfType<FamilyInstance>())
                {
                    string family = FamilyOf(fixture);

                    if (!VENTSETTINGS.CountsFamily(family))
                    {
                        tally.Ignored++;
                        continue;
                    }

                    string key = RoomOf(fixture, source, roomDocs, holding);

                    if (key == null)
                    {
                        tally.Outside++;
                        continue;
                    }

                    int n;
                    tally.ByRoom.TryGetValue(key, out n);
                    tally.ByRoom[key] = n + 1;

                    tally.Counted++;
                    families.Add(family);
                }
            }

            tally.Families = families.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

            return tally;
        }

        // WHICH ROOM A FIXTURE STANDS IN, tried at three points in turn:
        //
        //   1. the middle of its box - a wall-hung WC's own origin is ON
        //      the wall face, which is the room's boundary, where the
        //      answer could as well be the room behind the wall;
        //   2. its origin moved 300 mm the way it faces - a hosted
        //      family faces out of its wall, into the room;
        //   3. its origin.
        //
        // Each 150 mm above the bottom of the fixture, so the point is
        // inside the room's volume rather than on its floor.
        private static string RoomOf(
            FamilyInstance fixture, Source source,
            List<KeyValuePair<long, KeyValuePair<Document, Transform>>> roomDocs,
            ICollection<string> holding)
        {
            var points = new List<XYZ>();

            const double Up = 0.5;       // feet, 150 mm
            const double Out = 300 / 304.8;

            try
            {
                BoundingBoxXYZ box = fixture.get_BoundingBox(null);

                if (box != null)
                {
                    points.Add(new XYZ(
                        (box.Min.X + box.Max.X) / 2,
                        (box.Min.Y + box.Max.Y) / 2,
                        box.Min.Z + Up));
                }
            }
            catch
            {
            }

            var location = fixture.Location as LocationPoint;

            if (location != null)
            {
                XYZ origin = location.Point + new XYZ(0, 0, Up);

                try
                {
                    XYZ facing = fixture.FacingOrientation;

                    if (facing != null && facing.GetLength() > 0.5)
                        points.Add(origin + new XYZ(facing.X, facing.Y, 0).Normalize() * Out);
                }
                catch
                {
                }

                points.Add(origin);
            }

            foreach (XYZ point in points)
            {
                XYZ inHost = source.ToHost.OfPoint(point);

                foreach (KeyValuePair<long, KeyValuePair<Document, Transform>> room in roomDocs)
                {
                    try
                    {
                        Room found = room.Value.Key.GetRoomAtPoint(room.Value.Value.OfPoint(inHost));

                        if (found == null || found.Area <= 0)
                            continue;

                        string key = RVTLINKSELECTION.RoomKey(room.Key, found.Id.ToValue());

                        // An apartment with rooms inside: the room inside
                        // it, in another link, is the answer
                        if (holding != null && holding.Contains(key))
                            continue;

                        return key;
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }
    }
}
