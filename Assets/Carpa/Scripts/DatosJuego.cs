using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Datos que pasan de una escena a otra.</summary>
public static class DatosJuego
{
    /// <summary>Carpa elegida en ElegirCarpa: 1, 2 o 3.</summary>
    public static int carpaElegida = 1;

    public static readonly string[] NOMBRES = { "Canadiense", "Iglú", "Tipi" };
    static readonly Color[] COLORES =
    {
        new Color(0.95f, 0.63f, 0.23f),   // naranja (como la carpa de la portada)
        new Color(0.24f, 0.55f, 0.82f),   // azul
        new Color(0.47f, 0.67f, 0.28f),   // verde (como la carpa pequeña de la portada)
    };

    public static int Indice => Mathf.Clamp(carpaElegida, 1, 3) - 1;
    public static Color ColorCarpa(int numero) => COLORES[Mathf.Clamp(numero, 1, 3) - 1];
    public static Color ColorElegido => COLORES[Indice];

    public static void Reiniciar()
    {
        carpaElegida = 1;
    }
}

/// <summary>Cambiar de escena con aviso si falta en Build Settings.</summary>
public static class Escenas
{
    public static void Cargar(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return;
        if (!Application.CanStreamedLevelBeLoaded(nombre))
        {
            Debug.LogError("[Carpa] La escena '" + nombre + "' no está en Build Settings.");
            return;
        }
        if (Agarrable.Actual) Agarrable.Actual.Cancelar();
        SceneManager.LoadScene(nombre);
    }
}
