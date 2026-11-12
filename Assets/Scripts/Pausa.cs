using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SD = SeleccionDificultad;

/// <summary>
/// JUEGO UNIFICADO: menú de pausa en los 5 niveles (no en la historia del inicio).
/// Al empezar el nivel 1 (CarpaInicio), una vez por partida, aparece una ventana que explica
/// que se puede pausar levantando las DOS manos por encima de la cabeza (se cierra con la mano derecha / Enter).
///   Mantener las dos manos por encima de la cabeza (o Esc / P) → pausa (igual para diestros y zurdos;
///   ninguna seña ni movimiento de los juegos se hace por encima de la cabeza)
///     (0.4 s en el nivel 3; 1.2 s en los niveles de manos, para no pausar sin querer)
///   En la pausa:
///     Inclinarse a la izquierda (o ←)    → "Nivel anterior"   (pide confirmar)
///     Inclinarse a la derecha (o →)      → "Siguiente nivel"  (pide confirmar)
///     Cerrar los puños (o C)            → "Cambiar dificultad" en los niveles que la tienen:
///         2 Ruta de Energía (Fácil/Media/Difícil: tiempo por pila; empieza de nuevo desde la primera pila),
///         3 Cruce del bosque (se aplica al momento), 4 Pinzas de fuego (Fácil/Medio/Difícil; la fogata empieza de nuevo).
///         Los niveles 1 y 5 no tienen dificultad.
///     Levantar la mano izquierda (o Esc) → volver al juego
///   En "¿Ir al nivel…?": mano derecha (o Enter) → sí; mano izquierda (o Esc) → no, vuelve al menú
///   En "Cambiar dificultad":
///     Inclinarse izquierda / derecha (o flechas) → elegir
///     Mano derecha (o Enter)                    → confirmar (vuelve a la pausa con la nueva dificultad)
///     Mano izquierda (o Esc)                    → atrás, sin cambiar
/// Mientras está en pausa: Time.timeScale = 0, se pausan los sonidos del juego (la música sigue)
/// y EntradaJugador no entrega nada al juego.
/// </summary>
public class Pausa : MonoBehaviour
{
    public static Pausa Instancia { get; private set; }
    public static bool Activa { get; private set; }

    [Tooltip("Segundos que hay que mantener las dos manos sobre la cabeza para pausar (nivel 3, juego de cuerpo)")]
    public float segundosParaPausar = 0.8f;
    [Tooltip("Lo mismo en los niveles 1, 2, 4 y 5, que se juegan con las manos (más tiempo para no pausar sin querer)")]
    public float segundosParaPausarManos = 1.2f;
    [Tooltip("Después de volver al juego, segundos sin poder pausar otra vez (además hay que bajar la mano izquierda)")]
    public float cooldown = 2.5f;
    [Tooltip("Cuenta regresiva al volver al juego (3, 2, 1) para que el jugador se acomode")]
    public int cuentaRegresiva = 3;

    enum Vista { Ninguna, Menu, Dificultad, Volviendo, Aviso, CambioNivel, Saliendo }
    Vista vista = Vista.Ninguna;


    Canvas canvas;
    RectTransform raiz, vistaMenu, vistaDificultad;
    CanvasGroup grupo, indicador;
    Image rellenoIndicador;
    TextMeshProUGUI textoDificultadActual;
    RectTransform[] tarjetasMenu = new RectTransform[4];   // 0 anterior, 1 dificultad, 2 volver, 3 siguiente
    RectTransform vistaCambio;
    TextMeshProUGUI tituloCambio, nivelCambio, nombreCambio;
    Image insigniaCambio;
    int destinoNivel;
    TextMeshProUGUI[] nombresDif = new TextMeshProUGUI[3], descDif = new TextMeshProUGUI[3];
    TextMeshProUGUI notaDificultad;
    RectTransform[] tarjetas = new RectTransform[3];
    Image[] bordes = new Image[3], fondos = new Image[3];
    RectTransform etiquetaActual;
    TextMeshProUGUI[] avisos = new TextMeshProUGUI[0];

