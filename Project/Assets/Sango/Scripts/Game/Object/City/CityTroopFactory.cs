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
            if (troop == null || city == null)
                return null;

            troop = city.EnsureTroop(troop, scenario);

            // 【前置判断】出征前先确认部队能否走出城头：城池被完全围困
            // （移动范围内没有可停留的空地）时，部队一出征就会卡在城头无法行动。
            // 此时取消本次出征，退回组建成本并返回 null，让调用方跳过后续日志 / 任务。
            if (!TroopAIUtility.CanLeaveBuildingCell(troop, scenario))
            {
                Sango.Log.Warning($"势力[{city.BelongForce?.Name}]在{city.Name}出兵失败:城池被完全围困,部队无法出城,已取消本次出征");
                CancelEmitTroop(city, troop);
                return null;
            }

            city.CurActiveTroop = troop;
            city.Render?.UpdateRender();

            // 【分级策略】出征时固化部队角色：Auto 时依据任务与属性自动推导一次，
            // 避免每回合重算导致角色漂移、行为抖动。
            if (troop.role == TroopRole.Auto)
                troop.role = troop.ResolveRole();

            return troop;
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
        /// 【一次 DoAI 就是整条命令】本项目 <see cref="Troop.DoAI"/> 不是"走一步"：
        /// 它进到任务行为后由 <see cref="Troop.MoveTo"/> 在首次调用时一次性把
        /// **整条路径**的 <c>TroopMoveEvent</c> 全部排进渲染队列（Troop.cs:1941-1968），
        /// 部队随后会一路走到终点。所以这里调一次就等于原版跑完整条命令，
        /// 不需要（也不能）循环调用 —— 循环会重复执行同一个动作。
        ///
        /// 【判据】移动是异步渲染，<c>troop.cell</c> 要等动画播完才更新。
        /// 因此"已经出发"的判据是 **isMoving 为真** 或 **已不在建筑格**，两者满足其一即可，
        /// 不能只看 <see cref="Troop.DoAI"/> 的返回值 —— 寻路失败时它同样返回 true。
        /// </summary>
        /// <param name="troop">刚组建、仍停在城市格上的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>已出发（移动中或已在野外）返回 true；仍留在城头、或当刻就被任务链路收尾，返回 false</returns>
        public static bool DriveOutOfCity(Troop troop, Scenario scenario)
        {
            if (troop == null || scenario == null)
                return false;
            // 已经不在场上（被任务链路收尾 / 事件直接放到野外无格）→ 无需处理
            if (!troop.IsAlive || troop.cell == null)
                return true;
            // 不在建筑格上（例如事件直接把部队放到野外）→ 无需处理
            if (troop.cell.building == null)
                return true;

            // 只推一次：这一次已把整条路径排进渲染队列（见方法注释），重复调用会重复执行同一个动作
            troop.DoAI(scenario);

            // 任务链路可能当刻就把部队收尾（例如目标城已失效 → UpdateAIOrder 改判返城 →
            // TroopMovetoCity 抵达本城 → EnterCity 解散）。这是"没出发就结束了"，
            // 必须回报 false 让调用方走撤销分支（不扣行动力）；
            // 物资已由 EnterCity 退回城池，RevokeEmit 会识别 !IsAlive 并跳过重复结算。
            if (!troop.IsAlive || troop.cell == null)
                return false;
            if (troop.isMoving)
                return true;
            if (troop.cell.building == null)
                return true;

            return false;
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
