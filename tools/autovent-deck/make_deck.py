"""
AUTOVENT - a user guide for Revit users, as a PowerPoint deck.

Written straight to OOXML: no PowerPoint, LibreOffice or python-pptx is on
this machine, so the package is built by hand here and checked two ways -
the skill's validator (schema, relationships, content types) and a PNG of
every slide drawn from the SAME layout with the real Windows fonts, with
every text box that would overflow reported.

    python tools/autovent-deck/make_deck.py <renders> <deck.pptx> [<previews>]

<renders>  a folder of tests/ventpreview renders - run that first:
           dotnet run --project tests/ventpreview -c Release -- <renders>
           The screenshots are cropped from them, so the deck always shows
           the window as it is built now.
<previews> optional; one PNG per slide.

Pillow only (it is installed). Units are inches; the slide is 16:9.
"""

import io
import os
import sys
import uuid
import zipfile
import datetime
from xml.sax.saxutils import escape

from PIL import Image, ImageDraw, ImageFont

EMU = 914400
SW, SH = 13.333, 7.5
DPI = 150

# ---------------------------------------------------------------- palette
# Deep ocean for the dark slides and titles, the window's own blue for the
# steps, and the two colours an MEP drawing already gives fresh air and
# extract - so green and red mean the same here as on the drawings.
SCHEME = {
    "dk1": "1B2733", "lt1": "FFFFFF", "dk2": "123A5A", "lt2": "EEF3F7",
    "accent1": "1F64A0", "accent2": "2E9D63", "accent3": "D9503A",
    "accent4": "C98A12", "accent5": "6B7C8F", "accent6": "7A4FA3",
    "hlink": "1F64A0", "folHlink": "6B4EA0",
}
MAP = {"tx1": "dk1", "bg1": "lt1", "tx2": "dk2", "bg2": "lt2"}

MUTED = "5B6B7C"        # captions on white
ON_DARK = "BFD3E6"      # secondary text on the dark slides
LINE = "D3DCE5"         # card and screenshot borders
TINT_BLUE = "E7EFF8"
TINT_GREEN = "E6F4EC"
TINT_RED = "FBEAE6"
TINT_AMBER = "FCF1DA"

FONTS = {"major": "Segoe UI Semibold", "minor": "Segoe UI"}

SECTIONS = []           # (name, [slide index...])


def hexof(colour):
    colour = MAP.get(colour, colour)
    return SCHEME.get(colour, colour)


# ---------------------------------------------------------------- model
def R(text, size=16, bold=False, color="tx1", font="minor", italic=False):
    return {"text": text, "size": size, "bold": bold, "color": color,
            "font": font, "italic": italic}


def P(runs, align="l", after=0, before=0, bullet=False, size=16, color="tx1",
      bold=False, font="minor", italic=False, field=None):
    if isinstance(runs, str):
        runs = [R(runs, size, bold, color, font, italic)]
    return {"runs": runs, "align": align, "after": after, "before": before,
            "bullet": bullet, "size": size, "field": field}


class Slide:
    def __init__(self, title, section, dark=False):
        self.title = title
        self.dark = dark
        self.items = []
        self.images = []
        if not SECTIONS or SECTIONS[-1][0] != section:
            SECTIONS.append((section, []))
        SECTIONS[-1][1].append(self)

    def shape(self, x, y, w, h, geom="rect", fill=None, line=None, lw=1.0,
              paras=None, anchor="t", ins=(0.1, 0.05, 0.1, 0.05), name=None,
              adj=None, rot=0, flipv=False, is_title=False, text_box=False):
        self.items.append(dict(kind="sp", x=x, y=y, w=w, h=h, geom=geom,
                               fill=fill, line=line, lw=lw, paras=paras or [],
                               anchor=anchor, ins=ins, name=name, adj=adj,
                               rot=rot, flipv=flipv, is_title=is_title,
                               text_box=text_box))

    def text(self, x, y, w, h, paras, anchor="t", name=None, ins=(0, 0, 0, 0)):
        if isinstance(paras, dict):
            paras = [paras]
        self.shape(x, y, w, h, paras=paras, anchor=anchor, ins=ins,
                   name=name or "Text", text_box=True)

    def pic(self, img, x, y, w, h, descr, border=LINE):
        self.images.append(img)
        self.items.append(dict(kind="pic", img=img, x=x, y=y, w=w, h=h,
                               descr=descr, border=border,
                               index=len(self.images)))

    def arrow(self, x1, y1, x2, y2, color="accent1", lw=2.25, head=True):
        self.items.append(dict(kind="cxn", x1=x1, y1=y1, x2=x2, y2=y2,
                               color=color, lw=lw, head=head))


# ---------------------------------------------------------------- helpers
def title(s, text, color="tx2", y=0.42, size=32, x=0.6, w=12.13, h=0.85):
    s.shape(x, y, w, h, paras=[P(text, size=size, color=color, font="major")],
            anchor="ctr", ins=(0, 0, 0, 0), name="Title", is_title=True)


def footer(s, n):
    s.text(0.6, 7.02, 6, 0.3, P("AUTOVENT  ·  a guide for Revit users",
                                size=11, color=MUTED))
    s.text(11.73, 7.02, 1.0, 0.3, P("", align="r", size=11, color=MUTED,
                                    field=str(n)))


def card(s, x, y, w, h, fill="bg2", line=None, name="Card"):
    s.shape(x, y, w, h, geom="roundRect", fill=fill, line=line, adj=5000,
            name=name)


def badge(s, cx, cy, label, d=0.46, fill="accent1", size=16, color="bg1"):
    s.shape(cx - d / 2, cy - d / 2, d, d, geom="ellipse", fill=fill,
            paras=[P(label, align="ctr", size=size, color=color, font="major")],
            anchor="ctr", ins=(0, 0, 0, 0), name="Badge " + label)


def bullets(items, size=16, after=9, color="tx1"):
    return [P(t, bullet=True, size=size, after=after, color=color)
            if isinstance(t, str) else t for t in items]


def heading_and_body(s, x, y, w, h, head, body, head_size=18, body_size=14,
                     head_color="tx2", body_color="tx1", gap=6):
    s.text(x, y, w, h, [P(head, size=head_size, color=head_color,
                          font="major", after=gap),
                        P(body, size=body_size, color=body_color)])


