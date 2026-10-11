using ATKINSMEC;
using OfficeOpenXml;
using OfficeOpenXml.DataValidation.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Runs VENTOFFICE against the REAL office workbook and reads what it
// wrote straight back - with EPPlus, with the real VENTSHEET importer,
// and through EPPlus's own calculation engine.
internal static class Program
{
    // The office workbook as the add-in embeds it - byte for byte the
    // "API TWIN TOWER PODIUM VENTILATION CALCULATIONS" file
    static readonly string Source =
        RepoPath.Find(@"ATKINSMEC\Resources\VentTemplate.xlsx");

    static readonly string Out = Path.Combine(
        Path.GetDirectoryName(typeof(Program).Assembly.Location), "out");

    static int passed, failed;

    static void Check(bool ok, string what)
    {
        if (ok) passed++;
        else failed++;

        Console.WriteLine((ok ? "  ok    " : "  FAIL  ") + what);
    }

    static int Main()
    {
        ExcelPackage.License.SetNonCommercialPersonal("officetest");
        Directory.CreateDirectory(Out);

        byte[] template = File.ReadAllBytes(Source);

        RefsTests();

        foreach (string pattern in VENTOFFICE.Patterns)
        {
            NewWorkbookTest(template, pattern, Small(), "small");
            NewWorkbookTest(template, pattern, Big(), "big");
        }

        HomeTest(template);
        FixturesTest(template);
        ApartmentsTest(template);
        ArrangedTest(template);
        AddToOfficeTest(template);
        AddToBareTest(template);
        RefusalTests(template);

        Console.WriteLine();
        Console.WriteLine(passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }

    // ==================================================
    // Jobs
    // ==================================================

    static List<VENTTEMPLATE.Level> Small()
    {
        return new List<VENTTEMPLATE.Level>
        {
            L("GR_GROUND FLOOR", 0, R("ICT ROOM", "G-01", 9, 3.6), R("LOBBY", "G-02", 36.4, 3.6), R("ELEC ROOM", "0123", 31.8, 3.6)),
            L("Level 01", 4, R("OFFICE", "101", 120.5, 2.8), R("MEETING", "102", 24.25, 2.8)),
            L("Roof", 8, R("PLANT", "R-01", 300, 4.2))
        };
    }

    static List<VENTTEMPLATE.Level> Big()
    {
        var levels = new List<VENTTEMPLATE.Level>();

        for (int l = 0; l < 12; l++)
        {
            var rooms = new List<VENTTEMPLATE.Room>();

            for (int r = 0; r < 25; r++)
                rooms.Add(new VENTTEMPLATE.Room { Name = "ROOM " + r, Number = (l + 1) + "-" + r.ToString("00"), AreaM2 = 10 + r, HeightM = 3 });

            levels.Add(new VENTTEMPLATE.Level { LevelName = "Level " + (l + 1).ToString("00"), Elevation = l * 4, Rooms = rooms });
        }

        return levels;
    }

    static VENTTEMPLATE.Level L(string name, double z, params VENTTEMPLATE.Room[] rooms)
    {
        return new VENTTEMPLATE.Level { LevelName = name, Elevation = z, Rooms = rooms.ToList() };
    }

    static VENTTEMPLATE.Room R(string name, string number, double area, double height)
    {
        return new VENTTEMPLATE.Room { Name = name, Number = number, AreaM2 = area, HeightM = height };
    }

    static VENTOFFICE.Job Job(string sheet, List<VENTTEMPLATE.Level> levels, string pattern)
    {
        return new VENTOFFICE.Job
        {
            SheetName = sheet,
            ProjectName = "NEW HOSPITAL",
            ProjectNumber = "7000123",
            Levels = levels,
            Pattern = pattern
        };
    }

    // ==================================================
    // Apartments, in the office's Tower-1 pattern
    // ==================================================

    static VENTTEMPLATE.Room In(string apartmentKey, string apartment, string aptNumber,
                                string name, string number, double area)
    {
        var r = R(name, number, area, 3.5);
        r.ApartmentKey = apartmentKey;
        r.Apartment = apartment;
        r.ApartmentNumber = aptNumber;
        return r;
    }

    static void ApartmentsTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== apartments: the apartment down the Space column, its rooms beside it");

        // Deliberately SHUFFLED, as the links would hand them over
        var found = new List<VENTTEMPLATE.Room>
        {
            In("9:2", "DRIVER/STAFF APT LARGE", "P04-02", "Toilet", "P04-02-03", 3.88),
            R("LOBBY", "001", 40, 3.5),
            In("9:1", "DRIVER/STAFF APT", "P04-01", "Toilet", "P04-01-02", 3.5),
            In("9:2", "DRIVER/STAFF APT LARGE", "P04-02", "Bed room", "P04-02-01", 7.8),
            In("9:3", "STUDIO", "P04-03", "Studio room", "P04-03-01", 28),
            In("9:1", "DRIVER/STAFF APT", "P04-01", "Bed room", "P04-01-01", 19),
            R("CORRIDOR", "002", 70, 3.7),
            In("9:2", "DRIVER/STAFF APT LARGE", "P04-02", "Living", "P04-02-02", 17.5)
        };

        List<VENTTEMPLATE.Room> ordered = VENTTEMPLATE.Ordered(found);

        Check(string.Join(",", ordered.Select(r => r.Number)) ==
              "001,002,P04-01-01,P04-01-02,P04-02-01,P04-02-02,P04-02-03,P04-03-01",
              "each apartment's rooms come out together, the apartments among the rooms by number: " +
              string.Join(",", ordered.Select(r => r.Number)));

        var levels = new List<VENTTEMPLATE.Level> { L("PODIUM LEVEL 2", 0, ordered.ToArray()) };

        string path = Path.Combine(Out, "apartments.xlsx");

        VENTOFFICE.Result result = VENTOFFICE.NewWorkbook(template, Job("Podium", levels, null), path);

        Check(result.Apartments == 3, "three apartment groups written (" + result.Apartments + ")");

        using (var p = new ExcelPackage(new FileInfo(path)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Podium"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            Check(layout.SpaceEndCol == layout.SpaceNameCol + 2,
                  "the Space block is three columns wide, F to H (" +
                  ExcelCellAddress.GetColumnLetter(layout.SpaceNameCol) + ":" +
                  ExcelCellAddress.GetColumnLetter(layout.SpaceEndCol) + ")");

            Check(layout.BedroomsCol > 0 &&
                  ws.Cells[layout.HeaderRow, layout.BedroomsCol].Text.IndexOf("Bedrooms", StringComparison.OrdinalIgnoreCase) >= 0,
                  "and \"No of Bedrooms\" is found (" + ExcelCellAddress.GetColumnLetter(layout.BedroomsCol) + ")");

            int f = layout.SpaceNameCol, g = f + 1, h = layout.SpaceEndCol;

            int Row(string number) =>
                Enumerable.Range(layout.FirstDataRow, 40).First(r => ws.Cells[r, layout.RoomNumberCol].Text == number);

            int a1 = Row("P04-01-01"), a2 = Row("P04-01-02");
            int b1 = Row("P04-02-01"), b3 = Row("P04-02-03");
            int c1 = Row("P04-03-01"), lobby = Row("001");

            string Merge(int row, int col) => ws.MergedCells[row, col];

            Check(Merge(a1, f) == ExcelCellAddress.GetColumnLetter(f) + a1 + ":" + ExcelCellAddress.GetColumnLetter(f) + a2 &&
                  Merge(a1, g) == ExcelCellAddress.GetColumnLetter(g) + a1 + ":" + ExcelCellAddress.GetColumnLetter(g) + a2,
                  "the two-room apartment: F and G merged down over its two rows, as the office does - " + Merge(a1, f) + ", " + Merge(a1, g));

            Check(Merge(b1, f) == ExcelCellAddress.GetColumnLetter(f) + b1 + ":" + ExcelCellAddress.GetColumnLetter(f) + b3,
                  "the three-room apartment: over its three - " + Merge(b1, f));

            Check(Merge(a1, layout.BedroomsCol) != null && Merge(b1, layout.BedroomsCol) != null,
                  "and \"No of Bedrooms\" merged down over each, one figure per dwelling unit");

            Check(ws.Cells[a1, f].Text == "DRIVER/STAFF APT" && ws.Cells[b1, f].Text == "DRIVER/STAFF APT LARGE",
                  "the apartment's name heads its rows");

            Check(ws.Cells[a1, h].Text == "Bed room" && ws.Cells[a2, h].Text == "Toilet" &&
                  ws.Cells[b1 + 1, h].Text == "Living",
                  "each room's OWN name in H, one per row");

            Check(ws.Cells[a1, layout.RoomNumberCol].Text == "P04-01-01" && ws.Cells[a2, layout.RoomNumberCol].Text == "P04-01-02",
                  "and its own number in the Room Number column");

            Check(Convert.ToDouble(ws.Cells[a1, layout.AreaCol].Value) == 19 &&
                  Convert.ToDouble(ws.Cells[b1 + 1, layout.AreaCol].Value) == 17.5,
                  "area per room, not per apartment");

            Check(Merge(a1, h) == null && Merge(a2, h) == null,
                  "H is not merged on an apartment's rows - the room template's F:H merge was undone");

            Check(Merge(lobby, f) != null && new ExcelAddress(Merge(lobby, f)).End.Column == h &&
                  new ExcelAddress(Merge(lobby, f)).Start.Row == new ExcelAddress(Merge(lobby, f)).End.Row,
                  "an ordinary room keeps its name merged across F:H on its one row");

            Check(ws.Cells[c1, f].Text == "STUDIO" && ws.Cells[c1, h].Text == "Studio room" && Merge(c1, f) == null,
                  "a one-room apartment: the apartment in F, the room in H, nothing to merge down");

            // User (2026-10-11): the apartment's number from the
            // architect's model in the column beside it, which Tower-1
            // leaves empty
            Check(ws.Cells[a1, g].Text == "P04-01" && ws.Cells[b1, g].Text == "P04-02" &&
                  ws.Cells[c1, g].Text == "P04-03",
                  "each apartment's own number in G, at the top of its merge: " +
                  ws.Cells[a1, g].Text + ", " + ws.Cells[b1, g].Text + ", " + ws.Cells[c1, g].Text);

            Check(ws.Cells[a1, g].Value is string && ws.Cells[a1, g].Style.Numberformat.Format == "@",
                  "written as text, like every room number");

            Check(ws.Cells[a2, g].Value == null && ws.Cells[lobby, g].Value == null,
                  "nothing in G below the top of an apartment, or on an ordinary room");

            p.Workbook.Calculate();

            int errors = 0;

            for (int r = layout.FirstDataRow; r <= c1 + 2; r++)
                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                    if (ws.Cells[r, c].Value is ExcelErrorValue e && (e.ToString() == "#REF!" || e.ToString() == "#NAME?"))
                        errors++;

            Check(errors == 0, "no #REF! or #NAME? anywhere in the block (" + errors + ")");
        }

        // ---- and read back through the REAL importer
        VENTSHEET.sheetpath = path;
        VENTSHEET.sheetname = "Podium";

        Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData> read = new VENTSHEET().excelsheet();

        bool Has(string name, string number) =>
            read.Keys.Any(k => k.SpaceName == name && k.RoomNumber == number && k.Level == "PODIUM LEVEL 2");

        Check(read.Count == 8, "the importer reads every room, apartment rooms included: " + read.Count + " of 8");

        Check(Has("Bed room", "P04-01-01") && Has("Toilet", "P04-01-02") &&
              Has("Bed room", "P04-02-01") && Has("Living", "P04-02-02") && Has("Toilet", "P04-02-03"),
              "and keys each apartment room by its OWN name - what the room in the model is called");

        Check(Has("Studio room", "P04-03-01") && Has("LOBBY", "001") && Has("CORRIDOR", "002"),
              "the one-room apartment and the ordinary rooms read as before");

        Check(!read.Keys.Any(k => k.SpaceName.StartsWith("DRIVER/STAFF")),
              "no row is keyed by the apartment's name");
    }

