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

            // 【表现层刷新】据点换旗属于"数据层已经是新势力、表现层还停在旧颜色"的典型场景：
            // 小地图城市节点与战略地图贴图都只在节点/贴图重建时读一次归属。
            // 城陷路径自带 OnCityFall，但劝降合并（Force.SurrenderAllBuildings）、
            // 事件改属、编辑器改属这些非战斗易主不会派 OnCityFall，
            // 因此在这里统一补一个纯表现事件（区分见 GameEvent.OnCityChangeForce 的注释）。
            if (oldForce != city.BelongForce)
                GameEvent.OnCityChangeForce?.Invoke(city, oldForce, city.BelongForce);
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

            // 2. 俘虏释放：走 System::person_release —— 原势力存活则送回原势力，
            //    原势力已灭亡才就地落野（不能一律当在野处理）
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
        /// 城陷事件 只处理当前那一座据点，从不做都市 → 港关的联动改旗。
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
        /// 太守带城独立：让指定武将带着当前所在都市脱离原势力，建立新势力。
        ///
        /// 对齐出处：Open311 `s11_sys_event_independence.cpp:89-` 的
        /// <c>create_force_independence(person, local, koukei)</c>。
        /// 「驱虎吞狼」成功后的效果就是它（`s11_sys_person_order_strategy.cpp:769`
        /// 调用 <c>create_force_independence(target, false, false)</c>）——
        /// 注意原版是"太守独立建国"，**不是**"带城投奔发起方"。
        ///
        /// 【与 <see cref="Neutralize"/> 的区别】Neutralize 会把在籍武将迁往最近己方据点，
        /// 本方法**不迁人**：整城连同城内武将一起改属新势力。
        /// </summary>
        /// <param name="person">要独立的武将（须在本城任职，不能是君主）</param>
        /// <param name="reason">变更原因</param>
        /// <returns>新建的势力；失败返回 null</returns>
        public static Force CreateIndependence(Person person, CityChangeReason reason = CityChangeReason.Event)
        {
            if (person == null || !person.IsAlive)
                return null;

            City city = person.BelongCity ?? person.CurrentCity;
            if (city == null || !city.IsCity())
                return null;

            // 君主不能"独立" —— :97
            if (person.IsGovernor)
                return null;

            Force oldForce = city.BelongForce;

            // 原势力君主不能与本人在同一座城 —— :121-125
            if (oldForce != null)
            {
                Person kunshu = oldForce.mGovernor;
                if (kunshu != null && kunshu.IsAlive && kunshu.CurrentCity == city)
                    return null;
            }

            // 必须是太守 / 都督；普通武将则顶替原太守 —— :99-107
            int state = person.state;
            bool isLeaderOrHigher = state == (int)PersonStateType.Leader
                || state == (int)PersonStateType.Commander
                || state == (int)PersonStateType.Governor;
            if (!isLeaderOrHigher)
            {
                Person taishu = city.Leader;
                if (taishu == null || taishu == person || !taishu.IsAlive)
                    return null;
                if (taishu.state == (int)PersonStateType.Leader)
                    PersonStateSystem.SetState(taishu, PersonStateType.Normal, PersonChangeReason.None);
                PersonStateSystem.SetState(person, PersonStateType.Leader, PersonChangeReason.None);
                AppointTaishu(city);
            }

            // 逐个判定城内武将"是否跟随独立" —— 对齐 independence_join_chance（:268-309）。
            // 注意：原版**不是**全城跟随，而是逐人掷骰；不跟随者迁往最近的己方据点。
            Scenario scenario = Scenario.Cur;
            Person oldKunshu = oldForce != null ? oldForce.mGovernor : null;

            System.Collections.Generic.List<Person> joined = new System.Collections.Generic.List<Person>();
            System.Collections.Generic.List<Person> notJoined = new System.Collections.Generic.List<Person>();
            if (city.allPersons != null)
            {
                for (int i = 0; i < city.allPersons.Count; i++)
                {
                    Person p = city.allPersons[i];
                    if (p == null || !p.IsAlive || p == person)
                        continue;
                    if (IsIndependenceFollower(p, person, oldKunshu, scenario))
                        joined.Add(p);
                    else
                        notJoined.Add(p);
                }
            }

            // 原势力军师若正是独立者本人 → 解任 —— :181-182
            bool wasOldCounsellor = oldForce != null && oldForce.mCounsellor == person;
            if (wasOldCounsellor)
                oldForce.mCounsellor = null;

            // 【建势力前置】CreateForce 要求"君主无势力 + 都城无势力"。
            // 原版 create_force 内部会把这两者从旧势力摘出来（s11_sys_event_independence.cpp:173），
            // 本项目由调用方负责，所以这里先快照再临时清空；失败时必须原样还原。
            //
            // 【为什么必须清 person】独立者本人的 BelongForce 仍指向旧势力，
            // 不清就会撞上 CreateForce 的"君主武将已有势力"校验 —— 之前正是这样，
            // 加上 ScenarioEdit.Scenario 恒为 null，独立 100% 失败，且城已经被去势力化。
            int cityOldForceId = city.BelongForceId;
            Force cityOldForce = city.BelongForce;
            int cityOldCorpsId = city.BelongCorpsId;
            Corps cityOldCorps = city.BelongCorps;
            int personOldForceId = person.BelongForceId;
            int personOldCorpsId = person.BelongCorpsId;
            Corps personOldCorps = person.BelongCorps;

            city.BelongForceId = 0;
            city.BelongForce = null;
            city.BelongCorpsId = 0;
            city.BelongCorps = null;
            SyncBuildings(city);

            person.BelongForceId = 0;
            person.BelongCorpsId = 0;
            person.BelongCorps = null;

            // 运行期必须显式传当前剧本：编辑器系统（ScenarioEdit）在正常游戏里从未加载剧本，
            // 它自己的 Scenario 属性恒为 null（日志："请先新建或加载剧本"）。
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Force newForce = edit != null ? edit.CreateForce(person, city, scenario) : null;
            if (newForce == null)
            {
                // 回滚：否则城变无主、独立者丢归属，比"独立没发生"更糟
                city.BelongForceId = cityOldForceId;
                city.BelongForce = cityOldForce;
                city.BelongCorpsId = cityOldCorpsId;
                city.BelongCorps = cityOldCorps;
                SyncBuildings(city);

                person.BelongForceId = personOldForceId;
                person.BelongCorpsId = personOldCorpsId;
                person.BelongCorps = personOldCorps;

                if (wasOldCounsellor)
                    oldForce.mCounsellor = person;

                Sango.Log.Error($"*[独立]{person.Name} 在 {city.Name} 独立失败：新势力创建返回空");
                RebuildForceLedger(oldForce);
                return null;
            }

            // 跟随者归入新势力
            Corps mainCorps = newForce.CapitalCorps;
            for (int i = 0; i < joined.Count; i++)
            {
                Person p = joined[i];
                p.BelongForceId = newForce.Id;
                p.BelongCorpsId = mainCorps != null ? mainCorps.Id : 0;
                p.BelongCorps = mainCorps;
                // 换势力时复位褒赏标记 —— 对齐原版 person_change_force（s11_sys_scen_person.cpp:751）
                p.SetBonusDone(false);
                if (p.state != (int)PersonStateType.Leader
                    && p.state != (int)PersonStateType.Commander
                    && p.state != (int)PersonStateType.Governor)
                    PersonStateSystem.SetState(p, PersonStateType.Normal, PersonChangeReason.ForceChanged);
            }

            // 不跟随者迁往最近的己方据点；一处都没有则强制跟随 —— :160-172
            // 放在建势力成功之后：失败时这些人还没被迁走，不需要回滚。
            City retreatCity = oldForce != null ? FindRetreatCity(oldForce, city) : null;
            if (retreatCity != null)
            {
                for (int i = 0; i < notJoined.Count; i++)
                {
                    Person p = notJoined[i];
                    PersonStateSystem.MoveToCity(p, retreatCity, PersonChangeReason.ForceChanged);
                }
            }
            else
            {
                // 一处己方据点都没有 → 强制跟随（原版 :169-170），直接挂到新势力
                for (int i = 0; i < notJoined.Count; i++)
                {
                    Person p = notJoined[i];
                    if (p == null)
                        continue;
                    p.BelongForceId = newForce.Id;
                    p.BelongCorpsId = mainCorps != null ? mainCorps.Id : 0;
                    p.BelongCorps = mainCorps;
                    p.SetBonusDone(false);
                    if (p.state != (int)PersonStateType.Leader
                        && p.state != (int)PersonStateType.Commander
                        && p.state != (int)PersonStateType.Governor)
                        PersonStateSystem.SetState(p, PersonStateType.Normal, PersonChangeReason.ForceChanged);
                    joined.Add(p);
                }
            }

            // 【运行期初始化】编辑器新建势力不跑游戏，CreateForce 只搭骨架；
            // 运行期必须补 Prepare / Init，否则新势力的 actionList、主军团名单、科技树全是空的，
            // 回合流程一走到它就会空引用（或表现为"新势力什么都不做"）。
            newForce.OnScenarioPrepare(scenario);
            if (mainCorps != null)
                mainCorps.OnScenarioPrepare(scenario);
            newForce.Init(scenario);
            if (mainCorps != null)
                mainCorps.Init(scenario);

            // 【科技树入口继承 · 原版无对应实现】
            //
            // 原版科技树是**全局**的（Technique 表自带前置关系，Force 只有 tech 位图），
            // 所以 s11_sys_event_independence.cpp:251-255 只搬"已研究"位；本项目的
            // Force.InitTechniques 是自建的"势力科技树入口"（见该字段注释），
            // InitTechniquesTree 只按它建树（Force.Init 与 GainTechnique 都会重建）。
            // 不搬的话新势力会出现"已研究了一堆、树是空的"——纯界面/数据一致性问题。
            // 因此这里沿用旧势力的科技树，让已研究进度在新势力界面上可见。
            if (oldForce != null && oldForce.InitTechniques != null)
            {
                if (newForce.InitTechniques == null)
                    newForce.InitTechniques = new SangoObjectList<Technique>();
                newForce.InitTechniques.Clear();
                for (int i = 0; i < oldForce.InitTechniques.Count; i++)
                {
                    Technique init = oldForce.InitTechniques[i];
                    if (init != null)
                        newForce.InitTechniques.Add(init);
                }
                // 同步存档字段，避免中途存档把空数组写回去
                newForce.InitTechniques_list = newForce.InitTechniques.ToArray();
            }

            // 【原版对齐】新势力继承旧势力的全部已研究科技 —— s11_sys_event_independence.cpp:247-256：
            //     if (utils::is_alive(old_force))
            //         for (int i = 0; i < TechId_ExMax; i++)
            //             if (old_force->has_tech(i))
            //                 force_set_tech(new_force, i, true);
            //
            // 必须放在 Init 之后：GainTechnique 会把技巧的被动效果注册进 actionList，
            // 漏了就会出现"科技树里点亮了、实际效果全没有"（见 Force.GainTechnique 的注释）。
            // 也必须放在 InitTechniques 复制之后：GainTechnique 内部会按新的 InitTechniques 重建科技树。
            if (oldForce != null && oldForce.IsAlive && oldForce.Techniques != null)
            {
                int inheritCount = 0;
                for (int i = 0; i < oldForce.Techniques.Count; i++)
                {
                    Technique technique = oldForce.Techniques[i];
                    if (technique != null && newForce.GainTechnique(technique))
                        inheritCount++;
                }

                if (inheritCount > 0)
                {
                    Sango.Log.Info($"*[独立]{newForce.Name} 继承 {oldForce.Name} 的 {inheritCount} 项已研究科技");
                }
            }

            // 【原版对齐 · 关系重置】s11_sys_event_independence.cpp:249-250：
            //     force_set_like(old_force_id, new_force_id, 0);      // 友好度归 0（敌对）
            //     force_set_ally(old_force_id, new_force_id, false);  // 解除同盟
            //
            // 不设的话，新势力 Id 超出 RelationMap 时 GetRelation 返回 50（中立），
            // 表现为"刚独立就和老东家相安无事"，AI 也不会主动进攻。
            if (oldForce != null)
            {
                newForce.SetLike(oldForce, 0);

                // 解除两者间的同盟。新建势力通常没有盟约，这里做防御性清理（两侧都查，
                // 因为 CheckAlliance 只看自己列表，而盟约理论上可能只登记在旧势力侧）；
                // 清理顺序与 Alliance 自然到期一致（失效 → 摘出双方列表 → 移出场景）。
                Alliance alliance = newForce.CheckAlliance(oldForce, AllianceType.Alliance);
                if (alliance == null)
                    alliance = oldForce.CheckAlliance(newForce, AllianceType.Alliance);
                if (alliance != null)
                {
                    alliance.IsAlive = false;
                    if (alliance.ForceList != null)
                    {
                        for (int i = 0; i < alliance.ForceList.Count; i++)
                        {
                            Force f = alliance.ForceList.Get(i);
                            if (f != null && f.AllianceList != null)
                                f.AllianceList.Remove(alliance);
                        }
                    }
                    scenario.Remove(alliance);
                }
            }

            RebuildForceLedger(oldForce);
            RebuildForceLedger(newForce);

            // 原势力已无城 → 灭亡（原版 force_ruin 的等价处理）
            if (oldForce != null && oldForce.IsAlive && (oldForce.CityList == null || oldForce.CityList.Count == 0))
            {
                oldForce.Ruin(null, city, person);
                Sango.Log.Info($"*[独立]{oldForce.Name} 因最后一座城独立而灭亡");
            }

            Sango.Log.Info($"*[独立]{person.Name} 在 {city.Name} 建立 {newForce.Name}（原因 {reason}），" +
                $"原属 {oldForce?.Name ?? "在野"}，跟随 {joined.Count} 人、离去 {notJoined.Count} 人");
            return newForce;
        }

        /// <summary>
        /// 独立时某武将是否跟随。
        ///
        /// 对齐出处：s11_sys_event_independence.cpp:268-309 independence_join_chance。
        /// 判定链：
        ///   ① 前置：存活、君主本人有效、本人不在部队中、没有出境类任务
        ///   ② 厌恶君主且不厌恶主谋，或忠诚 &lt;80 → 掷"起事概率"
        ///   ③ 与主谋同据点 → 掷"随从概率"
        /// </summary>
        /// <param name="self">待判定武将</param>
        /// <param name="leader">发起独立的主谋</param>
        /// <param name="kunshu">原势力君主</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>跟随返回 true</returns>
        static bool IsIndependenceFollower(Person self, Person leader, Person kunshu, Scenario scenario)
        {
            if (self == null || !self.IsAlive || leader == null || !leader.IsAlive)
                return false;
            if (kunshu == null || !kunshu.IsGovernor)
                return false;
            // 在部队中不跟随 —— :272
            if (self.mBelongTroop != null)
                return false;
            // 有任务在身（出阵 / 运输 / 计略等）→ 不跟随。原版只放行留城类指令，
            // 这里保守取"必须处于待命"，差异仅影响正在城里干活的少数人。
            if (self.missionType != (int)MissionType.None)
                return false;

            // ② 厌恶君主且不厌恶主谋，或忠诚 <80 —— :294-302
            bool hateKunshu = self.IsHate(kunshu);
            bool hateLeader = self.IsHate(leader);
            if ((hateKunshu && !hateLeader) || self.loyalty < 80)
            {
                if (IndependenceChance(self, scenario))
                    return true;
            }

            // ③ 与主谋同据点 —— :303-307
            City selfCity = self.BelongCity ?? self.CurrentCity;
            City leaderCity = leader.BelongCity ?? leader.CurrentCity;
            if (selfCity != null && selfCity == leaderCity)
            {
                if (JoinChance(self, leader, kunshu))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// "起事概率"。对齐 s11_sys_event_independence.cpp:58-77 independence_chance。
        /// <code>
        /// n = (75 - loyalty) 钳到 0
        /// n *= (ambition + 4) / 4
        /// n -= giri * 10
        /// 难度：Normal ×1.5、Hard ×1.8
        /// rand_bool(n)
        /// </code>
        /// </summary>
        /// <param name="self">武将</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>命中返回 true</returns>
        static bool IndependenceChance(Person self, Scenario scenario)
        {
            if (self == null)
                return false;

            int n = 75 - self.loyalty;
            if (n < 0)
                n = 0;
            n *= (self.ambition + 4) / 4;
            n -= GetGiri(self) * 10;
            if (n < 0)
                n = 0;

            int difficulty = scenario != null ? scenario.Variables.difficulty : 0;
            if (difficulty == 1)
                n = n * 3 / 2;
            else if (difficulty == 2)
                n = n * 18 / 10;

            return GameRandom.Chance(n);
        }

        /// <summary>
        /// "随从概率"。逐段对齐 s11_sys_scen_person.cpp:599-641 的 join_chance。
        /// <code>
        /// ① 与自己的君主是夫妻/结义 → 绝不背叛（false）
        /// ② 与主谋是夫妻/结义，或厌恶君主 → 必跟随（true）
        /// ③ 不厌恶主谋的君主时：
        ///     a. 自己势力里（除主谋/自己）有自己的夫妻/结义 → 不跟随
        ///     b. 主谋势力里有自己的夫妻/结义 → 跟随
        ///     c. 不亲爱君主，且（亲爱主谋的君主 或 亲爱主谋 或 义理≤2 且 忠诚<100）→ 跟随
        /// ④ 高智/高武者按 n*(giri+1)/5 的概率"看清形势不跟随"
        /// ⑤ 其余情况跟随
        /// </code>
        /// </summary>
        /// <param name="self">武将</param>
        /// <param name="leader">主谋</param>
        /// <param name="kunshu">原势力君主</param>
        /// <returns>跟随返回 true</returns>
        static bool JoinChance(Person self, Person leader, Person kunshu)
        {
            if (self == null || !self.IsAlive || leader == null || !leader.IsAlive)
                return false;

            Person selfKunshu = self.BelongForce != null ? self.BelongForce.mGovernor : kunshu;
            Person leaderKunshu = leader.BelongForce != null ? leader.BelongForce.mGovernor : null;
            if (selfKunshu == null || leaderKunshu == null)
                return false;

            if (self == leader)
                return true;

            // ① 与自己的君主是夫妻/结义 → 绝不背叛 —— :612-613
            if (IsSpouseOrSworn(self, selfKunshu))
                return false;

            // ② 与主谋是夫妻/结义，或厌恶君主 → 必跟随 —— :614-615
            if (IsSpouseOrSworn(self, leader) || self.IsHate(selfKunshu))
                return true;

            // ③ 不厌恶主谋的君主 —— :616-636
            if (!self.IsHate(leaderKunshu))
            {
                // 3a 自己势力里还有牵挂 → 不跟随 —— :618-623
                if (HasSpouseOrSwornInForce(self, self.BelongForce, leader))
                    return false;
                // 3b 主谋势力里有牵挂 → 跟随 —— :624-627
                if (HasSpouseOrSwornInForce(self, leader.BelongForce, null))
                    return true;
                // 3c 不亲爱君主 —— :628-635
                if (!self.IsLike(selfKunshu))
                {
                    if (self.IsLike(leaderKunshu) || self.IsLike(leader))
                        return true;
                    int giri = GetGiri(self);
                    if (giri != 3 && giri != 4 && self.loyalty < 100)
                        return true;
                }
            }

            // ④ 高智/高武者有概率不跟随 —— :637-639
            int n = System.Math.Max(System.Math.Max(self.Intelligence, self.Strength), 50);
            if (GameRandom.Chance(n * (GetGiri(self) + 1) / 5))
                return false;

            return true;
        }

        /// <summary>
        /// 两人是否为夫妻或结义关系。
        /// 对齐原版 `is_fuufu_or_gikyoudai`：夫妻看 <see cref="Person.SpouseList"/>，
        /// 结义看 <see cref="Person.Brother"/>（组头）与 <see cref="Person.BrotherList"/>（同组）。
        /// </summary>
        /// <param name="a">武将 A</param>
        /// <param name="b">武将 B</param>
        /// <returns>是夫妻或结义返回 true</returns>
        public static bool IsSpouseOrSworn(Person a, Person b)
        {
            if (a == null || b == null || a == b)
                return false;
            if (a.IsSpouse(b) || b.IsSpouse(a))
                return true;
            if (a.Brother == b || b.Brother == a)
                return true;
            if (a.BrotherList != null && a.BrotherList.Contains(b))
                return true;
            if (b.BrotherList != null && b.BrotherList.Contains(a))
                return true;
            return false;
        }

        /// <summary>
        /// 指定势力里是否存在"自己的夫妻/结义"。
        /// 对齐原版 `has_fuufu_or_gikyoudai(self, person_list)`。
        /// </summary>
        /// <param name="self">武将</param>
        /// <param name="force">要检索的势力</param>
        /// <param name="exclude">排除的对象（通常是主谋本人）</param>
        /// <returns>存在返回 true</returns>
        static bool HasSpouseOrSwornInForce(Person self, Force force, Person exclude)
        {
            if (self == null || force == null)
                return false;

            if (self.SpouseList != null)
            {
                for (int i = 0; i < self.SpouseList.Count; i++)
                {
                    Person p = self.SpouseList[i];
                    if (p == null || p == exclude || !p.IsAlive)
                        continue;
                    if (p.BelongForce == force)
                        return true;
                }
            }

            if (self.Brother != null && self.Brother != exclude && self.Brother.IsAlive
                && self.Brother.BelongForce == force)
                return true;

            if (self.BrotherList != null)
            {
                for (int i = 0; i < self.BrotherList.Count; i++)
                {
                    Person p = self.BrotherList[i];
                    if (p == null || p == self || p == exclude || !p.IsAlive)
                        continue;
                    if (p.BelongForce == force)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 取义理档位（0~4）。本项目 `Argumentation.kind` 是 1~5，减 1 对齐原版 Giri_*（0~4）。
        /// </summary>
        /// <param name="person">武将</param>
        /// <returns>0~4</returns>
        public static int GetGiri(Person person)
        {
            if (person == null || person.mArgumentation == null)
                return 2;
            int giri = person.mArgumentation.kind - 1;
            if (giri < 0) giri = 0;
            if (giri > 4) giri = 4;
            return giri;
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
