using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 城池耐久伤害减免（坚城 / 固守 / 郝昭固守一类"城池更耐打"的效果）。
    ///
    /// <para>
    /// 【挂载点】<see cref="GameEvent.OnBuildingCheckDurabilityDamage"/>，
    /// 由 <c>BuildingBase.ChangeDurability</c> 在真正扣耐久之前询问，
    /// 因此飘字、城陷判定、AI 威胁登记拿到的都是修正后的数值，不会出现
    /// "界面显示 -1000、实际只掉 500" 的口径分裂。
    /// </para>
    /// <para>
    /// 【只保护城池】本类只对 <see cref="City"/> 生效，设施（箭楼 / 陷阱等）不参与 ——
    /// 设计稿里的"坚城"指的是据点，放开到设施会让野外建筑变得无法拆除。
    /// </para>
    /// <para>
    /// 【宿主】可以配在城池上（只保护该城），也可以配在势力上（保护该势力所有城）。
    /// 两者都为 null 时打 Warning 并不订阅，避免"配了却完全没反应"。
    /// </para>
    /// </summary>
    public class CityReduceDurabilityDamage : CityActionBase
    {
        /// <summary>减伤百分比（30 表示耐久伤害 -30%）</summary>
        int value;

        /// <summary>可选附加条件（走 CityConditionDatabase 语义的通用条件）</summary>
        Condition condition;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：value（减伤百分点）、condition（可选）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            value = p.Value<int>("value");
            if (value <= 0)
            {
                Sango.Log.Warning("CityReduceDurabilityDamage 的 value 未配置（或 <= 0），本次减免不会生效");
                return;
            }

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null)
                    condition.Init(conObj, sangoObjects);
                else
                    Sango.Log.Error($"城池 Action 的条件类未注册：{conObj.Value<string>("class")}，已忽略该条件");
            }

            if (City == null && Force == null)
            {
                Sango.Log.Warning("CityReduceDurabilityDamage 既没有绑定城池也没有绑定势力，不会生效");
                return;
            }

            GameEvent.OnBuildingCheckDurabilityDamage += OnBuildingCheckDurabilityDamage;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnBuildingCheckDurabilityDamage -= OnBuildingCheckDurabilityDamage;
        }

        /// <summary>
        /// 耐久结算前按配置减伤。
        /// </summary>
        /// <param name="building">被攻击的建筑</param>
        /// <param name="atk">攻击来源</param>
        /// <param name="damageData">耐久变化量（负数代表受伤）</param>
        void OnBuildingCheckDurabilityDamage(BuildingBase building, SangoObject atk, OverrideData<int> damageData)
        {
            if (building == null || damageData == null)
                return;
            if (damageData.Value >= 0)
                return;

            // 只保护城池，设施不参与
            City city = building as City;
            if (city == null)
                return;

            if (City != null && City != city) return;
            if (Force != null && city.BelongForce != Force) return;

            if (condition != null)
            {
                // 城池场景没有专用的条件上下文，用通用容器把"当前城池 + 所属势力"填进去，
                // 这样 CityAttributeCheck / FactionCheck 这类条件可以直接复用。
                DefaultConditionDatabase db = new DefaultConditionDatabase
                {
                    ActionCity = city,
                    ActionForce = city.BelongForce,
                    ActionObject = city,
                };
                if (!condition.Check(db))
                    return;
            }

            damageData.Value = damageData.Value * (100 - value) / 100;
        }
    }
}
