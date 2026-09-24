using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 影子模式入口（Phase A）：**只计算、不执行**。
    ///
    /// 注册进势力的 AI 命令队列（Phase C 后即势力唯一的人才部署入口），
    /// 报告反映的是"AI 在本回合开始时看到的人力分布"，可与岗位表直接对照。
    ///
    /// 输出只在编辑器 / 开发包，且可按势力 id 与回合间隔控制，避免刷屏。
    /// </summary>
    public static class DeploymentShadow
    {
        static readonly Dictionary<int, int> visitCounter = new Dictionary<int, int>();

        /// <summary>是否输出（编辑器 / 开发包）。</summary>
        static bool LogEnabled
        {
            get { return UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild; }
        }

        /// <summary>
        /// 势力 AI 命令：产出部署计划报告。永远返回 true（不阻断 AI 队列）。
        /// </summary>
        public static bool Run(Force force, Scenario scenario)
        {
            AIConfig config = AIConfig.Instance;
            if (config == null || config.deployment == null || !config.deployment.enabled)
                return true;
            if (!config.deployment.shadowLogEnabled || !LogEnabled)
                return true;
            if (!ShouldLog(force, config.deployment))
                return true;

            DeploymentPlan plan = DeploymentSolver.Solve(force, scenario);

            // 执行模式（shadowOnly=false）下报告实际执行条数；影子模式恒为 0
            string header = config.deployment.shadowOnly
                ? "[部署影子]"
                : string.Format("[部署执行] 本回合实际调动 {0} 人", DeploymentState.transferCountThisTurn);

            UnityEngine.Debug.Log(header + "\n" + plan.Report());
            return true;
        }

        /// <summary>是否轮到本势力输出（按配置的势力 id 与间隔）。</summary>
        static bool ShouldLog(Force force, DeploymentWeights weights)
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

            if (weights.shadowLogIntervalTurns <= 0)
                return true;

            int count;
            visitCounter.TryGetValue(force.Id, out count);
            count++;
            visitCounter[force.Id] = count;
            return (count - 1) % weights.shadowLogIntervalTurns == 0;
        }

        /// <summary>清空计数（剧本收尾时调用，避免跨剧本残留）。</summary>
        public static void Clear()
        {
            visitCounter.Clear();
        }
    }
}
