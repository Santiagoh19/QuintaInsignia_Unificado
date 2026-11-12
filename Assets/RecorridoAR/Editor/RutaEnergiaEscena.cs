using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;

namespace RecorridoAR.Editor
{
    // Adds the intro, night veil, sound button, Continuar link and audio to Recorrido.unity.
    // Safe to run again: it rebuilds only the objects it owns (by name) and never touches the others.
    // Menu: Ruta de Energía > Construir introduccion y audio. Batch: -executeMethod RecorridoAR.Editor.RutaEnergiaEscena.Construir
    public static class RutaEnergiaEscena
    {
        const string ScenePath="Assets/Scenes/Recorrido.unity";
        const string Art="Assets/RecorridoAR/Arte/";
        const string Audio="Assets/RecorridoAR/Audio/";
        static readonly Color Ink=new Color(.145f,.302f,.196f,1);
        static readonly Color Cream=new Color(.99f,.97f,.91f,1);
        static readonly Color Gold=new Color(.957f,.788f,.365f,1);
        static readonly Color Shadow=new Color(.08f,.16f,.12f,.35f);

        [MenuItem("Ruta de Energía/Construir introduccion y audio")]
        public static void ConstruirDesdeMenu()
        {
            if(EditorApplication.isPlaying)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Construir();
        }

