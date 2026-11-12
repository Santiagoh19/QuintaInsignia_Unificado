using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RecorridoAR
{
    // Session progress shared by every Quinta Insignia scene. It survives scene changes and
    // resets when the game closes (no save files), like the batteries of Ruta de Energía.
    public static class AventuraQuintaInsignia
    {
        public const int TotalInsignias=5;
        static int completados;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){ completados=0; }
        public static bool Completado(int nivel)=>nivel>=1 && nivel<=30 && (completados & (1<<nivel))!=0;
        public static void Completar(int nivel){ if(nivel>=1 && nivel<=30)completados|=1<<nivel; }
        public static int Insignias { get { int n=0; for(int i=1;i<=30;i++)if(Completado(i))n++; return n; } }
        public static void Reiniciar(){ completados=0; }
    }

    // Links this minigame to the rest of the adventure. Empty or unknown scene names hide the
    // corresponding button: Continuar never appears unless the scene really can be loaded.
    public sealed class AventuraEnlaces : MonoBehaviour
    {
        [Tooltip("Número de este minijuego dentro de Quinta Insignia.")]
        [Min(1)] public int nivel=2;
        [Tooltip("Nombre exacto de la escena del siguiente minijuego. Debe estar en File > Build Profiles > Scene List. Vacío: no se muestra Continuar.")]
        public string escenaSiguiente="";
        [Tooltip("Escena con la que empieza toda la aventura (para Partida nueva). Vacío: no se muestra Partida nueva.")]
        public string escenaInicioAventura="";
        [Tooltip("Escena que abre Finalizar juego. Vacío: muestra el cierre del nivel (aviso del nivel 3) en esta escena.")]
        public string escenaFinalizar="";
        [Tooltip("Pantalla que se funde al entrar y al salir de la escena.")]
        public CanvasGroup fundidoPantalla;
        [Min(.05f)] public float duracionFundido=.7f;
        bool loading;

        public static bool Disponible(string scene)=>!string.IsNullOrWhiteSpace(scene) && Application.CanStreamedLevelBeLoaded(scene.Trim());
        public bool SiguienteDisponible => Disponible(escenaSiguiente);
        public bool PartidaNuevaDisponible => Disponible(escenaInicioAventura);
        public bool Cargando => loading;

        void Start()
        {
            if(fundidoPantalla){ fundidoPantalla.blocksRaycasts=false; fundidoPantalla.alpha=1; StartCoroutine(Fade(1,0,null)); }
        }
        public void MarcarCompletado(){ AventuraQuintaInsignia.Completar(nivel); }
        public void IrASiguiente(){ if(SiguienteDisponible)Load(escenaSiguiente.Trim()); }
        public void ComenzarPartidaNueva()
        {
            if(!PartidaNuevaDisponible)return;
            AventuraQuintaInsignia.Reiniciar();
            Load(escenaInicioAventura.Trim());
        }
        // Finalizar juego: load escenaFinalizar if set; otherwise the scene shows its closing screen (AlFinalizar).
        public event System.Action AlFinalizar;
        public void FinalizarJuego()
        {
            if(loading)return;
            if(Disponible(escenaFinalizar)){Load(escenaFinalizar.Trim());return;}
            if(AlFinalizar!=null){AlFinalizar();return;}
            Salir();
        }
        // Salir del juego: fade to black and close (in the Editor, stop Play).
        public void Salir()
        {
            if(loading)return;
            loading=true;
            if(AudioDirector.Instance)AudioDirector.Instance.Duck(0,duracionFundido+1);
            StartCoroutine(Quit());
        }
        IEnumerator Quit()
        {
            yield return Fade(fundidoPantalla?fundidoPantalla.alpha:0,1,null);
            yield return new WaitForSecondsRealtime(.3f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void Load(string scene)
        {
            if(loading)return;
            loading=true;
            if(AudioDirector.Instance)AudioDirector.Instance.Duck(0,duracionFundido+1);
            StartCoroutine(Fade(fundidoPantalla?fundidoPantalla.alpha:0,1,scene));
        }
        IEnumerator Fade(float from,float to,string scene)
        {
            float t=0;
            while(t<duracionFundido)
            {
                t+=Time.unscaledDeltaTime;
                if(fundidoPantalla)fundidoPantalla.alpha=Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,t/duracionFundido));
                yield return null;
            }
            if(fundidoPantalla)fundidoPantalla.alpha=to;
            if(scene!=null)SceneManager.LoadScene(scene);
        }
    }
}
