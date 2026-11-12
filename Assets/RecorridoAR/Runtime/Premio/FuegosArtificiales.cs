using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Fireworks bursts and falling confetti for the insignia reward, drawn as one light UI mesh.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FuegosArtificiales : MaskableGraphic
    {
        public float gravedad=170, vida=1.6f;
        [Min(0)] public int piezasConfeti=70;
        public float duracionConfeti=4.5f;
        public Color[] paleta={new Color(.96f,.79f,.37f,1),new Color(.94f,.51f,.33f,1),new Color(.99f,.97f,.9f,1),
            new Color(.45f,.75f,.5f,1),new Color(.55f,.8f,.95f,1),new Color(.84f,.6f,.95f,1)};
        struct Burst{public Vector2 c;public float t0,speed;public Color col;public int n,seed;}
        readonly List<Burst> bursts=new List<Burst>();
        float now, confettiStart=-99;
        protected override void OnEnable(){ base.OnEnable(); raycastTarget=false; }
        // In play mode the clock follows real time even if this object was inactive until now.
        void Sync(){ if(Application.isPlaying)now=Time.unscaledTime; }
        public void Lanzar(Vector2 centro,Color color,int particulas=42)
        { Sync(); bursts.Add(new Burst{c=centro,t0=now,col=color,n=particulas,speed=Random.Range(170f,240f),seed=Random.Range(1,99999)}); SetVerticesDirty(); }
        public void Confeti(){ Sync(); confettiStart=now; SetVerticesDirty(); }
        public void Limpiar(){ Sync(); bursts.Clear(); confettiStart=-99; SetVerticesDirty(); }
        public int Estallidos => bursts.Count;
        public bool Activo => bursts.Count>0 || now-confettiStart<duracionConfeti+3;
        // Editor preview: set the internal clock.
        public float Ahora { get=>now; set{ now=value; SetVerticesDirty(); } }
        void Update()
        {
            Sync();
            bursts.RemoveAll(b=>now-b.t0>vida+.1f);
            if(Activo)SetVerticesDirty();
        }
        // Deterministic noise in [-1, 1].
        static float H(int s){ s=(s<<13)^s; return 1f-((s*(s*s*15731+789221)+1376312589)&0x7fffffff)/1073741824f; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach(var b in bursts)
            {
                float age=now-b.t0; if(age<0||age>vida)continue;
                float k=age/vida, alpha=(1-k)*(1-k);
                for(int i=0;i<b.n;i++)
                {
                    float ang=i*Mathf.PI*2/b.n+H(b.seed+i)*.25f, sp=b.speed*(.55f+.45f*Mathf.Abs(H(b.seed+i*7)));
                    var dir=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));
                    Vector2 P(float a)=>b.c+dir*sp*a*(1-a*.3f)+new Vector2(0,-.5f*gravedad*a*a);
                    float twinkle=.7f+.3f*Mathf.Sin(now*30+i);
                    var col=Color.Lerp(Color.white,b.col,Mathf.Clamp01(age*4));col.a=alpha*twinkle;
                    Diamond(vh,P(age),Mathf.Lerp(7,2.5f,k),col);
                    var trail=col;trail.a*=.45f;
                    Diamond(vh,P(Mathf.Max(0,age-.07f)),Mathf.Lerp(4.5f,1.5f,k),trail);
                }
            }
            float ca=now-confettiStart;
            if(ca>=0 && ca<duracionConfeti+3 && paleta!=null && paleta.Length>0)
            {
                float fade=Mathf.Clamp01((duracionConfeti+3-ca)/1.2f);
                for(int i=0;i<piezasConfeti;i++)
                {
                    float delay=Mathf.Abs(H(i*13))*1.2f, a=ca-delay; if(a<0)continue;
                    float x=H(i*3)*600+Mathf.Sin(a*2.2f+i)*22, y=390-a*(85+35*Mathf.Abs(H(i*5)));
                    if(y<-380)continue;
                    var col=paleta[i%paleta.Length];col.a=fade;
                    Rect(vh,new Vector2(x,y),new Vector2(10,6),a*(2+H(i*11)*3)+i,Mathf.Abs(Mathf.Cos(a*4+i)),col);
                }
            }
        }
        static void Diamond(VertexHelper vh,Vector2 c,float r,Color col)
        {
            int n=vh.currentVertCount;
            vh.AddVert(c+new Vector2(0,r),col,Vector2.zero);vh.AddVert(c+new Vector2(r,0),col,Vector2.zero);
            vh.AddVert(c+new Vector2(0,-r),col,Vector2.zero);vh.AddVert(c+new Vector2(-r,0),col,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void Rect(VertexHelper vh,Vector2 c,Vector2 size,float rot,float flip,Color col)
        {
            float cs=Mathf.Cos(rot),sn=Mathf.Sin(rot); var hx=new Vector2(cs,sn)*size.x*.5f; var hy=new Vector2(-sn,cs)*size.y*.5f*Mathf.Max(.15f,flip);
            int n=vh.currentVertCount;
            vh.AddVert(c-hx-hy,col,Vector2.zero);vh.AddVert(c+hx-hy,col,Vector2.zero);vh.AddVert(c+hx+hy,col,Vector2.zero);vh.AddVert(c-hx+hy,col,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
