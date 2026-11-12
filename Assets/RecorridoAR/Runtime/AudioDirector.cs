using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RecorridoAR
{
    // One persistent audio owner for the whole adventure. A scene that brings its own AudioDirector
    // hands over its clips (crossfade if the music changes); a scene without one fades the music out.
    // This prevents two music tracks playing at once after a scene change or a restart.
    public sealed class AudioDirector : MonoBehaviour
    {
        public enum Efecto { Boton, Recoger, Entregar, Etapa, Lobo, Fallo, Linterna, Premio, Fuegos }
        public const string ClaveSilencio="QuintaInsignia.AudioSilenciado";

        [Header("Música")]
        [Tooltip("Menús, introducción y final (calma).")]
        public AudioClip musica;
        [Tooltip("Música de juego por dificultad: más energía cuanto más difícil.")]
        public AudioClip musicaFacil, musicaMedia, musicaDificil;
        [Range(0,1)] public float volumenMusica=.30f;   // JUEGO UNIFICADO: mismo nivel de música que los otros niveles
        [Min(.05f)] public float fundidoMusica=1.6f;
        [Header("Efectos")]
        public AudioClip boton;
        public AudioClip recoger, entregar, etapa, lobo, fallo, linterna;
        [Tooltip("Fanfarria de recompensa al conseguir la insignia.")]
        public AudioClip premio;
        public AudioClip fuegos;
        [Range(0,1)] public float volumenEfectos=.7f;
        [Tooltip("Tiempo mínimo entre dos reproducciones del mismo efecto.")]
        [Min(0)] public float separacionMinima=.15f;

        public static AudioDirector Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){ Instance=null; }

        readonly AudioSource[] music=new AudioSource[2];
        AudioSource sfx;
        int current;
        float duckLevel=1,duckTarget=1,duckUntil;
        bool adoptedThisLoad;
        readonly Dictionary<Efecto,float> lastPlayed=new Dictionary<Efecto,float>();

        // JUEGO UNIFICADO: sin botón de silencio (silenciaba todo el juego). Nunca está silenciado.
        public static bool Muted => false;
        public AudioClip MusicaActual => music[current]?music[current].clip:null;
        public bool MusicaSonando => music[current] && music[current].isPlaying;

        void Awake()
        {
            if(Instance && Instance!=this){ Instance.Adopt(this); Destroy(gameObject); return; }
            Instance=this;
            if(transform.parent)transform.SetParent(null,false);
            DontDestroyOnLoad(gameObject);
            for(int i=0;i<2;i++)
            {
                var s=gameObject.AddComponent<AudioSource>();
                s.loop=true;s.playOnAwake=false;s.volume=0;s.spatialBlend=0;s.priority=0;
                music[i]=s;
            }
            sfx=gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake=false;sfx.spatialBlend=0;sfx.priority=16;
            SetMuted(false);
            adoptedThisLoad=true;
            SceneManager.sceneLoaded+=OnSceneLoaded;
        }
        void Start(){ if(Instance==this)PlayMusic(musica); }
        void OnDestroy()
        {
            if(Instance!=this)return;
            SceneManager.sceneLoaded-=OnSceneLoaded;
            Instance=null;
        }
        void Adopt(AudioDirector other)
        {
            boton=other.boton;recoger=other.recoger;entregar=other.entregar;etapa=other.etapa;
            lobo=other.lobo;fallo=other.fallo;linterna=other.linterna;premio=other.premio;fuegos=other.fuegos;
            volumenMusica=other.volumenMusica;volumenEfectos=other.volumenEfectos;fundidoMusica=other.fundidoMusica;
            musica=other.musica;musicaFacil=other.musicaFacil;musicaMedia=other.musicaMedia;musicaDificil=other.musicaDificil;
            adoptedThisLoad=true;
            duckTarget=1;duckUntil=0;
            PlayMusic(musica);
        }
        void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(mode!=LoadSceneMode.Single)return;
            // The next scene has no AudioDirector: let its own audio play alone.
            if(!adoptedThisLoad)PlayMusic(null);
            adoptedThisLoad=false;
        }

        public void PlayMusic(AudioClip clip)
        {
            var now=music[current];
            if(clip && now.clip==clip && now.isPlaying)return;
            if(!clip){ current=1-current; music[current].Stop(); music[current].clip=null; return; }
            current=1-current;
            var next=music[current];
            next.clip=clip;next.volume=0;next.time=0;next.Play();
        }
        public void FadeOutMusic(){ PlayMusic(null); }
        // 0 = calm theme (menus, intro, ending); 1..3 = gameplay music for Fácil, Media, Difícil.
        public static void MusicaPara(int dificultad)
        {
            if(!Instance)return;
            var d=Instance;
            var clip=dificultad==1?d.musicaFacil:dificultad==2?d.musicaMedia:dificultad==3?d.musicaDificil:null;
            d.PlayMusic(clip?clip:d.musica);
        }
        // Lower the music temporarily (for example under the final jingle).
        public void Duck(float level,float seconds)
        { duckTarget=Mathf.Clamp01(level); duckUntil=Time.unscaledTime+Mathf.Max(0,seconds); }

        public static void Play(Efecto e,float volume=1){ if(Instance)Instance.PlayEffect(e,volume); }
        public void PlayEffect(Efecto e,float volume=1)
        {
            var clip=Clip(e);
            if(!clip || !sfx)return;
            float now=Time.unscaledTime;
            if(e!=Efecto.Fuegos && lastPlayed.TryGetValue(e,out float t) && now-t<separacionMinima)return;
            lastPlayed[e]=now;
            // The recorded wolf howl is much louder than the synthesized effects: balance it.
            float balance=e==Efecto.Lobo?.5f:1f;
            sfx.PlayOneShot(clip,Mathf.Clamp01(volumenEfectos*volume*balance));
        }
        AudioClip Clip(Efecto e)=>e switch
        {
            Efecto.Boton=>boton, Efecto.Recoger=>recoger, Efecto.Entregar=>entregar, Efecto.Etapa=>etapa,
            Efecto.Lobo=>lobo, Efecto.Fallo=>fallo, Efecto.Linterna=>linterna, Efecto.Premio=>premio, _=>fuegos
        };

        public static void SetMuted(bool value)
        {
            // sin silencio en el juego unificado: borra un silencio guardado de versiones anteriores
            if(PlayerPrefs.HasKey(ClaveSilencio)){PlayerPrefs.DeleteKey(ClaveSilencio);PlayerPrefs.Save();}
            AudioListener.volume=1;
        }
        public static void ToggleMuted(){ SetMuted(!Muted); }

        void Update()
        {
            float dt=Time.unscaledDeltaTime;
            AudioListener.volume=Mathf.MoveTowards(AudioListener.volume,Muted?0:1,dt*2.5f);
            if(Time.unscaledTime>=duckUntil)duckTarget=1;
            duckLevel=Mathf.MoveTowards(duckLevel,duckTarget,dt*1.5f);
            float step=volumenMusica/Mathf.Max(.05f,fundidoMusica)*dt;
            for(int i=0;i<2;i++)
            {
                var s=music[i];
                if(!s)continue;
                float target=i==current && s.clip?volumenMusica*duckLevel:0;
                s.volume=Mathf.MoveTowards(s.volume,target,Mathf.Max(step,dt*.05f));
                if(i!=current && s.isPlaying && s.volume<=0){s.Stop();s.clip=null;}
            }
        }
    }
}