    float manoIzquierdaDesde = -1f;
    float bloqueoHasta;          // no se puede volver a pausar antes de este momento
    bool esperarBajarMano;       // y además hay que bajar la mano izquierda
    CanvasGroup avisoVuelta;
    RectTransform vistaAviso;
    float avisoDesde = -1f;
    bool cerrarAvisoAlFinal;
    TextMeshProUGUI textoVuelta, numeroCuenta;
    float listoEn;
    int seleccion;
    bool cambioReciente;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("Pausa");
        go.AddComponent<Pausa>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        SceneManager.sceneLoaded += AlCargar;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AlCargar;
        if (Instancia == this) { Reanudar(); Instancia = null; }
    }

    void AlCargar(Scene s, LoadSceneMode m) { StopAllCoroutines(); Cerrar(); esperarBajarMano = false; bloqueoHasta = 0f; MostrarAvisoVuelta(false); }   // nunca se queda pausado al cambiar de escena

    static int NivelActual => NivelesUnificados.NivelDeEscena(SceneManager.GetActiveScene().name);
    static bool EscenaConPausa() => NivelActual > 0;

    static PoseReceiver Pose => PoseReceiver.Instancia;
    static bool PoseActiva => Pose != null && Pose.Activo;

    // ------------------------------------------------------------ Entrada

    void Update()
    {
        float t = Time.unscaledTime;
        Animar(t);

        if (vista == Vista.Aviso)
        {
            if (t >= listoEn && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)
                                 || (Pose != null && Pose.ManoDerechaAhora)))
                CerrarAviso();
            return;
        }

        if (vista == Vista.Ninguna)
        {
            if (!EscenaConPausa() || Transicion.Cargando || VentanaInsignia.Abierta) { manoIzquierdaDesde = -1f; MostrarIndicador(0f); avisoDesde = -1f; return; }

            // Al empezar el nivel 1 (una vez por partida): ventana que explica la pausa
            if (!EstadoJuego.AvisoPausaMostrado && SceneManager.GetActiveScene().name == "CarpaInicio")
            {
                if (avisoDesde < 0f) avisoDesde = t;
                if (t - avisoDesde > 0.6f) AbrirAviso();
                return;
            }

            bool izquierda = PoseActiva && Pose.ManosSobreCabeza;   // gesto de pausa: las dos manos sobre la cabeza

            // Cooldown al volver al juego: hay que bajar las manos y esperar unos segundos
            if (esperarBajarMano && !izquierda) esperarBajarMano = false;
            if (esperarBajarMano || t < bloqueoHasta)
            {
                manoIzquierdaDesde = -1f;
                MostrarIndicador(0f);
                MostrarAvisoVuelta(esperarBajarMano);
                return;
            }
            MostrarAvisoVuelta(false);

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) { Abrir(); return; }

            if (izquierda && manoIzquierdaDesde < 0f) manoIzquierdaDesde = t;
            if (!izquierda) manoIzquierdaDesde = -1f;
            float espera = NivelActual == 3 ? segundosParaPausar : segundosParaPausarManos;
            float k = manoIzquierdaDesde < 0f ? 0f : Mathf.Clamp01((t - manoIzquierdaDesde) / espera);
            MostrarIndicador(k);
            if (k >= 1f) { manoIzquierdaDesde = -1f; MostrarIndicador(0f); Abrir(); }
            return;
        }

        if (vista == Vista.Volviendo || vista == Vista.Saliendo || t < listoEn) return;

        bool atras = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || (Pose != null && Pose.ManoIzquierdaAhora);

        if (vista == Vista.Menu)
        {
            if (atras) Volver();
            else if (TieneDificultad(NivelActual) && (Input.GetKeyDown(KeyCode.C) || (Pose != null && Pose.PunoAhora))) MostrarDificultad();
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || (Pose != null && Pose.CarrilCambioA(-1))) PedirCambioNivel(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || (Pose != null && Pose.CarrilCambioA(1))) PedirCambioNivel(1);
        }
        else if (vista == Vista.CambioNivel)
        {
            if (atras) { Sonido.Efecto("Boton"); MostrarMenu(); }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)
                     || (Pose != null && Pose.ManoDerechaAhora)) ConfirmarCambioNivel();
        }
        else if (vista == Vista.Dificultad)
        {
            if (atras) { Sonido.Efecto("Boton"); MostrarMenu(); }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || (Pose != null && Pose.CarrilCambioA(-1))) Elegir(seleccion - 1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || (Pose != null && Pose.CarrilCambioA(1))) Elegir(seleccion + 1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)
                     || (Pose != null && Pose.ManoDerechaAhora)) Confirmar();
        }
    }

    // ------------------------------------------------------------ Estados

    void Abrir()
    {
        Construir();
        Activa = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;   // troncos y carrete se detienen; música y efectos de menú no (Sonido.cs)
        cambioReciente = false;
        Sonido.Efecto("Aviso", 0.8f);
        canvas.gameObject.SetActive(true);
        StartCoroutine(Fundido(0f, 1f, 0.2f));
        MostrarMenu();
    }

    /// <summary>
    /// "Volver al juego": cuenta regresiva 3-2-1 con el juego aún detenido, luego sigue.
    /// Después, no se puede volver a pausar hasta bajar la mano izquierda y pasar el cooldown.
    /// </summary>
    void Volver()
    {
        if (vista == Vista.Menu || vista == Vista.Dificultad) StartCoroutine(RutinaVolver());
    }

    IEnumerator RutinaVolver()
    {
        Sonido.Efecto("Boton");
        vista = Vista.Volviendo;
        esperarBajarMano = true;
        vistaMenu.gameObject.SetActive(false);
        vistaDificultad.gameObject.SetActive(false);
        vistaCambio.gameObject.SetActive(false);
        numeroCuenta.gameObject.SetActive(true);
        for (int n = cuentaRegresiva; n >= 1; n--)
        {
            numeroCuenta.text = n.ToString();
            textoVuelta.text = esperarBajarMano ? "<b>Baja las manos</b>" : "<b>¡Prepárate!</b>";
            Sonido.Efecto("CambioCarril", 0.6f, 0f);
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.8f)
            {
                if (esperarBajarMano && !(PoseActiva && Pose.ManosSobreCabeza)) { esperarBajarMano = false; textoVuelta.text = "<b>¡Prepárate!</b>"; }
                numeroCuenta.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(k * 4f));
                var c = numeroCuenta.color; c.a = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f); numeroCuenta.color = c;
                yield return null;
            }
        }
        numeroCuenta.gameObject.SetActive(false);
        Sonido.Efecto("Aparecer", 0.7f, 0f);
        bloqueoHasta = Time.unscaledTime + cooldown;
        Cerrar();
    }

    void MostrarAvisoVuelta(bool visible)
    {
        if (avisoVuelta == null) return;
        if (visible) avisoVuelta.alpha = Mathf.MoveTowards(avisoVuelta.alpha, 1f, Time.unscaledDeltaTime * 6f);
        else avisoVuelta.alpha = Mathf.MoveTowards(avisoVuelta.alpha, 0f, Time.unscaledDeltaTime * 4f);
        avisoVuelta.gameObject.SetActive(avisoVuelta.alpha > 0.001f || visible);
    }

    // ------------------------------------------------------------ Ventana "puedes pausar"

    void AbrirAviso()
    {
        Construir();
        EstadoJuego.AvisoPausaMostrado = true;
        Activa = true;            // el juego no recibe entradas mientras está abierta
        Time.timeScale = 0f;
        vista = Vista.Aviso;
        listoEn = Time.unscaledTime + 0.8f;
        Sonido.Efecto("Aparecer", 0.7f, 0f);
        canvas.gameObject.SetActive(true);
        vistaMenu.gameObject.SetActive(false);
        vistaDificultad.gameObject.SetActive(false);
        vistaCambio.gameObject.SetActive(false);
        numeroCuenta.gameObject.SetActive(false);
        vistaAviso.gameObject.SetActive(true);
        StartCoroutine(Fundido(0f, 1f, 0.25f));
    }

    void CerrarAviso()
    {
        Sonido.Efecto("Boton");
        cerrarAvisoAlFinal = true;   // se cierra en LateUpdate: la mano derecha de este frame no llega al juego
        listoEn = Time.unscaledTime + 10f;
    }

    void LateUpdate()
    {
        if (!cerrarAvisoAlFinal) return;
        cerrarAvisoAlFinal = false;
        vistaAviso.gameObject.SetActive(false);
        bloqueoHasta = Time.unscaledTime + 0.5f;
        Cerrar();
    }

    void Cerrar()
    {
        vista = Vista.Ninguna;
        if (vistaAviso != null) vistaAviso.gameObject.SetActive(false);
        if (canvas != null) canvas.gameObject.SetActive(false);
        Reanudar();
    }

    static void Reanudar()
    {
        Activa = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void MostrarMenu()
    {
        vista = Vista.Menu;
        listoEn = Time.unscaledTime + 0.35f;
        vistaMenu.gameObject.SetActive(true);
        vistaDificultad.gameObject.SetActive(false);
        vistaAviso.gameObject.SetActive(false);
        vistaCambio.gameObject.SetActive(false);
        int nivel = NivelActual;
        string encabezado = "Nivel " + nivel + " · " + NivelesUnificados.NombreDeNivel[nivel];
        textoDificultadActual.text = !TieneDificultad(nivel) ? encabezado : cambioReciente
            ? encabezado + "  ·  <color=#F4C95D>¡Listo! Dificultad: " + NombreDificultadActual() + "</color>"
            : encabezado + "  ·  Dificultad: " + NombreDificultadActual();

        // tarjeta de dificultad: solo en los niveles que tienen dificultad (2, 3 y 4)
        tarjetasMenu[1].gameObject.SetActive(TieneDificultad(nivel));
        if (TieneDificultad(nivel)) Disponible(tarjetasMenu[1], PuedeCambiarDificultad(), "Cierra los puños", "Ahora no se puede");
        int n = 0;
        foreach (var tm in tarjetasMenu) if (tm.gameObject.activeSelf) n++;
        int i = 0;
        foreach (var tm in tarjetasMenu)
            if (tm.gameObject.activeSelf) tm.anchoredPosition = new Vector2((i++ - (n - 1) / 2f) * 450f, -20f);
        Disponible(tarjetasMenu[0], nivel > 1, "Inclínate a la izquierda", "Ya es el primer nivel");
        Disponible(tarjetasMenu[3], nivel < 5, "Inclínate a la derecha", "Ya es el último nivel");
    }

    static void Disponible(RectTransform tarjeta, bool si, string gesto, string no)
    {
        var cg = tarjeta.GetComponent<CanvasGroup>();
        if (cg == null) cg = tarjeta.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = si ? 1f : 0.4f;
        var txt = tarjeta.Find("Pildora/Texto")?.GetComponent<TextMeshProUGUI>();
        if (txt != null) txt.text = "<b>" + (si ? gesto : no) + "</b>";
    }

    // ------------------------------------------------------------ Dificultad de cada nivel

    static bool TieneDificultad(int nivel) => nivel == 2 || nivel == 3 || nivel == 4;

    static readonly string[] NombresNivel2 = { "Fácil", "Media", "Difícil" };
    static readonly string[] DescripcionesNivel2 =
    {
        "Tienes <b>1 min 30 s</b> para llevar cada pila hasta la linterna.",
        "Tienes <b>1 minuto</b> para llevar cada pila hasta la linterna.",
        "Tienes <b>40 segundos</b> para llevar cada pila hasta la linterna.",
    };
    static readonly string[] NombresNivel4 = { "Fácil", "Medio", "Difícil" };
    static readonly string[] DescripcionesNivel4 =
    {
        "Juntas <b>5 ramitas</b> haciendo la pinza con el índice. Sin reloj.",
        "Cada ramita con <b>otro dedo</b>, en orden: índice, medio, anular y meñique. Sin reloj.",
        "<b>6 ramitas</b>, el dedo sale al azar y tienes <b>2 minutos</b>.",
    };

    static RecorridoAR.RecorridoGame JuegoNivel2 => FindFirstObjectByType<RecorridoAR.RecorridoGame>();
    static FogataGame JuegoNivel4 => FindFirstObjectByType<FogataGame>();

    static int DificultadActualIndice()
    {
        switch (NivelActual)
        {
            case 2: { var g = JuegoNivel2; return g != null ? Mathf.Clamp(g.Dificultad - 1, 0, 2) : 0; }
            case 4: { var f = JuegoNivel4; return f != null ? (int)f.DificultadActual : 0; }
            default: return (int)EstadoJuego.Dificultad;
        }
    }

    static string NombreDificultadActual()
    {
        int i = DificultadActualIndice();
        switch (NivelActual)
        {
            case 2: return NombresNivel2[i];
            case 4: return NombresNivel4[i];
            default: return EstadoJuego.NombreDificultad;
        }
    }

    static bool PuedeCambiarDificultad()
    {
        if (NivelActual == 2) { var g = JuegoNivel2; return g != null && g.PuedeCambiarDificultad; }
        if (NivelActual == 4) return JuegoNivel4 != null;
        return true;
    }

    // ------------------------------------------------------------ Nivel anterior / siguiente

    void PedirCambioNivel(int paso)
    {
        int destino = NivelActual + paso;
        if (destino < 1 || destino > 5) { Sonido.Efecto("CambioCarril", 0.5f); return; }
        Sonido.Efecto("Boton");
        destinoNivel = destino;
        vista = Vista.CambioNivel;
        listoEn = Time.unscaledTime + 0.5f;
        vistaMenu.gameObject.SetActive(false);
        vistaCambio.gameObject.SetActive(true);
        tituloCambio.text = paso < 0 ? "¿Volver al nivel anterior?" : "¿Pasar al siguiente nivel?";
        nivelCambio.text = "Nivel " + destino;
        nombreCambio.text = NivelesUnificados.NombreDeNivel[destino];
        var tex = Resources.Load<Texture2D>("Insignias/Insignia" + destino);
        insigniaCambio.sprite = tex ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
        insigniaCambio.enabled = tex != null;
    }

    void ConfirmarCambioNivel()
    {
        if (vista != Vista.CambioNivel) return;
        Sonido.Efecto("Aparecer", 0.7f, 0f);
        vista = Vista.Saliendo;               // sigue en pausa hasta que cargue el otro nivel (AlCargar la cierra)
        listoEn = Time.unscaledTime + 99f;
        NivelesUnificados.IrANivel(destinoNivel);
    }

    void MostrarDificultad()
    {
        int nivel = NivelActual;
        if (!TieneDificultad(nivel) || !PuedeCambiarDificultad()) return;
        Sonido.Efecto("Boton");
        for (int k = 0; k < 3; k++)
        {
            nombresDif[k].text = nivel == 2 ? NombresNivel2[k] : nivel == 4 ? NombresNivel4[k] : SD.Nombres[k];
            descDif[k].text = nivel == 2 ? DescripcionesNivel2[k] : nivel == 4 ? DescripcionesNivel4[k] : SD.Descripciones[k];
        }
        notaDificultad.text = nivel == 2 ? "Al cambiarla, la ruta empieza de nuevo desde la primera pila."
                            : nivel == 4 ? "Al cambiarla, la fogata empieza de nuevo con esa dificultad."
                            : "";
        vista = Vista.Dificultad;
        listoEn = Time.unscaledTime + 0.35f;
        vistaMenu.gameObject.SetActive(false);
        vistaCambio.gameObject.SetActive(false);
        vistaDificultad.gameObject.SetActive(true);
        seleccion = DificultadActualIndice();
        etiquetaActual.SetParent(tarjetas[seleccion], false);
        Refrescar(true);
    }

    void Elegir(int i)
    {
        i = Mathf.Clamp(i, 0, 2);
        if (i == seleccion) return;
        seleccion = i;
        Sonido.Efecto("CambioCarril", 0.7f);
        Refrescar(false);
    }

    void Confirmar()
    {
        Sonido.Efecto("Boton");
        int nivel = NivelActual;
        bool cambia = seleccion != DificultadActualIndice();
        if (nivel == 4 && cambia)
        {
            // Pinzas de fuego: se recarga la escena y empieza directo con la nueva dificultad
            vista = Vista.Saliendo;
            listoEn = Time.unscaledTime + 99f;
            FogataGame.DificultadPendiente = (FogataGame.Dificultad)seleccion;
            Transicion.Cargar("Fogata");
            return;
        }
        if (nivel == 2 && cambia) { var g = JuegoNivel2; if (g != null) g.CambiarDificultadExterna(seleccion + 1); }
        if (nivel == 3) EstadoJuego.CambiarDificultad((Dificultad)seleccion);
        cambioReciente = cambia || cambioReciente;
        StartCoroutine(PulsoYMenu(tarjetas[seleccion]));
    }

    IEnumerator PulsoYMenu(RectTransform rt)
    {
        listoEn = Time.unscaledTime + 10f;
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.3f)
        {
            rt.localScale = Vector3.one * (1.08f + Mathf.Sin(k * Mathf.PI) * 0.1f);
            yield return null;
        }
        if (vista == Vista.Dificultad) MostrarMenu();
    }

    void Refrescar(bool inmediato)
    {
        for (int i = 0; i < 3; i++)
        {
            bool sel = i == seleccion;
            bordes[i].color = sel ? Hx("#4F8A5B") : Hx("#F7D98C");
            fondos[i].color = sel ? Hx("#F4C95D") : Hx("#FFFCF6");
            var cg = tarjetas[i].GetComponent<CanvasGroup>();
            if (cg == null) cg = tarjetas[i].gameObject.AddComponent<CanvasGroup>();
            cg.alpha = sel ? 1f : 0.7f;
            if (inmediato) tarjetas[i].localScale = Vector3.one * (sel ? 1.08f : 0.9f);
        }
    }

    void Animar(float t)
    {
        if (vista == Vista.Dificultad)
            for (int i = 0; i < 3; i++)
            {
                float objetivo = i == seleccion ? 1.08f + Mathf.Sin(t * 2.2f) * 0.015f : 0.9f;
                float s = Mathf.Lerp(tarjetas[i].localScale.x, objetivo, Time.unscaledDeltaTime * 12f);
                tarjetas[i].localScale = new Vector3(s, s, 1f);
            }
        if (vista == Vista.Menu)
            for (int i = 0; i < tarjetasMenu.Length; i++)
                tarjetasMenu[i].localScale = Vector3.one * (1f + Mathf.Sin(t * 2.2f + i * 1.5f) * 0.02f);
        foreach (var a in avisos)
            if (a != null) { var c = a.color; c.a = 0.75f + Mathf.Sin(t * 3f) * 0.25f; a.color = c; }
    }

    void MostrarIndicador(float k)
    {
        if (k > 0f) Construir();
        if (indicador == null) return;
        indicador.gameObject.SetActive(k > 0f);
        if (k > 0f) { indicador.alpha = 1f; rellenoIndicador.fillAmount = k; }
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

    // ------------------------------------------------------------ Interfaz (se dibuja por código)

    void Construir()
    {
        if (canvas != null) return;

        TMP_FontAsset titan = null, nunito = null;
        foreach (var tx in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            if (tx.font == null) continue;
            if (titan == null && tx.font.name.Contains("Titan")) titan = tx.font;
            if (nunito == null && tx.font.name.Contains("Nunito")) nunito = tx.font;
        }
        if (titan == null) titan = TMP_Settings.defaultFontAsset;
        if (nunito == null) nunito = titan;

        var go = new GameObject("CanvasPausa", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;   // debajo del recuadro de la cámara (900): el jugador se sigue viendo
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var c = (RectTransform)go.transform;

        // Indicador "Pausando..." (mientras se mantiene la mano izquierda), arriba al centro.
        // Va en su propio objeto para verse aunque el menú esté cerrado.
        var ind = new GameObject("IndicadorPausa", typeof(RectTransform), typeof(CanvasGroup));
        var irt = (RectTransform)ind.transform;
        irt.SetParent(transform, false);
        var ic = ind.AddComponent<Canvas>(); ind.AddComponent<CanvasScaler>();
        ic.renderMode = RenderMode.ScreenSpaceOverlay; ic.sortingOrder = 851;
        var isc = ind.GetComponent<CanvasScaler>();
        isc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; isc.referenceResolution = new Vector2(1920, 1080); isc.matchWidthOrHeight = 0.5f;
        indicador = ind.GetComponent<CanvasGroup>();
        var redondo = SD.Redondeado();
        var pil = SD.Img("Pildora", irt, redondo, Hx("#101F3D"));
        pil.type = Image.Type.Sliced;
        pil.rectTransform.anchorMin = pil.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        pil.rectTransform.sizeDelta = new Vector2(420, 70);
        pil.rectTransform.anchoredPosition = new Vector2(0, -170);
        var anillo = SD.Img("Anillo", pil.rectTransform, Circulo(), Hx("#F4C95D"));
        anillo.type = Image.Type.Filled; anillo.fillMethod = Image.FillMethod.Radial360; anillo.fillOrigin = 2;
        anillo.rectTransform.sizeDelta = new Vector2(46, 46);
        anillo.rectTransform.anchoredPosition = new Vector2(-160, 0);
        rellenoIndicador = anillo;
        SD.Txt("Texto", pil.rectTransform, nunito, "<b>Pausando...</b>", 30, Hx("#E6ECF5"), new Vector2(30, 0), new Vector2(320, 56));
        ind.SetActive(false);

        // Aviso después de volver al juego (mientras la mano izquierda siga arriba)
        var av = new GameObject("AvisoVuelta", typeof(RectTransform), typeof(CanvasGroup));
        var art = (RectTransform)av.transform;
        art.SetParent(irt.parent, false);
        var ac = av.AddComponent<Canvas>(); av.AddComponent<CanvasScaler>();
        ac.renderMode = RenderMode.ScreenSpaceOverlay; ac.sortingOrder = 851;
        var asc = av.GetComponent<CanvasScaler>();
        asc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; asc.referenceResolution = new Vector2(1920, 1080); asc.matchWidthOrHeight = 0.5f;
        avisoVuelta = av.GetComponent<CanvasGroup>();
        var pv = SD.Img("Pildora", art, redondo, Hx("#101F3D"));
        pv.type = Image.Type.Sliced;
        pv.rectTransform.anchorMin = pv.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        pv.rectTransform.sizeDelta = new Vector2(520, 70);
        pv.rectTransform.anchoredPosition = new Vector2(0, -170);
        SD.Txt("Texto", pv.rectTransform, nunito, "<b>Baja las manos</b>", 30, Hx("#F4C95D"), Vector2.zero, new Vector2(480, 56));
        avisoVuelta.alpha = 0f;
        av.SetActive(false);

        // Raíz del menú
        var r = new GameObject("Menu", typeof(RectTransform), typeof(CanvasGroup));
        raiz = (RectTransform)r.transform;
        raiz.SetParent(c, false);
        SD.Estirar(raiz);
        grupo = r.GetComponent<CanvasGroup>();
        var velo = SD.Img("Velo", raiz, null, new UnityEngine.Color(0.063f, 0.122f, 0.239f, 0.985f));   // 0.985: en espacio lineal 0.95 dejaba ver demasiado el fondo
        velo.raycastTarget = true;
        SD.Estirar(velo.rectTransform);

        var texManos = Resources.Load<Texture2D>("Gestos/ManosAbiertas");
        var texPunos = Resources.Load<Texture2D>("Gestos/ManosPunos");
        Sprite manoIzq = texManos ? Sprite.Create(texManos, new Rect(0, 0, texManos.width / 2f, texManos.height), new Vector2(0.5f, 0.5f), 100f) : null;
        Sprite manoDer = texManos ? Sprite.Create(texManos, new Rect(texManos.width / 2f, 0, texManos.width / 2f, texManos.height), new Vector2(0.5f, 0.5f), 100f) : null;
        Sprite punos = texPunos ? Sprite.Create(texPunos, new Rect(0, 0, texPunos.width, texPunos.height), new Vector2(0.5f, 0.5f), 100f) : null;

        // ---- Cuenta regresiva al volver al juego
        numeroCuenta = SD.Txt("Cuenta", raiz, titan, "3", 260, Hx("#F4C95D"), new Vector2(0, 40), new Vector2(600, 320));
        textoVuelta = SD.Txt("TextoVuelta", numeroCuenta.rectTransform, nunito, "<b>¡Prepárate!</b>", 40, Hx("#E6ECF5"), new Vector2(0, -230), new Vector2(900, 60));
        numeroCuenta.gameObject.SetActive(false);

        // ---- Ventana: "puedes pausar levantando la mano izquierda" (primera vez en la bifurcación)
        vistaAviso = Hijo("VistaAvisoPausa", raiz);
        var marco = SD.Img("Tarjeta", vistaAviso, redondo, Hx("#F7D98C"));
        marco.type = Image.Type.Sliced;
        marco.rectTransform.sizeDelta = new Vector2(900, 560);
        marco.rectTransform.anchoredPosition = new Vector2(0, 20);
        var fondoAviso = SD.Img("Relleno", marco.rectTransform, redondo, Hx("#FFFCF6"));
        fondoAviso.type = Image.Type.Sliced;
        SD.Estirar(fondoAviso.rectTransform, 8);
        var mt = marco.rectTransform;
        SD.Txt("Titulo", mt, titan, "Menú de pausa", 64, Hx("#365F73"), new Vector2(0, 205), new Vector2(800, 90));
        var texPausa = Resources.Load<Texture2D>("Gestos/PausaManosArriba");
        if (texPausa != null)
        {
            var mi = SD.Img("ManosSobreCabeza", mt, Sprite.Create(texPausa, new Rect(0, 0, texPausa.width, texPausa.height), new Vector2(0.5f, 0.5f), 100f), UnityEngine.Color.white);
            mi.preserveAspect = true;
            mi.rectTransform.sizeDelta = new Vector2(140, 166);
            mi.rectTransform.anchoredPosition = new Vector2(0, 62);
        }
        SD.Txt("Texto", mt, nunito, "Puedes abrir el menú de pausa levantando\nlas <b>dos manos por encima de la cabeza</b>.", 38, Hx("#18392B"),
            new Vector2(0, -75), new Vector2(800, 110));
        var bc = SD.Img("Continuar", mt, redondo, Hx("#F4C95D"));
        bc.type = Image.Type.Sliced;
        bc.rectTransform.sizeDelta = new Vector2(720, 80);
        bc.rectTransform.anchoredPosition = new Vector2(0, -190);
        var bcb = bc.gameObject.AddComponent<Button>(); bc.raycastTarget = true;
        bcb.transition = Selectable.Transition.None;
        bcb.onClick.AddListener(() => { if (vista == Vista.Aviso && Time.unscaledTime >= listoEn) CerrarAviso(); });
        if (manoDer != null)
        {
            var md = SD.Img("ManoDerecha", bc.rectTransform, manoDer, UnityEngine.Color.white);
            md.preserveAspect = true;
            md.rectTransform.sizeDelta = new Vector2(44, 60);
            md.rectTransform.anchoredPosition = new Vector2(-310, 0);
        }
        SD.Txt("Texto", bc.rectTransform, nunito, "<b>Levanta la mano derecha para continuar</b>", 30, Hx("#18392B"), new Vector2(25, 0), new Vector2(600, 64));
        vistaAviso.gameObject.SetActive(false);

        // ---- Vista: menú de pausa
        vistaMenu = Hijo("VistaMenu", raiz);
        SD.Txt("Titulo", vistaMenu, titan, "Juego en pausa", 86, Hx("#F4C95D"), new Vector2(0, 340), new Vector2(1400, 120));
        textoDificultadActual = SD.Txt("Dificultad", vistaMenu, nunito, "Dificultad: Normal", 38, Hx("#E6ECF5"), new Vector2(0, 250), new Vector2(1200, 60));
        var flecha = Resources.Load<Texture2D>("Gestos/Flecha");
        Sprite flechaSp = flecha ? Sprite.Create(flecha, new Rect(0, 0, flecha.width, flecha.height), new Vector2(0.5f, 0.5f), 100f) : null;
        tarjetasMenu[0] = TarjetaGesto(vistaMenu, redondo, titan, nunito, Vector2.zero, flechaSp, new Vector2(130, 150),
            "Nivel anterior", "Inclínate a la izquierda", () => PedirCambioNivel(-1));
        tarjetasMenu[1] = TarjetaGesto(vistaMenu, redondo, titan, nunito, Vector2.zero, punos, new Vector2(230, 150),
            "Cambiar dificultad", "Cierra los puños", MostrarDificultad);
        tarjetasMenu[2] = TarjetaGesto(vistaMenu, redondo, titan, nunito, Vector2.zero, manoIzq, new Vector2(110, 150),
            "Volver al juego", "Levanta la mano izquierda", Volver);
        tarjetasMenu[3] = TarjetaGesto(vistaMenu, redondo, titan, nunito, Vector2.zero, flechaSp, new Vector2(130, 150),
            "Siguiente nivel", "Inclínate a la derecha", () => PedirCambioNivel(1));
        // la flecha (apunta hacia arriba) gira hacia la izquierda o la derecha
        var iconoIzq = tarjetasMenu[0].Find("Gesto");
        if (iconoIzq != null) iconoIzq.localRotation = Quaternion.Euler(0, 0, 90f);
        var iconoDer = tarjetasMenu[3].Find("Gesto");
        if (iconoDer != null) iconoDer.localRotation = Quaternion.Euler(0, 0, -90f);

        // ---- Vista: ¿ir a otro nivel?
        vistaCambio = Hijo("VistaCambioNivel", raiz);
        tituloCambio = SD.Txt("Titulo", vistaCambio, titan, "¿Pasar al siguiente nivel?", 86, Hx("#F4C95D"), new Vector2(0, 360), new Vector2(1500, 120));
        var bordeCambio = SD.Img("Tarjeta", vistaCambio, redondo, Hx("#F7D98C"));
        bordeCambio.type = Image.Type.Sliced; bordeCambio.raycastTarget = true;
        bordeCambio.rectTransform.sizeDelta = new Vector2(760, 470);
        bordeCambio.rectTransform.anchoredPosition = new Vector2(0, 0);
        var bcCambio = bordeCambio.gameObject.AddComponent<Button>();
        bcCambio.transition = Selectable.Transition.None;
        bcCambio.onClick.AddListener(() => { if (Time.unscaledTime >= listoEn) ConfirmarCambioNivel(); });
        var rellenoCambio = SD.Img("Relleno", bordeCambio.rectTransform, redondo, Hx("#FFFCF6"));
        rellenoCambio.type = Image.Type.Sliced;
        SD.Estirar(rellenoCambio.rectTransform, 8);
        insigniaCambio = SD.Img("Insignia", bordeCambio.rectTransform, null, UnityEngine.Color.white);
        insigniaCambio.preserveAspect = true;
        insigniaCambio.rectTransform.sizeDelta = new Vector2(220, 220);
        insigniaCambio.rectTransform.anchoredPosition = new Vector2(0, 85);
        nivelCambio = SD.Txt("Nivel", bordeCambio.rectTransform, titan, "Nivel 2", 44, Hx("#365F73"), new Vector2(0, -75), new Vector2(700, 60));
        nombreCambio = SD.Txt("Nombre", bordeCambio.rectTransform, titan, "Ruta de Energía", 60, Hx("#18392B"), new Vector2(0, -150), new Vector2(700, 80));
        Pista(vistaCambio, nunito, manoDer, new Vector2(-330, -330), "<b>Mano derecha: sí, ir</b>", Hx("#F4C95D"));
        Pista(vistaCambio, nunito, manoIzq, new Vector2(330, -330), "<b>Mano izquierda: no, volver</b>", Hx("#E6ECF5"));
        vistaCambio.gameObject.SetActive(false);

        // ---- Vista: cambiar dificultad
        vistaDificultad = Hijo("VistaDificultad", raiz);
        SD.Txt("Titulo", vistaDificultad, titan, "Cambiar dificultad", 86, Hx("#F4C95D"), new Vector2(0, 360), new Vector2(1400, 120));
        SD.Txt("Subtitulo", vistaDificultad, nunito, "Inclínate a la izquierda o a la derecha para elegir", 36, Hx("#E6ECF5"), new Vector2(0, 270), new Vector2(1400, 60));
        notaDificultad = SD.Txt("Nota", vistaDificultad, nunito, "", 30, Hx("#F4C95D"), new Vector2(0, 215), new Vector2(1400, 50));
        for (int i = 0; i < 3; i++)
        {
            int indice = i;
            var borde = SD.Img("Tarjeta" + SD.Nombres[i], vistaDificultad, redondo, Hx("#F7D98C"));
            borde.type = Image.Type.Sliced; borde.raycastTarget = true;
            var rt = borde.rectTransform;
            rt.sizeDelta = new Vector2(440, 360);
            rt.anchoredPosition = new Vector2((i - 1) * 520f, -10f);
            var b = borde.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { if (indice == seleccion) Confirmar(); else Elegir(indice); });
            var relleno = SD.Img("Relleno", rt, redondo, Hx("#FFFCF6"));
            relleno.type = Image.Type.Sliced;
            SD.Estirar(relleno.rectTransform, 8);
            nombresDif[i] = SD.Txt("Nombre", rt, titan, SD.Nombres[i], 64, Hx("#18392B"), new Vector2(0, 100), new Vector2(400, 90));
            descDif[i] = SD.Txt("Descripcion", rt, nunito, SD.Descripciones[i], 28, Hx("#18392B"), new Vector2(0, -55), new Vector2(380, 200));
            tarjetas[i] = rt; bordes[i] = borde; fondos[i] = relleno;
        }
        var et = SD.Img("Actual", tarjetas[1], redondo, Hx("#EF8354"));
        et.type = Image.Type.Sliced;
        etiquetaActual = et.rectTransform;
        etiquetaActual.sizeDelta = new Vector2(170, 48);
        etiquetaActual.anchoredPosition = new Vector2(0, 180);
        SD.Txt("Texto", etiquetaActual, nunito, "<b>Actual</b>", 24, UnityEngine.Color.white, Vector2.zero, new Vector2(160, 44));

        var a1 = Pista(vistaDificultad, nunito, manoDer, new Vector2(-330, -300), "<b>Mano derecha: confirmar</b>", Hx("#F4C95D"));
        var a2 = Pista(vistaDificultad, nunito, manoIzq, new Vector2(330, -300), "<b>Mano izquierda: volver</b>", Hx("#E6ECF5"));
        avisos = new[] { a1 };

        canvas.gameObject.SetActive(false);
    }

    RectTransform TarjetaGesto(RectTransform padre, Sprite redondo, TMP_FontAsset titan, TMP_FontAsset nunito, Vector2 pos,
                               Sprite icono, Vector2 tamIcono, string titulo, string gesto, UnityEngine.Events.UnityAction alClic)
    {
        var borde = SD.Img("Tarjeta" + titulo, padre, redondo, Hx("#4F8A5B"));
        borde.type = Image.Type.Sliced; borde.raycastTarget = true;
        var rt = borde.rectTransform;
        rt.sizeDelta = new Vector2(420, 400);
        rt.anchoredPosition = pos;
        var b = borde.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { if (Time.unscaledTime >= listoEn) alClic(); });
        var relleno = SD.Img("Relleno", rt, redondo, Hx("#FFFCF6"));
        relleno.type = Image.Type.Sliced;
        SD.Estirar(relleno.rectTransform, 6);
        if (icono != null)
        {
            var img = SD.Img("Gesto", rt, icono, UnityEngine.Color.white);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = tamIcono;
            img.rectTransform.anchoredPosition = new Vector2(0, 80);
        }
        SD.Txt("Titulo", rt, titan, titulo, 46, Hx("#18392B"), new Vector2(0, -50), new Vector2(390, 70));
        var g = SD.Img("Pildora", rt, redondo, Hx("#F4C95D"));
        g.type = Image.Type.Sliced;
        g.rectTransform.sizeDelta = new Vector2(380, 64);
        g.rectTransform.anchoredPosition = new Vector2(0, -135);
        SD.Txt("Texto", g.rectTransform, nunito, "<b>" + gesto + "</b>", 28, Hx("#18392B"), Vector2.zero, new Vector2(360, 56));
        return rt;
    }

    TextMeshProUGUI Pista(RectTransform padre, TMP_FontAsset fuente, Sprite mano, Vector2 pos, string texto, Color color)
    {
        if (mano != null)
        {
            var img = SD.Img("Mano", padre, mano, UnityEngine.Color.white);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(56, 78);
            img.rectTransform.anchoredPosition = pos + new Vector2(-230, 0);
        }
        return SD.Txt("Aviso", padre, fuente, texto, 32, color, pos + new Vector2(30, 0), new Vector2(460, 56));
    }

    static RectTransform Hijo(string nombre, RectTransform padre)
    {
        var rt = (RectTransform)new GameObject(nombre, typeof(RectTransform)).transform;
        rt.SetParent(padre, false);
        SD.Estirar(rt);
        return rt;
    }

    static Sprite cacheCirculo;
    static Sprite Circulo()
    {
        if (cacheCirculo != null) return cacheCirculo;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                float a = Mathf.Clamp01(n / 2f - d) * Mathf.Clamp01(d - (n / 2f - 9f));   // aro de 9 px
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return cacheCirculo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Color Hx(string h) => SD.Hex(h);
}
