namespace Sango.Core
{
    /// <summary>
    /// 城池/关隘/港口归属变更原因。
    /// 对应 Open311 的 building_district_change_reason_t。
    /// </summary>
    public enum CityChangeReason
    {
        /// <summary>未指定</summary>
        None = 0,
        /// <summary>攻陷占领</summary>
        Occupied,
        /// <summary>势力投降合并</summary>
        Surrendered,
        /// <summary>中立化（无主）</summary>
        Neutralized,
        /// <summary>军团改编</summary>
        CorpsReformed,
        /// <summary>剧本编辑器修改</summary>
        Edited,
        /// <summary>事件强制变更</summary>
        Event,
        /// <summary>一致性巡检自动修复</summary>
        Fixed,
    }

    /// <summary>
    /// 城池归属治理系统。
    ///
    /// 【设计来源】照搬 Open311 的 System::building_* 处理链：
    ///   building_set_district_id  -> SetCorps
    ///   building_change_district  -> ChangeCorps
    ///   building_neutralize       -> Neutralize
    ///   building_empty            -> Empty
    ///   building_appoint_taishu   -> AppointTaishu
    ///   district_appoint_totoku   -> AppointTotoku
    ///
    /// 【归属链】Force（势力） &lt;- Corps（军团） &lt;- City（据点）
    ///   据点自身只记 BelongCorps；BelongForce 是"军团势力"的冗余副本。
    ///   因此归属变更必须成对处理：改据点 -> 改城内武将/建筑/俘虏 -> 重算账本 -> 重任命。
    ///
    /// 【港关特殊规则】Gate/Port 通过 BelongCity 指向所属都市，但有自己的
    ///   BelongForce/BelongCorps。都市易主时必须同步下属港关，
    ///   否则会出现"都市属 A、港口属 B"的孤立飞地。
    ///
    /// 【使用约定】所有城池归属变更都必须走本类，禁止直接写 BelongCorps / BelongForce
    /// （`City.ChangeCorps` + `City.UpdateCorps` 必须成对，本类内部已封装）。
    /// </summary>
    public static class CityStateSystem
    {
        /// <summary>
        /// 设置据点归属军团（对应 Open311 的 building_set_district_id）。
        ///
        /// 只改据点本体与账本，不动城内武将。跨势力易主请用 ChangeCorps。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="corps">目标军团，null 表示无主</param>
        public static void SetCorps(City city, Corps corps)
        {
            if (city == null || !city.IsAlive)
                return;

            Force oldForce = city.BelongForce;
            Corps oldCorps = city.BelongCorps;

            if (oldCorps == corps && (corps == null ? oldForce == null : oldForce == corps.BelongForce))
                return;

            // 【原版对齐】据点跨势力变更时，取消所有"以该据点为目的地"的在途任务，
            // 处置为就地掉头回出发城（Open311: cancel_all_order(BuildingForceChanged)
            // + cancel_move_order -> exec_return_command）。
            // 同势力换军团不取消 —— 原版 can_cancel_move_order 对
            // BuildingDistrictChanged 返回 false，移动照旧继续。
            Force newForce = corps != null ? corps.BelongForce : null;
            if (oldForce != newForce)
            {
                PersonStateSystem.CancelMissionsToCity(city);

                // 【原版对齐】跨势力易主时转移技巧点。
                // Open311 building_change_district：新势力 force_add_tech_point，
                // 旧势力 building_lost_tech_point（= 据点自身 tech_point + 城内设施数 × 10）。
                // 丢了这座城等于丢了这一份积累，这也是"打大城更值钱"的来源之一。
                if (oldForce != null && newForce != null)
                {
                    int techPoint = city.BuildingType != null ? city.BuildingType.techGain : 0;
                    int facilityCount = city.allBuildings != null ? city.allBuildings.Count : 0;
                    techPoint += facilityCount * 10;

                    if (techPoint > 0)
                    {
                        newForce.GainTechniquePoint(techPoint);
                        oldForce.GainTechniquePoint(-techPoint);
                        Sango.Log.Info($"*[技巧点]{city.Name} 易主：{newForce.Name} +{techPoint}，{oldForce.Name} -{techPoint}");
                    }
                }
            }

            // City.ChangeCorps 内部会读 other.BelongForce，传 null 会空引用，
            // 因此无主（中立化）情形必须直接写字段
            if (corps == null)
            {
                city.BelongCorps = null;
                city.BelongForce = null;
            }
            else
            {
                // City.ChangeCorps 内部会把 BelongForce 对齐到军团势力
                city.ChangeCorps(corps);
            }

            // 城内的内政设施跟随都市改旗（对应 Open311 building_get_force_id
            // 对 government type 的"动态继承 area_city"）。
            //
            // 只同步内政设施：军事设施/陷阱/障碍在原版是独立的 owner_force，不随城走。
            // 也不走 city.UpdateCorps()：那会连在籍武将一起改，
            // 而在城陷路径里武将已经由调用方处理完毕（在途者更不该被改）。
            SyncBuildings(city);

            // 账本是缓存快照，直接全量重算比增量维护更安全（幂等）
            RebuildForceLedger(oldForce);
            RebuildForceLedger(city.BelongForce);

            Sango.Log.Info($"*[归属]{city.Name} {oldCorps?.Name ?? "无军团"} -> {corps?.Name ?? "无军团"}");
        }

