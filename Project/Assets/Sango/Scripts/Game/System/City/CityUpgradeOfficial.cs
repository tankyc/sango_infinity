using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 官职任命（原版的"官职"对话框）。
    ///
    /// 【原版出处】
    /// <list type="bullet">
    /// <item>官职槽模型 <c>RankDialog::slot_</c>：s11_sys_ui.h:75-85、s11_sys_ui_rank_dialog.cpp:264-343
    ///   —— 一个官职（rank）只对应一个槽位，槽位里的人是该官职的现任者，
    ///   "每个职位唯一、只能一人担任"是这个模型自带的约束，不需要额外去重。</item>
    /// <item>提交 <c>PersonOrderManager::exec_ranks_command</c>：s11_sys_person_order_kunshu.cpp:642-695
    ///   —— 先清空全势力都督/太守/一般武将的官职（663-667 行），再按槽位重新设置（668-672 行）。</item>
    /// <item>菜单门禁 <c>can_ranks_command</c>：s11_sys_person_order_kunshu.cpp:577-640
    ///   —— 黄巾势力禁则 + 至少存在一名非出征武将。</item>
    /// <item>自动设定 <c>RankDialog::auto_fill</c>：s11_sys_ui_rank_dialog.cpp:264-343。</item>
    /// <item>候选排序 <c>auto_fill(Force*)</c>：s11_sys_ui_rank_dialog.cpp:345-366
    ///   —— 功绩降序、同功绩按 Id 升序。</item>
    /// </list>
    /// </summary>
    [GameSystem]
    public class CityUpgradeOfficial : CityBaseSystem
    {
        /// <summary>
        /// 本势力可任免的**全部**武将（固定集合，打开界面时算一次）。
        /// 对齐 <c>RankDialog::person_list_</c>（s11_sys_ui_rank_dialog.cpp:350）：
        /// 取都督 / 太守 / 一般武将，排除君主、在野、俘虏、未发现、出征中。
        ///
        /// 【本项目差异】在此基础上再排除"功绩够不上品级最低官职"的人（见
        /// <see cref="GetLowestOfficialMerit"/>）：原版这类人仍会列出来，只是点任何官职都会被
        /// can_set 拒绝；本项目把准入线前移到列表层，避免右表堆满点不动的条目。
        /// </summary>
        public List<Person> allPersons = new List<Person>();

        /// <summary>
        /// 右表：当前**空闲**（没有官职）的武将 = <see cref="allPersons"/> 中不在任何官职槽里的人。
        ///
        /// 界面规则：已有官职的武将不显示在右侧；被顶替 / 被免职的人会回到这个列表。
        /// </summary>
        public List<Person> targetList = new List<Person>();

        /// <summary>
        /// 官职槽（左表一行 = 一个官职）。
        /// 对齐 <c>RankDialog::Slot</c>（s11_sys_ui.h:75-80）的 person 字段：
        /// <see cref="person"/> 为 null 表示该官职空缺。
        /// </summary>
        public struct UpgradeOfficial
        {
            /// <summary>该官职的现任者；null = 空缺</summary>
            public Person person;

            /// <summary>官职</summary>
            public Official official;
        }

        /// <summary>
        /// 左表官职槽列表，按"官大 → 官小"排列（level 升序、同 level 按 Id 降序）。
        /// 长度固定为官职表里的有效条目数（不含 Id 0 的"无"）。
        /// </summary>
        public List<UpgradeOfficial> upgradeList = new List<UpgradeOfficial>();

        /// <summary>
        /// 打开界面时的官职快照（与 <see cref="upgradeList"/> 等长），供"恢复原状"还原。
        /// 原版靠 `RankDialog::auto_fill` 重建槽位实现复位，本项目界面在打开时留快照更直接。
        /// 右表（空闲武将）不需要快照：它恒等于「全部可任免武将 − 槽内任职者」，还原槽位即可自动推回。
        /// </summary>
        List<Person> originPersons = new List<Person>();

        /// <summary>右表当前的排序依据（点表头时记录，列表重建后仍按它排）</summary>
        ObjectSortTitle currentSortTitle;

        /// <summary>右表当前排序方向</summary>
        bool currentSortUp;

        public List<ObjectsDisplaySystem.ButtonData> buttonDatas;

        public CityUpgradeOfficial()
        {
            customTitleName = "官职";

            // 右表列：对齐原版官职界面的武将表（武将/功绩/当前官职/指挥/统率/武力/智力/政治）。
            // 第一列是固定列（预制体 item_person/textItem），其余列由 item_person/mask/content 横向滚动承载。
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByMerit,
                PersonSortFunction.SortByOfficial,
                PersonSortFunction.SortByTroopsLimit,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
            };
            customMenuName = "君主/官职";
            customMenuOrder = 4200;
            windowName = "window_official_set_org";

            buttonDatas = new List<ObjectsDisplaySystem.ButtonData>()
            {
                new ObjectsDisplaySystem.ButtonData()
                {
                    title = "自动设定",
                    style = 0,
                    action = AutoSet
                },

                new ObjectsDisplaySystem.ButtonData()
                {
                    title = "全解除",
                    style = 0,
                    action = ClearAll
                },

                new ObjectsDisplaySystem.ButtonData()
                {
                    title = "恢复原状",
                    style = 0,
                    action = Restore
                }
            };
        }

        protected override bool MenuCanShow()
        {
            return TargetCity.IsCityBase();
        }

        /// <summary>
        /// 命令是否可用。
        ///
        /// 【对齐】Open311 <c>PersonOrderManager::can_ranks_command</c>（s11_sys_person_order_kunshu.cpp:577-640）：
        /// ① 势力有效；② 黄巾（贼）势力禁止（582-591 行）；③ 存在可任免武将（593-613 行）。
        ///
        /// 【未对齐】原版还要求"官职名额未满"（614-632 行，get_available_rank_list 减去已占用的），
        /// 本项目的官职是**逐人唯一**而不是名额制，官职数量恒大于可用武将数时不成立，
        /// 所以不引入这条判定。
        /// </summary>
        public override bool IsValid
        {
            get
            {
                BuildAllPersons();
                return allPersons.Count > 0;
            }
        }

        /// <summary>
        /// 命令不可用时的原因（对齐原版 can_ranks_command 里的 set_command_error_text，
        /// s11_sys_person_order_kunshu.cpp:585/608/631）。
        /// </summary>
        public override string GetInvalidReason(City city)
        {
            if (city == null || city.BelongForce == null)
                return "无效的势力";
            if (city.BelongForce.IsBandit)
                return "该势力无法任命官职";
            return "没有可任命的武将";
        }

        /// <summary>
        /// 收集本势力可任免的全部武将（对齐 s11_sys_person_order_kunshu.cpp:593-603 的过滤条件）。
        /// 只填 <see cref="allPersons"/>，右表由 <see cref="RebuildTargetList"/> 推导。
        /// </summary>
        void BuildAllPersons()
        {
            allPersons.Clear();

            if (TargetCity == null || TargetCity.BelongForce == null)
                return;

            // 黄巾 / 山贼势力不可任命官职（原版 582-591 行的 KokugouId_YellowTurban 禁则）
            if (TargetCity.BelongForce.IsBandit)
                return;

            Force force = TargetCity.BelongForce;
            Person governor = force.mGovernor;
            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            // 右表准入线：连品级最低的官职都够不上的人，列出来也点不了任何一行，直接不显示。
            // 【原版无对应实现】原版 person_list_（s11_sys_ui_rank_dialog.cpp:350）只做身份过滤，
            // 功绩门槛是在 can_set（102-116 行）里逐官职判定的；本项目官职表是全开放列表，
            // 若不过滤，右表会被大量 0 功绩武将占满（当前剧本 850 人里有 629 人初始功绩为 0）。
            int minMerit = GetLowestOfficialMerit();

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive)
                    continue;
                if (person.BelongForce != force)
                    continue;
                if (person == governor)
                    continue;
                if (!CanBeOfficialPerson(person))
                    continue;
                if (minMerit > 0 && person.merit < minMerit)
                    continue;
                allPersons.Add(person);
            }
        }

        /// <summary>
        /// 右表准入线 = 官职表里**品级最低**（level 最大）那一档的功绩门槛。
        /// 同一档有多个官职时取最小门槛；官职表未加载、或该档门槛为 0 时返回 0（表示不过滤）。
        ///
        /// 【为什么按"品级最低"而不是"门槛最小"】官职表按 level 分层设计，门槛随等级单调递增，
        /// 两套口径结果一致；但若策划把某高阶官职的 meritNeeds 误填成很小的值，
        /// 按"门槛最小"会把准入线拉到那个错值上，按"品级最低"则不受影响。
        /// </summary>
        public int GetLowestOfficialMerit()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Officials == null)
                return 0;

            Official[] all = scenario.CommonData.Officials.objects;
            int maxLevel = int.MinValue;
            int merit = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Official official = all[i];
                // Id 0 是"无官职"占位，level 15 且门槛 0，不参与准入线计算
                if (official == null || official.Id == 0)
                    continue;

                if (official.level < maxLevel)
                    continue;

                int need = official.RequiredMerit;
                if (official.level > maxLevel)
                {
                    maxLevel = official.level;
                    merit = need;
                }
                else if (need < merit)
                {
                    merit = need;   // 同档取最小
                }
            }
            return merit;
        }

        /// <summary>
        /// 重建右表：空闲武将 = 全部可任免武将中**没有占据任何官职槽**的人。
        /// "已有官职的武将不出现在右侧列表"就是靠这一步实现；被顶替 / 被免职的人因为槽位被清空，
        /// 下一次重建时自然回到右表。
        /// </summary>
        public void RebuildTargetList()
        {
            targetList.Clear();
            for (int i = 0; i < allPersons.Count; i++)
            {
                Person person = allPersons[i];
                if (person == null)
                    continue;
                if (IndexOfPersonSlot(person) >= 0)
                    continue;
                targetList.Add(person);
            }
            SortTargetList();
        }

        /// <summary>把某个武将放回右表（若他本来就在可任免集合里，且不在表内）</summary>
        void AddToTargetList(Person person)
        {
            if (person == null || targetList.Contains(person))
                return;
            if (!allPersons.Contains(person))
                return;   // 出征 / 俘虏等不可任免的人被顶替后不回到右表
            targetList.Add(person);
        }

        /// <summary>
        /// 右表排序。列表会在"有人入册 / 被顶替 / 被免职"后重建，
        /// 因此必须记住玩家最后点过的表头，重建后仍按同一个依据排。
        /// </summary>
        public void SetSort(ObjectSortTitle sortTitle, bool up)
        {
            currentSortTitle = sortTitle;
            currentSortUp = up;
            SortTargetList();
        }

        void SortTargetList()
        {
            ObjectSortTitle sortTitle = currentSortTitle;
            if (sortTitle == null)
                sortTitle = customTitleList[0];
            ObjectSortTitle.SortList(targetList, sortTitle.Sort, currentSortUp);
        }

        /// <summary>
        /// 该武将是否属于"可任免身份"。
        /// 对齐原版 <c>test_mibun({ Mibun_Totoku, Mibun_Taishu, Mibun_Ippan })</c> + <c>not is_unitize()</c>
        /// （s11_sys_person_order_kunshu.cpp:598-601）。
        /// </summary>
        public static bool CanBeOfficialPerson(Person person)
        {
            if (person == null || !person.IsAlive)
                return false;

            int state = person.state;
            // 只认 都督 / 太守 / 一般 三种身份（原版 Mibun_Totoku / Mibun_Taishu / Mibun_Ippan）
            if (state != (int)PersonStateType.Commander
                && state != (int)PersonStateType.Leader
                && state != (int)PersonStateType.Normal)
                return false;

            // 出征中（原版 is_unitize）不参与任免
            if (person.mBelongTroop != null)
                return false;

            return true;
        }

        public override void OnEnter()
        {
            // 顺序不能换：先算可任免集合 → 建槽并回填现任者 → 再由槽推导"空闲武将"右表。
            // 若先算右表，此时槽还空着，所有有官职的人都会漏进右表。
            BuildAllPersons();
            BuildOfficialSlots();
            RebuildTargetList();
            base.OnEnter();
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
        }

        public override void OnDestroy()
        {
            GameEvent.DialogClose?.Invoke();
            base.OnDestroy();
        }

        /// <summary>
        /// 构建官职槽并记录快照。
        ///
        /// 排序口径：level 升序（0 = 最高品级，排最前）、同 level 按 Id 降序
        /// —— 与官职表 Id 由低到高的编排一致（Id 80=丞相 最前，Id 1=武卫校尉 最后）。
        /// </summary>
        void BuildOfficialSlots()
        {
            upgradeList.Clear();
            originPersons.Clear();

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Officials == null)
                return;

            Official[] all = scenario.CommonData.Officials.objects;
            List<Official> officials = new List<Official>();
            for (int i = 0; i < all.Length; i++)
            {
                Official official = all[i];
                // Id 0 是"无官职"占位（Officials.Get(0)），不进官职表
                if (official == null || official.Id == 0)
                    continue;
                officials.Add(official);
            }
            officials.Sort(CompareOfficial);

            // 先建空槽，再把现任者填进去。
            // 若同一官职被两人任职（脏数据 / 旧存档），只认第一个 —— 槽位模型本身只允许一个，
            // 第二个在提交时会因为"先全清再按槽设置"而自动落下（对齐 exec_ranks_command 663-672 行）。
            Dictionary<int, int> indexOfOfficial = new Dictionary<int, int>(officials.Count);
            for (int i = 0; i < officials.Count; i++)
            {
                indexOfOfficial[officials[i].Id] = i;
                upgradeList.Add(new UpgradeOfficial()
                {
                    person = null,
                    official = officials[i]
                });
            }

            // 填现任者时遍历的是**全势力**武将，而不只是右表的可任免列表。
            // 理由对齐原版 auto_fill（s11_sys_ui_rank_dialog.cpp:269-290）：出征中（is_unitize）
            // 和被俘（is_horyo）的人不参与任免，但他们在槽位上仍占着原来的官职，
            // 原版会把这些人**放回槽位**，别人就不能再抢同一官职；
            // 若这里只按 targetList 填，出征者的官职会显示成"空缺"，玩家往上一放人，
            // 提交后就成了"同一官职两人担任"—— 正是原版不会出现的状态。
            Force force = TargetCity != null ? TargetCity.BelongForce : null;
            if (force != null)
            {
                Person governor = force.mGovernor;
                force.ForEachPerson(person =>
                {
                    if (person == null || !person.IsAlive)
                        return;
                    if (person == governor)
                        return;

                    Official official = person.Official;
                    if (official == null || official.Id == 0)
                        return;

                    int index;
                    if (!indexOfOfficial.TryGetValue(official.Id, out index))
                        return;

                    UpgradeOfficial slot = upgradeList[index];
                    if (slot.person == null)
                    {
                        slot.person = person;
                        upgradeList[index] = slot;
                    }
                });
            }

            // 快照（恢复原状用）
            for (int i = 0; i < upgradeList.Count; i++)
                originPersons.Add(upgradeList[i].person);
        }

        /// <summary>
        /// 官职排序：level 升序（数字小 = 官大），同 level 按 Id 降序。
        /// 原版直接按 rank_id 升序（s11_sys_ui_rank_dialog.cpp:8-94 的表序），本项目用 level+Id 复现同一顺序。
        /// </summary>
        static int CompareOfficial(Official a, Official b)
        {
            if (a == b) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            if (a.level != b.level)
                return a.level.CompareTo(b.level);
            return b.Id.CompareTo(a.Id);
        }

        public void SortList()
        {
            // 官职从高到低；同官职者按 Id 兜底，保证每次排完的顺序一致
            ObjectSortTitle.SortList(targetList, PersonSortFunction.SortByOfficial.Sort, false);
        }

        /// <summary>按 Id 找官职在槽列表里的下标；找不到返回 -1</summary>
        public int IndexOfOfficial(Official official)
        {
            if (official == null) return -1;
            for (int i = 0; i < upgradeList.Count; i++)
            {
                Official o = upgradeList[i].official;
                if (o != null && o.Id == official.Id)
                    return i;
            }
            return -1;
        }

        /// <summary>按武将找它占用的槽下标；不在任何槽里返回 -1</summary>
        public int IndexOfPersonSlot(Person person)
        {
            if (person == null) return -1;
            for (int i = 0; i < upgradeList.Count; i++)
            {
                if (upgradeList[i].person == person)
                    return i;
            }
            return -1;
        }

        /// <summary>查该官职的现任者（槽位唯一约束的读取点）</summary>
        public Person GetOfficialPerson(Official official)
        {
            int index = IndexOfOfficial(official);
            return index < 0 ? null : upgradeList[index].person;
        }

        /// <summary>
        /// 该武将能否担任该官职（功绩门槛 + 属性门槛）。
        /// 对齐 <c>RankDialog::can_set</c>（s11_sys_ui_rank_dialog.cpp:102-116）与
        /// <c>System::can_set_rank</c>（s11_sys_scen_person.cpp:1102-1118）：人存活、官职有效、功绩达标。
        ///
        /// 【注意】这里**不判"该官职是否已有人"** —— 槽位模型允许顶替：
        /// 把武将放到已有人的官职上，原任职者会被挤回右侧空闲列表
        /// （对应 `slot_[rank_id].person = person` 的直接赋值，s11_sys_ui_rank_dialog.cpp:301/313/325/337）。
        /// 唯一性由"一个槽只存一个人"保证，不靠拒绝任命。
        /// </summary>
        public bool CanAssign(Person person, Official official)
        {
            if (person == null || official == null || official.Id == 0)
                return false;
            if (!person.IsAlive)
                return false;
            if (person.merit < official.RequiredMerit)
                return false;
            if (!official.CheckPerson(person))
                return false;
            return true;
        }

        /// <summary>
        /// 任命：把武将放到官职槽上（只在界面内存里改，点"决定"才写进人物数据）。
        ///
        /// 槽位模型下这里不会出现"一个官职两个人"，只有两种覆盖：
        /// ① 该武将原本占着别的官职 → 先腾空原槽（此人回到空闲列表再入新槽）；
        /// ② 目标官职已有人 → 该人被顶替并回到右侧空闲列表。
        /// </summary>
        public void Assign(Person person, Official official)
        {
            int index = IndexOfOfficial(official);
            if (index < 0 || person == null)
                return;

            int oldIndex = IndexOfPersonSlot(person);
            if (oldIndex >= 0 && oldIndex != index)
            {
                UpgradeOfficial old = upgradeList[oldIndex];
                old.person = null;
                upgradeList[oldIndex] = old;
            }

            UpgradeOfficial slot = upgradeList[index];
            if (slot.person != null && slot.person != person)
                AddToTargetList(slot.person);   // 被顶替者回到右表

            slot.person = person;
            upgradeList[index] = slot;

            targetList.Remove(person);          // 入册者离开右表
            SortTargetList();
        }

        /// <summary>
        /// 免职：清空该官职的任职者（对应原版"免职"按钮把 rank 设为 RankId_None）。
        /// 被免的人回到右侧空闲列表。
        /// </summary>
        public void RemoveOfficial(Official official)
        {
            int index = IndexOfOfficial(official);
            if (index < 0)
                return;

            UpgradeOfficial slot = upgradeList[index];
            if (slot.person == null)
                return;

            Person removed = slot.person;
            slot.person = null;
            upgradeList[index] = slot;

            AddToTargetList(removed);
            SortTargetList();
        }

        /// <summary>
        /// 自动设定。
        ///
        /// 对齐 <c>RankDialog::auto_fill</c>（s11_sys_ui_rank_dialog.cpp:264-343）：
        /// ① 所有槽位清空；② 候选按功绩降序、同功绩按 Id 升序（345-361 行）；
        /// ③ 顶级官职（本项目 level 0，对应原版 FirstRanks）先 strict 再非 strict 各排一轮；
        /// ④ 其余官职（原版 SecondRanks）同样两轮。
        ///
        /// 评分按官职类型（<see cref="OfficialRankType"/>）分三套公式，见
        /// <see cref="Official.GetAutoSetScore"/>（原版 193-262 行）。
        /// </summary>
        public void AutoSet()
        {
            // ① 清空所有槽（原版 269-273 行：slot_[i].person = nullptr）
            for (int i = 0; i < upgradeList.Count; i++)
            {
                UpgradeOfficial slot = upgradeList[i];
                slot.person = null;
                upgradeList[i] = slot;
            }

            // ② 候选 = 全部可任免武将（此时槽已全空，等价于原版把 person_list_ 全量投进去），
            //    排序：功绩降序，同功绩按 Id 升序
            List<Person> candidates = new List<Person>(allPersons);
            candidates.Sort(CompareByMeritThenId);

            // ③④ 顶级官职两轮 + 其余官职两轮
            AssignRange(candidates, true, true);
            AssignRange(candidates, true, false);
            AssignRange(candidates, false, true);
            AssignRange(candidates, false, false);

            // 剩余没排上的候选人自动成为右侧空闲武将
            RebuildTargetList();
        }

        /// <summary>候选排序：功绩降序，同功绩按 Id 升序（对齐 s11_sys_ui_rank_dialog.cpp:355-361）</summary>
        static int CompareByMeritThenId(Person a, Person b)
        {
            if (a == b) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            if (a.merit != b.merit)
                return b.merit.CompareTo(a.merit);
            return a.Id.CompareTo(b.Id);
        }

        /// <summary>
        /// 给一批官职分配人选。顶级官职 = level 0。
        /// </summary>
        void AssignRange(List<Person> candidates, bool firstRank, bool strict)
        {
            if (candidates.Count == 0)
                return;

            for (int i = 0; i < upgradeList.Count; i++)
            {
                UpgradeOfficial slot = upgradeList[i];
                if (slot.person != null || slot.official == null)
                    continue;

                bool isFirst = slot.official.level == 0;
                if (isFirst != firstRank)
                    continue;

                Person best = FindBestRanker(candidates, slot.official, strict);
                if (best == null)
                    continue;

                slot.person = best;
                upgradeList[i] = slot;
                candidates.Remove(best);
            }
        }

        /// <summary>
        /// 在候选里挑最适合该官职的人（对齐 <c>RankDialog::find_best_ranker</c>，s11_sys_ui_rank_dialog.cpp:193-262）。
        /// 评分按官职类型（<see cref="OfficialRankType"/>）分三套公式，具体实现见
        /// <see cref="Official.GetAutoSetScore"/>；忠诚 &lt; 90 的人不参与（原版 203 行）。
        /// </summary>
        Person FindBestRanker(List<Person> candidates, Official official, bool strict)
        {
            Person best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Person person = candidates[i];
                if (person == null)
                    continue;

                int score = Official.GetAutoSetScore(person, official, strict);
                if (bestScore < score)
                {
                    bestScore = score;
                    best = person;
                }
            }

            return best;
        }

        /// <summary>全解除：所有官职清空（原版的"全解除"按钮），所有人回到右侧空闲列表</summary>
        public void ClearAll()
        {
            for (int i = 0; i < upgradeList.Count; i++)
            {
                UpgradeOfficial slot = upgradeList[i];
                slot.person = null;
                upgradeList[i] = slot;
            }
            RebuildTargetList();
        }

        /// <summary>
        /// 恢复原状：还原到打开界面时的任职情况。
        /// 右表不需要单独还原 —— 槽位复原后 <see cref="RebuildTargetList"/> 会把
        /// "当时有官职的人"重新挡在右表之外、"当时空闲的人"重新放回右表。
        /// </summary>
        public void Restore()
        {
            if (originPersons.Count != upgradeList.Count)
                return;

            for (int i = 0; i < upgradeList.Count; i++)
            {
                UpgradeOfficial slot = upgradeList[i];
                slot.person = originPersons[i];
                upgradeList[i] = slot;
            }
            RebuildTargetList();
        }

        /// <summary>界面是否有未提交的改动（决定按钮的可点状态）</summary>
        public bool HasChanged()
        {
            if (originPersons.Count != upgradeList.Count)
                return true;

            for (int i = 0; i < upgradeList.Count; i++)
            {
                if (originPersons[i] != upgradeList[i].person)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 提交任免。
        ///
        /// 严格对齐 <c>PersonOrderManager::exec_ranks_command</c>（s11_sys_person_order_kunshu.cpp:642-695）：
        /// ① 收集本势力可任免武将（652-662 行）；
        /// ② 把他们的官职**全部清空**（663-667 行）；
        /// ③ 再按槽位逐个设置（668-672 行）。
        /// 这个"先全清后全设"的顺序是唯一性的最终保证 —— 即使旧存档里存在同一官职两人任职的脏数据，
        /// 提交一次也会被收敛成一人。
        ///
        /// 【未对齐】原版 673-684 行还会重算全部太守（building_appoint_taishu）与都督（district_appoint_totoku），
        /// 因为原版官职会影响指挥值从而影响任职资格；本项目的太守/都督委任不依赖官职，故不重算。
        /// </summary>
        public override void DoJob()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Officials == null)
            {
                Done();
                return;
            }

            Official none = scenario.CommonData.Officials.Get(0);
            if (none == null)
            {
                Done();
                return;
            }

            // ① 收集：右表候选 + 槽内任职者 + 本势力**所有现任官职者**。
            // 最后一项不能省：右表做过功绩过滤（见 BuildAllPersons），功绩不够的在任者既不在右表，
            // 被顶替后 AddToTargetList 又会拒绝把他放回右表 —— 只按前两项收集就会漏清他的官职，
            // 提交后同一官职出现两名任职者，正是"先全清后全设"要杜绝的脏数据。
            List<Person> all = new List<Person>(targetList);
            HashSet<Person> seen = new HashSet<Person>(all);

            for (int i = 0; i < upgradeList.Count; i++)
            {
                Person person = upgradeList[i].person;
                if (person != null && seen.Add(person))
                    all.Add(person);
            }

            Force force = TargetCity != null ? TargetCity.BelongForce : null;
            if (force != null)
            {
                for (int i = 0; i < scenario.personSet.Count; i++)
                {
                    Person person = scenario.personSet[i];
                    if (person == null || !person.IsAlive)
                        continue;
                    if (person.BelongForce != force)
                        continue;

                    Official current = person.Official;
                    if (current == null || current.Id == 0)
                        continue;

                    if (seen.Add(person))
                        all.Add(person);
                }
            }

            // ② 先全部清空（清空前记下每人的原官职 —— 判断"是否提升"必须靠它）
            Dictionary<Person, Official> oldOfficials = new Dictionary<Person, Official>(all.Count);
            for (int i = 0; i < all.Count; i++)
                oldOfficials[all[i]] = all[i].Official;

            for (int i = 0; i < all.Count; i++)
                all[i].SetOfficial(none, true);

            // ③ 再按槽设置
            List<Person> promoted = new List<Person>();
            // 只给"官职确实提升"的人播受封台词。
            // 官职没变 / 降级 / 平级换岗都不弹 —— 后期多线作战会频繁微调带兵数，
            // 一次提交几十个人同时冒对话框非常烦（需求：只有提升才有反馈）。
            List<Person> needSay = new List<Person>();
            for (int i = 0; i < upgradeList.Count; i++)
            {
                Person person = upgradeList[i].person;
                Official official = upgradeList[i].official;
                if (person == null || official == null)
                    continue;

                Official oldOfficial;
                oldOfficials.TryGetValue(person, out oldOfficial);

                person.SetOfficial(official, true);
                promoted.Add(person);
                if (IsOfficialPromoted(oldOfficial, official))
                    needSay.Add(person);
                Sango.Log.Info($"@个人@{person.Name}被任命为[{official.Name}]");
            }

            // ④ 演出（原版 S_MCD_RANK_RESULT + play_se(6)，s11_sys_person_order_kunshu.cpp:685-692）
            string[] says = new string[]{
                "主公知遇之恩，某虽肝脑涂地，不能报也！",
                "某愿为主公前驱，赴汤蹈火，在所不辞！",
                "主公在上，末将愿以此身，效犬马之劳，万死不辞！",
                "某誓死追随主公，若有二心，天诛地灭！"
            };

            // 忠诚：与原版一致，所有被任命者都加（本次只收窄对话演出，不改数值）
            for (int i = 0; i < promoted.Count; i++)
            {
                // 忠诚走 AddLoyalty：裸 `loyalty +=` 在内部值域放宽到 255 之后会突破上限
                // （原版 person_add_loyalty 也是 clamp 0..max_person_loyalty_）。
                promoted[i].AddLoyalty(10);
            }

            // 台词：只给官职提升的人播
            for (int i = 0; i < needSay.Count; i++)
            {
                Render.RenderEvent.Instance.Add(new Render.CityPersonSayEvent()
                {
                    person = needSay[i],
                    words = says[GameRandom.Range(says.Length)]
                });
            }

            Done();
            GameMedia.Instance.PlayDoAcitonSfx();
        }

        /// <summary>
        /// 判断这次任免对某个人来说是不是"官职提升"，用于决定要不要播受封台词。
        ///
        /// 【品级口径】<see cref="Official.level"/> 数字越小官越大
        /// （<see cref="BuildOfficialSlots"/> 按 level 升序排"官大 → 官小"，
        /// <see cref="Official.NextOfficials"/> 也用 level - 1 表示更高一级）。
        ///
        /// 【结果】
        /// <list type="bullet">
        /// <item>原官职为空（null / Id == 0）→ 首次受封，算提升；</item>
        /// <item>新旧是同一个官职 Id → 没有变化，不算；</item>
        /// <item>新官职 level 更小 → 品级更高，算提升；</item>
        /// <item>其余（降级 / 平级换岗 / 新官职无效）→ 不算。</item>
        /// </list>
        /// </summary>
        /// <param name="oldOfficial">本次提交前该人的官职（可能为 null 或 Id 0 的"无"）</param>
        /// <param name="newOfficial">本次分配到槽位上的官职</param>
        static bool IsOfficialPromoted(Official oldOfficial, Official newOfficial)
        {
            if (newOfficial == null || newOfficial.Id == 0)
                return false;
            if (oldOfficial == null || oldOfficial.Id == 0)
                return true;
            if (oldOfficial.Id == newOfficial.Id)
                return false;
            return newOfficial.level < oldOfficial.level;
        }
    }
}
