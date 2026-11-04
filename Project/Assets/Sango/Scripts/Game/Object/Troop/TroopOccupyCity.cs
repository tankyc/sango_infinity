using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopOccupyCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopOccupyCity; } }

        public override bool IsMissionComplete
        {
            get
            {
                if (TargetCity == null)
                    return true;

                // 【只有贼 / 异族在目标变白城时算完成】
                //
                // 普通势力能占领城池：白城对它们是合法的占领目标，任务要照常推进
                // （抵达后由进城流程接管）。在这里一味判完成，会把"去占空城"的部队
                // 半路打回去，属于功能性倒退。
                //
                // 贼 / 异族占不了城（原版 bandit_ignores_empty_kyoten，s11_sys_unit_action.cpp:222-230），
                // 对着白城继续攻击没有任何意义：本项目 City.ChangeTroops 对无主城是直接
                // return false（City.cs:2365-2367「白城直接占领」），兵力扣不掉、
                // 也永远不会触发 OnFall —— 这正是玩家反馈的"对着空港口反复鞭尸"。
                if (TargetCity.BelongForce == null && IsOutlander(Troop))
                    return true;

                return !TargetCity.IsEnemy(Troop);
            }
        }

        /// <summary>
        /// 是否为贼 / 异族势力（<see cref="Force.IsNormal"/> 的否定，对齐原版 <c>Force::is_normal()</c>）。
        /// 这两类势力无法占领城池，遇白城只能撤离。
        /// </summary>
        /// <param name="troop">待判定的部队</param>
        /// <returns>是贼或异族返回 true</returns>
        static bool IsOutlander(Troop troop)
        {
            return troop != null && troop.BelongForce != null && !troop.BelongForce.IsNormal;
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);
            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            // 【优化】原先此处还有"断粮时 30% 概率撤退"的随机判断,与 Troop.AIPrepare 的
            // 统一撤退逻辑重复且互相干扰,现统一由 AIPrepare 处理。
            if (IsMissionComplete)
            {
                if (TargetCity == null)
                {
                    Troop.SetMission(MissionType.TroopReturnCity, Troop.BelongCity.Id);
                }
                else if (TargetCity.BelongForce == null && IsOutlander(Troop))
                {
                    // 贼 / 异族占不了城：目标变白城就地撤离，与 TroopBanditRazeCity
                    // 的自毁口径一致（那边是 Clear，这里不保留驻留，避免它们停在别人的空城上）。
                    // 普通势力不会走到这里 —— 上面 IsMissionComplete 对它们是 false。
                    Sango.Log.Info($"*[异族]{Troop.Name} 目标 {TargetCity.Name} 已无主，无法占领，撤离");
                    Troop.Clear();
                    return;
                }
                else if (!TargetCity.IsSameForce(Troop))
                {
                    // 【同盟协防口径】被友军 / 盟友拿取时不是无脑驻防：
                    // 结盟后攻城任务会走到这个分支，若协防不划算必须立刻召回，
                    // 否则就是玩家反馈的"同盟队伍扛着同字标堵到家门口"。
                    if (TargetCity.BelongForce != null
                        && TargetCity.BelongForce != Troop.BelongForce
                        && TroopAIFallback.IsAiTroop(Troop)
                        && !TroopAIFallback.CanSupportDefense(Troop, TargetCity, scenario)
                        && TroopAIFallback.RecallToNearestOwnCity(Troop, scenario,
                            $"盟友城{TargetCity.Name}协防不划算（距离过远或兵力不足）"))
                    {
                        return;
                    }

                    // 被友军拿取,保护友军城池,直到消灭敌人
                    Troop.SetMission(MissionType.TroopProtectCity, TargetCity.Id);
                }
                else
                {
                    Troop.SetMission(MissionType.TroopMovetoCity, TargetCity.Id);
                }
                Troop.NeedPrepareMission();
                return;

            }
            else
            {
                // 检查目标城市周围的敌人
                //List<Cell> enemyCells = new List<Cell>();
                //TroopAIUtility.RangeEnemyCell(troop, 4, enemyCells, scenario);
                
                //// 根据情况选择不同的策略
                //if (enemyCells.Count > 0)
                //{
                    // 有敌人，优先攻击威胁大的敌人
                    priorityActionData = TroopAIUtility.PriorityAction(Troop, TargetCity.CenterCell, scenario, SkillAttackPriority);
                //}
                //else
                //{
                //    // 没有敌人，直接向城市前进
                //    priorityActionData = null;
                //}
            }
        }

        /// <summary>
        /// 技能攻击评分：命中任务目标城池为主目标，途中遭遇的部队为次要目标。
        /// 加成幅度全部取自 <see cref="AIConfig"/> 的 task* 倍率（不再硬编码 500000 / 30000 等）。
        /// </summary>
        public int SkillAttackPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            int score = TroopAIUtility.SkillStatusPriority(troop, skill, target, movetoCell, spellCell);
            if (score <= 0)
                return score;

            // 【严格对齐原版】Open311 评分体系下，分值完全由 get_target_score +
            // get_attack_damage_score + 动作常量决定，不再叠加任务特有加成
            // （打大部队加权 / 主目标倍率 / 原地或贴脸加成，原版都没有）。
            if (AIConfig.Instance.useOpen311ScoreFormula)
                return score;

            AIConfig cfg = AIConfig.Instance;
            bool isStay = movetoCell == troop.cell;
            bool isPrimary = false;

            if (!target.IsEmpty() && target.building != null)
            {
                // 主目标：任务指定的敌方城池
                isPrimary = target.building == TargetCity;
            }
            else if (!target.IsEmpty() && target.troop != null)
            {
                // 途中遭遇的敌方部队：兵力远大于我方时略有加成（值得优先削弱强敌）
                if (target.troop.troops * 100 > troop.troops * cfg.taskBigTroopPercent)
                    score = score + score * cfg.taskBigTroopBonus / 100;
            }

            bool isMeleeClose = isStay && !troop.TroopType.isRange;
            return TroopAIUtility.ApplyTaskBonus(score, isPrimary, isStay, isMeleeClose, troop.GetRoleWeights());
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
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
            else
            {
                return troop.TryCloseTo(TargetCity.CenterCell);
                // 向目标前进，优先选择安全路径
                //return TroopAIUtility.MoveToTargetSafely(troop, TargetCity.CenterCell, scenario);
            }
        }
    }
}
