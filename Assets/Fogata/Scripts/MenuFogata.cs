using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menú inicial (Jugar / Salir) y elección de dificultad (Fácil / Medio / Difícil).
/// Se controla con la mano: cursor sobre un botón y hacer la pinza, o dejar la mano quieta encima
/// hasta que el botón se llene. Con el mouse: clic.
/// Lo crea FogataGame al empezar (no hace falta tocar la escena). Colores del Figma de la clase.
/// </summary>
public class MenuFogata : MonoBehaviour
{
    [Tooltip("Título que aparece arriba del menú")]
    public string titulo = "Pinzas de fuego";
    [Tooltip("Segundos que hay que dejar la mano sobre un botón para elegirlo sin hacer la pinza")]
    public float segundosParaElegir = 1.5f;
    [Tooltip("Hacer la pinza sobre un botón lo elige al instante")]
    public bool elegirConPinza = true;
    [Tooltip("Fuente del menú y de los textos del juego. Vacío = se usa 'Arial Rounded MT Bold' de Windows (o la normal si no está)")]
    public Font fuente;
    [Tooltip("Aplicar la fuente del menú también a las instrucciones del juego")]
    public bool fuenteEnTodoElJuego = true;
    [Tooltip("Vacíos = se cargan de Resources/Sonidos (ui_sobre, ui_elegir)")]
    public AudioClip sonidoSobre, sonidoElegir;

    class Boton
    {
        public RectTransform rt;
        public RectTransform relleno;
        public Action accion;
    }

    // colores del "Estilo gráfico" del Figma
    static readonly Color Crema = Hex("FFFCF6"), Amarillo = Hex("F4CC5C"), Rojo = Hex("C9534F"), Lila = Hex("B9A3D9"),
                          Verde = Hex("74B06E"), Durazno = Hex("F4A27A"), Contorno = Hex("3D332B"),
                          TextoOscuro = Hex("001905"), Pizarra = Hex("365F73"), Coral = Hex("4B91B5");   // JUEGO UNIFICADO: colores de los otros niveles
    static readonly Color BordeVerde = Hex("4F8A5B"), SombraGris = Hex("8F8F8F");

    FogataGame _juego;
    HandInput _mano;
    RectTransform _raiz, _panelInicio, _panelNivel;
    Text _textoAyuda;
    readonly List<Boton> _botones = new List<Boton>();
    Boton _sobre;
    float _tSobre, _espera;
    Sprite _pildora, _arco;
    Font _fuente;
    Font _fuenteTitulos;   // Titan One (títulos y botones, como en los otros niveles)
    Sprite _brilloArriba, _brilloAbajo;
    bool _fuenteGruesa; // la fuente ya es gruesa: no hace falta Bold

    public static void Mostrar(FogataGame juego)
    {
        var m = juego.GetComponent<MenuFogata>();
        if (!m) m = juego.gameObject.AddComponent<MenuFogata>();
        m._juego = juego;
        m.Construir();
    }

    // =====================================================================
    void Construir()
    {
        if (_raiz) { _raiz.gameObject.SetActive(true); MostrarPanel(_panelInicio); return; }

        _mano = _juego.mano;
        var area = _juego.area;
        ElegirFuente();
        if (!sonidoSobre) sonidoSobre = Resources.Load<AudioClip>("Sonidos/ui_sobre");
        if (!sonidoElegir) sonidoElegir = Resources.Load<AudioClip>("Sonidos/ui_elegir");
        _pildora = CrearPildora();
        _arco = CrearArco();

        _raiz = NuevoRect("Menu", area);
        Estirar(_raiz);
        var fondo = _raiz.gameObject.AddComponent<Image>();
        fondo.color = new Color(0.04f, 0.06f, 0.13f, 0.6f);
        fondo.raycastTarget = false;
        // debajo de la vista de la cámara y de la mano, encima de todo lo demás
        _raiz.SetSiblingIndex(IndiceDebajoDelCursor(area));

        _panelInicio = Panel("Inicio");
        Titulo(_panelInicio, titulo, 300f);
        CrearBoton(_panelInicio, "Jugar", null, new Vector2(0f, 40f), new Vector2(480f, 130f), Amarillo,
                   () => MostrarPanel(_panelNivel));
        CrearBoton(_panelInicio, "Salir", null, new Vector2(0f, -140f), new Vector2(480f, 130f), Rojo, Salir);

        _panelNivel = Panel("Dificultad");
        Titulo(_panelNivel, "Elige la dificultad", 330f);
        CrearBoton(_panelNivel, "Fácil", "Como siempre", new Vector2(0f, 150f), new Vector2(620f, 140f), Verde,
                   () => Elegir(FogataGame.Dificultad.Facil));
        CrearBoton(_panelNivel, "Medio", "Cambiando de dedo en orden", new Vector2(0f, -25f), new Vector2(620f, 140f), Amarillo,
                   () => Elegir(FogataGame.Dificultad.Medio));
        CrearBoton(_panelNivel, "Difícil", "Dedos al azar y contra reloj", new Vector2(0f, -200f), new Vector2(620f, 140f), Durazno,
                   () => Elegir(FogataGame.Dificultad.Dificil));
        CrearBoton(_panelNivel, "Volver", null, new Vector2(0f, -360f), new Vector2(300f, 90f), Lila,
                   () => MostrarPanel(_panelInicio));

        _textoAyuda = Texto(_raiz, "", 30, false, Color.white, new Vector2(0f, -475f), new Vector2(1500f, 60f));
        _textoAyuda.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        MostrarPanel(_panelInicio);
    }

