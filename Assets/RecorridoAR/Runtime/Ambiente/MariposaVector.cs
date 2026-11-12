using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Small butterfly (vector). apertura 0..1 opens and closes the wings.
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class MariposaVector : MaskableGraphic
    {
        [Range(0,1)] public float apertura=1;
        public Color colorAlas=new Color(.937f,.514f,.329f,1);
        public Color colorDetalle=new Color(.957f,.788f,.365f,1);
        public Color colorCuerpo=new Color(.20f,.16f,.18f,1);
        public void Abrir(float value){ if(Mathf.Abs(value-apertura)<.02f)return; apertura=value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r=rectTransform.rect; float w=r.width*.5f*Mathf.Lerp(.15f,1,apertura), h=r.height*.5f; var c=r.center;
            for(int s=-1;s<=1;s+=2)
            {
                Oval(vh,c+new Vector2(s*w*.55f,h*.28f),w*.55f,h*.55f,colorAlas*color);
                Oval(vh,c+new Vector2(s*w*.45f,-h*.38f),w*.42f,h*.4f,colorAlas*color);
                Oval(vh,c+new Vector2(s*w*.6f,h*.34f),w*.2f,h*.18f,colorDetalle*color);
            }
            Oval(vh,c,Mathf.Max(1,r.width*.06f),h*.75f,colorCuerpo*color);
        }
        static void Oval(VertexHelper vh,Vector2 c,float rx,float ry,Color col)
        {
            int n=vh.currentVertCount; vh.AddVert(c,col,Vector2.zero);
            for(int i=0;i<=16;i++){float a=i*Mathf.PI*2/16;vh.AddVert(c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),col,Vector2.zero);}
            for(int i=1;i<=16;i++)vh.AddTriangle(n,n+i,n+i+1);
        }
    }
}
