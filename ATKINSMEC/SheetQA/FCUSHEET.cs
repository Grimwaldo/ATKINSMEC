using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ATKINSMEC
{
    // Reading an FCU schedule out of Excel. Deliberately Revit-free, so
    // it can be exercised without opening a model.
    //
    // Nothing about the layout is hard-coded to one office's template.
    // The header block is found by looking for the cell that says FCU
    // number, and the flow column by the one that says flow; a row
    // counts as a unit when it has a name AND a number for the flow,
    // which is what skips the units row and the notes at the bottom
    // without having to know where they are.
    public class FCUSHEET
    {
        public class Unit
        {
            public string Number { get; set; }
            public double FlowLitresPerSecond { get; set; }

            public string Model { get; set; }
            public string UnitType { get; set; }

            public int Row { get; set; }

            // A free discharge unit blows straight into the room. There
            // is no supply duct on it to check, so the flowrate check
            // says so rather than reporting it as wrong.
            public bool FreeDischarge { get; set; }

            public override string ToString()
            {
                return Number + "   -   " +
                       FlowLitresPerSecond.ToString("N0") + " L/s";
            }
        }

        public class Layout
        {
            public int HeaderRow;
            public int NumberColumn;
            public int FlowColumn;
            public int ModelColumn;
            public int TypeColumn;

            public bool Found
            {
                get { return HeaderRow > 0 && FlowColumn > 0; }
            }
        }

        public static List<string> WorksheetNames(string path)
        {
            License();

            var names = new List<string>();

            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                foreach (ExcelWorksheet sheet in package.Workbook.Worksheets)
                    names.Add(sheet.Name);
            }

            return names;
        }

        // Throws with a sentence a person can act on, rather than
        // returning an empty list and leaving them guessing
        public static List<Unit> Read(string path, string worksheet)
        {
            License();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new Exception("Choose the schedule spreadsheet first.");

            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                ExcelWorksheet sheet =
                    string.IsNullOrEmpty(worksheet)
                        ? First(package)
                        : package.Workbook.Worksheets[worksheet];

                if (sheet == null)
                {
                    throw new Exception(
                        "The spreadsheet has no worksheet called \"" +
                        worksheet + "\".");
                }

                if (sheet.Dimension == null)
                {
                    throw new Exception(
                        "Worksheet \"" + sheet.Name + "\" is empty.");
                }

                Layout layout = Detect(sheet);

                if (!layout.Found)
                {
                    throw new Exception(
                        "Could not find the columns in worksheet \"" +
                        sheet.Name + "\". It needs a heading with the FCU " +
                        "number in one column and the flow rate in " +
                        "another.");
                }

                List<Unit> units = Rows(sheet, layout);

                if (units.Count == 0)
                {
                    throw new Exception(
                        "Found the headings in worksheet \"" + sheet.Name +
                        "\" but no rows under them with both a unit name " +
                        "and a flow rate.");
                }

                return units;
            }
        }

        private static ExcelWorksheet First(ExcelPackage package)
        {
            foreach (ExcelWorksheet sheet in package.Workbook.Worksheets)
                return sheet;

            return null;
        }

        // ==================================================
        // Where the columns are
        // ==================================================

        public static Layout Detect(ExcelWorksheet sheet)
        {
            var layout = new Layout();

            int rows = Math.Min(sheet.Dimension.End.Row, 20);
            int columns = Math.Min(sheet.Dimension.End.Column, 60);

            for (int r = 1; r <= rows && layout.HeaderRow == 0; r++)
            {
                for (int c = 1; c <= columns; c++)
                {
                    string text = Normalise(Text(sheet, r, c));

                    if (text.Contains("fcu") && text.Contains("number"))
                    {
                        layout.HeaderRow = r;
                        layout.NumberColumn = c;

                        break;
                    }
                }
            }

            // No cell says "FCU number"? Then the first column of the
            // row that names the flow is taken as the unit name.
            if (layout.HeaderRow == 0)
            {
                for (int r = 1; r <= rows && layout.HeaderRow == 0; r++)
                {
                    for (int c = 1; c <= columns; c++)
                    {
                        if (!IsFlow(Normalise(Text(sheet, r, c))))
                            continue;

                        layout.HeaderRow = r;
                        layout.NumberColumn = 1;
                        layout.FlowColumn = c;

                        break;
                    }
                }
            }

            if (layout.HeaderRow == 0)
                return layout;

            // A heading can sit on any row of the block - "Flow rate"
            // over "L/s" is two rows, and merged cells report on the
            // top one only
            int last = Math.Min(layout.HeaderRow + 3, sheet.Dimension.End.Row);

            for (int r = layout.HeaderRow; r <= last; r++)
            {
                for (int c = 1; c <= columns; c++)
                {
                    string text = Normalise(Text(sheet, r, c));

                    if (text.Length == 0)
                        continue;

                    if (layout.FlowColumn == 0 && IsFlow(text))
                        layout.FlowColumn = c;

                    if (layout.ModelColumn == 0 && text.Contains("model"))
                        layout.ModelColumn = c;

                    if (layout.TypeColumn == 0
                        && text.Contains("type")
                        && !text.Contains("system"))
                        layout.TypeColumn = c;
                }
            }

            return layout;
        }

        private static bool IsFlow(string text)
        {
            if (text.Length == 0)
                return false;

            // "Flow rate", "Air flow", "Supply flow" - but not the
            // W/l/s power column beside it
            return (text.Contains("flow") || text.Contains("l/s"))
                   && !text.Contains("w/l")
                   && !text.Contains("sfp");
        }

        // ==================================================
        // The rows
        // ==================================================

        private static List<Unit> Rows(ExcelWorksheet sheet, Layout layout)
        {
            var units = new List<Unit>();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int r = layout.HeaderRow + 1;
                 r <= sheet.Dimension.End.Row;
                 r++)
            {
                string number = Text(sheet, r, layout.NumberColumn).Trim();

                if (number.Length == 0)
                    continue;

                double flow;

                if (!Number(sheet, r, layout.FlowColumn, out flow))
                    continue;

                // A repeated unit name would make the match ambiguous;
                // the first one wins and the rest are ignored
                if (!seen.Add(Key(number)))
                    continue;

                var unit = new Unit
                {
                    Number = number,
                    FlowLitresPerSecond = flow,
                    Row = r,
                    Model = layout.ModelColumn == 0
                        ? string.Empty
                        : Text(sheet, r, layout.ModelColumn).Trim(),
                    UnitType = layout.TypeColumn == 0
                        ? string.Empty
                        : Text(sheet, r, layout.TypeColumn).Trim()
                };

                unit.FreeDischarge = FreeDischarge(sheet, r);

                units.Add(unit);
            }

            return units;
        }

        // The words can be in whichever column that office put them in,
        // so the whole row is read rather than one named column
        private static bool FreeDischarge(ExcelWorksheet sheet, int row)
        {
            int columns = Math.Min(sheet.Dimension.End.Column, 60);

            for (int c = 1; c <= columns; c++)
            {
                string text = Normalise(Text(sheet, row, c));

                if (text.Contains("free discharge")
                    || text.Contains("freedischarge"))
                    return true;
            }

            return false;
        }

        // ==================================================
        // Matching a name in the sheet to one in the model
        // ==================================================

        // "FCU -02", "FCU-02", "fcu 02" and "FCU_02" are all the same
        // unit. Punctuation and case are noise; the digits are not, so
        // "FCU-2" and "FCU-02" stay different - an office that means
        // them to be the same can rename one.
        public static string Key(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var kept = new System.Text.StringBuilder();

            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                    kept.Append(char.ToUpperInvariant(c));
            }

            return kept.ToString();
        }

        // ==================================================

        private static string Text(ExcelWorksheet sheet, int row, int column)
        {
            if (row < 1 || column < 1)
                return string.Empty;

            try
            {
                return sheet.Cells[row, column].Text ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Normalise(string text)
        {
            return (text ?? string.Empty)
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Trim()
                .ToLowerInvariant();
        }

        // The VALUE, not the formatted text - see CHWSIZESHEET for the
        // same note. A flow of 160.4 in a column formatted to no
        // decimals reads back as "160" through .Text, and the schedule
        // the check compares against would quietly depend on how
        // somebody chose to display it.
        private static bool Number(
            ExcelWorksheet sheet, int row, int column, out double value)
        {
            value = 0;

            if (row < 1 || column < 1)
                return false;

            object raw;

            try
            {
                raw = sheet.Cells[row, column].Value;
            }
            catch
            {
                return false;
            }

            if (raw == null)
                return false;

            if (raw is double || raw is float || raw is int
                || raw is long || raw is decimal || raw is short)
            {
                try
                {
                    value = Convert.ToDouble(
                        raw, CultureInfo.InvariantCulture);

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return Number(raw.ToString(), out value);
        }

        private static bool Number(string text, out double value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleaned = text.Replace(",", string.Empty).Trim();

            return double.TryParse(
                cleaned,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value)
                || double.TryParse(
                    cleaned,
                    NumberStyles.Any,
                    CultureInfo.CurrentCulture,
                    out value);
        }

        private static void License()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");
        }
    }
}
