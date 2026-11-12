using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Exporta "Quinta Insignia" para Windows con la cámara (MediaPipe) incluida, para llevarlo en USB / OneDrive.
///
/// Menú Juego > Exportar:
///   1. Preparar la cámara  → convierte MediaPipe/send_pose_udp.py en un .exe independiente con PyInstaller
///                             (usa el Python del entorno trackingMano; no hace falta Python en el otro PC).
///   2. Exportar el juego   → compila el juego en Builds/QuintaInsignia y copia la cámara dentro.
///   Exportar todo          → 1 y luego 2.
///
/// En el juego exportado, PoseReceiver abre la cámara sola al iniciar (si no hay otra corriendo) y la cierra al salir.
/// </summary>
public static class ExportarJuego
{
    // Python con mediapipe ya instalado (entorno de la guía de MediaPipe)
    const string PythonVenv = @"C:\xampp\htdocs\MLtaller\mediaPipe\Hand\trackingMano\Scripts\python.exe";
    const string NombreCamara = "QuintaInsigniaCamara";
    const string NombreJuego = "QuintaInsignia";

    static string Raiz => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string CarpetaMediaPipe => Path.Combine(Raiz, "MediaPipe");
    static string CarpetaCamara => Path.Combine(CarpetaMediaPipe, "dist", NombreCamara);
    static string CarpetaBuild => Path.Combine(Raiz, "Builds", NombreJuego);

    static Process proceso;
    static System.Action alTerminar;

    [MenuItem("Juego/Exportar/1. Preparar la cámara (exe de MediaPipe)", priority = 1)]
    static void MenuCamara() => EmpaquetarCamara(null);

    [MenuItem("Juego/Exportar/2. Exportar el juego para Windows", priority = 2)]
    static void MenuJuego() => CompilarJuego();

    [MenuItem("Juego/Exportar/Exportar todo (cámara + juego)", priority = 20)]
    static void MenuTodo() => EmpaquetarCamara(CompilarJuego);

    // ------------------------------------------------------------------ Cámara

    static void EmpaquetarCamara(System.Action despues)
    {
        if (proceso != null && !proceso.HasExited)
        {
            EditorUtility.DisplayDialog("Exportar", "Ya se está preparando la cámara. Espera a que termine la ventana negra.", "OK");
            return;
        }
        if (!File.Exists(PythonVenv))
        {
            EditorUtility.DisplayDialog("Exportar",
                "No encontré el Python con MediaPipe en:\n" + PythonVenv + "\n\nRevisa la ruta en Assets/Editor/ExportarJuego.cs.", "OK");
            return;
        }

        // pip instala PyInstaller (solo la primera vez) y PyInstaller arma la carpeta con el .exe
        string py = "\"" + PythonVenv + "\"";
        string comandos =
            "echo Preparando la camara de Quinta Insignia (puede tardar unos minutos)... && " +
            py + " -m pip install --disable-pip-version-check -q pyinstaller && " +
            py + " empaquetar_camara.py" +
            " || (echo. & echo ERROR: revisa los mensajes de arriba. & pause & exit /b 1)";

        var info = new ProcessStartInfo("cmd.exe", "/s /c \"" + comandos + "\"")
        {
            WorkingDirectory = CarpetaMediaPipe,
            UseShellExecute = true,          // se ve la ventana negra con el progreso
        };
        proceso = Process.Start(info);
        alTerminar = despues;
        Debug.Log("Exportar: preparando la cámara con PyInstaller (ventana negra)...");
        EditorApplication.update += Vigilar;
    }

    static void Vigilar()
    {
        if (proceso == null) { EditorApplication.update -= Vigilar; return; }
        if (!proceso.HasExited)
        {
            EditorUtility.DisplayProgressBar("Exportar", "Preparando la cámara (mira la ventana negra)...", 0.5f);
            return;
        }
        EditorApplication.update -= Vigilar;
        EditorUtility.ClearProgressBar();
        int codigo = proceso.ExitCode;
        proceso = null;

        string exe = Path.Combine(CarpetaCamara, NombreCamara + ".exe");
        if (codigo != 0 || !File.Exists(exe))
        {
            Debug.LogError("Exportar: no se pudo preparar la cámara (código " + codigo + ").");
            EditorUtility.DisplayDialog("Exportar", "No se pudo preparar la cámara. Revisa la ventana negra / la consola.", "OK");
            alTerminar = null;
            return;
        }
        Debug.Log("Exportar: cámara lista en " + CarpetaCamara);
        var siguiente = alTerminar;
        alTerminar = null;
        if (siguiente != null) siguiente();
        else EditorUtility.RevealInFinder(exe);
    }

