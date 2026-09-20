using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopBuildBuilding : TroopMissionBehaviour
    {
        Cell FinalCell { get; set; }
        public override MissionType MissionType { get { return MissionType.TroopBuildBuilding; } }
        public override bool IsMissionComplete
        {
            get
            {
                if (TargetCell == null
                    || TargetBuildingType == null
                    || (TargetCell.building != null && TargetCell.building.IsSameForce(Troop) && TargetCell.building.isComplate)
                    || (TargetCell.building != null && !TargetCell.building.IsSameForce(Troop)))
                    return true;

                if(TargetCell.building == null)
                {
                    if(!TargetBuildingType.CanBuildToHere(TargetCell))
                        return true;

                    if(Troop.gold < TargetBuildingType.cost)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 本工程队已连续修建的建筑数量（借存在 <c>missionParams1</c> 中）。
        ///
        /// 说明：<c>missionParams1/2</c> 在求援任务里用于"记录原任务以便恢复"，
        /// 而工程队不需要恢复原任务，因此该字段可安全复用为"连建计数"。
        /// </summary>
        int BuiltCount
        {
            get { return Troop != null ? Troop.missionParams1 : 0; }
            set { if (Troop != null) Troop.missionParams1 = value; }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetBuildingType == null || TargetBuildingType.Id != troop.missionTarget) TargetBuildingType = scenario.GetObject<BuildingType>(Troop.missionTarget);
            if (TargetCity == null) TargetCity = troop.mBelongCity;

            // 【连续建造】目标格需每次都核对：工程队建完一座后会被改派到新的建址，
            // 原来的"仅在 TargetCell == null 时赋值"会导致它一直围着旧目标打转。
            if (troop.missionTargetCell != null && TargetCell != troop.missionTargetCell)
            {
                TargetCell = troop.missionTargetCell;
                FinalCell = null;               // 目标已变，落脚点需重算
            }

            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                // 【连续建造】工程队建完一座后，先在附近寻找下一个值得修建的位置继续施工；
                // 只有找不到建址（或资金花光 / 达到连建上限）才回城，避免来回奔波浪费行程。
                // 注意 NeedPrepareMission() 会清空行为对象缓存，因此这里直接返回即可。
                if (TryFindNextSite(scenario))
                    return;

                // 【随军增筑】若本次施工源自作战部队的"就地增筑"（见 Troop.TryBuildFieldBuilding），
                // 完工后恢复原任务让其重返战线，而不是像专职工程队那样回城待命。
                if (Troop.fieldBuildReturnMission != 0)
                {
                    MissionType backMission = (MissionType)Troop.fieldBuildReturnMission;
                    int backTarget = Troop.fieldBuildReturnTarget;
                    Troop.fieldBuildReturnMission = 0;
                    Troop.fieldBuildReturnTarget = 0;
                    Troop.missionParams1 = 0;       // 连建计数归零，不影响原任务的后续逻辑
                    Troop.SetMission(backMission, backTarget);
                    Troop.NeedPrepareMission();
                    return;
                }

                if (Troop.mBelongCity != null && Troop.mBelongCity.IsSameForce(Troop))
                {
                    Troop.SetMission(MissionType.TroopReturnCity, Troop.mBelongCity.Id);
                }
                else if (TargetCity != null)
                {
                    Troop.SetMission(MissionType.TroopOccupyCity, TargetCity.Id);
                }
                else
                {
                    Troop.SetMission(MissionType.TroopReturnCity, Troop.missionTarget);
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
                Cell cell = TargetCell.Neighbors[i];
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

        /// <summary>
        /// 当前建筑完工后，在附近寻找下一个值得修建的位置，实现"一次出行连续修建"。
        ///
        /// 以下任一情况即放弃连建（返回 false，由调用方安排回城）：
        ///   · 未开启连建（<c>frontBuildContinueAfterDone</c>）
        ///   · 已达到连建上限（<c>frontBuildMaxContinuous</c>）
        ///   · 携带资金已不足最便宜的一座建筑
        ///   · 搜索范围内没有评分达标的建址（<see cref="BattleSituation.EvaluateFrontSite"/>）
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否已成功改派到新建址</returns>
        bool TryFindNextSite(Scenario scenario)
        {
            AIConfig cfg = AIConfig.Instance;
            if (!cfg.frontBuildContinueAfterDone)
                return false;

            if (cfg.frontBuildMaxContinuous > 0 && BuiltCount >= cfg.frontBuildMaxContinuous)
                return false;

            Troop troop = Troop;
            if (troop == null || troop.cell == null || troop.mBelongForce == null)
                return false;

            List<BuildingType> candidates = troop.mBelongForce.canBuildMilitaryBuildingType;
            if (candidates == null || candidates.Count == 0)
                return false;

            // 剩余资金不足最便宜的一座建筑 → 回城补充
            int minCost = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                BuildingType t = candidates[i];
                if (t != null && t.cost < minCost)
                    minCost = t.cost;
            }
            if (minCost == int.MaxValue || troop.gold < minCost)
                return false;

            // 以工程队当前位置为中心重新评选建址
            BattleSituation.FrontSiteInfo best = new BattleSituation.FrontSiteInfo();
            best.Clear();

            int range = Math.Max(1, cfg.frontBuildSearchRange);
            Cell oldTarget = TargetCell;
            scenario.Map.SpiralAction(troop.cell, range, (cell) =>
            {
                if (cell == oldTarget)
                    return;                     // 刚建完的位置不再考虑

                BattleSituation.FrontSiteInfo info =
                    BattleSituation.EvaluateFrontSite(cell, troop.mBelongForce, scenario);
                if (!info.isValid || info.cell == null)
                    return;

                if (best.cell == null || info.score > best.score)
                    best = info;
            });

            if (!best.isValid || best.cell == null)
                return false;

            BuildingType next = BattleSituation.SelectFrontBuildingType(best, troop.mBelongForce, scenario);
            if (next == null || troop.gold < next.cost)
                return false;

            // 改派到新建址：换目标格 + 换建筑类型
            BuiltCount = BuiltCount + 1;
            troop.missionTargetCell = best.cell;
            troop.SetMission(MissionType.TroopBuildBuilding, next.Id);
            troop.NeedPrepareMission();
            return true;
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

                // 【防重复建造】目标格已有己方建筑（含仍在施工中的）时不再发起建造，
                // 否则工程队会在建筑完工前每回合重复调用 BuildBuilding。
                if (TargetCell.building != null && TargetCell.building.IsSameForce(troop))
                    return true;

                if (!troop.BuildBuilding(TargetCell, TargetBuildingType))
                    return false;
                return true;

            }

            return troop.TryCloseTo(TargetCell);
        }
    }
}
