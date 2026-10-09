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
    // The decision and the record are in VENTAPARTMENTMAP.cs, Revit-free;
    // this half only asks the model.
    public static partial class VENTAPARTMENTS
    {
        public static Map Build(Document doc)
        {
            var map = new Map();

            if (doc == null || VENTSETTINGS.ApartmentLinkIds.Count == 0)
                return map;

            map.Storeys = RVTLINKSELECTION.GetHostLevels(doc);
            map.Labels = RVTLINKSELECTION.LinkLabels(doc);

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
            }

            if (sources.Count == 0 || map.Storeys.Count == 0)
                return map;

            // ---- the INTERIOR placements, searched for rooms inside them:
            // those the keyword names, or the user ticked level by level
            // (VENTINTERIOR). An empty keyword, not chosen, is every
            // placement - the way it worked before.
            string keyword = VENTSETTINGS.InteriorKeyword;
            bool chosen = VENTSETTINGS.InteriorChosen;

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
                    continue;

                Document linked = link.GetLinkDocument();

                if (linked == null)
                    continue;

                Transform toHost = link.GetTotalTransform();

                foreach (Room room in new FilteredElementCollector(linked)
                             .OfCategory(BuiltInCategory.OST_Rooms)
                             .WhereElementIsNotElementType()
                             .Cast<Room>()
                             .Where(r => r.Area > 0))
                {
                    try
                    {
                        Place(map, sources, link, linked, toHost, room);
                    }
                    catch
                    {
                        // a room that cannot be placed stays an ordinary one
                    }
                }
            }

            return map;
        }

        private class Source
        {
            public long Id;
            public RevitLinkInstance Link;
            public Document Doc;
            public Transform Into;
        }

        private static Source Read(Document doc, long id)
        {
            var link = doc.GetElement(REVITCOMPAT.NewId(id)) as RevitLinkInstance;
            Document linked = link == null ? null : link.GetLinkDocument();

            return linked == null
                ? null
                : new Source { Id = id, Link = link, Doc = linked, Into = link.GetTotalTransform().Inverse };
        }

        private static void Place(
            Map map, List<Source> sources,
            RevitLinkInstance link, Document linked, Transform toHost, Room room)
        {
            int storey = VENTLEVELMAP.StoreyOf(
                map.Storeys, RVTLINKSELECTION.HostElevationOf(link, linked, room));

            // Below every level of this model: in no band, so in no
            // apartment either - the export leaves it out anyway
            if (storey < 0)
                return;

            // An interior model ON THIS STOREY? A placement ticked on L04
            // only gives its L04 rooms - VENTINTERIOR.Allowed
            if (!VENTINTERIOR.Allowed(
                    map.Storeys[storey].Name, link.Id.ToValue(), map.LabelOf(link.Id.ToValue()),
                    VENTSETTINGS.InteriorKeyword, VENTSETTINGS.InteriorChosen, VENTSETTINGS.InteriorPicks))
                return;

            // What each apartments model says about this room - the
            // decision is Map.Settle's, verified on THIS model's levels
            var candidates = new List<Candidate>();

            foreach (Source source in sources)
            {
                Room apartment = ApartmentOf(room, toHost, source);

                if (apartment == null)
                    continue;

                Parameter name = apartment.get_Parameter(BuiltInParameter.ROOM_NAME);

                candidates.Add(new Candidate
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

            map.Settle(
                link.Id.ToValue(),
                RVTLINKSELECTION.RoomKey(link.Id.ToValue(), room.Id.ToValue()),
                storey,
                candidates);
        }

        // The apartment a room stands in: asked at the room's own point
        // (where it was placed, which Revit keeps inside it) and, failing
        // that, the middle of its box - 150 mm above its floor, inside the
        // volume rather than on the boundary
        private static Room ApartmentOf(Room room, Transform toHost, Source source)
        {
            const double Up = 0.5;

            var points = new List<XYZ>();

            var location = room.Location as LocationPoint;

            if (location != null)
                points.Add(location.Point + new XYZ(0, 0, Up));

            BoundingBoxXYZ box = room.get_BoundingBox(null);

            if (box != null)
            {
                points.Add(new XYZ(
                    (box.Min.X + box.Max.X) / 2,
                    (box.Min.Y + box.Max.Y) / 2,
                    box.Min.Z + Up));
            }

            foreach (XYZ point in points)
            {
                Room found = source.Doc.GetRoomAtPoint(source.Into.OfPoint(toHost.OfPoint(point)));

                if (found != null && found.Area > 0)
                    return found;
            }

            return null;
        }
    }
}
