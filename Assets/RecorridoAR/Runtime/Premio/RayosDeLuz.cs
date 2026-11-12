using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Soft sunburst behind the insignia: rays fading out from the centre.
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class RayosDeLuz : MaskableGraphic
    {
        [Range(4,40)] public int rayos=16;
        [Range(.05f,.9f)] public float anchura=.45f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r=rectTransform.rect; var c=r.center; float R=Mathf.Min(r.width,r.height)*.5f;
            var inner=color; var outer=color; outer.a=0;
            for(int i=0;i<rayos;i++)
            {
                float a0=i*Mathf.PI*2/rayos, w=Mathf.PI*2/rayos*anchura*.5f;
                int n=vh.currentVertCount;
                vh.AddVert(c,inner,Vector2.zero);
                vh.AddVert(c+new Vector2(Mathf.Cos(a0-w),Mathf.Sin(a0-w))*R,outer,Vector2.zero);
                vh.AddVert(c+new Vector2(Mathf.Cos(a0+w),Mathf.Sin(a0+w))*R,outer,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);
            }
        }
    }
}
