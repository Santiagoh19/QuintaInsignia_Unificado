using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SD = SeleccionDificultad;

/// <summary>
/// Ventana "¡Has ganado esta insignia!" (al terminar la pesca, cuando ya se tiene leña y comida).
/// Muestra la insignia del nivel con rayos girando y destellos.
/// Mano derecha (o Enter / clic) → continuar.
/// </summary>
public class VentanaInsignia : MonoBehaviour
{
    public static bool Abierta { get; private set; }

    Action alContinuar;
    CanvasGroup grupo;
    RectTransform tarjeta, insignia, rayos;
    TextMeshProUGUI aviso;
    float listoEn, inicio;
    bool cerrando;

    public static VentanaInsignia Mostrar(string recursoInsignia, string titulo, string mensaje, Action alContinuar)
        => Mostrar(recursoInsignia, titulo, mensaje, alContinuar, EstadoJuego.InsigniaDeEsteNivel);

    public static VentanaInsignia Mostrar(string recursoInsignia, string titulo, string mensaje, Action alContinuar, int numeroInsignia)
    {
        var go = new GameObject("VentanaInsignia", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;   // encima del juego y la oscuridad; debajo de la pausa (850) y la cámara (900)
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        var v = go.AddComponent<VentanaInsignia>();
        v.alContinuar = alContinuar;
        v.Construir((RectTransform)go.transform, recursoInsignia, titulo, mensaje);
        Abierta = true;
        InsigniasHUD.Ganar(numeroInsignia, 1.3f);   // llena el círculo de abajo a la izquierda
        return v;
    }

    void OnDestroy() => Abierta = false;

    void Construir(RectTransform raiz, string recurso, string titulo, string mensaje)
    {
        grupo = GetComponent<CanvasGroup>();
        grupo.alpha = 0f;
        listoEn = Time.unscaledTime + 1.2f;
        inicio = Time.unscaledTime;

        TMP_FontAsset titan = null, nunito = null;
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            if (t.font == null) continue;
            if (titan == null && t.font.name.Contains("Titan")) titan = t.font;
            if (nunito == null && t.font.name.Contains("Nunito")) nunito = t.font;
        }
        // en los niveles de los compañeros no hay textos TMP en la escena: las fuentes vienen de Resources
        if (titan == null) titan = Resources.Load<TMP_FontAsset>("Fuentes/TitanOne SDF");
        if (nunito == null) nunito = Resources.Load<TMP_FontAsset>("Fuentes/NunitoSans SDF");
        if (titan == null) titan = TMP_Settings.defaultFontAsset;
        if (nunito == null) nunito = titan;

        var redondo = SD.Redondeado();
        var velo = SD.Img("Velo", raiz, null, new Color(0.063f, 0.122f, 0.239f, 0.85f));
        velo.raycastTarget = true;
        SD.Estirar(velo.rectTransform);

        // Tarjeta
        var borde = SD.Img("Tarjeta", raiz, redondo, SD.Hex("#F7D98C"));
        borde.type = Image.Type.Sliced;
        tarjeta = borde.rectTransform;
        tarjeta.sizeDelta = new Vector2(1000, 860);
        tarjeta.anchoredPosition = new Vector2(0, -10);
        var relleno = SD.Img("Relleno", tarjeta, redondo, SD.Hex("#FFFCF6"));
        relleno.type = Image.Type.Sliced;
        SD.Estirar(relleno.rectTransform, 10);

        SD.Txt("Titulo", tarjeta, titan, titulo, 66, SD.Hex("#365F73"), new Vector2(0, 345), new Vector2(920, 90));

        // Rayos detrás de la insignia
        var r = SD.Img("Rayos", tarjeta, Rayos(), SD.Hex("#F4C95D"));
        rayos = r.rectTransform;
        rayos.sizeDelta = new Vector2(560, 560);
        rayos.anchoredPosition = new Vector2(0, 60);
        var c = r.color; c.a = 0.55f; r.color = c;

        var tex = Resources.Load<Texture2D>(recurso);
        var img = SD.Img("Insignia", tarjeta, tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null, Color.white);
        img.preserveAspect = true;
        insignia = img.rectTransform;
        insignia.sizeDelta = new Vector2(360, 360);
        insignia.anchoredPosition = new Vector2(0, 60);
        insignia.localScale = Vector3.zero;

        SD.Txt("Mensaje", tarjeta, nunito, mensaje, 40, SD.Hex("#18392B"), new Vector2(0, -190), new Vector2(900, 110));

        // Botón "Levanta la mano derecha para continuar"
        var b = SD.Img("Continuar", tarjeta, redondo, SD.Hex("#F4C95D"));
        b.type = Image.Type.Sliced; b.raycastTarget = true;
        b.rectTransform.sizeDelta = new Vector2(720, 84);
        b.rectTransform.anchoredPosition = new Vector2(0, -305);
        var bt = b.gameObject.AddComponent<Button>();
        bt.transition = Selectable.Transition.None;
        bt.onClick.AddListener(Continuar);
        var texManos = Resources.Load<Texture2D>("Gestos/ManosAbiertas");
        if (texManos != null)
        {
            var mano = SD.Img("ManoDerecha", b.rectTransform,
                Sprite.Create(texManos, new Rect(texManos.width / 2f, 0, texManos.width / 2f, texManos.height), new Vector2(0.5f, 0.5f), 100f), Color.white);
            mano.preserveAspect = true;
            mano.rectTransform.sizeDelta = new Vector2(46, 64);
            mano.rectTransform.anchoredPosition = new Vector2(-310, 0);
        }
        aviso = SD.Txt("Texto", b.rectTransform, nunito, "<b>Levanta la mano derecha para continuar</b>", 30, SD.Hex("#18392B"), new Vector2(25, 0), new Vector2(600, 64));

        // Destellos alrededor de la insignia
        var estrella = Estrella();
        Vector2[] pos = { new(-250, 200), new(250, 180), new(-280, -40), new(290, -20), new(-190, 250), new(200, 260) };
        for (int i = 0; i < pos.Length; i++)
        {
            var e = SD.Img("Destello" + i, tarjeta, estrella, SD.Hex(i % 2 == 0 ? "#F4C95D" : "#EF8354"));
            e.rectTransform.sizeDelta = Vector2.one * (i % 2 == 0 ? 46 : 32);
            e.rectTransform.anchoredPosition = pos[i] + new Vector2(0, 60);
            e.gameObject.AddComponent<Titilar>().fase = i * 0.9f;
        }

        Sonido.Efecto("Aparecer", 0.8f, 0f);
        Sonido.Efecto("Destello", 0.8f, 0f);
        StartCoroutine(Entrar());
    }

