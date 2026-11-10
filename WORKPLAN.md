# Work plan — state and next steps

## NEXT: an AUTOVENT video presentation, recorded in Camtasia with Revit open - planned 2026-10-07

**The user wants a video of AUTOVENT working inside Revit, recorded with
Camtasia, with Claude driving it.** Claude has no desktop control of its
own (only the terminal and the browser pane), so a Windows desktop MCP
server was set up for this:

- **`windows-mcp`** (CursorTouch/Windows-MCP, on Windows UI Automation:
  screenshots, the UI tree with element names, click, type, launch apps).
  Configured in **`.mcp.json` at the repository root** - command
  `C:\Users\moham\.local\bin\uvx.exe`, args `windows-mcp serve`. The
  file is in `.gitignore` (the path is this PC's). `uv` is installed;
  Python is 3.14 (it needs 3.13+). The `claude` CLI is NOT on this PC's
  PATH - the desktop app runs Claude Code internally - which is why the
  file, not `claude mcp add`.
- **First thing in the new session: check the tools loaded** (take a
  desktop screenshot). If not, the user adds the server in the app's own
  MCP settings with the same command and arguments. The app may ask to
  approve the project's server once.
- **It has full system access** - use it only for what the user asks.
  **The user must not touch mouse or keyboard while it drives**, and
  Camtasia records whatever happens on screen, so every move is in the
  video.

**Software:** Camtasia **2026 (26.2.2)** at `C:\Program Files\TechSmith\
Camtasia` - no public automation API, so it is driven through its UI.
Revit **2025** was the one open. Its add-in folder now holds the build of
2026-10-09 (hash-checked): everything, the links search box and the
Interior models dialog included. Revit **2024** was open that day instead,
so ITS folder is the stale one - redeploy (`build-all.ps1` with Revit
closed) before recording in whichever version is used.

**Model:** the host the user has open is **"Snowdon Towers Sample HVAC"**
(Autodesk's sample), linked to Snowdon Towers Sample Architectural,
Electrical, Facades, Plumbing and Site - links and rooms on L2 and up.
**The airflow run WRITES to air terminals: never save the sample after
it, or work on a detached copy.**

**Shot list** (one take per shot, so a retake is cheap; Revit maximised,
Camtasia Recorder full screen at 1920x1080, cursor highlight on):

1. Title card (prepared beforehand - see below).
2. Atkins MEP Toolkit > Mechanical > AUTOVENT opens the window over the model.
3. **Linked models**: tick Snowdon Towers Sample Architectural; point out
   the second line (which of this model's levels it covers) and the
   Apartments tick beside each link (say what it is for; the sample has
   no IND model, so do not tick it).
4. **Levels and rooms...** (the levels tree and Arrange rooms in one
   window since 2026-10-19): the counts against each level, open one,
   show its rooms, untick a room, pick several (Ctrl/Shift), **Show in
   plan** - the window stays open beside Revit.
5. **Export tab**: "A new workbook, from the office template" - export,
   then show the workbook in Excel for a few seconds (office layout, one
   blue row per level, room names/numbers/areas filled, formulas kept).
6. **Import** that workbook back (card 2) - the levels it matches.
7. **Assign tab**: the fresh / extract system-type ticks and the endcap
   reach - what each is for.
8. **Run** - the status line, then a terminal's Flow in Properties, and
   `%AppData%\ATKINSMEC\ATKINSMEC_debug_log.txt` briefly (the Desktop
   until 2026-10-16).
9. Close and reopen AUTOVENT - "Set up as this model was last left".
10. End card.

**Around the recording, Claude can prepare without the MCP server:**
title and end cards (PNG, 1920x1080), a caption file (.srt) timed to the
shots for Camtasia to import, and music composed from scratch as for the
old tutorial (`tests/video/music.py` - no licence needed). Editing in
Camtasia's timeline by clicking is slow and fiddly; the user trims
pauses, or Claude does it if asked.

## SHEET QA: Tags checker - 2026-11-10

User: "lets make another window in the QAcheck addin called tags
checker, where it checks for annotations or tags clashing or overlapping
each other in the model". Read as an eighth TAB of SHEET QA (the window
is tabs; Room tags went the same way the same day), after Room tags, a
check page like Flowrate: Every sheet / ticked sheets / the view open
now; three checks - tags over tags (room, space, area tags included),
tags over text notes, text over text; Show selects both, Mark colours
both, Export; the overlap tolerance (0.5 mm on paper) beside Run. Built
on PR #2's branch (Room tags), not on main.

- How a tag is measured (`QATAGS`): Revit's box round it with its
  LEADER OFF - set in a transaction that is rolled back, never
  committed - and turned back to the view where the view is turned.
  Text notes from their own corner / width / height. The head's own box
  (`GetTagBoxMiddlePoints`) is documented for 2025+ but not public.
- Tests: `tagclashtest`, 40 checks; 8 mutations of QATAGCLASH all
  caught. Built here in the cloud for 2022-2027 (apt's dotnet SDKs,
  `EnableWindowsTargeting`) - the first cloud build; PR #2 built clean
  with it too.
- **NEVER RUN IN REVIT, and the page has not been seen on screen** (it
  is laid out in code from the Flowrate page's controls). To check on a
  real job: the tab looks like Flowrate's at 100% and 150%; a plan with
  tags known to overlap (with and without leaders, room tags with a
  leader outside the room) - the rows, the depth, Show framing both;
  that the undo list is empty after a run and a workshared model owned
  by someone else raises nothing; a turned view (rough rows near 45
  degrees); how long Every sheet takes on a big job
  (`ATKINSMEC_tags_log.txt` lists every view). Revit's box round a tag
  may be looser than its text - if pairs that only touch are listed,
  raise the tolerance and say so.
- Not done, offered: a fix that moves one tag of a pair clear; leaders
  crossing other tags; dimensions.

## ROOM TAGS becomes a tab of SHEET QA - 2026-11-10

User: "lets add the room tags addin as a new tab in the QAcheck addin
instead of being a seperate addin". The ROOM TAGS button (`Tool11`) and
`TAGCommand.cs` are gone; SHEET QA has a seventh tab, **Room tags**, the
same window embedded (`TAGForm.Embed`), its reads and writes through
`QAEVENT.RoomTagWork`. One `.addin` for the suite, so nothing else to
unregister; the `roomtags` icon is left in `Resources\Icons` unused.
`roomtagtest` drives `TAGForm` alone, unchanged. Written in the cloud
(no dotnet there): **build 2022 / 2025 / 2027 and run roomtagtest on the
PC before merging. Never run in Revit** - check on a real job that Check
sheets, Tag missing and Remove duplicates come back through the external
event, and that Esc / Enter in the tab do not close SHEET QA.

## AUTOVENT: Levels and rooms - headings when scrolled, drop-down cells, the calculation only - 2026-10-28

User, in Revit: "many columns are empty without headers"; "the occupancy
is not functioning properly its not a droplist like excel".

- Headings: past the window's width they were clipped (WPF clips to the
  slot before the RenderTransform moves them) - on a Canvas now.
  Reproduced in ventpreview (`autovent-arrange-right.png`, scrolled to
  the far right) before and after.
- Calculation columns: KIFAF 7.xlsx gave 23, half of them typed or empty
  on every row - now only those a formula works out with something in it
  on a room's row (`VENTWHATIF.ColumnsFor`): 12. Labels by the column's
  own heading ("Total Outside Air based on Actual Occupancy", not "Rp x N1
  + Ra x A ..."), over two lines. On KIFAF a change recalculates in a few
  ms (Office Space, 12 people: 40 L/s; 20: 60).
- Occupancy cells: one click opens the list (▾ in the cell) or the
  number box, keeping the picks; a double-click's first press had dropped
  them, so "several picked rooms at once" never worked with the mouse
  (the test had called BeginEdit directly).
- Tests: arrangeflowtest (click opens, picks kept, a row not picked alone,
  second press, arrow, headings scrolled), officetest (`ColumnsFor`,
  labels). 8 mutations, all caught.
- NOT run in Revit.

## AUTOVENT: occupancy chosen in Levels and rooms, new values, "Redundant rooms" - 2026-10-26

User: call the containers "Redundant rooms"; choose a room's occupancy in
Levels and rooms as in the sheet, show the new airflows in "New values"
(Fresh air, Exhaust air) with the sheet's other calculation columns; the
sheet's values read only; Write on Sheet writes the sheet's, on Model the
new values; a double-click on a Model airflow changes the model. Asked:
the Model double-click writes at once; categories, use, occupants, units
editable; "from rules" fills blanks only; the SHEET'S OWN FORMULAS
calculate (`VENTWHATIF`, a copy in memory, never saved).

- Done and tested: officetest 601 (`WhatIfTests`: never-opened and
  Excel-saved workbooks, 10 people / 2 units / light use / a category
  against a full recalculation, blank, #N/A, revert, a merged group's
  total, the broken Dwelling Unit mended, the file untouched),
  ventsettingstest (`roomoccupancy` lines), arrangeflowtest (the window:
  blocks, cells, Choose, FromRules, lists in cells, several picked, Model
  writes new values, WriteGroup). 8 mutations, all caught.
- NOT run in Revit. To check there: a real workbook's Calculation
  columns (labels from its headings), how long a choice takes to
  recalculate on a big sheet (one row + merge tops), the Model double-click
  write, and that the window is usable on a laptop (it scrolls sideways).
- Open: the export does not use the choices (the user said the sheet is
  not to be written) - ask if they want an export to carry them.
- On Model a sheet merge cut by FCU groups puts its whole total on its
  first room's group (lower rows' cells are empty) - as the Sheet columns
  already did.

## SPRINKLERS: a one-head room's head in the middle of its main part - 2026-10-25

User (plan, a store's head off-centre): "how to deal with sprinklers that
need to be centered like this"; asked whether SPRINKLERS placed it (the
heads in the screenshot line up across rooms, which the add-in never
does) - "SPRINKLERS placed it". A one-cell room's grid point was the
centre of the whole outline; a nook or recess pulls it off the store's
middle. `SPRINKLERLAYOUT.MainPart` = middle of the largest rectangle
inside the outline. geomtest: plain store (exact centre), store + nook
(middle of the store, the nook its own head), door recess (one head, the
store's middle), column (unchanged - checked with the change off: same 2
heads), turned 30 degrees. Without the change the nook and recess checks
fail. **Never run in Revit** - the real room's outline is assumed, not
seen: if a head is still off, the room's boundary is something else.

## AUTOVENT: Levels and rooms - Differences only, the sheet's columns, Columns... - 2026-10-25

User asked what to improve, "should we implement the excel sheet columns
and occupancy into it?"; recommended read-only (the workbook stays the
calculator - an editable category would mean a second ASHRAE calculator
or a hidden recalculation), and four things; "yes build all four":
1. **Differences only** - rows needing attention (twice, airflows apart,
   terminal on no FCU, no sheet row, area changed); tooltip says why.
2. Sheet | Model under **"Merge and write as"**.
3. **The sheet's columns, read only** - categories (blank = grey
   suggestion), occupants, units, sheet area; the model's area amber when
   it differs. VENTSHEET reads them (officetest: round trip of a real
   export, blanks NaN).
4. **Columns...** - VENTCOLUMNS, per person, remembered.
arrangeflowtest 20 new checks, officetest 4; 9 mutations caught. Rendered
1x, 1.5x, Differences only. Not yet seen in Revit.

## AUTOVENT: Levels and rooms looks like a sheet - 2026-10-25

User: "can you make the rooms and level window look more like excel
view". Cells with gridlines, a grey gutter (drag handle / level arrow),
grey two-row headings over EVERY column (Number, Name, Notes, Room m² m,
Sheet / Model Fresh Extract), level group rows, compact rows; the notes
moved out of the name into a Notes column. Headings aligned to the cells
exactly (2 px in: border + the ListBox template's own 1). arrangeflowtest:
helpers find parts by logical tree; 3 new checks (notes column, every cell
gridded, headings over cells); 6 mutations caught. Rendered 1x, 1.5x,
short, search. Not yet seen: a real job's long names at 1080p 150%.

## AUTOVENT: the return-duct endcap rule gets the reach back - 2026-10-25

User (MCC ROOM BP01.B1.179): "this room fresh air is showing 3660 in the
levels and rooms window, why". The Model column: three endcaps on its two
outside FCUs - 3946614 at 0.23 m from 3407825's return duct (its own),
4127553 at 2.94 m from 3408412's, 4129748 at 8.48 m from 3407825's and
9.5 m from any unit. The no-limit rule of 2026-10-24 took them. Every
return-duct match in the log was 0-0.5 m but 1.16, 2.94 and 8.48 m; the
EMCC ones 0.48 and 0.50 m. So `VENTFACING.Endcap` now needs the return
duct within the endcap reach (1500 mm) as well as no further than any
unit. ventfcutest: MCC cases added; mutations caught (one survivor - a
return duct beating a nearer unit - got its test).

## AUTOVENT: room area and height in Levels and rooms; "not enclosed" - 2026-10-24

User: "show a column for the room area and height in the levels and
rooms window, is there a solution for the "not enclosed" rooms ?". Two
columns under "Room" (m², m) left of Sheet / Model, shown with no sheet
too; the export's height rule (false ceiling, else the room's own) moved
to `RVTLINKSELECTION` so the window and the sheet agree; tooltip says
which. arrangeflowtest: 8 new checks, 5 mutations caught; rendered 1x and
1.5x (ventpreview gives its rooms areas). Not enclosed rooms: Area 0 in
the architect's model, so `ReadRooms` (Area > 0) never sees them and no
terminal can be placed in one (`IsPointInRoom` has no volume) - the fix
is the architect's (close the boundary, or a link of theirs set Room
Bounding). Then "yes add the list of not enclosed rooms": Levels and
rooms' **Not enclosed (N)...** button, a list (Model, Level, Number,
Name, Why - not enclosed / redundant inside X) and Copy (tab-separated).
`VENTUNENCLOSED` (Revit-free), `RVTLINKSELECTION.ReadUnenclosed` (Area 0
with a location; GetRoomAtPoint for redundant), read with the rooms.
arrangeflowtest: 9 new checks, 7 mutations caught - the first render
showed only Why (fields do not bind), now tested. **Never run in Revit**:
GetRoomAtPoint on a link document, 1 ft above the room's point.

## AUTOVENT: a store's fresh air bellmouth given to a unit supplying nothing - 2026-10-24

