using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FlashlightGlow : MaskableGraphic
    {
        [System.NonSerialized] public float intensity;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(intensity<=0)return;
            var r=rectTransform.rect;
            // The supplied flashlight faces left. A soft beam extends from its lens.
            vh.AddVert(new Vector2(r.xMax,r.center.y),new Color(1,.9f,.45f,.55f*intensity),Vector2.zero);
            vh.AddVert(new Vector2(r.xMin,r.yMax),new Color(1,.95f,.65f,0),Vector2.zero);
            vh.AddVert(new Vector2(r.xMin,r.yMin),new Color(1,.95f,.65f,0),Vector2.zero);
            vh.AddTriangle(0,1,2);
        }
    }
}
