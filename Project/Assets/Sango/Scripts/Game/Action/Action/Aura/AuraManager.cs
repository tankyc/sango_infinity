using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sango.Core.Action
{
    /// <summary>
    /// 光环贡献登记表。
    ///
    /// 记录每支部队当前受到哪些光环影响，并按叠加规则（取最大 / 累加）汇总成实际加成值，
    /// 再把"增量"写进部队的 extraAttack / extraDefence。
    ///
    /// 为什么用登记表而不是直接 += ：
    /// 光环是位置相关的，部队移动、光环源移动、光环源被拆除都会让"谁该吃多少"变化，
    /// 如果直接加减属性，重叠区域里就会出现"撤掉强光环、弱光环的加成一并消失"（原先
    /// BuildingImproveBase 的老问题）。登记表在每次变化时只做一次重新汇总，天然支持回退。
    ///
    /// 用 ConditionalWeakTable 挂在部队对象上：部队被回收时条目自动释放，不会跨剧本泄漏。
    /// </summary>
    public static class AuraManager
    {
        /// <summary>
        /// 单个属性上的光环汇总槽
        /// </summary>
        class AuraBucket
        {
            /// <summary>当前作用于该部队的光环实例</summary>
            public readonly List<AuraActionBase> items = new List<AuraActionBase>();

            /// <summary>已经写进部队属性的加成值，用于计算增量</summary>
            public int applied;
        }

        /// <summary>部队 -> (光环属性 -> 汇总槽)</summary>
        static readonly ConditionalWeakTable<Troop, Dictionary<int, AuraBucket>> table
            = new ConditionalWeakTable<Troop, Dictionary<int, AuraBucket>>();

        /// <summary>
        /// 取得（或创建）指定部队某个光环属性的汇总槽
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <param name="auraType">光环属性（0 攻击 / 1 防御）</param>
        /// <returns>汇总槽</returns>
        static AuraBucket GetBucket(Troop troop, int auraType)
        {
            Dictionary<int, AuraBucket> map = table.GetOrCreateValue(troop);
            if (!map.TryGetValue(auraType, out AuraBucket bucket))
            {
                bucket = new AuraBucket();
                map[auraType] = bucket;
            }
            return bucket;
        }

        /// <summary>
        /// 登记一个光环对本部队的作用
        /// </summary>
        /// <param name="troop">被作用的部队</param>
        /// <param name="aura">光环实例</param>
        public static void Add(Troop troop, AuraActionBase aura)
        {
            if (troop == null || aura == null)
                return;

            AuraBucket bucket = GetBucket(troop, aura.AuraType);
            if (!bucket.items.Contains(aura))
                bucket.items.Add(aura);

            Recount(troop, aura.AuraType);
        }

        /// <summary>
        /// 注销一个光环对本部队的作用，并回退到剩余光环的汇总值
        /// </summary>
        /// <param name="troop">被作用的部队</param>
        /// <param name="aura">光环实例</param>
        public static void Remove(Troop troop, AuraActionBase aura)
        {
            if (troop == null || aura == null)
                return;

            AuraBucket bucket = GetBucket(troop, aura.AuraType);
            bucket.items.Remove(aura);
            bucket.items.RemoveAll(x => x == null || !x.IsActive);

            Recount(troop, aura.AuraType);
        }

        /// <summary>
        /// 重新汇总该部队在某个属性上的光环加成，并把差值写进部队属性
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <param name="auraType">光环属性（0 攻击 / 1 防御）</param>
        public static void Recount(Troop troop, int auraType)
        {
            if (troop == null)
                return;

            AuraBucket bucket = GetBucket(troop, auraType);
            bucket.items.RemoveAll(x => x == null || !x.IsActive);

            // 叠加规则：累加型全部相加；取最大型只保留最大的那一个；两者可以共存
            int sumPart = 0;
            int maxPart = 0;
            bool hasMax = false;
            for (int i = 0; i < bucket.items.Count; i++)
            {
                AuraActionBase aura = bucket.items[i];
                if (aura.StackMode == AuraActionBase.STACK_SUM)
                {
                    sumPart += aura.Value;
                }
                else
                {
                    if (!hasMax || aura.Value > maxPart)
                    {
                        maxPart = aura.Value;
                        hasMax = true;
                    }
                }
            }

            int finalValue = sumPart + (hasMax ? maxPart : 0);

            // 【覆写型效果】直接写部队字段，不走 delta。
            //
            // 原因一：这些字段可能在 CalculateAttribute / 外部逻辑里被复位，
            //         bucket.applied 仍是旧值，delta==0 会让字段永远停在旧值。
            // 原因二：ZOC / 移动力 / 无视火在寻路里逐格读取，每次都走 GetValue
            //         （弱表 + 字典 + 重新汇总）代价太高，必须在光环变化时缓存到字段。
            switch (auraType)
            {
                case AuraActionBase.AURA_ASSIST:
                    troop.auraAssistChance = finalValue;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_IGNORE_ZOC:
                    troop.auraIgnoreZOC = finalValue > 0;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_IGNORE_FIRE:
                    troop.auraIgnoreFire = finalValue > 0;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_FIRE_IMMUNE:
                    troop.auraIgnoreFireDamage = finalValue > 0;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_MOVE_LAND:
                    troop.auraMoveLand = finalValue;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_MOVE_WATER:
                    troop.auraMoveWater = finalValue;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_MAX_MORALE:
                    troop.auraMaxMorale = finalValue;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_SKILL_ATTACK_RANGE:
                    troop.auraSkillAttackRange = finalValue;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_WOUNDED_RATE:
                    troop.auraWoundedRate = finalValue;
                    bucket.applied = finalValue;
                    return;
                case AuraActionBase.AURA_REFLECT_DAMAGE:
                    troop.auraReflectDamage = finalValue;
                    bucket.applied = finalValue;
                    return;
            }

            int delta = finalValue - bucket.applied;
            if (delta == 0)
                return;

            bucket.applied = finalValue;

            // 属性落地：只有攻防写常驻字段（不会被 CalculateAttribute 重置）；
            // 会心 / 暴伤 / 计策 / 识破 / 气力只记录在 applied 里，由 GetValue 读取。
            if (auraType == AuraActionBase.AURA_ATTACK)
                troop.extraAttack += delta;
            else if (auraType == AuraActionBase.AURA_DEFENCE)
                troop.extraDefence += delta;
        }

        /// <summary>
        /// 读取一支部队当前受光环影响的某项数值（攻防以外的效果用）。
        ///
        /// 【口径】每次读取都先剔除失效光环再汇总，保证源部队溃灭 / 建筑拆除后立即回退。
        /// 攻防（0/1）请直接读 Troop.Attack / Defence，不走本接口。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="auraType">效果类型（见 AuraActionBase.AURA_*）</param>
        /// <returns>当前汇总值；没有光环作用时返回 0</returns>
        public static int GetValue(Troop troop, int auraType)
        {
            if (troop == null)
                return 0;
            if (!table.TryGetValue(troop, out Dictionary<int, AuraBucket> map))
                return 0;
            if (!map.TryGetValue(auraType, out AuraBucket bucket))
                return 0;

            Recount(troop, auraType);
            return bucket.applied;
        }

        /// <summary>
        /// 全量刷新所有部队的光环缓存字段。
        ///
        /// 缓存字段（ZOC / 火 / 移动力 / 气力上限 / 战法范围 / 伤兵率 / 反伤等）不存档，
        /// 读档 / 新建部队后如果光环源的补挂顺序晚于目标部队，字段可能仍是默认值。
        /// 在 Scenario.Init 末尾统一刷一遍，保证所有部队的缓存与当前覆盖关系一致。
        /// </summary>
        public static void RefreshAllCaches(Scenario scenario)
        {
            if (scenario == null || scenario.troopsSet == null)
                return;

            scenario.troopsSet.ForEach(t =>
            {
                if (t == null) return;
                // GetValue 对覆写型会触发 Recount 并写字段
                GetValue(t, AuraActionBase.AURA_IGNORE_ZOC);
                GetValue(t, AuraActionBase.AURA_IGNORE_FIRE);
                GetValue(t, AuraActionBase.AURA_FIRE_IMMUNE);
                GetValue(t, AuraActionBase.AURA_MOVE_LAND);
                GetValue(t, AuraActionBase.AURA_MOVE_WATER);
                GetValue(t, AuraActionBase.AURA_MAX_MORALE);
                GetValue(t, AuraActionBase.AURA_SKILL_ATTACK_RANGE);
                GetValue(t, AuraActionBase.AURA_WOUNDED_RATE);
                GetValue(t, AuraActionBase.AURA_REFLECT_DAMAGE);
                GetValue(t, AuraActionBase.AURA_ASSIST);
            });
        }
    }
}
