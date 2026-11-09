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

        /// <summary>
        /// 演出上限。两层"点击推进"的对话正常几秒内看完；给 60s 是防
        /// "收尾回调被丢 → IsDone 永久 false → 串行演出队列停摆"的兜底线，
        /// 与 DiplomacyEvent.MaxDuration 同一套防卡死机制。已知会丢回调的路径：
        ///   · 玩家点取消 → GameDialog.OnWindowClosed(sure:false) → cancelAction，本事件过去没挂它；
        ///   · 窗口创建失败 → GameDialog.TryAdvance 只补 talkEndAction，sureAction 被丢弃。
        /// 登用数据在 Enter 里已一次性结算完（JobRecruitPerson 调用即出结果），
        /// 超时只需跳过剩余对话，OnForceComplete 用基类默认（空）即可。
        /// </summary>
        public override float MaxDuration => 10f;

        public void Init(Person person, Person target)
        {
            this.person = person;
            this.target = target;
            IsDone = false;
        }
        List<Feature> temp_FeatureList = new List<Feature>();
        void InitJobFeature(Person person)
        {
            sJobActions.Clear();
            temp_FeatureList.Clear();
            if (person != null && person.FeatureList != null)
            {
                for (int j = 0; j < person.FeatureList.Count; j++)
                {
                    Feature feature = person.FeatureList[j];
                    if (feature != null && feature.kind == (int)FeatureKindType.CityProduce)
                    {
                        if (!feature.only)
                        {
                            temp_FeatureList.Add(feature);
                            feature.InitActions(sJobActions, person.BelongCity, person);
                        }
                        else
                        {
                            if (!temp_FeatureList.Contains(feature))
                            {
                                temp_FeatureList.Add(feature);
                                feature.InitActions(sJobActions, person.BelongCity, person);
                            }
                        }
                    }
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
            // 【必须判空】出使途中执行武将可能已经变节 / 下野 / 阵亡，BelongCorps 被清空。
            // 旧写法第一行就解引用 person.BelongCorps，一旦为空就抛 NullReferenceException；
            // 异常从 RenderEvent.Update 的 while 循环里冒出去，CurEvent 不会被推进，
            // **整个演出队列就此卡死** —— 之后的移动、战斗、对话框全部不再播放，
            // 玩家看到的正是"既没有成功提示、也没有失败提示"。
            if (person == null || target == null)
            {
                IsDone = true;
                return;
            }

            InitJobFeature(person);

            // 判据用 IsPlayerControl（玩家势力 && 第一军团）而不是 IsPlayer：
            // 玩家势力委任军团的 IsPlayer 同样为真，但它由 AI 代管，
            // 走玩家分支会弹出"成功/失败"对话框并让回合卡在等待点击上。
            if (person.BelongCorps == null || !person.BelongCorps.IsPlayerControl)
            {
                IsDone = true;
                // 已无势力（下野 / 灭亡）的人不再结算登用，直接结束本事件
                if (person.BelongForce != null)
                    person.JobRecruitPerson(target, (int)PersonRecruitType.Normal);
                IsDone = true;
                ClearJobFeature();
                return;
            }

            if (person.JobRecruitPerson(target, (int)PersonRecruitType.Normal))
            {
                // 两层对话都要同时挂 sure / cancel：玩家点取消、或窗口被外部关闭时走的是 cancelAction，
                // 过去只挂 sureAction，取消一次 IsDone 就永远停在 false，串行演出队列从此卡死。
                // 登用结果在 JobRecruitPerson 里已经落库，这里回调只负责"演出收尾"，两条路等价。
                System.Action finish = () => IsDone = true;
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"成功招募了{target.ColorName}", () =>
                {
                    GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳",
                        finish, finish, target);
                }, finish, person);

            }
            else if (!Sango.Core.Debate.DebateConsequence.HasPending)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"很遗憾，\n未能招募到{target.ColorName}",
                    () => IsDone = true, () => IsDone = true, person);
            }
            else
            {
                IsDone = true;
            }
            // 登用失败被舌战接管时不播"很遗憾"：这时候舌战还没打（甚至还没确认是否观战），
            // 先播失败会误导玩家；真正的成败由 DebateConsequence 在舌战结束时统一播报。
            ClearJobFeature();
        }

        public override void Exit(Scenario scenario)
        {

        }

        public override bool IsVisible()
        {
            // 与 Enter 的判据保持一致：委任军团不参与玩家侧演出
            // 判空同 Enter：BelongCorps 可能因变节 / 下野被清空
            if (person == null || person.BelongCorps == null)
                return false;
            return person.BelongCorps.IsPlayerControl;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
