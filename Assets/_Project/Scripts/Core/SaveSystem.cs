using System.IO;
using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Core
{
    // 스펙 8장의 저장 시점(방 진입 / 물품 최초 방문 / 복도 시작 / 엔딩 확정)에 대응하는 최소 구현.
    // 세이브 슬롯 UI는 범위 밖이며, 지금은 단일 파일에 JSON으로 직렬화/역직렬화만 한다.
    public class SaveSystem
    {
        readonly GameBalanceConfig config;

        public SaveSystem(GameBalanceConfig config)
        {
            this.config = config;
        }

        public string GetDefaultSaveFilePath() =>
            FruitFlyParenting.Debugging.PlaytestRecorder.IsolatedSavePath ?? Path.Combine(Application.persistentDataPath, "savegame.json");

        // checkpointName은 지금 단계에서는 디버그/로그용이다. 별도 슬롯 분기는 하지 않는다.
        public void SaveCheckpoint(GameState state, string checkpointName, string filePath = null)
        {
            Debug.Log($"SaveSystem: checkpoint '{checkpointName}' 저장");
            Save(state, filePath);
        }

        public void Save(GameState state, string filePath = null)
        {
            filePath ??= GetDefaultSaveFilePath();
            GameStateSaveData data = state.ToSaveData();
            string json = JsonUtility.ToJson(data);
            File.WriteAllText(filePath, json);
        }

        public GameState Load(string filePath = null)
        {
            filePath ??= GetDefaultSaveFilePath();
            string json = File.ReadAllText(filePath);
            GameStateSaveData data = JsonUtility.FromJson<GameStateSaveData>(json);
            return GameState.FromSaveData(config, data);
        }

        public bool SaveFileExists(string filePath = null)
        {
            filePath ??= GetDefaultSaveFilePath();
            return File.Exists(filePath);
        }
    }
}
