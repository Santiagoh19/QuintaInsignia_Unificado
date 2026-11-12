using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "Prendamos la fogata"
/// 1) Calibración de la pinza  2) Agarrar ramitas y ponerlas en el círculo de piedras
/// 3) Tomar un fósforo y frotarlo en la caja  4) Acercar la llama y sostenerla quieta hasta que prenda.
/// Todas las posiciones están en coordenadas locales del Canvas (centro = 0,0).
/// </summary>
public class FogataGame : MonoBehaviour
{
    public enum Fase { Calibracion, Ramitas, Fosforo, Encender, Completado, Menu, SinTiempo }
    public enum ModoFosforo { Mover, Acercar, FrotarRapido }
    public enum Dificultad { Facil, Medio, Dificil }

    [Serializable]
    public class Ajustes
    {
        [Header("Calibración")]
        public bool calibrarAlInicio = true;
        public float segundosCalibracion = 2.5f;
        [Tooltip("Qué tanto hay que cerrar la pinza, como fracción del rango calibrado (abierta → cerrada).\n" +
                 "0.30 = cerrar un poco basta. 0.15 = hay que juntar bien los dedos")]
        [Range(0.05f, 0.6f)] public float exigenciaPinza = 0.30f;

        [Header("Ramitas")]
        public int ramitasNecesarias = 5;
        [Tooltip("Qué tan cerca debe estar el dedo de una ramita para agarrarla (px del Canvas)")]
        public float radioAgarre = 110f;
        [Tooltip("Si se cierra la pinza un poco antes de llegar a la ramita o al fósforo, igual se agarra durante estos segundos")]
        public float margenAgarre = 0.35f;
        [Tooltip("Tamaño del área donde se sueltan las ramitas (px del Canvas)")]
        public float radioFogata = 170f;
        [Tooltip("Si se suelta fuera del círculo, ¿la ramita vuelve a su lugar original?")]
        public bool ramitaVuelveAlInicio = false;
        public bool usarLinterna = true;
        [Tooltip("Tamaño del círculo de luz de la linterna (1 = normal). Más chico = hay que buscar más")]
        [Range(0.5f, 1f)] public float tamanoLinterna = 1f;

        [Header("Fósforo")]
        [Tooltip("Cómo se enciende el fósforo:\n" +
                 "Mover = cualquier movimiento sobre la lija suma, despacio también vale (recomendado).\n" +
                 "Acercar = basta con dejar el fósforo sobre la lija unos segundos (el más fácil).\n" +
                 "FrotarRapido = frotar de lado a lado con una velocidad mínima (el más difícil).")]
        public ModoFosforo modoFosforo = ModoFosforo.Mover;
        [Tooltip("Una vez agarrado, el fósforo se queda en la mano aunque se abran los dedos (no hay que sostener la pinza)")]
        public bool fosforoSePega = true;
        [Tooltip("Margen alrededor de la lija que también cuenta (px del Canvas). Más grande = más fácil atinarle")]
        public float margenLija = 90f;
        [Tooltip("Modo Mover: cuánto hay que mover el fósforo sobre la lija (anchos de pantalla). 0.6 ≈ 4 pasadas de lado a lado")]
        public float recorridoFrotar = 0.6f;
        [Tooltip("Modo Acercar: segundos que hay que dejar el fósforo sobre la lija")]
        public float segundosSobreLija = 1.5f;
        [Tooltip("Modo FrotarRapido: velocidad horizontal mínima para que el frote cuente (anchos de pantalla por segundo)")]
        public float velocidadFrotarMinima = 0.6f;
        [Tooltip("Modo FrotarRapido: segundos que hay que frotar con la velocidad mínima para que encienda")]
        public float segundosFrotando = 0.6f;
        [Tooltip("Si fosforoSePega está apagado: segundos que se puede abrir la pinza (o perder la mano) sin que se caiga el fósforo")]
        public float toleranciaSoltarFosforo = 0.4f;
        [Tooltip("Segundos que dura encendido el fósforo (0 = no se consume)")]
        public float duracionFosforo = 0f;

        [Header("Encender la fogata")]
        [Tooltip("Segundos que hay que sostener la llama quieta sobre las ramitas")]
        public float segundosSostenerLlama = 2.5f;
        [Tooltip("Movimiento máximo permitido para considerar la mano quieta (anchos de pantalla por segundo)")]
        public float quietudMaxima = 0.2f;

        [Header("Coordinación y tiempo")]
        [Tooltip("Cada ramita (y el fósforo) se agarra juntando el pulgar con el dedo que toca en la secuencia")]
        public bool alternarDedos = false;
        [Tooltip("Orden de los dedos: 1 = índice, 2 = medio, 3 = anular, 4 = meñique. Ej: 1, 3 = índice y anular alternados")]
        public int[] secuenciaDedos = { 1, 2, 3, 4 };
        [Tooltip("Los dedos de la secuencia salen al azar (nunca el mismo dos veces seguidas) en vez de en orden")]
        public bool dedosAleatorios = false;
        [Tooltip("Segundos para terminar (cuenta desde que acaba la calibración). 0 = sin cronómetro")]
        public float limiteSegundos = 0f;

        // Medio: igual que Fácil, pero cada agarre con el siguiente dedo en orden
        // (oposición del pulgar: índice, medio, anular, meñique). Sin cronómetro.
        public static Ajustes PresetMedio() => new Ajustes
        {
            alternarDedos = true,
            secuenciaDedos = new[] { 1, 2, 3, 4 },
        };

        // Difícil: la misma precisión que Fácil, pero los dedos salen al azar y es contra reloj.
        public static Ajustes PresetDificil() => new Ajustes
        {
            ramitasNecesarias = 6,
            alternarDedos = true,
            dedosAleatorios = true,
            secuenciaDedos = new[] { 1, 2, 3, 4 },
            limiteSegundos = 120f,
        };

        public Ajustes Copia() => JsonUtility.FromJson<Ajustes>(JsonUtility.ToJson(this));
    }

    [Serializable]
    public class Metricas
    {
        public string fecha;
        public string dificultad;
        public float segundosCalibracion, segundosRamitas, segundosFosforo, segundosEncender, segundosTotal;
        public int ramitasFueraDelCirculo, fosforosSoltados, fosforosConsumidos;
        public float umbralPinzaCerrar, umbralPinzaAbrir;
        public bool tiempoAgotado;
        public int dedoEquivocado; // veces que juntó el pulgar con otro dedo (nivel con secuencia de dedos)
    }

    [Header("Nivel Fácil (ajustable por el terapeuta)")]
    public Ajustes ajustes = new Ajustes();

