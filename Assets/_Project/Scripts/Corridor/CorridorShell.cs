using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Corridor
{
    public sealed class CorridorShell : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] GameObject endDoor;
        [SerializeField] CorridorRecapController recap;
        [SerializeField] UnityEngine.UI.Text narrationBoard;
        public CorridorRecapController Recap => recap;
        public GameObject EndDoor => endDoor;
        public float Length => config.corridorLength;
        public float Width => config.corridorWidth;
        public float Distance(Vector3 position) => transform.InverseTransformPoint(position).x;
        public bool AtSourceBoundary(Vector3 position)
        {
            var point = transform.InverseTransformPoint(position);
            return point.x >= 0 && point.x <= 1f && Mathf.Abs(point.z) < 1.5f;
        }
        public bool AtDestinationBoundary(Vector3 position)
        {
            var point = transform.InverseTransformPoint(position);
            // 휠체어 반경 0.4m 전체가 다음 방 바닥에 들어온 후 복도를 숨긴다.
            return point.x >= Length + .6f && Mathf.Abs(point.z) < Width * .5f;
        }
        public CorridorSection SectionAt(Vector3 position)
        {
            float distance = Distance(position);
            return distance < config.corridorRecapLength ? CorridorSection.Recap :
                distance < Length - config.corridorSilhouetteLength ? CorridorSection.Narration : CorridorSection.Silhouette;
        }
        public void Configure(GameBalanceConfig balance, GameObject door, CorridorRecapController memories, UnityEngine.UI.Text narration)
        { config = balance; endDoor = door; recap = memories; narrationBoard = narration; }
        public void SetRoute(RoomTransitionDefinition definition) => narrationBoard.text = definition.narration;
        public void SetNextRoomReady(bool ready) => endDoor.SetActive(!ready);
    }
}
