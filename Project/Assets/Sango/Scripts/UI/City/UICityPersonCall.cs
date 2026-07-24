using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityPersonCall : UGUIWindow
    {
        public Text windiwTitle;

        public UIStatusItem statusItem;
        public UIPersonItem personItem;

        public UITextField personCountLabel;

        public UITextField action_value;
        public Button sureBtn;

        CityCallPerson currentSystem;
        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityCallPerson>();
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
            List<Person> list = new List<Person>();
            currentSystem.TargetCity.BelongForce.ForEachCityBase(city =>
            {
                if (city != currentSystem.TargetCity)
                {
                    list.AddRange(city.freePersons);
                }
            });
            int max = currentSystem.TargetCity.BelongCorps.ActionPoint / JobType.GetJobCostAP((int)CityJobType.CallPerson);
            GameSystem.GetSystem<PersonSelectSystem>().Start(list,
             currentSystem.personList, max, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);

        }
        public void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = personList;
            ResetContent();
        }
    }
}
