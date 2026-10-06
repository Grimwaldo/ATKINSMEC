using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace ATKINSMEC
{
    // The results as a workbook: one row per unit on "ESP", and every
    // unit's index runs, step by step, on "Index runs". One sheet for all
    // the runs rather than one per unit - a floor of fan coils is a
    // hundred units, and a hundred worksheets is not a report.
    //
    // Numbers go in as numbers and are only FORMATTED, so a figure can be
    // summed or checked in Excel - the .Value/.Text lesson of CHWSIZESHEET.
    // Revit-free.
    public static class ESPEXPORT
    {
        public static void Write(string path, ESPRUN run, ESPSETTINGS settings, string title)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Mohamad Hamza");

            if (File.Exists(path))
                File.Delete(path);

            using (var package = new ExcelPackage())
            {
                Summary(package.Workbook.Worksheets.Add("ESP"), run, settings, title);
                Steps(package.Workbook.Worksheets.Add("Index runs"), run);

                package.SaveAs(new FileInfo(path));
            }
        }

        private static void Head(ExcelRange cells)
        {
            cells.Style.Font.Bold = true;
            cells.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cells.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(232, 241, 248));
            cells.Style.WrapText = true;
            cells.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
        }

        private static void Summary(ExcelWorksheet ws, ESPRUN run, ESPSETTINGS settings, string title)
        {
            ws.Cells[1, 1].Value = "External static pressure - " + title;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 13;

            ws.Cells[2, 1].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm") +
                                   "   margin " + settings.MarginPercent + " %   air " + settings.Density +
                                   " kg/m3   joint tolerance between models " + settings.ToleranceMm + " mm";

            ws.Cells[3, 1].Value = settings.TableIsBuiltIn()
                ? "Fitting coefficients: the add-in's BUILT-IN rough values, not the office's - check before relying on these figures."
                : "Fitting coefficients: as edited in the window.";
            ws.Cells[3, 1].Style.Font.Color.SetColor(
                settings.TableIsBuiltIn() ? Color.FromArgb(154, 91, 0) : Color.FromArgb(31, 41, 51));

            ws.Cells[4, 1].Value = "ESP = the worst outlet + the worst inlet. Return and fresh air arrive in parallel, " +
                                   "so only the worse of them counts. An AHU with its own return fan needs two figures - " +
                                   "use the per-port losses on 'Index runs'.";

            string[] heads =
            {
                "Unit", "Mark", "Model", "Worst outlet (Pa)", "Worst inlet (Pa)", "ESP (Pa)",
                "ESP + margin (Pa)", "'" + settings.Parameter + "' now", "Ports",
                "Terminals", "From defaults on index runs", "Notes"
            };

            const int top = 6;

            for (int i = 0; i < heads.Length; i++)
                ws.Cells[top, i + 1].Value = heads[i];

            Head(ws.Cells[top, 1, top, heads.Length]);

            int row = top + 1;

            foreach (ESPNET.Result result in run.Results)
            {
                double? now;
                run.InModel.TryGetValue(result.Unit.Key, out now);

                ws.Cells[row, 1].Value = result.Unit.Family;
                ws.Cells[row, 2].Value = result.Unit.Mark;
                ws.Cells[row, 3].Value = result.Unit.Model;
                ws.Cells[row, 4].Value = Math.Round(result.OutSide, 1);
                ws.Cells[row, 5].Value = Math.Round(result.InSide, 1);
                ws.Cells[row, 6].Value = Math.Round(result.Esp, 1);
                ws.Cells[row, 7].Value = Math.Round(result.EspWithMargin, 1);

                if (now.HasValue)
                    ws.Cells[row, 8].Value = now.Value;

                // A port that reaches nothing is a finding, not a zero
                ws.Cells[row, 9].Value = string.Join(", ", result.Runs.Select(r =>
                    (r.System.Length == 0 ? "?" : r.System) + " " +
                    (!r.Connected
                        ? "not joined"
                        : r.Terminals + r.Equipment == 0
                            ? "reaches no terminal"
                            : Math.Round(r.Index).ToString(System.Globalization.CultureInfo.InvariantCulture) + " Pa")));

                ws.Cells[row, 10].Value = result.Runs.Sum(r => r.Terminals);
                ws.Cells[row, 11].Value = result.Runs.Sum(r => r.FlaggedOnIndex);
                ws.Cells[row, 12].Value = string.Join("; ", result.Runs.SelectMany(r => r.Notes).Distinct());

                if (result.Runs.Sum(r => r.FlaggedOnIndex) > 0)
                    ws.Cells[row, 11].Style.Font.Color.SetColor(Color.FromArgb(154, 91, 0));

                row++;
            }

            ws.Cells[top + 1, 4, Math.Max(top + 1, row - 1), 8].Style.Numberformat.Format = "0.0";

            ws.Column(1).Width = 36;
            ws.Column(2).Width = 12;
            ws.Column(3).Width = 30;

            for (int c = 4; c <= 8; c++)
                ws.Column(c).Width = 13;

            ws.Column(9).Width = 34;
            ws.Column(10).Width = 10;
            ws.Column(11).Width = 14;
            ws.Column(12).Width = 70;

            ws.View.FreezePanes(top + 1, 2);
        }

        private static void Steps(ExcelWorksheet ws, ESPRUN run)
        {
            string[] heads =
            {
                "Unit", "Port", "What", "Size", "Flow (L/s)", "Velocity (m/s)", "Length (m)",
                "Friction (Pa/m)", "C", "Loss (Pa)", "Running (Pa)", "Figure from", "Default?", "Model", "Element", "Id"
            };

            for (int i = 0; i < heads.Length; i++)
                ws.Cells[1, i + 1].Value = heads[i];

            Head(ws.Cells[1, 1, 1, heads.Length]);

            int row = 2;

            foreach (ESPNET.Result result in run.Results)
            {
                foreach (ESPNET.Run r in result.Runs.Where(x => x.Connected))
                {
                    string port = (r.System.Length == 0 ? "?" : r.System) + (r.Converging ? " (in)" : " (out)") +
                                  " - index " + Math.Round(r.Index, 1) + " Pa";

                    ws.Cells[row, 1].Value = result.Unit.Label;
                    ws.Cells[row, 2].Value = port;
                    ws.Cells[row, 3].Value = "ends at " + r.EndsAt;
                    ws.Cells[row, 1, row, heads.Length].Style.Font.Bold = true;
                    row++;

                    foreach (ESPNET.Step s in r.Steps)
                    {
                        ws.Cells[row, 1].Value = result.Unit.Label;
                        ws.Cells[row, 2].Value = port;
                        ws.Cells[row, 3].Value = s.What;
                        ws.Cells[row, 4].Value = s.Size;
                        ws.Cells[row, 5].Value = Math.Round(s.Flow * 1000, 1);
                        ws.Cells[row, 6].Value = Math.Round(s.Velocity, 2);
                        ws.Cells[row, 7].Value = Math.Round(s.Length, 2);
                        ws.Cells[row, 8].Value = Math.Round(s.PerMetre, 3);
                        ws.Cells[row, 9].Value = Math.Round(s.Coefficient, 2);
                        ws.Cells[row, 10].Value = Math.Round(s.Loss, 1);
                        ws.Cells[row, 11].Value = Math.Round(s.Running, 1);
                        ws.Cells[row, 12].Value = s.Source;
                        ws.Cells[row, 13].Value = s.Flagged ? "DEFAULT" : string.Empty;
                        ws.Cells[row, 14].Value = s.Part.Model;
                        ws.Cells[row, 15].Value = s.Part.Label;
                        ws.Cells[row, 16].Value = s.Part.ElementId;

                        if (s.Flagged)
                            ws.Cells[row, 1, row, heads.Length].Style.Font.Color.SetColor(Color.FromArgb(154, 91, 0));

                        row++;
                    }

                    row++;
                }
            }

            int[] widths = { 30, 26, 20, 14, 10, 10, 10, 11, 7, 10, 11, 46, 10, 28, 36, 10 };

            for (int i = 0; i < widths.Length; i++)
                ws.Column(i + 1).Width = widths[i];

            ws.View.FreezePanes(2, 4);
        }
    }
}
