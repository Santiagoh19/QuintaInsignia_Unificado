"""Convierte los fondos de día a noche: cielo estrellado con luna, paisaje en tonos azulados."""
import sys, random, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

def es_cielo(a):
    mn = a[..., :3].min(-1); mx = a[..., :3].max(-1)
    return (mn > 195) & ((mx - mn) < 50)

def conectado_arriba(mask):
    # relleno desde la fila superior sin scipy
    h, w = mask.shape
    out = np.zeros_like(mask)
    out[0] = mask[0]
    for y in range(1, h):
        fila = mask[y] & out[y - 1]
        # expandir horizontalmente dentro de la máscara
        if fila.any():
            m = mask[y]
            # propagación izquierda->derecha y derecha->izquierda
            f = fila.copy()
            for _ in range(2):
                acc = False
                for x in range(w):
                    acc = m[x] and (acc or f[x]); f[x] = acc
                acc = False
                for x in range(w - 1, -1, -1):
                    acc = m[x] and (acc or f[x]); f[x] = acc
            out[y] = f
    return out

def convertir(ruta_in, ruta_out, semilla=7, luna=(0.80, 0.16)):
    img = Image.open(ruta_in).convert("RGBA")
    a = np.asarray(img).astype(np.float32)
    h, w = a.shape[:2]
    rgb = a[..., :3] / 255.0

    cielo_raw = es_cielo(a)
    try:
        from scipy import ndimage
        lab, n = ndimage.label(cielo_raw)
        ids = np.unique(lab[0][lab[0] > 0])
        cielo = np.isin(lab, ids)
    except ImportError:
        cielo = conectado_arriba(cielo_raw)
    # nubes = partes del cielo más claras que el color base
    lum = rgb.mean(-1)
    base = np.median(lum[cielo]) if cielo.any() else 0.9
    nube = cielo & (lum > base + 0.015)

    # --- paisaje de noche: azulado, más oscuro, menos saturado
    gris = (rgb * np.array([0.30, 0.59, 0.11])).sum(-1, keepdims=True)
    des = rgb * 0.55 + gris * 0.45
    tinte = np.array([0.42, 0.55, 0.85])
    noche = des * tinte * 0.80 + np.array([0.02, 0.03, 0.08])

    # --- cielo: degradado azul profundo
    y = np.linspace(0, 1, h)[:, None]
    horizonte = max(0.2, (np.where(cielo.any(1))[0].max() / h) if cielo.any() else 0.4)
    t = np.clip(y / horizonte, 0, 1)
    arriba = np.array([0.04, 0.07, 0.17]); abajo = np.array([0.15, 0.22, 0.40])
    grad = arriba + (abajo - arriba) * (t ** 1.4)[..., None]
    grad = np.broadcast_to(grad, (h, w, 3)).copy()
    # nubes: gris azulado translúcido
    grad[nube] = grad[nube] * 0.45 + np.array([0.30, 0.36, 0.52]) * 0.55

    out = np.where(cielo[..., None], grad, noche)
    res = Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")

    # --- estrellas y luna (solo sobre el cielo, sin tapar nubes)
    capa = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(capa)
    rnd = random.Random(semilla)
    esc = w / 1920
    estrellas = int(220 * (w * h) / (1920 * 1080))
    for _ in range(estrellas):
        x, yy = rnd.randrange(w), rnd.randrange(int(h * horizonte * 0.95))
        if not cielo[yy, x] or nube[yy, x]:
            continue
        r = rnd.choice([1, 1, 1, 1.5, 2, 2.5]) * esc
        al = rnd.randint(140, 255)
        d.ellipse([x - r, yy - r, x + r, yy + r], fill=(255, 250, 225, al))
        if r >= 2.4 * esc:  # destello en cruz
            L = r * 3
            d.line([x - L, yy, x + L, yy], fill=(255, 250, 225, al // 2), width=max(1, int(esc)))
            d.line([x, yy - L, x, yy + L], fill=(255, 250, 225, al // 2), width=max(1, int(esc)))

    lx, ly, R = int(w * luna[0]), int(h * luna[1]), int(62 * esc)
    halo = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    hd = ImageDraw.Draw(halo)
    hd.ellipse([lx - R * 2.6, ly - R * 2.6, lx + R * 2.6, ly + R * 2.6], fill=(200, 215, 255, 60))
    halo = halo.filter(ImageFilter.GaussianBlur(R * 0.9))
    capa = Image.alpha_composite(halo, capa)
    d = ImageDraw.Draw(capa)
    d.ellipse([lx - R, ly - R, lx + R, ly + R], fill=(255, 246, 214, 255))
    # cráteres suaves
    for cx, cy, cr in ((-0.3, -0.2, 0.22), (0.25, 0.15, 0.16), (-0.05, 0.4, 0.12), (0.35, -0.35, 0.1)):
        d.ellipse([lx + cx * R - cr * R, ly + cy * R - cr * R, lx + cx * R + cr * R, ly + cy * R + cr * R],
                  fill=(236, 222, 186, 255))
    # recortar a cielo (que la luna no tape árboles)
    m = Image.fromarray((cielo * 255).astype(np.uint8))
    capa.putalpha(Image.fromarray(np.minimum(np.asarray(capa)[..., 3], np.asarray(m))))
    res = Image.alpha_composite(res, capa)

    # mantener la transparencia original
    res.putalpha(Image.fromarray(a[..., 3].astype(np.uint8)))
    res.save(ruta_out)

if __name__ == "__main__":
    args = sys.argv[1:]
    luna = (float(args[2]), float(args[3])) if len(args) >= 4 else (0.80, 0.16)
    convertir(args[0], args[1], luna=luna)


# ---------------------------------------------------------------- Pantalla de Inicio
def luna_png(ruta, tam=512):
    """Luna llena con halo, para reemplazar el círculo lavanda (esquina superior izquierda)."""
    esc = tam / 512
    capa = Image.new("RGBA", (tam, tam), (0, 0, 0, 0))
    halo = Image.new("RGBA", (tam, tam), (0, 0, 0, 0))
    c, R = tam / 2, tam * 0.30
    ImageDraw.Draw(halo).ellipse([c - R * 1.55, c - R * 1.55, c + R * 1.55, c + R * 1.55], fill=(210, 222, 255, 90))
    halo = halo.filter(ImageFilter.GaussianBlur(30 * esc))
    d = ImageDraw.Draw(capa)
    d.ellipse([c - R, c - R, c + R, c + R], fill=(255, 246, 214, 255))
    for cx, cy, cr in ((-0.3, -0.2, 0.22), (0.25, 0.15, 0.16), (-0.05, 0.42, 0.12), (0.38, -0.35, 0.1)):
        d.ellipse([c + cx * R - cr * R, c + cy * R - cr * R, c + cx * R + cr * R, c + cy * R + cr * R],
                  fill=(236, 222, 186, 255))
    Image.alpha_composite(halo, capa).save(ruta)

def circulo_png(ruta, color, tam=512):
    img = Image.new("RGBA", (tam, tam), (0, 0, 0, 0))
    ImageDraw.Draw(img).ellipse([0, 0, tam - 1, tam - 1], fill=color)
    img.save(ruta)

def tono_noche(ruta_in, ruta_out):
    """Oscurece y azula cualquier imagen manteniendo su transparencia (césped, objetos)."""
    img = Image.open(ruta_in).convert("RGBA")
    a = np.asarray(img).astype(np.float32)
    rgb = a[..., :3] / 255.0
    gris = (rgb * np.array([0.30, 0.59, 0.11])).sum(-1, keepdims=True)
    des = rgb * 0.55 + gris * 0.45
    noche = des * np.array([0.42, 0.55, 0.85]) * 0.85 + np.array([0.02, 0.03, 0.08])
    out = np.concatenate([np.clip(noche, 0, 1) * 255, a[..., 3:]], -1).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(ruta_out)

def estrellas_png(ruta, w=1920, h=1080, semilla=3):
    """Capa transparente de estrellas para el fondo de Inicio."""
    capa = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(capa)
    rnd = random.Random(semilla)
    for _ in range(260):
        x, y = rnd.randrange(w), rnd.randrange(int(h * 0.85))
        r = rnd.choice([1, 1, 1.5, 1.5, 2, 2.5, 3])
        al = rnd.randint(120, 240)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 250, 225, al))
        if r >= 2.5:
            L = r * 3.2
            d.line([x - L, y, x + L, y], fill=(255, 250, 225, al // 2), width=1)
            d.line([x, y - L, x, y + L], fill=(255, 250, 225, al // 2), width=1)
    capa.save(ruta)
