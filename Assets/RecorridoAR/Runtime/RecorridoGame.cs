using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace RecorridoAR
{
    public sealed class RecorridoGame : MonoBehaviour
    {
        public enum IndexChoice { Automatico, R1, L1 }
        public IndexChoice indice=IndexChoice.Automatico;
        public bool espejo=true, fondoFijo=false, mostrarDiagnostico=false;
        [Header("Ruta de Energía · Nivel 2 · Tres etapas: 80, 90 y 110 segundos")]
        [Min(.01f)] public float confirmacionSalida=.25f;
        [Min(40)] public float velocidadAnimacionLobo=280;
        [Min(.1f)] public float permanenciaBoton=.8f;
        [Header("Referencias de la escena")]
        public RectTransform board, bateria, linterna, lucas, lobo;
        public RawImage cameraImage;
        [Tooltip("Seguimiento de la mano dentro de Unity (sin Python). Si llegan datos de Python por UDP 9050, se usan esos.")]
        public SeguimientoUnity camaraUnity;
        public RouteGraphic drawing;
        public FlashlightGlow luz;
        public FingerPointer pointer;
        public RawImage actionImage;
        public Texture2D jugarPNG, reintentarPNG, volverPNG;
        public Text overlayTitle, overlayHint;
        public Text overlayBadge, overlayBody, hudLevel, hudBatteries;
        public Text cameraStatus;
        public GameObject routeLayer, gameHud;
        public GameObject nextAction;
        public Graphic nextGraphic;
        public Graphic[] batterySlots;
        public RectTransform holdFill;
        public Text status, diagnostics, clock;
        public GameObject statusPanel, retryObject;
        public RectTransform retryRect;
        public Button retryButton;
        [Header("Introducción, audio y aventura (opcionales: sin ellos el juego funciona como antes)")]
        public IntroSequence intro;
        public NightSky noche;
        public SoftGlow haloLinterna;
        public HandButton botonSonido;
        public SpeakerIcon iconoSonido;
        public HandButton botonContinuar;
        public HandButton botonPartidaNueva;
        public HandButton botonFinalizar;
        [Tooltip("Pantalla de cierre tras Finalizar juego: noche, Lucas con la linterna y aviso del nivel 3.")]
        public GameObject cierreNivel;
        [Tooltip("Celebración de la insignia Ruta de Energía (al terminar cualquier dificultad).")]
        public CelebracionInsignia celebracion;
        // JUEGO UNIFICADO: si alguien se suscribe, muestra SU ventana de insignia en lugar de la celebración
        // propia y llama a la acción recibida al cerrarla. Devuelve false para usar la celebración de siempre.
        public static System.Func<System.Action,bool> PremioExterno;
        public HandButton botonJugarOtraVez;
        [Tooltip("Pájaros, luciérnagas y mariposas del paisaje.")]
        public AmbienteVivo ambiente;
        [Header("Selector de dificultad (Fácil 1:30 · Media 1:00 · Difícil 0:40, tres recorridos cada una)")]
        public GameObject selectorDificultad;
        public HandButton botonFacil, botonMedia, botonDificil;
        public HandButton botonSalir;
        public Text textoBotonSalir;
        public Graphic fondoBotonSalir;
        [Tooltip("Texto pequeño de la placa del título (QUINTA INSIGNIA · NIVEL 2). Se oculta durante la introducción.")]
        public Text tituloSuperior;
        [Tooltip("Texto grande de la placa: Quinta Insignia en la introducción, el nombre del nivel al jugar.")]
        public Text tituloPrincipal;
        public string tituloIntroduccion="Quinta Insignia";
        public string nombreDelNivel="Ruta de Energía";
        [Tooltip("Mostrar el punto rosa mientras se lleva la batería. Desactivado: solo se ve la batería.")]
        public bool puntoDuranteRecorrido=false;
        public AventuraEnlaces enlaces;
        [Header("Insignias (arriba a la izquierda) y menú de pausa")]
        [Tooltip("Hueco de la insignia del nivel 2 junto a la medalla del nivel 1 (silueta hasta conseguirla).")]
        public Graphic ranuraNivel2;
        public MenuJuego menu;
        [Tooltip("Segundos de la animación final (linterna encendiéndose) antes de mostrar Continuar / Volver a jugar.")]
        [Min(1)] public float duracionCierre=3.2f;
        [Tooltip("Desplazamiento horizontal de Volver a jugar cuando también se muestra Continuar.")]
        public float separacionBotonesFinal=140;
        CampaignRules campaign;
        LevelOneRules level => campaign.Current;
        RecorridoTransport transport;
        Texture2D videoTexture;
        RecorridoTransport.VideoFrame waiting;
        GameObject eventRoot;
        double lastStep,lastFresh,lastVideo;
        RecorridoTransport.Sample processedSample;
        string selectedHand;
        bool validFinger,indexUp,wasMirror,wasFallback;
        IndexChoice wasChoice;
        float outcomeSeconds;
        Vector2 winOrigin;
        LevelOneRules.Phase previousState;
        readonly DwellGate dwell=new DwellGate();
        Vector2 lucasHome, batteryHome;
        bool captureDone;
        Vector2 actionHome;
        float buttonProgress;
        bool stageSound,finalSound,failSound;
        bool IntroPlaying => intro && intro.Playing;
        bool enCierre, premioIniciado;
        bool MenuAbierto => menu && menu.Abierto;
        bool esperarSoltar;
        bool Choosing => selectorDificultad && level.State==LevelOneRules.Phase.Ready;
        void ElegirFacil()=>Elegir(1);
        void ElegirMedia()=>Elegir(2);
        void ElegirDificil()=>Elegir(3);
        // The player picks a difficulty: its three routes start from the first one.
        void Elegir(int dificultad)
        {
            if(!Choosing || !MenuAvailable || MenuAbierto)return;
            dwell.Reset();
            campaign.Elegir(dificultad);
            AudioDirector.MusicaPara(dificultad);
            ResetLevel();level.Arm();
        }
        public int Dificultad => campaign!=null?campaign.Dificultad:1;
        Vector2 titleHome;
        bool InRoute => level.State==LevelOneRules.Phase.Armed || level.State==LevelOneRules.Phase.Running;
        static Vector2 Local(Vector2 p)=>new Vector2(p.x-640,360-p.y);
        void OnEnable()
        {
            if(!board || !cameraImage || !drawing || !bateria || !linterna || !lucas || !lobo || !luz || !clock || !retryButton || !status || !diagnostics || !statusPanel || !retryObject || !retryRect || !pointer || !actionImage || !overlayTitle || !overlayHint || !holdFill || !jugarPNG || !reintentarPNG || !volverPNG || !overlayBadge || !overlayBody || !hudLevel || !hudBatteries || !nextAction || !nextGraphic || !cameraStatus || !routeLayer || !gameHud)
            {Debug.LogError("Abre Assets/Scenes/Recorrido.unity: faltan referencias de la interfaz.",this);enabled=false;return;}
            campaign=new CampaignRules();
            lucasHome=lucas.anchoredPosition;batteryHome=bateria.anchoredPosition;actionHome=retryRect.anchoredPosition;
            if(botonSonido)botonSonido.alConfirmar.AddListener(ToggleSound);
            if(botonContinuar && enlaces)botonContinuar.alConfirmar.AddListener(enlaces.IrASiguiente);
            if(botonPartidaNueva && enlaces)botonPartidaNueva.alConfirmar.AddListener(enlaces.ComenzarPartidaNueva);
            if(botonFinalizar && enlaces)botonFinalizar.alConfirmar.AddListener(enlaces.FinalizarJuego);
            if(enlaces)enlaces.AlFinalizar+=MostrarCierre;
            if(botonJugarOtraVez)botonJugarOtraVez.alConfirmar.AddListener(JugarOtraVez);
            // An insignia already won in this session stays in the top-left corner.
            if(celebracion){ if(PremioExterno==null && AventuraQuintaInsignia.Completado(enlaces?enlaces.nivel:2))celebracion.MostrarEnEsquina(); else celebracion.Ocultar(); }
            if(botonFacil)botonFacil.alConfirmar.AddListener(ElegirFacil);
            if(botonMedia)botonMedia.alConfirmar.AddListener(ElegirMedia);
            if(botonDificil)botonDificil.alConfirmar.AddListener(ElegirDificil);
            if(botonSalir && enlaces)botonSalir.alConfirmar.AddListener(SalirOContinuar);
            if(menu)
            {
                menu.Cerrar();
                if(menu.botonAbrir)menu.botonAbrir.alConfirmar.AddListener(AbrirMenu);
                if(menu.seguir)menu.seguir.alConfirmar.AddListener(CerrarMenu);
                if(menu.reiniciar)menu.reiniciar.alConfirmar.AddListener(MenuReiniciar);
                if(menu.dificultad)menu.dificultad.alConfirmar.AddListener(MenuCambiarDificultad);
                if(menu.inicio)menu.inicio.alConfirmar.AddListener(MenuVolverAlInicio);
            }
            enCierre=false;if(cierreNivel)cierreNivel.SetActive(false);
            if(tituloPrincipal)titleHome=tituloPrincipal.rectTransform.anchoredPosition;
            // A 2D menu game does not need hundreds of frames per second: cap it to keep the PC cool and smooth.
            QualitySettings.vSyncCount=1;Application.targetFrameRate=60;
            // The intro plays once per scene entry. Retrying or replaying never reloads the scene.
            if(intro)intro.Play(level.Path);
            if(!board.GetComponent<RectMask2D>())board.gameObject.AddComponent<RectMask2D>();
            videoTexture=new Texture2D(2,2,TextureFormat.RGB24,false);
            cameraImage.texture=videoTexture; cameraImage.enabled=false;
            if(EventSystem.current==null) eventRoot=new GameObject("Entrada_UI",typeof(EventSystem),typeof(InputSystemUIInputModule));
            retryButton.onClick.AddListener(Action);
            transport=new RecorridoTransport();
            lastStep=lastFresh=lastVideo=0;processedSample=null;waiting=null;
            wasMirror=espejo;wasFallback=fondoFijo;wasChoice=indice;
            ResetLevel();transport.Start();
        }
        void ResetLevel()
        {
            level.Confirmation=confirmacionSalida;level.Reset();
            drawing.route=level.Path;drawing.showCursor=false;drawing.SetAllDirty();
            selectedHand=null;validFinger=indexUp=false;outcomeSeconds=0;previousState=level.State;
            captureDone=false;dwell.Reset();
            bateria.gameObject.SetActive(true);bateria.localScale=Vector3.one;bateria.anchoredPosition=batteryHome;
            lobo.gameObject.SetActive(false);
            lucas.anchoredPosition=lucasHome;lucas.localScale=Vector3.one;lucas.localRotation=Quaternion.identity;
            luz.intensity=0;luz.SetVerticesDirty();
            pointer.visible=false;pointer.progress=0;pointer.SetVerticesDirty();
            stageSound=finalSound=failSound=false;buttonProgress=0;premioIniciado=false;
            if(noche && !IntroPlaying)noche.Set(0);
            if(haloLinterna){var c=haloLinterna.color;c.a=0;haloLinterna.color=c;haloLinterna.rectTransform.localScale=Vector3.one;}
            linterna.localScale=Vector3.one;
            retryRect.anchoredPosition=actionHome;
            UpdateOverlay();
        }
        // After Finalizar juego: night, Lucas with the lit flashlight and the notice about level 3.
        void MostrarCierre()
        {
            if(enCierre || !cierreNivel)return;
            enCierre=true;dwell.Reset();HandButton.ResetAll();
            AudioDirector.MusicaPara(0);
            UpdateOverlay();
        }
        // Play Ruta de Energía again from the difficulty selector; the insignia stays in the corner.
        void JugarOtraVez()
        {
            if(!enCierre)return;
            enCierre=false;premioIniciado=false;
            if(cierreNivel)cierreNivel.SetActive(false);
            campaign.Restart();ResetLevel();
            if(!selectorDificultad)level.Arm();
            AudioDirector.MusicaPara(0);
        }
        // ---------- Pause menu ----------
        // The Menu button shows whenever the player is not carrying the battery, watching the intro or the reward.
        public bool MenuPermitido => menu && !IntroPlaying && level.State!=LevelOneRules.Phase.Running && !PremioEnCurso &&
            !(level.State==LevelOneRules.Phase.Won && campaign.Complete && !enCierre) && !(enlaces && enlaces.Cargando);
        public bool MenuEstaAbierto => MenuAbierto;
        public void AbrirMenu()
        {
            if(!MenuPermitido || MenuAbierto)return;
            dwell.Reset();
            bool chosen=!Choosing;
            menu.Abrir(chosen,chosen,CampaignRules.Name(campaign.Dificultad));
        }
        public void CerrarMenu()
        {
            if(menu)menu.Cerrar();
            // The finger has to leave the spot before another button can be held (no accidental choice).
            esperarSoltar=true;dwell.Reset();HandButton.ResetAll();buttonProgress=0;
        }
        void SalirDelCierre(){ enCierre=false;premioIniciado=false;if(cierreNivel)cierreNivel.SetActive(false); }
        // Reiniciar: the same difficulty again from the first battery.
        public void MenuReiniciar()
        {
            CerrarMenu();SalirDelCierre();
            int d=campaign.Dificultad;campaign.Elegir(d);AudioDirector.MusicaPara(d);
            ResetLevel();level.Arm();
        }
        // Cambiar dificultad: back to the Facil / Media / Dificil selector.
        public void MenuCambiarDificultad()
        {
            CerrarMenu();SalirDelCierre();
            campaign.Restart();ResetLevel();AudioDirector.MusicaPara(0);
            if(!selectorDificultad)level.Arm();
        }
        // Volver al inicio: the opening (logo, the story and the tutorial) plays again.
        public void MenuVolverAlInicio()
        {
            CerrarMenu();SalirDelCierre();
            campaign.Restart();ResetLevel();AudioDirector.MusicaPara(0);
            if(intro){intro.Play(level.Path);UpdateOverlay();}
            else if(!selectorDificultad)level.Arm();
        }
        public bool PremioEnCurso => celebracion && celebracion.Playing;
        // JUEGO UNIFICADO: la dificultad también se cambia desde el menú de pausa general (Quinta Insignia).
        // Como cada dificultad tiene sus propias tres rutas, empieza esa dificultad desde la primera pila.
        public bool PuedeCambiarDificultad => !IntroPlaying && !PremioEnCurso && !enCierre;
        public void CambiarDificultadExterna(int dificultad)
        {
            if(!PuedeCambiarDificultad)return;
            if(MenuAbierto)CerrarMenu();
            campaign.Elegir(dificultad);AudioDirector.MusicaPara(campaign.Dificultad);
            ResetLevel();level.Arm();
        }
        public bool EnCierre => enCierre;
        // Closing button: go on to level 3 (Cruce del bosque) when its scene is linked; otherwise close the game.
        void SalirOContinuar(){ if(!enlaces)return; if(enlaces.SiguienteDisponible)enlaces.IrASiguiente();else enlaces.Salir(); }
        void ToggleSound()
        {
            AudioDirector.ToggleMuted();
            if(iconoSonido)iconoSonido.SetMuted(AudioDirector.Muted);
        }
        bool MenuAvailable => !enCierre && !MenuAbierto && !(enlaces && enlaces.Cargando) && (
            (level.State==LevelOneRules.Phase.Ready && !IntroPlaying) || level.State==LevelOneRules.Phase.TimedOut ||
            // After the third battery the insignia reward and the closing screen replace the results panel.
            (level.State==LevelOneRules.Phase.Won && (campaign.Complete?!celebracion && outcomeSeconds>=duracionCierre:outcomeSeconds>=.75f)) ||
            (level.State==LevelOneRules.Phase.Captured && captureDone));
        void Action()
        {
            if(!MenuAvailable || MenuAbierto)return;
            dwell.Reset();
            AudioDirector.Play(AudioDirector.Efecto.Boton);
            if(level.State==LevelOneRules.Phase.Ready){ if(!Choosing)level.Arm(); }
            else
            {
                if(level.State==LevelOneRules.Phase.Won)
                {
                    // After the third battery, Reintentar goes back to the difficulty selector.
                    if(campaign.Complete){campaign.Restart();ResetLevel();if(selectorDificultad)AudioDirector.MusicaPara(0);else level.Arm();return;}
                    campaign.Advance();
                }
                else campaign.Retry();
                ResetLevel();level.Arm();
            }
        }
        bool OverButton(Vector2 point)
        {
            Vector3 world=board.TransformPoint(Local(point));
            Vector2 local=retryRect.InverseTransformPoint(world);
            Rect hit=retryRect.rect;
            hit.xMin+=10;hit.xMax-=10;hit.yMin+=8;hit.yMax-=8;
            return hit.Contains(local);
        }
        void UpdateOverlay()
        {
            bool home=level.State==LevelOneRules.Phase.Ready;
            if(cierreNivel)cierreNivel.SetActive(enCierre);
            lucas.gameObject.SetActive(!enCierre);
            if(enCierre)
            {
                routeLayer.SetActive(false);gameHud.SetActive(false);retryObject.SetActive(false);statusPanel.SetActive(false);
                if(noche)noche.Set(.9f);
                if(textoBotonSalir)textoBotonSalir.text="Continuar";
                if(fondoBotonSalir)fondoBotonSalir.color=new Color(.31f,.54f,.36f,1);
                if(botonSonido)botonSonido.gameObject.SetActive(false);   // JUEGO UNIFICADO: sin botón de silencio
                if(iconoSonido)iconoSonido.SetMuted(AudioDirector.Muted);
                ActualizarMenuEInsignias();
                dwell.Reset();
                return;
            }
            routeLayer.SetActive(!home || (intro && intro.MuestraRecorrido));gameHud.SetActive(!home);
            if(tituloPrincipal)
            {
                // During the opening the original Quinta Insignia logo occupies the plate.
                tituloPrincipal.gameObject.SetActive(!(IntroPlaying && intro.MuestraLogo));
                string title=IntroPlaying?tituloIntroduccion:nombreDelNivel;
                if(tituloPrincipal.text!=title)tituloPrincipal.text=title;
                tituloPrincipal.rectTransform.anchoredPosition=IntroPlaying?new Vector2(titleHome.x,0):titleHome;
            }
            if(tituloSuperior)tituloSuperior.gameObject.SetActive(!IntroPlaying);
            if(botonSonido)botonSonido.gameObject.SetActive(false);   // JUEGO UNIFICADO: sin botón de silencio
            ActualizarMenuEInsignias();
            if(iconoSonido)iconoSonido.SetMuted(AudioDirector.Muted);
            bool show=MenuAvailable;
            retryObject.SetActive(show);
            bool finalScreen=show && level.State==LevelOneRules.Phase.Won && campaign.Complete;
            bool canContinue=finalScreen && enlaces && enlaces.SiguienteDisponible;
            if(botonContinuar)botonContinuar.gameObject.SetActive(canContinue);
            // Final screen: Finalizar juego | Reintentar. Continuar takes Finalizar's place once a next scene is linked.
            bool canFinish=finalScreen && !canContinue && botonFinalizar && enlaces;
            if(botonFinalizar)botonFinalizar.gameObject.SetActive(canFinish);
            if(botonPartidaNueva)botonPartidaNueva.gameObject.SetActive(finalScreen && enlaces && enlaces.PartidaNuevaDisponible);
            retryRect.anchoredPosition=(canContinue||canFinish)?actionHome+new Vector2(separacionBotonesFinal,0):actionHome;
            bool choosing=show && Choosing;
            if(selectorDificultad)selectorDificultad.SetActive(choosing);
            retryRect.gameObject.SetActive(!choosing);
            if(!show){dwell.Reset();return;}
            bool ready=level.State==LevelOneRules.Phase.Ready, won=level.State==LevelOneRules.Phase.Won;
            bool next=won && !campaign.Complete;
            nextAction.SetActive(next);actionImage.enabled=!next;
            retryButton.targetGraphic=next?nextGraphic:actionImage;
            actionImage.texture=ready?jugarPNG:(won && !campaign.Complete)?volverPNG:reintentarPNG;
            string stage=CampaignRules.Name(campaign.Dificultad).ToUpper()+" · ETAPA "+campaign.Number+" / 3";
            overlayBadge.text=ready?"QUINTA INSIGNIA · NIVEL 2":(won && campaign.Complete)?"NIVEL 2 · INSIGNIA CONSEGUIDA":stage;
            overlayTitle.text=ready?(choosing?"Elige la dificultad":"¿Listo, explorador?"):won?(campaign.Complete?"¡Linterna encendida!":"¡Batería conseguida!"):
                level.State==LevelOneRules.Phase.Captured?"¡El lobo te encontró!":"¡Prueba otra vez!";
            overlayBody.text=ready?(choosing?"Cada dificultad tiene 3 baterías con recorridos distintos.":"Tres etapas, tres baterías.\nLlévalas de derecha a izquierda hasta la linterna."):
                won?(campaign.Complete?"Con su linterna encendida, Lucas ya puede\ncruzar el bosque de noche.":("Sigue a la etapa "+(campaign.Number+1)+" de 3 · "+CampaignRules.Name(campaign.Dificultad))):
                level.State==LevelOneRules.Phase.Captured?"Sigue el camino con calma. ¡Tú puedes!":"Se acabó el tiempo. Tienes otra oportunidad.";
            overlayHint.text=!validFinger?"Muestra tu índice a la cámara":!indexUp?"Estira el índice para elegir":"Mantén el índice sobre el botón";
            holdFill.localScale=new Vector3(choosing?buttonProgress:dwell.Progress,1,1);
        }
        void ActualizarMenuEInsignias()
        {
            if(menu && menu.botonAbrir)menu.botonAbrir.gameObject.SetActive(MenuPermitido && !MenuAbierto);
            if(menu && MenuAbierto && !MenuPermitido)menu.Cerrar();
            if(ranuraNivel2)ranuraNivel2.enabled=!(celebracion && celebracion.EnEsquina);
        }
        Finger Choose(Packet packet)
        {
            bool playing=level.State==LevelOneRules.Phase.Running;
            var f=IndexTracking.Select(packet,playing?selectedHand:null,indice==IndexChoice.R1?"R1":indice==IndexChoice.L1?"L1":null);
            if(f!=null && f.isUp)selectedHand=f.id;return f;
        }
        void Update()
        {
            if(Time.timeScale==0f)return;   // JUEGO UNIFICADO: el menú de pausa general detiene el tiempo
            double now=RecorridoTransport.Now;
            if(espejo!=wasMirror || fondoFijo!=wasFallback || indice!=wasChoice)
            {campaign.Restart();ResetLevel();waiting=null;lastFresh=lastStep=0;wasMirror=espejo;wasFallback=fondoFijo;wasChoice=indice;}
            level.Confirmation=Mathf.Max(.01f,confirmacionSalida);
            // One clock only. It keeps running through hand loss and stops for every terminal state.
            if(!MenuAbierto)level.Tick(Time.unscaledDeltaTime);   // the pause menu stops the clock
            cameraImage.uvRect=espejo?new Rect(1,0,-1,1):new Rect(0,0,1,1);
            transport.Pump();var next=transport.TakeVideo();if(next!=null)waiting=next;
            RecorridoTransport.Sample sample=null;
            // Two sources with identical packets: Python (UDP 9050, if running) has priority; otherwise Unity's own camera.
            var udpSample=transport.LatestSample;
            bool pythonFresh=udpSample!=null && now-udpSample.arrival<.35;
            bool unitySource=!pythonFresh && camaraUnity && camaraUnity.isActiveAndEnabled;
            var latest=unitySource?camaraUnity.LatestSample:udpSample;
            if(fondoFijo){sample=transport.LatestSample;cameraImage.enabled=false;}
            else if(waiting!=null)
            {
                if(now-waiting.arrival>.35)waiting=null;
                else if(transport.Match(waiting.ts,out var matched))
                {
                    if(now-matched.arrival<.35 && ImageConversion.LoadImage(videoTexture,waiting.jpeg,false))
                    {sample=matched;lastVideo=now;cameraImage.enabled=true;}
                    waiting=null;
                }
            }
            // The camera is a reference preview now. Fresh UDP drives the pointer and gameplay
            // even when video stalls; it always maps to the entire board, not the preview rect.
            sample=latest;
            // Consume each received sample once. Python timestamps may restart or repeat;
            // local arrival time determines freshness, not the remote clock's progression.
            if(sample!=null && !ReferenceEquals(sample,processedSample) && now-sample.arrival<.35)
            {
                float dt=lastStep==0?0:(float)(now-lastStep);
                if(lastStep!=0 && now-lastStep>.35){level.Missing();dwell.Reset();selectedHand=null;dt=0;}
                lastStep=lastFresh=now;processedSample=sample;
                Finger finger=Choose(sample.packet);validFinger=finger!=null;indexUp=validFinger && finger.isUp;
                if(finger==null){level.Missing();dwell.Reset();selectedHand=null;HandButton.ResetAll();buttonProgress=0;}
                else
                {
                    drawing.cursor=RouteLayout.IndexToBoard(finger.x,finger.y,espejo);
                    if(!indexUp){level.Missing();dwell.Reset();HandButton.ResetAll();buttonProgress=0;}
                    else
                    {
                        // Extra buttons (Saltar, sonido, Continuar) never react while carrying the battery.
                        Vector3 world=board.TransformPoint(Local(drawing.cursor));
                        if(esperarSoltar && !HandButton.AnyContains(world) && !(MenuAvailable && OverButton(drawing.cursor)))esperarSoltar=false;
                        if(level.State==LevelOneRules.Phase.Running || esperarSoltar){HandButton.ResetAll();buttonProgress=0;}
                        else buttonProgress=HandButton.FeedAll(world,true,dt,permanenciaBoton);
                        if(MenuAbierto)dwell.Reset();
                        else if(MenuAvailable && !Choosing)
                        {
                            dwell.Duration=permanenciaBoton;
                            if(dwell.Sample(!esperarSoltar && OverButton(drawing.cursor),dt))Action();
                        }
                        else {dwell.Reset();level.Sample(drawing.cursor,dt);}
                    }
                }
            }
            bool fresh=lastFresh>0 && now-lastFresh<=.35;
            bool udp=latest!=null && now-latest.arrival<.35;
            bool video=lastVideo>0 && now-lastVideo<.35;
            if(unitySource && !fondoFijo)
            {
                if(cameraImage.texture!=camaraUnity.Vista)cameraImage.texture=camaraUnity.Vista;
                video=camaraUnity.CamaraActiva;cameraImage.enabled=video;
                if(camaraUnity.Vista is WebCamTexture w && w.videoVerticallyMirrored)
                    cameraImage.uvRect=espejo?new Rect(1,1,-1,-1):new Rect(0,1,1,-1);
            }
            else if(cameraImage.texture!=videoTexture)cameraImage.texture=videoTexture;
            if(!fresh){validFinger=indexUp=false;level.Missing();dwell.Reset();selectedHand=null;HandButton.ResetAll();buttonProgress=0;}
            if(!fondoFijo && !video)cameraImage.enabled=false;
            string camWarning=unitySource && cameraImage.enabled?camaraUnity.Aviso:null;
            cameraStatus.gameObject.SetActive(!cameraImage.enabled || camWarning!=null);
            cameraStatus.text=camWarning ?? (fondoFijo?"Vista previa desactivada":unitySource?camaraUnity.Estado:"Esperando cámara…");
            bool terminal=level.State==LevelOneRules.Phase.Won || level.State==LevelOneRules.Phase.Captured || level.State==LevelOneRules.Phase.TimedOut;
            drawing.showCursor=false; // The pointer is rendered above the overlay as a separate graphic.
            var guide=udp?IndexTracking.Guide(latest.packet,selectedHand):null;
            // While carrying the battery only the battery is shown. The guide returns when the hand is lost,
            // so the player can find the battery again.
            bool carrying=level.State==LevelOneRules.Phase.Running && !level.Recovering && validFinger && indexUp;
            pointer.visible=guide!=null && (puntoDuranteRecorrido || !carrying);
            if(guide!=null)pointer.position=RouteLayout.IndexToBoard(guide.x,guide.y,espejo);
            pointer.progress=guide!=null && validFinger && guide.id==selectedHand?Mathf.Max(dwell.Progress,buttonProgress):0;
            pointer.playing=level.State==LevelOneRules.Phase.Running;
            pointer.SetVerticesDirty();
            drawing.SetVerticesDirty();
            if(level.State!=previousState){StateSound(previousState,level.State);outcomeSeconds=0;winOrigin=level.Battery;previousState=level.State;}
            if(terminal && !MenuAbierto)outcomeSeconds+=Time.unscaledDeltaTime;
            if(level.State==LevelOneRules.Phase.Won)
            {
                campaign.Collect();
                float t=Mathf.Clamp01(outcomeSeconds/.65f);
                bateria.anchoredPosition=Vector2.Lerp(Local(winOrigin),linterna.anchoredPosition,t);
                bateria.localScale=Vector3.one*(1-t*.85f);bateria.gameObject.SetActive(t<1);
                luz.intensity=Mathf.Clamp01((outcomeSeconds-.45f)/.4f);luz.SetVerticesDirty();
                lucas.localScale=Vector3.one*(1+.04f*Mathf.Sin(outcomeSeconds*8));
                if(!stageSound && outcomeSeconds>=.65f)
                {stageSound=true;AudioDirector.Play(campaign.Complete?AudioDirector.Efecto.Linterna:AudioDirector.Efecto.Etapa);}
                if(campaign.Complete)
                {
                    FinalAnimation(outcomeSeconds);
                    // The flashlight is lit: award the insignia, then show what comes next.
                    if(celebracion && !premioIniciado && outcomeSeconds>=duracionCierre)
                    {premioIniciado=true;if(PremioExterno==null || !PremioExterno(MostrarCierre))celebracion.Play(MostrarCierre);}
                }
            }
            else if(!(intro && intro.MuestraRecorrido))
                bateria.anchoredPosition=(level.State==LevelOneRules.Phase.Ready || level.State==LevelOneRules.Phase.Armed)?batteryHome:Local(level.Battery);
            if(level.State==LevelOneRules.Phase.Captured)
            {
                lobo.gameObject.SetActive(true);
                captureDone=CaptureMotion.Evaluate(outcomeSeconds,lucasHome,lobo.rect.size,velocidadAnimacionLobo,
                    out var wolfPosition,out var childPosition,out bool dragging);
                lobo.anchoredPosition=wolfPosition;lucas.anchoredPosition=childPosition;
                lucas.localRotation=Quaternion.Euler(0,0,dragging?8:0);
                if(captureDone && !failSound){failSound=true;AudioDirector.Play(AudioDirector.Efecto.Fallo);}
            }
            int seconds=Mathf.CeilToInt(level.Remaining);clock.text=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            hudLevel.text=CampaignRules.Name(campaign.Dificultad).ToUpper()+" · ETAPA "+campaign.Number+" / 3";
            hudBatteries.text=campaign.Batteries+" / 3";
            if(batterySlots!=null)for(int i=0;i<batterySlots.Length;i++)
                if(batterySlots[i])batterySlots[i].color=i<campaign.Batteries?Color.white:new Color(.40f,.48f,.44f,.30f);
            UpdateOverlay();
            if(ambiente)ambiente.calma=InRoute;
            // Lucas breathes gently while he waits (the intro, the ending and the wolf animate him on their own).
            if(!IntroPlaying && !enCierre && (level.State==LevelOneRules.Phase.Ready || InRoute))
                lucas.localScale=new Vector3(1,1+.012f*Mathf.Sin(Time.unscaledTime*2.1f),1);
            if(terminal || level.State==LevelOneRules.Phase.Ready)status.text="";
            else if(transport.Error!=null)status.text="Revisa la conexión de la cámara";
            else if(level.State==LevelOneRules.Phase.Armed)status.text=validFinger?(indexUp?"Lleva el punto rosa a Inicio, a la derecha":"Estira el índice para comenzar"):"Muestra tu mano a la cámara";
            else if(level.State==LevelOneRules.Phase.Running && (!validFinger || !indexUp || level.Recovering))status.text="Muestra tu índice y vuelve a la batería";
            else if(!udp)status.text="Muestra tu índice a la cámara";
            else status.text="";
            statusPanel.SetActive(status.text.Length>0);
            diagnostics.gameObject.SetActive(mostrarDiagnostico);
            diagnostics.text=(unitySource?"Fuente: Unity ("+camaraUnity.Estado+")":pythonFresh?"Fuente: Python":"Fuente: ninguna")+" · Datos: "+udp+" · Video 9051: "+video+" · Índice: "+selectedHand+" · "+level.State+(fondoFijo?" · PRUEBA SIN VIDEO":"");
            if(mostrarDiagnostico && transport.Error!=null)diagnostics.text=transport.Error;
        }
        void StateSound(LevelOneRules.Phase from,LevelOneRules.Phase to)
        {
            if(to==LevelOneRules.Phase.Running && from==LevelOneRules.Phase.Armed)AudioDirector.Play(AudioDirector.Efecto.Recoger);
            else if(to==LevelOneRules.Phase.Won)AudioDirector.Play(AudioDirector.Efecto.Entregar);
            else if(to==LevelOneRules.Phase.Captured)AudioDirector.Play(AudioDirector.Efecto.Lobo);
            else if(to==LevelOneRules.Phase.TimedOut)AudioDirector.Play(AudioDirector.Efecto.Fallo);
        }
        // Third battery: the night falls, the flashlight lights up and Lucas celebrates.
        void FinalAnimation(float t)
        {
            if(noche)noche.Set(Mathf.Clamp01((t-.5f)/.9f)*.85f);
            if(haloLinterna)
            {
                float a=Mathf.Clamp01((t-.7f)/.5f);
                var c=haloLinterna.color;c.a=a*(.82f+.18f*Mathf.Sin(t*3.2f));haloLinterna.color=c;
                float s=Mathf.Lerp(.5f,1.15f,a);haloLinterna.rectTransform.localScale=new Vector3(s,s,1);
            }
            float punch=t>.65f?1+.18f*Mathf.Exp(-(t-.65f)*5)*Mathf.Sin((t-.65f)*18):1;
            linterna.localScale=new Vector3(punch,punch,1);
            float hop=t>1f && t<duracionCierre?Mathf.Abs(Mathf.Sin((t-1f)*6))*16:0;
            lucas.anchoredPosition=lucasHome+new Vector2(0,hop);
            if(!finalSound && t>=1.15f)
            {
                finalSound=true;
                AudioDirector.MusicaPara(0);   // back to the calm theme under the lit flashlight
                if(enlaces)enlaces.MarcarCompletado();
            }
        }
        void OnDisable()
        {
            transport?.Dispose();transport=null;
            if(retryButton)retryButton.onClick.RemoveListener(Action);
            if(botonSonido)botonSonido.alConfirmar.RemoveListener(ToggleSound);
            if(botonContinuar && enlaces)botonContinuar.alConfirmar.RemoveListener(enlaces.IrASiguiente);
            if(botonPartidaNueva && enlaces)botonPartidaNueva.alConfirmar.RemoveListener(enlaces.ComenzarPartidaNueva);
            if(botonFinalizar && enlaces)botonFinalizar.alConfirmar.RemoveListener(enlaces.FinalizarJuego);
            if(enlaces)enlaces.AlFinalizar-=MostrarCierre;
            if(botonJugarOtraVez)botonJugarOtraVez.alConfirmar.RemoveListener(JugarOtraVez);
            if(botonFacil)botonFacil.alConfirmar.RemoveListener(ElegirFacil);
            if(botonMedia)botonMedia.alConfirmar.RemoveListener(ElegirMedia);
            if(botonDificil)botonDificil.alConfirmar.RemoveListener(ElegirDificil);
            if(botonSalir && enlaces)botonSalir.alConfirmar.RemoveListener(SalirOContinuar);
            if(menu)
            {
                menu.Cerrar();
                if(menu.botonAbrir)menu.botonAbrir.alConfirmar.RemoveListener(AbrirMenu);
                if(menu.seguir)menu.seguir.alConfirmar.RemoveListener(CerrarMenu);
                if(menu.reiniciar)menu.reiniciar.alConfirmar.RemoveListener(MenuReiniciar);
                if(menu.dificultad)menu.dificultad.alConfirmar.RemoveListener(MenuCambiarDificultad);
                if(menu.inicio)menu.inicio.alConfirmar.RemoveListener(MenuVolverAlInicio);
            }
            if(cameraImage){cameraImage.enabled=false;cameraImage.texture=null;}
            if(drawing){drawing.route=new RouteRules(1,true);drawing.showCursor=false;drawing.SetAllDirty();}
            if(pointer){pointer.visible=false;pointer.SetVerticesDirty();}
            if(eventRoot)Destroy(eventRoot);if(videoTexture)Destroy(videoTexture);
        }
    }
}
