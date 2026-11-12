using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menú "Fogata > Crear escena de la fogata": arma la escena completa (Canvas, MediaPipe, ramitas,
/// fogata, caja de fósforos, interfaz) y la guarda en Assets/Fogata/Fogata.unity.
/// Requiere que FingersMove.cs (del paquete ManoMediaPipePaquete) ya esté en el proyecto.
/// </summary>
public static class FogataSceneBuilder
{
    const string RUTA = "Assets/Fogata";
    static readonly Vector2 CENTRO_FOGATA = new Vector2(0f, -230f);

    [MenuItem("Fogata/Crear escena de la fogata")]
    public static void Crear()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------- Cámara ----------
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.04f, 0.09f);
        cam.orthographic = true;
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        camGO.AddComponent<AudioListener>();

        // ---------- Canvas ----------
        var canvasGO = new GameObject("Canvas_Fogata", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var area = canvasGO.GetComponent<RectTransform>();

        // Fondo
        var fondo = Img("Fondo", area, S("fondo"), Vector2.zero, new Vector2(1920f, 1080f));
        Estirar(fondo.rectTransform);
        fondo.preserveAspect = false;

        // Resplandor del fuego (detrás de todo lo demás)
        var resplandor = Img("Resplandor", area, S("resplandor"), CENTRO_FOGATA + new Vector2(0f, 80f), new Vector2(2600f, 2600f));
        resplandor.color = new Color(1f, 0.55f, 0.15f, 0f);

        // ---------- Ramitas en el suelo ----------
        var contRamitas = Contenedor("Ramitas", area);
        var spRamita = S("ramita");
        var posiciones = new (Vector2 p, float r)[]
        {
            (new Vector2(-800f, -360f), 78f), (new Vector2(-560f, -150f), 102f), (new Vector2(-350f, -420f), 66f),
            (new Vector2(-280f, -60f), 95f),  (new Vector2(300f, -430f), 110f),  (new Vector2(380f, -110f), 72f),
            (new Vector2(800f, -170f), 88f),
        };
        var ramitas = new List<Ramita>();
        for (int i = 0; i < posiciones.Length; i++)
        {
            var img = Img("Ramita_" + (i + 1), contRamitas, spRamita, posiciones[i].p, new Vector2(60f, 260f), posiciones[i].r);
            ramitas.Add(img.gameObject.AddComponent<Ramita>());
        }

        // ---------- Linterna (oscuridad con un círculo de luz) ----------
        var linterna = Img("LuzLinterna", area, S("luz_linterna"), Vector2.zero, new Vector2(4600f, 4600f));
        linterna.color = new Color(1f, 1f, 1f, 0f);

        // ---------- Fogata ----------
        var contFogata = Contenedor("Fogata", area);
        var centro = Vacio("CentroFogata", contFogata, CENTRO_FOGATA);

        var marca = Img("MarcaFogata", contFogata, S("anillo"), CENTRO_FOGATA, new Vector2(400f, 160f));
        marca.color = new Color(1f, 0.85f, 0.4f, 0.25f);

        var spPiedra = S("piedra");
        const int NUM_PIEDRAS = 10;
        var delanteras = new List<(Vector2, float)>();
        for (int i = 0; i < NUM_PIEDRAS; i++)
        {
            float a = i * Mathf.PI * 2f / NUM_PIEDRAS;
            Vector2 p = CENTRO_FOGATA + new Vector2(Mathf.Cos(a) * 175f, Mathf.Sin(a) * 58f);
            float tam = 1f + 0.15f * Mathf.Sin(i * 2.3f);
            if (Mathf.Sin(a) > 0.01f) Img("Piedra_" + i, contFogata, spPiedra, p, new Vector2(95f, 70f) * tam, i * 17f % 30f - 15f);
            else delanteras.Add((p, tam));
        }

        var monton = Contenedor("Monton", contFogata);
        var slots = new List<RectTransform>();
        for (int i = 0; i < 7; i++)
        {
            int k = i / 2;
            float lado = (i % 2 == 0) ? -1f : 1f;
            var slot = Vacio("Posicion_" + (i + 1), monton, CENTRO_FOGATA + new Vector2(lado * (22f + 16f * k), 105f));
            slot.localEulerAngles = new Vector3(0f, 0f, lado * (24f - 4f * k));
            slots.Add(slot);
        }

        // Fuego (empieza invisible; crece al encender)
        var fuego = Vacio("Fuego", contFogata, CENTRO_FOGATA + new Vector2(0f, 40f));
        var spLlama = S("llama");
        var spCirculo = S("circulo");
        var brasas = Img("Brasas", fuego, spCirculo, new Vector2(0f, -40f), new Vector2(200f, 60f));
        brasas.color = new Color(1f, 0.35f, 0.05f, 0.9f);
        Llama("LlamaGrande", fuego, spLlama, new Vector2(0f, 90f), new Vector2(190f, 290f), new Color(1f, 0.45f, 0.1f, 0.95f));
        Llama("LlamaIzq", fuego, spLlama, new Vector2(-55f, 50f), new Vector2(110f, 170f), new Color(1f, 0.55f, 0.12f, 0.9f));
        Llama("LlamaDer", fuego, spLlama, new Vector2(55f, 45f), new Vector2(100f, 160f), new Color(1f, 0.55f, 0.12f, 0.9f));
        Llama("LlamaMedia", fuego, spLlama, new Vector2(0f, 65f), new Vector2(125f, 200f), new Color(1f, 0.75f, 0.2f, 1f));
        Llama("LlamaCentro", fuego, spLlama, new Vector2(0f, 40f), new Vector2(70f, 115f), new Color(1f, 0.95f, 0.65f, 1f));

        foreach (var (p, tam) in delanteras)
            Img("Piedra_frente", contFogata, spPiedra, p, new Vector2(105f, 78f) * tam, p.x * 0.05f);

        // ---------- Caja de fósforos ----------
        var caja = Img("CajaFosforos", area, S("caja"), new Vector2(620f, -340f), new Vector2(300f, 190f));
        var zona = Vacio("ZonaFrotar", caja.rectTransform, new Vector2(0f, 62f));
        zona.sizeDelta = new Vector2(260f, 36f);

        var contFosforo = Contenedor("FosforoCont", area);
        var fosforo = Img("Fosforo", contFosforo, S("fosforo"), new Vector2(520f, -205f), new Vector2(34f, 200f), 28f);
        fosforo.rectTransform.pivot = new Vector2(0.5f, 0.92f);
        var llamaFos = Llama("LlamaFosforo", fosforo.rectTransform, spLlama, new Vector2(0f, 30f), new Vector2(46f, 72f), new Color(1f, 0.75f, 0.3f, 1f));
        llamaFos.rectTransform.anchorMin = llamaFos.rectTransform.anchorMax = new Vector2(0.5f, 0.92f);

        // ---------- Efectos, anillo y cursor ----------
        var efectos = Contenedor("Efectos", area);

        var anillo = Img("AnilloProgreso", area, S("anillo"), Vector2.zero, new Vector2(120f, 120f));
        anillo.type = Image.Type.Filled;
        anillo.fillMethod = Image.FillMethod.Radial360;
        anillo.fillOrigin = (int)Image.Origin360.Top;
        anillo.fillClockwise = true;
        anillo.color = new Color(1f, 0.85f, 0.3f, 0.95f);

        // Mano que sigue al cursor. El pivote está en el punto donde se juntan pulgar e índice en el dibujo.
        var spManoAbierta = S("mano_abierta");
        var spManoPinza = S("mano_pinza");
        var manoCursor = Img("ManoCursor", area, spManoAbierta, Vector2.zero, new Vector2(170f, 170f));
        manoCursor.rectTransform.pivot = new Vector2(70f / 256f, 1f - 80f / 256f);
        var cursor = Img("Cursor", area, S("anillo"), Vector2.zero, new Vector2(56f, 56f));
        var punto = Img("Punto", cursor.rectTransform, spCirculo, Vector2.zero, new Vector2(12f, 12f));
        punto.color = Color.white;

        // ---------- Interfaz ----------
        var banda = Img("BandaSuperior", area, null, Vector2.zero, Vector2.zero);
        banda.color = new Color(0f, 0f, 0f, 0.45f);
        var rtB = banda.rectTransform;
        rtB.anchorMin = new Vector2(0f, 1f); rtB.anchorMax = new Vector2(1f, 1f);
        rtB.pivot = new Vector2(0.5f, 1f);
        rtB.anchoredPosition = Vector2.zero; rtB.sizeDelta = new Vector2(0f, 200f);

        var fuente = Fuente();
        var instr = Txt("TextoInstruccion", area, fuente, 46, FontStyle.Bold, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -65f), new Vector2(1800f, 110f));
        var ayuda = Txt("TextoAyuda", area, fuente, 32, FontStyle.Normal, new Color(1f, 0.92f, 0.7f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1700f, 70f));
        var contador = Txt("TextoContador", area, fuente, 36, FontStyle.Bold, Color.white, new Vector2(0f, 1f), new Vector2(200f, -250f), new Vector2(360f, 60f));

        // ---------- Vista de la cámara (arriba a la derecha) ----------
        var marco = Img("CamaraMarco", area, null, Vector2.zero, new Vector2(416f, 316f));
        marco.color = new Color(0.95f, 0.8f, 0.45f, 0.9f);
        var rtM = marco.rectTransform;
        rtM.anchorMin = rtM.anchorMax = new Vector2(1f, 1f);
        rtM.anchoredPosition = new Vector2(-232f, -380f);
        var rawGO = new GameObject("CamaraImagen", typeof(RectTransform), typeof(RawImage));
        var rtR = rawGO.GetComponent<RectTransform>();
        rtR.SetParent(rtM, false);
        rtR.anchorMin = rtR.anchorMax = rtR.pivot = new Vector2(0.5f, 0.5f);
        rtR.sizeDelta = new Vector2(400f, 300f);
        var raw = rawGO.GetComponent<RawImage>();
        raw.raycastTarget = false;
        raw.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        var sinSenal = Txt("TextoSinCamara", rtM, fuente, 26, FontStyle.Normal, new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 120f));
        sinSenal.text = "Esperando la cámara...";
        var etiqueta = Txt("TextoTuMano", rtM, fuente, 26, FontStyle.Bold, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, -24f), new Vector2(400f, 40f));
        etiqueta.text = "Tu mano";
        var prev = canvasGO.AddComponent<CamaraPreview>();
        prev.imagen = raw;
        prev.textoSinSenal = sinSenal;

        // el anillo y el cursor siempre por encima de todo
        anillo.transform.SetAsLastSibling();
        manoCursor.transform.SetAsLastSibling();
        cursor.transform.SetAsLastSibling();

        // ---------- MediaPipe ----------
        var mpGO = new GameObject("MediaPipe");
        var fm = mpGO.AddComponent<FingersMove>();
        fm.listenPort = 9050;
        fm.canvasRect = area;
        var hand = mpGO.AddComponent<HandInput>();
        hand.area = area;

        // ---------- Juego ----------
        var juegoGO = new GameObject("FogataGame");
        var juego = juegoGO.AddComponent<FogataGame>();
        juego.audioSource = juegoGO.AddComponent<AudioSource>();
        juego.audioSource.playOnAwake = false;
        juego.mano = hand;
        juego.area = area;
        juego.cursor = cursor.rectTransform;
        juego.cursorImagen = cursor;
        juego.manoImagen = manoCursor;
        juego.spriteManoAbierta = spManoAbierta;
        juego.spriteManoPinza = spManoPinza;
        juego.luzLinterna = linterna.rectTransform;
        juego.luzLinternaImagen = linterna;
        juego.ramitas = ramitas;
        juego.centroFogata = centro;
        juego.contenedorMonton = monton;
        juego.posicionesMonton = slots.ToArray();
        juego.marcaFogata = marca;
        juego.caja = caja.rectTransform;
        juego.zonaFrotar = zona;
        juego.fosforo = fosforo.rectTransform;
        juego.llamaFosforo = llamaFos.gameObject;
        juego.fuego = fuego;
        juego.resplandor = resplandor;
        juego.anilloProgreso = anillo;
        juego.contenedorEfectos = efectos;
        juego.spriteChispa = spCirculo;
        juego.textoInstruccion = instr;
        juego.textoAyuda = ayuda;
        juego.textoContador = contador;

        // ---------- Guardar ----------
        string rutaEscena = RUTA + "/Fogata.unity";
        EditorSceneManager.SaveScene(escena, rutaEscena);
        AgregarABuild(rutaEscena);
        Selection.activeGameObject = juegoGO;
        Debug.Log("[Fogata] Escena creada en " + rutaEscena + ". Inicia el script de Python y dale Play. Sin cámara puedes probar con el mouse (clic = pinza).");
    }

    // =====================================================================
    static Sprite S(string nombre)
    {
        string p = $"{RUTA}/Sprites/{nombre}.png";
        var ti = AssetImporter.GetAtPath(p) as TextureImporter;
        if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
        if (!sp) Debug.LogWarning("[Fogata] No se encontró el sprite " + p);
        return sp;
    }

    static RectTransform Contenedor(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        Estirar(rt);
        return rt;
    }

    static RectTransform Vacio(string nombre, Transform padre, Vector2 pos)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(10f, 10f);
        return rt;
    }

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image Img(string nombre, Transform padre, Sprite sp, Vector2 pos, Vector2 tam, float rot = 0f)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        rt.localEulerAngles = new Vector3(0f, 0f, rot);
        var img = go.GetComponent<Image>();
        img.sprite = sp;
        img.raycastTarget = false;
        return img;
    }

    static Image Llama(string nombre, Transform padre, Sprite sp, Vector2 pos, Vector2 tam, Color c)
    {
        var img = Img(nombre, padre, sp, pos, tam);
        img.color = c;
        img.rectTransform.pivot = new Vector2(0.5f, 0.1f);
        img.rectTransform.anchoredPosition = pos - new Vector2(0f, tam.y * 0.4f);
        img.gameObject.AddComponent<FlameFlicker>();
        return img;
    }

    static Text Txt(string nombre, Transform padre, Font f, int tam, FontStyle estilo, Color c, Vector2 ancla, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Text), typeof(Shadow));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.GetComponent<Text>();
        t.font = f;
        t.fontSize = tam;
        t.fontStyle = estilo;
        t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var sh = go.GetComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sh.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static Font Fuente()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    static void AgregarABuild(string ruta)
    {
        var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in lista) if (s.path == ruta) return;
        lista.Add(new EditorBuildSettingsScene(ruta, true));
        EditorBuildSettings.scenes = lista.ToArray();
    }
}
