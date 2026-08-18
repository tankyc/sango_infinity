using Sango.Render;
using Sango.UI;

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

        public void Start(Person recruitor, Person target, int recruitType, int tryLimit, System.Action<PersonRecruit> doneAction)
        {
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
            result = 0;
            this.tryLimit = tryLimit;
            this.recruitor = atker.mBelongForce.mGovernor;
            this.fallCity = fallCity;
            this.atker = atker;
            this.target = target;
            this.recruitType = recruitType;
            this.doneAction = doneAction;
            Push();
        }

        public void Start(Troop atker, Person target, int recruitType, int tryLimit, System.Action<PersonRecruit> doneAction)
        {
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
            if (tryLimit > 0)
            {
                if (fallCity != null)
                    result = recruitor.JobRecruitPerson(target, fallCity, recruitType) == true ? 1 : 0;
                else
                    result = recruitor.JobRecruitPerson(target, recruitType) == true ? 1 : 0;
                if (result == 1)
                {
                    GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                    {
                        GameDialog.Close();
                        Back();
                        doneAction?.Invoke(this);
                    });
                    dialog1.SetPerson(target);
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
            if (tryLimit > 0)
            {
                if (fallCity != null)
                    result = recruitor.JobRecruitPerson(target, fallCity, recruitType) == true ? 1 : 0;
                else
                    result = recruitor.JobRecruitPerson(target, recruitType) == true ? 1 : 0;
                if (result == 0)
                {
                    GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, talk[GameRandom.Range(0, talk.Length)], () =>
                    {
                        GameDialog.Close();
                    });
                    dialog1.SetPerson(target);
                }
                else if (result == 1)
                {
                    GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                    {
                        GameDialog.Close();
                        Back();
                        doneAction?.Invoke(this);
                    });
                    dialog1.SetPerson(target);
                }
                tryLimit--;
            }
        }


        // 释放
        public void ReleaseTarget()
        {
            result = 2;
            Force releaseForce = atker?.mBelongForce ?? fallCity?.mBelongForce;
            target.SetMission(MissionType.PersonReturn, target.mBelongCity);
            GameEvent.OnPersonRelease?.Invoke(target, releaseForce);
            Back();
            doneAction?.Invoke(this);
        }

        // 斩首
        public void KillTarget()
        {
            result = 3;
            Force executeForce = atker?.mBelongForce ?? fallCity?.mBelongForce;
            target.Dead();
            GameEvent.OnPersonExecute?.Invoke(target, executeForce);
            Back();
            doneAction?.Invoke(this);
        }

        // 收押
        public void DetainTarget()
        {
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
    }
}
