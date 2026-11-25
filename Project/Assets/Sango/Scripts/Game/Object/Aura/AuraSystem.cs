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
    ///   3. 每项效果的落点由配置的 Scope 决定：0 = 半径内同势力部队（含来源本队），
    ///      1 = 仅来源本队；同一受益部队同项取最高值；
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

            // ---------------- Scope = 0：光环半径内同势力部队 ----------------
            public int areaAttack;
            public int areaDefence;
            public int areaCriticalRate;
            public int areaCriticalDamage;
            public int areaSuccessRate;
            public int areaResistRate;
            public int areaAssistRate;
            public int areaMoraleRecover;

            // ---------------- Scope = 1：仅来源本队 ----------------
            public int selfAttack;
            public int selfDefence;
            public int selfCriticalRate;
            public int selfCriticalDamage;
            public int selfSuccessRate;
            public int selfResistRate;
            public int selfAssistRate;
            public int selfMoraleRecover;

            /// <summary>是否有范围效果（任一 Scope=0 的项）</summary>
            public bool HasAreaEffect => range > 0 && (
                areaAttack != 0 || areaDefence != 0 || areaCriticalRate != 0 || areaCriticalDamage != 0
                || areaSuccessRate != 0 || areaResistRate != 0 || areaAssistRate != 0 || areaMoraleRecover != 0);
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

            // 【剧本参数总开关】关闭时不加载配置、不订阅事件，等同没有光环系统。
            // 默认 true；旧存档没有该字段时保持默认，与 0.4.20 行为一致。
            if (scenario.Variables != null && !scenario.Variables.AuraEnabled)
            {
                Sango.Log.Info("*[光环]剧本参数已关闭（AuraEnabled=false），光环系统不启用");
                return;
            }

            // 配置使用副本：剧本参数会覆盖其中的阈值 / 公式，不能污染 CommonData 里的共享模板
            //（同一进程里换剧本、重读通用数据时，模板必须保持 JSON 原值）。
            if (scenario.CommonData.AuraConfigs != null)
            {
                scenario.CommonData.AuraConfigs.ForEach(c =>
                {
                    if (c != null && c.Effects != null && c.Effects.Count > 0)
                        configs.Add(CloneConfig(c));
                });
            }

            AuraGlobals globals = scenario.CommonData.AuraGlobals;
            if (globals != null)
            {
                moraleSoulFeatureId = globals.MoraleSoulFeatureId;
                if (globals.MoraleSoulFactor > 0)
                    moraleSoulFactor = globals.MoraleSoulFactor;
            }

            // 【剧本参数覆盖】必须在范围表构建之前：覆盖会重写统率光环的 RangeTiers。
            ApplyVariableOverrides(scenario.Variables);

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
        /// 运行时切换光环总开关（剧本参数界面使用）。
        ///
        /// 关闭：清零所有部队的 aura 字段并退订事件；
        /// 开启：在当前剧本上重新 Init（读配置 + 全量补算）。
        /// 开局前调用只改剧本参数，实际生效仍由 Scenario.Init → AuraSystem.Init 负责。
        /// </summary>
        /// <param name="enabled">是否启用</param>
        public static void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                ClearTroopAuraFields();
                Shutdown();
                Sango.Log.Info("*[光环]已关闭（剧本参数）");
                return;
            }

            Scenario scenario = Scenario.Cur;
            if (scenario == null || !scenario.IsAlive)
            {
                // 开局前切换：只记在剧本参数里，由 Scenario.Init → AuraSystem.Init 统一生效
                return;
            }

            Init(scenario);
            Sango.Log.Info("*[光环]已开启（剧本参数）");
        }

        /// <summary>
        /// 关闭光环时把部队身上的光环加成清零。
        /// 只清 aura 专属字段，建筑 / Buff / 战法写进 extraAttack / extraDefence 的加成不受影响。
        /// </summary>
        static void ClearTroopAuraFields()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.troopsSet == null)
                return;

            scenario.troopsSet.ForEach(t =>
            {
                if (t == null) return;
                t.auraExtraAttack = 0;
                t.auraExtraDefence = 0;
                t.auraAssistChance = 0;
            });
        }

        /// <summary>上一次 Reload 的帧号（同帧节流）</summary>
        static int lastReloadFrame = -1;

        /// <summary>
        /// 剧本参数修改后立即重算（剧本参数界面使用）。
        ///
        /// 同一帧内的连续输入（例如把 90 改到 120 时的 1 → 12 → 120）只重算一次，
        /// 避免每个字符都做一次全量补算。
        /// </summary>
        public static void Reload()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || !scenario.IsAlive)
                return;

            int frame = UnityEngine.Time.frameCount;
            if (frame == lastReloadFrame)
                return;
            lastReloadFrame = frame;

            Init(scenario);
        }

        /// <summary>
        /// 复制一份光环配置。
        ///
        /// 配置对象来自 CommonData 的共享模板，剧本参数覆盖只能改副本；
        /// 直接改模板会让下一次读档 / 换剧本拿到被改过的值。
        /// </summary>
        static AuraConfig CloneConfig(AuraConfig src)
        {
            AuraConfig dst = new AuraConfig
            {
                Id = src.Id,
                Name = src.Name,
                StatType = src.StatType,
                Threshold = src.Threshold,
                RequireCommandForOthers = src.RequireCommandForOthers,
                CommandRequire = src.CommandRequire,
                ForceEnableFeatureId = src.ForceEnableFeatureId,
                RangeFeatureId = src.RangeFeatureId,
                RangeFeatureAdd = src.RangeFeatureAdd,
                Effects = new List<AuraEffectDef>(),
                RangeTiers = new List<AuraRangeTier>(),
            };

            if (src.Effects != null)
            {
                for (int i = 0; i < src.Effects.Count; i++)
                {
                    AuraEffectDef e = src.Effects[i];
                    if (e == null) continue;
                    dst.Effects.Add(new AuraEffectDef
                    {
                        Type = e.Type,
                        Base = e.Base,
                        Step = e.Step,
                        PerStep = e.PerStep,
                        Min = e.Min,
                        Max = e.Max,
                        Scope = e.Scope,
                    });
                }
            }

            if (src.RangeTiers != null)
            {
                for (int i = 0; i < src.RangeTiers.Count; i++)
                {
                    AuraRangeTier t = src.RangeTiers[i];
                    if (t == null) continue;
                    dst.RangeTiers.Add(new AuraRangeTier { Min = t.Min, Range = t.Range });
                }
            }

            return dst;
        }

        /// <summary>
        /// 用剧本参数覆盖配置模板。
        ///
        /// 覆盖范围：五条光环的阈值 / 公式（Base+Step+PerStep+Max）/ 统率光环范围分档 /
        /// 附加光环统率门槛 / 军魂系数。下限统一取基础值（Min = Base）。
        /// </summary>
        static void ApplyVariableOverrides(ScenarioVariables v)
        {
            if (v == null)
                return;

            for (int i = 0; i < configs.Count; i++)
            {
                AuraConfig c = configs[i];
                if (c == null)
                    continue;

                if (c.RequireCommandForOthers)
                {
                    // 统率光环：攻防 + 会心 + 范围分档
                    c.Threshold = v.auraCommandThreshold;
                    SetEffect(c, AuraEffectType.Attack,
                        v.auraCommandAttackBase, v.auraCommandAttackStep, v.auraCommandAttackPerStep, v.auraCommandAttackMax);
                    SetEffect(c, AuraEffectType.Defence,
                        v.auraCommandAttackBase, v.auraCommandAttackStep, v.auraCommandAttackPerStep, v.auraCommandAttackMax);
                    SetEffect(c, AuraEffectType.SkillCriticalRate,
                        v.auraCriticalBase, v.auraCriticalStep, v.auraCriticalPerStep, v.auraCriticalMax);

                    c.RangeTiers = new List<AuraRangeTier>
                    {
                        new AuraRangeTier { Min = v.auraCommandThreshold, Range = 1 },
                        new AuraRangeTier { Min = v.auraRangeTier2, Range = 2 },
                        new AuraRangeTier { Min = v.auraRangeTier3, Range = 3 },
                    };
                    continue;
                }

                // 附加光环：本人统率门槛 + 各自公式
                c.CommandRequire = v.auraCommandRequire;
                switch (c.StatType)
                {
                    case 1:
                        c.Threshold = v.auraStrengthThreshold;
                        SetEffect(c, AuraEffectType.SkillCriticalDamage,
                            v.auraCriticalDamageBase, v.auraCriticalDamageStep, v.auraCriticalDamagePerStep, v.auraCriticalDamageMax);
                        break;
                    case 2:
                        c.Threshold = v.auraIntelligenceThreshold;
                        SetEffect(c, AuraEffectType.SkillSuccessRate,
                            v.auraSuccessBase, v.auraSuccessStep, v.auraSuccessPerStep, v.auraSuccessMax);
                        SetEffect(c, AuraEffectType.SkillResistRate,
                            v.auraResistBase, v.auraResistStep, v.auraResistPerStep, v.auraResistMax);
                        break;
                    case 3:
                        c.Threshold = v.auraPoliticsThreshold;
                        SetEffect(c, AuraEffectType.AssistRate,
                            v.auraAssistBase, v.auraAssistStep, v.auraAssistPerStep, v.auraAssistMax);
                        break;
                    case 4:
                        c.Threshold = v.auraGlamourThreshold;
                        SetEffect(c, AuraEffectType.MoraleRecover,
                            v.auraMoraleBase, v.auraMoraleStep, v.auraMoralePerStep, v.auraMoraleMax);
                        break;
                }
            }

            if (v.auraMoraleSoulFactor > 0)
                moraleSoulFactor = v.auraMoraleSoulFactor;
        }

        /// <summary>按效果类型覆盖一项公式（下限取基础值）</summary>
        static void SetEffect(AuraConfig config, AuraEffectType type, int baseValue, int step, int perStep, int max)
        {
            if (config.Effects == null)
                return;

            for (int i = 0; i < config.Effects.Count; i++)
            {
                AuraEffectDef e = config.Effects[i];
                if (e == null || e.Type != (int)type)
                    continue;

                e.Base = baseValue;
                e.Min = baseValue;
                e.Step = step;
                e.PerStep = perStep;
                e.Max = max;
                return;
            }
        }

        /// <summary>
        /// 取一支部队当前光环的显示半径（本队所有武将里有范围效果的最大值）。
        ///
        /// 【用途】部队点击 / 悬停时在地面画天蓝色范围（见 BuildingRangeSelector）。
        /// 【口径】范围只由提供者统率决定；同队武将的光环中心都是本队所在格，
        /// 因此并集 = 一个半径等于最大 range 的圆。没有范围效果时返回 0。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <returns>显示半径（格），0 表示不显示</returns>
        public static int GetTroopAuraRange(Troop troop)
        {
            if (!inited || troop == null || !troop.IsAlive || troop.cell == null)
                return 0;

            int range = GetPersonAuraRange(troop.Leader);
            int r = GetPersonAuraRange(troop.Member1);
            if (r > range) range = r;
            r = GetPersonAuraRange(troop.Member2);
            if (r > range) range = r;
            return range;
        }

        /// <summary>取一名武将提供的最大光环半径（仅统计有范围效果的项）</summary>
        static int GetPersonAuraRange(Person person)
        {
            List<AuraSource> list = EnsurePersonSources(person);
            if (list == null)
                return 0;

            int range = 0;
            for (int i = 0; i < list.Count; i++)
            {
                AuraSource src = list[i];
                if (src.HasAreaEffect && src.range > range)
                    range = src.range;
            }
            return range;
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

            // 【范围统一由统率决定】所有光环（含武力 / 智力 / 政治 / 魅力）的半径只看提供者统率，
            // 分档表取统率光环配置的 RangeTiers（Init 时复制为 globalRangeTiers）。
            // 各光环的"效果数值"才用各自属性和各自公式计算。
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
                    range = CalcRange(config.RangeTiers, person.Command);   // 兜底：全局表缺失
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

                // Scope 决定落点：0 = 半径内同势力部队（区域聚合），1 = 仅来源本队
                bool area = effect.Scope == 0;
                switch (effect.EffectType)
                {
                    case AuraEffectType.Attack:
                        if (area) source.areaAttack = value; else source.selfAttack = value;
                        break;
                    case AuraEffectType.Defence:
                        if (area) source.areaDefence = value; else source.selfDefence = value;
                        break;
                    case AuraEffectType.SkillCriticalRate:
                        if (area) source.areaCriticalRate = value; else source.selfCriticalRate = value;
                        break;
                    case AuraEffectType.SkillCriticalDamage:
                        if (area) source.areaCriticalDamage = value; else source.selfCriticalDamage = value;
                        break;
                    case AuraEffectType.SkillSuccessRate:
                        if (area) source.areaSuccessRate = value; else source.selfSuccessRate = value;
                        break;
                    case AuraEffectType.SkillResistRate:
                        if (area) source.areaResistRate = value; else source.selfResistRate = value;
                        break;
                    case AuraEffectType.AssistRate:
                        if (area) source.areaAssistRate = value; else source.selfAssistRate = value;
                        break;
                    case AuraEffectType.MoraleRecover:
                        if (area) source.areaMoraleRecover = value; else source.selfMoraleRecover = value;
                        break;
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

                // ② 范围效果（Scope = 0）：扫描所有部队的武将光环，同势力 + 在半径内 → 同项取最高
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
                // 只合并 Scope = 1 的项；Scope = 0 的项统一在 AccumulateArea 里按覆盖关系结算
                if (src.selfAttack > result.attack) result.attack = src.selfAttack;
                if (src.selfDefence > result.defence) result.defence = src.selfDefence;
                if (src.selfCriticalRate > result.criticalRate) result.criticalRate = src.selfCriticalRate;
                if (src.selfCriticalDamage > result.criticalDamage) result.criticalDamage = src.selfCriticalDamage;
                if (src.selfSuccessRate > result.successRate) result.successRate = src.selfSuccessRate;
                if (src.selfResistRate > result.resistRate) result.resistRate = src.selfResistRate;
                if (src.selfAssistRate > result.assistRate) result.assistRate = src.selfAssistRate;
                if (src.selfMoraleRecover > result.moraleRecover) result.moraleRecover = src.selfMoraleRecover;
            }
        }

        /// <summary>攻防聚合的部队遍历回调（静态复用，见 accumulateAction）</summary>
        static void AccumulateAreaCallback(Troop sourceTroop)
        {
            AccumulateArea(sourceTroop, accumulateTarget, accumulateResult);
        }

        /// <summary>把一支源部队的 Scope=0 效果累加到目标部队（同势力 + 半径内，同项取最高）</summary>
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

                // 只合并 Scope = 0 的项：攻防、会心、暴伤、计策、识破、助攻、气力都可按配置范围生效
                if (src.areaAttack > result.attack) result.attack = src.areaAttack;
                if (src.areaDefence > result.defence) result.defence = src.areaDefence;
                if (src.areaCriticalRate > result.criticalRate) result.criticalRate = src.areaCriticalRate;
                if (src.areaCriticalDamage > result.criticalDamage) result.criticalDamage = src.areaCriticalDamage;
                if (src.areaSuccessRate > result.successRate) result.successRate = src.areaSuccessRate;
                if (src.areaResistRate > result.resistRate) result.resistRate = src.areaResistRate;
                if (src.areaAssistRate > result.assistRate) result.assistRate = src.areaAssistRate;
                if (src.areaMoraleRecover > result.moraleRecover) result.moraleRecover = src.areaMoraleRecover;
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
