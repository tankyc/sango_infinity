using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopTransformGoodsToCity : TroopMissionBehaviour
    {
        internal static List<Cell> tempCellList = new List<Cell>(256);

        public override MissionType MissionType { get { return MissionType.TroopTransformGoodsToCity; } }

        /// <summary>
        /// 是否允许"目标城即将失守时自动回撤"。
        /// 势力 AI 的运输队为 true；玩家自己委派的运输队（<c>PlayerTroopTransformGoodsToCity</c>）覆写为 false
        /// —— 要不要冒险把货送进围城，由玩家自己决定。
        /// </summary>
        protected virtual bool AutoRetreatWhenDoomed { get { return true; } }

        /// <summary>
        /// 任务是否结束：目标城已易主 / 已不存在，或**即将失守**。
        ///
        /// 【及时回撤】"兵临城下且守军明显劣势"时同样判为结束，
        /// 于是 <see cref="Prepare"/> 会把运输队转回创建城（<c>TroopReturnCity</c>），
        /// 整车的金 / 粮 / 兵 / 军械原路带回 —— 免得跟着城池一起丢。
        /// 判定与调度侧共用 <see cref="ResourceBalance.IsTargetDoomed"/>，两边不会走偏。
        /// </summary>
        public override bool IsMissionComplete
        {
            get
            {
                if (TargetCity == null || !TargetCity.IsSameForce(Troop))
                    return true;

                if (AutoRetreatWhenDoomed && Troop != null && !Troop.IsPlayerControl)
                {
                    AIConfig config = AIConfig.Instance;
                    ResourceDispatchWeights w = config != null ? config.resourceDispatch : null;
                    if (ResourceBalance.IsTargetDoomed(TargetCity, w != null ? w.doomedDefenseRatio : 0f))
                        return true;
                }
                return false;
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);
           
            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                // 【说明】原先此处有 `if (troop.IsPlayerControl) ClearMission()` 的补丁。
                // 玩家第一军团现已改走 PlayerTroopTransformGoodsToCity，
                // 由该类型负责"交回玩家"，故此处只保留势力 AI 的处理。
                //
                // 走到这里有两种原因：① 城池已易主 / 消失；② 目标城即将失守（自动回撤）。
                // 两种都按"回创建城"处理：货与人原路带回，避免随城损失。
                Sango.Log.Info($"{scenario.GetDateStr()}运输队[{troop.Name}]的目标<{TargetCity?.Name}>已易主或将失守，原路返回{Troop.BelongCity?.Name}");
                Troop.SetMission(MissionType.TroopReturnCity, Troop.BelongCity.Id);
                Troop.NeedPrepareMission();
                return;
            }

            tempCellList.Clear();
            scenario.Map.GetDirectPath(Troop.cell, TargetCity.CenterCell, tempCellList);
            for (int i = 0; i < tempCellList.Count; ++i)
            {
                Cell road = tempCellList[i];
                if (road.building != null && !road.building.IsCity() && !road.building.IsSameForce(Troop))
                {
                    priorityActionData = TroopAIUtility.PriorityAction(Troop, road, scenario);
                    break;
                }
            }

        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
                return true;
            }

            if (priorityActionData != null)
            {
                if (!priorityActionData.moveFinish && !troop.MoveTo(priorityActionData.movetoCell))
                    return false;
                if (!priorityActionData.moveFinish)
                    priorityActionData.moveFinish = true;
                if (!troop.SpellSkill(priorityActionData.skill, priorityActionData.spellCell))
                    return false;
                return true;
            }
            else
            {
                if (troop.TryMoveToCity(TargetCity))
                {
                    // 移动完成，进入城市
                    if (troop.cell.building == TargetCity)
                    {
                        troop.EnterCity(TargetCity);
                    }
                    return true;
                }
                return false;
            }
        }

    }
}
