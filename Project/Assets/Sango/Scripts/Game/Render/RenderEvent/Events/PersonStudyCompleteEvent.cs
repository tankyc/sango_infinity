using Sango.Core;

namespace Sango.Render
{
    /// <summary>
    /// 能力培育（学习）完成事件 —— 武将对话框反馈。
    ///
    /// <para>
    /// 【为什么走 RenderEvent 而不是直接弹对话框】培育任务在回合结算时到期
    /// （<c>Person.UpdateMission</c> → <c>AbilityStudySystem.FinishStudyMission</c>），
    /// 那一刻流程里可能还排着部队移动、城池结算等演出。直接 <c>GameDialog.Open</c> 会跟它们抢窗口，
    /// 所以入 RenderEvent 队列，轮到本事件时再弹，弹完（玩家点掉）才继续后面的演出。
    /// </para>
    ///
    /// 【触发范围】只有玩家势力才入队（见 <c>AbilityStudySystem.NotifyStudyComplete</c>），
    /// AI 势力静默结算，避免回合推进被一堆对话框拖住。
    /// </summary>
    public class PersonStudyCompleteEvent : RenderEventBase
    {
        /// <summary>完成研习的武将</summary>
        public Person person;

        /// <summary>武将要说的台词</summary>
        public string message;

        /// <summary>
        /// 初始化（事件池复用，必须每次重置状态）。
        /// </summary>
        /// <param name="person">完成研习的武将</param>
        /// <param name="message">台词</param>
        public void Init(Person person, string message)
        {
            this.person = person;
            this.message = message;
            IsDone = false;
        }

        public override void Enter(Scenario scenario)
        {
            IsDone = false;

            // 武将可能在排队的这段时间里阵亡 / 离队，这时直接结束，不要弹一个指向空对象的对话框
            if (person == null || !person.IsAlive)
            {
                IsDone = true;
                return;
            }

            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, message,
            () =>
            {
                GameMedia.Instance.PlayDoAcitonSfx();
                IsDone = true;
            }, person);
        }
    }
}
