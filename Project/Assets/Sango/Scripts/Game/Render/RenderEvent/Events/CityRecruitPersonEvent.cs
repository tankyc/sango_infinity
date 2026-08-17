using Sango.Core;
using Sango.Core.Action;
using System.Collections.Generic;

namespace Sango.Render
{
    public class CityRecruitPersonEvent : RenderEventBase
    {
        public Person person;
        public Person target;
        static List<ActionBase> sJobActions = new List<ActionBase>();

        public void Init(Person person, Person target)
        {
            this.person = person;
            this.target = target;
            IsDone = false;
        }
        void InitJobFeature(Person person)
        {
            sJobActions.Clear();
            if (person != null && person.mFeatureList != null)
            {
                for (int j = 0; j < person.mFeatureList.Count; j++)
                {
                    Feature feature = person.mFeatureList[j];
                    if (feature.kind == (int)FeatureKindType.CityProduce)
                        person.mFeatureList[j].InitActions(sJobActions, person.mCity);
                }
            }
        }

        void ClearJobFeature()
        {
            for (int i = 0; i < sJobActions.Count; i++)
                sJobActions[i].Clear();
            sJobActions.Clear();
        }

        public override void Enter(Scenario scenario)
        {
            InitJobFeature(person);
            if (!person.mCorps.IsPlayer)
            {
                person.JobRecruitPerson(target, (int)PersonRecruitType.Normal);
                IsDone = true;
                ClearJobFeature();
                return;
            }

            if (person.JobRecruitPerson(target, (int)PersonRecruitType.Normal))
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
                GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"很遗憾，\n未能招募到{target.ColorName}", () =>
                {
                    // TODO:展示武将
                    // 暂时直接招募
                    ClearJobFeature();
                    GameDialog.Close();
                    IsDone = true;
                });
                dialog1.SetPerson(person);
            }
            ClearJobFeature();

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
