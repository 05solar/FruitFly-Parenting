using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using FruitFlyParenting.Corridor;

namespace FruitFlyParenting.Room
{
    // Match enlarged furniture to collision, approach distance and carved navigation.
    public static class RoomArt
    {
        public const string RootName = "Room Art";
        public const float FurnitureScale = 1.5f;
        public const float FurnitureInteractionRadius = 1.65f;
        static readonly HashSet<string> OldProps = new HashSet<string>
        {
            "Low Cushion", "Play Ball", "Sensory Block", "Cushion", "Pillow", "Picture Book",
            "Screen", "Bright Page", "Crawling Arch", "Snack Box", "Playground Floor",
            "Bookshelf Mural", "Painted Book", "Sky Mural", "Painted Cloud", "Sports Ground",
            "Painted School Locker", "Locker Handle", "Night Window", "Track Line",
            "Apartment Rug", "Office Flooring", "Gym Mat", "Office Window", "Painted City Building",
            "Apartment Picture", "Pinned Personal Note"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var room in root.GetComponentsInChildren<RoomController>(true)) Apply(room);
        }

        public static void Apply(RoomController room)
        {
            if (room.Stage < LifeStage.Infant || room.Stage > LifeStage.YoungAdult ||
                room.transform.Find(RootName) != null) return;
            var shell = Resources.Load<GameObject>("RoomArt/Decor/" + room.Stage);
            if (shell == null) return;
            var art = Object.Instantiate(shell, room.transform, false);
            art.name = RootName;
            // Soft ambient daylight also works when Quest strips secondary point lights.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f, .5f, .49f);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.intensity = .65f;
                    light.color = new Color(1f, .97f, .91f);
                }
            foreach (Transform child in room.transform)
            {
                if (child.name == "Floor") child.GetComponent<Renderer>().sharedMaterial = Mat("Floor");
                if (child.name.StartsWith("Wall_"))
                {
                    var wall = child.GetComponent<Renderer>();
                    wall.sharedMaterial = Mat("Plaster");
                    wall.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                if (OldProps.Contains(child.name)) Hide(child);
            }
            room.RefreshItemSlots();
            foreach (var slot in room.ItemSlots)
            {
                var preset = Resources.Load<GameObject>("RoomArt/Items/" + slot.ItemId);
                if (preset == null) continue;
                var prop = Object.Instantiate(preset, art.transform, false);
                prop.transform.localScale *= FurnitureScale;
                var local = room.transform.InverseTransformPoint(slot.transform.position);
                prop.transform.localPosition = new Vector3(local.x, 0, local.z);
                // Face the centre aisle using quarter turns, keeping each corner's footprint compact.
                var outward=new Vector3(local.x-12f,0,local.z-11f);
                if(outward.sqrMagnitude>.01f)
                    prop.transform.localRotation=Quaternion.LookRotation(Mathf.Abs(outward.x)>Mathf.Abs(outward.z) ? new Vector3(Mathf.Sign(outward.x),0,0):new Vector3(0,0,Mathf.Sign(outward.z)));
                slot.SetInteractionRadius(FurnitureInteractionRadius);
                var renderers=prop.GetComponentsInChildren<Renderer>();
                var artBounds=renderers[0].bounds;
                foreach(var renderer in renderers)artBounds.Encapsulate(renderer.bounds);
                // Furniture names in the infant scene use the original grid IDs.
                var furnitureRoot = room.transform.Find("ItemSlots");
                if (furnitureRoot == null) continue;
                foreach (Transform furniture in furnitureRoot)
                    if (furniture.name.StartsWith("Furniture_") &&
                        Vector2.Distance(new Vector2(furniture.localPosition.x, furniture.localPosition.z),
                            new Vector2(local.x, local.z)) < .1f)
                    {
                        Hide(furniture);
                        var collider = furniture.GetComponent<BoxCollider>();
                        if (collider == null) continue;
                        // Fit the downloaded composition, including its backdrop and trees.
                        var worldSize=Vector3.Max(artBounds.size,new Vector3(FurnitureScale,2.7f,FurnitureScale));
                        var lossy=furniture.lossyScale;
                        collider.size=new Vector3(worldSize.x/Mathf.Abs(lossy.x),worldSize.y/Mathf.Abs(lossy.y),worldSize.z/Mathf.Abs(lossy.z));
                        var centre=artBounds.center;centre.y=room.transform.position.y+worldSize.y*.5f;
                        collider.center=furniture.InverseTransformPoint(centre);
                        slot.SetInteractionRadius(Mathf.Max(FurnitureInteractionRadius,(Mathf.Max(worldSize.x,worldSize.z)*.5f+.45f)/.8f));
                        var obstacle = furniture.GetComponent<NavMeshObstacle>();
                        if (obstacle == null) obstacle = furniture.gameObject.AddComponent<NavMeshObstacle>();
                        obstacle.shape = NavMeshObstacleShape.Box;
                        obstacle.center = collider.center;
                        obstacle.size = collider.size;
                        obstacle.carving = true;
                        obstacle.carveOnlyStationary = false;
                    }
            }
        }

        static Material Mat(string name) => Resources.Load<Material>("RoomArt/Materials/" + name);
        static void Hide(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
    }
}
