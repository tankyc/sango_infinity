using System;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 求解层：岗位 × 人选 → 匹配分 → 贪心指派 → <see cref="DeploymentPlan"/>。
    ///
    /// · 影子模式（`shadowOnly = true`）：只产出计划，闸门照常判定但不执行；
    /// · 执行模式（`shadowOnly = false`）：计划通过闸门后由 <see cref="DeploymentExecutor"/> 真正下发调动令；
    /// · 失效驱动：城况未变的城直接复用上回合岗位表（<c>enableIncrementalPlan</c>）；
    /// · 稳态保护：防抖（同一武将 N 回合内不再动）+ 每回合按城收发配额。
    /// </summary>
    public static class DeploymentSolver
    {
        /// <summary>求解过程中的"待填岗位"（附带所属城况，用于打分与原因链）。</summary>
        public struct PostTask
        {
            /// <summary>岗位</summary>
            public Post post;
            /// <summary>所属城的威胁等级</summary>
            public int threatLevel;
            /// <summary>所属城的开发缺口（0..1）</summary>
            public float devGap;
            /// <summary>是否被围（被围城不作为接收目标）</summary>
            public bool underSiege;
            /// <summary>
            /// 是否只允许本城在岗填充、**不参与外调**。
            /// 用于军团委任"港关驻军"关闭时该军团的港 / 关岗位：
            /// 既不向港关调人（不产生外调需求），也不让港关因为"岗位填不满"而把自己的人当成富余调走
            /// —— 相当于"AI 不管理这个军团的港关驻军编制"，但也不会把港关抽空。
            /// </summary>
            public bool localOnly;
            /// <summary>
            /// 本岗位属于"把该城补到最低人数"的那几个岗（该城在城人数 &lt;
            /// <c>DeploymentWeights.minCityPersons</c>，按岗位优先级取最前面的 N 个，N = 还差多少人）。
            ///
            /// 这类岗位**排到最前拿人**、**免行程 / 跨圈层成本**、**门槛降到 0**：
            /// 一座 0~2 人的城连征兵 / 运输 / 内政都开不了工（资源运不走、兵造不出），
            /// 比"更靠边境"更急 —— 它是当前最需要人手的地方。
            /// 只覆盖"补到最低人数"所需的岗，编制里其余的岗仍按常规竞争。
            /// </summary>
            public bool lowPop;
        }

        /// <summary>
        /// 分配顺序（"谁先拿到稀缺的人"）—— **边境优先原则**：
        ///   1) 硬性岗位优先（港关守备 / 前线·高威胁的第一军事岗）；
        ///   2) 岗位优先级（守备 → 军事 → 征兵 → 军备 → 训练 → 搜索 → 运输 → 开发 → 后勤）；
        ///   3) 军事 / 守备岗：**圈层小的先拿人**（0 = 边境最优先），同圈层再比威胁高的优先；
        ///      内政岗：开发缺口大的先拿人，同缺口时更靠边境的先拿；
        ///   4) 城 id / 岗位类型兜底 → 结果稳定可复现。
        ///
        /// 注意：这里只决定"**先给谁**"；具体派谁由 `Score = 能力适配 − 行程成本 − 跨圈层成本` 决定。
        /// </summary>
        public static int CompareForAssign(PostTask a, PostTask b, Dictionary<int, int> ringOfCity)
        {
            if (a.post.required != b.post.required)
                return a.post.required ? -1 : 1;

            // 【人手极缺城优先】在城人数低于最低保底（< minCityPersons）的城先拿人：
            // 它连"征兵 / 运输 / 内政"都开不了工，比"谁更靠边境"更急。
            // 放在 required 之后、岗位优先级之前 —— 目的就是让"低于 3 人的城"先被补起来。
            if (a.lowPop != b.lowPop)
                return a.lowPop ? -1 : 1;

            if (a.post.priority != b.post.priority)
                return a.post.priority.CompareTo(b.post.priority);

            bool aMil = a.post.kind == PostKind.Military || a.post.kind == PostKind.Garrison;
            bool bMil = b.post.kind == PostKind.Military || b.post.kind == PostKind.Garrison;

            int aRing = RingOf(ringOfCity, a.post.cityId);
            int bRing = RingOf(ringOfCity, b.post.cityId);

            if (aMil && bMil)
            {
                if (aRing != bRing) return aRing.CompareTo(bRing);
                if (a.threatLevel != b.threatLevel) return b.threatLevel.CompareTo(a.threatLevel);
            }
            else if (!aMil && !bMil)
            {
                if (a.devGap != b.devGap) return b.devGap.CompareTo(a.devGap);
                if (aRing != bRing) return aRing.CompareTo(bRing);
            }

            if (a.post.cityId != b.post.cityId) return a.post.cityId.CompareTo(b.post.cityId);
            return a.post.kind.CompareTo(b.post.kind);
        }

        // ==================== 失效驱动：增量编制缓存 ====================

        struct CacheEntry
        {
            public int stamp;
            public List<Post> posts;
        }

        static readonly Dictionary<int, CacheEntry> cityCache = new Dictionary<int, CacheEntry>();
        static Scenario cachedScenario;

        /// <summary>
        /// 军团标签（报告用，如"第三军团"）。
        /// 不用 <c>Corps.Name</c>：它带富文本颜色标签，会污染纯文本日志。
        /// </summary>
        static string CorpsLabel(Corps corps)
        {
            if (corps == null) return null;
            int n = corps.number;
            if (n > 0 && n < Corps.numberTxt.Length)
                return "第" + Corps.numberTxt[n] + "军团";
            return "军团" + n;
        }

        /// <summary>清空增量编制缓存（换剧本时自动清，也可手动调）。</summary>
        public static void ClearCache()
        {
            cityCache.Clear();
            currentTeamDemand = null;
            cityDistanceCache = null;
        }

        /// <summary>
        /// 本回合"推荐队伍要人"的需求快照（每次求解建一次，供打分做 O(1) 查询）。
        /// 只在主线程、且每次 Solve 都会重建，因此静态缓存是安全的。
        /// </summary>
        static TroopTeamDemand currentTeamDemand;

        /// <summary>
        /// 本趟求解的"城对距离"缓存（每次 Solve 开始时清空复用）。
        ///
        /// 【为什么必须有】匹配分 <see cref="Score"/> 的行程成本要对"每个岗位 × 每个候选"算一次，
        /// 直接走 <c>Person.DistanceDays</c> → <c>City.Distance</c> → <c>Scenario.FindShortestPath</c>：
        /// 十万次评分就是十万次字典查询与短命分配。而行程只取决于 (出发城, 目标城) 这一对城，
        /// 按城对缓存后可把寻路查询压到 O(城对数)（几十量级）。
        ///
        /// 静态字段的理由与 <see cref="currentTeamDemand"/> 相同：只在主线程、每次 Solve 重建。
        /// </summary>
        static Dictionary<long, int> cityDistanceCache;

        /// <summary>
        /// 本趟求解的行程成本系数（= <c>costPerTurn / max(1, turnDays)</c>）。
        /// 提前算好，避免每次评分都做一遍除法与 <c>Math.Max</c>。
        /// </summary>
        static float travelCostScale = 1f;

        /// <summary>
        /// 目标城"驻军特技集合"的复用缓冲：每个岗位填一次，供该岗位的所有候选人复用，
        /// 避免每岗位新建一个 <see cref="HashSet{T}"/>（岗位是数百量级）。
        /// </summary>
        static readonly HashSet<int> destFeatureScratch = new HashSet<int>();

        /// <summary>
        /// 编制：优先复用缓存（城况"戳"未变时），否则重建。
        /// 戳只包含**稳定量**（等级 / 圈层 / 威胁档 / 内政点档 / 缺口档 / 预想兵力档 / 设施 / 在册人数），
        /// 因此出征、灾害、运输都不会把戳打散 → 不会无谓重建。
        /// </summary>
        static List<Post> GetPosts(City city, CitySituation situation, int threatLevel, int expectedTroops,
            DeploymentWeights weights, int personTotal)
        {
            if (weights != null && weights.enableIncrementalPlan)
            {
                int stamp = CityStamp(city, situation, threatLevel, expectedTroops, weights, personTotal);
                CacheEntry entry;
                if (cityCache.TryGetValue(city.Id, out entry) && entry.stamp == stamp && entry.posts != null)
                    return entry.posts;

                List<Post> fresh = new List<Post>();
                CityEstablishment.Build(city, situation, threatLevel, weights, personTotal, fresh);
                entry.stamp = stamp;
                entry.posts = fresh;
                cityCache[city.Id] = entry;
                return fresh;
            }

            List<Post> posts = new List<Post>();
            CityEstablishment.Build(city, situation, threatLevel, weights, personTotal, posts);
            return posts;
        }

        /// <summary>城况"戳"：只取稳定量，用 unchecked 哈希组合（碰撞最多导致复用一回合的旧表，无正确性风险）。</summary>
        static int CityStamp(City city, CitySituation situation, int threatLevel, int expectedTroops,
            DeploymentWeights weights, int personTotal)
        {
            int tier = threatLevel >= weights.threatHighAt ? 2 : (threatLevel >= weights.threatMidAt ? 1 : 0);
            int level = city.CityLevelType != null ? city.CityLevelType.Id : 1;
            int interiorBand = CityEstablishment.CountInteriorPoints(city) / 3;
            int devBand = (int)(CityEstablishment.CalcDevelopGap(city) * 4f);
            int troopBand = expectedTroops / 5000;
            int flag = (situation.isUnderSiege ? 1 : 0)
                     | (situation.hasMachineFactory ? 2 : 0)
                     | (situation.hasBoatFactory ? 4 : 0)
                     | (situation.hasBlacksmith ? 8 : 0)
                     | (situation.hasStable ? 16 : 0)
                     | (situation.hasPort ? 32 : 0)
                     // 资源调度登记的"待发存量"会临时加一个运输岗（见 CityEstablishment），
                     // 戳里必须带上它，否则岗位表会被缓存复用、新出现的运输需求看不到人。
                     | (ResourceDispatchState.HasPendingEnvoyDemand(city.Id) ? 64 : 0)
                     // 【港关·军情】军情判定（被围 / 附近有敌 / 境内有敌 / 邻接外势力）会决定港关
                     // "驻守还是只留运输岗"（见 CityEstablishment），三个分量都要进戳，
                     // 否则军情来了岗位表还被缓存复用、港口拿不到守将。
                     | (situation.hasThreatTroop ? 128 : 0)
                     | (situation.hasForeignNeighbor ? 256 : 0)
                     | (city.EnemyCount > 0 ? 512 : 0);
            int residents = city.allPersons != null ? city.allPersons.Count : 0;

            unchecked
            {
                int h = 17;
                h = h * 31 + level;
                h = h * 31 + CityEstablishment.ResolveRing(city);   // 【R1】港关圈层随归属都市变化时需重算
                h = h * 31 + tier;
                h = h * 31 + interiorBand;
                h = h * 31 + devBand;
                h = h * 31 + troopBand;
                h = h * 31 + flag;
                h = h * 31 + residents;
                h = h * 31 + personTotal / 5;
                return h;
            }
        }

        // ==================== 求解 ====================

        /// <summary>
        /// 为一个**势力**求解部署计划（**势力级作用域**：目标城与抽调池都是全势力）。
        ///
        /// 这是非玩家（AI）势力的正式入口 —— AI 势力以势力为边界，势力内可跨军团调人。
        /// 玩家势力走 <see cref="Solve(Force, Corps, Scenario)"/>，逐个军团求解。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">剧本</param>
        /// <returns>部署计划（永不为 null）</returns>
        public static DeploymentPlan Solve(Force force, Scenario scenario)
        {
            return Solve(force, null, scenario);
        }

        /// <summary>
        /// 求解核心：按<b>调度作用域</b>编制岗位、指派人员，并按其模式决定是否执行。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scope">
        /// 军团作用域（null = 势力级，不按军团切分）。
        /// 非 null 时目标城与抽调池**都限本军团** —— 军团内自治，绝不跨军团调人。
        /// </param>
        /// <param name="scenario">剧本</param>
        /// <returns>部署计划（永不为 null）</returns>
        public static DeploymentPlan Solve(Force force, Corps scope, Scenario scenario)
        {
            return Solve(force, scope, false, scenario);
        }

        /// <summary>
        /// 求解核心（带"仅参考"开关）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scope">军团作用域（null = 势力级）</param>
        /// <param name="dryRun">
        /// 是否**只算不调**：true 时照常求解并产出完整计划，但一次调动都不下发。
        /// 用于玩家直辖的第一军团 —— 出影子报告供参考，绝不改动玩家自己排好的部署。
        /// </param>
        /// <param name="scenario">剧本</param>
        /// <returns>部署计划（永不为 null）</returns>
        public static DeploymentPlan Solve(Force force, Corps scope, bool dryRun, Scenario scenario)
        {
            DeploymentPlan plan = new DeploymentPlan();
            if (force == null || scenario == null)
                return plan;

            AIConfig config = AIConfig.Instance;
            DeploymentWeights weights = (config != null && config.deployment != null)
                ? config.deployment : new DeploymentWeights();

            // 【军团委任 · 港关驻军开关】只对军团作用域（只可能是玩家军团）生效。
            // 关闭时本作用域不为港 / 关编制"可由外调补人"的驻军岗位：
            //   ① 不向港关调人（外调需求被掐掉）；
            //   ② 港关也不因岗位填不满而把自己的人当成富余调走（见 PostTask.localOnly）。
            // 势力级作用域（scope == null，即非玩家势力）恒为开，保证 AI 势力行为完全不变。
            bool allowPortGateGarrison = scope == null || DeploymentExecutor.IsPortGateGarrisonEnabled(scope);

            // 【性能】本趟求解的行程相关缓存与系数（详见字段注释）。
            // 放在最前面，保证后面所有打分路径都能安全复用。
            if (cityDistanceCache == null)
                cityDistanceCache = new Dictionary<long, int>(256);
            else
                cityDistanceCache.Clear();
            travelCostScale = weights.costPerTurn / Math.Max(1, weights.turnDays);

            // 换剧本 → 清掉增量缓存，避免复用上个剧本的岗位表
            if (!ReferenceEquals(cachedScenario, scenario))
            {
                cityCache.Clear();
                cachedScenario = scenario;
            }

            // 【回合号】由 Force.OnForceTurnStart 每回合推进一次（DeploymentState.BeginForceTurn）：
            // 同一势力的所有军团**共享**同一个回合号 —— 否则一个势力有 N 个军团时回合号会被加 N 次，
            // 防抖(debounceTurns)与前期加成(earlyGameTurns)的标度全部失真。
            int turn = DeploymentState.CurrentForceTurn(force.Id);
            int scopeKey = DeploymentState.ScopeKey(force, scope);

            // 【前期加成】在编制之前写入当前作用域的回合号，供编制层判断"开局前 N 回合"。
            DeploymentState.currentForceTurn = turn;
            DeploymentState.transferCountThisTurn = 0;

            plan.forceId = force.Id;
            plan.forceName = force.Name;
            plan.corpsId = scope != null ? scope.Id : 0;
            plan.corpsName = CorpsLabel(scope);
            plan.adviceOnly = dryRun;

            // ---------- 1) 采集本势力城池 + 编制 ----------
            List<PostTask> tasks = new List<PostTask>();
            List<City> cityList = new List<City>();
            int personTotal = 0;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force)
                    continue;
                // 【军团边界】军团作用域：只统计本军团的城。
                // 港关的 BelongCorps 由 Force.CreateCorps 设为所属都市的军团，因此会随都市一起被纳入。
                if (scope != null)
                {
                    Corps cityCorps = city.BelongCorps;
                    if (cityCorps == null)
                    {
                        // 归属缺失（异常数据 / 刚易手的瞬间）→ 归第一军团兜底，避免被所有作用域漏掉
                        if (force.CapitalCorps != scope) continue;
                    }
                    else if (cityCorps != scope)
                        continue;
                }

                CitySituation situation = CitySituation.Collect(city, scenario);
                int threatLevel = BattleSituation.EvaluateCityThreat(city).threatLevel;

                // 观察真实兵力 / 资源（慢速均值）：供"预想值"修正用，每城每回合一次
                DeploymentState.Observe(city, weights, situation);

                cityList.Add(city);
                personTotal += city.allPersons != null ? city.allPersons.Count : 0;

                if (situation.isUnderSiege)
                {
                    // 被围城本回合不作为接收目标（只上报原因，便于核对）
                    plan.unmet.Add(string.Format("{0} 被围: 本回合不接收调动", city.Name));
                    continue;
                }

                int expectedTroops = DeploymentState.GetExpectedTroops(city, weights, situation);
                List<Post> posts = GetPosts(city, situation, threatLevel, expectedTroops, weights, personTotal);

                float devGap = CityEstablishment.CalcDevelopGap(city);

                // 港关驻军关闭：本军团的港 / 关岗位标记为"只许本城在岗"，不参与外调
                bool localOnlyPosts = !allowPortGateGarrison && (city.IsPort() || city.IsGate());

                // 【补到最低人数】本城人数低于非港关最低保底（< minCityPersons，默认 3）时，
                // 只把"按优先级排在最前的 N 个岗位"当成补人目标（N = 还差多少才到 3）：
                //   · 这些岗位**排到最前拿人**（比"更靠边境"更急）；
                //   · **免跨圈层 / 行程成本**（见 Score）—— 一座 0~2 人的城连征兵 / 运输 / 内政
                //     都开不了工，"回撤贵"这类效率考量不该让它永远空着；
                //   · 门槛降到 0（见 scoreFloor）—— 宁可要个弱将。
                // 只补到 3 为止：编制里其余的岗仍按常规竞争，免得一次性抽走一队人去填满后方城。
                // 港关不参与（它们的保底是 minPortGateGuard，且不产内政）。
                int inCity = city.allPersons != null ? city.allPersons.Count : 0;
                int needToMin = (!city.IsPort() && !city.IsGate())
                    ? Math.Max(0, Math.Max(1, weights.minCityPersons) - inCity)
                    : 0;

                for (int j = 0; j < posts.Count; j++)
                {
                    PostTask task;
                    task.post = posts[j];
                    task.threatLevel = threatLevel;
                    task.devGap = devGap;
                    task.underSiege = false;
                    task.localOnly = localOnlyPosts;
                    // 岗位表本身按优先级生成（守备 → 军事 → 征兵 → 军备 → …）→ 前 N 个就是最要紧的 N 个
                    task.lowPop = j < needToMin;
                    tasks.Add(task);
                }
            }

            // ---------- 1.5) 编制总量约束：岗位总数不该远超势力人力 ----------
            // 否则每座城都停在"岗位 10 / 在岗 3"的空缺态：分布效果被稀释、报告看不出重点，
            // 稀缺人手也会被大量低价值岗位摊薄。
            //
            // 裁撤方式是**按城轮转**：每轮每座城各裁掉 1 个"该城优先级最低"的岗位，直到裁够。
            // 为什么不是"整体排序后截尾"：那等于让 city id 当最后的裁决 ——
            // 同样在前线、同样 10 万兵的城，id 排最后的会被砍到只剩硬性岗（"1 人守城"），
            // id 靠前的却一个不少。总量不足时应当"各城等比缩减"，而不是由 id 决定生死。
            // 硬性岗（Post.required）永不裁撤：每城至少保住硬性军事岗 / 港关守备。
            // 人数低于最低保底（minCityPersons）的城同样不裁 —— 它的岗位是"补人到 3"的需求信号。
            int postBudget = personTotal > 0
                ? (int)Math.Floor(personTotal * Math.Max(0.1f, weights.maxPostsPerPerson))
                : int.MaxValue;
            if (tasks.Count > postBudget)
            {
                Dictionary<int, int> cutRings = new Dictionary<int, int>();
                for (int i = 0; i < cityList.Count; i++)
                {
                    if (cityList[i] != null)
                        cutRings[cityList[i].Id] = CityEstablishment.ResolveRing(cityList[i]);
                }

                // 组内按"保留优先"排序（硬性 → 优先级 → 军事岗边境优先 / 内政岗缺口优先 → 稳定兜底），
                // 于是每城列表的**尾部就是该城最不优先的岗位**，裁撤从尾部取。
                tasks.Sort(delegate (PostTask a, PostTask b)
                {
                    return CompareForAssign(a, b, cutRings);
                });

                Dictionary<int, List<int>> byCity = new Dictionary<int, List<int>>();
                for (int i = 0; i < tasks.Count; i++)
                {
                    List<int> list;
                    if (!byCity.TryGetValue(tasks[i].post.cityId, out list))
                    {
                        list = new List<int>();
                        byCity[tasks[i].post.cityId] = list;
                    }
                    list.Add(i);
                }

                List<int> cutCityIds = new List<int>(byCity.Keys);
                cutCityIds.Sort();                       // 固定顺序 → 结果可复现

                bool[] cut = new bool[tasks.Count];
                int need = tasks.Count - postBudget;
                int cutCount = 0;
                while (cutCount < need)
                {
                    bool progress = false;
                    for (int c = 0; c < cutCityIds.Count && cutCount < need; c++)
                    {
                        List<int> list = byCity[cutCityIds[c]];
                        // 从该城尾部（最不优先）往回找第一个"可裁"的岗位
                        for (int k = list.Count - 1; k >= 0; k--)
                        {
                            int idx = list[k];
                            if (cut[idx]) continue;
                            // 【人手极缺城不裁】在城人数低于最低保底（< minCityPersons）的城，
                            // 它的岗位就是"要把人补到 3"的需求信号（见 PostTask.lowPop）——
                            // 被裁掉等于取消这条需求，这类城会永远停在 0~2 人（运不走货、造不出兵）。
                            // 代价可忽略：它们总共也就 3 个岗位（编制上限本身就按在城人数夹过）。
                            if (tasks[idx].lowPop) break;
                            if (tasks[idx].post.required) break;   // 该城只剩硬性岗 → 本轮换下一个城
                            cut[idx] = true;
                            cutCount++;
                            progress = true;
                            break;
                        }
                    }
                    if (!progress) break;                // 可裁的只剩硬性岗 → 停止（编制会略多于预算）
                }

                List<PostTask> kept = new List<PostTask>(tasks.Count - cutCount);
                for (int i = 0; i < tasks.Count; i++)
                {
                    if (!cut[i]) kept.Add(tasks[i]);
                }
                tasks = kept;

                // 恢复"按城聚集"的顺序，报告才能按城成组输出
                tasks.Sort(delegate (PostTask a, PostTask b)
                {
                    return a.post.cityId.CompareTo(b.post.cityId);
                });
            }

            // ---------- 1.6) 每城"还缺多少人"的账（调出额度的分母） ----------
            // 一座城只有在**自己的岗位都填满之后**，多出来的人才算真富余、才允许往别处调。
            //   postCountByCity = 该城岗位数（裁撤后的最终编制）
            //   filledByCity    = 该城已填数（本城在岗 + 外调到位），随填充实时递增
            Dictionary<int, int> postCountByCity = new Dictionary<int, int>();
            for (int i = 0; i < tasks.Count; i++)
                BumpCount(postCountByCity, tasks[i].post.cityId);

            // 【性能】城 id → cityList 下标：原先每个岗位都要线性扫一遍 cityList（IndexOfCity），
            // 在"岗位 × 候选人"量级下累计可观。cityList 在阶段 1 之后不再变化，建一次字典即可。
            Dictionary<int, int> cityIndexById = new Dictionary<int, int>(cityList.Count);
            for (int i = 0; i < cityList.Count; i++)
            {
                if (cityList[i] != null)
                    cityIndexById[cityList[i].Id] = i;
            }

            Dictionary<int, int> filledByCity = new Dictionary<int, int>();

            // ---------- 2) 岗位分两批：前线 / 硬性岗优先，其余岗（后方军事 / 内政族）回填 ----------
            // 【为什么要分阶段】旧实现无条件"先用本城在册人员填满**所有**岗位"：
            // 后方城的军事 / 开发岗会先把本城武将锁进 usedPersons（这些岗位大多没有真实产出），
            // 于是"可调动池"被大量无效占位吃掉 —— 表现就是"全势力几百号人闲着，前线却抽不到人"。
            // 现在按优先级分两批，并把"第二批的本城回填"推迟到"第一批外调之后"，
            // 让前线先把需要的人从池里拿走，后方再回填剩余，全局人力优先投向吃紧方向。
            //
            //   阶段 A：硬性岗 + 前线/次前线军事岗 → 本城在册填充（填不上的进 pending）
            //   阶段 B：把阶段 A 的 pending 从可调动池外调
            //   阶段 C：其余岗位（后方军事 / 内政族）→ 本城在册回填
            //   阶段 D：把阶段 C 的 pending 从可调动池外调
            //
            // 边境优先原则：先把每座城的圈层查出来（0 = 边境，越小越靠前；【R1】港关按归属都市）
            Dictionary<int, int> ringOfCity = new Dictionary<int, int>();
            for (int i = 0; i < cityList.Count; i++)
            {
                if (cityList[i] != null)
                    ringOfCity[cityList[i].Id] = CityEstablishment.ResolveRing(cityList[i]);
            }

            List<PostTask> primaryTasks = new List<PostTask>();
            List<PostTask> secondaryTasks = new List<PostTask>();
            for (int i = 0; i < tasks.Count; i++)
            {
                PostTask t = tasks[i];
                bool isMil = t.post.kind == PostKind.Military || t.post.kind == PostKind.Garrison;
                bool frontMil = isMil && RingOf(ringOfCity, t.post.cityId) <= 1;
                if (t.post.required || frontMil)
                    primaryTasks.Add(t);
                else
                    secondaryTasks.Add(t);
            }

            // 两批各自按"硬性优先 → 岗位优先级 → 军事岗边境优先 / 内政岗缺口优先 → 稳定兜底"排序
            primaryTasks.Sort(delegate (PostTask a, PostTask b)
            {
                return CompareForAssign(a, b, ringOfCity);
            });
            secondaryTasks.Sort(delegate (PostTask a, PostTask b)
            {
                return CompareForAssign(a, b, ringOfCity);
            });

            // 【人力占用分两类 —— 这是"池子被吃空"的根治点】
            //   · occupied    —— 被**本城在岗**记账占用的人。在岗没有真实任务下发，只是"本城有人可用"，
            //                    所以它**不禁止**别人来抢：外调时可以把人从"在岗"状态抢走。
            //   · transferred —— 本回合已经真正外调走的人，谁都不能再用。
            // 旧实现把两者混在一个 usedPersons 里，于是出现"势力里 90 个空闲武将，可调动池只剩 20"：
            // 虚高的军事岗把本城人全锁成"在岗"，后方的内政岗反而无人可派。
            HashSet<int> occupied = new HashSet<int>();
            HashSet<int> transferred = new HashSet<int>();
            HashSet<int> revokedPersons = new HashSet<int>();   // 被外调抢走的"在岗"记录（报告前统一剔除）
            List<PostTask> primaryPending = new List<PostTask>();
            List<PostTask> secondaryPending = new List<PostTask>();

            // 用本城在册人员填充一批岗位：成功后写入 fillings，填不上的进对应批次的 pending。
            // 在册池：户口在本城、不在部队、非俘非亡（含正在执行内政任务者 —— 与旧行为一致）。
            System.Action<List<PostTask>, List<PostTask>> fillFromLocal =
                delegate (List<PostTask> batch, List<PostTask> batchPending)
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    PostTask task = batch[i];
                    int cityIndex;
                    if (!cityIndexById.TryGetValue(task.post.cityId, out cityIndex))
                        continue;

                    City city = cityList[cityIndex];
                    Person best = null;
                    float bestScore = float.MinValue;

                    foreach (Person p in city.allPersons)
                    {
                        if (p == null || p.Id == 0) continue;
                        if (occupied.Contains(p.Id) || transferred.Contains(p.Id)) continue;
                        if (p.IsDead || p.IsPrisoner) continue;
                        if (p.mBelongTroop != null) continue;
                        // 【改动 C · 军事岗能力下限】统率 + 武力不达标的人不派去带兵（硬性岗除外）
                        if (!MeetsMilitaryFloor(p, task.post, weights)) continue;

                        float score = Fit(task.post.weights, p);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = p;
                        }
                    }

                    if (best != null)
                    {
                        occupied.Add(best.Id);
                        plan.fillings.Add(MakeFilling(task, best, bestScore, true, city, null));
                        BumpCount(filledByCity, task.post.cityId);      // 本城该岗位已填 → 空缺 −1
                    }
                    else if (!task.localOnly)
                    {
                        batchPending.Add(task);
                    }
                    // localOnly（港关驻军关闭的港 / 关岗位）：填不满也不进外调批次 ——
                    // 岗位空缺照常保留（于是港关的"净富余"不会被算高、自己的人不会被当富余调走），
                    // 但 AI 不会为了补它而向港关调人。
                }
            };

            // 阶段 A：前线 / 硬性岗先用本城人满足（缺口交给阶段 B 外调）
            fillFromLocal(primaryTasks, primaryPending);

            // ---------- 3) 剩余岗位：从可调动池（空闲武将）外调 ----------
            List<Person> pool = new List<Person>();
            int scopePersons = 0;                        // 本作用域在册武将总数（含在部队 / 有任务的人）
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person p = scenario.personSet[i];
                if (p == null || p.Id == 0) continue;
                if (p.BelongForce != force) continue;
                // 【军团边界】军团级作用域的抽调池只含本军团的人 —— 绝不跨军团抽人
                //（跨团调人会打乱玩家自己排好的部署；军团之间的隔离是刻意的）
                if (scope != null && !InCorps(p, scope, force)) continue;
                if (p.IsDead || p.IsPrisoner) continue;
                scopePersons++;                          // 在册：活着的本势力（本军团）武将
                if (!p.IsFree) continue;                 // 部队中 / 有任务 / 在途 → 不可再分配
                // 【改动 B】只排除"已外调"的人；"本城在岗"的人**仍然入池** ——
                // 在岗只是记账，别人更需要他时可以被抢走（抢占会扣 stealLocalCost 成本）。
                if (transferred.Contains(p.Id)) continue;
                pool.Add(p);
            }
            // 可调动池 = 全势力的机动人力（含"在岗"的人）——这是"能派出去干活的人"的真实上限
            plan.personPool = pool.Count;

            // ---------- 3b) 全局调动额度：按**在册总人数**缩放 ----------
            // 固定 12 人的额度对上百人的势力太小（上百个岗位缺口要卡几十回合），
            // 对十几人的小势力又偏大。按比例缩放后节奏与规模自洽：
            // 额度 = max(配置值, ⌈在册总人数 × maxTransferPerTurnRatio⌉)，默认比例 0.25
            // —— 也就是"100 人至少能调动 25 人"；配置值是**下限**，小势力不被缩小。
            int globalQuota = GlobalTransferQuota(weights, scopePersons);
            plan.personTotal = scopePersons;
            plan.transferQuota = globalQuota;

            // 推荐队伍要人：把"队伍成员 / 特技持有者"摊平成 id 集合，供下面打分 O(1) 查询
            currentTeamDemand = TroopTeamDemand.Build(pool);

            // 真正执行的条件：全局不是影子模式，且本作用域不是"仅参考"
            bool execute = !weights.shadowOnly && !dryRun;

            // 闸门/防抖否决过的人：本回合不再尝试。
            // 否则"全势力只有 1-2 个空闲武将"时，每个岗位都会选中同一个人、被同一条闸门否掉，
            // 既刷爆日志，也让**次优人选永远轮不到**。
            HashSet<int> blocked = new HashSet<int>();
            Dictionary<int, string> deniedNames = new Dictionary<int, string>();
            Dictionary<int, string> deniedReasons = new Dictionary<int, string>();
            Dictionary<int, int> deniedCounts = new Dictionary<int, int>();

            // 【日志可读性修正】"配额限流"与"全员冷却"是**系统性原因**，不是"没人可用"。
            // 若按岗位逐行输出，同一座城的 6 个岗位会刷 6 行"空缺: 无可调动人选"，
            // 而实际可调动池里可能还有上百人 —— 极易误读。这里按城聚合成一行。
            Dictionary<int, int> quotaStalled = new Dictionary<int, int>();
            Dictionary<int, int> quotaStalledLimit = new Dictionary<int, int>();
            Dictionary<int, int> coolingStalled = new Dictionary<int, int>();
            Dictionary<int, int> scoreStalled = new Dictionary<int, int>();
            Dictionary<int, int> vacantStalled = new Dictionary<int, int>();
            Dictionary<int, int> sourceStalled = new Dictionary<int, int>();
            Dictionary<int, string> stalledCityNames = new Dictionary<int, string>();
            HashSet<int> exhaustedSources = new HashSet<int>();   // 本回合"已调出够多"的源城
            bool globalQuotaUsed = false;                         // 作用域级调动总额度是否已用完

            // 阶段 B / 阶段 D：两批 pending 各自外调。
            // 两批共用同一套 blocked / 配额 / 统计状态 —— 同一回合内的约束对两批一视同仁。
            for (int phase = 0; phase < 2; phase++)
            {
                if (phase == 1)
                {
                    // 阶段 C：其余岗位（后方军事 / 内政族）用本城在册回填，缺口交给阶段 D
                    fillFromLocal(secondaryTasks, secondaryPending);
                }

                List<PostTask> batch = phase == 0 ? primaryPending : secondaryPending;

                for (int i = 0; i < batch.Count; i++)
                {
                    PostTask task = batch[i];
                    int cityIndex;
                    if (!cityIndexById.TryGetValue(task.post.cityId, out cityIndex))
                        continue;
                    City city = cityList[cityIndex];

                    string kindName = DeploymentPlan.KindName(task.post.kind);

                    // 作用域级总额度（按在册总人数缩放，见上）：本回合调动幅度已够
                    // → 剩余岗位下回合继续（避免一次大搬家）
                    if (!DeploymentState.CanTransferGlobal(scopeKey, turn, globalQuota))
                    {
                        globalQuotaUsed = true;
                        break;
                    }

                    // 目标城接收额度：前线 / 高威胁城额外放宽，让缺口最大的城优先补齐
                    int recvLimit = RecvLimitFor(city, task.threatLevel, weights);

                    // 同一岗位要一直试到有人可派：最优人选被否掉就退而求其次
                    Person chosen = null;
                    float chosenScore = 0f;
                    int chosenFromCity = 0;         // 选中者的源城 id（必须在 Transfer 之前记下来）
                    bool coolingOnly = false;
                    bool scoreBlocked = false;      // 是否因"最优人选低于门槛"而放弃

                    // 门槛：硬性岗与"补到最低人数"的岗为 0（宁可要个弱将，也不能让城空着）
                    float scoreFloor = (task.post.required || task.lowPop) ? 0f : weights.minTransferScore;

                    // 【性能】目标城的圈层与"驻军特技集合"在本岗位的所有候选人之间是常量，
                    // 提前算一次，避免在"每人每次评分"里重复计算（见 Score / FeatureNovelty）。
                    int destRing = CityEstablishment.ResolveRing(city);
                    FillDestFeatureSet(city, destFeatureScratch);

                    while (true)
                    {
                        // 【两轮挑人 · 改动 B】
                        //   第一轮：只看**真正的机动人力**（没被"在岗"记账占用的空闲人）；
                        //   第二轮：才允许**抢占"在岗"的人**（扣 stealLocalCost）。
                        // 人力优先来自真空闲，只有明显更划算时才去动别人城里的"在岗"记账。
                        float bestScore;
                        Person best = ScanBest(task.post, city, pool, transferred, blocked, exhaustedSources,
                            occupied, false, task.lowPop, weights, destRing, destFeatureScratch, out bestScore);
                        if (best == null || bestScore < scoreFloor)
                        {
                            float stealScore;
                            Person steal = ScanBest(task.post, city, pool, transferred, blocked, exhaustedSources,
                                occupied, true, task.lowPop, weights, destRing, destFeatureScratch, out stealScore);
                            if (steal != null && stealScore > bestScore)
                            {
                                best = steal;
                                bestScore = stealScore;
                            }
                        }

                        if (best == null)
                            break;

                        // 【最低分门槛】非硬性岗：净收益低于门槛就**不调**。
                        // 匹配分 = 能力适配 − 行程成本 − 跨圈层成本，负分 = "搬过去反而更差"
                        // （白耗行程、打断武将正在做的内政、还触发防抖）。硬性岗不受此门槛约束。
                        if (bestScore < scoreFloor)
                        {
                            scoreBlocked = true;
                            break;
                        }

                        // 防抖：刚被调过的武将本回合不再动（本人整体跳过）
                        if (!DeploymentState.CanTransferNow(best.Id, turn, weights.debounceTurns))
                        {
                            blocked.Add(best.Id);
                            coolingOnly = true;
                            continue;
                        }

                        // 合法性闸门（影子模式同样判定 → 报告里能看到"会被谁否决"）
                        DeploymentExecutor.Gate gate = DeploymentExecutor.CanTransfer(best, city, weights, dryRun);
                        if (!gate.allowed)
                        {
                            blocked.Add(best.Id);
                            int c;
                            deniedCounts.TryGetValue(best.Id, out c);
                            deniedCounts[best.Id] = c + 1;
                            deniedNames[best.Id] = best.Name;
                            deniedReasons[best.Id] = gate.reason;
                            continue;
                        }

                        // 目标城接收配额：本回合已接收够多 → 该城其余岗位下回合继续（不是没人）
                        if (!DeploymentState.CanReceive(scopeKey, turn, city.Id, recvLimit))
                            break;

                        // 源城调出配额：额度按**净富余**（空闲 − 本城未填岗位）动态取值。
                        //   自己的缺口还没补满（净富余 ≤ 0）→ **一个人都不许调出**，先保住本城；
                        //   富余越多额度越大：净富余 ÷ sendSurplusPerSeat(默认 2) + 1，即**最多借一半**。
                        // 注意：CanSend 把 limit ≤ 0 当作"不限制"，所以 0 必须在这里单独判掉。
                        int fromCity = best.BelongCity != null ? best.BelongCity.Id : 0;
                        int sendLimit = SendLimitFor(best.BelongCity,
                            VacancyOfCity(fromCity, postCountByCity, filledByCity), weights);
                        if (sendLimit <= 0 || !DeploymentState.CanSend(scopeKey, turn, fromCity, sendLimit))
                        {
                            exhaustedSources.Add(fromCity);
                            continue;
                        }

                        chosen = best;
                        chosenScore = bestScore;
                        chosenFromCity = fromCity;
                        break;
                    }

                    if (chosen == null)
                    {
                        if (!DeploymentState.CanReceive(scopeKey, turn, city.Id, recvLimit))
                        {
                            int n;
                            quotaStalled.TryGetValue(city.Id, out n);
                            quotaStalled[city.Id] = n + 1;
                            quotaStalledLimit[city.Id] = recvLimit;
                            stalledCityNames[city.Id] = city.Name;
                        }
                        else if (scoreBlocked)
                        {
                            // 有候选人，但净收益低于门槛 → "不值得调"，不是"没人"
                            int n;
                            scoreStalled.TryGetValue(city.Id, out n);
                            scoreStalled[city.Id] = n + 1;
                            stalledCityNames[city.Id] = city.Name;
                        }
                        else if (coolingOnly && pool.Count > 0 && blocked.Count >= pool.Count)
                        {
                            // 整个可调动池都在冷却期 → 系统性原因，按城汇总
                            int n;
                            coolingStalled.TryGetValue(city.Id, out n);
                            coolingStalled[city.Id] = n + 1;
                            stalledCityNames[city.Id] = city.Name;
                        }
                        else if (coolingOnly)
                        {
                            // 部分候选在冷却期、其余被否 → 仍属"节奏控制"，按城汇总
                            int n;
                            coolingStalled.TryGetValue(city.Id, out n);
                            coolingStalled[city.Id] = n + 1;
                            stalledCityNames[city.Id] = city.Name;
                        }
                        else if (HasExhaustedOnlyCandidate(pool, transferred, blocked, exhaustedSources))
                        {
                            // 池子里**还有**人，只是他们所属的源城本回合已经"送够人"了
                            // （每城每回合的调出额度）。含义与"真的没人"完全不同：下回合就能动。
                            int n;
                            sourceStalled.TryGetValue(city.Id, out n);
                            sourceStalled[city.Id] = n + 1;
                            stalledCityNames[city.Id] = city.Name;
                        }
                        else
                        {
                            // 池中确实没有可用人选（池子被本城在岗 / 前序岗位吃光了）
                            int n;
                            vacantStalled.TryGetValue(city.Id, out n);
                            vacantStalled[city.Id] = n + 1;
                            stalledCityNames[city.Id] = city.Name;
                        }
                        continue;
                    }

                    // 调动会把武将的户口改到目标城，源城名必须在执行前记下来（报告用）
                    string fromName = chosen.BelongCity != null ? chosen.BelongCity.Name : null;

                    string deny;
                    bool done = DeploymentExecutor.Transfer(chosen, city, weights, execute, turn, dryRun, out deny);
                    if (execute && !done)
                    {
                        plan.unmet.Add(string.Format("{0} {1} 执行失败: {2}", city.Name, kindName, deny));
                        continue;
                    }

                    // 【改动 B】这个人原本在别的城的"在岗"记账里 → 那条记账作废（人已去更缺人的地方），
                    // 该岗位在报告里会回到"空缺"。作废记录统一在报告前剔除。
                    if (occupied.Contains(chosen.Id))
                    {
                        revokedPersons.Add(chosen.Id);
                        // 源城那条"在岗"记账作废 → 它的已填数要减回去。
                        // 否则源城空缺被低估、净富余被高估，它会持续把本城需要的人继续送出去。
                        int prevFilled;
                        if (filledByCity.TryGetValue(chosenFromCity, out prevFilled) && prevFilled > 0)
                            filledByCity[chosenFromCity] = prevFilled - 1;
                    }

                    occupied.Remove(chosen.Id);
                    transferred.Add(chosen.Id);

                    DeploymentState.MarkReceive(scopeKey, turn, city.Id);
                    // 【必须用 Transfer 之前记下的源城 id】Transfer 内部会把 BelongCity 改到目标城，
                    // 这里若再读 chosen.BelongCity 就把"调出"记到了目标城头上 ——
                    // 后果是源城的调出配额**永远统计不到**（CanSend 永远放行），
                    // 一个城可以在一回合内被抽走多人（日志里柴桑 / 乌林港各被抽 3 人即此因）。
                    DeploymentState.MarkSend(scopeKey, turn, chosenFromCity);
                    DeploymentState.MarkTransferGlobal(scopeKey, turn);
                    if (execute)
                        DeploymentState.transferCountThisTurn++;

                    plan.fillings.Add(MakeFilling(task, chosen, chosenScore, false, city, fromName));
                    BumpCount(filledByCity, city.Id);                   // 目标城该岗位已填 → 空缺 −1
                }
            }

            // 闸门否决按人汇总一行（避免同一个被锁死的武将刷满整屏）
            foreach (KeyValuePair<int, int> kv in deniedCounts)
            {
                string name, reason;
                deniedNames.TryGetValue(kv.Key, out name);
                deniedReasons.TryGetValue(kv.Key, out reason);
                plan.unmet.Add(string.Format("闸门否决汇总: {0} 被拒 {1} 次（原因: {2}）", name, kv.Value, reason));
            }

            // ---------- 空缺归因：**每座城合并成一行** ----------
            // 四类原因分别统计后一次输出：
            //   · 接收额度用完 —— 节奏控制（下回合继续）
            //   · 净收益不足   —— 池里有候选人，但搬过去不划算（匹配分低于门槛）
            //   · 池中无可用   —— 池子被"本城在岗 / 前序岗位 / 冷却"吃光，确实没人可派
            //   · 冷却期       —— 候选都在防抖期内
            // 原来这四类是四行（甚至逐岗位一行），一座城能刷十几行；现在一城一行。
            List<int> stalledCityIds = new List<int>();
            AddKeys(quotaStalled, stalledCityIds);
            AddKeys(scoreStalled, stalledCityIds);
            AddKeys(coolingStalled, stalledCityIds);
            AddKeys(vacantStalled, stalledCityIds);
            AddKeys(sourceStalled, stalledCityIds);

            for (int i = 0; i < stalledCityIds.Count; i++)
            {
                int cityId = stalledCityIds[i];
                int q = CountOf(quotaStalled, cityId);
                int s = CountOf(scoreStalled, cityId);
                int c = CountOf(coolingStalled, cityId);
                int v = CountOf(vacantStalled, cityId);
                int e = CountOf(sourceStalled, cityId);

                string cname;
                stalledCityNames.TryGetValue(cityId, out cname);

                StringBuilder why = new StringBuilder();
                if (q > 0)
                {
                    int limit;
                    quotaStalledLimit.TryGetValue(cityId, out limit);
                    if (limit <= 0) limit = weights.maxTransferPerCityPerTurn;
                    AppendReason(why, string.Format("接收额度已用完 {0} 个（上限 {1} 人/回合）", q, limit));
                }
                if (s > 0)
                    AppendReason(why, string.Format("净收益不足 {0} 个（门槛 {1:F2}）", s, weights.minTransferScore));
                if (e > 0)
                    AppendReason(why, string.Format("源城无可调富余 {0} 个（本城缺口未补满，或本回合调出额度已用完，下回合可继续）", e));
                if (v > 0)
                    AppendReason(why, string.Format("池中确实无可用人选 {0} 个", v));
                if (c > 0)
                    AppendReason(why, string.Format("候选在冷却期 {0} 个（{1} 回合）", c, weights.debounceTurns));

                plan.unmet.Add(string.Format("{0}: {1} 个岗位待补 —— {2}", cname, q + s + c + v + e, why));
            }

            if (globalQuotaUsed)
            {
                plan.unmet.Add(string.Format(
                    "本作用域本回合调动总额度 {0} 人已用完（在册 {1} 人 × {2:P0} 缩放），剩余岗位下回合继续",
                    globalQuota, scopePersons,
                    weights.maxTransferPerTurnRatio > 0f ? weights.maxTransferPerTurnRatio : 0f));
            }

            // 【改动 B】剔除被"外调抢占"作废的在岗记账：那些岗位回到空缺（人已调去更缺人的城）。
            // 必须在统计"在岗 / 空缺"与"各城人力分布"**之前**清理，否则报告里的数字对不上。
            if (revokedPersons.Count > 0)
            {
                plan.fillings.RemoveAll(delegate (PostFilling f)
                {
                    return f.local && revokedPersons.Contains(f.personId);
                });
            }

            // 岗位总表：**按城顺序**重建（分阶段指派会打乱 tasks 顺序，报告需要按城成组输出）
            for (int c = 0; c < cityList.Count; c++)
            {
                City cc = cityList[c];
                if (cc == null) continue;
                for (int i = 0; i < tasks.Count; i++)
                {
                    if (tasks[i].post.cityId == cc.Id)
                        plan.posts.Add(tasks[i].post);
                }
            }

            // 【诊断】每城人力分布：分辨"人不在城"与"人在城但被部队/任务占用"。
            //   · 在册 = allPersons（户口在城的所有人）
            //   · 空闲 = freePersons（能派出去干活的：无部队、无任务、非俘非亡）
            //   · 在部队 = allPersons 中 mBelongTroop != null 的人
            //   · 在城 = 在册 − 在部队（能接收城内命令的那部分）
            // 同时列出岗位侧（岗位 / 在岗 / 外调），一眼看出这座城是"人力囤积"
            // （岗位 << 空闲）还是"人手不足"（在岗 << 岗位）。
            for (int i = 0; i < cityList.Count; i++)
            {
                City ci = cityList[i];
                if (ci == null) continue;

                int total = ci.allPersons != null ? ci.allPersons.Count : 0;
                int freeCnt = ci.freePersons != null ? ci.freePersons.Count : 0;
                int inTroopCnt = 0;
                if (ci.allPersons != null)
                {
                    for (int j = 0; j < ci.allPersons.Count; j++)
                    {
                        Person pj = ci.allPersons[j];
                        if (pj != null && pj.mBelongTroop != null)
                            inTroopCnt++;
                    }
                }

                int postCnt = 0, localCnt = 0, outCnt = 0;
                for (int k = 0; k < plan.posts.Count; k++)
                {
                    if (plan.posts[k].cityId == ci.Id) postCnt++;
                }
                for (int k = 0; k < plan.fillings.Count; k++)
                {
                    if (plan.fillings[k].post.cityId != ci.Id) continue;
                    if (plan.fillings[k].local) localCnt++;
                    else if (plan.fillings[k].personId > 0) outCnt++;
                }

                plan.cityInfo.Add(string.Format("{0} 在册{1} 空闲{2} 在部队{3} 在城{4} | 岗位{5} 在岗{6} 外调{7}",
                    ci.Name, total, freeCnt, inTroopCnt, total - inTroopCnt, postCnt, localCnt, outCnt));
            }

            return plan;
        }

        /// <summary>
        /// 单轮挑选：在可调动池里为某个岗位找最优人选。
        ///
        /// 分两轮使用（见调用处）：
        ///   · <paramref name="onlyOccupied"/> = false → 只看**真正的机动人力**（没被"在岗"记账占用）；
        ///   · <paramref name="onlyOccupied"/> = true  → 只看**"在岗"的人**（抢占轮，分数扣 <c>stealLocalCost</c>）。
        /// </summary>
        /// <param name="post">待填岗位</param>
        /// <param name="dest">目标城</param>
        /// <param name="pool">可调动池</param>
        /// <param name="transferred">本回合已外调的人（不可再用）</param>
        /// <param name="blocked">本回合已被否决的人（不再尝试）</param>
        /// <param name="exhaustedSources">本回合已"送够人"的源城</param>
        /// <param name="occupied">被"本城在岗"记账占用的人</param>
        /// <param name="onlyOccupied">true = 只扫在岗的人（抢占轮）</param>
        /// <param name="lowPopDest">true = 本岗位属于"把该城补到最低人数"的岗（免行程 / 跨圈层成本）</param>
        /// <param name="weights">部署参数</param>
        /// <param name="bestScore">返回最优人选的分（无人选时为 float.MinValue）</param>
        /// <returns>最优人选；无合适人选时为 null</returns>
        static Person ScanBest(Post post, City dest, List<Person> pool, HashSet<int> transferred,
            HashSet<int> blocked, HashSet<int> exhaustedSources, HashSet<int> occupied,
            bool onlyOccupied, bool lowPopDest, DeploymentWeights weights,
            int destRing, HashSet<int> destFeatures, out float bestScore)
        {
            Person best = null;
            bestScore = float.MinValue;

            for (int j = 0; j < pool.Count; j++)
            {
                Person p = pool[j];
                if (p == null) continue;
                if (transferred.Contains(p.Id) || blocked.Contains(p.Id)) continue;

                bool isOccupied = occupied.Contains(p.Id);
                if (onlyOccupied != isOccupied) continue;

                if (p.BelongCity != null && exhaustedSources.Contains(p.BelongCity.Id)) continue;
                // 【已在目标城 → 无需调动】本城人不必（也不能）外调到本城，闸门同样会以"已在目标城"否决。
                // 必须在**挑人阶段**就排除，否则"抢占在岗"那一轮会把本城在岗记账的人再次提名为
                // **同一座城的另一个岗位**，被闸门拒掉后还塞进 blocked（本回合彻底出局）：
                // 既白刷一遍"闸门否决汇总"，也让该城的在岗记录看起来像出了问题。
                if (p.BelongCity == dest) continue;
                // 军事 / 守备岗的能力下限（统率+武力）：硬性岗与"补到最低人数"的岗同样豁免 ——
                // 一座 0~2 人的城宁可先由文官顶着，也不能连 3 个人都凑不齐。
                if (!MeetsMilitaryFloor(p, post, weights) && !lowPopDest) continue;

                float score = Score(p, post, dest, lowPopDest, weights, destRing);
                // 特技组合搭配：军事 / 守备岗优先派"能带来本城还没有的特技"的武将
                if (post.kind == PostKind.Military || post.kind == PostKind.Garrison)
                {
                    score += FeatureNovelty(p, destFeatures, weights);
                    // 推荐队伍要人：把队员 / 特技持有者优先送往前线，配合 AI 出征组队
                    score += RecommendedTeamBonus(p, destRing, weights);
                }
                // 抢占"在岗"的人要付出额外成本：优先用真正的机动人力
                if (isOccupied)
                    score -= weights.stealLocalCost;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// 【改动 C】军事 / 守备岗的能力下限：`统率 + 武力` 低于 <c>militaryMinAbility</c> 的人不派去带兵。
        ///
        /// 用来挡住"纯文官 / 文弱武将当将军"这类明显不合理的指派（例如把大乔派去军事岗）。
        /// **硬性军事岗（<c>Post.required</c>）不受此限制** —— 前线必备岗宁可要个弱将也不能空缺。
        /// </summary>
        /// <param name="p">人选</param>
        /// <param name="post">岗位</param>
        /// <param name="weights">部署参数</param>
        static bool MeetsMilitaryFloor(Person p, Post post, DeploymentWeights weights)
        {
            if (p == null || weights == null) return true;
            if (weights.militaryMinAbility <= 0) return true;
            if (post.kind != PostKind.Military && post.kind != PostKind.Garrison) return true;
            if (post.required) return true;
            return p.Command + p.Strength >= weights.militaryMinAbility;
        }

        /// <summary>
        /// 池中剩下的候选是否**全部**来自"源城调出额度已用完"的城。
        ///
        /// 用于把空缺归因从"池中无可用人选"里细分出来 —— 两者含义完全不同：
        /// 前者下回合就能继续调（源城每回合的调出额度会重置），后者是真的没人可派。
        ///
        /// 【必须是"全部"】旧实现碰到第一个这样的人就返回 true，于是只要池里还有**任何**
        /// 一座已用尽额度的城的闲人（几乎必然有），每条空缺都会被写成
        /// "源城无可调富余"—— 真正的原因（例如"军事岗能力下限挡掉了文官"）
        /// 被这句话盖掉，报告因此指出错误方向。
        /// </summary>
        static bool HasExhaustedOnlyCandidate(List<Person> pool, HashSet<int> transferred,
            HashSet<int> blocked, HashSet<int> exhaustedSources)
        {
            if (pool == null || exhaustedSources.Count == 0) return false;
            bool any = false;
            for (int i = 0; i < pool.Count; i++)
            {
                Person p = pool[i];
                if (p == null || transferred.Contains(p.Id) || blocked.Contains(p.Id)) continue;
                if (p.BelongCity == null || !exhaustedSources.Contains(p.BelongCity.Id))
                    return false;                            // 还有"非额度用尽"的候选 → 不是这个原因
                any = true;
            }
            return any;
        }

        /// <summary>把计数表的键追加到列表（去重，保持首次出现顺序）。</summary>
        static void AddKeys(Dictionary<int, int> counts, List<int> keys)
        {
            foreach (KeyValuePair<int, int> kv in counts)
            {
                if (!keys.Contains(kv.Key))
                    keys.Add(kv.Key);
            }
        }

        /// <summary>取计数（键不存在返回 0）。</summary>
        static int CountOf(Dictionary<int, int> counts, int key)
        {
            int v;
            counts.TryGetValue(key, out v);
            return v;
        }

        /// <summary>追加空缺归因的一段（段间用"、"分隔）。</summary>
        static void AppendReason(StringBuilder sb, string text)
        {
            if (sb.Length > 0) sb.Append('、');
            sb.Append(text);
        }

        /// <summary>
        /// 武将是否属于该军团作用域（军团边界的池子过滤口径）。
        /// 以 <c>Person.BelongCorps</c> 为准；归属缺失时按"所在城的军团"兜底
        /// （<c>City.UpdateCorps</c> 会维护两者一致），两者都缺失则视为第一军团的人。
        /// </summary>
        static bool InCorps(Person p, Corps scope, Force force)
        {
            if (p == null) return false;
            if (p.BelongCorps != null)
                return p.BelongCorps == scope;

            if (p.BelongCity != null && p.BelongCity.BelongCorps != null)
                return p.BelongCity.BelongCorps == scope;

            return force != null && force.CapitalCorps == scope;
        }

        /// <summary>
        /// 目标城本回合的接收额度：前线 / 高威胁城在基准上叠加 <c>frontlineExtraReceiveSeat</c>，
        /// 让"缺口最大、最吃紧"的城优先补人，而不是被后方城的小缺口平均分摊掉额度。
        /// </summary>
        static int RecvLimitFor(City city, int threatLevel, DeploymentWeights w)
        {
            if (w == null || city == null) return 0;
            int limit = w.maxTransferPerCityPerTurn;
            if (CityEstablishment.ResolveRing(city) <= 0 || threatLevel >= w.threatHighAt)
                limit += w.frontlineExtraReceiveSeat;
            return limit;
        }

        /// <summary>
        /// 推荐队伍加成：候选是某支"要去这个圈层"的推荐队伍的成员（固定武将命中，
        /// 或持有该队伍的固定特技）→ 加分。
        ///
        /// 目的：配合 AI 出征的推荐队伍组队 —— 队伍要的人优先被送到对应前线，
        /// 而不是被后方城的开发岗截走。需求集合每次求解只建一次（见 <c>currentTeamDemand</c>）。
        /// </summary>
        /// <param name="p">候选武将</param>
        /// <param name="destRing">目标城的圈层（由调用方按岗位预算一次，避免每个候选人重复解析）</param>
        /// <param name="w">部署参数（用 <c>recommendedTeamBonus</c>）</param>
        static float RecommendedTeamBonus(Person p, int destRing, DeploymentWeights w)
        {
            if (p == null || w == null || w.recommendedTeamBonus <= 0f)
                return 0f;
            if (currentTeamDemand == null)
                return 0f;
            return currentTeamDemand.BonusFor(p, destRing, w.recommendedTeamBonus);
        }

        /// <summary>
        /// 源城本回合的调出额度：按**净富余**（空闲人数 − 本城未填岗位数）动态计算。
        ///
        /// 【为什么改成相对判据】旧实现是"空闲人数 ≥ 固定阈值 30 才算超载，否则按基准 1 人"，
        /// 而正常存档里单城空闲很难到 30（曹操存档最大 25）→ 每座城恒等于 1 人/回合，
        /// 洛阳"空闲 23 / 空缺 2"也只能送出 1 人，全势力上百个缺口被这个额度卡死。
        ///
        /// 现在：本城缺口还没补满（净富余 ≤ 0）→ **一个人都不许调出**（先保住自己）；
        ///       净富余越多额度越大：额度 = 净富余 ÷ <c>sendSurplusPerSeat</c>(默认 2) + 1
        ///       —— 即**一次最多借走净富余的一半**（净富余 1 → 1 人；8 → 5 人；20 → 11 人）。
        ///       上限由 <c>maxSendPerCityPerTurn</c>（默认 0 = 不限）与全局额度共同兜住。
        /// </summary>
        /// <param name="from">源城（人选当前所属的城）</param>
        /// <param name="ownVacancy">该城本回合还缺多少人（岗位数 − 已填）</param>
        /// <param name="w">部署参数</param>
        /// <returns>本回合允许从该城调出的上限；0 = 不许调出（调用处需单独判 0，CanSend 把 ≤0 当作"不限制"）</returns>
        static int SendLimitFor(City from, int ownVacancy, DeploymentWeights w)
        {
            if (w == null || from == null) return 0;

            int free = from.freePersons != null ? from.freePersons.Count : 0;
            int surplus = free - ownVacancy;              // 净富余：扣掉本城缺口后真多余出来的人
            if (surplus <= 0) return 0;                   // 自己都还缺人 → 先保住本城

            int baseLimit = Math.Max(1, w.maxTransferFromCityPerTurn);
            int slope = Math.Max(1, w.sendSurplusPerSeat);
            int limit = baseLimit + surplus / slope;

            // 硬顶：≤0 = 不限制 —— 只由全局额度与"目标城接收额度"兜住。
            // 之所以默认不限：额度公式本身就是"最多借净富余的一半"，
            // 再压一个固定硬顶会把这条口径直接截断（净富余 20 想要 11 人却只放 5 人）。
            if (w.maxSendPerCityPerTurn <= 0)
                return limit;
            return Math.Max(baseLimit, Math.Min(limit, w.maxSendPerCityPerTurn));
        }

        /// <summary>
        /// 本回合作用域级调动总额度 = <c>max(maxTransferPerTurn, ⌈在册总人数 × 比例⌉)</c>。
        ///
        /// 固定额度在不同规模的势力上游离太大：上百人的势力一回合只准动 12 人，
        /// "全势力上百个岗位缺口"要卡几十回合；十几人的小势力又显得宽松。
        /// 按**在册总人数**（含在部队 / 有任务的人，即势力的总盘子）缩放后，
        /// 调动节奏与规模自洽 —— 默认比例 0.25，即"100 人至少能调动 25 人"。
        /// 配置的 <c>maxTransferPerTurn</c> 是**下限**：配置更高时以配置为准，小势力不被缩小。
        /// </summary>
        /// <param name="w">部署参数</param>
        /// <param name="scopePersons">本作用域在册武将总数</param>
        /// <returns>本回合额度；≤0 = 不限制</returns>
        public static int GlobalTransferQuota(DeploymentWeights w, int scopePersons)
        {
            if (w == null) return 0;
            int cfg = w.maxTransferPerTurn;
            if (cfg <= 0 || w.maxTransferPerTurnRatio <= 0f || scopePersons <= 0)
                return cfg;                                  // 配 0 就是不限制，缩放不该把它变成有限
            int scaled = (int)Math.Ceiling(scopePersons * w.maxTransferPerTurnRatio);
            return scaled > cfg ? scaled : cfg;
        }

        /// <summary>本城本回合还缺多少人（岗位数 − 已填；负数按 0 计）。</summary>
        /// <param name="cityId">城 id</param>
        /// <param name="postCountByCity">该城岗位数（裁撤后的最终编制）</param>
        /// <param name="filledByCity">该城已填数（本城在岗 + 外调到位）</param>
        static int VacancyOfCity(int cityId, Dictionary<int, int> postCountByCity, Dictionary<int, int> filledByCity)
        {
            int posts, filled;
            postCountByCity.TryGetValue(cityId, out posts);
            filledByCity.TryGetValue(cityId, out filled);
            int vacancy = posts - filled;
            return vacancy > 0 ? vacancy : 0;
        }

        /// <summary>字典计数 +1（键不存在时按 0 起算）。</summary>
        static void BumpCount(Dictionary<int, int> dict, int key)
        {
            if (dict == null) return;
            int count;
            dict.TryGetValue(key, out count);
            dict[key] = count + 1;
        }

        /// <summary>本城在岗 / 外调的填充记录（含原因链）。</summary>
        /// <param name="task">待填岗位</param>
        /// <param name="person">人选</param>
        /// <param name="score">匹配分</param>
        /// <param name="local">是否本城在岗（false = 外调）</param>
        /// <param name="city">目标城</param>
        /// <param name="fromCityName">源城名（外调时非空；在岗为 null）</param>
        static PostFilling MakeFilling(PostTask task, Person person, float score, bool local, City city,
            string fromCityName)
        {
            PostFilling filling;
            filling.post = task.post;
            filling.personId = person.Id;
            filling.personName = person.Name;
            filling.score = score;
            filling.local = local;
            filling.fromCityName = fromCityName;

            // 原因链带上"编制依据"：兵力 / 主将带兵上限（军事岗位依据）、内政点数（开发岗位依据）
            int interiorPoints = CityEstablishment.CountInteriorPoints(city);
            int perGeneral = (city.Leader != null && city.Leader.TroopsLimit > 0) ? city.Leader.TroopsLimit : 0;
            // 【R1】报告标签也按 ResolveRing（港关显示其归属都市的圈层，避免所有港关都写"前线"）
            int reportRing = CityEstablishment.ResolveRing(city);
            filling.reason = string.Format("{0}/{1} 兵力{2}{3} 内政{4}点 缺口{5:P0} 威胁{6}{7}",
                local ? "在岗" : "外调",
                reportRing == 0 ? "前线" : (reportRing == 1 ? "次前线" : "后方"),
                city.troops,
                perGeneral > 0 ? string.Format("/主将带兵{0}", perGeneral) : "",
                interiorPoints,
                task.devGap,
                task.threatLevel,
                string.IsNullOrEmpty(task.post.trigger) ? "" : " | 依据: " + task.post.trigger);
            return filling;
        }

        /// <summary>能力适配度（向量点积，能力按 0..1 归一化）。</summary>
        static float Fit(PostFit w, Person p)
        {
            if (p == null) return 0f;
            float cmd = p.Command / 100f;
            float str = p.Strength / 100f;
            float intl = p.Intelligence / 100f;
            float pol = p.Politics / 100f;
            float gla = p.Glamour / 100f;
            float trans = p.HasFeatrue(8) ? 1f : 0f;

            return w.command * cmd + w.strength * str + w.intelligence * intl
                 + w.politics * pol + w.glamour * gla + w.transport * trans;
        }

        /// <summary>
        /// 特技组合搭配（第一步）：候选拥有"本城驻军里还没有的特技"时给加分。
        /// 只做**多样性**判断（避免同城堆满同类特技），不做"队伍级 combo 判定"——
        /// 后者需要知道出征编队的组队规则，属后续步骤。
        ///
        /// 【性能】目标城驻军的特技集合由调用方按岗位预算一次（见 <see cref="FillDestFeatureSet"/>）：
        /// 旧实现是"每个候选人 × 每个特技 × 每个驻军"三层遍历（且内层还是 <c>List.Contains</c>），
        /// 而目标城在同一岗位的所有候选人之间是固定的，重复扫描纯属浪费。
        /// </summary>
        /// <param name="p">候选武将</param>
        /// <param name="destFeatures">目标城驻军持有的特技 id 集合（可为 null = 空集）</param>
        /// <param name="w">部署参数（用 <c>featureNoveltyBonus</c>）</param>
        static float FeatureNovelty(Person p, HashSet<int> destFeatures, DeploymentWeights w)
        {
            if (w == null || w.featureNoveltyBonus <= 0f) return 0f;
            if (p == null || p.FeatureList == null || p.FeatureList.Count == 0) return 0f;
            if (destFeatures == null || destFeatures.Count == 0) return 0f;

            for (int i = 0; i < p.FeatureList.Count; i++)
            {
                Feature f = p.FeatureList[i];
                if (f == null) continue;
                if (!destFeatures.Contains(f.Id))
                    return w.featureNoveltyBonus;               // 有一个"新特技"就够
            }
            return 0f;
        }

        /// <summary>
        /// 收集目标城"驻军"持有的全部特技 id（出征部队不算，与旧 <see cref="FeatureNovelty"/> 同口径）。
        /// 写入调用方提供的复用集合，避免"每个岗位新建一个 HashSet"。
        /// </summary>
        /// <param name="dest">目标城</param>
        /// <param name="set">输出集合（会被清空后重填）</param>
        static void FillDestFeatureSet(City dest, HashSet<int> set)
        {
            set.Clear();
            if (dest == null || dest.allPersons == null)
                return;

            for (int j = 0; j < dest.allPersons.Count; j++)
            {
                Person other = dest.allPersons[j];
                if (other == null || other.mBelongTroop != null) continue;   // 只比"驻军"

                SangoObjectList<Feature> features = other.FeatureList;
                if (features == null) continue;
                for (int k = 0; k < features.Count; k++)
                {
                    Feature f = features[k];
                    if (f != null)
                        set.Add(f.Id);
                }
            }
        }

        /// <summary>
        /// 匹配分 = 能力适配 − 行程成本 − 跨圈层成本。
        ///
        /// <paramref name="lowPopDest"/> = 本岗位属于"把该城补到最低人数（<c>minCityPersons</c>）"的那几个岗：
        /// 此时**只按能力适配打分**，免掉行程与跨圈层成本 —— 这两项本来是"别为小事折腾武将"的效率考量，
        /// 可一座 0~2 人的城是**停摆**的（征集不了兵、押不出车、做不了内政），
        /// 拿"回撤贵 / 路远"把它永远空着，等于用一名武将的行程换掉一整座城的产能。
        /// （作用范围有限：每城最多补到 minCityPersons 人，源城仍有净富余闸门、每城接收额度与全局额度约束。）
        /// </summary>
        static float Score(Person p, Post post, City dest, bool lowPopDest, DeploymentWeights w, int destRing)
        {
            float fit = Fit(post.weights, p);
            if (lowPopDest)
                return fit;

            // 【性能】行程走城对缓存（见 <see cref="cityDistanceCache"/>），系数提前算好（见 travelCostScale）：
            // 旧实现每次都直连 Scenario.FindShortestPath（字符串 key + 字典查询）并做一次除法。
            float travel = 0f;
            if (p.CurrentCity != null && dest != null)
                travel = TravelDays(p, dest) * travelCostScale;

            // 【④ 方向化】跨圈层成本按方向计价：
            //   调向更前线（ring 变小）= 便宜 —— "前线有权利和内陆交换能力武将"；
            //   调向后方（ring 变大）= 贵 —— 抑制无谓回撤与左右横跳。
            // 用 ResolveRing：港关自身 borderLine 恒为 0，直接读会把所有港关误当成前线。
            // 目标城圈层由调用方按岗位预算一次（destRing），这里只解析候选人自己的出发城。
            float ring = 0f;
            int fromRing = p.BelongCity != null ? CityEstablishment.ResolveRing(p.BelongCity) : 0;
            if (fromRing != destRing)
                ring = destRing < fromRing
                    ? w.crossRingCost * w.frontwardCostFactor
                    : w.crossRingCost * w.backwardCostFactor;

            return fit - travel - ring;
        }

        /// <summary>
        /// 取"该武将所在城 → 目标城"的行程天数（带城对缓存）。
        ///
        /// 口径与 <c>Person.DistanceDays(City)</c> 完全一致（含"港关归约到归属都市"的两层处理），
        /// 但最内层的"都市 ↔ 都市"距离会落到 <see cref="cityDistanceCache"/>，
        /// 因此真正调用 <c>Scenario.GetCityDistance</c>（寻路）的次数是本趟求解的城对数，而不是评分数。
        /// </summary>
        /// <param name="p">武将</param>
        /// <param name="dest">目标城</param>
        /// <returns>行程天数（无法判定时为 999，与原实现一致）</returns>
        static int TravelDays(Person p, City dest)
        {
            if (dest == null) return 0;

            City from = p.mBelongTroop != null
                ? p.mBelongTroop.cell.BelongCity
                : (p.BelongCity == null ? p.CurrentCity : p.BelongCity);
            return CityDistanceCached(dest, from);
        }

        /// <summary>
        /// <c>City.Distance</c> 的等价实现（港关归约口径一致），只把最内层的"都市 ↔ 都市"距离缓存起来。
        /// 复刻而非直接调用，是为了让缓存落在都市这一层 —— 港关归约仍按原逻辑递归走。
        /// </summary>
        /// <param name="a">起点城（对应 <c>City.Distance</c> 的 this）</param>
        /// <param name="b">终点城（对应 <c>City.Distance</c> 的 other）</param>
        /// <returns>相隔天数</returns>
        static int CityDistanceCached(City a, City b)
        {
            if (a == null || b == null) return 999;
            if (a == b) return 0;

            if (a.BelongCity != null)
            {
                if (a.BelongCity == b) return 1;             // 隶属范围内，需要 1 回合
                return CityDistanceCached(a.BelongCity, b);
            }

            if (b.BelongCity != null)
            {
                if (b.BelongCity == a) return 1;
                b = b.BelongCity;
            }

            long key = Scenario.CityPairKey(a.Id, b.Id);
            int dist;
            if (cityDistanceCache != null && cityDistanceCache.TryGetValue(key, out dist))
                return dist;

            dist = Scenario.Cur.GetCityDistance(a, b);        // 真正的寻路：本趟只发生 O(城对数) 次
            if (cityDistanceCache != null)
                cityDistanceCache[key] = dist;
            return dist;
        }

        /// <summary>未定圈层（-1）在分配顺序里按"最深层"处理，避免被当成"比边境还优先"（【R2】）。</summary>
        const int DeepRing = 99;

        static int RingOf(Dictionary<int, int> ringOfCity, int cityId)
        {
            int ring;
            if (ringOfCity != null && ringOfCity.TryGetValue(cityId, out ring))
                return ring < 0 ? DeepRing : ring;              // 【R2】未定 → 最深层
            return DeepRing;
        }
    }
}
