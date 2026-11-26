/*
 * 文件名：PersonSeekOffice.cs
 * 描述：在野武将主动求仕的月度结算系统。
 *       每月月初扫描一遍在野武将：只向「所在城归属的势力」投递求仕意向，
 *       按相性 / 在野时长 / 关系武将 / 君主魅力算概率；玩家势力当回合弹窗答复并立即形成结果，
 *       AI 势力按需求默认接纳。不跨回合保留待答求仕。
 * 创建日期：2026-11-26
 * 最后修改：2026-11-26
 */

using System.Collections.Generic;
using Sango.Core.Player;
using Sango.Render;

namespace Sango.Core
{
    /// <summary>
    /// 在野武将主动求仕（月度结算）。
    ///
    /// 【规则口径（玩家需求）】
    ///   · 只向「武将所在城归属的势力」求仕；所在城没有势力（中立 / 空城 / 关港无主）→ 本月不执行；
    ///   · 不计算城距、不跨城选目标、不做多势力加权；
    ///   · 结果当回合形成：玩家势力弹窗，玩家答复后立即入仕 / 记冷却；不写待答标记、不跨回合保留。
    ///
    /// 【原版对应】Open311 没有"在野武将每月主动求仕"的独立逻辑（原版只有探索 / 登用与
    /// `person_debut` 的仕官介绍人；本机没有 D:\open311\s11 源码可逐行比对），
    /// 本功能按玩家需求自主设计，不参与原有登用概率公式，也不改写登用 / 搜索链路。
    ///
    /// 【触发时机】<see cref="GameEvent.OnMonthStart"/>：在 TurnEnd 之后、IncreaseDate 内触发，
    /// 本月俸禄 / 俘虏关押 / 忠诚衰减都已结算完，新入仕的武将下一回合才参与结算。
    ///
    /// 【性能约束（sango-gc-perf）】这是每月一次的「全池扫描」热路径：
    ///   · 只用 <c>personSet.objects</c> 数组正序 for；不遍历 City.wildPersons（名册可能重复）；
    ///   · 命中列表是实例字段缓冲，月初 Clear() 复用，不在循环里 new；
    ///   · 吸引度全整数运算，无 LINQ / lambda / Sort(Comparison) / 字符串拼接；
    ///   · 字符串与日志只出现在真正命中（每月至多几人）的分支里。
    ///
    /// 【单线程前提】复用缓冲是实例字段，本项目回合制单线程；若将来并行化需改成本地缓冲。
    /// </summary>
    [GameSystem(order = 120)]
    public class PersonSeekOffice : GameSystem
    {
        /// <summary>一次命中（武将 + 所在城的归属势力 + 吸引度）</summary>
        struct Hit
        {
            public Person person;
            public Force force;
            public City city;
            public int score;
        }

        /// <summary>本月命中列表（按吸引度降序结算）</summary>
        readonly List<Hit> hits = new List<Hit>(64);

        /// <summary>月度扫描计时器（只在开启统计时使用；复用同一个实例，不分配）</summary>
        static readonly System.Diagnostics.Stopwatch PassWatch = new System.Diagnostics.Stopwatch();

        // ====================================================================
        // 生命周期
        // ====================================================================

        /// <summary>初始化：订阅月初结算（求仕结果不跨回合，故不订阅剧本开始）。</summary>
        public override void Init()
        {
            GameEvent.OnMonthStart += OnMonthStart;
        }

        /// <summary>清理：退订事件，避免读档 / 回主菜单后重复触发。</summary>
        public override void Clear()
        {
            GameEvent.OnMonthStart -= OnMonthStart;
            hits.Clear();
        }

        /// <summary>月初结算入口。</summary>
        /// <param name="scenario">当前剧本</param>
        void OnMonthStart(Scenario scenario)
        {
            if (!IsReady(scenario))
                return;

            ScenarioVariables v = scenario.Variables;
            if (v.seekOfficeEnabled == 0)
                return;

            bool stats = v.seekOfficeLogStats != 0;
            if (stats)
                PassWatch.Restart();

            int hitCount = RunMonthlyPass(scenario, true);

            if (stats)
            {
                PassWatch.Stop();
                Sango.Log.Info($"@人才@[求仕] 本月判定：命中 {hitCount} 人，耗时 {PassWatch.Elapsed.TotalMilliseconds:0.00} ms");
            }
        }

        // ====================================================================
        // 月度主流程
        // ====================================================================

