using Sango.Core;
using System.Collections.Generic;
using System.Text;

namespace Sango.Render
{
    public class RenderEvent : Singleton<RenderEvent>
    {
        Queue<IRenderEventBase> eventQueue = new Queue<IRenderEventBase>();
        Stack<IRenderEventBase> listenerStack = new Stack<IRenderEventBase>();
        public void PushDepends(IRenderEventBase renderEventBase)
        {
            listenerStack.Push(renderEventBase);
        }
        public void PopDepends(IRenderEventBase renderEventBase)
        {
            if (renderEventBase != listenerStack.Peek())
            {
                Sango.Log.Error("栈不一致!!!");
            }
            listenerStack.Pop();
        }

        IRenderEventBase CurEvent { get; set; }

        /// <summary>待处理事件数（不含已处理但尚未清理的）</summary>
        public int EventCount => eventQueue.Count;

        public T Create<T>() where T : IRenderEventBase, new()
        {
            return new T();
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
            listenerStack.Clear();
            CurEvent = null;
        }

        /// <summary>
        /// 添加到当前队列或者添加进依赖父节点
        /// </summary>
        /// <param name="renderEvent"></param>
        public void Add(IRenderEventBase renderEvent)
        {
            IRenderEventBase dest;
            if (listenerStack.TryPeek(out dest))
            {
                dest.AddChild(renderEvent);
            }
            else
            {
                eventQueue.Enqueue(renderEvent);
            }
        }

        public bool Update(Scenario scenario, float deltaTime)
        {
            while (eventQueue.Count > 0)
            {
                CurEvent = eventQueue.Peek();
                try
                {
                    if (!CurEvent.IsInited)
                    {
                        CurEvent.IsInited = true;
                        CurEvent.Elapsed = 0f;      // 事件池化复用，进入时清零计时
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

        public string Dump()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"GameController.Enabled ={GameController.Instance.Enabled}");
            stringBuilder.AppendLine(GameSystemManager.Instance.Dump());
            stringBuilder.AppendLine($"CurrentCommand  ={GameSystemManager.Instance.CurrentCommand?.GetType()}");
            if (CurEvent != null)
            {
                stringBuilder.AppendLine($"Cur:{CurEvent.GetType()}->{CurEvent.IsDone}");
                stringBuilder.AppendLine(", Count:" + EventCount);
                IRenderEventBase [] renderEventBase_all = eventQueue.ToArray();
                for (int i = 0; i < renderEventBase_all.Length; i++)
                {
                    IRenderEventBase renderEventBase = renderEventBase_all[i];
                    stringBuilder.Append($", ({i}):{renderEventBase.GetType()} ->{renderEventBase.IsDone}");
                }
            }
            else
            {
                stringBuilder.Append("无!!");
            }
            //stringBuilder.AppendLine("dependsEvent:");
            //for (int i = 0; i < listenerStack.Count; ++i)
            //{
            //    IRenderEventBase renderEventBase = listenerStack[i];
            //    stringBuilder.Append($", ({i}):{renderEventBase.GetType()} ->{renderEventBase.IsDone}");
            //}

            stringBuilder.AppendLine($"GameController.KeyboardMoveEnabled ={GameController.Instance.KeyboardMoveEnabled}");
            stringBuilder.AppendLine($"GameController.RotateViewEnabled ={GameController.Instance.RotateViewEnabled}");
            stringBuilder.AppendLine($"GameController.BorderMoveViewEnabled ={GameController.Instance.BorderMoveViewEnabled}");
            stringBuilder.AppendLine($"GameController.ZoomViewEnabled ={GameController.Instance.ZoomViewEnabled}");
            stringBuilder.AppendLine($"GameController.DragMoveViewEnabled ={GameController.Instance.DragMoveViewEnabled}");
            return stringBuilder.ToString();
        }
    }
}
