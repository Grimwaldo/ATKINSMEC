# ATKINSMEC — the Atkins MEP Toolkit, a Revit MEP add-in suite

> **Starting a session: read `WORKPLAN.md` first** — current state, what
> has never run in a real model, open questions, things deliberately
> reverted.
>
> This file is the SHORT version: the rules and traps. The long one —
> why each rule exists, the user reports behind them, the dead ends — is
> in git: `git show 465c133:CLAUDE.md`. Read it there before reworking
> anything this file only summarises.

Eleven Revit add-ins, one assembly, one ribbon panel (**Atkins MEP Toolkit
→ Mechanical**), built from one source tree for **Revit 2022–2027**.

**"Atkins MEP Toolkit" is the name people SEE** (user, 2026-10-12): the
ribbon tab, the `.addin` Name/Text, window titles, dialog captions, log
headers, the installer and its output (`AtkinsMEPToolkit-Setup.exe/.zip`).
**`ATKINSMEC` stays wherever something is stored or looked up**: the
assembly, namespace and `.addin`/`files.txt` file names (an old install
upgrades and uninstalls in place), `%AppData%\ATKINSMEC\` (everyone's
saved settings), the log file names, and what lives inside
models — `QAAVF`'s style name and schema. Never rename those.

| Button | Entry point | What it does |
|---|---|---|
| AUTOVENT | `Command.cs` | Excel ventilation sheet → airflows on air terminals; exports model rooms back into the sheet. |
| CHW VOLUME | `CHWCommand.cs` | Water volume of two piping system types across the model and its links. Excel export. |
| DUCT INSULATION | `INSULATIONCommand.cs` | Insulates ducts/fittings/accessories as they are drawn, by system type. |
| SPRINKLERS | `SPRINKLERCommand.cs` | Pendent heads per NFPA 13 across rooms of a linked arch model. |
| ALIGN TERMINALS | `ALIGNCommand.cs` | Moves air terminals onto the nearest linked-model terminal, in plan. |
| AUTOSYNC | `SYNCCommand.cs` | Saves and syncs every open model on a timer (≥5 min). |
| NWC BATCH | `NWCCommand.cs` | Navisworks .nwc from a list of models (disk or ACC). |
| EQUIPMENT CHANGER | `EQUIPCommand.cs` | Changes equipment/terminal type and resizes the ductwork on it up to the first reducer/tap/tee/terminal. |
| DUCT ESP | `ESPCommand.cs` | External static pressure across this model and linked models. |
| ROOM TAGS | `TAGCommand.cs` | Rooms of ticked linked models shown untagged in plans on sheets; tags them, never twice. |
| SHEET QA | `QACommand.cs` | Six QA pages: flowrate tags, grids, duct disconnections, FCU flows, CHW sizes, CHW disconnections. Most can fix what they find. |

---

## Build, deploy, install

```bash
dotnet build ATKINSMEC/ATKINSMEC.csproj -c Release -p:RevitVersion=2025   # one version
powershell -NoProfile -ExecutionPolicy Bypass -File build-all.ps1          # all six, deployed
powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1    # installer/output/
```

- **Close Revit before building.** The deploy (`DeployToRevit`, to
  `%AppData%\Autodesk\Revit\Addins\<ver>\`) skips a locked DLL with only a
  warning. **Verify with a hash, never the build result:**
  ```powershell
  $d="$env:APPDATA\Autodesk\Revit\Addins\2022\ATKINSMEC.dll"; $s="ATKINSMEC\bin\Release\2022\ATKINSMEC.dll"
  (Get-FileHash $d).Hash -eq (Get-FileHash $s).Hash
  ```
- `-ExecutionPolicy Bypass` is required: no policy is set on this machine.
- Visual Studio builds and deploys **2022 only**, Debug; `build-all.ps1`
  deploys Release — whichever ran last is what Revit loads.
- **AUTOVENT on its own** (temporary, user 2026-10-15):
  `build-installer.ps1 -AutoVent` → `installer\output\AUTOVENT-Setup.exe`.
  Same source, `-p:Edition=AutoVent`: assembly `ATKINSMEC.AutoVent`,
  only `AutoVent\` + `Shared\` (minus `QUIET.cs`), `AUTOVENT_ONLY` in
  `UI.cs` (one button "AutoVentOnly", no updater, no AutoSync), own
  `AUTOVENT.addin` + AddInId, own obj/bin (`obj\AutoVent-<ver>`,
  `bin\Release\AutoVent\<ver>`), never deployed by a build. Installs
  `AUTOVENT.addin` + `AUTOVENT\*`, its own `AUTOVENT.files.txt`; uninstall
  removes only `autovent_*.txt` settings. Beside the suite: no shared
  file, two AUTOVENT buttons, one settings file.
- Installer: `installer/exe/` (net48 WinForms, one embedded zip). Keep
  `app.manifest` (`asInvoker`, or Windows raises UAC for a file called
  "setup"). Uninstall reads `ATKINSMEC.files.txt` — never clear the
  shared add-ins folder. Output `installer\output\AtkinsMEPToolkit-Setup.exe`
  (+ `.zip`), ~21 MB, no admin rights needed.
- **Git**: `main`, remote `origin` = github.com/Grimwaldo/ATKINSMEC,
  `core.autocrlf false` (files are a mix of LF and CRLF — keep each as
  it is). **Two-way sync** (2026-11-10): `tools\sync.ps1` commits local
  changes, rebases onto `origin/main`, pushes; the scheduled task "ATKINSMEC
  GitHub sync" runs it every 15 min (`tools\sync-install.ps1`, `-Remove`),
  log `ATKINSMEC_sync_log.txt`. It only syncs `main` and aborts on a
  conflict. Claude's cloud work reaches this folder once merged into
  `main` on GitHub. A post-commit hook
  (`tools/git-hooks`, `core.hooksPath`) pushes to `backup-onedrive`
  (`C:\Users\moham\OneDrive\Backups\ATKINSMEC.git`). OneDrive was not
  signed in and `D:` is write-protected, so that copy is on the same disk.

## Multi-version

| Revit | TFM |
|---|---|
| 2022–2024 | `net48` |
| 2025–2026 | `net8.0-windows` |
| 2027 | `net10.0-windows` |

- Revit API from NuGet (`Nice3point.Revit.Api.*`), pinned per version.
  The API docs are the `.xml` beside it —
  `~/.nuget/packages/nice3point.revit.api.revitapi/<ver>/ref/<tfm>/RevitAPI.xml`
  — check there, not from memory.
- `Directory.Build.props` sets `BaseIntermediateOutputPath` per version;
  moving it into the csproj breaks (MSB3539).
- `REVIT2023_OR_NEWER` (for `Selection.SetReferences`) and
  `REVIT2024_OR_NEWER` (ids). **`ElementId.IntegerValue` is gone in
  2027**: every id goes through `REVITCOMPAT` (`ToValue()`, `NewId()`),
  ids are `long`.
- Only third-party dependency: **EPPlus**.
- `TaskDialog`, `Color`, `Visibility`, `Application` are ambiguous on
  some targets — write `Autodesk.Revit.UI.TaskDialog`,
  `System.Windows.Visibility` etc. in full. A preview stub compiling is
  not the add-in compiling: build 2022, 2025 and 2027.

## Files

One folder per add-in under `ATKINSMEC\` (`AutoVent`, `ChwVolume`,
`DuctInsulation`, `Sprinklers`, `AlignTerminals`, `SheetQA`, `AutoSync`,
`NwcExport`, `EquipChanger`, `EspCalc`, `RoomTags`) plus `Shared\`. **The namespace
is flat (`ATKINSMEC`) everywhere** — folders are for finding, not
scoping. The SDK globs `**\*.cs`.

**Resource names are pinned with `LogicalName`** (the two form `.resx`
files, `Resources\Icons\*.png`, `Resources\VentTemplate.xlsx`) because
the default name follows the folder. After moving any, byte-scan:
```bash
grep -ao "ATKINSMEC\.[A-Za-z0-9.]*\.\(resources\|png\|xlsx\)" ATKINSMEC/bin/Release/2022/ATKINSMEC.dll | sort -u
```

**Ribbon icons** are drawn by `tools/make-icons.py` (one palette, one
grid) — check `tools/icons-contact-sheet.png` at 32 px. `UI.Icon` needs
`BitmapCacheOption.OnLoad` (else a blank button) and `Freeze()`; a bad
icon returns null, never throws.

**Shared**: `UI.cs` (ribbon, updater registration), `REVITCOMPAT`,
`FORMSIZE` (WinForms minimum size), `RVTLINKSELECTION` (+ Revit-free
`RVTLINKROOMS`: `LinkedRoom`, `RoomKey`, `RoomsOn`), `SAMEDOC`,
`QUIET`, `WORKSETPICK`, `LOGS` (where every log goes). `SpaceKey.cs`
is dead code.

---

## AUTOVENT

Modeless **WPF** window (`VENTWindow.xaml`; the only WPF window —
`<UseWPF>` and `<UseWindowsForms>` both on). Work lives outside it:
`VENTSHEET` reads the workbook, `REVITROOMS` writes terminals,
`REVITEXPORT` writes the sheet, `VENTREAD` reads the model for the window.
Revit-free (and tested): `VENTLEVELMAP`, `VENTSETTINGS`, `VENTSAVED`,
`VENTFCUCHECK`, `VENTOFFICE`, `VENTROWS`, `VENTTEMPLATE`, `VENTLEVELROWS`,
`VENTLEVELNOTE`, `VENTEXPORTNOTE`, `VENTLINKFILTER`, `VENTINTERIOR`,
`VENTAPARTMENTMAP`, `VENTARRANGE`, `VENTFIT`, `VENTUPDATE`,
`VENTOCCUPANCY`, the dialogs (`VENTFIXTUREDIALOG`, `VENTINTERIORDIALOG`,
`VENTSYSTEMDIALOG`, `VENTOCCUPANCYDIALOG`) and the Levels and rooms window
(`VENTROOMSWINDOW`).

**Keep its on-screen wording short** — the user: *"do not overexplain
things in the autovent addin"*. One-line hints, a few words in tooltips,
plain status lines.

### Window rules
- **Never read the model from a handler.** Reads go through `VENTREAD`
  — in the constructor (inside the command's context) or the external
  event (`ExtEventHan.Read`, answer to `Take`). The window caches what
  was read (`_roomsOf`, the apartments map + the key it was built for,
  fixture families) and `RefreshLevels` asks only for what is missing.
  Refresh drops it all. `VENTREAD.Do` answers every part asked, with
  nothing if it must, or it is asked forever.
- `ExtEventHan.Execute` takes and clears `Request` first; reads have
  their own slot. Reads use the window's own document, not `Command.Doc`.
- A modal dialog blocks external events: a dialog needing model data
  asks first and opens when `Take` gets the answer (Fixtures, Interior
  models).
- Owner = Revit's main window (`WindowInteropHelper`), or it vanishes
  behind Revit.
- WPF traps: a radio with `IsChecked="True"` raises `Checked` during
  parse (guard nulls); an underscore in `Content` is an access key (put a
  `TextBlock` inside); a `ScrollViewer` inside a `StackPanel` never
  scrolls; render at **1.5x as well as 1x** before calling a layout done.
- **Fits a 1080p laptop at 150%** (`VENTFIT.Screen`): the main window
  opens as tall as its content (`SizeToContent`) up to the work area,
  then keeps that height and lets the cap go (`ContentRendered`), so a
  bigger screen can still drag it taller; the body scrolls below it.
  MinHeight 480. (`BodyGrid`'s height was SET for the levels tree's
  virtualisation; the tree went to Levels and rooms, 2026-10-19.)
- `tests/ventpreview` renders the real XAML with a Revit-free stub. **The
  stub needs every handler the XAML names** (VS's designer adds handlers
  on a double-click).

### Settings, per model (`VENTSAVED`, `%AppData%\ATKINSMEC\autovent_settings.txt`)
- Keyed by where the model lives (`ModelKey`: ACC model GUID / central /
  file). A model never set up gets the defaults, not the last model's.
- Links remembered by **id AND label** (`Resolve`: id first, then a label
  naming exactly one loaded link); unloaded links keep their choices
  (`KeepUnloaded`).
- **Ticks that only repeat the default are saved as "follow the
  default"** (null), so later additions are guessed like any other;
  "chose none" is an empty list. Same rule for system types, fixtures,
  interior picks and room orders.
- Saved on every change, only after `_ready`.
- **Everything edited survives a close** (user, 2026-10-12): also the
  rooms ticked by hand (`_roomChoice` → `room` lines, re-pointed with
  the arrange links), the workbook + worksheet (`RestoreSheet`, in
  `Window_Loaded` BEFORE `Take(_first)`, so the levels build once on the
  sheet; a missing file keeps its path and says so in the status), the
  export way / sheet / new name, the merges. Room ticks save through
  `SaveSoon` (one write per level tick, not per room); the text boxes on
  `LostFocus`; `OnClosed` saves the rest.

### Airflow write
- Fresh/extract system types: ticked in `VENTSYSTEMDIALOG`, one per
  side, opened by the assign tab's two buttons (lists on the tab until
  2026-10-12), a line under each saying what is ticked; else the old name
  rule (`Fresh` / `Extract` / `Exhaust`); the two sides decided
  separately; the pre-tick IS the fallback rule.
- Endcap reach (default 1500 mm; ≤0 → default). Too large finds the
  wrong FCU.
- **A fresh air endcap by a RETURN DUCT** (user, 2026-10-24, YANM3's
  EMCC rooms: the FCUs hang in the corridor, the endcap is by the return
  duct from the room, 3.4 m from the unit). A unit standing outside every
  room it supplies (`VENTFACING.Outside`; supplies = its SUPPLY
  terminals' rooms, by ducts or system - never endcaps, so an AHU is not
  one) has its return ductwork read (`ReturnSide`: what each connector
  leads to that reaches no supply or fresh air terminal). An endcap
  WITHIN THE REACH OF that ductwork and no further from it than from any
  unit goes to it (`VENTFACING.Endcap`); a unit nearer keeps the reach.
  No limit at first - then MCC ROOM BP01.B1.179 read 3660 l/s (2026-10-25):
  its units took endcaps 2.9 m and 8.5 m off their return ducts. Every
  true match on YANM3 was within 1.2 m of the duct (EMCC's 0.48 / 0.50 m). Extract endcaps: reach only. One rule
  (`EndcapUnit`) for the endcap loop and the bellmouths' "unit has an
  endcap". Logged: `OUTSIDE FCU ...`, `nearer the return duct of FCU`.
- **Endcaps and bellmouths, by family name** (two boxes, comma-separated,
  matched like the links search). An **endcap** (`EndcapNames`, default
  and empty = `Endcap`) is always traced to the nearest unit. A
  **bellmouth** (`BellmouthNames`, default `BellMouth`, empty = none) is
  decided by `VENTFACING.Bellmouth`, in order: faces a unit within the
  reach (a ray from the duct connector, away from the duct, sign checked
  against the body, into the box grown 150 mm) → within 300 mm of a
  unit's casing → no unit in reach = its room → mouth pointing
  down = its room → in no room in scope, the nearest unit supplying its
  room, or its room's ROW giving none of that air (the SHEET; **no row is
  not "none"**, 2026-10-24) = feeds the nearest unit
  → its room has its OWN terminal for that air (no bellmouth/endcap) AND a
  room the unit supplies needs it with nothing bringing it (no terminal,
  no endcap on the unit) = feeds the unit (`UnitNeedsIt`, 2026-10-15) →
  otherwise its room. **Facing alone found 8 of 32 on the first real
  job** (kifaf, 2026-10-11) and left four rooms with no fresh air. A
  unit on the bellmouth's own duct system is never fed, **nor a unit
  whose supply system reaches no room in scope** (`VENTFACING.CanFeed`,
  every test above; YANM3's STOREs BP01.B1.019 / .076, 2026-10-24 - their
  air went to such units, one faced 1.22 m off). Every decision and its
  reason is in the debug log (`BELLMOUTH ...`; a unit in reach left out
  is named, "supplies no room in scope - not fed").
- **Between rooms of different links, the HIGHEST FLOOR IN THIS MODEL
  wins** (`RoomInfo.HostFloor`, `RoomAt`; total transforms). Comparing
  each link's own `BaseZ` made one interior model placed on every floor
  tie, and the tie went to the placement read first - KIFAF LEVEL 04A's
  kitchen/foyer terminals were in LEVEL 03's rooms (2026-10-19), hidden
  by equal sheet values, found by Merge by FCU.
- **A terminal's room is the room IN FRONT of its face** (300 mm along
  `VENTFACING.Mouth`, falling back to where it stands): a sidewall
  grille's insertion point in the kitchen wall made kitchens "served by
  2 FCUs". Logged as `TERMINAL ... faces into`. The log also lists the
  rooms each FCU supplies.
- **Air with nowhere to go is reported** (`VENTFCUCHECK.Nowhere`): a
  room with air in the sheet and no terminal in it or endcap carrying
  it — in the run's dialog and the log. The divided-between-endcaps list
  names only sides the sheet gives air to (`Worth(tally, fresh, extract)`).
- **Endcaps split a room's air between the endcaps bound to its units**
  (`VENTFCUCHECK`: `Tally`, `Share`, `Carried`) — the shares always add
  back to the whole. One unit, one endcap divides by one; counts per side.
- **A sheet row matches a room by its model ID first** (the hidden
  `AUTOVENT model ID` column, `AirData.Id`, `VENTSHEET.Resolve`), else by
  name + number + level (`VENTSHEET.SpaceKey`) — one row to one room,
  never a row whose ID is another model room's. Two apartments built
  from one interior model repeat name + number (KIFAF 121.141/143/144);
  without IDs both took one row's air. Still-ambiguous rooms are in the
  run's dialog ("share a name and number"). `LoadedRows` keeps every row;
  `LoadedData` (one per key) is for the window. Export and airflow write
  must still agree on name + number + level for ID-less sheets.
- **A workbook with formulas and no saved results** (AUTOVENT's own
  export, never opened in Excel) is calculated on reading
  (`VENTSHEET.NoResults` → `Calculate`, `Calculated` logged) — it read
  as 0 L/s everywhere.

### Levels
- **The levels are THIS model's** (`GetHostLevels`); linked level names
  play no part.
- **A linked room's floor is its GEOMETRY** — the bottom of its box
  through the link's total transform (`HostElevationOf`). Level
  arithmetic (`ProjectElevation` + base point Z) was 3.49 m high on a real
  job and put every room one level up. Every level is corrected by what
  its own model's rooms/spaces measure (`RVTLINKSELECTION.LevelError` →
  `VENTLEVELMAP.LevelError`, median, 50 mm dead band) — host storeys,
  link spans, SPRINKLERS. **A model with no rooms or spaces is measured
  on its ducts / pipes / trays / conduits** (start point vs reference
  level + Start Middle Elevation; user, 2026-10-24): YANM3's MEP model
  has none, its levels stayed 3.9 m high and 137 of 138 linked rooms fell
  below its lowest level. The run log says what it measured on.
  **Never compare `Level.Elevation` with geometry.** `Storey.Shown` is
  for telling a person only.
- **Band rule** (`VENTLEVELMAP.StoreyOf`): a room belongs to the highest
  level at or under its floor, with a 300 mm allowance below. Below every
  level = in no band, left out and counted.
- Link span (`LevelSpan`): lowest/highest linked level something modelled
  stands on (allow-list `Content`, shafts and rooms excluded);
  `ElementLevelFilter` is slow, so walk inwards from each end.

### Levels and rooms (`VENTROOMSWINDOW`, `VENTLEVELROWS`; tests: `arrangeflowtest`, `levelrowtest`, `toggletest`)
- **"Levels to process" and Arrange rooms... are ONE window** (user,
  2026-10-19: "combine..."; asked, chose a window of its own over the
  main window's card). Card 3 is now a count (`TxtTicked`,
  `VENTLEVELNOTE.Ticked`), the sheet-level warning and summary, and the
  **Levels and rooms...** button. The window: every level (open / close:
  arrow, double-click, Left/Right), under an open one its rooms and
  containers in sheet order, a tick on every row, the airflow columns,
  All / None / Show in plan beside Arrange rooms' buttons.
- **A SHEET'S LOOK** (user, 2026-10-25: "make the rooms and level window
  look more like excel view"): every row is cells (`Cell`: a gridline
  right and under, #D4D4D4) - a grey gutter (Excel's row numbers; the
  drag handle, a level's arrow) | tick | Number (units indented) | Name |
  **Notes** (merged with, one FCU with, ungrouped from, terminal on no
  FCU, the model - their own column now, parted by " · ") | m² | m |
  Fresh | Extract | Fresh | Extract; Name : Notes share the rest 4 : 5.
  Grey two-row headings (`Header`, groups Room / Sheet / Model) built
  from the same widths, in by **2** - the list's border AND the 1 its
  template pads inside (a ListBox's Padding is not that) - plus the scroll
  bar while it shows; `arrangeflowtest` checks the headings sit over
  their cells to 0.6 px. Levels are group rows; rows touch (item border
  0), so the drop mark lays 3 px over its neighbour with a -3 margin.
  Tests find a row's parts with `Within<T>` (logical tree), not by
  position. Window 1600 wide (VENTFIT caps it).
- **Columns, left to right** (`ColumnsShowing` - one list the headings
  and every row's cells are built from; blocks of cells docked right,
  tagged room / sheet / new / model / occupancy / calc): Room m² m |
  **Sheet** (m², Fresh, Extract - READ ONLY, always) | **New values**
  (Fresh air, Exhaust air) | **Model** (Fresh, Extract) | **Occupancy**
  (Fresh category, Extract category, Use, Occupants, Units) |
  **Calculation**. The airflows beside the name, so they show on a
  laptop. Name + Notes never under 340 px: past that the list SCROLLS
  SIDEWAYS - every row's Width set to max(columns, viewport)
  (`ApplyWidths`, on the list's own `ScrollChanged`), the headings moved
  by a `TranslateTransform` inside a clipping border (rows measured at
  infinite width would each take their own text's width and the columns
  would not line up). **The headings sit on a `Canvas`** in that border:
  in a narrower parent WPF clips an element to its slot BEFORE its
  RenderTransform moves it, so scrolled sideways the columns past the
  window had no headings (user, 2026-10-28: "many columns are empty
  without headers"; arrangeflowtest checks `GetLayoutClip` is null).
- **THE OCCUPANCY, CHOSEN HERE** (user, 2026-10-26: "select the
  occupancy of the space just like the excel sheet ... put the new values
  ... in another columns called 'new values'"; asked: categories, use,
  occupants, units; from rules = blank ones only; calculated by THE
  SHEET'S OWN FORMULAS). **ONE CLICK on an occupancy cell opens it, as
  Excel's drop-down** (user, 2026-10-28: "its not a droplist like excel";
  a list's cell shows a ▾): the cell's own list ("As the sheet",
  "(blank)", its drop-down's or ASHRAE's categories; Use's list) or a box
  for a number. The press is handled on the list's
  `PreviewMouseLeftButtonDown` (no drag, no pick): a row among the picks
  keeps them, so its choice is every picked room's; a row not picked is
  picked alone; Ctrl / Shift still pick. (A double-click's first press
  had dropped the other picks.) The list ignores a double-click's second
  press, which would close it. Tests press with `PreviewMouseDownEvent`:
  the left-button one is a DIRECT event each element raises from it. Kept in the arrangement (`VENTARRANGE.
  RoomOccupancy`, null = as the sheet, "" / NaN = blank; the sheet's own
  value chosen goes back to null, `VENTFLOWS.Choose`), saved per model as
  `roomoccupancy` lines (`VENTSAVED.RoomPick`, "~" for null; a key with a
  link id - re-pointed by `Remap`). A choice: blue cell, "Sheet: X" on
  hover. A blank category shows, grey italic, what an export would fill
  (`SheetFlows`: `Learned`, else `RuleFor`). **Occupancy from rules**
  (picked rooms, none picked: every room showing): blank categories from
  those suggestions, only a spelling the cell's list has (`PickFrom`),
  then the rule's Use / Units into blank ones where the exhaust category
  is the rule's (`FromRules`, as `VENTOCCUPANCY.Fill`). **Occupancy as
  the sheet** clears the picked rooms'. NOTHING is written to the
  workbook; the export does not use these choices.
- **New values** (`VENTWHATIF`, EPPlus, test officetest `WhatIfTests`):
  the main window opens a COPY of the imported workbook in memory (the
  file's bytes, `FileShare.ReadWrite`, never saved) with Levels and rooms
  and again for another sheet read (`FormulasFor`; closed with the
  window). Each room's sheet row (`AirData.Row`, by `VENTSHEET.Resolve`):
  a choice goes into its input cells (the original formula/value kept and
  put back when the choice goes), the row's cells are calculated, and the
  first row of a merge it is in. A row with no choice reads as the sheet
  had it when opened (`_asSheet`), never as EPPlus re-works it. The copy
  has "Dwelling Unit" and the kitchen mended (`DwellingCheck`,
  `KitchenCheck` on every row) as an export would. Final air as the
  sheet's are read (blank = 0); an error (`#N/A` for a category the table
  has not got) shown in the cell. Amber where not the sheet's.
  **Calculation** columns = the headed, unhidden columns after the Space
  block (not those shown, `Columns`) that a FORMULA works out with
  something in it on one of the rooms' rows (`ColumnsFor`, 2026-10-28 -
  KIFAF 7: 23 → 12; height, bedrooms, balancing, remarks were typed or
  empty all the way down); label = the lowest heading part that is not a
  unit, a sign-off ("Calc. by", initials, dates) or a formula written out
  ("Rp x N1 + Ra x A"; a bracketed one after a name left off), over two
  lines, letter + unit under it; amber where not the sheet's. Off in
  Columns... as one.
