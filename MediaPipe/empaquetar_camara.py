"""
Arma QuintaInsigniaCamara.exe (carpeta dist/QuintaInsigniaCamara) con PyInstaller.
Lo llama Unity desde Juego > Exportar. Se puede correr a mano:
    <python de trackingMano> empaquetar_camara.py

Incluye un parche para Python 3.10.0: su módulo `dis` tiene un error
(IndexError: tuple index out of range en _get_const_info) que rompe el análisis
de PyInstaller. Las versiones 3.10.1+ ya lo corrigieron; el parche no afecta nada más.
"""
import dis
import os
import sys

_original = dis._get_const_info


def _get_const_info_seguro(*args, **kwargs):
    try:
        return _original(*args, **kwargs)
    except IndexError:
        indice = args[0] if args else None
        return indice, repr(indice)


dis._get_const_info = _get_const_info_seguro

import PyInstaller.__main__  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
os.chdir(AQUI)

PyInstaller.__main__.run([
    "--noconfirm", "--clean", "--onedir",
    "--name", "QuintaInsigniaCamara",
    "--distpath", "dist", "--workpath", "build",
    # modelos (.tflite / .binarypb) y librerías nativas de MediaPipe
    "--collect-data", "mediapipe",
    "--collect-binaries", "mediapipe",
    "--hidden-import", "mediapipe.python.solutions.pose",
    "--hidden-import", "mediapipe.python.solutions.hands",
    # cosas pesadas que no usamos
    "--exclude-module", "tkinter",
    "--exclude-module", "IPython",
    "--exclude-module", "jupyter",
    "send_pose_udp.py",
])
print("\nListo: dist/QuintaInsigniaCamara/QuintaInsigniaCamara.exe")
