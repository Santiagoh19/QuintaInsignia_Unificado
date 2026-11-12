using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Burst;
using Unity.Collections;
using Unity.InferenceEngine;
using Unity.Jobs;
using UnityEngine;

namespace RecorridoAR
{
    // MediaPipe Hands inside Unity (no Python): palm detector (192x192) -> rotated hand crop -> 21 landmarks (224x224).
    // Same Google models as the Python version, run with Unity Inference Engine. Based on Unity's BlazeHand sample
    // (Unity-Technologies/sentis-samples, BlazeDetectionSample/Hand). Image preparation runs on the CPU so it works
    // on any PC; inference uses the GPU when available, otherwise the CPU.
    public sealed class ManoPipeline : IDisposable
    {
        public struct Mano { public bool derecha; public float presencia; public Vector2[] puntos; } // puntos: 0..1, y hacia abajo

        const int Anchors=2016, DetSize=192, LmSize=224;
        readonly float[,] anchors=new float[Anchors,4];
        readonly Worker detector, landmarker;
        readonly float[] detInput=new float[DetSize*DetSize*3];
        readonly float[] lmInput=new float[LmSize*LmSize*3];
        readonly string presenceName, handednessName;
        public float umbralDeteccion=.5f, umbralPresencia=.5f;
        public BackendType Backend { get; }
        public string Diagnostico { get; private set; }
        public float[] EntradaPuntos => lmInput;
        public float[] EntradaDetector => detInput;

        public ManoPipeline(ModelAsset detectorAsset,ModelAsset landmarkerAsset,string anchorsCsv,BackendType? backend=null)
        {
            var lines=anchorsCsv.Split('\n');
            for(int i=0;i<Anchors;i++){var v=lines[i].Split(',');for(int j=0;j<4;j++)anchors[i,j]=float.Parse(v[j],CultureInfo.InvariantCulture);}
            Backend=backend??BackendType.CPU; // measured: CPU (Burst) ~6 ms vs GPU ~18 ms per hand with immediate readback
            detector=new Worker(ModelLoader.Load(detectorAsset),Backend);
            var lm=ModelLoader.Load(landmarkerAsset);
            foreach(var o in lm.outputs){ if(o.name=="Identity_1")presenceName=o.name; if(o.name=="Identity_2")handednessName=o.name; }
            landmarker=new Worker(lm,Backend);
        }
        public void Dispose()
        {
            detector?.Dispose(); landmarker?.Dispose();
            if(nImg.IsCreated)nImg.Dispose();
            if(nOut.IsCreated)nOut.Dispose();
        }

        // 2x3 affine: (x,y) -> (a x + b y + c, d x + e y + f)
        struct Aff
        {
            public float a,b,c,d,e,f;
            public static Aff T(Vector2 t)=>new Aff{a=1,e=1,c=t.x,f=t.y};
            public static Aff S(float sx,float sy)=>new Aff{a=sx,e=sy};
            public static Aff R(float th)=>new Aff{a=Mathf.Cos(th),b=-Mathf.Sin(th),d=Mathf.Sin(th),e=Mathf.Cos(th)};
            public static Aff operator*(Aff p,Aff q)=>new Aff{a=p.a*q.a+p.b*q.d,b=p.a*q.b+p.b*q.e,c=p.a*q.c+p.b*q.f+p.c,
                d=p.d*q.a+p.e*q.d,e=p.d*q.b+p.e*q.e,f=p.d*q.c+p.e*q.f+p.f};
            public Vector2 Apply(Vector2 v)=>new Vector2(a*v.x+b*v.y+c,d*v.x+e*v.y+f);
        }

