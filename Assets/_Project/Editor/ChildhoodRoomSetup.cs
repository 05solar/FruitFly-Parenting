using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using FruitFlyParenting.Wheelchair;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using static FruitFlyParenting.Editor.CorridorSetup;

namespace FruitFlyParenting.Editor
{
    public static class ChildhoodRoomSetup
    {
        const string Root = "Assets/_Project/";
        public const string ScenePath = Root + "Scenes/03_Childhood.unity";
        [MenuItem("Fruit Fly Parenting/Setup 09 Childhood Room")]
        public static void Build()
        {
            InfantRoomSetup.Build();
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/03_RoomShellTest.unity",OpenSceneMode.Single);
            var template = Object.FindAnyObjectByType<FlyAgent>();
            var oldRoom = Object.FindAnyObjectByType<RoomController>();
            var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root + "ScriptableObjects/GameBalanceConfig.asset");
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Art/Fonts/NotoSansCJKkr-Regular.otf");
            var child = Create(config,font,template,Object.FindAnyObjectByType<WheelchairLocomotion>(),Vector3.zero);
            Object.DestroyImmediate(template.gameObject); Object.DestroyImmediate(oldRoom.gameObject);
            child.ConfigureStandalone(); child.gameObject.SetActive(true);
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)
                .Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Step09: childhood room, 12 items, connected NavMesh, airplane event and corridor 2 saved.");
        }

        public static ChildhoodRoomController Create(GameBalanceConfig config, Font font, FlyAgent template,
            WheelchairLocomotion chair, Vector3 origin)
        {
            Directory.CreateDirectory(Root+"ScriptableObjects/Items/Childhood"); AssetDatabase.Refresh();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Rooms/RoomShell.prefab"));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            root.name = "Childhood Room";
            var room = root.GetComponent<RoomController>(); room.ConfigureLifeStage(LifeStage.Childhood);
            var surface = root.GetComponent<NavMeshSurface>(); surface.RemoveData();
            Object.DestroyImmediate(root.transform.Find("ItemSlots").gameObject);
            var gizmos = root.GetComponent<RoomCoordinateGizmos>(); if (gizmos != null) Object.DestroyImmediate(gizmos);
            var wood = Material("Childhood_Classroom",new Color(.65f,.46f,.29f));
            var grass = Material("Childhood_Playground",new Color(.35f,.61f,.48f));
            var ink = Material("Childhood_Ink",new Color(.13f,.24f,.30f));
            var cream = Material("Childhood_Paper",new Color(.95f,.89f,.72f));
            var blue = Material("Childhood_Sky",new Color(.37f,.66f,.8f));
            var gold = Material("Childhood_Star",new Color(.93f,.66f,.26f));
            root.transform.Find("Floor").GetComponent<Renderer>().sharedMaterial = wood;
            Box("Playground Floor",root.transform,new Vector3(18,.005f,11),new Vector3(12,.01f,22),grass,false);
            foreach(var wall in root.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Wall_"))) wall.sharedMaterial=cream;
            WallArt(root.transform,wood,blue,cream,ink);
            var slots = new GameObject("ItemSlots").transform; slots.SetParent(root.transform,false);
            for(int i=0;i<ChildhoodCatalog.Entries.Length;i++)
            {
                var e=ChildhoodCatalog.Entries[i]; var item=Definition(e);
                var slotObject=new GameObject("ItemSlot_"+e.Id); slotObject.transform.SetParent(slots,false);
                slotObject.transform.localPosition=new Vector3(e.Position.x,.3f,e.Position.y);
                var slot=slotObject.AddComponent<ItemSlot>(); slot.ConfigureForBuilder(e.Id,1.2f,2.5f,2);
                slot.AssignDefinition(item); slot.ConfigureHesitation(config.extremeItemHesitationSeconds);
                var color=e.Extreme ? gold : i%2==0 ? blue : wood;
                var furniture=Box("Furniture_"+e.Id,slots,new Vector3(e.Position.x,.9f,e.Position.y),new Vector3(1,1.8f,1),color,true);
                furniture.GetComponent<Renderer>().enabled=false;
                var modifier=furniture.AddComponent<NavMeshModifier>(); modifier.overrideArea=true; modifier.area=NavMesh.GetAreaFromName("Not Walkable");
                Prop(e.Id,furniture.transform,color,cream,ink);
            }
            room.RefreshItemSlots(); Bake(surface);
            var gate=Box("Closed Corridor Door",root.transform,new Vector3(.05f,1.75f,11),new Vector3(.1f,3.5f,3),ink,true);
            var door=Box("Childhood Exit Door",root.transform,new Vector3(24,1.75f,11),new Vector3(.1f,3.5f,3),blue,true);
            gate.SetActive(false);
            var destination=root.AddComponent<RoomDestination>(); destination.Configure(gate,true);
            var fly=Object.Instantiate(template,root.transform); fly.name="Childhood Fly";
            fly.gameObject.SetActive(false); fly.transform.localPosition=new Vector3(12,.3f,11); fly.transform.localRotation=Quaternion.identity;
            fly.ConfigureForBuilder(config,room); fly.GetComponent<FlyContactReceiver>().Configure(config);
            foreach(var renderer in fly.GetComponentsInChildren<Renderer>()) if(!renderer.name.Contains("Spark")) renderer.enabled=false;
            var appearance=new GameObject("Childhood Appearance").transform; appearance.SetParent(fly.transform,false);
            Box("Yellow T Shirt",appearance,new Vector3(0,.13f,0),new Vector3(.38f,.3f,.3f),gold,false);
            Box("Blue Shorts",appearance,new Vector3(0,-.08f,0),new Vector3(.35f,.16f,.28f),blue,false);
            Shape("Head",appearance,PrimitiveType.Sphere,new Vector3(0,.43f,0),Vector3.one*.32f,cream);
            Shape("Hat",appearance,PrimitiveType.Cylinder,new Vector3(0,.58f,0),new Vector3(.36f,.04f,.36f),blue);
            Box("Propeller",appearance,new Vector3(0,.67f,0),new Vector3(.5f,.035f,.08f),gold,false);
            fly.gameObject.AddComponent<FlyHesitationFeedback>().Configure(fly,appearance,config);
            var view=root.AddComponent<InfantRoomView>(); view.Configure(font);
            var paper=Paper(root.transform,config,font,cream,ink);
            var controller=root.AddComponent<ChildhoodRoomController>();
            root.SetActive(false); root.transform.position=origin;
            var outgoing=Exit(config,font,chair,root,fly,origin,wood,cream,ink,blue);
            controller.Configure(config,room,fly,destination,door,view,paper,outgoing);
            return controller;
        }
        internal static ItemDefinition Definition(RoomItemSpec entry, string stage="Childhood")
        {
            string path=Root+"ScriptableObjects/Items/"+stage+"/"+entry.Id+".asset";
            var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if(item==null) { item=ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item,path); }
            item.itemId=entry.Id; item.displayName=entry.Name; item.tags=entry.Tags; item.isExtreme=entry.Extreme; item.trustDelta=entry.Trust;
            item.recordsDelinquency=entry.Delinquency;
            item.youngAdultFlags=entry.Flags;
            item.statDeltas=entry.Stats.Select((amount,i)=>new ItemStatDelta((StatType)i,amount))
                .Concat(entry.Rebellion==0 ? new ItemStatDelta[0] : new[]{new ItemStatDelta(StatType.Rebellion,entry.Rebellion)}).ToArray();
            item.narrativeLines=new[]{entry.FirstLine,entry.RevisitLine}; item.childReflectionLine=entry.RevisitLine;
            EditorUtility.SetDirty(item); return item;
        }
        internal static void Bake(NavMeshSurface surface,string stage="Childhood")
        {
            surface.BuildNavMesh(); var baked=surface.navMeshData; surface.RemoveData();
            string path=Root+"Prefabs/Rooms/"+stage+"NavMesh.asset";
            var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if(existing==null) AssetDatabase.CreateAsset(baked,path);
            else { EditorUtility.CopySerialized(baked,existing); Object.DestroyImmediate(baked); baked=existing; EditorUtility.SetDirty(baked); }
            surface.navMeshData=baked; surface.AddData();
        }
        static void WallArt(Transform parent, Material wood, Material sky, Material paper, Material ink)
        {
            Box("Bookshelf Mural",parent,new Vector3(6,2.5f,21.96f),new Vector3(9,3,.03f),wood,false);
            for(int row=0;row<3;row++) for(int book=0;book<12;book++)
                Box("Painted Book",parent,new Vector3(2.2f+book*.67f,1.6f+row*.85f,21.93f),new Vector3(.4f,.6f,.02f),book%3==0 ? sky : paper,false);
            Box("Sky Mural",parent,new Vector3(18,2.8f,21.96f),new Vector3(10,4,.03f),sky,false);
            for(int i=0;i<3;i++) Shape("Painted Cloud",parent,PrimitiveType.Sphere,new Vector3(15+i*2.5f,3.6f,21.92f),new Vector3(1.8f,.7f,.03f),paper);
            var light=new GameObject("Playground Light").AddComponent<Light>(); light.transform.SetParent(parent,false);
            light.transform.localPosition=new Vector3(18,4,11); light.type=LightType.Point; light.range=18; light.intensity=2; light.color=new Color(.8f,.9f,1);
        }
        internal static void Prop(string id, Transform parent, Material color, Material paper, Material ink)
        {
            // Parent is the 1 x 1.8 x 1 collision envelope; children counter its Y scale.
            var stand=new GameObject("Activity").transform; stand.SetParent(parent,false); stand.localScale=new Vector3(1,1/1.8f,1); stand.localPosition=new Vector3(0,-.5f,0);
            Box("Base",stand,new Vector3(0,.2f,0),new Vector3(.95f,.4f,.95f),color,false);
            if(id.EndsWith("soccer") || id.EndsWith("stadium") || id.EndsWith("training"))
            {
                for(int side=-1;side<=1;side+=2) Box("Goal Post",stand,new Vector3(side*.4f,.9f,.3f),new Vector3(.08f,1.2f,.08f),paper,false);
                Box("Goal Bar",stand,new Vector3(0,1.5f,.3f),new Vector3(.9f,.08f,.08f),paper,false);
                Shape("Football",stand,PrimitiveType.Sphere,new Vector3(0,.65f,-.15f),Vector3.one*.4f,paper);
            }
            else if(id.EndsWith("birthday"))
            {
                Shape("Cake",stand,PrimitiveType.Cylinder,new Vector3(0,.7f,0),new Vector3(.6f,.18f,.6f),paper);
                for(int i=-1;i<=1;i++) Box("Candle",stand,new Vector3(i*.15f,1,0),new Vector3(.04f,.25f,.04f),color,false);
            }
            else if(id.EndsWith("hideout"))
            {
                var roof=Box("Hideout Roof",stand,new Vector3(0,1.2f,0),new Vector3(.95f,.08f,.9f),color,false); roof.transform.localRotation=Quaternion.Euler(0,0,20);
                for(int i=0;i<3;i++) Box("Torn Homework",stand,new Vector3(-.25f+i*.25f,.5f,.1f),new Vector3(.16f,.02f,.3f),paper,false);
            }
            else if(id.EndsWith("star"))
            {
                Box("Camera Tripod",stand,new Vector3(0,.85f,0),new Vector3(.08f,1,.08f),ink,false);
                Box("Camera",stand,new Vector3(0,1.4f,0),new Vector3(.5f,.3f,.3f),ink,false);
                Shape("Lens",stand,PrimitiveType.Sphere,new Vector3(0,1.4f,-.18f),Vector3.one*.22f,paper);
            }
            else if(id.EndsWith("arcade") || id.EndsWith("nightgame"))
            {
                Box("Cabinet",stand,new Vector3(0,1,0),new Vector3(.8f,1.2f,.6f),ink,false);
                Box("Screen",stand,new Vector3(0,1.25f,-.31f),new Vector3(.64f,.48f,.02f),color,false);
            }
            else
            {
                Box("Table",stand,new Vector3(0,.75f,0),new Vector3(.95f,.12f,.9f),color,false);
                if(id.EndsWith("walk")) Shape("Tree",stand,PrimitiveType.Sphere,new Vector3(0,1.2f,0),Vector3.one*.7f,color);
                else Box(id.EndsWith("family") ? "Shared Meal" : "Book and Paper",stand,new Vector3(0,.86f,0),new Vector3(.65f,.08f,.5f),paper,false);
            }
        }
        static PaperAirplaneView Paper(Transform root, GameBalanceConfig config, Font font, Material paper, Material ink)
        {
            var go=new GameObject("Paper Airplane Event"); go.transform.SetParent(root,false); go.transform.localPosition=new Vector3(12,1.1f,12.5f);
            var plane=new GameObject("Folded Plane").transform; plane.SetParent(go.transform,false);
            var left=Wing(plane,paper,-1); var right=Wing(plane,paper,1);
            var tape=Box("Repair Tape",plane,new Vector3(0,.035f,0),new Vector3(.08f,.015f,.4f),Material("Childhood_Tape",new Color(.65f,.89f,.78f)),false);
            tape.SetActive(false);
            var actors=new GameObject("Paper Story Actors"); actors.transform.SetParent(go.transform,false);
            var child=StoryActor("Child",actors.transform,new Vector3(-.85f,-1.1f,0),Material("Childhood_Star",new Color(.93f,.66f,.26f)),paper,90);
            StoryActor("Friend",actors.transform,new Vector3(.85f,-1.1f,0),Material("Childhood_Sky",new Color(.37f,.66f,.8f)),paper,-90);
            actors.SetActive(false);
            var board=CorridorUI.Canvas("Paper Story",go.transform,new Vector3(0,1.1f,0),new Vector2(1400,380),.002f);
            CorridorUI.Image(board.transform,"Backdrop",new Vector2(1400,380),Vector2.zero,new Color(.10f,.19f,.23f));
            var label=CorridorUI.Text(board.transform,"Story",font,"친구와 함께 접은 종이비행기",new Vector2(1340,340),Vector2.zero,42);
            var view=go.AddComponent<PaperAirplaneView>(); view.Configure(config,left,right,label,plane,child,actors,tape); return view;
        }
        static Transform Wing(Transform parent, Material material, int side)
        {
            string path=Root+"Prefabs/Rooms/PaperWing"+(side<0 ? "Left" : "Right")+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null) { mesh=new Mesh { name="Folded Paper Wing" }; AssetDatabase.CreateAsset(mesh,path); }
            var face=new[]{new Vector3(0,0,.6f),new Vector3(side*.5f,0,-.4f),new Vector3(0,.08f,-.25f)};
            mesh.Clear(); mesh.vertices=face.Concat(face).ToArray();
            mesh.triangles=new[]{0,1,2,5,4,3}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var go=new GameObject(side<0 ? "Left Fold" : "Right Fold",typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);
            go.transform.localPosition=new Vector3(side*.015f,0,0); go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=material;
            return go.transform;
        }
        static Transform StoryActor(string name, Transform parent, Vector3 at, Material shirt, Material skin, float yaw)
        {
            var go=new GameObject(name).transform; go.SetParent(parent,false); go.localPosition=at; go.localRotation=Quaternion.Euler(0,yaw,0);
            Shape("Body",go,PrimitiveType.Capsule,new Vector3(0,.65f,0),new Vector3(.35f,.3f,.3f),shirt);
            Shape("Head",go,PrimitiveType.Sphere,new Vector3(0,1.1f,0),Vector3.one*.33f,skin);
            Box("Nose",go,new Vector3(0,1.1f,.17f),new Vector3(.08f,.08f,.08f),skin,false);
            for(int side=-1;side<=1;side+=2) Box("Leg",go,new Vector3(side*.1f,.25f,0),new Vector3(.12f,.45f,.14f),shirt,false);
            return go;
        }
        internal static RoomTransitionController Exit(GameBalanceConfig config, Font font, WheelchairLocomotion chair, GameObject room,
            FlyAgent fly, Vector3 origin, Material floor, Material wall, Material trim, Material blue,LifeStage stage=LifeStage.Childhood)
        {
            bool child=stage==LifeStage.Childhood;
            bool teen=stage==LifeStage.Adolescence;
            var route=AssetDatabase.LoadAssetAtPath<RoomTransitionDefinition>(Root+"ScriptableObjects/Transitions/"+(child ? "ChildhoodToAdolescence" : teen ? "AdolescenceToYoungAdult" : "YoungAdultToMiddleAge")+".asset");
            route.previousRoomSound=Sound(child ? "ChildhoodPlay" : teen ? "AdolescenceAmbience" : "YoungAdultAmbience",child ? 264 : teen ? 330 : 396);
            route.nextRoomSound=Sound(child ? "AdolescenceAmbience" : teen ? "YoungAdultAmbience" : "MiddleAgeAmbience",child ? 330 : teen ? 396 : 198); EditorUtility.SetDirty(route);
            var shell=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))).GetComponent<CorridorShell>();
            shell.name="Corridor "+(int)stage; shell.transform.position=origin+new Vector3(24,0,11); shell.gameObject.SetActive(false);
            var nextOrigin=origin+new Vector3(24+config.corridorLength,0,0);
            var teenager=child ? AdolescenceRoomSetup.Create(config,font,fly,chair,nextOrigin) : null;
            var adult=teen ? YoungAdultRoomSetup.Create(config,font,fly,chair,nextOrigin) : null;
            var middle=!child && !teen ? MiddleAgeSetup.Create(config,font,chair,nextOrigin) : null;
            var next=teenager!=null ? teenager.GetComponent<RoomDestination>() : adult!=null ? adult.GetComponent<RoomDestination>() : middle.GetComponent<RoomDestination>();
            next.transform.position=nextOrigin; next.gameObject.SetActive(false);
            var root=new GameObject(stage+" Transition"); root.SetActive(false);
            var flow=root.AddComponent<RoomTransitionController>(); var view=root.AddComponent<TransitionView>(); view.Configure(font);
            var sound=root.AddComponent<CorridorAudioController>();
            var previous=new GameObject("Previous Room Echo").AddComponent<AudioSource>(); previous.transform.SetParent(root.transform,false);
            var following=new GameObject("Next Room Ambience").AddComponent<AudioSource>(); following.transform.SetParent(root.transform,false);
            foreach(var source in new[]{previous,following}) { source.loop=true; source.playOnAwake=false; source.spatialBlend=0; }
            var echo=previous.gameObject.AddComponent<AudioEchoFilter>(); echo.delay=320; echo.decayRatio=.25f; echo.dryMix=1; echo.wetMix=0;
            sound.Configure(previous,following,echo); flow.Configure(config,route,room,fly.gameObject,chair,shell,next,view,sound);
            if(teenager!=null) teenager.ConfigureIncoming(flow);
            if(adult!=null) adult.ConfigureIncoming(flow);
            if(middle!=null) middle.ConfigureIncoming(flow);
            Boundary(root.transform,origin+new Vector3(24.2f,1,11),flow,false);
            Boundary(root.transform,origin+new Vector3(24+config.corridorLength+.7f,1,11),flow,true);
            return flow;
        }
        static void Boundary(Transform parent, Vector3 at, RoomTransitionController flow, bool destination)
        {
            var go=new GameObject(destination ? "Corridor 2 Exit Boundary" : "Childhood Exit Boundary"); go.transform.SetParent(parent,false); go.transform.position=at;
            var box=go.AddComponent<BoxCollider>(); box.isTrigger=true; box.size=new Vector3(1.2f,2,3);
            var body=go.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
            go.AddComponent<RoomExitTrigger>().Configure(flow,destination);
        }
    }
}
