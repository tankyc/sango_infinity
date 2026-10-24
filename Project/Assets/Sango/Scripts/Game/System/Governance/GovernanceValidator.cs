namespace Sango.Core
{
    /// <summary>
    /// 归属链一致性巡检与自动修复。
    ///
    /// 【为什么需要】武将的身份/位置/势力/军团分散在 5 个字段 + 城市侧 6 份名册里，
    /// 任何一处漏改都会留下"在野却有势力""太守字段指向已阵亡武将""都市属 A、港口属 B"
    /// 这类脏数据，并在后续的随机移动 / 登庸 / 内政链路中继续扩散。
    /// 治理层（PersonStateSystem / CityStateSystem）负责"不再产生"新的脏数据，
    /// 本类负责"把已经产生的修掉"，两者缺一不可。
    ///
    /// 【对齐 Open311】对应 Person::is_alive / CheckBelongConsistency 的语义，
    /// 但把"仅读档时校验一次"提升为"任意时机可调用的全局巡检"。
    ///
    /// 【修复原则】保守修复，只处理能确定的矛盾：
    ///   1. 以所属城市为准（城是归属链的锚点，与 Open311 的"以 service/district 为准"一致）
    ///   2. 不臆造数据：无法判断的只报错，不猜
    ///   3. 每处修复都打日志，便于追查上游是谁写坏的
    ///   4. **日志级别**：本类属于"发现即修复"的自愈流程，修复动作一律用 Warning
    ///      （受 SANGO_DEBUG 宏控制，正式包编译期删除）；只有"修不了、需要人工介入"的
    ///      才用 Error（Error 不受宏控制、正式包也会输出，不能拿它刷屏）。
    ///      早期版本全部写成 Error，年度巡检一跑就是满屏红字 + 完整堆栈，
    ///      真正的崩溃反而被淹没 —— 这就是把级别拆开的原因。
    /// </summary>
    public static class GovernanceValidator
    {
        /// <summary>
        /// 全局巡检。
        /// </summary>
        /// <param name="scenario">剧本/存档上下文</param>
        /// <param name="repair">是否自动修复（false 时只统计问题）</param>
        /// <returns>发现的问题数量</returns>
        public static int ValidateAll(Scenario scenario, bool repair = true)
        {
            if (scenario == null)
                return 0;

            int issueCount = 0;

            // 1. 武将
            if (scenario.personSet != null)
            {
                for (int i = 0; i < scenario.personSet.Count; i++)
                {
                    Person person = scenario.personSet[i];
                    if (ValidatePerson(person, repair))
                        issueCount++;
                }
            }

            // 2. 城池/关隘/港口
            if (scenario.citySet != null)
            {
                for (int i = 0; i < scenario.citySet.Count; i++)
                {
                    City city = scenario.citySet[i];
                    if (ValidateCity(city, repair))
                        issueCount++;
                }
            }

            // 3. 军团
            if (scenario.corpsSet != null)
            {
                for (int i = 0; i < scenario.corpsSet.Count; i++)
                {
                    Corps corps = scenario.corpsSet[i];
                    if (ValidateCorps(corps, repair))
                        issueCount++;
                }
            }

            // 4. 地图建筑：陷阱（Explosive）/ 障碍（Obstacle）建成后必须中立。
            //    对应 Open311 Building::set_completed 里对 trap/obstacle 的
            //    `set_owner_force_id(-1)`：这两类设施的效果对敌我一致，
            //    不能挂在某一方名下（否则敌方踩雷会算我方战果、设施归属面板也会错乱）。
            if (scenario.buildingSet != null)
            {
                int buildingIssue = 0;
                for (int i = 0; i < scenario.buildingSet.Count; i++)
                {
                    Building building = scenario.buildingSet[i];
                    if (building == null || !building.IsAlive || !building.isComplate)
                        continue;

                    BuildingType type = building.BuildingType;
                    if (type == null)
                        continue;
                    if (!type.IsExplosive && !type.IsObstacle)
                        continue;

                    // 堤防是陷阱类型里的例外（原版 is_trap_type 显式排除），需要保留归属
                    if (type.kind == (int)BuildingKindType.Levee)
                        continue;

                    if (building.BelongForce == null && building.BelongCorps == null)
                        continue;

                    buildingIssue++;
                    Sango.Log.Warning($"*[归属巡检] 陷阱/障碍 {building.Name} 残留归属 {Describe(building.BelongForce)}/{Describe(building.BelongCorps)}");
                    if (repair)
                        building.OnCompleted();
                }

                if (buildingIssue > 0)
                {
                    issueCount += buildingIssue;
                    Sango.Log.Warning($"*[归属巡检] 已中立化 {buildingIssue} 处陷阱/障碍");
                }
            }

            // 5. 在途武将表一致性。
            // 有任务的人必须在表里，否则他的任务永远不会被推进（回合结束时只遍历该表），
            // 表现为"武将卡在移动中/出使中不动"。这类泄漏通常来自直接赋 missionType
            // 或早期存档的历史数据。
            if (scenario.personSet != null)
            {
                int actorIssue = 0;
                for (int i = 0; i < scenario.personSet.Count; i++)
                {
                    Person person = scenario.personSet[i];
                    if (person == null || !person.IsAlive || person.IsDead)
                        continue;
                    if (person.missionType == 0)
                        continue;

                    // 手写查找而不是 LINQ.Contains：Actors 是 IReadOnlyList，且表本身很小
                    bool found = false;
                    for (int k = 0; k < scenario.Actors.Count; k++)
                    {
                        if (scenario.Actors[k] == person)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                        continue;

                    actorIssue++;
                    Sango.Log.Warning($"*[在途巡检] {person.Name} 有任务(missionType={person.missionType}) 但不在在途武将表里");
                    if (repair)
                        scenario.RegisterActor(person);
                }

                if (actorIssue > 0)
                {
                    issueCount += actorIssue;
                    Sango.Log.Warning($"*[在途巡检] 补登记 {actorIssue} 名漏登记的在途武将");
                }
            }

            // 6. 部队成员类型：事件武将 / NPC 不应编入部队。
            //    对应 Open311 Unit::can_set_member 的白名单判定
            //    （历史 / 古代 / 自建 / 临时才允许）。
            //    这里只报告不强行移除：摘主将会连锁触发部队解散，
            //    在巡检里做这种事风险远大于收益，交由玩家/AI 自行调整。
            if (scenario.troopsSet != null)
            {
                int memberIssue = 0;
                for (int i = 0; i < scenario.troopsSet.Count; i++)
                {
                    Troop troop = scenario.troopsSet[i];
                    if (troop == null || !troop.IsAlive)
                        continue;

                    troop.ForEachPerson(person =>
                    {
                        if (person == null || !person.IsAlive)
                            return;
                        if (person.CanBeTroopMember)
                            return;

                        memberIssue++;
                        Sango.Log.Warning($"*[归属巡检] 部队 {troop.Name} 的成员 {person.Name} 类型为 {(PersonType)person.type}，按原版不允许编入部队");
                    });
                }

                if (memberIssue > 0)
                    issueCount += memberIssue;
            }

            if (issueCount > 0)
            {
                Sango.Log.Warning($"*[归属巡检] 发现 {issueCount} 处问题，repair={repair}");
            }

            return issueCount;
        }

        /// <summary>
        /// 校验单个武将。
        ///
        /// 【不处理的武将】剧情 / NPC / 异族（<see cref="Person.IsSpecialPerson"/>）直接跳过：
        ///   这几类由事件系统或异族系统临时创建，归属字段由剧本 / 配置按需设置，
        ///   **不遵循**"以城为锚点"的不变式（剧情经常故意让某人不在任何城、或临时挂某势力下；
        ///   异族模板武将的 BelongCity 本来就是 0）。巡检若按常规规则去"修"，
        ///   会把人踢出城、清掉势力，直接打断剧情或把异族模板下野。
        ///   判定复用 <see cref="Person.IsSpecialPerson"/>（= 事件 + NPC + 异族，
        ///   与原版 `is_event() or is_iminzoku() or is_bandit()` 同口径），不在这里另写黑名单。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="repair">是否自动修复</param>
        /// <returns>是否存在问题</returns>
        public static bool ValidatePerson(Person person, bool repair = true)
        {
            if (person == null || person.Id <= 0)
                return false;

            // 剧情 / NPC / 异族武将不参与归属巡检（见上方说明）
            if (person.IsSpecialPerson)
                return false;

            bool hasIssue = false;
            PersonStateType state = (PersonStateType)person.state;

            // 【与原版对齐】Open311 的 is_alive 是"读 mibun 现算"的派生判定；
            // 本项目 IsAlive 是独立的存储标记，Person.Dead() 只改 state 不改它，
            // 于是"state 是死亡、IsAlive 还是 true"，任何用 IsAlive 过滤的地方都会把死人当活人。
            // 这里以 state 为准同步一次，让两者从此刻起给出同一个答案。
            if (state == PersonStateType.Dead && person.IsAlive)
            {
                Sango.Log.Warning($"*[归属巡检] 死亡武将 {person.Name} 的 IsAlive 仍为 true，已同步为 false");
                hasIssue = true;
                if (repair)
                    person.IsAlive = false;
            }

            switch (state)
            {
                case PersonStateType.Dead:
                    hasIssue |= CheckDead(person, repair);
                    break;

                case PersonStateType.Unemployed:
                    hasIssue |= CheckUnemployed(person, repair);
                    break;

                case PersonStateType.Invisible:
                    hasIssue |= CheckInvisible(person, repair);
                    break;

                case PersonStateType.Prisoner:
                    hasIssue |= CheckPrisoner(person, repair);
                    break;

                case PersonStateType.Governor:
                case PersonStateType.Commander:
                case PersonStateType.Leader:
                case PersonStateType.Normal:
                    hasIssue |= CheckInService(person, repair);
                    break;

                default:
                    // 错误状态：不臆测，只报告
                    //Sango.Log.Warning($"*[归属巡检]{person.Name} 身份非法 state={person.state}");
                    //hasIssue = true;
                    break;
            }

            return hasIssue;
        }

        /// <summary>
        /// 校验单个城池。
        /// </summary>
        /// <param name="city">城池</param>
        /// <param name="repair">是否自动修复</param>
        /// <returns>是否存在问题</returns>
        public static bool ValidateCity(City city, bool repair = true)
        {
            if (city == null || city.Id <= 0 || !city.IsAlive)
                return false;

            bool hasIssue = false;

            // 1. 势力必须与军团势力一致（归属链核心不变式）
            if (city.BelongCorps != null && city.BelongForce != city.BelongCorps.BelongForce)
            {
                Sango.Log.Warning($"*[归属巡检]{city.Name} 势力{Describe(city.BelongForce)} != 军团{city.BelongCorps.Name}的势力{Describe(city.BelongCorps.BelongForce)}");
                hasIssue = true;
                if (repair)
                {
                    city.BelongForce = city.BelongCorps.BelongForce;
                    Sango.Log.Warning($"*[归属巡检]{city.Name} 已修正势力为 {Describe(city.BelongForce)}");
                }
            }

            // 2. 势力为空却挂着军团：军团是势力的下属，无势力就无军团
            if (city.BelongCorps != null && city.BelongForce == null)
            {
                hasIssue = true;
                if (repair)
                {
                    city.BelongCorps = null;
                    Sango.Log.Warning($"*[归属巡检]{city.Name} 无势力，已清空军团");
                }
            }

            // 3. 太守有效性：必须活着、属于本城、且在本城在籍名册里
            if (city.Leader != null)
            {
                bool leaderValid = city.Leader.IsAlive
                    && city.Leader.BelongCity == city
                    && city.allPersons.Contains(city.Leader);

                if (!leaderValid)
                {
                    // 拆开打印具体是哪一条不满足：
                    //   · "已失效（死亡/已调离/不在名册）"这种笼统描述会逼着回头翻代码，
                    //     而三个条件的修复方式完全不同（死亡→重选，户口迁走→重算，名册缺失→补登记）。
                    string reason;
                    if (!city.Leader.IsAlive || city.Leader.IsDead)
                        reason = $"已死亡(state={(PersonStateType)city.Leader.state})";
                    else if (city.Leader.BelongCity != city)
                        reason = $"户口已迁至 {city.Leader.BelongCity?.Name ?? "无"}";
                    else
                        reason = "不在本城在籍名册 allPersons";

                    Sango.Log.Warning($"*[归属巡检]{city.Name} 太守 {city.Leader.Name} 已失效（{reason}）");
                    hasIssue = true;
                    if (repair)
                    {
                        if (city.Leader.IsLeader)
                            city.Leader.SetStateNormal();
                        city.Leader = null;
                        CityStateSystem.AppointTaishu(city);
                    }
                }
            }

            // 4. 有在籍武将却没有太守：补一次任命。
            //    空城（无势力）不参与，否则会给无主城凭空造出一个太守。
            //
            //    补任命后仍为空 → 说明在籍武将全都不在城里（出征 / 在途 / 死亡），
            //    这是**合法状态**（原版 can_be_taishu 要求 is_at_service，
            //    人不在城就不该被任命）。早期版本会对这种城反复报错 + 反复尝试任命，
            //    日志刷屏但永远补不上。
            if (city.Leader == null && city.allPersons.Count > 0 && city.BelongForce != null)
            {
                if (repair)
                {
                    CityStateSystem.AppointTaishu(city);
                    if (city.Leader != null)
                    {
                        hasIssue = true;
                        Sango.Log.Warning($"*[归属巡检]{city.Name} 太守为空，已重新任命 {city.Leader.Name}");
                    }
                    else
                    {
                        Sango.Log.Info($"*[归属巡检]{city.Name} 暂无可用太守（在籍武将均不在城内，等其回城后自动任命）");
                    }
                }
                else
                {
                    hasIssue = true;
                    Sango.Log.Error($"*[归属巡检]{city.Name} 有在籍武将但太守为空");
                }
            }

            // 5. 在籍名册与武将所属城必须互相对应
            if (city.allPersons.Count > 0)
            {
                System.Collections.Generic.List<Person> stale = null;
                for (int i = 0; i < city.allPersons.Count; i++)
                {
                    Person person = city.allPersons[i];
                    if (person == null)
                        continue;
                    if (person.BelongCity != city || !person.IsAlive)
                    {
                        if (stale == null)
                            stale = new System.Collections.Generic.List<Person>();
                        stale.Add(person);
                    }
                }

                if (stale != null)
                {
                    hasIssue = true;
                    if (repair)
                    {
                        for (int i = 0; i < stale.Count; i++)
                        {
                            city.allPersons.Remove(stale[i]);
                            Sango.Log.Warning($"*[归属巡检]{city.Name} 名册剔除脏数据 {stale[i].Name}");
                        }
                    }
                }
            }

            // 6. 下属港关：只校验"军团引用是否悬空"，不要求跟随都市
            hasIssue |= CheckSubCities(city, repair);

            return hasIssue;
        }

        /// <summary>
        /// 校验单个军团。
        /// </summary>
        /// <param name="corps">军团</param>
        /// <param name="repair">是否自动修复</param>
        /// <returns>是否存在问题</returns>
        public static bool ValidateCorps(Corps corps, bool repair = true)
        {
            if (corps == null || corps.Id <= 0 || !corps.IsAlive)
                return false;

            bool hasIssue = false;

            // 势力为空的僵尸军团
            if (corps.BelongForce == null)
            {
                Sango.Log.Warning($"*[归属巡检] 军团{corps.Name} 无势力");
                hasIssue = true;
                return hasIssue;
            }

            // 军团长有效性
            if (corps.mComander != null)
            {
                bool valid = corps.mComander.IsAlive && corps.mComander.BelongForce == corps.BelongForce;
                if (!valid)
                {
                    Sango.Log.Warning($"*[归属巡检] 军团{corps.Name} 军团长 {corps.mComander.Name} 已失效");
                    hasIssue = true;
                    if (repair)
                    {
                        corps.mComander = null;
                        corps.Comander = 0;
                        CityStateSystem.AppointTotoku(corps);
                    }
                }
            }
            else if (repair)
            {
                hasIssue = true;
                CityStateSystem.AppointTotoku(corps);
            }

            // 势力无任何存活武将（对应 Open311 district_appoint_totoku 的 A 分支：
            // force_is_not_empty 为假时直接 force_ruin）。
            //
            // 这里只报告、不自动灭亡：Force.Ruin 会连锁触发 OnForceFall 演出链路，
            // 而该事件目前假定"有一座陷落的城"作为上下文，凭空灭亡会拿到 null。
            // 真正需要自动灭亡时，应由城陷流程或显式调用 Force.Ruin 驱动。
            if (corps.BelongForce != null && !corps.BelongForce.HasAnyAlivePerson())
            {
                Sango.Log.Error($"*[归属巡检] 势力 {corps.BelongForce.Name} 已无任何存活武将，按原版应立即灭亡（需手动处理）");
                hasIssue = true;
            }

            // 支军团（第 2 军团及以后）无城应由 Force 负责解散，这里只报告。
            // 第 1 军团允许为空：势力可能只剩港关，或正处于灭亡流程的中间态。
            if (corps.number > 1)
            {
                int cityCount = 0;
                corps.ForEachCity(c => cityCount++);
                if (cityCount == 0)
                {
                    Sango.Log.Error($"*[归属巡检] 军团{corps.Name}（{corps.BelongForce.Name} 第{corps.number}军团）没有任何城池");
                    hasIssue = true;
                }
            }

            return hasIssue;
        }

        /// <summary>
        /// 死亡武将：不允许残留任何归属。
        /// </summary>
        private static bool CheckDead(Person person, bool repair)
        {
            if (person.BelongCity == null && person.BelongForce == null
                && person.BelongCorps == null && person.mBelongTroop == null)
                return false;

            Sango.Log.Warning($"*[归属巡检] 死亡武将 {person.Name} 仍残留 城:{Describe(person.BelongCity)} 势力:{Describe(person.BelongForce)} 军团:{Describe(person.BelongCorps)} 部队:{(person.mBelongTroop != null ? person.mBelongTroop.Name : "无")}");

            if (repair)
            {
                person.BelongCity?.RemovePerson(person);
                person.BelongCity?.RemoveWildPerson(person);
                person.BelongCity?.RemoveInvisiblePerson(person);
                person.BelongForce?.BeCaptiveList.Remove(person);
                person.BelongCity = null;
                person.BelongForce = null;
                person.BelongCorps = null;
                if (person.mBelongTroop != null)
                    person.mBelongTroop.RemovePerson(person);
            }

            return true;
        }

        /// <summary>
        /// 在野武将：不能有势力/军团/部队，且必须有所在城市。
        /// </summary>
        private static bool CheckUnemployed(Person person, bool repair)
        {
            bool hasIssue = false;

            if (person.BelongForce != null || person.BelongCorps != null)
            {
                Sango.Log.Warning($"*[归属巡检] 在野武将 {person.Name} 仍隶属 势力:{Describe(person.BelongForce)} 军团:{Describe(person.BelongCorps)}");
                hasIssue = true;
                if (repair)
                {
                    person.BelongForce = null;
                    person.BelongCorps = null;
                }
            }

            if (person.mBelongTroop != null)
            {
                Sango.Log.Warning($"*[归属巡检] 在野武将 {person.Name} 仍在部队 {person.mBelongTroop.Name} 中");
                hasIssue = true;
                if (repair)
                    person.mBelongTroop.RemovePerson(person);
            }

            // 在野必须有落脚城，否则随机移动 / 登庸会空引用
            City city = person.CurrentCity ?? person.BelongCity;
            if (city == null)
            {
                Sango.Log.Warning($"*[归属巡检] 在野武将 {person.Name} 没有任何所在城");
                hasIssue = true;
                if (repair)
                {
                    city = FindAnyCity();
                    if (city != null)
                    {
                        person.BelongCity = city;
                        person.ChangeCurrentCity(city);
                        city.AddWildPerson(person);
                        Sango.Log.Warning($"*[归属巡检] 在野武将 {person.Name} 已安置到 {city.Name}");
                    }
                }
            }
            else if (person.BelongCity == null)
            {
                // 所属城为空时用所在城补齐（在野不参与组织归属，但需要一致的落脚点）
                if (repair)
                    person.BelongCity = city;
            }

            return hasIssue;
        }

        /// <summary>
        /// 未发现武将：不能有势力/军团/部队，且必须在未发现名册里。
        /// </summary>
        private static bool CheckInvisible(Person person, bool repair)
        {
            bool hasIssue = false;

            if (person.BelongForce != null || person.BelongCorps != null || person.mBelongTroop != null)
            {
                Sango.Log.Warning($"*[归属巡检] 未发现武将 {person.Name} 残留归属 势力:{Describe(person.BelongForce)} 军团:{Describe(person.BelongCorps)} 部队:{(person.mBelongTroop != null ? person.mBelongTroop.Name : "无")}");
                hasIssue = true;
                if (repair)
                {
                    person.BelongForce = null;
                    person.BelongCorps = null;
                    if (person.mBelongTroop != null)
                        person.mBelongTroop.RemovePerson(person);
                }
            }

            City city = person.CurrentCity ?? person.BelongCity;
            if (city == null)
            {
                Sango.Log.Warning($"*[归属巡检] 未发现武将 {person.Name} 没有任何所在城");
                hasIssue = true;
                if (repair)
                {
                    city = FindAnyCity();
                    if (city != null)
                    {
                        person.BelongCity = city;
                        person.ChangeCurrentCity(city);
                        city.AddInvisiblePerson(person);
                    }
                }
            }

            return hasIssue;
        }

        /// <summary>
        /// 俘虏：只能关押在**城市**或**部队**中，且必须在对应的俘虏名册里；
        /// 户口为空、保留原势力（登用判定需要）。
        ///
        /// 两种合法形态（对应 Open311：俘虏必然挂在某个 location 上）：
        ///   · 城内俘虏：mBelongTroop == null，登记在 city.captiveList；
        ///   · 随军俘虏：mBelongTroop == troop，登记在 troop.captiveList。
        ///
        /// 【历史错误】早期版本把"俘虏属于部队"判成脏数据并强行 RemovePerson，
        /// 这会把野战收押的俘虏错判成非法状态 —— 随军俘虏是正常玩法。
        /// </summary>
        private static bool CheckPrisoner(Person person, bool repair)
        {
            bool hasIssue = false;

            Troop troop = person.mBelongTroop;
            City city = person.CurrentCity;

            // 1. 关押位置：必须落在部队或城市的俘虏名册之一
            if (troop != null)
            {
                // 随军俘虏：登记在部队的俘虏名册
                if (!troop.captiveList.Contains(person))
                {
                    Sango.Log.Warning($"*[归属巡检] 随军俘虏 {person.Name} 未登记在部队 {troop.Name}.captiveList");
                    hasIssue = true;
                    if (repair)
                        troop.captiveList.Add(person);
                }
            }
            else if (city != null)
            {
                // 城内俘虏：登记在城的俘虏名册
                if (!city.captiveList.Contains(person))
                {
                    Sango.Log.Warning($"*[归属巡检] 俘虏 {person.Name} 未登记在 {city.Name}.captiveList");
                    hasIssue = true;
                    if (repair)
                        city.AddCaptive(person);
                }
            }
            else
            {
                // 既不在部队也不在城：无从关押，转在野兜底
                Sango.Log.Warning($"*[归属巡检] 俘虏 {person.Name} 既不在部队也不在城中");
                hasIssue = true;
                if (repair)
                    PersonStateSystem.LeaveToWild(person, null, PersonChangeReason.Fixed);
                return true;
            }

            // 2. 户口必须为空：俘虏没有所属城
            if (person.BelongCity != null)
            {
                Sango.Log.Warning($"*[归属巡检] 俘虏 {person.Name} 仍挂着所属城 {person.BelongCity.Name}");
                hasIssue = true;
                if (repair)
                {
                    person.BelongCity.RemovePerson(person);
                    person.BelongCity = null;
                }
            }

            // 3. 军团不能挂：俘虏不属于任何军团
            if (person.BelongCorps != null)
            {
                hasIssue = true;
                if (repair)
                    person.BelongCorps = null;
            }

            // 4. 势力可以为空。
            //    城陷且势力灭亡时是"先 LeaveToWild 再被俘"，下野会把 BelongForce 清成 null，
            //    所以"俘虏没有势力"是**合法状态**，不是脏数据：
            //      · 登用判定按"关押城势力 vs 目标武将军团/势力"走，没有原势力只是少一层参照；
            //      · BeCaptiveList 不登记也无害，释放时按关押城重新落地。
            //    这里不检查、不报告，避免每年刷一条无法修复的告警。

            return hasIssue;
        }

        /// <summary>
        /// 在籍武将：必须有所属城，且势力/军团与所属城完全一致。
        /// 这是历史脏数据最集中的一条不变式。
        /// </summary>
        private static bool CheckInService(Person person, bool repair)
        {
            bool hasIssue = false;

            if (person.BelongCity == null)
            {
                // 在籍却无城：合法解释只有"还没落城"，直接下野（Open311 同样以 service 为空判定离籍）
                Sango.Log.Warning($"*[归属巡检] 在籍武将 {person.Name}({(PersonStateType)person.state}) 没有所属城");
                hasIssue = true;
                if (repair)
                    PersonStateSystem.LeaveToWild(person, person.CurrentCity, PersonChangeReason.Fixed);
                return true;
            }

            City city = person.BelongCity;

            // 【锚点修正】Open311 里 Person::get_force_id() 是 `district_->force_` 派生，
            // 与 service_（所属城）无关：
            //     force_id_t MilitaryObject::get_force_id() {
            //         const District* d = get_district(get_district_id());   // ← 军团
            //         return is_alive(d) ? d->get_force_id() : -1;
            //     }
            // 本项目 BelongForce 是存储字段，必须按同样顺序校正：军团 → 势力；
            // 所属城只在"同势力换军团"时参与。
            //
            // 【为什么不能无条件用城做锚点】在途武将的 BelongCity 会继续指向出发城，
            // 而出发城可能已被敌方攻陷 —— Open311 城陷时用 BuildingDistrictChangeReason_0
            // 跳过成员迁移，在途者的 service_ 会保持指向陷落城（见 城陷事件）。
            // 此时若用城校正，会把还在路上的己方武将瞬间改成敌方势力。
            //
            // 【城已易主】把上面这条原则收敛成一个开关，后面所有"跟随城"的修正都要先看它。
            //   踩过的坑：城陷 → 在途武将返回时 BelongCity 仍指向陷落城 → 次年年末巡检
            //   发现他不在该城 allPersons 里 → 调 City.AddPerson 补登记 →
            //   AddPerson 检测到势力不一致，**强行把武将改成本城所属势力** → 原地叛变。
            //   （日志链路：CheckInService → AddPerson → "势力不一致!!" → 该武将归属变为敌方）
            bool cityChangedHands = city.BelongForce != null
                && person.BelongForce != null
                && city.BelongForce != person.BelongForce;

            bool corpsBroken = person.BelongCorps == null
                || !person.BelongCorps.IsAlive
                || person.BelongCorps.BelongForce == null;

            if (corpsBroken)
            {
                // 军团没了：跟随所属城的军团（城仍属某个势力、且**没有易主**时）
                if (!cityChangedHands && city.BelongCorps != null && city.BelongForce != null)
                {
                    Sango.Log.Warning($"*[归属巡检] {person.Name} 的军团已失效（{Describe(person.BelongCorps)}），跟随所属城 {city.Name} 改挂 {city.BelongCorps.Name}");
                    hasIssue = true;
                    if (repair)
                    {
                        person.BelongCorps = city.BelongCorps;
                        person.BelongForce = city.BelongForce;
                    }
                }
                // 城也没势力：保留现状，由后续"无势力"分支处理
            }
            else
            {
                // 军团有效：势力以军团为准
                if (person.BelongForce != person.BelongCorps.BelongForce)
                {
                    Sango.Log.Warning($"*[归属巡检] {person.Name} 势力:{Describe(person.BelongForce)} 与军团 {person.BelongCorps.Name} 的势力:{Describe(person.BelongCorps.BelongForce)} 不一致");
                    hasIssue = true;
                    if (repair)
                        person.BelongForce = person.BelongCorps.BelongForce;
                }

                // 同势力换军团：跟随所属城。
                // 城已属敌方（在途武将）时不跟进 —— 那正是"户口暂挂在陷落城"的合法中间态，
                // 等他走到目的地由 PersonStateSystem.Enter 统一改户口与军团。
                if (person.BelongCorps != city.BelongCorps
                    && city.BelongCorps != null
                    && city.BelongForce == person.BelongForce)
                {
                    Sango.Log.Warning($"*[归属巡检] {person.Name} 军团:{Describe(person.BelongCorps)} 与所属城 {city.Name} 的军团:{Describe(city.BelongCorps)} 不一致");
                    hasIssue = true;
                    if (repair)
                        person.BelongCorps = city.BelongCorps;
                }
            }

            // 在籍必须在城的在籍名册里
            if (!city.allPersons.Contains(person))
            {
                if (cityChangedHands)
                {
                    // 【不能补登记】城已属敌方，City.AddPerson 会按城势力强行改写武将的
                    // BelongForce / BelongCorps（见 City.AddPerson 的"势力不一致"分支），
                    // 等于让还在返程路上的己方武将原地叛变。
                    // 这里只报告不修：户口由 PersonStateSystem.ReturnHome → Enter 在到达时统一改。
                    Sango.Log.Warning($"*[归属巡检] 在籍武将 {person.Name} 的所属城 {city.Name} 已属 " +
                                      $"{city.BelongForce.Name}，与本人势力不一致；跳过补登记，等返程 / 在野流程接管");
                    hasIssue = true;
                }
                else
                {
                    Sango.Log.Warning($"*[归属巡检] 在籍武将 {person.Name} 未登记在 {city.Name}.allPersons");
                    hasIssue = true;
                    if (repair)
                        city.AddPerson(person);
                }
            }

            // 所在城为空时回落到所属城
            if (person.CurrentCity == null)
            {
                hasIssue = true;
                if (repair)
                    person.ChangeCurrentCity(city);
            }

            return hasIssue;
        }

        /// <summary>
        /// 港关归属校验：只查军团引用是否悬空，不要求与都市同势力。
        /// </summary>
        private static bool CheckSubCities(City city, bool repair)
        {
            if (!city.IsCity())
                return false;

            bool hasIssue = false;

            if (city.portList != null)
            {
                for (int i = 0; i < city.portList.Count; i++)
                {
                    City sub = city.portList[i];
                    if (SubCityCorpsBroken(sub, city, "港口", repair))
                        hasIssue = true;
                }
            }

            if (city.gateList != null)
            {
                for (int i = 0; i < city.gateList.Count; i++)
                {
                    City sub = city.gateList[i];
                    if (SubCityCorpsBroken(sub, city, "关隘", repair))
                        hasIssue = true;
                }
            }

            return hasIssue;
        }

        /// <summary>
        /// 港关归属是否已损坏（军团引用悬空）。
        ///
        /// 【重要】港关是完全独立的据点：Open311 里 Gate/Port 各自持有 district_，
        /// 可以被单独攻占，与所属都市分属不同势力是**合法状态**，
        /// 甚至"无主（district_ = -1）"也是合法的（原版 building_neutralize 的产物）。
        ///
        /// 因此这里只判定一种真正的脏数据：军团引用还在，但那个军团已经解散/势力已灭亡。
        /// 修复方式是清成无主，**不是**挂到所属都市的军团 ——
        /// 后者等于把中立港口凭空送给都市势力。
        /// </summary>
        /// <param name="sub">港关</param>
        /// <param name="city">所属都市（仅用于日志）</param>
        /// <param name="label">日志用标签</param>
        /// <param name="repair">是否修复</param>
        /// <returns>是否存在问题</returns>
        private static bool SubCityCorpsBroken(City sub, City city, string label, bool repair)
        {
            if (sub == null || !sub.IsAlive)
                return false;

            // 无主港关合法，不报错也不修复
            if (sub.BelongCorps == null)
                return false;

            bool broken = !sub.BelongCorps.IsAlive || sub.BelongCorps.BelongForce == null;
            if (!broken)
                return false;

            Sango.Log.Warning($"*[归属巡检]{label}{sub.Name} 的军团已失效（{Describe(sub.BelongCorps)}），置为无主（所属都市 {city.Name} 不接管）");
            if (repair)
                CityStateSystem.SetCorps(sub, null);
            return true;
        }

        /// <summary>
        /// 随便找一个可用城池，用于给"无落脚点"的武将兜底安置。
        /// </summary>
        private static City FindAnyCity()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.citySet == null || scenario.citySet.Count == 0)
                return null;

            // 随机取几次，避免所有"无落脚点"的武将都被塞进第一座城
            for (int attempt = 0; attempt < 8; attempt++)
            {
                City city = scenario.citySet[GameRandom.Range(0, scenario.citySet.Count)];
                if (city != null && city.IsAlive && city.IsCity())
                    return city;
            }

            // 随机失败（城太少）时退回顺序查找
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city != null && city.IsAlive && city.IsCity())
                    return city;
            }

            return null;
        }

        /// <summary>
        /// 日志用的安全描述（对象可能为 null）。
        /// </summary>
        private static string Describe(SangoObject obj)
        {
            return obj != null ? obj.Name : "无";
        }
    }
}