        // Image = centered 16:9 crop of the frame (as send_fingers_ar.py). Pixel rows of 'px' go bottom-up (GetPixels32).
        Color32[] img; int texW,texH,x0,y0,W,H; bool flipY;
        // Input range [0, 1] for both models (as Unity's sample).
        // The rotated, scaled crop is sampled by a Burst job on all CPU cores (about 1 ms instead of 14 ms).
        NativeArray<Color32> nImg; NativeArray<float> nOut;
        void CopyImage(Color32[] px)
        {
            if(!nImg.IsCreated || nImg.Length!=px.Length)
            { if(nImg.IsCreated)nImg.Dispose(); nImg=new NativeArray<Color32>(px.Length,Allocator.Persistent,NativeArrayOptions.UninitializedMemory); }
            nImg.CopyFrom(px);
        }
        void Sample(float[] dst,int n,Aff m,float mul,float add)
        {
            if(!nOut.IsCreated)nOut=new NativeArray<float>(LmSize*LmSize*3,Allocator.Persistent);
            var job=new SampleJob{img=nImg,texW=texW,texH=texH,x0=x0,y0=y0,W=W,H=H,flipY=flipY,
                a=m.a,b=m.b,c=m.c,d=m.d,e=m.e,f=m.f,mul=mul,add=add,n=n,dst=nOut};
            job.Schedule(n,8).Complete();
            NativeArray<float>.Copy(nOut,0,dst,0,n*n*3);
        }
        [BurstCompile]
        struct SampleJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> img;
            [NativeDisableParallelForRestriction] public NativeArray<float> dst;
            public int texW,texH,x0,y0,W,H,n; public bool flipY;
            public float a,b,c,d,e,f,mul,add;
            public void Execute(int y)
            {
                for(int x=0;x<n;x++)
                {
                    float px=a*x+b*y+c, py=d*x+e*y+f;
                    int o=(y*n+x)*3;
                    if(px<0||py<0||px>W||py>H){dst[o]=add;dst[o+1]=add;dst[o+2]=add;continue;}
                    float fx=x0+px-.5f, fy=y0+py-.5f;
                    int ix=(int)fx; if(fx<ix)ix--; int iy=(int)fy; if(fy<iy)iy--;
                    float tx=fx-ix, ty=fy-iy;
                    int xa=Clamp(ix,x0,x0+W-1), xb=Clamp(ix+1,x0,x0+W-1), ya=Clamp(iy,y0,y0+H-1), yb=Clamp(iy+1,y0,y0+H-1);
                    if(flipY){ya=texH-1-ya;yb=texH-1-yb;}
                    Color32 c00=img[ya*texW+xa], c10=img[ya*texW+xb], c01=img[yb*texW+xa], c11=img[yb*texW+xb];
                    float w00=(1-tx)*(1-ty), w10=tx*(1-ty), w01=(1-tx)*ty, w11=tx*ty, k=mul/255f;
                    dst[o]  =(c00.r*w00+c10.r*w10+c01.r*w01+c11.r*w11)*k+add;
                    dst[o+1]=(c00.g*w00+c10.g*w10+c01.g*w01+c11.g*w11)*k+add;
                    dst[o+2]=(c00.b*w00+c10.b*w10+c01.b*w01+c11.b*w11)*k+add;
                }
            }
            static int Clamp(int v,int lo,int hi)=>v<lo?lo:v>hi?hi:v;
        }
        static float Sigmoid(float v)=>1f/(1f+Mathf.Exp(-Mathf.Clamp(v,-100,100)));
        static float Prob(float v)=>v<0||v>1?Sigmoid(v):v;

        // Hand region in crop-image pixels (y up): centre, side and rotation, as MediaPipe's hand rect.
        // 'last' = real centre in the previous frame (for motion prediction), 'right' = smoothed handedness (-1 unknown).
        struct Roi { public Vector2 c, last; public float s, rot, right; public bool hasLast; }
        readonly List<Roi> tracked=new List<Roi>();
        [Tooltip("Como MediaPipe: tras encontrar la mano, el siguiente fotograma parte de sus puntos y no vuelve a buscar la palma.")]
        public bool seguimientoEntreFotogramas=true;
        public void ReiniciarSeguimiento(){ tracked.Clear(); }
        [Tooltip("Con una mano ya seguida, buscar otra mano solo cada N fotogramas (ahorra tiempo).")]
        [Min(1)] public int framesEntreBusquedas=5;
        [Tooltip("Anticipar el movimiento de la mano al recortar el siguiente fotograma (0 = sin anticipar).")]
        [Range(0,1)] public float anticipacion=.6f;
        int sinBuscar;
        [Tooltip("Activado: leer los puntos de la mano en el mismo fotograma (bloquea el juego unos ms). Desactivado (recomendado): sin bloquear; el juego va fluido incluso en PCs modestos.")]
        public bool lecturaInmediata=false;
        async Awaitable<Tensor<float>> Read(Tensor<float> t)
        {
            if(lecturaInmediata)return t.ReadbackAndClone();
            return await t.ReadbackAndCloneAsync();
        }
        double tS,tD,tL;