        /// <summary>
        /// 月度扫描核心。返回命中人数。
        ///
        /// <paramref name="applyResults"/> = false 时只做「扫描 + 评分 + 排序」，
        /// 不写人物状态、不弹窗、不入仕、不播报 —— 供压测 / 回归调用（<see cref="MeasureMonthlyPass"/>）。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <param name="applyResults">是否落地结果</param>
        /// <returns>命中人数</returns>
        internal int RunMonthlyPass(Scenario scenario, bool applyResults)
        {
            ScenarioVariables v = scenario.Variables;
            Person[] persons = scenario.personSet.objects;

            hits.Clear();

            // ---------- ① 扫描在野武将（便宜判断在前，逐层收窄） ----------
            for (int i = 1; i < persons.Length; i++)
            {
                Person person = persons[i];
                if (person == null) continue;
                // state 是 int 字段：最便宜的一刀，先排除全部在职 / 俘虏 / 未登场
                if (person.state != (int)PersonStateType.Unemployed) continue;
                if (!person.IsAlive || person.IsDead) continue;
                if (person.IsSpecialPerson || person.IsTemporary) continue;
                if (person.Age < PersonSeekOfficeRules.MinSeekAge) continue;
                if (person.CurrentCity == null) continue;
                if (!person.IsFree) continue;   // 在部队 / 有任务 / 俘虏

                // 在野时长门槛：刚下野 / 刚被释放的武将需要先"沉淀"一段时间才会求仕
                int wildMonths = PersonSeekOfficeRules.WildMonths(person.wildTurnCount);
                if (!PersonSeekOfficeRules.MeetsWildMonthsRequirement(wildMonths, v)) continue;

                // ---------- ② 目标 = 所在城的归属势力（关港折算到主城） ----------
                City hostCity = person.CurrentCity;
                if (!hostCity.IsCity())
                {
                    City belong = hostCity.BelongCity;
                    if (belong == null) continue;       // 关港无主城：找不到可归顺的城
                    hostCity = belong;
                }

                Force force = hostCity.BelongForce;
                if (force == null) continue;            // 所在城无势力：不执行求仕
                if (!force.IsAlive || !force.IsNormal) continue;   // 异族 / 贼 / 已灭亡

                Person governor = force.mGovernor;
                if (governor == null || !governor.IsAlive || governor.IsPrisoner) continue;
                if (person.IsBannedFrom(force)) continue;                  // 半年内禁止仕官
                if (person.seekOfficeCooldownTimer > 0
                    && person.seekOfficeCooldownForceId == force.Id) continue; // 婉拒冷却中
                if (person.IsHate(governor)) continue;                     // 厌恶其君主

                // ---------- ③ 吸引度 + 月概率 ----------
                int likeCount = PersonSeekOfficeRules.LikeCountInForce(person, force);
                bool kinship = PersonSeekOfficeRules.HasKinshipInForce(person, force);
                int attraction = PersonSeekOfficeRules.Attraction(
                    wildMonths,
                    person.CompatibilityDistance(governor),
                    likeCount, kinship, governor.Glamour, force.CityCount, v);

                if (!GameRandom.Chance(PersonSeekOfficeRules.MonthlyChance(attraction, v)))
                    continue;

                Hit hit;
                hit.person = person;
                hit.force = force;
                hit.city = hostCity;
                hit.score = attraction;
                hits.Add(hit);
            }

            // ---------- ④ 按吸引度降序结算（玩家侧名额有限时高吸引度优先） ----------
            SortHitsDescending();

            if (!applyResults)
                return hits.Count;

            ResolveHits(v);
            return hits.Count;
        }

        /// <summary>结算命中列表：玩家势力排队弹窗（每月至多 N 人），AI 势力直接入仕。</summary>
        /// <param name="v">剧本变量</param>
        void ResolveHits(ScenarioVariables v)
        {
            int playerOffers = 0;
            int applied = 0;
            int budget = v.seekOfficeMonthlyBudget;

            for (int i = 0; i < hits.Count; i++)
            {
                if (budget > 0 && applied >= budget)
                    break;

                Hit hit = hits[i];
                Person person = hit.person;
                Force force = hit.force;
                City city = hit.city;

                // 入仕 / 弹窗前复核：前面的人可能已改变局面（例如同一武将已被别人登用）
                if (person == null || force == null || city == null) continue;
                if (!person.IsWild) continue;
                if (!force.IsAlive || city.BelongForce != force) continue;

                bool asksPlayer = force.IsPlayer
                    && city.BelongCorps != null
                    && city.BelongCorps.IsPlayerControl;   // 委任军团按 AI 处理

                if (asksPlayer)
                {
                    if (playerOffers >= v.seekOfficePlayerOffersPerMonth)
                        continue;                          // 本月玩家侧名额已满，本月不发起

                    playerOffers++;
                    EnqueueOffer(person, force, city);
                    Sango.Log.Info($"@人才@[求仕]{person.Name} -> {force.Name}/{city.Name}（待玩家答复，S={hit.score}）");
                }
                else
                {
                    if (PersonStateSystem.JoinByPetition(person, force, city))
                    {
                        applied++;
                        Sango.Log.Info($"@人才@[求仕]{person.Name} -> {force.Name}/{city.Name}（AI 接纳，S={hit.score}）");
                    }
                }
            }
        }

