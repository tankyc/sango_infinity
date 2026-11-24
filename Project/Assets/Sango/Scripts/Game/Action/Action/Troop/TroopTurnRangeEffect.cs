using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 周期范围效果：持技部队在**自己的回合开始时**，对指定范围内的部队逐条施加配置好的效果。
    ///
    /// 【为什么做成"一条 Action 带多组效果"】
    /// 新一批被动里有大量同构需求——"相邻敌部队每回合损失军粮 500""相邻敌部队每回合损兵 500"
    /// "相邻敌部队每回合士气 -15""相邻友军每回合 50% 回复兵力 1000"……
    /// 它们的差别只在"范围 / 敌我 / 数值 / 概率"，骨架完全一样。
    /// 若每条写一个 Action，会有二十多个近重复类；这里收敛成一个数据驱动的执行器，
    /// 与 <see cref="TroopAura"/> / <see cref="BuildingAura"/> 的"配置即效果"思路一致。
    ///
    /// 【时机】订阅 <c>GameEvent.OnTroopTurnStart</c>，且只处理**持技部队自己**的那一次回调。
    /// 部队回合开始由 <c>Force.OnForceTurnStart</c> 逐支部队触发（每势力每回合一次），
    /// 因此"每回合一次"的语义由事件本身保证，不需要额外维护回合标记。
    ///
    /// 【参数】
    /// bound        作用半径（格）。0 = 只作用于自身；1 = 相邻六格；2 = 两格以内。
    /// targetType   作用对象：0 自身 / 1 友军（不含自身） / 2 敌军 / 3 范围内所有（不含自身）
    /// effects      效果列表，见 <see cref="EffectItem"/>
    /// condition    额外条件（以 <see cref="TroopConditionDatabase"/> 为数据库，只读 ActionTroop / ActionPerson）
    ///
    /// 【通用前置开关】（不满足则整条效果不执行）
    /// requireWater        仅在水上时生效（如"锦帆侠影"）
    /// requireSeason       仅指定季节生效（-1 不限；取值见 <see cref="SeasonType"/>）
    /// requireTerrainId    仅站在指定地形上生效（-1 不限；森林 = 6）
    /// requireRegionId     仅站在指定大区生效（-1 不限；西凉 = 3 / 巴蜀 = 5）
    /// requireGovernor     仅主将是所在势力君主时生效
    /// requireNeighborCamp 仅相邻格存在"阵 / 砦 / 城塞"时生效
    /// requireEnemyFire    仅范围内存在**着火的敌军**时生效（如"收聚残兵"）
    /// </summary>
    public class TroopTurnRangeEffect : TroopActionBase
    {
        /// <summary>效果类型：兵力固定值增减（负数为扣兵）</summary>
        public const int EFFECT_TROOPS = 0;
        /// <summary>效果类型：兵力按目标当前兵力百分比增减（value 为百分比，负数为扣兵）</summary>
        public const int EFFECT_TROOPS_PERCENT = 1;
        /// <summary>效果类型：兵粮固定值增减（负数为扣粮）</summary>
        public const int EFFECT_FOOD = 2;
        /// <summary>效果类型：气力固定值增减</summary>
        public const int EFFECT_MORALE = 3;
        /// <summary>效果类型：把气力直接设置为指定值（如"险中出奇"气力固定 50）</summary>
        public const int EFFECT_MORALE_SET = 4;
        /// <summary>效果类型：附加状态（buffId / turns / probability）</summary>
        public const int EFFECT_BUFF = 5;
        /// <summary>效果类型：点火（turns / probability）</summary>
        public const int EFFECT_FIRE = 6;
        /// <summary>效果类型：给目标所属据点增加兵力（用于"收聚残兵"，目标取自身）</summary>
        public const int EFFECT_CITY_TROOPS = 7;

        /// <summary>
        /// 单条效果配置。
        /// </summary>
        public struct EffectItem
        {
            /// <summary>效果类型（EFFECT_*）</summary>
            public int type;
            /// <summary>数值（含义随 type 变化）</summary>
            public int value;
            /// <summary>触发概率（万分比，10000 = 必定；0 视为必定）</summary>
            public int probability;
            /// <summary>状态 Id（EFFECT_BUFF 用）</summary>
            public int buffId;
            /// <summary>持续回合数（EFFECT_BUFF / EFFECT_FIRE 用）</summary>
            public int turns;
            /// <summary>数值是否乘以"势力当前部队数"（"义之所在"用）</summary>
            public bool perForceTroopCount;
        }

        /// <summary>作用半径：0 仅自身 / 1 相邻 / 2 两格</summary>
        int bound;
        /// <summary>作用对象：0 自身 / 1 友军 / 2 敌军 / 3 所有</summary>
        int targetType;
        /// <summary>效果列表</summary>
        readonly List<EffectItem> effects = new List<EffectItem>();
        /// <summary>额外条件</summary>
        Condition condition;

        /// <summary>仅在水上生效</summary>
        bool requireWater;
        /// <summary>仅指定季节生效（-1 不限）</summary>
        int requireSeason = -1;
        /// <summary>仅指定地形生效（-1 不限）</summary>
        int requireTerrainId = -1;
        /// <summary>仅指定大区生效（-1 不限）</summary>
        int requireRegionId = -1;
        /// <summary>仅主将为势力君主时生效</summary>
        bool requireGovernor;
        /// <summary>仅相邻格有"阵 / 砦 / 城塞"时生效</summary>
        bool requireNeighborCamp;
        /// <summary>仅范围内存在着火敌军时生效</summary>
        bool requireEnemyFire;

        /// <summary>收集目标用的复用缓冲（避免每回合 new List）</summary>
        readonly List<Troop> targetBuffer = new List<Troop>(8);

        // ==================== 范围扫描的复用委托 ====================
        //
        // 【为什么不用 lambda】本 Action 在**每回合每人**的回合开始都会被回调，
        // 范围内的每一次扫描若写成捕获局部变量的 lambda，每回合都会 new 一个闭包 + 委托
        // （约 70B/次）。按 20 支持技部队、每回合各扫一圈估算，是纯垃圾的稳定来源。
        // 这里把三种扫描固化成实例方法 + 一次性缓存的委托，扫描上下文走字段传递，
        // 热路径零分配（与项目 GC 规范一致）。

        /// <summary>扫描委托：相邻设施是否为阵 / 砦 / 城塞</summary>
        Action<Cell> campScanAction;
        /// <summary>扫描委托：范围内是否存在着火敌军</summary>
        Action<Cell> fireScanAction;
        /// <summary>扫描委托：收集目标部队</summary>
        Action<Cell> collectScanAction;
        /// <summary>扫描上下文：当前持技部队</summary>
        Troop scanOwner;
        /// <summary>扫描中间结果（存在 / 命中）</summary>
        bool scanFound;

        /// <summary>
        /// 初始化效果参数并订阅部队回合开始事件。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">装配宿主（[0] 部队，[1] 触发装配的武将）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            bound = p.Value<int>("bound");
            targetType = p.Value<int>("targetType");

            requireWater = p.Value<bool>("requireWater");
            requireSeason = p["requireSeason"] != null ? p.Value<int>("requireSeason") : -1;
            requireTerrainId = p["requireTerrainId"] != null ? p.Value<int>("requireTerrainId") : -1;
            requireRegionId = p["requireRegionId"] != null ? p.Value<int>("requireRegionId") : -1;
            requireGovernor = p.Value<bool>("requireGovernor");
            requireNeighborCamp = p.Value<bool>("requireNeighborCamp");
            requireEnemyFire = p.Value<bool>("requireEnemyFire");

            // 委托只在这里创建一次，后续每回合复用
            campScanAction = OnScanCellForCamp;
            fireScanAction = OnScanCellForEnemyFire;
            collectScanAction = OnScanCellForCollect;

            ParseEffects(p);

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null)
                    condition.Init(conObj, sangoObjects);
                else
                    Sango.Log.Error($"周期范围效果的 condition 类未注册：{conObj.Value<string>("class")}，已忽略该条件");
            }

            // 一条效果都没解析出来时直接报错：多半是 JSON 结构写错（如 effects 写成 effect）
            if (effects.Count == 0)
            {
                Sango.Log.Error($"周期范围效果没有任何可用效果配置，特技不会生效：{p}");
                return;
            }

            // 装配宿主既不是部队也不是势力时不订阅事件：
            // 否则 OnTroopTurnStart 会对全场部队逐个结算，变成"全世界都在掉粮"。
            if (Troop == null && Force == null)
            {
                Sango.Log.Error($"周期范围效果的装配宿主既不是部队也不是势力，特技不会生效：{p}");
                return;
            }

            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
        }

        /// <summary>
        /// 解析 effects 数组。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        void ParseEffects(JObject p)
        {
            // 防御：同一实例若被重复 Init（异常路径），不能让效果列表无限累加
            effects.Clear();

            JArray array = p.Value<JArray>("effects");
            if (array == null)
                return;

            for (int i = 0; i < array.Count; i++)
            {
                JObject obj = array[i] as JObject;
                if (obj == null)
                    continue;

                EffectItem item = new EffectItem
                {
                    type = ParseEffectType(obj.Value<string>("type")),
                    value = obj.Value<int>("value"),
                    probability = obj["probability"] != null ? obj.Value<int>("probability") : 0,
                    buffId = obj.Value<int>("buffId"),
                    turns = obj.Value<int>("turns"),
                    perForceTroopCount = obj.Value<bool>("perForceTroopCount"),
                };

                if (item.probability <= 0)
                    item.probability = 10000;

                effects.Add(item);
            }
        }

        /// <summary>
        /// 把字符串效果类型转成内部枚举值。
        /// </summary>
        /// <param name="type">配置里的字符串</param>
        /// <returns>效果类型常量</returns>
        static int ParseEffectType(string type)
        {
            switch (type)
            {
                case "troops": return EFFECT_TROOPS;
                case "troopsPercent": return EFFECT_TROOPS_PERCENT;
                case "food": return EFFECT_FOOD;
                case "morale": return EFFECT_MORALE;
                case "moraleSet": return EFFECT_MORALE_SET;
                case "buff": return EFFECT_BUFF;
                case "fire": return EFFECT_FIRE;
                case "cityTroops": return EFFECT_CITY_TROOPS;
                default:
                    Sango.Log.Error($"周期范围效果出现未知的效果类型：{type}，已按兵力增减处理");
                    return EFFECT_TROOPS;
            }
        }

        /// <summary>
        /// 退订事件。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
        }

        /// <summary>
        /// 事件驱动效果，不需要外部主动触发。
        /// </summary>
        /// <param name="trigger">触发参数（未使用）</param>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>
        /// 部队回合开始：只处理持技部队自己那一次，然后对范围内目标结算效果。
        /// </summary>
        /// <param name="troop">当前回合开始的部队</param>
        /// <param name="scenario">当前剧本</param>
        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (troop == null || !troop.IsAlive || troop.cell == null)
                return;

            // 只处理持技部队自己的回合开始。
            // Troop == null 表示装配在势力（技法）上，此时对势力内每支部队都结算一次。
            if (Troop != null)
            {
                if (Troop != troop)
                    return;
            }
            else if (Force != null && troop.BelongForce != Force)
            {
                return;
            }

            if (!CheckRequirements(troop))
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(troop)))
                return;

            CollectTargets(troop);

            for (int i = 0; i < targetBuffer.Count; i++)
            {
                Troop target = targetBuffer[i];
                if (target == null || !target.IsAlive)
                    continue;

                for (int e = 0; e < effects.Count; e++)
                    ApplyEffect(troop, target, effects[e]);
            }
        }

        /// <summary>
        /// 通用前置开关判定。
        /// </summary>
        /// <param name="troop">持技部队</param>
        /// <returns>全部满足返回 true</returns>
        bool CheckRequirements(Troop troop)
        {
            if (requireWater && !troop.IsInWater)
                return false;

            if (requireSeason >= 0)
            {
                Scenario scenario = Scenario.Cur;
                if (scenario == null || (int)scenario.CurSeason != requireSeason)
                    return false;
            }

            Cell cell = troop.cell;
            if (requireTerrainId >= 0)
            {
                if (cell == null || cell.TerrainType == null || cell.TerrainType.Id != requireTerrainId)
                    return false;
            }

            if (requireRegionId >= 0)
            {
                // 部队所在格归属的城池决定所属大区（Cell.BelongCity 由地图 areaId 解析）
                City areaCity = cell?.BelongCity;
                int regionId = areaCity?.province != null ? areaCity.province.RegionId : -1;
                if (regionId != requireRegionId)
                    return false;
            }

            if (requireGovernor)
            {
                Person leader = troop.Leader;
                Force force = troop.BelongForce;
                if (leader == null || force == null || force.mGovernor != leader)
                    return false;
            }

            if (requireNeighborCamp && !HasNeighborCamp(cell))
                return false;

            if (requireEnemyFire && !HasEnemyFire(troop))
                return false;

            return true;
        }

        /// <summary>
        /// 相邻格是否存在"阵 / 砦 / 城塞"。
        /// 本项目这三种设施共用 <see cref="BuildingKindType.Camp"/>，靠建筑类型 Id 区分等级。
        /// </summary>
        /// <param name="cell">中心格</param>
        /// <returns>存在返回 true</returns>
        bool HasNeighborCamp(Cell cell)
        {
            if (cell == null)
                return false;

            scanFound = false;
            cell.GetNeighbors(campScanAction);
            return scanFound;
        }

        /// <summary>
        /// 相邻设施扫描：命中阵 / 砦 / 城塞即置位。
        /// </summary>
        /// <param name="cell">被扫描的相邻格</param>
        void OnScanCellForCamp(Cell cell)
        {
            if (scanFound || cell == null)
                return;

            BuildingBase building = cell.building;
            if (building != null && building.IsAlive
                && building.BuildingType != null
                && building.BuildingType.kind == (int)BuildingKindType.Camp)
            {
                scanFound = true;
            }
        }

        /// <summary>
        /// 范围内是否存在"正在着火的敌军"。
        /// </summary>
        /// <param name="owner">持技部队</param>
        /// <returns>存在返回 true</returns>
        bool HasEnemyFire(Troop owner)
        {
            if (owner.cell == null)
                return false;

            scanOwner = owner;
            scanFound = false;
            owner.cell.Spiral(bound < 1 ? 1 : bound, fireScanAction);
            scanOwner = null;
            return scanFound;
        }

        /// <summary>
        /// 着火敌军扫描：格子有火且格上有敌方部队即置位。
        /// </summary>
        /// <param name="cell">被扫描的格子</param>
        void OnScanCellForEnemyFire(Cell cell)
        {
            if (scanFound || cell == null || cell.fire == null)
                return;

            Troop other = cell.troop;
            if (other != null && other.IsAlive && scanOwner != null && scanOwner.IsEnemy(other))
                scanFound = true;
        }

        /// <summary>
        /// 按半径与敌我关系收集目标部队。
        /// </summary>
        /// <param name="owner">持技部队</param>
        void CollectTargets(Troop owner)
        {
            targetBuffer.Clear();

            // targetType = 0（自身）时只有自己一个目标。
            // 注意 boundary 仍可能 > 0 —— 那是给 requireEnemyFire 这类"范围侦察"用的检查半径，
            // 不参与目标收集，否则会出现"半径 2 + 只作用自身"时一个目标都收不到的情况。
            if (targetType == 0)
            {
                targetBuffer.Add(owner);
                return;
            }

            if (bound <= 0 || owner.cell == null)
                return;

            scanOwner = owner;
            owner.cell.Spiral(bound, collectScanAction);
            scanOwner = null;
        }

        /// <summary>
        /// 目标收集扫描：格上有存活部队且符合敌我关系时加入缓冲。
        /// </summary>
        /// <param name="cell">被扫描的格子</param>
        void OnScanCellForCollect(Cell cell)
        {
            if (cell == null)
                return;

            Troop other = cell.troop;
            if (other == null || !other.IsAlive || other == scanOwner)
                return;
            if (!MatchTarget(scanOwner, other))
                return;
            if (!targetBuffer.Contains(other))
                targetBuffer.Add(other);
        }

        /// <summary>
        /// 目标是否属于配置的作用对象。
        /// </summary>
        /// <param name="owner">持技部队</param>
        /// <param name="other">候选目标</param>
        /// <returns>命中返回 true</returns>
        bool MatchTarget(Troop owner, Troop other)
        {
            switch (targetType)
            {
                case 0: return false;                                   // 自身：radius 分支已处理
                case 1: return owner.IsSameForce(other);                 // 友军
                case 2: return owner.IsEnemy(other);                     // 敌军
                default: return true;                                    // 所有
            }
        }

        /// <summary>
        /// 对单个目标施加单条效果。
        /// </summary>
        /// <param name="owner">持技部队</param>
        /// <param name="target">目标部队</param>
        /// <param name="item">效果配置</param>
        void ApplyEffect(Troop owner, Troop target, EffectItem item)
        {
            if (item.probability < 10000 && !GameRandom.Chance(item.probability, 10000))
                return;

            switch (item.type)
            {
                case EFFECT_TROOPS:
                    {
                        int num = item.value;
                        // 扣兵前先夹到"最多扣光"：ChangeTroops 只按 troops + num 计算并判定 IsAlive，
                        // 不会把兵力夹到 0，直接把 -500 打在只剩 300 人的部队上会出现负数兵力。
                        if (num < 0 && -num > target.troops)
                            num = -target.troops;

                        if (num != 0)
                            target.ChangeTroops(num, owner, 0);
                        break;
                    }

                case EFFECT_TROOPS_PERCENT:
                    {
                        int num = target.troops * item.value / 100;
                        if (num != 0)
                            target.ChangeTroops(num, owner, 0);
                        break;
                    }

                case EFFECT_FOOD:
                    {
                        if (item.value != 0)
                            target.ChangeFood(item.value, false);
                        break;
                    }

                case EFFECT_MORALE:
                    {
                        int value = item.value;
                        // "义之所在"：回复量 = 势力当前部队数 × value
                        if (item.perForceTroopCount)
                            value *= CountForceTroops(owner.BelongForce);

                        if (value != 0)
                            target.ChangeMorale(value);
                        break;
                    }

                case EFFECT_MORALE_SET:
                    {
                        int delta = item.value - target.morale;
                        if (delta != 0)
                            target.ChangeMorale(delta);
                        break;
                    }

                case EFFECT_BUFF:
                    {
                        if (item.buffId > 0)
                            target.AddBuff(item.buffId, item.turns <= 0 ? 1 : item.turns, owner);
                        break;
                    }

                case EFFECT_FIRE:
                    {
                        IgniteCell(target.cell, owner, item.turns <= 0 ? 1 : item.turns);
                        break;
                    }

                case EFFECT_CITY_TROOPS:
                    {
                        City city = target.BelongCity;
                        if (city != null && city.IsAlive)
                            city.AddTroops(item.value);
                        break;
                    }
            }
        }

        /// <summary>
        /// 统计某势力当前存活的部队数（"义之所在"的气力回复量）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>部队数</returns>
        static int CountForceTroops(Force force)
        {
            if (force == null)
                return 0;

            Scenario scenario = Scenario.Cur;
            if (scenario?.troopsSet == null)
                return 0;

            int count = 0;
            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop != null && troop.IsAlive && troop.BelongForce == force)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 在指定格点起火；已有火焰时只刷新持续回合。
        /// 创建方式与计略效果 SetFire 保持一致（直接点燃视为第 0 代，享受满额蔓延概率）。
        /// </summary>
        /// <param name="cell">目标格</param>
        /// <param name="owner">点火来源部队（用于取智力与显示）</param>
        /// <param name="turns">持续回合数</param>
        public static void IgniteCell(Cell cell, Troop owner, int turns)
        {
            if (cell == null || cell.TerrainType == null || cell.TerrainType.fireRate <= 0)
                return;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            Fire fire = cell.fire;
            if (fire == null)
            {
                fire = new Fire
                {
                    damage = scenario.Variables.baseFireDamage,
                    intelligence = owner != null ? owner.Intelligence : 0,
                    cell = cell,
                    generation = 0,
                    counter = turns
                };
                scenario.Add(fire);
                fire.Init(scenario);
            }
            else
            {
                fire.intelligence = owner != null ? owner.Intelligence : fire.intelligence;
                fire.counter = turns;
                fire.generation = 0;
            }

            cell.fire = fire;
            fire.Action();
        }
    }
}
