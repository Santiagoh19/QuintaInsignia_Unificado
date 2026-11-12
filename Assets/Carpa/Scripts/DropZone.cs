using UnityEngine;

/// <summary>
/// Zona sobre la tela que acepta los tubos (escena Tubos). Necesita un Collider en modo Trigger.
/// Cada tubo soltado aquí se acomoda en el siguiente lugar libre y cuenta como tarea hecha.
/// </summary>
public class DropZone : MonoBehaviour, IZonaSoltar
{
    [Tooltip("Dónde queda cada tubo, en orden")]
    public Transform[] lugares;
    [Tooltip("Marca que se ilumina mientras la mano trae un tubo. Opcional")]
    public Renderer marca;
    public Color colorMarca = new Color(1f, 1f, 0.6f, 0.35f);

    int _siguiente;

    void Update()
    {
        if (!marca) return;
        bool trae = Agarrable.Actual != null && _siguiente < lugares.Length;
        marca.enabled = trae;
        if (trae)
        {
            var c = colorMarca;
            c.a *= 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
            Pintura.Pintar(marca, c);
        }
    }

    public bool Aceptar(Agarrable objeto)
    {
        if (_siguiente >= lugares.Length) return false;
        var lugar = lugares[_siguiente++];
        objeto.FijarEn(lugar.position, lugar.rotation, 0.3f);
        Sonidos.Tocar(Sonidos.Exito);
        if (Tareas.Instancia) Tareas.Instancia.Sumar();
        return true;
    }
}