        /// <summary>
        /// 同步城内的内政设施归属（只处理内政设施）。
        ///
        /// 【为什么需要】Open311 里内政设施的势力不落盘，`get_force_id()` 每次都去读
        /// 所属都市的势力（building_get_force_id 的 government 分支）。
        /// 本项目把 BelongForce 存成了字段，所以城易主后必须显式推一次，
        /// 否则会出现"城已属 A、城里的农田还属 B"——
        /// 之后玩家在城里操作这些设施时，产出的钱粮会算到错误势力头上。
        ///
        /// 【为什么只处理内政设施】军事设施 / 陷阱 / 障碍在原版有自己的 owner_force，
        /// 城易主不影响它们（它们可能被单独摧毁或中立化）。
        /// </summary>
        /// <param name="city">据点</param>
        public static void SyncBuildings(City city)
        {
            if (city == null || city.allBuildings == null)
                return;

            for (int i = 0; i < city.allBuildings.Count; i++)
            {
                Building building = city.allBuildings[i];
                if (building == null || !building.IsAlive)
                    continue;
                if (building.BuildingType == null || !building.BuildingType.IsIntrior)
                    continue;
                if (building.BelongCorps == city.BelongCorps)
                    continue;

                building.ChangeCorps(city.BelongCorps);
            }
        }

        /// <summary>
        /// 完整变更据点归属（对应 Open311 的 building_change_district）。
        ///
        /// 与 SetCorps 的区别：本方法会同步城内在籍武将、建筑、俘虏、下属港关，
        /// 并重算新旧军团的都督。攻陷/投降/改编一律用本方法。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="corps">目标军团，null 表示中立化</param>
        /// <param name="reason">变更原因</param>
        public static void ChangeCorps(City city, Corps corps, CityChangeReason reason = CityChangeReason.None)
        {
            if (city == null || !city.IsAlive)
                return;

            if (corps == null)
            {
                Neutralize(city, reason);
                return;
            }

            Force oldForce = city.BelongForce;
            Corps oldCorps = city.BelongCorps;
            bool sameForce = oldForce != null && oldForce == corps.BelongForce;

            SetCorps(city, corps);

            // 城内三份名单（在籍武将 / 建筑 / 自家俘虏）跟着换军团。
            // 敌方俘虏保留原属国，UpdateCorps 内部已按势力过滤，不会误改。
            city.UpdateCorps();

            if (sameForce)
            {
                // 同势力换团：只需重选新旧军团的都督
                if (reason != CityChangeReason.Edited)
                {
                    AppointTotoku(corps);
                    AppointTotoku(oldCorps);
                }
            }
            else
            {
                // 跨势力易主：在籍武将"以城为准"同步到新势力。
                // 需要撤退/俘虏的武将由调用方（City.OnFall）先处理完，这里只兜底一致性。
                city.allPersons.ForEach(p =>
                {
                    if (p != null && p.IsAlive && !p.IsPrisoner)
                        PersonStateSystem.SyncFromCity(p);
                });

                AppointTotoku(corps);
                AppointTotoku(oldCorps);
            }

            // 下属港关**不跟随都市改旗**（原版 Gate/Port 各有独立 district_，
            // 可被单独占领）。这里只清掉"军团引用已悬空"的港关，置为无主。
            SyncSubCities(city);

            // 太守重任命
            AppointTaishu(city);

            Sango.Log.Info($"*[{reason}]{city.Name} 归属 {oldForce?.Name ?? "无"} -> {corps.BelongForce?.Name ?? "无"}");
        }

