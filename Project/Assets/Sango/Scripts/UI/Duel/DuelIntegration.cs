/*
 * 文件名：DuelIntegration.cs
 * 描述：单挑系统与游戏 / UI 的集成点
 *
 * 职责：
 *   · 把"单挑要用的表现层怎么创建、怎么销毁"接到 DuelManager 上。
 *   · 当前使用卡牌表现（window_duel.prefab + CardDuelView）。
 *   · 将来要换成 3D 表现时，只需在此更换 CreateCardView 的实现，
 *     或者在 DuelManager.CreateViewHandler 上挂另一个工厂，逻辑层完全不受影响。
 */

using Sango.UI;
using UnityEngine;

namespace Sango.Core.Duel
{
    /// <summary>单挑系统集成入口</summary>
    public static class DuelIntegration
    {
        /// <summary>单挑窗口名</summary>
        public const string DuelWindowName = "window_duel";

        private static bool s_installed = false;

        /// <summary>安装单挑与游戏的集成（由 Game.Init 调用一次即可）</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;

            DuelManager.CreateViewHandler = CreateCardView;
            DuelManager.ReleaseViewHandler = ReleaseCardView;

            // 把"事件发起的单挑"接到统一发起流程（是否应战 / 是否观看都在那里处理）
            DuelChallengeFlow.Install();

            // 战法释放完成后按概率挑起单挑（战法自身开关 Skill.canTriggerDuel + 概率 = Troop.duelChance）
            DuelSkillTrigger.Install();
        }

        /// <summary>创建卡牌表现层</summary>
        private static IDuelView CreateCardView(Duel duel)
        {
            Window.WindowInterface wi = Window.Instance.Open(DuelWindowName);
            UGUIWindow win = wi != null ? wi.ugui_instance : null;
            if (win == null)
            {
                Sango.Log.Warning("单挑窗口 window_duel 打开失败，本次单挑将无表现层。");
                return null;
            }

            CardDuelView view = win.GetComponent<CardDuelView>();
            if (view == null)
            {
                // prefab 上预留的脚本槽位可能是空的，这里兜底补挂，保证表现层一定存在
                view = win.gameObject.AddComponent<CardDuelView>();
            }

            if (duel != null)
                view.Bind(duel);
            return view;
        }

        /// <summary>关闭卡牌表现层</summary>
        private static void ReleaseCardView()
        {
            Window.Instance.Close(DuelWindowName);
        }
    }
}
