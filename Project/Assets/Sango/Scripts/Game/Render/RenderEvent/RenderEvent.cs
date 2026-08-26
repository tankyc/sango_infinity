using Sango.Core;
using System.Collections.Generic;
using System.Text;

namespace Sango.Render
{
    public class RenderEvent : Singleton<RenderEvent>
    {
        List<IRenderEventBase> eventQueue = new List<IRenderEventBase>();
        List<IRenderEventBase> dependsEventQueue = new List<IRenderEventBase>();
        IRenderEventBase CurEvent { get; set; }
        int EventCount => eventQueue.Count;
        Dictionary<string, Stack<IRenderEventBase>> eventPool = new Dictionary<string, Stack<IRenderEventBase>>();

        public T Create<T>() where T : IRenderEventBase, new()
        {
            string key = typeof(T).FullName;

            Stack<IRenderEventBase> stack;
            if (!eventPool.TryGetValue(key, out stack))
            {
                stack = new Stack<IRenderEventBase>();
                eventPool[key] = stack;
            }

            if (stack.Count > 0)
            {
                return (T)stack.Pop();
            }
            else
            {
                return new T();
            }
        }

        public void Add(IRenderEventBase renderEvent)
        {
            if (renderEvent.MarkDepends)
                dependsEventQueue.Add(renderEvent);
            else
                eventQueue.Add(renderEvent);
        }

        public bool Update(Scenario scenario, float deltaTime)
        {
            int count = dependsEventQueue.Count;
            int evCount = eventQueue.Count;
            if (count > 0)
            {
                for (int i = 0; i < count; ++i)
                {
                    IRenderEventBase renderEventBase = dependsEventQueue[i];
                    if (renderEventBase.Update(scenario, deltaTime))
                    {
                        renderEventBase.IsDone = true;
                    }
                }
                dependsEventQueue.RemoveAll(x => x.IsDone);
            }

            if (evCount == 0 && count > 0)
                return false;

            while (eventQueue.Count > 0)
            {
                CurEvent = eventQueue[0];
                if (!CurEvent.IsInited)
                {
                    CurEvent.IsInited = true;
                    CurEvent.Enter(scenario);
                }

                if (!CurEvent.Update(scenario, deltaTime))
                    return false;

                CurEvent.Exit(scenario);
                eventQueue.RemoveAt(0);
                // 这里没考虑到同一帧复用导致其他类判断失败的问题
                //ReturnToPool(CurEvent);
                CurEvent = null;
            }

            return true;
        }

        public void ReturnToPool(IRenderEventBase renderEvent)
        {
            renderEvent.IsInited = false;

            string key = renderEvent.GetType().FullName;
            Stack<IRenderEventBase> stack;
            if (!eventPool.TryGetValue(key, out stack))
            {
                stack = new Stack<IRenderEventBase>();
                eventPool[key] = stack;
            }
            stack.Push(renderEvent);
        }

        public string Dump()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"GameController.Enabled ={GameController.Instance.Enabled}");
            stringBuilder.AppendLine($"CurrentCommand  ={GameSystemManager.Instance.CurrentCommand?.GetType()}");
            if (CurEvent != null)
            {
                stringBuilder.AppendLine($"Cur:{CurEvent.GetType()}->{CurEvent.IsDone}");
                stringBuilder.AppendLine(", Count:" + EventCount);
                for (int i = 0; i < EventCount; i++)
                {
                    IRenderEventBase renderEventBase = eventQueue[i];
                    stringBuilder.Append($", ({i}):{renderEventBase.GetType()} ->{renderEventBase.IsDone}");
                }
            }
            else
            {
                stringBuilder.Append("无!!");
            }
            stringBuilder.AppendLine($"GameController.KeyboardMoveEnabled ={GameController.Instance.KeyboardMoveEnabled}");
            stringBuilder.AppendLine($"GameController.RotateViewEnabled ={GameController.Instance.RotateViewEnabled}");
            stringBuilder.AppendLine($"GameController.BorderMoveViewEnabled ={GameController.Instance.BorderMoveViewEnabled}");
            stringBuilder.AppendLine($"GameController.ZoomViewEnabled ={GameController.Instance.ZoomViewEnabled}");
            stringBuilder.AppendLine($"GameController.DragMoveViewEnabled ={GameController.Instance.DragMoveViewEnabled}");
            return stringBuilder.ToString();
        }
    }
}
