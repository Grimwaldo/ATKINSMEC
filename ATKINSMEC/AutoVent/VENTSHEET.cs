using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    public class VENTSHEET
    {

        public static string sheetpath;

        // Worksheet picked in the form after the file is chosen
        public static string sheetname;

        public static List<string> GetWorksheetNames(string path)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");

            var names = new List<string>();

            using (ExcelPackage package =
                new ExcelPackage(new FileInfo(path)))
            {
                foreach (ExcelWorksheet ws in package.Workbook.Worksheets)
                    names.Add(ws.Name);
            }

            return names;
        }

        // ==================================================
        // Column header keywords.
        //
        // A column matches when its header contains EVERY word
        // of a keyword set; the sets are tried in order, so the
        // first is the strictest and the last the most forgiving.
        // "Preferred" words only break ties between near-identical
        // headers, e.g. "Total Exhaust" (an intermediate column)
        // versus "Total Exhaust (Max. with rounded up value)".
        //
        // Keywords must be lower case. Edit these to suit a new
        // sheet layout - no column numbers are hard coded.
        // ==================================================

        // Deliberately strict: a bare "number" would match headers like
        // "Number of Occupants Based on ASHRAE, N2"
        private static readonly string[][] RoomNumberKeywords =
        {
            new[] { "room", "number" },
            new[] { "room", "no" }
        };

        private static readonly string[][] SpaceNameKeywords =
        {
            new[] { "space", "name" },
            new[] { "room", "name" },
            new[] { "space" },
            new[] { "description" }
        };

        private static readonly string[][] FreshAirKeywords =
        {
            new[] { "fresh", "airflow" },
            new[] { "fresh" },
            new[] { "outside", "air" }
        };

        private static readonly string[] FreshAirPreferred =
        {
            "used", "alternate", "supercede"
        };

        private static readonly string[][] ExhaustAirKeywords =
        {
            new[] { "total", "exhaust" },
            new[] { "exhaust" }
        };

        private static readonly string[] ExhaustAirPreferred =
        {
            "max", "rounded"
        };

        // Used by the Revit -> Excel export. Deliberately strict: a bare
        // "area" would match "Area Outdoor Air Rate" and "Room/Area
        // served by unit".
        private static readonly string[][] AreaKeywords =
        {
            new[] { "total", "area" }
        };

        private static readonly string[][] HeightKeywords =
        {
            new[] { "room", "height" },
            new[] { "height" }
        };

        // Table 6-4's "No. of Units" - the water closets and urinals a
        // per-fixture exhaust rate is multiplied by. "No. of Keys",
        // "No. of Bedrooms" and "No. of Occupants" share the "No." and
        // none of them says "units".
        private static readonly string[][] UnitsKeywords =
        {
            new[] { "no", "units" },
            new[] { "number", "units" }
        };

        // "No of Bedrooms in a dwelling unit Nb" - one figure for a whole
        // apartment, so merged down over its rooms like the apartment name
        private static readonly string[][] BedroomsKeywords =
        {
            new[] { "bedrooms" }
        };

        private static readonly string[] NoPreference = { };

        // Level rows are highlighted blue in the sheet. List the exact
        // shades used here; any clearly blue fill also counts, so a
        // slightly different blue in another workbook still works.
        private static readonly string[] LevelFillColors = { "FF00B0F0" };

        public class SpaceKey
        {
            public string SpaceName { get; set; }
            public string RoomNumber { get; set; }
            public string Level { get; set; }

            private static string Norm(string s)
            {
                return (s ?? string.Empty).Trim();
            }

            public override bool Equals(object obj)
            {
                var other = obj as SpaceKey;

                if (other == null)
                    return false;

                return string.Equals(Norm(SpaceName), Norm(other.SpaceName), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Norm(RoomNumber), Norm(other.RoomNumber), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Norm(Level), Norm(other.Level), StringComparison.OrdinalIgnoreCase);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(Norm(SpaceName));
                    hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(Norm(RoomNumber));
                    hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(Norm(Level));
                    return hash;
                }
            }
        }

        public class AirData
        {
            public double FreshAir { get; set; }
            public double ExhaustAir { get; set; }
        }

        public class SheetLayout
        {
            public int HeaderRow { get; set; }
            public int HeaderEndRow { get; set; }
            public int FirstDataRow { get; set; }
            public int SpaceNameCol { get; set; }
            public int RoomNumberCol { get; set; }
            public int FreshAirCol { get; set; }
            public int ExhaustAirCol { get; set; }
            public int AreaCol { get; set; }
            public int HeightCol { get; set; }
            public int UnitsCol { get; set; }
            public int BedroomsCol { get; set; }

            // THE LAST COLUMN OF THE SPACE BLOCK. The office's "Space"
            // heading spans F:H; an ordinary room merges its name across
            // the three, while a room inside an apartment has the
            // APARTMENT in F (merged down over its rooms) and its own name
            // in H. Equal to SpaceNameCol where the block is one column.
            public int SpaceEndCol { get; set; }

            public bool LevelsByColor { get; set; }

            public string Describe()
            {
                return
                    "Space name: " + ColName(SpaceNameCol) +
                    ", Room number: " + ColName(RoomNumberCol) +
                    ", Fresh air: " + ColName(FreshAirCol) +
                    ", Exhaust: " + ColName(ExhaustAirCol) +
                    ", data from row " + FirstDataRow +
                    ", levels by " +
                    (LevelsByColor ? "blue fill" : "blank room number");
            }

            private static string ColName(int col)
            {
                return col <= 0
                    ? "not found"
                    : ExcelCellAddress.GetColumnLetter(col);
            }
        }

        // Last sheet read, so the form can show what it contains
        public static Dictionary<SpaceKey, AirData> LoadedData;

        // Which columns the keyword search picked, for the form and log
        public static string LayoutDescription;

        public static HashSet<string> GetSheetLevels()
        {
            var levels =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (LoadedData != null)
            {
                foreach (SpaceKey key in LoadedData.Keys)
                    levels.Add((key.Level ?? string.Empty).Trim());
            }

            return levels;
        }

        public Dictionary<SpaceKey, AirData> excelsheet()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");
            FileInfo fileInfo = new FileInfo(sheetpath);
            using (ExcelPackage package = new ExcelPackage(fileInfo))
            {
                if (string.IsNullOrEmpty(sheetname))
                    throw new Exception("No worksheet selected.");

                ExcelWorksheet xlWorksheet =
                    package.Workbook.Worksheets[sheetname];

                if (xlWorksheet == null)
                    throw new Exception(
                        "The workbook has no worksheet named \"" +
                        sheetname + "\".");

                if (xlWorksheet.Dimension == null)
                    throw new Exception(
                        "Worksheet \"" + sheetname + "\" is empty.");

                SheetLayout layout = DetectLayout(xlWorksheet);

                LayoutDescription = layout.Describe();

                int rowCount = xlWorksheet.Dimension.Rows;

                var dict = new Dictionary<SpaceKey, AirData>();
                string currentLevel = null;

                bool levelsByColor = layout.LevelsByColor;

                for (int i = layout.FirstDataRow; i <= rowCount; i++)
                {
                    ExcelRange nameCell =
                        xlWorksheet.Cells[i, layout.SpaceNameCol];

                    // A room's OWN name - inside an apartment that is the
                    // last column of the Space block, not the apartment's
                    // merged name (see NameOf)
                    string spaceName = NameOf(xlWorksheet, i, layout);

                    if (string.IsNullOrEmpty(spaceName))
                        continue;

                    // "Sub Total" / "Total" carry the level colour too
                    if (IsTotalRow(spaceName))
                        continue;

                    string roomNumber =
                        xlWorksheet.Cells[i, layout.RoomNumberCol].Text.Trim();

                    bool isLevelRow = levelsByColor
                        ? IsLevelFill(nameCell)
                        : string.IsNullOrEmpty(roomNumber);

                    if (isLevelRow)
                    {
                        currentLevel = spaceName;
                        continue;
                    }

                    if (string.IsNullOrEmpty(roomNumber))
                        continue;

                    if (currentLevel == null)
                        continue;

                    var key = new SpaceKey
                    {
                        SpaceName = spaceName,
                        RoomNumber = roomNumber,
                        Level = currentLevel
                    };

                    var value = new AirData
                    {
                        FreshAir = ReadDouble(xlWorksheet, i, layout.FreshAirCol),
                        ExhaustAir = ReadDouble(xlWorksheet, i, layout.ExhaustAirCol)
                    };

                    dict[key] = value;
                }

                LoadedData = dict;

                return dict;
            }
        }

        // ==================================================
        // A row's own name
        // ==================================================

        // THE NAME A ROW IS KEYED BY. Usually the Space column's text. But
        // a room INSIDE AN APARTMENT has the apartment in the Space column
        // - merged down over all its rooms, so only the first row even
        // holds the text - and its own name in the last column of the
        // Space block. That name is what the room in the model is called,
        // so it is what the row is keyed by: the block's last column is
        // read first, unless it is part of a merge reaching back to the
        // Space column (an ordinary room's name, merged across the block).
        //
        // Before this, the second and later rooms of an apartment were
        // skipped outright (an empty Space cell) and the first was keyed
        // by the APARTMENT's name, so no apartment room could ever match.
        public static string NameOf(ExcelWorksheet ws, int row, SheetLayout layout)
        {
            if (layout.SpaceEndCol > layout.SpaceNameCol)
            {
                string own = ws.Cells[row, layout.SpaceEndCol].Text.Trim();

                if (own.Length > 0)
                {
                    string merged = ws.MergedCells[row, layout.SpaceEndCol];

                    bool acrossTheBlock = merged != null &&
                                          new ExcelAddress(merged).Start.Column <= layout.SpaceNameCol;

                    if (!acrossTheBlock)
                        return own;
                }
            }

            return ws.Cells[row, layout.SpaceNameCol].Text.Trim();
        }

        // The Space heading's merge, read on the header row - the office's
        // F6:H9. A heading that is not merged is a one-column block.
        private static int SpaceEnd(ExcelWorksheet ws, SheetLayout layout)
        {
            try
            {
                string merged = ws.MergedCells[layout.HeaderRow, layout.SpaceNameCol];

                if (merged == null)
                    return layout.SpaceNameCol;

                var block = new ExcelAddress(merged);

                return block.Start.Column == layout.SpaceNameCol && block.End.Column > block.Start.Column
                    ? block.End.Column
                    : layout.SpaceNameCol;
            }
            catch
            {
                return layout.SpaceNameCol;
            }
        }

        // ==================================================
        // Locate the columns by their header text
        // ==================================================

        public static SheetLayout DetectLayout(ExcelWorksheet ws)
        {
            return DetectLayout(ws, true);
        }

        public static SheetLayout DetectLayout(
            ExcelWorksheet ws,
            bool requireRoomNumber)
        {
            int lastRow = ws.Dimension.Rows;
            int lastCol = ws.Dimension.Columns;

            var layout = new SheetLayout();

            // 1. The header row is the first row carrying any header we
            //    know how to recognise. Room number alone is not enough
            //    of an anchor: some sheets do not have that column.
            var anchors = new string[][][]
            {
                RoomNumberKeywords,
                SpaceNameKeywords,
                HeightKeywords,
                AreaKeywords
            };

            int scanDepth = Math.Min(25, lastRow);

            for (int r = 1; r <= scanDepth && layout.HeaderRow == 0; r++)
            {
                for (int c = 1; c <= lastCol; c++)
                {
                    string text = Normalize(ws.Cells[r, c].Text);

                    if (text.Length == 0)
                        continue;

                    foreach (string[][] sets in anchors)
                    {
                        if (MatchesAnySet(text, sets))
                        {
                            layout.HeaderRow = r;
                            break;
                        }
                    }

                    if (layout.HeaderRow > 0)
                        break;
                }
            }

            if (layout.HeaderRow == 0)
            {
                throw new Exception(
                    "Could not find the header row of the sheet - no " +
                    "recognisable column header was found.");
            }

            // 2. Headers may be merged down several rows; the data
            //    starts under the deepest of them.
            int headerEnd = layout.HeaderRow;

            for (int c = 1; c <= lastCol; c++)
            {
                string merged = ws.MergedCells[layout.HeaderRow, c];

                if (string.IsNullOrEmpty(merged))
                    continue;

                var address = new ExcelAddress(merged);

                if (address.End.Row > headerEnd)
                    headerEnd = address.End.Row;
            }

            layout.HeaderEndRow = headerEnd;
            layout.FirstDataRow = headerEnd + 1;

            // 3. Header text per column, read from everything above the
            //    data so that headers sitting on a different row than the
            //    room number are still seen, while data rows are not.
            var headers = new string[lastCol + 1];

            for (int c = 1; c <= lastCol; c++)
            {
                var sb = new StringBuilder();

                for (int r = 1; r <= headerEnd; r++)
                {
                    string text = Normalize(ws.Cells[r, c].Text);

                    if (text.Length == 0)
                        continue;

                    if (sb.Length > 0)
                        sb.Append(' ');

                    sb.Append(text);
                }

                headers[c] = sb.ToString();
            }

            layout.FreshAirCol =
                FindColumn(headers, FreshAirKeywords, FreshAirPreferred);

            layout.ExhaustAirCol =
                FindColumn(headers, ExhaustAirKeywords, ExhaustAirPreferred);

            layout.RoomNumberCol =
                FindColumn(headers, RoomNumberKeywords, NoPreference);

            if (requireRoomNumber && layout.RoomNumberCol == 0)
            {
                throw new Exception(
                    "Could not find a \"Room Number\" column in the sheet. " +
                    "Check the header wording, or add it to " +
                    "RoomNumberKeywords in VENTSHEET.cs.");
            }

            layout.AreaCol =
                FindColumn(headers, AreaKeywords, NoPreference);

            layout.HeightCol =
                FindColumn(headers, HeightKeywords, NoPreference);

            layout.UnitsCol =
                FindColumn(headers, UnitsKeywords, NoPreference);

            layout.BedroomsCol =
                FindColumn(headers, BedroomsKeywords, NoPreference);

            layout.SpaceNameCol =
                FindColumn(headers, SpaceNameKeywords, NoPreference);

            // A matching header is not enough: the column must actually
            // carry names in the data rows
            if (layout.SpaceNameCol > 0 &&
                !HasTextInDataRows(
                    ws,
                    layout.SpaceNameCol,
                    layout.FirstDataRow,
                    lastRow))
            {
                layout.SpaceNameCol = 0;
            }

            // 4. The space name column often has a blank header. It is
            //    the column carrying the level headers and "Sub Total"
            //    rows, which is exactly what the parser keys off.
            if (layout.SpaceNameCol == 0)
            {
                layout.SpaceNameCol = FindGroupingColumn(
                    ws,
                    layout.RoomNumberCol,
                    layout.FirstDataRow,
                    lastRow,
                    lastCol);
            }

            if (layout.SpaceNameCol == 0)
            {
                throw new Exception(
                    "Could not find the space/room name column in the sheet.");
            }

            layout.SpaceEndCol = SpaceEnd(ws, layout);

            // 5. Levels are the blue-highlighted rows. Sheets without that
            //    highlighting fall back to the older rule: a named row
            //    carrying no room number is a level header.
            layout.LevelsByColor =
                HasLevelFillRows(ws, layout, lastRow);

            return layout;
        }

        private static int FindColumn(
            string[] headers,
            string[][] keywordSets,
            string[] preferred)
        {
            foreach (string[] required in keywordSets)
            {
                int bestCol = 0;
                int bestScore = -1;

                for (int c = 1; c < headers.Length; c++)
                {
                    string header = headers[c];

                    if (string.IsNullOrEmpty(header))
                        continue;

                    string[] tokens = Tokenize(header);

                    if (!ContainsAll(tokens, required))
                        continue;

                    int score = 0;

                    foreach (string word in preferred)
                    {
                        if (ContainsWord(tokens, word))
                            score++;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCol = c;
                    }
                }

                if (bestCol > 0)
                    return bestCol;
            }

            return 0;
        }

        // The space name column is scored on how it behaves in the data:
        // it holds text (not a running index) on every row that has a
        // room number, and it is the column carrying the "Sub Total"
        // rows that close each level block.
        private static int FindGroupingColumn(
            ExcelWorksheet ws,
            int roomNumberCol,
            int firstDataRow,
            int lastRow,
            int lastCol)
        {
            int endRow = Math.Min(lastRow, firstDataRow + 500);

            // Without a room number column every column is a candidate
            bool haveRoomNumbers = roomNumberCol > 0;
            int boundary = haveRoomNumbers ? roomNumberCol : lastCol + 1;

            int bestCol = 0;
            int bestScore = int.MinValue;

            for (int c = 1; c < boundary; c++)
            {
                int textOnRoomRows = 0;
                int numberOnRoomRows = 0;
                bool hasSubTotal = false;

                for (int r = firstDataRow; r <= endRow; r++)
                {
                    string cell = ws.Cells[r, c].Text.Trim();

                    if (IsSubTotalRow(cell))
                        hasSubTotal = true;

                    if (cell.Length == 0)
                        continue;

                    if (haveRoomNumbers &&
                        ws.Cells[r, roomNumberCol].Text.Trim().Length == 0)
                        continue;

                    double number;

                    if (double.TryParse(cell, out number))
                        numberOnRoomRows++;
                    else
                        textOnRoomRows++;
                }

                // A numbering column (S.No) never names a space
                if (textOnRoomRows == 0)
                    continue;

                int score = textOnRoomRows - numberOnRoomRows;

                if (hasSubTotal)
                    score += 1000;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCol = c;
                }
            }

            return bestCol;
        }

        private static bool HasTextInDataRows(
            ExcelWorksheet ws,
            int col,
            int firstDataRow,
            int lastRow)
        {
            int endRow = Math.Min(lastRow, firstDataRow + 500);

            for (int r = firstDataRow; r <= endRow; r++)
            {
                string text = ws.Cells[r, col].Text.Trim();

                if (text.Length == 0)
                    continue;

                double number;

                if (double.TryParse(text, out number))
                    continue;

                return true;
            }

            return false;
        }

        private static bool MatchesAnySet(
            string text,
            string[][] keywordSets)
        {
            if (text.Length == 0)
                return false;

            foreach (string[] required in keywordSets)
            {
                if (ContainsAll(text, required))
                    return true;
            }

            return false;
        }

        private static bool ContainsAll(string text, string[] words)
        {
            return ContainsAll(Tokenize(text), words);
        }

        private static bool ContainsAll(string[] tokens, string[] words)
        {
            foreach (string word in words)
            {
                if (!ContainsWord(tokens, word))
                    return false;
            }

            return true;
        }

        // Whole-word matching: "Room Number" must not be matched by
        // "No of Bedrooms in a dwelling unit"
        private static bool ContainsWord(string[] tokens, string word)
        {
            foreach (string token in tokens)
            {
                if (token == word)
                    return true;

                // Tolerate plurals and the like on longer keywords
                if (word.Length >= 4 &&
                    token.StartsWith(word, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string[] Tokenize(string text)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();

            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Length = 0;
                }
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens.ToArray();
        }

        // Lower case, line breaks and repeated spaces collapsed
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            bool lastWasSpace = false;

            foreach (char ch in text)
            {
                char c = char.IsWhiteSpace(ch) ? ' ' : char.ToLowerInvariant(ch);

                if (c == ' ')
                {
                    if (lastWasSpace || sb.Length == 0)
                        continue;

                    lastWasSpace = true;
                }
                else
                {
                    lastWasSpace = false;
                }

                sb.Append(c);
            }

            return sb.ToString().Trim();
        }

        // Does this sheet mark its level rows with a blue fill at all?
        private static bool HasLevelFillRows(
            ExcelWorksheet ws,
            SheetLayout layout,
            int rowCount)
        {
            for (int i = layout.FirstDataRow; i <= rowCount; i++)
            {
                ExcelRange cell = ws.Cells[i, layout.SpaceNameCol];

                string text = cell.Text.Trim();

                if (text.Length == 0 || IsTotalRow(text))
                    continue;

                if (IsLevelFill(cell))
                    return true;
            }

            return false;
        }

        public static bool IsLevelFill(ExcelRange cell)
        {
            try
            {
                if (cell.Style.Fill.PatternType == ExcelFillStyle.None)
                    return false;

                string argb = cell.Style.Fill.BackgroundColor.LookupColor();

                if (string.IsNullOrEmpty(argb))
                    return false;

                argb = argb.TrimStart('#');

                // Drop the alpha channel
                if (argb.Length == 8)
                    argb = argb.Substring(2);

                if (argb.Length != 6)
                    return false;

                foreach (string known in LevelFillColors)
                {
                    string k = known.TrimStart('#');

                    if (k.Length == 8)
                        k = k.Substring(2);

                    if (string.Equals(k, argb, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                int r = Convert.ToInt32(argb.Substring(0, 2), 16);
                int g = Convert.ToInt32(argb.Substring(2, 2), 16);
                int b = Convert.ToInt32(argb.Substring(4, 2), 16);

                // Clearly blue: blue channel strong and well clear of red
                return b >= 100 && b > r + 40 && b >= g - 20;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsSubTotalRow(string name)
        {
            string n = Normalize(name);

            return n == "sub total" || n == "subtotal";
        }

        public static bool IsTotalRow(string name)
        {
            string n = Normalize(name);

            return IsSubTotalRow(name)
                || n == "total"
                || n == "grand total";
        }

        private static double ReadDouble(
            ExcelWorksheet xlWorksheet,
            int row,
            int col)
        {
            if (col <= 0)
                return 0;

            object raw = xlWorksheet.Cells[row, col].Value;

            if (raw == null)
                return 0;

            double cellValue;

            if (double.TryParse(raw.ToString(), out cellValue) && !double.IsNaN(cellValue))
                return cellValue;

            return 0;
        }
    }
}
