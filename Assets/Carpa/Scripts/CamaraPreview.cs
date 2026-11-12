using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra dentro del juego la imagen de la cámara que manda el script de Python
/// (send_mano_puno_udp.py: JPEG por UDP al puerto 9051, ya en espejo y con los puntos de la mano dibujados).
/// </summary>
public class CamaraPreview : MonoBehaviour
{
    public int puerto = 9056;   // JUEGO UNIFICADO: el 9051 es del nivel 3 (pose); el emisor unificado manda aquí este video
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
            _client = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
            _running = true;
            _thread = new Thread(Escuchar) { IsBackground = true };
            _thread.Start();
        }
        catch (SocketException e)
        {
            Debug.LogWarning("[Carpa] No se pudo abrir el puerto de video " + puerto + ": " + e.Message);
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
        else _tiempoSinImagen += Time.unscaledDeltaTime;

        bool hay = _tiempoSinImagen < 1f;
        if (imagen) imagen.color = hay ? Color.white : new Color(0.15f, 0.15f, 0.2f, 1f);
        if (textoSinSenal) textoSinSenal.gameObject.SetActive(!hay);
    }

    void OnDestroy()
    {
        if (_tex) Destroy(_tex);
    }
}
