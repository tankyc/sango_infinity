/*
 * 文件名：TroopActionDuel.cs
 * 描述：部队行动指令 —— 单挑。对应需求 1。
 *
 * 行为：
 *   · 在部队指令菜单里作为"单挑"项出现（排在攻击之后）。
 *   · 选择后高亮**落点周围**可单挑的敌对部队；点中即先移动过去、
 *     到达后再叫阵（与"攻击"的"先走过去再出手"一致）。
 *   · 到达后才做最终的相邻校验与发起，确保距离成立。
 *   · 接受判定 / 是否观看 / 是否带表现层全部交给 DuelChallengeFlow，
 *     本类只负责"选目标 + 移动过去"这一段。
 *
 * 复用：继承 TroopActionAttack，复用范围高亮（spellRangeCell / ShowSpellRange）
 *       与取消回退逻辑；移动部分按同样的 TroopMoveEvent 方式补上。
 */

using System.Collections.Generic;
using Sango.Render;
using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionDuel : TroopActionAttack
    {
        /// <summary>指令所处的阶段</summary>
        protected enum DuelPhase
        {
            /// <summary>还在选目标</summary>
            Selecting,
            /// <summary>正在移动过去</summary>
            Moving,
            /// <summary>已到位，准备叫阵</summary>
            Challenging,
            /// <summary>请求已发出，等待流程（对话框 + 单挑本体）走完</summary>
            Waiting,
        }

        /// <summary>本次选中的单挑目标</summary>
        protected Troop duelTarget;

        /// <summary>
        /// 叫阵本身是否消耗挑战方的行动。
        ///
        /// 注意区分两件事：
        ///   · 拒绝的惩罚（气力下降 / 士兵逃散）永远只落在**被挑战方**身上，挑战方不受影响；
        ///   · 本常量控制的是"叫阵算不算一次行动"，与惩罚无关。
        ///
        /// 默认 true（与《三国志11》一致：发起单挑即消耗该部队的行动）。
        /// 若希望被拒绝时挑战方不损失行动，改成 false 即可。
        /// </summary>
        public const bool ConsumeChallengerAction = true;

        protected DuelPhase phase = DuelPhase.Selecting;

        public TroopActionDuel()
        {
            customMenuName = "单挑";
            // 排在"攻击"(10)之后、"补给"(15)之前
            customMenuOrder = 12;
        }

        /// <summary>
        /// 可用条件：自身是战斗部队，且**这次的落点**相邻格上有具备单挑资格的敌对部队。
        /// 注意这里只做资格判定，不做距离校验——部队还没有走过去。
        /// 结果写入 spellRangeCell，供高亮与点击判定复用。
        /// </summary>
        public override bool IsValid
        {
            get
            {
                spellRangeCell.Clear();

                if (TargetTroop == null || !TargetTroop.IsFight) return false;

                Cell stayCell = ActionCell != null ? ActionCell : TargetTroop.cell;
                if (stayCell == null) return false;

                List<Cell> neighbors = stayCell.GetNeighbors();
                if (neighbors == null) return false;

                for (int i = 0; i < neighbors.Count; i++)
                {
                    Cell c = neighbors[i];
                    if (c == null || c.troop == null) continue;
                    if (!Duel.DuelManager.Instance.CanDuelEligible(TargetTroop, c.troop)) continue;
                    spellRangeCell.Add(c);
                }

                return spellRangeCell.Count > 0;
            }
        }

        public override void OnEnter()
        {
            // 基类会收起指令菜单、初始化移动路径并绘制 spellRangeCell 的范围高亮
            base.OnEnter();

            duelTarget = null;
            phase = DuelPhase.Selecting;
        }

        public override void Update()
        {
            switch (phase)
            {
                case DuelPhase.Challenging:
                    BeginChallenge();
                    break;

                case DuelPhase.Waiting:
                    // 还在等玩家回答「是否接受 / 是否观看」
                    if (Duel.DuelChallengeFlow.IsPending) return;
                    // 单挑本体还在进行
                    if (Duel.DuelManager.Instance.IsDueling) return;

                    // 整场单挑（含不观看时的瞬时结算）已经走完，收尾
                    phase = DuelPhase.Selecting;
                    TargetTroop.Render?.UpdateRender();
                    Done();
                    break;
            }
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, Vector3 clickPosition, bool isOverUI)
        {
            if (phase != DuelPhase.Selecting) return;

            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        GameSystemManager.Instance.BackTo(GameSystem.GetSystem<TroopSystem>());
                        break;
                    }

                case CommandEventType.Click:
                    {
                        if (isOverUI) return;
                        if (cell == null || cell.troop == null) return;
                        if (!spellRangeCell.Contains(cell)) return;
                        if (!Duel.DuelManager.Instance.CanDuelEligible(TargetTroop, cell.troop)) return;

                        duelTarget = cell.troop;
                        MoveToTarget();
                        break;
                    }
            }
        }

        /// <summary>先移动到落点，到达后再叫阵</summary>
        protected void MoveToTarget()
        {
            GameSystem.GetSystem<TroopActionMenu>().troopRender?.Clear();
            ContextMenu.CloseAll();

            Cell stayCell = MovePath != null && MovePath.Count > 0
                ? MovePath[MovePath.Count - 1]
                : TargetTroop.cell;

            if (TargetTroop.cell == stayCell)
            {
                phase = DuelPhase.Challenging;
                return;
            }

            Cell start = TargetTroop.cell;
            for (int i = 1; i < MovePath.Count; i++)
            {
                bool isLast = i == MovePath.Count - 1;
                Cell dest = MovePath[i];
                TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                @event.Init(TargetTroop, start, dest, isLast, isLast ? OnDuelMoveDone : null);
                RenderEvent.Instance.Add(@event);
                start = dest;
            }
            phase = DuelPhase.Moving;
        }

        /// <summary>移动结束，进入叫阵阶段</summary>
        protected void OnDuelMoveDone()
        {
            if (phase == DuelPhase.Moving)
                phase = DuelPhase.Challenging;
        }

        /// <summary>到位后正式提出单挑请求</summary>
        protected void BeginChallenge()
        {
            phase = DuelPhase.Selecting;
            ClearShowSpellRange();

            // 走到位之后再校验一次：距离、双方状态都以此为准
            if (!Duel.DuelManager.Instance.CanStartDuel(TargetTroop, duelTarget))
            {
                Done();
                return;
            }

            bool acceptedFlow = Duel.DuelChallengeFlow.Request(
                TargetTroop, duelTarget, Duel.DuelChallengeFlow.Source.TroopCommand);

            if (!acceptedFlow)
            {
                // 当场被否决（AI 拒绝 / 条件不成立），本次指令到此为止
                Done();
                return;
            }

            // 单挑成立：之后由 Update() 等待流程（对话框 + 单挑本体）走完
            if (ConsumeChallengerAction)
                TargetTroop.ActionOver = true;
            phase = DuelPhase.Waiting;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            duelTarget = null;
            phase = DuelPhase.Selecting;
        }
    }
}
