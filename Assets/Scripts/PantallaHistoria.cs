using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SD = SeleccionDificultad;

/// <summary>
/// JUEGO UNIFICADO · Pantalla de inicio (escena "Historia", la primera del juego).
/// Página 1: Lucas, un niño scout, empieza su campamento y quiere ganar las cinco insignias.
/// Página 2: el recorrido de las cinco misiones, con su insignia.
/// Mano derecha (o Enter / Espacio / clic) → siguiente página; en la última empieza el nivel 1.
/// Mano izquierda (o Esc) → página anterior. La interfaz se dibuja por código.
/// </summary>
public class PantallaHistoria : MonoBehaviour
{
    public string escenaSiguiente = "CarpaInicio";

    static readonly string[] Misiones =
    {
        "Limpia el lugar y arma tu carpa para dormir.",
        "Lleva las pilas por el camino hasta tu linterna.",
        "Consigue leña y comida en el bosque.",
        "Usa tus dedos como pinzas y prende la fogata.",
        "Haz figuras de sombras con tus manos.",
    };

    RectTransform[] paginas;
    CanvasGroup[] grupos;
    RectTransform[] tarjetas;
    RectTransform lucas;
    Vector2 lucasPos;
    TextMeshProUGUI textoBoton;
    RectTransform boton;
    int pagina;
    float listoEn;
    bool saliendo;
    TMP_FontAsset titan, nunito;

    void Start()
    {
        Construir();
        Mostrar(0);
    }

