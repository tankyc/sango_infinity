using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 兵种适应等级配置（Ｃ / Ｂ / Ａ / Ｓ / Ｓ１ …），
    /// 一条配置同时描述"升到下一级所需经验"和"该等级的战斗适应力加成"。
    /// </summary>
    public class AbilityLevelType : SangoObject
    {
        /// <summary>
        /// 从当前等级升到下一级所需的兵种适性经验。
        /// </summary>
        public int exp;

        /// <summary>
        /// 该等级的攻击 / 防御适应力加成（百分比，Ａ 级 = 100 为基准）。
        ///
        /// 【原版出处】原版把适性系数硬编码在 <c>s11_scen_obj_unit.cpp:995</c>：
        /// <c>float tekisei_coef = (ability-&gt;tekisei[...] + 7) * 0.1f;</c>
        /// 且 <c>tekisei_t</c> 只有 Ｃ(0) / Ｂ(1) / Ａ(2) / Ｓ(3) 四级，即 70 / 80 / 90 / 100。
        /// 本项目把适性扩展到 Ｓ１~Ｓ４ 共 8 级，并把基准定为 Ａ = 100，
        /// 故不能用"等级+7"的线性公式，改为按等级可配置；
        /// 数值口径沿用原 <c>ScenarioVariables.troops_adaptation_level_boost</c>（Ｃ80 / Ｂ90 / Ａ100 / Ｓ110 / ＳＳ120）。
        ///
        /// 由 <see cref="Troop.TroopsLevelBoost"/> 按等级下标取值；
        /// 等级下标越界时回退到最后一个等级（见 <see cref="ScenarioCommonData.GetAbilityLevelType"/>）。
        /// 默认 100 表示不加成，避免旧数据未配置该字段时把部队攻防算成 0。
        /// </summary>
        public int troopsAdaptationLevelBoost = 100;
    }
}