    void Update()
    {
        if (Pausa.Activa) return;   // JUEGO UNIFICADO: menú de pausa general abierto
        if (!_raiz || !_raiz.gameObject.activeSelf || !_mano) return;
        float dt = Time.unscaledDeltaTime;
        _espera -= dt;

        bool hayCursor = _mano.ManoDetectada || _mano.UsandoMouse;
        Boton sobre = null;
        if (hayCursor)
            foreach (var b in _botones)
                if (b.rt.gameObject.activeInHierarchy && Dentro(b.rt, _mano.Cursor)) { sobre = b; break; }
        if (sobre != _sobre)
        {
            _sobre = sobre;
            _tSobre = 0f;
            if (sobre != null) _juego.SonarEfecto(sonidoSobre);
        }

        foreach (var b in _botones)
        {
            float s = b == _sobre ? 1.07f : 1f;
            b.rt.localScale = Vector3.Lerp(b.rt.localScale, new Vector3(s, s, 1f), 1f - Mathf.Exp(-15f * dt));
            if (b != _sobre) Llenar(b, 0f);
        }

        if (_sobre != null && _espera <= 0f)
        {
            // con el mouse solo se elige con clic, no por tiempo
            if (!_mano.UsandoMouse) _tSobre += dt;
            float meta = Mathf.Max(0.2f, segundosParaElegir);
            Llenar(_sobre, _tSobre / meta);
            if ((elegirConPinza && _mano.PinzaEmpezo) || _tSobre >= meta) Activar(_sobre);
        }

        _textoAyuda.text = hayCursor
            ? "Pon la mano sobre un botón y haz la pinza, o déjala quieta un momento"
            : "Muestra tu mano a la cámara (o usa el mouse)";
    }

    void Activar(Boton b)
    {
        _espera = 0.6f; // evita elegir dos cosas seguidas al cambiar de panel
        _sobre = null;
        _tSobre = 0f;
        Llenar(b, 0f);
        _juego.SonarEfecto(sonidoElegir);
        b.accion?.Invoke();
    }

    void Elegir(FogataGame.Dificultad nivel)
    {
        _raiz.gameObject.SetActive(false);
        _juego.Iniciar(nivel);
    }

