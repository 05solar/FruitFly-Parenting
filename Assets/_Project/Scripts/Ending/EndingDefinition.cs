using UnityEngine;

namespace FruitFlyParenting.Ending
{
    public enum LetterboxState { Empty,Letter,Overflowing }
    public enum SuitcaseState { Absent,Packed,Worn }
    public enum HandleState { Closed,Open,Key }
    [CreateAssetMenu(menuName="FruitFlyParenting/Ending")]
    public sealed class EndingDefinition : ScriptableObject
    {
        public EndingCondition condition;
        public int priority;
        public string endingId,title,childLine,sceneLine,calendarText;
        public LetterboxState letterbox;
        public SuitcaseState suitcase;
        public HandleState handle;
        public bool childPresent;
        public Color lightColor=Color.white;
    }
}
