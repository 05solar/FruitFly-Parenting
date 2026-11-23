using System;
using UnityEngine;

namespace FruitFlyParenting.Core
{
    // 밀기 접촉의 두 소스. 04번 프롬프트(전기 휠체어/사랑의 매)의 PushResolver가 그대로 사용할 구분이다.
    public enum PushSource
    {
        Wheelchair,
        ElectricStick,
    }

    // 방/도구/엔딩 로직 사이의 느슨한 결합을 위한 전역 Pub/Sub.
    // 04번 이후 프롬프트들이 이 이벤트들을 발행/구독한다.
    public static class EventBus
    {
        public static event Action<string, bool> ItemVisited; // itemId, isFirstVisit
        public static event Action<PushSource, Vector3, string> ToolUsed; // source, direction, targetItemId(nullable)
        public static event Action<string> RoomEntered; // roomId
        public static event Action<string> RoomExited; // roomId

        public static void RaiseItemVisited(string itemId, bool isFirstVisit) =>
            ItemVisited?.Invoke(itemId, isFirstVisit);

        public static void RaiseToolUsed(PushSource source, Vector3 direction, string targetItemId) =>
            ToolUsed?.Invoke(source, direction, targetItemId);

        public static void RaiseRoomEntered(string roomId) => RoomEntered?.Invoke(roomId);

        public static void RaiseRoomExited(string roomId) => RoomExited?.Invoke(roomId);

        // 테스트 사이의 격리를 위해 구독을 전부 해제한다(정적 이벤트는 테스트 간에 상태가 남기 쉽다).
        public static void ClearAllSubscribers()
        {
            ItemVisited = null;
            ToolUsed = null;
            RoomEntered = null;
            RoomExited = null;
        }
    }
}
