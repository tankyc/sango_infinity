using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityExilePerson : CityBaseSystem
    {
        public Force TargetForce;
        public List<Person> targetList = new List<Person>();

        public CityExilePerson()
        {
            customTitleName = "流放";
            customMenuName = "君主/流放";
            customMenuOrder = 4500;
            windowName = "";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByIsCounsellor,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByOfficial,
                PersonSortFunction.SortByMerit,
            };

        }
        protected override bool MenuCanShow()
        {
            return TargetCity.IsCityBase();
        }
        public override bool IsValid
        {
            get
            {

                TargetForce = TargetCity.BelongForce;
                targetList.Clear();
                // 属下（在职武将）
                Scenario.Cur.personSet.ForEach(x =>
                {
                    if (x.BelongForce != TargetForce || x.IsPrisoner)
                        return;
                    // 身份必须是 都督 / 太守 / 一般 —— 君主不在其中，天然被排除；
                    // 军师若挂着普通身份则**同样可被流放**（原版不排除军师）。
                    // 对齐 can_banish_command（s11_sys_person_order_kunshu.cpp:1189-1192）。
                    if (x.state != (int)PersonStateType.Commander
                        && x.state != (int)PersonStateType.Leader
                        && x.state != (int)PersonStateType.Normal)
                        return;
                    // 在部队中不可流放 —— `is_unitize()`
                    if (x.mBelongTroop != null)
                        return;
                    // 任务进行中不可流放 —— `is_absent()`（order_timer_ != 0）
                    if (x.missionCounter != 0)
                        return;
                    // 事件 / NPC / 异族不参与人事处置
                    if (x.IsSpecialPerson)
                        return;
                    targetList.Add(x);
                });

                // 俘虏
                Scenario.Cur.citySet.ForEach((System.Action<City>)(x =>
                {
                    if (x.BelongForce == TargetForce)
                    {
                        x.captiveList.ForEach(y =>
                        {
                            targetList.Add(y);
                        });
                    }
                }));

                return targetList.Count > 0;
            }
        }

        public override void OnEnter()
        {
            personList.Clear();
            targetList.Sort((a, b) => -PersonSortFunction.SortByIntelligence.valueSortFunc.Invoke(a, b));
            PersonSelectSystem personSelectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            personSelectSystem.Start(
                targetList,
                personList, targetList.Count, OnPersonSelected, null, null);
        }

        void OnPersonSelected(List<Person> person_list)
        {
            if (person_list.Count <= 0)
            {
                Done();
                return;
            }

            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"再见了!!", () =>
            {
                // 暂时直接招募
                Done();

            }, null, person_list[0]);

            string content = "";
            if (person_list.Count > 1)
            {
                content = $"流放了{person_list[0].ColorName}等{person_list.Count}人!";
            }
            else
            {
                content = $"流放了{person_list[0].ColorName}!";
            }
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, content, () =>
            {
                int techPointDelta = 0;
                for (int i = 0; i < person_list.Count; i++)
                {
                    Person person = person_list[i];

                    // 技巧点增减：`Σ五维 / 100 + 5`，**流放武将扣、释放俘虏加**。
                    // 对齐 exec_banish_command（s11_sys_person_order_kunshu.cpp:1255-1288）。
                    int statSum = (person.command != null ? person.command.baseValue : 0)
                                + (person.strength != null ? person.strength.baseValue : 0)
                                + (person.intelligence != null ? person.intelligence.baseValue : 0)
                                + (person.politics != null ? person.politics.baseValue : 0)
                                + (person.glamour != null ? person.glamour.baseValue : 0);
                    int delta = statSum / 100 + 5;

                    if (person.IsPrisoner)
                    {
                        person.Escape(EscapeType.Released, TargetForce);
                        techPointDelta += delta;
                    }
                    else
                    {
                        person.LeaveToWild();
                        // 流放到临近都市
                        person.ChangeBelongCity(TargetCity.RandomNerghbor());
                        person.CurrentCity = person.BelongCity;
                        TargetForce.PersonCount--;
                        techPointDelta -= delta;
                    }
                }

                if (techPointDelta != 0)
                    TargetForce.GainTechniquePoint(techPointDelta);

                TargetForce.ForEachCorps(x =>
                {
                    x.CheckValid();
                });

                Done();

            }, 56);
        }

        public override void OnDestroy()
        {

        }
    }
}
