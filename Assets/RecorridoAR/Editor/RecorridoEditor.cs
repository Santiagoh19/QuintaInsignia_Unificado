using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RecorridoAR.Editor
{
    [InitializeOnLoad]
    static class RecorridoEditor
    {
        const string ScenePath="Assets/Scenes/Recorrido.unity";
        static RecorridoEditor() { EditorApplication.delayCall+=FirstOpen; }
        static void FirstOpen()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall+=FirstOpen; return; }
            if(SessionState.GetBool("Recorrido.InitialOpen",false))return;
            SessionState.SetBool("Recorrido.InitialOpen",true);
            var scene=SceneManager.GetActiveScene();
            // Only replace Unity's untouched blank startup scene; never discard user work.
            if(string.IsNullOrEmpty(scene.path) && !scene.isDirty) Open();
        }
        [MenuItem("Ruta de Energía/Abrir juego")]
        static void Open()
        {
            if(EditorApplication.isPlaying) return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            EditorApplication.delayCall+=Frame;
        }
        [MenuItem("Ruta de Energía/Ver diseno en 2D")]
        static void Frame()
        {
            var board=GameObject.Find("Area_de_juego");
            if(!board)return;
            Canvas.ForceUpdateCanvases();
            Selection.activeGameObject=board;
            var view=SceneView.lastActiveSceneView;
            if(!view)view=EditorWindow.GetWindow<SceneView>();
            view.in2DMode=true;
            view.FrameSelected();
            view.Repaint();
        }
        [MenuItem("Ruta de Energía/Mostrar diagnostico")]
        static void ToggleDiagnostics()
        {
            var controller=Object.FindFirstObjectByType<RecorridoGame>();
            if(!controller)return;
            Undo.RecordObject(controller,"Alternar diagnostico");
            controller.mostrarDiagnostico=!controller.mostrarDiagnostico;
            EditorUtility.SetDirty(controller);
        }
    }
}
