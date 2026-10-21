/*
 * 文件名：SkillEffectsExt.cs
 * 描述：战法效果（SkillEffect）扩展——士气变更、吸血、破坏耐久、杀伤城内兵力
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【战法与特技/科技是两套体系，不要混用】
 *   特技 Feature 与科技 Technique 走 ActionBase（订阅 GameEvent 的计算 hook）；
 *   战法 Skill 走 SkillEffect（在战法命中后逐个目标格调用 Action）。
 *   因此"给战法加效果"必须继承 SkillEffect 并注册到 SkillEffect.CreateMap，
 *   注册到 ActionBase.CreateMap 是无效的（反之亦然）。
 *
 * 【现有战法效果的共同形态】
 *   probability : 触发概率，万分比（10000 = 必定触发）
 *   condition   : 可选条件，口径为 SkillEffectConditionDatabase
 *   values      : 数值集合
 *   weight      : 与 values 等长的权重，按权重随机取一个
 *   本文件沿用同一套参数，策划配置时不需要记第二套规则。
 *
 * 【目标格可能为空】
 *   战法对空格施放（如落石、火计）时 targetCell.troop 为空，
 *   因此伤害类效果必须显式区分"打部队"还是"打建筑"，不能只判 troop。
 */

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 目标部队士气变化。
    ///
    /// 参数：
    ///   probability 万分比
    ///   values[]    士气变化量（可负）
    ///   weight[]    权重
    ///   targetType  0 仅敌方 / 1 仅我方 / 2 不限（默认 2）
    ///   condition   可选条件
    ///
    /// 典型用法：「鼓舞」values=[20] targetType=1（给友军加气力）；
    ///          「骂声」values=[-15] targetType=0（削敌军气力）。
    /// </summary>
    public class ChangeMorale : SkillEffect
    {
        /// <summary>触发概率（万分比）</summary>
        int probability;

        /// <summary>取值集合</summary>
        int[] values;

        /// <summary>权重集合</summary>
        int[] weight;

        /// <summary>作用对象：0 敌方 / 1 我方 / 2 不限</summary>
        int targetType = 2;

        /// <summary>可选条件</summary>
        Condition condition;

        /// <summary>初始化</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="master">战法实例</param>
        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);

            probability = p.Value<int>("probability");
            targetType = p.Value<int?>("targetType") ?? 2;

            JArray array = p.Value<JArray>("values");
            if (array != null)
            {
                List<int> list = new List<int>(array.Count);
                for (int i = 0; i < array.Count; i++)
                    list.Add(array[i].Value<int>());
                values = list.ToArray();
            }

            array = p.Value<JArray>("weight");
            if (array != null)
            {
                List<int> list = new List<int>(array.Count);
                for (int i = 0; i < array.Count; i++)
                    list.Add(array[i].Value<int>());
                weight = list.ToArray();
            }

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null) condition.Init(conObj, master);
            }
        }

        /// <summary>对目标格执行</summary>
        /// <param name="targetCell">目标格</param>
        public override void Action(Cell targetCell)
        {
            if (targetCell == null || values == null || weight == null) return;

            Troop target = targetCell.troop;
            if (target == null) return;

            // 敌我判定：战法常常同时覆盖多格，不加这层判定会连友军一起削
            Troop self = master != null ? master.master : null;
            if (self != null && targetType != 2)
            {
                bool isEnemy = target.BelongForce != self.BelongForce;
                if (targetType == 0 && !isEnemy) return;
                if (targetType == 1 && isEnemy) return;
            }

            if (!GameRandom.Chance(probability, 10000)) return;

            if (condition != null && !condition.Check(new SkillEffectConditionDatabase(this, targetCell)))
                return;

            int index = GameRandom.RandomWeightIndex(weight);
            target.ChangeMorale(values[index]);
        }
    }

    /// <summary>
    /// 吸血：对目标造成兵力损失，并按比例补充自身兵力。
    ///
    /// 参数：
    ///   probability   万分比
    ///   values[]      对目标造成的兵力百分比（相对目标当前兵力，如 10 = 10%）
    ///   weight[]      权重
    ///   drainPercent  吸血比例（默认 50，即伤害的 50% 转化为自身补充）
    ///   maxDamage     单次伤害上限，防止对满编部队一刀回满（0 = 不限制）
    ///   condition     可选条件
    ///
    /// 注意：伤害与治疗都走 Troop.ChangeTroops，因此编制上限、伤亡统计、
    /// 击破判定都由引擎统一处理，不会绕过收尾流程。
    /// </summary>
    public class DrainTroops : SkillEffect
    {
        /// <summary>触发概率（万分比）</summary>
        int probability;

        /// <summary>目标兵力损失百分比</summary>
        int[] values;

        /// <summary>权重</summary>
        int[] weight;

        /// <summary>吸血比例（百分比）</summary>
        int drainPercent = 50;

        /// <summary>单次伤害上限（0 = 不限制）</summary>
        int maxDamage;

        /// <summary>可选条件</summary>
        Condition condition;

        /// <summary>初始化</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="master">战法实例</param>
        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);

            probability = p.Value<int>("probability");
            drainPercent = p.Value<int?>("drainPercent") ?? 50;
            maxDamage = p.Value<int?>("maxDamage") ?? 0;

            JArray array = p.Value<JArray>("values");
            if (array != null)
            {
                List<int> list = new List<int>(array.Count);
                for (int i = 0; i < array.Count; i++)
                    list.Add(array[i].Value<int>());
                values = list.ToArray();
            }

            array = p.Value<JArray>("weight");
            if (array != null)
            {
                List<int> list = new List<int>(array.Count);
                for (int i = 0; i < array.Count; i++)
                    list.Add(array[i].Value<int>());
                weight = list.ToArray();
            }

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null) condition.Init(conObj, master);
            }
        }

        /// <summary>对目标格执行</summary>
        /// <param name="targetCell">目标格</param>
        public override void Action(Cell targetCell)
        {
            if (targetCell == null || values == null || weight == null) return;

            Troop target = targetCell.troop;
            if (target == null || master == null || master.master == null) return;

            Troop self = master.master;
            // 吸血只对敌方生效，否则会出现"给自己人放血再吸回来"的荒诞循环
            if (target.BelongForce == self.BelongForce) return;
            if (target.IsAlive == false) return;

            if (!GameRandom.Chance(probability, 10000)) return;

            if (condition != null && !condition.Check(new SkillEffectConditionDatabase(this, targetCell)))
                return;

            int index = GameRandom.RandomWeightIndex(weight);
            int percent = values[index];
            if (percent <= 0) return;

            int damage = target.troops * percent / 100;
            if (maxDamage > 0 && damage > maxDamage) damage = maxDamage;
            if (damage <= 0) return;

            target.ChangeTroops(-damage, master, 0);

            int heal = damage * drainPercent / 100;
            if (heal > 0) self.ChangeTroops(heal, master, 0);
        }
    }

    /// <summary>
    /// 对建筑 / 城池直接造成耐久伤害（values 为绝对值）。
    ///
    /// 参数：
    ///   probability 万分比
    ///   values[]    耐久伤害（绝对值，不是百分比）
    ///   weight[]    权重
    ///   condition   可选条件
    ///
    /// 【为什么用绝对值】战法的基础耐久伤害已经由 skill.atkDurability 在常规结算里扣过一次，
    /// 这里做的是"额外附带的破坏"（如「崩城」附加 800 耐久伤害），
    /// 用百分比会让它在不同城防下波动过大，不如直接给固定值。
    /// </summary>
    public class DamageBuildingDurability : SkillEffect
    {
        /// <summary>触发概率（万分比）</summary>
        int probability;

        /// <summary>耐久伤害值</summary>
        int[] values;

        /// <summary>权重</summary>
        int[] weight;

        /// <summary>可选条件</summary>
        Condition condition;

        /// <summary>初始化</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="master">战法实例</param>
        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);

            probability = p.Value<int>("probability");

            JArray array = p.Value<JArray>("values");
            if (array != null)
            {
                List<int> list = new List<int>(array.Count);
                for (int i = 0; i < array.Count; i++)
                    list.Add(array[i].Value<int>());
                values = list.ToArray();
            }

            array = p.Value<JArray>("weight");
            if (array != null)
            {
                List<int> list = new List<int>(array.Count);
                for (int i = 0; i < array.Count; i++)
                    list.Add(array[i].Value<int>());
                weight = list.ToArray();
            }

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null) condition.Init(conObj, master);
            }
        }

        /// <summary>对目标格执行</summary>
        /// <param name="targetCell">目标格</param>
        public override void Action(Cell targetCell)
        {
            if (targetCell == null || values == null || weight == null) return;

            BuildingBase building = targetCell.building;
            if (building == null || master == null || master.master == null) return;

            // 不打自家城
            if (building.BelongForce == master.master.BelongForce) return;

            if (!GameRandom.Chance(probability, 10000)) return;

            if (condition != null && !condition.Check(new SkillEffectConditionDatabase(this, targetCell)))
                return;

            int index = GameRandom.RandomWeightIndex(weight);
            int damage = values[index];
            if (damage <= 0) return;

            building.ChangeDurability(-damage, master);
        }
    }

    /// <summary>
    /// 对城池 / 关港内驻扎兵力直接造成伤害（values 为绝对值）。
    ///
    /// 参数：
    ///   probability 万分比
    ///   values[]    兵力伤害（绝对值）
    ///   weight[]    权重
    ///   condition   可选条件
    ///
    /// 与 DamageBuildingDurability 的区别：这个扣的是"城内守军"，
    /// 守军清零后常规结算才会开始扣耐久，用于做"先破军、后破城"的战法。
    /// </summary>
    public class DamageCityTroops : SkillEffect
    {
        /// <summary>触发概率（万分比）</summary>
        int probability;

        /// <summary>兵力伤害值</summary>
        int[] values;

        /// <summary>权重</summary>
        int[] weight;

        /// <summary>可选条件</summary>
        Condition condition;

        /// <summary>初始化</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="master">战法实例</param>
        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);

            probability = p.Value<int>("probability");

            JArray array = p.Value<JArray>("values");
            if (array != null)
            {
                List<int> list = new List<int>(array.Count);
                for (int i = 0; i < array.Count; i++)
                    list.Add(array[i].Value<int>());
                values = list.ToArray();
            }

            array = p.Value<JArray>("weight");
            if (array != null)
            {
                List<int> list = new List<int>(array.Count);
                for (int i = 0; i < array.Count; i++)
                    list.Add(array[i].Value<int>());
                weight = list.ToArray();
            }

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null) condition.Init(conObj, master);
            }
        }

        /// <summary>对目标格执行</summary>
        /// <param name="targetCell">目标格</param>
        public override void Action(Cell targetCell)
        {
            if (targetCell == null || values == null || weight == null) return;

            City city = targetCell.building as City;
            if (city == null || master == null || master.master == null) return;

            if (city.BelongForce == master.master.BelongForce) return;

            if (!GameRandom.Chance(probability, 10000)) return;

            if (condition != null && !condition.Check(new SkillEffectConditionDatabase(this, targetCell)))
                return;

            int index = GameRandom.RandomWeightIndex(weight);
            int damage = values[index];
            if (damage <= 0) return;

            // 第三个参数为 true：显示飘字并走完整的伤亡统计
            city.ChangeTroops(-damage, master, true);
        }
    }
}
