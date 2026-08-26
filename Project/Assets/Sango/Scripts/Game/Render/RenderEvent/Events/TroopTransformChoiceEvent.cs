using Sango.Core;
using Sango.Manager;
using System;
using UnityEngine;


namespace Sango.Render
{
    public class TroopTransformChoiceEvent : RenderEventBase
    {
        public PlayerChoice.ChoiceData[] choiceDatas;

        public override void Enter(Scenario scenario)
        {
            for (int i = 0; i < choiceDatas.Length; i++)
            {
                PlayerChoice.ChoiceData choiceData = choiceDatas[i];
                Action call = choiceData.call;
                choiceData.call = () =>
                {
                    IsDone = true;
                    call?.Invoke();
                };
                choiceDatas[i] = choiceData;
            }
            GameSystem.GetSystem<PlayerChoice>().Start(choiceDatas);
        }

        public override void Exit(Scenario scenario)
        {

        }

        public override bool IsVisible()
        {
            return true;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
