/*
 * 文件名：CityTroopFactory.cs
 * 描述：城池 AI 出兵的公共收尾逻辑。
 *       把「登记部队 → 设为当前活动部队 → 刷新渲染」这一在 CityAI 中重复出现
 *       8 次以上的样板集中到一处，避免遗漏与重复维护。
 */

namespace Sango.Core
{
    /// <summary>
    /// 城池 AI 出兵工厂。
    /// </summary>
    public static class CityTroopFactory
    {
        /// <summary>
        /// 出征收尾：把已组建的部队登记进场景、标记为城市当前活动部队并刷新渲染。
        ///
        /// 同时按任务确定部队角色（<see cref="TroopRole"/>），使同一任务下的部队
        /// 因角色不同而采取不同的作战风格（攻坚 / 防守 / 骚扰 / 游击）。
        /// </summary>
        /// <param name="city">出兵城市</param>
        /// <param name="troop">已组建的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>登记后的部队（troop 为 null 时返回 null）</returns>
        public static Troop EmitTroop(City city, Troop troop, Scenario scenario)
        {
            if (troop == null || city == null || scenario == null)
                return null;

            // 【出城队列】先入队打"新兵"标记，再登记进场景。
            //
            // 为什么必须在这里登记（不能推迟到 ActivatePendingOutTroops）：
            // 所有调用方在 EmitTroop 之后立刻就要用这支部队 —— troop.SetMission(...)、
            // troop.NeedPrepareMission()、读 troop.Leader / troop.BelongForce 打日志。
            // 这些成员由 Init 建立（BelongForce、Leader、Render、战法、属性），
            // 未登记的部队 BelongForce 为 null，SetMission 会直接 return（s11 任务根本设不上）。
            //
            // 因此"同一城池中心格只能站一支"不能靠"不登记"来解决，而是：
            //   · EnsureTroop 不抢占已被别的部队占用的 CenterCell.troop（见该方法）；
            //   · 多支部队同时停在城内属于允许的过渡态，由队列逐支送出城。
            EnqueueOutTroop(city, troop);
            city.EnsureTroop(troop, scenario);

            city.CurActiveTroop = troop;

            // 【分级策略】出征时固化部队角色：Auto 时依据任务与属性自动推导一次，
            // 避免每回合重算导致角色漂移、行为抖动。
            if (troop.role == TroopRole.Auto)
                troop.role = troop.ResolveRole();

            return troop;
        }

        /// <summary>
        /// 把"已组建、尚未出城"的部队加入出城队列（去重）。
        ///
        /// 只打标记，不做任何地图操作：出城统一由 <see cref="ActivatePendingOutTroops"/>
        /// 按队列顺序推进（队首走完才轮到下一支）。登记仍由调用方负责 ——
        /// <see cref="EmitTroop"/> 在入队后立即 <see cref="City.EnsureTroop"/>，
        /// 保证调用方能继续设置任务。
        /// </summary>
        /// <param name="city">出兵城市</param>
        /// <param name="troop">刚组建的部队</param>
        public static void EnqueueOutTroop(City city, Troop troop)
        {
            if (city == null || troop == null)
                return;

            if (!city.pendingOutTroops.Contains(troop))
                city.pendingOutTroops.Add(troop);

            troop.isNewEmitted = true;
            troop.ActionOver = false;
        }

