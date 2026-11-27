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
                        if(bounds.size.x>2.24f || bounds.size.z>2.24f)throw new Exception("Activity exceeds corner footprint: "+slot.ItemId+" "+bounds.size);
                        if(slot.InteractionRadius<RoomArt.FurnitureInteractionRadius)throw new Exception("Furniture approach distance mismatch: "+slot.ItemId);
                        var solid=room.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c=>c.name.StartsWith("Furniture_") && Vector2.Distance(new Vector2(c.transform.position.x,c.transform.position.z),new Vector2(slot.transform.position.x,slot.transform.position.z))<.1f);
                        if(solid==null)throw new Exception("Missing furniture collision: "+slot.ItemId);
                        var solidSize=Vector3.Scale(solid.size,solid.transform.lossyScale);
                        if(solidSize.x+.001f<bounds.size.x || solidSize.z+.001f<bounds.size.z || solidSize.y+.001f<bounds.size.y)throw new Exception("Furniture collision too small: "+slot.ItemId);
                        var obstacle=solid.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                        if(obstacle==null || !obstacle.carving)throw new Exception("Missing enlarged navigation obstacle: "+slot.ItemId);
                    }
                    foreach(var r in art.GetComponentsInChildren<Renderer>(true))
                        foreach(var m in r.sharedMaterials)
                            if(m==null || m.shader.name!="Universal Render Pipeline/Lit")throw new Exception("Invalid art material: "+r.name);
                    int count=art.childCount;
                    RoomArt.Apply(room);
                    if(count!=art.childCount)throw new Exception("Repeated art installation duplicated props");
                    report.Add(scene+" / "+room.Stage+": "+slots.Length+" items; collider count unchanged; enlarged collision/carving/approach valid; materials valid; renderers="+art.GetComponentsInChildren<Renderer>(true).Length);
                }
                var primary=rooms.First(r=>r.gameObject.activeInHierarchy);
                Capture(primary,scene);
                foreach(var id in new[]{"childhood_walk","childhood_soccer","childhood_birthday","adolescence_stars","adolescence_performance","youngadult_park"})
                    if(primary.ItemSlots.Any(s=>s.ItemId==id))CaptureActivity(primary,id);
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
        static void CaptureActivity(RoomController room,string id)
        {
            var prop=room.transform.Find(RoomArt.RootName).Find(id+"(Clone)");
            var camera=new GameObject("Activity review camera").AddComponent<Camera>();
            camera.transform.position=prop.position+prop.rotation*new Vector3(2.25f,1.8f,-3.4f);
            camera.transform.LookAt(prop.position+Vector3.up*.9f);
            camera.fieldOfView=43;camera.nearClipPlane=.03f;camera.aspect=16f/9f;
            var target=new RenderTexture(1600,900,24);camera.targetTexture=target;
            camera.Render();camera.Render();camera.Render();
            var prev=RenderTexture.active;RenderTexture.active=target;
            var picture=new Texture2D(1600,900,TextureFormat.RGB24,false);picture.ReadPixels(new Rect(0,0,1600,900),0,0);picture.Apply();
            File.WriteAllBytes("Logs/RoomArt/"+id+".png",picture.EncodeToPNG());
            RenderTexture.active=prev;camera.targetTexture=null;
            Object.DestroyImmediate(picture);Object.DestroyImmediate(target);Object.DestroyImmediate(camera.gameObject);
        }
    }
}
