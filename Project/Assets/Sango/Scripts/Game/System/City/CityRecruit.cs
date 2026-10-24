using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityRecruit : CityBaseSystem
    {
        public string customTargetTitleName;
        public string customActionTitleName;
        public List<ObjectSortTitle> customTargetTitleList;
        public List<ObjectSortTitle> customActionTitleList;
        public List<Person> targetList = new List<Person>();
        public List<Person> target = new List<Person>();
        public List<Person> counsellorRecommendList = new List<Person>();

        public CityRecruit()
        {

            customTargetTitleName = "登庸";
            customTitleName = customTargetTitleName;
            customActionTitleName = "执行武将";

            customMenuName = "人事/登庸武将";
            customMenuOrder = 2300;
            windowName = "window_city_recruit";

            customActionTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.GetSortByRecruitRecommend(counsellorRecommendList),
                PersonSortFunction.SortByGlamour,
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
                if (TargetCity.freePersons.Count <= 0)
                    return false;
                if (TargetCity.BelongCorps.ActionPoint < TargetCity.GetJobCostAP(CityJobType.RecruitPerson))
                    return false;
                // 必须存在可登庸目标。
                // 对齐原版 can_employ_command 的 `get_employable_person_list(self).size()` 判定
                // （s11_sys_person_order_personnel.cpp:725）—— 原实现漏了这条，
                // 全地图没人可登用时按钮仍亮，点进去是空列表。
                return HasEmployableTarget();
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            if (TargetCity.freePersons.Count <= 0)
                return "无空闲武将";
            // 与 IsValid 同源：走本城口径（人才府可减半）
            if (TargetCity.BelongCorps.ActionPoint < TargetCity.GetJobCostAP(CityJobType.RecruitPerson))
                return "行动力不足";
            if (!HasEmployableTarget())
                return "无可登用对象";
            return null;
        }

        /// <summary>
        /// 是否存在可登庸目标（与 OnEnter 填充候选池的口径完全一致）。
        /// </summary>
        /// <returns>存在返回 true</returns>
        bool HasEmployableTarget()
        {
            List<Person> buffer = new List<Person>();
            CollectTargets(buffer);
            return buffer.Count > 0;
        }

        /// <summary>
        /// 收集登庸候选。
        ///
        /// 口径：
        ///   · 他势力城：该城全部武将里排除 君主 / 俘虏 / 被本势力禁止仕官者；
        ///   · 他势力城：在野武将（排除禁仕官）；
        ///   · 本势力城：在野武将 + 俘虏。
        /// <see cref="IsValid"/> 与 <see cref="OnEnter"/> 共用本方法，避免两处口径分叉。
        /// </summary>
        /// <param name="dest">输出列表</param>
        void CollectTargets(List<Person> dest)
        {
            if (dest == null || TargetCity == null || TargetCity.BelongForce == null || Scenario.Cur == null)
                return;

            Force force = TargetCity.BelongForce;
            Scenario.Cur.citySet.ForEach(x =>
            {
                if (x == null || !x.IsAlive)
                    return;

                if (!x.IsSameForce(TargetCity))
                {
                    x.allPersons.ForEach(y =>
                    {
                        // 【禁止仕官】半年内被本势力拒绝过的人不进候选（对应 Open311 banned_kunshu）
                        // 【新增】排除事件/NPC/异族，以及已在部队中的敌将
                        // —— 对齐 can_employ 的 `is_event() or is_iminzoku() or is_bandit()` 与 `is_unitize()`
                        if (y.state == (int)PersonStateType.Governor
                            || y.state == (int)PersonStateType.Prisoner)
                            return;
                        if (y.IsBannedFrom(force) || y.IsSpecialPerson)
                            return;
                        if (y.mBelongTroop != null)
                            return;
                        dest.Add(y);
                    });
                    x.wildPersons.ForEach(y =>
                    {
                        if (y.IsBannedFrom(force) || y.IsSpecialPerson)
                            return;
                        dest.Add(y);
                    });
                }
                else
                {
                    x.wildPersons.ForEach(y =>
                    {
                        if (y.IsBannedFrom(force) || y.IsSpecialPerson)
                            return;
                        dest.Add(y);
                    });
                    x.captiveList.ForEach(y =>
                    {
                        if (y != null && !y.IsSpecialPerson)
                            dest.Add(y);
                    });
                }
            });
        }

        public override void OnEnter()
        {
            personList.Clear();
            target.Clear();
            counsellorRecommendList.Clear();

            customTargetTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByBelongForce,
                PersonSortFunction.SortByState,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.GetSortByDistanceDay(TargetCity),
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByCurrentCity,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
            };
            targetList.Clear();
            // 与 IsValid 的"是否存在可登庸目标"共用同一份收集逻辑
            CollectTargets(targetList);
            targetList.Sort((a, b) =>
            {
                if (a.state == b.state)
                {
                    if (a.loyalty == b.loyalty)
                    {
                        return a.DistanceDays(TargetCity).CompareTo(b.DistanceDays(TargetCity));
                    }
                    else
                    {
                        return a.loyalty.CompareTo(b.loyalty);
                    }
                }
                else
                {
                    return -a.state.CompareTo(b.state);
                }
            });

            if (customActionTitleList == null)
            {
                customActionTitleList = new List<ObjectSortTitle>()
                {
                    PersonSortFunction.SortByName,
                    PersonSortFunction.GetSortByRecruitRecommend(counsellorRecommendList),
                    PersonSortFunction.SortByGlamour,
                };
            }

            Window.Instance.Open(windowName);
        }
        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        public void SetTarget(List<Person> target)
        {
            if (target.Count == 0)
                return;

            this.target = target;
            personList.Clear();

            if (target.Count > 0)
            {
                counsellorRecommendList.Clear();
                Person recruter = null;
                int lastPro = 0;
                for (int i = 0; i < TargetCity.freePersons.Count; i++)
                {
                    Person person = TargetCity.freePersons[i];
                    int probability = GameFormula.Instance.RecruitPersonProbability(person, target[0], 0);
                    if (probability >= 30 && probability > lastPro)
                    {
                        lastPro = probability;
                        recruter = person;
                        //counsellorRecommendList.Add(person);
                    }
                }

                if (recruter != null)
                {
                    counsellorRecommendList.Add(recruter);
                    personList.Add(counsellorRecommendList[0]);
                }
            }
        }

        public override void DoJob()
        {
            if (personList.Count <= 0 || target.Count <= 0)
                return;

            if (!TargetCity.JobRecruitPerson(personList[0], target[0]))
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧", () =>
                {
                    // 暂时直接招募
                    Done();

                }, personList[0]);
            }
            else
            {
                Done();
            }
        }

    }
}
