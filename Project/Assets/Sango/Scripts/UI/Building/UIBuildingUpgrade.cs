using TKNewtonsoft.Json.Utilities.LinqBridge;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIBuildingUpgrade : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem[] personItems;

        public UITextField value_turn;
        public UITextField value_gold;

        public UITextField destBuldingName;
        public UITextField destGoldProduction;
        public UITextField destFoodProduction;


        public UITextField action_value;

        BuildingActionUpgrade currentSystem;


        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<BuildingActionUpgrade>();
            windiwTitle.text = currentSystem.customTitleName;

            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < currentSystem.personList.Count)
                {
                    personItems[i].SetPerson(currentSystem.personList[i]);
                }
                else
                {
                    personItems[i].SetPerson(null);
                }
            }

            value_turn.text = $"{currentSystem.wonderBuildCounter * 10}日";
            value_gold.text = $"{currentSystem.TargetUpgradeType.cost}/{currentSystem.TargetBuilding.mBelongCity.gold}";

            destBuldingName.text = currentSystem.TargetUpgradeType.Name;
            if (currentSystem.TargetUpgradeType.goldGain == 0)
                destGoldProduction.text = "---";
            else
                destGoldProduction.text = $"{currentSystem.TargetBuilding.BuildingType.goldGain} → {currentSystem.TargetUpgradeType.goldGain}";
            if (currentSystem.TargetUpgradeType.foodGain == 0)
                destFoodProduction.text = "---";
            else
                destFoodProduction.text = $"{currentSystem.TargetBuilding.BuildingType.foodGain} → {currentSystem.TargetUpgradeType.foodGain}";
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.UpgradeBuilding)}/{currentSystem.TargetBuilding.mBelongCorps.ActionPoint}";
        }


        public void OnSure()
        {
            currentSystem.DoJob();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }
        public virtual void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.TargetBuilding.mBelongCity.freePersons,
               currentSystem.personList, 3, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);

        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = personList;
            currentSystem.UpdateJobValue();
            OnOpen();
        }

    }
}
