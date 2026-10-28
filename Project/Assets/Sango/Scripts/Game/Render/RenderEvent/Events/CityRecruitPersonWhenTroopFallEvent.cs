using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    /// <summary>
    /// 部队被击破时的俘虏处置演出（对应 Open311 `System::horyo_shoguu` 的部队分支）。
    ///
    /// 【原版流程】s11_sys_scen_unit.cpp 的击破结算里，满足
    /// `存活 / can_capture / 双方势力不同 / 无血路技能` 才 `capture(...)`，
    /// 有俘虏就 `horyo_shoguu(all, captured, self, by)`，否则只播 `F_COM_BUTAI_KAIMETSU`。
    /// 裁决部分（六道闸门）与据点陷落完全一致，共用 <see cref="HoryoShoguu"/>；
    /// 差异只有落点：
    ///   · 登用 → 攻方主将所属据点（原版 `new_service = get_leader(self,1)->service`）；
    ///   · 收押 → 部队（<see cref="Troop.AddCaptive"/>，随军押送，回城后入城）；
    ///   · 可用资金 / 俸禄合计 → 攻方主将所属城（原版 `hobaku()` 的部队分支，:230-246）。
    ///
    /// 【注意】不能走玩家交互分支的判据见下方 Enter 的注释（委任军团）。
    /// </summary>
    public class CityRecruitPersonWhenTroopFallEvent : RenderEventBase
    {
        public List<Person> captiveList;
        public Troop atk;

        public void Init(List<Person> captiveList, Troop atk)
        {
            this.captiveList = captiveList;
            this.atk = atk;
            IsDone = false;
        }

        public override void Enter(Scenario scenario)
        {
            if (captiveList == null || captiveList.Count == 0)
            {
                IsDone = true;
                return;
            }

            // 判据必须用 IsPlayerControl（玩家势力 && 第一军团），不能用 IsPlayer：
            // 委任军团（玩家势力里非第一军团）的 IsPlayer 同样为真，但它由 AI 代管，
            // 一旦误走玩家分支就会在玩家当前界面（例如"创建队伍"）之上叠出招募窗口，
            // 而本事件只有等玩家操作完成后才会置 IsDone，于是整条回合推进
            // （Scenario.Run → RenderEvent.Update）被永久卡住。委任军团必须和 AI 势力一样无界面结算。
            if (atk == null || atk.BelongCorps == null || !atk.BelongCorps.IsPlayerControl)
            {
                HoryoShoguu.AiJudgeAll(captiveList, BuildAiContext());
                captiveList.Clear();
                IsDone = true;
                return;
            }

            Next();
        }

        /// <summary>
        /// 构造裁决上下文（部队击破路径）。
        ///
        /// 原版 `hobaku()` 的部队分支（s11_sys_event_capture.cpp:230-246）：
        /// <code>
        /// leader  = get_leader(self, 1)        // 攻方主将
        /// service = leader->service            // 攻方主将所属据点
        /// 没有据点 → 无法关押（返回 false）
        /// pay_sum = Σ(据点内籍武将俸禄) + (据点俘虏数 + 2) * horyo_pay_，再 ×2
        /// service 金 &lt; pay_sum → 无法关押
        /// </code>
        /// </summary>
        /// <returns>裁决上下文</returns>
        HoryoShoguuContext BuildAiContext()
        {
            // 攻方主将所属据点（原版 leader->service）
            City service = atk.Leader != null ? atk.Leader.BelongCity : null;
            if (service == null)
                service = atk.BelongCity;

            // 据点内在籍武将的俸禄合计（原版 get_pay_sum(get_building_member_list(service, ...))）
            List<Person> members = new List<Person>();
            if (service != null && service.allPersons != null)
                service.allPersons.ForEach(p => { if (p != null) members.Add(p); });

            return new HoryoShoguuContext
            {
                ByForce = atk.BelongForce,
                ByKunshu = atk.BelongForce != null ? atk.BelongForce.mGovernor : null,
                HasDetainFacility = service != null,
                AvailableGold = service != null ? service.gold : 0,
                PaySum = HoryoShoguu.SumPay(members),
                GetCaptiveCount = () => service != null && service.captiveList != null ? service.captiveList.Count : 0,
                TryRecruit = TryRecruit,
                Detain = DetainCaptive,
                Release = ReleaseCaptive,
                Execute = ExecuteCaptive,
            };
        }

        /// <summary>登用：成功时对玩家势力武将补一条"加入他势力"的提示演出</summary>
        /// <param name="person">俘虏</param>
        /// <returns>登用成功返回 true</returns>
        bool TryRecruit(Person person)
        {
            if (atk.Leader == null)
                return false;
            // recruitType = 1（有势力的俘虏），与原有口径一致
            if (!atk.Leader.JobRecruitPerson(person, atk.BelongCity, 1))
                return false;

            if (person.IsPlayer)
            {
                RenderEvent.Instance.Add(new PersonJoinToOtherForceEvent()
                {
                    person = person,
                    troop = atk
                });
            }
            Sango.Log.Info($"{person.Name} 加入了 {atk.BelongForce} 势力!!!");
            return true;
        }

        /// <summary>收押：随军押送（对齐原版 `person_hobaku(person, 攻方主将, ...)`）</summary>
        /// <param name="person">俘虏</param>
        void DetainCaptive(Person person)
        {
            atk.AddCaptive(person);
            if (person.IsPlayer)
            {
                RenderEvent.Instance.Add(new PersonBeCaptiveEvent()
                {
                    person = person,
                    troop = atk
                });
            }
        }

        /// <summary>
        /// 处斩（对齐原版 `person_die(person, by_kunshu, by, ..., DeathType_NonNatural)`）。
        /// </summary>
        /// <param name="person">俘虏</param>
        /// <param name="byKunshu">下令的君主</param>
        void ExecuteCaptive(Person person, Person byKunshu)
        {
            person.Dead();
            GameEvent.OnPersonExecute?.Invoke(person, byKunshu != null ? byKunshu.BelongForce : atk?.BelongForce);
        }

        /// <summary>释放：送回本国据点（对应 person_escape）</summary>
        /// <param name="person">俘虏</param>
        void ReleaseCaptive(Person person)
        {
            PersonStateSystem.ReturnHome(person);
            GameEvent.OnPersonRelease?.Invoke(person, atk != null ? atk.BelongForce : null);
        }

        void Next()
        {
            // 剔除已失效对象，避免为死人弹窗
            while (captiveList.Count > 0 && (captiveList[0] == null || captiveList[0].IsDead))
                captiveList.RemoveAt(0);

            if (captiveList.Count == 0)
            {
                IsDone = true;
                return;
            }

            Person target = captiveList[0];
            // 【原版对齐】登用次数不限：原版 player_judgement 里登用失败只把计数 +1，
            // 该俘虏仍留在未决队列，可以反复尝试，直到主动改成收押/释放/处斩。
            GameSystem.GetSystem<PersonRecruit>().Start(atk, target, 1, PersonRecruit.UnlimitedTry, x =>
            {
                // 玩家取消（result < 0）时按释放兜底：原版未决状态下必须给出处置。
                if (x == null || x.result < 0)
                {
                    Sango.Log.Info($"*[俘虏]{target.Name} 未作处置，按释放处理");
                    ReleaseCaptive(target);
                }

                captiveList.Remove(target);
                Next();
            });
        }

        public override void Exit(Scenario scenario)
        {

        }

        public override bool IsVisible()
        {
            // 与 Enter 的判据保持一致：委任军团不参与玩家侧演出
            return atk != null && atk.BelongCorps != null && atk.BelongCorps.IsPlayerControl;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!atk.ActionOver)
                return IsDone;

            return IsDone;
        }
    }
}
