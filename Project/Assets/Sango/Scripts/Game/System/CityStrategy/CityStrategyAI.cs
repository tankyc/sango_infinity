using System.Collections.Generic;
using Sango.Core.Player;

namespace Sango.Core
{
    /// <summary>
    /// 城市计略的 AI 决策入口，注册在 Force.AIPrepare 的指令链上（Force.cs 的 AICommandList）。
    /// 职责分两段：
    /// 1）事后反应：二虎竞食把两方交情打到 cityStrategyWarTriggerRelation 以下时，被挑拨的一方在
    ///    自己的势力回合里决定是否破盟宣战。放在 AI 回合而不是计略结算帧，是为了不在
    ///    Person.UpdateMission 的循环里改同盟结构（同外交事件的取舍，见 CityStrategyManager 类注释）。
    /// 2）主动施计：按"记仇报复"和"主动试探"两档概率，挑一个够得着、且我方智力占优的敌方据点施流言，
    ///    或在两个第三方同盟之间挑二虎竞食。
    /// 与 ForceAI.AIDiplomacy 的分工：外交 AI 目前整条被屏蔽（Force.cs 的 AIPrepare 里注释掉了），
    /// 这里只处理"由计略引起"的破盟，不接管结盟/停战/通商，避免把屏蔽掉的整套外交 AI 顺带放出来。
    /// 所有消耗、判重、在途校验都交给 CityStrategyManager.Dispatch，本类只负责"要不要做、对谁做"。
    /// </summary>
    public static class CityStrategyAI
    {
        /// <summary>
        /// AI 势力回合内的城市计略步骤。
        /// 约定与 ForceAI 里其它指令一致：返回 true 表示本步已处理完，可以让 DoAI 继续弹下一条指令。
        /// 本方法整回合最多产生一次计略行为（破盟反应优先于新施计），不做批量铺量。
        /// </summary>
        /// <param name="force">当前行动的势力</param>
        /// <param name="scenario">当前场景</param>
        /// <returns>恒为 true：本步骤从不挂起回合协程</returns>
        public static bool AICityStrategy(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;
            // 玩家侧的计略由界面两步选择器驱动，AI 不代劳
            if (force.IsPlayer)
                return true;
            // 开局若干回合不施计，先让地图上的势力边界稳定下来（与 ForceAI.AIDiplomacy 用同一道门）
            if (scenario.TurnCount < 10)
                return true;
            if (force.mGovernor == null || force.CapitalCity == null)
                return true;

            // 顺带清理过期的记仇与挑拨凭据，避免长局里字典无上限增长
            force.CleanupCityStrategyMarks(scenario.Variables.cityStrategyGrudgeKeepTurns);

            // 先还账再进攻：本轮既然已经被挑拨到翻脸，就不在该回合同时再发起新的计略
            if (TryBreakAllianceByStrategy(force, scenario))
                return true;

            TryPerformStrategy(force, scenario);
            return true;
        }

