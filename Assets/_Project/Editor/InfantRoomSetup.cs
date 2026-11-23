using System;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Wheelchair;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static class InfantRoomSetup
    {
        const string Root = "Assets/_Project/";
        public const string ScenePath = Root + "Scenes/01_Infant.unity";

        [MenuItem("Fruit Fly Parenting/Setup 05 Infant Room")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "ScriptableObjects/Items/Infant");
            AssetDatabase.Refresh();
            var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root + "ScriptableObjects/GameBalanceConfig.asset");
            EditorUtility.SetDirty(config);
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Art/Fonts/NotoSansCJKkr-Regular.otf");
            if (font == null) throw new InvalidOperationException("Import the Korean UI font before building the infant scene.");
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/03_RoomShellTest.unity", OpenSceneMode.Single);
            var room = Object.FindAnyObjectByType<RoomController>();
            PrefabUtility.UnpackPrefabInstance(room.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            room.name = "Infant Room";
            room.RefreshItemSlots();
            var definitions = new[]
            {
                Item("infant_book", "알파벳 그림책", new[]{"공부"}, new[]{3f,8f,0f,0f,2f}, "그림을 뒤집어 읽고 보호자를 찾아요.", "아까 보았던 그림을 다시 가리켜요."),
                Item("infant_blocks", "감각 블록", new[]{"놀이","창작"}, new[]{0f,5f,2f,0f,3f}, "탑을 무너뜨리고 다시 쌓아요.", "이번에는 다른 모양의 탑을 만들어요."),
                Item("infant_learning", "조기학습 화면", new[]{"성과","공부"}, new[]{9f,11f,-2f,0f,-2f}, "화면 빛을 따라가며 눈을 비벼요.", "익숙한 화면 앞에서 잠깐 고개를 갸웃해요."),
                Item("infant_hug", "푹신한 포옹 의자", new[]{"휴식","관계"}, new[]{-7f,0f,3f,0f,8f}, "손을 내밀고 포근히 잠들어요.", "익숙한 온기 곁에서 다시 쉬어요."),
                Item("infant_ball", "친구 곁 공놀이", new[]{"관계","운동"}, new[]{-2f,1f,5f,5f,2f}, "처음으로 다른 아이와 공을 나눠요.", "친구 쪽으로 다시 공을 굴려요."),
                Item("infant_snack", "달콤한 간식 상자", new[]{"소비","휴식"}, new[]{-3f,0f,-3f,1f,1f}, "바로 웃지만 금세 배가 아파요.", "상자를 만지다가 다른 곳을 바라봐요."),
                Item("infant_bed", "작은 침대", new[]{"휴식"}, new[]{-6f,0f,5f,0f,3f}, "잠깐 쉬고 새로운 지점을 탐색해요.", "작은 침대에 기대어 숨을 골라요."),
                Item("infant_crawl", "혼자 기어가기 길", new[]{"운동"}, new[]{2f,2f,8f,0f,2f}, "길 끝에서 뒤돌아보며 웃어요.", "조금 더 익숙하게 길을 따라가요."),
                Item("infant_tablet", "유튜브 태블릿", new[]{"소비","놀이"}, new[]{8f,-6f,-7f,-4f,-5f}, "자동재생 영상을 보다가, 화면을 떠나며 보채요.", "익숙한 영상 소리에 다시 시선을 빼앗겨요.")
            };
            string[] reflections = { "그림을 또 보고 싶어요.","내가 쌓았어요!","눈이 조금 피곤해요.","조금만 더 안아 주세요.",
                "친구에게 굴려 줄래요.","달콤했는데 배가 아파요.","푹 쉬었어요.","나 혼자 여기까지 왔어요.","영상이 자꾸 생각나요." };
            for (int i=0;i<definitions.Length;i++) { definitions[i].childReflectionLine = reflections[i]; EditorUtility.SetDirty(definitions[i]); }
            var colors = new[] { new Color(.82f,.48f,.3f), new Color(.4f,.72f,.64f), new Color(.38f,.5f,.75f),
                new Color(.82f,.57f,.62f), new Color(.62f,.76f,.36f), new Color(.92f,.68f,.25f),
                new Color(.61f,.56f,.79f), new Color(.35f,.7f,.8f), new Color(.3f,.34f,.46f) };
            for (int i = 0; i < RoomCoordinates.ItemSlots.Length; i++)
            {
                string slotId = RoomCoordinates.ItemSlots[i].id;
                var slot = room.ItemSlots.Single(s => s.ItemId == slotId);
                slot.AssignDefinition(definitions[i]);
                slot.name = "ItemSlot_" + definitions[i].itemId;
                var furniture = room.transform.Find("ItemSlots/Furniture_" + slotId);
                furniture.GetComponent<MeshRenderer>().enabled = false; // 기존 폭 1m 충돌 형상을 유지한다.
                MakeProp(furniture.position, room.transform, i, Material("Infant_Item_" + i, colors[i]));
            }
            var cream = Material("Infant_Cream", new Color(.88f,.79f,.64f));
            var wall = Material("Infant_Wall", new Color(.68f,.78f,.77f));
            foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name == "Floor") renderer.sharedMaterial = cream;
                else if (renderer.name.StartsWith("Wall_")) renderer.sharedMaterial = wall;
            }
            var blue = Material("Infant_Door", new Color(.23f,.43f,.5f));
            var door = Box("East Door", room.transform, new Vector3(24f, 1.5f, 11f), new Vector3(.2f,3f,3f), blue, true);
            Box("West Door", room.transform, new Vector3(0f,1.5f,11f), new Vector3(.2f,3f,3f), blue, true);
            Box("Exit Landing", room.transform, new Vector3(25f,-.1f,11f), new Vector3(2f,.2f,4f), cream, true);
            var fly = Object.FindAnyObjectByType<FlyAgent>();
            var swaddle = Material("Infant_Swaddle", new Color(.96f,.8f,.59f));
            fly.GetComponentInChildren<MeshRenderer>().sharedMaterial = swaddle;
            var pacifier = Shape("Pacifier", fly.transform, PrimitiveType.Sphere, new Vector3(0,.02f,.27f), new Vector3(.16f,.16f,.08f), blue);
            var chair = Object.FindAnyObjectByType<WheelchairLocomotion>();
            var sun = Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
            sun.intensity = 1.2f;
            var doorLight = new GameObject("Exit Light").AddComponent<Light>();
            doorLight.transform.SetParent(room.transform); doorLight.transform.position = new Vector3(22.8f,2.4f,11f);
            doorLight.type = LightType.Point; doorLight.range = 5f; doorLight.color = new Color(1f,.8f,.4f);
            var mobile = new GameObject("Ceiling Mobile"); mobile.transform.SetParent(room.transform, false);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                Shape("Mobile Star", mobile.transform, PrimitiveType.Sphere, new Vector3(12 + Mathf.Cos(angle)*1.3f,4.4f,11+Mathf.Sin(angle)*1.3f), Vector3.one*.3f, Material("Infant_Item_" + i, colors[i]));
            }
            var controller = new GameObject("Infant Room Flow").AddComponent<InfantRoomController>();
            var view = controller.gameObject.AddComponent<InfantRoomView>(); view.Configure(font);
            controller.Configure(config, room, fly, chair, door, sun, doorLight, view);
            var exit = new GameObject("East Exit Boundary"); exit.transform.position = new Vector3(24.2f,1f,11f);
            var box = exit.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(1f,2f,3f);
            var body = exit.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            exit.AddComponent<RoomExitTrigger>().Configure(controller);
            CorridorSetup.Attach(controller,config,font);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Step05: 9 item definitions, infant room, checkpoint flow and corridor fade saved. Build entry is 01_Infant.");
        }

        static ItemDefinition Item(string id, string name, string[] tags, float[] deltas, string first, string revisit)
        {
            string path = Root + "ScriptableObjects/Items/Infant/" + id + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null) { item = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item, path); }
            item.itemId = id; item.displayName = name; item.tags = tags; item.isExtreme = false;
            item.narrativeLines = new[] { first, revisit };
            item.statDeltas = deltas.Select((amount, index) => new ItemStatDelta((StatType)index, amount)).ToArray();
            EditorUtility.SetDirty(item);
            return item;
        }

        static void MakeProp(Vector3 center, Transform parent, int index, Material color)
        {
            Vector3 ground = new Vector3(center.x,0,center.z);
            Box("Low Cushion", parent, ground + Vector3.up*.2f, new Vector3(.95f,.4f,.95f), color, false);
            if (index == 4)
                Shape("Play Ball", parent, PrimitiveType.Sphere, ground + Vector3.up*.85f, Vector3.one*.8f, color);
            else if (index == 1)
                for (int j=0;j<3;j++) Box("Sensory Block", parent, ground+new Vector3(j%2*.15f-.08f,.6f+j*.35f,0), Vector3.one*.35f, color, false);
            else if (index == 3 || index == 6)
            {
                Box("Cushion", parent, ground+Vector3.up*.55f, new Vector3(.9f,.3f,.85f), color, false);
                Box("Pillow", parent, ground+new Vector3(0,.85f,.3f), new Vector3(.8f,.5f,.25f), color, false);
            }
            else if (index == 2 || index == 8 || index == 0)
            {
                var screen = Box(index == 0 ? "Picture Book" : "Screen", parent, ground+Vector3.up*1.05f, new Vector3(.8f,.65f,.12f), color, false);
                screen.transform.rotation = Quaternion.Euler(0, 180, 0);
                Box("Bright Page", parent, ground+new Vector3(0,1.05f,-.07f), new Vector3(.66f,.5f,.02f), Material("Infant_Page", new Color(.93f,.87f,.67f)), false);
            }
            else Box(index == 7 ? "Crawling Arch" : "Snack Box", parent, ground+Vector3.up*.8f, new Vector3(.8f,.7f,.8f), color, false);
        }
        static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collide)
        {
            var go = Shape(name, parent, PrimitiveType.Cube, position, scale, material);
            if (collide) go.AddComponent<BoxCollider>();
            return go;
        }
        static Material Material(string name, Color color)
        {
            string path = Root + "Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
