/*
 * 文件名：DebateChallengeFlow.cs
 * 描述：舌战发起流程 —— 所有舌战（外交失败 / 招募失败 / 剧本事件）的唯一入口。
 *
 * 与单挑 DuelChallengeFlow 的异同（照它的做法接）：
 *   · 相同：台词用 ClickPersonSay（带立绘、点一下继续）；需要选择时用 ChoosePersonSay（自带确定/取消）；
 *           IsPending 期间由 Game.Update 暂停剧本推进（对话框还没看完就不往前走）。
 *   · 不同：按需求舌战是"失败后**强制**进入"，所以**不问"是否应战"**、也不掷 AI 应战概率；
 *           玩家唯一的选择是"**是否观战**"——不观战则由 AI 代打，逻辑层瞬时跑完出结果。
 *
 * "进不进演示"的三个开关（优先级从高到低）：
 *   1. ForceNoView        排查表现层用的总闸：开着时无论谁参与、选什么，一律不观看；
 *   2. ForceViewOnEvent   剧本事件的剧情演出是否强制演出（默认 true，事件舌战一律开界面）；
 *   3. 双方是否有人由玩家**亲自操作**（Person.IsPlayerControl）：
 *        有人操作 → 弹"是否观战"，观战则手动出牌；没人操作且不强制演出 → 纯逻辑推演，不弹框不开界面。
 *
 * 对话框的两点注意（沿用单挑踩过的坑）：
 *   · UIDialog.OnCancel 在 cancelAction 为空时会退化成调用 sureAction，
 *     所以凡是要弹「确定 / 取消」的地方，两个回调都必须显式传入；
 *   · GameDialog 自带队列，台词与询问会按顺序逐条读下去。
 */

using System;
using UnityEngine;

namespace Sango.Core.Debate
{
    /// <summary>舌战发起流程</summary>
    public static class DebateChallengeFlow
    {
        /// <summary>发起来源</summary>
        public enum Source
        {
            /// <summary>外交交涉失败</summary>
            DiplomacyFail,
            /// <summary>招募(登用)失败</summary>
            RecruitFail,
            /// <summary>剧本事件发起（广播 GameEvent.OnDebateChallengeRequest）</summary>
            GameEvent,
        }

        /// <summary>是否正在等待玩家回答对话框</summary>
        public static bool IsPending { get; private set; }

        /// <summary>
        /// 是否强制按"不观看"处理。
        ///
        /// 【当前为 false】window_debate 的表现层（CardDebateView）已经和美术界面绑定完毕，
        /// "是否观战"的提示照弹，玩家点确定就创建表现层手动出牌、点取消就由 AI 自动对打。
        /// 想临时退回"一律不观看"（例如排查表现层问题时）把它改回 true 即可。
        /// </summary>
        public static bool ForceNoView = false;

        /// <summary>
        /// 剧本事件（广播 <see cref="GameEvent.OnDebateChallengeRequest"/>）发起的舌战是否强制演出。
        ///
        /// 【当前为 true】剧情演出（例如"诸葛亮舌战群儒""骂死王朗"）要的就是让玩家看，
        /// 所以默认打开：事件发起的舌战一律带表现层，即使双方都不是玩家亲自操作（AI 对 AI）也不降级成后台推演。
        /// 此时双方仍是自动出牌 —— 即"只演给你看，不用你出牌"。
        ///
        /// 想让事件舌战也静默结算（例如大批量刷事件时），把它置 false 即可；
        /// 单个事件也可以直接调 DebateManager.StartDebate(..., forceView: true) 绕过它。
        /// </summary>
        public static bool ForceViewOnEvent = true;

        private static Person s_Challenger;
        private static Person s_Challenged;

        #region 安装

        private static bool s_installed;

        /// <summary>把事件发起的舌战接到本流程（由 DebateIntegration.Install 调用一次）</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;
            GameEvent.OnDebateChallengeRequest += OnChallengeRequest;
        }

        private static void OnChallengeRequest(Person challenger, Person challenged)
        {
            // 剧本事件按 ForceViewOnEvent 决定要不要强制演出（事件广播带不了额外参数，只能走这个总开关）
            Request(challenger, challenged, Source.GameEvent, ForceViewOnEvent);
        }

        #endregion

        #region 台词

        /// <summary>
        /// 挑战方（失败方）先发话，应战方接一句。台词是内容不是规则，想改直接改数组；
        /// 走 ClickPersonSay（带立绘、点击继续、没有选项按钮）。
        /// </summary>
        public static readonly string[] ChallengeLines =
        {
            "且慢！此事须与你论个明白。",
            "话不说不明，今日便说个清楚。",
            "既然如此，敢与我辩上一场么？",
        };

