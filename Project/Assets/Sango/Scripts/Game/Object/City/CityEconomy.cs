/*
 * 文件名：CityEconomy.cs
 * 描述：城市收支评估 —— 把"这座城的钱粮是盈余还是亏空"算清楚，
 *       供内政建筑规划判断"该建几座农田 / 市场"，以及"拆掉某座会不会把收支打穿"。
 */

namespace Sango.Core
{
    /// <summary>
    /// 建筑归属的经济资源类别（与城市的粮 / 金收支口径一一对应）。
    /// </summary>
    public enum EconomyKind
    {
        /// <summary>不参与粮 / 金收支（兵舍 / 工房 / 造船厂 / 军事府 / 人才府 等）</summary>
        None = -1,
        /// <summary>资金（市场 / 造币厂 / 大市场 / 鱼市）</summary>
        Gold = 0,
        /// <summary>粮草（农田 / 谷仓 / 军屯农）</summary>
        Food = 1,
        /// <summary>兵装（锻冶厂 / 厩舍）</summary>
        Arms = 2,
    }

    /// <summary>
    /// 城市收支评估。
    ///
    /// 【为什么单独一层】建筑规划要回答的已经不是"各类建筑按什么权重铺开"，而是
    ///   「这座城的**缺口**出在粮上还是钱上、还差多少」。这需要把游戏里四条
    ///   不同结算频率的收支折算到同一口径（**年**），否则"每回合 vs 每季"混着算会差一个数量级：
    ///     · 粮耗：每**回合**结算（<c>City.OnForceTurnStart</c> → <c>CostFood</c>）
    ///     · 粮产：每**季**结算（<c>OnCitySeasonStart</c>）
    ///     · 金耗：每**月**结算（<c>OnCityMonthStart</c>，武将俸禄 + 俘虏关押）
    ///     · 金产：每**月**结算（<c>OnCityMonthStart</c>）
    ///   一年 36 回合 = 12 月 = 4 季（每月 3 回合、每季 9 回合）。
    /// </summary>
    public static class CityEconomy
    {
        /// <summary>一年回合数</summary>
        public const int TurnsPerYear = 36;

        /// <summary>一年月份数（每月 3 回合）</summary>
        public const int MonthsPerYear = 12;

        /// <summary>一年季度数（每季 9 回合）</summary>
        public const int SeasonsPerYear = 4;

        // ==================== 年化收支 ====================

        /// <summary>
        /// 城市一年粮草消耗 = 每回合粮耗 × 36。
        /// 每回合粮耗含驻军口粮与人口口粮（见 <c>City.FoodCost</c>）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>年粮耗</returns>
        public static int YearlyFoodCost(City city, Scenario scenario)
        {
            if (city == null || scenario == null)
                return 0;
            return city.FoodCost(scenario) * TurnsPerYear;
        }

        /// <summary>
        /// 城市一年资金支出 = 每月支出 × 12。
        /// 每月支出 = Σ(在职武将俸禄) + 100 × 俘虏数（见 <c>City.GoldCost</c>）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>年资金支出</returns>
        public static int YearlyGoldCost(City city, Scenario scenario)
        {
            if (city == null || scenario == null)
                return 0;
            return city.GoldCost(scenario) * MonthsPerYear;
        }

        /// <summary>
        /// 城市一年粮草产出 = 每季产出 × 4。
        /// <c>city.totalGainFood</c> 是"每次季度结算的产量"，已含建筑 / 农业 / 基础 / 人口与各项修正。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <returns>年粮产</returns>
        public static int YearlyFoodSupply(City city)
        {
            if (city == null)
                return 0;
            return city.totalGainFood * SeasonsPerYear;
        }

        /// <summary>
        /// 城市一年资金产出 = 每月产出 × 12（<c>city.totalGainGold</c> 为"每次月度结算的产量"）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <returns>年金产</returns>
        public static int YearlyGoldSupply(City city)
        {
            if (city == null)
                return 0;
            return city.totalGainGold * MonthsPerYear;
        }

