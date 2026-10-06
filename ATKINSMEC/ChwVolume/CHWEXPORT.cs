using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.IO;

namespace ATKINSMEC
{
    // Writes the chilled water volume result out as a spreadsheet.
    // Deliberately free of Revit types, so it can be exercised on its own.
    public class CHWEXPORT
    {
        private static readonly string[] Headers =
        {
            "Model",
            "Supply system type",
            "Return system type",
            "Supply (L)",
            "Return (L)",
            "Pipes",
            "Fittings",
            "Pipes (L)",
            "Fittings (L)",
            "Total (L)"
        };

        public static string SuggestedFileName(string modelTitle)
        {
            string name = "CHW volume - " + modelTitle;

            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, ' ');

            return name.Trim() + ".xlsx";
        }

        public static void Write(
            string path,
            string modelTitle,
            CHWVOLUME.Result result)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");

            using (ExcelPackage package = new ExcelPackage())
            {
                ExcelWorksheet sheet =
                    package.Workbook.Worksheets.Add("CHW volume");

                int row = 1;

                sheet.Cells[row, 1].Value = "Chilled water volume";
                sheet.Cells[row, 1].Style.Font.Bold = true;
                sheet.Cells[row, 1].Style.Font.Size = 14;

                row += 2;

                row = Info(sheet, row, "Measured from", modelTitle);
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

                foreach (CHWVOLUME.ModelResult model in result.Models)
                {
                    Row(
                        sheet,
                        row++,
                        RowName(model),
                        model.SupplyTypeName,
                        model.ReturnTypeName,
                        model.SupplyLitres + model.SupplyFittingLitres,
                        model.ReturnLitres + model.ReturnFittingLitres,
                        model.TotalPipes,
                        model.TotalFittings,
                        model.PipeLitres,
                        model.FittingLitres,
                        model.TotalLitres);
                }

                Row(
                    sheet,
                    row,
                    "TOTAL",
                    string.Empty,
                    string.Empty,
                    result.SupplyLitres + result.SupplyFittingLitres,
                    result.ReturnLitres + result.ReturnFittingLitres,
                    result.TotalPipes,
                    result.TotalFittings,
                    result.PipeLitres,
                    result.FittingLitres,
                    result.TotalLitres);

                ExcelRange totals =
                    sheet.Cells[row, 1, row, Headers.Length];

                totals.Style.Font.Bold = true;
                totals.Style.Border.Top.Style = ExcelBorderStyle.Thin;

                row += 2;

                row = Info(sheet, row, "Total litres",
                    result.TotalLitres.ToString("N1"));

                row = Info(sheet, row, "Total m3",
                    result.TotalCubicMetres.ToString("N3"));

                if (result.SkippedNoSize > 0)
                {
                    row = Info(sheet, row, "Skipped, size unreadable",
                        result.SkippedNoSize.ToString("N0"));
                }

                sheet.Cells[headerRow, 1, headerRow, Headers.Length]
                    .AutoFilter = true;

                sheet.View.FreezePanes(headerRow + 1, 1);

                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                if (sheet.Column(1).Width < 30)
                    sheet.Column(1).Width = 30;

                package.SaveAs(new FileInfo(path));
            }
        }

        // Carries the reason a model is missing, or the warning that
        // only part of it was loaded, into the spreadsheet
        private static string RowName(CHWVOLUME.ModelResult model)
        {
            if (!string.IsNullOrEmpty(model.Failure))
                return model.ModelName + "  -  could not open: " + model.Failure;

            if (!string.IsNullOrEmpty(model.WorksetsNote))
                return model.ModelName + "  (" + model.WorksetsNote + ")";

            return model.ModelName;
        }

        private static int Info(
            ExcelWorksheet sheet,
            int row,
            string label,
            string value)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 2].Value = value;

            return row + 1;
        }

        private static void Row(
            ExcelWorksheet sheet,
            int row,
            string name,
            string supplyType,
            string returnType,
            double supplyLitres,
            double returnLitres,
            int pipes,
            int fittings,
            double pipeLitres,
            double fittingLitres,
            double totalLitres)
        {
            sheet.Cells[row, 1].Value = name;
            sheet.Cells[row, 2].Value = supplyType;
            sheet.Cells[row, 3].Value = returnType;
            sheet.Cells[row, 4].Value = supplyLitres;
            sheet.Cells[row, 5].Value = returnLitres;
            sheet.Cells[row, 6].Value = pipes;
            sheet.Cells[row, 7].Value = fittings;
            sheet.Cells[row, 8].Value = pipeLitres;
            sheet.Cells[row, 9].Value = fittingLitres;
            sheet.Cells[row, 10].Value = totalLitres;

            sheet.Cells[row, 4, row, 5].Style.Numberformat.Format = "#,##0.0";
            sheet.Cells[row, 6, row, 7].Style.Numberformat.Format = "#,##0";
            sheet.Cells[row, 8, row, 10].Style.Numberformat.Format = "#,##0.0";
        }
    }
}
