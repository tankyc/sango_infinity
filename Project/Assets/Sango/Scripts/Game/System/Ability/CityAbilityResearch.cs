using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 玩家侧"能力研究"指令。
    ///
    /// 对齐出处：Open311 `force_research_ability`（s11_game_ai_force_cmd.cpp:754-817）的**玩家分支** ——
    /// 原版玩家通过独立的"能力研究"指令选一条 Ability 开始研究，AI 走同一套接口自动挑。
    ///
    /// 【交互】本指令**不需要自己的窗口**：点菜单后直接弹通用对象选择器
    /// （<see cref="AbilitySelectSystem"/>），选一条可研究的能力即开始研究。
    /// 因此 <c>windowName</c> 留空，并重写 <see cref="OnDestroy"/> 避免关闭不存在的窗口。
    /// </summary>
    [GameSystem]
    public class CityAbilityResearch : CityBaseSystem
    {
        public CityAbilityResearch()
        {
            customTitleName = "能力研究";
            customMenuName = "势力/能力研究";
            // 排在"计略"组（3950~3970）之后、"君主"组（4000+）之前
            customMenuOrder = 3980;
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
        /// 进入指令：不打开自己的窗口，直接弹通用能力选择器。
        /// </summary>
        public override void OnEnter()
        {
            // 注意：不调 base.OnEnter() —— 那会 Window.Open(windowName)，而本指令没有独立窗口
            Force force = TargetCity != null ? TargetCity.BelongForce : null;
            if (force == null || Scenario.Cur == null)
            {
                Done();
                return;
            }

            List<Ability> candidates = AbilityStudySystem.GetResearchableAbilities(force, Scenario.Cur);
            if (candidates.Count == 0)
            {
                Done();
                return;
            }

            AbilitySelectSystem selector = GameSystem.GetSystem<AbilitySelectSystem>();
            if (selector == null)
            {
                Done();
                return;
            }

            selector.Start(candidates, null, 1, OnAbilitySelected, null, "选择研究项目", -1);
        }

        /// <summary>
        /// 选择器回调：在君主所在城出资开始研究。
        /// </summary>
        /// <param name="abilities">选中的能力，单选时长度为 1</param>
        void OnAbilitySelected(List<Ability> abilities)
        {
            if (abilities == null || abilities.Count <= 0)
            {
                Done();
                return;
            }

            Force force = TargetCity != null ? TargetCity.BelongForce : null;
            City payCity = ResolvePayCity(force, TargetCity);

            if (payCity == null || !AbilityStudySystem.StartResearchAt(force, Scenario.Cur, payCity, abilities[0]))
            {
                int x = TargetCity != null ? TargetCity.x : 0;
                int y = TargetCity != null ? TargetCity.y : 0;
                // 失败原因分流：找不到出资城与"资金不足"是两回事，混在一句会误导玩家
                PlayerMessage.AddTextMessage(payCity == null
                    ? "能力研究无法开始：找不到可出资的城池（君主不在城中）。"
                    : "能力研究无法开始：资金或行动力不足，或研究条件不满足。", force, x, y);
            }

            // 不在这里 Done()：Done 会清空系统栈但**不关选择器窗口**
            // （只有 Back() 才会触发 ObjectSelectSystem.OnDestroy → Close）。
            // 因此让选择器自己 Back() 退栈关窗，随后触发本类的 OnBack。
        }

        /// <summary>
        /// 选择器退栈后结束本指令。
        /// </summary>
        /// <param name="whoGone">刚退栈的系统</param>
        public override void OnBack(ICommandEvent whoGone)
        {
            base.OnBack(whoGone);
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

        /// <summary>
        /// 本指令不打开自己的窗口，因此不调 base（那会 Close 一个从未打开的 windowName）。
        /// </summary>
        public override void OnDestroy()
        {
        }
    }
}