        /// <summary>
        /// 在建建筑的预期**年**产出（按完工后的满产能估）。
        ///
        /// 【为什么必须单独计这一项】游戏自己的 <c>city.totalGainFood</c> / <c>totalGainGold</c>
        /// **只统计已完工的建筑**（见 ClassicsCityWorking.OnCityCalculateHarvest 里的
        /// <c>isComplate</c> 判定），而一座内政建筑的工期有 5~6 回合 —— 若 AI 不计入在建部分，
        /// 它会在这 6 个回合里**每一回合都判定"还缺粮"**，于是同一类建筑被连建 6 座
        /// （实际踩过：开局一堆农田）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="kind">经济类别（只处理粮 / 金）</param>
        /// <returns>年预期产出</returns>
        public static int PendingYearlyOutput(City city, EconomyKind kind)
        {
            if (city == null || city.allBuildings == null)
                return 0;
            if (kind != EconomyKind.Food && kind != EconomyKind.Gold)
                return 0;

            int perSeason = 0;      // 粮：按季结算
            int perMonth = 0;       // 金：按月结算

            for (int i = 0; i < city.allBuildings.Count; i++)
            {
                Building b = city.allBuildings[i];
                if (b == null || b.BuildingType == null)
                    continue;
                if (b.isComplate)
                    continue;                                   // 已完工的由 totalGain* 负责，避免重复计
                if (KindOf(b.BuildingType.kind) != kind)
                    continue;

                if (kind == EconomyKind.Food)
                    perSeason += b.BuildingType.foodGain;
                else
                    perMonth += b.BuildingType.goldGain;
            }

            return kind == EconomyKind.Food
                ? perSeason * SeasonsPerYear
                : perMonth * MonthsPerYear;
        }

        /// <summary>
        /// 年粮草净收支（正 = 盈余，负 = 亏空）。**含在建建筑的预期产出** ——
        /// 不计入的话，工期内的每一回合都会重复判定"还缺粮"。
        /// </summary>
        public static int YearlyFoodBalance(City city, Scenario scenario)
        {
            return YearlyFoodSupply(city)
                 + PendingYearlyOutput(city, EconomyKind.Food)
                 - YearlyFoodCost(city, scenario);
        }

        /// <summary>
        /// 待建空地 + 待升级建筑的**一次性**建设预算。
        ///
        /// 【为什么必须计入资金需求】建造与升级都要掏钱（<c>City.JobBuildBuilding</c> /
        /// <c>JobUpgradeBuilding</c> 直接扣 <c>city.gold</c>），但这些支出是**一次性**的、
        /// 也不出现在"每月俸禄"那笔固定账里 —— 若不计入，AI 会以为资金有盈余而停止建市场，
        /// 等钱被自建工程吃光之后就再也升不起级。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>建设预算（金）</returns>
        public static int PendingBuildBudget(City city, Scenario scenario)
        {
            if (city == null || scenario == null)
                return 0;

            BuildingPlanWeights w = AIConfig.Instance != null ? AIConfig.Instance.buildingPlan : null;
            int perCell = w != null && w.buildCostEstimate > 0 ? w.buildCostEstimate : 300;

            int budget = 0;

            // 1) 还空着的内政地：每格按平均造价估（将来总要填上）
            int empty = city.InteriorCellCount - city.GetInteriorCellUsedCount();
            if (empty > 0)
                budget += empty * perCell;

            // 2) 还没升到顶的建筑：按 nextId 的真实造价累加（这部分是确定的）
            if (city.allBuildings != null)
            {
                for (int i = 0; i < city.allBuildings.Count; i++)
                {
                    Building b = city.allBuildings[i];
                    if (b == null || b.BuildingType == null)
                        continue;
                    if (!b.isComplate || b.isUpgrading || b.BuildingType.nextId <= 0)
                        continue;

                    BuildingType next = scenario.GetObject<BuildingType>(b.BuildingType.nextId);
                    if (next != null)
                        budget += next.cost;
                }
            }

            return budget;
        }

        /// <summary>
        /// 年化的"建设预留"：把 <see cref="PendingBuildBudget"/> 按分摊年数摊到每年，
        /// 作为资金需求的一部分参与收支判断。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>年化建设预留（金）</returns>
        public static int YearlyBuildReserve(City city, Scenario scenario)
        {
            BuildingPlanWeights w = AIConfig.Instance != null ? AIConfig.Instance.buildingPlan : null;
            int years = w != null && w.buildReserveYears > 0 ? w.buildReserveYears : 2;

            return PendingBuildBudget(city, scenario) / years;
        }

        /// <summary>
        /// 年资金**总需求** = 固定支出（俸禄 + 俘虏）× 12 + 年化建设预留。
        ///
        /// 不把建设预留算进来的后果（实际踩过）：AI 判定"资金有盈余"→ 不建市场 →
        /// 钱被自建工程吃光 → 全部建成农田之后就再也拿不出升级的钱。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>年资金需求</returns>
        public static int YearlyGoldDemand(City city, Scenario scenario)
        {
            return YearlyGoldCost(city, scenario) + YearlyBuildReserve(city, scenario);
        }

