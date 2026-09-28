/*
 * 文件名：DebateConsequence.cs
 * 描述：舌战结果对"发起行为"的改判（A 方案）。
 *
 * 背景：舌战是"发起行为失败后按概率强制进入"的后续玩法（见 DebateTrigger）。
 *   舌战自身只结算参战武将（经验 / 功绩 / 伤病 / 技术点，见 Debate.ParamSetWinner），
 *   默认不改写发起行为的成败 —— 也就是"失败依旧失败"。
 *   按需求：发起方（挑战方，即舌战里的 characters[0]）**获胜时要把发起行为改判为成功**，本类负责那一步。
 *
 * 衔接方式：
 *   · DebateTrigger 在真正开局之前调用 RegisterRecruit / RegisterDiplomacy 登记一条"待改判"；
 *   · 本类订阅 GameEvent.OnDebateEnd，舌战结束时若挑战方获胜就执行改判，随后清空登记；
 *   · 若开局被否决（Request 返回 false），DebateTrigger 会调 ClearPending 撤掉登记。
 *
 * 改判规则：
 *   · 登用失败：让目标武将 BeRecruit（与 JobRecruitPerson 的成功分支同一个动作）；
 *   · 外交交涉失败：先返还失败时扣掉的关系值，再用 action.PerformWithoutCheck(true)
 *     把这次外交按"成功"重新落地（结盟 / 停战 / 通商等效果由各 Action 自己实现）。
 *
 * 只登记一条、不用队列：同一时刻只可能有一场舌战（DebateManager.IsDebating 与
 * DebateChallengeFlow.IsPending 双闸），且登记发生在开局之前。结束时再做一次"是否同一对武将"的校验，
 * 防止 Abort（强退 / 读档，不发 OnDebateEnd）留下过期登记后误伤下一场舌战。
 */

using System;

namespace Sango.Core.Debate
{
    /// <summary>舌战结果对发起行为的改判（详见文件头注释）</summary>
    public static class DebateConsequence
    {
        /// <summary>待改判的发起行为类型</summary>
        private enum Kind
        {
            None,
            /// <summary>登用失败</summary>
            Recruit,
            /// <summary>外交交涉失败</summary>
            Diplomacy,
        }

        private static bool s_installed;

        private static Kind s_kind = Kind.None;
        private static Person s_challenger;
        private static Person s_challenged;

        // 登用改判用（RegisterRecruit）
        private static Person s_recruiter;
        private static Person s_recruitTarget;
        private static City s_recruitCity;

        // 外交改判用（RegisterDiplomacy）
        private static DiplomacyActionBase s_action;
        private static int s_penalty;

        /// <summary>是否有待改判（调试 / 测试用）</summary>
        public static bool HasPending { get { return s_kind != Kind.None; } }

        /// <summary>
        /// 改判判定成立、即将执行时的回调（text = 结果文本，anchor = 发起方武将）。
        /// 纯观察点：置空则什么都不做（本类不会自己写战报）；需要向玩家播报的接入方挂它即可，
        /// 单元测试也用它断言"这一场到底改判了没有"。
        /// </summary>
        public static System.Action<string, Person> OnRewritten;

        /// <summary>订阅舌战结束事件（由 DebateIntegration.Install 调用一次）</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;

            GameEvent.OnDebateEnd += OnDebateEnd;
        }

        #region 登记

        /// <summary>
        /// 登记"登用失败"的改判。舌战挑战方 = 招募者，应战方 = 被登用者。
        /// </summary>
        /// <param name="recruiter">招募者（执行登用的武将）</param>
        /// <param name="target">被招募者</param>
        /// <param name="targetCity">登用成功后目标加入的城市（即 JobRecruitPerson 的 targetCity）</param>
        public static void RegisterRecruit(Person recruiter, Person target, City targetCity)
        {
            s_kind = Kind.Recruit;
            s_challenger = recruiter;
            s_challenged = target;
            s_recruiter = recruiter;
            s_recruitTarget = target;
            s_recruitCity = targetCity;
            s_action = null;
            s_penalty = 0;
        }

        /// <summary>
        /// 登记"外交交涉失败"的改判。舌战挑战方 = 使者，应战方 = 对方代表。
        /// </summary>
        /// <param name="challenger">使者</param>
        /// <param name="challenged">对方代表（接收方君主）</param>
        /// <param name="action">外交行为本体（改判时用它按成功重新落地）</param>
        /// <param name="penalty">失败时已经扣掉的关系值（改判时原样返还）</param>
        public static void RegisterDiplomacy(Person challenger, Person challenged, DiplomacyActionBase action, int penalty)
        {
            s_kind = Kind.Diplomacy;
            s_challenger = challenger;
            s_challenged = challenged;
            s_recruiter = null;
            s_recruitTarget = null;
            s_recruitCity = null;
            s_action = action;
            s_penalty = penalty;
        }

        /// <summary>撤掉登记（开局被否决时调用，也可用于外部强制清理）</summary>
        public static void ClearPending()
        {
            s_kind = Kind.None;
            s_challenger = null;
            s_challenged = null;
            s_recruiter = null;
            s_recruitTarget = null;
            s_recruitCity = null;
            s_action = null;
            s_penalty = 0;
        }

