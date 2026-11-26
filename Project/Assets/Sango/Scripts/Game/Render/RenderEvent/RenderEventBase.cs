using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    public class RenderEventBase : IRenderEventBase
    {
        Queue<IRenderEventBase> eventQueue = new Queue<IRenderEventBase>();
        IRenderEventBase CurEvent { get; set; }

        public virtual bool IsInited { get; set; } = false;
        public virtual bool IsDone { get; set; } = false;
        public virtual bool IsVisible() => true;


        public virtual void Enter(Scenario scenario) { }
        public virtual void Exit(Scenario scenario) { }

        public virtual void LockDepends()
        {
            RenderEvent.Instance.PushDepends(this);
        }
        public virtual void ReleaseDepends()
        {
            RenderEvent.Instance.PopDepends(this);
        }
        public virtual void AddChild(IRenderEventBase child)
        {
            eventQueue.Enqueue(child);
        }
        protected virtual bool UpdateDepends(Scenario scenario, float deltaTime)
        {
            while (eventQueue.Count > 0)
            {
                CurEvent = eventQueue.Peek();
                try
                {
                    if (!CurEvent.IsInited)
                    {
                        CurEvent.IsInited = true;
                        CurEvent.LockDepends();
                        CurEvent.Enter(scenario);
                        CurEvent.ReleaseDepends();
                    }
                    bool finished = CurEvent.UpdateInternal(scenario, deltaTime);
                    if (!finished)
                        return false;       // 未完成：游标留在原地，下一帧继续处理它
                    CurEvent.Exit(scenario);
                }
                catch (System.Exception e)
                {
                    Sango.Log.Error($"*[演出]{CurEvent?.GetType().Name} 执行异常，已丢弃并继续：{e}");
                    try { CurEvent?.Exit(scenario); } catch { }
                }
                eventQueue.Dequeue();
                CurEvent = null;
            }
            return true;
        }
        public virtual bool UpdateInternal(Scenario scenario, float deltaTime)
        {
            // 优先子事件的播放
            if (!UpdateDepends(scenario, deltaTime))
                return false;

            return Update(scenario, deltaTime);
        }

        public virtual bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