User (YANM3, STORE BP01.B1.019): "this store fresh air was not
detected by the addin". Survey log: bellmouth 3711058 "feeds unit
2204575 at 1488 mm - the room it stands in needs none of this air", and
"FCU 2204575 supply system reaches no room in scope". Two faults: the
workbook loaded is KIFAF's (132 rows, LEVEL 04A; "Rows found: 0 of 138")
so the store had NO row, read as "needs none"; and nearness alone handed
the air to a unit passing it to no room. `VENTFACING.Bellmouth` now takes
`roomInSheet` (no row: not "needs none"; said `NotInSheet`). Then STORE
BP01.B1.076 ("the addin didnt read this store fresh air"): bellmouth
2673018 FACES unit 2032085 at 1220 mm, which also supplies no room in
scope - so a unit whose supply system reaches no room in scope is no
candidate at all (`VENTFACING.CanFeed`, beside "not on its own system"),
faced, touched or near; the log names it ("supplies no room in scope -
not fed"). ventfcutest; mutations caught. Tell the user: the workbook in
YANM3 is another job's - nothing can be written until a YANM3 sheet is
loaded.

## AUTOVENT: a fresh air endcap by an outside FCU's return duct - 2026-10-24

User (YANM3 plan, EMCC ROOM): "this example of EMCC room is not detected
by the addin, instead of using endcap reach tolerance, we can detect the
neareast freshair endcap to the return air duct connected to the FCU
serving a room located outside of it". The survey log: endcap 3786944
3473 mm from FCU 3626185 (EMCC BP01.B1.083), 3831390 3406 mm from
3648379 (EMCC BP01.B1.077) - rejected by the 1500 mm reach, ~20 like
them. Now: units outside every room they supply (by supply terminals)
have their return ductwork read; a fresh endcap no further from it than
from any unit binds to that unit with no reach (VENTFACING.Endcap /
Outside, tested in ventfcutest, 7 mutations caught). The supply-duct
trace moved ahead of the bellmouths so "outside" can use it. **Never run
in Revit**: on the next Levels and rooms opening, the survey log should
list `OUTSIDE FCU` lines (the EMCC units, with return ductwork pieces)
and the EMCC endcaps "nearer the return duct of FCU ...". Watch for an
endcap taken by the wrong unit's return duct - the log names the piece.

## AUTOVENT: linked rooms below every level - the open model measured on its ducts - 2026-10-24

User (Revit 2024, YANM3): "autovent doesnt detect the rooms from the
linked model of the currently opened model". The survey log had it:
"1 room(s) on selected levels ... 137 below this model's lowest level".
The architect's model measured its own levels 3.9 m out (corrected); the
open MEP model has no rooms or spaces, so its error counted as 0 and its
levels stayed 3.9 m high - its raw level heights equal the architect's
raw ones to the mm, level by level, so both share the same 3.9 m. Fix:
`RVTLINKSELECTION.LevelError` measures a model with no rooms/spaces on
its ducts, pipes, cable trays and conduits (start point Z less
`InternalZ(ReferenceLevel)` + `RBS_START_OFFSET_PARAM`), up to 60, median.
Every host-level user goes through it (AUTOVENT storeys, spans,
SPRINKLERS). The debug / survey log now says "Out by X, measured on N of
its ducts and pipes"; the apartments log too. **Never run in Revit** -
check the survey log's new line on the next Levels and rooms opening: it
should say about -3.900 m, and the rooms should be on their levels.

## ROOM TAGS: Remove duplicates - 2026-10-24

User: "add option in the addin to remove duplicate tags". A Duplicates
column, a Remove duplicates button and an Any tag type box (off: a name
tag and an area tag of one room are both kept). `TAGPLAN.Doubles`: per
owner view and room (and type), keep as few tags as still leave every
plan on a sheet that showed one showing one - by where each tag's head
is against each plan's crop (`SeenIn`, read in `TAGREAD.Seen`) - oldest
first on a tie; pinned and grouped tags stay; a tag in no sheet plan is
left alone. `TAGWRITE.Remove`: one transaction, only `RoomTag`s, counted
from the model. Tests: roomtagtest 104 checks; 12 mutations, 11 caught -
the survivor (owner view out of the duplicate key) is covered by
`SeenIn`, which only ever names plans of the tag's own owner. Rendered at
1x and 1.5x; all 24 harnesses pass; installer rebuilt. **Never run in
Revit.** To check first: a tag with a leader - its HEAD decides which
plans show it.

## ROOM TAGS: a new add-in, missing room tags on sheets - 2026-10-23

User: "make a new addin to check the missing room tags in sheets and
annotate them for selected links with search bar for links and make them
annotate without duplication". Eleventh button, ROOM TAGS (`Tool11`,
`RoomTags\`, icon `roomtags` - a room and an orange tag). Floor and
ceiling plans on sheets; the rooms of the ticked links each shows (2023+
the view's own linked-element collector, 2022 the level band), inside
the crop; tagged = a tag of that placement's room in the plan's owner
view (a dependent's = its primary's); Tag missing places one tag per room
per owner view, in the primary, in one transaction, then reads again.
Tests: `roomtagtest` (77 checks: the rules, settings, the real window
off-screen), 14 mutations caught; rendered at 1x and 1.5x. Built 2022 -
2027 and the AUTOVENT edition (RoomTags excluded). **Never run in
Revit.** Open, to check on the first real job: does the 2023+ collector
return linked rooms in MEP plans (if the Rooms category is off in the
view it returns none - the note says so); is the tag where Revit's own
"Tag all" would put it (the room's point, host XY); a ceiling plan
wanted at all, or only floor plans.

## AUTOVENT: a merge is one side's - FCU and Merge button fresh air only - 2026-10-22

User (TA-04-04 screenshots): "the kitchen exhaust is serving only a
kitchen with a slot diffuser attached to the ceiling but the addin
showing the exhaust serving the kitchen and the foyer and the
living/dining". Cause: a merge was one total for BOTH sides since
2026-10-12, so the FCU's (fresh air) merge of foyer + kitchen + living
put the kitchen slot's 65 l/s on the group. Asked; chose FRESH AIR ONLY
for an FCU merge and for the Merge button; extract merged only where
the sheet merges extract cells. Done everywhere merges are used:
`VENTARRANGE` (Merges = fresh, new ExtractMerges, Bound/Together,
Unmerge both, Merge(extract)), `VENTSAVED` (`xmerge`), `VENTFLOWS`
(SheetMerges/GroupsOf by side, Adopt into each side), the window (two
groupings, cells ↑ per side, notes "fresh air merged with" / "extract
merged with", write per side, Model = no extract merges), the run
(`freshOf` / `extractOf`, two tallies, every step per side), the export
(`Room.ExtractMerge`, MergeAir per column). Tests: officetest 567,
arrangeflowtest, ventleveltest, ventsettingstest; 7 mutations caught;
all 23 pass. Rendered. Installer rebuilt and sent. **The run's per-side
split has never run in Revit** - it is the airflow write itself: check
the debug log's "Merged groups, fresh air / extract" lines on the first
run. Open: a legacy save's `merge` lines (both sides before) now read as
fresh air only; extract merges come back when the window derives the
sheet's on opening.

## AUTOVENT: each diffuser traced back along its ducts to its FCU - 2026-10-21

User: the powder room is right now, "but in the dressing room in
TA-04-10 is not properly merged with the master bedroom ... despite
having diffuser connected to it with the FCU serving the master bedroom".
The survey log (01:20) showed: FCU 3461206 (the bedroom's) - its ducts AND
its Revit system reach the master bedroom only; the dressing 121.065
holds one supply terminal, counted in the dressing, on no unit's system.
And a regression from the first walk: FCU 3678148 (TA-04-04, user-
confirmed foyer + kitchen + living) walked to the foyer only, its system
naming the kitchen and living/dining - so walking OUT from connectors
classed SupplyAir misses ducts on others. Now each supply terminal is
traced BACK to whatever unit its ducts reach (`TraceDucts`, `IsSupply`),
any connector; a terminal reaching none is marked "terminal on no FCU" in
the window (id on hover) and logged with where its ducts end open. If the
dressing still shows that, its flex duct / diffuser is not connected in
Revit - the log names the element to look at. arrangeflowtest note
check; all 23 pass; installer rebuilt and sent. **Never run in Revit** -
read `ATKINSMEC_survey_log.txt` ("Supply terminals whose ducts reach no
unit", FCU 3461206, FCU 3678148) after the user opens the window.

## AUTOVENT: an FCU's rooms by its supply ducts, not its Revit system - 2026-10-21

User (screenshot, TA-04-10 1BDR-B on Model): the powder room 121.069 shown
merged with the kitchen 121.067 and living/dining 121.063, "but its
actually not supplied from the FCU that serves the kitchen and the
living/dining". Revit 2024 now has the 10-20 23:25 build (hash-checked).
The last RUN's log (23:14, older build) had FCU 3702622's served rooms
as the living/dining and kitchen only - the powder room one extract
terminal - but the window's survey wrote no log, so what it saw is
unknown. Two candidates: the FCU's Revit SYSTEM (what "served" meant)
holds a terminal counted in the powder room; or the foyer 121.068 - NOT a
room the add-in reads (1BDR-B's interior model gives 7 rooms, no foyer;
probably not enclosed) - has the FCU's grille, and the "room in front of
its face" rule puts it in the powder room. Changed: the window's FCU
merges now take the rooms the unit's supply DUCTS reach
(`DuctedRoomKeys`), the system only where the ducts reach no terminal;
the survey's log is kept (`ATKINSMEC_survey_log.txt`) with every FCU
whose ducts and system disagree, terminal ids included; a merge's hover
says where it came from (FCU id / the sheet / by hand). The run's own
endcap trace is unchanged. arrangeflowtest + hover checks; all 23 pass.
**The duct walk has never run in Revit** - after the user installs,
read `%AppData%\ATKINSMEC\ATKINSMEC_survey_log.txt` (search 3702622 and
121.069): if the powder room is still there, it is the foyer grille and
the face rule, and the fix is in which room a terminal is counted in.
Installer rebuilt and sent; Revit 2024 must be closed to install.

## AUTOVENT: the merges shown are always the button's, from opening - 2026-10-20 (late)

User again: "when pressing on the model button, it should be showing the
spaces from the model that are merged, when pressing on the sheet button
it should be showing the spaces from the sheet that are merged".
**Found: the user works in Revit 2024, whose add-in was still the
2026-10-19 13:55 build** (before both merge fixes) - Revit 2024 was open
through every build and the installer, so nothing could replace the DLL.
Their KIFAF settings hold 13 merge groups from that build, no
`mergeview` line. Also changed: on opening, the window works out the
merges of the button that is down (`Derive`; the sheet on Sheet, the
terminals on Model once read) - saved merges, hand merges included, no
longer show until a press. `AdoptOnce` and the "taken from the sheet"
count gone. arrangeflowtest: 4 mutations caught; all 23 pass. Installer
rebuilt 23:26 and sent - **the user must close Revit 2024 before running
it.** Never run in Revit.

## AUTOVENT: the sheet's air read only; Sheet pressed = as per sheet; installer - 2026-10-20

User: "the flowrate from the sheet should be read only, also the same
merge issue still exists, when pressing on sheet the spaces from model
showing merged". Set airflow... (and its double-click, `_set`) removed;
nothing picked writes nothing, either view. The merge issue: merges
saved as the window was left on Model before the ByModel flag existed
are FCU groups with nothing saying so - the window opened on Sheet with
them, and pressing Sheet with Sheet down did nothing. Now EVERY press
works its merges out afresh - Sheet from the sheet (`FromSheet`), Model
from the terminals - and a hand merge lasts until the next press. The
kept-Sheet-merges of the day before (`SheetKept`, `sheetkept` /
`sheetmerge` lines) removed as unused; `mergeview` kept. (The user had
likely been testing in Revit 2024, which still had the build without
the 10-19 fix - Revit 2024 was open at that build.) arrangeflowtest 3
mutations caught; all 23 harnesses pass. **Installer rebuilt**
(`installer\output\AtkinsMEPToolkit-Setup.exe` + `.zip`, 21.5 MB, all six
versions, payload checked against bin\Release) and sent to the user.
Not deployed by build-all this time (Revit open); the installer is the
way it reaches Revit. Never run in Revit.

## AUTOVENT: Sheet and Model each show their own merges - 2026-10-19

User: "when pressing sheet it should show the merged rooms according to
sheet and when pressing model it should show merged rooms according to
model". Two faults: Model was the FCU groups PLUS every Sheet merge no
FCU touched (`ByFcu(now, ...)`), and one merges list served both views,
so a window closed on Model reopened on Sheet showing the FCUs' merges.
Now Model = the FCUs' groups only, worked out afresh on every press;
Sheet's merges wait in `Arrangement.SheetKept` while on Model; the view
(`ByModel`) and the kept merges are saved (`mergeview` / `sheetkept` /
`sheetmerge` lines - an older add-in skips them), and the window reopens
on the view it was left on. Unchanged: the view showing decides the
export and the airflows (the user's choice earlier the same day).
arrangeflowtest 80 (5 mutations caught), ventsettingstest round trip
(mutation caught); all 23 pass. Rendered. Deployed to 2022, 2023, 2025,
2026, 2027, hash-checked; **2024 not** - Revit 2024 was open (it has the
combined window without this fix). Never run in Revit.

## AUTOVENT: "Levels to process" and Arrange rooms in one window - 2026-10-19

User: "i think its better to combine 'levels to process' and arrange
rooms window". Asked where; chose **its own window** (over the main
window's card). `VENTARRANGEDIALOG` became `VENTROOMSWINDOW` (git mv):
every level, opening to its rooms and containers in sheet order, a tick
on every row (the main window's `LevelRow` / `RoomRow`, bound), the
airflow columns, All / None / Show in plan beside the arrange buttons.
**Modeless; every change at once, saved by the main window - no OK /
Cancel; Reset level asks first.** Write to terminals leaves it open;
the terminals are read again inline after the write (request 4), and
when the ticked links change. Model + nothing picked now writes nothing
(was: the one level shown - there is no "one level" any more; pick the
level's heading). Double-click a room = Show in plan (it was Rename in
Arrange rooms; F2 renames). Card 3 of the main window: a count, the
sheet-level warning, the button; the main window now sizes to its
content (`VENTFIT.Screen`, `SizeToContent`) - the tree's fixed body
height went with the tree. Removed: the tree's own pick rules
(`VENTLEVELROWS.Picks`, `IsPicked`) and their checks; `Row.Exported`.
On-screen / report wording: "Levels and rooms" for "Arrange rooms" and
"the levels list" (the write's report, the export report, the logs).
arrangeflowtest 72 (ticks, picks, search, closed level, Show in plan,
live changes added; 12 mutations all caught), levelrowtest (picks out,
the search's words in, mutation-checked); all 23 harnesses pass.
Rendered at 1x and 1.5x and looked at (window, search, short; the main
window full and at 690). Built and deployed to all six, hash-checked
(Revit closed). **Never run in Revit** - the modeless window, Show in
plan from it, the inline re-read after a write.

## CHW VOLUME: links as a tree, nested links, a search - 2026-10-19

User: "the CHW volume addin should list the linked instances of main
model below it and the linked instances (nested links) of linked models
and add a search bar for the models". The list is now the open model,
each link placement indented below it, each nested link below the
placement it sits in (`CHWTREE.Order`). Revit gives a nested link's
parent as a link TYPE, so when the parent model is placed twice the
placement is picked by nearest origin (`CHWTREE.Nearest`). Until now
the list was flat, one row per model (deduplicated by title); now it is
one per placement, so a
model placed twice (Tower A / Tower B) would be counted twice. A repeat
is therefore offered **unticked** (grey) and the totals match the old
ones until someone ticks one in. A model is measured once per run
(`Again` reuses it).
A search box (top right of the list) hides rows only, keeping every
row above a match. Tick all / Untick all act on what shows.
chwtreetest 15 checks, mutation-checked; all 23 harnesses pass.
Built for all six. Deployed and hash-checked for 2022, 2023, 2025,
2026, 2027; 2024 later the same day, with Levels and rooms (Revit 2024 had been open). **Never run in
Revit**: nested links, the nearest-origin parent and the repeats need a
real model with nested links. anchorsim's btnCalculate/grpResults
overlap is on HEAD too, not from this change.

## AUTOVENT: interior models named "ID" too; no red for placed-again rooms - 2026-10-19

User: "can the container also detect models with 'ID' instead of 'IND'"
- the keyword box takes several keywords, commas between, any one will
do, each a word of the label (`VENTINTERIOR.Named`); default "IND, ID".
No model in the settings file had a keyword of its own, so all follow
it. Then, mid-change: "also do not the highlight the rooms of repeated
link instances" - red now needs a DIFFERENT room id on the number.
ventsettingstest +7, arrangeflowtest 53; mutation-checked. Deployed to
all six (Revit closed), hash-checked.

## AUTOVENT: the switch decides what is written - 2026-10-19

User: "the switch between sheet and model buttons should decide which
flowrates will be assigned to the air terminals". Asked: Model writes the
sheet's values grouped by FCU, or the Model column values - chose the
Model column values. Model now writes each group's terminal total (the
FCU grouping, sheet merges left out of the run's grouping too); Sheet
writes the sheet's as before. arrangeflowtest 52 (+5), mutation 5 of 5.
Deployed to all but **2024 (open)**. Never run in Revit.

## AUTOVENT: Arrange rooms - Sheet | Model switch, row colours - 2026-10-19

User: "instead of merge by fcu button, we can have two buttons that switch
between 'model' and 'sheet', and keep the columns of flowrates of model
under header called 'model' and ... sheet under ... 'sheet'". Asked what
the switch changes: "both option 1 and 2" (which merges apply AND which
values are written), "also rows with flowrate differences between sheet
and model should be highlighted in yellow, rooms which have same room
numbers (duplicated room numbers) should be highlighted in red".

Done as in CLAUDE.md (Arrange rooms). Duplicates are per LEVEL (a sheet
row is matched by name + number + level). On KIFAF expect much red: both
2BDR-A2s on a level come from one interior placement and repeat every
room number. arrangeflowtest 47 checks; mutation 7 of 8 - the survivor a
redundant guard (FCU notes on Model), removed. Rendered both views.

## AUTOVENT: terminals put in the room one floor DOWN - 2026-10-19

User: "the autovent still doesnt detect the foyer in TA-04-04 as merged".
The debug log: FCU 3678148's endcap served 3589274:1826168 (LIVING/DINING,
IND-001220 TIER 260, LEVEL 04A) and 3589212:1826169 / :1826170 - KITCHEN
and FOYER of TIER 35, the same interior model on LEVEL 03, same plan
origin. `RoomAt` kept the room with the highest `BaseZ` - in each LINK's
coordinates, so every floor's placement tied, and the first read (LEVEL
03) won. So Merge by FCU saw two of the FCU's rooms on LEVEL 03 and
suggested nothing for TA-04-04 - and every run with stacked placements
in scope put terminals in the floor below (unseen: the sheet's values are
the same floor to floor; endcap shares and "2 FCU(s)" were not).

Fixed: rooms compared by `HostFloor` (HostElevationOf); links placed by
their TOTAL transform (were GetTransform - nested placements). Built and
deployed to all six (Revit closed), hash-checked. **Never run in Revit**
- reopen Arrange rooms on LEVEL 04A: TA-04-04's three rooms should say
"one FCU with ...".

## AUTOVENT: Merge by FCU - 2026-10-19

User (screenshot of 2BDR-A2 TA-04-04, LEVEL 04A): "the FCU is serving the
foyer, the kitchen and the living/dinning, but it doesnt detect the foyer
as merged, why?" - AUTOVENT never inferred merges: only the sheet's
merged cells and Arrange rooms' Merge. KIFAF 7 had AA43:AA44 / AM43:AM44
(LIVING/DINING 121.125 + KITCHEN 121.126, the arrangement's one merge);
FOYER 121.127 row 45 in neither. The debug log showed FCU 3678148
supplying all three - and a SECOND "121.126 KITCHEN" (the mirror
apartment's: both 2BDR-A2s on a level come from one interior placement,
and the FCU's Revit system reaches it). "yes add the FCU merge detection".

`VENTFLOWS.FcuGroups` (rules in CLAUDE.md), the survey's `Units`, the
Arrange window's notes and **Merge by FCU** button - suggest, never
automatic. arrangeflowtest 37 checks (+14); mutation: 8 of 10 caught at
first - the two survivors were a tie check and a "two or more" check the
majority rule already implies, both removed, and a 2-1-1 case added.
**Never run in Revit**: whether KIFAF's TA-04-04 FCU is suggested depends
on its fresh endcap being bound to it (the debug log's FCU trace).

## AUTOVENT: airflows in Arrange rooms; Containers and Units - 2026-10-19

User: "allow the addin the option to read and write the airflows of the
rooms in 'arrange rooms' window, it should show the flowrates in a form
of columns inside the window and detect which rooms that are merged".
Asked: read the sheet AND the terminals side by side (chosen); write to
the terminals (chosen); sheet merges shown and adopted (chosen).

- `VENTFLOWS` (Revit-free): sheet merges, adoption, an endcap's air back
  to its groups (`Attribute`, weighted by the run's `Share` - a run's own
  values read back as the sheet's), group totals, `Overrides`.
- `REVITROOMS` gained `Mode.Survey` (read, via `VENTREAD.Ask.Terminals`,
  before the dialog opens) and `Mode.Write` (request 4: the picked groups
  only). The run itself is unchanged: every new branch is guarded by the
  mode. Survey and write use the window's own document; the run still
  `Command.Doc`.
- The dialog: four columns, Set airflow... (several rooms at once; blank
  keeps; Back to the sheet), Write to terminals. Columns are text, not
  boxes - boxes in rows fought the drag and the double-click rename.
- `arrangeflowtest` 23 checks (new, in run-all), mutation-checked 9 of 9
  that applied. Rendered at 1x and 1.5x.
- **Never run in Revit**: the survey's numbers and the targeted write -
  check a write in the debug log (`FROM ARRANGE ROOMS`, `WRITE ...`).
  Edits are not kept: set, then write, in one sitting.

Same day, user: rename "apartment" for offices too - "Container" for the
architect's room, "Units" for the rooms inside, on screen and in the logs
(67 strings, `rename_containers.py` in the scratchpad - gone with it).
Settings keys, the rules file's `apartment` scope, code names and the
apartments log's file name are unchanged. The dwelling behaviour of a
container's rooms is unchanged (a per-link Residential tick was offered).

## AUTOVENT: "equals" occupancy rules - 2026-10-19

User: "if we do a rule 'equals to' should it be higher than 'contains'?"
(answered yes: any equals beats every contains; between equals, scoped
before any), then "do it". `Rule.Exact`; `Fits` = same tokens, same order;
`RuleFor` ranks it with `ExactFirst`; the rules file's 8th field
`equals` (the old override's slot - a number reads as contains);
`WithBuiltIn` never gives built-in values to an equals rule; the dialog
has a Match column (contains / equals) and the window is 90 wider
(1234, min 1024 - fits a 1280-wide work area). officetest 565 (+10),
mutation-checked 7 of 7. No built-in rule is "equals".

## AUTOVENT: the apartment kitchen per unit, the override undone - 2026-10-19

User: "undo the override in the autovent, we need to use alternative
method for the kitchen exhaust", then, asked how many air changes, "check
this formula and the excel sheet why is it showing zero for the
kitchens", then "adjust the excel sheet so it can calculate residential
kitchen apartment same as toilet private".

Why 0: Table 6-4's "Residential kitchens- Apartments" row has no rate in
any column (62.1 leaves dwellings to 62.2, note G), and the per-unit
exhaust formula (AF) names only five categories - not it - so AG's
per-area lookup of an empty cell gave 0 x area.

- `VENTOCCUPANCY.KitchenCheck` (export, worksheet added, update): the
  empty row gets 50/100 cfm, 25/50 L/s a unit (62.1's residential kitchen
  rates, as "Other than Residences" has them); every OR(...) naming
  "Toilets - private" in the room row's formulas names the kitchen too.
  The kitchen rule gives 1 unit: 50 L/s, 25 for light use.
- The override is gone: `Rule.Override`, the dialog column, AK/AL writing,
  the 8th file field (no longer read; checked it is not read as units).
- KIFAF 5 (a copy) through the real KitchenCheck + 1 unit: 9 kitchens at
  50 L/s, the merged LIVING/DINING + KITCHEN pair 50; with the user's
  KITCHEN rule's own "Light Use" (typed 2026-10-19 01:54) 25 L/s each.
  **The user's rule says Light Use - so an export now gives 25, not 50.**
- officetest 555 (+12 over the override's), mutation-checked 7 of 7.
  The "worksheet added" test now allows exactly one change to the
  workbook's own sheets: Table 6-4's empty kitchen row given its rates.

The ACH "alternate approach" (AI/AJ) was offered and not taken.

## AUTOVENT: saved occupancy rules get later built-in values - 2026-10-19

KIFAF 5's ten apartment kitchens had 0 L/s exhaust: category
"Residential kitchens- Apartments" (no rate in Table 6-4) and no
override. The user's `autovent_occupancy.txt` was the built-in list from
an older build - same 86 rules, but no kitchen/pantry 50 L/s, no units
on the apartment toilets, no light use on other SHOWER - and a saved
file replaces the built-in rules whole. Most likely saved from Revit
2024, which ran an older build until 10-18 (open during deploys).

`VENTOCCUPANCY.WithBuiltIn`, applied in `Load`: a saved rule matching a
built-in one (scope, words by tokens, both categories) takes each BLANK
Use / Units / Override from it; typed values stand; a rule with a changed
category or scope, or one the built-in list has not, is left alone.
officetest 544 (+10), mutation-checked 8 of 8 (scope was the survivor at
first - test added). The user's real file read through it: kitchen and
pantry 50 L/s, apartment toilets 1 unit, other SHOWER light + 1; their
own Light Use on KITCHEN kept; the file not written.

Same day, from the KIFAF sheets: bathrooms' No. of Units was 3 (WC +
toilet roll holder + brush holder - the WC name rule ticks any family
with "toilet" in it, and misses `WallHungWc` / `AdaFloorMountedWC`), then
2 after the user unticked the holders, with two `Bath Set` families still
ticked by hand. **Open**: tighten `LooksLikeWcOrUrinal` (holder, brush,
roll, seat, cover...; a WC joined to other words); a per-room family
breakdown in the export log would have settled the "2" without guessing.

## AUTOVENT Fixtures dialog: models, several picked, a search - 2026-10-17

User: "list the linked models themselves instead of listing all the
instances of the linked models, also allow multi selection for the
models and the fixtures, and add a search bar for the fixtures".

- One row per model ("40 placements" beside it); a ticked model is
  every placement, those placed later included at export
  (`VENTFIXTURES.EveryPlacement`, explicit choices only). The default -
  the room links - is unchanged, and OK with the room links' models
  ticked still stores "follow the default". A choice saved by the old
  dialog (one placement) opens as its model ticked.
- Both lists: file-list picks; a box on a picked row ticks them all;
  Space. The families: a search box (hides rows, ticks stand).
- The "Read the fixture families" button is gone: the list re-reads from
  what the window already holds on every model tick, and keeps each
  family's tick. The long Table 6-4 hint was cut to one line.
- `fixturedialogtest` 56 checks (new, in run-all), mutation-checked: 6
  of 8 caught; the two survivors were both search guards, and together
  they still survive - WPF itself drops a row its filter hides from
  the selection - so the redundant one was removed.
- **Never run in Revit**: `EveryPlacement` (link type of each placement)
  is Revit-side and untested; the export log's fixture line says which
  models were read.

## Every log in %AppData%\ATKINSMEC, not on the Desktop - 2026-10-16

User: "save all the debug files being exported on desktop to that path"
(the folder holding `autovent_occupancy.txt`). `Shared\LOGS.cs`
(`Folder`, `PathOf` - creates the folder); all seven go through it:
`ATKINSMEC_` + `export`, `debug`, `apartments` (AUTOVENT), `equip`,
`esp`, `nwc`, `chw` + `_log.txt`. File names unchanged. On screen: the
airflow dialog's "Full details:" line, the EQUIPMENT CHANGER and NWC
help, the CHW fix's message - all print the new folder. Old files on the
Desktop are left alone. The new-workbook export still OFFERS the Desktop
as where to save the workbook - that is the user's file, not a log.
esppreview and nwcpreview compile `LOGS.cs` now. Deployed to all but
**2024 (Revit 2024 open)**; installers not rebuilt.

Same day: **FIRE LOBBY → Common Corridors** (was a blank rule). User:
FIREMAN LOBBY got no category although LOBBY has a rule - the blank
FIRE LOBBY rule was the more specific one and blank writes nothing. The
user's own saved rules already had it; the default now does too.
Installers rebuilt after it.

## AUTOVENT-Setup-2026-10-13.exe - the version from before the 14th - 2026-10-15

User: "rebuild the installer for autovent the older version 2 days ago".
Nothing changed on the 13th, so that is commit 92e3206 (12 Oct) plus the
rename made later that day. Built OUTSIDE the working tree: `git archive
92e3206` into the scratchpad, then the current copies of only the
packaging and rename-only files laid over it (ATKINSMEC.csproj,
Directory.Build.props, AUTOVENT.addin, Shared\UI.cs, AutoVent\ExtEventHan
/ REVITROOMS / VENTAPTLOG - those three differ by the rename alone -
installer\exe\*, build-installer.ps1), and `build-installer.ps1 -AutoVent`.
Result `installer\output\AUTOVENT-Setup-2026-10-13.exe`: "Fill the
Imported worksheet", no update, no occupancy, no model ID column; same
AddInId and file list as AUTOVENT-Setup.exe, so either replaces the other.

## AUTOVENT on its own: AUTOVENT-Setup.exe - 2026-10-15

User: "temporarily separate the autovent addin into separate installer
file". `build-installer.ps1 -AutoVent` → `installer\output\AUTOVENT-Setup.exe`
(19.9 MB, all six versions). Same source built with `-p:Edition=AutoVent`
(see CLAUDE.md): own assembly `ATKINSMEC.AutoVent.dll` (1.48 MB vs 2.27),
only AutoVent + Shared (QUIET.cs left out - it logs through NWC BATCH,
AUTOVENT never calls it), `AUTOVENT_ONLY` in UI.cs, own AddInId
677EC2E1-..., `AUTOVENT.addin` -> `AUTOVENT\ATKINSMEC.AutoVent.dll`.
The installer exe is the same project (`Edition.cs`): own name, title,
file list (AUTOVENT.files.txt) and settings files (autovent_*.txt only).
Checked: every packaged DLL = its build (both installers), the edition DLL
has no other tool's command, the suite build/deploy is unchanged (no
AUTOVENT.addin in it). **Never installed or run** - the .addin pointing
into a subfolder (dependencies loaded from there) is standard for Revit
add-ins but untried here on 2025+ (.NET 8/10). To undo the separation:
stop passing `-AutoVent`; nothing else depends on it.

## AUTOVENT: five fixes from the review list - 2026-10-15

User: "start with 1 ... use 50 l/s for the apartment kitchen, then 2,
then 3, then 4, then 5". Asked: the kitchen's 50 L/s as the apartments
category + override (chosen) or the other-than-residences category; the
occupants 2 in the master bedroom + 1 per other bedroom (chosen).

1. **Exhaust that came out 0** - rules carry nature of use, units and an
   override (dialog: three more columns). Apartment kitchen 50 L/s,
   residential toilets/showers light use + 1 unit. A public toilet with
   no fixtures counted is listed as 0 L/s in the export log. Found on the
   way: two "balancing" headings (Y and AL) - the first try wrote the 50
   into the OUTSIDE air's alternate column; the test caught it.
2. **Rooms sharing a name and number** - the airflow write matches rows
   by model ID first (`VENTSHEET.Resolve`), one row per room; ambiguous
   ones in the dialog. KIFAF's three pairs separate after one update
   (which writes the IDs).
3. **Bedrooms and occupants** per apartment (2 + 1 each). The W column's
   62.2 formula (header 0.15A + 3.5(Nb+1), formula MAX(T:U)) is the
   office's and was NOT changed - still open, for the office.
4. **No saved results** - calculated on reading.
5. **Bellmouth gap** - `UnitNeedsIt` before "its room's".

KIFAF copy (probe): 2BDR apartment M=2, master N=2, bedroom N=1,
ensuites 10 L/s, kitchen 50 L/s, no errors. officetest 533 (+13 this
round), ventfcutest +4; mutation-checked (10 + 2, one crash made a clean
fail). Deployed to all but **2024 (Revit 2024 open)**. **Never run in
Revit**: 2 and 5 depend on the model (IDs as the links have them; which
terminals stand where) - read `Rows found: ... by model ID` and
`BELLMOUTH ... its room has its own terminal` in the debug log.

## AUTOVENT: occupancy categories from room names - 2026-10-14

User: "make the addin automatically detect Occupancy Category and assign
it to the room for both fresh and exhaust". Asked and answered:
apartments → **Dwelling Unit** (with the fix below); the rules
**editable** (Occupancy... window, office-wide on this PC).

- `VENTOCCUPANCY`: rules, matching, the sheet's lists, `Fill` (blank
  cells only, list-checked, an update follows the sheet's own choices
  for a name first), `DwellingCheck`. `VENTOCCUPANCYDIALOG`: the rules
  grid, Add / Reset / remove, "Try a name". Export tab: "Occupancy
  categories from room names" + Occupancy... above the fixtures line;
  per-model switch saved. Done dialog adds "N occupancy categories filled
  from room names - check them."; the export log lists name -> categories.
- **Found on the way**: the office template's "Dwelling Unit" occupant
  test (`IF(O=$H$470,"NA",...)`) points at the wrong row in every pattern
  (Tower 2 $H$470 vs helper H427; one pointed at "Lecture Classroom"), and
  the user's KIFAF sheet carries the same ($H$288 vs H250). Choosing
  Dwelling Unit gave #VALUE!. Every export/update now points it at Table
  6-1's own cell. The old officetest check that the $H$ kept its distance
  (which preserved the bug) was replaced.
- Also: the update now shifts its row templates' references below the
  block by the resize, as VENTOFFICE always did.
- On a COPY of KIFAF (probe, sheet's own rooms as the model): 76 outside
  air + 40 exhaust categories filled, no errors on any room row once
  calculated; a Dwelling Unit room gives R=NA, S=0, AA=Ra x A rounded.
- officetest +30 (520), mutation-checked (6); ventsettingstest +1.
  Rendered (autovent-occupancy*.png, export tab). Deck regenerated.
  Deployed to all but **2025 (Revit 2025 open)**. **Never run in Revit.**
- The defaults are engineering guesses (no office sheet had categories
  filled to learn from): review them in Occupancy... before relying on
  them. Not set: "Nature of use" (AD) - the sheet's own default applies.

## AUTOVENT: "Update the imported worksheet" - 2026-10-14

From the improvement list (item 2), plus the user: "it will add any new
spaces added by the arch or ID to the excel sheet and do any updates to
the spaces updated by the arch". The old "Fill the Imported worksheet"
blanked every input in every room row and deleted unticked levels.

`VENTUPDATE` (Revit-free) replaces `REVITEXPORT.Rewrite`:
- **matching**: hidden `AUTOVENT model ID` column (all three exports
  write it now) → number on the level → a name only one room has there.
  A sheet from before the column: number/name the first time, exact
  after.
- matched rows keep everything typed (occupancy, notes, a value typed
  over a formula); name, number, area, height, units from the model.
- new rooms added (inputs blank, the sheet's own formulas); new levels
  placed by the model's order, the sheet's own level order untouched.
- **removed rooms kept, greyed + note** (user chose this over deleting);
  the dialog adds one line: "N room(s) no longer in the model".
- unticked levels copied exactly; unticked rooms' rows left as they are
  and never added (`Room.Keep`, collected instead of dropped).
- merged air rebuilt; a total typed over a merged cell put back.
- Sub Totals re-pointed, the Total rebuilt from every Sub Total, and
  the summary tables' room range grows with the block.

Two user decisions (asked): removed spaces kept + marked; a hidden ID
column is fine. Option renamed "Update the imported worksheet" (tooltip:
"Adds new rooms and updates changed ones. What you typed stays.");
confirm "Update worksheet "X" from N level(s)?". Deck slide 13 updated.
officetest +37 (490), mutation-checked (9 mutations; one survived at
first and exposed a lost typed total - fixed). All six build; deployed
to 2022, 2023, 2026, 2027 - **2024 and 2025 locked by open Revit**.
**Checked on a COPY of the user's `KIFAF (1).xlsx`** (worksheet
"Ventilation", 128 rooms, LEVEL 04A, apartments) with a throwaway probe
whose "model" was the sheet's own rooms: nothing changed but the hidden
ID column (all 128 rows identical, 131 -> 131 rows, no #REF!). With one
room removed and one added: the new row has the sheet's formulas, the
removed one is greyed at the level's end, Sub Total `SUM(AA12:AA140)`,
the summary SUMIFs `$AB$12:$AB$140`. **Never run in Revit** - the room
collection (Keep, storey order) is Revit-side. Read
`Desktop\ATKINSMEC_export_log.txt` after: it lists every room added,
changed and greyed.

## Renamed: the Atkins MEP Toolkit - 2026-10-12

User asked for a name for the suite, then: "rename it to atkins mep
toolkit". Done for everything a person sees - the ribbon tab (was
"ATKINS"; the Mechanical panel stays), the `.addin` Name/Text/Description
(the description listed three add-ins of ten), window titles, dialog
captions, external event and updater names, log headers, the CHW sizing
sheet's title line, the installer's window and messages, its file
metadata and its output, now `AtkinsMEPToolkit-Setup.exe` / `.zip`; the
installer README (which said five add-ins). **Kept as `ATKINSMEC` on
purpose**: assembly, namespace, `.addin` and `ATKINSMEC.files.txt` file
names, AddInId and VendorId, `%AppData%\ATKINSMEC\`, the Desktop log file
names, `QAAVF`'s analysis style and schema (stored in models) - see
CLAUDE.md. The tutorial video (`docs/AUTOVENT-tutorial.mp4`) still shows
the old tab; only its script was changed.

## AUTOVENT: Merge rooms; every edit survives a close - 2026-10-12

User: "two or more spaces served by 1 FCU, and this FCU is supplied by 1
fresh air connection" - a **Merge** (and **Unmerge**) in Arrange rooms:
the rooms' fresh air and extract cells merged in the sheet, their total
going to the one connection. Then: "any editing done while working with
the addin can be saved if it was closed and opened again".

- **Merge**: `VENTARRANGE.Merges` (+ `Together`, `Merge`, `Unmerge`;
  ungroup/regroup/reset leave a merge); the dialog's buttons, a "merged
  with 121.107" note on each row, "N merged group(s)"; the side buttons
  scroll at the window's smallest. Export: `VENTROWS.MergeAir` -
  fresh and extract merged down the group, the top cell
  `IF((f1)&(f2)="","",N(f1)+N(f2))` over each row's own formula. The
  first version, `(f1)+(f2)`, was `#VALUE!` straight from an export (a
  room with no occupancy is `""`) all the way to the Total - officetest
  now checks that. Import reads the merge (`FreshMerge/ExhaustMerge`);
  the airflow write joins sheet merges and Arrange merges into groups
  (`VENTFCUCHECK.Groups`), the group's total shared by every in-room
  terminal of its rooms, carried once by endcaps.
