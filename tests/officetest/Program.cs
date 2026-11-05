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
        MergedTest(template);
        MatchTests();
        UpdateTest(template);
        UpdateWithoutKeysTest(template);
        UpdateMergedTest(template);
        OccupancyRuleTests(template);
        OccupancyExportTest(template);
        WhatIfTests();
        OccupancyUpdateTest(template);
        KitchenUpdateTest(template);
        TwinsTest(template);
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
    // Merged rooms (VENTARRANGE.Merges -> VENTROWS.MergeAir)
    // ==================================================

    // User (2026-10-12): two or more spaces served by one FCU, fed by
    // one fresh air connection - their fresh air and extract cells merged
    // in the sheet, the total of the spaces in the one cell.
    static void MergedTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== merged rooms: one total of fresh air, or of extract - each side its own");

        const string Level = "LEVEL 04A";

        List<VENTTEMPLATE.Room> Rooms(bool merged)
        {
            var lobby = R("LOBBY", "121.001", 40, 3);
            var living = R("LIVING/DINING", "121.125", 30, 3);
            var kitchen = R("KITCHEN", "121.126", 12, 3);
            var bed = In("9:1", "1BDR-C", "121.120", "BEDROOM", "121.121", 14);
            var bath = In("9:1", "1BDR-C", "121.120", "ENSUITE", "121.122", 5);

            if (merged)
            {
                // The living and the kitchen merged on both sides (the sheet
                // merged their extract cells too); the apartment's pair on
                // fresh air only - an FCU's merge (2026-10-22)
                living.Merge = kitchen.Merge = "k";
                living.ExtractMerge = kitchen.ExtractMerge = "k";
                bed.Merge = bath.Merge = "b";

                // A group of one is no merge
                lobby.Merge = "solo";
            }

            return VENTTEMPLATE.Ordered(new List<VENTTEMPLATE.Room> { kitchen, bath, lobby, bed, living });
        }

        string plainPath = Path.Combine(Out, "merged-plain.xlsx");
        string mergedPath = Path.Combine(Out, "merged.xlsx");

        VENTOFFICE.NewWorkbook(template, Job("Merged", new List<VENTTEMPLATE.Level> { L(Level, 0, Rooms(false).ToArray()) }, null), plainPath);

        VENTOFFICE.Result result = VENTOFFICE.NewWorkbook(
            template, Job("Merged", new List<VENTTEMPLATE.Level> { L(Level, 0, Rooms(true).ToArray()) }, null), mergedPath);

        Check(result.Merged == 3, "three merged groups written - two of fresh air, one of extract (" + result.Merged + ")");

        // Both calculated with every room an office, as Calculate does
        // with one - and saved, so the importer reads what Excel would
        var fresh = new Dictionary<string, double>();
        var extract = new Dictionary<string, double>();
        double plainTotal = 0;

        foreach (string path in new[] { plainPath, mergedPath })
        {
            using (var p = new ExcelPackage(new FileInfo(path)))
            {
                ExcelWorksheet ws = p.Workbook.Worksheets["Merged"];
                VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

                int Row(string number) =>
                    Enumerable.Range(layout.FirstDataRow, 40).First(r => ws.Cells[r, layout.RoomNumberCol].Text == number);

                int total = Enumerable.Range(layout.FirstDataRow, 60)
                    .First(r => VENTSHEET.IsTotalRow(ws.Cells[r, layout.SpaceNameCol].Text));

                // Straight from the export, nothing chosen yet: every
                // room's air is "" - and so must a merged cell's be, or
                // "" + "" is #VALUE! all the way down to the Total
                if (path == mergedPath)
                {
                    p.Workbook.Calculate();

                    int top = Row("121.125");
                    var notYet = new List<string>();

                    foreach (int col in new[] { layout.FreshAirCol, layout.ExhaustAirCol })
                        foreach (int r in new[] { top, total })
                            if (ws.Cells[r, col].Value is ExcelErrorValue)
                                notYet.Add(ws.Cells[r, col].Address + " " + ws.Cells[r, col].Value);

                    Check(notYet.Count == 0 && (ws.Cells[top, layout.FreshAirCol].Value ?? "").ToString() == "",
                          "a merged cell with nothing chosen yet is blank, like its rooms - no #VALUE! down to the Total" +
                          (notYet.Count == 0 ? "" : ": " + string.Join(", ", notYet)));
                }

                foreach (string number in new[] { "121.001", "121.121", "121.122", "121.125", "121.126" })
                    ws.Cells[Row(number), 15].Value = "Office Space";

                p.Workbook.Calculate();

                double Num(int row, int col) => ws.Cells[row, col].Value is double d ? d : 0;

                int living = Row("121.125"), kitchen = Row("121.126");
                int bed = Row("121.121"), bath = Row("121.122"), lobby = Row("121.001");

                if (path == plainPath)
                {
                    foreach (string number in new[] { "121.121", "121.122", "121.125", "121.126" })
                    {
                        fresh[number] = Num(Row(number), layout.FreshAirCol);
                        extract[number] = Num(Row(number), layout.ExhaustAirCol);
                    }

                    plainTotal = Num(total, layout.FreshAirCol);

                    Check(ws.MergedCells[living, layout.FreshAirCol] == null,
                          "not merged, each room has its own fresh air cell");

                    continue;
                }

                string Span(int top, int bottom, int col) =>
                    ExcelCellAddress.GetColumnLetter(col) + top + ":" + ExcelCellAddress.GetColumnLetter(col) + bottom;

                Check(kitchen == living + 1 &&
                      ws.MergedCells[living, layout.FreshAirCol] == Span(living, kitchen, layout.FreshAirCol) &&
                      ws.MergedCells[living, layout.ExhaustAirCol] == Span(living, kitchen, layout.ExhaustAirCol),
                      "the living and the kitchen: fresh air and extract each merged down over both rows - " +
                      ws.MergedCells[living, layout.FreshAirCol] + ", " + ws.MergedCells[living, layout.ExhaustAirCol]);

                Check(ws.MergedCells[bed, layout.FreshAirCol] == Span(bed, bath, layout.FreshAirCol),
                      "rooms of one apartment merge the same way - " + ws.MergedCells[bed, layout.FreshAirCol]);

                Check(ws.MergedCells[bed, layout.ExhaustAirCol] == null && ws.MergedCells[bath, layout.ExhaustAirCol] == null &&
                      Math.Abs(Num(bath, layout.ExhaustAirCol) - extract["121.122"]) < 1e-9,
                      "a fresh air merge leaves the extract alone: the ensuite keeps its own extract cell, " + Num(bath, layout.ExhaustAirCol));

                Check(ws.MergedCells[living, layout.AreaCol] == null && Num(kitchen, layout.AreaCol) == 12,
                      "nothing else is merged: each room keeps its own area");

                Check(ws.MergedCells[lobby, layout.FreshAirCol] == null,
                      "a room alone in its group is not merged");

                Check(ws.Cells[kitchen, layout.FreshAirCol].Value == null &&
                      string.IsNullOrEmpty(ws.Cells[kitchen, layout.FreshAirCol].Formula),
                      "the cell under the top is emptied");

                Check(fresh["121.125"] > 0 && fresh["121.126"] > 0 &&
                      Math.Abs(Num(living, layout.FreshAirCol) - (fresh["121.125"] + fresh["121.126"])) < 1e-9,
                      "the top cell is the two rooms' fresh air added, live: " + Num(living, layout.FreshAirCol) +
                      " = " + fresh["121.125"] + " + " + fresh["121.126"] +
                      " (" + Short(ws.Cells[living, layout.FreshAirCol].Formula) + ")");

                Check(Math.Abs(Num(living, layout.ExhaustAirCol) - (extract["121.125"] + extract["121.126"])) < 1e-9 &&
                      Math.Abs(Num(bed, layout.FreshAirCol) - (fresh["121.121"] + fresh["121.122"])) < 1e-9,
                      "...and so is the extract, and the apartment's pair");

                Check(Math.Abs(Num(total, layout.FreshAirCol) - plainTotal) < 1e-9,
                      "the Total is what it was - nothing gained or lost: " + Num(total, layout.FreshAirCol));

                int errors = 0;

                foreach (var cell in ws.Cells[layout.FirstDataRow, 1, total, ws.Dimension.End.Column])
                    if (cell.Value is ExcelErrorValue e && (e.ToString() == "#REF!" || e.ToString() == "#NAME?"))
                        errors++;

                Check(errors == 0, "no #REF! or #NAME? (" + errors + ")");

                p.Save();
            }
        }

        // ---- read back: one group, its total on the first row
        VENTSHEET.sheetpath = mergedPath;
        VENTSHEET.sheetname = "Merged";

        Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData> read = new VENTSHEET().excelsheet();

        VENTSHEET.AirData Air(string number) => read.First(kv => kv.Key.RoomNumber == number).Value;

        Check(read.Count == 5, "the importer reads every room: " + read.Count + " of 5");

        Check(Air("121.125").FreshMerge != null && Air("121.125").FreshMerge == Air("121.126").FreshMerge &&
              Air("121.125").ExhaustMerge != null && Air("121.125").ExhaustMerge == Air("121.126").ExhaustMerge,
              "the merged rooms read as one group, fresh and extract - " + Air("121.125").FreshMerge);

        Check(Air("121.121").FreshMerge != null && Air("121.121").FreshMerge != Air("121.125").FreshMerge &&
              Air("121.001").FreshMerge == null,
              "the apartment's pair is a group of its own; the lobby is in none");

        Check(Air("121.121").ExhaustMerge == null && Air("121.122").ExhaustMerge == null,
              "...on fresh air only: its extract reads back room by room");

        Check(Math.Abs(Air("121.125").FreshAir + Air("121.126").FreshAir - (fresh["121.125"] + fresh["121.126"])) < 1e-9,
              "the group's air read back is the total, once: " + Air("121.125").FreshAir + " + " + Air("121.126").FreshAir);
    }

    // ==================================================
    // Updating the imported worksheet (VENTUPDATE)
    // ==================================================

    // User (2026-10-14): an export into the imported sheet keeps what was
    // typed, adds the rooms the architect added, updates the ones changed;
    // a room no longer in the model is kept, greyed, with a note.

    const int Occ = 15;   // O: occupancy, as Calculate types it

    static VENTUPDATE.Candidate C(string level, string key, string number, string name) =>
        new VENTUPDATE.Candidate { Level = level, Key = key, Number = number, Name = name };

    static VENTTEMPLATE.Room Rm(string key, string name, string number, double area, bool keep = false)
    {
        var r = R(name, number, area, 3);
        r.Key = key;
        r.Keep = keep;
        return r;
    }

    static int RowOf(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, string number) =>
        Enumerable.Range(layout.FirstDataRow, 80).FirstOrDefault(r => ws.Cells[r, layout.RoomNumberCol].Text.Trim() == number);

    static int LevelRowOf(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, string level) =>
        Enumerable.Range(layout.FirstDataRow, 80).FirstOrDefault(r => ws.Cells[r, layout.SpaceNameCol].Text.Trim() == level);

    static string NameAt(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int row) => VENTSHEET.NameOf(ws, row, layout);

    // A column a person types into: headed, no formula in the room row,
    // none the model writes
    static int NoteColumn(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int row)
    {
        var busy = new HashSet<int>
        {
            layout.RoomNumberCol, layout.AreaCol, layout.HeightCol, layout.UnitsCol,
            layout.BedroomsCol, layout.FreshAirCol, layout.ExhaustAirCol, Occ
        };

        for (int c = layout.SpaceEndCol + 1; c <= ws.Dimension.End.Column; c++)
        {
            if (busy.Contains(c) || ws.Column(c).Hidden || !string.IsNullOrEmpty(ws.Cells[row, c].Formula))
                continue;

            if (Enumerable.Range(1, layout.HeaderEndRow).Any(r => ws.Cells[r, c].Text.Trim().Length > 0))
                return c;
        }

        return 0;
    }

    // A level's five rows - level, spacer, two rooms, Sub Total - as text
    // and R1C1 formulas, so where they stand does not matter
    static string Snapshot(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, string level)
    {
        int top = LevelRowOf(ws, layout, level);
        var parts = new List<string>();

        for (int r = top; r < top + 5; r++)
        {
            for (int c = 1; c <= ws.Dimension.End.Column; c++)
            {
                ExcelRange cell = ws.Cells[r, c];

                parts.Add(string.IsNullOrEmpty(cell.Formula) ? cell.Text : "=" + cell.FormulaR1C1);
            }
        }

        return string.Join("|", parts);
    }

    static VENTUPDATE.Outcome Update(string from, string to, List<VENTTEMPLATE.Level> levels,
                                     HashSet<string> ticked, List<string> storeys)
    {
        using (var p = new ExcelPackage(new FileInfo(from)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            VENTUPDATE.Outcome done = VENTUPDATE.Update(
                ws, layout, levels, l => ticked.Contains(l.Trim()), storeys, "2026-10-14");

            p.SaveAs(new FileInfo(to));

            return done;
        }
    }

    static void MatchTests()
    {
        Console.WriteLine();
        Console.WriteLine("== update: which row is which room");

        int[] m = VENTUPDATE.Match(
            new[] { C("L1", "k1", "1", "A"), C("L1", "k2", "2", "B") },
            new[] { C("L1", "k2", "1", "A") });

        Check(m[0] == 1, "the model ID wins over the number - a renumbered room is still its own row");

        m = VENTUPDATE.Match(new[] { C("L1", "", "1", "A") }, new[] { C("L2", "", "1", "A") });

        Check(m[0] == -1, "a number matches only on its own level");

        m = VENTUPDATE.Match(new[] { C("L1", "", "1", "X"), C("L1", "", "1", "Y") }, new[] { C("L1", "", "1", "Y") });

        Check(m[0] == 1, "two rows with one number: the one with the same name");

        m = VENTUPDATE.Match(new[] { C("L1", "", "3", "STORE") }, new[] { C("L1", "", "13", "STORE") });

        Check(m[0] == 0, "renumbered, no ID yet: found by a name only it has on the level");

        m = VENTUPDATE.Match(new[] { C("L1", "", "5", "BEDROOM"), C("L1", "", "6", "BEDROOM") },
                             new[] { C("L1", "", "15", "BEDROOM"), C("L1", "", "16", "BEDROOM") });

        Check(m[0] == -1 && m[1] == -1, "...never by a name two rooms share - no guessing");

        m = VENTUPDATE.Match(new[] { C("L1", "", "1", "A") }, new[] { C("L1", "", "1", "A"), C("L1", "", "1", "A") });

        Check(m[0] == 0 && m[1] == -1, "a row is one room's only");

        string[] storeys = { "L01", "L02", "L03" };

        Check(VENTUPDATE.Place(new[] { "L01", "L02" }, new[] { "L03" }, storeys).SequenceEqual(new[] { 0, 1, -1 }),
              "a new level above the sheet's goes after them");

        Check(VENTUPDATE.Place(new[] { "L01", "L03" }, new[] { "L02" }, storeys).SequenceEqual(new[] { 0, -1, 1 }),
              "...between them, where the model has it");

        Check(VENTUPDATE.Place(new[] { "L02" }, new[] { "L03", "L01" }, storeys).SequenceEqual(new[] { -2, 0, -1 }),
              "...below them, and several at once");

        Check(VENTUPDATE.Place(new[] { "L03", "L01" }, new[] { "L02" }, storeys).SequenceEqual(new[] { 0, 1, -1 }),
              "the sheet's own order of levels is never changed");
    }

    static void UpdateTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== update the imported worksheet: what was typed stays, rooms added, changed, removed");

        var before = new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 01", 0, Rm("9:1", "OFFICE", "101", 20), Rm("9:2", "MEETING", "102", 15),
                             Rm("9:3", "STORE", "103", 5), Rm("9:4", "PANTRY", "104", 8)),
            L("LEVEL 02", 4, Rm("9:5", "OFFICE", "201", 30), Rm("9:6", "LOBBY", "202", 12))
        };

        string basePath = Path.Combine(Out, "update-base.xlsx");

        VENTOFFICE.NewWorkbook(template, Job("Vent", before, null), basePath);

        // ---- the engineer's own inputs
        string typedPath = Path.Combine(Out, "update-typed.xlsx");
        int noteCol;
        string level2Before;

        using (var p = new ExcelPackage(new FileInfo(basePath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            int keyCol = VENTUPDATE.KeyColumn(ws, layout, false, out bool made);

            Check(keyCol > 0 && ws.Column(keyCol).Hidden && ws.Cells[RowOf(ws, layout, "102"), keyCol].Text == "9:2",
                  "every export writes each room's model ID in a hidden column (" + Col(keyCol) + ")");

            foreach (string n in new[] { "101", "104", "201" })
                ws.Cells[RowOf(ws, layout, n), Occ].Value = "Office Space";

            noteCol = NoteColumn(ws, layout, RowOf(ws, layout, "102"));

            if (noteCol > 0)
                ws.Cells[RowOf(ws, layout, "102"), noteCol].Value = "checked MH";

            ExcelRange fresh = ws.Cells[RowOf(ws, layout, "103"), layout.FreshAirCol];
            fresh.Formula = null;
            fresh.Value = 42.0;

            level2Before = Snapshot(ws, layout, "LEVEL 02");

            p.SaveAs(new FileInfo(typedPath));
        }

        Check(noteCol > 0, "an input column with no formula to type a note in: " + Col(noteCol));

        // ---- the architect's changes
        List<VENTTEMPLATE.Level> After(bool storeUnticked) => new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 01", 0, Rm("9:1", "OFFICE", "101", 21), Rm("9:2", "MEETING ROOM", "112", 15),
                             storeUnticked ? Rm("9:3", "STORE ROOM", "103", 9, keep: true) : Rm("9:3", "STORE", "103", 5),
                             Rm("9:7", "COPY", "105", 6), Rm("9:8", "PRINT", "106", 4, keep: true)),
            L("LEVEL 02", 4, Rm("9:5", "OPEN OFFICE", "201", 31)),
            L("LEVEL 03", 8, Rm("9:9", "PLANT", "301", 40))
        };

        var ticked = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LEVEL 01", "LEVEL 03" };
        var storeys = new List<string> { "LEVEL 01", "LEVEL 02", "LEVEL 03" };

        string outPath = Path.Combine(Out, "update-out.xlsx");

        VENTUPDATE.Outcome done = Update(typedPath, outPath, After(false), ticked, storeys);

        Check(done.Matched == 3 && done.Changed == 2 && done.Added == 2 && done.Gone == 1 && done.Kept == 0 &&
              done.LevelsAdded == 1 && done.LevelsLeft == 1,
              "3 matched (2 changed), 2 added, 1 no longer in the model, a level added, one left alone: " +
              $"{done.Matched} ({done.Changed}), {done.Added}, {done.Gone}, {done.LevelsAdded}, {done.LevelsLeft}");

        using (var p = new ExcelPackage(new FileInfo(outPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);
            int nameCol = layout.SpaceNameCol;

            int Row(string n) => RowOf(ws, layout, n);

            int l1 = LevelRowOf(ws, layout, "LEVEL 01");
            int l2 = LevelRowOf(ws, layout, "LEVEL 02");
            int l3 = LevelRowOf(ws, layout, "LEVEL 03");

            Check(l1 > 0 && l1 < l2 && l2 < l3,
                  "the levels in the sheet's order, the new one where the model has it: " + l1 + ", " + l2 + ", " + l3);

            string order = string.Join(",", Enumerable.Range(l1, l2 - l1)
                .Select(r => ws.Cells[r, layout.RoomNumberCol].Text).Where(t => t.Length > 0));

            Check(order == "101,112,103,105,104",
                  "LEVEL 01 in the model's order, the room no longer in it last: " + order);

            Check(ws.Cells[Row("101"), Occ].Text == "Office Space" &&
                  Convert.ToDouble(ws.Cells[Row("101"), layout.AreaCol].Value) == 21,
                  "a matched room keeps what was typed in it (its occupancy) and takes the model's new area");

            int meeting = Row("112");

            Check(meeting > 0 && NameAt(ws, layout, meeting) == "MEETING ROOM" && ws.Cells[meeting, noteCol].Text == "checked MH",
                  "renamed AND renumbered: found by its model ID, the note typed in its row still there");

            Check(ws.Cells[Row("103"), layout.FreshAirCol].Value is double typed && typed == 42 &&
                  string.IsNullOrEmpty(ws.Cells[Row("103"), layout.FreshAirCol].Formula),
                  "a value typed over the sheet's formula stays");

            int copy = Row("105");

            Check(copy > 0 && ws.Cells[copy, Occ].Text.Length == 0 && ws.Cells[copy, noteCol].Text.Length == 0 &&
                  !string.IsNullOrEmpty(ws.Cells[copy, layout.FreshAirCol].Formula),
                  "a new room: a row like the sheet's own, the inputs blank, the formulas in place");

            Check(Row("106") == 0, "an unticked room with no row is not added");

            int pantry = Row("104");

            Check(pantry > 0 && ws.Cells[pantry, nameCol].Style.Font.Italic &&
                  ws.Cells[pantry, nameCol].Comment != null &&
                  ws.Cells[pantry, nameCol].Comment.Text.Contains("Not in the model") &&
                  ws.Cells[pantry, Occ].Text == "Office Space",
                  "a room no longer in the model: kept, greyed, with a note - and what was typed in it");

            Check(Snapshot(ws, layout, "LEVEL 02") == level2Before && NameAt(ws, layout, Row("201")) == "OFFICE",
                  "a level not ticked is left exactly as it was - the model's rename not applied");

            Check(VENTSHEET.IsLevelFill(ws.Cells[l3, nameCol]) && Row("301") > l3,
                  "the new level looks like the others, its room under it");

            Check(ws.Cells[Row("301"), VENTUPDATE.KeyColumn(ws, layout, false, out bool _)].Text == "9:9",
                  "...with its model ID");

            p.Workbook.Calculate();

            double Num(int r, int c) => ws.Cells[r, c].Value is double d ? d : 0;

            int sub1 = Enumerable.Range(l1, l2 - l1).First(r => VENTSHEET.IsSubTotalRow(ws.Cells[r, nameCol].Text));

            Check(Math.Abs(Num(sub1, layout.AreaCol) - (21 + 15 + 5 + 6 + 8)) < 1e-9,
                  "LEVEL 01's Sub Total adds up its own rows, the greyed one's too: " + Num(sub1, layout.AreaCol));

            int total = Enumerable.Range(l3, 20).First(r => ws.Cells[r, 1].Text.Trim() == "Total");

            Check(Math.Abs(Num(total, layout.AreaCol) - (55 + 42 + 40)) < 1e-9,
                  "the Total is every level's Sub Total, the new level's too: " + Num(total, layout.AreaCol) +
                  " (" + Short(ws.Cells[total, layout.AreaCol].Formula) + ")");

            ExcelRangeBase sumif = ws.Cells[total + 1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column]
                .FirstOrDefault(c => (c.Formula ?? string.Empty).StartsWith("SUMIF("));

            System.Text.RegularExpressions.Match span = sumif == null
                ? null
                : System.Text.RegularExpressions.Regex.Match(sumif.Formula, @"\$[A-Z]+\$(\d+):\$[A-Z]+\$(\d+)");

            Check(span != null && span.Success &&
                  int.Parse(span.Groups[1].Value) == Row("101") && int.Parse(span.Groups[2].Value) == Row("301"),
                  "the summary tables read every room row, the new ones too: " +
                  (sumif == null ? "(none)" : Short(sumif.Formula)) + ", rooms " + Row("101") + "-" + Row("301"));

            int errors = 0;

            foreach (var cell in ws.Cells[layout.FirstDataRow, 1, total, ws.Dimension.End.Column])
                if (cell.Value is ExcelErrorValue e && (e.ToString() == "#REF!" || e.ToString() == "#NAME?"))
                    errors++;

            Check(errors == 0, "no #REF! or #NAME? (" + errors + ")");
        }

        // ---- read back through the REAL importer
        VENTSHEET.sheetpath = outPath;
        VENTSHEET.sheetname = "Vent";

        Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData> read = new VENTSHEET().excelsheet();

        bool Has(string level, string name, string number) =>
            read.Keys.Any(k => k.Level == level && k.SpaceName == name && k.RoomNumber == number);

        Check(read.Count == 8 && Has("LEVEL 01", "MEETING ROOM", "112") && Has("LEVEL 03", "PLANT", "301") &&
              Has("LEVEL 02", "OFFICE", "201") && Has("LEVEL 01", "PANTRY", "104"),
              "the importer reads it all back, each room under its level: " + read.Count + " of 8");

        // ---- again, nothing changed: nothing moves
        VENTUPDATE.Outcome again = Update(outPath, Path.Combine(Out, "update-again.xlsx"), After(false), ticked, storeys);

        Check(again.Matched == 5 && again.Added == 0 && again.Changed == 0 && again.Gone == 1 &&
              again.LevelsAdded == 0 && again.RowsBefore == again.RowsAfter,
              $"updated again with nothing changed: all 5 found, nothing added, as many rows ({again.RowsBefore} -> {again.RowsAfter})");

        // ---- an unticked room keeps its row as it is
        string keptPath = Path.Combine(Out, "update-kept.xlsx");

        VENTUPDATE.Outcome left = Update(outPath, keptPath, After(true), ticked, storeys);

        using (var p = new ExcelPackage(new FileInfo(keptPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);
            int store = RowOf(ws, layout, "103");

            Check(left.Kept == 1 && NameAt(ws, layout, store) == "STORE" &&
                  Convert.ToDouble(ws.Cells[store, layout.AreaCol].Value) == 5 &&
                  ws.Cells[store, layout.FreshAirCol].Value is double v && v == 42,
                  "an unticked room keeps its row exactly - the model's new name and area not taken");
        }
    }

    static void UpdateWithoutKeysTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== update a sheet written before there was a model ID column");

        var before = new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 01", 0, R("OFFICE", "101", 20, 3), R("MEETING", "102", 15, 3), R("STORE", "103", 5, 3),
                             R("BEDROOM", "104", 12, 3), R("BEDROOM", "105", 12, 3))
        };

        string basePath = Path.Combine(Out, "update-nokeys.xlsx");

        VENTOFFICE.NewWorkbook(template, Job("Vent", before, null), basePath);

        using (var p = new ExcelPackage(new FileInfo(basePath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];

            Check(VENTUPDATE.KeyColumn(ws, VENTSHEET.DetectLayout(ws, false), false, out bool _) == 0,
                  "rooms with no model ID: no ID column written");
        }

        var after = new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 01", 0, Rm("9:1", "OFFICE", "101", 20), Rm("9:2", "BOARDROOM", "102", 15),
                             Rm("9:3", "STORE", "113", 5), Rm("9:4", "BEDROOM", "114", 12), Rm("9:5", "BEDROOM", "115", 12))
        };

        string outPath = Path.Combine(Out, "update-nokeys-out.xlsx");

        VENTUPDATE.Outcome done = Update(basePath, outPath, after,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LEVEL 01" }, new List<string> { "LEVEL 01" });

        Check(done.KeyColumnAdded && done.Matched == 3 && done.Added == 2 && done.Gone == 2,
              "renamed: by its number; renumbered: by a name only it has; two bedrooms renumbered: new rows, " +
              $"the old ones greyed - {done.Matched} matched, {done.Added} added, {done.Gone} greyed");

        using (var p = new ExcelPackage(new FileInfo(outPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);
            int keyCol = VENTUPDATE.KeyColumn(ws, layout, false, out bool _);

            Check(keyCol > 0 && ws.Column(keyCol).Hidden && ws.Cells[RowOf(ws, layout, "113"), keyCol].Text == "9:3",
                  "the ID column added on the way, hidden - the next update matches exactly");
        }
    }

    static void UpdateMergedTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== update a sheet with merged rooms");

        List<VENTTEMPLATE.Level> Rooms()
        {
            VENTTEMPLATE.Room living = Rm("9:1", "LIVING", "101", 30);
            VENTTEMPLATE.Room kitchen = Rm("9:2", "KITCHEN", "102", 12);

            living.Merge = kitchen.Merge = "9:1";

            return new List<VENTTEMPLATE.Level> { L("LEVEL 01", 0, living, kitchen, Rm("9:3", "BED", "103", 14)) };
        }

        string basePath = Path.Combine(Out, "update-merged.xlsx");
        string typedPath = Path.Combine(Out, "update-merged-typed.xlsx");
        string outPath = Path.Combine(Out, "update-merged-out.xlsx");

        VENTOFFICE.NewWorkbook(template, Job("Vent", Rooms(), null), basePath);

        double was;

        using (var p = new ExcelPackage(new FileInfo(basePath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            foreach (string n in new[] { "101", "102", "103" })
                ws.Cells[RowOf(ws, layout, n), Occ].Value = "Office Space";

            p.SaveAs(new FileInfo(typedPath));
            p.Workbook.Calculate();

            was = ws.Cells[RowOf(ws, layout, "101"), layout.FreshAirCol].Value is double d ? d : -1;
        }

        Update(typedPath, outPath, Rooms(),
               new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LEVEL 01" }, new List<string> { "LEVEL 01" });

        using (var p = new ExcelPackage(new FileInfo(outPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);
            int top = RowOf(ws, layout, "101");
            int col = layout.FreshAirCol;
            string f = ws.Cells[top, col].Formula ?? string.Empty;
            int n = (f.Length - f.Replace("N(", string.Empty).Length) / 2;

            Check(ws.MergedCells[top, col] == Col(col) + top + ":" + Col(col) + (top + 1) && n == 2,
                  "the merged pair merged again, its total made from each room's own formula, not the old total: " + Short(f));

            p.Workbook.Calculate();

            double now = ws.Cells[top, col].Value is double d ? d : -2;

            Check(was > 0 && Math.Abs(now - was) < 1e-9, "...and it adds up to what it did: " + now + " = " + was);
        }

        // ---- a total typed over the merged cell is the engineer's
        string overPath = Path.Combine(Out, "update-merged-typed-total.xlsx");
        string overOut = Path.Combine(Out, "update-merged-typed-total-out.xlsx");

        using (var p = new ExcelPackage(new FileInfo(typedPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);
            ExcelRange cell = ws.Cells[RowOf(ws, layout, "101"), layout.FreshAirCol];

            cell.Formula = null;
            cell.Value = 99.0;

            p.SaveAs(new FileInfo(overPath));
        }

        Update(overPath, overOut, Rooms(),
               new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LEVEL 01" }, new List<string> { "LEVEL 01" });

        using (var p = new ExcelPackage(new FileInfo(overOut)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);
            int top = RowOf(ws, layout, "101");
            int col = layout.FreshAirCol;

            Check(ws.MergedCells[top, col] == Col(col) + top + ":" + Col(col) + (top + 1) &&
                  ws.Cells[top, col].Value is double typed && typed == 99 &&
                  string.IsNullOrEmpty(ws.Cells[top, col].Formula),
                  "a total typed over the merged cell stays, the pair still merged - not added to the rooms' own");
        }
    }

    // ==================================================
    // Occupancy categories from room names (VENTOCCUPANCY)
    // ==================================================

    // User (2026-10-14): "automatically detect Occupancy Category and
    // assign it to the room for both fresh and exhaust". Apartments take
    // "Dwelling Unit" (the user's choice), whose occupant test was broken in
    // the office's own workbook.

    static VENTTEMPLATE.Room Dw(VENTTEMPLATE.Room room)
    {
        room.InDwelling = true;
        return room;
    }

    static void OccupancyRuleTests(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== occupancy: which rule a room name takes");

        List<VENTOCCUPANCY.Rule> rules = VENTOCCUPANCY.Defaults();

        string For(string name, bool apartment)
        {
            VENTOCCUPANCY.Rule rule = VENTOCCUPANCY.RuleFor(name, apartment, rules);
            return rule == null ? "(none)" : rule.Fresh + " / " + rule.Exhaust;
        }

        Check(For("MASTER BEDROOM", true) == "Dwelling Unit / ", "an apartment's bedroom: Dwelling Unit, no exhaust - " + For("MASTER BEDROOM", true));

        Check(For("KITCHEN", true) == "Dwelling Unit / Residential kitchens- Apartments" &&
              For("KITCHEN", false) == "Kitchen (Cooking) / Kitchen-Commercial",
              "a kitchen in an apartment or not: " + For("KITCHEN", true) + " | " + For("KITCHEN", false));

        Check(For("MASTER ENSUITE", true) == " / Toilets - private (residential)" && For("GENTS TOILET", false) == " / Toilets - public",
              "toilets: exhaust only, private in an apartment, public elsewhere");

        Check(For("LIFT LOBBY", false).StartsWith("Common Corridors") && For("MAIN LOBBY", false).StartsWith("Main Entry Lobbies"),
              "the more specific rule wins: LIFT LOBBY over LOBBY");

        // User (2026-10-16): FIREMAN LOBBY had no category - a lift lobby's,
        // not the plain LOBBY's main entry lobby
        Check(For("FIREMAN LOBBY", false) == "Common Corridors / " && For("FIRE MAN LOBBY", false) == "Common Corridors / ",
              "a fireman lobby is circulation, as a lift lobby is: " + For("FIREMAN LOBBY", false));

        Check(For("STAIR 01", false) == " / ", "...and a rule may say: nothing (" + For("STAIR 01", false) + ")");

        Check(For("BATHROOM", true).EndsWith("Toilets - private (residential)"), "a rule's word starts a name's: BATH finds BATHROOM");

        Check(For("G.RM", false).EndsWith("recycling") && !For("GYM RM", false).EndsWith("recycling") && For("W.C.", true).EndsWith("(residential)"),
              "a word of one or two letters must be a whole word: G.RM is a garbage room, GYM RM is not");

        // "EQUALS" (user, 2026-10-19): the name IS the words - and such a
        // rule wins over every "contains" rule
        List<VENTOCCUPANCY.Rule> withEquals = VENTOCCUPANCY.Defaults();

        withEquals.Add(new VENTOCCUPANCY.Rule { Words = "LOBBY", Exact = true, Fresh = "Reception Areas" });
        withEquals.Add(new VENTOCCUPANCY.Rule { Words = "KITCHEN", Exact = true, Fresh = "Break Room", Exhaust = "Kitchenettes" });
        withEquals.Add(new VENTOCCUPANCY.Rule
        {
            Scope = VENTOCCUPANCY.Scope.Apartment, Words = "kitchen", Exact = true, Fresh = "Dwelling Unit", Exhaust = "Kitchenettes"
        });
        withEquals.Add(new VENTOCCUPANCY.Rule
        {
            Scope = VENTOCCUPANCY.Scope.Apartment, Words = "master bedroom", Exact = true, Fresh = "Bedroom / Living Room"
        });

        string Eq(string name, bool apartment)
        {
            VENTOCCUPANCY.Rule rule = VENTOCCUPANCY.RuleFor(name, apartment, withEquals);
            return rule == null ? "(none)" : rule.Fresh + " / " + rule.Exhaust;
        }

        Check(Eq("LOBBY", false) == "Reception Areas / ", "equals LOBBY: a room named LOBBY takes it, not contains LOBBY's");
        Check(Eq("lobby", false) == "Reception Areas / " && Eq(" Lobby. ", false) == "Reception Areas / ",
              "equals: any case, any spaces or stops around it");
        Check(Eq("LIFT LOBBY", false).StartsWith("Common Corridors") && Eq("MAIN LOBBY", false).StartsWith("Main Entry Lobbies") &&
              Eq("LOBBY 2", false).StartsWith("Main Entry Lobbies"),
              "equals takes no longer name: LIFT LOBBY, MAIN LOBBY, LOBBY 2 keep the contains rules'");
        Check(Eq("KITCHEN", false) == "Break Room / Kitchenettes",
              "an equals rule for any room beats a contains rule for the room's own kind: " + Eq("KITCHEN", false));
        Check(Eq("KITCHEN", true) == "Dwelling Unit / Kitchenettes",
              "two equals rules: the one for apartments beats the one for any room - " + Eq("KITCHEN", true));
        Check(Eq("Master-Bedroom", true) == "Bedroom / Living Room / ",
              "words compared past spaces and punctuation: Master-Bedroom equals MASTER BEDROOM");
        Check(Eq("MASTER BEDROOM 2", true) == "Dwelling Unit / " && Eq("BEDROOM MASTER", true) == "Dwelling Unit / ",
              "not MASTER BEDROOM 2, nor BEDROOM MASTER: the same words in the same order, nothing more");
        Check(Eq("MASTER BEDROOM", false) == "Bedroom / Living Room / ",
              "an equals rule for apartments: not a room outside one - the contains rule's");

        Check(For("SWITCH ROOM", false) == "(none)" && For("PLANT", false) == "(none)", "a name no rule fits: left blank");

        // Every built-in category spelled as the office's lists have it -
        // VLOOKUP finds nothing for a near miss
        using (var p = new ExcelPackage(new MemoryStream(template)))
        {
            List<string> fresh = VENTOCCUPANCY.Table(p.Workbook, false);
            List<string> exhaust = VENTOCCUPANCY.Table(p.Workbook, true);

            var wrong = rules.Where(r => r.Fresh.Length > 0 && VENTOCCUPANCY.Pick(r.Fresh, fresh) == null).Select(r => r.Fresh)
                .Concat(rules.Where(r => r.Exhaust.Length > 0 && VENTOCCUPANCY.Pick(r.Exhaust, exhaust) == null).Select(r => r.Exhaust))
                .Distinct().ToList();

            Check(wrong.Count == 0, "every built-in category is one Table 6-1 / 6-4 has" +
                                    (wrong.Count == 0 ? "" : ": NOT " + string.Join(", ", wrong)));

            Check(fresh.Contains("Dwelling Unit") && !fresh.Contains("Residential") && !fresh.Contains("Office Buildings"),
                  "Table 6-1's section headings are not offered as categories (" + fresh.Count + " offered)");

            Check(VENTOCCUPANCY.Pick("toilets – PUBLIC", exhaust) == exhaust.First(e => e.StartsWith("Toilets") && e.EndsWith("public")),
                  "a category written with another dash or case is the list's own spelling");
        }

        // ---- the office's rules, on this PC
        List<VENTOCCUPANCY.Rule> back = VENTOCCUPANCY.Parse(VENTOCCUPANCY.Compose(rules));

        Check(back.Count == rules.Count &&
              back.Zip(rules, (a, b) => a.Scope == b.Scope && a.Words == b.Words && a.Fresh == b.Fresh && a.Exhaust == b.Exhaust &&
                                        a.Use == b.Use && a.Units == b.Units).All(x => x) &&
              back.Any(r => r.Exhaust == VENTOCCUPANCY.ApartmentKitchen && r.Units == 1) &&
              back.Any(r => r.Use == VENTOCCUPANCY.LightUse && r.Units == 1) &&
              VENTOCCUPANCY.Parse("rule\tany\tOFFICE\tOffice Space\t\n")[0].Units == 0,
              "the rules survive being saved and read back: " + back.Count);

        // The override of 2026-10-15..19 was an eighth field: never read as
        // anything now - least of all as units
        VENTOCCUPANCY.Rule eighth = VENTOCCUPANCY.Parse(
            "rule\tapartment\tKITCHEN\tDwelling Unit\tResidential kitchens- Apartments\t\t\t50\n")[0];

        Check(eighth.Units == 0 && eighth.Use == "" && !eighth.Exact && eighth.Exhaust == VENTOCCUPANCY.ApartmentKitchen,
              "an old file's override (an eighth field) is read as nothing - not units, not equals");

        VENTOCCUPANCY.Rule equalsBack = VENTOCCUPANCY.Parse(VENTOCCUPANCY.Compose(new[]
        {
            new VENTOCCUPANCY.Rule { Words = "LOBBY", Exact = true, Fresh = "Reception Areas" },
            new VENTOCCUPANCY.Rule { Words = "LOBBY", Fresh = "Main Entry Lobbies" }
        }))[0];

        Check(equalsBack.Exact && equalsBack.Words == "LOBBY" && !VENTOCCUPANCY.Parse(VENTOCCUPANCY.Compose(new[]
              {
                  new VENTOCCUPANCY.Rule { Words = "LOBBY", Fresh = "Main Entry Lobbies" }
              }))[0].Exact,
              "equals and contains survive being saved and read back");

        VENTOCCUPANCY.Rule equalsKitchen = VENTOCCUPANCY.Defaults()
            .First(r => r.Scope == VENTOCCUPANCY.Scope.Apartment && r.Words == "KITCHEN").Clone();
        equalsKitchen.Exact = true;
        equalsKitchen.Units = 0;

        Check(VENTOCCUPANCY.WithBuiltIn(new List<VENTOCCUPANCY.Rule> { equalsKitchen })[0].Units == 0,
              "a built-in rule made 'equals' is the office's own: no built-in values given to it");

        string path = Path.Combine(Out, "occupancy-rules.txt");

        VENTOCCUPANCY.Forget(path);

        Check(VENTOCCUPANCY.Load(path).Count == rules.Count, "no rules saved: the built-in ones");

        VENTOCCUPANCY.Save(new List<VENTOCCUPANCY.Rule>
        {
            new VENTOCCUPANCY.Rule { Scope = VENTOCCUPANCY.Scope.Apartment, Words = "BEDROOM", Fresh = "Bedroom / Living Room" }
        }, path);

        Check(VENTOCCUPANCY.Load(path).Count == 1 && VENTOCCUPANCY.Load(path)[0].Fresh == "Bedroom / Living Room",
              "rules saved are the office's from then on");

        // A FILE SAVED BEFORE THE BUILT-IN VALUES IT LACKS (user, 2026-10-19:
        // KIFAF's kitchens at 0 L/s). Saved like the user's own: the
        // built-in rules with the units gone, the apartment toilets' nature
        // of use kept - plus a value typed, a category changed, a rule of
        // the office's own and a word typed in lower case
        List<VENTOCCUPANCY.Rule> old = VENTOCCUPANCY.Defaults();

        foreach (VENTOCCUPANCY.Rule r in old)
        {
            if (!(r.Scope == VENTOCCUPANCY.Scope.Apartment && r.Use == VENTOCCUPANCY.LightUse))
                r.Use = string.Empty;

            r.Units = 0;
        }

        VENTOCCUPANCY.Rule Saved(VENTOCCUPANCY.Scope scope, string words)
        {
            return old.First(r => r.Scope == scope && r.Words == words);
        }

        Saved(VENTOCCUPANCY.Scope.Apartment, "PANTRY").Units = 3;
        Saved(VENTOCCUPANCY.Scope.Apartment, "TOILET").Exhaust = "Toilets - public";
        Saved(VENTOCCUPANCY.Scope.Apartment, "SHOWER").Words = "shower";
        old.Add(new VENTOCCUPANCY.Rule
        {
            Scope = VENTOCCUPANCY.Scope.Apartment, Words = "SCULLERY", Exhaust = "Residential kitchens- Apartments"
        });

        VENTOCCUPANCY.Rule anyKitchen = Saved(VENTOCCUPANCY.Scope.Apartment, "KITCHEN").Clone();
        anyKitchen.Scope = VENTOCCUPANCY.Scope.Any;
        old.Add(anyKitchen);

        VENTOCCUPANCY.Save(old, path);

        List<VENTOCCUPANCY.Rule> loaded = VENTOCCUPANCY.Load(path);

        VENTOCCUPANCY.Rule Got(VENTOCCUPANCY.Scope scope, string words)
        {
            return loaded.First(r => r.Scope == scope && VENTOCCUPANCY.Tokens(r.Words).SequenceEqual(VENTOCCUPANCY.Tokens(words)));
        }

        Check(Got(VENTOCCUPANCY.Scope.Apartment, "KITCHEN").Units == 1,
              "an old file's apartment KITCHEN gets the built-in 1 unit");
        Check(Got(VENTOCCUPANCY.Scope.Apartment, "ENSUITE").Units == 1 &&
              Got(VENTOCCUPANCY.Scope.Apartment, "ENSUITE").Use == VENTOCCUPANCY.LightUse,
              "a blank units takes the built-in 1, the nature of use typed stands");
        Check(Got(VENTOCCUPANCY.Scope.Other, "SHOWER").Use == VENTOCCUPANCY.LightUse &&
              Got(VENTOCCUPANCY.Scope.Other, "SHOWER").Units == 1,
              "a rule with both blank takes both");
        Check(Got(VENTOCCUPANCY.Scope.Apartment, "SHOWER").Units == 1, "words match in any case");
        Check(Got(VENTOCCUPANCY.Scope.Apartment, "PANTRY").Units == 3, "a value typed in the file stands");
        Check(Got(VENTOCCUPANCY.Scope.Apartment, "TOILET").Units == 0 &&
              Got(VENTOCCUPANCY.Scope.Apartment, "TOILET").Use == VENTOCCUPANCY.LightUse,
              "a rule whose category was changed is the office's own: its blanks stay blank");
        Check(Got(VENTOCCUPANCY.Scope.Apartment, "SCULLERY").Units == 0, "a rule the built-in list has not is left alone");
        Check(Got(VENTOCCUPANCY.Scope.Any, "KITCHEN").Units == 0, "the apartment KITCHEN rule made one for any room: left alone");
        Check(Got(VENTOCCUPANCY.Scope.Other, "KITCHEN").Units == 0 && Got(VENTOCCUPANCY.Scope.Any, "OFFICE").Units == 0,
              "a built-in rule with nothing to give gets nothing");
        Check(loaded.Count == old.Count, "every saved rule is still there, none added: " + loaded.Count);

        VENTOCCUPANCY.Forget(path);

        Check(!File.Exists(path) && VENTOCCUPANCY.Load(path).Count == rules.Count, "Reset: the built-in rules again");
    }

    static void OccupancyExportTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== occupancy: categories into a new sheet; Dwelling Unit calculates");

        var levels = new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 05", 0,
              R("OFFICE", "501", 40, 3), R("LIFT LOBBY", "502", 12, 3), R("GENTS TOILET", "503", 10, 3), R("PLANT", "504", 20, 3),
              Dw(In("9:1", "1BDR", "A-01", "BEDROOM", "505", 14)), Dw(In("9:1", "1BDR", "A-01", "KITCHEN", "506", 8)),
              Dw(In("9:1", "1BDR", "A-01", "ENSUITE", "507", 5)),
              Dw(In("9:2", "2BDR", "A-02", "MASTER BEDROOM", "510", 16)), Dw(In("9:2", "2BDR", "A-02", "BEDROOM 2", "511", 12)),
              Dw(In("9:3", "STUDIO", "A-03", "STUDIO ROOM", "520", 28)))
        };

        VENTOFFICE.Job job = Job("Occ", levels, null);
        job.Occupancy = VENTOCCUPANCY.Defaults();

        // A rule naming a category Table 6-1 has not got
        job.Occupancy.Add(new VENTOCCUPANCY.Rule { Words = "PLANT", Fresh = "Plant Room" });

        string path = Path.Combine(Out, "occupancy.xlsx");

        VENTOFFICE.Result result = VENTOFFICE.NewWorkbook(template, job, path);

        Check(result.Occupancy.Fresh == 6 && result.Occupancy.Exhaust == 3,
              $"6 outside air and 3 exhaust categories filled: {result.Occupancy.Fresh}, {result.Occupancy.Exhaust}");

        Check(result.Occupancy.Apartments == 3 && result.Occupancy.Occupants == 6,
              $"3 apartments given bedrooms and occupants, 6 people in all (2 + 3 + 1): {result.Occupancy.Apartments}, {result.Occupancy.Occupants}");

        Check(result.Occupancy.NoUnits.Contains("503 GENTS TOILET") && result.Occupancy.NoUnits.Count == 1,
              "a public toilet with no fixtures counted is said to get 0 L/s: " + string.Join(", ", result.Occupancy.NoUnits));

        Check(result.DwellingFixed == 1, "the room row's \"Dwelling Unit\" test mended (" + result.DwellingFixed + ")");

        using (var p = new ExcelPackage(new FileInfo(path)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Occ"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);
            int fresh = layout.FreshCategoryCol, exhaust = layout.ExhaustCategoryCol;

            Check(fresh == Occ && Col(exhaust) == "AC", "the two category columns found by their headings: " + Col(fresh) + ", " + Col(exhaust));

            string Cat(string number, int col) => ws.Cells[RowOf(ws, layout, number), col].Text;

            Check(Cat("501", fresh) == "Office Space" && Cat("502", fresh) == "Common Corridors" &&
                  Cat("505", fresh) == "Dwelling Unit" && Cat("506", fresh) == "Dwelling Unit",
                  "outside air: the office, the lift lobby, the apartment's rooms");

            Check(Cat("503", exhaust) == "Toilets - public" && Cat("506", exhaust) == "Residential kitchens- Apartments" &&
                  Cat("507", exhaust) == "Toilets - private (residential)" && Cat("503", fresh) == "" && Cat("507", fresh) == "",
                  "exhaust: the toilets and the apartment's kitchen; no outside air for a toilet");

            Check(Cat("504", fresh) == "" && Cat("504", exhaust) == "" && result.Occupancy.Missing.Contains("Plant Room"),
                  "a rule naming a category the sheet's list has not got writes nothing - and says so");

            int bed = RowOf(ws, layout, "505");

            Check((ws.Cells[bed, 18].Formula ?? "").Contains("'ASHRAE 62.1 - Table 6-1'!$A$81"),
                  "the Dwelling Unit test reads Table 6-1's own cell: " + Short(ws.Cells[bed, 18].Formula));

            p.Workbook.Calculate();

            var errors = new List<string>();

            foreach (string number in new[] { "501", "502", "503", "504", "505", "506", "507", "510", "511", "520" })
            {
                int row = RowOf(ws, layout, number);

                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                {
                    if (ws.Cells[row, c].Value is ExcelErrorValue e && e.ToString() != "#DIV/0!")
                        errors.Add(number + " " + Col(c) + " " + e.ToString());
                }
            }

            Check(errors.Count == 0, "no #VALUE!, #N/A or #REF! on any room row" + (errors.Count == 0 ? "" : ": " + string.Join(", ", errors)));

            double Num(string number, int col) => ws.Cells[RowOf(ws, layout, number), col].Value is double d ? d : -1;

            // ---- the exhaust each category now gives (user, 2026-10-15)
            int kitchen = RowOf(ws, layout, "506");

            // Per unit, as a private toilet (user, 2026-10-19) - not the
            // override it had from 10-15
            Check(result.Kitchen.Rates && result.Kitchen.Formulas == 2,
                  "the apartment kitchen made per unit: Table 6-4's rates, and 2 room formulas (" + result.Kitchen.Formulas + ")");

            ExcelWorksheet table = p.Workbook.Worksheets.First(w => w.Name.Contains("Table 6-4"));
            int kRow = Enumerable.Range(1, 60).First(r => table.Cells[r, 1].Text == VENTOCCUPANCY.ApartmentKitchen);

            Check(table.Cells[kRow, 7].Value is double lo && lo == 25 && table.Cells[kRow, 9].Value is double hi && hi == 50 &&
                  table.Cells[kRow, 2].Text == "50" && table.Cells[kRow, 4].Text == "100",
                  "Table 6-4's apartment kitchen row: 50 / 100 cfm, 25 / 50 L/s a unit");

            Check(ws.Cells[kitchen, layout.OverrideCol].Text == "" && ws.Cells[kitchen, layout.BalancingCol].Text == "" &&
                  Num("506", layout.UnitsCol) == 1 && Num("506", layout.ExhaustAirCol) == 50,
                  "the apartment kitchen: 1 unit x 50 L/s, no override - " + Col(layout.ExhaustAirCol) + " " +
                  Num("506", layout.ExhaustAirCol));

            ws.Cells[kitchen, layout.UseCol].Value = VENTOCCUPANCY.LightUse;
            p.Workbook.Calculate();

            Check(Num("506", layout.ExhaustAirCol) == 25, "light use: 25 L/s, as a private toilet takes its lower rate - " +
                                                         Num("506", layout.ExhaustAirCol));

            ws.Cells[kitchen, layout.UseCol].Value = null;
            p.Workbook.Calculate();

            Check(Cat("507", layout.UseCol) == VENTOCCUPANCY.LightUse && Num("507", layout.UnitsCol) == 1 &&
                  Num("507", layout.ExhaustAirCol) == 10,
                  "the ensuite: light use, one fixture - 10 L/s, not 0 - " + Num("507", layout.ExhaustAirCol));

            Check(Num("503", layout.ExhaustAirCol) == 0, "a public toilet with no fixtures counted: 0 L/s, as the log says");

            // ---- each apartment's bedrooms and people
            Check(Num("505", layout.BedroomsCol) == 1 && Num("510", layout.BedroomsCol) == 2 && Num("520", layout.BedroomsCol) == 0,
                  "No of Bedrooms on each apartment's first row: 1, 2 and the studio's 0");

            Check(Num("505", layout.OccupantsCol) == 2 && Num("510", layout.OccupantsCol) == 2 &&
                  Num("511", layout.OccupantsCol) == 1 && Num("520", layout.OccupantsCol) == 1 &&
                  Cat("506", layout.OccupantsCol) == "",
                  "bedrooms + 1 people: 2 in the master bedroom, 1 in each other; the studio's 1; none in the kitchen");

            Check(ws.Cells[bed, 18].Text == "NA" && Num("505", 19) == 2 && Num("505", layout.FreshAirCol) == 10,
                  "a Dwelling Unit bedroom takes its 2 people: 2.5 x 2 + 0.3 x 14 m2 = 9.2, rounded up to " + Num("505", layout.FreshAirCol));

            Check(ws.Cells[RowOf(ws, layout, "501"), layout.FreshAirCol].Value is double o && o == 20,
                  "the office: 2.5 x 2 people + 0.3 x 40 m2 = 17, rounded up to 20 - " + ws.Cells[RowOf(ws, layout, "501"), layout.FreshAirCol].Text);
        }

        // ---- read back as Levels and rooms reads it (user, 2026-10-25: the
        // sheet's columns in the window): categories, occupants, units, area
        VENTSHEET.sheetpath = path;
        VENTSHEET.sheetname = "Occ";

        new VENTSHEET().excelsheet();

        VENTSHEET.AirData Read(string number) => VENTSHEET.LoadedRows.First(r => r.Key.RoomNumber == number).Value;

        Check(Read("501").FreshCategory == "Office Space" && Read("501").ExhaustCategory == string.Empty && Read("501").AreaM2 == 40,
              "the reader keeps the office's categories and area: '" + Read("501").FreshCategory + "', '" + Read("501").ExhaustCategory + "', " + Read("501").AreaM2);

        Check(Read("503").ExhaustCategory == "Toilets - public" && Read("503").FreshCategory == string.Empty,
              "...the public toilet's exhaust category, no outside air one");

        Check(Read("505").Occupants == 2 && Read("511").Occupants == 1 && double.IsNaN(Read("506").Occupants),
              "...the bedrooms' occupants, and a blank (NaN, not 0) for the kitchen: " + Read("505").Occupants + ", " + Read("511").Occupants + ", " + Read("506").Occupants);

        Check(Read("507").Units == 1 && double.IsNaN(Read("503").Units),
              "...the ensuite's unit; the public toilet's blank: " + Read("507").Units + ", " + Read("503").Units);
    }

    // ==================================================
    // Levels and rooms' new values (VENTWHATIF)
    // ==================================================

    // User, 2026-10-26: the occupancy chosen in Levels and rooms, worked
    // through the sheet's own formulas in a COPY of the workbook - never
    // saved. Runs on OccupancyExportTest's and MergedTest's workbooks.
    static void WhatIfTests()
    {
        Console.WriteLine();
        Console.WriteLine("== new values: the sheet's own formulas over the occupancy chosen, in a copy");

        string exported = Path.Combine(Out, "occupancy.xlsx");
        string saved = Path.Combine(Out, "occupancy-saved.xlsx");

        // As Excel leaves it: calculated, the results saved with it
        using (var p = new ExcelPackage(new FileInfo(exported)))
        {
            p.Workbook.Calculate();
            p.SaveAs(new FileInfo(saved));
        }

        var none = new Dictionary<int, VENTARRANGE.RoomOccupancy>();

        foreach (string path in new[] { exported, saved })
        {
            string label = path == exported ? "never opened in Excel" : "saved by Excel";

            byte[] before = File.ReadAllBytes(path);
            DateTime when = File.GetLastWriteTimeUtc(path);

            VENTSHEET.sheetpath = path;
            VENTSHEET.sheetname = "Occ";

            new VENTSHEET().excelsheet();

            VENTSHEET.AirData Air(string number) => VENTSHEET.LoadedRows.First(r => r.Key.RoomNumber == number).Value;

            string trouble;

            using (VENTWHATIF w = VENTWHATIF.Open(path, "Occ", out trouble))
            {
                Check(w != null, label + ": the copy opens" + (trouble == null ? "" : " - " + trouble));

                if (w == null)
                    continue;

                VENTSHEET.SheetLayout layout = w.Layout;

                int office = Air("501").Row, kitchen = Air("506").Row, ensuite = Air("507").Row;

                Check(office > layout.HeaderEndRow && kitchen > office && Air("507").Use == VENTOCCUPANCY.LightUse && Air("501").Use == "",
                      label + ": each room's row and nature of use read - " + office + ", " + kitchen + ", '" + Air("507").Use + "'");

                Dictionary<int, VENTFLOWS.Calc> calc = w.Apply(none);

                Check(calc[office].Air.Fresh == 20 && calc[kitchen].Air.Extract == 50 && calc[ensuite].Air.Extract == 10 &&
                      calc[office].SheetCells == null,
                      label + ": nothing chosen, every row as the sheet has it - 20, 50, 10: " +
                      calc[office].Air.Fresh + ", " + calc[kitchen].Air.Extract + ", " + calc[ensuite].Air.Extract);

                // ---- the calculation's columns, as the sheet heads them
                List<VENTFLOWS.CalcColumn> columns = w.Columns;

                var shown = new[] { layout.FreshAirCol, layout.ExhaustAirCol, layout.FreshCategoryCol, layout.ExhaustCategoryCol,
                                    layout.UseCol, layout.OccupantsCol, layout.UnitsCol, layout.AreaCol, layout.RoomNumberCol };

                VENTFLOWS.CalcColumn rp = columns.FirstOrDefault(c => c.Heading.Contains("Rp") && c.Units.Contains("person"));
                VENTFLOWS.CalcColumn actual = columns.FirstOrDefault(c => c.Heading.Contains("Actual Occupancy"));

                Check(rp != null && actual != null && !columns.Any(c => shown.Contains(c.Col)) &&
                      columns.All(c => c.Col > layout.RoomNumberCol && c.Label.Length > 0 && !c.Label.Contains("-Apr-") && c.Label != "Date"),
                      label + ": the other columns, none shown already, labelled without the sign-off cells - " +
                      string.Join(" | ", columns.Select(c => c.Letter + " " + c.Label + (c.Units.Length > 0 ? " (" + c.Units + ")" : ""))));

                // ---- only the calculation, for the rooms' own rows (user,
                // 2026-10-28: "many columns are empty without headers"): a
                // column a formula works out with something in it
                List<int> roomRows = VENTSHEET.LoadedRows.Select(r => r.Value.Row).Where(r => r > 0).ToList();
                List<VENTFLOWS.CalcColumn> worked = w.ColumnsFor(roomRows);

                using (var p = new ExcelPackage(new FileInfo(path)))
                {
                    ExcelWorksheet ws = p.Workbook.Worksheets["Occ"];

                    bool Formula(int col) => roomRows.Any(r => !string.IsNullOrEmpty(ws.Cells[r, col].Formula));

                    List<VENTFLOWS.CalcColumn> typed = columns.Where(c => !Formula(c.Col)).ToList();

                    Check(worked.Count > 0 && worked.All(c => Formula(c.Col)) && worked.Contains(rp) && worked.Contains(actual) &&
                          typed.Count > 0 && !worked.Intersect(typed).Any(),
                          label + ": the calculation shown - worked out by a formula - " + worked.Count + " of " + columns.Count +
                          "; typed left out: " + string.Join(", ", typed.Select(c => c.Letter + " " + c.Label)));
                }

                Check(actual.Label == "Total Outside Air based on Actual Occupancy",
                      label + ": a column called by its own heading, not the formula written under it - " + actual.Label);

                Check(w.ColumnsFor(new int[0]).Count == 0, label + ": no rooms, no calculation shown");

                Check(w.FreshChoices(office).Contains("Office Space") && w.ExhaustChoices(office).Contains("Toilets - public") &&
                      w.UseChoices(office).Contains(VENTOCCUPANCY.LightUse),
                      label + ": the lists the cells offer");

                // ---- 10 people in the office: 2.5 x 10 + 0.3 x 40 = 37, rounded up to 40
                calc = w.Apply(new Dictionary<int, VENTARRANGE.RoomOccupancy> { [office] = new VENTARRANGE.RoomOccupancy { Occupants = 10 } });

                double Figure(string text) => double.TryParse(text, out double v) ? v : double.NaN;

                Check(calc[office].Air.Fresh == 40 && Figure(calc[office].Cells[actual.Col]) == 37 && Figure(calc[office].SheetCells[actual.Col]) == 17,
                      label + ": 10 people in the office - 37 L/s, 40 rounded up; the sheet's 17 beside it: " +
                      calc[office].Air.Fresh + ", " + calc[office].Cells[actual.Col] + " / " + calc[office].SheetCells[actual.Col]);

                // ---- the kitchen: 2 units, then light use
                calc = w.Apply(new Dictionary<int, VENTARRANGE.RoomOccupancy>
                {
                    [office] = new VENTARRANGE.RoomOccupancy { Occupants = 10 },
                    [kitchen] = new VENTARRANGE.RoomOccupancy { Units = 2 }
                });

                Check(calc[kitchen].Air.Extract == 100 && calc[office].Air.Fresh == 40,
                      label + ": the kitchen's 2 units, 2 x 50 = " + calc[kitchen].Air.Extract + "; the office's change stands");

                calc = w.Apply(new Dictionary<int, VENTARRANGE.RoomOccupancy>
                {
                    [kitchen] = new VENTARRANGE.RoomOccupancy { Units = 2, Use = VENTOCCUPANCY.LightUse }
                });

                Check(calc[kitchen].Air.Extract == 50 && calc[office].Air.Fresh == 20 && calc[office].SheetCells == null,
                      label + ": light use, 2 x 25 = " + calc[kitchen].Air.Extract + "; the office's choice gone, it is the sheet's again (" +
                      calc[office].Air.Fresh + ")");

                // ---- a category, against the same change worked out in full
                string other = w.FreshChoices(office).First(c => c != "Office Space" && c.StartsWith("Conference", StringComparison.OrdinalIgnoreCase));

                calc = w.Apply(new Dictionary<int, VENTARRANGE.RoomOccupancy>
                {
                    [office] = new VENTARRANGE.RoomOccupancy { FreshCategory = other }
                });

                double full;

                using (var p = new ExcelPackage(new FileInfo(saved)))
                {
                    ExcelWorksheet ws = p.Workbook.Worksheets["Occ"];
                    ws.Cells[office, layout.FreshCategoryCol].Value = other;
                    p.Workbook.Calculate();
                    full = ws.Cells[office, layout.FreshAirCol].Value is double d ? d : -1;
                }

                Check(full > 0 && calc[office].Air.Fresh == full && calc[kitchen].Air.Extract == 50,
                      label + ": the office as '" + other + "': " + calc[office].Air.Fresh + " - as the whole workbook worked out works it: " + full);

                // ---- blank, and a category the table has not got
                calc = w.Apply(new Dictionary<int, VENTARRANGE.RoomOccupancy>
                {
                    [ensuite] = new VENTARRANGE.RoomOccupancy { ExhaustCategory = "" },
                    [office] = new VENTARRANGE.RoomOccupancy { FreshCategory = "Nonsense" }
                });

                Check(calc[ensuite].Air.Extract == 0 && calc[ensuite].ExtractError == null,
                      label + ": the ensuite's category blank - no exhaust (" + calc[ensuite].Air.Extract + ")");

                Check(calc[office].FreshError != null && calc[office].Air.Fresh == 0,
                      label + ": a category the table has not got - its error said: " + (calc[office].FreshError ?? "(none)"));

                // ---- every choice gone: the sheet's
                calc = w.Apply(none);

                Check(calc[office].Air.Fresh == 20 && calc[office].FreshError == null && calc[ensuite].Air.Extract == 10 &&
                      calc[kitchen].Air.Extract == 50,
                      label + ": every choice gone - the sheet's air again: " + calc[office].Air.Fresh + ", " + calc[ensuite].Air.Extract + ", " + calc[kitchen].Air.Extract);

                // ...and the copy's cells as the sheet had them: the office's
                // 10 people again come out as before
                calc = w.Apply(new Dictionary<int, VENTARRANGE.RoomOccupancy> { [office] = new VENTARRANGE.RoomOccupancy { Occupants = 10 } });

                Check(calc[office].Air.Fresh == 40, label + ": chosen again after a revert, worked out again - " + calc[office].Air.Fresh);
            }

            Check(File.ReadAllBytes(path).SequenceEqual(before) && File.GetLastWriteTimeUtc(path) == when,
                  label + ": the workbook on disk untouched");
        }

        // ---- a merged group: its air on its first row, worked out with it
        string merged = Path.Combine(Out, "merged.xlsx");

        VENTSHEET.sheetpath = merged;
        VENTSHEET.sheetname = "Merged";

        new VENTSHEET().excelsheet();

        VENTSHEET.AirData Row(string number) => VENTSHEET.LoadedRows.First(r => r.Key.RoomNumber == number).Value;

        int living = Row("121.125").Row, kitchenRow = Row("121.126").Row;

        string why;

        using (VENTWHATIF w = VENTWHATIF.Open(merged, "Merged", out why))
        {
            Dictionary<int, VENTFLOWS.Calc> before = w.Apply(none);

            Dictionary<int, VENTFLOWS.Calc> after = w.Apply(new Dictionary<int, VENTARRANGE.RoomOccupancy>
            {
                [kitchenRow] = new VENTARRANGE.RoomOccupancy { Occupants = 10 }
            });

            double full;

            using (var p = new ExcelPackage(new FileInfo(merged)))
            {
                ExcelWorksheet ws = p.Workbook.Worksheets["Merged"];
                ws.Cells[kitchenRow, w.Layout.OccupantsCol].Value = 10d;
                p.Workbook.Calculate();
                full = ws.Cells[living, w.Layout.FreshAirCol].Value is double d ? d : -1;
            }

            Check(after[living].Air.Fresh == full && full > before[living].Air.Fresh && after[kitchenRow].Air.Fresh == 0 &&
                  after[living].SheetCells == null,
                  "a merged group: 10 people in its second room move the total on its first row, " + before[living].Air.Fresh +
                  " -> " + after[living].Air.Fresh + " (in full: " + full + "); the first row's own columns as the sheet's");
        }

        // ---- "Dwelling Unit" mended in the copy, as an export mends it: a
        // sheet with the office's broken test (IF(O=$H$470,"NA",...))
        string broken = Path.Combine(Out, "occupancy-broken.xlsx");
        int bedroomRow, officeRow, densityCol = 0;

        using (var p = new ExcelPackage(new FileInfo(saved)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Occ"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            bedroomRow = RowOf(ws, layout, "505");
            officeRow = RowOf(ws, layout, "501");

            for (int c = 1; c <= ws.Dimension.End.Column; c++)
            {
                string f = ws.Cells[bedroomRow, c].Formula ?? "";

                if (f.Contains("\"NA\"") && f.Contains("Table 6-1"))
                    densityCol = c;
            }

            for (int r = layout.FirstDataRow; r <= ws.Dimension.End.Row; r++)
            {
                string f = ws.Cells[r, densityCol].Formula;

                if (!string.IsNullOrEmpty(f))
                    ws.Cells[r, densityCol].Formula = Regex.Replace(f, @"'ASHRAE 62\.1 - Table 6-1'!\$A\$\d+(?=,""NA"")", "$$H$$470");
            }

            p.SaveAs(new FileInfo(broken));
        }

        double unmended;

        using (var p = new ExcelPackage(new FileInfo(broken)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Occ"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            ws.Cells[officeRow, layout.FreshCategoryCol].Value = "Dwelling Unit";
            p.Workbook.Calculate();

            unmended = ws.Cells[officeRow, layout.FreshAirCol].Value is double d ? d : -1;
        }

        using (VENTWHATIF w = VENTWHATIF.Open(broken, "Occ", out why))
        {
            Dictionary<int, VENTFLOWS.Calc> calc = w.Apply(new Dictionary<int, VENTARRANGE.RoomOccupancy>
            {
                [officeRow] = new VENTARRANGE.RoomOccupancy { FreshCategory = "Dwelling Unit" }
            });

            // No one counted in it - a living room's case: 0.3 x 40 m2 = 12,
            // rounded up to 15
            Check(unmended < 0 && calc[officeRow].FreshError == null && calc[officeRow].Air.Fresh == 15,
                  "the office's broken Dwelling Unit test (" + (unmended < 0 ? "an error" : unmended.ToString()) + " as it stands) mended in the copy: " +
                  calc[officeRow].Air.Fresh + (calc[officeRow].FreshError == null ? "" : " " + calc[officeRow].FreshError));
        }
    }

    // A SHEET MADE BEFORE 2026-10-19, UPDATED - KIFAF 5's case: the kitchen
    // has its category and nothing else, Table 6-4's row for it is empty
    // and no formula names it, so it reads 0 L/s. The update must make it
    // per unit (KitchenCheck) and give it its unit.
    static void KitchenUpdateTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== the apartment kitchen of an old sheet, updated: per unit, as a private toilet");

        var levels = new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 05", 0,
              Dw(In("9:1", "1BDR", "A-01", "BEDROOM", "505", 14)), Dw(In("9:1", "1BDR", "A-01", "KITCHEN", "506", 8)),
              Dw(In("9:1", "1BDR", "A-01", "ENSUITE", "507", 5)))
        };

        string oldPath = Path.Combine(Out, "kitchen-old.xlsx");
        string outPath = Path.Combine(Out, "kitchen-updated.xlsx");

        VENTOFFICE.Job job = Job("Vent", levels, null);
        job.Occupancy = VENTOCCUPANCY.Defaults();
        VENTOFFICE.NewWorkbook(template, job, oldPath);

        double Exhaust(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, string number) =>
            ws.Cells[RowOf(ws, layout, number), layout.ExhaustAirCol].Value is double d ? d : -1;

        // Back to the office's workbook as it was: the table row emptied,
        // the kitchen taken out of every formula, its unit cleared
        using (var p = new ExcelPackage(new FileInfo(oldPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);
            ExcelWorksheet table = p.Workbook.Worksheets.First(w => w.Name.Contains("Table 6-4"));
            int kRow = Enumerable.Range(1, 60).First(r => table.Cells[r, 1].Text == VENTOCCUPANCY.ApartmentKitchen);

            foreach (int c in new[] { 2, 3, 4, 7, 8, 9 })
                table.Cells[kRow, c].Value = null;

            int undone = 0;

            foreach (ExcelRangeBase cell in ws.Cells[ws.Dimension.Address])
            {
                if (string.IsNullOrEmpty(cell.Formula))
                    continue;

                string f = Regex.Replace(cell.Formula, @",\(\$?[A-Z]+\$?\d+=""Residential kitchens- Apartments""\)", "");

                if (f != cell.Formula)
                {
                    cell.Formula = f;
                    undone++;
                }
            }

            ws.Cells[RowOf(ws, layout, "506"), layout.UnitsCol].Value = null;

            p.Workbook.Calculate();

            Check(undone >= 2 && Exhaust(ws, layout, "506") == 0,
                  "the old sheet: the kitchen at 0 L/s, as KIFAF 5's (" + undone + " formulas taken back)");

            p.Save();
        }

        VENTUPDATE.Outcome done;

        using (var p = new ExcelPackage(new FileInfo(oldPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];

            done = VENTUPDATE.Update(ws, VENTSHEET.DetectLayout(ws, false), levels, l => true,
                                     new List<string> { "LEVEL 05" }, "2026-10-19", VENTOCCUPANCY.Defaults());

            p.SaveAs(new FileInfo(outPath));
        }

        using (var p = new ExcelPackage(new FileInfo(outPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            p.Workbook.Calculate();

            Check(done.Kitchen.Rates && done.Kitchen.Formulas == 2,
                  "the update gave Table 6-4 its rates and the room formulas the kitchen (" + done.Kitchen.Formulas + ")");

            Check(ws.Cells[RowOf(ws, layout, "506"), layout.UnitsCol].Value is double u && u == 1 &&
                  Exhaust(ws, layout, "506") == 50,
                  "updated: the kitchen 1 unit x 50 L/s - " + Exhaust(ws, layout, "506"));

            Check(Exhaust(ws, layout, "507") == 10, "the ensuite as it was: 10 L/s - " + Exhaust(ws, layout, "507"));

            // The office then types its own rate into the row
            ExcelWorksheet table = p.Workbook.Worksheets.First(w => w.Name.Contains("Table 6-4"));
            int kRow = Enumerable.Range(1, 60).First(r => table.Cells[r, 1].Text == VENTOCCUPANCY.ApartmentKitchen);
            table.Cells[kRow, 9].Value = 60d;

            p.Save();
        }

        // Updated again: nothing to make, and the office's 60 stands
        string againPath = Path.Combine(Out, "kitchen-updated-again.xlsx");

        using (var p = new ExcelPackage(new FileInfo(outPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];

            done = VENTUPDATE.Update(ws, VENTSHEET.DetectLayout(ws, false), levels, l => true,
                                     new List<string> { "LEVEL 05" }, "2026-10-19", VENTOCCUPANCY.Defaults());

            p.SaveAs(new FileInfo(againPath));
        }

        using (var p = new ExcelPackage(new FileInfo(againPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            p.Workbook.Calculate();

            string af = ws.Cells[RowOf(ws, layout, "506"), layout.ExhaustCategoryCol + 3].Formula ?? "";

            Check(!done.Kitchen.Rates && done.Kitchen.Formulas == 0 &&
                  Regex.Matches(af, "Residential kitchens- Apartments").Count == 1,
                  "updated again: nothing made twice (" + done.Kitchen.Formulas + ")");

            Check(Exhaust(ws, layout, "506") == 60, "a rate typed into the row stands: 60 L/s - " + Exhaust(ws, layout, "506"));
        }
    }

    static void OccupancyUpdateTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== occupancy: an update follows the sheet's own choices");

        var before = new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 01", 0, Rm("9:1", "OFFICE", "101", 20), Rm("9:2", "OFFICE", "102", 20), Rm("9:3", "STORE", "103", 5))
        };

        string basePath = Path.Combine(Out, "occupancy-update-base.xlsx");
        string typedPath = Path.Combine(Out, "occupancy-update-typed.xlsx");

        VENTOFFICE.NewWorkbook(template, Job("Vent", before, null), basePath);

        using (var p = new ExcelPackage(new FileInfo(basePath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            Check(ws.Cells[RowOf(ws, layout, "101"), Occ].Text == "", "with the categories switched off, none is filled");

            // The engineer's own choice for this project's offices
            ws.Cells[RowOf(ws, layout, "101"), Occ].Value = "Reception Areas";
            p.SaveAs(new FileInfo(typedPath));
        }

        var after = new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 01", 0, Rm("9:1", "OFFICE", "101", 20), Rm("9:2", "OFFICE", "102", 20), Rm("9:3", "STORE", "103", 5),
                             Rm("9:4", "OFFICE", "104", 20), Rm("9:5", "MEETING", "105", 15))
        };

        string outPath = Path.Combine(Out, "occupancy-update-out.xlsx");
        VENTUPDATE.Outcome done;

        using (var p = new ExcelPackage(new FileInfo(typedPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];

            done = VENTUPDATE.Update(ws, VENTSHEET.DetectLayout(ws, false), after, l => true,
                                     new List<string> { "LEVEL 01" }, "2026-10-14", VENTOCCUPANCY.Defaults());

            p.SaveAs(new FileInfo(outPath));
        }

        using (var p = new ExcelPackage(new FileInfo(outPath)))
        {
            ExcelWorksheet ws = p.Workbook.Worksheets["Vent"];
            VENTSHEET.SheetLayout layout = VENTSHEET.DetectLayout(ws, false);

            string Cat(string number) => ws.Cells[RowOf(ws, layout, number), Occ].Text;

            Check(Cat("101") == "Reception Areas", "a category somebody chose stays");

            Check(Cat("102") == "Reception Areas" && Cat("104") == "Reception Areas",
                  "offices left blank, and a new one, follow what the sheet chose for OFFICE - not the rule's Office Space");

            Check(Cat("105") == "Conference / Meeting" && Cat("103") == "Occupiable Storage Rooms for dry material",
                  "names the sheet has not decided: the rules");

            Check(done.Occupancy.Fresh == 4, "4 filled (" + done.Occupancy.Fresh + ")");
        }
    }

    // ==================================================
    // Rooms of one name and number; a workbook never saved in Excel
    // ==================================================

    // User's list (2026-10-15): KIFAF has MASTER ENSUITE 121.141 and two
    // others twice - two apartments built from one interior model - and
    // both rooms took one row's air. And a sheet AUTOVENT wrote, read back
    // before Excel ever saved it, read every room's air as 0.
    // A room's row, or an empty one - a missing room fails a check
    // rather than ending the run
    static VENTSHEET.AirData Row(Dictionary<string, VENTSHEET.AirData> airOf, string room) =>
        airOf.TryGetValue(room, out VENTSHEET.AirData air) ? air : new VENTSHEET.AirData { FreshAir = -1 };

    static void TwinsTest(byte[] template)
    {
        Console.WriteLine();
        Console.WriteLine("== two rooms of one name and number; formulas with no saved results");

        VENTTEMPLATE.Room Twin(string key, string apartment, string aptNumber, double area)
        {
            VENTTEMPLATE.Room room = Dw(In(apartment, "2BDR", aptNumber, "BEDROOM", "121.143", area));
            room.Key = key;
            return room;
        }

        var levels = new List<VENTTEMPLATE.Level>
        {
            L("LEVEL 04A", 0, Twin("21:143", "9:1", "A-01", 14), Twin("22:143", "9:2", "A-02", 40))
        };

        VENTOFFICE.Job job = Job("Twins", levels, null);
        job.Occupancy = VENTOCCUPANCY.Defaults();

        string path = Path.Combine(Out, "twins.xlsx");

        VENTOFFICE.NewWorkbook(template, job, path);

        // ---- read straight back: nobody has opened it in Excel
        VENTSHEET.sheetpath = path;
        VENTSHEET.sheetname = "Twins";

        Dictionary<VENTSHEET.SpaceKey, VENTSHEET.AirData> read = new VENTSHEET().excelsheet();

        Check(VENTSHEET.Calculated && VENTSHEET.LoadedRows.Count == 2 && VENTSHEET.LoadedRows.All(r => r.Value.FreshAir > 0),
              "a sheet with formulas and no saved results is worked out on reading - not 0 L/s: " +
              string.Join(", ", VENTSHEET.LoadedRows.Select(r => r.Value.FreshAir)));

        Check(read.Count == 1 && VENTSHEET.LoadedRows.Count == 2,
              "both rows are read, though they share a name, number and level");

        var key = new VENTSHEET.SpaceKey { SpaceName = "BEDROOM", RoomNumber = "121.143", Level = "LEVEL 04A" };

        var rooms = new List<KeyValuePair<string, VENTSHEET.SpaceKey>>
        {
            new KeyValuePair<string, VENTSHEET.SpaceKey>("21:143", key),
            new KeyValuePair<string, VENTSHEET.SpaceKey>("22:143", key)
        };

        Dictionary<string, VENTSHEET.AirData> airOf = VENTSHEET.Resolve(VENTSHEET.LoadedRows, rooms, out List<string> shared);

        Check(Row(airOf, "21:143").FreshAir == 10 && Row(airOf, "22:143").FreshAir == 20 && shared.Count == 0,
              "each room takes its own row, by the model ID: " + Row(airOf, "21:143").FreshAir + " and " + Row(airOf, "22:143").FreshAir);

        // ---- the same sheet without the IDs (one written before them)
        var bare = VENTSHEET.LoadedRows
            .Select(r => new KeyValuePair<VENTSHEET.SpaceKey, VENTSHEET.AirData>(r.Key, new VENTSHEET.AirData
            {
                FreshAir = r.Value.FreshAir, ExhaustAir = r.Value.ExhaustAir, Id = string.Empty
            }))
            .ToList();

        airOf = VENTSHEET.Resolve(bare, rooms, out shared);

        Check(Row(airOf, "21:143").Id != null && Row(airOf, "21:143") != Row(airOf, "22:143") && shared.Count == 2,
              "with no IDs: one row each, not one row for both - and both said to be a guess");

        // ---- a row whose ID is another room's is never taken by name
        var stale = new List<KeyValuePair<string, VENTSHEET.SpaceKey>>
        {
            new KeyValuePair<string, VENTSHEET.SpaceKey>("21:143", key),
            new KeyValuePair<string, VENTSHEET.SpaceKey>("30:143", key)
        };

        airOf = VENTSHEET.Resolve(VENTSHEET.LoadedRows, stale, out shared);

        Check(Row(airOf, "21:143").Id == "21:143" && Row(airOf, "30:143").Id == "22:143",
              "a room whose ID the sheet has not got (a link loaded again) takes the row no model room claims");

        // ---- saved with results: read as Excel left them, not worked out
        string saved = Path.Combine(Out, "twins-saved.xlsx");

        using (var p = new ExcelPackage(new FileInfo(path)))
        {
            p.Workbook.Calculate();
            p.SaveAs(new FileInfo(saved));
        }

        VENTSHEET.sheetpath = saved;
        new VENTSHEET().excelsheet();

        Check(!VENTSHEET.Calculated, "a sheet saved with its results is read as it is");
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

    // The office's occupancy "NA" test (IF(O=$H$363,"NA",...)) compares the
    // category with a cell meant to hold "Dwelling Unit" - and in the
    // office's own workbook that $H$ points tens of rows away from it, in
    // every pattern. Since 2026-10-14 every export points it at Table
    // 6-1's "Dwelling Unit" cell itself (VENTOCCUPANCY.DwellingCheck).
    static void AbsoluteCheck(ExcelWorksheet ws, byte[] template, string pattern, int firstRoom, int total)
    {
        using (var src = new ExcelPackage(new MemoryStream(template)))
        {
            ExcelWorksheet o = src.Workbook.Worksheets[pattern];
            ExcelWorksheet table = src.Workbook.Worksheets["ASHRAE 62.1 - Table 6-1"];

            int dwelling = Enumerable.Range(1, 200).First(r => table.Cells[r, 1].Text.Trim() == "Dwelling Unit");

            // What the office's own sheet points at, for the record
            Match theirs = Regex.Match(o.Cells[12, 17].Formula ?? "", @"\$H\$(\d+)");
            string pointed = theirs.Success ? o.Cells["H" + theirs.Groups[1].Value].Text : "(none)";

            string formula = ws.Cells[firstRoom, 18].Formula ?? "";

            Check(formula.Contains("'ASHRAE 62.1 - Table 6-1'!$A$" + dwelling + ",\"NA\"") && !Regex.IsMatch(formula, @"=\$H\$\d+,""NA"""),
                  "the occupancy test reads Table 6-1's \"Dwelling Unit\" (A" + dwelling + ") - the office's $H$ pointed at [" +
                  pointed + "]: " + Short(formula));
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

            // Every existing sheet is untouched, formula for formula - but
            // for Table 6-4's EMPTY apartment kitchen row, given its rates
            // so the new worksheet's kitchens are not 0 L/s (2026-10-19)
            bool same = true;
            string where = null;
            var kitchenCells = new HashSet<string>();

            foreach (var old in before.Workbook.Worksheets)
            {
                var now = p.Workbook.Worksheets[old.Name];

                if (old.Dimension == null) continue;

                if (old.Name.Contains("Table 6-4"))
                {
                    int kRow = Enumerable.Range(1, 60).FirstOrDefault(r => old.Cells[r, 1].Text == VENTOCCUPANCY.ApartmentKitchen);

                    foreach (int c in new[] { 2, 3, 4, 7, 8, 9 })
                    {
                        if (kRow > 0 && old.Cells[kRow, c].Text == "")
                            kitchenCells.Add(old.Name + "!" + old.Cells[kRow, c].Address);
                    }

                    Check(result.Kitchen.Rates && kRow > 0 && now.Cells[kRow, 9].Value is double hi && hi == 50,
                          "its own Table 6-4's empty apartment kitchen row given the rates, and said");
                }

                foreach (var cell in old.Cells[old.Dimension.Address])
                {
                    if (kitchenCells.Contains(old.Name + "!" + cell.Address))
                        continue;

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
