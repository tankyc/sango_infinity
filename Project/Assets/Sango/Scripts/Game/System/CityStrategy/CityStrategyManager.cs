namespace Sango.Core
{
    /// <summary>
    /// 城市计略管理器。
    /// 职责：作为二虎竞食、流言等城市计略的统一入口，负责创建计略行为、判重、扣除消耗、
    /// 把使者挂成 PersonCityStrategy 任务，以及在使者抵达后还原本次计略并结算效果。
    /// 与外交系统的分工：势力之间的关系读写仍然归 DiplomacyManager，本系统只决定"该扣多少"；
    /// 计略的成功与否在抵达时刻才判定（使者可能被半路截杀、目标可能中途易主），不在派遣时判定。
    /// 驱虎吞狼本期不实现，工厂分支只报错误日志，不产出兜底行为。
    /// </summary>
    [GameSystem]
    public class CityStrategyManager : GameSystem
    {
        /// <summary>
        /// 创建计略行为实例
        /// </summary>
        /// <param name="strategyType">计略类型</param>
        /// <param name="sender">发起方势力</param>
        /// <param name="fromCity">出发据点</param>
        /// <param name="diplomat">使者武将</param>
        /// <param name="targetCity">使者目的地据点</param>
        /// <param name="targetForceA">目标势力A（流言为被施法据点的所属势力）</param>
        /// <param name="targetForceB">目标势力B（仅二虎竞食使用，其余传 null）</param>
        /// <returns>计略行为；类型未实现或参数不足时返回 null</returns>
        public CityStrategyActionBase CreateAction(CityStrategyType strategyType, Force sender, City fromCity, Person diplomat, City targetCity, Force targetForceA, Force targetForceB = null)
        {
            switch (strategyType)
            {
                case CityStrategyType.TwoTigers:
                    return new CityStrategyActionTwoTigers(sender, fromCity, diplomat, targetCity, targetForceA, targetForceB);
                case CityStrategyType.Rumor:
                    return new CityStrategyActionRumor(sender, fromCity, diplomat, targetCity, targetForceA);
                default:
                    // 驱虎吞狼等未实现类型：不编造可执行行为，交由调用方按派遣失败处理
                    Sango.Log.Error($"@计略@计略类型 {strategyType} 本期未实现，无法创建对应行为");
                    return null;
            }
        }

        /// <summary>
        /// 检查同一份计略是否已有使者在途，避免玩家对同一目标反复叠加。
        /// 判定依据是"计略类型 + 存档任务参数"完全相同：二虎竞食看势力对（已按 Id 排序，与选择顺序无关），
        /// 流言额外看目标据点 Id，因为同一势力下的多个据点应视为不同目标。
        /// 这里不复用 DiplomacyManager 的在途判断，因为计略与外交的任务类型和参数排布完全不同。
        /// </summary>
        /// <param name="action">待派遣的计略行为</param>
        /// <returns>已有同参数计略在途返回 true</returns>
        public bool IsCityStrategyInProgress(CityStrategyActionBase action)
        {
            if (action == null)
                return false;

            int strategyType = (int)action.StrategyType;
            int param3 = action.MissionParam3;
            int param4 = action.MissionParam4;
            int targetCityId = action.TargetCity != null ? action.TargetCity.Id : 0;

            bool inProgress = false;
            Scenario.Cur.personSet.ForEach((System.Action<Person>)(person =>
            {
                if (inProgress)
                    return;
                if (person.missionType != (int)MissionType.PersonCityStrategy)
                    return;
                if (person.missionParams2 != strategyType)
                    return;
                if (person.missionParams3 != param3 || person.missionParams4 != param4)
                    return;
                if (action.StrategyType == CityStrategyType.Rumor && person.missionTarget != targetCityId)
                    return;
                inProgress = true;
            }));
            return inProgress;
        }

        /// <summary>
        /// 派遣使者执行计略：校验条件与消耗、扣钱扣行动力、挂上在途任务并移除空闲名单。
        /// 消耗在派遣时即扣除（视为使者出行的花费），与抵达后才扣钱的外交送礼不同；
        /// 计略没有窗口提交入口，也没有像外交那样由上层统一扣行动力，所以必须在本方法内自扣。
        /// </summary>
        /// <param name="action">待派遣的计略行为，Diplomat/FromCity/TargetCity 必须已经齐备</param>
        /// <returns>派遣成功返回 true；条件不满足、重复派遣或消耗不足返回 false 且不产生任何扣减</returns>
        public bool Dispatch(CityStrategyActionBase action)
        {
            if (action == null || action.Diplomat == null)
                return false;
            // SetMission 会直接解引用 missionTarget，出发城与目的地任一为空都不能派遣
            if (action.FromCity == null || action.TargetCity == null)
                return false;
            if (!action.CanPerform())
                return false;
            if (IsCityStrategyInProgress(action))
            {
#if SANGO_DEBUG
                Sango.Log.Info($"@计略@{action.GetActionName()}已有使者在途，本次派遣取消");
#endif
                return false;
            }

            int jobId = (int)action.JobType;
            int goldCost = JobType.GetJobCost(jobId);
            int apCost = JobType.GetJobCostAP(jobId);
            Corps corps = action.FromCity.mBelongCorps;
            if (action.FromCity.gold < goldCost)
                return false;
            if (corps != null && corps.ActionPoint < apCost)
                return false;

            action.FromCity.gold -= goldCost;
            // ReduceActionPoint 内部只对玩家势力生效，AI 势力不受行动力约束
            if (corps != null)
                corps.ReduceActionPoint(apCost);

            // 出使耗时沿用外交的算法：按两城之间的最短路径天数，同城或不可达时按 1 天保底
            int distance = action.Diplomat.DistanceDays(action.TargetCity);
            if (distance <= 0)
                distance = 1;

            // 参数约定（与 Person.UpdateMission 的 PersonCityStrategy 分支一一对应）：
            // missionTarget = 目的地据点 Id；p1 = 发起势力 Id；p2 = CityStrategyType；
            // p3/p4 = 二虎竞食按 Id 排序后的目标势力对；流言只用 p3（目标势力 Id），p4 恒为 0
            action.Diplomat.SetMission(MissionType.PersonCityStrategy, action.TargetCity, distance,
                action.Sender.Id, (int)action.StrategyType, action.MissionParam3, action.MissionParam4);

            // 使者已经出城，不能再作为本城的空闲武将参与其它工作
            action.FromCity.freePersons.Remove(action.Diplomat);

            action.OnDispatch();
            return true;
        }

        /// <summary>
        /// 使者抵达目的地后结算计略：按存档任务参数还原本次计略，掷一次骰得出四态结果，再执行对应效果。
        /// 成功率在抵达时刻重新计算，因此使者途中敌方军师变动、据点内武将变动都会影响最终判定。
        /// 本方法由 Person.UpdateMission 的 PersonCityStrategy 分支调用，处于回合结算循环内，
        /// 只做逻辑与调试日志，不弹对话框（弹窗会暂停整局推进）。
        /// </summary>
        /// <param name="envoy">抵达的使者武将</param>
        /// <param name="strategyType">计略类型</param>
        /// <param name="targetCity">目的地据点</param>
        /// <param name="sender">发起方势力</param>
        /// <param name="param3">任务参数3，见 Dispatch 中的参数约定</param>
        /// <param name="param4">任务参数4，见 Dispatch 中的参数约定</param>
        /// <returns>本次计略的四态结果；上下文不足无法结算时返回 Failed</returns>
        public CityStrategyResult ExecuteMission(Person envoy, CityStrategyType strategyType, City targetCity, Force sender, int param3, int param4)
        {
            if (envoy == null || targetCity == null || sender == null)
                return CityStrategyResult.Failed;

            Scenario scenario = Scenario.Cur;
            Force targetForceA = scenario.forceSet.Get(param3);
            Force targetForceB = strategyType == CityStrategyType.TwoTigers ? scenario.forceSet.Get(param4) : null;

            // DoMove 只改 mCurrentCity，mBelongCity 仍是出发城，故出发城与返程目标都直接取使者字段。
            // 被捕分支例外：City.AddCaptive 会把 mBelongCity 置空，所以调用方必须在使者被收押后跳过返程任务
            City fromCity = envoy.mBelongCity;
            CityStrategyActionBase action = CreateAction(strategyType, sender, fromCity, envoy, targetCity, targetForceA, targetForceB);
            if (action == null)
                return CityStrategyResult.Failed;

            // 抵达后不再复查 CanPerform：使者已经站在这里，目标中途生变也要给出确定结果
            int rate = action.CalculateSuccessRate();
            CityStrategyResult result = action.RollResult(rate);
            action.Perform(result);

            // 掷出"失败被擒"但收押没成功（君主不可俘虏、目标城中途失陷等）时降级为普通失败，
            // 让上层照常放使者返程，避免出现"既没坐牢也没回家"的黑户武将
            if (result == CityStrategyResult.FailedCaptured && !action.EnvoyCaptured)
                result = CityStrategyResult.Failed;

#if SANGO_DEBUG
            Sango.Log.Info($"@计略@{sender.Name} 的 {envoy.Name} 抵达 {targetCity.Name}，{action.GetActionName()}成功率 {rate}（抵抗者 {action.Resister?.Name ?? "无"}），结果：{GetResultName(result)}");
#endif
            return result;
        }

        /// <summary>
        /// 四态结果的中文名，仅用于调试日志，不参与任何判定。
        /// </summary>
        /// <param name="result">计略结果</param>
        /// <returns>中文描述</returns>
        private static string GetResultName(CityStrategyResult result)
        {
            switch (result)
            {
                case CityStrategyResult.SucceededUndetected:
                    return "成功未发现";
                case CityStrategyResult.SucceededDetected:
                    return "成功但被发现";
                case CityStrategyResult.FailedCaptured:
                    return "失败被捕";
                default:
                    return "失败";
            }
        }
    }
}
