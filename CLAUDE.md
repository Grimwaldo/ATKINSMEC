# ATKINSMEC — Revit MEP add-in suite

> **Starting a session: read `WORKPLAN.md` in this folder first.** It
> holds the current state — what is verified, what has never been run in
> a real model, open questions, and changes that were deliberately
> reverted and should not be reinstated without asking.

Ten Revit add-ins in one project, one assembly, one ribbon panel
(**ATKINS → Mechanical**). Built from a single source tree for **Revit
2022 through 2027**.

| Button | Entry point | What it does |
|---|---|---|
| AUTOVENT | `Command.cs` | Reads an Excel ventilation sheet and writes fresh/extract airflows onto air terminals; also exports model rooms back into the sheet. |
| CHW VOLUME | `CHWCommand.cs` | Water volume held by the pipes and fittings of two chosen piping system types, across the model and its links. Exports to Excel. |
| DUCT INSULATION | `INSULATIONCommand.cs` | Insulates ducts, fittings and accessories automatically as they are drawn, by system type. |
| SPRINKLERS | `SPRINKLERCommand.cs` | Distributes pendent sprinklers per NFPA 13 across rooms of a linked architectural model. |
| ALIGN TERMINALS | `ALIGNCommand.cs` | Moves this model's air terminals onto the nearest air terminal in a linked arch model, in plan and at the same height. |
| AUTOSYNC | `SYNCCommand.cs` | Saves and synchronises every open model on a timer, five minutes or more. Asks each time Revit starts; saves locally instead when central cannot be reached. |
| NWC BATCH | `NWCCommand.cs` | Writes a Navisworks .nwc from a list of models - files on disk or models in ACC - opening each one in the background and closing it again. Needs the Navisworks exporter installed. |
| EQUIPMENT CHANGER | `EQUIPCommand.cs` | Changes the type of the selected mechanical equipment or air terminals and resizes the ductwork on them - ducts, fittings and accessories - as far as the first reducer, tap-off, tee or air terminal. |
| DUCT ESP | `ESPCommand.cs` | External static pressure of chosen units: the index run of every duct connector, worked out across this model AND any linked models whose ductwork joins it (an AHU and riser in one model, the floors in another). Exports to Excel; can write the ESP to the units. |
| SHEET QA | `QACommand.cs` | QA/QC before an issue, on six tabs at the top of one window - flowrate (tags reading zero), grids (against a link, ragged ends, bubble ends), duct disconnections (hanging taps, branches never tapped in, ends that meet but are not joined), FCU flowrates (against an Excel schedule), CHW pipe sizes (against a built-in ASHRAE table) and CHW pipe disconnections (pipes drawn to a fan coil and never joined to it). Each tab is a whole page: its own scope, checks, Run and findings. Grids, ducts, FCUs and both CHW pages can also put what they found right. Can mark findings red, live. |

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
`installer/output/`, both ~20.5 MB (14.4 before AUTOVENT carried the office workbook) and both needing **no admin rights**:
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

