using Sango.Core.Action;
using System.Collections.Generic;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core
{
    public class Equipment : ItemType
    {
        /// <summary>
        /// 品级
        /// </summary>
        [JsonProperty] public int grade;

        /// <summary>
        /// 发现概率
        /// </summary>
        [JsonProperty] public int discoverProbability;

        /// <summary>
        /// 统率加成
        /// </summary>
        [JsonProperty] public int commandBonus;

        /// <summary>
        /// 武力加成
        /// </summary>
        [JsonProperty] public int strengthBonus;

        /// <summary>
        /// 智力加成
        /// </summary>
        [JsonProperty] public int intelligenceBonus;

        /// <summary>
        /// 政治加成
        /// </summary>
        [JsonProperty] public int politicsBonus;

        /// <summary>
        /// 魅力加成
        /// </summary>
        [JsonProperty] public int glamourBonus;

        /// <summary>
        /// 附加特技
        /// </summary>
        // 序列化形态保持 int[]（键名不变，老存档可读）；运行期列表由 OnScenarioPrepare 解析。
        [JsonProperty("features")]
        public int[] features_list;
        public SangoObjectList<Feature> features = new SangoObjectList<Feature>();

        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);
            if (features_list != null && features_list.Length > 0 && features.Count == 0)
                features.FromArray(features_list);
        }

        /// <summary>存档前回写 int[]（否则会把读档时的旧 id 存回去）。</summary>
        public override void OnScenarioSave(Scenario scenario)
        {
            base.OnScenarioSave(scenario);
            features_list = features != null ? features.ToArray() : null;
        }

        /// <summary>
        /// 效果实体集合
        /// </summary>
        [JsonProperty]
        public TKNewtonsoft.Json.Linq.JArray actionEntities;

        public void InitActions(List<ActionBase> list, params SangoObject[] sangoObjects)
        {
            if (actionEntities == null) return;
            for (int i = 0; i < actionEntities.Count; i++)
            {
                JObject valus = actionEntities[i] as JObject;
                ActionBase action = ActionBase.Create(valus.Value<string>("class"));
                if (action != null)
                {
                    action.Init(valus, sangoObjects);
                    list.Add(action);
                }
            }
        }
    }
}
