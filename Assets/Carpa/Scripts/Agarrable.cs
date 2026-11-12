using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Objeto 3D que se agarra con el puño y se suelta abriendo la mano.
/// Mientras está agarrado se arrastra sobre un plano paralelo a la cámara (a la profundidad del objeto)
/// y se levanta un poco hacia la cámara. Al soltarlo busca una zona (IZonaSoltar) con un Raycast
/// desde la mano y, si no la hay, con un OverlapSphere. Si ninguna lo acepta, vuelve a su lugar.
/// </summary>
public class Agarrable : MonoBehaviour
{
    /// <summary>El objeto que la mano tiene agarrado ahora (solo uno a la vez).</summary>
    public static Agarrable Actual { get; private set; }

    [Tooltip("Número o color del objeto, para zonas que solo aceptan uno (los tubos de Ubicar). -1 = ninguno")]
    public int id = -1;
    [Tooltip("Cuánto se acerca a la cámara mientras está en la mano")]
    public float levantar = 0.8f;
    public float escalaAgarrado = 1.12f;
    [Tooltip("Radio del OverlapSphere que busca zonas al soltar si el rayo no encontró ninguna")]
    public float radioBusqueda = 0.6f;
    public bool volverSiFalla = true;
    public float velocidadVolver = 7f;

    public UnityEvent alAgarrar, alSoltarFuera;

    public bool Agarrado => Actual == this;
    /// <summary>Ya quedó colocado en una zona: no se puede volver a agarrar.</summary>
    public bool Fijo { get; private set; }
    public Vector3 PosicionInicial { get; private set; }

    Quaternion _rotInicial;
    Vector3 _escalaInicial;
    Plane _plano;
    Vector3 _desfase;
    Vector3 _puntoPlano;
    bool _volviendo;
    Collider[] _colliders;
    static readonly Collider[] _cercanos = new Collider[16];

    void Awake()
    {
        PosicionInicial = transform.position;
        _rotInicial = transform.rotation;
        _escalaInicial = transform.localScale;
        _colliders = GetComponentsInChildren<Collider>();
    }

    void Update()
    {
        if (Fijo) return;

        if (Agarrado)
        {
            if (!Mano.Puno) Soltar();
            else Mover(false);
            return;
        }

        if (Actual == null && Mano.PunoEmpezo && Mano.ObjetoBajo<Agarrable>() == this)
        {
            Agarrar();
            return;
        }

        if (_volviendo)
        {
            float k = 1f - Mathf.Exp(-velocidadVolver * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, PosicionInicial, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, _rotInicial, k);
            transform.localScale = Vector3.Lerp(transform.localScale, _escalaInicial, k);
            if ((transform.position - PosicionInicial).sqrMagnitude < 0.0004f)
            {
                transform.SetPositionAndRotation(PosicionInicial, _rotInicial);
                _volviendo = false;
            }
        }
    }

    void Agarrar()
    {
        Actual = this;
        _volviendo = false;
        _plano = Mano.PlanoCamara(transform.position);
        _desfase = Mano.PuntoEnPlano(_plano, out var p) ? transform.position - p : Vector3.zero;
        // Sin colliders mientras está en la mano, para que el rayo vea lo que hay detrás (la caneca, los puntos)
        foreach (var c in _colliders) c.enabled = false;
        transform.localScale = _escalaInicial * escalaAgarrado;
        Sonidos.Tocar(Sonidos.Agarrar);
        if (HandCursor.Instance) HandCursor.Instance.Pulso();
        alAgarrar.Invoke();
        Mover(true);
    }

    void Mover(bool inmediato)
    {
        if (!Mano.PuntoEnPlano(_plano, out var p)) return;
        _puntoPlano = p + _desfase;
        var cam = Camera.main;
        Vector3 destino = _puntoPlano + (cam.transform.position - _puntoPlano).normalized * levantar;
        transform.position = inmediato ? destino : Vector3.Lerp(transform.position, destino, 1f - Mathf.Exp(-25f * Time.deltaTime));
    }

    void Soltar()
    {
        Actual = null;
        foreach (var c in _colliders) c.enabled = true;

        var zona = BuscarZona();
        if (zona != null && zona.Aceptar(this)) return;

        Sonidos.Tocar(Sonidos.Soltar);
        transform.localScale = _escalaInicial;
        alSoltarFuera.Invoke();
        if (volverSiFalla) _volviendo = true;
    }

    IZonaSoltar BuscarZona()
    {
        var zona = Mano.ZonaBajo(transform);
        if (zona != null) return zona;

        int n = Physics.OverlapSphereNonAlloc(_puntoPlano, radioBusqueda, _cercanos, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        float dMin = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (_cercanos[i].transform.IsChildOf(transform)) continue;
            var z = _cercanos[i].GetComponentInParent<IZonaSoltar>();
            float d = (_cercanos[i].ClosestPoint(_puntoPlano) - _puntoPlano).sqrMagnitude;
            if (z != null && d < dMin) { zona = z; dMin = d; }
        }
        return zona;
    }

    /// <summary>Lo suelta sin buscar zona (por ejemplo al cambiar de escena).</summary>
    public void Cancelar()
    {
        if (Actual == this) Actual = null;
        foreach (var c in _colliders) if (c) c.enabled = true;
    }

    /// <summary>Lo lleva a un lugar y lo deja fijo ahí.</summary>
    public void FijarEn(Vector3 posicion, Quaternion rotacion, float duracion = 0.25f)
    {
        Fijo = true;
        _volviendo = false;
        foreach (var c in _colliders) c.enabled = false;
        StartCoroutine(Llevar(posicion, rotacion, duracion));
    }

    IEnumerator Llevar(Vector3 pos, Quaternion rot, float dur)
    {
        Vector3 p0 = transform.position, e0 = transform.localScale;
        Quaternion r0 = transform.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(dur, 0.01f))
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(Vector3.Lerp(p0, pos, s), Quaternion.Slerp(r0, rot, s));
            transform.localScale = Vector3.Lerp(e0, _escalaInicial, s);
            yield return null;
        }
        transform.SetPositionAndRotation(pos, rot);
        transform.localScale = _escalaInicial;
    }

    /// <summary>Se encoge hacia un punto y desaparece (al botarlo en la caneca).</summary>
    public void Desaparecer(Vector3 hacia, float duracion = 0.35f)
    {
        Fijo = true;
        foreach (var c in _colliders) c.enabled = false;
        StartCoroutine(Encoger(hacia, duracion));
    }

    IEnumerator Encoger(Vector3 hacia, float dur)
    {
        Vector3 p0 = transform.position, e0 = transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
        {
            transform.position = Vector3.Lerp(p0, hacia, t * t);
            transform.localScale = e0 * (1f - t);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (Actual == this) Actual = null;
    }
}
