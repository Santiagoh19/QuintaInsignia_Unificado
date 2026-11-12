using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Editable vector UI art. Coordinates are normalized to a 100 x 100 design.
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScoutIcon : MaskableGraphic
    {
        public enum Shape { Battery, Flashlight, Wolf }
        public Shape shape;
        public bool lit;
        static readonly Color Ink=new Color(.145f,.302f,.196f,1);
        static readonly Color Gold=new Color(.957f,.788f,.365f,1);
        static readonly Color Orange=new Color(.937f,.514f,.329f,1);
        Vector2 P(float x,float y)=>new Vector2(rectTransform.rect.xMin+x/100*rectTransform.rect.width,rectTransform.rect.yMax-y/100*rectTransform.rect.height);
        void Poly(VertexHelper v,Color c,params Vector2[] points)
        { int n=v.currentVertCount; foreach(var p in points)v.AddVert(P(p.x,p.y),c*color,Vector2.zero); for(int i=1;i<points.Length-1;i++)v.AddTriangle(n,n+i,n+i+1); }
        void Box(VertexHelper v,float x,float y,float w,float h,Color c)
        { Poly(v,c,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)); }
        void Oval(VertexHelper v,float x,float y,float rx,float ry,Color c)
        {var p=new Vector2[32];for(int i=0;i<32;i++){float a=i*Mathf.PI/16;p[i]=new Vector2(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry);}Poly(v,c,p);}
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();
            if(shape==Shape.Battery)
            {
                Box(v,40,5,20,12,Ink);Box(v,25,14,50,77,Ink);Box(v,30,19,40,67,Orange);
                Box(v,30,19,40,16,Gold);Box(v,34,40,7,37,new Color(1,1,1,.3f));
                Box(v,45,22,10,3,Ink);Box(v,48.5f,18.5f,3,10,Ink);
                Poly(v,Gold,new Vector2(53,41),new Vector2(40,62),new Vector2(50,62),new Vector2(46,77),new Vector2(63,54),new Vector2(52,54));
            }
            else if(shape==Shape.Flashlight)
            {
                if(lit) Poly(v,new Color(1,.86f,.3f,.35f),new Vector2(68,30),new Vector2(100,4),new Vector2(100,96),new Vector2(68,70));
                Box(v,10,35,47,30,Ink);Box(v,14,39,43,22,new Color(.31f,.54f,.36f,1));
                Box(v,21,39,5,22,Gold);Box(v,31,39,5,22,Gold);
                Poly(v,Ink,new Vector2(50,35),new Vector2(70,23),new Vector2(80,23),new Vector2(80,77),new Vector2(70,77),new Vector2(50,65));
                Box(v,65,30,10,40,Gold);Oval(v,78,50,7,23,lit?new Color(1,1,.75f,1):new Color(.9f,.94f,.87f,1));
                Box(v,41,32,9,6,Orange);
            }
            else
            {
                Color fur=new Color(.48f,.56f,.55f,1),light=new Color(.79f,.83f,.78f,1);
                Poly(v,Ink,new Vector2(23,52),new Vector2(0,31),new Vector2(7,64),new Vector2(28,75));
                Oval(v,43,62,30,22,Ink);Oval(v,43,60,26,18,fur);
                Box(v,23,69,12,22,Ink);Box(v,55,69,12,22,Ink);
                Poly(v,Ink,new Vector2(51,40),new Vector2(51,8),new Vector2(68,26),new Vector2(86,7),new Vector2(91,44));
                Oval(v,71,43,25,26,Ink);Oval(v,71,44,21,22,fur);
                Poly(v,light,new Vector2(51,46),new Vector2(71,58),new Vector2(91,46),new Vector2(84,67),new Vector2(60,67));
                Oval(v,63,40,3,4,Ink);Oval(v,81,40,3,4,Ink);Oval(v,72,54,5,3,Ink);
                Poly(v,Orange,new Vector2(52,67),new Vector2(85,67),new Vector2(65,80));
            }
        }
    }
}
