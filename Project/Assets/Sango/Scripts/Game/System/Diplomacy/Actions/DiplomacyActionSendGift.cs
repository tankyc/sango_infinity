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
            if (Diplomat != null && Diplomat.BelongCity != null)
            {
                // 如果有执行外交的武将，检查武将所属城市的资金
                availableGold = Diplomat.BelongCity.gold;
            }
            else if (Sender.CapitalCity != null)
            {
                // 否则检查发送方首都的资金
                availableGold = Sender.CapitalCity.gold;
            }

            if (availableGold < ResourceValue)
                return false;

            // 【原版对齐】友好度已达上限时不可再赠送
            // （原版 get_donatable_force_list 会把 like >= max_force_like_ 的势力直接剔除，:376-383）
            if (Sender.GetLike(Receiver) >= DiplomacyRules.MaxLike)
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
            if (Diplomat != null && Diplomat.BelongCity != null)
            {
                paymentCity = Diplomat.BelongCity;
            }
            else if (Sender != null && Sender.CapitalCity != null)
            {
                paymentCity = Sender.CapitalCity;
            }

            // 扣除资金
            if (paymentCity != null && paymentCity.gold >= ResourceValue)
            {
                paymentCity.gold -= ResourceValue;

                Sango.Log.Info($"@外交@{Sender.Name} 送礼花费 {ResourceValue} 金");
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

            // 计算友好度增量（like 单位，2~20）
            int likeGain = CalculateDonateLikeGain();

            Receiver.CapitalCity.gold += ResourceValue;

            // 写入友好度：走 Force.AddLike（内部换算回 RelationMap 的 -5000~5000）
            Sender.AddLike(Receiver, likeGain);

            Sango.Log.Info($"@外交@{Sender.Name} 向 {Receiver.Name} 赠送了 {ResourceValue} 金，" +
                           $"友好度 +{likeGain}（当前 {Sender.GetLike(Receiver)}）");

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

            // 计算友好度增量（like 单位，2~20）
            int likeGain = CalculateDonateLikeGain();

            // 写入友好度：走 Force.AddLike（内部换算回 RelationMap）
            Sender.AddLike(Receiver, likeGain);

            Sango.Log.Info($"@外交@{Sender.Name} 向 {Receiver.Name} 赠送了 {ResourceValue} 金，" +
                           $"友好度 +{likeGain}（当前 {Sender.GetLike(Receiver)}）");

            // 触发事件
            GameEvent.OnDiplomacySendGift?.Invoke(Sender, Receiver, ResourceValue, true);
        }

        /// <summary>
        /// 送礼带来的友好度增量 —— 原版 `calc_donate_like_change`（s11_sys_person_order_diplomacy.cpp:385-406）。
        ///
        /// 【为什么整体替换旧实现】旧版是"每 10 金 +1 关系 + 势力贫富/强弱/性格/关系四类修正"的
        /// 自造公式，结果落在 RelationMap 的 -5000~5000 尺度上，与 Open311 的 like（0~100）完全不同调，
        /// 而且它把"送多少钱"当成了主要变量 —— 原版送礼金额只影响花费，不直接放大友好度。
        ///
        /// 原版只看三样（详见 DiplomacyRules.CalcDonateLikeGain）：
        ///   · 使者政治；
        ///   · 双方君主相性差（上限 50，厌恶则强制 50）；
        ///   · 目标君主对**使者本人**的亲爱 / 厌恶 / 义兄弟 / 夫妻关系。
        /// 返回 2~20（like 单位），由调用方用 <see cref="Force.AddLike"/> 写入。
        /// </summary>
        /// <returns>友好度增量（0~100 尺度）</returns>
        private int CalculateDonateLikeGain()
        {
            return DiplomacyRules.CalcDonateLikeGain(
                Diplomat,
                Sender != null ? Sender.mGovernor : null,
                Receiver != null ? Receiver.mGovernor : null);
        }
    }
}