using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 玩家侧"能力研究"指令：选一条可研究的能力 → 扣金与行动力 → 开始若干旬研究。
    ///
    /// 对齐出处：Open311 `force_research_ability`（s11_game_ai_force_cmd.cpp:754-817）的**玩家分支** ——
    /// 原版玩家通过独立的"能力研究"指令选一条 Ability 开始研究，AI 走同一套接口自动挑。
    ///
    /// 【窗口】<c>window_city_ability_research</c>（<see cref="UICityAbilityResearch"/>）：
    /// 以能力路径图展示全部条目，区分"已研究 / 可研究 / 前置未满足"三态。
    /// 早期版本调通用列表选择器（<see cref="AbilitySelectSystem"/>），
    /// 但能力表 94/98 条带前置、61 条前置跨分类，列表看不出路径，已替换为图。
    ///
    /// 【与"能力培育"的关系】研究是势力级解锁（本类）→ 培育让具体武将学会
    /// （<see cref="CityAbilityLearn"/>）。
    /// </summary>
    [GameSystem]
    public class CityAbilityResearch : CityBaseSystem
    {
        /// <summary>当前选中的能力（窗口读）</summary>
        public Ability selectedAbility;

        /// <summary>当前可研究的能力列表（窗口读，来自 <see cref="AbilityStudySystem.GetResearchableAbilities"/>）</summary>
        public List<Ability> optionList = new List<Ability>();

        public CityAbilityResearch()
        {
            customTitleName = "能力研究";
            customMenuName = "势力/能力研究";
            // 排在"计略"组（3950~3970）之后、"君主"组（4000+）之前
            customMenuOrder = 3980;
            windowName = "window_city_ability_research";
        }

        /// <summary>
        /// 是否可点：势力没有进行中的研究、且还有可研究的能力。
        /// </summary>
        public override bool IsValid
        {
            get
            {
                if (TargetCity == null || TargetCity.BelongForce == null)
                    return false;
                Force force = TargetCity.BelongForce;
                if (!force.IsPlayer)
                    return false;
                if (force.researchingAbilityId >= 0)
                    return false;

                // 金与行动力前置 —— 对齐原版 can_research_ability_command
                // （s11_sys_person_order_city.cpp:1348 / :1358）：
                //   district->get_action_point() >= cost 且 building->get_gold() >= gold_cost。
                // 原实现只查"有没有可研究项"，钱不够时按钮仍亮，点下去才提示失败。
                City payCity = ResolvePayCity(force, TargetCity);
                if (payCity == null)
                    return false;
                if (payCity.gold < AbilityStudySystem.ResearchGoldCost)
                    return false;
                // 【修正】这里原先把**金币**常量当成行动力门槛用（ResearchGoldCost * 2），
                // 而实际扣的是 ResearchActionPointCost（见 AbilityStudySystem.cs:237-239）。
                // 两者数值不同时，门槛会被放大成"2 倍金币值"，导致按钮长期灰着。
                if (payCity.BelongCorps != null
                    && payCity.BelongCorps.ActionPoint < AbilityStudySystem.ResearchActionPointCost * 2)
                    return false;

                return AbilityStudySystem.GetResearchableAbilities(force, Scenario.Cur).Count > 0;
            }
        }

        /// <summary>
        /// 进入指令：收集可研究项（由 <see cref="CityBaseSystem.OnEnter"/> 调用，
        /// 随后 <c>base.OnEnter</c> 会打开 <see cref="CityBaseSystem.windowName"/>）。
        /// </summary>
        public override void RecommandPersonList()
        {
            personList.Clear();
            optionList.Clear();
            selectedAbility = null;

            Force force = TargetCity != null ? TargetCity.BelongForce : null;
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null)
                return;

            optionList = AbilityStudySystem.GetResearchableAbilities(force, scenario);
        }

        /// <summary>
        /// 选中一条能力（窗口在节点点击时调用）。
        /// </summary>
        /// <param name="ability">能力；传 null 表示清空选中</param>
        public void SelectAbility(Ability ability)
        {
            selectedAbility = ability;
        }

        /// <summary>
        /// 是否可以确认（窗口用来控制按钮置灰）。
        /// </summary>
        /// <returns>已选中一条可研究能力返回 true</returns>
        public bool CanSure()
        {
            return selectedAbility != null;
        }

        /// <summary>
        /// 研究消耗与耗时（窗口展示用）。
        /// 耗时 = <c>ability.timeCost * 3</c>（timeCost 单位是"月"，按旬计需 ×3，
        /// 见 <see cref="AbilityStudySystem.StartResearchAt"/>）。
        /// </summary>
        /// <returns>形如「消耗 300 金 · 行动力 20 · 耗时 6 旬」；未选项目时不显示耗时</returns>
        public string GetResearchCostText()
        {
            int gold = AbilityStudySystem.ResearchGoldCost;
            int actionPoint = AbilityStudySystem.ResearchActionPointCost;
            string text = $"消耗 {gold} 金 · 行动力 {actionPoint}";
            if (selectedAbility != null)
                text += $" · 耗时 {System.Math.Max(selectedAbility.timeCost * 3, 1)} 旬";
            return text;
        }

        /// <summary>
        /// 确认执行：开始研究并结束指令；失败时提示原因且不关窗，便于玩家改选。
        /// </summary>
        public override void DoJob()
        {
            Force force = TargetCity != null ? TargetCity.BelongForce : null;
            if (selectedAbility == null || force == null || Scenario.Cur == null)
            {
                Done();
                return;
            }

            City payCity = ResolvePayCity(force, TargetCity);
            if (payCity == null
                || !AbilityStudySystem.StartResearchAt(force, Scenario.Cur, payCity, selectedAbility))
            {
                int x = TargetCity != null ? TargetCity.x : 0;
                int y = TargetCity != null ? TargetCity.y : 0;
                // 失败原因分流：找不到出资城与"资金不足"是两回事，混在一句会误导玩家
                PlayerMessage.AddTextMessage(payCity == null
                    ? "能力研究无法开始：找不到可出资的城池（君主不在城中）。"
                    : "能力研究无法开始：资金或行动力不足，或研究条件不满足。", force, x, y);
                return;
            }

            Done();
        }

        /// <summary>
        /// 出资城：优先君主所在城（与原版 <c>get_kunshu_service_id</c> 一致），
        /// 君主不在任何城时退回当前操作的城池（须仍属于本势力）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="fallback">回退城（当前操作的城池，可为 null）</param>
        /// <returns>出资城；没有返回 null</returns>
        static City ResolvePayCity(Force force, City fallback)
        {
            if (force == null)
                return null;
            Person governor = force.mGovernor;
            if (governor != null)
            {
                City city = governor.BelongCity ?? governor.CurrentCity;
                if (city != null)
                    return city;
            }
            if (fallback != null && fallback.BelongForce == force)
                return fallback;
            return null;
        }
    }
}