    // ==================================================
    // Arranged by hand (VENTARRANGE), as REVITEXPORT.CollectRooms applies it
    // ==================================================

    // User (2026-10-10): take an IND room out of its apartment so it is a
    // row of its own, and drag the rooms into the order the sheet should
    // have - "so it will come with same arrangement in the excel sheet".
    static void ArrangedTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== arranged by hand: the order chosen, an interior room taken out");

        VENTTEMPLATE.Room Keyed(VENTTEMPLATE.Room room) { room.Key = room.Number; return room; }

        var found = new List<VENTTEMPLATE.Room>
        {
            Keyed(In("9:2", "DRIVER/STAFF APT LARGE", "P04-02", "Toilet", "P04-02-03", 3.88)),
            Keyed(R("LOBBY", "001", 40, 3.5)),
            Keyed(In("9:1", "DRIVER/STAFF APT", "P04-01", "Toilet", "P04-01-02", 3.5)),
            Keyed(In("9:2", "DRIVER/STAFF APT LARGE", "P04-02", "Bed room", "P04-02-01", 7.8)),
            Keyed(In("9:3", "STUDIO", "P04-03", "Studio room", "P04-03-01", 28)),
            Keyed(In("9:1", "DRIVER/STAFF APT", "P04-01", "Bed room", "P04-01-01", 19)),
            Keyed(R("CORRIDOR", "002", 70, 3.7)),
            Keyed(In("9:2", "DRIVER/STAFF APT LARGE", "P04-02", "Living", "P04-02-02", 17.5))
        };

        const string Level = "PODIUM LEVEL 2";

        var arrangement = new VENTARRANGE.Arrangement();

        // The toilet of the small apartment taken out; the large apartment
        // first, its rooms reordered; the lobby last
        arrangement.Ungrouped.Add("P04-01-02");
        arrangement.Orders[VENTARRANGE.LevelContainer(Level)] =
            new List<string> { "9:2", "002", "9:3", "9:1", "P04-01-02", "001" };
        arrangement.Orders[VENTARRANGE.ApartmentContainer("9:2")] =
            new List<string> { "P04-02-02", "P04-02-03", "P04-02-01" };

