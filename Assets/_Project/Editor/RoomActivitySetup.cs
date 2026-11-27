using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static partial class RoomArtSetup
    {
        const string ActivitySource="Assets/_Project/Art/ThirdParty/ActivityProps/";
        public static void PrepareActivityReview()
        {
            FlyCharacterSetup.Build();
            Build();
            RoomArtVerification.Verify();
        }
        [Serializable] class ActivityCatalog { public ActivityModel[] models; }
        [Serializable] class ActivityModel { public string name; public ActivityMaterial[] materials; }
        [Serializable] class ActivityMaterial { public string name; public float[] color; public string texture; }

        static void PrepareActivityModels()
        {
            MakeMaterial("Grass",Hex("83AA69"));MakeMaterial("Trail",Hex("D7BE8E"));
            MakeMaterial("Night",Hex("293C69"));MakeMaterial("Paper",Hex("FFF4D8"));
            MakeMaterial("Coral",Hex("D78069"));MakeMaterial("Pitch",Hex("559579"));
            var catalog=JsonUtility.FromJson<ActivityCatalog>(File.ReadAllText(ActivitySource+"materials.json"));
            foreach(var model in catalog.models)
            {
                foreach(var surface in model.materials)
                {
                    var c=surface.color;
                    var mat=MakeMaterial(surface.name,new Color(c[0],c[1],c[2],c[3]).gamma);
                    mat.SetFloat("_Cull",0);
                    if(!string.IsNullOrEmpty(surface.texture))mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(ActivitySource+surface.texture));
                }
                var importer=(ModelImporter)AssetImporter.GetAtPath(ActivitySource+model.name+".obj");
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.importNormals=ModelImporterNormals.Import;importer.addCollider=false;importer.SaveAndReimport();
                foreach(var surface in model.materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),surface.name),Materials[surface.name]);
                importer.SaveAndReimport();
            }
        }

        static GameObject Prop(string name,Transform parent,Vector3 at,float width,float yaw=0)
        {
            var wrapper=new GameObject(name);wrapper.transform.SetParent(parent,false);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ActivitySource+name+".obj");
            if(source==null)throw new InvalidOperationException("Missing activity asset: "+name);
            var model=Object.Instantiate(source,wrapper.transform,false);model.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var bounds=Bounds(wrapper);model.transform.localScale*=width/Mathf.Max(bounds.size.x,bounds.size.z);
            bounds=Bounds(wrapper);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-wrapper.transform.position;
            wrapper.transform.localPosition=at;return wrapper;
        }
        static float Top(GameObject go)=>Bounds(go).max.y;
        static void Ground(Transform t,string color,float width=1.4f,float depth=1.2f)
            =>Box(t,"Activity floor",new Vector3(0,.008f,0),new Vector3(width,.016f,depth),color);
        static void Paper(Transform t,Vector3 at,bool letter=false)
        {
            Box(t,letter ? "Letter paper":"Study paper",at,new Vector3(.18f,.008f,.23f),"Paper");
            for(int i=0;i<4;i++)Box(t,"Writing",at+new Vector3(0,.006f,-.06f+i*.035f),new Vector3(.12f,.003f,.006f),"Slate");
            Box(t,"Pencil",at+new Vector3(.13f,.012f,0),new Vector3(.012f,.012f,.2f),"Ochre");
        }
        static void Dining(Transform t,bool birthday)
        {
            var table=Model(birthday ? "tableCloth":"table",t,Vector3.zero,.95f);
            Model("chair",t,new Vector3(-.33f,0,-.38f),.25f);
            Model("chairCushion",t,new Vector3(.33f,0,-.38f),.25f);
            float y=Top(table);
            if(birthday)Prop("Cake",t,new Vector3(0,y,0),.32f);
            else
            {
                for(int i=-1;i<=1;i+=2)
                {
                    Shape(t,"Plate",PrimitiveType.Cylinder,new Vector3(i*.25f,y+.015f,0),new Vector3(.2f,.008f,.2f),"Paper");
                    Prop("Cake",t,new Vector3(i*.25f,y+.03f,0),.12f);
                }
                Model("plantSmall1",t,new Vector3(0,y,.12f),.15f);
            }
        }
        static void Study(Transform t,string key)
        {
            var desk=Model(key=="studio" ? "deskCorner":"desk",t,new Vector3(0,0,.06f),.9f);
            float y=Top(desk);
            Model("chairDesk",t,new Vector3(0,0,-.31f),.27f);
            bool screen=new[]{"tablet","learning","nightgame","investment","startup","studio"}.Contains(key);
            if(screen)
            {
                Model(key=="tablet" || key=="learning" ? "computerScreen":"laptop",t,new Vector3(-.08f,y,.03f),.36f);
                if(key=="learning")Model("books",t,new Vector3(.28f,y,.05f),.18f);
                if(key=="nightgame"){Model("speakerSmall",t,new Vector3(.31f,y,.08f),.13f);Model("computerKeyboard",t,new Vector3(0,y,-.15f),.25f);}
            }
            else Model("books",t,new Vector3(-.22f,y,.08f),.22f);
            Paper(t,new Vector3(.17f,y+.01f,-.05f),key=="letter");
            if(key=="exam" || key=="contest" || key=="gifted")
            {
                Model("lampSquareTable",t,new Vector3(.3f,y,.18f),.15f);
                Box(t,"Exam board",new Vector3(0,.93f,.4f),new Vector3(.9f,.38f,.035f),"Slate");
                for(int i=0;i<3;i++)Box(t,"Board writing",new Vector3(-.05f,.84f+i*.075f,.377f),new Vector3(.56f,.012f,.01f),"Paper");
            }
        }
        static void Outdoors(Transform t,string key)
        {
            bool night=key=="stars";Ground(t,night ? "Slate":"Grass");
            Box(t,"Outdoor wallpaper",new Vector3(0,.74f,.57f),new Vector3(1.4f,1.48f,.025f),night ? "Night":"Sky");
            if(night)
            {
                for(int i=0;i<12;i++)Shape(t,"Wallpaper star",PrimitiveType.Sphere,new Vector3(-.6f+(i*7%13)*.1f,.8f+(i*3%7)*.08f,.548f),new Vector3(.022f,.022f,.007f),"Paper");
                Shape(t,"Moon",PrimitiveType.Sphere,new Vector3(.4f,1.2f,.54f),new Vector3(.17f,.17f,.012f),"Paper");
                Prop("Telescope",t,new Vector3(-.05f,0,0),.55f,30);
                Model("bench",t,new Vector3(.38f,0,-.28f),.45f);
            }
            else
            {
                // Flattened downloaded trees form the mural behind full 3D trees and stones.
                var mural=Prop("Tree",t,new Vector3(.15f,.2f,.545f),.75f);mural.transform.localScale=new Vector3(1,1,.035f);
                Shape(t,"Sun",PrimitiveType.Sphere,new Vector3(-.4f,1.18f,.54f),new Vector3(.2f,.2f,.012f),"Ochre");
                Prop("Tree",t,new Vector3(-.44f,0,.22f),.43f);
                Prop("Pine",t,new Vector3(.46f,0,.3f),.31f);
                var path=Prop("Path",t,new Vector3(0,.02f,-.22f),.75f);path.transform.localScale=new Vector3(.55f,.25f,1.2f);
                Prop("Rock",t,new Vector3(-.37f,.015f,-.35f),.22f);
                Prop("Rock",t,new Vector3(.35f,.015f,-.1f),.16f,45);
                if(key=="park")Model("bench",t,new Vector3(.18f,0,.14f),.6f);
                if(key=="quit")Model("cardboardBoxClosed",t,new Vector3(.28f,0,-.34f),.25f);
            }
        }
        static void Sports(Transform t,string key)
        {
            Ground(t,"Pitch",1.35f,1.15f);
            Prop("Goal",t,new Vector3(0,.02f,.28f),1.05f,180);
            for(int i=-1;i<=1;i+=2)Box(t,"Sideline",new Vector3(i*.59f,.02f,0),new Vector3(.014f,.008f,1.02f),"Paper");
            Box(t,"Penalty line",new Vector3(0,.02f,-.32f),new Vector3(1.18f,.008f,.014f),"Paper");
            var ball=Shape(t,"Football",PrimitiveType.Sphere,new Vector3(.1f,.12f,-.28f),Vector3.one*.21f,"Paper");
            Shape(ball.transform,"Football stripe",PrimitiveType.Cylinder,Vector3.zero,new Vector3(1.01f,.12f,1.01f),"Slate");
            if(key!="soccer")for(int i=-1;i<=1;i+=2)Shape(t,"Training marker",PrimitiveType.Cylinder,new Vector3(i*.4f,.06f,-.2f),new Vector3(.12f,.06f,.12f),"Coral");
        }
        static void Workshop(Transform t,bool performance)
        {
            if(performance)
            {
                Ground(t,"woodDark");Prop("Guitar",t,new Vector3(0,.02f,0),.35f);
                Model("speaker",t,new Vector3(-.47f,0,.12f),.25f);Model("speaker",t,new Vector3(.47f,0,.12f),.25f);
                Model("lampRoundFloor",t,new Vector3(-.45f,0,-.27f),.2f);
            }
            else
            {
                var table=Model("table",t,Vector3.zero,.95f);float y=Top(table);
                Model("cardboardBoxOpen",t,new Vector3(.27f,y,.12f),.22f);Model("books",t,new Vector3(-.3f,y,.13f),.2f);
                for(int i=0;i<5;i++)Box(t,"Craft piece",new Vector3(-.2f+i*.09f,y+.03f,-.12f),new Vector3(.07f,.05f,.12f),i%2==0 ? "Coral":"Blue");
                Paper(t,new Vector3(0,y+.015f,.07f));
                Model("stoolBar",t,new Vector3(0,0,-.35f),.24f);
            }
        }
        static void BuildActivityItem(string id)
        {
            var root=new GameObject(id);var t=root.transform;var key=id.Substring(id.IndexOf('_')+1);
            switch(key)
            {
                case "walk": case "park": case "stars": case "dayoff": case "quit": Outdoors(t,key);break;
                case "soccer": case "stadium": case "training": case "selection": Sports(t,key);break;
                case "birthday": case "dinner": case "family": Dining(t,key=="birthday");break;
                case "arcade": Prop("Arcade",t,Vector3.zero,.75f);break;
                case "gambling":
                    Prop("Arcade",t,new Vector3(-.24f,0,0),.48f);Prop("Arcade",t,new Vector3(.24f,0,0),.48f);
                    for(int i=0;i<5;i++)Shape(t,"Gaming chip",PrimitiveType.Cylinder,new Vector3(.08f+i*.075f,.018f,-.42f),new Vector3(.06f,.008f,.06f),i%2==0 ? "Coral":"Ochre");break;
                case "bed": Model("bedSingle",t,Vector3.zero,.96f);Model("bear",t,new Vector3(.18f,.18f,.18f),.22f);break;
                case "hug": Model("loungeChairRelax",t,Vector3.zero,.85f);Model("pillowBlue",t,new Vector3(0,.38f,0),.28f);break;
                case "parents": case "counsel":
                    Model("loungeChair",t,new Vector3(-.3f,0,0),.42f);Model("chairModernCushion",t,new Vector3(.3f,0,0),.42f);
                    Model("tableCoffee",t,new Vector3(0,0,-.25f),.3f);break;
                case "book": case "library":
                    Model("bookcaseOpenLow",t,new Vector3(0,0,.2f),.9f);Model("pillow",t,new Vector3(-.24f,0,-.25f),.32f);Model("books",t,new Vector3(.15f,.04f,-.22f),.26f);break;
                case "blocks": Ground(t,"Sage",1,1);for(int i=0;i<6;i++)Box(t,"Sensory block",new Vector3((i%3-1)*.23f,.1f+(i/3)*.15f,i/3*.2f-.1f),Vector3.one*.18f,i%3==0 ? "Coral":i%3==1 ? "Blue":"Ochre");Model("bear",t,new Vector3(.28f,0,.3f),.27f);break;
                case "crawl":
                    Ground(t,"Blue",1.25f,.95f);for(int i=0;i<3;i++){Model("pillow",t,new Vector3(0,0,-.3f+i*.3f),.35f);Box(t,"Crawl arch left",new Vector3(-.4f,.25f,-.3f+i*.3f),new Vector3(.1f,.5f,.1f),"Ochre");Box(t,"Crawl arch right",new Vector3(.4f,.25f,-.3f+i*.3f),new Vector3(.1f,.5f,.1f),"Ochre");Box(t,"Crawl arch top",new Vector3(0,.5f,-.3f+i*.3f),new Vector3(.9f,.1f,.1f),"Coral");}break;
                case "ball": Ground(t,"Sage",1,1);Model("bear",t,new Vector3(-.25f,0,.14f),.35f);Shape(t,"Play ball",PrimitiveType.Sphere,new Vector3(.2f,.18f,-.1f),Vector3.one*.36f,"Coral");break;
                case "snack": Model("cardboardBoxOpen",t,Vector3.zero,.75f);Prop("Cake",t,new Vector3(0,.18f,0),.35f);break;
                case "craft": case "club": case "workshop": Workshop(t,false);break;
                case "performance": Workshop(t,true);break;
                case "star":
                    Ground(t,"Coral");Model("loungeChair",t,Vector3.zero,.5f);Model("lampRoundFloor",t,new Vector3(-.45f,0,.2f),.24f);Model("lampSquareFloor",t,new Vector3(.45f,0,.2f),.24f);Box(t,"Studio backdrop",new Vector3(0,.75f,.45f),new Vector3(1.2f,1.5f,.025f),"Sky");break;
                case "hideout": Model("cardboardBoxOpen",t,Vector3.zero,.85f);Model("pillowBlue",t,new Vector3(.15f,0,-.3f),.32f);Paper(t,new Vector3(-.25f,.02f,-.32f));break;
                case "camp": Model("bedBunk",t,new Vector3(-.28f,0,.1f),.52f);var desk=Model("desk",t,new Vector3(.28f,0,-.05f),.5f);Model("books",t,new Vector3(.28f,Top(desk),0),.2f);break;
                case "job": var counter=Model("kitchenBar",t,Vector3.zero,.9f);Model("laptop",t,new Vector3(-.15f,Top(counter),0),.3f);Model("kitchenCoffeeMachine",t,new Vector3(.28f,Top(counter),0),.22f);break;
                case "theft": Model("bookcaseOpen",t,new Vector3(0,0,.2f),.85f);Model("cardboardBoxOpen",t,new Vector3(.25f,0,-.25f),.32f);Model("radio",t,new Vector3(-.2f,.15f,-.3f),.28f);break;
                case "independence": Model("bedSingle",t,new Vector3(-.15f,0,0),.85f);Model("cardboardBoxClosed",t,new Vector3(.35f,0,-.25f),.28f);Model("cardboardBoxOpen",t,new Vector3(.35f,.2f,-.25f),.23f);break;
                case "interview": var table=Model("table",t,Vector3.zero,.85f);Model("chairModernCushion",t,new Vector3(0,0,-.35f),.3f);Paper(t,new Vector3(0,Top(table),0));Model("pottedPlant",t,new Vector3(.4f,0,.3f),.22f);break;
                default: Study(t,key);break;
            }
            // Keep each diorama within its own interaction corner; runtime fits collision to these bounds.
            var bounds=Bounds(root);float scale=Mathf.Min(1f,1.48f/Mathf.Max(bounds.size.x,bounds.size.z),1.72f/bounds.size.y);
            root.transform.localScale=Vector3.one*scale;
            bounds=Bounds(root);foreach(Transform child in root.transform)child.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            Save(root,"Items/"+id);
        }
    }
}