        /// <summary>
        /// 破盟反应：本势力若带着"刚被二虎竞食挑拨"的凭据，且与对方仍是同盟、交情已跌破阈值，
        /// 就解除同盟并向对方宣战。
        /// 解约写法照抄既有的 AI 撕约实现（ForceAI.cs 的 CheckBreakAlliance 尾部），
        /// 宣战则复用 DiplomacyManager.PerformDiplomacyAction(DeclareWar)：
        /// 注意必须先解约再宣战，因为宣战的 CanPerform 会把"尚存任何协议"判为不可宣战。
        /// </summary>
        /// <param name="force">当前 AI 势力</param>
        /// <param name="scenario">当前场景</param>
        /// <returns>本回合执行了一次破盟（含随后的宣战）返回 true</returns>
        private static bool TryBreakAllianceByStrategy(Force force, Scenario scenario)
        {
            int triggerRelation = scenario.Variables.cityStrategyWarTriggerRelation;

            // 下面会真的从 AllianceList 里摘掉对象，所以先取快照再遍历，不能在 ForEach 里直接改
            List<Alliance> allianceSnapshot = new List<Alliance>();
            force.AllianceList.ForEach(alliance =>
            {
                if (alliance != null && alliance.IsAlive && alliance.allianceType == AllianceType.Alliance)
                    allianceSnapshot.Add(alliance);
            });

            for (int i = 0; i < allianceSnapshot.Count; i++)
            {
                Alliance alliance = allianceSnapshot[i];
                Force other = FindCounterpart(alliance, force);
                if (other == null || !other.IsAlive)
                    continue;

                // 凭据是一次性的：取不到（没被挑拨过、或已过期）就跳过，防止同一笔账反复放大
                if (!force.TakeStrategyWarSeed(other))
                    continue;
                if (scenario.GetRelation(force, other) > triggerRelation)
                    continue;

                // 解除同盟：置死并双向摘除，与 ForceAI 的撕约写法保持一致
                alliance.IsAlive = false;
                force.AllianceList.Remove(alliance);
                other.AllianceList.Remove(alliance);

                string headline = $"{force.ColorName}察觉与{other.ColorName}的交情已被人挑拨至冰点（关系 {scenario.GetRelation(force, other)}），遂解除同盟";

                // 解约后仍可能残留停战/通商协议，而宣战的 CanPerform 会把"尚存任何协议"判为不可宣战，
                // 那样走 Perform 只会触发一次"外交失败"的额外关系惩罚。这里先自查一次，只在确实无协议时才宣战。
                bool warDeclared = !force.HasActiveAgreement(other)
                    && GameSystem.GetSystem<DiplomacyManager>()
                        .PerformDiplomacyAction(DiplomacyActionType.DeclareWar, force, other);

                BroadcastToPlayer(headline + (warDeclared ? "并宣布出兵讨伐。" : "，但暂未宣战。"), force);

#if SANGO_DEBUG
                Sango.Log.Info($"@计略@二虎竞食后效：{force.Name} 与 {other.Name} 破盟，宣战={warDeclared}，关系={scenario.GetRelation(force, other)}");
#endif
                return true;
            }

            return false;
        }

