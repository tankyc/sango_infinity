using Sango.Core;


namespace Sango.Render
{
    public class TroopAIEvent : RenderEventBase
    {
        public Troop troop;
        public System.Action doneAction;

        public void Init(Troop troop, System.Action doneAction)
        {
            this.troop = troop;
            this.doneAction = doneAction;
            IsDone = false;
        }

        public override void Enter(Scenario scenario)
        {
            Update(scenario, 0);
        }

        public override void Exit(Scenario scenario)
        {

        }

        public override bool IsVisible()
        {
            return troop.Render.IsVisible();
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!troop.IsAlive)
            {
                IsDone = true;
                doneAction?.Invoke();
                return IsDone;
            }

            IsDone = troop.DoAI(scenario);
            return IsDone;
        }
    }
}
