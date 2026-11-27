using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FruitFlyParenting.Corridor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static class FlyCharacterSetup
    {
        const string Root="Assets/_Project/Resources/FlyCharacter/";
        const string Source="Assets/_Project/Art/ThirdParty/Fly/Fly.obj";
        static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
        [MenuItem("Fruit Fly Parenting/Art/Prepare Fly Character")]
        public static void Build()
        {
            Directory.CreateDirectory(Root+"Materials");AssetDatabase.Refresh();Mats.Clear();
            Mat("Body","59B84D",.15f);Mat("Dark","314344",.2f);Mat("Eyes","FF4235",.35f);
            Mat("Wings","A6DEE8",.25f);Mat("WingHighlight","E8E8E8",.25f);
            Mat("Cream","F3DFB4",.2f);Mat("Blue","568EAB",.2f);Mat("Gold","E2BB62",.2f);
            Mat("Navy","304858",.2f);Mat("Green","5D7963",.2f);Mat("Teal","508D87",.2f);
            foreach(string path in new[]{Source,Source.Replace("Fly.obj","Wings_0.obj"),Source.Replace("Fly.obj","Wings_1.obj")})
            {
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.importNormals=ModelImporterNormals.Import;importer.addCollider=false;importer.preserveHierarchy=true;importer.SaveAndReimport();
            foreach(var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),mat.name),Mats[mat.name]);
            importer.SaveAndReimport();
            }
            FlyClothingSetup.Prepare();
            foreach(var stage in new[]{LifeStage.Infant,LifeStage.Childhood,LifeStage.Adolescence,LifeStage.YoungAdult,LifeStage.MiddleAge})BuildStage(stage);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Review();
            Debug.Log("FLY_CHARACTER_READY: AroniaStudios Fly, five growth variants, two articulated wings; original asset 648 triangles.");
        }
        static void Mat(string name,string hex,float smooth)
        {
            var path=Root+"Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            ColorUtility.TryParseHtmlString("#"+hex,out var color);mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smooth);
            mat.SetFloat("_Metallic",0);mat.SetFloat("_Cull",0);Mats[name]=mat;EditorUtility.SetDirty(mat);
        }
        static void BuildStage(LifeStage stage)
        {
            var root=new GameObject(stage.ToString());var t=root.transform;
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source),t,false);model.name="Downloaded Fly";
            var wings=new List<GameObject>();
            foreach(var name in new[]{"Wings_0.obj","Wings_1.obj"})wings.Add(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source.Replace("Fly.obj",name)),model.transform,false));
            float length=stage==LifeStage.Infant ? .65f : stage==LifeStage.Childhood ? .75f : stage==LifeStage.Adolescence ? .82f : .9f;
            var bounds=Bounds(model);float scale=length/bounds.size.z;model.transform.localScale=Vector3.one*scale;
            bounds=Bounds(model);model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            wings=wings.OrderBy(w=>Bounds(w).center.x).ToList();
            for(int i=0;i<2;i++)
            {
                var pivot=new GameObject("Wing Pivot "+i).transform;pivot.SetParent(t,false);
                // Hinge at the inner wing root, in the imported model's coordinates.
                pivot.localPosition=model.transform.localPosition+new Vector3((i==0 ? -.25f:.25f)*scale,.9f*scale,.8f*scale);
                wings[i].transform.SetParent(pivot,true);
            }
            float headY=model.transform.localPosition.y+1.3f*scale;float headZ=3.2f*scale+model.transform.localPosition.z;
            FlyClothingSetup.Dress(stage,t,length,headY,headZ);
            PrefabUtility.SaveAsPrefabAsset(root,Root+stage+".prefab");Object.DestroyImmediate(root);
        }
        static Bounds Bounds(GameObject root){var rs=root.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        static void Review()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            ShaderUtil.allowAsyncCompilation=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            var sun=new GameObject("Light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.8f;sun.transform.rotation=Quaternion.Euler(50,-30,0);
            var models=new List<GameObject>();
            int i=0;foreach(var stage in new[]{LifeStage.Infant,LifeStage.Childhood,LifeStage.Adolescence,LifeStage.YoungAdult})
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+stage+".prefab"));go.transform.position=new Vector3((i++-1.5f)*1.05f,0,0);go.transform.rotation=Quaternion.Euler(0,-25,0);
                models.Add(go);
            }
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,1.7f,4.5f);camera.transform.LookAt(new Vector3(0,.25f,0));camera.fieldOfView=50;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.23f,.25f);camera.aspect=3;
            camera.orthographic=true;camera.orthographicSize=.78f;
            var target=new RenderTexture(1800,600,24);camera.targetTexture=target;camera.Render();camera.Render();camera.Render();
            var prev=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1800,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1800,600),0,0);image.Apply();
            File.WriteAllBytes("Logs/fly-character-variants.png",image.EncodeToPNG());
            foreach(var model in models)model.transform.Rotate(0,140,0);
            camera.Render();camera.Render();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1800,600),0,0);image.Apply();
            File.WriteAllBytes("Logs/fly-character-side.png",image.EncodeToPNG());
            RenderTexture.active=prev;camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(image);
        }
    }
}
