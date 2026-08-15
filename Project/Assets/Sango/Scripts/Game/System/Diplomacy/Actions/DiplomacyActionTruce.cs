using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 停战外交行为
    /// </summary>
    public class DiplomacyActionTruce : DiplomacyActionBase
    {


        /// <summary>
        /// 初始化停战外交行为
        /// </summary>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        /// <param name="resourceValue">资源价值</param>
        public DiplomacyActionTruce(Force sender, Force receiver, Person diplomat = null, int resourceValue = 0)
            : base(DiplomacyActionType.Truce, sender, receiver, diplomat, resourceValue)
        {
            // 如果没有指定资源价值，使用默认值
            if (resourceValue == 0)
            {
                ResourceValue = 2000; // 默认停战费用
            }
        }

        /// <summary>
        /// 检查是否可以执行停战行为
        /// </summary>
        /// <returns>是否可以执行</returns>
        public override bool CanPerform()
        {
            if (Sender == null || Receiver == null || Sender == Receiver)
                return false;

            // 检查资金：优先检查执行外交的武将所属城市的资金
            int availableGold = 0;
            if (Diplomat != null && Diplomat.mCity != null)
            {
                // 如果有执行外交的武将，检查武将所属城市的资金
                availableGold = Diplomat.mCity.gold;
            }
            else if (Sender.CapitalCity != null)
            {
                // 否则检查发送方首都的资金
                availableGold = Sender.CapitalCity.gold;
            }

            if (availableGold < ResourceValue)
                return false;

            // 检查关系是否足够
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            int relation = diplomacyManager.GetRelation(Sender, Receiver);
            if (relation < Scenario.Cur.Variables.diplomacyTruceRelationThreshold)
                return false;

            // 检查是否已经有停战协议
            if (Sender.HasActiveAgreement(Receiver))
                return false;

            return true;
        }

        /// <summary>
        /// 计算停战成功率
        /// </summary>
        /// <returns>成功率（0-100）</returns>
        public override int CalculateSuccessRate()
        {
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            int relation = diplomacyManager.GetRelation(Sender, Receiver);
            ScenarioVariables variables = Scenario.Cur.Variables;

            // 关系值越高，成功率越高，最高80%
            SuccessRate = (int)Mathf.Clamp(variables.diplomacyTruceBaseSuccessRate + 
                (relation - variables.diplomacyTruceRelationThreshold) / variables.diplomacyTruceRelationFactor, 
                variables.diplomacyTruceMinSuccessRate, variables.diplomacyTruceMaxSuccessRate);

            // 使者能力加成
            if (Diplomat != null)
            {
                int diplomacyAbility = Diplomat.Politics + Diplomat.Glamour / 2;
                int abilityBonus = Mathf.Min(diplomacyAbility / 10, variables.diplomacyAbilityBonusMax);
                SuccessRate += abilityBonus;
            }

            // 金钱加成
            if (ResourceValue >0)
            {
                int resourceBonus = Mathf.Min(ResourceValue / variables.diplomacyResourceBonusFactor, variables.diplomacyResourceBonusMax);
                SuccessRate += resourceBonus;
            }

            // 确保成功率在合理范围内
            SuccessRate = Mathf.Clamp(SuccessRate, 0, 100);
            return SuccessRate;
        }

        /// <summary>
        /// 派遣时触发的回调方法
        /// 处理停战的资源消耗
        /// </summary>
        public override void OnDispatch()
        {
            // 调用基类方法
            base.OnDispatch();

            // 如果不需要资源，直接返回
            if (ResourceValue <= 0)
                return;

            // 确定扣除资金的城市：优先武将所属城市，其次首都
            City paymentCity = null;
            if (Diplomat != null && Diplomat.mCity != null)
            {
                paymentCity = Diplomat.mCity;
            }
            else if (Sender != null && Sender.CapitalCity != null)
            {
                paymentCity = Sender.CapitalCity;
            }

            // 扣除资金
            if (paymentCity != null && paymentCity.gold >= ResourceValue)
            {
                paymentCity.gold -= ResourceValue;

#if SANGO_DEBUG
                Sango.Log.Info($"@外交@{Sender.Name} 停战花费 {ResourceValue} 金");
#endif
            }
        }

        /// <summary>
        /// 执行停战行为
        /// </summary>
        /// <returns>是否成功</returns>
        public override bool Perform()
        {
            if (!CanPerform())
            {
                // 条件不满足也算失败，触发惩罚
                OnFailed();
                return false;
            }

            // 计算成功率
            CalculateSuccessRate();

            // 判断是否成功
            if (!GameRandom.Chance(SuccessRate))
            {
                // 成功率失败，触发惩罚
                OnFailed();
                return false;
            }

            // 创建停战协议
            Alliance truce = new Alliance()
            {
                ForceList = new SangoObjectList<Force>(),
                leftCount = Scenario.Cur.Variables.diplomacyTruceDuration, // 6个月
                allianceType = AllianceType.Truce, // 停战类型
                IsAlive = true
            };

            truce.ForceList.Add(Sender);
            truce.ForceList.Add(Receiver);

            // 添加到场景
            Scenario.Cur.Add(truce);

            // 添加到势力的联盟列表
            Sender.AllianceList.Add(truce);
            Receiver.AllianceList.Add(truce);

            // 增加关系
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            diplomacyManager.AddRelation(Sender, Receiver, Scenario.Cur.Variables.diplomacyTruceRelationIncrease);

#if SANGO_DEBUG
            Sango.Log.Info($"@外交@{Sender.Name} 与 {Receiver.Name} 达成了{Scenario.Cur.Variables.diplomacyTruceDuration / 3}个月的停战协议!!");
#endif

            // 触发事件
            GameEvent.OnDiplomacyTruce?.Invoke(Sender, Receiver, true);

            return true;
        }

        /// <summary>
        /// 不检查条件直接执行停战行为
        /// </summary>
        /// <param name="success">执行结果（true表示成功，false表示失败）</param>
        public override void PerformWithoutCheck(bool success)
        {
            if (!success)
            {
                // 失败时触发惩罚机制
                OnFailed();
                // 触发失败事件
                GameEvent.OnDiplomacyTruce?.Invoke(Sender, Receiver, false);
                return;
            }

            // 创建停战协议
            Alliance truce = new Alliance()
            {
                ForceList = new SangoObjectList<Force>(),
                leftCount = Scenario.Cur.Variables.diplomacyTruceDuration, // 6个月
                allianceType = AllianceType.Truce, // 停战类型
                IsAlive = true
            };

            truce.ForceList.Add(Sender);
            truce.ForceList.Add(Receiver);

            // 添加到场景
            Scenario.Cur.Add(truce);

            // 添加到势力的联盟列表
            Sender.AllianceList.Add(truce);
            Receiver.AllianceList.Add(truce);

            // 增加关系
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            diplomacyManager.AddRelation(Sender, Receiver, Scenario.Cur.Variables.diplomacyTruceRelationIncrease);

#if SANGO_DEBUG
            Sango.Log.Info($"@外交@{Sender.Name} 与 {Receiver.Name} 达成了{Scenario.Cur.Variables.diplomacyTruceDuration / 3}个月的停战协议!!");
#endif

            // 触发事件
            GameEvent.OnDiplomacyTruce?.Invoke(Sender, Receiver, true);
        }
    }
}