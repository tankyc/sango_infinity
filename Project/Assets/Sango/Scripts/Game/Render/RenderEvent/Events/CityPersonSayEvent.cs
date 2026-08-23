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
            if(!person.mBelongCorps.IsPlayer)
            {
                IsDone = true;
                return;
            }

            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, words, () =>
            {
                IsDone = true;
            }, person);
        }

        public override void Exit(Scenario scenario)
        {

        }

        public override bool IsVisible()
        {
            return person.mBelongCorps.IsPlayer;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
