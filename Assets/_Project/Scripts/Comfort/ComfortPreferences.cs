using System;
using UnityEngine;

namespace FruitFlyParenting.Comfort
{
    [Serializable]
    public sealed class ComfortPreferences
    {
        public const string DefaultKey = "FruitFlyParenting.Comfort.v1";
        public const float MinTurn = 10, MaxTurn = 45, HeightLimit = .35f, SeatedEyeHeight = 1.2f;
        public int version = 1;
        public float turnSpeed = 30;
        public bool vignette = true;
        public float heightOffset;
        public void Sanitize()
        {
            turnSpeed = float.IsNaN(turnSpeed) || float.IsInfinity(turnSpeed) ? 30 : Mathf.Clamp(turnSpeed, MinTurn, MaxTurn);
            heightOffset = float.IsNaN(heightOffset) || float.IsInfinity(heightOffset) ? 0 : Mathf.Clamp(heightOffset, -HeightLimit, HeightLimit);
        }
        public static ComfortPreferences Load(string key = DefaultKey)
        {
            try
            {
                var value = JsonUtility.FromJson<ComfortPreferences>(PlayerPrefs.GetString(key, ""));
                if (value == null || value.version != 1) return new ComfortPreferences();
                value.Sanitize(); return value;
            }
            catch (ArgumentException) { return new ComfortPreferences(); }
        }
        public void Save(string key = DefaultKey) { Sanitize(); PlayerPrefs.SetString(key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static float CalibratedOffset(float currentOffset, float measuredEyeHeight) =>
            Mathf.Clamp(currentOffset + SeatedEyeHeight - measuredEyeHeight, -HeightLimit, HeightLimit);
    }
}
