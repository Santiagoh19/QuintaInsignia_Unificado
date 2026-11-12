using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Construye las escenas de la misión (leña y pesca) con la estética Quinta Insignia.
/// Menú: Juego > Crear escenas de la leña
/// - SampleScene: bifurcación (se guarda antes una copia en SampleScene_Original).
/// - CaminoLena: esquivar troncos que ruedan (3 vidas).
/// - Lena: cortar y recolectar leña.
/// - Pesca: armar la caña y pescar (3 vidas).
/// </summary>
public static class CrearEscenasJuego
{
    const string ImgInicio = "Assets/Imagenes/Inicio/";
    const string ImgJuego = "Assets/Imagenes/Juego/";
    const string Escenas = "Assets/Scenes/";

    static readonly Color VerdePastel = Hex("#EDF3E5");
    static readonly Color VerdeProfundo = Hex("#18392B");
    static readonly Color AzulMontana = Hex("#365F73");
    static readonly Color GrisMedio = Hex("#879188");
    static readonly Color Amarillo = Hex("#F4C95D");
    static readonly Color VerdeClaro = Hex("#DCE8C8");
    static readonly Color VerdeNaturaleza = Hex("#4F8A5B");
    static readonly Color NaranjaExplorador = Hex("#EF8354");
    static readonly Color GrisOscuro = Hex("#505A52");

    static TMP_FontAsset titan, nunito;

    struct Hud
    {
        public TextMeshProUGUI objetivo, ayuda, contador, mensaje;
        public RectTransform icono;
        public CanvasGroup grupoMensaje;
        public Vidas vidas;
    }

    [MenuItem("Juego/Crear escenas de la leña")]
    public static void CrearTodo()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Preparar();

        string original = Escenas + "SampleScene_Original.unity";
        if (!File.Exists(original) && File.Exists(Escenas + "SampleScene.unity"))
            AssetDatabase.CopyAsset(Escenas + "SampleScene.unity", original);

        CrearBifurcacion();
        CrearCaminoLena();
        CrearLena();
        CrearPesca();

        var orden = new[] { "Inicio", "SampleScene", "CaminoLena", "Lena", "Pesca" };
        EditorBuildSettings.scenes = orden
            .Select(n => Escenas + n + ".unity")
            .Where(File.Exists)
            .Select(p => new EditorBuildSettingsScene(p, true))
            .ToArray();

