/*
 * 文件名：DuelChallengeFlow.cs
 * 描述：单挑发起流程 —— 所有单挑（事件发起 / 部队指令发起）的唯一入口。
 *
 * 流程（对应需求）：
 *   1. 发起方对目标部队提出单挑请求。
 *   2. 若应战方是玩家 → 弹出「是否接受」；否则用 DuelAcceptChance 掷骰判定 AI 是否应战。
 *   3. 拒绝是有代价的：应战方气力下降、部分士兵逃散（见 ApplyRefusePenalty）。
 *   4. 接受之后，只要双方中有玩家 → 弹出「是否观看」。
 *   5. 不观看 → 不带表现层启动，逻辑层瞬时跑完出结果。
 *      观看   → 带表现层（window_duel / CardDuelView）启动，由表现层驱动并做出行动表现。
 *
 * 对话框统一使用 GameDialog 的 DialogStyle.ChoosePersonSay（window_dialog2，
 * 自带「确定 / 取消」两个按钮，分别回调 OnSure / OnCancel）。
 * 注意 UIDialog.OnCancel 在 cancelAction 为空时会退化成调用 sureAction，
 * 因此这里两个回调都必须显式传入。
 *
 * 事件发起的单挑：其它系统广播 GameEvent.OnDuelChallengeRequest 即可，
 * 由 Install() 统一接到本流程，不需要各自复制一遍判定逻辑。
 */

using System;
using UnityEngine;

namespace Sango.Core.Duel
{
    /// <summary>单挑发起流程</summary>
    public static class DuelChallengeFlow
    {
        /// <summary>发起来源</summary>
        public enum Source
        {
            /// <summary>部队行动指令发起（部队全员）</summary>
            TroopCommand,
            /// <summary>剧本事件发起（武将之间的单挑）</summary>
            GameEvent,
        }

        /// <summary>是否正在等待玩家回答对话框</summary>
        public static bool IsPending { get; private set; }

        private static Troop s_Challenger;
        private static Troop s_Challenged;

        #region 可调常量：拒绝的代价

        /// <summary>拒绝单挑扣减的气力（Troop.morale）</summary>
        public const int RefuseMoralePenalty = 10;

        /// <summary>拒绝单挑逃散的兵力</summary>
        public const int RefuseTroopPenalty = 100;

        #endregion

        #region 安装

        private static bool s_installed;

        /// <summary>把事件发起的单挑接到本流程（由 DuelIntegration.Install 调用一次）</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;
            GameEvent.OnDuelChallengeRequest += OnChallengeRequest;
        }

        private static void OnChallengeRequest(Troop challenger, Troop challenged)
        {
            Request(challenger, challenged, Source.GameEvent);
        }

        #endregion

        #region 发起

        /// <summary>
        /// 提出一次单挑请求。
        /// </summary>
        /// <returns>
        /// true 表示流程已被接管（要么正在等玩家回答，要么已进入单挑）；
        /// false 表示当场就被否决/无法发起，调用方应立即结束自己的指令流程。
        /// </returns>
        public static bool Request(Troop challenger, Troop challenged, Source source)
        {
            if (IsPending) return false;
            if (challenger == null || challenged == null) return false;
            if (DuelManager.Instance.IsDueling) return false;
            if (!DuelManager.Instance.CanStartDuel(challenger, challenged)) return false;

            s_Challenger = challenger;
            s_Challenged = challenged;

            // 应战判定：玩家自己决定，AI 按性格与能力掷骰
            if (challenged.IsPlayer)
            {
                AskAccept();
                return true;
            }

            if (!DuelAcceptChance.Roll(challenged, challenger))
            {
                Reject(challenged);
                return false;
            }

            AfterAccepted();
            return true;
        }

        #endregion

        #region 对话框

