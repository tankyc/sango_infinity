namespace Sango.Core
{
    /// <summary>
    /// 武将归属变更原因。
    /// 对应 Open311 的 person_district_change_reason_t，仅用于日志定位与差异化处理，
    /// 不参与存档。
    /// </summary>
    public enum PersonChangeReason
    {
        /// <summary>未指定</summary>
        None = 0,
        /// <summary>入城（从城外/部队回到据点）</summary>
        Entered,
        /// <summary>登用（被招揽加入势力）</summary>
        Employed,
        /// <summary>人事移动（转任到其它据点）</summary>
        Moved,
        /// <summary>被俘虏</summary>
        Captured,
        /// <summary>释放（俘虏被放回在野）</summary>
        Released,
        /// <summary>势力变更</summary>
        ForceChanged,
        /// <summary>军团变更</summary>
        DistrictChanged,
        /// <summary>登场（未发现/未登场转为在野）</summary>
        Appeared,
        /// <summary>下野</summary>
        LeftToWild,
        /// <summary>死亡</summary>
        Died,
        /// <summary>一致性巡检自动修复</summary>
        Fixed,
    }

    /// <summary>
    /// 武将状态与归属治理系统。
    ///
    /// 【设计来源】照搬 Open311 的 System::person_* 处理链：
    ///   person_set_mibun       -> SetState

    ///   person_set_district_id -> SetCorps
    ///   person_enter           -> Enter
    ///   person_change_district -> MoveToCity
    ///   person_set_zaiya       -> LeaveToWild
    ///
    /// 【字段对应】Open311 的三字段位置模型 vs 本项目的字段：
    ///   mibun      &lt;-&gt; state        身份（同时承载官职与生存状态，见 PersonStateType）
    ///   service_   &lt;-&gt; BelongCity   所属据点（组织归属，俸禄/兵役/任免都按它算）
    ///   location_  &lt;-&gt; CurrentCity  当前所在地点（可与 BelongCity 不同，在部队时为 null）
    ///   district_  &lt;-&gt; BelongCorps  所属军团
    ///   （派生）    &lt;-&gt; BelongForce  势力，理论上应恒等于 BelongCorps.BelongForce
    ///
    /// 【不变式】所有业务代码都必须满足，GovernanceValidator 会周期性检查并修复：
    ///   1. 在野（Unemployed）：有 CurrentCity，无 Force / Corps / Troop
    ///   2. 未发现（Invisible）：有 CurrentCity，无 Force / Corps / Troop，且登记在 invisiblePersons
    ///   3. 俘虏（Prisoner）：有 CurrentCity，保留原 Force / Corps（用于登用与释放判定），无 Troop
    ///   4. 在籍（Governor/Commander/Leader/Normal）：有 BelongCity，
    ///      且 BelongForce == BelongCity.BelongForce、BelongCorps == BelongCity.BelongCorps
    ///   5. 死亡（Dead）：无 City / Force / Corps / Troop
    ///   6. Leader 状态只能由 City.UpdateNewLeader 授予，且全势力内同一城只有一人
    ///
    /// 【使用约定】
    ///  - 改变"人在哪、属于谁、什么身份"一律走本类，不要直接给
    ///    state / BelongCity / CurrentCity / BelongCorps / BelongForce / mBelongTroop 赋值。
    ///  - 本类内部允许直写字段（它就是唯一入口），并对每次变更打印中文日志便于追查。
    ///  - 存量代码尚未全部改道，靠 GovernanceValidator 兜底；新增代码必须遵守本约定。
    /// </summary>
    public static class PersonStateSystem
    {
        /// <summary>
        /// 离开势力后被旧主"禁止仕官"的月数（对应 Open311 person_change_district 里的 6）。
        /// 本项目一个月 = 一个回合。
        /// </summary>
        public const int BanMonthsAfterLeavingForce = 6;

        /// <summary>
        /// 设置武将身份（对应 Open311 的 person_set_mibun）。
        ///
        /// 只负责"改身份 + 名册归位"，不负责改所属城/军团；
        /// 需要同时迁城时请用 MoveToCity / Enter。
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="state">新身份</param>
        /// <param name="reason">变更原因，仅用于日志</param>
        public static void SetState(Person person, PersonStateType state, PersonChangeReason reason = PersonChangeReason.None)
        {
            if (person == null)
                return;

            int oldState = person.state;
            int newState = (int)state;
            if (oldState == newState)
                return;

            // 【君主保护】在职君主不能被静默降级为一般 / 太守 / 都督。
            //
            // 【原版依据】Open311 里君主的 mibun_ 只有两种合法变更：
            //   · 继任：force_succeed 先改 force.governor_，再改旧君主的 mibun_
            //     （此时 mGovernor 已不是他，本判定自然放行）；
            //   · 灭亡 / 下野：势力 is_alive 已为 false，本判定同样放行。
            // 除此之外任何"人还在，势力还活着，却把 mibun_ 从 Kunshu 改成一般"的路径，
            // 都是把派生数据（BelongCity/Corps）反向污染身份的结果。
            //
            // 【要解决的现网问题】君主出使别城登庸后，户口（BelongCity）在途中的某条
            // 路径被改到目标城，再经 Recruit 降级为 Normal；此后 loyalty 仍是 100
            // （ResetLoyalty 对君主直接 return），于是出现"身份是一般、忠诚度是君主"的分裂。
            // 这里直接把非法降级挡在治理层入口，并打印调用栈方便定位真正的污染源。
            if (oldState == (int)PersonStateType.Governor
                && state != PersonStateType.Governor
                && state != PersonStateType.Dead
                && state != PersonStateType.Unemployed
                // ForceChanged = 继任 / 劝降投降这类**显式**的君主身份变更，
                // 原版也是先改 force.governor_id_ / 先 set_mibun 再换势力，必须放行。
                && reason != PersonChangeReason.ForceChanged
                && person.BelongForce != null
                && person.BelongForce.IsAlive
                && person.BelongForce.mGovernor == person)
            {
                Sango.Log.Error($"*[身份]{person.Name} 拒绝把在职君主降级为 {state}（原因:{reason}）"
                    + $" 势力:{person.BelongForce.Name} 户口:{person.BelongCity?.Name ?? "无"}"
                    + $" 所在:{person.CurrentCity?.Name ?? "无"}"
                    + $" 调用栈:{new System.Diagnostics.StackTrace(true)}");
                return;
            }

            // 【不变式】在野 / 未发现 的武将不得保留军团归属。
            //
            // `Person.BelongForce` 是派生属性（读 BelongCorps?.BelongForce，Person.cs:48），
            // 军团不清就会留下"在野却仍隶属某势力"的脏数据：
            //   · 返程入城时 City.AddPerson 校验 BelongForce 与本城不一致，刷出
            //     "势力不一致!!" 错误（本次线上日志就是这条）；
            //   · 登用 / 人才搜索 / 势力统计会把在野武将算进原势力。
            //
            // 存量代码里仍有直写 state 的路径（绕过本类，如剧本初始化与旧档恢复），
            // 所以在治理层入口统一兜底：只清军团，不动 BelongCity（在野仍需要所在城）。
            if ((state == PersonStateType.Unemployed || state == PersonStateType.Invisible)
                && person.BelongCorps != null)
            {
                Sango.Log.Warning($"*[身份]{person.Name} 转为{state}但残留军团 {person.BelongCorps.Name}，已清空归属");
                person.BelongCorps = null;
            }

            person.state = newState;

            // 【与原版对齐】Open311 的 is_alive 是读身份现算的；
            // 本项目的 IsAlive 是独立存储标记，死亡时必须同步，
            // 否则会出现"state 已死亡、IsAlive 仍为 true"，用 IsAlive 过滤的地方会把死人当活人。
            if (state == PersonStateType.Dead)
                person.IsAlive = false;

            Sango.Log.Info($"*[身份]{person.Name} {(PersonStateType)oldState} -> {state} 原因:{reason}");

            // 身份切换后名册要跟着换：城市侧的 6 份名册是按 state 分类登记的，
            // 只改 state 不改名册，就会出现"名单里有人但状态对不上"的分裂
            RebuildRoster(person, (PersonStateType)oldState, state);
        }

        /// <summary>
        /// 入城归位（对应 Open311 的 person_enter）。
        ///
        /// 负责人：迁户口 -> 改所在地 -> 状态归位 -> 标记太守重算。
        /// 俘虏入城不走归顺逻辑，只改所在地并登记进俘虏名单。
        /// </summary>
        /// <param name="person">入城武将</param>
        /// <param name="city">目标据点</param>
        /// <param name="reason">变更原因</param>
        public static void Enter(Person person, City city, PersonChangeReason reason = PersonChangeReason.Entered)
        {
            if (person == null || city == null || person.IsDead)
                return;

            // 俘虏入城：只改所在地，不归顺。对应 Open311 person_enter 对 horyo 的特判
            if (person.IsPrisoner)
            {
                if (person.CurrentCity != city)
                    person.ChangeCurrentCity(city);
                city.AddCaptive(person);
                return;
            }

            City oldCity = person.BelongCity;
            Corps oldCorps = person.BelongCorps;

            // 目标是无主空城：入城即下野（对应 Open311 中 service 无 district 的情形）
            if (city.BelongForce == null)
            {
                LeaveToWild(person, city, PersonChangeReason.LeftToWild);
                return;
            }

            // 【外势力在职武将不得入城】原版 person_enter 只处理"自己势力的据点"
            // （service_->get_force_id() == 入城据点所属势力），敌人进城要么是俘虏
            // （上面已单独处理），要么是换势力的显式事务（person_change_force → 本类 Recruit）。
            //
            // 【要解决的问题】返程任务的 missionTarget 一旦被写成敌城，
            // 到达时 City.OnPersonReturnCity → 本方法会把户口直接迁进敌城；
            // 下一回合读档走 Person.OnScenarioPrepare 的 Governor 分支时，
            // BelongForce 会按 BelongCity 重算成敌势力，于是 mibun_ 被降成一般武将 ——
            // 表现就是"君主回来卡在别的城市、身份变成一般，但忠诚度还是君主的"。
            // 这里拒绝迁户口，并让他就地改挂返程回本势力据点。
            if (person.BelongForce != null
                && person.BelongForce != city.BelongForce
                && IsInServiceState((PersonStateType)person.state))
            {
                Sango.Log.Error($"*[入城]{person.Name}（{person.BelongForce.Name}）不能进入"
                    + $"{city.Name}（{city.BelongForce.Name}），已改为返回本势力据点"
                    + $" 调用栈:{new System.Diagnostics.StackTrace(true)}");
                ReturnHome(person);
                return;
            }

            // 【原版对齐】君主进入己方势力的据点 → 该据点改挂君主所在军团。
            // 这是 Open311 person_enter 的第一句（is_kunshu && 同势力 → building_change_district）。
            // 语义是"君主在哪，哪就是第一军团"：若不改挂，君主带兵打下的城会留在原军团，
            // 出现"主城在 A 军团、君主本人却挂 B 军团"的分裂，
            // 进而让 CapitalCorps（= 君主所在军团）与实际主城对不上。
            if (person.IsGovernor
                && city.BelongForce == person.BelongForce
                && person.BelongCorps != null
                && city.BelongCorps != person.BelongCorps)
            {
                Sango.Log.Info($"*[军团]{person.Name}（君主）进入 {city.Name}，该城改挂 {person.BelongCorps.Name}");
                CityStateSystem.SetCorps(city, person.BelongCorps);
            }

            // 未发现/在野 转为在籍
            if (person.IsWild || person.Invisible)
            {
                city.RemoveWildPerson(person);
                city.RemoveInvisiblePerson(person);

                // 【顺序：必须先迁户口、再改身份】
                // RebuildRoster 以 BelongCity 为登记口径，而 City.AddPerson 会校验
                // BelongForce 与本城一致（City.cs:1143-1151）。旧顺序是"先 SetState(Normal)、
                // 后 ChangeBelongCity"，于是名册登记用的是**旧户口**：
                // 武将若因城陷/灭国残留了旧军团，就会在 AddPerson 里撞上
                // "<城><势力> -> allPersons 添加 <武将><旧势力> 势力不一致!!"。
                //
                // 在野分支的 ChangeBelongCity 会主动清空军团（Person.cs:3041-3050），
                // 所以这里再显式归到本城军团 —— "在野入城 = 归顺本城势力"，
                // 与原先 SetState 之后由 SyncFromCity 同步的结果一致，只是顺序正确。
                if (person.BelongCity != city)
                    person.ChangeBelongCity(city);
                if (person.BelongCorps != city.BelongCorps)
                    person.BelongCorps = city.BelongCorps;

                SetState(person, PersonStateType.Normal, reason);
            }

            // 第一步：户ロ（内部维护 allPersons 名册与势力/军团同步）
            if (person.BelongCity != city)
                person.ChangeBelongCity(city);
            else
                SyncFromCity(person);

            // 第二步：所在地
            if (person.CurrentCity != city)
                person.ChangeCurrentCity(city);

            // 第三步：旧城太守重算 + 新城太守重算
            if (oldCity != null && oldCity != city)
                oldCity.NeedUpdateLeader();
            city.NeedUpdateLeader();

            // 第四步：新旧军团都督重算（对应 Open311 person_enter 末尾的
            // district_appoint_totoku(old_district) / district_appoint_totoku(new_district)）。
            // 军团人员进出会改变"官职等级 -> 统率"的最优解，不重算会出现
            // "都督已经调走，军团还在用旧都督"的悬空状态。
            Corps newCorps = city.BelongCorps;
            if (oldCorps != newCorps)
            {
                if (oldCorps != null)
                    CityStateSystem.AppointTotoku(oldCorps);
                if (newCorps != null)
                    CityStateSystem.AppointTotoku(newCorps);
            }

            Sango.Log.Info($"*[{reason}]{person.Name} 入城 {city.Name}");
        }

        /// <summary>
        /// 登用归顺：把武将（俘虏 / 在野 / 未发现 / 他势力在职）收编进目标城的势力。
        /// 对应 Open311 的 System::person_change_force。
        ///
        /// 完整事务：摘部队 -> 摘全部旧名册 -> 清命令 -> 切归属 -> 身份转一般 -> 登记新名册。
        /// 与 Enter 的区别：Enter 遇到俘虏只改所在地（不归顺），Recruit 才是"收编"的语义。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="city">归顺后所属城</param>
        /// <param name="reason">变更原因</param>
        public static void Recruit(Person person, City city, PersonChangeReason reason = PersonChangeReason.Employed)
        {
            if (person == null || city == null || person.IsDead)
                return;

            // 无主城没有势力可归顺
            if (city.BelongForce == null)
            {
                Sango.Log.Error($"*[登用]{person.Name} 归顺失败：{city.Name} 是无主城");
                return;
            }

            City oldCity = person.BelongCity;
            City oldCurrent = person.CurrentCity;

            // 【君主不可被收编】在职君主只能通过"继任"或"势力灭亡（force_ruin 的投降分支）"
            // 改变归属；原版 person_change_force 的调用方在收编君主之前都已先把 mibun_ 改掉
            // （s11_sys_scen_force.cpp:976-982），因此走到这里仍是 Kunshu 的只有非法路径。
            //
            // 【为什么必须在改归属之前拦】下面第 4 步会先写 person.BelongCorps，
            // 一旦写完，person.BelongForce 就变成"目标城势力"，
            // 第 5 步的 staysGovernor 判定必然为 false，于是君主被降级成一般武将；
            // 同时户口也落到目标城 —— 就是"君主出去登庸，回来卡在别的城、身份变一般"。
            Force originForce = person.BelongForce;
            if (person.IsGovernor
                && originForce != null && originForce.IsAlive
                && originForce.mGovernor == person
                && originForce != city.BelongForce)
            {
                Sango.Log.Error($"*[登用]{person.Name} 是 {originForce.Name} 的君主，不能作为武将收编进"
                    + $" {city.Name}（{city.BelongForce?.Name ?? "无主"}），本操作已取消"
                    + $" 调用栈:{new System.Diagnostics.StackTrace(true)}");
                return;
            }

            // 1. 摘部队（经部队接口，保证双向一致）
            if (person.mBelongTroop != null)
                person.mBelongTroop.RemovePerson(person);

            // 2. 摘除全部旧名册：在籍 / 在野 / 未发现 / 俘虏 四份都要清，
            //    否则会出现"人已经在新势力，旧城名册里还挂着"的双份脏数据
            if (oldCity != null)
            {
                oldCity.RemovePerson(person);
                oldCity.RemoveWildPerson(person);
                oldCity.RemoveInvisiblePerson(person);
                oldCity.RemoveCaptive(person);
            }

            if (oldCurrent != null && oldCurrent != oldCity)
            {
                oldCurrent.RemovePerson(person);
                oldCurrent.RemoveWildPerson(person);
                oldCurrent.RemoveInvisiblePerson(person);
                oldCurrent.RemoveCaptive(person);
            }

            person.BelongForce?.BeCaptiveList.Remove(person);

            // 3. 清命令（在途任务不能跨势力保留）
            person.ClearMission();

            // 4. 切换归属（势力由军团派生，与 Open311 person_change_force 一致）
            person.BelongCity = city;
            person.BelongCorps = city.BelongCorps;
            // BelongForce 派生自 BelongCorps（对齐原版 get_force_id = district_->force_），无需单独赋值

            // 5. 身份归位
            //
            // 【君主必须区分两种情形】
            //   · 他本来就是本势力主公（新势力的 mGovernor 就是他）→ 保持 Governor，
            //     否则会出现"势力主公却是普通状态"的分裂；
            //   · 他势力君主被收编（劝降合并、势力灭亡后君主归顺）→ 必须降级。
            //     原先这里写的是"只要 IsGovernor 就不降级"，于是投降合并后旧的
            //     mGovernor 带着 Governor 身份进入新势力，而新势力的 mGovernor 是别人 ——
            //     一个势力两个君主：IsActiveGovernor 判真（不可收押）、
            //     太守任免与君主继承全部按"他是主公"处理。
            //
            // 【原版对齐】Open311 force_ruin 的投降分支在转势力之前就把君主身份改掉：
            //   s11_sys_scen_force.cpp:976-982
            //     `if (person->is_kunshu()) person->set_mibun(Mibun_Totoku);`
            //     `person_change_force(person, by, new_kunshu, PersonForceChangeReason_Surrendered);`
            // 本项目没有"都督"这个身份档（都督是军团级职位，见 CityStateSystem.AppointTotoku），
            // 因此降为普通武将（Normal），随后本方法第 7 步的 NeedUpdateLeader
            // 会按能力重新选太守 —— 最终表现为"普通武将或太守"，与玩家可感知的结果一致。
            Force recruitForce = city.BelongForce;
            bool staysGovernor = person.IsGovernor && recruitForce != null && recruitForce.mGovernor == person;
            if (!staysGovernor)
                SetState(person, PersonStateType.Normal, reason);

            // 6. 所在地与在籍名册
            //
            // 【原版对齐】归顺时人不在新主城，**不瞬移**，改为挂返程让他走回去。
            // 依据 Open311 person_change_district 里 reason != _0 的分支
            // （person_change_district 的调用方只有两处传非 0：换势力后的俘虏/在野归顺、
            //   战场投降登用，都等价于"Employed"）：
            //     location != service->location
            //       → person_leave（取最近据点）+ 摘副将 + person_change_order(Return)
            // 早期版本直接 ChangeCurrentCity 把人拉过来，等于"登用一个在成都的半路武将，
            // 他瞬间出现在襄阳"，与原版"要花时间赶路"的设定不符。
            if (!city.allPersons.Contains(person))
                city.AddPerson(person);

            if (person.CurrentCity == null)
            {
                // 位置未知（新生成/数据异常）：只能直接落到新城
                person.ChangeCurrentCity(city);
            }
            else if (person.CurrentCity != city)
            {
                // 在部队里的副将：先摘出来，再让他自己走回新主城
                if (person.mBelongTroop != null)
                {
                    Troop troop = person.mBelongTroop;
                    bool isLeader = troop.Leader == person;
                    troop.RemovePerson(person);
                    if (!isLeader)
                        Sango.Log.Info($"*[登用]{person.Name} 脱离原部队副将位，转入返程");
                }

                ReturnHome(person);
            }

            // 7. 新旧城太守重算
            oldCity?.NeedUpdateLeader();
            city.NeedUpdateLeader();

            Sango.Log.Info($"*[{reason}]{person.Name} 归顺 -> {city.Name}（{city.BelongForce.Name}）");
        }

        /// <summary>
        /// 就地掉头回所属城（对应 Open311 的 exec_return_command）。
        ///
        /// 已在所属城时只清任务；还在路上时改挂返程任务，
        /// 由 UpdateMission 逐回合走回去，到家后再由 OnPersonReturnCity 入城归位。
        /// </summary>
        /// <param name="person">武将</param>
        public static void ReturnHome(Person person)
        {
            if (person == null || person.IsDead)
                return;

            // 已经无势力：无家可回 —— **就地落野**，不能只清任务。
            //
            // 【为什么"只 ClearMission"是错的】
            // ClearMission 只是把 missionType 归零，人还停在半路：
            //   · 户口（BelongCity）可能还指着一个已经不存在的势力下的城；
            //   · 既不在任何城的在籍名册、也不在野，玩家在任何列表里都找不到他；
            //   · 更要命的是 Scenario.UpdateActors 会把"missionType == 0"的条目
            //     从 actor 表里清掉 —— 之后再也不会执行任何一次 UpdateMission，
            //     等于这个人被永久冻结在路上（就是玩家说的"出去的人回不来"）。
            // LeaveToWild 会一次性做全：清任务 + 摘部队 + 清归属 + 改身份 + 登记在野名册，
            // 落到他的 CurrentCity，之后由搜索 / 登用 / 归属巡检正常接管。
            if (person.BelongForce == null)
            {
                LeaveToWild(person);
                return;
            }

            City service = person.BelongCity;

            // 【原版对齐】所属城可能已被敌方攻陷（城陷时在途武将不改户口，service 继续
            // 指向陷落城）。此时"回家"会回到敌城，且到达后 Enter 会把户口落到敌城。
            // 改去本势力都城 —— 到达后 Enter 会正常迁户口与军团。
            if (service != null && service.BelongForce != null && service.BelongForce != person.BelongForce)
            {
                City capital = person.BelongForce.CapitalCity;
                if (capital != null)
                {
                    Sango.Log.Info($"*[在途]{person.Name} 的所属城 {service.Name} 已属 {service.BelongForce.Name}，改返本势力都城 {capital.Name}");
                    service = capital;
                }
                else
                {
                    // 连都城都没有 —— 就地落野，否则清完任务人就被 actor 表丢掉、永久冻结在半路
                    LeaveToWild(person);
                    return;
                }
            }

            if (service == null)
            {
                // 没有所属城（势力已灭/城已失）：同样不能只清任务 ——
                // missionType 归零后 UpdateActors 会把条目从 actor 表清掉，
                // 这个人就再也不会被推进。就地落野，交给搜索 / 登用 / 巡检接管。
                LeaveToWild(person);
                return;
            }

            // 已经在目的地：任务没有意义了，清掉即可
            if (person.CurrentCity == service)
            {
                person.ClearMission();
                return;
            }

            person.SetMission(MissionType.PersonReturn, service);
        }

        /// <summary>
        /// 城陷时把"户口仍挂在本城"的在途/出征武将迁到同势力有效据点。
        ///
        /// 对齐 Open311 的 unit_reset_service（s11_sys_scen_unit.cpp:751-844）：
        ///   ① 同军团都督据点 ② 君主据点（仍有效时）③ 最近己方据点，
        ///   并用"目标 == 陷落城 → 跳过"打破"君主据点即陷落城"的循环。
        /// 在途者（非部队）额外对齐 person_escape（s11_sys_event_capture.cpp:487-518）：
        ///   改户口后重设返程任务，走到新据点再入城归位。
        ///
        /// 【为什么需要】城陷循环只处理"物理在城"者，在途/出征者的户口会留在
        /// 已易主的城，污染 CapitalCity（mGovernor?.BelongCity）派生值，
        /// 进而让下一座城陷的 escapeCity 选到敌城。
        /// </summary>
        /// <param name="person">武将（户口必须仍挂 fallCity）</param>
        /// <param name="fallCity">被攻陷的城</param>
        public static void RelocateHomeOnCityFall(Person person, City fallCity)
        {
            if (person == null || person.IsDead || fallCity == null)
                return;
            if (person.BelongCity != fallCity)
                return;  // 户口不在陷落城，无需迁移
            if (person.BelongForce == null)
                return;  // 无势力：走灭亡分支（LeaveToWild）

            City newHome = ResolveFallReturnPoint(person, fallCity);
            if (newHome == null || newHome == fallCity)
                return;  // 没有其它同势力据点，维持现状（后续按最后一城处理）

            // 君主迁都前记录势力与原军团（dissolve 合并方向用）
            Force force = person.BelongForce;
            Corps oldCorps = person.IsGovernor ? person.BelongCorps : null;

            Sango.Log.Info($"*[城陷]{person.Name} 户口自 {fallCity.Name} 迁至 {newHome.Name}（{(person.mBelongTroop != null ? "随军出征" : "在途")}）");
            person.ChangeBelongCity(newHome);

            // 【原版对齐】君主跨军团迁都：对齐 unit_reset_service 的 district_dissolve
            //（s11_sys_scen_unit.cpp:832-835 → s11_sys_scen_district.cpp:88-133）。
            // 君主户口迁到别的军团城时，把目标城军团整体并入君主原军团（第1军团），
            // 保证"君主 ∈ 第1军团"不变式。
            if (person.IsGovernor && oldCorps != null && newHome.BelongCorps != null
                && newHome.BelongCorps != oldCorps)
            {
                Sango.Log.Info($"*[迁都]{person.Name} 迁都 {newHome.Name}：军团 {newHome.BelongCorps.Name} 并入 {oldCorps.Name}");
                force.MergeCorps(newHome.BelongCorps, oldCorps);
            }

            // 在途者（非部队）对齐 person_escape：改户口后重设返程任务
            if (person.mBelongTroop == null)
            {
                person.ClearMission();
                person.SetMission(MissionType.PersonReturn, person.BelongCity);
            }
        }

        /// <summary>
        /// 城陷后的"迁户口目标"计算，对齐 unit_reset_service 的三级优先级，
        /// 用"目标 == 陷落城 → 跳过"避免选回被攻陷的城本身。
        /// </summary>
        private static City ResolveFallReturnPoint(Person person, City fallCity)
        {
            Force force = person.BelongForce;
            Corps corps = person.BelongCorps;

            // ① 同军团都督据点
            if (corps != null && corps.mComander != null && corps.mComander != person
                && corps.mComander.IsAlive && !corps.mComander.IsDead
                && corps.mComander.BelongCity != null
                && corps.mComander.BelongCity != fallCity
                && corps.mComander.BelongCity.BelongForce == force)
            {
                return corps.mComander.BelongCity;
            }

            // ② 君主据点（仍有效且不等于陷落城）
            City capital = force.CapitalCity;
            if (capital != null && capital != fallCity && capital.BelongForce == force)
            {
                return capital;
            }

            // ③ 同军团最近据点（优先）：对齐 unit_reset_service 的
            //    get_building_list(district == 军团) 优先于 force == 势力 的两级 fallback。
            //    这能避免"君主在军团还有其它城时被迁到别军团"，只有军团无城才跨军团。
            if (corps != null)
            {
                City sameCorpsNearest = null;
                int bestDist = int.MaxValue;
                corps.ForEachCity(c =>
                {
                    if (c == fallCity)
                        return;
                    int d = fallCity.Distance(c);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        sameCorpsNearest = c;
                    }
                });
                if (sameCorpsNearest != null)
                    return sameCorpsNearest;
            }

            // ④ 同势力最近据点（GetNearnestForceCity 内部有 BelongForce 过滤）
            return fallCity.GetNearnestForceCity();
        }

        /// <summary>
        /// 某据点易主/被摧毁时，取消所有"以该据点为目的地"的在途任务。
        ///
        /// 对应 Open311 的 cancel_all_order(BuildingForceChanged) +
        /// can_cancel_move_order 里的"目的地 == 变更据点"判定，
        /// 处置沿用 exec_return_command：就地掉头回出发城。
        ///
        /// 注意只取消"目的地是该城"的任务：路过该城、或目的地是别处的武将不受影响
        /// —— 原版同样只按目的地匹配，不做路径重算。
        /// </summary>
        /// <param name="city">发生归属变更的据点</param>
        /// <returns>被取消的任务数</returns>
        public static int CancelMissionsToCity(City city)
        {
            if (city == null)
                return 0;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return 0;

            // 先取快照：取消过程会修改在途表
            System.Collections.Generic.List<Person> targets = new System.Collections.Generic.List<Person>();
            System.Collections.Generic.IReadOnlyList<Person> actors = scenario.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                Person person = actors[i];
                if (person == null || !person.IsAlive || person.IsDead)
                    continue;
                if (IsDestinationOf(person, city))
                    targets.Add(person);
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Person person = targets[i];
                Sango.Log.Info($"*[在途]{person.Name} 的目的地 {city.Name} 归属变更，任务取消，改为返程");
                ReturnHome(person);
            }

            return targets.Count;
        }

        /// <summary>
        /// 判断该在途武将的任务目的地是否就是指定据点。
        /// 各任务存放目的地的字段不同，这里集中处理，避免调用方各写一套判断。
        /// </summary>
        /// <param name="person">在途武将</param>
        /// <param name="city">据点</param>
        /// <returns>目的地是否为该据点</returns>
        private static bool IsDestinationOf(Person person, City city)
        {
            switch ((MissionType)person.missionType)
            {
                // 这三个任务的目的地直接存在 missionTarget（城 Id）
                case MissionType.PersonReturn:
                case MissionType.PersonDiplomacy:
                case MissionType.PersonCityStrategy:
                    return person.missionTarget == city.Id;

                // 招募任务：missionTarget 是被招募的武将，目的地城存在 missionParams1
                case MissionType.PersonRecruitPerson:
                    return person.missionParams1 == city.Id;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 迁城（对应 Open311 的 person_change_district）。
        ///
        /// 与 Enter 的区别：迁城一定会更换所属城，并触发旧城收尾（摘名册、太守重算）。
        /// 在籍武将迁入敌方城池时会按"无主城"处理转为下野，不会凭空获得敌方势力。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="city">目标据点</param>
        /// <param name="reason">变更原因</param>
        public static void MoveToCity(Person person, City city, PersonChangeReason reason = PersonChangeReason.Moved)
        {
            if (person == null || city == null || person.IsDead)
                return;

            City oldCity = person.BelongCity;

            // 迁入不同势力的城 -> 先下野再入城，避免"带着旧势力进驻敌城"
            if (city.BelongForce == null || (person.BelongForce != null && person.BelongForce != city.BelongForce))
            {
                LeaveToWild(person, city, PersonChangeReason.ForceChanged);
            }

            Enter(person, city, reason);

            if (oldCity != null && oldCity != city)
            {
                oldCity.RemovePerson(person);
                oldCity.RemoveWildPerson(person);
                oldCity.RemoveInvisiblePerson(person);
                oldCity.NeedUpdateLeader();
            }
        }

        /// <summary>
        /// 下野（对应 Open311 的 person_set_zaiya）。
        ///
        /// 完整事务：清命令 -> 摘部队 -> 清势力/军团 -> 改身份 -> 迁名册。
        /// 顺序与 Open311 一致（先摘归属再改身份），否则状态与归属会互相打架。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="city">下野后所在据点了，null 表示原地不动</param>
        /// <param name="reason">变更原因</param>
        public static void LeaveToWild(Person person, City city = null, PersonChangeReason reason = PersonChangeReason.LeftToWild)
        {
            if (person == null || person.IsDead)
                return;

            City oldCity = person.BelongCity;
            City stayCity = city ?? person.CurrentCity ?? oldCity;

            // 1. 清命令（否则会残留"正在执行某工作"的脏标记，卡住城池策略）
            person.ClearMission();

            // 2. 摘除部队（必须经部队接口摘，直接置空会留下双向不一致）
            if (person.mBelongTroop != null)
                person.mBelongTroop.RemovePerson(person);

            // 3. 旧名册摘除
            oldCity?.RemovePerson(person);
            oldCity?.RemoveWildPerson(person);
            if (person.CurrentCity != null && person.CurrentCity != oldCity)
                person.CurrentCity.RemovePerson(person);

            // 4. 清归属（对应 Open311 person_set_district_id(self, -1)）
            person.BelongCorps = null;
            // BelongForce 派生自 BelongCorps，随军团一并清空
            person.BelongCity = stayCity;
            if (stayCity != null && person.CurrentCity != stayCity)
                person.ChangeCurrentCity(stayCity);

            // 5. 改身份
            SetState(person, PersonStateType.Unemployed, reason);

            // 6. 登记在野名册
            if (stayCity != null)
                stayCity.AddWildPerson(person);

            // 7. 太守重算
            oldCity?.NeedUpdateLeader();

            Sango.Log.Info($"*[{reason}]{person.Name} 下野 -> {(stayCity != null ? stayCity.Name : "无城")}");
        }

        /// <summary>
        /// 被俘（对应 Open311 的 System::capture）。
        ///
        /// 保留原势力/军团（登用与释放判定要读），清部队与命令，改身份并进俘虏名册。
        /// </summary>
        /// <param name="person">被俘武将</param>
        /// <param name="city">关押据点</param>
        /// <param name="reason">变更原因</param>
        public static void ToCaptive(Person person, City city, PersonChangeReason reason = PersonChangeReason.Captured, Troop troop = null)
        {
            if (person == null || person.IsDead)
                return;
            if (city == null && troop == null)
            {
                Sango.Log.Error($"*收押失败：{person.Name} 没有关押城也没有关押部队");
                return;
            }

            // 势力尚存的君主不可收押（只能释放或斩首）。
            // 用 IsActiveGovernor 而不是 IsGovernor：势力已灭亡的君主下野后只是普通在野武将，
            // 可以被收押/招募；若只看 state，灭亡流程尚未跑到下野时会误拦。
            if (person.IsActiveGovernor)
            {
                Sango.Log.Error($"*拒绝收押君主 {person.Name}（势力尚存，只能被释放或斩首）");
                return;
            }

            person.OnWillBeCaptive();
            person.ClearMission();
            // 关押计时归零 —— 对齐 Open311 的 prison_timer_（入狱即 0，每月 +1）
            person.prisonTimer = 0;

            // 从原部队摘除必须经部队接口，直接置空会留下双向不一致。
            // 注意捕获者本身可能就是这支部队，摘完要重新挂回去，故这里排除 troop==oldTroop。
            //
            // 两种来源要分开处理：
            //   · 原来是随军俘虏（在 captiveList 里）→ RemoveCaptive；
            //   · 原来是部队成员（在 Leader/Member 里）→ RemovePerson。
            // 用 RemovePerson 摘俘虏会走"成员摘光 → 部队解散"的分支，把部队误销毁。
            Troop oldTroop = person.mBelongTroop;
            if (oldTroop != null && oldTroop != troop)
            {
                if (oldTroop.captiveList.Contains(person))
                    oldTroop.RemoveCaptive(person);
                else
                    oldTroop.RemovePerson(person);
            }

            // 从原城名册摘除（在籍/在野/未发现三份都要清）
            City oldCity = person.BelongCity;
            oldCity?.RemovePerson(person);
            oldCity?.RemoveWildPerson(person);
            oldCity?.RemoveInvisiblePerson(person);

            person.BelongCity = null;

            // 关押位置：部队内俘虏挂在部队上（随军移动），城内俘虏挂在城上
            if (troop != null)
            {
                person.mBelongTroop = troop;
                if (city != null && person.CurrentCity != city)
                    person.ChangeCurrentCity(city);
            }
            else if (person.CurrentCity != city)
            {
                person.ChangeCurrentCity(city);
            }

            // 先改身份，再登记名册，保证"名单里的人是俘虏状态"
            SetState(person, PersonStateType.Prisoner, reason);

            // 俘虏名册与势力俘虏索引（直接登记，避免与 City.AddCaptive / Troop.AddCaptive 互相调用成环）
            if (troop != null)
            {
                if (!troop.captiveList.Contains(person))
                    troop.captiveList.Add(person);
            }
            else if (!city.captiveList.Contains(person))
            {
                city.captiveList.Add(person);
            }

            if (person.BelongForce != null)
            {
                person.BelongForce.BeCaptiveList.Remove(person);
                person.BelongForce.BeCaptiveList.Add(person);
            }

            oldCity?.NeedUpdateLeader();
            city?.NeedUpdateLeader();

            Sango.Log.Info($"*[{reason}]{person.Name} 被俘 -> {(city != null ? city.Name : troop.Name)}");
        }

        /// <summary>
        /// 释放俘虏（对应 Open311 的 person_release）：转为在野并迁到指定据点。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="city">释放后所在据点</param>
        /// <param name="reason">变更原因</param>
        public static void Release(Person person, City city, PersonChangeReason reason = PersonChangeReason.Released)
        {
            if (person == null || person.IsDead || !person.IsPrisoner)
                return;

            // 随军俘虏：先从部队的俘虏名册摘除，再清部队引用。
            // 不处理会留下"人已在野、部队 captiveList 里还挂着他"的脏数据，
            // 部队解散时又会把这个已释放的人当俘虏再处理一遍。
            if (person.mBelongTroop != null)
            {
                person.mBelongTroop.captiveList.Remove(person);
                person.mBelongTroop = null;
            }

            person.CurrentCity?.RemoveCaptive(person);
            person.BelongForce?.BeCaptiveList.Remove(person);

            // 释放后不再保留原势力/军团（避免"在野却隶属某势力"的历史脏数据）
            // BelongForce 派生自 BelongCorps，随军团一并清空
            person.BelongCorps = null;

            LeaveToWild(person, city ?? person.CurrentCity, reason);
        }

        /// <summary>
        /// 设置武将所属军团（对应 Open311 的 person_set_district_id）。
        ///
        /// 势力由军团派生：军团变化时同步 BelongForce，不单独赋值，避免两者分裂。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="corps">目标军团</param>
        public static void SetCorps(Person person, Corps corps, bool fullTransaction = true)
        {
            if (person == null)
                return;

            if (person.BelongCorps == corps)
                return;

            Corps oldCorps = person.BelongCorps;

            // 原版 person_change_district：旧军团的都督若是本人，先卸任再换军团，
            // 否则会留下"都督字段指向已调离武将"的悬空引用
            if (fullTransaction && oldCorps != null && oldCorps.mComander == person)
            {
                person.SetStateNormal();
                oldCorps.mComander = null;
                oldCorps.Comander = 0;
                oldCorps.NeedUpdateCommander();
            }

            Force oldForce = oldCorps != null ? oldCorps.BelongForce : person.BelongForce;

            person.BelongCorps = corps;
            // BelongForce 派生自 BelongCorps

            // 巡检/批量修数据时只改字段，不做官职与身份的连带处理
            if (!fullTransaction)
                return;

            // 原版 person_change_district：旧势力解任军师身份
            // （old_force->dismiss_gunshi_id(self->get_id())）。
            // 不解会导致势力面板的军师指向一个已经不在本势力的人。
            if (oldForce != null && oldForce != person.BelongForce && oldForce.mCounsellor == person)
            {
                oldForce.Counsellor = 0;
                oldForce.mCounsellor = null;
                Sango.Log.Info($"*[人事]{person.Name} 离开 {oldForce.Name}，其军师职务已解除");
            }

            // 原版 person_change_district：记录"禁止仕官旧主"半年
            // （person_set_banned_kunshu(self, old_force->get_kunshu_id(), 6)）。
            // 防止"刚叛逃到敌方、下个月又被旧主登用回去"的来回横跳。
            if (oldForce != null && oldForce != person.BelongForce && oldForce.mGovernor != null)
            {
                person.BannedKunshuId = oldForce.mGovernor.Id;
                person.BanTimer = BanMonthsAfterLeavingForce;
            }

            // 原版 person_change_district：换军团清空官职（person_set_rank_id(RankId_None)）
            Scenario scenario = Scenario.Cur;
            if (scenario != null && scenario.CommonData != null && scenario.CommonData.Officials != null)
                person.UpgradeOfficial(scenario.CommonData.Officials.Get(0));

            // 在野 / 俘虏 / 未发现 被收编进军团 → 转一般武将
            if (person.IsWild || person.IsPrisoner || person.Invisible)
            {
                SetState(person, PersonStateType.Normal, PersonChangeReason.DistrictChanged);
            }
            // 太守留任判定：原身份是太守，但所属城已不在本军团 → 降为一般
            else if (person.IsLeader && (person.BelongCity == null || person.BelongCity.BelongCorps != corps))
            {
                SetState(person, PersonStateType.Normal, PersonChangeReason.DistrictChanged);
            }

            // 原版 person_change_district：进入新势力后按新君主重算忠诚
            // （person_reset_loyalty(self, new_force, false)）
            if (person.BelongForce != null)
                ResetLoyalty(person, person.BelongForce);
        }

        /// <summary>
        /// 按君主重算忠诚（对应 Open311 的 person_reset_loyalty）。
        ///
        /// 原版公式（loyalty 上限 255）：
        ///   君主本人 / 婚姻 / 义兄弟 / 亲爱 → 直接拉高到 120~250
        ///   其余：100 + 君主魅力/14 - 相性距离*10/义理，再按厌恶(-20)/血缘(+5)调整，最后 min(n,100)
        ///
        /// 本项目的 loyalty 是 0~100 制，且没有"相性距离 / 义理 / 出身地"这些细分字段，
        /// 因此保留结构、去掉无法映射的项：君主魅力决定基础值，厌恶关系扣 20，
        /// 亲爱关系直接给满。这样"魅力高的君主部下更忠"这一核心手感与原版一致。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="force">所属势力</param>
        public static void ResetLoyalty(Person person, Force force)
        {
            if (person == null || force == null || person.IsDead)
                return;

            Person governor = force.mGovernor;
            if (governor == null || governor == person || governor.IsDead)
                return;

            // 君主本人恒满
            if (person.IsGovernor)
            {
                person.loyalty = 100;
                return;
            }

            // 亲爱关系直接满（原版是 max(loyalty, 120)，本项目 100 制取满）
            if (IsLikedBy(person, governor.Id))
            {
                person.loyalty = 100;
                return;
            }

            // 基础值：君主魅力 / 14 + 100（魅力 99 → 107，钳到 100）
            int baseValue = 100 + governor.Glamour / 14;

            // 厌恶关系大幅扣减
            if (IsHatedBy(person, governor.Id))
                baseValue -= 20;

            if (baseValue < 10)
                baseValue = 10;
            else if (baseValue > 100)
                baseValue = 100;

            person.loyalty = baseValue;
        }

        /// <summary>武将是否亲爱指定武将。</summary>
        /// <param name="person">武将</param>
        /// <param name="otherId">目标武将 Id</param>
        /// <returns>是否亲爱</returns>
        private static bool IsLikedBy(Person person, int otherId)
        {
            if (person.LikePersonListId == null)
                return false;
            for (int i = 0; i < person.LikePersonListId.Length; i++)
            {
                if (person.LikePersonListId[i] == otherId)
                    return true;
            }
            return false;
        }

        /// <summary>武将是否厌恶指定武将。</summary>
        /// <param name="person">武将</param>
        /// <param name="otherId">目标武将 Id</param>
        /// <returns>是否厌恶</returns>
        private static bool IsHatedBy(Person person, int otherId)
        {
            if (person.HatePersonListId == null)
                return false;
            for (int i = 0; i < person.HatePersonListId.Length; i++)
            {
                if (person.HatePersonListId[i] == otherId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 以所属城市为准，同步武将的势力与军团（对应 Open311 的"以城为准"纠正）。
        /// 这是消除"武将势力 != 所属城势力"这类脏数据的核心手段。
        /// </summary>
        /// <param name="person">武将</param>
        /// <returns>是否发生了修正</returns>
        public static bool SyncFromCity(Person person)
        {
            if (person == null || person.BelongCity == null)
                return false;

            // 俘虏保留原属国，不参与"以城为准"同步
            if (person.IsPrisoner)
                return false;

            City city = person.BelongCity;
            bool changed = false;

            if (person.BelongCorps != city.BelongCorps)
            {
                person.BelongCorps = city.BelongCorps;
                changed = true;
            }

            // 【锚点】势力由军团派生（Open311: Person::get_force_id = district_->force_），
            // 故只需保证军团一致，势力自动跟随，无需再单独校验/回写 BelongForce。
            return changed;
        }

        /// <summary>
        /// 判断武将是否确实"在城里待命"（对应 Open311 的 is_at_service）。
        /// 条件：不是部队成员、没有正在执行的任务、所在地就是所属城。
        /// </summary>
        /// <param name="person">武将</param>
        /// <returns>是否在城待命</returns>
        public static bool IsAtService(Person person)
        {
            if (person == null || person.IsDead)
                return false;
            if (person.mBelongTroop != null)
                return false;
            if (person.BelongCity == null)
                return false;
            return person.CurrentCity == person.BelongCity;
        }

        /// <summary>
        /// 按身份变化重建城市侧名册。
        /// 城市维护了 6 份名单（在籍/空闲/在野/未发现/俘虏/其它），
        /// 身份一变就必须把对应的人从旧名单挪到新名单。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="oldState">旧身份</param>
        /// <param name="newState">新身份</param>
        private static void RebuildRoster(Person person, PersonStateType oldState, PersonStateType newState)
        {
            // 名册以"所属城"为登记口径（allPersons 就是按 BelongCity 收录的），
            // 不能用 CurrentCity：迁城过程中两者会短暂不同，用错会把脏数据写进旧城名册。
            City city = person.BelongCity ?? person.CurrentCity;
            if (city == null)
                return;

            bool wasInService = IsInServiceState(oldState);
            bool isInService = IsInServiceState(newState);

            // 在籍 <-> 非在籍：allPersons 名册增删。
            // 登记只在"身份已转入在籍 且 所属城已是本城"时进行：
            // 俘虏脱逃转一般武将的瞬间 BelongCity 还是 null，若此时登记，
            // 会把人错误地塞进关押城（即 oldCity）的在籍名单。
            if (wasInService && !isInService)
                city.RemovePerson(person);
            else if (!wasInService && isInService && person.BelongCity == city)
                city.AddPerson(person);

            // 在野名册
            if (newState == PersonStateType.Unemployed)
                city.AddWildPerson(person);
            else if (oldState == PersonStateType.Unemployed)
                city.RemoveWildPerson(person);

            // 未发现名册
            if (newState == PersonStateType.Invisible)
                city.AddInvisiblePerson(person);
            else if (oldState == PersonStateType.Invisible)
                city.RemoveInvisiblePerson(person);

            // 俘虏名册
            if (newState == PersonStateType.Prisoner)
            {
                if (!city.captiveList.Contains(person))
                    city.AddCaptive(person);
            }
            else if (oldState == PersonStateType.Prisoner)
            {
                city.RemoveCaptive(person);
            }

            // 状态变化可能影响太守资格
            if (wasInService || isInService)
                city.NeedUpdateLeader();
        }

        /// <summary>
        /// 是否为"在籍"身份（君主/都督/太守/一般）。
        /// </summary>
        /// <param name="state">身份</param>
        /// <returns>是否在籍</returns>
        public static bool IsInServiceState(PersonStateType state)
        {
            return state == PersonStateType.Governor
                || state == PersonStateType.Commander
                || state == PersonStateType.Leader
                || state == PersonStateType.Normal;
        }
    }
}
