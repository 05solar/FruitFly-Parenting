using System;
using System.IO;
using System.Linq;
using FruitFlyParenting.Connectome.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FruitFlyParenting.Connectome.Tests
{
    public sealed class ConnectomeGraphTests
    {
        static string Neurons => File.ReadAllText(ConnectomeSetup.Root + "Data/connectome-v783.json");
        [Test] public void RealDataHasSixCompleteDirectedPathsAndSmallBudget()
        {
            var graph = ConnectomeGraph.Parse(Neurons);
            Assert.That(graph.nodes.Length, Is.EqualTo(17));
            Assert.That(graph.edges.Length, Is.EqualTo(16));
            Assert.That(graph.paths.Select(p => p.bottleneck), Is.EqualTo(new[] { 21, 17, 13, 12, 12, 12 }));
            Assert.That(graph.nodes.All(n => n.id.Length == 18 && n.members.Single() == n.id), Is.True);
        }
        [Test] public void MotifConservesSelectedMembersAndSynapses()
        {
            var graph = ConnectomeGraph.Parse(Neurons);
            var motif = ConnectomeGraph.Parse(File.ReadAllText(ConnectomeSetup.Root + "Data/memory-motif-v783.json"));
            Assert.That(motif.nodes.SelectMany(n => n.members).OrderBy(x => x), Is.EqualTo(graph.nodes.Select(n => n.id).OrderBy(x => x)));
            Assert.That(motif.edges.Sum(e => e.synapses), Is.EqualTo(graph.edges.Sum(e => e.synapses)));
        }
        [Test] public void CorruptReferencesCountsAndWeightsAreRejected()
        {
            var graph = ConnectomeGraph.Parse(Neurons); graph.edges[0].target = "unknown";
            Assert.Throws<ArgumentException>(graph.Validate);
            graph = ConnectomeGraph.Parse(Neurons); graph.edges[0].synapses++;
            Assert.Throws<ArgumentException>(graph.Validate);
            graph = ConnectomeGraph.Parse(Neurons); graph.edges[0].visualWeight = float.NaN;
            Assert.Throws<ArgumentException>(graph.Validate);
            graph = ConnectomeGraph.Parse(Neurons); graph.paths[0].ids[0] = "unknown";
            Assert.Throws<ArgumentException>(graph.Validate);
        }
        [Test] public void LargeOrUncreditedDataIsRejected()
        {
            Assert.Throws<ArgumentException>(() => ConnectomeGraph.Parse(new string(' ', 65537)));
            var graph = ConnectomeGraph.Parse(Neurons); graph.license = "";
            Assert.Throws<ArgumentException>(graph.Validate);
        }
        [Test] public void MainScenesDoNotDependOnExperimentAndAssemblyCannotReadGame()
        {
            ConnectomeSetup.VerifyMainIsolation();
            Assert.That(typeof(ConnectomeViewer).Assembly.GetReferencedAssemblies().Any(a => a.Name == "FruitFlyParenting.Game"), Is.False);
            var asmdef = File.ReadAllText(ConnectomeSetup.Root + "Runtime/FruitFlyParenting.Connectome.asmdef");
            StringAssert.Contains("UNITY_EDITOR || CONNECTOME_EXPERIMENT", asmdef);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == ConnectomeSetup.ScenePath), Is.False);
        }
    }
}
