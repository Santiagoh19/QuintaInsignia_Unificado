using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Noche: oscurece el mundo y deja ver lo que ilumina la linterna que Lucas lleva en el cinturón.
/// Se crea sola y actúa en las escenas de juego: busca el objeto "Lucas", le cuelga la linterna
/// (Resources/Linterna/LinternaCinturon.png) y pone la capa de oscuridad encima del mundo
/// (la interfaz queda siempre visible). El haz sale de la lente hacia adelante.
/// También voltea a Lucas cuando camina hacia la izquierda.
/// </summary>
public class Linterna : MonoBehaviour
{
    public static Linterna Instancia { get; private set; }

    /// <summary>Si está activo, el haz apunta hacia él (la pesca lo usa con la boya).</summary>
    public static RectTransform Objetivo;

    static readonly string[] Escenas = { "SampleScene", "CaminoLena", "Lena", "Pesca" };

    [Header("Oscuridad")]
    public Color oscuridad = new Color(0.02f, 0.03f, 0.09f, 0.62f);
    [Tooltip("Tinte cálido dentro del haz (alfa = intensidad)")]
    public Color luzCalida = new Color(1f, 0.85f, 0.5f, 0.16f);

    [Header("Haz de la linterna")]
    [Tooltip("Largo del haz en alturas de pantalla")]
    public float largoHaz = 0.5f;
    [Tooltip("Apertura del haz (0.3 estrecho – 0.7 ancho)")]
    public float aperturaHaz = 0.5f;
    [Tooltip("Radio del brillo en la lente, en alturas de pantalla")]
    public float radioHalo = 0.05f;

    [Header("Resplandor sobre Lucas")]
    [Tooltip("Radio en alturas de Lucas")]
    public float radioCuerpo = 0.6f;
    [Range(0f, 1f)] public float brilloCuerpo = 0.35f;

    [Tooltip("1 = linterna encendida; 0 = apagada (se usa para desvanecerla al final)")]
    [Range(0f, 1f)] public float intensidad = 1f;

    [Header("Linterna en el cinturón (fracciones del tamaño de Lucas)")]
    public Vector2 posicionCinturon = new Vector2(0.15f, -0.135f);
    public float anchoLinterna = 0.30f;
    public float inclinacion = -8f;
    [Tooltip("Punto de la lente dentro de la imagen (0..1)")]
    public Vector2 lente = new Vector2(0.93f, 0.5f);
    [Tooltip("Lente de la linterna dibujada en Lucas de espaldas (camino de los troncos), en fracciones de Lucas (0..1)")]
    public Vector2 lenteEspalda = new Vector2(0.712f, 0.411f);

    RectTransform lucas, linterna;
    RawImage capa;
    Material material;
    Sprite spriteLinterna;
    Vector2 direccion = Vector2.right;
    int mirada = 1;               // 1 derecha, -1 izquierda
    string escena;
    bool voltear;
    Vector3 posAnterior;
    float parpadeo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("Linterna");
        go.AddComponent<Linterna>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        var shader = Resources.Load<Shader>("Oscuridad");
        if (shader == null) shader = Shader.Find("Juego/Oscuridad");
        if (shader != null) material = new Material(shader);
        else Debug.LogWarning("Linterna: no se encontró el shader Juego/Oscuridad");

        var tex = Resources.Load<Texture2D>("Linterna/LinternaCinturon");
        if (tex != null) spriteLinterna = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        SceneManager.sceneLoaded += AlCargar;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= AlCargar;

