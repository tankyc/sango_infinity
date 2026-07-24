using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityPersonTransform : UGUIWindow
    {
        public Text windiwTitle;

        public UIStatusItem statusItem;
        public UIPersonItem personItem;

        public UITextField personCountLabel;
        public UITextField targetLabel;
        public UITextField daysLabel;

        public UITextField action_value;
        public Button sureBtn;

        CityTransformPerson currentSystem;
        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityTransformPerson>();
            windiwTitle.text = currentSystem.customTitleName;
            ResetContent();
        }

        public void ResetContent()
        {
            if (currentSystem.personList.Count > 0)
            {
                Person target = currentSystem.personList[0];
                personItem.SetPerson(target);
                statusItem.SetPerson(target);
            }
            else
            {
                personItem.SetPerson(null);
                statusItem.SetPerson(null);
            }

            personCountLabel.text = $"{currentSystem.personList.Count}人";

            if (currentSystem.transformTo.Count > 0)
            {
                City target = currentSystem.transformTo[0];
                targetLabel.text = target.Name;
                daysLabel.text = $"{target.Distance(currentSystem.TargetCity) * 10}日";
            }
            else
            {
                targetLabel.text = "";
                daysLabel.text = "";
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

        public void OnSelectPerson()
        {
            int max = currentSystem.TargetCity.BelongCorps.ActionPoint / JobType.GetJobCostAP((int)CityJobType.TransformPerson); 
            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.TargetCity.freePersons,
             currentSystem.personList, max, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);

        }
        public void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = personList;
            ResetContent();
        }

        public void OnSelectCity()
        {
            List<City> cities = new List<City>();
            City targetCity = currentSystem.TargetCity;
            targetCity.BelongForce.ForEachCityBase(city =>
            {
                if (city != targetCity)
                {
                    cities.Add(city);
                }
            });

            GameSystem.GetSystem<CitySelectSystem>().Start(cities,
              currentSystem.transformTo, 1, OnCityChange, currentSystem.citySortTitleList, "目的城池选择");

        }
        public void OnCityChange(List<City> cities)
        {
            currentSystem.transformTo = cities;
            ResetContent();
        }
    }
}
