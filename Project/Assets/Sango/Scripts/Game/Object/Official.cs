using System.Collections;
using System.IO;
using Newtonsoft.Json;

namespace Sango.Game
{
    /// <summary>
    /// 特性
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class Official : SangoObject
    {
        public int troopsLimit;
        public int cost;
        public int level;
        public int meritNeeds;
    }
}
