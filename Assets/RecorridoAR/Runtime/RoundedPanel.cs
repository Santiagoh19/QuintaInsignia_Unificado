using UnityEngine;
using UnityEngine.UI;

namespace RecorridoAR
{
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedPanel : MaskableGraphic
    {
        [Min(0)] public float radius = 24;
        public Color lowerColor = Color.white;
        [Range(0,1)] public float gradient = 0;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            if(r.width<=0 || r.height<=0) return;
            float cr=Mathf.Min(radius,Mathf.Min(r.width,r.height)*0.5f);
            Add(vh,r.center,r); const int cornerSteps=24;
            Vector2[] centers={new Vector2(r.xMax-cr,r.yMax-cr),new Vector2(r.xMin+cr,r.yMax-cr),
                new Vector2(r.xMin+cr,r.yMin+cr),new Vector2(r.xMax-cr,r.yMin+cr)};
            for(int c=0;c<4;c++) for(int j=0;j<=cornerSteps;j++)
            {
                float a=(c*90+j*90f/cornerSteps)*Mathf.Deg2Rad;
                Add(vh,centers[c]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*cr,r);
            }
            int count=4*(cornerSteps+1);
            for(int i=0;i<count;i++) vh.AddTriangle(0,i+1,(i+1)%count+1);
            // A transparent outer fringe smooths the edge in a Screen Space Overlay canvas.
            const float feather=1;
            for(int c=0;c<4;c++) for(int j=0;j<=cornerSteps;j++)
            {
                float a=(c*90+j*90f/cornerSteps)*Mathf.Deg2Rad;
                Vector2 p=centers[c]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(cr+feather);
                vh.AddVert(p,new Color(color.r,color.g,color.b,0),Vector2.zero);
            }
            for(int i=0;i<count;i++)
            {int a=i+1,b=(i+1)%count+1;vh.AddTriangle(a,b,b+count);vh.AddTriangle(a,b+count,a+count);}
        }
        void Add(VertexHelper vh,Vector2 p,Rect r)
        {
            Color c=Color.Lerp(color,lowerColor,(1-Mathf.InverseLerp(r.yMin,r.yMax,p.y))*gradient);
            vh.AddVert(new Vector3(p.x,p.y,0),c,Vector2.zero);
        }
    }
}
