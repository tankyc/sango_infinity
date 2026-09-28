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
        Dictionary<string, Stack<IRenderEventBase>> eventPool = new Dictionary<string, Stack<IRenderEventBase>>();

        /// <summary>
        /// eventQueue 的读取游标。
        ///
        /// 原来每处理完一个事件就 <c>RemoveAt(0)</c>，整队处理完是 O(n²) 次数组搬移；
        /// 而移动演出是"路径每格排一个事件"（见各 <c>TroopAction*</c>），
        /// 队列很容易上百，长距离移动时这段搬移是实打实的开销。
        /// 改成游标前进、末尾一次性 <c>RemoveRange</c>，摊还成 O(n)。
        ///
        /// 语义与原来一致：
        ///   · Update 期间新增的事件只追加到队尾，游标自然能处理到；
        ///   · 事件未完成时 return false，游标停在原地，下一帧从同一个事件继续；
        ///   · <see cref="Reset"/> 必须把游标归零，否则读档后残留游标会跳过新事件。
        /// </summary>
        int queueHead;

        /// <summary>待处理事件数（不含已处理但尚未清理的）</summary>
        int EventCount => eventQueue.Count - queueHead;

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

        /// <summary>
        /// 清空待播事件与事件池（读档 / 回主菜单时调用）。
        ///
        /// 不清的后果：旧剧本的事件会留在队列里，新剧本第一次 Run 会把它当成
        /// "已经 Enter 过"的对象继续 Update —— 目标是旧剧本的部队 / 城池，
        /// 表现为"读档后莫名播上一次的动画，或者直接卡住"。
        /// </summary>
        public void Reset()
        {
            eventQueue.Clear();
            dependsEventQueue.Clear();
            eventPool.Clear();
            CurEvent = null;
            queueHead = 0;          // 游标必须一起归零
        }

        /// <summary>是否已经没有待播事件（复位验证用）</summary>
        public bool IsIdle
        {
            get { return EventCount == 0 && dependsEventQueue.Count == 0 && CurEvent == null; }
        }

        public void Add(IRenderEventBase renderEvent)
        {
            if (renderEvent.MarkDepends)
            {
                dependsEventQueue.Add(renderEvent);
            }
            else
                eventQueue.Add(renderEvent);
        }

        public bool Update(Scenario scenario, float deltaTime)
        {
            int count = dependsEventQueue.Count;
            int evCount = EventCount;       // 待处理数量（与原来的 eventQueue.Count 同义）
            if (count > 0)
            {
                for (int i = 0; i < count; ++i)
                {
                    IRenderEventBase renderEventBase = dependsEventQueue[i];

                    if (!renderEventBase.IsInited)
                    {
                        renderEventBase.IsInited = true;
                        renderEventBase.Enter(scenario);
                    }

                    if (renderEventBase.Update(scenario, deltaTime))
                    {
                        renderEventBase.IsDone = true;
                        renderEventBase.Exit(scenario);
                    }
                }
                dependsEventQueue.RemoveAll(x => x.IsDone);
            }

            if (evCount == 0 && count > 0)
                return false;

            while (queueHead < eventQueue.Count)
            {
                CurEvent = eventQueue[queueHead];
                if (!CurEvent.IsInited)
                {
                    CurEvent.IsInited = true;
                    CurEvent.Enter(scenario);
                }

                if (!CurEvent.Update(scenario, deltaTime))
                    return false;       // 未完成：游标留在原地，下一帧继续处理它

                CurEvent.Exit(scenario);
                // 这里没考虑到同一帧复用导致其他类判断失败的问题
                //ReturnToPool(CurEvent);
                CurEvent = null;
                queueHead++;
            }

            if (queueHead > 0)
            {
                // 一次性搬移：把原来每个事件一次的 O(n) 移位，摊还成整队一次 O(n)
                eventQueue.RemoveRange(0, queueHead);
                queueHead = 0;
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
            stringBuilder.AppendLine(GameSystemManager.Instance.Dump());
            //stringBuilder.AppendLine($"CurrentCommand  ={GameSystemManager.Instance.CurrentCommand?.GetType()}");
            if (CurEvent != null)
            {
                stringBuilder.AppendLine($"Cur:{CurEvent.GetType()}->{CurEvent.IsDone}");
                stringBuilder.AppendLine(", Count:" + EventCount);
                for (int i = queueHead; i < eventQueue.Count; i++)
                {
                    IRenderEventBase renderEventBase = eventQueue[i];
                    stringBuilder.Append($", ({i - queueHead}):{renderEventBase.GetType()} ->{renderEventBase.IsDone}");
                }
            }
            else
            {
                stringBuilder.Append("无!!");
            }
            stringBuilder.AppendLine("dependsEvent:");
            for (int i = 0; i < dependsEventQueue.Count; ++i)
            {
                IRenderEventBase renderEventBase = dependsEventQueue[i];
                stringBuilder.Append($", ({i}):{renderEventBase.GetType()} ->{renderEventBase.IsDone}");
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
