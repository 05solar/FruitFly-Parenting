using System.Linq;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FruitFlyParenting.Editor
{
    // Build-time scene copies only: PC authoring, NavMesh, story props and rig transforms stay untouched.
    public sealed class QuestSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 100;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.Android) return;
            Apply(scene);
        }
        public static bool IsFixedShell(Renderer renderer)
        {
            if (!(renderer is MeshRenderer)) return false;
            string name = renderer.name;
            // These are authored, immobile primitives. Never batch the rig, fly, doors or narrative props.
            return name == "Floor" || name.StartsWith("Wall_") || name.StartsWith("Furniture_") ||
                name == "North Wall" || name == "South Wall" || name == "Ceiling";
        }
        public static void Apply(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var gizmo in root.GetComponentsInChildren<RoomCoordinateGizmos>(true)) Object.DestroyImmediate(gizmo);
                foreach (var hud in root.GetComponentsInChildren<DebugHud>(true)) Object.DestroyImmediate(hud);
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.allowHDR = false;
                    var data = camera.GetComponent<UniversalAdditionalCameraData>();
                    if (data != null) data.renderPostProcessing = false;
                }
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Directional)
                    {
                        // Preserve the prologue pulse and ending's outcome-dependent light color.
                        light.enabled = light.enabled && (light.name == "Living Room Light" || light.name == "Heartbeat" || light.name == "Section Light 1");
                        light.shadows = LightShadows.None;
                    }
                    else { light.shadows = LightShadows.Hard; light.shadowCustomResolution = 1024; }
                }
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true).Where(IsFixedShell))
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                        GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
