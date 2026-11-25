#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Room;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FruitFlyParenting.Simulation
{
    [Serializable] public sealed class SimulationSummary
    {
        public int runs, replayMatches, hiddenCostumes, passiveLove100, sceneLoads;
        public double seconds;
        public int[] endingCounts = new int[14];
        public double[] meanFinalStats = new double[7];
        public List<string> violations = new List<string>();
        public List<SimulationWitness> witnesses = new List<SimulationWitness>();
        public string[] scenarioChecks;
    }
    [Serializable] public sealed class SimulationWitness { public string policy, hash; public int seed, ending; }

    public static class SimulationRunnerMenu
    {
        public const string ReportFolder = "SimulationReports";
        public static GameBalanceConfig LoadConfig() => AssetDatabase.LoadAssetAtPath<GameBalanceConfig>("Assets/_Project/ScriptableObjects/GameBalanceConfig.asset");
        public static EndingDefinition[] LoadEndings() => AssetDatabase.FindAssets("t:EndingDefinition", new[] { "Assets/_Project/ScriptableObjects/Endings" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EndingDefinition>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(d => d.priority).ToArray();
        public static SimulationRoom[] LoadRooms()
        {
            var items = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/ScriptableObjects/Items" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToDictionary(i => i.itemId);
            string[] infant = { "infant_book", "infant_blocks", "infant_learning", "infant_hug", "infant_ball", "infant_snack", "infant_bed", "infant_crawl", "infant_tablet" };
            SimulationItem Item(string id, Vector2 p) => new SimulationItem { Definition = items[id], Position = new Vector3(p.x, 0, p.y) };
            SimulationRoom Room(string id, LifeStage stage, RoomItemSpec[] catalog) => new SimulationRoom { Id = id, Stage = stage, Items = catalog.Select(e => Item(e.Id, e.Position)).ToArray() };
            return new[] {
                new SimulationRoom { Id = InfantRoomController.RoomId, Stage = LifeStage.Infant, Items = infant.Select((id, i) => Item(id, RoomCoordinates.ItemSlots[i].pos)).ToArray() },
                Room(ChildhoodRoomController.RoomId, LifeStage.Childhood, ChildhoodCatalog.Entries),
                Room(AdolescenceRoomController.RoomId, LifeStage.Adolescence, AdolescenceCatalog.Entries),
                Room(YoungAdultRoomController.RoomId, LifeStage.YoungAdult, YoungAdultCatalog.Entries)
            };
        }
        public static string Fingerprint(SimulationResult result)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(result)))).Replace("-", "").ToLowerInvariant();
        }
        public static void DiscoverTarget()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-simulationTarget");
            if (index < 0) throw new ArgumentException("Supply -simulationTarget 1..14");
            int ending = int.Parse(args[index + 1], CultureInfo.InvariantCulture);
            var runner = new SimulationRunner(LoadConfig(), LoadRooms(), LoadEndings());
            var policy = new TargetedEndingPolicy((EndingCondition)ending); var counts = new int[14];
            for (int seed = 0; seed < 20000; seed++)
            {
                var result = runner.Run(seed, policy); counts[result.ending - 1]++;
                if (result.violations.Length > 0) throw new InvalidOperationException(string.Join(";", result.violations));
                if (result.ending != ending) continue;
                Directory.CreateDirectory(ReportFolder);
                File.WriteAllText(Path.Combine(ReportFolder, policy.Id + "-ending-" + ending + ".json"), JsonUtility.ToJson(result, true));
                UnityEngine.Debug.Log("Discovered target " + ending + " seed=" + seed + " counts=" + string.Join(",", counts)); return;
            }
            throw new InvalidOperationException("Target search exhausted: " + ending + " counts=" + string.Join(",", counts));
        }
        [MenuItem("Fruit Fly Parenting/Run 16 Headless Simulation")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run simulations outside Play mode.");
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-simulationCount");
            int count = option >= 0 ? int.Parse(args[option + 1], CultureInfo.InvariantCulture) : 1000;
            bool probe = args.Contains("-simulationProbe");
            var config = LoadConfig(); var rooms = LoadRooms(); var endings = LoadEndings();
            var runner = new SimulationRunner(config, rooms, endings);
            var summary = new SimulationSummary(); var watch = Stopwatch.StartNew();
            summary.scenarioChecks = SimulationScenarioAudit.Run(config, rooms, endings);
            Directory.CreateDirectory(ReportFolder);
            using var csv = new StreamWriter(Path.Combine(ReportFolder, "runs.csv"), false, Encoding.UTF8);
            using var events = new StreamWriter(Path.Combine(ReportFolder, "runs.jsonl"), false, new UTF8Encoding(false));
            csv.WriteLine("policy,seed,ending,S,I,H,P,L,R,T,interventions,hash");
            void Loaded(Scene scene, LoadSceneMode mode) => summary.sceneLoads++;
            SceneManager.sceneLoaded += Loaded;
            try
            {
                void Execute(int seed, IPlayerPolicy policy)
                {
                    var result = runner.Run(seed, policy); string hash = Fingerprint(result);
                    var replay = runner.Run(seed, policy);
                    if (hash == Fingerprint(replay)) summary.replayMatches++;
                    else summary.violations.Add("Replay mismatch " + policy.Id + "/" + seed);
                    summary.runs++; summary.endingCounts[result.ending - 1]++;
                    summary.violations.AddRange(result.violations.Select(v => policy.Id + "/" + seed + ":" + v));
                    if (result.state.adolescenceHiddenCostume) summary.hiddenCostumes++;
                    var s = result.state.finalYoungAdultSnapshot;
                    if (policy is PassivePolicy && s.Love == 100) summary.passiveLove100++;
                    double[] stats = { s.Stress, s.Intelligence, s.Health, s.Popularity, s.Love, s.Rebellion, s.Trust };
                    for (int i = 0; i < stats.Length; i++) summary.meanFinalStats[i] += stats[i];
                    csv.WriteLine(policy.Id + "," + seed + "," + result.ending + "," + string.Join(",", stats.Select(x => x.ToString("R", CultureInfo.InvariantCulture))) + "," + s.Interventions + "," + hash);
                    events.WriteLine(JsonUtility.ToJson(result));
                    bool newEnding = !summary.witnesses.Any(w => w.ending == result.ending);
                    if (newEnding || (policy is TargetedEndingPolicy target && (int)target.TargetEnding == result.ending && !summary.witnesses.Any(w => w.policy == policy.Id)))
                    {
                        summary.witnesses.Add(new SimulationWitness { policy = policy.Id, seed = seed, ending = result.ending, hash = hash });
                        File.WriteAllText(Path.Combine(ReportFolder, policy.Id + "-ending-" + result.ending + ".json"), JsonUtility.ToJson(result, true));
                    }
                }
                foreach (var policy in new IPlayerPolicy[] { new PassivePolicy(), new RandomInterventionPolicy(), new AggressiveCoercionPolicy() })
                {
                    for (int seed = 0; seed < count; seed++) Execute(seed, policy);
                    UnityEngine.Debug.Log("Simulation " + policy.Id + " complete; endings=" + string.Join(",", summary.endingCounts));
                }
                if (!probe)
                {
                    for (int ending = 1; ending <= 14; ending++)
                    {
                        var policy = new TargetedEndingPolicy((EndingCondition)ending);
                        string cachedPath = Path.Combine(ReportFolder, policy.Id + "-ending-" + ending + ".json");
                        // A previous witness is only a search hint. Always replay its policy from the initial state.
                        if (File.Exists(cachedPath)) Execute(JsonUtility.FromJson<SimulationResult>(File.ReadAllText(cachedPath)).seed, policy);
                        for (int seed = 0; seed < 10000 && !summary.witnesses.Any(w => w.policy == policy.Id && w.ending == ending); seed++) Execute(seed, policy);
                        if (!summary.witnesses.Any(w => w.policy == policy.Id && w.ending == ending)) summary.violations.Add("Unreached targeted ending " + ending);
                        else if (ending == (int)EndingCondition.MagicPromise)
                            Execute(summary.witnesses.First(w => w.policy == policy.Id && w.ending == ending).seed, new PassivePolicy());
                        UnityEngine.Debug.Log("Targeted ending " + ending + " complete; runs=" + summary.runs);
                    }
                }
            }
            finally
            {
                SceneManager.sceneLoaded -= Loaded;
                summary.seconds = watch.Elapsed.TotalSeconds;
                for (int i = 0; i < summary.meanFinalStats.Length; i++) summary.meanFinalStats[i] /= Math.Max(1, summary.runs);
                File.WriteAllText(Path.Combine(ReportFolder, "summary.json"), JsonUtility.ToJson(summary, true));
            }
            UnityEngine.Debug.Log("SIMULATION SUMMARY " + JsonUtility.ToJson(summary));
            if (summary.violations.Count > 0) throw new InvalidOperationException("Simulation violations: " + string.Join(";", summary.violations.Take(20)));
            if (!probe && (summary.runs < 1000 || summary.endingCounts.Any(c => c == 0) || summary.passiveLove100 == 0 || summary.hiddenCostumes == 0 || summary.sceneLoads != 0))
                throw new InvalidOperationException("Simulation acceptance not met; inspect summary.json.");
        }
    }
}
#endif
