using System;
using System.IO;
using System.Linq;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Wheelchair;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static class CorridorSetup
    {
        const string Root = "Assets/_Project/";
        public const string PrefabPath = Root + "Prefabs/Rooms/CorridorShell.prefab";

        [MenuItem("Fruit Fly Parenting/Setup 06 Corridor and Transitions")]
        public static void Build() => InfantRoomSetup.Build();

        // 05 빌더가 씬을 저장하기 전에 호출한다. 같은 씬에 실제 인접 좌표로 배치한다.
        public static void Attach(InfantRoomController infant, GameBalanceConfig config, Font font)
        {
            Directory.CreateDirectory(Root + "ScriptableObjects/Transitions");
            Directory.CreateDirectory(Root + "Audio");
            AssetDatabase.Refresh();
            var oldSound = Sound("InfantLullaby",220);
            var nextSound = Sound("ChildhoodPlay",264);
            var route = CreateRoutes(oldSound,nextSound);
            var floor = Material("CorridorFloor",new Color(.25f,.32f,.34f));
            var wall = Material("CorridorWall",new Color(.32f,.43f,.48f));
            var trim = Material("CorridorTrim",new Color(.10f,.19f,.25f));
            var amber = Material("CorridorAmber",new Color(.85f,.64f,.34f));
            var blue = Material("CorridorBlue",route.themeColor);
            var shell = CreateShell(config,font,floor,wall,trim,amber,blue);
            PrefabUtility.SaveAsPrefabAsset(shell.gameObject,PrefabPath);
            Object.DestroyImmediate(shell.gameObject);
            shell = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))).GetComponent<CorridorShell>();
            shell.transform.position = new Vector3(RoomCoordinates.EastExit.x,0,RoomCoordinates.EastExit.y);
            shell.gameObject.SetActive(false);

            var chair = Object.FindAnyObjectByType<WheelchairLocomotion>();
            var fly = Object.FindAnyObjectByType<FlyAgent>();
            var sourceRoom = Object.FindAnyObjectByType<RoomController>();
            var child = ChildhoodRoomSetup.Create(config,font,fly,chair,new Vector3(24 + config.corridorLength,0,0));
            var destination = child.GetComponent<RoomDestination>();
            var root = new GameObject("Room Transition");
            var transition = root.AddComponent<RoomTransitionController>();
            var view = root.AddComponent<TransitionView>(); view.Configure(font);
            var sound = root.AddComponent<CorridorAudioController>();
            var previous = new GameObject("Previous Room Echo").AddComponent<AudioSource>(); previous.transform.SetParent(root.transform,false);
            var next = new GameObject("Next Room Ambience").AddComponent<AudioSource>(); next.transform.SetParent(root.transform,false);
            foreach (var source in new[]{previous,next}) { source.loop = true; source.playOnAwake = false; source.spatialBlend = 0f; }
            var echo = previous.gameObject.AddComponent<AudioEchoFilter>(); echo.delay = 320; echo.decayRatio = .25f; echo.dryMix = 1; echo.wetMix = 0;
            sound.Configure(previous,next,echo);
            transition.Configure(config,route,sourceRoom.gameObject,
                fly.gameObject,chair,shell,destination,view,sound);
            infant.ConfigureTransition(transition);
            child.ConfigureIncoming(transition);
            var sourceTrigger = GameObject.Find("East Exit Boundary").GetComponent<RoomExitTrigger>();
            sourceTrigger.Configure(transition,false);
            var exit = new GameObject("Corridor Exit Boundary"); exit.transform.SetParent(root.transform,false);
            exit.transform.position = new Vector3(24 + config.corridorLength + .7f,1,11);
            var collider = exit.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(1.2f,2,3);
            var body = exit.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            exit.AddComponent<RoomExitTrigger>().Configure(transition,true);
            Debug.Log("Step06: 4m x 14m corridor, five adjacent-stage routes, dynamic recap, audio and childhood container saved.");
        }

        static CorridorShell CreateShell(GameBalanceConfig config, Font font, Material floor, Material wall, Material trim, Material amber, Material blue)
        {
            var root = new GameObject("CorridorShell");
            float length = config.corridorLength, width = config.corridorWidth;
            // 입구를 1m 겹쳐 원래 방을 숨겨도 차체 전체를 받치는 바닥을 유지한다.
            Box("Floor",root.transform,new Vector3((length-1)*.5f,-.1f,0),new Vector3(length+1,.2f,width),floor,true);
            Box("North Wall",root.transform,new Vector3((length-1)*.5f,1.75f,width*.5f+.1f),new Vector3(length+1,3.5f,.2f),wall,true);
            Box("South Wall",root.transform,new Vector3((length-1)*.5f,1.75f,-width*.5f-.1f),new Vector3(length+1,3.5f,.2f),wall,true);
            Box("Ceiling",root.transform,new Vector3((length-1)*.5f,3.55f,0),new Vector3(length+1,.1f,width),wall,false);
            Box("Closed Previous Door",root.transform,new Vector3(-.55f,1.75f,0),new Vector3(.1f,3.5f,width),trim,true);
            var door = Box("Next Room Door",root.transform,new Vector3(length,1.75f,0),new Vector3(.15f,3.5f,width),trim,true);
            for (int i=0;i<3;i++)
            {
                float x = i == 0 ? 2f : i == 1 ? 7f : 12f;
                var light = new GameObject("Section Light " + i).AddComponent<Light>(); light.transform.SetParent(root.transform,false);
                light.transform.localPosition = new Vector3(x,2.9f,0); light.type = LightType.Point; light.range = 7f; light.intensity = 1.5f;
                light.color = Color.Lerp(new Color(1f,.78f,.52f),new Color(.6f,.82f,1f),i*.5f);
                Box("Ceiling Marker " + i,root.transform,new Vector3(x,3.45f,0),new Vector3(.12f,.02f,width-.2f),i == 0 ? amber : blue,false);
            }
            var anchors = new Transform[3];
            for (int i=0;i<3;i++)
            {
                var anchor = new GameObject("Memory Anchor " + i).transform; anchor.SetParent(root.transform,false);
                anchor.localPosition = new Vector3(1f+i,1.65f,i == 1 ? -1.96f : 1.96f);
                anchor.localRotation = Quaternion.Euler(0,i == 1 ? 180 : 0,0); anchors[i] = anchor;
            }
            var recap = root.AddComponent<CorridorRecapController>(); recap.Configure(font,anchors,config);
            var middle = CorridorUI.Canvas("Narration Wall",root.transform,new Vector3(7,1.7f,1.96f),new Vector2(1500,380),.002f);
            CorridorUI.Image(middle.transform,"Frame",new Vector2(1500,380),Vector2.zero,new Color(.08f,.14f,.19f));
            CorridorUI.Text(middle.transform,"Narration",font,"아이는 말할 수 있게 되었다.",new Vector2(1420,300),Vector2.zero,42);
            // 실루엣은 측면 벽에 붙여 중앙 4m 통행 공간에 충돌체를 만들지 않는다.
            Box("Child Silhouette Body",root.transform,new Vector3(11.8f,1.0f,1.94f),new Vector3(.55f,.8f,.04f),trim,false);
            Shape("Child Silhouette Head",root.transform,PrimitiveType.Sphere,new Vector3(11.8f,1.65f,1.94f),new Vector3(.55f,.55f,.04f),trim);
            Box("Play Blocks",root.transform,new Vector3(12.7f,.5f,1.94f),new Vector3(.55f,.6f,.04f),blue,false);
            var shell = root.AddComponent<CorridorShell>(); shell.Configure(config,door,recap,middle.GetComponentInChildren<UnityEngine.UI.Text>()); return shell;
        }

        internal static RoomDestination CreateNextRoom(GameBalanceConfig config, Font font, RoomTransitionDefinition route, Material floor, Material wall, Material trim, Material blue)
        {
            var root = new GameObject(route.toStage + " Room");
            Box("Floor",root.transform,new Vector3(12,-.1f,11),new Vector3(24,.2f,22),floor,true);
            Box("North Wall",root.transform,new Vector3(12,3,22.1f),new Vector3(24,6,.2f),wall,true);
            Box("South Wall",root.transform,new Vector3(12,3,-.1f),new Vector3(24,6,.2f),wall,true);
            Box("East Wall",root.transform,new Vector3(24.1f,3,11),new Vector3(.2f,6,22),wall,true);
            Box("West North",root.transform,new Vector3(-.1f,3,17.25f),new Vector3(.2f,6,9.5f),wall,true);
            Box("West South",root.transform,new Vector3(-.1f,3,4.75f),new Vector3(.2f,6,9.5f),wall,true);
            var gate = Box("Closed Corridor Door",root.transform,new Vector3(.05f,1.75f,11),new Vector3(.1f,3.5f,3),trim,true);
            for (int i=0;i<3;i++) Box("Play Block " + i,root.transform,new Vector3(7+i*1.2f,.5f,15),Vector3.one,blue,false);
            var label = CorridorUI.Canvas("Childhood Welcome",root.transform,new Vector3(9,2.1f,11),new Vector2(1500,400),.003f);
            label.transform.localRotation = Quaternion.Euler(0,90,0);
            CorridorUI.Image(label.transform,"Backdrop",new Vector2(1500,400),Vector2.zero,new Color(.08f,.16f,.2f));
            CorridorUI.Text(label.transform,"Title",font,route.nextRoomTitle + "\n" + route.nextRoomLine,new Vector2(1400,350),Vector2.zero,44);
            var destination = root.AddComponent<RoomDestination>(); destination.Configure(gate); gate.SetActive(false); return destination;
        }

        static RoomTransitionDefinition CreateRoutes(AudioClip previous, AudioClip next)
        {
            string[] rooms = { "00_Prologue","01_Infant","03_Childhood","04_Adolescence","05_YoungAdult","06_MiddleAge" };
            string[] titles = { "첫 손길","놀이터와 첫 약속","갈림길","자기 방","되돌아온 문" };
            string[] narrations = { "우리는 처음으로 같은 문을 바라보았다.","아이는 말할 수 있게 되었다.","아이는 좋아하는 것의 이름을 말하기 시작했다.","나는 이제 내일의 길을 고르고 싶어.","내가 고른 문을 당신에게 열어 주고 싶어." };
            RoomTransitionDefinition infant = null;
            for (int i=0;i<5;i++)
            {
                string path = Root + "ScriptableObjects/Transitions/" + ((LifeStage)i) + "To" + ((LifeStage)(i+1)) + ".asset";
                var route = AssetDatabase.LoadAssetAtPath<RoomTransitionDefinition>(path);
                if (route == null) { route = ScriptableObject.CreateInstance<RoomTransitionDefinition>(); AssetDatabase.CreateAsset(route,path); }
                route.fromStage = (LifeStage)i; route.toStage = (LifeStage)(i+1); route.fromRoomId = rooms[i]; route.toRoomId = rooms[i+1];
                route.corridorId = "corridor_" + i.ToString("00"); route.recapTitle = i <= 1 ? "함께 보낸 첫 시간" : "내가 지나온 시간";
                route.nextRoomTitle = titles[i]; route.narration = narrations[i]; route.nextRoomLine = i <= 1 ? "새로운 놀이를 함께 찾아볼까요." : "이번에는 내가 문을 열어 볼게.";
                route.previousRoomSound = previous; route.nextRoomSound = next; EditorUtility.SetDirty(route);
                if (i == 1) infant = route;
            }
            return infant;
        }

        internal static AudioClip Sound(string name, float frequency)
        {
            string path = Root + "Audio/" + name + ".wav";
            if (!File.Exists(path))
            {
                const int rate = 22050, seconds = 4, bytes = rate * seconds * 2;
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+bytes);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                    writer.Write(rate); writer.Write(rate*2); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                    for (int n=0;n<rate*seconds;n++)
                    {
                        double t = (double)n/rate;
                        double envelope = .65 + .35 * Math.Cos(2*Math.PI*t/seconds);
                        double sample = (Math.Sin(2*Math.PI*frequency*t) + .35*Math.Sin(2*Math.PI*frequency*1.5*t) + .2*Math.Sin(2*Math.PI*frequency*2*t)) * .16 * envelope;
                        writer.Write((short)(sample*short.MaxValue));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        internal static Material Material(string name, Color color)
        {
            string path = Root + "Art/Materials/" + name + ".mat";
            var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value == null) { value = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color }; AssetDatabase.CreateAsset(value,path); }
            return value;
        }
        internal static GameObject Shape(string name, Transform parent, PrimitiveType primitive, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(primitive); go.name = name; go.transform.SetParent(parent,false);
            go.transform.localPosition = at; go.transform.localScale = size; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        internal static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material material, bool collide)
        {
            var go = Shape(name,parent,PrimitiveType.Cube,at,size,material); if (collide) go.AddComponent<BoxCollider>(); return go;
        }
    }
}
