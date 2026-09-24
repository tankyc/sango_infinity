/*
 * 文件名：PersonLoyaltyRules.cs
 * 描述：三国志11 原版的"换季掉忠"资格判定（谁一定不会掉忠诚）。
 *
 * 原版规则（在仕武将只有换季才可能掉忠；以下情况**必不掉忠**）：
 *   1. 所在城池里有武将持有特技「仁政」；
 *   2. 武将本人就是君主；
 *   3. 亲爱君主（"亲爱武将"名单里含君主）；
 *   4. 与君主是夫妻或义兄弟关系；
 *   5. 是君主的子女或父母；
 *   6. 与君主相性差不超过 25 且不厌恶君主，且（义理为普通或以上 或 野心为普通或以下）。
 * 反过来，**会进入掉忠计算**的条件是：
 *   · 与君主相性差 > 25，或
 *   · 厌恶君主，或
 *   · 义理低（容易背叛 / 无情义）且野心高。
 *
 * 工程内的字段对应：
 *   君主          → Force.mGovernor
 *   亲爱 / 厌恶    → Person.IsLike / IsHate
 *   配偶 / 义兄弟  → Person.IsSpouse / IsBrotherGroupmate（对称判定）
 *   子女 / 父母    → Person.IsParentchild（双向）
 *   相性          → Person.CompatibilityDistance
 *   义理          → Person.mArgumentation.kind（1 容易背叛 2 无情义 3 普通 4 情理坚定 5 不会背叛）
 *   野心          → 工程用性格的"叛乱风险" Personality.revoltRiskAdd 代表（莽撞 +10 = 高）
 *   仁政          → 特技 Id 97（Features.json），判定为"同一所属城里有人持有它"
 *
 * 原版【俘虏】规则（与被俘的原势力君主比较，**每月**都可能掉忠）：
 *   · 必不掉忠：1. 亲爱君主  2. 与君主是夫妻或义兄弟  3. 是君主的子女或父母（原版存疑，这里一并免掉）；
 *   · 相性差 ≥ 25 会额外多掉（原版描述"约 2~5 点"）；
 *   · 义理低（容易背叛 / 无情义）掉得更快（原版"加快 50% 以上"），义理高（情理坚定 / 不会背叛）掉得慢；
 *   · 掌握人心科技：本人有 2/3 概率不进入掉忠计算（走 GameEvent.OnForcePersonLoyaltyChangeProbability，
 *     数据里给该科技挂一个 value=33 的 ForcePersonLoyaltyChange 即可）。
 *
 * 说明：
 *   · 本类只判定"要不要进入掉忠计算"和"掉多少"，实际扣减见 Force.ForcePersonLoyaltyChange（换季）
 *     与 Force.ForceCaptiveLoyaltyChange（俘虏每月）；
 *   · 换季结算里仍会发 GameEvent.OnForcePersonLoyaltyChange，允许 Action 再次否决
 *     （如仁政的 CityPreventPersonLoyaltyLoss），两条路互不影响；
 *   · 阈值都放在本类的常量里，便于按策划口径调整。
 */

namespace Sango.Core
{
    /// <summary>原版换季掉忠的资格判定</summary>
    public static class PersonLoyaltyRules
    {
        /// <summary>相性差在此值以内才可能免掉忠（原版：25）</summary>
        public const int SafeCompatibilityDistance = 25;

        /// <summary>义理"低"的上限：1=容易背叛 / 2=无情义（3=普通起算"普通或以上"）</summary>
        public const int LowArgumentationKindMax = 2;

        /// <summary>野心"高"的下限：工程里用性格的叛乱风险表示野心，>0 视为高（莽撞 = +10）</summary>
        public const int HighRevoltRiskMin = 1;

        /// <summary>特技「仁政」的 Id（Features.json：#97）</summary>
        public const int RenZhengFeatureId = 97;

        #region 俘虏（每月掉忠）常量

        /// <summary>俘虏每月的基础下降量</summary>
        public const int CaptiveBaseLoss = 2;

        /// <summary>俘虏：与原君主相性差达到此值会额外多掉（原版 25）</summary>
        public const int CaptiveUnsafeCompatibilityDistance = 25;

        /// <summary>俘虏：相性差过大时额外多掉的量（原版描述为"约 2~5 点"，这里取 2，可调）</summary>
        public const int CaptiveCompatExtraLoss = 2;

        /// <summary>义理低（容易背叛 / 无情义）时的下降量倍数（百分比，原版"加快 50% 以上"）</summary>
        public const int CaptiveLowArgumentationScale = 150;

        /// <summary>义理高（情理坚定 / 不会背叛）时的下降量倍数（百分比，原版"下降较慢"）</summary>
        public const int CaptiveHighArgumentationScale = 50;

        /// <summary>义理"高"的下限：4=情理坚定 / 5=不会背叛</summary>
        public const int HighArgumentationKindMin = 4;

        #endregion

