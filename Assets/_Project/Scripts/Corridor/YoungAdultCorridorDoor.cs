using UnityEngine;

namespace FruitFlyParenting.Corridor
{
    public sealed class YoungAdultCorridorDoor : MonoBehaviour
    {
        Transform door;
        float elapsed;
        bool opening;
        public float Angle { get; private set; }
        public void Configure(Transform hinge) { door=hinge; }
        public void Open() { opening=true; }
        void Update()
        {
            if(!opening || door==null) return;
            elapsed+=Time.unscaledDeltaTime; Angle=Mathf.SmoothStep(0,70,Mathf.Clamp01(elapsed/3));
            door.localRotation=Quaternion.Euler(0,Angle,0);
        }
    }
}