**THE PROJECT IS UNDER GIT** since 2026-10-06 (`main`, no remote). It
had no version control before that, so there is no history older than
the first commit. `.gitignore` keeps out `bin\`, `obj\`, the old
`packages\` folder, the installer's build output and the 54 MB tutorial
video (regenerated by `tests\video`). **`core.autocrlf` is `false` for
this repository**, on purpose: the files are a mix of LF and CRLF, and
Git for Windows' default would rewrite them on checkout. Git stores
exactly what is on disk. The identity is set for this repository only
(`git config user.name` / `user.email`); there was no global one.

**EVERY COMMIT IS COPIED TO A BACKUP REPOSITORY BY A HOOK**
(`tools\git-hooks\post-commit`, switched on by `git config
core.hooksPath tools/git-hooks`). The remote is `backup-onedrive` =
`C:\Users\moham\OneDrive\Backups\ATKINSMEC.git`, a bare repository. The
hook skips a remote a clone has not got and never fails a commit; a
push that does not go through prints a warning. Two things limit it,
both found on 2026-10-06:

- **OneDrive was not running and had no account signed in**, so that
  copy only leaves the machine once somebody signs OneDrive in. Until
  then it is a second copy on the SAME disk.
- **`D:` (the separate SATA disk, "STORE") is write-protected** - "The
  media is write protected" - which is a disk setting, not a folder
  permission. The hook already knows a `backup-d` remote: once `D:` is
  writable, `git init --bare D:\Backups\ATKINSMEC.git` and `git remote
  add backup-d D:/Backups/ATKINSMEC.git` are all it takes.

The test harnesses are in `tests\` - see **Conventions and traps** -
and `tests\run-all.ps1` runs them.

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
- **`REVIT2023_OR_NEWER`** is defined from 2023 up, for the one call
  that needs it: `Selection.SetReferences`, which can select an element
  INSIDE a link and does not exist in 2022 (AUTOVENT's "Show in plan").
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
| `AutoSync\` | AUTOSYNC |
| `NwcExport\` | NWC BATCH |
| `EquipChanger\` | EQUIPMENT CHANGER |
| `EspCalc\` | DUCT ESP |

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

**Ribbon icons** — `ATKINSMEC/Resources/Icons/<name>32.png` and
`<name>16.png`, ten of each, **drawn by `tools/make-icons.py`** and
embedded in the assembly. Regenerate with `python tools/make-icons.py`
from the repository root; it also writes
`tools/icons-contact-sheet.png`, which shows all nine big AND at 32 px.

**They are generated because ten icons that have to look like one set
is a consistency problem, not an art problem.** One palette, one stroke
width, one grid, so a tenth add-in gets an icon that matches by
construction. Any PNG can be replaced by a designer's and nothing else
changes - the loader only wants a file of the right name.

**LOOK AT THE CONTACT SHEET, AT 32 PX.** The first pass was drawn to
read at full size and four of the nine fell apart on the ribbon: the
insulation hatching went to a grey smudge, the sprinkler spray became
specks, the alignment guide's dashes vanished completely and the sync
arrowheads sat off the ends of their arc. All four were redrawn with
less detail, not more. Same argument as rendering a window.

**The resource name is PINNED with `LogicalName`**, exactly as
`CHWForm.resx` is and for exactly the same reason: the default name is
built from the FOLDER, so moving `Resources\Icons` would silently
rename all eighteen and `UI.Icon` would quietly find nothing. Verify
with the byte scan after any such move - a build cannot catch it:

```bash
grep -ao "ATKINSMEC\.Icons\.[A-Za-z0-9]*\.png" ATKINSMEC/bin/Release/2022/ATKINSMEC.dll | sort -u
```

**`BitmapCacheOption.OnLoad` IS NOT OPTIONAL** in `UI.Icon`. A
`BitmapImage` reads its `StreamSource` LAZILY by default, so the
`using` block disposes the stream before anything draws it - which
shows up as a **blank button**, never as an error. `Freeze()` follows,
because Revit puts these on its own UI thread. A missing or broken icon
is caught and returns null rather than throwing: the ribbon is built in
`OnStartup`, and taking the whole add-in down over a picture would be
the wrong trade.

Harness at `tests/icontest` - loads all eighteen **out of the real
built DLL** by the same three steps `UI.Icon` uses, and asserts each
decodes, is the size its name claims, and is frozen. Reading resources
needs no Revit, so it runs with no model and no Revit installed. The
byte scan proves the NAMES; this proves they decode.

**Shared**
(`Shared\`)
- `UI.cs` — `IExternalApplication`; ribbon buttons and their icons
  (`Icon` / `Dress`), registers the duct insulation updater.
- `REVITCOMPAT.cs` — ElementId compatibility shim.
- `FORMSIZE.cs` — the one thing about resizable windows that cannot go
  in a designer: a minimum size taken from the form once it has been
  built and scaled. Called from every form's constructor.
- `RVTLINKSELECTION.cs` — linked-model and level pickers, shared by
  AUTOVENT, SPRINKLERS, ALIGN TERMINALS and SHEET QA. `GetLinks` gives
  **one row per link INSTANCE** (every `RevitLinkInstance` with a loaded
  document), not per model; all four match rows by `Id`, never by name.
  **A model placed more than once is labelled by `LinkLabels`**, at the
  user's request - `ARC-000020.rvt : 2 : Tower B`: the instance's "Name"
  parameter (Revit numbers them 1, 2, ...; people rename them), then
  the shared site from `Element.Name` with the word "location" dropped
  and `<Not Shared>` left off. **Kept short on purpose**: office file
  names run to forty characters and the first render cut "location
  Tower B" off the end of the row - the one part that tells the two
  apart. A title that occurs once is still just the title.
  **A partial class**: its Revit-free half, `RVTLINKROOMS.cs`, holds
  `LinkedRoom`, `RoomKey` and `RoomsOn` (linked rooms onto this model's
  levels), so the tests can reach them.
- `SpaceKey.cs` — **dead code**, superseded by `VENTSHEET.SpaceKey`.

**AUTOVENT** — `Command.cs`, **`VENTWindow.xaml` + `.xaml.cs` (WPF)**,
`ExtEventHan.cs`, `VENTSHEET.cs` (Excel parsing), `REVITROOMS.cs`
(matching and writing), `REVITEXPORT.cs` (rooms → worksheet),
`VENTOFFICE.cs` (the office's own workbook as the export template,
Revit-free), `VENTROWS.cs` (capturing and stamping rows, shared by both
exports, Revit-free), `VENTTEMPLATE.cs` (the room/level data and the
worksheet-name rule, Revit-free),
`VENTLEVELMAP.cs` (a linked room onto a storey of this model,
Revit-free), `VENTSETTINGS.cs` (what it used to guess, Revit-free),
`VENTSAVED.cs` (each model's set-up, remembered between sessions,
Revit-free), `VENTLINKFILTER.cs` (the search box over the linked
models: every word typed must appear in the name, any order, any case;
it filters the list's VIEW only, so a ticked model it hides still counts
and the card says so - Revit-free, in the heading row so the card is no
taller), `VENTINTERIOR.cs` (which placements are interior models, by
keyword and per level, Revit-free) + `VENTINTERIORDIALOG.cs` (the
"Interior models..." dialog, built in code, Revit-free), `VENTREAD.cs` (everything the window reads from the model,
in the constructor or the external event - never from a handler),
`VENTFCUCHECK.cs` (a room whose units and endcaps disagree,
Revit-free), `VENTLEVELNOTE.cs` (what the levels card says, Revit-free
so the preview renders the real wording), `VENTEXPORTNOTE.cs` (what
the export tab says, likewise), `VENTFIXTURES.cs` (plumbing fixtures
counted into rooms) + `VENTFIXTUREDIALOG.cs` (which models and families,
Revit-free, built in code), `VENTAPARTMENTS.cs` (asking the models
which interior room stands in which apartment) + `VENTAPARTMENTMAP.cs`
(deciding it, the same-storey rule and the record - Revit-free, the
other half of the same partial class) + `VENTAPTLOG.cs` (what the map
found and why, `Desktop\ATKINSMEC_apartments_log.txt`), `VENTARRANGE.cs`
(the rooms arranged by hand for the sheet, Revit-free) +
`VENTARRANGEDIALOG.cs` (the "Arrange rooms..." dialog, built in code,
Revit-free), `VENTSHOW.cs` ("Show in
plan" for the rooms picked in the levels list), `VENTLEVELROWS.cs` (the
level and room rows of the levels tree, their ticks and the rules for
picking several - `Picks` - Revit-free).

**THE ENDCAP TRACE RUNS OVER ENDCAPS, NOT OVER FCUs**, and that is what
made the next thing invisible. Each endcap finds the unit it feeds,
asks which rooms that unit's supply serves, and is written **the whole
of those rooms' sheet values**. Nothing ever iterated the FCUs, so
nothing noticed when a room's units and its endcaps did not line up:

- a room served by **two FCUs with one endcap** gets its entire fresh
  air written onto that one endcap, and the other unit's branch gets
  nothing;
- the same room with an endcap on **each** unit gets its entire fresh
  air written **twice**, because neither endcap knows about the other.

Both read as a clean success in the log - `-> served rooms: X` then
`WRITE fresh <full value>`. **Raised by the user as a question**, which
is how it came to light at all.

**IT SPLITS BETWEEN THE ENDCAPS**, and the rule has changed twice,
each time at the user's request - the history is the explanation:

1. **Report only** - leave the numbers, list the rooms.
2. **Split by the FCUs** - half per unit. The right number per unit,
   but it LOST AIR: a room whose second unit had no endcap drawn got
   half its fresh air written and the other half went nowhere.
3. **Split by the endcaps** - what it does now. Collect every unit
   serving the room, count the endcaps on them, and divide the room's
   air equally between THOSE. Every endcap serving the room gets the
   same share, and **the shares always add back up to the whole**:
   nothing written twice, nothing lost.

So two units with one endcap between them now put the WHOLE room on
that one endcap - and that is reported, because the bare unit carries
none of the room's air in the model and that is a modelling gap. Two
units with two endcaps are half each. A unit with two endcaps and a unit
with one are a third each: **the endcaps decide, not the units.**

**"An endcap on a unit" means one the trace BOUND to it** - the nearest
equipment within the endcap reach, the binding that already existed.
Not a fresh search around each unit: an endcap standing between two
units would then be counted against both, and the double-write this
exists to stop would come straight back.

**THE TALLY, THE SHARE AND WHAT EACH ENDCAP IS WRITTEN ALL LIVE IN
`VENTFCUCHECK`**, which is Revit-free - `Tally`, `Share` and `Carried`.
`REVITROOMS` only collects (which unit serves which room, which endcap
bound to which unit) and writes what `Carried` returns. Before this the
tally was inline in `REVITROOMS`, so the one piece the split's
correctness depended on was the one piece no harness could reach.

**ONE UNIT WITH ONE ENDCAP DIVIDES BY ONE**, so the ordinary room -
nearly every room in nearly every model - is untouched; the harness
asserts that first. A count of zero or less is treated as one: dividing
by it would crash or give infinity, and treating it as none would write
a zero over a real number. It cannot actually be zero there - the
endcap asking is itself one of the count - so that is a guard, not a
rule.

**STAYING QUIET ON THE ORDINARY CASES IS THE HARD PART.** A report that
fires on every room is one nobody reads:

- **two units and no endcaps anywhere is not a finding** - the room's
  air comes from the in-room terminals. The harness caught this as a
  regression when splitting first replaced reporting;
- one unit and one endcap is normal;
- the counts are kept **per side**, because a correctly drawn unit
  usually has one fresh and one extract endcap and adding them together
  would halve every room in the job.

Harness at `tests/ventfcutest`, 29 checks. It builds small JOBS -
units, the rooms they serve, the endcaps bound to them, sheet values -
and runs them through exactly the calls `REVITROOMS` makes. **The
property everything hangs on is CONSERVATION**: across a whole mixed job
(shared rooms, a bare unit, a unit with two endcaps, a unit serving two
rooms) the endcaps are written EXACTLY what the sheet says, in total.

**TWO GUESSES BECAME SETTINGS**, both at the user's request, both
previously constants or keyword searches buried in `REVITROOMS`.

**1. WHICH DUCT SYSTEM TYPES ARE FRESH AND WHICH ARE EXTRACT**
(`VENTSETTINGS.FreshSystemTypeIds` / `ExtractSystemTypeIds`, two
ticked lists on the assign tab). It decided by searching the system and
system-type names for `Fresh`, `Extract` and `Exhaust` - which reads
fine for `ATK_ME_Fresh Air` and misses `ATK_ME_OA Supply` completely,
so every terminal on that system silently got nothing written. Same
shape as the CHW sizing page pre-ticking "return" by name and missing a
return system not called one.

**THE NAMES ARE STILL THE FALLBACK, UNCHANGED.** Nothing ticked means
exactly what AUTOVENT did before the lists existed, so no existing
model changes behaviour. **The two sides are decided separately**:
ticking the fresh types must not quietly make the extract side strict,
or somebody setting up one side at a time would watch the other stop
working for no visible reason. A system whose TYPE cannot be read
(`id == 0`) falls back to the name rather than being dropped - the
fallback cannot contradict an explicit tick, because a system called
"Fresh" is by definition not one of the ticked ids.

**THE PRE-TICK IS THE SAME RULE AS THE FALLBACK**, and the harness
asserts it directly: if the two ever drift, merely opening the window
would change what a model does, because the lists would arrive ticked
for something other than what the add-in was already doing.

**2. HOW FAR AN ENDCAP MAY LOOK FOR ITS FCU**
(`VENTSETTINGS.EndcapReachMm`, default 1500). An endcap does not serve
the space it stands in - it feeds an FCU serving somewhere else - so
AUTOVENT walks from the terminal to the nearest FCU connector. That
walk was `const double connectorTolerance = 4.92` feet. It describes
how somebody draws, not anything about the add-in.

Too small and an endcap finds no FCU and gets nothing written; **too
large and it finds the WRONG FCU** - a unit in the next ceiling void -
and writes another room's air onto it, which nothing downstream would
catch. **Zero or negative falls back to the default** rather than
matching nothing: an empty box would otherwise mean "no endcap may be
more than 0 mm from its FCU" and silently write nothing on every endcap
in the job.

Harness at `tests/ventsettingstest`, 21 checks against the real
`VENTSETTINGS`, most of them asserting what must NOT change: the name
fallback behaves exactly as before, a type that looks fresh but was not
ticked is not fresh, ticking one side leaves the other alone, and the
default reach is still the 4.92 ft constant it replaced.

The ticks and the reach are read **at the press**, not as they are
changed, so a half-finished edit cannot be picked up by a run somebody
already started.

**EACH MODEL'S SET-UP IS REMEMBERED** (`VENTSAVED`,
`%AppData%\ATKINSMEC\autovent_settings.txt`), at the user's request.
Until 2026-10-06 none of it was: the fresh/extract ticks went back to
the name guess **every time the window opened** - so a system ticked by
hand, the whole reason the lists exist, was silently guessed again next
time - and the reach, the fixture choices and the Apartments ticks were
statics that lasted until Revit closed. Being statics they also **leaked
into the next model opened in the same session**: another project's
link ids as fixture sources count no fixtures at all.

- **What is remembered**: the links ticked for rooms, the Apartments
  ticks, the fresh/extract ticks, the endcap reach, the fixture switch,
  sources and families. Not the level or room ticks - those follow the
  workbook.
- **One block per MODEL, keyed by where it lives** (`VENTWindow.ModelKey`):
  an ACC model by its model GUID, a workshared model by its CENTRAL
  (every person's local copy is a different file), otherwise the file.
  `Document.CreationGUID` would be neater but exists only from 2024, and
  a key that differed between versions would split one project's set-up
  in two. A model never saved has no key and is not remembered.
- **A model never set up gets the DEFAULTS** (`VENTWindow.Restore`), not
  what the previous model left in the statics.
- **Ticks that only repeat the name guess are saved as "follow the
  names"** (`VENTSAVED.ChoiceOf` returns null), so a system type added
  later is guessed like any other rather than frozen out by an old list
  - the rule the Fixtures dialog already follows. Unticking the guess IS
  a choice, of nothing, and is kept apart from null.
- **A link is remembered by id AND label** and settled by
  `VENTSAVED.Resolve`: the id across the whole list first, then a label
  naming exactly ONE loaded link not already taken - so a link removed
  and linked again is found under its new id, while a label two
  placements share (`IND : 1` twice) ticks neither. `WORKSETPICK.Find`'s
  order, for its reason. **A link not loaded right now keeps its choices**
  (`KeepUnloaded`) for when it comes back.
- **Saved on every change** - a link or system-type tick, Apartments,
  the fixture switch and dialog, at the press and on closing - but only
  once the window has finished filling itself (`_ready`): a save from
  halfway through would write an empty links list over the remembered
  one. Read, replace this model's block, write through a temporary
  file; a file that cannot be written says so once in the status bar
  and never stops a run.
- The status bar says when a model's set-up was restored, and when it
  was last saved.

Tested at `tests/ventsettingstest` (79 checks, 36 on `VENTSAVED`): the
round trip, null against an empty choice, no leaking between models'
blocks, de-DE, a title with a tab in it, replacing one model's block
and leaving the other's, every `Resolve` case, `KeepUnloaded` and
`ChoiceOf`. **Never run in Revit** - in particular `ModelKey` on an ACC
model and on a workshared local.

**THE LEVELS ARE THE MAIN MODEL'S, THE ROOMS ARE THE LINKS'**, at the
user's request. It used to group rooms by the LINKED model's own level
names, which was the wrong organising structure and showed three ways:

- the architect names levels to the architect's convention, so the
  sheet came out saying `Level 01` while every drawing, schedule and
  view in the MEP model says `GR_GROUND FLOOR`;
- two links with two conventions produced two sets of level headings
  for one floor;
- the levels OFFERED in the window were whatever the ticked links
  happened to have rooms on, so the list was never the same twice.

`RVTLINKSELECTION.GetHostLevels` reads this model's levels - it takes
no link ids, which is most of the point - and every linked room is put
on the storey it actually sits on. `GetLevelNamesWithRooms` is
untouched beside it, because **SPRINKLERS still wants exactly what it
does**.

**A LINKED LEVEL'S ELEVATION IS NOT IN THE HOST'S COORDINATES** until
the link's transform is applied (`RVTLINKSELECTION.HostElevationOf`).
A link set up by shared coordinates is usually a plain translation, and
it is the Z of that translation that would otherwise put every room in
the building on the wrong floor.

**AND `Level.Elevation` IS NOT IN ANY MODEL'S INTERNAL COORDINATES -
NEVER COMPARE IT WITH GEOMETRY OR PUT IT THROUGH A TRANSFORM.**
User-reported as *"listing the rooms of a level inside another level,
probably stepped up by 1"*. Autodesk's own words: with the level
type's Elevation Base at Project it is *"relative to project origin"*,
at Shared *"relative to shared origin"*, and *"The project base point
represents the origin of the project coordinate system"*. Architects
often type their levels to the SURVEY POINT so the heads read the site
datum; that datum height, put through a link transform (internal to
internal), lands every room the whole datum too high - with storeys
about that tall, exactly one level up. All of AUTOVENT now goes through
**`RVTLINKSELECTION.InternalZ(level)`** = `ProjectElevation` (always
from the base point) + the base point's `Position.Z`: the host's
storeys (`GetHostLevels`), each room's floor (`HostElevationOf`) and
the link span (`LevelSpan`). `Storey.Shown` keeps `Level.Elevation` for
TELLING a person only.

**Never run in Revit** - and `BasePoint.Position` being the base
point's place in internal coordinates is read from the API's design
(its `SharedPosition` is the shared one), not from a run. It only
matters where a base point has been moved vertically; the survey-point
case is fixed either way. **To check it in a real model, two places**:
hovering a room in the tree says which linked level it hangs from and
its floor height, and a level says its height and the floors it takes;
and an airflow run's `ATKINSMEC_debug_log.txt` lists every level of
this model and of each link (as shown -> in this model -> which level
takes it) and checks one room's floor two ways - from its level and
from its own bounding box, which is geometry and so certainly internal.
**"DISAGREE" there means the level arithmetic is wrong for that
model.**

**AND IT WAS WRONG - A ROOM'S FLOOR IS NOW ITS GEOMETRY.** The first real
job (kifaf, 2026-10-10) put every room of both architect's models ONE
LEVEL UP: *"LEVEL 03 showing no rooms despite having rooms in the
models"*. The apartments log proved it from geometry alone - interior
rooms with floors at 37.12 m stood physically INSIDE the architect's
TA-04 rooms, which the arithmetic put on the level at 40.62 m, and
TA-05-04's box began at exactly 40.620 m while its level said 44.11 m.
The links sit at Z = -3.49 m, and `ProjectElevation + BasePoint.Position.Z`
came out 3.49 m high: on that model the base point's height is counted
twice, so `ProjectElevation` there is from the INTERNAL origin, and
Autodesk's "relative to project origin" never says which. Rather than
guess again:

- **`HostElevationOf` takes the bottom of the room's own BOX** through
  the link's total transform (`LowestZ`, every corner). A box is in its
  model's internal coordinates, full stop. The level is only used for a
  room with no box.
- **Every LEVEL is corrected by what its model's own rooms and spaces
  measure** (`RVTLINKSELECTION.LevelError(doc)` -> the Revit-free
  `VENTLEVELMAP.LevelError`: the MEDIAN of box floor minus level floor
  over up to 60 of them, nothing under 50 mm). Used for this model's
  storeys (`GetHostLevels`), the link spans (`LevelSpan`), the debug
  log's level list, and SPRINKLERS' `LevelUnder` levels. A model with no
  rooms or spaces gets no correction - exactly the old behaviour.
- Both logs say how far each model's levels were out.
- Tested in `ventleveltest` with the kifaf numbers: by the arithmetic
  TA-05-04 is on 06A and a TA-03 room on 04A (LEVEL 03 empty); by
  geometry, or a level corrected by its rooms, TA-05 is on 05A and TA-03
  on LEVEL 03. Mutation-checked (an average instead of the median fails).

SPRINKLERS had the same comparison and is fixed too - see
`SPRINKLERPLAN.LevelUnder` under SPRINKLERS.

**A ROOM BELONGS TO THE LEVEL WHOSE BAND ITS FLOOR STANDS IN** - from
that level of the main model up to the next (`VENTLEVELMAP.StoreyOf`),
at the user's request: *"the level name of the main model doesnt have
to be same level name of the selected linked models ... the rooms only
need to be in the level boundaries of the main model"*. Linked level
names play no part anywhere - not in the window's counts, the export or
the airflow write.

**IT REPLACED "THE NEAREST STOREY"**, which was this file's own choice
and was wrong in a way worth remembering: it measured from the linked
room's LEVEL to the nearest main-model level, so a mezzanine at +2.5 m
between levels at 0 and 4 m went to the 4 m one, and a main model whose
levels are not at the architect's heights had a floor's rooms split
between two headings by whichever was closer.

**THE ROOM'S OWN FLOOR, NOT ITS LEVEL** (`HostElevationOf` adds
`Room.BaseOffset`): a room dropped 150 mm for a wet area, or raised
onto a mezzanine, sits where it sits whatever level it hangs from.

**THE 300 mm ALLOWANCE** (`VENTLEVELMAP.Allowance`) is what the old rule
was really protecting. An architectural floor is finished floor level
and a services level is often slab level, and a bare "at or below" drops
**a whole floor of rooms onto the storey beneath** whenever the arch
floor sits a few millimetres low. So a floor up to 300 mm BELOW a level
counts as on it; no storey is 300 mm tall, so it cannot take a room off
the floor it is really on. The highest level at or under the floor
wins; of two at one height, the first in the sorted list, every run.

**A ROOM BELOW EVERY LEVEL OF THE MAIN MODEL IS IN NO BAND** and is left
out rather than put on the lowest level (the old rule did that) - the
main model has no level for, say, an architect's basement it does not
model. It is COUNTED, never silent: the export report says how many,
the "no rooms" error says so when that is the reason, and the airflow
write logs it per link.

**BOTH DIRECTIONS ARE ONE DECISION.** The level name is part of the key
a sheet row is matched by (`REVITROOMS.KeyOf`), so if the export wrote
this model's storey names and the read-back looked up the linked
model's, every row would miss and nothing would ever be written.
`REVITEXPORT.CollectRooms` and `REVITROOMS` use the same mapping, and
changing one without the other silently breaks the round trip.

The window now says what each level will actually do - `124 room(s)` or
`no rooms` against each - rather than listing every level in the
project and leaving somebody to find out by running it. With no sheet
loaded the levels that HAVE rooms are pre-ticked.

**EACH LINK ROW SAYS WHICH OF THIS MODEL'S LEVELS IT COVERS**, at the
user's request, on a second, quieter line (`LinkTemplate`):
`B1_BASEMENT  to  P3_PODIUM 3   -   412 room(s)`.

**THE SPAN IS THE LINK'S MODEL CONTENT; THE ROOMS ARE ONLY COUNTED.**
`RVTLINKSELECTION.LevelSpan` takes the lowest and the highest of the
link's levels that something modelled STANDS ON (`Element.LevelId`) and
puts each on a storey of this model by the same band rule as the rooms.
It was worked out from the ROOMS at first (and from the link's bare
levels for a link with none); it changed when the user asked for
**shaft openings to be ignored** - which only means anything once the
span is read from what is modelled. A shaft is usually constrained to
some level well above or below the building and would stretch the
span to reach it.

- **What counts is an allow-list** (`Content`): every MODEL category
  the link has, less `NotContent` - shaft openings, rooms/areas/spaces
  (counted separately), masses, model and sketch lines, reference
  planes, scope and section boxes, cameras, levels, grids, nested links
  - and less imported CAD (categories with a POSITIVE id). Views and
  annotation are out without being named, not being model categories.
- **`ElementLevelFilter` is a SLOW filter**, so an empty level costs a
  scan of the whole link. The levels are walked INWARDS from each end
  (`FindIndex` / `FindLastIndex`) and stop at the first that carries
  something - only the empty levels at the very top and bottom are paid
  for, never every level.
- **A level only a wall's TOP reaches is not counted** - nothing stands
  on it. In a real model the roof is on the top level, so this costs
  nothing, and it keeps the test to one cheap question per level.
- Content on a level below every level of this model reads
  `below B1_BASEMENT  to  ...`; rooms there are still counted and
  skipped (`412 room(s), 12 below`). **The short `12 below` is
  deliberate**: the two nearly always come together - rooms down there
  stand on a slab down there - and the long form ran off the end of the
  row at 100%. The row's tooltip (`Tick.Tip`) has both lines in full.

It reads every room of every link, so only AUTOVENT calls it; the other
three add-ins keep a plain `GetLinks`. **Never run in Revit** - built
for all six and rendered at 1x and 1.5x, but which levels a real
link's content actually stands on has not been seen.

**THE LEVELS LIST IS A TREE, AND EACH LEVEL OPENS TO ITS ROOMS**, at the
user's request - number and name, the link's name too when more than
one link is ticked. `RVTLINKSELECTION.RoomsOn` gives both the count
beside a level and the list under it, through the SAME band rule the
export and the airflow write use, so what a level shows is exactly what
a run would process there. It is arithmetic over each link's rooms as
already READ (`ReadRooms`, through `VENTREAD` - see "THE WINDOW NEVER
READS THE MODEL" below), so a tick costs nothing. Levels start closed and the tree virtualises,
so thousands of rooms cost nothing until somebody opens a level; the
ones that were open stay open when a link tick rebuilds the list.

**EVERY ROOM HAS A TICK**, at the user's request: a ticked room is
exported to the sheet AND has its airflow written from it; an unticked
one is left out of both. The level's tick FOLLOWS its rooms - ticked
when all are, empty when none are, the third state when some are - and
a click on it (or All / None) ticks or unticks every room under it. The
label counts them: `GR_GROUND FLOOR     6 of 8 room(s)`.

The rules live in `VENTLEVELROWS` (`LevelRow`, `RoomRow`,
`LevelRow.Build`, `Picks`), Revit-free, tested at
`tests/levelrowtest` (43 checks: 22 on the ticks, 21 on picking)
and compiled into `ventpreview`, so the render is the real behaviour.
What is worth knowing:

- **BUILDING THE LIST IS NOT CHOOSING.** A room's tick is set BEFORE it
  is attached to its level (`Parent`), so nothing is recorded; only
  once attached does a change reach `RoomChosen`.
- **A PERSON'S CHOICE OUTLIVES A REBUILD** (`VENTWindow._roomChoice`,
  by room key). Ticking another link or opening a workbook rebuilds the
  list; rooms nobody touched follow their level's pre-tick as before,
  rooms somebody touched keep what they were given.
- **`_settling` and `_pushing` keep the two directions apart**: working
  the level's tick out from its rooms must not push back down onto
  them, and a click pushing down must not settle the level halfway
  through its rooms.
- **ONLY THE BOX TICKS.** The number and name sit outside the
  `CheckBox`, so clicking a row to PICK it cannot untick it. A level's
  label was inside its box until rows could be picked; it is outside
  now too, and a click on a level's name picks the level.
- **WPF SENDS A CLICK ON THE THIRD STATE TO *UNTICKED*.** The level box
  is `IsThreeState="False"` so that only the CODE sets the third state -
  and `ToggleButton.OnToggle` then turns null into FALSE, so a click on
  a part-ticked level unticked every room of it. This file said the
  opposite for a while; **tested at `tests/toggletest`**, through
  the real `OnClick`. (An automation peer's `Toggle()` does NOT raise
  `Click`, which is why the first version of that test proved nothing.)
  The level box is now bound **`OneWay`** and `LevelTick_Click` sets the
  row from what it WAS: ticked goes to unticked, unticked or part-ticked
  goes to ticked. The binding survives WPF's toggle and the box follows
  the row again as soon as the row changes - also tested. A room's box
  has no third state and stays `TwoWay`.

**THE RUNS ARE HANDED THE UNTICKED ROOMS**, not the ticked ones
(`RVTLINKSELECTION.ExcludedRooms`, keys from `RoomKey`, whose one
definition is `VENTWindow.RoomRow.KeyOf` - "link id:room id", the key
`REVITROOMS` already filed rooms by). A room drawn in the link after the
window was filled was never in the list and so could not be ticked;
this way it is processed as it always was rather than silently dropping
out. A partly ticked level still counts as selected. The export's
confirm dialog and report, and the airflow run's status line and log,
all say how many rooms were left out.

**SEVERAL ROWS CAN BE PICKED**, at the user's request, the way a file
list picks them: a click picks one, Ctrl+click adds or takes one away,
Shift+click picks from the last click to this one AS THE TREE SHOWS IT
(levels, and the rooms of open levels, top to bottom - a closed level's
rooms are passed over), Ctrl+Shift adds a range, Ctrl+A picks every row
showing, and a click on the empty space under the rows lets go. The
arrow keys pick as they move, Shift+arrow as a range.

- **A PICK IS NOT A TICK.** The ticks still decide what a run does; the
  picks say what an ACTION applies to. Ticking the box of a PICKED row
  ticks every picked row the same way (Space does the same); the box of
  an unpicked row ticks that row alone, as it always did. Picking alone
  never ticks anything.
- **A PICKED LEVEL STANDS FOR ALL ITS ROOMS**, even closed - for Show in
  plan and for ticking alike (`Picks.Rooms`, `Picks.Tick`). A room
  picked on a picked level is counted once. Levels are ticked before
  rooms, so a level and one of its own rooms picked together both end
  up as asked.
- **A TreeView SELECTS ONE ITEM**, so the tree keeps its own pick on the
  rows (`IsPicked`) and draws it itself: its `SystemColors.Highlight*`
  are made Transparent and each row's `PickRow` border colours itself
  from `IsPicked`. The TreeView's own selection still moves, invisibly,
  because that is what the arrow keys carry on from.
- **THE PICK IS SETTLED IN `PreviewMouseLeftButtonDown`**, before the
  TreeView selects the row, and the click is not marked handled so the
  tree still takes focus. A click on a `ToggleButton` (a tick, or the
  arrow that opens a level) or on the `ScrollBar` leaves the picks
  alone. `SelectedItemChanged` then ignores anything with the mouse
  button down - only a KEY moving the selection is a pick of its own.
- **Picks survive a rebuild**, by level name and room key, as the open
  levels already did - ticking another link must not throw them away.

**"SHOW IN PLAN"** (the button beside All/None, or a double-click ON A
ROOM - on a level the double-click opens and closes it, as a tree
always does) raises request 3 on `ExtEventHan`, and `VENTSHOW` does the
work, because changing the view and zooming are API calls and the
window is modeless. The button shows EVERY PICKED ROOM; a double-click
shows that one room. It:

- takes each room's box through its link's TOTAL transform, EVERY
  corner (a rotated link turns an axis-aligned box into one that is
  not), and frames the box round ALL of them;
- **ONE PLAN SHOWS ONE LEVEL**: picks spread over several levels show
  the level most of them are on (the higher in the list on a tie), and
  the status bar says how many picked rooms on other levels are not in
  that plan, rather than quietly framing part of what was asked for;
- picks a plan of the level the rooms were PUT UNDER in this model - the
  open view if it is one and the rooms are inside its crop, otherwise
  the best of that level's floor and ceiling plans: one whose crop
  contains them, then floor before ceiling, mechanical before other
  disciplines, main view before dependent, then by name;
- sets `ActiveView` (allowed in an external event: no transaction is
  open, which is all the setter asks) and zooms with
  `UIView.ZoomAndCenterRectangle`, a quarter of the box's size and at
  least a metre round it;
- selects them all through `CreateLinkReference` +
  `Selection.SetReferences` on 2023 and up; on 2022 they are framed and
  the status bar says they could not be selected;
- answers in the status bar, never with an exception - rooms or a link
  that have gone (counted, the rest still shown), another model in
  front, no plan of that level, or a crop region they are outside of
  are all said in a sentence.

It has **never been run in Revit** - the tree was rendered at 1x and
1.5x in `tests/ventpreview` (the stub picks a level and a range of
rooms through the real `Picks`, so the render shows both drawn and the
live button), and all six versions build.

Harness at `tests/ventleveltest`, 27 checks (49 with the apartments, below) against the real
`VENTLEVELMAP`: the mezzanine that "nearest" got wrong, the allowance to
the millimetre (299 mm below stays, 301 mm drops a storey), a room below
the lowest level answering -1, level names the architect never used,
an unsorted list giving the same answer, and levels sharing a height
settling the same way every run.

**THE SHEET CAN BE BUILT RATHER THAN FILLED** - three ways on the
export tab, `REVITEXPORT.Way`:

1. **Fill the worksheet I imported** - what it always did.
   `REVITEXPORT.Rewrite` works by CLONING THE STYLES off the level and
   room rows already there, which is why it needs a workbook and says
   *"The worksheet has no existing level/room rows to replace"* when it
   has none. Right answer when somebody has a sheet they want filled.
2. **A new workbook, from the office template** - needs nothing
   imported at all.
3. **Add a worksheet to the one I imported** - the same sheet, added to
   the imported workbook. Everything already in it is left exactly as
   it was (the harness compares every cell of every existing sheet), and
   **a name already in use is refused rather than overwritten**.

**THE LAST TWO START FROM THE OFFICE'S OWN WORKBOOK** (`VENTOFFICE`), at
the user's request - *"API TWIN TOWER PODIUM VENTILATION
CALCULATIONS"*, built into the DLL byte for byte as
`Resources\VentTemplate.xlsx` (LogicalName
`ATKINSMEC.VentTemplate.xlsx`). It REPLACED a six-column layout of the
add-in's own design (`VENTTEMPLATE.Build`, deleted, along with its
harness `scratchpad/venttemplate`). **To revise the calculation, replace
that file and rebuild** - no code changes, and `tests/officetest`
runs the whole export against whatever is there.

What the new worksheet is:

- **the office layout to the column** - title block, headings, colours,
  merges, print titles, hidden columns - **plus a Room Number column**,
  inserted after the Space block (so at I, and everything from the old
  I rightwards moves one column right, title block included);
- **one blue level row per level ticked**, its rooms, a Sub Total, and
  the Total adding every Sub Total;
- **name, number, area and height from Revit** into the room rows, and
  **every formula kept and every TYPED value left blank** - the user's
  rule, *"the cells which have no formulas will be blank"*;
- **the three ASHRAE worksheets** (Table 6-1, Table 6-4, Reference) the
  formulas look up, **untouched** - ASHRAE Reference stays HIDDEN, as
  the office has it, and a copy brought into somebody's workbook is
  hidden too;
- the title block's labels kept and its values blanked, then **project
  name and job ref from the model's Project Information** and **part of
  structure = the worksheet's name**. Revit's placeholders ("Project
  Name") count as empty. Who calculated, who checked and the dates are
  not the model's to know;
- the **equipment summary** under the Total kept with its SUMIFs and
  unit labels, **its unit tags, remarks and typed airflows cleared** (they
  were the API job's; the tags are also what the "served by" drop-down
  offers), and the office's scratch between the summary and the lists
  (more tags, `LEFT`/`RIGHT`, `4350l/s`) blanked. The lists that feed
  the drop-downs are left exactly as they are.

**WHICH WORKSHEET IS COPIED: TOWER 2, THEN TOWER-1, THEN MLCP.** The
user named MLCP, Tower 1 and Tower 2 as interchangeable, and they share
one layout to the column - but not one set of formulas. Comparing what
each one's rooms have in common showed MLCP has been typed over:
**"Total Exhaust (Max. with rounded up value)" - the very column
AUTOVENT reads the exhaust back from - is typed in MLCP and calculated
in the Towers**, MLCP's S.No is typed where the Towers' is a running
number, and MLCP's "Airflow for Balancing" is `=fresh x 0.9`, which is
`#VALUE!` on every room until an occupancy is picked. ("Tower 1", with a
space, is an OLDER layout with different columns and is not a
candidate.)

**A ROOM ROW IS WHAT MOST OF THE OFFICE'S ROOMS HAVE, NOT THE FIRST
ONE** (`VENTOFFICE.Consensus`). The office rows are not uniform: MLCP
marks 26 of its 159 rooms bright green, Tower-1 two in red, and people
have typed over formulas (`=AL16*1.05`, an area of `=68.56+9.2+9+15.05`).
Each column takes the style most rows have and the formula most rows
have; where most hold a typed value it is left blank. **A formula with
no cell in it (`=3.9-0.3`) counts as a typed value.**

**A SUB TOTAL CANNOT BE SETTLED THAT WAY**, because each sums its own
block and no two read alike - `SUM(J12:J18)`, `SUM(J22:J29)`. So each is
first rewritten relative to its own rooms (`VENTOFFICE.Refs.Relative`:
`{first}`, `{last}`, `{self}`, or an offset from `{first}`), and a
column keeps its formula only where MOST Sub Totals agree. Needed
because most of Tower 2's carry scratch sums past Remarks
(`=AL16+AL17+AL18`, different rooms on every level): copied from the
first, they became `#VALUE!` on every level of the new sheet.

**ROWS GO IN AND COME OUT *INSIDE* THE BLOCK** (`InsertRow(first + 1)`),
never at its edge: a range spanning the block only follows it when rows
are added strictly within it. The equipment summary's SUMIF ranges had
drifted in the office file (MLCP sums one unit over 12:209 and the next
over 49:292, into the summary itself), so they are widened to the whole
block first, carried by the resize, and trimmed to exactly the rooms
after. **The Total is rebuilt, not shifted** - MLCP's had already lost
three of its levels, and after a resize a reference to a deleted row is
`#REF!`.

**THE ROW TEMPLATES ARE READ BEFORE THE RESIZE AND WRITTEN AFTER IT**, so
an absolute reference below the block (`$H$172`, the occupancy test's
pointer into the lists) has moved in between. `Refs.ShiftR1C1` moves
those - and never a reference to ANOTHER SHEET, whose rows are not this
sheet's (`'ASHRAE 62.1 - Table 6-1'!R5C1:R103C10` must not move). The
harness asserts `$H$` lands the same distance under the Total as in the
office file, for all three patterns, growing and shrinking.

**DROP-DOWNS ARE REBUILT** (`Lists`, `ClearValidations`,
`AddValidations`). The office's are in pieces, one per level; each
column takes the one covering most of its rooms and applies it to every
new room row. **A drop-down reading THIS sheet that feeds a VLOOKUP into
another is replaced by that table's first column**: MLCP's occupancy
list read `$H$321:$H$417`, the tail of its category list and ninety
blanks. Cross-sheet ones (exhaust category, nature of use) and Yes/No
are the office's and kept.

Three EPPlus facts this cost:

- **a validation's address must be SPACE-separated** - EPPlus throws
  *"Multiple addresses may not be commaseparated"*, though Excel writes
  either;
