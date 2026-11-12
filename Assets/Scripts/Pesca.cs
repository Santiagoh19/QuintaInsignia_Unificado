using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Lago de la pesca:
/// 1) Lucas recoge el palo y el hilo, y arma una caña (animación).
/// 2) Camina a la orilla y lanza la caña con B (luego: levantar las manos al frente).
/// 3) Cuando el pez pica, debe mantener C (luego: cerrar los puños) hasta llenar el círculo
///    antes de que se acabe el tiempo. Si no, el pez escapa y pierde un corazón.
/// 4) Con la meta de peces, vuelve a la bifurcación. Sin corazones, se reinicia el nivel.
/// El pez pica en un momento aleatorio (0–5 s) y una tarjeta muestra el tiempo de reacción.
/// </summary>
public class Pesca : MonoBehaviour
{
    enum Estado { Recolectando, Armando, Listo, Lanzado, Pica, Resultado, Fin }

    [Header("Escenas")]
    public string escenaBifurcacion = "SampleScene";

    [Header("Mundo")]
    public RectTransform mundo;
    public RectTransform lucas;
    public RectTransform palo;
    public RectTransform hilo;
    public RectTransform cana;          // hijo de Lucas, inactivo al inicio
    public RectTransform puntaCana;     // hijo de la caña
    public RectTransform linea;         // imagen fina, pivote a la izquierda
    public RectTransform boya;
    public RectTransform pez;
    public RectTransform salpicadura;
    public RectTransform destello;
    public TextMeshProUGUI signoPica;   // "!"

    [Header("Interfaz")]
    public Image slotPalo;
    public Image slotHilo;
    public CanvasGroup panelTecla;
    public TextMeshProUGUI letraTecla;
    public TextMeshProUGUI textoTecla;
    public CanvasGroup panelRecoger;
    public Image anilloRecoger;          // relleno radial: cuánto llevas recogiendo
    public RectTransform barraTiempo;    // relleno horizontal: tiempo restante
    public TextMeshProUGUI textoObjetivo;
    public TextMeshProUGUI textoAyuda;
    public TextMeshProUGUI textoContador;
    public RectTransform iconoContador;
    public CanvasGroup mensaje;
    public TextMeshProUGUI textoMensaje;
    public Vidas vidas;

    [Header("Ajustes")]
    public float velocidad = 520f;
    public float limiteIzq = -840f;
    public float orillaX = -230f;        // hasta donde llega Lucas (borde del lago)
    public float distanciaRecoger = 110f;
    public Vector2 puntoAgua = new Vector2(470f, -300f);
    [Tooltip("El pez pica en un tiempo aleatorio entre estos dos valores (segundos)")]
    public Vector2 esperaAleatoria = new Vector2(0f, 5f);
    public float tiempoParaRecoger = 4f;   // ventana total cuando pica
    public float segundosManteniendo = 1.6f; // cuánto hay que mantener C

    Estado estado = Estado.Recolectando;
    bool tienePalo, tieneHilo;
    float baseY, tiempoPica, llevado;
    bool necesitaSoltar;

    // Tiempo de reacción
    float inicioPica;
    bool reaccionMedida;
    float ultimaReaccion = -1f, mejorReaccion = -1f;
    readonly List<float> historial = new List<float>();
    CanvasGroup panelReaccion;

    // Íconos de gestos (manos abiertas / puños) en lugar de las letras B y C
    Sprite spriteManos, spritePunos;
    Image teclaHud, gestoHud;
    TextMeshProUGUI pistaTecla;
    TextMeshProUGUI textoReaccion, comentarioReaccion, detalleReaccion;
    Coroutine rutinaReaccion;

