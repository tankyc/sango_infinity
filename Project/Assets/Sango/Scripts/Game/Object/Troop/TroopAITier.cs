/*
 * 文件名：TroopAITier.cs
 * 描述：部队战场态势档位（Tier）与分级策略快照（AIPolicy）。
 *       态势档位由"局部敌我兵力对比"得出，用于让部队根据战场情况采取不同的分级策略：
 *       进攻倾向、反击容忍度、撤退概率、行动择优池大小，都由当前档位决定。
 */

namespace Sango.Core
{
    /// <summary>
    /// 部队分级策略快照：由**态势档位**（<see cref="TroopTierWeights"/>）与
    /// **部队角色**（<see cref="TroopRoleWeights"/>）合成，统一驱动技能评分与行动选择。
    ///
    /// 这是一次性的值类型快照，每回合评估一次即可复用，避免在循环中反复查询配置。
    /// </summary>
    public struct AIPolicy
    {
        /// <summary>攻击收益倍率（%），100 = 不修正</summary>
        public int attackScale;

        /// <summary>反击惩罚倍率（%），100 = 不修正</summary>
        public int counterPenaltyScale;

        /// <summary>
        /// 行动择优池大小：
        /// 1 = 直接取评分最高的行动（最聪明）；
        /// 0 或负数 = 不择优，退化为全局加权随机。
        /// </summary>
        public int bestN;

        /// <summary>撤退概率修正（%），由领队性格给出，直接加到态势档位的撤退概率上</summary>
        public int retreatBonus;

        /// <summary>默认策略（不做任何修正，行为等价于改造前）。</summary>
        public static AIPolicy Default
        {
            get { return new AIPolicy { attackScale = 100, counterPenaltyScale = 100, bestN = 0, retreatBonus = 0 }; }
        }

        /// <summary>
        /// 由态势档位、角色权重与**领队性格**三层合成策略。任一参数为 null 时跳过对应修正。
        ///
        /// 合成顺序：tier × role × PersonalityId → 最终钳制。
        /// </summary>
        /// <param name="tier">态势档位权重（可为 null）</param>
        /// <param name="role">角色权重（可为 null）</param>
        /// <param name="PersonalityId">领队性格（可为 null，表示不做性格修正）</param>
        /// <returns>合成后的策略</returns>
        public static AIPolicy Combine(TroopTierWeights tier, TroopRoleWeights role, Personality PersonalityId)
        {
            AIConfig cfg = AIConfig.Instance;
            AIPolicy policy = Default;

            if (tier != null)
            {
                // 倍率累乘，且用 long 中转防止极端配置下的整数溢出
                policy.attackScale = (int)((long)policy.attackScale * ClampPositive(tier.attackScale) / 100);
                policy.counterPenaltyScale = (int)((long)policy.counterPenaltyScale * ClampPositive(tier.counterPenaltyScale) / 100);
                policy.bestN = tier.bestN;
            }

            if (role != null)
            {
                policy.attackScale = (int)((long)policy.attackScale * ClampPositive(role.attackScale) / 100);
                policy.counterPenaltyScale = (int)((long)policy.counterPenaltyScale * ClampPositive(role.counterPenaltyScale) / 100);
            }

            // ---------- 第三层：领队性格 ----------
            if (cfg.useLeaderPersonality && PersonalityId != null)
            {
                // 性格层自身先钳制到 [leaderScaleMin, leaderScaleMax]，防止极端数值拉爆
                int leaderAttack = ClampScale(PersonalityId.troopAttackScale, cfg.leaderScaleMin, cfg.leaderScaleMax);
                int leaderCounter = ClampScale(PersonalityId.troopCounterScale, cfg.leaderScaleMin, cfg.leaderScaleMax);

                policy.attackScale = (int)((long)policy.attackScale * leaderAttack / 100);
                policy.counterPenaltyScale = (int)((long)policy.counterPenaltyScale * leaderCounter / 100);

                policy.retreatBonus = PersonalityId.troopRetreatAdd;

                // 性格对择优池的修正：好战者更果断（池更小），怯战者更摇摆（池更大）
                policy.bestN += PersonalityId.troopBestNAdd;
            }

            // ---------- 最终钳制：防止三层连乘失控 ----------
            policy.attackScale = ClampScale(policy.attackScale, cfg.finalAttackScaleMin, cfg.finalAttackScaleMax);
            policy.counterPenaltyScale = ClampScale(policy.counterPenaltyScale, cfg.finalCounterScaleMin, cfg.finalCounterScaleMax);

            // 择优池：0 表示不择优，故只在 > 0 时钳制
            if (policy.bestN > 0)
            {
                if (policy.bestN < cfg.bestNMin) policy.bestN = cfg.bestNMin;
                if (policy.bestN > cfg.bestNMax) policy.bestN = cfg.bestNMax;
            }

            return policy;
        }

