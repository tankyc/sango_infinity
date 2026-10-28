using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 援军派发 —— 逐行对齐 Open311：
    ///   · `create_reinforcement_unit`（`s11_sys_scen_unit.cpp:156-172`）
    ///   · `create_reinforce_deploy_param`（`s11_game_ai_kyoten_cmd_mil.cpp:195-256`）
    ///   · `unit_reinforce`（`s11_sys_scen_unit.cpp:1355-1370`）
    ///
    /// 【原版口径】
    ///   1. 出兵据点 = 同盟的 `src`（本项目取同盟首都）；
    ///   2. 主将 = 据点空闲武将里「统率+武力+智力」均值最接近据点平均值的那个，
    ///      排除君主、排除守将（guard）；
    ///   3. 兵力 `n = min(守军/2 - 5000, 主将统率, 兵装库存)`，再向下取整到 **1000**，
    ///      `troops <= 0` 直接失败；
    ///   4. 粮草 `n/1000 * 3000`，夹在据点存粮之间，为 0 也失败；
    ///   5. **`dest` 是"要攻击的敌方据点"**，不是"要守的城"——
    ///      `unit_reinforce` 下的是 `UnitOrderId_Attack`（`s11_sys_scen_unit.cpp:1362`）。
    ///
    /// 【本项目差异】没有"武器库存"这一维之外的部分已全部对齐；部队不下发 `UpdateCell`
    /// 瞬移，而是**留在出兵城、由任务系统带它出城**（与原版 `unit_set_order + run_order` 同构）。
    /// </summary>
    public static class ReinforcementDispatcher
    {
        /// <summary>出兵门槛：守军一半再减 5000，低于此数不出兵（原版 `:240`）</summary>
        const int TroopsReserve = 5000;

        /// <summary>兵力按千取整（原版 `:241`）</summary>
        const int TroopsUnit = 1000;

        /// <summary>每千兵消耗粮草（原版 `:242`）</summary>
        const int FoodPerThousand = 3000;

        /// <summary>
        /// 尝试派出一支援军。
        /// </summary>
        /// <param name="ally">同盟（出兵方）</param>
        /// <param name="requester">请求方（受援方）</param>
        /// <param name="attackTarget">请求方指定的**敌方**攻击目标；null 时自动挑</param>
        /// <returns>是否真的派出了部队</returns>
        public static bool TryDispatch(Force ally, Force requester, City attackTarget)
        {
            if (ally == null || requester == null)
                return false;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return false;

            City src = ally.CapitalCity;
            if (src == null || src.freePersons == null || src.freePersons.Count == 0)
                return false;

            // 攻击目标：原版由请求方在提案时指定（`self->dest`）；未指定则挑一个相邻敌城
            City dest = attackTarget != null ? attackTarget : FindAttackTarget(requester, ally);
            if (dest == null)
            {
                Sango.Log.Info($"@外交@{ally.Name} 无法派出援军：{requester.Name} 周边没有可攻击的目标");
                return false;
            }

            Person leader = PickLeader(src, ally);
            if (leader == null)
                return false;

            // 兵装：枪/戟/弩/马里数量最多的那一类（原版 `:229-239`）；
            // 一类都没有则直接失败 —— 原版不允许"空手出征"。
            int bestKind = -1;
            int bestQuantity = 0;

            if (src.itemStore != null)
            {
                for (int kind = (int)ItemStoreKindType.Spear; kind <= (int)ItemStoreKindType.Horse; kind++)
                {
                    int q = src.itemStore.GetNumber(kind);
                    if (q > bestQuantity)
                    {
                        bestQuantity = q;
                        bestKind = kind;
                    }
                }
            }

            if (bestKind < 0 || bestQuantity <= 0)
            {
                Sango.Log.Info($"@外交@{ally.Name} 无力派出援军：{src.Name} 没有可用兵装");
                return false;
            }

            // 兵力：min(守军/2 - 5000, 主将统率, 兵装库存)，向下取整到千
            int n = src.troops / 2 - TroopsReserve;
            if (leader.Command < n) n = leader.Command;
            if (bestQuantity < n) n = bestQuantity;

            int troops = n / TroopsUnit * TroopsUnit;
            if (troops <= 0)
            {
                Sango.Log.Info($"@外交@{ally.Name} 无力派出援军：{src.Name} 守军 {src.troops}，" +
                               $"扣除 {TroopsReserve} 保留后不足千人");
                return false;
            }

            // 粮草：n/1000 * 3000，夹在据点存粮内
            int food = n / TroopsUnit * FoodPerThousand;
            if (src.food < food) food = src.food;
            if (food <= 0)
            {
                Sango.Log.Info($"@外交@{ally.Name} 无力派出援军：{src.Name} 存粮不足");
                return false;
            }

            // 兵种：按该武将可用兵种取第一个陆战兵种（对应原版的装备选择）
            List<Person> members = new List<Person> { leader };
            List<TroopType> activeTypes = new List<TroopType>();
            TroopType.CheckActivTroopTypeList(members, activeTypes);

            TroopType land = null;
            for (int i = 0; i < activeTypes.Count; i++)
            {
                if (activeTypes[i].isLand) { land = activeTypes[i]; break; }
            }

            if (land == null)
                return false;

            Troop troop = new Troop();
            troop.Leader = leader;
            troop.LandTroopType = land;
            troop.WaterTroopType = land;
            troop.troops = troops;
            troop.morale = src.morale;
            troop.energy = src.energy;
            troop.IsAlive = true;
            troop.ActionOver = false;

            // 入城完成初始化（Render / 战法 / 名单 / 属性都在 Init 里做）
            src.EnsureTroop(troop, scenario);

            // 编制上限要等 Init 里的 CalculateAttribute 之后才有效
            if (troop.MaxTroops > 0 && troop.troops > troop.MaxTroops)
                troop.troops = troop.MaxTroops;

            // 扣粮 + 扣兵装（原版由 deploy 参数带走：`equipment_quantity[0] = troops`）
            src.food -= food;
            if (src.itemStore != null && bestKind >= 0)
                src.itemStore.Remove(bestKind, troops);

            // 【下令】原版 `unit_reinforce`：记 request_force_id + `UnitOrderId_Attack` + run_order。
            // 部队在出兵城组建后**自己行军**过去 —— 不能用 UpdateCell 瞬移到目标城，
            // 那等于"空降"，与原版"从 src 行军过去"完全不同。
            troop.SetMission(MissionType.TroopOccupyCity, dest.Id);

            // 打上"响应谁"的标记（原版 `set_request_force_id(force_id)`，`:1361`）：
            // 请求方灭亡时据此把这支援军撤回（见 CancelByRuinedForce）。
            troop.requestForceId = requester.Id;

            // 【立即出城】原版的 `unit_reinforce` 是 `unit_set_order + run_order`：
            // 下令当刻就跑一次，部队不会停在城头等下一个回合
            // （s11_sys_scen_unit.cpp:1355-1370）。本项目移动是异步渲染，等价于推一次 DoAI。
            if (!CityTroopFactory.DriveOutOfCity(troop, scenario))
            {
                Sango.Log.Warning($"@外交@{ally.Name} 在 {src.Name} 组建的援军无法出城（城头无可停留格或寻路失败），本次履约取消");
                CityTroopFactory.RevokeEmit(src, troop);
                return false;
            }

            Sango.Log.Info($"@外交@{ally.Name} 履约派出援军：{leader.Name} 率 {troop.troops} 兵" +
                           $"（粮 {food}）自 {src.Name} 出击 {dest.Name}（受援方 {requester.Name}）");
            return true;
        }

        /// <summary>
        /// 请求方灭亡时撤回援军 —— 对应原版 `unit_cancel_request_ruin`
        /// （`s11_sys_scen_unit.cpp:1329-1353`）：
        /// 清掉 `request_force_id`、取消当前命令、让部队退回自己的城。
        ///
        /// 【为什么需要】援军是"欠请求方的人情"才出动的，请求方一灭亡，
        /// 这支部队就没有继续在外作战的理由；原版会把它撤回老家，
        /// 而不是留在原地继续打（可能正在打的是自己盟友的敌人）。
        /// </summary>
        /// <param name="ruinedForceId">灭亡的势力 Id</param>
        /// <param name="scenario">场景</param>
        public static void CancelByRuinedForce(int ruinedForceId, Scenario scenario)
        {
            if (ruinedForceId < 0 || scenario == null || scenario.troopsSet == null)
                return;

            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || !troop.IsAlive) continue;
                if (troop.requestForceId != ruinedForceId) continue;

                troop.requestForceId = -1;
                troop.ClearMission();

                if (troop.BelongCity != null && troop.BelongCity.IsAlive)
                    troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);

                Sango.Log.Info($"@外交@{troop.Name} 的援军对象（势力 {ruinedForceId}）已灭亡，撤回原城");
            }
        }

        /// <summary>
        /// 自动挑攻击目标（对应原版请求方在提案时指定的 `self->dest`）：
        /// 从请求方各城的**相邻敌城**里取守军最少的 —— 援军打成算帮忙，打不下也不至于赔光。
        /// </summary>
        static City FindAttackTarget(Force requester, Force ally)
        {
            if (requester == null) return null;

            City best = null;
            int bestTroops = int.MaxValue;

            requester.ForEachCity(c =>
            {
                if (c == null || !c.IsAlive || c.NeighborList == null) return;

                for (int i = 0; i < c.NeighborList.Count; i++)
                {
                    City nb = c.NeighborList[i];
                    if (nb == null || !nb.IsAlive || nb.BelongForce == null) continue;
                    if (nb.BelongForce == requester) continue;
                    if (ally != null && nb.BelongForce == ally) continue;
                    if (requester.IsAlliance(nb.BelongForce)) continue;

                    if (nb.troops < bestTroops)
                    {
                        bestTroops = nb.troops;
                        best = nb;
                    }
                }
            });

            return best;
        }

        /// <summary>
        /// 选主将 —— 原版 `create_reinforce_deploy_param`（`:200-225`）：
        /// 先算据点内所有武将（君主/都督/太守/一般）的「统率+武力+智力」均值，
        /// 再从**空闲武将**里挑与该均值最接近的一个；排除君主与守将，平局比 Id。
        /// </summary>
        static Person PickLeader(City src, Force ally)
        {
            if (src.allPersons == null || src.allPersons.Count == 0)
                return null;

            int sum = 0;
            for (int i = 0; i < src.allPersons.Count; i++)
            {
                Person p = src.allPersons[i];
                if (p == null) continue;
                sum += (p.Command + p.Strength + p.Intelligence) / 3;
            }

            int avg = sum / src.allPersons.Count;

            Person best = null;
            int bestDiff = int.MaxValue;

            for (int i = 0; i < src.freePersons.Count; i++)
            {
                Person p = src.freePersons[i];
                if (p == null || !p.IsAlive) continue;

                // 原版 `can_receive_order`：带兵 / 在途任务 / 被俘 都不能接令
                if (!p.IsFree) continue;

                if (ally != null && p == ally.mGovernor) continue;   // 君主不外出
                if (p.Id == src.guardId) continue;                   // 守将不抽走（原版 :214-217）

                int stat = (p.Command + p.Strength + p.Intelligence) / 3;
                int diff = stat > avg ? stat - avg : avg - stat;

                if (diff < bestDiff || (diff == bestDiff && best != null && p.Id < best.Id))
                {
                    bestDiff = diff;
                    best = p;
                }
            }

            return best;
        }
    }
}
