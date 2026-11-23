using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Core
{
    // GameState는 순수 C# 클래스라 씬 생명주기가 없다.
    // 이 MonoBehaviour는 부트스트랩 씬에 하나만 배치되어 GameState.Current를 만들고
    // DontDestroyOnLoad로 씬 전환 동안 유지한다. 게임플레이 로직은 여기에 두지 않는다.
    public class GameStateBootstrap : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] int runSeed;
        [SerializeField] bool useRandomSeedIfZero = true;

        // 에디터/런타임 빌더가 공통으로 쓰는 설정 경로(SerializedObject를 쓰지 않음).
        public void ConfigureForBuilder(GameBalanceConfig gameBalanceConfig, int seed)
        {
            config = gameBalanceConfig;
            runSeed = seed;
            useRandomSeedIfZero = false;
        }

        void Awake()
        {
            if (GameState.Current != null)
            {
                Destroy(gameObject);
                return;
            }

            if (config == null)
            {
                Debug.LogError("GameStateBootstrap: GameBalanceConfig가 할당되지 않았습니다.");
                return;
            }

            int seed = runSeed;
            if (useRandomSeedIfZero && seed == 0)
            {
                seed = System.Guid.NewGuid().GetHashCode();
            }

            var state = new GameState(config, seed);
            GameState.SetCurrent(state);
            DontDestroyOnLoad(gameObject);
        }
    }
}
