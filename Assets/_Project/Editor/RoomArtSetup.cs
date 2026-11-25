using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FruitFlyParenting.Room;
using FruitFlyParenting.Corridor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static class RoomArtSetup
    {
        const string Source = "Assets/_Project/Art/ThirdParty/KenneyFurniture/";
        const string Root = "Assets/_Project/Resources/RoomArt/";
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("Fruit Fly Parenting/Art/Prepare Room Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "Materials");
            Directory.CreateDirectory(Root + "Items");
            Directory.CreateDirectory(Root + "Decor");
            AssetDatabase.Refresh();
            Materials.Clear();
            var cue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/ApproachShadow.mat");
            if (cue != null)
            {
                cue.shader = Shader.Find("FruitFlyParenting/Soft Ground Cue");
                cue.renderQueue = 3000;
                EditorUtility.SetDirty(cue);
            }
            // Named FBX surfaces share URP materials across every furniture model.
            var colors = new Dictionary<string, Color>
            {
                {"fur", Hex("B88559")}, {"wood", Hex("CDA775")}, {"woodDark", Hex("947654")},
                {"metalDark", Hex("334852")}, {"metalMedium", Hex("657D80")}, {"metal", Hex("BCCACE")},
                {"carpetWhite", Hex("F5EEDC")}, {"carpet", Hex("D48C78")}, {"carpetDarker", Hex("9C625A")},
                {"plant", Hex("719B73")}, {"lamp", Hex("FFF1BD")}, {"_defaultMat", Color.white},
                {"Plaster", Hex("F2E9D7")}, {"Trim", Hex("FFF4DC")}, {"Sage", Hex("A7BCA2")},
                {"Blue", Hex("88A7BB")}, {"Slate", Hex("6B858E")}, {"Ochre", Hex("CEA15F")},
                {"Sky", Hex("C0DCE3")}, {"Cloud", Hex("F5F4DF")}, {"Rug", Hex("C4BA96")}
            };
            foreach (var pair in colors) MakeMaterial(pair.Key, pair.Value);
            Materials["Sky"].EnableKeyword("_EMISSION");
            Materials["Sky"].SetColor("_EmissionColor", Hex("C0DCE3") * .3f);
            var floor = MakeMaterial("Floor", new Color(1.25f,1.18f,1.06f));
            const string wood = "Assets/_Project/Art/ThirdParty/PolyHaven/";
            var normal = (TextureImporter)AssetImporter.GetAtPath(wood + "wood_floor_nor_gl_1k.jpg");
            normal.textureType = TextureImporterType.NormalMap;
            normal.maxTextureSize = 1024; normal.SaveAndReimport();
            floor.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(wood + "wood_floor_diff_1k.jpg"));
            floor.SetTextureScale("_BaseMap", new Vector2(10, 9));
            floor.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(wood + "wood_floor_nor_gl_1k.jpg"));
            floor.SetFloat("_BumpScale", .35f); floor.EnableKeyword("_NORMALMAP");
            foreach (string path in Directory.GetFiles(Source, "*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
                importer.isReadable = false;
                importer.SaveAndReimport();
                foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    var name = material.name;
                    if (!Materials.TryGetValue(name, out var replacement))
                        replacement = MakeMaterial(name, material.HasProperty("_Color") ? material.color : Color.white);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name), replacement);
                }
                importer.SaveAndReimport();
            }
            foreach (var stage in new[]{LifeStage.Infant, LifeStage.Childhood, LifeStage.Adolescence, LifeStage.YoungAdult})
                BuildDecor(stage);
            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[]{"Assets/_Project/ScriptableObjects/Items"}))
            {
                var item = AssetDatabase.LoadAssetAtPath<FruitFlyParenting.Data.ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && !string.IsNullOrEmpty(item.itemId)) BuildItem(item.itemId);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("ROOM_ART_READY: licensed furniture, four room interiors and 47 interaction presets generated.");
        }

        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value, out var color); return color; }
        static Material MakeMaterial(string name, Color color)
        {
            var path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .15f);
            Materials[name] = material; EditorUtility.SetDirty(material); return material;
        }

        static void BuildItem(string id)
        {
            var root = new GameObject(id);
            var key = id.Substring(id.IndexOf('_') + 1);
            var model = "desk";
            switch (key)
            {
                case "bed": case "dayoff": case "quit": model = "bedSingle"; break;
                case "hug": case "family": case "parents": case "counsel": model = "loungeSofa"; break;
                case "book": case "library": model = "bookcaseOpenLow"; break;
                case "blocks": case "crawl": case "hideout": model = "cardboardBoxOpen"; break;
                case "ball": case "soccer": case "stadium": case "training": case "selection": model = "benchCushion"; break;
                case "snack": case "birthday": case "dinner": model = "tableRound"; break;
                case "walk": case "park": case "stars": model = "loungeChair"; break;
                case "independence": model = "bedDouble"; break;
                case "theft": case "gifted": case "camp": model = "bookcaseOpen"; break;
                case "performance": case "star": model = "cabinetBed"; break;
            }
            var main = Model(model, root.transform, Vector3.zero, .96f);
            float top = Bounds(main).max.y;
            if (model == "desk")
            {
                bool screen = key == "tablet" || key == "nightgame" || key == "arcade" || key == "gambling" ||
                    key == "investment" || key == "startup" || key == "interview" || key == "studio";
                Model(screen ? "laptop" : "books", root.transform, new Vector3(0,top,0), .45f);
                Model("plantSmall1",root.transform,new Vector3(.3f,top,.1f),.18f);
            }
            else if (model == "tableRound")
            {
                Model(id.StartsWith("infant") ? "bear" : "plantSmall2",root.transform,new Vector3(0,top,0),.24f);
            }
            else if (model == "cardboardBoxOpen") Model("bear",root.transform,new Vector3(0,.2f,0),.42f);
            else if (key == "performance" || key == "star") Model("lampRoundTable",root.transform,new Vector3(0,top,0),.4f);
            else if (key == "ball" || key == "soccer" || key == "stadium" || key == "training" || key == "selection")
            {
                var ball = Shape(root.transform,"Ball",PrimitiveType.Sphere,new Vector3(.1f,top+.18f,0),Vector3.one*.36f,"Ochre");
                Shape(ball.transform,"Stripe",PrimitiveType.Cylinder,Vector3.zero,new Vector3(1.01f,.14f,1.01f),"Trim");
            }
            if (id.StartsWith("infant") && model == "bookcaseOpenLow") Model("bear",root.transform,new Vector3(.22f,top,0),.3f);
            // Keep all solid-looking geometry inside the existing one-metre obstacle footprint.
            var bounds = Bounds(root);
            var scale = Mathf.Min(1f, .98f / Mathf.Max(bounds.size.x,bounds.size.z), 1.75f/bounds.size.y);
            root.transform.localScale = Vector3.one * scale;
            Save(root,"Items/"+id);
        }

        static void BuildDecor(LifeStage stage)
        {
            var root = new GameObject(stage.ToString());
            string accent = stage == LifeStage.Infant ? "Sage" : stage == LifeStage.Childhood ? "Blue" : stage == LifeStage.Adolescence ? "Slate" : "Ochre";
            // Narrow trim hugs the wall; all freestanding gameplay obstacles remain the originals.
            foreach (float z in new[]{.025f,21.975f})
            {
                Box(root.transform,"Lower wall",new Vector3(12,.73f,z),new Vector3(24,1.46f,.035f),accent);
                Box(root.transform,"Chair rail",new Vector3(12,1.48f,z),new Vector3(24,.09f,.07f),"Trim");
                Box(root.transform,"Skirting",new Vector3(12,.1f,z),new Vector3(24,.2f,.07f),"Trim");
                Box(root.transform,"Cornice",new Vector3(12,5.85f,z),new Vector3(24,.17f,.12f),"Trim");
                for (int x=1;x<24;x+=2) Box(root.transform,"Panel seam",new Vector3(x,.72f,z < 1 ? .05f : 21.95f),new Vector3(.045f,1.32f,.025f),"Trim");
            }
            foreach (float x in new[]{.025f,23.975f}) foreach (float z in new[]{4.7f,17.3f})
            {
                Box(root.transform,"Side panel",new Vector3(x,.73f,z),new Vector3(.035f,1.46f,9.4f),accent);
                Box(root.transform,"Side rail",new Vector3(x,1.48f,z),new Vector3(.07f,.09f,9.4f),"Trim");
                Box(root.transform,"Side skirting",new Vector3(x,.1f,z),new Vector3(.07f,.2f,9.4f),"Trim");
            }
            foreach(float x in new[]{0f,24f})
                Box(root.transform,"Door lintel",new Vector3(x,4.75f,11),new Vector3(.25f,2.5f,3),"Plaster");
            foreach (float x in new[]{5f,12f,19f}) Window(root.transform,x,accent,stage);
            for(int i=0;i<3;i++)
            {
                float x=7+i*5;
                Box(root.transform,"Picture frame",new Vector3(x,3,.075f),new Vector3(2.2f,2.6f,.12f),"wood");
                Box(root.transform,"Picture paper",new Vector3(x,3,.15f),new Vector3(1.94f,2.34f,.035f),"Trim");
                Shape(root.transform,"Graphic circle",PrimitiveType.Sphere,new Vector3(x-.28f,3.4f,.19f),new Vector3(.7f,.7f,.025f),"Ochre");
                Box(root.transform,"Graphic landscape",new Vector3(x+.12f,2.75f,.18f),new Vector3(1.55f,.6f,.025f),accent);
            }
            // Flat rugs and ceiling fittings decorate open space without blocking navigation.
            var rug = Model("rugRectangle",root.transform,new Vector3(12,.012f,11),5.6f);
            foreach (var renderer in rug.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_=>Materials[accent]).ToArray();
            var rug2 = Model("rugRound",root.transform,new Vector3(6,.012f,8),2.5f);
            foreach (var renderer in rug2.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_=>Materials["Rug"]).ToArray();
            foreach(float x in new[]{6f,18f})
            {
                Box(root.transform,"Pendant cord",new Vector3(x,5.35f,11),new Vector3(.025f,1.2f,.025f),"metalDark");
                Shape(root.transform,"Pendant shade",PrimitiveType.Cylinder,new Vector3(x,4.8f,11),new Vector3(.9f,.18f,.9f),"Trim");
                Shape(root.transform,"Pendant diffuser",PrimitiveType.Cylinder,new Vector3(x,4.61f,11),new Vector3(.82f,.01f,.82f),"lamp");
            }
            Box(root.transform,"Ceiling",new Vector3(12,6.05f,11),new Vector3(24,.1f,22),"Plaster");
            CombineBoxes(root);
            Save(root,"Decor/"+stage);
        }

        static void Window(Transform parent,float x,string accent,LifeStage stage)
        {
            Box(parent,"Window casing",new Vector3(x,3.5f,21.91f),new Vector3(4.7f,3.5f,.14f),"wood");
            Box(parent,"Daylight",new Vector3(x,3.5f,21.82f),new Vector3(4.4f,3.2f,.025f),"Sky");
            // Layered exterior silhouettes give the flat window depth without extra lights.
            Shape(parent,"Garden canopy",PrimitiveType.Sphere,new Vector3(x-.9f,2.65f,21.78f),new Vector3(2.1f,1.3f,.025f),"Sage");
            Shape(parent,"Garden canopy",PrimitiveType.Sphere,new Vector3(x+1.1f,2.6f,21.76f),new Vector3(2.1f,1.2f,.025f),"plant");
            Shape(parent,"Cloud",PrimitiveType.Sphere,new Vector3(x+.7f,4.35f,21.77f),new Vector3(1.3f,.35f,.025f),"Cloud");
            Box(parent,"Window mullion",new Vector3(x,3.5f,21.7f),new Vector3(.1f,3.2f,.1f),"Trim");
            Box(parent,"Window transom",new Vector3(x,3.4f,21.7f),new Vector3(4.4f,.1f,.1f),"Trim");
            Box(parent,"Window sill",new Vector3(x,1.82f,21.78f),new Vector3(4.9f,.12f,.38f),"Trim");
            Model("plantSmall2",parent,new Vector3(x+1.5f,1.9f,21.73f),.3f);
            foreach (float dx in new[]{-2.55f,2.55f})
                for(int i=0;i<4;i++) Shape(parent,"Curtain pleat",PrimitiveType.Cylinder,new Vector3(x+dx+(i-1.5f)*.13f,3.45f,21.65f),new Vector3(.19f,1.75f,.14f),accent);
        }

        static GameObject Model(string name,Transform parent,Vector3 position,float width)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source+name+".fbx");
            if(source==null) throw new InvalidOperationException("Missing furniture: "+name);
            var wrapper=new GameObject(name); wrapper.transform.SetParent(parent,false);
            var model=Object.Instantiate(source,wrapper.transform,false);
            var bounds=Bounds(wrapper); float scale=width/Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.localScale*=scale;
            bounds=Bounds(wrapper);
            model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-wrapper.transform.position;
            wrapper.transform.localPosition=position;
            return wrapper;
        }
        static Bounds Bounds(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>(); var bounds=renderers[0].bounds;
            foreach(var r in renderers) bounds.Encapsulate(r.bounds); return bounds;
        }
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,string material)
            => Shape(parent,name,PrimitiveType.Cube,position,scale,material);
        static GameObject Shape(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,string material)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial=Materials[material]; return go;
        }
        static void CombineBoxes(GameObject root)
        {
            // Collapse architectural primitives to one mesh per shared material for PC/VR draw cost.
            var meshes=root.GetComponentsInChildren<MeshFilter>().Where(m=>m.transform.parent==root.transform).ToArray();
            foreach(var group in meshes.GroupBy(m=>m.GetComponent<Renderer>().sharedMaterial))
            {
                var mesh=new Mesh {name=root.name+"_"+group.Key.name, indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(group.Select(m=>new CombineInstance {mesh=m.sharedMesh,transform=m.transform.localToWorldMatrix}).ToArray());
                string path=Root+"Decor/"+mesh.name+".asset";
                var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old==null) AssetDatabase.CreateAsset(mesh,path); else {EditorUtility.CopySerialized(mesh,old); Object.DestroyImmediate(mesh);mesh=old;}
                var go=new GameObject(group.Key.name);go.transform.SetParent(root.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            foreach(var m in meshes)Object.DestroyImmediate(m.gameObject);
        }
        static void Save(GameObject root,string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root,Root+name+".prefab");Object.DestroyImmediate(root);
        }
    }
}