        /// <summary>
        /// 本次换季该武将是否会进入掉忠结算。
        /// </summary>
        /// <param name="force">所属势力</param>
        /// <param name="person">武将</param>
        /// <returns>true = 可能掉忠；false = 必不掉忠</returns>
        public static bool CanLoseLoyalty(Force force, Person person)
        {
            if (force == null || person == null) return false;

            // 1. 所在城里有「仁政」→ 该城武将（含出征者）本季不掉忠
            if (HasRenZhengInBelongCity(person)) return false;

            Person governor = force.mGovernor;
            // 没有君主可比对（异常数据）：按"会掉"处理
            if (governor == null) return true;

            // 2. 君主本人
            if (person == governor) return false;

            // 3. 亲爱君主
            if (person.IsLike(governor)) return false;

            // 4. 与君主是配偶 / 义兄弟（结义同组）
            if (person.IsSpouse(governor)) return false;
            if (person.IsBrotherGroupmate(governor)) return false;

            // 5. 是君主的子女或父母
            if (person.IsParentchild(governor)) return false;

            // 6. 相性差 ≤ 25 且不厌恶君主，且（义理普通以上 或 野心普通以下）
            if (person.CompatibilityDistance(governor) <= SafeCompatibilityDistance
                && !person.IsHate(governor)
                && (!IsLowArgumentation(person) || !IsHighAmbition(person)))
                return false;

            return true;
        }

        /// <summary>
        /// 与 person 同属一个城市的武将里，是否有人持有特技「仁政」。
        /// 原版口径：持有者本人带兵出征也有效，所以不能只看城市的人员名单，要全量遍历。
        /// </summary>
        public static bool HasRenZhengInBelongCity(Person person)
        {
            if (person == null || person.BelongCity == null) return false;

            Scenario scenario = Scenario.Cur;
            if (scenario == null) return false;

            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                Person other = scenario.personSet[i];
                if (other == null || !other.IsAlive) continue;
                if (other.BelongCity != person.BelongCity) continue;
                if (other.HasFeatrue(RenZhengFeatureId)) return true;
            }
            return false;
        }

        /// <summary>义理是否为"低"（容易背叛 / 无情义）</summary>
        public static bool IsLowArgumentation(Person person)
        {
            return person != null
                && person.mArgumentation != null
                && person.mArgumentation.kind <= LowArgumentationKindMax;
        }

        /// <summary>野心是否为"高"（用性格的叛乱风险代表：莽撞为高，胆小/冷静为低）</summary>
        public static bool IsHighAmbition(Person person)
        {
            return person != null
                && person.mPersonality != null
                && person.mPersonality.revoltRiskAdd >= HighRevoltRiskMin;
        }

        #region 俘虏（每月掉忠）

        /// <summary>
        /// 俘虏本月是否会进入掉忠计算。
        /// 原版俘虏版"必不掉忠"只有三条（与被俘的原势力君主比较）：
        /// 亲爱君主 / 与君主是夫妻或义兄弟 / 是君主的子女或父母。
        /// 注意：俘虏不受"仁政"与"相性差 ≤ 25"保护。
        /// </summary>
        /// <param name="captive">俘虏本人</param>
        /// <param name="governor">被俘势力的君主（原君主），可能已灭亡而为空</param>
        /// <returns>true = 可能掉忠；false = 必不掉忠</returns>
        public static bool CanCaptiveLoseLoyalty(Person captive, Person governor)
        {
            if (captive == null) return false;
            // 原势力已灭亡（没有君主可比对）：按"会掉"处理
            if (governor == null) return true;

            if (captive.IsLike(governor)) return false;
            if (captive.IsSpouse(governor)) return false;
            if (captive.IsBrotherGroupmate(governor)) return false;
            // 原版这一条标注"存疑"，这里按原版一并免掉
            if (captive.IsParentchild(governor)) return false;

            return true;
        }

        /// <summary>
        /// 俘虏本月要掉多少忠诚。
        /// 口径：基础 <see cref="CaptiveBaseLoss"/>，与原君主相性差 ≥
        /// <see cref="CaptiveUnsafeCompatibilityDistance"/> 时额外 +<see cref="CaptiveCompatExtraLoss"/>，
        /// 再按义理缩放（低 150% / 高 50%），最少 1 点。
        /// </summary>
        public static int CalcCaptiveLoyaltyLoss(Person captive, Person governor)
        {
            int loss = CaptiveBaseLoss;

            if (governor != null
                && captive.CompatibilityDistance(governor) >= CaptiveUnsafeCompatibilityDistance)
                loss += CaptiveCompatExtraLoss;

            int scale = 100;
            if (IsLowArgumentation(captive)) scale = CaptiveLowArgumentationScale;
            else if (IsHighArgumentation(captive)) scale = CaptiveHighArgumentationScale;

            loss = loss * scale / 100;
            return loss < 1 ? 1 : loss;
        }

        /// <summary>义理是否为"高"（情理坚定 / 不会背叛）</summary>
        public static bool IsHighArgumentation(Person person)
        {
            return person != null
                && person.mArgumentation != null
                && person.mArgumentation.kind >= HighArgumentationKindMin;
        }

        #endregion
    }
}
