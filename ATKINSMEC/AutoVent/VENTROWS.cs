using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ATKINSMEC
{
    // The row machinery both exports share: capturing what a level, room
    // or sub total row looks like, stamping it onto another row, and
    // re-pointing a sub total at its own block.
    //
    // It lived inside REVITEXPORT, which is where "fill the worksheet I
    // imported" still is. It moved out when VENTOFFICE needed the same
    // things, and it moved out REVIT-FREE on purpose: VENTOFFICE is run
    // against the real office workbook in scratchpad/officetest, and it
    // could not be if half of what it calls needed a model open.
    //
    // Nothing in here changed on the way over.
    internal static class VENTROWS
    {
        // One row captured before anything moves
        internal class RowTemplate
        {
            public int[] StyleIds { get; set; }
            public string[] FormulasR1C1 { get; set; }
            public string[] FormulasA1 { get; set; }
            public double Height { get; set; }
            public int MergeFrom { get; set; }
            public int MergeTo { get; set; }
        }

        internal enum RowKind { Level, Spacer, Room, SubTotal }

        internal class PlannedRow
        {
            public RowKind Kind { get; set; }
            public string LevelName { get; set; }
            public VENTTEMPLATE.Room Room { get; set; }
            public int BlockFirstRoomRow { get; set; }
            public int BlockLastRoomRow { get; set; }
        }

        // ==================================================
        // The room number column
        // ==================================================

        // Keeps the layout's column numbers true after a column has gone
        // in at insertedAt
        internal static void ShiftColumns(
            VENTSHEET.SheetLayout layout,
            int insertedAt)
        {
            layout.SpaceNameCol = Shift(layout.SpaceNameCol, insertedAt);
            layout.AreaCol = Shift(layout.AreaCol, insertedAt);
            layout.HeightCol = Shift(layout.HeightCol, insertedAt);
            layout.UnitsCol = Shift(layout.UnitsCol, insertedAt);
            layout.BedroomsCol = Shift(layout.BedroomsCol, insertedAt);
            layout.SpaceEndCol = Shift(layout.SpaceEndCol, insertedAt);
            layout.FreshAirCol = Shift(layout.FreshAirCol, insertedAt);
            layout.ExhaustAirCol = Shift(layout.ExhaustAirCol, insertedAt);

            layout.RoomNumberCol = insertedAt;
        }

        private static int Shift(int col, int insertedAt)
        {
            return col >= insertedAt ? col + 1 : col;
        }

        // Adds a "Room Number" column immediately right of the space
        // name block, styled like its neighbour. EPPlus shifts existing
        // formula references for us.
        internal static int InsertRoomNumberColumn(
            ExcelWorksheet ws,
            VENTSHEET.SheetLayout layout)
        {
            int insertAt = layout.SpaceNameCol + 1;

            string merged =
                ws.MergedCells[layout.HeaderRow, layout.SpaceNameCol];

            if (!string.IsNullOrEmpty(merged))
            {
                var address = new ExcelAddress(merged);

                if (address.End.Column >= insertAt)
                    insertAt = address.End.Column + 1;
            }

            ws.InsertColumn(insertAt, 1);

            int lastRow = ws.Dimension.Rows;

            // Take the look of the column it was pushed in front of
            for (int r = 1; r <= lastRow; r++)
                ws.Cells[r, insertAt].StyleID = ws.Cells[r, insertAt + 1].StyleID;

            try
            {
                ws.Column(insertAt).Width = ws.Column(insertAt + 1).Width;
            }
            catch
            {
                // width is cosmetic only
            }

            ws.Cells[layout.HeaderRow, insertAt].Value = "Room Number";

            if (layout.HeaderEndRow > layout.HeaderRow)
            {
                try
                {
                    ws.Cells[
                        layout.HeaderRow,
                        insertAt,
                        layout.HeaderEndRow,
                        insertAt].Merge = true;
                }
                catch
                {
                    // a neighbouring merge can block this; not fatal
                }
            }

            return insertAt;
        }

        // ==================================================
        // Capturing and stamping rows
        // ==================================================

        internal static RowTemplate Capture(
            ExcelWorksheet ws,
            int row,
            int lastCol,
            int nameCol)
        {
            var template = new RowTemplate
            {
                StyleIds = new int[lastCol + 1],
                FormulasR1C1 = new string[lastCol + 1],
                FormulasA1 = new string[lastCol + 1],
                Height = ws.Row(row).Height,
                MergeFrom = 0,
                MergeTo = 0
            };

            for (int c = 1; c <= lastCol; c++)
            {
                ExcelRange cell = ws.Cells[row, c];

                template.StyleIds[c] = cell.StyleID;

                string formula = cell.Formula;

                if (!string.IsNullOrEmpty(formula))
                {
                    template.FormulasA1[c] = formula;
                    template.FormulasR1C1[c] = cell.FormulaR1C1;
                }
            }

            // The name cell is often merged across a few columns. ACROSS
            // only: an apartment's name is merged DOWN over its rooms, and
            // that is not a shape every room should be given
            string merged = ws.MergedCells[row, nameCol];

            if (!string.IsNullOrEmpty(merged))
            {
                var address = new ExcelAddress(merged);

                if (address.Start.Row == address.End.Row)
                {
                    template.MergeFrom = address.Start.Column;
                    template.MergeTo = address.End.Column;
                }
            }

            return template;
        }

        internal static void ApplyTemplate(
            ExcelWorksheet ws,
            int row,
            RowTemplate template,
            int lastCol)
        {
            if (template == null)
                return;

            try
            {
                ws.Cells[row, 1, row, lastCol].Merge = false;
            }
            catch
            {
                // not merged, nothing to undo
            }

            for (int c = 1; c <= lastCol; c++)
            {
                ExcelRange cell = ws.Cells[row, c];

                cell.Value = null;
                cell.StyleID = template.StyleIds[c];

                // Formulas are carried over; constants are left blank
                if (!string.IsNullOrEmpty(template.FormulasR1C1[c]))
                    cell.FormulaR1C1 = template.FormulasR1C1[c];
            }

            if (template.Height > 0)
                ws.Row(row).Height = template.Height;

            if (template.MergeFrom > 0 &&
                template.MergeTo > template.MergeFrom)
            {
                try
                {
                    ws.Cells[row, template.MergeFrom, row, template.MergeTo]
                        .Merge = true;
                }
                catch
                {
                    // a neighbouring merge can block this; not fatal
                }
            }
        }

        // A Sub Total row sums its own block, so the relative offsets of
        // the template are wrong whenever the block size changes. Rewrite
        // the row numbers instead of copying them.
        internal static void ApplySubTotalFormulas(
            ExcelWorksheet ws,
            int row,
            RowTemplate template,
            int lastCol,
            int templateRow,
            int templateBlockFirst,
            int templateBlockLast,
            int blockFirst,
            int blockLast)
        {
            for (int c = 1; c <= lastCol; c++)
            {
                string formula = template.FormulasA1[c];

                if (string.IsNullOrEmpty(formula))
                    continue;

                ws.Cells[row, c].Formula = RewriteRows(
                    formula,
                    templateRow,
                    templateBlockFirst,
                    templateBlockLast,
                    row,
                    blockFirst,
                    blockLast);
            }
        }

        private static readonly Regex CellRef =
            new Regex(@"(\$?)([A-Za-z]{1,3})(\$?)(\d+)");

        internal static string RewriteRows(
            string formula,
            int templateRow,
            int templateBlockFirst,
            int templateBlockLast,
            int newRow,
            int newBlockFirst,
            int newBlockLast)
        {
            return CellRef.Replace(formula, match =>
            {
                string colAbs = match.Groups[1].Value;
                string col = match.Groups[2].Value;
                string rowAbs = match.Groups[3].Value;
                string rowText = match.Groups[4].Value;

                // Absolute rows point at lookup tables, never at the block
                if (rowAbs == "$")
                    return match.Value;

                int rowNumber;

                if (!int.TryParse(rowText, out rowNumber))
                    return match.Value;

                int mapped;

                if (rowNumber == templateRow)
                    mapped = newRow;
                else if (templateBlockFirst > 0 &&
                         rowNumber == templateBlockFirst)
                    mapped = newBlockFirst;
                else if (templateBlockLast > 0 &&
                         rowNumber == templateBlockLast)
                    mapped = newBlockLast;
                else if (templateBlockFirst > 0 &&
                         rowNumber > templateBlockFirst &&
                         rowNumber < templateBlockLast)
                    mapped = newBlockFirst;
                else
                    return match.Value;

                return colAbs + col + rowAbs + mapped;
            });
        }

        // ==================================================
        // Tidying
        // ==================================================

        internal static void ClearFontColor(
            ExcelWorksheet ws,
            int row,
            int lastCol)
        {
            try
            {
                ws.Cells[row, 1, row, lastCol].Style.Font.Color.SetAuto();
            }
            catch
            {
                // styling only, never worth failing the export over
            }
        }

        internal static bool RemoveAutoFilter(ExcelWorksheet ws)
        {
            try
            {
                if (ws.AutoFilter == null ||
                    ws.AutoFilter.Address == null)
                    return false;

                // Turning the flag off over the filtered range is how
                // EPPlus drops an AutoFilter; the address is read only
                ws.Cells[ws.AutoFilter.Address.Address].AutoFilter = false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        // A worksheet opens wherever it was last saved scrolled to. The
        // office's Tower 2 was saved at row 297, so once its block has been
        // rewritten shorter it opens on blank rows and reads as an empty
        // export. Back to the top - keeping any frozen rows and columns,
        // which have to be frozen again from A1 because Excel counts them
        // from the top-left cell.
        internal static void Home(ExcelWorksheet ws)
        {
            try
            {
                var pane = ws.View.PaneSettings;

                bool frozen = pane != null &&
                              (pane.State == ePaneState.Frozen ||
                               pane.State == ePaneState.FrozenSplit);

                int columns = frozen ? Count(pane.XSplit) : 0;
                int rows = frozen ? Count(pane.YSplit) : 0;

                ws.View.TopLeftCell = "A1";

                if (frozen && (columns > 0 || rows > 0))
                    ws.View.FreezePanes(rows + 1, columns + 1);
            }
            catch
            {
                // where the sheet opens is never worth failing an export
            }
        }

        // A split the sheet does not have reads as NaN - Tower 2 freezes
        // columns only - and NaN cast to int is -2147483648
        private static int Count(double split)
        {
            return double.IsNaN(split) || split < 0 ? 0 : (int)split;
        }

        internal static string ColName(int col)
        {
            return col <= 0
                ? "not found - skipped"
                : ExcelCellAddress.GetColumnLetter(col);
        }

        // ==================================================
        // Apartments
        // ==================================================
        //
        // THE OFFICE'S OWN PATTERN, from Tower-1 of the office workbook
        // (38 of them there): the APARTMENT's name in the Space column,
        // merged DOWN over the apartment's rooms - and the column beside
        // it the same - each room's OWN name in the last column of the
        // Space block, and "No of Bedrooms" merged down over them, being
        // one figure for the dwelling unit. Everything else - area,
        // height, occupancy, every formula - stays per room.
        //
        // THE COLUMN BESIDE THE APARTMENT HOLDS ITS NUMBER - the
        // apartment room's number in the architect's model - at the
        // user's request (2026-10-11); Tower-1 leaves it empty. As text,
        // like every room number. The importer reads only the first and
        // last columns of the block, so it changes nothing on the way back.
        //
        // Run AFTER the rows are written, over (sheet row, room) pairs:
        // consecutive rooms of one apartment become one group. The room
        // template merged each row's name across the Space block; that
        // merge is undone on these rows, because the room's own name now
        // sits in the block's last column.
        //
        // Returns how many apartment groups were written.
        internal static int Apartments(
            ExcelWorksheet ws,
            VENTSHEET.SheetLayout layout,
            IList<KeyValuePair<int, VENTTEMPLATE.Room>> rows)
        {
            int groups = 0;

            for (int i = 0; i < rows.Count;)
            {
                VENTTEMPLATE.Room room = rows[i].Value;

                if (room == null || string.IsNullOrEmpty(room.ApartmentKey))
                {
                    i++;
                    continue;
                }

                int j = i;

                while (j + 1 < rows.Count &&
                       rows[j + 1].Value != null &&
                       rows[j + 1].Value.ApartmentKey == room.ApartmentKey &&
                       rows[j + 1].Key == rows[j].Key + 1)
                {
                    j++;
                }

                Group(ws, layout, rows[i].Key, rows[j].Key, room.Apartment,
                      room.ApartmentNumber,
                      rows.Skip(i).Take(j - i + 1).Select(r => r.Value).ToList());

                groups++;
                i = j + 1;
            }

            return groups;
        }

        private static void Group(
            ExcelWorksheet ws,
            VENTSHEET.SheetLayout layout,
            int top,
            int bottom,
            string apartment,
            string number,
            List<VENTTEMPLATE.Room> rooms)
        {
            int nameCol = layout.SpaceNameCol;
            int endCol = layout.SpaceEndCol;

            // A one-column Space block has nowhere to put the room's own
            // name beside the apartment's, so the two share the cell
            if (endCol <= nameCol)
            {
                for (int k = 0; k < rooms.Count; k++)
                    ws.Cells[top + k, nameCol].Value = apartment + " - " + rooms[k].Name;

                return;
            }

            for (int row = top; row <= bottom; row++)
                Unmerge(ws, row, nameCol, endCol);

            for (int k = 0; k < rooms.Count; k++)
            {
                int row = top + k;

                for (int c = nameCol; c < endCol; c++)
                    ws.Cells[row, c].Value = null;

                ws.Cells[row, endCol].Value = rooms[k].Name ?? string.Empty;
            }

            ws.Cells[top, nameCol].Value = apartment ?? string.Empty;

            // Its number in the column beside it - only where the block
            // has one between the apartment and the rooms' own names
            if (endCol - nameCol >= 2 && !string.IsNullOrWhiteSpace(number))
            {
                ws.Cells[top, nameCol + 1].Style.Numberformat.Format = "@";
                ws.Cells[top, nameCol + 1].Value = number.Trim();
            }

            if (bottom > top)
            {
                // Column by column, as the office merges them (F108:F109,
                // G108:G109) rather than one block across both
                for (int c = nameCol; c < endCol; c++)
                    ws.Cells[top, c, bottom, c].Merge = true;

                if (layout.BedroomsCol > 0)
                {
                    for (int row = top + 1; row <= bottom; row++)
                        ws.Cells[row, layout.BedroomsCol].Value = null;

                    ws.Cells[top, layout.BedroomsCol, bottom, layout.BedroomsCol].Merge = true;
                }
            }

            ws.Cells[top, nameCol, bottom, endCol - 1].Style.VerticalAlignment =
                OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            ws.Cells[top, nameCol, bottom, endCol - 1].Style.HorizontalAlignment =
                OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            ws.Cells[top, nameCol, bottom, endCol - 1].Style.WrapText = true;
        }

        // ==================================================
        // Merged rooms' air
        // ==================================================
        //
        // ROOMS ONE FCU SERVES THROUGH ONE FRESH AIR CONNECTION, merged in
        // Arrange rooms (user, 2026-10-12): their fresh air and extract
        // cells - the two columns AUTOVENT reads back - are merged down over
        // the group and hold the rooms' TOTAL. A live total: each room's
        // own formula, added, so a change of occupancy still flows through;
        // a typed number is added as it is. Only rooms on consecutive rows
        // merge, and VENTARRANGE.Together keeps them so. The importer reads
        // a merged cell back as one group (VENTSHEET.AirData).
        //
        // Run after the rows are written, over (sheet row, room) pairs.
        // Returns how many groups were merged.
        internal static int MergeAir(
            ExcelWorksheet ws,
            VENTSHEET.SheetLayout layout,
            IList<KeyValuePair<int, VENTTEMPLATE.Room>> rows)
        {
            int groups = 0;

            for (int i = 0; i < rows.Count;)
            {
                VENTTEMPLATE.Room room = rows[i].Value;

                if (room == null || string.IsNullOrEmpty(room.Merge))
                {
                    i++;
                    continue;
                }

                int j = i;

                while (j + 1 < rows.Count &&
                       rows[j + 1].Value != null &&
                       rows[j + 1].Value.Merge == room.Merge &&
                       rows[j + 1].Key == rows[j].Key + 1)
                {
                    j++;
                }

                if (j > i)
                {
                    int[] cols = new[] { layout.FreshAirCol, layout.ExhaustAirCol }.Where(c => c > 0).Distinct().ToArray();

                    // Every column's sum worked out before any cell is
                    // emptied - one column's formula may read another's
                    string[] sums = cols.Select(c => Sum(ws, rows[i].Key, rows[j].Key, c)).ToArray();

                    for (int c = 0; c < cols.Length; c++)
                        Total(ws, rows[i].Key, rows[j].Key, cols[c], sums[c]);

                    groups++;
                }

                i = j + 1;
            }

            return groups;
        }

        // One column of a group added up: every row's own formula, or
        // number. A row not set up yet gives "" - and "" + 5 is #VALUE!,
        // which the subtotal and the Total would carry - so each part
        // counts through N(), and the cell is "" while every part is.
        // Null when there is nothing to add.
        private static string Sum(ExcelWorksheet ws, int top, int bottom, int col)
        {
            var parts = new List<string>();

            for (int row = top; row <= bottom; row++)
            {
                ExcelRange cell = ws.Cells[row, col];
                string formula = cell.Formula;
                double number;

                if (!string.IsNullOrEmpty(formula))
                    parts.Add(formula.TrimStart('='));
                else if (cell.Value != null &&
                         double.TryParse(Convert.ToString(cell.Value, System.Globalization.CultureInfo.InvariantCulture),
                                         System.Globalization.NumberStyles.Float,
                                         System.Globalization.CultureInfo.InvariantCulture, out number))
                    parts.Add(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }

            if (parts.Count == 0)
                return null;

            string sum = string.Join("+", parts.Select(p => "N(" + p + ")"));
            string blank = "IF(" + string.Join("&", parts.Select(p => "(" + p + ")")) + "=\"\",\"\"," + sum + ")";

            // Excel takes 8192 characters: past that, a 0 for a blank group
            return blank.Length <= 8000 ? blank : sum;
        }

        // One column of a group: its sum in the first cell; the others
        // emptied; the lot merged
        private static void Total(ExcelWorksheet ws, int top, int bottom, int col, string sum)
        {
            for (int row = top; row <= bottom; row++)
                Unmerge(ws, row, col, col);

            for (int row = top + 1; row <= bottom; row++)
            {
                ws.Cells[row, col].Formula = null;
                ws.Cells[row, col].Value = null;
            }

            if (sum != null)
                ws.Cells[top, col].Formula = sum;

            ws.Cells[top, col, bottom, col].Merge = true;
            ws.Cells[top, col, bottom, col].Style.VerticalAlignment =
                OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
        }

        // Undoes every merge touching this row between two columns
        private static void Unmerge(ExcelWorksheet ws, int row, int fromCol, int toCol)
        {
            foreach (string merged in ws.MergedCells.ToList())
            {
                if (string.IsNullOrEmpty(merged))
                    continue;

                var a = new ExcelAddress(merged);

                if (a.Start.Row <= row && a.End.Row >= row &&
                    a.Start.Column <= toCol && a.End.Column >= fromCol)
                {
                    ws.Cells[merged].Merge = false;
                }
            }
        }
    }
}