- **Saved now too** (`VENTSAVED`): merges, room ticks by hand, the
  workbook + worksheet (reopened on open; a missing file is said in the
  status, its path kept), the export way / worksheet / new name; the
  reach, endcap and bellmouth boxes save on leaving them.
- Tests: ventleveltest (+15), ventfcutest (+8), officetest (+16, 453),
  ventsettingstest (+7); each mutation-checked. All 20 harnesses pass.
  Built for all six; deployed to all but **2024 (Revit 2024 open with
  kifaf)** - close it and run `build-all.ps1` before trying this.
- **Never run in Revit.** To try on kifaf LEVEL 04A: Arrange rooms →
  pick KITCHEN 121.126 + LIVING/DINING 121.125 (or whichever pair one
  FCU serves) → Merge → export → check the merged cells → Assign
  airflows, and read `BELLMOUTH`/`MERGED` lines in the debug log.
  Undo the airflow write afterwards; do not sync.
- Not done: SUMIF-by-system tables count a merged group under its top
  row's system - right when the merged rooms share a system, which one
  FCU's rooms do.

## AUTOVENT: Ungroup / Regroup; system types in a dialog - 2026-10-12

User: Arrange rooms' "Take out of apartment" / "Put back in apartment"
are now **Ungroup** / **Regroup** (row note "ungrouped from ...", count
"N ungrouped", the levels tooltip and the export log to match). And the
two small system-type lists on the assign tab are now two buttons -
**Fresh air systems...** / **Extract systems...** - each opening
`VENTSYSTEMDIALOG` (search, ticks, All/None for the rows showing, a
count, OK/Cancel; Cancel changes nothing), with a line under each
saying what is ticked. The levels card gained the height. Rendered at
1x and 1.5x (`autovent-systems*.png`); `tests/video/make_video.py` and
the tutorial's control list point at the new names; the deck finds its
screenshot cards by colour now rather than by fixed position. All 20
harnesses pass; deployed to all but **2025 (Revit 2025 open)**.

## AUTOVENT: kifaf LEVEL 04A airflow run - bellmouths, wall grilles - 2026-10-11

User ran Assign airflows on LEVEL 04A with `KIFAF (1).xlsx`: 121.325,
121.172, 121.165, 121.168 got no fresh air; kitchens 121.126 / 121.137
and living 121.177 (no air in the sheet) were listed as "2 FCU(s)", served
by one. From the debug log:

- **The facing test found 8 of 32 bellmouths** - mostly ones sitting
  inside a unit's box. The rest, drawn beside their FCU pointing past it,
  were taken for terminals of the ensuite / powder room / kitchen they
  stand in. Now `VENTFACING.Bellmouth` (faces, touching, nearest in
  reach with the room and the sheet as tie-break) - see CLAUDE.md.
- **"2 FCU(s)"**: only the kitchen was flagged while its FCU also served
  living and foyer, so not a shared system - another FCU's terminal was
  counted in the kitchen. Most likely a sidewall grille in the kitchen
  wall. A terminal's room is now the one in front of its face.
- The divided list now names only rooms with air on that side.

ventfcutest +17 (mutation-checked); all 20 pass; deployed to all six.
**Re-run LEVEL 04A and read the log**: the `BELLMOUTH` lines (each
with its reason), `TERMINAL ... faces into`, and `FCU <id>: rooms`.
Known gap: a bellmouth in a room that needs air, beside an FCU that does
not supply that room and not touching it, is still the room's.

## AUTOVENT: the apartment's number beside its name - 2026-10-11

User (screenshot of an export): *"for the apartments only, add the
apartment room number from the linked arc model in this empty cell"* -
column G, merged down beside the apartment's name. `VENTROWS.Group` now
writes `Room.ApartmentNumber` there, as text, for both exports; only when
the Space block has a column between the apartment and the room names.
officetest +3 (mutation-checked), 437 pass. Deployed to all but **2024
(Revit 2024 open)**. Never exported from Revit with it.

## AUTOVENT: export dialogs one line, no "office" - 2026-10-11

User: *"remove any text the addin showing about the office, also keep
the task dialog message very short like 'the sheet has been exported to
(the path chosen)'"*. The export result is now exactly that; the full
report goes to `Desktop\ATKINSMEC_export_log.txt`. The confirm before
an export is one question (plus unticked rooms). "Office" removed from
the report, two VENTOFFICE errors and the Apartments tooltip. Then, at
the user's request, from the other add-ins too: DUCT ESP (coefficients
note, export, log), SHEET QA help (grid bubbles, CHW table) and NWC help.
**SPRINKLERRULES' "office" stays** - it is a room-name keyword for the
hazard class, not text. All 20 harnesses pass; deployed to all but
**2024 (Revit 2024 open)**.

## AUTOVENT: export tab hint removed; a user guide deck - 2026-10-11

User: *"remove this text from the autovent addin"* - the paragraph on the
export tab explaining each of the three ways (`TxtExportHint`,
`VENTEXPORTNOTE.Hint`). Gone, all three variants; the fixtures line stays,
at the foot of that column. Rendered at 1.5x. Deployed to all but **2024
(three Revit 2024 sessions open)**.

**`docs/AUTOVENT-guide.pptx`** - 19 slides for Revit users, generated by
`tools/autovent-deck/make_deck.py` from the `tests/ventpreview` renders
(OOXML written directly: no PowerPoint, LibreOffice or python-pptx here).
Passes the pptx skill's validator and the Open XML SDK's; never opened in
PowerPoint. Regenerate after any change to the window.

## AUTOVENT: a bellmouth into its room, or into an FCU - 2026-10-10

User's four cases: (1) bellmouth serving its room, FCU in that room;
(2) bellmouth serving its room, no FCU; (3) endcap feeding an FCU
outside the room; (4) bellmouth feeding an FCU, both in a room the FCU
does not serve. 1 and 4 told apart by where the MOUTH points
(`VENTFACING`): a bellmouth feeds the unit its air would hit within the
endcap reach, else it serves its room; endcaps traced as before. New
**Bellmouths** box beside **Endcaps** (default `BellMouth`; empty = every
bellmouth serves its room, the old behaviour). Units on the bellmouth's
own duct system are excluded. Tests: ventfcutest +16 (the four cases in
metres, both connector-Z conventions, margin, reach, two units;
mutation-checked), ventsettingstest +4; all 20 pass; rendered 1x/1.5x;
deployed to all but **2024 (Revit 2024 open)**. **Never run in Revit**
- above all the connector direction on the real bellmouth family; the
log prints each bellmouth's mouth vector and the unit it faces.

## AUTOVENT: endcaps by name; air with nowhere to go - 2026-10-10

User (kifaf 1BDR-C, with a plan screenshot): the master bedroom's FCU
sits over the ENSUITE, fed by a fresh air bellmouth there. The debug log
showed why it failed: only families named "Endcap" were traced to their
FCU, so `ATR_ME_MEQ_BellMouth-Fresh Air` counted as an ensuite terminal
(ensuite fresh = 0), and MASTER BEDROOM 121.046's 50 l/s was written
nowhere - silently. Now: an **Endcap names** box under the reach
(default `Endcap`; type `Endcap, BellMouth`), saved per model; and a
room whose air found no terminal or endcap is listed in the run's
dialog and log. Tests: ventsettingstest +8, ventfcutest +5 (both
mutation-checked); all 20 pass; rendered at 1x and 1.5x; deployed to
all six (2024 too). **Never run in Revit** - in particular which FCU
the bellmouth binds to (nearest connector within the reach).

## AUTOVENT: find and replace; a window that fits; CLAUDE.md cut - 2026-10-10

User picked 5, 7 and 8 of the suggestions. **Find and replace...** in
Arrange rooms (rooms and apartment headings, any case, through Rename).
**The window fits the screen** (`VENTFIT`): opens no bigger than the
working area, minimum 480 tall, and the body scrolls when shorter than
its rows need. **CLAUDE.md is the rules only** - 3555 lines down to
about 450; the long version is `git show 465c133:CLAUDE.md`. Tests:
ventleveltest +replace checks, all 20 pass. Deployed to all but **2024
(Revit 2024 open)**. Never run in Revit.

## AUTOVENT: rename rooms before exporting; shorter wording - 2026-10-10

User: *"allow option for user to rename rooms before exporting"*, and
*"do not overexplain things in the autovent addin"*. Rename... (F2,
double-click) in Arrange rooms, for rooms and apartment headings; the
export AND the airflow write use the new name, so the round trip holds.
Dialog hint, tooltips and status lines cut to a line. Tests: ventleveltest
+7 (mutation-checked), ventsettingstest +2; all 20 pass. Deployed to all
but **2024 (Revit 2024 open)**. Never run in Revit.

## AUTOVENT: a search box on Levels to process - 2026-10-10

User: *"add search bar also into the rooms to process"*. In the levels
card's heading: finds rooms by level, number and name (apartment and
model included); only hides rows - ticks stand and still count; while it
is on, a level's tick, All, None, Ctrl+A, ranges and picked levels act
on the rooms showing. Levels it opens close again when it is cleared.
`levelrowtest` +17 (mutation-checked), all 20 harnesses pass, rendered
at 1x and 1.5x, built and deployed to all six (hash-checked - 2024
included, so the Arrange rooms search below is now in 2024 too). Never
run in Revit.

## AUTOVENT: a search box in Arrange rooms... - 2026-10-10

