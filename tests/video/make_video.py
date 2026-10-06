"""The AUTOVENT tutorial: 2 min 21 s, 1920x1080, 25 fps, no voice.

    python make_video.py            -> autovent_tutorial.mp4
    python make_video.py --stills   -> one PNG per scene, to look at first

Frames are drawn here and piped raw into mp4enc (Media Foundation), with
the original music from music.py underneath.

The window shots are the REAL VENTWindow.xaml, rendered by
scratchpad/ventpreview at 1.5x, and every highlight box comes from the
bounds WPF itself reported for that control - so a box lands on the
control, not near it.
"""

import json
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.path.join(HERE, 'shots')
ICONS = r'C:\Users\moham\source\repos\ATKINSMEC\ATKINSMEC\Resources\Icons'
ENC = os.path.join(HERE, 'mp4enc', 'bin', 'Release', 'net8.0-windows',
                   'mp4enc.exe')

W, H, FPS = 1920, 1080, 25
XFADE = 0.5

# ---------------------------------------------------------------------
# Look
# ---------------------------------------------------------------------

BLUE = (29, 92, 150)
DEEP = (18, 60, 100)
ORANGE = (222, 116, 38)
INK = (32, 45, 58)
BODY = (62, 76, 90)
MUTED = (120, 132, 145)
BG = (236, 241, 246)
PANEL = (255, 255, 255)
LINE = (206, 215, 224)
GREEN = (44, 138, 86)
PALE = (198, 208, 217)

FONTS = r'C:\Windows\Fonts'


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


F_BAR = font('segoeuib.ttf', 30)
F_BAR_SMALL = font('segoeuisl.ttf', 26)
F_STEP = font('seguisb.ttf', 24)
F_HEAD = font('segoeuib.ttf', 42)
F_BODY = font('segoeui.ttf', 29)
F_BADGE = font('segoeuib.ttf', 30)
F_TITLE = font('segoeuib.ttf', 120)
F_SUB = font('segoeuisl.ttf', 40)
F_SMALL = font('segoeui.ttf', 24)
F_LABEL = font('seguisb.ttf', 26)
F_TINY = font('segoeui.ttf', 20)
F_MONO = font('consola.ttf', 22)
F_CELL = font('segoeui.ttf', 22)
F_CELL_B = font('segoeuib.ttf', 22)

# The two halves of the frame
VIEW = (48, 112, 1228, 1012)          # the window / the diagram
CARD = (1268, 112, 1872, 1012)        # the words
BAR_H = 80

VW, VH = VIEW[2] - VIEW[0], VIEW[3] - VIEW[1]


