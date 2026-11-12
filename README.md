# Quinta Insignia

Videojuego de motricidad fina hecho en Unity. Se juega frente a la cámara web, con el cuerpo y las manos (MediaPipe), o con el teclado.

Tiene 5 niveles, y cada uno da una insignia:

1. **Arma tu carpa**
2. **Ruta de Energía**
3. **Cruce del bosque**
4. **Pinzas de fuego**
5. **Sombras en la carpa**

## Jugar (no hace falta instalar nada)

1. Ve a **[Releases](../../releases/latest)** y descarga `QuintaInsignia.zip`.
2. Antes de descomprimir: clic derecho en el zip → **Propiedades** → marca **Desbloquear** → Aceptar. Después: clic derecho → **Extraer todo**.
3. Abre `QuintaInsignia.exe`. La cámara se enciende sola.

Requisitos: Windows 10/11 de 64 bits y una cámara web.

- **Pausa:** levanta las dos manos por encima de la cabeza, o presiona Esc.
- **Sin cámara:** se juega con las flechas, Enter, B (lanzar) y C (recoger).

## Abrir el proyecto en Unity

- Versión de Unity: **6000.3.28f1** (Unity 6.3 LTS).
- El programa de la cámara ya empaquetado no está en el repositorio porque pesa unos 280 MB. Para exportar el juego con cámara:
  - **Sin instalar Python:** menú **Juego → Exportar → 0. Descargar la cámara ya hecha**. Lo descarga de los Releases y lo deja en `MediaPipe/dist`.
  - **Con Python + MediaPipe:** menú **Juego → Exportar → 1. Preparar la cámara**. Lo arma desde `MediaPipe/send_pose_udp.py`.
- Después: **Juego → Exportar → 2. Exportar el juego para Windows**. Esto crea `Builds/QuintaInsignia` y `Builds/QuintaInsignia.zip`.

## Créditos de audio

Ver `Assets/Resources/Audio/CREDITOS.txt` y `Assets/RecorridoAR/Audio/LICENCIA_AUDIO.txt`.
