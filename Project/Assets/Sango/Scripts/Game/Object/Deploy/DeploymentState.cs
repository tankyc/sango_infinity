using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 部署系统的运行期状态（**不序列化、不进存档**）。
    ///
    /// 三部分：
    ///   ① 预想值（兵力 / 资金 / 粮草 / 兵装 / 经济系数）—— 让编制抗抖动；
    ///   ② **调度作用域(scope)运行状态** —— 势力回合号 / 每回合城配额 / 作用域级调动额度 / 防重入标记；
    ///   ③ 武将防抖（按人记录，**全作用域共享**：同一个人被任一军团调走后，本回合内谁都不能再动他）。
    ///
    /// 【为什么需要作用域】人才部署有两种调度单位（两套边界）：
    ///   · **势力级**（`scope == null`）—— **非玩家（AI）势力**：目标城与抽调池都是全势力，
    ///     势力内可跨军团调人；
    ///   · **军团级** —— **玩家军团**：目标城与抽调池都严格限于本军团内，绝不跨军团调人
    ///     （跨团会打乱玩家自己排好的部署）；"人才"委任开关也按军团生效；
    ///     第一军团（君主所在、玩家直辖）只出"仅参考"报告，不下发调动令。
    /// 作用域键由 <see cref="ScopeKey"/> 生成：军团级取正、势力级取负，避免两类 id 撞键。
    ///
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

        // ==================== ② 调度作用域运行状态 ====================

        /// <summary>
        /// 单个调度作用域（势力或军团）的运行状态。
        /// 配额与额度**随势力回合号变化自动重置**，因此不同军团之间不会互相清空。
        /// </summary>
        class ScopeRuntime
        {
            /// <summary>最近一次调度所属的势力回合号（变化 = 进入新回合 → 配额清零）</summary>
            public int turn;
            /// <summary>最近一次真正完成调度的势力回合号（防重入；int.MinValue = 从未调度）</summary>
            public int deployedTurn = int.MinValue;
            /// <summary>本回合各城已接收人数（城 id → 人数）</summary>
            public Dictionary<int, int> recv;
            /// <summary>本回合各城已调出人数（城 id → 人数）</summary>
            public Dictionary<int, int> send;
            /// <summary>本回合已消耗的作用域级调动总额度</summary>
            public int transferTotal;
        }

        static readonly Dictionary<int, ScopeRuntime> scopeRuntime = new Dictionary<int, ScopeRuntime>();
        static readonly Dictionary<int, int> forceTurn = new Dictionary<int, int>();
        static readonly Dictionary<int, int> personLastTransferTurn = new Dictionary<int, int>();

        /// <summary>本回合该作用域真正执行了多少次调动（每次 Solve 开始时重置，影子模式恒为 0）</summary>
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

        // ==================== ② 调度作用域接口 ====================

        /// <summary>
        /// 当前**调度作用域**所在势力的回合号（由 <see cref="DeploymentSolver"/> 在求解前写入，
        /// 供"前期加成"这类按回合的规则读取：0 = 尚未初始化）。
        /// 同一势力的所有军团共享同一个回合号 —— 城市 AI（CityAI / CityAIOrderPlanner）也用这一口径。
        /// </summary>
        public static int currentForceTurn;

        /// <summary>
        /// 调度作用域键：军团级 = +corpsId；势力级 = −forceId。
        /// 两类 id 都从 1 开始，必须分编码空间，否则"势力 1"与"军团 1"会撞键。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="corps">军团（null = 势力级作用域）</param>
        public static int ScopeKey(Force force, Corps corps)
        {
            if (corps != null) return corps.Id;
            return force != null ? -force.Id : 0;
        }

        /// <summary>
        /// 进入势力的新回合：回合号 +1。
        /// 约定由 <c>Force.OnForceTurnStart</c> **每回合调用一次**（而不是在 Solve 里自增）——
        /// 否则一个势力有多个军团时，回合号会被"每个军团各加一次"，防抖与前期加成的标度全部失真。
        /// </summary>
        /// <param name="forceId">势力 id</param>
        /// <returns>新的势力回合号</returns>
        public static int BeginForceTurn(int forceId)
        {
            int turn;
            forceTurn.TryGetValue(forceId, out turn);
            turn++;
            forceTurn[forceId] = turn;
            return turn;
        }

        /// <summary>势力当前回合号（未开始时为 0）。</summary>
        public static int CurrentForceTurn(int forceId)
        {
            int turn;
            forceTurn.TryGetValue(forceId, out turn);
            return turn;
        }

        /// <summary>取作用域运行状态；进入新的势力回合时自动清零该作用域的配额与额度。</summary>
        static ScopeRuntime GetRuntime(int scopeKey, int turn)
        {
            ScopeRuntime rt;
            if (!scopeRuntime.TryGetValue(scopeKey, out rt))
            {
                rt = new ScopeRuntime();
                scopeRuntime[scopeKey] = rt;
            }

            if (rt.turn != turn)
            {
                rt.turn = turn;
                rt.recv = null;
                rt.send = null;
                rt.transferTotal = 0;
            }
            return rt;
        }

        /// <summary>
        /// 尝试开始"本回合该作用域"的调度（防重入）。
        ///
        /// 必要性：玩家势力的 <c>Force.Run</c> 在等待玩家操作时会返回 false 并被**反复调用**，
        /// 若不设标记，同一回合会对同一军团反复调人。返回 true 表示本次是第一次。
        /// </summary>
        /// <param name="scopeKey">作用域键</param>
        /// <param name="turn">当前势力回合号</param>
        public static bool TryBeginScopeDeploy(int scopeKey, int turn)
        {
            ScopeRuntime rt = GetRuntime(scopeKey, turn);
            if (rt.deployedTurn == turn)
                return false;
            rt.deployedTurn = turn;
            return true;
        }

        /// <summary>防抖：该武将在 debounceTurns 个回合内是否还能被调走（跨作用域共享）。</summary>
        public static bool CanTransferNow(int personId, int turn, int debounceTurns)
        {
            if (debounceTurns <= 0)
                return true;

            int last;
            if (!personLastTransferTurn.TryGetValue(personId, out last))
                return true;
            return turn - last >= debounceTurns;
        }

        /// <summary>记录一次成功调动（写防抖时间）。</summary>
        /// <param name="personId">武将 id</param>
        /// <param name="turn">当前势力回合号</param>
        public static void MarkTransfer(int personId, int turn)
        {
            personLastTransferTurn[personId] = turn;
        }

        /// <summary>本回合该作用域的调动总额度是否还有剩余（≤0 = 不限制）。</summary>
        public static bool CanTransferGlobal(int scopeKey, int turn, int limit)
        {
            if (limit <= 0) return true;
            return GetRuntime(scopeKey, turn).transferTotal < limit;
        }

        /// <summary>记一次作用域级调动额度消耗。</summary>
        public static void MarkTransferGlobal(int scopeKey, int turn)
        {
            GetRuntime(scopeKey, turn).transferTotal++;
        }

        /// <summary>本回合该城是否还能再接收外调（配额）。</summary>
        public static bool CanReceive(int scopeKey, int turn, int cityId, int limit)
        {
            if (limit <= 0) return true;
            ScopeRuntime rt = GetRuntime(scopeKey, turn);
            if (rt.recv == null) return true;
            int count;
            rt.recv.TryGetValue(cityId, out count);
            return count < limit;
        }

        /// <summary>记一次接收。</summary>
        public static void MarkReceive(int scopeKey, int turn, int cityId)
        {
            ScopeRuntime rt = GetRuntime(scopeKey, turn);
            if (rt.recv == null) rt.recv = new Dictionary<int, int>();
            int count;
            rt.recv.TryGetValue(cityId, out count);
            rt.recv[cityId] = count + 1;
        }

        /// <summary>本回合该城是否还能再调出人（配额）。</summary>
        public static bool CanSend(int scopeKey, int turn, int cityId, int limit)
        {
            if (limit <= 0) return true;
            ScopeRuntime rt = GetRuntime(scopeKey, turn);
            if (rt.send == null) return true;
            int count;
            rt.send.TryGetValue(cityId, out count);
            return count < limit;
        }

        /// <summary>记一次调出。</summary>
        public static void MarkSend(int scopeKey, int turn, int cityId)
        {
            ScopeRuntime rt = GetRuntime(scopeKey, turn);
            if (rt.send == null) rt.send = new Dictionary<int, int>();
            int count;
            rt.send.TryGetValue(cityId, out count);
            rt.send[cityId] = count + 1;
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

            scopeRuntime.Clear();
            forceTurn.Clear();
            personLastTransferTurn.Clear();
            transferCountThisTurn = 0;
            currentForceTurn = 0;
        }
    }
}