    [Header("Menú y otros niveles")]
    [Tooltip("Mostrar el menú (Jugar / Salir y elegir la dificultad) antes de empezar. Apagado = empieza directo en Fácil")]
    public bool usarMenu = true;
    public Ajustes ajustesMedio = Ajustes.PresetMedio();
    public Ajustes ajustesDificil = Ajustes.PresetDificil();
    public Dificultad DificultadActual { get; private set; }
    /// <summary>JUEGO UNIFICADO: dificultad elegida en el menú de pausa general; la escena se recarga y empieza directo con ella.</summary>
    public static Dificultad? DificultadPendiente;
    [SerializeField, HideInInspector] int _versionNiveles;
    const int VERSION_NIVELES = 3; // 3 = Medio con dedos en orden, Difícil con dedos al azar + cronómetro

    [Header("Entrada")]
    public HandInput mano;
    public RectTransform area;
    [Tooltip("Abrir solo el Python de MediaPipe al empezar y cerrarlo al salir (si ya está abierto, usa ese)")]
    public bool abrirCamaraSola = true;
    [Tooltip("Número de la cámara para MediaPipe (0 = la primera). En el .exe también: -camara N")]
    public int numeroCamara = 0;
    [Tooltip("Carpeta mediaPipe\\Hand. Vacío = la busca sola junto al juego y en Escritorio\\MLtaller")]
    public string carpetaMediaPipe = "";

    [Header("Cursor")]
    public RectTransform cursor;
    public Image cursorImagen;
    public RectTransform pulgar;
    [Tooltip("Dibujo de la mano que sigue al cursor (cambia a pinza al cerrar los dedos)")]
    public Image manoImagen;
    public Sprite spriteManoAbierta, spriteManoPinza;
    public RectTransform luzLinterna;
    public Image luzLinternaImagen;

    [Header("Ramitas y fogata")]
    public List<Ramita> ramitas = new List<Ramita>();
    public RectTransform centroFogata;
    public RectTransform contenedorMonton;
    public RectTransform[] posicionesMonton;
    public Image marcaFogata;

    [Header("Fósforo")]
    public RectTransform caja;
    public RectTransform zonaFrotar;
    public RectTransform fosforo;
    public GameObject llamaFosforo;

    [Header("Fuego")]
    public RectTransform fuego;
    public Image resplandor;

    [Header("Interfaz")]
    public Image anilloProgreso;
    public RectTransform contenedorEfectos;
    public Sprite spriteChispa;
    public Text textoInstruccion;
    public Text textoAyuda;
    public Text textoContador;

    [Header("Sonidos (opcionales)")]
    [Tooltip("Los sonidos vacíos se cargan solos de Assets/Fogata/Resources/Sonidos")]
    public AudioSource audioSource;
    public AudioClip sonidoAgarrar, sonidoSoltar, sonidoColocar, sonidoRaspar, sonidoEncender, sonidoFuegoLoop;
    public AudioClip sonidoAmbiente, sonidoFogataPrende, sonidoLogro;
    [Range(0f, 1f)] public float volumenAmbiente = 0.9f;   // JUEGO UNIFICADO: ambiente al mismo nivel que en los otros niveles
    [Range(0f, 1f)] public float volumenFuego = 0.9f;
    [Range(0f, 1f)] public float volumenEfectos = 0.9f;

    [Header("Al terminar")]
    public float esperaAlTerminar = 4f;
    [Tooltip("Nombre de la escena del juego de sombras (debe estar en Build Settings). Vacío = no cambia de escena")]
    public string escenaSiguiente = "";
    [Tooltip("Si no hay escena siguiente, al terminar se vuelve al menú")]
    public bool volverAlMenuAlTerminar = true;
    public UnityEvent alCompletar;

    public Fase FaseActual { get; private set; }
    public Metricas metricas = new Metricas();

    // ===== estado interno =====
    Ramita _ramitaAgarrada;
    int _colocadas;
    float _tiempoFase, _tiempoTotal, _esperaCambio = -1f;
    int _calibPaso; float _calibT, _calibMax, _calibMin = 999f;
    bool _sosteniendoFosforo, _fosforoEncendido;
    float _cargaFrote, _vidaFosforo, _progresoFuego, _tRaspar, _acumChispas;
    Vector2 _fosforoReposoPos; float _fosforoReposoRot;
    float _escalaFuego;
    Image _brilloLija;
    Ajustes _ajustesFacil;
    readonly List<Image> _bordesLinterna = new List<Image>();
    AudioSource _loop, _ambiente;
    int _pasoDedo, _dedoAzar, _ultimoDedoTocando;
    float _tiempoRestante;
    bool _cronometroCorriendo, _cronometroUsado;
    RectTransform _cronometro, _etiquetaDedo;
    Image _cronometroFondo, _etiquetaDedoFondo;
    Text _cronometroTexto, _etiquetaDedoTexto;
    Sprite _pildora;

    static readonly string[] NombreDedo = { "pulgar", "índice", "medio", "anular", "meñique" };
    static readonly Color[] ColorDedo =
    {
        Color.white, new Color32(0xF4, 0xCC, 0x5C, 255), new Color32(0x74, 0xB0, 0x6E, 255),
        new Color32(0xB9, 0xA3, 0xD9, 255), new Color32(0xF4, 0xA2, 0x7A, 255),
    };

    /// <summary>Dedo que hay que juntar con el pulgar ahora (1 = índice ... 4 = meñique).</summary>
    int DedoActual
    {
        get
        {
            var s = ajustes.secuenciaDedos;
            if (!ajustes.alternarDedos || s == null || s.Length == 0) return 1;
            if (ajustes.dedosAleatorios)
            {
                if (_dedoAzar == 0) _dedoAzar = Mathf.Clamp(s[UnityEngine.Random.Range(0, s.Length)], 1, 4);
                return _dedoAzar;
            }
            return Mathf.Clamp(s[_pasoDedo % s.Length], 1, 4);
        }
    }

    /// <summary>Pasa al dedo siguiente: en orden, o al azar sin repetir el anterior.</summary>
    void SiguienteDedo()
    {
        _pasoDedo++;
        var s = ajustes.secuenciaDedos;
        if (!ajustes.dedosAleatorios || s == null || s.Length == 0) return;
        int anterior = DedoActual;
        for (int i = 0; i < 20; i++)
        {
            int d = Mathf.Clamp(s[UnityEngine.Random.Range(0, s.Length)], 1, 4);
            if (d != anterior) { _dedoAzar = d; return; }
        }
    }

    string TextoDedo() => $"Agarra juntando el pulgar con el {NombreDedo[DedoActual].ToUpper()}.";

    class Chispa { public RectTransform rt; public Image img; public Vector2 vel; public float vida, vidaMax; }
    readonly List<Chispa> _chispas = new List<Chispa>();

    int Necesarias => Mathf.Clamp(ajustes.ramitasNecesarias, 1, Mathf.Max(1, ramitas.Count));

    // =====================================================================
    void OnValidate() => ActualizarNivelesViejos();
    void Awake()
    {
        ActualizarNivelesViejos();
        if (abrirCamaraSola) LanzadorCamara.Asegurar(numeroCamara, carpetaMediaPipe);
    }

