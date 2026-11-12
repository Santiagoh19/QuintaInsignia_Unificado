"""Pasa las imágenes del juego a la noche. Guarda una copia de día en ImagenesDia/ (fuera de Assets)."""
import os, shutil, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import noche

RAIZ = sys.argv[1]                      # carpeta "My project"
IMG = os.path.join(RAIZ, "Assets", "Imagenes")
DIA = os.path.join(RAIZ, "ImagenesDia")

def respaldar(rel):
    src, dst = os.path.join(IMG, rel), os.path.join(DIA, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if not os.path.exists(dst):
        shutil.copy2(src, dst)
    return dst                          # siempre se convierte desde la copia de día

fondos = {"Fondo.png": (0.5, 0.13), "Juego/FondoClaro.png": (0.80, 0.16),
          "Juego/FondoLago.png": (0.45, 0.13), "Juego/FondoSendero.png": (0.38, 0.09)}
for rel, luna in fondos.items():
    noche.convertir(respaldar(rel), os.path.join(IMG, rel), luna=luna)
    print("noche:", rel)

respaldar("Inicio/CirculoLavanda.png"); respaldar("Inicio/CirculoAzul.png")
noche.luna_png(os.path.join(IMG, "Inicio/CirculoLavanda.png"))
noche.circulo_png(os.path.join(IMG, "Inicio/CirculoAzul.png"), (70, 105, 170, 110))
noche.tono_noche(respaldar("Inicio/Cesped.png"), os.path.join(IMG, "Inicio/Cesped.png"))
noche.estrellas_png(os.path.join(IMG, "Inicio/Estrellas.png"))
print("Inicio listo")