    void Start()
    {
        // Dificultad (los valores del Inspector son los de Normal); se reajusta si cambia desde la pausa
        baseParaRecoger = tiempoParaRecoger; baseManteniendo = segundosManteniendo;
        AplicarDificultad();
        EstadoJuego.DificultadCambiada += AplicarDificultad;

        baseY = lucas.anchoredPosition.y;
        EstadoJuego.Peces = 0;
        cana.gameObject.SetActive(false);
        linea.gameObject.SetActive(false);
        boya.gameObject.SetActive(false);
        pez.gameObject.SetActive(false);
        salpicadura.gameObject.SetActive(false);
        destello.gameObject.SetActive(false);
        signoPica.gameObject.SetActive(false);
        panelRecoger.alpha = 0f;
        panelTecla.alpha = 0f;
        mensaje.alpha = 0f;
        slotPalo.color = slotHilo.color = new Color(1, 1, 1, 0.3f);
        textoObjetivo.text = "Recoge el palo y el hilo";
        Ayuda("Inclínate o muévete a izquierda o derecha para avanzar");
        ActualizarContador(false);

        // Noche: la linterna apunta a la boya cuando está en el agua,
        // y el círculo de recoger queda por encima de la oscuridad.
        Linterna.Objetivo = boya;
        panelRecoger.transform.SetParent(mundo.parent, true);
        panelRecoger.transform.SetAsLastSibling();
        CrearPanelReaccion();
        PrepararGestos();
    }

    float baseParaRecoger, baseManteniendo;

    void AplicarDificultad()
    {
        tiempoParaRecoger   = baseParaRecoger * EstadoJuego.SegunDificultad(1.5f, 1f, 0.75f);   // 6 / 4 / 3 s para atrapar
        segundosManteniendo = baseManteniendo * EstadoJuego.SegunDificultad(0.625f, 1f, 1.25f); // 1.0 / 1.6 / 2.0 s con los puños cerrados
        tiempoPica = Mathf.Min(tiempoPica, tiempoParaRecoger);
    }

    void OnDestroy() => EstadoJuego.DificultadCambiada -= AplicarDificultad;