    void OnDestroy()
    {
        if (_pildora) { Destroy(_pildora.texture); Destroy(_pildora); }
    }

    /// <summary>
    /// Los niveles Medio y Difícil cambiaron de diseño. Si la escena guardó una versión vieja, se reemplazan
    /// una sola vez; después se respetan los cambios del terapeuta.
    /// </summary>
    void ActualizarNivelesViejos()
    {
        if (_versionNiveles >= VERSION_NIVELES) return;
        ajustesMedio = Ajustes.PresetMedio();
        ajustesDificil = Ajustes.PresetDificil();
        _versionNiveles = VERSION_NIVELES;
    }

    void Start()
    {
        if (!mano) mano = HandInput.Instance;
        if (!area && mano) area = mano.area;
        if (fosforo)
        {
            _fosforoReposoPos = fosforo.anchoredPosition;
            _fosforoReposoRot = fosforo.localEulerAngles.z;
        }
        if (llamaFosforo) llamaFosforo.SetActive(false);
        if (fuego) { fuego.localScale = Vector3.zero; fuego.gameObject.SetActive(false); }
        if (resplandor) SetAlfa(resplandor, 0f);
        if (anilloProgreso) anilloProgreso.gameObject.SetActive(false);
        CrearBrilloLija();
        PrepararSonidos();
        metricas.fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _ajustesFacil = ajustes.Copia();

        if (DificultadPendiente.HasValue)
        {
            var d = DificultadPendiente.Value;
            DificultadPendiente = null;
            Iniciar(d);
        }
        else if (usarMenu)
        {
            CambiarFase(Fase.Menu);
            MenuFogata.Mostrar(this);
        }
        else Iniciar(Dificultad.Facil);
    }

    /// <summary>Empieza el juego con el nivel elegido (lo llama el menú).</summary>
    public void Iniciar(Dificultad nivel)
    {
        DificultadActual = nivel;
        switch (nivel)
        {
            case Dificultad.Medio: ajustes = ajustesMedio.Copia(); break;
            case Dificultad.Dificil: ajustes = ajustesDificil.Copia(); break;
            default: ajustes = _ajustesFacil.Copia(); break;
        }
        metricas.dificultad = nivel.ToString();
        metricas.fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _tiempoTotal = 0f;
        AplicarTamanoLinterna();
        CambiarFase(Fase.Calibracion);
    }

    void Update()
    {
        if (Pausa.Activa) return;   // JUEGO UNIFICADO: menú de pausa general abierto
        if (!mano || !area) return;
        float dt = Time.deltaTime;
        _tiempoFase += dt;
        if (FaseActual != Fase.Completado && FaseActual != Fase.Menu) _tiempoTotal += dt;

        ActualizarCursor();

        // dedo de la pinza: en la calibración siempre el índice; con secuencia, el que toca
        // (no se cambia mientras se sostiene algo)
        if (FaseActual == Fase.Calibracion || !ajustes.alternarDedos) mano.dedoPinza = 1;
        else if (_ramitaAgarrada == null && !mano.Pinza) mano.dedoPinza = DedoActual;
        ActualizarCronometro(dt);

        if (_esperaCambio > 0f)
        {
            _esperaCambio -= dt;
            if (_esperaCambio <= 0f) CambiarFase(Fase.Fosforo);
        }
        else
        {
            switch (FaseActual)
            {
                case Fase.Calibracion: FaseCalibracion(dt); break;
                case Fase.Ramitas: FaseRamitas(); break;
                case Fase.Fosforo: FaseFosforo(dt); break;
                case Fase.Encender: FaseEncender(dt); break;
            }
        }

        ActualizarFosforoVisual(dt);
        ActualizarFuegoVisual(dt);
        ActualizarLinterna(dt);
        ActualizarMarca();
        ActualizarBrilloLija();
        ActualizarEtiquetaDedo();
        ActualizarChispas(dt);

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
    }

    // =====================================================================
    // FASES
    // =====================================================================
    void CambiarFase(Fase nueva)
    {
        // acumular el tiempo de la fase que termina
        switch (FaseActual)
        {
            case Fase.Calibracion: metricas.segundosCalibracion += _tiempoFase; break;
            case Fase.Ramitas: metricas.segundosRamitas += _tiempoFase; break;
            case Fase.Fosforo: metricas.segundosFosforo += _tiempoFase; break;
            case Fase.Encender: metricas.segundosEncender += _tiempoFase; break;
        }
        _tiempoFase = 0f;
        FaseActual = nueva;
        OcultarAnillo();
        if (textoContador) textoContador.gameObject.SetActive(nueva == Fase.Ramitas);

        switch (nueva)
        {
            case Fase.Menu:
                SetTextos("", "");
                break;
            case Fase.Calibracion:
                SetTextos("Vamos a conocer tu mano", "Muestra tu mano a la cámara (o haz clic para jugar con el mouse)");
                break;
            case Fase.Ramitas:
                SetTextos("Usa tu linterna para encontrar ramitas y llévalas al círculo de piedras",
                          ajustes.alternarDedos ? TextoDedo() + (ajustes.dedosAleatorios ? " ¡El dedo cambia al azar, atento!"
                                                                                         : " Cambia de dedo en cada ramita.")
                                                : "Junta el pulgar y el índice para agarrar. Abre los dedos para soltar.");
                ActualizarContador();
                if (ajustes.limiteSegundos > 0f && !_cronometroUsado)
                {
                    _tiempoRestante = ajustes.limiteSegundos;
                    _cronometroCorriendo = _cronometroUsado = true;
                }
                break;
            case Fase.Fosforo:
                SetTextos("¡La fogata está lista! Toma un fósforo de la caja",
                          ajustes.alternarDedos ? $"Agarra el fósforo con el pulgar y el {NombreDedo[DedoActual].ToUpper()}."
                                                : "Haz la pinza sobre el fósforo.");
                break;
            case Fase.SinTiempo:
                SetTextos("¡Se acabó el tiempo!", "Inténtalo otra vez, ¡tú puedes!");
                break;
            case Fase.Encender:
                SetTextos("¡Se encendió! Acerca la llama a las ramitas", "Mantén la mano quieta sobre la fogata.");
                break;
            case Fase.Completado:
                SetTextos("¡Lo lograste! La fogata está encendida",
                          "Ahora, con la luz del fuego... ¿qué figuras puedes hacer con tus manos?");
                break;
        }
    }