- **Columns...** (`VENTCOLUMNS`, per PERSON in
  `%AppData%\ATKINSMEC\autovent_columns.txt`, each key on / off so a new
  one takes its default): Notes, Room m² m, Sheet m², Occupancy
  categories + use, Occupants and units, Calculation columns; all on but
  Sheet m². Tests point `VENTCOLUMNS.SettingsPath` at a temp file.
- **Differences only** (beside the search; `Search.Apply`'s `keep`, both
  at once): a number used twice, model ≠ what Write would write, new
  values ≠ the sheet's, a terminal on no FCU, no row in the sheet, area
  changed (`Attention`; the row's tooltip
  says which). Sheet | Model sit under **"Merge and write as"** - they
  switch the grouping and what Write writes, not which columns show.
- **MODELESS, owned by the main window, everything at once** - no OK /
  Cancel. An arrangement change → `Arranged` (main window:
  `SaveSettings` + `RefreshLevels`, whose `Rebuilt` → `Refill`; a change
  made in the window is shown once, by `Changed`, `_arranging` guards the
  re-entry); a tick → `Ticked` (`ShowTicks`). **Reset level asks first.**
  Show in plan (request 3) and Write to terminals (request 4) go through
  the main window's external event; the answer (`Shown`) reaches both.
- The ticks are the main window's `LevelRow` / `RoomRow` (`() => _levels`,
  rebuilt by `RefreshLevels`), BOUND in the window (room two-way, level
  one-way + `ClickMeans`, label bound); a container's box is set by hand
  (`Ticks`). A level's tick follows its rooms (all / none / third state).
  Building the list is not choosing (tick set before `Parent`); a
  person's choice outlives a rebuild (`_roomChoice`); `_settling` /
  `_pushing` keep the two directions apart.
