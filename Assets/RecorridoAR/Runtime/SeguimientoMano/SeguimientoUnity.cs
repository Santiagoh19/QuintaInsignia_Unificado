using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

namespace RecorridoAR
{
    // Webcam + MediaPipe Hands inside Unity. Produces the SAME packets as send_fingers_ar.py
    // ({"fingers":[{"id":"R1","x":..,"y":..,"isUp":true}],"countL","countR"}), so the game logic does not change.
    // No Python, no BAT: this lets the game run on any Windows PC with a webcam.
    public sealed class SeguimientoUnity : MonoBehaviour
    {
        [Header("Modelos MediaPipe Hands (Apache 2.0)")]
        public ModelAsset detectorPalma;
        public ModelAsset puntosMano;
        public TextAsset anclas;
        [Header("Cámara")]
        [Tooltip("Vacío: la primera cámara real del equipo (las cámaras virtuales se prueban al final).")]
        public string nombreCamara="";
        [Tooltip("Segundos sin imagen antes de probar otra cámara o volver a intentarlo.")]
        [Min(1)] public float esperaSinImagen=3f;
        public int ancho=1280, alto=720, fps=30;
        [Tooltip("Ejecutar los modelos en la GPU. Desactivado (recomendado): CPU con Burst, más rápido para estos modelos y válido en cualquier PC.")]
        public bool usarGPU=false;
        [Header("Igual que send_fingers_ar.py")]
        [Range(0,1)] public float confianzaDeteccion=.5f;
        [Range(0,1)] public float confianzaSeguimiento=.5f;
        public bool intercambiarManos=false;
        public bool invertirPulgarDerecho=true, invertirPulgarIzquierdo=true;
        public float toleranciaY=.01f;
        public int framesSubir=2, framesBajar=2;
        [Tooltip("Si la mano desaparece solo un instante (desenfoque, movimiento rápido), se mantiene la última posición este tiempo en segundos.")]
        [Range(0,.4f)] public float toleranciaPerdida=.15f;
        [Header("Suavizado del índice (filtro One Euro)")]
        public bool suavizar=true;
        [Min(.01f)] public float minCutoff=1.2f;
        [Min(0)] public float beta=.03f;

        public RecorridoTransport.Sample LatestSample { get; private set; }
        // Last landmarks (0..1, y down, 16:9 crop) for the preview in Tu cámara.
        public List<ManoPipeline.Mano> UltimasManos { get; private set; }=new List<ManoPipeline.Mano>();
        Packet lastWithHands; double lastHandsTime;
        public Texture Vista => cam;
        public bool CamaraActiva => cam && cam.isPlaying && cam.width>16 && fotogramasCamara>0;
        public string Error { get; private set; }
        // Short message for "Tu cámara" while there is no image.
        public string Estado => Error ?? (pipeline==null?"Preparando cámara…":CamaraActiva?"Cámara lista":estadoCamara);
        // Warning shown over the image (for example, too dark).
        public string Aviso { get; private set; }
        public string NombreCamara => cam?cam.deviceName:"";

        WebCamTexture cam;
        List<string> candidatas=new List<string>();
        int indiceCamara=-1, fotogramasCamara;
        float inicioCamara, ultimoFotograma=-1, siguienteIntento;
        string estadoCamara="Abriendo cámara…";
        ManoPipeline pipeline;
        Color32[] buffer;
        bool busy; int analizados;
        static readonly int[] Tips={4,8,12,16,20}, Pips={3,6,10,14,18};
        readonly Dictionary<string,int[]> up=new Dictionary<string,int[]>(), down=new Dictionary<string,int[]>();
        readonly Dictionary<string,bool[]> state=new Dictionary<string,bool[]>();
        readonly Dictionary<string,OneEuro2> filters=new Dictionary<string,OneEuro2>();

        void Start()
        {
            // Automated tests run without a camera (they send UDP packets like Python).
            if(Application.isBatchMode){Error="Sin cámara en modo batch";enabled=false;return;}
            Application.runInBackground=true;
            try
            {
                pipeline=new ManoPipeline(detectorPalma,puntosMano,anclas.text,usarGPU && SystemInfo.supportsComputeShaders?BackendType.GPUCompute:BackendType.CPU){umbralDeteccion=confianzaDeteccion,umbralPresencia=confianzaSeguimiento};
            }
            catch(Exception e){Error="No se pudo preparar el seguimiento de la mano";Debug.LogException(e);return;}
            AbrirSiguiente();
        }

