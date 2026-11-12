using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Botón 3D: se agranda cuando la mano está encima y se pulsa al cerrar el puño.
/// Necesita un Collider. Si tiene escenaDestino, la carga al pulsarlo.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BotonMano : MonoBehaviour
{
    public string escenaDestino;
    [Tooltip("Reinicia DatosJuego antes de cambiar de escena (botón Volver al inicio)")]
    public bool reiniciarDatos;
    public bool interactuable = true;
    public float escalaEncima = 1.15f;
    [Tooltip("Escala extra que pueden poner otros scripts (por ejemplo la carpa elegida)")]
    public float multiplicador = 1f;
    public AudioClip sonido;
    public UnityEvent alPulsar;

    public bool Encima { get; private set; }

    Vector3 _escalaBase;
    float _escala = 1f;
    float _apretado;

    void Awake() => _escalaBase = transform.localScale;

    void Update()
    {
        Encima = interactuable && Agarrable.Actual == null && Mano.Sobre(transform);
        if (Encima && Mano.PunoEmpezo) Pulsar();

        float objetivo = (Encima ? escalaEncima : 1f) * multiplicador;
        _escala = Mathf.Lerp(_escala, objetivo, 1f - Mathf.Exp(-14f * Time.deltaTime));
        _apretado = Mathf.MoveTowards(_apretado, 0f, Time.deltaTime * 6f);
        transform.localScale = _escalaBase * _escala * (1f - 0.12f * _apretado);
    }

    public void Pulsar()
    {
        _apretado = 1f;
        Sonidos.Tocar(sonido ? sonido : Sonidos.Click);
        if (HandCursor.Instance) HandCursor.Instance.Pulso();
        alPulsar.Invoke();
        if (!string.IsNullOrEmpty(escenaDestino)) StartCoroutine(Ir());
    }

    IEnumerator Ir()
    {
        interactuable = false;
        yield return new WaitForSeconds(0.25f);
        if (reiniciarDatos) DatosJuego.Reiniciar();
        Escenas.Cargar(escenaDestino);
    }
}