        /// <summary>
        /// 施计主流程：报复档优先（对方在有效期内算计过我们，概率更高且要求距离够近），
        /// 其次才是主动挑一个弱势邻国施流言，最后尝试在第三方同盟之间挑二虎竞食。
        /// 每档都各掷一次概率，任一档真正派遣成功即结束本回合，避免 AI 一回合刷光金钱。
        /// </summary>
        /// <param name="force">当前 AI 势力</param>
        /// <param name="scenario">当前场景</param>
        /// <returns>本回合成功派出任一计略使者返回 true</returns>
        private static bool TryPerformStrategy(Force force, Scenario scenario)
        {
            ScenarioVariables variables = scenario.Variables;

            // 报复：谁在有效期内算过我们，优先对谁施流言（对应原版 721 的 rumor_timer + 报复范围=2 座城）
            Force attacker = FindGrudgeAttacker(force, scenario, variables.cityStrategyGrudgeKeepTurns);
            if (attacker != null && GameRandom.Chance(variables.cityStrategyAIRevengeChance))
            {
                if (TryDispatchRumor(force, scenario, attacker, variables.cityStrategyAIRevengeMaxDays))
                    return true;
            }

            // 主动流言：挑一个敌对邻国下手
            if (GameRandom.Chance(variables.cityStrategyAIAttemptChance))
            {
                Force victim = PickRumorVictim(force, scenario);
                if (victim != null && TryDispatchRumor(force, scenario, victim, 0))
                    return true;
            }

            // 主动二虎竞食：在第三方同盟之间挑，才有"两虎"可竞
            if (GameRandom.Chance(variables.cityStrategyAIAttemptChance))
            {
                if (TryDispatchTwoTigers(force, scenario))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 派遣流言使者：先在敌方选一座够得着的据点，再在我方选一个付得出路费的高智力空闲武将，
        /// 两道门槛（智力优势、资金倍数）都过了才交给 CityStrategyManager.Dispatch 落地。
        /// </summary>
        /// <param name="force">施计方</param>
        /// <param name="scenario">当前场景</param>
        /// <param name="victim">被施计势力</param>
        /// <param name="maxDistance">目的地距离上限（城数），0 表示不限制</param>
        /// <returns>派遣成功返回 true</returns>
        private static bool TryDispatchRumor(Force force, Scenario scenario, Force victim, int maxDistance)
        {
            if (victim == null || victim == force || !victim.IsAlive)
                return false;
            // 同盟之间施流言在语义上不成立：本方武将不会替敌国动摇军心，且会白扣一笔关系
            if (force.IsAlliance(victim))
                return false;

            City targetCity = PickTargetCity(victim, force, maxDistance);
            if (targetCity == null)
                return false;

            Person envoy = PickEnvoy(force, scenario, targetCity, (int)CityJobType.Rumor, maxDistance);
            if (envoy == null)
                return false;

            return DispatchIfWorthIt(scenario, CityStrategyType.Rumor, force, envoy, targetCity, victim, null);
        }

        /// <summary>
        /// 派遣二虎竞食使者：在两个"与本方不同且互为同盟"的第三方势力之间挑拨，
        /// 挑关系最铁的一对——只有把好消息打坏，这次施计才有可感知的收益。
        /// </summary>
        /// <param name="force">施计方</param>
        /// <param name="scenario">当前场景</param>
        /// <returns>派遣成功返回 true</returns>
        private static bool TryDispatchTwoTigers(Force force, Scenario scenario)
        {
            Force pairA = null;
            Force pairB = null;
            int bestRelation = int.MinValue;

            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force a = scenario.forceSet[i];
                if (a == null || !a.IsAlive || a == force)
                    continue;
                for (int j = i + 1; j < scenario.forceSet.Count; j++)
                {
                    Force b = scenario.forceSet[j];
                    if (b == null || !b.IsAlive || b == force || b == a)
                        continue;
                    if (!a.IsAlliance(b))
                        continue;

                    int relation = scenario.GetRelation(a, b);
                    // 已经跌破翻脸线的同盟不必再挑：AI 回合会自己处理，重复施计只是浪费金钱
                    if (relation <= scenario.Variables.cityStrategyWarTriggerRelation)
                        continue;
                    if (relation <= bestRelation)
                        continue;

                    bestRelation = relation;
                    pairA = a;
                    pairB = b;
                }
            }

            if (pairA == null || pairB == null)
                return false;

            // 使者目的地是 A 的治所（原版二虎竞食也是到第一方的城邑施法）
            City targetCity = pairA.CapitalCity;
            if (targetCity == null)
                return false;

            Person envoy = PickEnvoy(force, scenario, targetCity, (int)CityJobType.TwoTigers, 0);
            if (envoy == null)
                return false;

            return DispatchIfWorthIt(scenario, CityStrategyType.TwoTigers, force, envoy, targetCity, pairA, pairB);
        }

        /// <summary>
        /// 共用的派遣前评估：用真实结算口径算一次成功率（不掷骰），要求使者智力对抵抗者有明显优势，
        /// 再交给 Dispatch 做条件与消耗的终局校验。
        /// 智力优势门对应原版 #军师之战2.cpp 的"智力差 &lt; 5 不应战"，避免 AI 用庸才白送一趟路费。
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <param name="strategyType">计略类型</param>
        /// <param name="force">施计方</param>
        /// <param name="envoy">拟派遣的使者</param>
        /// <param name="targetCity">目的地据点</param>
        /// <param name="targetForceA">目标势力A</param>
        /// <param name="targetForceB">目标势力B，仅二虎竞食</param>
        /// <returns>实际派出使者返回 true</returns>
        private static bool DispatchIfWorthIt(Scenario scenario, CityStrategyType strategyType, Force force, Person envoy,
            City targetCity, Force targetForceA, Force targetForceB)
        {
            CityStrategyManager manager = GameSystem.GetSystem<CityStrategyManager>();
            CityStrategyActionBase action = manager.CreateAction(strategyType, force, envoy.BelongCity, envoy, targetCity, targetForceA, targetForceB);
            if (action == null)
                return false;

            int rate = action.CalculateSuccessRate();
            int resistanceIntelligence = action.Resister?.Intelligence ?? 0;
            if (envoy.Intelligence - resistanceIntelligence < scenario.Variables.cityStrategyAIMinIntelligenceEdge)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"@计略@AI放弃{action.GetActionName()}：{envoy.Name} 智力 {envoy.Intelligence} 对 {action.Resister?.Name ?? "无抵抗者"} {resistanceIntelligence} 优势不足");
#endif
                return false;
            }