        /// <summary>
        /// 中立化（对应 Open311 的 building_neutralize）。
        ///
        /// 在籍武将迁往最近己方据点，无路可走则下野；俘虏释放；据点变无主空城。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="reason">变更原因</param>
        public static void Neutralize(City city, CityChangeReason reason = CityChangeReason.Neutralized)
        {
            if (city == null || !city.IsAlive)
                return;

            Force oldForce = city.BelongForce;

            // 0. 据点即将变无主，以它为目的地、或从它出发的在途任务都要先处理
            //    （对应 Open311 building_neutralize -> cancel_all_order(BuildingForceChanged)）
            PersonStateSystem.CancelMissionsToCity(city);

            // 1. 在籍武将与俘虏先处理掉，避免清空据点后成为无主孤魂。
            //    SangoObjectList 不支持 IEnumerable，这里手工拷贝一份快照，
            //    因为迁移过程会实时增删原名单。
            if (city.allPersons != null && city.allPersons.Count > 0)
            {
                System.Collections.Generic.List<Person> persons = new System.Collections.Generic.List<Person>();
                for (int i = 0; i < city.allPersons.Count; i++)
                    persons.Add(city.allPersons[i]);

                for (int i = 0; i < persons.Count; i++)
                {
                    Person person = persons[i];
                    if (person == null || !person.IsAlive)
                        continue;

                    City retreat = FindRetreatCity(oldForce, city);
                    if (retreat != null)
                        PersonStateSystem.MoveToCity(person, retreat, PersonChangeReason.Moved);
                    else
                        PersonStateSystem.LeaveToWild(person, city, PersonChangeReason.LeftToWild);
                }
            }

            // 2. 俘虏释放回在野
            if (city.captiveList != null && city.captiveList.Count > 0)
            {
                System.Collections.Generic.List<Person> captives = new System.Collections.Generic.List<Person>();
                for (int i = 0; i < city.captiveList.Count; i++)
                    captives.Add(city.captiveList[i]);

                for (int i = 0; i < captives.Count; i++)
                {
                    if (captives[i] != null)
                        PersonStateSystem.Release(captives[i], city, PersonChangeReason.Released);
                }
            }

            // 3. 据点本体清空
            Empty(city);

            RebuildForceLedger(oldForce);

            Sango.Log.Info($"*[{reason}]{city.Name} 中立化（原属 {oldForce?.Name ?? "无"}）");
        }

        /// <summary>
        /// 清空据点数据（对应 Open311 的 building_empty）。
        ///
        /// 只清归属与资源，不做人员迁移；人员必须先由 Neutralize 处理。
        /// </summary>
        /// <param name="city">据点</param>
        public static void Empty(City city)
        {
            if (city == null || !city.IsAlive)
                return;

            // 太守必须先摘，否则会留下"太守字段指向已离开武将"的脏数据
            if (city.Leader != null)
            {
                if (city.Leader.IsLeader)
                    city.Leader.SetStateNormal();
                city.Leader = null;
            }

            city.BelongCorps = null;
            city.BelongForce = null;

            city.gold = 0;
            city.food = 0;
            city.troops = 0;
            city.morale = 0;

            Sango.Log.Info($"*{city.Name} 已清空");
        }

        /// <summary>
        /// 同步下属港关的归属（对应"都市改旗则港关跟随"的规则）。
        ///
        /// 本项目港关独立存 BelongForce/BelongCorps，与所属都市可能分裂，
        /// 是历史上最常出错的点之一，故在每次归属变更后强制同步。
        /// </summary>
        /// <param name="city">都市</param>
        public static void SyncSubCities(City city)
        {
            if (city == null || !city.IsCity())
                return;

            if (city.portList != null)
            {
                for (int i = 0; i < city.portList.Count; i++)
                    EnsureSubCityCorpsValid(city.portList[i], city);
            }

            if (city.gateList != null)
            {
                for (int i = 0; i < city.gateList.Count; i++)
                    EnsureSubCityCorpsValid(city.gateList[i], city);
            }
        }

        /// <summary>
        /// 港关归属兜底：只在"军团引用已悬空"时清理，**绝不跟随所属都市**。
        ///
        /// 【原版语义】Open311 里 Gate/Port 各自持有 district_，是可以被单独攻占的
        /// 独立据点 —— 港口与所属都市分属不同势力是**合法状态**（三国志11 原版即如此），
        /// kyoten_kanraku 只处理当前那一座据点，从不做都市 → 港关的联动改旗。
        ///
        /// 因此这里只有两种情况：
        ///   · district_ == null  → 无主港关，**合法**，不干预；
        ///   · district_ 悬空（军团已解散/势力已灭亡）→ 清成无主（原版 -1 的等价物）。
        /// 早期版本把无主也当成"失效"并挂到都市军团，等于凭空把中立港口送给都市势力。
        /// </summary>
        /// <param name="sub">港关</param>
        /// <param name="city">所属都市（仅用于日志）</param>
        private static void EnsureSubCityCorpsValid(City sub, City city)
        {
            if (sub == null || !sub.IsAlive)
                return;

            // 无主港关是合法状态（原版 district_ = -1）
            if (sub.BelongCorps == null)
                return;

            bool corpsBroken = !sub.BelongCorps.IsAlive || sub.BelongCorps.BelongForce == null;
            if (!corpsBroken)
                return;

            Sango.Log.Warning($"*[归属]港关{sub.Name} 的军团已失效（{sub.BelongCorps?.Name ?? "无"}），置为无主（所属都市 {city.Name} 不接管）");
            SetCorps(sub, null);
        }

