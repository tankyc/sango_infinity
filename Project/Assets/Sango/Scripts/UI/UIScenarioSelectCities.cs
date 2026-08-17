using System.Collections.Generic;
using Sango.Core;
using UnityEngine.UI;

namespace Sango.UI
{

    public class UIScenarioSelectCities : UGUIWindow
    {
        public UIEditWorldMap uIEditWorldMap;
        public UIObjectDisplayPlane uiForceView;
        public List<SangoObject> sangoObjects = new List<SangoObject>();
        public Button sureButton;
        List<int> LastSel = new List<int>();
        public ShortScenario scenario;
        public ShortScenario src_scenario;

        public List<ObjectSortTitle> SortList;
        public ShortForce targetForce;

        List<ShortCity> shortCities1 = new List<ShortCity>();
        List<ShortPerson> allPersons = new List<ShortPerson>();
        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            sureButton.interactable = false;
            uIEditWorldMap = (UIEditWorldMap)objects[0];
            sangoObjects.Clear();
            LastSel.Clear();

            src_scenario = (ShortScenario)objects[1];
            scenario = src_scenario.Copy();

            targetForce = (ShortForce)objects[2];
            uIEditWorldMap.SetScenario(scenario);
            SortList = new List<ObjectSortTitle>
            {
                ShortForceSortFunction.SortByName(scenario),
                ShortForceSortFunction.SortByPersonCount(scenario),
                ShortForceSortFunction.SortByCityCount(scenario),
                ShortForceSortFunction.SortByTotalGold(scenario),
                ShortForceSortFunction.SortByTotalFood(scenario),
                ShortForceSortFunction.SortByTotalTroops(scenario),
            };

            scenario.forceSet.ForEach(f =>
            {
                ShortPerson person = scenario.personSet.Get(f.Governor);
                if (person != null && person.BelongCity > 0)
                {
                    sangoObjects.Add(f);
                }
            });

            scenario.citySet.ForEach(c =>
            {
                if (c.BelongForce == targetForce.Id)
                {
                    if (c.Id != targetForce.CapitalCity)
                        shortCities1.Add(c);
                }
            });
            scenario.personSet.ForEach(person =>
            {
                if (person.BelongForce == targetForce.Id)
                    allPersons.Add(person);
            });

            uiForceView.Init(sangoObjects, SortList);
            uiForceView.OnMultiSelectCall = null;
            uiForceView.OnSelectCall = null;

            uIEditWorldMap.SetSelectEmptyCity(shortCities1);

            uIEditWorldMap.OnSelectCity = OnSelectCity;
            uIEditWorldMap.maxSelectCount = 0;
        }

        public void OnSelectCity(List<ShortCity> shortCities)
        {
            shortCities1.Clear();
            shortCities1.AddRange(shortCities);
            scenario.citySet.ForEach(c =>
            {
                if (c.Id == targetForce.CapitalCity)
                    return;

                bool contains = shortCities.Contains(c);
                if (contains)
                {
                    c.BelongForce = targetForce.Id;
                }
                else
                {
                    if (c.BelongForce == targetForce.Id)
                    {
                        c.BelongForce = 0;
                    }
                }
            });

            sureButton.interactable = true;
            uIEditWorldMap.SetSelectEmptyCity(shortCities1);
            uiForceView.Init(sangoObjects, SortList);
            uIEditWorldMap.OnSelectCity = OnSelectCity;
            uIEditWorldMap.maxSelectCount = 0;
        }

        public void OnSure()
        {
            List<int> allC = new List<int>();
            scenario.citySet.ForEach(c =>
            {
                if (c.BelongForce == targetForce.Id)
                {
                    allC.Add(c.Id);
                }
            });

            // 失去都市的武将回归本城
            scenario.personSet.ForEach(c =>
            {
                if (c.BelongForce == targetForce.Id)
                {
                    if (!allC.Contains(c.BelongCity))
                    {
                        c.BelongCity = targetForce.CapitalCity;
                    }
                }
            });

            src_scenario.personSet = scenario.personSet;
            src_scenario.citySet = scenario.citySet;
            src_scenario.forceSet = scenario.forceSet;
            Close();
        }
    }
}
