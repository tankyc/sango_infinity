/*
 * 文件名：TroopBattleEffectsExt.cs
 * 描述：特技 / 科技的"战斗结算"类扩展效果——暴击系数、攻城伤害、建筑内兵力伤害
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【这三条效果此前为什么无人可用】
 *   OnTroopCalculateSkillCriticalFactor / OnSkillDamageBuildingDurability /
 *   OnSkillDamageBuildingTroops 三个 hook 一直有真实抛出点，但 ActionBase.Init 里
 *   没有任何 Action 订阅它们，等于"引擎留了接口、玩法没接上"。
 *   本文件把这三个接口补满。
 *
 * 【装配兼容性】
 *   基类选 TroopTroopActionBase（继承 TroopActionBase），它同时持有
 *   Troop（特技装配：sangoObjects[0] 是部队）与 Force（科技装配：sangoObjects[0] 是势力）。
 *   判定时按"谁非空就用谁"，因此同一份效果配置既能写进 Features.json 也能写进 Techniques.json。
 *   —— 这一点很关键：ForceActionBase 系列在特技装配时 Force 恒为 null，
 *      若照抄 TroopAddAttack 的写法，特技会静默失效（现有代码里已有先例）。
 *
 * 【参数】
 *   value     百分比修正（100 = 不变，150 = 提高 50%）
 *   kinds     兵种类型过滤，null 表示不限
 *   isNormal  -1 不限 / 0 仅战法 / 1 仅一般攻击
 *   condition 额外条件（TroopSkillConditionDatabase 口径）
 */

using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 暴击伤害系数修正（value 为百分比，100 = 不变）。
    ///
    /// 接入事件：OnTroopCalculateSkillCriticalFactor。
    /// 该 hook 的传入值默认是剧本变量 skillCriticalFactor（通常 150），
    /// 且在"必爆"与"常规暴击成功"两条路径上都会抛出，因此两条路径都受本效果影响。
    ///
    /// 典型用法：
    ///   · 特技「猛者」：value = 120，持特技部队的暴击伤害 ×1.2
    ///   · 科技「兵法书」：value = 110，全势力部队暴击伤害 ×1.1
    /// </summary>
    public class TroopSkillCriticalFactor : TroopTroopActionBase
    {
        /// <summary>初始化：订阅暴击系数计算</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（部队或势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopCalculateSkillCriticalFactor += OnCalculateSkillCriticalFactor;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCalculateSkillCriticalFactor -= OnCalculateSkillCriticalFactor;
        }

        /// <summary>暴击系数计算回调</summary>
        /// <param name="troop">施法部队</param>
        /// <param name="skill">战法实例</param>
        /// <param name="spellCell">施法目标格（可能为 null）</param>
        /// <param name="overrideData">系数覆盖数据（初始为剧本默认暴击系数）</param>
        void OnCalculateSkillCriticalFactor(Troop troop, SkillInstance skill, Cell spellCell, OverrideData<int> overrideData)
        {
            if (!CheckOwner(troop)) return;
            if (!CheckIsNormalSkill(skill, isNormal)) return;
            if (kinds != null && troop.TroopType != null && !kinds.Contains(troop.TroopType.kind)) return;

            // 只有配置了 condition 时才需要目标格；spellCell 为空时跳过条件而不是空引用
            if (condition != null && spellCell != null
                && !condition.Check(new TroopActionConditionDatabase(skill, spellCell)))
                return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }

        /// <summary>
        /// 归属校验：势力装配看势力、部队装配看部队本体。
        /// </summary>
        /// <param name="troop">待判定的部队</param>
        /// <returns>是否受本效果作用</returns>
        internal bool CheckOwner(Troop troop)
        {
            if (troop == null) return false;
            if (Troop != null) return Troop == troop;
            if (Force != null) return troop.BelongForce == Force;
            return false;
        }
    }

    /// <summary>
    /// 战法对建筑 / 城池的耐久伤害修正（value 为百分比）。
    ///
    /// 接入事件：OnSkillDamageBuildingDurability。
    /// 传入值是"基础耐久伤害 × 暴击系数"，本效果在其上再乘一次百分比，
    /// 因此与暴击效果叠加不会互相覆盖。
    ///
    /// 典型用法：特技「攻城」value = 150；科技「投石车」value = 130。
    /// </summary>
    public class TroopSkillDamageBuildingDurability : TroopTroopActionBase
    {
        /// <summary>初始化：订阅耐久伤害计算</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnSkillDamageBuildingDurability += OnDamageBuildingDurability;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnSkillDamageBuildingDurability -= OnDamageBuildingDurability;
        }

        /// <summary>耐久伤害计算回调</summary>
        /// <param name="skill">战法实例（master 即施法部队）</param>
        /// <param name="building">被攻击的建筑 / 城池</param>
        /// <param name="overrideData">伤害覆盖数据</param>
        void OnDamageBuildingDurability(SkillInstance skill, BuildingBase building, OverrideData<int> overrideData)
        {
            Troop atker = skill != null ? skill.master : null;
            if (!CheckOwnerInternal(atker)) return;
            if (building == null) return;

            // 只对敌方建筑生效：给自己人拆自家城墙显然不是设计意图
            if (atker != null && building.BelongForce == atker.BelongForce) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }

        /// <summary>归属校验（复用一个实现，避免两份逻辑分叉）</summary>
        /// <param name="troop">施法部队</param>
        /// <returns>是否受本效果作用</returns>
        bool CheckOwnerInternal(Troop troop)
        {
            if (troop == null) return false;
            if (Troop != null) return Troop == troop;
            if (Force != null) return troop.BelongForce == Force;
            return false;
        }
    }

    /// <summary>
    /// 战法对"城内 / 建筑内兵力"的伤害修正（value 为百分比）。
    ///
    /// 接入事件：OnSkillDamageBuildingTroops。
    /// 与耐久伤害是两条独立结算：城里有人时先扣兵力，兵力清空后才轮到耐久，
    /// 因此"只加强兵力伤害"可以做出"破军而不毁城"的效果（如「突袭」）。
    /// </summary>
    public class TroopSkillDamageBuildingTroops : TroopTroopActionBase
    {
        /// <summary>初始化：订阅城内兵力伤害计算</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnSkillDamageBuildingTroops += OnDamageBuildingTroops;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnSkillDamageBuildingTroops -= OnDamageBuildingTroops;
        }

        /// <summary>城内兵力伤害计算回调</summary>
        /// <param name="skill">战法实例</param>
        /// <param name="building">被攻击的建筑 / 城池</param>
        /// <param name="overrideData">伤害覆盖数据</param>
        void OnDamageBuildingTroops(SkillInstance skill, BuildingBase building, OverrideData<int> overrideData)
        {
            Troop atker = skill != null ? skill.master : null;
            if (atker == null) return;
            if (Troop != null && Troop != atker) return;
            if (Force != null && atker.BelongForce != Force) return;
            if (building == null) return;
            if (building.BelongForce == atker.BelongForce) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }
}
