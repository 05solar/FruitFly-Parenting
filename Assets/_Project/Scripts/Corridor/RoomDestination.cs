using UnityEngine;

namespace FruitFlyParenting.Corridor
{
    // 09 이후 실제 방 컨트롤러가 이 준비 상태와 입장 훅을 재사용한다.
    public sealed class RoomDestination : MonoBehaviour
    {
        [SerializeField] GameObject entranceGate;
        bool prepared;
        public bool ContentReady { get; set; } = true;
        public bool ReadyForEntry => prepared && ContentReady && gameObject.activeInHierarchy;
        public bool Entered { get; private set; }
        public event System.Action Entering;
        public void Configure(GameObject gate) => entranceGate = gate;
        public void Prepare() { gameObject.SetActive(true); entranceGate.SetActive(false); prepared = true; }
        public void Enter()
        {
            Entered = true; entranceGate.SetActive(true); Entering?.Invoke();
        }
    }
}
