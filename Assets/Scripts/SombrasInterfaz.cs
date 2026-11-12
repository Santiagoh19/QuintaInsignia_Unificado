using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// La interfaz de Sombras en la carpa: inicio, dos pantallas cortas de instrucciones, el HUD del juego
// y la pantalla final con la graduacion y la insignia. Sigue el estilo grafico del grupo: pildoras de colores,
// crema, mostaza y azul oscuro para los textos.
//
// Se maneja con el mouse y TAMBIEN con la mano: sostener la PALMA frente a la camara
// presiona el boton principal (Jugar, Siguiente, ¡A jugar!, Volver a jugar),
// asi nadie necesita tocar el computador.
//
// La partida (serie, niveles, fogata) la lleva SombrasJuego; esta clase solo la muestra.
//
// Se construye con clic derecho en el titulo de este componente -> "Construir interfaz"
// (el menu "Sombras" -> "Crear escena de sombras" ya lo hace).
public class SombrasInterfaz : MonoBehaviour
{
    [Header("El juego (de donde sale el gesto que estas haciendo)")]
    public SombrasJuego juego;

    [Header("Pantallas (las pone el boton de construir)")]
    public GameObject pantallaInicio;
    public GameObject pantallaInstrucciones;
    public GameObject pantallaJuego;
    public GameObject pantallaFinal;
    public GameObject[] paginas;          // las dos paginas de instrucciones

    [Header("Piezas que cambian (las pone el boton de construir)")]
    public Button botonJugar;
    public Button botonAtras;
    public Button botonSiguiente;
    public Button botonInicio;
    public Button botonOtraVez;
    public Button botonInicioFinal;
    public Text textoTituloInstrucciones;
    public Text textoSiguiente;
    public Image[] puntos;                // los puntos de "pagina 1, 2"
    public Image barraPalmaInicio;        // se llenan mientras sostienes la palma
    public Image barraPalmaInstrucciones;
    public Image barraPalmaFinal;
    public Image[] marcosSenas;           // instrucciones 1: se iluminan y suenan con tu seña
    public Text textoNivel;
    public Text textoPalabra;             // HUD: la palabra que vas formando ("CA · MI · _")
    public Text textoFinalPalabras;       // final: las palabras que dijiste
    public Text textoAhora;
    public Image barraSostener;           // HUD: cuanto llevas sosteniendo la seña que toca
    public RectTransform filaSerie;       // donde van las tarjetas de la serie en el HUD
    public Image rellenoFogata;
    public GameObject panelMensaje;       // "¡Nivel superado!" y "¡Se apagó la fogata!"
    public Text textoMensajeTitulo;
    public Text textoMensajeDetalle;
    public RawImage vistaCamara;          // HUD: la ventanita con la camara del jugador
    public GameObject avisoCamara;        // "Prende la cámara" cuando Python no manda imagen

    [Header("Formas (las genera el boton de construir)")]
    public Sprite spriteRedondo;
    public Sprite spriteCirculo;
    public Sprite spriteTriangulo;
    public Sprite spritePentagono;

    [Header("Estilo del grupo: Quinta Insignia (Figma ESTETICA PROYECTO)")]
    [Tooltip("Titulos y botones. Si esta vacio, el boton de construir lo carga de Assets/UI/Fuentes")]
    public Font fuenteTitulos;            // Titan One
    public Font fuenteCuerpo;             // Nunito Sans Regular
    public Font fuenteCuerpoNegrita;      // Nunito Sans ExtraBold
    [Tooltip("La medalla dorada del nivel 5 (Assets/UI/Grupo/Medalla_Nivel5.png)")]
    public Sprite medalla;
    public Button botonSalir;
    [Tooltip("Lo que hace el boton Salir. Vacio = cierra el juego. En el ejecutable unificado, conectalo al menu")]
    public UnityEngine.Events.UnityEvent alSalir;
    [HideInInspector] public int versionInterfaz;   // si es menor que VERSION, la interfaz se reconstruye

    [Header("Palma")]
    public float tiempoPalma = 1.5f;     // segundos sosteniendo la palma para presionar el boton


    // sube este numero cuando cambie el diseno: las escenas guardadas con uno menor se reconstruyen solas
    public const int VERSION = 3;

    // ---- los colores del grupo: la paleta de IDENTIDAD en el Figma "ESTETICA PROYECTO" ----
    static readonly Color TINTA = Hex(0x18392B);         // verde bosque profundo: los textos
    static readonly Color VERDE_OSCURO = Hex(0x254D32);  // verde bosque: bordes de tarjetas y placas
    static readonly Color MOSTAZA = Hex(0xF4C95D);       // amarillo aventura: el boton principal
    static readonly Color DORADO = Hex(0xD99A27);        // amarillo dorado: el triangulo del logo
    static readonly Color LILA = Hex(0xB7A1D0);          // lavanda: Volver a jugar, Atras, x2
    static readonly Color VERDE = Hex(0x4F8A5B);         // verde naturaleza: Inicio y lo ya hecho
    static readonly Color VERDE_CLARO = Hex(0xA8C686);   // verde salvia: la seña que estas haciendo
    static readonly Color CREMA = Hex(0xFFF4D6);         // amarillo crema: tarjetas y paneles
    static readonly Color NARANJA = Hex(0xEF8354);       // naranja explorador: el pentagono y el fuego
    static readonly Color ROJO = Hex(0xB94A48);          // error: el boton Salir
    static readonly Color AZUL_LOGO = Hex(0x203A43);     // azul petroleo oscuro: "Quinta"
    static readonly Color AZUL_INSIGNIA = Hex(0x4B91B5); // azul exploracion: "Insignia"
    static readonly Color PIEL = new Color(0.94f, 0.74f, 0.58f);
    static readonly Color MADERA = Hex(0x70503A);        // cafe madera
    static readonly Color SOMBRA = new Color(0.18f, 0.12f, 0.16f, 0.9f);

