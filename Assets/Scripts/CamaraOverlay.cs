using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Recuadro con la imagen de la cámara abajo a la derecha, en todas las escenas.
/// La imagen la manda send_pose_udp.py (JPEG pequeño por UDP, puerto 9052): así la cámara
/// la sigue usando solo MediaPipe y Unity no tiene que abrirla.
/// Marco delgado con los colores del juego (verde #4F8A5B + amarillo #F4C95D).
/// Tecla V: mostrar / ocultar el recuadro. Se crea solo y vive entre escenas.
/// </summary>
public class CamaraOverlay : MonoBehaviour
{
    public static CamaraOverlay Instancia { get; private set; }

    [Tooltip("Debe coincidir con --video-puerto de send_pose_udp.py")]
    public int puerto = 9052;
    [Tooltip("Ancho del recuadro (en la resolución de referencia 1920x1080)")]
    public float ancho = 300f;
    public Vector2 margen = new Vector2(24f, 24f);
    public bool visible = true;
    [Tooltip("Segundos sin imágenes para mostrar 'Cámara sin señal'")]
    public float tiempoSinSenal = 1.5f;

    static readonly Color Verde = new Color32(0x4F, 0x8A, 0x5B, 255);
    static readonly Color Amarillo = new Color32(0xF4, 0xC9, 0x5D, 255);
    static readonly Color AzulNoche = new Color32(0x10, 0x1F, 0x3D, 255);
    static readonly Color TextoClaro = new Color32(0xE6, 0xEC, 0xF5, 255);

    Canvas canvas;
    RectTransform marco;
    RawImage imagen;
    TextMeshProUGUI aviso;
    Texture2D textura;

    UdpClient cliente;
    Thread hilo;
    volatile bool corriendo;
    readonly object candado = new object();
    byte[] ultimoJpg;
    float ultimaImagen = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("CamaraOverlay");
        go.AddComponent<CamaraOverlay>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        Construir();
        SceneManager.sceneLoaded += AlCargar;
        Escuchar();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AlCargar;
        corriendo = false;
        try { cliente?.Close(); } catch { }
        if (textura != null) Destroy(textura);
    }

    // ------------------------------------------------------------------ Red

    void Escuchar()
    {
        try
        {
            cliente = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
            cliente.Client.ReceiveBufferSize = 1 << 20;
        }
        catch (Exception e)
        {
            Debug.LogWarning("CamaraOverlay: no se pudo abrir el puerto " + puerto + " (" + e.Message + ")");
            return;
        }
        corriendo = true;
        hilo = new Thread(() =>
        {
            var desde = new IPEndPoint(IPAddress.Any, 0);
            while (corriendo)
            {
                try
                {
                    byte[] datos = cliente.Receive(ref desde);
                    lock (candado) ultimoJpg = datos;     // solo importa la imagen más reciente
                }
                catch (SocketException) { if (!corriendo) break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { }
            }
        }) { IsBackground = true, Name = "CamaraOverlay" };
        hilo.Start();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.V)) visible = !visible;
        // en el nivel 5 (Sombras) la escena trae su propia ventanita de la cámara
        canvas.enabled = visible && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Sombras" && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != NivelesUnificados.EscenaHistoria && !NivelesUnificados.EsNivelExterno(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        byte[] jpg = null;
        lock (candado) { jpg = ultimoJpg; ultimoJpg = null; }
        if (jpg != null && textura.LoadImage(jpg, false))
        {
            ultimaImagen = Time.unscaledTime;
            imagen.texture = textura;
        }

        bool senal = Time.unscaledTime - ultimaImagen < tiempoSinSenal;
        imagen.enabled = senal;
        aviso.enabled = !senal;
    }

    // ------------------------------------------------------------------ Interfaz

    void AlCargar(Scene s, LoadSceneMode m) => BuscarFuente();

    void BuscarFuente()
    {
        if (aviso == null) return;
        foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == aviso || t.font == null) continue;
            if (t.font.name.ToLower().Contains("nunito")) { aviso.font = t.font; return; }
        }
    }

    void Construir()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;                      // encima del juego, debajo de la pantalla de carga (5000)
        var escala = gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.matchWidthOrHeight = 0.5f;

        float alto = ancho * 3f / 4f;                   // la cámara manda 4:3

        // marco: verde por fuera (2 px) y amarillo por dentro (3 px)
        var exterior = Hijo<Image>("MarcoCamara", transform);
        exterior.color = Verde;
        exterior.raycastTarget = false;
        marco = exterior.rectTransform;
        marco.anchorMin = marco.anchorMax = marco.pivot = new Vector2(1f, 0f);
        marco.sizeDelta = new Vector2(ancho + 10f, alto + 10f);
        marco.anchoredPosition = new Vector2(-margen.x, margen.y);

        var interior = Hijo<Image>("Amarillo", marco);
        interior.color = Amarillo;
        interior.raycastTarget = false;
        Estirar(interior.rectTransform, 2f);

        var fondo = Hijo<Image>("Fondo", marco);
        fondo.color = AzulNoche;
        fondo.raycastTarget = false;
        Estirar(fondo.rectTransform, 5f);

        imagen = Hijo<RawImage>("Camara", marco);
        imagen.raycastTarget = false;
        imagen.enabled = false;
        Estirar(imagen.rectTransform, 5f);

        aviso = Hijo<TextMeshProUGUI>("SinSenal", marco);
        aviso.text = "Cámara sin señal";
        aviso.fontSize = 24;
        aviso.color = TextoClaro;
        aviso.alignment = TextAlignmentOptions.Center;
        aviso.raycastTarget = false;
        Estirar(aviso.rectTransform, 12f);

        textura = new Texture2D(2, 2, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
    }

    static T Hijo<T>(string nombre, Transform padre) where T : Component
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.transform.SetParent(padre, false);
        return go.GetComponent<T>();
    }

    static void Estirar(RectTransform r, float borde)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(borde, borde);
        r.offsetMax = new Vector2(-borde, -borde);
    }
}
