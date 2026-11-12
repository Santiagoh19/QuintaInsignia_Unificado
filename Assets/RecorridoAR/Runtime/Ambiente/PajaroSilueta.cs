using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Small flying bird silhouette (vector, sharp at any size). aleteo: -1 wings down .. 1 wings up.
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class PajaroSilueta : MaskableGraphic
    {
        [Range(-1,1)] public float aleteo=.4f;
        public void Aletear(float value){ if(Mathf.Abs(value-aleteo)<.01f)return; aleteo=value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r=rectTransform.rect; float w=r.width*.5f, h=r.height*.5f; var c=r.center;
            float tip=aleteo*h*.9f, mid=aleteo*h*.25f+h*.15f;
            Wing(vh,c,-1,w,h,mid,tip); Wing(vh,c,1,w,h,mid,tip);
            // body
            int n=vh.currentVertCount;
            vh.AddVert(c+new Vector2(-w*.12f,0),color,Vector2.zero);vh.AddVert(c+new Vector2(0,h*.18f),color,Vector2.zero);
            vh.AddVert(c+new Vector2(w*.14f,0),color,Vector2.zero);vh.AddVert(c+new Vector2(0,-h*.2f),color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        void Wing(VertexHelper vh,Vector2 c,float side,float w,float h,float mid,float tip)
        {
            // Bent wing: body -> elbow -> tip, thick at the body, thin at the tip.
            Vector2 a=c, b=c+new Vector2(side*w*.45f,mid), t=c+new Vector2(side*w,tip);
            float ta=h*.32f, tb=h*.2f;
            int n=vh.currentVertCount;
            vh.AddVert(a+new Vector2(0,ta*.5f),color,Vector2.zero);vh.AddVert(a-new Vector2(0,ta*.5f),color,Vector2.zero);
            vh.AddVert(b+new Vector2(0,tb*.5f),color,Vector2.zero);vh.AddVert(b-new Vector2(0,tb*.5f),color,Vector2.zero);
            vh.AddVert(t,color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+3);vh.AddTriangle(n,n+3,n+2);vh.AddTriangle(n+2,n+3,n+4);
        }
    }
}
