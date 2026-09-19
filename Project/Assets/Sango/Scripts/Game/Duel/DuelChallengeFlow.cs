/*
 * 文件名：DuelChallengeFlow.cs
 * 描述：单挑发起流程 —— 所有单挑（事件发起 / 部队指令发起）的唯一入口。
 *
 * 流程（对应需求）：
 *   1. 发起方对目标部队提出单挑请求，挑战方随即叫阵一句（ChallengeLines 随机）。
 *   2. 若应战方是玩家 → 弹出「是否接受」；否则用 DuelAcceptChance 掷骰判定 AI 是否应战。
 *   3. 无论应战还是拒绝，应战方都回一句台词（AcceptLines / RefuseLines 随机）。
 *   4. 拒绝是有代价的：应战方气力下降、部分士兵逃散（见 ApplyRefusePenalty）。
 *   5. 接受之后，只要双方中有玩家 → 弹出「是否观看」。
 *   6. 不观看 → 不带表现层启动，逻辑层瞬时跑完出结果。
 *      观看   → 带表现层（window_duel / CardDuelView）启动，由表现层驱动并做出行动表现。
 *
 * 对话框分两类，各用各的样式：
 *   · 台词       ClickPersonSay（window_dialog4）——带立绘、点一下继续，没有选项按钮。
 *   · 需要选择时 ChoosePersonSay（window_dialog2）——自带「确定 / 取消」，分别回调 OnSure / OnCancel。
 * 注意 UIDialog.OnCancel 在 cancelAction 为空时会退化成调用 sureAction，
 * 因此凡是要弹「确定 / 取消」的地方，两个回调都必须显式传入。
 * 拒绝播报只是告知结果，用 ClickSay（window_dialog5，点击继续）——不再给玩家一个无意义的选择框。
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

        #region 台词

        /// <summary>
        /// 发起单挑时的台词：挑战方先叫阵，应战方再回应；应战 / 拒绝各一套，每套随机取一句。
        /// 想加台词直接往对应数组里加；数组为空则那一步不说话。
        ///
        /// 台词走 ClickPersonSay（window_dialog4）：带武将立绘、点一下继续，**不带选项按钮**。
        /// GameDialog 自带队列，所以"叫阵 → 回应 → 是否观战 / 拒绝播报"会按顺序逐条读下去。
        /// </summary>
        public static readonly string[] ChallengeLines =
        {
            "可敢与我一战！",
            "久闻大名，今日讨教！",
            "莫要缩在阵后，出来单挑！",
        };

        /// <summary>应战方【接受】时的台词</summary>
        public static readonly string[] AcceptLines =
        {
            "正合我意！",
            "谁来送死？",
            "那就分个高下！",
        };

        /// <summary>应战方【拒绝】时的台词</summary>
        public static readonly string[] RefuseLines =
        {
            "匹夫之勇，不足为道。",
            "我岂能与你一般见识。",
            "休想激我。",
        };

        /// <summary>随机播一句台词（person 为空或没配台词则跳过）</summary>
        private static void PlayLine(Person person, string[] lines)
        {
            if (person == null || lines == null || lines.Length == 0) return;
            if (GameDialog.Instance == null) return;

            // sure / cancel 都给同一个空回调：这句只是台词，读完接着走下一条
            System.Action ignore = () => { };
            GameDialog.Instance.Open(
                GameDialog.DialogStyle.ClickPersonSay,
                lines[UnityEngine.Random.Range(0, lines.Length)],
                ignore,
                ignore,
                person);
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

            // 挑战方先叫阵（排队播，读完自动接应战方的回应）
            PlayLine(challenger.Leader, ChallengeLines);

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

        /// <summary>接受之后：先说一句应战台词，涉及玩家就问是否观看，否则直接瞬时结算</summary>
        private static void AfterAccepted()
        {
            Troop challenger = s_Challenger;
            Troop challenged = s_Challenged;

            PlayLine(challenged != null ? challenged.Leader : null, AcceptLines);

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
        /// 应战方拒绝：先说一句拒绝台词，再播报代价（气力下降 + 兵力逃散）。
        ///
        /// 播报只是把结果告诉玩家，**不再是「确定 / 取消」那种选择框**——
        /// 拒都拒了，没有可选的余地；改用 ClickSay（点击继续、没有按钮）读过去即可。
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

            // 拒绝方的台词（排队在挑战方叫阵之后）
            PlayLine(refuser != null ? refuser.Leader : null, RefuseLines);

            string challengerName = challenger != null && challenger.Leader != null
                ? challenger.Leader.Name : "对手";
            string refuserName = refuser != null && refuser.Leader != null
                ? refuser.Leader.Name : "对手";

            // 播报期间同样暂停剧本推进，避免对白还没看完战局就往前走
            IsPending = true;
            GameDialog.Instance.Open(
                GameDialog.DialogStyle.ClickSay,
                refuserName + "拒绝了" + challengerName + "的单挑，"
                    + refuserName + "部队气力下降，部分士兵逃散。",
                () => { IsPending = false; },
                () => { IsPending = false; });
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
