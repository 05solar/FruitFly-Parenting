using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Wheelchair;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using static FruitFlyParenting.Editor.CorridorSetup;

namespace FruitFlyParenting.Editor
{
    public static class MiddleAgeSetup
    {
        const string Root="Assets/_Project/";
        [MenuItem("Fruit Fly Parenting/Setup 12 Middle Age and Endings")]
        public static void Build()
        {
            Definitions(); YoungAdultRoomSetup.Build();
            var config=AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root+"ScriptableObjects/GameBalanceConfig.asset");
            var font=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/Fonts/NotoSansCJKkr-Regular.otf");
            var scene=EditorSceneManager.OpenScene(Root+"Scenes/03_RoomShellTest.unity",OpenSceneMode.Single);
            Object.DestroyImmediate(Object.FindAnyObjectByType<FlyAgent>().gameObject); Object.DestroyImmediate(Object.FindAnyObjectByType<RoomController>().gameObject);
            var chair=Object.FindAnyObjectByType<WheelchairLocomotion>(); chair.transform.SetPositionAndRotation(new Vector3(12,0,9),Quaternion.identity);
            var middle=Create(config,font,chair,Vector3.zero); middle.ConfigureStandalone(); middle.gameObject.SetActive(true);
            EditorSceneManager.SaveScene(scene,Root+"Scenes/06_MiddleAge.unity");
            BuildPrologue(config,font);
            var paths=new[]{Root+"Scenes/00_Bootstrap.unity",Root+"Scenes/06_MiddleAge.unity"};
            EditorBuildSettings.scenes=paths.Take(1).Concat(EditorBuildSettings.scenes.Select(s=>s.path).Where(p=>!paths.Contains(p))).Concat(paths.Skip(1))
                .Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root+"ScriptableObjects/GameBalanceConfig.asset")); AssetDatabase.SaveAssets();
            Debug.Log("Step12: prologue, middle-age room, 14 endings, gallery and restart saved.");
        }
        public static EndingDefinition[] Definitions()
        {
            Directory.CreateDirectory(Root+"ScriptableObjects/Endings"); var definitions=new EndingDefinition[14];
            for(int i=0;i<14;i++)
            {
                string path=Root+"ScriptableObjects/Endings/ending_"+(i+1).ToString("00")+".asset";
                var d=AssetDatabase.LoadAssetAtPath<EndingDefinition>(path);
                if(d==null) { d=ScriptableObject.CreateInstance<EndingDefinition>(); AssetDatabase.CreateAsset(d,path); }
                EndingCatalog.Configure(d,i+1); EditorUtility.SetDirty(d); definitions[i]=d;
            }
            return definitions;
        }
        public static MiddleAgeRoomController Create(GameBalanceConfig config,Font font,WheelchairLocomotion chair,Vector3 origin)
        {
            var dark=Material("Ending_Dark",new Color(.045f,.055f,.075f)); var warm=Material("Ending_Wood",new Color(.45f,.27f,.13f));
            var cream=Material("Ending_Paper",new Color(.9f,.83f,.65f)); var blue=Material("Ending_Blue",new Color(.15f,.3f,.4f));
            var root=new GameObject("Middle Age Room"); var t=root.transform;
            Shell(t,dark);
            var gate=Box("Closed Corridor Door",t,new Vector3(.05f,1.75f,11),new Vector3(.1f,3.5f,3),dark,true);
            var entry=root.AddComponent<RoomDestination>(); entry.Configure(gate,true); gate.SetActive(false);
            Box("Living Room Rug",t,new Vector3(12,.01f,12),new Vector3(10,.02f,10),warm,false);
            Box("Small Table",t,new Vector3(12,.7f,13.5f),new Vector3(2.8f,1.4f,1.3f),warm,true);
            Box("Empty Parent Seat",t,new Vector3(10,.4f,11.5f),new Vector3(1,.8f,1),blue,true);
            var date=Label(t,font,"Calendar",new Vector3(11.3f,1.8f,13.5f),new Vector2(700,420),38);
            Box("Letterbox",t,new Vector3(14,1,14),new Vector3(.8f,1.2f,.8f),blue,true);
            var letter=Box("Letter",t,new Vector3(14,1.65f,13.7f),new Vector3(.65f,.04f,.35f),cream,false);
            var extra=Box("More Letters",t,new Vector3(14,1.7f,13.7f),new Vector3(.6f,.1f,.3f),cream,false);
            var luggage=Box("Suitcase",t,new Vector3(15,.55f,16),new Vector3(1,1.1f,.5f),warm,false);
            Box("Case Handle",luggage.transform,new Vector3(0,.6f,0),new Vector3(.3f,.1f,.15f),cream,false);
            var hinge=new GameObject("Door Hinge").transform; hinge.SetParent(t,false); hinge.localPosition=new Vector3(10.5f,0,18);
            Box("Own Door",hinge,new Vector3(1,1.5f,0),new Vector3(2,3,.1f),warm,false);
            Box("Door Handle",hinge,new Vector3(1.7f,1.4f,-.1f),new Vector3(.12f,.12f,.16f),cream,false);
            var key=Box("House Key",t,new Vector3(12,1.5f,13.2f),new Vector3(.5f,.08f,.15f),cream,false);
            var child=Person(t,"Adult Child",new Vector3(12,0,17),blue,cream);
            var magic=new GameObject("Star Costume"); magic.transform.SetParent(child.transform,false);
            for(int i=0;i<7;i++) Shape("Star",magic.transform,PrimitiveType.Sphere,new Vector3(Mathf.Sin(i)*.7f,1.2f+Mathf.Cos(i)*.6f,0),Vector3.one*.12f,cream);
            var companions=new GameObject("Companions"); companions.transform.SetParent(t,false);
            Person(companions.transform,"Companion Left",new Vector3(10,0,17.5f),dark,cream); Person(companions.transform,"Companion Right",new Vector3(14,0,17.5f),dark,cream);
            var tickets=new GameObject("Travel Tickets"); tickets.transform.SetParent(t,false);
            for(int i=0;i<2;i++) Box("Ticket",tickets.transform,new Vector3(12+i*.5f,1.45f,13.5f),new Vector3(.4f,.04f,.25f),cream,false);
            var candle=new GameObject("Birthday Candle"); candle.transform.SetParent(t,false); candle.transform.localPosition=new Vector3(12,1.4f,13.5f);
            Shape("Wax",candle.transform,PrimitiveType.Cylinder,new Vector3(0,.15f,0),new Vector3(.1f,.15f,.1f),cream);
            Shape("Flame",candle.transform,PrimitiveType.Sphere,new Vector3(0,.36f,0),Vector3.one*.12f,cream);
            var news=Label(t,font,"News",new Vector3(15.5f,2.4f,17),new Vector2(900,500),44).gameObject; news.GetComponent<Text>().text="오늘의 뉴스\n멀리서 들려오는 아이의 이름";
            var docs=Label(t,font,"Debt Documents",new Vector3(12.7f,1.7f,13.5f),new Vector2(650,400),34).gameObject; docs.GetComponent<Text>().text="남아 있는 빚\n함께 이야기할 수 있을까요";
            var story=Label(t,font,"Ending Scene",new Vector3(12,3.3f,17),new Vector2(2000,350),42);
            var light=new GameObject("Living Room Light").AddComponent<Light>(); light.transform.SetParent(t,false); light.transform.localPosition=new Vector3(12,4,12); light.type=LightType.Point; light.range=13; light.intensity=4;
            var controller=root.AddComponent<MiddleAgeRoomController>();
            controller.Configure(config,font,Definitions(),entry,chair,date,story,hinge,light,new[]{letter,extra,luggage,key,child,magic,companions,tickets,candle,news,docs});
            root.SetActive(false); t.position=origin; return controller;
        }
        static GameObject Person(Transform parent,string name,Vector3 at,Material body,Material head)
        {
            var root=new GameObject(name); root.transform.SetParent(parent,false); root.transform.localPosition=at;
            Box("Body",root.transform,new Vector3(0,.9f,0),new Vector3(.55f,1.1f,.4f),body,false);
            Shape("Head",root.transform,PrimitiveType.Sphere,new Vector3(0,1.7f,0),Vector3.one*.45f,head); return root;
        }
        static Text Label(Transform parent,Font font,string name,Vector3 at,Vector2 size,int fontSize)
        {
            var canvas=CorridorUI.Canvas(name+" Board",parent,at,size,.002f); return CorridorUI.Text(canvas.transform,name,font,"",size,Vector2.zero,fontSize);
        }
        static void Shell(Transform t,Material material)
        {
            Box("Floor",t,new Vector3(12,-.1f,11),new Vector3(24,.2f,22),material,true);
            Box("North Wall",t,new Vector3(12,3,22),new Vector3(24,6,.2f),material,true);
            Box("South Wall",t,new Vector3(12,3,0),new Vector3(24,6,.2f),material,true);
            Box("East Wall",t,new Vector3(24,3,11),new Vector3(.2f,6,22),material,true);
            Box("West North",t,new Vector3(0,3,17.25f),new Vector3(.2f,6,9.5f),material,true);
            Box("West South",t,new Vector3(0,3,4.75f),new Vector3(.2f,6,9.5f),material,true);
        }
        static void BuildPrologue(GameBalanceConfig config,Font font)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            config=AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root+"ScriptableObjects/GameBalanceConfig.asset");
            font=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/Fonts/NotoSansCJKkr-Regular.otf");
            new GameObject("Game State").AddComponent<GameStateBootstrap>().ConfigureForBuilder(config,System.Guid.NewGuid().GetHashCode());
            var rig=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/VR/WheelchairRig.prefab")); rig.transform.position=new Vector3(12,0,9);
            var dark=Material("Ending_Dark",new Color(.045f,.055f,.075f)); var paper=Material("Ending_Paper",new Color(.9f,.83f,.65f));
            var room=new GameObject("Memory Room"); Shell(room.transform,dark);
            var walls=room.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("Wall") || r.name.StartsWith("West")).Select(r=>r.gameObject).ToArray();
            var egg=Shape("Small Egg",room.transform,PrimitiveType.Sphere,new Vector3(12,1.2f,12),new Vector3(.5f,.7f,.5f),paper);
            var light=new GameObject("Heartbeat").AddComponent<Light>(); light.transform.position=new Vector3(12,3,11); light.type=LightType.Point; light.range=16;
            var hatchling=Person(room.transform,"Hatchling",new Vector3(12,.9f,12),paper,paper); hatchling.transform.localScale=Vector3.one*.35f; hatchling.SetActive(false);
            new GameObject("Prologue Flow").AddComponent<PrologueController>().Configure(config,font,walls,egg.transform,light,hatchling);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.015f,.02f,.025f);
            EditorSceneManager.SaveScene(scene,Root+"Scenes/00_Bootstrap.unity");
        }
    }
}
