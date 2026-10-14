/*
 * 文件名：BuildingPlanner.cs
 * 描述：AI 内政建筑规划器 —— 取代原来的固定建造队列（CityAI.CityBuildingTemplate）。
 *
 *       两类建筑的取舍口径**不同**：
 *         · 农田 / 市场：**不设数量上限**，只按"能不能满足需求"决定建不建
 *           （粮产要能抵扣城内士兵消耗、资金产出要覆盖城市消耗）；
 *         · 产出建筑（锻冶厂 / 厩舍 / 工房 / 造船厂）：**总数封顶**，
 *           绝对富余时可以为 0。
 *       在"确实有需求"的前提下，才用份额权重与各类修正（港口 / 地域 / 性格 / 势力水位）
 *       决定优先建哪一种。
 */

using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 内政建筑规划器。
    ///
    /// 【与旧实现的关系】
    ///   旧实现：<c>CityAI.CityBuildingTemplate</c> 的 4 套 int[][] 固定模板，
    ///           按「是否边境 / 是否有港口」选一套，坑位顺序与内政地索引一一对应 ——
    ///           建什么、先建什么，全部写死。
    ///   本类：只回答两个问题 ——「本城下一个该建什么」「要不要拆旧建新」。
    ///
    /// 【为什么返回值可能是 null】
    ///   规划器关闭 / 影子模式 / 当前没有需求时返回 null，调用方（<c>CityAI.AIBuildIntriore</c>）
    ///   应回退到旧的固定模板 —— 任何异常情况下 AI 的建设行为都不退化。
    /// </summary>
    public static class BuildingPlanner
    {
        // ==================== 入口 ====================

        /// <summary>
        /// 规划器是否处于"接管"状态（已启用且非影子模式）。
        ///
        /// 调用方据此区分两种 null：**规划器接管时说"没需求"就是真的不该建**；
        /// 规划器未接管时则应回退到旧的固定模板。
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                BuildingPlanWeights w = GetWeights();
                return w != null && w.enabled && !w.shadowOnly;
            }
        }

        /// <summary>
        /// 选出本城"下一个该建"的内政设施类型。
        ///
        /// 返回 null 表示：规划器已关闭 / 处于影子模式 / 参数异常 / 当前没有需求。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>要建的建筑类型；无需求时返回 null</returns>
        public static BuildingType PickNextBuilding(City city, Scenario scenario)
        {
            BuildingPlanWeights w = GetWeights();
            if (w == null || !w.enabled || w.shadowOnly)
                return null;
            if (city == null || scenario == null)
                return null;

            // 已经建满就没有"新建"的空间 —— 要腾位请走 TryPlanReplace
            if (city.IsInteriorBuildFull())
                return null;

            DemandContext ctx = BuildContext(city, scenario, w);

            // 调试日志（Error 级别）：只在真正选出目标时收集并输出，
            // 所以日志条数 = 实际建造次数，不会每回合每城刷屏。
            List<KeyValuePair<float, string>> logCandidates =
                ShouldLog(city.BelongForce) ? new List<KeyValuePair<float, string>>() : null;

            BuildingType best = FindBestType(city, scenario, w, ctx, logCandidates);

            if (best != null && logCandidates != null)
                LogBuildPlan(city, scenario, best, ctx, logCandidates);

            return best;
        }

        /// <summary>
        /// 判定本城是否该"拆掉一座建筑，腾出位置给更有需求的设施"。
        ///
        /// **只判定、不执行**：调用方拿到 <paramref name="outgoing"/> /
        /// <paramref name="incoming"/> 后自行 OnFall + 建造 —— 这样"先收集、再处理"
        /// 的约束落在调用方，不会在遍历建筑表的过程中改动它
        /// （SangoObjectList.ForEach 是倒序索引循环，删除非当前元素会重复处理甚至越界）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="outgoing">要拆掉的既有建筑</param>
        /// <param name="incoming">要新建的建筑类型</param>
        /// <returns>应当更换时返回 true</returns>
        public static bool TryPlanReplace(City city, Scenario scenario,
            out Building outgoing, out BuildingType incoming)
        {
            outgoing = null;
            incoming = null;

            BuildingPlanWeights w = GetWeights();
            if (w == null || !w.enabled || w.shadowOnly || !w.replaceEnabled)
                return false;
            if (city == null || scenario == null)
                return false;

            // 只有"建满"才需要腾位：有空位时直接新建更划算，没必要拆掉一座
            if (!city.IsInteriorBuildFull())
                return false;

            // 冷却：同一座城两次更换之间必须隔开，否则需求轻微波动就会反复拆建，
            //       收益全耗在重建工期里，反而比不换更差。
            int turn = scenario.TurnCount;
            if (!BuildingPlanState.CanReplace(city.Id, turn, w.replaceCooldownTurns))
                return false;

            DemandContext ctx = BuildContext(city, scenario, w);

            // 1) 现在最该建什么（不看"是否建满" —— 换就是要腾位）
            BuildingType best = FindBestType(city, scenario, w, ctx, null);
            if (best == null)
                return false;

            // 2) 需求强度门槛：新建筑至少要能补足相当于 replaceGainRatio 座的需求量，
            //    否则"能补一点点"就拆建，重建期的损失反而更大。
            float incomingScore = DemandScore(best, CountKind(city, best.kind), city, scenario, w, ctx);
            if (incomingScore < w.replaceGainRatio)
                return false;

            // 3) 金钱富余门槛（拆除不退钱，新建筑还得全额掏钱）
            if (city.gold < best.cost * w.replaceGoldFactor)
                return false;

            // 4) 找"已经没必要留"的那一座：它的类别当前是盈余 / 富余的，
            //    且拆掉后粮 / 金收支仍然不亏空（硬约束）。
            float worstKeep;
            Building worst = PickWorstReplaceable(city, scenario, w, out worstKeep);
            if (worst == null || worstKeep > 0f)
                return false;

            // 5) 可行性预检：拆完必须能立刻建起来，否则就是白拆一座。
            //    （空位由拆除本身提供，剩下的唯一变数是"有没有空闲武将可用"。）
            if (city.freePersons.Count < 1)
                return false;
            Person[] people = ForceAI.CounsellorRecommendBuild(city.freePersons, best);
            if (people == null || people.Length == 0)
                return false;

            outgoing = worst;
            incoming = best;
            return true;
        }

        /// <summary>
        /// 是否应当"紧急扩产兵装"：势力层面兵装严重不足 **且** 本城资金富余。
        ///
        /// 用于让 <c>CityAI.AICreateItems</c> 一回合多产几次（受空闲建筑数与空闲武将数约束，
        /// **不受行动点约束** —— AI 势力的 <c>Corps.ReduceActionPoint</c> 是空操作）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>应当扩产返回 true</returns>
        public static bool ShouldEmergencyExpandArms(City city, Scenario scenario)
        {
            BuildingPlanWeights w = GetWeights();
            if (w == null || !w.enabled || !w.armsEmergencyEnabled)
                return false;
            if (city == null || scenario == null || city.BelongForce == null)
                return false;

            // 钱富余（拆除 / 生产都要掏钱，穷城不配扩产）
            if (city.gold < AIConfig.Instance.createItemsMinGold * w.armsEmergencyGoldFactor)
                return false;

            // 兵装短缺：看**势力层面**的水位，而不是本城 ——
            // 前线缺兵装时，后方的富余城也该加班造（造完由资源调度送过去）。
            ForceBuildState.Snapshot snap = ForceBuildState.Get(city.BelongForce, scenario);
            return snap.armsFill < w.armsEmergencyArmsFill;
        }

        // ==================== 需求上下文 ====================

        /// <summary>
        /// 本城当期的需求快照 —— 一次算好，供所有候选共用，
        /// 避免"每个候选各自重算一遍粮 / 金收支"。
        /// </summary>
        struct DemandContext
        {
            /// <summary>年粮草缺口（&gt; 0 表示缺，等于 0 表示绝对富余）</summary>
            public int foodNeed;
            /// <summary>年金资金缺口</summary>
            public int goldNeed;
            /// <summary>产出建筑还能再建几座（总数上限 − 已有数）</summary>
            public int produceQuota;
        }

        /// <summary>采集本城的需求快照。</summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>需求快照</returns>
        static DemandContext BuildContext(City city, Scenario scenario, BuildingPlanWeights w)
        {
            DemandContext ctx;
            ctx.foodNeed = -CityEconomy.YearlyFoodBalance(city, scenario);
            ctx.goldNeed = -CityEconomy.YearlyGoldBalance(city, scenario);
            ctx.produceQuota = w.produceBuildingMaxCount - CountProduceBuildings(city, w);
            if (ctx.produceQuota < 0)
                ctx.produceQuota = 0;
            return ctx;
        }

        // ==================== 求解 ====================

        /// <summary>
        /// 在候选建筑里取需求得分最高的一种（**不看城市是否已建满**）。
        ///
        /// 与 <see cref="PickNextBuilding"/> 的区别只有这一点：
        /// 更换（拆旧建新）恰恰发生在"已建满"的时候，所以那一步不能带"满则返回 null"的限制。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="w">规划参数</param>
        /// <param name="ctx">需求快照</param>
        /// <returns>最优建筑类型；无需求返回 null</returns>
        static BuildingType FindBestType(City city, Scenario scenario, BuildingPlanWeights w,
            DemandContext ctx, List<KeyValuePair<float, string>> logCandidates)
        {
            var types = scenario.CommonData != null ? scenario.CommonData.BuildingTypes : null;
            if (types == null)
                return null;

            BuildingType best = null;
            float bestScore = 0f;

            // 权重随机用的候选收集（只在开启时分配，避免常态开销）
            List<KeyValuePair<BuildingType, float>> weighted = w.useWeightedRandom
                ? new List<KeyValuePair<BuildingType, float>>()
                : null;

            for (int i = 0; i < types.Count; i++)
            {
                BuildingType t = types[i];
                if (t == null)
                    continue;

                // 候选门槛：内政设施 + 一级形态 + canBuild（**内政方式**就在这里体现） + 科技/数量限制。
                // 与玩家侧可建列表同一口径（CityBuildBuilding.InitCanBuildingTypes），
                // 不会出现"AI 能建、玩家不能建"的分歧。
                if (!t.CanBuildInteriorAt(city))
                    continue;

                if (IsExcluded(w, t.kind))
                    continue;

                int now = CountKind(city, t.kind);
                int cap = MaxPerCityOf(t, w);
                if (cap > 0 && now >= cap)
                    continue;

                float score = DemandScore(t, now, city, scenario, w, ctx);
                if (score <= 0f)
                    continue;

                if (logCandidates != null)
                    logCandidates.Add(new KeyValuePair<float, string>(
                        score, string.Format("{0}({1})", t.Name, t.kind)));

                if (weighted != null)
                    weighted.Add(new KeyValuePair<BuildingType, float>(t, score));

                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            // 【权重随机】得分即权重，按比例抽取 —— 数量比例会自然逼近得分比例，
            // 同时天然避免"平局时永远选中遍历在前的那个"（市场 Id=32 压过农田 33）。
            //
            // 注意：GameRandom.RandomWeightIndex 内部用 `v > weight` 判断，
            // 权重为 0 的项在 v=0 时会被误选中，所以每项权重至少取 1。
            if (weighted != null && weighted.Count > 0)
            {
                int[] weights = new int[weighted.Count];
                for (int i = 0; i < weighted.Count; i++)
                {
                    int wi = (int)(weighted[i].Value * 100f);
                    weights[i] = wi > 0 ? wi : 1;
                }
                return weighted[GameRandom.RandomWeightIndex(weights)].Key;
            }

            return best;
        }

        /// <summary>
        /// 单种建筑的"需求得分"。三种情形口径不同：
        ///
        /// ① **产出建筑**（锻冶厂 / 厩舍 / 工房 / 造船厂）
        ///    —— 受"名额 + 实际缺口"双重约束：名额用完、或该类资源绝对富余，一律得 0。
        ///    有需求时按份额权重 × 势力水位 ÷ (已建数+1) 排序。
        ///
        /// ② **粮草 / 资金类**（农田 / 市场 …）
        ///    —— **无数量上限**：得分 = 缺口 ÷ 单座年产能，即"还差几座"。
        ///    缺口为 0（绝对富余）时得 0，一座都不多建。
        ///    造币厂 / 谷仓这类"乘数型"建筑不直接产出，另有判别，有缺口时给固定分。
        ///
        /// ③ **其它**（兵舍 / 军事府 / 人才府 …）
        ///    —— 不影响粮 / 金收支，沿用份额权重铺开。
        /// </summary>
        /// <param name="t">候选建筑类型</param>
        /// <param name="now">本城该类建筑现有数量（含在建）</param>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="w">规划参数</param>
        /// <param name="ctx">需求快照</param>
        /// <returns>需求得分（&lt;= 0 表示不建）</returns>
        static float DemandScore(BuildingType t, int now, City city, Scenario scenario,
            BuildingPlanWeights w, DemandContext ctx)
        {
            int kind = t.kind;
            float baseRatio = BaseRatioOf(kind, w);
            if (baseRatio <= 0f)
                return 0f;

            // ---------- ① 产出建筑 ----------
            if (IsProduceKind(w, kind))
            {
                if (ctx.produceQuota <= 0)
                    return 0f;                                  // 名额已满（总数上限）
                if (!ProduceKindNeeded(kind, city, scenario, w))
                    return 0f;                                  // 绝对富余 → 可以为 0

                return baseRatio
                    * RegionFactor(city, kind)
                    * PersonalityFactor(t, city, w)
                    * SupplyFactor(kind, city, w, scenario)
                    / (now + 1);
            }

            // ---------- ② 粮草 / 资金类（无上限，只看缺口） ----------
            EconomyKind ek = CityEconomy.KindOf(kind);

            if (ek == EconomyKind.Food)
            {
                // 【硬性基础】每城至少 baseFarmCount 座农田 —— 粮草盈余时也要先补齐，
                // 否则"刚好够"的产能一碰上征兵 / 出征就会立刻断粮。
                if (kind == (int)BuildingKindType.Farm && now < w.baseFarmCount)
                    return (w.baseFarmCount - now) * w.baseDemandPriority;

                if (ctx.foodNeed <= 0)
                {
                    // 【补 1 增 1】需求已满足，且还有空地、资金宽裕时：
                    //   · 农田 —— 继续扩张产能（得分与功能建筑交替）；
                    //   · 谷仓这类乘数型 —— 基础农田够多时也补一座，提升整体效率。
                    if (kind == (int)BuildingKindType.Farm && ShouldExpand(city, w))
                        return CapacityExpandScore(city, w, kind);

                    if (CityEconomy.IsMultiplierBuilding(t, ek)
                        && CountKind(city, MultiplierBaseKind(kind)) >= w.multiplierMinBase
                        && ShouldExpand(city, w))
                        return w.expandScore;

                    return 0f;                                  // 否则：绝对富余 → 不建
                }

                if (CityEconomy.IsMultiplierBuilding(t, ek))
                {
                    // 乘数型只放大同类基础建筑（造币厂→市场 / 谷仓→农田）：
                    // 基础建筑太少时建它等于放大一个空架子，先不给分。
                    if (CountKind(city, MultiplierBaseKind(kind)) < w.multiplierMinBase)
                        return 0f;
                    return baseRatio * PersonalityFactor(t, city, w) * RegionFactor(city, kind);
                }

                int output = CityEconomy.YearlyFoodOf(t);
                if (output <= 0)
                    return 0f;

                // 缺口 ÷ 单座年产能 = 还差几座（无上限，缺口越大越想建）
                return (float)ctx.foodNeed / output
                    * RegionFactor(city, kind)
                    * PersonalityFactor(t, city, w);
            }

            if (ek == EconomyKind.Gold)
            {
                // 【硬性基础】每城至少 baseMarketCount 座市场 —— 武将俸禄与俘虏关押是刚性支出，
                // 资金盈余时也要先打底。
                if (kind == (int)BuildingKindType.Market && now < w.baseMarketCount)
                    return (w.baseMarketCount - now) * w.baseDemandPriority;

                if (ctx.goldNeed <= 0)
                {
                    // 【补 1 增 1】资金有富余且还有空地：
                    //   · 市场 —— 继续扩张产能（与功能建筑交替）；
                    //   · 造币厂这类乘数型 —— 基础市场够多时也补一座。
                    if (kind == (int)BuildingKindType.Market && ShouldExpand(city, w))
                        return CapacityExpandScore(city, w, kind);

                    if (CityEconomy.IsMultiplierBuilding(t, ek)
                        && CountKind(city, MultiplierBaseKind(kind)) >= w.multiplierMinBase
                        && ShouldExpand(city, w))
                        return w.expandScore;

                    return 0f;
                }

                if (CityEconomy.IsMultiplierBuilding(t, ek))
                {
                    // 乘数型只放大同类基础建筑（造币厂→市场 / 谷仓→农田）：
                    // 基础建筑太少时建它等于放大一个空架子，先不给分。
                    if (CountKind(city, MultiplierBaseKind(kind)) < w.multiplierMinBase)
                        return 0f;
                    return baseRatio * PersonalityFactor(t, city, w) * RegionFactor(city, kind);
                }

                int output = CityEconomy.YearlyGoldOf(t);
                if (output <= 0)
                    return 0f;

                return (float)ctx.goldNeed / output
                    * RegionFactor(city, kind)
                    * PersonalityFactor(t, city, w);
            }

            // ---------- ③ 其它（兵舍 / 军事府 / 人才府 …） ----------
            // 这类建筑的需求是**软性**的（治安 / 搜人 / 征兵能力），量纲与"还差几座"不同，
            // 所以按 minorScoreScale 压到硬缺口之下：粮 / 金亏空时优先补产能，之后才轮到它们。
            return baseRatio
                * w.minorScoreScale
                * (IsPortBiasKind(w, kind) ? PortFactor(city, w) : 1f)
                * RegionFactor(city, kind)
                * PersonalityFactor(t, city, w)
                * SupplyFactor(kind, city, w, scenario)
                / (now + 1);
        }

        // ==================== 需求口径 ====================

        /// <summary>
        /// 兵装缺口 = 守军兵力 × <see cref="BuildingPlanWeights.armsCoverRatio"/> − 现有兵装
        /// （枪 / 戟 / 弩 / 军马，与 <see cref="CitySituation"/> 同一口径）。
        ///
        /// 注意：**兵装不是兵器** —— 兵器（冲车/木兽/井阑/投石）见 <see cref="MachineNeed"/>。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>缺口（可为负）</returns>
        static int ArmsNeed(City city, BuildingPlanWeights w)
        {
            int want = (int)(city.troops * w.armsCoverRatio);
            int have = 0;
            if (city.itemStore != null)
            {
                for (int id = (int)ItemStoreKindType.Spear; id <= (int)ItemStoreKindType.Horse; id++)
                    have += city.itemStore.GetNumber(id);
            }
            return want - have;
        }

        /// <summary>
        /// 兵器缺口 = **兵器库存上限 × 持有率** − 现有兵器数。
        ///
        /// 【兵器 = 冲车 / 木兽 / 井阑 / 投石】它们是**攻城**用的台装兵器，与兵装（枪戟弩马）完全不同。
        /// 需求按"库存上限的 10%~30%"算（前线高一档），而不是按兵力量级 ——
        /// 后者会得出"3 万兵要 3 万台兵器"这种不可能的数字。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <param name="scenario">场景对象（读 ItemType 求库存上限）</param>
        /// <returns>缺口（可为负）</returns>
        static int MachineNeed(City city, BuildingPlanWeights w, Scenario scenario)
        {
            int capacity = MachineCapacity(city, scenario);
            float ratio = city.IsBorderCity ? w.machineHoldRatioBorder : w.machineHoldRatioRear;
            int want = (int)(capacity * ratio);
            return want - MachineCount(city);
        }

        /// <summary>
        /// 兵器库存上限：从**工房的产出清单**里取一种兵器，按它的 <c>p1</c> 换算
        /// （4 种兵器 p1 均为 10 → 上限 = 仓库上限 × 10/1000 = 1%）。
        ///
        /// 用数据反查而不是写死道具 Id：将来改产出清单（或换建筑）都不会失配。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>库存上限（读不到数据时按 1% 兜底）</returns>
        static int MachineCapacity(City city, Scenario scenario)
        {
            if (scenario != null)
            {
                BuildingType factory = scenario.GetObject<BuildingType>((int)BuildingKindType.MechineFactory);
                if (factory != null && factory.productItems != null && factory.productItems.Length > 0)
                {
                    ItemType t = scenario.GetObject<ItemType>(factory.productItems[0]);
                    if (t != null)
                        return t.TransformLimit(city.StoreLimit);
                }
            }

            return city.StoreLimit / 100;
        }

        /// <summary>舰船缺口 = 守军兵力 ÷ 每艘兵数 − 现有舰船；没有下属港口的城不需要船。</summary>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>缺口（可为负）</returns>
        static int BoatNeed(City city, BuildingPlanWeights w)
        {
            if (city.portList == null || city.portList.Count == 0)
                return 0;

            int per = w.boatTroopsPerUnit > 0 ? w.boatTroopsPerUnit : 2000;
            int have = city.itemStore != null
                ? city.itemStore.GetNumber((int)ItemStoreKindType.Boat)
                : 0;
            return city.troops / per - have;
        }

        /// <summary>器械数量（冲车 + 投石）。</summary>
        /// <param name="city">城市对象</param>
        /// <returns>器械数</returns>
        static int MachineCount(City city)
        {
            if (city.itemStore == null)
                return 0;

            return city.itemStore.GetNumber((int)ItemStoreKindType.Helepolis)
                 + city.itemStore.GetNumber((int)ItemStoreKindType.Catapult);
        }

        /// <summary>
        /// 该产出建筑当前是否有需求（对应资源的缺口达到阈值）。
        /// 全部返回 false（绝对富余）时，产出建筑可以一座都不建 —— 这正是
        /// "产出建筑在绝对富余情况下可以为 0"的落点。
        /// </summary>
        /// <param name="kind">建筑小类</param>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>有需求返回 true</returns>
        static bool ProduceKindNeeded(int kind, City city, Scenario scenario, BuildingPlanWeights w)
        {
            switch ((BuildingKindType)kind)
            {
                case BuildingKindType.BlacksmithShop:
                case BuildingKindType.Stable:
                    return ArmsNeed(city, w) >= w.armsNeedMinGap;

                case BuildingKindType.MechineFactory:
                    return MachineNeed(city, w, scenario) >= w.machineNeedMinGap;

                case BuildingKindType.BoatFactory:
                    return BoatNeed(city, w) >= w.boatNeedMinGap;

                default:
                    return false;
            }
        }

        /// <summary>本城已建的产出建筑总数（锻冶厂 + 厩舍 + 工房 + 造船厂）。</summary>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>总数</returns>
        static int CountProduceBuildings(City city, BuildingPlanWeights w)
        {
            if (w.produceKinds == null)
                return 0;

            int total = 0;
            for (int i = 0; i < w.produceKinds.Length; i++)
                total += CountKind(city, w.produceKinds[i]);
            return total;
        }

        /// <summary>该建筑是否属于"产出建筑"（受总数上限约束）。</summary>
        /// <param name="w">规划参数</param>
        /// <param name="kind">建筑小类</param>
        /// <returns>是则返回 true</returns>
        static bool IsProduceKind(BuildingPlanWeights w, int kind)
        {
            return Contains(w.produceKinds, kind);
        }

        /// <summary>
        /// "补 1 增 1" 交替下的**产能扩张**得分（农田 / 市场在需求已满足时才用）。
        ///
        /// 两层交替：
        ///   ① **功能建筑 vs 产能建筑** —— 谁的已建数少就先补谁；
        ///   ② **农田 vs 市场** —— 数量少的一方优先。
        ///
        /// 【第 ② 层为什么必须要有】两者在扩张阶段得分完全相同，而择优是"严格大于才替换"，
        /// 于是遍历在前的市场（BuildingKindType = 32 < 农田 33）**永远胜出**，
        /// 实际表现就是市场越建越多、农田停滞。
        ///
        /// 分档（以 <see cref="BuildingPlanWeights.expandScore"/> = 0.3 为基准）：
        ///   · 产能落后 → 0.75，压过功能建筑的兜底分（约 0.3~0.6）；
        ///   · 否则     → 0.06，把机会让给功能建筑。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <param name="kind">建筑小类（农田 / 市场）</param>
        /// <returns>扩张得分</returns>
        static float CapacityExpandScore(City city, BuildingPlanWeights w, int kind)
        {
            int farm = CountKind(city, (int)BuildingKindType.Farm);
            int market = CountKind(city, (int)BuildingKindType.Market);
            int capacity = farm + market;

            // 内政地上已用的格子总数 − 农田市场 = 功能建筑数
            // （内政地只放内政建筑，所以不必再按 IsIntrior 过滤）
            int function = city.GetInteriorCellUsedCount() - capacity;

            float score = capacity <= function ? w.expandScore * 2.5f : w.expandScore * 0.2f;

            // ② 农田 / 市场之间再交替：数量多的那一方折半，与少的一方拉开差距
            if (kind == (int)BuildingKindType.Farm && farm > market)
                score *= w.capacityAlternateFactor;
            else if (kind == (int)BuildingKindType.Market && market > farm)
                score *= w.capacityAlternateFactor;

            return score;
        }

        /// <summary>
        /// 是否应当"继续建设"：需求虽已满足，但本城还有空内政地 ——
        /// **内政地未建满时不保留现金**，继续把钱投入建设
        /// （空着的格子本身就是"没做完的投入"，留现金在库里等于浪费发展时间）。
        ///
        /// 自限来自两条：① 建满即停；② 钱不够时 <c>BuildOne</c> 会拒绝（它检查
        /// <c>city.gold >= 造价</c>），自然停手 —— 所以这里不必重复判断金额。
        /// 建满之后本条失效，钱开始攒下来用于升级 / 应急。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>可以扩张返回 true</returns>
        static bool ShouldExpand(City city, BuildingPlanWeights w)
        {
            if (!w.expandEnabled)
                return false;

            if (city.IsInteriorBuildFull())
                return false;                                   // 建满即停

            return city.gold > w.expandGoldKeep;                // 默认 0：几乎不留钱
        }

        /// <summary>
        /// 乘数型建筑所放大的"基础建筑"小类（造币厂 → 市场、谷仓 → 农田）；非乘数型返回 0。
        /// </summary>
        /// <param name="kind">建筑小类</param>
        /// <returns>基础建筑小类；非乘数型返回 0</returns>
        static int MultiplierBaseKind(int kind)
        {
            if (kind == (int)BuildingKindType.Mint)
                return (int)BuildingKindType.Market;
            if (kind == (int)BuildingKindType.Barn)
                return (int)BuildingKindType.Farm;
            return 0;
        }

        /// <summary>
        /// 该建筑小类在"更换"时必须保留的最少座数 =
        /// max(通用 <see cref="BuildingPlanWeights.replaceMinKeep"/>,
        ///     硬性基础座数（农田 / 市场）)。
        ///
        /// 否则会出现"把打底的农田拆掉换成别的"—— 与 <see cref="BuildingPlanWeights.baseFarmCount"/>
        /// 的意图直接冲突。
        /// </summary>
        /// <param name="kind">建筑小类</param>
        /// <param name="w">规划参数</param>
        /// <returns>最少保留座数</returns>
        static int ReplaceMinKeepOf(int kind, BuildingPlanWeights w)
        {
            int keep = w.replaceMinKeep;

            if (kind == (int)BuildingKindType.Farm && w.baseFarmCount > keep)
                keep = w.baseFarmCount;
            else if (kind == (int)BuildingKindType.Market && w.baseMarketCount > keep)
                keep = w.baseMarketCount;

            return keep;
        }

        /// <summary>
        /// 该建筑"拆掉之后是否还有保留价值"（1 = 有，0 = 没有，越大越不该拆）。
        ///
        /// 判定口径：它所属的经济类别当前是**亏空**（说明还需要它）→ 有价值；
        /// 已经**盈余 / 富余** → 没有保留价值，可以让位。
        /// </summary>
        /// <param name="kind">建筑小类</param>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>保留价值</returns>
        static float KeepValue(int kind, City city, Scenario scenario, BuildingPlanWeights w)
        {
            EconomyKind ek = CityEconomy.KindOf(kind);

            if (ek == EconomyKind.Food)
                return CityEconomy.YearlyFoodBalance(city, scenario) <= 0 ? 1f : 0f;

            if (ek == EconomyKind.Gold)
                return CityEconomy.YearlyGoldBalance(city, scenario) <= 0 ? 1f : 0f;

            if (IsProduceKind(w, kind))
                return ProduceKindNeeded(kind, city, scenario, w) ? 1f : 0f;

            // 兵舍 / 军事府 / 人才府等不影响粮 / 金收支，保留价值最低
            return 0f;
        }

        // ==================== 更换目标 ====================

        /// <summary>
        /// 在既有建筑里挑出"保留价值最低"且允许拆掉的那一座。
        ///
        /// 排除项：在建 / 在升级 / 正在工作（拆了会中断产出）、不在可拆白名单、
        /// 拆后同类会低于 <see cref="BuildingPlanWeights.replaceMinKeep"/> 座的、
        /// 以及**拆掉会打穿粮 / 金收支**的（硬约束 —— 粮产必须还能抵扣士兵消耗，
        /// 资金产出必须还能覆盖城市消耗）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="w">规划参数</param>
        /// <param name="keepValue">该建筑的保留价值</param>
        /// <returns>最该拆的建筑；没有可拆的返回 null</returns>
        static Building PickWorstReplaceable(City city, Scenario scenario, BuildingPlanWeights w,
            out float keepValue)
        {
            keepValue = 1f;
            Building worst = null;

            if (city.allBuildings == null)
                return null;

            for (int i = 0; i < city.allBuildings.Count; i++)
            {
                Building b = city.allBuildings[i];
                if (b == null || b.BuildingType == null)
                    continue;
                if (!b.isComplate || b.isUpgrading || b.isWorking)
                    continue;
                if (!IsReplaceableKind(w, b.BuildingType.kind))
                    continue;

                int now = CountKind(city, b.BuildingType.kind);
                if (now <= ReplaceMinKeepOf(b.BuildingType.kind, w))
                    continue;                                   // 同类型必须留够（含硬性基础）

                // 硬约束：拆掉后粮 / 金收支不能变成亏空
                if (!CityEconomy.CanAffordRemoving(city, scenario, b))
                    continue;

                float v = KeepValue(b.BuildingType.kind, city, scenario, w);
                if (worst == null || v < keepValue)
                {
                    keepValue = v;
                    worst = b;
                }
            }

            return worst;
        }

        // ==================== 修正乘子 ====================

        /// <summary>
        /// 港口修正：造船厂 / 鱼市在有下属港口的城里权重更高（否则造出来没水域可用）。
        ///
        /// 【判定口径】用"本城是否**拥有下属港口**"（city.portList），**不能用 city.IsPort()** ——
        /// 港关本身不参与都市内政（OnCityAIPrepare 开头就有 <c>if (!city.IsCity()) return;</c>），
        /// 那条分支永远不会命中。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>乘数</returns>
        static float PortFactor(City city, BuildingPlanWeights w)
        {
            bool hasPort = city.portList != null && city.portList.Count > 0;
            return hasPort ? w.portBias : w.nonPortBias;
        }

        /// <summary>
        /// 性格修正乘数：由**太守**（本城 Leader）与**主公**（势力君主）的性格加权混合而来。
        ///
        /// 数据源是 <see cref="Personality"/> 的 13 项 domestic*Scale（%，100 = 平均值）——
        /// 它们本来就是"这个性格偏好哪类内政"的表达（农业 / 商业 / 治安 / 生产兵装 …），
        /// 直接拿来当建筑偏好即可，不必为建筑再造一套性格数据。
        /// </summary>
        /// <param name="t">候选建筑类型</param>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <returns>乘数（1 = 不修正）</returns>
        static float PersonalityFactor(BuildingType t, City city, BuildingPlanWeights w)
        {
            if (!w.personalityEnabled)
                return 1f;

            Force force = city.BelongForce;
            Personality leader = city.Leader != null ? city.Leader.mPersonality : null;
            Personality governor = force != null && force.mGovernor != null ? force.mGovernor.mPersonality : null;

            if (leader == null && governor == null)
                return 1f;

            int leaderScale = PersonalityScaleOf(leader, t.kind);
            int governorScale = PersonalityScaleOf(governor, t.kind);

            float lw = w.leaderPersonalityWeight;
            float gw = w.governorPersonalityWeight;

            // 缺一方时把权重全部让给另一方（"没人"不该被当成"平均"）
            if (leader == null) { lw = 0f; gw = 1f; }
            else if (governor == null) { gw = 0f; lw = 1f; }
            else
            {
                float sum = lw + gw;
                if (sum <= 0f) { lw = 0.5f; gw = 0.5f; }
                else { lw /= sum; gw /= sum; }
            }

            float scale = leaderScale * lw + governorScale * gw;

            // 100% → 1.0，偏离部分按 sensitivity 放大后再限幅
            float factor = 1f + (scale - 100f) * 0.01f * w.personalitySensitivity;
            if (factor < w.personalityFactorMin) factor = w.personalityFactorMin;
            if (factor > w.personalityFactorMax) factor = w.personalityFactorMax;
            return factor;
        }

        /// <summary>
        /// 该建筑对应的"性格内政效率"取值（%，无对应项返回 100）。
        ///
        /// 这是**结构映射**（哪个建筑归哪个性格字段管），不是数值配置 ——
        /// 数值本身在 <see cref="Personality"/> 的 domestic*Scale 里，策划可直接调。
        /// </summary>
        /// <param name="p">性格（可为 null）</param>
        /// <param name="kind">建筑小类</param>
        /// <returns>效率百分比（100 = 平均）</returns>
        static int PersonalityScaleOf(Personality p, int kind)
        {
            if (p == null)
                return 100;

            switch ((BuildingKindType)kind)
            {
                // 粮草系
                case BuildingKindType.Farm:             return p.domesticFarmingScale;
                case BuildingKindType.Barn:             return p.domesticFarmingScale;
                case BuildingKindType.MilitaryGarrison: return p.domesticFarmingScale;
                // 资金系
                case BuildingKindType.Market:           return p.domesticDevelopScale;
                case BuildingKindType.Mint:             return p.domesticDevelopScale;
                case BuildingKindType.BigMarket:        return p.domesticDevelopScale;
                case BuildingKindType.FishMarket:       return p.domesticDevelopScale;
                // 兵员 / 兵装系
                case BuildingKindType.Barracks:         return p.domesticRecruitTroopScale;
                case BuildingKindType.BlacksmithShop:   return p.domesticCreateItemScale;
                case BuildingKindType.Stable:           return p.domesticCreateItemScale;
                case BuildingKindType.MechineFactory:   return p.domesticCreateMachineScale;
                case BuildingKindType.BoatFactory:      return p.domesticCreateBoatScale;
                // 治安 / 人才系
                case BuildingKindType.MilitaryOffice:   return p.domesticSecurityScale;
                case BuildingKindType.RecruitBuilding:  return p.domesticSearchScale;
                default:                                return 100;
            }
        }

        /// <summary>
        /// 势力资源水位修正：整个势力该资源**紧缺 → 抬高**此类产出建筑，**富余 → 压低**。
        ///
        /// 数据来自 <see cref="ForceBuildState"/>（每回合每势力算一次，O(城数)/回合，
        /// 与"每城各扫一遍全势力"的 O(城数²) 相比是数量级的差别）。
        /// </summary>
        /// <param name="kind">建筑小类</param>
        /// <param name="city">城市对象</param>
        /// <param name="w">规划参数</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>乘数（1 = 不修正）</returns>
        static float SupplyFactor(int kind, City city, BuildingPlanWeights w, Scenario scenario)
        {
            if (!w.supplyEnabled)
                return 1f;

            Force force = city.BelongForce;
            if (force == null || scenario == null)
                return 1f;

            EconomyKind ek = CityEconomy.KindOf(kind);
            if (ek != EconomyKind.Gold && ek != EconomyKind.Food && ek != EconomyKind.Arms)
                return 1f;

            ForceBuildState.Snapshot snap = ForceBuildState.Get(force, scenario);

            int fill;
            if (ek == EconomyKind.Gold) fill = snap.goldFill;
            else if (ek == EconomyKind.Food) fill = snap.foodFill;
            else fill = snap.armsFill;

            if (fill < w.supplyLowFill) return w.supplyLowBias;
            if (fill > w.supplyHighFill) return w.supplyHighBias;
            return 1f;
        }

        /// <summary>
        /// 地域 / 州 的建筑偏好乘数（**百分比换算成倍数**，100 = 不修正）。
        ///
        /// 优先级：城市所属**州（Province）** → **地域（Region）**，两处都没配则不修正。
        /// 更细粒度的配置覆盖更粗的，**不相乘**。
        ///
        /// 【当前状态】既有数据里 Province / Region 的 buildBias 都是 null，
        /// 所以这一层恒为 1 —— 属于"预留但已接通"：将来在
        /// Provinces.json / Regions.json 里填 buildBias，立刻生效，无需再改代码。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="kind">建筑小类</param>
        /// <returns>乘数（1 = 不修正）</returns>
        static float RegionFactor(City city, int kind)
        {
            Province province = city.province;

            int bias = 0;
            if (province != null && province.buildBias != null)
                province.buildBias.TryGetValue(kind, out bias);

            if (bias <= 0)
            {
                Region region = province != null ? province.Region : null;
                if (region != null && region.buildBias != null)
                    region.buildBias.TryGetValue(kind, out bias);
            }

            return bias > 0 ? bias * 0.01f : 1f;
        }

        // ==================== 工具 ====================

        /// <summary>
        /// 本城该建筑小类的数量上限（**0 = 不限制**）。
        ///
        /// 优先级：
        ///   ① 建筑数据自带的限制 —— limitType（1 限一座 / 2 限港口一座 / 3 限大都市一座）
        ///      与 buildNumLimit；
        ///   ② 配置 <see cref="BuildingPlanWeights.maxPerCity"/>；
        ///   ③ 配置 <see cref="BuildingPlanWeights.defaultMaxPerCity"/>（默认 1）。
        ///
        /// 【为什么必须有这一层】建筑数据里只有限量型才有上限，"无限量型"（如兵舍）是 0 ——
        /// 若规划器不兜底，它会在"没有别的需求"时被无限堆叠。
        /// </summary>
        /// <param name="t">候选建筑类型</param>
        /// <param name="w">规划参数</param>
        /// <returns>每城上限（0 = 不限制）</returns>
        static int MaxPerCityOf(BuildingType t, BuildingPlanWeights w)
        {
            // ① 数据自带的限制
            if (t.limitType != 0 || t.buildNumLimit > 0)
                return t.buildNumLimit > 0 ? t.buildNumLimit : 1;

            // ② 配置表
            int configured;
            if (w.maxPerCity != null && w.maxPerCity.TryGetValue(t.kind, out configured))
                return configured;

            // ③ 默认值
            return w.defaultMaxPerCity;
        }

        /// <summary>本城该建筑类型的数量（含正在建造 / 升级中的）。</summary>
        /// <param name="city">城市对象</param>
        /// <param name="kind">建筑小类</param>
        /// <returns>数量</returns>
        static int CountKind(City city, int kind)
        {
            int count;
            if (city.buildingCountMap != null && city.buildingCountMap.TryGetValue(kind, out count))
                return count;
            return 0;
        }

        /// <summary>该建筑的份额权重（未配置的用 defaultRatio）。</summary>
        /// <param name="kind">建筑小类</param>
        /// <param name="w">规划参数</param>
        /// <returns>份额权重</returns>
        static float BaseRatioOf(int kind, BuildingPlanWeights w)
        {
            int ratio;
            if (w.baseRatio == null || !w.baseRatio.TryGetValue(kind, out ratio))
                ratio = w.defaultRatio;
            return ratio;
        }

        /// <summary>该建筑是否属于"受港口修正"的类别。</summary>
        /// <param name="w">规划参数</param>
        /// <param name="kind">建筑小类</param>
        /// <returns>是则返回 true</returns>
        static bool IsPortBiasKind(BuildingPlanWeights w, int kind)
        {
            return Contains(w.portBiasKinds, kind);
        }

        /// <summary>该建筑是否被列入"永不新建"名单（如黑市 —— 每年 1 月被强制拆除）。</summary>
        /// <param name="w">规划参数</param>
        /// <param name="kind">建筑小类</param>
        /// <returns>被排除返回 true</returns>
        static bool IsExcluded(BuildingPlanWeights w, int kind)
        {
            return Contains(w.excludedKinds, kind);
        }

        /// <summary>该建筑是否在"可被拆掉"的白名单里（只列无限量型）。</summary>
        /// <param name="w">规划参数</param>
        /// <param name="kind">建筑小类</param>
        /// <returns>可拆返回 true</returns>
        static bool IsReplaceableKind(BuildingPlanWeights w, int kind)
        {
            return Contains(w.replaceableKinds, kind);
        }

        /// <summary>
        /// 规划报告是否输出到当前势力。
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>输出返回 true</returns>
        static bool ShouldLog(Force force)
        {
            BuildingPlanWeights w = GetWeights();
            if (w == null || !w.logEnabled)
                return false;

            // logForceId：-1 = 全部；0 = 只报告玩家势力；>0 = 只报告该势力
            if (w.logForceId > 0)
                return force != null && force.Id == w.logForceId;
            if (w.logForceId == 0)
                return force != null && force.IsPlayer;
            return true;
        }

        /// <summary>
        /// 输出"本城为什么建这个"的规划报告。
        ///
        /// 【为什么用 <c>Info(message, LogType.None)</c> 这个重载】
        ///   · <c>Sango.Log.Info(object)</c> 与 <c>Warning(object)</c> 都带
        ///     <c>[Conditional("SANGO_DEBUG")]</c>，而本工程任何平台都没定义 SANGO_DEBUG，
        ///     走它们等于**什么都不打**；
        ///   · 带 <c>LogType</c> 参数的 <c>Info(object, LogType)</c> **没有**该特性，会真正输出，
        ///     且级别是普通 Log —— 不会污染 Console 的 Error 列表。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="chosen">最终选中的建筑类型</param>
        /// <param name="ctx">需求快照</param>
        /// <param name="candidates">候选（得分 + 名称）</param>
        static void LogBuildPlan(City city, Scenario scenario, BuildingType chosen,
            DemandContext ctx, List<KeyValuePair<float, string>> candidates)
        {
            StringBuilder sb = new StringBuilder(512);

            sb.Append("[建筑规划] ").Append(city.Name);
            if (city.BelongForce != null)
                sb.Append('(').Append(city.BelongForce.Name).Append(')');
            sb.Append("  空地 ").Append(city.InteriorCellCount - city.GetInteriorCellUsedCount())
              .Append('/').Append(city.InteriorCellCount)
              .Append("  产出名额 ").Append(ctx.produceQuota)
              .Append("  金 ").Append(city.gold);

            sb.Append("\n  粮: 年耗 ").Append(CityEconomy.YearlyFoodCost(city, scenario))
              .Append(" / 产出 ").Append(CityEconomy.YearlyFoodSupply(city))
              .Append(" + ").Append(CityEconomy.PendingYearlyOutput(city, EconomyKind.Food))
              .Append("(在建) → 缺口 ").Append(ctx.foodNeed);

            sb.Append("\n  金: 年需 ").Append(CityEconomy.YearlyGoldDemand(city, scenario))
              .Append(" / 产出 ").Append(CityEconomy.YearlyGoldSupply(city))
              .Append(" + ").Append(CityEconomy.PendingYearlyOutput(city, EconomyKind.Gold))
              .Append("(在建) → 缺口 ").Append(ctx.goldNeed);

            sb.Append("\n  选中: ").Append(chosen.Name)
              .Append('(').Append(chosen.kind).Append(')');

            // 候选按得分降序，最多列 6 条
            candidates.Sort((a, b) => b.Key.CompareTo(a.Key));
            sb.Append("\n  候选: ");
            int n = candidates.Count < 6 ? candidates.Count : 6;
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                    sb.Append(" | ");
                sb.Append(candidates[i].Value).Append(' ')
                  .Append(candidates[i].Key.ToString("F2"));
            }

            Sango.Log.Info(sb.ToString(), Sango.Log.LogType.None);
        }

        /// <summary>整数数组是否包含指定值（配置里的小工具，避免到处写重复循环）。</summary>
        /// <param name="arr">数组（可为 null）</param>
        /// <param name="value">待查值</param>
        /// <returns>包含返回 true</returns>
        static bool Contains(int[] arr, int value)
        {
            if (arr == null)
                return false;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == value)
                    return true;
            }
            return false;
        }

        /// <summary>取建筑规划参数（配置总入口）。</summary>
        /// <returns>参数对象（可能为 null）</returns>
        static BuildingPlanWeights GetWeights()
        {
            AIConfig config = AIConfig.Instance;
            return config != null ? config.buildingPlan : null;
        }
    }
}