        /// <summary>
        /// 委任太守（对应 Open311 的 building_appoint_taishu）。
        ///
        /// 复用 City.UpdateNewLeader：内部按 君主 -> 都督 -> 官职/统率 的优先级挑选，
        /// 并把旧太守降为一般武将（保证同一城不会出现两个太守状态）。
        /// </summary>
        /// <param name="city">据点</param>
        public static void AppointTaishu(City city)
        {
            if (city == null || !city.IsAlive)
                return;

            city.NeedUpdateLeader();
            city.UpdateNewLeader();
        }

        /// <summary>
        /// 委任都督（对应 Open311 的 district_appoint_totoku）。
        ///
        /// 只处理"军团无军团长/军团长已失效"的情形，不主动换人，
        /// 避免破坏玩家手动任命的配置。
        /// </summary>
        /// <param name="corps">军团</param>
        public static void AppointTotoku(Corps corps)
        {
            if (corps == null || !corps.IsAlive || corps.BelongForce == null)
                return;

            // 【原版对齐】district_appoint_totoku 的 A 分支：
            // 势力一个存活武将都没有 → 直接 force_ruin（不是"等最后一座城陷落"）。
            // 否则会出现空壳势力：有城、有军团，却没有任何人当都督/太守，
            // AI 每次任命都空转，玩家也拿不下这些城（因为城没丢）。
            //
            // 传 FirstCity 作为演出上下文：Force.Ruin 会广播 OnForceFall，
            // 订阅方假定有一座"陷落的城"，传 null 会空引用。
            if (!corps.BelongForce.HasAnyAlivePerson())
            {
                City fallCity = corps.BelongForce.FirstCity();
                Sango.Log.Warning($"*[军团]{corps.BelongForce.Name} 已无任何存活武将，按原版立即灭亡");
                corps.BelongForce.Ruin(null, fallCity, null);
                return;
            }

            // 【原版对齐】军团已经没有任何城 → 先解散再谈都督任命。
            // 对应 Open311 district_appoint_totoku 的 D 分支（非 1 号军团空 → district_dissolve）。
            // 用实时遍历而不是 cityCount 缓存：缓存在刚被批量搬城的瞬间可能还没刷新。
            if (corps.number > 1)
            {
                int cityCount = 0;
                corps.ForEachCity(c => cityCount++);
                if (cityCount == 0)
                {
                    Sango.Log.Info($"*[军团]{corps.Name} 已无城池，解散");
                    corps.BelongForce?.DeleteCorps(corps);
                    return;
                }
            }

            // 复用军团自身的任命实现（对应 Open311 的 district_appoint_totoku）：
            // 第 1 军团（君主本军团）不设都督，UpdateCommander 内部会直接返回；
            // 其余军团按"官职等级 -> 统率"择优，并把旧都督降为一般武将。
            corps.NeedUpdateCommander();
            corps.UpdateCommander();
        }

        /// <summary>
        /// 重算势力账本（城市数/城塞数/城市列表）。
        /// 全量重算而非增量加减，避免重复计数导致的数值漂移。
        /// </summary>
        /// <param name="force">势力，null 时忽略</param>
        private static void RebuildForceLedger(Force force)
        {
            if (force == null)
                return;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            force.UpdateCityCount(scenario);
        }

        /// <summary>
        /// 找一个可以撤退的己方据点：优先同势力其它都市，没有则返回 null。
        /// </summary>
        /// <param name="force">原势力</param>
        /// <param name="excluding">排除的据点</param>
        /// <returns>可撤退据点</returns>
        private static City FindRetreatCity(Force force, City excluding)
        {
            if (force == null)
                return null;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return null;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City c = scenario.citySet[i];
                if (c == null || !c.IsAlive || c == excluding || !c.IsCity())
                    continue;
                if (c.BelongForce == force)
                    return c;
            }

            return null;
        }
    }
}
