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

        /// <summary>本城所有可培育的武将（窗口"换人"用）</summary>
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
        /// 是否可点：本城至少有一名空闲武将存在可研习的能力。
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

                return CollectLearnablePersons(scenario).Count > 0;
            }
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
        /// <returns>形如「消耗 300 金 · 耗时 3 旬」</returns>
        public string GetStudyCostText()
        {
            return $"消耗 {AbilityStudySystem.LearnGoldCost} 金 · 耗时 {AbilityStudySystem.StudyTurns} 旬";
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
                    PlayerMessage.AddTextMessage("无法开始研习：资金不足，或武将当前不空闲。",
                        TargetCity != null ? TargetCity.BelongForce : null, x, y);
                    return;
                }
            }
            Done();
        }

        /// <summary>
        /// 本城可培育的武将：空闲、且至少有一条可研习的能力。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>候选武将列表</returns>
        List<Person> CollectLearnablePersons(Scenario scenario)
        {
            List<Person> result = new List<Person>();
            if (TargetCity == null || TargetCity.freePersons == null || scenario == null)
                return result;

            for (int i = 0; i < TargetCity.freePersons.Count; i++)
            {
                Person person = TargetCity.freePersons[i];
                if (person == null || !person.IsAlive || !person.IsFree)
                    continue;
                if (AbilityStudySystem.GetLearnableAbilitiesFor(person, scenario).Count == 0)
                    continue;
                result.Add(person);
            }
            return result;
        }
    }
}
