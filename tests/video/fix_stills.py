import io

p = ('C:/Users/moham/AppData/Local/Temp/claude/'
     'C--Users-moham-source-repos-ATKINSMEC/'
     '762927c6-4583-4e32-8dfb-2cd6035f5021/scratchpad/video/make_video.py')

s = io.open(p, encoding='utf-8').read()


def swap(old, new):
    global s
    assert s.count(old) == 1, 'NOT FOUND / NOT UNIQUE: ' + old[:80]
    s = s.replace(old, new, 1)


# ---- 1. crop the transparent margin off, rather than let it go black --
swap("""            png = Image.open(os.path.join(SHOTS, name + '.png'))
            png = png.convert('RGB')""",
"""            png = Image.open(os.path.join(SHOTS, name + '.png'))
            png = png.convert('RGBA')

            # The render carries a TRANSPARENT margin round the window,
            # and converting straight to RGB turns it into a black band
            # down the right and along the bottom. Crop to what is
            # actually opaque; the bounds JSON counts from the top-left,
            # so it is unaffected.
            png = png.crop(png.getchannel('A').getbbox())""")

swap("""            canvas.paste(sh, (p - m, p - m), sh)
            canvas.paste(png, (p, p))""",
"""            canvas.paste(sh, (p - m, p - m), sh)
            canvas.paste(png, (p, p), png)""")

# ---- 2. frame the CONTROLS, not the whole-width cards ----------------
swap("""    s3 = WindowScene(
        13, 3, 'Choose the levels',""",
"""    # The CAMERA frames what is lit, not the card it sits on: the levels
    # and action cards are as wide as the window, so framing the card
    # left nothing to zoom into and the text stayed small.
    s3 = WindowScene(
        13, 3, 'Choose the levels',""")

swap("""        'tut_assign', 'ListLevels.card', start=s2.end_rect())""",
"""        'tut_assign', 'ListLevels', start=s2.end_rect())""")

LISTS = "['TxtFreshTypes', 'LstFresh', 'TxtExtractTypes', 'LstExtract']"

swap("""        'tut_assign', 'TabActions.card',
        spot=""" + LISTS + """,
        start=s3.end_rect())""",
"""        'tut_assign', """ + LISTS + """,
        start=s3.end_rect())""")

swap("""        'tut_assign', 'TabActions.card',
        spot=(820, 975, 1362, 1038))""",
"""        'tut_assign', (820, 975, 1362, 1038))""")

RADIOS = """['RdoIntoImported', 'CboExportSheet', 'RdoNewWorkbook',
              'RdoAddWorksheet']"""

swap("""        'tut_export', 'TabActions.card',
        spot=""" + RADIOS + """,
        start=s6.end_rect())""",
"""        'tut_export', """ + RADIOS + """,
        start=s6.end_rect())""")

swap("""        'tut_export_new', 'TabActions.card',
        spot=['TxtNewSheet', 'BtnExport'], start=s7.end_rect())""",
"""        'tut_export_new', ['TxtNewSheet', 'BtnExport'],
        start=s7.end_rect())""")

# ---- 3. the ribbon: labels that fit, and no pointer over them --------
swap("""RIBBON_ICONS = [
    ('autovent', 'AUTOVENT'), ('chwvolume', 'CHW VOLUME'),
    ('ductinsulation', 'DUCT INSUL.'), ('sprinklers', 'SPRINKLERS'),
    ('alignterminals', 'ALIGN'), ('sheetqa', 'SHEET QA'),
    ('autosync', 'AUTOSYNC'), ('nwcbatch', 'NWC BATCH'),
    ('equipchanger', 'EQUIP.'),
]""",
"""RIBBON_ICONS = [
    ('autovent', 'AUTOVENT'), ('chwvolume', 'CHW VOL.'),
    ('ductinsulation', 'INSULATION'), ('sprinklers', 'SPRINKLERS'),
    ('alignterminals', 'ALIGN'), ('sheetqa', 'SHEET QA'),
    ('autosync', 'AUTOSYNC'), ('nwcbatch', 'NWC BATCH'),
    ('equipchanger', 'EQUIPMENT'),
]

F_RIBBON = font('segoeui.ttf', 17)""")

swap("""            centred_text(d, cx, 318, label, F_TINY, INK)""",
"""            centred_text(d, cx, 318, label, F_RIBBON, INK)""")

swap("""    def pick(d, _):
        d.rounded_rectangle((52, 228, 168, 356), 12, outline=ORANGE,
                            width=5)

        # A pointer, drawn
        px, py = 150, 300
        d.polygon([(px, py), (px, py + 52), (px + 14, py + 40),
                   (px + 24, py + 62), (px + 32, py + 58),
                   (px + 22, py + 36), (px + 40, py + 36)],
                  fill=(255, 255, 255), outline=INK)""",
"""    def pick(d, _):
        # The box alone says which one - a pointer drawn over the icon
        # covered the very label it was pointing at
        d.rounded_rectangle((56, 228, 164, 350), 12, outline=ORANGE,
                            width=5)""")

# ---- 4. the endcaps: reach circles inside the panel, legend clear ----
swap("""    def caps(d, _):
        for cx in (495, 835):
            ex, ey = cx - 150, 150

            # The reach, round the endcap
            r = 175
            for a in range(0, 360, 12):
                d.arc((ex - r, ey - r, ex + r, ey + r), a, a + 6,
                      fill=MUTED, width=3)

            d.ellipse((ex - 22, ey - 22, ex + 22, ey + 22), fill=GREEN)
            d.line((ex + 22, ey + 10, cx - 80, 230), fill=GREEN, width=6)

        d.text((40, 60), 'endcap  \\u2192  the nearest FCU within the reach',
               font=F_SMALL, fill=GREEN)""",
"""    def caps(d, _):
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
        d.text((76, 28), 'endcap  \\u2192  the nearest FCU within the reach',
               font=F_SMALL, fill=GREEN)""")

swap("""    def shares(d, _):
        for cx in (495, 835):
            ex, ey = cx - 150, 150""",
"""    def shares(d, _):
        for cx in (495, 835):
            ex, ey = cx - 150, 190""")

io.open(p, 'w', encoding='utf-8', newline='').write(s)
print('four fixes applied')
