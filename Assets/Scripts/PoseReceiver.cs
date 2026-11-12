using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Recibe la lectura del cuerpo que envía send_pose_udp.py (MediaPipe Pose + Hands) por UDP.
/// Se crea solo al iniciar el juego y sobrevive entre escenas; no hay que ponerlo en ninguna escena.
/// Los minijuegos no lo usan directamente: lo leen a través de EntradaJugador.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class PoseReceiver : MonoBehaviour
{
    public static PoseReceiver Instancia { get; private set; }

    [Tooltip("Debe coincidir con UDP_PORT en send_pose_udp.py (las manos usan 9050).")]
    public int puerto = 9051;
    [Tooltip("Segundos sin datos para considerar la cámara desconectada.")]
    public float tiempoSinDatos = 1f;
    [Tooltip("Muestra abajo a la izquierda el estado de la cámara.")]
    public bool mostrarEstado = true;

    [Header("Juego exportado")]
    [Tooltip("En el juego exportado abre sola la cámara (StreamingAssets/Camara/QuintaInsigniaCamara.exe) si no hay otra enviando datos.")]
    public bool abrirCamaraAlIniciar = true;
    [Tooltip("Abrir la cámara con su ventana de vista previa. F2 en el juego la reinicia con ventana.")]
    public bool mostrarVentanaCamara = false;

    static Process procesoCamara;
    static string ExeCamara => Path.Combine(Application.streamingAssetsPath, "Camara", "QuintaInsigniaCamara.exe");

    [Serializable]
    class Paquete
    {
        public double ts;
        public bool detected;
        public float x;
        public int lane;
        public bool handsUp;
        public bool rightUp;
        public bool leftUp;
        public bool fist;
        public bool rightFist;
        public bool pauseUp;
        public int handsSeen;
    }

    // Estado actual (se actualiza al inicio de cada frame)
    public bool Conectado => Time.unscaledTime - ultimaRecepcion < tiempoSinDatos;
    public bool Activo => Conectado && Detectado;
    public bool Detectado { get; private set; }
    public int Carril { get; private set; }          // -1 izquierda, 0 centro, 1 derecha
    public float PosicionX { get; private set; }     // 0..1
    public bool ManosArriba { get; private set; }
    public bool ManoDerecha { get; private set; }    // solo la mano derecha levantada
    public bool ManoIzquierda { get; private set; }  // solo la mano izquierda levantada (volver, dentro del menú de pausa)
    public bool Puno { get; private set; }
    public bool ManoDerechaPuno { get; private set; }
    public bool ManosSobreCabeza { get; private set; }  // las dos manos por encima de la cabeza (pausa)   // solo la mano derecha al frente, en puño

    // Estado del frame anterior, para detectar cambios ("pulsaciones")
    int carrilAnterior;
    bool manosAnterior;
    bool derechaAnterior;
    bool izquierdaAnterior;
    bool punoAnterior;

    UdpClient cliente;
    Thread hilo;
    volatile bool corriendo;
    volatile bool hayNuevo;
    readonly object candado = new object();
    Paquete recibido;
    float ultimaRecepcion = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("PoseReceiver");
        go.AddComponent<PoseReceiver>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        StartCoroutine(AbrirCamaraSiHaceFalta());
    }

    // ---- Cámara incluida en el juego exportado ----
    IEnumerator AbrirCamaraSiHaceFalta()
    {
#if UNITY_EDITOR
        yield break;   // en el editor se usa el script de Python de siempre
#else
        yield return new WaitForSecondsRealtime(1.5f);
        if (!abrirCamaraAlIniciar || Conectado) yield break;   // ya hay una cámara enviando datos
        AbrirCamara(mostrarVentanaCamara);
#endif
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    static extern bool DeleteFileW(string ruta);

    /// Quita la marca "descargado de internet" (Zone.Identifier) que Windows pone al bajar el zip,
    /// para que SmartScreen no bloquee la cámara. No hace nada si no hay marca.
    static void QuitarMarcaInternet(string carpeta)
    {
        try
        {
            foreach (var f in Directory.GetFiles(carpeta, "*.exe", SearchOption.TopDirectoryOnly))
                DeleteFileW(f + ":Zone.Identifier");
            foreach (var f in Directory.GetFiles(carpeta, "*.dll", SearchOption.AllDirectories))
                DeleteFileW(f + ":Zone.Identifier");
            foreach (var f in Directory.GetFiles(carpeta, "*.pyd", SearchOption.AllDirectories))
                DeleteFileW(f + ":Zone.Identifier");
        }
        catch (Exception e) { Debug.LogWarning("PoseReceiver: no se pudo desbloquear " + carpeta + " (" + e.Message + ")"); }
    }

    static int codigoSalidaCamara = int.MinValue;

    void AbrirCamara(bool conVentana)
    {
        if (!File.Exists(ExeCamara)) { Debug.Log("PoseReceiver: no hay cámara incluida en " + ExeCamara); return; }
        QuitarMarcaInternet(Path.GetDirectoryName(ExeCamara));
        codigoSalidaCamara = int.MinValue;
        try
        {
            // UseShellExecute = false: se abre directo (CreateProcess), sin pasar por el aviso de SmartScreen.
            procesoCamara = Process.Start(new ProcessStartInfo(ExeCamara, conVentana ? "" : "--sin-ventana")
            {
                WorkingDirectory = Path.GetDirectoryName(ExeCamara),
                UseShellExecute = false,
                CreateNoWindow = !conVentana,
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("PoseReceiver: no se pudo abrir la cámara directo (" + e.Message + "), pruebo con el shell");
            try
            {
                procesoCamara = Process.Start(new ProcessStartInfo(ExeCamara, conVentana ? "" : "--sin-ventana")
                {
                    WorkingDirectory = Path.GetDirectoryName(ExeCamara),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized,
                });
            }
            catch (Exception e2) { Debug.LogWarning("PoseReceiver: no se pudo abrir la cámara (" + e2.Message + ")"); }
        }
    }

    static void CerrarCamara()
    {
        try { if (procesoCamara != null && !procesoCamara.HasExited) procesoCamara.Kill(); } catch { }
        procesoCamara = null;
    }

    void OnApplicationQuit() => CerrarCamara();

    void OnEnable()
    {
        try
        {
            cliente = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
            corriendo = true;
            hilo = new Thread(Escuchar) { IsBackground = true };
            hilo.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning("PoseReceiver: no se pudo abrir el puerto " + puerto + " (" + e.Message + ")");
        }
    }

    void OnDisable()
    {
        corriendo = false;
        try { cliente?.Close(); } catch { }
        try { hilo?.Join(200); } catch { }
    }

    void Escuchar()
    {
        var ep = new IPEndPoint(IPAddress.Any, puerto);
        while (corriendo)
        {
            try
            {
                var datos = cliente.Receive(ref ep);
                var p = JsonUtility.FromJson<Paquete>(Encoding.UTF8.GetString(datos));
                if (p == null) continue;
                lock (candado) recibido = p;
                hayNuevo = true;
            }
            catch
            {
                // paquete inválido o socket cerrado
            }
        }
    }

    void Update()
    {
#if !UNITY_EDITOR
        // F2: reiniciar la cámara con su ventana (para ver la imagen o centrar con la tecla C)
        if (Input.GetKeyDown(KeyCode.F2)) { CerrarCamara(); AbrirCamara(true); }
        // F3: abrir la configuración de privacidad de la cámara de Windows
        if (Input.GetKeyDown(KeyCode.F3)) { try { Application.OpenURL("ms-settings:privacy-webcam"); } catch { } }
        if (procesoCamara != null && codigoSalidaCamara == int.MinValue)
        {
            try { if (procesoCamara.HasExited) codigoSalidaCamara = procesoCamara.ExitCode; } catch { codigoSalidaCamara = -1; }
        }
#endif
        if (hayNuevo)
        {
            Paquete p;
            lock (candado) p = recibido;
            hayNuevo = false;
            ultimaRecepcion = Time.unscaledTime;
            Detectado = p.detected;
            Carril = Mathf.Clamp(p.lane, -1, 1);
            PosicionX = p.x;
            ManosArriba = p.handsUp;
            ManoDerecha = p.rightUp;
            ManoIzquierda = p.leftUp;
            Puno = p.fist;
            ManoDerechaPuno = p.rightFist;
            ManosSobreCabeza = p.pauseUp;
        }

        if (!Conectado)
        {
            Detectado = false;
            Carril = 0;
            ManosArriba = false;
            ManoDerecha = false;
            ManoIzquierda = false;
            Puno = false;
            ManoDerechaPuno = false;
            ManosSobreCabeza = false;
        }
    }

    void LateUpdate()
    {
        carrilAnterior = Carril;
        manosAnterior = ManosArriba;
        derechaAnterior = ManoDerecha;
        izquierdaAnterior = ManoIzquierda;
        punoAnterior = Puno;
    }

    // ---- Eventos de un solo frame ----
    public bool CarrilCambioA(int c) => Activo && Carril == c && carrilAnterior != c;
    public bool ManosArribaAhora => Activo && ManosArriba && !manosAnterior;
    public bool ManoDerechaAhora => Activo && ManoDerecha && !derechaAnterior;
    public bool ManoIzquierdaAhora => Activo && ManoIzquierda && !izquierdaAnterior;
    public bool PunoAhora => Activo && Puno && !punoAnterior;

    // ---- Indicador en pantalla ----
    GUIStyle estilo;

    void OnGUI()
    {
        if (!mostrarEstado) return;
        if (NivelesUnificados.EsNivelExterno(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)) return;   // el nivel 5 tiene su propio texto abajo al centro
        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            estilo.normal.textColor = new Color(0.094f, 0.224f, 0.169f);
            var fondo = new Texture2D(1, 1);
            fondo.SetPixel(0, 0, new Color(1f, 0.988f, 0.965f, 0.9f));
            fondo.Apply();
            estilo.normal.background = fondo;
            estilo.padding = new RectOffset(12, 12, 6, 6);
        }

        string texto;
        if (!Conectado)
            texto = (procesoCamara != null && codigoSalidaCamara == int.MinValue)
                ? "Cámara: encendiendo..."
                : codigoSalidaCamara != int.MinValue
                    ? "Cámara bloqueada o en uso · F3 permisos de cámara · F2 reintentar"
                    : "Cámara: sin conexión (usa el teclado)";
        else if (!Detectado) texto = "Cámara: conectada · ponte frente a la cámara";
        else
        {
            string c = Carril < 0 ? "Izquierda" : Carril > 0 ? "Derecha" : "Centro";
            texto = "Cámara: " + c + (ManosArriba ? " · Manos al frente" : "") + (ManoDerecha ? " · Mano derecha" : "") + (ManoIzquierda ? " · Mano izquierda" : "") + (Puno ? " · Puños" : "");
        }
        GUI.Label(new Rect((Screen.width - 660) / 2f, Screen.height - 44, 660, 32), texto, estilo);   // abajo al centro (abajo a la izquierda van las insignias)
    }
}
