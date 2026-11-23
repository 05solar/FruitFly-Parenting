using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.OpenXR;

namespace FruitFlyParenting.Tests.EditMode
{
    public class ProjectConfigurationTests
    {
        [Test]
        public void AllQualityLevels_UsePipelineWithValidDefaultRenderer()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(pipeline);
            Assert.IsNotNull(pipeline.rendererDataList[0]);
            Assert.IsNotNull(pipeline.scriptableRenderer);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                Assert.AreEqual(pipeline, QualitySettings.GetRenderPipelineAssetAt(i));
        }

        [Test]
        public void SavedRoomPrefab_HasPersistentNavMeshMatchingFlySize()
        {
            var room = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Rooms/RoomShell.prefab");
            var surface = room.GetComponent<NavMeshSurface>();
            Assert.IsNotNull(surface.navMeshData, "A transient test bake cannot replace saved scene data.");
            Assert.IsTrue(AssetDatabase.Contains(surface.navMeshData));
            var settings = surface.GetBuildSettings();
            Assert.AreEqual(0.25f, settings.agentRadius, 0.001f);
            Assert.AreEqual(0.6f, settings.agentHeight, 0.001f);
        }

        [Test]
        public void XRRig_HasHeadAndBothHandBindings_WithoutPrematureLocomotion()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VR/XROrigin.prefab");
            var origin = rig.GetComponent<XROrigin>();
            Assert.IsNotNull(origin.Camera);
            Assert.IsNotNull(origin.CameraFloorOffsetObject);
            var tracking = rig.GetComponentsInChildren<TrackedPoseDriver>(true);
            Assert.AreEqual(3, tracking.Length);
            foreach (var driver in tracking)
            {
                Assert.IsNotNull(driver.positionInput.reference);
                Assert.IsNotNull(driver.rotationInput.reference);
                Assert.IsNotNull(driver.trackingStateInput.reference);
                Assert.IsNotEmpty(driver.positionInput.action.bindings);
            }
            Assert.IsEmpty(rig.GetComponentsInChildren<LocomotionProvider>(true));
        }

        [Test]
        public void PCOpenXR_StartsAutomatically_WithControllerProfile()
        {
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            Assert.IsTrue(general.InitManagerOnStart);
            // GeneralSettings가 자동 초기화를 소유한다. Manager의 MonoBehaviour 생명주기와 중복 금지.
            Assert.IsFalse(general.Manager.automaticLoading);
            Assert.IsFalse(general.Manager.automaticRunning);
            Assert.IsTrue(general.Manager.activeLoaders.Any(loader => loader is OpenXRLoader));
            Assert.IsTrue(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone).GetFeatures()
                .Any(feature => feature.enabled && feature.GetType().Name == "OculusTouchControllerProfile"));
        }
    }
}
