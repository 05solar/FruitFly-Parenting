using UnityEngine;

namespace FruitFlyParenting.Data
{
    public sealed class RoomItemSpec
    {
        public readonly string Id, Name, FirstLine, RevisitLine;
        public readonly string[] Tags;
        public readonly float[] Stats;
        public readonly float Trust, Rebellion;
        public readonly Vector2 Position;
        public readonly bool Delinquency;
        public readonly YoungAdultFlags Flags;
        public bool Extreme => Trust != 0;
        public RoomItemSpec(string id, string name, Vector2 position, string[] tags, float[] stats, string line,
            string revisit, float trust = 0, float rebellion = 0, string prefix = "childhood_", bool delinquency = false, YoungAdultFlags flags=YoungAdultFlags.None)
        { Id=prefix+id; Name=name; Position=position; Tags=tags; Stats=stats; FirstLine=line; RevisitLine=revisit; Trust=trust; Rebellion=rebellion; Delinquency=delinquency; Flags=flags; }
    }
}
