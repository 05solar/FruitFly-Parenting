using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static class PlayerEquipmentSetup
    {
        const string Root="Assets/_Project/Resources/PlayerEquipment/";
        const string Source="Assets/_Project/Art/ThirdParty/PlayerEquipment/";
        static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
        [MenuItem("Fruit Fly Parenting/Art/Prepare Player Equipment")]
        public static void Build()
        {
            Directory.CreateDirectory(Root+"Materials");AssetDatabase.Refresh();Mats.Clear();
            Mat("Graphite","29353D",.25f,.35f);Mat("Rubber","192126",0,.15f);
            Mat("Metal","A8B8BD",.7f,.55f);Mat("Teal","277F88",.3f,.4f);
            Mat("Screen","12252E",.2f,.45f);Mat("Cyan","62EEE7",.1f,.5f,true);
            foreach(string name in new[]{"Wheelchair","Baton"})
            {
                var path=Source+name+".obj";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.importNormals=ModelImporterNormals.Import;importer.addCollider=false;
                importer.importAnimation=false;importer.SaveAndReimport();
                foreach(var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    string key=material.name.StartsWith("alloy") || material.name=="spoke" ? "Metal" :
                        material.name.StartsWith("wheelchair") ? "Graphite" : "Rubber";
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.name),Mats[key]);
                }
                importer.SaveAndReimport();
            }
            Chair();Baton();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("PLAYER_EQUIPMENT_READY: imported wheelchair and baton, left joystick console and electric arcs.");
        }
        static Material Mat(string name,string hex,float metal,float smooth,bool glow=false)
        {
            string path=Root+"Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            ColorUtility.TryParseHtmlString("#"+hex,out var color);mat.SetColor("_BaseColor",color);
            mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",smooth);
            if(glow){mat.SetColor("_EmissionColor",color*1.3f);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;mat.EnableKeyword("_EMISSION");}
            Mats[name]=mat;EditorUtility.SetDirty(mat);return mat;
        }
        static void Chair()
        {
            var root=new GameObject("PoweredChair");var t=root.transform;
            Model("Wheelchair",t);
            Shape("Electric drive housing",t,PrimitiveType.Cube,new Vector3(0,.24f,-.08f),new Vector3(.43f,.25f,.43f),"Teal");
            for(int i=0;i<5;i++)Shape("Cooling rib",t,PrimitiveType.Cube,new Vector3(0,.20f+i*.025f,-.3f),new Vector3(.34f,.008f,.012f),"Graphite");
            foreach(float side in new[]{-1f,1f})
            {
                var cover=Shape("Motor cover",t,PrimitiveType.Cylinder,new Vector3(side*.346f,.305f,-.11f),new Vector3(.29f,.012f,.29f),"Teal");
                cover.transform.localRotation=Quaternion.Euler(0,0,90);
                var cap=Shape("Axle cap",t,PrimitiveType.Cylinder,new Vector3(side*.361f,.305f,-.11f),new Vector3(.085f,.013f,.085f),"Metal");
                cap.transform.localRotation=Quaternion.Euler(0,0,90);
                Shape("Arm pad",t,PrimitiveType.Cube,new Vector3(side*.277f,.72f,.08f),new Vector3(.105f,.055f,.4f),"Graphite");
            }
            Rod("Console support",t,new Vector3(-.28f,.70f,.25f),new Vector3(-.26f,.89f,.52f),.025f,"Metal");
            var console=new GameObject("Left Console").transform;console.SetParent(t,false);console.localPosition=new Vector3(-.26f,.9f,.55f);
            Shape("Console lower shell",console,PrimitiveType.Cube,Vector3.zero,new Vector3(.215f,.07f,.29f),"Teal");
            Shape("Console top",console,PrimitiveType.Cube,new Vector3(0,.038f,0),new Vector3(.2f,.025f,.275f),"Graphite");
            var pivot=new GameObject("Joystick Pivot").transform;pivot.SetParent(console,false);pivot.localPosition=new Vector3(0,.06f,.065f);
            Shape("Dust boot",pivot,PrimitiveType.Sphere,new Vector3(0,.005f,0),new Vector3(.075f,.035f,.075f),"Rubber");
            Rod("Joystick shaft",pivot,Vector3.zero,new Vector3(0,.11f,0),.014f,"Metal");
            Shape("Joystick grip",pivot,PrimitiveType.Sphere,new Vector3(0,.12f,0),new Vector3(.065f,.055f,.065f),"Graphite");
            Shape("Joystick cap",pivot,PrimitiveType.Cylinder,new Vector3(0,.146f,0),new Vector3(.036f,.002f,.036f),"Teal");
            Shape("Status display",console,PrimitiveType.Cube,new Vector3(0,.054f,-.067f),new Vector3(.135f,.008f,.07f),"Screen");
            for(int i=0;i<4;i++)Shape("Power indicator",console,PrimitiveType.Cube,new Vector3(-.043f+i*.028f,.059f,-.07f),new Vector3(.017f,.003f,.032f),"Cyan");
            Shape("Power button",console,PrimitiveType.Cylinder,new Vector3(.073f,.055f,.036f),new Vector3(.025f,.006f,.025f),"Cyan");
            Save(root,"PoweredChair");
        }
        static void Baton()
        {
            var root=new GameObject("ElectricBaton");var t=root.transform;
            var model=Model("Baton",t);model.transform.localRotation=Quaternion.Euler(90,0,0);
            var bounds=Bounds(model);model.transform.localScale*=.515f/bounds.size.z;
            // The original baton has a side handle. Centre its shaft, not the bounds of that handle.
            bounds=Bounds(model);model.transform.localPosition-=new Vector3(bounds.center.x,.0384595f*model.transform.localScale.x,bounds.min.z);
            for(int i=0;i<6;i++)
            {
                var ring=Shape("Grip rib",t,PrimitiveType.Cylinder,new Vector3(0,0,.035f+i*.018f),new Vector3(.052f,.004f,.052f),"Graphite");
                ring.transform.localRotation=Quaternion.Euler(90,0,0);
            }
            foreach(float z in new[]{.22f,.29f,.36f,.43f})
            {
                var collar=Shape("Metal collar",t,PrimitiveType.Cylinder,new Vector3(0,0,z),new Vector3(.057f,.009f,.057f),"Metal");
                collar.transform.localRotation=Quaternion.Euler(90,0,0);
                var coil=Shape("Powered coil",t,PrimitiveType.Cylinder,new Vector3(0,0,z+.011f),new Vector3(.049f,.003f,.049f),"Cyan");
                coil.transform.localRotation=Quaternion.Euler(90,0,0);
            }
            foreach(float side in new[]{-1f,1f}) Rod("Electrode",t,new Vector3(side*.027f,0,.43f),new Vector3(side*.027f,0,.522f),.013f,"Metal");
            for(int a=0;a<2;a++)
            {
                var arc=new GameObject("Electric arc "+a).AddComponent<LineRenderer>();arc.transform.SetParent(t,false);
                arc.useWorldSpace=false;arc.sharedMaterial=Mats["Cyan"];arc.positionCount=9;arc.widthMultiplier=.0024f;
                arc.shadowCastingMode=ShadowCastingMode.Off;arc.receiveShadows=false;arc.numCapVertices=3;
                for(int i=0;i<9;i++)arc.SetPosition(i,new Vector3(-.027f+i*.00675f,a*.011f,.522f+Mathf.Sin(i/8f*Mathf.PI)*.025f));
            }
            Save(root,"ElectricBaton");
        }
        static GameObject Model(string name,Transform parent)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source+name+".obj"),parent,false);
            go.name=name+" Model";return go;
        }
        static Bounds Bounds(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
        }
        static void Rod(string name,Transform parent,Vector3 from,Vector3 to,float diameter,string material)
        {
            var go=Shape(name,parent,PrimitiveType.Cylinder,(from+to)*.5f,new Vector3(diameter,Vector3.Distance(from,to)*.5f,diameter),material);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);
        }
        static GameObject Shape(string name,Transform parent,PrimitiveType type,Vector3 pos,Vector3 scale,string material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=scale;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial=Mats[material];return go;
        }
        static void Save(GameObject go,string name){PrefabUtility.SaveAsPrefabAsset(go,Root+name+".prefab");Object.DestroyImmediate(go);}
        public static void BuildAndReview()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            ShaderUtil.allowAsyncCompilation=false;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.64f,.67f);
            var light=new GameObject("Review daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;
            light.transform.rotation=Quaternion.Euler(50,-35,0);
            var chair=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"PoweredChair.prefab"));
            var camera=new GameObject("Review camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.15f,.19f,.22f);camera.fieldOfView=42;
            camera.transform.position=new Vector3(-1.25f,1.35f,1.75f);camera.transform.LookAt(new Vector3(0,.52f,.07f));
            var bounds=Bounds(chair.transform.Find("Wheelchair Model").gameObject);
            if(bounds.size.x<.6f || bounds.size.x>.8f || bounds.size.y<.8f || bounds.size.y>1.1f)
                throw new InvalidOperationException("Imported wheelchair scale is incorrect: "+bounds.size);
            var target=new RenderTexture(1200,1000,24);camera.targetTexture=target;camera.aspect=1.2f;
            camera.Render();camera.Render();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1200,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,1000),0,0);image.Apply();
            File.WriteAllBytes("Logs/player-equipment-exterior.png",image.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);
            Debug.Log("PLAYER_EQUIPMENT_REVIEWED: wheelchair metres "+bounds.size);
        }
    }
}
