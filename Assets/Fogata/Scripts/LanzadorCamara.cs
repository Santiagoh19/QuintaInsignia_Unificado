using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Abre solo el Python de MediaPipe (send_fingers_udp.py) al empezar el juego y lo cierra al salir,
/// para no tener que abrirlo a mano. No cambia el protocolo UDP ni FingersMove.
/// Si ya hay uno corriendo (abierto a mano o con Jugar.bat), lo usa y no abre otro.
/// La carpeta "mediaPipe\Hand" se busca subiendo desde la carpeta del juego y en Escritorio\MLtaller.
/// Argumentos opcionales del .exe:  -camara N   -hand "C:\ruta\mediaPipe\Hand"   -sinCamara
/// </summary>
public class LanzadorCamara : MonoBehaviour
{
    static LanzadorCamara _instancia;

    Process _proceso;
    bool _loAbriYo;
    string _ultimaLinea = "";
    readonly object _lockLinea = new object();
    Text _aviso;
    float _tAviso;

    public static bool Encendiendo => _instancia && _instancia._loAbriYo && _instancia._proceso != null && !_instancia._proceso.HasExited;

    float _tInicio;
    /// <summary>Segundos desde que el juego abrió el Python (-1 si no lo abrió el juego o ya se cerró).</summary>
    public static float SegundosEncendiendo => Encendiendo ? Time.unscaledTime - _instancia._tInicio : -1f;

    /// <summary>Abre el Python si hace falta (solo una vez por partida, aunque se recargue la escena).</summary>
    public static void Asegurar(int camara, string rutaHand)
    {
        if (_instancia) return;
        if (TieneArgumento("-sinCamara")) return;
        // JUEGO UNIFICADO: la cámara la abre el emisor unificado (PoseReceiver); no se lanza otro Python
        if (NivelesUnificados.Activo) return;
        var go = new GameObject("LanzadorCamara");
        DontDestroyOnLoad(go);
        _instancia = go.AddComponent<LanzadorCamara>();
        _instancia.Abrir(camara, rutaHand);
    }

    void Abrir(int camara, string rutaHand)
    {
        string argCam = ValorArgumento("-camara");
        if (argCam != null && int.TryParse(argCam, out int c)) camara = c;
        string argHand = ValorArgumento("-hand");
        if (!string.IsNullOrEmpty(argHand)) rutaHand = argHand;

        string hand = BuscarHand(rutaHand);
        if (hand == null)
        {
            Debug.LogWarning("[Fogata] No encontré la carpeta mediaPipe\\Hand con el Python. Ábrelo a mano o usa -hand \"ruta\".");
            MostrarAviso("No encontré la carpeta de la cámara (mediaPipe\\Hand).\nÁbrela a mano o pon la carpeta Fogata junto a mediaPipe.", 12f);
            return;
        }
        string scripts = Path.Combine(hand, "trackingMano", "Scripts");
        string python = Path.Combine(scripts, "python.exe");
        string script = Path.Combine(scripts, "send_fingers_udp.py");

        if (YaEstaCorriendo(hand))
        {
            Debug.Log("[Fogata] El Python de la cámara ya estaba abierto; uso ese.");
            return;
        }

        try
        {
            var info = new ProcessStartInfo(python, $"\"{script}\" --camera {camara} --sin-ventana")
            {
                WorkingDirectory = scripts,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            info.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            // El Python (si es la versión nueva) se cierra solo si el juego se cierra de golpe.
            info.EnvironmentVariables["FOGATA_PID_PADRE"] = Process.GetCurrentProcess().Id.ToString();

            _proceso = new Process { StartInfo = info };
            _proceso.OutputDataReceived += (s, e) => Linea(e.Data, false);
            _proceso.ErrorDataReceived += (s, e) => Linea(e.Data, true);
            _proceso.Start();
            _proceso.BeginOutputReadLine();
            _proceso.BeginErrorReadLine();
            _loAbriYo = true;
            _tInicio = Time.unscaledTime;
            Debug.Log($"[Fogata] Encendiendo la cámara {camara} (MediaPipe en {hand})");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Fogata] No pude abrir el Python de la cámara: " + ex.Message);
            MostrarAviso("No pude encender la cámara.\nEjecuta Arreglar_Python.bat de la carpeta Hand.", 12f);
        }
    }

    void Linea(string texto, bool error)
    {
        if (string.IsNullOrEmpty(texto)) return;
        if (texto.StartsWith("MANO:")) return; // se imprime en cada cuadro: no llenar la consola
        lock (_lockLinea) _ultimaLinea = texto;
        // Los avisos de TensorFlow/absl salen por stderr pero no son errores
        Debug.Log("[Cámara] " + texto);
    }

