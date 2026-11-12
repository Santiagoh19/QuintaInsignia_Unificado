using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RecorridoAR.Editor
{
    // Generates the new intro PNGs from the ORIGINAL art (no redrawing of Lucas) and trims the wolf howl.
    // Menu: Ruta de Energía > Generar PNG nuevos y sonido del lobo. Output: Assets/RecorridoAR/Arte/Nuevas.
    public static class RutaEnergiaArte
    {
        const string Nuevas="Assets/RecorridoAR/Arte/Nuevas/";
        static readonly Color Ink=new Color(.145f,.302f,.196f,1);
        static readonly Color Cream=new Color(.99f,.97f,.925f,1);
        static readonly Color Gold=new Color(.957f,.788f,.365f,1);

        [MenuItem("Ruta de Energía/Generar PNG nuevos y sonido del lobo")]
        public static void Generar()
        {
            Directory.CreateDirectory(Nuevas);
            LucasCaminando();
            Nube();
            Exclamacion();
            LucasConLinterna();
            Insignia();
            MedallaNivel1();
            Logo();
            CopiarACarpetaDeImagenes();
            AssetDatabase.Refresh();
            RecortarLobo();
            ConfigurarTexturas();
            Debug.Log("Ruta de Energía: PNG nuevos y sonido del lobo generados.");
        }

        // ---------- Lucas caminando: the original drawing with legs rotated at the hips ----------
        struct Img { public int w,h; public Color[] px; }
        static Img Load(string path)
        {
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false);
            t.LoadImage(File.ReadAllBytes(path));
            var img=new Img{w=t.width,h=t.height,px=t.GetPixels()};
            Object.DestroyImmediate(t);
            return img;
        }
        // Top-based coordinates, bilinear, premultiplied internally.
        static Color Sample(Img s,float x,float yTop)
        {
            float y=s.h-1-yTop;
            int x0=Mathf.FloorToInt(x),y0=Mathf.FloorToInt(y);
            float fx=x-x0,fy=y-y0;
            Color c=Color.clear;
            for(int j=0;j<2;j++)for(int i=0;i<2;i++)
            {
                int xx=x0+i,yy=y0+j;
                if(xx<0||yy<0||xx>=s.w||yy>=s.h)continue;
                var p=s.px[yy*s.w+xx];
                float wgt=(i==0?1-fx:fx)*(j==0?1-fy:fy);
                c.r+=p.r*p.a*wgt;c.g+=p.g*p.a*wgt;c.b+=p.b*p.a*wgt;c.a+=p.a*wgt;
            }
            if(c.a>0){c.r/=c.a;c.g/=c.a;c.b/=c.a;}
            return c;
        }
        static Color Over(Color dst,Color src)
        {
            float a=src.a+dst.a*(1-src.a);
            if(a<=0)return Color.clear;
            return new Color((src.r*src.a+dst.r*dst.a*(1-src.a))/a,(src.g*src.a+dst.g*dst.a*(1-src.a))/a,(src.b*src.a+dst.b*dst.a*(1-src.a))/a,a);
        }
        static void LucasCaminando()
        {
            var src=Load("Assets/RecorridoAR/Arte/Usuario/lucas.png");
            // Measured on lucas.png (500 x 700): shorts hem ~y 525, legs split at x 265.
            const float legTop=505, bodyCut=528, split=265;
            var pivotL=new Vector2(200,legTop); var pivotR=new Vector2(330,legTop);
            float[,] frames={{13,-13,0},{0,0,7},{-13,13,0},{0,0,7}};
            for(int f=0;f<4;f++)
            {
                float aL=frames[f,0]*Mathf.Deg2Rad,aR=frames[f,1]*Mathf.Deg2Rad,bob=frames[f,2];
                var outPx=new Color[src.w*src.h];
                for(int yTop=0;yTop<src.h;yTop++)for(int x=0;x<src.w;x++)
                {
                    var p=new Vector2(x+.5f,yTop+.5f+bob);
                    Color c=Color.clear;
                    for(int leg=0;leg<2;leg++)
                    {
                        var piv=leg==0?pivotL:pivotR; float a=leg==0?aL:aR;
                        var d=p-piv; float cs=Mathf.Cos(-a),sn=Mathf.Sin(-a);
                        var q=piv+new Vector2(d.x*cs-d.y*sn,d.x*sn+d.y*cs);
                        if(q.y<legTop || (leg==0?q.x>=split:q.x<split))continue;
                        c=Over(c,Sample(src,q.x-.5f,q.y-.5f));
                    }
                    if(p.y<bodyCut)c=Over(c,Sample(src,p.x-.5f,p.y-.5f));
                    outPx[(src.h-1-yTop)*src.w+x]=c;
                }
                Save(Nuevas+"lucas_camina_"+(f+1)+".png",src.w,src.h,outPx);
            }
        }

        // ---------- Signed-distance drawing for the cloud and the exclamation badge ----------
        delegate float Sdf(Vector2 p);
        static void Paint(string path,int w,int h,Sdf shape,Color fill,Color stroke,float strokeW,Sdf inner=null,Color innerColor=default,Vector2 shadow=default)
        {
            var px=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                var p=new Vector2(x+.5f,y+.5f); // top-based
                float d=shape(p);
                Color c=Color.clear;
                if(shadow!=Vector2.zero)
                {
                    float ds=shape(p-shadow);
                    c=new Color(.08f,.16f,.12f,.22f*Mathf.Clamp01(.5f-ds/3f));
                }
                float outer=Mathf.Clamp01(.5f-d);
                if(outer>0)
                {
                    float fillMix=Mathf.Clamp01(.5f-(d+strokeW));
                    var col=Color.Lerp(stroke,fill,fillMix);
                    if(inner!=null){float di=inner(p);col=Color.Lerp(col,innerColor,Mathf.Clamp01(.5f-di));}
                    col.a=outer;
                    c=Over(c,col);
                }
                px[(h-1-y)*w+x]=c;
            }
            Save(path,w,h,px);
        }
        static float Circle(Vector2 p,Vector2 c,float r)=>Vector2.Distance(p,c)-r;
        static float Ellipse(Vector2 p,Vector2 c,Vector2 r)
        { var q=new Vector2((p.x-c.x)/r.x,(p.y-c.y)/r.y); return (q.magnitude-1)*Mathf.Min(r.x,r.y); }
        static float Capsule(Vector2 p,Vector2 a,Vector2 b,float r)
        { var pa=p-a; var ba=b-a; float h=Mathf.Clamp01(Vector2.Dot(pa,ba)/Vector2.Dot(ba,ba)); return (pa-ba*h).magnitude-r; }

        static void Nube()
        {
            // 1400 x 820: bumpy body upper right, three thought bubbles toward the lower left.
            var c=new Vector2(770,330); var r=new Vector2(560,235);
            var bumps=new List<Vector3>();
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI*2/12+.2f;
                bumps.Add(new Vector3(c.x+Mathf.Cos(a)*(r.x-120),c.y+Mathf.Sin(a)*(r.y-85),i%2==0?150:128));
            }
            Sdf body=p=>
            {
                float d=Ellipse(p,c,r-new Vector2(40,20));
                foreach(var b in bumps)d=Mathf.Min(d,Circle(p,new Vector2(b.x,b.y),b.z));
                return d;
            };
            Sdf all=p=>Mathf.Min(body(p),Mathf.Min(Circle(p,new Vector2(285,655),58),Mathf.Min(Circle(p,new Vector2(170,740),38),Circle(p,new Vector2(92,792),22))));
            Paint(Nuevas+"nube_pensamiento.png",1400,820,all,Cream,Ink,14,null,default,new Vector2(0,14));
        }
        static void Exclamacion()
        {
            Sdf badge=p=>Circle(p,new Vector2(130,124),110);
            Sdf mark=p=>Mathf.Min(Capsule(p,new Vector2(130,62),new Vector2(130,138),21),Circle(p,new Vector2(130,184),22));
            Paint(Nuevas+"signo_sorpresa.png",260,260,badge,Gold,Ink,12,mark,Ink,new Vector2(0,10));
        }
        // ---------- Lucas holding the lit flashlight (original lucas.png + linterna.png, no redrawing) ----------
        static void LucasConLinterna()
        {
            var lucas=Load("Assets/RecorridoAR/Arte/Usuario/lucas.png");    // 500 x 700
            var lamp=Load("Assets/RecorridoAR/Arte/Usuario/linterna.png");  // 400 x 300, lens on the left
            const int W=1100,H=700,lx=600;                                  // Lucas on the right, beam to the left
            const float ls=.55f;                                            // flashlight scale
            var lampRect=new Rect(lx+120-400*ls,388-300*ls*.5f,400*ls,300*ls); // tail at his hand, top-based
            var lens=new Vector2(lampRect.xMin+18,lampRect.center.y);
            var px=new Color[W*H];
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);
                Color c=Color.clear;
                // Warm beam: soft cone from the lens towards the left, plus a glow around the lens.
                var d=lens-p;
                if(d.x>0)
                {
                    float spread=Mathf.Abs(d.y)/(d.x*.42f+30);
                    float a=Mathf.Clamp01(1-spread)*Mathf.Clamp01(1-d.x/560f)*.55f;
                    c=Over(c,new Color(1,.92f,.55f,a*a*1.6f));
                }
                float g=Mathf.Clamp01(1-Vector2.Distance(p,lens)/70f);
                c=Over(c,new Color(1,.95f,.7f,g*g*.8f));
                c=Over(c,Sample(lucas,p.x-lx-.5f,p.y-.5f));
                if(lampRect.Contains(p))c=Over(c,Sample(lamp,(p.x-lampRect.xMin)/ls-.5f,(p.y-lampRect.yMin)/ls-.5f));
                px[(H-1-y)*W+x]=c;
            }
            Save(Nuevas+"lucas_linterna.png",W,H,px);
        }
        // ---------- Insignia Ruta de Energía: the supplied badge without its white background ----------
        const string InsigniaFuente="Documentacion/Arte_fuente/insignia_ruta_de_energia_original.png";
        public static void Insignia()
        {
            if(!File.Exists(InsigniaFuente)){Debug.LogWarning("Falta "+InsigniaFuente);return;}
            var src=Load(InsigniaFuente); int w=src.w,h=src.h; var px=src.px;
            // The supplied badge has its own transparency: keep it and only crop it square.
            if(px[0].a<.5f && px[px.Length-1].a<.5f){ CropSquare(px,w,h,InsigniaDestino); return; }
            bool White(Color c)=>Mathf.Min(c.r,Mathf.Min(c.g,c.b))>.88f && Mathf.Max(c.r,Mathf.Max(c.g,c.b))-Mathf.Min(c.r,Mathf.Min(c.g,c.b))<.08f;
            // Flood fill the white background from the image border.
            var bg=new bool[w*h]; var stack=new Stack<int>();
            for(int x=0;x<w;x++){stack.Push(x);stack.Push((h-1)*w+x);}
            for(int y=0;y<h;y++){stack.Push(y*w);stack.Push(y*w+w-1);}
            while(stack.Count>0)
            {
                int i=stack.Pop(); if(bg[i]||!White(px[i]))continue; bg[i]=true;
                int x=i%w,y=i/w;
                if(x>0)stack.Push(i-1); if(x<w-1)stack.Push(i+1); if(y>0)stack.Push(i-w); if(y<h-1)stack.Push(i+w);
            }
            // Distance (up to 3 px) from the background, for the soft edge.
            var dist=new int[w*h]; for(int i=0;i<w*h;i++)dist[i]=bg[i]?0:99;
            for(int pass=1;pass<=3;pass++)for(int i=0;i<w*h;i++)
            {
                if(dist[i]!=99)continue; int x=i%w,y=i/w;
                if((x>0&&dist[i-1]==pass-1)||(x<w-1&&dist[i+1]==pass-1)||(y>0&&dist[i-w]==pass-1)||(y<h-1&&dist[i+w]==pass-1))dist[i]=pass;
            }
            var outPx=new Color[w*h];
            for(int i=0;i<w*h;i++)
            {
                if(bg[i]){outPx[i]=new Color(1,1,1,0);continue;}
                var c=px[i]; float a=1;
                if(dist[i]<=3)
                {   // edge pixel blended with white: estimate coverage from the blue channel (gold has little blue)
                    a=Mathf.Clamp01((1-c.b)/.82f);
                    if(a>.02f){c.r=Mathf.Clamp01((c.r-(1-a))/a);c.g=Mathf.Clamp01((c.g-(1-a))/a);c.b=Mathf.Clamp01((c.b-(1-a))/a);}
                }
                c.a=a; outPx[i]=c;
            }
            CropSquare(outPx,w,h,InsigniaDestino);
        }
        const string InsigniaDestino="Assets/RecorridoAR/Arte/Usuario/insignia_ruta_de_energia.png";
        // ---------- Level-1 medal (Medalla dorada de campamento): supplied with transparency, only cropped square ----------
        const string MedallaFuente="Documentacion/Arte_fuente/medalla_nivel_1_original.png";
        public const string MedallaDestino="Assets/RecorridoAR/Arte/Usuario/medalla_nivel_1.png";
        public static void MedallaNivel1()
        {
            if(!File.Exists(MedallaFuente)){Debug.LogWarning("Falta "+MedallaFuente);return;}
            var src=Load(MedallaFuente);
            CropSquare(src.px,src.w,src.h,MedallaDestino);
        }
        static void CropSquare(Color[] outPx,int w,int h,string destino)
        {
            int x0=w,y0=h,x1=-1,y1=-1;
            for(int i=0;i<w*h;i++)if(outPx[i].a>.05f){int x=i%w,y=i/w;x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y);}
            // Square crop around the coin with a small margin.
            int side=Mathf.Max(x1-x0,y1-y0)+12, cx=(x0+x1)/2, cy=(y0+y1)/2, ox=cx-side/2, oy=cy-side/2;
            var crop=new Color[side*side];
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                int sx=ox+x,sy=oy+y;
                crop[y*side+x]=(sx<0||sy<0||sx>=w||sy>=h)?new Color(1,1,1,0):outPx[sy*w+sx];
            }
            Save(destino,side,side,crop);
            Debug.Log(Path.GetFileName(destino)+": "+side+"x"+side+" px sin fondo");
        }

        static void Save(string path,int w,int h,Color[] px)
        {
            var t=new Texture2D(w,h,TextureFormat.RGBA32,false);
            t.SetPixels(px);t.Apply();
            File.WriteAllBytes(path,t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        // ---------- Quinta Insignia logo, 2.4x the original titulo.png, same layout and colours ----------
        // Text in Titan One (Figma typography); triangle and pentagon redrawn as smooth shapes.
        const int LW=2400,LH=480; const float LS=2.4f;
        static void Logo()
        {
            var src=Load("Assets/RecorridoAR/Arte/Usuario/titulo.png");
            bg=Color.white; // titulo.png: opaque white plate with transparent corners
            var textBox=Box(src,200,800); var tri=Box(src,0,200); var pent=Box(src,800,src.w);
            int split=FindGap(src,textBox);
            var cQ=Avg(src,new RectInt(textBox.xMin,textBox.yMin,split-textBox.xMin,textBox.height));
            var cI=Avg(src,new RectInt(split,textBox.yMin,textBox.xMax-split,textBox.height));
            var cT=Avg(src,tri); var cP=Avg(src,pent);
            var target=new Rect(textBox.x*LS,textBox.y*LS,textBox.width*LS,textBox.height*LS);
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/RecorridoAR/Arte/TitanOne.ttf");
            string rich="<color=#"+ColorUtility.ToHtmlStringRGB(cQ)+">Quinta</color> <color=#"+ColorUtility.ToHtmlStringRGB(cI)+">Insignia</color>";
            int size=200; Color[] text=null; RectInt tb=default;
            for(int pass=0;pass<3;pass++)
            {
                text=RenderText(rich,font,size);tb=AlphaBox(text,LW,LH);
                if(tb.width<=0)break;
                size=Mathf.RoundToInt(size*target.width/tb.width);
            }
            var outPx=new Color[LW*LH];
            int dx=Mathf.RoundToInt(target.x-tb.x), dy=Mathf.RoundToInt(target.y-tb.y);
            for(int y=0;y<LH;y++)for(int x=0;x<LW;x++)
            {
                int sx=x-dx,sy=y-dy; if(sx<0||sy<0||sx>=LW||sy>=LH)continue;
                outPx[(LH-1-y)*LW+x]=text[(LH-1-sy)*LW+sx];
            }
            Shape(outPx,Scale(tri),3,cT,Mathf.PI/2);
            Shape(outPx,Scale(pent),5,cP,Mathf.PI/2);
            Save(Nuevas+"logo_quinta_insignia.png",LW,LH,outPx);
            Debug.Log("Logo: texto "+ColorUtility.ToHtmlStringRGB(cQ)+"/"+ColorUtility.ToHtmlStringRGB(cI)+" tamaño "+size+" caja "+textBox);
        }
        // titulo.png has an opaque white background: "ink" = pixels clearly different from the corner colour.
        static Color bg;
        static float Diff(Color p)=>p.a<.5f?0:Mathf.Abs(p.r-bg.r)+Mathf.Abs(p.g-bg.g)+Mathf.Abs(p.b-bg.b);
        static bool IsInk(Img s,int x,int y,float min=.3f)=>Diff(s.px[(s.h-1-y)*s.w+x])>min;
        // Top-based box of ink pixels between two columns.
        static RectInt Box(Img s,int xa,int xb)
        {
            int x0=9999,y0=9999,x1=-1,y1=-1;
            for(int y=0;y<s.h;y++)for(int x=xa;x<xb;x++)if(IsInk(s,x,y)){x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y);}
            return new RectInt(x0,y0,x1-x0+1,y1-y0+1);
        }
        // Average of solid ink pixels (edges excluded).
        static Color Avg(Img s,RectInt r)
        {
            float max=0;
            for(int y=r.yMin;y<r.yMax;y++)for(int x=r.xMin;x<r.xMax;x++)max=Mathf.Max(max,Diff(s.px[(s.h-1-y)*s.w+x]));
            Color c=Color.clear;int n=0;
            for(int y=r.yMin;y<r.yMax;y++)for(int x=r.xMin;x<r.xMax;x++){var p=s.px[(s.h-1-y)*s.w+x];if(Diff(p)>max*.9f){c+=p;n++;}}
            if(n==0)return Color.black; c/=n; c.a=1; return c;
        }
        static Rect Scale(RectInt r)=>new Rect(r.x*LS,r.y*LS,r.width*LS,r.height*LS);
        static int FindGap(Img s,RectInt b)
        {   // widest empty column run inside the text box = space between the words
            int best=b.xMin+b.width/2,run=0,bestRun=0;
            for(int x=b.xMin;x<b.xMax;x++)
            {
                bool empty=true;for(int y=b.yMin;y<b.yMax;y++)if(IsInk(s,x,y,.15f)){empty=false;break;}
                run=empty?run+1:0; if(run>bestRun){bestRun=run;best=x;}
            }
            return best;
        }
        static Color[] RenderText(string rich,Font font,int size)
        {
            var camGo=new GameObject("Camara_logo"); var cam=camGo.AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(0,0,0,0);cam.orthographic=true;
            var rt=new RenderTexture(LW,LH,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;cam.aspect=LW/(float)LH;
            var cgo=new GameObject("Lienzo_logo",typeof(Canvas));var canvas=cgo.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=5;
            var tgo=new GameObject("Texto",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
            tgo.transform.SetParent(cgo.transform,false);
            var r=tgo.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            var t=tgo.GetComponent<Text>();t.font=font;t.fontSize=size;t.supportRichText=true;t.text=rich;
            t.alignment=TextAnchor.MiddleCenter;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;
            Canvas.ForceUpdateCanvases();t.SetAllDirty();t.Rebuild(CanvasUpdate.PreRender);Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active=rt;var tex=new Texture2D(LW,LH,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,LW,LH),0,0);tex.Apply();RenderTexture.active=null;
            var px=tex.GetPixels();
            for(int i=0;i<px.Length;i++){var c=px[i];if(c.a>0){c.r=Mathf.Clamp01(c.r/c.a);c.g=Mathf.Clamp01(c.g/c.a);c.b=Mathf.Clamp01(c.b/c.a);}px[i]=c;} // un-premultiply
            Object.DestroyImmediate(tex);cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cgo);Object.DestroyImmediate(camGo);
            return px;
        }
        static RectInt AlphaBox(Color[] px,int w,int h)
        {
            int x0=w,y0=h,x1=-1,y1=-1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(px[(h-1-y)*w+x].a>.5f){x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y);}
            return x1<0?default:new RectInt(x0,y0,x1-x0+1,y1-y0+1);
        }
        // Regular polygon (point up) filling the measured box, slightly rounded, antialiased. Top-based coordinates.
        static void Shape(Color[] px,Rect box,int sides,Color color,float start)
        {
            float R=sides==3?box.height*2f/3f:box.height/(1+Mathf.Cos(Mathf.PI/sides));
            var c=new Vector2(box.center.x,box.yMin+R);
            float round=R*.12f; var v=new Vector2[sides];
            for(int i=0;i<sides;i++){float a=start+i*2*Mathf.PI/sides;v[i]=c+new Vector2(Mathf.Cos(a),-Mathf.Sin(a))*(R-round);}
            for(int y=(int)box.yMin-8;y<box.yMax+8;y++)for(int x=(int)box.xMin-8;x<box.xMax+8;x++)
            {
                if(x<0||y<0||x>=LW||y>=LH)continue;
                var p=new Vector2(x+.5f,y+.5f); float d=-1e9f;
                for(int i=0;i<sides;i++){var a=v[i];var b=v[(i+1)%sides];var e=(b-a).normalized;var n=new Vector2(e.y,-e.x);if(Vector2.Dot(n,c-a)>0)n=-n;d=Mathf.Max(d,Vector2.Dot(p-a,n));}
                float alpha=Mathf.Clamp01(.5f-(d-round));
                if(alpha<=0)continue;
                int idx=(LH-1-y)*LW+x; var col=color;col.a=alpha; px[idx]=Over(px[idx],col);
            }
        }

        // New PNGs are also copied to the shared images folder, next to the originals.
        static readonly string CarpetaImagenes=Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),"Quinta_Insignia","Imagenes","imagenes_quinta_insignia","nuevas_ruta_de_energia");
        static void CopiarACarpetaDeImagenes()
        {
            if(!Directory.Exists(Path.GetDirectoryName(CarpetaImagenes)))return;
            Directory.CreateDirectory(CarpetaImagenes);
            foreach(var f in Directory.GetFiles(Nuevas,"*.png"))File.Copy(f,Path.Combine(CarpetaImagenes,Path.GetFileName(f)),true);
            var ins="Assets/RecorridoAR/Arte/Usuario/insignia_ruta_de_energia.png";
            if(File.Exists(ins))File.Copy(ins,Path.Combine(CarpetaImagenes,"insignia_ruta_de_energia.png"),true);
        }

        // ---------- Wolf howl (U.S. National Park Service, public domain) ----------
        const string WolfSource="Documentacion/Audio/fuentes/nps_lobo_denali.mp3";
        static void RecortarLobo()
        {
            if(!File.Exists(WolfSource)){Debug.LogWarning("Falta "+WolfSource);return;}
            const string tmp="Assets/RecorridoAR/Audio/_tmp_lobo.mp3";
            File.Copy(WolfSource,tmp,true);
            AssetDatabase.ImportAsset(tmp,ImportAssetOptions.ForceSynchronousImport);
            var imp=(AudioImporter)AssetImporter.GetAtPath(tmp);
            var s=imp.defaultSampleSettings;s.loadType=AudioClipLoadType.DecompressOnLoad;s.compressionFormat=AudioCompressionFormat.PCM;
            imp.defaultSampleSettings=s;imp.loadInBackground=false;imp.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(tmp);
            clip.LoadAudioData();
            var data=new float[clip.samples*clip.channels];
            clip.GetData(data,0);
            int ch=clip.channels,rate=clip.frequency,n=clip.samples;
            var mono=new float[n];
            for(int i=0;i<n;i++){float v=0;for(int k=0;k<ch;k++)v+=data[i*ch+k];mono[i]=v/ch;}
            // Loudest 2.8 s window (the howl), with gentle fades.
            int win=Mathf.Min(n,(int)(2.8f*rate)),hop=rate/20,best=0;double bestE=-1;
            for(int st=0;st+win<=n;st+=hop){double e=0;for(int i=st;i<st+win;i+=8)e+=mono[i]*mono[i];if(e>bestE){bestE=e;best=st;}}
            var cut=new float[win];float peak=1e-6f;
            for(int i=0;i<win;i++){cut[i]=mono[best+i];peak=Mathf.Max(peak,Mathf.Abs(cut[i]));}
            int fin=(int)(.03f*rate),fout=(int)(.7f*rate);
            for(int i=0;i<win;i++)
            {
                float g=.75f/peak;
                if(i<fin)g*=i/(float)fin;
                if(i>win-fout)g*=(win-i)/(float)fout;
                cut[i]*=g;
            }
            WriteWav("Assets/RecorridoAR/Audio/sfx_lobo_aullido.wav",cut,rate);
            AssetDatabase.DeleteAsset(tmp);
            AssetDatabase.ImportAsset("Assets/RecorridoAR/Audio/sfx_lobo_aullido.wav");
            Debug.Log("Aullido: "+n/(float)rate+" s de origen; recorte desde "+best/(float)rate+" s");
        }
        static void WriteWav(string path,float[] s,int rate)
        {
            using var fs=new FileStream(path,FileMode.Create);
            using var w=new BinaryWriter(fs);
            w.Write("RIFF".ToCharArray());w.Write(36+s.Length*2);w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray());w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
            w.Write("data".ToCharArray());w.Write(s.Length*2);
            foreach(var v in s)w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v,-1,1)*32767));
        }

        // ---------- Import settings: sharp when drawn smaller than the source ----------
        // Mipmaps + trilinear remove the jagged look of large PNGs drawn at a smaller size.
        public static void ConfigurarTexturas()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/RecorridoAR/Arte"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                if(AssetImporter.GetAtPath(path) is not TextureImporter ti)continue;
                ti.mipmapEnabled=true;
                ti.mipmapFilter=TextureImporterMipFilter.KaiserFilter;
                ti.filterMode=FilterMode.Trilinear;
                ti.anisoLevel=1;
                ti.alphaIsTransparency=true;
                ti.wrapMode=TextureWrapMode.Clamp;
                ti.npotScale=TextureImporterNPOTScale.None;
                ti.textureCompression=TextureImporterCompression.Uncompressed;
                // The medals are drawn small: 512 px keeps them sharp and light.
                ti.maxTextureSize=path.EndsWith("medalla_nivel_1.png")?512:path.EndsWith("insignia_ruta_de_energia.png")?1024:4096;
                ti.isReadable=false;
                ti.SaveAndReimport();
            }
        }
    }
}
