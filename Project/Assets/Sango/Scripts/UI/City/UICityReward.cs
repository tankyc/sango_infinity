using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityReward : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem targetPersonItems;
        public UIStatusItem targetStatusItem;
        public UITextField targetPersonCount;
        public UITextField targetGold;

        public UITextField action_value;

        City TargetCity;
        CityReward currentSystem;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityReward>();
            TargetCity = currentSystem.TargetCity;
            windiwTitle.text = currentSystem.customTitleName;
            UpdateContent();
        }

        public void UpdateContent()
        {
            int count = currentSystem.personList.Count;
            Person target = count > 0 ? currentSystem.personList[0] : null;
            action_value.text = $"{count * JobType.GetJobCostAP((int)CityJobType.Reward)}/{TargetCity.mCorps.ActionPoint}";
            sureButton.interactable = target != null;
            targetPersonItems.SetPerson(target);
            targetStatusItem.SetPerson(target);
            targetGold.text = $"{(count * JobType.GetJobCost((int)CityJobType.Reward))}/{TargetCity.gold}";
            targetPersonCount.text = $"{count}人";
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
            int maxCount = Math.Min(currentSystem.targetList.Count, TargetCity.mCorps.ActionPoint / JobType.GetJobCostAP((int)CityJobType.Reward));
            maxCount = Math.Min(maxCount, TargetCity.gold / JobType.GetJobCost((int)CityJobType.Reward));

            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.targetList,
               currentSystem.personList, maxCount, OnTargetPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);
        }

        public virtual void OnTargetPersonChange(List<Person> personList)
        {
            currentSystem.personList = (personList);
            UpdateContent();
        }

    }
}
