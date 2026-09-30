/*
 * 文件名：GameForceControlMenuBase.cs
 * 描述：设置菜单中"势力控制权交接"类命令（中途参加 / 委托给AI）的公共流程基类
 * 创建日期：2026-09-29
 * 作者：XBJ
 */

using System;
using System.Collections.Generic;
using Sango.UI;
using UnityEngine;

namespace Sango.Core.Player
{
    /// <summary>
    /// "势力控制权交接"菜单命令的公共基类。
    ///
    /// 承担两个方向（玩家接管势力 / 玩家把势力交给AI）**完全一致**的那套流程：
    ///   进入命令 → 冻结回合推进 → 打开势力选择列表 → 选中一个势力 → 确认框 →
    ///   调用 <see cref="ForceControlService.TrySetPlayerControl"/> → 退出命令并解冻。
    /// 子类只提供四样差异：候选集合、选择窗标题、确认框文案、移交方向；
    /// 真正的状态变更一律走服务，基类和子类都不许自己写 <c>Force.IsPlayer</c>，
    /// 否则又会出现"改了 bool 没改存档数组"的假切换。
    ///
    /// 势力列表用工程现成的 <see cref="ForceSelectSystem"/>（外交选目标势力、军团任命都是它）：
    /// 它打开 <c>window_object_selector</c>，按势力详情逐行列出、自带表头排序，
    /// 不是右键菜单那种瀑布式下拉。传 limit = 1 时它进入点选模式，
    /// 点一行就等于选完（回调立刻拿到结果并自行退栈），"确定/再选"由本类的确认框承担。
    /// 单选口径也因此天然成立，不需要额外勾选控件。
    /// </summary>
    public abstract class GameForceControlMenuBase : GameSettingMenuBase
    {
        /// <summary>已经点选、正在等确认框回话的势力。确认框是异步回调，靠它把目标带到回调里。</summary>
        Force pendingForce;

        /// <summary>本次交接要交给谁：true = 交给玩家（中途参加），false = 交给AI（委托）。</summary>
        protected abstract bool PlayerControlledTarget { get; }

        /// <summary>
        /// 注册设置菜单项，并按"此刻有没有玩家在操作势力"决定本项是否出现：
        /// 放置模式（场上没有玩家势力）只给"中途参加"，玩家模式只给"委托给AI"，两项互斥。
        ///
        /// 判据用运行时控制位（<see cref="ForceControlService.HasPlayerControlledForce"/>）而不是存档数组：
        /// 交接排队等待生效的那一回合里，玩家此刻确实还在操作旧势力，所见即所得。
        /// 采用"隐藏"而不是"置灰"，是因为另一项在当前状态下没有任何合法候选，灰着点只会得到一句空提示。
        /// </summary>
        /// <param name="menuData">设置菜单数据</param>
        protected override void OnGameSettingContextMenuShow(IContextMenuData menuData)
        {
            // PlayerControlledTarget = true（参加）→ 只在"没有玩家势力"时出现；false（委托）→ 只在"有玩家势力"时出现
            if (ForceControlService.HasPlayerControlledForce() == PlayerControlledTarget)
                return;

            base.OnGameSettingContextMenuShow(menuData);
        }

        /// <summary>本命令的候选势力集合（服务侧已过滤为"存活 + 控制权状态符合方向"）。</summary>
        /// <returns>候选势力列表</returns>
        protected abstract List<Force> GetCandidates();

        /// <summary>势力选择列表的窗口标题。</summary>
        protected abstract string SelectTitle { get; }

        /// <summary>确认框文案。子类有责任在这一句里把该方向的后果（例如AI不代管的操作）写清楚。</summary>
        /// <param name="force">目标势力</param>
        /// <returns>确认框内容</returns>
        protected abstract string MakeConfirmText(Force force);

        /// <summary>无候选时的提示。</summary>
        protected abstract string EmptyHint { get; }

        /// <summary>
        /// 进入命令：先冻结回合推进，再打开势力列表。
        /// 冻结放在最前面，是为了让"没有候选势力"这条分支也走同一套解冻出口（OnDestroy）。
        /// </summary>
        public override void OnEnter()
        {
            ForceControlService.BlockTurn(this);
            ShowForceSelector();
        }

        /// <summary>
        /// 弹出势力选择列表；一个都没有时不开空列表，直接提示并结束命令。
        /// </summary>
        void ShowForceSelector()
        {
            List<Force> candidates = GetCandidates();
            if (candidates == null || candidates.Count == 0)
            {
                Done();
                // 两个回调都给空实现：这条只是告知，不需要玩家做选择，
                // 但缺 cancelAction 的重载会把"取消"退化成"确定"（本工程已知口径），这里显式避开。
                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, EmptyHint, () => { }, () => { });
                return;
            }

