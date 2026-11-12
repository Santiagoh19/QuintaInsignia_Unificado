using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Gentle life over the sunset: occasional birds crossing the sky, fireflies over the meadow
    // (brighter as night falls) and two butterflies by day. Calmer while the player carries the battery.
    public sealed class AmbienteVivo : MonoBehaviour
    {
        public NightSky noche;
        public PajaroSilueta[] pajaros;
        public SoftGlow[] luciernagas;
        public MariposaVector[] mariposas;
        [Tooltip("Menos movimiento (lo activa el juego mientras se lleva la batería).")]
        public bool calma;
        [Tooltip("Segundos entre bandadas (mínimo y máximo).")]
        public Vector2 intervaloBandada=new Vector2(10,22);
        [Tooltip("Velocidad de los pájaros en unidades del tablero por segundo.")]
        public Vector2 velocidadPajaros=new Vector2(70,100);
        [Tooltip("Franja del cielo por la que vuelan (y local del tablero).")]
        public Vector2 alturaPajaros=new Vector2(240,325);
        public Color colorPajaro=new Color(.22f,.15f,.26f,.8f);

        System.Random random=new System.Random(20261109);
        float nextFlock=4, flockStart=-1, flockSpeed, flockDir, flockY;
        int flockSize;
        Vector2[] fireflyHome, butterflyHome; float[] phase;
        bool ready;

        float R(float a,float b)=>a+(float)random.NextDouble()*(b-a);
        void Init()
        {
            if(ready)return; ready=true;
            int n=luciernagas!=null?luciernagas.Length:0;
            fireflyHome=new Vector2[n]; phase=new float[Mathf.Max(n,8)];
            for(int i=0;i<n;i++){fireflyHome[i]=luciernagas[i]?luciernagas[i].rectTransform.anchoredPosition:Vector2.zero;phase[i]=R(0,6.28f);}
            int m=mariposas!=null?mariposas.Length:0; butterflyHome=new Vector2[m];
            for(int i=0;i<m;i++)butterflyHome[i]=mariposas[i]?mariposas[i].rectTransform.anchoredPosition:Vector2.zero;
            if(pajaros!=null)foreach(var p in pajaros)if(p)p.gameObject.SetActive(false);
        }
        static float Smooth(float a,float b,float x){x=Mathf.Clamp01((x-a)/(b-a));return x*x*(3-2*x);}
        void Update(){ Apply(Time.unscaledTime,noche?noche.Alpha:0); }

        // Editor preview of one instant (used by the design-view capture).
        public void PreviewAt(float t,float night,bool flockVisible)
        {
            Init();
            if(flockVisible){flockStart=t-6;flockDir=1;flockSpeed=85;flockY=290;flockSize=3;}
            Apply(t,night);
        }
        void Apply(float t,float night)
        {
            Init();
            float day=1-Smooth(.2f,.55f,night);
            // ---- birds ----
            if(pajaros!=null && pajaros.Length>0)
            {
                if(flockStart<0 && t>=nextFlock && night<.45f)
                {
                    flockStart=t;flockDir=random.NextDouble()<.5?1:-1;flockSpeed=R(velocidadPajaros.x,velocidadPajaros.y);
                    flockY=R(alturaPajaros.x,alturaPajaros.y);flockSize=calma?1:1+random.Next(Mathf.Min(3,pajaros.Length));
                }
                bool any=false;
                for(int i=0;i<pajaros.Length;i++)
                {
                    var p=pajaros[i]; if(!p)continue;
                    bool on=flockStart>=0 && i<flockSize;
                    if(on)
                    {
                        float x=-flockDir*720+flockDir*flockSpeed*(t-flockStart)-flockDir*i*46;
                        float y=flockY+(i%2==0?1:-1)*i*9+Mathf.Sin((t+i)*1.3f)*4;
                        on=Mathf.Abs(x)<760; if(Mathf.Abs(x)<800)any|=true;
                        p.rectTransform.anchoredPosition=new Vector2(x,y);
                        p.rectTransform.localScale=new Vector3(flockDir*(1-i*.08f),1-i*.08f,1);
                        p.Aletear(Mathf.Sin(t*(8.5f+i*.7f)+i*1.9f));
                        var c=colorPajaro;c.a*=day;p.color=c;
                    }
                    if(p.gameObject.activeSelf!=on)p.gameObject.SetActive(on);
                }
                if(flockStart>=0 && !any && t-flockStart>1){flockStart=-1;nextFlock=t+R(intervaloBandada.x,intervaloBandada.y)*(calma?1.8f:1);}
            }
            // ---- fireflies: faint at sunset, bright at night ----
            if(luciernagas!=null)
            {
                float vis=Mathf.Lerp(.28f,1,Smooth(.15f,.6f,night))*(calma?.75f:1);
                for(int i=0;i<luciernagas.Length;i++)
                {
                    var f=luciernagas[i]; if(!f)continue; float ph=phase[i];
                    var pos=fireflyHome[i]+new Vector2(Mathf.Sin(t*.37f+ph)*38+Mathf.Sin(t*.91f+ph*2)*12,Mathf.Cos(t*.29f+ph)*22+Mathf.Sin(t*.73f+ph)*8);
                    f.rectTransform.anchoredPosition=pos;
                    float blink=Mathf.Pow(.5f+.5f*Mathf.Sin(t*(1.1f+(i%5)*.23f)+ph*3),2);
                    var c=f.color;c.a=vis*(.25f+.75f*blink);f.color=c;
                }
            }
            // ---- butterflies: by day only ----
            if(mariposas!=null)
            {
                for(int i=0;i<mariposas.Length;i++)
                {
                    var b=mariposas[i]; if(!b)continue; float ph=i*2.3f+.7f;
                    float a=day*(calma?.6f:1);
                    bool on=a>.02f; if(b.gameObject.activeSelf!=on)b.gameObject.SetActive(on); if(!on)continue;
                    var off=new Vector2(Mathf.Sin(t*.21f+ph)*90+Mathf.Sin(t*.53f+ph)*25,Mathf.Sin(t*.37f+ph*1.7f)*35+Mathf.Abs(Mathf.Sin(t*2.6f+ph))*10);
                    var vel=new Vector2(Mathf.Cos(t*.21f+ph)*.21f*90+Mathf.Cos(t*.53f+ph)*.53f*25,0);
                    b.rectTransform.anchoredPosition=butterflyHome[i]+off;
                    float face=vel.x>=0?1:-1; b.rectTransform.localScale=new Vector3(face,1,1);
                    b.Abrir(.25f+.75f*Mathf.Abs(Mathf.Sin(t*13f+ph)));
                    var c=b.color;c.a=a;b.color=c;
                }
            }
        }
    }
}
