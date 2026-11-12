#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>
/// SOLO EDITOR — ayuda para sacar capturas del juego para el Figma.
///   F12 = guardar captura 1920x1080 en "My project/Capturas/NN_Escena.png"
///         (oculta el recuadro de la cámara y el texto de estado de la cámara)
///   F8  = probar la ventana de insignia (niveles 2 y 5)
///   F6  = llenar los corazones   ·   F7 = camino de los troncos casi completo (95 %)
/// No se incluye en el ejecutable.
/// </summary>
public class CapturasEditor : MonoBehaviour
{
    static int numero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Crear()
    {
        var go = new GameObject("CapturasEditor");
        go.AddComponent<CapturasEditor>();
        DontDestroyOnLoad(go);
        string dir = Carpeta();
        if (Directory.Exists(dir)) numero = Directory.GetFiles(dir, "*.png").Length;
    }

    static string Carpeta() => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Capturas");

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            if (CamaraOverlay.Instancia != null) CamaraOverlay.Instancia.visible = false;
            if (PoseReceiver.Instancia != null) PoseReceiver.Instancia.mostrarEstado = false;
            StartCoroutine(Capturar());
        }
        if (Input.GetKeyDown(KeyCode.F6))
            foreach (var v in FindObjectsByType<Vidas>(FindObjectsSortMode.None))
            {
                typeof(Vidas).GetField("actuales", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(v, v.corazones.Length);
                typeof(Vidas).GetMethod("Refrescar", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(v, null);
            }
        if (Input.GetKeyDown(KeyCode.F8))   // probar la ventana de insignia del nivel actual
        {
            string esc = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (esc == "Recorrido") RecorridoAR.RecorridoGame.PremioExterno?.Invoke(() => Debug.Log("Insignia 2: continuar"));
            else if (esc == "Sombras") NivelesUnificados.GanarInsigniaNivel5();
            else if (esc == "Fogata") NivelesUnificados.GanarInsigniaNivel4(() => Debug.Log("Insignia 4: continuar"));
        }
        if (Input.GetKeyDown(KeyCode.F10))   // nivel 5: saltar a la partida
        {
            var ui = FindFirstObjectByType<SombrasInterfaz>();
            if (ui != null) ui.EmpezarJuego();
        }
        if (Input.GetKeyDown(KeyCode.F7))
            foreach (var c in FindObjectsByType<CaminoLena>(FindObjectsSortMode.None))
                typeof(CaminoLena).GetField("progreso", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c, 0.95f);
    }

    System.Collections.IEnumerator Capturar()
    {
        yield return null;   // un cuadro para que se oculte el recuadro
        Directory.CreateDirectory(Carpeta());
        numero++;
        string escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string ruta = Path.Combine(Carpeta(), numero.ToString("00") + "_" + escena + ".png");
        ScreenCapture.CaptureScreenshot(ruta);
        Debug.Log("Captura: " + ruta);
    }
}
#endif