- **a whole-row validation is written back as `46:197`** once rows move,
  which openpyxl refuses outright and Excel's schema does not describe.
  MLCP has one over 150 whole rows (the occupancy list applied to the
  summary and everything right of it - a slip), so a validation spanning
  every column is dropped (`DropWholeRows`);
- **EPPlus writes `=+IF(` back as `=IF(`** on save, in sheets it never
  touched. Same formula; the harness's "untouched" comparison ignores a
  leading `+`.

**THE FIRST REAL EXPORT "CAME OUT EMPTY" - AND EVERY CELL WAS RIGHT.**
User-reported. The copy carried the office file's saved VIEW too: Tower 2
was saved in Page Break Preview, scrolled to row 297. **EPPlus's
`FreezePanes` freezes relative to the sheet's `TopLeftCell`** (its own
docs say so), so the frozen "headings" became rows 297-305 and scrolling
began under them - a job whose rooms end at row 25 opened on blank cells
with no way to scroll up. 365 checks passed, because every one of them
read CELLS. So `Fill` now sets Normal view and `TopLeftCell = "A1"`
**before** freezing, puts the cursor in the scrolling pane, and `Show`
keeps the tab strip from starting past the new sheet (the office file's
`firstSheet="2"` scrolled the new tab out of sight). The harness now
asserts where the sheet OPENS as well as what is in it.

**"Fill the worksheet I imported" had the same hole** - it keeps the
user's own sheet, view and all, so a sheet saved at row 297 and
rewritten shorter opens on blank rows too. `VENTROWS.Home` takes it back
to A1 and re-freezes the same rows and columns from there (they have to
be re-frozen: Excel counts them from the top-left cell). **A split the
sheet does not have reads as `NaN`** - Tower 2 freezes columns only - and
`(int)NaN` is `-2147483648`, which made the first version of `Home`
throw inside its own `catch` and quietly do nothing. Caught by the
harness.

**THE OFFICE WORKBOOK'S BROKEN NAMES ARE NOT COPIED** - 23 `#REF!`
names and two into OTHER workbooks (`[1]AHU!$A$10`, `OFFSET([2]Logo!`),
which are what make Excel ask about "links to external sources" on
every open. `Tidy` drops names containing `#REF!` or `[` and clears the
external links - in the TEMPLATE only. Somebody's own workbook's names
are never touched.

`VENTROWS` holds the row machinery both exports share (`Capture`,
`ApplyTemplate`, `RewriteRows`, the room number column). It moved out of
`REVITEXPORT` unchanged, Revit-free, so the harness can reach it.

`VENTTEMPLATE.CleanName` still applies Excel's naming rules (31
characters, none of `: \ / ? * [ ]`) HERE rather than letting EPPlus
throw halfway through a run, and the window says so when it has had to
change what was typed. A new workbook's sheet may not be named like an
ASHRAE sheet.

**WATER CLOSETS AND URINALS ARE COUNTED INTO "No. of Units"**, at the
user's request - *"fixtures FROM LINK, no occupants"*. Table 6-4's
per-fixture exhaust (`Toilets - public`: 25 L/s per water closet or
urinal at light use) is the office formula `units x rate`, and "No. of
Units" was counted off the drawings by hand. `VENTFIXTURES.Count` counts
the plumbing fixtures standing in each linked room and both exports
write the count; **occupants are deliberately NOT counted** - "No. of
Occupants" is left as the office sheet has it.

- **The column is found by its heading** (`VENTSHEET.UnitsKeywords`:
  "no" + "units"), which "No. of Keys", "No. of Bedrooms" and "No. of
  Occupants" cannot match, and **it shifts with the inserted Room
  Number column** (`VENTROWS.ShiftColumns`) - AE in the new sheet, AD in
  the office's own file. A room with no fixtures is left BLANK (the
  office rule for typed values), and a worksheet with no such column
  gets nothing written and says so in the report.
- **Which models**: `VENTSETTINGS.FixtureSources`, EMPTY = the links the
  rooms come from. Not every link: an architect's model draws its WCs,
  and a plumbing model linked as well would count every one twice -
  choosing it is a person's decision. 0 is this model.
- **Which families**: `VENTSETTINGS.FixtureFamilies` by FAMILY NAME (ids
  differ between models), EMPTY = the name rule
  `LooksLikeWcOrUrinal` - water closet, WC as a word of its own,
  toilet, urinal, squat; never lavatory (in Revit's own US families a
  "Lavatory" is a WASH BASIN), basin, sink, shower, bath, drain or
  bidet. **The dialog pre-ticks by the same rule and stores "follow the
  defaults" again whenever the ticks still say what the defaults say**
  - the system-type lists' rule: opening it never changes an export.
- **Which room a fixture stands in** is asked of each ROOM LINK
  (`Document.GetRoomAtPoint`, Revit's own index - not a loop over every
  room), the point put through the fixture model's transform and back
  through the room link's inverse, so a plumbing model's WC finds an
  architect's room. Tried at three points, 150 mm above the bottom of
  the fixture: **the middle of its box first**, because a wall-hung
  WC's origin is ON the wall face - the room's boundary, where the
  answer could as well be the room behind; then 300 mm along its
  `FacingOrientation`; then its origin. A counted fixture in no room is
  reported, not dropped silently.
- The window says what will be counted on one line under the export
  hint (`VENTEXPORTNOTE.Fixtures`, short - the first version ran off the
  end at "from 1..."), and where from in its tooltip; the **Fixtures...**
  dialog changes either. The three export hints became one paragraph
  each to give that line room - rendered at 1x and 1.5x.
- The dialog's families are read ONCE per window through the external
  event (`VENTREAD`, per model), and the dialog opens when they arrive;
  `VENTSETTINGS.FamiliesFrom` adds them up for the models ticked in it.
  **The dialog cannot ask for them itself: an external event does not
  run while a modal dialog is up** - and it used to read them straight
  from its callback, outside any API context.

Proven in `tests/officetest` against the REAL office workbook:
the column is found and is "No. of Units", it sits right of the Room
Number column, 6 and 4 land in the two toilets, a lobby stays blank,
and with `Toilets - public` / light use picked **the office's own
formulas make 6 x 25 = 150 L/s - the Total Exhaust AUTOVENT reads back**.
The name rule is 22 checks in `tests/ventsettingstest`. **The
counting itself has never run in Revit.**

**APARTMENTS FROM ARCHITECTS' MODELS, THE ROOMS INSIDE THEM FROM
INTERIOR MODELS**, at the user's request - *"in some projects we have apartments in the ARCH
model and the rooms inside the apartments are in the IND model ... can
we follow the same sequence/pattern"*, pointing at the office
workbook's Tower-1 sheet. That sheet has 38 apartment groups, read with
an EPPlus probe: the APARTMENT's name in the Space column F, merged
DOWN over its rooms (`F108:F109`), G merged down the same and empty,
each room's OWN name in H, and "No of Bedrooms" (L) merged down -
one figure per dwelling unit. Area, height, occupancy and every formula
stay per room. An ordinary room merges F:H across its one row instead.
Tower 2 and MLCP have no apartments.

- **SEVERAL MODELS MAY HOLD APARTMENTS, AND THE INTERIOR MODELS ARE
  FOUND, NOT CHOSEN** - the user's second request: *"apartments can be
  extracted from multiple linked arch models, so when selecting arch
  model it has to find the IND model the lies in the same floors of
  that arch model and verified from the main model that may have
  different IND model instances in different floors"*. It replaced a
  single "Apartments from" combo (`ApartmentLinkId`), which could name
  one model and only searched the TICKED links:
  - **an "Apartments" tick on every link row** (`Tick.IsApartments`,
    `VENTSETTINGS.ApartmentLinkIds`, empty = none, the default, which
    is exactly what AUTOVENT always did). Ticking it ticks the row too
    - an architect's model has corridors and lobbies as well - and the
    row can be unticked afterwards: its rooms then only group the
    others;
  - **`VENTAPARTMENTS.Build(doc)` takes no link ids**: it searches the
    loaded placements that are INTERIOR MODELS (below), never the
    apartment ones, because an interior model is typically placed once
    per band of floors, each placement its own link instance, and nobody
    should have to tick fourteen of them on the links list. For each
    room it asks every apartments model which of its rooms is there
    (`Document.GetRoomAtPoint` at the room's own point, then the middle
    of its box, through both transforms - VENTFIXTURES' move);
  - **WHICH PLACEMENTS ARE INTERIOR MODELS: A KEYWORD, THEN TICKS LEVEL
    BY LEVEL** (`VENTINTERIOR`, Revit-free, and the **Interior
    models...** button beside Refresh, `VENTINTERIORDIALOG`), at the
    user's request (2026-10-09): *"instead of identifying IND model where
    their rooms sit, you can identify the instance links in each level
    defined by keyword from user, and the user selects the links found
    in each level"*. It used to search EVERY loaded placement.
    - **The keyword** (`VENTSETTINGS.InteriorKeyword`, default `IND`)
      names the candidates by VENTLINKFILTER's rule - every word in the
      label, any order, any case. **Empty = every placement**, which is
      exactly the old behaviour, kept as the fallback.
    - **The dialog lists each candidate under every level of THIS model
      where it has ROOMS** (`VENTINTERIOR.Find`: each room's floor by
      `HostElevationOf` + `VENTLEVELMAP.StoreyOf` - the rule the map
      itself places rooms by, so a placement is offered on exactly the
      levels the run will search it on). The keyword filters live.
    - **Ticks still saying "everything found" are NOT a choice**
      (`VENTINTERIOR.Choice` returns null, `InteriorChosen` false): the
      keyword keeps deciding, so a placement linked later is picked up.
      Once a tick is changed the exact picks are kept per level -
      `InteriorPicks`, level name -> placement ids - **"none" included**.
      The rule `VENTSETTINGS`' system-type lists and the Fixtures dialog
      already follow: opening a dialog never changes a run.
    - **A pick is per LEVEL** (`VENTINTERIOR.Allowed`, asked in
      `VENTAPARTMENTS.Place` per room): a placement ticked on L04 gives
      only its L04 rooms; a level whose placements were all unticked
      searches none. Chosen, a ticked placement counts whatever its name.
    - **The dialog cannot read the model** - it is modal, and an
      external event does not run under a modal dialog - so the button
      first asks VENTREAD for the rooms of every loaded placement not
      read yet and opens the dialog when they arrive (`Take`), the
      Fixtures dialog's pattern. Refresh cancels a pending open.
    - **`VENTREAD.ApartmentsKey()` now carries the keyword and the
      picks** (`VENTINTERIOR.Key`), so changing either builds the map
      again; the key ignores order and case.
    - **Remembered per model** (VENTSAVED): `interiorkeyword` (absent =
      the default; written empty for "every placement") and
      `interiorchosen` + one `interior <level> <id> <label>` line per
      pick, resolved id-first then by a unique label like every other
      link list, a placement not loaded now kept for when it returns.
    - Tested in `tests/ventsettingstest` (finding, the 300 mm allowance,
      what the run searches, what OK keeps, the key, and the file round
      trip - mutation-checked); rendered at 1x and 1.5x by
      `tests/ventpreview` (`autovent-interiors*.png`). **Never run in
      Revit.**
  - **AN INTERIOR ROOM IN NO APARTMENT USED TO VANISH WITHOUT A WORD.**
    User-reported on the first real job (2026-10-09, kifaf, HVC-000100):
    *"the apartments rooms from IND model are not exported in the excel
    sheet under the apartment from the arch model"*. The export had
    `LEVEL 04A` with `ARC-000010`'s 28 core rooms and not one room of the
    seven IND placements ticked there, and nothing anywhere said why. An
    interior placement gives only the rooms inside apartments (`Takes`),
    so a room `GetRoomAtPoint` found in nothing was simply never read.
    Now:
    - **`Map.Outside` / `OutsideOn`** (in `Settle`, Revit-free, tested in
      `ventleveltest`) count them per storey, and the links-card line,
      its tooltip, the export report and the airflow log all say how
      many and where;
    - **`VENTAPTLOG` writes `Desktop\ATKINSMEC_apartments_log.txt` on
      every build of the map** (levels list, export, airflow write -
      whichever ran last owns it): this model's levels, every
      apartments model's rooms PER LEVEL with their names (a level with
      only corridors and stairs shows as exactly that), and per interior
      placement per level how many rooms joined, stood in an apartment
      on another level, or in none - with examples, and **for a room in
      none, whether it lies inside an apartments-model room IN PLAN and
      that room's height range**. That is the difference between "the
      apartments are in another architect's model" and "the two models'
      floors do not line up". Also each placement's origin and whether
      it is a NESTED link. **Read it before theorising.**
    - **The apartment is asked for at 150 mm, then 1.0 m and 1.5 m above
      the interior room's floor** (`PointsOf`). `GetRoomAtPoint` answers
      only inside a room's VOLUME, so 150 mm missed every apartment drawn
      from a finished floor more than 150 mm above the interior model's.
      The old points come first, unchanged; the same-storey rule still
      decides whether a higher answer counts.
    - **WHAT THE FIRST REAL JOB'S LOG SHOWED - AND WHAT WAS FIRST MISREAD
      FROM IT.** On kifaf `LEVEL 04A` the architect's apartments seemed
      too few and one `2BDR-A1` took rooms from four interior placements,
      and that was put down to the architect's rooms. **It was the level
      arithmetic** (above, under "A ROOM'S FLOOR IS NOW ITS GEOMETRY"):
      every architect's room was one level up, so `LEVEL 04A` was
      showing LEVEL 03's apartments, and a LEVEL 03 apartment whose
      volume reaches into the floor above was being matched with the
      interior rooms of 04A. **`ApartmentOf` now prefers an answer on
      the room's own storey** over the first room its low points hit,
      for exactly that apartment.
    - **An apartment that takes rooms from more than one interior
      placement is now NAMED** (`Map.PlacementsIn`, `Spread`,
      `DescribeSpread`) in the export report, the airflow log and the
      links-card tooltip. Not refused: one interior model placed per
      floor legitimately gives many apartments from one placement, and
      the other way round cannot be ruled out for every office.
    - **The log asks every OTHER loaded model** (not an apartments model,
      not an interior placement, not one the keyword names, and only one
      with rooms) what stands at up to 40 interior rooms per level, so it
      says which model - if any - has the apartments drawn there.
      `Near` now looks at the room's OWN level only: a box on another
      level was noise.
  - **an UNTICKED placement gives only the rooms inside apartments**
    (`Map.Takes`); a ticked one gives all its rooms as before.
    `Map.RoomLinks` is the ticked links then the placements found, and
    the tree, the export, the airflow write and the fixture count all
    iterate it - **fixtures are still COUNTED from the ticked links**
    (an interior model drawing its own WCs as well as the architect's
    would count every one twice) but may land in any of those rooms.
- **VERIFIED ON THIS MODEL'S LEVELS** (`Map.Settle`, Revit-free): a room
  joins an apartment only when both FLOORS are on the same storey of
  the main model by the band rule (`VENTLEVELMAP.StoreyOf`). Standing
  inside it in space is not enough - a duplex's upper bedroom stands
  inside the apartment drawn on the floor below, and a placement
  dropped on the wrong floor stands inside the apartments of the floor
  it was dropped on. Such a room is left an ordinary room and COUNTED
  (`OtherStorey`, once however many models said so); the window's
  tooltip, the export report and the airflow log say how many. Of two
  models both on the right storey the first asked wins (by id, every
  run).
- **The window says what was found, by THIS model's levels**: an
  interior placement's second line becomes `in apartments on L04_LEVEL
  04 to L10_LEVEL 10  -  ARC... : 1` (**floors first** - the row cuts
  the line off and the first render lost them), from `Map.Pairs` via
  `VENTLEVELMAP.Floors`; the line beside Refresh sums it up
  (`VENTLEVELNOTE.Apartments`) and its tooltip lists every pairing
  (`Map.Describe`). Room rows in the tree name their link whenever the
  rooms come from more than one placement, counted from the ROOMS
  since interior placements are not ticked.
- An apartment with a room inside goes in `Holding`; one with nothing
  inside (a corridor in the architect's model) stays an ordinary room.
- **ONE MAP, FOUR USERS, AND THEY MUST AGREE**: the levels tree
  (`RoomsOn` - interior rooms listed as `APARTMENT  /  room`,
  grouped), the export (`CollectRooms` - `Holding` apartments are no
  row, interior rooms carry `Room.ApartmentKey/Apartment/Number`), the
  airflow write (`REVITROOMS` - `Holding` apartments are NOT candidates
  for a terminal) and the fixture count (`VENTFIXTURES.Count` - nor for
  a WC). **The last two matter most**: a bedroom terminal stands in
  the bedroom AND the apartment at the same floor, and the old rule
  breaks that tie by link order - it would have landed on the
  apartment, which has no sheet row, and written nothing.
- **`VENTTEMPLATE.Ordered`** keeps an apartment's rooms together (they
  are one merged group, nothing may come between): an apartment sorts
  among the rooms by its own number and name, its rooms inside it by
  theirs. `CompareRooms` moved there from `REVITEXPORT` as `Compare`.
- **`VENTROWS.Apartments`** writes the groups AFTER the rows, for both
  exports: undoes the room template's F:H merge on those rows, puts each
  room's name in the block's last column, the apartment's at the top of
  the first, merges F and G down column by column as the office does,
  and No of Bedrooms down. A one-room apartment has nothing to merge. A
  one-column Space block writes `apartment - room` in the one cell.
  The block's width is the Space HEADING's merge (`VENTSHEET.SpaceEnd`,
  `SheetLayout.SpaceEndCol`) and "No of Bedrooms" is found by its
  heading (`BedroomsCol`); both shift with the inserted Room Number
  column.
- **The IMPORTER had never read this pattern** and now does
  (`VENTSHEET.NameOf`): a row's name is the block's last column when it
  has text that is not part of a merge reaching back to the Space
  column, else the Space column. Before, an apartment's second and later
  rooms were skipped outright (an empty, merged Space cell) and its
  first was keyed by the APARTMENT's name - so no apartment room could
  ever have matched a room in the model.
- **Fill the imported worksheet** clears every merge in its block before
  writing (a sheet exported with apartments carries their vertical
  merges), and both `Capture` and its choice of template row ignore a
  name merged DOWN - an apartment's first row would otherwise teach
  every ordinary room the wrong shape.

Proven in `tests/officetest` against the real office workbook (19
new checks, 427 in all): eight rooms handed over shuffled come out with
each apartment together; three groups written; F14:F15 and G14:G15 for
the two-room apartment, F16:F18 for the three-room one, No of Bedrooms
merged per apartment; names in H, numbers in the Room Number column,
area per room; H unmerged on apartment rows while an ordinary room
keeps F:H; a one-room apartment unmerged; no `#REF!`/`#NAME?` after
EPPlus calculates; and **the real importer reads all eight back, each
apartment room keyed by its OWN name and none by an apartment's**.
`tests/ventleveltest` (49, 22 new) runs the REAL `Map.Settle`,
`Takes`, `RoomLinks`, `Describe` and `VENTLEVELMAP.Floors`: a slab 50
mm under the finished floor still joins; the duplex is refused and
counted; of two models the right storey wins whichever was asked
first; wrong in every model counts once; a room below every level
joins nothing and is not counted; an unticked placement gives only its
apartment rooms; nothing is read twice. The window renders at 1x and
1.5x (`ventpreview` renders `autovent-links-apt*.png` scrolled to the
interior placements). **The Revit half has never run** - in particular
whether `GetRoomAtPoint` finds the apartment from an interior room's
point in a real pair of models.

**THE ROOMS CAN BE ARRANGED BY HAND FOR THE SHEET** (`VENTARRANGE`,
**Arrange rooms...** under All/None/Show in plan), at the user's request
(2026-10-10): *"select any IND room and make it ungrouped from ARCH
rooms so it can be added directly to the excel sheet ... arrange the IND
rooms grouped under ARCH room by dragging in the list so it will come
with same arrangement in the excel sheet, do the same also for ARCH
rooms but keep everything under its level under its level"*.

- **Taking an interior room out of its apartment changes the SHEET
  only** (`Arrangement.Ungrouped`): it is written as a row of its own,
  by its own name. The apartments map is untouched, so the airflow write
  and the fixture count do exactly what they did - a room is keyed by
  its own name grouped or not, which `officetest` proves by reading the
  arranged sheet back through the real importer. An apartment whose
  rooms are ALL taken out has no row left (it is still `Holding`).
- **Orders are per CONTAINER** - a level's top rows (`level:<name>`:
  ordinary rooms and apartments, each apartment one unit) or one
  apartment's own rooms (`apt:<apartment key>`). Nothing can move
  between containers, so nothing leaves its level or its apartment;
  dropping anywhere else is refused in the dialog.
- **An order that is only the default is not kept** (`SetOrder`) - the
  system-type lists' rule - so a room drawn later still lands by
  number, and a row dragged away and back is forgotten, not frozen.
  Keys an order names that no longer exist are passed over; rooms it
  never knew follow, in default order.
