using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    public interface IRenderEventBase
    {
        void LockDepends();
        void ReleaseDepends();

        void AddChild(IRenderEventBase child);

        bool IsInited { get; set; }
        bool IsDone { get; set; }

        /// <summary>
        /// 演出最长时长（秒），由 <see cref="RenderEvent"/> 的 watchdog 计时。
        /// 0 = 不限制。**等待玩家交互的演出不要设**，否则挂机的玩家会被强跳。
        /// 外交演出（DiplomacyEvent）设了上限：它的结算挂在对话收尾回调上，
        /// 回调一旦被丢（GameDialog 窗口创建失败 / 对话被外部关闭），
        /// IsDone 永远不复位，Scenario.Run 会每帧提前 return，回合推进彻底停摆。
        /// </summary>
        float MaxDuration { get; }

        /// <summary>已播放时长（秒）。由 RenderEvent 逐帧累加，事件进入（Enter）时清零。</summary>
        float Elapsed { get; set; }

        bool IsVisible();
        bool UpdateInternal(Scenario scenario, float deltaTime);
        bool Update(Scenario scenario, float deltaTime);
        
        void Enter(Scenario scenario);
        void Exit(Scenario scenario);

        /// <summary>
        /// watchdog 强制完成时的兜底（在 Exit 之前调用）。
        /// 有"必须落地的业务结果"的事件（如外交结算）在这里做幂等收尾，
        /// 保证即使演出链断了，数据侧也不缺账。
        /// </summary>
        void OnForceComplete(Scenario scenario);
    }
}
