/*
 * 文件名：DebateTrigger.cs
 * 描述：舌战的玩法触发点（对应单挑的 Game/Duel/DuelSkillTrigger.cs）
 *
 * 当前接了两处，都是"失败后按概率强制进入舌战"：
 *   1. 外交交涉失败 —— 订阅 DiplomacyActionBase.OnDiplomacyFailed（唯一带 DiplomacyActionBase 的失败事件，
 *      因此能拿到使者 action.Diplomat 与对方代表 action.Receiver.mGovernor）。
 *      按需求排除两类外交：宣战（DeclareWar）与送礼（SendGift）不触发。
 *   2. 招募(登用)失败 —— 由 Person.JobRecruitPerson 在失败分支调 OnRecruitFailed（该处没有现成事件，
 *      所以直接调这里的入口）。覆盖：玩家城池登庸、异城任务到达、搜索、人才府、破城/部队灭亡招降、AI 城池登用。
 *      【身份限制】按需求，登用失败触发舌战只对"在野武将"或"没有势力的俘虏"生效
 *      （见 CanRecruitFailTriggerDebate）；敌方/他势力在职武将、有势力的俘虏都不触发。
 *      未覆盖：AI 招降俘虏走 ForceAI.TryRecruitCaptive 的独立掷骰（要接的话在 ForceAI.cs 的失败分支补一行同样调用）。
 *
 * 两道闸门（缺一不可）：
 *   · 概率   ：剧本参数 debateChanceWhenDiplomacyFail / debateChanceWhenRecruitFail，0 = 关闭；
 *   · AI 开关：剧本参数 debateAllowAIVsAI，false 时纯 AI 之间不触发。
 * 触发频率只由概率决定，另无冷却（按决策）。
 *
 * 触发之后交给 DebateChallengeFlow 走"是否观战"的询问与真正的开局，本类不直接开舌战。
 * 开局前还会用 DebateConsequence 登记一条"待改判"：发起方（挑战方）辩胜时把发起行为改判为成功（A 方案）。
 */

namespace Sango.Core.Debate
{
    /// <summary>舌战玩法触发</summary>
    public static class DebateTrigger
    {
        private static bool s_installed = false;

        /// <summary>安装触发点（由 DebateIntegration.Install 调用一次）</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;

            DiplomacyActionBase.OnDiplomacyFailed += OnDiplomacyFailed;
        }

        /// <summary>
        /// 招募(登用)失败后的入口 —— 由 Person.JobRecruitPerson 的失败分支调用。
        /// </summary>
        /// <param name="recruiter">招募者（执行登用的武将）</param>
        /// <param name="target">被招募者（目标人才）</param>
        /// <param name="targetCity">登用成功后目标加入的城市（即 JobRecruitPerson 的 targetCity）</param>
        public static void OnRecruitFailed(Person recruiter, Person target, City targetCity)
        {
            // 按需求：登用失败触发舌战只对"在野武将 / 没有势力的俘虏"生效
            if (!CanRecruitFailTriggerDebate(target))
                return;

            TryTrigger(recruiter, target, DebateRules.GetRecruitFailChance(),
                DebateChallengeFlow.Source.RecruitFail, "登用失败",
                () => DebateConsequence.RegisterRecruit(recruiter, target, targetCity));
        }

        /// <summary>
        /// 登用失败时该目标是否允许进入舌战。
        ///
        /// 按需求只有两类：**在野武将**（未出仕、无势力）、**没有势力的俘虏**。
        /// 其余（敌方 / 他势力在职武将、有势力的俘虏）不触发 —— 它们本来就"登不动"，
        /// 靠舌战翻盘会绕过势力归属规则。
        /// </summary>
        public static bool CanRecruitFailTriggerDebate(Person target)
        {
            if (target == null) return false;
            if (target.IsDead) return false;

            // 在野（未出仕）
            if (target.IsWild) return true;

            // 没有势力的俘虏（例如所属势力已灭亡，只剩下俘虏身份）
            if (target.IsPrisoner && target.mBelongForce == null) return true;

            return false;
        }

