using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Construye la escena "Inicio" con la estética del Figma (Quinta Insignia).
/// Menú: Juego > Crear escena Inicio
/// </summary>
[InitializeOnLoad]
public static class CrearEscenaInicio
{
    // Se ejecuta sola una vez, si la escena Inicio aún no existe.
    static CrearEscenaInicio()
    {
        if (File.Exists(RutaEscena) || SessionState.GetBool("InicioIntentado", false)) return;
        SessionState.SetBool("InicioIntentado", true);
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Crear();
        };
    }

    const string CarpetaImg = "Assets/Imagenes/Inicio/";
    const string CarpetaFuentes = "Assets/Fuentes/";
    const string RutaEscena = "Assets/Scenes/Inicio.unity";

    // Paleta del Figma
    static readonly Color VerdePastel = Hex("#EDF3E5");
    static readonly Color VerdeBosqueProfundo = Hex("#18392B");
    static readonly Color TextoBoton = Hex("#001905");

    const string Caracteres =
        "ABCDEFGHIJKLMNÑOPQRSTUVWXYZabcdefghijklmnñopqrstuvwxyz0123456789" +
        "áéíóúÁÉÍÓÚüÜ¡!¿?.,;:-_()\"'%/ ";

    [MenuItem("Juego/Crear escena Inicio")]
    public static void Crear()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        ConfigurarSprites();
        var titan = CrearFuente(CarpetaFuentes + "TitanOne-Regular.ttf", CarpetaFuentes + "TitanOne SDF.asset");
        var nunito = CrearFuente(CarpetaFuentes + "NunitoSans-Regular.ttf", CarpetaFuentes + "NunitoSans SDF.asset");

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Cámara
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var c = cam.GetComponent<Camera>();
        c.orthographic = true;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = AplicarNoche.AzulNoche;
        cam.transform.position = new Vector3(0, 0, -10);

        // EventSystem (Input Manager clásico)
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Canvas 1920x1080
        var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = LayerMask.NameToLayer("UI");
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGO.GetComponent<RectTransform>();

        // Fondo
        var fondo = Img("Fondo", canvas, null);
        fondo.color = AplicarNoche.AzulNoche;
        Stretch(fondo.rectTransform);
        var estrellas = Img("Estrellas", canvas, Spr("Estrellas"));
        estrellas.raycastTarget = false;
        Stretch(estrellas.rectTransform);

        // Círculos decorativos
        var lav = Img("CirculoLavanda", canvas, Spr("CirculoLavanda"));
        Place(lav.rectTransform, new Vector2(0, 1), new Vector2(-40, 60), new Vector2(620, 620));
        var azul = Img("CirculoAzul", canvas, Spr("CirculoAzul"));
        Place(azul.rectTransform, new Vector2(1, 0), new Vector2(40, 0), new Vector2(760, 760));

        // Césped
        var cesped = Img("Cesped", canvas, Spr("Cesped"));
        var rc = cesped.rectTransform;
        rc.anchorMin = new Vector2(0, 0); rc.anchorMax = new Vector2(1, 0); rc.pivot = new Vector2(0.5f, 0);
        rc.anchoredPosition = Vector2.zero; rc.sizeDelta = new Vector2(0, 150);

        // Lucas
        var lucas = Img("Lucas", canvas, Spr("Lucas"));
        lucas.preserveAspect = true;
        Place(lucas.rectTransform, new Vector2(0, 0), new Vector2(420, 360), new Vector2(480, 672));

        // ---- Columna derecha ----
        var contenido = new GameObject("Contenido", typeof(RectTransform)).GetComponent<RectTransform>();
        contenido.SetParent(canvas, false);
        contenido.gameObject.layer = canvasGO.layer;
        contenido.anchorMin = contenido.anchorMax = new Vector2(0.5f, 0.5f);
        contenido.anchoredPosition = new Vector2(230, 40);
        contenido.sizeDelta = new Vector2(1100, 760);

        // Título (píldora)
        var pildora = Img("Titulo", contenido, Spr("PildoraTitulo"));
        pildora.type = Image.Type.Sliced;
        Place(pildora.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 270), new Vector2(1000, 200));

        var tri = Img("Triangulo", pildora.rectTransform, Spr("Triangulo"));
        Place(tri.rectTransform, new Vector2(0, 0.5f), new Vector2(85, 0), new Vector2(62, 55));
        var pen = Img("Pentagono", pildora.rectTransform, Spr("Pentagono"));
        Place(pen.rectTransform, new Vector2(1, 0.5f), new Vector2(-85, 0), new Vector2(62, 59));

        var titulo = Txt("TextoTitulo", pildora.rectTransform, titan,
            "<color=#365F73>Cruce del</color> <color=#4B91B5>bosque</color>", 84, Color.white);
        Stretch(titulo.rectTransform, 140, 140, 20, 20);
        titulo.textWrappingMode = TextWrappingModes.NoWrap;
        titulo.enableAutoSizing = true; titulo.fontSizeMin = 50; titulo.fontSizeMax = 88;

        // Subtítulo
        var subtitulo = Txt("Subtitulo", contenido, titan, "¡Continuemos la aventura!", 50, AplicarNoche.AmarilloAventura);
        Place(subtitulo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1100, 80));
        subtitulo.textWrappingMode = TextWrappingModes.NoWrap;
        subtitulo.enableAutoSizing = true; subtitulo.fontSizeMin = 34; subtitulo.fontSizeMax = 52;

        // Párrafo
        var parrafo = Txt("Descripcion", contenido, nunito,
            "Ayúdalo a encontrar un poco de leña y comida para volver al campamento y terminar nuestro viaje.",
            44, AplicarNoche.TextoClaro);
        Place(parrafo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1060, 150));

        // Botón Comenzar
        var botonImg = Img("BotonComenzar", contenido, Spr("BotonAmarillo"));
        Place(botonImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -240), new Vector2(512, 176));
        var boton = botonImg.gameObject.AddComponent<Button>();
        boton.targetGraphic = botonImg;
        var colores = boton.colors;
        colores.highlightedColor = new Color(1f, 0.95f, 0.85f);
        colores.pressedColor = new Color(0.9f, 0.85f, 0.75f);
        boton.colors = colores;

        var b1 = Img("BrilloArriba", botonImg.rectTransform, Spr("BrilloArriba"));
        b1.raycastTarget = false;
        PlaceTopLeft(b1.rectTransform, new Vector2(19, -10), new Vector2(78, 60));
        var b2 = Img("BrilloAbajo", botonImg.rectTransform, Spr("BrilloAbajo"));
        b2.raycastTarget = false;
        PlaceTopLeft(b2.rectTransform, new Vector2(413, -105), new Vector2(68, 41));

        var textoBoton = Txt("Texto", botonImg.rectTransform, titan, "Comenzar", 64, TextoBoton);
        Stretch(textoBoton.rectTransform, 6, 6, 0, 16);

        // Lógica
        var logica = new GameObject("PantallaInicio").AddComponent<PantallaInicio>();
        logica.botonComenzar = boton;
        logica.boton = botonImg.rectTransform;
        logica.lucas = lucas.rectTransform;
        logica.escenaJuego = "SampleScene";

        EditorSceneManager.SaveScene(escena, RutaEscena);
        AgregarABuildSettings();
        Debug.Log("Escena Inicio creada en " + RutaEscena);
    }

    // ---------- Utilidades ----------

    static void ConfigurarSprites()
    {
        foreach (var ruta in Directory.GetFiles(CarpetaImg, "*.png"))
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
            if (p.EndsWith("PildoraTitulo.png")) imp.spriteBorder = new Vector4(100, 100, 100, 100);
            imp.SaveAndReimport();
        }
    }

    static TMP_FontAsset CrearFuente(string ttf, string destino)
    {
        var existente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destino);
        if (existente != null) return existente;

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = Path.GetFileNameWithoutExtension(destino);
        AssetDatabase.CreateAsset(fa, destino);
        fa.atlasTextures[0].name = fa.name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters(Caracteres, out _);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    static void AgregarABuildSettings()
    {
        var lista = EditorBuildSettings.scenes.Where(s => s.path != RutaEscena).ToList();
        lista.Insert(0, new EditorBuildSettingsScene(RutaEscena, true));
        EditorBuildSettings.scenes = lista.ToArray();
    }

    static Sprite Spr(string nombre) => AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaImg + nombre + ".png");

    static Image Img(string nombre, Transform padre, Sprite sprite)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        return img;
    }

    static TextMeshProUGUI Txt(string nombre, Transform padre, TMP_FontAsset fuente, string texto, float tam, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = fuente;
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

    static void PlaceTopLeft(RectTransform r, Vector2 pos, Vector2 tam)
    {
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
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
