using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    /// <summary>
    /// "兄弟同心"提气的演出事件。
    /// 只有玩家控制的部队才弹出提升士气(气力)的台词对话框, 说完才把气力推满;
    /// 电脑(AI)不弹对话框, 直接按已经判定通过的概率把气力推满, 不打断操作。
    /// 参战部队的"提气进行中"标记在排入本事件前就已被同步挂起, 这里负责在加气落地后解除,
    /// 从而保证"先提气、再攻击"。
    /// </summary>
    public class TroopSwornCheerEvent : RenderEventBase
    {
        Troop self;
        List<Troop> brothers;
        List<GameDialog.TalkData> talks;

        public void Init(Troop self, List<Troop> brothers, List<GameDialog.TalkData> talks)
        {
            this.self = self;
            this.brothers = brothers;
            this.talks = talks;
            IsInited = false;
            IsDone = false;
        }

        public override void Enter(Scenario scenario)
        {
            // talks 为 null 或空表示不需要演出(电脑部队, 或场上无人可说话): 直接加气
            if (talks != null && talks.Count > 0)
                GameDialog.StartTalk(talks, Finish);
            else
                Finish();
        }

        /// <summary>
        /// 加气落地并解除攻击挂起, 结束本事件
        /// </summary>
        void Finish()
        {
            if (self != null)
            {
                self.MarkCheerPending(brothers, false);
                self.CheerFullMorale(brothers);
            }
            IsDone = true;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
