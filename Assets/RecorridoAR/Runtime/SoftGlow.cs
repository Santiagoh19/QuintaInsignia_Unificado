using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Editable radial glow (light halo, stars). Center uses the Graphic color; the edge fades to transparent.
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class SoftGlow : MaskableGraphic
    {
        [Range(0,1)] public float nucleo=.35f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r=rectTransform.rect;
            if(r.width<=0 || r.height<=0) return;
            Vector2 c=r.center, half=r.size*.5f;
            Color center=color, mid=color, edge=color;
            mid.a*=.45f; edge.a=0;
            vh.AddVert(c,center,Vector2.zero);
            const int steps=48;
            float inner=Mathf.Clamp(nucleo,.05f,.95f);
            for(int i=0;i<steps;i++)
            {
                float a=i*Mathf.PI*2/steps;
                var dir=new Vector2(Mathf.Cos(a)*half.x,Mathf.Sin(a)*half.y);
                vh.AddVert(c+dir*inner,mid,Vector2.zero);
                vh.AddVert(c+dir,edge,Vector2.zero);
            }
            for(int i=0;i<steps;i++)
            {
                int a=1+i*2, b=1+((i+1)%steps)*2;
                vh.AddTriangle(0,a,b);
                vh.AddTriangle(a,a+1,b+1);
                vh.AddTriangle(a,b+1,b);
            }
        }
    }
}
