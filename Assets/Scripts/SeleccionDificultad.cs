using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla "Elige la dificultad" (se abre al pulsar Comenzar en Inicio).
/// Tres tarjetas: Fácil · Normal · Difícil. Empieza en Normal.
///   Inclinarse a la izquierda / derecha (o flechas)  → cambia la tarjeta
///   Levantar la mano derecha (o Enter / Espacio)     → confirma
///   Clic en una tarjeta → la elige; clic otra vez → confirma
/// Se dibuja por código encima del Canvas de Inicio, con los colores del Figma.
/// </summary>
public class SeleccionDificultad : MonoBehaviour
{
    static readonly Color AzulNoche = Hex("#101F3D");
    static readonly Color TextoClaro = Hex("#E6ECF5");
    static readonly Color Amarillo = Hex("#F4C95D");
    static readonly Color VerdeBorde = Hex("#4F8A5B");
    static readonly Color VerdeProfundo = Hex("#18392B");
    static readonly Color BlancoCalido = Hex("#FFFCF6");
    static readonly Color BordeTarjeta = Hex("#F7D98C");
    static readonly Color Gris = Hex("#879188");
    static readonly Color Naranja = Hex("#EF8354");

    internal static readonly string[] Nombres = { "Fácil", "Normal", "Difícil" };
    internal static readonly string[] Descripciones =
    {
        "Troncos más lentos\ny separados.\nMás tiempo para\natrapar el pez.",
        "La aventura\ntal como es.",
        "Troncos más rápidos\ny seguidos.\nMenos tiempo para\natrapar el pez."
    };

    Action<Dificultad> alConfirmar;
    CanvasGroup grupo;
    RectTransform[] tarjetas = new RectTransform[3];
    Image[] fondos = new Image[3];
    Image[] bordes = new Image[3];
    TextMeshProUGUI[] titulos = new TextMeshProUGUI[3];
    TextMeshProUGUI[] textos = new TextMeshProUGUI[3];
    TextMeshProUGUI aviso;
    int seleccion = (int)Dificultad.Normal;
    float listoEn;
    bool confirmado;

    public static SeleccionDificultad Mostrar(RectTransform canvas, Action<Dificultad> alConfirmar)
    {
        var go = new GameObject("SeleccionDificultad", typeof(RectTransform), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.SetAsLastSibling();
        Estirar(rt);
        var s = go.AddComponent<SeleccionDificultad>();
        s.alConfirmar = alConfirmar;
        s.Construir(rt);
        return s;
    }

    void Construir(RectTransform raiz)
    {
        grupo = GetComponent<CanvasGroup>();
        grupo.alpha = 0f;
        listoEn = Time.unscaledTime + 0.8f;   // la mano derecha de "Comenzar" no confirma de una vez

        TMP_FontAsset titan = null, nunito = null;
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            if (t.font == null) continue;
            if (titan == null && t.font.name.Contains("Titan")) titan = t.font;
            if (nunito == null && t.font.name.Contains("Nunito")) nunito = t.font;
        }
        if (titan == null) titan = TMP_Settings.defaultFontAsset;
        if (nunito == null) nunito = titan;

        // Fondo azul noche (bloquea los clics a lo que hay detrás)
        var fondo = Img("Fondo", raiz, null, AzulNoche);
        fondo.raycastTarget = true;
        Estirar(fondo.rectTransform);

        var redondo = Redondeado();

        Txt("Titulo", raiz, titan, "Elige la dificultad", 86, Amarillo, new Vector2(0, 360), new Vector2(1400, 120));
        Txt("Subtitulo", raiz, nunito, "Inclínate a la izquierda o a la derecha para cambiar", 36, TextoClaro,
            new Vector2(0, 270), new Vector2(1400, 60));

        for (int i = 0; i < 3; i++)
        {
            int indice = i;
            var borde = Img("Tarjeta" + Nombres[i], raiz, redondo, BordeTarjeta);
            borde.type = Image.Type.Sliced;
            borde.raycastTarget = true;
            var rt = borde.rectTransform;
            rt.sizeDelta = new Vector2(440, 380);
            rt.anchoredPosition = new Vector2((i - 1) * 520f, -10f);
            var boton = borde.gameObject.AddComponent<Button>();
            boton.transition = Selectable.Transition.None;
            boton.onClick.AddListener(() => Clic(indice));

            var relleno = Img("Relleno", rt, redondo, BlancoCalido);
            relleno.type = Image.Type.Sliced;
            Estirar(relleno.rectTransform, 8);

            titulos[i] = Txt("Nombre", rt, titan, Nombres[i], 64, VerdeProfundo, new Vector2(0, 110), new Vector2(400, 90));
            textos[i] = Txt("Descripcion", rt, nunito, Descripciones[i], 28, VerdeProfundo, new Vector2(0, -50), new Vector2(380, 200));

            tarjetas[i] = rt; bordes[i] = borde; fondos[i] = relleno;
        }

        // Etiqueta "Recomendado" sobre Normal
        var etiqueta = Img("Etiqueta", tarjetas[1], redondo, Naranja);
        etiqueta.type = Image.Type.Sliced;
        etiqueta.rectTransform.sizeDelta = new Vector2(230, 48);
        etiqueta.rectTransform.anchoredPosition = new Vector2(0, 190);
        Txt("Texto", etiqueta.rectTransform, nunito, "<b>Recomendado</b>", 24, Color.white, Vector2.zero, new Vector2(220, 44));

        // Aviso de confirmar con la mano derecha
        var tex = Resources.Load<Texture2D>("Gestos/ManosAbiertas");
        if (tex != null)
        {
            var mano = Img("Mano", raiz, Sprite.Create(tex, new Rect(tex.width / 2f, 0, tex.width / 2f, tex.height), new Vector2(0.5f, 0.5f), 100f), Color.white);
            mano.preserveAspect = true;
            mano.rectTransform.sizeDelta = new Vector2(70, 96);
            mano.rectTransform.anchoredPosition = new Vector2(-400, -330);
        }
        aviso = Txt("Aviso", raiz, nunito, "<b>Levanta la mano derecha para confirmar</b>", 36, Amarillo,
            new Vector2(40, -320), new Vector2(800, 56));

        Refrescar(true);
        StartCoroutine(Fundido(0f, 1f, 0.35f));
    }

