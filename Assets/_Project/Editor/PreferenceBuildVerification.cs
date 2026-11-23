using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Fly;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitFlyParenting.Editor
{
    public static class PreferenceBuildVerification
    {
        // Builds copies, preserving the authored scene and all player/build settings.
        public static void BuildVariants()
        {
            const string source="Assets/_Project/Scenes/03_RoomShellTest.unity";
            const string temporary="Assets/_Project/Scenes/Step08BuildVerification.unity";
            if(File.Exists(temporary)) throw new InvalidOperationException("Verification scene path already exists.");
            const string xrEditorSettings="Assets/XR/Settings/OpenXR Editor Settings.asset";
            var paths=new[]{"Assets/DefaultVolumeProfile.asset","Assets/UniversalRenderPipelineGlobalSettings.asset",
                "Assets/_Project/Settings/URP-Default.asset",xrEditorSettings,xrEditorSettings+".meta"}
                .Concat(Directory.GetFiles("ProjectSettings","*.asset"));
            var backups=new Dictionary<string,byte[]>();
            foreach(var path in paths) backups[path]=File.Exists(path)?File.ReadAllBytes(path):null;
            var scene=EditorSceneManager.OpenScene(source,OpenSceneMode.Single);
            new GameObject("Preference build probe").AddComponent<PreferenceBuildProbe>();
            try
            {
                EditorSceneManager.SaveScene(scene,temporary);
                foreach(bool development in new[]{true,false})
                {
                    string output="Builds/Step08"+(development?"Development":"Release")+"/FruitFlyParenting.exe";
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                        scenes=new[]{temporary},locationPathName=output,target=BuildTarget.StandaloneWindows64,
                        options=development?BuildOptions.Development:BuildOptions.None,
                        extraScriptingDefines=new[]{"PREFERENCE_VERIFICATION"}
                    });
                    if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Verification build failed: "+output);
                    Debug.Log("Step08 verified build compilation: "+output);
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(source,OpenSceneMode.Single);
                AssetDatabase.DeleteAsset(temporary);
                // URP/OpenXR build preprocessors serialize caches into authored settings.
                // Flush their changes, then restore the exact pre-verification bytes.
                AssetDatabase.SaveAssets();
                foreach(var backup in backups)
                {
                    if(backup.Value!=null) File.WriteAllBytes(backup.Key,backup.Value);
                    else if(File.Exists(backup.Key)) File.Delete(backup.Key);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
