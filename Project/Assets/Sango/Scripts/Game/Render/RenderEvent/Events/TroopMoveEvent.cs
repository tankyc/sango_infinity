using Sango.Render;
using UnityEngine;


namespace Sango.Game.Render
{
    public class TroopMoveEvent : RenderEventBase
    {
        public Troop troop;
        public Cell start;
        public Cell dest;
        public bool isLastMove;

        public override void Enter(Scenario scenario)
        {
            if (IsVisible())
            {
                troop.Render.SetSmokeShow(true);
            }
        }

        public override void Exit(Scenario scenario)
        {
            if (IsVisible() && isLastMove)
            {
                troop.Render.SetSmokeShow(false);
            }
        }

        public override bool IsVisible()
        {
            return troop.Render.IsVisible();
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            Vector3 destPosition = dest.Position;
            Vector3 startPosition = start.Position;
            Vector3 dir = destPosition - startPosition;
            dir.y = 0;
            dir.Normalize();

            if (!IsVisible())
            {
                troop.Render.SetForward(dir);
                troop.UpdateCell(dest, start, isLastMove);
                IsDone = true;
                return IsDone;
            }

            //troop.Render.SetSmokeShow();
         
           

            Vector3 newPos = troop.Render.GetPosition() + dir * (GameVariables.TroopMoveSpeed * deltaTime);
            
            if( Vector3.Dot(newPos - destPosition, dir) >= 0)
            {
                newPos = destPosition;
              
                troop.Render.SetForward(dir);
                troop.Render.SetPosition(newPos);
                troop.UpdateCell(dest, start, isLastMove);
                IsDone = true;
                return IsDone;
            }
            else
            {
                newPos.y = MapRender.QueryHeight(newPos);
                troop.Render.SetPosition(newPos);
                return IsDone;
            }
        }
    }
}
