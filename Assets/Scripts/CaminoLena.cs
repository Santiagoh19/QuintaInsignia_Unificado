using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Camino hacia la leña: los troncos aparecen al fondo del sendero y ruedan hacia Lucas
/// por uno de 3 carriles (izquierda, centro, derecha). Lucas los esquiva cambiando de carril.
/// Al llenar la barra, pasa a la escena de la leña.
/// </summary>
public class CaminoLena : MonoBehaviour
{
    [Header("Escenas")]
    public string escenaLena = "Lena";

    [Header("Referencias")]
    public RectTransform mundo;
    public RectTransform lucas;
    public Image imagenLucas;
    public RectTransform barraRelleno;
    public TextMeshProUGUI textoProgreso;
    public CanvasGroup mensaje;
    public TextMeshProUGUI textoMensaje;
    public Sprite[] spritesRodando;   // cuadros de la animación de rodar
    public Sprite spriteSombra;
    public Vidas vidas;

    [Header("Carriles y perspectiva")]
    public float[] carriles = { -360f, 0f, 360f };
    public float sueloY = -390f;       // altura de los pies de Lucas
    public float horizonteY = 75f;     // donde nace el camino al fondo
    public float escalaFondo = 0.1f;   // tamaño del tronco al aparecer

    [Header("Dificultad")]
    public float duracion = 25f;            // segundos de camino sin golpes
    public float intervaloInicial = 1.7f;
    public float intervaloFinal = 1.0f;
    public float tiempoRodarInicial = 1.9f; // segundos desde el fondo hasta Lucas
    public float tiempoRodarFinal = 1.3f;
    public float penalizacion = 0f;     // progreso que se pierde al ser golpeado
    public float tiempoAturdido = 1.1f;

    [Header("Sonido de los troncos")]
    [Tooltip("Volumen máximo del golpe de un tronco cuando llega a Lucas (0..1). Al fondo suena muy bajito.")]
    [Range(0f, 1f)] public float volumenTroncos = 0.6f;
    [Tooltip("Qué tan rápido sube el volumen al acercarse (más alto = se oye más tarde)")]
    public float curvaVolumen = 1.3f;
    [Tooltip("Volumen del 'toc' cuando aparece un tronco al fondo")]
    [Range(0f, 1f)] public float volumenAparece = 0.3f;

    class Tronco
    {
        public RectTransform rt, sombra;
        public Image img, imgSombra;
        public int carril;
        public float t, duracion, cuadro;
        public bool paso;   // ya pasó la línea de Lucas
        public AudioSource audio;   // fuente propia: 'toc' al aparecer y un golpe por cada vuelta
        public float tono;
        public int vueltas;
    }

    readonly List<Tronco> troncos = new List<Tronco>();
    AudioClip[] clipsGira;
    AudioClip clipAparece;
    float sonidoTroncos = 1f;   // baja a 0 al terminar la prueba
    int carrilLucas = 1;
    int carrilCuerpoAnterior = 1;
    int carrilSonado = -1;
    int ultimoCarril = -1;
    float progreso, temporizador, aturdido;
    bool jugando, terminado;

    void Awake()
    {
        // En el camino Lucas se ve de espaldas (los troncos vienen hacia él), con la linterna en el cinturón.
        var tex = Resources.Load<Texture2D>("Personajes/LucasEspalda");
        if (tex != null && imagenLucas != null)
        {
            imagenLucas.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            imagenLucas.preserveAspect = true;
        }
        // valores del Inspector = Normal; se guardan para poder cambiar la dificultad desde la pausa
        baseDuracion = duracion; baseIntervaloInicial = intervaloInicial; baseIntervaloFinal = intervaloFinal;
        baseRodarInicial = tiempoRodarInicial; baseRodarFinal = tiempoRodarFinal;
        AplicarDificultad();
        EstadoJuego.DificultadCambiada += AplicarDificultad;

        // "100%" en una sola línea (la caja es angosta y el % se bajaba de renglón)
        if (textoProgreso != null)
        {
            textoProgreso.textWrappingMode = TextWrappingModes.NoWrap;
            textoProgreso.overflowMode = TextOverflowModes.Overflow;
            textoProgreso.enableAutoSizing = true;
            textoProgreso.fontSizeMax = textoProgreso.fontSize;
            textoProgreso.fontSizeMin = textoProgreso.fontSize * 0.6f;
        }
    }

