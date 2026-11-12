using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Vector speaker for the sound toggle. Coordinates use a 100 x 100 design like ScoutIcon.
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpeakerIcon : MaskableGraphic
    {
        public bool silenciado;
        public Color colorTachado=new Color(.80f,.29f,.27f,1);
        Vector2 P(float x,float y)=>new Vector2(rectTransform.rect.xMin+x/100*rectTransform.rect.width,rectTransform.rect.yMax-y/100*rectTransform.rect.height);
        void Poly(VertexHelper v,Color c,params Vector2[] pts)
        { int n=v.currentVertCount; foreach(var p in pts)v.AddVert(P(p.x,p.y),c,Vector2.zero); for(int i=1;i<pts.Length-1;i++)v.AddTriangle(n,n+i,n+i+1); }
        void Line(VertexHelper v,Color c,Vector2 a,Vector2 b,float w)
        {
            var n=new Vector2(-(b.y-a.y),b.x-a.x).normalized*w*.5f;
            Poly(v,c,a+n,b+n,b-n,a-n);
        }
        void Arc(VertexHelper v,Color c,float radius,float w,float span)
        {
            const int steps=14;
            for(int i=0;i<steps;i++)
            {
                float a0=Mathf.Lerp(-span,span,i/(float)steps)*Mathf.Deg2Rad, a1=Mathf.Lerp(-span,span,(i+1)/(float)steps)*Mathf.Deg2Rad;
                Vector2 o=new Vector2(46,50);
                Vector2 p0=o+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*(radius-w*.5f), p1=o+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*(radius-w*.5f);
                Vector2 q0=o+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*(radius+w*.5f), q1=o+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*(radius+w*.5f);
                Poly(v,c,p0,p1,q1,q0);
            }
        }
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            Color ink=color;
            Poly(v,ink,new Vector2(14,38),new Vector2(30,38),new Vector2(50,20),new Vector2(50,80),new Vector2(30,62),new Vector2(14,62));
            if(!silenciado){Arc(v,ink,16,7,42);Arc(v,ink,30,7,46);}
            else{Line(v,colorTachado,new Vector2(60,36),new Vector2(86,64),8);Line(v,colorTachado,new Vector2(60,64),new Vector2(86,36),8);}
        }
        public void SetMuted(bool value){ if(silenciado==value)return; silenciado=value; SetVerticesDirty(); }
    }
}