# ---------------------------------------------------------------- slides
def build(shots):
    slides = []

    def new(title_text, section, dark=False):
        s = Slide(title_text, section, dark)
        slides.append(s)
        return s

    # 1 ------------------------------------------------------------ title
    s = new("AUTOVENT", "Introduction", dark=True)
    s.text(0.7, 1.55, 6.0, 0.4, P("ATKINS MEP TOOLKIT  →  MECHANICAL   ·   REVIT 2022 – 2027",
                                  size=13, color=ON_DARK, font="major"))
    title(s, "AUTOVENT", color="bg1", y=1.95, size=60, x=0.7, w=6.0, h=1.2)
    s.text(0.7, 3.25, 5.7, 1.6, P("Ventilation airflows from the Excel schedule "
                                  "onto Revit air terminals - and the model's "
                                  "rooms back into the sheet",
                                  size=22, color="bg1"))
    s.text(0.7, 6.25, 5.5, 0.4, P("A guide for Revit users", size=14,
                                  color=ON_DARK))
    s.pic(shots["main"], 7.05, 1.0, 5.65, 5.65 / shots["main"].width
          * shots["main"].height, "The AUTOVENT window in Revit", border="FFFFFF")

    # 2 ------------------------------------------------------------ what it does
    s = new("What AUTOVENT does", "Introduction")
    title(s, "What AUTOVENT does")
    for x, tag, fill, head, lines in (
            (0.6, "xlsx", "accent2", "Excel ventilation schedule",
             ["The office ventilation calculation",
              "One row per room, under a blue row per level",
              "Fresh air and total exhaust in L/s"]),
            (9.13, "rvt", "accent1", "Revit MEP model",
             ["The architect's models linked in",
              "Air terminals on fresh air and extract systems",
              "FCUs fed by endcaps or bellmouths"])):
        card(s, x, 1.65, 3.6, 4.0)
        badge(s, x + 1.8, 2.45, tag, d=0.95, fill=fill, size=17)
        s.text(x + 0.3, 3.15, 3.0, 0.5, P(head, align="ctr", size=18,
                                          color="tx2", font="major"))
        s.text(x + 0.3, 3.75, 3.0, 1.8,
               [P(t, align="ctr", size=14, after=7) for t in lines])
    s.arrow(4.45, 2.75, 8.88, 2.75, color="accent2", lw=4)
    s.text(4.6, 1.9, 4.15, 0.75, [P("Assign airflows", align="ctr", size=18,
                                    color="accent2", font="major"),
                                  P("fresh air and extract onto every terminal",
                                    align="ctr", size=14)])
    s.arrow(8.88, 4.55, 4.45, 4.55, color="accent1", lw=4)
    s.text(4.6, 4.72, 4.15, 0.75, [P("Export rooms", align="ctr", size=18,
                                     color="accent1", font="major"),
                                   P("names, numbers, areas, heights, WC counts",
                                     align="ctr", size=14)])
    badge(s, 0.83, 6.27, "i", d=0.34, fill="accent5", size=13)
    s.text(1.15, 6.08, 11.5, 0.4, P("Rooms come from the architect's linked "
                                    "models. The levels are your model's levels.",
                                    size=15, color=MUTED))
    footer(s, 2)

    # 3 ------------------------------------------------------------ before you start
    s = new("Before you start", "Introduction")
    title(s, "Before you start")
    cells = (
        ("Install once", "Run AtkinsMEPToolkit-Setup.exe - no admin rights needed. "
         "AUTOVENT appears on the Atkins MEP Toolkit tab, Mechanical panel."),
        ("Open your MEP model", "With the architect's models linked and "
         "loaded: the rooms are read from them."),
        ("Terminals on the right systems", "Fresh air and extract terminals on "
         "duct systems named Fresh, Extract or Exhaust - or tick the system "
         "types in the window."),
        ("The ventilation workbook", "The office layout: a blue row per level, "
         "a row per room. No workbook yet? AUTOVENT can export a new one."),
    )
    for i, (head, body) in enumerate(cells):
        x = 0.6 + (i % 2) * 6.17
        y = 1.6 + (i // 2) * 2.6
        card(s, x, y, 5.96, 2.35)
        badge(s, x + 0.65, y + 0.68, str(i + 1), d=0.62, fill="accent2", size=20)
        heading_and_body(s, x + 1.25, y + 0.38, 4.45, 1.85, head, body,
                         body_size=15)
    footer(s, 3)

    # 4 ------------------------------------------------------------ at a glance
    s = new("The window at a glance", "Introduction")
    title(s, "The window at a glance")
    img = shots["main"]
    w = 6.7
    h = w / img.width * img.height
    s.pic(img, 0.6, 1.45, w, h, "The AUTOVENT window, four numbered parts")
    k = w / img.width
    badge(s, 0.6 + 60 * k, 1.45 + 926 * k, "4", d=0.2, size=10)
    rows = (("Linked models", "Which architect's models the rooms are read from."),
            ("Ventilation schedule", "The workbook, and the worksheet with the airflows."),
            ("Levels to process", "Your model's levels, each with the linked rooms "
                                  "on it. Tick what to process."),
            ("Write airflows  ·  Export", "The two jobs: airflows onto the "
                                          "terminals, or rooms into Excel."))
    for i, (head, body) in enumerate(rows):
        y = 1.55 + i * 1.33
        badge(s, 8.15, y + 0.3, str(i + 1), d=0.5)
        heading_and_body(s, 8.65, y, 4.1, 1.25, head, body, head_size=17,
                         gap=3)
    footer(s, 4)

    # 5 ------------------------------------------------------------ step 1
    s = new("Step 1 - Tick the linked models", "Writing airflows")
    title(s, "Step 1  ·  Tick the linked models")
    img = shots["links"]
    w = 7.2
    s.pic(img, 0.6, 1.6, w, w / img.width * img.height,
          "The Linked models card")
    s.text(8.2, 1.6, 4.53, 4.6, bullets([
        "Tick each architect's model to read rooms from. A model linked "
        "twice shows twice - one row per placement.",
        "The grey line says which of your levels it covers, and how many rooms.",
        "Search narrows a long list; a ticked model it hides still counts.",
        "Apartments and Interior models... are for apartment jobs - slide 15.",
    ], size=16, after=12))
    card(s, 0.6, 5.95, 7.2, 0.75, fill=TINT_BLUE)
    s.text(0.85, 5.95, 6.8, 0.75, P([R("Refresh ", 15, color="accent1",
                                       font="major"),
                                     R("reads the links again after one is "
                                       "reloaded or a room is drawn.", 15)]),
           anchor="ctr")
    footer(s, 5)

    # 6 ------------------------------------------------------------ step 2
    s = new("Step 2 - Open the ventilation workbook", "Writing airflows")
    title(s, "Step 2  ·  Open the ventilation workbook")
    img = shots["schedule"]
    w = 7.2
    s.pic(img, 0.6, 1.6, w, w / img.width * img.height,
          "The Ventilation schedule card")
    s.text(8.2, 1.6, 4.53, 0.45, P("It reads every room row:", size=17,
                                    color="tx2", font="major"))
    for i, (label, fill) in enumerate((("Space name", TINT_BLUE),
                                       ("Room number", TINT_BLUE),
                                       ("Fresh air  (L/s)", TINT_GREEN),
                                       ("Total exhaust, max. rounded up  (L/s)",
                                        TINT_RED))):
        y = 2.15 + i * 0.52
        s.shape(8.2, y, 4.53, 0.42, geom="roundRect", fill=fill, adj=20000,
                paras=[P(label, size=15)], anchor="ctr",
                ins=(0.18, 0, 0.1, 0), name="Column " + label)
    s.text(8.2, 4.3, 4.53, 1.4, bullets([
        "Columns are found by their headings, wherever they sit.",
        "Rows under a blue level row belong to that level.",
    ], size=15, after=8))
    card(s, 0.6, 5.95, 12.13, 0.8, fill=TINT_AMBER)
    s.text(0.85, 5.95, 11.7, 0.8, P([
        R("In the sheet, not in this model: ", 15, color="accent4",
          font="major"),
        R("the sheet's level rows must carry your model's level names. Rows "
          "under any other level match nothing.", 15)]), anchor="ctr")
    footer(s, 6)

    # 7 ------------------------------------------------------------ step 3
    s = new("Step 3 - Choose the levels and rooms", "Writing airflows")
    title(s, "Step 3  ·  Choose the levels and rooms")
    img = shots["levels"]
    w = 12.13
    s.pic(img, 0.6, 1.5, w, w / img.width * img.height,
          "The Levels to process card")
    for i, (head, body) in enumerate((
            ("Your model's levels", "Each linked room goes on the level its "
             "floor stands on - up to the next level. A floor up to 300 mm "
             "below a level still counts."),
            ("Tick rooms, not only levels", "Open a level to tick single rooms. "
             "An unticked room is left out of the airflow write and the export."),
            ("Find and show", "Search by room number or name. Show in plan - or "
             "a double-click on a room - zooms to it in a plan of its level."))):
        x = 0.6 + i * 4.14
        card(s, x, 4.4, 3.85, 2.35)
        heading_and_body(s, x + 0.25, 4.56, 3.4, 2.1, head, body)
    footer(s, 7)

    # 8 ------------------------------------------------------------ matching
    s = new("How a room finds its row in the sheet", "Writing airflows")
    title(s, "How a room finds its row in the sheet")
    s.text(2.5, 1.55, 3.6, 0.4, P("In the model", align="ctr", size=16,
                                   color=MUTED, font="major"))
    s.text(7.3, 1.55, 3.6, 0.4, P("In the sheet", align="ctr", size=16,
                                   color=MUTED, font="major"))
    for i, (label, value) in enumerate((("Name", "MASTER BEDROOM"),
                                        ("Number", "121.046"),
                                        ("Level", "LEVEL 03"))):
        y = 2.05 + i * 0.85
        s.text(0.6, y, 1.75, 0.62, P(label, align="r", size=17, color="tx2",
                                      font="major"), anchor="ctr")
        for x in (2.5, 7.3):
            s.shape(x, y, 3.6, 0.62, geom="roundRect", fill=TINT_BLUE,
                    adj=18000, paras=[P(value, align="ctr", size=17)],
                    anchor="ctr", name="Value " + value)
        badge(s, 6.7, y + 0.31, "=", d=0.48, fill="accent2", size=18)
    s.text(0.6, 4.75, 5.9, 0.9, P("All three must match. Capitals and spaces "
                                  "at the ends do not matter; everything else "
                                  "does.", size=16))
    for i, (model, sheet) in enumerate((("121.46", "121.046"),
                                        ("L03", "LEVEL 03"))):
        y = 4.72 + i * 0.68
        s.shape(7.3, y, 3.6, 0.52, geom="roundRect", fill=TINT_RED, adj=18000,
                paras=[P([R(model, 15), R("   no match   ", 13, color="accent3",
                                          font="major"), R(sheet, 15)],
                         align="ctr")],
                anchor="ctr", name="No match " + model)
    card(s, 0.6, 6.15, 12.13, 0.6, fill=TINT_GREEN)
    s.text(0.85, 6.15, 11.7, 0.6, P([
        R("Renamed a room in Arrange rooms...? ", 15, color="accent2",
          font="major"),
        R("The new name is used both ways, so it still matches.", 15)]),
        anchor="ctr")
    footer(s, 8)

    # 9 ------------------------------------------------------------ step 4
    s = new("Step 4 - Assign airflows", "Writing airflows")
    title(s, "Step 4  ·  Assign airflows")
    img = shots["assign"]
    w = 12.13
    s.pic(img, 0.6, 1.5, w, w / img.width * img.height,
          "The Write airflows into the model tab")
    for i, (head, body) in enumerate((
            ("Fresh air  ·  Extract", "Each button opens the duct system "
             "types to tick. Tick none and the names decide."),
            ("Endcap reach", "How far an endcap or bellmouth looks for the FCU "
             "it feeds. 1500 mm unless you change it."),
            ("Endcaps  ·  Bellmouths", "Family names, a comma between. "
             "Bellmouth: feeds an FCU only when it faces one."),
            ("Assign airflows", "Writes the Flow of every terminal in one step "
             "- a single Ctrl+Z undoes it."))):
        x = 0.6 + i * 3.1
        card(s, x, 4.0, 2.88, 2.75)
        heading_and_body(s, x + 0.22, 4.18, 2.46, 2.45, head, body,
                         head_size=17)
    footer(s, 9)

    # 10 ----------------------------------------------------------- the four cases
    s = new("Which airflow each terminal gets", "Writing airflows")
    title(s, "Which airflow each terminal gets")
    cases = (
        ("Grille or diffuser", "The room's air, shared equally between the "
         "room's terminals on that side.", "grille"),
        ("Bellmouth into its room", "Treated like a grille - FCU in the room "
         "or not.", "into"),
        ("Endcap on an FCU", "The air of the rooms that FCU supplies, "
         "wherever the endcap stands.", "endcap"),
        ("Bellmouth facing an FCU", "In the ceiling void, mouth on the unit: "
         "it feeds that FCU, like an endcap.", "facing"),
    )
    for i, (head, body, kind) in enumerate(cases):
        x = 0.6 + i * 3.1
        card(s, x, 1.55, 2.88, 5.2)
        case_diagram(s, x, 1.55, kind)
        # heading and body apart, so the bodies line up whether a heading
        # takes one line or two
        s.text(x + 0.22, 4.15, 2.46, 0.8, P(head, size=17, color="tx2",
                                             font="major"))
        s.text(x + 0.22, 4.98, 2.46, 1.65, P(body, size=14))
    footer(s, 10)

    # 11 ----------------------------------------------------------- two FCUs
    s = new("One room, two FCUs", "Writing airflows")
    title(s, "One room, two FCUs")
    s.shape(0.6, 1.6, 6.4, 4.95, geom="roundRect", fill="bg1", line=LINE,
            lw=1.5, adj=3000, name="Room")
    s.text(0.85, 1.75, 5.9, 0.75, [P("MEETING ROOM", size=17, color="tx2",
                                     font="major"),
                                   P("Fresh air in the sheet: 100 L/s", size=14,
                                     color=MUTED)])
    for j, x in enumerate((1.25, 4.05)):
        s.shape(x, 3.0, 2.1, 1.0, fill="accent5", paras=[P("FCU " + str(j + 1),
                align="ctr", size=16, color="bg1", font="major")],
                anchor="ctr", name="FCU")
        s.shape(x + 0.45, 4.75, 1.2, 0.5, fill="accent2",
                paras=[P("endcap", align="ctr", size=13, color="bg1")],
                anchor="ctr", name="Endcap")
        s.arrow(x + 1.05, 4.75, x + 1.05, 4.05, color="accent2", lw=2.5)
        s.text(x, 5.4, 2.1, 0.5, P("50 L/s", align="ctr", size=20,
                                   color="accent2", font="major"))
    s.text(7.55, 1.6, 5.18, 4.9, bullets([
        "Every endcap serving a room gets an equal share - the shares add back "
        "up to the room's total.",
        "One FCU drawn with no endcap? The others carry all of it, and the "
        "result says so: the gap is worth modelling.",
        "Nothing is written twice; nothing is lost.",
    ], size=17, after=16))
    footer(s, 11)

    # 12 ----------------------------------------------------------- the result
    s = new("Reading the result", "Writing airflows")
    title(s, "Reading the result")
    s.shape(0.6, 1.55, 6.3, 4.3, geom="roundRect", fill="bg1", line=LINE,
            lw=1.5, adj=2500, name="Result dialog")
    s.shape(0.6, 1.55, 6.3, 0.5, fill="bg2", name="Dialog caption",
            paras=[P("AUTOVENT", size=14, color="tx2", font="major")],
            anchor="ctr", ins=(0.25, 0, 0.1, 0))
    lines = ["Models: 3", "Levels: LEVEL 03", "Rooms in scope: 69",
             "Rooms matched to the sheet: 68", "Terminals updated: 41",
             "Terminals skipped (read-only): 0",
             "Bellmouths feeding an FCU: 12 of 27"]
    paras = [P(t, size=15, after=2) for t in lines]
    paras.append(P("1 room(s) with fresh air and no terminal or endcap to take "
                   "it:", size=15, before=10, after=2, color="accent3"))
    paras.append(P("      121.046 MASTER BEDROOM: 50 l/s", size=15, after=10,
                   color="accent3"))
    paras.append(P("Full details: ATKINSMEC_debug_log.txt on your Desktop.",
                   size=15))
    s.text(0.9, 2.25, 5.8, 3.45, paras)
    s.text(7.45, 1.6, 5.28, 0.45, P("What to look at", size=18, color="tx2",
                                     font="major"))
    s.text(7.45, 2.2, 5.28, 4.5, bullets([
        "Matched against in scope: a big gap means names, numbers or levels "
        "differ - slide 8.",
        "Skipped: the terminal's Flow cannot be written - a family setting.",
        "A room with air and nothing to take it: no terminal in it, an endcap "
        "or bellmouth name missing, or the FCU beyond the reach.",
        "The log lists every terminal and why it got what it got.",
    ], size=15, after=11))
    footer(s, 12)

    # 13 ----------------------------------------------------------- export
    s = new("Export rooms back to Excel", "Exporting to Excel")
    title(s, "Export rooms back to Excel")
    img = shots["export"]
    w = 12.13
    s.pic(img, 0.6, 1.5, w, w / img.width * img.height,
          "The Export rooms back to Excel tab")
    for i, (head, body) in enumerate((
            ("Update the imported worksheet", "Adds new rooms and updates "
             "changed ones; what you typed stays. Rooms no longer in the "
             "model are greyed, never deleted."),
            ("A new workbook", "Built from the office ventilation calculation: "
             "its headings, formulas and ASHRAE tables. Typed inputs are left "
             "blank."),
            ("Add a worksheet", "The same new sheet, added to the workbook you "
             "imported. Every other sheet is left alone; a name in use is "
             "refused."))):
        x = 0.6 + i * 4.14
        card(s, x, 4.0, 3.85, 2.75)
        heading_and_body(s, x + 0.25, 4.18, 3.4, 2.45, head, body)
    footer(s, 13)

    # 14 ----------------------------------------------------------- what goes in
    s = new("What goes into the sheet", "Exporting to Excel")
    title(s, "What goes into the sheet")
    sheet_mock(s, 0.6, 1.6)
    s.text(8.35, 1.6, 4.38, 4.6, bullets([
        "From Revit: name, number, area and height. Numbers go in as text, so "
        "0123 stays 0123.",
        "No. of Units: the WCs and urinals standing in each room. Fixtures... "
        "picks the models and families.",
        "Project name and job number from Project Information.",
        "Then fill in occupancy and the rest - and use the sheet for Step 2.",
    ], size=15, after=11))
    footer(s, 14)

    # 15 ----------------------------------------------------------- apartments
    s = new("Apartments: ARCH apartments, IND rooms inside", "Apartments and arranging")
    title(s, "Apartments: ARCH apartments, IND rooms inside")
    img = shots["interiors"]
    h = 5.15
    w = h / img.height * img.width
    s.pic(img, 0.6, 1.55, w, h, "The Interior models, level by level dialog")
    x0 = 0.6 + w + 0.5
    steps = (
        ("Tick Apartments", "on the architect's model that draws the "
         "apartments."),
        ("Interior models...", "finds the IND models by keyword, level by "
         "level. Untick one to leave it out on that level."),
        ("Grouped like Tower-1:", "each IND room under the apartment it stands "
         "in, on the same level: apartment down column F, rooms in H, "
         "bedrooms merged."),
    )
    for i, (head, body) in enumerate(steps):
        y = 1.6 + i * 1.45
        badge(s, x0 + 0.25, y + 0.25, str(i + 1), d=0.5)
        s.text(x0 + 0.7, y, 12.73 - x0 - 0.7, 1.35, P([
            R(head + " ", 16, color="tx2", font="major"), R(body, 16)]))
    card(s, x0, 6.0, 12.73 - x0, 0.75, fill=TINT_AMBER)
    s.text(x0 + 0.2, 6.0, 12.73 - x0 - 0.35, 0.75, P(
        "Rooms in no apartment are counted and listed in "
        "ATKINSMEC_apartments_log.txt on the Desktop.", size=14),
        anchor="ctr")
    footer(s, 15)

    # 16 ----------------------------------------------------------- arrange
    s = new("Arrange rooms for the sheet", "Apartments and arranging")
    title(s, "Arrange rooms for the sheet")
    img = shots["arrange"]
    h = 5.15
    w = h / img.height * img.width
    s.pic(img, 0.6, 1.55, w, h, "The Arrange rooms for the sheet dialog")
    x0 = 0.6 + w + 0.5
    rows = (("Reorder", "Drag a room, or Move up / Move down. Rooms stay "
                        "inside their level and apartment."),
            ("Rename", "Rename... or F2. The new name goes into the sheet and "
                       "is matched when airflows are written."),
            ("Find and replace", "Change text in many names at once - one "
                                 "level or all."),
            ("Ungroup", "Write an interior room as an ordinary room. "
                        "Regroup puts it back under its apartment."))
    for i, (head, body) in enumerate(rows):
        y = 1.55 + i * 1.18
        heading_and_body(s, x0, y, 12.73 - x0, 1.1, head, body, head_size=17,
                         body_size=15, gap=2)
    s.text(x0, 6.35, 12.73 - x0, 0.4, P("Saved with this model.", size=15,
                                        color=MUTED, italic=True))
    footer(s, 16)

    # 17 ----------------------------------------------------------- good to know
    s = new("Good to know", "Help")
    title(s, "Good to know")
    tips = (
        ("Remembered per model", "Links, system types, reach, endcap and "
         "bellmouth names, fixtures, apartments, arrangement - shared by every "
         "local copy."),
        ("Refresh", "Reads links, rooms and levels again after a link is "
         "reloaded or a room is drawn."),
        ("One model per window", "The window works on the model it was opened "
         "on. Switch back to it to run."),
        ("Fits a laptop", "The window resizes, and scrolls when the screen is "
         "short."),
        ("Desktop logs", "ATKINSMEC_debug_log.txt after every airflow run; "
         "ATKINSMEC_apartments_log.txt for apartments."),
        ("Undo", "Assign airflows is one step in Revit: Ctrl+Z undoes all of "
         "it."),
    )
    for i, (head, body) in enumerate(tips):
        x = 0.6 + (i % 3) * 4.14
        y = 1.55 + (i // 3) * 2.65
        card(s, x, y, 3.85, 2.45)
        s.text(x + 0.3, y + 0.15, 3.3, 2.15,
               [P(head, size=18, color="tx2", font="major", after=6),
                P(body, size=15)], anchor="ctr")
    footer(s, 17)

    # 18 ----------------------------------------------------------- troubleshooting
    s = new("When something looks wrong", "Help")
    title(s, "When something looks wrong")
    trouble = (
        ("No rooms matched", "The dialog shows the first room and the first "
         "sheet rows side by side: compare name, number and level."),
        ("In the sheet, not in this model", "The sheet's level rows must carry "
         "your model's level names."),
        ("A room with air, nothing to take it", "No terminal in it, an endcap "
         "or bellmouth name missing, or the FCU beyond the reach."),
        ("Rooms on the wrong level", "ATKINSMEC_debug_log.txt lists each link's "
         "levels and checks one room's floor two ways."),
        ("IND rooms not under their apartment", "ATKINSMEC_apartments_log.txt "
         "says, level by level, where each interior room stood."),
    )
    s.text(0.6, 1.5, 4.5, 0.4, P("You see", size=14, color=MUTED,
                                 font="major"))
    s.text(5.35, 1.5, 7.3, 0.4, P("Check", size=14, color=MUTED, font="major"))
    for i, (seen, check) in enumerate(trouble):
        y = 1.95 + i * 0.97
        s.shape(0.6, y, 4.55, 0.85, geom="roundRect", fill=TINT_RED, adj=12000,
                paras=[P(seen, size=15, color="tx1", font="major")],
                anchor="ctr", ins=(0.2, 0, 0.1, 0), name="Symptom")
        s.shape(5.3, y, 7.43, 0.85, geom="roundRect", fill="bg2", adj=12000,
                paras=[P(check, size=14)], anchor="ctr",
                ins=(0.2, 0, 0.15, 0), name="Check")
    footer(s, 18)

    # 19 ----------------------------------------------------------- close
    s = new("The workflow", "Help", dark=True)
    title(s, "The workflow", color="bg1", y=1.0, size=40, x=0.7, w=11.9,
          h=1.0)
    flow = ("Tick the linked models", "Open the workbook",
            "Tick levels and rooms", "Assign airflows or Export")
    for i, t in enumerate(flow):
        x = 0.7 + i * 3.08
        badge(s, x + 0.45, 3.0, str(i + 1), d=0.9, fill="accent1", size=26)
        s.text(x, 3.7, 2.6, 1.0, P(t, size=18, color="bg1", font="major"))
        if i < 3:
            s.arrow(x + 1.05, 3.0, x + 2.73, 3.0, color=ON_DARK, lw=2)
    s.text(0.7, 5.6, 11.9, 0.9, P("Something looks wrong? Send the Desktop log "
                                  "with the model name - it shows what "
                                  "AUTOVENT saw.", size=17, color=ON_DARK))
    return slides


def case_diagram(s, x, y, kind):
    """A small picture of each case, in the top of its card."""
    rx, ry, rw, rh = x + 0.3, y + 0.35, 2.28, 1.95
    s.shape(rx, ry, rw, rh, fill="bg1", line=LINE, lw=1.25, name="Room")
    if kind == "grille":
        for tx in (rx + 0.45, rx + 1.4):
            s.shape(tx, ry + 0.3, 0.45, 0.22, fill="accent2", name="Grille")
            s.arrow(tx + 0.225, ry + 0.55, tx + 0.225, ry + 1.05,
                    color="accent2", lw=1.75)
        s.text(rx, ry + 1.2, rw, 0.6, P("room's air  ÷  2", align="ctr",
                                         size=13, color=MUTED))
    elif kind == "into":
        s.shape(rx + 1.35, ry + 0.25, 0.7, 0.42, fill="accent5",
                paras=[P("FCU", align="ctr", size=11, color="bg1")],
                anchor="ctr", ins=(0, 0, 0, 0), name="FCU")
        s.shape(rx + 0.35, ry + 0.3, 0.5, 0.32, geom="trapezoid",
                fill="accent2", flipv=False, name="Bellmouth")
        s.arrow(rx + 0.6, ry + 0.65, rx + 0.6, ry + 1.2, color="accent2",
                lw=1.75)
        s.text(rx, ry + 1.3, rw, 0.55, P("into the room", align="ctr", size=13,
                                         color=MUTED))
    elif kind in ("endcap", "facing"):
        # the unit over one room, supplying the next
        s.shape(rx + rw / 2 - 0.02, ry, 0.04, 1.15, fill=LINE, name="Wall")
        s.shape(rx + 0.15, ry + 0.25, 0.75, 0.45, fill="accent5",
                paras=[P("FCU", align="ctr", size=11, color="bg1")],
                anchor="ctr", ins=(0, 0, 0, 0), name="FCU")
        if kind == "endcap":
            s.shape(rx + 0.37, ry + 0.95, 0.32, 0.22, fill="accent2",
                    name="Endcap")
            top = ry + 0.95
        else:
            s.shape(rx + 0.33, ry + 0.85, 0.4, 0.3, geom="trapezoid",
                    fill="accent2", flipv=True, name="Bellmouth")
            top = ry + 0.85
        s.arrow(rx + 0.53, top, rx + 0.53, ry + 0.73, color="accent2",
                lw=1.75)
        s.arrow(rx + 0.92, ry + 0.47, rx + 1.75, ry + 0.47, color="accent1",
                lw=1.75)
        s.shape(rx + 1.6, ry + 0.38, 0.4, 0.18, fill="accent1",
                name="Supply grille")
        s.text(rx, ry + 1.3, rw, 0.55, P("air of the room it supplies",
                                         align="ctr", size=13, color=MUTED))


def sheet_mock(s, x, y):
    """A few rows of the office sheet: what AUTOVENT writes, what the office
    formulas work out, and what is yours to fill in."""
    cols = (("Space", 1.8), ("Room No.", 1.0), ("Area", 0.75),
            ("Height", 0.8), ("No. of Units", 1.05), ("Occupancy", 1.05),
            ("Fresh air", 0.95))
    rows = (
        ("level", ("LEVEL 03",)),
        ("room", ("MASTER BEDROOM", "121.046", "18.5", "2.70", "", "", "=")),
        ("room", ("ENSUITE", "121.048", "6.2", "2.40", "1", "", "=")),
        ("room", ("LIVING/DINING", "121.053", "31.4", "2.70", "", "", "=")),
        ("sub", ("Sub Total", "", "=", "", "", "", "=")),
    )
    rh = 0.6
    cx = x
    for j, (head, w) in enumerate(cols):
        s.shape(cx, y, w, rh, fill="tx2", line="FFFFFF", lw=0.75,
                paras=[P(head, align="ctr", size=12, color="bg1", font="major")],
                anchor="ctr", ins=(0.03, 0, 0.03, 0), name="Heading")
        cx += w
    total = cx - x
    for i, (kind, values) in enumerate(rows):
        ry = y + rh * (i + 1)
        if kind == "level":
            s.shape(x, ry, total, rh, fill="00B0F0", line="FFFFFF", lw=0.75,
                    paras=[P(values[0], size=13, color="tx1", font="major")],
                    anchor="ctr", ins=(0.1, 0, 0.1, 0), name="Level row")
            continue
        cx = x
        for j, ((head, w), value) in enumerate(zip(cols, values)):
            revit = kind == "room" and j <= 4
            formula = value == "="
            fill = TINT_GREEN if revit else ("ECEFF2" if formula else "FFFFFF")
            text = "=..." if formula else value
            s.shape(cx, ry, w, rh, fill=fill, line=LINE, lw=0.75,
                    paras=[P(text, align="l" if j == 0 else "ctr", size=13,
                             color=MUTED if formula else "tx1",
                             font="major" if kind == "sub" and j == 0 else "minor")],
                    anchor="ctr", ins=(0.06, 0, 0.04, 0), name="Cell")
            cx += w
    ly = y + rh * (len(rows) + 1) + 0.35
    for i, (fill, label) in enumerate(((TINT_GREEN, "from Revit"),
                                       ("ECEFF2", "office formula"),
                                       ("FFFFFF", "yours to fill in"))):
        lx = x + i * 2.5
        s.shape(lx, ly + 0.05, 0.32, 0.26, fill=fill, line=LINE, lw=0.75,
                name="Key")
        s.text(lx + 0.45, ly, 2.0, 0.38, P(label, size=14, color=MUTED),
               anchor="ctr")


# ---------------------------------------------------------------- screenshots
def shots_from(folder):
    def crop(name, box=None):
        img = Image.open(os.path.join(folder, name)).convert("RGBA")
        # a WPF render has a transparent margin - off with it first
        img = img.crop(img.split()[-1].getbbox())
        if box:
            img = img.crop(box)
        flat = Image.new("RGB", img.size, (255, 255, 255))
        flat.paste(img, mask=img.split()[-1])
        return flat

    # THE CARDS ARE FOUND, NOT MEASURED: white panels on the grey window,
    # read down a column just inside their left edge. Fixed coordinates
    # broke the first time the bottom card changed height.
    def cards(name):
        img = Image.open(os.path.join(folder, name)).convert("RGB")
        x = 45
        runs, start = [], None
        for y in range(img.height):
            white = min(img.getpixel((x, y))) >= 250
            if white and start is None:
                start = y
            elif not white and start is not None:
                if y - start > 100:
                    runs.append((start, y))
                start = None
        return runs

    def card(name, index, left=25, right=1543):
        top, bottom = cards(name)[index]
        return crop(name, (left, top - 10, right, bottom + 10))

    main = "autovent-1.5x.png"
    return {
        "main": crop(main),
        "links": card(main, 0, 25, 780),
        "schedule": card(main, 0, 787, 1543),
        "levels": card(main, 1),
        "assign": card(main, 2),
        "export": card("autovent-export-1.5x.png", 2),
        "interiors": crop("autovent-interiors-1.5x.png"),
        "arrange": crop("autovent-arrange-1.5x.png"),
    }


# ---------------------------------------------------------------- OOXML
NS = ('xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" '
      'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" '
      'xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"')
REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/"
CT = "application/vnd.openxmlformats-officedocument.presentationml."


def emu(inches):
    return int(round(inches * EMU))


def attr(text):
    return escape(text, {'"': "&quot;"})


def colour_xml(colour):
    name = colour
    if name in MAP or name in SCHEME:
        return '<a:schemeClr val="%s"/>' % name
    return '<a:srgbClr val="%s"/>' % colour


def fill_xml(colour):
    if colour is None:
        return "<a:noFill/>"
    return "<a:solidFill>%s</a:solidFill>" % colour_xml(colour)


def run_xml(r):
    face = "+mj-lt" if r["font"] == "major" else "+mn-lt"
    attrs = 'lang="en-GB" sz="%d"' % round(r["size"] * 100)
    if r["bold"]:
        attrs += ' b="1"'
    if r["italic"]:
        attrs += ' i="1"'
    return ('<a:rPr %s dirty="0">%s<a:latin typeface="%s"/></a:rPr>'
            % (attrs, fill_xml(r["color"]), face))


def para_xml(p):
    ppr = 'algn="%s"' % p["align"]
    kids = ""
    if p["bullet"]:
        ppr += ' marL="%d" indent="%d"' % (emu(0.3), -emu(0.3))
    if p["before"]:
        kids += '<a:spcBef><a:spcPts val="%d"/></a:spcBef>' % (p["before"] * 100)
    if p["after"]:
        kids += '<a:spcAft><a:spcPts val="%d"/></a:spcAft>' % (p["after"] * 100)
    if p["bullet"]:
        kids += ('<a:buClr>%s</a:buClr><a:buFont typeface="Arial"/>'
                 '<a:buChar char="&#8226;"/>' % colour_xml("accent1"))
    else:
        kids += "<a:buNone/>"
    out = "<a:p><a:pPr %s>%s</a:pPr>" % (ppr, kids)
    first = p["runs"][0] if p["runs"] else R("", p["size"])
    if p["field"] is not None:
        out += ('<a:fld id="{B6F15528-21DE-4FAA-801E-634DDDAF4B2B}" '
                'type="slidenum">%s<a:t>%s</a:t></a:fld>'
                % (run_xml(first), escape(p["field"])))
    else:
        for r in p["runs"]:
            if r["text"] == "":
                continue
            out += "<a:r>%s<a:t>%s</a:t></a:r>" % (run_xml(r), escape(r["text"]))
    out += ('<a:endParaRPr lang="en-GB" sz="%d" dirty="0"/></a:p>'
            % round(first["size"] * 100))
    return out


def body_xml(item):
    l, t, r, b = item["ins"]
    out = ('<p:txBody><a:bodyPr wrap="square" lIns="%d" tIns="%d" rIns="%d" '
           'bIns="%d" anchor="%s" rtlCol="0"><a:noAutofit/></a:bodyPr>'
           '<a:lstStyle/>' % (emu(l), emu(t), emu(r), emu(b), item["anchor"]))
    paras = item["paras"] or [P("", size=12)]
    out += "".join(para_xml(p) for p in paras)
    return out + "</p:txBody>"


def xfrm_xml(x, y, w, h, rot=0, flipv=False, fliph=False):
    a = ""
    if rot:
        a += ' rot="%d"' % (rot * 60000)
    if fliph:
        a += ' flipH="1"'
    if flipv:
        a += ' flipV="1"'
    return ('<a:xfrm%s><a:off x="%d" y="%d"/><a:ext cx="%d" cy="%d"/></a:xfrm>'
            % (a, emu(x), emu(y), emu(w), emu(h)))


def geom_xml(item):
    av = "<a:avLst/>"
    if item["adj"] is not None:
        av = '<a:avLst><a:gd name="adj" fmla="val %d"/></a:avLst>' % item["adj"]
    return '<a:prstGeom prst="%s">%s</a:prstGeom>' % (item["geom"], av)


def line_xml(colour, lw):
    if colour is None:
        return "<a:ln><a:noFill/></a:ln>"
    return '<a:ln w="%d">%s</a:ln>' % (round(lw * 12700), fill_xml(colour))


def sp_xml(item, sid):
    name = attr(item["name"] or ("Shape %d" % sid))
    if item["is_title"]:
        nv = ('<p:nvSpPr><p:cNvPr id="%d" name="%s"/><p:cNvSpPr>'
              '<a:spLocks noGrp="1"/></p:cNvSpPr><p:nvPr><p:ph type="title"/>'
              '</p:nvPr></p:nvSpPr>' % (sid, name))
    else:
        box = ' txBox="1"' if item["text_box"] else ""
        nv = ('<p:nvSpPr><p:cNvPr id="%d" name="%s"/><p:cNvSpPr%s/><p:nvPr/>'
              '</p:nvSpPr>' % (sid, name, box))
    sppr = ("<p:spPr>%s%s%s%s</p:spPr>"
            % (xfrm_xml(item["x"], item["y"], item["w"], item["h"],
                        item["rot"], item["flipv"]),
               geom_xml(item), fill_xml(item["fill"]),
               line_xml(item["line"], item["lw"])))
    body = body_xml(item) if (item["paras"] or item["text_box"]
                              or item["is_title"]) else ""
    return "<p:sp>%s%s%s</p:sp>" % (nv, sppr, body)


def pic_xml(item, sid, rid):
    return ('<p:pic><p:nvPicPr><p:cNvPr id="%d" name="Screenshot %d" descr="%s"/>'
            '<p:cNvPicPr><a:picLocks noChangeAspect="1"/></p:cNvPicPr><p:nvPr/>'
            '</p:nvPicPr><p:blipFill><a:blip r:embed="%s"/><a:stretch>'
            '<a:fillRect/></a:stretch></p:blipFill><p:spPr>%s'
            '<a:prstGeom prst="rect"><a:avLst/></a:prstGeom>%s</p:spPr></p:pic>'
            % (sid, sid, attr(item["descr"]), rid,
               xfrm_xml(item["x"], item["y"], item["w"], item["h"]),
               line_xml(item["border"], 1.0)))


def cxn_xml(item, sid):
    x1, y1, x2, y2 = item["x1"], item["y1"], item["x2"], item["y2"]
    head = '<a:tailEnd type="triangle" w="med" len="med"/>' if item["head"] else ""
    return ('<p:cxnSp><p:nvCxnSpPr><p:cNvPr id="%d" name="Arrow %d"/>'
            '<p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr><p:spPr>%s'
            '<a:prstGeom prst="straightConnector1"><a:avLst/></a:prstGeom>'
            '<a:ln w="%d">%s%s</a:ln></p:spPr></p:cxnSp>'
            % (sid, sid, xfrm_xml(min(x1, x2), min(y1, y2), abs(x2 - x1),
                                  abs(y2 - y1), 0, y2 < y1, x2 < x1),
               round(item["lw"] * 12700), fill_xml(item["color"]), head))


GROUP = ('<p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/>'
         '</p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/>'
         '<a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/>'
         '</a:xfrm></p:grpSpPr>')


def slide_xml(slide):
    shapes = []
    sid = 2
    for item in slide.items:
        if item["kind"] == "sp":
            shapes.append(sp_xml(item, sid))
        elif item["kind"] == "pic":
            shapes.append(pic_xml(item, sid, "rId%d" % (item["index"] + 1)))
        else:
            shapes.append(cxn_xml(item, sid))
        sid += 1
    bg = ""
    if slide.dark:
        bg = ('<p:bg><p:bgPr>%s<a:effectLst/></p:bgPr></p:bg>'
              % fill_xml("tx2"))
    return ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<p:sld %s><p:cSld>%s<p:spTree>%s%s</p:spTree></p:cSld>'
            '<p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>'
            % (NS, bg, GROUP, "".join(shapes)))


def theme_xml():
    order = ("dk1", "lt1", "dk2", "lt2", "accent1", "accent2", "accent3",
             "accent4", "accent5", "accent6", "hlink", "folHlink")
    colours = "".join('<a:%s><a:srgbClr val="%s"/></a:%s>' % (k, SCHEME[k], k)
                      for k in order)
    fonts = "".join(
        '<a:%s><a:latin typeface="%s"/><a:ea typeface=""/><a:cs typeface=""/>'
        '</a:%s>' % (tag, attr(FONTS[key]), tag)
        for tag, key in (("majorFont", "major"), ("minorFont", "minor")))
    solid = '<a:solidFill><a:schemeClr val="phClr"/></a:solidFill>'
    lines = "".join('<a:ln w="%d" cap="flat" cmpd="sng" algn="ctr">%s'
                    '<a:prstDash val="solid"/><a:miter lim="800000"/></a:ln>'
                    % (w, solid) for w in (6350, 12700, 19050))
    effects = "<a:effectStyle><a:effectLst/></a:effectStyle>" * 3
    return ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" '
            'name="AUTOVENT"><a:themeElements><a:clrScheme name="AUTOVENT">%s'
            '</a:clrScheme><a:fontScheme name="AUTOVENT">%s</a:fontScheme>'
            '<a:fmtScheme name="AUTOVENT"><a:fillStyleLst>%s%s%s</a:fillStyleLst>'
            '<a:lnStyleLst>%s</a:lnStyleLst><a:effectStyleLst>%s</a:effectStyleLst>'
            '<a:bgFillStyleLst>%s%s%s</a:bgFillStyleLst></a:fmtScheme>'
            '</a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>'
            % (colours, fonts, solid, solid, solid, lines, effects,
               solid, solid, solid))


