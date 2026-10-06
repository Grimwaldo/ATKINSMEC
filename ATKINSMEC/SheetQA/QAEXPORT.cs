using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATKINSMEC
{
    // Writes the QA findings out as a spreadsheet. Deliberately free of
    // Revit types, so it can be exercised on its own.
    public class QAEXPORT
    {
        private static readonly string[] Headers =
        {
            "Severity",
            "Check",
            "Sheet number",
            "Sheet name",
            "View",
            "Element",
            "Problem",
            "Value",
            "Element id"
        };

        public static string SuggestedFileName(string modelTitle)
        {
            string name = "QA sheets - " + modelTitle;

            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, ' ');

            return name.Trim() + ".xlsx";
        }

        public static void Write(
            string path,
            string modelTitle,
            List<QAFinding> findings,
            string scopeDescription)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");

            using (ExcelPackage package = new ExcelPackage())
            {
                ExcelWorksheet sheet =
                    package.Workbook.Worksheets.Add("QA findings");

                int row = 1;

                sheet.Cells[row, 1].Value = "Sheet QA findings";
                sheet.Cells[row, 1].Style.Font.Bold = true;
                sheet.Cells[row, 1].Style.Font.Size = 14;

                row += 2;

                row = Info(sheet, row, "Model", modelTitle);
                row = Info(sheet, row, "Checked", scopeDescription);
                row = Info(sheet, row, "Findings", findings.Count.ToString("N0"));
                row = Info(sheet, row, "Exported",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

                row++;

                int headerRow = row;

                for (int i = 0; i < Headers.Length; i++)
                {
                    ExcelRange cell = sheet.Cells[headerRow, i + 1];

                    cell.Value = Headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                row++;

                foreach (QAFinding finding in findings)
                {
                    sheet.Cells[row, 1].Value = finding.Severity.ToString();
                    sheet.Cells[row, 2].Value = finding.Check;
                    sheet.Cells[row, 3].Value = finding.SheetNumber;
                    sheet.Cells[row, 4].Value = finding.SheetName;
                    sheet.Cells[row, 5].Value = finding.ViewName;
                    sheet.Cells[row, 6].Value = finding.Element;
                    sheet.Cells[row, 7].Value = finding.Problem;
                    sheet.Cells[row, 8].Value = finding.Value;
                    sheet.Cells[row, 9].Value = finding.ElementId;

                    row++;
                }

                if (findings.Count > 0)
                {
                    sheet.Cells[headerRow, 1, headerRow, Headers.Length]
                        .AutoFilter = true;

                    sheet.View.FreezePanes(headerRow + 1, 1);
                }

                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                // Element id is for looking things up, not for reading
                sheet.Column(9).Width = 12;

                package.SaveAs(new FileInfo(path));
            }
        }

        private static int Info(
            ExcelWorksheet sheet, int row, string label, string value)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 2].Value = value;

            return row + 1;
        }
    }
}
