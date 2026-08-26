using Sango.Core;

namespace Sango.Render
{
    public class PersonEscapeEvent : RenderEventBase
    {
        public Person person;

        public override void Enter(Scenario scenario)
        {
            IsDone = false;
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, 
                $"主公,不好了,{person.ColorName}乘机会逃跑了。", 
            () =>
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, 
                    $"哈哈哈,想关住我{person.ColorName},没门!!!",  () =>
                {
                    IsDone = true;
                }, person);
            });
        }
    }
}
