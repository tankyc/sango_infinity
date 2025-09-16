namespace Sango.Game
{
    public abstract class Trigger
    {

        public delegate void TriggerCall(Trigger trigger);
        public TriggerCall triggerCall;
        public virtual Trigger Clone()
        {
            return null;
        }

        public virtual void Active(TriggerCall call)
        {
            triggerCall = call;
        }

        public virtual void Clear()
        {
        }
    }
}
