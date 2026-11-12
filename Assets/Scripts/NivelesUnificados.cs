using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// JUEGO UNIFICADO: reglas para los niveles de los compañeros que traen su propia interfaz.
///   Recorrido = Nivel 2 · Ruta de Energía (namespace RecorridoAR)
///   Fogata    = Nivel 4 · Pinzas de fuego (dedos en otro formato: UDP 9055, video UDP 9054)
///   Sombras   = Nivel 5 · Sombras en la carpa
///   Carpa*    = Nivel 1 · Arma tu carpa (9 escenas en Assets/Carpa; mano "x;y;puño" UDP 5052, video UDP 9056)
/// - La webcam la tiene SIEMPRE el emisor unificado de MediaPipe (dedos por UDP 9050), así que el
///   seguimiento de mano dentro de Unity del nivel 2 se apaga antes de que abra la cámara.
/// - Insignias iguales en todo el juego: los 5 círculos de InsigniasHUD abajo a la izquierda y la
///   ventana "¡Has ganado la insignia…!" (VentanaInsignia) al completar cada nivel.
/// - En esas escenas se ocultan el recuadro de cámara y la etiqueta de estado de este juego.
/// </summary>
public static class NivelesUnificados
{
    public static bool EsNivelExterno(string escena) => EsNivelCarpa(escena) || escena == "Recorrido" || escena == "Fogata" || escena == "Sombras";

    static readonly string[] EscenasCarpa = { "CarpaInicio", "ElegirCarpa", "Hojas", "AbrirMaleta", "Tela", "Tubos", "Ubicar", "Estacas", "CarpaFinal" };
    public static bool EsNivelCarpa(string escena) => System.Array.IndexOf(EscenasCarpa, escena) >= 0;

    // ---------- Los 5 niveles (para el menú de pausa: nivel anterior / siguiente) ----------
    public const string EscenaHistoria = "Historia";   // pantalla de inicio con la historia de Lucas
    public static readonly string[] EscenaDeNivel = { EscenaHistoria, "CarpaInicio", "Recorrido", "Inicio", "Fogata", "Sombras" };
    public static readonly string[] NombreDeNivel = { "", "Arma tu carpa", "Ruta de Energía", "Cruce del bosque", "Pinzas de fuego", "Sombras en la carpa" };

    /// <summary>1..5 según la escena; 0 = fuera de los niveles (Historia).</summary>
    public static int NivelDeEscena(string e)
    {
        if (EsNivelCarpa(e)) return 1;
        switch (e)
        {
            case "Recorrido": return 2;
            case "Inicio": case "SampleScene": case "CaminoLena": case "Lena": case "Pesca": return 3;
            case "Fogata": return 4;
            case "Sombras": return 5;
            default: return 0;
        }
    }

    /// <summary>Salta al comienzo del nivel n con las insignias de los niveles anteriores.</summary>
    public static void IrANivel(int n)
    {
        n = Mathf.Clamp(n, 1, 5);
        if (n == 3) EstadoJuego.Reiniciar();          // el nivel 3 empieza de cero (leña y comida)
        EstadoJuego.InsigniasGanadas = n - 1;
        Transicion.Cargar(EscenaDeNivel[n]);
    }

