using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    public class REVITROOMS
    {
        // A room from one of the selected links. Key is unique across
        // links because room ids repeat between documents.
        private class RoomInfo
        {
            public Room Room { get; set; }
            public BoundingBoxXYZ Bbox { get; set; }
            public double BaseZ { get; set; }
            public string LevelName { get; set; }
            public string RoomName { get; set; }
            public string RoomNumber { get; set; }
            public string Key { get; set; }
            public string LinkName { get; set; }
        }

        private class LinkRooms
        {
            public string Name { get; set; }
            public Transform InverseTransform { get; set; }
            public List<RoomInfo> Rooms { get; set; }
        }

        public void Rooms(Autodesk.Revit.DB.Document doc)
        {
            _log = new StringBuilder();
            L($"=== ATKINSMEC run {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");

            try
            {
                RoomsCore(doc);
                L("Run completed normally");
            }
            catch (Exception ex)
            {
                L("EXCEPTION: " + ex);

                TaskDialog.Show(
                    "ATKINSMEC Error",
                    ex.Message);
            }
            finally
            {
                FlushLog();
            }
        }

        private void RoomsCore(Autodesk.Revit.DB.Document doc)
        {
            Autodesk.Revit.DB.Document doc2 = Command.Doc;

            VENTSHEET ventSheet = new VENTSHEET();

            Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData> ventilationData =
                ventSheet.excelsheet();

            L($"Excel records: {ventilationData.Count}");

            foreach (var rec in ventilationData)
            {
                L($"  EXCEL '{rec.Key.SpaceName}' | '{rec.Key.RoomNumber}' | '{rec.Key.Level}' | Fresh={rec.Value.FreshAir} | Exhaust={rec.Value.ExhaustAir}");
            }

            List<long> selectedLinkIds =
                RVTLINKSELECTION.SelectedLinkIds;

            HashSet<string> selectedLevels =
                RVTLINKSELECTION.SelectedLevels;

            // THE SAME MAPPING THE EXPORT USES, AND IT HAS TO BE.
            //
            // The level name is part of the key a sheet row is matched
            // by (KeyOf), so if the export writes this model's storey
            // names and the read-back looked up the linked model's,
            // every row would miss and nothing would ever be written.
            // The two directions are one decision.
            List<VENTLEVELMAP.Storey> storeys =
                RVTLINKSELECTION.GetHostLevels(doc);

            if (selectedLinkIds == null ||
                selectedLinkIds.Count == 0)
            {
                TaskDialog.Show(
                    "AUTOVENT",
                    "No linked architectural model selected.");

                L("Aborted: no link selected");
                return;
            }

            L($"Selected levels: {string.Join(", ", selectedLevels)}");

            // Where this model's levels are - in the model, which is what
            // rooms are compared against, and as Revit shows them. The
            // first thing to read when rooms land on the wrong level.
            L("This model's levels (in the model / as shown):");

            foreach (VENTLEVELMAP.Storey storey in storeys)
                L($"  {storey.Name}: {Metres(storey.Elevation)} / {Metres(storey.Shown)}");

            // ==================================================
            // Rooms from every selected link, on selected levels
            // ==================================================

            var linkGroups = new List<LinkRooms>();
            var allRooms = new List<RoomInfo>();

            // APARTMENTS WITH ROOMS INSIDE ARE NOT CANDIDATES. A terminal
            // in a bedroom stands in the bedroom (interior model) AND in
            // the apartment (architect's model), at the same floor - and
            // a tie goes to whichever link came first. The bedroom has the
            // sheet row; the apartment has none. See VENTAPARTMENTS.
            VENTAPARTMENTS.Map apartments = VENTAPARTMENTS.Build(doc2, "the airflow write");

            var ticked = new HashSet<long>(selectedLinkIds);

            if (apartments.On)
            {
                L($"Apartments from '{apartments.Label}': {apartments.Holding.Count} with rooms inside, " +
                  $"{apartments.ByRoom.Count} room(s) inside them - those apartments take no terminals of their own");

                foreach (string line in apartments.Describe())
                    L("   " + line);

                if (apartments.OtherStorey > 0)
                    L($"   {apartments.OtherStorey} room(s) stood inside an apartment on another level of this model - not grouped");

                if (apartments.Outside > 0)
                    L($"   {apartments.Outside} interior room(s) stood in NO apartment - not grouped, and given no airflow; why is in {VENTAPTLOG.Where}");

                foreach (string line in apartments.DescribeSpread())
                    L($"   CHECK: {line} - an architect's room across several apartments?");
            }

            // The ticked links, then every interior placement found holding
            // an apartment's rooms - only those rooms are read from it
            foreach (long linkId in apartments.RoomLinks(selectedLinkIds))
            {
                RevitLinkInstance linkInstance =
                    doc2.GetElement(REVITCOMPAT.NewId(linkId)) as RevitLinkInstance;

                if (linkInstance == null)
                    continue;

                Autodesk.Revit.DB.Document linkedDoc =
                    linkInstance.GetLinkDocument();

                if (linkedDoc == null)
                {
                    L($"Link {linkId}: not loaded, skipped");
                    continue;
                }

                var group = new LinkRooms
                {
                    Name = linkedDoc.Title,
                    InverseTransform = linkInstance.GetTransform().Inverse,
                    Rooms = new List<RoomInfo>()
                };

                Heights(linkInstance, linkedDoc, storeys);

                var rooms =
                    new FilteredElementCollector(linkedDoc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0);

                int skippedByLevel = 0;
                int belowEveryLevel = 0;
                int unticked = 0;

                foreach (Room room in rooms)
                {
                    if (!apartments.Takes(linkId, RVTLINKSELECTION.RoomKey(linkId, room.Id.ToValue()), ticked))
                        continue;

                    // The level of THIS model whose band the room's floor
                    // stands in - the same rule the export writes with
                    string levelName = VENTLEVELMAP.NameAt(
                        storeys,
                        RVTLINKSELECTION.HostElevationOf(
                            linkInstance, linkedDoc, room));

                    if (levelName.Length == 0)
                    {
                        belowEveryLevel++;
                        continue;
                    }

                    if (selectedLevels.Count > 0 &&
                        !selectedLevels.Contains(levelName.Trim()))
                    {
                        skippedByLevel++;
                        continue;
                    }

                    // Unticked under its level in the window
                    if (RVTLINKSELECTION.ExcludedRooms.Contains(
                            RVTLINKSELECTION.RoomKey(linkId, room.Id.ToValue())))
                    {
                        unticked++;
                        continue;
                    }

                    // An apartment with rooms inside: they take its air
                    if (apartments.Holding.Contains(
                            RVTLINKSELECTION.RoomKey(linkId, room.Id.ToValue())))
                    {
                        continue;
                    }

                    BoundingBoxXYZ bbox = room.get_BoundingBox(null);

                    if (bbox == null)
                        continue;

                    Parameter nameParam =
                        room.get_Parameter(BuiltInParameter.ROOM_NAME);

                    // By the name the export wrote it under - renamed in
                    // Arrange rooms... or the model's own - or its sheet
                    // row is never found
                    var info = new RoomInfo
                    {
                        Room = room,
                        Bbox = bbox,
                        BaseZ = Math.Round(bbox.Min.Z, 2),
                        LevelName = levelName,
                        RoomName = VENTARRANGE.Current.NameOf(
                            RVTLINKSELECTION.RoomKey(linkId, room.Id.ToValue()),
                            nameParam == null ? string.Empty : nameParam.AsString()),
                        RoomNumber = room.Number,
                        Key = linkId + ":" + room.Id.ToValue(),
                        LinkName = linkedDoc.Title
                    };

                    group.Rooms.Add(info);
                    allRooms.Add(info);
                }

                linkGroups.Add(group);

                L($"Link '{linkedDoc.Title}': {group.Rooms.Count} room(s) on selected levels, {skippedByLevel} skipped by level filter, {unticked} unticked in the list, {belowEveryLevel} below this model's lowest level");
            }

            List<FamilyInstance> allTerminals =
                new FilteredElementCollector(doc2)
                .OfCategory(BuiltInCategory.OST_DuctTerminal)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            L($"Rooms in scope: {allRooms.Count}, Air terminals: {allTerminals.Count}");

            if (allRooms.Count == 0)
            {
                TaskDialog.Show(
                    "AUTOVENT",
                    "No rooms found on the selected levels.");

                return;
            }

            // ==================================================
            // Assign each terminal to the room it stands in
            // ==================================================

            // A terminal belongs to the room whose footprint contains its
            // XY position. Terminals sit at ceiling height or in the void
            // above, so vertically a terminal may be anywhere from the
            // room base up to ~6 m above it; when storeys stack, the room
            // with the highest base at or below the terminal wins. The
            // point-in-room test runs at a height just above the room's
            // floor, where the point is safely inside the room volume.
            const double maxAboveBase = 19.7;
            const double belowBaseTolerance = 0.5;

            var roomTerminalMap =
                new Dictionary<string, List<FamilyInstance>>();

            var roomByKey = new Dictionary<string, RoomInfo>();

            foreach (RoomInfo info in allRooms)
                roomByKey[info.Key] = info;

            RoomInfo RoomAt(XYZ point)
            {
                RoomInfo found = null;
                double foundBaseZ = double.MinValue;

                foreach (LinkRooms group in linkGroups)
                {
                    XYZ pointInLink =
                        group.InverseTransform.OfPoint(point);

                    foreach (RoomInfo info in group.Rooms)
                    {
                        if (pointInLink.Z < info.BaseZ - belowBaseTolerance ||
                            pointInLink.Z > info.BaseZ + maxAboveBase)
                            continue;

                        if (pointInLink.X < info.Bbox.Min.X - 0.1 ||
                            pointInLink.X > info.Bbox.Max.X + 0.1 ||
                            pointInLink.Y < info.Bbox.Min.Y - 0.1 ||
                            pointInLink.Y > info.Bbox.Max.Y + 0.1)
                            continue;

                        if (info.BaseZ <= foundBaseZ)
                            continue;

                        XYZ testPoint = new XYZ(
                            pointInLink.X,
                            pointInLink.Y,
                            info.BaseZ + 2.0);

                        if (info.Room.IsPointInRoom(testPoint))
                        {
                            found = info;
                            foundBaseZ = info.BaseZ;
                        }
                    }
                }

                return found;
            }

            // A TERMINAL BELONGS TO THE ROOM ITS AIR GOES INTO: a step in
            // front of its face, and only failing that the room it stands
            // in. A sidewall grille's insertion point is often in the wall,
            // on whichever side the family put it - user-reported (kifaf,
            // 2026-10-11): kitchens counted as served by TWO FCUs because
            // the next room's grille, in the kitchen wall, was taken for
            // one of the kitchen's. A ceiling diffuser faces down, so its
            // step lands in the room it stands in, as before.
            int turned = 0;

            foreach (FamilyInstance terminal in allTerminals)
            {
                LocationPoint lp =
                    terminal.Location as LocationPoint;

                if (lp == null)
                    continue;

                RoomInfo standing = RoomAt(lp.Point);
                RoomInfo matched = null;

                double[] mouth = MouthOf(terminal);

                if (mouth != null)
                    matched = RoomAt(X(VENTFACING.InFront(P(lp.Point), mouth, FrontStepFeet)));

                if (matched == null)
                    matched = standing;

                if (matched == null)
                    continue;

                if (standing != null && standing.Key != matched.Key && ++turned <= 60)
                    L($"TERMINAL {terminal.Id.ToValue()} '{terminal.Symbol?.FamilyName}' stands in {standing.RoomNumber} {standing.RoomName} but faces into {matched.RoomNumber} {matched.RoomName}");

                List<FamilyInstance> terminalList;

                if (!roomTerminalMap.TryGetValue(
                    matched.Key,
                    out terminalList))
                {
                    terminalList = new List<FamilyInstance>();
                    roomTerminalMap.Add(matched.Key, terminalList);
                }

                terminalList.Add(terminal);
            }

            L($"Rooms containing terminals: {roomTerminalMap.Count}");

            if (turned > 0)
                L($"Terminals facing into another room than the one they stand in: {turned}" +
                  (turned > 60 ? " (the first 60 listed)" : string.Empty));

            var terminalRoom =
                new Dictionary<ElementId, string>();

            foreach (var kvp in roomTerminalMap)
            {
                foreach (FamilyInstance t in kvp.Value)
                    terminalRoom[t.Id] = kvp.Key;
            }

            // Each room's air in the sheet, per side - asked by the
            // bellmouth decision, the report and the write
            var sheetFresh = new Dictionary<string, double>();
            var sheetExtract = new Dictionary<string, double>();

            foreach (RoomInfo info in allRooms)
            {
                VENTSHEET.AirData air;

                if (ventilationData.TryGetValue(KeyOf(info), out air))
                {
                    sheetFresh[info.Key] = air.FreshAir;
                    sheetExtract[info.Key] = air.ExhaustAir;
                }
            }

            List<FamilyInstance> equipment =
                new FilteredElementCollector(doc2)
                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            // How far the terminal may be from the nearest FCU
            // connector. Was a constant 4.92 ft (~1.5 m); asked for on
            // the window now, because it describes how somebody draws
            // rather than anything about the add-in - see VENTSETTINGS.
            double connectorTolerance = VENTSETTINGS.EndcapReachFeet;

            L($"Endcap reach: {VENTSETTINGS.EndcapReachMm:0} mm");
            L($"Endcap names: {VENTSETTINGS.EndcapNames}");
            L($"Bellmouth names: {(VENTSETTINGS.BellmouthNames.Length == 0 ? "(none)" : VENTSETTINGS.BellmouthNames)}");

            // ==================================================
            // Bellmouths: into the room, or into an FCU?
            // ==================================================
            //
            // VENTFACING.Bellmouth decides; every bellmouth's answer and
            // why is in the log.
            Dictionary<ElementId, Faced> faced =
                BellmouthsFeeding(allTerminals, equipment, connectorTolerance,
                                  terminalRoom, sheetFresh, sheetExtract, allRooms,
                                  out int bellmouths);

            // ==================================================
            // Classify the in-room terminals as fresh / extract
            // ==================================================

            var supplyMap =
                new Dictionary<string, List<FamilyInstance>>();

            var exhaustMap =
                new Dictionary<string, List<FamilyInstance>>();

            foreach (var kvp in roomTerminalMap)
            {
                supplyMap[kvp.Key] = new List<FamilyInstance>();
                exhaustMap[kvp.Key] = new List<FamilyInstance>();

                foreach (FamilyInstance terminal in kvp.Value)
                {
                    // Endcaps, and bellmouths facing a unit, feed an FCU
                    // serving ANOTHER space — they are handled by the FCU
                    // trace, never as in-room terminals
                    if (IsEndcap(terminal) || faced.ContainsKey(terminal.Id))
                        continue;

                    bool isSupply;
                    bool isExhaust;

                    ClassifyTerminal(
                        terminal,
                        out isSupply,
                        out isExhaust);

                    if (isSupply)
                        supplyMap[kvp.Key].Add(terminal);

                    if (isExhaust)
                        exhaustMap[kvp.Key].Add(terminal);
                }

                L($"Room {kvp.Key}: terminalsInside={kvp.Value.Count}, freshInside={supplyMap[kvp.Key].Count}, extractInside={exhaustMap[kvp.Key].Count}");
            }

            // ==================================================
            // Endcaps feed an FCU that serves another space: trace
            // terminal -> touching FCU -> FCU supply system ->
            // grilles inside a room -> that room's sheet value
            // ==================================================

            // Rooms present in the sheet, so an endcap standing in an
            // unmatched room (a corridor, say) still traces to its FCU
            var matchedRoomKeys = new HashSet<string>();

            foreach (RoomInfo info in allRooms)
            {
                if (ventilationData.ContainsKey(KeyOf(info)))
                    matchedRoomKeys.Add(info.Key);
            }

            var outsideFresh =
                new Dictionary<FamilyInstance, List<string>>();

            var outsideExtract =
                new Dictionary<FamilyInstance, List<string>>();

            int noFcuFound = 0;
            int fcuServesNoRoom = 0;
            double closestMiss = double.MaxValue;

            // WHICH UNIT EACH ENDCAP BOUND TO. The trace already works
            // this out and used to throw it away, keeping only the
            // rooms - but it is the only way to answer "how many of the
            // units serving this room actually have an endcap".
            var freshEndcapsOn = new Dictionary<long, int>();
            var extractEndcapsOn = new Dictionary<long, int>();

            foreach (FamilyInstance terminal in allTerminals)
            {
                bool isFresh;
                bool isExtract;

                ClassifyTerminal(
                    terminal,
                    out isFresh,
                    out isExtract);

                if (!isFresh && !isExtract)
                    continue;

                string inRoomKey;

                bool insideRoom =
                    terminalRoom.TryGetValue(
                        terminal.Id,
                        out inRoomKey);

                // Endcaps, and bellmouths VENTFACING.Bellmouth gave to a
                // unit, are traced through an FCU: they serve another
                // space, even when standing in a matched room. Every other
                // terminal is in-room only: its value comes from the room
                // it stands in, so an unmatched room means nothing is
                // written.
                bool endcap = IsEndcap(terminal);

                Faced facedUnit;

                bool feeds = !endcap && faced.TryGetValue(terminal.Id, out facedUnit);

                if (!endcap && !feeds)
                {
                    if (!insideRoom)
                    {
                        L($"UNRESOLVED terminal {terminal.Id.ToValue()} '{terminal.Symbol.FamilyName}' fresh={isFresh} extract={isExtract}: not an endcap and not inside any room in scope, nothing written");
                    }
                    else if (!matchedRoomKeys.Contains(inRoomKey))
                    {
                        L($"UNRESOLVED terminal {terminal.Id.ToValue()} '{terminal.Symbol.FamilyName}' fresh={isFresh} extract={isExtract}: room {inRoomKey} not in Excel, nothing written");
                    }

                    continue;
                }

                L($"FCU-CANDIDATE {(endcap ? "endcap" : "bellmouth")} {terminal.Id.ToValue()} '{terminal.Symbol.FamilyName}' fresh={isFresh} extract={isExtract}" +
                    (insideRoom
                        ? $" (standing in room {inRoomKey})"
                        : " (not inside any room in scope)"));

                FamilyInstance fcu;

                if (feeds)
                {
                    // The unit already settled - within the reach by
                    // construction
                    fcu = faced[terminal.Id].Unit;

                    L($"  feeds FCU {fcu.Id.ToValue()} at {faced[terminal.Id].Distance * 304.8:F0} mm - {VENTFACING.Say(faced[terminal.Id].Why)}");
                }
                else
                {
                    double nearestDistance;

                    fcu = FindNearestEquipment(
                        terminal,
                        equipment,
                        out nearestDistance);

                    L($"  nearest FCU: {(fcu == null ? "none" : fcu.Id.ToValue().ToString())}, distance={(fcu == null ? 0 : nearestDistance * 304.8):F0} mm (tolerance {connectorTolerance * 304.8:F0} mm)");

                    if (fcu == null ||
                        nearestDistance > connectorTolerance)
                    {
                        L("  -> rejected: no FCU within tolerance");

                        noFcuFound++;

                        if (fcu != null &&
                            nearestDistance < closestMiss)
                            closestMiss = nearestDistance;

                        continue;
                    }
                }

                List<string> servedRooms =
                    GetServedRoomKeys(fcu, terminalRoom);

                if (servedRooms.Count == 0)
                {
                    L($"  -> FCU {fcu.Id.ToValue()} supply system reaches no room in scope");

                    fcuServesNoRoom++;
                    continue;
                }

                L($"  -> served rooms: {string.Join(", ", servedRooms)}");

                long fcuId = fcu.Id.ToValue();

                if (isFresh)
                {
                    outsideFresh[terminal] = servedRooms;

                    Bump(freshEndcapsOn, fcuId);
                }

                if (isExtract)
                {
                    outsideExtract[terminal] = servedRooms;

                    Bump(extractEndcapsOn, fcuId);
                }
            }

            L($"FCU trace: freshTraced={outsideFresh.Count}, extractTraced={outsideExtract.Count}, noFcu={noFcuFound}, fcuNoRoom={fcuServesNoRoom}, equipment={equipment.Count}");

            // ==================================================
            // Do a room's UNITS and its ENDCAPS agree?
            // ==================================================
            //
            // Everything above runs endcap -> unit -> rooms. This is the
            // walk the other way, unit -> rooms, which nothing did
            // before - and without it a room served by two units with
            // one endcap looks like a clean success.
            //
            // IT REPORTS AND CHANGES NOTHING. See VENTFCUCHECK.
            var roomsOfFcu = new Dictionary<long, List<string>>();

            foreach (FamilyInstance unit in equipment)
            {
                List<string> served =
                    GetServedRoomKeys(unit, terminalRoom);

                // Equipment with no supply reaching a room in scope is
                // not an FCU as far as this is concerned - pumps and
                // air handling units drop out here on their own
                if (served.Count > 0)
                    roomsOfFcu[unit.Id.ToValue()] = served;
            }

            // What each unit was found to supply - the rooms behind every
            // "N FCU(s)" in the report
            L($"Units supplying rooms in scope: {roomsOfFcu.Count}");

            foreach (var unit in roomsOfFcu.OrderBy(u => u.Key))
                L($"  FCU {unit.Key}: " + string.Join(", ", unit.Value.Select(k => RoomLabel(allRooms, k))));

            // Every room, its units, and the endcaps the trace bound to
            // them. Worked out in VENTFCUCHECK, which is Revit-free, so
            // the split can be tested end to end with no model open.
            Dictionary<string, VENTFCUCHECK.Room> tally =
                VENTFCUCHECK.Tally(
                    roomsOfFcu,
                    freshEndcapsOn,
                    extractEndcapsOn,
                    key => RoomLabel(allRooms, key));

            // Only rooms the sheet gives air to on the side concerned
            List<VENTFCUCHECK.Room> divided =
                VENTFCUCHECK.Worth(tally, sheetFresh, sheetExtract);

            L($"Endcap split: {tally.Count} room(s) served by equipment, " +
              $"{divided.Count} with air divided between endcaps or a unit " +
              "that has none");

            foreach (VENTFCUCHECK.Room room in divided)
                L("  " + VENTFCUCHECK.Line(room, sheetFresh, sheetExtract));

            // ==================================================
            // Write the airflows
            // ==================================================

            int matchedRooms = 0;
            int terminalsUpdated = 0;
            int terminalsSkipped = 0;

            // Rooms whose air found a terminal - in the room, or an endcap
            // on its unit - per side. The rest went nowhere.
            var landedFresh = new HashSet<string>();
            var landedExtract = new HashSet<string>();

            List<string> nowhereFresh = new List<string>();
            List<string> nowhereExtract = new List<string>();

            using (Transaction t =
                new Transaction(doc2, "Update Airflows"))
            {
                t.Start();

                var roomAirData =
                    new Dictionary<string, VENTSHEET.AirData>();

                foreach (RoomInfo info in allRooms)
                {
                    VENTSHEET.AirData airData;

                    if (!ventilationData.TryGetValue(
                        KeyOf(info),
                        out airData))
                    {
                        continue;
                    }

                    L($"MATCHED room {info.Key} '{info.RoomName}' | '{info.RoomNumber}' | '{info.LevelName}': Fresh={airData.FreshAir}, Exhaust={airData.ExhaustAir}");

                    matchedRooms++;
                    roomAirData[info.Key] = airData;

                    List<FamilyInstance> supplyTerminals;

                    if (supplyMap.TryGetValue(info.Key, out supplyTerminals) &&
                        supplyTerminals.Count > 0 &&
                        airData.FreshAir > 0)
                    {
                        landedFresh.Add(info.Key);

                        double perTerminal =
                            airData.FreshAir / supplyTerminals.Count;

                        foreach (FamilyInstance diffuser in supplyTerminals)
                        {
                            if (WriteFlow(diffuser, perTerminal))
                            {
                                terminalsUpdated++;
                                L($"  WRITE fresh {perTerminal} l/s -> in-room terminal {diffuser.Id.ToValue()}");
                            }
                            else
                            {
                                terminalsSkipped++;
                                L($"  SKIP in-room terminal {diffuser.Id.ToValue()}: Flow parameter missing or read-only");
                            }
                        }
                    }

                    List<FamilyInstance> exhaustTerminals;

                    if (exhaustMap.TryGetValue(info.Key, out exhaustTerminals) &&
                        exhaustTerminals.Count > 0 &&
                        airData.ExhaustAir > 0)
                    {
                        landedExtract.Add(info.Key);

                        double perTerminal =
                            airData.ExhaustAir / exhaustTerminals.Count;

                        foreach (FamilyInstance grille in exhaustTerminals)
                        {
                            if (WriteFlow(grille, perTerminal))
                            {
                                terminalsUpdated++;
                                L($"  WRITE exhaust {perTerminal} l/s -> in-room terminal {grille.Id.ToValue()}");
                            }
                            else
                            {
                                terminalsSkipped++;
                                L($"  SKIP in-room terminal {grille.Id.ToValue()}: Flow parameter missing or read-only");
                            }
                        }
                    }
                }

                // ENDCAPS: EACH ONE CARRIES ITS SHARE of every room its
                // unit serves - the room's air divided equally between
                // ALL the endcaps on ALL the units serving that room.
                // Nothing is written twice and nothing is lost; one unit
                // with one endcap divides by one, so the ordinary room is
                // untouched. See VENTFCUCHECK.
                var freshValues = new Dictionary<string, double>();
                var extractValues = new Dictionary<string, double>();

                foreach (var each in roomAirData)
                {
                    freshValues[each.Key] = each.Value.FreshAir;
                    extractValues[each.Key] = each.Value.ExhaustAir;
                }

                foreach (var kvp in outsideFresh)
                {
                    double total = VENTFCUCHECK.Carried(
                        kvp.Value, freshValues, tally, true);

                    if (total <= 0)
                    {
                        L($"OUTSIDE fresh terminal {kvp.Key.Id.ToValue()}: served rooms not in Excel or fresh=0, nothing written");
                        continue;
                    }

                    landedFresh.UnionWith(
                        VENTFCUCHECK.CarriedRooms(kvp.Value, freshValues));

                    if (WriteFlow(kvp.Key, total))
                    {
                        terminalsUpdated++;
                        L($"WRITE fresh {total} l/s -> endcap {kvp.Key.Id.ToValue()}");
                    }
                    else
                    {
                        terminalsSkipped++;
                        L($"SKIP endcap {kvp.Key.Id.ToValue()}: Flow parameter missing or read-only");
                    }
                }

                foreach (var kvp in outsideExtract)
                {
                    double total = VENTFCUCHECK.Carried(
                        kvp.Value, extractValues, tally, false);

                    if (total <= 0)
                    {
                        L($"OUTSIDE extract terminal {kvp.Key.Id.ToValue()}: served rooms not in Excel or exhaust=0, nothing written");
                        continue;
                    }

                    landedExtract.UnionWith(
                        VENTFCUCHECK.CarriedRooms(kvp.Value, extractValues));

                    if (WriteFlow(kvp.Key, total))
                    {
                        terminalsUpdated++;
                        L($"WRITE extract {total} l/s -> endcap {kvp.Key.Id.ToValue()}");
                    }
                    else
                    {
                        terminalsSkipped++;
                        L($"SKIP endcap {kvp.Key.Id.ToValue()}: Flow parameter missing or read-only");
                    }
                }

                nowhereFresh = VENTFCUCHECK.Nowhere(
                    freshValues, landedFresh, key => RoomLabel(allRooms, key));

                nowhereExtract = VENTFCUCHECK.Nowhere(
                    extractValues, landedExtract, key => RoomLabel(allRooms, key));

                t.Commit();
            }

            L($"Air with nowhere to go: {nowhereFresh.Count} room(s) fresh, {nowhereExtract.Count} extract");

            foreach (string line in nowhereFresh)
                L("  NOWHERE fresh " + line);

            foreach (string line in nowhereExtract)
                L("  NOWHERE extract " + line);

            L($"SUMMARY: matchedRooms={matchedRooms}, terminalsUpdated={terminalsUpdated}, terminalsSkipped={terminalsSkipped}");

            string report =
                $"Models: {linkGroups.Count}\n" +
                $"Levels: {string.Join(", ", selectedLevels)}\n\n" +
                $"Rooms in scope: {allRooms.Count}\n" +
                $"Rooms matched to the sheet: {matchedRooms}\n" +
                $"Terminals updated: {terminalsUpdated}\n" +
                $"Terminals skipped (read-only): {terminalsSkipped}" +
                (bellmouths > 0
                    ? $"\nBellmouths feeding an FCU: {faced.Count} of {bellmouths}"
                    : string.Empty);

            // A ROOM WHOSE UNITS AND ENDCAPS DISAGREE IS NOT AN
            // ERROR, AND NOTHING WAS CHANGED FOR IT - but it is the
            // one thing here that looks like a clean success and is
            // not, so it is said on the window and not only in the
            // log.
            // WRITING A DIFFERENT NUMBER FROM THE ONE IN THE SHEET IS
            // WORTH SAYING OUT LOUD, even though it is now what was
            // asked for - and a room short of an endcap has air going
            // nowhere, which is the cost of splitting equally.
            if (divided.Count > 0)
            {
                report +=
                    $"\n\n{divided.Count} room(s) whose air was divided " +
                    "equally between the endcaps serving it:\n";

                int listed = 0;

                foreach (VENTFCUCHECK.Room room in divided)
                {
                    if (listed == 6)
                    {
                        report +=
                            $"    ... and {divided.Count - listed} " +
                            "more, all of them in the log.\n";

                        break;
                    }

                    report += "    " + VENTFCUCHECK.Line(room, sheetFresh, sheetExtract) + "\n";

                    listed++;
                }
            }

            // AIR WITH NOWHERE TO GO - a room the sheet gives air to, with
            // no terminal in it and no endcap on its unit. Usually an
            // endcap named some other way (Endcaps on the window).
            report += Nowhere("fresh", nowhereFresh);
            report += Nowhere("extract", nowhereExtract);

            if (matchedRooms == 0 &&
                allRooms.Count > 0 &&
                ventilationData.Count > 0)
            {
                RoomInfo sample = allRooms[0];

                var excelKeys = new StringBuilder();
                int shown = 0;

                foreach (var k in ventilationData.Keys)
                {
                    excelKeys.AppendLine(
                        $"'{k.SpaceName}' | '{k.RoomNumber}' | '{k.Level}'");

                    if (++shown >= 5)
                        break;
                }

                report +=
                    "\n\nNo matches. Compare key formats:\n" +
                    $"First room in scope:\n'{sample.RoomName}' | '{sample.RoomNumber}' | '{sample.LevelName}'\n\n" +
                    $"First sheet keys (name | number | level):\n{excelKeys}";
            }

            report +=
                "\n\nFull details: ATKINSMEC_debug_log.txt on your Desktop.";

            TaskDialog.Show("AUTOVENT", report);
        }

        private static string Nowhere(string side, List<string> lines)
        {
            if (lines == null || lines.Count == 0)
                return string.Empty;

            string text =
                $"\n\n{lines.Count} room(s) with {side} air and no terminal or endcap to take it:\n";

            for (int i = 0; i < lines.Count; i++)
            {
                if (i == 6)
                {
                    text += $"    ... and {lines.Count - i} more, in the log.\n";
                    break;
                }

                text += "    " + lines[i] + "\n";
            }

            return text;
        }

        private static VENTSHEET.SpaceKey KeyOf(RoomInfo info)
        {
            return new VENTSHEET.SpaceKey
            {
                SpaceName = info.RoomName,
                RoomNumber = info.RoomNumber,
                Level = info.LevelName
            };
        }

        // Sheet values are l/s; Revit stores flow in internal units
        private static bool WriteFlow(
            FamilyInstance terminal,
            double litresPerSecond)
        {
            Parameter flow =
                terminal.get_Parameter(
                    BuiltInParameter.RBS_DUCT_FLOW_PARAM);

            if (flow == null || flow.IsReadOnly)
                return false;

            flow.Set(
                UnitUtils.ConvertToInternalUnits(
                    litresPerSecond,
                    UnitTypeId.LitersPerSecond));

            return true;
        }

        // Every duct system type in the model, for the two lists on the
        // window. Read here rather than in RVTLINKSELECTION because it
        // has nothing to do with links - this is the file that decides
        // what a terminal carries, so this is where its raw material
        // belongs.
        public static List<VENTSETTINGS.SystemTypeItem> AirSystemTypes(
            Document doc)
        {
            var found = new List<VENTSETTINGS.SystemTypeItem>();

            if (doc == null)
                return found;

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(MechanicalSystemType));

            foreach (Element element in collector)
            {
                if (element == null)
                    continue;

                found.Add(new VENTSETTINGS.SystemTypeItem
                {
                    Id = element.Id.ToValue(),
                    Name = element.Name ?? string.Empty
                });
            }

            found.Sort(delegate (
                VENTSETTINGS.SystemTypeItem a,
                VENTSETTINGS.SystemTypeItem b)
            {
                return string.Compare(
                    a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return found;
        }

        // Endcap families feed an FCU that serves another space; every
        // other terminal family serves the space it stands in. Which
        // families those are is the window's (VENTSETTINGS.EndcapNames).
        private static bool IsEndcap(FamilyInstance terminal)
        {
            return terminal.Symbol != null &&
                VENTSETTINGS.IsEndcap(terminal.Symbol.FamilyName);
        }

        private static void ClassifyTerminal(
            FamilyInstance terminal,
            out bool isFresh,
            out bool isExtract)
        {
            isFresh = false;
            isExtract = false;

            if (terminal.MEPModel == null ||
                terminal.MEPModel.ConnectorManager == null)
                return;

            foreach (Connector c in
                     terminal.MEPModel.ConnectorManager.Connectors)
            {
                MechanicalSystem sys =
                    c.MEPSystem as MechanicalSystem;

                if (sys == null)
                    continue;

                // The system's own name (e.g. "OAD 40"), its TYPE's
                // name (e.g. "ATK_ME_Fresh Air"), and the type's id -
                // which is what the window's two lists are ticked by.
                // VENTSETTINGS decides; this only reads.
                string systemName = sys.Name;
                string systemTypeName = string.Empty;

                long systemTypeId = 0;

                ElementId typeId = sys.GetTypeId();

                if (typeId != ElementId.InvalidElementId)
                {
                    systemTypeId = typeId.ToValue();

                    Element systemType =
                        sys.Document.GetElement(typeId);

                    if (systemType != null)
                        systemTypeName = systemType.Name;
                }

                if (VENTSETTINGS.IsFresh(
                        systemTypeId, systemName, systemTypeName))
                {
                    isFresh = true;
                }

                if (VENTSETTINGS.IsExtract(
                        systemTypeId, systemName, systemTypeName))
                {
                    isExtract = true;
                }
            }
        }

        private static StringBuilder _log;

        // Each linked level as the link SHOWS it, where it lands in
        // THIS model, and which of this model's levels takes it - then
        // one room's floor worked out two ways: from its level
        // (RVTLINKSELECTION.InternalZ, what everything uses) and from the
        // room's own bounding box, which is geometry and so always in
        // the model's coordinates. If those two ever disagree, the level
        // arithmetic is wrong for this model, and this says by how much.
        private static void Heights(
            RevitLinkInstance link,
            Autodesk.Revit.DB.Document linked,
            List<VENTLEVELMAP.Storey> storeys)
        {
            try
            {
                Transform total = link.GetTotalTransform();

                // What this model's own rooms say its levels are out by -
                // the correction every level below is given
                double error = RVTLINKSELECTION.LevelError(linked);

                L($"  Its levels, measured on its own rooms, are out by {Metres(error)}" +
                  (error == 0 ? " - none" : " - corrected below; each room's floor is its own geometry"));

                L($"  Levels of {linked.Title} (as it shows them -> in this model -> this model's level):");

                foreach (Level level in new FilteredElementCollector(linked)
                             .OfClass(typeof(Level))
                             .Cast<Level>()
                             .OrderBy(l => l.ProjectElevation))
                {
                    double here = total.OfPoint(
                        new XYZ(0, 0, RVTLINKSELECTION.InternalZ(level) + error)).Z;

                    string onto = VENTLEVELMAP.NameAt(storeys, here);

                    L($"    {level.Name}: {Metres(level.Elevation)} -> {Metres(here)} -> " +
                      (onto.Length == 0 ? "(below every level)" : onto));
                }

                Room sample = new FilteredElementCollector(linked)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .FirstOrDefault(r => r.Area > 0 && r.get_BoundingBox(null) != null);

                var own = sample == null ? null : linked.GetElement(sample.LevelId) as Level;

                if (own != null)
                {
                    double byLevel = RVTLINKSELECTION.InternalZ(own) + sample.BaseOffset;
                    double byBox = sample.get_BoundingBox(null).Min.Z;

                    L($"  Check, room {sample.Number}: floor by its level {Metres(byLevel)}, " +
                      $"by its box {Metres(byBox)} - " +
                      (Math.Abs(byLevel - byBox) < 0.01
                          ? "agree"
                          : "DISAGREE by " + Metres(byBox - byLevel)));
                }
            }
            catch (Exception ex)
            {
                L($"  (could not list the link's levels: {ex.Message})");
            }
        }

        private static string Metres(double feet)
        {
            return (feet * 0.3048).ToString(
                       "+0.000;-0.000;0.000",
                       System.Globalization.CultureInfo.InvariantCulture) + " m";
        }

        private static void L(string line)
        {
            if (_log != null)
                _log.AppendLine(line);
        }

        private static void FlushLog()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Desktop),
                    "ATKINSMEC_debug_log.txt");

                File.WriteAllText(path, _log.ToString());
            }
            catch
            {
                // logging must never break the add-in
            }
        }

        // A bellmouth, the unit it feeds, and why
        private class Faced
        {
            public FamilyInstance Unit;
            public double Distance;
            public VENTFACING.Reason Why;
        }

        // How much a unit's box is grown before asking whether a mouth
        // faces it - a bellmouth drawn a little off the plenum's centre
        // still feeds it
        private const double FacingMarginFeet = 150 / 304.8;

        // Within this of a unit's casing, a bellmouth is beside it
        private const double TouchingFeet = VENTFACING.TouchingM / 0.3048;

        // How far in front of a terminal's face its room is judged
        private const double FrontStepFeet = 0.3 / 0.3048;

        // EVERY BELLMOUTH ON A FRESH OR EXTRACT SYSTEM, decided once by
        // VENTFACING.Bellmouth: the unit it feeds, or - absent from the
        // answer - it serves the room it stands in. Every one is logged
        // with its reason.
        //
        // A UNIT ON THE BELLMOUTH'S OWN DUCT SYSTEM IS NOT FED THROUGH ITS
        // MOUTH: an AHU's fresh air intake faces the AHU, but its duct
        // runs into it. Only air crossing an open gap into a unit counts.
        private static Dictionary<ElementId, Faced> BellmouthsFeeding(
            List<FamilyInstance> terminals,
            List<FamilyInstance> units,
            double reach,
            Dictionary<ElementId, string> terminalRoom,
            Dictionary<string, double> sheetFresh,
            Dictionary<string, double> sheetExtract,
            List<RoomInfo> allRooms,
            out int bellmouths)
        {
            var feeding = new Dictionary<ElementId, Faced>();

            bellmouths = 0;

            var boxes = new List<VENTFACING.Box>();
            var systemsOf = new List<HashSet<long>>();

            foreach (FamilyInstance unit in units)
            {
                BoundingBoxXYZ box = unit.get_BoundingBox(null);

                boxes.Add(box == null
                    ? null
                    : new VENTFACING.Box { Min = P(box.Min), Max = P(box.Max) });

                systemsOf.Add(SystemsOf(unit));
            }

            // The rooms each unit supplies, asked once per unit
            var servedBy = new Dictionary<long, List<string>>();

            List<string> Served(FamilyInstance unit)
            {
                List<string> rooms;

                if (!servedBy.TryGetValue(unit.Id.ToValue(), out rooms))
                {
                    rooms = GetServedRoomKeys(unit, terminalRoom);
                    servedBy[unit.Id.ToValue()] = rooms;
                }

                return rooms;
            }

            foreach (FamilyInstance terminal in terminals)
            {
                if (terminal.Symbol == null ||
                    IsEndcap(terminal) ||
                    !VENTSETTINGS.IsBellmouth(terminal.Symbol.FamilyName))
                    continue;

                bool isFresh;
                bool isExtract;

                ClassifyTerminal(terminal, out isFresh, out isExtract);

                if (!isFresh && !isExtract)
                    continue;

                bellmouths++;

                HashSet<long> own = SystemsOf(terminal);

                // The units it could feed: not on its own system
                var candidates = new List<VENTFACING.Box>();
                var candidateUnits = new List<FamilyInstance>();

                for (int i = 0; i < units.Count; i++)
                {
                    bool mine = systemsOf[i].Overlaps(own);

                    candidates.Add(mine ? null : boxes[i]);

                    if (!mine)
                        candidateUnits.Add(units[i]);
                }

                BoundingBoxXYZ body = terminal.get_BoundingBox(null);

                double[] centre = body == null
                    ? null
                    : P((body.Min + body.Max) / 2);

                // Its own points, for "beside a unit"
                var points = new List<double[]>();

                if (centre != null)
                    points.Add(centre);

                if (terminal.Location is LocationPoint lp)
                    points.Add(P(lp.Point));

                // 1. what its mouth faces
                int best = -1;
                double bestDistance = -1;
                double mouthZ = 0;
                string mouth = "no direction";

                foreach (Connector c in GetHvacConnectors(terminal))
                {
                    double[] at;
                    double[] direction;

                    try
                    {
                        at = P(c.Origin);
                        direction = VENTFACING.Mouth(P(c.CoordinateSystem.BasisZ), at, centre);
                    }
                    catch
                    {
                        continue;
                    }

                    points.Add(at);

                    if (direction == null)
                        continue;

                    mouthZ = direction[2];
                    mouth = $"mouth ({direction[0]:0.00}, {direction[1]:0.00}, {direction[2]:0.00})";

                    double distance;

                    int hit = VENTFACING.Faced(at, direction, candidates, reach, FacingMarginFeet, out distance);

                    if (hit >= 0 && (best < 0 || distance < bestDistance))
                    {
                        best = hit;
                        bestDistance = distance;
                    }
                }

                // 2. a unit's casing right beside it
                int touch = -1;
                double touchDistance = double.MaxValue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i] == null)
                        continue;

                    foreach (double[] point in points)
                    {
                        double d = VENTFACING.Distance(point, candidates[i]);

                        if (d <= TouchingFeet && d < touchDistance)
                        {
                            touch = i;
                            touchDistance = d;
                        }
                    }
                }

                // 3. the nearest unit within the reach, as an endcap's
                double nearestDistance;

                FamilyInstance nearest = FindNearestEquipment(terminal, candidateUnits, out nearestDistance);

                bool inReach = nearest != null && nearestDistance <= reach;

                string room;
                bool inRoom = terminalRoom.TryGetValue(terminal.Id, out room);

                bool unitServesItsRoom = inReach && inRoom && Served(nearest).Contains(room);

                double air = 0;

                if (inRoom)
                    (isFresh ? sheetFresh : sheetExtract).TryGetValue(room, out air);

                VENTFACING.Reason why;

                VENTFACING.Role role = VENTFACING.Bellmouth(
                    best >= 0, touch >= 0, inReach, mouthZ, inRoom, unitServesItsRoom, air, out why);

                string where = inRoom ? "in " + RoomLabel(allRooms, room) : "in no room in scope";

                if (role == VENTFACING.Role.Feeds)
                {
                    FamilyInstance unit = why == VENTFACING.Reason.Faces ? units[best]
                                        : why == VENTFACING.Reason.Touching ? units[touch]
                                        : nearest;

                    double distance = why == VENTFACING.Reason.Faces ? bestDistance
                                    : why == VENTFACING.Reason.Touching ? touchDistance
                                    : nearestDistance;

                    feeding[terminal.Id] = new Faced { Unit = unit, Distance = distance, Why = why };

                    L($"BELLMOUTH {terminal.Id.ToValue()} '{terminal.Symbol.FamilyName}' {mouth}, {where}: feeds unit {unit.Id.ToValue()} '{unit.Symbol?.FamilyName}' at {distance * 304.8:F0} mm - {VENTFACING.Say(why)}");
                }
                else
                {
                    L($"BELLMOUTH {terminal.Id.ToValue()} '{terminal.Symbol.FamilyName}' {mouth}, {where}: serves its room - {VENTFACING.Say(why)}" +
                      (nearest == null ? string.Empty : $" (nearest unit {nearest.Id.ToValue()} at {nearestDistance * 304.8:F0} mm)"));
                }
            }

            L($"Bellmouths: {bellmouths}, feeding a unit: {feeding.Count}");

            return feeding;
        }

        // Which way a terminal's face points (VENTFACING.Mouth), from its
        // duct connector and its body; null when it cannot say
        private static double[] MouthOf(FamilyInstance terminal)
        {
            double[] centre = null;

            try
            {
                BoundingBoxXYZ body = terminal.get_BoundingBox(null);

                if (body != null)
                    centre = P((body.Min + body.Max) / 2);
            }
            catch
            {
                // no body, no check - the connector alone
            }

            foreach (Connector c in GetHvacConnectors(terminal))
            {
                try
                {
                    double[] mouth = VENTFACING.Mouth(P(c.CoordinateSystem.BasisZ), P(c.Origin), centre);

                    if (mouth != null)
                        return mouth;
                }
                catch
                {
                    // a connector that cannot say
                }
            }

            return null;
        }

        private static XYZ X(double[] point)
        {
            return new XYZ(point[0], point[1], point[2]);
        }

        // The duct systems an element's connectors are on
        private static HashSet<long> SystemsOf(FamilyInstance element)
        {
            var systems = new HashSet<long>();

            foreach (Connector c in GetHvacConnectors(element))
            {
                try
                {
                    if (c.MEPSystem != null)
                        systems.Add(c.MEPSystem.Id.ToValue());
                }
                catch
                {
                    // a connector that cannot say is on no system
                }
            }

            return systems;
        }

        private static double[] P(XYZ point)
        {
            return new[] { point.X, point.Y, point.Z };
        }

        private static FamilyInstance FindNearestEquipment(
            FamilyInstance terminal,
            List<FamilyInstance> equipment,
            out double bestDistance)
        {
            bestDistance = double.MaxValue;
            FamilyInstance best = null;

            // Measure from the terminal's connectors AND its location
            // point: an endcap's only connector sits on its duct side,
            // while the face aimed at the FCU has none
            var terminalPoints = new List<XYZ>();

            foreach (Connector tc in GetHvacConnectors(terminal))
                terminalPoints.Add(tc.Origin);

            LocationPoint lp =
                terminal.Location as LocationPoint;

            if (lp != null)
                terminalPoints.Add(lp.Point);

            if (terminalPoints.Count == 0)
                return null;

            foreach (FamilyInstance eq in equipment)
            {
                foreach (Connector ec in GetHvacConnectors(eq))
                {
                    foreach (XYZ tp in terminalPoints)
                    {
                        double d = ec.Origin.DistanceTo(tp);

                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = eq;
                        }
                    }
                }
            }

            return best;
        }

        private static List<Connector> GetHvacConnectors(
            FamilyInstance instance)
        {
            var result = new List<Connector>();

            if (instance.MEPModel == null ||
                instance.MEPModel.ConnectorManager == null)
                return result;

            foreach (Connector c in
                     instance.MEPModel.ConnectorManager.Connectors)
            {
                if (c.Domain == Domain.DomainHvac)
                    result.Add(c);
            }

            return result;
        }

        private static void Bump(Dictionary<long, int> into, long key)
        {
            int already;

            into[key] = into.TryGetValue(key, out already)
                ? already + 1
                : 1;
        }

        // What to call a room in the report - the number and name the
        // drawing uses, not the internal key
        private static string RoomLabel(
            List<RoomInfo> allRooms, string roomKey)
        {
            foreach (RoomInfo info in allRooms)
            {
                if (info.Key != roomKey)
                    continue;

                string number = (info.RoomNumber ?? string.Empty).Trim();
                string name = (info.RoomName ?? string.Empty).Trim();

                if (number.Length > 0 && name.Length > 0)
                    return number + " " + name;

                return number.Length > 0 ? number : name;
            }

            return roomKey;
        }

        private static List<string> GetServedRoomKeys(
            FamilyInstance fcu,
            Dictionary<ElementId, string> terminalRoom)
        {
            var roomKeys = new List<string>();

            foreach (Connector c in GetHvacConnectors(fcu))
            {
                if (c.DuctSystemType != DuctSystemType.SupplyAir)
                    continue;

                MechanicalSystem sys =
                    c.MEPSystem as MechanicalSystem;

                if (sys == null)
                    continue;

                foreach (Element e in sys.Elements)
                {
                    string roomKey;

                    if (terminalRoom.TryGetValue(e.Id, out roomKey) &&
                        !roomKeys.Contains(roomKey))
                    {
                        roomKeys.Add(roomKey);
                    }
                }
            }

            return roomKeys;
        }
    }
}
