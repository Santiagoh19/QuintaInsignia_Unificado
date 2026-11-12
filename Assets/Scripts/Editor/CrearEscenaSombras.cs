using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Crea la escena nueva de las sombras con un clic: menu de arriba "Sombras" -> "Crear escena de sombras".
// Arma una escena vacia, le pone la camara, el MediaPipe del profe (el prefab MediaPipeMano)
// el objeto SombrasJuego, que construye el campamento y las manos, y la Interfaz (inicio, instrucciones y HUD).
// La escena del muro (SampleScene) no se toca.
public static class CrearEscenaSombras
{
    const string RUTA_ESCENA = "Assets/Scenes/Sombras.unity";
    const string RUTA_MEDIAPIPE = "Assets/MediaPipeMano.prefab";


    [MenuItem("Sombras/Crear escena de sombras")]
    static void Crear()
    {
        // si ya existe, se pregunta antes de reemplazarla
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RUTA_ESCENA) != null)
        {
            bool reemplazar = EditorUtility.DisplayDialog("Sombras",
                "La escena Sombras ya existe. ¿La reemplazo por una nueva?\n(La escena del muro no se toca.)",
                "Reemplazar", "Cancelar");
            if (!reemplazar)
            {
                return;
            }
        }

        // si la escena abierta tiene cambios sin guardar, Unity pregunta si guardarlos
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- la camara ----
        GameObject camara = new GameObject("Main Camera");
        camara.tag = "MainCamera";
        camara.AddComponent<Camera>();
        camara.AddComponent<AudioListener>();

        // ---- MediaPipe: el mismo prefab del muro (trae el FingersMove del profe) ----
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_MEDIAPIPE);
        if (prefab != null)
        {
            GameObject mediaPipe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // el prefab esta guardado APAGADO (la escena del muro lo prende por su cuenta).
            // Apagado, FingersMove no abre el puerto 9050 y no llegan los dedos
            mediaPipe.SetActive(true);

            // HandStateLogger escribe en la consola en CADA cuadro: llena el log y pone lento el editor.
            // Aqui se apaga solo en esta escena (el prefab y el muro no cambian)
            HandStateLogger[] registradores = mediaPipe.GetComponentsInChildren<HandStateLogger>(true);
            for (int i = 0; i < registradores.Length; i++)
            {
                registradores[i].enabled = false;
            }

            // los circulos de los dedos tapan la carpa: se apaga su Canvas.
            // Si quieres verlos para revisar, vuelve a activar el componente Canvas
            Canvas[] canvases = mediaPipe.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].enabled = false;
            }
        }
        else
        {
            Debug.LogWarning("[Sombras] No encontre " + RUTA_MEDIAPIPE + ". Arrastra el prefab de MediaPipe a la escena a mano.");
        }

        // ---- el juego: construye la carpa, la fogata y las manos ----
        GameObject juego = new GameObject("SombrasJuego");
        SombrasJuego sombras = juego.AddComponent<SombrasJuego>();
        sombras.ConstruirTodo();

        // ---- la interfaz: inicio, instrucciones y HUD ----
        GameObject interfaz = new GameObject("Interfaz");
        SombrasInterfaz pantallas = interfaz.AddComponent<SombrasInterfaz>();
        pantallas.juego = sombras;
        pantallas.ConstruirInterfaz();

        EditorSceneManager.SaveScene(escena, RUTA_ESCENA);
        Debug.Log("[Sombras] Escena creada en " + RUTA_ESCENA + ". Prende el emisor de Python y dale Play.");
    }
}
