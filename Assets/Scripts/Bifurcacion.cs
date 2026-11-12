using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Escena de la bifurcación: Lucas elige un camino. El de la izquierda lleva a la leña
/// (sin decirlo); el de la derecha, a la pesca (se habilita después de tener la leña).
/// Con leña y comida: "Levanta la mano derecha para ir al campamento" → Lucas sube por el
/// centro del camino hasta la montaña, se desvanece, fundido a negro y mensaje final.
/// </summary>
public class Bifurcacion : MonoBehaviour
{
    [Header("Escenas")]
    public string escenaCaminoLena = "CaminoLena";
    public string escenaPesca = "Pesca";

    [Header("Referencias")]
    public RectTransform lucas;
    public RectTransform destinoIzquierda;
    public RectTransform destinoDerecha;
    public RectTransform flechaIzquierda;
    public RectTransform flechaDerecha;
    public TextMeshProUGUI textoObjetivo;
    public TextMeshProUGUI textoAyuda;
    public TextMeshProUGUI textoLena;
    public TextMeshProUGUI textoPeces;
    public CanvasGroup mensaje;
    public TextMeshProUGUI textoMensaje;

    [Header("Movimiento")]
    public float duracionCaminata = 1.4f;
    public float escalaLejos = 0.4f;

    [Header("Final: regreso al campamento")]
    [Tooltip("Punto en lo alto de la montaña, al centro, donde Lucas desaparece")]
    public Vector2 puntoMontana = new Vector2(0f, 45f);
    public float duracionSubida = 4.5f;
    public float escalaMontana = 0.14f;
    public string escenaInicio = "Inicio";
    [Tooltip("JUEGO UNIFICADO: al terminar este nivel se pasa al siguiente (prueba: nivel 5, Sombras en la carpa)")]
    public string escenaSiguienteNivel = "Fogata";

    Vector2 posInicio;
    bool ocupado;
    bool esperandoCampamento;   // con leña y comida: esperando la mano derecha
    bool finTerminado;          // pantalla negra final: la mano derecha vuelve al inicio
    CanvasGroup avisoFinal;

    IEnumerator Start()
    {
        posInicio = lucas.anchoredPosition;
        if (mensaje != null) mensaje.alpha = 0f;
        ActualizarTextos();

        if (EstadoJuego.LenaCompleta && EstadoJuego.ComidaCompleta)
        {
            ocupado = true;
            if (flechaIzquierda) flechaIzquierda.gameObject.SetActive(false);
            if (flechaDerecha) flechaDerecha.gameObject.SetActive(false);
            yield return MostrarMensaje("¡Tenemos leña y comida!\nYa podemos volver al campamento.", 3f);
            CrearAvisoFinal();
            Sonido.Efecto("Aparecer", 0.6f, 0f);
            yield return Fundido(avisoFinal, 0f, 1f, 0.3f);
            esperandoCampamento = true;
            ocupado = false;
        }
    }

    void ActualizarTextos()
    {
        if (textoLena != null) textoLena.text = EstadoJuego.Lena + "/" + EstadoJuego.MetaLena;
        if (textoPeces != null) textoPeces.text = EstadoJuego.Peces + "/" + EstadoJuego.MetaPeces;
        if (textoObjetivo == null) return;

        if (!EstadoJuego.LenaCompleta) textoObjetivo.text = "Ve por la leña";
        else if (!EstadoJuego.ComidaCompleta) textoObjetivo.text = "Ve por la comida";
        else textoObjetivo.text = "Vuelve al campamento";

        if (textoAyuda != null)
            textoAyuda.text = (EstadoJuego.LenaCompleta && EstadoJuego.ComidaCompleta)
                ? "Levanta la mano derecha para ir al campamento"
                : "Inclínate a izquierda o derecha para elegir camino";
    }

