using Sango.Core.Player;
using Sango.Render;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    [GameSystem]
    public class PersonRecruit : GameSystem
    {
        public Person target;
        public Person recruitor;
        public City fallCity;
        public Troop atker;
        public int recruitType;
        public int tryLimit;
        public int result = 0;
        public System.Action<PersonRecruit> doneAction;
        public List<Person> all_persons;
        List<Person> recruited_list = new List<Person>();

        /// <summary>目标是否为君主(主公，PersonStateType.Governor)：君主不可被登用/收押，只能释放或斩首</summary>
        public bool IsTargetGovernor => target != null && target.IsGovernor;

        public void Start(Person recruitor, Person target, int recruitType, int tryLimit, System.Action<PersonRecruit> doneAction)
        {
            recruited_list.Clear();
            all_persons = null;
            result = 0;
            this.tryLimit = tryLimit;
            this.recruitor = recruitor;
            this.fallCity = null;
            this.atker = null;
            this.target = target;
            this.recruitType = recruitType;
            this.doneAction = doneAction;
            Push();
        }

        public void Start(City fallCity, Troop atker, Person target, int recruitType, int tryLimit, System.Action<PersonRecruit> doneAction)
        {
            recruited_list.Clear();
            all_persons = null;
            result = 0;
            this.tryLimit = tryLimit;
            this.recruitor = atker.BelongForce.mGovernor;
            this.fallCity = fallCity;
            this.atker = atker;
            this.target = target;
            this.recruitType = recruitType;
            this.doneAction = doneAction;
            Push();
        }

        public void Start(City fallCity, Troop atker, Person target, List<Person> target_list, int recruitType, int tryLimit, System.Action<PersonRecruit> doneAction)
        {
            recruited_list.Clear();
            result = 0;
            this.tryLimit = tryLimit;
            this.recruitor = atker.BelongForce.mGovernor;
            this.fallCity = fallCity;
            this.atker = atker;
            this.target = target;
            this.recruitType = recruitType;
            this.doneAction = doneAction;
            all_persons = new List<Person>(target_list);
            Push();
        }

        public void Start(Troop atker, Person target, int recruitType, int tryLimit, System.Action<PersonRecruit> doneAction)
        {
            recruited_list.Clear();
            all_persons = null;
            result = 0;
            this.tryLimit = tryLimit;
            this.recruitor = atker.Leader;
            this.fallCity = null;
            this.atker = atker;
            this.target = target;
            this.recruitType = recruitType;
            this.doneAction = doneAction;
            Push();
        }

        public override void OnEnter()
        {
            Window.Instance.Open("window_person_recruit_info");
        }

        public override void OnDestroy()
        {
            Window.Instance.Close("window_person_recruit_info");
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {

        }

        string[] talk = new string[]
        {
            "杀了我吧，我是不会加入你们的！！",
            "休想让我替你卖命！！",
            "宁死不降！！"
        };


        // 招募
        public void RecruitTarget()
        {
            // 规则：君主(主公)不可被登用，只能释放或斩首
            if (IsTargetGovernor)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, "此人乃一方之主，岂能屈居人下！只能释放或斩首。", () => { }, target);
                return;
            }

            if (tryLimit > 0)
            {
                if (fallCity != null)
                    result = recruitor.JobRecruitPerson(target, fallCity, recruitType) == true ? 1 : 0;
                else
                    result = recruitor.JobRecruitPerson(target, recruitType) == true ? 1 : 0;
                if (result == 1)
                {
                    GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                    {
                        Back();
                        doneAction?.Invoke(this);
                    }, target);
                }
                tryLimit--;
            }

            if (tryLimit <= 0 && atker == null && fallCity == null)
            {
                Back();
                doneAction?.Invoke(this);
            }
        }

        public void RecruitTarget2()
        {
            if (all_persons != null)
            {
                List<Person> list = new List<Person>();
                foreach (Person person in all_persons)
                    if (!recruited_list.Contains(person))
                        list.Add(person);
                GameSystem.GetSystem<PersonSelectSystem>().Start(list, new List<Person>(), list.Count, OnPersonRecruit, PersonSortFunction.DefaultSortList, "选择");
                return;
            }

            // 规则：君主(主公)不可被登用，只能释放或斩首
            if (IsTargetGovernor)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, "此人乃一方之主，岂能屈居人下！只能释放或斩首。", () => { }, target);
                return;
            }

            if (tryLimit > 0)
            {
                if (fallCity != null)
                    result = recruitor.JobRecruitPerson(target, fallCity, recruitType) == true ? 1 : 0;
                else
                    result = recruitor.JobRecruitPerson(target, recruitType) == true ? 1 : 0;
                if (result == 0)
                {
                    // 登用失败若已经进入舌战（或正在问"是否观战"），就不再弹"宁死不降"的拒绝台词，
                    // 否则这句失败台词会跟舌战的台词/对话框抢队列。
                    bool debateTookOver = Sango.Core.Debate.DebateChallengeFlow.IsPending
                        || Sango.Core.Debate.DebateManager.Instance.IsDebating;
                    if (!debateTookOver)
                    {
                        GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, talk[GameRandom.Range(0, talk.Length)], () =>
                        {
                        }, target);
                    }
                }
                else if (result == 1)
                {
                    GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                    {
                        Back();
                        doneAction?.Invoke(this);
                    }, target);
                }
                tryLimit--;
            }
        }


        // 释放
        public void ReleaseTarget()
        {
            if (all_persons != null)
            {
                GameSystem.GetSystem<PersonSelectSystem>().Start(all_persons, new List<Person>(), all_persons.Count, OnPersonRelease, PersonSortFunction.DefaultSortList, "选择");
                return;
            }

            result = 2;
            Force releaseForce = atker?.BelongForce ?? fallCity?.BelongForce;
            target.SetMission(MissionType.PersonReturn, target.BelongCity);
            GameEvent.OnPersonRelease?.Invoke(target, releaseForce);
            Back();
            doneAction?.Invoke(this);
        }

        // 斩首
        public void KillTarget()
        {
            if (all_persons != null)
            {
                GameSystem.GetSystem<PersonSelectSystem>().Start(all_persons, new List<Person>(), all_persons.Count, OnPersonKill, PersonSortFunction.DefaultSortList, "选择");
                return;
            }

            result = 3;
            Force executeForce = atker?.BelongForce ?? fallCity?.BelongForce;
            target.Dead();
            GameEvent.OnPersonExecute?.Invoke(target, executeForce);
            Back();
            doneAction?.Invoke(this);
        }

        // 收押
        public void DetainTarget()
        {
            if (all_persons != null)
            {
                GameSystem.GetSystem<PersonSelectSystem>().Start(all_persons, new List<Person>(), all_persons.Count, OnPersonDetain, PersonSortFunction.DefaultSortList, "选择");
                return;
            }

            // 规则：君主(主公)不可收押，只能释放或斩首
            if (IsTargetGovernor)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, "此人乃一方之主，只能释放或斩首。", () => { }, target);
                return;
            }

            result = 3;
            if (fallCity != null)
                fallCity.AddCaptive(target);
            else if (atker != null)
                atker.AddCaptive(target);
            Back();
            doneAction?.Invoke(this);
        }

        public void Cancel()
        {
            result = -1;
            Back();
            doneAction?.Invoke(this);
        }


        void OnPersonRecruit(List<Person> persons)
        {
            if (persons == null || persons.Count == 0)
                return;

            for (int i = 0; i < persons.Count; i++)
            {
                Person person = persons[i];
                recruited_list.Add(person);
                for (int j = 0; j < tryLimit; j++)
                {
                    if (fallCity != null)
                        result = recruitor.JobRecruitPerson(person, fallCity, recruitType) == true ? 1 : 0;
                    else
                        result = recruitor.JobRecruitPerson(person, recruitType) == true ? 1 : 0;

                    if (result == 1)
                    {
                        GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                        {

                        }, person);
                        all_persons.Remove(person);
                        break;
                    }
                }
            }

            if (all_persons.Count == 0)
            {
                Back();
                doneAction?.Invoke(this);
            }
            else
            {
                target = all_persons[0];
                RefreshUI();
            }
        }


        void OnPersonRelease(List<Person> persons)
        {
            if (persons == null || persons.Count == 0)
                return;

            for (int i = 0; i < persons.Count; i++)
            {
                Person person = persons[i];
                Force releaseForce = atker?.BelongForce ?? fallCity?.BelongForce;
                person.SetMission(MissionType.PersonReturn, person.BelongCity);
                GameEvent.OnPersonRelease?.Invoke(person, releaseForce);
                all_persons.Remove((Person)person);
            }

            if (all_persons.Count == 0)
            {
                Back();
                doneAction?.Invoke(this);
            }
            else
            {
                target = all_persons[0];
                RefreshUI();
            }
        }

        void OnPersonKill(List<Person> persons)
        {
            if (persons == null || persons.Count == 0)
                return;

            for (int i = 0; i < persons.Count; i++)
            {
                Person person = persons[i];
                Force executeForce = atker?.BelongForce ?? fallCity?.BelongForce;
                person.Dead();
                GameEvent.OnPersonExecute?.Invoke(person, executeForce);
                all_persons.Remove((Person)person);
            }

            if (all_persons.Count == 0)
            {
                Back();
                doneAction?.Invoke(this);
            }
            else
            {
                target = all_persons[0];
                RefreshUI();
            }
        }

        void OnPersonDetain(List<Person> persons)
        {
            if (persons == null || persons.Count == 0)
                return;

            for (int i = 0; i < persons.Count; i++)
            {
                Person person = persons[i];
                if (fallCity != null)
                    fallCity.AddCaptive(person);
                else if (atker != null)
                    atker.AddCaptive(person);
                all_persons.Remove((Person)person);
            }

            if (all_persons.Count == 0)
            {
                Back();
                doneAction?.Invoke(this);
            }
            else
            {
                target = all_persons[0];
                RefreshUI();
            }
        }

        void RefreshUI()
        {
            Window.Instance.Close("window_person_recruit_info");
            Window.Instance.Open("window_person_recruit_info");
        }
    }
}
