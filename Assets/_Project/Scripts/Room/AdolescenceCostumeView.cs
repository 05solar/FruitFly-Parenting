using UnityEngine;

namespace FruitFlyParenting.Room
{
    public sealed class AdolescenceCostumeView : MonoBehaviour
    {
        [SerializeField] GameObject uniform, hidden;
        public bool Hidden { get; private set; }
        public void Configure(GameObject normal,GameObject alternative) { uniform=normal; hidden=alternative; }
        // Both outfits share the same actor, collider, navigation and choice model.
        public void Apply(bool value) { Hidden=value; uniform.SetActive(!value); hidden.SetActive(value); }
    }
}
