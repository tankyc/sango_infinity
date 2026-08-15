using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 送礼外交行为
    /// </summary>
    public class DiplomacyActionSendGift : DiplomacyActionBase
    {


        /// <summary>
        /// 初始化送礼外交行为
        /// </summary>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        /// <param name="giftValue">礼物价值</param>
        public DiplomacyActionSendGift(Force sender, Force receiver, Person diplomat = null, int giftValue = 0)
            : base(DiplomacyActionType.SendGift, sender, receiver, diplomat, giftValue)
        {
            // 如果没有指定礼物价值，使用配置的默认值
            if (giftValue == 0)
            {
                ResourceValue = Scenario.Cur.Variables.diplomacySendGiftAmount;
            }
        }

        /// <summary>
        /// 检查是否可以执行送礼行为
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

            return true;
        }

        /// <summary>
        /// 计算送礼成功率（送礼总是成功）
        /// </summary>
        /// <returns>成功率（0-100）</returns>
        public override int CalculateSuccessRate()
        {
            SuccessRate = 100; // 送礼总是成功
            return SuccessRate;
        }

        /// <summary>
        /// 派遣时触发的回调方法
        /// 处理送礼的资源消耗
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
                Sango.Log.Info($"@外交@{Sender.Name} 送礼花费 {ResourceValue} 金");
#endif
            }
        }

        /// <summary>
        /// 执行送礼行为
        /// </summary>
        /// <returns>是否成功</returns>
        public override bool Perform()
        {
            if (!CanPerform())
            {
                // 送礼失败不触发惩罚机制
                // 触发失败事件
                GameEvent.OnDiplomacySendGift?.Invoke(Sender, Receiver, ResourceValue, false);
                return false;
            }

            // 计算关系增加量（考虑多种因素）
            int relationIncrease = CalculateDynamicRelationIncrease();

            Receiver.CapitalCity.gold += ResourceValue;

            // 增加关系
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            diplomacyManager.AddRelation(Sender, Receiver, relationIncrease);

#if SANGO_DEBUG
            Sango.Log.Info($"@外交@{Sender.Name} 向 {Receiver.Name} 赠送了 {ResourceValue} 金，关系增加了 {relationIncrease}!!");
#endif

            // 触发事件
            GameEvent.OnDiplomacySendGift?.Invoke(Sender, Receiver, ResourceValue, true);

            return true;
        }

        /// <summary>
        /// 不检查条件直接执行送礼行为
        /// </summary>
        /// <param name="success">执行结果（true表示成功，false表示失败）</param>
        public override void PerformWithoutCheck(bool success)
        {
            if (!success)
            {
                // 送礼失败不触发惩罚机制
                // 触发失败事件
                GameEvent.OnDiplomacySendGift?.Invoke(Sender, Receiver, ResourceValue, false);
                return;
            }

            Receiver.CapitalCity.gold += ResourceValue;

            // 计算关系增加量（考虑多种因素）
            int relationIncrease = CalculateDynamicRelationIncrease();

            // 增加关系
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            diplomacyManager.AddRelation(Sender, Receiver, relationIncrease);

#if SANGO_DEBUG
            Sango.Log.Info($"@外交@{Sender.Name} 向 {Receiver.Name} 赠送了 {ResourceValue} 金，关系增加了 {relationIncrease}!!");
#endif

            // 触发事件
            GameEvent.OnDiplomacySendGift?.Invoke(Sender, Receiver, ResourceValue, true);
        }

        /// <summary>
        /// 动态计算送礼的关系增加量
        /// 考虑因素：对象势力的金钱、势力对比、主公喜好、性格等
        /// </summary>
        /// <returns>关系增加量</returns>
        private int CalculateDynamicRelationIncrease()
        {
            ScenarioVariables variables = Scenario.Cur.Variables;
            
            // 基础关系增加量（每10金增加1点关系）
            int baseIncrease = ResourceValue / variables.diplomacySendGiftRelationFactor;
            
            // 计算各种因素的修正系数
            float factor = 1.0f;
            
            // 1. 对象势力的金钱因素：越穷的势力越看重礼物
            int receiverTotalGold = 0;
            Receiver.ForEachCity(city =>
            {
                receiverTotalGold += city.gold;
            });
            
            // 如果接收方很穷，增加礼物效果
            if (receiverTotalGold< 5000)
            {
                factor += (5000 - receiverTotalGold) / 10000.0f; // 最多增加0.5倍效果
            }
            // 如果接收方很富，减少礼物效果
            else if (receiverTotalGold >50000)
            {
                factor -= (receiverTotalGold - 50000) / 200000.0f; // 最多减少0.25倍效果
                factor = Mathf.Max(factor, 0.75f); // 最低保持0.75倍效果
            }
            
            // 2. 势力对比因素：弱小势力向强大势力送礼效果更好
            int senderPower = Sender.FightPower >0 ? Sender.FightPower : 1;
            int receiverPower = Receiver.FightPower > 0 ? Receiver.FightPower : 1;
            float powerRatio = (float)senderPower / receiverPower;
            
            // 当发送方势力较弱时，增加礼物效果
            if (powerRatio< 0.5f)
            {
                factor += (0.5f - powerRatio) * 0.4f; // 最多增加0.2倍效果
            }
            // 当发送方势力较强时，减少礼物效果
            else if (powerRatio >2.0f)
            {
                factor -= (powerRatio - 2.0f) * 0.1f; // 最多减少0.2倍效果
                factor = Mathf.Max(factor, 0.8f); // 最低保持0.8倍效果
            }
            
            // 3. 主公喜好和性格因素
            if (Receiver.mGovernor != null)
            {
                Person governor = Receiver.mGovernor;
                
                // 性格影响：使用Personality类中的送礼效果加成参数
                if (governor.mPersonality != null)
                {
                    // 获取性格的送礼效果加成（百分比）
                    int giftEffectBonus = governor.mPersonality.giftEffectAdd;
                    // 将百分比转换为系数（例如：10表示+10%，即0.1）
                    float bonusFactor = giftEffectBonus / 100.0f;
                    factor += bonusFactor;
                }
                
                // 相性影响：相性好的话效果更好
                if (Sender.mGovernor != null)
                {
                    int compatibilityDiff = System.Math.Abs(Sender.mGovernor.compatibility - governor.compatibility);
                    if (compatibilityDiff< 30)
                    {
                        factor += (30 - compatibilityDiff) / 300.0f; // 最多增加0.1倍效果
                    }
                }
                
                // 政治属性影响：政治高的主公更看重外交
                factor += governor.Politics / 1000.0f; // 最多增加0.1倍效果
            }
            
            // 4. 关系基础影响：关系越差，送礼效果越好
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            int currentRelation = diplomacyManager.GetRelation(Sender, Receiver);
            if (currentRelation < 0)
            {
                factor += Mathf.Abs(currentRelation) / 2000.0f; // 最多增加0.5倍效果
            }
            else if (currentRelation >1000)
            {
                factor -= currentRelation / 4000.0f; // 最多减少0.25倍效果
                factor = Mathf.Max(factor, 0.75f); // 最低保持0.75倍效果
            }
            
            // 计算最终关系增加量
            int finalIncrease = Mathf.RoundToInt(baseIncrease * factor);
            
            // 确保关系增加量至少为1
            return Mathf.Max(finalIncrease, 1);
        }
    }
}