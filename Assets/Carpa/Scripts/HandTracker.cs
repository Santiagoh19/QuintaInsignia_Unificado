using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Recibe la mano que manda Python (send_mano_puno_udp.py) por UDP al puerto 5052 con el formato
/// "x;y;puño" (x, y de 0 a 1 con y=0 arriba; puño 1 = cerrada, 0 = abierta) en un hilo aparte.
/// Va en el objeto HandManager de la escena Inicio y sobrevive a los cambios de escena.
/// Si no hay mano, se puede jugar con el mouse (clic sostenido = puño).
/// </summary>
[DefaultExecutionOrder(-100)]
public class HandTracker : MonoBehaviour
{
    public static HandTracker Instance { get; private set; }

    [Header("Conexión")]
    public int puerto = 5052;
    [Tooltip("Segundos sin paquetes para considerar que no hay mano")]
    public float tiempoSinMano = 0.3f;

    [Header("Cursor")]
    [Tooltip("Menor = cursor más suave (ayuda con el temblor). 0 = sin suavizar")]
    public float seguimiento = 14f;
    [Tooltip("Parte de la cámara que se ignora en cada borde (mayor = menos hay que estirar el brazo)")]
    [Range(0f, 0.3f)] public float margenCamara = 0.12f;
    [Tooltip("Invierte el movimiento horizontal (tecla M)")]
    public bool invertirX;

    [Header("Puño")]
    [Tooltip("Segundos que el puño debe mantenerse igual antes de cambiar (evita parpadeos)")]
    public float tiempoConfirmar = 0.05f;
    [Tooltip("Sin mano en la cámara se usa el mouse (clic izquierdo sostenido = puño)")]
    public bool usarMouseSinMano = true;

    /// <summary>Posición de la mano en píxeles de pantalla (0,0 abajo a la izquierda).</summary>
    public Vector2 HandScreenPos { get; private set; }
    /// <summary>La mano está cerrada.</summary>
    public bool IsFist { get; private set; }
    /// <summary>Hay una mano real en la cámara.</summary>
    public bool HandDetected { get; private set; }
    /// <summary>Se está usando el mouse en lugar de la mano.</summary>
    public bool UsandoMouse { get; private set; }
    /// <summary>Hay algo que mueve el cursor (mano o mouse).</summary>
    public bool HayCursor => HandDetected || UsandoMouse;
    /// <summary>En este frame la mano pasó de abierta a cerrada (agarrar / pulsar).</summary>
    public bool FistDown { get; private set; }
    /// <summary>En este frame la mano pasó de cerrada a abierta (soltar).</summary>
    public bool FistUp { get; private set; }

    UdpClient _client;
    Thread _thread;
    volatile bool _running;
    readonly object _lock = new object();
    float _x, _y;
    bool _punoCrudo;
    int _paquetes;

    int _ultimoPaquete;
    float _sinPaquete = 99f;
    float _cambioPuno;
    bool _primeraVez = true;

    /// <summary>Crea HandManager y Cursor si la escena se abrió sin pasar por Inicio.</summary>
    public static void Asegurar()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[Carpa] No hay HandManager (la escena no se abrió desde Inicio). Se crea uno.");
            new GameObject("HandManager").AddComponent<HandTracker>();
        }
        if (HandCursor.Instance == null)
            new GameObject("Cursor").AddComponent<HandCursor>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += SalirDelNivel;
    }

    // JUEGO UNIFICADO: al pasar a una escena que no es de la carpa (siguiente nivel) se quitan
    // la mano, el cursor y su vista de cámara, y se libera el puerto.
    void SalirDelNivel(Scene s, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single || s.path.Contains("/Carpa/")) return;
        if (HandCursor.Instance) Destroy(HandCursor.Instance.gameObject);
        Destroy(gameObject);
    }

    void Start()
    {
        if (Instance != this) return;
        try
        {
            _client = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
            _running = true;
            _thread = new Thread(Escuchar) { IsBackground = true };
            _thread.Start();
        }
        catch (SocketException e)
        {
            Debug.LogWarning("[Carpa] No se pudo abrir el puerto " + puerto + ": " + e.Message);
        }
    }

    void Escuchar()
    {
        var ep = new IPEndPoint(IPAddress.Any, puerto);
        while (_running)
        {
            try
            {
                var texto = Encoding.UTF8.GetString(_client.Receive(ref ep));
                var partes = texto.Trim().Split(';');
                if (partes.Length < 3) continue;
                if (!float.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) continue;
                if (!float.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) continue;
                bool puno = partes[2].Trim() == "1";
                lock (_lock)
                {
                    _x = x; _y = y; _punoCrudo = puno;
                    _paquetes++;
                }
            }
            catch { }
        }
    }

    void Update()
    {
        if (Instance != this) return;
        // JUEGO UNIFICADO: con el menú de pausa general (Time.timeScale = 0) la mano no agarra ni suelta nada
        if (Time.timeScale == 0f) { FistDown = FistUp = false; return; }
        float dt = Time.unscaledDeltaTime;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.M)) invertirX = !invertirX;
#endif

        float x, y; bool punoCrudo; int paquetes;
        lock (_lock) { x = _x; y = _y; punoCrudo = _punoCrudo; paquetes = _paquetes; }

        if (paquetes != _ultimoPaquete) { _ultimoPaquete = paquetes; _sinPaquete = 0f; }
        else _sinPaquete += dt;

        bool antes = IsFist;
        HandDetected = paquetes > 0 && _sinPaquete < tiempoSinMano;
        UsandoMouse = false;

        if (HandDetected)
        {
            float u = Mathf.InverseLerp(margenCamara, 1f - margenCamara, x);
            float v = 1f - Mathf.InverseLerp(margenCamara, 1f - margenCamara, y);
            if (invertirX) u = 1f - u;
            var destino = new Vector2(u * Screen.width, v * Screen.height);
            HandScreenPos = (_primeraVez || seguimiento <= 0f)
                ? destino
                : Vector2.Lerp(HandScreenPos, destino, 1f - Mathf.Exp(-seguimiento * dt));
            _primeraVez = false;

            // El puño solo cambia si se mantiene igual un momento
            if (punoCrudo != IsFist)
            {
                _cambioPuno += dt;
                if (_cambioPuno >= tiempoConfirmar) { IsFist = punoCrudo; _cambioPuno = 0f; }
            }
            else _cambioPuno = 0f;
        }
        else
        {
            _primeraVez = true;
            _cambioPuno = 0f;
            IsFist = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (usarMouseSinMano && Input.mousePresent)
            {
                UsandoMouse = true;
                HandScreenPos = Input.mousePosition;
                IsFist = Input.GetMouseButton(0);
            }
#endif
        }

        FistDown = IsFist && !antes;
        FistUp = !IsFist && antes;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= SalirDelNivel;
        if (Instance == this) Instance = null;
        _running = false;
        try { _client?.Close(); } catch { }
        try { _thread?.Join(200); } catch { }
    }
}
