using System;
using System.Collections.Generic;
using Sango.Core.Tools;

namespace Sango.Core
{
    /// <summary>
    /// 武将属性光环系统（全局规则，不依赖特技挂载）。
    ///
    /// 【设计口径】（2026-11-23 与策划确认）
    ///   1. 光环按"每名出战武将"独立判定：主将 / 副将各自用自己的当前属性开自己的光环，
    ///      不会把不同人的最高属性拼成一套；
    ///   2. 统率光环是总开关：本人统率 ≥90（或持「武干」）才开；其余附加光环
    ///      还要求本人统率 ≥90；
    ///   3. 攻防两项在半径内覆盖同势力部队（含来源本队），同项取最高值；
    ///      会心 / 暴伤 / 计策 / 识破 / 助攻 / 气力只在来源本队结算；
    ///   4. 武干：统率不足 90 时按 90 档强行开启 1 格；统率 ≥90 时正常半径 +1 格；
    ///   5. 数值 = clamp(基础 + floor((属性-阈值)/步进)*每档, 下限, 上限)，
    ///      封顶后再乘军魂系数（默认 150%）并向下取整。
    ///
    /// 【为什么新增本类而不是扩展 TroopAura】
    ///   旧 <c>TroopAura</c> 是"特技装配的静态效果"：value / bound 只从 JSON 读一次，
    ///   没有属性公式、没有分档、也没有"按武将分别判定"的概念。本类把数据、公式、
    ///   聚合与重算时机统一收口，旧类保持不动，避免影响既有配置。
    ///
    /// 【重算时机】
    ///   · 部队属性重算（OnTroopCalculateAttribute）—— 属性变化会改数值与半径；
    ///   · 部队进入新格子（OnTroopEnterCell）—— 旧位置覆盖的部队失去光环，新位置获得；
    ///   · 剧本初始化（Init）—— 全量补算一次；
    ///   · 部队收尾（OnTroopClear）—— 释放缓存。
    /// </summary>
    public static class AuraSystem
    {
        /// <summary>
        /// 单个武将的一条属性光环（已按公式算好，可直接参与聚合）。
        /// </summary>
        sealed class AuraSource
        {
            /// <summary>光环来源武将</summary>
            public Person person;
            /// <summary>光环半径（格）；0 表示没有范围效果</summary>
            public int range;
            /// <summary>攻击力加成</summary>
            public int attack;
            /// <summary>防御力加成</summary>
            public int defence;
            /// <summary>物理战法会心率（百分点）</summary>
            public int criticalRate;
            /// <summary>暴击倍率加成（百分点）</summary>
            public int criticalDamage;
            /// <summary>计策成功率（百分点）</summary>
            public int successRate;
            /// <summary>计策识破率（百分点）</summary>
            public int resistRate;
            /// <summary>辅助攻击概率（百分点）</summary>
            public int assistRate;
            /// <summary>每回合气力回复（点）</summary>
            public int moraleRecover;

            /// <summary>是否有范围效果（攻防 / 辅助攻击概率）</summary>
            public bool HasAreaEffect => range > 0 && (attack != 0 || defence != 0 || assistRate != 0);
        }

        /// <summary>
        /// 一支部队当前汇总后的光环结果。
        /// </summary>
        sealed class TroopAuraResult
        {
            public int attack;
            public int defence;
            public int criticalRate;
            public int criticalDamage;
            public int successRate;
            public int resistRate;
            public int assistRate;
            public int moraleRecover;

            public void Reset()
            {
                attack = 0;
                defence = 0;
                criticalRate = 0;
                criticalDamage = 0;
                successRate = 0;
                resistRate = 0;
                assistRate = 0;
                moraleRecover = 0;
            }
        }

        /// <summary>是否已初始化（未进剧本时所有回调空转）</summary>
        static bool inited;

        /// <summary>当前剧本（配置与全量重算用）</summary>
        static Scenario curScenario;

        /// <summary>光环配置（统率 / 武力 / 智力 / 政治 / 魅力）</summary>
        static readonly List<AuraConfig> configs = new List<AuraConfig>();

        /// <summary>
        /// 全局光环半径分档表。
        ///
        /// 【口径】设定表的"光环范围规则（由统率决定）"适用于全部光环：
        /// 武力 / 智力 / 政治 / 魅力光环不单独配半径，统一按本人统率取格数。
        /// 这里在 Init 时把统率光环的 RangeTiers 复制一份，避免直接引用配置对象。
        /// </summary>
        static readonly List<AuraRangeTier> globalRangeTiers = new List<AuraRangeTier>();