    static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }

    // ---- las señas: como se dibuja cada una (pulgar a menique, 1 = arriba) y su nombre bonito ----
    // van en el mismo orden que SombrasJuego.NOMBRES
    static readonly string[] DIBUJOS = { "11111", "01100", "01001", "10000", "10001" };
    static readonly string[] BONITOS = { "Palma", "Paz", "Cuernos", "Pulgar arriba", "Llámame" };

    // ---- lo que pasa ahora ----
    int pagina = 0;
    float palmaSostenida = 0f;
    bool esperarSoltar = true;           // despues de presionar con la palma, hay que soltarla
    string gestoAnterior = "—";          // para que la seña suene una vez al hacerla, no en cada cuadro

    // la serie del HUD (la manda SombrasJuego)
    string[] serie = new string[0];
    bool[] dobles = new bool[0];
    int paso = 0;
    Image[] marcosSerie = new Image[0];
    GameObject[] chulosSerie = new GameObject[0];

    // un texto corto de felicitacion ("¡Muy bien!") que tapa a "Ahora toca" un momento
    string celebracion = "";
    float tiempoCelebracion = 0f;

    Font fuente;


    void Start()
    {
        PrepararEstilo();

        // por si no se construyo desde el editor, o es de una version anterior del diseno
        if (NecesitaReconstruir())
        {
            ConstruirInterfaz();
        }

        if (juego == null)
        {
            juego = FindFirstObjectByType<SombrasJuego>();
        }

        // los botones se conectan aqui (lo que se conecta en el editor por codigo no se guarda)
        botonJugar.onClick.AddListener(IrAInstrucciones);
        botonAtras.onClick.AddListener(Atras);
        botonSiguiente.onClick.AddListener(Siguiente);
        botonInicio.onClick.AddListener(IrAlInicio);
        botonOtraVez.onClick.AddListener(VolverAlInicioDeTodo);
        botonInicioFinal.onClick.AddListener(IrAlInicio);
        botonSalir.onClick.AddListener(Salir);

        PrepararFinalConManos();
        IrAlInicio();
    }

    // ---- JUEGO UNIFICADO: pantalla final manejada con la mano derecha ----
    //   mano derecha abierta al frente (sostenida) -> Volver a jugar (al inicio de todo el juego)
    //   mano derecha en puño al frente (sostenida)  -> Salir
    Image barraSalirFinal;
    float derechaSostenida, punoSostenido;
    bool esperarSoltarFinal = true;

    void PrepararFinalConManos()
    {
        var pista = pantallaFinal.transform.Find("Pista");
        if (pista != null && pista.GetComponent<Text>() != null) pista.GetComponent<Text>().text = "Levanta la mano derecha";
        if (pantallaFinal.transform.Find("PistaSalir") == null)
        {
            barraSalirFinal = BarraPalma(pantallaFinal.transform, new Vector2(290f, -428f), 400f);
            Texto("PistaSalir", pantallaFinal.transform, new Vector2(290f, -470f), new Vector2(560f, 46f),
                  "Levanta la mano derecha con el puño cerrado", 30, Color.white, FontStyle.Bold);
        }
    }

    public void VolverAlInicioDeTodo()
    {
        EstadoJuego.Reiniciar();
        EstadoJuego.InsigniasGanadas = 0;
        Transicion.Cargar(NivelesUnificados.EscenaHistoria);
    }

    void FinalConManos()
    {
        var pose = PoseReceiver.Instancia;
        bool derecha = pose != null && pose.Activo && pose.ManoDerecha;
        bool puno = pose != null && pose.Activo && pose.ManoDerechaPuno;

        if (esperarSoltarFinal)
        {
            if (!derecha && !puno) esperarSoltarFinal = false;   // al llegar a esta pantalla hay que bajar la mano primero
            derechaSostenida = punoSostenido = 0f;
        }
        else
        {
            derechaSostenida = derecha ? derechaSostenida + Time.deltaTime : 0f;
            punoSostenido = puno ? punoSostenido + Time.deltaTime : 0f;
            if (derechaSostenida >= tiempoPalma) { esperarSoltarFinal = true; VolverAlInicioDeTodo(); }
            else if (punoSostenido >= tiempoPalma) { esperarSoltarFinal = true; Salir(); }
        }
        barraPalmaFinal.fillAmount = derechaSostenida / tiempoPalma;
        if (barraSalirFinal != null) barraSalirFinal.fillAmount = punoSostenido / tiempoPalma;
    }

    // la interfaz guardada en la escena es de un diseno anterior (o le falta algo)
    public bool NecesitaReconstruir()
    {
        return versionInterfaz < VERSION || pantallaInicio == null || pantallaFinal == null || vistaCamara == null ||
               textoPalabra == null || textoFinalPalabras == null || botonSalir == null ||
               paginas == null || paginas.Length != 2;
    }

    // el boton Salir: si alguien lo conecto (el menu del ejecutable unificado) hace eso; si no, cierra el juego
    public void Salir()
    {
        if (alSalir != null && alSalir.GetPersistentEventCount() > 0)
        {
            alSalir.Invoke();
            return;
        }
        // JUEGO UNIFICADO: "Volver a jugar" ya lleva al inicio de todo; "Salir" cierra el juego
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    void Update()
    {
        if (Pausa.Activa) return;   // JUEGO UNIFICADO: menú de pausa general abierto
        string gesto = "—";
        if (juego != null)
        {
            gesto = juego.GestoActual();
        }

        bool enMenu = pantallaInicio.activeSelf || pantallaInstrucciones.activeSelf || pantallaFinal.activeSelf;
        if (pantallaFinal.activeSelf)
        {
            FinalConManos();
        }
        else
        {
            esperarSoltarFinal = true;
            if (enMenu) PalmaPresiona(gesto);
        }

        if (pantallaInstrucciones.activeSelf && pagina == 0)
        {
            IluminarSenas(gesto);
        }
        gestoAnterior = gesto;

        if (pantallaJuego.activeSelf)
        {
            ActualizarSerie(gesto);
        }
    }


    // =====================================================================
    // NAVEGACION
    // =====================================================================

    void MostrarPantalla(GameObject cual)
    {
        pantallaInicio.SetActive(cual == pantallaInicio);
        pantallaInstrucciones.SetActive(cual == pantallaInstrucciones);
        pantallaJuego.SetActive(cual == pantallaJuego);
        pantallaFinal.SetActive(cual == pantallaFinal);
        palmaSostenida = 0f;
        esperarSoltar = true;
    }

    public void IrAlInicio()
    {
        if (juego != null)
        {
            juego.DetenerPartida();
        }
        MostrarPantalla(pantallaInicio);
    }

    public void IrAInstrucciones()
    {
        pagina = 0;
        MostrarPagina();
        MostrarPantalla(pantallaInstrucciones);
    }

    // empieza la partida (desde las instrucciones o desde "Volver a jugar")
    public void EmpezarJuego()
    {
        OcultarMensaje();
        MostrarPantalla(pantallaJuego);
        if (juego != null)
        {
            juego.EmpezarPartida();
        }
    }

    // la llama SombrasJuego al pasar el ultimo nivel, con las palabras que dijiste
    public void MostrarFinal(string[] dichas)
    {
        textoFinalPalabras.text = "Tus señas secretas: " + string.Join("  ·  ", dichas);
        MostrarPantalla(pantallaFinal);
        // JUEGO UNIFICADO: misma ventana de insignia que los otros niveles, encima de la pantalla final
        NivelesUnificados.GanarInsigniaNivel5();
    }

    void Siguiente()
    {
        if (pagina < paginas.Length - 1)
        {
            pagina = pagina + 1;
            MostrarPagina();
            palmaSostenida = 0f;
            esperarSoltar = true;
        }
        else
        {
            EmpezarJuego();
        }
    }

    void Atras()
    {
        if (pagina > 0)
        {
            pagina = pagina - 1;
            MostrarPagina();
        }
        else
        {
            IrAlInicio();
        }
    }

    // muestra la pagina de instrucciones que toca, con su titulo y sus puntos
    void MostrarPagina()
    {
        string[] titulos = { "Cada seña suena", "Forma la palabra" };
        textoTituloInstrucciones.text = titulos[pagina];

        for (int i = 0; i < paginas.Length; i++)
        {
            paginas[i].SetActive(i == pagina);

            if (i == pagina)
            {
                puntos[i].color = MOSTAZA;
            }
            else
            {
                puntos[i].color = new Color(TINTA.r, TINTA.g, TINTA.b, 0.25f);
            }
        }

        if (pagina == paginas.Length - 1)
        {
            textoSiguiente.text = "¡A jugar!";
        }
        else
        {
            textoSiguiente.text = "Siguiente";
        }
    }


    // ---- sostener la palma presiona el boton principal de la pantalla ----
    // la barra debajo del boton se va llenando; al soltar, se vacia
    void PalmaPresiona(string gesto)
    {
        if (esperarSoltar)
        {
            // recien se presiono (o se cambio de pantalla): hay que bajar la palma primero
            if (gesto != "PALMA")
            {
                esperarSoltar = false;
            }
            palmaSostenida = 0f;
        }
        else if (gesto == "PALMA")
        {
            palmaSostenida = palmaSostenida + Time.deltaTime;

            if (palmaSostenida >= tiempoPalma)
            {
                palmaSostenida = 0f;
                esperarSoltar = true;

                if (pantallaInicio.activeSelf)
                {
                    IrAInstrucciones();
                }
                else if (pantallaFinal.activeSelf)
                {
                    EmpezarJuego();
                }
                else
                {
                    Siguiente();
                }
            }
        }
        else
        {
            palmaSostenida = 0f;
        }

        float lleno = palmaSostenida / tiempoPalma;
        barraPalmaInicio.fillAmount = lleno;
        barraPalmaInstrucciones.fillAmount = lleno;
    }


    // ---- instrucciones 1: la tarjeta de la seña que haces se ilumina en verde y suena su silaba ----
    void IluminarSenas(string gesto)
    {
        if (gesto != gestoAnterior && juego != null && juego.sonidos != null)
        {
            juego.sonidos.TocarSena(gesto, false);   // si no es una seña, no suena nada
        }

        for (int i = 0; i < marcosSenas.Length; i++)
        {
            if (gesto == SombrasJuego.NOMBRES[i])
            {
                marcosSenas[i].color = VERDE_CLARO;
            }
            else
            {
                marcosSenas[i].color = Color.clear;
            }
        }
    }


    // =====================================================================
    // EL HUD: lo que SombrasJuego le pide a la interfaz
    // =====================================================================

    // pone en la fila de arriba las tarjetas de la serie, cada una con su silaba.
    // Las que piden las dos manos llevan un "x2"
    public void MostrarSerie(string[] gestos, bool[] sonDobles, string[] silabas, int pasoActual)
    {
        serie = gestos;
        dobles = sonDobles;
        paso = pasoActual;

        // se borran las tarjetas anteriores
        for (int i = filaSerie.childCount - 1; i >= 0; i--)
        {
            Destroy(filaSerie.GetChild(i).gameObject);
        }

        marcosSerie = new Image[gestos.Length];
        chulosSerie = new GameObject[gestos.Length];

        Vector2 tam = new Vector2(150f, 180f);
        float separacion = 24f;
        float total = gestos.Length * tam.x + (gestos.Length - 1) * separacion;

        for (int i = 0; i < gestos.Length; i++)
        {
            float x = -total / 2f + tam.x / 2f + i * (tam.x + separacion);
            Image marco;
            RectTransform tarjeta = Tarjeta(filaSerie, new Vector2(x, 0f), tam, gestos[i], out marco, silabas[i]);
            marcosSerie[i] = marco;
            chulosSerie[i] = Chulo(tarjeta, new Vector2(tam.x / 2f - 8f, tam.y / 2f - 8f), 46f);

            if (dobles[i])
            {
                Image doble = Caja("Doble", tarjeta, new Vector2(-tam.x / 2f + 10f, tam.y / 2f - 10f), new Vector2(54f, 54f), spriteCirculo, LILA, 0f);
                Borde(doble, Color.white, 3f);
                Texto("Texto", doble.transform, Vector2.zero, new Vector2(54f, 54f), "x2", 26, TINTA, FontStyle.Bold);
            }
        }
    }

    public void FijarPaso(int pasoActual)
    {
        paso = pasoActual;
    }

    // la palabra que vas formando, arriba en el centro
    public void FijarPalabra(string texto)
    {
        textoPalabra.text = texto;
    }

    public void FijarNivel(int nivel, int total)
    {
        // "Ronda" y no "Nivel": para el grupo, todo este juego es el NIVEL 5
        textoNivel.text = "Ronda " + nivel + "/" + total;
    }

    // 1 = fogata llena, 0 = apagada
    public void FijarFogata(float cuanto)
    {
        rellenoFogata.fillAmount = Mathf.Clamp01(cuanto);
    }

    // 0 = no la estas haciendo, 1 = ya cuenta
    public void FijarSostenido(float cuanto)
    {
        barraSostener.fillAmount = Mathf.Clamp01(cuanto);
    }

    // un mensaje grande en el centro: "¡Nivel 1 superado!", "¡Se apagó la fogata!"
    public void MostrarMensaje(string titulo, string detalle)
    {
        textoMensajeTitulo.text = titulo;
        textoMensajeDetalle.text = detalle;
        panelMensaje.SetActive(true);
    }

    public void OcultarMensaje()
    {
        panelMensaje.SetActive(false);
    }

    // un texto corto de felicitacion en lugar de "Ahora toca", por un segundo
    public void Celebrar(string texto)
    {
        celebracion = texto;
        tiempoCelebracion = 1f;
    }


    // marca las tarjetas: verdes las hechas, mostaza la que toca (verde claro si ya la estas haciendo)
    public void ActualizarSerie(string gesto)
    {
        bool haciendola = juego != null && juego.HaciendoLaQueToca();

        for (int i = 0; i < marcosSerie.Length; i++)
        {
            chulosSerie[i].SetActive(i < paso);

            if (i < paso)
            {
                marcosSerie[i].color = VERDE;
            }
            else if (i == paso && haciendola)
            {
                marcosSerie[i].color = VERDE_CLARO;
            }
            else if (i == paso)
            {
                // la que toca late, para que se encuentre facil
                float latido = 0.6f + 0.4f * Mathf.Sin(Time.time * 5f);
                marcosSerie[i].color = new Color(MOSTAZA.r, MOSTAZA.g, MOSTAZA.b, latido);
            }
            else
            {
                marcosSerie[i].color = Color.clear;
            }
        }

        tiempoCelebracion = tiempoCelebracion - Time.deltaTime;

        if (tiempoCelebracion > 0f)
        {
            textoAhora.text = celebracion;
        }
        else if (paso >= serie.Length)
        {
            textoAhora.text = "¡Serie completa!";
        }
        else if (haciendola)
        {
            textoAhora.text = "¡Así! Sostén la seña";
        }
        else if (juego != null && juego.PideDosManos())
        {
            textoAhora.text = Bonito(serie[paso]) + " con LAS DOS MANOS";
        }
        else if (juego != null && juego.AyudaUnaMano())
        {
            textoAhora.text = Bonito(serie[paso]) + " (una mano también vale)";
        }
        else
        {
            textoAhora.text = "Ahora toca: " + Bonito(serie[paso]);
        }
    }


    // =====================================================================
    // CONSTRUIR LA INTERFAZ
    // =====================================================================
    [ContextMenu("Construir interfaz")]
    public void ConstruirInterfaz()
    {
        PrepararEstilo();

        // si ya habia una interfaz construida, se borra para no duplicarla
        string[] viejos = { "Canvas", "EventSystem" };
        for (int i = 0; i < viejos.Length; i++)
        {
            Transform viejo = transform.Find(viejos[i]);
            if (viejo != null)
            {
                Borrar(viejo.gameObject);
            }
        }

        if (juego == null)
        {
            juego = FindFirstObjectByType<SombrasJuego>();
        }

        CargarFormas();

        // el lienzo: se ve encima de todo y se escala segun la pantalla (disenado para 1920 x 1080)
        GameObject lienzo = new GameObject("Canvas", typeof(RectTransform));
        lienzo.transform.SetParent(transform, false);
        Canvas canvas = lienzo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler escalador = lienzo.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;
        lienzo.AddComponent<GraphicRaycaster>();

        // para que los botones reciban el mouse (el proyecto usa el Input System nuevo)
        GameObject eventos = new GameObject("EventSystem");
        eventos.transform.SetParent(transform, false);
        eventos.AddComponent<EventSystem>();
        InputSystemUIInputModule modulo = eventos.AddComponent<InputSystemUIInputModule>();
        modulo.AssignDefaultActions();

        pantallaInicio = ConstruirInicio(lienzo.transform);
        pantallaInstrucciones = ConstruirInstrucciones(lienzo.transform);
        pantallaJuego = ConstruirHUD(lienzo.transform);
        pantallaFinal = ConstruirFinal(lienzo.transform);

        pantallaInstrucciones.SetActive(false);
        pantallaJuego.SetActive(false);
        pantallaFinal.SetActive(false);

        // la camara del jugador: escucha la imagen que manda Python y la pinta en la ventanita del HUD.
        // Va en este objeto (siempre prendido) y no en el HUD, para no cerrar el puerto en cada cambio de pantalla
        CamaraJugador camara = GetComponent<CamaraJugador>();
        if (camara == null)
        {
            camara = gameObject.AddComponent<CamaraJugador>();
        }
        camara.pantalla = vistaCamara;
        camara.avisoSinCamara = avisoCamara;

        versionInterfaz = VERSION;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
        Debug.Log("[Sombras] Listo: interfaz construida. Guarda la escena con Ctrl+S.");
    }


    // ---- pantalla de inicio ----
    GameObject ConstruirInicio(Transform lienzo)
    {
        RectTransform p = Pantalla("Pantalla_Inicio", lienzo);
        Caja("Velo", p, Vector2.zero, new Vector2(1920f, 1080f), null, new Color(0f, 0f, 0f, 0.2f), 0f);

        // el logo del grupo: "Quinta Insignia" en una placa, con el triangulo y el pentagono
        Image logo = Caja("Logo_Quinta_Insignia", p, new Vector2(0f, 360f), new Vector2(1040f, 190f), spriteRedondo, Color.white, 95f);
        Borde(logo, VERDE_OSCURO, 4f);
        Sombra(logo);
        Text textoLogo = Texto("Texto", logo.transform, new Vector2(0f, 6f), new Vector2(780f, 160f), "", 100, AZUL_LOGO, FontStyle.Bold);
        textoLogo.supportRichText = true;
        textoLogo.text = "Quinta <color=#" + ColorUtility.ToHtmlStringRGB(AZUL_INSIGNIA) + ">Insignia</color>";
        Caja("Triangulo", logo.transform, new Vector2(-445f, 0f), new Vector2(64f, 56f), spriteTriangulo, DORADO, 0f);
        Caja("Pentagono", logo.transform, new Vector2(445f, 0f), new Vector2(58f, 58f), spritePentagono, NARANJA, 0f);

        // la placa de la mision: el nombre de este nivel
        Image placa = Caja("Placa_Mision", p, new Vector2(0f, 195f), new Vector2(820f, 100f), spriteRedondo, CREMA, 50f);
        Borde(placa, VERDE_OSCURO, 3f);
        Sombra(placa);
        Texto("Texto", placa.transform, Vector2.zero, new Vector2(780f, 90f), "Nivel 5 · Sombras en la carpa", 50, TINTA, FontStyle.Bold);

        // Lucas habla (globo de dialogo)
        Image etiqueta = Caja("Etiqueta", p, new Vector2(-640f, 70f), new Vector2(170f, 60f), spriteRedondo, MOSTAZA, 30f);
        Borde(etiqueta, VERDE_OSCURO, 3f);
        Texto("Texto", etiqueta.transform, Vector2.zero, new Vector2(160f, 56f), "Lucas", 34, TINTA, FontStyle.Bold);
        Image globo = Caja("Globo", p, new Vector2(-430f, -45f), new Vector2(660f, 190f), spriteRedondo, Color.white, 45f);
        Borde(globo, VERDE_OSCURO, 3f);
        Sombra(globo);
        Image cola = Caja("Cola", p, new Vector2(-600f, -137f), new Vector2(46f, 46f), null, Color.white, 0f);
        cola.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Texto("Texto", globo.transform, Vector2.zero, new Vector2(600f, 170f),
              "¡Aprende las señas secretas y gana la Quinta Insignia!", 40, TINTA, FontStyle.Bold);

        // el boton Jugar, con la barra de la palma debajo
        botonJugar = Boton("Boton_Jugar", p, new Vector2(0f, -330f), new Vector2(440f, 140f), "Jugar", 72, MOSTAZA);
        barraPalmaInicio = BarraPalma(p, new Vector2(0f, -428f), 440f);
        Texto("Pista", p, new Vector2(0f, -470f), new Vector2(1000f, 50f),
              "o sostén la PALMA frente a la cámara", 32, Color.white, FontStyle.Bold);

        return p.gameObject;
    }


    // ---- instrucciones: un panel crema con dos paginas cortas ----
    GameObject ConstruirInstrucciones(Transform lienzo)
    {
        RectTransform p = Pantalla("Pantalla_Instrucciones", lienzo);
        Caja("Velo", p, Vector2.zero, new Vector2(1920f, 1080f), null, new Color(0f, 0f, 0f, 0.45f), 0f);

        Image panel = Caja("Panel", p, new Vector2(0f, -10f), new Vector2(1560f, 860f), spriteRedondo, CREMA, 50f);
        Borde(panel, VERDE_OSCURO, 4f);
        Sombra(panel);

        Image encabezado = Caja("Encabezado", p, new Vector2(0f, 405f), new Vector2(680f, 110f), spriteRedondo, Color.white, 55f);
        Borde(encabezado, VERDE_OSCURO, 3f);
        Sombra(encabezado);
        textoTituloInstrucciones = Texto("Texto", encabezado.transform, Vector2.zero, new Vector2(660f, 100f),
                                         "Cada seña suena", 54, TINTA, FontStyle.Bold);

        puntos = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            puntos[i] = Caja("Punto_" + (i + 1), p, new Vector2(-20f + 40f * i, 325f), new Vector2(24f, 24f), spriteCirculo, TINTA, 0f);
        }

        paginas = new GameObject[2];
        paginas[0] = PaginaSenas(p);
        paginas[1] = PaginaPalabra(p);

        // los botones de abajo
        botonAtras = Boton("Boton_Atras", p, new Vector2(-560f, -350f), new Vector2(300f, 110f), "Atrás", 50, LILA);
        botonSiguiente = Boton("Boton_Siguiente", p, new Vector2(540f, -350f), new Vector2(380f, 120f), "Siguiente", 54, MOSTAZA);
        textoSiguiente = botonSiguiente.GetComponentInChildren<Text>();
        barraPalmaInstrucciones = BarraPalma(p, new Vector2(540f, -425f), 380f);
        Texto("Pista", p, new Vector2(0f, -350f), new Vector2(560f, 50f),
              "o sostén la PALMA para seguir", 30, new Color(TINTA.r, TINTA.g, TINTA.b, 0.7f), FontStyle.Bold);

        MostrarPaginaEnEditor();
        return p.gameObject;
    }

    // pagina 1: las cinco señas con su silaba. Se iluminan y suenan cuando las haces
    GameObject PaginaSenas(RectTransform padre)
    {
        RectTransform p = Pantalla("Pagina_1", padre);

        Texto("Explicacion", p, new Vector2(0f, 240f), new Vector2(1400f, 60f),
              "Cada seña es una sílaba secreta de los exploradores.", 40, TINTA, FontStyle.Bold);

        marcosSenas = new Image[5];
        Vector2 tam = new Vector2(230f, 270f);
        for (int i = 0; i < 5; i++)
        {
            float x = -560f + 280f * i;
            Image marco;
            Tarjeta(p, new Vector2(x, 45f), tam, SombrasJuego.NOMBRES[i], out marco);
            marcosSenas[i] = marco;

            Image silaba = Caja("Silaba_" + i, p, new Vector2(x, -135f), new Vector2(150f, 70f), spriteRedondo, MOSTAZA, 35f);
            Sombra(silaba);
            Texto("Texto", silaba.transform, Vector2.zero, new Vector2(150f, 70f), SombrasJuego.SILABAS[i], 44, TINTA, FontStyle.Bold);
        }

        Texto("Prueba", p, new Vector2(0f, -240f), new Vector2(1400f, 60f),
              "Pruébalas frente a la cámara: la tarjeta se pone verde y suena.", 34, new Color(TINTA.r, TINTA.g, TINTA.b, 0.75f), FontStyle.Normal);
        return p.gameObject;
    }

    // pagina 2: las señas en orden forman una palabra; x2 = las dos manos; la fogata
    GameObject PaginaPalabra(RectTransform padre)
    {
        RectTransform p = Pantalla("Pagina_2", padre);

        // a la izquierda: CA-MI-NO, con una hecha, una que toca y una que falta
        Vector2 tam = new Vector2(150f, 180f);
        string[] ejemplo = { "PALMA", "PAZ", "CUERNOS" };
        for (int i = 0; i < 3; i++)
        {
            Image marco;
            RectTransform t = Tarjeta(p, new Vector2(-560f + 180f * i, 140f), tam, ejemplo[i], out marco, SombrasJuego.SilabaDe(ejemplo[i]));
            if (i == 0)
            {
                marco.color = VERDE;
                Chulo(t, new Vector2(tam.x / 2f - 8f, tam.y / 2f - 8f), 46f);
            }
            else if (i == 1)
            {
                marco.color = MOSTAZA;
            }
            else
            {
                t.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
            }
        }
        Image palabra = Caja("Palabra", p, new Vector2(-380f, -5f), new Vector2(420f, 76f), spriteRedondo, Color.white, 38f);
        Sombra(palabra);
        Texto("Texto", palabra.transform, Vector2.zero, new Vector2(400f, 72f), "CA · _ · _", 44, TINTA, FontStyle.Bold);
        Texto("Orden", p, new Vector2(-380f, -90f), new Vector2(640f, 60f), "En orden, forman una palabra", 36, TINTA, FontStyle.Bold);

        // a la derecha: x2 = con las dos manos
        Mano(p, new Vector2(300f, 140f), 170f, "01100", PIEL, TINTA);
        Mano(p, new Vector2(470f, 140f), 170f, "01100", PIEL, TINTA);
        Image doble = Caja("Doble", p, new Vector2(385f, 255f), new Vector2(70f, 70f), spriteCirculo, LILA, 0f);
        Borde(doble, Color.white, 3f);
        Texto("Texto", doble.transform, Vector2.zero, new Vector2(70f, 70f), "x2", 32, TINTA, FontStyle.Bold);
        Texto("Dos_Manos", p, new Vector2(385f, -40f), new Vector2(640f, 60f), "x2 = con las dos manos", 36, TINTA, FontStyle.Bold);

        // abajo: sostener y la fogata, en una sola linea
        Texto("Sostener", p, new Vector2(0f, -210f), new Vector2(1400f, 60f),
              "Sostén cada seña un momento. Si la fogata se apaga, se vuelve a prender.", 34, TINTA, FontStyle.Normal);
        return p.gameObject;
    }

    // en el editor se deja visible solo la primera pagina
    void MostrarPaginaEnEditor()
    {
        for (int i = 0; i < paginas.Length; i++)
        {
            paginas[i].SetActive(i == 0);
            puntos[i].color = i == 0 ? MOSTAZA : new Color(TINTA.r, TINTA.g, TINTA.b, 0.25f);
        }
    }


    // ---- el HUD del juego ----
    GameObject ConstruirHUD(Transform lienzo)
    {
        RectTransform p = Pantalla("Pantalla_Juego", lienzo);

        // arriba: titulo pequeno, nivel y boton de inicio
        // arriba en el centro va la palabra que vas formando (la pone SombrasJuego)
        // la placa de la mision, como en los otros niveles: aqui es la palabra que vas formando
        Image titulo = Caja("Palabra", p, new Vector2(0f, 480f), new Vector2(760f, 90f), spriteRedondo, Color.white, 45f);
        Borde(titulo, VERDE_OSCURO, 3f);
        Sombra(titulo);
        Caja("Triangulo", titulo.transform, new Vector2(-335f, 0f), new Vector2(40f, 36f), spriteTriangulo, DORADO, 0f);
        Caja("Pentagono", titulo.transform, new Vector2(335f, 0f), new Vector2(38f, 38f), spritePentagono, NARANJA, 0f);
        textoPalabra = Texto("Texto", titulo.transform, Vector2.zero, new Vector2(600f, 86f), "CA · MI · NO", 50, AZUL_LOGO, FontStyle.Bold);

        Image nivel = Caja("Nivel", p, new Vector2(-790f, 480f), new Vector2(270f, 84f), spriteRedondo, LILA, 42f);
        Borde(nivel, VERDE_OSCURO, 3f);
        Sombra(nivel);
        textoNivel = Texto("Texto", nivel.transform, Vector2.zero, new Vector2(250f, 80f), "Ronda 1/3", 42, TINTA, FontStyle.Bold);

        botonInicio = BotonRedondo("Boton_Inicio", p, new Vector2(860f, 470f), 110f);

        // la fila de la serie (las tarjetas se ponen en MostrarSerie), lo que toca y cuanto llevas sosteniendo.
        // "Ahora toca" es una tarjeta de objetivo, como la del nivel 4
        filaSerie = Pantalla("Serie", p);
        filaSerie.anchoredPosition = new Vector2(0f, 330f);
        Image ahora = Caja("Ahora", p, new Vector2(0f, 185f), new Vector2(760f, 100f), spriteRedondo, CREMA, 30f);
        Borde(ahora, VERDE_OSCURO, 3f);
        Sombra(ahora);
        Texto("Etiqueta", ahora.transform, new Vector2(-250f, 32f), new Vector2(240f, 30f), "Objetivo actual", 22, new Color(TINTA.r, TINTA.g, TINTA.b, 0.6f), FontStyle.Normal)
            .alignment = TextAnchor.MiddleLeft;
        textoAhora = Texto("Texto", ahora.transform, new Vector2(0f, -8f), new Vector2(720f, 60f), "Ahora toca: Palma", 38, TINTA, FontStyle.Bold);
        barraSostener = BarraPalma(p, new Vector2(0f, 120f), 420f);

        // el mensaje del centro ("¡Nivel superado!", "¡Se apagó la fogata!"), escondido
        Image mensaje = Caja("Mensaje", p, new Vector2(0f, -40f), new Vector2(1000f, 260f), spriteRedondo, CREMA, 50f);
        Borde(mensaje, VERDE_OSCURO, 4f);
        Sombra(mensaje);
        textoMensajeTitulo = Texto("Titulo", mensaje.transform, new Vector2(0f, 45f), new Vector2(960f, 100f), "", 72, TINTA, FontStyle.Bold);
        textoMensajeDetalle = Texto("Detalle", mensaje.transform, new Vector2(0f, -55f), new Vector2(960f, 70f), "", 42, TINTA, FontStyle.Normal);
        panelMensaje = mensaje.gameObject;
        panelMensaje.SetActive(false);

        // abajo a la izquierda: el medidor de la fogata, un tronco que se llena de fuego
        Image medidor = Caja("Medidor_Fogata", p, new Vector2(-640f, -460f), new Vector2(580f, 100f), spriteRedondo, CREMA, 50f);
        Borde(medidor, VERDE_OSCURO, 3f);
        Sombra(medidor);
        Fogata(medidor.transform, new Vector2(-235f, 8f), 64f);
        Caja("Tronco", medidor.transform, new Vector2(45f, 0f), new Vector2(420f, 38f), spriteRedondo, MADERA, 19f);
        rellenoFogata = Caja("Fuego", medidor.transform, new Vector2(45f, 0f), new Vector2(420f, 38f), spriteRedondo, NARANJA, 19f);
        rellenoFogata.type = Image.Type.Filled;
        rellenoFogata.fillMethod = Image.FillMethod.Horizontal;
        rellenoFogata.fillAmount = 1f;
        Texto("Etiqueta", p, new Vector2(-640f, -390f), new Vector2(400f, 44f), "Fogata", 32, Color.white, FontStyle.Bold);

        // abajo a la derecha: la camara del jugador, para que vea sus manos (la pinta CamaraJugador).
        // 384 x 216 es 16:9, igual que el recorte que procesa MediaPipe
        Image marco = Caja("Camara", p, new Vector2(728f, -392f), new Vector2(404f, 236f), spriteRedondo, CREMA, 24f);
        Borde(marco, VERDE_OSCURO, 3f);
        Sombra(marco);
        Caja("Fondo", marco.transform, Vector2.zero, new Vector2(384f, 216f), null, TINTA, 0f);
        avisoCamara = Texto("Aviso", marco.transform, Vector2.zero, new Vector2(360f, 120f), "Prende la cámara\n(el emisor de Python)", 28, Color.white, FontStyle.Bold).gameObject;
        vistaCamara = Nuevo("Imagen", marco.transform, Vector2.zero, new Vector2(384f, 216f)).gameObject.AddComponent<RawImage>();
        vistaCamara.raycastTarget = false;
        Texto("Etiqueta_Camara", p, new Vector2(728f, -249f), new Vector2(400f, 44f), "Tu cámara", 32, Color.white, FontStyle.Bold);

        return p.gameObject;
    }


    // ---- pantalla final: la insignia ----
    GameObject ConstruirFinal(Transform lienzo)
    {
        RectTransform p = Pantalla("Pantalla_Final", lienzo);
        Caja("Velo", p, Vector2.zero, new Vector2(1920f, 1080f), null, new Color(0f, 0f, 0f, 0.4f), 0f);

        Image titulo = Caja("Titulo", p, new Vector2(0f, 420f), new Vector2(1240f, 130f), spriteRedondo, Color.white, 65f);
        Borde(titulo, VERDE_OSCURO, 4f);
        Sombra(titulo);
        Texto("Texto", titulo.transform, Vector2.zero, new Vector2(1180f, 120f), "¡Conseguiste la Quinta Insignia!", 68, AZUL_LOGO, FontStyle.Bold);

        // la medalla dorada del nivel 5 (la del grupo). Si no esta, un circulo dorado con la carpa
        Vector2 centro = new Vector2(0f, 95f);
        if (medalla != null)
        {
            Image m = Caja("Medalla", p, centro, new Vector2(400f, 370f), medalla, Color.white, 0f);
            m.preserveAspect = true;
        }
        else
        {
            Caja("Medalla_Borde", p, centro, new Vector2(400f, 400f), spriteCirculo, DORADO, 0f);
            Caja("Medalla", p, centro, new Vector2(360f, 360f), spriteCirculo, MOSTAZA, 0f);
            Caja("Carpa", p, centro + new Vector2(0f, 10f), new Vector2(200f, 140f), spriteTriangulo, DORADO, 0f);
        }

        Image cinta = Caja("Cinta", p, new Vector2(0f, -140f), new Vector2(620f, 80f), spriteRedondo, MOSTAZA, 40f);
        Borde(cinta, VERDE_OSCURO, 3f);
        Sombra(cinta);
        Texto("Texto", cinta.transform, Vector2.zero, new Vector2(590f, 72f), "Nivel 5 · Sombras en la carpa", 38, TINTA, FontStyle.Bold);

        textoFinalPalabras = Texto("Palabras", p, new Vector2(0f, -232f), new Vector2(1500f, 60f), "Tus señas secretas: CAMINO", 40, Color.white, FontStyle.Bold);

        botonOtraVez = Boton("Boton_OtraVez", p, new Vector2(-290f, -350f), new Vector2(480f, 120f), "Volver a jugar", 50, LILA);
        barraPalmaFinal = BarraPalma(p, new Vector2(-290f, -428f), 480f);
        Texto("Pista", p, new Vector2(-290f, -470f), new Vector2(700f, 46f), "o sostén la PALMA", 30, Color.white, FontStyle.Bold);
        botonSalir = Boton("Boton_Salir", p, new Vector2(290f, -350f), new Vector2(400f, 120f), "Salir", 54, ROJO);
        botonInicioFinal = BotonRedondo("Boton_Inicio", p, new Vector2(860f, -440f), 120f);

        return p.gameObject;
    }


    // =====================================================================
    // PIEZAS
    // =====================================================================

    // una tarjeta con el dibujo de la seña y su nombre. "marco" es el borde que se ilumina.
    // Con "silaba", la tarjeta muestra la silaba (grande) en vez del nombre
    RectTransform Tarjeta(Transform padre, Vector2 pos, Vector2 tam, string gesto, out Image marco, string silaba = null)
    {
        marco = Caja("Marco_" + gesto, padre, pos, tam + new Vector2(22f, 22f), spriteRedondo, Color.clear, 34f);
        Image tarjeta = Caja("Tarjeta_" + gesto, padre, pos, tam, spriteRedondo, CREMA, 26f);
        Borde(tarjeta, VERDE_OSCURO, 3f);
        Sombra(tarjeta);

        Mano(tarjeta.transform, new Vector2(0f, tam.y * 0.1f), tam.y * 0.58f, Dibujo(gesto), PIEL, TINTA);
        if (silaba != null)
        {
            Texto("Silaba", tarjeta.transform, new Vector2(0f, -tam.y * 0.34f), new Vector2(tam.x, 56f),
                  silaba, Mathf.RoundToInt(tam.y * 0.2f), TINTA, FontStyle.Bold);
        }
        else
        {
            Texto("Nombre", tarjeta.transform, new Vector2(0f, -tam.y * 0.36f), new Vector2(tam.x, 44f),
                  Bonito(gesto), Mathf.RoundToInt(tam.y * 0.13f), TINTA, FontStyle.Bold);
        }
        return tarjeta.rectTransform;
    }

    // ---- el dibujo de una mano: palma y cinco dedos que estan arriba o doblados ----
    // patron: pulgar a menique, '1' = arriba
    void Mano(Transform padre, Vector2 pos, float a, string patron, Color color, Color borde)
    {
        RectTransform caja = Nuevo("Dibujo_Mano", padre, pos, new Vector2(a * 0.8f, a));

        // los cuatro dedos van primero, para que la palma tape su base
        float[] xs = { 0f, -0.21f, -0.07f, 0.07f, 0.21f };
        float[] largos = { 0.3f, 0.4f, 0.44f, 0.4f, 0.32f };
        for (int i = 1; i < 5; i++)
        {
            float largo = 0.1f * a;
            if (patron[i] == '1')
            {
                largo = largos[i] * a;
            }
            Image dedo = Caja("Dedo_" + i, caja, Vector2.zero, new Vector2(0.12f * a, largo), spriteRedondo, color, 0.06f * a);
            dedo.rectTransform.pivot = new Vector2(0.5f, 0f);   // crece desde la base
            dedo.rectTransform.anchoredPosition = new Vector2(xs[i] * a, -0.06f * a);
            Borde(dedo, borde, 3f);
        }

        // el pulgar, inclinado hacia afuera
        float largoPulgar = 0.1f * a;
        if (patron[0] == '1')
        {
            largoPulgar = largos[0] * a;
        }
        Image pulgar = Caja("Pulgar", caja, Vector2.zero, new Vector2(0.13f * a, largoPulgar), spriteRedondo, color, 0.065f * a);
        pulgar.rectTransform.pivot = new Vector2(0.5f, 0f);
        pulgar.rectTransform.anchoredPosition = new Vector2(-0.24f * a, -0.32f * a);
        pulgar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 35f);
        Borde(pulgar, borde, 3f);

        Image palma = Caja("Palma", caja, new Vector2(0f, -0.27f * a), new Vector2(0.56f * a, 0.46f * a), spriteRedondo, color, 0.16f * a);
        Borde(palma, borde, 3f);
    }

    // un chulo verde (✓) hecho con un circulo y dos barritas
    GameObject Chulo(Transform padre, Vector2 pos, float tam)
    {
        Image circulo = Caja("Chulo", padre, pos, new Vector2(tam, tam), spriteCirculo, VERDE, 0f);
        Borde(circulo, Color.white, 3f);
        Image corta = Caja("Corta", circulo.transform, new Vector2(-tam * 0.12f, -tam * 0.04f), new Vector2(tam * 0.32f, tam * 0.12f), null, Color.white, 0f);
        corta.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        Image larga = Caja("Larga", circulo.transform, new Vector2(tam * 0.1f, tam * 0.06f), new Vector2(tam * 0.55f, tam * 0.12f), null, Color.white, 0f);
        larga.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        return circulo.gameObject;
    }

    // una fogata pequena: dos troncos cruzados y dos llamas
    void Fogata(Transform padre, Vector2 pos, float tam)
    {
        Image t1 = Caja("Tronco_1", padre, pos + new Vector2(0f, -tam * 0.42f), new Vector2(tam * 0.9f, tam * 0.16f), spriteRedondo, MADERA, tam * 0.08f);
        t1.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        Image t2 = Caja("Tronco_2", padre, pos + new Vector2(0f, -tam * 0.42f), new Vector2(tam * 0.9f, tam * 0.16f), spriteRedondo, MADERA, tam * 0.08f);
        t2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        Caja("Llama", padre, pos, new Vector2(tam * 0.7f, tam * 0.85f), spriteTriangulo, NARANJA, 0f);
        Caja("Nucleo", padre, pos + new Vector2(0f, -tam * 0.12f), new Vector2(tam * 0.36f, tam * 0.45f), spriteTriangulo, MOSTAZA, 0f);
    }

    // un boton en forma de pildora, como los del grupo: borde mas oscuro que el relleno,
    // un brillo blanco arriba y una sombrita abajo
    Button Boton(string nombre, Transform padre, Vector2 pos, Vector2 tam, string texto, int tamTexto, Color color)
    {
        Image fondo = Caja(nombre, padre, pos, tam, spriteRedondo, color, tam.y / 2f);
        Borde(fondo, Oscurecer(color, 0.6f), 4f);
        Sombra(fondo);
        Button b = fondo.gameObject.AddComponent<Button>();
        b.targetGraphic = fondo;

        // al pasar el mouse se aclara un poco, al presionar se oscurece
        ColorBlock colores = b.colors;
        colores.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colores.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        b.colors = colores;

        Image brillo = Caja("Brillo", fondo.transform, new Vector2(0f, tam.y * 0.22f), new Vector2(tam.x - tam.y * 0.5f, tam.y * 0.22f),
                            spriteRedondo, new Color(1f, 1f, 1f, 0.35f), tam.y * 0.11f);
        brillo.raycastTarget = false;

        Texto("Texto", fondo.transform, Vector2.zero, tam - new Vector2(tam.y * 0.4f, 0f), texto, tamTexto, TINTA, FontStyle.Bold);
        return b;
    }

    // el boton redondo verde "Inicio" con texto amarillo, como en el estilo del grupo
    Button BotonRedondo(string nombre, Transform padre, Vector2 pos, float tam)
    {
        Button b = Boton(nombre, padre, pos, new Vector2(tam, tam), "Inicio", Mathf.RoundToInt(tam * 0.26f), VERDE);
        Image fondo = b.GetComponent<Image>();
        fondo.sprite = spriteCirculo;
        fondo.type = Image.Type.Simple;
        Text t = b.GetComponentInChildren<Text>();
        t.color = MOSTAZA;
        t.rectTransform.sizeDelta = new Vector2(tam, tam);

        // en el circulo el brillo tapa la palabra: se quita
        Transform brillo = b.transform.Find("Brillo");
        if (brillo != null)
        {
            Borrar(brillo.gameObject);
        }
        return b;
    }

    static Color Oscurecer(Color c, float cuanto)
    {
        return new Color(c.r * cuanto, c.g * cuanto, c.b * cuanto, 1f);
    }

    // una barra que se llena (la de la palma y la de sostener la seña)
    Image BarraPalma(Transform padre, Vector2 pos, float ancho)
    {
        Caja("Barra", padre, pos, new Vector2(ancho, 16f), spriteRedondo, new Color(1f, 1f, 1f, 0.35f), 8f);
        Image relleno = Caja("Relleno", padre, pos, new Vector2(ancho, 16f), spriteRedondo, VERDE_CLARO, 8f);
        relleno.type = Image.Type.Filled;
        relleno.fillMethod = Image.FillMethod.Horizontal;
        relleno.fillAmount = 0f;
        return relleno;
    }


    // =====================================================================
    // AYUDANTES
    // =====================================================================

    // un objeto de interfaz vacio, centrado en su padre
    RectTransform Nuevo(string nombre, Transform padre, Vector2 pos, Vector2 tam)
    {
        GameObject g = new GameObject(nombre, typeof(RectTransform));
        g.transform.SetParent(padre, false);
        RectTransform rt = g.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        return rt;
    }

    // una pantalla: ocupa todo el lienzo
    RectTransform Pantalla(string nombre, Transform padre)
    {
        RectTransform rt = Nuevo(nombre, padre, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    // un rectangulo de color. Con el sprite redondo, "radio" dice que tan redondas son sus esquinas
    Image Caja(string nombre, Transform padre, Vector2 pos, Vector2 tam, Sprite forma, Color color, float radio)
    {
        RectTransform rt = Nuevo(nombre, padre, pos, tam);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = forma;
        img.color = color;

        if (forma != null && forma == spriteRedondo)
        {
            // el sprite redondo tiene esquinas de 50 px; el multiplicador las agranda o achica
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 50f / Mathf.Max(radio, 1f);
        }
        return img;
    }

    Text Texto(string nombre, Transform padre, Vector2 pos, Vector2 tam, string contenido, int tamano, Color color, FontStyle estilo)
    {
        RectTransform rt = Nuevo(nombre, padre, pos, tam);
        Text t = rt.gameObject.AddComponent<Text>();

        // las fuentes del grupo: Titan One en titulos y botones (lo grande y en negrita),
        // Nunito Sans en lo demas. Con las fuentes reales la negrita no se finge (FontStyle.Normal).
        // OJO: Titan One dibuja la "í" sin tilde ("silaba"): por eso solo va en textos de 44 o mas,
        // que no llevan í. Los textos que cambian en el juego ("¡Así! Sostén la seña") van en Nunito
        Font elegida = fuente;
        if (estilo == FontStyle.Bold && tamano >= 44 && fuenteTitulos != null)
        {
            elegida = fuenteTitulos;
            estilo = FontStyle.Normal;
        }
        else if (estilo == FontStyle.Bold && fuenteCuerpoNegrita != null)
        {
            elegida = fuenteCuerpoNegrita;
            estilo = FontStyle.Normal;
        }
        else if (estilo != FontStyle.Bold && fuenteCuerpo != null)
        {
            elegida = fuenteCuerpo;
        }
        t.font = elegida;
        t.text = contenido;
        t.fontSize = tamano;
        t.fontStyle = estilo;

        // Titan One es mas ancha que la fuente de antes: si un texto no cabe, se achica (hasta la mitad)
        t.resizeTextForBestFit = true;
        t.resizeTextMaxSize = tamano;
        t.resizeTextMinSize = Mathf.Max(10, tamano / 2);
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;   // con Overflow, el ajuste de tamano no mira el alto
        t.raycastTarget = false;   // el texto no tapa los clics del boton
        return t;
    }

    // la sombrita de abajo de las pildoras
    void Sombra(Graphic g)
    {
        Shadow s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.25f);
        s.effectDistance = new Vector2(0f, -6f);
    }

    // un borde de dibujo animado alrededor de una pieza
    void Borde(Graphic g, Color color, float grosor)
    {
        if (color.a <= 0f)
        {
            return;
        }
        Outline o = g.gameObject.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(grosor, -grosor);
    }

    string Dibujo(string gesto)
    {
        int i = System.Array.IndexOf(SombrasJuego.NOMBRES, gesto);
        if (i < 0)
        {
            return "00000";
        }
        return DIBUJOS[i];
    }

    string Bonito(string gesto)
    {
        int i = System.Array.IndexOf(SombrasJuego.NOMBRES, gesto);
        if (i < 0)
        {
            return gesto;
        }
        return BONITOS[i];
    }

    void Borrar(Object cosa)
    {
        if (Application.isPlaying)
        {
            Destroy(cosa);
        }
        else
        {
            DestroyImmediate(cosa);
        }
    }


    // =====================================================================
    // LAS FORMAS: se dibujan pixel por pixel y se guardan como PNG en Assets/UI/Generados
    // =====================================================================
    // las fuentes y la medalla del grupo. En el editor se cargan solas de Assets/UI; en el juego ya
    // vienen guardadas en la escena. Si faltan, se usa la fuente de Unity y el circulo dorado
    void PrepararEstilo()
    {
        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (fuenteTitulos == null)
            {
                fuenteTitulos = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fuentes/TitanOne-Regular.ttf");
            }
            if (fuenteCuerpo == null)
            {
                fuenteCuerpo = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fuentes/NunitoSans-Regular.ttf");
            }
            if (fuenteCuerpoNegrita == null)
            {
                fuenteCuerpoNegrita = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fuentes/NunitoSans-ExtraBold.ttf");
            }
            if (medalla == null)
            {
                string ruta = "Assets/UI/Grupo/Medalla_Nivel5.png";
                TextureImporter importador = AssetImporter.GetAtPath(ruta) as TextureImporter;
                if (importador != null && importador.textureType != TextureImporterType.Sprite)
                {
                    importador.textureType = TextureImporterType.Sprite;
                    importador.spriteImportMode = SpriteImportMode.Single;
                    importador.mipmapEnabled = false;
                    importador.alphaIsTransparency = true;
                    importador.SaveAndReimport();
                }
                medalla = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            }
        }
#endif
    }

    void CargarFormas()
    {
        spriteRedondo = ObtenerForma("Redondo", 128, 50, PixelRedondo);
        spriteCirculo = ObtenerForma("Circulo", 128, 0, PixelCirculo);
        spriteTriangulo = ObtenerForma("Triangulo", 128, 0, PixelTriangulo);
        spritePentagono = ObtenerForma("Pentagono", 128, 0, PixelPentagono);
    }

    // cada funcion dice que tan "lleno" esta un pixel (0 = vacio, 1 = lleno). p va de -64 a 64
    float PixelRedondo(Vector2 p)
    {
        // un cuadrado con esquinas redondas de radio 50
        float radio = 50f;
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(64f - radio, 64f - radio);
        float fuera = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radio;
        return Mathf.Clamp01(0.5f - fuera);   // el borde queda suavizado medio pixel
    }

    float PixelCirculo(Vector2 p)
    {
        return Mathf.Clamp01(0.5f - (p.magnitude - 63f));
    }

    float PixelTriangulo(Vector2 p)
    {
        Vector2[] puntas = { new Vector2(0f, 62f), new Vector2(62f, -56f), new Vector2(-62f, -56f) };
        return Cubre(puntas, p);
    }

    float PixelPentagono(Vector2 p)
    {
        Vector2[] puntas = new Vector2[5];
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.PI / 2f - i * Mathf.PI * 2f / 5f;   // empieza arriba y va en el sentido del reloj
            puntas[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 62f;
        }
        return Cubre(puntas, p);
    }

    // que fraccion del pixel cae dentro del poligono (se mira en 4 x 4 puntitos, para suavizar el borde)
    float Cubre(Vector2[] poligono, Vector2 p)
    {
        int dentro = 0;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                Vector2 q = p + new Vector2((i - 1.5f) / 4f, (j - 1.5f) / 4f);
                if (DentroDe(poligono, q))
                {
                    dentro = dentro + 1;
                }
            }
        }
        return dentro / 16f;
    }

    // un punto esta dentro de un poligono convexo (en el sentido del reloj) si queda a la derecha de todos sus lados
    bool DentroDe(Vector2[] poligono, Vector2 q)
    {
        for (int i = 0; i < poligono.Length; i++)
        {
            Vector2 a = poligono[i];
            Vector2 b = poligono[(i + 1) % poligono.Length];
            float cruz = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x);
            if (cruz > 0f)
            {
                return false;
            }
        }
        return true;
    }

    Texture2D DibujarForma(int tam, System.Func<Vector2, float> forma)
    {
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - tam / 2f, y + 0.5f - tam / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, forma(p)));
            }
        }
        tex.Apply();
        return tex;
    }

    Sprite ObtenerForma(string nombre, int tam, int borde, System.Func<Vector2, float> forma)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string ruta = "Assets/UI/Generados/" + nombre + ".png";
            Sprite existente = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            if (existente != null)
            {
                return existente;
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "UI/Generados"));
            File.WriteAllBytes(Path.Combine(Application.dataPath, "UI/Generados/" + nombre + ".png"), DibujarForma(tam, forma).EncodeToPNG());
            AssetDatabase.ImportAsset(ruta);

            // que Unity lo importe como sprite, con sus bordes para estirarlo sin deformar las esquinas
            TextureImporter importador = (TextureImporter)AssetImporter.GetAtPath(ruta);
            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.spriteBorder = new Vector4(borde, borde, borde, borde);
            importador.mipmapEnabled = false;
            importador.alphaIsTransparency = true;
            importador.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        }
#endif
        Texture2D tex = DibujarForma(tam, forma);
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(borde, borde, borde, borde));
    }
}
