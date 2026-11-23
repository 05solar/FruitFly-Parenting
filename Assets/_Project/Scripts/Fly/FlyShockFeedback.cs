using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Fly
{
    public sealed class FlyShockFeedback : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] GameObject spark;
        float visibleUntil;
        public int ShockCount { get; private set; }
        public bool IsVisible => spark != null && spark.activeSelf;
        public void Configure(GameBalanceConfig balance, GameObject sparkVisual) { config = balance; spark = sparkVisual; }
        void OnEnable() => EventBus.ElectricShockVisual += OnShock;
        void OnDisable()
        {
            EventBus.ElectricShockVisual -= OnShock;
            if (spark != null) spark.SetActive(false);
        }
        void OnShock(Vector3 position)
        {
            if ((position - transform.position).sqrMagnitude > 0.001f || config == null || spark == null) return;
            ShockCount++;
            visibleUntil = Time.time + config.shockVisualDurationSeconds;
            spark.SetActive(true);
        }
        void Update() { if (spark != null && Time.time >= visibleUntil) spark.SetActive(false); }
    }
}