        /// <summary>武将 → 他本人提供的全部光环（缓存，属性变化时作废）</summary>
        static readonly Dictionary<Person, List<AuraSource>> personSources = new Dictionary<Person, List<AuraSource>>();

        /// <summary>部队 → 聚合结果（攻击 / 防御 / 会心 / 暴伤 / 计策 / 助攻 / 气力）</summary>
        static readonly Dictionary<Troop, TroopAuraResult> troopResults = new Dictionary<Troop, TroopAuraResult>();

        /// <summary>军魂特技 Id（0 = 不启用）</summary>
        static int moraleSoulFeatureId;

        /// <summary>军魂系数（百分比，150 = ×1.5）</summary>
        static int moraleSoulFactor = 100;

        /// <summary>重算缓冲：待重算部队</summary>
        static readonly HashSet<Troop> dirtyTroops = new HashSet<Troop>();
        /// <summary>重算缓冲：待重算部队的遍历副本</summary>
        static readonly List<Troop> dirtyList = new List<Troop>();

        /// <summary>范围收集：目标集合（与 collectAction 配套，避免每次分配闭包）</summary>
        static HashSet<Troop> collectTarget;
        /// <summary>范围收集：格子回调（静态复用）</summary>
        static readonly Action<Cell> collectAction = CollectCell;

        /// <summary>攻防聚合：当前受益部队（与 accumulateAction 配套）</summary>
        static Troop accumulateTarget;
        /// <summary>攻防聚合：当前结果槽（与 accumulateAction 配套）</summary>
        static TroopAuraResult accumulateResult;
        /// <summary>攻防聚合：部队遍历回调（静态复用，避免每次 RecalcTroop 分配闭包）</summary>
        static readonly Action<Troop> accumulateAction = AccumulateAreaCallback;

        /// <summary>
        /// 进入剧本时初始化：读取配置、订阅事件、全量补算一次。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public static void Init(Scenario scenario)
        {
            // 先退订上一次的订阅，防止跨剧本累积
            Shutdown();

            curScenario = scenario;
            configs.Clear();
            moraleSoulFeatureId = 0;
            moraleSoulFactor = 100;

            if (scenario == null || scenario.CommonData == null)
            {
                Sango.Log.Error("*[光环]剧本通用数据为空，光环系统不启用");
                return;
            }

            if (scenario.CommonData.AuraConfigs != null)
            {
                scenario.CommonData.AuraConfigs.ForEach(c =>
                {
                    if (c != null && c.Effects != null && c.Effects.Count > 0)
                        configs.Add(c);
                });
            }

            // 【范围表】取"统率光环"（RequireCommandForOthers = true）的 RangeTiers 作为全局半径表；
            // 找不到时回退到第一条带 RangeTiers 的配置，避免配置顺序变化导致全部光环失去范围。
            globalRangeTiers.Clear();
            for (int i = 0; i < configs.Count; i++)
            {
                AuraConfig c = configs[i];
                if (c != null && c.RequireCommandForOthers && c.RangeTiers != null && c.RangeTiers.Count > 0)
                {
                    for (int j = 0; j < c.RangeTiers.Count; j++)
                        if (c.RangeTiers[j] != null) globalRangeTiers.Add(c.RangeTiers[j]);
                    break;
                }
            }
            if (globalRangeTiers.Count == 0)
            {
                for (int i = 0; i < configs.Count; i++)
                {
                    AuraConfig c = configs[i];
                    if (c != null && c.RangeTiers != null && c.RangeTiers.Count > 0)
                    {
                        for (int j = 0; j < c.RangeTiers.Count; j++)
                            if (c.RangeTiers[j] != null) globalRangeTiers.Add(c.RangeTiers[j]);
                        break;
                    }
                }
            }

            AuraGlobals globals = scenario.CommonData.AuraGlobals;
            if (globals != null)
            {
                moraleSoulFeatureId = globals.MoraleSoulFeatureId;
                if (globals.MoraleSoulFactor > 0)
                    moraleSoulFactor = globals.MoraleSoulFactor;
            }

            inited = true;

            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
            GameEvent.OnTroopEnterCell += OnTroopEnterCell;
            GameEvent.OnTroopClear += OnTroopClear;
            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
            GameEvent.OnTroopAfterCalculateSkillCritical += OnTroopAfterCalculateSkillCritical;
            GameEvent.OnTroopCalculateSkillCriticalFactor += OnTroopCalculateSkillCriticalFactor;
            GameEvent.OnTroopAfterCalculateSkillSuccess += OnTroopAfterCalculateSkillSuccess;
            GameEvent.OnGameShutdown += Shutdown;

            if (configs.Count == 0)
            {
                Sango.Log.Error("*[光环]未读到任何光环配置（AuraConfigs.json），武将属性光环不生效");
            }
            else
            {
                Sango.Log.Info($"*[光环]已加载 {configs.Count} 条属性光环配置");
            }

            // 剧本初始化阶段，部队可能已经全部建好（也可能还没建），统一补算一次；
            // 后续新建部队会走 OnTroopEnterCell（Troop.Init 内抛出）自动挂接。
            if (scenario.troopsSet != null)
            {
                scenario.troopsSet.ForEach(t =>
                {
                    if (t == null) return;
                    InvalidateTroopPersons(t);
                    RecalcTroop(t);
                });
            }
        }

