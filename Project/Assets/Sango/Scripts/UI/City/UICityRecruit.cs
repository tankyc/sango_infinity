using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityRecruit : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem targetPersonItems;
        public UIStatusItem targetStatusItem;
        public UITextField targetForce;
        public UITextField targetLoyalty;
        public UITextField targetDays;
        public UITextField targetCityName;
        public UIStatusItem actionStatusItem;
        public UIPersonItem actionPersonItems;

        public UITextField action_value;

        City TargetCity;
        CityRecruit currentSystem;
        public Button sureButton;


        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityRecruit>();
            TargetCity = currentSystem.TargetCity;
            windiwTitle.text = currentSystem.customTitleName;
            UpdateContent();
        }

        public void UpdateContent()
        {
            Person target = currentSystem.target.Count > 0 ? currentSystem.target[0] : null;
            Person action = currentSystem.personList.Count > 0 ? currentSystem.personList[0] : null;
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.RecruitPerson)}/{TargetCity.mCorps.ActionPoint}";
            sureButton.interactable = target != null && action != null;

            targetPersonItems.SetPerson(target);
            actionPersonItems.SetPerson(action);
            targetStatusItem.SetPerson(target);
            actionStatusItem.SetPerson(action);

            if (target != null)
            {
                targetForce.text = target.mForce != null ? target.mForce.Name : "--";
                targetLoyalty.text = target.loyalty.ToString();
                City where = target.mCity ?? target.mCurrentCity;
                targetDays.text = $"{10 * where.Distance(TargetCity)}日";
                targetCityName.text = where.Name;
            }
            else
            {
                targetForce.text = "--";
                targetLoyalty.text = "--";
                targetDays.text = "--";
                targetCityName.text = "--";
            }

        }

        public void OnSure()
        {
            currentSystem.DoJob();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }

        public void OnSelectTargetPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.targetList,
               currentSystem.target, 1, OnTargetPersonChange, currentSystem.customTargetTitleList, currentSystem.customTargetTitleName, -1);

        }

        public void OnSelectActionPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(TargetCity.freePersons,
               currentSystem.personList, 1, OnActionPersonChange, currentSystem.customActionTitleList, currentSystem.customActionTitleName);

        }

        public virtual void OnTargetPersonChange(List<Person> personList)
        {
            currentSystem.SetTarget(personList);
            
            if (currentSystem.personList.Count > 0)
            {
                string content = $"最适合担任此任务的人，\n除{currentSystem.personList[0].ColorName}之外别无其他人选。";
                if (currentSystem.personList[0] == TargetCity.mForce.mCounsellor)
                {
                    content = $"我对此任务很有信心，\n请务必交给我吧。";
                }

                GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, content, () => { GameDialog.Close(); UpdateContent(); });
                Person person = TargetCity.mForce.mCounsellor;
                dialog.SetPerson(person);
            }
            else
            {
                GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"如今并无适合担任此任务的人选。", () => { GameDialog.Close(); UpdateContent(); });
                Person person = TargetCity.mForce.mCounsellor;
                dialog.SetPerson(person);
            }
        }

        public virtual void OnActionPersonChange(List<Person> personList)
        {
            currentSystem.personList = personList;
            UpdateContent();
        }

    }
}
