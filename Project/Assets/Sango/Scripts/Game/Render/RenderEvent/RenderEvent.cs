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

        public bool Update(Scenario scenario, float deltaTime)
        {
            while (CurEvent != null)
            {
                if (!CurEvent.Update(scenario, deltaTime))
                    return false;

                CurEvent.Exit(scenario);
                CurEvent = null;

                if (eventQueue.Count > 0)
                {
                    CurEvent = eventQueue.Dequeue();
                    CurEvent.Enter(scenario);
                }
            }

            if (eventQueue.Count > 0)
            {
                CurEvent = eventQueue.Dequeue();
                CurEvent.Enter(scenario);
                return false;
            }

            return true;
        }
    }
}
