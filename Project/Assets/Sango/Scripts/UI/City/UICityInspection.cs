using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityInspection : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem [] personItems;
        public UITextField targetValue;
        public UITextField targetGold;

        public UITextField action_value;

        City TargetCity;
        CityInspection currentSystem;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityInspection>();
            windiwTitle.text = currentSystem.customTitleName;
            TargetCity = currentSystem.TargetCity;
            UpdateContent();
        }

        public void UpdateContent()
        {
            int cost = HantuPopulation.Enabled
                ? TargetCity.PreviewInspectionCost(currentSystem.personList.ToArray())
                : TargetCity.GetJobCost(CityJobType.Inspection);
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.Inspection)}/{TargetCity.BelongCorps.ActionPoint}";
            sureButton.interactable = currentSystem.personList.Count > 0
                && (!HantuPopulation.Enabled || (TargetCity.gold >= cost && currentSystem.wonderNumber > 0));
            for(int i = 0; i < 3; i++)
            {
                if(i < currentSystem.personList.Count)
                    personItems[i].SetPerson(currentSystem.personList[i]);
                else
                    personItems[i].SetPerson(null);
            }

            int destValue = TargetCity.security + currentSystem.wonderNumber;
            int securityLimit = TargetCity.InspectionSecurityLimit;
            destValue = Math.Min(destValue, HantuPopulation.Enabled ? Math.Max(TargetCity.security, securityLimit) : securityLimit);

            targetValue.text = $"{TargetCity.security}→{destValue}";
            targetGold.text = $"{cost}/{TargetCity.gold}";
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
