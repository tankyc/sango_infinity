using Sango.Core;

namespace Sango.Render
{
    public class PersonJoinToOtherForceEvent : RenderEventBase
    {
        public Person person;
        public Troop troop;

        public override void Enter(Scenario scenario)
        {
            IsDone = false;
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, 
                $"不好了,{person.ColorName}加入了{troop.mBelongForce.ColorName}。", 
            () =>
            {
                IsDone = true;
            });
        }
    }
}
