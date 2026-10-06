using OfficeOpenXml;
using System;
using System.Linq;
ExcelPackage.License.SetNonCommercialPersonal("probe");
using var p = new ExcelPackage(new System.IO.FileInfo(RepoPath.Find(@"ATKINSMEC\Resources\VentTemplate.xlsx")));
foreach (string name in new[] { "Tower-1", "Tower 2", "MLCP" })
{
    var ws = p.Workbook.Worksheets[name];
    Console.WriteLine("===== " + name + "  hidden cols: " + string.Join(",", Enumerable.Range(1, 15).Where(c => ws.Column(c).Hidden).Select(c => ExcelCellAddress.GetColumnLetter(c))));
    // where are apartment-like groups: rows whose F is empty but H has text, inside merged F ranges spanning rows
    var vmerges = ws.MergedCells.Where(m => m != null).Select(m => new ExcelAddress(m)).Where(a => a.Start.Row != a.End.Row && a.Start.Row > 9 && a.Start.Column <= 14).ToList();
    Console.WriteLine("vertical merges in A:N below the header: " + vmerges.Count + "  e.g. " + string.Join(" ", vmerges.Take(12).Select(a => a.Address)));
    var cols = vmerges.GroupBy(a => ExcelCellAddress.GetColumnLetter(a.Start.Column) + ":" + ExcelCellAddress.GetColumnLetter(a.End.Column)).Select(g => g.Key + " x" + g.Count());
    Console.WriteLine("  by columns: " + string.Join(", ", cols));
}
var t1 = p.Workbook.Worksheets["Tower-1"];
for (int r = 104; r <= 128; r++)
{
    var parts = Enumerable.Range(1, 16).Select(c => {
        var cell = t1.Cells[r, c];
        string f = cell.Formula;
        string v = cell.Value?.ToString();
        if (!string.IsNullOrEmpty(f)) return ExcelCellAddress.GetColumnLetter(c) + "=" + ("=" + f).Substring(0, Math.Min(30, f.Length + 1));
        if (!string.IsNullOrEmpty(v)) return ExcelCellAddress.GetColumnLetter(c) + ":" + v.Substring(0, Math.Min(26, v.Length));
        return null; }).Where(s => s != null);
    string m = string.Join(" ", Enumerable.Range(1, 16).Select(c => t1.MergedCells[r, c]).Where(x => x != null).Distinct());
    Console.WriteLine(r + " [" + m + "] " + string.Join(" | ", parts));
}
// header text of F..H rows 6-9
for (int c = 5; c <= 12; c++) Console.WriteLine("hdr " + ExcelCellAddress.GetColumnLetter(c) + ": " + string.Join(" / ", Enumerable.Range(6, 4).Select(r => t1.Cells[r, c].Text).Where(s => s.Length > 0)) + "   merged " + t1.MergedCells[6, c]);
