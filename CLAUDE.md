# ATKINSMEC — Revit MEP add-in suite

> **Starting a session: read `WORKPLAN.md` first** — current state, what
> has never run in a real model, open questions, things deliberately
> reverted.
>
> This file is the SHORT version: the rules and traps. The long one —
> why each rule exists, the user reports behind them, the dead ends — is
> in git: `git show 465c133:CLAUDE.md`. Read it there before reworking
> anything this file only summarises.

Ten Revit add-ins, one assembly, one ribbon panel (**ATKINS →
Mechanical**), built from one source tree for **Revit 2022–2027**.

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
- Installer: `installer/exe/` (net48 WinForms, one embedded zip). Keep
  `app.manifest` (`asInvoker`, or Windows raises UAC for a file called
  "setup"). Uninstall reads `ATKINSMEC.files.txt` — never clear the
  shared add-ins folder. Output ~20.5 MB, no admin rights needed.
- **Git**: `main`, no remote, `core.autocrlf false` (files are a mix of
  LF and CRLF — keep each as it is). A post-commit hook
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
`NwcExport`, `EquipChanger`, `EspCalc`) plus `Shared\`. **The namespace
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
`QUIET`, `WORKSETPICK`. `SpaceKey.cs` is dead code.

---

## AUTOVENT

Modeless **WPF** window (`VENTWindow.xaml`; the only WPF window —
`<UseWPF>` and `<UseWindowsForms>` both on). Work lives outside it:
`VENTSHEET` reads the workbook, `REVITROOMS` writes terminals,
`REVITEXPORT` writes the sheet, `VENTREAD` reads the model for the window.
Revit-free (and tested): `VENTLEVELMAP`, `VENTSETTINGS`, `VENTSAVED`,
`VENTFCUCHECK`, `VENTOFFICE`, `VENTROWS`, `VENTTEMPLATE`, `VENTLEVELROWS`,
`VENTLEVELNOTE`, `VENTEXPORTNOTE`, `VENTLINKFILTER`, `VENTINTERIOR`,
`VENTAPARTMENTMAP`, `VENTARRANGE`, `VENTFIT`, the dialogs
(`VENTFIXTUREDIALOG`, `VENTINTERIORDIALOG`, `VENTARRANGEDIALOG`).

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
- **Fits a 1080p laptop at 150%** (`VENTFIT`): opens no bigger than the
  work area, MinHeight 480, the body scrolls below what its rows need
  and then gives the levels list 300. **`BodyGrid`'s height is SET** —
  left to the scroll viewer, the levels row's `*` becomes Auto and the
  tree loses its virtualisation.
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

### Airflow write
- Fresh/extract system types: ticked lists, else the old name rule
  (`Fresh` / `Extract` / `Exhaust`); the two sides decided separately;
  the pre-tick IS the fallback rule.
- Endcap reach (default 1500 mm; ≤0 → default). Too large finds the
  wrong FCU.
- **Endcaps split a room's air between the endcaps bound to its units**
  (`VENTFCUCHECK`: `Tally`, `Share`, `Carried`) — the shares always add
  back to the whole. One unit, one endcap divides by one; counts per side.
- **A sheet row matches a room by name + number + level**
  (`VENTSHEET.SpaceKey`). Export and airflow write must agree on all
  three: same storey mapping, same names (`VENTARRANGE.NameOf`).

### Levels
- **The levels are THIS model's** (`GetHostLevels`); linked level names
  play no part.
- **A linked room's floor is its GEOMETRY** — the bottom of its box
  through the link's total transform (`HostElevationOf`). Level
  arithmetic (`ProjectElevation` + base point Z) was 3.49 m high on a real
  job and put every room one level up. Every level is corrected by what
  its own model's rooms/spaces measure (`RVTLINKSELECTION.LevelError` →
  `VENTLEVELMAP.LevelError`, median, 50 mm dead band) — host storeys,
  link spans, SPRINKLERS. **Never compare `Level.Elevation` with
  geometry.** `Storey.Shown` is for telling a person only.
- **Band rule** (`VENTLEVELMAP.StoreyOf`): a room belongs to the highest
  level at or under its floor, with a 300 mm allowance below. Below every
  level = in no band, left out and counted.
- Link span (`LevelSpan`): lowest/highest linked level something modelled
  stands on (allow-list `Content`, shafts and rooms excluded);
  `ElementLevelFilter` is slow, so walk inwards from each end.

### Levels tree (`VENTLEVELROWS`, tests: `levelrowtest`, `toggletest`)
- A level's tick follows its rooms (all / none / third state). Building
  the list is not choosing (tick set before `Parent`); a person's choice
  outlives a rebuild (`_roomChoice`); `_settling`/`_pushing` keep the two
  directions apart.
- **WPF sends a click on the third state to UNTICKED**: the level box is
  `OneWay` and `LevelTick_Click` decides (`ClickMeans`).
- Runs are handed the **unticked** rooms (`ExcludedRooms`), so a room
  drawn later is still processed.
- Picks ≠ ticks (file-list rules: click, Ctrl, Shift, Ctrl+A); ticking a
  picked row ticks all picked; a picked level stands for its rooms.
  Settled in `PreviewMouseLeftButtonDown`; the TreeView's own highlight
  is transparent and rows draw `IsPicked`.
- **Search** (`Search.Apply`) only hides rows (`IsShown`): ticks stand.
  While on, a level's tick, All, None, Ctrl+A, ranges and picked levels
  act on rows **showing**. Opened levels are restored on clear
  (`_openBeforeSearch`, by name). With no search, behaviour is unchanged.
- Show in plan (`VENTSHOW`, request 3): one plan, one level; frames every
  corner through the link transform; selects via `SetReferences` on 2023+.

### Export (`REVITEXPORT`, `VENTOFFICE`, `VENTROWS`; test: `officetest`)
- Three ways: fill the imported sheet (clones its rows' styles), a new
  workbook from the office template, a worksheet added to the imported
  workbook (refuses a name in use; leaves every other cell alone).
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

### Fixtures (`VENTFIXTURES`)
WCs and urinals counted into "No. of Units" (column found by heading,
shifts with the Room Number column). Sources default to the room links;
families default to `LooksLikeWcOrUrinal` (never "lavatory" — a basin in
Revit's US families). Room found by `GetRoomAtPoint` at the box middle,
300 mm in front, then the origin. Occupants are NOT counted.

### Apartments (Tower-1 pattern: apartment down F, rooms in H, bedrooms merged)
- "Apartments" tick per link (`ApartmentLinkIds`); interior placements
  named by a keyword (default `IND`) or ticked level by level in
  **Interior models...** (`VENTINTERIOR`; picks per level name).
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
  name from the block's last column (`VENTSHEET.NameOf`).
- **`Desktop\ATKINSMEC_apartments_log.txt`** is written on every map
  build: each model's level error, rooms per level, what each interior
  room was found in, what other models have there. **Read it before
  theorising.**

### Arrange rooms (`VENTARRANGE`, `VENTARRANGEDIALOG`)
- Per container (`level:<name>`, `apt:<key>`) orders; an interior room
  taken out of its apartment (**sheet only** — the map is untouched);
  renames (`Names`, rooms and apartment headings; empty or the model's
  name = no rename); find and replace across names (literal, any case,
  one level or all). An order equal to the default is not kept.
- Applied on top of the default sort in **both** `RoomsOn` and
  `CollectRooms`; **`REVITROOMS` uses the same `NameOf`** or renamed rows
  get no airflow. Nothing leaves its level or apartment.
- Dialog search: `Words` (incl. model names), headings kept with their
  rooms, moves step past rows showing (`Stepped`).
- Saved per model (`ungrouped`, `order`, `rename`, `arrangelink`); link
  ids re-pointed by label (`Remap`).

### Logs and video
- `Desktop\ATKINSMEC_debug_log.txt` on every airflow run (levels of each
  link, a room's floor checked two ways).
- Tutorial video: `tests/ventpreview tutorial` + `tests/video`
  (`make_video.py`, `music.py` — composed, no samples, `mp4enc` — Media
  Foundation). RGB32 needs a positive stride; crop a WPF render's
  transparent margin before `convert('RGB')`.

---

## CHW VOLUME
Modal; runs with **no model open**. `CHWVOLUME` measures, `CHWEXPORT`
(Revit-free) writes, `CHWCLOUD` opens ACC models in the background
(picked from the collaboration cache; regions probed).

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
  `QAMARK` overrides (saved with the model, Clear is exact); `QAAVF` +
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
  of it: change both together.** `Desktop\ATKINSMEC_chw_log.txt`.
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
Reach 40. `Desktop\ATKINSMEC_equip_log.txt`.

## DUCT ESP
Its own calculation across this model and ticked links (`ESPCOLLECT`
reads; `ESPNET`, `ESPAIR`, `ESPSETTINGS` Revit-free, test: `esptest`).
Open ends in different models within 150 mm and facing are joined, never
within one model; near misses kept. Darcy–Weisbach/Colebrook; fittings:
family value > family coefficient > table; built-in coefficients are
rough and flagged everywhere. ESP = worst outlet + worst inlet + margin.
A port reaching no terminal is a finding. Writes only to this model's
units. Window built in code (set `AutoScaleMode.Font` explicitly; fits
the work area). `Desktop\ATKINSMEC_esp_log.txt`. Never run in Revit.

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
- Logs on the Desktop exist because harnesses passed while real models
  failed. Read them first.
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