    IEnumerator Entrar()
    {
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.35f)
        {
            grupo.alpha = k;
            tarjeta.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0, 1, k));
            yield return null;
        }
        grupo.alpha = 1f; tarjeta.localScale = Vector3.one;
        // la insignia "rebota" al aparecer
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.6f)
        {
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
            insignia.localScale = Vector3.one * s;
            yield return null;
        }
        insignia.localScale = Vector3.one;
    }

    void Update()
    {
        float t = Time.unscaledTime;
        if (rayos != null) rayos.localRotation = Quaternion.Euler(0, 0, -t * 18f);
        if (insignia != null && t - inicio > 1f)
        {
            insignia.localScale = Vector3.one * (1f + Mathf.Sin(t * 2.2f) * 0.025f);
            insignia.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * 3f);
        }
        if (aviso != null) { var c = aviso.color; c.a = 0.75f + Mathf.Sin(t * 3f) * 0.25f; aviso.color = c; }

        if (cerrando || t < listoEn || Transicion.Cargando) return;
        bool mano = PoseReceiver.Instancia != null && PoseReceiver.Instancia.ManoDerechaAhora;
        if (mano || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            Continuar();
    }

    void Continuar()
    {
        if (cerrando || Time.unscaledTime < listoEn) return;
        cerrando = true;
        Sonido.Efecto("Boton");
        StartCoroutine(Salir());
    }

    IEnumerator Salir()
    {
        for (float k = 1f; k > 0f; k -= Time.unscaledDeltaTime / 0.3f) { grupo.alpha = k; yield return null; }
        grupo.alpha = 0f;
        Abierta = false;
        alContinuar?.Invoke();
        Destroy(gameObject, 0.1f);
    }

    // ------------------------------------------------------------ Sprites dibujados por código

    static Sprite cacheRayos, cacheEstrella;

    static Sprite Rayos()
    {
        if (cacheRayos != null) return cacheRayos;
        const int n = 256; const int rayosN = 14;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (n / 2f);
                float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * rayosN;
                float rayo = Mathf.Clamp01((0.5f - Mathf.Abs(ang - Mathf.Round(ang))) * 6f - 1.2f);
                float a = rayo * Mathf.Clamp01(1f - d) * Mathf.Clamp01(d * 3f);
                a = Mathf.Max(a, Mathf.Clamp01(1f - d * 1.6f) * 0.6f);   // brillo suave al centro
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return cacheRayos = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite Estrella()
    {
        if (cacheEstrella != null) return cacheEstrella;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - n / 2f) / (n / 2f), dy = Mathf.Abs(y + 0.5f - n / 2f) / (n / 2f);
                // destello de 4 puntas
                float a = Mathf.Clamp01(1f - (Mathf.Sqrt(dx) + Mathf.Sqrt(dy)) * 1.1f) * 3f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return cacheEstrella = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    class Titilar : MonoBehaviour
    {
        public float fase;
        void Update()
        {
            float s = 0.6f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f + fase)) * 0.6f;
            transform.localScale = Vector3.one * s;
            transform.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 40f + fase * 30f);
        }
    }
}
