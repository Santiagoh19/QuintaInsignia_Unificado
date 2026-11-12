"""
Lectura del cuerpo para "Quinta Insignia" (MediaPipe Pose + Hands, mp.solutions).
JUEGO UNIFICADO: además manda los dedos de los niveles 2 y 5 (UDP 9050), el video del nivel 5 (UDP 9053)
y el del nivel 2 (TCP 9051, servidor). Nivel 4: dedos UDP 9055 y video UDP 9054.
Nivel 1: mano "x;y;puño" UDP 5052 y video UDP 9056.
Envía cada frame un JSON por UDP a Unity (PoseReceiver.cs, puerto 9051):
  {"ts", "detected", "x", "lane", "handsUp", "rightUp", "leftUp", "fist", "handsSeen"}
y ~15 veces por segundo una imagen JPEG pequeña de la cámara (CamaraOverlay.cs, puerto 9052)
para el recuadro de la cámara abajo a la derecha del juego.

Gestos (las manos se cuentan con el detector de manos: solo las que de verdad se ven,
alzadas al frente por encima del pecho):
  lane     -1 / 0 / 1  -> moverse o inclinarse a la izquierda / centro / derecha
  handsUp  true        -> DOS manos al frente, abiertas          (lanzar la caña)
  fist     true        -> DOS manos al frente, en puño           (recoger el pez)
  rightUp  true        -> SOLO la mano derecha al frente, abierta (continuar / botones)
  leftUp   true        -> SOLO la mano izquierda al frente, abierta (pausa / volver)
  rightFist true       -> SOLO la mano derecha al frente, en puño (Salir al final del nivel 5)
  pauseUp  true        -> LAS DOS manos por encima de la cabeza (menú de pausa en todos los niveles)

Teclas en la ventana:  C = usar mi posición actual como centro · R = volver al centro de la cámara · ESC / Q = salir
Uso:  python send_pose_udp.py [--camara 0] [--puerto 9051] [--video-puerto 9052] [--sin-ventana]
"""
import argparse
import json
import math
import os
import socket
import struct
import sys
import threading
import time

import cv2
import mediapipe as mp

# ---- Ajustes de sensibilidad ----
CARRIL_ENTRAR = 0.11   # distancia al centro (fracción del ancho de la imagen) para pasar a izquierda/derecha
CARRIL_SALIR = 0.07    # por debajo de esto vuelve al centro (histéresis, evita parpadeos)
SUAVIZADO = 0.35       # 0..1: cuánto sigue la posición nueva en cada frame (más bajo = más suave)
MANO_ALTA = 0.7        # una mano cuenta como "al frente" si está por encima de hombro + 0.7 anchos de hombro (pecho)
MANOS_ANCHO = 1.8      # manos a más de 1.8 anchos de hombro del centro = brazos abiertos, no "al frente"
FRAMES_ON = 3          # frames seguidos para activar un gesto
FRAMES_OFF = 3         # frames seguidos para desactivarlo

# ---- Imagen de la cámara para el recuadro del juego ----
VIDEO_ANCHO = 320      # píxeles de ancho de la imagen que se envía
VIDEO_FPS = 15         # imágenes por segundo
VIDEO_CALIDAD = 70     # calidad JPEG (0-100)


class Rebote:
    """Evita parpadeos: el gesto cambia solo tras N frames seguidos."""
    def __init__(self):
        self.valor = False
        self.cuenta = 0

    def __call__(self, crudo):
        if crudo != self.valor:
            self.cuenta += 1
            if self.cuenta >= (FRAMES_ON if crudo else FRAMES_OFF):
                self.valor = crudo
                self.cuenta = 0
        else:
            self.cuenta = 0
        return self.valor


def mano_cerrada(lm):
    """Puño: la punta de al menos 3 de los 4 dedos está más cerca de la muñeca que su articulación media."""
    w = lm[0]
    d = lambda p: ((p.x - w.x) ** 2 + (p.y - w.y) ** 2) ** 0.5
    cerrados = sum(1 for tip, pip in ((8, 6), (12, 10), (16, 14), (20, 18)) if d(lm[tip]) < d(lm[pip]))
    return cerrados >= 3