        /// <summary>
        /// 退订事件并清空缓存（换剧本 / 关游戏时调用）。
        /// </summary>
        public static void Shutdown()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
            GameEvent.OnTroopEnterCell -= OnTroopEnterCell;
            GameEvent.OnTroopClear -= OnTroopClear;
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
            GameEvent.OnTroopAfterCalculateSkillCritical -= OnTroopAfterCalculateSkillCritical;
            GameEvent.OnTroopCalculateSkillCriticalFactor -= OnTroopCalculateSkillCriticalFactor;
            GameEvent.OnTroopAfterCalculateSkillSuccess -= OnTroopAfterCalculateSkillSuccess;
            GameEvent.OnGameShutdown -= Shutdown;

            inited = false;
            curScenario = null;
            configs.Clear();
            globalRangeTiers.Clear();
            personSources.Clear();
            troopResults.Clear();
            dirtyTroops.Clear();
            dirtyList.Clear();
            collectTarget = null;
            moraleSoulFeatureId = 0;
            moraleSoulFactor = 100;
        }

        /// <summary>
        /// 取得指定部队的光环聚合结果；缓存缺失时立即补算。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <returns>聚合结果（无数据时返回 null）</returns>
        static TroopAuraResult GetResultFor(Troop troop)
        {
            if (troop == null || !inited)
                return null;
            if (!troopResults.TryGetValue(troop, out TroopAuraResult result))
            {
                RecalcTroop(troop);
                troopResults.TryGetValue(troop, out result);
            }
            return result;
        }

        /// <summary>取武将的判定属性（0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力）</summary>
        static int GetStat(Person person, int statType)
        {
            switch (statType)
            {
                case 0: return person.Command;
                case 1: return person.Strength;
                case 2: return person.Intelligence;
                case 3: return person.Politics;
                case 4: return person.Glamour;
                default: return 0;
            }
        }

        /// <summary>作废整支部队所有武将的光环缓存（属性 / 编制变化时调用）</summary>
        static void InvalidateTroopPersons(Troop troop)
        {
            if (troop == null) return;
            if (troop.Leader != null) personSources.Remove(troop.Leader);
            if (troop.Member1 != null) personSources.Remove(troop.Member1);
            if (troop.Member2 != null) personSources.Remove(troop.Member2);
        }

        /// <summary>取得（或建立）武将的光环列表</summary>
        static List<AuraSource> EnsurePersonSources(Person person)
        {
            if (person == null || person.IsDead)
                return null;
            if (personSources.TryGetValue(person, out List<AuraSource> list))
                return list;

            list = new List<AuraSource>(configs.Count);
            for (int i = 0; i < configs.Count; i++)
            {
                AuraSource src = BuildSource(person, configs[i]);
                if (src != null)
                    list.Add(src);
            }
            personSources[person] = list;
            return list;
        }

