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
        }

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
                     | (situation.hasPort ? 32 : 0);
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
                for (int j = 0; j < posts.Count; j++)
                {
                    PostTask task;
                    task.post = posts[j];
                    task.threatLevel = threatLevel;
                    task.devGap = devGap;
                    task.underSiege = false;
                    tasks.Add(task);
                }
            }

            // ---------- 1.5) 编制总量约束：岗位总数不该远超势力人力 ----------
            // 否则每座城都停在"岗位 10 / 在岗 3"的空缺态：分布效果被稀释、报告看不出重点，
            // 稀缺人手也会被大量低价值岗位摊薄。
            // 裁撤方式：按"硬性 → 优先级 → 军事岗边境优先"排序后截尾 ——
            // 最先被裁的是深后方的后勤 / 开发 / 运输岗，前线军事岗最后动。
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

                tasks.Sort(delegate (PostTask a, PostTask b)
                {
                    return CompareForAssign(a, b, cutRings);
                });
                tasks.RemoveRange(postBudget, tasks.Count - postBudget);

                // 恢复"按城聚集"的顺序，报告才能按城成组输出
                tasks.Sort(delegate (PostTask a, PostTask b)
                {
                    return a.post.cityId.CompareTo(b.post.cityId);
                });
            }

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
                    int cityIndex = IndexOfCity(cityList, task.post.cityId);
                    if (cityIndex < 0) continue;

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
                    }
                    else
                    {
                        batchPending.Add(task);
                    }
                }
            };

            // 阶段 A：前线 / 硬性岗先用本城人满足（缺口交给阶段 B 外调）
            fillFromLocal(primaryTasks, primaryPending);

            // ---------- 3) 剩余岗位：从可调动池（空闲武将）外调 ----------
            List<Person> pool = new List<Person>();
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person p = scenario.personSet[i];
                if (p == null || p.Id == 0) continue;
                if (p.BelongForce != force) continue;
                // 【军团边界】军团级作用域的抽调池只含本军团的人 —— 绝不跨军团抽人
                //（跨团调人会打乱玩家自己排好的部署；军团之间的隔离是刻意的）
                if (scope != null && !InCorps(p, scope, force)) continue;
                if (p.IsDead || p.IsPrisoner) continue;
                if (!p.IsFree) continue;                 // 部队中 / 有任务 / 在途 → 不可再分配
                // 【改动 B】只排除"已外调"的人；"本城在岗"的人**仍然入池** ——
                // 在岗只是记账，别人更需要他时可以被抢走（抢占会扣 stealLocalCost 成本）。
                if (transferred.Contains(p.Id)) continue;
                pool.Add(p);
            }
            // 可调动池 = 全势力的机动人力（含"在岗"的人）——这是"能派出去干活的人"的真实上限
            plan.personPool = pool.Count;

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
                    int cityIndex = IndexOfCity(cityList, task.post.cityId);
                    if (cityIndex < 0) continue;
                    City city = cityList[cityIndex];

                    string kindName = DeploymentPlan.KindName(task.post.kind);

                    // 作用域级总额度：本回合调动幅度已够 → 剩余岗位下回合继续（避免一次大搬家）
                    if (!DeploymentState.CanTransferGlobal(scopeKey, turn, weights.maxTransferPerTurn))
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

                    float scoreFloor = task.post.required ? 0f : weights.minTransferScore;

                    while (true)
                    {
                        // 【两轮挑人 · 改动 B】
                        //   第一轮：只看**真正的机动人力**（没被"在岗"记账占用的空闲人）；
                        //   第二轮：才允许**抢占"在岗"的人**（扣 stealLocalCost）。
                        // 人力优先来自真空闲，只有明显更划算时才去动别人城里的"在岗"记账。
                        float bestScore;
                        Person best = ScanBest(task.post, city, pool, transferred, blocked, exhaustedSources,
                            occupied, false, weights, out bestScore);
                        if (best == null || bestScore < scoreFloor)
                        {
                            float stealScore;
                            Person steal = ScanBest(task.post, city, pool, transferred, blocked, exhaustedSources,
                                occupied, true, weights, out stealScore);
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

                        // 源城调出配额：本回合该城已送够人 → 换一个来源，不拉黑这个武将。
                        // 额度按城况动态取值：人力超载的城放宽，让囤积城尽快把闲置武将从城里输出去。
                        int fromCity = best.BelongCity != null ? best.BelongCity.Id : 0;
                        if (!DeploymentState.CanSend(scopeKey, turn, fromCity, SendLimitFor(best.BelongCity, weights)))
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
                        revokedPersons.Add(chosen.Id);

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

            for (int i = 0; i < stalledCityIds.Count; i++)
            {
                int cityId = stalledCityIds[i];
                int q = CountOf(quotaStalled, cityId);
                int s = CountOf(scoreStalled, cityId);
                int c = CountOf(coolingStalled, cityId);
                int v = CountOf(vacantStalled, cityId);

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
                if (v > 0)
                    AppendReason(why, string.Format("池中无可用人选 {0} 个", v));
                if (c > 0)
                    AppendReason(why, string.Format("候选在冷却期 {0} 个（{1} 回合）", c, weights.debounceTurns));

                plan.unmet.Add(string.Format("{0}: {1} 个岗位待补 —— {2}", cname, q + s + c + v, why));
            }

            if (globalQuotaUsed)
            {
                plan.unmet.Add(string.Format("本作用域本回合调动总额度 {0} 人已用完，剩余岗位下回合继续",
                    weights.maxTransferPerTurn));
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
        /// <param name="weights">部署参数</param>
        /// <param name="bestScore">返回最优人选的分（无人选时为 float.MinValue）</param>
        /// <returns>最优人选；无合适人选时为 null</returns>
        static Person ScanBest(Post post, City dest, List<Person> pool, HashSet<int> transferred,
            HashSet<int> blocked, HashSet<int> exhaustedSources, HashSet<int> occupied,
            bool onlyOccupied, DeploymentWeights weights, out float bestScore)
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
                if (!MeetsMilitaryFloor(p, post, weights)) continue;

                float score = Score(p, post, dest, weights);
                // 特技组合搭配：军事 / 守备岗优先派"能带来本城还没有的特技"的武将
                if (post.kind == PostKind.Military || post.kind == PostKind.Garrison)
                    score += FeatureNovelty(p, dest, weights);
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
        /// 源城本回合的调出额度：人力超载（空闲人数 ≥ <c>overloadedFreePersonThreshold</c>）的城
        /// 放宽到 <c>overloadedSendLimit</c>，让囤积城尽快把闲置武将输送给缺人的城。
        /// </summary>
        static int SendLimitFor(City from, DeploymentWeights w)
        {
            if (w == null) return 0;
            int limit = w.maxTransferFromCityPerTurn;
            int free = (from != null && from.freePersons != null) ? from.freePersons.Count : 0;
            if (free >= w.overloadedFreePersonThreshold)
                limit = Math.Max(limit, w.overloadedSendLimit);
            return limit;
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
        /// </summary>
        static float FeatureNovelty(Person p, City dest, DeploymentWeights w)
        {
            if (w == null || w.featureNoveltyBonus <= 0f) return 0f;
            if (p == null || p.FeatureList == null || p.FeatureList.Count == 0) return 0f;
            if (dest == null || dest.allPersons == null || dest.allPersons.Count == 0) return 0f;

            for (int i = 0; i < p.FeatureList.Count; i++)
            {
                Feature f = p.FeatureList[i];
                if (f == null) continue;

                bool exists = false;
                for (int j = 0; j < dest.allPersons.Count; j++)
                {
                    Person other = dest.allPersons[j];
                    if (other == null || other == p) continue;
                    if (other.mBelongTroop != null) continue;   // 只比"驻军"，出征部队不算
                    if (other.HasFeatrue(f.Id))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    return w.featureNoveltyBonus;               // 有一个"新特技"就够
            }
            return 0f;
        }

        /// <summary>匹配分 = 能力适配 − 行程成本 − 跨圈层成本。</summary>
        static float Score(Person p, Post post, City dest, DeploymentWeights w)
        {
            float fit = Fit(post.weights, p);
            float travel = 0f;
            if (p.CurrentCity != null && dest != null)
                travel = p.DistanceDays(dest) / (float)Math.Max(1, w.turnDays) * w.costPerTurn;

            // 【④ 方向化】跨圈层成本按方向计价：
            //   调向更前线（ring 变小）= 便宜 —— "前线有权利和内陆交换能力武将"；
            //   调向后方（ring 变大）= 贵 —— 抑制无谓回撤与左右横跳。
            // 用 ResolveRing：港关自身 borderLine 恒为 0，直接读会把所有港关误当成前线。
            float ring = 0f;
            int fromRing = p.BelongCity != null ? CityEstablishment.ResolveRing(p.BelongCity) : 0;
            int toRing = dest != null ? CityEstablishment.ResolveRing(dest) : 0;
            if (fromRing != toRing)
                ring = toRing < fromRing
                    ? w.crossRingCost * w.frontwardCostFactor
                    : w.crossRingCost * w.backwardCostFactor;

            return fit - travel - ring;
        }

        static int IndexOfCity(List<City> cities, int cityId)
        {
            for (int i = 0; i < cities.Count; i++)
            {
                if (cities[i] != null && cities[i].Id == cityId)
                    return i;
            }
            return -1;
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