    void AlCargar(Scene s, LoadSceneMode modo)
    {
        Objetivo = null;
        intensidad = 1f;
        lucas = linterna = null;
        capa = null;
        escena = s.name;
        if (material == null || System.Array.IndexOf(Escenas, escena) < 0) return;

        var go = GameObject.Find("Lucas");
        if (go == null) return;
        lucas = go.GetComponent<RectTransform>();
        var canvas = go.GetComponentInParent<Canvas>()?.rootCanvas;
        if (lucas == null || canvas == null) return;

        // --- linterna colgada del cinturón (en el camino ya viene dibujada en Lucas de espaldas)
        if (spriteLinterna != null && escena != "CaminoLena")
        {
            var l = new GameObject("LinternaCinturon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            linterna = l.GetComponent<RectTransform>();
            linterna.SetParent(lucas, false);
            var r = lucas.rect;
            float ancho = r.width * anchoLinterna;
            linterna.anchorMin = linterna.anchorMax = new Vector2(0.5f, 0.5f);
            linterna.sizeDelta = new Vector2(ancho, ancho * spriteLinterna.rect.height / spriteLinterna.rect.width);
            linterna.anchoredPosition = r.center + new Vector2(r.width * posicionCinturon.x, r.height * posicionCinturon.y);
            linterna.localRotation = Quaternion.Euler(0, 0, inclinacion);
            var img = l.GetComponent<Image>();
            img.sprite = spriteLinterna;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        // --- capa de oscuridad justo encima del "mundo" (hijo directo del canvas que contiene a Lucas)
        Transform raiz = lucas;
        while (raiz.parent != null && raiz.parent != canvas.transform) raiz = raiz.parent;
        var capaGO = new GameObject("Oscuridad", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        capaGO.layer = canvas.gameObject.layer;
        var rt = capaGO.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetSiblingIndex(raiz.GetSiblingIndex() + 1);
        capa = capaGO.GetComponent<RawImage>();
        capa.material = material;
        capa.raycastTarget = false;

        voltear = escena != "CaminoLena";   // en el camino corre hacia el fondo: no se voltea
        mirada = 1;
        direccion = DireccionDeseada();
        posAnterior = lucas.position;
    }

    Vector2 DireccionDeseada()
    {
        if (escena == "CaminoLena") return new Vector2(0.08f, 1f).normalized;          // los troncos vienen del fondo
        if (escena == "SampleScene") return new Vector2(0.55f * mirada, 0.85f).normalized; // hacia los caminos
        return new Vector2(mirada, -0.12f).normalized;                                   // hacia adelante
    }

    void LateUpdate()
    {
        if (capa == null || lucas == null) return;
        Vector2 pantalla = new Vector2(Screen.width, Screen.height);

        // --- hacia dónde mira Lucas
        Vector3 mov = lucas.position - posAnterior;
        posAnterior = lucas.position;
        bool hayObjetivo = Objetivo != null && Objetivo.gameObject.activeInHierarchy;
        if (hayObjetivo) mirada = Objetivo.position.x >= lucas.position.x ? 1 : -1;
        else if (mov.x < -0.5f) mirada = -1;
        else if (mov.x > 0.5f) mirada = 1;
        if (voltear)
        {
            var e = lucas.localScale;
            lucas.localScale = new Vector3(Mathf.Abs(e.x) * mirada, e.y, e.z);
        }

        // --- posición de la lente y del cuerpo en pantalla
        Vector2 lenteP;
        if (linterna != null)
        {
            var r = linterna.rect;
            Vector3 w = linterna.TransformPoint(new Vector2(r.xMin + r.width * lente.x, r.yMin + r.height * lente.y));
            lenteP = RectTransformUtility.WorldToScreenPoint(null, w);
        }
        else
        {
            var r = lucas.rect;
            Vector3 w = lucas.TransformPoint(new Vector2(r.xMin + r.width * lenteEspalda.x, r.yMin + r.height * lenteEspalda.y));
            lenteP = RectTransformUtility.WorldToScreenPoint(null, w);
        }
        Vector2 cuerpoP = RectTransformUtility.WorldToScreenPoint(null, lucas.TransformPoint(lucas.rect.center));
        float altoLucas = Mathf.Abs(lucas.rect.height * lucas.lossyScale.y) / pantalla.y;

        // --- dirección del haz
        Vector2 deseada = DireccionDeseada();
        float largo = largoHaz;
        if (hayObjetivo)
        {
            Vector2 o = RectTransformUtility.WorldToScreenPoint(null, Objetivo.position) - lenteP;
            deseada = o.normalized;
            largo = Mathf.Max(largoHaz, o.magnitude / pantalla.y + 0.15f);
        }
        direccion = ((Vector2)Vector3.Slerp(direccion, deseada, 1f - Mathf.Exp(-Time.deltaTime * 10f))).normalized;
        if (direccion.sqrMagnitude < 0.01f) direccion = deseada;

        // pequeño parpadeo de la linterna
        parpadeo = Mathf.Lerp(parpadeo, Random.Range(-1f, 1f), Time.deltaTime * 6f);
        float pulso = 1f + parpadeo * 0.02f;

        float i = Mathf.Clamp01(intensidad);
        var calido = luzCalida; calido.a *= i;
        material.SetColor("_Color", oscuridad);
        material.SetColor("_Calido", calido);
        material.SetVector("_Lente", new Vector4(lenteP.x / pantalla.x, lenteP.y / pantalla.y, Mathf.Max(0.001f, radioHalo * pulso * i), pantalla.x / pantalla.y));
        material.SetVector("_Haz", new Vector4(direccion.x, direccion.y, Mathf.Max(0.001f, largo * pulso * i), aperturaHaz));
        material.SetVector("_Cuerpo", new Vector4(cuerpoP.x / pantalla.x, cuerpoP.y / pantalla.y, altoLucas * radioCuerpo, brilloCuerpo * i));
    }
}