- **ONE RULE, TWO PLACES, ON TOP OF THE DEFAULT SORT**:
  `RVTLINKSELECTION.RoomsOn` (the levels list - which therefore shows
  the sheet's order, and a taken-out room without its "APT / " prefix)
  and `REVITEXPORT.CollectRooms` (every export way). `VENTTEMPLATE.Room`
  gained `Key` for it. The export report says what was arranged.
- **The dialog cannot work rows out for itself** - the window hands it
  a function over the SAME `RoomsOn`, so the dialog, the list and the
  sheet cannot disagree. Drag (an insertion line shows where), Move
  up/down or Ctrl+Up/Down; Take out / Put back act on every picked
  room; Reset this level forgets it all. Nothing is kept until OK.
- **A SEARCH BOX** beside the Level picker (at the user's request): the
  links search's rule (`VENTLINKFILTER` - every word, any order, any
  case, found INSIDE other words, so "apt a" finds APT B too) over each
  room's number, name, model and apartment - written under or standing
  in - so an apartment's name finds its rooms and the ones taken out of
  it (`VENTARRANGE.Words` / `Shown`). A found room always comes with its
  apartment's heading. It only HIDES rows: Move up/down step past the
  next row SHOWING (`Stepped`), and the line under the list counts the
  matches on every other level as links that open it - which is why the
  dialog's function now returns every level in one go. Esc in the box
  clears it rather than closing the dialog; Ctrl+F returns to it.
- **Saved per model** (VENTSAVED: `ungrouped`, `order <container>
  <keys...>`, `arrangelink <id> <label>`). Keys carry link ids, so
  every link they name is saved with its label and a link loaded again
  under a new id is re-pointed (`VENTARRANGE.Remap`, `Resolve`).
- Tested: `ventleveltest` (25 - through the real `RoomsOn`, both
  mutation-checked), `officetest` (7 - the real office workbook comes
  out in the chosen order, merges intact, read back by the real
  importer), `ventsettingstest` (5 - the file). Rendered at 1x and 1.5x
  (`autovent-arrange*.png`). **The dragging itself is not exercised by
  any harness** - only drawn - and **never run in Revit**.

**A ROOM NUMBER IS WRITTEN AS TEXT** (`Numberformat = "@"`). `0123` is
not `123`, and a column of room numbers Excel has turned into integers
matches nothing on the way back in.

**THE ROUND TRIP IS THE TEST.** Harness at `tests/officetest`, 427
checks against the REAL workbook with the real `VENTOFFICE`, `VENTROWS`,
`VENTSHEET` and `VENTTEMPLATE` compiled in: every pattern, a job smaller
and larger than the pattern's block, a new workbook, adding to the
office file itself and to a workbook with no ASHRAE sheets. Each output
is read back **through `VENTSHEET.excelsheet()`, the actual import
path** (every room under its own level), then **calculated by EPPlus**:
an occupancy is picked on one room and its Rp must match Table 6-1,
fresh air must come out, and nothing may be `#REF!` or `#NAME?`.
`officeembed` loaded the BUILT 2025 DLL and exported through its own
embedded resource, which is the one thing the harness, reading the file
from disk, cannot show - it was lost with the scratchpad. `officetest`
now reads `ATKINSMEC\Resources\VentTemplate.xlsx`, byte for byte the
Downloads copy it used to read.

**`#DIV/0!` IN A FRESH SHEET IS THE OFFICE'S**, not a fault: the Sub
Total ratios (`=AM15/K15`, OA over EA) and the summary's percentages
divide by an exhaust nobody has entered yet. The same cells read
`#DIV/0!` in the office's own file wherever a unit has no flow.

The DLL grew from about 0.8 MB to 1.87 MB with the workbook in it, in
each of the six versions.

**THE FIRST LAYOUT OF THE NEW TAB WAS CLIPPED** - six controls down one
column, and the name box and the Export button were simply not on
screen. Caught by `tests/ventpreview`, which is the only way it
ever would be; two columns now, and the longest of the three hints was
shortened after a render showed it reaching the card's bottom edge with
no margin at all.

**A RADIO WITH `IsChecked="True"` RAISES `Checked` WHILE THE XAML IS
STILL BEING PARSED**, so every control declared after it is still null.
`ExportWay_Changed` guards on all six before doing anything - this is
the `CheckedListBox` trap in `NWCForm` in a new place, and a window
that throws while it is being built never appears at all.

**THE ONLY WPF WINDOW IN THE SUITE**, at the user's request; the other
eight add-ins are still WinForms and the two sit in one assembly
perfectly well. **`<UseWPF>` AND `<UseWindowsForms>` must both be on**
in the csproj, or the XAML is never compiled and the window is not
found at run time.

**The move was cheap because none of the work was ever in the form.**
`VENTSHEET` reads the workbook, `REVITROOMS` writes the terminals,
`REVITEXPORT` writes the sheet back; the window only orchestrated them.
Worth keeping true. It is still MODELESS, so every Revit call goes
through `ExtEventHan` - a WPF button handler is no more an API context
than a WinForms one.

**THE WINDOW NEVER READS THE MODEL FROM ITS OWN HANDLERS - and until
2026-10-06 it did.** The runs went through `ExtEventHan`, but the READS
did not: every link tick re-read every ticked link's rooms (and, with
apartments on, every room of EVERY loaded link plus a `GetRoomAtPoint`
each) straight from the tick, Refresh re-read the links and their level
spans, and the Fixtures dialog read families from its callback - all
outside any API context. Autodesk does not support that; read-only
calls mostly work, which is how it went unnoticed, but nothing promises
they will, and every tick on a big job paid for a full re-read.

Now every read goes through **`VENTREAD`** (`Ask` in, `Answer` out):

- **the first one in the window's CONSTRUCTOR**, which runs inside the
  command's API context - the links, their spans and this model's
  levels - so the window opens with its links list and no round trip;
- **the rest through the external event** (`ExtEventHan.Read`), and the
  answer comes back to `VENTWindow.Take` on Revit's thread, which is the
  window's thread too;
- **the window holds what was read** - each link's rooms (`_roomsOf`,
  all of them, before any level or apartment is applied), the
  apartments map and the Apartments ticks it was built for (`_mapFor`),
  each model's fixture families - and **`RefreshLevels` asks only for
  what is missing** (`Missing`): a link ticked for the first time, or
  the map when the Apartments ticks changed. The interior placements a
  new map finds are read in the same trip. Everything else is
  `RVTLINKSELECTION.RoomsOn` over what is held, which is Revit-free and
  tested (`tests/ventleveltest`);
- **Refresh lets go of all of it** and reads afresh - the one way to see
  a link reloaded, a room drawn or a level added since the window
  opened;
- **`VENTREAD.Do` answers EVERY part it was asked for**, with nothing if
  it must (a closed model, an unreadable link). The window asks again
  for whatever is still missing, so an unanswered part would be asked
  for forever;
- **reads come from the window's OWN document** (`VENTWindow.Document`),
  not `Command.Doc` - the AUTOVENT button resets that even when it only
  brings an open window forward over another model.

**`ExtEventHan` NEVER CLEARED `Request`**, so any later `Raise` - which
reads now do all the time - would have run the last airflow write or
export AGAIN. `Execute` now takes the request and clears it first, and
reads are kept apart from it (`_reads`, merged when several are raised
before Revit gets to them), so a read and a run waiting together both
happen: the read first, then the run.

**Never run in Revit.** Built for all six; `tests/ventleveltest` covers
`RoomsOn` (11 checks) and `tests/ventsettingstest` `FamiliesFrom`.

**A modeless WPF window MUST be given Revit as its owner**
(`WindowInteropHelper` + `UIApplication.MainWindowHandle`). Without it
the window drops behind the main window the moment somebody clicks the
model, with nothing in the taskbar to bring it back - which reads as
the add-in having crashed.

**`Color` is ambiguous between `Autodesk.Revit.DB` and
`System.Windows.Media`** in a WPF code-behind - the same clash as
`Application` in `NWCForm`, and the same answer: say which.

**AN UNDERSCORE IN `CheckBox.Content` IS AN ACCESS KEY.**
`ContentPresenter` swallows it, so `GR_GROUND FLOOR` draws as
`GRGROUND FLOOR` with a hidden accelerator - and Revit level names are
full of underscores. Caught by rendering the window, not in Revit. The
item template puts a **`TextBlock`** inside the CheckBox instead, which
does no such thing.

**`tests/ventpreview` renders the real XAML to a PNG** with a
Revit-free stub code-behind, the WPF counterpart of
`tests/nwcpreview`. It caught the underscores, a starved `*` row
that left the levels card a sliver, and a clipped action card. Look at
a window before shipping it.

**THE WINDOW CLIPPED TEXT AT 150% SCALING - fixed.** The tutorial's
1.5x render is exactly what a laptop at 150% Windows scaling shows, and
two texts ran off their cards there. The default-size 1x preview did
not show either: WPF lays text out differently at 144 DPI, so **render
at 1.5x as well as 1x** before calling a WPF layout done.

1. **The levels summary lost its last lines** - including *"In the
   sheet, not in this model: ..."*, the one part that names levels which
   will silently process nothing. It had a `ScrollViewer` round it
   already, and that did nothing: **a `ScrollViewer` inside a
   `StackPanel` is handed unlimited height**, so it grew to its whole
   text and the CARD clipped it, with no scroll bar to say there was
   more. Now:
   - the warning is its OWN `TextBlock` (`TxtLevelsWarning`), FIRST, in
     the `Warn` amber (5.4:1 on white), collapsed when there is nothing
     to warn about;
   - the counts are two lines where they were three and a gap;
   - the panel is a `DockPanel`, so the scroll viewer fills what is left
     of the card and genuinely scrolls - ten missing levels show the
     heading and the first names, with a scroll bar for the rest.
2. **The endcap hint lost its second sentence** - and by the time this
   was looked at, the XAML had been cut to the first sentence alone
   (with a trailing space), so *"tick none and the system names
   decide"* was gone from the window altogether. It is back, as two
   short sentences on their own lines, each short enough for one line
   at 150%: the card is as tall as the two lists beside it, and a third
   line pushes the whole window past its height.

**The window was also taller than itself at both scales**: the bottom
card's lower edge sat under the status bar, because the top row's two
cards (links list 150 px, schedule text up to 150 px) left the levels
card at its `MinHeight`. Both are 116 now; the 34 px went to the levels
card, which is what gives the warning its room.

**THE PREVIEW RENDERS THE REAL WORDING NOW.** The stub carried its own
copy of the levels text and it had drifted two versions behind the
window, so a render of it said nothing about whether the real text fit.
`VENTLEVELNOTE` holds the wording, Revit-free, and `ventpreview`
compiles it in. The normal run also renders `autovent-levels-many.png`
and the tutorial run `tut_levels_many.png` - a sheet naming ten levels
the model has not got, the case the scroll viewer exists for.

**`Visibility` IS AMBIGUOUS IN THE CODE-BEHIND, like `Color`.**
`using Autodesk.Revit.DB` brings a `Visibility` of its own, so C#'s
"property named like its type" rule gives up and `Visibility.Collapsed`
reads as the WINDOW's `Visibility` property - error CS0176. The stub
has no Revit `using` and compiled it happily, so **the preview building
is not the add-in building**: `System.Windows.Visibility` in full.

**Still worth knowing:** the window is 860 tall with a `MinHeight` of
800, and a 1920x1080 screen at 150% is about 720 high once the taskbar
is off it - so on such a laptop the window cannot fit on screen at all,
whatever its text does. Not changed; nobody has asked.

**THE TUTORIAL VIDEO** - `docs/AUTOVENT-tutorial.mp4`, 2 min 21 s,
1920x1080, 25 fps, no voice, original music. Everything that makes it
lives in the scratchpad:

- `tests/ventpreview` with the argument `tutorial` renders the real
  window at 1.5x in three states AND writes a JSON of where every named
  control sits, taken from WPF's own layout - so every highlight box in
  the video lands on its control rather than on measured coordinates;
- `tests/video/make_video.py` is the storyboard and the frames
  (`--stills` renders one PNG per scene to look at first - it caught
  black bands, a camera with nothing to zoom into, overlapping labels
  and a clipped diagram before a single frame was encoded);
- `tests/video/music.py` COMPOSES the soundtrack from sine waves
  and filtered noise - no samples, nothing downloaded, so it belongs to
  nobody else and needs no licence or attribution;
- `tests/video/mp4enc` encodes it with **Media Foundation**, which
  ships with Windows - no ffmpeg. Every GUID and every COM vtable was
  read out of the Windows SDK headers, not remembered, because C#
  interop has no interface inheritance and one slot out sends every
  later call to the wrong method. `mp4enc check <file> <prefix>
  <seconds...>` reads an MP4 back with Media Foundation's own reader,
  decodes EVERY frame, confirms the audio, and saves real decoded
  frames - **"the encoder exited 0" is not "the video plays"**.

Two traps worth keeping. **RGB32 input needs a positive
`MF_MT_DEFAULT_STRIDE`** or the whole video comes out upside down - the
smoke test draws TOP in a corner to catch exactly that. And **a WPF
render has a TRANSPARENT margin**: `convert('RGB')` turns it into a
black band; crop to the alpha bounding box first.

Regenerate after any change to the window: re-run the tutorial render,
then `python make_video.py`.

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

**AN UNHOSTED HEAD TAKES THE LEVEL OF THE FLOOR UNDER IT**
(`SPRINKLERPLAN.LevelUnder`: the highest level at or below the point,
the lowest one if it is below them all). `NewFamilyInstance(point,
symbol, level, ...)` keeps the point exactly - *"the physical location
where the instance is to be placed"* - so the level is only the head's
reference level and offset, but schedules, views and tags by level all
go on it. It was `NearestLevel`, wrong twice over:

- **it compared `Level.Elevation` with a point**, which is the AUTOVENT
  "rooms a level up" bug in another place (`RVTLINKSELECTION.InternalZ`
  now, for the same reason);
- **"nearest" is usually the level ABOVE** - a ceiling at 2.7 m in a 4 m
  storey is 1.3 m from the slab over it - so nearly every head was tied
  to the floor above with a negative offset. Revit gives a head placed
  in a plan view that plan's level, the floor below.

**The two had to be fixed together**: on a model whose levels are typed
to the survey point, the first fault happened to undo the second, so
correcting the coordinates alone would have moved those models' heads
onto the level above. Heads hosted on a ceiling face are untouched -
Revit decides their level. Never run in Revit.

**A HEAD THAT LANDS IN SOMETHING GOES JUST OFF IT, ON ITS LINE.**
User-reported twice on 2026-10-07 from a real model - one head of a room
off its line, first against the façade, then 345 mm aside - and three
faults were behind it:

1. **A grid point in a HOLE was skipped without a word.** A room-bounding
   column (Revit cuts it out of the room's boundary) or a wall standing
   in the room sent the point to `CellCentroid`, and for a hole near the
   middle of its cell the centroid of the rest of the cell lands straight
   back IN it - so the cell was dropped, uncounted, and `FillGaps` put a
   head at the WORST uncovered point instead: against a wall. Now
   `NearestClear` takes the nearest point of the cell that clears every
   wall and hole edge; a point SQUEEZED between two holes, which
   `PushInside` only bounces between, gets the same; and `CellCentroid`
   takes the nearest in-room sample when its centroid lands in a hole,
   rather than giving up.
2. **`NearestClear` prefers the head's own ROW OR COLUMN LINE**, so it
   stays in line with its neighbours - unless that is more than
   `AlignAllowanceM` (0.3 m) further than the nearest clear point in any
   direction, since every extra metre along the line is a metre more
   spacing to the next head. A thin wall at 45 degrees did NOT test this
   (the nearest sample happened to lie on a line); a column turned 45
   degrees does, and was checked to fail with the preference off.
3. **WALLS WERE OBSTACLES ON EVERY STOREY.** `CollectWalls` read every
   wall in the link and `WallsNear` kept any whose PLAN outline met the
   room - so a partition on the floor above or below, invisible in the
   plan, pushed heads aside. That was the 345 mm. Each wall now carries
   its height range (every corner of its box through the link's
   transform) and counts only if `SPRINKLERLAYOUT.StandsInRoom` - it
   rises more than 300 mm above the room's floor and starts more than
   300 mm below its ceiling. The 300 mm absorbs walls drawn into the
   slab from below and down to the ceiling from above. A wall whose box
   cannot be read, or a room with no height, keeps the old behaviour.
   **A real bulkhead hanging less than 300 mm below the ceiling is
   ignored** - the price of the tolerance.

All in `tests/geomtest` (34 checks). **Never run in Revit.**

**THE WINDOW READS THE MODEL ONLY IN AN API CONTEXT** - the AUTOVENT fix
(`VENTREAD`) applied here on 2026-10-06, at the user's request. It used
to read every room of the ticked links from the LINK TICK
(`GetLevelNamesWithRooms`), and the family list and the Place
confirmation asked the `FamilySymbol` from their own handlers. Now:

- the links, the sprinkler families and what the window says about each
  family (`SymbolFacts` - label, placement, needs a host) are read in
  the CONSTRUCTOR, inside the command. `Load` would have worked too -
  WinForms raises it inside `Show()` - but that is timing, not a
  promise;
- each link's floors are read ONCE through the event
  (`SPRINKLEREVENT.ReadLevels`, answered in `ReportLevels`) and kept per
  link (`_levelsOf`), so a tick is a merge of what is held - de-duplicated
  and sorted by name, which is exactly what one read of all of them gave;
- **those reads have their own slot, apart from `Action`**: a tick can
  come while a listing or a placement is waiting, and the one slot
  would throw one of them away - a lost listing leaves "List rooms"
  greyed for good. `DoReadLevels` answers every link it was asked for,
  with nothing if it must, or the window would ask again on every tick;
- reads come from the window's own document (`SPRINKLERForm.Document`),
  not the one in front.

The listing, zoom and placement went through the event already. Never
run in Revit; built for all six.

**ALIGN TERMINALS** — `ALIGNCommand.cs`, `ALIGNEVENT.cs` (external
event), `ALIGNForm*.cs`, `ALIGNHELPForm*.cs`, `ALIGNTERMINALS.cs`
(matching and moving). Matching
is **plan distance only** — the MEP elevation is the thing being fixed,
so including it would let a badly levelled terminal match the wrong
ceiling. Each linked terminal is claimed once, greedily nearest first.

**THE MATCHING IS `ALIGNMATCH.cs`, REVIT-FREE**, moved out of
`ALIGNTERMINALS.Plan` on 2026-10-06 so the test compiles the real thing
(`tests/aligntest`, 15 checks). The harness before it MIRRORED the
matching and was lost with the scratchpad. `Plan` now only reads each
terminal's point, family and type off the model and builds the results.
**Ties are now settled by terminal id, then target** - the plain sort
it replaced left two equal distances in whatever order it liked, so two
runs on one model could disagree. The cell key packs two signed indices
into one long, and **negative coordinates are where that bites**: the
test unpacks all 16 million keys of a 4000 x 4000 block round the
origin, and was checked to FAIL against the classic `(cx << 32) | cy`.
A broken key only merges buckets - the grid-against-brute-force check
still passes - which is why the key has a check of its own.

**SHEET QA** — `QACommand.cs`, `QAEVENT.cs` (external event),
`QAForm*.cs`, `QAHELPForm*.cs`, `QAEXPORT.cs` (spreadsheet, Revit-free),
`QACHECK.cs` (the framework), `QAFLOWCHECK.cs` (the flowrate check),
`QAGRIDS.cs` + `QAGRIDLINKCHECK.cs` / `QAGRIDEXTENTCHECK.cs` /
`QAGRIDBUBBLECHECK.cs` (the grid checks), `QAGRIDFIX.cs` (putting them
right), `QADUCTS.cs` + `QADUCTCHECKS.cs` + `QADUCTFIX.cs` (the duct
disconnection page), `FCUSHEET.cs` (the Excel schedule, deliberately
Revit-free) + `QAFCU.cs` + `QAFCUCHECKS.cs` + `QAFCUFIX.cs` (the FCU
flowrate page), `CHWSIZESHEET.cs` (the ASHRAE sizing table, Revit-free)
+ `QACHW.cs` + `QACHWCHECKS.cs` (the CHW pipe size page), `QAMARK.cs`
(one-off tag overrides), `QAAVF.cs` + `QAWATCH.cs` (live analysis
markers on the tags).

**`QAForm.Selected` is what every button acts on**: the rows picked in
the table, or the whole list when nothing is picked. It required MORE
THAN ONE row until a user asked for exactly this feature and it turned
out picking a single finding silently did the entire model - the
opposite of what picking one row means. One row is a selection. The CHW
confirm dialog now says "the N pipe(s) YOU PICKED" against "all N
pipe(s)", because the difference is one pipe or a whole job.

**The window is six whole pages**, tabs at the top, each with its own
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

**"The view open now" has to reach the COLLECTOR** (`QACHECKS.Look`).
Whether a pipe shows in a view is decided by the crop region, the view
range, the discipline, hidden elements and every view filter on it - no
per-element test can answer that, but `new FilteredElementCollector(doc,
viewId)` is Revit's own answer. `QACHW.InScope` and `QADUCTS.InScope`
only ever handled `PickedLevels` and returned true for everything else,
so **"the view open now" silently meant the whole project on both the
CHW and duct pages** - user-reported. `QASCOPE.ViewId` is filled in by
`BuildContext` (the window has no API context to ask), and is left at
zero for a **ViewSheet**: no pipe is "in" a sheet, so collecting from
one finds viewports and a title block and would read as a model with no
pipework at all. A sheet keeps the whole-model behaviour.

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
Standalone harness at `tests/ducttest`; keep it in step.

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
it is testable — harness at `tests/sheettest`, run against the
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

**The CHW page** compares chilled water pipework against a sizing
table. A pipe is judged as one thing; a fitting or accessory is judged
at each **connector**, since a tee has three sizes and three flows. Only
round connectors, because the table is about DN.

**A REDUCER IS NEVER LISTED** (`QACHW.IsReducer`, skipped in `Fitting`).
It exists *because* the size changes across it, so its small end always
carries the flow of its big end and always reads as undersized - a false
positive on every transition in the model, by construction. Removed at
the user's request. It is still put right: `QACHWFITTINGS` reaches it
through `Touching` from the pipes it joins. `PartType.Transition` is the
test, with "two ends of different radius" as the fallback for a family
that will not say what it is.

**The table is BUILT IN** — `CHWSIZESHEET.Builtin()`, the office's own
ASHRAE 90.1 / ASTM A53 sheet, seventeen sizes DN20–DN600, at the
workbook's full precision. Nobody loads a file and no machine can end up
checking against a different copy. The numbers were **generated** from
the workbook, not typed.

**IT CAN NOW BE EXPORTED, EDITED AND IMPORTED BACK**, at the user's
request - three buttons on the CHW page, `CHWSIZESHEET.Write` /
`Read`. The built-in table is still what the window opens on and is
still the right answer for a normal job; this is for an office that
revises the standard, or a job working to a different one, which
previously meant a new build.

**THE ROUND TRIP IS THE WHOLE FEATURE, AND IT IS EASY TO BREAK
INVISIBLY.** `Detect` finds the columns by what the headings *say*, so
`Write` deliberately emits the headings it hunts for rather than
whatever reads best - `Pipe DN Designator` (carries `dn` as its own
word), `Maximum Flow` (carries `flow`, and neither `velocity` nor
`speed`), `Inner Diameter`, `Maximum Velocity`. Rename one and the
add-in writes a file it can no longer read, with nothing failing until
somebody tries. The units row is written as `L/s` for the same reason:
`Read` **refuses** a flow column in gpm or gallons rather than taking
them for litres.

**Numbers go out at FULL PRECISION and are only FORMATTED.** That is
the same `.Value` / `.Text` distinction as above, now load-bearing in
the other direction: DN20's limit is displayed to four places and still
reads back as `0.18977330777945564`. `tests/chwsizetest` asserts
every row comes back **bit for bit**, that an edited limit survives and
changes the answer, that writing over an existing file replaces rather
than appends, and that an empty table is refused rather than written.

**WHICH WORKSHEET IS FOUND, NOT ASKED FOR** (`QAForm.Sizing`). `Detect`
already knows what a sizing sheet looks like, so the first worksheet
that parses is used and the label says which it was - one fewer dialog,
and an exported file has only the one sheet anyway. The FIRST
complaint is kept rather than the last, or a workbook whose real sheet
comes first and whose cover sheet comes second would be described by
its cover sheet.

**An imported table is shown in GREEN and names its file**; the
built-in one stays grey. A check run against somebody's edited numbers
and one run against the office standard must never look the same on
screen. *"Use the built-in one"* greys out while it is the one in use,
and both switches say **run the checks again** - the findings on screen
were judged against the table that was loaded at the time.

`CHWSIZESHEET.Read()` also still serves `tests/chwsizetest`, which
reads the real workbook and asserts every built-in row still matches it
- a seventeen-row wall of `0.18977330777945564` cannot be reviewed by
eye, so something has to check it. Revise the standard by editing the
workbook, regenerating the constants, and letting that harness confirm
them.

**Read `.Value`, never `.Text`, for a number out of Excel.** `.Text`
gives the *formatted* string: a cell holding 0.1898 displayed to two
places reads back as "0.19", so the limits would depend on how somebody
chose to display their spreadsheet — and formatted to no decimals,
DN20's limit would read as 0 and everything would be flagged. Caught by
the harness at `tests/chwsizetest`. `FCUSHEET` had the same defect
and was fixed with it.

A flow column in gallons would be read as litres and pass everything, so
a sheet whose units row says gpm/gal is **refused** rather than guessed
at — that guard still protects the harness path. A size the table has
never heard of is reported once with a count, not once per pipe.

**Supply and return are picked separately**, two checked lists rather
than one, matching how CHW VOLUME already asks. Hydraulically the sides
carry the same flow so the sizing is identical — the split is so a
finding can say which side it is on (the System column groups by it) and
so one side can be checked without the other. A guess is pre-ticked only
where the system type's name says both *chilled water* and *which side*;
a name with no side in it is left for the user, because guessing there
is worse than asking.

**`QACHWFLOW` clears the "Additional Flow" parameter**
(`RBS_PIPE_ADDITIONAL_FLOW_PARAM`, writable — verified). That is flow
typed onto a pipe segment on top of what the system calculates: it does
not show on the drawing, survives every later change, and inflates what
the pipe reports carrying, so the size check reads a flow the system
never produces. It is **counted first and acted on second** — two
requests, `SurveyChwFlow` (reads only) then `ClearChwFlow` — so the
confirm dialog can name how many pipes and how many L/s rather than
asking blind.

**It clears EVERY PIPE IN THE PROJECT**, at the user's request. It used
to follow the page - the ticked CHW system types, inside the page's
scope - which reads sensibly and is wrong in practice: Additional Flow
is a nuisance wherever it is, and a pipe on an unticked system, or on no
system at all, still reports a flow the system never produced. Leaving
those behind meant running the button, fixing the ticks and running it
again. `Survey`/`Clear` therefore take **no scope at all** rather than
take one and ignore it, and `Result.Outside` counts the pipes beyond the
ticked chilled water systems so the confirm dialog can say how far it
reaches before anybody agrees.

**THE WHOLE SELECTION GOES TO `QACHWFIX`, NOT JUST THE PIPES.** The
button used to send `FixableIn` - findings carrying a `FixKind`, which
is pipes alone - so a fitting was only ever put right if it happened to
touch a pipe being resized. Once most of a model's pipes are correct
that is nothing, and the remaining wrong fittings are unreachable:
**user-reported as "fittings are not resized along with the pipes",
with a log line reading `0 fitting(s) listed`.** `Fix` sorts pipes from
fittings itself by asking the model what each element is; the window has
no business guessing, and it cannot - it has no API context.

**`QACHWFIX` writes the PIPES, then drives the FITTINGS to match
them.** Only pipe findings carry a `FixKind`, because a fitting has no
size of its own to be told - it is the size of the pipes on its ends,
and `QACHWFITTINGS` reads that from the model at the time rather than
off the finding.

**"Set the pipes and the fittings follow" IS FALSE THROUGH THE API.**
That is what happens when a person changes a size on the drawing, and
the first version of this page relied on it - **user-reported from a
real model: the pipes resized and the fittings stayed as they were.**
There is no call anywhere in the API that means "make the fittings
follow" (searched: `PlumbingUtils` has only `BreakCurve`, the placeholder
joins, `HasOpenConnector` and `PlaceCapOnOpenEnds`), so `QACHWFITTINGS`
drives each one, gentlest route first:

