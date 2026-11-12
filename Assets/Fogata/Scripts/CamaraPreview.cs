using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra dentro del juego la imagen de la cámara que manda el script de Python
/// (JPEG por UDP al puerto 9051, ya en espejo y con los puntos de la mano dibujados).
/// </summary>
public class CamaraPreview : MonoBehaviour
{
    public int puerto = 9051;
    public RawImage imagen;
    [Tooltip("Texto que se muestra mientras no llega imagen")]
    public Text textoSinSenal;

    UdpClient _client;
    Thread _thread;
    volatile bool _running;
    readonly object _lock = new object();
    byte[] _ultimo;
    bool _nuevo;
    Texture2D _tex;
    float _tiempoSinImagen = 99f;

    void OnEnable()
    {
        try
        {
            puerto = NivelesUnificados.PuertoVideoNivel4;   // JUEGO UNIFICADO: el 9051 lo usa la pose del nivel 3
            _client = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
            _running = true;
            _thread = new Thread(Escuchar) { IsBackground = true };
            _thread.Start();
        }
        catch (SocketException e)
        {
            Debug.LogWarning("[Fogata] No se pudo abrir el puerto de video " + puerto + ": " + e.Message);
        }
    }

    void OnDisable()
    {
        _running = false;
        try { _client?.Close(); } catch { }
        try { _thread?.Join(200); } catch { }
    }

    void Escuchar()
    {
        var ep = new IPEndPoint(IPAddress.Any, puerto);
        while (_running)
        {
            try
            {
                var data = _client.Receive(ref ep);
                lock (_lock) { _ultimo = data; _nuevo = true; }
            }
            catch { }
        }
    }

    void Update()
    {
        byte[] datos = null;
        lock (_lock)
        {
            if (_nuevo) { datos = _ultimo; _nuevo = false; }
        }

        if (datos != null && imagen)
        {
            if (!_tex) _tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (_tex.LoadImage(datos))
            {
                imagen.texture = _tex;
                _tiempoSinImagen = 0f;
            }
        }
        else _tiempoSinImagen += Time.deltaTime;

        bool hay = _tiempoSinImagen < 1f;
        if (imagen) imagen.color = hay ? Color.white : new Color(0.15f, 0.15f, 0.2f, 1f);
        if (textoSinSenal)
        {
            textoSinSenal.gameObject.SetActive(!hay);
            if (!hay) textoSinSenal.text = TextoSinSenal();
        }
    }

    string _textoOriginal;

    /// <summary>Si el juego abrió solo el Python, decir que se está encendiendo (y no "abre el script").</summary>
    string TextoSinSenal()
    {
        if (_textoOriginal == null) _textoOriginal = textoSinSenal.text;
        float s = LanzadorCamara.SegundosEncendiendo;
        if (s < 0f) return _textoOriginal;
        if (s < 25f) return "Encendiendo la cámara" + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3) + "\n(tarda unos segundos)";
        return "La cámara no manda imagen.\n¿Está conectada? Prueba otra con -camara 1";
    }

    void OnDestroy()
    {
        if (_tex) Destroy(_tex);
    }
}