def title_ph(xml_xfrm):
    return ('<p:sp><p:nvSpPr><p:cNvPr id="2" name="Title 1"/><p:cNvSpPr>'
            '<a:spLocks noGrp="1"/></p:cNvSpPr><p:nvPr><p:ph type="title"/>'
            '</p:nvPr></p:nvSpPr><p:spPr>%s</p:spPr><p:txBody>'
            '<a:bodyPr anchor="ctr"/><a:lstStyle/><a:p><a:r><a:rPr lang="en-GB"/>'
            '<a:t>Title</a:t></a:r></a:p></p:txBody></p:sp>' % xml_xfrm)


def master_xml():
    def level(size, colour, face):
        return ('<a:lvl1pPr marL="0" indent="0" algn="l"><a:buNone/>'
                '<a:defRPr sz="%d" kern="1200">%s<a:latin typeface="%s"/>'
                '</a:defRPr></a:lvl1pPr>' % (size, fill_xml(colour), face))
    clr = ('<p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" '
           'accent2="accent2" accent3="accent3" accent4="accent4" '
           'accent5="accent5" accent6="accent6" hlink="hlink" '
           'folHlink="folHlink"/>')
    return ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<p:sldMaster %s><p:cSld><p:bg><p:bgRef idx="1001">'
            '<a:schemeClr val="bg1"/></p:bgRef></p:bg><p:spTree>%s%s</p:spTree>'
            '</p:cSld>%s<p:sldLayoutIdLst><p:sldLayoutId id="2147483649" '
            'r:id="rId1"/></p:sldLayoutIdLst><p:txStyles><p:titleStyle>%s'
            '</p:titleStyle><p:bodyStyle>%s</p:bodyStyle><p:otherStyle>%s'
            '</p:otherStyle></p:txStyles></p:sldMaster>'
            % (NS, GROUP, title_ph(xfrm_xml(0.6, 0.42, 12.13, 0.85)), clr,
               level(3200, "tx2", "+mj-lt"), level(1600, "tx1", "+mn-lt"),
               level(1600, "tx1", "+mn-lt")))


