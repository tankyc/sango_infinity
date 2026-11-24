using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 部队委任-移动到指定格子。
    ///
    /// 【为什么也要接"据点格"】Open311 点建筑时走的是 `move_to_building`
    /// （`s11_sys_unit_order.cpp:168-216`）：只有**无主或敌对**据点才转 `attack_building`，
    /// 其余（自己人 / 同盟 / 停战）一律"往据点方向移动、走到跟前即算到达"
    /// （到达判定见 `is_reached`，同文件 :233-247）。
    /// 本项目把攻占 / 摧毁拆成独立指令后，"非敌对据点"只剩空点击 ——
    /// 攻城指令被挡掉、又没有对应的移动入口，表现就是点了毫无反应。
    /// 这里补上那一半：非敌对据点交给本指令，目标是"据点外围可停留的一格"。
    /// </summary>
    [GameSystem]
    public class TroopInteractiveMoveToCell : TroopInteractiveBase
    {
        public Cell TargetCell { get; set; }

        public TroopInteractiveMoveToCell()
        {
            customMenuName = "移动";
            customMenuOrder = 0;
        }

        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        protected override bool Check(Troop troop, Cell actionCell)
        {
            // 【非敌对据点：只能移动】同势力 / 同盟 / 停战势力的城港关不可攻击，
            // 点击改派"移动"。这条必须在 MoveRange 判定之前：据点格可能是可通行的
            //（同盟据点 CanPassThrough 放行），落在移动范围内时走的是"直接交互对话框"分支，
            // 不能被范围判定提前挡掉。
            if (actionCell.building != null)
            {
                if (troop.IsSameForce(actionCell.building) || troop.IsEnemy(actionCell.building))
                    return false;

                TargetCell = actionCell;
                content = string.Format("即将往{0}方向移动。\n确定吗？", actionCell.building.ColorName);
                return true;
            }

            if (!actionCell.CanStay(troop) || !actionCell.moveAble) return false;
            if (troop.MoveRange.Contains(actionCell)) return false;

            TargetCell = actionCell;
            content = string.Format("即将往坐标<{0},{1}>进行移动。\n确定吗？", actionCell.x, actionCell.y);
            return true;

        }

        public override void OnEnter()
        {
            base.OnEnter();

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);

            // 据点格不能停留，落脚点换成"据点跟前"的可停留格（详见 ResolveStopCell）
            Cell moveTarget = ResolveStopCell(TargetTroop, TargetCell);
            TargetTroop.SetMission(MissionType.TroopMovetoCell, 0);
            TargetTroop.missionParams1 = moveTarget.x;
            TargetTroop.missionParams2 = moveTarget.y;
            TargetTroop.NeedPrepareMission();
            TargetTroop.Render?.UpdateRender();
        }

        public override void Update()
        {
            base.Update();
            if (!TargetTroop.IsAlive || TargetTroop.ActionOver)
            {
                Done();
                return;
            }
            if (!TargetTroop.DoAI(Scenario.Cur))
                return;

            OnAIDone();
        }

        public override void OnDestroy()
        {

        }

        public void OnAIDone()
        {
            TargetTroop.ActionOver = true;
            TargetTroop.Render?.UpdateRender();
            Done();
        }

        /// <summary>
        /// 把"点击的目标格"换算成"实际落脚格"。
        ///
        /// 【为什么需要】据点格上永远站着建筑，而 <see cref="Cell.CanStay"/> 要求格上无建筑 ——
        /// 直接把据点格当移动目标，<see cref="TroopMovetoCell"/> 的完成判定
        /// （<c>TargetCell == Troop.cell</c>）永远不成立，部队会停在城下却一直挂着"移动"任务。
        /// 因此把目标换成据点外围一圈里离部队最近、且能停留的格子，
        /// 与 Open311 `is_reached`（`s11_sys_unit_order.cpp:233-247`）"进入据点外围即算到达"一致。
        ///
        /// 【兜底】外围一圈全被占满（被围死 / 环上全是水与山）时退回原格：
        /// <see cref="Troop.TryMoveToCell(Cell)"/> 自己会就近停在能走的格子上，不在这里重复那套逻辑。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="target">玩家点击的目标格</param>
        /// <returns>可停留的落脚格；没有更优解时返回 target 本身</returns>
        static Cell ResolveStopCell(Troop troop, Cell target)
        {
            if (troop == null || target == null) return target;
            // 普通空地，或据点格理论上可停留（建筑已毁的瞬间）：直接用
            if (target.building == null || target.CanStay(troop)) return target;

            Map map = Scenario.Cur.Map;
            if (map == null) return target;

            // 据点外围：城 / 港 / 关的占地半径 + 1（与 BuildingBase.IsBeSurrounded 同口径）
            int radius = (target.building.BuildingType != null ? (int)target.building.BuildingType.radius : 0) + 1;
            List<Cell> ring = new List<Cell>();
            map.GetRing(target.x, target.y, radius, ring);

            Cell best = null;
            int bestDist = int.MaxValue;
            for (int i = 0; i < ring.Count; i++)
            {
                Cell cell = ring[i];
                if (cell == null || !cell.CanStay(troop)) continue;
                int dist = map.Distance(troop.cell, cell);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = cell;
                }
            }
            return best != null ? best : target;
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {

        }

    }
}
