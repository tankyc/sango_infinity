/*
 * 文件名：TroopSupplyTroop.cs
 * 描述：部队补给行为（战场级）。AI 补给队会自动寻找需要补给的友军部队并安全靠近，
 *       每回合只补给一支部队（遵守"一回合一次"约定），长期轮流服务整条战线。
 */

namespace Sango.Core
{
    /// <summary>
    /// 部队补给行为（战场级）。
    ///
    /// 行为约定：
    /// 1. **一回合只补给一支部队**：优先选择相邻部队中"最需要补给"的一支（缺粮优先，其次兵力缺口）；
    /// 2. 不与友军相邻时，向最需要补给的友军安全移动（规避敌方威胁区域）；
    /// 3. 移动 / 站位优先选择友军背向敌人一侧，借助友军作为屏障规避被攻击；
    /// 4. 每支补给队携带战场级物资，可连续多回合**依次**补给多支部队（一回合一支，而非一次全补）；
    /// 5. **资源维持**：兵装 / 兵力 / 粮草任一低于阈值时，返回最近己方据点补充；
    /// 6. **保持距离**：与最近敌方部队过近（不足约一个回合的行程）时，向己方据点后撤；
    /// 7. **战场态势**：我方战力占比低于阈值（大劣）时立刻撤退；
    /// 8. 物资耗尽或没有可补给对象时，前往最近的己方据点。
    /// </summary>
    public class TroopSupplyTroop : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopSupplyTroop; } }

        /// <summary>
        /// 补给队是否需要撤回据点。
        /// 兵装 / 兵力 / 粮草任一不足（或完全耗尽）时返回 true。
        /// </summary>
        public override bool IsMissionComplete
        {
            get
            {
                Troop t = Troop;
                if (t == null)
                    return true;

                // 完全耗尽
                if (t.food <= 0 || t.troops <= 0)
                    return true;

                // 【需求2】兵装 / 士兵 / 粮食任一不足即返回
                AIConfig cfg = AIConfig.Instance;
                if (cfg.supplyReturnTroops > 0 && t.troops < cfg.supplyReturnTroops)
                    return true;
                if (cfg.supplyReturnFood > 0 && t.food < cfg.supplyReturnFood)
                    return true;

                // 【修复】补给队的兵装是"要给前线补的"，运输兵种自身并不消耗兵装。
                // 原先用"少于 100 件"判定，导致随队兵装本就只有几十件时，
                // 刚出城就被判定为资源不足而返城（因它就在本城内，返城即进城解散）。
                // 改为"携带量已清空"才回去补货。
                if (cfg.supplyReturnItems > 0
                    && (t.itemStore == null || t.itemStore.TotalNumber <= 0))
                    return true;

                return false;
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            priorityActionData = null;
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            // 【需求5】我方大劣 → 拔腿就跑
            if (IsForceLosing(troop, scenario))
            {
                ReturnToNearestCity(troop, scenario);
                return true;
            }

            // 【需求2】资源任一不足 → 返回据点补充
            if (IsMissionComplete)
            {
                ReturnToNearestCity(troop, scenario);
                return true;
            }

            // 【需求2】与最近敌人过近（不足约一个回合的行程）→ 向己方据点后撤
            if (IsTooCloseToEnemy(troop, scenario, out City backCity))
                return TroopAIUtility.MoveToTargetSafely(troop, backCity.CenterCell, scenario);

            // 【出城优先】部队不允许停留在城池 / 关隘 / 港口等建筑格上。
            //
            // 补给队刚组建（或被返城 / 后撤逻辑带回）时会临时停在城头。
            // 若此时就地给城边友军补给，SupplyOneAdjacentAlly 会 return true 直接结束行动，
            // 而前线友军几乎永远处于"需要补给"状态（持续减员 / 缺粮），
            // 于是补给队一回合接一回合地钉在城头补兵，永远不出城 —— 表现为"补给队滞留在城头"。
            //
            // 因此停在建筑格上时，本回合只做一件事：离开城头。
            // 该判断放在"该解散 / 该返城"三个分支之后，保证物资耗尽等收尾逻辑优先生效。
            if (IsOnBuildingCell(troop))
            {
                if (!TryLeaveBuildingCell(troop, scenario))
                    return false;               // 移动动画未播完，交给下一帧继续

                return true;
            }

            // 1) 与友军相邻 → 本回合只补给其中"最需要"的一支（一回合一支）
            if (SupplyOneAdjacentAlly(troop))
                return true;

            // 2) 否则向最需要补给的友军（其背向敌人一侧）安全移动
            Troop ally = FindSupplyTarget(troop, scenario);
            if (ally == null)
            {
                // 附近已无待补给友军 → 返城收尾。
                // 【游戏规则】部队不允许停留在城市格上，因此不能"原地待命"；
                // 若此时已在城内，ReturnToNearestCity 会直接让它进城解散。
                ReturnToNearestCity(troop, scenario);
                return true;
            }

            Cell dest = FindSafeSupplyCell(troop, ally, scenario);
            if (dest == null)
                dest = ally.cell;

            return TroopAIUtility.MoveToTargetSafely(troop, dest, scenario);
        }

        /// <summary>
        /// 【需求5】评估**补给队附近战场**的态势：附近我方与敌方兵力对比，
        /// 我方占比低于阈值时视为"大劣"。
        /// </summary>
        /// <param name="supplier">补给队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处于大劣态势</returns>
        static bool IsForceLosing(Troop supplier, Scenario scenario)
        {
            return IsForceLosingAt(supplier.cell, supplier.BelongForce, scenario);
        }

        /// <summary>
        /// 判断指定势力在给定位置周围是否处于"大劣"（我方兵力占比低于阈值）。
        ///
        /// 只统计**该位置附近**的部队，反映局部战场态势，而非全势力总兵力。
        /// 【修复】原先统计全局兵力，导致开局我方总兵力少于敌方（很常见）时，
        /// 补给队一组建就被判定为"大劣"，进而不停地组建 → 立即解散，白白消耗资源。
        /// </summary>
        /// <param name="center">评估中心（补给队所在格 / 城市中心格）</param>
        /// <param name="force">我方势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处于大劣态势</returns>
        public static bool IsForceLosingAt(Cell center, Force force, Scenario scenario)
        {
            int percent = AIConfig.Instance.supplyRetreatForcePercent;
            if (percent <= 0 || center == null || force == null || scenario == null)
                return false;

            int range = AIConfig.Instance.supplySearchRange;

            long myPower = 0;
            long enemyPower = 0;
            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop t = scenario.troopsSet[i];
                if (t == null || !t.IsAlive || t.cell == null)
                    continue;

                // 只统计附近战场上的部队，忽略远处无关战线的兵力对比
                if (scenario.Map.Distance(center, t.cell) > range)
                    continue;

                if (t.BelongForce == force)
                    myPower += t.troops;
                else if (force.IsEnemy(t.BelongForce))
                    enemyPower += t.troops;
            }

            if (enemyPower <= 0)
                return false;

            // 我方占比 = my / (my + enemy)
            return myPower * 100 < (myPower + enemyPower) * percent;
        }

        /// <summary>
        /// 【需求2】判断补给队是否过于靠近敌人（不足约一个回合的行程）。
        /// 若是，则输出一个"向己方据点后撤"的目标城市。
        /// </summary>
        /// <param name="supplier">补给队</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="backCity">后撤目标据点</param>
        /// <returns>是否需要后撤</returns>
        static bool IsTooCloseToEnemy(Troop supplier, Scenario scenario, out City backCity)
        {
            backCity = null;

            int safeDistance = AIConfig.Instance.supplySafeDistance;
            if (safeDistance <= 0)
                return false;

            Troop nearestEnemy = FindNearestEnemyTroop(supplier, scenario);
            if (nearestEnemy == null || nearestEnemy.cell == null)
                return false;

            if (scenario.Map.Distance(supplier.cell, nearestEnemy.cell) >= safeDistance)
                return false;

            backCity = supplier.FindNearestFriendlyCity(scenario);
            if (backCity == null || backCity.CenterCell == null)
            {
                backCity = null;
                return false;
            }

            // 【修复】补给队刚组建时就在据点中心格上。此时若仍以"最近据点中心"为后撤目标，
            // 目标恰好是它自己所在的格子，会导致它每回合原地打转、永远出不了城。
            // 已在据点内 / 紧贴据点时视为处于安全位置，不再触发后撤。
            if (scenario.Map.Distance(supplier.cell, backCity.CenterCell) <= 1)
            {
                backCity = null;
                return false;
            }

            return true;
        }

        /// <summary>待补给友军查询缓存（避免每次调用分配 List）</summary>
        static readonly System.Collections.Generic.List<Troop> needyAllyCache =
            new System.Collections.Generic.List<Troop>(32);

        /// <summary>
        /// 物资不足或无可补给对象时，前往最近的己方城市 / 港口 / 关卡。
        /// </summary>
        /// <param name="troop">补给队</param>
        /// <param name="scenario">场景对象</param>
        void ReturnToNearestCity(Troop troop, Scenario scenario)
        {
            City back = troop.FindNearestFriendlyCity(scenario);
            if (back == null)
                back = troop.BelongCity;
            if (back == null)
                return;

            // 【游戏规则】部队不允许停留在城市格上。
            // 若补给队当前就站在据点格上（刚组建或已返抵），直接进城解散；
            // 不能"原地待命"——那会让部队停在城市格上，违反规则。
            if (troop.cell != null && troop.cell.building == back)
            {
                troop.EnterCity(back);
                return;
            }

            troop.SetMission(MissionType.TroopMovetoCity, back.Id);
            troop.NeedPrepareMission();
        }

        /// <summary>
        /// 【出城优先】判断补给队是否正停在城池 / 关隘 / 港口等建筑格（城头）上。
        /// </summary>
        /// <param name="troop">补给队</param>
        /// <returns>是否停在建筑格上</returns>
        static bool IsOnBuildingCell(Troop troop)
        {
            return troop != null && troop.cell != null && troop.cell.building != null;
        }

        /// <summary>
        /// 【出城优先】把停在城头的补给队送到移动范围内最近的、可停留的空地。
        ///
        /// 落脚点由 <see cref="Cell.CanStay"/> 把关（无部队、无建筑、地形可通行），
        /// 确保离开城头后一定站在合法地块上，不会再出现"停在城头"的情况。
        /// </summary>
        /// <param name="troop">补给队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>
        /// true  —— 已经站在可停留地块上，或本回合移动范围内确实没有落脚点；
        /// false —— 正在移动途中，调用方需 return false 等待下一帧。
        /// </returns>
        static bool TryLeaveBuildingCell(Troop troop, Scenario scenario)
        {
            if (troop.cell == null || troop.cell.building == null)
                return true;

            Map map = scenario.Map;
            // 与其它移动逻辑保持同一口径：移动范围只在为空时计算一次
            if (troop.MoveRange.Count == 0)
                map.GetMoveRange(troop, troop.MoveRange);

            Cell best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < troop.MoveRange.Count; i++)
            {
                Cell candidate = troop.MoveRange[i];
                // 跳过自己所在格（建筑格不可停留）与不可停留的地块
                if (candidate == null || candidate == troop.cell)
                    continue;
                if (!candidate.CanStay(troop))
                    continue;

                // 选最近的一格：出城这一步尽量少消耗移动力
                int distance = map.Distance(troop.cell, candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            // 被敌军 / 建筑彻底围死：本回合无处可去，保持临时状态，下次行动再试
            if (best == null)
                return true;

            return troop.MoveTo(best);
        }

        /// <summary>
        /// 本回合为相邻的、最需要补给的一支友军补充物资。
        /// 严格遵守"一回合只补给一支部队"的约定；补给完成后本部队行动结束。
        /// </summary>
        /// <param name="supplier">补给队</param>
        /// <returns>是否执行了补给</returns>
        bool SupplyOneAdjacentAlly(Troop supplier)
        {
            Cell[] neighbors = supplier.cell.Neighbors;
            if (neighbors == null)
                return false;

            Troop bestAlly = null;
            int bestNeed = 0;

            for (int i = 0; i < neighbors.Length; i++)
            {
                Cell cell = neighbors[i];
                if (cell == null)
                    continue;
                Troop ally = cell.troop;
                if (ally == null || !ally.IsAlive || ally == supplier)
                    continue;
                if (!ally.IsSameForce(supplier))
                    continue;
                // 补给 / 运输队之间不互相补给
                if (ally.IsTransport)
                    continue;
                // 【一致性】与 CollectNeedyTroops 保持一致：不补给"无任务的城内驻守部队"，
                // 否则补给队刚出城就会赖在城里给守军补兵，迟迟不向前线推进。
                if (ally.missionType <= 0)
                    continue;

                int need = CalcSupplyNeed(ally);
                if (need > bestNeed)
                {
                    bestNeed = need;
                    bestAlly = ally;
                }
            }

            if (bestAlly == null)
                return false;

            return SupplyOne(supplier, bestAlly);
        }

        /// <summary>
        /// 计算一支部队的补给需求度（0 表示不需要补给）。
        /// 缺粮优先，其次兵力缺口。
        /// </summary>
        /// <param name="ally">待评估部队</param>
        /// <returns>需求度，越大越需要补给</returns>
        public static int CalcSupplyNeed(Troop ally)
        {
            if (ally == null || !ally.IsAlive)
                return 0;

            int need = 0;
            if (ally.food <= 0)
                need += 10000;
            else if (ally.food < ally.troops * AIConfig.Instance.supplyFoodThresholdFactor)
                need += 5000;

            if (ally.MaxTroops > 0 && ally.troops < ally.MaxTroops)
                need += (ally.MaxTroops - ally.troops) / 100;

            // 【修复】兵装缺口必须与 CollectNeedyTroops 的口径一致。
            // 否则"仅兵装不足"的友军会被 FindSupplyTarget 全部跳过，导致补给队找不到目标
            // 而直接返城（因它就在本城内，返城即进城解散，表现为"刚出城就消失"）。
            if (CityAI.IsItemShortage(ally))
                need += 3000;

            return need;
        }

        /// <summary>
        /// 判断一支部队是否需要补给（供求援方判断是否值得发起求援）。
        /// </summary>
        /// <param name="ally">待判断部队</param>
        /// <returns>是否需要补给</returns>
        public static bool NeedSupply(Troop ally)
        {
            return CalcSupplyNeed(ally) > 0;
        }

        /// <summary>
        /// 向单支友军补给：粮草补齐到「兵力 × 阈值 × 2」，兵力补到满编，兵装按需移交。
        /// 供补给队的自动行为与友军的「求援」指令共用。
        /// </summary>
        /// <param name="supplier">补给队</param>
        /// <param name="ally">接收补给的友军</param>
        /// <returns>是否发生了补给</returns>
        public static bool SupplyOne(Troop supplier, Troop ally)
        {
            if (supplier == null || ally == null || !supplier.IsAlive || !ally.IsAlive)
                return false;

            // ---- 粮草：补齐到「兵力 × 阈值 × 2」为止 ----
            int foodTarget = ally.troops * AIConfig.Instance.supplyFoodThresholdFactor * 2;
            int foodGive = foodTarget - ally.food;
            if (foodGive < 0)
                foodGive = 0;
            // 保留补给队自身最低行军口粮（兵力 × 2）
            int foodKeep = supplier.troops * 2;
            if (supplier.food - foodGive < foodKeep)
                foodGive = supplier.food - foodKeep;
            if (foodGive < 0)
                foodGive = 0;

            // ---- 兵力：补到友军满编为止 ----
            int troopGive = 0;
            if (ally.MaxTroops > 0 && ally.troops < ally.MaxTroops)
                troopGive = ally.MaxTroops - ally.troops;
            if (troopGive > supplier.troops)
                troopGive = supplier.troops;
            // 【修复】补给队必须给自己保留最低 1 兵，不允许把兵力全部交出。
            // 兵力归零的补给队不会被 ChangeTroops 正常收尾（SupplyTroop 直接改 troops），
            // 会变成"0 兵但仍存活"的幽灵部队继续跑 AI，最终在 EnterCity 处以零为分母崩溃。
            int supplierKeepTroops = 1;
            if (troopGive > supplier.troops - supplierKeepTroops)
                troopGive = supplier.troops - supplierKeepTroops;
            if (troopGive < 0)
                troopGive = 0;

            // ---- 兵装：按需移交一部分 ----
            ItemStore itemGive = null;
            if (ally.MaxTroops > 0 && ally.troops < ally.MaxTroops
                && supplier.itemStore != null && supplier.itemStore.TotalNumber > 0)
            {
                itemGive = supplier.itemStore.Split(50);
            }

            // ---- 资金：让补给队同时输送现金 ----
            // 【前线建筑】前线部队只有携带现金，才能在战场就地修建辅助建筑（B3 随军增筑）。
            // 因此补给不再"只送粮不送钱"：仅当友军资金低于阈值时补足，
            // 并保留补给队自身的最低资金，避免把后勤掏空。
            AIConfig cfg = AIConfig.Instance;
            int goldGive = 0;
            int goldThreshold = cfg.supplyNeedGoldThreshold;
            if (cfg.supplyTransferGoldPercent > 0
                && goldThreshold > 0
                && ally.gold < goldThreshold
                && supplier.gold > 0)
            {
                goldGive = supplier.gold * cfg.supplyTransferGoldPercent / 100;

                // 只补到"够用"为止，不做超额转移
                int need = goldThreshold - ally.gold;
                if (goldGive > need)
                    goldGive = need;

                // 保留补给队自身的最低资金
                int keep = goldThreshold;
                if (supplier.gold - goldGive < keep)
                    goldGive = supplier.gold - keep;
                if (goldGive < 0)
                    goldGive = 0;
            }

            if (foodGive <= 0 && troopGive <= 0 && goldGive <= 0
                && (itemGive == null || itemGive.TotalNumber <= 0))
                return false;

            supplier.SupplyTroop(ally, itemGive, goldGive, foodGive, troopGive);
            Sango.Log.Info($"{supplier.BelongForce?.Name}的补给队[{supplier.Name}]为[{ally.Name}]补充 粮草{foodGive} 兵力{troopGive} 资金{goldGive}!");
            return true;
        }

        /// <summary>
        /// 判断某友军是否为有效的补给目标：同势力、非运输队、有任务在身、确有补给需求。
        /// </summary>
        /// <param name="supplier">补给队</param>
        /// <param name="ally">待判断友军</param>
        /// <returns>是否可作为补给目标</returns>
        static bool IsValidSupplyTarget(Troop supplier, Troop ally)
        {
            if (ally == null || !ally.IsAlive || ally == supplier)
                return false;
            if (!ally.IsSameForce(supplier))
                return false;
            if (ally.IsTransport)
                return false;
            // 无任务的城内驻守部队不作为目标（与 CollectNeedyTroops 一致）
            if (ally.missionType <= 0)
                return false;

            return CalcSupplyNeed(ally) > 0;
        }

        /// <summary>
        /// 挑选最需要补给的友军部队（用于移动目标）。
        /// 评分同时考虑：补给需求度、与补给队的距离（越近越优先）。
        ///
        /// 【修复】改为复用 CityAI.CollectNeedyTroops，使"找目标"与"出征判定"的口径、
        /// 搜索范围（含兵装不足维度）完全一致。此前两处条件不同，会出现
        /// "出征时有目标，出发后却找不到目标"而原地待命不动的情况。
        /// </summary>
        /// <param name="supplier">补给队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>需要补给的友军，没有则返回 null</returns>
        Troop FindSupplyTarget(Troop supplier, Scenario scenario)
        {
            City home = supplier.BelongCity;
            if (home == null)
                return null;

            CityAI.CollectNeedyTroops(needyAllyCache, home, scenario);

            // 【优先】出征时指定的目标（missionTarget）若仍需要补给，优先服务它，
            // 避免"出征时判定有目标、出发后重新挑选却挑不到"而空跑。
            Troop assigned = scenario.troopsSet.Get(supplier.missionTarget);
            if (IsValidSupplyTarget(supplier, assigned))
                return assigned;

            Troop best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < needyAllyCache.Count; i++)
            {
                Troop ally = needyAllyCache[i];
                if (ally == null || !ally.IsAlive || ally == supplier)
                    continue;

                int distance = scenario.Map.Distance(supplier.cell, ally.cell);
                // 需求度越高、距离越近，越优先
                int score = CalcSupplyNeed(ally) - distance * 50;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = ally;
                }
            }

            return best;
        }

        /// <summary>
        /// 计算安全的补给站位：优先选与目标友军相邻、且距离最近敌人最远的空格。
        /// 这样补给队会待在友军「背向敌人」的一侧，由友军充当屏障。
        /// </summary>
        /// <param name="supplier">补给队</param>
        /// <param name="ally">目标友军</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>推荐的站位格（可能为 null）</returns>
        Cell FindSafeSupplyCell(Troop supplier, Troop ally, Scenario scenario)
        {
            Troop nearestEnemy = FindNearestEnemyTroop(ally, scenario);
            if (nearestEnemy == null)
                return ally.cell;

            Cell[] neighbors = ally.cell.Neighbors;
            if (neighbors == null)
                return ally.cell;

            Cell bestCell = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < neighbors.Length; i++)
            {
                Cell cell = neighbors[i];
                if (cell == null)
                    continue;
                // 必须可停留，且没有被其它部队占据
                if (cell.troop != null && cell.troop != supplier)
                    continue;
                if (!cell.CanStay(supplier))
                    continue;

                int distanceToEnemy = scenario.Map.Distance(cell, nearestEnemy.cell);
                int distanceToSelf = scenario.Map.Distance(cell, supplier.cell);

                // 离敌人越远越安全；离自己越近越省行动力
                int score = distanceToEnemy * 100 - distanceToSelf * 10;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return bestCell ?? ally.cell;
        }

        /// <summary>
        /// 查找距离指定部队最近的敌方部队（用于判断威胁方向）。
        /// </summary>
        /// <param name="self">参照部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>最近的敌方部队；威胁范围内没有敌人时返回 null</returns>
        static Troop FindNearestEnemyTroop(Troop self, Scenario scenario)
        {
            Troop nearest = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop enemy = scenario.troopsSet[i];
                if (enemy == null || !enemy.IsAlive || !self.IsEnemy(enemy))
                    continue;

                int distance = scenario.Map.Distance(self.cell, enemy.cell);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = enemy;
                }
            }

            // 范围内没有敌人则视为无威胁
            if (bestDistance > AIConfig.Instance.supplyThreatScanRange)
                return null;
            return nearest;
        }
    }
}
