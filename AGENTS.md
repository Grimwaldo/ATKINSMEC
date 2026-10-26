# ATKINSMEC — Revit MEP add-in suite

> **Starting a session: read `WORKPLAN.md` in this folder first.** It
> holds the current state — what is verified, what has never been run in
> a real model, open questions, and changes that were deliberately
> reverted and should not be reinstated without asking.

Six Revit add-ins in one project, one assembly, one ribbon panel
(**Atkins MEP Toolkit → Mechanical**). Built from a single source tree for **Revit
2022 through 2027**.

| Button | Entry point | What it does |
|---|---|---|
| AUTOVENT | `Command.cs` | Reads an Excel ventilation sheet and writes fresh/extract airflows onto air terminals; also exports model rooms back into the sheet. |
| CHW VOLUME | `CHWCommand.cs` | Water volume held by the pipes and fittings of two chosen piping system types, across the model and its links. Exports to Excel. |
| DUCT INSULATION | `INSULATIONCommand.cs` | Insulates ducts, fittings and accessories automatically as they are drawn, by system type. |
| SPRINKLERS | `SPRINKLERCommand.cs` | Distributes pendent sprinklers per NFPA 13 across rooms of a linked architectural model. |
| ALIGN TERMINALS | `ALIGNCommand.cs` | Moves this model's air terminals onto the nearest air terminal in a linked arch model, in plan and at the same height. |
| SHEET QA | `QACommand.cs` | QA/QC before an issue, on four tabs at the top of one window - flowrate (tags reading zero), grids (against a link, ragged ends, bubble ends), duct disconnections (hanging taps, branches never tapped in, ends that meet but are not joined) and FCU flowrates (against an Excel schedule). Each tab is a whole page: its own scope, checks, Run and findings. Grids, ducts and FCUs can also put what they found right. Can mark findings red, live. |

---

## Building and deploying

```bash
# one version
dotnet build ATKINSMEC/ATKINSMEC.csproj -c Release -p:RevitVersion=2025

# all six, and deploy each
powershell -NoProfile -ExecutionPolicy Bypass -File build-all.ps1
```

