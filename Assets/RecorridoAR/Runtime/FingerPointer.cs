using UnityEngine;
using UnityEngine.UI;
namespace RecorridoAR
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FingerPointer : MaskableGraphic
    {
        [System.NonSerialized] public bool visible;
        [System.NonSerialized] public Vector2 position;
        [System.NonSerialized] public float progress;
        [System.NonSerialized] public bool playing;
        RectTransform visual;
        Image hold;
        Texture2D diskTexture;
        Sprite diskSprite;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget=false;
            maskable=false;
            if(Application.isPlaying) EnsureVisual();
        }
        void EnsureVisual()
        {
            if(visual)return;
            // Standard UI Images keep valid geometry even before the first tracking sample.
            // A separate sorting canvas prevents the menu and camera frame covering the guide.
            var root=new GameObject("Guia_rosa_visible",typeof(RectTransform),typeof(Canvas));
            root.hideFlags=HideFlags.DontSave;
            visual=root.GetComponent<RectTransform>();
            visual.SetParent(rectTransform,false);
            visual.anchorMin=visual.anchorMax=visual.pivot=new Vector2(.5f,.5f);
            visual.sizeDelta=new Vector2(56,56);
            var layer=root.GetComponent<Canvas>();
            layer.overrideSorting=true;layer.sortingOrder=32000;
            const int size=128;
            diskTexture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            diskTexture.name="Disco_guia_rosa";diskTexture.hideFlags=HideFlags.DontSave;
            diskTexture.filterMode=FilterMode.Bilinear;diskTexture.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=x+.5f-size*.5f,dy=y+.5f-size*.5f;
                float alpha=Mathf.Clamp01((size*.5f-1-Mathf.Sqrt(dx*dx+dy*dy))/2);
                pixels[y*size+x]=new Color(1,1,1,alpha);
            }
            diskTexture.SetPixels(pixels);diskTexture.Apply(false,false);
            diskSprite=Sprite.Create(diskTexture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            diskSprite.hideFlags=HideFlags.DontSave;
            Circle("Halo",56,new Color(1,.08f,.40f,.25f));
            hold=Circle("Permanencia",50,new Color(1,.80f,.26f,1));
            hold.type=Image.Type.Filled;hold.fillMethod=Image.FillMethod.Radial360;
            hold.fillOrigin=2;hold.fillClockwise=true;
            Circle("Contorno",42,new Color(.13f,.20f,.25f,1));
            Circle("Borde_blanco",36,Color.white);
            Circle("Punto_rosa",28,new Color(1,.08f,.40f,1));
            root.SetActive(false);
        }
        Image Circle(string name,float diameter,Color tint)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var rect=go.GetComponent<RectTransform>();rect.SetParent(visual,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(diameter,diameter);
            var image=go.GetComponent<Image>();image.sprite=diskSprite;image.color=tint;
            image.raycastTarget=false;image.maskable=false;
            return image;
        }
        void LateUpdate()
        {
            if(!Application.isPlaying)return;
            EnsureVisual();
            visual.anchoredPosition=new Vector2(position.x-640,360-position.y);
            hold.fillAmount=Mathf.Clamp01(progress);
            visual.gameObject.SetActive(visible);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        { vh.Clear(); } // Retain the serialized component; its child Images render the guide.
        protected override void OnDisable()
        {
            if(visual)visual.gameObject.SetActive(false);
            base.OnDisable();
        }
        protected override void OnDestroy()
        {
            if(visual)Release(visual.gameObject);
            if(diskSprite)Release(diskSprite);
            if(diskTexture)Release(diskTexture);
            base.OnDestroy();
        }
        static void Release(Object item)
        {if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
    }
}