1. **Set `Connector.Radius`** to the pipe on that connector's side.
   Autodesk's own words: *"In order to set this property, it must be
   mapped to a writable instance parameter in the family definition"* -
   which most stock fittings, driven by a lookup table, are not. So it
   is set, regenerated, and **read back**: a lookup-table family can
   take the write and go straight back to what the table says, and only
   reading again tells the difference.
2. **An EQUAL fitting takes the largest pipe on it** (`TryBiggest`).
   Per-connector sizing is right for a reducing tee and **impossible
   for a family with one nominal size driving every end** - `M_Tee -
   Generic` is one, and its properties show a single `Nominal Radius`.
   No per-connector write can satisfy all three ends, so route 1 always
   rolls back and the fitting keeps its old size: **user-reported as a
   40-40-40 tee sitting on a DN100 main.** So every connector is set to
   the biggest want, which is what the fitting physically is; the
   reduction then belongs to the branch pipe. Tried only when the ends
   actually differ - when they agree, that IS route 1.
3. **Replace it.** Revit sizes a *new* fitting from the pipes it is
   given and the type's routing preferences, so the old one is deleted
   and `NewElbowFitting` / `NewTeeFitting` / `NewCrossFitting` /
   `NewTransitionFitting` / `NewUnionFitting` makes a fresh one - the
   same move `QADUCTFIX` makes on a hanging tap. `MechanicalFitting.PartType`
   says which to call (it works for pipe fittings too); the geometry is
   the fallback when a family will not say. **Straight is `|dot| >
   0.9994` = cos(2 degrees), and the 2 degrees is Revit's own**:
   `NewElbowFitting` takes a turn of "typically 2 to 95 degrees", so
   below that the answer has to be a union or a reducer.

**An accessory is never replaced** - a valve is something somebody
specified, and its size is usually a choice of type. It gets route 1 and
is reported if it will not take it.

**Equipment is never moved, and does not have to be.** A pipe into a
WSHP or a pump is anchored at that end, so the room is taken from the
other end. Note that **a pipe running into equipment can still be the
giver**: only its near end moves, its far end stays on the unit. That
is why `Survives` checks lengths rather than refusing on what a pipe is
attached to.

**A BIGGER FITTING NEEDS MORE ROOM, and that is what deletes pipes**
(`QACHWROOM`). A DN20 elbow turns the corner in a few millimetres and a
DN80 one needs far more, so a short jog between two elbows is eaten from
both ends at once; when nothing is left, Revit deletes it - *"Line is
too short"* - **user-reported, with two elbows left sitting against each
other.** Two answers:

1. **Know beforehand, and know it from the FITTINGS.** `QACHWROOM.Room`
   measures what each fitting takes up now (`LocationPoint.Point` to
   `Connector.Origin` - the centre-to-end) and scales it by the size
   ratio, since a fitting is a shape and a shape scales with its size.
   An unmeasurable one falls back on 1.5x the change in diameter (a
   long-radius elbow turns in about 1.5 diameters). The floor is
   `max(2 x ShortCurveTolerance, 2.5 mm)` - **it was 5 mm and a real
   model showed that was far too strict**: fourteen runs in one pass
   were refused because a 12 mm pipe would have ended at 4 mm, which
   Revit is perfectly happy with. Refusing that much work to save a
   millimetre is the wrong trade.

   **It weighs every pipe on those fittings, not the run's own pipes**,
   and the distinction cost a pipe in a real model. **A run STOPS at a
   tee**, so a short piece between two tees is a run of its own and may
   not be changing size at all - but resizing the main either side makes
   both tees grow and they eat it from both ends. The first version
   looked only at the pipes being resized and never saw it. `Eats`
   scales on `Biggest` (the largest size on that fitting now) because a
   fitting is as long as the biggest thing on it: a DN80 x DN15 tee is a
   DN80 tee with a small branch. Conservative on the branch side, which
   means more refusals rather than more deletions.

   `Verdict.Watched` carries every pipe weighed, and `QACHWFIX.Lost`
   re-checks all of them after the resize - two runs sharing a tee each
   predict only their own half, so the pair of them can kill a pipe
   neither one saw.
2. **Make the room by SLIDING THE FITTING AWAY** from the pipe that
   would be lost, along that pipe's own line. `QACHWROOM.Make` unhooks
   the joint (`Connector.DisconnectFrom`), `MoveElements` what has to
   move, rewrites the stretching pipes' `LocationCurve`s, and hooks it
   back up (`ConnectTo`). **Nothing is deleted** - an earlier version
   cut the boundary fittings out and made new ones, which threw away
   whatever was written on them and could fail at the recreate; this
   keeps the fitting, its id and its parameters.

   The length has to come from somewhere, and there are exactly two
   honest answers, tried in this order **on both ends before the second
   is tried on either**:

   - **`Leg`** - the first pipe IN LINE with the short one gives it up
     by changing length, and nothing past that point moves. Walks up to
     `Reach` = 8 elements (a plain jog finds it in three; a valve or an
     extra bend costs a couple more).
   - **`Branch`** - where nothing in line turns up, a DEAD-END branch is
     pushed along bodily, up to `Bundle` = 12 elements. A breadth-first
     walk that refuses on equipment, on more than `Bundle`, and on
     reaching the short pipe from anywhere but the fitting on its end
     (that is a ring, not a dead end).
   - **Crossing a tee.** The walk used to stop at the first tee, which
     on a branch to equipment is usually the only thing between the
     short pipe and a pipe in line that could give up the length.
     `Bring` now crosses one when everything hanging off it is a dead
     end that slides along too, and `Onward` follows the THROUGH path
     (the connector most nearly opposite the one it came in on) rather
     than whichever neighbour came first - otherwise the walk wanders
     down a branch believing it is going straight on.
   - **`Teed`** - the near fitting may be a TEE, which is the case that
     matters most on a main. Slide the tee along the main: the short
     pipe grows, the pipe in line gives up the same length, and
     everything hanging off the branch comes along (`DeadEnd`, bounded
     by `Bundle`, refusing on equipment). A drop to nothing is fine; a
     drop to an FCU is not, since the unit stays where it was put.

   A 2-ended accessory **is** slid along with its own pipes - a valve
   moving a few millimetres with the pipework either side of it changes
   nothing about the valve. A tee is refused in `Leg` (its branch would
   be left behind) but allowed inside `Branch`, where the whole
   component moves together.

**The room is made where the damage happens.** `QACHWROOM.Ensure` is
called from `QACHWFITTINGS.One` immediately before a fitting is grown,
because **growing the fitting is what eats the pipe** - the pass over
the runs predicts it beforehand, but by then other runs have been
written and the fitting may be about to grow more than anything
predicted. Room first, then size. `QACHWFITTINGS.Squashed` is the
backstop: if a fitting's new size still leaves a pipe on it too short
(or has deleted it), that fitting's `SubTransaction` rolls back and it
keeps its old size - a wrong-sized fitting is visible and fixable, a
deleted pipe is neither.

**`QACHWLOG` writes `Desktop\ATKINSMEC_chw_log.txt` on every fix.** It
exists because this page has now been wrong three times in a real model
while every harness passed, and a log turns a round trip of guessing
into one read. It records each run's target size, every pipe weighed
with before/after, every refusal and its reason, what each short pipe's
ends are joined to (the commonest reason a slide is impossible, and not
something a harness can know), the fittings pass, and a final sweep.
**Read it before theorising.**

**A CAP IS A DEAD END, NOT AN OBSTACLE.** `DeadEnd` demanded a fitting
with exactly two piping ends, or more than two - so a cap, which has
ONE, fell through both tests and was turned away as though it were a
chiller. A real model showed it blocking a branch that was otherwise
free to move. The rule is now simply: pipes, fittings and accessories
move; **only equipment stays put**.

**A slide may not BEND anything, and that needs checking on purpose.**
A slide moves things along a line and never rotates them - but a joint
can come back together crooked and still be a joint, which is how a
resize passed every check and still drew a visibly stretched elbow in a
real model. `Sound` now also requires that **no pipe has turned** (its
direction unchanged to 1e-5) and that **every remade joint is square**
(ends within 0.5 mm and facing each other to -0.999). Either fails and
the slide goes back.

**`Sound` asks the model rather than trusting the arithmetic**: every
joint the involved elements had before (`QACHWRUNS.JointsOf`) must still
be joined, and nothing involved may be below the floor. Either fails and
the `SubTransaction` rolls back - so this cannot trade one *"Line is too
short"* for another, or leave anything disconnected.

**After the resize the model gets the last word too** - `Lost` checks
every pipe in the run still exists and is still long enough, and rolls
the run back if not, since the growth figure is an estimate and Revit's
answer is not.

All of it is behind **`QASETTINGS.ChwKeepShortPipes`** (the checkbox
"Slide fittings along to keep pipes Revit would delete", on by default).
Off, a run with a doomed pipe is simply refused - still nothing deleted.

Harness at `tests/roomtest` - 29 checks, and it has earned its keep
three times: the far boundary fitting deleted and then moved; the
dead-end walk calling its own seam a ring, which made that whole route
dead code; and, once the models were given a proper termination, a walk
coming back round to the short pipe and taking it for a pipe in line
with itself. **Model the far side as ending at equipment** - a stub
hanging in mid-air lets the branch route "succeed" through something
that would not exist in a real job.

**IT IS A MIRROR, AND IT DRIFTED.** `QACHWROOM` walks Revit's own
connectors, so it cannot be compiled into a test; the harness keeps a
hand copy on a toy model. The copy recovered from the transcripts on
2026-10-06 predated six fixes - the 2.5 mm floor, a reach of 8, the
gentle route on both ends before the branch on either, `Leg` refusing a
walk back round to the short pipe, caps as dead ends, and `Branch`
letting go of the short pipe's own joint - and its models had lost
their equipment ends. All put back, plus a check for the cap.
**Change `QACHWROOM`'s walk and the copy in the same commit.** Making
the walk testable for real means running it over an abstract network,
which is a rewrite of the most field-debugged code in the suite - not
something to do without a real model to check it on.

Order is pipes -> fittings -> joints -> fittings once more (only if a
joint was actually mended, since mending one can hand a fitting back the
pipe it takes its size from). A replaced fitting's id goes in `Gone` so
`Mend` does not report its old joints as broken. Decision harness at
`tests/fittingtest` - 20 checks on which creation call each shape
needs, including a tee's branch found wherever it sits in the list.
**It compiles the REAL rules**: `QACHWSHAPE.cs` (Revit-free - `Join`,
`Odd`, `Partner`, `Straight`, `SameSize`), moved out of `QACHWFITTINGS`
unchanged on 2026-10-06. Its hand copy had fallen behind - still
"straight" at 0.999 after the add-in moved to Revit's 2 degrees - and
one check had been written against the older line; it now pins 1.9
degrees as straight and 2.1 as a turn.

`MEPCurve.Diameter` is **read-only**; the writable route is
`RBS_PIPE_DIAMETER_PARAM` via `Parameter.Set`. (`FamilyInstance.Symbol` is settable too, but swapping a
pipe's type is not what "make it DN80" means. `Connector.Radius` is
settable and IS used - on the fittings, not on the pipes; see below.)

**Three guards against leaving the model in pieces** - `QACHWRUNS`
supplies all three, and they matter in this order:

1. **Whole runs move together.** Resizing one pipe out of a continuous
   length puts a reducer mid-straight for no reason and asks a joint to
   survive a change its neighbour did not make. `QACHWRUNS.Group` walks
   the network: it crosses a direct pipe-to-pipe joint, and a **fitting
   or accessory with exactly two piping ends of equal size** - which is
   what an elbow or union *is*, without interrogating the family. It
   stops at a tee or cross (three ends: the flow divides), at a
   transition (two ends, different sizes: it exists on purpose), and at
   **equipment** - a pump has two equal connectors and would otherwise
   be crossed as though it were an elbow, so the category is checked as
   well as the geometry. An in-line accessory *is* crossed but recorded
   as a boundary, since its own size may not follow.
2. **The pipe type is asked first.** `QACHWRUNS.SizesFor` reads
   `PipeType.RoutingPreferenceManager` -> `Segment.GetSizes()` ->
   `MEPSize.NominalDiameter`. A size not on that list has no segment and
   no fitting, and is the commonest cause of failure - so the run is
   refused with the size and the type named. An unreadable type returns
   an empty list, which `Offers` takes as consent: refusing on silence
   would block every resize on a model this cannot read.
3. **The joints are written down.** `QACHWRUNS.JointsOf` records every
   piping joint before anything moves; afterwards `Connector.IsConnectedTo`
   says which came apart, and `QACHWFIX.Mend` rejoins them -
   `ConnectTo` where the ends now match, `NewTransitionFitting` where
   they differ. Revit often inserts its own reducer, which shows up as
   "already connected" and is left alone. A joint whose ends also
   *moved* (more than 2 mm) is reported rather than forced.

Runs are done **biggest first**; each is its own `SubTransaction` with a
`doc.Regenerate()` inside it, so a refusal surfaces while it can still be
rolled back alone. Graph harness at `tests/runtest` - 14 checks,
including ring-main cycle termination and "no pipe in two runs"; keep it
in step with `QACHWRUNS`.

This page was written report-only and gained the button at the user's
request. The hesitation is still worth knowing: resizing a pipe changes
the hydraulics of everything downstream of it. The button puts the
drawing in step with the table; it does not redesign the system.

`QAEVENT.DoFixGrids` **re-runs the checks inline** after committing,
rather than raising another request — `Execute` clears `Action` on its
way out, so a request raised from inside a handler would arrive with
nothing to do. This is a general trap for the modeless four.

**THE CHW DISCONNECTION PAGE** is the duct page's shape applied to
chilled water pipework, plus the thing that prompted it: pipes drawn to
a fan coil and never joined to it. `QACHWJOINTS` walks once and caches
on `QACONTEXT.Shared`; three checks divide the loose ends so nothing is
reported twice - belongs on a unit's connector, meets another end, or
has nothing within reach (a warning, never fixed, because a stub is
usually deliberate). It collects `OST_PipeCurves`,
`OST_FlexPipeCurves`, `OST_PipeFitting` and `OST_PipeAccessory` -
**pipes only, never ductwork**.

**CLONING THE DUCT TAB STOLE THE DUCT TAB'S CONTROLS.** The tab was
copied from the duct tab's designer block, which is the right idea -
the pages are the same shape and a hand-written copy would drift. But
the renamer only knew the names it was told to rename, so
`grpChecksDucts.Controls.Add(this.numDuctReach)` came across as
`grpChecksChwJ.Controls.Add(this.numDuctReach)`. **A WinForms control
has exactly one parent**, so the DUCT page silently lost its reach
spinner, its join tolerance and its fallback tick to the CHW page -
user-reported, and the visible half ("it says duct") was the lesser
one. The radios were wired to `ScopeDucts_CheckedChanged` too, so
picking a scope on CHW reconfigured the duct page.

**When cloning a designer block, the `Controls.Add` lines and the
event wiring are the two things that must be checked by eye** - a
rename list covers the declarations and the property blocks, and
silently misses both of these.

The CHW page has its own `numChwJReach` (300 mm), its own
`ScopeChwJ_CheckedChanged`, and a note saying in as many words that it
never looks at ductwork. **A NEW PAGE HAS TO BE ADDED IN FOUR PLACES**, and they are in four
different methods, so adding it to three of them leaves a page that
looks finished and is not:

1. `Fill(page)` - the CHECKS list and the findings columns.
2. **`Offer(page, ...)`** - the SCOPE list, the levels or sheets down
   the right. This is a separate method reached from a separate place,
   and missing it is the one that looks most like nothing is wrong:
   the checks appear, the tab draws, and the levels box is simply
   empty. **User-reported twice** - once for the checks and once, after
   `Fill` was added, for the levels.
3. The Fix button's enable line.
4. Its own `Scope*_CheckedChanged` handler.

Worth remembering that `Page` exists precisely so a tab's behaviour is
written once - but the WIRING of a new page is still four hand edits,
and nothing fails the build when one is missed.

**WHICH EQUIPMENT FAMILIES COUNT IS PICKED IN THE WINDOW**
(`clbChwJFamilies`, `QASETTINGS.ChwEquipFamilyIds`), at the user's
request. A model with pumps, chillers and air handling units in it
would otherwise list ends that were never meant to be on a fan coil.

