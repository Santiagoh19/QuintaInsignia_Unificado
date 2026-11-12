"""Íconos de referencia de gestos: dos manos abiertas (palmas al frente) y dos puños cerrados."""
import cairosvg

PIEL, SOMBRA, LINEA, UNA = "#F7C9A3", "#E9A87E", "#7A4B36", "#FCE6D4"
W, H = 640, 470

def capsula(x, y, w, h, rot=0, cx=None, cy=None):
    t = f' transform="rotate({rot} {cx} {cy})"' if rot else ""
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{w/2}"{t}/>'

def mano_abierta():
    formas = [
        '<rect x="102" y="300" width="96" height="190" rx="30"/>',            # antebrazo
        '<rect x="78" y="175" width="146" height="160" rx="52"/>',            # palma
        capsula(80, 108, 32, 120),                                            # meñique
        capsula(115, 62, 34, 160),                                            # anular
        capsula(152, 46, 34, 170),                                            # medio
        capsula(189, 72, 33, 150),                                            # índice
        capsula(186, 196, 38, 104, rot=34, cx=205, cy=300),                   # pulgar (hacia el centro)
    ]
    detalle = [
        f'<path d="M110 268 Q150 300 196 262" fill="none" stroke="{SOMBRA}" stroke-width="7" stroke-linecap="round"/>',
        f'<path d="M118 232 Q150 246 184 226" fill="none" stroke="{SOMBRA}" stroke-width="6" stroke-linecap="round"/>',
        f'<path d="M150 370 L150 440" fill="none" stroke="{SOMBRA}" stroke-width="6" stroke-linecap="round" opacity=".6"/>',
    ]
    for x, y, w in ((84, 114, 24), (120, 68, 24), (157, 52, 24), (193, 78, 24)):
        detalle.append(f'<ellipse cx="{x + w/2 + 0}" cy="{y + 16}" rx="{w/2 - 2}" ry="12" fill="{UNA}"/>')
    return formas, detalle

def puno():
    formas = [
        '<rect x="102" y="300" width="96" height="190" rx="30"/>',            # antebrazo
        '<rect x="70" y="150" width="162" height="170" rx="56"/>',            # puño
        capsula(72, 140, 38, 110), capsula(110, 132, 40, 118),
        capsula(150, 132, 40, 118), capsula(190, 142, 38, 104),              # dedos doblados
    ]
    pulgar = (f'<g><rect x="112" y="236" width="122" height="46" rx="23" fill="{LINEA}" '
              f'stroke="{LINEA}" stroke-width="16" stroke-linejoin="round"/>'
              f'<rect x="112" y="236" width="122" height="46" rx="23" fill="{PIEL}"/>'
              f'<ellipse cx="133" cy="259" rx="13" ry="14" fill="{UNA}"/></g>')
    detalle = [
        f'<path d="M150 370 L150 440" fill="none" stroke="{SOMBRA}" stroke-width="6" stroke-linecap="round" opacity=".6"/>',
        f'<path d="M84 205 Q90 222 104 226 M124 205 Q130 224 144 228 M164 205 Q170 224 184 228" fill="none" stroke="{SOMBRA}" stroke-width="6" stroke-linecap="round"/>',
        pulgar,
    ]
    return formas, detalle

def una_mano(formas, detalle):
    contorno = "".join(formas)
    return (f'<g fill="{LINEA}" stroke="{LINEA}" stroke-width="16" stroke-linejoin="round">{contorno}</g>'
            f'<g fill="{PIEL}">{contorno}</g>' + "".join(detalle))

def svg(mano):
    m = una_mano(*mano)
    # mano izquierda de la imagen con el pulgar hacia el centro, y su espejo a la derecha
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">'
            f'<defs><clipPath id="c"><rect width="{W}" height="{H - 4}"/></clipPath></defs>'
            f'<g clip-path="url(#c)"><g transform="translate(14,0)">{m}</g>'
            f'<g transform="translate({W - 14},0) scale(-1,1)">{m}</g></g></svg>')

for nombre, mano in (("ManosAbiertas", mano_abierta()), ("ManosPunos", puno())):
    s = svg(mano)
    open(nombre + ".svg", "w").write(s)
    cairosvg.svg2png(bytestring=s.encode(), write_to=nombre + ".png")
print("ok")