# ==================== NIVEL 5 · Sombras en la carpa ====================
# Mismo contrato que su send_fingers_udp.py: SOLO dedos levantados, coordenadas 0..1 sobre un recorte
# central 16:9 de la imagen SIN espejo, etiqueta de mano como la da MediaPipe en esa imagen sin espejo.
# Aquí la imagen se procesa en espejo, así que se deshace: x -> 1 - x y Left <-> Right.
N5_TIPS = [4, 8, 12, 16, 20]
N5_PIPS = [3, 6, 10, 14, 18]
N5_INVERT_THUMB = True       # INVERT_THUMB_RIGHT / LEFT = True en el original
N5_Y_TOL = 0.01
N5_UP_FRAMES, N5_DOWN_FRAMES = 2, 2
N5_VIDEO_W, N5_VIDEO_H, N5_VIDEO_CALIDAD = 480, 270, 70
_n5_up = {"Left": [0] * 5, "Right": [0] * 5}
_n5_dn = {"Left": [0] * 5, "Right": [0] * 5}
_n5_st = {"Left": [False] * 5, "Right": [False] * 5}


def recorte_16_9(h, w):
    """(x0, y0, ancho, alto) del recorte central 16:9 de una imagen w x h."""
    if w / h > 16 / 9:
        nw = int(h * 16 / 9)
        return (w - nw) // 2, 0, nw, h
    nh = int(w * 9 / 16)
    return 0, (h - nh) // 2, w, nh


def dedos_nivel5(rh, h, w, ts=None):
    """Paquete del puerto 9050 a partir de las manos detectadas en la imagen en espejo."""
    x0, y0, cw, ch = recorte_16_9(h, w)
    dedos, cuenta = [], {"Left": 0, "Right": 0}
    vistas = set()
    if rh.multi_hand_landmarks and rh.multi_handedness:
        for lm, handed in zip(rh.multi_hand_landmarks, rh.multi_handedness):
            etiqueta = "Right" if handed.classification[0].label == "Left" else "Left"   # deshacer espejo
            if etiqueta in vistas:
                continue
            vistas.add(etiqueta)
            # puntos en el recorte 16:9 de la imagen sin espejo
            pts = [(((1.0 - p.x) * w - x0) / cw, (p.y * h - y0) / ch) for p in lm.landmark]
            crudos = []
            for i in range(5):
                tx, ty = pts[N5_TIPS[i]]
                px, py = pts[N5_PIPS[i]]
                if i == 0:
                    up = (tx > px) if etiqueta == "Right" else (tx < px)
                    if N5_INVERT_THUMB:
                        up = not up
                else:
                    up = (ty + N5_Y_TOL) < py
                crudos.append(up)
            for i in range(5):   # histéresis
                if crudos[i]:
                    _n5_up[etiqueta][i] += 1; _n5_dn[etiqueta][i] = 0
                    if not _n5_st[etiqueta][i] and _n5_up[etiqueta][i] >= N5_UP_FRAMES:
                        _n5_st[etiqueta][i] = True
                else:
                    _n5_dn[etiqueta][i] += 1; _n5_up[etiqueta][i] = 0
                    if _n5_st[etiqueta][i] and _n5_dn[etiqueta][i] >= N5_DOWN_FRAMES:
                        _n5_st[etiqueta][i] = False
            ups = _n5_st[etiqueta]
            cuenta[etiqueta] = sum(ups)
            for i in range(5):
                if ups[i]:
                    tx, ty = pts[N5_TIPS[i]]
                    dedos.append({"id": ("L" if etiqueta == "Left" else "R") + str(i),
                                  "x": float(max(0.0, min(1.0, tx))), "y": float(max(0.0, min(1.0, ty))), "isUp": True})
    for etiqueta in ("Left", "Right"):   # mano que no se ve: sus dedos bajan
        if etiqueta not in vistas:
            _n5_st[etiqueta] = [False] * 5; _n5_up[etiqueta] = [0] * 5; _n5_dn[etiqueta] = [0] * 5
    return {"ts": ts if ts is not None else time.time(), "fingers": dedos, "countL": int(cuenta["Left"]), "countR": int(cuenta["Right"])}
# ======================================================================


