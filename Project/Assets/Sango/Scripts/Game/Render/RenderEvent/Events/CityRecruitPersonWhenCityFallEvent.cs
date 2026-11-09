using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    /// <summary>
    /// 据点陷落 / 势力灭亡时的俘虏处置演出（对应 Open311 的 `System::horyo_shoguu`）。
    ///
    /// 【原版流程】s11_sys_event_kanraku.cpp:252-263 先 `capture()` 判定谁被俘，
    /// 再把"被俘者 + 未出阵的同势力者"一起交给 `horyo_shoguu(all, captured, self, by)`：
    ///   s11_sys_event_capture.cpp:570-689
    ///     ① 按身份排序（据点：mibun 降序，君主优先）；
    ///     ② `judgement()`：玩家走 `player_judgement`（逐个弹窗、未决者反复回到队列），
    ///        AI 走 `com_judgement`（六道闸门，见 <see cref="HoryoShoguu.AiJudge"/>）；
    ///     ③ 逐个结算（登用改属、收押关押、释放/逃脱回本国、处斩死亡）。
    ///
    /// 【本类与兵力溃败演出的区别】<see cref="CityRecruitPersonWhenTroopFallEvent"/> 处理的是
    /// "部队被击破"，俘虏终归挂在部队上；这里是"据点被攻陷"，处置结果落在被攻陷的据点
    /// （收押进 <see cref="City.AddCaptive"/>、释放则回本国据点）。
    /// 两者的裁决顺序共用 <see cref="HoryoShoguu"/>，只有落点不同。
    ///
    /// 【为什么要逐个处置而不是一次性弹窗】原版 `player_judgement` 是一个
    /// `while(true)` 循环，未决的俘虏会反复回到对话框，直到全部有归宿（:524-533）。
    /// 旧实现只给 `captiveList[0]` 弹一次窗就结束，第二个及以后的俘虏
    /// 既没被登用也没被收押/释放，会以"旧势力的人质"身份滞留在已易主的城里，
    /// 成为无法自愈的脏数据（既不在俘虏名单，也不在新势力名册）。
    /// </summary>
    public class CityRecruitPersonWhenCityFallEvent : RenderEventBase
    {
        /// <summary>待处置的俘虏（原版 `CaptureContext::horyo`）</summary>
        public List<Person> captiveList;
        /// <summary>被攻陷 / 被接收的据点</summary>
        public City targetCity;
        /// <summary>残部撤往的据点；为 null 表示这是该势力最后一座城（势力灭亡）</summary>
        public City escapeCity;
        /// <summary>攻方部队</summary>
        public Troop atk;
        /// <summary>登用类型（<see cref="PersonRecruitType"/>）</summary>
        public int recruitType;

        public void Init(List<Person> captiveList, City targetCity, City escapeCity, Troop atk, int recruitType)
        {
            this.captiveList = captiveList;
            this.targetCity = targetCity;
            this.escapeCity = escapeCity;
            this.atk = atk;
            this.recruitType = recruitType;
            IsDone = false;
        }

        public override void Enter(Scenario scenario)
        {
            if (captiveList == null || captiveList.Count == 0)
            {
                IsDone = true;
                return;
            }

            // 判据用 IsPlayerControl（玩家势力 && 第一军团）而不是 IsPlayer：
            // 玩家势力的委任军团 IsPlayer 也为真，但它由 AI 代管，不能走玩家交互分支，
            // 否则会在玩家当前界面之上叠出招募窗口，并让回合卡在等待玩家操作上。
            if (atk == null || atk.BelongCorps == null || !atk.BelongCorps.IsPlayerControl)
            {
                AiJudgeAll();
                IsDone = true;
                return;
            }

            Next();
        }

        /// <summary>
        /// AI 势力逐人裁决全部俘虏（裁决顺序见 <see cref="HoryoShoguu.AiJudge"/>）。
        /// </summary>
        void AiJudgeAll()
        {
            HoryoShoguu.AiJudgeAll(captiveList, BuildAiContext());
            captiveList.Clear();
        }

        /// <summary>
        /// 构造裁决上下文：据点陷落路径的差异只在"登用落点 = 被攻陷城"、
        /// "收押去处 = 被攻陷城"、"可用资金 = 随军金 + 城库存金"这三点上。
        /// （对应原版 `hobaku()` 的据点分支：`by_gold + target_gold &lt; pay_sum`，:214-228）
        /// </summary>
        /// <returns>裁决上下文</returns>
        HoryoShoguuContext BuildAiContext()
        {
            List<Person> members = new List<Person>();
            if (atk.Leader != null) members.Add(atk.Leader);
            if (atk.Member1 != null) members.Add(atk.Member1);
            if (atk.Member2 != null) members.Add(atk.Member2);

            return new HoryoShoguuContext
            {
                ByForce = atk.BelongForce,
                ByKunshu = atk.BelongForce != null ? atk.BelongForce.mGovernor : null,
                AvailableGold = atk.gold + (targetCity != null ? targetCity.gold : 0),
                PaySum = HoryoShoguu.SumPay(members),
                GetCaptiveCount = () => targetCity != null && targetCity.captiveList != null ? targetCity.captiveList.Count : 0,
                TryRecruit = p => atk.Leader != null && atk.Leader.JobRecruitPerson(p, targetCity, recruitType),
                Detain = p => targetCity.AddCaptive(p),
                Release = ReleaseCaptive,
                Execute = ExecuteCaptive,
            };
        }

        /// <summary>
        /// 处斩俘虏（对齐原版 `person_die(person, by_kunshu, by, ..., DeathType_NonNatural)`，
        /// 结算口径与 <see cref="PersonRecruit.KillTarget"/> 一致）。
        /// </summary>
        /// <param name="person">俘虏</param>
        /// <param name="byKunshu">下令的君主</param>
        void ExecuteCaptive(Person person, Person byKunshu)
        {
            person.Dead();
            GameEvent.OnPersonExecute?.Invoke(person, byKunshu != null ? byKunshu.BelongForce : atk?.BelongForce);
        }

        /// <summary>
        /// 释放俘虏（对齐原版 `HoryoShoguu_Kaihou` → `person_escape`，
        /// s11_sys_event_capture.cpp:662-675 / 487-518）。
        ///
        /// 释放 = 送回本国据点：城陷时俘虏的户口已被迁到 <see cref="escapeCity"/>
        /// （见 City.OnFall 的迁城段），这里只需要挂返程；
        /// 势力已灭的俘虏没有户口，ReturnHome 会直接清任务（转为在野，由归属巡检安置）。
        /// </summary>
        /// <param name="person">俘虏</param>
        void ReleaseCaptive(Person person)
        {
            PersonStateSystem.ReturnHome(person);
            GameEvent.OnPersonRelease?.Invoke(person, atk != null ? atk.BelongForce : null);
        }

        /// <summary>
        /// 处置队列中的第一个人（对齐 `player_judgement` 的 `while(true)` 循环，
        /// s11_sys_event_capture.cpp:521-558）。
        /// </summary>
        void Next()
        {
            // 先剔除已经失效的对象（阵亡 / 已被前置流程收编），避免为死人弹窗
            while (captiveList.Count > 0 && (captiveList[0] == null || captiveList[0].IsDead))
                captiveList.RemoveAt(0);

            if (captiveList.Count == 0)
            {
                IsDone = true;
                return;
            }

            Person target = captiveList[0];
            // 【原版对齐】登用次数不限：原版 player_judgement 里登用失败只把计数 +1，
            // 该俘虏仍留在未决队列，玩家可以反复尝试，直到主动改成收押/释放/处斩。
            GameSystem.GetSystem<PersonRecruit>().Start(targetCity, atk, target, captiveList, recruitType, PersonRecruit.UnlimitedTry, x =>
            {
                IsDone = true;
                if(captiveList.Count > 0)
                {
                    Person tp = captiveList[0];
                    ReleaseCaptive(target);
                }
                captiveList.Clear();
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
            return IsDone;
        }
    }
}
