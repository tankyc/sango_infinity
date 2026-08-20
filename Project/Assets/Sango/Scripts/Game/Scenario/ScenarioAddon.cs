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
        [JsonConverter(typeof(SangoObjectOffSetConverter<PersonLib>))]
        [JsonProperty] public SangoObjectOffSet<PersonLib> PersonLibrary = new SangoObjectOffSet<PersonLib>();

        public void Load(string file)
        {
            if (File.Exists(file))
            {
                ScenarioAddon scenarioAddon = new ScenarioAddon();
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), scenarioAddon);
                Combine(scenarioAddon);
            }
        }

        void FixID(ref int[] ids, int start)
        {
            if(ids == null || ids.Length == 0) { return; }
            for(int i = 0; i < ids.Length; i++)
            {
                int v = ids[i];
                if (v > 30000)
                {
                    v = v - 30000 + start;
                    ids[i] = v;
                }
            }
        }

        void FixID(ref int id, int start)
        {
            if (id > 30000)
                id = id + start;
        }

        public int Load(Mod.Mod mod, int start, string file)
        {
            if (File.Exists(file))
            {
                ScenarioAddon scenarioAddon = new ScenarioAddon();
                scenarioAddon.PersonLibrary.offset = PersonLibrary.offset;
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), scenarioAddon);
                scenarioAddon.PersonLibrary.ForEach(x =>
                {
                    x.Id = x.Id + start;
                    // 分配新ID
                    PersonLibrary.Add(x);

                    x.modName = mod.Name;
                    
                    // 梳理关系ID
                    FixID(ref x.Father, start);
                    FixID(ref x.Mother, start);
                    FixID(ref x.Brother, start);
                    FixID(ref x.BrotherList, start);
                    FixID(ref x.LikePersonList, start);
                    FixID(ref x.HatePersonList, start);
                });
            }
            return start;
        }

        public void Combine(ScenarioAddon scenarioAddon)
        {
            //int count = PersonLibrary.Count;
            scenarioAddon.PersonLibrary.ForEach(x =>
            {
                //// 检查重复名字的
                //PersonLib exsist = PersonLibrary.Find(y => x.familyName == y.familyName && x.giveName == y.giveName);
                //if (exsist == null)
                //{
                //    x.Id += count;
                //}
                //else
                //{

                //}
                PersonLibrary.Add(x);
            });
        }
    }
}