    void Update()
    {
        float t = Time.unscaledTime;
        if (lucas != null) lucas.anchoredPosition = lucasPos + Vector2.up * Mathf.Sin(t * 1.6f) * 10f;
        if (boton != null) boton.localScale = Vector3.one * (1f + Mathf.Sin(t * 3f) * 0.025f);
        if (pagina == 1 && tarjetas != null)
            for (int i = 0; i < tarjetas.Length; i++)
                tarjetas[i].localScale = Vector3.one * (1f + Mathf.Sin(t * 2f + i * 0.9f) * 0.015f);

        if (saliendo || t < listoEn || Transicion.Cargando) return;
        var pose = PoseReceiver.Instancia;
        bool seguir = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)
                      || Input.GetMouseButtonDown(0) || (pose != null && pose.ManoDerechaAhora);
        bool atras = pagina > 0 && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)
                                    || (pose != null && pose.ManoIzquierdaAhora));
        if (seguir) Siguiente();
        else if (atras) { Sonido.Efecto("Boton"); Mostrar(pagina - 1); }
    }

    void Siguiente()
    {
        Sonido.Efecto("Boton");
        if (pagina < paginas.Length - 1) { Mostrar(pagina + 1); return; }
        saliendo = true;
        Transicion.Cargar(escenaSiguiente);
    }

    void Mostrar(int i)
    {
        pagina = i;
        listoEn = Time.unscaledTime + 0.9f;   // que la misma mano no pase dos páginas
        StopAllCoroutines();
        for (int k = 0; k < paginas.Length; k++)
        {
            paginas[k].gameObject.SetActive(k == i);
            if (k == i) StartCoroutine(Aparecer(grupos[k]));
        }
        textoBoton.text = i < paginas.Length - 1
            ? "<b>Levanta la mano derecha para continuar</b>"
            : "<b>Levanta la mano derecha para comenzar</b>";
    }

    IEnumerator Aparecer(CanvasGroup g)
    {
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.35f) { g.alpha = k; yield return null; }
        g.alpha = 1f;
    }

    // ------------------------------------------------------------ Interfaz

    void Construir()
    {
        titan = Resources.Load<TMP_FontAsset>("Fuentes/TitanOne SDF");
        nunito = Resources.Load<TMP_FontAsset>("Fuentes/NunitoSans SDF");
        if (titan == null) titan = TMP_Settings.defaultFontAsset;
        if (nunito == null) nunito = titan;
        var redondo = SD.Redondeado();

        var go = new GameObject("CanvasHistoria", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var raiz = (RectTransform)go.transform;

        // fondo: el bosque de noche del juego y un velo para que se lea el texto
        var fondo = SD.Img("Fondo", raiz, ACrear("Historia/Fondo"), Color.white);
        SD.Estirar(fondo.rectTransform);
        var velo = SD.Img("Velo", raiz, null, new Color(0.063f, 0.122f, 0.239f, 0.35f));
        SD.Estirar(velo.rectTransform);

        paginas = new RectTransform[2];
        grupos = new CanvasGroup[2];
        for (int i = 0; i < 2; i++)
        {
            var p = new GameObject("Pagina" + (i + 1), typeof(RectTransform), typeof(CanvasGroup));
            paginas[i] = (RectTransform)p.transform;
            paginas[i].SetParent(raiz, false);
            SD.Estirar(paginas[i]);
            grupos[i] = p.GetComponent<CanvasGroup>();
        }

        // ---------------- Página 1: Lucas
        var p1 = paginas[0];
        var pil = SD.Img("Titulo", p1, redondo, SD.Hex("#FFFCF6"));
        pil.type = Image.Type.Sliced;
        pil.rectTransform.sizeDelta = new Vector2(900, 150);
        pil.rectTransform.anchoredPosition = new Vector2(0, 400);
        SD.Txt("Texto", pil.rectTransform, titan, "<color=#365F73>Quinta</color> <color=#4B91B5>Insignia</color>", 96,
            Color.white, Vector2.zero, new Vector2(860, 130));

        var lu = SD.Img("Lucas", p1, ACrear("Historia/Lucas"), Color.white);
        lu.preserveAspect = true;
        lucas = lu.rectTransform;
        lucas.sizeDelta = new Vector2(430, 602);
        lucasPos = new Vector2(-560, -90);
        lucas.anchoredPosition = lucasPos;

        var tarjeta = Tarjeta(p1, redondo, new Vector2(1000, 560), new Vector2(290, -40));
        SD.Txt("Saludo", tarjeta, titan, "¡Hola! Soy Lucas", 68, SD.Hex("#18392B"), new Vector2(0, 190), new Vector2(900, 90));
        SD.Txt("Historia", tarjeta, nunito,
            "Soy un niño scout y hoy empieza mi campamento.\n" +
            "Para convertirme en un gran explorador debo cumplir <b>cinco misiones</b> " +
            "y ganar las <b>cinco insignias</b> del campamento.\n\n<b>¿Me acompañas?</b>",
            40, SD.Hex("#18392B"), new Vector2(0, -40), new Vector2(880, 330));

        // ---------------- Página 2: el recorrido
        var p2 = paginas[1];
        SD.Txt("Titulo", p2, titan, "Tu recorrido", 96, SD.Hex("#F4C95D"), new Vector2(0, 410), new Vector2(1400, 120));
        SD.Txt("Subtitulo", p2, nunito, "Cinco misiones de campamento. En cada una ganas una insignia.", 38,
            SD.Hex("#E6ECF5"), new Vector2(0, 325), new Vector2(1600, 60));
        tarjetas = new RectTransform[5];
        for (int i = 0; i < 5; i++)
        {
            var t = Tarjeta(p2, redondo, new Vector2(340, 520), new Vector2((i - 2) * 365f, -10));
            tarjetas[i] = t;
            var ins = SD.Img("Insignia", t, ACrear("Insignias/Insignia" + (i + 1)), Color.white);
            ins.preserveAspect = true;
            ins.rectTransform.sizeDelta = new Vector2(180, 180);
            ins.rectTransform.anchoredPosition = new Vector2(0, 145);
            var mision = SD.Img("Mision", t, redondo, SD.Hex("#EF8354"));
            mision.type = Image.Type.Sliced;
            mision.rectTransform.sizeDelta = new Vector2(190, 48);
            mision.rectTransform.anchoredPosition = new Vector2(0, 25);
            SD.Txt("Texto", mision.rectTransform, nunito, "<b>Misión " + (i + 1) + "</b>", 28, Color.white, Vector2.zero, new Vector2(180, 44));
            SD.Txt("Nombre", t, titan, NivelesUnificados.NombreDeNivel[i + 1], 40, SD.Hex("#18392B"), new Vector2(0, -50), new Vector2(310, 100));
            SD.Txt("Descripcion", t, nunito, Misiones[i], 28, SD.Hex("#18392B"), new Vector2(0, -170), new Vector2(300, 130));
        }
        SD.Txt("Nota", p2, nunito, "Juegas con tu cuerpo y tus manos frente a la cámara.", 34,
            SD.Hex("#E6ECF5"), new Vector2(0, -325), new Vector2(1600, 56));

        // ---------------- Botón (las dos páginas)
        var bc = SD.Img("Continuar", raiz, redondo, SD.Hex("#F4C95D"));
        bc.type = Image.Type.Sliced;
        boton = bc.rectTransform;
        boton.sizeDelta = new Vector2(820, 90);
        boton.anchoredPosition = new Vector2(0, -440);
        var texManos = Resources.Load<Texture2D>("Gestos/ManosAbiertas");
        if (texManos != null)
        {
            var mano = Sprite.Create(texManos, new Rect(texManos.width / 2f, 0, texManos.width / 2f, texManos.height), new Vector2(0.5f, 0.5f), 100f);
            var md = SD.Img("ManoDerecha", boton, mano, Color.white);
            md.preserveAspect = true;
            md.rectTransform.sizeDelta = new Vector2(48, 66);
            md.rectTransform.anchoredPosition = new Vector2(-350, 0);
        }
        textoBoton = SD.Txt("Texto", boton, nunito, "", 34, SD.Hex("#18392B"), new Vector2(25, 0), new Vector2(680, 70));
    }

    static RectTransform Tarjeta(RectTransform padre, Sprite redondo, Vector2 tam, Vector2 pos)
    {
        var borde = SD.Img("Tarjeta", padre, redondo, SD.Hex("#F7D98C"));
        borde.type = Image.Type.Sliced;
        borde.rectTransform.sizeDelta = tam;
        borde.rectTransform.anchoredPosition = pos;
        var relleno = SD.Img("Relleno", borde.rectTransform, redondo, SD.Hex("#FFFCF6"));
        relleno.type = Image.Type.Sliced;
        SD.Estirar(relleno.rectTransform, 8);
        return borde.rectTransform;
    }

    static Sprite ACrear(string recurso)
    {
        var tex = Resources.Load<Texture2D>(recurso);
        return tex ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
    }
}
