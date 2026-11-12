using UnityEngine;

/// <summary>
/// Va en cada escena. Comprueba que HandManager y Cursor existen (vienen desde Inicio);
/// si la escena se abrió sola, los crea para que igual se pueda probar.
/// </summary>
[DefaultExecutionOrder(-200)]
public class VerificarMano : MonoBehaviour
{
    void Awake()
    {
        HandTracker.Asegurar();
    }
}
