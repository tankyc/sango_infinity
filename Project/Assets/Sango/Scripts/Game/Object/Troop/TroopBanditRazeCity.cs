namespace Sango.Core
{
    /// <summary>
    /// 异族 / 贼"烧城"任务。
    ///
    /// 【原版对应】Subjugate(9) 的异族专用分支。异族不占地：
    ///   - 沿途攻击有主建筑（设施）与有主城（run_target_action 按 get_target_score 打分，
    ///     s11_sys_unit_action.cpp:1687-1778）；
    ///   - 城陷 → 城白城化（building_neutralize），异族部队随即**解散自毁**
    ///     （update_bandits 城陷分支走到 unit_disband_with_message，s11_sys_event_kanraku.cpp:126-129）；
    ///   - 港关陷 → 异族部队 get_bandit_next_target 转攻同城区域下一据点，找不到才解散。
    ///
    /// 普通势力的占领走 <see cref="TroopOccupyCity"/>；异族部队改用本任务：
    /// 目标城白城（无主）即征服完成，随后自毁解散。
    /// </summary>
    public class TroopBanditRazeCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopBanditRazeCity; } }

        public override bool IsMissionComplete
        {
            get
            {
                // 【原版对齐】目标城白城（无主）即征服完成；异族忽略空据点（bandit_ignores_empty_kyoten，
                // s11_sys_unit_action.cpp:222-230），不再攻击白城。
                if (TargetCity == null)
                    return true;
                if (TargetCity.BelongForce == null)
                    return true;
                return Scenario.Cur.citySet.Get(TargetCity.Id) == null;
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget)
                TargetCity = scenario.citySet.Get(Troop.missionTarget);

            // 目标城已白城 / 被夷平 → 异族部队自毁解散（城陷主路径在 UpdateBanditsAfterFall，
            // 这里是"目标城被其它势力抢先攻陷"时的兜底）。
            if (IsMissionComplete)
            {
                Sango.Log.Info($"*[异族]{troop.Name} 目标 {TargetCity?.Name ?? "据点"} 已无主，自毁解散");
                troop.Clear();
                return;
            }

            // 目标城还有主（敌方正常势力城）→ 计算攻击策略（含沿途有主建筑）
            priorityActionData = TroopAIUtility.PriorityAction(troop, TargetCity.CenterCell, scenario, SkillAttackPriority);
        }

        /// <summary>
        /// 技能攻击评分：异族 / 贼严格走原版离散评分（get_target_score + get_attack_damage_score
        /// + 动作常量），不受全局 <see cref="AIConfig.useOpen311ScoreFormula"/> 开关影响。
        ///
        /// 异族分支的 main_target 清空（原版 s11_sys_unit_action.cpp:65-68：
        /// `if (is_city(main_target)) main_target = nullptr;`）见
        /// <see cref="TroopAIUtility"/> 的 CalcOpen311TargetScore。
        /// </summary>
        public int SkillAttackPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            return TroopAIUtility.Open311SkillPriority(troop, skill, target, movetoCell, spellCell);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (IsMissionComplete)
            {
                troop.NeedPrepareMission();
                return true;
            }

            if (priorityActionData != null)
            {
                if (!priorityActionData.moveFinish && !troop.MoveTo(priorityActionData.movetoCell))
                    return false;
                if (!priorityActionData.moveFinish)
                    priorityActionData.moveFinish = true;
                if (!troop.SpellSkill(priorityActionData.skill, priorityActionData.spellCell))
                    return false;
                return true;
            }
            return troop.TryCloseTo(TargetCity.CenterCell);
        }
    }
}