    void OnDestroy() => EstadoJuego.DificultadCambiada -= AplicarDificultad;

    float baseDuracion, baseIntervaloInicial, baseIntervaloFinal, baseRodarInicial, baseRodarFinal;

    /// <summary>
    /// Ajusta los troncos según la dificultad elegida al inicio (los valores del Inspector son los de Normal).
    /// Fácil: troncos más espaciados y lentos, camino más corto. Difícil: más seguidos y rápidos, camino más largo.
    /// También se llama al cambiar la dificultad desde la pausa.
    /// </summary>
    void AplicarDificultad()
    {
        duracion           = baseDuracion + EstadoJuego.SegunDificultad(-10f, -5f, 0f); // 15 s / 20 s / 25 s (Inspector = 25)
        intervaloInicial   = baseIntervaloInicial * EstadoJuego.SegunDificultad(1.35f, 1f, 0.76f); // 2.3 / 1.7 / 1.3 s
        intervaloFinal     = baseIntervaloFinal * EstadoJuego.SegunDificultad(1.5f, 1f, 0.75f);    // 1.5 / 1.0 / 0.75 s
        tiempoRodarInicial = baseRodarInicial * EstadoJuego.SegunDificultad(1.3f, 1f, 0.8f);       // 2.5 / 1.9 / 1.5 s
        tiempoRodarFinal   = baseRodarFinal * EstadoJuego.SegunDificultad(1.45f, 1f, 0.77f);       // 1.9 / 1.3 / 1.0 s
    }

    IEnumerator Start()
    {
        lucas.anchoredPosition = new Vector2(carriles[carrilLucas], lucas.anchoredPosition.y);
        mensaje.alpha = 0f;
        ActualizarBarra();
        textoMensaje.text = "¡Cuidado! Vienen troncos rodando.\nMuévete a los lados para esquivarlos.";
        yield return Bifurcacion.Fundido(mensaje, 0f, 1f, 0.3f);
        yield return new WaitForSeconds(2.2f);
        yield return Bifurcacion.Fundido(mensaje, 1f, 0f, 0.3f);
        jugando = true;
    }

    void Update()
    {
        MoverLucas();
        ActualizarTroncos();
        if (!jugando || terminado) return;

        if (aturdido > 0f)
        {
            aturdido -= Time.deltaTime;
            imagenLucas.color = (Mathf.Repeat(Time.time * 10f, 1f) > 0.5f) ? new Color(1, 1, 1, 0.35f) : Color.white;
            if (aturdido <= 0f) imagenLucas.color = Color.white;
        }
        else
        {
            progreso += Time.deltaTime / duracion;
        }
        ActualizarBarra();

        temporizador -= Time.deltaTime;
        if (temporizador <= 0f)
        {
            CrearTronco();
            temporizador = Mathf.Lerp(intervaloInicial, intervaloFinal, progreso);
        }

        if (progreso >= 1f && !terminado) StartCoroutine(Llegar());
    }

    void MoverLucas()
    {
        if (!terminado)
        {
            // Teclado: un carril por pulsación
            if (EntradaJugador.TeclaIzquierda) carrilLucas = Mathf.Max(0, carrilLucas - 1);
            if (EntradaJugador.TeclaDerecha) carrilLucas = Mathf.Min(carriles.Length - 1, carrilLucas + 1);
            // Cámara: tu posición es el carril (izquierda / centro / derecha)
            if (EntradaJugador.PoseActiva)
            {
                int c = Mathf.Clamp(EntradaJugador.Carril + 1, 0, carriles.Length - 1);
                if (c != carrilCuerpoAnterior) { carrilLucas = c; carrilCuerpoAnterior = c; }
            }
        }
        if (carrilLucas != carrilSonado) { if (carrilSonado >= 0) Sonido.Efecto("CambioCarril", 0.7f); carrilSonado = carrilLucas; }
        var p = lucas.anchoredPosition;
        float objetivo = carriles[carrilLucas];
        p.x = Mathf.Lerp(p.x, objetivo, Time.deltaTime * 14f);
        lucas.anchoredPosition = p;
        lucas.localRotation = Quaternion.Euler(0, 0, (objetivo - p.x) * -0.04f);
    }