- **WPF sends a click on the third state to UNTICKED**: the level box is
  `OneWay` and `LevelClicked` decides (`ClickMeans`).
- Runs are handed the **unticked** rooms (`ExcludedRooms`), so a room
  drawn later is still processed.
- Picks ≠ ticks: the ListBox's own (Extended: click, Ctrl, Shift,
  Ctrl+A); a box on a PICKED row ticks every picked row; Space ticks the
  picks; a picked level or container stands for its rooms showing (Show
  in plan, Write). Colours sit on the ITEM so a pick shows.
- **Search** (`Search.Apply`, words = level + `VENTARRANGE.Words`) only
  hides rows (`IsShown`): ticks stand. While on, a level's tick, a
  container's, All, None and picked levels act on rows **showing**; a
  container shows if a unit does. Opened levels are restored on clear
  (`_openBefore`, by name) and on close. Esc clears it, then closes.
- Show in plan (`VENTSHOW`, request 3; also a double-click on a room or
  container): one plan, one level - the one most picks are on, a tie to
  the higher; frames every corner through the link transform; selects via
  `SetReferences` on 2023+.
- **The terminals are read** (`SurveyTerminals`) when the window opens,
  when the ticked links differ from those last read (`_surveyedFor`),
  after Refresh, and **after a write - inline in request 4** (a request
  raised inside the handler would arrive empty). Meanwhile the Model
  columns say "-" and the line "Reading the terminals...".

