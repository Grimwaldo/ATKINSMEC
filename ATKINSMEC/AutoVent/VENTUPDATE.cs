using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ATKINSMEC
{
    // "UPDATE THE IMPORTED WORKSHEET" - at the user's request (2026-10-14):
    // the sheet is the engineer's, so an export into it ADDS and UPDATES and
    // never throws away what was typed. It was "fill", which cleared every
    // input in every room row and dropped every level not ticked.
    //
    //   - every room the model has on a TICKED level is matched to its row:
    //     by the hidden model ID column first (KeyHeading - written by every
    //     export, so from the second time on a room renamed AND renumbered is
    //     still found), then by its number on the same level, then by a name
    //     that is the only one of its kind on that level on both sides
    //     (Match). A matched row keeps everything typed in it; its name,
    //     number, area, height and units come from the model.
    //   - a room with no row is ADDED, with the inputs blank; a ticked level
    //     the sheet has not got is added where the model's order puts it.
    //   - a row whose room is no longer in the model is KEPT, greyed, with a
    //     note - the user chose that over deleting it.
    //   - a level NOT ticked is left exactly as it is; a room unticked in the
    //     levels list keeps its row as it is and is never added (Room.Keep).
    //   - the rows go in the model's order (Arrange rooms...), each level's
    //     rooms first and its removed rooms after them.
    //
    // The block grows and shrinks from INSIDE its room rows, so the ranges
    // the summary tables under it read ($AB$12:$AB$16) grow with it, and the
    // Total is rebuilt from every Sub Total, as VENTOFFICE does.
    //
    // Revit-free: tests\officetest runs it on real workbooks.
    internal static class VENTUPDATE
    {
        // The hidden column every export writes each room's model ID in
        public const string KeyHeading = "AUTOVENT model ID";

        // Who wrote the "not in the model" note - and so whose note it is
        // to replace
        private const string Author = "AUTOVENT";

        public class Outcome
        {
            public int Matched;
            public int Changed;
            public int Added;
            public int Gone;
            public int Kept;
            public int LevelsAdded;
            public int LevelsLeft;
            public int Apartments;
            public int Merged;
            public int RowsBefore;
            public int RowsAfter;
            public bool NumberColumnAdded;
            public bool KeyColumnAdded;
            public bool FilterRemoved;

            // Occupancy categories filled into blank cells (VENTOCCUPANCY)
            public VENTOCCUPANCY.Filled Occupancy = new VENTOCCUPANCY.Filled();

            // Room formulas whose "Dwelling Unit" test was mended
            public int DwellingFixed;

            // The apartment kitchen made per unit: rates and formulas changed
            public VENTOCCUPANCY.Kitchen Kitchen = new VENTOCCUPANCY.Kitchen();

            // One line per room added, changed or no longer in the model
            public List<string> Lines = new List<string>();
        }

        // ==================================================
        // Matching - neither Revit nor Excel
        // ==================================================

        public class Candidate
        {
            public string Level = string.Empty;
            public string Key = string.Empty;
            public string Number = string.Empty;
            public string Name = string.Empty;
        }

        // For each model room, the sheet row it is - an index into `sheet`,
        // or -1. Each row is claimed once. The model ID first, anywhere in
        // the sheet; then the number on the same level (the same name
        // preferred); then a name only one room has on that level, in the
        // sheet AND in the model - "BEDROOM" forty times over is no match.
        public static int[] Match(IList<Candidate> sheet, IList<Candidate> model)
        {
            sheet = sheet ?? new List<Candidate>();
            model = model ?? new List<Candidate>();

            int[] found = Enumerable.Repeat(-1, model.Count).ToArray();
            var taken = new bool[sheet.Count];

            var byKey = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < sheet.Count; i++)
            {
                string key = Clean(sheet[i].Key);

                if (key.Length > 0 && !byKey.ContainsKey(key))
                    byKey[key] = i;
            }

            for (int m = 0; m < model.Count; m++)
            {
                string key = Clean(model[m].Key);
                int i;

                if (key.Length > 0 && byKey.TryGetValue(key, out i) && !taken[i])
                {
                    found[m] = i;
                    taken[i] = true;
                }
            }

            for (int m = 0; m < model.Count; m++)
            {
                if (found[m] >= 0 || Clean(model[m].Number).Length == 0)
                    continue;

                int best = -1;

                for (int i = 0; i < sheet.Count; i++)
                {
                    if (taken[i] || !Same(sheet[i].Level, model[m].Level) ||
                        !Same(sheet[i].Number, model[m].Number))
                        continue;

                    if (best < 0)
                        best = i;

                    if (Same(sheet[i].Name, model[m].Name))
                    {
                        best = i;
                        break;
                    }
                }

                if (best >= 0)
                {
                    found[m] = best;
                    taken[best] = true;
                }
            }

            for (int m = 0; m < model.Count; m++)
            {
                if (found[m] >= 0 || Clean(model[m].Name).Length == 0)
                    continue;

                List<int> rows = Enumerable.Range(0, sheet.Count)
                    .Where(i => !taken[i] && Same(sheet[i].Level, model[m].Level) &&
                                Same(sheet[i].Name, model[m].Name))
                    .ToList();

                int rooms = Enumerable.Range(0, model.Count)
                    .Count(k => found[k] < 0 && Same(model[k].Level, model[m].Level) &&
                                Same(model[k].Name, model[m].Name));

                if (rows.Count == 1 && rooms == 1)
                {
                    found[m] = rows[0];
                    taken[rows[0]] = true;
                }
            }

            return found;
        }

        // WHERE THE NEW LEVELS GO among the sheet's: after the last sheet
        // level below each in the model's order, the sheet's own order
        // untouched. Returns slots: n >= 0 is the sheet's n-th level, -1 -
        // n is the n-th new one. A sheet level the model does not know
        // (null, or not in `storeys`) only keeps its place.
        public static List<int> Place(IList<string> sheet, IList<string> added, IList<string> storeys)
        {
            sheet = sheet ?? new List<string>();
            added = added ?? new List<string>();
            storeys = storeys ?? new List<string>();

            Func<string, int> at = name =>
            {
                for (int i = 0; i < storeys.Count; i++)
                {
                    if (name != null && Same(storeys[i], name))
                        return i;
                }

                return -1;
            };

            var slots = Enumerable.Range(0, sheet.Count).ToList();

            foreach (int n in Enumerable.Range(0, added.Count).OrderBy(n => at(added[n])))
            {
                int mine = at(added[n]);
                int insert = 0;

                for (int s = 0; s < slots.Count; s++)
                {
                    string name = slots[s] >= 0 ? sheet[slots[s]] : added[-1 - slots[s]];
                    int theirs = at(name);

                    if (theirs >= 0 && theirs < mine)
                        insert = s + 1;
                }

                slots.Insert(insert, -1 - n);
            }

            return slots;
        }

        private static string Clean(string text)
        {
            return (text ?? string.Empty).Trim();
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);
        }

        // ==================================================
        // The model ID column
        // ==================================================

        // The column headed KeyHeading, on the header rows; or, asked to,
        // a new one past the last used column, hidden. 0 when there is
        // none and none was asked for.
        public static int KeyColumn(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, bool create, out bool made)
        {
            made = false;

            int lastCol = ws.Dimension == null ? 0 : ws.Dimension.End.Column;
            int top = Math.Max(1, layout.HeaderRow);
            int bottom = Math.Max(top, layout.HeaderEndRow);

            for (int c = 1; c <= lastCol; c++)
            {
                for (int r = top; r <= bottom; r++)
                {
                    if (string.Equals(ws.Cells[r, c].Text.Trim(), KeyHeading, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
            }

            if (!create)
                return 0;

            int col = lastCol + 1;

            ws.Cells[top, col].Value = KeyHeading;
            ws.Column(col).Hidden = true;

            made = true;

            return col;
        }

        // Every room's model ID beside it - what the next update finds it by
        public static void WriteKeys(
            ExcelWorksheet ws, VENTSHEET.SheetLayout layout,
            IList<KeyValuePair<int, VENTTEMPLATE.Room>> rows)
        {
            if (rows == null || rows.All(r => r.Value == null || string.IsNullOrEmpty(r.Value.Key)))
                return;

            bool made;
            int col = KeyColumn(ws, layout, true, out made);

            foreach (KeyValuePair<int, VENTTEMPLATE.Room> row in rows)
            {
                if (row.Value != null && !string.IsNullOrEmpty(row.Value.Key))
                    ws.Cells[row.Key, col].Value = row.Value.Key;
            }
        }

        // ==================================================
        // The worksheet as it is
        // ==================================================

        private enum Kind { Level, Room, SubTotal, Other }

        private class Cell
        {
            public int Style;
            public object Value;
            public string R1C1;
            public string Comment;
            public string Author;
        }

        private class Old
        {
            public int Row;
            public Kind Kind;
            public string Name = string.Empty;
            public string Number = string.Empty;
            public string Key = string.Empty;
            public double? Area;
            public double? Height;
            public double RowHeight;
            public bool CustomHeight;

            // A room of an apartment: its own name in the Space block's last
            // column, the apartment's in the first
            public bool InApartment;

            // Its fresh air / extract cell was part of a merged group's
            // total (VENTROWS.MergeAir): never carried, the formula is
            // the room's own again and the group is merged afresh
            public bool FreshDown;
            public bool ExhaustDown;

            // ...but a total TYPED over such a group is the user's, and
            // goes back on the merged cell once the group is merged again

            public Cell[] Cells;
        }

        private class Section
        {
            public string Level;          // null: rows before the first level
            public bool Update;           // ticked - and the first of its name
            public Old LevelRow;
            public List<Old> All = new List<Old>();
            public List<Old> Head = new List<Old>();
            public List<Old> Rooms = new List<Old>();
            public bool HasSubTotal;
            public List<Old> Tail = new List<Old>();
        }

        private enum Make { Copy, Level, Spacer, Room, Gone, SubTotal }

        private class Planned
        {
            public Make Make;
            public Old Old;
            public VENTTEMPLATE.Room Room;
            public string Level;
            public int BlockFirst;
            public int BlockLast;
        }

        // ==================================================
        // The update
        // ==================================================

        public static Outcome Update(
            ExcelWorksheet ws,
            VENTSHEET.SheetLayout layout,
            IList<VENTTEMPLATE.Level> blocks,
            Func<string, bool> ticked,
            IList<string> storeys,
            string today,
            IList<VENTOCCUPANCY.Rule> occupancy = null)
        {
            var outcome = new Outcome();

            blocks = blocks ?? new List<VENTTEMPLATE.Level>();
            ticked = ticked ?? (level => true);

            // A sheet with no room number column gets one, as before
            if (layout.RoomNumberCol == 0)
            {
                int insertedAt = VENTROWS.InsertRoomNumberColumn(ws, layout);
                VENTROWS.ShiftColumns(layout, insertedAt);
                outcome.NumberColumnAdded = true;
            }

            bool keyMade;
            int keyCol = KeyColumn(ws, layout, true, out keyMade);
            outcome.KeyColumnAdded = keyMade;

            int lastCol = ws.Dimension.End.Column;
            int nameCol = layout.SpaceNameCol;
            int first = layout.FirstDataRow;
            int last = BlockEnd(ws, layout);

            if (last < first)
                throw new Exception("The worksheet has no level/room rows to update.");

            // ---- 1. every row as it is, before anything moves
            List<Old> rows = Read(ws, layout, first, last, lastCol, keyCol);

            var merges = ws.MergedCells
                .Where(m => !string.IsNullOrEmpty(m))
                .Select(m => new ExcelAddress(m))
                .Where(a => a.End.Row >= first && a.Start.Row <= last)
                .ToList();

            // ---- 2. the rows every new row is made from
            Old levelOld = rows.FirstOrDefault(r => r.Kind == Kind.Level);

            Old roomOld = rows.FirstOrDefault(r => r.Kind == Kind.Room && !r.FreshDown && !r.ExhaustDown &&
                                                   !Down(merges, r.Row, nameCol))
                          ?? rows.FirstOrDefault(r => r.Kind == Kind.Room);

            if (levelOld == null || roomOld == null)
                throw new Exception("Could not find a level row and a room row to use as templates.");

            Old spacerOld = rows.FirstOrDefault(r => r.Row == levelOld.Row + 1 && r.Kind == Kind.Other);
            Old subOld = rows.FirstOrDefault(r => r.Kind == Kind.SubTotal);

            VENTROWS.RowTemplate levelTemplate = VENTROWS.Capture(ws, levelOld.Row, lastCol, nameCol);
            VENTROWS.RowTemplate roomTemplate = VENTROWS.Capture(ws, roomOld.Row, lastCol, nameCol);
            VENTROWS.RowTemplate spacerTemplate = spacerOld == null ? null : VENTROWS.Capture(ws, spacerOld.Row, lastCol, nameCol);
            VENTROWS.RowTemplate subTemplate = subOld == null ? null : VENTROWS.Capture(ws, subOld.Row, lastCol, nameCol);

            // The office's "Dwelling Unit" test, pointed at Table 6-1's own
            // cell rather than a row that is not it (VENTOCCUPANCY)
            outcome.DwellingFixed = VENTOCCUPANCY.DwellingCheck(ws.Workbook, roomTemplate.FormulasR1C1);
            outcome.Kitchen = VENTOCCUPANCY.KitchenCheck(ws.Workbook, roomTemplate.FormulasR1C1);

            List<Section> sections = Sections(rows, ticked);

            int tmplFirst = 0, tmplLast = 0;

            if (subOld != null)
            {
                Section home = sections.First(s => s.All.Contains(subOld));

                if (home.Rooms.Count > 0)
                {
                    tmplFirst = home.Rooms.First().Row;
                    tmplLast = home.Rooms.Last().Row;
                }
            }

            // ---- 3. which row is which room
            var sheetRooms = sections.Where(s => s.Update)
                                     .SelectMany(s => s.Rooms.Select(r => new { Section = s, Row = r }))
                                     .ToList();

            var modelRooms = blocks.Where(b => b != null && ticked(b.LevelName))
                                   .SelectMany(b => (b.Rooms ?? new List<VENTTEMPLATE.Room>())
                                                    .Select(r => new { Level = b.LevelName, Room = r }))
                                   .ToList();

            int[] found = Match(
                sheetRooms.Select(x => new Candidate
                {
                    Level = x.Section.Level, Key = x.Row.Key, Number = x.Row.Number, Name = x.Row.Name
                }).ToList(),
                modelRooms.Select(x => new Candidate
                {
                    Level = x.Level, Key = x.Room.Key, Number = x.Room.Number, Name = x.Room.Name
                }).ToList());

            var oldOf = new Dictionary<VENTTEMPLATE.Room, Old>();
            var claimed = new HashSet<Old>();

            for (int m = 0; m < modelRooms.Count; m++)
            {
                if (found[m] < 0)
                    continue;

                oldOf[modelRooms[m].Room] = sheetRooms[found[m]].Row;
                claimed.Add(sheetRooms[found[m]].Row);
            }

            // ---- 4. the new block, row by row
            var plan = new List<Planned>();

            List<string> added = blocks
                .Where(b => b != null && ticked(b.LevelName) &&
                            !sections.Any(s => Same(s.Level, b.LevelName)) &&
                            (b.Rooms ?? new List<VENTTEMPLATE.Room>()).Any(r => !r.Keep || oldOf.ContainsKey(r)))
                .Select(b => b.LevelName)
                .ToList();

            Func<string, VENTTEMPLATE.Level> blockOf = name =>
                blocks.FirstOrDefault(b => b != null && Same(b.LevelName, name));

            foreach (int slot in Place(sections.Select(s => s.Level).ToList(), added, storeys))
            {
                if (slot >= 0)
                {
                    Section section = sections[slot];

                    if (!section.Update)
                    {
                        foreach (Old row in section.All)
                            plan.Add(new Planned { Make = Make.Copy, Old = row });

                        if (section.Level != null)
                            outcome.LevelsLeft++;

                        continue;
                    }

                    plan.Add(new Planned { Make = Make.Copy, Old = section.LevelRow });

                    foreach (Old row in section.Head)
                        plan.Add(new Planned { Make = Make.Copy, Old = row });

                    int from = plan.Count;

                    Rooms(plan, blockOf(section.Level), oldOf, outcome, section.Level);

                    foreach (Old row in section.Rooms.Where(r => !claimed.Contains(r)))
                    {
                        plan.Add(new Planned { Make = Make.Gone, Old = row });

                        outcome.Gone++;
                        outcome.Lines.Add(section.Level + "   " + Label(row.Number, row.Name) +
                                          " - not in the model, greyed");
                    }

                    SubTotal(plan, subTemplate, from, first);

                    foreach (Old row in section.Tail)
                        plan.Add(new Planned { Make = Make.Copy, Old = row });
                }
                else
                {
                    string level = added[-1 - slot];

                    plan.Add(new Planned { Make = Make.Level, Level = level });

                    if (spacerTemplate != null)
                        plan.Add(new Planned { Make = Make.Spacer });

                    int from = plan.Count;

                    Rooms(plan, blockOf(level), oldOf, outcome, level);
                    SubTotal(plan, subTemplate, from, first);

                    outcome.LevelsAdded++;
                }
            }

            // ---- 5. the block resized - from inside its room rows
            int existing = last - first + 1;
            int needed = plan.Count;
            int delta = needed - existing;

            outcome.RowsBefore = existing;
            outcome.RowsAfter = needed;

            foreach (ExcelAddress merge in merges)
            {
                try { ws.Cells[merge.Address].Merge = false; }
                catch { /* already gone */ }
            }

            RemoveComments(ws, first, last, lastCol);

            List<Old> roomRowsBefore = rows.Where(r => r.Kind == Kind.Room).ToList();
            int oldFirstRoom = roomRowsBefore.Count > 0 ? roomRowsBefore.First().Row : 0;
            int oldLastRoom = roomRowsBefore.Count > 0 ? roomRowsBefore.Last().Row : 0;

            // What the summary tables' room range became, if it followed
            int[] followed = null;

            if (delta > 0)
            {
                bool inside = oldFirstRoom > 0 && oldLastRoom > oldFirstRoom;

                ws.InsertRow(inside ? oldLastRoom : last + 1, delta);

                if (oldFirstRoom > 0)
                    followed = new[] { oldFirstRoom, inside ? oldLastRoom + delta : oldLastRoom };
            }
            else if (delta < 0)
            {
                int k = -delta;

                if (oldFirstRoom > 0 && oldFirstRoom + k <= oldLastRoom)
                {
                    ws.DeleteRow(oldFirstRoom + 1, k);
                    followed = new[] { oldFirstRoom, oldLastRoom - k };
                }
                else
                {
                    ws.DeleteRow(first + needed, k);
                }
            }
            else if (oldFirstRoom > 0)
            {
                followed = new[] { oldFirstRoom, oldLastRoom };
            }

            // The templates and rows were read before the resize: whatever
            // they name BELOW the block has moved by delta since, as the
            // office exports (VENTOFFICE) already allow for
            Func<string, string> moved = f => VENTOFFICE.Refs.ShiftR1C1(f, last, delta);

            foreach (VENTROWS.RowTemplate t in new[] { levelTemplate, roomTemplate, spacerTemplate, subTemplate })
            {
                if (t == null)
                    continue;

                for (int c = 1; c < t.FormulasR1C1.Length; c++)
                    t.FormulasR1C1[c] = moved(t.FormulasR1C1[c]);
            }

            // ---- 6. every row written
            var rowOf = new Dictionary<Old, int>();
            var roomRows = new List<KeyValuePair<int, VENTTEMPLATE.Room>>();
            var subRows = new List<int>();
            var typedTotals = new List<Tuple<int, int, object>>();

            for (int i = 0; i < plan.Count; i++)
            {
                Planned p = plan[i];
                int r = first + i;

                switch (p.Make)
                {
                    case Make.Copy:
                    case Make.Gone:
                        Put(ws, r, p.Old, lastCol, moved);
                        rowOf[p.Old] = r;

                        if (p.Old.Kind == Kind.SubTotal)
                            subRows.Add(r);

                        break;

                    case Make.Level:
                        VENTROWS.ApplyTemplate(ws, r, levelTemplate, lastCol);
                        ws.Cells[r, nameCol].Value = p.Level;
                        break;

                    case Make.Spacer:
                        VENTROWS.ApplyTemplate(ws, r, spacerTemplate, lastCol);
                        break;

                    case Make.SubTotal:
                        VENTROWS.ApplyTemplate(ws, r, subTemplate, lastCol);

                        VENTROWS.ApplySubTotalFormulas(
                            ws, r, subTemplate, lastCol,
                            subOld.Row, tmplFirst, tmplLast,
                            p.BlockFirst, p.BlockLast);

                        ws.Cells[r, nameCol].Value = "Sub Total";
                        subRows.Add(r);
                        break;

                    case Make.Room:
                        VENTROWS.ApplyTemplate(ws, r, roomTemplate, lastCol);
                        Fill(ws, layout, r, p, roomTemplate, lastCol, keyCol, typedTotals, moved);

                        if (p.Old != null)
                            rowOf[p.Old] = r;

                        roomRows.Add(new KeyValuePair<int, VENTTEMPLATE.Room>(r, p.Room));
                        break;
                }
            }

            // Merges among the rows carried as they were - a level's name
            // across the Space block, an untouched level's apartments
            foreach (ExcelAddress merge in merges)
            {
                bool carried = true;
                int top = 0;

                for (int r = merge.Start.Row; r <= merge.End.Row && carried; r++)
                {
                    Old row = rows.FirstOrDefault(o => o.Row == r);
                    int now;

                    if (row == null || !rowOf.TryGetValue(row, out now) ||
                        plan[now - first].Make == Make.Room)
                    {
                        carried = false;
                        break;
                    }

                    if (r == merge.Start.Row)
                        top = now;
                    else if (now != top + (r - merge.Start.Row))
                        carried = false;
                }

                if (!carried)
                    continue;

                try
                {
                    ws.Cells[top, merge.Start.Column, top + merge.End.Row - merge.Start.Row, merge.End.Column].Merge = true;
                }
                catch
                {
                    // a merge that cannot come back costs only its look
                }
            }

            // ---- 7. apartments and merged rooms, as every export does
            outcome.Apartments = VENTROWS.Apartments(ws, layout, roomRows);
            outcome.Merged = VENTROWS.MergeAir(ws, layout, roomRows);

            // A group total typed by hand, back on its merged cell - or on
            // the row, if the group is merged no more. Not on a row that is
            // now under another room's total.
            foreach (Tuple<int, int, object> typed in typedTotals)
            {
                string merged = ws.MergedCells[typed.Item1, typed.Item2];

                if (merged != null && new ExcelAddress(merged).Start.Row != typed.Item1)
                    continue;

                ws.Cells[typed.Item1, typed.Item2].Formula = null;
                ws.Cells[typed.Item1, typed.Item2].Value = typed.Item3;
            }

            // ---- 7b. occupancy categories into blank cells - what the sheet
            // already gave rooms of that name first, then the rules
            if (occupancy != null)
            {
                VENTOCCUPANCY.Learned learned = VENTOCCUPANCY.Learned.From(
                    rows.Where(o => o.Kind == Kind.Room).Select(o => Tuple.Create(
                        o.Name, o.InApartment,
                        TextOf(o, layout.FreshCategoryCol), TextOf(o, layout.ExhaustCategoryCol))));

                outcome.Occupancy = VENTOCCUPANCY.Fill(ws, layout, roomRows, occupancy, learned);

                VENTOCCUPANCY.Dwellings(ws, layout, roomRows, outcome.Occupancy);
            }

            // ---- 8. notes back where they were; removed rooms greyed
            foreach (KeyValuePair<Old, int> placed in rowOf)
            {
                bool gone = plan[placed.Value - first].Make == Make.Gone;

                for (int c = 1; c <= lastCol; c++)
                {
                    Cell cell = placed.Key.Cells[c];

                    if (cell.Comment == null || cell.Author == Author)
                        continue;

                    AddComment(ws, placed.Value, c, cell.Comment, cell.Author);
                }

                if (!gone)
                    continue;

                try
                {
                    ws.Cells[placed.Value, 1, placed.Value, lastCol].Style.Font.Italic = true;
                    ws.Cells[placed.Value, 1, placed.Value, lastCol].Style.Font.Color.SetColor(255, 140, 140, 140);
                }
                catch
                {
                    // the note still says it
                }

                if (placed.Key.Cells[nameCol].Comment == null || placed.Key.Cells[nameCol].Author == Author)
                    AddComment(ws, placed.Value, nameCol, "Not in the model (" + today + ").", Author);
            }

            // ---- 9. the summary tables' range, and the Total
            int newEnd = first + needed - 1;

            List<int> newRoomRows = Enumerable.Range(0, plan.Count)
                .Where(i => plan[i].Make == Make.Room || plan[i].Make == Make.Gone ||
                            (plan[i].Make == Make.Copy && plan[i].Old.Kind == Kind.Room))
                .Select(i => first + i)
                .ToList();

            // The room range as it followed the resize - or as it stood, had
            // it not - pointed at the rooms now
            if (followed != null && newRoomRows.Count > 0)
            {
                int a1 = newRoomRows.First(), b1 = newRoomRows.Last();

                Func<int, int, bool> wasRooms = (a, b) =>
                    (a == followed[0] && b == followed[1]) ||
                    (a == oldFirstRoom && b == oldLastRoom);

                if (!(followed[0] == a1 && followed[1] == b1))
                {
                    foreach (ExcelRangeBase cell in ws.Cells[newEnd + 1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column])
                    {
                        string formula = cell.Formula;

                        if (string.IsNullOrEmpty(formula))
                            continue;

                        string mapped = VENTOFFICE.Refs.MapAreas(formula, ws.Name,
                            (a, b) => wasRooms(a, b) ? new[] { a1, b1 } : null);

                        if (mapped != formula)
                            cell.Formula = mapped;
                    }
                }
            }

            if (TotalLabel(ws, layout, newEnd + 1))
                VENTOFFICE.TotalOfSubTotals(ws, newEnd + 1, lastCol, subRows);

            outcome.FilterRemoved = VENTROWS.RemoveAutoFilter(ws);
            VENTROWS.Home(ws);

            return outcome;
        }

        // One level's rooms, in the model's order: matched, new, and the
        // unticked ones that have a row (Keep) - an unticked room with no
        // row is not added
        private static void Rooms(
            List<Planned> plan, VENTTEMPLATE.Level block,
            Dictionary<VENTTEMPLATE.Room, Old> oldOf, Outcome outcome, string level)
        {
            if (block == null || block.Rooms == null)
                return;

            foreach (VENTTEMPLATE.Room room in block.Rooms)
            {
                Old old;
                oldOf.TryGetValue(room, out old);

                if (room.Keep && old == null)
                    continue;

                plan.Add(new Planned { Make = Make.Room, Room = room, Old = old });

                if (room.Keep)
                {
                    outcome.Kept++;
                    continue;
                }

                if (old == null)
                {
                    outcome.Added++;
                    outcome.Lines.Add(level + "   " + Label(room.Number, room.Name) + " - new");
                    continue;
                }

                outcome.Matched++;

                string changed = Changes(old, room);

                if (changed.Length > 0)
                {
                    outcome.Changed++;
                    outcome.Lines.Add(level + "   " + Label(room.Number, room.Name) + " - " + changed);
                }
            }
        }

        private static void SubTotal(List<Planned> plan, VENTROWS.RowTemplate template, int from, int first)
        {
            if (template == null)
                return;

            // With no rooms it adds up the row above it - nothing
            int top = plan.Count > from ? first + from : first + plan.Count - 1;
            int bottom = plan.Count > from ? first + plan.Count - 1 : top;

            plan.Add(new Planned { Make = Make.SubTotal, BlockFirst = top, BlockLast = bottom });
        }

        private static string Label(string number, string name)
        {
            return ((number ?? string.Empty) + " " + (name ?? string.Empty)).Trim();
        }

        // What the model changed about a room, said briefly
        private static string Changes(Old old, VENTTEMPLATE.Room room)
        {
            var said = new List<string>();

            if (!string.Equals(Clean(old.Name), Clean(room.Name), StringComparison.Ordinal))
                said.Add("name was " + old.Name);

            if (!string.Equals(Clean(old.Number), Clean(room.Number), StringComparison.Ordinal))
                said.Add("number was " + old.Number);

            double area = Math.Round(room.AreaM2, 2);

            if (old.Area == null || Math.Abs(old.Area.Value - area) > 0.004)
                said.Add("area " + Num(old.Area) + " -> " + Num(area));

            double height = Math.Round(room.HeightM, 2);

            if (old.Height == null || Math.Abs(old.Height.Value - height) > 0.004)
                said.Add("height " + Num(old.Height) + " -> " + Num(height));

            return string.Join(", ", said);
        }

        private static string Num(double? value)
        {
            return value == null ? "-" : value.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // ==================================================
        // Reading
        // ==================================================

        // The last row of the level/room block: the last row with a name or
        // a room number, above the Total (the office's "Total" sits right
        // under the block, the summary tables under that)
        private static int BlockEnd(ExcelWorksheet ws, VENTSHEET.SheetLayout layout)
        {
            int end = ws.Dimension.End.Row;
            int stop = end;

            for (int r = layout.FirstDataRow; r <= end; r++)
            {
                if (TotalLabel(ws, layout, r))
                {
                    stop = r - 1;
                    break;
                }
            }

            int last = layout.FirstDataRow - 1;

            for (int r = layout.FirstDataRow; r <= stop; r++)
            {
                if (ws.Cells[r, layout.SpaceNameCol].Text.Trim().Length > 0 ||
                    (layout.RoomNumberCol > 0 && ws.Cells[r, layout.RoomNumberCol].Text.Trim().Length > 0))
                    last = r;
            }

            return last;
        }

        // A "Total" or "Grand Total" row - not a Sub Total
        private static bool TotalLabel(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int row)
        {
            int to = Math.Max(layout.SpaceEndCol, Math.Max(layout.SpaceNameCol, layout.RoomNumberCol));

            for (int c = 1; c <= to; c++)
            {
                string text = ws.Cells[row, c].Text;

                if (VENTSHEET.IsTotalRow(text) && !VENTSHEET.IsSubTotalRow(text))
                    return true;
            }

            return false;
        }

        private static List<Old> Read(
            ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int first, int last, int lastCol, int keyCol)
        {
            var rows = new List<Old>();

            for (int r = first; r <= last; r++)
            {
                string nameText = ws.Cells[r, layout.SpaceNameCol].Text.Trim();
                string name = VENTSHEET.NameOf(ws, r, layout);
                string number = layout.RoomNumberCol > 0 ? ws.Cells[r, layout.RoomNumberCol].Text.Trim() : string.Empty;

                Kind kind;

                if (VENTSHEET.IsSubTotalRow(nameText) || VENTSHEET.IsSubTotalRow(name))
                    kind = Kind.SubTotal;
                else if (name.Length == 0 && number.Length == 0)
                    kind = Kind.Other;
                else if (layout.LevelsByColor
                             ? nameText.Length > 0 && VENTSHEET.IsLevelFill(ws.Cells[r, layout.SpaceNameCol])
                             : number.Length == 0)
                    kind = Kind.Level;
                else
                    kind = Kind.Room;

                var old = new Old
                {
                    Row = r,
                    Kind = kind,
                    Name = name,
                    Number = number,
                    Key = keyCol > 0 ? ws.Cells[r, keyCol].Text.Trim() : string.Empty,
                    Area = Number(ws, r, layout.AreaCol),
                    Height = Number(ws, r, layout.HeightCol),
                    RowHeight = ws.Row(r).Height,
                    CustomHeight = ws.Row(r).CustomHeight,
                    InApartment = OwnName(ws, layout, r),
                    FreshDown = DownMerged(ws, r, layout.FreshAirCol),
                    ExhaustDown = DownMerged(ws, r, layout.ExhaustAirCol),
                    Cells = new Cell[lastCol + 1]
                };

                for (int c = 1; c <= lastCol; c++)
                {
                    ExcelRange cell = ws.Cells[r, c];
                    string formula = cell.Formula;

                    var kept = new Cell { Style = cell.StyleID };

                    if (!string.IsNullOrEmpty(formula))
                        kept.R1C1 = cell.FormulaR1C1;
                    else
                        kept.Value = cell.Value;

                    if (cell.Comment != null)
                    {
                        kept.Comment = cell.Comment.Text;
                        kept.Author = cell.Comment.Author;
                    }

                    old.Cells[c] = kept;
                }

                rows.Add(old);
            }

            return rows;
        }

        // What a row had typed in a column - nothing, for a formula
        private static string TextOf(Old row, int col)
        {
            if (col <= 0 || col >= row.Cells.Length || row.Cells[col].R1C1 != null)
                return string.Empty;

            return Convert.ToString(row.Cells[col].Value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        // As VENTSHEET.NameOf reads it: a name in the block's last column
        // that is not the room's name merged across the whole block
        private static bool OwnName(ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int row)
        {
            if (layout.SpaceEndCol <= layout.SpaceNameCol ||
                ws.Cells[row, layout.SpaceEndCol].Text.Trim().Length == 0)
                return false;

            string merged = ws.MergedCells[row, layout.SpaceEndCol];

            return merged == null || new ExcelAddress(merged).Start.Column > layout.SpaceNameCol;
        }

        private static double? Number(ExcelWorksheet ws, int row, int col)
        {
            if (col <= 0)
                return null;

            object value = ws.Cells[row, col].Value;

            if (value == null)
                return null;

            double number;

            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                                   NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                ? number
                : (double?)null;
        }

        private static bool DownMerged(ExcelWorksheet ws, int row, int col)
        {
            if (col <= 0)
                return false;

            string merged = ws.MergedCells[row, col];

            return !string.IsNullOrEmpty(merged) &&
                   new ExcelAddress(merged).Start.Row != new ExcelAddress(merged).End.Row;
        }

        private static bool Down(List<ExcelAddress> merges, int row, int col)
        {
            return merges.Any(m => m.Start.Row <= row && m.End.Row >= row &&
                                   m.Start.Column <= col && m.End.Column >= col &&
                                   m.Start.Row != m.End.Row);
        }

        // The rows cut into levels: a level row, the rows before its first
        // room (its spacer), its rooms, its Sub Total and what follows it
        private static List<Section> Sections(List<Old> rows, Func<string, bool> ticked)
        {
            var sections = new List<Section>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Section current = null;

            foreach (Old row in rows)
            {
                if (row.Kind == Kind.Level)
                {
                    bool firstOfName = seen.Add(Clean(row.Name));

                    current = new Section
                    {
                        Level = row.Name,
                        LevelRow = row,
                        Update = firstOfName && ticked(row.Name)
                    };

                    current.All.Add(row);
                    sections.Add(current);
                    continue;
                }

                if (current == null)
                {
                    current = new Section();
                    sections.Add(current);
                }

                current.All.Add(row);

                switch (row.Kind)
                {
                    case Kind.Room:
                        current.Rooms.Add(row);
                        break;

                    case Kind.SubTotal:
                        current.HasSubTotal = true;
                        break;

                    default:
                        // Before the rooms: the spacer. After the Sub
                        // Total: kept. A blank row between rooms is not
                        if (current.Rooms.Count == 0 && !current.HasSubTotal)
                            current.Head.Add(row);
                        else if (current.HasSubTotal)
                            current.Tail.Add(row);

                        break;
                }
            }

            return sections;
        }

        // ==================================================
        // Writing
        // ==================================================

        // A row exactly as it was, wherever it now stands. Formulas go back
        // in R1C1, so "the row above + 1" stays the row above.
        private static void Put(ExcelWorksheet ws, int row, Old old, int lastCol, Func<string, string> moved)
        {
            for (int c = 1; c <= lastCol; c++)
            {
                ExcelRange cell = ws.Cells[row, c];
                Cell kept = old.Cells[c];

                cell.Formula = null;
                cell.Value = null;
                cell.StyleID = kept.Style;

                if (kept.R1C1 != null)
                    cell.FormulaR1C1 = moved(kept.R1C1);
                else
                    cell.Value = kept.Value;
            }

            if (old.CustomHeight)
                ws.Row(row).Height = old.RowHeight;
        }

        // A room's row: the template's formulas, everything typed in its
        // old row, and what the model says of it
        private static void Fill(
            ExcelWorksheet ws, VENTSHEET.SheetLayout layout, int row, Planned p,
            VENTROWS.RowTemplate template, int lastCol, int keyCol,
            List<Tuple<int, int, object>> typedTotals, Func<string, string> moved)
        {
            VENTTEMPLATE.Room room = p.Room;
            bool keep = room.Keep;

            if (p.Old != null)
            {
                for (int c = 1; c <= lastCol; c++)
                {
                    if (c == keyCol || (!keep && FromModel(layout, room, c)))
                        continue;

                    Cell kept = p.Old.Cells[c];
                    ExcelRange cell = ws.Cells[row, c];

                    // A merged group's air: made afresh from its rooms
                    // (VENTROWS.MergeAir) - a total typed over it is kept
                    // aside and put back once it is merged again
                    if ((c == layout.FreshAirCol && p.Old.FreshDown) ||
                        (c == layout.ExhaustAirCol && p.Old.ExhaustDown))
                    {
                        if (kept.R1C1 == null && kept.Value != null &&
                            !(kept.Value is string && ((string)kept.Value).Length == 0))
                            typedTotals.Add(Tuple.Create(row, c, kept.Value));

                        continue;
                    }

                    if (kept.R1C1 != null)
                    {
                        // The sheet's own formula wins; one typed into a
                        // column that has none is the user's
                        if (!string.IsNullOrEmpty(template.FormulasR1C1[c]))
                            continue;

                        cell.StyleID = kept.Style;
                        cell.FormulaR1C1 = moved(kept.R1C1);
                    }
                    else if (kept.Value != null && !(kept.Value is string && ((string)kept.Value).Length == 0))
                    {
                        // Typed over a formula, or into an input: kept
                        cell.Formula = null;
                        cell.StyleID = kept.Style;
                        cell.Value = kept.Value;
                    }
                }

                if (keep)
                {
                    // Left as it was - the apartments writer names it so too
                    room.Name = p.Old.Name;
                    room.Number = p.Old.Number;
                }
            }

            if (!keep)
            {
                ws.Cells[row, layout.SpaceNameCol].Value = room.Name;

                if (layout.RoomNumberCol > 0)
                    ws.Cells[row, layout.RoomNumberCol].Value = room.Number;

                if (layout.AreaCol > 0)
                    ws.Cells[row, layout.AreaCol].Value = Math.Round(room.AreaM2, 2);

                if (layout.HeightCol > 0)
                    ws.Cells[row, layout.HeightCol].Value = Math.Round(room.HeightM, 2);

                if (layout.UnitsCol > 0 && room.Fixtures > 0)
                    ws.Cells[row, layout.UnitsCol].Value = room.Fixtures;
            }

            if (keyCol > 0 && !string.IsNullOrEmpty(room.Key))
                ws.Cells[row, keyCol].Value = room.Key;
        }

        // The columns the model writes: the Space block, number, area,
        // height - and the units, when the model counted any
        private static bool FromModel(VENTSHEET.SheetLayout layout, VENTTEMPLATE.Room room, int col)
        {
            int end = Math.Max(layout.SpaceEndCol, layout.SpaceNameCol);

            return (col >= layout.SpaceNameCol && col <= end) ||
                   col == layout.RoomNumberCol ||
                   col == layout.AreaCol ||
                   col == layout.HeightCol ||
                   (col == layout.UnitsCol && room.Fixtures > 0);
        }

        private static void RemoveComments(ExcelWorksheet ws, int first, int last, int lastCol)
        {
            var notes = new List<ExcelComment>();

            foreach (ExcelComment comment in ws.Comments)
            {
                var at = new ExcelAddress(comment.Address);

                if (at.Start.Row >= first && at.Start.Row <= last && at.Start.Column <= lastCol)
                    notes.Add(comment);
            }

            foreach (ExcelComment comment in notes)
                ws.Comments.Remove(comment);
        }

        private static void AddComment(ExcelWorksheet ws, int row, int col, string text, string author)
        {
            try
            {
                ExcelRange cell = ws.Cells[row, col];

                if (cell.Comment != null)
                    ws.Comments.Remove(cell.Comment);

                ws.Comments.Add(cell, text, string.IsNullOrEmpty(author) ? Author : author);
            }
            catch
            {
                // a note that will not go back costs only the note
            }
        }
    }
}
