using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Room
{
    public sealed class AdolescenceNarrative
    {
        readonly GameBalanceConfig config;
        readonly Dictionary<string,int> approaches=new Dictionary<string,int>();
        string[] preferredTags=Array.Empty<string>();
        float lastTool;
        bool announcedWindow, answeredBlocks;
        public string PreferredItemId { get; private set; }
        public int BlockCount { get; private set; }
        public bool Waited { get; private set; }
        public bool RefusedConversation { get; private set; }
        public AdolescenceNarrative(GameBalanceConfig balance) { config=balance; }
        public string Approach(ItemDefinition item)
        {
            if(item==null || PreferredItemId!=null) return null;
            approaches.TryGetValue(item.itemId,out int count); approaches[item.itemId]=++count;
            if(count<config.adolescencePreferenceApproaches) return null;
            PreferredItemId=item.itemId; preferredTags=(string[])item.tags.Clone();
            return "나 저기 가 볼래.";
        }
        public string Block(string[] tags,float rebellion)
        {
            if(PreferredItemId==null || tags==null || !tags.Intersect(preferredTags).Any() || answeredBlocks) return null;
            if(++BlockCount<config.adolescenceRepeatedBlocks) return null;
            answeredBlocks=true;
            return rebellion>=config.highRebellionDialogueThreshold ? "왜 자꾸 막아? 내가 고를래." : "알았어. 시키는 대로 할게.";
        }
        public bool RefuseConversation(float trust)
        {
            if(RefusedConversation || trust>=config.adolescenceConversationTrustThreshold) return false;
            RefusedConversation=true; return true;
        }
        public void ToolUsed(float elapsed) => lastTool=elapsed;
        public bool InWaitingWindow(float elapsed) => elapsed>=DoorTime-config.adolescenceWaitingWindowSeconds && elapsed<=DoorTime;
        float DoorTime => Math.Max(config.roomEarliestExitSeconds,config.roomDoorOpenSeconds);
        public string Advance(float elapsed)
        {
            if(Waited || elapsed<DoorTime-config.adolescenceWaitingWindowSeconds) return null;
            float start=Math.Max(lastTool,DoorTime-config.adolescenceWaitingWindowSeconds);
            // A frame can cross the door deadline. Credit only time up to that deadline.
            if(Math.Min(elapsed,DoorTime)-start>=config.adolescenceQuietSeconds)
            { Waited=true; return "기다려 줘서 고마워. 나 이제 말할게."; }
            if(announcedWindow || elapsed>DoorTime) return null;
            announcedWindow=true; return "잠깐만. 무슨 말부터 할지 생각 좀 할게.";
        }
    }
}