    void Update()
    {
        float t = Time.unscaledTime;
        if (aviso != null)
        {
            var c = aviso.color; c.a = 0.75f + Mathf.Sin(t * 3f) * 0.25f; aviso.color = c;
        }
        for (int i = 0; i < 3; i++)
        {
            float objetivo = i == seleccion ? 1.08f + Mathf.Sin(t * 2.2f) * 0.015f : 0.9f;
            float s = Mathf.Lerp(tarjetas[i].localScale.x, objetivo, Time.unscaledDeltaTime * 12f);
            tarjetas[i].localScale = new Vector3(s, s, 1f);
        }

        if (confirmado || t < listoEn || Transicion.Cargando) return;

        if (EntradaJugador.Izquierda) Cambiar(seleccion - 1);
        else if (EntradaJugador.Derecha) Cambiar(seleccion + 1);
        else if (EntradaJugador.Accion) Confirmar();
    }

    void Clic(int i)
    {
        if (confirmado || Time.unscaledTime < listoEn) return;
        if (i == seleccion) Confirmar(); else Cambiar(i);
    }

    void Cambiar(int i)
    {
        i = Mathf.Clamp(i, 0, 2);
        if (i == seleccion) return;
        seleccion = i;
        Sonido.Efecto("CambioCarril", 0.7f);
        Refrescar(false);
    }

    void Refrescar(bool inmediato)
    {
        for (int i = 0; i < 3; i++)
        {
            bool sel = i == seleccion;
            bordes[i].color = sel ? VerdeBorde : BordeTarjeta;
            fondos[i].color = sel ? Amarillo : BlancoCalido;
            var cg = tarjetas[i].GetComponent<CanvasGroup>();
            if (cg == null) cg = tarjetas[i].gameObject.AddComponent<CanvasGroup>();
            cg.alpha = sel ? 1f : 0.7f;
            if (inmediato) tarjetas[i].localScale = Vector3.one * (sel ? 1.08f : 0.9f);
        }
    }

    void Confirmar()
    {
        confirmado = true;
        Sonido.Efecto("Boton");
        StartCoroutine(Pulso(tarjetas[seleccion]));
        alConfirmar?.Invoke((Dificultad)seleccion);
    }

    IEnumerator Pulso(RectTransform rt)
    {
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.3f)
        {
            rt.localScale = Vector3.one * (1.08f + Mathf.Sin(k * Mathf.PI) * 0.1f);
            yield return null;
        }
    }

    IEnumerator Fundido(float a, float b, float d)
    {
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / d)
        {
            grupo.alpha = Mathf.Lerp(a, b, k);
            yield return null;
        }
        grupo.alpha = b;
    }

    // ------------------------------------------------------------ Utilidades de UI

    static Sprite cacheRedondo;
    /// <summary>Rectángulo redondeado blanco (9-slice) dibujado por código.</summary>
    internal static Sprite Redondeado()
    {
        if (cacheRedondo != null) return cacheRedondo;
        const int n = 64, r = 24;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        cacheRedondo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return cacheRedondo;
    }

    internal static Image Img(string nombre, RectTransform padre, Sprite s, Color c)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    internal static TextMeshProUGUI Txt(string nombre, RectTransform padre, TMP_FontAsset f, string texto, float tam, Color c, Vector2 pos, Vector2 tamano)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;
        var t = go.GetComponent<TextMeshProUGUI>();
        if (f != null) t.font = f;
        t.text = texto;
        t.fontSize = tam;
        t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = tam * 0.6f; t.fontSizeMax = tam;
        t.raycastTarget = false;
        return t;
    }

    internal static void Estirar(RectTransform rt, float margen = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margen, margen); rt.offsetMax = new Vector2(-margen, -margen);
    }

    internal static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
