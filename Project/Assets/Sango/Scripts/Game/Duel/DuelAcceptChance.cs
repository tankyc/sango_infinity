/*
 * 文件名：DuelAcceptChance.cs
 * 描述：单挑应战概率。对应需求"按照性格和能力给一个应战概率"。
 *
 * 设计要点：
 *   · 数据来源：
 *       性格   Person.mPersonality.duelAcceptAdd（单挑应战倾向，专有参数）
 *       能力   Person.Strength（武力）
 *       状态   Person.stamina（体力 0~100）、Person.injury（伤病 0~3）
 *       兵力   Troop.troops 的双方比值
 *   · 兵力项故意做成"兵力越多越不愿应战"：单挑是以小博大的手段，
 *     优势方犯不着冒险。该项**封顶 ±10 个百分点**，避免兵力差压过性格与武力。
 *   · 注意：兵力项是本项目自定的补充规则，并非三国志11原版判定（原版主要是武力+性格）。
 *
 * 所有数值都抽成常量，便于后续调平衡。
 */

using UnityEngine;

namespace Sango.Core.Duel
{
    /// <summary>单挑应战概率</summary>
    public static class DuelAcceptChance
    {
        #region 可调常量

        /// <summary>基础接受率（百分点）</summary>
        public const int BaseChance = 50;

        /// <summary>武力差每 1 点的影响（百分点）</summary>
        public const float StrengthWeight = 1.5f;

        /// <summary>兵力项最大影响（百分点，正负都封顶到这个值）</summary>
        public const float TroopMaxInfluence = 10f;

        /// <summary>兵力比每偏离 1 倍的影响（百分点）</summary>
        public const float TroopRatioWeight = 20f;

        /// <summary>每级伤病扣减（百分点）</summary>
        public const int InjuryPenalty = 12;

        /// <summary>接受率下限（永远留一点意外）</summary>
        public const int MinChance = 5;

        /// <summary>接受率上限（永远不封死）</summary>
        public const int MaxChance = 95;

        /// <summary>
        /// 【临时 · 测试用】为 true 时被挑战方一律应战（概率恒 100），方便反复验证单挑流程。
        /// 不需要时改成 false，或连同 Calc 里那一行判断一起删掉，即恢复正常的性格/能力/兵力判定。
        /// </summary>
        public const bool DebugAlwaysAccept = false;

        #endregion

        /// <summary>
        /// 计算应战概率。
        /// </summary>
        /// <param name="target">应战方主将</param>
        /// <param name="challenger">挑战方主将</param>
        /// <param name="targetTroops">应战方兵力</param>
        /// <param name="challengerTroops">挑战方兵力</param>
        /// <returns>0~100 的接受概率（实际被夹在 MinChance ~ MaxChance 之间）</returns>
        public static int Calc(Person target, Person challenger, int targetTroops, int challengerTroops)
        {
            // 临时测试用：无条件应战（测完删掉这一行即可）
            if (DebugAlwaysAccept) return 100;

            if (target == null) return MinChance;
            if (challenger == null) return MaxChance;

            int chance = BaseChance;

            // 1) 性格：单挑应战倾向（Personality.duelAcceptAdd）
            //    正值更愿应战（莽撞 +25 / 刚胆 +10），负值更回避（胆小 −20）
            if (target.mPersonality != null)
                chance += target.mPersonality.duelAcceptAdd;

            // 2) 能力：武力差，高打低更敢接
            chance += Mathf.RoundToInt((target.Strength - challenger.Strength) * StrengthWeight);

            // 3) 体力：满值不修正，残血怯战
            chance += (target.stamina - 100) / 2;

            // 4) 伤病：带伤怯战
            chance -= Mathf.Max(0, target.injury) * InjuryPenalty;

            // 5) 兵力：己方越多越不愿应战；封顶 ±TroopMaxInfluence
            float ratio = (float)targetTroops / Mathf.Max(1, challengerTroops);
            float troopTerm = Mathf.Clamp(
                (ratio - 1f) * TroopRatioWeight, -TroopMaxInfluence, TroopMaxInfluence);
            chance -= Mathf.RoundToInt(troopTerm);

            return Mathf.Clamp(chance, MinChance, MaxChance);
        }

        /// <summary>按概率掷骰，判断应战方是否接受</summary>
        public static bool Roll(Person target, Person challenger, int targetTroops, int challengerTroops)
        {
            return DuelRandom.Chance(Calc(target, challenger, targetTroops, challengerTroops));
        }

        /// <summary>按部队掷骰（自动取双方主将与兵力）</summary>
        public static bool Roll(Troop target, Troop challenger)
        {
            if (target == null || challenger == null) return false;
            return Roll(target.Leader, challenger.Leader, target.troops, challenger.troops);
        }
    }
}