    void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // JUEGO UNIFICADO: Salir vuelve al inicio de Quinta Insignia en lugar de cerrar todo
        if (Application.CanStreamedLevelBeLoaded(NivelesUnificados.EscenaHistoria)) { Transicion.Cargar(NivelesUnificados.EscenaHistoria); return; }   // JUEGO UNIFICADO: al inicio del juego
        Application.Quit();
#endif
    }

    void MostrarPanel(RectTransform p)
    {
        _panelInicio.gameObject.SetActive(p == _panelInicio);
        _panelNivel.gameObject.SetActive(p == _panelNivel);
        _sobre = null;
        _tSobre = 0f;
    }

    static void Llenar(Boton b, float v) => b.relleno.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);

    bool Dentro(RectTransform rt, Vector2 p)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 a = _juego.area.InverseTransformPoint(c[0]);
        Vector2 b = _juego.area.InverseTransformPoint(c[2]);
        return p.x >= Mathf.Min(a.x, b.x) && p.x <= Mathf.Max(a.x, b.x) &&
               p.y >= Mathf.Min(a.y, b.y) && p.y <= Mathf.Max(a.y, b.y);
    }

    int IndiceDebajoDelCursor(RectTransform area)
    {
        int i = area.childCount;
        void Ver(Component c) { if (c && c.transform.parent == area) i = Mathf.Min(i, c.transform.GetSiblingIndex()); }
        var prev = FindFirstObjectByType<CamaraPreview>();
        if (prev && prev.imagen) Ver(prev.imagen.transform.parent);
        Ver(_juego.anilloProgreso);
        Ver(_juego.manoImagen);
        Ver(_juego.cursor);
        return i;
    }

    // =====================================================================
    // CONSTRUCCIÓN DE LA INTERFAZ
    // =====================================================================
    RectTransform Panel(string nombre)
    {
        var rt = NuevoRect(nombre, _raiz);
        Estirar(rt);
        return rt;
    }

    /// <summary>Píldora crema con borde amarillo (como "Quinta Insignia"); la última palabra va en coral.</summary>
    void Titulo(RectTransform padre, string texto, float y)
    {
        var borde = Pildora("TituloBorde", padre, Amarillo, new Vector2(0f, y), new Vector2(920f, 140f));
        Pildora("Titulo", borde.rectTransform, Crema, Vector2.zero, new Vector2(904f, 124f));
        int i = texto.LastIndexOf(' ');
        string rico = i > 0
            ? texto.Substring(0, i + 1) + "<color=#" + ColorUtility.ToHtmlStringRGB(Coral) + ">" + texto.Substring(i + 1) + "</color>"
            : texto;
        var tt = Texto(borde.rectTransform, rico, 62, true, Pizarra, Vector2.zero, new Vector2(880f, 120f));
        tt.supportRichText = true;
        tt.font = _fuenteTitulos;
    }

    /// <summary>
    /// Botón del "Estilo gráfico": píldora con contorno oscuro, sombra inferior del mismo color más oscuro,
    /// brillo curvo arriba a la derecha y texto oscuro.
    /// </summary>
    void CrearBoton(RectTransform padre, string texto, string sub, Vector2 pos, Vector2 tam, Color colorFondo, Action accion)
    {
        // JUEGO UNIFICADO: mismo estilo que el botón "Comenzar" del nivel 3: borde fino, sombra gris abajo,
        // brillos blancos arriba a la izquierda y abajo a la derecha, texto Titan One verde muy oscuro
        const float GROSOR = 4f, PROFUNDIDAD = 14f;
        var rt = NuevoRect("Boton_" + texto, padre);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;

        Color borde = colorFondo == Amarillo ? BordeVerde : Color.Lerp(colorFondo, Color.black, 0.35f);
        Pildora("Sombra", rt, SombraGris, new Vector2(0f, -PROFUNDIDAD), tam + new Vector2(GROSOR * 2f, GROSOR * 2f));
        Pildora("Contorno", rt, borde, Vector2.zero, tam + new Vector2(GROSOR * 2f, GROSOR * 2f));
        var fondo = Pildora("Fondo", rt, colorFondo, Vector2.zero, tam);
        fondo.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        // barra que se llena mientras la mano está encima (recortada a la forma de píldora por la máscara)
        var relleno = NuevoRect("Relleno", fondo.rectTransform);
        relleno.anchorMin = Vector2.zero;
        relleno.anchorMax = new Vector2(0f, 1f);
        relleno.pivot = new Vector2(0f, 0.5f);
        relleno.offsetMin = relleno.offsetMax = Vector2.zero;
        var ri = relleno.gameObject.AddComponent<Image>();
        ri.color = new Color(1f, 1f, 1f, 0.35f);
        ri.raycastTarget = false;

        // brillos (los mismos dibujos del botón del nivel 3, a escala del alto del botón)
        float k = tam.y / 176f;
        if (_brilloArriba)
        {
            var b1 = NuevoRect("BrilloArriba", fondo.rectTransform);
            b1.anchorMin = b1.anchorMax = b1.pivot = new Vector2(0f, 1f);
            b1.anchoredPosition = new Vector2(19f * k, -10f * k);
            b1.sizeDelta = new Vector2(78f, 60f) * k;
            var i1 = b1.gameObject.AddComponent<Image>(); i1.sprite = _brilloArriba; i1.raycastTarget = false;
        }
        if (_brilloAbajo)
        {
            var b2 = NuevoRect("BrilloAbajo", fondo.rectTransform);
            b2.anchorMin = b2.anchorMax = b2.pivot = new Vector2(1f, 0f);
            b2.anchoredPosition = new Vector2(-31f * k, 30f * k);
            b2.sizeDelta = new Vector2(68f, 41f) * k;
            var i2 = b2.gameObject.AddComponent<Image>(); i2.sprite = _brilloAbajo; i2.raycastTarget = false;
        }

        if (string.IsNullOrEmpty(sub))
            Texto(fondo.rectTransform, texto, 56, true, TextoOscuro, new Vector2(0f, 4f), tam).font = _fuenteTitulos;
        else
        {
            Texto(fondo.rectTransform, texto, 52, true, TextoOscuro, new Vector2(0f, 20f), new Vector2(tam.x, 62f)).font = _fuenteTitulos;
            Texto(fondo.rectTransform, sub, 26, false, new Color(TextoOscuro.r, TextoOscuro.g, TextoOscuro.b, 0.8f),
                  new Vector2(0f, -33f), new Vector2(tam.x, 40f));
        }

        _botones.Add(new Boton { rt = rt, relleno = relleno, accion = accion });
    }

    Image Pildora(string nombre, RectTransform padre, Color color, Vector2 pos, Vector2 tam)
    {
        var rt = NuevoRect(nombre, padre);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = _pildora;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text Texto(RectTransform padre, string texto, int tam, bool grueso, Color color, Vector2 pos, Vector2 size)
    {
        var rt = NuevoRect("Texto", padre);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = rt.gameObject.AddComponent<Text>();
        t.font = _fuente;
        t.text = texto;
        t.fontSize = tam;
        t.fontStyle = grueso && !_fuenteGruesa ? FontStyle.Bold : FontStyle.Normal;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static RectTransform NuevoRect(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Círculo blanco con borde suave; usado como Sliced da una píldora de cualquier tamaño.</summary>
    public static Sprite CrearPildora()
    {
        const int N = 128;
        const float R = N * 0.5f - 0.5f;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x + 0.5f - N * 0.5f, dy = y + 0.5f - N * 0.5f;
                float a = Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        const float b = N * 0.5f - 1f;
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    /// <summary>Arco blanco de un cuarto de círculo (esquina superior derecha) para el brillo de los botones.</summary>
    static Sprite CrearArco()
    {
        const int N = 64;
        const float R = N * 0.68f, GROSOR = N * 0.14f;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f; // centro del círculo en la esquina inferior izquierda
                float r = Mathf.Sqrt(fx * fx + fy * fy);
                float ang = Mathf.Atan2(fy, fx) * Mathf.Rad2Deg;
                float a = Mathf.Clamp01(GROSOR * 0.5f - Mathf.Abs(r - R) + 0.5f);
                a *= Mathf.Clamp01((ang - 18f) / 10f) * Mathf.Clamp01((70f - ang) / 10f); // puntas suaves
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), Vector2.zero, 100f);
    }

    void ElegirFuente()
    {
        // JUEGO UNIFICADO: las fuentes de los otros niveles (Titan One y Nunito Sans), copiadas en Resources/EstiloQuinta
        _fuente = fuente ? fuente : Resources.Load<Font>("EstiloQuinta/NunitoSans-Bold");
        _fuenteTitulos = Resources.Load<Font>("EstiloQuinta/TitanOneQuinta");   // copia de Titan One con la "í" corregida (la original casi no muestra la tilde)
        if (!_fuente) _fuente = _juego.textoInstruccion ? _juego.textoInstruccion.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!_fuenteTitulos) _fuenteTitulos = _fuente;
        _fuenteGruesa = true;   // las dos ya son gruesas: no se finge negrita

        var t1 = Resources.Load<Texture2D>("EstiloQuinta/BrilloArriba");
        var t2 = Resources.Load<Texture2D>("EstiloQuinta/BrilloAbajo");
        if (t1) _brilloArriba = Sprite.Create(t1, new Rect(0, 0, t1.width, t1.height), new Vector2(0.5f, 0.5f), 100f);
        if (t2) _brilloAbajo = Sprite.Create(t2, new Rect(0, 0, t2.width, t2.height), new Vector2(0.5f, 0.5f), 100f);

        if (fuenteEnTodoElJuego)
        {
            foreach (var t in new[] { _juego.textoInstruccion, _juego.textoAyuda })
                if (t) { t.font = _fuente; t.fontStyle = FontStyle.Normal; }
            if (_juego.textoContador) { _juego.textoContador.font = _fuenteTitulos; _juego.textoContador.fontStyle = FontStyle.Normal; }
            // el resto de textos de la escena (vista de la cámara, "Tu mano"...) con Nunito Sans
            foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.gameObject.scene == gameObject.scene && (t.font == null || t.font.name.Contains("Arial") || t.font.name.Contains("LegacyRuntime")))
                { t.font = _fuente; t.fontStyle = FontStyle.Normal; }
        }
    }

    void OnDestroy()
    {
        if (_pildora) { Destroy(_pildora.texture); Destroy(_pildora); }
        if (_arco) { Destroy(_arco.texture); Destroy(_arco); }
    }

    static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString("#" + h, out var c);
        return c;
    }
}
