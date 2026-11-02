using System.Collections.Generic;
using Sango.Core;
using Sango.UI;

namespace Sango.Core.Player
{
    /// <summary>
    /// 玩家侧"培育武将"指令：进入先弹**二级菜单**（培育能力 / 培育适应 / 培育特技），
    /// 选其中一项才打开 <c>window_city_ability_learn</c>，三个入口共用同一个窗口，只有内容不同。
    ///
    /// <para>
    /// 【为什么走二级菜单】对齐军团指令（<see cref="CorpsSystem"/>）的交互：
    /// `OnEnter` 里自建 <see cref="ContextMenuData"/> 弹菜单，点具体菜单项再打开窗口 / 选择器，
    /// 收尾统一 `Done()`。培育的三条路径在原版是三条独立指令
    /// （LearnBaseStat / LearnHeishuTekisei / LearnSkill，见 s11_sys_person_order_kunshu.cpp），
    /// 本项目合并成一条指令 + <see cref="learnCategory"/> 分类，避免城市菜单里堆三个按钮。
    /// </para>
    ///
    /// <para>
    /// 【界面口径】窗口里铺的是"**本势力已研究完毕**、且属于当前分类的能力"
    /// （<see cref="AbilityStudySystem.GetResearchedAbilities"/>），不是"该武将能学的"——
    /// 玩家要先看到全部可选项（含次数已用完的），再靠节点状态区分能不能点。
    /// "能不能点 / 确定键能不能按"一律由 <see cref="AbilityStudySystem.CanStudy"/> 决定，
    /// 与真正执行的 <see cref="AbilityStudySystem.BeginStudy"/> 同源。
    /// </para>
    /// </summary>
    [GameSystem]
    public class CityAbilityLearn : CityBaseSystem
    {
        /// <summary>当前培育大类（能力 / 适性 / 特技）</summary>
        public AbilityCategory learnCategory { get; private set; } = AbilityCategory.BaseStat;

        /// <summary>当前选中的武将（窗口读）</summary>
        public Person selectedPerson;

        /// <summary>当前选中的能力（窗口读；只有"真的能培育"的那条才会写进来）</summary>
        public Ability selectedAbility;

        /// <summary>窗口要铺的节点数据源：本势力已研究、且属于当前分类的能力（窗口读）</summary>
        public List<Ability> optionList = new List<Ability>();

        /// <summary>当前分类下可培育的武将（窗口"对象武将"选择用；不限于本城，与原版一致）</summary>
        public List<Person> learnablePersons = new List<Person>();

        /// <summary>二级菜单数据（与军团指令同一套交互）</summary>
        ContextMenuData menuData = new ContextMenuData();

        public CityAbilityLearn()
        {
            customTitleName = "培育武将";
            customMenuName = "势力/培育武将";
            // 排在"能力研究"（3980）之后
            customMenuOrder = 3990;
            windowName = "window_city_ability_learn";
        }

        /// <summary>当前分类的标题（窗口标题 / 菜单提示用）</summary>
        public string CategoryTitle => GetCategoryTitle(learnCategory);

        /// <summary>分类 → 中文标题（与二级菜单项文案保持一致）</summary>
        /// <param name="category">能力大类</param>
        /// <returns>「培育能力 / 培育适应 / 培育特技」</returns>
        public static string GetCategoryTitle(AbilityCategory category)
        {
            switch (category)
            {
                case AbilityCategory.TroopAptitude: return "培育适应";
                case AbilityCategory.Skill: return "培育特技";
                default: return "培育能力";
            }
        }

        /// <summary>
        /// 是否可点：势力内至少有一名空闲武将可以培育（三类任一），且资金 / 行动力足够。
        /// 对齐原版 <c>can_learn_base_stat_command</c>（s11_sys_person_order_kunshu.cpp:1671-1680）：
        /// 用 <c>get_learnable_person_list(force, ...)</c> 判空，范围是**全势力**而非单一城池。
        /// </summary>
        public override bool IsValid
        {
            get
            {
                if (TargetCity == null || TargetCity.BelongForce == null)
                    return false;
                if (!TargetCity.BelongForce.IsPlayer)
                    return false;

                Scenario scenario = Scenario.Cur;
                if (scenario == null)
                    return false;

                // 【资金与行动力门禁】
                // 原实现只查"有可培育武将"，点了才在 AbilityStudySystem 里扣金 / 行动力，
                // 不够时弹「资金不足」—— 按钮本应提前置灰，与执行侧同源。
                if (TargetCity.gold < AbilityStudySystem.LearnGoldCost)
                    return false;
                if (TargetCity.BelongCorps != null
                    && TargetCity.BelongCorps.ActionPoint < AbilityStudySystem.LearnActionPointCost)
                    return false;

                CheckAvailability(scenario, out bool hasBase, out bool hasAptitude, out bool hasSkill);
                return hasBase || hasAptitude || hasSkill;
            }
        }

