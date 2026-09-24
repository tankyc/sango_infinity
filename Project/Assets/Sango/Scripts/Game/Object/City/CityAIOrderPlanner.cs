/*
 * 文件名：CityAIOrderPlanner.cs
 * 描述：城池 AI 命令编排器。根据城池态势为每条命令评分，叠加个性修正与紧急度覆盖，
 *       最终输出一份按优先级降序排列的命令列表。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 排序中间项：命令 + 最终得分。
    /// </summary>
    public class CityOrderEntry
    {
        /// <summary>命令元数据</summary>
        public CityCommandProfile profile;
        /// <summary>最终得分</summary>
        public int score;
    }

    /// <summary>
    /// 城池 AI 命令编排器。
    ///
    /// 流程：城池态势采集 → 类型/硬条件过滤 → 基础分 + 态势分 + 个性修正 + 紧急覆盖 → 降序排序。
    /// </summary>
    public static class CityAIOrderPlanner
    {
        static readonly List<CityOrderEntry> entries = new List<CityOrderEntry>(24);

        /// <summary>
        /// 根据城池态势生成有序的 AI 命令列表。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="allowIds">允许参与排序的命令集合（null 表示全部；用于区分工作制 / 经典内政 / 港关）</param>
        /// <returns>有序命令列表</returns>
        public static List<Func<City, Scenario, bool>> Plan(City city, Scenario scenario,
            HashSet<string> allowIds = null)
        {
            List<Func<City, Scenario, bool>> result = new List<Func<City, Scenario, bool>>(24);
            if (city == null || scenario == null)
                return result;

            CitySituation situation = CitySituation.Collect(city, scenario);
            entries.Clear();

            List<CityCommandProfile> all = CityCommandRegistry.Profiles;
            for (int i = 0; i < all.Count; i++)
            {
                CityCommandProfile p = all[i];
                if (p == null || p.action == null)
                    continue;
                // 允许集合过滤（null 表示全部）
                if (allowIds != null && !allowIds.Contains(p.id))
                    continue;
                // 城池类型过滤（都市 / 港口 / 关卡）
                if ((p.allowKinds & situation.kind) == 0)
                    continue;
                // 硬性条件过滤
                if (p.validRule != null && !p.validRule(situation))
                    continue;

                int score = p.basePriority;
                if (p.scoreRule != null)
                    score += p.scoreRule(situation);
                score += GetPersonalityBias(situation, p.id);
                score += GetOverrideBias(situation, p.id);

                entries.Add(new CityOrderEntry { profile = p, score = score });
            }

            // 降序排序；同分时按 id 排序，保证结果稳定可复现
            entries.Sort((a, b) =>
            {
                int c = b.score.CompareTo(a.score);
                if (c != 0)
                    return c;
                return string.CompareOrdinal(a.profile.id, b.profile.id);
            });

            for (int i = 0; i < entries.Count; i++)
                result.Add(entries[i].profile.action);

            return result;
        }

        /// <summary>
        /// 取得指定命令在当前态势下的最终得分（供调试 / 调优使用）。
        /// </summary>
        /// <param name="situation">态势快照</param>
        /// <param name="id">命令标识</param>
        /// <returns>最终得分；命令不适用时返回 int.MinValue</returns>
        public static int GetScore(CitySituation situation, string id)
        {
            List<CityCommandProfile> all = CityCommandRegistry.Profiles;
            for (int i = 0; i < all.Count; i++)
            {
                CityCommandProfile p = all[i];
                if (p == null || p.id != id)
                    continue;
                if ((p.allowKinds & situation.kind) == 0)
                    return int.MinValue;
                if (p.validRule != null && !p.validRule(situation))
                    return int.MinValue;

                int score = p.basePriority;
                if (p.scoreRule != null)
                    score += p.scoreRule(situation);
                score += GetPersonalityBias(situation, p.id);
                score += GetOverrideBias(situation, p.id);
                return score;
            }
            return int.MinValue;
        }

        /// <summary>
        /// 势力个性对命令优先级的修正（权重取自 <see cref="CityOrderWeights"/>）。
        /// </summary>
        /// <param name="s">态势快照</param>
        /// <param name="id">命令标识</param>
        /// <returns>修正分</returns>
        static int GetPersonalityBias(CitySituation s, string id)
        {
            CityOrderWeights w = AIConfig.Instance.cityOrder;
            switch (s.PersonalityId)
            {
                case ForceAI.AIPersonalityType.Aggressive:
                    if (id == "AIAttack" || id == "AIRecruitTroop")
                        return w.personalityAggressiveMilitaryBonus;
                    if (id == "AIIntrior")
                        return -w.personalityAggressiveInternalPenalty;
                    break;

                case ForceAI.AIPersonalityType.Defensive:
                    if (id == "AIAttack" || id == "AIReinforce")
                        return w.personalityDefensiveMilitaryBonus;
                    if (id == "AIIntrior")
                        return w.personalityDefensiveInternalBonus;
                    break;

                case ForceAI.AIPersonalityType.Economic:
                    if (id == "AIIntrior" || id == "AITradeFood" || id == "AITransfrom")
                        return w.personalityEconomicInternalBonus;
                    if (id == "AIAttack")
                        return -w.personalityEconomicMilitaryPenalty;
                    break;

                case ForceAI.AIPersonalityType.Diplomatic:
                    if (id == "AIRewardPerson" || id == "AIRecruitPerson")
                        return w.personalityDiplomaticPersonBonus;
                    break;
            }
            return 0;
        }

        /// <summary>
        /// 紧急度覆盖：战时态 / 危机态 / 发展态对优先级的强制偏移（阈值与幅度取自 <see cref="CityOrderWeights"/>）。
        /// </summary>
        /// <param name="s">态势快照</param>
        /// <param name="id">命令标识</param>
        /// <returns>覆盖偏移分</returns>
        static int GetOverrideBias(CitySituation s, string id)
        {
            CityOrderWeights w = AIConfig.Instance.cityOrder;
            int bias = 0;

            // ---------- 战时态：本城被围 ----------
            if (s.isUnderSiege)
            {
                if (id == "AIAttack")
                    bias += w.siegeDefenseBias;
                if (IsInternalAffair(id))
                    bias -= w.siegeInternalPenalty;
            }

            // ---------- 战时态：邻城被围 / 有威胁部队 / 港关丢失 ----------
            if (s.besiegedNeighborCount > 0 && id == "AIReinforce")
                bias += w.reinforceBias;
            if (s.hasThreatTroop && id == "AIAttack")
                bias += w.threatBias;
            if (s.lostSubCityCount > 0 && id == "AIAttack")
                bias += w.subCityBias;

            // ---------- 危机态：断粮 ----------
            if (s.foodFill < w.crisisFoodFill && id == "AITradeFood")
                bias += w.crisisFoodBias;

            // ---------- 危机态：治安崩坏 ----------
            if (s.security < w.securityCritical && id == "AISecurity")
                bias += w.crisisSecurityBias;

            // ---------- 危机态：兵装枯竭 ----------
            if (s.weaponCount <= 0 && s.troops > w.weaponCrisisTroops && id == "AICreateItems")
                bias += w.crisisWeaponBias;

            // ---------- 发展态：内陆 + 无战事 ----------
            bool peaceful = !s.isUnderSiege
                            && s.besiegedNeighborCount == 0
                            && !s.hasThreatTroop
                            && !s.isBorderCity;
            if (peaceful)
            {
                if (IsInternalAffair(id) || id == "AITransfrom" || id == "AIResearch")
                    bias += w.peaceInternalBias;
                if (id == "AIAttack")
                    bias -= w.peaceMilitaryPenalty;
            }

            return bias;
        }

        /// <summary>
        /// 是否属于内政类命令（战时会被整体降权）。
        /// </summary>
        /// <param name="id">命令标识</param>
        /// <returns>是否内政类</returns>
        static bool IsInternalAffair(string id)
        {
            return id == "AIIntrior"
                || id == "AISecurity"
                || id == "AITrainTroop"
                || id == "AICreateItems"
                || id == "AICreateMachine"
                || id == "AICreateBoat"
                || id == "AISearching"
                || id == "AIRecruitPerson"
                || id == "AIRewardPerson";
        }
    }
}