### Export (`REVITEXPORT`, `VENTOFFICE`, `VENTROWS`; test: `officetest`)
- Three ways: **update the imported sheet** (`VENTUPDATE`), a new
  workbook from the office template, a worksheet added to the imported
  workbook (refuses a name in use; leaves every other cell alone).
- **The update never throws away what was typed** (user, 2026-10-14;
  it was "fill", which blanked every input and dropped unticked levels).
  Rooms on TICKED levels matched to rows: hidden `AUTOVENT model ID`
  column (every export writes it, `WriteKeys`) → number on the same
  level → a name unique on that level on both sides (`Match`). Matched:
  typed values carried (a value typed over a formula too; the sheet's
  formula wins over an old one); name/number/area/height/units from the
  model. New rooms and new levels (`Place`: after the last sheet level
  below it, sheet order untouched) added. A room no longer in the model:
  row KEPT, greyed italic + note (user's choice). Unticked level: rows
  copied byte for byte (R1C1). Unticked room (`Room.Keep`): row as it
  was, never added. Merged air cells are rebuilt by `MergeAir`; a total
  TYPED over a merge is put back. The block grows **inside** its room
  rows so summary-table ranges follow (and are re-pointed if not); the
  Total is rebuilt (`VENTOFFICE.TotalOfSubTotals`). Tests: officetest
  `MatchTests`, `UpdateTest`, `UpdateWithoutKeysTest`, `UpdateMergedTest`.
- Template = the office workbook embedded byte for byte
  (`Resources\VentTemplate.xlsx`); replace the file to revise it.
  Worksheet preference **Tower 2 → Tower-1 → MLCP**. A Room Number column
  is inserted after the Space block. Formulas kept, typed values blank.
- Room/subtotal rows are a **consensus** of the office's rows
  (`VENTOFFICE.Consensus`, `Refs.Relative`); rows inserted strictly
  inside the block; templates read before the resize and shifted after
  (`Refs.ShiftR1C1`, never other sheets' refs); the Total is rebuilt.
- Drop-downs rebuilt. EPPlus: validation addresses space-separated;
  whole-row validations dropped; it rewrites `=+IF(` as `=IF(`.
- **Reset the saved view before freezing** (`TopLeftCell = "A1"`,
  Normal view) — EPPlus freezes relative to `TopLeftCell`; a split that
  does not exist reads as `NaN`. Broken names (`#REF!`, `[`) dropped from
  the template only. Room numbers written as text. `#DIV/0!` in a fresh
  sheet is the office's own.
- Round trip is the test: every output is read back through
  `VENTSHEET.excelsheet()` and calculated by EPPlus.

### Occupancy categories (`VENTOCCUPANCY`, `VENTOCCUPANCYDIALOG`)
- User, 2026-10-14: fill the sheet's two "Occupancy Category" columns —
  Table 6-1 (outside air, office col O) and Table 6-4 (exhaust, AC) —
  from room names. Found by heading (`FreshCategoryCol`,
  `ExhaustCategoryCol`: "occupancy"+"category", "exhaust" decides which).
- **Rules**: "name contains" words → both categories, for apartments /
  not / any (`Room.InDwelling`, set before ungrouping). Most specific wins
  (words' length, scoped before any); a rule word ≥3 letters starts a name
  word, 1–2 letters must BE one (`G RM` ≠ GYM RM). **"Equals" rules**
  (`Rule.Exact`, user 2026-10-19; the dialog's Match column, the file's
  8th field `equals` — an old override number there reads as contains):
  the name's words ARE the rule's, same order, nothing more (`Fits`), and
  any equals rule beats every contains rule (`ExactFirst`); between two,
  scoped before any. Blank = write nothing.
  `FIRE LOBBY` → Common Corridors, so FIREMAN LOBBY is not a main entry
  lobby (user, 2026-10-16). Office-wide on this PC
  (`%AppData%\ATKINSMEC\autovent_occupancy.txt`; none = `Defaults()`,
  Reset deletes it). Per model on/off (`occupancy` line, default on).
  **A saved file still gets later built-in values** (`WithBuiltIn`, in
  `Load`; user, 2026-10-19 — KIFAF's kitchens were 0 L/s because the file
  predated the kitchen's value): a saved rule with the same scope, words
  and both categories as a built-in one takes the built-in Use / Units
  for each it leaves BLANK. Typed values stand; blank cannot mean "none"
  for those two on a built-in rule.
- **Only blank cells**, and only a category the cell's own drop-down (else
  the ASHRAE sheet) has — `Pick` returns the list's spelling; a miss would
  be #N/A across the row. An update first follows what the SHEET gave rooms
  of that name (`Learned`). Apartments → "Dwelling Unit" (user's choice).
- **The office's Table 6-4 gave 0 L/s** for apartment kitchens (no rate)
  and residential toilets (only the light-use rate; the sheet takes the
  heavy one unless "Nature of use" says light), and per-fixture
  categories need "No. of Units". A rule therefore also carries `Use`
  (AD) and `Units` (AE, when the fixture count left it blank): residential
  toilet / shower = light use, 1 unit; apartment kitchen = 1 unit.
  Extras apply when the row's exhaust category IS the rule's. Rows still
  per-fixture with no units → `Filled.NoUnits`, in the export log.
- **The apartment kitchen is PER UNIT, as "Toilets - private"** (user,
  2026-10-19, `KitchenCheck`, beside `DwellingCheck` in every export and
  update). The office's Table 6-4 row for "Residential kitchens-
  Apartments" is EMPTY (62.1 leaves dwellings to 62.2) and the AF formula's
  per-unit OR(...) list did not name it, so it read 0 L/s. Now: the empty
  row gets 62.1's residential kitchen rates (50/100 cfm, 25/50 L/s a
  unit; never over a row with any value) and every OR(...) naming
  `"Toilets - private"` names the kitchen beside it (AF reads it per unit,
  AG leaves it). 1 unit x 50 L/s, 25 for light use. Adding a worksheet to
  an imported workbook fills ITS empty row too (said in the log). It
  replaced an override (AK = Yes + AL 50, 10-15 to 10-19) the user had
  undone: no rule writes AK/AL any more; an old file's eighth field is
  not read. `OverrideCol`/`BalancingCol` are still detected (a typed
  balancing airflow keeps a row off the NoUnits list). **Two "balancing"
  headings**: Y (outside air's alternate approach) and AL —
  `BalancingCol` is the one AFTER the override column.
- **Apartments' bedrooms and occupants** (`Dwellings`): "No of Bedrooms"
  (M, apartment's first row) = rooms named bedroom (`IsBedroom`);
  occupants N1 = bedrooms + 1, **2 in the master (else first) bedroom, 1
  in each other** (user's choice); a studio 1 in its first Dwelling Unit
  room. Blank cells only. `OccupantsCol` = "occupants" + "n1", not N2.
- **"Dwelling Unit" was broken in the office's own workbook**: the room
  formula's `IF(O=$H$470,"NA",...)` points tens of rows away from the
  helper cell (one pattern pointed at "Lecture Classroom"), so the
  category gave #VALUE!. `DwellingCheck` re-points it at Table 6-1's own
  cell in every export and update (template room row only; untouched
  levels keep theirs).

### Fixtures (`VENTFIXTURES`)
WCs and urinals counted into "No. of Units" (column found by heading,
shifts with the Room Number column). Sources default to the room links;
families default to `LooksLikeWcOrUrinal` (never "lavatory" — a basin in
Revit's US families). Room found by `GetRoomAtPoint` at the box middle,
300 mm in front, then the origin. Occupants are NOT counted.
- **The dialog lists MODELS, not placements** (user, 2026-10-17):
  `VENTFIXTUREDIALOG.ModelOf` = the label's title before `" : "`; a
  ticked model stores all its placements, and the export adds any placed
  since (`VENTFIXTURES.EveryPlacement`, by link type — explicit choices
  only; the default stays the room links). The window's line counts
  models (`ModelCount`). Ticks equal to the room links' MODELS = follow
  the default.
- **Both lists pick several rows** (ListBox, Extended: click / Ctrl /
  Shift / Ctrl+A); a box on a picked row ticks every picked row; Space
  ticks the picks. The name picks the row, the box ticks it. Picks are
  read from `SelectedItems`. The families search only hides rows; WPF
  drops a hidden row from the selection itself. Families keep their tick
  while models are unticked and ticked again (`_familyOn`). Test:
  `fixturedialogtest` (real dialog, off-screen, real `OnClick`).

### Apartments (Tower-1 pattern: apartment down F, rooms in H, bedrooms merged)
- **On screen they are "Redundant rooms", their rooms "Units"** (user,
  2026-10-26; "Containers" from 2026-10-19 - AUTOVENT for offices too) —
  every window, tooltip, status,
  the export/debug/apartments logs; the occupancy scope reads Units / Not
  units. **Code, settings keys (`apartments`, `apartment`), the rules file
  and `ATKINSMEC_apartments_log.txt` keep "apartment".** A container's
  rooms are still DWELLINGS for the occupancy rules (Dwelling Unit,
  bedrooms + occupants) — a per-link "Residential" tick was offered, not
  taken.
- "Apartments" tick per link (`ApartmentLinkIds`); interior placements
  named by keywords (default `IND, ID` since 2026-10-19; commas between,
  any one; each a WORD of the label - ≥3 letters starts a word, 1–2 must
  be one, so "ID" is never GRID/SIDE: `VENTINTERIOR.Named`) or ticked
  level by level in **Interior models...** (`VENTINTERIOR`; picks per
  level name). A keyword the user typed is saved per model; the default
  is saved as null, so models that never changed it follow a new default.
- `VENTAPARTMENTS.Build` asks every apartments model which room stands at
  each interior room (`GetRoomAtPoint` at 150 mm, 1.0 m, 1.5 m above its
  floor; **prefers an answer on the room's own storey**). `Map.Settle`
  joins only on the **same storey**; `OtherStorey` and `Outside` are
  counted; an apartment taking rooms from several placements is flagged
  (`Spread`).
- **One map, four users** (tree, export, airflow, fixtures): holding
  apartments get no row and no terminals. An unticked placement gives
  only its apartment rooms (`Takes`).
- `VENTROWS.Apartments` writes the groups; the importer reads a row's
  name from the block's last column (`VENTSHEET.NameOf`). The column
  beside the apartment name (G) holds the apartment's number from the
  architect's model, as text (user, 2026-10-11) - the importer never
  reads it.
- **`ATKINSMEC_apartments_log.txt`** is written on every map
  build: each model's level error, rooms per level, what each interior
  room was found in, what other models have there. **Read it before
  theorising.**

### Arranging rooms (`VENTARRANGE`, in Levels and rooms)
- Per container (`level:<name>`, `apt:<key>`) orders; an interior room
  taken out of its apartment - **Ungroup** / **Regroup** on screen -
  (**sheet only** — the map is untouched);
  renames (`Names`, rooms and apartment headings; empty or the model's
  name = no rename); find and replace across names (literal, any case,
  one level or all). An order equal to the default is not kept.
- Applied on top of the default sort in **both** `RoomsOn` and
  `CollectRooms`; **`REVITROOMS` uses the same `NameOf`** or renamed rows
  get no airflow. Nothing leaves its level or apartment.
- Search: `Words` (incl. model names), headings kept with their rooms,
  moves step past rows showing (`Stepped`). Moves and drags stay in the
  row's level and container.
- Saved per model (`ungrouped`, `order`, `rename`, `merge`,
  `arrangelink`); link ids re-pointed by label (`Remap`, `Repointed`).
- **Merge / Unmerge** (user, 2026-10-12: spaces on one FCU fed by one
  fresh air connection). **A MERGE IS ONE SIDE'S** (user, 2026-10-22:
  TA-04-04's kitchen exhaust "is serving only a kitchen" but showed as
  the foyer's and living/dining's; asked, chose FRESH AIR ONLY for both
  an FCU merge and the Merge button): `VENTARRANGE.Merges` = fresh air
  (the Merge button, an FCU's rooms, the sheet's merged fresh cells);
  `ExtractMerges` = extract, ONLY the sheet's merged extract cells
  (`VENTFLOWS.Adopt` puts each side's into its own; Model has none).
  Groups of room keys; one apartment's rooms or the level's own, never
  mixed; `Together` keeps both kinds adjacent (`Bound`); Unmerge parts
  both; ungroup/regroup/reset leave the merge. Saved as `merge` /
  `xmerge` lines. Export: `Room.Merge` / `Room.ExtractMerge` →
  `VENTROWS.MergeAir` merges each column by its own side down the group,
  top cell `IF((f1)&(f2)="","",N(f1)+N(f2))` from each row's own formula
  (a row not set up is `""`, and `""+x` is `#VALUE!` down to the Total),
  lower cells emptied; every sum taken before any cell is emptied.
  Import: `AirData.FreshMerge/ExhaustMerge`. Airflow write
  (`REVITROOMS`): `freshOf` = sheet fresh cells ∪ `Merges`, `extractOf`
  = sheet extract cells ∪ `ExtractMerges` (`VENTFCUCHECK.Groups`,
  union-find, earliest room leads); every step takes its own side's -
  in-room terminals, endcaps (`AsGroups`), the tally (`tally` /
  `tallyExtract`), bellmouths, the survey, a write. A legacy save's
  `merge` lines read as fresh air; an export before the window is opened
  again has no extract merges. Tests: ventleveltest, ventfcutest,
  officetest (`MergedTest`), ventsettingstest, arrangeflowtest.
- **Room columns** (user, 2026-10-24): "Room" m² and m, left of the
  airflows and shown with or without a sheet - `LinkedRoom.AreaM2` /
  `HeightM` / `HeightToCeiling`, read in `ReadRooms` by the export's own
  rule (`RVTLINKSELECTION.Ceilings` + `RoomHeightM`, moved there from
  REVITEXPORT: floor to the lowest false ceiling over the room's point,
  else the room's box height; the tooltip says which), carried on
  `VENTARRANGE.Row`. "-" = not read.
- **Not enclosed (N)...** (user, 2026-10-24; `VENTUNENCLOSED`, Revit-free,
  test `arrangeflowtest`): the ticked links' (and found interior
  placements') rooms with NO AREA but a location - `ReadUnenclosed`, read
  with each link's rooms in `VENTREAD` (`Answer.Unenclosed`, cached
  `_unenclosedOf`, let go on Refresh). Not enclosed, or **inside another
  room** (said so - "redundant" is the containers' word now)
  (`GetRoomAtPoint` 1 ft above it finds another room with an area -
  named). Never placed = not listed. A side button, off at 0; opens a list
  (Model, Level - the link's own, Number, Name, Why) by model, up the
  building, natural number order; **Copy** = tab-separated with a heading,
  for an email or Excel. They get no airflows - the fix is the
  architect's. The row class has PROPERTIES: WPF binds to nothing else.
- **Airflow columns** (user, 2026-10-19; `VENTFLOWS`, test
  `arrangeflowtest`): Fresh / now / Extract / now. The value is the
  SHEET's (the window's `SheetFlows` = `VENTSHEET.Resolve`, model ID
  first), **READ ONLY** (user, 2026-10-20: "the flowrate from the sheet
  should be read only" - Set airflow... and its double-click are gone);
  "now" = what the terminals carry, amber where it differs (none = 0).
  Groups as the RUN makes them (sheet merges ∪ arrangement, so a sheet
  merge across containers shows as one group though it cannot be
  adopted); a group's air on its first row, ↑ below. **The sheet's merges
  become the arrangement's** on Sheet (`FromSheet`: `VENTFLOWS.Adopt` via
  `VENTARRANGE.Merge`; refused across containers) - see the switch below.
- **"now"** (read as Levels and rooms says above: `VENTREAD.Ask.Terminals` →
  `REVITROOMS` `Mode.Survey`: the run's own walk over the ticked links,
  every level, unticked rooms too; no write, no dialog, no log): a room's
  own terminals + its part of each endcap on its units
  (`VENTFLOWS.Attribute` — weighted by the run's `Share`, so a run's own
  values read back exactly; split within a merged group by sheet value).
- **Merge by FCU** (user, 2026-10-19: "the FCU is serving the foyer, the
  kitchen and the living/dining, but it doesnt detect the foyer as
  merged"; `VENTFLOWS.FcuGroups`): the survey also returns each unit's
  supplied rooms and its fresh endcaps. **Supplied = the rooms of the
  supply terminals whose DUCTS reach it** (user 2026-10-21: TA-04-10's
  powder room "is actually not supplied from the FCU"; then its dressing,
  whose diffuser IS on the bedroom's FCU). `TraceDucts` follows each
  supply terminal (`IsSupply`: not endcap / bellmouth / fresh / extract;
  connector supply air or flowing in) BACK along connected ducts / flex /
  fittings / accessories to the equipment it reaches - at ANY of the
  unit's connectors, never through another terminal or unit. Walking OUT
  from an FCU's "supply" connectors (the first try, same day) missed
  TA-04-04's kitchen and living/dining - ducts on a connector the family
  classes otherwise. The Revit SYSTEM (`GetServedRoomKeys`) is wider than
  the ductwork (KIFAF: one FCU's system reached the mirror apartment's
  kitchen; a no-endcap unit "supplied" eight apartments) and now decides
  only for a unit no terminal's ducts reach. A supply terminal reaching no
  unit is "terminal on no FCU" on its row (`Unreached`, id on hover) and
  in the survey log with where its ducts end open. The RUN's endcap trace
  still uses the system - not changed. `ATKINSMEC_survey_log.txt` (the
  survey's whole walk, since 2026-10-21) lists the FCUs whose ducts and
  system disagree, terminal by terminal. Suggested only: a unit with a
  fresh air connection; its rooms in the one container (or level-own)
  holding MORE THAN HALF of them (stray rooms left out, named in the
  tooltip); no room claimed by two suggestions (both dropped, counted);
  not already merged together. Rows say "one FCU with ...".
- **Sheet | Model switch** (user, 2026-10-19 - replaced a Merge by FCU
  button): two ToggleButtons. **The switch decides what Write to
  terminals writes** (user: "should decide which flowrates will be
  assigned to the air terminals"; asked, chose the Model column values).
  **EACH PRESS WORKS ITS OWN MERGES OUT AFRESH** (user, 2026-10-19/20:
  Sheet "should show the merged rooms according to sheet", Model
  "according to model"; then "when pressing on sheet the spaces from
  model showing merged"). **Sheet = the sheet's merged rooms and NOTHING
  else** (`FromSheet`: `VENTFLOWS.Adopt` into an empty arrangement; no
  sheet, none) - pressed even with Sheet already down, so merges saved by
  an older version (left on Model, FCU groups in `Merges`, nothing
  saying so) are replaced; writes the Sheet columns. **Model = the FCUs'
  groups, fresh air, and NOTHING else - no extract merges** (`VENTFLOWS.ByFcu(null, ...)` - it used to
  carry every Sheet merge no FCU touched), from the terminals as last
  read; sheet merges NOT unioned; writes the **NEW VALUES** (user,
  2026-10-26: "from the new occupancy selected from the addin when
  'model' is selected" - it wrote the terminals' own totals before) - each group's
  terminal total; `WroteModel` → `ExtEventHan.WriteSheetMerges = false`
  → `REVITROOMS.SheetMerges` leaves the sheet's merged cells out of the
  grouping. **ALSO ON OPENING** (`Derive`, user 2026-10-20 again: "when
  pressing on the model button, it should be showing the spaces from the
  model that are merged..."): the button that is down has its merges
  worked out as soon as their source is here - the sheet on Sheet, the
  terminals on Model (`Terminals` / `Refill` call it once they arrive;
  until then the saved merges stand); saved only if they changed (`Same`).
  So the merges shown are ALWAYS the button's. **A hand Merge / Unmerge
  lasts until the next press, or the window is opened again.** Nothing
  picked: nothing written, either view (pick rooms or a level). The view
  showing is the arrangement's (`Merges` - saved, exported, used by the
  runs); `Arrangement.ByModel` (saved, `mergeview` line) reopens the
  window on the view it was left on. Renames/orders are shared.
  Caution: a room with an in-room fresh terminal AND an endcap on its
  unit reads both as "now" - a Model write puts that sum on both.
  Columns headed **Sheet** (Fresh, Extract) and **Model** (Fresh,
  Extract). Rows: **red** = room number shared on the level with a
  DIFFERENT room (`VENTFLOWS.Repeated`; the same room id under another
  link id - one model placed again - is not red, user 2026-10-19), else
  **yellow** = sheet ≠ model (every row of the group).
- **A double-click on a Model airflow** types a value written to that
  group's terminals AT ONCE on Enter (the user's choice; Esc or clicking
  away writes nothing) - `WriteGroup`, the same request 4 on one side.
- **Write to terminals** = request 4, the window left open → `REVITROOMS` `Mode.Write`:
  the picked rooms' groups (none picked: nothing),
  `VENTFLOWS.Overrides` (group value on its first room, 0 on the rest),
  only those groups' in-room terminals and the endcaps carrying them
  (an endcap also carries its other groups' SHEET air). Short report +
  the debug log. **Never run in Revit.**

### Logs and video
- `ATKINSMEC_debug_log.txt` on every airflow run (levels of each
  link, a room's floor checked two ways).
- `ATKINSMEC_export_log.txt` on every export: the full report.
  The dialog itself is one line - "The sheet has been exported to
  <path>" - and nothing on screen mentions "the office" (user, 2026-10-11).
- Tutorial video: `tests/ventpreview tutorial` + `tests/video`
  (`make_video.py`, `music.py` — composed, no samples, `mp4enc` — Media
  Foundation). RGB32 needs a positive stride; crop a WPF render's
  transparent margin before `convert('RGB')`.

---

## CHW VOLUME
Modal; runs with **no model open**. `CHWVOLUME` measures, `CHWEXPORT`
(Revit-free) writes, `CHWCLOUD` opens ACC models in the background
(picked from the collaboration cache; regions probed).
- **The list is a tree** (`CHWTREE`, Revit-free, test: `chwtreetest`;
  user, 2026-10-19): the open model, each link PLACEMENT below it, each
  nested link below the placement it is in (`IsNestedLink` →
  `GetParentId` is a TYPE, so the placement is the parent type's
  placement with the nearest origin, `CHWTREE.Nearest`). A model placed
  again is a `Repeat`, offered **unticked** (totals as before until
  ticked in); a doc measured once per run (`Again`).
- Search box: hides rows only (`CHWTREE.Shown`: a match + every row
  above it + the open model); Tick all / Untick all act on the rows
  showing.

## DUCT INSULATION
`DUCTINSULATIONUPDATER` (`IUpdater`) — **must never throw**; it would
abort the user's own transaction.

## SPRINKLERS
`SPRINKLERPLAN` (rooms, ceilings, placement), `SPRINKLERLAYOUT`
(geometry, test: `geomtest` with `RevitStub.cs`), `SPRINKLERRULES`.
- Capture room outlines once in an API context; then it is arithmetic.
- Unhosted head's level = highest level at/below the point
  (`LevelUnder`, corrected levels). Not "nearest" — that is the level above.
- A grid point in a hole goes to the nearest clear point, **preferring
  its own row/column line** (within 0.3 m); walls count only on the
  room's own storey (`StandsInRoom`, 300 mm).
- **A one-head room's head goes in the middle of its MAIN PART** (user,
  2026-10-25: a store's head 0.6 m off its middle, "SPRINKLERS placed
  it"): one cell = the whole rectangle round the room, whose centre is the
  OUTLINE's - pulled towards a nook or a door recess. `MainPart`: the
  largest rectangle inside the outline (holes ignored - columns are still
  dodged after), 48 samples a side, histogram; a plain rectangle's own
  centre exactly. A part then beyond reach gets its own head from the gap
  filling (the nook case: 2 heads where there was 1 - the user took that).
- Window reads only in an API context (constructor / `SPRINKLEREVENT`),
  per-link floors cached, reads have their own slot.

## ALIGN TERMINALS
Plan distance only (elevation is what is being fixed); each target
claimed once, nearest first; ties by id (`ALIGNMATCH`, Revit-free, test:
`aligntest`). The cell key packs two signed indices — negative
coordinates are where that breaks.

## SHEET QA
Framework: `IQACHECK` (`Group`, `Name`, `Explanation`, `Run`) + one line
in `QACHECKS.All()`; `QACONTEXT` built once (sheets, views, `Shared`
cache, `Scope`); a throwing check becomes a warning row; settings via
`QASETTINGS`. `QAForm.Page` = one tab.
- **`Selected` = the picked rows, or all when none** — one row IS a
  selection.
- **A new page needs four edits**: `Fill`, `Offer` (scope list), the Fix
  button's enable line, its own `Scope*_CheckedChanged`. Cloning a
  designer block: check `Controls.Add` lines and event wiring by eye (a
  control has one parent).
- Don't name helpers `Load`, `Text`, `Show`, `Update` — they hide members.
- "The view open now" must reach the collector (`QACHECKS.Look`); a
  sheet means the whole model.
- **Flowrate**: tag colours the TAG; an unset flow is a finding.
  **Only a tag that SHOWS a flow** (user, 2026-11-10: a riser tag
  reading "500x400 EAD FA/TB" was listed): `QAFLOWTEXT` (Revit-free,
  test `flowtagtest`). The API cannot say what a tag's labels show, so
  either is enough: its `TagText` reads a flow unit or a zero standing
  on its own (not "500x400", "L00", "+0.00", "0 mm"), or most of its
  TYPE's tags on elements with a flow read that flow (`Shows`, ≤40 per
  type, only for types the text did not settle) - an unset flow may
  print nothing, and `TagText` runs labels together ("500x4000 L/s").
  Text unreadable and the type unknown = reported, as before. The row
  says what the tag reads. `QAMARK` overrides (saved with the model, Clear is exact); `QAAVF` +
  `QAWATCH` live markers (`DocumentChanged` handler only raises the event,
  guards against its own writes, `Stop` in `OnFormClosed`). No view
  filters — they cannot colour a tag.
- **Grids**: `QAGRIDFIX` driven by `FixKind`, recomputes at fix time,
  `SetDatumExtentType(ViewSpecific)` before `SetCurveInView`, moves along
  the grid's own line, never vertically, skips pinned.
- **Ducts**: one walk (`QADUCTS.Of`, 3D buckets), four checks;
  `QADUCTFIX` — every change its own `SubTransaction`; a hanging tap is
  replaced (`NewTakeoffFitting`); connectors carried as (owner, id).
- **FCU**: `FCUSHEET` finds columns by heading; names matched on letters
  and digits; `QAFCUFIX` writes the TERMINALS by one ratio; flow via
  `RBS_DUCT_FLOW_PARAM`, else `Connector.AssignedFlow` on the used
  connector.
- **CHW sizes**: built-in table (`CHWSIZESHEET.Builtin`), exportable and
  importable — `Write` emits exactly the headings `Detect` looks for;
  full precision; read `.Value` never `.Text`; gpm refused. Reducers
  never listed. Additional Flow cleared on every pipe (survey first).
  `QACHWFIX` gets the whole selection; pipes via
  `RBS_PIPE_DIAMETER_PARAM`; fittings driven (`Connector.Radius` read
  back → biggest size for equal fittings → replaced); accessories never
  replaced; whole runs together, sizes checked against routing
  preferences, joints written down and mended. `QACHWROOM` slides
  fittings to keep short pipes (Leg / Branch / Teed; `Sound` checks
  joints, no turned pipe, square joints) — **`roomtest` is a hand MIRROR
  of it: change both together.** `ATKINSMEC_chw_log.txt`.
- **CHW disconnections**: pipes only; each pipe to its nearest unit
  (all pairings sorted); connector by system, then description, then
  distance; supply never to return; unclassified connectors joinable
  (option, on); ambiguity said; `QACHWALIGN.Make` arithmetic (stretch →
  shift a free pipe → slide); families and system types ticked on the
  page (none = all); the scan logs systems on both sides.

## AUTOSYNC
A timer tick only raises the event (runs when Revit is idle). Asked at
`ApplicationInitialized`; on/off never persisted; 5-minute floor in three
places; reachability probes never block (background thread, ~4 s); probe
what the model syncs to (cloud vs file). **`Document.IsModifiable` is
false outside a transaction — never guard on it.**

## NWC BATCH
Modal; reuses `CHWCLOUD`. Exporter availability asked first; view
matched by name (missing = reported); the open document is never closed;
shared settings never carry the model list. Per-model view, settings
(whole or none) and worksets (by `UniqueId` then name; ids never saved).
Cloud reads walk the regions; `Blame` reports the informative exception;
`NWCLOG` logs exception types. `QUIET` answers dialogs (tries OK, 1001,
Close, Cancel, Yes/No last), clears warnings only, `Stop` in `finally`.
`CheckedListBox` cannot multi-select; `Unpick` after filling a grid.

## EQUIPMENT CHANGER
Modeless, acts on the selection. **Five steps in order**: walk and record
joints → disconnect → swap type → resize (ducts, then fittings, read
back) → reconnect by position. `EQUIPSTOP` (Revit-free) stops at
reducer/tap/tee/terminal/equipment. Connectors matched by position.
Shape changes refused. `Verify` reads the model after commit (failures
are processed at commit). Rewrite curves keeping direction
(`EQUIPLINE.MovingIsStart`). `EQUIPFAILURES` never resolves an error.
Reach 40. `ATKINSMEC_equip_log.txt`.

## DUCT ESP
Its own calculation across this model and ticked links (`ESPCOLLECT`
reads; `ESPNET`, `ESPAIR`, `ESPSETTINGS` Revit-free, test: `esptest`).
Open ends in different models within 150 mm and facing are joined, never
within one model; near misses kept. Darcy–Weisbach/Colebrook; fittings:
family value > family coefficient > table; built-in coefficients are
rough and flagged everywhere. ESP = worst outlet + worst inlet + margin.
A port reaching no terminal is a finding. Writes only to this model's
units. Window built in code (set `AutoScaleMode.Font` explicitly; fits
the work area). `ATKINSMEC_esp_log.txt`. Never run in Revit.

## ROOM TAGS
User, 2026-10-23: "check the missing room tags in sheets and annotate
them for selected links with search bar for links and make them annotate
without duplication". Modal, window built in code (`TAGForm`, Revit-free,
callbacks from `TAGCommand`); `TAGREAD` reads, `TAGWRITE` tags, `TAGPLAN`
(Revit-free, test: `roomtagtest`, also `roomtagtest preview <folder>
[1.5]`) decides.
- Plans = floor and ceiling plans placed on sheets (no templates). A
  room is a placement's room (`link:room`) - a model placed twice is two.
- **Shown in a plan**: 2023+ asks the view
  (`FilteredElementCollector(doc, viewId, linkId)`, rooms, area > 0);
  2022 has no such collector - the band rule (`VENTLEVELMAP.StoreyOf`
  on the room's geometry floor, `GenLevel`'s name). Both: the room's
  own point (`LocationPoint` through the link's total transform) inside
  the crop box, else counted as outside the crop and left alone (the
  collector passes rooms outside a crop). A plan showing none of a
  link's rooms while rooms stand on its level gets a note ("Rooms hidden
  in the view?"); so does a hidden link.
- **Tagged** = any `RoomTag` (any type, not orphaned) of that linked
  room whose owner view, mapped to its PRIMARY, is the plan's owner.
- **Never twice**: a dependent's tags are its primary's, so tags go in
  the primary (`OwnerOf`) and a room missing in a plan and its
  dependents is one tag (`ToTag`, per owner); a second run finds nothing.
- Tags: `NewRoomTag(LinkElementId, UV host XY, ownerViewId)` →
  `ChangeTypeId` to the picked type; one transaction "Room tags"; warnings
  deleted; counted from the model after the commit, then read again.
- **Remove duplicates** (user, 2026-10-24; `TAGPLAN.Doubles`): tags of
  one room in one owner view, of one type (**Any tag type** ticked: any
  type - off by default, as a name tag + an area tag is often meant).
  Each tag knows which plans on sheets show it (`SeenIn`: its head inside
  the plan's crop). As few stay as leave every plan that showed a tag of
  the room showing one - greedy, the tag in most plans not yet covered,
  the oldest on a tie - so a room tagged once in each of two dependents'
  areas keeps both. Pinned / grouped tags stay; a tag no sheet plan shows
  is left alone. Acts on ticked plans' duplicates (a row's = the ones its
  plan shows), asks first, one transaction "Remove duplicate room tags",
  only ever deletes a `RoomTag`, counted from the model, read again.
- Window: links (search hides only, ticks stand; All / None act on rows
  showing), Check sheets → a row per plan with Missing and Duplicates
  (Fix ticks: rows with either; unticked rows remembered; sheets search;
  Tick all / Untick all on rows showing), the picked plan's missing rooms,
  duplicates and notes, tag type (Revit's default for room tags), Tag
  missing (asks first), Remove duplicates + Any tag type, Log. A link changed after a check
  drops the rows. Per model in `%AppData%\ATKINSMEC\roomtags.txt`:
  ticked links by label (default = links with rooms, saved as null; an
  unloaded link keeps its tick) and the tag type by name.
- `ATKINSMEC_roomtags_log.txt` on every check and tagging. **Never run
  in Revit.**

---

## Windows (WinForms)
All resizable: anchors on every control, `FORMSIZE` minimum (a form
naming its own `MinimumSize` is left alone). **A control between two
groups is top-anchored**, never bottom. Run `tests/anchorsim.py` after
touching a layout.

## Conventions and traps
- **Modal vs modeless.** Modeless windows (AUTOVENT, SPRINKLERS, ALIGN,
  SHEET QA, EQUIPMENT CHANGER) route every API call through their
  external event, keep form/handler/event in statics, clear them in
  `OnFormClosed`. A request raised inside a handler arrives empty — do
  follow-up work inline.
- **A modeless window acts only on its own model** (`SAMEDOC.Problem`,
  `Handler.Home`): a new one needs both halves.
- Subscribing to a Revit event is itself an API call; so is reading
  `doc.Title` — take what a window needs in its constructor.
- **A `Connector` dies with the geometry it was read from** — re-read by
  (owner, connector id) after any move.
- **Insulation and lining are `MEPCurve`s and appear in `AllRefs`** —
  filter network walks by CATEGORY (`QADUCTS.Carries`,
  `QACHWJOINTFIX.Pipework`); where a `Domain` test is the only guard,
  it is load-bearing.
- `Document.IsReadOnly` means "not right now", not "read-only file".
- **Report from the model, not from what was attempted** (commit-time
  failures).
- Logs exist because harnesses passed while real models failed. Read
  them first. **All nine are in `%AppData%\ATKINSMEC\`** (the eighth,
  `ATKINSMEC_survey_log.txt`, is Levels and rooms' terminals read; the
  ninth `ATKINSMEC_roomtags_log.txt`) beside the
  settings, through `LOGS.PathOf` (user, 2026-10-16; on the Desktop
  before). A new log goes through it too.
- **Tests live in `tests\`, never the scratchpad** (Windows cleans
  `%LocalAppData%\Temp`). Each compiles the real Revit-free files by
  relative path; run them all after any change:
  `powershell -NoProfile -ExecutionPolicy Bypass -File tests/run-all.ps1`
  (names as extra arguments run some — needs `ValueFromRemainingArguments`).
  Mutation-check a new test: break the code, see it fail.
- Write C# with the Write/Edit tools: Bash heredocs mangle `\r\n`/`\t`
  literals, and `sed -i` in Git Bash rewrites CRLF files as LF.
- `python` may be the Windows Store alias — use
  `C:\Users\moham\AppData\Local\Python\pythoncore-3.14-64\python.exe`.
  openpyxl never finishes on the office workbook — use an EPPlus probe.
- PowerShell reflection on Revit assemblies gives stale answers — compile
  or byte-scan instead.
- A Revit-free WinForm can be previewed: a scratch WinExe that includes
  it, `Show()`, `DrawToBitmap`.
