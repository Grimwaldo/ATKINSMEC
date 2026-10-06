using OfficeOpenXml;
using OfficeOpenXml.DataValidation;
using OfficeOpenXml.DataValidation.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ATKINSMEC
{
    // THE OFFICE'S OWN VENTILATION WORKBOOK, AS THE TEMPLATE.
    //
    // At the user's request the export no longer builds a sheet of its
    // own design (that was VENTTEMPLATE.Build, now gone). It starts from
    // the office's real calculation workbook - "API TWIN TOWER PODIUM
    // VENTILATION CALCULATIONS" - which rides inside the add-in as an
    // embedded resource (Resources\VentTemplate.xlsx, pinned to
    // ResourceName in the csproj), and it writes:
    //
    //   - a worksheet laid out EXACTLY like the MLCP / Tower worksheets:
    //     same title block, same headings, same colours, every formula
    //     in every column, plus one more column - Room Number;
    //   - one blue level row per level ticked in the window, with its
    //     rooms under it and a Sub Total after them, then the Total;
    //   - the room's name, number, area and height from Revit, and
    //     every other cell that held a TYPED value left blank;
    //   - the three ASHRAE worksheets the formulas look things up in,
    //     untouched.
    //
    // Deliberately REVIT-FREE, so scratchpad/officetest can run it
    // against the real workbook and read the result straight back with
    // the real VENTSHEET. The sheet is only useful if AUTOVENT can
    // import it again, and nothing but a round trip proves that.
    //
    // WHY THE WORKBOOK IS COPIED RATHER THAN DESCRIBED. The office sheet
    // is two hundred rows of thirty-odd formulas that look up two ASHRAE
    // tables; re-typing any of it here would be a second copy of the
    // office's calculation that nobody in the office could see or check.
    // Taking theirs means a revised calculation is a revised file, not a
    // revised add-in.
    public static class VENTOFFICE
    {
        public const string ResourceName = "ATKINSMEC.VentTemplate.xlsx";

        // Kept exactly as they are - the formulas look things up in them
        public static readonly string[] Kept =
        {
            "ASHRAE 62.1 - Table 6-1",
            "ASHRAE 62.1 - Table 6-4",
            "ASHRAE Reference"
        };

        // The worksheets that carry the office layout, in the order they
        // are tried. The user named MLCP, Tower 1 and Tower 2 as equally
        // good; they share one layout to the column, but NOT one set of
        // formulas, and that decides the order:
        //
        //   - in MLCP somebody has typed over "Total Exhaust (Max. with
        //     rounded up value)" - the very column AUTOVENT reads the
        //     exhaust back from - so a sheet copied from MLCP leaves it
        //     empty, where the Towers still CALCULATE it;
        //   - MLCP's S.No is typed and the Towers' is a running number;
        //   - MLCP's "Airflow for Balancing" is =fresh x 0.9, which is
        //     #VALUE! on every room until an occupancy is picked.
        //
        // Found by comparing what each one's rooms have in common, in
        // scratchpad/officetest. ("Tower 1", WITH a space, is an older
        // layout with different columns and is deliberately not here.)
        public static readonly string[] Patterns =
        {
            "Tower 2",
            "Tower-1",
            "MLCP"
        };

        public class Job
        {
            public string SheetName { get; set; }
            public string ProjectName { get; set; }
            public string ProjectNumber { get; set; }
            public List<VENTTEMPLATE.Level> Levels { get; set; }

            // Which of Patterns to copy; null means the first one found
            public string Pattern { get; set; }
        }

        public class Result
        {
            public string Sheet { get; set; }
            public string Pattern { get; set; }
            public int Levels { get; set; }
            public int Rooms { get; set; }
            public int RowsBefore { get; set; }
            public int RowsAfter { get; set; }
            public int RoomNumberCol { get; set; }

            // Where the fixture counts went; 0 = the sheet has no such column
            public int UnitsCol { get; set; }

            // How many apartment groups were written
            public int Apartments { get; set; }

            public List<string> Brought { get; } = new List<string>();
            public List<string> Notes { get; } = new List<string>();
        }

        // ==================================================
        // The template
        // ==================================================

        public static byte[] Template()
        {
            using (Stream stream = typeof(VENTOFFICE).Assembly
                       .GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    throw new Exception(
                        "The office ventilation template is missing from " +
                        "the add-in (" + ResourceName + "). It is built " +
                        "in from Resources\\VentTemplate.xlsx - rebuild " +
                        "the add-in.");
                }

                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }

        // ==================================================
        // The two ways in
        // ==================================================

        // A brand new workbook: the pattern worksheet becomes the new
        // one, and the ASHRAE worksheets are the only others kept
        public static Result NewWorkbook(byte[] template, Job job, string path)
        {
            using (var package = new ExcelPackage(new MemoryStream(template)))
            {
                ExcelWorkbook wb = package.Workbook;

                ExcelWorksheet ws = PatternOf(wb, job.Pattern);

                string pattern = ws.Name;
                string name = job.SheetName;

                if (Kept.Any(k => string.Equals(
                        k, name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new Exception(
                        "\"" + name + "\" is the name of one of the ASHRAE " +
                        "worksheets the formulas read from. Choose another " +
                        "name.");
                }

                foreach (ExcelWorksheet other in wb.Worksheets.ToList())
                {
                    if (other != ws && !IsKept(other.Name))
                        wb.Worksheets.Delete(other);
                }

                ws.Name = name;
                wb.Worksheets.MoveToStart(name);

                Tidy(wb);

                Result result = Fill(ws, job);

                result.Pattern = pattern;

                Show(wb, ws);

                package.SaveAs(new FileInfo(path));

                return result;
            }
        }

        // A new worksheet in a workbook somebody already has. Everything
        // in it is left alone; the ASHRAE worksheets are brought across
        // only if it has not got them, because the formulas need them.
        public static Result AddTo(
            byte[] template,
            string workbookPath,
            Job job,
            string path)
        {
            using (var source = new ExcelPackage(new MemoryStream(template)))
            using (var package = new ExcelPackage(new FileInfo(workbookPath)))
            {
                ExcelWorkbook wb = package.Workbook;

                // A silent overwrite here would destroy somebody's
                // worksheet, so it is refused and named
                if (wb.Worksheets[job.SheetName] != null)
                {
                    throw new Exception(
                        "That workbook already has a worksheet called \"" +
                        job.SheetName + "\". Choose another name - nothing " +
                        "was changed.");
                }

                // Tidied BEFORE copying, so none of the template's broken
                // names or links to other files come across with it
                Tidy(source.Workbook);

                ExcelWorksheet pattern = PatternOf(source.Workbook, job.Pattern);

                var brought = new List<string>();

                foreach (string kept in Kept)
                {
                    if (wb.Worksheets[kept] != null)
                        continue;

                    ExcelWorksheet from = source.Workbook.Worksheets[kept];

                    if (from == null)
                        continue;

                    ExcelWorksheet copy = wb.Worksheets.Add(kept, from);

                    // The office hides ASHRAE Reference; a copy would
                    // otherwise arrive visible
                    copy.Hidden = from.Hidden;

                    brought.Add(kept);
                }

                ExcelWorksheet ws = wb.Worksheets.Add(job.SheetName, pattern);

                TidyNames(ws.Names);

                Result result = Fill(ws, job);

                result.Pattern = pattern.Name;
                result.Brought.AddRange(brought);

                Show(wb, ws);

                package.SaveAs(new FileInfo(path));

                return result;
            }
        }

        private static ExcelWorksheet PatternOf(ExcelWorkbook wb, string wanted)
        {
            IEnumerable<string> order = string.IsNullOrEmpty(wanted)
                ? Patterns
                : new[] { wanted };

            foreach (string name in order)
            {
                ExcelWorksheet ws = wb.Worksheets[name];

                if (ws != null)
                    return ws;
            }

            throw new Exception(
                "The ventilation template has none of the worksheets it is " +
                "built from (" + string.Join(", ", order) + ").");
        }

        private static bool IsKept(string name)
        {
            return Kept.Any(k => string.Equals(
                k, name, StringComparison.OrdinalIgnoreCase));
        }

        // The new worksheet is the one that opens
        private static void Show(ExcelWorkbook wb, ExcelWorksheet ws)
        {
            try
            {
                foreach (ExcelWorksheet each in wb.Worksheets)
                    each.View.TabSelected = each == ws;

                int active = ws.Index - wb.Worksheets.First().Index;

                wb.View.ActiveTab = active;

                // The office file's tab strip starts scrolled to its third
                // tab, so in a new workbook the new sheet's own tab was
                // scrolled out of sight to the left
                if (wb.View.FirstSheet > active)
                    wb.View.FirstSheet = active;
            }
            catch
            {
                // cosmetic
            }
        }

        // ==================================================
        // Broken names and links to other files
        // ==================================================

        // THE OFFICE WORKBOOK CARRIES TWENTY-THREE DEAD NAMES - "#REF!"
        // left behind by deleted sheets, and two pointing into OTHER
        // workbooks ("[1]AHU!$A$10", "OFFSET([2]Logo!..."). Copied into a
        // new job they are what makes Excel ask about "links to external
        // sources" every time the file opens, and nothing uses them. A
        // name that still resolves is kept.
        private static void Tidy(ExcelWorkbook wb)
        {
            TidyNames(wb.Names);

            foreach (ExcelWorksheet ws in wb.Worksheets)
                TidyNames(ws.Names);

            try
            {
                // With the names gone nothing refers to them
                wb.ExternalLinks.Clear();
            }
            catch
            {
                // an unremovable link is a nuisance, not a failure
            }
        }

        private static void TidyNames(ExcelNamedRangeCollection names)
        {
            foreach (ExcelNamedRange name in names.ToList())
            {
                string text = string.Empty;

                try
                {
                    text = (name.GetFormula() ?? string.Empty) + " " +
                           (name.Address ?? string.Empty);
                }
                catch
                {
                    // unreadable is as good as broken
                    text = "#REF!";
                }

                if (text.Contains("#REF!") || text.Contains("["))
                    names.Remove(name.Name);
            }
        }

        // ==================================================
        // Filling the worksheet
        // ==================================================

        public static Result Fill(ExcelWorksheet ws, Job job)
        {
            var result = new Result { Sheet = ws.Name };

            List<VENTTEMPLATE.Level> levels = (job.Levels ?? new List<VENTTEMPLATE.Level>())
                .Where(l => l != null && l.Rooms != null && l.Rooms.Count > 0)
                .ToList();

            if (levels.Count == 0)
                throw new Exception("There are no rooms to write.");

            if (ws.Dimension == null)
                throw new Exception("Worksheet \"" + ws.Name + "\" is empty.");

            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            if (!layout.LevelsByColor)
            {
                throw new Exception(
                    "Worksheet \"" + ws.Name + "\" does not mark its level " +
                    "rows in blue, so it is not the office layout.");
            }

            // ---- 1. The room number column ------------------------
            if (layout.RoomNumberCol == 0)
            {
                int at = VENTROWS.InsertRoomNumberColumn(ws, layout);
                VENTROWS.ShiftColumns(layout, at);
            }

            result.RoomNumberCol = layout.RoomNumberCol;
            result.UnitsCol = layout.UnitsCol;

            int nameCol = layout.SpaceNameCol;
            int first = layout.FirstDataRow;
            int lastCol = ws.Dimension.End.Column;

            // MLCP and Tower 2 leave the space heading EMPTY. The reader
            // copes, but only by guessing from the data; a heading it can
            // match costs nothing and makes the sheet say what it is.
            if (ws.Cells[layout.HeaderRow, nameCol].Text.Trim().Length == 0)
                ws.Cells[layout.HeaderRow, nameCol].Value = "Space";

            // ---- 2. Where the block ends --------------------------
            int total = TotalRow(ws, first, nameCol);
            int blockEnd = total - 1;

            // ---- 3. What each kind of row looks like --------------
            List<KeyValuePair<int, VENTROWS.RowKind>> kinds =
                Classify(ws, first, blockEnd, nameCol);

            VENTROWS.RowTemplate level = Consensus(ws, kinds, VENTROWS.RowKind.Level, lastCol, nameCol);
            VENTROWS.RowTemplate spacer = Consensus(ws, kinds, VENTROWS.RowKind.Spacer, lastCol, nameCol);
            VENTROWS.RowTemplate room = Consensus(ws, kinds, VENTROWS.RowKind.Room, lastCol, nameCol);

            if (level == null || room == null)
            {
                throw new Exception(
                    "Worksheet \"" + ws.Name + "\" has no level row or no " +
                    "room row to copy the layout from.");
            }

            int subRow;
            int subFirst;
            int subLast;

            VENTROWS.RowTemplate sub = SubTotal(
                ws, kinds, lastCol, nameCol, out subRow, out subFirst, out subLast);

            // The drop-downs, read while the rows they sit on still exist
            Dictionary<int, ListRule> lists = Lists(ws, kinds, room, lastCol);

            // ---- 4. The new rows -----------------------------------
            var plan = new List<VENTROWS.PlannedRow>();
            var subRows = new List<int>();

            int firstRoom = 0;
            int lastRoom = 0;

            foreach (VENTTEMPLATE.Level block in levels)
            {
                plan.Add(new VENTROWS.PlannedRow
                {
                    Kind = VENTROWS.RowKind.Level,
                    LevelName = block.LevelName
                });

                if (spacer != null)
                    plan.Add(new VENTROWS.PlannedRow { Kind = VENTROWS.RowKind.Spacer });

                int blockFirst = first + plan.Count;

                foreach (VENTTEMPLATE.Room each in block.Rooms)
                {
                    plan.Add(new VENTROWS.PlannedRow
                    {
                        Kind = VENTROWS.RowKind.Room,
                        Room = each
                    });
                }

                int blockLast = first + plan.Count - 1;

                if (firstRoom == 0)
                    firstRoom = blockFirst;

                lastRoom = blockLast;

                if (sub != null)
                {
                    subRows.Add(first + plan.Count);

                    plan.Add(new VENTROWS.PlannedRow
                    {
                        Kind = VENTROWS.RowKind.SubTotal,
                        BlockFirstRoomRow = blockFirst,
                        BlockLastRoomRow = blockLast
                    });
                }
            }

            int existing = blockEnd - first + 1;
            int needed = plan.Count;
            int delta = needed - existing;

            result.RowsBefore = existing;
            result.RowsAfter = needed;

            // ---- 5. Everything below that looks into the block -----
            //
            // THE EQUIPMENT SUMMARY UNDER THE TOTAL SUMS THE ROOMS WITH
            // SUMIF, and in the office's own sheets those ranges had
            // drifted - MLCP sums one unit over rows 12:209 and the next
            // over 49:292, which runs into the summary itself. Each is
            // widened to the WHOLE block first, so the resize below
            // carries it to the whole new block, and step 9 trims that to
            // the rooms.
            Rewrite(ws, total + 1, lastCol, formula =>
                Refs.MapAreas(formula, ws.Name, (a, b) =>
                    a >= first && a <= blockEnd
                        ? new[] { first, blockEnd }
                        : null));

            // ---- 6. Make the block exactly as long as the plan -----
            //
            // INSIDE the block, never at its edge: a range that runs over
            // the block only grows or shrinks with it when the rows go in
            // or come out strictly within it. (The fill-an-imported-sheet
            // export adds its rows after the last one, which a range
            // ending on the last room does not follow.)
            if (delta > 0)
                ws.InsertRow(first + 1, delta);
            else if (delta < 0)
                ws.DeleteRow(first + 1, -delta);

            int newEnd = first + needed - 1;
            int newTotal = newEnd + 1;

            // The templates were read before the resize, so anything
            // they point at BELOW the block has moved by delta since
            foreach (VENTROWS.RowTemplate t in new[] { level, spacer, room, sub })
            {
                if (t == null)
                    continue;

                for (int c = 1; c <= lastCol; c++)
                {
                    if (!string.IsNullOrEmpty(t.FormulasR1C1[c]))
                        t.FormulasR1C1[c] = Refs.ShiftR1C1(t.FormulasR1C1[c], blockEnd, delta);
                }
            }

            // ---- 7. Clear the block --------------------------------
            foreach (string merged in ws.MergedCells.ToList())
            {
                if (string.IsNullOrEmpty(merged))
                    continue;

                var a = new ExcelAddress(merged);

                if (a.Start.Row <= newEnd && a.End.Row >= first)
                    ws.Cells[merged].Merge = false;
            }

            ClearValidations(ws, first, newEnd);
            DropWholeRows(ws);

            // ---- 8. Write ------------------------------------------
            int rooms = 0;

            // Which sheet row each room went to - what the apartments pass
            // below groups by
            var roomRows = new List<KeyValuePair<int, VENTTEMPLATE.Room>>();

            for (int i = 0; i < plan.Count; i++)
            {
                VENTROWS.PlannedRow planned = plan[i];
                int row = first + i;

                switch (planned.Kind)
                {
                    case VENTROWS.RowKind.Level:
                        VENTROWS.ApplyTemplate(ws, row, level, lastCol);
                        Put(ws.Cells[row, nameCol], planned.LevelName ?? string.Empty);
                        break;

                    case VENTROWS.RowKind.Spacer:
                        VENTROWS.ApplyTemplate(ws, row, spacer, lastCol);
                        break;

                    case VENTROWS.RowKind.SubTotal:
                        VENTROWS.ApplyTemplate(ws, row, sub, lastCol);

                        VENTROWS.ApplySubTotalFormulas(
                            ws, row, sub, lastCol,
                            subRow, subFirst, subLast,
                            planned.BlockFirstRoomRow,
                            planned.BlockLastRoomRow);

                        Put(ws.Cells[row, nameCol], "Sub Total");
                        break;

                    default:
                        VENTROWS.ApplyTemplate(ws, row, room, lastCol);

                        VENTTEMPLATE.Room r = planned.Room;

                        Put(ws.Cells[row, nameCol], r.Name ?? string.Empty);

                        // AS TEXT, ALWAYS. A room numbered 0123 is not the
                        // number 123, and one Excel has turned into 123
                        // matches nothing on the way back in.
                        ExcelRange number = ws.Cells[row, layout.RoomNumberCol];
                        number.Style.Numberformat.Format = "@";
                        Put(number, r.Number ?? string.Empty);

                        if (layout.AreaCol > 0)
                            Put(ws.Cells[row, layout.AreaCol], Math.Round(r.AreaM2, 2));

                        if (layout.HeightCol > 0)
                            Put(ws.Cells[row, layout.HeightCol], Math.Round(r.HeightM, 2));

                        // Water closets and urinals counted from the
                        // model; a room with none is left blank, as every
                        // typed value in the office sheet is
                        if (layout.UnitsCol > 0 && r.Fixtures > 0)
                            Put(ws.Cells[row, layout.UnitsCol], r.Fixtures);

                        roomRows.Add(new KeyValuePair<int, VENTTEMPLATE.Room>(row, r));

                        rooms++;
                        break;
                }
            }

            // ---- 8b. Apartments, in the office's Tower-1 pattern -------
            result.Apartments = VENTROWS.Apartments(ws, layout, roomRows);

            // ---- 9. The Total, and the SUMIFs under it --------------
            TotalOfSubTotals(ws, newTotal, lastCol, subRows);

            Rewrite(ws, newTotal + 1, lastCol, formula =>
                Refs.MapAreas(formula, ws.Name, (a, b) =>
                    a == first && b == newEnd
                        ? new[] { firstRoom, lastRoom }
                        : null));

            // ---- 10. The rest of the sheet --------------------------
            Title(ws, layout.HeaderRow, lastCol, job, result);
            Leftovers(ws, newTotal + 1, lastCol, result);

            // ---- 11. Drop-downs on every room ----------------------
            AddValidations(ws, plan, first, lists, blockEnd, delta);

            // ---- 12. Finishing -------------------------------------
            VENTROWS.RemoveAutoFilter(ws);

            // THE OFFICE SAVED TOWER 2 IN PAGE BREAK PREVIEW, SCROLLED TO
            // ROW 297, and that came across with the copy. EPPlus freezes
            // panes RELATIVE TO THAT top-left cell, so the frozen "headings"
            // were rows 297-305 and scrolling began under them - a sheet
            // whose rooms end at row 25 opened on nothing but blank cells,
            // with no way to scroll up to them. User-reported as "the
            // exported excel sheet is coming out empty"; every check on
            // the CELLS had passed. So: Normal view, the top, THEN freeze.
            try
            {
                ws.View.PageBreakView = false;
                ws.View.TopLeftCell = "A1";

                // The headings and the room's name and number stay put
                ws.View.FreezePanes(first, layout.RoomNumberCol + 1);

                // The cursor in the SCROLLING pane - the office's was left
                // at AN59, and anything left of the freeze is not in it
                ws.View.SelectedRange =
                    ExcelCellAddress.GetColumnLetter(layout.RoomNumberCol + 1) + first;
            }
            catch
            {
                // cosmetic
            }

            try
            {
                int printTo = ws.PrinterSettings.PrintArea != null
                    ? ws.PrinterSettings.PrintArea.End.Column
                    : lastCol;

                ws.PrinterSettings.PrintArea = ws.Cells[1, 1, newTotal, printTo];
            }
            catch
            {
                // cosmetic
            }

            TidyNames(ws.Names);

            result.Levels = levels.Count;
            result.Rooms = rooms;

            return result;
        }

        // A SUB TOTAL CANNOT BE SETTLED THE WAY A ROOM IS. Each sums its
        // own block, so no two read alike even when they mean the same
        // thing - SUM(J12:J18) and SUM(J22:J29). So each is first written
        // relative to its own rooms (the first room, the last, itself),
        // and in that form a column keeps its formula only where MOST Sub
        // Totals agree on it. The row copied is the one that agrees with
        // the rest most.
        //
        // Needed because the office's are not uniform either: most of
        // Tower 2's carry somebody's scratch sums past the Remarks column
        // ("=AL16+AL17+AL18"), a different set of rooms on every level.
        // Copied from the first, they became nonsense and #VALUE! on
        // every level of the new sheet.
        private static VENTROWS.RowTemplate SubTotal(
            ExcelWorksheet ws,
            List<KeyValuePair<int, VENTROWS.RowKind>> kinds,
            int lastCol,
            int nameCol,
            out int subRow,
            out int subFirst,
            out int subLast)
        {
            subRow = 0;
            subFirst = 0;
            subLast = 0;

            // Each Sub Total with the rooms directly above it
            var subs = new List<int[]>();

            for (int i = 0; i < kinds.Count; i++)
            {
                if (kinds[i].Value != VENTROWS.RowKind.SubTotal)
                    continue;

                int firstRoom = 0;
                int lastRoom = 0;

                for (int j = i - 1; j >= 0 && kinds[j].Value == VENTROWS.RowKind.Room; j--)
                {
                    firstRoom = kinds[j].Key;

                    if (lastRoom == 0)
                        lastRoom = kinds[j].Key;
                }

                subs.Add(new[] { kinds[i].Key, firstRoom, lastRoom });
            }

            if (subs.Count == 0)
                return null;

            // Every Sub Total's formulas, relative to its own block
            var canon = new string[subs.Count][];

            for (int i = 0; i < subs.Count; i++)
            {
                canon[i] = new string[lastCol + 1];

                for (int c = 1; c <= lastCol; c++)
                {
                    string formula = ws.Cells[subs[i][0], c].Formula;

                    if (!string.IsNullOrEmpty(formula))
                        canon[i][c] = Refs.Relative(formula, ws.Name, subs[i][0], subs[i][1], subs[i][2]);
                }
            }

            // What most of them say, column by column
            var agreed = new string[lastCol + 1];

            for (int c = 1; c <= lastCol; c++)
            {
                string most = Most(canon.Select(k => k[c] ?? string.Empty));
                int count = canon.Count(k => (k[c] ?? string.Empty) == most);

                agreed[c] = most.Length > 0 && count * 2 >= subs.Count ? most : null;
            }

            int best = 0;
            int bestScore = -1;

            for (int i = 0; i < subs.Count; i++)
            {
                int score = Enumerable.Range(1, lastCol).Count(c => canon[i][c] == agreed[c]);

                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            subRow = subs[best][0];
            subFirst = subs[best][1];
            subLast = subs[best][2];

            VENTROWS.RowTemplate sub = VENTROWS.Capture(ws, subRow, lastCol, nameCol);
            VENTROWS.RowTemplate look = Consensus(ws, kinds, VENTROWS.RowKind.SubTotal, lastCol, nameCol);

            for (int c = 1; c <= lastCol; c++)
            {
                if (agreed[c] == null || canon[best][c] != agreed[c])
                {
                    sub.FormulasA1[c] = null;
                    sub.FormulasR1C1[c] = null;
                }

                sub.StyleIds[c] = look.StyleIds[c];
            }

            sub.Height = look.Height;

            return sub;
        }

        // A value where a formula may have been
        private static void Put(ExcelRange cell, object value)
        {
            if (!string.IsNullOrEmpty(cell.Formula))
                cell.ClearFormulas();

            cell.Value = value;
        }

        private static int TotalRow(ExcelWorksheet ws, int first, int nameCol)
        {
            int end = ws.Dimension.End.Row;

            for (int r = first; r <= end; r++)
            {
                for (int c = 1; c <= nameCol; c++)
                {
                    string text = Norm(ws.Cells[r, c].Text);

                    if (text == "total" || text == "grand total")
                        return r;
                }
            }

            throw new Exception(
                "Worksheet \"" + ws.Name + "\" has no \"Total\" row, so " +
                "where its rooms end cannot be told.");
        }

        private static List<KeyValuePair<int, VENTROWS.RowKind>> Classify(
            ExcelWorksheet ws, int first, int blockEnd, int nameCol)
        {
            var kinds = new List<KeyValuePair<int, VENTROWS.RowKind>>();

            VENTROWS.RowKind? previous = null;

            for (int r = first; r <= blockEnd; r++)
            {
                ExcelRange cell = ws.Cells[r, nameCol];
                string text = cell.Text.Trim();

                VENTROWS.RowKind? kind = null;

                if (VENTSHEET.IsSubTotalRow(text))
                    kind = VENTROWS.RowKind.SubTotal;
                else if (text.Length > 0 && VENTSHEET.IsLevelFill(cell))
                    kind = VENTROWS.RowKind.Level;
                else if (text.Length == 0 && previous == VENTROWS.RowKind.Level)
                    kind = VENTROWS.RowKind.Spacer;
                else if (text.Length > 0)
                    kind = VENTROWS.RowKind.Room;

                if (kind.HasValue)
                    kinds.Add(new KeyValuePair<int, VENTROWS.RowKind>(r, kind.Value));

                previous = kind;
            }

            return kinds;
        }

        // WHAT A ROOM ROW LOOKS LIKE IS DECIDED BY ALL OF THEM, NOT THE
        // FIRST ONE. The office's rows are not uniform: MLCP marks 26 of
        // its 159 rooms bright green, Tower-1 two in red, and here and
        // there somebody has typed over a formula ("=AL16*1.05", an area
        // of "=68.56+9.2+9+15.05"). Whatever the first row happened to be
        // would be stamped onto every room of the next job. So each
        // column takes the style most of its rows have, and the formula
        // most of them have - where most of them hold a typed value, it
        // is left BLANK, which is what the user asked for.
        //
        // A formula with no cell in it ("=3.9-0.3") is a typed value
        // written with an equals sign, and counts as one.
        private static VENTROWS.RowTemplate Consensus(
            ExcelWorksheet ws,
            List<KeyValuePair<int, VENTROWS.RowKind>> kinds,
            VENTROWS.RowKind kind,
            int lastCol,
            int nameCol)
        {
            List<int> rows = kinds.Where(k => k.Value == kind).Select(k => k.Key).ToList();

            if (rows.Count == 0)
                return null;

            var shape = new VENTROWS.RowTemplate
            {
                StyleIds = new int[lastCol + 1],
                FormulasR1C1 = new string[lastCol + 1],
                FormulasA1 = new string[lastCol + 1],
                Height = Most(rows.Select(r => ws.Row(r).Height))
            };

            for (int c = 1; c <= lastCol; c++)
            {
                var styles = new List<int>();
                var formulas = new List<string>();

                foreach (int r in rows)
                {
                    ExcelRange cell = ws.Cells[r, c];

                    styles.Add(cell.StyleID);

                    string formula = null;

                    if (!string.IsNullOrEmpty(cell.Formula))
                    {
                        string r1c1 = cell.FormulaR1C1;

                        if (Refs.HasReference(r1c1))
                            formula = r1c1;
                    }

                    // A typed value, or nothing, is one bucket
                    formulas.Add(formula ?? string.Empty);
                }

                shape.StyleIds[c] = Most(styles);

                string chosen = Most(formulas);

                shape.FormulasR1C1[c] = chosen.Length == 0 ? null : chosen;
            }

            string merged = ws.MergedCells[rows[0], nameCol];

            if (!string.IsNullOrEmpty(merged))
            {
                var address = new ExcelAddress(merged);

                if (address.Start.Row == address.End.Row)
                {
                    shape.MergeFrom = address.Start.Column;
                    shape.MergeTo = address.End.Column;
                }
            }

            return shape;
        }

        // The commonest; a tie goes to whichever came first
        private static T Most<T>(IEnumerable<T> items)
        {
            var counts = new Dictionary<T, int>();
            var order = new List<T>();

            foreach (T item in items)
            {
                if (counts.ContainsKey(item))
                {
                    counts[item]++;
                }
                else
                {
                    counts[item] = 1;
                    order.Add(item);
                }
            }

            T best = order[0];

            foreach (T item in order)
            {
                if (counts[item] > counts[best])
                    best = item;
            }

            return best;
        }

        // Runs a formula rewrite over every formula from a row down
        private static void Rewrite(
            ExcelWorksheet ws, int fromRow, int lastCol, Func<string, string> map)
        {
            if (ws.Dimension == null || fromRow > ws.Dimension.End.Row)
                return;

            foreach (ExcelRangeBase cell in
                     ws.Cells[fromRow, 1, ws.Dimension.End.Row, lastCol])
            {
                string formula = cell.Formula;

                if (string.IsNullOrEmpty(formula))
                    continue;

                string mapped = map(formula);

                if (mapped != formula)
                    cell.Formula = mapped;
            }
        }

        // THE TOTAL IS REBUILT, NOT SHIFTED. The office's own Total had
        // already lost three of its levels (MLCP's adds rows 61 onward
        // and never 19, 30 or 46), and after a resize a reference to a
        // deleted row is #REF!. So a Total cell that adds up other rows
        // becomes the sum of that column's Sub Totals - all of them. One
        // that only looks along its own row (a ratio) is left alone.
        private static void TotalOfSubTotals(
            ExcelWorksheet ws, int row, int lastCol, List<int> subRows)
        {
            if (subRows.Count == 0)
                return;

            for (int c = 1; c <= lastCol; c++)
            {
                ExcelRange cell = ws.Cells[row, c];
                string formula = cell.Formula;

                if (string.IsNullOrEmpty(formula))
                    continue;

                List<int> rows = Refs.Rows(formula, ws.Name);

                bool broken = formula.Contains("#REF!");

                if (!broken && (rows.Count == 0 || rows.All(r => r == row)))
                    continue;

                string col = ExcelCellAddress.GetColumnLetter(c);

                cell.Formula = string.Join("+", subRows.Select(r => col + r));
            }
        }

        // ==================================================
        // The title block
        // ==================================================

        private static readonly string[] TitleLabels =
        {
            "project", "part of structure", "job ref", "rev", "calc by",
            "date", "checked by", "date of issue"
        };

        // The labels stay; what was typed against them was the API job's
        // and is blanked. The project's name and number come from the
        // model's Project Information and "part of structure" is the new
        // worksheet's name - the rest (who calculated it, who checked
        // it, the dates) is not the model's to know.
        private static void Title(
            ExcelWorksheet ws, int headerRow, int lastCol, Job job, Result result)
        {
            var blank = new List<ExcelRange>();
            var slots = new Dictionary<string, ExcelRange>();

            for (int r = 1; r < headerRow; r++)
            {
                for (int c = 1; c <= lastCol; c++)
                {
                    ExcelRange cell = ws.Cells[r, c];

                    if (!string.IsNullOrEmpty(cell.Formula) || cell.Value == null)
                        continue;

                    string text = Label(cell.Text);

                    if (text.Length == 0)
                        continue;

                    if (!TitleLabels.Contains(text))
                    {
                        blank.Add(cell);
                        continue;
                    }

                    if (text == "project" || text == "part of structure" || text == "job ref")
                    {
                        ExcelRange slot = SlotFor(ws, r, c);

                        if (slot != null && !slots.ContainsKey(text))
                            slots[text] = slot;
                    }
                }
            }

            foreach (ExcelRange cell in blank)
                cell.Value = null;

            Write(slots, "project", Real(job.ProjectName, "Project Name"));
            Write(slots, "job ref", Real(job.ProjectNumber, "Project Number"));
            Write(slots, "part of structure", ws.Name);

            if (Real(job.ProjectName, "Project Name").Length == 0)
            {
                result.Notes.Add(
                    "The model's Project Information has no project name, " +
                    "so the title block's is blank.");
            }
        }

        // What was typed against a label: to its right, past its merge,
        // or failing that under it
        private static ExcelRange SlotFor(ExcelWorksheet ws, int r, int c)
        {
            int right = c + 1;
            int below = r + 1;

            string merged = ws.MergedCells[r, c];

            if (!string.IsNullOrEmpty(merged))
            {
                var a = new ExcelAddress(merged);
                right = a.End.Column + 1;
                below = a.End.Row + 1;
            }

            ExcelRange side = ws.Cells[r, right];
            ExcelRange under = ws.Cells[below, c];

            if (Filled(side))
                return side;

            if (Filled(under))
                return under;

            return null;
        }

        private static bool Filled(ExcelRange cell)
        {
            return cell.Value != null &&
                   cell.Text.Trim().Length > 0 &&
                   !TitleLabels.Contains(Label(cell.Text));
        }

        private static void Write(
            Dictionary<string, ExcelRange> slots, string key, string value)
        {
            ExcelRange slot;

            if (slots.TryGetValue(key, out slot))
                slot.Value = value.Length == 0 ? null : value;
        }

        // Revit's own placeholders are not a project name
        private static string Real(string value, string placeholder)
        {
            string v = (value ?? string.Empty).Trim();

            return string.Equals(v, placeholder, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : v;
        }

        private static string Label(string text)
        {
            return Norm((text ?? string.Empty).Replace(".", " ").Replace(":", " "));
        }

        // ==================================================
        // What is left of the API job below the Total
        // ==================================================

        // THE EQUIPMENT SUMMARY STAYS AND ITS UNITS GO. Its headings, its
        // SUMIFs and its "l/s OA" labels are the format; the unit tags
        // (OA-AHU-CPK-P10-01 ...), the remarks ("Valet Laundry") and any
        // airflow typed over a SUMIF (Tower-1's first unit reads 4550
        // and 2820) were the API job's. The tags are also what the
        // "served by" drop-down offers, so a new job fills those in
        // first.
        //
        // Between the summary and the lists that feed the drop-downs the
        // office sheets carry scratch - more tags, "LEFT" / "RIGHT",
        // "4350l/s" - which goes too. The lists themselves, from the
        // first row that looks something up in an ASHRAE sheet down, are
        // left exactly as they are.
        private static void Leftovers(ExcelWorksheet ws, int from, int lastCol, Result result)
        {
            int end = ws.Dimension.End.Row;

            int heading = 0;

            for (int r = from; r <= end && heading == 0; r++)
            {
                for (int c = 1; c <= lastCol; c++)
                {
                    string text = Norm(ws.Cells[r, c].Text);

                    if (text.Contains("equipment") && text.Contains("summary"))
                    {
                        heading = r;
                        break;
                    }
                }
            }

            if (heading == 0)
                return;

            int header = 0;
            int tagCol = 0;
            int remarksCol = 0;

            for (int r = heading + 1; r <= Math.Min(end, heading + 3) && header == 0; r++)
            {
                for (int c = 1; c <= lastCol; c++)
                {
                    string text = Norm(ws.Cells[r, c].Text);

                    if (tagCol == 0 && text.Contains("tag"))
                    {
                        header = r;
                        tagCol = c;
                    }

                    if (text.Contains("remark"))
                        remarksCol = c;
                }
            }

            if (header == 0)
                return;

            int stop = 0;

            for (int r = header + 1; r <= end; r++)
            {
                if (Norm(ws.Cells[r, tagCol].Text) == "total")
                {
                    stop = r;
                    break;
                }
            }

            if (stop == 0)
                return;

            int units = 0;

            for (int r = header + 1; r < stop; r++)
            {
                for (int c = 1; c <= lastCol; c++)
                {
                    ExcelRange cell = ws.Cells[r, c];

                    bool mine = c == tagCol ||
                                (remarksCol > 0 && c >= remarksCol) ||
                                IsNumber(cell.Value);

                    if (mine && Typed(cell))
                    {
                        if (c == tagCol)
                            units++;

                        ws.Cells[r, c].Value = null;
                    }
                }
            }

            // The lists start at the first formula reaching another sheet
            int lists = 0;

            for (int r = stop + 1; r <= end && lists == 0; r++)
            {
                for (int c = 1; c <= lastCol; c++)
                {
                    string formula = ws.Cells[r, c].Formula;

                    if (!string.IsNullOrEmpty(formula) && formula.Contains("!"))
                    {
                        lists = r;
                        break;
                    }
                }
            }

            if (lists > 0)
            {
                for (int r = stop + 1; r < lists; r++)
                {
                    for (int c = 1; c <= lastCol; c++)
                    {
                        if (Typed(ws.Cells[r, c]))
                            ws.Cells[r, c].Value = null;
                    }
                }
            }

            result.Notes.Add(
                "The equipment summary under the Total was kept with its " +
                "formulas; the API job's " + units + " unit tag(s) in it " +
                "were cleared.");
        }

        private static bool IsNumber(object value)
        {
            return value is double || value is int || value is long ||
                   value is decimal || value is float;
        }

        private static bool Typed(ExcelRange cell)
        {
            return string.IsNullOrEmpty(cell.Formula) && cell.Value != null;
        }

        // ==================================================
        // Drop-downs
        // ==================================================

        private class ListRule
        {
            public string Formula;
            public List<string> Values = new List<string>();
            public bool AllowBlank = true;
            public bool ShowError = true;
            public bool ShowInput;
            public string Error, ErrorTitle, Prompt, PromptTitle;
            public ExcelDataValidationWarningStyle Style =
                ExcelDataValidationWarningStyle.stop;
        }

        // WHICH DROP-DOWN EACH COLUMN HAD - the one covering the most of
        // its rooms, since the office's are in pieces, one per level.
        //
        // AND WHERE THAT DROP-DOWN HAD GONE STALE, WHAT IT SHOULD HAVE
        // BEEN. MLCP's occupancy category drop-down reads $H$321:$H$417,
        // which after years of inserted and deleted rows is the tail of
        // its category list and ninety blank cells. But a column that
        // feeds a VLOOKUP into another sheet can only usefully hold what
        // that table's first column holds - so a drop-down reading this
        // sheet is replaced by that column. One already reading another
        // sheet, or a plain Yes/No, is the office's and is kept.
        private static Dictionary<int, ListRule> Lists(
            ExcelWorksheet ws,
            List<KeyValuePair<int, VENTROWS.RowKind>> kinds,
            VENTROWS.RowTemplate room,
            int lastCol)
        {
            var roomRows = new HashSet<int>(
                kinds.Where(k => k.Value == VENTROWS.RowKind.Room).Select(k => k.Key));

            var best = new Dictionary<int, KeyValuePair<IExcelDataValidationList, int>>();

            foreach (IExcelDataValidation dv in ws.DataValidations)
            {
                var list = dv as IExcelDataValidationList;

                if (list == null || dv.Address == null)
                    continue;

                var covered = new Dictionary<int, int>();

                foreach (ExcelAddress part in Parts(dv.Address.Address))
                {
                    for (int c = part.Start.Column; c <= Math.Min(part.End.Column, lastCol); c++)
                    {
                        int n = roomRows.Count(r => r >= part.Start.Row && r <= part.End.Row);

                        if (n > 0)
                            covered[c] = (covered.TryGetValue(c, out int m) ? m : 0) + n;
                    }
                }

                foreach (var kv in covered)
                {
                    KeyValuePair<IExcelDataValidationList, int> held;

                    if (!best.TryGetValue(kv.Key, out held) || kv.Value > held.Value)
                        best[kv.Key] = new KeyValuePair<IExcelDataValidationList, int>(list, kv.Value);
                }
            }

            var rules = new Dictionary<int, ListRule>();

            foreach (var kv in best)
            {
                rules[kv.Key] = RuleOf(kv.Value.Key);
            }

            // The VLOOKUP keys
            for (int c = 1; c <= lastCol; c++)
            {
                string formula = room.FormulasR1C1[c];

                if (string.IsNullOrEmpty(formula))
                    continue;

                foreach (Match m in Refs.Lookup.Matches(formula))
                {
                    int key = Refs.Column(m.Groups["key"].Value, c);

                    if (key < 1 || key > lastCol)
                        continue;

                    string table = m.Groups["sheet"].Value +
                        "$" + ExcelCellAddress.GetColumnLetter(int.Parse(m.Groups["c1"].Value)) +
                        "$" + m.Groups["r1"].Value +
                        ":$" + ExcelCellAddress.GetColumnLetter(int.Parse(m.Groups["c1"].Value)) +
                        "$" + m.Groups["r2"].Value;

                    ListRule rule;

                    if (rules.TryGetValue(key, out rule))
                    {
                        bool stale = rule.Values.Count == 0 &&
                                     !string.IsNullOrEmpty(rule.Formula) &&
                                     !rule.Formula.Contains("!");

                        if (stale)
                            rule.Formula = table;
                    }
                    else
                    {
                        rules[key] = new ListRule { Formula = table };
                    }
                }
            }

            return rules;
        }

        private static IEnumerable<ExcelAddress> Parts(string address)
        {
            foreach (string part in (address ?? string.Empty).Split(
                         new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ExcelAddress a = null;

                try
                {
                    a = new ExcelAddress(part);
                }
                catch
                {
                    // not an address
                }

                if (a != null)
                    yield return a;
            }
        }

        // A drop-down that reaches into the rewritten rows is taken off
        // them; whatever part of it lies outside is put back as it was
        private static void ClearValidations(ExcelWorksheet ws, int first, int last)
        {
            foreach (IExcelDataValidation dv in ws.DataValidations.ToList())
            {
                if (dv.Address == null)
                    continue;

                var keep = new List<string>();
                bool touches = false;

                foreach (ExcelAddress a in Parts(dv.Address.Address))
                {
                    if (a.End.Row < first || a.Start.Row > last)
                    {
                        keep.Add(a.Address);
                        continue;
                    }

                    touches = true;

                    if (a.Start.Row < first)
                        keep.Add(new ExcelAddress(a.Start.Row, a.Start.Column, first - 1, a.End.Column).Address);

                    if (a.End.Row > last)
                        keep.Add(new ExcelAddress(last + 1, a.Start.Column, a.End.Row, a.End.Column).Address);
                }

                if (!touches)
                    continue;

                var list = dv as IExcelDataValidationList;

                ListRule rule = null;

                if (list != null && keep.Count > 0)
                    rule = RuleOf(list);

                ws.DataValidations.Remove(dv);

                if (rule != null)
                    Add(ws, string.Join(" ", keep), rule);
            }
        }

        // MLCP CARRIES A DROP-DOWN OVER EVERY CELL OF 150 WHOLE ROWS
        // (A232:XFD383) under its summary - the occupancy list, applied to
        // the summary, the lists and everything to the right of them. It
        // is somebody's slip, it would stop anyone typing a unit tag into
        // the summary, and once the rows move EPPlus writes it back as
        // "46:197", which openpyxl refuses outright and Excel's schema
        // does not describe. Nobody means "every column" by a drop-down,
        // so a whole-row one is dropped.
        private static void DropWholeRows(ExcelWorksheet ws)
        {
            foreach (IExcelDataValidation dv in ws.DataValidations.ToList())
            {
                if (dv.Address == null)
                    continue;

                List<ExcelAddress> parts = Parts(dv.Address.Address).ToList();

                List<ExcelAddress> keep = parts
                    .Where(a => !(a.Start.Column == 1 && a.End.Column >= ExcelPackage.MaxColumns))
                    .ToList();

                if (keep.Count == parts.Count)
                    continue;

                var list = dv as IExcelDataValidationList;

                ListRule rule = list == null || keep.Count == 0 ? null : RuleOf(list);

                ws.DataValidations.Remove(dv);

                if (rule != null)
                    Add(ws, string.Join(" ", keep.Select(a => a.Address)), rule);
            }
        }

        private static ListRule RuleOf(IExcelDataValidationList dv)
        {
            var rule = new ListRule
            {
                Formula = dv.Formula.ExcelFormula,
                AllowBlank = dv.AllowBlank ?? true,
                ShowError = dv.ShowErrorMessage ?? true,
                ShowInput = dv.ShowInputMessage ?? false,
                Error = dv.Error,
                ErrorTitle = dv.ErrorTitle,
                Prompt = dv.Prompt,
                PromptTitle = dv.PromptTitle,
                Style = dv.ErrorStyle
            };

            if (dv.Formula.Values != null)
                rule.Values.AddRange(dv.Formula.Values);

            return rule;
        }

        private static void AddValidations(
            ExcelWorksheet ws,
            List<VENTROWS.PlannedRow> plan,
            int first,
            Dictionary<int, ListRule> lists,
            int blockEnd,
            int delta)
        {
            // The room rows as runs, one per level
            var runs = new List<int[]>();

            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].Kind != VENTROWS.RowKind.Room)
                    continue;

                int row = first + i;

                if (runs.Count > 0 && runs[runs.Count - 1][1] == row - 1)
                    runs[runs.Count - 1][1] = row;
                else
                    runs.Add(new[] { row, row });
            }

            foreach (var kv in lists)
            {
                string col = ExcelCellAddress.GetColumnLetter(kv.Key);

                // Space-separated: EPPlus refuses commas in a
                // validation's address, although Excel writes either
                string address = string.Join(" ", runs.Select(
                    run => col + run[0] + ":" + col + run[1]));

                ListRule rule = kv.Value;

                // Read before the resize, so a list further down this
                // sheet has moved since
                if (!string.IsNullOrEmpty(rule.Formula) && !rule.Formula.Contains("!"))
                    rule.Formula = Refs.ShiftA1(rule.Formula, ws.Name, blockEnd, delta);

                Add(ws, address, rule);
            }
        }

        private static void Add(ExcelWorksheet ws, string address, ListRule rule)
        {
            IExcelDataValidationList dv = ws.DataValidations.AddListValidation(address);

            if (rule.Values.Count > 0)
            {
                foreach (string value in rule.Values)
                    dv.Formula.Values.Add(value);
            }
            else
            {
                dv.Formula.ExcelFormula = rule.Formula;
            }

            dv.AllowBlank = rule.AllowBlank;
            dv.ShowErrorMessage = rule.ShowError;
            dv.ShowInputMessage = rule.ShowInput;
            dv.ErrorStyle = rule.Style;

            if (!string.IsNullOrEmpty(rule.Error)) dv.Error = rule.Error;
            if (!string.IsNullOrEmpty(rule.ErrorTitle)) dv.ErrorTitle = rule.ErrorTitle;
            if (!string.IsNullOrEmpty(rule.Prompt)) dv.Prompt = rule.Prompt;
            if (!string.IsNullOrEmpty(rule.PromptTitle)) dv.PromptTitle = rule.PromptTitle;
        }

        // ==================================================
        // Text
        // ==================================================

        private static string Norm(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            bool space = false;

            foreach (char ch in text)
            {
                if (char.IsWhiteSpace(ch))
                {
                    space = sb.Length > 0;
                    continue;
                }

                if (space)
                {
                    sb.Append(' ');
                    space = false;
                }

                sb.Append(char.ToLowerInvariant(ch));
            }

            return sb.ToString();
        }

        public static string Describe(Result r)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Worksheet \"" + r.Sheet + "\", in the office layout (copied from " + r.Pattern + "):");
            sb.AppendLine("    " + r.Levels + " level(s), " + r.Rooms + " room(s)");
            sb.AppendLine("    name, number, area and height from the model; the room number is column " +
                          ExcelCellAddress.GetColumnLetter(r.RoomNumberCol));
            sb.AppendLine("    every formula kept; every typed value left blank");

            if (r.Brought.Count > 0)
                sb.AppendLine("    brought in for the formulas: " + string.Join(", ", r.Brought));

            foreach (string note in r.Notes)
            {
                sb.AppendLine();
                sb.AppendLine(note);
            }

            return sb.ToString().TrimEnd();
        }

        // ==================================================
        // References inside formulas
        // ==================================================

        // Excel's formula grammar is not something to parse with a
        // regex in general, and none of this tries to. It only has to
        // find CELL REFERENCES, and for that the three things that matter
        // are: skip string literals, keep a reference that names another
        // sheet away from this one's arithmetic, and do not mistake part
        // of a function name (LOG10, ATAN2) for a cell. Tested in
        // scratchpad/officetest on exactly those.
        public static class Refs
        {
            private const string Sheet = @"(?:'(?:[^']|'')+'|[A-Za-z_][\w.]*)!";

            private const string A1Cell = @"\$?[A-Za-z]{1,3}\$?\d+";

            private const string R1C1Cell = @"R(?:\[-?\d+\]|\d+)?C(?:\[-?\d+\]|\d+)?";

            private static readonly Regex A1 = new Regex(
                "(?<str>\"(?:[^\"]|\"\")*\")" +
                "|(?<sheet>" + Sheet + ")(?<ref>" + A1Cell + "(?::" + A1Cell + ")?)" +
                @"|(?<![\w.$])(?<ref>" + A1Cell + "(?::" + A1Cell + @")?)(?![\w(!])");

            private static readonly Regex R1C1 = new Regex(
                "(?<str>\"(?:[^\"]|\"\")*\")" +
                "|(?<sheet>" + Sheet + ")(?<ref>" + R1C1Cell + "(?::" + R1C1Cell + ")?)" +
                @"|(?<![\w.$\]])(?<ref>" + R1C1Cell + "(?::" + R1C1Cell + @")?)(?![\w(\[!])");

            private static readonly Regex A1Part = new Regex(
                @"(\$?)([A-Za-z]{1,3})(\$?)(\d+)");

            private static readonly Regex AbsoluteRow = new Regex(
                @"R(\d+)(?=C)");

            // VLOOKUP(<key>, <sheet>!R<r1>C<c1>:R<r2>C<c2>, ...
            public static readonly Regex Lookup = new Regex(
                @"VLOOKUP\(\s*(?<key>" + R1C1Cell + @")\s*,\s*(?<sheet>" + Sheet +
                @")R(?<r1>\d+)C(?<c1>\d+):R(?<r2>\d+)C(?<c2>\d+)",
                RegexOptions.IgnoreCase);

            private static bool Same(Match m, string sheet)
            {
                string s = m.Groups["sheet"].Value;

                if (s.Length == 0)
                    return true;

                s = s.Substring(0, s.Length - 1);

                if (s.StartsWith("'") && s.EndsWith("'"))
                    s = s.Substring(1, s.Length - 2).Replace("''", "'");

                return string.Equals(s, sheet, StringComparison.OrdinalIgnoreCase);
            }

            public static bool HasReference(string r1c1)
            {
                if (string.IsNullOrEmpty(r1c1))
                    return false;

                foreach (Match m in R1C1.Matches(r1c1))
                {
                    if (m.Groups["ref"].Success)
                        return true;
                }

                return false;
            }

            // Absolute rows below afterRow move by delta. A reference to
            // another sheet never does - its rows are not this sheet's.
            public static string ShiftR1C1(string r1c1, int afterRow, int delta)
            {
                if (delta == 0 || string.IsNullOrEmpty(r1c1))
                    return r1c1;

                return R1C1.Replace(r1c1, m =>
                {
                    if (!m.Groups["ref"].Success || m.Groups["sheet"].Value.Length > 0)
                        return m.Value;

                    return AbsoluteRow.Replace(m.Value, p =>
                    {
                        int row = int.Parse(p.Groups[1].Value);

                        return row > afterRow ? "R" + (row + delta) : p.Value;
                    });
                });
            }

            // Every row below afterRow moves by delta, $ or not - for a
            // drop-down's list, which names cells, not offsets
            public static string ShiftA1(string a1, string sheet, int afterRow, int delta)
            {
                if (delta == 0 || string.IsNullOrEmpty(a1))
                    return a1;

                return A1.Replace(a1, m =>
                {
                    if (!m.Groups["ref"].Success || !Same(m, sheet))
                        return m.Value;

                    string moved = A1Part.Replace(m.Groups["ref"].Value, p =>
                    {
                        int row = int.Parse(p.Groups[4].Value);

                        if (row <= afterRow)
                            return p.Value;

                        return p.Groups[1].Value + p.Groups[2].Value +
                               p.Groups[3].Value + (row + delta);
                    });

                    return m.Groups["sheet"].Value + moved;
                });
            }

            // An AREA on this sheet whose rows the rule wants changed
            public static string MapAreas(
                string a1, string sheet, Func<int, int, int[]> rule)
            {
                if (string.IsNullOrEmpty(a1))
                    return a1;

                return A1.Replace(a1, m =>
                {
                    if (!m.Groups["ref"].Success || !Same(m, sheet))
                        return m.Value;

                    string text = m.Groups["ref"].Value;

                    MatchCollection parts = A1Part.Matches(text);

                    if (parts.Count != 2)
                        return m.Value;

                    int a = int.Parse(parts[0].Groups[4].Value);
                    int b = int.Parse(parts[1].Groups[4].Value);

                    int[] to = rule(Math.Min(a, b), Math.Max(a, b));

                    if (to == null)
                        return m.Value;

                    return m.Groups["sheet"].Value +
                           Part(parts[0], to[0]) + ":" + Part(parts[1], to[1]);
                });
            }

            private static string Part(Match p, int row)
            {
                return p.Groups[1].Value + p.Groups[2].Value + p.Groups[3].Value + row;
            }

            // The rows a formula looks at on this sheet
            public static List<int> Rows(string a1, string sheet)
            {
                var rows = new List<int>();

                if (string.IsNullOrEmpty(a1))
                    return rows;

                foreach (Match m in A1.Matches(a1))
                {
                    if (!m.Groups["ref"].Success || !Same(m, sheet))
                        continue;

                    foreach (Match p in A1Part.Matches(m.Groups["ref"].Value))
                        rows.Add(int.Parse(p.Groups[4].Value));
                }

                return rows;
            }

            // A Sub Total's formula written relative to its own block:
            // itself, its first room and its last, and anything else as
            // an offset from the first room. Absolute rows are left as
            // they are - they point at lists, not at the block.
            public static string Relative(
                string a1, string sheet, int self, int firstRoom, int lastRoom)
            {
                if (string.IsNullOrEmpty(a1))
                    return a1;

                return A1.Replace(a1, m =>
                {
                    if (!m.Groups["ref"].Success || !Same(m, sheet))
                        return m.Value;

                    string moved = A1Part.Replace(m.Groups["ref"].Value, p =>
                    {
                        if (p.Groups[3].Value == "$")
                            return p.Value;

                        int row = int.Parse(p.Groups[4].Value);

                        string token =
                            row == self ? "{self}" :
                            row == firstRoom ? "{first}" :
                            row == lastRoom ? "{last}" :
                            "{first" + (row - firstRoom).ToString("+0;-0") + "}";

                        return p.Groups[1].Value + p.Groups[2].Value + token;
                    });

                    return m.Groups["sheet"].Value + moved;
                });
            }

            // The column an R1C1 reference in column `at` points at
            public static int Column(string r1c1, int at)
            {
                Match m = Regex.Match(r1c1, @"C(?:\[(-?\d+)\]|(\d+))?$");

                if (!m.Success)
                    return 0;

                if (m.Groups[2].Success)
                    return int.Parse(m.Groups[2].Value);

                if (m.Groups[1].Success)
                    return at + int.Parse(m.Groups[1].Value);

                return at;
            }
        }
    }
}
