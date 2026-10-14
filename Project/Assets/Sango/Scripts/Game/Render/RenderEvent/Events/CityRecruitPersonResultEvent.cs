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
            // 判据用 IsPlayerControl（玩家势力 && 第一军团）而不是 IsPlayer：
            // 玩家势力委任军团由 AI 代管，不能弹玩家侧的招募结果对话框。
            if (!person.BelongCorps.IsPlayerControl)
            {
                IsDone = true;
                return;
            }

            if (result)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"成功招募了{target.ColorName}", () =>
                {
                    // TODO:展示武将
                    // 暂时直接招募
                    GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                    {
                        // TODO:展示武将
                        // 暂时直接招募
                        IsDone = true;
                    },target);
                },person);
            }
            else
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"很遗憾，\n未能招募到 {target.ColorName}", () =>
                {
                    // TODO:展示武将
                    // 暂时直接招募
                    IsDone = true;
                },person);
            }
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
