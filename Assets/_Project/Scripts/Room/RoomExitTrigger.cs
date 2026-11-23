using FruitFlyParenting.Wheelchair;
using FruitFlyParenting.Corridor;
using UnityEngine;

namespace FruitFlyParenting.Room
{
    public sealed class RoomExitTrigger : MonoBehaviour
    {
        [SerializeField] InfantRoomController room;
        [SerializeField] RoomTransitionController transition;
        [SerializeField] bool corridorExit;
        public void Configure(InfantRoomController controller) => room = controller;
        public void Configure(RoomTransitionController controller, bool isCorridorExit)
        { transition = controller; corridorExit = isCorridorExit; room = null; }
        void OnTriggerEnter(Collider other) => TryExit(other);
        void OnTriggerStay(Collider other) => TryExit(other);
        void TryExit(Collider other)
        {
            var chair = other.GetComponentInParent<WheelchairLocomotion>();
            if (chair == null) return;
            if (transition != null)
            {
                if (corridorExit) transition.TryEnterNextRoom(chair);
                else transition.TryEnterCorridor(chair);
            }
            else if (room != null) room.TryBeginExit(chair);
        }
    }
}