        static readonly string[] Virtuales={"virtual","obs","ndi","snap camera","xsplit","manycam","splitcam","vcam","camo","epoccam","broadcast"};
        // Real cameras first (in the system order), the preferred name before all, virtual cameras last.
        public static List<string> OrdenarCamaras(IEnumerable<string> names,string preferida)
        {
            var reales=new List<string>(); var virtuales=new List<string>();
            foreach(var n in names)
            {
                if(string.IsNullOrEmpty(n))continue;
                string low=n.ToLowerInvariant(); bool v=false;
                foreach(var k in Virtuales)if(low.Contains(k)){v=true;break;}
                (v?virtuales:reales).Add(n);
            }
            reales.AddRange(virtuales);
            if(!string.IsNullOrWhiteSpace(preferida))
            {
                int i=reales.FindIndex(n=>n.IndexOf(preferida.Trim(),StringComparison.OrdinalIgnoreCase)>=0);
                if(i>0){var p=reales[i];reales.RemoveAt(i);reales.Insert(0,p);}
            }
            return reales;
        }
        void CerrarCamara()
        {
            if(cam){cam.Stop();Destroy(cam);}
            cam=null;fotogramasCamara=0;ultimoFotograma=-1;
            pipeline?.ReiniciarSeguimiento();
        }
        void AbrirSiguiente()
        {
            CerrarCamara();
            if(indiceCamara+1>=candidatas.Count)
            {
                var names=new List<string>();
                foreach(var d in WebCamTexture.devices)names.Add(d.name);
                candidatas=OrdenarCamaras(names,nombreCamara);indiceCamara=-1;
                if(candidatas.Count==0)
                {
                    estadoCamara="Conecta una cámara web";
                    siguienteIntento=Time.unscaledTime+2;   // hot-plug: look again in 2 s
                    return;
                }
            }
            indiceCamara++;
            string device=candidatas[indiceCamara];
            try
            {
                cam=new WebCamTexture(device,ancho,alto,fps);
                cam.Play();
                estadoCamara="Abriendo cámara…";
                Debug.Log("Seguimiento Unity: cámara \""+device+"\" ("+candidatas.Count+" disponibles)");
            }
            catch(Exception e){Debug.LogWarning("No se pudo abrir "+device+": "+e.Message);cam=null;siguienteIntento=Time.unscaledTime+1;}
            inicioCamara=Time.unscaledTime;
        }
        void Update()
        {
            if(pipeline==null)return;
            float now=Time.unscaledTime;
            if(!cam){ if(now>=siguienteIntento)AbrirSiguiente(); return; }
            bool fresh=cam.didUpdateThisFrame && cam.width>16;
            if(fresh){ultimoFotograma=now;fotogramasCamara++;}
            // Watchdog: a camera that delivers no image (busy in another app, unplugged, broken driver)
            // is closed and the next one is tried; when all fail, try again from the start.
            float silent=ultimoFotograma<0?now-inicioCamara:now-ultimoFotograma;
            if(silent>(ultimoFotograma<0?esperaSinImagen+2:esperaSinImagen))
            {
                bool last=indiceCamara+1>=candidatas.Count;
                estadoCamara=last?"La cámara está ocupada o desconectada.\nCierra otras apps que la usen.":"Probando otra cámara…";
                Debug.LogWarning("Seguimiento Unity: sin imagen de \""+NombreCamara+"\" durante "+silent.ToString("0.0")+" s");
                LatestSample=null;UltimasManos=new List<ManoPipeline.Mano>();
                if(last){CerrarCamara();indiceCamara=candidatas.Count;siguienteIntento=now+2;}
                else AbrirSiguiente();
                return;
            }
            if(busy || !fresh)return;
            int n=cam.width*cam.height;
            if(buffer==null || buffer.Length!=n)buffer=new Color32[n];
            cam.GetPixels32(buffer);
            if(fotogramasCamara%30==1)Aviso=Brillo(buffer)<22?"Poca luz: busca una zona más iluminada":null;
            Process(buffer,cam.width,cam.height,cam.videoVerticallyMirrored);
        }
        static float Brillo(Color32[] px)
        {
            long sum=0;int n=0;
            for(int i=0;i<px.Length;i+=97){var c=px[i];sum+=(c.r*3+c.g*6+c.b)/10;n++;}
            return n>0?sum/(float)n:0;
        }
        async void Process(Color32[] px,int w,int h,bool flip)
        {
            busy=true;
            try
            {
                var hands=await pipeline.Run(px,w,h,flip);
                if(!this)return;
                double now=RecorridoTransport.Now;
                var packet=ToPacket(hands);
                if(packet.fingers.Length>0){lastWithHands=packet;lastHandsTime=now;UltimasManos=hands;}
                else if(lastWithHands!=null && now-lastHandsTime<toleranciaPerdida)
                    packet=new Packet{ts=now,fingers=lastWithHands.fingers,countL=lastWithHands.countL,countR=lastWithHands.countR};
                else UltimasManos=hands;
                LatestSample=new RecorridoTransport.Sample{packet=packet,arrival=now};
                analizados++;
                if(analizados==1 || analizados==300)Debug.Log("Seguimiento Unity: fotograma "+analizados+" de "+w+"x"+h+" · "+pipeline.Diagnostico);
            }
            // A readback still in flight when Play stops (or the camera closes) finds the models already released: not an error.
            catch(Exception e){if(!this || !isActiveAndEnabled)return;Error="Seguimiento Unity: "+e.Message;Debug.LogException(e);}
            finally{busy=false;}
        }
        // Public for tests: analyse one still image and return its packet.
        public static async Awaitable<Packet> AnalizarImagen(ManoPipeline p,Texture2D image)
        {
            var hands=await p.Run(image.GetPixels32(),image.width,image.height,false);
            var tracker=new GameObject("tmp").AddComponent<SeguimientoUnity>();
            tracker.enabled=false;tracker.framesSubir=1;tracker.suavizar=false;
            var packet=tracker.ToPacket(hands);
            Destroy(tracker.gameObject);
            return packet;
        }

