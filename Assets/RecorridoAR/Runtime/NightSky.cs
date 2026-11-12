using UnityEngine;
namespace RecorridoAR
{
    // Night veil over the sunset illustration. Its children (stars) twinkle while it is visible.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class NightSky : MonoBehaviour
    {
        [Range(0,1)] public float objetivo;
        [Min(.05f)] public float velocidad=1.4f;
        public RectTransform estrellas;
        CanvasGroup group;
        public float Alpha => Group.alpha;
        CanvasGroup Group { get { if(!group)group=GetComponent<CanvasGroup>(); return group; } }
        public void Set(float value,bool immediate=false)
        {
            objetivo=Mathf.Clamp01(value);
            if(immediate)Group.alpha=objetivo;
        }
        void OnEnable(){ Group.blocksRaycasts=false; Group.interactable=false; }
        void Update()
        {
            Group.alpha=Mathf.MoveTowards(Group.alpha,objetivo,velocidad*Time.unscaledDeltaTime);
            if(!estrellas || Group.alpha<=0)return;
            float t=Time.unscaledTime;
            for(int i=0;i<estrellas.childCount;i++)
            {
                var star=estrellas.GetChild(i);
                float s=.75f+.25f*Mathf.Sin(t*(1.3f+i*.37f)+i*1.7f);
                star.localScale=new Vector3(s,s,1);
            }
        }
    }
}
