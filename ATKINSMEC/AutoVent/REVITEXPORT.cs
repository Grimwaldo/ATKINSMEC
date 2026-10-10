using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // Rewrites the level / room block of the selected worksheet from the
    // linked Revit models. Every other column keeps the formulas of the
    // row it was copied from; cells that held a constant are left blank.
    public class REVITEXPORT
    {
        // Where to save, chosen in the form before the event is raised
        public static string TargetPath;

        // Which worksheet of the imported workbook to rewrite. May be a
        // different sheet than the one the airflow data was read from.
        public static string TargetWorksheet;

        // WHICH OF THREE THINGS THE EXPORT BUTTON IS DOING, at the
        // user's request.
        //
        // Filling an imported worksheet was the only one there was, and
        // it needs a sheet to exist already - Rewrite works by cloning
        // the styles off the level and room rows that are there, and
        // says so when there are none. The other two start from the
        // OFFICE'S OWN WORKBOOK, built into the add-in (VENTOFFICE), so a
        // job that has no sheet yet does not have to go and find last
        // job's to import.
        public enum Way
        {
            // Replace the level/room rows of the imported worksheet,
            // keeping its formatting and its formulas
            IntoImported = 0,

            // A brand new workbook: one worksheet in the office layout,
            // and the three ASHRAE worksheets its formulas read
            NewWorkbook,

            // A worksheet in the office layout added to the imported
            // workbook, leaving everything already in it alone
            AddWorksheet
        }

        public static Way How = Way.IntoImported;

        // What to call the worksheet the last two make
        public static string NewWorksheetName;

        // The room and level rows live in VENTTEMPLATE, the row machinery
        // in VENTROWS and the office template in VENTOFFICE - all three
        // Revit-free, so scratchpad/officetest can run them against the
        // real workbook.

        // Footprint of a ceiling plus the elevation of its underside
        private class CeilingBox
        {
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
            public double UnderZ { get; set; }
        }

        public void Export(Autodesk.Revit.DB.Document doc)
        {
            try
            {
                string report = ExportCore();

                TaskDialog.Show("AUTOVENT export", report);
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "AUTOVENT export failed",
                    ex.Message);
            }
        }

        private string ExportCore()
        {
            Autodesk.Revit.DB.Document doc2 = Command.Doc;

            // ONLY THE FIRST WAY NEEDS A WORKBOOK IMPORTED. That is the
            // whole point of the other two.
            if (How != Way.NewWorkbook &&
                string.IsNullOrEmpty(VENTSHEET.sheetpath))
                throw new Exception("Import a workbook first.");

            string worksheetName = string.IsNullOrEmpty(TargetWorksheet)
                ? VENTSHEET.sheetname
                : TargetWorksheet;

            if (How == Way.IntoImported &&
                string.IsNullOrEmpty(worksheetName))
                throw new Exception("No worksheet chosen to export into.");

            if (string.IsNullOrEmpty(TargetPath))
                throw new Exception("No destination file chosen.");

            int heightsFromCeiling;
            int heightsFromBoundingBox;
            int ceilingsFound;
            int belowEveryLevel;
            int unticked;
            VENTFIXTURES.Tally fixtures;
            VENTAPARTMENTS.Map apartments;

            List<VENTTEMPLATE.Level> blocks = CollectRooms(
                doc2,
                out heightsFromCeiling,
                out heightsFromBoundingBox,
                out ceilingsFound,
                out belowEveryLevel,
                out unticked,
                out fixtures,
                out apartments);

            if (blocks.Count == 0)
                throw new Exception(
                    "No rooms found on the selected levels of the selected models." +
                    (belowEveryLevel > 0
                        ? " " + belowEveryLevel + " room(s) sit below this " +
                          "model's lowest level, so no level of it holds them."
                        : string.Empty));

            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");

            string heights =
                "\r\n\r\nHeights:\r\n" +
                $"    to a false ceiling: {heightsFromCeiling}\r\n" +
                $"    full room height (no ceiling found): {heightsFromBoundingBox}\r\n" +
                $"    ceilings in the linked model(s): {ceilingsFound}";

            // Said rather than dropped quietly: a room lower than every
            // level of this model is in none of its levels' bands
            if (belowEveryLevel > 0)
            {
                heights +=
                    "\r\n\r\n" + belowEveryLevel + " room(s) in the linked " +
                    "model(s) sit below this model's lowest level, so no " +
                    "level holds them - they were left out.";
            }

            if (unticked > 0)
            {
                heights +=
                    "\r\n\r\n" + unticked + " room(s) unticked in the " +
                    "levels list were left out.";
            }

            if (How != Way.IntoImported)
            {
                int unitsCol;
                int groups;
                string made = FromTemplate(doc2, blocks, out unitsCol, out groups);

                return made + heights + Fixtures(fixtures, unitsCol) +
                       Apartments(apartments, blocks, groups) + Arranged(blocks);
            }

            using (ExcelPackage package =
                new ExcelPackage(new FileInfo(VENTSHEET.sheetpath)))
            {
                ExcelWorksheet ws =
                    package.Workbook.Worksheets[worksheetName];

                if (ws == null)
                    throw new Exception(
                        "The workbook has no worksheet named \"" +
                        worksheetName + "\".");

                // The target sheet need not have a room number column;
                // one is added when it is missing
                VENTSHEET.SheetLayout layout =
                    VENTSHEET.DetectLayout(ws, false);

                bool addedNumberColumn = false;

                if (layout.RoomNumberCol == 0)
                {
                    int insertedAt = VENTROWS.InsertRoomNumberColumn(ws, layout);

                    // Shift the indices by hand rather than re-detecting:
                    // the new column is still empty, so the structural
                    // search would have nothing to key off.
                    VENTROWS.ShiftColumns(layout, insertedAt);

                    addedNumberColumn = true;
                }

                int groups;
                string report = Rewrite(ws, layout, blocks, out groups);

                if (addedNumberColumn)
                {
                    report +=
                        "\r\n\r\nA \"Room Number\" column was added at " +
                        ExcelCellAddress.GetColumnLetter(layout.RoomNumberCol) +
                        ".";
                }

                report += heights + Fixtures(fixtures, layout.UnitsCol) +
                          Apartments(apartments, blocks, groups) + Arranged(blocks);

                package.SaveAs(new FileInfo(TargetPath));

                return report;
            }
        }

        // ==================================================
        // The two that build the sheet rather than fill one
        // ==================================================

        // Both start from the office's own workbook - see VENTOFFICE
        private static string FromTemplate(
            Autodesk.Revit.DB.Document doc,
            List<VENTTEMPLATE.Level> blocks,
            out int unitsCol,
            out int groups)
        {
            string name = VENTTEMPLATE.CleanName(
                NewWorksheetName, "Ventilation");

            // The title block's project name and job number are the
            // model's; the rest of it is not the model's to know
            ProjectInfo info = doc.ProjectInformation;

            var job = new VENTOFFICE.Job
            {
                SheetName = name,
                ProjectName = info == null ? null : info.Name,
                ProjectNumber = info == null ? null : info.Number,
                Levels = blocks
            };

            byte[] template = VENTOFFICE.Template();

            string saved = "\r\n\r\nSaved to: " + TargetPath;

            if (How == Way.NewWorkbook)
            {
                VENTOFFICE.Result made =
                    VENTOFFICE.NewWorkbook(template, job, TargetPath);

                unitsCol = made.UnitsCol;
                groups = made.Apartments;

                return "A new workbook was written from the office " +
                       "template.\r\n\r\n" +
                       VENTOFFICE.Describe(made) +
                       "\r\n\r\nIt also holds the three ASHRAE worksheets " +
                       "the formulas read, exactly as the office has them." +
                       saved;
            }

            // ADDING TO THE IMPORTED WORKBOOK LEAVES IT ALONE OTHERWISE.
            // Everything already in it keeps its formatting, its
            // formulas and its own worksheets - this only adds one, and
            // an ASHRAE worksheet only if it has not got it.
            VENTOFFICE.Result added = VENTOFFICE.AddTo(
                template, VENTSHEET.sheetpath, job, TargetPath);

            unitsCol = added.UnitsCol;
            groups = added.Apartments;

            return "A worksheet was added in the office layout; every " +
                   "worksheet already in the workbook was left as it " +
                   "was.\r\n\r\n" +
                   VENTOFFICE.Describe(added) +
                   saved;
        }

        // What the fixture count did, said in the report - including
        // when it did nothing, since a blank "No. of Units" column and a
        // count that was never made look the same in Excel
        private static string Fixtures(VENTFIXTURES.Tally tally, int unitsCol)
        {
            if (tally == null)
                return "\r\n\r\nFixtures: not counted (switched off in the window).";

            string said = "\r\n\r\nFixtures into \"No. of Units\": " + tally.Counted +
                          " counted into " + tally.ByRoom.Count + " room(s), from " +
                          tally.Models + " model(s)";

            said += tally.Families.Count == 0
                ? "."
                : " - " + string.Join(", ", tally.Families) + ".";

            if (tally.Outside > 0)
            {
                said += "\r\n    " + tally.Outside + " of that kind stand in no room " +
                        "of the ticked models, and were not counted.";
            }

            if (unitsCol == 0 && tally.Counted > 0)
            {
                said += "\r\n    The worksheet has no \"No. of Units\" column, so " +
                        "the counts were NOT written.";
            }
            else if (unitsCol > 0)
            {
                said += "\r\n    Written to column " +
                        ExcelCellAddress.GetColumnLetter(unitsCol) +
                        "; a room with none is left blank.";
            }

            return said;
        }

        // ==================================================
        // Revit side
        // ==================================================

        private static int FixturesIn(VENTFIXTURES.Tally tally, long linkId, Room room)
        {
            int n;

            return tally != null &&
                   tally.ByRoom.TryGetValue(RVTLINKSELECTION.RoomKey(linkId, room.Id.ToValue()), out n)
                ? n
                : 0;
        }

        private static List<VENTTEMPLATE.Level> CollectRooms(
            Autodesk.Revit.DB.Document doc2,
            out int heightsFromCeiling,
            out int heightsFromBoundingBox,
            out int ceilingsFound,
            out int belowEveryLevel,
            out int unticked,
            out VENTFIXTURES.Tally fixtures,
            out VENTAPARTMENTS.Map apartments)
        {
            heightsFromCeiling = 0;
            heightsFromBoundingBox = 0;
            ceilingsFound = 0;
            belowEveryLevel = 0;
            unticked = 0;

            // Which room stands in which apartment, when the window names
            // an apartments link - see VENTAPARTMENTS. Built first: the
            // fixture count below must not give a WC to the apartment
            // when the toilet it stands in is the room with the row.
            apartments = VENTAPARTMENTS.Build(doc2, "the export");

            // The ticked links, and every interior placement a room inside
            // an apartment was found in - ticked or not
            var ticked = new HashSet<long>(RVTLINKSELECTION.SelectedLinkIds);

            List<long> roomLinks = apartments.RoomLinks(ticked);

            // Water closets and urinals per room, from the models the
            // window chose (the room links unless told otherwise) - see
            // VENTFIXTURES. Counted once, before the rooms are walked.
            fixtures = VENTSETTINGS.CountFixtures
                ? VENTFIXTURES.Count(doc2, RVTLINKSELECTION.SelectedLinkIds, apartments.Holding, roomLinks)
                : null;

            var byLevel = new Dictionary<string, VENTTEMPLATE.Level>(
                StringComparer.OrdinalIgnoreCase);

            HashSet<string> selectedLevels =
                RVTLINKSELECTION.SelectedLevels;

            // THE LEVELS ARE THE MAIN MODEL'S, THE ROOMS ARE THE LINKS'.
            // A linked room lands on the storey of this model it
            // actually sits on - see VENTLEVELMAP.
            List<VENTLEVELMAP.Storey> storeys =
                RVTLINKSELECTION.GetHostLevels(doc2);

            foreach (long linkId in roomLinks)
            {
                RevitLinkInstance linkInstance =
                    doc2.GetElement(REVITCOMPAT.NewId(linkId)) as RevitLinkInstance;

                if (linkInstance == null)
                    continue;

                Autodesk.Revit.DB.Document linkedDoc =
                    linkInstance.GetLinkDocument();

                if (linkedDoc == null)
                    continue;

                List<CeilingBox> ceilings = CollectCeilings(linkedDoc);

                ceilingsFound += ceilings.Count;

                var rooms =
                    new FilteredElementCollector(linkedDoc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0);

                foreach (Room room in rooms)
                {
                    // From an interior placement nobody ticked, only the
                    // rooms that belong in an apartment
                    if (!apartments.Takes(linkId, RVTLINKSELECTION.RoomKey(linkId, room.Id.ToValue()), ticked))
                        continue;

                    double roomZ = RVTLINKSELECTION.HostElevationOf(
                        linkInstance, linkedDoc, room);

                    // The level of THIS model whose band the room's floor
                    // stands in - see VENTLEVELMAP.StoreyOf
                    int at = VENTLEVELMAP.StoreyOf(storeys, roomZ);

                    // Below every level of this model (or it has none):
                    // nowhere to put it, and a heading is not invented
                    if (at < 0)
                    {
                        belowEveryLevel++;
                        continue;
                    }

                    string levelName = storeys[at].Name;

                    if (selectedLevels.Count > 0 &&
                        !selectedLevels.Contains(levelName.Trim()))
                        continue;

                    string roomKey = RVTLINKSELECTION.RoomKey(linkId, room.Id.ToValue());

                    // An apartment with rooms inside is not a row of its
                    // own: its rooms are, under its name
                    if (apartments.Holding.Contains(roomKey))
                        continue;

                    // Unticked under its level in the window
                    if (RVTLINKSELECTION.ExcludedRooms.Contains(roomKey))
                    {
                        unticked++;
                        continue;
                    }

                    VENTAPARTMENTS.Apartment apartment = apartments.Of(roomKey);

                    // Taken out of its apartment in Arrange rooms...: a row
                    // of its own, by its own name (VENTARRANGE)
                    if (apartment != null && VENTARRANGE.Current.Ungrouped.Contains(roomKey))
                        apartment = null;

                    VENTTEMPLATE.Level block;

                    if (!byLevel.TryGetValue(levelName, out block))
                    {
                        block = new VENTTEMPLATE.Level
                        {
                            LevelName = levelName,

                            // The MAIN model's elevation, so the blocks
                            // come out in this model's storey order
                            Elevation = storeys[at].Elevation,
                            Rooms = new List<VENTTEMPLATE.Room>()
                        };

                        byLevel.Add(levelName, block);
                    }

                    Parameter nameParam =
                        room.get_Parameter(BuiltInParameter.ROOM_NAME);

                    bool fromCeiling;

                    double heightM = RoomHeightM(
                        room,
                        ceilings,
                        out fromCeiling);

                    if (fromCeiling)
                        heightsFromCeiling++;
                    else
                        heightsFromBoundingBox++;

                    block.Rooms.Add(new VENTTEMPLATE.Room
                    {
                        Key = roomKey,
                        Name = nameParam == null
                            ? string.Empty
                            : nameParam.AsString(),
                        Number = room.Number,
                        AreaM2 = UnitUtils.ConvertFromInternalUnits(
                            room.Area,
                            UnitTypeId.SquareMeters),
                        HeightM = heightM,
                        Fixtures = FixturesIn(fixtures, linkId, room),
                        ApartmentKey = apartment == null ? null : apartment.Key,
                        Apartment = apartment == null ? null : apartment.Name,
                        ApartmentNumber = apartment == null ? null : apartment.Number
                    });
                }
            }

            List<VENTTEMPLATE.Level> blocks = byLevel.Values.ToList();

            blocks.Sort((a, b) => a.Elevation.CompareTo(b.Elevation));

            // By number, with each apartment's rooms kept together - they
            // are one merged group in the sheet - and then as arranged by
            // hand in Arrange rooms... (VENTARRANGE), the order the levels
            // list shows. Nothing leaves its level or its apartment.
            foreach (VENTTEMPLATE.Level block in blocks)
            {
                block.Rooms = VENTARRANGE.Order(
                    VENTTEMPLATE.Ordered(block.Rooms), block.LevelName, VENTARRANGE.Current,
                    r => r.Key, r => r.ApartmentKey);
            }

            return blocks;
        }

        // What was arranged by hand (Arrange rooms...), for the report - a
        // sheet in an order nobody remembers choosing must never surprise
        private static string Arranged(List<VENTTEMPLATE.Level> blocks)
        {
            VENTARRANGE.Arrangement arrangement = VENTARRANGE.Current;

            if (arrangement == null || arrangement.IsEmpty)
                return string.Empty;

            var keys = new HashSet<string>(blocks.SelectMany(b => b.Rooms).Select(r => r.Key ?? string.Empty));

            int takenOut = arrangement.Ungrouped.Count(keys.Contains);

            int levels = blocks.Count(b => arrangement.OrderOf(VENTARRANGE.LevelContainer(b.LevelName)) != null ||
                                           b.Rooms.Any(r => !string.IsNullOrEmpty(r.ApartmentKey) &&
                                                            arrangement.OrderOf(VENTARRANGE.ApartmentContainer(r.ApartmentKey)) != null));

            if (takenOut == 0 && levels == 0)
                return string.Empty;

            return "\r\n\r\nArranged by hand (Arrange rooms...): " +
                   (levels > 0 ? levels + " level(s) in the order chosen" : "no order changed") +
                   (takenOut > 0 ? ", " + takenOut + " interior room(s) taken out of their apartment as rows of their own" : string.Empty) +
                   ".";
        }

        // What the apartments did, for the report
        private static string Apartments(VENTAPARTMENTS.Map map, List<VENTTEMPLATE.Level> blocks, int written)
        {
            if (map == null || !map.On)
                return string.Empty;

            int inside = blocks.Sum(b => b.Rooms.Count(r => !string.IsNullOrEmpty(r.ApartmentKey)));

            string said = "\r\n\r\nApartments from " + map.Label + ": " + written +
                          " written, holding " + inside + " room(s) from interior models - the " +
                          "apartment down the Space column, each room's own name beside it.";

            foreach (string line in map.Describe())
                said += "\r\n    " + line;

            if (map.OtherStorey > 0)
            {
                said += "\r\n    " + map.OtherStorey + " room(s) stood inside an apartment on ANOTHER " +
                        "level of this model, and were not put under it.";
            }

            // Never silent: an interior room in no apartment is in no sheet
            if (map.Outside > 0)
            {
                said += "\r\n    " + map.Outside + " room(s) of the interior models stood in NO apartment (" +
                        string.Join(", ", map.OutsideOn.Select(o =>
                            (o.Key >= 0 && o.Key < map.Storeys.Count ? map.Storeys[o.Key].Name : "?") + " " + o.Value)) +
                        ") and are not in the sheet.";
            }

            // One architect's room taking several interior placements' rooms
            // is drawn across several apartments - the groups are wrong
            List<string> spread = map.DescribeSpread();

            if (spread.Count > 0)
            {
                said += "\r\n    CHECK: " + spread.Count + " apartment(s) took rooms from more than one interior " +
                        "placement - the architect's room probably runs across several apartments:";

                foreach (string line in spread)
                    said += "\r\n        " + line;
            }

            if (map.OtherStorey > 0 || map.Outside > 0 || inside == 0 || spread.Count > 0)
                said += "\r\n    What each room was found in, and what the other models have there: " + VENTAPTLOG.Where;

            return said;
        }

        private static List<CeilingBox> CollectCeilings(
            Autodesk.Revit.DB.Document linkedDoc)
        {
            var result = new List<CeilingBox>();

            var ceilings =
                new FilteredElementCollector(linkedDoc)
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType();

            foreach (Element ceiling in ceilings)
            {
                BoundingBoxXYZ bbox = ceiling.get_BoundingBox(null);

                if (bbox == null)
                    continue;

                result.Add(new CeilingBox
                {
                    MinX = bbox.Min.X,
                    MaxX = bbox.Max.X,
                    MinY = bbox.Min.Y,
                    MaxY = bbox.Max.Y,

                    // The box starts at the ceiling's underside, which
                    // is the surface the clear height is measured to
                    UnderZ = bbox.Min.Z
                });
            }

            return result;
        }

        // Floor to the underside of the false ceiling above the room.
        // Without a ceiling, the room's own bounding box height is used.
        private static double RoomHeightM(
            Room room,
            List<CeilingBox> ceilings,
            out bool fromCeiling)
        {
            fromCeiling = false;

            BoundingBoxXYZ bbox = room.get_BoundingBox(null);

            if (bbox == null)
                return 0;

            double baseZ = bbox.Min.Z;
            double topZ = bbox.Max.Z;

            XYZ point;

            LocationPoint lp = room.Location as LocationPoint;

            if (lp != null)
            {
                point = lp.Point;
            }
            else
            {
                point = new XYZ(
                    (bbox.Min.X + bbox.Max.X) / 2,
                    (bbox.Min.Y + bbox.Max.Y) / 2,
                    baseZ);
            }

            // ~150 mm, so a ceiling sitting on the floor slab or just
            // above the room's upper limit is not mistaken for one inside
            const double tolerance = 0.5;

            double lowestUnderside = double.MaxValue;

            foreach (CeilingBox ceiling in ceilings)
            {
                if (point.X < ceiling.MinX - 0.1 ||
                    point.X > ceiling.MaxX + 0.1 ||
                    point.Y < ceiling.MinY - 0.1 ||
                    point.Y > ceiling.MaxY + 0.1)
                    continue;

                if (ceiling.UnderZ <= baseZ + tolerance)
                    continue;

                if (ceiling.UnderZ > topZ + tolerance)
                    continue;

                // The false ceiling is the lowest one over the floor
                if (ceiling.UnderZ < lowestUnderside)
                    lowestUnderside = ceiling.UnderZ;
            }

            if (lowestUnderside < double.MaxValue)
            {
                fromCeiling = true;

                return UnitUtils.ConvertFromInternalUnits(
                    lowestUnderside - baseZ,
                    UnitTypeId.Meters);
            }

            return UnitUtils.ConvertFromInternalUnits(
                topZ - baseZ,
                UnitTypeId.Meters);
        }


        // ==================================================
        // Excel side
        // ==================================================

        private static bool DownMerged(ExcelWorksheet ws, int row, int col)
        {
            string merged = ws.MergedCells[row, col];

            return !string.IsNullOrEmpty(merged) &&
                   new ExcelAddress(merged).Start.Row != new ExcelAddress(merged).End.Row;
        }

        private static string Rewrite(
            ExcelWorksheet ws,
            VENTSHEET.SheetLayout layout,
            List<VENTTEMPLATE.Level> blocks,
            out int groups)
        {
            int lastCol = ws.Dimension.Columns;
            int lastRow = ws.Dimension.Rows;
            int nameCol = layout.SpaceNameCol;
            int firstDataRow = layout.FirstDataRow;


            // 1. Extent of the existing level/room block
            int lastDataRow = firstDataRow - 1;

            for (int r = firstDataRow; r <= lastRow; r++)
            {
                if (ws.Cells[r, nameCol].Text.Trim().Length > 0)
                    lastDataRow = r;
            }

            if (lastDataRow < firstDataRow)
                throw new Exception(
                    "The worksheet has no existing level/room rows to replace.");

            // 2. Template rows, captured before anything moves
            int levelRow = 0;
            int spacerRow = 0;
            int roomRow = 0;
            int subTotalRow = 0;

            for (int r = firstDataRow; r <= lastDataRow; r++)
            {
                string text = ws.Cells[r, nameCol].Text.Trim();

                if (VENTSHEET.IsSubTotalRow(text))
                {
                    if (subTotalRow == 0)
                        subTotalRow = r;

                    continue;
                }

                if (text.Length == 0)
                    continue;

                bool isLevel = layout.LevelsByColor
                    ? VENTSHEET.IsLevelFill(ws.Cells[r, nameCol])
                    : ws.Cells[r, layout.RoomNumberCol].Text.Trim().Length == 0;

                if (isLevel)
                {
                    if (levelRow == 0)
                    {
                        levelRow = r;

                        // A blank spacer row often follows the level header
                        if (r + 1 <= lastDataRow &&
                            ws.Cells[r + 1, nameCol].Text.Trim().Length == 0)
                        {
                            spacerRow = r + 1;
                        }
                    }
                }
                else if (roomRow == 0 || (DownMerged(ws, roomRow, nameCol) && !DownMerged(ws, r, nameCol)))
                {
                    // An ordinary room's row, where there is one: an
                    // apartment's first row has its name merged DOWN and
                    // would teach every room the wrong shape
                    roomRow = r;
                }
            }

            if (levelRow == 0 || roomRow == 0)
                throw new Exception(
                    "Could not find a level row and a room row to use as templates.");

            VENTROWS.RowTemplate levelTemplate = VENTROWS.Capture(ws, levelRow, lastCol, nameCol);
            VENTROWS.RowTemplate roomTemplate = VENTROWS.Capture(ws, roomRow, lastCol, nameCol);

            VENTROWS.RowTemplate spacerTemplate = spacerRow == 0
                ? null
                : VENTROWS.Capture(ws, spacerRow, lastCol, nameCol);

            VENTROWS.RowTemplate subTotalTemplate = subTotalRow == 0
                ? null
                : VENTROWS.Capture(ws, subTotalRow, lastCol, nameCol);

            // The room rows the template sub total summed over, so its
            // ranges can be re-pointed at each new block
            int tmplBlockFirst = 0;
            int tmplBlockLast = 0;

            if (subTotalRow > 0)
            {
                for (int r = subTotalRow - 1; r >= firstDataRow; r--)
                {
                    string text = ws.Cells[r, nameCol].Text.Trim();

                    if (text.Length == 0)
                        break;

                    bool isLevel = layout.LevelsByColor
                        ? VENTSHEET.IsLevelFill(ws.Cells[r, nameCol])
                        : ws.Cells[r, layout.RoomNumberCol].Text.Trim().Length == 0;

                    if (isLevel || VENTSHEET.IsSubTotalRow(text))
                        break;

                    tmplBlockFirst = r;

                    if (tmplBlockLast == 0)
                        tmplBlockLast = r;
                }
            }

            // 3. Plan the new rows
            var plan = new List<VENTROWS.PlannedRow>();

            foreach (VENTTEMPLATE.Level block in blocks)
            {
                plan.Add(new VENTROWS.PlannedRow
                {
                    Kind = VENTROWS.RowKind.Level,
                    LevelName = block.LevelName
                });

                if (spacerTemplate != null)
                    plan.Add(new VENTROWS.PlannedRow { Kind = VENTROWS.RowKind.Spacer });

                int blockFirst = firstDataRow + plan.Count;

                foreach (VENTTEMPLATE.Room room in block.Rooms)
                {
                    plan.Add(new VENTROWS.PlannedRow
                    {
                        Kind = VENTROWS.RowKind.Room,
                        Room = room
                    });
                }

                int blockLast = firstDataRow + plan.Count - 1;

                if (subTotalTemplate != null)
                {
                    plan.Add(new VENTROWS.PlannedRow
                    {
                        Kind = VENTROWS.RowKind.SubTotal,
                        BlockFirstRoomRow = blockFirst,
                        BlockLastRoomRow = blockLast
                    });
                }
            }

            // 4. Make the region exactly as long as the plan. Rows are
            //    added or removed at the end of the region so that
            //    formulas elsewhere follow the range.
            int existing = lastDataRow - firstDataRow + 1;
            int needed = plan.Count;

            if (needed > existing)
                ws.InsertRow(lastDataRow + 1, needed - existing, roomRow);
            else if (needed < existing)
                ws.DeleteRow(firstDataRow + needed, existing - needed);

            // 5. Write - into a block cleared of merges first: a sheet
            //    exported with apartments last time carries their names
            //    merged DOWN, and the rows now under them are not theirs
            foreach (string merged in ws.MergedCells.ToList())
            {
                if (string.IsNullOrEmpty(merged))
                    continue;

                var a = new ExcelAddress(merged);

                if (a.Start.Row <= firstDataRow + needed - 1 && a.End.Row >= firstDataRow)
                    ws.Cells[merged].Merge = false;
            }

            int written = 0;

            var roomRows = new List<KeyValuePair<int, VENTTEMPLATE.Room>>();

            for (int i = 0; i < plan.Count; i++)
            {
                VENTROWS.PlannedRow planned = plan[i];
                int row = firstDataRow + i;

                VENTROWS.RowTemplate template;

                switch (planned.Kind)
                {
                    case VENTROWS.RowKind.Level: template = levelTemplate; break;
                    case VENTROWS.RowKind.Spacer: template = spacerTemplate; break;
                    case VENTROWS.RowKind.SubTotal: template = subTotalTemplate; break;
                    default: template = roomTemplate; break;
                }

                VENTROWS.ApplyTemplate(ws, row, template, lastCol);

                if (planned.Kind == VENTROWS.RowKind.SubTotal)
                {
                    VENTROWS.ApplySubTotalFormulas(
                        ws,
                        row,
                        template,
                        lastCol,
                        subTotalRow,
                        tmplBlockFirst,
                        tmplBlockLast,
                        planned.BlockFirstRoomRow,
                        planned.BlockLastRoomRow);

                    ws.Cells[row, nameCol].Value = "Sub Total";
                    continue;
                }

                if (planned.Kind == VENTROWS.RowKind.Level)
                {
                    ws.Cells[row, nameCol].Value = planned.LevelName;
                    continue;
                }

                if (planned.Kind == VENTROWS.RowKind.Spacer)
                    continue;

                // Room rows are written in plain text: the source sheet
                // colours some of them (red, for instance) and that
                // highlighting is not carried over to exported rooms
                VENTROWS.ClearFontColor(ws, row, lastCol);

                ws.Cells[row, nameCol].Value = planned.Room.Name;

                if (layout.RoomNumberCol > 0)
                    ws.Cells[row, layout.RoomNumberCol].Value = planned.Room.Number;

                if (layout.AreaCol > 0)
                    ws.Cells[row, layout.AreaCol].Value =
                        Math.Round(planned.Room.AreaM2, 2);

                if (layout.HeightCol > 0)
                    ws.Cells[row, layout.HeightCol].Value =
                        Math.Round(planned.Room.HeightM, 2);

                // Water closets and urinals; a room with none keeps
                // whatever the worksheet had there
                if (layout.UnitsCol > 0 && planned.Room.Fixtures > 0)
                    ws.Cells[row, layout.UnitsCol].Value = planned.Room.Fixtures;

                roomRows.Add(new KeyValuePair<int, VENTTEMPLATE.Room>(row, planned.Room));

                written++;
            }

            // Apartments, in the office's Tower-1 pattern - the same
            // writer the office-template export uses
            groups = VENTROWS.Apartments(ws, layout, roomRows);

            // The rewritten worksheet is left without an AutoFilter
            bool filterRemoved = VENTROWS.RemoveAutoFilter(ws);

            // Open at the top, not wherever the sheet was saved scrolled to
            VENTROWS.Home(ws);

            var report = new StringBuilder();

            report.AppendLine("Worksheet: " + ws.Name);
            report.AppendLine();
            report.AppendLine($"Levels written: {blocks.Count}");
            report.AppendLine($"Rooms written: {written}");
            report.AppendLine(
                $"Rows replaced: {existing} -> {needed}");
            report.AppendLine();
            report.AppendLine("Columns written:");
            report.AppendLine("    Name: " + VENTROWS.ColName(nameCol));
            report.AppendLine("    Number: " + VENTROWS.ColName(layout.RoomNumberCol));
            report.AppendLine("    Area (m2): " + VENTROWS.ColName(layout.AreaCol));
            report.AppendLine("    Height (m): " + VENTROWS.ColName(layout.HeightCol));
            report.AppendLine("    No. of Units: " + VENTROWS.ColName(layout.UnitsCol));
            report.AppendLine();
            report.AppendLine(
                subTotalTemplate == null
                    ? "No Sub Total template row found; none written."
                    : "Sub Total rows rebuilt per level.");

            if (filterRemoved)
                report.AppendLine("The sheet's AutoFilter was removed.");
            report.AppendLine();
            report.Append("Saved to: " + TargetPath);

            return report.ToString();
        }
    }
}
