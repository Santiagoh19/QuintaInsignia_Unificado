using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de carga entre escenas: fundido a una pantalla azul noche con estrellas,
/// "Cargando..." en Titan One y el nombre de la misión que viene. Dura unos 3 segundos
/// y baja el volumen mientras tanto, para que el cambio de ambiente y música sea suave.
/// Uso: Transicion.Cargar("NombreEscena") en lugar de SceneManager.LoadScene.
/// Se crea sola y vive entre escenas.
/// </summary>
public class Transicion : MonoBehaviour
{
    public static Transicion Instancia { get; private set; }
    public static bool Cargando { get; private set; }

    [Tooltip("Tiempo total aproximado de la pantalla de carga (segundos)")]
    public float duracion = 3f;
    public float fundido = 0.45f;
    [Range(0f, 1f)] public float volumenDuranteCarga = 0.3f;

    static readonly Color AzulNoche = new Color32(0x10, 0x1F, 0x3D, 255);
    static readonly Color Amarillo = new Color32(0xF4, 0xC9, 0x5D, 255);
    static readonly Color TextoClaro = new Color32(0xE6, 0xEC, 0xF5, 255);

    static TMP_FontAsset fuenteTitulo, fuenteCuerpo;
    static Sprite spriteEstrellas;

    Canvas canvas;
    CanvasGroup grupo;
    Image estrellas;
    TextMeshProUGUI texto, subtitulo;
    RectTransform linterna;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("Transicion");
        go.AddComponent<Transicion>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        SceneManager.sceneLoaded += (s, m) => BuscarRecursos();
    }

    // ------------------------------------------------------------------ API

    public static void Cargar(string escena)
    {
        if (Instancia == null) { SceneManager.LoadScene(escena); return; }
        if (Cargando) return;                       // evita cargar dos veces (p. ej. dos gestos seguidos)
        Instancia.StartCoroutine(Instancia.Rutina(escena));
    }

    public static void Recargar() => Cargar(SceneManager.GetActiveScene().name);

    static string Titulo(string escena)
    {
        switch (escena)
        {
            case "Historia": return "Quinta Insignia";
            case "Inicio": return "Nivel 3 · Cruce del bosque";
            case "SampleScene": return "Leña y comida";
            case "CaminoLena": return "Camino de la leña";
            case "Lena": return "Bosque de la leña";
            case "Pesca": return "Lago de la pesca";
            case "Sombras": return "Nivel 5 · Sombras en la carpa";
            case "CarpaInicio": return "Nivel 1 · Arma tu carpa";
            case "Recorrido": return "Nivel 2 · Ruta de Energía";
            case "Fogata": return "Nivel 4 · Pinzas de fuego";
            default: return "";
        }
    }

    // ------------------------------------------------------------------ Carga

    IEnumerator Rutina(string escena)
    {
        Cargando = true;
        BuscarRecursos();
        Construir();
        subtitulo.text = Titulo(escena);
        canvas.enabled = true;
        grupo.blocksRaycasts = true;
        float inicio = Time.unscaledTime;

        // entra la pantalla y baja el volumen
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / fundido)
        {
            grupo.alpha = k;
            AudioListener.volume = Mathf.Lerp(1f, volumenDuranteCarga, k);
            Animar();
            yield return null;
        }
        grupo.alpha = 1f;
        AudioListener.volume = volumenDuranteCarga;

        var op = SceneManager.LoadSceneAsync(escena);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f || Time.unscaledTime - inicio < duracion - fundido)
        {
            Animar();
            yield return null;
        }
        op.allowSceneActivation = true;
        while (!op.isDone) { Animar(); yield return null; }
        yield return null;

        // sale la pantalla y vuelve el volumen
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / fundido)
        {
            grupo.alpha = 1f - k;
            AudioListener.volume = Mathf.Lerp(volumenDuranteCarga, 1f, k);
            Animar();
            yield return null;
        }
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        canvas.enabled = false;
        AudioListener.volume = 1f;
        Cargando = false;
    }

    void Animar()
    {
        int puntos = (int)(Time.unscaledTime * 2.5f) % 4;
        // los puntos que faltan van invisibles para que la palabra no se mueva
        texto.text = "Cargando" + new string('.', puntos) + "<alpha=#00>" + new string('.', 3 - puntos);
        float t = Time.unscaledTime;
        if (linterna != null)
        {
            linterna.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2.2f) * 8f);
            linterna.localScale = Vector3.one * (1f + Mathf.Sin(t * 4f) * 0.03f);
        }
        if (estrellas != null) estrellas.color = new Color(1, 1, 1, 0.75f + Mathf.Sin(t * 1.7f) * 0.25f);
    }

    // ------------------------------------------------------------------ Interfaz

    /// <summary>Toma las fuentes del juego (Titan One / Nunito Sans) y las estrellas de la pantalla de inicio.</summary>
    static void BuscarRecursos()
    {
        if (fuenteTitulo == null || fuenteCuerpo == null)
        {
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.font == null) continue;
                string n = t.font.name.ToLower();
                if (fuenteTitulo == null && n.Contains("titan")) fuenteTitulo = t.font;
                if (fuenteCuerpo == null && n.Contains("nunito")) fuenteCuerpo = t.font;
            }
        }
        if (spriteEstrellas == null)
        {
            var go = GameObject.Find("Estrellas");
            if (go != null && go.TryGetComponent<Image>(out var img)) spriteEstrellas = img.sprite;
        }
        if (Instancia != null && Instancia.texto != null) Instancia.AplicarRecursos();
    }

    void Construir()
    {
        if (canvas != null) { AplicarRecursos(); return; }

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var escala = gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;

        var fondo = Hijo<Image>("Fondo", transform);
        fondo.color = AzulNoche;
        Estirar(fondo.rectTransform);

        estrellas = Hijo<Image>("Estrellas", transform);
        estrellas.raycastTarget = false;
        Estirar(estrellas.rectTransform);

        var tex = Resources.Load<Texture2D>("Linterna/LinternaCinturon");
        if (tex != null)
        {
            var img = Hijo<Image>("Linterna", transform);
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            img.preserveAspect = true;
            img.raycastTarget = false;
            linterna = img.rectTransform;
            linterna.anchorMin = linterna.anchorMax = new Vector2(0.5f, 0.5f);
            linterna.sizeDelta = new Vector2(170f, 103f);
            linterna.anchoredPosition = new Vector2(0f, 150f);
        }

        texto = Hijo<TextMeshProUGUI>("Cargando", transform);
        texto.fontSize = 96;
        texto.color = Amarillo;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.raycastTarget = false;
        var rt = texto.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1200f, 140f);
        rt.anchoredPosition = new Vector2(0f, 0f);

        subtitulo = Hijo<TextMeshProUGUI>("Destino", transform);
        subtitulo.fontSize = 38;
        subtitulo.color = TextoClaro;
        subtitulo.alignment = TextAlignmentOptions.Center;
        subtitulo.raycastTarget = false;
        var rs = subtitulo.rectTransform;
        rs.anchorMin = rs.anchorMax = new Vector2(0.5f, 0.5f);
        rs.sizeDelta = new Vector2(1200f, 60f);
        rs.anchoredPosition = new Vector2(0f, -95f);

        AplicarRecursos();
        canvas.enabled = false;
    }

    void AplicarRecursos()
    {
        if (fuenteTitulo != null) texto.font = fuenteTitulo;
        if (fuenteCuerpo != null) subtitulo.font = fuenteCuerpo;
        estrellas.sprite = spriteEstrellas;
        estrellas.enabled = spriteEstrellas != null;
    }

    static T Hijo<T>(string nombre, Transform padre) where T : Component
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.transform.SetParent(padre, false);
        return go.GetComponent<T>();
    }

    static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
