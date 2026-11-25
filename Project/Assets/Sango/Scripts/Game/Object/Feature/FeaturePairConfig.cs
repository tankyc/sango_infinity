using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 成对 / 组合特技配置（连舟寨、冀雄、车下士这类"两人配合"的机制）。
    ///
    /// <para>
    /// 【职责】只描述"配对关系"与"生效条件"，具体指令效果由 Action（commandType）执行。
    /// 这样数据可以独立于代码扩充，新增一对组合不需要改系统代码。
    /// </para>
    /// <para>
    /// 【性能】配对检查只在 UI 菜单弹出 / 指令执行时调用，属低频路径；
    /// 条件全部是标量比较 + 一次格子距离，不做全量武将遍历。
    /// </para>
    /// </summary>
    public class FeaturePairConfig : SangoObject
    {
        /// <summary>主特技 Id（发起方必须具备）</summary>
        [JsonProperty] public int featureId;

        /// <summary>对方特技 Id（0 = 不要求）</summary>
        [JsonProperty] public int partnerFeatureId;

        /// <summary>配对武将 Id 列表（空 = 不限定具体武将）</summary>
        [JsonProperty] public int[] partnerPersonIds;

        /// <summary>是否要求同队（1 = 同队，0 = 不限）</summary>
        [JsonProperty] public int requireSameTroop;

        /// <summary>是否要求同势力（1 = 同势力，0 = 不限）</summary>
        [JsonProperty] public int requireSameForce;

        /// <summary>跨队时的最大距离（0 = 不限制）</summary>
        [JsonProperty] public int maxDistance;

        /// <summary>指令类型（由具体 Action 解释，0 = 仅配对、无指令）</summary>
        [JsonProperty] public int commandType;

        /// <summary>指令参数（交给 Action 解释，避免每加一种组合就改数据类）</summary>
        [JsonProperty] public JObject commandParams;

        /// <summary>
        /// 剧本准备：校验配置引用，避免"写错 Id 静默不生效"。
        /// 只在开局 / 读档时跑一次，低频。
        /// </summary>
        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);

            if (scenario == null || scenario.CommonData == null)
                return;

            if (featureId > 0 && scenario.CommonData.Features.Get(featureId) == null)
                Sango.Log.Error($"[成对特技] {Name}(Id={Id}) 的主特技 Id={featureId} 不存在，组合不会触发");

            if (partnerFeatureId > 0 && scenario.CommonData.Features.Get(partnerFeatureId) == null)
                Sango.Log.Error($"[成对特技] {Name}(Id={Id}) 的对方特技 Id={partnerFeatureId} 不存在，组合不会触发");

            if (partnerPersonIds != null)
            {
                for (int i = 0; i < partnerPersonIds.Length; i++)
                {
                    int personId = partnerPersonIds[i];
                    if (personId > 0 && scenario.personSet.Get(personId) == null)
                        Sango.Log.Error($"[成对特技] {Name}(Id={Id}) 的配对武将 Id={personId} 不存在");
                }
            }

            if (commandParams != null)
            {
                int troopsCost = commandParams.Value<int>("troopsCost");
                int goldCost = commandParams.Value<int>("goldCost");
                if (troopsCost < 0 || goldCost < 0)
                    Sango.Log.Error($"[成对特技] {Name}(Id={Id}) 的 troopsCost/goldCost 不能为负（当前 {troopsCost}/{goldCost}）");
            }
        }
    }
}