# ==================== NIVEL 4 · Pinzas de fuego ====================
# Mismo contrato que SU send_fingers_udp.py: imagen EN ESPEJO, etiqueta Left/Right tal cual la da
# MediaPipe sobre esa imagen, y las 5 puntas de cada mano detectada (también las bajadas) con isUp.
def dedos_nivel4(rh, ts):
    dedos, cl, cr = [], 0, 0
    if rh.multi_hand_landmarks and rh.multi_handedness:
        for lm, handed in zip(rh.multi_hand_landmarks, rh.multi_handedness):
            etiqueta = handed.classification[0].label
            p = lm.landmark
            arriba = [(p[4].x < p[3].x) if etiqueta == "Right" else (p[4].x > p[3].x)]
            arriba += [p[t].y < p[t - 2].y for t in (8, 12, 16, 20)]
            for i, t in enumerate((4, 8, 12, 16, 20)):
                dedos.append({"id": ("L" if etiqueta == "Left" else "R") + str(i),
                              "x": float(p[t].x), "y": float(p[t].y), "isUp": bool(arriba[i])})
                if etiqueta == "Left":
                    cl += 1
                else:
                    cr += 1
    return {"ts": ts, "fingers": dedos, "countL": cl, "countR": cr}
# ======================================================================


# ==================== NIVEL 1 · Arma tu carpa ====================
# Mismo contrato que SU send_mano_puno_udp.py: texto "x;y;puño" por UDP 5052, solo cuando hay mano.
# x, y = centro de la palma (landmarks 0, 5, 9, 13, 17) sobre la imagen EN ESPEJO, 0..1, y = 0 arriba.
# Puño con histéresis: se cierra con 3 o más dedos doblados y se abre con 1 o menos.
# Su juego usa una sola mano: se sigue la misma mano mientras esté en la imagen.
class ManoNivel1:
    DEDOS = ((8, 6), (12, 10), (16, 14), (20, 18))   # (punta, PIP) de índice a meñique
    PALMA = (0, 5, 9, 13, 17)

    def __init__(self):
        self.puno = False
        self.etiqueta = None
        self.lm = None

    def __call__(self, rh):
        self.lm = None
        if not rh.multi_hand_landmarks:
            self.puno = False
            self.etiqueta = None
            return None
        etiquetas = [h.classification[0].label for h in (rh.multi_handedness or [])]
        i = etiquetas.index(self.etiqueta) if self.etiqueta in etiquetas else 0
        self.etiqueta = etiquetas[i] if i < len(etiquetas) else None
        mano = rh.multi_hand_landmarks[i]
        p = mano.landmark
        d = lambda a, b: math.hypot(a.x - b.x, a.y - b.y)
        doblados = sum(1 for t, pip in self.DEDOS if d(p[t], p[0]) < d(p[pip], p[0]))
        if doblados >= 3:
            self.puno = True
        elif doblados <= 1:
            self.puno = False
        x = min(max(sum(p[k].x for k in self.PALMA) / 5.0, 0.0), 1.0)
        y = min(max(sum(p[k].y for k in self.PALMA) / 5.0, 0.0), 1.0)
        self.lm = mano
        return f"{x:.4f};{y:.4f};{1 if self.puno else 0}"
# ======================================================================


# ==================== NIVEL 2 · Ruta de Energía: video por TCP 9051 ====================
# Su Unity (RecorridoTransport) se conecta como cliente a 127.0.0.1:9051 (TCP; el 9051 UDP del nivel 3
# es otro espacio de puertos y no choca). Cada trama: struct "!Id" (largo del JPEG, ts) + JPEG 960x540
# del recorte 16:9 SIN espejo. El ts tiene que ser el MISMO del paquete de dedos del 9050 de ese cuadro.
class ServidorVideoNivel2:
    def __init__(self, puerto=9051):
        self.ultimo = None
        self.cliente = False
        self.evento = threading.Event()
        threading.Thread(target=self._servir, args=(puerto,), daemon=True).start()

    def publicar(self, ts, jpg):
        self.ultimo = (ts, jpg)
        self.evento.set()

    def _servir(self, puerto):
        try:
            srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            srv.bind(("127.0.0.1", puerto))
            srv.listen(1)
        except OSError as e:
            print("Video nivel 2: no se pudo abrir TCP", puerto, e)
            return
        while True:
            try:
                con, _ = srv.accept()
                con.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            except OSError:
                time.sleep(0.5)
                continue
            self.cliente = True
            try:
                while True:
                    if not self.evento.wait(2.0):
                        continue
                    self.evento.clear()
                    ts, jpg = self.ultimo
                    con.sendall(struct.pack("!Id", len(jpg), ts) + jpg)
            except OSError:
                pass
            finally:
                self.cliente = False
                try:
                    con.close()
                except OSError:
                    pass