        public async Awaitable<List<Mano>> Run(Color32[] pixels,int width,int height,bool verticallyFlipped,int maxHands=2)
        {
            img=pixels;texW=width;texH=height;flipY=verticallyFlipped;
            CopyImage(pixels);
            // 16:9 centered crop, same as center_crop_to_aspect in Python.
            W=width;H=height;x0=y0=0;
            float aspect=W/(float)H;
            if(Mathf.Abs(aspect-16f/9f)>1e-3f)
            {
                if(aspect>16f/9f){int nw=(int)(H*16f/9f);x0=(W-nw)/2;W=nw;}
                else{int nh=(int)(W*9f/16f);y0=(H-nh)/2;H=nh;}
            }
            tS=tD=tL=0;
            var rois=new List<Roi>(seguimientoEntreFotogramas?tracked:new List<Roi>());
            tracked.Clear();
            var result=new List<Mano>();
            bool detected=false;
            if(rois.Count==0 || (rois.Count<maxHands && ++sinBuscar>=framesEntreBusquedas))
            {
                detected=true;sinBuscar=0;
                rois.AddRange(await Detect(rois,maxHands));
            }
            int lost=await Landmarks(rois,result);
            // A tracked hand was lost (fast movement): look for it again in THIS frame instead of leaving a gap.
            if(lost>0 && !detected && result.Count<maxHands)
            {
                detected=true;sinBuscar=0;
                var known=new List<Roi>(tracked);
                var found=await Detect(known,maxHands);
                await Landmarks(found,result);
            }
            Diagnostico=(detected?"detector + ":"seguimiento + ")+result.Count+" mano(s) · muestreo "+tS.ToString("0.0")+" ms, detector "+tD.ToString("0.0")+" ms, puntos "+tL.ToString("0.0")+" ms";
            img=null;
            return result;
        }

