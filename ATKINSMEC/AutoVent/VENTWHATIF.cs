using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OfficeOpenXml;

namespace ATKINSMEC
{
    // THE SHEET'S OWN FORMULAS OVER LEVELS AND ROOMS' OCCUPANCY (user,
    // 2026-10-26: "select the occupancy of the space just like the excel
    // sheet ... put the new values that are about to be changed in another
    // columns called 'new values'"; asked, chose the sheet's own formulas).
    //
    // A COPY of the imported workbook, held in memory - read from the file
    // once and NEVER written back. A room's two categories, nature of use,
    // occupants and units go into its row of the copy, the row is worked
    // out again by EPPlus, and its air and the rest of the calculation are
    // read back. A row with no choice reads as the sheet has it (taken when
    // the copy is opened), so an untouched room's new values ARE the
    // sheet's - whatever EPPlus would make of a formula Excel saved.
    //
    // What an update would calculate with: "Dwelling Unit" and the
    // apartment kitchen mended in the copy (VENTOCCUPANCY.DwellingCheck,
    // KitchenCheck), as an export mends them in the sheet.
    //
    // Revit-free; tests\officetest (WhatIfTests).
    public sealed class VENTWHATIF : IDisposable
    {
        private readonly ExcelPackage _package;
        private readonly ExcelWorksheet _ws;
        private readonly int _lastCol;

        public VENTSHEET.SheetLayout Layout { get; private set; }

        // The calculation's other columns, as the sheet heads them
        public List<VENTFLOWS.CalcColumn> Columns { get; private set; }

        // Each row as the sheet has it - what a row with no choice reads as
        private readonly Dictionary<int, VENTFLOWS.Calc> _asSheet = new Dictionary<int, VENTFLOWS.Calc>();

        // An input cell as the sheet had it (formula, value), kept before
        // its first change - put back when the choice goes
        private readonly Dictionary<long, KeyValuePair<string, object>> _original = new Dictionary<long, KeyValuePair<string, object>>();

        // What is in the copy now, by row
        private readonly Dictionary<int, VENTARRANGE.RoomOccupancy> _applied = new Dictionary<int, VENTARRANGE.RoomOccupancy>();

        public static VENTWHATIF Open(string path, string sheetName, out string trouble)
        {
            trouble = null;

            try
            {
                return new VENTWHATIF(path, sheetName);
            }
            catch (Exception ex)
            {
                trouble = ex.Message;
                return null;
            }
        }

        private VENTWHATIF(string path, string sheetName)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");

            // The bytes, not the file: nothing here can write to it, and a
            // workbook open in Excel is still read
            byte[] bytes;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                bytes = memory.ToArray();
            }

            _package = new ExcelPackage(new MemoryStream(bytes));

            _ws = string.IsNullOrEmpty(sheetName) ? null : _package.Workbook.Worksheets[sheetName];

            if (_ws == null)
                throw new Exception("The workbook has no worksheet named \"" + sheetName + "\".");

            if (_ws.Dimension == null)
                throw new Exception("Worksheet \"" + sheetName + "\" is empty.");

            Layout = VENTSHEET.DetectLayout(_ws);

            _lastCol = _ws.Dimension.End.Column;
            int lastRow = _ws.Dimension.End.Row;

            // AUTOVENT's own export, never saved by Excel: no results to read
            // (as VENTSHEET reads it)
            if (VENTSHEET.NoResults(_ws, Layout, lastRow))
                Calculate(option => _package.Workbook.Calculate(option));

            Columns = ReadColumns();

            for (int r = Layout.FirstDataRow; r <= lastRow; r++)
                _asSheet[r] = Read(r);

