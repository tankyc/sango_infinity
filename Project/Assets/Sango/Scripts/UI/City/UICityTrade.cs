using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityTrade : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem targetPersonItems;
        public UIStatusItem targetStatusItem;
        public UITextField targetEffectValue;
        public UITextField targetGoldLabel;
        public UITextField targetFoodLabel;
        public UITextField targetPercent;
        public Slider tradeSlider;

        public UITextField action_value;

        City TargetCity;
        CityTrade currentSystem;
        float totalFood = 0;
        float currentP = 0;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityTrade>();
            TargetCity = currentSystem.TargetCity;
            windiwTitle.text = currentSystem.customTitleName;
            totalFood = TargetCity.food + currentSystem.wonderNumber * TargetCity.gold * TargetCity.hasBusiness / 100;
            currentP = TargetCity.food / totalFood;
            tradeSlider.SetValueWithoutNotify(currentP);
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.TradeFood)}/{TargetCity.mCorps.ActionPoint}";
            targetPercent.text = $"1 : {TargetCity.hasBusiness}";
            UpdatePerson();
            UpdateContent();
        }

        void UpdatePerson()
        {
            if (currentSystem.personList.Count > 0)
            {
                Person person = currentSystem.personList[0];
                targetPersonItems.SetPerson(person);
                targetStatusItem.SetPerson(person);
            }
            else
            {
                targetStatusItem.SetPerson(null);
                targetPersonItems.SetPerson(null);
            }

            targetEffectValue.text = $"{currentSystem.wonderNumber}%";
        }

        public void UpdateContent()
        {
            int targetFood = (int)(totalFood * currentP);
            int costGold = (int)((targetFood - TargetCity.food) / TargetCity.hasBusiness / currentSystem.wonderNumber * 100);
            targetGoldLabel.text = $"{TargetCity.gold}→{TargetCity.gold - costGold}";
            targetFoodLabel.text = $"{TargetCity.food}→{targetFood}";
            if (costGold > 0)
                currentSystem.targetValue = (int)costGold;
            else
                currentSystem.targetValue = -(TargetCity.food - targetFood);

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
               currentSystem.personList, 1, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);
        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = (personList);
            currentSystem.UpdateJobValue();
            totalFood = TargetCity.food + currentSystem.wonderNumber * TargetCity.gold * TargetCity.hasBusiness / 100;
            UpdatePerson();
            UpdateContent();
        }

        public void OnSelectFood()
        {

        }

        public void OnTradeSliderValueChanged(float p)
        {
            currentP = p;
            UpdateContent();
        }
    }
}
