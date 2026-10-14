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
            // 判据用 IsPlayerControl（玩家势力 && 第一军团）而不是 IsPlayer：
            // 玩家势力委任军团由 AI 代管，不能弹玩家侧的武将台词对话框。
            if (!person.BelongCorps.IsPlayerControl)
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
            // 与 Enter 的判据保持一致：委任军团不参与玩家侧演出
            return person.BelongCorps.IsPlayerControl;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
