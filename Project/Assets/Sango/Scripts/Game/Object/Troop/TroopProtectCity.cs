using static Sango.Core.City;

namespace Sango.Core
{
    public class TroopProtectCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopProtectCity; } }
        Troop nearestEnemy;
        bool isNoEnemyAlive = false;

        /// <summary>
        /// 驻防待命标记（运行期，不入档）：本回合"目标城暂时安全、驻防未到期"。
        /// 仅用于 <see cref="DoAI"/> 选择"向城郊靠拢待命"而不是返城。
        /// </summary>
        bool isGarrisonWaiting;
        public override bool IsMissionComplete
        {
            get
            {
                return isNoEnemyAlive;
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);

            EnemyInfo enemyInfo;
            isNoEnemyAlive = !TargetCity.CheckEnemiesIfAlive(out enemyInfo);

            // 【口径统一 · 实时兜底】城市是因为"周边存在敌人"才出兵
            //（CityAI.AICanDefense / DispatchDefenseTroop 都用实时扫描，
            //  该扫描会把"正在攻击城池周边建筑"的敌人也算进去），
            // 但这里原先只依据城池预计算的**围城**敌人判定任务是否完成。
            //
            // 快照只在城池自己跑 AIPrepare 时刷新，可能过期：城 A 因"周边有敌人"把在外部队
            // 改派守城，部队 Prepare 读到旧快照（没有敌人）就立刻判"任务完成"→ 返城，
            // 玩家看到的就是"明明身边有敌军、粮草也够，却一群人往回跑"。
            // 因此快照说"没敌人"时再实时扫一次，让"改派判定"与"完成判定"同口径。
            if (isNoEnemyAlive)
            {
                Troop liveEnemy = FindNearestEnemyAround(TargetCity, troop, scenario);
                if (liveEnemy != null)
                    isNoEnemyAlive = false;
            }

            // ---------- 驻防协防（跨军团支援）----------
            // 【原版无对应实现】原版支援部队走 DeployType_Transport + UnitOrderId_Move，
            // 抵达后由 unit_enter 直接并兵（s11_game_ai_kyoten_cmd_mil_support.cpp:189-191），
            // 兵权永久转给目标城，没有"驻守 / 到期返程"。本项目按玩家反馈改为：
            // 不并兵、驻守目标城，连续 N 回合无战事后回所属城
            //（见 Troop.garrisonLastBattleTurn 与 AIConfig.garrisonIdleTurns）。
            isGarrisonWaiting = false;

            // 目标已经换成自己的归属城（被召回 / 改派守本城）：驻防标记失效，
            // 交回下面的"守城 → 无敌情返城"原逻辑，不再带"3 回合后自动返程"的心智。
            if (troop.garrisonLastBattleTurn > 0 && TargetCity == troop.BelongCity)
                troop.garrisonLastBattleTurn = 0;

            if (troop.garrisonLastBattleTurn > 0)
            {
                // 目标城已易主 / 失守：驻防失去意义，直接回所属城，不替别人反攻
                if (TargetCity.BelongForce != troop.BelongForce)
                {
                    Sango.Log.Info($"*[驻防]{troop.Name} 驻防目标{TargetCity.Name}已易主，返回所属城");
                    troop.garrisonLastBattleTurn = 0;
                    if (troop.BelongCity != null)
                        troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                    troop.NeedPrepareMission();
                    return;
                }

                int nowTurn = scenario != null ? scenario.TurnCount : 0;
                if (!isNoEnemyAlive)
                {
                    // 目标城有敌情：刷新"最后交战回合"，驻防计时从头算。
                    // 【边界】TurnCount 在剧本首回合可能为 0，而 0 是本字段的"非驻防"哨兵值，
                    // 直接写入会让驻防语义丢失（退化成普通守城、永不返程），故钳到 1。
                    troop.garrisonLastBattleTurn = nowTurn > 0 ? nowTurn : 1;
                }
                else
                {
                    int idleTurns = nowTurn - troop.garrisonLastBattleTurn;
                    int needTurns = AIConfig.Instance.garrisonIdleTurns;
                    if (needTurns > 0 && idleTurns < needTurns)
                    {
                        // 未到期：留在目标城近郊待命，且**不判任务完成**（否则 DoAI 立刻返城）
                        isNoEnemyAlive = false;
                        isGarrisonWaiting = true;
                        priorityActionData = null;
                        nearestEnemy = null;
                        return;
                    }

                    Sango.Log.Info($"*[驻防]{troop.Name} 驻防{TargetCity.Name}已连续 {idleTurns} 回合无战事，返回所属城");
                    troop.garrisonLastBattleTurn = 0;
                }
            }

            // 【优化】移除"断粮时 30% 概率撤退"的随机判断，统一由 Troop.AIPrepare 处理
            if (IsMissionComplete || (TargetCity.troops < 2000 && TargetCity == Troop.BelongCity))
            {
                // 全局和平期 / 军团被设为禁止攻击：城池虽失守也不反攻，改为返城待命，
                // 避免 AI 部队在和平期或禁攻期内自主发起进攻。
                // 【禁止攻击】此前只判和平期，禁攻军团的守港部队会在港湾失守时
                // 当场把任务改成"夺回港湾"，绕过据点层的委任限制。
                if (TargetCity.IsEnemy(troop) && !scenario.IsPeacePeriod
                    && !TroopAIUtility.IsAttackForbidden(troop))
                {
                    // 如果城池失守,不返回,直接死战,避免过长的寻路导致性能问题
                    Troop.SetMission(MissionType.TroopOccupyCity, TargetCity.Id);
                }
                else
                {
                    Troop.SetMission(MissionType.TroopReturnCity, Troop.BelongCity.Id);
                }
                Troop.NeedPrepareMission();
                return;
            }
            else
            {
                // 【同盟协防口径】结盟不等于"无条件帮盟友守城"：
                // 能投入协防才留下（够得着 + 打得过），否则立即召回最近的自有城。
                // 否则就会出现玩家反馈的"同字标部队长期堵在盟友城下"。
                if (TargetCity.BelongForce != null
                    && TargetCity.BelongForce != troop.BelongForce
                    && TroopAIFallback.IsAiTroop(troop)
                    && !TroopAIFallback.CanSupportDefense(troop, TargetCity, scenario)
                    && TroopAIFallback.RecallToNearestOwnCity(troop, scenario,
                        $"盟友城{TargetCity.Name}协防不划算（距离过远或兵力不足）"))
                {
                    return;
                }

                // 获取目标城市周围的敌人
                priorityActionData = TroopAIUtility.PriorityAction(Troop, TargetCity.CenterCell, scenario, SkillAttackPriority);
                if (priorityActionData == null)
                {
                    nearestEnemy = TargetCity.GetNearestEnemy(troop.cell);

                    // 【修复】城池预计算的敌人未覆盖时（例如敌人正在攻击周边建筑），
                    // 实时扫描兜底，避免"城池在挨打却出城后立刻折返"。
                    if (nearestEnemy == null)
                        nearestEnemy = FindNearestEnemyAround(TargetCity, troop, scenario);

                    if (nearestEnemy == null)
                    {
                        Troop.SetMission(MissionType.TroopReturnCity, Troop.BelongCity.Id);

                        isNoEnemyAlive = true;
                        Troop.NeedPrepareMission();
                    }
                }
            }
        }

        /// <summary>
        /// 在城池周边**实时查找**最近的敌方部队。
        /// 用于兜底城池预计算未覆盖的场景（例如敌人正在攻击城池周边建筑）。
        /// </summary>
        /// <param name="city">目标城池</param>
        /// <param name="self">自身部队（敌我判定基准）</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>最近的敌方部队；范围内没有则返回 null</returns>
        static Troop FindNearestEnemyAround(City city, Troop self, Scenario scenario)
        {
            if (city == null || city.CenterCell == null || self == null || scenario == null || scenario.Map == null)
                return null;

            Troop nearest = null;
            int bestDistance = int.MaxValue;

            scenario.Map.SpiralAction(city.CenterCell, AIConfig.Instance.cityDefenseScanRange, (cell) =>
            {
                Troop enemy = cell.troop;
                if (enemy == null || !enemy.IsAlive || !self.IsEnemy(enemy))
                    return;

                int distance = city.CenterCell.Distance(cell);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = enemy;
                }
            });

            return nearest;
        }

        /// <summary>
        /// 技能攻击评分：距离目标城池越近的敌人越优先（视为正在威胁城池）。
        /// 加成幅度全部取自 <see cref="AIConfig"/>（不再硬编码 500 / 1000 / 100000 等）。
        /// </summary>
        public int SkillAttackPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            int score = TroopAIUtility.SkillStatusPriority(troop, skill, target, movetoCell, spellCell);
            if (score <= 0)
                return score;

            // 【严格对齐原版】Open311 评分体系下不叠加任务特有加成
            // （"越靠近城池越优先"的线性加成原版没有）。
            if (AIConfig.Instance.useOpen311ScoreFormula)
                return score;

            AIConfig cfg = AIConfig.Instance;
            bool isStay = movetoCell == troop.cell;
            bool isPrimary = false;

            if (!target.IsEmpty() && target.troop != null && TargetCity != null && TargetCity.CenterCell != null)
            {
                int distance = Scenario.Cur.Map.Distance(TargetCity.CenterCell, target.troop.cell);
                // 越靠近城池，越视为"主目标"，并按接近程度线性加成
                int nearCells = cfg.protectCityThreatRange - distance;
                if (nearCells > 0)
                {
                    isPrimary = true;
                    score = score + score * cfg.protectCityNearEnemyBonusPerCell * nearCells / 100;
                }
            }

            bool isMeleeClose = isStay && !troop.TroopType.isRange;
            return TroopAIUtility.ApplyTaskBonus(score, isPrimary, isStay, isMeleeClose, troop.GetRoleWeights());
        }


        public override bool DoAI(Troop troop, Scenario scenario)
        {

            // 【驻防待命】目标城暂时安全、驻防未到期：向城郊靠拢后待命。
            // 不进城、不并兵 —— 部队不允许停留在城池格上，靠到 1 格外即可。
            if (isGarrisonWaiting)
            {
                // 【禁止在这里判 isMoving 就 return false】
                // isMoving 的清零点只有 Troop.MoveTo / TryMoveToCell 内部（移动事件完成时顺手清，
                // 见 Troop.cs:2123-2128 / 2651-2655），**没有任何后台计时器会改它**。
                // 一旦这里直接返回 false，就再也没有调用能把它改回 false；
                // 而 DoAI 返回 false 会让 AI 原地重试同一支部队 → 整条 AI 命令链卡死。
                // 原实现正是靠 TryCloseTo 每回合被反复调用，才把移动状态推进并收尾的。
                //
                // 也不会重复起动画：TryMoveToCell 在 isMoving 为 true 时跳过寻路，
                // 只调 MoveTo(tryToDest)，而 MoveTo 在事件未完成时直接 return false（等待）。
                if (troop.cell != null && TargetCity != null && TargetCity.CenterCell != null
                    && Scenario.Cur.Map.Distance(troop.cell, TargetCity.CenterCell) > 1)
                    return troop.TryCloseTo(TargetCity.CenterCell);

                // 【状态收尾】已到城郊：借 TryMoveToCell(当前格) 命中"目标 == 所在格"的早退分支，
                // 只清 moveRenderEvent / isMoving，不寻路、不起动画。
                // 不清的话本回合内 isMoving 会一直为 true（要等 OnForceTurnStart 才重置），
                // 期间援助 / 补给 / 再次寻路等所有"是否正在移动"判定都会误判。
                if (troop.isMoving)
                    troop.TryMoveToCell(troop.cell);
                return true;
            }

            // 任务完成后,回到创建城池
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
                return true;
            }

            // 获取目标城市周围的敌人
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
                if (nearestEnemy != null)
                {
                    // 【移动受阻兜底】出击路线被敌方 / 无主港关挡住时改派攻占：
                    // 移动范围会在港关格处停止扩展，部队会永远停在它前一格。
                    // 详见 TroopAIUtility.TryAttackBlockingCityOnPath 的说明。
                    if (TroopAIUtility.TryAttackBlockingCityOnPath(troop, nearestEnemy.cell, scenario))
                        return true;

                    return troop.TryCloseTo(nearestEnemy.cell);
                }
            }

            return true;
        }
    }
}
