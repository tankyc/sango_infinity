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
            MarkDepends = true;
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
            if (!troop.IsAlive || troop.ActionOver)
            {
                IsDone = true;
                return true;
            }

            // 【空引用修复】逃回目标城市或其中心格为空时，无法寻路，直接结束本次行动，
            // 避免 TryMoveToCity 抛 NullReferenceException 且事件每帧重试刷屏卡死。
            if (dest == null || dest.CenterCell == null)
            {
                troop.ActionOver = true;
                IsDone = true;
                return true;
            }

            if (troop.TryMoveToCity(dest))
            {
                troop.ActionOver = true;
                // 移动完成，进入城市
                if (troop.cell.building == dest)
                {
                    troop.EnterCity(dest);
                }
                IsDone = true;
                return true;
            }
            return IsDone;
        }
    }
}