    void CrearTronco()
    {
        int c;
        do { c = Random.Range(0, carriles.Length); } while (c == ultimoCarril && Random.value < 0.7f);
        ultimoCarril = c;

        int detras = lucas.GetSiblingIndex(); // aparece detrás de Lucas
        var sombra = NuevaImagen("Sombra", spriteSombra, new Vector2(330, 90), detras);
        var rt = NuevaImagen("Tronco", spritesRodando[0], new Vector2(330, 118), detras + 1);

        var tr = new Tronco
        {
            rt = rt, sombra = sombra, carril = c,
            img = rt.GetComponent<Image>(), imgSombra = sombra.GetComponent<Image>(),
            duracion = Mathf.Lerp(tiempoRodarInicial, tiempoRodarFinal, progreso)
        };
        // sonido propio de cada tronco: un 'toc' al aparecer y luego un golpe por vuelta
        if (clipsGira == null)
        {
            var lista = new List<AudioClip>();
            for (int i = 1; i <= 8; i++)
            {
                var clip = Resources.Load<AudioClip>("Audio/Efectos/TroncoGira_" + i);
                if (clip != null) lista.Add(clip);
            }
            clipsGira = lista.ToArray();
            clipAparece = Resources.Load<AudioClip>("Audio/Efectos/TroncoAparece");
        }
        tr.audio = rt.gameObject.AddComponent<AudioSource>();
        tr.audio.playOnAwake = false;
        tr.audio.spatialBlend = 0f;
        tr.tono = Random.Range(0.92f, 1.08f);
        tr.audio.panStereo = Mathf.Clamp((c - carrilLucas) * 0.6f, -0.85f, 0.85f);
        if (clipAparece != null) tr.audio.PlayOneShot(clipAparece, volumenAparece * VolumenBase());
        troncos.Add(tr);
        Colocar(tr, 0f);
    }