        EditorSceneManager.OpenScene(Escenas + "SampleScene.unity");
        Debug.Log("Escenas creadas: SampleScene (bifurcación), CaminoLena, Lena y Pesca.");
    }

    // ------------------------------------------------------------------ Escenas

    static void CrearBifurcacion()
    {
        var canvas = NuevaEscena(out var escena);
        var fondo = Img("Fondo", canvas, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imagenes/Fondo.png"));
        Stretch(fondo.rectTransform);

        var destinoIzq = Vacio("DestinoIzquierda", canvas, new Vector2(-560, 30));
        var destinoDer = Vacio("DestinoDerecha", canvas, new Vector2(560, 30));

        var flechaIzq = Img("FlechaIzquierda", canvas, Spr(ImgJuego, "Flecha"));
        Place(flechaIzq.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-330, -150), new Vector2(96, 112));
        flechaIzq.rectTransform.localRotation = Quaternion.Euler(0, 0, 50);
        var flechaDer = Img("FlechaDerecha", canvas, Spr(ImgJuego, "Flecha"));
        Place(flechaDer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330, -150), new Vector2(96, 112));
        flechaDer.rectTransform.localRotation = Quaternion.Euler(0, 0, -50);

        var lucas = Img("Lucas", canvas, Spr(ImgInicio, "Lucas"));
        lucas.preserveAspect = true;
        Place(lucas.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -310), new Vector2(300, 420));

        var hud = CrearHud(canvas, "Leña y comida", "Ve por la leña", "Inclínate a izquierda o derecha para elegir camino",
            Spr(ImgJuego, "Lena"), false);
        var peces = Contador(canvas, "TarjetaPeces", Spr(ImgJuego, "Pez"), new Vector2(-185, -420), out _);

        var logica = new GameObject("Bifurcacion").AddComponent<Bifurcacion>();
        logica.lucas = lucas.rectTransform;
        logica.destinoIzquierda = destinoIzq;
        logica.destinoDerecha = destinoDer;
        logica.flechaIzquierda = flechaIzq.rectTransform;
        logica.flechaDerecha = flechaDer.rectTransform;
        logica.textoObjetivo = hud.objetivo;
        logica.textoAyuda = hud.ayuda;
        logica.textoLena = hud.contador;
        logica.textoPeces = peces;
        logica.mensaje = hud.grupoMensaje;
        logica.textoMensaje = hud.mensaje;

        EditorSceneManager.SaveScene(escena, Escenas + "SampleScene.unity");
    }

    static void CrearCaminoLena()
    {
        var canvas = NuevaEscena(out var escena);
        var fondo = Img("Fondo", canvas, Spr(ImgJuego, "FondoSendero"));
        Stretch(fondo.rectTransform);

        var mundo = Mundo(canvas);

        var lucas = Img("Lucas", mundo, Spr(ImgInicio, "Lucas"));
        lucas.preserveAspect = true;
        Place(lucas.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -235), new Vector2(230, 322));

        var hud = CrearHud(canvas, "Camino de la leña", "Llega hasta la leña",
            "Inclínate o muévete a izquierda o derecha para esquivar", Spr(ImgJuego, "Lena"), true);

        // Barra de progreso del camino (arriba al centro)
        var tarjeta = Img("TarjetaCamino", canvas, Spr(ImgJuego, "Tarjeta"));
        tarjeta.type = Image.Type.Sliced;
        Place(tarjeta.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(620, 120));
        var etiqueta = Txt("Etiqueta", tarjeta.rectTransform, nunito, "Camino recorrido", 26, GrisMedio);
        Stretch(etiqueta.rectTransform, 30, 30, 10, 70);
        etiqueta.alignment = TextAlignmentOptions.Left;
        var fondoBarra = Img("FondoBarra", tarjeta.rectTransform, null);
        fondoBarra.color = VerdeClaro;
        Stretch(fondoBarra.rectTransform, 30, 130, 60, 26);
        var relleno = Img("Relleno", fondoBarra.rectTransform, null);
        relleno.color = Amarillo;
        var rr = relleno.rectTransform;
        rr.anchorMin = Vector2.zero; rr.anchorMax = new Vector2(0, 1); rr.offsetMin = rr.offsetMax = Vector2.zero;
        var porcentaje = Txt("Porcentaje", tarjeta.rectTransform, titan, "0%", 36, VerdeProfundo);
        Stretch(porcentaje.rectTransform, 500, 20, 50, 16);

        var logica = new GameObject("CaminoLena").AddComponent<CaminoLena>();
        logica.mundo = mundo;
        logica.lucas = lucas.rectTransform;
        logica.imagenLucas = lucas;
        logica.barraRelleno = rr;
        logica.textoProgreso = porcentaje;
        logica.mensaje = hud.grupoMensaje;
        logica.textoMensaje = hud.mensaje;
        logica.spritesRodando = new[] { Spr(ImgJuego, "TroncoRodando1"), Spr(ImgJuego, "TroncoRodando2"), Spr(ImgJuego, "TroncoRodando3") };
        logica.spriteSombra = Spr(ImgJuego, "Sombra");
        logica.vidas = hud.vidas;
        logica.sueloY = -390f;

        EditorSceneManager.SaveScene(escena, Escenas + "CaminoLena.unity");
    }

    static void CrearLena()
    {
        var canvas = NuevaEscena(out var escena);
        var fondo = Img("Fondo", canvas, Spr(ImgJuego, "FondoClaro"));
        Stretch(fondo.rectTransform);

        var mundo = Mundo(canvas);

        float[] xs = { -380f, 220f, 760f };
        var tocones = new RectTransform[xs.Length];
        for (int i = 0; i < xs.Length; i++)
        {
            var t = Img("Tronco" + (i + 1), mundo, Spr(ImgJuego, "Tocon"));
            t.preserveAspect = true;
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(xs[i], -260), new Vector2(250, 216));
            tocones[i] = t.rectTransform;
        }

        var lucas = Img("Lucas", mundo, Spr(ImgInicio, "Lucas"));
        lucas.preserveAspect = true;
        Place(lucas.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-800, -200), new Vector2(260, 364));

        var pila = Vacio("PilaLena", lucas.rectTransform, new Vector2(-120, -40));
        pila.sizeDelta = new Vector2(110, 55);

        var hacha = Img("Hacha", lucas.rectTransform, Spr(ImgJuego, "Hacha"));
        Place(hacha.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120, 10), new Vector2(150, 177));
        hacha.rectTransform.pivot = new Vector2(0.45f, 0.1f);

        var hud = CrearHud(canvas, "Bosque de la leña", "Acércate a los troncos para cortar leña",
            "Inclínate o muévete a izquierda o derecha para avanzar", Spr(ImgJuego, "Lena"), false);

        var logica = new GameObject("RecolectarLena").AddComponent<RecolectarLena>();
        logica.mundo = mundo;
        logica.lucas = lucas.rectTransform;
        logica.hacha = hacha.rectTransform;
        logica.pilaLucas = pila;
        logica.tocones = tocones;
        logica.iconoContador = hud.icono;
        logica.textoContador = hud.contador;
        logica.textoObjetivo = hud.objetivo;
        logica.mensaje = hud.grupoMensaje;
        logica.textoMensaje = hud.mensaje;
        logica.spriteLena = Spr(ImgJuego, "Lena");

        EditorSceneManager.SaveScene(escena, Escenas + "Lena.unity");
    }

    static void CrearPesca()
    {
        var canvas = NuevaEscena(out var escena);
        var fondo = Img("Fondo", canvas, Spr(ImgJuego, "FondoLago"));
        Stretch(fondo.rectTransform);

        var mundo = Mundo(canvas);
        Vector2 agua = new Vector2(470, -300);

        var hilo = Img("Hilo", mundo, Spr(ImgJuego, "Hilo"));
        hilo.preserveAspect = true;
        Place(hilo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-580, -320), new Vector2(115, 100));
        var palo = Img("Palo", mundo, Spr(ImgJuego, "Palo"));
        palo.preserveAspect = true;
        Place(palo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-380, -330), new Vector2(240, 96));

        var lucas = Img("Lucas", mundo, Spr(ImgInicio, "Lucas"));
        lucas.preserveAspect = true;
        Place(lucas.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-820, -190), new Vector2(260, 364));

        var cana = Img("Cana", lucas.rectTransform, Spr(ImgJuego, "Cana"));
        cana.preserveAspect = true;
        Place(cana.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 210));
        cana.rectTransform.pivot = new Vector2(0.1f, 0.07f);
        cana.rectTransform.anchoredPosition = new Vector2(70, -35);
        var punta = Vacio("Punta", cana.rectTransform, Vector2.zero);
        punta.anchorMin = punta.anchorMax = new Vector2(0.91f, 0.91f);
        punta.anchoredPosition = Vector2.zero;

        var linea = Img("Linea", mundo, Spr(ImgJuego, "Linea"));
        linea.color = GrisOscuro;
        Place(linea.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 4));
        linea.rectTransform.pivot = new Vector2(0f, 0.5f);

        var boya = Img("Boya", mundo, Spr(ImgJuego, "Boya"));
        Place(boya.rectTransform, new Vector2(0.5f, 0.5f), agua, new Vector2(64, 64));
        var salpicadura = Img("Salpicadura", mundo, Spr(ImgJuego, "Salpicadura"));
        Place(salpicadura.rectTransform, new Vector2(0.5f, 0.5f), agua, new Vector2(200, 130));
        var pez = Img("Pez", mundo, Spr(ImgJuego, "Pez"));
        Place(pez.rectTransform, new Vector2(0.5f, 0.5f), agua, new Vector2(180, 112));
        var destello = Img("Destello", mundo, Spr(ImgJuego, "Destello"));
        Place(destello.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
        var signo = Txt("SignoPica", mundo, titan, "!", 120, NaranjaExplorador);
        Place(signo.rectTransform, new Vector2(0.5f, 0.5f), agua + new Vector2(0, 110), new Vector2(100, 150));

        // Panel para mantener C (sobre el agua)
        var panel = new GameObject("PanelRecoger", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        panel.SetParent(mundo, false);
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(agua.x, 70), new Vector2(240, 260));
        var grupoRecoger = panel.GetComponent<CanvasGroup>();
        var aroFondo = Img("AroFondo", panel, Spr(ImgJuego, "Circulo"));
        aroFondo.color = VerdeClaro;
        Place(aroFondo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(190, 190));
        var aro = Img("Aro", panel, Spr(ImgJuego, "Circulo"));
        aro.color = VerdeNaturaleza;
        aro.type = Image.Type.Filled;
        aro.fillMethod = Image.FillMethod.Radial360;
        aro.fillOrigin = (int)Image.Origin360.Top;
        aro.fillClockwise = true;
        aro.fillAmount = 0f;
        Place(aro.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(190, 190));
        var centro = Img("Centro", panel, Spr(ImgJuego, "Circulo"));
        centro.color = Hex("#FFFCF6");
        Place(centro.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(150, 150));
        var tecla = Img("Tecla", panel, Spr(ImgJuego, "Tecla"));
        Place(tecla.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(96, 96));
        var letraC = Txt("Letra", tecla.rectTransform, titan, "C", 56, VerdeProfundo);
        Stretch(letraC.rectTransform, 0, 0, 0, 12);
        var tiempoFondo = Img("TiempoFondo", panel, null);
        tiempoFondo.color = VerdeClaro;
        Place(tiempoFondo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(220, 22));
        var tiempo = Img("Tiempo", tiempoFondo.rectTransform, null);
        tiempo.color = NaranjaExplorador;
        var tr = tiempo.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;

        var hud = CrearHud(canvas, "Lago de la pesca", "Recoge el palo y el hilo",
            "Inclínate o muévete a izquierda o derecha para avanzar", Spr(ImgJuego, "Pez"), true);

        // Materiales (hilo y palo)
        var mat = Img("TarjetaMateriales", canvas, Spr(ImgJuego, "Tarjeta"));
        mat.type = Image.Type.Sliced;
        Place(mat.rectTransform, new Vector2(0, 1), new Vector2(215, -365), new Vector2(370, 130));
        var etiquetaMat = Txt("Etiqueta", mat.rectTransform, nunito, "Materiales", 22, GrisMedio);
        Stretch(etiquetaMat.rectTransform, 28, 28, 10, 90);
        etiquetaMat.alignment = TextAlignmentOptions.Left;
        var slotHilo = Img("SlotHilo", mat.rectTransform, Spr(ImgJuego, "Hilo"));
        slotHilo.preserveAspect = true;
        Place(slotHilo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-80, -14), new Vector2(84, 74));
        var slotPalo = Img("SlotPalo", mat.rectTransform, Spr(ImgJuego, "Palo"));
        slotPalo.preserveAspect = true;
        Place(slotPalo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70, -14), new Vector2(160, 64));

        // Indicador de tecla (arriba al centro)
        var pt = Img("PanelTecla", canvas, Spr(ImgJuego, "Tarjeta"));
        pt.type = Image.Type.Sliced;
        Place(pt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -235), new Vector2(760, 120));
        var grupoTecla = pt.gameObject.AddComponent<CanvasGroup>();
        var teclaHud = Img("Tecla", pt.rectTransform, Spr(ImgJuego, "Tecla"));
        Place(teclaHud.rectTransform, new Vector2(0, 0.5f), new Vector2(75, 0), new Vector2(88, 88));
        var letra = Txt("Letra", teclaHud.rectTransform, titan, "B", 50, VerdeProfundo);
        Stretch(letra.rectTransform, 0, 0, 0, 10);
        var textoTecla = Txt("Texto", pt.rectTransform, titan, "", 34, AzulMontana);
        Stretch(textoTecla.rectTransform, 140, 24, 12, 12);
        textoTecla.alignment = TextAlignmentOptions.Left;
        textoTecla.enableAutoSizing = true; textoTecla.fontSizeMin = 22; textoTecla.fontSizeMax = 34;

        var logica = new GameObject("Pesca").AddComponent<Pesca>();
        logica.mundo = mundo;
        logica.lucas = lucas.rectTransform;
        logica.palo = palo.rectTransform;
        logica.hilo = hilo.rectTransform;
        logica.cana = cana.rectTransform;
        logica.puntaCana = punta;
        logica.linea = linea.rectTransform;
        logica.boya = boya.rectTransform;
        logica.pez = pez.rectTransform;
        logica.salpicadura = salpicadura.rectTransform;
        logica.destello = destello.rectTransform;
        logica.signoPica = signo;
        logica.slotPalo = slotPalo;
        logica.slotHilo = slotHilo;
        logica.panelTecla = grupoTecla;
        logica.letraTecla = letra;
        logica.textoTecla = textoTecla;
        logica.panelRecoger = grupoRecoger;
        logica.anilloRecoger = aro;
        logica.barraTiempo = tr;
        logica.textoObjetivo = hud.objetivo;
        logica.textoAyuda = hud.ayuda;
        logica.textoContador = hud.contador;
        logica.iconoContador = hud.icono;
        logica.mensaje = hud.grupoMensaje;
        logica.textoMensaje = hud.mensaje;
        logica.vidas = hud.vidas;
        logica.puntoAgua = agua;

        EditorSceneManager.SaveScene(escena, Escenas + "Pesca.unity");
    }

    // ------------------------------------------------------------------ HUD común

    static Hud CrearHud(RectTransform canvas, string mision, string objetivo, string ayuda, Sprite icono, bool conVidas)
    {
        var hud = new Hud();

        // Nombre de la misión
        var pildora = Img("Mision", canvas, Spr(ImgInicio, "PildoraTitulo"));
        pildora.type = Image.Type.Sliced;
        Place(pildora.rectTransform, new Vector2(0, 1), new Vector2(395, -95), new Vector2(730, 140));
        var tri = Img("Triangulo", pildora.rectTransform, Spr(ImgInicio, "Triangulo"));
        Place(tri.rectTransform, new Vector2(0, 0.5f), new Vector2(58, 0), new Vector2(34, 30));
        var pen = Img("Pentagono", pildora.rectTransform, Spr(ImgInicio, "Pentagono"));
        Place(pen.rectTransform, new Vector2(1, 0.5f), new Vector2(-58, 0), new Vector2(34, 32));
        var titulo = Txt("Texto", pildora.rectTransform, titan, mision, 52, AzulMontana);
        Stretch(titulo.rectTransform, 120, 120, 16, 16);
        titulo.textWrappingMode = TextWrappingModes.NoWrap;
        titulo.enableAutoSizing = true; titulo.fontSizeMin = 30; titulo.fontSizeMax = 54;

        // Vidas
        if (conVidas)
        {
            var tv = Img("TarjetaVidas", canvas, Spr(ImgJuego, "Tarjeta"));
            tv.type = Image.Type.Sliced;
            Place(tv.rectTransform, new Vector2(0, 1), new Vector2(185, -235), new Vector2(310, 100));
            var corazones = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                corazones[i] = Img("Corazon" + (i + 1), tv.rectTransform, Spr(ImgJuego, "Corazon"));
                corazones[i].preserveAspect = true;
                Place(corazones[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-88 + i * 88, 0), new Vector2(72, 67));
            }
            hud.vidas = tv.gameObject.AddComponent<Vidas>();
            hud.vidas.corazones = corazones;
            hud.vidas.lleno = Spr(ImgJuego, "Corazon");
            hud.vidas.vacio = Spr(ImgJuego, "CorazonVacio");
        }

        // Objetivo actual
        var tarjeta = Img("TarjetaObjetivo", canvas, Spr(ImgJuego, "Tarjeta"));
        tarjeta.type = Image.Type.Sliced;
        Place(tarjeta.rectTransform, new Vector2(1, 1), new Vector2(-340, -125), new Vector2(620, 190));
        var etiqueta = Txt("Etiqueta", tarjeta.rectTransform, nunito, "Objetivo actual", 26, GrisMedio);
        Stretch(etiqueta.rectTransform, 34, 34, 18, 140);
        etiqueta.alignment = TextAlignmentOptions.Left;
        hud.objetivo = Txt("Objetivo", tarjeta.rectTransform, titan, objetivo, 40, VerdeProfundo);
        Stretch(hud.objetivo.rectTransform, 34, 34, 50, 60);
        hud.objetivo.alignment = TextAlignmentOptions.Left;
        hud.objetivo.enableAutoSizing = true; hud.objetivo.fontSizeMin = 26; hud.objetivo.fontSizeMax = 40;
        hud.ayuda = Txt("Ayuda", tarjeta.rectTransform, nunito, ayuda, 24, AzulMontana);
        Stretch(hud.ayuda.rectTransform, 34, 34, 135, 16);
        hud.ayuda.alignment = TextAlignmentOptions.Left;
        hud.ayuda.enableAutoSizing = true; hud.ayuda.fontSizeMin = 18; hud.ayuda.fontSizeMax = 24;

        // Contador
        hud.contador = Contador(canvas, "TarjetaContador", icono, new Vector2(-185, -295), out hud.icono);

        // Mensaje central
        var msj = Img("Mensaje", canvas, Spr(ImgJuego, "Tarjeta"));
        msj.type = Image.Type.Sliced;
        Place(msj.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1060, 280));
        hud.grupoMensaje = msj.gameObject.AddComponent<CanvasGroup>();
        hud.grupoMensaje.alpha = 0f;
        hud.grupoMensaje.blocksRaycasts = false;
        hud.mensaje = Txt("Texto", msj.rectTransform, titan, "", 50, AzulMontana);
        Stretch(hud.mensaje.rectTransform, 50, 50, 30, 30);
        hud.mensaje.enableAutoSizing = true; hud.mensaje.fontSizeMin = 30; hud.mensaje.fontSizeMax = 52;

        return hud;
    }

    static TextMeshProUGUI Contador(RectTransform canvas, string nombre, Sprite icono, Vector2 pos, out RectTransform iconoRt)
    {
        var inv = Img(nombre, canvas, Spr(ImgJuego, "Tarjeta"));
        inv.type = Image.Type.Sliced;
        Place(inv.rectTransform, new Vector2(1, 1), pos, new Vector2(310, 110));
        var ic = Img("Icono", inv.rectTransform, icono);
        ic.preserveAspect = true;
        Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(95, 0), new Vector2(130, 70));
        iconoRt = ic.rectTransform;
        var t = Txt("Contador", inv.rectTransform, titan, "0/3", 46, VerdeProfundo);
        Stretch(t.rectTransform, 170, 20, 10, 10);
        return t;
    }

    // ------------------------------------------------------------------ Utilidades

    static void Preparar()
    {
        foreach (var ruta in Directory.GetFiles(ImgJuego, "*.png"))
        {
            var p = ruta.Replace('\\', '/');
            var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 2048;
            if (p.EndsWith("Tarjeta.png")) imp.spriteBorder = new Vector4(50, 50, 50, 50);
            imp.SaveAndReimport();
        }
        titan = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fuentes/TitanOne SDF.asset");
        nunito = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fuentes/NunitoSans SDF.asset");
        if (titan == null || nunito == null)
            Debug.LogWarning("No se encontraron las fuentes TMP. Ejecuta primero Juego > Crear escena Inicio.");
    }

    static RectTransform NuevaEscena(out UnityEngine.SceneManagement.Scene escena)
    {
        escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var c = cam.GetComponent<Camera>();
        c.orthographic = true;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = VerdePastel;
        cam.transform.position = new Vector3(0, 0, -10);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return go.GetComponent<RectTransform>();
    }

    static RectTransform Mundo(RectTransform canvas)
    {
        var mundo = new GameObject("Mundo", typeof(RectTransform)).GetComponent<RectTransform>();
        mundo.gameObject.layer = LayerMask.NameToLayer("UI");
        mundo.SetParent(canvas, false);
        Stretch(mundo);
        return mundo;
    }

    static Sprite Spr(string carpeta, string nombre) => AssetDatabase.LoadAssetAtPath<Sprite>(carpeta + nombre + ".png");

    static Image Img(string nombre, Transform padre, Sprite sprite)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static RectTransform Vacio(string nombre, Transform padre, Vector2 pos)
    {
        var rt = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    static TextMeshProUGUI Txt(string nombre, Transform padre, TMP_FontAsset fuente, string texto, float tam, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    static void Place(RectTransform r, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        r.anchorMin = r.anchorMax = ancla;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = tam;
    }

    static void Stretch(RectTransform r, float izq = 0, float der = 0, float arriba = 0, float abajo = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(izq, abajo);
        r.offsetMax = new Vector2(-der, -arriba);
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
