using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitFlyParenting.Editor
{
    public static class DesktopBuildSetup
    {
        [MenuItem("Fruit Fly Parenting/Repair Legacy Comfort References")]
        public static void RepairComfortReferences()
        {
            const string rigPath="Assets/_Project/Prefabs/VR/WheelchairRig.prefab";
            var rig=PrefabUtility.LoadPrefabContents(rigPath);
            int removed=0;
            foreach(var transform in rig.GetComponentsInChildren<Transform>(true))
                removed+=GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            PrefabUtility.SaveAsPrefabAsset(rig,rigPath); PrefabUtility.UnloadPrefabContents(rig);
            foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
            {
                var scene=EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Single);
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                        removed+=GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                EditorSceneManager.SaveScene(scene);
            }
            ComfortSetup.Build();
            // A repaired prefab can inherit a component that was previously supplied by a scene override.
            foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
            {
                var scene=EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Single);
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var chair in root.GetComponentsInChildren<FruitFlyParenting.Wheelchair.WheelchairLocomotion>(true))
                    {
                        var settings=chair.GetComponents<FruitFlyParenting.Comfort.ComfortController>();
                        for(int i=0;i<settings.Length-1;i++) UnityEngine.Object.DestroyImmediate(settings[i]);
                    }
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Removed stale comfort script references: "+removed);
        }
        [MenuItem("Fruit Fly Parenting/Build PC and PCVR Player")]
        public static void Build()
        {
            BuildTo("Builds/PC");
        }
        [MenuItem("Fruit Fly Parenting/Build Playtest PC Player")]
        public static void BuildPlaytest() => BuildTo("Builds/PlaytestPC");

        [MenuItem("Fruit Fly Parenting/Build Flat HUD Player")]
        public static void BuildFlatHud() => BuildTo("Builds/FlatHUD");

        static void BuildTo(string output)
        {
            PlayerSettings.runInBackground = true;
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            foreach(var path in scenes)
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)>0)
                            throw new InvalidOperationException("Missing component in "+path+": "+transform.name);
            }
            Directory.CreateDirectory(output);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=scenes, target=BuildTarget.StandaloneWindows64, locationPathName=output+"/FruitFlyParenting.exe",
                options=BuildOptions.DetailedBuildReport | BuildOptions.CleanBuildCache
            });
            if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("PC build failed.");
            Debug.Log("PC/PCVR release build succeeded: "+output+"/FruitFlyParenting.exe");
        }
    }
    public sealed class DesktopSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 100;
        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            if(report==null || report.summary.platform!=BuildTarget.StandaloneWindows64) return;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var gizmo in root.GetComponentsInChildren<FruitFlyParenting.Room.RoomCoordinateGizmos>(true))
                    UnityEngine.Object.DestroyImmediate(gizmo);
        }
    }
}
