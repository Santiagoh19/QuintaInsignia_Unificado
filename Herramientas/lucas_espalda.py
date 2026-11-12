"""Lucas de espaldas (500x700, mismo encuadre que Lucas.png) con la linterna en el cinturón."""
import cairosvg

L = "#2A1A14"   # contorno
C = dict(
    sombrero="#2E8B4E", sombreroOsc="#23683F", sombreroClaro="#45A565", cinta="#1E4F33",
    pelo="#5C2A17", peloOsc="#3E1A0C", peloClaro="#7A3B21",
    piel="#FDB882", pielOsc="#E8956A",
    camisa="#FDE9C0", camisaOsc="#EBCF9C",
    panoleta="#FC4926", panoletaOsc="#D9361A",
    short="#178951", shortOsc="#106B3E",
    media="#FCF3E7", mediaOsc="#E6DCCB",
    bota="#B05729", botaOsc="#89411C", suela="#4A2414",
    morral="#2D2627", morralClaro="#463B3A", bolsillo="#9A4B21", bolsilloOsc="#7A3A18", hebilla="#E9B949",
    cinturon="#4A2A1A",
    linterna="#F2A516", linternaOsc="#C97F0A", negro="#26262B", lente="#FFF3B0",
)

def s(d, fill, w=4, extra="", trazo=None):
    return f'<path d="{d}" fill="{fill}" stroke="{trazo or L}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round" {extra}/>'

