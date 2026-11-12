using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de inicio: anima a Lucas y al botón, y carga la escena del juego al pulsar "Comenzar".
/// Se comienza levantando la mano derecha (MediaPipe), con Enter / Espacio o con un clic.
/// Encima del botón aparece el aviso "Levanta la mano derecha para comenzar".
/// </summary>
public class PantallaInicio : MonoBehaviour
{
    [Header("Navegación")]
    [Tooltip("Nombre de la escena que se carga al pulsar Comenzar (debe estar en Build Settings).")]
    public string escenaJuego = "SampleScene";
    public Button botonComenzar;

    [Header("Animación")]
    public RectTransform lucas;
    public RectTransform boton;
    public float amplitudLucas = 12f;
    public float velocidadLucas = 1.6f;
    public float pulsoBoton = 0.03f;

    [Header("Aviso del gesto")]
    public string textoAviso = "Levanta la mano derecha para continuar";

    Vector2 posicionLucas;
    bool cargando;
    TextMeshProUGUI aviso;

    void Start()
    {
        if (lucas != null) posicionLucas = lucas.anchoredPosition;
        if (botonComenzar != null) botonComenzar.onClick.AddListener(Comenzar);
        CrearAviso();
    }

    /// <summary>Texto pequeño encima del botón, con la misma fuente del párrafo.</summary>
    void CrearAviso()
    {
        if (boton == null) return;
        var go = new GameObject("AvisoManoDerecha", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(boton.parent, false);
        rt.anchorMin = rt.anchorMax = boton.anchorMin;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(800f, 40f);
        rt.anchoredPosition = boton.anchoredPosition + new Vector2(0f, boton.rect.height * 0.5f + 4f);
        aviso = go.GetComponent<TextMeshProUGUI>();
        var parrafo = boton.parent.Find("Descripcion")?.GetComponent<TextMeshProUGUI>();
        if (parrafo != null) aviso.font = parrafo.font;
        aviso.text = textoAviso;
        aviso.fontSize = 28;
        aviso.fontStyle = FontStyles.Bold;
        aviso.color = new Color32(0xF4, 0xC9, 0x5D, 255);
        aviso.alignment = TextAlignmentOptions.Center;
        aviso.textWrappingMode = TextWrappingModes.NoWrap;
        aviso.raycastTarget = false;
    }

    void Update()
    {
        if (lucas != null)
            lucas.anchoredPosition = posicionLucas + Vector2.up * Mathf.Sin(Time.time * velocidadLucas) * amplitudLucas;

        if (boton != null)
        {
            float s = 1f + Mathf.Sin(Time.time * 2.2f) * pulsoBoton;
            boton.localScale = new Vector3(s, s, 1f);
        }

        if (aviso != null)
        {
            // late suavemente para llamar la atención; se ilumina si la cámara ve la mano derecha arriba
            bool mano = PoseReceiver.Instancia != null && PoseReceiver.Instancia.Activo && PoseReceiver.Instancia.ManoDerecha;
            var c = aviso.color; c.a = mano ? 1f : 0.75f + Mathf.Sin(Time.time * 3f) * 0.25f;
            aviso.color = c;
        }

        // Enter / Espacio o levantar la mano derecha (EntradaJugador.Accion)
        if (!cargando && EntradaJugador.Accion) Comenzar();
    }

    /// <summary>"Comenzar" abre la pantalla de dificultad; al confirmar, empieza la aventura.</summary>
    public void Comenzar()
    {
        if (cargando) return;
        cargando = true;
        Sonido.Efecto("Boton");
        var canvas = boton != null ? boton.GetComponentInParent<Canvas>().rootCanvas : FindFirstObjectByType<Canvas>();
        SeleccionDificultad.Mostrar((RectTransform)canvas.transform, d =>
        {
            EstadoJuego.Reiniciar();
            EstadoJuego.Dificultad = d;
            Transicion.Cargar(escenaJuego);
        });
    }
}
