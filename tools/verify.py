import sys, os, glob
from PIL import Image

CLEAR = (24, 31, 30)   # rendering/environment/defaults/default_clear_color

def near(c, t, tol=7):
    return all(abs(a - b) <= tol for a, b in zip(c, t))

def check(path):
    im = Image.open(path).convert('RGB')
    w, h = im.size
    px = im.load()
    rows = [h // 6, h // 3, h // 2, 2 * h // 3, 5 * h // 6]
    empty = [x for x in range(w) if all(near(px[x, y], CLEAR) for y in rows)]
    pure  = [x for x in range(w) if all(px[x, y] == (0, 0, 0) for y in rows)]
    scale = 5120 / w
    name = os.path.basename(path)
    if not empty and not pure:
        return "  OK    %-18s canvas cubierto de borde a borde" % name
    bits = []
    if empty: bits.append("clear-color en %d col (%.0f px reales)" % (len(empty), len(empty) * scale))
    if pure:  bits.append("negro puro en %d col (%.0f px reales)" % (len(pure), len(pure) * scale))
    return "  AVISO %-18s %s" % (name, "; ".join(bits))

for p in sorted(sys.argv[1:]):
    print(check(p))