def layout_xml():
    return ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<p:sldLayout %s type="titleOnly" preserve="1"><p:cSld name="Title Only">'
            '<p:spTree>%s%s</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/>'
            '</p:clrMapOvr></p:sldLayout>'
            % (NS, GROUP, title_ph(xfrm_xml(0.6, 0.42, 12.13, 0.85))))


def rels(entries):
    return ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            + "".join('<Relationship Id="%s" Type="%s" Target="%s"/>' % e
                      for e in entries) + "</Relationships>")


def write_pptx(slides, path):
    n = len(slides)
    files = {}
    over = [("/ppt/presentation.xml", CT + "presentation.main+xml"),
            ("/ppt/slideMasters/slideMaster1.xml", CT + "slideMaster+xml"),
            ("/ppt/slideLayouts/slideLayout1.xml", CT + "slideLayout+xml"),
            ("/ppt/theme/theme1.xml",
             "application/vnd.openxmlformats-officedocument.theme+xml"),
            ("/ppt/presProps.xml", CT + "presProps+xml"),
            ("/ppt/viewProps.xml", CT + "viewProps+xml"),
            ("/ppt/tableStyles.xml", CT + "tableStyles+xml"),
            ("/docProps/core.xml",
             "application/vnd.openxmlformats-package.core-properties+xml"),
            ("/docProps/app.xml",
             "application/vnd.openxmlformats-officedocument.extended-properties+xml")]
    media = 0
    for i, slide in enumerate(slides, 1):
        over.append(("/ppt/slides/slide%d.xml" % i, CT + "slide+xml"))
        files["ppt/slides/slide%d.xml" % i] = slide_xml(slide)
        entries = [("rId1", REL + "slideLayout", "../slideLayouts/slideLayout1.xml")]
        for k, img in enumerate(slide.images, 2):
            media += 1
            buf = io.BytesIO()
            img.save(buf, "PNG", optimize=True)
            files["ppt/media/image%d.png" % media] = buf.getvalue()
            entries.append(("rId%d" % k, REL + "image",
                            "../media/image%d.png" % media))
        files["ppt/slides/_rels/slide%d.xml.rels" % i] = rels(entries)

    files["[Content_Types].xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
        '<Default Extension="xml" ContentType="application/xml"/>'
        '<Default Extension="png" ContentType="image/png"/>'
        + "".join('<Override PartName="%s" ContentType="%s"/>' % o for o in over)
        + "</Types>")
    files["_rels/.rels"] = rels([
        ("rId1", REL + "officeDocument", "ppt/presentation.xml"),
        ("rId2", "http://schemas.openxmlformats.org/package/2006/relationships/"
                 "metadata/core-properties", "docProps/core.xml"),
        ("rId3", REL + "extended-properties", "docProps/app.xml")])

    pres_rels = [("rId1", REL + "slideMaster", "slideMasters/slideMaster1.xml")]
    pres_rels += [("rId%d" % (i + 1), REL + "slide", "slides/slide%d.xml" % i)
                  for i in range(1, n + 1)]
    pres_rels += [("rId%d" % (n + 2), REL + "presProps", "presProps.xml"),
                  ("rId%d" % (n + 3), REL + "viewProps", "viewProps.xml"),
                  ("rId%d" % (n + 4), REL + "theme", "theme/theme1.xml"),
                  ("rId%d" % (n + 5), REL + "tableStyles", "tableStyles.xml")]
    files["ppt/_rels/presentation.xml.rels"] = rels(pres_rels)

    ids = {id(sl): 256 + i for i, sl in enumerate(slides)}
    sections = "".join(
        '<p14:section name="%s" id="{%s}"><p14:sldIdLst>%s</p14:sldIdLst>'
        '</p14:section>' % (attr(name), str(uuid.uuid5(uuid.NAMESPACE_URL,
                                                       "autovent-deck/" + name)).upper(),
                            "".join('<p14:sldId id="%d"/>' % ids[id(sl)]
                                    for sl in members))
        for name, members in SECTIONS)
    files["ppt/presentation.xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<p:presentation %s saveSubsetFonts="1"><p:sldMasterIdLst>'
        '<p:sldMasterId id="2147483648" r:id="rId1"/></p:sldMasterIdLst>'
        '<p:sldIdLst>%s</p:sldIdLst><p:sldSz cx="12192000" cy="6858000"/>'
        '<p:notesSz cx="6858000" cy="9144000"/><p:defaultTextStyle>'
        '<a:lvl1pPr marL="0" algn="l"><a:defRPr sz="1800" kern="1200">'
        '<a:solidFill><a:schemeClr val="tx1"/></a:solidFill>'
        '<a:latin typeface="+mn-lt"/></a:defRPr></a:lvl1pPr></p:defaultTextStyle>'
        '<p:extLst><p:ext uri="{521415D9-36F7-43E2-AB2F-B90AF26B5E84}">'
        '<p14:sectionLst xmlns:p14="http://schemas.microsoft.com/office/powerpoint/2010/main">'
        '%s</p14:sectionLst></p:ext></p:extLst></p:presentation>'
        % (NS, "".join('<p:sldId id="%d" r:id="rId%d"/>' % (256 + i, i + 2)
                       for i in range(n)), sections))
    files["ppt/slideMasters/slideMaster1.xml"] = master_xml()
    files["ppt/slideMasters/_rels/slideMaster1.xml.rels"] = rels([
        ("rId1", REL + "slideLayout", "../slideLayouts/slideLayout1.xml"),
        ("rId2", REL + "theme", "../theme/theme1.xml")])
    files["ppt/slideLayouts/slideLayout1.xml"] = layout_xml()
    files["ppt/slideLayouts/_rels/slideLayout1.xml.rels"] = rels([
        ("rId1", REL + "slideMaster", "../slideMasters/slideMaster1.xml")])
    files["ppt/theme/theme1.xml"] = theme_xml()
    files["ppt/presProps.xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<p:presentationPr %s/>' % NS)
    files["ppt/viewProps.xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<p:viewPr %s><p:normalViewPr><p:restoredLeft sz="15620"/>'
        '<p:restoredTop sz="94660"/></p:normalViewPr><p:gridSpacing cx="76200" '
        'cy="76200"/></p:viewPr>' % NS)
    files["ppt/tableStyles.xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<a:tblStyleLst xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" '
        'def="{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}"/>')
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    files["docProps/core.xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" '
        'xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" '
        'xmlns:dcmitype="http://purl.org/dc/dcmitype/" '
        'xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">'
        '<dc:title>AUTOVENT - a guide for Revit users</dc:title>'
        '<dc:creator>Atkins MEP Toolkit</dc:creator>'
        '<dcterms:created xsi:type="dcterms:W3CDTF">%s</dcterms:created>'
        '<dcterms:modified xsi:type="dcterms:W3CDTF">%s</dcterms:modified>'
        '</cp:coreProperties>' % (now, now))
    files["docProps/app.xml"] = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" '
        'xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">'
        '<Application>Microsoft Office PowerPoint</Application>'
        '<PresentationFormat>Widescreen</PresentationFormat><Slides>%d</Slides>'
        '</Properties>' % n)

    if os.path.exists(path):
        os.remove(path)
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        # the content types first, as Office writes them
        z.writestr("[Content_Types].xml", files.pop("[Content_Types].xml"))
        for name in sorted(files):
            data = files[name]
            z.writestr(name, data if isinstance(data, bytes) else data.encode("utf-8"))


