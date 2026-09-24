using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 部署系统的运行期状态（**不序列化、不进存档**）。
    ///
    /// 两部分：
    ///   ① 预想值（兵力 / 资金 / 粮草 / 兵装 / 经济系数）—— 让编制抗抖动；
    ///   ② 执行状态（势力回合号 / 在岗指派 / 防抖 / 每回合调动配额）—— Phase B 的稳态维护。
    /// 全部跨剧本复位（由 ScenarioLifecycle 调用 <see cref="Clear"/>）。
    /// </summary>
    public static class DeploymentState
    {
        // ==================== ① 预想值 ====================

        static readonly Dictionary<int, float> troopEma = new Dictionary<int, float>();
        static readonly Dictionary<int, float> goldEma = new Dictionary<int, float>();
        static readonly Dictionary<int, float> foodEma = new Dictionary<int, float>();
        static readonly Dictionary<int, float> weaponEma = new Dictionary<int, float>();
        static readonly Dictionary<int, float> ecoEma = new Dictionary<int, float>();

        // ==================== ② 执行状态 ====================

        static readonly Dictionary<int, int> forceTurn = new Dictionary<int, int>();
        static readonly Dictionary<int, int> personLastTransferTurn = new Dictionary<int, int>();
        static readonly Dictionary<int, int> personAssignCity = new Dictionary<int, int>();
        static readonly Dictionary<int, int> cityRecvCount = new Dictionary<int, int>();
        static readonly Dictionary<int, int> citySendCount = new Dictionary<int, int>();

        /// <summary>本回合真正执行了多少次调动（由 DeploymentSolver 维护，影子模式恒为 0）</summary>
        public static int transferCountThisTurn;

        /// <summary>
        /// 每回合观察一次（兵力 / 资金 / 粮草 / 兵装 / 经济系数）。
        /// 约定：**每城每回合调用一次**（由 DeploymentSolver 在采集城况后调用）。
        /// </summary>
        public static void Observe(City city, DeploymentWeights weights, CitySituation situation)
        {
            if (city == null || weights == null)
                return;

            float troopAlpha = Clamp01(weights.troopObserveAlpha);
            if (troopAlpha <= 0f) troopAlpha = 0.25f;

            Observe(troopEma, city.Id, city.troops, troopAlpha);

            float resAlpha = Clamp01(weights.resourceObserveAlpha);
            if (resAlpha <= 0f) resAlpha = troopAlpha;

            Observe(goldEma, city.Id, city.gold, resAlpha);
            Observe(foodEma, city.Id, city.food, resAlpha);
            Observe(weaponEma, city.Id, situation.weaponCount, resAlpha);

            float eco = CalcEconomyFactor(situation, weights);
            float ecoAlpha = Clamp01(weights.troopEcoAlpha);
            if (ecoAlpha <= 0f) ecoAlpha = troopAlpha;
            Observe(ecoEma, city.Id, eco, ecoAlpha);
        }

        /// <summary>经济系数（0..1）：粮草 / 金钱越充裕，城市越"养得起兵"。</summary>
        public static float CalcEconomyFactor(CitySituation situation, DeploymentWeights weights)
        {
            if (weights == null || !weights.troopEcoAdjust)
                return 1f;

            float factor = weights.troopEcoBase
                         + weights.troopEcoFoodWeight * Clamp01(situation.foodFill)
                         + weights.troopEcoGoldWeight * Clamp01(situation.goldFill);

            if (factor > 1f) factor = 1f;
            if (factor < weights.troopEcoFloor) factor = weights.troopEcoFloor;
            return factor;
        }

        /// <summary>预想兵力（军事编制依据，抗抖动）。</summary>
        public static int GetExpectedTroops(City city, DeploymentWeights weights, CitySituation situation)
        {
            if (city == null)
                return 0;
            if (weights == null)
                return city.troops;

            float limit = city.TroopsLimit;

            float eco;
            if (!ecoEma.TryGetValue(city.Id, out eco))
                eco = CalcEconomyFactor(situation, weights);

            float expected = limit > 0f
                ? limit * Math.Max(0f, weights.troopExpectFill) * eco
                : 0f;

            float ema;
            if (troopEma.TryGetValue(city.Id, out ema) && ema > expected)
                expected = ema;

            if (weights.useCurrentTroopsWhenHigher && city.troops > expected)
                expected = city.troops;

            if (expected < 0f) expected = 0f;
            if (limit > 0f && expected > limit) expected = limit;

            return (int)Math.Ceiling(expected);
        }

        /// <summary>预想资金。</summary>
        public static int GetExpectedGold(City city, DeploymentWeights weights, CitySituation situation)
        {
            if (city == null) return 0;
            if (weights == null) return city.gold;
            return (int)Math.Ceiling(Expected(goldEma, city.Id, city.GoldLimit, weights.goldExpectFill));
        }

        /// <summary>预想粮草。</summary>
        public static int GetExpectedFood(City city, DeploymentWeights weights, CitySituation situation)
        {
            if (city == null) return 0;
            if (weights == null) return city.food;
            return (int)Math.Ceiling(Expected(foodEma, city.Id, city.FoodLimit, weights.foodExpectFill));
        }

        /// <summary>预想兵装。</summary>
        public static int GetExpectedWeapon(City city, DeploymentWeights weights, CitySituation situation)
        {
            if (city == null) return 0;
            float ema;
            if (weaponEma.TryGetValue(city.Id, out ema))
                return (int)Math.Ceiling(ema);
            return Math.Max(0, situation.weaponCount);
        }

        // ==================== ② 执行状态接口 ====================

        /// <summary>
        /// 本次求解的势力与其"本剧本内第几回合"（由 <see cref="DeploymentSolver"/> 在每回合求解前写入）。
        /// 供"前期加成"这类按回合的规则读取：0 = 尚未初始化。
        /// </summary>
        public static int currentForceId;
        /// <summary>当前势力本剧本内的回合号（换剧本自动归零，见 DeploymentSolver 的 turnScenario 判断）</summary>
        public static int currentForceTurn;

        /// <summary>进入某势力的新回合：回合号 +1，并清空"每回合调动配额"。</summary>
        /// <param name="forceId">势力 id</param>
        /// <returns>新的势力回合号</returns>
        public static int NextForceTurn(int forceId)
        {
            int turn;
            forceTurn.TryGetValue(forceId, out turn);
            turn++;
            forceTurn[forceId] = turn;

            // 每回合配额清零（按城统计，键是城 id，跨势力不会冲突）
            cityRecvCount.Clear();
            citySendCount.Clear();
            return turn;
        }

        /// <summary>当前势力回合号（未开始时为 0）。</summary>
        public static int CurrentForceTurn(int forceId)
        {
            int turn;
            forceTurn.TryGetValue(forceId, out turn);
            return turn;
        }

        /// <summary>防抖：该武将在 debounceTurns 个回合内是否还能被调走。</summary>
        public static bool CanTransferNow(int personId, int turn, int debounceTurns)
        {
            if (debounceTurns <= 0)
                return true;

            int last;
            if (!personLastTransferTurn.TryGetValue(personId, out last))
                return true;
            return turn - last >= debounceTurns;
        }

        /// <summary>记录一次成功调动（写防抖时间 + 在岗指派）。</summary>
        public static void MarkTransfer(int personId, int cityId, int turn)
        {
            personLastTransferTurn[personId] = turn;
            personAssignCity[personId] = cityId;
        }

        /// <summary>武将当前指派到的城（0 = 未记录）。</summary>
        public static int GetAssignedCity(int personId)
        {
            int cityId;
            personAssignCity.TryGetValue(personId, out cityId);
            return cityId;
        }

        /// <summary>本回合该城是否还能再接收外调（配额）。</summary>
        public static bool CanReceive(int cityId, int limit)
        {
            if (limit <= 0) return true;
            int count;
            cityRecvCount.TryGetValue(cityId, out count);
            return count < limit;
        }

        /// <summary>记一次接收。</summary>
        public static void MarkReceive(int cityId)
        {
            int count;
            cityRecvCount.TryGetValue(cityId, out count);
            cityRecvCount[cityId] = count + 1;
        }

        /// <summary>本回合该城是否还能再调出人（配额）。</summary>
        public static bool CanSend(int cityId, int limit)
        {
            if (limit <= 0) return true;
            int count;
            citySendCount.TryGetValue(cityId, out count);
            return count < limit;
        }

        /// <summary>记一次调出。</summary>
        public static void MarkSend(int cityId)
        {
            int count;
            citySendCount.TryGetValue(cityId, out count);
            citySendCount[cityId] = count + 1;
        }

        static float Expected(Dictionary<int, float> emaMap, int cityId, int limit, float fill)
        {
            float expected = limit > 0f ? limit * Math.Max(0f, fill) : 0f;
            float ema;
            if (emaMap.TryGetValue(cityId, out ema) && ema > expected)
                expected = ema;
            if (expected < 0f) expected = 0f;
            return expected;
        }

        static void Observe(Dictionary<int, float> emaMap, int cityId, float observed, float alpha)
        {
            float old;
            emaMap[cityId] = emaMap.TryGetValue(cityId, out old)
                ? old + (observed - old) * alpha
                : observed;
        }

        static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        /// <summary>清空运行期状态（剧本收尾时调用）。</summary>
        public static void Clear()
        {
            troopEma.Clear();
            goldEma.Clear();
            foodEma.Clear();
            weaponEma.Clear();
            ecoEma.Clear();

            forceTurn.Clear();
            personLastTransferTurn.Clear();
            personAssignCity.Clear();
            cityRecvCount.Clear();
            citySendCount.Clear();
        }
    }
}