    void Update()
    {
        float t = Time.time;
        if (flechaIzquierda) flechaIzquierda.localScale = Vector3.one * (1f + Mathf.Sin(t * 3f) * 0.08f);
        if (flechaDerecha) flechaDerecha.localScale = Vector3.one * (1f + Mathf.Sin(t * 3f + 1.5f) * 0.08f);

#if UNITY_EDITOR
        // Atajo de prueba (solo en el editor): F9 = como si ya tuviera leña y comida
        if (Input.GetKeyDown(KeyCode.F9))
        {
            EstadoJuego.Lena = EstadoJuego.MetaLena; EstadoJuego.Peces = EstadoJuego.MetaPeces;
            EstadoJuego.LenaCompleta = EstadoJuego.ComidaCompleta = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }
#endif
        if (finTerminado)
        {
            if (EntradaJugador.Accion)
            {
                Sonido.Efecto("Boton");
                bool hay = !string.IsNullOrEmpty(escenaSiguienteNivel) && Application.CanStreamedLevelBeLoaded(escenaSiguienteNivel);
                Transicion.Cargar(hay ? escenaSiguienteNivel : escenaInicio);
            }
            return;
        }
        if (ocupado) return;
        lucas.anchoredPosition = posInicio + Vector2.up * Mathf.Sin(t * 1.6f) * 8f;

        if (esperandoCampamento)
        {
            if (avisoFinal != null) avisoFinal.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 3f) * 0.03f);
            if (EntradaJugador.Accion) StartCoroutine(IrAlCampamento());
            return;
        }

        if (EntradaJugador.Izquierda) StartCoroutine(IrPorCamino(true));
        else if (EntradaJugador.Derecha) StartCoroutine(IrPorCamino(false));
    }

    // ------------------------------------------------------------ Final: regreso al campamento

    /// <summary>Tarjeta encima de Lucas: mano derecha + "Levanta la mano derecha para ir al campamento".</summary>
    void CrearAvisoFinal()
    {
        var canvas = (RectTransform)lucas.parent;
        var tarjeta = textoObjetivo != null ? textoObjetivo.transform.parent.GetComponent<Image>() : null;

        var go = new GameObject("AvisoCampamento", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 130f);
        rt.anchoredPosition = new Vector2(0f, -10f);
        var fondo = go.GetComponent<Image>();
        if (tarjeta != null) { fondo.sprite = tarjeta.sprite; fondo.type = Image.Type.Sliced; }
        fondo.raycastTarget = false;
        avisoFinal = go.GetComponent<CanvasGroup>();
        avisoFinal.alpha = 0f;

        // mano derecha: mitad derecha del dibujo de las dos manos abiertas
        var tex = Resources.Load<Texture2D>("Gestos/ManosAbiertas");
        if (tex != null)
        {
            var mano = new GameObject("Mano", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var mr = mano.GetComponent<RectTransform>();
            mr.SetParent(rt, false);
            mr.anchorMin = mr.anchorMax = new Vector2(0f, 0.5f);
            mr.anchoredPosition = new Vector2(70f, 0f);
            mr.sizeDelta = new Vector2(80f, 110f);
            var img = mano.GetComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(tex.width / 2f, 0, tex.width / 2f, tex.height), new Vector2(0.5f, 0.5f), 100f);
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        Texto(rt, "Texto", textoMensaje.font, "Levanta la mano derecha\npara ir al campamento", 34,
            new Color32(0x36, 0x5F, 0x73, 255), new Vector2(40f, 0f), new Vector2(540f, 84f));
    }

    static TextMeshProUGUI Texto(RectTransform padre, string nombre, TMP_FontAsset fuente, string texto, float tam, Color color, Vector2 pos, Vector2 tamano)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = fuente;
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = tam * 0.6f; t.fontSizeMax = tam;
        t.raycastTarget = false;
        return t;
    }

    IEnumerator IrAlCampamento()
    {
        esperandoCampamento = false;
        ocupado = true;
        Sonido.Efecto("Boton");
        if (avisoFinal != null) yield return Fundido(avisoFinal, 1f, 0f, 0.25f);
        if (textoAyuda != null) textoAyuda.text = "Lucas regresa al campamento...";

        // Lucas sube por el centro del camino hacia la montaña, se hace pequeño y se desvanece
        var grupo = lucas.GetComponent<CanvasGroup>();
        if (grupo == null) grupo = lucas.gameObject.AddComponent<CanvasGroup>();
        Vector2 desde = lucas.anchoredPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duracionSubida;
            float k = Mathf.SmoothStep(0f, 1f, t);
            float paso = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 14f)) * 12f * (1f - k);
            lucas.anchoredPosition = Vector2.Lerp(desde, puntoMontana, k) + Vector2.up * paso;
            // se encoge más rápido al principio (perspectiva)
            float s = Mathf.Lerp(1f, escalaMontana, 1f - Mathf.Pow(1f - k, 1.6f));
            lucas.localScale = new Vector3(s, s, 1f);
            lucas.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 14f) * 3f * (1f - k));
            float desvanecer = Mathf.InverseLerp(0.55f, 1f, t);
            grupo.alpha = 1f - desvanecer;
            if (Linterna.Instancia != null) Linterna.Instancia.intensidad = 1f - desvanecer;
            yield return null;
        }
        grupo.alpha = 0f;

        // fundido a negro y mensaje final
        Sonido.Efecto("Alejarse", 0.7f, 0f);
        Sonido.Bajar(0f, 2f);
        var negro = PantallaNegra();
        yield return Fundido(negro, 0f, 1f, 1.2f);
        yield return new WaitForSeconds(0.4f);

        var textos = negro.transform;
        var t1 = Texto((RectTransform)textos, "Titulo", textoMensaje.font, "¡Ya casi llegamos!", 72,
            new Color32(0xF4, 0xC9, 0x5D, 255), new Vector2(0f, 120f), new Vector2(1400f, 100f));
        var t2 = Texto((RectTransform)textos, "Mensaje", textoMensaje.font,
            "Acompaña a Lucas a llegar al campamento.\nVe a la siguiente sección.", 46,
            new Color32(0xE6, 0xEC, 0xF5, 255), new Vector2(0f, -10f), new Vector2(1400f, 150f));
        var g1 = t1.gameObject.AddComponent<CanvasGroup>();
        var g2 = t2.gameObject.AddComponent<CanvasGroup>();
        g1.alpha = g2.alpha = 0f;
        Sonido.Efecto("Final", 1f, 0f);
        yield return Fundido(g1, 0f, 1f, 0.8f);
        yield return new WaitForSeconds(0.5f);
        yield return Fundido(g2, 0f, 1f, 0.8f);
        yield return new WaitForSeconds(3f);

        var t3 = Texto((RectTransform)textos, "Fin", textoAyuda != null ? textoAyuda.font : textoMensaje.font,
            "Levanta la mano derecha para ir al siguiente nivel", 26,
            new Color32(0x87, 0x91, 0x88, 255), new Vector2(0f, -180f), new Vector2(1400f, 40f));
        var g3 = t3.gameObject.AddComponent<CanvasGroup>();
        yield return Fundido(g3, 0f, 1f, 0.6f);
        finTerminado = true;
    }

    CanvasGroup PantallaNegra()
    {
        var canvas = (RectTransform)lucas.parent;
        var go = new GameObject("FundidoNegro", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;
        var g = go.GetComponent<CanvasGroup>();
        g.alpha = 0f;
        return g;
    }

    IEnumerator IrPorCamino(bool izquierda)
    {
        ocupado = true;
        var destino = (izquierda ? destinoIzquierda : destinoDerecha).anchoredPosition;
        yield return Caminar(posInicio, destino, 1f, escalaLejos);

        bool caminoLena = izquierda;
        if (caminoLena && !EstadoJuego.LenaCompleta)
        {
            Transicion.Cargar(escenaCaminoLena);
            yield break;
        }
        if (!caminoLena && EstadoJuego.LenaCompleta && !EstadoJuego.ComidaCompleta)
        {
            Transicion.Cargar(escenaPesca);
            yield break;
        }

        string texto;
        if (!EstadoJuego.LenaCompleta)
            texto = "Mmm... por aquí no hay leña.\n¡Probemos el otro camino!";
        else if (EstadoJuego.ComidaCompleta)
            texto = "¡Ya tenemos leña y comida!\nEs hora de volver al campamento.";
        else
            texto = "Ya recogimos la leña.\nAhora busquemos comida.";

        yield return MostrarMensaje(texto, 2.4f);
        yield return Caminar(destino, posInicio, escalaLejos, 1f);
        ocupado = false;
    }

    IEnumerator Caminar(Vector2 desde, Vector2 hasta, float escalaDesde, float escalaHasta)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duracionCaminata;
            float k = Mathf.SmoothStep(0f, 1f, t);
            float paso = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f)) * 14f;
            lucas.anchoredPosition = Vector2.Lerp(desde, hasta, k) + Vector2.up * paso;
            float s = Mathf.Lerp(escalaDesde, escalaHasta, k);
            lucas.localScale = new Vector3(s, s, 1f);
            lucas.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 6f) * 4f);
            yield return null;
        }
        lucas.localRotation = Quaternion.identity;
    }

    IEnumerator MostrarMensaje(string texto, float segundos)
    {
        if (mensaje == null) yield break;
        textoMensaje.text = texto;
        Sonido.Efecto("Aviso", 0.6f, 0f);
        yield return Fundido(mensaje, 0f, 1f, 0.25f);
        yield return new WaitForSeconds(segundos);
        yield return Fundido(mensaje, 1f, 0f, 0.25f);
    }

    public static IEnumerator Fundido(CanvasGroup g, float a, float b, float d)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / d;
            g.alpha = Mathf.Lerp(a, b, t);
            yield return null;
        }
        g.alpha = b;
    }
}
