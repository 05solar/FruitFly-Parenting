using System.IO;
using System.Linq;
using FruitFlyParenting.Comfort;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using FruitFlyParenting.Wheelchair;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitFlyParenting.Editor
{
    public static class ComfortSetup
    {
        const string Root = "Assets/_Project/";
        [MenuItem("Fruit Fly Parenting/Setup 14 Comfort and Accessibility")]
        public static void Build()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Art/Fonts/NotoSansCJKkr-Regular.otf");
            string overlayPath = Root + "Art/Materials/ComfortOverlay.mat";
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(overlayPath);
            if (overlay == null)
            {
                overlay = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Root + "Art/Shaders/ComfortOverlay.shader"));
                AssetDatabase.CreateAsset(overlay, overlayPath);
            }
            const string rigPath = Root + "Prefabs/VR/WheelchairRig.prefab";
            var rig = PrefabUtility.LoadPrefabContents(rigPath);
            AddSettings(rig.GetComponent<WheelchairLocomotion>(), font, overlay);
            PrefabUtility.SaveAsPrefabAsset(rig, rigPath); PrefabUtility.UnloadPrefabContents(rig);
            string materialPath = Root + "Art/Materials/ApproachShadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, materialPath); }
            var cue = Clip("ItemApproach");
            string[] sounds = { "InfantLullaby", "InfantLullaby", "ChildhoodPlay", "AdolescenceAmbience", "YoungAdultAmbience", "MiddleAgeAmbience" };
            foreach (var guid in AssetDatabase.FindAssets("t:RoomTransitionDefinition", new[] { Root + "ScriptableObjects/Transitions" }))
            {
                var route = AssetDatabase.LoadAssetAtPath<RoomTransitionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                route.previousRoomSound = Clip(sounds[(int)route.fromStage]); route.nextRoomSound = Clip(sounds[(int)route.toStage]);
                EditorUtility.SetDirty(route);
            }
            int slots = 0, rigs = 0;
            foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                foreach (var chair in Object.FindObjectsByType<WheelchairLocomotion>(FindObjectsInactive.Include)) { AddSettings(chair, font, overlay); rigs++; }
                foreach (var slot in Object.FindObjectsByType<ItemSlot>(FindObjectsInactive.Include))
                {
                    var feedback = slot.GetComponent<ItemApproachFeedback>() ?? slot.gameObject.AddComponent<ItemApproachFeedback>();
                    feedback.Configure(material, cue); EditorUtility.SetDirty(feedback); slots++;
                }
                if (scene.name == "00_Bootstrap" || scene.name == "06_MiddleAge")
                {
                    var existing = GameObject.Find("Stage ambience");
                    var source = existing != null ? existing.GetComponent<AudioSource>() : new GameObject("Stage ambience").AddComponent<AudioSource>();
                    source.clip = Clip(scene.name == "00_Bootstrap" ? "InfantLullaby" : "MiddleAgeAmbience");
                    source.loop = true; source.playOnAwake = true; source.volume = .12f;
                }
                EditorSceneManager.SaveScene(scene);
            }
            var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root + "ScriptableObjects/GameBalanceConfig.asset");
            if (config.roomFadeSeconds < .5f) { config.roomFadeSeconds = .5f; EditorUtility.SetDirty(config); }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/step14-scene-audit.json", "{\"scenes\":" + EditorBuildSettings.scenes.Count(s => s.enabled) + ",\"rigs\":" + rigs + ",\"slots\":" + slots + "}");
            Debug.Log($"Step14 applied to {rigs} rigs and {slots} item slots across all main scenes.");
        }
        static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/" + name + ".wav");
            if (clip == null) throw new FileNotFoundException("Run python tools/generate_comfort_audio.py first: " + name);
            return clip;
        }
        static void AddSettings(WheelchairLocomotion chair, Font font, Material overlay)
        {
            var settings = chair.GetComponent<ComfortController>() ?? chair.gameObject.AddComponent<ComfortController>();
            settings.Configure(font, overlay); EditorUtility.SetDirty(settings);
        }
    }
}
