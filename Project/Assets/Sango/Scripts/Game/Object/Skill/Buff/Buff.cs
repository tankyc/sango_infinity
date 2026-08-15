using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using Sango.Core.Action;
using System.Collections.Generic;
using System.Numerics;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Buff : SangoObjectExtensionData
    {
        [JsonProperty] public int kind;
        [JsonProperty] public int subKind;
        [JsonProperty] public int limit;

        [JsonProperty] public string asset;

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty] public UnityEngine.Vector3 offset;

        /// <summary>
        /// 技能效果
        /// </summary>
        [JsonProperty] public JArray buffEffects;

        public bool IsControlBuff()
        {
            return true;
        }
    }
}