            ForceSelectSystem selectSystem = GameSystem.GetSystem<ForceSelectSystem>();
            if (selectSystem == null)
            {
                // 选择系统没注册上来属于工程级异常，必须报出来而不是让玩家以为"点了没反应"
                Sango.Log.Error("ForceSelectSystem 未注册,无法打开势力选择列表");
                Done();
                return;
            }

            // limit = 1 → 点选模式：点中一行即完成选择；resultList 传空列表表示"当前没有已选项"
            selectSystem.Start(candidates, new List<Force>(), 1, OnForcePicked, null, SelectTitle);
        }

        /// <summary>
        /// 列表里点中一个势力（点选模式下选择器随即自行退栈，本命令重新成为栈顶）。
        /// 这里只记下目标并弹确认框，真正的状态变更等玩家点确定。
        /// </summary>
        /// <param name="forces">选择器回传的已选势力，点选模式下固定只有一个</param>
        void OnForcePicked(List<Force> forces)
        {
            if (forces == null || forces.Count == 0)
            {
                // 空结果只可能是列表被中途清空：回到列表重选，不静默退出
                ShowForceSelector();
                return;
            }

            pendingForce = forces[0];
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                MakeConfirmText(pendingForce), OnConfirm, OnCancelSelect);
        }

        /// <summary>
        /// 玩家点了确定：执行移交并结束命令（结束时会经由 OnDestroy 解冻回合推进）。
        /// 移交被服务侧拒绝时必须给反馈，否则玩家只会看到"点了没反应"。
        /// </summary>
        void OnConfirm()
        {
            Force force = pendingForce;
            pendingForce = null;

            if (force == null)
            {
                Done();
                return;
            }

            if (!ForceControlService.TrySetPlayerControl(force, PlayerControlledTarget))
            {
                // 候选列表已按"存活 + 控制权方向"过滤，且界面打开期间回合推进是冻结的，
                // 所以这里基本只会命中"重复点同一个目标（意图已一致）"。
                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                    $"【{force.Name}】无需重复移交或状态已变化，请重新选择",
                    OnReShowList, OnReShowList);
                return;
            }

            // 接管方向把镜头带到该势力首都，让玩家先看清自己接手了什么；委托方向不动镜头。
            if (PlayerControlledTarget)
                ForceControlService.FocusCameraOnCapital(force);

            Done();
        }

        /// <summary>移交未成功时的出口：提示关掉后回到势力列表继续选，命令不退出、回合保持冻结。</summary>
        void OnReShowList()
        {
            ShowForceSelector();
        }

        /// <summary>
        /// 玩家点了取消：回到势力列表继续挑，命令不退出，回合保持冻结。
        /// </summary>
        void OnCancelSelect()
        {
            pendingForce = null;
            ShowForceSelector();
        }

        /// <summary>
        /// 下层命令（势力选择列表）退出后回到本命令时触发。两种情况要分开：
        ///   · 玩家在列表里点选了势力 —— 此时确认框正在等回话，pendingForce 已置位，交给确认框处理；
        ///   · 玩家直接取消列表（取消按钮 / 右键 / ESC）—— 说明不想交接了，整条命令退出并解冻。
        /// 注意 <c>Back</c> 不会重跑 <see cref="OnEnter"/>，所以这里不能指望 OnEnter 再驱动一遍流程。
        /// </summary>
        /// <param name="whoGone">刚刚退栈的那个命令</param>
        public override void OnBack(ICommandEvent whoGone)
        {
            if (pendingForce != null)
                return;

            Done();
        }

        /// <summary>
        /// 地图输入处理，判据与 <c>GameSettingSystem</c> 保持一致：
        /// 右键 / ESC 退出命令；点在界面（列表、确认框）上不做任何事，交给 UI 自己的按钮回调。
        /// </summary>
        /// <param name="eventType">命令事件类型</param>
        /// <param name="cell">当前格</param>
        /// <param name="clickPosition">点击位置</param>
        /// <param name="isOverUI">是否点在界面上</param>
        public override void HandleEvent(CommandEventType eventType, Cell cell, Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClickDown:
                    if (isOverUI)
                        return;
                    Back();
                    break;

                case CommandEventType.ClickDown:
                    if (isOverUI)
                        return;
                    Done();
                    break;
            }
        }

        /// <summary>
        /// 命令离开时的统一出口：丢掉待确认目标、解除回合冻结。
        /// 确定（OnDone 默认转 OnDestroy）、取消、被外部拆栈都会走这里，因此解冻只需写一遍。
        /// </summary>
        public override void OnDestroy()
        {
            pendingForce = null;
            ForceControlService.UnblockTurn(this);
        }
    }
}
