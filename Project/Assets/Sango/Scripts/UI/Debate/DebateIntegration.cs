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
 * 【未接线】按需求舌战暂不接入玩法，所以 Game.cs 目前还没有调用 Install()。
 *   接玩法时两步：
 *     1) Game.Init 里加一行 Debate.DebateIntegration.Install();
 *     2) Game.Update 里照单挑那样驱动 DebateManager.Instance.Update()。
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

        /// <summary>安装舌战与游戏的集成（接入玩法时由 Game.Init 调用一次即可）</summary>
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
