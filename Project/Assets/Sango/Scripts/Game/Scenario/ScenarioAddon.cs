using System.IO;
using TKNewtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 剧本附加数据
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioAddon
    {
        /// <summary>
        /// 自建武将
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<PersonLib>))]
        [JsonProperty] public SangoObjectMap<PersonLib> PersonAddonMap = new SangoObjectMap<PersonLib>();


        public void Load(string file)
        {
            if (File.Exists(file))
            {
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), this);
            }
        }
    }
}
