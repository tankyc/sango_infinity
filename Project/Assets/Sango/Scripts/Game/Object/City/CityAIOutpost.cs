/*
 * 文件名：CityAIOutpost.cs
 * 描述：据点级 AI —— 按据点状态生成固定命令队列（CityAI 的 partial 分支）。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 据点主流程   —— s11_game_ai_kyoten.cpp:5-23      据点主流程
 *   · 据点数据装载 —— s11_game_ai_kyoten.cpp:25-38     据点数据装载
 *   · 命令队列生成 —— s11_game_ai_kyoten.cpp:40-228    据点命令表生成
 *   · 命令队列执行 —— s11_game_ai_kyoten_cmd.cpp:5-76  据点命令调度
 *
 * 这是整个 AI 行为差异最大的一处：原版据点不做"给每条命令打分排序"，
 * 而是**按据点状态查一张写死的命令表**，然后照表顺序逐条尝试；每条命令自己判断做不做得了。
 * 谁先谁后完全由表决定，不受个性 / 态势打分影响。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public partial class CityAI
    {
        /// <summary>
        /// 据点 AI 主流程（对齐 s11_game_ai_kyoten.cpp:5-23）。
        /// <para>据点数据装载 → 据点命令表生成 → 据点命令调度。</para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool DoOutpostAI(City city, Scenario scenario)
        {
            if (city == null || scenario == null || city.BelongForce == null)
                return true;

            // 兜底：没有经过军团调度（CorpsAIDistrict.AIDistrictCommand 会把预算算好放进来）时，
            // 给一个最小额度，避免所有命令都被行动力门禁挡掉而"整城不作为"。
            // 原版对应的是 outpost[i].action_point 在 update_district_data 里至少给都市 20 点
            // （s11_game_ai_district.cpp:122-124）。
            if (OutpostRuntime.corps == null && OutpostRuntime.actionPoint <= 0)
                OutpostRuntime.actionPoint = 20;

            LoadOutpostData(city, scenario);
            UpdateOutpostData(city, scenario);

            if (AIConfig.Instance.logOutpostAI)
            {
                OutpostInfo logInfo = OutpostRuntime.Get(city.Id);
                Sango.Log.Info($"*[据点AI]{city.Name} 状态{logInfo.state} 可用行动力{OutpostRuntime.actionPoint} " +
                    $"金{OutpostRuntime.gold} 粮{OutpostRuntime.food} 可下令人{OutpostRuntime.idlePersonList.Count} " +
                    $"命令[{string.Join(",", OutpostRuntime.orderQueue)}]");
            }

            return OutpostCommand(city, scenario);
        }

        /// <summary>
        /// 装载据点数据：可下令武将、可用金、可用粮。
        ///
        /// 对齐出处：s11_game_ai_kyoten.cpp:25-38 据点数据装载。
        /// <code>
        /// idle_person_list_ = 可下令人列表(据点 Id);
        /// gold_ = building->get_gold();  if (gold_income &lt; 0) gold_ += gold_income * 6;
        /// food_ = building->get_food();  if (food_income &lt; 0) food_ += food_income * 2;
        /// order_count_ = 0;
        /// </code>
        /// 语义：收支为负时，可用资源按"撑到储备线"折算，避免 AI 明明会赤字还在花钱。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        public static void LoadOutpostData(City city, Scenario scenario)
        {
            OutpostRuntime.idlePersonList.Clear();

            // 【守将排除】可下令池里不能包含本据点的守将。
            // 对齐 ai_table_get_kyoten_dispatchable_person_list（s11_sys_scen_ai.cpp:422-431）：
            //   is_dispatchable = 可接令 && 不是本据点的 `guard`（s11_game_ai_force.cpp:214-219）。
            // 原实现直接装 city.freePersons 全量，导致守将会被 MOV / 运输命令调走，防务出现空缺。
            int guardId = -1;
            Force force = city.BelongForce;
            if (force != null && force.guardPersonIds != null)
                force.guardPersonIds.TryGetValue(city.Id, out guardId);

            // 原版的可下令人列表接口返回副本，命令体在里面 Remove
            // 来保证"同一个人不会被同一据点的两条命令重复使用"。
            for (int i = 0; i < city.freePersons.Count; i++)
            {
                Person p = city.freePersons[i];
                if (p == null || !p.IsAlive || !p.IsFree)
                    continue;
                if (p.Id == guardId)
                    continue;
                OutpostRuntime.idlePersonList.Add(p);
            }

            OutpostInfo info = OutpostRuntime.Get(city.Id);

            // 原版 idle_person_count 在 势力据点态势装载 里算（s11_game_ai_force.cpp:174-175），
            // 而 idle_person_list_ 在 据点数据装载 里实时取（s11_game_ai_kyoten.cpp:29）。
            // 两者之间可能隔着若干条命令的执行（人已被派走），这里以实时列表为准回写，
            // 避免用陈旧计数生成一个"没人可派"的满队列。
            info.idlePersonCount = OutpostRuntime.idlePersonList.Count;

            OutpostRuntime.gold = city.gold;
            if (info.goldIncome < 0)
                OutpostRuntime.gold += info.goldIncome * OutpostRuntime.GoldReserveTurn;

            OutpostRuntime.food = city.food;
            if (info.foodIncome < 0)
                OutpostRuntime.food += info.foodIncome * OutpostRuntime.FoodReserveTurn;

            OutpostRuntime.orderQueue.Clear();
        }

        /// <summary>
        /// 生成据点命令队列。
        ///
        /// 对齐出处：s11_game_ai_kyoten.cpp:40-228 据点命令表生成。
        /// 分支顺序严格照搬：
        ///   ① 据点被禁用势力 AI → 只做防御
        ///   ② 三格内有敌军 → 防御 + 都市的募兵/治安/生产/拆除，然后结束
        ///   ③ 没有可下令武将 → 空队列
        ///   ④ 都市 / 关隘港口 各按 Rear / Middle / Front / Battle 四张固定表装填
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        public static void UpdateOutpostData(City city, Scenario scenario)
        {
            OutpostInfo info = OutpostRuntime.Get(city.Id);
            List<AIOrderId> q = OutpostRuntime.orderQueue;

            // ① 三格内有敌军：无条件防御优先 —— s11_game_ai_kyoten.cpp:50-61
            if (info.existEnemy3)
            {
                q.Add(AIOrderId.Defense);
                if (city.IsCity())
                {
                    q.Add(AIOrderId.Recruit);
                    q.Add(AIOrderId.Patrol);
                    q.Add(AIOrderId.Produce);
                    q.Add(AIOrderId.Destroy);
                }
                return;
            }

            // ② 没有可下令武将 —— s11_game_ai_kyoten.cpp:62-63
            if (info.idlePersonCount <= 0)
                return;

            if (city.IsCity())
            {
                // ---- 都市四态表 —— s11_game_ai_kyoten.cpp:65-165 ----
                switch (info.state)
                {
                    case OutpostState.Rear:
                        q.Add(AIOrderId.Patrol);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.SearchMax);
                        q.Add(AIOrderId.EmployMax);
                        q.Add(AIOrderId.EmployEnemyMax);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Merchant);
                        q.Add(AIOrderId.Strategy);
                        q.Add(AIOrderId.Produce);
                        q.Add(AIOrderId.Transport);
                        q.Add(AIOrderId.Support);
                        q.Add(AIOrderId.Move);
                        q.Add(AIOrderId.MerchantExtra);
                        q.Add(AIOrderId.Recruit);
                        q.Add(AIOrderId.Drill);
                        q.Add(AIOrderId.Destroy);
                        q.Add(AIOrderId.Develop);
                        q.Add(AIOrderId.Merge);
                        q.Add(AIOrderId.Search);
                        q.Add(AIOrderId.Employ);
                        q.Add(AIOrderId.EmployEnemy);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Strategy);
                        break;

                    case OutpostState.Middle:
                        q.Add(AIOrderId.Attack);
                        q.Add(AIOrderId.Transport);
                        q.Add(AIOrderId.Support);
                        q.Add(AIOrderId.Move);
                        q.Add(AIOrderId.SearchMax);
                        q.Add(AIOrderId.EmployMax);
                        q.Add(AIOrderId.EmployEnemyMax);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Recruit);
                        q.Add(AIOrderId.Patrol);
                        q.Add(AIOrderId.Strategy);
                        q.Add(AIOrderId.MerchantExtra);
                        q.Add(AIOrderId.Merchant);
                        q.Add(AIOrderId.Produce);
                        q.Add(AIOrderId.Drill);
                        q.Add(AIOrderId.Destroy);
                        q.Add(AIOrderId.Develop);
                        q.Add(AIOrderId.Merge);
                        q.Add(AIOrderId.Search);
                        q.Add(AIOrderId.Employ);
                        q.Add(AIOrderId.EmployEnemy);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Strategy);
                        break;

                    case OutpostState.Front:
                        q.Add(AIOrderId.Attack);
                        q.Add(AIOrderId.Build);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.SearchMax);
                        q.Add(AIOrderId.EmployMax);
                        q.Add(AIOrderId.EmployEnemyMax);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Recruit);
                        q.Add(AIOrderId.Patrol);
                        q.Add(AIOrderId.Drill);
                        q.Add(AIOrderId.Strategy);
                        q.Add(AIOrderId.MerchantExtra);
                        q.Add(AIOrderId.Merchant);
                        q.Add(AIOrderId.Produce);
                        q.Add(AIOrderId.Destroy);
                        q.Add(AIOrderId.Develop);
                        q.Add(AIOrderId.Merge);
                        q.Add(AIOrderId.Transport);
                        q.Add(AIOrderId.Move);
                        q.Add(AIOrderId.Support);
                        q.Add(AIOrderId.Search);
                        q.Add(AIOrderId.Employ);
                        q.Add(AIOrderId.EmployEnemy);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Strategy);
                        break;

                    case OutpostState.Battle:
                        q.Add(AIOrderId.Defense);
                        q.Add(AIOrderId.Destroy);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Recruit);
                        q.Add(AIOrderId.Drill);
                        q.Add(AIOrderId.Merchant);
                        q.Add(AIOrderId.Patrol);
                        q.Add(AIOrderId.Produce);
                        q.Add(AIOrderId.Strategy);
                        q.Add(AIOrderId.MerchantExtra);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Search);
                        q.Add(AIOrderId.Employ);
                        q.Add(AIOrderId.EmployEnemy);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Strategy);
                        break;
                }
            }
            else
            {
                // ---- 关隘 / 港口四态表 —— s11_game_ai_kyoten.cpp:166-226 ----
                // 原版注释明确：关隘港口不能执行运输命令（s11_game_ai_kyoten.cpp:169-171），
                // 但表里仍然排了 Transport（原版遗留），由命令体自己 return。
                switch (info.state)
                {
                    case OutpostState.Rear:
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Transport);
                        q.Add(AIOrderId.Support);
                        q.Add(AIOrderId.Move);
                        q.Add(AIOrderId.Drill);
                        q.Add(AIOrderId.Strategy);
                        q.Add(AIOrderId.Search);
                        q.Add(AIOrderId.Employ);
                        q.Add(AIOrderId.EmployEnemy);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Strategy);
                        break;

                    case OutpostState.Middle:
                        q.Add(AIOrderId.Attack);
                        q.Add(AIOrderId.Transport);
                        q.Add(AIOrderId.Support);
                        q.Add(AIOrderId.Move);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Drill);
                        q.Add(AIOrderId.Strategy);
                        q.Add(AIOrderId.Search);
                        q.Add(AIOrderId.Employ);
                        q.Add(AIOrderId.EmployEnemy);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Strategy);
                        break;

                    case OutpostState.Front:
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Attack);
                        q.Add(AIOrderId.Build);
                        q.Add(AIOrderId.Drill);
                        q.Add(AIOrderId.Transport);
                        q.Add(AIOrderId.Move);
                        q.Add(AIOrderId.Support);
                        q.Add(AIOrderId.Strategy);
                        q.Add(AIOrderId.Search);
                        q.Add(AIOrderId.Employ);
                        q.Add(AIOrderId.EmployEnemy);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Strategy);
                        break;

                    case OutpostState.Battle:
                        q.Add(AIOrderId.Defense);
                        q.Add(AIOrderId.Drill);
                        q.Add(AIOrderId.Transport);
                        q.Add(AIOrderId.Move);
                        q.Add(AIOrderId.Transport);
                        q.Add(AIOrderId.Bonus);
                        q.Add(AIOrderId.Strategy);
                        break;
                }
            }
        }

        /// <summary>
        /// 执行据点命令队列。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd.cpp:5-76 据点命令调度。
        /// <para>
        /// 【关键差异】原版对每条命令的返回值**不作判断**，一律继续下一条：
        /// <c>(this-&gt;*ai_order_table_[order_id])(building);</c>
        /// 本项目既有的 CityAI 命令返回 false 表示"本步没做完，下次接着做"，
        /// 如果直接沿用会让队列在第一处 false 就断掉，所以这里统一用 <see cref="IgnoreResult"/> 包装，
        /// 保持"照表跑完"的原版语义。
        /// </para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool OutpostCommand(City city, Scenario scenario)
        {
            List<AIOrderId> q = OutpostRuntime.orderQueue;
            for (int i = 0; i < q.Count; i++)
            {
                AIOrderId orderId = q[i];
                Func<City, Scenario, bool> action = GetOrderAction(orderId);
                if (action == null)
                    continue;

                // 行动力门禁：原版每条据点命令开头都有
                //   if (action_point_ < person_order_->get_xxx_action_point_cost()) return true;
                // 见 s11_game_ai_kyoten_cmd.cpp:100-101 / 197-198 / 264-265 / 574-575 等。
                int cost = GetOrderActionPointCost(orderId, city);
                if (cost > 0 && OutpostRuntime.actionPoint < cost)
                    continue;

                OutpostRuntime.commandExecuted = false;
                OutpostRuntime.delegatedExecuted = false;
                action(city, scenario);

                // 【行动力扣减：按命令类型分流】
                // 原版每条据点命令都在自己体内执行 `action_point_ -= <自身成本>`
                // （如 s11_game_ai_kyoten_cmd_personnel.cpp:31、mil_transport.cpp:192），
                // 调度器 `kyoten_command` 只负责照表调用，不参与扣减。
                // <para>
                // 本项目的据点命令因此分两类：
                //   · **自扣型**（Move / Summon / Transport / 计策 / 拆建筑…）——
                //     命令体内已 `actionPoint -=` 并 `MarkExecuted()`，调度器**不再扣**，
                //     否则成本翻倍、队列比原版早停一半。
                //   · **委托型**（Develop / Recruit / Patrol / Drill / Attack / Build /
                //     Search / Employ / Bonus / Produced / Merchant / Merge）——
                //     由 `CityAI` 的通用实现承担，那是给非据点场景写的，不会动 `actionPoint`。
                //     它们由 <see cref="IgnoreResult"/> 标记 `delegatedExecuted`，这里补扣一次，
                //     否则行动力门禁对这批命令永久放行，等于无限行动力。
                // </para>
                // 注意：不与 corps.ActionPoint 联动 —— 原版 action_point_ 只是本据点这一批命令的
                // 额度上限（由 district_command 每次重新计算）。
                if (!OutpostRuntime.commandExecuted
                    && OutpostRuntime.delegatedExecuted
                    && cost > 0)
                {
                    OutpostRuntime.actionPoint -= cost;
                }
            }

            q.Clear();
            OutpostRuntime.idlePersonList.Clear();
            return true;
        }

        /// <summary>
        /// 命令编号 → 本项目执行体。对应原版 <c>ai_order_table_</c>（s11_game_ai.cpp:11-40）。
        /// <para>
        /// 映射说明（原版命令 → 本项目落点）：
        /// Develop→AIIntrior、Recruit→AIRecruitTroop、Produce→AICreateItems/Machine/Boat、
        /// Patrol→AISecurity、Merchant/MerchantExtra→AITradeFood、Drill→AITrainTroop、
        /// 当前映射（24 条命令，覆盖原版全部有执行体的编号）：
        /// Develop→AIIntrior、Recruit→AIRecruitTroop、Produce→ProduceAny、Patrol→AISecurity、
        /// Merchant/MerchantExtra→AITradeFood、Drill→AITrainTroop、Defense/Attack→AIAttack、
        /// Build→AIBuildMilitaryBuilding、Support→AISupport、Search→AISearching、SearchMax→AISearchMax、
        /// Employ→AIRecruitPerson、EmployMax→AIEmployMax、EmployEnemy→AIEmployEnemy、
        /// EmployEnemyMax→AIEmployEnemyMax、Bonus→AIRewardPerson、Destroy→AIDestroy、Merge→AIMerge、
        /// Move→AIMove、Summon→AISummon、Transport→AITransport、Strategy→AIStrategy。
        /// <para>
        /// Disrupt / Collaborate / Rumor 不是队列项：原版 <c>kyoten_strategy</c> 会先过一道概率闸门，
        /// 再在这三者中随机挑一个执行，因此它们由 <c>AIStrategy</c> 内部调用，不单独注册。
        /// 其中 Collaborate（驱虎吞狼）在本项目尚无对应的计略行动类，为唯一空壳分支。
        /// </para>
        /// </summary>
        /// <param name="orderId">命令编号</param>
        /// <returns>执行委托；无对应实现时返回 null</returns>
        static Func<City, Scenario, bool> GetOrderAction(AIOrderId orderId)
        {
            switch (orderId)
            {
                case AIOrderId.Develop: return IgnoreResult(AIIntrior);
                case AIOrderId.Recruit: return IgnoreResult(AIRecruitTroop);
                // 【统一用 IgnoreResult 包装】
                // 委托给 CityAI / 内部实现的命令不会自行扣行动力，必须靠这层包装把
                // "确实执行了动作"转成 OutpostRuntime.delegatedExecuted，调度器才会补扣。
                // 早期只有部分 case 包了，`Produce / Merchant / Support / SearchMax /
                // EmployMax / EmployEnemy* / Merge` 这几个漏包 → 行动力门禁对它们永久放行。
                case AIOrderId.Produce: return IgnoreResult(ProduceAny);
                case AIOrderId.Patrol: return IgnoreResult(AISecurity);
                case AIOrderId.Merchant: return IgnoreResult(AIMerchant);
                case AIOrderId.MerchantExtra: return IgnoreResult(AIMerchantExtra);
                case AIOrderId.Drill: return IgnoreResult(AITrainTroop);
                case AIOrderId.Defense: return IgnoreResult(AIAttack);
                case AIOrderId.Attack: return IgnoreResult(AIAttack);
                case AIOrderId.Build: return IgnoreResult(AIBuildMilitaryBuilding);
                case AIOrderId.Support: return IgnoreResult(AISupport);
                case AIOrderId.Search: return IgnoreResult(AISearching);
                case AIOrderId.SearchMax: return IgnoreResult(AISearchMax);
                case AIOrderId.Employ: return IgnoreResult(AIRecruitPerson);
                case AIOrderId.EmployMax: return IgnoreResult(AIEmployMax);
                case AIOrderId.EmployEnemy: return IgnoreResult(AIEmployEnemy);
                case AIOrderId.EmployEnemyMax: return IgnoreResult(AIEmployEnemyMax);
                case AIOrderId.Destroy: return AIDestroy;
                case AIOrderId.Merge: return IgnoreResult(AIMerge);
                case AIOrderId.Bonus: return IgnoreResult(AIRewardPerson);
                // ---- 人员调动 / 物资运输：按原版据点级命令重写，取代已删除的
                //      人才部署（DeploymentShadow 系列）与资源调度（ResourceDispatcher）两套自建系统 ----
                case AIOrderId.Move: return AIMove;
                case AIOrderId.Summon: return AISummon;
                case AIOrderId.Transport: return AITransport;
                // ---- 计略：原版据点级三选一（离间 / 协助 / 流言）----
                // 与势力级的 CityStrategyAI.AICityStrategy 并存：后者负责"被挑拨后的破盟反应"，
                // 前者负责"主动施计"，职责不重叠。
                case AIOrderId.Strategy: return AIStrategy;
                default: return null;
            }
        }

        /// <summary>
        /// 把命令体包装成"忽略返回值"的委托，对齐原版 据点命令调度 不判断返回值的做法。
        /// </summary>
        /// <param name="body">命令体</param>
        /// <returns>包装后的委托</returns>
        static Func<City, Scenario, bool> IgnoreResult(Func<City, Scenario, bool> body)
        {
            if (body == null)
                return null;
            return (city, scenario) =>
            {
                bool executed = body(city, scenario);
                // 委托给 CityAI 的命令体不会动 OutpostRuntime.actionPoint，
                // 这里把"真的做了事"转成调度器能识别的标记，由 OutpostCommand 统一补扣。
                if (executed)
                    OutpostRuntime.MarkDelegated();
                return true;
            };
        }

        /// <summary>
        /// 生产类命令：原版 生产命令 内部按权重在"兵装 / 器械 / 船只"里挑一种生产
        /// （见 s11_game_ai_kyoten_cmd.cpp:267-479 的 weight 表）。
        /// 本项目把三者拆成了独立命令，这里按同样顺序尝试，第一条真正执行的即停止。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        static bool ProduceAny(City city, Scenario scenario)
        {
            // 对齐原版 kyoten_produce（s11_game_ai_kyoten_cmd.cpp:267-479）：
            // 原版把"兵装 / 攻城器械 / 船只"放在**同一张权重表**里，
            // 逐项按"是否解锁 / 金是否够 / 是否满仓 / 有无空闲设施 / 现有数量 vs 需求"过滤，
            // 最后用 ai_table_choice 做一次加权随机挑一种生产。
            // 本项目原先按"兵装 → 器械 → 船只"固定顺序尝试，会出现"永远只造兵装"的偏置。
            //
            // 这里的权重规则（与原版同结构，参数按本项目口径）：
            //   基础兵装：库存 <5000 → 权重 5；< 需求 → 4；否则 0（满了不再造）
            //   攻城器械：库存 ≤ 人物数/4+1 → 2；否则 0
            //   船只    ：库存 ≤ 人物数     → 1；否则 0
            //   战斗中的据点不造器械与船只（:467-475）
            if (city.itemStore == null)
                return true;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            int personCount = info.personCount;
            // 【数量级折算】原版器械有 4 种（冲车 / 井阑 / 投石 / 木兽），每种独立计数，
            // 上限各为 person_count/4+1；本项目把它们并成 2 个 ItemStore 分组
            // （Helepolis = 冲车 + 木兽，Catapult = 井阑 + 投石，见 BuildingPlanWeights.cs:214-217），
            // 所以同一分组要容纳两种，上限翻倍。
            int minSiege = (personCount / 4 + 1) * 2;
            // 同理：原版船只 2 种（楼船 / 斗舰）各自上限 = person_count，
            // 本项目合并成 1 个 Boat 分组，上限取 2 倍。
            int minVessel = personCount * 2;

            int kindCount = (int)ItemStoreKindType.Boat + 1;
            int[] weight = new int[kindCount];

            // 基础兵装（枪 / 戟 / 弩 / 马）
            for (int kind = (int)ItemStoreKindType.Spear; kind <= (int)ItemStoreKindType.Horse; kind++)
            {
                int n = city.itemStore.GetNumber(kind);
                ItemType itemType = scenario != null ? scenario.GetObject<ItemType>(kind) : null;
                int cap = itemType != null ? itemType.TransformLimit(city.StoreLimit) : city.StoreLimit;
                if (cap > 0 && n >= cap)
                    continue;
                weight[kind] = n < 5000 ? 5 : 4;
            }

            // 攻城器械 / 船只：战斗状态下一律不产
            if (info.state != OutpostState.Battle)
            {
                for (int kind = (int)ItemStoreKindType.Helepolis; kind <= (int)ItemStoreKindType.Catapult; kind++)
                {
                    int n = city.itemStore.GetNumber(kind);
                    if (n > minSiege)
                        continue;
                    weight[kind] = n < 1 ? 2 : 1;
                }

                int boat = city.itemStore.GetNumber((int)ItemStoreKindType.Boat);
                if (boat <= minVessel)
                    weight[(int)ItemStoreKindType.Boat] = 1;
            }

            int total = 0;
            for (int i = 0; i < weight.Length; i++)
                total += weight[i];
            if (total <= 0)
                return true;

            // 加权随机 —— 对应原版 ai_table_choice
            int roll = GameRandom.Range(0, total);
            int picked = -1;
            for (int i = 0; i < weight.Length; i++)
            {
                if (weight[i] <= 0)
                    continue;
                roll -= weight[i];
                if (roll < 0)
                {
                    picked = i;
                    break;
                }
            }
            if (picked < 0)
                return true;

            // 分派执行：兵装走既有的按等级权重挑选，器械 / 船只各走专用入口
            if (picked >= (int)ItemStoreKindType.Spear && picked <= (int)ItemStoreKindType.Horse)
                AICreateItems(city, scenario);
            else if (picked <= (int)ItemStoreKindType.Catapult)
                AICreateMachine(city, scenario);
            else
                AICreateBoat(city, scenario);
            return true;
        }

        /// <summary>
        /// 命令的行动力成本。
        ///
        /// 对齐出处：PersonOrderManager 的 get_*_action_point_cost（成本字段来自参数表，
        /// 见 s11_sys_person_order_city.cpp:358-361 等）。本项目对应 JobType.GetJobCostAP。
        /// </summary>
        /// <param name="orderId">命令编号</param>
        /// <param name="city">据点</param>
        /// <returns>行动力成本</returns>
        static int GetOrderActionPointCost(AIOrderId orderId, City city)
        {
            switch (orderId)
            {
                case AIOrderId.Develop: return JobType.GetJobCostAP((int)CityJobType.Build);
                case AIOrderId.Recruit: return JobType.GetJobCostAP((int)CityJobType.RecruitTroops);
                case AIOrderId.Produce: return JobType.GetJobCostAP((int)CityJobType.CreateItems);
                case AIOrderId.Patrol: return JobType.GetJobCostAP((int)CityJobType.Inspection);
                case AIOrderId.Merchant:
                case AIOrderId.MerchantExtra: return JobType.GetJobCostAP((int)CityJobType.TradeFood);
                case AIOrderId.Drill: return JobType.GetJobCostAP((int)CityJobType.TrainTroops);
                case AIOrderId.Defense:
                case AIOrderId.Attack: return JobType.GetJobCostAP((int)CityJobType.MakeTroop);
                case AIOrderId.Build: return JobType.GetJobCostAP((int)CityJobType.Build);
                case AIOrderId.Support: return JobType.GetJobCostAP((int)CityJobType.MakeTansport);
                case AIOrderId.Transport: return JobType.GetJobCostAP((int)CityJobType.MakeTansport);
                case AIOrderId.Move: return JobType.GetJobCostAP((int)CityJobType.TransformPerson);
                case AIOrderId.Summon: return JobType.GetJobCostAP((int)CityJobType.CallPerson);
                case AIOrderId.Search:
                case AIOrderId.SearchMax: return JobType.GetJobCostAP((int)CityJobType.Searching);
                case AIOrderId.Employ:
                case AIOrderId.EmployMax:
                case AIOrderId.EmployEnemy:
                case AIOrderId.EmployEnemyMax: return JobType.GetJobCostAP((int)CityJobType.RecruitPerson);
                case AIOrderId.Destroy: return JobType.GetJobCostAP((int)CityJobType.Build);
                case AIOrderId.Merge: return JobType.GetJobCostAP((int)CityJobType.UpgradeBuilding);
                case AIOrderId.Bonus: return JobType.GetJobCostAP((int)CityJobType.Reward);
                // 计略的门禁取"流言"的成本：离间与流言的成本相近，
                // 具体类型在 AIStrategy 内部再按 CityJobType.TwoTigers / Rumor 精确扣减。
                case AIOrderId.Strategy: return JobType.GetJobCostAP((int)CityJobType.Rumor);
                default: return 0;
            }
        }
    }
}