    // ---------- 1. Calibración ----------
    void FaseCalibracion(float dt)
    {
        if (!ajustes.calibrarAlInicio) { CambiarFase(Fase.Ramitas); return; }

        if (!mano.ManoDetectada)
        {
            if (mano.UsandoMouse && mano.PinzaEmpezo) { CambiarFase(Fase.Ramitas); return; }
            SetAyuda("Muestra tu mano a la cámara (o haz clic para jugar con el mouse)");
            return;
        }

        _calibT += dt;
        if (_calibPaso == 0)
        {
            _calibMax = Mathf.Max(_calibMax, mano.DistanciaPinza);
            SetTextos("Abre bien la mano", "Separa el pulgar del índice todo lo que puedas.");
        }
        else
        {
            _calibMin = Mathf.Min(_calibMin, mano.DistanciaPinza);
            SetTextos("Ahora junta el pulgar con el índice", "Haz una pinza y sostenla.");
        }
        MostrarAnillo(Vector2.zero, _calibT / Mathf.Max(0.1f, ajustes.segundosCalibracion));

        if (_calibT >= ajustes.segundosCalibracion)
        {
            _calibT = 0f;
            if (_calibPaso == 0) _calibPaso = 1;
            else
            {
                mano.AplicarCalibracion(_calibMax, _calibMin, ajustes.exigenciaPinza);
                Debug.Log($"[Fogata] Calibración: abierta={_calibMax:F3} cerrada={_calibMin:F3} -> cerrar<{mano.umbralCerrar:F3} abrir>{mano.umbralAbrir:F3}");
                CambiarFase(Fase.Ramitas);
            }
        }
    }

    // ---------- 2. Ramitas ----------
    void FaseRamitas()
    {
        Vector2 c = mano.Cursor;

        if (_ramitaAgarrada == null)
        {
            Ramita cercana = RamitaMasCercana(c);
            foreach (var r in ramitas)
                if (r && r.estado == Ramita.Estado.EnSuelo)
                    r.escalaObjetivo = (r == cercana) ? 1.15f : 1f;
            if (ajustes.alternarDedos) RevisarDedo();

            if (PinzaRecien() && cercana != null)
            {
                _ramitaAgarrada = cercana;
                cercana.estado = Ramita.Estado.Agarrada;
                cercana.escalaObjetivo = 1.1f;
                cercana.transform.SetAsLastSibling();
                Sonar(sonidoAgarrar);
                SetAyuda(ajustes.alternarDedos
                    ? $"¡Bien! Llévala al círculo sin separar el pulgar del {NombreDedo[mano.dedoPinza]}."
                    : "¡Bien! Llévala al círculo de piedras sin soltar la pinza.");
            }
        }
        else
        {
            _ramitaAgarrada.objetivo = c;
            if (!mano.Pinza) // soltó
            {
                bool dentro = Vector2.Distance(c, PosEnArea(centroFogata)) <= ajustes.radioFogata;
                if (dentro) ColocarRamita(_ramitaAgarrada);
                else SoltarRamita(_ramitaAgarrada);
                _ramitaAgarrada = null;
            }
        }

        if (_colocadas >= Necesarias && _ramitaAgarrada == null && _esperaCambio <= 0f)
        {
            SetTextos("¡Muy bien! Ya tienes suficientes ramitas", "");
            _esperaCambio = 1.2f;
        }
    }

    Ramita RamitaMasCercana(Vector2 p)
    {
        Ramita mejor = null;
        float mejorD = ajustes.radioAgarre;
        foreach (var r in ramitas)
        {
            if (!r || r.estado != Ramita.Estado.EnSuelo) continue;
            float d = Vector2.Distance(p, r.Rect.anchoredPosition);
            if (d <= mejorD) { mejorD = d; mejor = r; }
        }
        return mejor;
    }

    void ColocarRamita(Ramita r)
    {
        r.estado = Ramita.Estado.Colocada;
        if (contenedorMonton) r.transform.SetParent(contenedorMonton, true);

        if (posicionesMonton != null && posicionesMonton.Length > 0)
        {
            var slot = posicionesMonton[_colocadas % posicionesMonton.Length];
            r.objetivo = PosEnArea(slot);
            r.rotacionObjetivo = slot.localEulerAngles.z;
        }
        else
        {
            r.objetivo = PosEnArea(centroFogata) + new Vector2(UnityEngine.Random.Range(-40f, 40f), 60f);
            r.rotacionObjetivo = UnityEngine.Random.Range(-30f, 30f);
        }
        r.escalaObjetivo = 1f;
        _colocadas++;
        Sonar(sonidoColocar);
        ActualizarContador();
        if (ajustes.alternarDedos) SiguienteDedo(); // la siguiente ramita va con otro dedo
        string siguiente = ajustes.alternarDedos ? $"¡Perfecto! Ahora con el {NombreDedo[DedoActual].ToUpper()}."
                                                 : "¡Perfecto! Busca otra ramita.";
        SetAyuda(_colocadas < Necesarias ? siguiente : "");
    }

    /// <summary>Si junta el pulgar con otro dedo, le avisa cuál tiene que usar (y lo cuenta en las métricas).</summary>
    void RevisarDedo()
    {
        int t = mano.DedoTocandoPulgar;
        if (t != 0 && t != DedoActual)
        {
            if (t != _ultimoDedoTocando) metricas.dedoEquivocado++;
            SetAyuda($"Ese es el {NombreDedo[t]}. ¡Junta el pulgar con el {NombreDedo[DedoActual].ToUpper()}!");
        }
        else if (_ultimoDedoTocando != 0 && _ultimoDedoTocando != DedoActual && t == 0) SetAyuda(TextoDedo());
        _ultimoDedoTocando = t;
    }

    // ---------- Cronómetro ----------
    void ActualizarCronometro(float dt)
    {
        bool enJuego = FaseActual == Fase.Ramitas || FaseActual == Fase.Fosforo || FaseActual == Fase.Encender;
        if (_cronometroCorriendo && enJuego)
        {
            _tiempoRestante -= dt;
            if (_tiempoRestante <= 0f) { _tiempoRestante = 0f; TiempoAgotado(); }
        }

        bool ver = _cronometroUsado && (enJuego || FaseActual == Fase.SinTiempo || FaseActual == Fase.Completado);
        if (!ver) { if (_cronometro) _cronometro.gameObject.SetActive(false); return; }
        if (!_cronometro) CrearCronometro();
        _cronometro.gameObject.SetActive(true);
        _cronometroTexto.text = FormatoTiempo(_tiempoRestante);

        // últimos 15 segundos: se pone coral y late
        bool apurado = _cronometroCorriendo && _tiempoRestante <= 15f;
        _cronometroFondo.color = apurado ? new Color32(0xF4, 0xA2, 0x7A, 255) : new Color32(0xFC, 0xF4, 0xD4, 255);
        float s = apurado ? 1f + 0.06f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
        _cronometro.localScale = new Vector3(s, s, 1f);
    }

    void TiempoAgotado()
    {
        _cronometroCorriendo = false;
        metricas.tiempoAgotado = true;
        if (_ramitaAgarrada != null)
        {
            _ramitaAgarrada.estado = Ramita.Estado.EnSuelo;
            _ramitaAgarrada.escalaObjetivo = 1f;
            _ramitaAgarrada = null;
        }
        if (_sosteniendoFosforo) DevolverFosforo();
        if (llamaFosforo) llamaFosforo.SetActive(false);
        _esperaCambio = -1f;
        _progresoFuego = 0f;
        CambiarFase(Fase.SinTiempo);
        GuardarMetricas();
        StartCoroutine(VolverAlMenuLuego(esperaAlTerminar + 1f));
    }

