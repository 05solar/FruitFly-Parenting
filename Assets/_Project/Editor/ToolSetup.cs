using System.IO;
using System.Linq;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Tool;
using FruitFlyParenting.Wheelchair;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static class ToolSetup
    {
        public const string RigPath = "Assets/_Project/Prefabs/VR/WheelchairRig.prefab";
        const string Root = "Assets/_Project/";
        const string Actions = "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/XRI Default Input Actions.inputactions";

        [MenuItem("Fruit Fly Parenting/Setup 04 Wheelchair and Stick")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root + "ScriptableObjects/GameBalanceConfig.asset");
            // 새 필드 기본값을 실제 에셋에 저장한다. 기존 밸런스 튜닝값을 덮어쓰지 않는다.
            EditorUtility.SetDirty(config);
            Directory.CreateDirectory(Root + "Art/Materials");
            AssetDatabase.Refresh();
            var black = Material("WheelchairBlack", new Color(0.035f, 0.04f, 0.05f));
            var metal = Material("WheelchairMetal", new Color(0.3f, 0.34f, 0.38f));
            var electric = Material("ElectricYellow", new Color(1f, 0.8f, 0.05f));
            BuildRig(config, black, metal, electric);
            foreach (string sceneName in new[] { "00_Bootstrap", "03_RoomShellTest" })
            {
                string path = Root + "Scenes/" + sceneName + ".unity";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var old in Object.FindObjectsByType<WheelchairLocomotion>()) Object.DestroyImmediate(old.gameObject);
                foreach (var old in Object.FindObjectsByType<XROrigin>()) Object.DestroyImmediate(old.gameObject);
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
                rig.transform.position = new Vector3(12f, 0f, 9f);
                rig.transform.rotation = Quaternion.LookRotation(new Vector3(12f, 0f, 2f));
                var fly = Object.FindAnyObjectByType<FlyAgent>();
                if (fly != null)
                {
                    fly.transform.position = new Vector3(12f, 0.3f, 11f); // 차체와 겹쳐 시작하지 않는다.
                    var receiver = fly.GetComponent<FlyContactReceiver>() ?? fly.gameObject.AddComponent<FlyContactReceiver>();
                    receiver.Configure(config);
                    var feedback = fly.GetComponent<FlyShockFeedback>() ?? fly.gameObject.AddComponent<FlyShockFeedback>();
                    var oldSpark = fly.transform.Find("Shock Spark");
                    if (oldSpark != null) Object.DestroyImmediate(oldSpark.gameObject);
                    var spark = new GameObject("Shock Spark");
                    spark.transform.SetParent(fly.transform, false);
                    var line = spark.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.sharedMaterial = electric;
                    line.widthMultiplier = 0.055f;
                    line.positionCount = 7;
                    line.SetPositions(new[] { new Vector3(-0.4f, 0.35f, 0), new Vector3(-0.1f, 0.15f, -0.1f),
                        new Vector3(-0.25f, 0, -0.3f), new Vector3(0.15f, 0.1f, -0.3f),
                        new Vector3(0, -0.2f, -0.3f), new Vector3(0.35f, 0, -0.1f), new Vector3(0.4f, 0.3f, 0) });
                    feedback.Configure(config, spark);
                    spark.SetActive(false);
                }
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Step04: WheelchairRig and fixed right-hand electric stick saved in both scenes.");
        }

        static void BuildRig(GameBalanceConfig config, Material black, Material metal, Material electric)
        {
            var root = new GameObject("WheelchairRig");
            var xr = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/VR/XROrigin.prefab"));
            xr.transform.SetParent(root.transform, false);
            var origin = xr.GetComponent<XROrigin>();
            origin.Origin = root;
            PrefabUtility.RecordPrefabInstancePropertyModifications(origin);
            var body = root.AddComponent<CharacterController>();
            body.radius = 0.4f; body.height = 1.2f; body.center = new Vector3(0f, 0.6f, 0f);
            body.skinWidth = 0.01f; body.stepOffset = 0.05f; body.minMoveDistance = 0f;
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.4f; obstacle.height = 1.2f; obstacle.center = new Vector3(0f, 0.6f, 0f);
            obstacle.carving = false; // 아이가 동적 차체를 피하지만 휠체어 이동은 NavMesh에 의존하지 않는다.
            var transformer = root.AddComponent<XRBodyTransformer>();
            transformer.xrOrigin = origin;
            var mediator = root.AddComponent<LocomotionMediator>();
            var move = root.AddComponent<ContinuousMoveProvider>();
            var turn = root.AddComponent<ContinuousTurnProvider>();
            move.mediator = mediator; turn.mediator = mediator;
            move.enableStrafe = false; move.forwardSource = root.transform;
            turn.enableTurnAround = false;
            move.leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            turn.leftHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            move.rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
            turn.rightHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
            var motion = root.AddComponent<WheelchairLocomotion>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Actions);
            var action = actions.FindAction("XRI Left/Thumbstick", true);
            var reference = AssetDatabase.LoadAllAssetsAtPath(Actions).OfType<InputActionReference>().First(a => a.action.id == action.id);
            motion.Configure(config, reference, move, turn);
            var serialized = new SerializedObject(transformer);
            serialized.FindProperty("m_BodyPositionEvaluatorObject").objectReferenceValue = motion;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Visual("Seat", root.transform, PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.12f, 0.6f), black);
            Visual("Back", root.transform, PrimitiveType.Cube, new Vector3(0, 0.78f, -0.26f), new Vector3(0.6f, 0.5f, 0.1f), black);
            foreach (float side in new[] { -1f, 1f })
            {
                var wheel = Visual("Wheel", root.transform, PrimitiveType.Cylinder, new Vector3(side * 0.34f, 0.25f, 0), new Vector3(0.5f, 0.05f, 0.5f), metal);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            var bumper = new GameObject("Front Bumper");
            bumper.transform.SetParent(root.transform, false);
            bumper.transform.localPosition = new Vector3(0f, 0.3f, 0.5f);
            var trigger = bumper.AddComponent<BoxCollider>();
            trigger.isTrigger = true; trigger.size = new Vector3(0.6f, 0.5f, 0.2f);
            Kinematic(bumper);
            bumper.AddComponent<WheelchairBumper>().Configure(config, motion);
            Visual("Bumper", root.transform, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0.43f), new Vector3(0.6f, 0.1f, 0.08f), metal);

            var hand = xr.transform.Find("Camera Offset/Right Controller");
            hand.localPosition = new Vector3(0.32f, -0.45f, 0.25f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hand);
            var stick = new GameObject("Love Stick");
            stick.transform.SetParent(hand, false);
            var shaft = Visual("Black Shaft", stick.transform, PrimitiveType.Cylinder, new Vector3(0, 0, 0.25f), new Vector3(0.055f, 0.25f, 0.055f), black);
            shaft.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var tip = Visual("Electric Tip", stick.transform, PrimitiveType.Sphere, new Vector3(0, 0, 0.52f), Vector3.one * 0.12f, electric);
            var tipCollider = tip.AddComponent<SphereCollider>();
            tipCollider.radius = 0.7f; tipCollider.isTrigger = true;
            Kinematic(tip);
            tip.AddComponent<LoveStickTool>();
            PrefabUtility.SaveAsPrefabAsset(root, RigPath);
            Object.DestroyImmediate(root);
        }

        static void Kinematic(GameObject go)
        {
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
        }
        static GameObject Visual(string name, Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        static Material Material(string name, Color color)
        {
            string path = Root + "Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
