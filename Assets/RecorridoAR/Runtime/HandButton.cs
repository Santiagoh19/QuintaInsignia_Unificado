using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace RecorridoAR
{
    // A button confirmed by holding the index over it (same DwellGate as Jugar). The mouse also works.
    // RecorridoGame feeds the tracked index to every active HandButton outside the route.
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class HandButton : MonoBehaviour, IPointerClickHandler
    {
        public static readonly List<HandButton> Active=new List<HandButton>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){ Active.Clear(); Modal=null; }
        // While set (the pause menu), only buttons inside it react to the finger.
        public static Transform Modal;
        bool Allowed => !Modal || transform.IsChildOf(Modal);

        public UnityEvent alConfirmar=new UnityEvent();
        [Tooltip("Imagen opcional (Filled) que muestra el avance de la permanencia.")]
        public Image relleno;
        [Tooltip("Margen interior en unidades del tablero para no activar el botón por el borde.")]
        [Min(0)] public float margen=6;
        public bool sonidoAlConfirmar=true;
        readonly DwellGate gate=new DwellGate();
        public float Progress => gate.Fired?0:gate.Progress;
        RectTransform Rect => (RectTransform)transform;

        void OnEnable(){ if(!Active.Contains(this))Active.Add(this); ResetHold(); }
        void OnDisable(){ Active.Remove(this); ResetHold(); }
        public void ResetHold(){ gate.Reset(); if(relleno)relleno.fillAmount=0; }
        public bool Contains(Vector3 world)
        {
            Vector2 local=Rect.InverseTransformPoint(world);
            var r=Rect.rect; r.xMin+=margen; r.xMax-=margen; r.yMin+=margen; r.yMax-=margen;
            return r.Contains(local);
        }
        public bool Feed(bool inside,float seconds,float duration)
        {
            gate.Duration=duration;
            bool fired=gate.Sample(inside,seconds);
            if(relleno)relleno.fillAmount=Progress;
            return fired;
        }
        public void Confirm()
        {
            if(!isActiveAndEnabled)return;
            if(relleno)relleno.fillAmount=0;
            if(sonidoAlConfirmar)AudioDirector.Play(AudioDirector.Efecto.Boton);
            alConfirmar.Invoke();
        }
        public void OnPointerClick(PointerEventData eventData){ if(Allowed)Confirm(); }

        // Feeds one fresh index sample. At most one button fires per sample.
        public static float FeedAll(Vector3 world,bool valid,float seconds,float duration)
        {
            float best=0; HandButton fired=null;
            for(int i=0;i<Active.Count;i++)
            {
                var b=Active[i];
                if(!b || !b.isActiveAndEnabled)continue;
                if(!b.Allowed){b.ResetHold();continue;}
                if(b.Feed(valid && b.Contains(world),seconds,duration) && fired==null)fired=b;
                best=Mathf.Max(best,b.Progress);
            }
            if(fired)fired.Confirm();
            return best;
        }
        // True when the point is over any button that can react now.
        public static bool AnyContains(Vector3 world)
        {
            for(int i=0;i<Active.Count;i++){var b=Active[i];if(b && b.isActiveAndEnabled && b.Allowed && b.Contains(world))return true;}
            return false;
        }
        public static void ResetAll(){ for(int i=0;i<Active.Count;i++)if(Active[i])Active[i].ResetHold(); }
    }
}
