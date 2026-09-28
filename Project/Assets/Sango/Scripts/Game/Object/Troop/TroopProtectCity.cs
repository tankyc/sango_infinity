using static Sango.Core.City;

namespace Sango.Core
{
    public class TroopProtectCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopProtectCity; } }
        Troop nearestEnemy;
        bool isNoEnemyAlive = false;
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

            // 【修复】口径统一：城市是因为"周边存在敌人"才出兵
            //（CityAI.DispatchDefenseTroop 使用 BattleSituation.EvaluateThreat 实时扫描，
            //  该扫描会把"正在攻击城池周边建筑"的敌人也算进去），
            // 但这里原先只依据城池预计算的**围城**敌人判定任务是否完成。
            // 于是敌人改为攻击建筑时，部队刚出城就被误判为"任务完成"→ 切返城 →
            // 表现为城池反复派兵、部队却始终不出城（或卡在城池格上）。
            // 现增加实时扫描兜底，使"出兵判定"与"任务完成判定"口径一致。
            if (isNoEnemyAlive)
            {
                BattleSituation.ThreatSnapshot live = BattleSituation.EvaluateThreat(
                    TargetCity.CenterCell, AIConfig.Instance.cityDefenseScanRange, TargetCity, scenario);
                if (live.HasEnemy)
                    isNoEnemyAlive = false;
            }

            // 【优化】移除"断粮时 30% 概率撤退"的随机判断，统一由 Troop.AIPrepare 处理
            if (IsMissionComplete || (TargetCity.troops < 2000 && TargetCity == Troop.BelongCity))
            {
                if (TargetCity.IsEnemy(troop))
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
                    return troop.TryCloseTo(nearestEnemy.cell);
            }

            return true;
        }
    }
}