        /// <summary>
        /// 进入指令：先弹二级菜单（培育能力 / 培育适应 / 培育特技 / 返回），**不直接开窗口**。
        /// 交互对齐 <see cref="CorpsSystem.OnEnter"/>。
        /// </summary>
        public override void OnEnter()
        {
            personList.Clear();
            learnablePersons.Clear();
            optionList.Clear();
            selectedPerson = null;
            selectedAbility = null;

            UpdateJobValue();
            BuildMenu();
            ContextMenu.CloseAll();
            ContextMenu.Show(menuData, UnityEngine.Input.mousePosition, ContextMenuType.Other);
        }

        /// <summary>
        /// 离开指令：收起二级菜单并恢复主菜单，同时关掉可能还开着的培育窗口。
        /// </summary>
        public override void OnDestroy()
        {
            ContextMenu.CloseAll();
            ContextMenu.Show(ContextMenuData.MenuData, ContextMenuData.MenuData.startPosition);
            Window.Instance.Close(windowName);
        }

        /// <summary>构建二级菜单（每次进入 / 候选变化后重建，保证可点状态是最新的）</summary>
        void BuildMenu()
        {
            menuData.Clear();
            // 一次遍历算出三类各自有没有候选：菜单要显示三个项，逐项重扫武将池太浪费
            CheckAvailability(Scenario.Cur, out bool hasBase, out bool hasAptitude, out bool hasSkill);
            menuData.Add("培育能力", 10, this, OnClickLearnBaseStat, hasBase);
            menuData.Add("培育适应", 11, this, OnClickLearnAptitude, hasAptitude);
            menuData.Add("培育特技", 12, this, OnClickLearnSkill, hasSkill);
            menuData.Add("返回", 13, this, OnClickReturn, true);
        }

        /// <summary>菜单：返回上一级</summary>
        /// <param name="contextMenuItem">菜单项</param>
        void OnClickReturn(IContextMenuItem contextMenuItem)
        {
            GameSystemManager.Instance.Done();
        }

        /// <summary>菜单：培育能力</summary>
        /// <param name="contextMenuItem">菜单项</param>
        void OnClickLearnBaseStat(IContextMenuItem contextMenuItem)
        {
            StartLearn(AbilityCategory.BaseStat);
        }

        /// <summary>菜单：培育适应</summary>
        /// <param name="contextMenuItem">菜单项</param>
        void OnClickLearnAptitude(IContextMenuItem contextMenuItem)
        {
            StartLearn(AbilityCategory.TroopAptitude);
        }

        /// <summary>菜单：培育特技</summary>
        /// <param name="contextMenuItem">菜单项</param>
        void OnClickLearnSkill(IContextMenuItem contextMenuItem)
        {
            StartLearn(AbilityCategory.Skill);
        }

        /// <summary>
        /// 选定分类 → 收集候选武将 → 打开培育窗口。
        /// 三个入口共用同一个窗口，只由 <see cref="learnCategory"/> 决定内容。
        /// </summary>
        /// <param name="category">能力大类</param>
        void StartLearn(AbilityCategory category)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || TargetCity == null)
                return;

            learnCategory = category;
            customTitleName = GetCategoryTitle(category);

            learnablePersons = CollectLearnablePersons(scenario, category);
            if (learnablePersons.Count == 0)
            {
                PlayerMessage.AddTextMessage(
                    $"{GetCategoryTitle(category)}：暂无可培育的武将（需要对应能力已研究完成，且武将空闲无伤病）。",
                    TargetCity.BelongForce, TargetCity.x, TargetCity.y);
                BuildMenu();
                return;
            }