        public static void Construir()
        {
            ConfigurarAudio();
            if(!System.IO.File.Exists(RutaEnergiaArte.MedallaDestino)){RutaEnergiaArte.MedallaNivel1();AssetDatabase.Refresh();}
            RutaEnergiaArte.ConfigurarTexturas();
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var game=Object.FindFirstObjectByType<RecorridoGame>(FindObjectsInactive.Include);
            var board=game.board;
            var cursor=game.pointer.rectTransform;
            var title=board.Find("Titulo_Ruta_de_Energia");
            var route=game.routeLayer.transform;
            var overlay=game.retryObject.transform;
            Font titan=AssetDatabase.LoadAssetAtPath<Font>(Art+"TitanOne.ttf");
            Font fredoka=AssetDatabase.LoadAssetAtPath<Font>(Art+"Fredoka-Medium.ttf");
            var uiSprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var knob=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            foreach(var n in new[]{"Velo_noche","Introduccion","Boton_sonido","Fundido_pantalla","Cierre_nivel","Ambiente_vivo","Celebracion_insignia","Insignias_superiores","Boton_menu","Menu_pausa"})Remove(board,n);
            foreach(var old in game.GetComponents<MenuJuego>())Object.DestroyImmediate(old);
            Remove(route,"Halo_linterna");
            Remove(overlay,"Boton_continuar_aventura");Remove(overlay,"Selector_dificultad");Remove(overlay,"Boton_finalizar_juego");Remove(overlay,"Boton_partida_nueva");
            foreach(var n in new[]{"Audio_Ruta_de_Energia","Aventura_Quinta_Insignia","Seguimiento_mano_Unity"}){var go=GameObject.Find(n);if(go)Object.DestroyImmediate(go);}

            // Night veil: behind the title, Lucas and the route so the illustration darkens but the art stays readable.
            var veil=Node("Velo_noche",board,Vector2.zero,new Vector2(1280,720));
            veil.SetSiblingIndex(title.GetSiblingIndex());
            var veilArt=veil.gameObject.AddComponent<RoundedPanel>();
            veilArt.radius=0;veilArt.color=new Color(.04f,.06f,.18f,.96f);veilArt.lowerColor=new Color(.07f,.10f,.24f,.80f);veilArt.gradient=1;veilArt.raycastTarget=false;
            var veilGroup=veil.gameObject.AddComponent<CanvasGroup>();veilGroup.alpha=0;veilGroup.blocksRaycasts=false;
            var night=veil.gameObject.AddComponent<NightSky>();
            var stars=Node("Estrellas",veil,Vector2.zero,new Vector2(1280,720));
            night.estrellas=stars;
            Vector3[] starData={new Vector3(-560,300,16),new Vector3(-470,215,10),new Vector3(-380,322,13),new Vector3(-250,250,9),
                new Vector3(-150,200,12),new Vector3(370,330,11),new Vector3(250,215,9),new Vector3(395,205,14),new Vector3(520,250,10),
                new Vector3(600,170,13),new Vector3(-600,140,9),new Vector3(120,190,8),new Vector3(-40,215,10),new Vector3(470,120,8)};
            for(int i=0;i<starData.Length;i++)
            {
                var s=Node("Estrella_"+(i+1),stars,new Vector2(starData[i].x,starData[i].y),Vector2.one*starData[i].z*1.6f);
                var g=s.gameObject.AddComponent<SoftGlow>();g.color=new Color(1,.97f,.84f,.95f);g.nucleo=.2f;g.raycastTarget=false;
            }

            // Living landscape: birds, fireflies and butterflies, above the night veil and behind title, route and Lucas.
            var life=Node("Ambiente_vivo",board,Vector2.zero,new Vector2(1280,720));
            life.SetSiblingIndex(veil.GetSiblingIndex()+1);
            var ambient=life.gameObject.AddComponent<AmbienteVivo>();ambient.noche=night;
            var birds=new PajaroSilueta[3];
            for(int i=0;i<3;i++){var b=Node("Pajaro_"+(i+1),life,new Vector2(-760,290),new Vector2(34,16)).gameObject.AddComponent<PajaroSilueta>();b.color=ambient.colorPajaro;b.raycastTarget=false;birds[i]=b;}
            Vector2[] flies={new Vector2(-590,-200),new Vector2(-470,-290),new Vector2(-380,-160),new Vector2(-200,-305),new Vector2(-60,-180),new Vector2(80,-300),
                new Vector2(200,-170),new Vector2(320,-90),new Vector2(560,-60),new Vector2(600,40),new Vector2(-610,-20),new Vector2(460,-120)};
            var glows=new SoftGlow[flies.Length];
            for(int i=0;i<flies.Length;i++){var g=Node("Luciernaga_"+(i+1),life,flies[i],Vector2.one*(i%3==0?20:15)).gameObject.AddComponent<SoftGlow>();g.color=new Color(.98f,.95f,.55f,.5f);g.nucleo=.22f;g.raycastTarget=false;glows[i]=g;}
            var flies2=new MariposaVector[2];
            Vector2[] bHome={new Vector2(-500,-165),new Vector2(40,-250)};
            for(int i=0;i<2;i++){var m=Node("Mariposa_"+(i+1),life,bHome[i],new Vector2(26,22)).gameObject.AddComponent<MariposaVector>();m.raycastTarget=false;
                if(i==1){m.colorAlas=new Color(.96f,.79f,.37f,1);m.colorDetalle=new Color(.94f,.51f,.33f,1);}flies2[i]=m;}
            ambient.pajaros=birds;ambient.luciernagas=glows;ambient.mariposas=flies2;

            // Warm halo around the flashlight for the ending.
            var lantern=game.linterna;
            var halo=Node("Halo_linterna",route,lantern.anchoredPosition,new Vector2(380,380));
            halo.SetSiblingIndex(game.luz.transform.GetSiblingIndex());
            var haloArt=halo.gameObject.AddComponent<SoftGlow>();haloArt.color=new Color(1,.87f,.45f,0);haloArt.nucleo=.3f;haloArt.raycastTarget=false;

            // Sound toggle (top right). Hidden automatically while carrying the battery.
            var sound=Node("Boton_sonido",board,new Vector2(586,316),new Vector2(76,76));
            sound.SetSiblingIndex(cursor.GetSiblingIndex());
            Panel("Sombra",sound,new Vector2(0,-4),new Vector2(76,76),38,Shadow,false);
            Panel("Borde",sound,Vector2.zero,new Vector2(76,76),38,Ink,true);
            var ring=Filled("Progreso_permanencia",sound,Vector2.zero,new Vector2(72,72),knob,Gold,Image.FillMethod.Radial360);
            Panel("Fondo",sound,Vector2.zero,new Vector2(62,62),31,Cream,true);
            var iconRect=Node("Icono_altavoz",sound,new Vector2(0,0),new Vector2(42,42));
            var icon=iconRect.gameObject.AddComponent<SpeakerIcon>();icon.color=Ink;icon.raycastTarget=false;
            var soundButton=sound.gameObject.AddComponent<HandButton>();soundButton.relleno=ring;soundButton.margen=2;

            // Intro (inactive in the saved scene; it activates itself when the level starts).
            var introRoot=Node("Introduccion",board,Vector2.zero,new Vector2(1280,720));
            introRoot.SetSiblingIndex(cursor.GetSiblingIndex());
            var introGroup=introRoot.gameObject.AddComponent<CanvasGroup>();introGroup.alpha=1;
            var intro=introRoot.gameObject.AddComponent<IntroSequence>();
            // Thought cloud (new PNG) with editable text.
            var cloud=Node("Nube_pensamiento",introRoot,new Vector2(150,8),new Vector2(560,328));
            var cloudImage=cloud.gameObject.AddComponent<RawImage>();cloudImage.raycastTarget=false;
            cloudImage.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Nuevas/nube_pensamiento.png");
            var cloudGroup=cloud.gameObject.AddComponent<CanvasGroup>();cloudGroup.alpha=0;cloudGroup.blocksRaycasts=false;
            var cloudText=Label("Texto_nube",cloud,new Vector2(28,34),new Vector2(400,150),titan,34,Ink,"¡Se está haciendo de noche!");
            var surprise=Node("Signo_sorpresa",introRoot,new Vector2(-80,-60),new Vector2(66,66));
            var surpriseImage=surprise.gameObject.AddComponent<RawImage>();surpriseImage.raycastTarget=false;
            surpriseImage.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Nuevas/signo_sorpresa.png");
            surprise.gameObject.SetActive(false);
            // Demo guide (same look as the real pink guide) used to explain Inicio.
            var demo=Node("Punto_demo",introRoot,Vector2.zero,new Vector2(56,56));
            Circle("Halo",demo,56,new Color(1,.08f,.40f,.25f),knob);
            var demoRing=Filled("Permanencia",demo,Vector2.zero,new Vector2(50,50),knob,new Color(1,.80f,.26f,1),Image.FillMethod.Radial360);
            Circle("Contorno",demo,42,new Color(.13f,.20f,.25f,1),knob);
            Circle("Borde_blanco",demo,36,Color.white,knob);
            Circle("Punto_rosa",demo,28,new Color(1,.08f,.40f,1),knob);
            demo.gameObject.SetActive(false);
            // Mechanics card (top, under the title).
            var card=Node("Tarjeta_mecanicas",introRoot,new Vector2(40,188),new Vector2(600,58));
            var cardGroup=card.gameObject.AddComponent<CanvasGroup>();cardGroup.alpha=0;cardGroup.blocksRaycasts=false;
            Panel("Sombra",card,new Vector2(0,-5),new Vector2(600,58),29,Shadow,false);
            Panel("Borde",card,Vector2.zero,new Vector2(600,58),29,Ink,false);
            Panel("Fondo",card,new Vector2(0,1),new Vector2(592,50),25,Cream,false);
            var cardText=Label("Texto_mecanica",card,new Vector2(0,1),new Vector2(566,44),titan,26,Ink,"Levanta tu índice: verás un punto rosa.");
            // Saltar: small, above Tu cámara, away from Lucas and the route.
            var skip=Node("Boton_saltar",introRoot,new Vector2(516,-112),new Vector2(176,56));
            Panel("Sombra",skip,new Vector2(0,-5),new Vector2(176,56),28,Shadow,false);
            Panel("Borde",skip,Vector2.zero,new Vector2(176,56),28,Ink,true);
            Panel("Fondo",skip,new Vector2(0,1),new Vector2(168,48),24,Cream,true);
            Label("Texto_saltar",skip,new Vector2(0,5),new Vector2(170,36),titan,26,Ink,"Saltar  ›");
            Panel("Base_permanencia",skip,new Vector2(0,-15),new Vector2(110,5),2.5f,new Color(Ink.r,Ink.g,Ink.b,.15f),false);
            var skipFill=Filled("Progreso_permanencia",skip,new Vector2(0,-15),new Vector2(110,5),uiSprite,Gold,Image.FillMethod.Horizontal);
            var skipButton=skip.gameObject.AddComponent<HandButton>();skipButton.relleno=skipFill;
            // Opening: the original Quinta Insignia logo (high-resolution redraw) big at the centre, then into the title plate.
            var logo=Node("Logo_quinta_insignia",introRoot,new Vector2(0,40),new Vector2(600,120));
            var plate=Node("Placa_logo",logo,Vector2.zero,new Vector2(640,124));
            var logoPlate=plate.gameObject.AddComponent<CanvasGroup>();logoPlate.blocksRaycasts=false;
            Panel("Sombra",plate,new Vector2(0,-5),new Vector2(640,124),62,Shadow,false);
            Panel("Borde",plate,Vector2.zero,new Vector2(640,124),62,Ink,false);
            Panel("Fondo",plate,new Vector2(0,1),new Vector2(632,116),58,Cream,false);
            var logoImage=Node("Imagen_logo",logo,Vector2.zero,new Vector2(600,120)).gameObject.AddComponent<RawImage>();logoImage.raycastTarget=false;
            logoImage.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Nuevas/logo_quinta_insignia.png");
            logo.gameObject.AddComponent<CanvasGroup>().alpha=0;
            // Level card: NIVEL 2 · Ruta de Energía.
            var lvl=Node("Tarjeta_nivel",introRoot,new Vector2(0,10),new Vector2(560,190));
            var lvlGroup=lvl.gameObject.AddComponent<CanvasGroup>();lvlGroup.alpha=0;lvlGroup.blocksRaycasts=false;
            Panel("Sombra",lvl,new Vector2(0,-7),new Vector2(560,170),44,Shadow,false);
            Panel("Borde",lvl,new Vector2(0,-10),new Vector2(560,164),44,Ink,false);
            Panel("Fondo",lvl,new Vector2(0,-9),new Vector2(548,152),39,Cream,false);
            Panel("Sombra_insignia",lvl,new Vector2(0,63),new Vector2(250,50),25,Shadow,false);
            Panel("Insignia_nivel",lvl,new Vector2(0,67),new Vector2(250,50),25,Gold,false);
            Label("Texto_nivel",lvl,new Vector2(0,67),new Vector2(230,40),titan,30,Ink,"NIVEL 2");
            Label("Nombre_nivel",lvl,new Vector2(0,-18),new Vector2(520,90),titan,64,new Color(.20f,.37f,.47f,1),"Ruta de Energía");
            intro.fondoLogo=logoPlate;intro.logo=logo;intro.destinoLogo=title.Find("Placa") as RectTransform;intro.tarjetaNivel=lvlGroup;
            var routeGroup=route.GetComponent<CanvasGroup>();
            if(!routeGroup)routeGroup=route.gameObject.AddComponent<CanvasGroup>();
            routeGroup.alpha=1;routeGroup.blocksRaycasts=false;routeGroup.interactable=false;
            var walk=new Texture2D[4];
            for(int i=0;i<4;i++)walk[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Nuevas/lucas_camina_"+(i+1)+".png");
            intro.lucas=game.lucas;intro.lucasImagen=game.lucas.GetComponent<RawImage>();intro.lucasCaminando=walk;
            intro.sorpresa=surprise;intro.nube=cloudGroup;intro.textoNube=cloudText;
            intro.recorrido=routeGroup;intro.bateria=game.bateria;intro.luz=game.luz;intro.lobo=game.lobo;
            intro.puntoDemo=demo;intro.anilloDemo=demoRing;intro.tarjeta=cardGroup;intro.textoTarjeta=cardText;
            intro.grupo=introGroup;intro.noche=night;intro.saltar=skipButton;
            introRoot.gameObject.SetActive(false);

            // Closing screen (after the insignia): night, Lucas with the lit flashlight, what comes next in level 3.
            var closing=Node("Cierre_nivel",board,Vector2.zero,new Vector2(1280,720));
            closing.SetSiblingIndex(introRoot.GetSiblingIndex());
            var lucasLamp=Node("Lucas_con_linterna",closing,new Vector2(-330,-150),new Vector2(560,356));
            var lucasLampImg=lucasLamp.gameObject.AddComponent<RawImage>();lucasLampImg.raycastTarget=false;
            lucasLampImg.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Nuevas/lucas_linterna.png");
            var note=Node("Aviso_nivel_3",closing,new Vector2(250,30),new Vector2(540,300));
            Panel("Sombra",note,new Vector2(0,-7),new Vector2(540,300),40,Shadow,false);
            Panel("Borde",note,Vector2.zero,new Vector2(540,300),40,Ink,false);
            Panel("Fondo",note,new Vector2(0,1),new Vector2(530,290),35,Cream,false);
            Panel("Sombra_insignia",note,new Vector2(0,146),new Vector2(300,46),23,Shadow,false);
            Panel("Insignia_siguiente",note,new Vector2(0,150),new Vector2(300,46),23,Gold,false);
            Label("Texto_siguiente_nivel",note,new Vector2(0,150),new Vector2(280,36),titan,24,Ink,"SIGUIENTE · NIVEL 3");
            Label("Nombre_nivel_3",note,new Vector2(0,96),new Vector2(500,56),titan,44,new Color(.20f,.37f,.47f,1),"Cruce del bosque");
            var body=Label("Texto_cierre",note,new Vector2(0,26),new Vector2(490,84),fredoka?fredoka:titan,22,Ink,
                "Ya es de noche. Lucas necesita leña y comida\npara su campamento: tendrá que superar\nnuevos retos en el bosque.");
            body.resizeTextMinSize=15;
            var exit=Node("Boton_continuar_nivel_3",note,new Vector2(-72,-94),new Vector2(236,62));
            var green=new Color(.31f,.54f,.36f,1);
            Panel("Sombra",exit,new Vector2(0,-5),new Vector2(236,62),31,Shadow,false);
            Panel("Borde",exit,Vector2.zero,new Vector2(236,62),31,Ink,true);
            var exitBack=Panel("Fondo",exit,new Vector2(0,1),new Vector2(228,54),27,green,true);
            Panel("Brillo",exit,new Vector2(-70,18),new Vector2(36,6),3,new Color(1,1,1,.45f),false);
            var exitText=Label("Texto_continuar",exit,new Vector2(0,5),new Vector2(216,38),titan,28,Color.white,"Continuar");
            Panel("Base_permanencia",exit,new Vector2(0,-16),new Vector2(130,5),2.5f,new Color(1,1,1,.25f),false);
            var exitFill=Filled("Progreso_permanencia",exit,new Vector2(0,-16),new Vector2(130,5),uiSprite,Gold,Image.FillMethod.Horizontal);
            var exitButton=exit.gameObject.AddComponent<HandButton>();exitButton.relleno=exitFill;
            var again=Node("Boton_jugar_otra_vez",note,new Vector2(150,-94),new Vector2(176,52));
            Panel("Sombra",again,new Vector2(0,-4),new Vector2(176,52),26,Shadow,false);
            Panel("Borde",again,Vector2.zero,new Vector2(176,52),26,Ink,true);
            Panel("Fondo",again,new Vector2(0,1),new Vector2(170,46),23,Color.white,true);
            Label("Texto",again,new Vector2(0,4),new Vector2(160,30),titan,20,Ink,"Jugar otra vez");
            var againFill=Filled("Progreso_permanencia",again,new Vector2(0,-14),new Vector2(100,4),uiSprite,Gold,Image.FillMethod.Horizontal);
            var againButton=again.gameObject.AddComponent<HandButton>();againButton.relleno=againFill;
            closing.gameObject.SetActive(false);

            // Insignia reward: rays, glow, fireworks, the insignia and the congratulation card. Ends in the top-left corner.
            var cel=Node("Celebracion_insignia",board,Vector2.zero,new Vector2(1280,720));
            cel.SetSiblingIndex(introRoot.GetSiblingIndex());
            var celebration=cel.gameObject.AddComponent<CelebracionInsignia>();
            var rays=Node("Rayos",cel,new Vector2(0,70),new Vector2(640,640));
            var raysArt=rays.gameObject.AddComponent<RayosDeLuz>();raysArt.color=new Color(1,.9f,.55f,.5f);raysArt.raycastTarget=false;
            var raysGroup=rays.gameObject.AddComponent<CanvasGroup>();raysGroup.alpha=0;raysGroup.blocksRaycasts=false;
            var glow=Node("Brillo",cel,new Vector2(0,70),new Vector2(440,440)).gameObject.AddComponent<SoftGlow>();
            glow.color=new Color(1,.9f,.55f,0);glow.nucleo=.35f;glow.raycastTarget=false;
            var sparks=Node("Fuegos_artificiales",cel,Vector2.zero,new Vector2(1280,720)).gameObject.AddComponent<FuegosArtificiales>();
            var badge=Node("Insignia_ruta_de_energia",cel,new Vector2(0,70),new Vector2(300,300));
            var badgeImg=badge.gameObject.AddComponent<RawImage>();badgeImg.raycastTarget=false;
            badgeImg.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Usuario/insignia_ruta_de_energia.png");
            var congrats=Node("Tarjeta_felicidades",cel,new Vector2(30,-205),new Vector2(560,116));
            var congratsGroup=congrats.gameObject.AddComponent<CanvasGroup>();congratsGroup.alpha=0;congratsGroup.blocksRaycasts=false;
            Panel("Sombra",congrats,new Vector2(0,-6),new Vector2(560,116),36,Shadow,false);
            Panel("Borde",congrats,Vector2.zero,new Vector2(560,116),36,Ink,false);
            Panel("Fondo",congrats,new Vector2(0,1),new Vector2(550,106),32,Cream,false);
            Label("Texto_felicidades",congrats,new Vector2(0,22),new Vector2(520,52),titan,46,Ink,"¡Felicidades!");
            Label("Texto_insignia",congrats,new Vector2(0,-26),new Vector2(520,36),titan,24,new Color(.20f,.37f,.47f,1),"Has conseguido la insignia Ruta de Energía");
            celebration.insignia=badge;celebration.rayos=rays;celebration.grupoRayos=raysGroup;celebration.brillo=glow;
            celebration.tarjeta=congratsGroup;celebration.fuegos=sparks;
            cel.gameObject.SetActive(false);

            // Insignia shelf (top left, always visible): level-1 medal + slot of the level-2 insignia.
            // The won insignia flies into the slot; until then the slot shows a faint silhouette.
            var shelf=Node("Insignias_superiores",board,new Vector2(-536,316),new Vector2(176,78));
            shelf.SetSiblingIndex(cel.GetSiblingIndex());
            Panel("Sombra",shelf,new Vector2(0,-4),new Vector2(176,78),39,Shadow,false);
            Panel("Borde",shelf,Vector2.zero,new Vector2(176,78),39,Ink,false);
            Panel("Fondo",shelf,Vector2.zero,new Vector2(166,68),34,Cream,false);
            var medal=Node("Medalla_nivel_1",shelf,new Vector2(-42,0),new Vector2(66,66)).gameObject.AddComponent<RawImage>();medal.raycastTarget=false;
            medal.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(RutaEnergiaArte.MedallaDestino);
            var slot=Node("Ranura_insignia_nivel_2",shelf,new Vector2(42,0),new Vector2(66,66)).gameObject.AddComponent<RawImage>();slot.raycastTarget=false;
            slot.texture=badgeImg.texture;slot.color=new Color(Ink.r,Ink.g,Ink.b,.22f);
            celebration.esquina=shelf.anchoredPosition+new Vector2(42,0);celebration.tamanoEsquina=66;

            // Continuar (green, as in the Figma style guide). Shown only when the next scene can be loaded.
            var nextTemplate=game.nextAction.transform;
            var cont=Object.Instantiate(nextTemplate.gameObject,overlay).GetComponent<RectTransform>();
            cont.name="Boton_continuar_aventura";
            cont.SetSiblingIndex(game.retryRect.GetSiblingIndex()+1);
            cont.anchoredPosition=game.retryRect.anchoredPosition-new Vector2(game.separacionBotonesFinal,0);
            var contText=cont.Find("Texto_siguiente");
            if(contText){contText.name="Texto_continuar";contText.GetComponent<Text>().text="Continuar";}
            var contButton=cont.gameObject.AddComponent<HandButton>();
            cont.gameObject.SetActive(false);

            // Difficulty selector (Ready screen, after the tutorial). Figma button colours.
            var selector=Node("Selector_dificultad",overlay,new Vector2(0,-26),new Vector2(560,88));
            selector.SetSiblingIndex(game.retryRect.GetSiblingIndex()+1);
            HandButton Difficulty(string name,string label,string time,float x,Color fill,Color text)
            {
                var b=Node(name,selector,new Vector2(x,0),new Vector2(172,86));
                Panel("Sombra",b,new Vector2(0,-5),new Vector2(172,86),26,Shadow,false);
                Panel("Borde",b,Vector2.zero,new Vector2(172,86),26,Ink,true);
                Panel("Fondo",b,new Vector2(0,1),new Vector2(164,78),22,fill,true);
                Panel("Brillo",b,new Vector2(-46,30),new Vector2(36,6),3,new Color(1,1,1,.45f),false);
                Label("Texto",b,new Vector2(0,10),new Vector2(156,40),titan,32,text,label);
                Label("Tiempo",b,new Vector2(0,-17),new Vector2(150,24),titan,20,new Color(text.r,text.g,text.b,.85f),time);
                var fillBar=Filled("Progreso_permanencia",b,new Vector2(0,-33),new Vector2(110,5),uiSprite,Gold,Image.FillMethod.Horizontal);
                var hb=b.gameObject.AddComponent<HandButton>();hb.relleno=fillBar;
                return hb;
            }
            var easy=Difficulty("Boton_facil","Fácil","1:30",-188,new Color(.31f,.54f,.36f,1),Color.white);
            var medium=Difficulty("Boton_media","Media","1:00",0,Gold,Ink);
            var hard=Difficulty("Boton_dificil","Difícil","0:40",188,new Color(.92f,.47f,.31f,1),Color.white);
            selector.gameObject.SetActive(false);

            // Partida nueva: only when the first scene of the adventure is configured.
            var fresh=Node("Boton_partida_nueva",overlay,new Vector2(0,-198),new Vector2(236,52));
            Panel("Borde",fresh,Vector2.zero,new Vector2(236,52),26,Ink,true);
            Panel("Fondo",fresh,Vector2.zero,new Vector2(230,46),23,Color.white,true);
            Label("Texto",fresh,new Vector2(0,1),new Vector2(220,40),titan,22,Ink,"Partida nueva");
            var freshFill=Filled("Progreso_permanencia",fresh,new Vector2(0,-16),new Vector2(120,5),uiSprite,Gold,Image.FillMethod.Horizontal);
            var freshButton=fresh.gameObject.AddComponent<HandButton>();freshButton.relleno=freshFill;
            fresh.gameObject.SetActive(false);

            // Scene fade (entry and exit).
            var fade=Node("Fundido_pantalla",board,Vector2.zero,new Vector2(1280,720));
            fade.SetSiblingIndex(cursor.GetSiblingIndex());
            var fadeImage=fade.gameObject.AddComponent<Image>();fadeImage.color=new Color(.05f,.08f,.15f,1);fadeImage.raycastTarget=false;
            var fadeGroup=fade.gameObject.AddComponent<CanvasGroup>();fadeGroup.alpha=0;fadeGroup.blocksRaycasts=false;fadeGroup.interactable=false;

            // Menú (top right, next to the sound button) and the pause card, all held with the index.
            var menuButton=Node("Boton_menu",board,new Vector2(459,316),new Vector2(156,70));
            menuButton.SetSiblingIndex(sound.GetSiblingIndex()+1);
            Panel("Sombra",menuButton,new Vector2(0,-4),new Vector2(156,70),35,Shadow,false);
            Panel("Borde",menuButton,Vector2.zero,new Vector2(156,70),35,Ink,true);
            Panel("Fondo",menuButton,Vector2.zero,new Vector2(146,60),30,Cream,true);
            for(int i=0;i<3;i++)Panel("Linea_"+(i+1),menuButton,new Vector2(-40,13-i*9),new Vector2(28,5),2.5f,Ink,false);
            Label("Texto_menu",menuButton,new Vector2(16,4),new Vector2(86,36),titan,26,Ink,"Menú");
            var menuFill=Filled("Progreso_permanencia",menuButton,new Vector2(0,-21),new Vector2(96,5),uiSprite,Gold,Image.FillMethod.Horizontal);
            var openButton=menuButton.gameObject.AddComponent<HandButton>();openButton.relleno=menuFill;openButton.margen=2;

            var pause=Node("Menu_pausa",board,Vector2.zero,new Vector2(1280,720));
            pause.SetSiblingIndex(fade.GetSiblingIndex());
            Panel("Fondo_oscuro",pause,Vector2.zero,new Vector2(1280,720),0,new Color(.04f,.07f,.13f,.62f),true);
            var pc=Node("Tarjeta_menu",pause,new Vector2(0,-28),new Vector2(470,440));
            var pShadow=Panel("Sombra",pc,new Vector2(0,-7),new Vector2(470,440),40,Shadow,false);
            var pBorder=Panel("Borde",pc,Vector2.zero,new Vector2(470,440),40,Ink,false);
            var pBack=Panel("Fondo",pc,Vector2.zero,new Vector2(460,430),35,Cream,false);
            var head=Node("Cabecera",pc,new Vector2(0,214),new Vector2(240,48));
            Panel("Sombra_insignia",head,new Vector2(0,-4),new Vector2(220,48),24,Shadow,false);
            Panel("Insignia_menu",head,Vector2.zero,new Vector2(220,48),24,Gold,false);
            Label("Texto_menu",head,new Vector2(0,1),new Vector2(200,40),titan,28,Ink,"MENÚ");
            Label("Texto_pausa",head,new Vector2(0,-48),new Vector2(400,32),fredoka?fredoka:titan,22,new Color(.20f,.37f,.47f,1),"Juego en pausa");
            Text pauseLabel=null;
            HandButton PauseButton(string name,string label,Color fill,Color text)
            {
                var b=Node(name,pc,Vector2.zero,new Vector2(380,64));
                Panel("Sombra",b,new Vector2(0,-5),new Vector2(380,64),32,Shadow,false);
                Panel("Borde",b,Vector2.zero,new Vector2(380,64),32,Ink,true);
                Panel("Fondo",b,new Vector2(0,1),new Vector2(372,56),28,fill,true);
                Panel("Brillo",b,new Vector2(-150,19),new Vector2(36,6),3,new Color(1,1,1,.45f),false);
                pauseLabel=Label("Texto",b,new Vector2(0,5),new Vector2(350,38),titan,28,text,label);
                var bar=Filled("Progreso_permanencia",b,new Vector2(0,-18),new Vector2(150,5),uiSprite,Gold,Image.FillMethod.Horizontal);
                var hb=b.gameObject.AddComponent<HandButton>();hb.relleno=bar;
                return hb;
            }
            var keepPlaying=PauseButton("Boton_seguir_jugando","Seguir jugando",new Color(.31f,.54f,.36f,1),Color.white);
            var restart=PauseButton("Boton_reiniciar","Reiniciar",Color.white,Ink);var restartText=pauseLabel;
            var changeLevel=PauseButton("Boton_cambiar_dificultad","Cambiar dificultad",Gold,Ink);
            var toStart=PauseButton("Boton_volver_al_inicio","Volver al inicio",Color.white,Ink);
            var menuComp=game.gameObject.AddComponent<MenuJuego>();
            menuComp.botonAbrir=openButton;menuComp.panel=pause.gameObject;
            menuComp.seguir=keepPlaying;menuComp.reiniciar=restart;menuComp.dificultad=changeLevel;menuComp.inicio=toStart;
            menuComp.textoReiniciar=restartText;menuComp.cabecera=head;
            menuComp.marcos=new[]{pShadow.rectTransform,pBorder.rectTransform,pBack.rectTransform};
            menuComp.alturaCabecera=104;menuComp.separacion=80;menuComp.margenInferior=24;
            pause.gameObject.SetActive(false);

            // Hand tracking inside Unity (MediaPipe Hands models, no Python).
            var tracker=new GameObject("Seguimiento_mano_Unity").AddComponent<SeguimientoUnity>();
            tracker.detectorPalma=AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/RecorridoAR/SeguimientoMano/Modelos/hand_detector.onnx");
            tracker.puntosMano=AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/RecorridoAR/SeguimientoMano/Modelos/hand_landmarks_detector.onnx");
            tracker.anclas=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/RecorridoAR/SeguimientoMano/Datos/anclas_mano.csv");
            if(!tracker.detectorPalma||!tracker.puntosMano||!tracker.anclas)Debug.LogError("Faltan modelos de seguimiento de mano");
            // Hand points drawn over Tu cámara (what the camera is reading).
            var camFrame=game.cameraImage.rectTransform;
            Remove(camFrame.parent,"Puntos_mano");
            var dots=Node("Puntos_mano",camFrame.parent,camFrame.anchoredPosition,camFrame.sizeDelta);
            dots.SetSiblingIndex(camFrame.GetSiblingIndex()+1);
            var dotsArt=dots.gameObject.AddComponent<PuntosManoVista>();dotsArt.seguimiento=tracker;dotsArt.juego=game;
            // Sharper UI: pixel snapping makes scaled and animated art look jagged.
            var rootCanvas=board.GetComponentInParent<Canvas>().rootCanvas;rootCanvas.pixelPerfect=false;
            var links=new GameObject("Aventura_Quinta_Insignia").AddComponent<AventuraEnlaces>();
            links.nivel=2;links.fundidoPantalla=fadeGroup;

            var audio=new GameObject("Audio_Ruta_de_Energia").AddComponent<AudioDirector>();
            audio.musica=Clip("musica_sendero_atardecer");
            audio.musicaFacil=Clip("musica_dificultad_facil");audio.musicaMedia=Clip("musica_dificultad_media");audio.musicaDificil=Clip("musica_dificultad_dificil");
            audio.boton=Clip("sfx_boton_confirmar");audio.recoger=Clip("sfx_bateria_recoger");audio.entregar=Clip("sfx_bateria_entregar");
            audio.etapa=Clip("sfx_etapa_completada");audio.lobo=Clip("sfx_lobo_aullido");audio.fallo=Clip("sfx_fallo");
            audio.linterna=Clip("sfx_linterna_encender");audio.premio=Clip("sfx_insignia_premio");audio.fuegos=Clip("sfx_fuegos_artificiales");

            Undo.RecordObject(game,"Conectar introduccion y audio");
            game.intro=intro;game.noche=night;game.haloLinterna=haloArt;game.botonSonido=soundButton;game.iconoSonido=icon;
            game.botonContinuar=contButton;game.botonPartidaNueva=freshButton;game.enlaces=links;game.botonFinalizar=null;game.celebracion=celebration;game.botonJugarOtraVez=againButton;game.duracionCierre=2.6f;game.ambiente=ambient;game.selectorDificultad=selector.gameObject;game.botonFacil=easy;game.botonMedia=medium;game.botonDificil=hard;game.cierreNivel=closing.gameObject;game.botonSalir=exitButton;game.textoBotonSalir=exitText;game.fondoBotonSalir=exitBack;game.camaraUnity=tracker;game.menu=menuComp;game.ranuraNivel2=slot;
            game.tituloSuperior=title.Find("Juego_general")?.GetComponent<Text>();
            game.tituloPrincipal=title.Find("Nombre_del_minijuego")?.GetComponent<Text>();
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Ruta de Energía: introducción, audio y enlaces construidos en "+ScenePath);
        }

        static void Remove(Transform parent,string name){ var t=parent.Find(name); if(t)Object.DestroyImmediate(t.gameObject); }
        static RectTransform Node(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));
            go.layer=5;
            var r=go.GetComponent<RectTransform>();
            r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
            r.anchoredPosition=pos;r.sizeDelta=size;
            return r;
        }
        static RoundedPanel Panel(string name,Transform parent,Vector2 pos,Vector2 size,float radius,Color color,bool raycast)
        {
            var p=Node(name,parent,pos,size).gameObject.AddComponent<RoundedPanel>();
            p.radius=radius;p.color=color;p.lowerColor=color;p.gradient=0;p.raycastTarget=raycast;
            return p;
        }
        static Image Circle(string name,Transform parent,float d,Color c,Sprite sprite)
        {
            var img=Node(name,parent,Vector2.zero,new Vector2(d,d)).gameObject.AddComponent<Image>();
            img.sprite=sprite;img.color=c;img.raycastTarget=false;
            return img;
        }
        static Image Filled(string name,Transform parent,Vector2 pos,Vector2 size,Sprite sprite,Color color,Image.FillMethod method)
        {
            var img=Node(name,parent,pos,size).gameObject.AddComponent<Image>();
            img.sprite=sprite;img.color=color;img.type=Image.Type.Filled;img.fillMethod=method;
            img.fillOrigin=method==Image.FillMethod.Radial360?2:0;img.fillAmount=0;img.raycastTarget=false;
            return img;
        }
        static Text Label(string name,Transform parent,Vector2 pos,Vector2 size,Font font,int fontSize,Color color,string text)
        {
            var t=Node(name,parent,pos,size).gameObject.AddComponent<Text>();
            t.font=font;t.fontSize=fontSize;t.color=color;t.text=text;t.alignment=TextAnchor.MiddleCenter;
            t.resizeTextForBestFit=true;t.resizeTextMinSize=14;t.resizeTextMaxSize=fontSize;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;
            return t;
        }
        static AudioClip Clip(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Audio+name+".wav");

        // Music streams (small memory); short effects decompress on load to play without delay.
        static void ConfigurarAudio()
        {
            AssetDatabase.Refresh();
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/RecorridoAR/Audio"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                if(AssetImporter.GetAtPath(path) is not AudioImporter importer)continue;
                bool music=path.Contains("musica_");
                var s=importer.defaultSampleSettings;
                s.loadType=music?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat=AudioCompressionFormat.Vorbis;
                s.quality=music?.7f:.85f;
                s.preloadAudioData=!music;
                importer.defaultSampleSettings=s;
                importer.forceToMono=true;
                importer.loadInBackground=music;
                importer.SaveAndReimport();
            }
        }
    }
}
