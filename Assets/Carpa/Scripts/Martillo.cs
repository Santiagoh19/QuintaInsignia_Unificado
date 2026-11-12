using UnityEngine;

/// <summary>
/// Martillo de la escena Estacas. Se agarra con el puño y, con el puño cerrado, se sube y se baja
/// la mano sobre una estaca. Un golpe cuenta cuando la mano baja con velocidad suficiente (medida en
/// la posición vertical de la pantalla) y, al frenar, el martillo queda sobre una estaca sin terminar.
/// El golpe se decide al final de la bajada (no al empezar), porque al subir la mano sobre una estaca
/// de adelante el martillo pasa por encima de la de atrás.
/// Para el siguiente golpe hay que volver a subir la mano. Si se abre la mano, vuelve a su sitio.
/// </summary>
public class Martillo : MonoBehaviour
{
    public Estaca[] estacas;
    [Tooltip("Velocidad de bajada para contar un golpe (altos de pantalla por segundo). Menor = más fácil")]
    public float velocidadGolpe = 0.7f;
    [Tooltip("Velocidad de subida para preparar el siguiente golpe")]
    public float velocidadRearmar = 0.25f;
    [Tooltip("Qué tan cerca de la estaca (en pantalla, fracción del alto) tiene que estar la mano")]
    public float distanciaEstaca = 0.11f;
    public float levantar = 1.2f;
    [Tooltip("Parte que se inclina al golpear (hijo). Opcional")]
    public Transform modelo;
    public float anguloGolpe = 55f;

    public bool Agarrado { get; private set; }

    Vector3 _posInicial;
    Quaternion _rotInicial, _rotModelo;
    Vector3 _escalaInicial;
    Plane _plano;
    float _yAnterior, _vy;
    bool _armado = true;
    bool _bajando;
    float _golpeVisual;
    Estaca _objetivo;
    Collider[] _colliders;

    void Awake()
    {
        _posInicial = transform.position;
        _rotInicial = transform.rotation;
        _escalaInicial = transform.localScale;
        if (modelo) _rotModelo = modelo.localRotation;
        _colliders = GetComponentsInChildren<Collider>();
    }

    bool Terminado => Tareas.Instancia ? Tareas.Instancia.Completo : TodasClavadas();

    bool TodasClavadas()
    {
        foreach (var e in estacas) if (e && !e.Clavada) return false;
        return true;
    }

    void Update()
    {
        if (Agarrado && (!Mano.Puno || Terminado)) Soltar();

        if (!Agarrado)
        {
            if (!Terminado && Agarrable.Actual == null && Mano.PunoEmpezo && Mano.Sobre(transform)) Agarrar();
            else
            {
                float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, _posInicial, k);
                transform.rotation = Quaternion.Slerp(transform.rotation, _rotInicial, k);
                transform.localScale = Vector3.Lerp(transform.localScale, _escalaInicial, k);
            }
        }
        else Golpear();

        _golpeVisual = Mathf.MoveTowards(_golpeVisual, 0f, Time.deltaTime * 5f);
        if (modelo) modelo.localRotation = _rotModelo * Quaternion.Euler(0f, 0f, -anguloGolpe * Mathf.Sin(_golpeVisual * Mathf.PI));
    }

    void Agarrar()
    {
        Agarrado = true;
        _armado = true;
        _bajando = false;
        _vy = 0f;
        _yAnterior = Mano.Pos.y / Screen.height;
        _plano = Mano.PlanoCamara(transform.position);
        foreach (var c in _colliders) c.enabled = false;
        transform.localScale = _escalaInicial * 1.1f;
        Sonidos.Tocar(Sonidos.Agarrar);
        if (HandCursor.Instance) HandCursor.Instance.Pulso();
    }

    void Soltar()
    {
        Agarrado = false;
        foreach (var c in _colliders) c.enabled = true;
        if (_objetivo) _objetivo.Resaltar(false);
        _objetivo = null;
        Sonidos.Tocar(Sonidos.Soltar);
    }

    void Golpear()
    {
        // Seguir la mano sobre el plano paralelo a la cámara, un poco levantado
        if (Mano.PuntoEnPlano(_plano, out var p))
        {
            var cam = Camera.main;
            Vector3 destino = p + (cam.transform.position - p).normalized * levantar;
            transform.position = Vector3.Lerp(transform.position, destino, 1f - Mathf.Exp(-30f * Time.deltaTime));
        }

        // Velocidad vertical de la mano en pantalla (negativa = bajando)
        float y = Mano.Pos.y / Screen.height;
        float dt = Mathf.Max(Time.deltaTime, 0.001f);
        _vy = Mathf.Lerp(_vy, (y - _yAnterior) / dt, 0.5f);
        _yAnterior = y;

        // Estaca sin terminar más cercana a la mano
        Estaca mejor = null;
        float dMin = distanciaEstaca;
        foreach (var e in estacas)
        {
            if (!e || e.Clavada) continue;
            float d = Mano.DistanciaPantalla(e.Punta);
            if (d < dMin) { dMin = d; mejor = e; }
        }
        if (mejor != _objetivo)
        {
            if (_objetivo) _objetivo.Resaltar(false);
            _objetivo = mejor;
            if (_objetivo) _objetivo.Resaltar(true);
        }

        if (_armado && _vy < -velocidadGolpe)
        {
            // Bajada rápida: el golpe cae cuando la mano frene
            _bajando = true;
            _armado = false;
        }
        else if (_bajando && _vy > -velocidadGolpe * 0.35f)
        {
            _bajando = false;
            _golpeVisual = 1f;
            if (_objetivo && _objetivo.Golpear() && HandCursor.Instance) HandCursor.Instance.Pulso();
        }
        else if (!_armado && !_bajando && _vy > velocidadRearmar)
        {
            _armado = true;
        }
    }
}
