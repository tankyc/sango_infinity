using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 宝物 / 装备的产出规则（对应"探索人才"的第四个结果）。
    ///
    /// 【产出位置】探索人才结算 <see cref="City.DoJobSearching"/> 中，人才判定之后、金钱判定之前；
    /// 命中后本次探索即结束（不再判金钱），产出物**直接进势力国库**
    /// （<see cref="Force.Treasury"/>），而不是留在城里 —— 珍品是势力级资源。
    ///
    /// 【概率】受执行武将属性影响，政治为主、智力为辅：
    ///     装备概率(%) = 基础 + 政治 × 政治折算 / 100 + 智力 × 智力折算 / 100（封顶）
    ///                 + 全局搜索加成（封顶之后加，不受上限约束）
    ///     宝物概率(%) = 装备概率 × 宝物比例 / 100
    ///   默认值下 政治 90 / 智力 80 约为 15%。所有参数取自剧本设置
    ///   （<see cref="ScenarioVariables.itemDiscoverGain"/>），代码侧不硬编码。
    ///   <see cref="GainPlace.ItemDiscoverGlobalBonus"/> 默认 0，是留给测试与 Mod 的全局旋钮。
    ///
    ///   注意判定顺序：先判装备，装备落空才判宝物。所以把全局加成顶到 100 时装备必中，
    ///   宝物线就不会被走到 —— 要单独验证宝物产出，需先把装备类道具的
    ///   discoverProbability 归零（或用一个中间值让两条线都有机会）。
    ///
    /// 【产出限定】道具可以用 <see cref="ItemType.discoverCityIds"/> /
    ///   <see cref="ItemType.discoverProvinceIds"/> 把自己限定在特定城市或州才能被搜出
    ///   （两个都空 = 不限，也就是默认形态）。
    ///   判定发生在候选池筛选阶段（<see cref="RollItem"/>）——"限定后池子为空"只表现为本次不产出，
    ///   不会去动冷却 / 累计上限这些状态。
    ///
    /// 【五层限流】逐层拦截，任何一层不通过就本次不产出：
    ///     ① 开局保护：前 N 回合不产出，避免开局爆装备
    ///     ② 势力冷却：同势力每 N 回合最多 1 件
    ///     ③ 城市冷却：同城市每 N 回合最多 1 件
    ///     ④ 势力总量：累计上限 = 固定值 + 历史峰值城数 × 每城增量（掉城不回收额度）
    ///     ⑤ 单件唯一：每件道具本局只产出一次（<see cref="Force.discoveredItemIds"/>）
    ///   另外国库已满时同样不产出（判在概率之前，避免玩家"白跑一趟"）。
    ///   注：国库容量 v1 暂不设上限（策划案 4.2，<see cref="Force.TreasuryLimit"/> 为 0 即不限），
    ///   此时 <see cref="Force.CanAddTreasury"/> 恒为 true，这一条等同于失效。
    ///
    /// 【节奏】回合制：每人每回合最多一个行为、一年 36 回合。
    ///   一个 8 城势力约 3 人常驻搜索 ⇒ 约 60 次/年；单次命中率 = 装备线（默认 10~15%）
    ///   + 宝物线（装备概率 × 60%）≈ 16~23%，即每年 6~9 次命中。
    ///   随后被三层闸门依次截断：
    ///     ② 势力冷却 12 回合 ⇒ 势力 3 件/年；
    ///     ③ 城市冷却 18 回合 ⇒ 单城 2 件/年（搜索分散在多城时不起作用）；
    ///     ④ 累计上限 10 + 峰值城数 × 3 ⇒ 3 城 19 / 8 城 34 / 15 城 55 件。
    ///   P6 回归仿真（20 年、720 回合，跑的就是本类的判定顺序）：
    ///     3 城 ≈ 0.95 件/年（19 件）、8 城 ≈ 1.7（34 件）、15 城 ≈ 1.9~2.7（36~55 件），
    ///   与策划案 3.5 的 1 / 1.5~2 / 2~3 件/年一致。
    /// </summary>
    public static class ForceItemDiscover
    {
        /// <summary>
        /// 探索人才时尝试产出珍品。命中则已完成入库与状态更新。
        /// </summary>
        /// <param name="city">执行探索的城市</param>
        /// <param name="person">执行探索的武将</param>
        /// <returns>产出的道具；未产出返回 null</returns>
        public static ItemType TryDiscover(City city, Person person)
        {
            if (city == null || person == null)
                return null;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null)
                return null;

            Force force = city.BelongForce;
            if (force == null)
                return null;

            // 五层限流：任一不通过都不产出（限流判在概率之前，不浪费玩家的探索机会）
            if (!CanDiscover(city, force))
                return null;

            // 装备与宝物分两条独立判定线：先判装备，装备落空再判宝物。
            // 这样两种供给可以各自用冷却参数调节，也避免"一次探索同时给两件"。
            ItemType item = RollItem(force, city, (int)ItemKindType.Equipment, CalcChance(person, false));
            if (item == null)
                item = RollItem(force, city, (int)ItemKindType.Treasure, CalcChance(person, true));

            if (item == null)
                return null;

            Commit(force, city, item);
            return item;
        }

        /// <summary>
        /// 计算发现概率（百分比，与 <see cref="GameRandom.Chance"/> 口径一致）
        /// </summary>
        /// <param name="person">执行探索的武将</param>
        /// <param name="treasure">true = 算宝物概率，false = 算装备概率</param>
        /// <returns>百分比概率，已按上限封顶</returns>
        public static int CalcChance(Person person, bool treasure)
        {
            if (person == null)
                return 0;

            int chance = GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverBaseChance)
                       + GainValueConfig.Rate(person.Politics,
                            GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverFromPolitics))
                       + GainValueConfig.Rate(person.Intelligence,
                            GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverFromIntelligence));

            int cap = GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverChanceCap);
            if (chance > cap)
                chance = cap;

            // 全局搜索加成：**加在封顶之后**，因此不受 ItemDiscoverChanceCap 约束。
            // 默认 0（正常剧本无感）；测试时设 100 即必中，Mod 也可用它整体调节产出率。
            // 注意：它只解决"概率是否命中"，五层限流仍在（见 CanDiscover）。
            chance += GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverGlobalBonus);

            if (treasure)
            {
                chance = GainValueConfig.Rate(chance,
                    GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverTreasureRate));
            }

            return chance < 0 ? 0 : chance;
        }

        /// <summary>
        /// 检查五层限流与国库容量是否允许本次产出
        /// </summary>
        /// <param name="city">执行探索的城市</param>
        /// <param name="force">所属势力</param>
        /// <returns>允许产出返回 true</returns>
        public static bool CanDiscover(City city, Force force)
        {
            Scenario scenario = Scenario.Cur;
            if (city == null || force == null || scenario == null)
                return false;

            // ① 开局保护
            int turn = scenario.TurnCount;
            if (turn < GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverStartProtect))
                return false;

            // ② 势力冷却
            int forceCooldown = GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverForceCooldown);
            if (force.lastItemDiscoverTurn > 0 && turn - force.lastItemDiscoverTurn < forceCooldown)
                return false;

            // ③ 城市冷却
            int cityCooldown = GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverCityCooldown);
            if (city.lastItemDiscoverTurn > 0 && turn - city.lastItemDiscoverTurn < cityCooldown)
                return false;

            // ④ 势力累计上限（按历史峰值城数算，掉城不回收额度）
            int maxTotal = GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverMaxBase)
                         + force.peakCityCount * GainValueConfig.ItemDiscover(GainPlace.ItemDiscoverMaxPerCity);
            if (force.discoveredItemIds != null && force.discoveredItemIds.Count >= maxTotal)
                return false;

            // 国库已满：不产出（否则道具会凭空消失）
            // 不设上限时 CanAddTreasury 恒为 true，本行不构成拦截（策划案 4.2）
            return force.CanAddTreasury();
        }

        /// <summary>
        /// 在指定大类（装备 / 宝物）的候选池里按发现权重加权抽选。
        /// 候选池 = 该大类中 权重 &gt; 0、本局尚未产出过、且**产出限定允许本城**的道具。
        /// </summary>
        /// <param name="force">所属势力（用于排除已产出道具）</param>
        /// <param name="city">执行搜索的城市（用于产出限定城市 / 州的筛选）</param>
        /// <param name="kind">道具大类（<see cref="ItemKindType.Equipment"/> / <see cref="ItemKindType.Treasure"/>）</param>
        /// <param name="chance">命中概率（百分比），先判概率再抽池</param>
        /// <returns>抽中的道具；未命中或池为空返回 null</returns>
        static ItemType RollItem(Force force, City city, int kind, int chance)
        {
            if (chance <= 0 || !GameRandom.Chance(chance))
                return null;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null)
                return null;

            List<ItemType> pool = new List<ItemType>();
            int totalWeight = 0;

            scenario.CommonData.ItemTypes.ForEach(item =>
            {
                if (item == null || item.kind != kind)
                    return;

                // 权重 0 = 不可通过搜索获得
                if (item.discoverProbability <= 0)
                    return;

                // ⑤ 单件唯一
                if (force.discoveredItemIds != null && force.discoveredItemIds.Contains(item.Id))
                    return;

                // 产出限定城市 / 州：两个列表都没配就是不限（见 ItemType.CanDiscoverAt）
                if (!item.CanDiscoverAt(city))
                    return;

                pool.Add(item);
                totalWeight += item.discoverProbability;
            });

            if (pool.Count == 0 || totalWeight <= 0)
                return null;

            int roll = GameRandom.Range(0, totalWeight);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].discoverProbability;
                if (roll < 0)
                    return pool[i];
            }

            return pool[pool.Count - 1];
        }

        /// <summary>
        /// 产出落地：记录冷却回合与已产出清单，并把道具放进势力国库
        /// </summary>
        static void Commit(Force force, City city, ItemType item)
        {
            int turn = Scenario.Cur.TurnCount;

            if (force.discoveredItemIds == null)
                force.discoveredItemIds = new List<int>();

            if (!force.discoveredItemIds.Contains(item.Id))
                force.discoveredItemIds.Add(item.Id);

            force.lastItemDiscoverTurn = turn;
            city.lastItemDiscoverTurn = turn;

            force.AddTreasury(item, 1);

            Sango.Log.Info($"@内政@[{force.Name}]<{city.Name}>产出了{item.Name}（累计 {force.discoveredItemIds.Count} 件）");
        }
    }
}
