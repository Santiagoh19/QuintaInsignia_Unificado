using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Draws the detected hand points over "Tu cámara" (like the green markers of the Python preview),
    // so the player and the teacher can see what the camera is reading. Index fingertip in pink.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PuntosManoVista : MaskableGraphic
    {
        public SeguimientoUnity seguimiento;
        public RecorridoGame juego;
        [Min(1)] public float tamano=5;
        public Color colorPuntos=new Color(.36f,.85f,.55f,1);
        public Color colorIndice=new Color(1,.08f,.40f,1);
        static readonly int[,] Bones={{0,1},{1,2},{2,3},{3,4},{0,5},{5,6},{6,7},{7,8},{5,9},{9,10},{10,11},{11,12},{9,13},{13,14},{14,15},{15,16},{13,17},{0,17},{17,18},{18,19},{19,20}};
        protected override void OnEnable(){ base.OnEnable(); raycastTarget=false; }
        void LateUpdate(){ if(Application.isPlaying)SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(!Application.isPlaying || !seguimiento || !seguimiento.isActiveAndEnabled || !seguimiento.CamaraActiva)return;
            var hands=seguimiento.UltimasManos; if(hands==null)return;
            var r=rectTransform.rect; bool mirror=!juego || juego.espejo;
            foreach(var h in hands)
            {
                if(h.puntos==null)continue;
                Vector2 P(int i){var p=h.puntos[i];float x=mirror?1-p.x:p.x;return new Vector2(r.xMin+x*r.width,r.yMax-p.y*r.height);}
                var line=new Color(colorPuntos.r,colorPuntos.g,colorPuntos.b,.7f);
                for(int b=0;b<Bones.GetLength(0);b++)Line(vh,P(Bones[b,0]),P(Bones[b,1]),tamano*.35f,line);
                for(int i=0;i<21;i++)Dot(vh,P(i),i==8?tamano*1.6f:tamano*.6f,i==8?colorIndice:colorPuntos);
            }
        }
        static void Dot(VertexHelper vh,Vector2 c,float rad,Color col)
        {
            int n=vh.currentVertCount; vh.AddVert(c,col,Vector2.zero);
            for(int i=0;i<=12;i++){float a=i*Mathf.PI*2/12;vh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*rad,col,Vector2.zero);}
            for(int i=1;i<=12;i++)vh.AddTriangle(n,n+i,n+i+1);
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float w,Color col)
        {
            var d=(b-a); if(d.sqrMagnitude<1e-4f)return; var nrm=new Vector2(-d.y,d.x).normalized*w*.5f;
            int n=vh.currentVertCount;
            vh.AddVert(a+nrm,col,Vector2.zero);vh.AddVert(b+nrm,col,Vector2.zero);vh.AddVert(b-nrm,col,Vector2.zero);vh.AddVert(a-nrm,col,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
