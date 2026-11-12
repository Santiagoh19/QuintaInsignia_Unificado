using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    // Pause menu opened with the "Menú" button (top right). Everything is chosen with the index finger (or the mouse):
    // Seguir jugando · Reiniciar · Cambiar dificultad · Volver al inicio. RecorridoGame decides what each one does.
    public sealed class MenuJuego : MonoBehaviour
    {
        public HandButton botonAbrir;
        [Tooltip("Fondo oscuro y tarjeta. Inactivo mientras el menú está cerrado.")]
        public GameObject panel;
        public HandButton seguir, reiniciar, dificultad, inicio;
        public Text textoReiniciar;
        [Tooltip("Sombra, borde y fondo de la tarjeta: se ajustan al número de botones visibles.")]
        public RectTransform[] marcos;
        public RectTransform cabecera;
        [Min(40)] public float separacion=80, alturaCabecera=96, margenInferior=26;

        public bool Abierto => panel && panel.activeSelf;

        public void Abrir(bool puedeReiniciar,bool puedeCambiarDificultad,string detalleReiniciar)
        {
            if(!panel)return;
            panel.SetActive(true);
            HandButton.Modal=panel.transform;
            if(reiniciar)reiniciar.gameObject.SetActive(puedeReiniciar);
            if(dificultad)dificultad.gameObject.SetActive(puedeCambiarDificultad);
            if(textoReiniciar)textoReiniciar.text=string.IsNullOrEmpty(detalleReiniciar)?"Reiniciar":"Reiniciar · "+detalleReiniciar;
            Ordenar();
            HandButton.ResetAll();
        }
        public void Cerrar()
        {
            if(panel)panel.SetActive(false);
            if(HandButton.Modal==(panel?panel.transform:null))HandButton.Modal=null;
            HandButton.ResetAll();
        }
        // Stacks the visible buttons and resizes the card around them.
        void Ordenar()
        {
            var botones=new[]{seguir,reiniciar,dificultad,inicio};
            int n=0; foreach(var b in botones)if(b && b.gameObject.activeSelf)n++;
            float alto=alturaCabecera+n*separacion+margenInferior;
            float y=alto*.5f-alturaCabecera-separacion*.5f+8;
            foreach(var b in botones)
            {
                if(!b || !b.gameObject.activeSelf)continue;
                var r=(RectTransform)b.transform; r.anchoredPosition=new Vector2(r.anchoredPosition.x,y); y-=separacion;
            }
            if(marcos!=null)foreach(var m in marcos)if(m)m.sizeDelta=new Vector2(m.sizeDelta.x,alto-(m.name=="Fondo"?10:0));
            if(cabecera)cabecera.anchoredPosition=new Vector2(cabecera.anchoredPosition.x,alto*.5f-6);
        }
        void OnDisable(){ if(panel && HandButton.Modal==panel.transform)HandButton.Modal=null; }
    }
}
