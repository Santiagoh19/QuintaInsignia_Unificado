using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

// La ventanita con la camara del jugador, para que se vea las manos mientras juega.
//
// Unity NO abre la webcam: la tiene el emisor de Python (MediaPipe) y en este portatil
// la camara es de un solo programa a la vez; si Unity la pidiera, la veria negra.
// Por eso Python manda cada cuadro como JPEG al puerto 9051 (send_fingers_udp.py, VIDEO_PORT)
// y este script lo pinta en una RawImage. Es el mismo recorte 16:9 que procesa MediaPipe,
// asi que lo que ves coincide con lo que el juego lee de tus dedos.
//
// La ventanita la pone SombrasInterfaz al construir la interfaz.
public class CamaraJugador : MonoBehaviour
{
    [Header("UDP")]
    [Tooltip("Debe coincidir con VIDEO_PORT del emisor Python")]
    public int puerto = 9051;
    public const int PuertoUnificado = 9053;

    [Header("Donde se ve")]
    public RawImage pantalla;
    [Tooltip("Lo que se muestra cuando no llega imagen (el emisor de Python esta apagado)")]
    public GameObject avisoSinCamara;

    [Tooltip("Como un espejo, igual que los dedos (FingersMove.mirrorX)")]
    public bool espejo = true;

    [Tooltip("Segundos sin imagen para mostrar el aviso")]
    public float esperaAviso = 1f;

    UdpClient cliente;
    Thread hilo;
    volatile bool escuchando;

    // el ultimo JPEG que llego; el hilo lo deja aqui y Update lo pinta
    byte[] ultimoJpeg;
    readonly object candado = new object();

    Texture2D textura;
    float ultimaImagen = -100f;


    void OnEnable()
    {
        escuchando = true;
        // JUEGO UNIFICADO: el 9051 lo usa PoseReceiver (nivel 3); el emisor unificado manda este video al 9053
        puerto = PuertoUnificado;
        cliente = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
        hilo = new Thread(Escuchar) { IsBackground = true };
        hilo.Start();
    }

    void OnDisable()
    {
        escuchando = false;
        try { cliente?.Close(); } catch { }
        try { hilo?.Join(200); } catch { }
    }

    void OnDestroy()
    {
        if (textura != null)
        {
            Destroy(textura);
        }
    }


    void Update()
    {
        byte[] jpeg = null;
        lock (candado)
        {
            jpeg = ultimoJpeg;
            ultimoJpeg = null;
        }

        // la textura se pinta aqui y no en el hilo: Unity solo deja tocar texturas desde el hilo principal
        if (jpeg != null && pantalla != null)
        {
            if (textura == null)
            {
                textura = new Texture2D(2, 2, TextureFormat.RGB24, false);
            }

            // LoadImage acomoda solo el tamano de la textura al del JPEG
            if (textura.LoadImage(jpeg))
            {
                pantalla.texture = textura;
                ultimaImagen = Time.time;
            }
        }

        if (pantalla != null)
        {
            // uvRect con ancho -1 voltea la imagen de izquierda a derecha
            pantalla.uvRect = espejo ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
        }

        bool hayImagen = Time.time - ultimaImagen < esperaAviso;
        if (pantalla != null)
        {
            pantalla.enabled = hayImagen;
        }
        if (avisoSinCamara != null)
        {
            avisoSinCamara.SetActive(!hayImagen);
        }
    }


    // corre en otro hilo: Receive se queda esperando hasta que llega un paquete
    void Escuchar()
    {
        IPEndPoint origen = new IPEndPoint(IPAddress.Any, puerto);
        while (escuchando)
        {
            try
            {
                byte[] datos = cliente.Receive(ref origen);
                lock (candado)
                {
                    // si llegan dos antes de que Update pinte, el viejo se descarta: solo importa el mas nuevo
                    ultimoJpeg = datos;
                }
            }
            catch
            {
                // al cerrar el cliente Receive lanza un error: es la forma normal de salir
            }
        }
    }
}
