namespace Sango.Core
{
    /// <summary>
    /// 成对 / 组合特技的判定入口。
    ///
    /// <para>
    /// 所有"两人配合"的条件（主特技、对方特技、指定武将、同队/跨队、同势力、距离）
    /// 收敛到这里，避免每个指令各写一遍。
    /// </para>
    /// <para>
    /// 【性能】只做标量比较 + 一次格子距离；不做全量武将遍历，
    /// 候选列表由调用方（当前部队 / 城的武将）提供。
    /// </para>
    /// </summary>
    public static class FeaturePairSystem
    {
        /// <summary>
        /// 判断一对武将是否满足某条成对特技配置。
        /// </summary>
        /// <param name="cfg">成对配置</param>
        /// <param name="a">发起方</param>
        /// <param name="b">配合方</param>
        /// <returns>满足返回 true</returns>
        public static bool IsValid(FeaturePairConfig cfg, Person a, Person b)
        {
            if (cfg == null || a == null || b == null || a == b)
                return false;
            if (a.IsDead || b.IsDead)
                return false;

            if (cfg.featureId > 0 && !a.HasFeature(cfg.featureId))
                return false;

            if (cfg.partnerFeatureId > 0 && !b.HasFeature(cfg.partnerFeatureId))
                return false;

            if (cfg.partnerPersonIds != null && cfg.partnerPersonIds.Length > 0
                && !Contains(cfg.partnerPersonIds, b.Id))
                return false;

            Troop ta = a.mBelongTroop;
            Troop tb = b.mBelongTroop;

            if (cfg.requireSameTroop == 1 && ta != tb)
                return false;

            if (cfg.requireSameForce == 1)
            {
                Force fa = a.BelongForce;
                Force fb = b.BelongForce;
                if (fa == null || fa != fb)
                    return false;
            }

            if (cfg.maxDistance > 0)
            {
                if (ta == null || tb == null || ta.cell == null || tb.cell == null)
                    return false;
                if (ta.cell.Distance(tb.cell) > cfg.maxDistance)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 在候选列表里找第一个满足配置的配合方。
        /// </summary>
        /// <param name="cfg">成对配置</param>
        /// <param name="a">发起方</param>
        /// <param name="candidates">候选武将（通常为本队 / 同城武将）</param>
        /// <returns>配合方；找不到返回 null</returns>
        public static Person FindPartner(FeaturePairConfig cfg, Person a, System.Collections.Generic.List<Person> candidates)
        {
            if (cfg == null || a == null || candidates == null)
                return null;

            for (int i = 0; i < candidates.Count; i++)
            {
                Person b = candidates[i];
                if (b == a)
                    continue;
                if (IsValid(cfg, a, b))
                    return b;
            }
            return null;
        }

        static bool Contains(int[] values, int target)
        {
            for (int i = 0; i < values.Length; i++)
                if (values[i] == target)
                    return true;
            return false;
        }
    }
}