`QAEQUIP.Families` reads the families **from the placed instances**
rather than from the loaded-family list - a project can carry a hundred
loaded families and have six in the model - and the instance count
comes free and is shown beside each name.

**EMPTY MEANS EVERY FAMILY.** That inversion is surprising, so it is
written on the label permanently rather than only being true when the
list is built: *"Tick none to search every family."* Families whose
name says FCU or fan coil are pre-ticked, which is a guess - but a
wrong guess here only widens or narrows the SEARCH. It can never join
a pipe to the wrong thing, because the system match still decides
every joint.

**THIS PAGE PICKS ITS OWN SYSTEM TYPES** (`clbChwJSystems`,
`QASETTINGS.ChwJointSystemTypeIds`, read in `QACHWJOINTS.Ours`), at the
user's request. It used to borrow the CHW SIZING page's ticked types,
which was wrong twice over:

- **A circuit is a flow AND a return.** That page pre-ticks by NAME, so
  a model whose return system type is not called "return" left the
  return list empty - and this page then saw supply pipes only and
  silently did half the job. **User-reported.**
- **It was a coupling that should never have existed.** The tabs are
  separate pages with separate scopes and separate checks; one quietly
  governing another is the thing this file says must not happen.

**IT PRE-TICKS ON REVIT'S OWN CLASSIFICATION, NEVER ON A NAME** -
`MEPSystemClassification.SupplyHydronic` / `ReturnHydronic`, which is
the whole point: it is the naming guess above that failed. Note that
**`SystemClassification` IS NOT DECLARED ON `PipingSystemType`** - it
is inherited from the base `MEPSystemType`, so `QACHW.IsHydronic` casts
to that. Compile-verified on 2022 and 2027.

**NOTHING TICKED MEANS EVERY HYDRONIC TYPE**, which is exactly what the
page did before the list existed - so a model nobody has set up behaves
as it always did, and ticking only ever narrows. Said out loud in the
label rather than only being true when the list is built, the same
reasoning as the families beside it.

**AN ELEMENT ON NO SYSTEM FALLS BACK TO THE CLASSIFICATION.** A fitting
whose pipes were never joined up carries no system type at all, and on
THIS page that is the normal case rather than a rare one - it is
precisely what the page exists to find. Matching it against the ticked
ids would hide the disconnections nearest the break, so it is judged by
`SideOfElement` instead. Wider than asked for, which is the right way
to be wrong here.

Narrowing WHAT A PIPE MAY BE JOINED TO is still separate: the ticked
EQUIPMENT FAMILIES decide that.

**NO `PipeSystemType` NUMBER IS WRITTEN INTO `QACHWALIGN`.** That file
is Revit-free, and naming Revit's enum values there would be an
assumption with no way to check it - a reference assembly cannot be
executed to read them back, so the numbers could only ever be guessed.
Everything in it compares ints for EQUALITY, which needs no constants;
`QACHWJOINTS.IsHydronic` asks the enum itself, being the only place
that can see it.

**THE UNIT IS NEVER MOVED - THE PIPE IS.** A fan coil sits on a ceiling
grid in a coordinated position; the pipework is what was drawn to it
and missed.

**EACH PIPE GOES TO ITS NEAREST UNIT, AND THE COLLECTOR'S ORDER MEANS
NOTHING.** `OnEquipment` used to loop over UNITS, each one claiming any
unclaimed end within reach - so a pipe 250 mm from one fan coil and
40 mm from another went to whichever `FilteredElementCollector`
happened to return first. **User-reported as "it should connect the
disconnected pipes to their nearest FCU".**

It now builds every (pipe end, unit) pairing within reach FIRST, sorts
the lot by distance and settles the closest in the whole model before
the next - `ALIGNTERMINALS`' greedy matching, applied across units
rather than inside one. A pipe whose nearest unit has no connector left
falls through to the next-nearest on the same pass, because the list
still holds that pairing. Ties break on element id so two runs of one
model cannot disagree.

**WHICH UNIT and WHICH CONNECTOR ON IT are different questions.**
Distance settles the unit; `QACHWALIGN.Pick` still settles the
connector by system, then description, then distance. The system
remains absolute in both: a unit with no free connector of the pipe's
side is not a candidate at all, so a supply pipe is never *held* by a
fan coil it could only join wrongly.

**CONNECTOR READS ARE CACHED BEFORE THE DOUBLE LOOP** (`Spot`).
`Origin`, `PipeSystemType` and `Description` are each an API call, and
the matching is ends x units x connectors - a thousand ends against
five hundred units would otherwise be millions of interop calls to
answer a question about points. Read once, then arithmetic.

**"NOTHING WITHIN REACH" ON ITS OWN IS NOT ACTIONABLE**, so the scan
remembers the nearest unit of the right system WHETHER OR NOT it was
reachable, and the stranded finding names which of SIX things
happened:

1. beyond the reach - with both figures, so the number to type into
   the spinner is on screen;
2. in reach, but that connector has already gone to a nearer pipe;
3. **no mechanical equipment in the scope at all** - which is far
   likelier than it sounds, because the FAMILIES LIST IS READ FROM THE
   WHOLE DOCUMENT (`QAEQUIP.Families(doc)`) while the SCAN is read
   through `QACHECKS.Look(doc, scope)`. On "the view open now" the
   window can therefore offer eighteen families while the scan sees no
   equipment whatsoever;
4. **the systems disagree** - see below;
5. the pipe is on no system at all;
6. equipment in scope, but every pipe connector on it already taken.

**THE SYSTEM MISMATCH IS THE ONE NOBODY CAN SEE ON THE DRAWING.** The
pipe is touching the unit, the drawing looks right, and it will never
join - because `Connector.PipeSystemType` on the two differs. So the
scan also records the nearest unit of ANY system (`NearestAny`), which
is never a candidate and exists only to say so, and the finding names
BOTH classifications through `QACHWJOINTS.SideName`:

> *nearest is IU-Type P at 45 mm - but that connector carries OtherPipe
> and this pipe carries ReturnHydronic, so they are never joined*

That is the difference between "the add-in is broken" and "the family's
connector is set to the wrong classification". **User-reported as 246
warnings on a VRV refrigerant model** - note that a REFRIGERANT system
type is often built on the Hydronic Supply/Return classification, which
is why this page finds such a model at all.

**THE SCAN WRITES ITSELF DOWN TOO** (`QACHWJOINTS.Report`, into the same
`Desktop\ATKINSMEC_chw_log.txt`). The FIX has had a log from the start
and it settled two rounds of theorising in one read each; the scan had
none, so a run that found 246 open ends and joined nothing left no
trace of what it had looked at. It now records the scope reach, the
pipework and open-end counts, how many equipment were in scope AND how
many had a free pipe connector, up to twenty unmatched ends with their
nearest unit - and, the point of the whole thing, **the systems carried
by the open PIPE ends beside the systems carried by the free EQUIPMENT
connectors**. Put those two tallies side by side and a mismatch is
visible instantly. Whichever of scan or fix ran last owns the file.

**SUPPLY GOES TO SUPPLY, AND THE DISTANCE ONLY BREAKS TIES**
(`QACHWALIGN.Pick`, `QACHWSIDE.Match`). `Connector.PipeSystemType`
gives `SupplyHydronic` / `ReturnHydronic` on both the pipe and the
unit's connector, and they must be the SAME - **never the nearest
connector regardless**. Joining flow to return looks perfectly correct
on the drawing and is wrong in every calculation afterwards, which is
the one mistake here nothing downstream would catch. An unknown system
on either side is refused rather than guessed at, and each connector is
claimed once, nearest first - `ALIGNTERMINALS`' greedy matching. **The
system is checked AGAIN at the moment of joining**, because a list can
be minutes old.

**A MANUFACTURER'S FAMILY OFTEN CLASSIFIES NOTHING AT ALL, AND THAT
USED TO REFUSE EVERY JOIN** (`QASETTINGS.ChwJoinUnknownSystem`, the
checkbox "Also join where the unit's connector has no system set (VRV
families)", **ON by default**, at the user's request).

The scan log settled it in one read. A real Daikin VRV model gave:

```
systems on the open PIPE ends     systems on the free EQUIPMENT connectors
   ReturnHydronic  -  125            Sanitary    -  117
   SupplyHydronic  -  127            no system   -  233
```

- the refrigerant connectors are left at **`UndefinedSystemType`** -
  which `Match` refused on both sides, so **a pipe end sitting 0 mm
  off its connector was turned away**, 246 times;
- the 117 `Sanitary` ones are the **condensate drains**, and those are
  a real hazard sitting right beside the refrigerant connectors.

**THE RELAXATION DOES NOT TOUCH THE RULE THAT MATTERS.** Two sides that
BOTH say what they are must still say the SAME thing, so supply never
reaches return - and the drain connector stays refused, because
`Sanitary` and `SupplyHydronic` are both known and different. What it
opens up is only the case where **Revit itself has no opinion**: a
connector at `UndefinedSystemType` is not a return connector being
mistaken for a supply one, it is a connector nobody classified, so
there is no information there to protect and refusing it protected
nothing.

Which of two unclassified connectors a pipe takes is then settled by
the description and then the distance - exactly as it already is for a
four-pipe unit whose connectors both say `SupplyHydronic`, below - and
`Choice.Ambiguous` still says so when only the distance chose. The
finding also says *"that connector has NO system set, so it was matched
on position"*, so such a join is never silent.

Off, the page goes back to the strict rule and does nothing at all on
these families. Tested at `tests/chwjointtest` (64 checks), where
most of the new ones assert what it must NOT do.

**A FREE CONNECTOR ONLY TELLS YOU ITS CLASSIFICATION, AND ON A
FOUR-PIPE UNIT THAT IS NOT ENOUGH.** Revit gives an unconnected
connector `SupplyHydronic` or `ReturnHydronic` and nothing more - it
has no system, because nothing is on it. A fan coil with a chilled
coil AND a heating coil therefore has **two connectors that both say
SupplyHydronic**, and a chilled water pipe could be sent to the
heating one on distance alone. Nothing downstream would catch it.

So `QACHWALIGN.Pick` decides in this order: **the system, then the
connector's DESCRIPTION, then the distance.** `Connector.Description`
is what the family author wrote ("CHW Supply", "HHW Return") and is
the only thing in the API that separates the two. `QACHWALIGN.Hint`
scores it against the pipe's system name on shared words of three
letters or more - so `CHW Supply` beats `HHW Supply`, which shares
only "supply".

**Where nothing separates them, the finding SAYS SO.** `Choice.Ambiguous`
is set when several connectors of the right system were on offer and
only the distance chose, and the finding then carries *"CHECK: that
unit has more than one free connector of this system"*. The answer is
probably right; saying it is a guess costs nothing and a silent
supply/heating swap costs a great deal.

**HOW FAR A PIPE CAN BE BROUGHT IS ARITHMETIC, AND IT IS REVIT-FREE**
(`QACHWALIGN.Make`, tested at `tests/chwjointtest`, 39 checks).
The gap from the pipe's end to the connector is split into what the
pipe itself can absorb by changing length, and what the pipe above it
can absorb by the fitting between them sliding along. **Anything left
over after those two is a direction nothing runs in, and the whole
thing is refused rather than bent.**

**A PIPE LOOSE AT ITS FAR END SIMPLY MOVES** (`Kind.Shift`).
**User-reported**: pipes drawn at the wrong ELEVATION beside the unit
they serve. That offset is vertical - a direction neither the pipe nor
anything above it runs in - so every other route refuses it, and no
amount of stretching along its own axis can reach. But if nothing is
joined to the pipe's far end, the whole pipe can be moved onto the
connector's line and then stretched onto it: no joint has to survive
the move and no other element is touched. It is both the simplest
answer and the safest, and it is what a person does by hand.

It is tried AFTER a stretch and BEFORE a slide - a connector already on
the pipe's line needs no move at all, and where the far end is held a
slide is the only thing left.

**A REFUSAL NAMES WHAT IS HOLDING THE PIPE.** The log records whether
the far end was free, what it is joined to, which way the pipe runs and
where the connector is relative to its end - all in millimetres. A pipe
that could not be moved is only actionable if you can see what is
holding it.

**THE OTHER CASES ARE CARRIED OUT TOO.** The stretch moves one end;
`QACHWJOINTFIX.Slide` does the offset case from the picture - a drop
that does not line up with the connector under it - in the same five
steps `QACHWROOM.Make` uses, and for the same reason:

1. Read the joint between the fitting above and the pipe beyond it.
2. **Unhook it**, so Revit is not asked to keep a joint through a move
   it cannot survive.
3. `MoveElements` the fitting AND the drop sideways together.
4. **Restretch the pipe beyond**, whose far end stays exactly where it
   was - keeping the line's direction (`EQUIPLINE.MovingIsStart`), or
   Revit throws the transaction out.
5. Hook it back up, then stretch the drop onto the connector.

It refuses rather than forces when there is nothing beyond the fitting
to give up the length, or when doing so would leave that pipe too
short to exist.

**THE FIRST REAL MODEL JOINED NOTHING AT ALL** - *"0 joined, 0 aligned
and joined, 25 refused"*, user-reported with the log - and neither
cause was in the matching or the arithmetic, which had worked
perfectly. Both are in **Conventions and traps** above because neither
is about chilled water:

1. **Dead connectors.** The moves SUCCEEDED and the joins failed. Four
   pipes logged *"stretched 662 mm onto the connector"* and then threw
   `InvalidObjectException` on the very next call; the ones that slid
   gave up without a word, because `Origin()` on a dead connector
   returns null. `Align` now hands back the live connector.
2. **Insulation walked into the network.** `PipeInsulation` is an
   `MEPCurve` and sits in `AllRefs`, so "the pipe above" was often a
   sleeve of fibreglass - which is why so many refusals read *"there is
   nothing above it to slide along"* while the log simultaneously
   reported the far end as *"joined to Pipe Fittings - Standard"*. The
   two statements cannot both be true, and that contradiction is what
   gave it away.

**Read the log before theorising** - it named both of these, in its own
words, on the first run.

The stretch keeps the line's direction (`EQUIPLINE.MovingIsStart`) for
the same reason the equipment changer does.

**AN UNSET FLOW IS A ZERO, NOT A REASON TO LOOK AWAY.** `QAFLOWCHECK`
read the flow with `parameter == null || !parameter.HasValue` and
skipped both alike. But `Parameter.HasValue` is *"identifies if the
parameter has an assigned value"*, and a pipe or duct belonging to NO
SYSTEM has none - which is the commonest way of all to end up with a
tag printing 0 on a sheet. So the elements most worth reporting were
the ones being thrown away, and **pipe tags in particular never
appeared while duct tags did** - user-reported as "also check the
zero flowrate tags of the pipes".

The category test already establishes that the element is supposed to
carry flow, so an unassigned value is now a finding. The two are still
told apart, because they are put right differently: *"flow is zero"*
means the system calculated nothing, *"flow is not set - is it on a
system?"* usually means the element is in no system at all, and the
Value column reads `not set`.

The check never needed pipes ADDING - `FlowOf` has always handled
`OST_PipeCurves`, `OST_FlexPipeCurves`, `OST_PipeFitting` and
`OST_PipeAccessory` through `RBS_PIPE_FLOW_PARAM`, and `QACHECKS.TagsIn`
collects every `IndependentTag` in a view with no category filter. It
was only ever the `HasValue` guard.

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

**AUTOSYNC** — `SYNCCommand.cs`, `SYNCSERVICE.cs` (the timer),
`SYNCEVENT.cs` (the only file here that touches Revit), `SYNCNET.cs`
(reachability, Revit-free), `SYNCSETTINGS.cs`, `SYNCLOG.cs`,
`SYNCForm*.cs` (status window), `SYNCASKForm*.cs` (the startup
question).

**A timer tick is no more an API context than a button handler is.**
This is the same trap as `app.DocumentChanged +=`, in a new place:
`System.Timers.Timer` fires on a threadpool thread, so `SYNCSERVICE`
does nothing but `ExternalEvent.Raise()`. Raising is safe from any
thread and Revit runs the handler **when it is idle**, which is also
what stops an auto sync interrupting somebody mid-command.

**The question is asked from `ApplicationInitialized`, not `OnStartup`.**
`OnStartup` is too early to put a dialog in front of somebody, and
`ExternalEvent.Create` wants a live application. `UI.RevitIsUp` handles
both and unhooks itself.

**Whether AutoSync is running is deliberately NOT persisted.** The
interval and the options are; the on/off is asked fresh every session,
because a thing that writes to the central model should not switch
itself on because it was on last Tuesday. `SYNCSETTINGS.LastAnswer`
only decides which button the question opens on.

**Five minutes is enforced in three places** — the spinner's `Minimum`,
`SYNCSETTINGS.Floor` on load, and `Floor` again on save — so a
hand-edited settings file cannot sneak a shorter one in.

**The reachability probe must never block.** It runs on Revit's own
thread, and an unreachable UNC path can take the best part of a minute
to admit it; that would be a frozen Revit every five minutes. Every
probe in `SYNCNET` runs on a throwaway background thread behind a
`WaitOne` timeout and gives up at ~4 s. Harness at `tests/synctest`
times this as well as asserting it.

**"Is the internet up" is the wrong question for half of these models.**
A file-based central needs the LAN, not the internet; a cloud model
needs the internet and does not care about the share. So `SYNCEVENT`
probes what the model actually syncs to — `IsModelInCloud` picks which —
and caches one answer per central location so five models off one server
cost one test. A model that is not workshared, or detached, has nothing
to sync to and is simply saved.