    RectTransform NuevaImagen(string nombre, Sprite s, Vector2 tam, int indice)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(mundo, false);
        go.transform.SetSiblingIndex(indice);
        var img = go.GetComponent<Image>();
        img.sprite = s;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = tam;
        return rt;
    }

    // Posición en perspectiva: k=0 al fondo, k=1 a los pies de Lucas (k>1 sigue hacia la cámara)
    void Colocar(Tronco tr, float k)
    {
        float x = Mathf.LerpUnclamped(carriles[tr.carril] * escalaFondo, carriles[tr.carril], k);
        float y = Mathf.LerpUnclamped(horizonteY, sueloY, k);
        float s = Mathf.LerpUnclamped(escalaFondo, 1f, k);
        float rebote = Mathf.Abs(Mathf.Sin(tr.t * 18f)) * 14f * s;

        tr.sombra.anchoredPosition = new Vector2(x, y);
        tr.sombra.localScale = Vector3.one * s;
        tr.rt.anchoredPosition = new Vector2(x, y + 50f * s + rebote);
        tr.rt.localScale = Vector3.one * s;
    }

    void ActualizarTroncos()
    {
        for (int i = troncos.Count - 1; i >= 0; i--)
        {
            var tr = troncos[i];
            tr.t += Time.deltaTime / tr.duracion;
            float k = tr.t * tr.t; // acelera al acercarse (perspectiva)
            Colocar(tr, k);
            SonarTronco(tr, k);

            // animación de rodar
            tr.cuadro += Time.deltaTime * Mathf.Lerp(8f, 22f, Mathf.Clamp01(k));
            if (spritesRodando.Length > 0)
                tr.img.sprite = spritesRodando[(int)tr.cuadro % spritesRodando.Length];

            if (!tr.paso && tr.t >= 1f)
            {
                tr.paso = true;
                // ahora pasa por delante de Lucas
                tr.sombra.SetSiblingIndex(mundo.childCount - 1);
                tr.rt.SetSiblingIndex(mundo.childCount - 1);
                if (jugando && !terminado && aturdido <= 0f && tr.carril == carrilLucas) Golpe();
            }

            if (tr.paso)
            {
                float a = Mathf.Clamp01(1f - (tr.t - 1f) / 0.25f);
                tr.img.color = new Color(1, 1, 1, a);
                tr.imgSombra.color = new Color(1, 1, 1, a);
                if (tr.t > 1.25f)
                {
                    Destroy(tr.rt.gameObject);
                    Destroy(tr.sombra.gameObject);
                    troncos.RemoveAt(i);
                }
            }
        }
    }

    float VolumenBase()
    {
        float efectos = Sonido.Instancia != null ? Sonido.Instancia.volumenEfectos : 1f;
        return efectos * sonidoTroncos * (Transicion.Cargando ? 0f : 1f);
    }

    /// <summary>
    /// Un golpe de madera por cada vuelta del tronco (sigue la animación: se acelera al acercarse).
    /// Volumen según la cercanía, con tope; sale del lado del carril por donde viene respecto a Lucas.
    /// </summary>
    void SonarTronco(Tronco tr, float k)
    {
        if (tr.audio == null || clipsGira == null || clipsGira.Length == 0) return;
        float cerca = Mathf.Clamp01(k);
        tr.audio.panStereo = Mathf.Clamp((tr.carril - carrilLucas) * 0.6f, -0.85f, 0.85f);
        tr.audio.pitch = tr.tono * (0.95f + 0.1f * cerca);

        int cuadrosPorVuelta = Mathf.Max(1, spritesRodando.Length);
        int vuelta = (int)(tr.cuadro / cuadrosPorVuelta);
        if (vuelta <= tr.vueltas) return;
        tr.vueltas = vuelta;

        float vol = 0.05f + 0.95f * Mathf.Pow(cerca, curvaVolumen);
        if (tr.carril == carrilLucas) vol *= 1.15f;                       // un poco más si viene directo hacia él
        if (tr.paso) vol *= Mathf.Clamp01(1f - (tr.t - 1f) / 0.25f);       // se apaga al pasar
        vol = Mathf.Min(vol, 1f) * volumenTroncos * VolumenBase();
        if (vol > 0.005f) tr.audio.PlayOneShot(clipsGira[Random.Range(0, clipsGira.Length)], vol);
    }

    /// <summary>Al terminar la prueba (llegar o quedarse sin vidas) se apagan los troncos.</summary>
    IEnumerator ApagarTroncos()
    {
        float desde = sonidoTroncos;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
        {
            sonidoTroncos = Mathf.Lerp(desde, 0f, t);
            foreach (var tr in troncos) if (tr.audio != null) tr.audio.volume = sonidoTroncos;
            yield return null;
        }
        sonidoTroncos = 0f;
        foreach (var tr in troncos) if (tr.audio != null) tr.audio.Stop();
    }

    void Golpe()
    {
        aturdido = tiempoAturdido;
        Sonido.Efecto("TroncoGolpe");
        progreso = Mathf.Max(0f, progreso - penalizacion);
        StartCoroutine(Sacudir());
        if (vidas != null && vidas.Perder() <= 0) StartCoroutine(SinVidas());
    }

    IEnumerator SinVidas()
    {
        terminado = true;
        StartCoroutine(ApagarTroncos());
        Sonido.Efecto("SinVidas");
        textoMensaje.text = "¡Oh no! Te quedaste sin vidas.\nIntentémoslo otra vez.";
        yield return Bifurcacion.Fundido(mensaje, 0f, 1f, 0.3f);
        yield return new WaitForSeconds(2.2f);
        Transicion.Recargar();
    }

    IEnumerator Sacudir()
    {
        var baseY = lucas.anchoredPosition.y;
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            var p = lucas.anchoredPosition;
            p.y = baseY + Mathf.Sin(t * 60f) * 10f;
            lucas.anchoredPosition = p;
            yield return null;
        }
        var q = lucas.anchoredPosition; q.y = baseY; lucas.anchoredPosition = q;
    }

    void ActualizarBarra()
    {
        float p = Mathf.Clamp01(progreso);
        barraRelleno.anchorMax = new Vector2(p, 1f);
        textoProgreso.text = Mathf.RoundToInt(p * 100f) + "%";
    }

    IEnumerator Llegar()
    {
        terminado = true;
        StartCoroutine(ApagarTroncos());
        imagenLucas.color = Color.white;
        Sonido.Efecto("MisionCompleta");
        textoMensaje.text = "¡Lo lograste!\nLlegamos al lugar de la leña.";
        yield return Bifurcacion.Fundido(mensaje, 0f, 1f, 0.3f);
        yield return new WaitForSeconds(1.8f);
        Transicion.Cargar(escenaLena);
    }
}
