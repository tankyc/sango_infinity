using Sango.Core;
using UnityEngine.UI;

namespace Sango.Render
{
    public class CityPersonSayEvent : RenderEventBase
    {
        public Person person;
        public string words;

        public void Init(Person person)
        {
            this.person = person;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            if(!person.mCorps.IsPlayer)
            {
                IsDone = true;
                return;
            }

            GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, words, () =>
            {
                GameDialog.Close();
                IsDone = true;
            }).SetPerson(person);
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
