using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityRecruitTroops : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem[] personItems;
        public UITextField targetValue;
        public UITextField targetGold;

        public UITextField action_value;

        City TargetCity;
        CityRecruitTroops currentSystem;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityRecruitTroops>();
            windiwTitle.text = currentSystem.customTitleName;
            TargetCity = currentSystem.TargetCity;
            UpdateContent();
        }

        public void UpdateContent()
        {
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.RecruitTroops)}/{TargetCity.mBelongCorps.ActionPoint}";
            sureButton.interactable = currentSystem.personList.Count > 0;
            for (int i = 0; i < 3; i++)
            {
                if (i < currentSystem.personList.Count)
                    personItems[i].SetPerson(currentSystem.personList[i]);
                else
                    personItems[i].SetPerson(null);
            }

            int destValue = TargetCity.troops + currentSystem.wonderNumber;
            if (destValue > TargetCity.TroopsLimit)
                destValue = TargetCity.TroopsLimit;

            targetValue.text = $"{TargetCity.troops}→{destValue}";
            targetGold.text = $"{TargetCity.GetJobCost(CityJobType.RecruitTroops)}/{TargetCity.gold}";
        }

        public void OnSure()
        {
            currentSystem.DoJob();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.TargetCity.freePersons,
               currentSystem.personList, 3, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);
        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = (personList);
            currentSystem.UpdateJobValue();
            UpdateContent();
        }

    }
}
