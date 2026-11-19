namespace Sango.Core
{
    /// <summary>
    /// 支援己方据点：把兵力送进目标城（抵达后 <c>EnterCity</c> 并兵），
    /// **途中主动迎击拦路的敌军**。
    ///
    /// 【为什么需要独立任务】
    /// 原实现用 <see cref="TroopMovetoCity"/> 兼任"支援"。而 Move 的
    /// <c>Prepare</c> 只在**路径上有敌方建筑**时才评估出手
    /// （TroopMovetoCity.cs:34-48），完全不管敌军部队：
    /// 支援队在途中被敌军堵住时，<c>TryMoveToCity</c> 每回合都推进不了，
    /// 只能原地重试 —— 玩家看到的就是"被人断路就一直逛街"。
    ///
    /// 本任务每回合用 <c>PriorityAction</c> 评估一次出手机会：
    /// 能打就打，打不到就继续推进；连推进都不行时改为歼灭就近敌军（断路兜底）。
    ///
    /// 【原版对应】原版支援是 DeployType_Transport + UnitOrderId_Move
    /// （s11_game_ai_kyoten_cmd_mil_support.cpp:189-191），原版 Move 的 run_order
    /// 自带战斗流程；本项目 Move 的直译（<see cref="TroopMovetoCity"/>）没有这一层，
    /// 故拆出本任务补齐。并兵语义保持不变（对齐 unit_enter）。
    /// </summary>
    public class TroopSupportCity : TroopMissionBehaviour
    {
        /// <summary>任务枚举</summary>
        public override MissionType MissionType { get { return MissionType.TroopSupportCity; } }

        /// <summary>
        /// 目标城不再属于本方势力（失守 / 被夺 / 消失）时任务结束。
        /// </summary>
        public override bool IsMissionComplete
        {
            get
            {
                if (TargetCity == null)
                    return true;
                return !TargetCity.IsSameForce(Troop);
            }
        }

        /// <summary>
        /// 行动前准备：解析目标城 → 判定是否结束 → 评估本回合的出手机会。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="scenario">场景对象</param>
        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget)
                TargetCity = scenario.citySet.Get(Troop.missionTarget);

            priorityActionData = null;

            // 目标城没了 / 易主：兵带回去，不白送
            if (IsMissionComplete)
            {
                if (troop.BelongCity != null)
                    troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                troop.NeedPrepareMission();
                return;
            }

            // 【返程条件】断粮 / 兵力过低：把残部带回所属城，不硬送。
            // 口径对齐 unit_update_order 的撤退判定（s11_game_ai_unit.cpp:132-163）：
            // food_shortage 或 兵力 ≤ 500。原版对 Move 类部队同样适用，本项目 Move 的直译
            // 只在 Attack 等分支做撤退，支援队需要单独补这一层。
            if (troop.BelongCity != null
                && (troop.food < troop.PrepeareFoodCost() || troop.troops <= 500))
            {
                Sango.Log.Info($"{troop.Name} 支援{TargetCity.Name}途中粮草不足或兵力过低，返回所属城");
                troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                troop.NeedPrepareMission();
                return;
            }

            // 已在目标城格上：交给 DoAI 的进城分支，不做攻击评估
            if (troop.cell != null && troop.cell.building == TargetCity)
                return;

            // 【核心】每回合评估一次出手机会 —— 这是它与 TroopMovetoCity 的实质差别。
            // 评分用基础战况分（不带任务加成：支援优先赶路，只有"打了明显划算"才停下），
            // 因此不会为了追远处的敌人偏离支援路线。
            priorityActionData = TroopAIUtility.PriorityAction(
                Troop, TargetCity.CenterCell, scenario, TroopAIUtility.SkillStatusPriority);
        }

        /// <summary>
        /// 执行：进城并兵 / 出手 / 推进 / 断路兜底。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>false 表示本帧动画未播完，需要继续等待</returns>
        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
                return true;
            }

            // 抵达：进城并兵（对齐原版 unit_enter：支援队抵达己方据点即合流）
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

            // 无出手机会 → 向目标城推进。
            //
            // 【返回值语义】TryMoveToCity **不能**用返回值判断"有没有路"：
            // Troop.TryMoveToCell 在"移动范围内找不到任何通往目标的落脚格"时
            // 是 `tryToDest == null → return true`（Troop.cs:2658-2661），
            // 只有"移动动画还没播完"才返回 false。旧 TroopMovetoCity 把 true 当成功，
            // 于是完全断路时每回合什么都不做 —— 这就是"被人断路就一直逛街"的机制。
            // 因此这里改判「本回合有没有真的挪窝」：坐标没变且没有动画在跑 = 断路。
            // 【移动受阻兜底】支援路线被敌方 / 无主港关挡住时改派攻占：
            // 移动范围（Map.GetMoveRange）会在港关格处停止向外扩展，
            // 而寻路只看地形仍给出穿过它的路径 —— 部队会永远停在港关前一格。
            // 详见 TroopAIUtility.TryAttackBlockingCityOnPath 的说明。
            if (TroopAIUtility.TryAttackBlockingCityOnPath(troop, TargetCity.CenterCell, scenario))
                return true;

            Cell before = troop.cell;
            if (!troop.TryMoveToCity(TargetCity))
                return false;   // 移动动画未播完，下帧继续

            if (troop.cell != null && troop.cell.building == TargetCity)
            {
                troop.EnterCity(TargetCity);
                return true;
            }

            if (!troop.isMoving && troop.cell == before)
                return TryBreakThrough(troop);

            return true;
        }

        /// <summary>
        /// 断路兜底：本回合一步都推不动时，就近找拦路敌军开打；连敌人都没有就退回所属城，
        /// 避免在原版 Move 的"原地重试"里永久卡死。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <returns>始终返回 true（任务已改派或已给出动作）</returns>
        bool TryBreakThrough(Troop troop)
        {
            Troop blocker = troop.cell != null ? TargetCity.GetNearestEnemy(troop.cell) : null;
            if (blocker != null && blocker.IsAlive && troop.IsEnemy(blocker))
            {
                Sango.Log.Info($"{troop.Name} 支援{TargetCity.Name}途中被{blocker.Name}阻断，转为歼灭");
                troop.SetMission(MissionType.TroopDestroyTroop, blocker.Id);
                // 意图层同步为"进攻"，避免部队层按 Defense 意图继续做据点改判
                troop.deployType = (int)TroopDeployType.Attack;
                troop.deployTarget = -1;
                troop.NeedPrepareMission();
                return true;
            }

            Sango.Log.Warning($"{troop.Name} 支援{TargetCity.Name}无路可走且附近无敌军，返回所属城");
            if (troop.BelongCity != null)
                troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
            troop.NeedPrepareMission();
            return true;
        }
    }
}
