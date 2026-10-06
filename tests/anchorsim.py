import io, re, glob, os

# Works out where every control LANDS when the window is made bigger,
# using WinForms' own anchor arithmetic, and reports siblings that end
# up on top of each other or outside their parent.
#
# This is the renderer's answer without needing a stub per form: the
# only forms that can be drawn without Revit are the NWC ones, and the
# bug it is looking for (Export and Stop sliding behind the results
# table) is a pure geometry mistake that shows up the same way here.

GROW_W = 400
GROW_H = 400

CONTAINERS = ('GroupBox', 'TabControl', 'TabPage', 'Panel')


def read(path):
    s = io.open(path, encoding='utf-8').read()

    kind = {}
    for m in re.finditer(
            r'this\.(\w+) = new System\.Windows\.Forms\.(\w+)\(\)', s):
        kind[m.group(1)] = m.group(2)

    parent = {}
    for m in re.finditer(r'this\.(\w+)\.Controls\.Add\(this\.(\w+)\)', s):
        parent[m.group(2)] = m.group(1)
    for m in re.finditer(r'this\.Controls\.Add\(this\.(\w+)\)', s):
        parent[m.group(1)] = '(form)'

    loc, size, anchor = {}, {}, {}
    for m in re.finditer(
            r'this\.(\w+)\.Location = new System\.Drawing\.Point'
            r'\((-?\d+), *(-?\d+)\)', s):
        loc[m.group(1)] = (int(m.group(2)), int(m.group(3)))
    for m in re.finditer(
            r'this\.(\w+)\.Size = new System\.Drawing\.Size'
            r'\((\d+), *(\d+)\)', s):
        size[m.group(1)] = (int(m.group(2)), int(m.group(3)))
    for m in re.finditer(r'this\.(\w+)\.Anchor = ([^;]+);', s):
        anchor[m.group(1)] = m.group(2)

    cs = re.search(
        r'this\.ClientSize = new System\.Drawing\.Size\((\d+), *(\d+)\)', s)

    return kind, parent, loc, size, anchor, cs


def moved(x, w, dparent, left, right):
    """One axis of WinForms anchoring."""
    if left and right:
        return x, w + dparent
    if right:
        return x + dparent, w
    if left:
        return x, w
    return x + dparent // 2, w


def run():
    total = 0

    for path in sorted(glob.glob('ATKINSMEC/*/*.Designer.cs')):
        kind, parent, loc, size, anchor, cs = read(path)

        if cs is None:
            continue

        kids = {}
        for c, p in parent.items():
            kids.setdefault(p, []).append(c)

        # box[name] = (x, y, w, h) inside its parent, after growing
        box = {}
        grew = {'(form)': (GROW_W, GROW_H)}

        def has(name, side):
            return 'AnchorStyles.' + side in anchor.get(name, 'Top | Left')

        def walk(node):
            dw, dh = grew[node]

            for c in sorted(kids.get(node, [])):
                if c not in loc or c not in size:
                    continue

                x, y = loc[c]
                w, h = size[c]

                nx, nw = moved(x, w, dw, has(c, 'Left'), has(c, 'Right'))
                ny, nh = moved(y, h, dh, has(c, 'Top'), has(c, 'Bottom'))

                box[c] = (nx, ny, nw, nh)

                if kind.get(c) in CONTAINERS:
                    grew[c] = (nw - w, nh - h)
                    walk(c)

        walk('(form)')

        # A TabPage is sized by its TabControl, not by its own Location
        for c in list(box):
            if kind.get(c) == 'TabPage' and c in grew:
                pass

        name = os.path.basename(path)
        said = []

        for p, family in sorted(kids.items()):
            family = [c for c in family if c in box]

            # TabPages of one TabControl are meant to sit on each other
            family = [c for c in family if kind.get(c) != 'TabPage']

            for i in range(len(family)):
                for j in range(i + 1, len(family)):
                    a, b = family[i], family[j]

                    ax, ay, aw, ah = box[a]
                    bx, by, bw, bh = box[b]

                    over = (ax < bx + bw and bx < ax + aw and
                            ay < by + bh and by < ay + ah)

                    if not over:
                        continue

                    # Was it already overlapping before it grew?
                    ox, oy = loc[a]
                    ow, oh = size[a]
                    px, py = loc[b]
                    pw, ph = size[b]

                    before = (ox < px + pw and px < ox + ow and
                              oy < py + ph and py < oy + oh)

                    said.append('    %s and %s overlap when grown%s'
                                % (a, b, ' (and did before)'
                                   if before else ' - NEW'))

        if said:
            print(name)
            for line in said:
                print(line)
            total += len(said)

    print()
    print('%d overlaps' % total)


run()
