/*
 * 文件名：DebateIntegration.cs
 * 描述：舌战系统与游戏 / UI 的集成点（对应单挑的 UI/Duel/DuelIntegration.cs）
 *
 * 职责：
 *   · 把"舌战要用的表现层怎么创建、怎么销毁"接到 DebateManager 上。
 *   · 当前使用卡牌式 2D 表现（window_debate.prefab + CardDebateView）。
 *   · 将来要换 3D 表现时，只需在此更换 CreateCardView 的实现，
 *     或者在 DebateManager.CreateViewHandler 上挂另一个工厂，逻辑层完全不受影响。
 *
 * 安装内容（由 Game.Init 调用一次，与 DuelIntegration 并列）：
 *   · DebateManager 的表现层工厂（CreateCardView / ReleaseCardView）；
 *   · DebateChallengeFlow：事件发起的舌战走统一发起流程；
 *   · DebateTrigger：外交失败 / 招募失败后按概率强制进入舌战；
 *   · DebateConsequence：舌战结束后按胜负改判发起行为（登用 / 外交）为成功。
 * 驱动由 Game.Update 负责：DrivingFlow 待答 / 舌战进行中都会暂停剧本推进，见 Game.Update。
 */

using Sango.UI;
using UnityEngine;

namespace Sango.Core.Debate
{
    /// <summary>舌战系统集成入口</summary>
    public static class DebateIntegration
    {
        /// <summary>舌战窗口名</summary>
        public const string DebateWindowName = "window_debate";

        private static bool s_installed = false;

        /// <summary>安装舌战与游戏的集成（由 Game.Init 调用一次即可）</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;

            DebateManager.CreateViewHandler = CreateCardView;
            DebateManager.ReleaseViewHandler = ReleaseCardView;

            // 把"事件发起的舌战"接到统一发起流程
            DebateChallengeFlow.Install();

            // 玩法触发点：外交失败 / 招募失败后按概率强制进入舌战
            DebateTrigger.Install();

            // 舌战结束后的改判：发起方辩胜则把发起行为改判为成功
            DebateConsequence.Install();

            // 【玩家反馈】舌战结果播报：登用 / 外交的成败必须让玩家看见，
            // 不能只写开发日志（玩家看不到）或只在舌战界面里亮一下"胜/负"。
            DebateConsequence.OnSettled += OnDebateSettled;
        }

        /// <summary>
        /// 把舌战的最终结论告诉玩家。
        ///
        /// 为什么需要：舌战界面只在内部亮一下"胜/负"大字，玩家看不到"这次登用 / 交涉算不算数"；
        /// 选择"不观战"时更是全程没有任何界面，打完什么都不知道。
        ///
        /// AI 对 AI 的舌战不弹窗（玩家不在场，弹窗只会打断回合推进）。
        /// </summary>
        /// <param name="text">面向玩家的结果文本</param>
        /// <param name="anchor">发起方武将（弹窗头像）</param>
        private static void OnDebateSettled(string text, Person anchor)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 玩家不参与这场舌战：只留开发日志，不打断回合
            if (!DebateTrigger.IsPlayerControl(anchor))
            {
                Sango.Log.Info(text);
                return;
            }

            if (GameDialog.Instance == null)
            {
                Sango.Log.Warning($"舌战结果对话框不可用，结果仅记录日志：{text}");
                return;
            }

            // ClickPersonSay：点一下人物说话后关闭，不阻塞剧本推进（没有后续回调）
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, text, () => { }, anchor);
        }

        /// <summary>创建卡牌式 2D 表现层</summary>
        private static IDebateView CreateCardView(Debate debate)
        {
            Window.WindowInterface wi = Window.Instance.Open(DebateWindowName);
            UGUIWindow win = wi != null ? wi.ugui_instance : null;
            if (win == null)
            {
                Sango.Log.Warning("舌战窗口 window_debate 打开失败，本次舌战将无表现层。");
                return null;
            }

            // prefab 上预留的脚本槽位可能是空的，这里兜底补挂，保证表现层一定存在。
            // 注意：不在这里调 Rebind() —— 节点引用由 prefab 固化（编辑器构建器负责写回），
            // 运行时重绑反而会把手工绑好的引用清掉；AutoBind 在 Awake 里已经跑过一次。
            CardDebateView view = win.GetComponent<CardDebateView>();
            if (view == null)
            {
                view = win.gameObject.AddComponent<CardDebateView>();
            }

            if (debate != null)
                view.Bind(debate);
            return view;
        }

        /// <summary>关闭卡牌式表现层</summary>
        private static void ReleaseCardView()
        {
            Window.Instance.Close(DebateWindowName);
        }
    }
}
