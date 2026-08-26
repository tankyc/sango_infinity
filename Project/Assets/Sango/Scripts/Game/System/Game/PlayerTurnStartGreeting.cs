using Sango.Render;
using Sango.UI;

namespace Sango.Core.Player
{
    [GameSystem]
    public class PlayerTurnStartGreeting : GameSystem
    {
        public PlayerTurnStartGreeting()
        {

        }

        public override void OnEnter()
        {
            OnBack(null);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            Force force = Scenario.Cur.CurRunForce;
            //GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{force.ColorName}大人，\n终于轮到我们了啊。", null);
            Person person = force.mCounsellor;
            if (person == null || person.mBelongForce != force)
            {
                int max = force.CapitalCity.allPersons.Count;
                person = force.CapitalCity.allPersons.Get(GameRandom.Range(0, max));
            }
            GameMedia.Instance.PlaySfx(32);
            PlayerMessage.AddPersonMessage($"下一步，我们该干些什么呢？。", person);
            DialogEvent te = RenderEvent.Instance.Create<DialogEvent>();
            te.Init(GameDialog.DialogStyle.ClickPersonSay, $"{force.ColorName}大人，\n终于轮到我们了啊。", person, null, () =>
            {
                Done();
            });
            RenderEvent.Instance.Add(te);

        }

        public override void OnDestroy()
        {
            
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.ClickDown:
                case CommandEventType.RClickDown:
                    GameSystemManager.Instance.Back(); break;
            }

        }
    }
}
