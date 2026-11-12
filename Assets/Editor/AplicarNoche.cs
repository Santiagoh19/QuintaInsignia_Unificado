using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pasa la escena Inicio a la noche sin regenerarla (respeta los cambios hechos a mano):
/// fondo azul noche, capa de estrellas y textos claros. Se ejecuta sola una vez;
/// también desde el menú Juego > Aplicar noche a Inicio.
/// Los fondos de las demás escenas ya son imágenes de noche (Herramientas/aplicar_noche.py)
/// y la oscuridad con linterna la pone Linterna.cs al jugar.
/// </summary>
[InitializeOnLoad]
public static class AplicarNoche
{
    const string RutaInicio = "Assets/Scenes/Inicio.unity";
    const string RutaEstrellas = "Assets/Imagenes/Inicio/Estrellas.png";
    const string Clave = "QuintaInsignia_NocheInicio_v2";

    public static readonly Color AzulNoche = Hex("#101F3D");
    public static readonly Color AmarilloAventura = Hex("#F4C95D");
    public static readonly Color TextoClaro = Hex("#E6ECF5");

    static AplicarNoche()
    {
        if (EditorPrefs.GetBool(Clave + Application.dataPath, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Aplicar(false)) EditorPrefs.SetBool(Clave + Application.dataPath, true);
        };
    }

    [MenuItem("Juego/Aplicar noche a Inicio")]
    static void Menu() => Aplicar(true);

    static bool Aplicar(bool preguntar)
    {
        if (!System.IO.File.Exists(RutaInicio)) return false;

        // la capa de estrellas debe importarse como sprite
        var imp = AssetImporter.GetAtPath(RutaEstrellas) as TextureImporter;
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;   // un solo sprite, no recortado en estrellas sueltas
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }

        var actual = EditorSceneManager.GetActiveScene();
        bool eraInicio = actual.path == RutaInicio;
        if (!eraInicio)
        {
            if (actual.isDirty)
            {
                if (!preguntar) return false;           // no tocar trabajo sin guardar
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            }
        }
        string volverA = eraInicio ? null : actual.path;
        var escena = eraInicio ? actual : EditorSceneManager.OpenScene(RutaInicio, OpenSceneMode.Single);

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            cam.backgroundColor = AzulNoche;

        Transform canvas = null;
        foreach (var root in escena.GetRootGameObjects())
            if (root.GetComponent<Canvas>() != null) { canvas = root.transform; break; }

        if (canvas != null)
        {
            var fondo = canvas.Find("Fondo");
            if (fondo != null && fondo.TryGetComponent<Image>(out var img)) img.color = AzulNoche;

            var estrellasViejas = canvas.Find("Estrellas");
            if (estrellasViejas != null && estrellasViejas.TryGetComponent<Image>(out var ev))
                ev.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RutaEstrellas);
            if (estrellasViejas == null)
            {
                var go = new GameObject("Estrellas", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = canvas.gameObject.layer;
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(canvas, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                rt.SetSiblingIndex(fondo != null ? fondo.GetSiblingIndex() + 1 : 0);
                var est = go.GetComponent<Image>();
                est.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RutaEstrellas);
                est.raycastTarget = false;
            }

            foreach (var t in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.name == "Subtitulo") t.color = AmarilloAventura;
                else if (t.name == "Descripcion") t.color = TextoClaro;
            }
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (volverA != null && !string.IsNullOrEmpty(volverA)) EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);
        Debug.Log("Noche aplicada a la escena Inicio.");
        return true;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