            SelectPerson(learnablePersons[0]);
            ContextMenu.CloseAll();
            Window.Instance.Open(windowName);
        }

        /// <summary>
        /// 换人：更新选中武将与其可选能力。
        /// 这里铺的是"势力已研究 + 当前分类"的能力，能不能点由 <see cref="AbilityStudySystem.CanStudy"/> 逐条判定。
        /// </summary>
        /// <param name="person">新选中的武将</param>
        public void SelectPerson(Person person)
        {
            selectedPerson = person;
            selectedAbility = null;

            personList.Clear();
            if (person != null)
                personList.Add(person);

            Force force = person != null ? person.BelongForce : (TargetCity != null ? TargetCity.BelongForce : null);
            optionList = AbilityStudySystem.GetResearchedAbilities(force, Scenario.Cur, learnCategory);
        }

        /// <summary>选中一条能力（只允许"真的能培育"的那条）</summary>
        /// <param name="ability">能力</param>
        public void SelectAbility(Ability ability)
        {
            selectedAbility = ability;
        }

        /// <summary>
        /// 是否可以确认（窗口用来控制"确定"键高亮）。
        /// 判据 = 选中的能力此刻真的可执行，与 <see cref="AbilityStudySystem.BeginStudy"/> 同源。
        /// </summary>
        /// <returns>可执行返回 true</returns>
        public bool CanSure()
        {
            if (selectedPerson == null || selectedAbility == null)
                return false;
            return AbilityStudySystem.CanStudy(selectedPerson, selectedAbility, TargetCity, Scenario.Cur,
                out int gain, out string reason);
        }

        /// <summary>
        /// 取某条能力对当前武将"不可培育"的具体原因（窗口展示用）。
        /// </summary>
        /// <param name="ability">能力</param>
        /// <returns>可培育返回 null；否则返回原因文案</returns>
        public string GetStudyBlockReason(Ability ability)
        {
            if (selectedPerson == null || ability == null)
                return "未选择武将";
            AbilityStudySystem.CanStudy(selectedPerson, ability, TargetCity, Scenario.Cur,
                out int gain, out string reason);
            return reason;
        }

        /// <summary>
        /// 当前研习的消耗与耗时（窗口展示用）。
        /// </summary>
        /// <returns>形如「消耗 300 金 · 耗时 3 旬」；金钱消耗为 0（原版默认值）时不显示金额</returns>
        public string GetStudyCostText()
        {
            int gold = AbilityStudySystem.LearnGoldCost;
            string costText = gold > 0 ? $"消耗 {gold} 金" : "不消耗金钱";
            return $"{costText} · 耗时 {AbilityStudySystem.StudyTurns} 旬";
        }

        /// <summary>
        /// 确认执行：完成任务链（<see cref="GameSystem.Done"/>）并派发研习任务。
        /// 判定失败时不结束指令，只提示原因，让玩家重新选。
        /// </summary>
        public override void DoJob()
        {
            if (selectedPerson == null || selectedAbility == null)
            {
                Done();
                return;
            }

            if (AbilityStudySystem.BeginStudy(selectedPerson, selectedAbility, TargetCity, Scenario.Cur))
            {
                Done();
                return;
            }

            int x = TargetCity != null ? TargetCity.x : 0;
            int y = TargetCity != null ? TargetCity.y : 0;
            PlayerMessage.AddTextMessage(
                $"无法开始研习：{GetStudyBlockReason(selectedAbility) ?? "条件不满足"}。",
                TargetCity != null ? TargetCity.BelongForce : null, x, y);
        }

        /// <summary>取消：完成任务链</summary>
        public void CancelJob()
        {
            Done();
        }

        /// <summary>
        /// 一次遍历武将池，算出三个分类各自"有没有可培育的武将"。
        ///
        /// 菜单三个项要同时决定是否可点，逐项重扫武将池会让城市菜单的显示明显变慢，
        /// 因此这里只扫一遍：先把三类已研究能力表取出来，再对每个空闲武将逐类试一遍，命中即置位。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="hasBase">基础能力类是否有候选</param>
        /// <param name="hasAptitude">兵种适性类是否有候选</param>
        /// <param name="hasSkill">特技类是否有候选</param>
        void CheckAvailability(Scenario scenario, out bool hasBase, out bool hasAptitude, out bool hasSkill)
        {
            hasBase = false;
            hasAptitude = false;
            hasSkill = false;

            if (TargetCity == null || TargetCity.BelongForce == null
                || scenario == null || scenario.personSet == null)
                return;

            Force force = TargetCity.BelongForce;
            List<Ability> baseStats = AbilityStudySystem.GetResearchedAbilities(force, scenario, AbilityCategory.BaseStat);
            List<Ability> aptitudes = AbilityStudySystem.GetResearchedAbilities(force, scenario, AbilityCategory.TroopAptitude);
            List<Ability> skills = AbilityStudySystem.GetResearchedAbilities(force, scenario, AbilityCategory.Skill);
            if (baseStats.Count == 0 && aptitudes.Count == 0 && skills.Count == 0)
                return;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.BelongForce != force)
                    continue;
                if (!person.IsFree || !person.IsHealthy)
                    continue;

                if (!hasBase && CanStudyAny(person, baseStats))
                    hasBase = true;
                if (!hasAptitude && CanStudyAny(person, aptitudes))
                    hasAptitude = true;
                if (!hasSkill && CanStudyAny(person, skills))
                    hasSkill = true;

                if (hasBase && hasAptitude && hasSkill)
                    return;
            }
        }

        /// <summary>该武将在给定能力表里是否至少能学一条</summary>
        /// <param name="person">武将</param>
        /// <param name="abilities">已研究的能力表</param>
        /// <returns>至少一条可学返回 true</returns>
        bool CanStudyAny(Person person, List<Ability> abilities)
        {
            for (int i = 0; i < abilities.Count; i++)
            {
                if (AbilityStudySystem.CanStudy(person, abilities[i], TargetCity, Scenario.Cur,
                    out int gain, out string reason))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 单个武将是否可在指定分类下培育：同势力 + 空闲 + 健康 + 至少一条能力真的能学。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="force">目标势力</param>
        /// <param name="abilities">该分类下势力已研究的能力</param>
        /// <returns>可培育返回 true</returns>
        bool IsLearnablePerson(Person person, Force force, List<Ability> abilities)
        {
            if (person == null || !person.IsAlive || person.BelongForce != force)
                return false;
            if (!person.IsFree || !person.IsHealthy)
                return false;

            return CanStudyAny(person, abilities);
        }

        /// <summary>
        /// 势力内可培育的武将（指定分类）：空闲、且至少有一条能力真的能学。
        ///
        /// 对齐出处：<c>PersonOrderManager::get_learnable_person_list</c>
        /// （s11_sys_person_order_kunshu.cpp:1583-1602）—— 遍历**全势力**武将池，
        /// 只按 <c>force_id</c> + <c>can_learn_order</c> 过滤，**不限定在当前城池**。
        /// 因此在下令城 A 也能指派 B 城的武将闭关；资费（金 / 行动力）仍由 A 城与其军团承担，
        /// 与原版"扣 <c>self->building</c> 的钱和所属 district 的行动力"一致。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="category">能力大类</param>
        /// <returns>候选武将列表</returns>
        List<Person> CollectLearnablePersons(Scenario scenario, AbilityCategory category)
        {
            List<Person> result = new List<Person>();
            if (TargetCity == null || TargetCity.BelongForce == null
                || scenario == null || scenario.personSet == null)
                return result;

            Force force = TargetCity.BelongForce;
            List<Ability> abilities = AbilityStudySystem.GetResearchedAbilities(force, scenario, category);
            if (abilities.Count == 0)
                return result;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (!IsLearnablePerson(person, force, abilities))
                    continue;
                result.Add(person);
            }

            // 同据点的人排在一起，便于跨城列表辨认（原版按 person_pool 顺序，未排序）
            result.Sort((a, b) =>
            {
                City ca = a.BelongCity ?? a.CurrentCity;
                City cb = b.BelongCity ?? b.CurrentCity;
                int ia = ca != null ? ca.Id : int.MaxValue;
                int ib = cb != null ? cb.Id : int.MaxValue;
                return ia != ib ? ia.CompareTo(ib) : a.Id.CompareTo(b.Id);
            });
            return result;
        }

        /// <summary>
        /// 基类钩子：进入时不需要预先选人（窗口打开后才选），保留空实现以便基类调用安全。
        /// </summary>
        public override void RecommandPersonList()
        {
            personList.Clear();
        }
    }
}
