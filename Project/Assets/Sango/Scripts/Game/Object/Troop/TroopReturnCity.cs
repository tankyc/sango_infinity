using static Sango.Core.TroopAIUtility;

namespace Sango.Core
{
    public class TroopReturnCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopReturnCity; } }
        public override bool IsMissionComplete
        {
            get
            {
                // 【防御】TargetCity 尚未解析到（目标 Id 无效 / 城池已被摧毁）或行为对象还没绑定部队时，
                // 原写法 `TargetCity != Troop.BelongCity` 在两者都为 null 的情况下为 false，
                // 会继续走到 `!TargetCity.IsSameForce(...)` 抛 NullReferenceException
                //（Troop.BelongCity 派生自主将，见 Troop.cs:173，异族等无城部队可能为 null）。
                // 返回 true 表示"本任务已失效"，由 Prepare 负责改派或清空。
                if (Troop == null || TargetCity == null)
                    return true;

                return TargetCity != Troop.BelongCity || !TargetCity.IsSameForce(Troop);
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);
            priorityActionData = null;

            // 这种情况发生在玩家把部队放在外面没有任何任务,然后将此座城池设置成军团了
            if (TargetCity == null)
            {
                TargetCity = troop.BelongCity;

                // 【防御】连归属城都推导不出来（主将的 service 是建筑 / 主将缺失）时无处可回，
                // 直接清空任务收尾，避免下面 `TargetCity.Id` 空引用，
                // 也避免 missionType 一直是"返城"却永远准备失败而空转。
                if (TargetCity == null)
                {
                    troop.ClearMission();
                    return;
                }

                troop.SetMission(MissionType.TroopReturnCity, TargetCity.Id);
            }

            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                // 【说明】原先此处有 `if (troop.IsPlayerControl) ClearMission()` 的补丁。
                // 玩家第一军团现已改走 PlayerTroopReturnCity（见 PlayerTroopMissions.cs），
                // 由该类型负责"清空任务交回玩家"，故此处只保留势力 AI 的处理。
                // 全局和平期：不把返城任务改写成"攻打该城"，避免 AI 部队自主发起进攻，
                // 走下面的返城分支回归属城。
                if (TargetCity.IsEnemy(troop) && !scenario.IsPeacePeriod)
                {
                    Troop.SetMission(MissionType.TroopOccupyCity, TargetCity.Id);
                }
                else if (troop.BelongCity != null)
                {
                    // 【修复】目标既不是敌方也不是归属城（例如被改成了其它友城）：
                    // 纠正为"返回归属城"，而不是切到无意义的 TroopStay 并打印错误日志。
                    Troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                }
                troop.NeedPrepareMission();
                return;
            }

            // 【修复】部队已经站在目标城池格上（例如刚出城就切到返城任务）：
            // 此时不做任何通路检查。否则返城路径若经过正在被攻击的敌方建筑，
            // 会把任务改写为"攻击该建筑"，导致部队永远停留在城池格上反复尝试，表现为"卡在城池上"。
            // 直接留空 priorityActionData，由 DoAI 走进城分支。
            if (troop.cell != null && troop.cell.building == TargetCity)
                return;

            // 检查通路
            Troop.tempCellList.Clear();
            scenario.Map.GetDirectPath(Troop.cell, TargetCity.CenterCell, Troop.tempCellList);
            for (int i = 0; i < Troop.tempCellList.Count; ++i)
            {
                Cell road = Troop.tempCellList[i];
                // 【对齐原版 hostile_target + 寻路代价】路径上的中立建筑（陷阱 / 障碍 / 无主设施）
                // 原版不会引发攻击，只有敌对方建筑才值得停下评估。详见 TroopMovetoCell 同处说明。
                // 这条正是"工程队刚把陷阱修好、返城时顺手把它引爆"的触发点：
                // 陷阱建成即中立（Building.cs:484 OnCompleted 清 BelongForce），
                // 旧写法 !IsSameForce 恒成立 → 触发全量 PriorityAction → 选中身边这座陷阱。
                if (road.building != null && !road.building.IsCity()
                    && road.building.BelongForce != null && Troop.IsEnemy(road.building))
                {
                    priorityActionData = TroopAIUtility.PriorityAction(Troop, (Cell)null, scenario, SkillStatusPriority);
                    return;
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

            // 【修复】已在目标城池格上：直接进城解散，优先于任何攻击行动。
            // 原先该判断被放在 TryMoveToCity 分支内部，一旦 priorityActionData 非空
            // 就完全绕过进城逻辑，部队会卡在城池格上无法移动。
            if (troop.cell != null && troop.cell.building == TargetCity)
            {
                troop.EnterCity(TargetCity);
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
            else if (troop.TryMoveToCity(TargetCity))
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