    void Update()
    {
        if (_loAbriYo && _proceso != null && _proceso.HasExited)
        {
            _loAbriYo = false;
            string ultima; lock (_lockLinea) ultima = _ultimaLinea;
            Debug.LogWarning($"[Fogata] El Python de la cámara se cerró (código {_proceso.ExitCode}): {ultima}");
            string motivo = ultima.Contains("No module") || ultima.Contains("No Python")
                ? "El Python no funciona en esta computadora:\nejecuta Arreglar_Python.bat de la carpeta Hand."
                : ultima.Contains("No se pudo abrir")
                    ? "No se pudo abrir la cámara.\nRevisa que esté conectada o prueba otra con -camara 1."
                    : "La cámara se apagó.\n" + Recortar(ultima, 90);
            MostrarAviso(motivo, 15f);
        }
        if (_aviso && _tAviso > 0f)
        {
            _tAviso -= Time.unscaledDeltaTime;
            if (_tAviso <= 0f) _aviso.transform.parent.gameObject.SetActive(false);
        }
    }

    void OnApplicationQuit() => Cerrar();
    void OnDestroy() => Cerrar();

    void Cerrar()
    {
        if (!_loAbriYo || _proceso == null) return;
        _loAbriYo = false;
        try
        {
            if (!_proceso.HasExited)
            {
                // python.exe del venv es un lanzador que abre otro python: hay que cerrar el árbol entero
                var kill = new ProcessStartInfo("taskkill", $"/PID {_proceso.Id} /T /F")
                { UseShellExecute = false, CreateNoWindow = true };
                Process.Start(kill)?.WaitForExit(3000);
                if (!_proceso.HasExited) _proceso.Kill();
            }
        }
        catch (Exception ex) { Debug.LogWarning("[Fogata] No pude cerrar el Python de la cámara: " + ex.Message); }
    }

    // ===================================================================== búsqueda

    static string BuscarHand(string preferida)
    {
        if (!string.IsNullOrEmpty(preferida) && EsHand(preferida)) return preferida;

        // Subir desde la carpeta del juego (en el editor, desde la carpeta del proyecto)
        var dir = new DirectoryInfo(Path.GetDirectoryName(Application.dataPath) ?? ".");
        for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            foreach (var sub in new[] { @"mediaPipe\Hand", @"MLtaller\mediaPipe\Hand", @"MLtaller\MLtaller\mediaPipe\Hand", "Hand" })
            {
                string p = Path.Combine(dir.FullName, sub);
                if (EsHand(p)) return p;
            }
        }
        string escritorio = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        foreach (var sub in new[] { @"MLtaller\mediaPipe\Hand", @"MLtaller\MLtaller\mediaPipe\Hand", @"mediaPipe\Hand" })
        {
            string p = Path.Combine(escritorio, sub);
            if (EsHand(p)) return p;
        }
        return null;
    }

    static bool EsHand(string p) =>
        File.Exists(Path.Combine(p, "trackingMano", "Scripts", "python.exe")) &&
        File.Exists(Path.Combine(p, "trackingMano", "Scripts", "send_fingers_udp.py"));

    static bool YaEstaCorriendo(string hand)
    {
        string carpeta = Path.GetFullPath(hand).TrimEnd('\\').ToLowerInvariant();
        foreach (var p in Process.GetProcessesByName("python"))
        {
            try
            {
                string f = p.MainModule?.FileName;
                if (f != null && Path.GetFullPath(f).ToLowerInvariant().StartsWith(carpeta)) return true;
            }
            catch { /* proceso de otro usuario o que ya se cerró */ }
        }
        return false;
    }

    static bool TieneArgumento(string nombre)
    {
        foreach (var a in Environment.GetCommandLineArgs())
            if (string.Equals(a, nombre, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string ValorArgumento(string nombre)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], nombre, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    static string Recortar(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";

    // ===================================================================== aviso en pantalla

    void MostrarAviso(string texto, float segundos)
    {
        if (!_aviso)
        {
            var canvasGo = new GameObject("AvisoCamara", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondo.transform.SetParent(canvasGo.transform, false);
            var rf = (RectTransform)fondo.transform;
            rf.anchorMin = rf.anchorMax = new Vector2(0.5f, 0f);
            rf.pivot = new Vector2(0.5f, 0f);
            rf.anchoredPosition = new Vector2(0, 40);
            rf.sizeDelta = new Vector2(1100, 130);
            var img = fondo.GetComponent<Image>();
            img.color = new Color32(0x2B, 0x24, 0x20, 230);
            img.raycastTarget = false;

            var tGo = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            tGo.transform.SetParent(fondo.transform, false);
            var rt = (RectTransform)tGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(24, 12); rt.offsetMax = new Vector2(-24, -12);
            _aviso = tGo.GetComponent<Text>();
            _aviso.font = Font.CreateDynamicFontFromOSFont("Arial Rounded MT Bold", 34)
                          ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _aviso.fontSize = 34;
            _aviso.alignment = TextAnchor.MiddleCenter;
            _aviso.color = new Color32(0xF4, 0xEC, 0xDC, 255);
            _aviso.raycastTarget = false;
        }
        _aviso.text = texto;
        _aviso.transform.parent.gameObject.SetActive(true);
        _tAviso = segundos;
    }
}
