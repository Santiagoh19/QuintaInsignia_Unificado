using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Cuenta las tareas hechas en la escena (objetos botados, esquinas, tubos, estacas...).
/// Al completarlas espera unos segundos y carga la escena siguiente sola, sin botón.
/// </summary>
public class Tareas : MonoBehaviour
{
    public static Tareas Instancia { get; private set; }

    public int total = 8;
    [Tooltip("Segundos de espera antes de cambiar de escena")]
    public float espera = 1f;
    public string escenaSiguiente = "AbrirMaleta";

    [Header("Interfaz (opcional)")]
    public Text textoProgreso;
    public string formato = "{0} / {1}";
    [Tooltip("Se muestra al completar (por ejemplo un letrero de ¡Muy bien!)")]
    public GameObject mensajeCompleto;

    public UnityEvent alSumar, alCompletar;

    public int Hechas { get; private set; }
    public bool Completo => Hechas >= total;
    public float Progreso => total > 0 ? Mathf.Clamp01(Hechas / (float)total) : 1f;

    void Awake()
    {
        Instancia = this;
        if (mensajeCompleto) mensajeCompleto.SetActive(false);
    }

    void Start() => Actualizar();

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    public void Sumar()
    {
        if (Completo) return;
        Hechas++;
        Actualizar();
        alSumar.Invoke();
        if (Completo) StartCoroutine(Terminar());
    }

    void Actualizar()
    {
        if (textoProgreso) textoProgreso.text = string.Format(formato, Hechas, total);
    }

    IEnumerator Terminar()
    {
        Sonidos.Tocar(Sonidos.Fanfarria);
        if (mensajeCompleto) mensajeCompleto.SetActive(true);
        alCompletar.Invoke();
        yield return new WaitForSeconds(espera);
        Escenas.Cargar(escenaSiguiente);
    }
}