    IEnumerator VolverAlMenuLuego(float espera)
    {
        yield return new WaitForSeconds(espera);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static string FormatoTiempo(float segundos)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(segundos));
        return $"{s / 60}:{s % 60:00}";
    }

    void CrearCronometro()
    {
        // arriba a la izquierda, debajo del contador de ramitas (como el marco_cronometro del Figma)
        _cronometro = PildoraConContorno("Cronometro", new Vector2(260f, 110f), out _cronometroFondo);
        _cronometro.anchorMin = _cronometro.anchorMax = new Vector2(0f, 1f);
        _cronometro.anchoredPosition = new Vector2(200f, -350f);
        var fuente = textoContador ? textoContador.font : (textoInstruccion ? textoInstruccion.font : null);
        TextoHUD(_cronometro, "TIEMPO", fuente, 22, new Vector2(0f, 30f), new Vector2(240f, 30f));
        _cronometroTexto = TextoHUD(_cronometro, "", fuente, 52, new Vector2(0f, -12f), new Vector2(240f, 64f));
    }

    // ---------- Etiqueta del dedo junto a la mano ----------
    void ActualizarEtiquetaDedo()
    {
        bool ver = ajustes.alternarDedos && mano.ManoDetectada && _esperaCambio <= 0f &&
                   (FaseActual == Fase.Ramitas || (FaseActual == Fase.Fosforo && !_sosteniendoFosforo));
        if (!ver) { if (_etiquetaDedo) _etiquetaDedo.gameObject.SetActive(false); return; }
        if (!_etiquetaDedo)
        {
            _etiquetaDedo = PildoraConContorno("EtiquetaDedo", new Vector2(210f, 60f), out _etiquetaDedoFondo);
            var fuente = textoContador ? textoContador.font : (textoInstruccion ? textoInstruccion.font : null);
            _etiquetaDedoTexto = TextoHUD(_etiquetaDedo, "", fuente, 34, Vector2.zero, new Vector2(200f, 56f));
        }
        _etiquetaDedo.gameObject.SetActive(true);
        _etiquetaDedo.SetAsLastSibling();
        int d = Mathf.Clamp(mano.dedoPinza, 1, 4);
        _etiquetaDedoTexto.text = NombreDedo[d].ToUpper();
        _etiquetaDedoFondo.color = ColorDedo[d];
        _etiquetaDedo.anchoredPosition = mano.Cursor + new Vector2(0f, 95f); // justo encima de la pinza
    }

    /// <summary>Píldora con contorno oscuro (mismo estilo que los botones del menú).</summary>
    RectTransform PildoraConContorno(string nombre, Vector2 tam, out Image relleno)
    {
        if (!_pildora) _pildora = MenuFogata.CrearPildora();
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tam;
        Image Capa(string n, Color c, Vector2 t)
        {
            var g = new GameObject(n, typeof(RectTransform), typeof(Image));
            var r = g.GetComponent<RectTransform>();
            r.SetParent(rt, false);
            r.sizeDelta = t;
            var img = g.GetComponent<Image>();
            img.sprite = _pildora;
            img.type = Image.Type.Sliced;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }
        Capa("Contorno", new Color32(0x3D, 0x33, 0x2B, 255), tam + new Vector2(10f, 10f));
        relleno = Capa("Fondo", Color.white, tam);
        return rt;
    }

    static Text TextoHUD(RectTransform padre, string texto, Font fuente, int tam, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.GetComponent<Text>();
        t.font = fuente ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = texto;
        t.fontSize = tam;
        t.color = new Color32(0x2B, 0x24, 0x20, 255);
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    void SoltarRamita(Ramita r)
    {
        r.estado = Ramita.Estado.EnSuelo;
        r.escalaObjetivo = 1f;
        metricas.ramitasFueraDelCirculo++;
        if (ajustes.ramitaVuelveAlInicio) r.VolverAlInicio();
        else r.objetivo = r.Rect.anchoredPosition;
        Sonar(sonidoSoltar);
        SetAyuda("Se cayó fuera del círculo. ¡Inténtalo de nuevo!");
    }

    void ActualizarContador()
    {
        if (textoContador) textoContador.text = $"Ramitas: {Mathf.Min(_colocadas, Necesarias)} / {Necesarias}";
    }

    // ---------- 3. Fósforo ----------
    void FaseFosforo(float dt)
    {
        Vector2 c = mano.Cursor;

        if (!_sosteniendoFosforo)
        {
            bool cerca = fosforo && Vector2.Distance(c, PosEnArea(fosforo)) <= ajustes.radioAgarre * 1.5f;
            if (fosforo) fosforo.localScale = Vector3.one * (cerca ? 1.12f : 1f);
            if (ajustes.alternarDedos) RevisarDedo();
            if (PinzaRecien() && cerca)
            {
                if (ajustes.alternarDedos) SiguienteDedo();
                _sosteniendoFosforo = true;
                _cargaFrote = 0f;
                if (fosforo) fosforo.localScale = Vector3.one;
                Sonar(sonidoAgarrar);
                switch (ajustes.modoFosforo)
                {
                    case ModoFosforo.Mover:
                        SetTextos("Frota el fósforo sobre la lija de la caja", "Muévelo sobre la parte oscura. Despacio también vale.");
                        break;
                    case ModoFosforo.Acercar:
                        SetTextos("Pon el fósforo sobre la lija de la caja", "Déjalo ahí un momento.");
                        break;
                    default:
                        SetTextos("Frota el fósforo rápido sobre la lija de la caja",
                                  ajustes.fosforoSePega ? "Muévelo rápido de lado a lado." : "Muévelo de lado a lado sin soltar la pinza.");
                        break;
                }
            }
            return;
        }

        if (FosforoSeCayo())
        {
            metricas.fosforosSoltados++;
            DevolverFosforo();
            SetTextos("¡Uy! Se cayó el fósforo", "Toma otro de la caja con la pinza.");
            return;
        }

        bool enZona = SobreLija();
        float meta;
        switch (ajustes.modoFosforo)
        {
            case ModoFosforo.Mover:
                // suma la distancia recorrida sobre la lija, en cualquier dirección y a cualquier velocidad.
                // El avance no se pierde al salir de la lija.
                meta = Mathf.Max(0.05f, ajustes.recorridoFrotar);
                if (enZona)
                {
                    float v = mano.VelocidadNormalizada;
                    if (v > 0.03f) // se ignora el temblor muy pequeño para que no se encienda solo
                    {
                        _cargaFrote += v * dt;
                        EmitirChispas(c, 10f + 35f * Mathf.Clamp01(v / 0.5f), dt, 0.8f);
                        SonarRaspar(dt);
                        SetAyuda("¡Eso! Sigue moviéndolo sobre la lija...");
                    }
                    else SetAyuda("Mueve el fósforo sobre la lija, de lado a lado.");
                }
                else SetAyuda("Lleva el fósforo a la lija oscura de la caja.");
                break;

            case ModoFosforo.Acercar:
                meta = Mathf.Max(0.1f, ajustes.segundosSobreLija);
                if (enZona)
                {
                    _cargaFrote += dt;
                    EmitirChispas(c, 15f, dt, 0.6f);
                    SonarRaspar(dt);
                    SetAyuda("¡Muy bien! Déjalo ahí un momento...");
                }
                else SetAyuda("Lleva el fósforo a la lija oscura de la caja.");
                break;

            default: // FrotarRapido: el modo original
                meta = Mathf.Max(0.05f, ajustes.segundosFrotando);
                float vx = mano.VelocidadXNormalizada;
                if (enZona)
                {
                    if (vx >= ajustes.velocidadFrotarMinima)
                    {
                        _cargaFrote += dt;
                        EmitirChispas(c, 45f, dt, 1f);
                        SonarRaspar(dt);
                        SetAyuda("¡Eso! Sigue frotando...");
                    }
                    else
                    {
                        _cargaFrote = Mathf.Max(0f, _cargaFrote - dt * 0.3f);
                        if (vx > ajustes.velocidadFrotarMinima * 0.35f) EmitirChispas(c, 8f, dt, 0.5f);
                        SetAyuda("¡Más rápido!");
                    }
                }
                else
                {
                    _cargaFrote = Mathf.Max(0f, _cargaFrote - dt * 0.5f);
                    SetAyuda("Lleva el fósforo a la lija oscura de la caja.");
                }
                break;
        }

        MostrarAnillo(zonaFrotar ? CentroEnArea(zonaFrotar) + new Vector2(0f, 110f) : c, _cargaFrote / meta);

        if (_cargaFrote >= meta)
        {
            _fosforoEncendido = true;
            _vidaFosforo = ajustes.duracionFosforo;
            _progresoFuego = 0f;
            if (llamaFosforo) llamaFosforo.SetActive(true);
            EmitirChispas(c, 0f, 0f, 1f, 18);
            Sonar(sonidoEncender);
            CambiarFase(Fase.Encender);
        }
    }

    // ---------- 4. Encender ----------
    void FaseEncender(float dt)
    {
        if (FosforoSeCayo())
        {
            metricas.fosforosSoltados++;
            DevolverFosforo();
            CambiarFase(Fase.Fosforo);
            SetTextos("El fósforo se apagó al soltarlo", "Toma otro de la caja con la pinza.");
            return;
        }

        if (ajustes.duracionFosforo > 0f)
        {
            _vidaFosforo -= dt;
            if (_vidaFosforo <= 0f)
            {
                metricas.fosforosConsumidos++;
                DevolverFosforo();
                CambiarFase(Fase.Fosforo);
                SetTextos("El fósforo se consumió", "Toma otro de la caja con la pinza.");
                return;
            }
        }

        Vector2 c = mano.Cursor;
        Vector2 centro = PosEnArea(centroFogata);
        bool cerca = Vector2.Distance(c, centro) <= ajustes.radioFogata * 0.75f;
        bool quieto = mano.VelocidadNormalizada <= ajustes.quietudMaxima;
        float seg = Mathf.Max(0.1f, ajustes.segundosSostenerLlama);

        if (cerca && quieto)
        {
            _progresoFuego += dt / seg;
            EmitirChispas(centro + new Vector2(0f, 20f), 6f + 30f * _progresoFuego, dt, 0.6f);
            SetAyuda("¡Muy bien! Mantén la mano quieta...");
        }
        else
        {
            _progresoFuego = Mathf.Max(0f, _progresoFuego - dt * 0.35f / seg);
            SetAyuda(cerca ? "Quieto... intenta no mover la mano" : "Acerca la llama a las ramitas.");
        }

        MostrarAnillo(centro + new Vector2(0f, 60f), _progresoFuego);

        if (_progresoFuego >= 1f) Completar();
    }

    void Completar()
    {
        CambiarFase(Fase.Completado);
        if (_cronometroUsado)
        {
            _cronometroCorriendo = false;
            SetAyuda($"¡Lo lograste en {FormatoTiempo(ajustes.limiteSegundos - _tiempoRestante)}!");
        }
        _sosteniendoFosforo = false;
        _fosforoEncendido = false;
        if (fosforo) fosforo.gameObject.SetActive(false);
        Sonar(sonidoFogataPrende ? sonidoFogataPrende : sonidoEncender);
        StartCoroutine(SonarLuego(sonidoLogro, 0.6f));
        GuardarMetricas();
        StartCoroutine(TerminarLuego());
    }

    IEnumerator TerminarLuego()
    {
        yield return new WaitForSeconds(esperaAlTerminar);
        // JUEGO UNIFICADO: ventana de insignia (como en los otros niveles) y luego al siguiente nivel
        bool cerrada = false;
        NivelesUnificados.GanarInsigniaNivel4(() => cerrada = true);
        while (!cerrada) yield return null;
        alCompletar?.Invoke();
        if (!string.IsNullOrEmpty(escenaSiguiente)) Transicion.Cargar(escenaSiguiente);   // JUEGO UNIFICADO: con pantalla de carga
        else if (usarMenu && volverAlMenuAlTerminar) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>La pinza se cerró hace poco (permite cerrar un poco antes de llegar al objeto).</summary>
    bool PinzaRecien() => mano.Pinza && mano.SegundosEnPinza <= Mathf.Max(ajustes.margenAgarre, Time.deltaTime);

    bool FosforoSeCayo() => !ajustes.fosforoSePega && !mano.Pinza && mano.SegundosSinPinza >= ajustes.toleranciaSoltarFosforo;

    bool SobreLija() => DentroDe(zonaFrotar, mano.Cursor, ajustes.margenLija);

    void SonarRaspar(float dt)
    {
        _tRaspar -= dt;
        if (_tRaspar <= 0f) { Sonar(sonidoRaspar); _tRaspar = 0.25f; }
    }

    void DevolverFosforo()
    {
        _sosteniendoFosforo = false;
        _fosforoEncendido = false;
        _cargaFrote = 0f;
        _progresoFuego = 0f;
        if (llamaFosforo) llamaFosforo.SetActive(false);
        Sonar(sonidoSoltar);
    }

    // =====================================================================
    // VISUALES
    // =====================================================================
    void ActualizarCursor()
    {
        bool visible = mano.ManoDetectada || mano.UsandoMouse;
        if (cursor)
        {
            cursor.anchoredPosition = mano.Cursor;
            float s = mano.Pinza ? 0.75f : 1f;
            cursor.localScale = Vector3.Lerp(cursor.localScale, new Vector3(s, s, 1f), 1f - Mathf.Exp(-20f * Time.deltaTime));
        }
        if (cursorImagen)
        {
            Color col = mano.Pinza ? new Color(1f, 0.85f, 0.2f) : Color.white;
            col.a = visible ? 0.95f : 0.3f;
            cursorImagen.color = col;
        }
        if (pulgar)
        {
            pulgar.gameObject.SetActive(mano.ManoDetectada);
            pulgar.anchoredPosition = mano.Pulgar;
        }
        if (manoImagen)
        {
            var rt = manoImagen.rectTransform;
            rt.anchoredPosition = mano.Cursor;
            // el dibujo es una mano derecha; se voltea para la izquierda
            float lado = mano.EsIzquierda && mano.ManoDetectada ? -1f : 1f;
            float s = mano.Pinza ? 0.95f : 1f;
            rt.localScale = Vector3.Lerp(rt.localScale, new Vector3(lado * s, s, 1f), 1f - Mathf.Exp(-20f * Time.deltaTime));
            if (mano.Pinza ? spriteManoPinza : spriteManoAbierta)
                manoImagen.sprite = mano.Pinza ? spriteManoPinza : spriteManoAbierta;
            Color col = mano.Pinza ? new Color(1f, 0.92f, 0.55f) : Color.white;
            col.a = visible ? 1f : 0.35f;
            manoImagen.color = col;
        }
    }

    void ActualizarFosforoVisual(float dt)
    {
        if (!fosforo || !fosforo.gameObject.activeSelf) return;
        float k = 1f - Mathf.Exp(-14f * dt);
        if (_sosteniendoFosforo)
        {
            fosforo.anchoredPosition = mano.Cursor;
            float z = Mathf.LerpAngle(fosforo.localEulerAngles.z, -20f, k);
            fosforo.localEulerAngles = new Vector3(0f, 0f, z);
        }
        else
        {
            fosforo.anchoredPosition = Vector2.Lerp(fosforo.anchoredPosition, _fosforoReposoPos, k);
            float z = Mathf.LerpAngle(fosforo.localEulerAngles.z, _fosforoReposoRot, k);
            fosforo.localEulerAngles = new Vector3(0f, 0f, z);
        }
    }

    void ActualizarFuegoVisual(float dt)
    {
        float objetivo = 0f, alfaResplandor = 0f;
        if (FaseActual == Fase.Encender && _progresoFuego > 0f)
        {
            objetivo = Mathf.Lerp(0.15f, 0.55f, _progresoFuego);
            alfaResplandor = 0.25f * _progresoFuego;
        }
        else if (FaseActual == Fase.Completado)
        {
            objetivo = 1f;
            alfaResplandor = 0.55f;
        }

        _escalaFuego = Mathf.Lerp(_escalaFuego, objetivo, 1f - Mathf.Exp(-4f * dt));
        if (fuego)
        {
            bool activo = _escalaFuego > 0.02f;
            if (fuego.gameObject.activeSelf != activo) fuego.gameObject.SetActive(activo);
            fuego.localScale = new Vector3(_escalaFuego, _escalaFuego, 1f);
        }
        if (resplandor)
        {
            float a = Mathf.Lerp(resplandor.color.a, alfaResplandor, 1f - Mathf.Exp(-3f * dt));
            SetAlfa(resplandor, a);
        }
        // el crepitar crece con las llamas
        if (_loop) _loop.volume = volumenFuego * Mathf.Clamp01(_escalaFuego);
    }

    void ActualizarLinterna(float dt)
    {
        if (!luzLinterna) return;
        luzLinterna.anchoredPosition = mano.Cursor;
        bool encendida = ajustes.usarLinterna &&
                         (FaseActual == Fase.Ramitas || FaseActual == Fase.Fosforo || FaseActual == Fase.Encender);
        if (luzLinternaImagen)
        {
            float a = Mathf.MoveTowards(luzLinternaImagen.color.a, encendida ? 1f : 0f, dt * (encendida ? 1.5f : 0.6f));
            SetAlfa(luzLinternaImagen, a);
            foreach (var b in _bordesLinterna) SetAlfa(b, a * 0.94f); // 0.94 = opacidad del borde de la textura
            luzLinterna.gameObject.SetActive(a > 0.001f);
        }
    }

    void ActualizarMarca()
    {
        if (!marcaFogata) return;
        float a = 0f;
        if (FaseActual == Fase.Ramitas)
        {
            if (_ramitaAgarrada != null)
            {
                bool dentro = Vector2.Distance(mano.Cursor, PosEnArea(centroFogata)) <= ajustes.radioFogata;
                a = dentro ? 0.9f : 0.45f + 0.2f * Mathf.Sin(Time.time * 5f);
            }
            else a = 0.25f;
        }
        else if (FaseActual == Fase.Encender) a = 0.35f + 0.15f * Mathf.Sin(Time.time * 4f);
        SetAlfa(marcaFogata, Mathf.Lerp(marcaFogata.color.a, a, 0.2f));
    }

    /// <summary>
    /// Achica el círculo de luz. La textura deja de tapar toda la pantalla, así que se agregan cuatro
    /// bordes del mismo color oscuro alrededor.
    /// </summary>
    void AplicarTamanoLinterna()
    {
        if (!luzLinterna) return;
        float s = Mathf.Clamp(ajustes.tamanoLinterna, 0.3f, 1.5f);
        luzLinterna.localScale = new Vector3(s, s, 1f);
        if (s >= 0.999f || _bordesLinterna.Count > 0) return;

        const float B = 6000f;
        Vector2 tam = luzLinterna.rect.size;
        var lados = new (Vector2 ancla, Vector2 pivote, Vector2 tam)[]
        {
            (new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(tam.x + 2f * B, B)),
            (new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(tam.x + 2f * B, B)),
            (new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(B, tam.y)),
            (new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(B, tam.y)),
        };
        foreach (var (ancla, pivote, t) in lados)
        {
            var go = new GameObject("BordeLinterna", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(luzLinterna, false);
            rt.anchorMin = rt.anchorMax = ancla;
            rt.pivot = pivote;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = t;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(4f / 255f, 6f / 255f, 14f / 255f, 0f); // color del borde de luz_linterna.png
            _bordesLinterna.Add(img);
        }
    }

    /// <summary>Brillo sobre la lija que indica dónde llevar el fósforo (se crea por código, no hace falta rehacer la escena).</summary>
    void CrearBrilloLija()
    {
        if (!zonaFrotar || !spriteChispa) return;
        var go = new GameObject("BrilloLija", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(zonaFrotar, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = zonaFrotar.sizeDelta + new Vector2(60f, 50f);
        _brilloLija = go.GetComponent<Image>();
        _brilloLija.sprite = spriteChispa;
        _brilloLija.raycastTarget = false;
        _brilloLija.color = new Color(1f, 0.85f, 0.3f, 0f);
    }

    void ActualizarBrilloLija()
    {
        if (!_brilloLija) return;
        float a = 0f;
        if (FaseActual == Fase.Fosforo && _sosteniendoFosforo)
            a = SobreLija() ? 0.5f : 0.25f + 0.15f * Mathf.Sin(Time.time * 5f);
        SetAlfa(_brilloLija, Mathf.Lerp(_brilloLija.color.a, a, 0.2f));
    }

    void MostrarAnillo(Vector2 pos, float valor)
    {
        if (!anilloProgreso) return;
        if (!anilloProgreso.gameObject.activeSelf) anilloProgreso.gameObject.SetActive(true);
        anilloProgreso.rectTransform.anchoredPosition = pos;
        anilloProgreso.fillAmount = Mathf.Clamp01(valor);
    }

    void OcultarAnillo()
    {
        if (anilloProgreso) anilloProgreso.gameObject.SetActive(false);
    }

    // ---------- Chispas ----------
    void EmitirChispas(Vector2 pos, float porSegundo, float dt, float fuerza, int extra = 0)
    {
        if (!contenedorEfectos) return;
        _acumChispas += porSegundo * dt;
        int n = extra + Mathf.FloorToInt(_acumChispas);
        _acumChispas -= Mathf.Floor(_acumChispas);
        for (int i = 0; i < n; i++)
        {
            var ch = ObtenerChispa();
            if (ch == null) return;
            ch.rt.anchoredPosition = pos;
            float ang = UnityEngine.Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float vel = UnityEngine.Random.Range(150f, 450f) * fuerza;
            ch.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * vel;
            ch.vidaMax = ch.vida = UnityEngine.Random.Range(0.3f, 0.7f);
            float tam = UnityEngine.Random.Range(8f, 18f);
            ch.rt.sizeDelta = new Vector2(tam, tam);
            ch.img.color = Color.Lerp(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.45f, 0.1f), UnityEngine.Random.value);
            ch.rt.gameObject.SetActive(true);
        }
    }

    Chispa ObtenerChispa()
    {
        foreach (var ch in _chispas) if (!ch.rt.gameObject.activeSelf) return ch;
        if (_chispas.Count >= 80) return null;
        var go = new GameObject("Chispa", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(contenedorEfectos, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.sprite = spriteChispa;
        img.raycastTarget = false;
        var nueva = new Chispa { rt = rt, img = img };
        _chispas.Add(nueva);
        return nueva;
    }

    void ActualizarChispas(float dt)
    {
        foreach (var ch in _chispas)
        {
            if (!ch.rt.gameObject.activeSelf) continue;
            ch.vida -= dt;
            if (ch.vida <= 0f) { ch.rt.gameObject.SetActive(false); continue; }
            ch.vel.y -= 900f * dt;
            ch.rt.anchoredPosition += ch.vel * dt;
            var c = ch.img.color; c.a = ch.vida / ch.vidaMax; ch.img.color = c;
        }
    }

    // =====================================================================
    // UTILIDADES
    // =====================================================================
    Vector2 PosEnArea(RectTransform rt)
    {
        if (!rt) return Vector2.zero;
        return (Vector2)area.InverseTransformPoint(rt.position);
    }

    Vector2 CentroEnArea(RectTransform rt)
    {
        var esquinas = new Vector3[4];
        rt.GetWorldCorners(esquinas);
        return ((Vector2)area.InverseTransformPoint(esquinas[0]) + (Vector2)area.InverseTransformPoint(esquinas[2])) * 0.5f;
    }

    bool DentroDe(RectTransform rt, Vector2 p, float margen)
    {
        if (!rt) return false;
        var esquinas = new Vector3[4];
        rt.GetWorldCorners(esquinas);
        Vector2 a = area.InverseTransformPoint(esquinas[0]);
        Vector2 b = area.InverseTransformPoint(esquinas[2]);
        Vector2 min = Vector2.Min(a, b) - Vector2.one * margen;
        Vector2 max = Vector2.Max(a, b) + Vector2.one * margen;
        return p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y;
    }

    void SetTextos(string instruccion, string ayuda)
    {
        if (textoInstruccion) textoInstruccion.text = instruccion;
        SetAyuda(ayuda);
    }

    void SetAyuda(string ayuda)
    {
        if (textoAyuda) textoAyuda.text = ayuda;
    }

    static void SetAlfa(Graphic g, float a)
    {
        var c = g.color; c.a = a; g.color = c;
    }

    void Sonar(AudioClip clip)
    {
        if (clip && audioSource) audioSource.PlayOneShot(clip, volumenEfectos);
    }

    IEnumerator SonarLuego(AudioClip clip, float espera)
    {
        yield return new WaitForSeconds(espera);
        Sonar(clip);
    }

    /// <summary>Carga los sonidos que falten desde Resources/Sonidos y arranca el ambiente y el fuego en bucle.</summary>
    void PrepararSonidos()
    {
        AudioClip Cargar(AudioClip actual, string nombre) => actual ? actual : Resources.Load<AudioClip>("Sonidos/" + nombre);
        sonidoAgarrar = Cargar(sonidoAgarrar, "agarrar");
        sonidoSoltar = Cargar(sonidoSoltar, "soltar");
        sonidoColocar = Cargar(sonidoColocar, "colocar");
        sonidoRaspar = Cargar(sonidoRaspar, "raspar");
        sonidoEncender = Cargar(sonidoEncender, "encender");
        sonidoFuegoLoop = Cargar(sonidoFuegoLoop, "fuego_loop");
        sonidoAmbiente = Cargar(sonidoAmbiente, "ambiente_noche");
        sonidoFogataPrende = Cargar(sonidoFogataPrende, "fogata_prende");
        sonidoLogro = Cargar(sonidoLogro, "logro");

        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        _ambiente = Bucle(sonidoAmbiente, volumenAmbiente);
        _loop = Bucle(sonidoFuegoLoop, 0f);
    }

    AudioSource Bucle(AudioClip clip, float volumen)
    {
        if (!clip) return null;
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.volume = volumen;
        s.playOnAwake = false;
        s.Play();
        return s;
    }

    /// <summary>Para que el menú use los mismos parlantes y volumen.</summary>
    public void SonarEfecto(AudioClip clip) => Sonar(clip);

    void GuardarMetricas()
    {
        metricas.segundosTotal = _tiempoTotal;
        metricas.umbralPinzaCerrar = mano.umbralCerrar;
        metricas.umbralPinzaAbrir = mano.umbralAbrir;
        string json = JsonUtility.ToJson(metricas);
        Debug.Log("[Fogata] Sesión terminada: " + json);
        try
        {
            string ruta = Path.Combine(Application.persistentDataPath, "fogata_sesiones.jsonl");
            File.AppendAllText(ruta, json + "\n");
            Debug.Log("[Fogata] Métricas guardadas en: " + ruta);
        }
        catch (Exception e) { Debug.LogWarning("[Fogata] No se pudieron guardar las métricas: " + e.Message); }
    }
}
