using System.Collections;
using UnityEngine;

/// <summary>
/// Cremallera de la maleta (escena AbrirMaleta). El tirador se agarra con el puño y solo se mueve
/// sobre la línea Inicio → Fin: la posición de la mano se proyecta sobre esa línea en la pantalla.
/// Si la mano se aleja demasiado de la línea, el tirador se suelta y queda donde estaba.
/// </summary>
public class Cremallera : MonoBehaviour
{
    public Transform tirador;
    public Transform inicio, fin;
    [Tooltip("La maleta que se abre según el progreso")]
    public Maleta maleta;
    [Tooltip("Tira oscura que crece desde Inicio hasta el tirador (la parte ya abierta), alineada con su eje X " +
             "y sin padre escalado. Opcional")]
    public Transform abertura;

    [Tooltip("Qué tan lejos de la línea puede ir la mano antes de soltar el tirador (fracción del alto de pantalla)")]
    public float distanciaMaxima = 0.12f;
    [Tooltip("Si está activo la cremallera no se puede volver a cerrar")]
    public bool soloAvanza = true;

    public float espera = 2f;
    public string escenaSiguiente = "Tela";

    [Range(0f, 1f)] public float progreso;

    public bool Agarrado { get; private set; }
    bool _terminado;
    float _ultimoTic;
    Vector3 _escalaTirador;

    void Awake()
    {
        _escalaTirador = tirador.localScale;
        Aplicar();
    }

    void Update()
    {
        if (_terminado) return;

        bool encima = !Agarrado && Mano.Sobre(tirador);
        if (encima && Mano.PunoEmpezo)
        {
            Agarrado = true;
            Sonidos.Tocar(Sonidos.Agarrar);
            if (HandCursor.Instance) HandCursor.Instance.Pulso();
        }

        if (Agarrado)
        {
            if (!Mano.Puno) Agarrado = false;
            else Arrastrar();
        }

        float escala = Agarrado ? 1.25f : encima ? 1.15f : 1f;
        tirador.localScale = Vector3.Lerp(tirador.localScale, _escalaTirador * escala, 1f - Mathf.Exp(-14f * Time.deltaTime));
        Aplicar();

        if (progreso >= 0.98f)
        {
            progreso = 1f;
            Aplicar();
            _terminado = true;
            Agarrado = false;
            StartCoroutine(Terminar());
        }
    }

    void Arrastrar()
    {
        var cam = Camera.main;
        Vector2 a = cam.WorldToScreenPoint(inicio.position);
        Vector2 b = cam.WorldToScreenPoint(fin.position);
        Vector2 p = Mano.Pos;
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1f));
        Vector2 cercano = a + ab * t;

        if (Vector2.Distance(p, cercano) > distanciaMaxima * Screen.height)
        {
            Agarrado = false;
            Sonidos.Tocar(Sonidos.Soltar);
            return;
        }

        float nuevo = soloAvanza ? Mathf.Max(progreso, t) : t;
        // Sonido de dientes de la cremallera mientras avanza
        if (Mathf.Abs(nuevo - _ultimoTic) > 0.04f)
        {
            _ultimoTic = nuevo;
            Sonidos.Tocar(Sonidos.Tic, 0.8f, Random.Range(0.9f, 1.15f));
        }
        progreso = Mathf.MoveTowards(progreso, nuevo, Time.deltaTime * 3f);
    }

    void Aplicar()
    {
        tirador.position = Vector3.Lerp(inicio.position, fin.position, progreso);
        if (abertura)
        {
            abertura.position = Vector3.Lerp(inicio.position, tirador.position, 0.5f);
            var s = abertura.localScale;
            s.x = Mathf.Max(0.001f, Vector3.Distance(inicio.position, tirador.position));
            abertura.localScale = s;
        }
        if (maleta) maleta.Abrir(progreso);
    }

    IEnumerator Terminar()
    {
        Sonidos.Tocar(Sonidos.Fanfarria);
        yield return new WaitForSeconds(espera);
        Escenas.Cargar(escenaSiguiente);
    }
}