        #endregion

        #region 结束处理

        private static void OnDebateEnd(Debate debate)
        {
            if (s_kind == Kind.None) return;

            // 先取出并立刻清空：这条登记只对应这一场舌战，无论改判是否成立都不再复用
            Kind kind = s_kind;
            Person challenger = s_challenger;
            Person challenged = s_challenged;
            Person recruiter = s_recruiter;
            Person target = s_recruitTarget;
            City city = s_recruitCity;
            DiplomacyActionBase action = s_action;
            int penalty = s_penalty;
            ClearPending();

            if (debate == null) return;

            // 登记必须与这场舌战是同一对武将，否则是 Abort 留下的过期登记
            if (!IsSameMatch(debate, challenger, challenged))
            {
                Sango.Log.Warning("【舌战】结束的舌战与登记的发起行为不匹配，跳过改判。");
                return;
            }

            // 只有"发起方（挑战方）获胜"才改判；发起方落败则维持原结果（失败）
            if (!debate.ParamIsChallengerWin(debate.DebateParam))
            {
                Sango.Log.Info("【舌战】发起方落败，发起行为维持原结果。");
                return;
            }

            switch (kind)
            {
                case Kind.Recruit:
                    ApplyRecruit(recruiter, target, city);
                    break;
                case Kind.Diplomacy:
                    ApplyDiplomacy(action, penalty);
                    break;
            }
        }

        /// <summary>结束的舌战是否就是登记的那一对武将</summary>
        private static bool IsSameMatch(Debate debate, Person challenger, Person challenged)
        {
            Debate.Param param = debate.DebateParam;
            if (param == null || challenger == null || challenged == null) return false;
            return param.characters[0].person == challenger && param.characters[1].person == challenged;
        }

        /// <summary>登用改判：目标武将加入招募者所在势力（与 JobRecruitPerson 成功分支同一动作）</summary>
        private static void ApplyRecruit(Person recruiter, Person target, City city)
        {
            if (recruiter == null || target == null) return;

            if (recruiter.IsDead || target.IsDead)
            {
                Sango.Log.Info("【舌战】登用改判：当事武将已死亡，跳过。");
                return;
            }
            if (recruiter.BelongForce == null)
            {
                Sango.Log.Info("【舌战】登用改判：招募者已无势力，跳过。");
                return;
            }
            // 目标必须仍处于"可被登用"的身份：舌战期间可能已被别的势力登走、或身份发生变化
            if (!DebateTrigger.CanRecruitFailTriggerDebate(target))
            {
                Sango.Log.Info($"【舌战】登用改判：{target.Name} 已不是在野或无势力俘虏，跳过。");
                return;
            }

            // 目标城市兜底：JobRecruitPerson 传进来的就是"目标加入的城"，为空时退回招募者所在城
            City joinCity = city ?? recruiter.BelongCity;
            if (joinCity == null)
            {
                Sango.Log.Warning("【舌战】登用改判：找不到目标城市，跳过。");
                return;
            }

            string text = $"【舌战】{recruiter.Name} 辩胜 {target.Name}，登用改判为成功（{joinCity.Name}）。";
            OnRewritten?.Invoke(text, recruiter);

            try
            {
                target.BeRecruit(recruiter, joinCity);
            }
            catch (Exception e)
            {
                Sango.Log.Error($"【舌战】登用改判执行失败：{e}");
                return;
            }

            Log(text);
        }

        /// <summary>外交改判：返还失败时扣掉的关系值，并把这次外交按"成功"重新落地</summary>
        private static void ApplyDiplomacy(DiplomacyActionBase action, int penalty)
        {
            if (action == null || action.Sender == null || action.Receiver == null) return;

            string text = $"【舌战】{action.Diplomat?.Name} 辩胜，{action.GetActionName()}改判为成功。";
            OnRewritten?.Invoke(text, action.Diplomat);

            // 返还 + 重新落地。两者都可能依赖运行期数据（剧本 / 各 Action 自己的资源），
            // 任一步失败都只记错误、不把异常抛回舌战收尾流程。
            try
            {
                // 1) 返还 OnFailed 里扣掉的关系值
                DiplomacyManager manager = GameSystem.GetSystem<DiplomacyManager>();
                if (manager != null && penalty > 0)
                    manager.AddRelation(action.Sender, action.Receiver, penalty);

                // 2) 按"成功"重新落地：结盟 / 停战 / 通商等效果由各 Action 自己实现。
                //    这里用 PerformWithoutCheck(true) 而不是 Perform() —— 改判不需要也不应该重掷成功率。
                action.PerformWithoutCheck(true);
            }
            catch (Exception e)
            {
                Sango.Log.Error($"【舌战】外交改判执行失败：{e}");
                return;
            }

            Log(text);
        }

        /// <summary>
        /// 写开发日志。刻意**不写战报**（不往玩家消息 / HistoryLog 里塞东西）：
        /// 改判本身只是把发起行为按其原成功分支重跑一遍，播报交给该行为自己的通道（如外交的成功事件）。
        /// 需要额外播报的接入方挂 OnRewritten 即可。
        /// </summary>
        private static void Log(string text)
        {
            Sango.Log.Info(text);
        }

        #endregion
    }
}