# =====================================================================================


def abrir_camara(preferida):
    """Prueba la cámara pedida y luego otras (0-3) con DirectShow y Media Foundation.
    Solo acepta una cámara que de verdad entregue imagen (algunos portátiles tienen
    una cámara infrarroja o virtual en el índice 0 que abre pero no da cuadros)."""
    indices = [preferida] + [i for i in range(4) if i != preferida]
    backends = [cv2.CAP_DSHOW, cv2.CAP_MSMF] if sys.platform == "win32" else [cv2.CAP_ANY]
    respaldo = None
    for i in indices:
        for b in backends:
            try:
                cap = cv2.VideoCapture(i, b)
                if not cap.isOpened():
                    cap.release()
                    continue
                da_imagen = False
                for _ in range(15):
                    ok, frame = cap.read()
                    if ok and frame is not None and frame.size > 0:
                        da_imagen = True
                        if frame.max() > 10:
                            print(f"Cámara {i} abierta (backend {b})")
                            if respaldo is not None:
                                respaldo.release()
                            return cap
                    time.sleep(0.05)
                if da_imagen and respaldo is None:
                    respaldo = cap          # da imagen pero oscura (tapa cerrada?): se usa si no hay otra
                else:
                    cap.release()
            except Exception as e:
                print("Cámara", i, "falló:", e)
    return respaldo


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--camara", type=int, default=0)
    ap.add_argument("--puerto", type=int, default=9051)
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--video-puerto", type=int, default=9052)
    ap.add_argument("--sin-ventana", action="store_true")
    ap.add_argument("--puerto-dedos", type=int, default=9050)          # nivel 5: FingersMove.cs
    ap.add_argument("--video-puerto-nivel5", type=int, default=9053)   # nivel 5: CamaraJugador.cs
    ap.add_argument("--video-tcp-nivel2", type=int, default=9051)      # nivel 2: RecorridoTransport (TCP)
    ap.add_argument("--puerto-dedos-nivel4", type=int, default=9055)   # nivel 4: FingersMove (otro formato)
    ap.add_argument("--video-puerto-nivel4", type=int, default=9054)   # nivel 4: CamaraPreview
    ap.add_argument("--puerto-mano-nivel1", type=int, default=5052)    # nivel 1: HandTracker ("x;y;puño")
    ap.add_argument("--video-puerto-nivel1", type=int, default=9056)   # nivel 1: CamaraPreview de la carpa
    args = ap.parse_args()

    pose = mp.solutions.pose.Pose(model_complexity=1, min_detection_confidence=0.5,
                                  min_tracking_confidence=0.5)
    manos = mp.solutions.hands.Hands(max_num_hands=2, model_complexity=1,   # 1 como el nivel 5 (dedos más precisos)
                                     min_detection_confidence=0.5, min_tracking_confidence=0.5)

    cap = abrir_camara(args.camara)
    if cap is None:
        print("No se pudo abrir ninguna cámara. Revisa: Configuración > Privacidad > Cámara "
              "(permitir aplicaciones de escritorio) y que otra app no la esté usando.")
        sys.exit(2)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    destino = (args.host, args.puerto)
    print(f"Enviando a {destino}. C = recalibrar, ESC/Q = salir.")

    centro = 0.5          # centro de la cámara (C lo cambia a tu posición, R lo devuelve aquí)
    x_suave = None
    carril = 0
    reb_manos, reb_puno, reb_derecha, reb_izquierda = Rebote(), Rebote(), Rebote(), Rebote()
    reb_derecha_puno = Rebote()
    reb_pausa = Rebote()
    destino_video = (args.host, args.video_puerto)
    video_n2 = ServidorVideoNivel2(args.video_tcp_nivel2)
    ultimo_n2 = 0.0
    ultimo_video = 0.0
    mano_n1 = ManoNivel1()

    while True:
        ok, frame = cap.read()
        if not ok:
            time.sleep(0.01)
            continue
        crudo = frame                # sin espejo (para el video del nivel 5)
        frame = cv2.flip(frame, 1)  # espejo: tu derecha = derecha de la pantalla
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        rgb.flags.writeable = False
        rp = pose.process(rgb)
        rh = manos.process(rgb)
        lista_manos = [m.landmark for m in (rh.multi_hand_landmarks or [])]

        detectado = rp.pose_landmarks is not None
        x = 0.5
        manos_crudo = False
        derecha_crudo = False
        izquierda_crudo = False
        derecha_puno_crudo = False
        pausa_crudo = False
        puno_crudo = False
        info_manos = []
        n_alzadas, cerradas = 0, 0
        estado_txt = "No te veo: ponte frente a la cámara"

        if detectado:
            lm = rp.pose_landmarks.landmark
            hi, hd = lm[11], lm[12]
            ancho = max(abs(hi.x - hd.x), 0.05)
            x = (hi.x + hd.x) / 2
            if lm[0].visibility > 0.5:            # nariz: al inclinarse la cabeza se mueve más
                x = 0.6 * x + 0.4 * lm[0].x
            x_suave = x if x_suave is None else x_suave + (x - x_suave) * SUAVIZADO
            x = x_suave

            # Carril: posición respecto al centro, en fracción del ancho de la imagen
            # (líneas fijas: no cambian si te acercas o te alejas de la cámara)
            off = x - centro
            if carril == 0:
                if off <= -CARRIL_ENTRAR: carril = -1
                elif off >= CARRIL_ENTRAR: carril = 1
            elif carril * off < CARRIL_SALIR:
                carril = 0 if abs(off) < CARRIL_ENTRAR else (1 if off > 0 else -1)

            # --- Manos al frente, contadas con el detector de manos (no con la pose, que "adivina"
            #     la muñeca aunque la mano esté abajo o fuera de cámara)
            hombro_y = (hi.y + hd.y) / 2
            cmx = (hi.x + hd.x) / 2
            limite = hombro_y + MANO_ALTA * ancho
            for hl in lista_manos:
                cx = sum(hl[i].x for i in (0, 5, 9, 13, 17)) / 5
                cy = sum(hl[i].y for i in (0, 5, 9, 13, 17)) / 5
                info_manos.append((cx, cy, mano_cerrada(hl)))
            alzadas = [m for m in info_manos if m[1] < limite and abs(m[0] - cmx) < MANOS_ANCHO * ancho]
            n_alzadas = len(alzadas)
            cerradas = sum(1 for m in alzadas if m[2])

            # la pose ayuda a saber si la OTRA mano también está arriba (por si el detector de manos la pierde)
            mi, md = lm[15], lm[16]
            pose_dos_arriba = (mi.visibility > 0.6 and md.visibility > 0.6
                               and mi.y < limite and md.y < limite)

            # PAUSA: las dos muñecas claramente por encima de la cabeza (más arriba que los ojos).
            # Es una postura que no se usa en ningún juego; si se cumple, anula los demás gestos
            ojos_y = min(lm[2].y, lm[5].y)
            cabeza_y = ojos_y - 0.2 * ancho             # un poco por encima de la frente
            mano_sobre = lambda w: w.visibility > 0.5 and w.y < cabeza_y
            manos_det_sobre = sum(1 for m in info_manos if m[1] < cabeza_y)
            pausa_crudo = (mano_sobre(mi) and mano_sobre(md)) or (manos_det_sobre >= 2 and (mano_sobre(mi) or mano_sobre(md)))

            if pausa_crudo:
                pass
            elif n_alzadas >= 2:
                if cerradas == n_alzadas:
                    puno_crudo = True          # dos puños al frente
                elif cerradas == 0:
                    manos_crudo = True         # dos manos abiertas al frente
            elif n_alzadas == 1:
                cx, cy, cerrada = alzadas[0]
                if cerrada and pose_dos_arriba:
                    puno_crudo = True          # se ve un puño y la pose dice que las dos están arriba
                elif cerrada and cx > cmx - 0.2 * ancho:
                    derecha_puno_crudo = True  # solo la mano derecha, en puño
                elif not cerrada and cx > cmx - 0.2 * ancho:
                    derecha_crudo = True       # solo la mano derecha (lado derecho de la imagen en espejo)
                elif not cerrada:
                    izquierda_crudo = True     # solo la mano izquierda (volver, dentro del menú de pausa)
        else:
            x_suave = None

        vistas = len(lista_manos)

        manos_arriba = reb_manos(manos_crudo and detectado)
        derecha_arriba = reb_derecha(derecha_crudo and detectado)
        izquierda_arriba = reb_izquierda(izquierda_crudo and detectado)
        puno = reb_puno(puno_crudo)
        derecha_puno = reb_derecha_puno(derecha_puno_crudo and detectado)
        pausa = reb_pausa(pausa_crudo and detectado)
        if not detectado:
            carril = 0

        paquete = {
            "ts": time.time(), "detected": detectado, "x": round(x, 4),
            "lane": carril,
            "handsUp": manos_arriba, "rightUp": derecha_arriba, "leftUp": izquierda_arriba, "fist": puno, "rightFist": derecha_puno, "pauseUp": pausa, "handsSeen": vistas,
        }
        try:
            sock.sendto(json.dumps(paquete).encode("utf-8"), destino)
        except OSError:
            pass

        # niveles 2 y 5: dedos levantados (cada cuadro)
        ts_cuadro = time.time()
        try:
            fh0, fw0 = frame.shape[:2]
            sock.sendto(json.dumps(dedos_nivel5(rh, fh0, fw0, ts_cuadro)).encode("utf-8"), (args.host, args.puerto_dedos))
        except OSError:
            pass
        # nivel 4: 5 puntas por mano, en espejo (cada cuadro)
        try:
            sock.sendto(json.dumps(dedos_nivel4(rh, ts_cuadro)).encode("utf-8"), (args.host, args.puerto_dedos_nivel4))
        except OSError:
            pass

        # nivel 1: centro de la palma y puño (solo si hay mano)
        msg_n1 = mano_n1(rh)
        if msg_n1:
            try:
                sock.sendto(msg_n1.encode("utf-8"), (args.host, args.puerto_mano_nivel1))
            except OSError:
                pass

        # nivel 2: video 960x540 por TCP (solo si su juego está conectado), con el mismo ts
        if video_n2.cliente and ts_cuadro - ultimo_n2 >= 1.0 / 20:
            ultimo_n2 = ts_cuadro
            x0, y0, cw, ch = recorte_16_9(*crudo.shape[:2])
            n2 = cv2.resize(crudo[y0:y0 + ch, x0:x0 + cw], (960, 540), interpolation=cv2.INTER_AREA)
            ok2, jpg2 = cv2.imencode(".jpg", n2, [cv2.IMWRITE_JPEG_QUALITY, 75])
            if ok2:
                video_n2.publicar(ts_cuadro, jpg2.tobytes())

        # imagen pequeña para el recuadro de la cámara en el juego (antes de dibujar la ventana)
        ahora = time.time()
        if ahora - ultimo_video >= 1.0 / VIDEO_FPS:
            ultimo_video = ahora
            fh, fw = frame.shape[:2]
            vh = int(VIDEO_ANCHO * fh / fw)
            peq = cv2.resize(frame, (VIDEO_ANCHO, vh), interpolation=cv2.INTER_AREA)
            if detectado:
                for k in (-1, 1):                     # líneas de los carriles (amarillo del juego)
                    lx = int((centro + k * CARRIL_ENTRAR) * VIDEO_ANCHO)
                    cv2.line(peq, (lx, 0), (lx, vh), (93, 201, 244), 1, cv2.LINE_AA)
                cv2.circle(peq, (int(x * VIDEO_ANCHO), vh // 2), 5, (84, 131, 239), -1, cv2.LINE_AA)
            ok_jpg, jpg = cv2.imencode(".jpg", peq, [cv2.IMWRITE_JPEG_QUALITY, VIDEO_CALIDAD])
            if ok_jpg and len(jpg) < 60000:
                try:
                    sock.sendto(jpg.tobytes(), destino_video)
                except OSError:
                    pass
            # nivel 4: 320x240 en espejo (su recuadro "Tu mano")
            ok4, jpg4 = cv2.imencode(".jpg", cv2.resize(frame, (320, 240), interpolation=cv2.INTER_AREA),
                                     [cv2.IMWRITE_JPEG_QUALITY, 65])
            if ok4 and len(jpg4) <= 60000:
                try:
                    sock.sendto(jpg4.tobytes(), (args.host, args.video_puerto_nivel4))
                except OSError:
                    pass
            # nivel 1: 320x240 en espejo con la mano dibujada y ABIERTA/PUÑO (su recuadro "Tu mano")
            v1 = cv2.resize(frame, (320, 240), interpolation=cv2.INTER_AREA)
            if mano_n1.lm is not None:
                mp.solutions.drawing_utils.draw_landmarks(v1, mano_n1.lm, mp.solutions.hands.HAND_CONNECTIONS)
                cv2.putText(v1, "PUNO" if mano_n1.puno else "ABIERTA", (8, 26), cv2.FONT_HERSHEY_SIMPLEX, 0.7,
                            (0, 0, 255) if mano_n1.puno else (0, 200, 0), 2)
            ok1, jpg1 = cv2.imencode(".jpg", v1, [cv2.IMWRITE_JPEG_QUALITY, 65])
            if ok1 and len(jpg1) <= 60000:
                try:
                    sock.sendto(jpg1.tobytes(), (args.host, args.video_puerto_nivel1))
                except OSError:
                    pass
            # nivel 5: el mismo recorte 16:9 sin espejo que usa su ventanita "Tu cámara"
            x0, y0, cw, ch = recorte_16_9(*crudo.shape[:2])
            n5 = cv2.resize(crudo[y0:y0 + ch, x0:x0 + cw], (N5_VIDEO_W, N5_VIDEO_H), interpolation=cv2.INTER_AREA)
            ok5, jpg5 = cv2.imencode(".jpg", n5, [cv2.IMWRITE_JPEG_QUALITY, N5_VIDEO_CALIDAD])
            if ok5 and len(jpg5) < 65000:
                try:
                    sock.sendto(jpg5.tobytes(), (args.host, args.video_puerto_nivel5))
                except OSError:
                    pass

        if not args.sin_ventana:
            h, w = frame.shape[:2]
            if detectado:
                estado_txt = ["Izquierda", "Centro", "Derecha"][carril + 1]
                for k in (-1, 1):
                    lx = int((centro + k * CARRIL_ENTRAR) * w)
                    cv2.line(frame, (lx, 0), (lx, h), (93, 201, 244), 2)
                cx_px = int(centro * w)                      # centro, punteado
                for yy in range(0, h, 16):
                    cv2.line(frame, (cx_px, yy), (cx_px, yy + 8), (230, 230, 230), 1)
                cv2.circle(frame, (int(x * w), int(h * 0.5)), 10, (84, 131, 239), -1)
                for i in (11, 12, 15, 16):
                    p = lm[i]
                    cv2.circle(frame, (int(p.x * w), int(p.y * h)), 6, (43, 57, 24), -1)
            for hand in lista_manos:
                for p in hand:
                    cv2.circle(frame, (int(p.x * w), int(p.y * h)), 2, (115, 95, 54), -1)
            for cx, cy, cerrada in info_manos:   # aro en cada mano: verde abierta, naranja puño
                color = (84, 131, 239) if cerrada else (91, 138, 79)
                cv2.circle(frame, (int(cx * w), int(cy * h)), 34, color, 3)
            if detectado:
                ly = int(limite * h)              # línea: por encima de ella la mano cuenta como "al frente"
                cv2.line(frame, (0, ly), (w, ly), (93, 201, 244), 1)
            gesto = ("MANOS SOBRE LA CABEZA (pausa)" if pausa else
                     "DOS MANOS ABIERTAS (lanzar)" if manos_arriba else
                     "DOS PUÑOS (recoger)" if puno else
                     "MANO DERECHA (continuar)" if derecha_arriba else
                     "MANO IZQUIERDA" if izquierda_arriba else "")
            linea2 = f"Manos al frente: {n_alzadas}  ({cerradas} en puño)   " + gesto
            cv2.rectangle(frame, (0, 0), (w, 60), (229, 243, 237), -1)
            cv2.putText(frame, estado_txt, (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (43, 57, 24), 2)
            cv2.putText(frame, linea2.replace("Ñ", "N").replace("ñ", "n"), (10, 52), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (84, 131, 239), 2)
            cv2.imshow("Quinta Insignia - camara", frame)
            k = cv2.waitKey(1) & 0xFF
            if k in (27, ord("q")):
                break
            if k == ord("c") and x_suave is not None:
                centro, carril = x_suave, 0
                print(f"Centro en tu posición actual: x={centro:.2f}")
            if k == ord("r"):
                centro, carril = 0.5, 0
                print("Centro de la cámara.")

    cap.release()
    cv2.destroyAllWindows()
    pose.close()
    manos.close()


if __name__ == "__main__":
    main()
