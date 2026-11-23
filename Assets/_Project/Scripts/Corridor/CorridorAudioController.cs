using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Corridor
{
    public sealed class CorridorAudioController : MonoBehaviour
    {
        [SerializeField] AudioSource previous, next;
        [SerializeField] AudioEchoFilter echo;
        GameBalanceConfig config;
        CorridorAudioMix target = new CorridorAudioMix(1,0);
        bool inCorridor;
        public AudioSource Previous => previous;
        public AudioSource Next => next;
        public void TakeOverPrevious(AudioSource source)
        {
            if (previous != null) previous.Stop();
            previous = source;
            echo = source.GetComponent<AudioEchoFilter>();
            // Unity components can be managed wrappers with a null native object.
            if (echo == null) echo = source.gameObject.AddComponent<AudioEchoFilter>();
            echo.delay = 320; echo.decayRatio = .25f; echo.dryMix = 1; echo.wetMix = 0;
        }
        public void Configure(AudioSource oldSound, AudioSource newSound, AudioEchoFilter echoFilter)
        { previous = oldSound; next = newSound; echo = echoFilter; }
        public void Initialize(GameBalanceConfig balance, RoomTransitionDefinition route)
        {
            config = balance;
            bool keepPlaying = previous.isPlaying && previous.clip == route.previousRoomSound;
            previous.clip = route.previousRoomSound; next.clip = route.nextRoomSound;
            previous.volume = config.corridorMusicVolume; next.volume = 0;
            echo.wetMix = 0;
            if (!keepPlaying) previous.Play(); next.Play();
        }
        public void SetCorridorDistance(float distance)
        {
            inCorridor = true;
            target = CorridorAudioMix.At(distance,config.corridorRecapLength,config.corridorLength);
        }
        public void EnterNextRoom() { inCorridor = false; target = new CorridorAudioMix(0,1); }
        void Update()
        {
            if (config == null || previous == null || next == null || echo == null) return;
            float step = Time.unscaledDeltaTime / Mathf.Max(.1f,config.corridorAudioBlendSeconds);
            previous.volume = Mathf.MoveTowards(previous.volume,target.Previous * config.corridorMusicVolume,step * config.corridorMusicVolume);
            next.volume = Mathf.MoveTowards(next.volume,target.Next * config.corridorMusicVolume,step * config.corridorMusicVolume);
            echo.wetMix = Mathf.MoveTowards(echo.wetMix,inCorridor ? .25f : 0f,step * .25f);
        }
    }
}
