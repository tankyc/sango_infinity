using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 人才部署入口（Phase C 后即**唯一**的武将调动入口）。
    ///
    /// 职责分成两件**互不影响**的事：
    ///   ① 求解并执行部署 —— 每个调度作用域每回合都要做（由 <c>shadowOnly</c> 决定是否真的调人）；
    ///   ② 输出部署报告 —— 只在编辑器 / 开发包、且按势力 / 军团与回合间隔控制，避免刷屏。
    ///
    /// 【两种入口 · 两套边界】
    ///   · <see cref="Run"/>：**非玩家（AI）势力**，注册在 <c>Force.AIPrepare</c> 的 AI 命令队列里，
    ///     **以势力为边界**调度一次（目标城与抽调池都是全势力，势力内可跨军团调人）；
    ///   · <see cref="RunPlayerCorps"/>：**玩家势力**，由 <c>Force.Run</c> 每回合调用，
    ///     **逐个玩家军团**调度 —— "人才"开关与军团边界只对这一侧生效。
    ///
    /// 【只有玩家军团才受约束】
    ///   ① **"人才"委任开关**（`Corps.AppointContentType.Person`：0 = 允许 AI 代管，1 = 玩家自己管）
    ///      —— 未开启的军团**整块跳过**，连求解都不做（见 <see cref="DeploymentExecutor.IsPersonManaged"/>）；
    ///   ② **第一军团**（君主所在军团）由玩家直辖 —— 只出"仅参考"影子报告，不下发调动令；
    ///   ③ **军团边界**（源城与目标城必须同团，跨团一律否决）。
    ///
    /// 两者都以回合号防重入（<c>Force.Run</c> 在等待玩家操作时会被反复调用）。
    /// </summary>
    public static class DeploymentShadow
    {
        static readonly Dictionary<int, int> visitCounter = new Dictionary<int, int>();

        /// <summary>是否输出（编辑器 / 开发包）。</summary>
        static bool LogEnabled
        {
            get { return UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild; }
        }

        /// <summary>部署配置是否可用（总开关）。</summary>
        static bool ConfigReady(AIConfig config)
        {
            return config != null && config.deployment != null && config.deployment.enabled;
        }

        /// <summary>
        /// 非玩家（AI）势力入口（注册在 <c>Force.AIPrepare</c> 的 AI 命令队列）。
        ///
        /// **以势力为边界**调度：目标城与抽调池都是全势力，势力内可以跨军团调人。
        /// "人才"委任开关与军团边界**只约束玩家军团**，AI 势力不参与这两条判定
        /// （AI 势力在自己的地盘里怎么调人是它自己的事）。
        /// 永远返回 true（不阻断 AI 命令队列）。
        /// </summary>
        public static bool Run(Force force, Scenario scenario)
        {
            // 守卫：玩家势力一律走 RunPlayerCorps（Force.Run）。两条入口必须互斥 ——
            // 否则玩家势力会被势力级调度绕过"第一军团不介入 / 军团边界"这两条约束。
            if (force != null && force.IsPlayer)
                return true;

            AIConfig config = AIConfig.Instance;
            if (!ConfigReady(config))
                return true;

            RunScope(force, null, false, scenario, config.deployment);
            return true;
        }

        /// <summary>
        /// 玩家势力入口（由 <c>Force.Run</c> 每回合调用）：**逐个玩家军团**调度。
        ///
        /// 玩家势力才有的三条约束在这里生效：
        ///   · **"人才"委任开关** —— 未开启的军团**整块跳过**，连求解都不做；
        ///   · **第一军团**（君主所在、玩家直辖）—— 只出"仅参考"影子报告，不下发调动令；
        ///   · **军团边界**（源城与目标城必须同团）—— 由执行层闸门保证。
        /// 防重入由回合号保证：<c>Force.Run</c> 在等待玩家操作时会被反复进入。
        /// </summary>
        public static bool RunPlayerCorps(Force force, Scenario scenario)
        {
            if (force == null || scenario == null || !force.IsPlayer)
                return true;                             // 非玩家势力走 AI 势力入口（以势力为边界）

            AIConfig config = AIConfig.Instance;
            if (!ConfigReady(config))
                return true;

            Corps capital = force.CapitalCorps;
            for (int i = 0; i < scenario.corpsSet.Count; i++)
            {
                Corps corps = scenario.corpsSet[i];
                if (corps == null || !corps.IsAlive || corps.BelongForce != force)
                    continue;

                // 【军团委任 · 人才开关】只有"人才"开启（允许 AI 代管）的军团才跑调度逻辑
                if (!DeploymentExecutor.IsPersonManaged(corps))
                    continue;

                // 玩家直辖的第一军团：**只算不调**，出一份"仅参考"的影子报告 ——
                // 既让玩家能看到 AI 视角下的岗位 / 人力分布，又绝不改动玩家自己排好的部署。
                bool adviceOnly = corps == capital;
                RunScope(force, corps, adviceOnly, scenario, config.deployment);
            }
            return true;
        }

        /// <summary>
        /// 单个调度作用域的"求解 + 执行 + 报告"。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="corps">军团作用域（null = 势力级作用域）</param>
        /// <param name="adviceOnly">
        /// 是否"只算不调"（仅参考）：玩家直辖的第一军团为 true ——
        /// 照常求解并输出影子报告，但**不下发任何调动令**。
        /// </param>
        /// <param name="scenario">剧本</param>
        /// <param name="weights">部署参数</param>
        static void RunScope(Force force, Corps corps, bool adviceOnly, Scenario scenario, DeploymentWeights weights)
        {
            if (force == null || scenario == null || weights == null)
                return;

            int scopeKey = DeploymentState.ScopeKey(force, corps);
            int turn = DeploymentState.CurrentForceTurn(force.Id);

            // 【关键 · 防重入】Force.Run 在"等待玩家操作"时会被反复调用，
            // 必须保证同一势力回合内每个作用域只真正部署一次。
            if (!DeploymentState.TryBeginScopeDeploy(scopeKey, turn))
                return;

            // 【关键 · 执行与日志解耦】求解 / 执行**无条件**发生，只把"打印"挂在节流开关上。
            // 旧实现把 Solve 放在日志节流判断之后，导致：
            //   ① 每个势力每 shadowLogIntervalTurns 回合才真正调一次人，其余回合完全不部署；
            //   ② 正式包（非编辑器 / 非开发包）LogEnabled 为 false → 永远不部署，静默失效；
            //   ③ 连带的回合标度失真（防抖 / 前期加成按"部署次数"而非真实回合计）。
            // 【adviceOnly】只算不调：照常求解并出报告，但一次调动令都不下发（玩家直辖军团）。
            DeploymentPlan plan = DeploymentSolver.Solve(force, corps, adviceOnly, scenario);

            if (!weights.shadowLogEnabled || !LogEnabled)
                return;
            if (!ShouldLog(force, corps, weights))
                return;

            // 标题跟随模式：真正执行时才报"实际调动人数"，其余（全局影子 / 仅参考）都报"影子"
            string title = (weights.shadowOnly || adviceOnly)
                ? "[部署影子]"
                : string.Format("[部署执行] 本回合实际调动 {0} 人", DeploymentState.transferCountThisTurn);

            // 【为什么刻意不走 Sango.Log】Sango.Log.Info 带 [Conditional("SANGO_DEBUG")]，
            // 而本工程任何平台都没定义 SANGO_DEBUG（ProjectSettings → scriptingDefineSymbols，
            // 与 GameEventDiagnostics 的注释同一结论），走它等于**什么都不打** ——
            // 部署报告会凭空消失。这里与 GameEventDiagnostics 保持同一取舍：
            // 诊断输出直接用 UnityEngine.Debug，并靠上面的 LogEnabled（编辑器 / 开发包）
            // + shadowLogEnabled + 回合间隔三重门控，保证正式玩家包安静。
            UnityEngine.Debug.Log(plan.Report(title));
        }

        /// <summary>
        /// 是否轮到本作用域输出（按配置的势力 id / 军团 id 与回合间隔）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="corps">军团作用域（null = 势力级）</param>
        /// <param name="weights">部署参数</param>
        static bool ShouldLog(Force force, Corps corps, DeploymentWeights weights)
        {
            if (force == null) return false;

            // shadowLogForceId：>0 = 只报告该势力；-1 = 报告所有势力；0 = 只报告玩家势力
            if (weights.shadowLogForceId > 0)
            {
                if (force.Id != weights.shadowLogForceId) return false;
            }
            else if (weights.shadowLogForceId == 0 && !force.IsPlayer)
            {
                return false;   // 默认只报告玩家势力，避免刷屏
            }

            // shadowLogCorpsId：>0 = 只报告该军团（只在军团级调度时才有意义）
            if (weights.shadowLogCorpsId > 0)
            {
                if (corps == null || corps.Id != weights.shadowLogCorpsId) return false;
            }

            if (weights.shadowLogIntervalTurns <= 0)
                return true;

            // 计数按作用域分开：势力级与军团级互不干扰
            int key = DeploymentState.ScopeKey(force, corps);
            int count;
            visitCounter.TryGetValue(key, out count);
            count++;
            visitCounter[key] = count;
            return (count - 1) % weights.shadowLogIntervalTurns == 0;
        }

        /// <summary>清空计数（剧本收尾时调用，避免跨剧本残留）。</summary>
        public static void Clear()
        {
            visitCounter.Clear();
        }
    }
}
