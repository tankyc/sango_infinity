using Sango.Core;

namespace Sango.Render
{
    public class PersonValidEvent : RenderEventBase
    {
        public Province province;
        public Person person;
        GameDialog.IDialog dialog;

        public override void Enter(Scenario scenario)
        {
            IsDone = false;

            // 这里根据属性出现不同的提示

            dialog = GameDialog.Open(GameDialog.DialogStyle.Normal,
                $"据传闻,在{province.ColorName}出现了一名具有大才能的人。",
            () =>
            {
                GameDialog.Close();
                IsDone = true;
            });
        }
    }
}