**NWC BATCH** - `NWCCommand.cs` (+ `NWCAvailability`), `NWCForm*.cs`,
`NWCHELPForm*.cs`, `NWCMODELForm*.cs` (one model's own settings),
`NWCWORKSETForm*.cs` (one model's worksets), `NWCEXPORT.cs` (the
engine), `NWCSETTINGS.cs` (the saved list and options, deliberately
Revit-free). Both per-model windows are Revit-free too, so they render
in the preview harness.

**Built on the CHW VOLUME pattern, and it reuses its machinery.** The
form is MODAL, so it runs inside the command's API context and can open
and close documents directly - a modeless window would need an external
event for every step. `CHWCLOUD` is called as it stands for the ACC
side: `ScanCache`, `Open`, the region probing and the workset modes are
substantial, hard-won code and duplicating them would be worse than the
coupling. `CHWCACHEForm` is reused whole as the ACC picker. Only the
saved list is its own (`NWCJOB`, in `nwc_export.txt`) - sharing
`CHWCLOUD.CloudModel`'s settings file would have one add-in writing the
other's choices away.

**THE EXPORTER IS NOT PART OF REVIT.** It ships with Navisworks (or the
free standalone NWC exporter) and is optional, so
`OptionalFunctionalityUtils.IsNavisworksExporterAvailable()` is asked
FIRST - the documented way to check. Asking beforehand turns "an
exception on every one of your twenty models" into one sentence naming
what to install.

**A model without the named 3D view is REPORTED, not guessed at.** The
view is matched by NAME across models, because that is the only thing
models have in common - each has its own views, and most offices keep
one called `Navisworks` for exactly this. Falling back to the whole
model would quietly swap one deliverable for another.

**The document already open in Revit is exported where it sits and is
NEVER closed.** Closing the model somebody is working in is not a thing
a batch job may do; `NWCEXPORT.One` tracks whether it opened a document
before deciding to close it.

The API was compile-verified on 2022 and 2027 before any of it was
written. One wrong guess: the property is **`Parameters`**, not
`ExportParameters`. The rest - `ExportScope` (Model/View/SelectedElements),
`ViewId`, `Coordinates` (Shared/Internal), `ExportLinks`,
`DivideFileIntoLevels`, `ExportElementIds`, `ExportParts`,
`ExportRoomAsAttribute`, `ExportRoomGeometry`, `ConvertElementProperties`,
`FindMissingMaterials`, `ConvertLights`, `ConvertLinkedCADFormats`,
`FacetingFactor`, `ExportUrls` - is as expected. `Document.Export` adds
the `.nwc` extension itself if the name has not got one.

**The export settings can be handed round** (`SaveTo` / `LoadFrom`,
`.nwcset`). One `Compose(bool withJobs)` writes both the personal saved
state and the shareable file, so the two formats cannot drift apart.

**The model list is deliberately NOT in the shared file**, and
`LoadFrom` ignores `job` lines even when a file has them: the list is
paths and cloud GUIDs belonging to one machine, and importing a
colleague's settings must never empty or repopulate somebody's own
list. Import reads the window first, so a setting the file does not
mention keeps what is on screen rather than reverting to a default
nobody chose.

**THREE THINGS CAN BE SET FOR ONE MODEL** rather than for the run, all
from its row in the list, all at the user's request:

1. **Its own 3D view** - typed into the row's editable `3D view` cell,
   empty meaning "follow the window". `NWCEXPORT.ViewFor` resolves it;
   the literal `(the whole model)` in a row beats a view name on the
   window, which is the only way to say "all of this one" in a run that
   otherwise uses a view.
2. **Its own export settings** (`Settings...` -> `NWCMODELForm`) - a
   WHOLE `NWCSETTINGS` or none, never a few fields. A per-field
   override would give every box on the main window two meanings and
   leave somebody working out which won. `NWCEXPORT.For` merges it and
   **takes the folder and the view back off it**: every file in a run
   goes to the one folder the window names, and the view comes from the
   row - an override carrying an empty view name would otherwise
   silently mean "the whole model".
3. **Its own worksets** (`Worksets...` -> `NWCWORKSETForm`) - see
   below.

The models list is a `DataGridView` rather than a tick list because of
these: it carries a tick, a name, an editable view, and what each model
is set up with. `ReadGrid` copies it back into the jobs before anything
acts on it, so a view name typed and not tabbed out of is not lost.
**`Unpick` clears the selection after every fill** - a `DataGridView`
selects its first row the moment it is populated and a `CheckedListBox`
did not, so "the picked row" would have silently meant row one and
Remove would have deleted a model nobody chose. That is the same trap
`QAForm.Selected` fell into.

**WORKSETS ARE PICKED BY NAME, AND READ WITHOUT OPENING THE MODEL.**
`WorksharingUtils.GetUserWorksetInfo(ModelPath)` exists for exactly
this - Autodesk's words: it gives "a preview of the user worksets
available in a file, allowing an application to look up the necessary
workset ids and information to properly fill out a WorksetConfiguration
structure before opening". Verified present on all six versions.

**A workset is remembered by NAME AND BY UniqueId** (`WORKSETPICK`, in
`Shared/`, Revit-free). Neither alone is enough, and the API says why:

- `WorksetPreview.Id` - *"may be changed while synchronizing with
  central"*. So a `WorksetId` is **never persisted**; it is resolved
  from the model in `NamedWorksets` immediately before the open.
- `WorksetPreview.UniqueId` - *"Stable identifier for a workset,
  UniqueId will not change while synchronizing with central"*, and it
  survives a rename too. It is a **`Guid`**, not a string - the
  compiler caught that - and is stored as text because the settings
  file is text.
- `Name` - the only thing readable in a settings file, and all an
  older settings file has. Breaks on a rename.

`WORKSETPICK.Find` tries the id **across the whole list first**, and
only then falls back to names. The order matters: match by name first
and a renamed workset gets matched to whatever now carries its old
name, which is the one answer worse than no answer. Tested directly.

The `jobwork` line is `jobwork <name> <uniqueid>`; the id is the third
field and may be absent, so a settings file written before this existed
still reads and those picks match by name as they always did.

A pick the model no longer has is **shown ticked and marked** `(not in
this model)` rather than dropped, and one found by id under a different
name says `(renamed, was ...)`. The count under the list says what will
ACTUALLY open, not how many are ticked.

`CHWCLOUD` grew `CloudModel.WorksetPicks`, `NamedWorksets`,
`WorksetsOf` and `WorksetsByOpening` for this. A named set **beats the
two-mode choice** (all / only the ones I own); an empty list leaves CHW
VOLUME's behaviour exactly as it was, and CHW VOLUME never fills it in.
Both the local and the cloud open path use it, so the two cannot drift.
`GetUserWorksetInfo` throws on a model that is not workshared - that is
caught and reported as "not workshared", not as an error.

**WHETHER `GetUserWorksetInfo` ANSWERS FOR AN ACC MODEL IS UNDOCUMENTED,
AND IT IS NOT RELIED ON.** The API says nothing either way, which is
conspicuous, because Autodesk *is* explicit elsewhere in that same
class - `CreateNewLocal` documents *"The given path sourcePath is a
cloud path which is not supported in this method"*, and `IsUpToDate`
documents *"This API only works for cloud-based models"*. Every
exception `GetUserWorksetInfo` does document is a file-or-Revit-Server
one (`CentralModelException`, `RevitServerCommunicationException`,
`FileNotFoundException`): it was written around a central that is a
file or a Revit Server, and an ACC model has no such thing to
interrogate.

**AN ACC MODEL SAVED IN A NEWER REVIT CANNOT BE READ AT ALL**, and
that is not a workset problem. `NWCLOG` earned its place on its first
run: Revit 2022 against an ACC model gave
`InvalidOperationException: "The cloud model is not saved in current
release of Revit"`, and no amount of region probing or opening gets
past it. Run the export from the Revit version the model was saved in.
`NWCEXPORT.Explain` now says so in the window rather than showing the
raw message.

**REPORT THE INFORMATIVE FAILURE, NOT THE FIRST ONE** (`CHWCLOUD.Blame`).
Probing regions means the WRONG region always throws
`CentralModelMissingException` - "the central model is missing" - which
is noise: of course it is missing, the model is not in that region.
The first run of the log showed US saying *"not saved in current
release of Revit"* and EMEA saying *"the central model is missing"*,
and **the window showed the second one**, which sent the search in
entirely the wrong direction for two rounds. `Blame` prefers any
exception over that one. It matches **by type NAME**, because
`CentralModelMissingException` is not public and the compiler refuses
to name it.

**`NWCLOG` writes `Desktop\ATKINSMEC_nwc_log.txt`** on every press of
the Worksets button: the Revit version asking, the project and model
GUIDs, each region tried, the resolved user-visible path, **the
exception TYPE as well as its message**, whether the open succeeded and
`doc.IsWorkshared`, and what the window will show. The type matters -
the API documents a different one for "not workshared", "central
missing", "access denied" and "cannot reach the server", and they call
for completely different answers. Two rounds of guessing from a
screenshot were replaced by one read.

**A CLOUD PATH NEEDS A REGION, AND NOTHING READABLE REPORTS IT.** This
is the one that kept the ACC picker empty. `CHWCLOUD.ScanCache` sets
`Region = string.Empty` because the collaboration cache does not carry
it, and `OpenWithRegions` has always coped by *trying* each region and
remembering the one that worked. The workset reads did not: they built
their path from `PathFor(model)`, which uses `model.Region` verbatim.
An empty region gives a path that cannot resolve, so the quick call AND
the background open both failed with *"The central server could not be
reached"* - **user-reported, empty picker on every ACC model, twice**.

`WorksetsOfCloud` and `WorksetsByOpeningCloud` now walk `RegionsToTry`
exactly as the open does, and whichever region answers is written back
onto the job (`NWCEXPORT.Cloudy`), so the probe is paid for once and
the row then shows `ACC - US` rather than `ACC -`. A cloud model's
worksets must never be read through a hand-built `PathFor` again.

**AND THE QUICK CALL ASKS THE CENTRAL EVEN FOR A FILE ON DISK**, which
is the case that actually bit: a local copy whose central is off the
network, a detached model, or an ACC model all answer *"The central
server could not be reached"* - **user-reported, with an empty picker
and no way to choose anything**. Opening the model reads the file
itself and needs no central at all, so it works where the quick call
cannot.

That is why **the fallback runs on its own rather than asking**. It was
behind a Yes/No at first, which is a wall: for a model whose central
cannot be reached the quick way NEVER works, so the same question came
back on every click and the worksets never appeared. The window says
what it is doing instead.

`NWCForm` **remembers each model's worksets by `NWCJOB.Key`** for the
life of the window, empty answers included, so a model is opened at
most once per session rather than once per click. Only the *picker's*
list is remembered - `NamedWorksets` still resolves ids from the model
at export time, so what actually opens is never stale.

**The document already open in Revit answers for itself**
(`NWCEXPORT.WorksetsOfOpen`) and is never reopened - asking Revit to
open a document it already has open throws, and "Add the model open
now" is a button, so that model is often in the list.

`CHWCLOUD.WorksetsByOpening` is the fallback: open the model DETACHED
with `WorksetConfigurationOption.CloseAllWorksets` and no `Open` list,
walk `FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)`,
close it. Closing every workset means it loads the workset table and
almost no geometry, so it is far cheaper than a real open - but it is
still an open, so **the user is asked first and never surprised by it**.
`Workset` derives from `WorksetPreview`, so the names and ids come back
in the same shape and nothing downstream changes.

**Nothing is persisted but the user's selection.** The worksets a model
has are never written to the settings file - they are read from the
model, cached only while the window is open, and resolved again at
export time.

Round-trip harness at `tests/nwcsettingstest` - 38 checks against
the REAL `NWCSETTINGS` (compiled in, not mirrored), covering the things
that break a settings file silently: false values surviving, a
`de-DE` machine writing `3,5` for the faceting factor, tabs inside a
value, job lines being ignored, and a missing or nonsense file being
refused rather than throwing. The per-model lines (`jobview`, `jobset`,
`jobwork`) all attach to **the job last read**, which is the whole
format - so the tests assert not just that they survive but that they
do **not leak onto the neighbouring models**. They go through
`Save`/`Load`, the one path that writes the list, so the harness puts
the real settings file aside and gives it back in a `finally`.

**`CheckedListBox` cannot multi-select.** Setting `SelectionMode` to
anything but `One` throws `ArgumentException` inside
`InitializeComponent`, so the window dies the moment the command runs.
Caught by the preview harness at `tests/nwcpreview` before it ever
reached Revit - which is the whole argument for rendering a hand-written
designer file rather than trusting it. It is why the models list became
a grid, and `NWCWORKSETForm` takes one tick at a time.

**AN UNATTENDED BATCH MUST NOT STALL ON A DIALOG** (`Shared/QUIET.cs`,
at the user's request; it was offered for AUTOSYNC first and not taken
up). Opening twenty models means twenty chances of *"this was made in
an earlier version"*, *"there are unresolved references"* or a
worksharing message, and every one of them waits for a person.

`QUIET.Start(ui, app)` subscribes to **`UIApplication.DialogBoxShowing`**
(the UI assembly - it is NOT on `Application`, which is why `NWCCommand`
now hands the form the `UIApplication` as well) and to
`Application.FailuresProcessing`.

**Which button it presses is tried, not guessed.**
`DialogBoxShowingEventArgs.OverrideResult` *returns a bool* - "True if
the result code was accepted" - so `QUIET` walks `1` (OK), `1001` (the
first custom button, which is Revit's own wording for carrying on, e.g.
"Ignore and continue opening the project"), `8` (Close), `2` (Cancel),
then `6`/`7` (Yes/No) last because those are the ones that can mean
something destructive. A dialog that accepts none of them is **logged
as unanswerable** rather than silently left - that is precisely the
case that stalls a run.

`FailuresProcessing` clears **warnings only**. An error is left exactly
as it is: resolving one can delete elements, and this add-in reads
models rather than repairing them.

**SILENT IS NOT INVISIBLE.** Every dialog answered goes to `NWCLOG`
with its id, its text and the button pressed, and the run itself logs
each model and what became of it. Suppressing a dialog and hiding a
problem are different things.

**`Start` and `Stop` must be balanced, and `Stop` is in a `finally`.**
A handler left subscribed would answer Revit's questions for the rest
of the session - the `QAWATCH` trap in a far more dangerous place.

**Progress is a count, not a spinner.** `CHWSTOP` gained
`Step`/`Went(done, total, what)` beside `Pump`, and `NWCEXPORT.Run`
calls it between models. One model can take minutes, so "3 of 20 -
now <model>" is worth more than an animation. `CHWSTOP` is CHW
VOLUME's type and the addition is optional, so nothing there changes.

**EQUIPMENT CHANGER** - `EQUIPCommand.cs` (+ `EQUIPAvailability`),
`EQUIPForm*.cs`, `EQUIPHELPForm*.cs`, `EQUIPSTOP.cs` (where a resize
stops, deliberately Revit-free), `EQUIPWALK.cs` (following the
ductwork), `EQUIPCHANGE.cs` (the five steps), `EQUIPJOINT.cs` (taking a
joint apart and putting it back), `EQUIPSIZER.cs` (driving ducts and
fittings to a size), `EQUIPTYPES.cs` (what to offer), `EQUIPFAILURES.cs`
(an `IFailuresPreprocessor`), `EQUIPLOG.cs`.

**MODELESS, at the user's request, and it acts on the SELECTION.**
It was written modal and that was wrong for the job: the work is "look
at that unit, pick a type, look at the next one", which a modal window
turns into a loop of opening and closing. Modeless means the model
stays usable, the selection can be changed with the window open
(**Read the selection** re-reads it), and double-clicking a row shows
that element in the view.

The price is the one CLAUDE.md warns about: **nothing in `EQUIPForm`
may touch the API**. It raises `EQUIPEVENT` and is handed plain data -
`EQUIPROW`, `EQUIPTYPEROW`, `EQUIPPICTURE`, all Revit-free. Even the
document title is taken in the constructor, which runs in the command's
context. `EQUIPCommand` keeps the form, handler and event in statics
and `OnFormClosed` clears them.

Anything in the selection that is not mechanical equipment or an air
terminal is **ignored rather than complained about** - people
rubber-band a region and expect the tool to know its own business.

**THE ORDER OF THE FIVE STEPS IS THE WHOLE ADD-IN.** Revit raises

> *"The family is connected to a network and can no longer keep the
> connectivity. Disconnect the family from the network?"*

as an error it labels **cannot be ignored** - user-reported, four at
once, with a screenshot - because the type is swapped while the family
is still joined to ducts it no longer fits. Its only offer is to
disconnect the family and leave the ductwork hanging. So:

1. **Walk** each connector's branch and write down every joint
   (`EQUIPJOINT.Of`), BEFORE anything moves.
2. **Take the joints apart** on purpose (`DisconnectFrom`). This is what
   stops the error: Revit is no longer being asked to keep a connection
   through a change it cannot survive.
3. **Swap the type** - `FamilyInstance.Symbol`, then `Regenerate`.
4. **Resize the branch** to the new connector size (`EQUIPSIZER`).
5. **Join it back up**, moving each duct end onto wherever the new
   connector actually is.

**WHERE IT STOPS IS `EQUIPSTOP`, AND IT HAS NO REVIT IN IT.** The walk
describes each element as an `EQUIPNODE` and asks; the rule is tested
directly at `tests/equiptest` (33 checks). It stops at, and never
touches, a **reducer**, a **tap or spud**, a **tee or cross**, an **air
terminal**, and **other equipment**.

A reducer in particular **exists because the size changes there** -
going past it would undo somebody's design, and everything beyond it
belongs to the main rather than to this unit. That is the same argument
`QACHW` makes for never listing one.

**What a family CALLS itself wins, then its geometry.** A family that
says `Transition` is a reducer even if its two ends measure the same
today; a family that says nothing is read from its ends - **one end is
a tap** (its main side is hosted on the duct's body and is not an end
connector at all, the same thing that made `QADUCTFIX` replace a
hanging tap rather than repair it), **more than two is a tee**, and
**two of different sizes is a reducer whatever it is called**.

**CONNECTORS ARE MATCHED BY POSITION IN SPACE, NOT BY LIST ORDER**
(`EQUIPJOINT.Nearest`). A different family can order its connectors
differently, and reconnecting by index would join the supply duct to
the return connector - a mistake that looks perfectly fine on the
drawing and is wrong in every calculation afterwards. Each new
connector is claimed once, nearest to where the old one was, which is
the greedy matching `ALIGNTERMINALS` already uses.

**"Set the ducts and the fittings follow" is as false as it was for
pipes.** `EQUIPSIZER` drives the ducts first (a fitting takes its size
from the ducts on it), regenerates, then the fittings - and **reads
each fitting back**, because a lookup-table family takes the write and
quietly reverts. One that will not change is reported rather than
replaced: it is visible on the drawing, and replacing it would throw
away whatever is written on it. Exactly `QACHWFITTINGS`' reasoning.

**A SHAPE CHANGE IS REFUSED, NOT FORCED.** A round connector on a
rectangular duct needs a different duct type and different fittings, so
that branch is left alone and reported.

**"IT DOESN'T CHANGE THE FAMILY TYPE" HAS TWO CAUSES, AND BOTH WERE
REAL.** User-reported on the first run:

1. **It was already that type.** Setting `Symbol` to the symbol it
   already has succeeds, changes nothing, and reports success - which
   from outside is exactly a broken button. A family with one type
   offers only the type the unit already has, so this is the DEFAULT
   experience for such a family. Now: the combo marks the current type,
   opens on a different one, the Apply button says so before doing
   anything, and `Outcome.AlreadyThat` is counted separately from
   refused.
2. **REVIT PROCESSES FAILURES WHEN THE TRANSACTION COMMITS**, not when
   each `SubTransaction` does. So every element ran with
   `quiet.Errored` still false, the SubTransactions all committed, and
   a preprocessor answering `ProceedWithRollBack` at the very end took
   **the whole run** with it - every element, including the ones that
   were fine - while the window reported success for all of them.
   `EQUIPCHANGE.Verify` now asks the MODEL, after the commit, what each
   element's symbol actually is, and rewrites the outcomes to match.
   **Never report a model change from what was attempted.**

**REWRITING A LINE MUST KEEP ITS DIRECTION** (`EQUIPLINE.MovingIsStart`).
Revit stores a duct or pipe as a `LocationCurve` running from endpoint
0 to endpoint 1, and its connectors belong to those ends. Build the new
line far-end-first and the curve is REVERSED, which Revit throws out
with

> *"The duct/pipe has been modified to be in the opposite direction
> causing the connections to be invalid."*

**User-reported from the log**, and the log is the only reason it took
one round: every step of a real equipment change succeeded - walk,
unhook, swap, three resizes, rehook - and the whole transaction was
then rolled back at the commit by that one error.

**The same mistake was in `QACHWROOM.Restretch`**, shipped, since
`Other(line, was)` returns the FAR end and it was written
`Line.CreateBound(other, moved)`. There `Sound` catches it as "a pipe
has turned" and rolls the slide back, so it reads as a route that
simply never works rather than as a flip - which is a fair description
of how often that route succeeded. Both now build `(moved, finish)` or
`(start, moved)` according to which end is moving. The rule is one
comparison, so it is named, shared and tested at
`tests/equiptest` rather than written twice.

`QACHWROOM`'s other two uses of `Other` are a length check and a
direction, neither of which rewrites a curve; only `Restretch` was
wrong.

**`EQUIPFAILURES` never resolves an error.** Revit's own resolution for
the connectivity one is "Disconnect the family from the network", which
is the exact damage this exists to prevent. Warnings are cleared; an
error means `ProceedWithRollBack` and a reported reason. Each element
is its own `SubTransaction` inside one `Transaction`, so a refusal
costs that element and nothing else, and the whole run is one Ctrl+Z.

`EQUIPWALK.Reach` = 40 elements. A branch longer than that has almost
certainly escaped onto a main, and resizing a main because somebody
swapped a fan coil is the worst thing this could do - so it is refused.

**`EQUIPLOG` writes `Desktop\ATKINSMEC_equip_log.txt`** on every run:
which connector, how far the walk went and why it stopped, every duct
resized from and to, every fitting that would not take its new size,
and the exception TYPE for anything Revit refused.

**DUCT ESP** - `ESPCommand.cs` (+ `ESPAvailability`), `ESPForm.cs`,
`ESPCOLLECT.cs` (the only file beside the command that touches Revit),
`ESPNET.cs` (the network, the joints between models, the walk, every
loss - Revit-free), `ESPAIR.cs` (friction and velocity pressure,
Revit-free), `ESPSETTINGS.cs` (the coefficient table, family values,
options and their file, Revit-free), `ESPROW.cs`, `ESPEXPORT.cs`,
`ESPLOG.cs`. Asked for after the user's question *"is ESP calculation
addin possible?"*, with *"provide option to use linked model in
calculation where there are cases where duct risers and AHUs are
located in different model and the duct networks in other models"*.

**ITS OWN CALCULATION, NOT REVIT'S.** Revit already computes sections
and a critical path per system (`MEPSystem.GetCriticalPathSectionNumbers`,
`PressureLossOfCriticalPath`, `MEPSection.TotalPressureLoss`) - but only
INSIDE one model. A riser in a linked model whose terminals are in this
one carries no flow there, so Revit gives it no loss; an AHU in another
model is not on the system at all. So `ESPCOLLECT.Read` reads every duct,
flex duct, fitting, accessory, terminal and unit of this model and the
ticked links into `ESPNET` - metres, m3/s and Pa, every point and
direction through its link's TOTAL transform - and everything after that
is arithmetic.

**HOW THE MODELS MEET (`ESPNET.BridgeModels`).** Ducts in different
models can never be connected, so two OPEN ends in different placements,
within the tolerance (default 150 mm) and FACING (outward directions
within 60 degrees of opposite), are taken as one joint - each end once,
closest first, ties settled by element so two runs agree (ALIGNTERMINALS'
greedy matching). **Two open ends in the SAME model are never joined**,
however close: that is a drawing fault for SHEET QA's duct page, not
something to paper over. Pairs out to three tolerances that were NOT
joined are kept as near misses, so a run that stops short can be
explained; the log lists both.

