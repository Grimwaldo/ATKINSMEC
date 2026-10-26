"""Draws the eleven ribbon icons for ATKINSMEC.

Run it from the repository root:

    python tools/make-icons.py

It writes ATKINSMEC/Resources/Icons/<name>32.png and <name>16.png, which
the csproj embeds and Shared/UI.cs loads onto the ribbon buttons.

WHY GENERATED RATHER THAN DRAWN BY HAND: nine icons that have to look
like one set is a consistency problem, not an art problem. Everything
here comes off one palette, one stroke width and one grid, so a tenth
add-in gets an icon that matches by construction. Replace any PNG with a
designer's and nothing else has to change - the loader only wants a file
of the right name.

Everything is drawn at 256 px and downsampled with LANCZOS. Drawing
straight at 32 px gives ragged diagonals; drawing big and shrinking is
what makes the arrowheads and the droplet survive.
"""

import math
import os

from PIL import Image, ImageDraw

# ----------------------------------------------------------------------
# One palette for all nine
# ----------------------------------------------------------------------

BLUE = (29, 92, 150, 255)       # the ribbon's own blue, as the windows use
DEEP = (18, 60, 100, 255)       # a darker blue for a second plane
ORANGE = (222, 116, 38, 255)    # the thing that MOVES or CHANGES
GREY = (128, 139, 150, 255)     # context: what is not the point
PALE = (198, 208, 217, 255)
GREEN = (44, 138, 86, 255)      # only SHEET QA, only the tick
WHITE = (255, 255, 255, 255)

S = 256                          # working size
STROKE = 20                      # main stroke, survives to ~2.5 px at 32


def canvas():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def rrect(d, box, r, fill=None, outline=None, width=STROKE):
    d.rounded_rectangle(box, radius=r, fill=fill, outline=outline,
                        width=width)


def line(d, a, b, colour, width=STROKE):
    d.line([a, b], fill=colour, width=width)


def arrow(d, a, b, colour, width=STROKE, head=46):
    """A straight shaft with a solid triangular head at b."""
    ax, ay = a
    bx, by = b

    angle = math.atan2(by - ay, bx - ax)

    # Stop the shaft short so it does not poke through the head
    sx = bx - math.cos(angle) * head * 0.72
    sy = by - math.sin(angle) * head * 0.72

    d.line([(ax, ay), (sx, sy)], fill=colour, width=width)

    left = angle + math.radians(140)
    right = angle - math.radians(140)

    d.polygon(
        [(bx, by),
         (bx + math.cos(left) * head, by + math.sin(left) * head),
         (bx + math.cos(right) * head, by + math.sin(right) * head)],
        fill=colour)


def head_at(d, centre, angle_deg, colour, size=44):
    """An arrowhead on its own, for the ends of an arc."""
    cx, cy = centre
    a = math.radians(angle_deg)

    left = a + math.radians(140)
    right = a - math.radians(140)

    d.polygon(
        [(cx, cy),
         (cx + math.cos(left) * size, cy + math.sin(left) * size),
         (cx + math.cos(right) * size, cy + math.sin(right) * size)],
        fill=colour)


# ----------------------------------------------------------------------
# The nine
# ----------------------------------------------------------------------

def autovent():
    """A supply grille with air coming down into it."""
    img, d = canvas()

    arrow(d, (128, 18), (128, 92), ORANGE, STROKE, 44)

    rrect(d, (40, 108, 216, 226), 16, outline=BLUE, width=STROKE)

    for y in (142, 168, 194):
        line(d, (74, y), (182, y), BLUE, 14)

    return img


def chwvolume():
    """A droplet with the level it is filled to."""
    img, d = canvas()

    # Teardrop: a circle with a triangular cap
    d.ellipse((66, 104, 190, 228), fill=BLUE)
    d.polygon([(128, 24), (72, 136), (184, 136)], fill=BLUE)

    # The measured level
    d.rectangle((76, 168, 180, 186), fill=WHITE)

    return img


def ductinsulation():
    """A duct inside its wrap.

    The first version hatched the wrap, which at 32 px was a grey
    smudge. Two concentric shapes say "wrapped" on their own and
    survive the downsample - the detail was costing the meaning.
    """
    img, d = canvas()

    rrect(d, (24, 54, 232, 202), 18, outline=ORANGE, width=26)
    rrect(d, (74, 100, 182, 156), 10, fill=BLUE)

    return img


def sprinklers():
    """A pendent head and its spray.

    Four small ellipses for the spray read as specks at 32 px. Three
    THICK fanned strokes carry the same idea and are still there after
    the downsample.
    """
    img, d = canvas()

    line(d, (128, 20), (128, 62), GREY, 24)          # the drop
    d.ellipse((100, 54, 156, 110), fill=BLUE)        # the body
    line(d, (64, 118), (192, 118), BLUE, 22)         # the deflector

    # The spray, fanned - bold enough to survive
    line(d, (96, 142), (56, 216), BLUE, 20)
    line(d, (128, 146), (128, 224), BLUE, 20)
    line(d, (160, 142), (200, 216), BLUE, 20)

    return img


