using Sango.Core;

namespace Sango.Render
{
    public class PersonEscapeEvent : RenderEventBase
    {
        public Person father;
        public Person person;
        GameDialog.IDialog dialog;

        public override void Enter(Scenario scenario)
        {
            IsDone = false;
            string sex = person.sex == 1 ? "小女" : "犬子";
            string sex_say = person.sex == 1 ? "民女‌" : "小子";
            dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, 
                $"启禀主公,{sex}{person.ColorName}现已长大成人,可效力于军中,助主公一臂之力。", 
            () =>
            {
                dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, 
                    $"{sex_say}{person.ColorName},愿为主公赴汤蹈火,在所不辞!",  () =>
                {
                    GameMedia.Instance.PlayDoAcitonSfx();
                    GameDialog.Close();
                    IsDone = true;
                });
                if (person != null)
                    dialog.SetPerson(person);

                GameDialog.Close();
            });
             dialog.SetPerson(father);
        }
    }
}