        // Exactly what CollectRooms does: ungroup, the default order, then
        // the arrangement
        foreach (VENTTEMPLATE.Room room in found.Where(r => arrangement.Ungrouped.Contains(r.Key)))
        {
            room.ApartmentKey = null;
            room.Apartment = null;
            room.ApartmentNumber = null;
        }

        List<VENTTEMPLATE.Room> ordered = VENTARRANGE.Order(
            VENTTEMPLATE.Ordered(found), Level, arrangement, r => r.Key, r => r.ApartmentKey);

        string expected = "P04-02-02,P04-02-03,P04-02-01,002,P04-03-01,P04-01-01,P04-01-02,001";

        Check(string.Join(",", ordered.Select(r => r.Number)) == expected,
              "the rooms in the order chosen, each apartment still together: " + string.Join(",", ordered.Select(r => r.Number)));

        string path = Path.Combine(Out, "arranged.xlsx");

        VENTOFFICE.Result result = VENTOFFICE.NewWorkbook(
            template, Job("Arranged", new List<VENTTEMPLATE.Level> { L(Level, 0, ordered.ToArray()) }, null), path);

        Check(result.Apartments == 3, "still three apartment groups written (" + result.Apartments + ")");

        using (var p = new ExcelPackage(new FileInfo(path)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Arranged"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            int f = layout.SpaceNameCol, h = layout.SpaceEndCol;

            var numbers = Enumerable.Range(layout.FirstDataRow, 40)
                .Select(r => ws.Cells[r, layout.RoomNumberCol].Text)
                .Where(t => t.Length > 0)
                .ToList();

            Check(string.Join(",", numbers) == expected,
                  "the SHEET has them in that order, row by row: " + string.Join(",", numbers));

            int Row(string number) =>
                Enumerable.Range(layout.FirstDataRow, 40).First(r => ws.Cells[r, layout.RoomNumberCol].Text == number);

            int first = Row("P04-02-02"), last = Row("P04-02-01");

            Check(ws.MergedCells[first, f] == ExcelCellAddress.GetColumnLetter(f) + first + ":" + ExcelCellAddress.GetColumnLetter(f) + last &&
                  ws.Cells[first, f].Text == "DRIVER/STAFF APT LARGE" && ws.Cells[first, h].Text == "Living",
                  "the large apartment heads its rooms, merged down over all three in their new order - " + ws.MergedCells[first, f]);

            int toilet = Row("P04-01-02");

            Check(ws.Cells[toilet, f].Text == "Toilet" && ws.MergedCells[toilet, f] != null &&
                  new ExcelAddress(ws.MergedCells[toilet, f]).End.Column == h &&
                  new ExcelAddress(ws.MergedCells[toilet, f]).Start.Row == toilet,
                  "the toilet taken out is a row of its own: its OWN name across F:H, no apartment over it");

            int bed = Row("P04-01-01");

            Check(ws.Cells[bed, f].Text == "DRIVER/STAFF APT" && ws.Cells[bed, h].Text == "Bed room" &&
                  ws.MergedCells[bed, f] == null,
                  "its apartment keeps the room still inside it - one room now, nothing to merge down");
        }

        // ---- and read back through the REAL importer: every room by its own name
        VENTSHEET.sheetpath = path;
        VENTSHEET.sheetname = "Arranged";

        Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData> read = new VENTSHEET().excelsheet();

        Check(read.Count == 8 &&
              read.Keys.Any(k => k.SpaceName == "Toilet" && k.RoomNumber == "P04-01-02" && k.Level == Level) &&
              read.Keys.Any(k => k.SpaceName == "Living" && k.RoomNumber == "P04-02-02") &&
              !read.Keys.Any(k => k.SpaceName.StartsWith("DRIVER/STAFF")),
              "the importer reads all 8 back, the taken-out toilet by its own name - so the airflow write still finds it");
    }

    // ==================================================
    // Fixtures into "No. of Units"
    // ==================================================

    // Water closets and urinals counted from the model land in Table
    // 6-4's "No. of Units", and the office's own formulas multiply them:
    // six WCs in a public toilet at light use are 6 x 25 = 150 L/s.
    static void FixturesTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== fixtures into No. of Units");

        var gents = R("GENTS TOILET", "G-10", 22, 3);
        gents.Fixtures = 6;

        var ladies = R("LADIES TOILET", "G-11", 18, 3);
        ladies.Fixtures = 4;

        var lobby = R("LOBBY", "G-12", 40, 3);

        var levels = new List<VENTTEMPLATE.Level> { L("GR_GROUND FLOOR", 0, gents, ladies, lobby) };

        string path = Path.Combine(Out, "fixtures.xlsx");

        VENTOFFICE.Result result = VENTOFFICE.NewWorkbook(template, Job("Toilets", levels, null), path);

