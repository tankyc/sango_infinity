using System.Collections.Generic;

namespace Sango.Game.Render
{
    public class RenderEvent : Singletion<RenderEvent>
    {
        Queue<IRenderEventBase> eventQueue = new Queue<IRenderEventBase>();
        IRenderEventBase CurEvent;
        public void Add(IRenderEventBase renderEvent)
        {
            eventQueue.Enqueue(renderEvent);
        }

        public void Update(float deltaTime)
        {
            if (CurEvent != null)
            {
                if (CurEvent.Update(deltaTime))
                    CurEvent = null;
            }

            if (CurEvent == null && eventQueue.Count > 0)
            {
                CurEvent = eventQueue.Dequeue();
            }
        }
    }
}
