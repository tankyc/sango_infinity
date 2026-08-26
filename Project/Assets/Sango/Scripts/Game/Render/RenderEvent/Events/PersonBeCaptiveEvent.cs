using Sango.Core;

namespace Sango.Render
{
    public class PersonBeCaptiveEvent : RenderEventBase
    {
        public Person person;
        public Troop troop;

        public override void Enter(Scenario scenario)
        {
            IsDone = false;
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, 
                $"主公,{person.ColorName}被{troop.ColorName}俘虏了。", 
            () =>
            {
                IsDone = true;
            });
        }
    }
}
