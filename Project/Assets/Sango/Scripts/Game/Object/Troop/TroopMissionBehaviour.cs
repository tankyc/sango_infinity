using System;
using System.Collections.Generic;
using static Sango.Core.TroopAIUtility;

namespace Sango.Core
{
    public abstract class TroopMissionBehaviour
    {
        public List<Cell> canMovedCells = new List<Cell>();
        public Troop Troop { get; set; }
        public Troop TargetTroop { get; set; }
        public Building TargetBuilding { get; set; }
        public Person TargetPerson { get; set; }
        public City TargetCity { get; set; }
        public Cell TargetCell { get; set; }
        public BuildingType TargetBuildingType { get; set; }
        public abstract MissionType MissionType { get; }
        public abstract bool IsMissionComplete { get; }
        public abstract bool DoAI(Troop troop, Scenario scenario);
        public virtual void Prepare(Troop troop, Scenario scenario) { }

        protected PriorityActionData priorityActionData;

        public static TroopMissionBehaviour Create(int missionType)
        {
            switch (missionType)
            {
                case (int)MissionType.TroopDestroyTroop:
                    return new TroopDestroyTroop();
                case (int)MissionType.TroopDestroyBuilding:
                    return new TroopDestroyBuilding();
                case (int)MissionType.TroopOccupyCity:
                    return new TroopOccupyCity();
                case (int)MissionType.TroopBanishTroop:
                    return new TroopBanishTroop();
                case (int)MissionType.TroopProtectBuilding:
                    return new TroopProtectBuilding();
                case (int)MissionType.TroopProtectTroop:
                    return new TroopProtectTroop();
                case (int)MissionType.TroopProtectCity:
                    return new TroopProtectCity();
                case (int)MissionType.TroopMovetoCity:
                    return new TroopMovetoCity();
                case (int)MissionType.TroopReturnCity:
                    return new TroopReturnCity();
                case (int)MissionType.TroopBuildBuilding:
                    return new TroopBuildBuilding();
                case (int)MissionType.TroopFixBuilding:
                    return new TroopFixBuilding();
                case (int)MissionType.TroopTransformGoodsToCity:
                    return new TroopTransformGoodsToCity();
                case (int)MissionType.TroopMovetoCell:
                    return new TroopMovetoCell();
                case (int)MissionType.TroopMovetoBuild:
                    return new TroopMovetoBuild();
                case (int)MissionType.TroopSupplyTroop:
                    return new TroopSupplyTroop();
                case (int)MissionType.TroopAskSupply:
                    return new TroopAskSupply();
                case (int)MissionType.TroopStay:
                    return new TroopStay();

                // ---------- 委任部队（玩家第一军团）专用行为 ----------
                case (int)MissionType.PlayerTroopReturnCity:
                    return new PlayerTroopReturnCity();
                case (int)MissionType.PlayerTroopMovetoCity:
                    return new PlayerTroopMovetoCity();
                case (int)MissionType.PlayerTroopDestroyTroop:
                    return new PlayerTroopDestroyTroop();
                case (int)MissionType.PlayerTroopDestroyBuilding:
                    return new PlayerTroopDestroyBuilding();
                case (int)MissionType.PlayerTroopOccupyCity:
                    return new PlayerTroopOccupyCity();
                case (int)MissionType.PlayerTroopBanishTroop:
                    return new PlayerTroopBanishTroop();
                case (int)MissionType.PlayerTroopBuildBuilding:
                    return new PlayerTroopBuildBuilding();
                case (int)MissionType.PlayerTroopFixBuilding:
                    return new PlayerTroopFixBuilding();
                case (int)MissionType.PlayerTroopTransformGoodsToCity:
                    return new PlayerTroopTransformGoodsToCity();
                case (int)MissionType.PlayerTroopMovetoCell:
                    return new PlayerTroopMovetoCell();

                default:
                    return new TroopReturnCity();
            }
        }

        /// <summary>
        /// 把面向势力 AI 的任务枚举映射为委任部队专用的对应枚举。
        ///
        /// <c>Troop.SetMission</c> 会在部队属于**玩家第一军团**时先调用本方法，
        /// 因此 UI 指令层（TroopInteractive*.cs / CityTransport / TroopActionBuild）
        /// 与行为类内部的改派代码都无需关心"这支部队是不是委任部队"——
        /// 枚举映射在一处收敛完成。
        ///
        /// 没有委任对应项的任务（势力 AI 专属，如部队补给 / 求援 / 协防城池）
        /// 原样返回，因为玩家侧不存在指派它们的入口。
        /// </summary>
        /// <param name="missionType">原任务</param>
        /// <returns>委任部队应使用的任务枚举</returns>
        public static MissionType ToPlayerMission(MissionType missionType)
        {
            switch (missionType)
            {
                case MissionType.TroopReturnCity: return MissionType.PlayerTroopReturnCity;
                case MissionType.TroopMovetoCity: return MissionType.PlayerTroopMovetoCity;
                case MissionType.TroopMovetoCell: return MissionType.PlayerTroopMovetoCell;
                case MissionType.TroopDestroyTroop: return MissionType.PlayerTroopDestroyTroop;
                case MissionType.TroopDestroyBuilding: return MissionType.PlayerTroopDestroyBuilding;
                case MissionType.TroopOccupyCity: return MissionType.PlayerTroopOccupyCity;
                case MissionType.TroopBanishTroop: return MissionType.PlayerTroopBanishTroop;
                case MissionType.TroopBuildBuilding: return MissionType.PlayerTroopBuildBuilding;
                case MissionType.TroopFixBuilding: return MissionType.PlayerTroopFixBuilding;
                case MissionType.TroopTransformGoodsToCity: return MissionType.PlayerTroopTransformGoodsToCity;
                default:
                    // 势力 AI 专属任务（补给 / 求援 / 协防城池 / 移动到建筑等）原样返回
                    return missionType;
            }
        }

        /// <summary>
        /// 判断某任务是否为委任部队专用任务。
        ///
        /// 依赖"PlayerTroop 系列枚举连续且位于枚举末尾"这一约定（见 MissionType.cs）。
        /// </summary>
        /// <param name="missionType">待判断的任务</param>
        /// <returns>是否为委任专用任务</returns>
        public static bool IsPlayerMission(MissionType missionType)
        {
            return missionType >= MissionType.PlayerTroopReturnCity
                && missionType <= MissionType.PlayerTroopMovetoCell;
        }

    }
}
