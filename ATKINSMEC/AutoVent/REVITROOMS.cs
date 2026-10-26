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
        // WHAT THIS PASS IS FOR (2026-10-19, VENTFLOWS): the airflow run;
        // a READ of what the terminals carry now, room by room, for Arrange
        // rooms... - every level, unticked rooms too, nothing written, no
        // dialog, no log; or a WRITE of the values typed there, onto the
        // rooms given only. One walk for all three, so the window shows and
        // writes exactly what a run would.
        public enum Mode { Run, Survey, Write }

        public Mode Doing = Mode.Run;

        // Write: each room's air over the sheet's (a NaN side: the sheet's),
        // and the rooms whose fresh air / extract is written
        public Dictionary<string, VENTFLOWS.Air> Overrides;
        public HashSet<string> WriteFresh = new HashSet<string>();
        public HashSet<string> WriteExtract = new HashSet<string>();

        // Write: false when the window's Model wrote - its rooms grouped by
        // the arrangement's merges only (the FCUs'), not the sheet's as well
        public bool SheetMerges = true;

        // Survey: what each room's terminals carry now - its own, and its
        // part of the endcaps on its units; and why, if nothing was read
        public Dictionary<string, VENTFLOWS.Air> Carrying = new Dictionary<string, VENTFLOWS.Air>();
        public string Trouble;

        // Survey: every unit supplying rooms in scope, with its rooms and
        // fresh air connections - for the FCU merge suggestions
        public List<VENTFLOWS.Unit> Units = new List<VENTFLOWS.Unit>();

        // Survey: each room's supply terminals whose ducts reach no unit -
        // marked in Levels and rooms (TraceDucts)
        public Dictionary<string, List<long>> Unreached = new Dictionary<string, List<long>>(StringComparer.Ordinal);

        // A room from one of the selected links. Key is unique across
        // links because room ids repeat between documents.
        private class RoomInfo
        {
            public Room Room { get; set; }
            public BoundingBoxXYZ Bbox { get; set; }
            public double BaseZ { get; set; }          // in its LINK's coordinates

            // Its floor in THIS model's coordinates - what rooms of
            // different links are compared by (RoomAt). BaseZ cannot be:
            // one interior model placed on every floor has the same BaseZ
            // on every floor (KIFAF, 2026-10-19: LEVEL 04A's kitchen and
            // foyer terminals were put in LEVEL 03's rooms)
            public double HostFloor { get; set; }
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
            L($"=== Atkins MEP Toolkit - AUTOVENT {(Doing == Mode.Write ? "write from Levels and rooms" : Doing == Mode.Survey ? "terminals read for Levels and rooms" : "run")} {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");

            try
            {
                RoomsCore(doc);
                L("Run completed normally");
            }
            catch (Exception ex)
            {
                L("EXCEPTION: " + ex);

                // A read for the window says why on the window
                if (Doing == Mode.Survey)
                    Trouble = ex.Message;
                else
                    TaskDialog.Show(
                        "Atkins MEP Toolkit - AUTOVENT error",
                        ex.Message);
            }
            finally
            {
                // A read leaves the last run's log as it was, and keeps its
                // own: what Levels and rooms was shown, and why
                FlushLog(Doing == Mode.Survey ? "ATKINSMEC_survey_log.txt" : "ATKINSMEC_debug_log.txt");
            }
        }

        private void RoomsCore(Autodesk.Revit.DB.Document doc)
        {
            // A read or a write from the window: the window's own model, as
            // every read is (VENTREAD) - the run as it always was
            Autodesk.Revit.DB.Document doc2 = Doing == Mode.Run ? Command.Doc : doc;

            VENTSHEET ventSheet = new VENTSHEET();

            Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData> ventilationData;

            // The window's read and write work with no workbook too: the
            // terminals can be read, and typed values written, without one
            try
            {
                ventilationData = ventSheet.excelsheet();
            }
            catch (Exception ex) when (Doing != Mode.Run)
            {
                L("No sheet read: " + ex.Message);

                ventilationData = new Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData>();
                VENTSHEET.LoadedRows = new List<KeyValuePair<VENTSHEET.SpaceKey, VENTSHEET.AirData>>();
            }

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
                if (Doing == Mode.Survey)
                {
                    Trouble = "no linked model ticked";
                    return;
                }

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

            try
            {
                string measuredOn;
                double hostError = RVTLINKSELECTION.LevelError(doc, out measuredOn);

                L($"  Out by {Metres(hostError)}, measured on {measuredOn}" +
                  (hostError == 0 ? " - none" : " - corrected below"));
            }
            catch (Exception ex)
            {
                L($"  (could not measure this model's levels: {ex.Message})");
            }

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
                L($"Redundant rooms from '{apartments.Label}': {apartments.Holding.Count} with units inside, " +
                  $"{apartments.ByRoom.Count} unit(s) inside them - those redundant rooms take no terminals of their own");

                foreach (string line in apartments.Describe())
                    L("   " + line);

                if (apartments.OtherStorey > 0)
                    L($"   {apartments.OtherStorey} room(s) stood inside a redundant room on another level of this model - not grouped");

                if (apartments.Outside > 0)
                    L($"   {apartments.Outside} interior room(s) stood in NO redundant room - not grouped, and given no airflow; why is in {VENTAPTLOG.Where}");

                foreach (string line in apartments.DescribeSpread())
                    L($"   CHECK: {line} - an architect's room across several redundant rooms?");
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
                    // TOTAL: an interior model nested in another link is
                    // placed through its parent too - as HostElevationOf,
                    // the apartments map and the fixtures all take it
                    InverseTransform = linkInstance.GetTotalTransform().Inverse,
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

                    // The room's floor in THIS model - the bottom of its box
                    // through the link's total transform
                    double hostFloor = RVTLINKSELECTION.HostElevationOf(
                        linkInstance, linkedDoc, room);

                    // The level of THIS model whose band the room's floor
                    // stands in - the same rule the export writes with
                    string levelName = VENTLEVELMAP.NameAt(storeys, hostFloor);

                    if (levelName.Length == 0)
                    {
                        belowEveryLevel++;
                        continue;
                    }

                    // The window's read and write: every level, every room
                    // Arrange rooms... lists - the run, the ticked ones
                    if (Doing == Mode.Run &&
                        selectedLevels.Count > 0 &&
                        !selectedLevels.Contains(levelName.Trim()))
                    {
                        skippedByLevel++;
                        continue;
                    }

                    // Unticked under its level in the window
                    if (Doing == Mode.Run &&
                        RVTLINKSELECTION.ExcludedRooms.Contains(
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
                        HostFloor = hostFloor,
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
                if (Doing == Mode.Survey)
                    return;

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

            // THE HIGHEST FLOOR IN THIS MODEL wins - HostFloor, not BaseZ:
            // BaseZ is in each link's own coordinates, so one interior
            // model placed on every floor ties on every floor, and the tie
            // went to whichever placement was read first - the floor below
            // (KIFAF LEVEL 04A, 2026-10-19). Within one link the order is
            // the same as it always was.
            RoomInfo RoomAt(XYZ point)
            {
                RoomInfo found = null;
                double foundFloor = double.MinValue;

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

                        if (info.HostFloor <= foundFloor)
                            continue;

                        XYZ testPoint = new XYZ(
                            pointInLink.X,
                            pointInLink.Y,
                            info.BaseZ + 2.0);

                        if (info.Room.IsPointInRoom(testPoint))
                        {
                            found = info;
                            foundFloor = info.HostFloor;
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

            // EACH ROOM'S ROW - by the model ID the sheet keeps for it, else
            // by name, number and level, one row to one room (VENTSHEET.
            // Resolve). Two rooms of one name and number used to take one
            // row's air between them (KIFAF, 2026-10-15).
            List<string> sharedRows;

            Dictionary<string, VENTSHEET.AirData> airOf = VENTSHEET.Resolve(
                VENTSHEET.LoadedRows,
                allRooms.Select(r => new KeyValuePair<string, VENTSHEET.SpaceKey>(r.Key, KeyOf(r))).ToList(),
                out sharedRows);

            int byId = allRooms.Count(r => airOf.ContainsKey(r.Key) &&
                                           string.Equals(airOf[r.Key].Id, r.Key, StringComparison.Ordinal));

            L($"Rows found: {airOf.Count} of {allRooms.Count} room(s), {byId} by model ID" +
              (VENTSHEET.Calculated ? "; the workbook had no saved results - worked out by AUTOVENT" : string.Empty));

            foreach (string room in sharedRows)
                L($"  SHARED ROW {RoomLabel(allRooms, room)}: another room has the same name, number and level, and the sheet has no model ID to tell them apart");

            // Each room's air in the sheet, per side - asked by the
            // bellmouth decision, the report and the write
            var sheetFresh = new Dictionary<string, double>();
            var sheetExtract = new Dictionary<string, double>();

            foreach (RoomInfo info in allRooms)
            {
                VENTSHEET.AirData air;

                if (airOf.TryGetValue(info.Key, out air))
                {
                    sheetFresh[info.Key] = air.FreshAir;
                    sheetExtract[info.Key] = air.ExhaustAir;
                }
            }

            // Arrange rooms...' values over the sheet's (Mode.Write)
            if (Overrides != null)
            {
                foreach (KeyValuePair<string, VENTFLOWS.Air> o in Overrides)
                {
                    if (!double.IsNaN(o.Value.Fresh))
                        sheetFresh[o.Key] = o.Value.Fresh;

                    if (!double.IsNaN(o.Value.Extract))
                        sheetExtract[o.Key] = o.Value.Extract;

                    L($"FROM ARRANGE ROOMS {RoomLabel(allRooms, o.Key)}: fresh {VENTFLOWS.Say(o.Value.Fresh)}, extract {VENTFLOWS.Say(o.Value.Extract)}");
                }
            }

            // MERGED ROOMS ARE ONE (user, 2026-10-12): rooms one FCU serves
            // through one fresh air connection - their fresh air or extract
            // cell merged down in the sheet, or merged in Levels and rooms.
            // Their air is added up and given once; everything below works
            // on these groups, a room on its own being a group of one.
            //
            // PER SIDE since 2026-10-22 (user: TA-04-04's kitchen exhaust
            // "is serving only a kitchen", but the FCU's fresh air merge made
            // it the foyer's and the living/dining's too): FRESH AIR groups -
            // the sheet's merged fresh air cells and the arrangement's merges
            // (the Merge button, an FCU's rooms); EXTRACT groups - the sheet's
            // merged extract cells and the arrangement's ExtractMerges (the
            // sheet's, taken in). Every step below takes its own side's.
            var freshSets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var extractSets = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            if (!SheetMerges)
                L("The sheet's merged cells left out: grouped as Levels and rooms' Model showed them");

            foreach (RoomInfo info in allRooms)
            {
                VENTSHEET.AirData air;

                if (!SheetMerges || !airOf.TryGetValue(info.Key, out air))
                    continue;

                foreach (bool fresh in new[] { true, false })
                {
                    string cell = fresh ? air.FreshMerge : air.ExhaustMerge;

                    if (cell == null)
                        continue;

                    Dictionary<string, List<string>> sets = fresh ? freshSets : extractSets;
                    List<string> set;

                    if (!sets.TryGetValue(cell, out set))
                        sets[cell] = set = new List<string>();

                    set.Add(info.Key);
                }
            }

            Dictionary<string, string> freshOf = VENTFCUCHECK.Groups(
                allRooms.Select(r => r.Key),
                freshSets.Values.Cast<IEnumerable<string>>().Concat(VENTARRANGE.Current.Merges));

            Dictionary<string, string> extractOf = VENTFCUCHECK.Groups(
                allRooms.Select(r => r.Key),
                extractSets.Values.Cast<IEnumerable<string>>().Concat(VENTARRANGE.Current.ExtractMerges));

            Dictionary<string, double> groupFresh = VENTFCUCHECK.Totals(sheetFresh, freshOf);
            Dictionary<string, double> groupExtract = VENTFCUCHECK.Totals(sheetExtract, extractOf);

            // What to call a group: its rooms, joined - on its side
            string LabelIn(Dictionary<string, string> groupOf, string group) =>
                string.Join(" + ", allRooms.Where(r => groupOf.TryGetValue(r.Key, out string g) && g == group)
                                           .Select(r => RoomLabel(allRooms, r.Key))
                                           .DefaultIfEmpty(RoomLabel(allRooms, group)));

            string Label(string group) => LabelIn(freshOf, group);
            string XLabel(string group) => LabelIn(extractOf, group);

            foreach (bool fresh in new[] { true, false })
            {
                Dictionary<string, string> groupOf = fresh ? freshOf : extractOf;
                Dictionary<string, double> values = fresh ? groupFresh : groupExtract;

                var merged = groupOf.GroupBy(g => g.Value).Where(g => g.Count() > 1).ToList();

                L($"Merged groups, {(fresh ? "fresh air" : "extract")}: {merged.Count}");

                foreach (var group in merged)
                {
                    values.TryGetValue(group.Key, out double air);

                    L($"  MERGED {LabelIn(groupOf, group.Key)}: {(fresh ? "fresh" : "extract")} {air} l/s");
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

            // The rooms themselves, not their groups - what the window's
            // FCU merges are made of (VENTFLOWS.FcuGroups) - BY THE DUCTS:
            // each supply terminal followed back along its own ducts to the
            // unit(s) they reach (TraceDucts), whichever of the unit's
            // connectors that is. The Revit system only for a unit no
            // terminal's ducts reach.
            var byDucts = new Dictionary<long, Dictionary<string, List<long>>>();
            var onNoUnit = new List<string>();

            foreach (FamilyInstance terminal in allTerminals)
            {
                string room;

                if (!terminalRoom.TryGetValue(terminal.Id, out room) || !IsSupply(terminal))
                    continue;

                DuctTrace trace = TraceDucts(terminal);

                if (trace.Units.Count == 0)
                {
                    List<long> ids;

                    if (!Unreached.TryGetValue(room, out ids))
                        Unreached[room] = ids = new List<long>();

                    ids.Add(terminal.Id.ToValue());

                    onNoUnit.Add($"  terminal {terminal.Id.ToValue()} '{terminal.Symbol?.FamilyName}' in {RoomLabel(allRooms, room)}: " +
                                 (!trace.Connected
                                     ? "connected to nothing"
                                     : "its ducts reach no unit" +
                                       (trace.OpenEnds.Count > 0 ? " - they end open at " + string.Join(", ", trace.OpenEnds) : string.Empty)));
                    continue;
                }

                foreach (long unit in trace.Units)
                {
                    Dictionary<string, List<long>> rooms;

                    if (!byDucts.TryGetValue(unit, out rooms))
                        byDucts[unit] = rooms = new Dictionary<string, List<long>>(StringComparer.Ordinal);

                    Note(rooms, terminal.Id, terminalRoom);
                }
            }

            // ==================================================
            // Units outside the rooms they supply, and their RETURN DUCTS
            // ==================================================
            //
            // User, 2026-10-24 (YANM3's EMCC rooms - VENTFACING.Endcap): an
            // FCU in the corridor supplying a room is fed its fresh air by an
            // endcap beside the return duct coming back from that room, not
            // beside the unit. Such a unit's return ductwork - what its
            // connectors lead to that reaches no supply or fresh air
            // terminal - is where an endcap is measured to as well, with no
            // reach.
            var returnPieces = new List<ReturnPiece>();
            int outsideUnits = 0;

            foreach (FamilyInstance unit in equipment)
            {
                // By its SUPPLY terminals only: an air handling unit whose
                // system reaches the fresh air endcaps "supplies" their rooms
                // too, and its return main would take every endcap near it
                var supplies = new HashSet<string>(SupplyRoomKeys(unit, terminalRoom), StringComparer.Ordinal);

                Dictionary<string, List<long>> ducted;

                if (byDucts.TryGetValue(unit.Id.ToValue(), out ducted))
                    supplies.UnionWith(ducted.Keys);

                if (supplies.Count == 0)
                    continue;

                RoomInfo standing = null;

                BoundingBoxXYZ unitBox = unit.get_BoundingBox(null);

                if (unitBox != null)
                    standing = RoomAt((unitBox.Min + unitBox.Max) / 2);
                else if (unit.Location is LocationPoint unitPoint)
                    standing = RoomAt(unitPoint.Point);

                if (!VENTFACING.Outside(standing?.Key, supplies))
                    continue;

                List<ReturnPiece> pieces = ReturnSide(unit);

                outsideUnits++;
                returnPieces.AddRange(pieces);

                L($"OUTSIDE FCU {unit.Id.ToValue()} '{unit.Symbol?.FamilyName}' stands in " +
                  (standing == null ? "no room in scope" : RoomLabel(allRooms, standing.Key)) +
                  $", supplies {string.Join(", ", supplies.Select(k => RoomLabel(allRooms, k)))}; " +
                  (pieces.Count == 0 ? "no return ductwork" : $"return ductwork: {pieces.Count} piece(s)"));
            }

            L($"Units outside the rooms they supply: {outsideUnits}, with {returnPieces.Count} piece(s) of return ductwork");

            // WHICH UNIT AN ENDCAP FEEDS (VENTFACING.Endcap): the nearest
            // unit within the reach, or a nearer return duct of a unit
            // outside its rooms - fresh air only, as asked
            FamilyInstance EndcapUnit(FamilyInstance endcap, bool fresh, out double distance, out VENTFACING.EndcapBy by, out long piece)
            {
                FamilyInstance unit = FindNearestEquipment(endcap, equipment, out distance);

                double toReturn = double.MaxValue;
                FamilyInstance returnUnit = null;

                piece = 0;

                if (fresh)
                    returnUnit = NearestReturn(endcap, returnPieces, out toReturn, out piece);

                by = VENTFACING.Endcap(unit == null ? double.MaxValue : distance, toReturn, connectorTolerance);

                if (by == VENTFACING.EndcapBy.ReturnDuct)
                {
                    distance = toReturn;
                    return returnUnit;
                }

                return by == VENTFACING.EndcapBy.Reach ? unit : null;
            }

            // ==================================================
            // Bellmouths: into the room, or into an FCU?
            // ==================================================
            //
            // VENTFACING.Bellmouth decides; every bellmouth's answer and
            // why is in the log.
            Dictionary<ElementId, Faced> faced =
                BellmouthsFeeding(allTerminals, equipment, connectorTolerance,
                                  terminalRoom, freshOf, extractOf, groupFresh, groupExtract, allRooms,
                                  (endcap, fresh) => EndcapUnit(endcap, fresh, out _, out _, out _),
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
                if (airOf.ContainsKey(info.Key))
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

                    FamilyInstance nearestUnit = FindNearestEquipment(
                        terminal,
                        equipment,
                        out nearestDistance);

                    L($"  nearest FCU: {(nearestUnit == null ? "none" : nearestUnit.Id.ToValue().ToString())}, distance={(nearestUnit == null ? 0 : nearestDistance * 304.8):F0} mm (tolerance {connectorTolerance * 304.8:F0} mm)");

                    fcu = EndcapUnit(terminal, isFresh, out double boundAt, out VENTFACING.EndcapBy by, out long piece);

                    if (by == VENTFACING.EndcapBy.ReturnDuct)
                        L($"  nearer the return duct of FCU {fcu.Id.ToValue()} (piece {piece}) at {boundAt * 304.8:F0} mm - the unit stands outside the rooms it supplies; the reach measured to its return duct");

                    if (fcu == null)
                    {
                        L("  -> rejected: no FCU within tolerance, no return duct nearer");

                        noFcuFound++;

                        if (nearestUnit != null &&
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

                // As groups - of its side: a merged group's rooms are carried
                // once
                if (isFresh)
                {
                    outsideFresh[terminal] = VENTFCUCHECK.AsGroups(servedRooms, freshOf);

                    Bump(freshEndcapsOn, fcuId);
                }

                if (isExtract)
                {
                    outsideExtract[terminal] = VENTFCUCHECK.AsGroups(servedRooms, extractOf);

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
            var roomsOfFcuExtract = new Dictionary<long, List<string>>();

            var servedRaw = new Dictionary<long, List<string>>();
            var notDucted = new List<string>();

            foreach (FamilyInstance unit in equipment)
            {
                List<string> served =
                    GetServedRoomKeys(unit, terminalRoom);

                Dictionary<string, List<long>> ducted;
                byDucts.TryGetValue(unit.Id.ToValue(), out ducted);

                // Equipment with no supply reaching a room in scope is
                // not an FCU as far as this is concerned - pumps and
                // air handling units drop out here on their own
                if (served.Count > 0)
                {
                    roomsOfFcu[unit.Id.ToValue()] = VENTFCUCHECK.AsGroups(served, freshOf);
                    roomsOfFcuExtract[unit.Id.ToValue()] = VENTFCUCHECK.AsGroups(served, extractOf);
                }

                if (served.Count == 0 && ducted == null)
                    continue;

                servedRaw[unit.Id.ToValue()] = ducted == null ? served : ducted.Keys.ToList();

                // Said where the two disagree, terminal by terminal
                if (ducted == null)
                {
                    notDucted.Add($"  FCU {unit.Id.ToValue()}: no supply terminal's ducts reach it - its Revit system taken: " +
                                  string.Join(", ", served.Select(k => RoomLabel(allRooms, k))));
                }
                else if (!served.All(ducted.ContainsKey) || !ducted.Keys.All(served.Contains))
                {
                    Dictionary<string, List<long>> inSystem = SystemRoomKeys(unit, terminalRoom);

                    string Rooms(IEnumerable<string> keys, Dictionary<string, List<long>> terminals) =>
                        string.Join(", ", keys.Select(k => RoomLabel(allRooms, k) +
                            (terminals.TryGetValue(k, out List<long> ids) ? " (terminal " + string.Join(", ", ids) + ")" : string.Empty)));

                    notDucted.Add($"  FCU {unit.Id.ToValue()}: ducted to {Rooms(ducted.Keys, ducted)}" +
                                  (served.Any(k => !ducted.ContainsKey(k))
                                      ? $"; its Revit system also names {Rooms(served.Where(k => !ducted.ContainsKey(k)), inSystem)} - not ducted to it, left out of its merges"
                                      : string.Empty));
                }
            }

            L($"FCUs whose ducts and Revit system disagree: {notDucted.Count}");

            foreach (string line in notDucted)
                L(line);

            L($"Supply terminals whose ducts reach no unit: {onNoUnit.Count}");

            foreach (string line in onNoUnit)
                L(line);

            // What each unit was found to supply - the rooms behind every
            // "N FCU(s)" in the report
            L($"Units supplying rooms in scope: {roomsOfFcu.Count}");

            foreach (var unit in roomsOfFcu.OrderBy(u => u.Key))
                L($"  FCU {unit.Key}: " + string.Join(", ", unit.Value.Select(Label)));

            // Every room - or merged group - its units, and the endcaps the
            // trace bound to them, PER SIDE (its own groups). Worked out in
            // VENTFCUCHECK, which is Revit-free, so the split can be tested
            // with no model open.
            var none = new Dictionary<long, int>();
            var noAir = new Dictionary<string, double>();

            Dictionary<string, VENTFCUCHECK.Room> tally =
                VENTFCUCHECK.Tally(roomsOfFcu, freshEndcapsOn, none, Label);

            Dictionary<string, VENTFCUCHECK.Room> tallyExtract =
                VENTFCUCHECK.Tally(roomsOfFcuExtract, none, extractEndcapsOn, XLabel);

            // Only rooms the sheet gives air to on the side concerned
            List<VENTFCUCHECK.Room> divided = VENTFCUCHECK.Worth(tally, groupFresh, noAir);
            List<VENTFCUCHECK.Room> dividedExtract = VENTFCUCHECK.Worth(tallyExtract, noAir, groupExtract);

            L($"Endcap split: {tally.Count} room(s) served by equipment, " +
              $"{divided.Count + dividedExtract.Count} with air divided between endcaps or a unit " +
              "that has none");

            foreach (VENTFCUCHECK.Room room in divided)
                L("  " + VENTFCUCHECK.Line(room, groupFresh, noAir));

            foreach (VENTFCUCHECK.Room room in dividedExtract)
                L("  " + VENTFCUCHECK.Line(room, noAir, groupExtract));

            // ==================================================
            // A READ FOR ARRANGE ROOMS... stops here: what each room's
            // terminals carry now, by the walk a run would make
            // ==================================================
            if (Doing == Mode.Survey)
            {
                Carrying = Survey(allRooms, freshOf, extractOf, supplyMap, exhaustMap, outsideFresh, outsideExtract,
                                  groupFresh, groupExtract, sheetFresh, sheetExtract, tally, tallyExtract);

                // Each unit's rooms and fresh air connections
                Units = servedRaw.Select(u =>
                {
                    int fresh;
                    freshEndcapsOn.TryGetValue(u.Key, out fresh);

                    return new VENTFLOWS.Unit { Id = u.Key, Rooms = u.Value, Fresh = fresh };
                }).ToList();

                return;
            }

            // A WRITE FROM ARRANGE ROOMS... writes the groups of the rooms
            // it was given only, per side - an endcap carrying one of them
            // carries the others' sheet air as well, as a run would
            HashSet<string> freshGroups = Doing == Mode.Write ? GroupsOfRooms(WriteFresh, freshOf) : null;
            HashSet<string> extractGroups = Doing == Mode.Write ? GroupsOfRooms(WriteExtract, extractOf) : null;

            bool Writes(string group, HashSet<string> only) => only == null || only.Contains(group);
            bool WritesAny(IEnumerable<string> groups, HashSet<string> only) => only == null || groups.Any(only.Contains);

            Dictionary<string, double> Only(Dictionary<string, double> values, HashSet<string> only) =>
                only == null ? values : values.Where(v => only.Contains(v.Key)).ToDictionary(v => v.Key, v => v.Value);

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

                foreach (RoomInfo info in allRooms)
                {
                    VENTSHEET.AirData airData;

                    if (!airOf.TryGetValue(info.Key, out airData))
                        continue;

                    L($"MATCHED room {info.Key} '{info.RoomName}' | '{info.RoomNumber}' | '{info.LevelName}': Fresh={airData.FreshAir}, Exhaust={airData.ExhaustAir}");

                    matchedRooms++;
                }

                // IN-ROOM TERMINALS, BY GROUP: a room's air shared equally
                // between its terminals on that side - a merged group's
                // total between the terminals standing in any of its rooms
                var freshIn = new Dictionary<string, List<FamilyInstance>>(StringComparer.Ordinal);
                var extractIn = new Dictionary<string, List<FamilyInstance>>(StringComparer.Ordinal);

                foreach (var kvp in supplyMap)
                    Gather(freshIn, freshOf, kvp.Key, kvp.Value);

                foreach (var kvp in exhaustMap)
                    Gather(extractIn, extractOf, kvp.Key, kvp.Value);

                foreach (var group in groupFresh)
                {
                    List<FamilyInstance> supplyTerminals;

                    // A write from Arrange rooms... writes 0 if 0 was typed
                    if (!Writes(group.Key, freshGroups) ||
                        (Doing == Mode.Write ? group.Value < 0 : group.Value <= 0) ||
                        !freshIn.TryGetValue(group.Key, out supplyTerminals) ||
                        supplyTerminals.Count == 0)
                        continue;

                    landedFresh.Add(group.Key);

                    double perTerminal = group.Value / supplyTerminals.Count;

                    foreach (FamilyInstance diffuser in supplyTerminals)
                    {
                        if (WriteFlow(diffuser, perTerminal))
                        {
                            terminalsUpdated++;
                            L($"  WRITE fresh {perTerminal} l/s -> in-room terminal {diffuser.Id.ToValue()} ({Label(group.Key)})");
                        }
                        else
                        {
                            terminalsSkipped++;
                            L($"  SKIP in-room terminal {diffuser.Id.ToValue()}: Flow parameter missing or read-only");
                        }
                    }
                }

                foreach (var group in groupExtract)
                {
                    List<FamilyInstance> exhaustTerminals;

                    if (!Writes(group.Key, extractGroups) ||
                        (Doing == Mode.Write ? group.Value < 0 : group.Value <= 0) ||
                        !extractIn.TryGetValue(group.Key, out exhaustTerminals) ||
                        exhaustTerminals.Count == 0)
                        continue;

                    landedExtract.Add(group.Key);

                    double perTerminal = group.Value / exhaustTerminals.Count;

                    foreach (FamilyInstance grille in exhaustTerminals)
                    {
                        if (WriteFlow(grille, perTerminal))
                        {
                            terminalsUpdated++;
                            L($"  WRITE exhaust {perTerminal} l/s -> in-room terminal {grille.Id.ToValue()} ({XLabel(group.Key)})");
                        }
                        else
                        {
                            terminalsSkipped++;
                            L($"  SKIP in-room terminal {grille.Id.ToValue()}: Flow parameter missing or read-only");
                        }
                    }
                }

                // ENDCAPS: EACH ONE CARRIES ITS SHARE of every room - or
                // merged group - its unit serves: its air divided equally
                // between ALL the endcaps on ALL the units serving it.
                // Nothing is written twice and nothing is lost; one unit
                // with one endcap divides by one, so the ordinary room is
                // untouched. See VENTFCUCHECK.
                Dictionary<string, double> freshValues = groupFresh;
                Dictionary<string, double> extractValues = groupExtract;

                foreach (var kvp in outsideFresh)
                {
                    if (!WritesAny(kvp.Value, freshGroups))
                        continue;

                    double total = VENTFCUCHECK.Carried(
                        kvp.Value, freshValues, tally, true);

                    if (total <= 0 && Doing != Mode.Write)
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
                    if (!WritesAny(kvp.Value, extractGroups))
                        continue;

                    double total = VENTFCUCHECK.Carried(
                        kvp.Value, extractValues, tallyExtract, false);

                    if (total <= 0 && Doing != Mode.Write)
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

                // A write from Arrange rooms...: the rooms it was given only
                nowhereFresh = VENTFCUCHECK.Nowhere(Only(freshValues, freshGroups), landedFresh, Label);

                nowhereExtract = VENTFCUCHECK.Nowhere(Only(extractValues, extractGroups), landedExtract, XLabel);

                t.Commit();
            }

            L($"Air with nowhere to go: {nowhereFresh.Count} room(s) fresh, {nowhereExtract.Count} extract");

            foreach (string line in nowhereFresh)
                L("  NOWHERE fresh " + line);

            foreach (string line in nowhereExtract)
                L("  NOWHERE extract " + line);

            L($"SUMMARY: matchedRooms={matchedRooms}, terminalsUpdated={terminalsUpdated}, terminalsSkipped={terminalsSkipped}");

            // A write from Arrange rooms... says what it wrote and where air
            // found no terminal - short, at the user's request: "do not
            // overexplain things in the autovent addin"
            if (Doing == Mode.Write)
            {
                int rooms = WriteFresh.Union(WriteExtract).Distinct().Count();

                TaskDialog.Show("AUTOVENT",
                    $"From Levels and rooms: {rooms} room(s), {terminalsUpdated} terminal(s) written" +
                    (terminalsSkipped > 0 ? $", {terminalsSkipped} read-only" : string.Empty) + "." +
                    Nowhere("fresh", nowhereFresh) + Nowhere("extract", nowhereExtract) +
                    "\n\nFull details: " + LOGS.PathOf("ATKINSMEC_debug_log.txt"));

                return;
            }

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
            // Rooms the sheet cannot tell apart: whichever row each took is a
            // guess. Updating the sheet gives every room its model ID.
            if (sharedRows.Count > 0)
            {
                report += "\n\n" + sharedRows.Count + " room(s) share a name and number with another on " +
                          "their level - update the sheet (Export tab) so each gets its own row:\n" +
                          string.Join("\n", sharedRows.Take(8).Select(r => "  " + RoomLabel(allRooms, r))) +
                          (sharedRows.Count > 8 ? "\n  ..." : string.Empty);
            }

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
                "\n\nFull details: " + LOGS.PathOf("ATKINSMEC_debug_log.txt");

            TaskDialog.Show("AUTOVENT", report);
        }

        // The groups of these rooms - a room in no group is its own
        private static HashSet<string> GroupsOfRooms(IEnumerable<string> rooms, Dictionary<string, string> groupOf)
        {
            var groups = new HashSet<string>(StringComparer.Ordinal);

            foreach (string room in rooms ?? Enumerable.Empty<string>())
            {
                string group;

                groups.Add(groupOf != null && groupOf.TryGetValue(room, out group) ? group : room);
            }

            return groups;
        }

        // WHAT EACH ROOM'S TERMINALS CARRY NOW (Mode.Survey): its own on
        // each side, and its part of every endcap carrying its group's air
        // (VENTFLOWS.Attribute) - shared between a merged group's rooms by
        // their sheet values (VENTFLOWS.Split). NaN: nothing carries it.
        private static Dictionary<string, VENTFLOWS.Air> Survey(
            List<RoomInfo> allRooms, Dictionary<string, string> freshOf, Dictionary<string, string> extractOf,
            Dictionary<string, List<FamilyInstance>> supplyMap, Dictionary<string, List<FamilyInstance>> exhaustMap,
            Dictionary<FamilyInstance, List<string>> outsideFresh, Dictionary<FamilyInstance, List<string>> outsideExtract,
            Dictionary<string, double> groupFresh, Dictionary<string, double> groupExtract,
            Dictionary<string, double> sheetFresh, Dictionary<string, double> sheetExtract,
            Dictionary<string, VENTFCUCHECK.Room> tally, Dictionary<string, VENTFCUCHECK.Room> tallyExtract)
        {
            var carrying = new Dictionary<string, VENTFLOWS.Air>(StringComparer.Ordinal);

            VENTFLOWS.Air Of(string key)
            {
                VENTFLOWS.Air air;

                if (!carrying.TryGetValue(key, out air))
                    carrying[key] = air = new VENTFLOWS.Air();

                return air;
            }

            double Plus(double had, double more) => double.IsNaN(had) ? more : had + more;

            foreach (RoomInfo info in allRooms)
            {
                List<FamilyInstance> list;

                if (supplyMap.TryGetValue(info.Key, out list) && list.Count > 0)
                    Of(info.Key).Fresh = list.Sum(FlowOf);

                if (exhaustMap.TryGetValue(info.Key, out list) && list.Count > 0)
                    Of(info.Key).Extract = list.Sum(FlowOf);
            }

            foreach (bool fresh in new[] { true, false })
            {
                // Each group's rooms in scope, in the order they were read -
                // this side's groups
                Dictionary<string, string> groupOf = fresh ? freshOf : extractOf;
                var roomsOf = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                foreach (RoomInfo info in allRooms)
                {
                    string group;

                    if (!groupOf.TryGetValue(info.Key, out group))
                        group = info.Key;

                    List<string> members;

                    if (!roomsOf.TryGetValue(group, out members))
                        roomsOf[group] = members = new List<string>();

                    members.Add(info.Key);
                }

                Dictionary<FamilyInstance, List<string>> endcaps = fresh ? outsideFresh : outsideExtract;

                Dictionary<string, double> parts = VENTFLOWS.Attribute(
                    endcaps.Select(e => new KeyValuePair<List<string>, double>(e.Value, FlowOf(e.Key))),
                    fresh ? groupFresh : groupExtract, fresh ? tally : tallyExtract, fresh);

                foreach (KeyValuePair<string, double> part in parts)
                {
                    List<string> rooms;

                    if (!roomsOf.TryGetValue(part.Key, out rooms))
                        continue;

                    foreach (KeyValuePair<string, double> share in VENTFLOWS.Split(part.Value, rooms, fresh ? sheetFresh : sheetExtract))
                    {
                        VENTFLOWS.Air air = Of(share.Key);

                        if (fresh)
                            air.Fresh = Plus(air.Fresh, share.Value);
                        else
                            air.Extract = Plus(air.Extract, share.Value);
                    }
                }
            }

            return carrying;
        }

        // A terminal's flow now, l/s - 0 with no flow to read
        private static double FlowOf(FamilyInstance terminal)
        {
            Parameter flow = terminal.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);

            if (flow == null || !flow.HasValue)
                return 0;

            return UnitUtils.ConvertFromInternalUnits(flow.AsDouble(), UnitTypeId.LitersPerSecond);
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

        private static void FlushLog(string fileName)
        {
            try
            {
                string path = LOGS.PathOf(fileName);

                File.WriteAllText(path, _log.ToString());
            }
            catch
            {
                // logging must never break the add-in
            }
        }

        // A room's terminals added to its group's
        private static void Gather(
            Dictionary<string, List<FamilyInstance>> into,
            Dictionary<string, string> groupOf,
            string room,
            List<FamilyInstance> terminals)
        {
            string group;

            if (!groupOf.TryGetValue(room, out group))
                group = room;

            List<FamilyInstance> list;

            if (!into.TryGetValue(group, out list))
                into[group] = list = new List<FamilyInstance>();

            list.AddRange(terminals);
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
            Dictionary<string, string> freshOf,
            Dictionary<string, string> extractOf,
            Dictionary<string, double> groupFresh,
            Dictionary<string, double> groupExtract,
            List<RoomInfo> allRooms,
            Func<FamilyInstance, bool, FamilyInstance> endcapUnit,
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

            // PER SIDE, what already brings each room its air - for the last
            // test (VENTFACING.Reason.UnitNeedsIt): the rooms (groups) with a
            // diffuser or grille of their own (no bellmouth, no endcap); the
            // terminals in each room but endcaps; the units an endcap is
            // traced to
            var hasOwn = new[] { new HashSet<string>(), new HashSet<string>() };
            var inGroup = new[] { new Dictionary<string, List<ElementId>>(), new Dictionary<string, List<ElementId>>() };
            var endcapOn = new[] { new HashSet<long>(), new HashSet<long>() };

            foreach (FamilyInstance terminal in terminals)
            {
                if (terminal.Symbol == null)
                    continue;

                bool tFresh, tExtract;
                ClassifyTerminal(terminal, out tFresh, out tExtract);

                if (!tFresh && !tExtract)
                    continue;

                int side = tFresh ? 0 : 1;

                if (IsEndcap(terminal))
                {
                    FamilyInstance unit = endcapUnit(terminal, tFresh);

                    if (unit != null)
                        endcapOn[side].Add(unit.Id.ToValue());

                    continue;
                }

                string where;

                if (!terminalRoom.TryGetValue(terminal.Id, out where))
                    continue;

                string g;

                if (!(side == 0 ? freshOf : extractOf).TryGetValue(where, out g))
                    g = where;

                List<ElementId> list;

                if (!inGroup[side].TryGetValue(g, out list))
                    inGroup[side][g] = list = new List<ElementId>();

                list.Add(terminal.Id);

                if (!VENTSETTINGS.IsBellmouth(terminal.Symbol.FamilyName))
                    hasOwn[side].Add(g);
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

                // The units it could feed (VENTFACING.CanFeed): not on its
                // own system, and supplying a room in scope
                var candidates = new List<VENTFACING.Box>();
                var candidateUnits = new List<FamilyInstance>();

                for (int i = 0; i < units.Count; i++)
                {
                    bool can = VENTFACING.CanFeed(systemsOf[i].Overlaps(own), Served(units[i]).Count > 0);

                    candidates.Add(can ? boxes[i] : null);

                    if (can)
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

                // Its room's GROUP on its side - merged rooms are one
                Dictionary<string, string> groupOf = isFresh ? freshOf : extractOf;
                string group = null;

                if (inRoom && !groupOf.TryGetValue(room, out group))
                    group = room;

                bool unitServesItsRoom = inReach && inRoom &&
                                         VENTFCUCHECK.AsGroups(Served(nearest), groupOf).Contains(group);

                double air = 0;

                // A row in the sheet, 0 or not - a room with none is not
                // known to need none of this air
                bool inSheet = inRoom && (isFresh ? groupFresh : groupExtract).TryGetValue(group, out air);

                // The last test: its room has a terminal of its own for this
                // air, and a room the unit supplies needs it and has nothing
                // bringing it - no terminal there, no endcap on the unit
                int mySide = isFresh ? 0 : 1;
                Dictionary<string, double> sideAir = isFresh ? groupFresh : groupExtract;

                bool roomHasOwn = inRoom && hasOwn[mySide].Contains(group);

                bool unitStarved = inReach && !endcapOn[mySide].Contains(nearest.Id.ToValue()) &&
                    VENTFCUCHECK.AsGroups(Served(nearest), groupOf).Any(g =>
                    {
                        double needs;
                        List<ElementId> there;

                        return g != group &&
                               sideAir.TryGetValue(g, out needs) && needs > 0 &&
                               (!inGroup[mySide].TryGetValue(g, out there) || there.All(id => id == terminal.Id));
                    });

                VENTFACING.Reason why;

                VENTFACING.Role role = VENTFACING.Bellmouth(
                    best >= 0, touch >= 0, inReach, mouthZ, inRoom, unitServesItsRoom, air, out why,
                    roomHasOwn, unitStarved, inSheet);

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
                    // A unit within the reach that supplies no room in scope,
                    // nearer than any it could feed: said, or the line reads
                    // as if the unit were not there
                    FamilyInstance any = FindNearestEquipment(terminal, units, out double anyDistance);

                    string leftOut = any != null && anyDistance <= reach && (nearest == null || any.Id != nearest.Id) &&
                                     Served(any).Count == 0
                        ? $"; unit {any.Id.ToValue()} at {anyDistance * 304.8:F0} mm supplies no room in scope - not fed"
                        : string.Empty;

                    L($"BELLMOUTH {terminal.Id.ToValue()} '{terminal.Symbol.FamilyName}' {mouth}, {where}: serves its room - {VENTFACING.Say(why)}" +
                      (nearest == null ? string.Empty : $" (nearest unit {nearest.Id.ToValue()} at {nearestDistance * 304.8:F0} mm)") +
                      leftOut);
                }
            }

            L($"Bellmouths: {bellmouths}, feeding a unit: {feeding.Count}");

            return feeding;
        }

        // A PIECE OF A UNIT'S RETURN DUCTWORK, and how to measure to it: a
        // duct by its line, a fitting or accessory by its box
        private class ReturnPiece
        {
            public FamilyInstance Unit;
            public long Id;
            public Curve Line;
            public VENTFACING.Box Box;
        }

        // What a unit's connectors lead to that reaches no supply or fresh
        // air terminal - ducts, flex, fittings, accessories - walked as
        // TraceDucts walks, connector by connector; a connector whose
        // ductwork reaches one is the supply or fresh air side, left out
        private static List<ReturnPiece> ReturnSide(FamilyInstance unit)
        {
            var side = new List<ReturnPiece>();

            foreach (Connector start in GetHvacConnectors(unit))
            {
                if (!start.IsConnected)
                    continue;

                var found = new List<ReturnPiece>();
                var seen = new HashSet<long> { unit.Id.ToValue() };
                var next = new Queue<Element>();
                bool supplies = false;

                void Onwards(Connector from)
                {
                    if (!from.IsConnected)
                        return;

                    foreach (Connector to in from.AllRefs)
                    {
                        Element other = to.Owner;

                        if (other != null && seen.Add(other.Id.ToValue()))
                            next.Enqueue(other);
                    }
                }

                Onwards(start);

                while (next.Count > 0 && !supplies && seen.Count < 2000)
                {
                    Element e = next.Dequeue();
                    long category = e.Category == null ? 0 : e.Category.Id.ToValue();

                    // A supply terminal, or a fresh air one (a ducted fresh air
                    // connection: its main would otherwise be the unit's
                    // "return", endcaps and all)
                    if (category == (long)BuiltInCategory.OST_DuctTerminal)
                    {
                        if (e is FamilyInstance terminal)
                        {
                            ClassifyTerminal(terminal, out bool isFresh, out bool isExtract);

                            if (isFresh || IsSupply(terminal))
                                supplies = true;
                        }

                        continue;
                    }

                    if (category != (long)BuiltInCategory.OST_DuctCurves &&
                        category != (long)BuiltInCategory.OST_FlexDuctCurves &&
                        category != (long)BuiltInCategory.OST_DuctFitting &&
                        category != (long)BuiltInCategory.OST_DuctAccessory)
                    {
                        continue;
                    }

                    var piece = new ReturnPiece { Unit = unit, Id = e.Id.ToValue() };

                    if (e.Location is LocationCurve along && along.Curve != null)
                    {
                        piece.Line = along.Curve;
                    }
                    else
                    {
                        BoundingBoxXYZ box = e.get_BoundingBox(null);

                        if (box != null)
                            piece.Box = new VENTFACING.Box { Min = P(box.Min), Max = P(box.Max) };
                    }

                    if (piece.Line != null || piece.Box != null)
                        found.Add(piece);

                    ConnectorManager connectors = e is MEPCurve curve
                        ? curve.ConnectorManager
                        : (e as FamilyInstance)?.MEPModel?.ConnectorManager;

                    if (connectors == null)
                        continue;

                    foreach (Connector c in connectors.Connectors)
                    {
                        if (c.Domain == Domain.DomainHvac && c.ConnectorType != ConnectorType.Logical)
                            Onwards(c);
                    }
                }

                if (!supplies)
                    side.AddRange(found);
            }

            return side;
        }

        // The return ductwork nearest an endcap - from its connectors and
        // its point, as FindNearestEquipment measures - and whose it is
        private static FamilyInstance NearestReturn(
            FamilyInstance endcap, List<ReturnPiece> pieces, out double best, out long bestPiece)
        {
            best = double.MaxValue;
            bestPiece = 0;

            FamilyInstance unit = null;

            if (pieces == null || pieces.Count == 0)
                return null;

            var points = new List<XYZ>();

            foreach (Connector c in GetHvacConnectors(endcap))
                points.Add(c.Origin);

            if (endcap.Location is LocationPoint lp)
                points.Add(lp.Point);

            foreach (ReturnPiece piece in pieces)
            {
                foreach (XYZ point in points)
                {
                    double d;

                    try
                    {
                        d = piece.Line != null ? piece.Line.Distance(point) : VENTFACING.Distance(P(point), piece.Box);
                    }
                    catch
                    {
                        continue;
                    }

                    if (d < best)
                    {
                        best = d;
                        bestPiece = piece.Id;
                        unit = piece.Unit;
                    }
                }
            }

            return unit;
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

        // The rooms of the supply terminals (IsSupply) on a unit's supply
        // systems - GetServedRoomKeys without the endcaps and bellmouths
        private static List<string> SupplyRoomKeys(
            FamilyInstance fcu,
            Dictionary<ElementId, string> terminalRoom)
        {
            var roomKeys = new List<string>();

            foreach (Connector c in GetHvacConnectors(fcu))
            {
                var sys = c.DuctSystemType == DuctSystemType.SupplyAir ? c.MEPSystem as MechanicalSystem : null;

                if (sys == null)
                    continue;

                foreach (Element e in sys.Elements)
                {
                    string roomKey;

                    if (e is FamilyInstance terminal && terminalRoom.TryGetValue(e.Id, out roomKey) &&
                        !roomKeys.Contains(roomKey) && IsSupply(terminal))
                    {
                        roomKeys.Add(roomKey);
                    }
                }
            }

            return roomKeys;
        }

        // ...and the terminals that put each room there, for the log
        private static Dictionary<string, List<long>> SystemRoomKeys(
            FamilyInstance fcu,
            Dictionary<ElementId, string> terminalRoom)
        {
            var rooms = new Dictionary<string, List<long>>(StringComparer.Ordinal);

            foreach (Connector c in GetHvacConnectors(fcu))
            {
                var sys = c.DuctSystemType == DuctSystemType.SupplyAir ? c.MEPSystem as MechanicalSystem : null;

                if (sys == null)
                    continue;

                foreach (Element e in sys.Elements)
                    Note(rooms, e.Id, terminalRoom);
            }

            return rooms;
        }

        private static void Note(Dictionary<string, List<long>> rooms, ElementId terminal, Dictionary<ElementId, string> terminalRoom)
        {
            string roomKey;

            if (!terminalRoom.TryGetValue(terminal, out roomKey))
                return;

            List<long> ids;

            if (!rooms.TryGetValue(roomKey, out ids))
                rooms[roomKey] = ids = new List<long>();

            if (!ids.Contains(terminal.ToValue()))
                ids.Add(terminal.ToValue());
        }

        // A SUPPLY TERMINAL: one an FCU blows air out of - not an endcap or
        // a bellmouth (fresh air INTO a unit), not fresh air or extract by
        // its system type, its connector supply air or air flowing in
        private static bool IsSupply(FamilyInstance terminal)
        {
            if (terminal.Symbol == null || IsEndcap(terminal) || VENTSETTINGS.IsBellmouth(terminal.Symbol.FamilyName))
                return false;

            bool isFresh;
            bool isExtract;

            ClassifyTerminal(terminal, out isFresh, out isExtract);

            if (isFresh || isExtract)
                return false;

            foreach (Connector c in GetHvacConnectors(terminal))
            {
                if (c.DuctSystemType == DuctSystemType.ReturnAir || c.DuctSystemType == DuctSystemType.ExhaustAir)
                    return false;

                if (c.DuctSystemType == DuctSystemType.SupplyAir || c.Direction == FlowDirectionType.In)
                    return true;
            }

            return false;
        }

        // WHERE A TERMINAL'S DUCTS LEAD (user, 2026-10-21: TA-04-10's powder
        // room "is actually not supplied from the FCU that serves the kitchen
        // and the living/dining"; then the dressing, whose diffuser IS on
        // the bedroom's FCU). Followed back from the terminal along what is
        // CONNECTED to it - ducts, flex ducts, fittings, accessories - to the
        // equipment it reaches, whichever of the unit's connectors that is,
        // never on through another terminal or unit. The Revit system an
        // FCU's supply connector is in can be far wider than its ductwork
        // (KIFAF: one FCU's system reached the mirror apartment's kitchen),
        // and walking out from an FCU's "supply" connectors missed ducts on
        // connectors its family classes otherwise (TA-04-04's kitchen).
        private class DuctTrace
        {
            public readonly HashSet<long> Units = new HashSet<long>();

            // Ductwork with an end connected to nothing - where to look
            public readonly List<long> OpenEnds = new List<long>();

            // The terminal itself joined to anything at all
            public bool Connected;
        }

        private static DuctTrace TraceDucts(FamilyInstance terminal)
        {
            var trace = new DuctTrace();
            var seen = new HashSet<long> { terminal.Id.ToValue() };
            var next = new Queue<Element>();

            void Onwards(Connector from, Element owner)
            {
                if (!from.IsConnected)
                {
                    long id = owner.Id.ToValue();

                    if (owner.Id != terminal.Id && trace.OpenEnds.Count < 5 && !trace.OpenEnds.Contains(id))
                        trace.OpenEnds.Add(id);

                    return;
                }

                foreach (Connector to in from.AllRefs)
                {
                    Element other = to.Owner;

                    if (other != null && seen.Add(other.Id.ToValue()))
                        next.Enqueue(other);
                }
            }

            foreach (Connector c in GetHvacConnectors(terminal))
            {
                if (c.IsConnected)
                    trace.Connected = true;

                Onwards(c, terminal);
            }

            // Bounded: a few thousand pieces is a whole floor's ductwork
            while (next.Count > 0 && seen.Count < 5000)
            {
                Element e = next.Dequeue();
                long category = e.Category == null ? 0 : e.Category.Id.ToValue();

                if (category == (long)BuiltInCategory.OST_MechanicalEquipment)
                {
                    trace.Units.Add(e.Id.ToValue());
                    continue;
                }

                // Only ductwork carries the air on - not another terminal,
                // insulation or lining (MEPCurves in AllRefs too), a system
                if (category != (long)BuiltInCategory.OST_DuctCurves &&
                    category != (long)BuiltInCategory.OST_FlexDuctCurves &&
                    category != (long)BuiltInCategory.OST_DuctFitting &&
                    category != (long)BuiltInCategory.OST_DuctAccessory)
                {
                    continue;
                }

                ConnectorManager connectors = e is MEPCurve curve
                    ? curve.ConnectorManager
                    : (e as FamilyInstance)?.MEPModel?.ConnectorManager;

                if (connectors == null)
                    continue;

                foreach (Connector c in connectors.Connectors)
                {
                    if (c.Domain == Domain.DomainHvac && c.ConnectorType != ConnectorType.Logical)
                        Onwards(c, e);
                }
            }

            return trace;
        }
    }
}
