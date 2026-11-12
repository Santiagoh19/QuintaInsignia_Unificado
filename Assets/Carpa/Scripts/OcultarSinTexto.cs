using UnityEngine;
using UnityEngine.UI;

/// <summary>Esconde el panel (su imagen de fondo) mientras el texto está vacío.</summary>
[RequireComponent(typeof(Graphic))]
public class OcultarSinTexto : MonoBehaviour
{
    public Text texto;
    Graphic _fondo;

    void Awake() => _fondo = GetComponent<Graphic>();

    void LateUpdate()
    {
        if (texto) _fondo.enabled = !string.IsNullOrEmpty(texto.text);
    }
}
