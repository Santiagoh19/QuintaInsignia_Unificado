using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RecorridoAR.Editor
{
    // Windows build that tracks the hand inside Unity: copy the whole folder to another PC with a webcam and run the .exe.
    public static class RutaEnergiaCompilar
    {
        [MenuItem("Ruta de Energía/Compilar para Windows")]
        public static void Windows()
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","..","Entrega_final","RutaDeEnergia_Windows"));
            Directory.CreateDirectory(dir);
            var options=new BuildPlayerOptions
            {
                scenes=new[]{"Assets/Scenes/Recorrido.unity"},
                locationPathName=Path.Combine(dir,"RutaDeEnergia.exe"),
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.None
            };
            var report=BuildPipeline.BuildPlayer(options);
            Debug.Log("Compilación: "+report.summary.result+" · "+(report.summary.totalSize/1048576f).ToString("0.0")+" MB · "+dir);
            if(report.summary.result!=BuildResult.Succeeded && Application.isBatchMode)EditorApplication.Exit(1);
        }

        // Everything for the delivery in one run: rebuild the scene, Windows build, Unity package.
        public static void Todo()
        {
            RutaEnergiaEscena.Construir();
            Windows();
            Paquete();
        }

        // Unity package with the whole level (scene, scripts, art, audio, models, tests).
        // Required packages in the target project: com.unity.ai.inference 2.4.0, com.unity.inputsystem, com.unity.ugui.
        [MenuItem("Ruta de Energía/Exportar paquete de Unity")]
        public static void Paquete()
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","..","Entrega_final"));
            Directory.CreateDirectory(dir);
            string file=Path.Combine(dir,"RutaDeEnergia_Nivel2.unitypackage");
            AssetDatabase.ExportPackage(new[]{"Assets/Scenes/Recorrido.unity","Assets/RecorridoAR"},file,
                ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
            Debug.Log("Paquete: "+(new FileInfo(file).Length/1048576f).ToString("0.0")+" MB · "+file);
        }
    }
}
