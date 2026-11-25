using System.Linq;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using UnityEngine;

namespace FruitFlyParenting.Comfort
{
    [RequireComponent(typeof(ItemSlot))]
    public sealed class ItemApproachFeedback : MonoBehaviour
    {
        [SerializeField] Material haloMaterial;
        [SerializeField] AudioClip approachCue;
        ItemSlot slot;
        RoomController room;
        Renderer halo;
        Light lamp;
        AudioSource sound;
        MaterialPropertyBlock colors;
        bool approaching;
        bool lastVisited;
        float nextCheck;
        public bool Visited => room != null && room.VisitedItemIds.Contains(slot.ItemId);
        public int CueCount { get; private set; }
        public float LampIntensity => lamp != null ? lamp.intensity : 0;
        public bool Approaching => approaching;
        public void Configure(Material material, AudioClip cue) { haloMaterial = material; approachCue = cue; }
        void OnEnable() { nextCheck = 0; }
        void Start()
        {
            slot = GetComponent<ItemSlot>(); room = GetComponentInParent<RoomController>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad); floor.name = "Approach floor shadow";
            Destroy(floor.GetComponent<Collider>()); floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0, .025f - transform.position.y, 0);
            floor.transform.localRotation = Quaternion.Euler(90, 0, 0);
            floor.transform.localScale = Vector3.one * (slot.InteractionRadius * 2);
            halo = floor.GetComponent<Renderer>(); halo.sharedMaterial = haloMaterial; colors = new MaterialPropertyBlock();
            halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; halo.receiveShadows = false;
            lamp = new GameObject("First visit lamp").AddComponent<Light>(); lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(0, 1.4f, 0); lamp.type = LightType.Point; lamp.range = 2.5f;
            lamp.shadows = LightShadows.None; lamp.color = new Color(.75f,.9f,1);
            // Quest uses the unlit floor cue for first-visit dimming, avoiding one realtime light per slot.
            lamp.enabled = Application.platform != RuntimePlatform.Android;
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 1;
            sound.volume = .08f; sound.minDistance = 1; sound.maxDistance = 8; sound.clip = approachCue;
            RefreshVisuals();
        }
        void Update()
        {
            if (slot == null || Time.time < nextCheck) return;
            nextCheck = Time.time + .1f;
            bool close = false;
            float radius = slot.InteractionRadius + 1.5f;
            var actors = FlyAgent.SceneActors;
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor == null || !actor.gameObject.activeInHierarchy) continue;
                var delta = actor.transform.position - transform.position; delta.y = 0;
                if (delta.sqrMagnitude < radius * radius) { close = true; break; }
            }
            // Entry cue starts OUTSIDE the interaction radius; rearm only after leaving.
            if (close && !approaching) { sound.Play(); CueCount++; }
            bool visited = Visited;
            if (approaching == close && lastVisited == visited) return;
            approaching = close; RefreshVisuals();
        }
        void RefreshVisuals()
        {
            lastVisited = Visited;
            lamp.intensity = lastVisited ? .12f : .65f;
            var color = approaching ? new Color(.12f,.28f,.33f) : new Color(.035f,.05f,.06f);
            if (lastVisited) color *= .45f;
            colors.SetColor("_BaseColor", color); halo.SetPropertyBlock(colors);
        }
    }
}
