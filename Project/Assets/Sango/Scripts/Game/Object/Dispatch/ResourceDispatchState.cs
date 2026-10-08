using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 资源调度的运行期状态（**不序列化**，换剧本由 <see cref="ScenarioLifecycle"/> 复位）。
    ///
    /// 只承担两件跨系统协作的事：
    ///   ① **防重入**：玩家势力的 <c>Force.Run</c> 在等待玩家操作时会被反复调用，
    ///      同一势力回合内每个调度作用域只能真正跑一次（与 <see cref="DeploymentState.TryBeginScopeDeploy"/>
    ///      同一思路，但**不能用同一份标记** —— 人才调度与资源调度是两个独立的调用点）；
    ///   ② **人员需求登记**：某城有货待发却抽不出空闲武将时，把该城登记为"待发存量"，
    ///      由编制层 <see cref="CityEstablishment"/> 在下一次人才调度时补一个"运输"岗，
    ///      这就是"人员需求与人才调度系统配合完成"的落点 ——
    ///      资源调度**只提出需求**，武将达到底仍唯一经 <see cref="DeploymentExecutor"/> 执行。
    /// </summary>
    public static class ResourceDispatchState
    {
        /// <summary>每种城池预留的"资源种类"槽位数（&gt;= <see cref="ResourceKind"/> 的枚举项数即可）</summary>
        const int ResourceKindSlots = 8;

        /// <summary>作用域键 → 已运行的势力回合号（防重入）</summary>
        static readonly Dictionary<int, int> lastRunTurn = new Dictionary<int, int>();

        /// <summary>有货待发、但源城抽不出空闲武将的城池 id（供人才调度补运输岗）</summary>
        static readonly HashSet<int> pendingEnvoyCityIds = new HashSet<int>();

        /// <summary>城池 id → 最近一次"被资源调度覆盖"的回合（<see cref="Scenario.TurnCount"/>）</summary>
        static readonly Dictionary<int, int> coveredTurn = new Dictionary<int, int>();

        /// <summary>(城池 id, 资源种类) → 最近一次"有富余却运不出去"的回合</summary>
        static readonly Dictionary<int, int> backlogTurn = new Dictionary<int, int>();

        /// <summary>资源调度总开关是否打开（每处读配置都走它，避免各处判断口径不一）。</summary>
        static bool FeatureEnabled
        {
            get
            {
                AIConfig config = AIConfig.Instance;
                return config != null && config.resourceDispatch != null && config.resourceDispatch.enabled;
            }
        }

        // ==================== ① 防重入 ====================

        /// <summary>
        /// 尝试开始"本回合该作用域"的资源调度。返回 true 表示本次是第一次。
        /// </summary>
        /// <param name="scopeKey">调度作用域键（<see cref="DeploymentState.ScopeKey"/>）</param>
        /// <param name="turn">当前势力回合号（<see cref="DeploymentState.CurrentForceTurn"/>）</param>
        public static bool TryBeginScopeRun(int scopeKey, int turn)
        {
            int last;
            if (lastRunTurn.TryGetValue(scopeKey, out last) && last == turn)
                return false;
            lastRunTurn[scopeKey] = turn;
            return true;
        }

        // ==================== ② 人员需求登记 ====================

        /// <summary>
        /// 清空**本作用域**残留的人员需求。必须在真正开始求解**之前**调用
        /// （每回合重新登记，否则上回合的需求会永远留着、把运输岗钉死在一座已经不发车的城上）。
        /// </summary>
        /// <param name="cities">本作用域的城池</param>
        public static void ClearScopeDemand(List<City> cities)
        {
            if (cities == null)
                return;
            for (int i = 0; i < cities.Count; i++)
            {
                City city = cities[i];
                if (city != null)
                    pendingEnvoyCityIds.Remove(city.Id);
            }
        }

        /// <summary>登记"本城有货待发、缺运输主将"。</summary>
        /// <param name="cityId">城池 id</param>
        public static void MarkPendingEnvoyDemand(int cityId)
        {
            if (cityId > 0)
                pendingEnvoyCityIds.Add(cityId);
        }

        /// <summary>
        /// 本城是否有"待发存量"（有货要走，但没抽出武将）。
        /// 编制层据此补一个"运输"岗，向人才调度提出人员需求。
        ///
        /// 功能关闭时一律返回 false：否则关掉资源调度之后，编制层会一直给某座城留一个运输岗
        /// （残留的待发存量没人清理）。
        /// </summary>
        /// <param name="cityId">城池 id</param>
        /// <returns>需要补人返回 true</returns>
        public static bool HasPendingEnvoyDemand(int cityId)
        {
            if (cityId <= 0 || pendingEnvoyCityIds.Count == 0)
                return false;
            if (!FeatureEnabled)
                return false;
            return pendingEnvoyCityIds.Contains(cityId);
        }

        /// <summary>当前登记了"待发存量"的城池数（诊断 / 报告用）。</summary>
        public static int PendingEnvoyCityCount
        {
            get { return pendingEnvoyCityIds.Count; }
        }

        // ==================== ②b 兵装需求（与城池 AI 命令优先级协调） ====================
        //
        // 注意：这里**没有**跨系统状态。原因：兵装需求（"本城守军还差多少兵装"）是完全可从
        // CitySituation 现算出来的（weaponCount vs troops × cover），
        // 城池 AI 命令排序自己就能算（见 CityAIOrderPlanner.GetOverrideBias）；
        // 资源调度抽运输主将时也只用本城自己的数据（see ResourceDispatcher.PickEnvoy）。
        // 与"待发存量"（② 人员需求）不同 —— 那条信息只有资源调度知道，所以必须登记。

        // ==================== ③ 生产背压（"后方满仓后不应当停产"的依据） ====================
        //
        // 兵装 / 器械 / 船的容器满了以后，产物端**不该立刻停产** —— 满仓只说明"该往外运了"，
        // 而资源调度正是干这件事的（把水位以上的富余推向前线）。
        // 但也不能无条件地一直造：如果上一回合的富余**根本运不出去**（前线也满、没有接收方），
        // 再投钱 / 投人只会把存货堆在仓库里。这里就是这条"背压"信号：
        //   · 覆盖：本城在所辖作用域里被调度过（说明有人管它的富余）；
        //   · 背压：刚才那趟调度里，本城这类资源还有**运不走的富余**。
        // 于是产物端的判定变成：满仓 && (没被覆盖 || 有背压) → 才停产。

        /// <summary>标记某城本回合被资源调度覆盖（每次 RunDomain 对本作用域全城登记）。</summary>
        /// <param name="cityId">城池 id</param>
        /// <param name="turn">当前回合（<see cref="Scenario.TurnCount"/>）</param>
        public static void MarkCovered(int cityId, int turn)
        {
            if (cityId > 0)
                coveredTurn[cityId] = turn;
        }

        /// <summary>标记某城这类资源本回合"有富余却运不出去"。</summary>
        /// <param name="cityId">城池 id</param>
        /// <param name="kind">资源种类（<see cref="ResourceKind"/> 的整数值）</param>
        /// <param name="turn">当前回合（<see cref="Scenario.TurnCount"/>）</param>
        public static void MarkBacklog(int cityId, int kind, int turn)
        {
            if (cityId > 0)
                backlogTurn[cityId * 16 + kind] = turn;
        }

        /// <summary>
        /// 清空**本作用域**上的覆盖 / 背压标记（每回合重新登记，与 <see cref="ClearScopeDemand"/> 同时调用）。
        /// 不清的话，上一回合的"能运 / 运不出去"会一直留着，产物端读到的就是过期的信号。
        /// </summary>
        /// <param name="cities">本作用域的城池</param>
        public static void ClearScopeBacklog(List<City> cities)
        {
            if (cities == null)
                return;
            for (int i = 0; i < cities.Count; i++)
            {
                City city = cities[i];
                if (city == null)
                    continue;
                coveredTurn.Remove(city.Id);
                for (int kind = 0; kind < ResourceKindSlots; kind++)
                    backlogTurn.Remove(city.Id * 16 + kind);
            }
        }

        /// <summary>
        /// 满仓的产物还能不能继续生产（"后方满仓后不应当停产"的落点）。
        ///
        /// 返回 true 表示"可以继续造"：本城在 <paramref name="grace"/> 回合内被资源调度覆盖过，
        /// 且那趟调度里**没有**这类资源运不出去的背压。
        /// 未被覆盖（功能关闭 / 军团禁止运输 / 玩家直辖军团等）时返回 false → 沿用原来的"满仓即停"，
        /// 保证不在没人接管富余的情况下无限生产。
        /// </summary>
        /// <param name="cityId">城池 id</param>
        /// <param name="kind">资源种类（<see cref="ResourceKind"/> 的整数值）</param>
        /// <param name="turn">当前回合（<see cref="Scenario.TurnCount"/>）</param>
        /// <param name="grace">认可多少回合内的登记（默认 1，容忍跨回合的先后顺序差）</param>
        /// <returns>允许继续生产返回 true</returns>
        public static bool CanKeepProducing(int cityId, int kind, int turn, int grace = 1)
        {
            if (cityId <= 0)
                return false;

            // 功能关掉就必须回到"满仓即停"的原行为：否则会拿着上一回合的残留标记继续超产。
            if (!FeatureEnabled)
                return false;

            if (grace < 0)
                grace = 0;

            int stamp;
            if (!coveredTurn.TryGetValue(cityId, out stamp) || turn - stamp > grace)
                return false;                                   // 没被调度覆盖 → 满仓就停（原行为）

            if (backlogTurn.TryGetValue(cityId * 16 + kind, out stamp) && turn - stamp <= grace)
                return false;                                   // 有背压（运不出去）→ 停

            return true;
        }

        /// <summary>清空全部运行期状态（换剧本时由 ScenarioLifecycle 调用）。</summary>
        public static void Clear()
        {
            lastRunTurn.Clear();
            pendingEnvoyCityIds.Clear();
            coveredTurn.Clear();
            backlogTurn.Clear();
        }
    }
}