        // Palm detector on the letterboxed frame. Returns new hand rects not overlapping 'known'.
        async Awaitable<List<Roi>> Detect(List<Roi> known,int maxHands)
        {
            var found=new List<Roi>();
            int size=Mathf.Max(W,H); float scale=size/(float)DetSize;
            var M=Aff.T(.5f*(new Vector2(W,H)+new Vector2(-size,size)))*Aff.S(scale,-scale);
            var sw=System.Diagnostics.Stopwatch.StartNew();
            Sample(detInput,DetSize,M,1,0);tS+=sw.Elapsed.TotalMilliseconds;sw.Restart();
            using var detTensor=new Tensor<float>(new TensorShape(1,DetSize,DetSize,3),detInput);
            detector.Schedule(detTensor);
            // Waiting for the palm detector does not block the frame; once it is done the second output is ready.
            using var boxesT=await (detector.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync();
            using var scoresT=(detector.PeekOutput(1) as Tensor<float>).ReadbackAndClone();
            var boxes=boxesT.DownloadToArray(); var scores=scoresT.DownloadToArray();tD+=sw.Elapsed.TotalMilliseconds;
            int stride=boxes.Length/Anchors;
            // Candidates above threshold, best first, simple non-maximum suppression.
            var order=new List<int>();
            for(int i=0;i<Anchors;i++)if(Sigmoid(scores[i])>=umbralDeteccion)order.Add(i);
            order.Sort((p,q)=>scores[q].CompareTo(scores[p]));
            var chosen=new List<(Vector2 c,float s)>();
            foreach(int i in order)
            {
                if(known.Count+found.Count>=maxHands)break;
                var anchor=DetSize*new Vector2(anchors[i,0],anchors[i,1]);
                var c=anchor+new Vector2(boxes[i*stride],boxes[i*stride+1]);
                float s=Mathf.Max(boxes[i*stride+2],boxes[i*stride+3]);
                bool overlap=false;
                foreach(var k in chosen)if(IoU(c,s,k.c,k.s)>.3f){overlap=true;break;}
                if(overlap)continue;
                chosen.Add((c,s));
                // Rotated hand rect from the palm (Unity BlazeHand sample): wrist -> middle finger.
                var kp0=anchor+new Vector2(boxes[i*stride+4],boxes[i*stride+5]);
                var kp2=anchor+new Vector2(boxes[i*stride+8],boxes[i*stride+9]);
                var delta=kp2-kp0; var up=delta.normalized;
                float rotation=.5f*Mathf.PI-Mathf.Atan2(delta.y,delta.x);
                var centre=c+.5f*s*up;
                var roi=new Roi{c=M.Apply(centre),s=s*2.6f*scale,rot=rotation,right=-1};
                if(!Near(known,roi))found.Add(roi);
            }
            return found;
        }
        static bool Near(List<Roi> list,Roi roi){ foreach(var r in list)if(Vector2.Distance(r.c,roi.c)<.4f*Mathf.Max(r.s,roi.s))return true; return false; }

        // Hand landmarks for each rect. Adds hands to 'result', the next rects to 'tracked'. Returns how many were lost.
        async Awaitable<int> Landmarks(List<Roi> rois,List<Mano> result)
        {
            var origin=new Vector2(.5f*LmSize,.5f*LmSize);
            int lost=0;
            foreach(var roi in rois)
            {
                float k=roi.s/LmSize;
                var M2=Aff.T(roi.c)*Aff.S(k,k)*Aff.R(roi.rot)*Aff.T(-origin);
                var sw2=System.Diagnostics.Stopwatch.StartNew();
                Sample(lmInput,LmSize,M2,1,0);tS+=sw2.Elapsed.TotalMilliseconds;sw2.Restart();
                using var lmTensor=new Tensor<float>(new TensorShape(1,LmSize,LmSize,3),lmInput);
                landmarker.Schedule(lmTensor);
                using var lmOut=await Read(landmarker.PeekOutput("Identity") as Tensor<float>);
                float presence=1,right=1;
                if(presenceName!=null){using var p=(landmarker.PeekOutput(presenceName) as Tensor<float>).ReadbackAndClone();presence=Prob(p.DownloadToArray()[0]);}
                if(handednessName!=null){using var h=(landmarker.PeekOutput(handednessName) as Tensor<float>).ReadbackAndClone();right=Prob(h.DownloadToArray()[0]);}
                tL+=sw2.Elapsed.TotalMilliseconds;
                if(presence<umbralPresencia){lost++;continue;}
                var lm=lmOut.DownloadToArray();
                var img21=new Vector2[21]; var pts=new Vector2[21];
                for(int i=0;i<21;i++)
                {
                    img21[i]=M2.Apply(new Vector2(lm[3*i],lm[3*i+1]));   // crop pixels, y up
                    pts[i]=new Vector2(img21[i].x/W,1-img21[i].y/H);      // 0..1, y down (as MediaPipe)
                }
                var next=RoiFromLandmarks(img21);
                // Two rects that ended on the same hand: keep only the first.
                if(Near(tracked,next))continue;
                // Stable left/right label: smooth the classifier so one doubtful frame does not swap R1 <-> L1.
                next.right=roi.right<0?right:Mathf.Lerp(roi.right,right,.2f);
                // Predict where the hand will be in the next frame (it keeps moving the same way).
                var real=next.c;
                if(roi.hasLast && anticipacion>0)next.c=real+(real-roi.last)*anticipacion;
                next.last=real;next.hasLast=true;
                tracked.Add(next);
                result.Add(new Mano{derecha=next.right>.5f,presencia=presence,puntos=pts});
            }
            return lost;
        }
        // Next frame's hand rect from this frame's landmarks: aligned wrist -> middle finger, twice the extent.
        static Roi RoiFromLandmarks(Vector2[] p)
        {
            var d=p[9]-p[0];
            float rot=.5f*Mathf.PI+Mathf.Atan2(d.y,d.x);
            var u=d.normalized; var v=new Vector2(-u.y,u.x);
            float minU=1e9f,maxU=-1e9f,minV=1e9f,maxV=-1e9f;
            foreach(var q in p){float a=Vector2.Dot(q,u),b=Vector2.Dot(q,v);minU=Mathf.Min(minU,a);maxU=Mathf.Max(maxU,a);minV=Mathf.Min(minV,b);maxV=Mathf.Max(maxV,b);}
            var c=u*(.5f*(minU+maxU))+v*(.5f*(minV+maxV));
            float side=Mathf.Max(maxU-minU,maxV-minV)*2f;
            return new Roi{c=c,s=side,rot=rot};
        }
        static float IoU(Vector2 c1,float s1,Vector2 c2,float s2)
        {
            float ix=Mathf.Max(0,Mathf.Min(c1.x+s1/2,c2.x+s2/2)-Mathf.Max(c1.x-s1/2,c2.x-s2/2));
            float iy=Mathf.Max(0,Mathf.Min(c1.y+s1/2,c2.y+s2/2)-Mathf.Max(c1.y-s1/2,c2.y-s2/2));
            float inter=ix*iy; return inter/(s1*s1+s2*s2-inter+1e-6f);
        }
    }
}
