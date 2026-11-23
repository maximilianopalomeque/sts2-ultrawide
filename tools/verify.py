import sys, os
import numpy as np
from PIL import Image

CLEAR = np.array([24, 31, 30])   # default_clear_color del proyecto

def analyse(path):
    im = Image.open(path).convert('RGB')
    w, h = im.size
    a = np.asarray(im, dtype=np.float32)
    scale = 5120 / w
    band = a[int(h*0.10):int(h*0.80)]          # evitar barra superior y mano de cartas

    # 1) lienzo vacio: clear-color o negro puro
    empty = (np.abs(band - CLEAR).max(axis=2) <= 7).all(axis=0) | (band.max(axis=2) <= 2).all(axis=0)

    # 2) smear de borde: columnas sin gradiente horizontal, o sea el ultimo texel del
    #    fondo repetido hacia afuera porque la textura no llega. No es vacio, pero se
    #    ve igual de roto.
    dx = np.abs(np.diff(band, axis=1)).mean(axis=(0, 2))   # largo w-1
    flat = np.concatenate([[False], dx < 0.35])

    issues = []
    for label, mask in (("vacio", empty), ("smear", flat)):
        xs = np.flatnonzero(mask)
        if xs.size == 0:
            continue
        # solo importan los tramos que tocan un borde
        runs, start = [], xs[0]
        for i in range(1, xs.size):
            if xs[i] != xs[i-1] + 1:
                runs.append((start, xs[i-1])); start = xs[i]
        runs.append((start, xs[-1]))
        for s, e in runs:
            touches = s <= 2 or e >= w - 3
            if touches and (e - s + 1) * scale >= 25:
                side = "izq" if s <= 2 else "der"
                issues.append("%s en borde %s: %d px" % (label, side, (e - s + 1) * scale))
    return issues

for p in sorted(sys.argv[1:]):
    name = os.path.basename(p)
    issues = analyse(p)
    print(("  OK    %-20s canvas cubierto de borde a borde" % name) if not issues
          else ("  FALLA %-20s %s" % (name, "; ".join(issues))))
