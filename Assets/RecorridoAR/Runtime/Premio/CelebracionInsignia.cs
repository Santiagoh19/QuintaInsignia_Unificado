using UnityEngine;
namespace RecorridoAR
{
    // Reward for finishing any difficulty: the Ruta de Energía insignia pops in at the centre with light rays,
    // fireworks, confetti and a reward fanfare, then flies to the top-left corner and stays there.
    public sealed class CelebracionInsignia : MonoBehaviour
    {
        public RectTransform insignia;
        public RectTransform rayos;
        public CanvasGroup grupoRayos;
        public SoftGlow brillo;
        public CanvasGroup tarjeta;
        public FuegosArtificiales fuegos;
        [Header("Posiciones (unidades del tablero)")]
        public Vector2 centro=new Vector2(0,70);
        public Vector2 esquina=new Vector2(-494,316);
        [Min(10)] public float tamanoCentro=300, tamanoEsquina=66;
        [Header("Tiempo")]
        [Min(2)] public float duracion=4.8f;

        public bool Playing { get; private set; }
        public bool EnEsquina { get; private set; }
        float t; System.Action done; int nextBurst;
        static readonly float[] BurstTimes={.15f,.5f,.95f,1.4f,1.9f,2.45f,3.0f};
        static readonly Vector2[] BurstPos={new Vector2(-330,190),new Vector2(340,215),new Vector2(-170,275),new Vector2(230,110),
            new Vector2(-430,80),new Vector2(430,270),new Vector2(10,280)};

        public void Play(System.Action alTerminar)
        {
            gameObject.SetActive(true);
            t=0;Playing=true;EnEsquina=false;done=alTerminar;nextBurst=0;
            if(fuegos){fuegos.Limpiar();fuegos.Confeti();}
            AudioDirector.Play(AudioDirector.Efecto.Premio);
            if(AudioDirector.Instance)AudioDirector.Instance.Duck(.3f,duracion);
            Apply();
        }
        // The insignia already won in this session: shown directly in the corner.
        public void MostrarEnEsquina()
        { gameObject.SetActive(true); Playing=false; EnEsquina=true; t=duracion; if(fuegos)fuegos.Limpiar(); Apply(); }
        public void Ocultar(){ Playing=false; EnEsquina=false; gameObject.SetActive(false); }
        // Editor preview of one instant (design-view capture), fireworks included.
        public void PreviewAt(float time)
        {
            gameObject.SetActive(true); t=time; Playing=false; EnEsquina=time>=duracion;
            if(fuegos)
            {
                fuegos.Limpiar(); fuegos.Ahora=0; fuegos.Confeti();
                for(int i=0;i<BurstTimes.Length && BurstTimes[i]<=time;i++){fuegos.Ahora=BurstTimes[i];fuegos.Lanzar(BurstPos[i],fuegos.paleta[i%fuegos.paleta.Length]);}
                fuegos.Ahora=time;
            }
            Apply();
        }

        void Update()
        {
            if(!Playing){ if(EnEsquina)Idle(); return; }
            t+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            var pal=fuegos?fuegos.paleta:null;
            while(nextBurst<BurstTimes.Length && t>=BurstTimes[nextBurst])
            {
                if(fuegos)fuegos.Lanzar(BurstPos[nextBurst],pal!=null&&pal.Length>0?pal[nextBurst%pal.Length]:Color.yellow);
                AudioDirector.Play(AudioDirector.Efecto.Fuegos,nextBurst==0?.8f:.55f);
                nextBurst++;
            }
            Apply();
            if(t>=duracion){Playing=false;EnEsquina=true;Apply();var d=done;done=null;d?.Invoke();}
        }
        static float Ease(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
        static float Back(float x){x=Mathf.Clamp01(x);const float s=1.7f;x-=1;return x*x*((s+1)*x+s)+1;}
        static float Window(float t,float a,float b,float f=.35f)=>Mathf.Clamp01((t-a)/f)*Mathf.Clamp01((b-t)/f);
        void Apply()
        {
            float fly=Ease((t-(duracion-1f))/.85f);
            float pop=t<.5f?Back(t/.5f):1;
            var pos=Vector2.Lerp(centro,esquina,fly);
            float size=Mathf.Lerp(tamanoCentro,tamanoEsquina,fly);
            if(insignia)
            {
                insignia.anchoredPosition=pos;insignia.sizeDelta=new Vector2(size,size);
                float breathe=1+.025f*Mathf.Sin(t*3.2f)*(1-fly);
                insignia.localScale=Vector3.one*pop*breathe;
                insignia.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*2.1f)*4*(1-fly));
            }
            if(rayos)
            {
                rayos.anchoredPosition=pos;rayos.localRotation=Quaternion.Euler(0,0,-t*18);
                float s=Mathf.Lerp(1,.45f,fly)*Mathf.Lerp(.6f,1,Ease(t/.6f));rayos.localScale=new Vector3(s,s,1);
            }
            if(grupoRayos)grupoRayos.alpha=EnEsquina?0:Mathf.Lerp(Window(t,.05f,duracion,.4f),0,fly);
            if(brillo){brillo.rectTransform.anchoredPosition=pos;var c=brillo.color;c.a=Mathf.Lerp(.85f,0,fly)*Mathf.Clamp01(t/.3f);brillo.color=c;
                float bs=size/tamanoCentro;brillo.rectTransform.localScale=new Vector3(bs,bs,1);}
            if(tarjeta)tarjeta.alpha=Window(t,.45f,duracion-.95f);
        }
        // Resting in its slot next to the level-1 medal: still and clean.
        void Idle()
        {
            if(insignia){insignia.localScale=Vector3.one;insignia.localRotation=Quaternion.identity;}
        }
    }
}
