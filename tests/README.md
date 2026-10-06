# Test harnesses

None of these needs Revit. Each is a small console (or WPF/WinForms)
program that compiles the add-in's **Revit-free** source files directly -
`..\..\ATKINSMEC\...` in its `.csproj` - and checks them. A few MIRROR
add-in logic by hand instead; those are marked below, and have to be kept
in step by hand when the add-in changes.

Run every current one:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests\run-all.ps1
```

or some of them by name: `... tests\run-all.ps1 officetest esptest`.

## Where they came from

Until 2026-10-06 these lived in the Claude session scratchpad under
`%LocalAppData%\Temp`, which Windows cleans. By the time they were moved
here several had lost their source entirely. Those were **rebuilt from the
session transcripts**, which record every file write and edit: the writes
and edits were replayed in order, then the scripted edits that the replay
cannot see were run again on copies. What could not be brought fully up to
date is listed under **Stale** below.

Inputs that used to be read from Downloads and the Desktop are in `data\`,
and harnesses find files from the repository root (`Shared\RepoPath.cs`),
so they run from any clone.

## Current - run by `run-all.ps1`

| Harness | What it checks | Compiles |
|---|---|---|
| `officetest` | AUTOVENT export from the office workbook, read back through the real importer and calculated by EPPlus (427 checks) | VENTOFFICE, VENTROWS, VENTSHEET, VENTTEMPLATE |
| `ventleveltest` | Rooms onto this model's storeys; `Floors`; apartments verified on this model's levels | VENTLEVELMAP, VENTAPARTMENTMAP |
| `ventsettingstest` | Fresh/extract fallback, endcap reach, the WC/urinal name rule, the AUTOVENT settings file | VENTSETTINGS |
| `levelrowtest` | The levels tree's ticks and picks | VENTLEVELROWS |
| `ventfcutest` | Splitting a room's air between endcaps - conservation | VENTFCUCHECK |
| `toggletest` | WPF's click on a third-state tick, through the real `OnClick` | (WPF only) |
| `esptest` | DUCT ESP friction, index runs, joints between models, settings file | ESPAIR, ESPNET, ESPSETTINGS |
| `equiptest` | Where an equipment resize stops; line direction | EQUIPSTOP, EQUIPLINE |
| `chwjointtest` | CHW pipe to fan coil: system match, connector choice, how a pipe is brought (55 of the 64 checks it once had) | QACHWALIGN |
| `chwsizetest` | The built-in ASHRAE sizing table against the office workbook, and its round trip | CHWSIZESHEET |
| `sheettest` | The FCU schedule reader against `data\FCU_Schedule.xlsx` | FCUSHEET |
| `nwcsettingstest` | NWC BATCH settings round trip (18 of the 38 checks it once had) | NWCSETTINGS, WORKSETPICK |
| `synctest` | AUTOSYNC reachability probes, timed, and its settings | SYNCNET, SYNCSETTINGS |
| `runtest` | CHW run grouping - **mirror** of QACHWRUNS | (mirror) |
| `ducttest` | Duct spatial index and meeting ends - **mirror** of QADUCTS | (mirror) |
| `icontest` | The eighteen ribbon icons decode out of the BUILT 2025 DLL - build the add-in first | (reads the DLL) |

## Previews - run by hand, then LOOK at the PNGs

| Harness | What it renders |
|---|---|
| `ventpreview` | The real AUTOVENT XAML with a stub code-behind, at 1x and 1.5x. `ventpreview.exe <folder>`; with `tutorial` it also writes the control positions the tutorial video uses |
| `esppreview` | The DUCT ESP window on a network built through the real ESPNET, at 1x and 1.5x |
| `video\` | The AUTOVENT tutorial: `make_video.py` (storyboard and frames), `music.py` (composed soundtrack), `mp4enc` (Media Foundation encoder and checker) |
| `aptprobe` | Not a test - prints the apartment pattern out of the office workbook |
| `anchorsim.py` | Applies WinForms' anchor arithmetic to every designer and reports controls that end up on top of each other |

## Stale - kept, not run

Recovered at an older version and out of step with the add-in. A failure
in one of these says nothing about the add-in.

| Harness | State |
|---|---|
| `roomtest` | Mirror of QACHWROOM. 4 of its checks fail - the mirror predates the tee and dead-end fixes the later checks were written for |
| `fittingtest` | Mirror of QACHWFITTINGS. 1 check fails (an unknown family far off-axis as an elbow) |
| `geomtest` | Mirror of SPRINKLERLAYOUT from 2026-08-31; prints a layout, asserts nothing |
| `nwcpreview` | Predates the worksets picker; does not build |

`aligntest` (ALIGN TERMINALS matching) could not be recovered at all.
