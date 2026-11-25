using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitFlyParenting.Connectome
{
    [Serializable] public sealed class GraphNode
    {
        public string id, label, side;
        public int stage;
        public string[] members;
    }
    [Serializable] public sealed class GraphRegion { public string name; public int synapses; }
    [Serializable] public sealed class GraphEdge
    {
        public string source, target;
        public int synapses;
        public float visualWeight;
        public GraphRegion[] regions;
    }
    [Serializable] public sealed class GraphPath { public int bottleneck; public string[] ids; }
    [Serializable] public sealed class GraphSource { public string name, url, sha256; }
    [Serializable] public sealed class ConnectomeGraph
    {
        public int schemaVersion;
        public string dataset, license, attribution, mode;
        public GraphSource[] sources;
        public GraphNode[] nodes;
        public GraphEdge[] edges;
        public GraphPath[] paths;

        public static ConnectomeGraph Parse(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 65536)
                throw new ArgumentException("Only a small preprocessed graph is allowed.");
            var graph = JsonUtility.FromJson<ConnectomeGraph>(json);
            graph.Validate();
            return graph;
        }

        public void Validate()
        {
            if (schemaVersion != 1 || dataset != "FlyWire FAFB v783" || license != "CC BY-NC 4.0"
                || string.IsNullOrEmpty(attribution) || sources == null || sources.Length != 2
                || (mode != "neurons" && mode != "motif") || paths == null)
                throw new ArgumentException("Graph provenance/schema is missing or unsupported.");
            var names = new[] { "classification.csv.gz", "connections.csv.gz" };
            var hashes = new[] { "e946b552f4056dfc977707be0674609832c3f64332a22d69dc0d9615e7aae663",
                "d49dd692e59e153aa3c83f5257bfc0eff51247b86d7bb183386c6d1622c70fc9" };
            for (int i = 0; i < 2; i++)
                if (sources[i] == null || sources[i].name != names[i] || sources[i].sha256 != hashes[i]
                    || sources[i].url != "https://storage.googleapis.com/flywire-data/codex/data/fafb/783/" + names[i])
                    throw new ArgumentException("Unpinned source provenance.");
            if (nodes == null || edges == null || nodes.Length == 0 || nodes.Length > 30
                || edges.Length == 0 || edges.Length > 24)
                throw new ArgumentException("Graph exceeds the experimental budget.");
            var byId = new Dictionary<string, GraphNode>();
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.id) || string.IsNullOrEmpty(node.label)
                    || node.stage < 0 || node.stage > 4 || node.members == null || node.members.Length == 0
                    || !byId.TryAdd(node.id, node)) throw new ArgumentException("Invalid/duplicate node.");
            }
            var pairs = new Dictionary<string, GraphEdge>();
            int maximum = 0;
            foreach (var edge in edges)
            {
                if (edge == null || edge.source == null || edge.target == null
                    || !byId.ContainsKey(edge.source) || !byId.ContainsKey(edge.target)
                    || byId[edge.target].stage != byId[edge.source].stage + 1 || edge.synapses < 5
                    || float.IsNaN(edge.visualWeight) || edge.visualWeight <= 0 || edge.visualWeight > 1
                    || edge.regions == null || edge.regions.Length == 0
                    || !pairs.TryAdd(edge.source + ":" + edge.target, edge))
                    throw new ArgumentException("Invalid directed edge.");
                long sum = 0;
                foreach (var region in edge.regions)
                {
                    if (region == null || string.IsNullOrEmpty(region.name) || region.synapses < 0)
                        throw new ArgumentException("Invalid region.");
                    sum += region.synapses;
                }
                if (sum != edge.synapses) throw new ArgumentException("Regional synapse count mismatch.");
                maximum = Math.Max(maximum, edge.synapses);
            }
            foreach (var edge in edges)
                if (Math.Abs(edge.visualWeight - Math.Log(1 + edge.synapses) / Math.Log(1 + maximum)) > .000002)
                    throw new ArgumentException("Visual weight does not match the documented transform.");
            if (mode == "neurons")
            {
                if (paths.Length != 6) throw new ArgumentException("Six selected paths are required.");
                foreach (var path in paths)
                {
                    if (path.ids == null || path.ids.Length != 5) throw new ArgumentException("Incomplete path.");
                    int bottleneck = int.MaxValue;
                    for (int i = 0; i < 4; i++)
                    {
                        if (!pairs.TryGetValue(path.ids[i] + ":" + path.ids[i + 1], out var edge))
                            throw new ArgumentException("Path uses a nonexistent edge.");
                        bottleneck = Math.Min(bottleneck, edge.synapses);
                    }
                    if (bottleneck != path.bottleneck) throw new ArgumentException("Path bottleneck mismatch.");
                }
            }
            else if (nodes.Length != 5 || edges.Length != 4 || paths.Length != 0)
                throw new ArgumentException("Invalid memory motif.");
        }
    }
}