**THE WALK (`ESPNET.Walk`)** starts at one of a unit's duct connectors
and builds a tree over PORTS, iteratively (a riser is thousands of
elements deep): across a joint, then through a part - a fitting to all
its other ports, a duct along its CHAIN. **A duct is a chain**: its two
ends and any taps on its body (connectors of type `Curve`), ordered by
`Along`, so the main is costed in pieces, each carrying what is still in
it. It stops at an air terminal (a leaf, with its flow), at OTHER
equipment (a leaf - an FCU fed with fresh air by an AHU takes over from
its connector; 0 Pa unless a family value says otherwise), at a dead end,
and where it comes back to the unit. A port reached twice is a loop: cut
and counted. Flows come from the leaves back (the terminal's own Flow,
else its connector's assigned flow); losses from the unit out.

**WHERE EACH FIGURE COMES FROM**, in this order, every step saying which:

- duct and flex duct: **Darcy-Weisbach with Colebrook-White**
  (`ESPAIR`), hydraulic diameter and actual velocity for rectangular and
  flat oval; roughness from the duct type (`MEPCurveType.Roughness` - the
  parameter was `RBS_CURVETYPE_ROUGHNESS_PARAM` to 2023 and
  `DUCT_ROUGHNESS` from 2024, the property is on all six) else the
  setting (0.09 mm; flex 3 mm); a flex duct on its own length;
- fittings: **your value for the family (Pa)** > the family's own
  coefficient (`RBS_LOSS_COEFFICIENT`) > the table, by kind - what the
  family CALLS itself (`PartType`, then the family's Part Type
  parameter), then its geometry (2 ends: elbow / union / transition by
  size; 3: tee; 4: cross); **through or branch is decided by the PATH**
  (in one side and straight out the other is "through");
- accessories: your value > the family's own pressure drop
  (`Connector.AssignedPressureDrop`) > its own coefficient > "Damper" >
  the flagged fallback;
- terminals: your value > the family's own pressure drop > the setting
  (25 Pa), FLAGGED.

`C x velocity pressure` on the leg the path LEAVES by for a branch, a
run through, or a tap; on the faster side for anything else. **The
table has a supply (diverging) and a return (converging) column**, and
a run is converging when the unit's connector says air goes IN.

**THE BUILT-IN COEFFICIENTS ARE ROUGH, AND SAY SO EVERYWHERE.** They are
typical figures chosen for this add-in - NOT copied from the ASHRAE Duct
Fitting Database or CIBSE Guide C, whose tables are copyrighted. The
window, the results, the export and the write confirmation all say when
they are still the built-in ones (`ESPSETTINGS.TableIsBuiltIn`), and the
Coefficients tab takes the office's own, saved to
`%AppData%\ATKINSMEC\esp_settings.txt` (invariant culture, tested on
de-DE). **Anything that came from a default is FLAGGED** - amber in the
window and the export, counted per unit - and "Add the families the last
run had no data for" lists them for a value.

**ESP = THE WORST OUTLET + THE WORST INLET, plus the margin**
(`ESPNET.Calculate`). Right for the common units: a fan coil is supply
plus return; an exhaust fan its suction plus its discharge; an AHU's
return and fresh air arrive in parallel at the mixing box, so the worse
of them counts, not both. An AHU with its own return fan needs two
figures, which is why every port's own loss is shown and exported. The
connector's `Direction` says which side a port is; failing that the
system (supply and exhaust leave, return and other air arrive).

**A PORT THAT REACHES NO TERMINAL IS A FINDING, NOT A ZERO** - amber in
the window, "reaches no terminal" in the export, with a note naming the
likely cause (an open end, a cap, or a joint to another model that was
not made - see the near misses).

**WRITING** (`ESPCOLLECT.Write`) is to units in THIS model only, onto
the parameter named in the window (default "External Static
Pressure"): a pressure parameter in Pa, a plain number as Pa, an integer
rounded, text as "123 Pa"; a length or flow parameter is refused rather
than given a number that means something else. `LookupParameter` finds
INSTANCE parameters, so a type parameter of that name reads as missing.

**THE WINDOW IS BUILT IN CODE** (`ESPForm`), in table panels and docking
rather than designer positions, Revit-free through callbacks so it
renders without Revit. Three things that cost a render to find:

- **a code-built form gets no DPI scaling unless asked**:
  `AutoScaleDimensions = (7, 15)` + `AutoScaleMode.Font` inside
  `SuspendLayout`/`ResumeLayout`, as a designer form does. Proven in the
  preview by a probe form built the same way (a 100x40 child comes out
  143x67 at a 1.5x font - font-based scaling is not exactly 1.5);
- **its design size does not fit a 1080p laptop at 150%** (about
  1786 x 1100 against ~1030 above the taskbar), so it names its OWN
  `MinimumSize` (900 x 600 - table panels reflow, unlike anchors, so it
  may shrink below its design size; `FORMSIZE` leaves a named minimum
  alone) and **fits itself to the screen's working area on Load**;
- helpers named `Text` and `Shown` would have hidden `Form.Text` and the
  `Shown` event - renamed before the first build (`Column`, `Filtered`).

**A takeoff joined to a `Curve` connector the duct did not list** is
given a tap position on that duct from the takeoff's own data
(`ESPCOLLECT.TapOnDuct`). Ducts are expected to list their tap
connectors; whether every version does was not verifiable without
Revit, and a missing one would silently drop a whole branch and its air.

**`ESPLOG` writes `Desktop\ATKINSMEC_esp_log.txt`** on every Calculate:
the settings and whether the table is built-in, what was read from each
model, every joint made between models with both elements and the gap,
the near misses, and per unit per port the counts, flows at the unit and
at the terminals, the index and every step with where its figure came
from.

Harnesses: `tests/esptest`, 60 checks against the real `ESPAIR`,
`ESPNET` and `ESPSETTINGS` - friction against an INDEPENDENT Colebrook
(400 mm round at 500 L/s, 0.4502 Pa/m), index runs to the pascal through
a tee and along taps, joints between models (made, refused in one model,
refused facing the same way, near miss reported, nearer wins, the same
every time), where each figure comes from, the ESP rule, a ring main,
and the settings file on de-DE. `tests/esppreview` renders the real
window at 1x and 1.5x (fitted to this machine's 1920 x 1032 working
area - exactly the laptop case) on a network built through the real
`ESPNET` - an AHU and riser in a link bridged into this model, a
fresh-air louvre 300 mm off reported as a near miss, two fan coils - and
exports it through the real `ESPEXPORT`.

**Never run in Revit.** Every Revit read in `ESPCOLLECT` compiles on
2022 and 2027 and is wrapped so one unreadable element is skipped and
counted, not fatal - but which models list tap connectors, what
`AssignedPressureDrop` and `AssignedFlow` hold on real families, and how
far apart real risers and floor ducts actually stop are all for the
first real run and its log.

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

## Every window resizes

At the user's request, **all eighteen windows are `Sizable` with a
maximise and a minimise box**. That is three parts, and only the first
is the obvious one:

1. **The border style and the two boxes**, in each designer.
2. **Anchors on every control** - 221 of them. Without these a bigger
   window is just more grey: anchors are what hand the extra space to
   the table somebody wanted bigger. The shape is the same everywhere -
   the group boxes at the top stretch sideways (`Top|Left|Right`), the
   findings/results/log table takes the height (`Top|Bottom|Left|Right`),
   the buttons down the right of a list hold the right edge
   (`Top|Right`), the summary under a table holds its bottom
   (`Bottom|Left|Right`), and the buttons along the bottom stick to
   `Bottom|Left` or `Bottom|Right`.
3. **A minimum size**, `Shared/FORMSIZE.cs`. A window may not shrink
   below the size it was DESIGNED at, because anchors move controls and
   do not reflow them. That size cannot be typed into the designer:
   Revit runs at whatever the machine's scaling is and WinForms scales
   the form on the way up, so `FORMSIZE.Remember` hooks `Load` and
   takes `MinimumSize` from the form once it is built and scaled. **A
   form that already names a `MinimumSize` is left alone** - the four
   help windows deliberately say a smaller one, being a text box that
   reads perfectly at half the size.

**A control BETWEEN two groups must not be anchored to the bottom.**
`btnExport` and `btnStop` on NWC BATCH sit between the options group
(top-anchored) and the results group (which grows). Anchored
`Bottom|Left` they slid down by the full height increase and ended up
behind the results table - they simply vanished. They belong to the top.
Found by rendering the window 300x260 bigger in `tests/nwcpreview`,
which is the only reason it was caught.

**`tests/anchorsim.py` asks that question of every form at once.**
It reads the designers, applies WinForms' own anchor arithmetic to a
400x400 growth, and reports siblings that end up on top of each other -
telling a NEW overlap from one that was already there. Only the NWC
windows can actually be drawn without Revit, so for the other thirteen
this is the whole verification. Run it after touching any layout. (Its
own first version read "no `Anchor` line" as unanchored rather than as
WinForms' default of `Top|Left`, and reported thirteen collisions that
were not real - the default matters.)

**One genuine layout bug fell out of this.** In `SPRINKLERForm` the
"Clear of walls (mm)" label and its box sat ON TOP OF the Place and
Close buttons (x 748-946 against 740-952, both on y 610-640). Nothing
could be anchored there without deciding where the buttons go, so they
were given a row of their own at y 688 and the window grew from 692 to
730 high. Its designer also has header comments that name the wrong
control (`// lblFallback` above the `lblClearance` block), which is why
anchors are inserted before each `.Location` line rather than after the
comment header.

**`SYNCASKForm` had `ShowInTaskbar = false`** and now has a minimise
box, which together make a window somebody loses. It shows in the
taskbar now.

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
- **A MODELESS WINDOW ACTS ONLY ON THE MODEL IT WAS OPENED ON**
  (`Shared/SAMEDOC.cs`, 2026-10-07). Every handler used to work on
  `app.ActiveUIDocument` - whatever model was in FRONT when Revit ran
  the event - with element ids collected in the window's own model. Open
  SHEET QA on A, switch to B, press Fix, and the fix ran in B on ids that
  are other elements there, or nothing; a QA fix creates and deletes.
  Now the command gives the handler its window's model (`Handler.Home`,
  `HomeTitle`, set beside `Handler.Form`), and the handler asks
  `SAMEDOC.Problem` before ANY request: wrong model in front, model
  closed, or none open, it does nothing and the window says which,
  through the same method it reports errors with (`ReportError`,
  `Trouble`, AUTOVENT's status line plus a dialog for a run or export).
  SHEET QA's `Repaint` waits silently - the live markers ask for one on
  every change of any model. `Document.Equals` is Revit's own "same open
  document" test. **A new modeless window needs both halves**: `Home` set
  in its command, and the check at the top of its handler's `Execute`.
  Not runnable outside Revit, so untested; never run in Revit.
- **Subscribing to a Revit event is itself an API call.**
  `app.DocumentChanged += ...` from a modeless form's own button or
  checkbox handler throws *"Revit is currently not within an API
  context"* — this was hit for real in SHEET QA. Both the `+=` and the
  `-=` must happen inside the external event handler. `QAWATCH.Start` /
  `Stop` are marked context-only for that reason, and `QAWATCH.Disarm()`
  exists as the no-API way to silence a watcher from `OnFormClosed`,
  where raising an event is all a dying form can do.
- **`Document.IsModifiable` means "a transaction is open on this
  document RIGHT NOW"** — not "this document can be edited". Autodesk's
  own words: *"Regardless of the mode a document is opened with, the
  model can only be modified inside an open transaction."* So outside a
  transaction it is **false for every document, always**. AUTOSYNC
  guarded on it and skipped every model on every run, logging "the model
  was busy" — a total failure that read like a real condition. There is
  now a comment in `SYNCEVENT` saying not to put it back.
  `Document.IsReadOnly` is dynamic too, and is mostly *"Revit is
  processing failures at this moment"* rather than "opened read-only";
  it is a fair "not right now" guard but not an "is this file
  writable" test. **The API `.xml` beside the NuGet dll answers these
  in seconds** — prefer it to memory:
  `~/.nuget/packages/nice3point.revit.api.revitapi/<ver>/ref/<tfm>/RevitAPI.xml`.
- **Reading `doc.Title` from a modeless form is the same trap.** Take
  what the window needs in the constructor, which runs inside the
  command's context, and hold the string. `QAForm._docTitle` does this.
- **Capture geometry once.** `SPRINKLERPLAN` extracts each room's outline
  into `SPRINKLERLAYOUT.RoomShape` while the API context is valid, so
  later recalculation is pure arithmetic and needs no Revit access.
- **A `Connector` DIES WITH THE GEOMETRY IT WAS READ FROM.** Rewrite a
  pipe's `LocationCurve`, or `MoveElement` it, and every `Connector`
  taken beforehand is invalid: the next call throws
  *"The referenced object is not valid, possibly because it has been
  deleted from the database, or its creation was undone."* Re-read it
  with `QADUCTS.Lookup(doc, ownerId, connectorId)` after EVERY
  geometry change - the (owner, connector id) pair is the only thing
  that survives, which is the same rule `QADUCTFIX` already follows
  for carrying a connector across a transaction.
  **This cost the CHW disconnection page every single join in a real
  model - "0 joined, 25 refused"** - and it failed in two different
  ways at once, which is why it was not obvious: a stretch threw
  loudly on the next call, while a slide failed SILENTLY, because
  `Origin()` on a dead connector is caught and returns null and the
  caller simply gave up. `QACHWJOINTFIX.Align` therefore RETURNS the
  live connector rather than a bool, so a caller cannot forget.
- **`PipeInsulation` IS an `MEPCurve`, AND INSULATION APPEARS IN
  `Connector.AllRefs`.** Either alone would be survivable; together
  they defeat the obvious guard, because `element as MEPCurve` ACCEPTS
  a sleeve of fibreglass. Compile-verified: `MEPCurve curve =
  insulation;` builds on 2022. So a network walk that takes the first
  owner out of `AllRefs` gets the insulation instead of the fitting,
  and then measures against it - the CHW log read *"its far end is
  joined to Pipe Insulations - Fiberglass"* and *"sliding the fitting
  above 33 mm along Pipe Insulations - Fiberglass"*.
  **Filter by CATEGORY**, which no class hierarchy can confuse
  (`QACHWJOINTFIX.Pipework`). `QACHWRUNS` escapes this only because it
  happens to test `other.Domain != Domain.DomainPiping` first.
- **THE EQUIPMENT CHANGER HAD THE SAME BUG, AND DUCTWORK HAS TWO
  SLEEVES RATHER THAN ONE.** The note above used to end "unconfirmed,
  and worth checking"; this is the check. **`DuctInsulation` derives
  from `MEPCurve`, and so does `DuctLining`** - both compile-verified
  the same way (`MEPCurve curve = insulation;` builds against the 2022
  reference assemblies). Pipework has no lining, so the duct side has
  two things that pass the cast instead of one.

  **A `ConnectorType` test is not enough on its own**, which is what
  made this worth looking at: `EQUIPWALK.Across` filtered
  `ConnectorType.End`/`Curve` and self-id and nothing else, and
  `EQUIPJOINT.Of` filtered `ConnectorType.End` and self-id. Neither
  has any way to tell a sleeve from the duct inside it.

  **In the walk it is a button that does nothing**, and the chain is
  worth following because no single step looks wrong: `Across` hands
  back the sleeve -> `Describe` reads a category that is neither duct
  nor fitting and calls it `Sort.Other` -> `EQUIPSTOP` turns `Other`
  into `Unknown` -> and **`EQUIPCHANGE` line 411 refuses the WHOLE type
  change** when `Unknown` comes back on the first hop with nothing
  resized. An insulated duct on a fan coil is the ordinary case, so
  that is a **third cause** of *"it doesn't change the family type"*,
  beside the two already written up under EQUIPMENT CHANGER.

  **In the joint it is worse than doing nothing.** `EQUIPJOINT.Of`
  would write the sleeve down as what the connector was joined to, so
  `Undo` unhooks the SLEEVE and leaves the real duct joined - and the
  type swap then raises the very *"cannot keep the connectivity"*
  error the five-step order exists to prevent. `Redo` -> `Bring` would
  go on to rewrite the **insulation's** `LocationCurve`, moving a
  sleeve of fibreglass while the duct stayed where it was. The cast
  there (`doc.GetElement(...) as MEPCurve`) is exactly the one that is
  not a guard.

  Both now ask `EQUIPWALK.Ductwork`, an **allowlist by category** -
  `OST_DuctCurves`, `OST_FlexDuctCurves`, `OST_DuctFitting`,
  `OST_DuctAccessory`, plus `OST_MechanicalEquipment` and
  `OST_DuctTerminal` **on purpose**, because the walk has to REACH
  those two for `EQUIPSTOP` to say "stopped at an air terminal" or
  "stopped at other equipment". An allowlist rather than a list of
  sleeves to avoid, so a category nobody has thought of is skipped
  too. **`Across` carries on past a sleeve rather than giving up** -
  the duct it wraps is in the same list, usually right behind it.

  **`EQUIPWALK.Joins` needs no filter and deliberately has none**: it
  asks whether one KNOWN id is in the list, so a sleeve fails the
  comparison and can neither be returned nor mistaken for what is
  being looked for. Same reason `QACHWJOINTFIX.Joined` was left alone
  when the pipe walks were filtered.

- **THE `Domain` TEST IS LOAD-BEARING WHEREVER IT IS THE ONLY THING
  STANDING THERE.** `QACHWROOM.Beyond`, `QACHWFITTINGS.PipeSide` and
  `QAFCU` all escape this only because they ask the domain before
  anything else - `PipeSide` most pointedly, since its own
  `other.Owner is MEPCurve` is precisely the cast that does not work
  and the domain line above it is all that saves it. Comments now say
  so in all three, because a tidy-up that removed a "redundant" test
  would reintroduce the bug silently. `QACHWFITTINGS.Refs` returns
  everything unfiltered, so its callers must filter - both do,
  `Touching` by category through `QACHWRUNS.IsFitting`/`IsAccessory`.

- **`QADUCTFIX.Branching` WAS THE LAST UNFILTERED ONE, and is now done
  too.** It tested `ConnectorType.End` and self-id, the same shape
  `EQUIPJOINT.Of` had. The `Domain.DomainHvac` test above it is **on
  the TAP's own connector, not on what comes back**, so unlike
  `QACHWROOM.Beyond` or `QAFCU.Across` it had no incidental guard
  either - the weakest of all the sites.

  **What the pair becomes is why it mattered.** `DoTap` hands
  `(ownerId, connectorId)` to `NewTakeoffFitting` as the BRANCH
  connector, **having already deleted the tap**, so a sleeve there
  means Revit is asked to build a takeoff onto fibreglass. The
  `SubTransaction` rolls that back, which is the good case and also
  the misleading one: the tap reads as *"could not be replaced"* when
  it was perfectly fixable.

- **THE DUCT CATEGORY LIST NOW EXISTS ONCE**, as `QADUCTS.Carries`,
  beside the `Carriers` array the duct scan already collects by. That
  is the point rather than tidiness: **the scan and anything walking
  the network afterwards have to agree about what a duct network is
  made of**, and two hand-kept copies would be two things to drift.
  `EQUIPWALK.Ductwork` is now one line onto it - no new dependency,
  since EquipChanger already leans on `QADUCTS` for `Manager` and
  `Lookup`. The equipment/terminal entries stay in the list for
  EQUIPWALK's reason: the walk must REACH them for `EQUIPSTOP` to say
  "stopped at an air terminal".

- **THE TEST HARNESSES ARE IN `tests\`, AND A NEW ONE GOES THERE TOO -
  NEVER IN THE SCRATCHPAD.** They used to live in the session scratchpad
  under `%LocalAppData%\Temp`, which Windows cleans, and by 2026-10-06
  it had eaten whole harnesses and single files out of others. They
  were moved into the repository that day and **rebuilt from the session
  transcripts** (`.jsonl` under `~\.claude\projects\`), which record the
  full input of every Write and Edit: replaying those per file, in time
  order, gave back every lost harness but `aligntest`. Edits made by a
  SCRIPT are not in that record, so those were re-run on copies - which
  is why `chwjointtest` has 55 of its 64 checks and `nwcsettingstest` 18
  of 38. `roomtest` and `fittingtest` came back stale and were brought
  in step the same day, `aligntest` was rewritten against the real code,
  and only `geomtest` and `nwcpreview` remain **stale**
  (`tests\README.md`). **A harness written
  through a shell script is invisible to that record** - one more reason
  to write files with the Write and Edit tools.

  Each `.csproj` compiles the add-in's files by RELATIVE path
  (`..\..\ATKINSMEC\...`); inputs are found from the repository root
  (`tests\Shared\RepoPath.cs`), and the two workbooks that used to be
  read off Downloads and the Desktop are in `tests\data\`.
  **`tests\run-all.ps1` builds and runs every current one** - run it
  after any change to a Revit-free file:

  ```bash
  powershell -NoProfile -ExecutionPolicy Bypass -File tests/run-all.ps1
  ```

  `run-all.ps1 officetest esptest` runs some by name. **Its list needs
  `ValueFromRemainingArguments`**: under `-File` a plain `[string[]]`
  binds only the FIRST name and silently drops the rest.
- **Visual Studio's XAML designer writes handlers on a double-click.**
  `VENTWindow.xaml` gained `SelectionChanged="TabActions_SelectionChanged"`
  and an EMPTY method for it between two builds on 2026-09-30 - nobody
  asked for it; it is what double-clicking the tab control in the
  designer does. Harmless in the add-in, but it broke
  `tests/ventpreview`, whose stub must have every handler the XAML
  names. Left in; the stub has a matching empty one.
- **Never let an `IUpdater` throw** — it would abort the user's own
  drawing transaction. `DUCTINSULATIONUPDATER` swallows everything.
- **`python` ON THIS MACHINE CAN RESOLVE TO THE WINDOWS STORE ALIAS**
  (`AppData\Local\Microsoft\WindowsApps\python`), which answers
  "Permission denied" - and a script piped to it then hangs or does
  nothing at all while its task reports success. Found on 2026-10-06:
  three edit scripts silently changed nothing. The real install is
  `C:\Users\moham\AppData\Local\Python\pythoncore-3.14-64\python.exe`;
  call it by that path, and make C# edits with the Edit tool anyway.
  **openpyxl on the office workbook does not finish** - it ran for
  fourteen CPU-hours before being stopped. Read that workbook with a
  small EPPlus probe instead (`tests/aptprobe`), as the add-in does.
- **Bash heredocs mangle C#.** Writing C# through `python - <<'PY'` in
  the Bash tool corrupts `\r\n`, `\t` and `' '` char literals. Use the
  Write/Edit tools for C# containing those, or verify afterwards.
- **Verifying against the Revit API by reflection is unreliable in
  PowerShell** — a failed `GetType`/assembly load leaves stale values in
  the variable and silently reports the previous version's answer. Two
  wrong conclusions came from this. Prefer compiling, or scan the DLL
  bytes.
- AUTOVENT writes `ATKINSMEC_debug_log.txt` to the Desktop on every run —
  the fastest way to diagnose it - and `ATKINSMEC_apartments_log.txt`
  every time the apartments map is built.
- A WinForm with no Revit types in it (e.g. `CHWHELPForm`) can be
  previewed without Revit: a scratch `net8.0-windows` WinExe that
  `<Compile Include=`s the form's two files, calls `Show()` then
  `DrawToBitmap`, and saves a PNG. Fastest way to check wording and
  layout before deploying.
- The matching harness for ALIGN TERMINALS (`tests/aligntest`) was
  lost and has been REWRITTEN against the real `ALIGNMATCH` - see ALIGN
  TERMINALS above.
- Geometry harness for the sprinkler layout: `tests/geomtest` - the
  REAL `SPRINKLERLAYOUT` and `SPRINKLERRULES`, compiled against
  stand-ins for the Revit types they name (`RevitStub.cs`). See
  SPRINKLERS above.
