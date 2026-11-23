using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using FruitFlyParenting.Wheelchair;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    // Unity API를 통해 생성/저장한다. 다시 실행해도 기존 에셋의 GUID는 보존한다.
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project/";
        const string RigPath = Root + "Prefabs/VR/XROrigin.prefab";
        const string NavPath = Root + "Prefabs/Rooms/RoomShellNavMesh.asset";
        const string RoomPath = Root + "Prefabs/Rooms/RoomShell.prefab";
        const string InputPath = "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/XRI Default Input Actions.inputactions";

        [MenuItem("Fruit Fly Parenting/Repair 01-03 Setup")]
        public static void Repair()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "Prefabs/VR");
            AssetDatabase.Refresh();
            RepairRendering();
            ConfigureXR();
            CreateRig();
            BakeRoom();
            ConfigureScenes();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ToolSetup.RigPath) != null) ToolSetup.Build();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(InfantRoomSetup.ScenePath) != null) InfantRoomSetup.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSetup: rendering, saved NavMesh, XR rig and PC/Android OpenXR settings repaired.");
        }

        static void RepairRendering()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root + "Settings/URP-Default_Renderer.asset");
            if (renderer == null) throw new InvalidOperationException("URP renderer asset cannot be loaded.");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "Settings/URP-Default.asset");
            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            if (pipeline.scriptableRenderer == null) throw new InvalidOperationException("URP default renderer is missing.");
        }

        static void ConfigureXR()
        {
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            foreach (var target in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                if (!perTarget.HasManagerSettingsForBuildTarget(target))
                    perTarget.CreateDefaultManagerSettingsForBuildTarget(target);
                var general = perTarget.SettingsForBuildTarget(target);
                var manager = general.Manager;
                if (!XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, target))
                    throw new InvalidOperationException($"Could not assign OpenXR for {target}");
                general.InitManagerOnStart = true;
                // XRGeneralSettings가 로더를 초기화/시작한다. Manager 자체 자동 실행은 중복이므로 끈다.
                manager.automaticLoading = false;
                manager.automaticRunning = false;
                FeatureHelpers.RefreshFeatures(target);
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
                if (settings == null)
                {
                    if (target != BuildTargetGroup.Android || BuildPipeline.IsBuildTargetSupported(target, BuildTarget.Android))
                        throw new InvalidOperationException($"Missing OpenXR settings for {target}");
                    EditorUtility.SetDirty(manager);
                    EditorUtility.SetDirty(general);
                    Debug.Log("ProjectSetup: Android loader prepared; Android Build Support is not installed. Install it and rerun setup for Quest feature settings.");
                    continue;
                }
                foreach (var feature in settings.GetFeatures())
                {
                    string name = feature.GetType().Name;
                    if (name == "OculusTouchControllerProfile" || name == "MetaQuestTouchPlusControllerProfile" ||
                        (target == BuildTargetGroup.Standalone && (name == "ValveIndexControllerProfile" || name == "HTCViveControllerProfile")))
                    {
                        feature.enabled = true;
                        EditorUtility.SetDirty(feature);
                    }
                }
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(manager);
                EditorUtility.SetDirty(general);
            }
            EditorUtility.SetDirty(perTarget);
            // OpenXR requires the new Input System; legacy Input Manager is unused.
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            playerSettings.FindProperty("activeInputHandler").intValue = 1;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateRig()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (actions == null) throw new InvalidOperationException("Import XRI 3.7.0 Starter Assets before running setup.");
            var root = new GameObject("XR Origin");
            var origin = root.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(root.transform, false);
            origin.Origin = root;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 1.2f; // seated placeholder; calibrated in step 14
            offset.transform.localPosition = Vector3.up * origin.CameraYOffset;
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(offset.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            origin.Camera = camera;
            AddTracking(cameraGo, actions, "XRI Head");
            foreach (string hand in new[] { "Left", "Right" })
            {
                var controller = new GameObject(hand + " Controller");
                controller.transform.SetParent(offset.transform, false);
                AddTracking(controller, actions, "XRI " + hand);
            }
            root.AddComponent<InputActionManager>().actionAssets = new List<InputActionAsset> { actions };
            PrefabUtility.SaveAsPrefabAsset(root, RigPath);
            Object.DestroyImmediate(root);
        }

        static void AddTracking(GameObject go, InputActionAsset actions, string map)
        {
            var driver = go.AddComponent<TrackedPoseDriver>();
            driver.positionInput = ActionProperty(actions, map + "/Position");
            driver.rotationInput = ActionProperty(actions, map + "/Rotation");
            driver.trackingStateInput = ActionProperty(actions, map + "/Tracking State");
        }

        static InputActionProperty ActionProperty(InputActionAsset actions, string path)
        {
            var action = actions.FindAction(path, true);
            var reference = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>()
                .First(r => r.action.id == action.id);
            return new InputActionProperty(reference);
        }

        static void BakeRoom()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // This project's only NavMesh is for the fly. The wheelchair uses physical colliders.
            var navSettingsObjects = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            var navSettings = new SerializedObject(navSettingsObjects[0]);
            var agent = navSettings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            agent.FindPropertyRelative("agentRadius").floatValue = 0.25f;
            agent.FindPropertyRelative("agentHeight").floatValue = 0.6f;
            agent.FindPropertyRelative("agentClimb").floatValue = 0.1f;
            navSettings.FindProperty("m_SettingNames").GetArrayElementAtIndex(0).stringValue = "Fruit Fly";
            navSettings.ApplyModifiedPropertiesWithoutUndo();
            var room = RoomShellBuilder.BuildAndBake();
            var surface = room.GetComponent<NavMeshSurface>();
            var baked = surface.navMeshData;
            if (baked == null) throw new InvalidOperationException("Room bake produced no data.");
            baked.name = Path.GetFileNameWithoutExtension(NavPath);
            surface.RemoveData();
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (existing == null)
                AssetDatabase.CreateAsset(baked, NavPath);
            else
            {
                EditorUtility.CopySerialized(baked, existing);
                Object.DestroyImmediate(baked);
                baked = existing;
            }
            EditorUtility.SetDirty(baked);
            surface.navMeshData = baked;
            surface.AddData();
            PrefabUtility.SaveAsPrefabAsset(room, RoomPath);
            Object.DestroyImmediate(room);
        }

        static void ConfigureScenes()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root + "ScriptableObjects/GameBalanceConfig.asset");
            foreach (string name in new[] { "00_Bootstrap", "03_RoomShellTest" })
            {
                string path = Root + "Scenes/" + name + ".unity";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var chair in Object.FindObjectsByType<WheelchairLocomotion>()) Object.DestroyImmediate(chair.gameObject);
                foreach (var origin in Object.FindObjectsByType<XROrigin>()) Object.DestroyImmediate(origin.gameObject);
                foreach (var camera in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(camera.gameObject);
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
                rig.transform.position = new Vector3(12f, 0f, 9f);
                rig.transform.rotation = Quaternion.LookRotation(new Vector3(12f, 0f, 2f));
                var bootstrap = Object.FindAnyObjectByType<GameStateBootstrap>();
                if (bootstrap == null) bootstrap = new GameObject("GameStateBootstrap").AddComponent<GameStateBootstrap>();
                bootstrap.ConfigureForBuilder(config, 999);
                if (name == "03_RoomShellTest")
                {
                    var fly = Object.FindAnyObjectByType<NavMeshAgent>();
                    fly.radius = 0.25f;
                    fly.height = 0.6f;
                    fly.baseOffset = 0.3f;
                    fly.stoppingDistance = 0.05f;
                    fly.transform.position = new Vector3(12f, 0.3f, 9f);
                    fly.GetComponent<CapsuleCollider>().center = Vector3.zero;
                    if (fly.GetComponentInChildren<MeshRenderer>() == null)
                    {
                        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        visual.name = "Fly Placeholder";
                        Object.DestroyImmediate(visual.GetComponent<Collider>());
                        visual.transform.SetParent(fly.transform, false);
                        visual.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
                    }
                }
                EditorSceneManager.SaveScene(scene);
            }
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Root + "Scenes/00_Bootstrap.unity", true),
                new EditorBuildSettingsScene(Root + "Scenes/03_RoomShellTest.unity", true),
            };
        }

        public static void ImportTextResources()
        {
            // Package.Import는 비동기다. 배치 호출은 -quit 없이 완료 콜백을 기다려야 한다.
            if (Application.isBatchMode)
            {
                AssetDatabase.importPackageCompleted += _ => EditorApplication.Exit(0);
                AssetDatabase.importPackageFailed += (_, error) =>
                {
                    Debug.LogError(error);
                    EditorApplication.Exit(1);
                };
            }
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            UnityEditor.AssetPackage.Package.Import(Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage"), false);
        }
    }
}
