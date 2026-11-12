using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RecorridoAR
{
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class RouteGraphic : MaskableGraphic
    {
        [System.NonSerialized] public RouteRules route;
        [System.NonSerialized] public Vector2 cursor;
        [System.NonSerialized] public bool showCursor;
        public Color lila = new Color(0.64f,0.46f,1,1);
        public Color coral = new Color(1,0.48f,0.49f,1);
        public Color turquesa = new Color(0.22f,0.82f,0.84f,1);
        static readonly Color Ink=new Color(0.13f,0.12f,0.29f,1);
        static readonly Color Green=new Color(0.23f,0.88f,0.55f,1);
        static readonly Color Blue=new Color(0.19f,0.64f,1,1);
        protected override void OnEnable()
        { if(route==null) route=new RouteRules(1,true); base.OnEnable(); SetVerticesDirty(); }
        Vector2 Local(Vector2 p)=>new Vector2(p.x-640,360-p.y);
        void Vertex(VertexHelper vh,Vector2 p,Color c)=>vh.AddVert(new Vector3(p.x,p.y,0),c,Vector2.zero);
        void Disk(VertexHelper vh,Vector2 p,float r,Color c)
        {
            p=Local(p);int first=vh.currentVertCount;Vertex(vh,p,c);const int n=32;
            for(int i=0;i<=n;i++) {float a=i*2*Mathf.PI/n;Vertex(vh,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,c);}
            for(int i=0;i<n;i++) vh.AddTriangle(first,first+i+1,first+i+2);
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {
            a=Local(a);b=Local(b);Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*0.5f;int i=vh.currentVertCount;
            Vertex(vh,a-n,c);Vertex(vh,a+n,c);Vertex(vh,b+n,c);Vertex(vh,b-n,c);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        Color Ribbon(float t)=>t<0.5f?Color.Lerp(lila,coral,t*2):Color.Lerp(coral,turquesa,(t-.5f)*2);
        // Joined strip: fewer vertices and no repeated translucent disks at every sample.
        void Stroke(VertexHelper vh,IList<Vector2> points,float width,Color c,float length=float.MaxValue,bool gradient=false,Vector2 offset=default)
        {
            if(points.Count<2 || length<=0) return;
            float traveled=0; int first=vh.currentVertCount; int pairs=0;
            Vector2 final=points[0];
            for(int i=0;i<points.Count;i++)
            {
                Vector2 p=points[i];bool end=false;
                if(i>0)
                {
                    float segment=Vector2.Distance(points[i-1],points[i]);
                    if(traveled+segment>=length)
                    {p=Vector2.Lerp(points[i-1],points[i],(length-traveled)/segment);traveled=length;end=true;}
                    else traveled+=segment;
                }
                Vector2 direction=i==0?points[1]-points[0]:i==points.Count-1 || end?points[i]-points[i-1]:points[i+1]-points[i-1];
                direction.Normalize();Vector2 normal=new Vector2(-direction.y,direction.x);
                Color shade=gradient?Ribbon(traveled/route.Length):c;
                Color clear=new Color(shade.r,shade.g,shade.b,0);
                Vertex(vh,Local(p+offset-normal*(width/2+1)),clear);
                Vertex(vh,Local(p+offset-normal*width/2),shade);
                Vertex(vh,Local(p+offset+normal*width/2),shade);
                Vertex(vh,Local(p+offset+normal*(width/2+1)),clear);pairs++;
                final=p;
                if(end)break;
            }
            for(int i=0;i<pairs-1;i++)for(int j=0;j<3;j++)
            {int q=first+i*4+j;vh.AddTriangle(q,q+1,q+5);vh.AddTriangle(q,q+5,q+4);}
            Disk(vh,points[0]+offset,width/2,gradient?Ribbon(0):c);
            Disk(vh,final+offset,width/2,gradient?Ribbon(Mathf.Min(length,route.Length)/route.Length):c);
        }
        void Endpoint(VertexHelper vh,Vector2 p,Color c,bool finish)
        {
            Disk(vh,p+new Vector2(0,5),32,new Color(0.08f,0.06f,0.2f,0.3f));
            Disk(vh,p,32,Color.white);Disk(vh,p,27,c);
            Disk(vh,p+new Vector2(-8,-9),5,new Color(1,1,1,0.45f));
            if(!finish) Disk(vh,p,7,Color.white);
            else
            {
                Line(vh,p+new Vector2(-5,8),p+new Vector2(-5,-10),3,Color.white);
                Line(vh,p+new Vector2(-3,-8),p+new Vector2(9,-8),8,Color.white);
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(route==null)route=new RouteRules(1,true);
            float width=route.LaneRadius*2;
            Stroke(vh,route.Points,width+18,new Color(0.08f,0.055f,0.2f,0.22f),offset:new Vector2(0,7));
            Stroke(vh,route.Points,width+16,new Color(.20f,.36f,.43f,1));
            Stroke(vh,route.Points,width,new Color(1,.99f,.97f,1));
            if(route.Progress>0)Stroke(vh,route.Points,22,new Color(.65f,.78f,.53f,1),route.Progress);
            // Endpoints are the user's PNG images, serialized in the scene.
            if(showCursor)
            {
                Disk(vh,cursor,20,new Color(1,0.83f,0.22f,0.24f));Disk(vh,cursor,13,Ink);
                Disk(vh,cursor,10,new Color(1,0.85f,0.22f,1));Disk(vh,cursor+new Vector2(-3,-3),3,Color.white);
            }
        }
    }
}
