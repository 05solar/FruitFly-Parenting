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
    public static class YoungAdultRoomSetup
    {
        const string Root="Assets/_Project/";
        public const string ScenePath=Root+"Scenes/05_YoungAdult.unity";
        [MenuItem("Fruit Fly Parenting/Setup 11 Young Adult Room")]
        public static void Build()
        {
            AdolescenceRoomSetup.Build();
            var scene=EditorSceneManager.OpenScene(Root+"Scenes/03_RoomShellTest.unity",OpenSceneMode.Single);
            var template=Object.FindAnyObjectByType<FlyAgent>(); var oldRoom=Object.FindAnyObjectByType<RoomController>();
            var config=AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root+"ScriptableObjects/GameBalanceConfig.asset");
            var font=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/Fonts/NotoSansCJKkr-Regular.otf");
            var adult=Create(config,font,template,Object.FindAnyObjectByType<WheelchairLocomotion>(),Vector3.zero);
            Object.DestroyImmediate(template.gameObject); Object.DestroyImmediate(oldRoom.gameObject);
            adult.ConfigureStandalone(); adult.gameObject.SetActive(true); EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            Debug.Log("Step11: young adult room, 13 items, gambling montage, recovery flags and corridor 4 saved.");
        }
        public static YoungAdultRoomController Create(GameBalanceConfig config,Font font,FlyAgent template,WheelchairLocomotion chair,Vector3 origin)
        {
            Directory.CreateDirectory(Root+"ScriptableObjects/Items/YoungAdult"); AssetDatabase.Refresh();
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Rooms/RoomShell.prefab"));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction); root.name="Young Adult Room";
            var room=root.GetComponent<RoomController>(); room.ConfigureLifeStage(LifeStage.YoungAdult);
            var surface=root.GetComponent<NavMeshSurface>(); surface.RemoveData(); Object.DestroyImmediate(root.transform.Find("ItemSlots").gameObject);
            Object.DestroyImmediate(root.GetComponent<RoomCoordinateGizmos>());
            var floor=Material("YoungAdult_Street",new Color(.25f,.30f,.35f)); var home=Material("YoungAdult_Home",new Color(.66f,.49f,.35f));
            var office=Material("YoungAdult_Office",new Color(.51f,.62f,.66f)); var gym=Material("YoungAdult_Gym",new Color(.38f,.52f,.42f));
            var wall=Material("YoungAdult_Wall",new Color(.76f,.75f,.68f)); var ink=Material("YoungAdult_Ink",new Color(.10f,.18f,.22f));
            var paper=Material("YoungAdult_Paper",new Color(.95f,.91f,.78f)); var amber=Material("YoungAdult_Amber",new Color(.89f,.62f,.24f));
            root.transform.Find("Floor").GetComponent<Renderer>().sharedMaterial=floor;
            Box("Apartment Rug",root.transform,new Vector3(7,.005f,5.5f),new Vector3(12,.01f,9),home,false);
            Box("Office Flooring",root.transform,new Vector3(8,.005f,17),new Vector3(14,.01f,9),office,false);
            Box("Gym Mat",root.transform,new Vector3(19,.005f,17),new Vector3(8,.01f,9),gym,false);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Wall_"))) renderer.sharedMaterial=wall;
            Box("Office Window",root.transform,new Vector3(7,3,21.95f),new Vector3(10,4,.04f),ink,false);
            for(int i=0;i<10;i++) Box("Painted City Building",root.transform,new Vector3(2.6f+i*.95f,2.1f+(i%3)*.2f,21.91f),new Vector3(.65f,1.4f+(i%3)*.4f,.02f),office,false);
            Box("Apartment Picture",root.transform,new Vector3(7,2.8f,.04f),new Vector3(5,2.8f,.03f),home,false);
            for(int i=0;i<3;i++) Box("Pinned Personal Note",root.transform,new Vector3(5.7f+i*1.3f,2.8f,.07f),new Vector3(.8f,1.2f,.02f),paper,false);
            for(int i=0;i<3;i++)
            {
                var lamp=new GameObject("Street Light "+i).AddComponent<Light>(); lamp.transform.SetParent(root.transform,false);
                lamp.transform.localPosition=new Vector3(21.5f,3.3f,3+i*3); lamp.type=LightType.Point; lamp.color=new Color(1,.78f,.48f); lamp.range=7; lamp.intensity=1.5f;
            }
            var slots=new GameObject("ItemSlots").transform; slots.SetParent(root.transform,false);
            foreach(var entry in YoungAdultCatalog.Entries)
            {
                var go=new GameObject("ItemSlot_"+entry.Id); go.transform.SetParent(slots,false); go.transform.localPosition=new Vector3(entry.Position.x,.3f,entry.Position.y);
                var slot=go.AddComponent<ItemSlot>(); slot.ConfigureForBuilder(entry.Id,1.2f,2.5f,2); slot.AssignDefinition(ChildhoodRoomSetup.Definition(entry,"YoungAdult")); slot.ConfigureHesitation(config.extremeItemHesitationSeconds);
                var color=entry.Extreme ? amber : entry.Position.y<11 ? home : office;
                var furniture=Box("Furniture_"+entry.Id,slots,new Vector3(entry.Position.x,.9f,entry.Position.y),new Vector3(1,1.8f,1),color,true);
                furniture.GetComponent<Renderer>().enabled=false; var modifier=furniture.AddComponent<NavMeshModifier>(); modifier.overrideArea=true; modifier.area=NavMesh.GetAreaFromName("Not Walkable");
                ChildhoodRoomSetup.Prop(entry.Id,furniture.transform,color,paper,ink);
                var prop=furniture.transform.Find("Activity"); prop.localRotation=Quaternion.Euler(0,entry.Position.x<12 ? 12 : -12,0); prop.localScale*=.84f;
                if(entry.Id.EndsWith("park")) Shape("Tree Canopy",furniture.transform,PrimitiveType.Sphere,new Vector3(0,.3f,0),new Vector3(.75f,.4f,.75f),gym);
                if(entry.Id.EndsWith("letter")) Box("Envelope",furniture.transform,new Vector3(0,.08f,0),new Vector3(.7f,.06f,.4f),paper,false);
                if(entry.Id.EndsWith("gambling")) Box("Game Display",furniture.transform,new Vector3(0,.2f,0),new Vector3(.75f,.5f,.25f),ink,false);
            }
            room.RefreshItemSlots(); ChildhoodRoomSetup.Bake(surface,"YoungAdult");
            var gate=Box("Closed Corridor Door",root.transform,new Vector3(.05f,1.75f,11),new Vector3(.1f,3.5f,3),ink,true); gate.SetActive(false);
            var door=Box("Young Adult Exit Door",root.transform,new Vector3(24,1.75f,11),new Vector3(.1f,3.5f,3),home,true);
            var destination=root.AddComponent<RoomDestination>(); destination.Configure(gate,true);
            var fly=Object.Instantiate(template,root.transform); fly.name="Young Adult Fly"; fly.gameObject.SetActive(false);
            fly.transform.localPosition=new Vector3(12,.3f,11); fly.transform.localRotation=Quaternion.identity;
            fly.ConfigureForBuilder(config,room); fly.GetComponent<FlyContactReceiver>().Configure(config);
            foreach(var renderer in fly.GetComponentsInChildren<Renderer>(true)) if(!renderer.name.Contains("Spark")) renderer.enabled=false;
            foreach(var old in fly.GetComponents<FlyHesitationFeedback>()) Object.DestroyImmediate(old);
            var oldCostume=fly.GetComponent<AdolescenceCostumeView>(); if(oldCostume!=null) Object.DestroyImmediate(oldCostume);
            foreach(string name in new[]{"Childhood Appearance","Adolescence Appearance","Flight Wings","Smoke Placeholder","Cigarette Placeholder"})
            { var old=fly.transform.Find(name); if(old!=null) Object.DestroyImmediate(old.gameObject); }
            var appearance=new GameObject("Young Adult Appearance").transform; appearance.SetParent(fly.transform,false);
            Box("Shirt",appearance,new Vector3(0,.25f,0),new Vector3(.42f,.38f,.32f),gym,false);
            for(int side=-1;side<=1;side+=2) Box("Trouser Leg",appearance,new Vector3(side*.1f,-.06f,0),new Vector3(.16f,.3f,.25f),home,false);
            Shape("Head",appearance,PrimitiveType.Sphere,new Vector3(0,.59f,0),Vector3.one*.34f,paper);
            fly.gameObject.AddComponent<FlyHesitationFeedback>().Configure(fly,appearance,config);
            var board=CorridorUI.Canvas("Gambling Calendar",root.transform,new Vector3(3,3,20),new Vector2(1400,450),.002f);
            CorridorUI.Image(board.transform,"Calendar Page",new Vector2(1400,450),Vector2.zero,new Color(.08f,.14f,.18f));
            var caption=CorridorUI.Text(board.transform,"Passing Time",font,"반짝이는 도박장\n그 뒤에 지나가는 시간",new Vector2(1340,410),Vector2.zero,44);
            var coin=Shape("Small Win Coin",root.transform,PrimitiveType.Cylinder,new Vector3(3,1.6f,19.8f),new Vector3(.35f,.04f,.35f),amber); coin.SetActive(false);
            var montage=root.AddComponent<GamblingMontage>(); montage.Configure(config,fly,caption,board.transform,coin);
            var view=root.AddComponent<InfantRoomView>(); view.Configure(font);
            var controller=root.AddComponent<YoungAdultRoomController>(); root.SetActive(false); root.transform.position=origin;
            var outgoing=ChildhoodRoomSetup.Exit(config,font,chair,root,fly,origin,floor,wall,ink,office,LifeStage.YoungAdult);
            controller.Configure(config,room,fly,destination,door,view,montage,outgoing); return controller;
        }
    }
}