def ease(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def ramp(t, start, length):
    return ease((t - start) / length)


def lerp(a, b, k):
    return a + (b - a) * k


def lerp_box(a, b, k):
    return tuple(lerp(a[i], b[i], k) for i in range(4))


def wrap(text, fnt, width):
    """Word-wrap, keeping blank lines as paragraph breaks."""
    lines = []

    for para in text.split('\n'):
        if para.strip() == '':
            lines.append('')
            continue

        words = para.split(' ')
        line = ''

        for word in words:
            trial = word if line == '' else line + ' ' + word

            if fnt.getlength(trial) <= width:
                line = trial
            else:
                lines.append(line)
                line = word

        lines.append(line)

    return lines


def shadowed(size, radius, fill, blur=18, offset=(0, 6), alpha=60):
    """A rounded panel with a soft shadow, as RGBA, with a margin."""
    m = blur * 2
    w, h = size

    shadow = Image.new('RGBA', (w + 2 * m, h + 2 * m), (0, 0, 0, 0))
    d = ImageDraw.Draw(shadow)
    d.rounded_rectangle((m + offset[0], m + offset[1],
                         m + w + offset[0], m + h + offset[1]),
                        radius, fill=(20, 40, 60, alpha))
    shadow = shadow.filter(ImageFilter.GaussianBlur(blur))

    d = ImageDraw.Draw(shadow)
    d.rounded_rectangle((m, m, m + w, m + h), radius, fill=fill + (255,))

    return shadow, m


# ---------------------------------------------------------------------
# The fixed parts of every frame
# ---------------------------------------------------------------------

def background():
    img = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(img)

    d.rectangle((0, 0, W, BAR_H), fill=BLUE)

    icon = Image.open(os.path.join(ICONS, 'autovent32.png')).convert('RGBA')
    icon = icon.resize((44, 44), Image.LANCZOS)

    # The icon on white, so it reads on the blue bar
    plate = Image.new('RGBA', (56, 56), (0, 0, 0, 0))
    ImageDraw.Draw(plate).rounded_rectangle((0, 0, 55, 55), 10,
                                            fill=(255, 255, 255, 255))
    plate.alpha_composite(icon, (6, 6))
    img.paste(plate, (40, 12), plate)

    d.text((112, 20), 'AUTOVENT', font=F_BAR, fill=(255, 255, 255))
    d.text((112 + F_BAR.getlength('AUTOVENT') + 18, 25), 'Quick guide',
           font=F_BAR_SMALL, fill=(200, 220, 240))

    return img


BASE = None


def card_layer(step, heading, body, total_steps=8):
    """The words on the right: a badge, a heading, a paragraph."""
    w = CARD[2] - CARD[0]
    h = CARD[3] - CARD[1]

    panel, m = shadowed((w, h), 22, PANEL)
    d = ImageDraw.Draw(panel)

    x = m + 48
    y = m + 52

    if step is not None:
        d.ellipse((x, y, x + 64, y + 64), fill=BLUE)
        label = str(step)
        tw = F_BADGE.getlength(label)
        d.text((x + 32 - tw / 2, y + 12), label, font=F_BADGE,
               fill=(255, 255, 255))

        d.text((x + 82, y + 18), 'STEP %d OF %d' % (step, total_steps),
               font=F_STEP, fill=MUTED)

        y += 100

    for line in wrap(heading, F_HEAD, w - 96):
        d.text((x, y), line, font=F_HEAD, fill=INK)
        y += 54

    y += 22
    d.line((x, y, x + 72, y), fill=ORANGE, width=5)
    y += 34

    for line in wrap(body, F_BODY, w - 96):
        if line == '':
            y += 20
            continue

        if line.startswith('\u2022 '):
            d.ellipse((x + 2, y + 16, x + 12, y + 26), fill=ORANGE)
            d.text((x + 26, y), line[2:], font=F_BODY, fill=BODY)
        else:
            d.text((x, y), line, font=F_BODY, fill=BODY)

        y += 43

    return panel, m


def step_label(img, step):
    if step is None:
        return

    d = ImageDraw.Draw(img)
    text = 'Step %d of 8' % step
    d.text((W - 48 - F_BAR_SMALL.getlength(text), 25), text,
           font=F_BAR_SMALL, fill=(200, 220, 240))


# ---------------------------------------------------------------------
# Scenes
# ---------------------------------------------------------------------

class Scene(object):
    step = None
    settle = 2.0            # after this the frame no longer changes

    def __init__(self, duration):
        self.duration = duration

    def frame(self, t):
        raise NotImplementedError


class Captioned(Scene):
    """Anything with the words on the right."""

    def __init__(self, duration, step, heading, body):
        Scene.__init__(self, duration)
        self.step = step
        self.heading = heading
        self.body = body
        self._card = None

    def put_card(self, img, t):
        if self._card is None:
            self._card = card_layer(self.step, self.heading, self.body)

        panel, m = self._card

        # Slides up a little as it arrives
        k = ramp(t, 0.15, 0.7)
        dy = int((1 - k) * 24)

        if k <= 0:
            return

        layer = panel

        if k < 1:
            layer = panel.copy()
            a = np.asarray(layer).copy()
            a[..., 3] = (a[..., 3] * k).astype(np.uint8)
            layer = Image.fromarray(a, 'RGBA')

        img.paste(layer, (CARD[0] - m, CARD[1] - m + dy), layer)


class WindowScene(Captioned):
    """The real window, the camera moving to one part of it, and that
    part lit while the rest is dimmed."""

    PAD = 120              # background round the shot, so the camera can
                           # frame an edge card without running off it

    _shots = {}

    def __init__(self, duration, step, heading, body, shot, camera,
                 spot=None, start=None):
        Captioned.__init__(self, duration, step, heading, body)
        self.shot = shot
        self.camera_box = camera
        self.spot_box = spot or camera
        self.start_rect = start
        self.settle = 2.2

    @classmethod
    def load(cls, name):
        if name not in cls._shots:
            png = Image.open(os.path.join(SHOTS, name + '.png'))
            png = png.convert('RGBA')

            # The render carries a TRANSPARENT margin round the window,
            # and converting straight to RGB turns it into a black band
            # down the right and along the bottom. Crop to what is
            # actually opaque; the bounds JSON counts from the top-left,
            # so it is unaffected.
            png = png.crop(png.getchannel('A').getbbox())

            with open(os.path.join(SHOTS, name + '.json')) as f:
                bounds = json.load(f)

            p = cls.PAD
            w, h = png.size

            canvas = Image.new('RGB', (w + 2 * p, h + 2 * p), BG)

            # A shadow under the window, so it sits on the page
            sh, m = shadowed((w, h), 10, (255, 255, 255), blur=22,
                             alpha=70)
            canvas.paste(sh, (p - m, p - m), sh)
            canvas.paste(png, (p, p), png)

            cls._shots[name] = (canvas, bounds)

        return cls._shots[name]

    def box(self, spec):
        """A box in canvas pixels: a name, a list of names (their
        union), or four numbers."""
        _, bounds = self.load(self.shot)
        p = self.PAD

        if isinstance(spec, str):
            spec = [spec]

        if isinstance(spec[0], str):
            xs0, ys0, xs1, ys1 = [], [], [], []

            for name in spec:
                b = bounds[name]
                xs0.append(b[0])
                ys0.append(b[1])
                xs1.append(b[2])
                ys1.append(b[3])

            spec = (min(xs0), min(ys0), max(xs1), max(ys1))

        return (spec[0] + p, spec[1] + p, spec[2] + p, spec[3] + p)

    def full(self):
        canvas, _ = self.load(self.shot)
        cw, ch = canvas.size
        aspect = VW / VH

        h = ch - 2 * self.PAD + 90
        w = h * aspect

        return (cw / 2 - w / 2, ch / 2 - h / 2, cw / 2 + w / 2, ch / 2 + h / 2)

    def frame_for(self, target):
        """The camera rectangle that frames a box, at the view's shape,
        never closer than 1.3x so the text stays sharp."""
        canvas, _ = self.load(self.shot)
        cw, ch = canvas.size
        aspect = VW / VH

        x0, y0, x1, y1 = target
        margin = 70

        w = (x1 - x0) + 2 * margin
        h = (y1 - y0) + 2 * margin

        if w / h < aspect:
            w = h * aspect
        else:
            h = w / aspect

        least = VW / 1.3

        if w < least:
            w = least
            h = w / aspect

        cx = (x0 + x1) / 2
        cy = (y0 + y1) / 2

        # Kept over the WINDOW, not the padding round it - clamped to the
        # canvas, a frame near an edge showed a band of empty dimmed
        # background. A little allowance keeps the shadow in view.
        slack = 40
        lo_x, hi_x = self.PAD - slack, cw - self.PAD + slack
        lo_y, hi_y = self.PAD - slack, ch - self.PAD + slack

        if w <= hi_x - lo_x:
            left = min(max(cx - w / 2, lo_x), hi_x - w)
        else:
            left = cw / 2 - w / 2

        if h <= hi_y - lo_y:
            top = min(max(cy - h / 2, lo_y), hi_y - h)
        else:
            top = ch / 2 - h / 2

        return (left, top, left + w, top + h)

    def camera(self, t):
        start = self.start_rect or self.full()
        end = self.frame_for(self.box(self.camera_box))

        return lerp_box(start, end, ramp(t, 0.35, 1.3))

    def end_rect(self):
        return self.frame_for(self.box(self.camera_box))

    def frame(self, t):
        img = BASE.copy()
        step_label(img, self.step)

        canvas, _ = self.load(self.shot)
        cam = self.camera(t)

        view = canvas.resize((VW, VH), Image.BICUBIC, box=cam,
                             reducing_gap=2.0)

        # ---- the spotlight ----
        k = ramp(t, 1.35, 0.6)

        if k > 0:
            sx0, sy0, sx1, sy1 = self.box(self.spot_box)

            sx = VW / (cam[2] - cam[0])
            sy = VH / (cam[3] - cam[1])

            r = (int((sx0 - cam[0]) * sx) - 10,
                 int((sy0 - cam[1]) * sy) - 10,
                 int((sx1 - cam[0]) * sx) + 10,
                 int((sy1 - cam[1]) * sy) + 10)

            mask = Image.new('L', (VW, VH), 0)
            ImageDraw.Draw(mask).rounded_rectangle(r, 14, fill=255)

            dim = Image.blend(view, Image.new('RGB', (VW, VH), (18, 32, 48)),
                              0.5 * k)
            view = Image.composite(view, dim, mask)

            d = ImageDraw.Draw(view)
            outline = tuple(int(lerp(c, o, k)) for c, o in
                            zip((255, 255, 255), ORANGE))
            d.rounded_rectangle(r, 14, outline=outline, width=5)

        # The view, with rounded corners
        corner = Image.new('L', (VW, VH), 0)
        ImageDraw.Draw(corner).rounded_rectangle((0, 0, VW - 1, VH - 1), 22,
                                                 fill=255)

        img.paste(view, VIEW[:2], corner)

        self.put_card(img, t)

        return img


class DiagramScene(Captioned):
    """An illustration drawn for the video, on a white panel."""

    def __init__(self, duration, step, heading, body, draw, settle):
        Captioned.__init__(self, duration, step, heading, body)
        self.draw = draw
        self.settle = settle
        self._panel = None

    def frame(self, t):
        img = BASE.copy()
        step_label(img, self.step)

        if self._panel is None:
            self._panel = shadowed((VW, VH), 22, PANEL)

        panel, m = self._panel
        img.paste(panel, (VIEW[0] - m, VIEW[1] - m), panel)

        layer = Image.new('RGBA', (VW, VH), (0, 0, 0, 0))
        self.draw(layer, t)
        img.paste(layer, VIEW[:2], layer)

        self.put_card(img, t)

        return img


class TitleScene(Scene):
    def __init__(self, duration, title, sub, foot):
        Scene.__init__(self, duration)
        self.title = title
        self.sub = sub
        self.foot = foot
        self.settle = 1.6

    def frame(self, t):
        img = Image.new('RGB', (W, H), BLUE)
        d = ImageDraw.Draw(img)

        # A quiet band of lighter blue behind the words
        d.rectangle((0, 330, W, 760), fill=(34, 102, 164))

        k = ramp(t, 0.1, 0.9)

        icon = Image.open(os.path.join(ICONS, 'autovent32.png'))
        icon = icon.convert('RGBA').resize((150, 150), Image.LANCZOS)

        plate = Image.new('RGBA', (190, 190), (0, 0, 0, 0))
        ImageDraw.Draw(plate).rounded_rectangle((0, 0, 189, 189), 30,
                                                fill=(255, 255, 255, 255))
        plate.alpha_composite(icon, (20, 20))

        a = np.asarray(plate).copy()
        a[..., 3] = (a[..., 3] * k).astype(np.uint8)
        plate = Image.fromarray(a, 'RGBA')

        img.paste(plate, (W // 2 - 95, 150 + int((1 - k) * 20)), plate)

        k2 = ramp(t, 0.4, 0.9)
        white = (255, 255, 255)

        def centred(text, fnt, y, colour, kk):
            c = tuple(int(lerp(b, f, kk)) for b, f in zip(BLUE, colour))
            d.text((W / 2 - fnt.getlength(text) / 2, y + (1 - kk) * 16),
                   text, font=fnt, fill=c)

        centred(self.title, F_TITLE, 380, white, k2)
        centred(self.sub, F_SUB, 560, (215, 230, 245), ramp(t, 0.7, 0.9))
        centred(self.foot, F_SMALL, 680, (180, 205, 230), ramp(t, 1.0, 0.8))

        return img


# ---------------------------------------------------------------------
# The drawings
# ---------------------------------------------------------------------

def fade_in(layer, t, start, draw_fn, length=0.45):
    """Draw something onto its own layer and fade it in."""
    k = ramp(t, start, length)

    if k <= 0:
        return

    part = Image.new('RGBA', layer.size, (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(part), part)

    if k < 1:
        a = np.asarray(part).copy()
        a[..., 3] = (a[..., 3] * k).astype(np.uint8)
        part = Image.fromarray(a, 'RGBA')

    layer.alpha_composite(part)


def arrow(d, a, b, colour, width=8, head=26):
    ax, ay = a
    bx, by = b
    ang = math.atan2(by - ay, bx - ax)

    sx = bx - math.cos(ang) * head * 0.8
    sy = by - math.sin(ang) * head * 0.8

    d.line((ax, ay, sx, sy), fill=colour, width=width)

    l = ang + math.radians(150)
    r = ang - math.radians(150)

    d.polygon([(bx, by),
               (bx + math.cos(l) * head, by + math.sin(l) * head),
               (bx + math.cos(r) * head, by + math.sin(r) * head)],
              fill=colour)


def centred_text(d, x, y, text, fnt, colour):
    d.text((x - fnt.getlength(text) / 2, y), text, font=fnt, fill=colour)


def sheet_icon(d, x, y, w, h):
    """A spreadsheet, as a drawing."""
    d.rounded_rectangle((x, y, x + w, y + h), 14, fill=(255, 255, 255),
                        outline=LINE, width=3)
    d.rectangle((x + 3, y + 3, x + w - 3, y + 44), fill=(33, 115, 70))
    d.text((x + 20, y + 8), 'Ventilation schedule.xlsx', font=F_TINY,
           fill=(255, 255, 255))

    rows = 7
    for r in range(rows):
        ry = y + 60 + r * 38
        fill = (0, 176, 240) if r in (0, 4) else (255, 255, 255)
        d.rectangle((x + 20, ry, x + w - 20, ry + 30), fill=fill,
                    outline=LINE)

        if r not in (0, 4):
            d.line((x + w * 0.45, ry, x + w * 0.45, ry + 30), fill=LINE)
            d.line((x + w * 0.65, ry, x + w * 0.65, ry + 30), fill=LINE)
            d.rectangle((x + 30, ry + 10, x + 30 + (60 + r * 13) % 110,
                         ry + 20), fill=PALE)


def model_icon(d, x, y, w, h):
    """A plan: rooms, with terminals in them."""
    d.rounded_rectangle((x, y, x + w, y + h), 14, fill=(250, 251, 252),
                        outline=LINE, width=3)
    d.rectangle((x + 3, y + 3, x + w - 3, y + 44), fill=BLUE)
    d.text((x + 20, y + 8), 'Revit model', font=F_TINY,
           fill=(255, 255, 255))

    walls = (70, 80, 90)
    d.rectangle((x + 30, y + 70, x + w - 30, y + h - 30), outline=walls,
                width=5)
    d.line((x + w / 2, y + 70, x + w / 2, y + h - 30), fill=walls, width=5)
    d.line((x + 30, y + h / 2 + 30, x + w / 2, y + h / 2 + 30), fill=walls,
           width=5)

    for gx, gy in ((0.25, 0.42), (0.25, 0.78), (0.72, 0.45), (0.72, 0.75)):
        cx = x + w * gx
        cy = y + h * gy
        d.rectangle((cx - 16, cy - 16, cx + 16, cy + 16),
                    outline=ORANGE, width=4)
        d.line((cx - 9, cy, cx + 9, cy), fill=ORANGE, width=3)


def draw_overview(layer, t):
    def sheet(d, _):
        sheet_icon(d, 70, 250, 380, 360)
        centred_text(d, 260, 632, 'The ventilation schedule', F_LABEL, INK)

    def model(d, _):
        model_icon(d, 730, 250, 380, 360)
        centred_text(d, 920, 632, 'The model\u2019s air terminals', F_LABEL,
                     INK)

    def forward(d, _):
        arrow(d, (470, 330), (710, 330), ORANGE)
        centred_text(d, 590, 270, 'Assign airflows', F_LABEL, ORANGE)

    def back(d, _):
        arrow(d, (710, 530), (470, 530), BLUE)
        centred_text(d, 590, 548, 'Export rooms', F_LABEL, BLUE)

    fade_in(layer, t, 0.3, sheet)
    fade_in(layer, t, 0.8, model)
    fade_in(layer, t, 1.5, forward)
    fade_in(layer, t, 2.3, back)


RIBBON_ICONS = [
    ('autovent', 'AUTOVENT'), ('chwvolume', 'CHW VOL.'),
    ('ductinsulation', 'INSULATION'), ('sprinklers', 'SPRINKLERS'),
    ('alignterminals', 'ALIGN'), ('sheetqa', 'SHEET QA'),
    ('autosync', 'AUTOSYNC'), ('nwcbatch', 'NWC BATCH'),
    ('equipchanger', 'EQUIPMENT'),
]

F_RIBBON = font('segoeui.ttf', 17)


def draw_ribbon(layer, t):
    def tabs(d, _):
        d.rectangle((40, 150, VW - 40, 200), fill=(243, 245, 247))
        x = 60
        for name in ('File', 'Architecture', 'Systems', 'Insert',
                     'Annotate', 'Analyze', 'View', 'Manage', 'ATKINS'):
            colour = BLUE if name == 'ATKINS' else BODY
            f = F_LABEL if name == 'ATKINS' else F_SMALL
            d.text((x, 160), name, font=f, fill=colour)
            if name == 'ATKINS':
                d.line((x, 196, x + f.getlength(name), 196), fill=BLUE,
                       width=4)
            x += f.getlength(name) + 34

    def panel(d, part):
        d.rectangle((40, 200, VW - 40, 470), fill=(252, 253, 254),
                    outline=LINE)

        for i, (icon, label) in enumerate(RIBBON_ICONS):
            cx = 110 + i * 120
            img = Image.open(os.path.join(ICONS, icon + '32.png'))
            img = img.convert('RGBA').resize((64, 64), Image.LANCZOS)
            part.alpha_composite(img, (int(cx - 32), 240))
            centred_text(d, cx, 318, label, F_RIBBON, INK)

        centred_text(d, (VW) / 2, 430, 'Mechanical', F_SMALL, MUTED)

    def pick(d, _):
        # The box alone says which one - a pointer drawn over the icon
        # covered the very label it was pointing at
        d.rounded_rectangle((56, 228, 164, 350), 12, outline=ORANGE,
                            width=5)

    def window(d, _):
        d.rounded_rectangle((270, 560, 910, 820), 14, fill=(255, 255, 255),
                            outline=LINE, width=3)
        d.rectangle((273, 563, 907, 620), fill=BLUE)
        d.text((296, 576), 'AUTOVENT', font=F_LABEL, fill=(255, 255, 255))
        for i, w in enumerate((520, 420, 470)):
            d.rounded_rectangle((300, 650 + i * 50, 300 + w, 680 + i * 50), 8,
                                fill=(240, 244, 248))
        arrow(d, (110, 380), (262, 600), ORANGE, width=6, head=22)

    fade_in(layer, t, 0.2, tabs)
    fade_in(layer, t, 0.6, panel)
    fade_in(layer, t, 1.6, pick)
    fade_in(layer, t, 2.6, window)


def draw_endcaps(layer, t):
    room = (330, 420, 1000, 800)

    def the_room(d, _):
        d.rectangle(room, outline=(70, 80, 90), width=6)
        d.text((room[0] + 20, room[1] + 16), 'Room 1.02  -  fresh air 100 l/s',
               font=F_LABEL, fill=INK)

        for gx in (430, 560, 770, 900):
            d.rectangle((gx - 18, 690, gx + 18, 726), outline=ORANGE,
                        width=4)
            d.line((gx - 10, 708, gx + 10, 708), fill=ORANGE, width=3)

    def units(d, _):
        for cx, feeds in ((495, (430, 560)), (835, (770, 900))):
            d.rounded_rectangle((cx - 80, 200, cx + 80, 290), 12, fill=BLUE)
            centred_text(d, cx, 228, 'FCU', F_LABEL, (255, 255, 255))

            for gx in feeds:
                d.line((cx, 290, cx, 600), fill=PALE, width=10)
                d.line((cx, 600, gx, 600), fill=PALE, width=10)
                d.line((gx, 600, gx, 690), fill=PALE, width=10)

    def caps(d, _):
        for cx in (495, 835):
            ex, ey = cx - 150, 190

            # The reach, round the endcap - kept inside the panel
            r = 118
            for a in range(0, 360, 12):
                d.arc((ex - r, ey - r, ex + r, ey + r), a, a + 6,
                      fill=MUTED, width=3)

            d.ellipse((ex - 22, ey - 22, ex + 22, ey + 22), fill=GREEN)
            d.line((ex + 22, ey + 10, cx - 80, 240), fill=GREEN, width=6)

        # The legend sits clear of both circles
        d.ellipse((44, 34, 64, 54), fill=GREEN)
        d.text((76, 28), 'endcap  \u2192  the nearest FCU within the reach',
               font=F_SMALL, fill=GREEN)

    def shares(d, _):
        for cx in (495, 835):
            ex, ey = cx - 150, 190
            d.rounded_rectangle((ex - 62, ey + 36, ex + 62, ey + 80), 10,
                                fill=ORANGE)
            centred_text(d, ex, ey + 40, '50 l/s', F_LABEL, (255, 255, 255))

        centred_text(d, 665, 830, '100 l/s  shared equally  =  50 + 50',
                     F_LABEL, ORANGE)

    fade_in(layer, t, 0.3, the_room)
    fade_in(layer, t, 1.1, units)
    fade_in(layer, t, 2.1, caps)
    fade_in(layer, t, 3.3, shares)


SHEET_ROWS = [
    ('level', 'GR_GROUND FLOOR'),
    ('room', ('Reception', 'G-001', '42.50', '2.70')),
    ('room', ('Meeting Room', 'G-002', '18.25', '2.70')),
    ('room', ('Store', '0123', '6.00', '2.40')),
    ('sub', 'Sub Total'),
    ('gap', ''),
    ('level', 'L1_FIRST FLOOR'),
    ('room', ('Open Office', '1-001', '210.75', '2.85')),
    ('room', ('Plant Room', '1-002', '30.00', '')),
    ('sub', 'Sub Total'),
]

COLS = [('Space Name', 290), ('Room Number', 150), ('Total Area (m2)', 150),
        ('Room Height (m)', 150), ('Fresh Airflow (l/s)', 170),
        ('Total Exhaust (l/s)', 170)]


def draw_sheet(layer, t):
    x0, y0 = 40, 110
    rh = 46

    def header(d, _):
        d.text((x0, 40), 'Ventilation', font=F_LABEL, fill=INK)
        d.text((x0 + 150, 44), '(the worksheet you named)', font=F_TINY,
               fill=MUTED)
        x = x0
        for name, w in COLS:
            d.rectangle((x, y0, x + w, y0 + 62), fill=(217, 217, 217),
                        outline=(160, 160, 160))
            for i, line in enumerate(wrap(name, F_CELL_B, w - 16)):
                d.text((x + 8, y0 + 6 + i * 26), line, font=F_CELL_B,
                       fill=INK)
            x += w

    fade_in(layer, t, 0.3, header)

    for i, (kind, value) in enumerate(SHEET_ROWS):
        y = y0 + 62 + i * rh

        def row(d, _, kind=kind, value=value, y=y):
            x = x0
            if kind == 'gap':
                return
            for c, (_, w) in enumerate(COLS):
                fill = (0, 176, 240) if kind == 'level' else (255, 255, 255)
                d.rectangle((x, y, x + w, y + rh), fill=fill,
                            outline=(170, 170, 170))
                text = ''
                f = F_CELL
                if kind == 'level' and c == 0:
                    text, f = value, F_CELL_B
                elif kind == 'sub' and c == 0:
                    text, f = value, F_CELL_B
                elif kind == 'room' and c < 4:
                    text = value[c]
                if text:
                    if c >= 2:
                        d.text((x + w - 10 - f.getlength(text), y + 10), text,
                               font=f, fill=INK)
                    else:
                        d.text((x + 10, y + 10), text, font=f, fill=INK)
                x += w

        fade_in(layer, t, 0.8 + i * 0.28, row, length=0.3)

    def empty(d, _):
        fx = x0 + sum(w for _, w in COLS[:4])
        fw = COLS[4][1] + COLS[5][1]
        top = y0
        bottom = y0 + 62 + len(SHEET_ROWS) * rh
        d.rounded_rectangle((fx - 4, top - 4, fx + fw + 4, bottom + 4), 10,
                            outline=ORANGE, width=5)
        centred_text(d, fx + fw / 2, bottom + 24, 'you fill these in',
                     F_LABEL, ORANGE)

    fade_in(layer, t, 0.8 + len(SHEET_ROWS) * 0.28 + 0.3, empty)


LOG_LINES = [
    'Endcap reach: 1500 mm',
    "MATCHED room 1.02 'Meeting Room' | 'G-002' | 'GR_GROUND FLOOR':",
    '        Fresh=100, Exhaust=80',
    'WRITE fresh 50 l/s -> endcap 1283746',
    'WRITE fresh 50 l/s -> endcap 1283791',
    'Endcap split: 42 room(s) served by equipment, 3 divided ...',
    'SUMMARY: matchedRooms=124, terminalsUpdated=388',
]


def draw_log(layer, t):
    def desk(d, _):
        d.rounded_rectangle((60, 60, 300, 330), 18, fill=(245, 247, 250),
                            outline=LINE, width=3)
        d.polygon([(130, 110), (210, 110), (240, 140), (240, 250),
                   (130, 250)], fill=(255, 255, 255), outline=MUTED)
        d.polygon([(210, 110), (210, 140), (240, 140)], fill=PALE)
        for i in range(4):
            d.line((145, 160 + i * 20, 225, 160 + i * 20), fill=PALE,
                   width=5)
        centred_text(d, 180, 268, 'Desktop', F_TINY, MUTED)

    def file_name(d, _):
        d.text((340, 170), 'ATKINSMEC_debug_log.txt', font=F_LABEL,
               fill=INK)
        d.text((340, 212), 'written on every run', font=F_SMALL, fill=MUTED)

    def text(d, _):
        d.rounded_rectangle((60, 390, VW - 60, 820), 14, fill=(24, 32, 42))
        for i, line in enumerate(LOG_LINES):
            colour = (140, 200, 255) if line.startswith('WRITE') else (
                (255, 190, 120) if line.startswith('MATCHED') else
                (210, 220, 230))
            d.text((90, 420 + i * 52), line, font=F_MONO, fill=colour)

    fade_in(layer, t, 0.3, desk)
    fade_in(layer, t, 0.8, file_name)
    fade_in(layer, t, 1.4, text)


# ---------------------------------------------------------------------
# The storyboard
# ---------------------------------------------------------------------

# Steps 7 and 8 are one panel: the camera holds still on the whole row
# and only the highlight moves, from the three ways to the name and the
# button - re-framing between them was a jolt for no reason
EXPORT_ROW = ['RdoIntoImported', 'RdoAddWorksheet', 'TxtNewSheet',
              'BtnExport']


def storyboard():
    scenes = []

    scenes.append(TitleScene(
        6, 'AUTOVENT',
        'Ventilation airflows from the schedule onto the air terminals',
        'Quick guide  \u00b7  ATKINS \u203a Mechanical'))

    scenes.append(DiagramScene(
        11, None, 'Two directions',
        'Assign airflows reads the ventilation schedule and writes the '
        'fresh and extract flow onto every air terminal in the rooms it '
        'names.\n\n'
        'Export rooms goes the other way: the model\u2019s levels and '
        'rooms, into a sheet ready to fill in.',
        draw_overview, 3.0))

    scenes.append(DiagramScene(
        8, None, 'Open AUTOVENT',
        'ATKINS tab, Mechanical panel, AUTOVENT.\n\n'
        'The window stays open while you work in the model, so you can '
        'check a room and come straight back.',
        draw_ribbon, 3.2))

    s1 = WindowScene(
        12, 1, 'Pick the linked model',
        'Tick the linked architectural model the rooms come from \u2014 '
        'usually the architect\u2019s.\n\n'
        'Tick more than one if the rooms are split across models.',
        'tut_assign', 'ListLinks.card')
    scenes.append(s1)

    s2 = WindowScene(
        13, 2, 'Open the schedule',
        'Open workbook\u2026 and choose the worksheet.\n\n'
        'Columns are found by their headings \u2014 space name, room '
        'number, fresh air, exhaust \u2014 so another office\u2019s layout '
        'still reads.',
        'tut_assign', 'BtnImport.card', start=s1.end_rect())
    scenes.append(s2)

    # The CAMERA frames what is lit, not the card it sits on: the levels
    # and action cards are as wide as the window, so framing the card
    # left nothing to zoom into and the text stayed small.
    s3 = WindowScene(
        13, 3, 'Choose the levels',
        'The levels are this model\u2019s. Every linked room lands on the '
        'storey it actually sits on, and each level shows how many rooms '
        'it has.\n\n'
        'Levels the sheet knows are ticked for you.',
        'tut_assign', 'ListLevels', start=s2.end_rect())
    scenes.append(s3)

    s4 = WindowScene(
        13, 4, 'Fresh or extract?',
        'Tick which duct system types carry fresh air, and which carry '
        'extract.\n\n'
        'Tick none and AUTOVENT goes by the system names, as it always '
        'has.',
        'tut_assign', ['TxtFreshTypes', 'LstFresh', 'TxtExtractTypes', 'LstExtract'],
        start=s3.end_rect())
    scenes.append(s4)

    scenes.append(DiagramScene(
        14, 5, 'Endcaps and FCUs',
        'An endcap feeds an FCU that serves another room. AUTOVENT finds '
        'the nearest FCU within the endcap reach.\n\n'
        'A room\u2019s air is shared equally between the endcaps that '
        'serve it \u2014 never written twice, never lost.',
        draw_endcaps, 3.9))

    s6 = WindowScene(
        10, 6, 'Assign the airflows',
        'Set the endcap reach, then press Assign airflows.\n\n'
        'The flows are written onto the terminals and a summary says which '
        'rooms matched. It is one Undo in Revit.',
        'tut_assign', (820, 975, 1362, 1038))
    scenes.append(s6)

    s7 = WindowScene(
        13, 7, 'Export rooms to Excel',
        'Three ways:\n'
        '\u2022 fill the worksheet you imported\n'
        '\u2022 a new workbook, from the standard layout\n'
        '\u2022 add a worksheet to the imported workbook',
        'tut_export', EXPORT_ROW,
        spot=['RdoIntoImported', 'CboExportSheet', 'RdoNewWorkbook',
              'RdoAddWorksheet'],
        start=s6.end_rect())
    scenes.append(s7)

    s8 = WindowScene(
        5, 8, 'A sheet ready to fill in',
        'Name the worksheet and press Export rooms. Every room goes under '
        'its level, with its number, area and height.\n\n'
        'Fill in the airflows, then import the sheet to write them back.',
        'tut_export_new', EXPORT_ROW,
        spot=['TxtNewSheet', 'BtnExport'], start=s7.end_rect())
    scenes.append(s8)

    scenes.append(DiagramScene(
        9, 8, 'A sheet ready to fill in',
        'Name the worksheet and press Export rooms. Every room goes under '
        'its level, with its number, area and height.\n\n'
        'Fill in the airflows, then import the sheet to write them back.',
        draw_sheet, 4.2))

    scenes.append(DiagramScene(
        8, None, 'If a room doesn\u2019t match',
        'Every run writes ATKINSMEC_debug_log.txt to your Desktop. It '
        'lists each room it matched \u2014 by name, number and level \u2014 '
        'and every flow it wrote.\n\n'
        'Read it first.',
        draw_log, 2.0))

    scenes.append(TitleScene(
        6, 'AUTOVENT',
        'Schedule to model, and back again.',
        'ATKINS  \u203a  Mechanical  \u203a  AUTOVENT'))

    return scenes


# ---------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------

def with_progress(img, fraction):
    """The thin bar along the bottom, drawn straight into the array."""
    a = np.array(img)

    a[1052:1058, 48:W - 48] = LINE
    a[1052:1058, 48:48 + int((W - 96) * fraction)] = BLUE

    return a


def main():
    global BASE

    BASE = background()

    scenes = storyboard()

    total = sum(s.duration for s in scenes)
    frames = int(round(total * FPS))

    print('%d scene(s), %.1f s, %d frame(s)' % (len(scenes), total, frames))

    if total >= 180:
        raise SystemExit('over three minutes - cut something')

    if '--stills' in sys.argv:
        for i, s in enumerate(scenes):
            s.frame(s.duration - 0.1).save(
                os.path.join(HERE, 'still_%02d.png' % i))
        print('stills written')
        return

    wav = os.path.join(HERE, 'music.wav')

    subprocess.check_call([sys.executable, os.path.join(HERE, 'music.py'),
                           str(total), wav])

    out = os.path.join(HERE, 'autovent_tutorial.mp4')

    enc = subprocess.Popen(
        [ENC, out, str(W), str(H), str(FPS), str(frames), wav],
        stdin=subprocess.PIPE)

    written = 0
    previous = None

    for s in scenes:
        n = int(round(s.duration * FPS))
        cached = None
        last = None

        for i in range(n):
            t = i / FPS

            if cached is not None:
                img = cached
            else:
                img = s.frame(t)

                if t >= s.settle:
                    cached = img

            shown = img

            # Cross-fade out of whatever came before
            if previous is not None and t < XFADE:
                shown = Image.blend(previous, img, ease(t / XFADE))

            a = with_progress(shown, written / max(1, frames - 1))

            bgra = np.empty((H, W, 4), np.uint8)
            bgra[..., 0] = a[..., 2]
            bgra[..., 1] = a[..., 1]
            bgra[..., 2] = a[..., 0]
            bgra[..., 3] = 255

            enc.stdin.write(bgra.tobytes())

            written += 1
            last = img

        previous = last

        print('  %-14s %5.1f s  done (%d frames so far)' % (
            type(s).__name__, s.duration, written))

    enc.stdin.close()
    code = enc.wait()

    if code != 0 or written != frames:
        raise SystemExit('encoder exit %d, %d of %d frames' % (
            code, written, frames))

    print('wrote %s (%.1f MB)' % (out, os.path.getsize(out) / 1e6))


if __name__ == '__main__':
    main()
