using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Nivel 1 (Arma tu carpa) con la misma cara que los demás niveles:
///   · Botones 3D (Jugar, Listo, Volver al inicio) → píldora amarilla del nivel 3 (BotonAmarillo + brillos)
///     con texto Titan One verde oscuro.
///   · Paneles café de la interfaz → paneles crema redondeados; textos en Nunito Sans / Titan One.
/// Menú: Juego > Nivel 1 - Botones como los otros niveles. Se puede correr varias veces (no duplica nada).
/// </summary>
public static class ArreglarNivel1
{
    static readonly string[] Escenas =
    {
        "Assets/Carpa/Escenas/CarpaInicio.unity", "Assets/Carpa/Escenas/ElegirCarpa.unity", "Assets/Carpa/Escenas/Hojas.unity",
        "Assets/Carpa/Escenas/AbrirMaleta.unity", "Assets/Carpa/Escenas/Tela.unity", "Assets/Carpa/Escenas/Tubos.unity",
        "Assets/Carpa/Escenas/Ubicar.unity", "Assets/Carpa/Escenas/Estacas.unity", "Assets/Carpa/Escenas/CarpaFinal.unity",
    };
    const string CarpetaImg = "Assets/Imagenes/Inicio/";
    const float AltoSprite = 1.76f;   // BotonAmarillo: 512 x 176 px a 100 px/unidad
    const float TamTexto = 6.5f, MargenTexto = 1.4f;   // texto del botón con aire a los lados, como en el nivel 3

    static readonly Color TextoBoton = Hex("#001905");
    static readonly Color TextoOscuro = Hex("#18392B");
    static readonly Color TituloAzul = Hex("#2B5D72");
    static readonly Color Panel = new Color(1f, 0.988f, 0.965f, 0.95f);
    static readonly Color Cafe = new Color(0.5411765f, 0.33333334f, 0.1882353f);

    [MenuItem("Juego/Nivel 1 - Botones como los otros niveles", priority = 40)]
    public static void Aplicar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string escenaAntes = SceneManager.GetActiveScene().path;