        /// <summary>
        /// 按配置与武将属性构建一条光环源；未开启返回 null。
        /// </summary>
        static AuraSource BuildSource(Person person, AuraConfig config)
        {
            if (config == null || config.Effects == null || config.Effects.Count == 0)
                return null;

            int stat = GetStat(person, config.StatType);
            bool forceEnabled = false;
            bool enabled;

            if (config.RequireCommandForOthers)
            {
                // 统率光环：本人属性达标即开启
                enabled = stat >= config.Threshold;
                if (!enabled && config.ForceEnableFeatureId > 0 && person.HasFeature(config.ForceEnableFeatureId))
                {
                    // 武干强行开启：只开统率光环，数值按阈值档起算
                    enabled = true;
                    forceEnabled = true;
                }
            }
            else
            {
                // 附加光环：本人属性达标 且 本人统率达到 90（武干强开不带动附加光环）
                enabled = stat >= config.Threshold && person.Command >= config.CommandRequire;
            }

            if (!enabled)
                return null;

            int calcStat = forceEnabled ? config.Threshold : stat;

            // 【范围统一由统率决定】设定表的"光环范围规则（由统率决定）"适用于全部光环：
            // 武力 / 智力 / 政治 / 魅力光环不单独配半径，统一按本人统率取格数。
            int range;
            if (forceEnabled)
            {
                // 武干强行开启固定 1 格（不再叠加武干的 +1）
                range = 1;
            }
            else
            {
                range = CalcRange(globalRangeTiers, person.Command);
                if (range <= 0)
                    range = CalcRange(config.RangeTiers, person.Command);
                // 武干：统率已满 90 时在正常半径上 +1 格（只对统率光环配置了该特技）
                if (config.RangeFeatureId > 0 && config.RangeFeatureAdd > 0
                    && person.HasFeature(config.RangeFeatureId))
                {
                    range += config.RangeFeatureAdd;
                }
            }

            bool hasMoraleSoul = moraleSoulFeatureId > 0 && person.HasFeature(moraleSoulFeatureId);

            AuraSource source = new AuraSource
            {
                person = person,
                range = range,
            };

            for (int i = 0; i < config.Effects.Count; i++)
            {
                AuraEffectDef effect = config.Effects[i];
                if (effect == null)
                    continue;

                int value = CalcEffectValue(config, effect, calcStat);
                if (hasMoraleSoul && moraleSoulFactor > 0)
                    value = value * moraleSoulFactor / 100;

                switch (effect.EffectType)
                {
                    case AuraEffectType.Attack: source.attack = value; break;
                    case AuraEffectType.Defence: source.defence = value; break;
                    case AuraEffectType.SkillCriticalRate: source.criticalRate = value; break;
                    case AuraEffectType.SkillCriticalDamage: source.criticalDamage = value; break;
                    case AuraEffectType.SkillSuccessRate: source.successRate = value; break;
                    case AuraEffectType.SkillResistRate: source.resistRate = value; break;
                    case AuraEffectType.AssistRate: source.assistRate = value; break;
                    case AuraEffectType.MoraleRecover: source.moraleRecover = value; break;
                }
            }

            return source;
        }

        /// <summary>按分档表取半径（取满足条件里的最大值）</summary>
        static int CalcRange(List<AuraRangeTier> tiers, int stat)
        {
            int range = 0;
            if (tiers == null)
                return range;

            for (int i = 0; i < tiers.Count; i++)
            {
                AuraRangeTier tier = tiers[i];
                if (tier != null && stat >= tier.Min && tier.Range > range)
                    range = tier.Range;
            }
            return range;
        }

        /// <summary>
        /// 按公式算单项数值：clamp(Base + floor((属性-阈值)/Step)*PerStep, Min, Max)。
        /// </summary>
        static int CalcEffectValue(AuraConfig config, AuraEffectDef effect, int stat)
        {
            int step = effect.Step > 0 ? effect.Step : 1;
            int value = effect.Base;
            int diff = stat - config.Threshold;
            if (diff > 0)
                value += (diff / step) * effect.PerStep;

            if (value < effect.Min) value = effect.Min;
            if (value > effect.Max) value = effect.Max;
            return value;
        }

        /// <summary>
        /// 重算一支部队的光环结果，并把结果写入部队的专属字段。
        /// </summary>
        static void RecalcTroop(Troop troop)
        {
            if (troop == null)
                return;

            TroopAuraResult result = GetOrCreateResult(troop);
            result.Reset();

            if (troop.IsAlive && troop.cell != null)
            {
                // ① 本队武将的"本队结算"效果：同项取最高
                ApplyPersonSelf(troop.Leader, result);
                ApplyPersonSelf(troop.Member1, result);
                ApplyPersonSelf(troop.Member2, result);

                // ② 攻防：扫描所有部队的武将光环，同势力 + 在半径内 → 同项取最高
                Scenario scenario = Scenario.Cur;
                if (scenario != null && scenario.troopsSet != null && troop.BelongForce != null)
                {
                    // 用静态上下文 + 静态回调遍历，避免每次重算都分配一个闭包
                    accumulateTarget = troop;
                    accumulateResult = result;
                    scenario.troopsSet.ForEach(accumulateAction);
                    accumulateTarget = null;
                    accumulateResult = null;
                }
            }

            troop.auraExtraAttack = result.attack;
            troop.auraExtraDefence = result.defence;
            troop.auraAssistChance = result.assistRate;
        }