User: *"add a search bar inside the Arrange rooms for the sheet window to
look for rooms using keyword"*. Beside the Level picker: every word, any
order, any case, over number, name, model and apartment; found rooms
shown with their apartment heading; Move up/down step past what is
showing; the line under the list names the other levels with matches as
links. `ventleveltest` +9 (mutation-checked), all 20 harnesses pass,
rendered at 1x and 1.5x. Deployed to all but **2024 (Revit 2024 open -
redeploy after closing it)**. Never run in Revit.

## AUTOVENT: Arrange rooms... - take IND rooms out, drag rooms into order - 2026-10-10

User: *"make option for user to list the IND rooms and he can select any
IND room and make it ungrouped from ARCH rooms so it can be added
directly to the excel sheet, also make an option for the user to arrange
the IND rooms grouped under ARCH room by dragging in the list so it will
come with same arrangement in the excel sheet, do the same also for ARCH
rooms but keep everything under its level under its level"*.

- **Arrange rooms...** (levels card, under All / None / Show in plan):
  one level at a time as the sheet will have it - apartments as shaded
  headings with their IND rooms under them, the architect's rooms as
  rows. Drag, Move up/down or Ctrl+Up/Down; an apartment moves with its
  rooms, a room inside an apartment only within it, nothing leaves its
  level. **Take out of apartment** / **Put back** on any picked IND
  rooms; **Reset this level**.
- The levels list and every export follow it (`VENTARRANGE`, applied in
  `RoomsOn` and `CollectRooms`); saved per model; the export report
  says what was arranged. Taking a room out changes the sheet only - the
  airflow write still finds it by its own name.
- Verified: `ventleveltest` +25, `officetest` +7 (434, the real sheet in
  the chosen order), `ventsettingstest` +5; all 20 harnesses pass; built
  for all six and deployed, hash-checked; dialog and window rendered at
  1x and 1.5x. **Never run in Revit; the drag itself has not been tried
  by anyone** - Move up/down does the same thing if it misbehaves.

## AUTOVENT: every architect's room was ONE LEVEL UP - floors now from geometry - 2026-10-10

User: *"LEVEL 03 showing no rooms despite having rooms in the models and
having also apartments and IND models in that level"*. The apartments
log (04:01) proved the cause from geometry: 102 IND rooms with floors
at 37.12 m (`LEVEL 04A`) stood physically inside `ARC-000100`'s TA-04
rooms, which the level arithmetic put on `LEVEL 05A`; `TA-05-04`'s box
began at exactly 40.620 m (`05A`) while its level said 44.11 m (`06A`).
Both ARC links sit at Z = -3.49 m; `ProjectElevation` + the base
point's Z double-counted it. **So the previous entry's reading - "the
architect's rooms are the problem" - was wrong**: `LEVEL 04A` was
showing LEVEL 03's apartments (the nine the user expected), and
`2BDR-A1` (TA-03-09, a LEVEL 03 apartment whose volume reaches up) was
taking 04A's interior rooms.

- **Fixed**: a linked room's floor is the bottom of its own box
  (`HostElevationOf`); every level is corrected by what its model's own
  rooms and spaces measure (`RVTLINKSELECTION.LevelError`, median, 50 mm
  dead band) - this model's storeys, link spans, the debug log, and
  SPRINKLERS' levels. `ApartmentOf` prefers an apartment on the room's
  own storey. Both logs print each model's level error.
- `ventleveltest` +11 (the kifaf numbers; mutation-checked); all 20
  harnesses pass; all six built, deployed and hash-checked (Revit was
  closed).
- **Expect after the fix**: `LEVEL 03` gets `ARC-000100`'s 16 rooms (its
  nine apartments) and `ARC-000010`'s TA-03 cores. The saved interior
  picks are for `LEVEL 04A` only, so LEVEL 03's IND placements (tiers
  253-258, by their numbering) must be ticked in **Interior models...**.
  On `LEVEL 04A`, `ARC-000100` has ONE room (`2BDR-A1`, TA-04-11) that
  every 04A IND room stands in - it will be flagged as spread across six
  placements; that one is the architect's model.
- **Never run in Revit** in its fixed form: the next export's apartments
  log should say "levels out by -3.490 m" for both ARC models and ~0
  for this model and the IND models.

## AUTOVENT: the apartments log, read - the architect's rooms are the problem - 2026-10-09 (evening) - SUPERSEDED, misread (above)

User re-exported `LEVEL 04A` with Apartments on `ARC-000010` AND
`ARC-000100`: *"there should be 9 apartments in the level, but only 3
... were exported and 2 of them showing no rooms ... while 2BDR-A1
showing rooms from other apartments"*. `Desktop\ATKINSMEC_apartments_log.txt`
(16:59) answers all three:

- `ARC-000100` has **57 rooms in the whole model**; on `LEVEL 04A` only
  `2BDR-A2` (TA-03-04, 477 m2), `1BDR-C` (TA-03-06) and `2BDR-A1`
  (TA-03-09) are apartments. `ARC-000010` has only cores there.
- `2BDR-A1` took 28 rooms from tiers 259, 262, 263 and 264 - one room
  drawn across several apartments. `2BDR-A2` and `1BDR-C` took none.
- 80 of 111 interior rooms stood in no room of either model on that
  level. Tier 266 (`IND-000109`, the core: lift lobby, corridors) stood
  in `ARC-000010` cores on `LEVEL 05A` - counted as another level.
- So the add-in reads the models correctly; the nine apartments are not
  drawn as rooms in either ticked model on that level.

Done: an apartment taking rooms from more than one interior placement
is named (export report, airflow log, tooltip); the log now asks every
other loaded model with rooms what stands at the interior rooms, so the
next run says whether `ARC-000150` (Tower A, not ticked) or any other
model has the nine apartments. `ventleveltest` +3 (mutation-checked).
Deployed to all but 2024 (Revit 2024 open).

