using UnityEditor;
using UnityEngine;

/// <summary>
/// JUEGO UNIFICADO · Nivel 1 (Arma tu carpa) se hizo en Built-in con el shader Standard, que en URP sale rosado.
/// Al abrir el editor pasa sus materiales de Assets/Carpa/Materiales a "Universal Render Pipeline/Lit"
/// conservando color, textura, brillo y metal. Solo toca los que siguen en Standard (se puede repetir sin daño).
/// </summary>
[InitializeOnLoad]
static class ConvertirCarpaURP
{
    static ConvertirCarpaURP() { EditorApplication.delayCall += () => { Convertir(); CrearHistoria(); OrdenEscenas(); }; }

    // ---- Escena "Historia" (pantalla de inicio con la historia de Lucas): se crea si no existe
    const string RutaHistoria = "Assets/Scenes/Historia.unity";

    [MenuItem("Juego/Crear escena Historia (si falta)")]
    static void CrearHistoria()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaHistoria) != null) return;
        var escena = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.063f, 0.122f, 0.239f);
        c.orthographic = true;
        cam.AddComponent<AudioListener>();
        var h = new GameObject("Historia");
        h.AddComponent<PantallaHistoria>();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cam, escena);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(h, escena);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(escena, RutaHistoria);
        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(escena, true);
        Debug.Log("[Juego unificado] Escena Historia creada.");
    }

    // ---- Orden en Build Settings: Historia, las 9 escenas de la carpa (nivel 1) y luego el resto
    static readonly string[] Escenas = { "CarpaInicio", "ElegirCarpa", "Hojas", "AbrirMaleta", "Tela", "Tubos", "Ubicar", "Estacas", "CarpaFinal" };
    static void OrdenEscenas()
    {
        var deseadas = new System.Collections.Generic.List<string>();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaHistoria) != null) deseadas.Add(RutaHistoria);
        foreach (var n in Escenas)
        {
            string ruta = "Assets/Carpa/Escenas/" + n + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ruta) != null) deseadas.Add(ruta);
        }
        var actuales = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool enOrden = actuales.Count >= deseadas.Count;
        for (int i = 0; enOrden && i < deseadas.Count; i++) enOrden = actuales[i].path == deseadas[i] && actuales[i].enabled;
        if (enOrden) return;
        actuales.RemoveAll(e => deseadas.Contains(e.path));
        var nuevas = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var r in deseadas) nuevas.Add(new EditorBuildSettingsScene(r, true));
        nuevas.AddRange(actuales);
        EditorBuildSettings.scenes = nuevas.ToArray();
        Debug.Log("[Juego unificado] Build Settings: Historia y nivel 1 al inicio.");
    }

    [MenuItem("Juego/Nivel 1 · Convertir materiales a URP")]
    static void Convertir()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Carpa/Materiales" }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null || mat.shader.name != "Standard") continue;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Vector2 escala = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 desfase = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
            float brillo = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            float metal = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            bool emite = mat.IsKeywordEnabled("_EMISSION");
            Color emision = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            mat.shader = lit;
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", escala);
            mat.SetTextureOffset("_BaseMap", desfase);
            mat.SetFloat("_Smoothness", brillo);
            mat.SetFloat("_Metallic", metal);
            if (emite)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emision);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            n++;
        }
        if (n > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[Juego unificado] Nivel 1: " + n + " materiales pasados a URP Lit.");
        }
    }
}