# ---------------------------------------------------------------- previews
FONT_FILES = {("major", False, False): "seguisb.ttf",
              ("major", True, False): "segoeuib.ttf",
              ("minor", False, False): "segoeui.ttf",
              ("minor", True, False): "segoeuib.ttf",
              ("minor", False, True): "segoeuii.ttf",
              ("major", False, True): "seguisbi.ttf"}
_fonts = {}


def font(r, scale=DPI):
    key = (r["font"], r["bold"], r["italic"], r["size"])
    if key not in _fonts:
        name = FONT_FILES.get((r["font"], r["bold"], r["italic"]), "segoeui.ttf")
        path = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", name)
        _fonts[key] = ImageFont.truetype(path, size=max(1, round(r["size"] * scale / 72)))
    return _fonts[key]


def rgb(colour):
    h = hexof(colour)
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def px(v):
    return round(v * DPI)


def layout_text(item, problems, where):
    """Lines of (x offset, [(text, run)...], ascent, height) for one text
    body, wrapped as PowerPoint wraps it; reports what will not fit."""
    l, t, r, b = item["ins"]
    width = (item["w"] - l - r) * DPI
    lines = []
    for p in item["paras"]:
        runs = p["runs"] if p["field"] is None else [dict(p["runs"][0] if p["runs"] else R("", p["size"]), text=p["field"])]
        indent = 0.3 * DPI if p["bullet"] else 0
        avail = width - indent
        words = []
        for run in runs:
            parts = run["text"].split(" ")
            for k, part in enumerate(parts):
                piece = part + (" " if k < len(parts) - 1 else "")
                if piece:
                    words.append((piece, run))
        current, used, para_lines = [], 0.0, []
        for piece, run in words:
            w = font(run).getlength(piece)
            if current and used + font(run).getlength(piece.rstrip()) > avail:
                para_lines.append(current)
                current, used = [], 0.0
            if not current and font(run).getlength(piece.rstrip()) > avail:
                problems.append("%s: '%s' is wider than its box" % (where, piece.strip()))
            current.append((piece, run))
            used += w
        para_lines.append(current)
        first = True
        for line in para_lines:
            sized = [run for _, run in line] or [R("", p["size"])]
            asc = max(font(run).getmetrics()[0] for run in sized)
            desc = max(font(run).getmetrics()[1] for run in sized)
            lines.append({"x": indent, "parts": line, "asc": asc,
                          "h": asc + desc, "bullet": p["bullet"] and first,
                          "before": p["before"] * DPI / 72 if first else 0,
                          "align": p["align"], "avail": avail, "after": 0})
            first = False
        lines[-1]["after"] = p["after"] * DPI / 72
    total = sum(ln["before"] + ln["h"] + ln["after"] for ln in lines)
    if lines:
        total -= lines[-1]["after"]
    room = (item["h"] - t - b) * DPI
    if total > room + 1:
        problems.append("%s: text needs %.2f in, box has %.2f in"
                        % (where, total / DPI, room / DPI))
    return lines, total


