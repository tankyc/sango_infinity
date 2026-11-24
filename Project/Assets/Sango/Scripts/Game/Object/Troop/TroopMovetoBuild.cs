using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopMovetoBuild : TroopMissionBehaviour
    {
        Cell FinalCell { get; set; }
        public override MissionType MissionType { get { return MissionType.TroopMovetoBuild; } }
        public override bool IsMissionComplete
        {
            get
            {
                if (TargetCell == null)
                    return true;

                BuildingBase building = TargetCell.building;
                if (building != null)
                {
                    // 目标格已建成（己方且完工）→ 任务完成
                    if (building.IsSameForce(Troop) && building.isComplate)
                        return true;
                    // 目标格被别人占了 → 放弃
                    if (!building.IsSameForce(Troop))
                        return true;
                }

                // 【陷阱 / 障碍物不参与建筑间距】本次任务要修的正是这两类设施：
                // 它们修建时不判任何间距，所以"周围已有建筑"不能成为放弃理由。
                // 原来这里用无过滤的 SpiralHasBuilding，紧挨着任何建筑（包括另一处陷阱、
                // 农田、障碍物）都会让部队走到目的地后直接放弃，玩家看到的是
                // "点得动、也派得出去，到了却什么都没建"。
                BuildingType targetType = TargetBuildingType;
                if (targetType != null && (targetType.IsExplosive || targetType.IsObstacle))
                    return false;

                // 其余设施只把"军事设施 / 城港关"视为阻挡，与 BuildingType.CanBuildToHere 同口径
                return TargetCell.SpiralHasBuilding(Scenario.Cur.Variables.BuildingSpace, sIsBlockingBuilding);
            }
        }

        /// <summary>
        /// 间距判定用的委托缓存。
        /// <para>
        /// 【为什么要缓存】<see cref="IsMissionComplete"/> 在部队移动判定里被高频访问，
        /// 直接传方法组会每次转换出一个新的 <see cref="System.Func{T,TResult}"/> 实例 ——
        /// 那是实打实的 GC 分配。这里缓存成静态只读字段，全程零分配。
        /// </para>
        /// </summary>
        static readonly System.Func<BuildingBase, bool> sIsBlockingBuilding = IsBlockingBuilding;

        /// <summary>
        /// 间距判定里"算数"的建筑：军事设施与城/港/关。
        /// 内政设施、陷阱、障碍物在间距上完全透明，与 <see cref="BuildingType.CanBuildToHere"/> 保持一致。
        /// </summary>
        /// <param name="b">待判定的既有建筑</param>
        /// <returns>会阻挡本次修建返回 true</returns>
        static bool IsBlockingBuilding(BuildingBase b)
        {
            if (b == null || b.BuildingType == null)
                return false;
            BuildingType type = b.BuildingType;
            return !type.IsIntrior && !type.IsObstacle && !type.IsExplosive;
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetBuildingType == null || TargetBuildingType.Id != troop.missionTarget) TargetBuildingType = scenario.GetObject<BuildingType>(Troop.missionTarget);
            if (TargetCity == null) TargetCity = troop.BelongCity;
            if (TargetCell == null) TargetCell = troop.missionTargetCell;

            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                // 全局和平期 / 军团被设为禁止攻击：归属城即使失守也不改派为"攻打它"，
                // 改为返城待命，避免 AI 部队自主进攻。
                if (Troop.BelongCity.IsSameForce(Troop) || scenario.IsPeacePeriod
                    || TroopAIUtility.IsAttackForbidden(Troop))
                {
                    Troop.SetMission(MissionType.TroopReturnCity, Troop.BelongCity.Id);
                }
                else
                {
                    Troop.SetMission(MissionType.TroopOccupyCity, TargetCity.Id);
                }
                Troop.NeedPrepareMission();
                return;

            }


            if (troop.MoveRange.Count == 0)
            {
                scenario.Map.GetMoveRange(troop, troop.MoveRange);
#if SANGO_DEBUG_AI
                GameAIDebug.Instance.ShowMoveRange(troop.MoveRange, troop);
#endif
            }

            List<Cell> emptyCell = new List<Cell>();
            for (int i = 0; i < 6; i++)
            {
                Cell cell = TargetCell.GetNeighbor(i);
                // 边界格的邻居可能为空，跳过
                if (cell == null)
                    continue;
                if (cell == troop.cell)
                {
                    FinalCell = cell;
                    return;
                }
                else if (cell.IsEmpty() && troop.MoveRange.Contains(cell))
                {
                    emptyCell.Add(cell);
                }
            }

            if (emptyCell.Count > 0)
            {
                emptyCell.Sort((a, b) => a.Distance(troop.cell).CompareTo(b.Distance(troop.cell)));
                FinalCell = emptyCell[0];
            }
            else
            {
                FinalCell = null;
            }
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (IsMissionComplete)
            {
                Troop.actionRenderEvent = null;
                Troop.NeedPrepareMission();
                return true;
            }

            if (FinalCell != null)
            {
                if (!troop.MoveTo(FinalCell))
                    return false;

                if (!troop.BuildBuilding(TargetCell, TargetBuildingType))
                    return false;
                return true;

            }

            return troop.TryCloseTo(TargetCell);
        }
    }
}