        using (var p = new ExcelPackage(new FileInfo(path)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Toilets"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            Check(layout.UnitsCol > 0 && layout.UnitsCol == result.UnitsCol,
                  "the office sheet's \"No. of Units\" column is found, and the result says where (" +
                  ExcelCellAddress.GetColumnLetter(layout.UnitsCol) + ")");

            string header = string.Join(" ", Enumerable.Range(1, layout.HeaderEndRow)
                .Select(r => ws.Cells[r, layout.UnitsCol].Text).Where(t => t.Length > 0));

            Check(header.IndexOf("Units", StringComparison.OrdinalIgnoreCase) >= 0 &&
                  header.IndexOf("Keys", StringComparison.OrdinalIgnoreCase) < 0 &&
                  header.IndexOf("Occupants", StringComparison.OrdinalIgnoreCase) < 0,
                  "and it is No. of Units, not No. of Keys or Occupants: \"" + header + "\"");

            Check(layout.UnitsCol > layout.RoomNumberCol,
                  "it sits right of the inserted Room Number column, so it moved with it");

            int Row(string name) =>
                Enumerable.Range(layout.FirstDataRow, 60).First(r => ws.Cells[r, layout.SpaceNameCol].Text == name);

            int g = Row("GENTS TOILET"), l = Row("LADIES TOILET"), b = Row("LOBBY");

            Check(Convert.ToDouble(ws.Cells[g, layout.UnitsCol].Value) == 6 &&
                  Convert.ToDouble(ws.Cells[l, layout.UnitsCol].Value) == 4,
                  "each toilet gets its own count: 6 and 4");

            Check(ws.Cells[b, layout.UnitsCol].Value == null,
                  "a room with no fixtures is left blank, as typed values are");

            // Category two left of the units, use one left - the office
            // layout, shifted by the Room Number column like everything else
            ws.Cells[g, layout.UnitsCol - 2].Value = "Toilets - public";
            ws.Cells[g, layout.UnitsCol - 1].Value = "Light Use / Cont. Oper.";

            p.Workbook.Calculate();

            object rate = ws.Cells[g, layout.UnitsCol + 1].Value;
            object total64 = ws.Cells[g, layout.UnitsCol + 3].Value;
            object exhaust = ws.Cells[g, layout.ExhaustAirCol].Value;

            Check(rate is double r1 && r1 == 25,
                  "Toilets - public at light use reads 25 L/s per unit off Table 6-4: " + rate);

            Check(total64 is double t1 && t1 == 150,
                  "and the office formula makes it 6 x 25 = 150 L/s: " + total64);

            Check(exhaust is double e1 && e1 == 150,
                  "which is the Total Exhaust AUTOVENT reads back: " + exhaust);
        }

    }

    // ==================================================
    // A brand new workbook
    // ==================================================

    static void NewWorkbookTest(byte[] template, string pattern, List<VENTTEMPLATE.Level> levels, string tag)
    {
        Console.WriteLine();
        Console.WriteLine("== new workbook from " + pattern + ", " + tag);

        string path = Path.Combine(Out, "new_" + pattern.Replace(" ", "") + "_" + tag + ".xlsx");

        VENTOFFICE.Result result = VENTOFFICE.NewWorkbook(
            template, Job("Block C", levels, pattern), path);

        Console.WriteLine("  rows " + result.RowsBefore + " -> " + result.RowsAfter + ", room number col " + result.RoomNumberCol);

        using (var p = new ExcelPackage(new FileInfo(path)))
        {
            ExcelWorkbook wb = p.Workbook;

            Check(string.Join("|", wb.Worksheets.Select(w => w.Name)) ==
                  "Block C|ASHRAE 62.1 - Table 6-1|ASHRAE 62.1 - Table 6-4|ASHRAE Reference",
                  "only the new sheet and the three ASHRAE sheets, new one first: " +
                  string.Join(" | ", wb.Worksheets.Select(w => w.Name)));

            Check(wb.Worksheets["ASHRAE Reference"].Drawings.Count == 6,
                  "ASHRAE Reference still has its 6 pictures");

            Check(wb.Names.All(n => !Dead(n)) && wb.Worksheets.All(w => w.Names.All(n => !Dead(n))),
                  "no broken or external names left (" + wb.Names.Count + " workbook names)");

            Check(wb.ExternalLinks.Count == 0, "no links to other workbooks");

            Sheet(p, wb.Worksheets["Block C"], template, pattern, levels, result, "NEW HOSPITAL", "7000123");
        }

        RoundTrip(path, "Block C", levels);
    }

    static bool Dead(ExcelNamedRange n)
    {
        string text = (n.GetFormula() ?? "") + " " + (n.Address ?? "");
        return text.Contains("#REF!") || text.Contains("[");
    }

    // ==================================================
    // Everything about the written worksheet
    // ==================================================

    static void Sheet(
        ExcelPackage p, ExcelWorksheet ws, byte[] template, string pattern,
        List<VENTTEMPLATE.Level> levels, VENTOFFICE.Result result,
        string project, string number)
    {
        VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, true);

        Check(layout.RoomNumberCol == result.RoomNumberCol && layout.RoomNumberCol > 0,
              "the importer finds the Room Number column at " + Col(layout.RoomNumberCol));

        Check(layout.LevelsByColor, "the importer sees blue level rows");

        int nameCol = layout.SpaceNameCol;
        int first = layout.FirstDataRow;
        int numberCol = layout.RoomNumberCol;
        int areaCol = layout.AreaCol;
        int heightCol = layout.HeightCol;

        Check(Col(nameCol) == "F" && Col(numberCol) == "I" && Col(areaCol) == "K" && Col(heightCol) == "L",
              "columns: name " + Col(nameCol) + ", number " + Col(numberCol) + ", area " + Col(areaCol) + ", height " + Col(heightCol));

        Check(ws.Cells[layout.HeaderRow, nameCol].Text == "Space" || ws.Cells[layout.HeaderRow, nameCol].Text.Length > 0,
              "the space heading says '" + ws.Cells[layout.HeaderRow, nameCol].Text + "'");

        // ---- the planned rows, walked ------------------------------
        int row = first;
        var subRows = new List<int>();
        int firstRoom = 0, lastRoom = 0;
        bool levelsOk = true, roomsOk = true, constantsOk = true, subsOk = true;
        string problem = null;

        int lastCol = ws.Dimension.End.Column;
        var allowed = new HashSet<int> { nameCol, numberCol, areaCol, heightCol };

        foreach (VENTTEMPLATE.Level level in levels)
        {
            if (ws.Cells[row, nameCol].Text != level.LevelName || !VENTSHEET.IsLevelFill(ws.Cells[row, nameCol]))
            {
                levelsOk = false;
                problem = problem ?? "level row " + row + " reads '" + ws.Cells[row, nameCol].Text + "'";
            }

            row++;

            if (ws.Cells[row, nameCol].Text.Length == 0)
                row++;   // the spacer

            int blockFirst = row;

            foreach (VENTTEMPLATE.Room room in level.Rooms)
            {
                ExcelRange name = ws.Cells[row, nameCol];
                ExcelRange num = ws.Cells[row, numberCol];

                bool ok = name.Text == room.Name && string.IsNullOrEmpty(name.Formula) &&
                          num.Value is string && (string)num.Value == room.Number &&
                          Math.Abs(Convert.ToDouble(ws.Cells[row, areaCol].Value) - Math.Round(room.AreaM2, 2)) < 1e-9 &&
                          Math.Abs(Convert.ToDouble(ws.Cells[row, heightCol].Value) - Math.Round(room.HeightM, 2)) < 1e-9;

                if (!ok)
                {
                    roomsOk = false;
                    problem = problem ?? "room row " + row + ": '" + name.Text + "' '" + num.Text + "'";
                }

                for (int c = 1; c <= lastCol; c++)
                {
                    ExcelRange cell = ws.Cells[row, c];

                    if (!allowed.Contains(c) && string.IsNullOrEmpty(cell.Formula) && cell.Value != null && cell.Text.Length > 0)
                    {
                        constantsOk = false;
                        problem = problem ?? "a typed value survived at " + cell.Address + ": '" + cell.Text + "'";
                    }
                }

                if (firstRoom == 0) firstRoom = row;
                lastRoom = row;

                row++;
            }

            int blockLast = row - 1;

            if (ws.Cells[row, nameCol].Text != "Sub Total")
            {
                subsOk = false;
                problem = problem ?? "no Sub Total at " + row;
            }

            string area = ws.Cells[row, areaCol].Formula;
            string want = "SUM(" + Col(areaCol) + blockFirst + ":" + Col(areaCol) + blockLast + ")";

            if (area != want)
            {
                subsOk = false;
                problem = problem ?? "Sub Total at " + row + " reads " + area + ", wanted " + want;
            }

            subRows.Add(row);
            row++;
        }

        int total = row;

        Check(levelsOk, "every level row is blue and named, in order");
        Check(roomsOk, "every room has its name, number (as text), area and height");
        Check(constantsOk, "no typed value is left on a room row but those four");
        Check(subsOk, "every Sub Total sums exactly its own rooms");

        if (problem != null)
            Console.WriteLine("        first problem: " + problem);

        string totalText = ws.Cells[total, 1].Text + ws.Cells[total, nameCol].Text;

        Check(totalText.Trim().ToLower().Contains("total"), "the Total row follows the last Sub Total (row " + total + ")");

        string wantTotal = string.Join("+", subRows.Select(r => Col(areaCol) + r));

        Check(ws.Cells[total, areaCol].Formula == wantTotal,
              "the Total adds every Sub Total: " + Short(ws.Cells[total, areaCol].Formula));

        // ---- formulas carried onto every room ----------------------
        int formulasFirst = Enumerable.Range(1, lastCol).Count(c => !string.IsNullOrEmpty(ws.Cells[firstRoom, c].Formula));
        bool same = true;

        for (int r = firstRoom; r <= lastRoom; r++)
        {
            if (ws.Cells[r, nameCol].Text == "Sub Total" || VENTSHEET.IsLevelFill(ws.Cells[r, nameCol]) || ws.Cells[r, nameCol].Text.Length == 0)
                continue;

            int n = Enumerable.Range(1, lastCol).Count(c => !string.IsNullOrEmpty(ws.Cells[r, c].Formula));

            if (n != formulasFirst)
                same = false;
        }

        Check(formulasFirst >= 12 && same, "every room row carries the same " + formulasFirst + " formulas");

        // ---- the equipment summary --------------------------------
        bool sumifsOk = true;
        int sumifs = 0;
        string bad = null;

        foreach (var cell in ws.Cells[total + 1, 1, ws.Dimension.End.Row, lastCol])
        {
            string f = cell.Formula;

            if (string.IsNullOrEmpty(f) || !f.Contains("SUMIF"))
                continue;

            foreach (Match m in Regex.Matches(f, @"\$?[A-Z]{1,3}\$?(\d+):\$?[A-Z]{1,3}\$?(\d+)"))
            {
                int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);

                if (a < total && (a != firstRoom || b != lastRoom))
                {
                    sumifsOk = false;
                    bad = bad ?? cell.Address + " " + f;
                }
            }

            sumifs++;
        }

        Check(sumifs > 0 && sumifsOk, sumifs + " SUMIF(s) under the Total cover exactly rows " + firstRoom + ":" + lastRoom +
              (bad == null ? "" : "  - " + bad));

        // ---- absolute references into the lists, moved with them ----
        AbsoluteCheck(ws, template, pattern, firstRoom, total);

        // ---- the title block --------------------------------------
        // One column right of the office's, with the room number in
        Check(ws.Cells["R1"].Text == project, "project name in the title: '" + ws.Cells["R1"].Text + "'");
        Check(ws.Cells["R2"].Text == ws.Name, "part of structure is the sheet name: '" + ws.Cells["R2"].Text + "'");
        Check(ws.Cells["AO2"].Text == number, "job ref is the project number: '" + ws.Cells["AO2"].Text + "'");

        Check(new[] { "O5", "R5", "Y5", "AG5", "AO5" }.All(a => ws.Cells[a].Value == null),
              "who calculated, who checked and the dates are blank");

        Check(ws.Cells["O1"].Text.StartsWith("Project") && ws.Cells["O4"].Text.StartsWith("Calc") && ws.Cells["AO4"].Text.StartsWith("Date"),
              "the title labels are still there");

        // ---- the summary's tags are gone, its formulas are not -----
        int tagRow = 0, tagCol = 0;

        foreach (var cell in ws.Cells[total + 1, 1, ws.Dimension.End.Row, lastCol])
        {
            if (cell.Text.Trim().Equals("Equipment TAG", StringComparison.OrdinalIgnoreCase))
            {
                tagRow = cell.Start.Row;
                tagCol = cell.Start.Column;
                break;
            }
        }

        Check(tagRow > 0 && Enumerable.Range(tagRow + 1, 10).All(r => ws.Cells[r, tagCol].Value == null),
              "the API job's unit tags are cleared from the summary");

        string sumif = tagRow == 0 ? null : Enumerable.Range(tagRow + 1, 5)
            .SelectMany(r => Enumerable.Range(tagCol, 10).Select(c => ws.Cells[r, c].Formula))
            .FirstOrDefault(f => !string.IsNullOrEmpty(f) && f.StartsWith("SUMIF"));

        bool numbersGone = tagRow > 0 && Enumerable.Range(tagRow + 1, 10)
            .All(r => Enumerable.Range(1, lastCol).All(c =>
                !string.IsNullOrEmpty(ws.Cells[r, c].Formula) || !(ws.Cells[r, c].Value is double)));

        Check(numbersGone, "no airflow typed over a SUMIF is left in the summary");

        Check(sumif != null && sumif.StartsWith("SUMIF"), "the summary keeps its formulas: " + Short(sumif));

        // ---- drop-downs -------------------------------------------
        Validations(ws, layout, firstRoom, lastRoom, tagRow, tagCol);

        // ---- nothing broken ---------------------------------------
        int refs = 0;
        string refAt = null;

        foreach (var cell in ws.Cells[ws.Dimension.Address])
        {
            if (!string.IsNullOrEmpty(cell.Formula) && cell.Formula.Contains("#REF!"))
            {
                refs++;
                refAt = refAt ?? cell.Address + " " + cell.Formula;
            }
        }

        Check(refs == 0, "no #REF! in any formula" + (refAt == null ? "" : " - " + refs + ", e.g. " + refAt));

        Check(ws.PrinterSettings.PrintArea != null && ws.PrinterSettings.PrintArea.End.Row == total,
              "the print area ends on the Total: " + ws.PrinterSettings.PrintArea?.Address);

        Check(ws.AutoFilter == null || ws.AutoFilter.Address == null, "no AutoFilter");

        // WHERE IT OPENS. The office saved Tower 2 in Page Break Preview
        // scrolled to row 297; copied as it was, the frozen headings were
        // rows 297-305 and the sheet opened on blank cells. User-reported
        // as "the exported excel sheet is coming out empty" while every
        // check on the cells above passed.
        string topLeft = ws.View.TopLeftCell ?? "";

        Check(topLeft == "" || topLeft == "A1", "the sheet opens at the top: '" + topLeft + "'");
        Check(!ws.View.PageBreakView, "in Normal view, not Page Break Preview");

        var pane = ws.View.PaneSettings;

        Check(pane != null && pane.State == ePaneState.Frozen && (int)pane.YSplit == first - 1 &&
              (int)pane.XSplit == numberCol && pane.TopLeftCell == Col(numberCol + 1) + first,
              "rows 1-" + (first - 1) + " and columns A-" + Col(numberCol) + " frozen, scrolling from " +
              (pane == null ? "?" : pane.TopLeftCell));

        Check((p.Workbook.View.FirstSheet ?? 0) <= p.Workbook.View.ActiveTab,
              "the new sheet's tab is not scrolled out of sight (first " + p.Workbook.View.FirstSheet +
              ", active " + p.Workbook.View.ActiveTab + ")");

        // ---- and it calculates ------------------------------------
        Calculate(p, ws, layout, firstRoom, total, pattern);
    }

    // The office's own absolute references ($H$363 in the occupancy
    // formula) point into the lists under the Total. However many rows
    // the block grew or shrank by, they must still land the same
    // distance below the Total as they did in the office's sheet.
    static void AbsoluteCheck(ExcelWorksheet ws, byte[] template, string pattern, int firstRoom, int total)
    {
        using (var src = new ExcelPackage(new MemoryStream(template)))
        {
            ExcelWorksheet o = src.Workbook.Worksheets[pattern];

            int oTotal = 0;

            for (int r = 10; r <= o.Dimension.End.Row && oTotal == 0; r++)
                if (o.Cells[r, 1].Text.Trim() == "Total") oTotal = r;

            // The occupancy "NA" test is in column Q of the office sheet,
            // R once the room number column is in
            var offsets = new Dictionary<int, int>();

            for (int r = 12; r < oTotal; r++)
            {
                Match m = Regex.Match(o.Cells[r, 17].Formula ?? "", @"\$H\$(\d+)");

                if (m.Success)
                {
                    int off = int.Parse(m.Groups[1].Value) - oTotal;
                    offsets[off] = offsets.TryGetValue(off, out int n) ? n + 1 : 1;
                }
            }

            int want = offsets.OrderByDescending(kv => kv.Value).First().Key;

            Match got = Regex.Match(ws.Cells[firstRoom, 18].Formula ?? "", @"\$H\$(\d+)");

            Check(got.Success && int.Parse(got.Groups[1].Value) - total == want,
                  "the occupancy test's $H$ reference is still " + want + " rows under the Total (" +
                  (got.Success ? "$H$" + got.Groups[1].Value : "none") + ")");
        }
    }

    static void Validations(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int firstRoom, int lastRoom, int tagRow, int tagCol)
    {
        var lists = ws.DataValidations.OfType<IExcelDataValidationList>().ToList();

        IExcelDataValidationList On(string address)
        {
            var a = new ExcelAddress(address);

            return lists.FirstOrDefault(dv => dv.Address.Address.Split(',', ' ')
                .Where(s => s.Length > 0)
                .Select(s => new ExcelAddress(s))
                .Any(x => x.Start.Row <= a.Start.Row && x.End.Row >= a.Start.Row &&
                          x.Start.Column <= a.Start.Column && x.End.Column >= a.Start.Column));
        }

        // Every room in the four office drop-down columns has one
        string[] cols = { "O", "AB", "AC", "AD", "AK" };
        int missing = 0;
        string missAt = null;
        int onOthers = 0;

        for (int r = firstRoom; r <= lastRoom; r++)
        {
            bool isRoom = ws.Cells[r, layout.SpaceNameCol].Text != "Sub Total" &&
                          !VENTSHEET.IsLevelFill(ws.Cells[r, layout.SpaceNameCol]) &&
                          ws.Cells[r, layout.SpaceNameCol].Text.Length > 0;

            foreach (string c in cols)
            {
                var dv = On(c + r);

                if (isRoom && dv == null)
                {
                    missing++;
                    missAt = missAt ?? c + r;
                }

                if (!isRoom && dv != null)
                    onOthers++;
            }
        }

        Check(missing == 0, "every room has the occupancy, served-by, exhaust, use and override drop-downs" +
              (missAt == null ? "" : " - missing at " + missAt));

        Check(onOthers == 0, "no level, spacer or Sub Total row has a drop-down (" + onOthers + ")");

        var occupancy = On("O" + firstRoom);

        Check(occupancy != null && occupancy.Formula.ExcelFormula == "'ASHRAE 62.1 - Table 6-1'!$A$5:$A$103",
              "occupancy lists the first column of the table its VLOOKUP reads: " + occupancy?.Formula.ExcelFormula);

        var served = On("AB" + firstRoom);
        Match m = Regex.Match(served?.Formula.ExcelFormula ?? "", @"\$H\$(\d+)");

        Check(m.Success && int.Parse(m.Groups[1].Value) == tagRow + 1,
              "served-by lists the summary's unit tags, which start at row " + (tagRow + 1) + ": " + served?.Formula.ExcelFormula);

        var yes = On("AK" + firstRoom);

        Check(yes != null && string.Join(",", yes.Formula.Values) == "Yes,No", "override is still Yes/No");

        var exhaust = On("AC" + firstRoom);

        Check(exhaust != null && exhaust.Formula.ExcelFormula.StartsWith("'ASHRAE 62.1 - Table 6-4'!"),
              "exhaust category still reads Table 6-4: " + exhaust?.Formula.ExcelFormula);

        // Overlap: Excel repairs a file whose validations overlap
        var boxes = new List<(ExcelAddress a, int id)>();
        int id = 0;

        foreach (var dv in ws.DataValidations)
        {
            foreach (string s in dv.Address.Address.Split(',', ' ').Where(s => s.Length > 0))
                boxes.Add((new ExcelAddress(s), id));

            id++;
        }

        int overlaps = 0;

        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
                if (boxes[i].id != boxes[j].id &&
                    boxes[i].a.Start.Row <= boxes[j].a.End.Row && boxes[j].a.Start.Row <= boxes[i].a.End.Row &&
                    boxes[i].a.Start.Column <= boxes[j].a.End.Column && boxes[j].a.Start.Column <= boxes[i].a.End.Column)
                    overlaps++;

        Check(overlaps == 0, "no two drop-downs overlap (" + overlaps + ")");
    }

    // Choose an occupancy on the first room and let EPPlus calculate:
    // the lookups into the ASHRAE sheet must answer
    static void Calculate(ExcelPackage p, ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int firstRoom, int total, string pattern)
    {
        ExcelWorksheet table = p.Workbook.Worksheets["ASHRAE 62.1 - Table 6-1"];

        int officeRow = 0;

        for (int r = 5; r <= 103; r++)
            if (table.Cells[r, 1].Text.Trim() == "Office Space") officeRow = r;

        ws.Cells[firstRoom, 15].Value = "Office Space";   // O: occupancy

        p.Workbook.Calculate();

        double rp = Convert.ToDouble(ws.Cells[firstRoom, 16].Value);  // P: Rp
        double tableRp = Convert.ToDouble(table.Cells[officeRow, 3].Value);

        Check(officeRow > 0 && Math.Abs(rp - tableRp) < 1e-9,
              "Office Space looks up Rp = " + rp + " (table says " + tableRp + ")");

        object fresh = ws.Cells[firstRoom, layout.FreshAirCol].Value;

        Check(fresh is double d && d > 0, "and the fresh air column calculates: " + fresh);

        object tot = ws.Cells[total, layout.AreaCol].Value;

        Check(tot is double, "the Total calculates: " + tot);

        var errors = new Dictionary<string, int>();

        foreach (var cell in ws.Cells[1, 1, total, ws.Dimension.End.Column])
        {
            if (cell.Value is ExcelErrorValue e)
            {
                string k = e.ToString();
                errors[k] = errors.TryGetValue(k, out int n) ? n + 1 : 1;
            }
        }

        Check(!errors.ContainsKey("#REF!") && !errors.ContainsKey("#NAME?"),
              "no #REF! or #NAME? down to the Total" +
              (errors.Count == 0 ? "" : " (" + string.Join(", ", errors.Select(kv => kv.Key + " x" + kv.Value)) + ")"));

        // MLCP'S "Airflow for Balancing" IS =FRESH x 0.9, and an empty
        // fresh cell times 0.9 is #VALUE! in Excel as much as here -
        // which is one reason MLCP is no longer the default. From the
        // Towers, a #VALUE! anywhere is a fault.
        int balancing = Enumerable.Range(1, ws.Dimension.End.Column)
            .FirstOrDefault(c => (ws.Cells[layout.HeaderRow, c].Text ?? "").Trim().ToLower() == "airflow for balancing");

        var valueCols = new HashSet<int>();

        foreach (var cell in ws.Cells[1, 1, total, ws.Dimension.End.Column])
            if (cell.Value is ExcelErrorValue e && e.ToString() == "#VALUE!")
                valueCols.Add(cell.Start.Column);

        bool mlcp = pattern == "MLCP";

        Check(mlcp ? valueCols.All(c => c == balancing) : valueCols.Count == 0,
              (mlcp ? "#VALUE! only in MLCP's Airflow for Balancing (" + Col(balancing) + ")"
                    : "no #VALUE! anywhere") +
              (valueCols.Count == 0 ? "" : ": " + string.Join(",", valueCols.Select(Col))));

        if (!mlcp)
        {
            // The exhaust AUTOVENT reads back is CALCULATED on the Towers
            Check(!string.IsNullOrEmpty(ws.Cells[firstRoom, layout.ExhaustAirCol].Formula),
                  "the exhaust column AUTOVENT reads is a formula: " + Short(ws.Cells[firstRoom, layout.ExhaustAirCol].Formula));

            Check(ws.Cells[firstRoom, 1].Formula != null && ws.Cells[firstRoom, 1].Text == "1",
                  "S.No runs from 1: " + ws.Cells[firstRoom, 1].Text + " (" + ws.Cells[firstRoom, 1].Formula + ")");
        }
    }

    // ==================================================
    // Read it back with the importer AUTOVENT actually uses
    // ==================================================

    static void RoundTrip(string path, string sheet, List<VENTTEMPLATE.Level> levels)
    {
        VENTSHEET.sheetpath = path;
        VENTSHEET.sheetname = sheet;

        Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData> read = new VENTSHEET().excelsheet();

        int want = levels.Sum(l => l.Rooms.Count);

        bool all = levels.All(l => l.Rooms.All(r => read.ContainsKey(new VENTSHEET.SpaceKey
        {
            SpaceName = r.Name, RoomNumber = r.Number, Level = l.LevelName
        })));

        Check(read.Count == want && all,
              "AUTOVENT imports it back: " + read.Count + " of " + want + " rooms, each under its own level");

        Check(VENTSHEET.GetSheetLevels().SetEquals(levels.Select(l => l.LevelName)),
              "and reads exactly the levels written");
    }

    // ==================================================
    // Adding to a workbook
    // ==================================================

    static void AddToOfficeTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== add to the office workbook itself");

        string path = Path.Combine(Out, "add_office.xlsx");
        List<VENTTEMPLATE.Level> levels = Small();

        VENTOFFICE.Result result = VENTOFFICE.AddTo(template, Source, Job("Tower 3", levels, null), path);

        Check(result.Brought.Count == 0, "the ASHRAE sheets it already has are used, not duplicated");

        using (var before = new ExcelPackage(new FileInfo(Source)))
        using (var p = new ExcelPackage(new FileInfo(path)))
        {
            var names = p.Workbook.Worksheets.Select(w => w.Name).ToList();

            Check(names.Count == before.Workbook.Worksheets.Count + 1 && names.Last() == "Tower 3",
                  "one worksheet added, at the end: " + string.Join(" | ", names));

            // Every existing sheet is untouched, formula for formula
            bool same = true;
            string where = null;

            foreach (var old in before.Workbook.Worksheets)
            {
                var now = p.Workbook.Worksheets[old.Name];

                if (old.Dimension == null) continue;

                foreach (var cell in old.Cells[old.Dimension.Address])
                {
                    var c2 = now.Cells[cell.Address];

                    // EPPlus writes "=+IF(" back as "=IF(" - the same formula
                    if ((cell.Formula ?? "").TrimStart('+') != (c2.Formula ?? "").TrimStart('+') ||
                        (string.IsNullOrEmpty(cell.Formula) && !Equals(cell.Value, c2.Value)))
                    {
                        same = false;
                        where = where ?? old.Name + "!" + cell.Address;
                    }
                }
            }

            Check(same, "every worksheet already there is exactly as it was" + (where == null ? "" : " - differs at " + where));

            Check(p.Workbook.Names.Count == before.Workbook.Names.Count,
                  "the workbook's own names are left alone (" + p.Workbook.Names.Count + ")");

            Check(result.Pattern == VENTOFFICE.Patterns[0], "copied from " + result.Pattern);

            Sheet(p, p.Workbook.Worksheets["Tower 3"], template, result.Pattern, levels, result, "NEW HOSPITAL", "7000123");
        }

        RoundTrip(path, "Tower 3", levels);
    }

    static void AddToBareTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== add to a workbook without the ASHRAE sheets");

        string bare = Path.Combine(Out, "bare.xlsx");

        using (var p = new ExcelPackage())
        {
            var ws = p.Workbook.Worksheets.Add("Mine");
            ws.Cells["A1"].Value = "somebody's own data";
            ws.Cells["B2"].Formula = "1+1";
            p.SaveAs(new FileInfo(bare));
        }

        string path = Path.Combine(Out, "add_bare.xlsx");
        List<VENTTEMPLATE.Level> levels = Big();

        VENTOFFICE.Result result = VENTOFFICE.AddTo(template, bare, Job("Ventilation", levels, null), path);

        Check(result.Brought.SequenceEqual(VENTOFFICE.Kept), "the three ASHRAE sheets are brought in: " + string.Join(", ", result.Brought));

        using (var p = new ExcelPackage(new FileInfo(path)))
        {
            Check(p.Workbook.Worksheets["Mine"].Cells["A1"].Text == "somebody's own data" &&
                  p.Workbook.Worksheets["Mine"].Cells["B2"].Formula == "1+1",
                  "the existing worksheet is untouched");

            Check(p.Workbook.Worksheets["ASHRAE Reference"].Drawings.Count == 6,
                  "the copied ASHRAE Reference has its 6 pictures");

            Check(p.Workbook.Worksheets["ASHRAE Reference"].Hidden == eWorkSheetHidden.Hidden,
                  "and is hidden, as the office has it");

            Check(p.Workbook.ExternalLinks.Count == 0 && p.Workbook.Names.All(n => !Dead(n)),
                  "nothing broken came across with the copy");

            Sheet(p, p.Workbook.Worksheets["Ventilation"], template, result.Pattern, levels, result, "NEW HOSPITAL", "7000123");
        }

        RoundTrip(path, "Ventilation", levels);
    }

    // "Fill the worksheet I imported" rewrites the user's own sheet in
    // place, so it keeps its view - but not its scroll position
    static void HomeTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== back to the top, for the fill-the-imported-sheet export");

        using (var p = new ExcelPackage(new MemoryStream(template)))
        {
            foreach (string name in new[] { "Tower 2", "Tower-1", "MLCP" })
            {
                ExcelWorksheet ws = p.Workbook.Worksheets[name];

                string before = ws.View.TopLeftCell;
                var was = ws.View.PaneSettings;
                int xs = was == null ? 0 : Split(was.XSplit);
                int ys = was == null ? 0 : Split(was.YSplit);
                var state = was == null ? (ePaneState?)null : was.State;
                bool pageBreak = ws.View.PageBreakView;

                VENTROWS.Home(ws);

                var now = ws.View.PaneSettings;

                // Scrolling must start straight under and right of the
                // freeze - not at row 297
                string wantScroll = Col(xs + 1) + (ys + 1);

                Check(((ws.View.TopLeftCell ?? "") == "" || ws.View.TopLeftCell == "A1") &&
                      (now == null ? 0 : Split(now.XSplit)) == xs &&
                      (now == null ? 0 : Split(now.YSplit)) == ys &&
                      now != null && now.TopLeftCell == wantScroll &&
                      ws.View.PageBreakView == pageBreak,
                      name + ": was saved at '" + before + "' (" + state + ", " + xs + " col(s), " + ys +
                      " row(s) frozen, scrolling from " + (was == null ? "?" : "") + "); now opens at A1, same freeze, scrolling from " +
                      (now == null ? "?" : now.TopLeftCell) + ", same view");
            }
        }
    }

    static void RefusalTests(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== refusals");

        string path = Path.Combine(Out, "refused.xlsx");

        if (File.Exists(path)) File.Delete(path);

        try
        {
            VENTOFFICE.AddTo(template, Source, Job("MLCP", Small(), null), path);
            Check(false, "adding a sheet that already exists is refused");
        }
        catch (Exception e)
        {
            Check(e.Message.Contains("already has") && !File.Exists(path), "adding a sheet that already exists is refused, nothing written");
        }

        try
        {
            VENTOFFICE.NewWorkbook(template, Job("ASHRAE Reference", Small(), null), path);
            Check(false, "a new sheet named like an ASHRAE sheet is refused");
        }
        catch (Exception e)
        {
            Check(e.Message.Contains("ASHRAE") && !File.Exists(path), "a new sheet named like an ASHRAE sheet is refused");
        }

        try
        {
            VENTOFFICE.NewWorkbook(template, Job("X", new List<VENTTEMPLATE.Level>(), null), path);
            Check(false, "no rooms at all is refused");
        }
        catch (Exception e)
        {
            Check(e.Message.Contains("no rooms"), "no rooms at all is refused");
        }
    }

    // ==================================================
    // The reference rewriter on its own
    // ==================================================

    static void RefsTests()
    {
        Console.WriteLine("== references");

        Check(VENTOFFICE.Refs.ShiftR1C1("IF(RC[-1]=R363C8,\"NA\",VLOOKUP(RC[-1],'ASHRAE 62.1 - Table 6-1'!R5C1:R103C10,7,FALSE))", 210, 50) ==
              "IF(RC[-1]=R413C8,\"NA\",VLOOKUP(RC[-1],'ASHRAE 62.1 - Table 6-1'!R5C1:R103C10,7,FALSE))",
              "R1C1: an absolute row below the block moves; the other sheet's rows do not");

        Check(VENTOFFICE.Refs.ShiftR1C1("R12C3+R[5]C", 210, 50) == "R12C3+R[5]C",
              "R1C1: rows above the block and relative rows stay");

        Check(VENTOFFICE.Refs.ShiftR1C1("ROUNDUP(R300C1,0)&\"R300C1\"", 210, -7) == "ROUNDUP(R293C1,0)&\"R300C1\"",
              "R1C1: ROUNDUP is not a reference and text in quotes is not touched");

        Check(!VENTOFFICE.Refs.HasReference("3.9-0.3") && VENTOFFICE.Refs.HasReference("RC[-2]*0.8") &&
              VENTOFFICE.Refs.HasReference("VLOOKUP(\"x\",'T'!R1C1:R2C2,2)"),
              "a formula with no cell in it is a typed value");

        Check(VENTOFFICE.Refs.MapAreas("SUMIF($AA$12:$AA$209,H215,$Z$12:$Z$209)", "S", (a, b) => a == 12 ? new[] { 20, 30 } : null) ==
              "SUMIF($AA$20:$AA$30,H215,$Z$20:$Z$30)",
              "areas are re-pointed and single cells are not");

        Check(VENTOFFICE.Refs.MapAreas("SUM('Other'!A12:A20)+LOG10(A12)", "S", (a, b) => new[] { 1, 2 }) ==
              "SUM('Other'!A12:A20)+LOG10(A12)",
              "another sheet's area, and LOG10, are left alone");

        Check(VENTOFFICE.Refs.MapAreas("SUM('S'!A12:A20)", "S", (a, b) => new[] { 1, 2 }) == "SUM('S'!A1:A2)",
              "this sheet named explicitly is still this sheet");

        Check(VENTOFFICE.Refs.ShiftA1("$H$215:$H$230", "S", 210, -150) == "$H$65:$H$80",
              "a drop-down's list moves with the rows it names");

        Check(VENTOFFICE.Refs.Rows("AL211/J211", "S").All(r => r == 211) &&
              VENTOFFICE.Refs.Rows("J61+J73", "S").SequenceEqual(new[] { 61, 73 }),
              "the rows a formula reads");

        Check(VENTOFFICE.Refs.Relative("SUM(J12:J18)", "S", 19, 12, 18) ==
              VENTOFFICE.Refs.Relative("SUM(J22:J29)", "S", 30, 22, 29) &&
              VENTOFFICE.Refs.Relative("AL19/J19", "S", 19, 12, 18) == "AL{self}/J{self}",
              "two Sub Totals that mean the same thing read the same relative to their blocks");

        Check(VENTOFFICE.Refs.Relative("AL16+AL17+AL18", "S", 19, 12, 18) !=
              VENTOFFICE.Refs.Relative("AL24+AL25+AL26", "S", 30, 22, 29),
              "scratch sums over different rooms do not");

        Check(VENTOFFICE.Refs.Column("RC[-1]", 15) == 14 && VENTOFFICE.Refs.Column("R12C3", 15) == 3 && VENTOFFICE.Refs.Column("RC", 15) == 15,
              "the column an R1C1 key points at");
    }

    static int Split(double v) => double.IsNaN(v) || v < 0 ? 0 : (int)v;

    static string Col(int c) => c <= 0 ? "-" : ExcelCellAddress.GetColumnLetter(c);

    static string Short(string s) => s == null ? "(none)" : s.Length > 70 ? s.Substring(0, 70) + "..." : s;
}
