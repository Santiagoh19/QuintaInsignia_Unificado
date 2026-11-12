using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RecorridoAR.Editor
{
    // Renders design views of the intro and the ending to Documentacion/Vistas (1920 x 1080).
    // They are editor renders of the scene, not gameplay captures. The scene is reopened without saving.
    public static class RutaEnergiaCaptura
    {
        [MenuItem("Ruta de Energía/Capturar vistas de diseno")]
        public static void Capturar()
        {
            if(EditorApplication.isPlaying)return;
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Recorrido.unity",OpenSceneMode.Single);
            var game=Object.FindFirstObjectByType<RecorridoGame>(FindObjectsInactive.Include);
            var canvas=game.board.GetComponentInParent<Canvas>().rootCanvas;
            var camGo=new GameObject("Camara_captura");
            var cam=camGo.AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.orthographic=true;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Documentacion","Vistas"));
            Directory.CreateDirectory(dir);

            var intro=game.intro;
            var path=new RouteRules(1,true);
            game.drawing.route=path;game.drawing.SetAllDirty();
            var title=game.tituloPrincipal; var small=game.tituloSuperior;
            string levelName=title?title.text:"";
            intro.gameObject.SetActive(true);
            game.retryObject.SetActive(false);
            if(intro.grupo)intro.grupo.alpha=1;
            if(title)title.text=game.tituloIntroduccion;
            if(small)small.gameObject.SetActive(false);
            var menuButton=game.menu&&game.menu.botonAbrir?game.menu.botonAbrir.gameObject:null;
            if(menuButton)menuButton.SetActive(false);   // no Menú during the intro
            float[] times={0.9f,4.2f,7.0f,10.6f,13.6f,17.3f,21.6f,24.6f};
            for(int i=0;i<times.Length;i++)
            {
                game.routeLayer.SetActive(times[i]-intro.apertura>=9.8f);
                if(title)title.gameObject.SetActive(!intro.logo);
                intro.PreviewAt(times[i],path);
                Shot(cam,Path.Combine(dir,"Intro_"+(i+1)+".png"));
            }
            intro.PreviewAt(intro.duracion+intro.salida,path);
            intro.gameObject.SetActive(false);
            game.routeLayer.SetActive(false);
            if(title){title.text=game.nombreDelNivel;title.gameObject.SetActive(true);}
            if(intro.logo)intro.logo.gameObject.SetActive(false);
            if(small)small.gameObject.SetActive(true);
            game.lucas.anchoredPosition=new Vector2(-315,-266);game.lucas.localRotation=Quaternion.identity;game.lucas.localScale=Vector3.one;
            game.noche.Set(0,true);
            game.retryObject.SetActive(true);
            game.overlayBadge.text="QUINTA INSIGNIA · NIVEL 2";game.overlayTitle.text="Elige la dificultad";
            game.overlayBody.text="Cada dificultad tiene 3 baterías con recorridos distintos.";
            if(game.selectorDificultad){game.selectorDificultad.SetActive(true);game.retryRect.gameObject.SetActive(false);}
            game.overlayHint.text="Mantén el índice sobre el botón";
            game.actionImage.texture=game.jugarPNG;
            if(game.ambiente)game.ambiente.PreviewAt(12,0,true);
            if(menuButton)menuButton.SetActive(true);
            Shot(cam,Path.Combine(dir,"Pantalla_Jugar.png"));
            if(game.menu)
            {
                game.menu.Abrir(true,true,"Fácil");
                Shot(cam,Path.Combine(dir,"Menu_pausa.png"));
                game.menu.Cerrar();
            }

            if(game.selectorDificultad){game.selectorDificultad.SetActive(false);game.retryRect.gameObject.SetActive(true);}
            // The nine routes (3 per difficulty), drawn alone on the board.
            game.retryObject.SetActive(false);game.routeLayer.SetActive(true);game.gameHud.SetActive(true);
            var routeGroup=game.routeLayer.GetComponent<CanvasGroup>();if(routeGroup)routeGroup.alpha=1;
            string[] names={"Facil","Media","Dificil"};
            for(int d=1;d<=3;d++)for(int e=1;e<=3;e++)
            {
                game.drawing.route=RouteRules.Crear(d,e);game.drawing.SetAllDirty();
                game.hudLevel.text=CampaignRules.Name(d).ToUpper()+" · ETAPA "+e+" / 3";
                game.clock.text=d==1?"01:30":d==2?"01:00":"00:40";
                Shot(cam,Path.Combine(dir,"Recorrido_"+names[d-1]+"_"+e+".png"));
            }
            game.retryObject.SetActive(true);
            game.routeLayer.SetActive(true);game.gameHud.SetActive(true);
            game.bateria.gameObject.SetActive(false);
            game.noche.Set(.7f,true);
            game.luz.intensity=1;game.luz.SetVerticesDirty();
            var c=game.haloLinterna.color;c.a=.9f;game.haloLinterna.color=c;game.haloLinterna.rectTransform.localScale=Vector3.one*1.1f;
            game.retryObject.SetActive(false);game.botonSonido.gameObject.SetActive(true);
            game.hudLevel.text="MEDIA · ETAPA 3 / 3";game.hudBatteries.text="3 / 3";
            if(game.batterySlots!=null)foreach(var slot in game.batterySlots)if(slot)slot.color=Color.white;
            if(menuButton)menuButton.SetActive(false);   // hidden during the reward
            if(game.celebracion){game.celebracion.PreviewAt(1.7f);Shot(cam,Path.Combine(dir,"Premio_insignia.png"));}
            if(menuButton)menuButton.SetActive(true);
            if(game.ranuraNivel2)game.ranuraNivel2.enabled=false;
            game.routeLayer.SetActive(false);game.gameHud.SetActive(false);
            game.lucas.gameObject.SetActive(false);game.noche.Set(.9f,true);
            if(game.cierreNivel)game.cierreNivel.SetActive(true);
            if(game.celebracion)game.celebracion.MostrarEnEsquina();
            if(game.ambiente)game.ambiente.PreviewAt(30,.9f,false);
            Shot(cam,Path.Combine(dir,"Cierre_noche_nivel_3.png"));

            Object.DestroyImmediate(camGo);
            EditorSceneManager.OpenScene(scene.path,OpenSceneMode.Single); // discard preview changes
            Debug.Log("Vistas de diseño guardadas en "+dir);
        }
        static void Shot(Camera cam,string path)
        {
            const int w=1920,h=1080;
            var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);
            cam.targetTexture=rt;cam.aspect=w/(float)h;
            Canvas.ForceUpdateCanvases();
            foreach(var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);}
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active=rt;
            var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
            RenderTexture.active=null;cam.targetTexture=null;
            File.WriteAllBytes(path,tex.EncodeToPNG());
            Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
        }
    }
}
