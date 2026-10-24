using System.Collections.Generic;
using Sango.Core.Player;

namespace Sango.Core
{
    /// <summary>
    /// 城市计略的"事后反应"入口，注册在 Force.AIPrepare 的指令链上。
    ///
    /// 【职责只剩一件事】二虎竞食把两方交情打到 cityStrategyWarTriggerRelation 以下时，
    /// 被挑拨的一方在自己的势力回合里决定是否破盟宣战。
    /// 放在 AI 回合而不是计略结算帧，是为了不在 Person.UpdateMission 的循环里改同盟结构
    /// （同外交事件的取舍，见 CityStrategyManager 类注释）。
    ///
    /// 【为什么没有"主动施计"】Open311 的计略只由**据点命令**触发
    /// （`s11_game_ai.h:31`：AIOrderId_Strategy = 二虎竞食 / 驱虎吞狼 / 流言），
    /// 势力级没有独立的主动施计环节。
    /// 本项目曾在这里另有一套"记仇报复 + 主动流言 + 主动二虎竞食"的势力级实现，
    /// 与据点级 CityAIOutpostCommand.AIStrategy 叠加后同一势力每回合可能施计 2 次以上，
    /// 已整体删除。主动施计全部交给据点命令队列。
    /// </summary>
    public static class CityStrategyAI
    {
        /// <summary>
        /// AI 势力回合内的城市计略后效步骤。
        /// 约定与 ForceAI 里其它指令一致：返回 true 表示本步已处理完，可以让 DoAI 继续弹下一条指令。
        /// </summary>
        /// <param name="force">当前行动的势力</param>
        /// <param name="scenario">当前场景</param>
        /// <returns>恒为 true：本步骤从不挂起回合协程</returns>
        public static bool AICityStrategy(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;
            // 玩家侧的计略由界面选择器驱动，AI 不代劳
            if (force.IsPlayer)
                return true;
            // 全局和平回合内不破盟宣战，避免刚开局就出现势力互相宣战
            if (scenario.IsPeacePeriod)
                return true;
            if (force.mGovernor == null || force.CapitalCity == null)
                return true;

            TryBreakAllianceByStrategy(force, scenario);
            return true;
        }

        /// <summary>
        /// 破盟反应：本势力若带着"刚被二虎竞食挑拨"的凭据，且与对方仍是同盟、交情已跌破阈值，
        /// 就解除同盟并向对方宣战。
        /// 解约写法照抄既有的 AI 撕约实现（ForceAI.cs 的 CheckBreakAlliance 尾部），
        /// 宣战则复用 DiplomacyManager.PerformDiplomacyAction(DeclareWar)：
        /// 注意必须先解约再宣战，因为宣战的 CanPerform 会把"尚存任何协议"判为不可宣战。
        /// </summary>
        /// <param name="force">当前 AI 势力</param>
        /// <param name="scenario">当前场景</param>
        /// <returns>本回合执行了一次破盟（含随后的宣战）返回 true</returns>
        private static bool TryBreakAllianceByStrategy(Force force, Scenario scenario)
        {
            int triggerRelation = scenario.Variables.cityStrategyWarTriggerRelation;

            // 下面会真的从 AllianceList 里摘掉对象，所以先取快照再遍历，不能在 ForEach 里直接改
            List<Alliance> allianceSnapshot = new List<Alliance>();
            force.AllianceList.ForEach(alliance =>
            {
                if (alliance != null && alliance.IsAlive && alliance.allianceType == AllianceType.Alliance)
                    allianceSnapshot.Add(alliance);
            });

            for (int i = 0; i < allianceSnapshot.Count; i++)
            {
                Alliance alliance = allianceSnapshot[i];
                Force other = FindCounterpart(alliance, force);
                if (other == null || !other.IsAlive)
                    continue;

                // 凭据是一次性的：取不到（没被挑拨过、或已过期）就跳过，防止同一笔账反复放大
                if (!force.TakeStrategyWarSeed(other))
                    continue;
                if (scenario.GetRelation(force, other) > triggerRelation)
                    continue;

                // 解除同盟：置死并双向摘除，与 ForceAI 的撕约写法保持一致
                alliance.IsAlive = false;
                force.AllianceList.Remove(alliance);
                other.AllianceList.Remove(alliance);

                string headline = $"{force.ColorName}察觉与{other.ColorName}的交情已被人挑拨至冰点（关系 {scenario.GetRelation(force, other)}），遂解除同盟";

                // 解约后仍可能残留停战/通商协议，而宣战的 CanPerform 会把"尚存任何协议"判为不可宣战，
                // 那样走 Perform 只会触发一次"外交失败"的额外关系惩罚。这里先自查一次，只在确实无协议时才宣战。
                bool warDeclared = !force.HasActiveAgreement(other)
                    && GameSystem.GetSystem<DiplomacyManager>()
                        .PerformDiplomacyAction(DiplomacyActionType.DeclareWar, force, other);

                BroadcastToPlayer(headline + (warDeclared ? "并宣布出兵讨伐。" : "，但暂未宣战。"), force);

#if SANGO_DEBUG
                Sango.Log.Info($"@计略@二虎竞食后效：{force.Name} 与 {other.Name} 破盟，宣战={warDeclared}，关系={scenario.GetRelation(force, other)}");
#endif
                return true;
            }

            return false;
        }

        /// <summary>
        /// 取同盟对象里除了自己之外的另一方。同盟是两方结构，故取到第一个不是本方的势力即可。
        /// </summary>
        /// <param name="alliance">同盟对象</param>
        /// <param name="force">本势力</param>
        /// <returns>对方势力，异常结构下返回 null</returns>
        private static Force FindCounterpart(Alliance alliance, Force force)
        {
            Force counterpart = null;
            alliance.ForceList.ForEach(other =>
            {
                if (counterpart == null && other != null && other != force)
                    counterpart = other;
            });
            return counterpart;
        }

        /// <summary>
        /// 把 AI 侧的计略后效写进左下角信息窗口，坐标取该势力治所，玩家点击可跳转到事发城。
        /// 与 CityStrategyActionBase.BroadcastMessage 同一口径（PlayerMessage.AddTextMessage）。
        /// </summary>
        /// <param name="text">广播正文</param>
        /// <param name="force">消息归属势力</param>
        private static void BroadcastToPlayer(string text, Force force)
        {
            City capital = force.CapitalCity;
            PlayerMessage.AddTextMessage(text, force, capital != null ? capital.x : 0, capital != null ? capital.y : 0);
        }
    }
}
