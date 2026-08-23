using Sango.Core;


namespace Sango.Render
{
    public class TroopEscapeToCityEvent : RenderEventBase
    {
        public Troop troop;
        public City dest;
        public System.Action doneAction;

        public void Init(Troop troop, City dest, System.Action doneAction)
        {
            this.troop = troop;
            this.dest = dest;
            this.doneAction = doneAction;
            IsDone = false;
        }

        public override void Enter(Scenario scenario)
        {
            if (troop.ActionOver)
                IsDone = true;
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
                MarkDepends = false;
                return true;
            }

            MarkDepends = true;
            if (troop.TryMoveToCity(dest))
            {
                MarkDepends = false;
                troop.ActionOver = true;
                // 移动完成，进入城市
                if (troop.cell.building == dest)
                {
                    troop.EnterCity(dest);
                }
                return true;
            }
            MarkDepends = false;
            return false;
        }
    }
}