    /// <summary>Siempre true en el juego unido (lo usan los scripts de los compañeros para no abrir otra cámara).</summary>
    public static bool Activo => true;
    public const int PuertoDedosNivel4 = 9055;   // formato de send_fingers_udp.py de la fogata (5 puntas por mano, en espejo)
    public const int PuertoVideoNivel4 = 9054;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        RecorridoAR.RecorridoGame.PremioExterno = PremioNivel2;
    }

    // sceneLoaded llega después de Awake/OnEnable y antes de Start
    static void AlCargar(Scene s, LoadSceneMode modo)
    {
        // insignias que ya se tienen al entrar a cada nivel (los anteriores se dan por hechos)
        // la historia empieza una partida nueva; la portada del nivel 1 empieza sin insignias
        if (s.name == EscenaHistoria) { EstadoJuego.Reiniciar(); EstadoJuego.AvisoPausaMostrado = false; EstadoJuego.InsigniasGanadas = 0; }
        if (s.name == "CarpaInicio") EstadoJuego.InsigniasGanadas = 0;
        int minimo = EsNivelCarpa(s.name) ? 0 : s.name == "Recorrido" ? 1 : s.name == "Fogata" ? 3 : s.name == "Sombras" ? 4 : 2;
        if (EstadoJuego.InsigniasGanadas < minimo) EstadoJuego.InsigniasGanadas = minimo;
        if (InsigniasHUD.Instancia != null) InsigniasHUD.Instancia.Refrescar();

        if (EsNivelCarpa(s.name))
        {
            // su barra de indicaciones de abajo se corre a la derecha (y un poco más angosta)
            // para no tapar los círculos de las insignias; subirla tapaba el botón Jugar
            foreach (var raiz in s.GetRootGameObjects())
                foreach (var rt in raiz.GetComponentsInChildren<RectTransform>(true))
                    if (rt.name == "Indicacion" && rt.anchorMin.y == 0f && rt.anchorMax.y == 0f)
                    {
                        rt.sizeDelta = new Vector2(Mathf.Min(rt.sizeDelta.x, 1440f), rt.sizeDelta.y);
                        rt.anchoredPosition += new Vector2(190f, 0f);
                    }
        }

        if (s.name == "CarpaFinal")
        {
            // su botón "Volver al inicio" se cambia por la ventana de la insignia, que lleva al nivel 2
            var boton = Buscar(s, "BotonInicio");
            if (boton != null) boton.SetActive(false);
            new GameObject("InsigniaNivel1").AddComponent<EsperaInsigniaNivel1>();
        }
        else if (s.name == "Recorrido")
        {
            // seguimiento propio con la webcam: apagado (la cámara la tiene el emisor unificado)
            foreach (var seg in Object.FindObjectsByType<RecorridoAR.SeguimientoUnity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                seg.gameObject.SetActive(false);
            // su indicador de insignias de arriba a la izquierda: lo reemplazan los 5 círculos de abajo
            var arriba = Buscar(s, "Insignias_superiores");
            if (arriba != null) arriba.SetActive(false);
        }
        else if (s.name == "Fogata")
        {
            // su FingersMove escucha el 9050 (formato del nivel 5): se pasa al 9055, donde el emisor manda su formato
            foreach (var fm in Object.FindObjectsByType<FingersMove>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool estaba = fm.enabled;
                fm.enabled = false;
                fm.listenPort = PuertoDedosNivel4;
                fm.enabled = estaba;
            }
        }
        else if (s.name == "Sombras")
        {
            // el medidor de la fogata estaba abajo a la izquierda: sube para dejar sitio a las insignias
            var medidor = Buscar(s, "Medidor_Fogata");   // está en la pantalla de juego, inactiva al cargar
            if (medidor != null)
            {
                var rt = (RectTransform)medidor.transform;
                rt.anchoredPosition += new Vector2(0f, 100f);
                foreach (Transform hermano in rt.parent)
                {
                    var t = hermano.GetComponent<UnityEngine.UI.Text>();
                    if (t != null && t.text == "Fogata") ((RectTransform)hermano).anchoredPosition += new Vector2(0f, 100f);
                }
            }
        }
    }

    // GameObject.Find no ve objetos inactivos: se recorre la escena completa
    static GameObject Buscar(Scene s, string nombre)
    {
        foreach (var raiz in s.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        return null;
    }

    // Nivel 1: se deja ver "¡Carpa lista!" y el confeti unos segundos y luego sale la ventana
    class EsperaInsigniaNivel1 : MonoBehaviour
    {
        System.Collections.IEnumerator Start()
        {
            yield return new WaitForSeconds(3.5f);
            GanarInsigniaNivel1();
        }
    }

    public static void GanarInsigniaNivel1()
    {
        if (VentanaInsignia.Abierta) return;
        VentanaInsignia.Mostrar("Insignias/Insignia1", "¡Has ganado la insignia de la carpa!",
            "¡Muy bien! Esta es tu <b>primera insignia</b>.\nVe por las otras cuatro.", () => Transicion.Cargar("Recorrido"), 1);
    }

    // Nivel 2: la ventana del juego unido en lugar de su celebración; al cerrarla sigue su cierre (Continuar)
    static bool PremioNivel2(System.Action alTerminar)
    {
        VentanaInsignia.Mostrar("Insignias/Insignia2", "¡Has ganado la insignia de la energía!",
            "¡Muy bien! Esta es tu <b>segunda insignia</b>.\nVe por las otras tres.", alTerminar, 2);
        return true;
    }

    // Nivel 4: la llama FogataGame.TerminarLuego; al cerrar la ventana pasa al siguiente nivel
    public static void GanarInsigniaNivel4(System.Action alTerminar)
    {
        if (EstadoJuego.InsigniasGanadas >= 4) { alTerminar?.Invoke(); return; }
        VentanaInsignia.Mostrar("Insignias/Insignia4", "¡Has ganado la insignia del fuego!",
            "¡Muy bien! Esta es tu <b>cuarta insignia</b>.\nVe por la última.", alTerminar, 4);
    }

    // Nivel 5: la llama SombrasInterfaz.MostrarFinal
    public static void GanarInsigniaNivel5()
    {
        if (VentanaInsignia.Abierta || EstadoJuego.InsigniasGanadas >= 5) return;
        VentanaInsignia.Mostrar("Insignias/Insignia5", "¡Has ganado la insignia de las sombras!",
            "¡Muy bien! Esta es tu <b>quinta insignia</b>.\n¡Completaste la aventura!", null, 5);
    }
}