        /// <summary>
        /// 按"现状 + 任务推导角色 + 领队性格"合成策略（常用重载）。
        /// </summary>
        /// <param name="tier">态势档位权重</param>
        /// <param name="role">角色权重</param>
        /// <param name="PersonalityId">领队性格</param>
        /// <returns>合成后的策略</returns>
        public static AIPolicy Combine(TroopTierWeights tier, TroopRoleWeights role)
        {
            return Combine(tier, role, null);
        }

        /// <summary>
        /// 把倍率钳制到指定区间；原始值 ≤ 0 时视为不修正（100）。
        /// </summary>
        /// <param name="scale">原始倍率</param>
        /// <param name="min">下限</param>
        /// <param name="max">上限</param>
        /// <returns>钳制后的倍率</returns>
        static int ClampScale(int scale, int min, int max)
        {
            int value = scale > 0 ? scale : 100;
            if (value < min) value = min;
            if (value > max) value = max;
            return value;
        }

        /// <summary>
        /// 把倍率钳制到 ≥ 1，避免配置为 0 或负数时把收益彻底抹平。
        /// </summary>
        /// <param name="scale">原始倍率</param>
        /// <returns>钳制后的倍率</returns>
        static int ClampPositive(int scale)
        {
            return scale > 0 ? scale : 1;
        }
    }


    /// <summary>
    /// 部队战场态势档位。由局部敌我兵力对比得出（见 BattleSituation.EvaluateBalance），
    /// 每个档位对应一组可配置权重 <see cref="TroopTierWeights"/>，
    /// 从而让部队在不同战场形势下表现出不同的行为倾向。
    /// </summary>
    public enum TroopBattleTier : int
    {
        /// <summary>危局：我方兵力占比极低，应保存实力、脱离接触</summary>
        Critical = 0,
        /// <summary>劣势：敌方占优，以收缩防守为主</summary>
        Disadvantaged = 1,
        /// <summary>均势：双方接近，谨慎试探</summary>
        Even = 2,
        /// <summary>优势：我方占优，可主动进攻</summary>
        Advantaged = 3,
        /// <summary>碾压：我方压倒性优势，可强攻、可追击</summary>
        Decisive = 4,
    }

    /// <summary>
    /// 单个态势档位的权重集合。
    ///
    /// 所有倍率均为百分数，<c>100</c> 表示"不修正"，便于在 JSON 中直观调参。
    /// 可通过 <c>Data/Common/AIConfig.json</c> 的 <c>tierCritical</c> /
    /// <c>tierDisadvantaged</c> / <c>tierEven</c> / <c>tierAdvantaged</c> /
    /// <c>tierDecisive</c> 节点做部分覆盖。
    /// </summary>
    public class TroopTierWeights
    {
        /// <summary>攻击收益倍率（%）：越高越倾向主动出手</summary>
        public int attackScale = 100;

        /// <summary>反击惩罚倍率（%）：越高越规避"贴脸打高反击单位"</summary>
        public int counterPenaltyScale = 100;

        /// <summary>每回合的撤退触发概率（%）；0 表示不因态势撤退</summary>
        public int retreatChance = 0;

        /// <summary>
        /// 行动择优池大小：
        /// 1 = 直接取评分最高的行动（最聪明）；
        /// 0 或负数 = 不做择优，退化为全局加权随机（保留最大随机性）。
        /// </summary>
        public int bestN = 3;
    }
}