        /// <summary>
        /// 依次激活出城队列：出一支、走一支，直到队列空或队首被卡住。
        ///
        /// 【流程】对队首部队：
        /// <list type="number">
        /// <item>未登记 → <see cref="City.EnsureTroop"/> 放到城池中心格（分配 Id / Init）；</item>
        /// <item>被完全围困（<see cref="TroopAIUtility.CanLeaveBuildingCell"/> 为假）
        ///       → <see cref="RevokeEmit"/> 回退进城，继续下一支；</item>
        /// <item>推一次 <see cref="Troop.DoAI"/>：离开城池格即视为出城成功，出队继续；
        ///       仍停在城格 → 保留队首，返回 false（下一轮再推）。</item>
        /// </list>
        /// 这样同一回合内可以连续派多支，而不是被"一支部队占着城头"卡住。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>队列已清空返回 true；队首尚未走出城返回 false</returns>
        public static bool ActivatePendingOutTroops(City city, Scenario scenario)
        {
            if (city == null || scenario == null)
                return true;

            while (city.pendingOutTroops.Count > 0)
            {
                Troop troop = city.pendingOutTroops[0];

                if (troop == null || !troop.IsAlive)
                {
                    city.pendingOutTroops.RemoveAt(0);
                    continue;
                }

                // ① 未登记 → 放到城池中心格并入场景
                if (troop.cell == null)
                {
                    city.EnsureTroop(troop, scenario);
                }
                else if (troop.cell.building == null)
                {
                    // 已在野外（事件直接摆放）→ 无需出城
                    city.pendingOutTroops.RemoveAt(0);
                    troop.isNewEmitted = false;
                    continue;
                }

                // ② 被完全围死 → 回退进城（EnterCity 会经 ClearNewEmittedFlag 自动出队）
                if (!TroopAIUtility.CanLeaveBuildingCell(troop, scenario))
                {
                    Sango.Log.Warning($"势力[{city.BelongForce?.Name}]在{city.Name}出兵失败:城池被完全围困,部队无法出城,已回退进城");
                    RevokeEmit(city, troop);
                    continue;
                }

                // ③ 推一次出城
                troop.DoAI(scenario);

                // 已站到野外格 → 出城成功，让位给下一支
                if (troop.cell != null && troop.cell.building == null)
                {
                    city.pendingOutTroops.Remove(troop);
                    troop.isNewEmitted = false;
                    continue;
                }

                // 行动已结束却仍停在城格：DoAI 正常收尾（不是命令失败），
                // 说明本回合没能挪出去（寻路为空 / 移动力耗尽）。
                // 此时**不能**回退进城解散 —— 那是 run_order 失败才做的处理
                // （s11_sys_person_order_military.cpp:337-352）；这里只需让它出队，
                // 部队留到下一回合由任务行为自己继续出城。
                if (troop.ActionOver)
                {
                    Sango.Log.Warning($"势力[{city.BelongForce?.Name}]在{city.Name}组建的部队本回合未能走出城头,下一回合继续出城");
                    city.pendingOutTroops.Remove(troop);
                    troop.isNewEmitted = false;
                    continue;
                }

                // 移动动画还没播完 → 保留队首，下一轮继续推（后面的部队继续排队）
                return false;
            }

            return true;
        }

