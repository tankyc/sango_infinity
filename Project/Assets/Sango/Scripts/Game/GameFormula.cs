/*
 * 文件名：GameFormula.cs
 * 描述：游戏公式类，包含游戏中使用的各种计算公式
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core
{
    /// <summary>
    /// 游戏公式类，包含游戏中使用的各种计算公式
    /// 如招募概率、伤害计算、成功率计算等
    /// </summary>
    public class GameFormula
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        private static GameFormula _instance = new GameFormula();

        /// <summary>
        /// 获取或设置单例实例
        /// </summary>
        public static GameFormula Instance
        {
            set { _instance = value; }
            get { return _instance; }
        }

        /// <summary>
        /// 目标武将和可执行武将間是否有特殊关系
        /// 返回是否有特殊关系，是否登用成功
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="target"></param>
        /// <param name="type">0,正常招募 1,俘虏招募 2.破城招募</param>
        /// <param name="probability"></param>
        /// <returns></returns>
        public virtual bool RecruitPersonProbabilityByRelationship(Person actor, Person target, int type, out int probability)
        {
            if (RecruitPersonProbabilityByRelationshipOverride != null)
                return RecruitPersonProbabilityByRelationshipOverride(actor, target, type, out probability);

            probability = 0;
            if (!target.IsAlive || !actor.IsAlive) return true;

            bool target_is_returnable = target.mBelongForce != null;
            //目标武将势力消灭
            if (type == (int)PersonRecruitType.OnForceFall)
                target_is_returnable = false;

            //执行武将沒有君主时总是失敗
            if (actor.mBelongForce == null) return true;

            // 当目标武将的禁止仕官君主是执行武将君主时，总是失敗
            if (target.bannedForceId == actor.mBelongForce.Id)
                return true;

            //目标武将是君主时，总是失敗
            if (target_is_returnable && target == target.mBelongForce.mGovernor)
                return true;

            //目标武将有义兄弟
            if (target.BrotherList != null)
            {
                for (int i = 0; i < target.BrotherList.Count; i++)
                {
                    // 目标武将与义兄弟在同一势力时，总是失敗
                    Person brother = target.BrotherList[i];
                    if (target_is_returnable && brother.mBelongForce == target.mBelongForce)
                        return true;

                    // 目标武将与执行武将是义兄弟或与执行武将君主时义兄弟时，总是成功
                    if (brother == actor || brother == actor.mBelongForce.mGovernor)
                    {
                        probability = 100;
                        return true;
                    }
                    else if (brother.mBelongForce != null && brother.mBelongForce == actor.mBelongForce)
                    {
                        probability = 100;
                        return true;
                    }
                    //目标武将的义兄弟属於执行武将以外势力时，总是失敗
                    else if (brother.mBelongForce != null && brother.mBelongForce != actor.mBelongForce)
                        return true;

                }
            }

            //目标武将有配偶
            if (target.mSpouseList != null)
            {
                for (int i = 0; i < target.mSpouseList.Count; i++)
                {
                    // 目标武将与配偶在同一势力时，总是失敗
                    Person spouse = target.mSpouseList[i];
                    if (target_is_returnable && spouse.mBelongForce == target.mBelongForce)
                        return true;
                    //目标武将的配偶属於执行武将以外势力时，总是失敗
                    else if (spouse.mBelongForce != null && spouse.mBelongForce != actor.mBelongForce)
                        return true;
                    //目标武将与执行武将是配偶或与执行武将君主时配偶时，总是成功
                    else if (spouse == actor || spouse == actor.mBelongForce.mGovernor)
                    {
                        probability = 100;
                        return true;
                    }
                    //目标武将的配偶在执行武将势力时，总是成功
                    else if (spouse.mBelongForce != null && spouse.mBelongForce == actor.mBelongForce)
                    {
                        probability = 100;
                        return true;
                    }
                }
            }

            //目标武将有厌恶武将
            if (target.mHatePersonList != null)
            {
                for (int i = 0; i < target.mHatePersonList.Count; i++)
                {
                    // 目标武将的厌恶武将是执行武将时，总是失敗
                    Person person = target.mHatePersonList[i];
                    if (person == actor)
                        return true;
                    //目标武将的厌恶武将是执行武将的君主时，总是失敗
                    else if (person == actor.mBelongForce.mGovernor)
                        return true;
                }
            }


            //目标武将有亲爱武将
            if (target.mLikePersonList != null)
            {
                for (int i = 0; i < target.mLikePersonList.Count; i++)
                {
                    // 目标武将的亲爱武将是目标武将的君主时，总是失敗
                    Person person = target.mLikePersonList[i];
                    if (person == actor)
                        return true;
                    //目标武将的亲爱武将是目标武将的君主时，总是失敗
                    else if (target_is_returnable && person == target.mBelongForce.mGovernor)
                        return true;
                    //目标武将的亲爱武将是执行武将的君主时，总是成功
                    else if (person == actor.mBelongForce.mGovernor)
                    {
                        probability = 100;
                        return true;
                    }
                }
            }

            return false;

        }
        public delegate bool RecruitPersonProbabilityByRelationshipCall(Person actor, Person target, int type, out int probability);
        public static RecruitPersonProbabilityByRelationshipCall RecruitPersonProbabilityByRelationshipOverride;

        /// <summary>
        /// 获取招募概率
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="target"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public virtual int RecruitPersonProbability(Person actor, Person target, int type)
        {
            if (RecruitPersonProbabilityOverride != null)
                return RecruitPersonProbabilityOverride(actor, target, type);

            if (!target.IsAlive || !actor.IsAlive) return 0;

            //执行武将沒有君主时总是失敗
            if (actor.mBelongForce == null) return 0;


            ScenarioVariables variables = Scenario.Cur.Variables;
            int playerAdd = actor.IsPlayer ? variables.playerRecruitAdd : 0;

            //检测是否有特殊关系
            if (RecruitPersonProbabilityByRelationship(actor, target, type, out int p))
                return p + playerAdd;

            int loyalty = target.loyalty;
            if (type == (int)PersonRecruitType.Normal)
            {
                // 普通招募武将
                loyalty = target.IsWild ? 0 : target.loyalty;
                if (loyalty + target.mArgumentation.loyaltyAdd >= variables.recruitableLine)
                    return 0;
            }

            // 

            Argumentation argumentation = target.mArgumentation;

            Person actorGovernor = actor.mBelongForce.mGovernor;
            Person targetGovernor = null;
            if (target.mBelongForce != null && type != (int)PersonRecruitType.OnForceFall)
                targetGovernor = target.mBelongForce.mGovernor;

            int aishou = variables.recruitBaseCompatibility;
            //目标武将在野或是已灭亡势力的俘虏
            if (target.IsWild || type == (int)PersonRecruitType.OnForceFall)
            {
                loyalty = variables.recruitWildLoyaltyBase + variables.difficulty * variables.recruitLoyaltyDifficultyFactor;
                if (!target.IsPrisoner)
                    //义理_普通
                    argumentation = Scenario.Cur.GetObject<Argumentation>(variables.recruitDefaultArgumentationId);
            }
            // 获取目标相性与君主的距离
            else
            {
                if (targetGovernor != null)
                    aishou = target.CompatibilityDistance(targetGovernor);
            }
            int n = variables.recruitBaseSuccessRate + (aishou - target.CompatibilityDistance(actorGovernor)) * variables.recruitCompatibilityFactorNumerator / variables.recruitCompatibilityFactorDenominator;
            n -= (argumentation.loyaltyAdd + variables.recruitLoyaltyInfluenceBase) * loyalty * variables.recruitLoyaltyInfluenceNumerator / variables.recruitLoyaltyInfluenceDenominator;
            n += Math.Max(actor.Glamour, variables.recruitMinGlamour) * variables.recruitGlamourFactorNumerator / variables.recruitGlamourFactorDenominator;
            n -= target.IsLike(targetGovernor) ? variables.recruitLikePersonInfluence : 0;
            n -= target.IsParentchild(targetGovernor) ? variables.recruitParentChildInfluence : 0;
            n += target.IsHate(targetGovernor) ? variables.recruitHatePersonInfluence : 0;
            n += target.IsPrisoner ? variables.recruitPrisonerInfluence : 0;
            n += GameRandom.Range(0, Math.Max(0, variables.recruitRandomMax - argumentation.loyaltyAdd));
            
            // 在野回合数加成,每在野1回合加成3%
            n += target.wildTurnCount * 3;

            // 主公魅力影响
            n += actorGovernor.Glamour / variables.recruitGovernorGlamourFactor;
            n = Math.Max(n, 0);

            // 第一次发现,成功率加成;
            if (type == 3)
                n += variables.recruitFirstDiscoveryBonus;

            int giri = variables.recruitBaseGiri;
            if (type != 0)
                giri = Math.Min(variables.recruitMaxGiri - argumentation.loyaltyAdd * variables.recruitGiriLoyaltyFactor, variables.recruitBaseGiri);
            n = Math.Min(n * giri / variables.recruitBaseGiri, 100);

            return n + playerAdd;
        }
        public delegate int RecruitPersonProbabilityCall(Person actor, Person target, int type);
        public static RecruitPersonProbabilityCall RecruitPersonProbabilityOverride;

        /// <summary>
        /// 计算武将的逃跑概率(城池)
        /// </summary>
        /// <param name="person"></param>
        /// <param name="city"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public virtual int PersonEscapeProbablility_InCity(Person person, City city, Scenario scenario)
        {
            if (PersonEscapeProbablility_InCityOverride != null)
            {
                return PersonEscapeProbablility_InCityOverride(person, city, scenario);
            }

            ScenarioVariables variables = scenario.Variables;
            int escapeChance = variables.baseEscapeProbabllity + variables.baseEscapeProbablilityAddByTurn * person.missionCounter;

            // 如果在城市中，逃跑概率减少
            escapeChance -= variables.escapeCityReduction;
            if (escapeChance < 0) escapeChance = 0;
            if (escapeChance > variables.escapeMaxProbability) escapeChance = variables.escapeMaxProbability;

            //TODO: 其他影响越狱的概率
            return escapeChance;
        }
        public delegate int PersonEscapeProbablility_InCityCall(Person person, City city, Scenario scenario);
        public static PersonEscapeProbablility_InCityCall PersonEscapeProbablility_InCityOverride;

        /// <summary>
        /// 计算武将的逃跑概率(部队里)
        /// </summary>
        /// <param name="person"></param>
        /// <param name="city"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public virtual int PersonEscapeProbablility_InTroop(Person person, Troop troop, Scenario scenario)
        {
            if (PersonEscapeProbablility_InTroopOverride != null)
            {
                return PersonEscapeProbablility_InTroopOverride(person, troop, scenario);
            }
            ScenarioVariables variables = scenario.Variables;
            int escapeChance = variables.baseEscapeProbabllity + variables.baseEscapeProbablilityAddByTurn * person.missionCounter;

            // 如果在队伍中，逃跑概率增加
            escapeChance += variables.escapeTroopIncrease; 

            // 确保逃跑概率在合理范围内
            if (escapeChance < 0) escapeChance = 0;
            if (escapeChance > variables.escapeMaxProbability) escapeChance = variables.escapeMaxProbability;

            //TODO: 其他影响越狱的概率
            return escapeChance;
        }
        public delegate int PersonEscapeProbablility_InTroopCall(Person person, Troop troop, Scenario scenario);
        public static PersonEscapeProbablility_InTroopCall PersonEscapeProbablility_InTroopOverride;


        
    }

}