            if (!manager.Dispatch(action))
                return false;

#if SANGO_DEBUG
            Sango.Log.Info($"@计略@AI施计：{force.Name} 派 {envoy.Name} 自 {envoy.BelongCity?.Name} 前往 {targetCity.Name} 行{action.GetActionName()}，成功率 {rate}");
#endif
            return true;
        }

        /// <summary>
        /// 在目标势力的据点里挑一个可施计的城：在我方任一城能抵达的范围内取最近的一座，
        /// 同距离下优先都市（流言对都市才会同时降治安，收益更高）。
        /// 限距（报复档）时若范围内无城则返回 null，让本回合不做强行之举。
        /// </summary>
        /// <param name="victim">被施计势力</param>
        /// <param name="force">施计方，用于计算行程距离</param>
        /// <param name="maxDistance">距离上限（城数），0 表示不限制</param>
        /// <returns>选中的据点，无可选项时返回 null</returns>
        private static City PickTargetCity(Force victim, Force force, int maxDistance)
        {
            City best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < victim.CityList.Count; i++)
            {
                City city = victim.CityList[i];
                if (city == null || !city.IsAlive)
                    continue;

                int distance = NearestDistance(city, force);
                if (distance <= 0)
                    continue;
                if (maxDistance > 0 && distance > maxDistance)
                    continue;

                // 更近者优先；等距时都市优先
                if (best == null || distance < bestDistance
                    || (distance == bestDistance && city.IsCity() && !best.IsCity()))
                {
                    best = city;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// 取某座城到我方所有城池的最短距离（城数）。用现成的 City.Distance，它已把港/关折算到所属都市。
        /// </summary>
        /// <param name="city">候选据点</param>
        /// <param name="force">我方势力</param>
        /// <returns>最短距离，我方无城时返回 int.MaxValue</returns>
        private static int NearestDistance(City city, Force force)
        {
            int nearest = int.MaxValue;
            for (int i = 0; i < force.CityList.Count; i++)
            {
                City own = force.CityList[i];
                if (own == null || !own.IsAlive)
                    continue;
                int distance = city.Distance(own);
                if (distance < nearest)
                    nearest = distance;
            }
            return nearest;
        }

        /// <summary>
        /// 为我方挑选执行计略的使者与出发城：遍历本势力城池，按"付得起路费 + 距离达标"筛城，
        /// 城内取智力最高的空闲非君主武将，全局以智力优先、同智力取距离更近的。
        /// 排除君主是因为原版"君主亲往不被俘"，而且君主一旦被擒等于势力核心被摘走，AI 不该冒这个险。
        /// </summary>
        /// <param name="force">施计方</param>
        /// <param name="scenario">当前场景</param>
        /// <param name="targetCity">目的地据点</param>
        /// <param name="jobId">对应城市工作 Id，用于读取金钱消耗</param>
        /// <param name="maxDistance">距离上限（城数），0 表示不限制</param>
        /// <returns>可用的使者武将，没有合适人选时返回 null</returns>
        private static Person PickEnvoy(Force force, Scenario scenario, City targetCity, int jobId, int maxDistance)
        {
            int baseCost = JobType.GetJobCost(jobId);
            Person best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < force.CityList.Count; i++)
            {
                City city = force.CityList[i];
                if (city == null || !city.IsAlive || city.freePersons.Count == 0)
                    continue;

                int distance = targetCity.Distance(city);
                if (maxDistance > 0 && distance > maxDistance)
                    continue;

                // 资金门：对齐原版 255 号脚本对 AI 施计的"gold >= 消耗 × rand(10,20)/10"，
                // 让 AI 在穷城手里不去做只亏不赚的买卖（Range 上界开区间，故取 21）
                if (city.gold < baseCost * GameRandom.Range(10, 21) / 10)
                    continue;

                for (int k = 0; k < city.freePersons.Count; k++)
                {
                    Person person = city.freePersons[k];
                    if (person == null || !person.IsAlive || !person.IsFree || person.IsGovernor)
                        continue;
                    if (person.BelongForce != force)
                        continue;

                    if (best == null || person.Intelligence > best.Intelligence
                        || (person.Intelligence == best.Intelligence && distance < bestDistance))
                    {
                        best = person;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// 找出最近一次在有效期内对本势力施过计略的敌方势力（取记仇表里时点最新的一方）。
        /// </summary>
        /// <param name="force">被施计的势力</param>
        /// <param name="scenario">当前场景</param>
        /// <param name="keepTurns">记仇有效期（回合）</param>
        /// <returns>可报复的施计方，没有则返回 null</returns>
        private static Force FindGrudgeAttacker(Force force, Scenario scenario, int keepTurns)
        {
            if (keepTurns <= 0 || force.CityStrategyGrudgeTurn.Count == 0)
                return null;

            int now = scenario.TurnCount;
            Force attacker = null;
            int latestTurn = int.MinValue;
            foreach (KeyValuePair<int, int> pair in force.CityStrategyGrudgeTurn)
            {
                if (now - pair.Value > keepTurns)
                    continue;
                Force candidate = scenario.forceSet.Get(pair.Key);
                if (candidate == null || !candidate.IsAlive || candidate == force)
                    continue;
                if (pair.Value <= latestTurn)
                    continue;
                latestTurn = pair.Value;
                attacker = candidate;
            }
            return attacker;
        }

        /// <summary>
        /// 主动施计时的目标选择：从接壤势力里挑一个非同盟、且据点最少（最弱）的对手，
        /// 让 AI 的金钱花在"打得动"的目标上而不是硬啃强者。
        /// </summary>
        /// <param name="force">施计方</param>
        /// <param name="scenario">当前场景</param>
        /// <returns>被施计势力，没有合适对象时返回 null</returns>
        private static Force PickRumorVictim(Force force, Scenario scenario)
        {
            Force victim = null;
            for (int i = 0; i < force.NeighborForceList.Count; i++)
            {
                Force other = force.NeighborForceList[i];
                if (other == null || other == force || !other.IsAlive)
                    continue;
                if (force.IsAlliance(other))
                    continue;
                if (victim == null || other.CityList.Count < victim.CityList.Count)
                    victim = other;
            }
            return victim;
        }

        /// <summary>
        /// 取同盟对象里除了自己之外的另一方。同盟是两方结构，故取到第一个不是本方的势力即可。
        /// </summary>
        /// <param name="alliance">同盟对象</param>
        /// <param name="force">本势力</param>
        /// <returns>对方势力，异常结构下返回 null</returns>
        private static Force FindCounterpart(Alliance alliance, Force force)
        {
            Force counterpart = null;
            alliance.ForceList.ForEach(other =>
            {
                if (counterpart == null && other != null && other != force)
                    counterpart = other;
            });
            return counterpart;
        }

        /// <summary>
        /// 把 AI 侧的计略后效写进左下角信息窗口，坐标取该势力治所，玩家点击可跳转到事发城。
        /// 与 CityStrategyActionBase.BroadcastMessage 同一口径（PlayerMessage.AddTextMessage）。
        /// </summary>
        /// <param name="text">广播正文</param>
        /// <param name="force">消息归属势力</param>
        private static void BroadcastToPlayer(string text, Force force)
        {
            City capital = force.CapitalCity;
            PlayerMessage.AddTextMessage(text, force, capital != null ? capital.x : 0, capital != null ? capital.y : 0);
        }
    }
}
