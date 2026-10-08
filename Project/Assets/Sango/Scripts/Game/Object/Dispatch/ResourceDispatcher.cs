using System;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 资源调度入口（与人才调度 <see cref="DeploymentShadow"/> 完全同构：域 / 防重入 / 报告三段）。
    ///
    /// 【两种入口 · 两套边界】
    ///   · <see cref="Run"/>：**非玩家（AI）势力**，注册在 <c>Force.AIPrepare</c> 的 AI 命令队列里，
    ///     **以势力为边界**调度一次（域内可跨军团调货）；
    ///   · <see cref="RunPlayerCorps"/>：**玩家势力**，由 <c>Force.Run</c> 每回合调用，
    ///     **逐个玩家军团**调度 —— 军团边界只对这一侧生效。
    ///
    /// 【玩家第一军团】默认**不自动运货**（<c>includeCapitalCorps = false</c>）：
    /// 第一军团由玩家直辖，物资由玩家自己安排。打开开关后它也会自动运货
    /// —— 资源运输不会打乱玩家排好的阵型，属于纯增益，所以这里的开关与人才调度的
    /// "第一军团只出参考报告"是两回事。
    ///
    /// 【与人才调度的衔接】本入口在 <c>Force.AIPrepare</c> / <c>Force.Run</c> 里刻意排在
    /// 人才调度**之前**：先算出"哪座城有货要走"，再让编制层（<see cref="CityEstablishment"/>）
    /// 依据 <see cref="ResourceDispatchState.HasPendingEnvoyDemand"/> 补运输岗，
    /// 同一回合内就有人被派到那座城去 —— 这是"人员需求与人才调度配合完成"的时序保证。
    /// </summary>
    public static class ResourceDispatcher
    {
        /// <summary>报告计数（按作用域分，用于按回合间隔节流）</summary>
        static readonly Dictionary<int, int> visitCounter = new Dictionary<int, int>();

        /// <summary>是否输出报告（编辑器 / 开发包）</summary>
        static bool LogEnabled
        {
            get { return UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild; }
        }

        /// <summary>资源调度配置是否可用（总开关）。</summary>
        static bool ConfigReady(AIConfig config)
        {
            return config != null && config.resourceDispatch != null && config.resourceDispatch.enabled;
        }

        // ==================== 入口 ====================

        /// <summary>
        /// 非玩家（AI）势力入口（注册在 <c>Force.AIPrepare</c> 的 AI 命令队列里）。
        /// **以势力为边界**调度：域 = 全势力城池（含港关），势力内可跨军团调货。
        /// 永远返回 true（不阻断 AI 命令队列）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">剧本</param>
        public static bool Run(Force force, Scenario scenario)
        {
            // 守卫：玩家势力一律走 RunPlayerCorps（Force.Run），两条入口必须互斥
            if (force != null && force.IsPlayer)
                return true;

            AIConfig config = AIConfig.Instance;
            if (!ConfigReady(config))
                return true;

            RunDomain(force, null, false, scenario, config.resourceDispatch);
            return true;
        }

        /// <summary>
        /// 玩家势力入口（由 <c>Force.Run</c> 每回合调用）：**逐个玩家军团**调度。
        /// 军团边界只在这一侧生效；第一军团默认跳过（见类注释）。
        /// 防重入由回合号保证（<c>Force.Run</c> 在等待玩家操作时会被反复进入）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">剧本</param>
        public static bool RunPlayerCorps(Force force, Scenario scenario)
        {
            if (force == null || scenario == null || !force.IsPlayer)
                return true;

            AIConfig config = AIConfig.Instance;
            if (!ConfigReady(config))
                return true;

            ResourceDispatchWeights w = config.resourceDispatch;
            if (!w.enablePlayerCorps)
                return true;

            Corps capital = force.CapitalCorps;
            for (int i = 0; i < scenario.corpsSet.Count; i++)
            {
                Corps corps = scenario.corpsSet[i];
                if (corps == null || !corps.IsAlive || corps.BelongForce != force)
                    continue;

                bool isCapital = corps == capital;
                if (isCapital && !w.includeCapitalCorps)
                {
                    // 第一军团默认不自动运货：顺手清掉它名下的"待发存量"与"覆盖 / 背压"登记，
                    // 免得给玩家直辖的军团凭空补出一个运输岗，或让产物端误以为有人接管它的富余。
                    List<City> capitalCities = ResourceBalance.CollectDomain(force, corps, scenario);
                    ResourceDispatchState.ClearScopeDemand(capitalCities);
                    ResourceDispatchState.ClearScopeBacklog(capitalCities);
                    continue;
                }
                RunDomain(force, corps, isCapital, scenario, w);
            }
            return true;
        }

        // ==================== 单个作用域 ====================

        /// <summary>
        /// 单个调度作用域的"求解 + 执行 + 报告"。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="corps">军团作用域（null = 势力级）</param>
        /// <param name="adviceOnly">是否"只算不运"（玩家直辖军团打开开关后的参考模式）</param>
        /// <param name="scenario">剧本</param>
        /// <param name="w">参数</param>
        static void RunDomain(Force force, Corps corps, bool adviceOnly, Scenario scenario, ResourceDispatchWeights w)
        {
            if (force == null || scenario == null || w == null)
                return;

            List<City> domain = ResourceBalance.CollectDomain(force, corps, scenario);
            if (domain.Count == 0)
                return;

            int scopeKey = DeploymentState.ScopeKey(force, corps);
            int turn = DeploymentState.CurrentForceTurn(force.Id);

            // 【关键 · 防重入】必须在清需求**之前**判定：
            // Force.Run 等待玩家操作时会被反复调用，若先清后判，
            // 重复调用会把上一轮登记的"待发存量"清空 → 人才调度永远看不到需求。
            if (!ResourceDispatchState.TryBeginScopeRun(scopeKey, turn))
                return;

            ResourceDispatchState.ClearScopeDemand(domain);       // 本回合重新登记
            ResourceDispatchState.ClearScopeBacklog(domain);      // 覆盖 / 背压标记同样按回合重建

            bool dryRun = adviceOnly || w.shadowOnly;
            ResourcePlan plan = ResourceBalance.Solve(force, corps, scenario, w, dryRun);

            if (!dryRun)
            {
                Execute(force, plan, scenario, w);
                PublishCoverage(plan, scenario);
                NotifyPlayer(force, plan, scenario, w);
            }

            Report(force, corps, plan, w);
        }

        /// <summary>
        /// 发布"生产背压"信号（产物端依据见 <see cref="ResourceDispatchState.CanKeepProducing"/>）：
        ///   ① 本趟纳入调度的城池全部登记为"被覆盖"（有人管它的富余，满仓后可以继续生产）；
        ///   ② 本趟**运不出去**的富余逐条登记为背压（有货堆着走不动，满仓时就该停）；
        ///   ③ **兵装待产**（守军缺兵装）的诊断随报告输出 —— 兵装生产排不上人手时，
        ///      报告直接写明卡在哪一步（无空闲武将 / 无空闲锻冶马厩 / 资金 / 城内目标已满），
        ///      省得只知道"缺兵装"却查不到为什么。（给"造兵装"命令加权重排优先级在 CityAIOrderPlanner。）
        ///
        /// 只在真正执行（非影子 / 非仅参考）时发布：影子模式下没有任何东西被运走，
        /// 若发布"覆盖"会让产物端误以为有人接管而无限生产。
        /// </summary>
        /// <param name="plan">本趟计划</param>
        /// <param name="scenario">剧本</param>
        static void PublishCoverage(ResourcePlan plan, Scenario scenario)
        {
            if (plan == null || scenario == null)
                return;
            int turn = scenario.TurnCount;

            if (plan.cities != null)
            {
                for (int i = 0; i < plan.cities.Count; i++)
                {
                    if (plan.cities[i] != null)
                        ResourceDispatchState.MarkCovered(plan.cities[i].Id, turn);
                }
            }


            if (plan.backlog == null)
                return;
            for (int i = 0; i < plan.backlog.Count; i++)
                ResourceDispatchState.MarkBacklog(plan.backlog[i].cityId, plan.backlog[i].kind, turn);
        }

        /// <summary>
        /// 本作用域本回合的派车配额。
        ///
        /// 固定额度（<c>maxShipmentsPerDomain</c>）在小势力够用，但大帝国（20+ 城）会长期
        /// "想运的多、配额不够"：所以按城数放大 —— <c>ceil(城数 / shipmentsPerCityDivisor)</c>，
        /// 再取下限。战线越长、运力越足。
        /// </summary>
        static int QuotaFor(ResourcePlan plan, ResourceDispatchWeights w)
        {
            int quota = Math.Max(1, w.maxShipmentsPerDomain);
            if (w.shipmentsPerCityDivisor <= 0 || plan == null || plan.cities == null)
                return quota;

            int scaled = (plan.cities.Count + w.shipmentsPerCityDivisor - 1) / w.shipmentsPerCityDivisor;
            return scaled > quota ? scaled : quota;
        }

        /// <summary>执行计划：按"最该送的先发"的顺序派车，受配额与人员约束。</summary>
        /// <param name="force">势力（玩家可见性用）</param>
        /// <param name="plan">计划</param>
        /// <param name="scenario">剧本</param>
        /// <param name="w">参数</param>
        static void Execute(Force force, ResourcePlan plan, Scenario scenario, ResourceDispatchWeights w)
        {
            if (plan == null || plan.shipments == null || scenario == null || w == null)
                return;

            int quota = QuotaFor(plan, w);
            int executed = 0;
            for (int i = 0; i < plan.shipments.Count; i++)
            {
                if (executed >= quota)
                {
                    // 超过本回合配额：剩下的本回合统统不派（下回合重新求解，缺口还在就一定还会被补）
                    for (int j = i; j < plan.shipments.Count; j++)
                    {
                        ResourceShipment left = plan.shipments[j];
                        left.failReason = "超过本回合派车配额";
                        plan.shipments[j] = left;
                    }
                    break;
                }

                ResourceShipment s = plan.shipments[i];
                City from = scenario.citySet.Get(s.fromCityId);
                City to = scenario.citySet.Get(s.toCityId);

                string reason;
                bool ok = ResourceTransfer.Ship(from, to, s, scenario, w, out reason);
                s.executed = ok;
                s.failReason = ok ? null : reason;
                plan.shipments[i] = s;

                if (ok)
                {
                    executed++;
                }
                else if (reason == ResourceTransfer.ReasonNoFreeEnvoy && from != null)
                {
                    // 有货要走却抽不出人 → 向人才调度提"补运输岗"的需求。
                    // 编制层读 ResourceDispatchState.HasPendingEnvoyDemand，下一次人才调度就会补人过来。
                    ResourceDispatchState.MarkPendingEnvoyDemand(from.Id);
                }
            }
            plan.executedCount = executed;
        }

        // ==================== 玩家可见性 ====================

        /// <summary>
        /// 把本趟**真正发出**的运输队汇总成几条消息告诉玩家（只对玩家势力）。
        ///
        /// 必要性：玩家军团（第一军团以外）的资源也会被自动搬运，而调度报告只在编辑器 / 开发包输出，
        /// 正式包里玩家只会看到"资源莫名变少"却查不到原因。
        /// 按**目的地**合并成一条消息（同一座城可能由多座城供货），点消息可跳到那座城；
        /// 每回合有条数上限，超出的合并成"另有 N 座城"。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="plan">本趟计划</param>
        /// <param name="scenario">剧本</param>
        /// <param name="w">参数</param>
        static void NotifyPlayer(Force force, ResourcePlan plan, Scenario scenario, ResourceDispatchWeights w)
        {
            if (force == null || plan == null || scenario == null || w == null)
                return;
            if (!force.IsPlayer || !w.notifyPlayer)
                return;
            if (plan.shipments == null || plan.shipments.Count == 0)
                return;

            // 按目的地合并：一城一条消息
            List<ResourceShipment> merged = new List<ResourceShipment>();
            Dictionary<int, int> mergedIndex = new Dictionary<int, int>();
            for (int i = 0; i < plan.shipments.Count; i++)
            {
                ResourceShipment s = plan.shipments[i];
                if (!s.executed)
                    continue;

                int idx;
                if (mergedIndex.TryGetValue(s.toCityId, out idx))
                {
                    ResourceShipment m = merged[idx];
                    m.gold += s.gold;
                    m.food += s.food;
                    m.troops += s.troops;
                    m.arms += s.arms;
                    m.machines += s.machines;
                    m.boats += s.boats;
                    merged[idx] = m;
                }
                else
                {
                    mergedIndex[s.toCityId] = merged.Count;
                    merged.Add(s);
                }
            }
            if (merged.Count == 0)
                return;

            int limit = Math.Max(1, w.notifyMaxCityPerTurn);
            int shown = merged.Count < limit ? merged.Count : limit;
            for (int i = 0; i < shown; i++)
            {
                ResourceShipment s = merged[i];
                City to = scenario.citySet.Get(s.toCityId);
                Sango.Core.Player.PlayerMessage.AddTextMessage(
                    string.Format("自动调度：向{0}运出{1}（{2}）", s.toCityName, AmountText(s), s.reason),
                    force, to != null ? to.x : 0, to != null ? to.y : 0);
            }

            if (merged.Count > shown)
            {
                Sango.Core.Player.PlayerMessage.AddTextMessage(
                    string.Format("自动调度：另有 {0} 座城收到物资。", merged.Count - shown), force, 0, 0);
            }
        }

        /// <summary>把一张单的货量拼成"金1000 粮2000 兵300"这样的短文本。</summary>
        static string AmountText(ResourceShipment s)
        {
            StringBuilder sb = new StringBuilder();
            AppendAmount(sb, "金", s.gold);
            AppendAmount(sb, "粮", s.food);
            AppendAmount(sb, "兵", s.troops);
            AppendAmount(sb, "装", s.arms);
            AppendAmount(sb, "器", s.machines);
            AppendAmount(sb, "船", s.boats);
            return sb.Length > 0 ? sb.ToString() : "物资";
        }

        /// <summary>追加一项货量（为 0 时不写，避免"金0 粮0"这种噪声）。</summary>
        static void AppendAmount(StringBuilder sb, string label, int amount)
        {
            if (amount <= 0)
                return;
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(label).Append(amount);
        }

        // ==================== 报告 ====================

        static void Report(Force force, Corps corps, ResourcePlan plan, ResourceDispatchWeights w)
        {
            if (plan == null || w == null)
                return;
            if (!w.logEnabled || !LogEnabled)
                return;
            if (!ShouldLog(force, corps, w))
                return;

            string title = plan.adviceOnly ? "[资源调度影子]" : "[资源调度]";
            // 与 DeploymentShadow 同一取舍：Sango.Log 带 [Conditional("SANGO_DEBUG")]（本工程未定义），
            // 走它等于什么都不打，所以诊断输出直接用 UnityEngine.Debug。
            UnityEngine.Debug.Log(plan.Report(title));
        }

        /// <summary>是否轮到本作用域输出（按配置的势力 / 军团 id 与回合间隔）。</summary>
        static bool ShouldLog(Force force, Corps corps, ResourceDispatchWeights w)
        {
            if (force == null)
                return false;

            // logForceId：-1 = 全部；0 = 只报告玩家势力；>0 = 只报告该势力
            if (w.logForceId > 0)
            {
                if (force.Id != w.logForceId)
                    return false;
            }
            else if (w.logForceId == 0 && !force.IsPlayer)
            {
                return false;
            }

            if (w.logCorpsId > 0)
            {
                if (corps == null || corps.Id != w.logCorpsId)
                    return false;
            }

            if (w.logIntervalTurns <= 0)
                return true;

            int key = DeploymentState.ScopeKey(force, corps);
            int count;
            visitCounter.TryGetValue(key, out count);
            count++;
            visitCounter[key] = count;
            return (count - 1) % w.logIntervalTurns == 0;
        }

        /// <summary>清空报告计数（换剧本时调用，避免跨剧本残留）。</summary>
        public static void Clear()
        {
            visitCounter.Clear();
        }
    }
}
