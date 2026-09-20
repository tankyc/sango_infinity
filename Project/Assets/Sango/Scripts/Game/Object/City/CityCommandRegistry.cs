/*
 * 文件名：CityCommandRegistry.cs
 * 描述：城池 AI 命令注册表。集中登记所有城池 AI 命令的元数据：
 *       执行委托、基础权重、态势动态评分规则、硬性适用条件与适用城池类型。
 *
 * 注意：全部权重与阈值均来自 AIConfig.cityOrder（CityOrderWeights），
 *       可通过 Data/Common/AIConfig.json 的 "cityOrder" 节点覆盖，无需改动代码。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城池 AI 命令元数据。
    /// </summary>
    public class CityCommandProfile
    {
        /// <summary>命令标识（也用于配置键与调试输出）</summary>
        public string id;
        /// <summary>执行委托</summary>
        public Func<City, Scenario, bool> action;
        /// <summary>基础权重</summary>
        public int basePriority;
        /// <summary>态势动态评分规则（可空，返回附加分）</summary>
        public Func<CitySituation, int> scoreRule;
        /// <summary>硬性适用条件（可空；不满足则直接排除）</summary>
        public Func<CitySituation, bool> validRule;
        /// <summary>适用城池类型</summary>
        public CityKind allowKinds = CityKind.All;
    }

    /// <summary>
    /// 城池 AI 命令注册表（单例式静态表）。
    /// </summary>
    public static class CityCommandRegistry
    {
        static List<CityCommandProfile> profiles;

        /// <summary>全部已注册命令</summary>
        public static List<CityCommandProfile> Profiles
        {
            get
            {
                if (profiles == null)
                    Build();
                return profiles;
            }
        }

        /// <summary>
        /// 让命令表在下次访问时重建（用于配置热更新 / 调试）。
        /// </summary>
        public static void Rebuild()
        {
            profiles = null;
        }

        /// <summary>
        /// 构建命令表。所有权重与阈值均取自 <see cref="CityOrderWeights"/>（可 JSON 覆盖）。
        /// </summary>
        static void Build()
        {
            CityOrderWeights w = AIConfig.Instance.cityOrder;
            profiles = new List<CityCommandProfile>(24);

            // ==================== 军事 ====================
            Add("AIAttack", CityAI.AIAttack, w.baseAIAttack, s =>
            {
                int score = 0;
                if (s.isBorderCity) score += w.scoreAttackBorderBonus;
                if (s.troopFill > w.troopHighFill) score += w.scoreAttackTroopHighBonus;
                else if (s.troopFill < w.troopLowFill) score -= w.scoreAttackTroopLowPenalty;
                if (s.freePersons < 2) score -= w.scoreAttackNoPersonPenalty;
                if (s.isUnderSiege) score -= w.scoreAttackSiegePenalty;
                return score;
            });

            Add("AIReinforce", CityAI.AIReinforce, w.baseAIReinforce, s =>
            {
                int score = s.besiegedNeighborCount * w.scoreReinforcePerNeighbor;
                if (s.troopFill > w.troopMidFill) score += w.scoreReinforceTroopBonus;
                if (s.isUnderSiege) score -= w.scoreReinforceSelfSiegePenalty;
                return score;
            });

            // ==================== 经济 ====================
            Add("AITradeFood", CityAI.AITradeFood, w.baseAITradeFood, s =>
            {
                if (s.foodFill < w.fillCritical) return w.scoreFoodCriticalBonus;
                if (s.foodFill < w.fillLow) return w.scoreFoodLowBonus;
                if (s.foodFill > w.fillFull) return 0;
                return w.scoreFoodDefaultBonus;
            });

            // ==================== 内政 ====================
            Add("AIIntrior", CityAI.AIIntrior, w.baseAIIntrior, s =>
            {
                int score = w.scoreInternalDefault;
                if (s.isUnderSiege) score -= w.scoreInternalSiegePenalty;
                if (s.isBorderCity) score -= w.scoreInternalBorderPenalty;
                if (s.foodFill > w.fillHigh && s.goldFill > w.fillHigh) score += w.scoreInternalRichBonus;
                return score;
            });

            Add("AISecurity", CityAI.AISecurity, w.baseAISecurity, s =>
            {
                if (s.security < w.securityLow) return w.scoreSecurityCriticalBonus;
                if (s.security < w.securityOk) return w.scoreSecurityLowBonus;
                return 0;
            });

            Add("AITrainTroop", CityAI.AITrainTroop, w.baseAITrainTroop, s =>
            {
                if (s.morale < w.moraleLow) return w.scoreMoraleLowBonus;
                if (s.morale < w.moraleOk) return w.scoreMoraleOkBonus;
                return w.scoreMoraleDefaultBonus;
            });

            Add("AIRewardPerson", CityAI.AIRewardPerson, w.baseAIRewardPerson, s =>
            {
                int score = w.scoreRewardBase;
                if (s.gold > w.rewardGoldThreshold) score += w.scoreRewardGoldBonus;
                return score;
            });

            // ==================== 募兵 / 兵装 ====================
            Add("AIRecruitTroop", CityAI.AIRecruitTroop, w.baseAIRecruitTroop, s =>
            {
                int score = 0;
                if (s.troopFill < w.troopLowFill) score += w.scoreRecruitTroopCriticalBonus;
                else if (s.troopFill < w.troopMidFill) score += w.scoreRecruitTroopLowBonus;
                if (s.security < w.securityOk) score -= w.scoreRecruitSecurityPenalty;
                return score;
            });

            Add("AICreateItems", CityAI.AICreateItems, w.baseAICreateItems, s =>
            {
                int need = (int)(s.troops * w.weaponCoverRatio);
                if (s.weaponCount < need) return w.scoreWeaponCriticalBonus;
                if (s.weaponCount < s.troops) return w.scoreWeaponLowBonus;
                return 0;
            });

            Add("AICreateMachine", CityAI.AICreateMachine, w.baseAICreateMachine, s =>
            {
                if (s.machineCount >= w.machineEnough) return -w.scoreMachineEnoughPenalty;
                return w.scoreMachineBonus;
            }, s => s.hasMachineFactory);

            Add("AICreateBoat", CityAI.AICreateBoat, w.baseAICreateBoat, s =>
            {
                if (s.hasPort) return w.scoreBoatPortBonus;
                return -w.scoreBoatPenalty;
            }, s => s.hasBoatFactory);

            // ==================== 人事 / 搜索 ====================
            Add("AISearching", CityAI.AISearching, w.baseAISearching, s =>
            {
                if (s.freePersons <= 1) return -w.scoreSearchingNoPersonPenalty;
                return w.scoreSearchingBonus;
            });

            Add("AIRecruitPerson", CityAI.AIRecruitPerson, w.baseAIRecruitPerson, s =>
            {
                if (s.freePersons <= 1) return -w.scoreRecruitPersonNoPersonPenalty;
                return w.scoreRecruitPersonBonus;
            });

            // ==================== 运输 ====================
            Add("AITransfrom", CityAI.AITransfrom, w.baseAITransfrom, s =>
            {
                if (s.foodFill > w.fillLow && s.goldFill > w.fillLow) return w.scoreTransfromRichBonus;
                return w.scoreTransfromDefaultBonus;
            }, s => !s.isBorderCity);

            Add("AITransfromToBelongCity", CityAI.AITransfromToBelongCity, w.baseAITransfromToBelongCity,
                s => w.scoreTransfromToBelongCityBonus,
                s => (s.kind & (CityKind.Port | CityKind.Gate)) != 0);

            // ==================== 科技 ====================
            Add("AIResearch", TechniqueResearch.AIResearch, w.baseAIResearch, s =>
            {
                if (s.isUnderSiege) return -w.scoreResearchSiegePenalty;
                if (s.gold > w.researchGoldThreshold) return w.scoreResearchGoldBonus;
                return w.scoreResearchDefaultBonus;
            });

            // ==================== 补给 ====================
            Add("AIMakeSupplyTroop", CityAI.AIMakeSupplyTroop, w.baseAIMakeSupplyTroop, s =>
            {
                if (s.needyAllyCount >= AIConfig.Instance.supplyMinNeedyTroops) return w.scoreSupplyBonus;
                return -w.scoreSupplyPenalty;
            });
        }

        /// <summary>
        /// 注册一条命令。
        /// </summary>
        /// <param name="id">命令标识</param>
        /// <param name="action">执行委托</param>
        /// <param name="basePriority">基础权重</param>
        /// <param name="scoreRule">态势动态评分规则（可空）</param>
        /// <param name="validRule">硬性适用条件（可空）</param>
        /// <param name="allowKinds">适用城池类型</param>
        static void Add(string id, Func<City, Scenario, bool> action, int basePriority,
            Func<CitySituation, int> scoreRule = null,
            Func<CitySituation, bool> validRule = null,
            CityKind allowKinds = CityKind.All)
        {
            profiles.Add(new CityCommandProfile
            {
                id = id,
                action = action,
                basePriority = basePriority,
                scoreRule = scoreRule,
                validRule = validRule,
                allowKinds = allowKinds,
            });
        }
    }
}