        /// <summary>
        /// 组建后立即推进一次行动，把部队从城市格上带出来。
        ///
        /// 【为什么需要】项目硬规则：部队不允许停留在城市格
        /// （<see cref="Cell.CanStay"/> 要求目标格既无部队也无建筑）。
        /// 而 <see cref="City.EnsureTroop"/> 只能把部队临时放在城市中心格，
        /// 真正的出城要等部队自己的行动循环。若组建发生在"部队行动阶段之后"
        /// （据点 AI / 外交履约等），这支部队会整整停留一个回合，玩家看到的就是
        /// "部队杵在城头不动"。
        ///
        /// 【原版依据】Open311 的 com_deploy 在 unit_set_order 之后立刻跑一次该命令：
        /// <code>
        /// s11_sys_person_order_military.cpp:331-353
        ///     system_->unit_set_order(unit, self->order, self->target_pos, self->target);
        ///     if (not system_->run_order(unit))
        ///     {
        ///         district_add_action_point(district, action_point_cost);  // 退回行动力
        ///         unit_enter(unit, self->building);                        // 部队回城解散
        ///         person->set_action_done(false);                          // 恢复武将行动
        ///         return -1;
        ///     }
        /// </code>
        /// 本方法与 <see cref="RevokeEmit"/> 合起来等价于上面这段。
        ///
        /// 【谁推 DoAI】本方法入队后立刻就地推进一次队列
        /// （<see cref="ActivatePendingOutTroops"/>），由队列负责"按顺序逐支送出城"：
        ///   · 队首已登记 → 直接推一次 <see cref="Troop.DoAI"/> 开始移动；
        ///   · 队首还在播移动动画 → 队列保留它，返回值不影响本次组建；
        ///   · 队首被围死 → <see cref="RevokeEmit"/> 回退进城后继续下一支。
        /// 之后军团调度器 <see cref="CorpsAI.AITroops"/> 仍会把新兵排第一继续续跑，
        /// 两条路径都幂等（靠 <see cref="Troop.ActionOver"/> / <see cref="Troop.isNewEmitted"/>
        /// 跳过已处理项），不会重复结算。
        /// </summary>
        /// <param name="troop">刚组建、仍停在城市格上的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>
        /// 部队仍在队列中或已出城返回 true；
        /// 本次组建被队列判定"无法出城"并回退进城（<see cref="RevokeEmit"/> 已把 IsAlive 置 false）
        /// 返回 false，调用方据此退回行动力 / 取消本次出兵。
        /// </returns>
        public static bool DriveOutOfCity(Troop troop, Scenario scenario)
        {
            if (troop == null || scenario == null)
                return false;
            if (!troop.IsAlive)
                return true;
            // 已在野外（例如事件直接把部队放到无建筑格）→ 无需处理
            if (troop.cell != null && troop.cell.building == null)
                return true;

            City city = troop.BelongCity;
            if (city == null && troop.cell != null)
                city = troop.cell.building as City;
            if (city == null)
                return true;

            // 【出城队列】入队 + 打"新兵"标记，**不拒绝后续部队**：
            // 前面已有部队在出城时，本部队排在队尾，等前一支落地后自动接上，
            // 从而支持同一回合连续派多支。
            EnqueueOutTroop(city, troop);

            // 就地推进一次，让队首立刻开始出城（不必等下一帧的军团调度）
            ActivatePendingOutTroops(city, scenario);

            // 被队列回退（围死 / 走不出城头）时 IsAlive 已被 EnterCity 置 false。
            // 注意：本部队不是队首时既没出城也没被撤销，IsAlive 仍为 true —— 它只是排队，
            // 调用方照常扣行动力，后续由队列负责把它送出城。
            return troop.IsAlive;
        }

        /// <summary>
        /// 撤销一次已登记但未能出发的组建：部队进城解散，兵力 / 钱粮 / 兵装原样退回，
        /// 成员放回空闲名单并恢复行动。
        ///
        /// 对应 <c>s11_sys_person_order_military.cpp:337-352</c> 的失败回退分支
        /// （<c>run_order</c> 失败 → 退行动力 / <c>unit_enter</c> / <c>set_action_done(false)</c>）。
        /// 行动力由调用方负责退回（本方法不持有据点行动力上下文）。
        /// </summary>
        /// <param name="city">出兵城市</param>
        /// <param name="troop">需要撤销的部队</param>
        public static void RevokeEmit(City city, Troop troop)
        {
            if (city == null || troop == null)
                return;
            // 已在别处收尾（Clear 过）的部队不再回退：EnterCity 会重复结算
            if (!troop.IsAlive)
                return;

            CancelEmitTroop(city, troop);
        }

        /// <summary>
        /// 出征前置判断失败时回退：先收集成员，再进城返还兵力 / 钱粮 / 兵装，
        /// 最后把成员放回城市的空闲名单（与 CityExpedition.OnBack 的取消约定一致）。
        /// </summary>
        /// <param name="city">出兵城市</param>
        /// <param name="troop">已登记但需取消的部队</param>
        static void CancelEmitTroop(City city, Troop troop)
        {
            // 必须在进城解散之前收集成员：EnterCity 会 Clear 部队，之后无法再遍历成员
            System.Collections.Generic.List<Person> members = new System.Collections.Generic.List<Person>();
            troop.ForEachPerson(person => members.Add(person));

            troop.EnterCity(city);

            foreach (Person person in members)
            {
                // 幂等：部分调用方（如补给队）在 EmitTroop 之后才从 freePersons 摘人，
                // 取消时该成员可能仍在名单里，重复 Add 会产生重复引用。
                if (!city.freePersons.Contains(person))
                    city.freePersons.Add(person);
                person.ActionOver = false;
            }
        }
    }
}
