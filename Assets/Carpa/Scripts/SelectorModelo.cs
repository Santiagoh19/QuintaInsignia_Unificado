using UnityEngine;

/// <summary>
/// Muestra el modelo de la carpa elegida (DatosJuego.carpaElegida) y pinta del color de esa carpa
/// lo que corresponda (maleta, tela, tubos...).
/// </summary>
public class SelectorModelo : MonoBehaviour
{
    [Tooltip("Un hijo por carpa: 1, 2 y 3")]
    public GameObject[] modelos;
    [Tooltip("Se pintan del color de la carpa elegida")]
    public Renderer[] pintar;
    [Tooltip("0 = usar la carpa elegida; 1-3 = mostrar siempre esa")]
    public int forzar;

    void Awake() => Aplicar();

    public void Aplicar()
    {
        int n = forzar > 0 ? forzar : DatosJuego.carpaElegida;
        n = Mathf.Clamp(n, 1, 3);
        for (int i = 0; i < modelos.Length; i++)
            if (modelos[i]) modelos[i].SetActive(i == n - 1);
        var color = DatosJuego.ColorCarpa(n);
        foreach (var r in pintar) Pintura.Pintar(r, color);
    }
}

/// <summary>Cambia el color de un Renderer sin crear materiales nuevos (Built-in y URP).</summary>
public static class Pintura
{
    static MaterialPropertyBlock _bloque;

    public static void Pintar(Renderer r, Color color)
    {
        if (!r) return;
        if (_bloque == null) _bloque = new MaterialPropertyBlock();
        r.GetPropertyBlock(_bloque);
        _bloque.SetColor("_Color", color);
        _bloque.SetColor("_BaseColor", color);
        r.SetPropertyBlock(_bloque);
    }
}
