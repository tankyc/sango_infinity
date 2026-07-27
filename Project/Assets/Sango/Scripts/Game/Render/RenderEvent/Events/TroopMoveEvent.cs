using Sango.Core;
using Sango.Manager;
using UnityEngine;


namespace Sango.Render
{
    public class TroopMoveEvent : RenderEventBase
    {
        public Troop troop;
        public Cell start;
        public Cell dest;
        public bool isLastMove;
        public System.Action doneAction;
        int soundChannel = -1;

        public void Init(Troop troop, Cell start, Cell dest, bool isLastMove, System.Action doneAction)
        {
            this.troop = troop;
            this.start = start;
            this.dest = dest;
            this.isLastMove = isLastMove;
            this.doneAction = doneAction;
            IsDone = false;
        }

        public override void Enter(Scenario scenario)
        {
            if (IsVisible())
            {
                troop.Render.SetSmokeShow(true);
                soundChannel = GameMedia.Instance.PlaySfx(troop.TroopType.moveSound);
            }
        }

        public override void Exit(Scenario scenario)
        {
            if (IsVisible() && isLastMove)
            {
                troop.Render.SetSmokeShow(false);
            }
            if (soundChannel >= 0)
                GameMedia.Instance.StopSfx(soundChannel);
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

            if (!troop.IsAlive)
            {
                IsDone = true;
                doneAction?.Invoke();
                return IsDone;
            }

            if (!IsVisible() || Input.GetMouseButtonDown(0))
            {
                troop.Render.SetForward(dir);
                troop.UpdateCell(dest, start, isLastMove);
                IsDone = true;
                doneAction?.Invoke();
                return IsDone;
            }

            //troop.Render.SetSmokeShow();



            Vector3 newPos = troop.Render.GetPosition() + dir * (GameVariables.TroopMoveSpeed * deltaTime);

            if (Vector3.Dot(newPos - destPosition, dir) >= 0)
            {
                newPos = destPosition;

                troop.Render.SetForward(dir);
                troop.Render.SetPosition(newPos);
                troop.UpdateCell(dest, start, isLastMove);
                IsDone = true;
                doneAction?.Invoke();
                return IsDone;
            }
            else
            {
                newPos.y = MapRender.QueryHeight(newPos);
                troop.Render.SetForward(dir);
                troop.Render.SetPosition(newPos);
                return IsDone;
            }
        }
    }
}