def alignterminals():
    """One terminal moved onto the line of another."""
    img, d = canvas()

    # A SOLID guide: dashes vanish entirely at 32 px
    line(d, (195, 24), (195, 232), PALE, 12)

    rrect(d, (22, 92, 108, 178), 12, outline=GREY, width=18)
    rrect(d, (152, 92, 238, 178), 12, fill=BLUE)

    arrow(d, (112, 135), (146, 135), ORANGE, 20, 36)

    return img


def sheetqa():
    """A sheet, checked."""
    img, d = canvas()

    rrect(d, (44, 26, 196, 208), 12, fill=WHITE, outline=BLUE,
          width=STROKE)

    for y in (78, 112, 146):
        line(d, (76, y), (164, y), PALE, 14)

    # The tick sits over the corner, so it reads first
    d.line([(128, 186), (168, 224), (236, 142)], fill=GREEN, width=30,
           joint="curve")

    return img


def autosync():
    """Two arrows round a circle."""
    img, d = canvas()

    # The heads are PUT ON THE ARC rather than guessed at: the first
    # version placed them by eye and left a notch where they missed.
    cx, cy, r = 128, 128, 82

    box = (cx - r, cy - r, cx + r, cy + r)

    d.arc(box, 205, 20, fill=BLUE, width=STROKE)
    d.arc(box, 25, 200, fill=BLUE, width=STROKE)

    for end in (20, 200):
        a = math.radians(end)

        head_at(d,
                (cx + math.cos(a) * r, cy + math.sin(a) * r),
                end + 90, BLUE, 46)

    return img


def nwcbatch():
    """A stack of models going out."""
    img, d = canvas()

    rrect(d, (26, 34, 150, 158), 10, fill=PALE)
    rrect(d, (48, 60, 172, 184), 10, fill=GREY)
    rrect(d, (70, 86, 194, 210), 10, fill=BLUE)

    arrow(d, (150, 148), (238, 148), ORANGE, 20, 42)

    return img


def equipchanger():
    """A unit, swapped."""
    img, d = canvas()

    # No detail inside the box - at 32 px it only muddied the blue
    rrect(d, (78, 92, 178, 164), 12, fill=BLUE)

    # Swap arrows over and under it
    arrow(d, (24, 50), (188, 50), ORANGE, 20, 40)
    arrow(d, (232, 208), (68, 208), ORANGE, 20, 40)

    return img


def ductesp():
    """A unit, the duct leaving it, and a pressure gauge on the duct."""
    img, d = canvas()

    rrect(d, (22, 150, 106, 222), 12, fill=BLUE)

    # The duct: out of the unit and up to the gauge, one thick stroke so
    # it survives at 32 px
    d.line([(106, 186), (176, 186), (176, 146)], fill=BLUE, width=26,
           joint="curve")

    # The gauge: an orange ring with a needle - the thing being measured
    d.ellipse((112, 26, 240, 154), outline=ORANGE, width=20)
    line(d, (176, 90), (214, 58), ORANGE, 18)
    d.ellipse((164, 78, 188, 102), fill=ORANGE)

    return img


def roomtags():
    """A room, and the tag that goes in it."""
    img, d = canvas()

    # The room: its walls, with a door left open so it reads as a plan
    d.line([(150, 30), (30, 30), (30, 226), (226, 226), (226, 112)],
           fill=BLUE, width=STROKE, joint="curve")

    # The tag: an orange label, its hole knocked out
    d.polygon([(64, 100), (152, 100), (196, 140), (152, 180), (64, 180)],
              fill=ORANGE)
    d.ellipse((146, 128, 170, 152), fill=WHITE)

    return img


ICONS = {
    "autovent": autovent,
    "chwvolume": chwvolume,
    "ductinsulation": ductinsulation,
    "sprinklers": sprinklers,
    "alignterminals": alignterminals,
    "sheetqa": sheetqa,
    "autosync": autosync,
    "nwcbatch": nwcbatch,
    "equipchanger": equipchanger,
    "ductesp": ductesp,
    "roomtags": roomtags,
}


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out = os.path.join(here, "..", "ATKINSMEC", "Resources", "Icons")
    out = os.path.normpath(out)

    if not os.path.isdir(out):
        os.makedirs(out)

    sheet = Image.new("RGBA", (len(ICONS) * 160, 200), (245, 247, 249, 255))

    for i, (name, draw) in enumerate(ICONS.items()):
        big = draw()

        for size in (32, 16):
            small = big.resize((size, size), Image.LANCZOS)

            small.save(os.path.join(out, "%s%d.png" % (name, size)))

        # A contact sheet, so the set can be LOOKED at rather than
        # trusted - the same argument as rendering a window
        sheet.paste(big.resize((128, 128), Image.LANCZOS),
                    (i * 160 + 16, 16), big.resize((128, 128), Image.LANCZOS))

        at32 = big.resize((32, 32), Image.LANCZOS)

        sheet.paste(at32, (i * 160 + 64, 156), at32)

    sheet.save(os.path.join(here, "icons-contact-sheet.png"))

    print("wrote %d icon(s) to %s" % (len(ICONS) * 2, out))


if __name__ == "__main__":
    main()
