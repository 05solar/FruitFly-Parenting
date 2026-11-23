using System;

namespace FruitFlyParenting.Core
{
    // 방문 순간의 내용을 저장한다. 이후 밸런스/대사 에셋이 바뀌어도 회상은 실제 경험을 유지한다.
    [Serializable]
    public sealed class ItemExperience
    {
        public string itemId;
        public string displayName;
        public string[] tags = Array.Empty<string>();
        public string narrative;
        public string childLine;
        public float actualChangeMagnitude;
    }
}
