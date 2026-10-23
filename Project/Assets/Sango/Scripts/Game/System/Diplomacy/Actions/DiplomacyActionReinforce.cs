namespace Sango.Core
{
    /// <summary>
    /// 援军请求（Reinforce）—— 对齐 Open311 的 `try_reinforce`
    /// （`s11_sys_person_order_diplomacy.cpp:1999-2265`）。
    ///
    /// 【流程】
    ///   1. 前置：没有进行中的请求（`requestForceId &lt; 0`）、目标必须是同盟、
    ///      不处于禁谈期、首都存在；
    ///   2. 判定：`n = (rand+12)*政治/24 - 相性惩罚 + 金/100 + rel_coef + like`，
    ///      对比 `g = (武将数缺口)*5*2 + (兵力缺口)/120`；
    ///   3. 成功：写入 `requestForceId` / `requestTimer`（9 回合），同盟须在此期间出兵；
    ///   4. 失败：技术点 -25，且**强制进入禁谈期**（原版不区分目标是否为玩家，:2254-2264）。
    ///
    /// 【未完成】"同盟出兵"这一步需要部队部署系统配合（原版在源据点直接
    /// `create_reinforcement_unit` + `unit_reinforce`）。本项目先落状态与计时器，
    /// 出兵由 P3（AI）或玩家手动执行。
    /// </summary>
    public class DiplomacyActionReinforce : DiplomacyActionBase
    {
        public DiplomacyActionReinforce(Force sender, Force receiver, Person diplomat = null, int resourceValue = 0)
            : base(DiplomacyActionType.Reinforce, sender, receiver, diplomat, resourceValue)
        {
            if (resourceValue == 0)
                ResourceValue = 1000;
        }

        public override bool CanPerform()
        {
            if (Sender == null || Receiver == null || Sender == Receiver)
                return false;

            // 原版：已有进行中的援军请求时不能再提（:1999-2057）
            if (Sender.requestForceId >= 0 || Sender.requestTimer > 0)
                return false;

            // 只能向同盟请求援军
            if (!Sender.IsAlliance(Receiver))
                return false;

            // 禁谈期内不可提案
            if (Sender.diplomacyBanTimer > 0)
                return false;

            if (Sender.CapitalCity == null)
                return false;

            return true;
        }

        public override int CalculateSuccessRate()
        {
            City src = Sender.CapitalCity;
            int srcBushou = src != null && src.allPersons != null ? src.allPersons.Count : 0;

            // 兵力口径：原版用的是**源据点自身的兵力**（`self->src->get_troops()`，`:2472`），
            // 不是势力汇总。本项目对应 City.troops。
            int srcTroops = src != null ? src.troops : 0;

            int destLike = Receiver.GetLike(Sender);

            DiplomacyRules.Result result = DiplomacyRules.TryReinforce(
                Diplomat,
                Sender.mGovernor,
                Receiver.mGovernor,
                Sender,
                Receiver,
                ResourceValue,
                srcBushou,
                destLike,
                srcTroops,
                Scenario.Cur.Variables.difficulty);

            LastResult = result;
            SuccessRate = DiplomacyRules.ToSuccessRate(result);
            return SuccessRate;
        }

        public override bool Perform()
        {
            if (!CanPerform())
            {
                GameEvent.OnDiplomacyReinforce?.Invoke(Sender, Receiver, ResourceValue, false);
                return false;
            }

            int rate = CalculateSuccessRate();

            // 原版 try_reinforce 返回 Debate → 确定性进入舌战（不掷骰、不落地失败）
            if (TryHandOverToDebate()) return false;

            bool success = rate >= 100 || GameRandom.Chance(rate);
            PerformWithoutCheck(success);
            return success;
        }

        public override void PerformWithoutCheck(bool success)
        {
            if (!success)
            {
                // 原版失败：技术点 -25、金退回、**强制 ban 9**（不区分玩家，:2254-2264）
                Sender.GainTechniquePoint(-25);
                Sender.diplomacyBanTimer = DiplomacyRules.DiplomacyBanTurns;

                Sango.Log.Info($"@外交@{Sender.Name} 向 {Receiver.Name} 请求援军被拒，" +
                               $"技术点 -25，{DiplomacyRules.DiplomacyBanTurns} 回合内不可再提案");

                // 广播失败事件：DebateTrigger 订阅它，按概率把这次失败转成舌战
                RaiseFailedEvent(25);
                GameEvent.OnDiplomacyReinforce?.Invoke(Sender, Receiver, ResourceValue, false);
                return;
            }

            // 原版成功（:2204-2251）：技术点 +30，并写入 request（同盟 9 周内须出兵）
            Sender.GainTechniquePoint(DiplomacyRules.CeasefireTechGain);
            Sender.requestForceId = Receiver.Id;
            Sender.requestTimer = DiplomacyRules.DiplomacyBanTurns;

            Sango.Log.Info($"@外交@{Sender.Name} 向 {Receiver.Name} 请求援军成功，" +
                           $"约定 {Sender.requestTimer} 回合内出兵");
            GameEvent.OnDiplomacyReinforce?.Invoke(Sender, Receiver, ResourceValue, true);
        }
    }
}