    // ------------------------------------------------------------------ Juego

    static void CompilarJuego()
    {
        string exeCamara = Path.Combine(CarpetaCamara, NombreCamara + ".exe");
        if (!File.Exists(exeCamara) &&
            !EditorUtility.DisplayDialog("Exportar",
                "Todavía no está preparada la cámara (paso 1). El juego funcionará solo con teclado.\n\n¿Exportar igual?",
                "Exportar igual", "Cancelar"))
            return;

        var escenas = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (escenas.Length == 0) { EditorUtility.DisplayDialog("Exportar", "No hay escenas en Build Settings.", "OK"); return; }

        PlayerSettings.productName = "Quinta Insignia";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.runInBackground = true;          // sigue corriendo aunque la cámara tome el foco

        if (Directory.Exists(CarpetaBuild)) Directory.Delete(CarpetaBuild, true);
        string exeJuego = Path.Combine(CarpetaBuild, NombreJuego + ".exe");
        var reporte = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = exeJuego,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        if (reporte.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Exportar", "La compilación del juego falló. Revisa la consola.", "OK");
            return;
        }

        // la cámara va dentro de los datos del juego: QuintaInsignia_Data/StreamingAssets/Camara
        if (File.Exists(exeCamara))
        {
            string destino = Path.Combine(CarpetaBuild, NombreJuego + "_Data", "StreamingAssets", "Camara");
            CopiarCarpeta(CarpetaCamara, destino);
        }

        File.WriteAllText(Path.Combine(CarpetaBuild, "LEEME.txt"),
            "QUINTA INSIGNIA - prototipo\r\n\r\n" +
            "ANTES DE DESCOMPRIMIR (si lo recibiste por internet / WhatsApp / Drive / correo):\r\n" +
            "   clic derecho en QuintaInsignia.zip > Propiedades > marca 'Desbloquear' > Aceptar.\r\n" +
            "   Luego clic derecho > Extraer todo. Así Windows no bloquea el juego ni la cámara.\r\n\r\n" +
            "1. No muevas archivos de adentro de la carpeta.\r\n" +
            "2. Abre QuintaInsignia.exe. La cámara se enciende sola a los pocos segundos\r\n" +
            "   (abajo en el centro dice el estado de la cámara).\r\n" +
            "3. Si Windows muestra 'Windows protegió su PC': Más información > Ejecutar de todas formas.\r\n" +
            "4. Si dice 'Cámara bloqueada o en uso':\r\n" +
            "   - cierra Zoom / Teams / Meet u otra app que use la cámara;\r\n" +
            "   - pulsa F3 y activa 'Acceso a la cámara' y 'Permitir que las aplicaciones de escritorio\r\n" +
            "     accedan a la cámara';\r\n" +
            "   - pulsa F2 para reintentar (abre la ventana de la cámara; C = centrar, R = centro).\r\n" +
            "5. Sin cámara se puede jugar con el teclado: flechas, Enter, B (lanzar), C (recoger).\r\n" +
            "6. Salir: Alt + F4.\r\n\r\n" +
            "Requisitos: Windows 10/11 de 64 bits con cámara web. No hace falta instalar Python ni Unity.\r\n");

        // la carpeta de Burst es solo para depurar: no se envía
        foreach (var d in Directory.GetDirectories(CarpetaBuild, "*_DoNotShip"))
            try { Directory.Delete(d, true); } catch { }

        // zip listo para enviar: Builds/QuintaInsignia.zip
        string zip = Path.Combine(Raiz, "Builds", NombreJuego + ".zip");
        try
        {
            if (File.Exists(zip)) File.Delete(zip);
            EditorUtility.DisplayProgressBar("Exportar", "Comprimiendo " + NombreJuego + ".zip...", 0.9f);
            var ps = Process.Start(new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Compress-Archive -Path '" + CarpetaBuild + "' -DestinationPath '" + zip + "' -Force\"")
            { UseShellExecute = false, CreateNoWindow = true });
            ps.WaitForExit();
        }
        catch (System.Exception e) { Debug.LogWarning("Exportar: no se pudo crear el zip (" + e.Message + ")"); }
        finally { EditorUtility.ClearProgressBar(); }

        Debug.Log("Exportar: juego listo en " + CarpetaBuild);
        EditorUtility.RevealInFinder(File.Exists(zip) ? zip : exeJuego);
    }

    static void CopiarCarpeta(string origen, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (var dir in Directory.GetDirectories(origen, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(origen, destino));
        foreach (var archivo in Directory.GetFiles(origen, "*", SearchOption.AllDirectories))
            File.Copy(archivo, archivo.Replace(origen, destino), true);
    }
}
