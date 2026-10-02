using Sango.UI;
using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 都市军事「出征」Job 的玩家侧入口系统：注册「军事/出征」菜单、定义选将界面的列（默认按统率倒序）、
    /// 维护目标部队的兵种与兵力配置并派发执行。部队属性与行军的实际结算不在本类，统一委托 Troop 与 City 侧逻辑。
    /// </summary>
    [GameSystem]
    public class CityExpedition : CityBaseSystem
    {
        /// <summary>
        /// 「统率」列在 customTitleList 中的下标，供选将界面打开时指定默认排序列。
        /// 调整出征列顺序时必须同步本值，否则默认排序会落到错误的列上。
        /// </summary>
        public const int CommandSortTitleIndex = 3;

        public List<TroopType> ActivedLandTroopTypes = new List<TroopType>();
        public List<TroopType> ActivedWaterTroopTypes = new List<TroopType>();

        public int CurSelectLandTrropTypeIndex { get; set; }
        public int CurSelectWaterTrropTypeIndex { get; set; }

        public Troop TargetTroop { get; set; }

        public CityExpedition()
        {
            customTitleName = "出征";
            // 统率列使用出征专用副本（倒序），配合 CommandSortTitleIndex 使选将界面打开时统率最高者在前。
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByTroopsLimit,
                CreateExpeditionCommandSortTitle(),
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
                PersonSortFunction.SortBySpearLv,
                PersonSortFunction.SortByHalberdLv,
                PersonSortFunction.SortByCrossbowLv,
                PersonSortFunction.SortByRideLv,
                PersonSortFunction.SortByWaterLv,
                PersonSortFunction.SortByMachineLv,
                PersonSortFunction.SortByFeatureList,
            };
            customMenuName = "军事/出征";
            customMenuOrder = 1100;
            windowName = "window_city_create_troop";
        }

        /// <summary>创建出征专用统率列：按统率倒序，使统率最高的武将排在选将列表前部。</summary>
        private static PersonSortFunction.SortTitle CreateExpeditionCommandSortTitle()
        {
            // 取共享静态列的副本再覆写比较器，避免把其它界面的统率列默认排序方向一起改成倒序。
            PersonSortFunction.SortTitle sortTitle = PersonSortFunction.SortByCommand.Copy();
            sortTitle.valueSortFunc = (a, b) => b.Command.CompareTo(a.Command);
            return sortTitle;
        }

        protected override bool MenuCanShow()
        {
            return true;
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.troops > 0 && TargetCity.food > 0 && TargetCity.freePersons.Count > 0 &&
                    TargetCity.BelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.MakeTroop);
            }
        }
        public override void UpdateJobValue()
        {
            if (personList.Count == 0) return;

            TargetTroop.Leader = personList[0];

            if (personList.Count > 1)
                TargetTroop.Member1 = personList[1];
            else
                TargetTroop.Member1 = null;
            if (personList.Count > 2)
                TargetTroop.Member2 = personList[2];
            else
                TargetTroop.Member2 = null;

            TargetTroop.LandTroopType = ActivedLandTroopTypes[CurSelectLandTrropTypeIndex];
            TargetTroop.WaterTroopType = ActivedWaterTroopTypes[CurSelectWaterTrropTypeIndex];

            TargetTroop.CalculateMaxTroops();

            if (TargetTroop.troops > TargetTroop.MaxTroops)
                TargetTroop.troops = TargetTroop.MaxTroops;

            int troop = TargetTroop.troops;
            troop = Math.Min(troop, TargetCity.troops);
            troop = TargetCity.itemStore.CheckCostMin(TargetTroop.LandTroopType.costItems, troop);
            troop = TargetCity.itemStore.CheckCostMin(TargetTroop.WaterTroopType.costItems, troop);

            TargetTroop.troops = troop;

            TargetTroop.CalculateAttribute(Scenario.Cur);
        }

        public override void OnEnter()
        {
            personList.Clear();
            TargetTroop = new Troop();
            List<TroopType> activeTroopTypes = new List<TroopType>();
            TroopType.CheckActivTroopTypeList(TargetCity.freePersons, activeTroopTypes);

            ActivedLandTroopTypes.Clear();
            ActivedLandTroopTypes.AddRange(activeTroopTypes.FindAll(x => x.isLand));

            ActivedWaterTroopTypes.Clear();
            ActivedWaterTroopTypes.AddRange(activeTroopTypes.FindAll(x => !x.isLand));

            CurSelectLandTrropTypeIndex = 0;
            CurSelectWaterTrropTypeIndex = 0;

            TargetTroop.LandTroopType = ActivedLandTroopTypes[CurSelectLandTrropTypeIndex];
            TargetTroop.WaterTroopType = ActivedWaterTroopTypes[CurSelectWaterTrropTypeIndex];

            TargetTroop.morale = TargetCity.morale;
            //TargetTroop.MaxMorale = TargetCity.MaxMorale;
            TargetTroop.energy = TargetCity.energy;
            if (TargetTroop.troops == 0)
            {
                TargetTroop.troops = 1;
                TargetTroop.MaxTroops = 5000;
            }

            UpdateJobValue();
            RefreshTeamCandidates();          // 推荐队伍面板的候选（可用 / 人员被占用）

            Window.Instance.Open(windowName);
        }

        public override void DoJob()
        {
            if (TargetTroop.troops <= 0) return;
            if (TargetTroop.food <= 0) return;
            if (personList.Count == 0) return;

            ContextMenu.CloseAll();
            TargetTroop.ActionOver = false;
            TargetTroop.IsAlive = true;

            TargetTroop.LandTroopType.Cost(TargetCity, TargetTroop.troops);
            TargetTroop.WaterTroopType.Cost(TargetCity, TargetTroop.troops);
            TargetCity.troops -= TargetTroop.troops;
            TargetCity.food -= TargetTroop.food;
            TargetCity.gold -= TargetTroop.gold;

            TargetTroop.ForEachPerson(person =>
            {
                TargetCity.freePersons.Remove(person);
            });
            TargetCity.Render?.UpdateRender();
            TargetTroop.BelongCorps.ReduceActionPoint(JobType.GetJobCostAP((int)CityJobType.MakeTroop));
            TargetCity.EnsureTroop(TargetTroop, Scenario.Cur);
            Window.Instance.SetVisible(windowName, false);
            GameSystem.GetSystem<TroopSystem>().Start(TargetTroop);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            base.OnBack(whoGone);
            if (whoGone is ObjectSelectSystem) return;
            Window.Instance.SetVisible(windowName, true);
            TargetTroop.BelongCorps.ReduceActionPoint(-JobType.GetJobCostAP((int)CityJobType.MakeTroop));
            TargetTroop.EnterCity(TargetCity);
            TargetTroop.ForEachPerson(person =>
            {
                TargetCity.freePersons.Add(person);
                person.ActionOver = false;
            });
        }

        public void AutoMakeTroop(int troopTypeKind)
        {
            List<TroopType> troopTypes = ActivedLandTroopTypes.FindAll(x => x.kind == troopTypeKind);
            TroopType targetTroopType = troopTypes[troopTypes.Count - 1];
            CurSelectLandTrropTypeIndex = ActivedLandTroopTypes.FindIndex(x => x == targetTroopType);
            AutoMakeTroop(targetTroopType);
        }

        public void AutoMakeTroop(TroopType troopType)
        {
            personList.Clear();
            Person[] people = ForceAI.CounsellorRecommendMakeTroop(TargetCity.freePersons, troopType, 3, TargetCity);
            if (people == null || people.Length == 0)
                return;
            for (int i = 0; i < people.Length; i++)
            {
                Person person = people[i];
                if (person == null) continue;
                personList.Add(person);
            }

            TargetTroop.LandTroopType = troopType;
            TargetTroop.WaterTroopType = ActivedWaterTroopTypes[CurSelectWaterTrropTypeIndex];

            TargetTroop.Leader = personList[0];

            if (personList.Count > 1) TargetTroop.Member1 = personList[1];
            if (personList.Count > 2) TargetTroop.Member2 = personList[2];

            TargetTroop.CalculateMaxTroops();
            TargetTroop.CalculateAttribute(Scenario.Cur);

            SetTroops(TargetTroop.MaxTroops);
        }

        /// <summary>
        /// 自动编队：按 AI 出征的思路自动挑武将，并把兵种切到推荐 / 适性最高的一个。
        ///   ① 本城能立刻凑齐一支推荐队伍（含"最低兵力"门槛判定）→ 直接用它的成员与推荐兵种；
        ///   ② 没有可用推荐队伍 → 取军事能力最高的武将，选其兵种适性最高的可造兵种，再按顾问推荐挑人。
        /// </summary>
        /// <returns>false = 本城没有可用武将 / 没有可组建的兵种</returns>
        public bool AutoMakeFormation()
        {
            if (TargetCity == null || TargetCity.freePersons == null || TargetCity.freePersons.Count == 0)
                return false;

            // ① 推荐队伍优先（与 CityAI.AIMakeTroop 同一套判定口径）
            TroopTeamCandidate teamPick;
            if (TroopTeamService.TryPickForAI(TargetCity, ActiveTroopTypes(),
                    CityEstablishment.ResolveRing(TargetCity), out teamPick)
                && ApplyTeam(teamPick.team))
            {
                return true;
            }

            // ② 兜底：按适性选兵种，再按顾问推荐挑人
            TroopType bestTroopType = PickBestTroopType();
            if (bestTroopType == null)
                return false;

            // 同步"当前选中兵种"下标，避免后续 UpdateJobValue 又把兵种改回原来的
            int index = ActivedLandTroopTypes.FindIndex(x => x == bestTroopType);
            if (index >= 0)
                CurSelectLandTrropTypeIndex = index;

            AutoMakeTroop(bestTroopType);
            return personList.Count > 0;
        }

        /// <summary>
        /// 自动编队的兜底选兵种：取军事能力最高的空闲武将，选其兵种适性最高的一个
        /// （并列时取 id 小的基础兵种），与 <c>CityAI.AIMakeTroop</c> 的兜底口径一致。
        /// </summary>
        private TroopType PickBestTroopType()
        {
            List<TroopType> candidates = ActivedLandTroopTypes;
            if (candidates == null || candidates.Count == 0)
                candidates = ActivedWaterTroopTypes;
            if (candidates == null || candidates.Count == 0)
                return null;

            // 主将候选：军事能力最高的空闲武将（不改动 freePersons 的顺序）
            Person person = null;
            for (int i = 0; i < TargetCity.freePersons.Count; i++)
            {
                Person p = TargetCity.freePersons[i];
                if (p == null || p.IsDead || p.IsPrisoner) continue;
                if (person == null || p.MilitaryAbility > person.MilitaryAbility)
                    person = p;
            }
            if (person == null)
                return null;

            TroopType best = null;
            int bestLevel = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                TroopType troopType = candidates[i];
                if (troopType == null) continue;

                int level = Troop.CheckTroopTypeLevel(troopType, person);
                if (level > bestLevel || (level == bestLevel && best != null && troopType.Id < best.Id))
                {
                    bestLevel = level;
                    best = troopType;
                }
            }
            return best != null ? best : candidates[0];
        }

        // ==================== 推荐队伍 / 我的队伍（出征界面的子面板 · 逻辑层） ====================
        // 界面只负责显示与点击：候选列表来自 teamCandidates，列表项成员用 UIPersonItem.SetPerson 展示。

        /// <summary>面板候选：可用（freePersons 满足）+ 人员满足但不可用（allPersons 满足）。</summary>
        public List<TroopTeamCandidate> teamCandidates = new List<TroopTeamCandidate>();

        /// <summary>面板当前选中的候选下标（-1 = 未选）。</summary>
        public int selectedTeamIndex = -1;

        /// <summary>当前选中的候选（未选 / 越界返回 null）。</summary>
        public TroopTeamCandidate SelectedTeamCandidate
        {
            get
            {
                if (selectedTeamIndex < 0 || selectedTeamIndex >= teamCandidates.Count)
                    return null;
                return teamCandidates[selectedTeamIndex];
            }
        }

        /// <summary>当前可造兵种（陆 + 水），用于解析队伍的推荐兵种。</summary>
        public List<TroopType> ActiveTroopTypes()
        {
            List<TroopType> all = new List<TroopType>();
            if (ActivedLandTroopTypes != null) all.AddRange(ActivedLandTroopTypes);
            if (ActivedWaterTroopTypes != null) all.AddRange(ActivedWaterTroopTypes);
            return all;
        }

        /// <summary>当前界面选中的兵种（陆优先，与 <see cref="UpdateJobValue"/> 口径一致）。</summary>
        public TroopType CurrentTroopType()
        {
            if (ActivedLandTroopTypes != null && ActivedLandTroopTypes.Count > 0)
            {
                int i = Math.Min(Math.Max(CurSelectLandTrropTypeIndex, 0), ActivedLandTroopTypes.Count - 1);
                return ActivedLandTroopTypes[i];
            }
            if (ActivedWaterTroopTypes != null && ActivedWaterTroopTypes.Count > 0)
            {
                int i = Math.Min(Math.Max(CurSelectWaterTrropTypeIndex, 0), ActivedWaterTroopTypes.Count - 1);
                return ActivedWaterTroopTypes[i];
            }
            return null;
        }

        /// <summary>刷新候选列表（打开面板 / 编队变化后调用）。</summary>
        public void RefreshTeamCandidates()
        {
            teamCandidates = TroopTeamService.BuildList(TargetCity, ActiveTroopTypes());
            if (selectedTeamIndex >= teamCandidates.Count)
                selectedTeamIndex = -1;
        }

        /// <summary>
        /// 套用一支队伍：成员填进编队、兵种切成队伍推荐兵种，再走既有流程重算兵力与属性。
        /// </summary>
        /// <returns>false = 这支队伍现在用不了（人不在本城 / 被占用 / 兵种造不出）</returns>
        public bool ApplyTeam(TroopTeam team)
        {
            if (team == null)
                return false;

            TroopType troopType;
            List<Person> members = TroopTeamService.MatchMembers(team, TargetCity.freePersons,
                ActiveTroopTypes(), out troopType);
            if (members == null || members.Count == 0)
                return false;

            personList.Clear();
            personList.AddRange(members);

            if (troopType != null)
            {
                if (troopType.isLand)
                {
                    int idx = ActivedLandTroopTypes.FindIndex(x => x == troopType);
                    if (idx >= 0) CurSelectLandTrropTypeIndex = idx;
                }
                else
                {
                    int idx = ActivedWaterTroopTypes.FindIndex(x => x == troopType);
                    if (idx >= 0) CurSelectWaterTrropTypeIndex = idx;
                }
            }

            UpdateJobValue();
            return true;
        }

        /// <summary>最近一次保存的结果（界面据此提示"已新增 / 已更新"）。</summary>
        public TroopTeamSaveResult lastSaveResult = TroopTeamSaveResult.Failed;

        /// <summary>
        /// 保存当前编队（界面"新增 / 保存"都走这里）：
        ///   ① 已有**同名**队伍 → 更新它的信息；
        ///   ② 没有同名、但有**同样内容**的队伍（同兵种 + 同一批武将 / 同一组特技）→ 更新那一条并保留其名字；
        ///   ③ 都没有 → 新增。
        /// 之后刷新面板并选中被保存的那一条；结果见 <see cref="lastSaveResult"/>。
        /// </summary>
        public TroopTeam AddCurrentAsTeam(string name = null)
        {
            lastSaveResult = TroopTeamSaveResult.Failed;
            if (personList.Count == 0)
                return null;

            TroopTeam team = TroopTeamService.CaptureFrom(personList, CurrentTroopType(), name);
            TroopTeamSaveResult result;
            int index = CustomTroopTeams.SaveTeam(team, out result);
            lastSaveResult = result;
            if (index < 0)
                return null;

            RefreshTeamCandidates();

            // 注意：面板排序（可用优先）与队伍库顺序不同，选中项要按名字重新定位
            TroopTeam saved = CustomTroopTeams.Get(index);
            selectedTeamIndex = FindTeamIndex(saved != null ? saved.name : team.name);
            return saved != null ? saved : team;
        }

        /// <summary>
        /// 快捷保存当前编队（出征界面 bg/save 用）：已存在**完全相同的武将组合**时不再重复保存。
        /// 队伍按武将 ID 保存（<see cref="TroopTeamService.CaptureFrom"/> → memberPersonIds）。
        /// </summary>
        /// <param name="created">输出：true = 新建了一支队伍，false = 命中了已有的同组合队伍</param>
        /// <returns>命中的队伍；编队为空 / 超上限时返回 null</returns>
        public TroopTeam QuickSaveCurrentTeam(out bool created)
        {
            created = false;
            if (personList.Count == 0)
                return null;

            // 去重：已有相同武将组合的队伍就直接用它，不再新增
            TroopTeam exist = FindTeamWithSameMembers();
            if (exist != null)
            {
                selectedTeamIndex = FindTeamIndex(exist.name);
                return exist;
            }

            TroopTeam team = AddCurrentAsTeam();
            created = team != null;
            return team;
        }

        /// <summary>
        /// 按武将 ID 组合（与顺序无关）找一支已存在的队伍（只查"我的队伍"），没有返回 null。
        /// </summary>
        public TroopTeam FindTeamWithSameMembers()
        {
            if (personList.Count == 0)
                return null;

            for (int i = 0; i < CustomTroopTeams.teams.Count; i++)
            {
                TroopTeam team = CustomTroopTeams.teams[i];
                if (IsSameMembers(team, personList))
                    return team;
            }
            return null;
        }

        /// <summary>队伍的固定武将与给定编队是否为同一组人（只比 id，与顺序无关）。</summary>
        private static bool IsSameMembers(TroopTeam team, List<Person> members)
        {
            if (team == null || members == null)
                return false;

            int[] ids = team.FixedMemberIds;
            if (ids.Length != members.Count)
                return false;

            for (int i = 0; i < members.Count; i++)
            {
                Person person = members[i];
                int id = person != null ? person.Id : 0;
                bool find = false;
                for (int j = 0; j < ids.Length; j++)
                {
                    if (ids[j] == id)
                    {
                        find = true;
                        break;
                    }
                }
                if (!find)
                    return false;
            }
            return true;
        }

        /// <summary>保存：把当前编队覆盖到选中的队伍（选中的是推荐模板时 = 另存为同名"我的队伍"）。</summary>
        public bool SaveCurrentToSelected()
        {
            TroopTeamCandidate sel = SelectedTeamCandidate;
            if (sel == null || sel.team == null)
                return AddCurrentAsTeam() != null;          // 没选中的按"新增"处理
            return AddCurrentAsTeam(sel.name) != null;
        }

        /// <summary>删除：删掉选中的"我的队伍"（推荐模板只读，删不了）。</summary>
        public bool DeleteSelectedTeam()
        {
            TroopTeamCandidate sel = SelectedTeamCandidate;
            if (sel == null || !sel.custom || sel.team == null)
                return false;

            bool ok = CustomTroopTeams.Remove(sel.team.name);
            if (ok)
            {
                RefreshTeamCandidates();
                selectedTeamIndex = -1;
            }
            return ok;
        }

        /// <summary>在候选列表里按名字找下标（找不到 -1）。</summary>
        public int FindTeamIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;
            for (int i = 0; i < teamCandidates.Count; i++)
            {
                if (teamCandidates[i] != null && teamCandidates[i].team != null
                    && teamCandidates[i].team.name == name)
                    return i;
            }
            return -1;
        }

        public void AutoMakeBuildTroop()
        {
            personList.Clear();
            Scenario scenario = Scenario.Cur;
            BuildingType buildingType = scenario.GetObject<BuildingType>(31);
            Person[] people = ForceAI.CounsellorRecommendBuild(TargetCity.freePersons, buildingType);
            if (people == null || people.Length == 0)
                return;
            for (int i = 0; i < people.Length; i++)
            {
                Person person = people[i];
                if (person == null) continue;
                personList.Add(person);
            }

            TargetTroop.LandTroopType = scenario.GetObject<TroopType>(1); ;
            TargetTroop.WaterTroopType = ActivedWaterTroopTypes[CurSelectWaterTrropTypeIndex];
            CurSelectLandTrropTypeIndex = 0;

            TargetTroop.Leader = personList[0];

            if (personList.Count > 1) TargetTroop.Member1 = personList[1];
            if (personList.Count > 2) TargetTroop.Member2 = personList[2];

            TargetTroop.CalculateMaxTroops();
            TargetTroop.CalculateAttribute(scenario);

            SetTroops(3000);
            TargetTroop.gold = 1000;
        }


        public void SetTroops(int num)
        {
            int maxTroopNum = Math.Min(num, TargetCity.troops);
            maxTroopNum = TargetCity.itemStore.CheckCostMin(TargetTroop.LandTroopType.costItems, maxTroopNum);
            maxTroopNum = TargetCity.itemStore.CheckCostMin(TargetTroop.WaterTroopType.costItems, maxTroopNum);
            int wonderFood = (int)(maxTroopNum * Scenario.Cur.Variables.baseFoodCostInTroop * 20);
            int food = Math.Min(wonderFood, TargetCity.food);

            TargetTroop.troops = maxTroopNum;
            TargetTroop.food = food;
        }

        public void SetWaterType(TroopType troopType)
        {
            TargetTroop.WaterTroopType = troopType;
            if (personList.Count <= 0)
                return;

            TargetTroop.CalculateAttribute(Scenario.Cur);
            SetTroops(TargetTroop.MaxTroops);
        }

        public void SetLandType(TroopType troopType)
        {
            TargetTroop.LandTroopType = troopType;
            if (personList.Count <= 0)
                return;

            TargetTroop.CalculateAttribute(Scenario.Cur);
            SetTroops(TargetTroop.MaxTroops);
        }
    }
}
