using System.Collections.Generic;
using TKNewtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 外交管理器
    /// </summary>
    [GameSystem(order = 100, nickName = "DiplomacyManager")]
    public class DiplomacyManager : GameSystem
    {
        /// <summary>
        /// 势力关系字典
        /// </summary>
        private Dictionary<string, ForceRelation> _forceRelations;

        /// <summary>
        /// 初始化外交管理器
        /// </summary>
        public override void Init()
        {
            _forceRelations = new Dictionary<string, ForceRelation>();
            
            // 注册每月开始事件
            GameEvent.OnMonthStart += HandleMonthlyRelationChanges;
        }

        /// <summary>
        /// 获取势力间的关系
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <returns>关系值</returns>
        public int GetRelation(Force forceA, Force forceB)
        {
            if (forceA == null || forceB == null || forceA == forceB)
                return 0;

            // 使用Scenario中的关系数据
            if (Scenario.Cur != null)
            {
                return Scenario.Cur.GetRelation(forceA, forceB);
            }

            // 备用：使用本地关系数据
            string key = GetForceRelationKey(forceA, forceB);
            if (_forceRelations.TryGetValue(key, out ForceRelation relation))
            {
                return relation.relation;
            }

            // 默认为0
            return 0;
        }

        /// <summary>
        /// 设置势力间的关系
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <param name="relation">关系值</param>
        public void SetRelation(Force forceA, Force forceB, int relation)
        {
            if (forceA == null || forceB == null || forceA == forceB)
                return;

            // 使用Scenario中的关系数据
            if (Scenario.Cur != null)
            {
                // 计算当前关系与目标关系的差值
                int currentRelation = Scenario.Cur.GetRelation(forceA, forceB);
                int delta = relation - currentRelation;
                Scenario.Cur.AddRelation(forceA, forceB, delta);
                return;
            }

            // 备用：使用本地关系数据
            string key = GetForceRelationKey(forceA, forceB);
            ForceRelation forceRelation;

            if (!_forceRelations.TryGetValue(key, out forceRelation))
            {
                forceRelation = new ForceRelation();
                _forceRelations[key] = forceRelation;
            }

            forceRelation.relation = relation;
        }

        /// <summary>
        /// 增加势力间的关系
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <param name="value">增加的值</param>
        public void AddRelation(Force forceA, Force forceB, int value)
        {
            int currentRelation = GetRelation(forceA, forceB);
            SetRelation(forceA, forceB, currentRelation + value);
        }

        /// <summary>
        /// 减少势力间的关系
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <param name="value">减少的值</param>
        public void ReduceRelation(Force forceA, Force forceB, int value)
        {
            int currentRelation = GetRelation(forceA, forceB);
            SetRelation(forceA, forceB, currentRelation - value);
        }

        /// <summary>
        /// 获取势力关系的键
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <returns>关系键</returns>
        private string GetForceRelationKey(Force forceA, Force forceB)
        {
            // 确保键的唯一性，使用较小的ID在前
            if (forceA.Id< forceB.Id)
            {
                return $"{forceA.Id}_{forceB.Id}";
            }
            else
            {
                return $"{forceB.Id}_{forceA.Id}";
            }
        }

        /// <summary>
        /// 检查是否可以执行外交行动
        /// </summary>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <returns>是否可以执行</returns>
        public bool CanPerformDiplomacyAction(DiplomacyActionType actionType, Force sender, Force receiver)
        {
            DiplomacyActionBase action = CreateDiplomacyAction(actionType, sender, receiver);
            return action != null && action.CanPerform();
        }

        /// <summary>
        /// 创建外交行为实例
        /// </summary>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        /// <param name="resourceValue">资源价值</param>
        /// <returns>外交行为实例</returns>
        public DiplomacyActionBase CreateDiplomacyAction(DiplomacyActionType actionType, Force sender, Force receiver, Person diplomat = null, int resourceValue = 0)
        {
            switch (actionType)
            {
                case DiplomacyActionType.Alliance:
                    return new DiplomacyActionAlliance(sender, receiver, diplomat, resourceValue);
                case DiplomacyActionType.Truce:
                    return new DiplomacyActionTruce(sender, receiver, diplomat, resourceValue);
                case DiplomacyActionType.DeclareWar:
                    return new DiplomacyActionDeclareWar(sender, receiver, diplomat);
                case DiplomacyActionType.SendGift:
                    return new DiplomacyActionSendGift(sender, receiver, diplomat, resourceValue);
                // 其他外交行为类型可以在这里添加
                default:
                    return null;
            }
        }

        /// <summary>
        /// 执行外交行动
        /// </summary>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        /// <param name="param">行动参数</param>
        /// <param name="captiveId">俘虏ID（仅用于赎回俘虏）</param>
        /// <returns>行动是否成功</returns>
        public bool PerformDiplomacyAction(DiplomacyActionType actionType, Force sender, Force receiver, Person diplomat = null, object param = null, int captiveId = 0)
        {
            int resourceValue = 0;
            if (param != null && param is int intParam)
            {
                resourceValue = intParam;
            }

            DiplomacyActionBase action = CreateDiplomacyAction(actionType, sender, receiver, diplomat, resourceValue);
            if (action != null)
            {
                return action.Perform();
            }
            return false;
        }

        /// <summary>
        /// 获取外交行动的名称
        /// </summary>
        /// <param name="actionType">外交行动类型</param>
        /// <returns>行动名称</returns>
        public string GetActionName(DiplomacyActionType actionType)
        {
            switch (actionType)
            {
                case DiplomacyActionType.Alliance:
                    return "结盟";
                case DiplomacyActionType.Truce:
                    return "停战";
                case DiplomacyActionType.DeclareWar:
                    return "宣战";
                case DiplomacyActionType.SendGift:
                    return "送礼";
                case DiplomacyActionType.RequestTechnique:
                    return "请求技术";
                case DiplomacyActionType.RequestTroops:
                    return "请求兵力";
                case DiplomacyActionType.Trade:
                    return "通商";
                case DiplomacyActionType.Marriage:
                    return "和亲";
                case DiplomacyActionType.AllianceRequest:
                    return "请求结盟";
                case DiplomacyActionType.TruceRequest:
                    return "请求停战";
                case DiplomacyActionType.Ransom:
                    return "赎回俘虏";
                default:
                    return "未知行动";
            }
        }

        /// <summary>
        /// 处理每月关系变化
        /// </summary>
        /// <param name="scenario">场景</param>
        private void HandleMonthlyRelationChanges(Scenario scenario)
        {
            if (scenario == null || scenario.forceSet.Count< 2)
                return;

            int forceCount = scenario.forceSet.Count;
            ScenarioVariables variables = scenario.Variables;
            
            for (int i = 0; i< forceCount; ++i)
            {
                Force forceA = scenario.forceSet[i];
                if (forceA == null || !forceA.IsAlive)
                    continue;

                for (int j = i + 1; j < forceCount; ++j)
                {
                    Force forceB = scenario.forceSet[j];
                    if (forceB == null || !forceB.IsAlive)
                        continue;

                    // 检查是否有同盟或停战协议
                    bool hasAlliance = forceA.IsAlliance(forceB);
                    
                    // 关系变化概率
                    int chance = variables.relationChangeChance;
                    
                    // 同盟关系变化概率降低，且变化值为正
                    if (hasAlliance)
                    {
                        chance = chance / 2;
                        if (GameRandom.Chance(chance))
                        {
                            // 同盟关系缓慢增长
                            AddRelation(forceA, forceB, variables.diplomacyMonthlyAllianceRelationIncrease);
                        }
                    }
                    else
                    {
                        // 普通关系有一定概率下降
                        if (GameRandom.Chance(chance))
                        {
                            // 关系自然衰减
                            ReduceRelation(forceA, forceB, variables.diplomacyMonthlyNormalRelationDecrease);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 步骤1：确定自势力是否需要外交
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>是否需要外交</returns>
        public bool DetermineIfDiplomacyNeeded(Force force)
        {
            if (force == null || !force.IsAlive)
                return false;

            // 检查是否有足够的资金
            if (force.CapitalCity == null || force.CapitalCity.gold< 500)
                return false;

            // 检查是否有空闲武将
            bool hasFreePerson = false;
            force.ForEachPerson(person =>
            {
                if (person.IsFree && person.IsAlive)
                {
                    hasFreePerson = true;
                    return;
                }
            });

            return hasFreePerson;
        }

        /// <summary>
        /// 步骤2：选定外交对象
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="actionType">外交行动类型</param>
        /// <returns>外交对象</returns>
        public Force SelectDiplomacyTarget(Force force, DiplomacyActionType actionType)
        {
            if (force == null || !force.IsAlive)
                return null;

            // 获取所有其他势力
            List<Force> potentialTargets = new List<Force>();
            foreach (Force otherForce in Scenario.Cur.forceSet)
            {
                if (otherForce != force && otherForce.IsAlive)
                {
                    potentialTargets.Add(otherForce);
                }
            }

            // 根据外交行动类型选择合适的目标
            Force bestTarget = null;
            int bestScore = -999999;

            foreach (Force target in potentialTargets)
            {
                int score = CalculateTargetScore(force, target, actionType);
                if (score >bestScore)
                {
                    bestScore = score;
                    bestTarget = target;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// 计算目标势力的得分
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="target">目标势力</param>
        /// <param name="actionType">外交行动类型</param>
        /// <returns>得分</returns>
        private int CalculateTargetScore(Force force, Force target, DiplomacyActionType actionType)
        {
            int score = 0;
            int relation = GetRelation(force, target);

            switch (actionType)
            {
                case DiplomacyActionType.Alliance:
                    // 寻找关系较好的势力
                    score = relation;
                    // 如果对方势力强大，结盟更有价值
                    score += target.FightPower / 100;
                    break;
                    
                case DiplomacyActionType.SendGift:
                    // 寻找关系较差的势力，送礼效果更好
                    score = -relation;
                    // 如果对方势力贫穷，送礼效果更好
                    int targetTotalGold = 0;
                    target.ForEachCity(city => targetTotalGold += city.gold);
                    score += (5000 - targetTotalGold) / 100;
                    break;
                    
                case DiplomacyActionType.DeclareWar:
                    // 寻找关系较差且实力较弱的势力
                    score = -relation - target.FightPower / 100;
                    break;
                    
                case DiplomacyActionType.Truce:
                    // 寻找关系较差但有停战可能的势力
                    score = Mathf.Abs(relation);
                    break;
                    
                default:
                    score = relation;
                    break;
            }

            return score;
        }

        /// <summary>
        /// 步骤3：选定执行外交的武将
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="actionType">外交行动类型</param>
        /// <returns>武将</returns>
        public Person SelectDiplomat(Force force, DiplomacyActionType actionType)
        {
            if (force == null || !force.IsAlive)
                return null;

            Person bestDiplomat = null;
            int bestScore = -999999;

            force.ForEachPerson(person =>
            {
                if (!person.IsFree || !person.IsAlive)
                    return;

                int score = CalculateDiplomatScore(person, actionType);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDiplomat = person;
                }
            });

            return bestDiplomat;
        }

        /// <summary>
        /// 计算武将的外交能力得分
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="actionType">外交行动类型</param>
        /// <returns>得分</returns>
        private int CalculateDiplomatScore(Person person, DiplomacyActionType actionType)
        {
            int score = 0;

            switch (actionType)
            {
                case DiplomacyActionType.Alliance:
                case DiplomacyActionType.Truce:
                case DiplomacyActionType.SendGift:
                    // 外交类行动看重政治和魅力
                    score = person.Politics * 2 + person.Glamour;
                    break;
                    
                case DiplomacyActionType.DeclareWar:
                    // 宣战看重统率和武力
                    score = person.Command + person.Strength;
                    break;
                    
                case DiplomacyActionType.RequestTechnique:
                    // 请求技术看重智力
                    score = person.Intelligence * 2;
                    break;
                    
                default:
                    // 默认看重政治
                    score = person.Politics;
                    break;
            }

            // 性格加成
            if (person.mPersonality != null)
            {
                score += person.mPersonality.diplomacyTendencyAdd;
            }

            return score;
        }

        /// <summary>
        /// 步骤4：计算外交可能性
        /// </summary>
        /// <param name="action">外交行为</param>
        /// <returns>成功率</returns>
        public int CalculateDiplomacySuccessRate(DiplomacyActionBase action)
        {
            if (action == null)
                return 0;

            return action.CalculateSuccessRate();
        }

        /// <summary>
        /// 步骤5：派遣武将执行外交任务
        /// </summary>
        /// <param name="action">外交行为</param>
        /// <returns>是否成功派遣</returns>
        public bool DispatchDiplomat(DiplomacyActionBase action)
        {
            if (action == null || action.Diplomat == null)
                return false;

            // 获取目标势力主公所在的城市
            City targetCity = action.Receiver.mGovernor?.mBelongCity;
            if (targetCity == null)
                return false;

            // 计算移动所需时间
            int distance = action.Diplomat.DistanceDays(targetCity);
            if (distance<= 0)
                distance = 1;

            // 设置外交任务
            action.Diplomat.SetMission(MissionType.PersonDiplomacy, targetCity, distance, action.Receiver.Id, (int)action.ActionType, action.ResourceValue);
            
            // 将武将从首都的空闲武将列表中移除
            action.Diplomat.mBelongCity.freePersons.Remove(action.Diplomat);

            // 调用外交行为的 OnDispatch 方法，用于对不同外交事件做不同处理
            action.OnDispatch();

#if SANGO_DEBUG
            Sango.Log.Info($"@外交@{action.Sender.Name} 对 {action.Receiver.Name} 派遣了使者 {action.Diplomat.Name} 执行{action.GetActionName()}行动！");
#endif

            return true;
        }

        /// <summary>
        /// 步骤6和7：武将前往目标城池并返回（由任务系统处理）
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="receiverForce">接收方势力</param>
        /// <param name="resourceValue">资源价值</param>
        /// <returns>外交结果</returns>
        /// <summary>
        /// 执行外交任务（指定执行结果）
        /// </summary>
        /// <param name="success">执行结果（true表示成功，false表示失败）</param>
        /// <param name="person">执行外交的武将</param>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="receiverForce">接收方势力</param>
        /// <param name="resourceValue">资源价值</param>
        public void ExecuteDiplomacyMission(bool success, Person person, DiplomacyActionType actionType, Force receiverForce, int resourceValue)
        {
            if (person == null || receiverForce == null)
                return;

            Force senderForce = person.mBelongForce;
            if (senderForce == null)
                return;

            // 创建外交行为
            DiplomacyActionBase action = CreateDiplomacyAction(actionType, senderForce, receiverForce, person, resourceValue);
            if (action == null)
                return;

            // 使用 PerformWithoutCheck 直接执行，不检查条件
            action.PerformWithoutCheck(success);

            // 武将返回所属城市（任务完成后自动返回）
        }

        public bool ExecuteDiplomacyMission(Person person, DiplomacyActionType actionType, Force receiverForce, int resourceValue)
        {
            if (person == null || receiverForce == null)
                return false;

            Force senderForce = person.mBelongForce;
            if (senderForce == null)
                return false;

            // 创建外交行为
            DiplomacyActionBase action = CreateDiplomacyAction(actionType, senderForce, receiverForce, person, resourceValue);
            if (action == null)
                return false;

            // 执行外交行为
            bool success = action.Perform();

            // 武将返回所属城市（任务完成后自动返回）

            return success;
        }

        /// <summary>
        /// 执行完整的外交流程
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="actionType">外交行动类型</param>
        /// <returns>是否成功</returns>
        public bool ExecuteDiplomacyProcess(Force force, DiplomacyActionType actionType)
        {
            // 步骤1：确定是否需要外交
            if (!DetermineIfDiplomacyNeeded(force))
                return false;

            // 步骤2：选定外交对象
            Force target = SelectDiplomacyTarget(force, actionType);
            if (target == null)
                return false;

            // 步骤3：选定执行外交的武将
            Person diplomat = SelectDiplomat(force, actionType);
            if (diplomat == null)
                return false;

            // 创建外交行为
            DiplomacyActionBase action = CreateDiplomacyAction(actionType, force, target, diplomat);
            if (action == null)
                return false;

            // 步骤4：计算外交可能性
            int successRate = CalculateDiplomacySuccessRate(action);

            // 步骤5：派遣武将执行外交任务
            if (!DispatchDiplomat(action))
                return false;

            return true;
        }

        /// <summary>
        /// 完整外交流程执行方法（支持玩家决策）
        /// </summary>
        /// <param name="action">外交行为</param>
        /// <param name="playerDecision">玩家决策（如果外交对象是玩家，需要玩家确定）</param>
        /// <returns>外交结果</returns>
        public DiplomacyResult ExecuteDiplomacyFlow(DiplomacyActionBase action, bool? playerDecision = null)
        {
            DiplomacyResult result = new DiplomacyResult();
            result.Action = action;

            // 步骤1: 检查大边界范围（金钱、概率等）
            if (!CheckBoundaryConditions(action, result))
            {
                result.Success = false;
                result.Message = "边界条件不满足";
                return result;
            }

            // 步骤2: 检查是否有执行当前外交行为的需求
            if (!CheckDiplomacyNeed(action, result))
            {
                result.Success = false;
                result.Message = "没有执行此外交行为的需求";
                return result;
            }

            // 步骤3: 细节判断决定外交对象
            if (!CheckTargetValidity(action, result))
            {
                result.Success = false;
                result.Message = "外交对象无效";
                return result;
            }

            // 步骤4: 判断外交豁免与是否重复
            if (!CheckDiplomacyExemption(action, result))
            {
                result.Success = false;
                result.Message = "存在外交豁免或重复操作";
                return result;
            }

            // 步骤5: 准备外交武将
            if (!PrepareDiplomat(action, result))
            {
                result.Success = false;
                result.Message = "无法准备外交武将";
                return result;
            }

            // 步骤6: 派遣武将执行任务（玩家流程从这里开始）
            if (!DispatchDiplomat(action))
            {
                result.Success = false;
                result.Message = "派遣武将失败";
                return result;
            }

            // 步骤7: 武将执行任务到达外交对象主公所在城市（由武将任务系统完成）
            // 这里只是标记到达，实际移动由任务系统处理

            // 步骤8: 外交对象根据所有外交数据计算外交同意概率
            bool success = CalculateDiplomacyResult(action, playerDecision);
            result.Success = success;

            // 步骤9: 拿到外交结果执行外交逻辑
            ExecuteDiplomacyLogic(action, success);

            // 步骤10: 武将返回（由武将任务系统完成）
            result.Message = success ? "外交成功" : "外交失败";
            return result;
        }

        /// <summary>
        /// 步骤1: 检查大边界范围（金钱、实力、边境状态、战争状态等）
        /// </summary>
        private bool CheckBoundaryConditions(DiplomacyActionBase action, DiplomacyResult result)
        {
            if (action == null || action.Sender == null || action.Receiver == null)
                return false;

            // 检查势力是否存活
            if (!action.Sender.IsAlive || !action.Receiver.IsAlive)
            {
                result.ErrorCode = DiplomacyError.ForceNotAlive;
                return false;
            }

            // 检查资金
            int requiredGold = action.ResourceValue;
            if (requiredGold > 0)
            {
                City paymentCity = action.Diplomat?.mBelongCity ?? action.Sender.CapitalCity;
                if (paymentCity == null || paymentCity.gold < requiredGold)
                {
                    result.ErrorCode = DiplomacyError.InsufficientFunds;
                    return false;
                }
            }

            // 检查自身实力（是否有足够的城市和兵力）
            if (!CheckSelfStrength(action.Sender, result))
                return false;

            // 检查边境状态（是否有共同边界或邻近城市）
            if (!CheckBorderStatus(action.Sender, action.Receiver, result))
                return false;

            // 检查战争状态
            if (!CheckWarStatus(action.Sender, action.Receiver, action.ActionType, result))
                return false;

            // 检查弱势判断（弱势势力可能无法执行某些外交行为）
            if (!CheckWeakForceStatus(action.Sender, action.Receiver, action.ActionType, result))
                return false;

            return true;
        }

        /// <summary>
        /// 检查自身实力
        /// </summary>
        private bool CheckSelfStrength(Force force, DiplomacyResult result)
        {
            // 检查是否有足够的城市
            if (force.CityCount == 0)
            {
                result.ErrorCode = DiplomacyError.NoCities;
                return false;
            }

            // 检查是否有足够的兵力（至少需要有一些军队）
            int totalTroops = 0;
            int cityCount = 0;
            force.ForEachCity(city =>
            {
                totalTroops += city.troops;
                cityCount++;
            });
            
            // 如果是弱小势力，可能无法执行某些外交行为
            if (totalTroops == 0 && cityCount <= 1)
            {
                result.ErrorCode = DiplomacyError.TooWeak;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查边境状态（是否有共同边界或邻近城市）
        /// </summary>
        private bool CheckBorderStatus(Force sender, Force receiver, DiplomacyResult result)
        {
            // 如果是宣战行为，需要有共同边界或邻近城市
            // 其他外交行为则不需要严格的边界要求

            // 检查是否有共同边界
            bool hasBorder = false;
            sender.ForEachCity(senderCity =>
            {
                foreach (City neighbor in senderCity.NeighborList)
                {
                    if (neighbor.mBelongForce == receiver)
                    {
                        hasBorder = true;
                        return;
                    }
                }
            });

            // 如果没有共同边界，设置警告但不阻止
            if (!hasBorder)
            {
                result.ErrorCode = DiplomacyError.NoBorder;
            }

            return true;
        }

        /// <summary>
        /// 检查战争状态
        /// </summary>
        private bool CheckWarStatus(Force sender, Force receiver, DiplomacyActionType actionType, DiplomacyResult result)
        {
            // 检查是否正在与目标势力交战
            bool isAtWar = sender.IsEnemy(receiver);

            switch (actionType)
            {
                case DiplomacyActionType.DeclareWar:
                    // 如果已经在战争中，不能再次宣战
                    if (isAtWar)
                    {
                        result.ErrorCode = DiplomacyError.AlreadyAtWar;
                        return false;
                    }
                    break;

                case DiplomacyActionType.Alliance:
                    // 如果正在交战，不能结盟
                    if (isAtWar)
                    {
                        result.ErrorCode = DiplomacyError.AtWarCannotAlliance;
                        return false;
                    }
                    break;

                case DiplomacyActionType.Truce:
                    // 如果不在战争中，不需要停战
                    if (!isAtWar)
                    {
                        result.ErrorCode = DiplomacyError.NotAtWarCannotTruce;
                        return false;
                    }
                    break;

                case DiplomacyActionType.SendGift:
                    // 送礼在战争中也可以进行
                    break;
            }

            return true;
        }

        /// <summary>
        /// 检查弱势判断
        /// </summary>
        private bool CheckWeakForceStatus(Force sender, Force receiver, DiplomacyActionType actionType, DiplomacyResult result)
        {
            // 计算双方实力对比
            int senderStrength = CalculateForceStrength(sender);
            int receiverStrength = CalculateForceStrength(receiver);

            // 实力差距过大时的限制
            float strengthRatio = (float)senderStrength / (float)receiverStrength;

            switch (actionType)
            {
                case DiplomacyActionType.DeclareWar:
                    // 弱势势力不能向强大势力宣战（实力差距超过5倍）
                    if (strengthRatio < 0.2f)
                    {
                        result.ErrorCode = DiplomacyError.TooWeakToDeclareWar;
                        return false;
                    }
                    break;

                case DiplomacyActionType.Alliance:
                    // 实力差距过大时结盟成功率会降低，但不阻止
                    break;

                case DiplomacyActionType.Truce:
                    // 弱势势力更需要停战，允许
                    break;

                case DiplomacyActionType.SendGift:
                    // 送礼总是允许
                    break;
            }

            return true;
        }

        /// <summary>
        /// 计算势力实力
        /// </summary>
        private int CalculateForceStrength(Force force)
        {
            if (force == null)
                return 0;

            int strength = 0;

            // 城市数量
            strength += force.CityCount * 100;

            // 兵力数量
            force.ForEachCity(city =>
            {
                strength += city.troops;
            });

            // 武将数量（优秀武将加成）
            force.ForEachPerson(person =>
            {
                if (person.IsFree && person.Politics > 80)
                {
                    strength += 50;
                }
            });

            return strength;
        }

        /// <summary>
        /// 步骤2: 检查是否有执行当前外交行为的需求
        /// </summary>
        private bool CheckDiplomacyNeed(DiplomacyActionBase action, DiplomacyResult result)
        {
            // 默认允许所有外交行为
            // 子类可以重写此逻辑来实现特定需求检查
            return true;
        }

        /// <summary>
        /// 步骤3: 细节判断决定外交对象
        /// </summary>
        private bool CheckTargetValidity(DiplomacyActionBase action, DiplomacyResult result)
        {
            // 检查是否为自己
            if (action.Sender.Id == action.Receiver.Id)
            {
                result.ErrorCode = DiplomacyError.SelfDiplomacy;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 步骤4: 判断外交豁免与是否重复
        /// </summary>
        private bool CheckDiplomacyExemption(DiplomacyActionBase action, DiplomacyResult result)
        {
            // 检查是否存在相同的外交任务正在进行
            if (IsDiplomacyInProgress(action.Sender, action.Receiver, action.ActionType))
            {
                result.ErrorCode = DiplomacyError.DiplomacyInProgress;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 步骤5: 准备外交武将
        /// </summary>
        private bool PrepareDiplomat(DiplomacyActionBase action, DiplomacyResult result)
        {
            if (action.Diplomat != null)
                return true;

            // 如果没有指定武将，尝试自动选择
            Person diplomat = SelectDiplomat(action.Sender);
            if (diplomat != null)
            {
                action.Diplomat = diplomat;
                return true;
            }

            result.ErrorCode = DiplomacyError.NoDiplomatAvailable;
            return false;
        }

        /// <summary>
        /// 步骤8: 根据所有外交数据计算外交同意概率
        /// </summary>
        /// <param name="action">外交行为</param>
        /// <param name="playerDecision">玩家决策（如果外交对象是玩家）</param>
        /// <returns>外交结果</returns>
        public bool CalculateDiplomacyResult(DiplomacyActionBase action, bool? playerDecision = null)
        {
            // 如果外交对象是玩家，使用玩家的决策
            if (playerDecision.HasValue)
            {
                return playerDecision.Value;
            }

            // 计算成功率
            action.CalculateSuccessRate();
            
            // 根据成功率随机决定结果
            return GameRandom.Chance(action.SuccessRate);
        }

        /// <summary>
        /// 步骤9: 执行外交逻辑
        /// </summary>
        /// <param name="action">外交行为</param>
        /// <param name="success">是否成功</param>
        public void ExecuteDiplomacyLogic(DiplomacyActionBase action, bool success)
        {
            // 使用 PerformWithoutCheck 直接执行
            action.PerformWithoutCheck(success);
        }

        /// <summary>
        /// 检查是否有相同的外交任务正在进行
        /// </summary>
        private bool IsDiplomacyInProgress(Force sender, Force receiver, DiplomacyActionType actionType)
        {
            // 检查武将是否有正在进行的外交任务
            bool inProgress = false;
            sender.ForEachPerson(person =>
            {
                if (person.missionType == (int)MissionType.PersonDiplomacy && 
                    person.missionTarget == receiver.Id &&
                    (DiplomacyActionType)person.missionParams1 == actionType)
                {
                    inProgress = true;
                }
            });
            return inProgress;
        }

        /// <summary>
        /// 选择外交武将（无参数版本）
        /// </summary>
        private Person SelectDiplomat(Force force)
        {
            // 优先选择政治属性高的武将
            Person bestDiplomat = null;
            int bestPolitics = 0;

            force.ForEachPerson(person =>
            {
                if (person.IsFree && person.Politics > bestPolitics)
                {
                    bestPolitics = person.Politics;
                    bestDiplomat = person;
                }
            });

            return bestDiplomat;
        }
    }

    /// <summary>
    /// 外交结果类
    /// </summary>
    public class DiplomacyResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 执行的外交行为
        /// </summary>
        public DiplomacyActionBase Action { get; set; }

        /// <summary>
        /// 错误代码
        /// </summary>
        public DiplomacyError ErrorCode { get; set; }
    }

    /// <summary>
    /// 外交错误枚举
    /// </summary>
    public enum DiplomacyError
    {
        /// <summary>
        /// 无错误
        /// </summary>
        None,

        /// <summary>
        /// 资金不足
        /// </summary>
        InsufficientFunds,

        /// <summary>
        /// 势力不存在或已灭亡
        /// </summary>
        ForceNotAlive,

        /// <summary>
        /// 不能对自己执行外交
        /// </summary>
        SelfDiplomacy,

        /// <summary>
        /// 外交任务正在进行中
        /// </summary>
        DiplomacyInProgress,

        /// <summary>
        /// 没有可用的外交武将
        /// </summary>
        NoDiplomatAvailable,

        /// <summary>
        /// 没有城市
        /// </summary>
        NoCities,

        /// <summary>
        /// 势力太弱
        /// </summary>
        TooWeak,

        /// <summary>
        /// 没有共同边界
        /// </summary>
        NoBorder,

        /// <summary>
        /// 已经在战争中
        /// </summary>
        AlreadyAtWar,

        /// <summary>
        /// 交战中不能结盟
        /// </summary>
        AtWarCannotAlliance,

        /// <summary>
        /// 不在战争中不能停战
        /// </summary>
        NotAtWarCannotTruce,

        /// <summary>
        /// 实力太弱无法宣战
        /// </summary>
        TooWeakToDeclareWar
    }
}