**NEXT**: re-export once with the new DLL (close Revit 2024 first, run
`build-all.ps1`), read the "What OTHER models have" section. If no model
has the apartments as rooms, the choices are the architect's (add the
rooms) or a new rule - e.g. an apartment per interior placement, which
does NOT fit this job as it stands (tiers 262/263/264 hold two
apartments' worth of rooms each: 22, 14, 18).

## AUTOVENT: interior rooms in no apartment - counted, logged, asked higher - 2026-10-09

User, from the first real job (kifaf, `HVC-000100`, Revit 2024): *"the
apartments rooms from IND model are not exported in the excel sheet
under the apartment from the arch model"*. Their set-up: Apartments on
`ARC-000010` only; seven IND tier placements ticked on `LEVEL 04A`.
`Desktop\vent kifaf.xlsx` (16:31) has `LEVEL 04A` with `ARC-000010`'s 28
core rooms (corridors, stairs, lobbies, risers for both towers) and NO
apartment and NO IND room. Nothing on screen said why.

- **Cause not confirmed - the model cannot be read from here.** Most
  likely the apartments are not in `ARC-000010` at all: on 10/7 (before
  the keyword) its corridor TA-03-31 "held" a `2BDR-A2` room (TA-03-04,
  477 m2) from another placement. The host links `ARC-000100` and
  `ARC-000150` (Tower A) as well, both full of `2BDR-`/`1BDR-` names.
  Second candidate: the IND floors sit more than 150 mm below the ARC
  apartments' volumes, where the point used to be asked.
- **Done**: interior rooms in no apartment are counted (`Map.Outside`,
  by level) and said on the links card, its tooltip, the export report
  and the airflow log; `Desktop\ATKINSMEC_apartments_log.txt` on every
  map build says, per level, what each apartments model has and what
  each interior room was found in (and, for one in none, whether it is
  inside an apartment in plan but outside its height); the apartment is
  now also asked for 1.0 m and 1.5 m above the room's floor.
- `ventleveltest` +5 checks (mutation-checked); all 20 harnesses pass;
  all six built and deployed, hash-checked (Revit was closed).
- **NEXT**: the user re-exports in Revit; read the apartments log. If it
  shows `ARC-000010` with no apartments on `LEVEL 04A`, the answer is
  to tick Apartments on the Tower A ARC model (and untick its row if
  its core rooms duplicate `ARC-000010`'s).

## AUTOVENT: interior models by keyword, ticked level by level - 2026-10-09

User: *"instead of identifying IND model where their rooms sit, you can
identify the instance links in each level defined by keyword from user,
and the user selects the links found in each level"*. Before, every
loaded placement was searched for rooms standing inside an apartment.

- **Interior models...** button beside Refresh on the links card opens
  `VENTINTERIORDIALOG`: a keyword box (default `IND`, filters live; empty
  = every placement, the old behaviour) and, under each level of THIS
  model, the matching placements that have rooms there, with the count.
- Everything found starts ticked and is kept as "follow the keyword"
  until a tick is changed; then the exact picks per level are stored,
  "none" included. A pick on L04 searches only that placement's L04
  rooms (`VENTINTERIOR.Allowed` in `VENTAPARTMENTS.Place`).
- Remembered per model in `autovent_settings.txt` (`interiorkeyword`,
  `interiorchosen`, `interior <level> <id> <label>`).
- Verified: `ventsettingstest` 122 checks (mutation-checked), all 20
  harnesses pass, built for all six, the dialog and the window rendered
  at 1x and 1.5x. Deployed to 2022, 2023, 2025, 2026 and 2027 and checked
  by hash; **2024 was locked by two open Revit 2024 sessions** and is
  stale.
- **Never run in Revit.** Worth checking in a real model: that the
  levels a placement is listed under match where its rooms really are,
  and that unticking one on a level takes its rooms out from under the
  apartments there (the levels tree shows it at once).

## SPRINKLERS: heads off their line - 2026-10-07

User-reported twice with screenshots: one head of a room off its line,
first against the façade, then 345 mm aside. Three faults (CLAUDE.md,
SPRINKLERS, "A HEAD THAT LANDS IN SOMETHING"):

1. a grid point in a column or wall was skipped and replaced against a
   wall - now moved to the nearest clear point of its cell (commit 476d811);
2. that move now prefers the head's own row or column line, within 0.3 m;
3. walls on the floors above and below counted as obstacles - now only
   walls standing in the room's own storey count. This was the 345 mm.

`geomtest` 34 checks, all 20 harnesses pass, all six built and deployed
(hashes match). **Never run in Revit** - re-place the room from the
screenshots: the circled head should sit in line with the others. If one
still moves, the add-in is avoiding something real on that storey.
`nwcpreview` is the one harness still stale. The installer predates all
of this.

## SPRINKLERS reads in an API context; the test gaps closed - 2026-10-06

At the user's request (issues 3 and 5 of the review).

**SPRINKLERS' window no longer reads the model from its own handlers.**
Links, families and what the window says about each family are read in
the constructor; each link's floors through `SPRINKLEREVENT.ReadLevels`,
in a slot apart from `Action` so a tick cannot throw away a waiting
listing. Never run in Revit - check that ticking a link fills the floors
list ("Reading the link's floors..." first).

**Tests.** All 19 current harnesses pass.
- `fittingtest` compiles the REAL rules now - `QACHWSHAPE.cs`, moved out of
  `QACHWFITTINGS` unchanged. 20 checks; one rewritten for Revit's 2-degree
  line, which it predated.
- `roomtest` is still a mirror (QACHWROOM walks Revit connectors) but is
  back in step with six fixes it predated, its models given equipment
  ends, and a cap check added. 29 checks. Change the two together.
- `aligntest` rewritten against the REAL matching - `ALIGNMATCH.cs`, moved
  out of `ALIGNTERMINALS.Plan`; ties now settled by id. 15 checks; the cell
  key check was confirmed to fail against the classic broken packing.
- Still stale: `geomtest` (asserts nothing), `nwcpreview` (does not build).

Built for all six; QACHWFITTINGS and ALIGNTERMINALS changed shape but not
behaviour (ALIGN ties aside). **Neither has run in Revit since.**

## AUTOVENT reads the model only in an API context; backups; installer - 2026-10-06

At the user's request ("rebuild the installer, then do 4 then 3").

**Installer** rebuilt after everything below; both carry the day's DLLs
(checked by hash inside the zips).

**Backups.** Every commit is pushed by a hook (`tools\git-hooks`) to
`backup-onedrive`, a bare repository in `C:\Users\moham\OneDrive\Backups`.
**OneDrive was not running and no account was signed in**, so that copy
only leaves the machine once somebody signs OneDrive in. **`D:` is
write-protected** ("The media is write protected" - a disk setting),
so the D: backup the user also chose could not be made; the hook already
knows `backup-d` for when it can. Both told to the user.

**AUTOVENT's window no longer reads the model from its own handlers.**
Every read goes through `VENTREAD` - in the constructor (inside the
command) or the external event - and the window holds what was read, so
a tick is arithmetic (`RVTLINKSELECTION.RoomsOn`) and asks Revit only for
what it has not got. Refresh lets go of everything. Found on the way:
**`ExtEventHan` never cleared `Request`**, so a later Raise would have
re-run the last write or export - fixed. `ventleveltest` 60 (11 new),
`ventsettingstest` 83 (4 new), all 16 harnesses pass; built and deployed
for all six, hashes match. **Never run in Revit** - check that ticking
links fills the levels list (the status bar says "Reading the linked
models..." first), that Refresh picks up a link reloaded, and that
Fixtures... opens with its families.

## Under git, harnesses in the repo, AUTOVENT remembers each model - 2026-10-06

At the user's request, after a review of what was worth worrying about.

**Version control.** The project is now a git repository (`main`, no
remote; identity set for this repository only, `core.autocrlf false`).
It had none before, so history starts here.

**`tests\`.** The harnesses moved out of the session scratchpad, which
Windows had been cleaning. The lost ones were rebuilt from the session
transcripts. `tests\run-all.ps1` runs the 16 current ones - **all pass**.
Kept but stale (older recoveries, out of step): `roomtest` (4 checks
fail), `fittingtest` (1), `geomtest` (asserts nothing), `nwcpreview`
(does not build). `chwjointtest` came back with 55 of 64 checks,
`nwcsettingstest` with 18 of 38. `aligntest` is lost outright. Worth
doing, not done: bring the four stale ones back in step, and rewrite
`aligntest`.

**AUTOVENT remembers each model's set-up** (`VENTSAVED`): the room-link
ticks, Apartments, fresh/extract system types, endcap reach and fixture
choices, keyed by the model's central / ACC GUID / file, so every local
copy and every Revit version shares one. A model never set up gets the
defaults rather than the last model's leftovers. `ventsettingstest` 79
(36 new). Built for all six; **all six deployed, hashes match**. **Never
run in Revit.** Check first: open a workshared local, tick something,
close, reopen from a different local of the same central - the status
bar should say the set-up was restored.

The installer still predates all of this - rebuild before handing out.

## AUTOVENT: apartments from SEVERAL architects' models, interior placements found per floor - 2026-10-06

At the user's request: apartments can come from more than one linked
architect's model, and the interior model is usually placed several
times (one placement per band of floors). The "Apartments from" combo
is gone; every link row has an **Apartments** tick instead (any number;
ticking it ticks the row too). Every other loaded placement is searched,
ticked or not, and a room joins an apartment only when **both floors are
on the same level of this model** (the band rule) - a duplex's upper
room or a placement dropped on the wrong floor is left an ordinary room
and counted. An unticked interior placement contributes only its rooms
inside apartments. Each interior row now reads `in apartments on L04 to
L10 - ARC... : 1`, the line beside Refresh sums it up, and its tooltip
lists every pairing; the export report and the airflow log say the same.

The decision moved to `VENTAPARTMENTMAP.cs` (Revit-free half of
`VENTAPARTMENTS`). `ventleveltest` 49 (22 new: `Floors` and the
same-storey decision through the real `Map`); `officetest` 427,
`ventsettingstest`, `levelrowtest` all pass; window rendered at 1x and
1.5x; built for all six; **2025 NOT redeployed** (Revit 2025 open, PID
36344). **Never run in Revit** - the thing to check first is the
pairing line on each interior placement against the floors it really
sits on.

## AUTOVENT: apartments from one link, their rooms from another - 2026-10-06 (superseded above)

At the user's request, following the office workbook's Tower-1 pattern:
an architect's model holds the apartments ("DRIVER/STAFF APT"), an
interior model the rooms inside them. Choose the apartments link under
"Apartments from" on the links card; rooms of the other ticked links
standing inside one of its rooms are then listed under it in the tree
and exported under it - the apartment down the Space column (merged), each
room's own name beside it in H, No of Bedrooms merged per apartment -
in both export ways. The importer now reads that pattern back by each
room's own name (it used to skip an apartment's second and later rooms
and key the first by the apartment). The airflow write and the fixture
count give a terminal or WC to the room inside the apartment, never the
apartment. "None" (the default) changes nothing.

`officetest` 427 (19 new); window rendered at 1x and 1.5x; built for
all six; **2025 NOT redeployed** (Revit 2025 open). **Never run in
Revit.** The installer predates this, the fixture counts and DUCT ESP.

Not done, worth asking: "No of Bedrooms" is merged per apartment but left
BLANK (the office rule for typed values) - it could be counted from the
rooms named "bed". Each apartment room's own S.No/keys likewise blank.


## AUTOVENT: WCs and urinals into "No. of Units" - 2026-10-04

At the user's request ("fixtures FROM LINK, no occupants"): both exports
count the plumbing fixtures standing in each room and write them into
the sheet's "No. of Units" - Table 6-4's per-fixture exhaust multiplies
it. Occupants untouched. From the room links by default (a plumbing
model is a choice, to avoid counting WCs twice), families by a name rule
(WC / water closet / toilet / urinal), both changeable under
**Fixtures...** on the export tab; a tick box turns it off.

`officetest` 408 (8 new, against the real workbook: 6 WCs at 25 L/s come
out as 150 L/s Total Exhaust through the office formulas) - its
`.csproj` had been lost to the temp clean-up and was recreated;
`ventsettingstest` 43 (22 new, the name rule). Window and dialog
rendered at 1x and 1.5x. Built for all six; **2025 NOT redeployed**
(Revit 2025 open). **Never run in Revit** - the export report's
"Fixtures" paragraph says how many were counted, into how many rooms,
and how many stood in no room.

## DUCT ESP - new add-in, 2026-10-04

The tenth button. External static pressure of chosen units - the index
run of every duct connector - calculated across this model AND any
ticked linked models: open duct ends in different models that meet
(within 150 mm, facing) are joined, so an AHU and riser in one model and
the floors in another make one network. Its own Darcy-Weisbach /
Colebrook calculation and a coefficient table, because Revit's own
critical path stops at the edge of a model. ESP = worst outlet + worst
inlet + margin; every port shown; Excel export; writes the ESP to units
in this model; desktop log.

**The built-in fitting coefficients are rough values chosen for the
add-in**, not ASHRAE/CIBSE tables - the window says so until the office
puts its own in (Coefficients tab, saved). Anything from a default is
flagged amber.

`esptest` 60 checks; window rendered at 1x and 1.5x; built for all six;
**2025 NOT redeployed** (Revit 2025 open). **Never run in Revit** - the
first real run's log (`ATKINSMEC_esp_log.txt`, in `%AppData%\ATKINSMEC`
since 2026-10-16) is the thing to
read: joints made between models, near misses, and where each figure
came from. The installer predates it.

**Open questions for the user**: the office's own fitting coefficients
or ESP sheet; whether "worst outlet + worst inlet" is the rule they want
for AHUs with return fans; the right default for a terminal with no data
(25 Pa).

## SPRINKLERS: each head's level - fixed, 2026-10-02

An unhosted head now takes the level of the floor under it
(`SPRINKLERPLAN.LevelUnder`), measured in model coordinates. It used
the NEAREST level by `Level.Elevation`: wrong on survey-point models,
and "nearest" is usually the level above a ceiling anyway, so most
heads were tied to the floor above with a negative offset. Fixed
together on purpose - see CLAUDE.md. Position of the heads unchanged;
ceiling-hosted heads untouched. Built for all six; **2025 NOT
redeployed** (Revit 2025 was open) - close it and run build-all.ps1.
**Never run in Revit.** The installer predates this too.

## AUTOVENT rooms under the level above - fixed, 2026-10-01

User-reported: a level's rooms listed under another level, "probably
stepped up by 1". Every height came from `Level.Elevation`, which is
measured from the project base point or the SURVEY POINT as the level
type says - not in the model's internal coordinates, which is what a
link transform works in. A link whose levels are typed to the survey
point therefore landed every room the whole site datum too high. Now
`RVTLINKSELECTION.InternalZ` everywhere (tree, export, airflow write,
link span). Hovering a room or level in the tree shows the heights
used; the airflow run's debug log lists every level both ways and
checks one room's floor against its own bounding box. Built for all
six, hashes match. **Never run in Revit - confirm on the model that
showed it**, and if a room still sits under the wrong level, the
tooltip and the log's "Check" line are the first things to read.
SPRINKLERS had the same comparison - fixed 2026-10-02, below.
**The installer built this morning predates this and the content span.**

## Link level span from model content, shaft openings ignored - 2026-10-01

At the user's request the span on each link row ("B1_BASEMENT to
P3_PODIUM 3") now comes from the link's MODEL CONTENT - the lowest and
highest of its levels that something stands on - ignoring Shaft
Openings (and rooms, masses, lines, reference planes, scope boxes,
levels, grids, nested links and imported CAD). It used to come from the
rooms, so ignoring shafts would have changed nothing; asked, the user
chose the content basis. The room count stays beside it. Built for all
six and deployed (hashes match, 2025 included); rendered at 1x and
1.5x. **Never run in Revit.** **The installer built this morning
predates this** - rebuild it before handing it out.

## AUTOVENT levels list: several rows picked - 2026-09-30

"Levels to process" picks like a file list - click, Ctrl+click,
Shift+click, Ctrl+A, the arrow keys. Ticking a picked row's box (or
Space) ticks every picked row; Show in plan frames and selects every
picked room (a picked level counts as all its rooms) on the level most
of them are on, and says how many were on other levels. A level's name
now picks the level; only its box ticks.

**Also fixed on the way:** a click on a PART-ticked level unticked all
its rooms - WPF's own toggle sends the third state to unticked, and
CLAUDE.md had said the opposite. It now ticks them all. Tested, not
assumed (`scratchpad/toggletest`).

`levelrowtest` 43 checks (21 new, on picking); rendered at 1x and 1.5x;
built and deployed for **all six, 2025 included** - so 2025 now also has
the room ticks and link labels it missed while Revit 2025 was open.
**Never run in Revit.** Installer rebuilt 2026-10-01 (20.6 MB, both
the exe and the zip); its payload hashes match all six builds and carries
the tree, the room ticks, the link labels and this.

## Link placements labelled, with their level span - 2026-09-30

A model linked more than once now shows which placement each row is
(`ARC-000020.rvt : 2 : Tower B`) - in AUTOVENT, SPRINKLERS, ALIGN
TERMINALS and SHEET QA alike, since they share `GetLinks`. AUTOVENT's
link rows also say which of the main model's levels each placement
covers, from its rooms (or its levels when it has none). Rendered at 1x
and 1.5x; built for all six; **2025 not redeployed** (Revit 2025 open).
**Never run in Revit** - worth checking what `RVT_LINK_INSTANCE_NAME`
and `Element.Name` actually hold on a real instance, since the label is
built from them.

## AUTOVENT room ticks - 2026-09-30

Each room under a level has a tick, at the user's request: unticked
rooms are left out of the export AND the airflow write. The level's
tick follows its rooms (all / none / some). Logic in VENTLEVELROWS,
tested at scratchpad/levelrowtest (22 checks); rendered at 1x and 1.5x.
Built for all six; **2025 was not redeployed** - Revit 2025 was open.
**Never run in Revit.**

## AUTOVENT levels tree and "Show in plan" - 2026-09-30

At the user's request the levels list became a tree: each level opens
to the linked rooms on it, and a room can be shown in a plan of its
level (button, or double-click). See CLAUDE.md under AUTOVENT.
Rendered at 1x and 1.5x; all six built and deployed, hashes match.

**Never run in Revit.** Worth checking on a real model:

- that setting `ActiveView` from the external event switches the view
  (the API allows it with no transaction open, which is the case);
- that the zoom lands on the room when the link is ROTATED or moved by
  shared coordinates;
- the plan chosen when a level has several (mechanical, ceiling,
  dependents with crops);
- on 2023+, that the linked room shows as selected; on 2022 it is only
  framed.

The tutorial video is now further out of date (the levels card is a
tree).

## AUTOVENT window at 150% scaling - 2026-09-28

The two texts that clipped at 150% are fixed (see CLAUDE.md, "THE WINDOW
CLIPPED TEXT AT 150% SCALING"): the levels warning comes first in its own
amber text and the summary scrolls for real; the endcap hint has both
sentences again. The top two lists went from 150 to 116 px so the bottom
card clears the status bar. Checked by rendering at 1x and 1.5x, assign
and export tabs, and with ten missing levels. Built and deployed to all
six, hashes match. **Not opened in Revit.**

**The tutorial video (docs/AUTOVENT-tutorial.mp4) is out of date** - the
levels card, the endcap hint and the top lists all look different now,
on top of the export-tab wording noted below. Regenerate as CLAUDE.md
describes.

**Open:** at 150% on a 1080p screen the window (860 tall, MinHeight 800)
is taller than the ~720 px of screen available. Not changed.

## AUTOVENT levels by band - 2026-09-24

A linked room now belongs to the main-model level whose BAND its floor
stands in (that level up to the next), with a 300 mm allowance below a
level, at the user's request - replacing "nearest level". The floor is
the room's level plus its base offset. Rooms below the main model's
lowest level are left out and counted. Harness `ventleveltest`, 27
checks; built and deployed to all six, hashes match. **Not run in a real
model** - worth checking the window's per-level room counts against a
job with a mezzanine or split levels.

The installer was rebuilt with this and the 150% window fix on 2026-09-28.

## AUTOVENT export from the office template - 2026-09-24

Asked for as: use "API TWIN TOWER PODIUM VENTILATION CALCULATIONS" as
the template, keep its three ASHRAE worksheets, and write a new
worksheet in the MLCP / Tower 1 / Tower 2 format with a Room Number
column, the ticked levels as blue rows, room name/number/height from
Revit, and every cell without a formula blank. Both "new workbook" and
"add a worksheet" now do this (`VENTOFFICE`); "fill the worksheet I
imported" is unchanged.

**First real run: "the exported excel sheet is coming out empty"** -
the cells were all there; the sheet opened scrolled to row 297 with the
wrong rows frozen, a view carried over from the office file. Fixed in
both export paths, the harness now checks where the sheet opens, 2023-2027
deployed. **2022 was NOT redeployed** - Revit 2022 was open and held
the DLL. Re-run the export after the next deploy and confirm it opens
on the headings and the first level.

**Verified without Revit:** `scratchpad/officetest`, 400 checks against
the real workbook - all three patterns, growing and shrinking the
block, new workbook and both add-to cases, read back through the real
`VENTSHEET` import and calculated by EPPlus. `scratchpad/officeembed`
exported through the built 2025 DLL's own embedded resource. Every
output opens in openpyxl. Built and deployed to all six versions,
hashes match.

**Judgement calls the user has not confirmed:**

- **Tower 2 is copied, not MLCP** - MLCP's exhaust column (the one
  AUTOVENT reads back) is typed, the Towers' is calculated. See
  CLAUDE.md.
- **Area is written too**, though only name, number and height were
  asked for - every formula in the row works from it.
- **The name goes in the visible Space cell (F)** as a value, replacing
  the office's `=CONCAT(B," ",C," ",D," ",E)` over the hidden columns,
  which would otherwise read "   ICT ROOM".
- **Title block**: project name and job ref from Project Information,
  part of structure = the sheet name, the rest blanked.
- **Equipment summary**: kept with its formulas; the API job's unit
  tags, remarks and typed airflows cleared.

**Not opened in Excel** - no Excel on this machine. EPPlus and openpyxl
both read every output, but Excel's own "repair" check has not been
seen. Worth one look: open a new workbook, pick an occupancy on a room
and watch the row calculate.

**The tutorial video is now slightly stale**: it shows the old radio
label ("from the standard layout") and the old hint. Regenerate with
`scratchpad/ventpreview <folder> tutorial` then `make_video.py` (its
caption at line ~980 says "standard layout" too).

## AUTOVENT in WPF - 2026-09-20

Asked for as "create the autovent addin in WPF instead of windows form
and make it look good and professional". `Winform.cs`,
`Winform.Designer.cs` and `Winform.resx` are gone; `VENTWindow.xaml` +
`.xaml.cs` replace them, hand-styled with no third-party dependency
(the suite's one outside package is still EPPlus).

Three bugs came out of rendering it at `scratchpad/ventpreview`, none
of which a compile would have caught: WPF eating the underscores in
level names, the levels card starved to a sliver by auto-sized rows
either side of it, and the action card clipped at the default height.

**Not opened in Revit.** Worth watching: that the window really does
stay in front of the model (it is owned by Revit's main window handle,
which is the fix for that), and that it looks right at the machine's
own DPI - the preview renders at 96.

## CHW pipe disconnection page - new, 2026-09-20

A sixth SHEET QA tab, asked for as "similar to the duct disconnection
check but for CHW, it should also check the pipes connected to FCU
family and reconnect them, aligned to the corresponding connector of
the same system - the FCU will not be moved, only pipes".

The scan, the three checks and the tab all mirror the duct page. The
new thinking is the matching rule and the geometry, both Revit-free and
tested at `scratchpad/chwjointtest` (27 checks).

**First run, 2026-09-21**: reported as "it should work on pipes not
ducts". The scan always did - the wording and the controls did not.
Cloning the duct tab re-parented the duct page's own spinners onto the
CHW group, which took them OFF the duct page, and wired the CHW radios
to the duct page's scope handler. Both fixed; the page now has its own
reach and its own handler, and `Fill` was missing for it, which is why
both lists came up empty.

**Still not verified against a real model.** Specific unknowns, in
order:

- Whether `Connector.PipeSystemType` reads `SupplyHydronic` /
  `ReturnHydronic` on the real FCU family, or comes back Undefined - in
  which case every pipe is refused rather than mis-joined, which is the
  safe direction but means the page does nothing.
- Whether 300 mm reach is right. It is a setting.
- **The slide IS done now** (`QACHWJOINTFIX.Slide`) - unhook, move the
  fitting and the drop together, restretch the pipe beyond, hook back
  up. Never run in Revit. The things to watch: whether `MoveElements`
  on a connected fitting behaves, and whether the pipe beyond really
  absorbs the shift rather than Revit dragging something unexpected.
- **A four-pipe unit is the dangerous case** and the reason
  `Connector.Description` is now read: two connectors both saying
  SupplyHydronic, one chilled and one heating. Where the family author
  wrote nothing, the finding says the choice was made on distance
  alone. Worth checking what the office's FCU families actually put in
  that field - if it is blank everywhere, every four-pipe unit will be
  flagged as a guess.

## EQUIPMENT CHANGER - new, 2026-09-17

Asked for as "change the type of the mechanical equipment and resize
the duct or fitting connected to it along with any duct accessory
connected on it until it reaches a reducer or tapoff or air terminal",
with two screenshots: a branch takeoff fitting, and Revit's
"cannot be ignored" connectivity error with four errors queued up.

Built as QACHWRUNS' walk plus QACHWROOM's disconnect-and-reconnect,
applied to ducts. Verified on 2022 and 2027 before any of it was
written: the duct size parameters, `FamilyInstance.Symbol`,
`Connector.Shape/Width/Height/Radius`, every `PartType` it names, and
the whole `IFailuresPreprocessor` surface.

**Where it stops is Revit-free and tested** - `EQUIPSTOP`, 33 checks at
`scratchpad/equiptest`, including the case that matters most: a fitting
whose family will not say what it is, whose two ends are simply
different sizes, is a reducer.

**First run in Revit, 2026-09-17**: reported as "it doesn't change the
family type, and it's not modeless". Both dealt with - made modeless
through `EQUIPEVENT`, and two real causes found for the type not
changing: changing a unit to the type it already has (silent success),
and the preprocessor rolling the whole transaction back at commit while
the window reported success. The report is now taken from the model
after the commit rather than from what was attempted.

**Second run, 2026-09-17**: the log did its job. Every step succeeded
and the commit threw the lot out with "the duct/pipe has been modified
to be in the opposite direction" - the new duct line was built far-end
first, reversing it. Fixed in `EQUIPJOINT.Bring`, and **the identical
bug was found and fixed in `QACHWROOM.Restretch`**, where `Sound` had
been quietly catching it as "a pipe has turned" and refusing the slide.
Worth re-testing the CHW slide routes now: they may work considerably
more often.

**Still not verified in Revit beyond that.** The specific unknowns,
worth watching in this order:

- **Does the disconnect-first order actually avoid the error?** That is
  the whole premise. The log names any failure Revit raises and its
  type; `EQUIPFAILURES` rolls back rather than taking Revit's offer to
  disconnect the family.
- **Do the fittings take their new size, or revert?** The pipe side
  needed three routes before it worked. This has only the gentle one
  (write the connectors, read them back) and reports what will not
  take, rather than replacing anything.
- **Does `EQUIPJOINT.Nearest` pick the right connector** on a swap
  between families? Within one family the connectors are in the same
  places and it is safe; across families it is a guess, and getting it
  wrong crosses supply and return silently.
- Whether `Reach` = 40 is generous or mean on a real branch.
- Whether the modeless conversion left anything calling the API from
  the form. It compiles either way; the symptom would be "Revit is
  currently not within an API context" on a button.

## NWC BATCH EXPORTER - new, 2026-09-16

Asked for as "the same idea as the CHW volume addin but it will export
NWCs": a batch exporter over many models, opened in the background.

Built on CHW VOLUME's pattern and reusing its ACC machinery whole
(`CHWCLOUD` for the cache scan, the region probing, the detached open
and the workset modes; `CHWCACHEForm` as the picker). Its own settings
file, so neither add-in writes the other's choices away.

**Verified before writing any of it**, on 2022 and 2027: the whole
`NavisworksExportOptions` surface, `Document.Export`, and
`OptionalFunctionalityUtils.IsNavisworksExporterAvailable()`. One wrong
guess - `Parameters`, not `ExportParameters`.

**The preview harness earned its place immediately**
(`scratchpad/nwcpreview`): `CheckedListBox` throws on any `SelectionMode`
but `One`, inside `InitializeComponent`, so the window would have
crashed the instant the button was pressed. A hand-written designer file
should always be rendered before it is shipped.

**Import/export of the export settings, asked for the same day.**
`.nwcset` files through `SaveTo`/`LoadFrom`, sharing one `Compose` with
the personal saved state so the formats cannot drift. The model list is
kept out on purpose and `LoadFrom` ignores job lines even if a file has
them - a shared settings file must not drop somebody else's models into
your window, nor empty your list. Harness at
`scratchpad/nwcsettingstest`, now 38 checks, including a German-locale
machine writing `3,5` for the faceting factor.

**Three per-model choices, asked for over two messages.** A model can
have its own 3D view (typed into its row), its own whole set of export
settings (`Settings...`), and its own worksets (`Worksets...`). All
three are stored under the model's job line - `jobview`, `jobset`,
`jobwork` - and all three attach to "the job last read", so the tests
assert they do not leak onto the neighbouring models. The models list
became a `DataGridView` to carry them.

Worksets are read with `WorksharingUtils.GetUserWorksetInfo`, which is
documented for exactly this and needs no open; verified present on all
six versions. They are stored by **name and UniqueId** (`WORKSETPICK`),
never by `WorksetId` - the API says that one changes on sync, so it is
resolved fresh before every open. The id is what lets a pick survive a
rename; the name is what makes the file readable and is all an older
settings file has. Matching tries the id across the whole list before
falling back to names, because the other order matches a renamed
workset to whatever inherited its old name.

**The ACC models tested are saved in a newer Revit than 2022.** The
log settled it: the region probe worked and resolved a real path, then
the open said *"The cloud model is not saved in current release of
Revit"*. Nothing in the add-in can read those from 2022 - the export
has to run from the version the model belongs to. **So the ACC path is
still unproven end to end**; what is proven is that the region probing,
the fallback and the logging all behave as intended. Try again from
the matching Revit version.

**A cloud path needs a region, and the cache does not carry one.**
This was the actual cause of the empty ACC picker: `PathFor` used the
model's region verbatim, and for anything picked out of the
collaboration cache that region is empty, so the path could not
resolve and both reads failed identically. The reads now probe regions
the way `OpenWithRegions` always has, and keep the one that answered.

**The no-open read asks the CENTRAL, and that is its real limit** -
not the cloud as such. **User-reported from a real model**: an empty
picker saying "The central server could not be reached". It fails for a
local copy off the network, a detached model and often an ACC model
alike. Opening the model reads the file itself and needs no central, so
`WorksetsByOpening` now runs **automatically** when the quick call
comes back with nothing; it was behind a Yes/No, which for those models
was a wall that appeared on every click. Results are cached per window,
and the model already open in Revit answers for itself.

**Still not run against a real ACC model.** What the screenshots
confirm is the failure path and the message; whether the background
open then succeeds on ACC is the thing to try first.

**Not run in Revit at all yet.** Everything about it is compile- and
render-verified only. The specific unknowns: whether a background-opened
document exports as readily as an active one; whether the exporter
behaves when handed a view id from a detached model; whether
`GetUserWorksetInfo` is quick enough on a big ACC model to sit behind a
button with only a wait cursor; and how long a real batch takes. The
failure mode is per-model and reported in the table rather than fatal,
and nothing is written back to any model.

**Deliberate refusals worth keeping:** a model without the named 3D view
is reported rather than silently exported whole; the document open in
Revit is never closed; models are opened DETACHED so no central is
touched and nobody's worksets are taken.


## Flowrate page: pipe tags - 2026-09-16

Reported as "also check the zero flowrate tags of the pipes with the
ducts". Pipes were never missing from the check - `FlowOf` has always
handled the four pipe categories and `TagsIn` filters no categories at
all. The defect was `!parameter.HasValue` being treated as "this
element carries no flow", when for a pipe or duct on no system it means
exactly the case worth reporting. Unset now counts as zero and is
worded differently from a calculated zero.

**Worth checking on the real model**: whether this now lists more than
expected on ducts too - the same guard was hiding unset duct flows, and
that side has not been seen in a real job.

## Silent running - 2026-09-16

Asked for as "make the exporting a silent operation with a progress
bar" and "automatically select OK or Close for any messages during the
models opening".

`Shared/QUIET.cs` answers `UIApplication.DialogBoxShowing` and clears
warnings from `FailuresProcessing`. The button is found by trying -
`OverrideResult` returns whether the code was accepted - rather than
assumed, and a dialog that accepts nothing is logged as unanswerable.
Warnings are cleared; errors are not touched. On by default
(`NWCSETTINGS.Silent`), with a tick to turn it off.

The progress bar is driven by `CHWSTOP.Went(done, total, what)`, called
between models by `NWCEXPORT.Run`.

**Not run in Revit.** The two things to watch: whether the button order
in `QUIET.Buttons` actually dismisses the dialogs these models raise -
the log names any it could not answer, which is the first thing to read
- and that `QUIET.Stop` really does come off, since a handler left
behind would answer dialogs for the rest of the session. The `finally`
is there; it has not been watched.

## Every window resizes - 2026-09-16

Asked for as "make the windows of all addins sizeable and can be
maximized/minimized". All seventeen are `Sizable` with both boxes, 221
anchors so the space goes to the tables rather than to grey, and
`Shared/FORMSIZE.cs` for a minimum size taken after WinForms has scaled
the form.

**Only the NWC windows can be rendered without Revit**, so the other
thirteen are verified by `scratchpad/anchorsim.py` - it applies
WinForms' anchor arithmetic to a 400x400 growth and reports siblings
that collide, distinguishing a new overlap from one that was already
there. It reports one overlap, in AUTOVENT's `Winform`, of one pixel
between `lblSheetPath` and `cmbWorksheet`, which was there before and
is invisible.

Two real bugs came out of it, both recorded in `CLAUDE.md`: `btnExport`
and `btnStop` on NWC BATCH disappearing behind the results table when
anchored to the bottom while sitting between two groups, and
`SPRINKLERForm`'s "Clear of walls" field sitting on top of the Place
and Close buttons - a pre-existing overlap that had to be resolved
before that row could be anchored at all. The sprinkler buttons moved to
a row of their own and that window grew 692 -> 730 high.

**Not opened in Revit yet.** The arithmetic and the NWC renders are the
whole verification. Worth a look on a high-DPI screen in particular,
since `FORMSIZE` takes the minimum size from the scaled form and that
path has not been watched.

---

Last updated 2026-09-16. Read `CLAUDE.md` first for how to build and the
traps worth knowing.

**Revit 2025 was running during the last build**, so five of the six
deployed and 2025 kept its old DLL - the documented silent skip, caught
by the hash check. Close Revit and run `build-all.ps1` again to put 2025
in step. Everything below is about behaviour, not the build.

---

## Verified

- **Multi-version build** 2022–2027, hash-checked into each add-ins
  folder.
- **Sprinkler geometry** validated against a standalone harness on
  rectangles, a rotated room, a trapezoid, an L-shape, a wedge, a
  corridor, rooms with columns, and slots too thin to place in. NFPA
  spacing, max coverage area, the 1.8 m minimum head spacing and the
  100 mm wall clearance all hold.
- **Excel parsing** (AUTOVENT) run against the real `vent sheet.xlsx`:
  column detection by keyword, blue-fill level rows, and the four
  worksheets each having different column layouts.
- **CHW Excel export** driven with synthetic results; produces a valid
  workbook.

## Not verified — needs a real model

Nothing in the Revit-side plumbing of SPRINKLERS or DUCT INSULATION has
been run against live geometry. Highest-value next tests:

1. **SPRINKLERS end to end** — list rooms from the arch link, check the
   preview table, place into a real ceiling. The `Zimmer 14.0 m²`
   trapezoid is the obvious first room (Light hazard → one head).
2. **Face hosting** — placing a face-hosted concealed pendent onto a
   *linked* ceiling uses `Reference.CreateLinkReference`. Compiles, never
   run. Try one room before a whole floor.
3. **DUCT INSULATION** — draw one duct on a system type with a thickness
   set; confirm insulation appears and undoes in a single Ctrl+Z.
4. **ACC background open (CHW VOLUME)** — see below. Compile-verified on
   all six versions, never executed.
5. **ALIGN TERMINALS end to end** — see below. The matching maths is
   tested; nothing that touches Revit has been run.
6. **SHEET QA** — see below. Run it on a model with known zero-flow tags
   and check the count against a manual look.

### ACC background open — built 2026-09-01, compile-verified only

CHW VOLUME can now measure a model that lives in ACC without showing it.
`CHWCLOUD.Open` builds a cloud path from region + project GUID + model
GUID, opens a background document, `CHWVOLUME.Calculate` measures it, and
it is closed in a `finally`. Files: `CHWCLOUD.cs`, `CHWCLOUDForm.cs`,
plus `Cloud`/`IsCloud` on `CHWVOLUME.ModelItem` and `Failure` on
`ModelResult`.

What to expect on the first real run:

- **Sign-in is required.** Revit must be signed in to an account with
  access to that ACC project, or the open throws. The failure is caught
  and shown as a red row naming the model, not a row of zeroes.
- **The region is no longer asked for.** Nothing readable reports it, so
  `CHWCLOUD.Open` tries each region in turn, keeps whichever opened, and
  `CHWForm` saves that back to the list — one wasted attempt, once. The
  combo in the manual dialog still lets it be set outright.
- **Links inside a background-opened model are not loaded**, so an ACC
  model reports its own pipework only. Add each linked model to the list
  separately if its volume is wanted.
- **Piping system type names cannot be read from a closed model.** There
  is no API for it, and a byte scan of a cached `.rvt` finds nothing —
  only the header is plain text, element data is compressed (checked
  against a real 93 MB mechanical model: zero hits for any system type
  name). So the names are harvested from an open instead:
  `CHWVOLUME.ReadCloudSystemTypes` opens each checked ACC model once,
  lists its types, closes it, and stores them on the model entry
  (`CloudModel.SystemTypes`, fifth column of the saved list, `|`
  separated). `Calculate` refreshes them for free whenever it opens a
  cloud model. From the second run on the dropdowns fill with nothing
  opened. The **Read type names from ticked ACC models** button does the
  one-off read.
- The type cells accept **typed text** as well as a pick, so a name can
  be entered before any read has happened.

**System types are per model, not global.** Two models rarely name their
chilled water systems alike, so `ModelItem.SupplyType`/`ReturnType` carry
one pair each (stored on the `CloudModel` for an ACC model, so the choice
persists) and `Calculate` no longer takes a global pair. The models sit on
**two tabs** — *Open in Revit* (host and its links) and *ACC models* —
each a **`DataGridView`** edited in place: tick box, model name, two
`DataGridViewComboBoxColumn`s for the types, plus worksets on the ACC
grid only. There is no settings panel; the tables are the editor. Both
grids share the same column indices (0–3, worksets 4) so one set of
handlers drives both, dispatching on `sender`. Calculate takes whatever
is ticked on **both** tabs and produces one result table; the split is
presentational. With no model open the window starts on the ACC tab.

Both grids are **`FullRowSelect` + `MultiSelect`**. Ticking targets
`TargetRows`: the selection when two or more rows are picked, otherwise
the whole tab — and `UpdateActionLabels` rewrites the button captions
("Tick 3 selected" vs "Tick all") so the two modes are never ambiguous.
A "copy one row's types to the rest" button existed briefly and was
**removed at the user's request** — types are set per row, in the cells.
Do not reinstate it without asking. **Remove is the exception** and never falls back to the whole
tab, since an empty selection would otherwise wipe the saved ACC list;
removing more than one asks first, naming them.

Free text is allowed in the two type columns (`EditingControlShowing`
switches the editor to `DropDown` and `Validating` adds anything typed to
the column's `Items`, with `DataError` swallowed), which is what lets a
name be entered for a model the add-in has never opened. `EnsureItem`
must be called before assigning any cell value, or the combo cell rejects
it. A row missing either type shows red and blocks the run.

`ModelResult` carries `SupplyTypeName`/`ReturnTypeName`, shown as columns
in the window and the spreadsheet — `CHWEXPORT.Write` lost its two type
arguments accordingly.

**Only system types in use are listed.** `SystemTypeNamesIn` walks the
pipes (`OST_PipeCurves` + `OST_FlexPipeCurves`) and fittings
(`OST_PipeFitting`) actually in the model and collects their system type
names, rather than collecting every `PipingSystemType` element. A project
template defines dozens that were never drawn on, which buried the two
that matter. It deliberately reuses `SystemTypeNameOf` /
`FittingSystemTypeName` — the same functions `Measure` compares against —
so a name offered in the drop down always matches something. That costs a
full pipe pass per model at form load; the cheaper route of reading
`RBS_PIPING_SYSTEM_TYPE_PARAM` directly was rejected because a mismatch
with `Measure` would offer names that measure zero.

**A run can be stopped, mostly.** `CHWSTOP` (in `CHWCLOUD.cs`) is a
volatile flag plus a `Pump` action; `CHWForm.BeginRun` creates one with
`Pump = Application.DoEvents`, turns Calculate into Stop and disables
everything else, and `FormClosing` refuses to close mid-run so a
background document is never orphaned.

Two levels, and only one is guaranteed:

- **Between models — reliable.** `Calculate` and `ReadCloudSystemTypes`
  call `stop.Breathe()` (which pumps) then check `stop.Stopped` before
  each model. `Result.Stopped`/`NotMeasured` drive a red "STOPPED - N
  model(s) were not measured" line under the totals, so a partial figure
  cannot be mistaken for a whole one.
- **Mid-open — unverified.** `CHWCLOUD.Open` subscribes to
  `Application.ProgressChanged` and calls `e.Cancel()` when `e.Cancellable`
  and the flag is set (both members compile-verified on 2022 and 2027).
  **Whether Revit ever reports a cloud download as cancellable is
  unknown** — it has never been run. If it does not, a large download
  runs to completion and only the between-models check bites. A cancelled
  open deliberately does *not* fall through to the other region, or Stop
  would start a second download.

`DoEvents` is only ever called between models, never inside a Revit
operation, and every other control is disabled while it runs.

**Worksets, per ACC model.** `CloudModel.Worksets` is All (default) or
Editable. Editable is resolved *before* the open:
`WorksharingUtils.GetUserWorksetInfo(path)` reads the workset list off a
**closed** model, those whose `Owner` matches `app.Username` are opened,
the rest closed (all four API calls compile-verified on 2022 and 2027).
Only ACC models offer the choice, since they are the only ones the add-in
opens; local rows show a dash.

Editable **undercounts on purpose** — it loads your worksets and nobody
else's. It is warned about when chosen, the result row is coloured and
suffixed `(editable worksets only)`, and that note follows into the
spreadsheet. Owning no worksets in a model means it measures nothing at
all; that is the honest meaning of the setting and is deliberately not
silently upgraded to All.
- The open uses `DetachAndPreserveWorksets` first, falling back to
  `DoNotDetach` for non-workshared models. Detaching keeps the run from
  touching the central file.
- ACC entries persist in `%AppData%\ATKINSMEC\acc_models.txt`,
  tab separated: name, region, project GUID, model GUID, system types
  (`|` separated), worksets, supply type, return type. The region column
  is empty until the first successful open fills it in. Files written by
  an earlier build are shorter; the missing columns take their defaults.

**Models are picked, not typed.** `CHWCLOUD.ScanCache` reads Revit's own
collaboration cache at
`%LocalAppData%\Autodesk\Revit\<version>\CollaborationCache\<account>\<project guid>\<model guid>.rvt`
— both GUIDs are in the path, and the readable ACC path comes from
`BasicFileInfo.Extract(file).CentralPath` (compile-verified on 2022 and
2027). `CHWCACHEForm` lists what it finds; `CHWCLOUDForm` is now only the
fallback for a model never opened on this machine. On this machine the
scan found 15 models across 3 projects, all named.

Cautions: the cache layout is **undocumented** and could change between
releases, so every step of the scan is defensive and a failure yields an
empty list rather than an error. Duplicates across Revit versions are
collapsed, newest cache winning. `BasicFileInfo.Extract` is skipped
silently for a file locked by a running Revit — that model still lists,
under its GUID.

**Zero-document state.** CHW VOLUME is the one button that works with no
model open, via `CHWAvailability` (`IExternalCommandAvailability`) on the
button and null-document handling through `CHWCommand`, `CHWForm` and
`CHWVOLUME`. In that state the model list holds ACC entries only and the
system type names must be typed into the table, since nothing is open to
list them from.
The other three buttons deliberately still require a document.

---

### ALIGN TERMINALS — built 2026-09-02

Fifth button. Moves this model's air terminals onto the nearest air
terminal in a linked architectural model, in plan and in height.
`ALIGNTERMINALS.Plan` reads and reports; `Apply` moves, inside one
transaction opened by the modal form, so the whole alignment is a single
Ctrl+Z.

Decisions worth keeping:

- **Matching is plan distance only.** The MEP elevation is exactly what
  is being corrected, so including Z in the search would let a badly
  levelled terminal match a diffuser on another floor.
- **Each linked terminal is claimed once**, greedily nearest first.
  Without that, two MEP terminals near one diffuser both get dragged onto
  it and end up stacked.
- Candidates are bucketed into a grid of the search radius, so each
  terminal only examines nine cells rather than every candidate.
- Link points go through `link.GetTransform().OfPoint()`, so a rotated or
  displaced link is handled.
- After each move the result is re-read. A face-hosted terminal takes its
  height from its ceiling, so it can move in plan but not vertically —
  that is reported as "aligned in plan; elevation held by its host"
  rather than counted as a success. **Rehosting is deliberately not
  attempted**: it would silently change which room a terminal belongs to.

**Tested:** the matching algorithm has a standalone harness at
`scratchpad/aligntest` (mirrors the grid and greedy assignment). Seven
checks pass, including grid-versus-brute-force agreement over 6000 probes
on negative coordinates, and cell-key uniqueness across 4000x4000 cells —
the key packs two signed indices into one long, which is where negative
model coordinates would bite. Keep the harness in step with
`ALIGNTERMINALS` if the algorithm changes.

**Modeless, via `ALIGNEVENT`.** It began modal and was converted at the
user's request, so a terminal could be shown in the view from the list.
Nothing in `ALIGNForm` touches the API any more: it raises
`Refresh`/`Find`/`Pick`/`Apply`/`Zoom` and the handler calls back into
`ShowLinks`/`ShowMatches`/`SetPicked`/`Applied`/`ReportError`, which run
on Revit's thread. `ALIGNCommand` holds form, handler and event in
statics; `OnFormClosed` clears the static so a second click opens a fresh
window (and an already-open one is just activated).

The conversion **deleted `ALIGNSTATE`** and the close-pick-reopen loop it
existed for. With a modeless window `PickObjects` runs inside the event
handler and the window simply stays open, so nothing has to be carried
across a round-trip.

**A Show button per row** (`DataGridViewButtonColumn`, handled through
`CellContentClick`) selects the terminal and frames it —
`uiDoc.ShowElements` first, then `ZoomAndCenterRectangle` on its bounding
box with a 2 ft margin, since `ShowElements` alone frames far too tight.

**Scope: whole model or a picked set.** `Options.OnlyTerminals` (empty =
everything). Terminals already selected when the button is pressed are
collected by the `Refresh` request. Only `OST_DuctTerminal` is pickable
(`ALIGNSELECTIONFILTER`); Escape keeps the previous selection. Rows that
actually moved turn green and read "moved".

**Not tested:** everything touching Revit. No terminal has ever actually
been moved. First real test should be a handful of picked terminals in one room with
the search distance low, checking that ducts left behind are dealt with —
the add-in moves terminals only, not the ductwork connected to them.

### SHEET QA — built 2026-09-03, framework only tested

Sixth button. QA of what the sheets say, with the option to mark the
findings on the drawings.

**Running the checks never writes.** The only thing that does is
`QAMARK`, so the command is `TransactionMode.Manual`. Keep the *checks*
read-only — that is why the window is safe to leave open while working.

**The framework is the point**, since more checks are coming. A check is
one class implementing `IQACHECK` plus one line in `QACHECKS.All()`; the
window, the Show button, severity sorting and the Excel export all pick
it up with no further change. `QACONTEXT` is built once per run (sheets,
the views placed on them, a view-to-sheet map) so ten checks do not each
walk the sheet list. A check that throws becomes a warning row rather
than costing every other check its results.

**Scope** is every sheet, picked sheets, or the active view. Only views
actually *placed on a sheet* are examined — an unplaced view is not going
out. The active-view option is the exception, for working on one drawing.

**`QAFLOWCHECK` — tags reading zero flow.** Works from tags, not
elements: an untagged terminal with no flow is a modelling matter, but a
tag printing 0 is on a drawing. Deliberate consequences:

- The same element tagged on two sheets is reported **twice** — both
  sheets are wrong.
- Tags into a **linked** model are skipped (`GetTaggedLocalElementIds`
  returns local only); that flow is not this model's to fix.
- Air terminals, ducts, flex ducts, duct fittings and accessories use
  `RBS_DUCT_FLOW_PARAM`; pipes, flex pipes, pipe fittings and accessories
  use `RBS_PIPE_FLOW_PARAM`. Anything with no flow parameter is passed
  over rather than reported.
- The reported value comes from `AsValueString()`, so it reads in the
  project's own units.

API compile-verified on 2022 and 2027: both flow parameters,
`IndependentTag.GetTaggedLocalElementIds`/`TagText`/`OwnerViewId`,
`ViewSheet.GetAllPlacedViews`/`SheetNumber`, `UnitTypeId.LitersPerSecond`.

**Marking findings on the drawings** (`QAMARK`) colours the offending
**tag** red in the view it was found in — `QAFLOWCHECK` puts the tag id
on the finding, since the tag is what is wrong on the sheet — using
`View.SetElementOverrides` (compile-verified on 2022 and 2027, along with
`AreGraphicsOverridesAllowed`). Chosen over revision clouds and detail
lines deliberately: it creates nothing, so there is nothing to hunt down
afterwards, and clearing restores the view exactly.

**The override is saved with the model.** It is not a temporary view
mode. `QAForm` keeps the marked (view, element) pairs so Clear is exact,
and Clear also sweeps everything currently in the list — so mark, re-run,
Clear still tidies up. If someone marks, closes Revit and returns, they
must re-run the same check before clearing or the add-in does not know
what to clear. A blunt "clear every override in these views" was
rejected: it would wipe overrides someone set deliberately.

**Analysis Visualisation markers (`QAAVF` + `QAWATCH`)** — added at the
user's request after they asked for a live option that is not filters.
A `SpatialFieldManager` per view carries one primitive of points, one
point per finding, at the tag head; an `AnalysisDisplayStyle` named
"ATKINSMEC - zero flow" draws them as red markers with the legend off.

Why it is the least intrusive of the three: it writes nothing onto the
elements, adds no filters (so no fight with view templates), and
`manager.Clear()` removes it completely. It draws over the sheet rather
than restyling it.

**AVF does not recompute itself**, which is the whole reason `QAWATCH`
exists: it subscribes to `Application.DocumentChanged` and raises a
`Repaint`. Two things about that handler are load-bearing —

- It is **read-only**. Nothing may be written from inside
  `DocumentChanged`; all it does is raise the external event.
- It must **not react to its own writes**, or paint triggers repaint
  forever. Guarded twice: by transaction name (`QAWATCH.PaintTransaction`
  / `ClearTransaction`) and by the `Painting` flag set around the
  transaction.

It only repaints when something flow-related changed — a terminal, duct,
pipe, fitting, accessory, one of their tags, or an `MEPSystem`. Any
deletion counts, since a deleted element cannot be examined.
`QAWATCH.Stop()` is called from `OnFormClosed`.

Unlike the filters, this needs the add-in loaded to stay live.

API compile-verified on 2022 and 2027, after two wrong guesses corrected
by the compiler: it is `UpdateSpatialFieldPrimitive` (not
`SetValueAtPoints`) and it needs a schema index from
`RegisterResult(new AnalysisResultSchema(...))`; `NumberOfResults` does
not exist. Verified working: `GetSpatialFieldManager` /
`CreateSpatialFieldManager`, `AddSpatialFieldPrimitive`,
`FieldDomainPointsByXYZ`, `ValueAtPoint`/`FieldValues`, `Clear`,
`AnalysisDisplayStyle.CreateAnalysisDisplayStyle` with
`AnalysisDisplayMarkersAndTextSettings`, `View.AnalysisDisplayStyleId`,
`IndependentTag.TagHeadPosition`, and on the watcher side
`DocumentChangedEventArgs.GetTransactionNames`/`GetModifiedElementIds`/
`GetAddedElementIds`/`GetDeletedElementIds`.

**A view-filter version (`QALIVE`) was built and then removed at the
user's request**, because it coloured the ducts, pipes and terminals
rather than their tags. That is not a bug to fix: a `ParameterFilterElement`
rule matches an element's **own** parameters, and a tag carries no copy of
its host's flow, so no filter can select "tags whose host reads zero".
Do not reintroduce filters as a way of colouring tags. If element
colouring is ever wanted in its own right — it would also catch untagged
zero-flow elements — it belongs as a separate check, not as the marking
for this one.

Marking respects a multi-row selection, like the other grids here.

**Findings are ordered by sheet**, then severity, then view, then check,
then element — a QA list is worked through one drawing at a time.
`QACHECKS.CompareSheetNumbers` sorts sheet numbers **naturally**, so M-9
comes before M-10 and leading zeros are ignored (M-007 = M-7); a finding
with no sheet sorts last. The same comparer orders the sheet checklist,
so the two read alike. `QAForm` shades alternate sheet groups
(234,239,247) so the boundaries show without separator rows, and the
summary names the four sheets carrying the most.

The comparer has a standalone harness at `scratchpad/sorttest` that
lifts it verbatim out of `QACHECK.cs` — 12 checks including
antisymmetry over every pair, since an inconsistent comparer makes
`List.Sort` throw rather than merely mis-order.

**Checks are grouped onto tabs (2026-09-04).** `IQACHECK` gained
`Group`; `QAGROUPS` names them. `QASETTINGS` carries whatever a check
needs from the window, so `Run(doc, context, settings)` stays one
signature however many settings accumulate.

**Each tab runs on its own button (2026-09-04, at the user's request).**
The shared Run at the bottom of the window is gone; `tabFlow` and
`tabGrids` each carry their own. The group was renamed
`QAGROUPS.Presentation` to `QAGROUPS.Grids` at the same time - all three
checks in it are grid checks, and the user calls it the grids tab. A
non-grid presentation check later gets its own group and tab.

**Then the whole window was rebuilt as two pages (2026-09-04, same
day, user's choice between two offered layouts).** The tabs sit at the
top and each is a complete job: its own "What to check" scope, its own
checks and settings, its own Run, and **its own findings table**. The
two never have to agree - flowrate over every sheet while the grids are
checked on the view open now is a normal thing to want - and running one
leaves the other's list untouched.

`QAForm.Page` is one tab's worth of window: the controls, the checks,
the findings and the scope description. Every method that acts on a tab
takes a `Page`, so the logic is written once. A third tab is a tab page
in the designer plus one more `Page` object.

Three things worth knowing about that rewrite:

- **The findings tables get their columns in code** (`QAForm.Columns`),
  not from the designer. Two hand-maintained sets of eight columns would
  drift; one method cannot.
- **`_running` remembers which page asked.** `ShowFindings` comes back
  from `QAEVENT` with no idea which table it is for, and guessing from
  the selected tab would put the answer in the wrong place if the user
  switched tabs while it ran.
- **Do not name a form helper `Load`, `Text`, `Show` or `Update`.** They
  are inherited members; a helper named after one silently hides it and
  `this.Text = ...` or the designer's `this.Load +=` stops meaning what
  it says. `Fill`, `Columns`, `TextColumn`, `Buttons`, `Explain` were
  chosen to dodge that.

**Grids tab: three grid checks, all untested.**

- `QAGRIDLINKCHECK` — grids against the model they were copied from.
  Matches by name, compares the grid **line in plan**, not the drawn
  ends: a grid is the same grid however far it is drawn and at whatever
  level. Reports moved / here-but-not-in-link / in-link-but-missing.
  These findings carry **no view or sheet**, since a grid belongs to the
  model, so they sort to the end under "(no sheet)" and cannot be
  marked (marking needs a view) — `QAMARK` and `QAAVF` already skip
  `ViewId == 0`.
- `QAGRIDEXTENTCHECK` — ragged ends. The standard is taken from the
  grids themselves: the furthest reach among **parallel** grids in that
  view is treated as the extent covering the drawing, and anything short
  of it by more than the tolerance is reported with which end and by how
  much. Vertical and horizontal families are judged separately. Plan
  views only.
- `QAGRIDBUBBLECHECK` — bubble on the wrong end, at both ends, or
  missing. The wanted corner is chosen in the window.

`QAGRIDS` holds the shared geometry. **Top/bottom and left/right are
read in the view's own frame** (`view.RightDirection` / `UpDirection`,
dot-producted against the grid ends), so a rotated plan is judged as
drawn rather than by world axes. A grid within 3x of square is called
skew and the bubble convention is not applied to it.

Grid API compile-verified on 2022 and 2027: `Grid.Curve`,
`GetCurvesInView(DatumExtentType.ViewSpecific, view)`,
`GetDatumExtentTypeInView`, `HasBubbleInView`, `ShowBubbleInView`,
`HideBubbleInView`, `DatumEnds`, `IsHidden`.

**`QAGRIDFIX` puts the findings right (2026-09-04).** The user asked for
the grids tab to fix what it reports, so "Put the grids right" sits
beside Run on that tab. It is the only thing in the add-in that changes
the drawing rather than the way it is coloured, so it asks first, listing
what it is about to do in the words of the drawing, and it is one
transaction - one Ctrl+Z.

- Driven by **`QAFinding.FixKind`**, a string the check sets
  (`grid-extent` / `grid-bubble` / `grid-moved`), not by a switch on the
  check's display name - renaming a check must not quietly break fixing.
- **Only three findings carry one.** A grid the link does not have, and
  one the link has that is missing here, are deliberately left: drawing
  and deleting grids are not decisions for an add-in. The count of those
  is reported as skipped, with the reason.
- **Everything is recomputed from the model at fix time.** The findings
  may be minutes old; a list left open while somebody moved a grid must
  not apply a stale correction.
- **The extent type is switched to view-specific first.** Most grids
  carry a 3D extent shared by every view and `SetCurveInView` will only
  take a view-specific one. `SetDatumExtentType(End0/End1, view,
  ViewSpecific)` is what dragging the grip does, and it leaves the
  grid's other views untouched. Without this the fix would skip nearly
  every grid in a real model - the failure would have looked like the
  API not working.
- **A stretch is parameterised along the grid's own line**
  (`t = (target - s0) / (s1 - s0)`, then `Line.CreateBound`), so it
  cannot shift the grid sideways however the view is rotated.
- **A drifted grid moves in plan only**, never up or down a level, and
  pinned grids are skipped and said so.
- `QAEVENT.DoFixGrids` **re-runs the checks inline** after committing,
  so the list shows what is still wrong. It must not raise another
  request instead: `Execute` sets `Action = None` in its `finally`, so a
  request raised from inside a handler arrives with nothing to do. That
  trap applies to all four modeless add-ins.

Fix API compile-verified on 2022 and 2027: `SetCurveInView`,
`SetDatumExtentType`, `GetDatumExtentTypeInView`, `ShowBubbleInView`,
`HideBubbleInView`, `ElementTransformUtils.MoveElement`, `Grid.Pinned`.

**None of the three has been run, and neither has the fix.** The
likeliest thing to be wrong is
the skew threshold or the extent tolerance producing noise on a real
drawing; both are cheap to adjust. The bubble check is the one most
worth watching, since End0/End1 to top/left is inferred geometry rather
than something Revit states.

**First real-model bug, found by the user and fixed 2026-09-04:**
ticking the markers box threw *"Revit is currently not within an API
context"*. `QAForm.chkMarkers_CheckedChanged` was calling
`QAWATCH.Start`, and `app.DocumentChanged += ...` is itself an API call —
a form's own event handler is not an API context. The subscription now
happens inside `QAEVENT.DoPaint`, and the unsubscribe inside the same
method when switching off. `QAWATCH.Disarm()` (a plain flag, no API) is
what `OnFormClosed` calls, since a closing form cannot make API calls;
it then raises `Unpaint` so the real unsubscribe and the clearing happen
in context. `QAEVENT` remembers `_painted` so clearing works with no
window left. Two more instances of the same mistake were swept up at the
same time: `_doc.Title` read from the export handler, now cached as
`_docTitle` in the constructor.

**Everything else here is still untested:** both pages have been
rendered from the real designer file (scratch WinForms host at
`scratchpad/formpreview`, which compiles `QAForm.Designer.cs` against a
stub partial class carrying the event handlers and a `Fill()` - no Revit
needed, and the fastest way to check a layout change); no tag has ever
been examined and nothing has been marked. **Put the grids right is the thing to try
first, and on a copy** - it is the only writer of geometry in the suite.
Worth watching on a real model: whether the extent standard picks a
sensible furthest grid rather than one stray long one, and whether
switching grids to 2D extents is acceptable house practice.

### Duct disconnection page - added 2026-09-04, never run

Third tab, at the user's request: find disconnected takeoffs and taps
and **join them back up** - to their own system, or to the nearest duct
when they have no system.

**What counts as disconnected:** an END connector in the HVAC domain
with nothing on the other side. That is Revit's own definition of a
disconnect. Ducts, flex, fittings, accessories, air terminals and
mechanical equipment are all walked.

**Four checks divide the open ends between them**, so nothing is
reported twice - a tap (`QADUCTTAPCHECK`), anything else with a duct in
reach (`QADUCTBRANCHCHECK`), two ends meeting and facing each other
(`QADUCTJOINCHECK`), and the rest (`QADUCTSTRANDEDCHECK`, a warning,
never fixed since an open end with nothing near it is usually a stub
left on purpose).

**The scope is the model, not sheets.** A disconnected duct is wrong
whether or not anyone has put it on a sheet, so `QASCOPE` gained
`WholeModel` and `PickedLevels` and the page offers levels. `QACONTEXT`
now carries the `Scope` so a model-wide check can read its own filter,
and a `Shared` dictionary so the four checks share one walk of the
model rather than each collecting every duct.

**Ducts are bucketed into a coarse 3D grid**, cells twice the search
reach so only the 27 around a point are ever measured. The cell key
packs three signed indices into one long - the exact shape of the bug
`aligntest` caught before - so there is a harness at
`scratchpad/ducttest` mirroring it. Eight checks pass, including grid
versus brute force over 16,000 queries in four coordinate regions, one
of them entirely negative, and the meeting-ends sweep. A duct too long
to sample at cell spacing would break the 27-cell guarantee, so it goes
on an always-measured list instead of being quietly missed.

**`QADUCTFIX` is the only writer in the suite that creates and deletes
elements.** Each change is its own `SubTransaction` inside the one outer
transaction, so what Revit refuses rolls back alone. Joins first, then
taps, then branches.

- **A hanging tap is replaced, not repaired.** A tap's main side has no
  connector to rejoin - it is hosted on the body of the duct - so the
  branch it still holds is remembered, the tap deleted, `doc.Regenerate`
  called, and `NewTakeoffFitting` asked for a fresh one. A tap with
  nothing on either side is left alone and reported.
- **`QAFinding.FixData`** carries the connector id, because a
  `Connector` cannot be held across a transaction. The fix walks the
  network afresh and matches findings to it by (element, connector);
  an end somebody joined since the check ran is counted as `Already`,
  not connected twice.
- The system rule is the user's: an end with a system joins that
  system's nearest duct; an end without one joins the nearest duct of
  any system; an end whose own system is out of reach does nothing
  unless the fall-back box is ticked.

**API compile-verified on 2022 and 2027**, after three wrong guesses the
compiler caught: `Connector.Direction` is a `FlowDirectionType`, not a
vector (the facing is `CoordinateSystem.BasisZ`); `PartType` has no
`Tap` member (only `TapPerpendicular`, `TapAdjustable`,
`SpudPerpendicular`, `SpudAdjustable`); and `IntersectionResult` has no
`XYZDistance` (use `Curve.Distance`). Verified working:
`Document.Create.NewTakeoffFitting(Connector, MEPCurve)`,
`Connector.ConnectTo`/`DisconnectFrom`/`IsConnected`/`Id`/`Owner`/
`AllRefs`/`MEPSystem`, `ConnectorManager.Lookup`,
`MechanicalFitting.PartType`, `Duct.MEPSystem`, `FlexDuct.MEPSystem`,
`SubTransaction`, `Document.Regenerate`, `ElementMulticategoryFilter`.

**Nothing here has been run.** The likeliest things to be wrong, in
order: whether `NewTakeoffFitting` accepts the geometry it is given
(Revit refuses when the end does not face the duct, or the duct type
has no tap in its routing preferences - caught and reported per
finding); whether the 1500 mm default reach is noise or too tight on a
real model; and whether replacing a hanging tap loses anything worth
keeping. **Try it on a copy.**

### FCU flowrate page - added 2026-09-04, never run

Fourth tab, at the user's request: import the FCU schedule from Excel,
compare each unit's supply duct against it, and **correct the ones that
are out by scaling their air terminals**.

**`FCUSHEET` is Revit-free and tested against the real file.** Harness
at `scratchpad/sheettest` compiles the actual `FCUSHEET.cs` against
EPPlus and runs it on the user's `FCU_Schedule.xlsx`: 12 checks pass -
18 units read, right flows, header and notes rows excluded, 13 ducted
and 5 free discharge classified correctly, the SFP (W/l/s) column not
mistaken for the flow, and the loose name matching behaving.

- Columns are found **by what the headings say**, not where they are, so
  another office's layout still reads.
- A row counts as a unit when it has **both a name and a numeric flow**,
  which is what skips the units row and the notes at the bottom without
  knowing where they are.
- `IsFlow` excludes `w/l` and `sfp` on purpose: the specific fan power
  column sits right beside the flow column and says "W/l/s".
- Free discharge is spotted by reading the **whole row** for the words,
  since which column carries them varies.

**Name matching keeps letters and digits only.** `FCU-02`, `FCU -02` and
`fcu_02` are one unit; `FCU-2` and `FCU-02` stay different, because an
office that means those to be the same can rename one, and silently
merging them would be worse.

**Which parameter holds the unit number is picked in the window**, not
assumed. `QAFCU.ParameterNames` walks up to 50 pieces of equipment and
offers Mark, Type Name and every string parameter they carry, instance
and type. This avoided a blocking question about the user's families.

**`QAFCU` walks out of the supply connector** (`FlowDirectionType.Out`)
and follows the ductwork, stopping at each air terminal and at any other
piece of mechanical equipment, so one unit never reaches into another's
branch. The first `MEPCurve` off the supply connector is the duct the
schedule is compared against.

**`QAFCUFIX` writes the terminals, never the duct.** Revit works a
duct's flow out from the terminals it feeds, so writing the duct is not
possible and would not hold. Each unit's terminals are scaled by **one
ratio** so they keep their shares - 40/40/40/40 against a schedule of
200 all become 50; 10/10/10/130 become 12.5/12.5/12.5/162.5. An even
split would throw away whoever balanced that branch.

- A unit with **no terminals, terminals reading zero, or a read-only
  Flow** has no ratio to scale by and is reported, not guessed at.
- The write is `RBS_DUCT_FLOW_PARAM` in `UnitTypeId.LitersPerSecond`,
  the same as AUTOVENT's `WriteFlow` - which is the one part of this
  that has run in a real model before, in another add-in.
- Each unit is its own `SubTransaction`; a terminal that refuses rolls
  that unit back whole rather than leaving it half-scaled.
- Tolerance is the percentage OR the litres, whichever is larger: the
  percentage stops small units being judged too harshly, the litres stop
  large ones being judged too finely.

**A trap worth remembering:** the window raises ONE external event at
startup. `ExternalEvent.Raise()` called twice in a row runs once, and
the second `Action` simply overwrites the first - so reading the
equipment's parameter names rides along inside `DoRefresh` rather than
being its own request.

**First real-model report, 2026-09-04:** the check said *"a terminal's
flow is read-only here"*. The user's families have **two duct
connectors, only one used** - and that is the answer, not a workaround:
where a family has more than one connector Revit offers **no writable
instance Flow at all**, because the air belongs to a connector.

Fixed by giving `QAFCU.Write` a second route: the instance parameter
where the family offers one, otherwise `Connector.AssignedFlow` on the
connector actually carrying the air (`QAFCU.Used` - the connected one
where only one of two is used). `QAFCU.LitresOf` reads the same way,
falling back to `Connector.Flow`, since a two-connector family usually
leaves the instance parameter at nothing.

API pinned down by compiling on 2022 and 2027, after three wrong guesses:

- `Connector.AssignedFlow` - **settable**. This is the one.
- `Connector.Flow` - readable only.
- `Connector.AssignedDuctFlowConfiguration` - **read-only**, of type
  `Autodesk.Revit.DB.Mechanical.DuctFlowConfigurationType`
  (`Preset` / `Calculated` / `System`). Only `Preset` can be given a
  flow, and the API cannot change it - it is a family setting.
- `Connector.DemandFlow` - **does not exist**.
- `FlowConfigurationType` and `DuctFlowConfigurationType` as a bare name
  in `Autodesk.Revit.DB` - **do not exist**; the enum needs
  `using Autodesk.Revit.DB.Mechanical`.

The finding now groups locked terminals **by reason** and names element
ids for each, so "cannot be written" says whether it is a Calculated
connector, a missing parameter, or something else. An unactionable
error message was the real defect in the first version.

**Still not run against a model.** Remaining unknowns: whether the FCUs
are `OST_MechanicalEquipment`; whether the user's connectors are Preset
(if Calculated or System, nothing can write them and the finding will
now say so); and whether walking out of the supply connector reaches the
terminals where an FCU feeds a plenum or shared riser. **Try it on a
copy.**

**Next checks to add** — the user said more are coming but has not named
them. Worth asking about: untagged elements that should be tagged, tags
that have come unhosted or moved off their element, duplicate tags on one
element, views on sheets with no annotation at all, elements outside the
crop, unconnected duct/pipe ends, sheets with placeholder titles or
missing revisions.

## AUTOSYNC - added 2026-09-11, never run in Revit

Seventh button, at the user's request: sync every open model on a timer,
minimum five minutes, on/off asked each time Revit starts, and save
locally instead when the connection is gone.

**Every API call compiled on 2022 and 2027 first time** - no wrong
guesses this round. Verified: `Document.SynchronizeWithCentral(
TransactWithCentralOptions, SynchronizeWithCentralOptions)`,
`SynchronizeWithCentralOptions.Comment/SaveLocalBefore/SaveLocalAfter/
Compact/SetRelinquishOptions`, `RelinquishOptions(bool)` with its five
flags, `Document.Save()` and `Save(SaveOptions)`,
`Document.IsWorkshared/IsModelInCloud/IsDetached/IsModified/IsReadOnly/
IsModifiable`, `GetWorksharingCentralModelPath` +
`ModelPathUtils.ConvertModelPathToUserVisiblePath`,
`ControlledApplication.ApplicationInitialized` with
`ApplicationInitializedEventArgs`.

**What is tested, and it is more than usual.** `SYNCNET` and
`SYNCSETTINGS` have no Revit in them, so `scratchpad/synctest` runs the
real files: 14 checks pass, including the five-minute floor surviving a
hand-edited settings file asking for 2, rubbish in the file falling back
to defaults, and - the important one - **a dead UNC path giving up in
4.0 s rather than the ~45 s it would otherwise take**. That timing is
asserted, not just observed, because it is the difference between an
add-in and a frozen Revit. Both windows rendered from the real designer
files at `scratchpad/syncpreview`.

**Decisions worth not re-litigating:**

- **On/off is not persisted.** Asked every session on purpose. Only the
  interval, relinquish, comment and host are remembered.
- **Relinquish defaults to OFF.** Silently handing back everything
  somebody has borrowed, on a timer, while they are still working, is
  not a default.
- **`SaveLocalBefore` and `SaveLocalAfter` are both on.** If the sync
  itself falls over, the work is on disk rather than only in memory.
- **A model that has never been saved is skipped**, not saved to a
  chosen path. Picking a filename on somebody's behalf is not this
  add-in's business.
- **Closing the window does not stop it.** The timer is static in
  `SYNCSERVICE` and outlives every window.

**First real-model report, 2026-09-11: "the model was busy", every
model, every run.** AutoSync had never synced anything.

The cause was a guard of mine: `if (!doc.IsModifiable) skip`.
`Document.IsModifiable` means *a transaction is open on this document
right now* - Autodesk: *"Regardless of the mode a document is opened
with, the model can only be modified inside an open transaction."* The
handler has no transaction open and **must not** have one, because
`SynchronizeWithCentral` and `Save` both have to be called outside a
transaction. So the guard was false for every document on every run.

The guard is gone, with a comment in `SYNCEVENT` explaining why it must
not come back - it reads like a missing safety check, and it is the
opposite. `IsReadOnly` stays but its comment is corrected: it is dynamic
and mostly means "Revit is processing failures at this moment", not
"opened read-only".

Worth remembering as a method: the **`RevitAPI.xml` beside the NuGet
dll** settled both of these in seconds, and would have prevented the bug
outright. Compiling proves a member exists; only the docs say what it
means.

**Still not run successfully in Revit. The likeliest things to be wrong,
in order:**

1. **Revit's own dialogs during a sync.** `SynchronizeWithCentral` can
   put up conflict or worksharing prompts, and there is nobody there to
   answer them - the sync would sit waiting. Suppressing them needs a
   `DialogBoxShowing` handler, which is NOT written. This is the first
   thing to watch on a real workshared job.
2. Whether `ApplicationInitialized` is a comfortable place for a modal
   dialog on every Revit version - it should be, but 2022 and 2027 are
   six years apart.
3. Whether a big model's sync outlasts the interval often enough for the
   `Busy` guard to matter (it is there, and logs when it skips).

**Try it on a copy, on a test central, before anybody depends on it.**

### CHW pipe size page - added 2026-09-12, never run in Revit

Fifth tab, at the user's request: check chilled water pipe and fitting
sizes against their flow, using the supplied ASHRAE 90.1 sizing sheet as
the reference.

**The table is built in (2026-09-12, user's request).** No import, no
file to go missing, every machine checking the same numbers:
`CHWSIZESHEET.Builtin()` carries the office's own ASHRAE 90.1 / ASTM A53
sheet - seventeen sizes DN20-DN600 - at the workbook's full precision.
The constants were GENERATED from the workbook, never typed.

`CHWSIZESHEET.Read()` stays but is unreachable from the window. That is
deliberate and worth defending: a wall of seventeen rows of
`0.18977330777945564` cannot be reviewed by eye, so `chwsizetest` reads
the real workbook through it and asserts **every built-in row still
matches, to 1e-9**. Delete the reader and the constants have nothing
checking them. To revise the standard: edit the workbook, regenerate,
run the harness.

17 checks pass - the row-for-row comparison, plus every boundary in the
lookup (2.00 L/s fits DN50, 2.20 needs DN65; 12.00 fits DN100, 12.50
needs DN150), a flow past the end of the table, and a guard that the
outer diameters (26.7 mm is DN25's OD) never match as sizes.

**It caught a real bug, and the same bug in FCUSHEET.** Reading numbers
with `ExcelWorksheet.Cells[r,c].Text` gives the FORMATTED string, not
the value: the DN20 limit of 0.1898 came back as "0.19" and DN600's
626.9788 as "627". Harmless at those two, but the limits then depend on
how somebody formatted their sheet - a column shown to no decimals would
make DN20's limit 0 and flag every pipe in the job. Both readers now
take `.Value` and fall back to parsing text only when the cell is not
numeric.

**A pipe is one judgement; a fitting is one per CONNECTOR.** A tee has
three sizes and three flows, so judging it as a single thing would be
meaningless. Only round connectors are judged - the table is about DN.
Zero-flow runs are skipped entirely: no flow means no opinion, and a dry
branch would otherwise look oversized at every size there is.

**Supply and return are picked separately (2026-09-12, user's
request)** - two checked lists, the same shape CHW VOLUME already uses.
Both offer every `PipingSystemType` in the model and either may hold
more than one, since a job with primary and secondary circuits has
several per side.

The sides carry the same flow, so the sizing is identical either way.
The split earns its keep elsewhere: `QACHW.Run.Side` puts "Supply" or
"Return" at the front of the System column, so the list groups by side -
which is how a chilled water QA list is read - and leaving one list
empty checks the other side alone.

A guess is pre-ticked only where the name says BOTH chilled water and
which side. A type called just "Chilled Water" is left unticked on both:
guessing a side there would be worse than asking. Without any pick the
checks say so rather than judging every pipe in the building.

**Oversizing is a separate, tickable check** because it is noisy by
nature - a branch at its practical minimum size is oversized for its
flow nearly everywhere. Two settings keep it quiet: how many sizes over,
and from what size upwards to bother (default 2 sizes, 50 mm).

**Clearing Additional Flow added 2026-09-12, at the user's request**
(`QACHWFLOW`). `RBS_PIPE_ADDITIONAL_FLOW_PARAM` is Revit's "Additional
Flow" - flow typed onto a pipe on top of what the system calculates.
Verified writable on 2022 and 2027.

It matters here because it is invisible on the drawing and inflates
what a pipe reports carrying, which makes the size check condemn pipes
that are actually fine. Clearing it is often the right first move before
reading the findings at all.

- **Counted first, acted on second.** Two requests: `SurveyChwFlow`
  reads only, then the dialog says how many pipes and how many L/s, and
  `ClearChwFlow` does it. Asking "clear the additional flow?" without a
  number in it is asking blind.
- **Follows the page's scope**, so per floor is "tick that level, press
  the button" - which is what the user asked for - and whole model is
  the default.
- **Only the ticked CHW system types.** A pipe in a system nobody
  ticked is left alone whatever it carries.
- The checks re-run afterwards, because every size judgement in the
  list was made against the inflated flow.

**Resizing added 2026-09-12, at the user's request** after the page was
first written report-only. The concern was stated once and the user
asked for it anyway, which is their call - it is recorded here because
the reasoning still matters, not to relitigate it: changing a pipe size
changes the hydraulics of everything downstream. The button and its
confirm dialog both say so plainly.

- **Pipes only.** `QACHWFIX` writes `RBS_PIPE_DIAMETER_PARAM`;
  `MEPCurve.Diameter` is read-only (compiler-checked on both ends).
  A fitting takes its size from the pipes it joins, so the pipes are
  set, the document regenerates and the fittings follow. Fitting
  findings carry no `FixKind` and say why in the Problem text.
- `Connector.Radius` and `FamilyInstance.Symbol` **are** settable - both
  verified - but forcing a connector radius against a family's lookup
  table makes a fitting that disagrees with itself, so neither is used.
- **Biggest first**, so a main grows before its branches. Each RUN is
  its own `SubTransaction` with a `doc.Regenerate()` inside it, so a
  refusal surfaces while it can still be rolled back alone.

**"The view open now" did nothing, 2026-09-16.** Reported on the CHW
page and true of the duct page too: `InScope` handled `PickedLevels` and
returned true for everything else, so that radio button checked the
whole project. `QASCOPE.ViewId` (new, filled by `BuildContext`) now
reaches `QACHECKS.Look`, which hands back a view-scoped collector -
Revit's own answer to "does this view show this pipe", crop and filters
included. A ViewSheet deliberately keeps the whole-model behaviour,
since collecting from a sheet returns viewports rather than pipe.

**Additional Flow over the whole project, 2026-09-15**, at the user's
request. It was scoped to the ticked CHW systems and the page's scope;
now it is every pipe in the document, with `Result.Outside` reporting
how many sit outside chilled water so the dialog can say so. `Survey`
and `Clear` dropped the `QASCOPE` parameter rather than keep one they
ignore, and `InScope` went with it.

**Reducers out of the list, 2026-09-15**, at the user's request and
plainly right: a transition is where the size changes, so its small end
reads as undersized on every single one. `QACHW.Fitting` skips them.
They are still resized, via `Touching` - only the reporting changed.

**"Fittings should be resized to the size of the larger connection",
2026-09-15.** A 40-40-40 tee on a DN100 main. The cause is that the
per-connector rule - each end to the pipe on its side - **cannot be
satisfied by an equal fitting**, and `M_Tee - Generic` is one (a single
`Nominal Radius` in its properties drives all three ends). Route 1 set
one end to 100, the verify saw the others no longer matching their own
pipes, and the whole thing rolled back every time. `TryBiggest` now
follows it: every connector to the largest pipe on the fitting. Only
attempted when the ends differ, so a genuine reducing tee still keeps
its reducing sizes.

**"Fittings are not resized along with the pipes", 2026-09-15.** The
button sent only findings with a `FixKind` - pipes - so `QACHWFIX` saw
`0 fitting(s) listed` every time, and a fitting was only reconciled if
it touched a pipe being resized. After the first pass fixes the pipes,
that reaches nothing: hundreds of fitting findings stay wrong and the
button looks inert. The whole selection now goes over and `Fix` sorts it
by asking the model what each element is.

**Resize by selection, asked for 2026-09-15** - and it already existed,
badly. `QAForm.Selected` took the picked rows only when MORE THAN ONE
was selected, so picking a single finding and pressing Fix quietly
resized the whole model. Changed to one row being a selection, which
also fixes Mark and the grid/duct/FCU fixes, and the CHW confirm now
says whether it is the picked rows or all of them.

**Equipment, same day.** Asked for the room to be taken from the fitting
at the far end rather than by moving the unit. Two changes: the walk can
now CROSS a tee (`Bring`) when everything hanging off it is a dead end
that slides along, and `Onward` follows the through path rather than
whichever neighbour came first - it used to wander down a branch while
believing it was going straight on, which is why the giver search kept
failing on branches to plant. A pipe running INTO equipment was already
allowed as the giver (only its near end moves); that is now said out
loud in the help.

**Two more from the log, 2026-09-15.** A cap (`Pipe Fittings`, ONE
connector) was being refused as immovable because `DeadEnd` wanted two
ends or three - it blocked a branch that could have moved freely. Now
only equipment stays put. And a slide that passed every check still drew
a **visibly bent elbow** (user screenshot: pipe 726658, 9 mm -> 13 mm at
DN32); `Sound` now also checks that no pipe has TURNED and that every
remade joint is square, since a crooked joint is still a joint as far as
`IsConnectedTo` is concerned.

The remaining refusal, 528238, is genuinely impossible: it sits between
two tees whose branches both run to Mechanical Equipment (497783 and
488842). Lengthening it means moving a WSHP. Refusing it is the right
answer and the log now says so in those words.

**The log settled it, 2026-09-15.** After two wrong guesses at a
distance, `QACHWLOG` went in and the first real log answered everything
in one read: **250 pipes checked, 0 gone, 0 too short** - the deletion
was already fixed by guarding the fittings pass - but **16 runs refused,
14 of them over ONE MILLIMETRE** (a 12 mm pipe predicted to end at 4 mm,
under a 5 mm floor I had invented). Two changes followed: the floor
dropped to `max(2 x ShortCurveTolerance, 2.5 mm)`, and the room is now
made inside the fittings pass (`QACHWROOM.Ensure`, called from
`QACHWFITTINGS.One` before the fitting is grown) rather than only in the
earlier pass over the runs, whose measurements are stale by then.

The log also settled a guess in the docs: **173 of 260 fittings took
`Connector.Radius` directly** and only 27 needed replacing, so "most
stock fittings are lookup-table driven and will refuse it" was wrong for
this office's families.

**Keep the log.** Three reports, three harnesses that all passed, and
the model disagreeing every time - the lesson is to read what the model
did before theorising about it.

**"Line is too short", 2026-09-15: the bigger fittings eat the pipe**
(`QACHWROOM`). Reported with a picture: a short jog between two elbows,
sized up, gone - Revit deletes a pipe it cannot keep, and the two elbows
were left against each other. The user asked whether the pipe could be
kept instead by moving one of the connections. It can, in the case that
matters:

- **Predict first, FROM THE FITTINGS.** Measure what the fitting takes
  up now (`LocationPoint` to `Connector.Origin`) and scale by the size
  ratio; a shape scales with its size. Floor of
  `max(4 x ShortCurveTolerance, 5 mm)`.
- **Weigh every pipe on those fittings, not the run's own pipes.**
  Reported a second time, with a picture: a short pipe between two TEES,
  deleted. A run stops at a tee, so that pipe was a run of its own and
  was not being resized - the mains either side were, which grew both
  tees, which ate it from both ends. The first version only ever looked
  at the pipes it was resizing, so nothing was watching it. `Eats`
  scales on the biggest size at the fitting, because a DN80 x DN15 tee
  is a DN80 tee with a small branch. `Verdict.Watched` + `Lost` re-check
  the neighbours afterwards too, since two runs sharing a tee each
  predict only their own half.
- **Slide the fitting away** from the doomed pipe, along that pipe's own
  line - asked for in those words at the second attempt. Unhook, move,
  rewrite the `LocationCurve`s, hook back up. **Nothing is deleted**:
  the first version cut the boundary fittings out and made new ones,
  which threw away whatever was written on them.
- **Three places the length can come from**, tried gently first on both
  ends: the first pipe IN LINE gives it up (`Leg`, up to 8 elements); a
  TEE slides along its main with its drop in tow (`Teed`, the drop
  bounded by `DeadEnd`); or a DEAD-END branch is pushed along bodily
  (`Branch`, up to 12). The tee case is the one the second report
  needed - a drop to nothing moves, a drop to an FCU does not.
- **Prove it afterwards.** `Sound` re-checks every joint the involved
  elements had, and every length, against the model - so it cannot
  trade one "Line is too short" for another or leave a disconnection.
  That was the explicit condition on the request.
- **Refuse the run otherwise**, unchanged - at a tee that would lose its
  branch, at equipment, at a ring, past the walk limits, or where the
  giver would itself go too short.
- **Behind a checkbox** (`ChwKeepShortPipes`, on by default), since it
  moves pipework.
- **Then let the model have the last word**: after the resize, every
  pipe in the run is checked for still existing and still being long
  enough, and the run is rolled back if not. The estimate is an
  estimate; Revit's answer is not.

Harness at `scratchpad/roomtest` - 22 checks, and it earned its keep
three times over: the far boundary fitting deleted and then moved; the
dead-end route calling its own seam a ring, which made it **dead code
that could never once have run**; and a walk coming back round to the
short pipe and taking it for a pipe in line with itself.

The third only showed up after the toy models were made realistic. **A
test model whose pipework just stops in mid-air is not a model** - it
let the branch route "succeed" by pushing a stub that would not exist in
a real job, and hid three real refusals. Terminate the far side at
equipment.

**Not run in Revit**: whether `MoveElements` leaves the unhooked ends
where the geometry says, and whether `ConnectTo` takes them again, are
the untested parts - inside one SubTransaction per run, with `Sound`
checking the result, so a failure leaves that run untouched.

**"The addin is not resizing the fittings", 2026-09-15: the one
assumption the page was built on, disproved in a real model**
(`QACHWFITTINGS`). The page wrote only the pipes, on the belief that a
fitting takes its size from the pipes it joins - which is exactly what
Revit does when a PERSON changes a size on the drawing, and is why the
belief survived review, the harnesses and the help text. Through the API
it does not happen, and there is no call that asks for it.

So the fittings are now driven, gentlest route first: set
`Connector.Radius` and **read it back** (a lookup-table family will take
the write and revert, so writing is not evidence); failing that, delete
and let Revit make a fresh one at the pipes' size. `MechanicalFitting.PartType`
picks which of the five creation calls; geometry is the fallback.
**A valve is never replaced** - its size is a type choice and that is
somebody's specification, not an add-in's decision.

Two things worth keeping: **straight is cos(2 degrees)**, because
`NewElbowFitting` refuses a shallower turn - a number taken from the API
docs rather than guessed, after the harness questioned an arbitrary
0.999. And **replacing loses whatever was written on that fitting**, so
the confirm dialog says so before anything is agreed to.

Harness at `scratchpad/fittingtest` - 19 checks. **Not run in Revit**:
whether a given family takes a radius, and whether the five creation
calls accept these connectors, are exactly the questions no harness can
answer. Each fitting is its own SubTransaction, so a refusal leaves that
one as it was, and the dialog reports resized / replaced / could-not
separately rather than claiming success.

**Disconnections, asked about 2026-09-15: avoided first, repaired
second** (`QACHWRUNS`). The user asked whether the add-in could fix, and
better avoid, the disconnections a size change causes. Both, and the
avoidance is worth far more:

1. **Whole runs.** The first version resized individual findings, which
   is itself the commonest cause: two pipes in one straight length, only
   one of them flagged, now needing a reducer between them that nobody
   wanted. `Group` crosses a direct joint and any fitting/accessory with
   exactly two equal piping ends - an elbow or union by definition, no
   family interrogation needed. It stops at tees (the flow divides), at
   transitions (deliberate) and at equipment. **Equipment was a real
   trap**: a pump has two equal connectors and the geometric rule alone
   would have crossed it, so the category is checked too.
2. **Routing preferences, read before attempting.**
   `PipeType.RoutingPreferenceManager` -> `Segment.GetSizes()` ->
   `MEPSize.NominalDiameter` gives the sizes a type can actually be made
   in. Not on the list means no segment and no fitting - refused with
   the size and type named. An unreadable type gives an empty list,
   treated as consent rather than a blanket refusal.
3. **Joints recorded and mended.** Every joint written down before,
   `Connector.IsConnectedTo` checked after, `ConnectTo` or
   `NewTransitionFitting` to put it back. Revit usually inserts its own
   reducer, which reads as "already connected" and is left alone.

API compile-verified on 2022 and 2027, no wrong guesses this time:
`RoutingPreferenceManager`, `RoutingPreferenceRuleGroupType.Segments` /
`Transitions`, `RoutingPreferenceRule.MEPPartId`, `Segment.GetSizes`,
`MEPSize.NominalDiameter`, `NewTransitionFitting` / `NewElbowFitting` /
`NewUnionFitting`, `Connector.IsConnectedTo`.

Graph harness at `scratchpad/runtest` - 14 checks on the grouping alone,
including a ring main (a cycle, which would hang a naive walk), a pump
not being crossed, a valve being crossed but noted, and the invariant
that no pipe ends up in two runs. **Still not run in Revit**: whether
`NewTransitionFitting` will take two coincident connectors is the one
thing the harness cannot answer. It is caught and counted per joint
rather than assumed, and the window warns when any is left open.
- The commonest refusal is a size missing from that pipe type's routing
  preferences - Revit then has no fitting to make the joint with. The
  note says exactly that.
- Both directions are fixable: undersized and oversized findings alike
  go to `SmallestFor(flow)`. Pick rows first to do only some.

**API compile-verified on 2022 and 2027, no wrong guesses:**
`Plumbing.Pipe`/`FlexPipe`, `Pipe.Diameter`, `RBS_PIPE_FLOW_PARAM`,
`RBS_PIPE_DIAMETER_PARAM`, `RBS_PIPING_SYSTEM_TYPE_PARAM`,
`PipingSystemType`, `Connector.Radius`/`Flow`/`MEPSystem`,
`Domain.DomainPiping`, `ConnectorProfileType.Round`.

**Not run in Revit. Likeliest things to be wrong:** whether
`Connector.Radius` on a pipe fitting is the nominal radius (the DN
match) or something else - if fittings come back unmatched in bulk, that
is why, and the "not in the table" finding will say so with a count;
and whether `RBS_PIPE_DIAMETER_PARAM` is nominal rather than inside
diameter in the user's pipe types. Both show up immediately as a pile of
"this size is not in the sizing table" rows. **Try it on a copy.**

## Open questions waiting on the user

- **The off-centre sprinkler.** A head sat high in its room rather than
  centred. Confirmed it was *not* a stale build. Added a **Free m²**
  column (room area after columns and wall obstacles are removed, red
  when obstacles took >10%). Next step is to compare **Area vs Free m²**
  for that room:
  - close to each other → obstacles are innocent, the room's real
    boundary extends beyond what was visible, head is correct;
  - Free m² much lower and red → the wall carving is eating the room, and
    the fix is to clip wall rectangles to the room boundary rather than
    subtracting them whole.
- **Coverage below 100%** in some rooms. Causes, in order of impact:
  grid cells that clip the room by under 12% get no head; heads dropped
  for wall clearance; wall obstacles splitting a room; heads that moved.
  A gap-filling pass now closes most of it. Note the coverage figure is
  **optimistic** — it uses a circle of radius half the cell diagonal,
  which circumscribes the true rectangular coverage.

## Deliberately reverted — do not re-add without asking

- **Head centring** (`CentreInCoverage`). Moved each head to the middle
  of the area it served. Worked (L-shape: 0.177 m → 0.659 m off the wall,
  coverage held at 100%, uniform rooms untouched), but the user asked for
  it to be removed. The **Free m² diagnostic was re-added** afterwards at
  their request; centring was not.

---

## Known gaps, by add-in

**AUTOVENT**
- Level names must match between sheet and model; the level checklist
  flags mismatches before a run.
- `MLCP` worksheet has **no area column** — its column J is headed "Room
  Number" but the sheet's own ASHRAE formulas use J as area. The room
  export therefore writes name, number and height but skips area. User
  has not said which way to resolve it.
- The three Tower worksheets have **no Room Number column** at all, so
  they cannot be imported. They *can* be exported into — the export adds
  a Room Number column when one is missing.
- Endcap-vs-bellmouth routing depends on the family name containing
  "Endcap". Other naming would need adding.

**CHW VOLUME**
- **Pipe accessories are excluded** (valves, strainers — category
  `OST_PipeAccessory`). One-line addition using the same connector
  method if wanted.
- Fitting volume is the sum of centre-to-connector legs, so it reads
  slightly **high on sharp bends** where the legs overlap at the corner.
- Matching is by exact system type name, so a link using a different name
  for the same service contributes nothing — visible as a model row
  showing 0 pipes and 0 fittings.

**SPRINKLERS**
- No beam or obstruction handling; the layout is a plan problem only.
- Coverage is sampled, not analytic.
- Hazard keywords cover English and German; extend
  `SPRINKLERRULES.Keywords` as needed. Unrecognised names fall back to
  Ordinary I and are highlighted yellow.

**DUCT INSULATION**
- Rules live in `%AppData%\ATKINSMEC\duct_insulation.txt`.
- A system type needs both a thickness > 0 and an insulation type before
  anything is placed; saving blocks the half-set case.

---

## Discussed, not built

- **CHW VOLUME with no local Revit at all** — Design Automation for Revit
  (APS). Bigger setup: APS app, app bundle as `IExternalDBApplication`
  with **no UI**, activity, workitems, JSON in and out. `CHWVOLUME` and
  `CHWEXPORT` would carry over nearly unchanged, since neither needs the
  form. Only worth it for unattended or server-side runs — the
  background open below already covers a person at a workstation.

## Installer — built 2026-09-02, tested

`build-installer.ps1` builds all six Release, stages them into
`installer/payload/<version>/` (minus `.pdb`), and writes two
interchangeable installers to `installer/output/`, both ~13 MB:

- **`ATKINSMEC-Setup.exe`** — one self-contained file, the normal way in.
  A net48 WinForms app (`installer/exe/`) carrying the add-in as a single
  embedded zip resource. net48 because it ships with every Windows 10/11,
  so nothing has to be installed first. Stored file by file rather than
  compressed the exe was 32 MB; as one zip it is 13.4. `/silent` and
  `/silent /uninstall` drive it headlessly, exit 2 if Revit is running
  and 3 if no Revit was found.
- **`ATKINSMEC-Setup.zip`** — the same logic as a PowerShell script plus
  `Install.cmd`, for sites whose IT blocks unsigned executables.

**`installer/exe/app.manifest` must not be removed.** It requests
`asInvoker`. Windows has installer detection: an unsigned exe whose
*filename* contains "setup" or "install" is assumed to need elevation and
gets a UAC prompt before it runs. Verified `asInvoker` is embedded and no
`requireAdministrator` appears (scan the whole file — the manifest sits
after the 13 MB payload, so a first-200 KB scan misses it and looks like
a failure).

**No admin rights**, and that is deliberate: it installs into
`%AppData%\Autodesk\Revit\Addins\<version>\`, the per-user location.
`%ProgramData%\...` would cover every user of a machine but needs
elevation, so it is documented in `installer/README.md` as a manual step
rather than built in.

Both were tested. The **script** installer ran against a sandboxed
`%APPDATA%`; the **exe** could not be sandboxed that way, because
`Environment.GetFolderPath(ApplicationData)` resolves the real known
folder and ignores the `APPDATA` variable — so it was run for real with
`/silent`, which installed all six versions cleanly (exit 0), matched
every deployed DLL to its Release build, and left RevitAddinManager's
files untouched.

What the sandboxed run proved:

- Version detection picked exactly the three Revit versions installed
  here (2022, 2025, 2027) and skipped the rest. A version counts as
  present if `Program Files\Autodesk\Revit <v>\Revit.exe` exists **or**
  its add-ins folder does.
- **The uninstall is surgical.** A fake `RevitLookup.addin` +
  `RevitLookup.dll` were planted in the 2025 folder; after uninstall both
  survived and none of our 19 files remained. This matters — that folder
  really is shared, and clearing it would delete other people's add-ins.
  The mechanism is `ATKINSMEC.files.txt`, written at install time next to
  the files and read back at uninstall. Do not replace it with a wildcard
  delete.
- Settings (`%AppData%\ATKINSMEC\`) are kept on uninstall unless
  `-RemoveSettings` is passed.
- Both install and uninstall refuse to run while Revit is open, rather
  than half-completing — the same trap that wasted so much time here.

`installer/ATKINSMEC.iss` is an alternative wizard-style installer with an
*Apps & features* entry, `PrivilegesRequired=lowest`; `build-installer.ps1`
picks up ISCC automatically if present. It is not needed now that the exe
exists, and **it has never been compiled** — Inno Setup is not installed
here.

## Housekeeping

- `SpaceKey.cs` is dead code; `VENTSHEET.SpaceKey` is the live one.
- The Revit **2022** add-ins folder holds ~30 stale DLLs from the old
  packages.config dependency set (ClosedXML, RBush, SixLabors and
  friends). Harmless but untidy. That folder is shared with RevitLookup
  and Add-In Manager, so only our own leftovers should be removed —
  offered, not yet done.