        /// <summary>应战方是玩家 → 询问是否接受</summary>
        private static void AskAccept()
        {
            Troop challenged = s_Challenged;
            Troop challengerTroop = s_Challenger;
            Person challenger = challengerTroop != null ? challengerTroop.Leader : null;
            Person target = challenged != null ? challenged.Leader : null;
            string challengerName = challenger != null ? challenger.Name : "敌军";

            IsPending = true;
            GameDialog.Instance.Open(
                GameDialog.DialogStyle.ChoosePersonSay,
                challengerName + "向你发起单挑，是否应战？",
                () =>
                {
                    IsPending = false;
                    AfterAccepted();
                },
                () =>
                {
                    IsPending = false;
                    Reject(challenged);
                },
                challenger);
        }

        /// <summary>接受之后：涉及玩家就问是否观看，否则直接瞬时结算</summary>
        private static void AfterAccepted()
        {
            Troop challenger = s_Challenger;
            Troop challenged = s_Challenged;

            bool anyPlayer = (challenger != null && challenger.IsPlayer)
                          || (challenged != null && challenged.IsPlayer);
            if (!anyPlayer)
            {
                StartDuel(false);
                return;
            }

            string a = challenger != null && challenger.Leader != null ? challenger.Leader.Name : "？";
            string b = challenged != null && challenged.Leader != null ? challenged.Leader.Name : "？";

            IsPending = true;
            GameDialog.Instance.Open(
                GameDialog.DialogStyle.ChoosePersonSay,
                a + "与" + b + "的单挑即将开始，是否观战？",
                () =>
                {
                    IsPending = false;
                    StartDuel(true);
                },
                () =>
                {
                    IsPending = false;
                    StartDuel(false);
                },
                challenger != null ? challenger.Leader : null);
        }

        #endregion

        #region 收尾

        /// <summary>真正启动单挑</summary>
        private static void StartDuel(bool withView)
        {
            Troop challenger = s_Challenger;
            Troop challenged = s_Challenged;
            s_Challenger = null;
            s_Challenged = null;

            if (challenger == null || challenged == null) return;
            DuelManager.Instance.StartDuel(challenger, challenged, withView);
        }

        /// <summary>
        /// 应战方拒绝：气力下降 + 兵力逃散，并向玩家播报。
        /// </summary>
        private static void Reject(Troop refuser)
        {
            Troop challenger = s_Challenger;
            s_Challenger = null;
            s_Challenged = null;

            ApplyRefusePenalty(refuser);

            bool anyPlayer = (challenger != null && challenger.IsPlayer)
                          || (refuser != null && refuser.IsPlayer);
            if (!anyPlayer) return;

            string challengerName = challenger != null && challenger.Leader != null
                ? challenger.Leader.Name : "对手";
            string refuserName = refuser != null && refuser.Leader != null
                ? refuser.Leader.Name : "对手";

            // 播报期间同样暂停剧本推进，避免对白还没看完战局就往前走
            IsPending = true;
            GameDialog.Instance.Open(
                GameDialog.DialogStyle.ChoosePersonSay,
                refuserName + "拒绝了" + challengerName + "的单挑，"
                    + refuserName + "部队气力下降，部分士兵逃散。",
                () => { IsPending = false; },
                () => { IsPending = false; },
                refuser != null ? refuser.Leader : null);
        }

        /// <summary>
        /// 拒绝单挑的代价：气力（Troop.morale）下降、兵力逃散。
        /// 数值见 RefuseMoralePenalty / RefuseTroopPenalty，可按手感调整。
        /// </summary>
        public static void ApplyRefusePenalty(Troop refuser)
        {
            if (refuser == null) return;

            // 气力下降（ChangeMorale 内部会夹到 0 ~ MaxMorale，并带浮动数值表现）
            refuser.ChangeMorale(-RefuseMoralePenalty);

            // 兵力逃散；至少保留 1 兵，避免一支部队因为"拒绝"被直接抹掉
            int loss = Mathf.Min(RefuseTroopPenalty, Mathf.Max(0, refuser.troops - 1));
            if (loss > 0)
                refuser.ChangeTroops(-loss, null, 0);
        }

        #endregion
    }
}
