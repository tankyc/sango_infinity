/*
 * 文件名：GameJoinScenarioForce.cs
 * 描述：设置菜单项"中途参加"——上帝放置模式中原来临时旁观的玩家接管一个现存势力开始操作
 * 创建日期：2026-09-29
 * 作者：XBJ
 */

using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 设置菜单项"中途参加"。
    ///
    /// 场景：开局不选势力进入上帝放置模式后，玩了一会儿想下场——从左上角设置里选本项，
    /// 在势力详情列表（存活且不归玩家控制的势力）里点一个势力，确认后接管它。
    ///
    /// 与"委托给AI"（<see cref="GameDelegateForceToAI"/>）共用
    /// <see cref="GameForceControlMenuBase"/> 的流程与 <see cref="ForceControlService"/> 的状态变更，
    /// 本类只声明方向（交给玩家）、候选集合与文案。
    ///
    /// 可见性：只在场上没有玩家势力时出现（基类按 <see cref="PlayerControlledTarget"/> 统一裁决），
    /// 玩家已经在操作势力时这一项没有合法候选，直接隐藏。
    /// </summary>
    [GameSystem]
    public class GameJoinScenarioForce : GameForceControlMenuBase
    {
        /// <summary>
        /// 注册菜单项：order 取 210，排在设置菜单**最末尾**（现有项最大的是"返回主菜单"= 200，
        /// 菜单按 order 升序显示，见 ContextMenuData 的"order 越小越靠前"）。
        /// 控制权交接属于低频操作，放最后不挤占"保存/加载"这些常用项的位置，两项仍按方向相邻成对。
        /// </summary>
        public GameJoinScenarioForce()
        {
            customMenuName = "中途参加";
            customMenuOrder = 210;
        }

        /// <summary>本项是把控制权拿过来，因此只在"没有玩家势力"时出现。</summary>
        protected override bool PlayerControlledTarget
        {
            get { return true; }
        }

        /// <summary>候选 = 存活且不归玩家控制的势力。</summary>
        /// <returns>可接管的势力列表</returns>
        protected override List<Force> GetCandidates()
        {
            return ForceControlService.GetJoinableForces();
        }

        /// <summary>势力详情列表的窗口标题。</summary>
        protected override string SelectTitle
        {
            get { return "选择要接管的势力"; }
        }

        /// <summary>确认框文案。把"什么时候能上手"说清楚：控制位只在势力回合开始这个干净边界上翻。</summary>
        /// <param name="force">目标势力</param>
        /// <returns>确认框内容</returns>
        protected override string MakeConfirmText(Force force)
        {
            return $"确定接管【{force.Name}】，亲自操作该势力进行游戏吗？\n" +
                   "该势力本回合还没轮到行动的话，会立刻轮到它由你操作；\n" +
                   "若它正在行动或本回合已经行动完，则从下一回合起归你接管。";
        }

        /// <summary>无候选时的提示：全部势力都已由玩家控制。</summary>
        protected override string EmptyHint
        {
            get { return "现存势力都已由玩家控制，没有可接管的势力"; }
        }
    }
}