To hand it to someone else, build the installer instead:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1
```

That builds all six Release and writes two things to
`installer/output/`, both ~13 MB and both needing **no admin rights**:
`ATKINSMEC-Setup.exe` (one self-contained file) and
`ATKINSMEC-Setup.zip` (the same as a script, for IT that blocks
executables). `/silent` and `/silent /uninstall` drive the exe headlessly.

The exe is `installer/exe/`, a **net48** WinForms project — net48 ships
with every Windows 10/11, so it runs with no runtime to install. The
add-in rides inside it as one embedded zip resource; stored file by file
it was 32 MB rather than 13. **`app.manifest` must stay**: it requests
`asInvoker`, without which Windows raises a UAC prompt purely because the
filename says "setup". `installer/ATKINSMEC.iss` is an alternative Inno
Setup wizard — written, never compiled.

The installer records what it wrote in `ATKINSMEC.files.txt` next to the
files, and the uninstall reads that list. It must stay that way: the
add-ins folder is shared with RevitLookup and the Add-In Manager, so
clearing the folder would take their files too.

**`-ExecutionPolicy Bypass` is not optional here.** No execution policy
is set at any scope on this machine, so Windows PowerShell falls back to
its default of `Restricted` and refuses every `.ps1`. The flag scopes the
change to that one process. (`Set-ExecutionPolicy -Scope CurrentUser
RemoteSigned` would fix it permanently without admin, but has not been
run — it is the user's machine setting to make.) `Install.cmd` and
`Uninstall.cmd` already pass the flag, so people installing the add-in
never meet this.

**A rebuild in Visual Studio deploys Revit 2022 only.** `RevitVersion`
defaults to 2022 when nothing sets it, the solution has only plain
Debug/Release configurations, and `DeployToRevit` copies to the one
folder for the version being built. The other five keep whatever was put
there last — silently. Use `build-all.ps1` for all six.

A `DeployToRevit` MSBuild target copies the output into
`%AppData%\Autodesk\Revit\Addins\<version>\` after every build.

**Close Revit before building.** The deploy uses `ContinueOnError`, so a
running Revit does *not* fail the build — it warns and silently skips the
copy. A build can therefore "succeed" while Revit keeps loading the old
DLL. This wasted a lot of time; verify with a hash check rather than
trusting the build result:

```powershell
$d="$env:APPDATA\Autodesk\Revit\Addins\2022\ATKINSMEC.dll"
$s="ATKINSMEC\bin\Release\2022\ATKINSMEC.dll"
(Get-FileHash $d).Hash -eq (Get-FileHash $s).Hash
```

**Visual Studio deploys Debug, `build-all.ps1` deploys Release**, both to
the same folder — whichever ran last is what Revit loads. When in doubt,
build both configurations.

## Multi-version architecture

`ATKINSMEC.csproj` is SDK-style and takes one Revit version per build.
The runtime follows the version:

| Revit | TFM | Notes |
|---|---|---|
| 2022, 2023, 2024 | `net48` | .NET Framework |
| 2025, 2026 | `net8.0-windows` | .NET 8 |
| 2027 | `net10.0-windows` | .NET 10 |

- Revit API comes from NuGet (`Nice3point.Revit.Api.RevitAPI` /
  `...RevitAPIUI`), pinned per version, so versions that are not
  installed locally still build.
- `Directory.Build.props` sets `BaseIntermediateOutputPath` per version.
  It must stay there — setting it in the csproj triggers MSB3539 and lets
  one version's generated files leak into another's compile.
- **`ElementId.IntegerValue` is removed in Revit 2027** (deprecated from
  2024). All element ids go through `REVITCOMPAT.cs` (`ToValue()` /
  `NewId()`), switched by the `REVIT2024_OR_NEWER` constant, and id
  variables are `long`. Never call `IntegerValue` or `new ElementId(int)`
  directly.
- Only third-party dependency is **EPPlus**. ClosedXML, OpenXml, Office
  interop, RBush and SixLabors were unused and dropped.

---

## File map

The source is sorted into **one folder per add-in** under `ATKINSMEC\`,
plus `Shared\`. The project root keeps only `ATKINSMEC.csproj`,
`Directory.Build.props`, `ATKINSMEC.addin`, `app.config` and the old
`.bak`.

| Folder | Add-in |
|---|---|
| `Shared\` | used by more than one |
| `AutoVent\` | AUTOVENT |
| `ChwVolume\` | CHW VOLUME |
| `DuctInsulation\` | DUCT INSULATION |
| `Sprinklers\` | SPRINKLERS |
| `AlignTerminals\` | ALIGN TERMINALS |
| `SheetQA\` | SHEET QA |

**The namespace stayed flat at `ATKINSMEC`** — every file declares it
regardless of folder, so nothing needed a `using` and no type moved.
Keep it that way: folders are for finding things, not for scoping them.
The SDK globs `**\*.cs`, so adding a file to a folder needs no csproj
edit.

**The one thing folders did break, and how it is pinned.** A form's
resources are found at runtime by TYPE name —
`ComponentResourceManager(typeof(Winform))` looks for
`ATKINSMEC.Winform.resources` — and that name knows nothing about
folders. Moving the forms silently renamed them to
`ATKINSMEC.AutoVent.Winform.resources`, which nothing noticed because
neither designer actually loads resources (both `.resx` are identical
untouched WinForms boilerplate). It would have failed the moment
somebody put an image on `Winform` or `CHWForm`. The csproj now pins
both with `LogicalName`, and `DependentUpon` nests each `.resx` under
its form. **Verify with a byte scan after any such move**, since a build
cannot catch it:

```bash
grep -ao "ATKINSMEC\.[A-Za-z.]*\.resources" ATKINSMEC/bin/Release/2022/ATKINSMEC.dll | sort -u
```

**Shared**
(`Shared\`)
- `UI.cs` — `IExternalApplication`; ribbon buttons, registers the duct
  insulation updater.
- `REVITCOMPAT.cs` — ElementId compatibility shim.
- `RVTLINKSELECTION.cs` — linked-model and level pickers, shared by
  AUTOVENT and SPRINKLERS.
- `SpaceKey.cs` — **dead code**, superseded by `VENTSHEET.SpaceKey`.

**AUTOVENT** — `Command.cs`, `Winform*.cs`, `ExtEventHan.cs`,
`VENTSHEET.cs` (Excel parsing), `REVITROOMS.cs` (matching and writing),
`REVITEXPORT.cs` (rooms → worksheet).

**CHW VOLUME** — `CHWCommand.cs`, `CHWForm*.cs`, `CHWVOLUME.cs`
(measurement), `CHWEXPORT.cs` (spreadsheet, deliberately Revit-free),
`CHWCLOUD.cs` + `CHWCACHEForm*.cs` + `CHWCLOUDForm*.cs` (ACC models,
measured through a background open and closed again, never shown in the
window; picked from Revit's collaboration cache, typed only as a
fallback). This is the one button that runs with **no model open**.

**DUCT INSULATION** — `INSULATIONCommand.cs`, `INSULATIONForm*.cs`,
`INSULATIONSETTINGS.cs`, `DUCTINSULATIONUPDATER.cs` (`IUpdater`).

**SPRINKLERS** — `SPRINKLERCommand.cs`, `SPRINKLERForm*.cs`,
`SPRINKLEREVENT.cs` (external event), `SPRINKLERPLAN.cs` (rooms,
ceilings, placement), `SPRINKLERLAYOUT.cs` (geometry),
`SPRINKLERRULES.cs` (NFPA 13 table and hazard keywords).

**ALIGN TERMINALS** — `ALIGNCommand.cs`, `ALIGNEVENT.cs` (external
event), `ALIGNForm*.cs`, `ALIGNHELPForm*.cs`, `ALIGNTERMINALS.cs`
(matching and moving). Matching
is **plan distance only** — the MEP elevation is the thing being fixed,
so including it would let a badly levelled terminal match the wrong
ceiling. Each linked terminal is claimed once, greedily nearest first.

**SHEET QA** — `QACommand.cs`, `QAEVENT.cs` (external event),
`QAForm*.cs`, `QAHELPForm*.cs`, `QAEXPORT.cs` (spreadsheet, Revit-free),
`QACHECK.cs` (the framework), `QAFLOWCHECK.cs` (the flowrate check),
`QAGRIDS.cs` + `QAGRIDLINKCHECK.cs` / `QAGRIDEXTENTCHECK.cs` /
`QAGRIDBUBBLECHECK.cs` (the grid checks), `QAGRIDFIX.cs` (putting them
right), `QADUCTS.cs` + `QADUCTCHECKS.cs` + `QADUCTFIX.cs` (the duct
disconnection page), `FCUSHEET.cs` (the Excel schedule, deliberately
Revit-free) + `QAFCU.cs` + `QAFCUCHECKS.cs` + `QAFCUFIX.cs` (the FCU
flowrate page), `QAMARK.cs` (one-off tag overrides), `QAAVF.cs` +
`QAWATCH.cs` (live analysis markers on the tags).

**The window is four whole pages**, tabs at the top, each with its own
scope, checks, Run button and findings table — the two never have to
agree, and running one leaves the other's list alone. `QAGROUPS` names
the tabs; a check's `Group` decides which one it lands on.

`QAForm.Page` is one tab's worth of window (its controls, its checks,
its findings, its scope description), so the code that runs a tab is
written once rather than once per tab. Adding a third tab is a tab page
in the designer plus one more `Page`. Both findings tables get their
columns from `QAForm.Columns` in code rather than the designer, so they
cannot drift apart. `_running` remembers which page asked, since
`ShowFindings` comes back from `QAEVENT` with no idea which table it is
for. The shared buttons along the bottom — Mark, Clear, Export, and the
live markers — act on `Current`, the tab in front.

Careful with method names on the form: `Load`, `Text`, `Show`, `Update`
and friends are inherited members, and a helper named after one of them
silently hides it — `this.Text = ...` and the designer's `this.Load +=`
stop meaning what they say.

**Adding a QA check is one class and one line.** Implement `IQACHECK`
(`Group`, `Name`, `Explanation`, `Run(doc, context, settings)`) and add
it to `QACHECKS.All()`. `Group` decides which tab it lands on —
`QAGROUPS.Flowrate` or `QAGROUPS.Grids`; a new tab is a string
there plus a tab page. Anything a check needs from the window goes on
`QASETTINGS` rather than into the signature, so adding a setting does
not disturb the checks that ignore it. Adding it to `QACHECKS.All()` — the window picks it up, and the export, Show button
and severity sorting all work without further change. `QACONTEXT` is
built once per run and carries the sheets, the views placed on them and
a view-to-sheet map, so ten checks do not each walk the sheet list.
`QACHECKS.Run` catches a throwing check and turns it into a warning row
rather than losing every other check's results.

**Putting the grids right** is `QAGRIDFIX`, behind the one button in the
add-in that changes the drawing rather than the way it is coloured. It
is driven by `QAFinding.FixKind` — a string the check sets — rather than
by a switch on the check's display name, so renaming a check cannot
quietly break fixing. Only three of the findings carry one: a ragged end,
a bubble, and a grid that drifted. A grid the link does not have, or one
missing here, is deliberately left alone — drawing and deleting grids are
not decisions for an add-in.

`QAGRIDFIX` **recomputes everything from the model at the moment of
fixing** and trusts no number carried on the finding; a list left open
while somebody moved a grid must not apply a stale correction. Three
traps it handles: a grid usually carries a 3D extent shared by every
view and `SetCurveInView` will only take a view-specific one, so
`SetDatumExtentType(..., ViewSpecific)` comes first (this is what
dragging the grip does, and it leaves the grid's other views alone); a
stretch is parameterised **along the grid's own line** so it cannot
shift sideways; and a drifted grid is moved in plan only, never up or
down a level, with pinned grids skipped and reported.

**The duct page** finds every HVAC end connector with nothing on the
other side and divides them between four checks, so nothing is reported
twice: a tap or spud fitting → `QADUCTTAPCHECK`; anything else with a
duct in reach → `QADUCTBRANCHCHECK`; two ends meeting and facing each
other → `QADUCTJOINCHECK`; the rest → `QADUCTSTRANDEDCHECK` (warning,
never fixed — an open end with nothing near it is usually a deliberate
stub).

A duct belongs to the model, not to a sheet, so this page's scope is
**the whole model or levels** rather than sheets — `QASCOPE` gained
`WholeModel` and `PickedLevels`, and `QACONTEXT` now carries the
`Scope` so a model-wide check can read its own filter. The findings
carry no view, which is why the marking buttons grey out on that page:
`QAMARK` and `QAAVF` colour an element *in a view*. The Sheet and View
columns carry the system and the element kind instead.

**Four checks, one walk.** `QACONTEXT.Shared` is a
`Dictionary<string, object>` where a family of checks leaves one
expensive scan; `QADUCTS.Of` puts the network walk there so four checks
do not each collect every duct in the job. Ducts are bucketed into a
coarse 3D grid (cells twice the search reach, so only the 27 around a
point are ever looked at) — a duct too long to sample at that spacing
goes on an "always measured" list rather than being silently missed.
Standalone harness at `scratchpad/ducttest`; keep it in step.

**`QADUCTFIX` is the only thing in the suite that creates and deletes
elements**, so every single change sits in its own `SubTransaction`
inside the one outer transaction: what Revit refuses rolls back alone
and the rest still go through. Joins first (cheapest, and can make later
ones unnecessary), then taps, then branches. A hanging tap is
**replaced, not repaired** — its main side has no connector to rejoin,
being hosted on the duct's body, so the old fitting is deleted and
`doc.Create.NewTakeoffFitting` makes a fresh one. `QAFinding.FixData`
carries the connector id, since a `Connector` cannot be held across a
transaction; the fix walks the network again and matches findings to it
by (element, connector).

**The FCU page** reads an Excel schedule and compares each fan coil
unit's supply duct with the flow that schedule gives it. `FCUSHEET`
finds the columns by what the headings *say* rather than where they are,
so another office's layout still reads; a row counts as a unit when it
has both a name and a numeric flow, which is what skips the units row
and the notes without knowing where they are. It has no Revit in it, so
it is testable — harness at `scratchpad/sheettest`, run against the
user's real file.

The schedule is read in the window (Excel needs no API context) and
handed to the checks on `QASETTINGS.FcuUnits`, so a check never touches
a file. Names are matched on letters and digits only, so `FCU-02`,
`FCU -02` and `fcu_02` are one unit while `FCU-2` stays separate. Which
parameter holds the unit's name is **picked in the window** rather than
assumed — `QAFCU.ParameterNames` offers Mark, the type name, and every
string parameter the equipment actually carries.

**`QAFCUFIX` writes the TERMINALS, never the duct.** Revit works a duct's
flow out from the terminals it feeds, so the terminals are the only
thing that can be written and the duct follows. Each unit's terminals
are scaled by **one ratio** so they keep their shares — an even split
would throw away whoever balanced that branch. A unit with no terminals,
terminals reading zero, or a read-only Flow has no ratio to scale by and
is reported rather than guessed at.

**Writing a terminal's flow takes two routes, and the second one matters.**
`RBS_DUCT_FLOW_PARAM` in `UnitTypeId.LitersPerSecond` — exactly as
AUTOVENT's `WriteFlow` — works only where the family has **one** duct
connector. A family with two (common: one used, one spare) exposes no
writable instance `Flow` at all, because in Revit the air belongs to a
*connector*. So `QAFCU.Write` falls back to `Connector.AssignedFlow` on
the connector actually carrying it — the connected one where only one of
two is used. `QAFCU.Used` picks it; `QAFCU.LitresOf` reads the same way,
since a two-connector family usually leaves the instance parameter at
nothing.

`Connector.AssignedDuctFlowConfiguration` is **read-only** (`Preset` /
`Calculated` / `System`) — only `Preset` can be given a flow, and it is
a family setting the API cannot change. `Connector.DemandFlow` does not
exist. Both verified by compiling on 2022 and 2027.

`QAEVENT.DoFixGrids` **re-runs the checks inline** after committing,
rather than raising another request — `Execute` clears `Action` on its
way out, so a request raised from inside a handler would arrive with
nothing to do. This is a general trap for the modeless four.

**Two ways to show findings, and both colour the TAG** — the tag is
what is wrong on the drawing, so `QAFLOWCHECK` sets `QAFinding.ElementId`
to the tag, not the tagged element.

`QAAVF` + `QAWATCH` is the live one: Analysis Visualisation markers at
each tag head, touching neither the elements nor the view's filters, and
`SpatialFieldManager.Clear()` removes them completely. AVF does **not**
recompute itself, so `QAWATCH` subscribes to `Application.DocumentChanged`
and raises a repaint. That handler is read-only — it may only raise the
external event — and it guards against its own writes twice, by
transaction name and by a `Painting` flag, or paint and repaint chase
each other forever. `QAWATCH.Stop()` runs in `OnFormClosed`; a watcher
outliving its window would repaint for the rest of the session.

`QAMARK` is the one-off: `View.SetElementOverrides` on exactly the tags
in the list, chosen over revision clouds or detail lines because it
creates nothing. That is why the command is `Manual` rather than
`ReadOnly`. The override **is saved with the model**; `QAForm` keeps the
marked (view, element) pairs so Clear is exact, and Clear also sweeps
everything currently listed so a re-run then Clear still tidies up.

**A view-filter version existed briefly and was removed at the user's
request.** Filters match an element's *own* parameters, and a tag does
not carry its host's flow — so a filter could only ever colour the duct,
pipe or terminal, which is not what was wanted. Do not reintroduce it as
a way of colouring tags; it cannot do that.

**Installer** — `build-installer.ps1` drives it all.
`installer/exe/` is the single-file installer (`Program.cs`,
`Payload.cs` reads the embedded zip, `Installer.cs` does the copying,
`SetupForm.cs` is the window, `app.manifest` keeps UAC away).
`installer/Install.ps1` is the same logic as a script, with
`Install.cmd` / `Uninstall.cmd`. `installer/ATKINSMEC.iss` is an
uncompiled Inno Setup alternative. `installer/README.md` is written for
the person installing, not for us. `installer/payload/`,
`installer/exe/payload.zip`, `installer/exe/bin/` and
`installer/output/` are all build output.

---

## Conventions and traps

- **Modal vs modeless.** CHW VOLUME and DUCT INSULATION are modal
  dialogs shown from the command, so they run inside a valid API context
  and can open a transaction directly. AUTOVENT, SPRINKLERS and ALIGN
  TERMINALS are **modeless** and must route every Revit API call through
  an `ExternalEvent` (`ExtEventHan`, `SPRINKLEREVENT`, `ALIGNEVENT`,
  `QAEVENT`).
  Reading a linked model from a modeless form without the event is a
  latent crash. The modeless four keep the form, handler and event in
  statics on the command so they survive it returning, and clear the
  static in `OnFormClosed`.
- **Subscribing to a Revit event is itself an API call.**
  `app.DocumentChanged += ...` from a modeless form's own button or
  checkbox handler throws *"Revit is currently not within an API
  context"* — this was hit for real in SHEET QA. Both the `+=` and the
  `-=` must happen inside the external event handler. `QAWATCH.Start` /
  `Stop` are marked context-only for that reason, and `QAWATCH.Disarm()`
  exists as the no-API way to silence a watcher from `OnFormClosed`,
  where raising an event is all a dying form can do.
- **Reading `doc.Title` from a modeless form is the same trap.** Take
  what the window needs in the constructor, which runs inside the
  command's context, and hold the string. `QAForm._docTitle` does this.
- **Capture geometry once.** `SPRINKLERPLAN` extracts each room's outline
  into `SPRINKLERLAYOUT.RoomShape` while the API context is valid, so
  later recalculation is pure arithmetic and needs no Revit access.
- **Never let an `IUpdater` throw** — it would abort the user's own
  drawing transaction. `DUCTINSULATIONUPDATER` swallows everything.
- **Bash heredocs mangle C#.** Writing C# through `python - <<'PY'` in
  the Bash tool corrupts `\r\n`, `\t` and `' '` char literals. Use the
  Write/Edit tools for C# containing those, or verify afterwards.
- **Verifying against the Revit API by reflection is unreliable in
  PowerShell** — a failed `GetType`/assembly load leaves stale values in
  the variable and silently reports the previous version's answer. Two
  wrong conclusions came from this. Prefer compiling, or scan the DLL
  bytes.
- AUTOVENT writes `ATKINSMEC_debug_log.txt` to the Desktop on every run —
  the fastest way to diagnose it.
- A WinForm with no Revit types in it (e.g. `CHWHELPForm`) can be
  previewed without Revit: a scratch `net8.0-windows` WinExe that
  `<Compile Include=`s the form's two files, calls `Show()` then
  `DrawToBitmap`, and saves a PNG. Fastest way to check wording and
  layout before deploying.
- Matching test harness (no Revit needed) for ALIGN TERMINALS:
  `%LocalAppData%\Temp\Codex\...\scratchpad\aligntest`. Mirrors the
  grid bucketing and greedy one-to-one assignment of `ALIGNTERMINALS`;
  the cell key packs two signed cell indices into one long, which is
  where negative model coordinates would bite.
- Geometry test harness (no Revit needed) for the sprinkler layout:
  `%LocalAppData%\Temp\Codex\...\scratchpad\geomtest`. It mirrors
  `SPRINKLERLAYOUT`; keep it in step if you change that file.
