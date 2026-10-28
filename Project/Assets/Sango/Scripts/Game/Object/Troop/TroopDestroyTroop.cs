using Sango.Tools;
using static Sango.Core.TroopAIUtility;

namespace Sango.Core
{
    public class TroopDestroyTroop : TroopMissionBehaviour
    {
        static WeightList<PriorityActionData> wightList = new WeightList<PriorityActionData>();

        public override MissionType MissionType { get { return MissionType.TroopDestroyTroop; } }

        public override bool IsMissionComplete
        {
            get
            {
                return (TargetTroop == null || !TargetTroop.IsAlive || !TargetTroop.IsEnemy(Troop));
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetTroop == null || TargetTroop.Id != troop.missionTarget) TargetTroop = scenario.troopsSet.Get(Troop.missionTarget);

            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                // 【歼灭目标后顺手清剿根据地】
                // AI 势力的"驱逐威胁部队"任务就是本类（CityAI.AIAttack 3.2 步：
                // city.TroopMissionType = TroopDestroyTroop）。贼 / 异族部队都是从根城
                // （kind 15/16）刷出来的，只杀掉部队不拆根城，等于本回合清掉、下回合又补一支。
                //
                // 【按城维护的"贼地格"直接查】不再做半径扫描：
                //   城在初始化范围（City.AddAreaCell）与营地生成（BanditCampPlacer.CreateCamp）时
                //   都会把营地格登记到 City.raiderCampCells，查询时惰性剔除已拆除的记录，
                //   因此这里直接按城取即可，既不会漏掉 4~8 格外的营地，也不用全图遍历。
                //
                // 【对所有部队生效】包括玩家手动指定的"歼灭"：玩家下的是"打掉这支部队"，
                // 打完顺手清掉刷兵营地属于同一意图的自然延续；有别的安排可以随时改任务。
                Building camp = FindEnemyCamp(troop);

                if (camp != null)
                {
                    Sango.Log.Info($"*[部队]{troop.Name} 歼灭 {TargetTroop?.Name} 后转攻敌方根据地 {camp.Name}（{camp.x},{camp.y}）");
                    troop.SetMission(MissionType.TroopDestroyBuilding, camp.Id);
                    troop.NeedPrepareMission();
                    return;
                }

                // 【说明】原先此处有 `if (troop.IsPlayerControl) ClearMission()` 的补丁。
                // 玩家第一军团现已改走 PlayerTroopDestroyTroop，
                // 由该类型负责"交回玩家"，故此处只保留势力 AI 的处理。
                troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                troop.NeedPrepareMission();
                return;

            }
            else
            {
                // 【分级策略】传入"态势档位 × 部队角色 × 领队性格"合成策略
                AIPolicy policy = AIPolicy.Combine(Troop.GetTierWeights(scenario), Troop.GetRoleWeights(), Troop.LeaderPersonality);

                // 获取目标城市周围的敌人
                TroopAIUtility.PriorityAction(wightList, Troop, TargetTroop.cell, scenario, SkillAttackPriority, null, policy);
                priorityActionData = wightList.Find((x) =>
                {
                    for(int i = 0; i < x.targets.Length; i++)
                    {
                        if (x.targets[i] == TargetTroop)
                        {
                            return true;
                        }
                    }

                    return false;
                });

                if (priorityActionData == null)
                {
                    // 【严格对齐原版】原版总是取评分最高的动作（第一名永不移除），
                    // Open311 模式下不使用"按档位动态择优池"，池大小由 AIConfig.o311BestN 控制。
                    priorityActionData = AIConfig.Instance.useOpen311ScoreFormula
                        ? wightList.RandomGetTop(AIConfig.Instance.o311BestN)
                        : wightList.RandomGetTop(policy.bestN);
                }
            }
        }

        /// <summary>
        /// 找与这支部队相关的敌方根城（异族 Base / 贼 Den）。
        ///
        /// 查的是城维护的 <see cref="City.raiderCampCells"/>（地图标记的"贼格"，即原版
        /// <c>hex.is_nejiro()</c> 的候选位），再判断格上是否真的立着敌方根城：
        ///   ① 先查部队**当前所在格**的领地城 —— 交战点就在那片领地里；
        ///   ② 再查部队归属城 —— 贼军跑到别的城去烧杀时，营地可能挂在老家那边；
        ///   ③ 最后翻本势力所有据点 —— 营地按格子的领地城登记，可能挂在第三座城名下。
        /// 全都没有时返回 null（调用方按返城处理）。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <returns>敌方根城；没有返回 null</returns>
        static Building FindEnemyCamp(Troop troop)
        {
            if (troop == null || troop.cell == null)
                return null;

            City here = troop.cell.BelongCity;
            Building camp = here != null ? here.FindRaiderCampFor(troop) : null;
            if (camp != null)
                return camp;

            City home = troop.BelongCity;
            if (home != null && home != here)
            {
                camp = home.FindRaiderCampFor(troop);
                if (camp != null)
                    return camp;
            }

            // ③ 还没找到：翻本势力所有据点的"贼地格"
            //    （营地是按格子的领地城登记的，贼军跨城流窜时营地可能挂在第三座城名下）
            if (troop.BelongForce == null)
                return null;

            Building found = null;
            troop.BelongForce.ForEachCityBase(c =>
            {
                if (found != null || c == null)
                    return;
                found = c.FindRaiderCampFor(troop);
            });
            return found;
        }

        /// <summary>
        /// 技能攻击评分：命中任务指定的追击目标为主目标，其它目标显著降权。
        /// 加成幅度全部取自 <see cref="AIConfig"/> 的 task* 倍率（不再硬编码 500000 等）。
        /// </summary>
        public int SkillAttackPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            int score = TroopAIUtility.SkillStatusPriority(troop, skill, target, movetoCell, spellCell);
            if (score <= 0)
                return score;

            // 【严格对齐原版】Open311 评分体系下不叠加任务特有加成（主目标倍率 / 原地 / 贴脸）。
            // 原版只有 get_target_score 里的 +10000，已在 CalcOpen311TargetScore 内实现。
            if (AIConfig.Instance.useOpen311ScoreFormula)
                return score;

            bool isStay = movetoCell == troop.cell;
            bool isPrimary = !target.IsEmpty() && target.troop != null && target.troop == TargetTroop;
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
                // 向目标前进
                return troop.TryCloseTo(TargetTroop.cell);
            }
        }
    }
}
