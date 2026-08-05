using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Sango.Core
{
    public class ForceAI
    {


        /// <summary>
        /// AI个性类型
        /// </summary>
        public enum AIPersonalityType
        {
            Aggressive,    // 侵略型
            Defensive,     // 防御型
            Diplomatic,    // 外交型
            Economic,      // 经济型
            Balanced       // 平衡型
        }

        /// <summary>
        /// 获取势力的AI个性
        /// </summary>
        public static AIPersonalityType GetAIPersonality(Force force)
        {
            // 基于势力领袖的性格决定AI个性
            if (force.Governor != null && force.Governor.personality != null)
            {
                // 基于性格特征计算AI个性
                int warScore = force.Governor.personality.warTendencyAdd;
                int defenseScore = force.Governor.personality.defenseTendencyAdd;
                int diplomacyScore = force.Governor.personality.diplomacyTendencyAdd;
                int economicScore = force.Governor.personality.economicTendencyAdd;

                // 找出最高得分的个性类型
                int maxScore = System.Math.Max(System.Math.Max(warScore, defenseScore), System.Math.Max(diplomacyScore, economicScore));

                if (maxScore == warScore)
                    return AIPersonalityType.Aggressive;
                else if (maxScore == defenseScore)
                    return AIPersonalityType.Defensive;
                else if (maxScore == diplomacyScore)
                    return AIPersonalityType.Diplomatic;
                else if (maxScore == economicScore)
                    return AIPersonalityType.Economic;
                else
                    return AIPersonalityType.Balanced;
            }
            return AIPersonalityType.Balanced;
        }

        /// <summary>
        /// AI外交
        /// </summary>
        public static bool AIDiplomacy(Force force, Scenario scenario)
        {
            if (scenario.TurnCount < 10) return true;
            if (force.IsPlayer) return true;
            if (force.Governor == null) return true;
            if (force.CapitalCity == null) return true;

            City centerCity = force.CapitalCity;
            if (centerCity.freePersons.Count == 0)
                return true;

            if (!GameRandom.Chance(30)) return true;

            // 获取AI个性
            AIPersonalityType personality = GetAIPersonality(force);

            // 清理过期的外交免疫时间
            CleanupDiplomacyImmunity(force, scenario);

            // 检查是否需要撕毁条约
            if (CheckBreakAlliance(force, scenario, personality))
            {
                return true;
            }

            // 优先处理停战请求
            foreach (Force neighbor in force.NeighborForceList)
            {
                if (force.IsAlliance(neighbor)) continue;
                if (IsInDiplomacyImmunity(force, neighbor.Id)) continue;

                int relation = scenario.GetRelation(force, neighbor);
                if (relation < -500)
                {
                    // 考虑停战
                    if (centerCity.gold >= 2000)
                    {
                        // 防御型AI更倾向于停战
                        int chance = personality == AIPersonalityType.Defensive ? 20 : 10;
                        if (GameRandom.Chance(chance))
                        {
                            // 计算附加金钱价值，关系越差，投入越多
                            int additionalValue = CalculateDiplomacyResourceValue(force, neighbor, relation, DiplomacyActionType.Truce, personality);

                            // 计算成功率
                            Person diplomat = FindSuitableDiplomat(force);
                            if (diplomat != null)
                            {
                                DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                                // 创建外交行为实例并计算成功率
                                DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.Truce, force, neighbor, diplomat, additionalValue);
                                int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                                if (successRate >= 20)
                                {
                                    bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.Truce, force, neighbor, diplomat, additionalValue);
                                    if (!success)
                                    {
                                        RecordDiplomacyFailure(force, neighbor.Id);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            // 处理结盟
            foreach (Force neighbor in force.NeighborForceList)
            {
                // 不和玩家结盟
                if (neighbor.IsPlayer && GameRandom.Chance(95)) continue;

                if (force.IsAlliance(neighbor)) continue;
                if (IsInDiplomacyImmunity(force, neighbor.Id)) continue;
                if (neighbor == force) continue;
                int relation = scenario.GetRelation(force, neighbor);
                if (relation >= 1000)
                {
                    // 考虑结盟
                    if (centerCity.gold >= 3000)
                    {
                        // 外交型AI更倾向于结盟
                        int chance = personality == AIPersonalityType.Diplomatic ? 30 : 20;
                        if (GameRandom.Chance(chance))
                        {
                            // 计算附加金钱价值，关系越好，投入越多
                            int additionalValue = CalculateDiplomacyResourceValue(force, neighbor, relation, DiplomacyActionType.Alliance, personality);

                            // 计算成功率
                            Person diplomat = FindSuitableDiplomat(force);
                            if (diplomat != null)
                            {
                                DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                                // 创建外交行为实例并计算成功率
                                DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.Alliance, force, neighbor, diplomat, additionalValue);
                                int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                                if (successRate >= 20)
                                {
                                    bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.Alliance, force, neighbor, diplomat, additionalValue);
                                    if (!success)
                                    {
                                        RecordDiplomacyFailure(force, neighbor.Id);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            // 处理通商
            foreach (Force neighbor in force.NeighborForceList)
            {
                if (force.IsAlliance(neighbor)) continue;
                if (IsInDiplomacyImmunity(force, neighbor.Id)) continue;

                int relation = scenario.GetRelation(force, neighbor);
                if (relation >= 0)
                {
                    // 考虑通商
                    // 经济型AI更倾向于通商
                    int chance = personality == AIPersonalityType.Economic ? 25 : 15;
                    if (GameRandom.Chance(chance))
                    {
                        // 计算附加金钱价值，促进通商成功
                        int additionalValue = CalculateDiplomacyResourceValue(force, neighbor, relation, DiplomacyActionType.Trade, personality);

                        // 计算成功率
                        Person diplomat = FindSuitableDiplomat(force);
                        if (diplomat != null)
                        {
                            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                            // 创建外交行为实例并计算成功率
                            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.Trade, force, neighbor, diplomat, additionalValue);
                            int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                            if (successRate >= 20)
                            {
                                bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.Trade, force, neighbor, diplomat, additionalValue);
                                if (!success)
                                {
                                    RecordDiplomacyFailure(force, neighbor.Id);
                                }
                                return true;
                            }
                        }
                    }
                }
            }

            // 处理和亲
            foreach (Force neighbor in force.NeighborForceList)
            {
                if (force.IsAlliance(neighbor)) continue;
                if (IsInDiplomacyImmunity(force, neighbor.Id)) continue;

                int relation = scenario.GetRelation(force, neighbor);
                if (relation >= 800)
                {
                    // 考虑和亲
                    if (centerCity.gold >= 5000)
                    {
                        // 外交型AI更倾向于和亲
                        int chance = personality == AIPersonalityType.Diplomatic ? 15 : 10;
                        if (GameRandom.Chance(chance))
                        {
                            // 计算附加金钱价值，和亲需要较高投入
                            int additionalValue = CalculateDiplomacyResourceValue(force, neighbor, relation, DiplomacyActionType.Marriage, personality);

                            // 计算成功率
                            Person diplomat = FindSuitableDiplomat(force);
                            if (diplomat != null)
                            {
                                DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                                // 创建外交行为实例并计算成功率
                                DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.Marriage, force, neighbor, diplomat, additionalValue);
                                int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                                if (successRate >= 20)
                                {
                                    bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.Marriage, force, neighbor, diplomat, additionalValue);
                                    if (!success)
                                    {
                                        RecordDiplomacyFailure(force, neighbor.Id);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            // 处理送礼
            foreach (Force neighbor in force.NeighborForceList)
            {
                if (force.IsAlliance(neighbor)) continue;
                if (IsInDiplomacyImmunity(force, neighbor.Id)) continue;

                int relation = scenario.GetRelation(force, neighbor);
                if (relation < 500 && centerCity.gold >= 1000)
                {
                    // 考虑送礼
                    // 外交型AI更倾向于送礼
                    int chance = personality == AIPersonalityType.Diplomatic ? 35 : 25;
                    if (GameRandom.Chance(chance))
                    {
                        int giftValue = CalculateDiplomacyResourceValue(force, neighbor, relation, DiplomacyActionType.SendGift, personality);
                        if (centerCity.gold >= giftValue)
                        {
                            // 计算成功率
                            Person diplomat = FindSuitableDiplomat(force);
                            if (diplomat != null)
                            {
                                DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                                // 创建外交行为实例并计算成功率
                                DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.SendGift, force, neighbor, diplomat, giftValue);
                                int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                                if (successRate >= 20)
                                {
                                    bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.SendGift, force, neighbor, diplomat, giftValue);
                                    if (!success)
                                    {
                                        RecordDiplomacyFailure(force, neighbor.Id);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            // 敌人的敌人就是朋友
            foreach (Force neighbor in force.NeighborForceList)
            {
                if (force.IsAlliance(neighbor)) continue;

                int neighborRelation = scenario.GetRelation(neighbor, force);
                if (neighborRelation > 0) continue;

                foreach (Force enemysenemy in neighbor.NeighborForceList)
                {
                    if (enemysenemy != force && !enemysenemy.IsAlliance(neighbor) && !force.NeighborForceList.Contains(enemysenemy) && !enemysenemy.IsAlliance(force))
                    {
                        if (IsInDiplomacyImmunity(force, enemysenemy.Id)) continue;

                        int enemysenemy_relation = scenario.GetRelation(enemysenemy, force);
                        if (enemysenemy_relation > 1000)
                        {
                            if (centerCity.gold > 3000)
                            {
                                // 考虑结盟
                                // 侵略型AI更倾向于与敌人的敌人结盟
                                int chance = personality == AIPersonalityType.Aggressive ? enemysenemy_relation * 2 / 100 : enemysenemy_relation / 100;
                                if (GameRandom.Chance(chance, 100))
                                {
                                    // 计算附加金钱价值
                                    int additionalValue = CalculateDiplomacyResourceValue(force, enemysenemy, enemysenemy_relation, DiplomacyActionType.AllianceRequest, personality);

                                    // 计算成功率
                                    Person diplomat = FindSuitableDiplomat(force);
                                    if (diplomat != null)
                                    {
                                        DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                                        // 创建外交行为实例并计算成功率
                                        DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.AllianceRequest, force, enemysenemy, diplomat, additionalValue);
                                        int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                                        if (successRate >= 20)
                                        {
                                            bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.AllianceRequest, force, enemysenemy, diplomat, additionalValue);
                                            if (!success)
                                            {
                                                RecordDiplomacyFailure(force, enemysenemy.Id);
                                            }
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                        else if (enemysenemy_relation > 0)
                        {
                            if (centerCity.gold > 2000)
                            {
                                // 考虑结交
                                int giftValue = CalculateDiplomacyResourceValue(force, enemysenemy, enemysenemy_relation, DiplomacyActionType.SendGift, personality);
                                if (centerCity.gold >= giftValue)
                                {
                                    // 计算成功率
                                    Person diplomat = FindSuitableDiplomat(force);
                                    if (diplomat != null)
                                    {
                                        DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                                        // 创建外交行为实例并计算成功率
                                        DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.SendGift, force, enemysenemy, diplomat, giftValue);
                                        int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                                        if (successRate >= 20)
                                        {
                                            bool success = diplomacyManager.PerformDiplomacyAction(DiplomacyActionType.SendGift, force, enemysenemy, diplomat, giftValue);
                                            if (!success)
                                            {
                                                RecordDiplomacyFailure(force, enemysenemy.Id);
                                            }
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 查找合适的外交使者
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>合适的武将</returns>
        private static Person FindSuitableDiplomat(Force force)
        {
            if (force.Governor?.BelongCity == null) return null;

            // 使用ForceAI中的外交推荐方法选择合适的武将
            Person[] recommendedDiplomats = CounsellorRecommendDiplomacy(force.CapitalCity.freePersons);
            if (recommendedDiplomats != null && recommendedDiplomats.Length > 0)
            {
                return recommendedDiplomats[0];
            }

            return null;
        }

        /// <summary>
        /// 检查是否处于外交免疫期
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="targetForceId">目标势力ID</param>
        /// <returns>是否处于免疫期</returns>
        public static bool IsInDiplomacyImmunity(Force force, int targetForceId)
        {
            if (force.DiplomacyImmunityTime.TryGetValue(targetForceId, out int immunityEndTime))
            {
                return immunityEndTime > (Scenario.Cur?.TurnCount ?? 0);
            }
            return false;
        }

        /// <summary>
        /// 清理过期的外交免疫时间
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景</param>
        private static void CleanupDiplomacyImmunity(Force force, Scenario scenario)
        {
            if (scenario == null || force.DiplomacyImmunityTime.Count == 0) return;

            List<int> expiredForces = new List<int>();
            foreach (var kvp in force.DiplomacyImmunityTime)
            {
                if (kvp.Value <= scenario.TurnCount)
                {
                    expiredForces.Add(kvp.Key);
                }
            }

            foreach (int forceId in expiredForces)
            {
                force.DiplomacyImmunityTime.Remove(forceId);
                force.DiplomacyFailCount.Remove(forceId);
            }
        }

        /// <summary>
        /// 记录外交失败
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="targetForceId">目标势力ID</param>
        private static void RecordDiplomacyFailure(Force force, int targetForceId)
        {
            // 增加失败次数
            if (force.DiplomacyFailCount.TryGetValue(targetForceId, out int failCount))
            {
                failCount++;
                force.DiplomacyFailCount[targetForceId] = failCount;
            }
            else
            {
                force.DiplomacyFailCount[targetForceId] = 1;
            }

            // 如果失败次数超过3次，设置外交免疫时间
            if (force.DiplomacyFailCount[targetForceId] >= 3)
            {
                // 设置30天的免疫时间
                force.DiplomacyImmunityTime[targetForceId] = (Scenario.Cur?.TurnCount ?? 0) + 30;
                force.DiplomacyFailCount.Remove(targetForceId);

#if SANGO_DEBUG
                Force targetForce = Scenario.Cur?.forceSet.Get(targetForceId);
                if (targetForce != null)
                {
                    Sango.Log.Info($"@外交@{force.Name} 对 {targetForce.Name} 的外交连续失败3次，进入30天外交免疫期！");
                }
#endif
            }
        }

        /// <summary>
        /// 检查是否需要撕毁条约
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景</param>
        /// <param name="personality">AI个性</param>
        /// <returns>是否执行了撕毁条约</returns>
        private static bool CheckBreakAlliance(Force force, Scenario scenario, AIPersonalityType personality)
        {
            // 检查所有当前的同盟和停战协议
            List<Alliance> alliancesToBreak = new List<Alliance>();
            List<Force> targetsToBreak = new List<Force>();

            foreach (Alliance alliance in force.AllianceList)
            {
                if (!alliance.IsAlive) continue;

                // 找到同盟中的另一个势力
                Force otherForce = null;
                foreach (Force f in alliance.ForceList)
                {
                    if (f != force)
                    {
                        otherForce = f;
                        break;
                    }
                }

                if (otherForce == null) continue;

                // 检查是否需要撕毁条约
                if (ShouldBreakAlliance(force, otherForce, alliance, scenario, personality))
                {
                    alliancesToBreak.Add(alliance);
                    targetsToBreak.Add(otherForce);
                }
            }

            // 执行撕毁条约
            for (int i = 0; i < alliancesToBreak.Count; i++)
            {
                // 获取同盟关系并撕毁
                Alliance alliance = force.CheckAlliance(targetsToBreak[i]);
                if (alliance != null)
                {
                    alliance.IsAlive = false;
                    // 从双方的同盟列表中移除
                    force.AllianceList.Remove(alliance);
                    targetsToBreak[i].AllianceList.Remove(alliance);
                }
                return true; // 一次只撕毁一个条约
            }

            return false;
        }

        /// <summary>
        /// 判断是否应该撕毁条约
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="otherForce">另一方势力</param>
        /// <param name="alliance">条约</param>
        /// <param name="scenario">场景</param>
        /// <param name="personality">AI个性</param>
        /// <returns>是否应该撕毁</returns>
        private static bool ShouldBreakAlliance(Force force, Force otherForce, Alliance alliance, Scenario scenario, AIPersonalityType personality)
        {
            int relation = scenario.GetRelation(force, otherForce);

            // 关系恶化到一定程度
            if (relation < -500)
            {
                // 侵略型AI更倾向于撕毁条约
                int chance = personality == AIPersonalityType.Aggressive ? 40 : 20;
                if (GameRandom.Chance(chance))
                {
                    return true;
                }
            }

            // 对方与自己的敌人结盟
            foreach (Force enemy in force.NeighborForceList)
            {
                if (force.IsEnemy(enemy) && otherForce.IsAlliance(enemy))
                {
                    // 侵略型和防御型AI更倾向于撕毁条约
                    int chance = (personality == AIPersonalityType.Aggressive || personality == AIPersonalityType.Defensive) ? 50 : 30;
                    if (GameRandom.Chance(chance))
                    {
                        return true;
                    }
                }
            }

            // 停战协议下，关系恢复到一定程度
            if (alliance.allianceType == AllianceType.Truce && relation > 500)
            {
                // 侵略型AI更倾向于撕毁停战协议
                int chance = personality == AIPersonalityType.Aggressive ? 30 : 15;
                if (GameRandom.Chance(chance))
                {
                    return true;
                }
            }

            // 经济型AI在对方经济实力过强时可能撕毁条约
            if (personality == AIPersonalityType.Economic)
            {
                int otherForcePower = CalculateForcePower(otherForce);
                int selfPower = CalculateForcePower(force);
                if (otherForcePower > selfPower * 1.5f)
                {
                    if (GameRandom.Chance(25))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 计算势力实力
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>势力实力值</returns>
        private static int CalculateForcePower(Force force)
        {
            int power = 0;

            // 城市数量
            int cityCount = 0;
            force.ForEachCity(city => cityCount++);
            power += cityCount * 1000;

            // 兵力
            int totalTroops = 0;
            force.ForEachCity(city => totalTroops += city.troops);
            power += totalTroops;

            // 金钱
            int totalGold = 0;
            force.ForEachCity(city => totalGold += city.gold);
            power += totalGold / 10;

            return power;
        }

        /// <summary>
        /// 计算外交资源价值
        /// </summary>
        /// <param name="force">发送方势力</param>
        /// <param name="targetForce">目标势力</param>
        /// <param name="relation">当前关系值</param>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="personality">AI个性</param>
        /// <returns>资源价值</returns>
        private static int CalculateDiplomacyResourceValue(Force force, Force targetForce, int relation, DiplomacyActionType actionType, AIPersonalityType personality)
        {
            City centerCity = force.CapitalCity;
            if (centerCity == null) return 0;

            int baseValue = 0;
            int maxValue = centerCity.gold / 2; // 最多使用一半的资金

            switch (actionType)
            {
                case DiplomacyActionType.SendGift:
                    // 使用配置的送礼金额
                    baseValue = Scenario.Cur.Variables.diplomacySendGiftAmount;
                    break;
                case DiplomacyActionType.Truce:
                    // 停战需要较高投入
                    baseValue = 2000 - relation;
                    baseValue = System.Math.Max(1500, System.Math.Min(baseValue, 5000));
                    break;
                case DiplomacyActionType.Alliance:
                    // 结盟需要高投入
                    baseValue = 3000 + (relation / 5);
                    baseValue = System.Math.Max(2000, System.Math.Min(baseValue, 8000));
                    break;
                case DiplomacyActionType.Marriage:
                    // 和亲需要最高投入
                    baseValue = 5000 + (relation / 3);
                    baseValue = System.Math.Max(3000, System.Math.Min(baseValue, 10000));
                    break;
                case DiplomacyActionType.Trade:
                    // 通商投入适中
                    baseValue = 1000 + (relation / 10);
                    baseValue = System.Math.Max(500, System.Math.Min(baseValue, 3000));
                    break;
                case DiplomacyActionType.AllianceRequest:
                    // 请求结盟投入适中
                    baseValue = 2000 + (relation / 5);
                    baseValue = System.Math.Max(1500, System.Math.Min(baseValue, 5000));
                    break;
                default:
                    baseValue = 1000;
                    break;
            }

            // 根据AI个性调整投入
            switch (personality)
            {
                case AIPersonalityType.Diplomatic:
                    // 外交型AI更愿意投入
                    baseValue = (int)(baseValue * 1.2f);
                    break;
                case AIPersonalityType.Economic:
                    // 经济型AI投入较少
                    baseValue = (int)(baseValue * 0.8f);
                    break;
                case AIPersonalityType.Aggressive:
                    // 侵略型AI在结盟时投入较多
                    if (actionType == DiplomacyActionType.Alliance || actionType == DiplomacyActionType.AllianceRequest)
                    {
                        baseValue = (int)(baseValue * 1.1f);
                    }
                    break;
                case AIPersonalityType.Defensive:
                    // 防御型AI在停战时投入较多
                    if (actionType == DiplomacyActionType.Truce)
                    {
                        baseValue = (int)(baseValue * 1.1f);
                    }
                    break;
            }

            // 确保投入不超过最大可用资金
            return System.Math.Min(baseValue, maxValue);
        }

        /// <summary>
        /// AI-俘虏
        /// </summary>
        public static bool AICaptives(Force force, Scenario scenario)
        {
            // 处理本势力的俘虏
            ProcessCaptives(force, scenario);

            // 处理其他势力的俘虏赎回请求
            ProcessRansomRequests(force, scenario);

            return true;
        }

        private static bool ProcessCaptives(Force force, Person captive, Scenario scenario)
        {
            // 检查是否可以招降
            if (CanRecruitCaptive(force, captive, scenario))
            {
                // 尝试招降
                if (TryRecruitCaptive(force, captive, scenario))
                {
                    return true;
                }
            }

            // 检查是否应该释放
            if (ShouldReleaseCaptive(force, captive, scenario))
            {
                ReleaseCaptive(force, captive, scenario);
                return true;
            }

            return false;
        }

        private static void ProcessCaptives(Force force, Scenario scenario)
        {
            force.ForEachCity(city =>
            {
                for (int i = 0; i < city.captiveList.Count; i++)
                {
                    Person captive = city.captiveList[i];
                    if (captive == null) continue;

                    // 检查是否可以招降
                    if (ProcessCaptives(force, captive, scenario))
                    {
                        i--;
                        continue;
                    }
                }
            });

            force.ForEachTroop(city =>
            {
                for (int i = 0; i < city.captiveList.Count; i++)
                {
                    Person captive = city.captiveList[i];
                    // 检查是否可以招降
                    if (ProcessCaptives(force, captive, scenario))
                    {
                        i--;
                        continue;
                    }
                }
            });
        }

        private static bool CanRecruitCaptive(Force force, Person captive, Scenario scenario)
        {
            // 检查忠诚度
            if (captive.loyalty > 70)
                return false;

            // 检查势力关系
            int relation = scenario.GetRelation(force, captive.BelongForce);
            if (relation < -5000)
                return false;

            // 检查是否有足够的资金
            City capital = force.Governor?.BelongCity;
            if (capital == null || capital.gold < 2000)
                return false;

            return true;
        }

        private static bool TryRecruitCaptive(Force force, Person captive, Scenario scenario)
        {
            int probability = GameFormula.Instance.RecruitPersonProbability(force.Governor, captive, force.Id);

            // 根据势力领袖的性格调整招降概率
            if (force.Governor != null && force.Governor.personality != null)
            {
                probability += force.Governor.personality.recruitCaptiveTendencyAdd * 100;
            }

            if (GameRandom.Chance(probability, 10000))
            {
                captive.CurrentCity.RemoveCaptive(captive);
                captive.ChangeCorps(force.Governor?.BelongCorps);
                captive.ChangeCity(force.Governor?.BelongCity);
                captive.SetMission(MissionType.PersonReturn, captive.BelongCity);
#if SANGO_DEBUG
                Sango.Log.Info($"{force.Name}成功招降了{captive.BelongForce?.Name}的{captive.Name}！");
#endif
                return true;

            }
            return false;
        }

        private static bool ShouldReleaseCaptive(Force force, Person captive, Scenario scenario)
        {
            // 检查忠诚度
            //if (captive.loyalty > 100)
            //    return true;

            // 检查势力关系
            int relation = scenario.GetRelation(force, captive.BelongForce);
            if (relation > 5000)
                return true;

            // 检查是否有足够的粮食
            City capital = captive.CurrentCity;
            if (capital != null && capital.totalGainGold < capital.GoldCost(scenario))
                return true;

            // 根据势力领袖的性格调整释放概率
            int releaseChance = 5;
            if (force.Governor != null && force.Governor.personality != null)
            {
                releaseChance += force.Governor.personality.releaseCaptiveTendencyAdd;
            }

            // 随机释放
            if (GameRandom.Chance(releaseChance))
                return true;

            return false;
        }

        private static void ReleaseCaptive(Force force, Person captive, Scenario scenario)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"{force.Name}在{captive.CurrentCity.Name}释放了{captive.BelongForce?.Name}的{captive.Name}！");
#endif
            // 直接调用Person.Escape方法释放俘虏
            captive.Escape(EscapeType.Released, force);
        }

        private static void ProcessRansomRequests(Force force, Scenario scenario)
        {
            // 检查是否有其他势力的俘虏在本势力
            if (force.BeCaptiveList == null || force.BeCaptiveList.Count == 0)
                return;
            force.BeCaptiveList.Sort(PersonSortFunction.SortByMilitaryAbility.Sort);

            // 根据势力领袖的性格调整赎回概率
            int ransomChance = 50;
            if (force.Governor != null && force.Governor.personality != null)
            {
                ransomChance += force.Governor.personality.ransomCaptiveTendencyAdd;
            }
            // 原势力有足够的资金，考虑接受赎回
            if (GameRandom.Chance(ransomChance))
            {
                Person captive = force.BeCaptiveList[GameRandom.Range(force.BeCaptiveList.Count)];
                int ransom = CalculateRansom(captive);
                bool hasSend = false;
                force.ForEachCity(city =>
                {
                    if (!hasSend && city.gold >= ransom + 500)
                    {
                        Person[] people = ForceAI.CounsellorRecommendDiplomacy(city.freePersons);
                        if (people != null && people.Length > 0)
                        {
                            Person person = people[0];
                            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                            // 创建外交行为实例并计算成功率
                            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.Ransom, force, captive.CurrentCity.BelongForce, person, ransom);
                            int rate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                            if (rate > 50)
                            {
                                city.gold -= ransom;
                                person.SetMission(MissionType.PersonDiplomacy, captive.CurrentCity, (int)DiplomacyActionType.Ransom, ransom, person.Id);
                                city.freePersons.Remove(person);
                                hasSend = true;
#if SANGO_DEBUG
                                Sango.Log.Info($"{captive.BelongForce?.Name}派遣{person.Name}前往{captive.CurrentCity.BelongForce.Name}赎回我方俘虏{captive.Name}！");
#endif          
                                return;
                            }
                        }
                    }
                });
            }
        }

        private static int CalculateRansom(Person captive)
        {
            // 根据俘虏的能力计算赎金
            int baseRansom = 5000;
            int abilitySum = captive.Command + captive.Strength + captive.Intelligence + captive.Politics + captive.Glamour;
            int abilityBonus = abilitySum * 10;
            return baseRansom + abilityBonus;
        }
        /// <summary>
        /// AI-科技研发
        /// </summary>

        public static bool AITechniques(Force force, Scenario scenario)
        {
            // 检查是否有城市可以进行科技研发
            City capital = force.Governor?.BelongCity;
            if (capital == null)
                return true;

            // 检查是否有空闲武将进行研发
            if (capital.freePersons.Count == 0)
                return true;

            // 检查是否有正在研发的科技
            if (force.ResearchTechnique > 0)
                return true;

            // 选择要研发的科技
            Technique targetTechnique = SelectTechnique(force, scenario);
            if (targetTechnique == null)
                return true;

            // 选择研发人员
            Person[] researchers = ForceAI.CounsellorRecommendResearch(capital.freePersons, targetTechnique);
            if (researchers == null || researchers.Length == 0)
                return true;

            // 计算研发成本
            int[] cost = targetTechnique.GetCost(researchers, capital);
            if (cost == null)
                return true;

            // 检查是否有足够的资金和技巧点
            if (capital.gold < cost[0] || force.TechniquePoint < cost[1])
                return true;

            // 开始研发
            StartResearch(force, capital, targetTechnique, researchers, scenario);

            return true;
        }

        private static Technique SelectTechnique(Force force, Scenario scenario)
        {
            List<Technique> availableTechniques = new List<Technique>();
            Dictionary<Technique, float> techniqueWeights = new Dictionary<Technique, float>();

            // 收集所有可研发的科技
            foreach (Technique technique in scenario.CommonData.Techniques)
            {
                if (IsTechniqueAvailable(force, technique, scenario))
                {
                    availableTechniques.Add(technique);
                    float weight = CalculateTechniqueWeight(force, technique, scenario);
                    techniqueWeights[technique] = weight;
                }
            }

            if (availableTechniques.Count == 0)
                return null;

            // 根据权重选择科技
            return WeightedRandom(techniqueWeights);
        }

        private static bool IsTechniqueAvailable(Force force, Technique technique, Scenario scenario)
        {
            // 检查是否已经研发过
            if (force.HasTechnique(technique.Id))
                return false;

            // 检查前置科技
            if (technique.needTech > 0 && !force.HasTechnique(technique.needTech))
                return false;

            return true;
        }

        private static float CalculateTechniqueWeight(Force force, Technique technique, Scenario scenario)
        {
            float weight = 1.0f;

            // 基础权重
            weight += technique.level * 0.1f;

            // 根据当前局势调整权重
            if (IsAtWar(force, scenario))
            {
                // 战争时期优先军事相关科技
                weight *= 1.3f;
            }
            else
            {
                // 和平时期优先经济相关科技
                weight *= 1.2f;
            }

            // 根据城市数量调整权重
            int cityCount = force.CityCount;
            if (cityCount > 5)
            {
                // 城市多优先管理相关科技
                weight *= 1.1f;
            }

            // 根据势力领袖的性格调整科技研发倾向
            if (force.Governor != null && force.Governor.personality != null)
            {
                weight *= (1.0f + force.Governor.personality.technologyTendencyAdd * 0.01f);
            }

            return weight;
        }

        private static bool IsAtWar(Force force, Scenario scenario)
        {
            foreach (Force otherForce in scenario.forceSet)
            {
                if (otherForce != null && otherForce != force)
                {
                    int relation = scenario.GetRelation(force, otherForce);
                    if (relation < -3000)
                        return true;
                }
            }
            return false;
        }

        private static Technique WeightedRandom(Dictionary<Technique, float> weights)
        {
            float totalWeight = 0;
            foreach (float weight in weights.Values)
            {
                totalWeight += weight;
            }

            float random = GameRandom.Range(0, totalWeight);
            float current = 0;

            foreach (KeyValuePair<Technique, float> pair in weights)
            {
                current += pair.Value;
                if (random <= current)
                {
                    return pair.Key;
                }
            }

            return weights.Keys.FirstOrDefault();
        }

        private static void StartResearch(Force force, City city, Technique technique, Person[] researchers, Scenario scenario)
        {
            // 计算研发所需时间和资源
            int[] cost = technique.GetCost(researchers, city);
            if (cost == null)
                return;

            int researchDays = cost[2];
            int researchCost = cost[0];
            int techPointCost = cost[1];

            // 消耗资金和技巧点
            city.gold -= researchCost;
            force.GainTechniquePoint(-techPointCost);

            // 分配研发任务
            foreach (Person researcher in researchers)
            {
                if (researcher != null)
                {
                    city.freePersons.Remove(researcher);
                    researcher.SetMission(MissionType.PersonResearch, city, researchDays, technique.Id);
                }
            }

            // 记录研发中的科技
            force.ResearchTechnique = technique.Id;
            force.ResearchLeftCounter = researchDays;

#if SANGO_DEBUG
            Sango.Log.Info($"{force.Name}开始研发科技{technique.Name}，预计需要{researchDays}天！");
#endif
        }

        /// <summary>
        /// 3人推荐代理
        /// </summary>
        /// <param name="checkValue"></param>
        /// <param name="check1"></param>
        /// <param name="check2"></param>
        /// <param name="check3"></param>
        /// <returns></returns>
        public delegate bool Recommend3PersonValue(ref int[] checkValue, Person check1, Person check2, Person check3);
        public delegate bool Recommend2PersonValue(ref int[] checkValue, Person check1, Person check2);
        public delegate bool Recommend1PersonValue(ref int[] checkValue, Person check1);

        /// <summary>
        /// 1-3人遍历最优推荐基础函数(效率慢,最优解,尽量不用在人数>20的城市)
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="recommend3PersonValueFunc"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        static Person[] CounsellorRecommend3Person(List<Person> personList, Recommend3PersonValue recommend3PersonValueFunc)
        {
            return CounsellorRecommend3Person(personList, null, null, recommend3PersonValueFunc);
        }

        static Person[] CounsellorRecommend3Person(List<Person> personList, Person p1, Person p2, Recommend3PersonValue recommend3PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (count >= 20)
                return CounsellorFastRecommend3Person(personList, recommend3PersonValueFunc);

            Person[] checkPersons = new Person[3];
            int[] maxValue = new int[] { -99999, 99999 };
            bool hasValue = false;
            for (int i = 0; i < count; i++)
            {
                Person person1 = personList[i];
                if (p1 != null && person1 != p1) continue;
                if (recommend3PersonValueFunc(ref maxValue, person1, null, null))
                {
                    checkPersons[0] = person1;
                    checkPersons[1] = null;
                    checkPersons[2] = null;
                    hasValue = true;
                }
                for (int j = i + 1; j < count; j++)
                {
                    Person person2 = personList[j];
                    if (p2 != null && person2 != p2) continue;
                    if (recommend3PersonValueFunc(ref maxValue, person1, person2, null))
                    {
                        checkPersons[0] = person1;
                        checkPersons[1] = person2;
                        checkPersons[2] = null;
                        hasValue = true;
                    }
                    for (int k = j + 1; k < count; k++)
                    {
                        Person person3 = personList[k];
                        if (recommend3PersonValueFunc(ref maxValue, person1, person2, person3))
                        {
                            checkPersons[0] = person1;
                            checkPersons[1] = person2;
                            checkPersons[2] = person3;
                            hasValue = true;
                        }
                    }
                }
            }

            if (!hasValue)
                return null;

            return checkPersons;
        }

        static Person[] CounsellorRecommend2Person(List<Person> personList, Recommend2PersonValue recommend2PersonValueFunc)
        {
            return CounsellorRecommend2Person(personList, null, recommend2PersonValueFunc);
        }
        static Person[] CounsellorRecommend2Person(List<Person> personList, Person p1, Recommend2PersonValue recommend2PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (count >= 20)
                return CounsellorFastRecommend2Person(personList, recommend2PersonValueFunc);

            Person[] checkPersons = new Person[3];
            int[] maxValue = new int[] { -99999, 99999 };
            bool hasValue = false;
            for (int i = 0; i < count; i++)
            {
                Person person1 = personList[i];
                if (p1 != null && p1 != person1) continue;
                if (recommend2PersonValueFunc(ref maxValue, person1, null))
                {
                    checkPersons[0] = person1;
                    checkPersons[1] = null;
                    hasValue = true;
                }
                for (int j = i + 1; j < count; j++)
                {
                    Person person2 = personList[j];
                    if (recommend2PersonValueFunc(ref maxValue, person1, person2))
                    {
                        checkPersons[0] = person1;
                        checkPersons[1] = person2;
                        hasValue = true;
                    }
                }
            }

            if (!hasValue)
                return null;

            return checkPersons;
        }

        static Person[] CounsellorRecommend1Person(List<Person> personList, Recommend1PersonValue recommend1PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (count >= 20)
                return CounsellorFastRecommend1Person(personList, recommend1PersonValueFunc);

            Person[] checkPersons = new Person[3];
            int[] maxValue = new int[] { -99999, 99999 };
            bool hasValue = false;
            for (int i = 0; i < count; i++)
            {
                Person person1 = personList[i];
                if (recommend1PersonValueFunc(ref maxValue, person1))
                {
                    checkPersons[0] = person1;
                    hasValue = true;
                }
            }

            if (!hasValue)
                return null;

            return checkPersons;
        }

        /// <summary>
        /// 3人快速推荐, 不考虑溢出, 先找到第一个最高武将, 第N个武将第N高或者能改变执行值
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="recommend3PersonValueFunc"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        static Person[] CounsellorFastRecommend3Person(List<Person> personList, Recommend3PersonValue fastRecommend3PersonValueFunc)
        {
            return CounsellorFastRecommend3Person(personList, null, null, fastRecommend3PersonValueFunc);
        }

        static Person[] CounsellorFastRecommend3Person(List<Person> personList, Person p1, Person p2, Recommend3PersonValue fastRecommend3PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (personList.Count <= 3)
                return personList.ToArray();

            Person[] checkPersons = new Person[3];
            int[] maxValues = new int[] { -99999, 99999 };
            Person person1 = null, person2 = null;

            // 第一位找目标属性最大
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (p1 != null && p1 != person) continue;
                if (fastRecommend3PersonValueFunc(ref maxValues, person, null, null))
                {
                    person1 = person;
                    checkPersons[0] = person;
                }
            }


            maxValues[0] = -99999;
            maxValues[1] = 99999;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (p2 != null && p2 != person) continue;
                if (person != person1 && fastRecommend3PersonValueFunc(ref maxValues, person1, person, null))
                {
                    person2 = person;
                    checkPersons[1] = person;
                }
            }

            maxValues[0] = -99999;
            maxValues[1] = 99999;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (person != person1 && person != person2 && fastRecommend3PersonValueFunc(ref maxValues, person1, person2, person))
                {
                    checkPersons[2] = person;
                }
            }

            return checkPersons;
        }

        static Person[] CounsellorFastRecommend2Person(List<Person> personList, Recommend2PersonValue fastRecommend2PersonValueFunc)
        {
            return CounsellorFastRecommend2Person(personList, null, fastRecommend2PersonValueFunc);
        }
        static Person[] CounsellorFastRecommend2Person(List<Person> personList, Person p1, Recommend2PersonValue fastRecommend2PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (personList.Count <= 2)
                return personList.ToArray();

            Person[] checkPersons = new Person[3];
            int[] maxValues = new int[] { -99999, 99999 };
            Person person1 = null;
            // 第一位找目标属性最大
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (p1 != null && person != p1) continue;
                if (fastRecommend2PersonValueFunc(ref maxValues, person, null))
                {
                    person1 = person;
                    checkPersons[0] = person;
                }
            }

            maxValues[0] = -99999;
            maxValues[1] = 99999;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (person != person1 && fastRecommend2PersonValueFunc(ref maxValues, person1, person))
                {
                    checkPersons[1] = person;
                }
            }
            return checkPersons;
        }

        static Person[] CounsellorFastRecommend1Person(List<Person> personList, Recommend1PersonValue fastRecommend1PersonValueFunc)
        {
            int count = personList.Count;
            if (count <= 0)
                return null;

            if (personList.Count <= 1)
                return personList.ToArray();

            Person[] checkPersons = new Person[3];
            int[] maxValues = new int[] { -99999, 99999 };
            // 第一位找目标属性最大
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                if (fastRecommend1PersonValueFunc(ref maxValues, person))
                {
                    checkPersons[0] = person;
                }
            }
            return checkPersons;
        }


        /// <summary>
        /// 军师建造推荐
        /// </summary>
        public static Person[] CounsellorRecommendBuild(List<Person> personList, BuildingType buildingType)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxBuildTurn, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = GameUtility.Method_PersonBuildAbility(check1, check2, check3);
                int turnCount = buildingType.durabilityLimit % buildAbility == 0 ? 0 : 1;
                int buildCount = System.Math.Min(Scenario.Cur.Variables.BuildMaxTurn, buildingType.durabilityLimit / buildAbility + turnCount);

                if (buildCount < maxBuildTurn[1])
                {
                    maxBuildTurn[1] = buildCount;
                    return true;
                }

                return false;

            });
        }

        /// <summary>
        /// 军师巡查推荐
        /// </summary>
        public static Person[] CounsellorRecommendInspection(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseSecurityAbility;
                if (check2 != null) buildAbility += check2.BaseSecurityAbility;
                if (check3 != null) buildAbility += check3.BaseSecurityAbility;

                buildAbility = GameUtility.Method_SecurityAbility(buildAbility, 1);

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// 军师商业推荐
        /// </summary>
        public static Person[] CounsellorRecommendDevelop(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseCommerceAbility;
                if (check2 != null) buildAbility += check2.BaseCommerceAbility;
                if (check3 != null) buildAbility += check3.BaseCommerceAbility;

                buildAbility = GameUtility.Method_DevelopAbility(buildAbility);

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }


        /// <summary>
        /// 军师农业推荐
        /// </summary>
        public static Person[] CounsellorRecommendFarming(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseAgricultureAbility;
                if (check2 != null) buildAbility += check2.BaseAgricultureAbility;
                if (check3 != null) buildAbility += check3.BaseAgricultureAbility;

                buildAbility = GameUtility.Method_FarmingAbility(buildAbility);

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }

        /// <summary>
        /// 军师训练推荐
        /// </summary>
        public static Person[] CounsellorRecommendTrainTroops(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseTrainTroopAbility;
                if (check2 != null) buildAbility += check2.BaseTrainTroopAbility;
                if (check3 != null) buildAbility += check3.BaseTrainTroopAbility;

                buildAbility = GameUtility.Method_TrainTroops(buildAbility, 1);

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }

        /// <summary>
        /// 军师招募士兵推荐
        /// </summary>
        public static Person[] CounsellorRecommendRecruitTroop(List<Person> personList)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseRecruitmentAbility;
                if (check2 != null) buildAbility += check2.BaseRecruitmentAbility;
                if (check3 != null) buildAbility += check3.BaseRecruitmentAbility;

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }

        /// <summary>
        /// 军师招募兵装生产推荐
        /// </summary>
        public static Person[] CounsellorRecommendCreateItems(List<Person> personList, int[] commandFeatures = null)
        {
            return CounsellorRecommend3Person(personList, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
            {
                int buildAbility = 0;
                if (check1 != null) buildAbility += check1.BaseCreativeAbility;
                if (check2 != null) buildAbility += check2.BaseCreativeAbility;
                if (check3 != null) buildAbility += check3.BaseCreativeAbility;

                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;

            });
        }

        /// <summary>
        /// 军师部队推荐指定部队
        /// </summary>
        public static Person[] CounsellorRecommendMakeTroop(List<Person> personList, TroopType troopType, int maxPersonLimit = 3)
        {
            if (personList.Count <= 0)
                return null;

            if (personList.Count <= 1)
                return personList.ToArray();

            Person[] checkPersons = new Person[3];
            Person person1 = null;

            //TODO: 优先内置推荐队伍
            ///
            int checkValue = 0;
            int v_int = 0;
            int v_stength = 0;
            int v_command = 0;
            int level = 0;

            List<Person> list = new List<Person>(personList);
            list.Sort((a, b) =>
            {
                return b.MilitaryAbility.CompareTo(a.MilitaryAbility);
            });

            // 确认主将
            person1 = list[0];
            checkPersons[0] = person1;
            list.RemoveAt(0);
            v_int = person1.Intelligence;
            v_stength = person1.Strength;
            v_command = person1.Command;
            level = Troop.CheckTroopTypeLevel(troopType, person1);

            int destSlot = 1;
            if (destSlot >= maxPersonLimit)
                return checkPersons;

            // 必须要兵符携带者为主将
            if (troopType.validItemId > 0)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Person person = list[i];
                    if (person.HasItem(troopType.validItemId))
                    {
                        checkPersons[destSlot] = person;
                        list.RemoveAt(i);
                        person1 = person;
                        v_int = System.Math.Max(v_int, person1.Intelligence);
                        v_stength = System.Math.Max(v_stength, person1.Strength);
                        v_command = System.Math.Max(v_command, person1.Command);
                        level = System.Math.Max(level, Troop.CheckTroopTypeLevel(troopType, person1));
                        destSlot++;
                        break;
                    }
                }
            }

            // 补充适配的特性的武将, 一般适配特性的武将都带有高适应力
            if (troopType.matchFeatures != null)
            {
                bool alreadtHasFeature = false;
                for (int i = 0; i < destSlot; i++)
                {
                    Person exsistP = checkPersons[i];
                    if (exsistP.FeatureList != null)
                    {
                        for (int j = 0; j < troopType.matchFeatures.Length; j++)
                        {
                            if (exsistP.FeatureList.Contains(troopType.matchFeatures[j]))
                            {
                                alreadtHasFeature = true;
                                break;
                            }
                        }
                    }
                    if (alreadtHasFeature)
                        break;
                }

                if (!alreadtHasFeature)
                {
                    person1 = null;
                    // 优先战斗力适配的特性主将
                    for (int i = 0; i < list.Count; i++)
                    {
                        Person person = personList[i];
                        if (person.FeatureList != null)
                        {
                            for (int j = 0; j < troopType.matchFeatures.Length; j++)
                            {
                                if (person.FeatureList.Contains(troopType.matchFeatures[j]))
                                {
                                    checkPersons[destSlot] = person;
                                    person1 = person;
                                    v_int = System.Math.Max(v_int, person1.Intelligence);
                                    v_stength = System.Math.Max(v_stength, person1.Strength);
                                    v_command = System.Math.Max(v_command, person1.Command);
                                    level = System.Math.Max(level, Troop.CheckTroopTypeLevel(troopType, person1));
                                    break;
                                }
                            }
                        }

                        if (person1 != null)
                            break;
                    }

                    if (person1 != null)
                    {
                        list.Remove(person1);
                        destSlot++;
                    }
                }
            }

            if (destSlot >= maxPersonLimit)
                return checkPersons;

            // 优先补充适应
            list.Sort((a, b) =>
            {
                int lvl_a = Troop.CheckTroopTypeLevel(troopType, a);
                int lvl_b = Troop.CheckTroopTypeLevel(troopType, b);
                if (lvl_a == lvl_b)
                {
                    return a.MilitaryAbility.CompareTo(b.MilitaryAbility);
                }
                else
                    return lvl_b.CompareTo(lvl_a);
            });

            if(list.Count == 0)
                return checkPersons;

            person1 = list[0];
            int templevel = Troop.CheckTroopTypeLevel(troopType, person1);
            if (level < templevel)
            {
                list.RemoveAt(0);
                v_int = System.Math.Max(v_int, person1.Intelligence);
                v_stength = System.Math.Max(v_stength, person1.Strength);
                v_command = System.Math.Max(v_command, person1.Command);
                checkPersons[destSlot] = person1;
                destSlot++;
            }

            if (destSlot >= maxPersonLimit)
                return checkPersons;

            for (int k = 0; k < 2; k++)
            {
                // 不需要补充适应,尝试补充战斗力
                if (v_stength < 60)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Person person = list[i];
                        int checkLvl = Troop.CheckTroopTypeLevel(troopType, person);
                        if (person.Strength >= 70 && (checkLvl < level && level >= 3 || checkLvl <= level && level < 3))
                        {
                            checkPersons[destSlot] = person;
                            destSlot++;
                            v_int = System.Math.Max(v_int, person.Intelligence);
                            v_stength = System.Math.Max(v_stength, person.Strength);
                            v_command = System.Math.Max(v_command, person.Command);
                            list.RemoveAt(i);
                            break;
                        }
                    }
                }
                else if (v_command < 60)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Person person = list[i];
                        int checkLvl = Troop.CheckTroopTypeLevel(troopType, person);
                        if (person.Command >= 70 && (checkLvl < level && level >= 3 || checkLvl <= level && level < 3))
                        {
                            checkPersons[destSlot] = person;
                            destSlot++;
                            v_int = System.Math.Max(v_int, person.Intelligence);
                            v_stength = System.Math.Max(v_stength, person.Strength);
                            v_command = System.Math.Max(v_command, person.Command);
                            list.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (destSlot >= maxPersonLimit)
                    return checkPersons;

                if (person1.Intelligence < 70)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Person person = list[i];
                        int checkLvl = Troop.CheckTroopTypeLevel(troopType, person);
                        if (person.Intelligence >= 80 && (checkLvl < level && level >= 3 || checkLvl <= level && level < 3))
                        {
                            checkPersons[destSlot] = person;
                            v_int = System.Math.Max(v_int, person.Intelligence);
                            v_stength = System.Math.Max(v_stength, person.Strength);
                            v_command = System.Math.Max(v_command, person.Command);
                            destSlot++;
                            list.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (destSlot >= maxPersonLimit)
                    return checkPersons;
            }

            return checkPersons;
        }

        /// <summary>
        /// 军师运输队伍推荐
        /// </summary>
        public static Person[] CounsellorRecommendTransportTroop(List<Person> personList)
        {
            return CounsellorRecommend1Person(personList, (ref int[] maxValue, Person check1) =>
            {
                int buildAbility = check1.MilitaryAbility;
                if (buildAbility < maxValue[1])
                {
                    maxValue[1] = buildAbility;
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 军师交易推荐
        /// </summary>
        public static Person[] CounsellorRecommendTrade(List<Person> personList, int[] commandFeatures = null)
        {
            return CounsellorRecommend1Person(personList, (ref int[] maxValue, Person check1) =>
            {
                int buildAbility = check1.Politics;
                if (buildAbility > maxValue[0])
                {
                    maxValue[0] = buildAbility;
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 军师外交推荐
        /// </summary>
        public static Person[] CounsellorRecommendDiplomacy(List<Person> personList)
        {
            return CounsellorRecommend1Person(personList, (ref int[] maxValue, Person check1) =>
            {
                // 计算外交能力：政治 + 魅力/2
                int diplomacyAbility = check1.Politics + check1.Glamour / 2;
                if (diplomacyAbility > maxValue[0])
                {
                    maxValue[0] = diplomacyAbility;
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 军师研究推荐
        /// </summary>
        public static Person[] CounsellorRecommendResearch(List<Person> personList, Technique technique)
        {
            List<Person> featurePersonList = personList.FindAll(x => x.FeatureList != null && x.FeatureList.Contains(technique.recommandFeatures));
            if (featurePersonList.Count >= 3) return featurePersonList.ToArray();

            Person p1 = featurePersonList.Count > 0 ? featurePersonList[0] : null;
            Person p2 = featurePersonList.Count > 1 ? featurePersonList[1] : null;

            switch (technique.needAttr)
            {
                case (int)AttributeType.Command:
                    return CounsellorRecommend3Person(personList, p1, p2, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Command;
                        if (check2 != null) buildAbility += check2.Command;
                        if (check3 != null) buildAbility += check3.Command;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                case (int)AttributeType.Strength:
                    return CounsellorRecommend3Person(personList, p1, p2, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Strength;
                        if (check2 != null) buildAbility += check2.Strength;
                        if (check3 != null) buildAbility += check3.Strength;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                case (int)AttributeType.Intelligence:
                    return CounsellorRecommend3Person(personList, p1, p2, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Intelligence;
                        if (check2 != null) buildAbility += check2.Intelligence;
                        if (check3 != null) buildAbility += check3.Intelligence;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                case (int)AttributeType.Politics:
                    return CounsellorRecommend3Person(personList, p1, p2, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Politics;
                        if (check2 != null) buildAbility += check2.Politics;
                        if (check3 != null) buildAbility += check3.Politics;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                case (int)AttributeType.Glamour:
                    return CounsellorRecommend3Person(personList, p1, p2, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Glamour;
                        if (check2 != null) buildAbility += check2.Glamour;
                        if (check3 != null) buildAbility += check3.Glamour;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
                default:
                    // 默认情况：使用智力作为研发能力
                    return CounsellorRecommend3Person(personList, p1, p2, (ref int[] maxValue, Person check1, Person check2, Person check3) =>
                    {
                        int buildAbility = 0;
                        if (check1 != null) buildAbility += check1.Intelligence;
                        if (check2 != null) buildAbility += check2.Intelligence;
                        if (check3 != null) buildAbility += check3.Intelligence;

                        int c = buildAbility / 70;
                        if (c > maxValue[0])
                        {
                            maxValue[0] = c;
                            maxValue[1] = buildAbility;
                            return true;
                        }
                        else if (c == maxValue[0])
                        {
                            if (buildAbility < maxValue[1])
                            {
                                maxValue[1] = buildAbility;
                                return true;
                            }
                        }
                        return false;

                    });
            }
        }

        /// <summary>
        /// 军师推荐搜索的人选
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="target"></param>
        /// <param name="commandFeatures"></param>
        /// <returns></returns>
        public static Person[] CounsellorRecommendSearching(List<Person> personList, City target, int[] commandFeatures = null)
        {
            if (target.invisiblePersons.Count == 0)
                return null;

            List<Person> result = new List<Person>();
            for (int i = 0; i < personList.Count; i++)
            {
                Person person = personList[i];
                if (commandFeatures != null && person.HasFeatrue(commandFeatures))
                    result.Add(person);
                else if (person.Politics >= 90)
                    result.Add(person);
            }

            if (result.Count == 0)
                return null;

            return result.ToArray();
        }

        /// <summary>
        /// 军师推荐招募人才的人选
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="target"></param>
        /// <param name="commandFeatures"></param>
        /// <returns></returns>
        public static Person CounsellorRecommendRecruitPerson(List<Person> personList, Person target, int[] commandFeatures = null)
        {
            Person maxP = null;
            int max = 0;
            for (int i = 0; i < personList.Count; i++)
            {
                Person person = personList[i];
                int probability = GameFormula.Instance.RecruitPersonProbability(person, target, 0);
                if (probability >= 100)
                    return person;
                else if (probability >= 30)
                {
                    if (probability > max)
                    {
                        max = probability;
                        maxP = person;
                    }
                }
            }

            return maxP;
        }

        public static bool AISetOfficial(Force force, Scenario scenario)
        {
            List<Official> match_officials = new List<Official>();
            force.ForEachPerson(person =>
            {
                if (person.CanUpgradeOfficial)
                {
                    Official[] officials = person.Official.NextOfficials;
                    match_officials.Clear();
                    foreach (Official official in officials)
                    {
                        if (official.CheckPerson(person))
                        {
                            match_officials.Add(official);
                        }
                    }

                    // AI随机给一个官职
                    if (match_officials.Count > 0)
                    {
                        Official dst = match_officials[GameRandom.Range(match_officials.Count)];
                        person.UpgradeOfficial(dst);
                    }
                }
            });

            return true;
        }

        public static bool AITransfromPerson(Force force, Scenario scenario)
        {
            if(force.IsPlayer)
            {
                return true;
            }

            List<Person> canTransforPersons = new List<Person>();
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == force && c.IsCity())
                {
                    City kCity = c;
                    if (kCity.PersonHole < 0 && kCity.freePersons.Count > 0)
                    {
                        int count = Math.Abs(kCity.PersonHole);
                        kCity.freePersons.Sort((a, b) =>
                        {
                            return -a.MilitaryAbility.CompareTo(b.MilitaryAbility);
                        });

                        int maxCount = kCity.freePersons.Count;
                        for (int k = 0; k < count; k++)
                        {
                            if (k < maxCount)
                                canTransforPersons.Add(kCity.freePersons[maxCount - 1 - k]);
                        }
                    }
                }
            }

            if (canTransforPersons.Count <= 0)
                return true;

            canTransforPersons.Sort((a, b) =>
            {
                return a.MilitaryAbility.CompareTo(b.MilitaryAbility);
            });

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == force && c.IsCity())
                {
                    City kCity = c;
                    if (canTransforPersons.Count <= 0)
                        break;

                    if (kCity.PersonHole > 0 && kCity.IsBorderCity)
                    {
                        for (int k = 0; k < kCity.PersonHole; k++)
                        {
                            if (canTransforPersons.Count > 0)
                            {
                                canTransforPersons[0].TransformToCity(kCity);
                                canTransforPersons.RemoveAt(0);
                            }
                        }
                    }

                    if (canTransforPersons.Count <= 0)
                        break;
                }
            }

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == force && c.IsCity())
                {
                    City kCity = c;
                    if (canTransforPersons.Count <= 0)
                        break;
                    if (kCity.PersonHole > 0 && !kCity.IsBorderCity)
                    {
                        for (int k = 0; k < kCity.PersonHole; k++)
                        {
                            if (canTransforPersons.Count > 0)
                            {
                                canTransforPersons[0].TransformToCity(kCity);
                                canTransforPersons.RemoveAt(0);
                            }
                        }
                    }
                }
            }
            return true;
        }
    }

}
