/*
 * 文件名：TroopAskSupply.cs
 * 描述：部队"求援"行为。状态不佳的己方部队可主动接近友方补给队（运输队），
 *       并在相邻时请求其对自己执行一次补给。
 *
 * 行为约定：
 * 1. **目标必须是运输队（补给队）**，否则任务立即结束；
 * 2. 与目标相邻时，由补给队对求援方执行一次补给（复用 TroopSupplyTroop.SupplyOne）；
 * 3. 未相邻时向补给队安全移动（走安全路径，规避敌方威胁）；
 * 4. 状态恢复到安全线以上、或目标失效 → 任务结束；
 * 5. **恢复原任务**：AIPrepare 在切换求援前会把原任务记录到 missionParams1/2，
 *    求援结束时恢复，避免部队直接返城而丢失进攻 / 防守任务。
 */

namespace Sango.Core
{
    /// <summary>
    /// 部队求援行为：接近友方补给队并请求其对自己补给。
    /// </summary>
    public class TroopAskSupply : TroopMissionBehaviour
    {
        /// <summary>求援目标补给队</summary>
        Troop supplier;

        public override MissionType MissionType { get { return MissionType.TroopAskSupply; } }

        /// <summary>
        /// 任务完成条件：目标无效（不存在 / 阵亡 / 非同势力 / 非运输队），或自身已恢复到安全线。
        /// </summary>
        public override bool IsMissionComplete
        {
            get
            {
                Troop self = Troop;
                if (self == null)
                    return true;
                if (supplier == null || !supplier.IsAlive)
                    return true;
                if (!supplier.IsSameForce(self))
                    return true;
                // 目标必须始终是运输队（补给队）
                if (!supplier.IsTransport)
                    return true;
                // 已恢复到安全线 → 任务结束
                return IsRecovered(self);
            }
        }

        /// <summary>
        /// 判断部队是否已恢复到安全线以上（求援目标达成）：
        /// 粮草不再告急，且兵力回到满编阈值以上。
        /// </summary>
        /// <param name="t">待判断部队</param>
        /// <returns>是否已恢复</returns>
        public static bool IsRecovered(Troop t)
        {
            if (t == null)
                return true;

            AIConfig cfg = AIConfig.Instance;
            // 粮草仍然告急 → 还未恢复
            if (t.IsWithOutFood() == 1)
                return false;
            // 兵力仍低于安全线 → 还未恢复
            if (t.MaxTroops > 0 && t.troops * 100 < t.MaxTroops * cfg.askSupplyHealthPercent)
                return false;

            return true;
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            supplier = scenario.troopsSet.Get(troop.missionTarget);
            priorityActionData = null;
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            // 目标已失效或已恢复 → 结束求援
            if (IsMissionComplete)
            {
                FinishAskSupply(troop);
                return true;
            }

            if (troop.cell == null || supplier.cell == null)
            {
                FinishAskSupply(troop);
                return true;
            }

            // 已相邻 → 由补给队对求援方执行一次补给（即"求援"生效）
            if (troop.cell.Distance(supplier.cell) <= 1)
            {
                bool supplied = TroopSupplyTroop.SupplyOne(supplier, troop);
                Sango.Log.Info(supplied
                    ? $"{troop.mBelongForce?.Name}的[{troop.Name}]向补给队[{supplier.Name}]求援成功!"
                    : $"{troop.mBelongForce?.Name}的[{troop.Name}]向补给队[{supplier.Name}]求援,但补给队暂无可用物资!");
                // 补给后仍未恢复时保留任务，下回合继续跟随补给队；已恢复则收尾
                if (IsMissionComplete)
                    FinishAskSupply(troop);
                return true;
            }

            // 未相邻 → 向补给队安全移动
            return TroopAIUtility.MoveToTargetSafely(troop, supplier.cell, scenario);
        }

        /// <summary>
        /// 求援收尾：恢复被记录的原任务（如继续进攻 / 继续防守）。
        /// 若不恢复，任务被清空后会由通用逻辑自动改为"返回归属城"，导致原任务永久丢失。
        /// </summary>
        /// <param name="troop">求援部队</param>
        static void FinishAskSupply(Troop troop)
        {
            int prevType = troop.missionParams1;
            int prevTarget = troop.missionParams2;

            bool canRestore = AIConfig.Instance.askSupplyRestoreMission
                              && prevType > 0
                              && prevType != (int)MissionType.TroopAskSupply
                              && prevType != (int)MissionType.TroopMovetoCity
                              && prevType != (int)MissionType.TroopReturnCity;

            troop.ClearMission();

            if (canRestore)
                troop.SetMission((MissionType)prevType, prevTarget);
        }
    }
}