        // ====================================================================
        // 排序（手写，避免委托与闭包分配）
        // ====================================================================

        /// <summary>命中列表按吸引度降序；同分按人物 Id 升序（保证可复现）。手写插入排序，列表很小。</summary>
        void SortHitsDescending()
        {
            for (int i = 1; i < hits.Count; i++)
            {
                Hit current = hits[i];
                int j = i - 1;
                while (j >= 0)
                {
                    Hit prev = hits[j];
                    bool after = prev.score < current.score
                        || (prev.score == current.score
                            && prev.person != null && current.person != null
                            && prev.person.Id > current.person.Id);
                    if (!after)
                        break;
                    hits[j + 1] = prev;
                    j--;
                }
                hits[j + 1] = current;
            }
        }

        // ====================================================================
        // 冷却 / 失效播报
        // ====================================================================

        /// <summary>玩家婉拒后给「该武将 × 该势力」记冷却，只挡求仕，不挡登用。</summary>
        /// <param name="person">武将</param>
        /// <param name="force">目标势力</param>
        public static void StartCooldown(Person person, Force force)
        {
            if (person == null || force == null)
                return;

            ScenarioVariables v = Scenario.Cur != null ? Scenario.Cur.Variables : null;
            int turns = v != null ? v.seekOfficeRejectCooldownTurns : 18;
            if (turns <= 0)
            {
                person.seekOfficeCooldownForceId = -1;
                person.seekOfficeCooldownTimer = 0;
                return;
            }

            person.seekOfficeCooldownForceId = force.Id;
            person.seekOfficeCooldownTimer = turns;
        }

        /// <summary>答复时发现时局已变（人已不在野 / 势力已灭 / 城已失）的兜底播报。</summary>
        /// <param name="person">武将</param>
        /// <param name="force">目标势力</param>
        public static void NotifyStale(Person person, Force force)
        {
            if (person == null || force == null)
                return;
            Sango.Log.Info($"@人才@[求仕]{person.Name} 的求仕在答复前已失效（{force.Name}）");
            PlayerMessage.AddTextMessage(
                $"【求仕】{person.ColorName}已另行他处。", force, 0, 0);
        }

        /// <summary>把求仕演出事件放入队列（RenderEvent 是全局串行队列，玩家逐人答复）。</summary>
        /// <param name="person">武将</param>
        /// <param name="force">目标势力</param>
        /// <param name="city">接受求仕的城</param>
        static void EnqueueOffer(Person person, Force force, City city)
        {
            RenderEvent renderEvent = RenderEvent.Instance;
            if (renderEvent == null)
                return;

            PersonSeekOfficeEvent evt = renderEvent.Create<PersonSeekOfficeEvent>();
            evt.Init(person, force, city);
            renderEvent.Add(evt);
        }

        // ====================================================================
        // 压测 / 调试入口
        // ====================================================================

        /// <summary>
        /// 【仅供压测 / 回归】对指定剧本跑一次「只计算、不落地」的月度扫描，返回命中人数。
        ///
        /// 不写人物状态、不弹窗、不入仕、不播报，也不依赖 GameSystemManager：
        /// 用一个临时实例复用同样的算法，便于在编辑器里测量耗时与 GC。
        /// </summary>
        /// <param name="scenario">已加载的剧本</param>
        /// <returns>命中人数；参数不合法返回 -1</returns>
        public static int MeasureMonthlyPass(Scenario scenario)
        {
            if (!IsReady(scenario))
                return -1;

            PersonSeekOffice probe = new PersonSeekOffice();
            return probe.RunMonthlyPass(scenario, false);
        }

        /// <summary>剧本是否具备月度扫描所需的数据。</summary>
        /// <param name="scenario">剧本</param>
        /// <returns>就绪返回 true</returns>
        static bool IsReady(Scenario scenario)
        {
            if (scenario == null) return false;
            if (scenario.Variables == null) return false;
            if (scenario.Info == null) return false;
            if (scenario.personSet == null || scenario.personSet.objects == null) return false;
            if (scenario.forceSet == null || scenario.forceSet.objects == null) return false;
            if (scenario.citySet == null || scenario.citySet.objects == null) return false;
            return true;
        }
    }
}