def svg():
    p = []
    # ---- piernas, medias y botas
    for x0 in (196, 264):
        p.append(s(f"M{x0} 515 h40 v60 h-40 z", C["piel"]))
        p.append(s(f"M{x0-2} 566 h44 v44 h-44 z", C["media"]))
        p.append(f'<path d="M{x0} 578 h40" stroke="{C["mediaOsc"]}" stroke-width="5"/>')
    for x0 in (172, 256):
        p.append(s(f"M{x0} 608 q0 -12 18 -12 h38 q16 0 18 14 l4 60 q0 12 -14 12 h-60 q-14 0 -12 -14 z", C["bota"]))
    for x0 in (170, 252):
        p.append(s(f"M{x0+2} 662 h76 l-2 16 q0 8 -10 8 h-56 q-10 0 -9 -8 z", C["suela"], w=3))
        p.append(f'<path d="M{x0+16} 626 q24 -8 48 0" fill="none" stroke="{C["botaOsc"]}" stroke-width="6" stroke-linecap="round"/>')
        p.append(f'<path d="M{x0+20} 645 q20 -6 40 0" fill="none" stroke="{C["botaOsc"]}" stroke-width="5" stroke-linecap="round"/>')

    # ---- pantaloneta
    p.append(s("M172 430 h156 l10 92 q-34 8 -66 0 l-22 -40 l-22 40 q-32 8 -66 0 z", C["short"]))
    p.append(f'<path d="M250 446 v34" stroke="{C["shortOsc"]}" stroke-width="5" stroke-linecap="round"/>')
    p.append(s("M190 452 h36 v26 h-36 z", C["shortOsc"], w=3))          # bolsillo trasero
    p.append(s("M274 452 h36 v26 h-36 z", C["shortOsc"], w=3))

    # ---- brazos (detrás del torso salen por los lados)
    p.append(s("M140 300 q-18 50 -14 104 q2 26 22 28 q18 0 20 -22 q4 -52 14 -96 z", C["piel"]))
    p.append(s("M360 300 q18 50 14 104 q-2 26 -22 28 q-18 0 -20 -22 q-4 -52 -14 -96 z", C["piel"]))
    p.append(f'<path d="M136 360 q4 30 4 50" fill="none" stroke="{C["pielOsc"]}" stroke-width="6" stroke-linecap="round"/>')
    p.append(f'<path d="M364 360 q-4 30 -4 50" fill="none" stroke="{C["pielOsc"]}" stroke-width="6" stroke-linecap="round"/>')

    # ---- camisa
    p.append(s("M182 262 q68 -18 136 0 q34 8 48 30 l14 40 q-20 14 -42 12 l-10 -18 l0 106 h-156 l0 -106 l-10 18 q-22 2 -42 -12 l14 -40 q14 -22 48 -30 z", C["camisa"]))
    p.append(f'<path d="M168 330 v96 M332 330 v96" stroke="{C["camisaOsc"]}" stroke-width="10" stroke-linecap="round"/>')
    p.append(f'<path d="M150 290 q-6 20 -6 40 M350 290 q6 20 6 40" stroke="{C["camisaOsc"]}" stroke-width="8" stroke-linecap="round" fill="none"/>')
    # cinturón
    p.append(s("M170 420 h160 v18 h-160 z", C["cinturon"], w=3))

    # ---- cuello y pañoleta
    p.append(s("M224 244 h52 v26 q-26 10 -52 0 z", C["piel"]))
    p.append(s("M196 262 q54 18 108 0 l-54 74 z", C["panoleta"]))
    p.append(s("M200 258 q50 22 100 0 q4 10 -2 16 q-48 18 -96 0 q-6 -6 -2 -16 z", C["panoletaOsc"], w=3))

    # ---- morral
    p.append(s("M182 270 q-6 14 -2 30 M318 270 q6 14 2 30", "none", w=12))   # correas sobre los hombros
    p.append(s("M180 270 q-6 14 -2 30", "none", w=6, trazo=C["bolsilloOsc"]))
    p.append(s("M162 318 q0 -36 40 -40 h96 q40 4 40 40 v98 q0 18 -18 18 h-140 q-18 0 -18 -18 z", C["morral"]))
    p.append(f'<path d="M176 320 q0 -26 30 -30 h40" fill="none" stroke="{C["morralClaro"]}" stroke-width="8" stroke-linecap="round"/>')
    p.append(s("M170 312 q0 -30 34 -32 h92 q34 2 34 32 v24 q-80 18 -160 0 z", C["bolsillo"]))            # tapa
    p.append(f'<path d="M180 318 q70 14 140 0" fill="none" stroke="{C["bolsilloOsc"]}" stroke-width="5" stroke-linecap="round"/>')
    for x0 in (212, 288):
        p.append(s(f"M{x0-7} 330 h14 v30 h-14 z", C["bolsilloOsc"], w=3))
        p.append(s(f"M{x0-9} 352 h18 v12 h-18 z", C["hebilla"], w=3))
    p.append(s("M196 372 h108 q10 0 10 10 v40 q0 10 -10 10 h-108 q-10 0 -10 -10 v-40 q0 -10 10 -10 z", C["bolsillo"]))
    p.append(s("M192 372 h116 v20 q-58 10 -116 0 z", C["bolsilloOsc"], w=3))
    p.append(s("M244 386 h12 v12 h-12 z", C["hebilla"], w=3))

    # ---- cabeza (de espaldas) y orejas
    p.append(s("M168 196 q-22 0 -20 24 q4 22 26 16 z", C["piel"]))
    p.append(s("M332 196 q22 0 20 24 q-4 22 -26 16 z", C["piel"]))
    p.append(f'<path d="M160 208 q8 4 10 14 M340 208 q-8 4 -10 14" fill="none" stroke="{C["pielOsc"]}" stroke-width="5" stroke-linecap="round"/>')
    p.append(s("M166 196 q0 -86 84 -88 q84 2 84 88 q0 50 -30 68 q-54 18 -108 0 q-30 -18 -30 -68 z", C["pelo"]))
    for d in ("M200 150 q14 50 6 106", "M232 136 q10 60 4 126", "M268 136 q-6 60 0 126", "M300 150 q-14 50 -8 106"):
        p.append(f'<path d="{d}" fill="none" stroke="{C["peloOsc"]}" stroke-width="6" stroke-linecap="round"/>')
    p.append(f'<path d="M190 160 q14 -24 40 -30" fill="none" stroke="{C["peloClaro"]}" stroke-width="9" stroke-linecap="round"/>')
    p.append(f'<path d="M214 262 q12 10 20 0 q10 12 32 0 q8 10 20 0" fill="{C["pelo"]}" stroke="{L}" stroke-width="4" stroke-linejoin="round"/>')

    # ---- sombrero
    p.append(s("M86 166 q164 -66 328 0 q12 22 -12 32 q-152 44 -304 0 q-24 -10 -12 -32 z", C["sombrero"]))
    p.append(f'<path d="M104 182 q146 40 292 0" fill="none" stroke="{C["sombreroOsc"]}" stroke-width="9" stroke-linecap="round"/>')
    p.append(f'<path d="M120 160 q60 -20 110 -24" fill="none" stroke="{C["sombreroClaro"]}" stroke-width="7" stroke-linecap="round"/>')
    p.append(s("M168 152 q-8 -120 82 -124 q90 4 82 124 q-82 22 -164 0 z", C["sombrero"]))
    p.append(f'<path d="M196 74 q26 -28 62 -26" fill="none" stroke="{C["sombreroClaro"]}" stroke-width="11" stroke-linecap="round"/>')
    p.append(f'<path d="M250 34 q6 40 0 70" fill="none" stroke="{C["sombreroOsc"]}" stroke-width="7" stroke-linecap="round"/>')
    p.append(s("M168 126 q82 20 164 0 l2 26 q-84 22 -168 0 z", C["cinta"], w=3))

    # ---- linterna en el cinturón (cadera derecha), apuntando hacia adelante/arriba
    p.append('<defs><radialGradient id="brillo"><stop offset="0" stop-color="#FFF6C8" stop-opacity="0.95"/>'
             '<stop offset="0.45" stop-color="#FFE28A" stop-opacity="0.45"/><stop offset="1" stop-color="#FFE28A" stop-opacity="0"/></radialGradient></defs>')
    p.append('<g transform="translate(-60,-80) scale(1.18) rotate(14 344 448)">')
    p.append(s("M334 432 h22 v42 q0 8 -11 8 q-11 0 -11 -8 z", C["linterna"]))
    p.append(f'<path d="M334 446 h22 M334 456 h22 M334 466 h22" stroke="{C["negro"]}" stroke-width="4"/>')
    p.append(s("M330 416 h30 l-4 18 h-22 z", C["negro"]))
    p.append(f'<ellipse cx="345" cy="416" rx="15" ry="5" fill="{C["lente"]}" stroke="{L}" stroke-width="3"/>')
    p.append(s("M326 434 h12 v10 h-12 z", C["cinturon"], w=3))          # gancho al cinturón
    p.append('</g>')
    p.append('<circle cx="356" cy="404" r="36" fill="url(#brillo)"/>')

    return ('<svg xmlns="http://www.w3.org/2000/svg" width="500" height="700" viewBox="0 0 500 700">'
            + "".join(p) + "</svg>")

if __name__ == "__main__":
    data = svg()
    open("LucasEspalda.svg", "w").write(data)
    cairosvg.svg2png(bytestring=data.encode(), write_to="LucasEspalda.png")
    print("ok")