        /// <summary>外交交涉失败（订阅 DiplomacyActionBase.OnDiplomacyFailed）</summary>
        private static void OnDiplomacyFailed(DiplomacyActionBase action, int penalty)
        {
            if (action == null) return;

            // 按需求排除：宣战与送礼不触发舌战（送礼失败本来也不发这个事件，这里一并挡掉）
            if (!IsDiplomacyTypeAllowed(action.ActionType))
                return;

            Person diplomat = action.Diplomat;                                        // 使者
            Person rival = action.Receiver != null ? action.Receiver.mGovernor : null; // 对方代表（接收方君主）
            TryTrigger(diplomat, rival, DebateRules.GetDiplomacyFailChance(),
                DebateChallengeFlow.Source.DiplomacyFail, "外交失败",
                () => DebateConsequence.RegisterDiplomacy(diplomat, rival, action, penalty));
        }

        /// <summary>
        /// 该外交类型是否允许触发舌战。
        /// 按需求排除：宣战（本就要打，不必辩）与送礼（不算交涉）不触发。
        /// </summary>
        public static bool IsDiplomacyTypeAllowed(DiplomacyActionType type)
        {
            return type != DiplomacyActionType.DeclareWar && type != DiplomacyActionType.SendGift;
        }

        /// <summary>
        /// 按概率决定是否进入舌战。
        /// </summary>
        /// <param name="challenger">挑战方（失败方/发起方）</param>
        /// <param name="challenged">应战方</param>
        /// <param name="chancePercent">触发概率（百分比）</param>
        /// <param name="source">发起来源（用于流程与日志）</param>
        /// <param name="reason">原因（仅用于日志）</param>
        /// <param name="onStarted">
        /// 真正开局之前执行的登记回调（登记"待改判的发起行为"）。只有流程确实被接管时才保留；
        /// 开局被否决时本方法会把它撤掉。传 null 表示本次不做改判。
        /// </param>
        /// <returns>是否真的发起了舌战（流程被接管）</returns>
        public static bool TryTrigger(Person challenger, Person challenged, int chancePercent,
            DebateChallengeFlow.Source source, string reason, System.Action onStarted = null)
        {
            if (chancePercent <= 0) return false;
            if (challenger == null || challenged == null || challenger == challenged) return false;
            if (challenger.IsDead || challenged.IsDead) return false;
            // 已经在舌战中就不再叠加；玩家那条线还在等"是否观战"的回答时同样不叠加
            // —— 等待期间 IsDebating 还是 false，只有 IsPending 能挡住，否则会把已登记的改判顶掉。
            if (DebateManager.Instance.IsDebating) return false;
            if (DebateChallengeFlow.IsPending) return false;

            bool playerInvolved = IsPlayerControl(challenger) || IsPlayerControl(challenged);
            if (!playerInvolved && !DebateRules.AllowAIVsAI()) return false;

            // 用 DebateRandom 而不是 GameRandom：与舌战内部其它随机保持同一个来源，
            // 未设种子时它同样转发到工程的 GameRandom，行为一致；设了种子则便于复现测试。
            if (!DebateRandom.Chance(chancePercent)) return false;

            Sango.Log.Info($"【舌战】{reason}触发：{challenger.Name} VS {challenged.Name}"
                + (playerInvolved ? "（玩家参与，询问是否观战）" : "（AI 对战，后台结算）"));

            // 先登记改判、再开局；被否决就把登记撤掉。同一时刻只可能有一场舌战，所以只登记一条。
            if (onStarted != null)
                onStarted();

            if (DebateChallengeFlow.Request(challenger, challenged, source))
                return true;

            if (onStarted != null)
                DebateConsequence.ClearPending();
            return false;
        }

        /// <summary>
        /// 该武将是否由玩家**亲自操作**（玩家直属军团，等价 C++ 的 district.is_player() &amp;&amp; get_number()==1）。
        ///
        /// 与单挑保持一致：判断"要不要弹对话 / 要不要让玩家出牌"一律用它，不用势力级的 Person.IsPlayer ——
        /// 玩家势力里交给 AI 托管（非直属军团）的武将触发舌战时，不该弹对话框，也不该停下来等玩家。
        /// </summary>
        public static bool IsPlayerControl(Person person)
        {
            return person != null && person.IsPlayerControl;
        }
    }
}
