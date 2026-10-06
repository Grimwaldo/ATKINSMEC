using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ATKINSMEC
{
    // The chilled water pipe sizing table, read out of Excel.
    //
    // One row per nominal size, carrying the greatest flow that size is
    // allowed to take. The supplied sheet works that limit out from a
    // max velocity and a 250 Pa/m pressure drop per ASHRAE 90.1, but
    // none of that matters here: this only needs the size and the flow
    // it tops out at.
    //
    // Deliberately Revit-free, so it can be exercised against the real
    // spreadsheet with no model open.
    public class CHWSIZESHEET
    {
        public class Size
        {
            // Nominal bore in mm - the DN designator
            public double DN { get; set; }

            // The most this size may carry, litres per second
            public double MaxLitresPerSecond { get; set; }

            // Shown in the findings so a person can see the reasoning
            public double BoreMm { get; set; }
            public double MaxSpeed { get; set; }

            public int Row { get; set; }

            public override string ToString()
            {
                return "DN" + DN.ToString("N0") + "   up to " +
                       MaxLitresPerSecond.ToString("N2") + " L/s";
            }
        }

        public class Table
        {
            // Ascending by DN, which is what the lookups rely on
            public List<Size> Sizes = new List<Size>();

            public string Worksheet = string.Empty;

            // The smallest size that can carry this flow, or null when
            // the flow is past the end of the table
            public Size SmallestFor(double litresPerSecond)
            {
                foreach (Size size in Sizes)
                {
                    if (size.MaxLitresPerSecond >= litresPerSecond)
                        return size;
                }

                return null;
            }

            // The table row for a size drawn in the model. Matched
            // within a tolerance because a nominal size comes back from
            // Revit as 49.999999 often enough to matter.
            public Size Match(double dnMm, double toleranceMm)
            {
                Size best = null;
                double closest = double.MaxValue;

                foreach (Size size in Sizes)
                {
                    double apart = Math.Abs(size.DN - dnMm);

                    if (apart <= toleranceMm && apart < closest)
                    {
                        closest = apart;
                        best = size;
                    }
                }

                return best;
            }

            // How many sizes apart two rows are, for judging oversizing
            public int StepsBetween(Size from, Size to)
            {
                int one = Sizes.IndexOf(from);
                int two = Sizes.IndexOf(to);

                if (one < 0 || two < 0)
                    return 0;

                return two - one;
            }

            public Size Largest
            {
                get
                {
                    return Sizes.Count == 0
                        ? null
                        : Sizes[Sizes.Count - 1];
                }
            }
        }

        public class Layout
        {
            public int HeaderRow;
            public int SizeColumn;
            public int FlowColumn;
            public int BoreColumn;
            public int SpeedColumn;

            public bool Found
            {
                get
                {
                    return HeaderRow > 0
                           && SizeColumn > 0
                           && FlowColumn > 0;
                }
            }
        }

        // ==================================================
        // The table this add-in ships with
        // ==================================================

        // Taken from the office's own sheet - "Chilled Water Pipe Sizing
        // - ASHRAE 90", ASTM A53, dated 12 July 2025. Nobody has to go
        // and find a spreadsheet before the check will run.
        //
        // The numbers are the workbook's own, at full precision, and
        // were GENERATED from it rather than typed - see
        // scratchpad/chwsizetest, which reads the real file and asserts
        // every row here still matches it. If the standard changes,
        // change these and let that harness confirm it.
        //
        // Below DN65 the limit is a 1.2 m/s speed cap trimmed to hold
        // 250 Pa/m; from DN65 up it is ASHRAE 90.1's own flow, likewise
        // trimmed; from DN350 it is 90.1's 2.286 m/s (7.5 ft/s) cap.
        public static Table Builtin()
        {
            var table = new Table { Worksheet = "ASHRAE 90.1  (built in)" };

            table.Sizes.Add(Row(20, 0.18977330777945564, 20.96, 0.55));
            table.Sizes.Add(Row(25, 0.35672890139025265, 26.64, 0.64));
            table.Sizes.Add(Row(32, 0.734552164883111, 35.08, 0.76));
            table.Sizes.Add(Row(40, 1.10577010415809, 40.94, 0.84));
            table.Sizes.Add(Row(50, 2.1414736192215442, 52.48, 0.99));
            table.Sizes.Add(Row(65, 3.4250809142965233, 62.68, 1.11));
            table.Sizes.Add(Row(80, 6.056078418145485, 77.92, 1.27));
            table.Sizes.Add(Row(100, 12.40161958626914, 102.26, 1.51));
            table.Sizes.Add(Row(150, 36.35942715683508, 154.08, 1.95));
            table.Sizes.Add(Row(200, 69.40763166320106, 202.74, 2.15));
            table.Sizes.Add(Row(250, 101.70889188564774, 254.46, 2.0));
            table.Sizes.Add(Row(300, 145.8934541957209, 304.76, 2.0));
            table.Sizes.Add(Row(350, 203.347804698804, 336.54, 2.286));
            table.Sizes.Add(Row(400, 268.8149153117862, 386.94, 2.286));
            table.Sizes.Add(Row(450, 344.34623231938315, 437.94, 2.286));
            table.Sizes.Add(Row(500, 429.21732521532135, 488.94, 2.286));
            table.Sizes.Add(Row(600, 626.9788386722213, 590.94, 2.286));

            return table;
        }

        // NOT called Size: that name is the nested class above
        private static Size Row(
            double dn, double litres, double bore, double speed)
        {
            return new Size
            {
                DN = dn,
                MaxLitresPerSecond = litres,
                BoreMm = bore,
                MaxSpeed = speed
            };
        }

        // ==================================================
        // Reading one out of a spreadsheet
        // ==================================================
        //
        // Not reachable from the window any more - the built-in table is
        // what the check uses. It stays because it is how the numbers
        // above were produced and how they are verified: the harness
        // reads the real workbook through this and compares. Deleting it
        // would leave the constants above with nothing checking them.

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

        public static Table Read(string path, string worksheet)
        {
            License();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new Exception("Choose the sizing spreadsheet first.");

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
                    throw new Exception(
                        "Worksheet \"" + sheet.Name + "\" is empty.");

                Layout layout = Detect(sheet);

                if (!layout.Found)
                {
                    throw new Exception(
                        "Could not find the columns in worksheet \"" +
                        sheet.Name + "\". It needs a heading naming the " +
                        "nominal size (DN) and another naming the maximum " +
                        "flow.");
                }

                Units(sheet, layout);

                var table = new Table { Worksheet = sheet.Name };

                table.Sizes = Rows(sheet, layout);

                if (table.Sizes.Count == 0)
                {
                    throw new Exception(
                        "Found the headings in worksheet \"" + sheet.Name +
                        "\" but no rows under them with both a size and a " +
                        "maximum flow.");
                }

                table.Sizes.Sort(
                    delegate (Size a, Size b)
                    {
                        return a.DN.CompareTo(b.DN);
                    });

                return table;
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
            int columns = Math.Min(sheet.Dimension.End.Column, 40);

            for (int r = 1; r <= rows && layout.HeaderRow == 0; r++)
            {
                int size = 0;
                int flow = 0;
                int bore = 0;
                int speed = 0;

                for (int c = 1; c <= columns; c++)
                {
                    string text = Normalise(Text(sheet, r, c));

                    if (text.Length == 0)
                        continue;

                    if (size == 0 && IsSize(text))
                        size = c;

                    if (flow == 0 && IsFlow(text))
                        flow = c;

                    if (bore == 0 && text.Contains("inner"))
                        bore = c;

                    if (speed == 0
                        && (text.Contains("speed") || text.Contains("velocity")))
                        speed = c;
                }

                // A row only counts as the heading when it names both of
                // the two things this actually needs
                if (size > 0 && flow > 0)
                {
                    layout.HeaderRow = r;
                    layout.SizeColumn = size;
                    layout.FlowColumn = flow;
                    layout.BoreColumn = bore;
                    layout.SpeedColumn = speed;
                }
            }

            return layout;
        }

        private static bool IsSize(string text)
        {
            // "Pipe DN Designator", "DN", "Nominal size". Outer/inner
            // diameter columns must not win this.
            if (text.Contains("outer") || text.Contains("inner")
                || text.Contains("thickness") || text.Contains("area"))
                return false;

            return Word(text, "dn")
                   || text.Contains("designator")
                   || (text.Contains("nominal") && text.Contains("size"));
        }

        private static bool IsFlow(string text)
        {
            return text.Contains("flow")
                   && !text.Contains("velocity")
                   && !text.Contains("speed");
        }

        // "dn" as its own word, so "condenser" does not match
        private static bool Word(string text, string wanted)
        {
            foreach (string part in text.Split(
                         ' ', '.', ',', '(', ')', '/', '-', ':'))
            {
                if (part == wanted)
                    return true;
            }

            return false;
        }

        // The row under the heading usually carries the units. Only one
        // thing here is worth refusing over: a flow given in gallons
        // would be read as litres and silently pass everything.
        private static void Units(ExcelWorksheet sheet, Layout layout)
        {
            int row = layout.HeaderRow + 1;

            if (row > sheet.Dimension.End.Row)
                return;

            string units = Normalise(Text(sheet, row, layout.FlowColumn));

            if (units.Length == 0)
                return;

            if (units.Contains("gpm") || units.Contains("gal")
                || units.Contains("cfm") || units.Contains("m3"))
            {
                throw new Exception(
                    "The maximum flow column is in \"" + units.Trim() +
                    "\". This check reads litres per second, and would " +
                    "read those numbers as L/s and pass everything. " +
                    "Convert the column, or point it at a sheet in L/s.");
            }
        }

        // ==================================================
        // The rows
        // ==================================================

        private static List<Size> Rows(ExcelWorksheet sheet, Layout layout)
        {
            var sizes = new List<Size>();

            var seen = new HashSet<double>();

            for (int r = layout.HeaderRow + 1;
                 r <= sheet.Dimension.End.Row;
                 r++)
            {
                double dn;
                double flow;

                if (!Number(sheet, r, layout.SizeColumn, out dn))
                    continue;

                if (!Number(sheet, r, layout.FlowColumn, out flow))
                    continue;

                if (dn <= 0 || flow <= 0 || !seen.Add(dn))
                    continue;

                double bore;
                double speed;

                Number(sheet, r, layout.BoreColumn, out bore);
                Number(sheet, r, layout.SpeedColumn, out speed);

                sizes.Add(new Size
                {
                    DN = dn,
                    MaxLitresPerSecond = flow,
                    BoreMm = bore,
                    MaxSpeed = speed,
                    Row = r
                });
            }

            return sizes;
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

        // The VALUE, not the formatted text. A cell holding 0.1898 and
        // formatted to two places reads back as "0.19" through .Text,
        // and the limits would then depend on how somebody chose to
        // display their spreadsheet. Format it to no decimals at all and
        // DN20's limit would read as 0.
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

            // A number typed as text still counts
            return Number(raw.ToString(), out value);
        }

        private static bool Number(string text, out double value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleaned = text.Replace(",", string.Empty).Trim();

            return double.TryParse(cleaned, NumberStyles.Any,
                       CultureInfo.InvariantCulture, out value)
                   || double.TryParse(cleaned, NumberStyles.Any,
                       CultureInfo.CurrentCulture, out value);
        }

        // ==================================================
        // Writing the table back out
        // ==================================================

        // THE ROUND TRIP IS THE WHOLE POINT, so the headings written
        // here are the ones Detect() hunts for rather than whatever
        // reads best: "Pipe DN Designator" carries "dn" as its own
        // word, "Maximum Flow" carries "flow" without "velocity" or
        // "speed", "Inner Diameter" carries "inner" and "Maximum
        // Velocity" carries "velocity". Change a heading here and this
        // writes a file it can no longer read - which is why
        // scratchpad/chwsizetest asserts the trip out and back, row by
        // row, rather than just that a file appeared.
        //
        // The UNITS ROW matters every bit as much. Read() refuses a
        // flow column in gpm or gallons rather than silently taking
        // them for litres, so this writes "L/s" exactly where it will
        // be looked for.
        //
        // NUMBERS GO IN AT FULL PRECISION AND ARE ONLY *FORMATTED* TO
        // SOMETHING READABLE. That distinction is the one this file
        // already turns on: Read() takes .Value and never .Text, so
        // how a cell is displayed cannot change what comes back. A
        // limit of 0.18977330777945564 shown as 0.1898 still reads
        // back whole.
        public static void Write(Table table, string path)
        {
            License();

            if (table == null || table.Sizes.Count == 0)
                throw new Exception("There is no sizing table to export.");

            if (string.IsNullOrWhiteSpace(path))
                throw new Exception("Choose where to save the file.");

            var file = new FileInfo(path);

            // SaveAs onto an existing package appends rather than
            // replaces, so the old file goes first
            if (file.Exists)
                file.Delete();

            using (var package = new ExcelPackage())
            {
                ExcelWorksheet sheet =
                    package.Workbook.Worksheets.Add("CHW Pipe Sizing");

                sheet.Cells[1, 1].Value =
                    "ATKINSMEC - chilled water pipe sizing. Edit the " +
                    "numbers below and import this file back into " +
                    "SHEET QA. Keep the headings on row 2.";

                sheet.Cells[1, 1].Style.Font.Italic = true;

                var headings = new[]
                {
                    "Pipe DN Designator",
                    "Maximum Flow",
                    "Inner Diameter",
                    "Maximum Velocity"
                };

                var units = new[] { "mm", "L/s", "mm", "m/s" };

                var formats = new[] { "0", "0.0000", "0.00", "0.000" };

                for (int c = 0; c < headings.Length; c++)
                {
                    sheet.Cells[2, c + 1].Value = headings[c];
                    sheet.Cells[2, c + 1].Style.Font.Bold = true;

                    sheet.Cells[3, c + 1].Value = units[c];
                }

                int row = 4;

                foreach (Size size in table.Sizes)
                {
                    sheet.Cells[row, 1].Value = size.DN;
                    sheet.Cells[row, 2].Value = size.MaxLitresPerSecond;

                    // Zero means the source never carried it. Writing a
                    // 0 back would turn "not stated" into "stated as
                    // nothing" on the next trip round.
                    if (size.BoreMm > 0)
                        sheet.Cells[row, 3].Value = size.BoreMm;

                    if (size.MaxSpeed > 0)
                        sheet.Cells[row, 4].Value = size.MaxSpeed;

                    for (int c = 0; c < formats.Length; c++)
                        sheet.Cells[row, c + 1].Style.Numberformat.Format =
                            formats[c];

                    row++;
                }

                // Widths are set rather than auto-fitted: AutoFitColumns
                // measures text with the system's fonts and can throw
                // where there are none to measure
                sheet.Column(1).Width = 22;
                sheet.Column(2).Width = 18;
                sheet.Column(3).Width = 18;
                sheet.Column(4).Width = 20;

                package.SaveAs(file);
            }
        }

        private static void License()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");
        }
    }
}
