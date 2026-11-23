using FruitFlyParenting.Data;
using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.Room
{
    public sealed class PaperAirplaneView : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] Transform leftWing, rightWing;
        [SerializeField] Transform paper, child;
        [SerializeField] GameObject actors, tape;
        [SerializeField] Text caption;
        float started;
        public PaperAirplaneOutcome Outcome { get; private set; }
        public string Caption => caption != null ? caption.text : string.Empty;
        public float WingSeparation => Mathf.Abs(leftWing.localPosition.x - rightWing.localPosition.x);
        public void Configure(GameBalanceConfig balance, Transform left, Transform right, Text label,
            Transform plane, Transform childActor, GameObject sceneActors, GameObject repairTape)
        { config = balance; leftWing = left; rightWing = right; caption = label; paper=plane; child=childActor; actors=sceneActors; tape=repairTape; }
        public void Play(PaperAirplaneOutcome outcome)
        {
            Outcome = outcome; started = Time.time; caption.text = PaperAirplaneEvent.SceneLine(outcome);
            actors.SetActive(true);
        }
        void Update()
        {
            if (Outcome == PaperAirplaneOutcome.None) return;
            float progress = Mathf.Clamp01((Time.time - started) / config.paperAirplaneSceneSeconds);
            float gap = Outcome == PaperAirplaneOutcome.ApologizeAndRepair ? Mathf.Lerp(.22f,.015f,progress) : .22f;
            leftWing.localPosition = new Vector3(-gap,0,0); rightWing.localPosition = new Vector3(gap,0,0);
            rightWing.localRotation = Quaternion.Euler(0,Outcome == PaperAirplaneOutcome.ApologizeAndRepair ? Mathf.Lerp(18,0,progress) : 18,0);
            tape.SetActive(Outcome == PaperAirplaneOutcome.ApologizeAndRepair && progress >= .8f);
            paper.localPosition = Outcome == PaperAirplaneOutcome.TalkTomorrow ? Vector3.down * (.35f * progress) : Vector3.zero;
            child.localRotation = Outcome == PaperAirplaneOutcome.PostponeApology ? Quaternion.Euler(0,Mathf.Lerp(90,210,progress),0) :
                Quaternion.Euler(Outcome == PaperAirplaneOutcome.ApologizeAndRepair ? Mathf.Sin(progress*Mathf.PI)*20 : 0,90,0);
        }
    }
}
