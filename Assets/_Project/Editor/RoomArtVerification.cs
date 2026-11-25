using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FruitFlyParenting.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Editor
{
    public static class RoomArtVerification
    {
        public static void VerifyAndBuild()
        {
            Verify();
            DesktopBuildSetup.BuildFlatHud();
        }
        public static void PrepareVerifyAndBuild()
        {
            RoomArtSetup.Build();
            VerifyAndBuild();
        }
        public static void Verify()
        {
            ShaderUtil.allowAsyncCompilation=false;
            Directory.CreateDirectory("Logs/RoomArt");
            var report = new List<string>();
            foreach (string scene in new[]{"01_Infant","03_Childhood","04_Adolescence","05_YoungAdult"})
            {
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+scene+".unity");
                var rooms=Object.FindObjectsByType<RoomController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                foreach(var room in rooms)
                {
                    int colliders=room.GetComponentsInChildren<Collider>(true).Length;
                    var slots=room.GetComponentsInChildren<ItemSlot>(true);
                    RoomArt.Apply(room);
                    var art=room.transform.Find(RoomArt.RootName);
                    if(art==null) throw new Exception("Missing room art: "+room.name);
                    if(colliders!=room.GetComponentsInChildren<Collider>(true).Length) throw new Exception("Art changed collision");
                    foreach(var slot in slots)
                    {
                        var prop=art.Find(slot.ItemId+"(Clone)");
                        if(prop==null) throw new Exception("Missing item art: "+slot.ItemId);
                        var renderers=prop.GetComponentsInChildren<Renderer>();
                        var bounds=renderers[0].bounds;
                        foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                        if(bounds.size.x>1.001f || bounds.size.z>1.001f)throw new Exception("Furniture exceeds collider footprint: "+slot.ItemId+" "+bounds.size);
                    }
                    foreach(var r in art.GetComponentsInChildren<Renderer>(true))
                        foreach(var m in r.sharedMaterials)
                            if(m==null || m.shader.name!="Universal Render Pipeline/Lit")throw new Exception("Invalid art material: "+r.name);
                    int count=art.childCount;
                    RoomArt.Apply(room);
                    if(count!=art.childCount)throw new Exception("Repeated art installation duplicated props");
                    report.Add(scene+" / "+room.Stage+": "+slots.Length+" items; collision unchanged; materials valid; footprint valid; renderers="+art.GetComponentsInChildren<Renderer>(true).Length);
                }
                var primary=rooms.First(r=>r.gameObject.activeInHierarchy);
                Capture(primary,scene);
            }
            File.WriteAllLines("Logs/RoomArt/validation.txt",report);
            Debug.Log("ROOM_ART_VERIFIED\n"+string.Join("\n",report));
        }
        static void Capture(RoomController room,string name)
        {
            foreach(var camera in Object.FindObjectsByType<Camera>()) camera.enabled=false;
            var go=new GameObject("Art review camera");var cam=go.AddComponent<Camera>();
            cam.transform.position=room.transform.TransformPoint(new Vector3(12,3.6f,2));
            cam.transform.LookAt(room.transform.TransformPoint(new Vector3(12,1.5f,16)));
            cam.fieldOfView=83;cam.nearClipPlane=.05f;cam.farClipPlane=100;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.7f,.8f,.85f);
            var target=new RenderTexture(1600,900,24);cam.targetTexture=target;
            cam.Render(); cam.Render(); cam.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            File.WriteAllBytes("Logs/RoomArt/"+name+".png",image.EncodeToPNG());
            RenderTexture.active=previous;cam.targetTexture=null;
            Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(go);
        }
    }
}
