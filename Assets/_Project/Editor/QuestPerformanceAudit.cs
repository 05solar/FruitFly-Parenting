using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitFlyParenting.Editor
{
    public static class QuestPerformanceAudit
    {
        [Serializable] public sealed class Row
        {
            public string asset, kind;
            public long triangles;
            public int renderers, materialSlots, uniqueMaterials, staticEligibleRenderers, enabledLights, itemSlots;
            public long textureSourceBytes;
        }
        [Serializable] sealed class Evidence
        {
            public string measurement = "Authored meshes and material submissions, including inactive content. NOT measured GPU draw calls or HMD frame times.";
            public Row[] before, after;
        }
        [MenuItem("Fruit Fly Parenting/Audit 15 Quest Geometry")]
        public static void Run()
        {
            var before = new List<Row>(); var after = new List<Row>();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Rooms" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        before.Add(Measure(path, "prefab", new[] { prefab }));
                        QuestSceneProcessor.Apply(prefab.scene);
                        after.Add(Measure(path, "prefab", new[] { prefab }));
                    }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                }
                foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
                {
                    var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                    before.Add(Measure(entry.path, "scene", scene.GetRootGameObjects()));
                    var rooms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FruitFlyParenting.Room.RoomController>(true)).ToArray();
                    foreach (var room in rooms) before.Add(Measure(entry.path + "#" + room.name, "room", new[] { room.gameObject }));
                    QuestSceneProcessor.Apply(scene);
                    after.Add(Measure(entry.path, "scene", scene.GetRootGameObjects()));
                    foreach (var room in rooms) after.Add(Measure(entry.path + "#" + room.name, "room", new[] { room.gameObject }));
                }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/step15-geometry.json", JsonUtility.ToJson(new Evidence { before = before.ToArray(), after = after.ToArray() }, true));
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
        static Row Measure(string path, string kind, GameObject[] roots)
        {
            var renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
            var row = new Row { asset = path, kind = kind, renderers = renderers.Length,
                uniqueMaterials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count(),
                enabledLights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).Count(l => l.enabled),
                itemSlots = roots.Sum(r => r.GetComponentsInChildren<FruitFlyParenting.Room.ItemSlot>(true).Length) };
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : (filter != null ? filter.sharedMesh : null);
                if (mesh == null) continue;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles) row.triangles += (long)mesh.GetIndexCount(i) / 3;
                row.materialSlots += renderer.sharedMaterials.Length;
                if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0) row.staticEligibleRenderers++;
            }
            row.textureSourceBytes = AssetDatabase.GetDependencies(path, true)
                .Select(p => AssetDatabase.LoadAssetAtPath<Texture>(p)).Where(t => t != null).Distinct()
                .Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            return row;
        }
    }
}
