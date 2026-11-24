using System.IO;
using System.Linq;
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
using Object=UnityEngine.Object;
using static FruitFlyParenting.Editor.CorridorSetup;

namespace FruitFlyParenting.Editor
{
    public static class AdolescenceRoomSetup
    {
        const string Root="Assets/_Project/";
        public const string ScenePath=Root+"Scenes/04_Adolescence.unity";
        [MenuItem("Fruit Fly Parenting/Setup 10 Adolescence Room")]
        public static void Build()
        {
            ChildhoodRoomSetup.Build();
            var scene=EditorSceneManager.OpenScene(Root+"Scenes/03_RoomShellTest.unity",OpenSceneMode.Single);
            var template=Object.FindAnyObjectByType<FlyAgent>(); var oldRoom=Object.FindAnyObjectByType<RoomController>();
            var config=AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root+"ScriptableObjects/GameBalanceConfig.asset");
            var font=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/Fonts/NotoSansCJKkr-Regular.otf");
            var teen=Create(config,font,template,Object.FindAnyObjectByType<WheelchairLocomotion>(),Vector3.zero);
            Object.DestroyImmediate(template.gameObject); Object.DestroyImmediate(oldRoom.gameObject);
            teen.ConfigureStandalone(); teen.gameObject.SetActive(true); EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            Debug.Log("Step10: adolescence 13 items, costumes, flight/smoke sequence and corridor 3 saved.");
        }
        public static AdolescenceRoomController Create(GameBalanceConfig config,Font font,FlyAgent template,WheelchairLocomotion chair,Vector3 origin)
        {
            Directory.CreateDirectory(Root+"ScriptableObjects/Items/Adolescence"); AssetDatabase.Refresh();
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Rooms/RoomShell.prefab"));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction); root.name="Adolescence Room";
            var room=root.GetComponent<RoomController>(); room.ConfigureLifeStage(LifeStage.Adolescence);
            var surface=root.GetComponent<NavMeshSurface>(); surface.RemoveData(); Object.DestroyImmediate(root.transform.Find("ItemSlots").gameObject);
            Object.DestroyImmediate(root.GetComponent<RoomCoordinateGizmos>());
            var floor=Material("Adolescence_School",new Color(.40f,.46f,.54f)); var track=Material("Adolescence_Track",new Color(.49f,.30f,.28f));
            var wall=Material("Adolescence_Wall",new Color(.69f,.73f,.77f)); var night=Material("Adolescence_Night",new Color(.07f,.12f,.23f));
            var paper=Material("Adolescence_Paper",new Color(.92f,.9f,.8f)); var blue=Material("Adolescence_Blue",new Color(.24f,.43f,.58f));
            var amber=Material("Adolescence_Amber",new Color(.76f,.53f,.27f));
            root.transform.Find("Floor").GetComponent<Renderer>().sharedMaterial=floor;
            Box("Sports Ground",root.transform,new Vector3(18,.005f,11),new Vector3(12,.01f,22),track,false);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Wall_"))) renderer.sharedMaterial=wall;
            for(int i=0;i<8;i++)
            {
                Box("Painted School Locker",root.transform,new Vector3(2.2f+i*1.1f,2.5f,21.96f),new Vector3(.9f,3,.03f),blue,false);
                Box("Locker Handle",root.transform,new Vector3(2.4f+i*1.1f,2.4f,21.92f),new Vector3(.06f,.22f,.02f),paper,false);
            }
            Box("Night Window",root.transform,new Vector3(19,3,21.95f),new Vector3(8,4,.04f),night,false);
            for(int i=0;i<15;i++) Shape("Window Star",root.transform,PrimitiveType.Sphere,new Vector3(15.5f+(i*1.37f)%7,1.4f+(i*.83f)%3.3f,21.90f),Vector3.one*.07f,paper);
            Shape("Moon",root.transform,PrimitiveType.Sphere,new Vector3(21,4.1f,21.90f),new Vector3(.65f,.65f,.03f),paper);
            for(int i=0;i<3;i++) Box("Track Line",root.transform,new Vector3(14+i*3,.016f,11),new Vector3(.05f,.008f,20),paper,false);
            var light=new GameObject("Window Light").AddComponent<Light>(); light.transform.SetParent(root.transform,false);
            light.transform.localPosition=new Vector3(20,4,19); light.type=LightType.Point; light.color=new Color(.6f,.72f,1); light.intensity=2; light.range=16;
            var slots=new GameObject("ItemSlots").transform; slots.SetParent(root.transform,false);
            foreach(var entry in AdolescenceCatalog.Entries)
            {
                var go=new GameObject("ItemSlot_"+entry.Id); go.transform.SetParent(slots,false); go.transform.localPosition=new Vector3(entry.Position.x,.3f,entry.Position.y);
                var slot=go.AddComponent<ItemSlot>(); slot.ConfigureForBuilder(entry.Id,1.2f,2.5f,2);
                slot.AssignDefinition(ChildhoodRoomSetup.Definition(entry,"Adolescence")); slot.ConfigureHesitation(config.extremeItemHesitationSeconds);
                var color=entry.Extreme ? amber : blue;
                var furniture=Box("Furniture_"+entry.Id,slots,new Vector3(entry.Position.x,.9f,entry.Position.y),new Vector3(1,1.8f,1),color,true);
                furniture.GetComponent<Renderer>().enabled=false; var modifier=furniture.AddComponent<NavMeshModifier>(); modifier.overrideArea=true; modifier.area=NavMesh.GetAreaFromName("Not Walkable");
                ChildhoodRoomSetup.Prop(entry.Id,furniture.transform,color,paper,night);
                if(entry.Id.EndsWith("parents") || entry.Id.EndsWith("counsel"))
                    Box("Chair Back",furniture.transform,new Vector3(0,.15f,.35f),new Vector3(.85f,.5f,.15f),wall,false);
                if(entry.Id.EndsWith("performance"))
                { Box("Microphone Stand",furniture.transform,new Vector3(0,.1f,0),new Vector3(.05f,.8f,.05f),night,false); Shape("Microphone",furniture.transform,PrimitiveType.Sphere,new Vector3(0,.5f,0),Vector3.one*.15f,paper); }
                if(entry.Id.EndsWith("theft")) Box("Unpaid Goods",furniture.transform,new Vector3(0,.08f,0),new Vector3(.35f,.3f,.35f),night,false);
            }
            room.RefreshItemSlots(); ChildhoodRoomSetup.Bake(surface,"Adolescence");
            var gate=Box("Closed Corridor Door",root.transform,new Vector3(.05f,1.75f,11),new Vector3(.1f,3.5f,3),night,true); gate.SetActive(false);
            var door=Box("Adolescence Exit Door",root.transform,new Vector3(24,1.75f,11),new Vector3(.1f,3.5f,3),blue,true);
            var destination=root.AddComponent<RoomDestination>(); destination.Configure(gate,true);
            var fly=Object.Instantiate(template,root.transform); fly.name="Adolescence Fly"; fly.gameObject.SetActive(false);
            fly.transform.localPosition=new Vector3(12,.3f,11); fly.transform.localRotation=Quaternion.identity;
            fly.ConfigureForBuilder(config,room); fly.GetComponent<FlyContactReceiver>().Configure(config);
            foreach(var renderer in fly.GetComponentsInChildren<Renderer>(true)) if(!renderer.name.Contains("Spark")) renderer.enabled=false;
            foreach(var old in fly.GetComponents<FlyHesitationFeedback>()) Object.DestroyImmediate(old);
            var oldAppearance=fly.transform.Find("Childhood Appearance"); if(oldAppearance!=null) Object.DestroyImmediate(oldAppearance.gameObject);
            var appearance=new GameObject("Adolescence Appearance").transform; appearance.SetParent(fly.transform,false);
            var uniform=Clothes("School Uniform",appearance,wall,night);
            var hidden=Clothes("Black Shirt Green Trousers",appearance,Material("Adolescence_Black",new Color(.06f,.065f,.07f)),Material("Adolescence_Green",new Color(.2f,.39f,.22f)));
            hidden.SetActive(false); Shape("Head",appearance,PrimitiveType.Sphere,new Vector3(0,.57f,0),Vector3.one*.33f,paper);
            var costume=fly.gameObject.AddComponent<AdolescenceCostumeView>(); costume.Configure(uniform,hidden);
            fly.gameObject.AddComponent<FlyHesitationFeedback>().Configure(fly,appearance,config);
            var wings=new GameObject("Flight Wings"); wings.transform.SetParent(fly.transform,false);
            for(int side=-1;side<=1;side+=2) Shape("Wing",wings.transform,PrimitiveType.Sphere,new Vector3(side*.4f,.4f,-.1f),new Vector3(.75f,.04f,.35f),paper);
            wings.SetActive(false);
            var smoke=new GameObject("Smoke Placeholder"); smoke.transform.SetParent(fly.transform,false);
            for(int i=0;i<3;i++) Shape("Smoke Puff",smoke.transform,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.15f,wall);
            smoke.SetActive(false);
            var cigarette=Box("Cigarette Placeholder",fly.transform,new Vector3(.1f,.5f,.26f),new Vector3(.035f,.035f,.18f),paper,false); cigarette.SetActive(false);
            var sequence=root.AddComponent<AdolescenceExitSequence>(); sequence.Configure(config,fly,chair,root.transform,wings,smoke,cigarette);
            var view=root.AddComponent<InfantRoomView>(); view.Configure(font);
            var controller=root.AddComponent<AdolescenceRoomController>(); root.SetActive(false); root.transform.position=origin;
            var outgoing=ChildhoodRoomSetup.Exit(config,font,chair,root,fly,origin,floor,wall,night,blue,LifeStage.Adolescence);
            controller.Configure(config,room,fly,destination,door,view,costume,sequence,outgoing); return controller;
        }
        static GameObject Clothes(string name,Transform parent,Material shirt,Material trousers)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            Box("Shirt",go.transform,new Vector3(0,.25f,0),new Vector3(.4f,.35f,.3f),shirt,false);
            for(int side=-1;side<=1;side+=2) Box("Trouser Leg",go.transform,new Vector3(side*.1f,-.05f,0),new Vector3(.15f,.3f,.25f),trousers,false);
            return go;
        }
    }
}
