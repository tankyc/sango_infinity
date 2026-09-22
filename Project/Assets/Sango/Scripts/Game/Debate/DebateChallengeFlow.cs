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
            Request(challenger, challenged, Source.GameEvent);
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
        /// 置空则用 GameDialog 弹「确定 / 取消」；测试里可替换掉它，直接给定答案。
        /// </summary>
        public static Func<string, Person, bool> AskWatchHandler;

        /// <summary>
        /// 提出一场舌战。舌战是强制进入的，这里只负责台词与"是否观战"。
        /// </summary>
        /// <returns>true = 流程已被接管（正在问 / 已开局）；false = 当场被否决（已在舌战中、武将无效等）</returns>
        public static bool Request(Person challenger, Person challenged, Source source)
        {
            if (IsPending) return false;
            if (challenger == null || challenged == null || challenger == challenged) return false;
            if (DebateManager.Instance.IsDebating) return false;
            if (!DebateManager.Instance.CanStartDebate(challenger, challenged)) return false;

            s_Challenger = challenger;
            s_Challenged = challenged;

            // 对话只在"有玩家亲自操作的一方"时才弹；否则（AI 之间 / 玩家势力里被 AI 托管的武将）直接后台结算
            bool showDialog = DebateTrigger.IsPlayerControl(challenger) || DebateTrigger.IsPlayerControl(challenged);
            if (!showDialog)
            {
                StartDebate(false);
                return true;
            }

            // 挑战方叫阵 → 应战方回应（排队播，读完自动接"是否观战"）
            PlayLine(challenger, ChallengeLines);
            PlayLine(challenged, AcceptLines);

            string question = challenger.Name + "与" + challenged.Name + "的舌战即将开始，是否观战？";

            if (AskWatchHandler != null)
            {
                StartDebate(AskWatchHandler(question, challenger));
                return true;
            }

            if (GameDialog.Instance == null)
            {
                // 没有对话框可用（编辑器 / 单元测试）：默认按"观战"处理。
                // 此时表现层若也拿不到（window_debate 打不开），DebateManager 会退化成无表现层瞬时结算。
                StartDebate(true);
                return true;
            }

            IsPending = true;
            GameDialog.Instance.Open(
                GameDialog.DialogStyle.ChoosePersonSay,
                question,
                () =>
                {
                    IsPending = false;
                    StartDebate(true);
                },
                () =>
                {
                    IsPending = false;
                    StartDebate(false);
                },
                challenger);
            return true;
        }

        /// <summary>真正启动舌战（同时清掉暂存的两名武将）</summary>
        public static void StartDebate(bool withView)
        {
            Person challenger = s_Challenger;
            Person challenged = s_Challenged;
            s_Challenger = null;
            s_Challenged = null;

            if (challenger == null || challenged == null) return;
            DebateManager.Instance.StartDebate(challenger, challenged, withView);
        }

        #endregion
    }
}
