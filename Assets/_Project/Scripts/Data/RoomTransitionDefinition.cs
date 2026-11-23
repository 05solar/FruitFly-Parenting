using FruitFlyParenting.Corridor;
using UnityEngine;

namespace FruitFlyParenting.Data
{
    [CreateAssetMenu(fileName = "Transition", menuName = "FruitFlyParenting/Room Transition")]
    public sealed class RoomTransitionDefinition : ScriptableObject
    {
        public LifeStage fromStage, toStage;
        public string fromRoomId, toRoomId, corridorId;
        public string recapTitle, nextRoomTitle;
        [TextArea] public string narration, nextRoomLine;
        public Color themeColor = new Color(.4f,.65f,.7f);
        public AudioClip previousRoomSound, nextRoomSound;
    }
}
