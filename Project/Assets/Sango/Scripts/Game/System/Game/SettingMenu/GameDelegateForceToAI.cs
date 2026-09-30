/*
 * 文件名：GameDelegateForceToAI.cs
 * 描述：设置菜单项"委托给AI"——把手上控制的势力交还AI代管，回到上帝放置模式
 * 创建日期：2026-09-29
 * 作者：XBJ
 */

using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 设置菜单项"委托给AI"。
    ///
    /// 场景：玩家控制着势力，打到一半不想继续操作，交还AI，世界重新变成 AI 自治的上帝放置模式。
    /// 若这是玩家手上最后一个势力，交出去之后场上就没有玩家势力了，等同于回到放置模式；
    /// 若同时控制着多个势力，则只交出被点选的那一个，其余仍由玩家操作。
    ///
    /// 移交本身不是"等价代打"：AI 势力根本不会执行外交、斩首、流放、任命军师这几类操作
    /// （AI 势力之间的外交在 <c>Force.AIPrepare</c> 里被整体屏蔽，其余几项只有玩家菜单入口），
    /// 所以确认框里必须把这一点写明白，避免玩家以为委托后这些也会照常发生。
    ///
    /// 可见性：只在场上有玩家势力时出现（基类按 <see cref="PlayerControlledTarget"/> 统一裁决），
    /// 放置模式下没有可交出的势力，直接隐藏。
    /// </summary>
    [GameSystem]
    public class GameDelegateForceToAI : GameForceControlMenuBase
    {
        /// <summary>
        /// 注册菜单项：order 取 220，与"中途参加"(210) 一起放在设置菜单**最末尾**且方向相邻成对
        /// （菜单按 order 升序显示，现有项最大的是"返回主菜单"= 200，见 ContextMenuData 的"order 越小越靠前"）。
        /// </summary>
        public GameDelegateForceToAI()
        {
            customMenuName = "委托给AI";
            customMenuOrder = 220;
        }

        /// <summary>本项是把控制权交出去，因此只在"有玩家势力"时出现。</summary>
        protected override bool PlayerControlledTarget
        {
            get { return false; }
        }

        /// <summary>候选 = 存活且当前归玩家控制的势力。</summary>
        /// <returns>可委托给AI的势力列表</returns>
        protected override List<Force> GetCandidates()
        {
            return ForceControlService.GetDelegatableForces();
        }

        /// <summary>势力详情列表的窗口标题。</summary>
        protected override string SelectTitle
        {
            get { return "选择要委托给AI的势力"; }
        }

        /// <summary>确认框文案：写明生效时机、本回合收尾方式与AI不会代做的操作。</summary>
        /// <param name="force">目标势力</param>
        /// <returns>确认框内容</returns>
        protected override string MakeConfirmText(Force force)
        {
            return $"确定把【{force.Name}】委托给AI吗？\n" +
                   "该势力若正在行动中，本回合会立即结束（未行动的部队不再补跑），AI从下一回合起代管；\n" +
                   "场上没有其它玩家势力时即进入上帝放置模式。\n" +
                   "注意：AI不会执行外交、斩首、流放、任命军师等操作。";
        }

        /// <summary>无候选时的提示：场上已经没有玩家势力。</summary>
        protected override string EmptyHint
        {
            get { return "当前没有由你控制的势力，可先选“中途参加”接管一个势力"; }
        }
    }
}
