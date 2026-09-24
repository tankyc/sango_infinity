using System;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 编制层：把"城况画像"翻译成"岗位表"。
    ///
    /// 编制依据（来自需求方）：
    ///   · **军事岗位按兵力** —— 兵力 / 主将带兵上限 = 能编成的队数，军事岗位不会超过它
    ///     （避免"拉满兵力出门还剩一堆将"）；
    ///   · **开发岗位按内政建设情况** —— 内政设施（<c>BuildingType.IsIntrior</c>）等级之和决定基本人手，
    ///     开发缺口只决定是否加派；
    ///   · **内政工作族** 还要覆盖 征兵 / 军备（兵装·器械·船）/ 训练（士气）/ 搜索 / 运输 / 后勤，
    ///     每类岗位都由对应的城况触发，并在岗位上写明"编制依据"（<see cref="Post.trigger"/>）。
    /// </summary>
    public static class CityEstablishment
    {
        /// <summary>
        /// 生成一座城池的岗位表。
        /// </summary>
        /// <param name="city">城池（都市 / 港口 / 关卡）</param>
        /// <param name="situation">城池态势快照（CitySituation.Collect，含兵装 / 设施等）</param>
        /// <param name="threatLevel">威胁等级（BattleSituation.EvaluateCityThreat）</param>
        /// <param name="weights">部署参数</param>
        /// <param name="personTotal">势力总人数（用于单城上限）</param>
        /// <param name="output">输出岗位表（追加）</param>
        public static void Build(City city, CitySituation situation, int threatLevel,
            DeploymentWeights weights, int personTotal, List<Post> output)
        {
            if (city == null || weights == null || output == null)
                return;

            bool isPortGate = city.IsPort() || city.IsGate();

            // 【R1】圈层改用 ResolveRing：港关/关卡的 borderLine 恒为 0
            // （Force.UpdateTurnInfo 只对 IsCity() 计算圈层），必须按其归属都市的圈层判断，
            // 否则所有港关都会被当成前线（"最低 0 人"就永远不成立）。
            int ring = ResolveRing(city);
            bool isBorder = ring == 0;

            // ---------- 人力丰裕度（① ② ③ ④ 共用口径） ----------
            // 丰裕度 = (势力总人数 / 势力城数) / staffPerCityBaseline，夹在 [1, staffAbundanceMax]。
            // 1.0 表示"人力紧张，弹性岗位一律按基准"（行为与放开前一致，不误伤小势力）。
            // 例：9 城 196 人 → 21.8 人/城 → 2.5；28 城 50 人 → 1.8 人/城 → 1.0。
            float abundance = 1f;
            if (weights.staffAbundanceScaling && city.BelongForce != null)
            {
                int forceCityCount = Math.Max(1, city.BelongForce.CityCount);
                int forcePeople = city.BelongForce.PersonCount > 0
                    ? city.BelongForce.PersonCount
                    : personTotal;
                abundance = (forcePeople / (float)forceCityCount) / Math.Max(1f, weights.staffPerCityBaseline);
                if (abundance < 1f) abundance = 1f;
                if (abundance > weights.staffAbundanceMax) abundance = weights.staffAbundanceMax;
            }

            // ---------- 基础编制上限：按城池等级 ----------
            int levelIndex = city.CityLevelType != null ? city.CityLevelType.Id - 1 : 0;
            if (levelIndex < 0) levelIndex = 0;
            int baseSeat = 3;
            if (weights.levelSeat != null && weights.levelSeat.Length > 0)
            {
                if (levelIndex >= weights.levelSeat.Length)
                    levelIndex = weights.levelSeat.Length - 1;
                baseSeat = weights.levelSeat[levelIndex];
            }

            // ---------- 圈层 / 威胁 ----------
            int ringSeat = isBorder ? weights.borderRingBonus
                : (ring == 1 ? weights.subFrontRingBonus : 0);
            int threatSeat = 0;
            if (threatLevel >= weights.threatHighAt) threatSeat = weights.threatHighBonus;
            else if (threatLevel >= weights.threatMidAt) threatSeat = weights.threatMidBonus;

            // ---------- 军事（② 已放开：不再被基础编制压死） ----------
            // 取三者较大值：
            //   · milByTroops：按主将带兵上限折算的**队数**（例：永安 97k 兵 / 7040 → 14 队）；
            //   · baseSeat + ringSeat + threatSeat：保证"兵少但位置关键"的城仍有守将；
            //   · minMilitarySeatAtBorder：前线保底。
            // 只受 militarySeatHardMax 硬顶约束。
            int milSeat = 0;
            int expectedTroops = 0;
            if (!isPortGate)
            {
                // 用预想兵力而不是当前兵力：出征 / 灾害 / 被攻击不会让军事编制乱跳，
                // 也就不会把将军们反复调来调去。
                expectedTroops = DeploymentState.GetExpectedTroops(city, weights, situation);

                // 【前线人数保证】队数 = 预想兵力 ÷ 每队编制（默认 **5000 一队**），
                // 再按"多人队伍（主将 + 副将）"放大：
                //     人数 = 队数 × (1 + deputyPerUnit)
                //   例：9 万兵 → 18 队 → 18 × 1.5 = **27 人**（旧算法按主将带兵上限只给 9 人，明显偏低）。
                // 主将带不满一队编制时，实际队数更多（改按主将上限折算）。
                int perUnit = weights.troopsPerUnit > 0 ? weights.troopsPerUnit : weights.defaultTroopsPerGeneral;
                if (perUnit <= 0) perUnit = 5000;
                if (city.Leader != null && city.Leader.TroopsLimit > 0 && city.Leader.TroopsLimit < perUnit)
                    perUnit = city.Leader.TroopsLimit;
                int unitCount = (int)Math.Ceiling(expectedTroops / (double)perUnit);
                int milByTroops = (int)Math.Ceiling(unitCount * (1.0 + Math.Max(0f, weights.deputyPerUnit)));
                if (milByTroops < 0) milByTroops = 0;

                int milFloor = isBorder ? weights.minMilitarySeatAtBorder : 0;
                int milPlan = baseSeat + ringSeat + threatSeat;
                milSeat = Math.Max(Math.Max(milByTroops, milPlan), milFloor);
                if (milSeat > weights.militarySeatHardMax) milSeat = weights.militarySeatHardMax;
                if (milSeat < 0) milSeat = 0;
            }

            // ---------- 港关守备（③ 放开：最低 0，常驻最多 5） ----------
            // 边境 1 + 中威胁 1 + 高威胁 2 + 人力充裕再 +1 → 最多 5；非边境低威胁仍为 0。
            int garrisonSeat = 0;
            if (isPortGate && weights.enablePortGateDistribute)
            {
                garrisonSeat = (isBorder ? 1 : 0)
                    + (threatLevel >= weights.threatMidAt ? 1 : 0)
                    + (threatLevel >= weights.threatHighAt ? 2 : 0)
                    + (isBorder && abundance > 1.5f ? 1 : 0);
                if (garrisonSeat > weights.portGateMaxSeat) garrisonSeat = weights.portGateMaxSeat;
                if (garrisonSeat < 0) garrisonSeat = 0;
            }

            // ---------- 内政工作族（港关不产内政） ----------
            int recruitSeat = 0;
            string recruitTrigger = null;
            int armSeat = 0;
            string armTrigger = null;
            int trainSeat = 0;
            string trainTrigger = null;
            int searchSeat = 0;
            string searchTrigger = null;
            int recruitPersonSeat = 0;
            string recruitPersonTrigger = null;
            int devSeat = 0;
            string devTrigger = null;
            int logisticsSeat = 0;
            string logisticsTrigger = null;
            int transportSeat = 0;

            if (!isPortGate)
            {
                // 征兵：兵力不足
                if (weights.recruitSlotEnabled && city.TroopsLimit > 0)
                {
                    float fill = (float)city.troops / city.TroopsLimit;
                    if (fill < weights.recruitFillTarget)
                    {
                        recruitSeat = fill < weights.recruitSevereFill ? 2 : 1;
                        recruitTrigger = string.Format("征兵: 兵力{0}/上限{1} ({2:P0})",
                            city.troops, city.TroopsLimit, fill);
                    }
                }

                // 军备：兵装 / 器械 / 船
                if (weights.armamentSlotEnabled)
                {
                    StringBuilder reasons = new StringBuilder();
                    if (situation.weaponCount < city.troops * weights.armamentCoverTarget
                        && (!weights.armamentNeedsFacility || situation.hasBlacksmith || situation.hasStable))
                    {
                        reasons.AppendFormat("兵装{0}<兵{1} ", situation.weaponCount, city.troops);
                    }
                    if (situation.hasMachineFactory && situation.machineCount < weights.machineCountTarget)
                    {
                        reasons.AppendFormat("器械{0}<{1} ", situation.machineCount, weights.machineCountTarget);
                    }
                    if (weights.boatSlotEnabled && situation.hasBoatFactory
                        && (situation.hasPort || (city.portList != null && city.portList.Count > 0)))
                    {
                        reasons.Append("船厂待造 ");
                    }
                    if (reasons.Length > 0)
                    {
                        armSeat = 1;
                        armTrigger = "军备: " + reasons.ToString().TrimEnd();
                    }
                }

                // 训练（决策①：最多 3 人 —— 这是游戏内训练机制的上限；人力充裕时多派人加速回气）
                if (weights.trainSlotEnabled)
                {
                    int maxMorale = city.MaxMorale > 0 ? city.MaxMorale : 100;
                    if (city.morale < maxMorale * weights.moraleFillTarget)
                    {
                        trainSeat = (int)Math.Round(abundance);
                        if (trainSeat < 1) trainSeat = 1;
                        if (trainSeat > weights.trainSeatMax) trainSeat = weights.trainSeatMax;
                        trainTrigger = string.Format("训练: 士气{0}/{1} 派{2}人(上限{3})",
                            city.morale, maxMorale, trainSeat, weights.trainSeatMax);
                    }
                }

                // 搜索（CityJobType.Searching：只负责"发现"**未发现**人才）—— 动态上限、最低 0
                if (weights.searchSlotEnabled)
                {
                    int invisible = CountInvisiblePersons(city);
                    if (invisible > 0)
                    {
                        searchSeat = (int)Math.Ceiling(invisible / (double)Math.Max(1f, weights.hiddenPerSearchSeat));
                        if (searchSeat < 1) searchSeat = 1;
                        if (searchSeat > weights.searchSeatMax) searchSeat = weights.searchSeatMax;
                        searchTrigger = string.Format("搜索: 未发现{0}人 派{1}人(上限{2})",
                            invisible, searchSeat, weights.searchSeatMax);
                    }
                    else
                    {
                        searchSeat = 0;                      // 没有未发现人才 → 不设岗（可以为 0）
                    }
                }

                // 登用（CityJobType.RecruitPerson：招揽**在野**武将）—— 动态上限、最低 0
                // 与搜索是两个阶段：搜索把"未发现"变成"在野"，登用把"在野"招进本势力。
                if (weights.recruitPersonSlotEnabled)
                {
                    int wild = CountWildPersons(city);
                    if (wild > 0)
                    {
                        // 【前期加成】开局前 N 回合 + 城内在野够多 → 优先登用：
                        //   ① 人手翻倍：每 earlyWildPerRecruitSeat(4) 人配 1 岗（平时是 8）；
                        //   ② 优先级由 9 提到 earlyRecruitPriority(3)，先于训练/搜索/运输/开发/后勤拿人。
                        bool early = RecruitPersonPriority(city, weights) < 9;
                        float perSeat = early && weights.earlyWildPerRecruitSeat > 0f
                            ? weights.earlyWildPerRecruitSeat
                            : weights.wildPerRecruitSeat;
                        recruitPersonSeat = (int)Math.Ceiling(wild / (double)Math.Max(1f, perSeat));
                        if (recruitPersonSeat < 1) recruitPersonSeat = 1;
                        if (recruitPersonSeat > weights.recruitPersonSeatMax)
                            recruitPersonSeat = weights.recruitPersonSeatMax;
                        recruitPersonTrigger = early
                            ? string.Format("登用: 在野{0}人 派{1}人(前期优先) 上限{2}",
                                wild, recruitPersonSeat, weights.recruitPersonSeatMax)
                            : string.Format("登用: 在野{0}人 派{1}人(上限{2})",
                                wild, recruitPersonSeat, weights.recruitPersonSeatMax);
                    }
                    else
                    {
                        recruitPersonSeat = 0;               // 城内无在野武将 → 不设岗（可以为 0）
                    }
                }

                // 开发：内政建设情况（基本盘）+ 开发缺口（是否加派）
                int interiorPoints = CountInteriorPoints(city);
                int devByBuilding = interiorPoints > 0
                    ? (int)Math.Ceiling(interiorPoints / (double)Math.Max(0.5f, weights.interiorPointsPerDevSeat))
                    : 0;

                float devGap = CalcDevelopGap(city);
                int devByGap = 0;
                if (devGap >= weights.devGapHigh) devByGap = weights.devGapBonusHigh;
                else if (devGap >= weights.devGapLow) devByGap = weights.devGapBonusLow;
                if (weights.devSeatNeedsInterior && interiorPoints == 0) devByGap = 0;

                // 开发修正：离边境距离（圈层）× 资源富裕度（用预想资金/粮草/兵装衡量）
                int devRaw = devByBuilding + devByGap;
                float devRing = CalcDevelopRingFactor(city, weights);
                float devResource = CalcResourceRate(city, weights, situation);
                float devModifier = CalcDevelopModifier(city, weights, situation);

                devSeat = (int)Math.Round(devRaw * (double)devModifier);

                // 【F7】开发岗位上限不再是一个死数字：
                //   · 基准仍是 devSeatMax（默认 3）—— 人力紧张的势力行为完全不变；
                //   · 人力丰裕时按"人均城数 / staffPerCityBaseline"放大（最多 staffAbundanceMax 倍）：
                //     这样"196 人却把成都的开发岗压到 3 人、100 多人闲置"的浪费不会再发生；
                //   · 上限与倍数直接印进原因链，便于核对。
                int devCap = weights.devSeatMax;
                float devAbundance = 1f;
                if (weights.staffAbundanceScaling && city.BelongForce != null)
                {
                    int forceCityCount = Math.Max(1, city.BelongForce.CityCount);
                    int forcePeople = city.BelongForce.PersonCount > 0
                        ? city.BelongForce.PersonCount
                        : personTotal;
                    float perCity = forcePeople / (float)forceCityCount;
                    devAbundance = perCity / Math.Max(1f, weights.staffPerCityBaseline);
                    if (devAbundance < 1f) devAbundance = 1f;
                    if (devAbundance > weights.staffAbundanceMax) devAbundance = weights.staffAbundanceMax;
                    devCap = Math.Max(devCap, (int)Math.Round(weights.devSeatMax * devAbundance));
                }

                if (devSeat > devCap) devSeat = devCap;
                if (devSeat < 0) devSeat = 0;
                if (devRaw > 0)
                {
                    devTrigger = string.Format("开发: 内政{0}点(→{1}人){2} 缺口{3:P0} 距边境{4}层(×{5:F2}) 资源{6:P0}(修正×{7:F2}) 上限{8}人{9}",
                        interiorPoints, devByBuilding,
                        devByGap > 0 ? string.Format(" 加派{0}", devByGap) : "",
                        devGap, city.borderLine, devRing, devResource, devModifier,
                        devCap,
                        devAbundance > 1.001f ? string.Format("(人力×{0:F1})", devAbundance) : "");
                }

                // 后勤（决策①：最多 2 人）—— 拆成两项各 1 人，原因链分别说明：
                //   · 治安项：后方城治安不足 → 巡查 / 警备；
                //   · 粮草项：后方城粮草吃紧 → 屯田 / 输送。
                if (city.borderLine >= 2)
                {
                    bool needSecurity = situation.securityRate < weights.logisticsSecurityTarget;
                    bool needFood = situation.foodFill < weights.logisticsFoodFillTarget;
                    logisticsSeat = (needSecurity ? 1 : 0) + (needFood ? 1 : 0);
                    if (logisticsSeat > weights.logisticsSeatMax) logisticsSeat = weights.logisticsSeatMax;
                    if (logisticsSeat > 0)
                    {
                        logisticsTrigger = string.Format("后勤: 治安{0:P0}{1} 粮草{2:P0}{3} 派{4}人(上限{5})",
                            situation.securityRate, needSecurity ? "→巡查" : "",
                            situation.foodFill, needFood ? "→屯粮" : "",
                            logisticsSeat, weights.logisticsSeatMax);
                    }
                }

                // 运输（决策①：1~2 人 —— 人力充裕时 2 人）
                if (weights.transportSlotEnabled && !isBorder
                    && city.troops >= weights.transportMinTroops && city.food >= weights.transportMinFood
                    && !IsTransportDisabled(city))
                {
                    transportSeat = abundance >= weights.transportExtraSeatAbundance
                        ? Math.Max(1, weights.transportSeatMax)
                        : 1;
                }
            }

            // ---------- 单城上限（占势力总人数比例） ----------
            int cap = personTotal > 0
                ? Math.Max(1, (int)Math.Floor(personTotal * weights.maxSeatSharePerCity))
                : int.MaxValue;
            int total = milSeat + garrisonSeat + recruitSeat + armSeat + trainSeat + searchSeat
                      + transportSeat + devSeat + logisticsSeat + recruitPersonSeat;
            if (total > cap)
            {
                // 牺牲顺序：后勤 → 开发 → 运输 → 搜索 → 训练 → 军备 → 征兵
                // （军事与港关守备不动：前者是战力，后者是领土）
                int over = total - cap;
                over = Cut(ref recruitPersonSeat, over);     // 登用最先牺牲（长期投资、非战力）
                over = Cut(ref logisticsSeat, over);
                over = Cut(ref devSeat, over);
                over = Cut(ref transportSeat, over);
                over = Cut(ref searchSeat, over);
                over = Cut(ref trainSeat, over);
                over = Cut(ref armSeat, over);
                over = Cut(ref recruitSeat, over);
            }

            // 【后方城保底】后方城至少保留 N 人（小1/中2/大3/巨5），用于"运输 + 战略资源积累"。
            // 放在"单城上限截断"之后：**保底优先于上限**（保底很小，且这正是需要的语义 ——
            // 后方城不该被军事/内政的其它优先项挤空，否则运输会断、资源积累会停）。
            if (!isPortGate && ring >= 2)
            {
                int rearMin = MinRearSeats(city, weights);
                int supportSeats = transportSeat + devSeat + logisticsSeat;
                if (supportSeats < rearMin)
                    devSeat += rearMin - supportSeats;      // 缺口优先补"资源积累"（开发）
            }

            // ---------- 生成岗位（按优先级：守备 → 军事 → 征兵 → 军备 → 训练 → 搜索 → 运输 → 开发 → 后勤） ----------
            for (int i = 0; i < garrisonSeat; i++)
                output.Add(MakePost(PostKind.Garrison, 0, true, weights, city, null));

            for (int i = 0; i < milSeat; i++)
            {
                // 前线 / 高威胁的第一军事岗位视为硬性
                bool required = (isBorder || threatLevel >= weights.threatHighAt) && i == 0;
                output.Add(MakePost(PostKind.Military, 1, required, weights, city,
                    i == 0
                        ? string.Format("军事: {0} 预想兵力{1}(当前{2}/兵力上限{3})",
                            isBorder ? "前线" : (threatLevel >= weights.threatHighAt ? "高威胁" : "常规"),
                            expectedTroops, city.troops, city.TroopsLimit)
                        : null));
            }
            for (int i = 0; i < recruitSeat; i++)
                output.Add(MakePost(PostKind.RecruitTroops, 2, false, weights, city, i == 0 ? recruitTrigger : null));
            for (int i = 0; i < armSeat; i++)
                output.Add(MakePost(PostKind.Armament, 3, false, weights, city, armTrigger));
            for (int i = 0; i < trainSeat; i++)
                output.Add(MakePost(PostKind.TrainTroops, 4, false, weights, city, trainTrigger));
            for (int i = 0; i < searchSeat; i++)
                output.Add(MakePost(PostKind.Search, 5, false, weights, city, searchTrigger));
            for (int i = 0; i < recruitPersonSeat; i++)
                output.Add(MakePost(PostKind.RecruitPerson, RecruitPersonPriority(city, weights), false, weights, city, recruitPersonTrigger));
            for (int i = 0; i < transportSeat; i++)
                output.Add(MakePost(PostKind.Transport, 6, false, weights, city,
                    string.Format("运输: 兵力{0} 粮{1}", city.troops, city.food)));
            for (int i = 0; i < devSeat; i++)
                output.Add(MakePost(PostKind.Develop, 7, false, weights, city, i == 0 ? devTrigger : null));
            for (int i = 0; i < logisticsSeat; i++)
                output.Add(MakePost(PostKind.Logistics, 8, false, weights, city, logisticsTrigger));
        }

        /// <summary>截断辅助：从 seat 中最多扣掉 over 个，返回剩余的 over。</summary>
        static int Cut(ref int seat, int over)
        {
            if (over <= 0) return 0;
            int cut = seat < over ? seat : over;
            seat -= cut;
            return over - cut;
        }

        static Post MakePost(PostKind kind, int priority, bool required, DeploymentWeights weights,
            City city, string trigger)
        {
            Post post;
            post.kind = kind;
            post.priority = priority;
            post.required = required;
            post.weights = weights.GetFit(kind);
            post.cityId = city.Id;
            post.cityName = city.Name;
            post.trigger = trigger;
            return post;
        }

        /// <summary>
        /// 内政点数 = 城内**内政设施**（<c>BuildingType.IsIntrior</c>）的等级之和（等级 ≤ 0 按 1 计）。
        /// 这是"内政地建设情况"的量化口径，用于决定开发人手多少。
        /// </summary>
        public static int CountInteriorPoints(City city)
        {
            if (city == null || city.allBuildings == null)
                return 0;

            int points = 0;
            for (int i = 0; i < city.allBuildings.Count; i++)
            {
                Building building = city.allBuildings[i];
                if (building == null || building.BuildingType == null)
                    continue;
                if (!building.BuildingType.IsIntrior)
                    continue;

                int level = building.BuildingType.level;
                points += level > 0 ? level : 1;
            }
            return points;
        }

        /// <summary>
        /// 登用岗优先级：平时 9（排在后勤之后 —— 登用是长期投资，不抢稀缺人手）；
        /// **前期加成**：开局前 <c>earlyGameTurns</c> 回合内、且城内在野武将 ≥
        /// <c>earlyRecruitWildThreshold</c> 时，提到 <c>earlyRecruitPriority</c>(3)，
        /// 让"登用"先于训练/搜索/运输/开发/后勤拿到人（前期招人回报最高）。
        /// 回合号取自 <see cref="DeploymentState.currentForceTurn"/>（换剧本自动归零）。
        /// </summary>
        public static int RecruitPersonPriority(City city, DeploymentWeights weights)
        {
            const int NormalPriority = 9;
            if (city == null || weights == null)
                return NormalPriority;

            int turn = DeploymentState.currentForceTurn;
            if (turn <= 0 || turn > weights.earlyGameTurns)
                return NormalPriority;                       // 非前期（或未初始化）→ 常规

            if (CountWildPersons(city) < weights.earlyRecruitWildThreshold)
                return NormalPriority;                       // 在野不够多 → 不值当前期优先

            return Math.Min(NormalPriority, Math.Max(0, weights.earlyRecruitPriority));
        }

        /// <summary>
        /// 最低运转保底（非港关、圈层 ≥ rearFloorMinRing，默认 1 = 次前线与后方）：
        /// **小/中 2 人、大/巨 3 人**。用于"运输 + 战略资源积累"；
        /// 编制（截尾后补开发）与调动闸门（源城不得抽到低于此值）共用同一口径。
        /// 上限由 <c>maxSeatAtRearByLevel</c> 与"人均城数"共同约束（人多才放宽）。
        /// </summary>
        public static int MinRearSeats(City city, DeploymentWeights weights)
        {
            if (city == null || weights == null || weights.minSeatAtRearByLevel == null)
                return 0;
            if (city.IsPort() || city.IsGate())
                return 0;
            // 【最低运转保底】次前线(1) + 后方(≥2) 都受约束；边境(0) 交给军事保底。
            // 原实现只约束圈层 ≥ 2，导致"次前线"城可以被抽到 0 人 —— 内政/运输直接停摆。
            if (ResolveRing(city) < Math.Max(0, weights.rearFloorMinRing))
                return 0;

            int idx = city.CityLevelType != null ? city.CityLevelType.Id - 1 : 0;
            if (idx < 0) idx = 0;

            int lvMin = ArrayAt(weights.minSeatAtRearByLevel, idx);
            if (!weights.enableDynamicRearFloor)
                return Math.Max(0, lvMin);

            // 【动态基准兜底】用"人均城数"平衡后方（与 abundance 同基数，口径统一）：
            //   目标 = 人均城数 × rearShareRatio，再夹在 [等级下限, 等级上限]，最后不超过人均。
            //   人多 → 后方留得住人（运输 / 资源积累转得动）；
            //   人少 → 夹到下限后再被"人均"压回来，不会强留、不会饿着前线。
            float avg = 0f;
            if (city.BelongForce != null && city.BelongForce.CityCount > 0)
                avg = city.BelongForce.PersonCount / (float)city.BelongForce.CityCount;
            if (avg <= 0f)
                return Math.Max(0, lvMin);                  // 取不到势力规模 → 退回固定保底

            int lvMax = weights.maxSeatAtRearByLevel != null
                ? ArrayAt(weights.maxSeatAtRearByLevel, idx)
                : lvMin;
            if (lvMax < lvMin) lvMax = lvMin;

            int target = (int)Math.Round(avg * weights.rearShareRatio);
            if (target < lvMin) target = lvMin;
            if (target > lvMax) target = lvMax;

            int peopleCap = (int)Math.Floor(avg);           // 无论如何不超过人均
            if (peopleCap < 1) peopleCap = 1;
            if (target > peopleCap) target = peopleCap;

            return Math.Max(0, target);
        }

        static int ArrayAt(int[] arr, int idx)
        {
            if (arr == null || arr.Length == 0)
                return 0;
            if (idx < 0) idx = 0;
            if (idx >= arr.Length) idx = arr.Length - 1;
            return arr[idx];
        }

        /// <summary>
        /// **未发现**人才数（`invisiblePersons`）= 本城 + 所属港关 —— 这是「搜索」岗的依据
        /// （搜索把未发现变成在野）。与「登用」分开统计，两者不再混为一谈。
        /// </summary>
        public static int CountInvisiblePersons(City city)
        {
            return CountByPool(city, true);
        }

        /// <summary>
        /// **在野**武将数（`wildPersons`）= 本城 + 所属港关 —— 这是「登用」岗的依据
        /// （登用把在野招进本势力，CityJobType.RecruitPerson）。
        /// </summary>
        public static int CountWildPersons(City city)
        {
            return CountByPool(city, false);
        }

        static int CountByPool(City city, bool invisible)
        {
            if (city == null)
                return 0;

            int count = 0;
            if (invisible)
            {
                if (city.invisiblePersons != null) count += city.invisiblePersons.Count;
            }
            else
            {
                if (city.wildPersons != null) count += city.wildPersons.Count;
            }

            if (city.subCities != null)
            {
                for (int i = 0; i < city.subCities.Count; i++)
                {
                    City sub = city.subCities[i];
                    if (sub == null) continue;
                    if (invisible)
                    {
                        if (sub.invisiblePersons != null) count += sub.invisiblePersons.Count;
                    }
                    else
                    {
                        if (sub.wildPersons != null) count += sub.wildPersons.Count;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 待发现人才数 = 未发现 + 在野，含所属港关（搜索会发现港关的人才，见 City.cs:3456）。
        /// </summary>
        public static int CountHiddenPersons(City city)
        {
            if (city == null)
                return 0;

            int count = 0;
            if (city.invisiblePersons != null) count += city.invisiblePersons.Count;
            if (city.wildPersons != null) count += city.wildPersons.Count;

            if (city.subCities != null)
            {
                for (int i = 0; i < city.subCities.Count; i++)
                {
                    City sub = city.subCities[i];
                    if (sub != null && sub.invisiblePersons != null)
                        count += sub.invisiblePersons.Count;
                }
            }
            return count;
        }

        /// <summary>
        /// 开发缺口（0..1）：农业 / 商业与各自上限的差距均值。缺口越大越"急"，
        /// 只用于决定是否加派开发人手（基本盘由内政建设情况决定）。
        /// </summary>
        public static float CalcDevelopGap(City city)
        {
            if (city == null || city.CityLevelType == null)
                return 0f;

            float commerceRate, agricultureRate;
            try
            {
                commerceRate = city.CommerceLimit > 0 ? (float)city.commerce / city.CommerceLimit : 1f;
                agricultureRate = city.AgricultureLimit > 0 ? (float)city.agriculture / city.AgricultureLimit : 1f;
            }
            catch (Exception)
            {
                // 港关等极端数据下上限链可能抛异常，按"无缺口"处理，不影响整体部署
                return 0f;
            }

            if (commerceRate > 1f) commerceRate = 1f;
            if (agricultureRate > 1f) agricultureRate = 1f;

            float gap = 1f - (commerceRate + agricultureRate) * 0.5f;
            if (gap < 0f) gap = 0f;
            if (gap > 1f) gap = 1f;
            return gap;
        }

        /// <summary>
        /// 离边境距离系数：圈层取自 <see cref="ResolveRing"/>（港关按归属都市）。
        /// 越靠近边境越不适合大搞内政（资源优先军需、随时可能失守），越纵深越适合发展。
        /// 系数表见 <c>DeploymentWeights.devRingFactor</c>（默认 0.5 / 0.8 / 1.0 / 1.15）。
        /// 【R2】`borderLine == -1`（未定）按最深层处理，避免被当成最前线打对折。
        /// </summary>
        public static float CalcDevelopRingFactor(City city, DeploymentWeights weights)
        {
            float[] table = weights != null ? weights.devRingFactor : null;
            if (table == null || table.Length == 0)
                return 1f;

            int ring = ResolveRing(city);
            if (ring < 0) ring = table.Length - 1;              // 未定 → 最深层
            if (ring >= table.Length) ring = table.Length - 1;
            return table[ring];
        }

        /// <summary>
        /// 取"形势圈层"（【R1】）。
        ///
        /// `Force.UpdateTurnInfo`（`Force.cs:840-914`）只对 `IsCity()` 计算 `borderLine`，
        /// 因此**港口 / 关卡的 borderLine 恒为 0（= 边境）** —— 直接使用会让所有港关都被当作前线，
        /// "最低 0 人"永远不成立。这里让港关以其**归属都市**（`City.BelongCity`）的圈层为准。
        /// </summary>
        /// <param name="city">城池（都市 / 港口 / 关卡）</param>
        /// <returns>圈层（0 = 边境；&lt;0 = 未定）</returns>
        public static int ResolveRing(City city)
        {
            if (city == null)
                return 0;

            if (city.IsPort() || city.IsGate())
            {
                City parent = city.BelongCity;
                if (parent != null && parent != city)
                    return ResolveRing(parent);
            }
            return city.borderLine;
        }

        /// <summary>
        /// 资源富裕度（0..1）：用**预想资金 / 预想粮草 / 预想兵装**衡量，而不是当前库存 ——
        /// 运输搬走粮草、赏赐花掉金钱都不会让开发编制抖动。
        /// 兵装率 = 预想兵装 ÷ 预想兵力（1.0 = 每兵一件）。
        /// </summary>
        public static float CalcResourceRate(City city, DeploymentWeights weights, CitySituation situation)
        {
            if (city == null || weights == null)
                return 0f;

            float goldRate = city.GoldLimit > 0
                ? (float)DeploymentState.GetExpectedGold(city, weights, situation) / city.GoldLimit : 1f;
            float foodRate = city.FoodLimit > 0
                ? (float)DeploymentState.GetExpectedFood(city, weights, situation) / city.FoodLimit : 1f;

            int expectedTroops = DeploymentState.GetExpectedTroops(city, weights, situation);
            float weaponRate = expectedTroops > 0
                ? (float)DeploymentState.GetExpectedWeapon(city, weights, situation) / expectedTroops : 1f;

            goldRate = Clamp01(goldRate);
            foodRate = Clamp01(foodRate);
            weaponRate = Clamp01(weaponRate);
            return Clamp01((goldRate + foodRate + weaponRate) / 3f);
        }

        /// <summary>
        /// 开发修正 = `离边境距离系数 × (devResourceBase + (1 − devResourceBase) × 资源富裕度)`，
        /// 并裁剪到 `[devModMin, devModMax]`。
        /// 直观含义：前线打对折（资源优先军需），纵深富城最多加成到 1.5 倍。
        /// </summary>
        public static float CalcDevelopModifier(City city, DeploymentWeights weights, CitySituation situation)
        {
            if (weights == null)
                return 1f;

            float ring = CalcDevelopRingFactor(city, weights);
            float resource = CalcResourceRate(city, weights, situation);

            float baseRate = Clamp01(weights.devResourceBase);
            float modifier = ring * (baseRate + (1f - baseRate) * resource);

            if (modifier < weights.devModMin) modifier = weights.devModMin;
            if (modifier > weights.devModMax) modifier = weights.devModMax;
            return modifier;
        }

        static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        /// <summary>军团是否禁用了运输（沿用现有托管开关）。</summary>
        static bool IsTransportDisabled(City city)
        {
            Corps corps = city != null ? city.BelongCorps : null;
            if (corps == null) return false;
            return corps.GetAppointValue(Corps.AppointContentType.TransportDisable) == 1;
        }
    }
}
