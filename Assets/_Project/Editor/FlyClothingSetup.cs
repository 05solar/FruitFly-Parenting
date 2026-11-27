using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FruitFlyParenting.Corridor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static class FlyClothingSetup
    {
        const string Source="Assets/_Project/Art/ThirdParty/FlyClothing/";
        const string Materials="Assets/_Project/Resources/FlyCharacter/Materials/";
        [Serializable] class Catalog { public Model[] models; }
        [Serializable] class Model { public string name; public Surface[] materials; }
        [Serializable] class Surface { public string name; public float[] color; public string texture; }
        static readonly Dictionary<string,Material> palette=new Dictionary<string,Material>();

        public static void Prepare()
        {
            AssetDatabase.Refresh(); palette.Clear();
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Source+"materials.json"));
            foreach(var model in catalog.models)
            {
                foreach(var surface in model.materials)
                {
                    var rgba=surface.color;
                    var color=new Color(rgba[0],rgba[1],rgba[2],rgba[3]).gamma;
                    var mat=Material(surface.name,color);
                    if(!string.IsNullOrEmpty(surface.texture))mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+surface.texture));
                }
                string path=Source+model.name+".obj";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.importNormals=ModelImporterNormals.Import;importer.addCollider=false;importer.SaveAndReimport();
                foreach(var surface in model.materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),surface.name),palette[surface.name]);
                importer.SaveAndReimport();
            }
            Material("ClothBlue",Hex("619EC0"));Material("ClothCream",Hex("F6DFC0"));
            Material("ClothMint",Hex("82B5A4"));Material("ClothNavy",Hex("384A68"));
            Material("ClothCoral",Hex("DB876D"));Material("ClothGold",Hex("E7BD64"));
            Material("FittedFabric",Hex("619EC0"));Material("FittedTrim",Hex("F6DFC0"));
            var fitted=(ModelImporter)AssetImporter.GetAtPath(Source+"FittedVest.obj");
            fitted.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            fitted.importNormals=ModelImporterNormals.Import;fitted.addCollider=false;fitted.SaveAndReimport();
            foreach(var name in new[]{"FittedFabric","FittedTrim"})
                fitted.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),palette[name]);
            fitted.SaveAndReimport();
        }
        static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
        static Material Material(string name,Color color)
        {
            string path=Materials+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Metallic",0);mat.SetFloat("_Cull",0);
            palette[name]=mat;EditorUtility.SetDirty(mat);return mat;
        }
        static Transform Group(string name,Transform parent)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        static Bounds Bounds(GameObject go)
        {var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        static GameObject Wear(string asset,Transform parent,Vector3 center,Vector3 size,Vector3 rotation,string tint=null)
        {
            var wrapper=Group("Asset "+asset,parent);
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source+asset+".obj"),wrapper,false);
            model.transform.localRotation=asset=="Shirt" || asset=="Jacket" ? Quaternion.LookRotation(Vector3.up,Vector3.forward):Quaternion.Euler(rotation);
            var bounds=Bounds(model);
            wrapper.localScale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            wrapper.localPosition=center;
            if(tint!=null)foreach(var r in model.GetComponentsInChildren<Renderer>())
                r.sharedMaterials=r.sharedMaterials.Select(_=>palette[tint]).ToArray();
            return wrapper.gameObject;
        }
        static void FittedGarment(Transform parent,Transform body,string fabric,string trim)
        {
            var garment=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source+"FittedVest.obj"),parent,false);
            garment.name="Fitted sleeveless vest";
            garment.transform.localPosition=body.localPosition;
            garment.transform.localRotation=body.localRotation;
            garment.transform.localScale=body.localScale;
            foreach(var renderer in garment.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>palette[m.name=="FittedTrim" ? trim:fabric]).ToArray();
        }
        public static void Dress(LifeStage stage,Transform parent,float length,float headY,float headZ)
        {
            float growth=length/.75f;
            var body=parent.Find("Downloaded Fly");
            var clothes=Group(stage>=LifeStage.Adolescence ? "Uniform":"Clothes",parent);
            string tone=stage==LifeStage.Infant || stage==LifeStage.MiddleAge ? "ClothCream":
                stage==LifeStage.Childhood ? "ClothBlue":stage==LifeStage.Adolescence ? "ClothNavy":"ClothMint";
            FittedGarment(clothes,body,tone,stage==LifeStage.Infant ? "ClothCoral":"ClothCream");
            if(stage==LifeStage.Childhood)
                Wear("Cap",clothes,new Vector3(0,headY+.027f,headZ-.025f),new Vector3(.25f,.11f,.29f),new Vector3(-5,0,0),"ClothBlue");
            if(stage>=LifeStage.YoungAdult)
                Wear("Backpack",clothes,new Vector3(.175f*growth,.17f*growth,-.23f*growth),new Vector3(.08f,.14f,.11f)*growth,new Vector3(0,90,0));
            if(stage==LifeStage.Adolescence)
            {
                var alternate=Group("Alternative",parent);
                FittedGarment(alternate,body,"ClothCoral","ClothNavy");
                Wear("Cap",alternate,new Vector3(0,headY+.03f,headZ-.065f),new Vector3(.25f,.11f,.29f),new Vector3(5,180,0),"ClothNavy");
                Wear("Backpack",alternate,new Vector3(.175f*growth,.17f*growth,-.23f*growth),new Vector3(.08f,.14f,.11f)*growth,new Vector3(0,90,0));
                alternate.gameObject.SetActive(false);
            }
        }
    }
}
