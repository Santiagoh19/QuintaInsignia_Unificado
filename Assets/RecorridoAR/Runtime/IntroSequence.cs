using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Intro (~21 s, Saltar at any time). Plays once per scene entry; retries never replay it.
    //  1. Lucas walks in (walk-cycle PNGs) at sunset.        2. He stops: night is falling (! and thought cloud).
    //  3. The game scene appears and a short demo explains the mechanics: index -> Inicio -> path -> wolf.
    // The level stays in Ready the whole time: the clock cannot start and nobody can fail.
    public sealed class IntroSequence : MonoBehaviour
    {
        [Header("Apertura: logo Quinta Insignia y presentación del nivel")]
        public RectTransform logo;
        [Tooltip("Posición final del logo: la placa del título.")]
        public RectTransform destinoLogo;
        [Min(1)] public float escalaInicialLogo=1.75f;
        [Tooltip("Placa crema detrás del logo mientras está grande; se desvanece al llegar a la placa del título.")]
        public CanvasGroup fondoLogo;
        public CanvasGroup tarjetaNivel;
        [Tooltip("Segundos de la apertura antes de que Lucas entre caminando.")]
        [Min(0)] public float apertura=5f;
        [Header("Lucas")]
        public RectTransform lucas;
        public RawImage lucasImagen;
        public Texture2D[] lucasCaminando;
        [Min(1)] public float pasosPorSegundo=7;
        [Header("Lucas se da cuenta")]
        public RectTransform sorpresa;
        public CanvasGroup nube;
        public Text textoNube;
        public string[] pensamientos={"¡La carpa ya está lista… y se hace de noche!","Necesito energía para mi linterna."};
        [Header("Escenario y mecánicas")]
        public CanvasGroup recorrido;
        public RectTransform bateria;
        public FlashlightGlow luz;
        public RectTransform lobo;
        public RectTransform puntoDemo;
        public Image anilloDemo;
        public CanvasGroup tarjeta;
        public Text textoTarjeta;
        public string[] mecanicas={"Levanta tu índice: verás un punto rosa.","Déjalo en Inicio para tomar la batería.",
            "Lleva la batería por el camino hasta la linterna.","Si te sales del camino… ¡cuidado con el lobo!"};
        [Header("General")]
        public CanvasGroup grupo;
        public NightSky noche;
        public HandButton saltar;
        [Min(4)] public float duracion=25.8f;
        [Min(.1f)] public float salida=.7f;

        public bool Playing { get; private set; }
        public bool MuestraRecorrido => Playing && t-apertura>=T.Scene && t<duracion+salida;
        public bool MuestraLogo => Playing && logo;
        public float Tiempo => t;
        public event System.Action Terminada;

        // Timeline (seconds)
        static class T
        {
            public const float WalkEnd=4.0f, Notice=4.1f, Cloud1=4.6f, Cloud2=7.2f, CloudEnd=9.8f, Scene=9.8f, Home=11.0f,
                Step1=11.0f, Step2=13.2f, Pick=14.9f, Step3=15.2f, Arrive=18.2f, Step4=18.4f;
        }
        float t,prevT;
        Vector2 lucasHome,batteryHome,wolfHome;
        Vector3 lucasScale=Vector3.one;
        Texture lucasTexture;
        RouteRules route;
        float[] arcs;

        void Awake(){ if(saltar)saltar.alConfirmar.AddListener(Skip); }
        public void Play(RouteRules path)
        {
            route=path;
            Capture();
            gameObject.SetActive(true);
            t=prevT=0;Playing=true;
            if(grupo){grupo.alpha=0;grupo.blocksRaycasts=true;}
            Apply();
        }
        void Capture()
        {
            if(lucas){lucasHome=lucas.anchoredPosition;lucasScale=lucas.localScale;}
            if(lucasImagen)lucasTexture=lucasImagen.texture;
            if(bateria)batteryHome=bateria.anchoredPosition;
            if(lobo)wolfHome=lobo.anchoredPosition;
            arcs=null;
            if(route!=null && route.Points.Count>1)
            {
                arcs=new float[route.Points.Count];
                for(int i=1;i<arcs.Length;i++)arcs[i]=arcs[i-1]+Vector2.Distance(route.Points[i-1],route.Points[i]);
            }
        }
        public void Skip(){ if(Playing && t<duracion)t=duracion; }
        void Update()
        {
            if(!Playing)return;
            prevT=t;
            t+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            Apply();
            if(t>=duracion+salida)Finish();
        }
        void Finish()
        {
            Playing=false;
            Restore();
            if(saltar)saltar.ResetHold();
            gameObject.SetActive(false);
            Terminada?.Invoke();
        }
        void Restore()
        {
            if(lucas){lucas.anchoredPosition=lucasHome;lucas.localScale=lucasScale;lucas.localRotation=Quaternion.identity;}
            if(lucasImagen && lucasTexture)lucasImagen.texture=lucasTexture;
            if(bateria){bateria.anchoredPosition=batteryHome;bateria.localScale=Vector3.one;bateria.gameObject.SetActive(true);}
            if(luz){luz.intensity=0;luz.SetVerticesDirty();}
            if(lobo){lobo.anchoredPosition=wolfHome;lobo.gameObject.SetActive(false);}
            if(recorrido)recorrido.alpha=1;
            route?.SetVisualProgress(0);
            if(noche)noche.Set(0);
        }
        // Editor preview of one instant (Ruta de Energía > Capturar vistas de diseno).
        public void PreviewAt(float seconds,RouteRules path)
        {
            if(route==null || path!=route){route=path;Capture();}
            t=prevT=seconds;
            Apply();
        }

        static float Ease(float x){ x=Mathf.Clamp01(x); return x*x*(3-2*x); }
        static float Window(float t,float a,float b,float fade=.35f)=>Mathf.Clamp01((t-a)/fade)*Mathf.Clamp01((b-t)/fade);
        static Vector2 Local(Vector2 p)=>new Vector2(p.x-640,360-p.y);
        // Called from Story, where t is story time.
        bool Crossed(float mark)=>prevT-apertura<mark && t>=mark && t+apertura<duracion;

        void Apply()
        {
            Opening();
            float real=t; t=real-apertura;   // story time: the original timeline starts after the opening
            try{Story(real>=duracion,real);}finally{t=real;}
        }
        // 0-1 s logo appears big; 2.2-3.2 s it shrinks into the title plate; 3-5.6 s the level card.
        void Opening()
        {
            bool outro=t>=duracion;
            if(logo)
            {
                logo.gameObject.SetActive(true);
                float appear=Ease(t/.9f), move=Ease((t-2.2f)/1f);
                Vector2 to=destinoLogo?(Vector2)logo.parent.InverseTransformPoint(destinoLogo.position):new Vector2(0,285);
                logo.anchoredPosition=Vector2.Lerp(new Vector2(0,40),to,move);
                float sc=Mathf.Lerp(escalaInicialLogo,1,move)*(t<.9f?Mathf.Lerp(.8f,1,appear):1);
                logo.localScale=new Vector3(sc,sc,1);
                var g=logo.GetComponent<CanvasGroup>();
                if(g)g.alpha=outro?1-Mathf.Clamp01((t-duracion)/salida):appear;
                if(fondoLogo)fondoLogo.alpha=1-move;
            }
            if(tarjetaNivel)
            {
                tarjetaNivel.alpha=outro?0:Window(t,3.0f,5.6f,.35f);
                float pop=Mathf.Clamp01((t-3f)/.35f);
                float sc=pop<1?Mathf.Lerp(.85f,1.04f,pop):1+.04f*Mathf.Clamp01(1-(t-3.35f)/.3f);
                tarjetaNivel.transform.localScale=new Vector3(sc,sc,1);
            }
        }
        void Story(bool outro,float real)
        {
            float k=outro?1-Mathf.Clamp01((real-duracion)/salida):1;
            if(grupo)grupo.alpha=outro?k:Mathf.Clamp01(real/.5f);

            // ---- Lucas: walks in large, stops, then walks to his place in the game ----
            const float stage=1.3f, stageX=-150;
            float feet=lucasHome.y-(lucas?lucas.rect.height*.5f*lucasScale.y:92);
            float half=lucas?lucas.rect.height*.5f:92;
            bool walking=(t>=0 && t<T.WalkEnd) || (t>=T.Scene && t<T.Home);
            Vector2 pos; float scale;
            if(t<T.Scene)
            {
                float w=Ease(Mathf.Max(0,t)/T.WalkEnd);
                pos=new Vector2(Mathf.Lerp(760,stageX,w),feet+half*stage);scale=stage;
            }
            else
            {
                float w=Ease((t-T.Scene)/(T.Home-T.Scene));
                scale=Mathf.Lerp(stage,lucasScale.x,w);
                pos=new Vector2(Mathf.Lerp(stageX,lucasHome.x,w),feet+half*scale);
            }
            if(outro){pos=lucasHome;scale=lucasScale.x;walking=false;}
            if(lucas){lucas.anchoredPosition=pos;lucas.localScale=new Vector3(scale,scale,1);lucas.localRotation=Quaternion.identity;}
            if(lucasImagen)
            {
                Texture frame=lucasTexture;
                if(walking && lucasCaminando!=null && lucasCaminando.Length>0)
                    frame=lucasCaminando[Mathf.FloorToInt(t*pasosPorSegundo)%lucasCaminando.Length];
                if(frame)lucasImagen.texture=frame;
            }

            // ---- Night falls while he walks; it lifts a little when the game scene appears ----
            float night=Mathf.Clamp01((t-2.4f)/3f)*.88f;
            if(t>T.Scene)night=Mathf.Lerp(.88f,.4f,Mathf.Clamp01((t-T.Scene)/1.2f));
            if(noche)noche.Set(outro?0:night,true);

            // ---- He notices: "!" and the thought cloud ----
            if(sorpresa)
            {
                float pop=Mathf.Clamp01((t-T.Notice)/.25f);
                float s=pop<1?Mathf.Sin(pop*Mathf.PI*.5f)*1.2f:1+.06f*Mathf.Sin(t*10);
                bool on=t>=T.Notice && t<T.Cloud2 && !outro;
                sorpresa.gameObject.SetActive(on);
                sorpresa.localScale=new Vector3(s,s,1);
                sorpresa.anchoredPosition=new Vector2(pos.x-62*scale,pos.y+half*scale+18);
            }
            if(nube)
            {
                float a=outro?0:Window(t,T.Cloud1,T.CloudEnd,.3f);
                nube.alpha=a;
                float s=Mathf.Lerp(.85f,1,Mathf.Clamp01((t-T.Cloud1)/.3f));
                nube.transform.localScale=new Vector3(s,s,1);
                if(textoNube && pensamientos!=null && pensamientos.Length>0)
                {
                    int i=t<T.Cloud2?0:Mathf.Min(1,pensamientos.Length-1);
                    if(textoNube.text!=pensamientos[i])textoNube.text=pensamientos[i];
                }
            }

            // ---- Game scene + mechanics demo ----
            if(recorrido)recorrido.alpha=outro?k:Mathf.Clamp01((t-(T.Scene+.2f))/.6f);
            int step=t<T.Step2?0:t<T.Step3?1:t<T.Step4?2:3;
            if(tarjeta)tarjeta.alpha=outro?0:Window(t,T.Step1,duracion-apertura+.35f);
            if(textoTarjeta && mecanicas!=null && step<mecanicas.Length && textoTarjeta.text!=mecanicas[step])textoTarjeta.text=mecanicas[step];

            Vector2 start=route!=null?Local(route.Points[0]):batteryHome;
            if(puntoDemo)
            {
                bool on=t>=T.Step1 && t<T.Pick+.25f && !outro;
                puntoDemo.gameObject.SetActive(on);
                var from=start+new Vector2(160,150);
                puntoDemo.anchoredPosition=Vector2.Lerp(from,start,Ease((t-T.Step1-.3f)/1.6f));
                float pulse=1+.08f*Mathf.Sin(t*8);
                puntoDemo.localScale=new Vector3(pulse,pulse,1)*(t>T.Pick?1-Mathf.Clamp01((t-T.Pick)/.25f):1);
                if(anilloDemo)anilloDemo.fillAmount=Mathf.Clamp01((t-T.Step2)/(T.Pick-T.Step2));
            }
            if(Crossed(T.Pick))AudioDirector.Play(AudioDirector.Efecto.Recoger);
            if(bateria && route!=null && arcs!=null && !outro && t>=T.Scene)
            {
                float travel=Ease((t-T.Step3)/(T.Arrive-T.Step3));
                float arc=travel*arcs[arcs.Length-1];
                bateria.gameObject.SetActive(t<T.Arrive+.3f);
                // Before being picked up the battery waits beside Inicio, exactly as in the game.
                bateria.anchoredPosition=t<T.Pick?batteryHome:t<T.Step3?Vector2.Lerp(batteryHome,start,Ease((t-T.Pick)/.3f)):Local(PointAt(arc));
                float shrink=t>T.Arrive?1-Mathf.Clamp01((t-T.Arrive)/.3f)*.85f:1;
                bateria.localScale=new Vector3(shrink,shrink,1);
                route.SetVisualProgress(t>=T.Step3?arc:0);
            }
            if(Crossed(T.Arrive))AudioDirector.Play(AudioDirector.Efecto.Entregar,.8f);
            if(luz && !outro){luz.intensity=Mathf.Clamp01((t-T.Arrive)/.4f);luz.SetVerticesDirty();}
            if(lobo)
            {
                bool on=t>=T.Step4 && !outro;
                lobo.gameObject.SetActive(on);
                float peek=Mathf.Sin(Mathf.Clamp01((t-T.Step4)/(duracion-apertura-T.Step4))*Mathf.PI);
                lobo.anchoredPosition=wolfHome+new Vector2(200*Ease(peek*1.6f),0);
            }
            if(Crossed(T.Step4+.3f))AudioDirector.Play(AudioDirector.Efecto.Lobo,.45f);
            if(saltar)saltar.gameObject.SetActive(!outro);
        }
        Vector2 PointAt(float arc)
        {
            var p=route.Points;
            for(int i=1;i<p.Count;i++)
                if(arcs[i]>=arc){float seg=arcs[i]-arcs[i-1];return Vector2.Lerp(p[i-1],p[i],seg>0?(arc-arcs[i-1])/seg:0);}
            return p[p.Count-1];
        }
    }
}
