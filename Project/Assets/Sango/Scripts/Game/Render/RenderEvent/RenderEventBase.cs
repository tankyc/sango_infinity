using Sango.Core;

namespace Sango.Render
{
    public class RenderEventBase : IRenderEventBase
    {
        public virtual bool IsInited { get; set; }
        public virtual bool IsDone { get; set; }
        public virtual bool IsVisible() => true;
        public virtual bool MarkDepends{ get; set; }

        /// <summary>演出上限（秒）。默认 0 = 不限制，等待玩家交互的演出不要设。</summary>
        public virtual float MaxDuration => 0f;

        /// <summary>已播放时长。RenderEvent 逐帧累加，Enter 时清零（事件池化复用）。</summary>
        public virtual float Elapsed { get; set; }

        public virtual bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
        public virtual void Enter(Scenario scenario) { }
        public virtual void Exit(Scenario scenario) { }

        /// <summary>watchdog 强制完成兜底。默认无操作，需要"必须落地业务结果"的事件自行覆写。</summary>
        public virtual void OnForceComplete(Scenario scenario) { }
    }
}
