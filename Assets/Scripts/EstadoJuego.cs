/// <summary>Nivel de dificultad elegido al comenzar.</summary>
public enum Dificultad { Facil = 0, Normal = 1, Dificil = 2 }

/// <summary>
/// Progreso de la aventura que se conserva al cambiar de escena.
/// </summary>
public static class EstadoJuego
{
    public const int MetaLena = 3;
    public const int MetaPeces = 3;

    public static int Lena;
    public static int Peces;
    public static bool LenaCompleta;
    public static bool ComidaCompleta;
    /// <summary>Ya se mostró en esta partida la ventana "Puedes pausar levantando la mano izquierda".</summary>
    public static bool AvisoPausaMostrado;

    /// <summary>Insignias: 1 Arma tu carpa, 2 Ruta de Energía (ya ganadas en los juegos anteriores), 3 = esta (Cruce del bosque).</summary>
    public const int TotalInsignias = 5;
    public const int InsigniasPrevias = 2;
    public const int InsigniaDeEsteNivel = 3;
    public static int InsigniasGanadas = 0;   // JUEGO UNIFICADO: se empieza en el nivel 1 (Arma tu carpa) sin insignias

    /// <summary>Se elige en la pantalla de dificultad (al inicio). Empieza en Normal.</summary>
    public static Dificultad Dificultad = Dificultad.Normal;

    /// <summary>Se dispara al cambiar la dificultad desde el menú de pausa (las escenas se reajustan al momento).</summary>
    public static event System.Action DificultadCambiada;

    public static void CambiarDificultad(Dificultad d)
    {
        if (d == Dificultad) return;
        Dificultad = d;
        DificultadCambiada?.Invoke();
    }

    /// <summary>Devuelve el valor que corresponde a la dificultad actual.</summary>
    public static float SegunDificultad(float facil, float normal, float dificil)
    {
        switch (Dificultad)
        {
            case Dificultad.Facil: return facil;
            case Dificultad.Dificil: return dificil;
            default: return normal;
        }
    }

    public static string NombreDificultad =>
        Dificultad == Dificultad.Facil ? "Fácil" : Dificultad == Dificultad.Dificil ? "Difícil" : "Normal";

    /// <summary>Reinicia el progreso del nivel 3 (la dificultad elegida y el aviso de la pausa se conservan).</summary>
    public static void Reiniciar()
    {
        Lena = 0;
        Peces = 0;
        LenaCompleta = false;
        ComidaCompleta = false;
        InsigniasGanadas = InsigniasPrevias;
    }
}
