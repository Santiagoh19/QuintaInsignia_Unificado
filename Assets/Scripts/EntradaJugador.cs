using UnityEngine;

/// <summary>
/// Punto único de entrada del jugador: teclado + lectura del cuerpo (MediaPipe, ver PoseReceiver).
///   Izquierda / Derecha   → flechas  · o moverse al carril izquierdo / derecho frente a la cámara
///   Accion (continuar)    → Enter, Espacio · o levantar solo la mano derecha
///   Lanzar (B)            → B · o levantar las dos manos al frente
///   Recoger (mantener C)  → C · o cerrar los puños
/// </summary>
public static class EntradaJugador
{
    // Mientras el juego está en pausa (Pausa.cs) el juego no recibe ninguna entrada.
    static bool Libre => !Pausa.Activa;
    static PoseReceiver Pose => Libre ? PoseReceiver.Instancia : null;

    /// <summary>True cuando la cámara está enviando datos y ve a la persona.</summary>
    public static bool PoseActiva => Pose != null && Pose.Activo;

    /// <summary>Carril del cuerpo: -1 izquierda, 0 centro, 1 derecha (0 si no hay cámara).</summary>
    public static int Carril => PoseActiva ? Pose.Carril : 0;

    // Solo teclado (para cuando el cuerpo ya se lee de forma directa, p. ej. carriles)
    public static bool TeclaIzquierda => Libre && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A));
    public static bool TeclaDerecha => Libre && (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D));

    // Pulsaciones (un solo frame)
    public static bool Izquierda => Libre && (
        Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) ||
        (Pose != null && Pose.CarrilCambioA(-1)));

    public static bool Derecha => Libre && (
        Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) ||
        (Pose != null && Pose.CarrilCambioA(1)));

    public static bool Accion => Libre && (
        Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
        (Pose != null && Pose.ManoDerechaAhora));

    // Pesca
    public static bool Lanzar => Libre && (
        Input.GetKeyDown(KeyCode.B) || (Pose != null && Pose.ManosArribaAhora));

    public static bool RecogerMantenido => Libre && (
        Input.GetKey(KeyCode.C) || (PoseActiva && Pose.Puno));

    // Movimiento continuo: -1 izquierda, 0 quieto, 1 derecha
    public static float Horizontal
    {
        get
        {
            float h = 0f;
            if (!Libre) return 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) h -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) h += 1f;
            if (h == 0f && PoseActiva) h = Pose.Carril;
            return h;
        }
    }
}
