using System.Collections.Generic;
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
        [JsonConverter(typeof(SangoObjectSetConverter<PersonLib>))]
        [JsonProperty] public SangoObjectSet<PersonLib> PersonLibrary = new SangoObjectSet<PersonLib>();

        public void Load(string file)
        {
            if (File.Exists(file))
            {
                ScenarioAddon scenarioAddon = new ScenarioAddon();
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), scenarioAddon);
                Combine(scenarioAddon);
            }
        }

        public void Combine(ScenarioAddon scenarioAddon)
        {
            int count = PersonLibrary.Count;
            scenarioAddon.PersonLibrary.ForEach(x =>
            {
                // 检查重复名字的
                PersonLib exsist = PersonLibrary.Find(y => x.familyName == y.familyName && x.giveName == y.giveName);
                if (exsist == null)
                {
                    x.Id += count;
                    PersonLibrary.Add(x);
                }
                else
                {

                }
            });
        }
    }
}