            Mend(lastRow);
        }

        public void Dispose()
        {
            _package.Dispose();
        }

        // ==================================================
        // The columns
        // ==================================================

        // EVERY OTHER COLUMN OF THE CALCULATION: after the Space block, up to
        // the last headed one - not those shown already (number, area, the
        // inputs, the two airflows), not a hidden one, not the model ID
        private List<VENTFLOWS.CalcColumn> ReadColumns()
        {
            var columns = new List<VENTFLOWS.CalcColumn>();

            bool made;
            int keyCol = VENTUPDATE.KeyColumn(_ws, Layout, false, out made);

            var shown = new HashSet<int>
            {
                Layout.RoomNumberCol, Layout.AreaCol, Layout.FreshCategoryCol, Layout.ExhaustCategoryCol,
                Layout.UseCol, Layout.OccupantsCol, Layout.UnitsCol, Layout.FreshAirCol, Layout.ExhaustAirCol, keyCol
            };

            for (int c = Math.Max(Layout.SpaceEndCol, Layout.SpaceNameCol) + 1; c <= _lastCol; c++)
            {
                if (shown.Contains(c) || _ws.Column(c).Hidden)
                    continue;

                List<string> parts = HeadingOf(c);

                if (parts.Count == 0)
                    continue;

                string units = parts.LastOrDefault(IsUnit) ?? string.Empty;
                string label = parts.Where(p => !IsUnit(p) && !IsNoise(p))
                                    .OrderByDescending(p => p.Length)
                                    .FirstOrDefault();

                string letter = ExcelCellAddress.GetColumnLetter(c);

                columns.Add(new VENTFLOWS.CalcColumn
                {
                    Col = c,
                    Letter = letter,
                    Label = label ?? letter,
                    Units = units,
                    Heading = string.Join(" / ", parts)
                });
            }

            return columns;
        }

        // A column's heading, cell by cell down the header rows
        private List<string> HeadingOf(int col)
        {
            var parts = new List<string>();

            for (int r = Layout.HeaderRow; r <= Layout.HeaderEndRow; r++)
            {
                string text = Regex.Replace(_ws.Cells[r, col].Text ?? string.Empty, @"\s+", " ").Trim();

                if (text.Length > 0)
                    parts.Add(text);
            }

            return parts;
        }

        // l/s, L/s*person, l/s.m², #, No., %
        private static bool IsUnit(string part)
        {
            string p = part.Trim();

            return p.Length <= 14 &&
                   (p.IndexOf("/s", StringComparison.OrdinalIgnoreCase) >= 0 || p == "#" || p == "%" ||
                    p.Equals("No.", StringComparison.OrdinalIgnoreCase) || p.Contains("m²") || p.Contains("m2"));
        }

        // The office's sign-off cells among the headings: a date, initials,
        // "Calc. by", "Checked by"
        private static bool IsNoise(string part)
        {
            string p = part.Trim();

            return Regex.IsMatch(p, @"^\d{1,2}[-/ ][A-Za-z]{3,9}[-/ ]\d{2,4}$") ||
                   Regex.IsMatch(p, @"^\d{1,2}/\d{1,2}/\d{2,4}$") ||
                   Regex.IsMatch(p, @"^[A-Z]{2,4}(/[A-Z]{2,4})*$") ||
                   Regex.IsMatch(p, @"^(date|date of issue|calc\.? by|checked by|default values)$", RegexOptions.IgnoreCase);
        }

        // ==================================================
        // The lists a cell offers
        // ==================================================

        public List<string> FreshChoices(int row)
        {
            return VENTOCCUPANCY.Choices(_ws, Layout.FreshCategoryCol, row, false);
        }

        public List<string> ExhaustChoices(int row)
        {
            return VENTOCCUPANCY.Choices(_ws, Layout.ExhaustCategoryCol, row, true);
        }

        public List<string> UseChoices(int row)
        {
            return Layout.UseCol <= 0 ? null : VENTOCCUPANCY.ListAt(_ws, Layout.UseCol, row) ?? VENTOCCUPANCY.Uses(_package.Workbook);
        }

        // ==================================================
        // Working it out
        // ==================================================

        // THE CHOICES, BY ROW, AS THEY STAND NOW: what changed since the last
        // call goes into the copy and those rows are worked out again. Every
        // row back: a row with a choice - and the first row of a merge it is
        // in, which holds the merge's air - as worked out; the rest as the
        // sheet has them.
        public Dictionary<int, VENTFLOWS.Calc> Apply(IDictionary<int, VENTARRANGE.RoomOccupancy> wanted)
        {
            wanted = wanted ?? new Dictionary<int, VENTARRANGE.RoomOccupancy>();

            var dirty = new List<int>();

            foreach (int row in _applied.Keys.Union(wanted.Keys).ToList())
            {
                VENTARRANGE.RoomOccupancy now;
                VENTARRANGE.RoomOccupancy next;

                _applied.TryGetValue(row, out now);
                wanted.TryGetValue(row, out next);

                if (next != null && next.IsEmpty)
                    next = null;

                if (Same(now, next))
                    continue;

                Put(row, next);

                if (next == null)
                    _applied.Remove(row);
                else
                    _applied[row] = next.Copy();

                dirty.Add(row);
            }

            var tops = new HashSet<int>();

            foreach (int row in _applied.Keys)
            {
                foreach (int col in new[] { Layout.FreshAirCol, Layout.ExhaustAirCol })
                {
                    int top = TopOf(row, col);

                    if (top > 0 && top != row)
                        tops.Add(top);
                }
            }

            if (dirty.Count > 0)
            {
                Calculate(option =>
                {
                    foreach (int row in dirty)
                        _ws.Cells[row, 1, row, _lastCol].Calculate(option);

                    foreach (int row in dirty)
                    {
                        foreach (int col in new[] { Layout.FreshAirCol, Layout.ExhaustAirCol })
                        {
                            int top = TopOf(row, col);

                            if (top > 0 && top != row)
                                _ws.Cells[top, col].Calculate(option);
                        }
                    }
                });
            }

            var result = new Dictionary<int, VENTFLOWS.Calc>();

            foreach (KeyValuePair<int, VENTFLOWS.Calc> row in _asSheet)
            {
                if (_applied.ContainsKey(row.Key))
                {
                    VENTFLOWS.Calc live = Read(row.Key);
                    live.SheetCells = new Dictionary<int, string>(row.Value.Cells);

                    result[row.Key] = live;
                }
                else if (tops.Contains(row.Key))
                {
                    // Its own calculation as the sheet has it; the merge's air
                    // as worked out
                    VENTFLOWS.Calc live = Read(row.Key);
                    VENTFLOWS.Calc kept = row.Value.Copy();

                    kept.Air = live.Air;
                    kept.FreshError = live.FreshError;
                    kept.ExtractError = live.ExtractError;

                    result[row.Key] = kept;
                }
                else
                {
                    result[row.Key] = row.Value;
                }
            }

            return result;
        }

        // A cell's merge's first row, when it runs down more than one; 0 else
        private int TopOf(int row, int col)
        {
            if (col <= 0)
                return 0;

            string merged = _ws.MergedCells[row, col];

            if (string.IsNullOrEmpty(merged))
                return 0;

            var address = new ExcelAddress(merged);

            return address.Start.Row == address.End.Row ? 0 : address.Start.Row;
        }

        private static bool Same(VENTARRANGE.RoomOccupancy a, VENTARRANGE.RoomOccupancy b)
        {
            if (a == null || b == null)
                return a == null && b == null;

            return a.FreshCategory == b.FreshCategory && a.ExhaustCategory == b.ExhaustCategory && a.Use == b.Use &&
                   Same(a.Occupants, b.Occupants) && Same(a.Units, b.Units);
        }

        private static bool Same(double? a, double? b)
        {
            if (!a.HasValue || !b.HasValue)
                return a.HasValue == b.HasValue;

            return (double.IsNaN(a.Value) && double.IsNaN(b.Value)) || a.Value == b.Value;
        }

        // One row's inputs: each as chosen, or as the sheet had it
        private void Put(int row, VENTARRANGE.RoomOccupancy chosen)
        {
            PutText(row, Layout.FreshCategoryCol, chosen == null ? null : chosen.FreshCategory);
            PutText(row, Layout.ExhaustCategoryCol, chosen == null ? null : chosen.ExhaustCategory);
            PutText(row, Layout.UseCol, chosen == null ? null : chosen.Use);
            PutNumber(row, Layout.OccupantsCol, chosen == null ? null : chosen.Occupants);
            PutNumber(row, Layout.UnitsCol, chosen == null ? null : chosen.Units);
        }

        private void PutText(int row, int col, string text)
        {
            if (col <= 0)
                return;

            if (text == null)
                Restore(row, col);
            else
                Write(row, col, text.Trim().Length == 0 ? null : (object)text.Trim());
        }

        private void PutNumber(int row, int col, double? value)
        {
            if (col <= 0)
                return;

            if (!value.HasValue)
                Restore(row, col);
            else
                Write(row, col, double.IsNaN(value.Value) ? null : (object)value.Value);
        }

        private long Address(int row, int col)
        {
            return (long)row * 100000 + col;
        }

        private void Write(int row, int col, object value)
        {
            ExcelRange cell = _ws.Cells[row, col];

            long at = Address(row, col);

            if (!_original.ContainsKey(at))
                _original[at] = new KeyValuePair<string, object>(cell.Formula, cell.Value);

            if (!string.IsNullOrEmpty(cell.Formula))
                cell.Formula = null;

            cell.Value = value;
        }

        private void Restore(int row, int col)
        {
            KeyValuePair<string, object> was;

            if (!_original.TryGetValue(Address(row, col), out was))
                return;

            ExcelRange cell = _ws.Cells[row, col];

            if (!string.IsNullOrEmpty(was.Key))
            {
                cell.Formula = was.Key;
            }
            else
            {
                cell.Formula = null;
                cell.Value = was.Value;
            }
        }

        // A calculation - again with circular references allowed, when it
        // stopped on one
        private static void Calculate(Action<OfficeOpenXml.FormulaParsing.ExcelCalculationOption> calculate)
        {
            try
            {
                calculate(new OfficeOpenXml.FormulaParsing.ExcelCalculationOption());
                return;
            }
            catch
            {
            }

            try
            {
                calculate(new OfficeOpenXml.FormulaParsing.ExcelCalculationOption { AllowCircularReferences = true });
            }
            catch
            {
                // what could be worked out stands; the rest reads as it was
            }
        }

        // A row as the copy has it now
        private VENTFLOWS.Calc Read(int row)
        {
            var calc = new VENTFLOWS.Calc();

            string error;

            calc.Air.Fresh = Number(row, Layout.FreshAirCol, out error);
            calc.FreshError = error;

            calc.Air.Extract = Number(row, Layout.ExhaustAirCol, out error);
            calc.ExtractError = error;

            foreach (VENTFLOWS.CalcColumn column in Columns)
                calc.Cells[column.Col] = (_ws.Cells[row, column.Col].Text ?? string.Empty).Trim();

            return calc;
        }

        // A final airflow as the sheet's are read (VENTSHEET): blank or not a
        // number, 0 - and an error said
        private double Number(int row, int col, out string error)
        {
            error = null;

            if (col <= 0)
                return 0;

            object raw = _ws.Cells[row, col].Value;

            if (raw == null)
                return 0;

            if (raw is ExcelErrorValue)
            {
                error = raw.ToString();
                return 0;
            }

            double value;

            return double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float,
                                   CultureInfo.InvariantCulture, out value) && !double.IsNaN(value)
                ? value
                : 0;
        }

        // "Dwelling Unit" and the apartment kitchen mended in every room row
        // of the copy, as an export mends the sheet's room row
        private void Mend(int lastRow)
        {
            try
            {
                for (int r = Layout.FirstDataRow; r <= lastRow; r++)
                {
                    var formulas = new string[_lastCol + 1];
                    bool any = false;

                    for (int c = 1; c <= _lastCol; c++)
                    {
                        if (string.IsNullOrEmpty(_ws.Cells[r, c].Formula))
                            continue;

                        formulas[c] = _ws.Cells[r, c].FormulaR1C1;
                        any = true;
                    }

                    if (!any)
                        continue;

                    var before = (string[])formulas.Clone();

                    VENTOCCUPANCY.DwellingCheck(_package.Workbook, formulas);
                    VENTOCCUPANCY.KitchenCheck(_package.Workbook, formulas);

                    for (int c = 1; c <= _lastCol; c++)
                    {
                        if (formulas[c] != before[c])
                            _ws.Cells[r, c].FormulaR1C1 = formulas[c];
                    }
                }
            }
            catch
            {
                // the sheet's formulas as they are, then
            }
        }
    }
}