    void Update()
    {
        float t = Time.time;

        // Palo e hilo flotan para llamar la atención
        if (!tienePalo) palo.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 3f) * 6f);
        if (!tieneHilo) hilo.localScale = Vector3.one * (1f + Mathf.Sin(t * 3f) * 0.06f);

        if (panelTecla.alpha > 0f) panelTecla.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 4f) * 0.03f);

        switch (estado)
        {
            case Estado.Recolectando:
                Caminar();
                if (!tienePalo && Cerca(palo)) StartCoroutine(Recoger(palo, slotPalo, true));
                else if (!tieneHilo && Cerca(hilo)) StartCoroutine(Recoger(hilo, slotHilo, false));
                break;

            case Estado.Listo:
                Caminar();
                bool enOrilla = lucas.anchoredPosition.x >= orillaX - 30f;
                MostrarTecla(enOrilla ? "B" : "»", enOrilla ? "Levanta las manos para lanzar la caña" : "Muévete a la derecha para llegar a la orilla");
                if (enOrilla && EntradaJugador.Lanzar) StartCoroutine(Lanzar());
                break;

            case Estado.Lanzado:
                boya.anchoredPosition = puntoAgua + Vector2.up * Mathf.Sin(t * 2.5f) * 5f;
                break;

            case Estado.Pica:
                ActualizarPica();
                break;

            case Estado.Fin:
                break;   // la ventana de la insignia se encarga de continuar
        }

        DibujarLinea();
    }

    // ------------------------------------------------------------ Movimiento

    void Caminar()
    {
        float h = EntradaJugador.Horizontal;
        var p = lucas.anchoredPosition;
        p.x = Mathf.Clamp(p.x + h * velocidad * Time.deltaTime, limiteIzq, orillaX);
        p.y = baseY + (h != 0f ? Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 10f : 0f);
        lucas.anchoredPosition = p;
        lucas.localRotation = Quaternion.Euler(0, 0, h != 0f ? Mathf.Sin(Time.time * 12f) * 3f : 0f);
    }

    void Quieto()
    {
        var p = lucas.anchoredPosition; p.y = baseY;
        lucas.anchoredPosition = p;
        lucas.localRotation = Quaternion.identity;
    }

    bool Cerca(RectTransform rt) => Mathf.Abs(rt.anchoredPosition.x - lucas.anchoredPosition.x) < distanciaRecoger;

    // ------------------------------------------------------------ Armar la caña

    IEnumerator Recoger(RectTransform objeto, Image slot, bool esPalo)
    {
        estado = Estado.Armando;
        Quieto();
        if (esPalo) tienePalo = true; else tieneHilo = true;
        Sonido.Efecto(esPalo ? "Recoger" : "Tela");

        // vuela hasta su casilla en la tarjeta de materiales
        Vector2 desde = objeto.position;
        Vector2 hasta = slot.rectTransform.position;
        Vector3 escala = objeto.localScale;
        for (float k = 0; k < 1f; k += Time.deltaTime / 0.6f)
        {
            var pos = Vector2.Lerp(desde, hasta, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 200f * mundo.lossyScale.y;
            objeto.position = pos;
            objeto.localScale = escala * Mathf.Lerp(1f, 0.4f, k);
            yield return null;
        }
        objeto.gameObject.SetActive(false);
        slot.color = Color.white;
        Sonido.Efecto("Punto", 0.8f);
        yield return Pop(slot.rectTransform, 0.4f);

        if (tienePalo && tieneHilo)
        {
            yield return ArmarCana();
            estado = Estado.Listo;
        }
        else
        {
            textoObjetivo.text = tienePalo ? "Ahora recoge el hilo" : "Ahora recoge el palo";
            estado = Estado.Recolectando;
        }
    }

    IEnumerator ArmarCana()
    {
        textoObjetivo.text = "Armando la caña...";
        Sonido.Efecto("ArmarCana", 0.8f, 0f);
        // copias de los íconos giran sobre Lucas y se juntan
        var a = Copia(slotPalo);
        var b = Copia(slotHilo);
        Vector2 centro = lucas.anchoredPosition + new Vector2(0, 260f);
        Vector2 da = a.anchoredPosition, db = b.anchoredPosition;

        for (float k = 0; k < 1f; k += Time.deltaTime / 0.6f)
        {
            a.anchoredPosition = Vector2.Lerp(da, centro + new Vector2(-110, 0), k);
            b.anchoredPosition = Vector2.Lerp(db, centro + new Vector2(110, 0), k);
            a.localScale = b.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.2f, k);
            yield return null;
        }
        for (float k = 0; k < 1f; k += Time.deltaTime / 1.1f)
        {
            float ang = k * Mathf.PI * 4f;
            float r = Mathf.Lerp(110f, 0f, k * k);
            a.anchoredPosition = centro + new Vector2(Mathf.Cos(ang + Mathf.PI), Mathf.Sin(ang + Mathf.PI)) * r;
            b.anchoredPosition = centro + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            a.localRotation = b.localRotation = Quaternion.Euler(0, 0, k * 720f);
            yield return null;
        }
        Destroy(a.gameObject);
        Destroy(b.gameObject);

        // destello y aparece la caña
        Sonido.Efecto("Destello", 0.9f, 0f);
        destello.anchoredPosition = centro;
        destello.gameObject.SetActive(true);
        var canaVolando = Copia(cana.GetComponent<Image>());
        canaVolando.anchoredPosition = centro;
        for (float k = 0; k < 1f; k += Time.deltaTime / 0.5f)
        {
            destello.localScale = Vector3.one * Mathf.Sin(k * Mathf.PI) * 1.4f;
            destello.localRotation = Quaternion.Euler(0, 0, k * 120f);
            canaVolando.localScale = Vector3.one * Mathf.Lerp(0f, 1.3f, k);
            yield return null;
        }
        destello.gameObject.SetActive(false);
        textoMensaje.text = "¡Armaste una caña de pescar!";
        Sonido.Efecto("Materiales", 0.9f, 0f);
        yield return Bifurcacion.Fundido(mensaje, 0f, 1f, 0.25f);
        yield return new WaitForSeconds(1.2f);
        yield return Bifurcacion.Fundido(mensaje, 1f, 0f, 0.25f);

        // la caña baja a la mano de Lucas
        Vector2 mano = (Vector2)mundo.InverseTransformPoint(cana.position);
        Vector2 inicio = canaVolando.anchoredPosition;
        for (float k = 0; k < 1f; k += Time.deltaTime / 0.4f)
        {
            canaVolando.anchoredPosition = Vector2.Lerp(inicio, mano, k);
            canaVolando.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, k);
            yield return null;
        }
        Destroy(canaVolando.gameObject);
        cana.gameObject.SetActive(true);
        textoObjetivo.text = "Pesca " + EstadoJuego.MetaPeces + " peces";
        Ayuda("En la orilla, levanta las manos al frente para lanzar");
    }

    void Ayuda(string t) { if (textoAyuda != null) textoAyuda.text = t; }

    RectTransform Copia(Image origen)
    {
        var go = new GameObject("Copia", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(mundo, false);
        var img = go.GetComponent<Image>();
        img.sprite = origen.sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = origen.rectTransform.rect.size;
        rt.anchoredPosition = mundo.InverseTransformPoint(origen.rectTransform.position);
        return rt;
    }

    // ------------------------------------------------------------ Pescar

    IEnumerator Lanzar()
    {
        estado = Estado.Resultado;
        Quieto();
        OcultarTecla();
        textoObjetivo.text = "Espera a que pique...";
        Ayuda("Cuando aparezca el ! cierra los puños");

        // echar la caña hacia atrás y adelante
        yield return GirarCana(0f, 35f, 0.2f);
        boya.gameObject.SetActive(true);
        linea.gameObject.SetActive(true);
        Vector2 desde = Punta();
        StartCoroutine(GirarCana(35f, -15f, 0.15f));
        Sonido.Efecto("Lanzar");
        for (float k = 0; k < 1f; k += Time.deltaTime / 0.7f)
        {
            boya.anchoredPosition = Vector2.Lerp(desde, puntoAgua, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 260f;
            yield return null;
        }
        boya.anchoredPosition = puntoAgua;
        yield return Salpicar(puntoAgua);
        StartCoroutine(GirarCana(-15f, 0f, 0.3f));

        estado = Estado.Lanzado;
        yield return new WaitForSeconds(Random.Range(esperaAleatoria.x, esperaAleatoria.y));

        // ¡pica!
        estado = Estado.Pica;
        tiempoPica = tiempoParaRecoger;
        llevado = 0f;
        inicioPica = Time.time;
        reaccionMedida = false;
        necesitaSoltar = EntradaJugador.RecogerMantenido; // hay que reaccionar cuando pica
        Ayuda("Mantén los puños cerrados hasta llenar el círculo");
        signoPica.gameObject.SetActive(true);
        panelRecoger.alpha = 1f;
        textoObjetivo.text = "¡Picó un pez!";
        Sonido.Efecto("Pica", 1f, 0f);
        Sonido.Efecto("Gota", 0.6f);
        MostrarTecla("C", "¡Cierra los puños para recoger!");
    }

    void ActualizarPica()
    {
        float t = Time.time;
        bool manteniendo = EntradaJugador.RecogerMantenido;
        if (necesitaSoltar)
        {
            if (!manteniendo) necesitaSoltar = false;
            manteniendo = false;
        }
        if (manteniendo && !reaccionMedida)
        {
            reaccionMedida = true;
            MostrarReaccion(Time.time - inicioPica);
        }
        Sonido.Carrete(manteniendo);
        tiempoPica -= Time.deltaTime;
        llevado = manteniendo ? llevado + Time.deltaTime : Mathf.Max(0f, llevado - Time.deltaTime * 0.5f);

        boya.anchoredPosition = puntoAgua + new Vector2(Mathf.Sin(t * 23f) * 6f, -Mathf.Abs(Mathf.Sin(t * 14f)) * 22f);
        signoPica.rectTransform.anchoredPosition = puntoAgua + new Vector2(0, 110f + Mathf.Sin(t * 10f) * 8f);
        signoPica.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 12f) * 0.15f);
        cana.localRotation = Quaternion.Euler(0, 0, manteniendo ? 10f + Mathf.Sin(t * 30f) * 4f : Mathf.Sin(t * 18f) * 3f);

        anilloRecoger.fillAmount = Mathf.Clamp01(llevado / segundosManteniendo);
        barraTiempo.anchorMax = new Vector2(Mathf.Clamp01(tiempoPica / tiempoParaRecoger), 1f);

        if (llevado >= segundosManteniendo) StartCoroutine(Capturar());
        else if (tiempoPica <= 0f) StartCoroutine(Escapa());
    }

    IEnumerator Capturar()
    {
        estado = Estado.Resultado;
        TerminarPica();

        // el pez salta del agua hacia Lucas
        pez.gameObject.SetActive(true);
        StartCoroutine(Salpicar(puntoAgua));
        Vector2 hasta = lucas.anchoredPosition + new Vector2(60f, 120f);
        Sonido.Efecto("PezAtrapado", 0.9f, 0f);
        yield return Saltar(pez, puntoAgua, hasta, 320f, 0.8f, 1f, 1.1f);
        yield return RecogerBoya();

        EstadoJuego.Peces++;
        ActualizarContador(true);

        // el pez vuela al contador
        Vector2 icono = mundo.InverseTransformPoint(iconoContador.position);
        yield return Saltar(pez, hasta, icono, 120f, 0.5f, 1.1f, 0.4f);
        pez.gameObject.SetActive(false);

        if (EstadoJuego.Peces >= EstadoJuego.MetaPeces)
        {
            estado = Estado.Fin;
            EstadoJuego.ComidaCompleta = true;
            textoObjetivo.text = "¡Comida conseguida!";
            Sonido.Efecto("MisionCompleta");
            textoMensaje.text = "¡Pescaste " + EstadoJuego.MetaPeces + " peces!\n¡Ya tenemos comida!";
            yield return Bifurcacion.Fundido(mensaje, 0f, 1f, 0.3f);
            yield return new WaitForSeconds(2.2f);
            yield return Bifurcacion.Fundido(mensaje, 1f, 0f, 0.3f);
            // Con leña y comida, el nivel está completo: ventana de la insignia y luego vuelve al camino
            VentanaInsignia.Mostrar("Insignias/Insignia3", "¡Has ganado la insignia del aventurero!",
                "¡Muy bien! Esta es tu <b>tercera insignia</b>.\nVe por las otras dos.",
                () => Transicion.Cargar(escenaBifurcacion));
            yield break;
        }

        yield return Aviso("¡Pescaste un pez!", 1f);
        textoObjetivo.text = "Pesca " + EstadoJuego.MetaPeces + " peces";
        estado = Estado.Listo;
    }

    IEnumerator Escapa()
    {
        estado = Estado.Resultado;
        TerminarPica();
        if (!reaccionMedida) MostrarReaccion(-1f);

        // el pez salta y se va
        pez.gameObject.SetActive(true);
        pez.localScale = new Vector3(-1f, 1f, 1f);
        Sonido.Efecto("PezEscapa", 0.9f, 0f);
        StartCoroutine(Salpicar(puntoAgua));
        yield return Saltar(pez, puntoAgua, puntoAgua + new Vector2(300f, -40f), 220f, 0.7f, 1f, 0.8f);
        pez.gameObject.SetActive(false);
        pez.localScale = Vector3.one;
        StartCoroutine(Salpicar(puntoAgua + new Vector2(300f, -40f)));
        yield return RecogerBoya();

        int quedan = vidas != null ? vidas.Perder() : 1;
        if (quedan <= 0)
        {
            estado = Estado.Fin;
            Sonido.Efecto("SinVidas");
            textoMensaje.text = "¡Oh no! Te quedaste sin vidas.\nIntentémoslo otra vez.";
            yield return Bifurcacion.Fundido(mensaje, 0f, 1f, 0.3f);
            yield return new WaitForSeconds(2.2f);
            Transicion.Recargar();
            yield break;
        }

        yield return Aviso("¡Se escapó!\nCierra los puños más rápido.", 1.4f);
        textoObjetivo.text = "Pesca " + EstadoJuego.MetaPeces + " peces";
        estado = Estado.Listo;
    }

    void TerminarPica()
    {
        Sonido.Carrete(false);
        signoPica.gameObject.SetActive(false);
        panelRecoger.alpha = 0f;
        OcultarTecla();
        cana.localRotation = Quaternion.identity;
    }

    IEnumerator RecogerBoya()
    {
        Vector2 desde = boya.anchoredPosition;
        for (float k = 0; k < 1f; k += Time.deltaTime / 0.4f)
        {
            boya.anchoredPosition = Vector2.Lerp(desde, Punta(), k);
            yield return null;
        }
        boya.gameObject.SetActive(false);
        linea.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ Tiempo de reacción

    void CrearPanelReaccion()
    {
        var canvas = (RectTransform)mundo.parent;
        var tarjeta = slotPalo.transform.parent.GetComponent<Image>();

        var go = new GameObject("PanelReaccion", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);   // abajo, sobre el lago (no choca con los avisos)
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(200f, 40f);
        rt.sizeDelta = new Vector2(600f, 170f);
        var fondo = go.GetComponent<Image>();
        fondo.sprite = tarjeta != null ? tarjeta.sprite : null;
        fondo.type = Image.Type.Sliced;
        fondo.raycastTarget = false;
        panelReaccion = go.GetComponent<CanvasGroup>();
        panelReaccion.alpha = 0f;
        panelReaccion.blocksRaycasts = false;

        var titulo = textoMensaje.font;
        var cuerpo = textoAyuda != null ? textoAyuda.font : titulo;
        TextoPanel(rt, "Etiqueta", cuerpo, "Tiempo de reacción", 24, new Color32(0x87, 0x91, 0x88, 255), new Vector2(0, 56), new Vector2(540, 34));
        textoReaccion = TextoPanel(rt, "Tiempo", titulo, "0.00 s", 56, new Color32(0x18, 0x39, 0x2B, 255), new Vector2(0, 12), new Vector2(540, 64));
        comentarioReaccion = TextoPanel(rt, "Comentario", titulo, "", 28, new Color32(0x36, 0x5F, 0x73, 255), new Vector2(0, -38), new Vector2(560, 36));
        detalleReaccion = TextoPanel(rt, "Detalle", cuerpo, "", 22, new Color32(0x87, 0x91, 0x88, 255), new Vector2(0, -68), new Vector2(560, 28));
    }

    TextMeshProUGUI TextoPanel(RectTransform padre, string nombre, TMP_FontAsset fuente, string texto, float tam, Color color, Vector2 pos, Vector2 tamano)
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
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true; t.fontSizeMin = tam * 0.6f; t.fontSizeMax = tam;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>segundos &lt; 0 = no alcanzó a reaccionar.</summary>
    void MostrarReaccion(float segundos)
    {
        if (panelReaccion == null) return;
        string comentario, detalle = "";
        Color color;

        if (segundos < 0f)
        {
            textoReaccion.text = "—";
            color = new Color32(0xEF, 0x83, 0x54, 255);
            comentario = "¡Uy! No alcanzaste a cerrar los puños";
            if (mejorReaccion > 0f) detalle = "Tu mejor tiempo: " + mejorReaccion.ToString("0.00") + " s";
        }
        else
        {
            textoReaccion.text = segundos.ToString("0.00") + " s";
            if (segundos < 0.5f) { comentario = "¡Rapidísimo!"; color = new Color32(0x4F, 0x8A, 0x5B, 255); }
            else if (segundos < 0.9f) { comentario = "¡Muy rápido!"; color = new Color32(0x4F, 0x8A, 0x5B, 255); }
            else if (segundos < 1.5f) { comentario = "¡Bien hecho!"; color = new Color32(0x4B, 0x91, 0xB5, 255); }
            else if (segundos < 2.5f) { comentario = "Puedes ser más rápido"; color = new Color32(0xD9, 0xA4, 0x2B, 255); }
            else { comentario = "Uy, esta vez tardaste"; color = new Color32(0xEF, 0x83, 0x54, 255); }

            bool record = mejorReaccion > 0f && segundos < mejorReaccion;
            if (ultimaReaccion >= 0f)
            {
                float dif = segundos - ultimaReaccion;
                if (dif < -0.05f) comentario = "¡Uf, mejoraste " + (-dif).ToString("0.00") + " s!";
                else if (dif > 0.05f) comentario += " · " + dif.ToString("0.00") + " s más lento";
                else comentario += " · igual que antes";
            }
            if (record) comentario = "¡Nuevo récord! " + comentario;

            ultimaReaccion = segundos;
            if (mejorReaccion < 0f || segundos < mejorReaccion) mejorReaccion = segundos;
            historial.Add(segundos);

            // últimos intentos: 1.20 → 0.85 → 0.42
            int desde = Mathf.Max(0, historial.Count - 4);
            var partes = new List<string>();
            for (int i = desde; i < historial.Count; i++) partes.Add(historial[i].ToString("0.00"));
            detalle = (historial.Count > 1 ? "Intentos: " + string.Join("  →  ", partes) + "   ·   " : "") +
                      "Mejor: " + mejorReaccion.ToString("0.00") + " s";
        }

        textoReaccion.color = color;
        comentarioReaccion.text = comentario;
        detalleReaccion.text = detalle;
        if (rutinaReaccion != null) StopCoroutine(rutinaReaccion);
        rutinaReaccion = StartCoroutine(AnimarReaccion());
        if (segundos >= 0f) Sonido.Efecto(comentario.StartsWith("¡Nuevo récord") ? "Record" : "Reaccion", 0.55f, 0f);
    }

    IEnumerator AnimarReaccion()
    {
        var rt = (RectTransform)panelReaccion.transform;
        rt.SetAsLastSibling();
        for (float k = 0; k < 1f; k += Time.deltaTime / 0.25f)
        {
            panelReaccion.alpha = k;
            rt.localScale = Vector3.one * (0.8f + Mathf.Sin(k * Mathf.PI * 0.5f) * 0.2f + Mathf.Sin(k * Mathf.PI) * 0.08f);
            yield return null;
        }
        panelReaccion.alpha = 1f;
        rt.localScale = Vector3.one;
        yield return new WaitForSeconds(3f);
        for (float k = 1f; k > 0f; k -= Time.deltaTime / 0.4f)
        {
            panelReaccion.alpha = k;
            yield return null;
        }
        panelReaccion.alpha = 0f;
        rutinaReaccion = null;
    }

    // ------------------------------------------------------------ Utilidades visuales

    Vector2 Punta() => mundo.InverseTransformPoint(puntaCana.position);

    void DibujarLinea()
    {
        if (!linea.gameObject.activeSelf) return;
        Vector2 a = Punta();
        Vector2 b = boya.anchoredPosition + new Vector2(0, 30f);
        Vector2 d = b - a;
        linea.anchoredPosition = a;
        linea.sizeDelta = new Vector2(d.magnitude, 4f);
        linea.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    IEnumerator GirarCana(float a, float b, float d)
    {
        for (float k = 0; k < 1f; k += Time.deltaTime / d)
        {
            cana.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(a, b, k));
            yield return null;
        }
        cana.localRotation = Quaternion.Euler(0, 0, b);
    }

    IEnumerator Saltar(RectTransform rt, Vector2 desde, Vector2 hasta, float altura, float d, float s0, float s1)
    {
        for (float k = 0; k < 1f; k += Time.deltaTime / d)
        {
            rt.anchoredPosition = Vector2.Lerp(desde, hasta, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * altura;
            float sx = Mathf.Sign(rt.localScale.x);
            float s = Mathf.Lerp(s0, s1, k);
            rt.localScale = new Vector3(sx * s, s, 1f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(40f, -40f, k) * sx);
            yield return null;
        }
        rt.anchoredPosition = hasta;
        rt.localRotation = Quaternion.identity;
    }

    IEnumerator Salpicar(Vector2 pos)
    {
        Sonido.Efecto("Chapoteo", 0.85f, 0.1f);
        salpicadura.gameObject.SetActive(true);
        salpicadura.anchoredPosition = pos;
        var img = salpicadura.GetComponent<Image>();
        for (float k = 0; k < 1f; k += Time.deltaTime / 0.45f)
        {
            salpicadura.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.3f, k);
            img.color = new Color(1, 1, 1, 1f - k);
            yield return null;
        }
        salpicadura.gameObject.SetActive(false);
    }

    IEnumerator Pop(RectTransform rt, float d)
    {
        for (float k = 0; k < 1f; k += Time.deltaTime / d)
        {
            rt.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.4f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator Aviso(string texto, float segundos)
    {
        textoMensaje.text = texto;
        yield return Bifurcacion.Fundido(mensaje, 0f, 1f, 0.2f);
        yield return new WaitForSeconds(segundos);
        yield return Bifurcacion.Fundido(mensaje, 1f, 0f, 0.2f);
    }

    void MostrarTecla(string letra, string texto)
    {
        panelTecla.alpha = 1f;
        letraTecla.text = letra;
        textoTecla.text = texto;

        // B = manos al frente, C = puños: se muestra el gesto y la tecla queda como pista pequeña
        Sprite gesto = letra == "B" ? spriteManos : letra == "C" ? spritePunos : null;
        if (gestoHud != null)
        {
            gestoHud.gameObject.SetActive(gesto != null);
            if (gesto != null) gestoHud.sprite = gesto;
        }
        if (teclaHud != null) teclaHud.gameObject.SetActive(gesto == null);
        if (pistaTecla != null)
            pistaTecla.text = "";   // sin pistas de teclado en pantalla
    }

    // ------------------------------------------------------------ Gestos

    static Sprite CargarGesto(string nombre)
    {
        var tex = Resources.Load<Texture2D>("Gestos/" + nombre);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Ajusta el aviso de arriba (más pequeño y más abajo) y cambia las letras por dibujos de las manos.</summary>
    void PrepararGestos()
    {
        spriteManos = CargarGesto("ManosAbiertas");
        spritePunos = CargarGesto("ManosPunos");
        var nunito = textoAyuda != null ? textoAyuda.font : textoTecla.font;

        // --- aviso de arriba: más pequeño y más abajo, sin tapar el objetivo de la derecha
        var pt = (RectTransform)panelTecla.transform;
        pt.anchoredPosition = new Vector2(-40f, -315f);
        pt.sizeDelta = new Vector2(620f, 132f);
        teclaHud = pt.Find("Tecla")?.GetComponent<Image>();
        if (teclaHud != null)
        {
            var tr = teclaHud.rectTransform;
            tr.anchoredPosition = new Vector2(70f, 0f);
            tr.sizeDelta = new Vector2(76f, 76f);
        }
        gestoHud = NuevaImagen(pt, "Gesto", new Vector2(0f, 0.5f), new Vector2(96f, 0f), new Vector2(168f, 124f));
        var tt = textoTecla.rectTransform;
        tt.offsetMin = new Vector2(190f, 40f);
        tt.offsetMax = new Vector2(-20f, -12f);
        textoTecla.fontSizeMax = 28f; textoTecla.fontSizeMin = 18f;
        pistaTecla = TextoPanel(pt, "Pista", nunito, "", 20, new Color32(0x87, 0x91, 0x88, 255), Vector2.zero, Vector2.zero);
        var pr = pistaTecla.rectTransform;
        pr.anchorMin = new Vector2(0f, 0f); pr.anchorMax = new Vector2(1f, 0f); pr.pivot = new Vector2(0.5f, 0f);
        pr.offsetMin = new Vector2(190f, 12f); pr.offsetMax = new Vector2(-20f, 40f);
        pistaTecla.alignment = TextAlignmentOptions.Left;

        // --- círculo de recoger: puños en lugar de la tecla C, y pista debajo
        var panel = (RectTransform)panelRecoger.transform;
        var teclaC = panel.Find("Tecla");
        if (teclaC != null && spritePunos != null) teclaC.gameObject.SetActive(false);
        if (spritePunos != null)
        {
            var g = NuevaImagen(panel, "Gesto", new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(128f, 94f));
            g.sprite = spritePunos;
        }
    }

    Image NuevaImagen(RectTransform padre, string nombre, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = ancla;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var img = go.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        go.SetActive(false);
        if (nombre == "Gesto" && padre == (RectTransform)panelRecoger.transform) go.SetActive(true);
        return img;
    }

    void OcultarTecla() => panelTecla.alpha = 0f;

    void ActualizarContador(bool animar)
    {
        textoContador.text = EstadoJuego.Peces + "/" + EstadoJuego.MetaPeces;
        if (animar) { StartCoroutine(Pop(iconoContador, 0.35f)); Sonido.Efecto("Punto", 0.8f); }
    }
}
