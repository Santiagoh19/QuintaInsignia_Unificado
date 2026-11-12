using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja una mano (abierta o cerrada) que sigue a HandTracker. Crea su propio Canvas en
/// Screen Space Overlay con el Sort Order más alto para quedar siempre encima de todo.
/// Va en el objeto Cursor de la escena Inicio y sobrevive a los cambios de escena.
/// También muestra la vista de la cámara ("Tu mano") en la esquina.
/// </summary>
public class HandCursor : MonoBehaviour
{
    public static HandCursor Instance { get; private set; }

    [Tooltip("Si se dejan vacíos se cargan de Resources/Carpa")]
    public Sprite manoAbierta, manoCerrada;
    [Tooltip("Tamaño de la mano en píxeles con pantalla de 1080 de alto")]
    public float tamano = 170f;
    [Tooltip("Recuadro con la imagen de la cámara arriba a la derecha")]
    public bool mostrarCamara = true;

    // Las dos manos vienen centradas en la palma (ver Herramientas/preparar_guia.py)
    static readonly Vector2 PIVOTE = new Vector2(0.5f, 0.5f);

    Image _img;
    RectTransform _rt;
    float _pulso;

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
        Construir();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Construir()
    {
        if (!manoAbierta) manoAbierta = CargarSprite("Carpa/mano_abierta");
        if (!manoCerrada) manoCerrada = CargarSprite("Carpa/mano_cerrada");

        var canvasGO = new GameObject("CanvasCursor", typeof(RectTransform), typeof(Canvas));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        if (mostrarCamara) CrearVistaCamara(canvasGO.transform);

        var go = new GameObject("Mano", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasGO.transform, false);
        _rt = go.GetComponent<RectTransform>();
        _rt.anchorMin = _rt.anchorMax = Vector2.zero;
        _rt.pivot = PIVOTE;
        _img = go.GetComponent<Image>();
        _img.sprite = manoAbierta;
        _img.raycastTarget = false;
        _img.preserveAspect = true;
    }

    static Sprite CargarSprite(string ruta)
    {
        var tex = Resources.Load<Texture2D>(ruta);
        if (!tex)
        {
            Debug.LogWarning("[Carpa] Falta Resources/" + ruta + ".png");
            return null;
        }
        tex.filterMode = FilterMode.Point; // pixel art: sin difuminar
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), PIVOTE, 100f);
    }

    void CrearVistaCamara(Transform padre)
    {
        var fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var marco = new GameObject("VistaCamara", typeof(RectTransform), typeof(Image));
        marco.transform.SetParent(padre, false);
        var rm = marco.GetComponent<RectTransform>();
        rm.anchorMin = rm.anchorMax = rm.pivot = new Vector2(1f, 1f);
        rm.anchoredPosition = new Vector2(-16f, -16f);
        rm.sizeDelta = new Vector2(232f, 200f);
        var fondo = marco.GetComponent<Image>();
        fondo.color = new Color(0.27f, 0.17f, 0.11f, 0.85f);
        fondo.raycastTarget = false;

        var vista = new GameObject("Imagen", typeof(RectTransform), typeof(RawImage));
        vista.transform.SetParent(marco.transform, false);
        var rv = vista.GetComponent<RectTransform>();
        rv.anchorMin = rv.anchorMax = rv.pivot = new Vector2(0.5f, 0f);
        rv.anchoredPosition = new Vector2(0f, 8f);
        rv.sizeDelta = new Vector2(216f, 162f);
        var raw = vista.GetComponent<RawImage>();
        raw.raycastTarget = false;

        var titulo = Texto("Titulo", marco.transform, fuente, "Tu mano", 20, new Vector2(0f, -4f));
        titulo.rectTransform.anchorMin = titulo.rectTransform.anchorMax = titulo.rectTransform.pivot = new Vector2(0.5f, 1f);
        var sinSenal = Texto("SinSenal", vista.transform, fuente, "Sin cámara", 18, Vector2.zero);

        var preview = marco.AddComponent<CamaraPreview>();
        preview.imagen = raw;
        preview.textoSinSenal = sinSenal;
    }

    static Text Texto(string nombre, Transform padre, Font fuente, string texto, int tam, Vector2 pos)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        var t = go.GetComponent<Text>();
        t.font = fuente;
        t.text = texto;
        t.fontSize = tam;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.95f, 0.85f);
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(216f, 30f);
        t.rectTransform.anchoredPosition = pos;
        return t;
    }

    /// <summary>Pequeño salto del cursor (al pulsar o agarrar algo).</summary>
    public void Pulso() => _pulso = 1f;

    void LateUpdate()
    {
        var t = HandTracker.Instance;
        bool ver = t != null && t.HayCursor && Time.timeScale > 0f;   // JUEGO UNIFICADO: oculta durante la pausa
        _img.enabled = ver;
        if (!ver) return;

        _pulso = Mathf.MoveTowards(_pulso, 0f, Time.unscaledDeltaTime * 5f);
        float lado = tamano * Screen.height / 1080f * (1f + 0.25f * _pulso);
        _rt.sizeDelta = new Vector2(lado, lado);
        _rt.position = t.HandScreenPos;
        _img.sprite = t.IsFist ? manoCerrada : manoAbierta;
        // Con el mouse la mano se ve un poco transparente, para notar que no hay cámara
        _img.color = t.HandDetected ? Color.white : new Color(1f, 1f, 1f, 0.75f);
    }
}