        /// <summary>应战方的回应台词</summary>
        public static readonly string[] AcceptLines =
        {
            "哼，正合我意。",
            "便叫你心服口服。",
            "那就分个高下。",
        };

        /// <summary>随机播一句台词（person 为空或没配台词则跳过）</summary>
        private static void PlayLine(Person person, string[] lines)
        {
            if (person == null || lines == null || lines.Length == 0) return;
            if (GameDialog.Instance == null) return;

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
        /// 是否观战的询问实现。返回值 true = 观战（带表现层）。
        /// 置空则用 GameDialog 弹「确定 / 取消」；测试里可替换掉它，直接给定答案
        /// （注意仍会被 ForceNoView 压成"不观看"，要真正观战请临时把它置 false）。
        /// </summary>
        public static Func<string, Person, bool> AskWatchHandler;

        /// <summary>
        /// 提出一场舌战。舌战是强制进入的，这里只负责台词与"是否观战"。
        /// </summary>
        /// <param name="forceView">
        /// 强制演出：两侧都不是玩家操作时也照样开界面（剧本事件的剧情演出用，见 <see cref="ForceViewOnEvent"/>）。
        /// </param>
        /// <returns>true = 流程已被接管（正在问 / 已开局）；false = 当场被否决（已在舌战中、武将无效等）</returns>
        public static bool Request(Person challenger, Person challenged, Source source, bool forceView = false)
        {
            if (IsPending) return false;
            if (challenger == null || challenged == null || challenger == challenged) return false;
            if (DebateManager.Instance.IsDebating) return false;
            if (!DebateManager.Instance.CanStartDebate(challenger, challenged)) return false;

            s_Challenger = challenger;
            s_Challenged = challenged;

            // 两侧都没有玩家亲自操作（AI 之间 / 玩家势力里被 AI 托管的武将）时：
            // 不弹对话（没人可问），默认也不进演示、直接后台推演完事；
            // 只有要求强制演出（剧情事件）才开界面 —— 这时双方仍然自动出牌，玩家只是看。
            bool playerInvolved = DebateTrigger.IsPlayerControl(challenger) || DebateTrigger.IsPlayerControl(challenged);
            if (!playerInvolved)
            {
                StartDebate(ResolveWithView(forceView), forceView);
                return true;
            }

            // 挑战方叫阵 → 应战方回应（排队播，读完自动接"是否观战"）
            PlayLine(challenger, ChallengeLines);
            PlayLine(challenged, AcceptLines);

            string question = challenger.Name + "与" + challenged.Name + "的舌战即将开始，是否观战？";

            if (AskWatchHandler != null)
            {
                StartDebate(ResolveWithView(AskWatchHandler(question, challenger)));
                return true;
            }

            if (GameDialog.Instance == null)
            {
                // 没有对话框可用（编辑器 / 单元测试）：按不观看处理
                StartDebate(ResolveWithView(false));
                return true;
            }

            IsPending = true;
            GameDialog.Instance.Open(
                GameDialog.DialogStyle.ChoosePersonSay,
                question,
                () =>
                {
                    // 确定 = 观战：开舌战界面，轮到自己出牌时手动点
                    IsPending = false;
                    StartDebate(ResolveWithView(true));
                },
                () =>
                {
                    // 取消 = 不观看：纯逻辑推演，双方都交给 AI 自动出牌
                    IsPending = false;
                    StartDebate(ResolveWithView(false));
                },
                challenger);
            return true;
        }

        /// <summary>
        /// 把"玩家是否选择观战"落到 withView。
        /// ForceNoView 是排查表现层用的总闸：开着时无论玩家选什么都按不观看走。
        /// </summary>
        private static bool ResolveWithView(bool watch)
        {
            return !ForceNoView && watch;
        }

        /// <summary>真正启动舌战（同时清掉暂存的两名武将）</summary>
        /// <param name="withView">是否带表现层</param>
        /// <param name="forceView">两侧都非玩家操作时也强制演出（剧情事件用）</param>
        public static void StartDebate(bool withView, bool forceView = false)
        {
            Person challenger = s_Challenger;
            Person challenged = s_Challenged;
            s_Challenger = null;
            s_Challenged = null;

            if (challenger == null || challenged == null) return;
            DebateManager.Instance.StartDebate(challenger, challenged, withView, null, forceView);
        }

        #endregion
    }
}
