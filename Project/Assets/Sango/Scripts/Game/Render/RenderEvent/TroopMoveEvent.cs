using Sango.Render;
using UnityEngine;


namespace Sango.Game.Render
{
    public struct TroopMoveEvent : IRenderEventBase
    {
        public Troop troop;
        public Cell start;
        public Cell dest;
        public bool isLastMove;

        public bool IsVisible()
        {
            return troop.Render.IsVisible();
        }

        public bool Update(float deltaTime)
        {
            //troop.Render.SetSmokeShow();
            Vector3 destPosition = dest.Position;
            Vector3 startPosition = start.Position;
            Vector3 dir = destPosition - startPosition;
            dir.y = 0;
            dir.Normalize();

            Vector3 newPos = troop.Render.GetPosition() + dir * (GameVariables.TroopMoveSpeed * deltaTime);
            
            if( Vector3.Dot(newPos - destPosition, dir) >= 0)
            {
                newPos = destPosition;
                troop.Render.SetForward(dir);
                troop.Render.SetPosition(newPos);
                return true;
            }
            else
            {
                newPos.y = MapRender.QueryHeight(newPos);
                troop.Render.SetForward(dir);
                troop.Render.SetPosition(newPos);
                return false;
            }
        }
    }
}
