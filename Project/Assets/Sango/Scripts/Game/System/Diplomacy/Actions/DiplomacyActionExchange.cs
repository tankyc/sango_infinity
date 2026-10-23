namespace Sango.Core
{
    /// <summary>
    /// 俘虏交换（Exchange）—— 对齐 Open311 的 `try_exchange`
    /// （`s11_sys_person_order_diplomacy.cpp:1631-1841`）。
    ///
    /// 【流程】
    ///   1. 前置：己方手里有俘虏（作为筹码），资金足够；
    ///   2. 判定：`n = 政治 + rel_coef + 释放者价值/关系系数 - 相性/2 + 金/40`，
    ///      对比 `g = (价值²)/400 - like/2 + 100`；
    ///   3. 成功：双方 `PersonStateSystem.Release` 交换俘虏，技术点 +30；
    ///   4. 失败：目标为玩家时进入禁谈期（本项目目标若是玩家由 UI 走人工确认，不走本类）。
    ///
    /// 【与原版的差异】
    ///   · 原版支持"只索要、不带回"（`release == nullptr`），这里同样允许 `OfferCaptive == null`；
    ///   · 原版 `get_horyo_value` 的具体权重未还原，本项目用五维均值近似（见 `DiplomacyRules.PersonValue`）。
    /// </summary>
    public class DiplomacyActionExchange : DiplomacyActionBase
    {
        /// <summary>我方交出的俘虏（可为 null = 只索要）</summary>
        public Person OfferCaptive { get; set; }

        /// <summary>我方索要的对方俘虏</summary>
        public Person RequestCaptive { get; set; }

        public DiplomacyActionExchange(Force sender, Force receiver, Person diplomat = null, int resourceValue = 0)
            : base(DiplomacyActionType.Exchange, sender, receiver, diplomat, resourceValue)
        {
            if (resourceValue == 0)
                ResourceValue = 1000;

            PickDefaultCaptives();
        }

        /// <summary>
        /// 默认挑选：我方俘虏里价值最高的作为筹码，对方俘虏里价值最高的作为索要目标。
        /// 玩家界面后续可以覆盖这两个字段。
        /// </summary>
        void PickDefaultCaptives()
        {
            if (OfferCaptive == null && Sender != null && Sender.BeCaptiveList != null)
            {
                Sender.BeCaptiveList.ForEach(p =>
                {
                    if (p == null || !p.IsAlive) return;
                    if (OfferCaptive == null || DiplomacyRules.PersonValue(p) > DiplomacyRules.PersonValue(OfferCaptive))
                        OfferCaptive = p;
                });
            }

            if (RequestCaptive == null && Receiver != null && Receiver.BeCaptiveList != null)
            {
                Receiver.BeCaptiveList.ForEach(p =>
                {
                    if (p == null || !p.IsAlive) return;
                    if (RequestCaptive == null || DiplomacyRules.PersonValue(p) > DiplomacyRules.PersonValue(RequestCaptive))
                        RequestCaptive = p;
                });
            }
        }

        public override bool CanPerform()
        {
            if (Sender == null || Receiver == null || Sender == Receiver)
                return false;

            // 至少要有一个方向（原版允许 release == nullptr 的"只索要"模式：
            // 不交还俘虏、只出钱把自家俘虏赎回来，AI 的赎买逻辑就复用这一种）
            if (OfferCaptive == null && RequestCaptive == null)
                return false;

            // 资金检查（原版 validate_exchange_command：金 ≥ 1000，:1631-1642）
            int availableGold = Diplomat != null && Diplomat.BelongCity != null
                ? Diplomat.BelongCity.gold
                : (Sender.CapitalCity != null ? Sender.CapitalCity.gold : 0);

            return availableGold >= ResourceValue;
        }

        public override int CalculateSuccessRate()
        {
            DiplomacyRules.Result result = DiplomacyRules.TryExchange(
                Diplomat,
                Sender != null ? Sender.mGovernor : null,
                Receiver != null ? Receiver.mGovernor : null,
                Sender,
                Receiver,
                OfferCaptive,
                RequestCaptive,
                ResourceValue,
                Scenario.Cur.Variables.difficulty);

            LastResult = result;
            SuccessRate = DiplomacyRules.ToSuccessRate(result);
            return SuccessRate;
        }

        public override bool Perform()
        {
            if (!CanPerform())
            {
                GameEvent.OnDiplomacyExchange?.Invoke(Sender, Receiver, ResourceValue, false);
                return false;
            }

            int rate = CalculateSuccessRate();

            // 原版 try_exchange 返回 Debate → 确定性进入舌战（不掷骰、不落地失败）
            if (TryHandOverToDebate()) return false;

            bool success = rate >= 100 || GameRandom.Chance(rate);
            PerformWithoutCheck(success);
            return success;
        }

        public override void PerformWithoutCheck(bool success)
        {
            if (!success)
            {
                Sango.Log.Info($"@外交@{Sender.Name} 与 {Receiver.Name} 的俘虏交换被拒绝");

                // 广播失败事件：DebateTrigger 订阅它，按概率把这次失败转成舌战
                RaiseFailedEvent(0);
                GameEvent.OnDiplomacyExchange?.Invoke(Sender, Receiver, ResourceValue, false);
                return;
            }

            // 原版成功（:1797-1828）：双方释放，技术点 +30
            if (RequestCaptive != null && Sender.CapitalCity != null)
                PersonStateSystem.Release(RequestCaptive, Sender.CapitalCity, PersonChangeReason.Released);

            if (OfferCaptive != null && Receiver.CapitalCity != null)
                PersonStateSystem.Release(OfferCaptive, Receiver.CapitalCity, PersonChangeReason.Released);

            Sender.GainTechniquePoint(DiplomacyRules.CeasefireTechGain);

            Sango.Log.Info($"@外交@{Sender.Name} 与 {Receiver.Name} 完成俘虏交换：" +
                           $"交还 {OfferCaptive?.Name ?? "无"}，赎回 {RequestCaptive?.Name ?? "无"}");

            GameEvent.OnDiplomacyExchange?.Invoke(Sender, Receiver, ResourceValue, true);
        }
    }
}
