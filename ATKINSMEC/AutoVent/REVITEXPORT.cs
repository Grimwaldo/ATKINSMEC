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
    // The linked models' rooms into a ventilation worksheet: an update of
    // the imported one (VENTUPDATE - what was typed stays, new rooms are
    // added, changed ones updated), or a new one from the office template
    // (VENTOFFICE).
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
        // it needs a sheet to exist already - it works by cloning the
        // styles off the level and room rows that are there, and says
        // so when there are none. Since 2026-10-14 it UPDATES the sheet
        // rather than replacing its rooms (VENTUPDATE). The other two start from the
        // OFFICE'S OWN WORKBOOK, built into the add-in (VENTOFFICE), so a
        // job that has no sheet yet does not have to go and find last
        // job's to import.
        public enum Way
        {
            // Update the imported worksheet: matched rooms keep what was
            // typed, new rooms are added, removed ones greyed (VENTUPDATE)
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
        public void Export(Autodesk.Revit.DB.Document doc)
        {
            try
            {
                string report = ExportCore();

                // ONE LINE ON SCREEN, at the user's request (2026-10-11):
                // the whole report used to be the dialog. It goes to the
                // log instead, as the airflow run's does (LOGS).
                SaveLog(report);

                TaskDialog.Show(
                    "AUTOVENT",
                    "The sheet has been exported to " + TargetPath +
                    (LastGone > 0
                        ? Environment.NewLine + Environment.NewLine +
                          LastGone + " room(s) no longer in the model - greyed in the sheet."
                        : string.Empty) +
                    (LastFilled > 0
                        ? Environment.NewLine + Environment.NewLine +
                          LastFilled + " occupancy categories filled from room names - check them."
                        : string.Empty));
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "AUTOVENT export failed",
                    ex.Message);
            }
        }

        // %AppData%\ATKINSMEC\ATKINSMEC_export_log.txt - what the export
        // did, in full (on the Desktop until 2026-10-16)
        private static void SaveLog(string report)
        {
            try
            {
                File.WriteAllText(
                    LOGS.PathOf("ATKINSMEC_export_log.txt"),
                    "=== AUTOVENT export " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    " ===\r\n\r\n" + report + "\r\n");
            }
            catch
            {
                // logging must never break the add-in
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
            List<string> storeyOrder;

            // Every room on the ticked levels - the unticked ones too,
            // marked Keep: an update leaves their rows as they are
            List<VENTTEMPLATE.Level> all = CollectRooms(
                doc2,
                out heightsFromCeiling,
                out heightsFromBoundingBox,
                out ceilingsFound,
                out belowEveryLevel,
                out unticked,
                out fixtures,
                out apartments,
                out storeyOrder);

            // ...and the rooms actually written
            List<VENTTEMPLATE.Level> blocks = Written(all);

            LastGone = 0;
            LastFilled = 0;

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
                    "\r\n\r\n" + unticked + " room(s) unticked in Levels and rooms" +
                    (How == Way.IntoImported
                        ? ": their rows were left as they are, and none was added."
                        : " were left out.");
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
                // the update adds one when it is missing
                VENTSHEET.SheetLayout layout =
                    VENTSHEET.DetectLayout(ws, false);

                HashSet<string> ticked = RVTLINKSELECTION.SelectedLevels;

                VENTUPDATE.Outcome done = VENTUPDATE.Update(
                    ws, layout, all,
                    level => ticked.Count == 0 || ticked.Contains((level ?? string.Empty).Trim()),
                    storeyOrder,
                    DateTime.Now.ToString("yyyy-MM-dd"),
                    VENTSETTINGS.Occupancy ? VENTOCCUPANCY.Load() : null);

                LastGone = done.Gone;
                LastFilled = done.Occupancy.Total;

                string report = Updated(ws.Name, layout, done);

                report += heights + Fixtures(fixtures, layout.UnitsCol) +
                          Apartments(apartments, blocks, done.Apartments) + Arranged(blocks) +
                          Occupancy(done.Occupancy, done.DwellingFixed, done.Kitchen);

                package.SaveAs(new FileInfo(TargetPath));

                return report;
            }
        }

        // How many rooms the last update greyed as no longer in the model -
        // said on screen, in one line
        public static int LastGone;

        // The rooms written by the two template ways: none unticked, and no
        // level left with nothing in it
        private static List<VENTTEMPLATE.Level> Written(List<VENTTEMPLATE.Level> all)
        {
            var written = new List<VENTTEMPLATE.Level>();

            foreach (VENTTEMPLATE.Level level in all)
            {
                List<VENTTEMPLATE.Room> rooms = level.Rooms.Where(r => !r.Keep).ToList();

                if (rooms.Count > 0)
                {
                    written.Add(new VENTTEMPLATE.Level
                    {
                        LevelName = level.LevelName,
                        Elevation = level.Elevation,
                        Rooms = rooms
                    });
                }
            }

            return written;
        }

        // What an update did, for the export log
        private static string Updated(string sheet, VENTSHEET.SheetLayout layout, VENTUPDATE.Outcome done)
        {
            var report = new StringBuilder();

            report.AppendLine("Worksheet: " + sheet + " - updated");
            report.AppendLine();
            report.AppendLine($"Rooms matched: {done.Matched} ({done.Changed} changed in the model)");
            report.AppendLine($"Rooms added: {done.Added}");
            report.AppendLine($"Rooms no longer in the model (greyed, kept): {done.Gone}");
            report.AppendLine($"Unticked rooms left as they were: {done.Kept}");
            report.AppendLine($"Levels added: {done.LevelsAdded}");
            report.AppendLine($"Levels not ticked, left as they were: {done.LevelsLeft}");
            report.AppendLine($"Rows: {done.RowsBefore} -> {done.RowsAfter}");

            if (done.Lines.Count > 0)
            {
                report.AppendLine();

                foreach (string line in done.Lines)
                    report.AppendLine("    " + line);
            }

            report.AppendLine();
            report.AppendLine("Columns written from the model:");
            report.AppendLine("    Name: " + VENTROWS.ColName(layout.SpaceNameCol));
            report.AppendLine("    Number: " + VENTROWS.ColName(layout.RoomNumberCol));
            report.AppendLine("    Area (m2): " + VENTROWS.ColName(layout.AreaCol));
            report.AppendLine("    Height (m): " + VENTROWS.ColName(layout.HeightCol));
            report.AppendLine("    No. of Units: " + VENTROWS.ColName(layout.UnitsCol));
            report.AppendLine("Everything else typed in a room's row was kept.");

            if (done.NumberColumnAdded)
                report.AppendLine("A \"Room Number\" column was added at " + VENTROWS.ColName(layout.RoomNumberCol) + ".");

            if (done.KeyColumnAdded)
                report.AppendLine("A hidden \"" + VENTUPDATE.KeyHeading + "\" column was added - the next update matches every room by it.");

            if (done.FilterRemoved)
                report.AppendLine("The sheet's AutoFilter was removed.");

            if (done.Merged > 0)
                report.AppendLine(done.Merged + " merged group(s): their fresh air, or extract, as one total.");

            report.AppendLine();
            report.Append("Saved to: " + TargetPath);

            return report.ToString();
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
                Levels = blocks,
                Occupancy = VENTSETTINGS.Occupancy ? VENTOCCUPANCY.Load() : null
            };

            byte[] template = VENTOFFICE.Template();

            string saved = "\r\n\r\nSaved to: " + TargetPath;

            if (How == Way.NewWorkbook)
            {
                VENTOFFICE.Result made =
                    VENTOFFICE.NewWorkbook(template, job, TargetPath);

                unitsCol = made.UnitsCol;
                groups = made.Apartments;
                LastFilled = made.Occupancy.Total;

                return "A new workbook was written.\r\n\r\n" +
                       VENTOFFICE.Describe(made) +
                       "\r\n\r\nIt also holds the three ASHRAE worksheets " +
                       "the formulas read." +
                       Occupancy(made.Occupancy, made.DwellingFixed, made.Kitchen) +
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
            LastFilled = added.Occupancy.Total;

            return "A worksheet was added; every worksheet already in " +
                   "the workbook was left as it was" +
                   (added.Kitchen.Rates ? ", but for Table 6-4's apartment kitchen rates (below)" : string.Empty) +
                   ".\r\n\r\n" +
                   VENTOFFICE.Describe(added) +
                   Occupancy(added.Occupancy, added.DwellingFixed, added.Kitchen) +
                   saved;
        }

        // How many occupancy categories the last export filled - said on
        // screen, in one line, so they get checked
        public static int LastFilled;

        // What the occupancy categories did, for the export log
        private static string Occupancy(VENTOCCUPANCY.Filled filled, int dwellingFixed, VENTOCCUPANCY.Kitchen kitchen)
        {
            var said = new StringBuilder();

            said.Append("\r\n\r\nOccupancy categories: ");

            if (!VENTSETTINGS.Occupancy)
            {
                said.Append("not filled (switched off in the window).");
            }
            else
            {
                said.Append(filled.Fresh + " outside air (Table 6-1), " + filled.Exhaust +
                            " exhaust (Table 6-4) filled from room names, into blank cells only.");

                foreach (KeyValuePair<string, int> given in filled.Given.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                    said.Append("\r\n    " + given.Key + (given.Value > 1 ? "   x" + given.Value : string.Empty));

                if (filled.Missing.Count > 0)
                {
                    said.Append("\r\n    Not in this sheet's lists, so not written: " +
                                string.Join(", ", filled.Missing.OrderBy(m => m)));
                }

                if (filled.Apartments > 0)
                {
                    said.Append("\r\n\r\n" + filled.Apartments + " redundant room(s) given their number of bedrooms and " +
                                filled.Occupants + " occupant(s) in all: 2 in the master bedroom, 1 in each other.");
                }

                if (filled.NoUnits.Count > 0)
                {
                    said.Append("\r\n\r\n" + filled.NoUnits.Count + " room(s) exhausted per fixture with no " +
                                "\"No. of Units\" - 0 L/s until the fixtures are counted or typed:\r\n    " +
                                string.Join(", ", filled.NoUnits.Distinct().OrderBy(n => n)));
                }
            }

            if (dwellingFixed > 0)
            {
                said.Append("\r\n\r\nThe \"Dwelling Unit\" test in the room formulas pointed at a cell " +
                            "that does not hold \"Dwelling Unit\" (as in the office's own workbook); " +
                            "it now reads Table 6-1 itself, so a dwelling takes its occupants " +
                            "from \"No. of Occupants\".");
            }

            if (kitchen != null && (kitchen.Rates || kitchen.Formulas > 0))
            {
                said.Append("\r\n\r\n\"" + VENTOCCUPANCY.ApartmentKitchen + "\" is calculated per unit, as " +
                            "\"Toilets - private\": No. of Units x 25 L/s for light use, 50 L/s otherwise.");

                if (kitchen.Rates)
                    said.Append(" Table 6-4's row for it was empty; it was given those rates.");

                if (kitchen.Formulas > 0)
                    said.Append(" " + kitchen.Formulas + " room formula(s) now name it beside \"Toilets - private\".");
            }

            return said.ToString();
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
                          tally.Models + " model(s)" +
                          (tally.Placements > tally.Models ? " in " + tally.Placements + " placements" : string.Empty) +
                          (tally.ModelNames.Count == 0 ? string.Empty : " (" + string.Join(", ", tally.ModelNames) + ")");

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
            out VENTAPARTMENTS.Map apartments,
            out List<string> storeyOrder)
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

            // This model's levels, bottom up - where an update puts a level
            // the sheet has not got yet
            storeyOrder = storeys.Select(s => s.Name).ToList();

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

                List<RVTLINKSELECTION.CeilingBox> ceilings = RVTLINKSELECTION.Ceilings(linkedDoc);

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

                    // Unticked under its level in the window: carried as
                    // KEEP - an update leaves its row as it is, the other
                    // exports leave it out (Written)
                    bool keep = RVTLINKSELECTION.ExcludedRooms.Contains(roomKey);

                    if (keep)
                        unticked++;

                    VENTAPARTMENTS.Apartment apartment = apartments.Of(roomKey);

                    // In a dwelling, grouped under it or not - what its
                    // occupancy category is guessed by (VENTOCCUPANCY)
                    bool inDwelling = apartment != null;

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

                    double heightM = RVTLINKSELECTION.RoomHeightM(
                        room,
                        ceilings,
                        out fromCeiling);

                    if (keep)
                    {
                        // counted with the unticked, not the heights
                    }
                    else if (fromCeiling)
                        heightsFromCeiling++;
                    else
                        heightsFromBoundingBox++;

                    // Renamed for the sheet in Arrange rooms... - the airflow
                    // write matches by the same name (REVITROOMS)
                    block.Rooms.Add(new VENTTEMPLATE.Room
                    {
                        Key = roomKey,
                        Name = VENTARRANGE.Current.NameOf(roomKey, nameParam == null
                            ? string.Empty
                            : nameParam.AsString()),
                        Number = room.Number,
                        AreaM2 = UnitUtils.ConvertFromInternalUnits(
                            room.Area,
                            UnitTypeId.SquareMeters),
                        HeightM = heightM,
                        Fixtures = FixturesIn(fixtures, linkId, room),
                        ApartmentKey = apartment == null ? null : apartment.Key,
                        Apartment = apartment == null ? null : VENTARRANGE.Current.NameOf(apartment.Key, apartment.Name),
                        ApartmentNumber = apartment == null ? null : apartment.Number,
                        Keep = keep,
                        InDwelling = inDwelling
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

                // Merged rooms - next to each other by now (Together) -
                // have their air written as one total (VENTROWS.MergeAir),
                // each side by its own merges
                foreach (VENTTEMPLATE.Room room in block.Rooms)
                {
                    room.Merge = VENTARRANGE.Current.MergeIdOf(room.Key);
                    room.ExtractMerge = VENTARRANGE.Current.ExtractMergeIdOf(room.Key);
                }
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

            int renamed = arrangement.Names.Keys.Count(k =>
                keys.Contains(k) || blocks.SelectMany(b => b.Rooms).Any(r => r.ApartmentKey == k));

            int levels = blocks.Count(b => arrangement.OrderOf(VENTARRANGE.LevelContainer(b.LevelName)) != null ||
                                           b.Rooms.Any(r => !string.IsNullOrEmpty(r.ApartmentKey) &&
                                                            arrangement.OrderOf(VENTARRANGE.ApartmentContainer(r.ApartmentKey)) != null));

            var said = new List<string>();

            if (levels > 0)
                said.Add(levels + " level(s) reordered");

            if (takenOut > 0)
                said.Add(takenOut + " room(s) ungrouped");

            if (renamed > 0)
                said.Add(renamed + " renamed");

            int merges = arrangement.Merges.Concat(arrangement.ExtractMerges).Count(g => g.Count(keys.Contains) > 1);

            if (merges > 0)
                said.Add(merges + " merged group(s)");

            return said.Count == 0
                ? string.Empty
                : "\r\n\r\nLevels and rooms: " + string.Join(", ", said) + ".";
        }

        // What the apartments did, for the report
        private static string Apartments(VENTAPARTMENTS.Map map, List<VENTTEMPLATE.Level> blocks, int written)
        {
            if (map == null || !map.On)
                return string.Empty;

            int inside = blocks.Sum(b => b.Rooms.Count(r => !string.IsNullOrEmpty(r.ApartmentKey)));

            string said = "\r\n\r\nRedundant rooms from " + map.Label + ": " + written +
                          " written, holding " + inside + " unit(s) from interior models - the " +
                          "redundant room down the Space column, each unit's own name beside it.";

            foreach (string line in map.Describe())
                said += "\r\n    " + line;

            if (map.OtherStorey > 0)
            {
                said += "\r\n    " + map.OtherStorey + " room(s) stood inside a redundant room on ANOTHER " +
                        "level of this model, and were not put under it.";
            }

            // Never silent: an interior room in no apartment is in no sheet
            if (map.Outside > 0)
            {
                said += "\r\n    " + map.Outside + " room(s) of the interior models stood in NO redundant room (" +
                        string.Join(", ", map.OutsideOn.Select(o =>
                            (o.Key >= 0 && o.Key < map.Storeys.Count ? map.Storeys[o.Key].Name : "?") + " " + o.Value)) +
                        ") and are not in the sheet.";
            }

            // One architect's room taking several interior placements' rooms
            // is drawn across several apartments - the groups are wrong
            List<string> spread = map.DescribeSpread();

            if (spread.Count > 0)
            {
                said += "\r\n    CHECK: " + spread.Count + " redundant room(s) took units from more than one interior " +
                        "placement - the architect's room probably runs across several redundant rooms:";

                foreach (string line in spread)
                    said += "\r\n        " + line;
            }

            if (map.OtherStorey > 0 || map.Outside > 0 || inside == 0 || spread.Count > 0)
                said += "\r\n    What each room was found in, and what the other models have there: " + VENTAPTLOG.Where;

            return said;
        }
    }
}
