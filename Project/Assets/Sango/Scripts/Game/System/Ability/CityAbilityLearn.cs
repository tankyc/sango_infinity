using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 玩家侧"能力培育"指令：选一名武将 → 选一条已掌握的能力 → 闭关研习 3 旬。
    ///
    /// 对齐出处：Open311 `force_learn`（s11_game_ai_force_cmd_learn.cpp:5-35）的**玩家分支** ——
    /// 原版玩家通过城内的能力培育指令逐人指派，AI 走同一套 `LearnBaseStat/LearnTroopAptitude/LearnSkill`。
    ///
    /// 【与"能力研究"的关系】研究解锁能力 → 培育让具体武将学会它。
    ///
    /// 【窗口】<c>window_city_ability_learn</c>（prefab 需美术按 <see cref="UICityAbilityLearn"/>
    /// 的公开字段名创建控件）。窗口侧只负责展示与转发，判定全部在
    /// <see cref="AbilityStudySystem.GetLearnableAbilitiesFor"/> 与
    /// <see cref="AbilityStudySystem.BeginStudy"/> 里。
    /// </summary>
    [GameSystem]
    public class CityAbilityLearn : CityBaseSystem
    {
        /// <summary>当前选中的武将（窗口读）</summary>
        public Person selectedPerson;

        /// <summary>当前选中的能力（窗口读）</summary>
        public Ability selectedAbility;

        /// <summary>该武将当前可研习的能力列表（窗口读）</summary>
        public List<Ability> optionList = new List<Ability>();

        /// <summary>势力内所有可培育的武将（窗口"换人"用；不限于本城，与原版一致）</summary>
        public List<Person> learnablePersons = new List<Person>();

        public CityAbilityLearn()
        {
            customTitleName = "能力培育";
            customMenuName = "势力/能力培育";
            // 排在"能力研究"（3980）之后
            customMenuOrder = 3990;
            windowName = "window_city_ability_learn";
        }

        /// <summary>
        /// 是否可点：势力内至少有一名空闲武将存在可研习的能力。
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

                // 【补金与行动力门禁】
                // 原实现只查"有可培育武将"，点了才在 AbilityStudySystem 里扣金 / 行动力，
                // 不够时弹「资金不足」—— 按钮本应提前置灰，与执行侧同源。
                if (TargetCity.gold < AbilityStudySystem.LearnGoldCost)
                    return false;
                if (TargetCity.BelongCorps != null
                    && TargetCity.BelongCorps.ActionPoint < AbilityStudySystem.LearnActionPointCost)
                    return false;

                return HasLearnablePerson(scenario);
            }
        }

        /// <summary>
        /// 势力内是否至少有一名可培育武将（命中即停，供 <see cref="IsValid"/> 用）。
        ///
        /// 为什么不直接复用 <see cref="CollectLearnablePersons"/>：候选池是全势力，
        /// 全员不可培育时要遍历上千人，而按钮置灰判定只要一个"有/没有"。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>存在返回 true</returns>
        bool HasLearnablePerson(Scenario scenario)
        {
            if (TargetCity == null || TargetCity.BelongForce == null
                || scenario == null || scenario.personSet == null)
                return false;

            Force force = TargetCity.BelongForce;
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                if (IsLearnablePerson(scenario.personSet[i], force, scenario))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 单个武将是否可培育：同势力 + 空闲 + 至少一条可研习能力。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="force">目标势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>可培育返回 true</returns>
        bool IsLearnablePerson(Person person, Force force, Scenario scenario)
        {
            if (person == null || !person.IsAlive || person.BelongForce != force)
                return false;
            if (!person.IsFree)
                return false;
            return AbilityStudySystem.GetLearnableAbilitiesFor(person, scenario).Count > 0;
        }

        /// <summary>
        /// 进入指令（由 <see cref="CityBaseSystem.OnEnter"/> 调用）：
        /// 收集可培育武将，默认选中第一个，并刷新该人的可研习项。
        /// </summary>
        public override void RecommandPersonList()
        {
            personList.Clear();
            learnablePersons.Clear();
            optionList.Clear();
            selectedPerson = null;
            selectedAbility = null;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            learnablePersons = CollectLearnablePersons(scenario);
            if (learnablePersons.Count == 0)
                return;

            SelectPerson(learnablePersons[0]);
        }

        /// <summary>
        /// 换人：更新选中武将与其可研习项。
        /// </summary>
        /// <param name="person">新选中的武将</param>
        public void SelectPerson(Person person)
        {
            if (person == null || Scenario.Cur == null)
                return;

            selectedPerson = person;
            selectedAbility = null;
            personList.Clear();
            personList.Add(person);
            optionList = AbilityStudySystem.GetLearnableAbilitiesFor(person, Scenario.Cur);
        }

        /// <summary>
        /// 选中一条能力。
        /// </summary>
        /// <param name="ability">能力</param>
        public void SelectAbility(Ability ability)
        {
            selectedAbility = ability;
        }

        /// <summary>
        /// 是否可以确认（窗口用来控制按钮置灰）。
        /// </summary>
        /// <returns>武将与能力都已选中返回 true</returns>
        public bool CanSure()
        {
            return selectedPerson != null && selectedAbility != null;
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
        /// 确认执行：发起研习任务并结束指令。
        /// </summary>
        public override void DoJob()
        {
            if (selectedPerson != null && selectedAbility != null && Scenario.Cur != null)
            {
                if (!AbilityStudySystem.BeginStudy(selectedPerson, selectedAbility, TargetCity, Scenario.Cur))
                {
                    int x = TargetCity != null ? TargetCity.x : 0;
                    int y = TargetCity != null ? TargetCity.y : 0;
                    PlayerMessage.AddTextMessage("无法开始研习：资金/行动力不足、武将当前不空闲，或该能力数据已失效。",
                        TargetCity != null ? TargetCity.BelongForce : null, x, y);
                    return;
                }
            }
            Done();
        }

        /// <summary>
        /// 势力内可培育的武将：空闲、且至少有一条可研习的能力。
        ///
        /// 对齐出处：<c>PersonOrderManager::get_learnable_person_list</c>
        /// （s11_sys_person_order_kunshu.cpp:1583-1602）—— 遍历**全势力**武将池，
        /// 只按 <c>force_id</c> + <c>can_learn_order</c> 过滤，**不限定在当前城池**。
        /// 因此在下令城 A 也能指派 B 城的武将闭关；资费（金 / 行动力）仍由 A 城与其军团承担，
        /// 与原版"扣 <c>self->building</c> 的钱和所属 district 的行动力"一致。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>候选武将列表</returns>
        List<Person> CollectLearnablePersons(Scenario scenario)
        {
            List<Person> result = new List<Person>();
            if (TargetCity == null || TargetCity.BelongForce == null
                || scenario == null || scenario.personSet == null)
                return result;

            Force force = TargetCity.BelongForce;
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (!IsLearnablePerson(person, force, scenario))
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
    }
}