        var boton = PrepararBoton();
        var brillo1 = AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaImg + "BrilloArriba.png");
        var brillo2 = AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaImg + "BrilloAbajo.png");
        var titan = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fuentes/TitanOne SDF.asset");
        var titanTtf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fuentes/TitanOne-Regular.ttf");
        var nunito = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fuentes/NunitoSans-Bold.ttf");
        var redondo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Generados/Redondo.png");
        if (boton == null || titan == null)
        {
            EditorUtility.DisplayDialog("Nivel 1", "No encontré Imagenes/Inicio/BotonAmarillo.png o Fuentes/TitanOne SDF.asset.", "OK");
            return;
        }

        int botones = 0, paneles = 0, textos = 0;
        foreach (var ruta in Escenas)
        {
            var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            bool cambio = false;

            // ---- Botones 3D (BotonMano con Cara + Borde) ----
            foreach (var root in escena.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "BotonMano") continue;
                var t = mb.transform;
                var cara = t.Find("Cara"); var borde = t.Find("Borde"); var texto = t.Find("Texto");
                var hecha = t.Find("Pildora");
                if (hecha != null)   // ya convertido: solo ajusta el tamaño del texto
                {
                    var tm = hecha.GetComponentInChildren<TextMeshPro>(true);
                    var srh = hecha.GetComponent<SpriteRenderer>();
                    if (tm != null && srh != null) { tm.fontSizeMax = TamTexto; tm.rectTransform.sizeDelta = new Vector2(srh.size.x - MargenTexto, 1.0f); botones++; cambio = true; }
                    continue;
                }
                if (cara == null || borde == null) continue;

                float alto = borde.localScale.y * 0.95f;
                float ancho = Mathf.Max(borde.localScale.x * 1.08f, alto * 2.9f);
                float s = alto / AltoSprite;
                string etiqueta = texto != null && texto.GetComponent<TextMesh>() != null ? texto.GetComponent<TextMesh>().text : "";

                cara.gameObject.SetActive(false);
                borde.gameObject.SetActive(false);
                if (texto != null) texto.gameObject.SetActive(false);

                var pildora = new GameObject("Pildora");
                pildora.transform.SetParent(t, false);
                pildora.transform.localPosition = new Vector3(0, 0, -0.05f);
                pildora.transform.localScale = Vector3.one * s;
                var sr = pildora.AddComponent<SpriteRenderer>();
                sr.sprite = boton;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(ancho / s, AltoSprite);
                sr.sortingOrder = 20;

                float mitad = sr.size.x / 2f;
                Brillo(pildora.transform, brillo1, new Vector3(-mitad + 0.58f, 0.48f, -0.002f));
                Brillo(pildora.transform, brillo2, new Vector3(mitad - 0.65f, -0.375f, -0.002f));

                var go = new GameObject("TextoBoton", typeof(RectTransform));
                go.transform.SetParent(pildora.transform, false);
                go.transform.localPosition = new Vector3(0, 0.08f, -0.004f);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.font = titan;
                tmp.text = etiqueta;
                tmp.color = TextoBoton;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 2f; tmp.fontSizeMax = TamTexto;
                tmp.rectTransform.sizeDelta = new Vector2(sr.size.x - MargenTexto, 1.0f);
                tmp.sortingOrder = 22;

                var col = t.GetComponent<BoxCollider>();
                if (col != null) col.size = new Vector3(Mathf.Max(col.size.x, ancho), Mathf.Max(col.size.y, alto), col.size.z);
                botones++; cambio = true;
            }

            // ---- Interfaz (Canvas): paneles café → crema redondeado; textos con las fuentes del juego ----
            foreach (var root in escena.GetRootGameObjects())
            {
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                {
                    var c = img.color;
                    if (img.sprite != null || Mathf.Abs(c.r - Cafe.r) > 0.02f || Mathf.Abs(c.g - Cafe.g) > 0.02f || Mathf.Abs(c.b - Cafe.b) > 0.02f) continue;
                    img.color = Panel;
                    if (redondo != null) { img.sprite = redondo; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.6f; }
                    foreach (var efecto in img.GetComponents<Shadow>()) efecto.effectColor = new Color(0f, 0f, 0f, 0.18f);
                    paneles++; cambio = true;
                }
                foreach (var tx in root.GetComponentsInChildren<Text>(true))
                {
                    if (tx.font != null && (tx.font == nunito || tx.font == titanTtf)) continue;
                    bool titulo = tx.fontSize >= 60;
                    bool contador = !titulo && tx.color.b < 0.5f;   // amarillos (contadores "x / 4")
                    tx.font = (titulo || contador) ? titanTtf : nunito;
                    tx.color = titulo ? TituloAzul : TextoOscuro;
                    if (titulo) tx.fontSize = Mathf.RoundToInt(tx.fontSize * 0.9f);
                    foreach (var efecto in tx.GetComponents<Shadow>()) efecto.enabled = false;
                    textos++; cambio = true;
                }
            }

            if (cambio) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
        }

        if (!string.IsNullOrEmpty(escenaAntes)) EditorSceneManager.OpenScene(escenaAntes);
        Debug.Log($"Nivel 1: {botones} botones, {paneles} paneles y {textos} textos con el estilo de los demás niveles.");
        EditorUtility.DisplayDialog("Nivel 1", $"Listo: {botones} botones, {paneles} paneles y {textos} textos actualizados.", "OK");
    }

    static void Brillo(Transform padre, Sprite sprite, Vector3 pos)
    {
        if (sprite == null) return;
        var go = new GameObject(sprite.name);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 21;
    }

    /// BotonAmarillo con bordes para estirarlo a lo ancho sin deformar las puntas redondas.
    static Sprite PrepararBoton()
    {
        string ruta = CarpetaImg + "BotonAmarillo.png";
        var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (imp == null) return null;
        var borde = new Vector4(90, 0, 90, 0);
        var ajustes = new TextureImporterSettings();
        imp.ReadTextureSettings(ajustes);
        if (imp.spriteBorder != borde || ajustes.spriteMeshType != SpriteMeshType.FullRect)
        {
            imp.spriteBorder = borde;
            ajustes.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(ajustes);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
