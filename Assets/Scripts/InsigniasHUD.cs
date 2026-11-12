using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Insignias abajo a la izquierda: 5 círculos en fila, sin título.
/// Llenos = insignias ganadas (empieza con 2: las de los juegos anteriores). Al ganar la de este nivel
/// se llena el 3.º con una animación. Se crea solo y vive entre escenas.
///
/// MEDIDAS (en la resolución de referencia 1920x1080, CanvasScaler "Scale With Screen Size", match 0.5):
///   ancla abajo-izquierda, margen 24 px desde la izquierda y 24 px desde abajo;
///   5 círculos de 64x64 px, separación 12 px  →  bloque de 368x64 px
///   (de x=24 a x=392, de y=992 a y=1056 contando desde arriba).
///   Vacío: disco azul noche #101F3D al 55 % + aro #F7D98C de 3 px.  Lleno: la imagen de la insignia a 64x64.
/// </summary>
public class InsigniasHUD : MonoBehaviour
{
    public const float Tamano = 64f, Separacion = 12f, Margen = 24f;
    static readonly string[] Recursos = { "Insignias/Insignia1", "Insignias/Insignia2", "Insignias/Insignia3", "Insignias/Insignia4", "Insignias/Insignia5" };

    public static InsigniasHUD Instancia { get; private set; }

    Canvas canvas;
    Image[] insignias = new Image[EstadoJuego.TotalInsignias];
    Image[] aros = new Image[EstadoJuego.TotalInsignias];
    int mostradas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("InsigniasHUD");
        go.AddComponent<InsigniasHUD>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        Construir();
        SceneManager.sceneLoaded += AlCargar;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= AlCargar;

    void AlCargar(Scene s, LoadSceneMode m)
    {
        StopAllCoroutines(); Refrescar();
        // el nivel 5 tiene su barra de la fogata abajo a la izquierda: ahí no se muestran (por ahora)
        canvas.enabled = s.name != NivelesUnificados.EscenaHistoria;   // en todos los niveles (no en la historia del inicio)
    }

    /// <summary>Gana la insignia n (1..5); el círculo se llena tras 'retraso' segundos (tiempo real).</summary>
    public static void Ganar(int n, float retraso = 0f)
    {
        if (EstadoJuego.InsigniasGanadas >= n) return;
        EstadoJuego.InsigniasGanadas = n;
        if (Instancia != null) Instancia.StartCoroutine(Instancia.Llenar(n - 1, retraso));
    }

    void Construir()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 810;   // encima de la ventana de la insignia (800), debajo de la pausa (850) y la cámara (900)
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;

        var fila = new GameObject("Insignias", typeof(RectTransform)).GetComponent<RectTransform>();
        fila.SetParent(go.transform, false);
        fila.anchorMin = fila.anchorMax = fila.pivot = Vector2.zero;
        fila.anchoredPosition = new Vector2(Margen, Margen);
        fila.sizeDelta = new Vector2(EstadoJuego.TotalInsignias * Tamano + (EstadoJuego.TotalInsignias - 1) * Separacion, Tamano);

        var disco = Disco(); var aro = Aro();
        for (int i = 0; i < EstadoJuego.TotalInsignias; i++)
        {
            var slot = new GameObject("Circulo" + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
            slot.SetParent(fila, false);
            slot.anchorMin = slot.anchorMax = slot.pivot = Vector2.zero;
            slot.anchoredPosition = new Vector2(i * (Tamano + Separacion), 0f);
            slot.sizeDelta = new Vector2(Tamano, Tamano);

            var fondo = Img("Vacio", slot, disco, new Color(0.063f, 0.122f, 0.239f, 0.55f));
            aros[i] = Img("Aro", slot, aro, SeleccionDificultad.Hex("#F7D98C"));
            var tex = Resources.Load<Texture2D>(Recursos[i]);
            insignias[i] = Img("Insignia", slot, tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null, Color.white);
            insignias[i].preserveAspect = true;
        }
        Refrescar();
    }

    public void Refrescar()
    {
        mostradas = EstadoJuego.InsigniasGanadas;
        for (int i = 0; i < insignias.Length; i++)
        {
            bool lleno = i < mostradas && insignias[i].sprite != null;
            insignias[i].gameObject.SetActive(lleno);
            insignias[i].rectTransform.localScale = Vector3.one;
            aros[i].gameObject.SetActive(!lleno);
        }
    }

    IEnumerator Llenar(int i, float retraso)
    {
        float hasta = Time.unscaledTime + retraso;
        while (Time.unscaledTime < hasta) yield return null;
        if (i < 0 || i >= insignias.Length) yield break;
        mostradas = Mathf.Max(mostradas, i + 1);
        var rt = insignias[i].rectTransform;
        insignias[i].gameObject.SetActive(true);
        aros[i].gameObject.SetActive(false);
        Sonido.Efecto("Destello", 0.8f, 0f);
        // aparece grande y se acomoda en su círculo
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.55f)
        {
            float s = k < 0.6f ? Mathf.Lerp(0f, 1.5f, k / 0.6f) : Mathf.Lerp(1.5f, 1f, (k - 0.6f) / 0.4f);
            rt.localScale = Vector3.one * s;
            rt.localRotation = Quaternion.Euler(0, 0, (1f - k) * 25f);
            yield return null;
        }
        rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
    }

    static Image Img(string nombre, RectTransform padre, Sprite s, Color c)
    {
        var img = SeleccionDificultad.Img(nombre, padre, s, c);
        SeleccionDificultad.Estirar(img.rectTransform);
        return img;
    }

    static Sprite cacheDisco, cacheAro;
    static Sprite Disco() => cacheDisco != null ? cacheDisco : (cacheDisco = Circulo(0f));
    static Sprite Aro() => cacheAro != null ? cacheAro : (cacheAro = Circulo(3f));

    /// <summary>grosor 0 = disco lleno; si no, aro de ese grosor (en px de un círculo de 64).</summary>
    static Sprite Circulo(float grosor)
    {
        const int n = 128; float esc = n / Tamano;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        float r = n / 2f - 1f, g = grosor * esc;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (grosor > 0f) a *= Mathf.Clamp01(d - (r - g) + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