        Packet ToPacket(List<ManoPipeline.Mano> hands)
        {
            var fingers=new List<Finger>(); int countL=0,countR=0;
            var seen=new HashSet<string>();
            foreach(var hand in hands)
            {
                string label=hand.derecha?"Right":"Left";
                if(intercambiarManos)label=label=="Right"?"Left":"Right";
                if(!seen.Add(label))continue; // one hand per label, as MediaPipe
                var lm=hand.puntos;
                var ups=Hysteresis(label,i=>IsUp(label,lm,Tips[i],Pips[i]));
                int count=0;
                for(int i=0;i<5;i++)
                {
                    if(!ups[i])continue;
                    count++;
                    string id=(label=="Left"?"L":"R")+i;
                    var tip=lm[Tips[i]];
                    if(suavizar && i==1)tip=Filter(id,tip);
                    fingers.Add(new Finger{id=id,x=Mathf.Clamp01(tip.x),y=Mathf.Clamp01(tip.y),isUp=true});
                }
                if(label=="Left")countL=count;else countR=count;
            }
            foreach(var label in new[]{"Left","Right"})if(!seen.Contains(label))Hysteresis(label,_=>false);
            return new Packet{ts=RecorridoTransport.Now,fingers=fingers.ToArray(),countL=countL,countR=countR};
        }
        bool IsUp(string label,Vector2[] lm,int tip,int pip)
        {
            if(tip==4)
            {
                bool u=label=="Right"?lm[tip].x>lm[pip].x:lm[tip].x<lm[pip].x;
                if((label=="Right" && invertirPulgarDerecho)||(label=="Left" && invertirPulgarIzquierdo))u=!u;
                return u;
            }
            return lm[tip].y+toleranciaY<lm[pip].y;
        }
        bool[] Hysteresis(string label,Func<int,bool> raw)
        {
            if(!state.TryGetValue(label,out var st)){st=new bool[5];state[label]=st;up[label]=new int[5];down[label]=new int[5];}
            var cu=up[label];var cd=down[label];
            for(int i=0;i<5;i++)
            {
                if(raw(i)){cu[i]++;cd[i]=0;if(!st[i]&&cu[i]>=framesSubir)st[i]=true;}
                else{cd[i]++;cu[i]=0;if(st[i]&&cd[i]>=framesBajar)st[i]=false;}
            }
            return st;
        }
        Vector2 Filter(string id,Vector2 p)
        {
            if(!filters.TryGetValue(id,out var f)){f=new OneEuro2();filters[id]=f;}
            double now=RecorridoTransport.Now;
            if(now-f.last>.35)f.Reset();
            return f.Step(p,now,minCutoff,beta);
        }
        void OnDestroy()
        {
            CerrarCamara();
            pipeline?.Dispose();
        }

        // One Euro filter: smooth when still, responsive when moving fast.
        sealed class OneEuro2
        {
            public double last=-1; Vector2 x,dx; bool has;
            public void Reset(){has=false;}
            static float Alpha(float cutoff,float dt){float r=2*Mathf.PI*cutoff*dt;return r/(r+1);}
            public Vector2 Step(Vector2 v,double now,float minCutoff,float beta)
            {
                float dt=has?Mathf.Max(1e-3f,(float)(now-last)):1/30f; last=now;
                if(!has){x=v;dx=Vector2.zero;has=true;return v;}
                var d=(v-x)/dt; dx=Vector2.Lerp(dx,d,Alpha(1,dt));
                float cutoff=minCutoff+beta*dx.magnitude*100;
                x=Vector2.Lerp(x,v,Alpha(cutoff,dt));
                return x;
            }
        }
    }
}
