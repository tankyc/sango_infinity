using System;
using System.Collections.Generic;

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

        // 【前期加成】势力回合计数：Solve 每势力每回合调用一次，换剧本（scenario 实例变化）自动归零。
        static readonly Dictionary<int, int> forceTurnCount = new Dictionary<int, int>();
        static Scenario turnScenario;

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
        /// 为一个势力求解部署计划（并按其模式决定是否执行）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">剧本</param>
        /// <returns>部署计划（永不为 null）</returns>
        public static DeploymentPlan Solve(Force force, Scenario scenario)
        {
            DeploymentPlan plan = new DeploymentPlan();
            if (force == null || scenario == null)
                return plan;

            AIConfig config = AIConfig.Instance;
            DeploymentWeights weights = (config != null && config.deployment != null)
                ? config.deployment : new DeploymentWeights();

            // 【前期加成】登记"本势力本剧本内第几回合"，供编制层判断"开局前 N 回合"。
            // 放在编制之前写入，确保 CityEstablishment 读到的就是本回合的值。
            // 换剧本（scenario 实例变化）时计数归零 —— 否则第二次开局永远进不了"前期"。
            if (!ReferenceEquals(turnScenario, scenario))
            {
                turnScenario = scenario;
                forceTurnCount.Clear();
            }
            int ft;
            if (!forceTurnCount.TryGetValue(force.Id, out ft))
                ft = 0;
            ft++;
            forceTurnCount[force.Id] = ft;
            DeploymentState.currentForceId = force.Id;
            DeploymentState.currentForceTurn = ft;

            // 换剧本 → 清掉增量缓存，避免复用上个剧本的岗位表
            if (!ReferenceEquals(cachedScenario, scenario))
            {
                cityCache.Clear();
                cachedScenario = scenario;
            }

            int turn = DeploymentState.NextForceTurn(force.Id);
            DeploymentState.transferCountThisTurn = 0;

            plan.forceId = force.Id;
            plan.forceName = force.Name;

            // ---------- 1) 采集本势力城池 + 编制 ----------
            List<PostTask> tasks = new List<PostTask>();
            List<City> cityList = new List<City>();
            int personTotal = 0;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force)
                    continue;

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

            // ---------- 2) 先用本城在册人员满足（在岗） ----------
            HashSet<int> usedPersons = new HashSet<int>();
            List<PostTask> pending = new List<PostTask>();

            for (int i = 0; i < tasks.Count; i++)
            {
                PostTask task = tasks[i];
                int cityIndex = IndexOfCity(cityList, task.post.cityId);
                if (cityIndex < 0) continue;

                City city = cityList[cityIndex];
                Person best = null;
                float bestScore = float.MinValue;

                // 在册池：户口在本城、不在部队、非俘非亡（含正在执行内政任务者）
                foreach (Person p in city.allPersons)
                {
                    if (p == null || p.Id == 0) continue;
                    if (usedPersons.Contains(p.Id)) continue;
                    if (p.IsDead || p.IsPrisoner) continue;
                    if (p.mBelongTroop != null) continue;

                    float score = Fit(task.post.weights, p);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = p;
                    }
                }

                if (best != null)
                {
                    usedPersons.Add(best.Id);
                    plan.fillings.Add(MakeFilling(task, best, bestScore, true, city));
                }
                else
                {
                    pending.Add(task);
                }
            }

            // ---------- 3) 剩余岗位：从可调动池（空闲武将）外调 ----------
            List<Person> pool = new List<Person>();
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person p = scenario.personSet[i];
                if (p == null || p.Id == 0) continue;
                if (p.BelongForce != force) continue;
                if (p.IsDead || p.IsPrisoner) continue;
                if (!p.IsFree) continue;                 // 部队中 / 有任务 / 在途 → 不可再分配
                if (usedPersons.Contains(p.Id)) continue;
                pool.Add(p);
            }
            plan.personPool = pool.Count;

            // 边境优先原则：先把每座城的圈层查出来（0 = 边境，越小越靠前）
            Dictionary<int, int> ringOfCity = new Dictionary<int, int>();
            for (int i = 0; i < cityList.Count; i++)
            {
                if (cityList[i] != null)
                    ringOfCity[cityList[i].Id] = CityEstablishment.ResolveRing(cityList[i]);   // 【R1】港关按归属都市
            }

            // 分配顺序：硬性优先 → 岗位优先级 → 军事岗边境优先 / 内政岗缺口优先 → 稳定兜底
            pending.Sort(delegate (PostTask a, PostTask b)
            {
                return CompareForAssign(a, b, ringOfCity);
            });

            bool execute = !weights.shadowOnly;

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
            Dictionary<int, int> coolingStalled = new Dictionary<int, int>();
            Dictionary<int, string> stalledCityNames = new Dictionary<int, string>();
            HashSet<int> exhaustedSources = new HashSet<int>();   // 本回合"已调出够多"的源城

            for (int i = 0; i < pending.Count; i++)
            {
                PostTask task = pending[i];
                int cityIndex = IndexOfCity(cityList, task.post.cityId);
                if (cityIndex < 0) continue;
                City city = cityList[cityIndex];

                string kindName = DeploymentPlan.KindName(task.post.kind);

                // 同一岗位要一直试到有人可派：最优人选被否掉就退而求其次
                Person chosen = null;
                float chosenScore = 0f;
                bool coolingOnly = false;

                while (true)
                {
                    Person best = null;
                    float bestScore = float.MinValue;

                    for (int j = 0; j < pool.Count; j++)
                    {
                        Person p = pool[j];
                        if (p == null || usedPersons.Contains(p.Id) || blocked.Contains(p.Id)) continue;
                        if (p.BelongCity != null && exhaustedSources.Contains(p.BelongCity.Id)) continue;

                        float score = Score(p, task.post, city, weights);
                        // 特技组合搭配：军事 / 守备岗优先派"能带来本城还没有的特技"的武将
                        if (task.post.kind == PostKind.Military || task.post.kind == PostKind.Garrison)
                            score += FeatureNovelty(p, city, weights);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = p;
                        }
                    }

                    if (best == null)
                        break;

                    // 防抖：刚被调过的武将本回合不再动（本人整体跳过）
                    if (!DeploymentState.CanTransferNow(best.Id, turn, weights.debounceTurns))
                    {
                        blocked.Add(best.Id);
                        coolingOnly = true;
                        continue;
                    }

                    // 合法性闸门（影子模式同样判定 → 报告里能看到"会被谁否决"）
                    DeploymentExecutor.Gate gate = DeploymentExecutor.CanTransfer(best, city, weights);
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

                    // 目标城收发配额：本回合已接收够多 → 该城其余岗位下回合继续（不是没人）
                    if (!DeploymentState.CanReceive(city.Id, weights.maxTransferPerCityPerTurn))
                        break;

                    // 源城调出配额：本回合该城已送够人 → 换一个来源，不拉黑这个武将
                    int fromCity = best.BelongCity != null ? best.BelongCity.Id : 0;
                    if (!DeploymentState.CanSend(fromCity, weights.maxTransferFromCityPerTurn))
                    {
                        exhaustedSources.Add(fromCity);
                        continue;
                    }

                    chosen = best;
                    chosenScore = bestScore;
                    break;
                }

                if (chosen == null)
                {
                    if (!DeploymentState.CanReceive(city.Id, weights.maxTransferPerCityPerTurn))
                    {
                        int n;
                        quotaStalled.TryGetValue(city.Id, out n);
                        quotaStalled[city.Id] = n + 1;
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
                        plan.unmet.Add(string.Format("{0} {1} 待命: 候选均在调动冷却期", city.Name, kindName));
                    }
                    else
                    {
                        plan.unmet.Add(string.Format("{0} {1}{2} 真实空缺: 池中无可用人选",
                            city.Name, kindName, task.post.required ? "(硬)" : ""));
                    }
                    continue;
                }

                string deny;
                bool done = DeploymentExecutor.Transfer(chosen, city, weights, execute, turn, out deny);
                if (execute && !done)
                {
                    plan.unmet.Add(string.Format("{0} {1} 执行失败: {2}", city.Name, kindName, deny));
                    continue;
                }

                usedPersons.Add(chosen.Id);
                DeploymentState.MarkReceive(city.Id);
                DeploymentState.MarkSend(chosen.BelongCity != null ? chosen.BelongCity.Id : 0);
                if (execute)
                    DeploymentState.transferCountThisTurn++;

                plan.fillings.Add(MakeFilling(task, chosen, chosenScore, false, city));
            }

            // 闸门否决按人汇总一行（避免同一个被锁死的武将刷满整屏）
            foreach (KeyValuePair<int, int> kv in deniedCounts)
            {
                string name, reason;
                deniedNames.TryGetValue(kv.Key, out name);
                deniedReasons.TryGetValue(kv.Key, out reason);
                plan.unmet.Add(string.Format("闸门否决汇总: {0} 被拒 {1} 次（原因: {2}）", name, kv.Value, reason));
            }

            // 配额限流 / 全员冷却：按城汇总（这些是"节奏控制"，不是"没人可用"）
            foreach (KeyValuePair<int, int> kv in quotaStalled)
            {
                string cname;
                stalledCityNames.TryGetValue(kv.Key, out cname);
                plan.unmet.Add(string.Format("{0}: {1} 个岗位待补 —— 本回合外调配额 {2} 人已用完，下回合继续",
                    cname, kv.Value, weights.maxTransferPerCityPerTurn));
            }
            foreach (KeyValuePair<int, int> kv in coolingStalled)
            {
                string cname;
                stalledCityNames.TryGetValue(kv.Key, out cname);
                plan.unmet.Add(string.Format("{0}: {1} 个岗位待补 —— 可调动池全部在调动冷却期({2} 回合)内",
                    cname, kv.Value, weights.debounceTurns));
            }

            // 岗位总表（按城顺序）用于报告
            for (int i = 0; i < tasks.Count; i++)
                plan.posts.Add(tasks[i].post);

            // 【诊断】每城人力分布：分辨"人不在城"与"人在城但被部队/任务占用"。
            //   · 在册 = allPersons（户口在城的所有人）
            //   · 空闲 = freePersons（能派出去干活的：无部队、无任务、非俘非亡）
            //   · 在部队 = allPersons 中 mBelongTroop != null 的人
            //   · 在城 = 在册 − 在部队（能接收城内命令的那部分）
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

                plan.cityInfo.Add(string.Format("{0} 在册{1} 空闲{2} 在部队{3} 在城{4}",
                    ci.Name, total, freeCnt, inTroopCnt, total - inTroopCnt));
            }

            return plan;
        }

        /// <summary>本城在岗 / 外调的填充记录（含原因链）。</summary>
        static PostFilling MakeFilling(PostTask task, Person person, float score, bool local, City city)
        {
            PostFilling filling;
            filling.post = task.post;
            filling.personId = person.Id;
            filling.personName = person.Name;
            filling.score = score;
            filling.local = local;

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