        /// <summary>取（或建立）部队的光环结果槽</summary>
        static TroopAuraResult GetOrCreateResult(Troop troop)
        {
            if (!troopResults.TryGetValue(troop, out TroopAuraResult result))
            {
                result = new TroopAuraResult();
                troopResults[troop] = result;
            }
            return result;
        }

        /// <summary>把一名武将的"本队结算"效果合并进结果（同项取最高）</summary>
        static void ApplyPersonSelf(Person person, TroopAuraResult result)
        {
            List<AuraSource> list = EnsurePersonSources(person);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                AuraSource src = list[i];
                if (src.criticalRate > result.criticalRate) result.criticalRate = src.criticalRate;
                if (src.criticalDamage > result.criticalDamage) result.criticalDamage = src.criticalDamage;
                if (src.successRate > result.successRate) result.successRate = src.successRate;
                if (src.resistRate > result.resistRate) result.resistRate = src.resistRate;
                // 辅助攻击概率不在这里合并：它是范围效果，统一在 AccumulateArea 里按覆盖关系结算
                if (src.moraleRecover > result.moraleRecover) result.moraleRecover = src.moraleRecover;
            }
        }

        /// <summary>攻防聚合的部队遍历回调（静态复用，见 accumulateAction）</summary>
        static void AccumulateAreaCallback(Troop sourceTroop)
        {
            AccumulateArea(sourceTroop, accumulateTarget, accumulateResult);
        }

        /// <summary>把一支源部队的攻防光环累加到目标部队（同势力 + 半径内，同项取最高）</summary>
        static void AccumulateArea(Troop sourceTroop, Troop targetTroop, TroopAuraResult result)
        {
            if (sourceTroop == null || !sourceTroop.IsAlive || sourceTroop.cell == null)
                return;

            AccumulateAreaPerson(sourceTroop.Leader, sourceTroop, targetTroop, result);
            AccumulateAreaPerson(sourceTroop.Member1, sourceTroop, targetTroop, result);
            AccumulateAreaPerson(sourceTroop.Member2, sourceTroop, targetTroop, result);
        }

        /// <summary>把一名武将的攻防光环累加到目标部队</summary>
        static void AccumulateAreaPerson(Person person, Troop sourceTroop, Troop targetTroop, TroopAuraResult result)
        {
            if (person == null || person.IsDead || person.mBelongTroop != sourceTroop)
                return;

            List<AuraSource> list = EnsurePersonSources(person);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                AuraSource src = list[i];
                if (!src.HasAreaEffect)
                    continue;

                if (!targetTroop.IsSameForce(sourceTroop))
                    continue;
                if (targetTroop.cell.Distance(sourceTroop.cell) > src.range)
                    continue;

                if (src.attack > result.attack) result.attack = src.attack;
                if (src.defence > result.defence) result.defence = src.defence;
                // 政治光环：让光环半径内的友军作为支援方时更容易触发辅助攻击
                if (src.assistRate > result.assistRate) result.assistRate = src.assistRate;
            }
        }

        /// <summary>把以 center 为圆心、半径内的部队全部加入待重算集合</summary>
        static void CollectRange(Cell center, int range, HashSet<Troop> set)
        {
            if (center == null || range <= 0 || set == null)
                return;
            collectTarget = set;
            center.Spiral(range, collectAction);
            collectTarget = null;
        }

        /// <summary>Spiral 回调：收集格子上的部队</summary>
        static void CollectCell(Cell cell)
        {
            if (cell == null || cell.troop == null || collectTarget == null)
                return;
            collectTarget.Add(cell.troop);
        }

        /// <summary>把一支部队所有武将光环覆盖到的部队加入待重算集合</summary>
        static void CollectTroopArea(Troop troop, Cell center, HashSet<Troop> set)
        {
            if (troop == null || center == null)
                return;
            CollectPersonArea(troop.Leader, center, set);
            CollectPersonArea(troop.Member1, center, set);
            CollectPersonArea(troop.Member2, center, set);
        }

        /// <summary>把一名武将光环在 center 处覆盖到的部队加入待重算集合</summary>
        static void CollectPersonArea(Person person, Cell center, HashSet<Troop> set)
        {
            List<AuraSource> list = EnsurePersonSources(person);
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                AuraSource src = list[i];
                if (src.HasAreaEffect)
                    CollectRange(center, src.range, set);
            }
        }

        /// <summary>重算 dirtyTroops 里的全部部队</summary>
        static void RecalcDirty()
        {
            if (dirtyTroops.Count == 0)
                return;

            dirtyList.Clear();
            foreach (Troop t in dirtyTroops)
                dirtyList.Add(t);
            dirtyTroops.Clear();

            for (int i = 0; i < dirtyList.Count; i++)
                RecalcTroop(dirtyList[i]);
            dirtyList.Clear();
        }

        /// <summary>
        /// 部队属性重算：属性可能改变数值与半径，先作废本队武将缓存，
        /// 再把"旧半径（按新值近似）覆盖到的部队"一起重算。
        /// </summary>
        static void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (!inited || troop == null)
                return;

            InvalidateTroopPersons(troop);

            dirtyTroops.Clear();
            dirtyTroops.Add(troop);
            CollectTroopArea(troop, troop.cell, dirtyTroops);
            RecalcDirty();
        }

        /// <summary>
        /// 部队进入新格子：本队攻防可能变化；本队武将作为光环源移动时，
        /// 旧位置与新位置覆盖到的部队都要重算。
        /// </summary>
        static void OnTroopEnterCell(Troop troop, Cell dest, Cell last)
        {
            if (!inited || troop == null)
                return;

            InvalidateTroopPersons(troop);

            dirtyTroops.Clear();
            dirtyTroops.Add(troop);
            CollectTroopArea(troop, last, dirtyTroops);
            CollectTroopArea(troop, dest, dirtyTroops);
            RecalcDirty();
        }

        /// <summary>部队收尾：释放该部队相关缓存</summary>
        static void OnTroopClear(Troop troop, Scenario scenario)
        {
            if (troop == null)
                return;
            InvalidateTroopPersons(troop);
            troopResults.Remove(troop);
        }

        /// <summary>部队回合开始：结算本队的气力回复光环</summary>
        static void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (!inited || troop == null || !troop.IsAlive)
                return;

            TroopAuraResult result = GetResultFor(troop);
            if (result != null && result.moraleRecover > 0)
                troop.ChangeMorale(result.moraleRecover);
        }

        /// <summary>
        /// 物理战法会心率：只对"战法"（排除普攻与计略）生效。
        /// </summary>
        static void OnTroopAfterCalculateSkillCritical(Troop troop, SkillInstance skill, Cell spellCell, OverrideData<int> overrideData)
        {
            if (!inited || troop == null || skill == null || overrideData == null)
                return;
            if (skill.IsNormal() || skill.IsStrategy())
                return;

            TroopAuraResult result = GetResultFor(troop);
            if (result != null && result.criticalRate != 0)
                overrideData.Value += result.criticalRate;
        }

        /// <summary>
        /// 暴击倍率：直接给倍率加百分点（120% 基础 + 11 点 = 131%）。
        /// </summary>
        static void OnTroopCalculateSkillCriticalFactor(Troop troop, SkillInstance skill, Cell spellCell, OverrideData<int> overrideData)
        {
            if (!inited || troop == null || skill == null || overrideData == null)
                return;
            if (skill.IsNormal() || skill.IsStrategy())
                return;

            TroopAuraResult result = GetResultFor(troop);
            if (result != null && result.criticalDamage != 0)
                overrideData.Value += result.criticalDamage;
        }

        /// <summary>
        /// 计策成功率 / 识破率：
        ///   · 施法方本队的光环加成功率；
        ///   · 被施法方（spellCell.troop）本队的光环减成功率（即"降低对方计策成功率"）。
        /// </summary>
        static void OnTroopAfterCalculateSkillSuccess(Troop troop, SkillInstance skill, Cell spellCell, OverrideData<int> overrideData)
        {
            if (!inited || troop == null || skill == null || overrideData == null)
                return;
            if (!skill.IsStrategy())
                return;

            TroopAuraResult result = GetResultFor(troop);
            if (result != null && result.successRate != 0)
                overrideData.Value += result.successRate;

            Troop target = spellCell != null ? spellCell.troop : null;
            if (target != null)
            {
                TroopAuraResult targetResult = GetResultFor(target);
                if (targetResult != null && targetResult.resistRate != 0)
                    overrideData.Value -= targetResult.resistRate;
            }
        }
    }
}
