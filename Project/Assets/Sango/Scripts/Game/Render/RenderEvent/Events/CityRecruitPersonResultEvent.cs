using Sango.Core;

namespace Sango.Render
{
    public class CityRecruitPersonResultEvent : RenderEventBase
    {
        public Person person;
        public Person target;
        public bool result;

        public void Init(Person person, Person target, bool result)
        {
            this.person = person;
            this.target = target;
            this.result = result;
            IsDone = false;
        }
        
        public override void Enter(Scenario scenario)
        {
            if (!person.mCorps.IsPlayer)
            {
                IsDone = true;
                return;
            }

            if (result)
            {
                GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"成功招募了{target.ColorName}", () =>
                {
                    // TODO:展示武将
                    // 暂时直接招募
                    GameDialog.Close();
                    GameDialog.IDialog dialog2 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                    {
                        // TODO:展示武将
                        // 暂时直接招募
                        GameDialog.Close();
                        IsDone = true;
                    });
                    dialog2.SetPerson(target);
                });
                dialog1.SetPerson(person);
            }
            else
            {
                GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"很遗憾，\n未能招募到 {target.ColorName}", () =>
                {
                    // TODO:展示武将
                    // 暂时直接招募
                    GameDialog.Close();
                    IsDone = true;
                });
                dialog1.SetPerson(person);
            }
        }

        public override void Exit(Scenario scenario)
        {

        }

        public override bool IsVisible()
        {
            return person.mCorps.IsPlayer;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