def polygon(item):
    x, y, w, h = px(item["x"]), px(item["y"]), px(item["w"]), px(item["h"])
    if item["geom"] == "trapezoid":
        a = (item["adj"] if item["adj"] is not None else 25000) / 100000 * min(w, h)
        pts = [(0, h), (a, 0), (w - a, 0), (w, h)]
    else:
        pts = [(0, 0), (w, 0), (w, h), (0, h)]
    if item["flipv"]:
        pts = [(u, h - v) for u, v in pts]
    return [(x + u, y + v) for u, v in pts]


def draw_slide(slide, number, path, problems):
    img = Image.new("RGB", (px(SW), px(SH)), rgb("tx2" if slide.dark else "bg1"))
    d = ImageDraw.Draw(img)
    for n, item in enumerate(slide.items):
        where = "slide %d '%s' #%d %s" % (number, slide.title, n, item.get("name") or item["kind"])
        if item["kind"] == "pic":
            box = (px(item["x"]), px(item["y"]), px(item["x"] + item["w"]), px(item["y"] + item["h"]))
            img.paste(item["img"].resize((box[2] - box[0], box[3] - box[1]), Image.LANCZOS), box[:2])
            if item["border"]:
                d.rectangle(box, outline=rgb(item["border"]), width=2)
            if box[2] > px(SW) or box[3] > px(SH):
                problems.append(where + ": off the slide")
            continue
        if item["kind"] == "cxn":
            col = rgb(item["color"])
            w = max(1, round(item["lw"] * DPI / 72))
            x1, y1, x2, y2 = (px(item["x1"]), px(item["y1"]), px(item["x2"]), px(item["y2"]))
            d.line((x1, y1, x2, y2), fill=col, width=w)
            if item["head"]:
                import math
                ang = math.atan2(y2 - y1, x2 - x1)
                size = 3 * w + 6
                left = (x2 - size * math.cos(ang - 0.45), y2 - size * math.sin(ang - 0.45))
                right = (x2 - size * math.cos(ang + 0.45), y2 - size * math.sin(ang + 0.45))
                d.polygon([(x2, y2), left, right], fill=col)
            continue
        box = (px(item["x"]), px(item["y"]), px(item["x"] + item["w"]), px(item["y"] + item["h"]))
        if box[2] > px(SW) + 1 or box[3] > px(SH) + 1:
            problems.append(where + ": off the slide")
        fill = rgb(item["fill"]) if item["fill"] else None
        outline = rgb(item["line"]) if item["line"] else None
        width = max(1, round(item["lw"] * DPI / 72)) if outline else 0
        if fill or outline:
            if item["geom"] == "roundRect":
                a = (item["adj"] if item["adj"] is not None else 16667) / 100000
                rad = a * min(box[2] - box[0], box[3] - box[1])
                d.rounded_rectangle(box, radius=rad, fill=fill, outline=outline, width=width)
            elif item["geom"] == "ellipse":
                d.ellipse(box, fill=fill, outline=outline, width=width)
            elif item["geom"] in ("trapezoid",):
                d.polygon(polygon(item), fill=fill, outline=outline)
            else:
                d.rectangle(box, fill=fill, outline=outline, width=width)
        if not item["paras"]:
            continue
        lines, total = layout_text(item, problems, where)
        l, t, r, b = item["ins"]
        top = box[1] + t * DPI
        room = (item["h"] - t - b) * DPI
        if item["anchor"] == "ctr":
            top += (room - total) / 2
        elif item["anchor"] == "b":
            top += room - total
        y = top
        for ln in lines:
            y += ln["before"]
            widths = sum(font(run).getlength(text) for text, run in ln["parts"])
            if ln["parts"]:
                widths -= font(ln["parts"][-1][1]).getlength(ln["parts"][-1][0]) - \
                    font(ln["parts"][-1][1]).getlength(ln["parts"][-1][0].rstrip())
            x = box[0] + l * DPI + ln["x"]
            if ln["align"] == "ctr":
                x += (ln["avail"] - widths) / 2
            elif ln["align"] == "r":
                x += ln["avail"] - widths
            if ln["bullet"] and ln["parts"]:
                run = ln["parts"][0][1]
                d.text((box[0] + l * DPI, y + ln["asc"]), "\u2022",
                       font=font(run), fill=rgb("accent1"), anchor="ls")
            for text, run in ln["parts"]:
                d.text((x, y + ln["asc"]), text, font=font(run), fill=rgb(run["color"]), anchor="ls")
                x += font(run).getlength(text)
            y += ln["h"] + ln["after"]
    img.save(path)


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    renders, out = sys.argv[1], sys.argv[2]
    previews = sys.argv[3] if len(sys.argv) > 3 else None
    slides = build(shots_from(renders))
    write_pptx(slides, out)
    print("wrote %s - %d slides, %d KB" % (out, len(slides), os.path.getsize(out) // 1024))
    if previews:
        os.makedirs(previews, exist_ok=True)
        problems = []
        for i, slide in enumerate(slides, 1):
            draw_slide(slide, i, os.path.join(previews, "slide-%02d.png" % i), problems)
        print("previews in %s" % previews)
        for p in problems:
            print("  FIT  " + p)
        if not problems:
            print("  every text box fits")
    return 0


if __name__ == "__main__":
    sys.exit(main())