        /// <summary>
        /// 年资金净收支（正 = 盈余，负 = 亏空）。**含在建建筑的预期产出**。
        /// </summary>
        public static int YearlyGoldBalance(City city, Scenario scenario)
        {
            return YearlyGoldSupply(city)
                 + PendingYearlyOutput(city, EconomyKind.Gold)
                 - YearlyGoldDemand(city, scenario);
        }

        // ==================== 单座建筑的年度贡献 ====================

        /// <summary>
        /// 单座该建筑的**年**粮草产出。
        ///
        /// 直接用建筑自身的 <c>foodGain</c> 乘结算次数 —— 这会忽略 leftInfluence、势力系数
        /// 等全局乘数，但那些对所有建筑一视同仁，不影响"还需要几座"的比例判断。
        ///
        /// 注意：谷仓这类"乘数型"建筑的 <c>foodGain</c> 只有 1（它不是直接产粮，而是放大农田），
        /// 所以要靠调用方用 <see cref="IsMultiplierBuilding"/> 另行判别，不能拿它算座数。
        /// </summary>
        /// <param name="t">建筑类型</param>
        /// <returns>年粮产</returns>
        public static int YearlyFoodOf(BuildingType t)
        {
            return t != null ? t.foodGain * SeasonsPerYear : 0;
        }

        /// <summary>单座该建筑的**年**资金产出（口径同 <see cref="YearlyFoodOf"/>）。</summary>
        /// <param name="t">建筑类型</param>
        /// <returns>年金产</returns>
        public static int YearlyGoldOf(BuildingType t)
        {
            return t != null ? t.goldGain * MonthsPerYear : 0;
        }

        /// <summary>
        /// 是否为"乘数型"建筑（造币厂 / 谷仓）：它们本身不直接产出，
        /// 而是放大同类基础建筑（市场 / 农田）的效果，数据上用 goldGain/foodGain = 1 表示。
        /// 这类建筑不该用"产出 ÷ 缺口"去估座数，建一座即可。
        /// </summary>
        /// <param name="t">建筑类型</param>
        /// <param name="kind">其经济类别</param>
        /// <returns>是乘数型返回 true</returns>
        public static bool IsMultiplierBuilding(BuildingType t, EconomyKind kind)
        {
            if (t == null)
                return false;
            if (kind == EconomyKind.Food)
                return t.foodGain <= 1;
            if (kind == EconomyKind.Gold)
                return t.goldGain <= 1;
            return false;
        }

        // ==================== 建筑 → 经济类别 ====================

        /// <summary>
        /// 该建筑小类归哪一类经济资源。与建筑规划里的乘子映射保持同一口径。
        /// </summary>
        /// <param name="buildingKind">建筑小类（<c>BuildingKindType</c>）</param>
        /// <returns>经济类别</returns>
        public static EconomyKind KindOf(int buildingKind)
        {
            switch ((BuildingKindType)buildingKind)
            {
                case BuildingKindType.Market:
                case BuildingKindType.Mint:
                case BuildingKindType.BigMarket:
                case BuildingKindType.FishMarket:
                    return EconomyKind.Gold;

                case BuildingKindType.Farm:
                case BuildingKindType.Barn:
                case BuildingKindType.MilitaryGarrison:
                    return EconomyKind.Food;

                case BuildingKindType.BlacksmithShop:
                case BuildingKindType.Stable:
                    return EconomyKind.Arms;

                default:
                    return EconomyKind.None;
            }
        }

        // ==================== 更换的硬约束 ====================

        /// <summary>
        /// 拆掉指定建筑之后，本城的粮 / 金收支是否**仍然不亏空**。
        ///
        /// 【这是更换的硬约束】粮产必须能抵扣城内士兵消耗、资金产出必须覆盖城市消耗
        /// （武将俸禄 + 俘虏关押）。否则"拆农田换锻冶厂"这类操作会把城市拖进饥荒，
        /// 短期看着划算，长期是灾难。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="b">待拆建筑</param>
        /// <returns>拆后仍能维持收支返回 true</returns>
        public static bool CanAffordRemoving(City city, Scenario scenario, Building b)
        {
            if (city == null || scenario == null || b == null || b.BuildingType == null)
                return true;

            EconomyKind kind = KindOf(b.BuildingType.kind);

            if (kind == EconomyKind.Food)
            {
                int supplyAfter = YearlyFoodSupply(city) - YearlyFoodOf(b.BuildingType);
                return supplyAfter >= YearlyFoodCost(city, scenario);
            }

            if (kind == EconomyKind.Gold)
            {
                int supplyAfter = YearlyGoldSupply(city) - YearlyGoldOf(b.BuildingType);
                return supplyAfter >= YearlyGoldDemand(city, scenario);
            }

            // 兵装与其它建筑不参与粮 / 金收支，不受这条约束
            return true;
        }
    }
}
