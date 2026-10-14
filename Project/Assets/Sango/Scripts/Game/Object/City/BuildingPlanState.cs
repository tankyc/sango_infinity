/*
 * 文件名：BuildingPlanState.cs
 * 描述：内政建筑规划的运行期状态（不序列化）—— 目前只承载"更换建筑的冷却"。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 内政建筑规划的运行期状态（**不序列化**，换剧本由 <see cref="ScenarioLifecycle"/> 复位）。
    ///
    /// 只承担一件事：**更换冷却** —— 同一座城两次"拆旧建新"之间必须间隔若干回合。
    /// 没有冷却的话，AI 会在权重轻微波动时反复拆建：每拆一次都要重走一遍工期，
    /// 收益全耗在重建期里，反而比不换更差。
    ///
    /// 与 <see cref="ResourceDispatchState"/> / <see cref="DeploymentState"/> 同一范式
    /// （静态表 + 回合号判定 + 跨剧本 Clear）。
    /// </summary>
    public static class BuildingPlanState
    {
        /// <summary>城池 id → 最近一次"更换建筑"成功的回合号</summary>
        static readonly Dictionary<int, int> lastReplaceTurn = new Dictionary<int, int>();

        /// <summary>
        /// 本城距上次更换是否已过冷却期。
        /// </summary>
        /// <param name="cityId">城池 id</param>
        /// <param name="turn">当前回合号（<see cref="Scenario.TurnCount"/>）</param>
        /// <param name="cooldownTurns">冷却回合数（&lt;= 0 表示不限制）</param>
        /// <returns>可以更换返回 true</returns>
        public static bool CanReplace(int cityId, int turn, int cooldownTurns)
        {
            if (cityId <= 0)
                return false;

            if (cooldownTurns <= 0)
                return true;                                    // 冷却关闭

            int last;
            if (!lastReplaceTurn.TryGetValue(cityId, out last))
                return true;                                    // 从未换过

            return turn - last >= cooldownTurns;
        }

        /// <summary>
        /// 登记"本城刚完成一次更换"（只有真正拆建成功才调用，失败不占用冷却）。
        /// </summary>
        /// <param name="cityId">城池 id</param>
        /// <param name="turn">当前回合号</param>
        public static void MarkReplaced(int cityId, int turn)
        {
            if (cityId > 0)
                lastReplaceTurn[cityId] = turn;
        }

        /// <summary>清空全部运行期状态（换剧本时由 <see cref="ScenarioLifecycle"/> 调用）。</summary>
        public static void Clear()
        {
            lastReplaceTurn.Clear();
        }
    }
}
