using System.Collections.Generic;
using Sango.Core;
using UnityEngine.UI;

namespace Sango.UI
{

    public class UIScenarioAssignPersonToCitiy : UGUIWindow
    {
        public UIEditWorldMap uIEditWorldMap;
        public UIObjectDisplayPlane uiForceView;
        public List<SangoObject> sangoObjects = new List<SangoObject>();
        public Button sureButton;
        List<int> LastSel = new List<int>();
        public ShortScenario scenario;
        public ShortScenario src_scenario;

        public List<ObjectSortTitle> SortList;


        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            sureButton.interactable = false;
            uIEditWorldMap = (UIEditWorldMap)objects[0];
            sangoObjects.Clear();
            LastSel.Clear();

            src_scenario = (ShortScenario)objects[1];
            scenario = src_scenario.Copy();

            uIEditWorldMap.SetScenario(scenario);
            SortList = new List<ObjectSortTitle>
            {
                    PersonLibSortFunction.SortByName,
                    PersonLibSortFunction.SortByBelongForce(scenario),
                    PersonLibSortFunction.SortByBelongCity(scenario),
                    PersonLibSortFunction.SortByYearBorn,
                    PersonLibSortFunction.SortByYearDead,
                    PersonLibSortFunction.SortBySex ,
                    PersonLibSortFunction.SortByCommand,
                    PersonLibSortFunction.SortByStrength,
                    PersonLibSortFunction.SortByIntelligence,
                    PersonLibSortFunction.SortByPolitics,
                    PersonLibSortFunction.SortByGlamour,
            };

            scenario.personSet.ForEach(person =>
            {
                // 指派了,但是不是主公,都可以重新指派
                if (person.PersonLib != null && person.state != (int)PersonStateType.Governor)
                {
                    sangoObjects.Add(person.PersonLib);
                }
            });
            uiForceView.Init(sangoObjects, SortList);
            uiForceView.OnMultiSelectCall = OnMultiSelectCall;


        }

        public void OnMultiSelectCall(List<int> index)
        {
            LastSel.Clear();
            LastSel.AddRange(index);
            if (LastSel.Count > 0)
            {
                sureButton.interactable = true;
                uIEditWorldMap.SetSelectAllCity(null);
                uIEditWorldMap.OnSelectCity = OnSelectCity;
                uIEditWorldMap.maxSelectCount = 1;
            }
            else
            {
                sureButton.interactable = false;
                uIEditWorldMap.RefreshCity();
                uIEditWorldMap.OnSelectCity = null;
            }
        }

        public void OnSelectCity(List<ShortCity> shortCities)
        {
            ShortCity city = shortCities[0];
            if (city != null)
            {
                if (city.BelongForce == 0)
                {
                    for (int i = 0; i < LastSel.Count; i++)
                    {
                        PersonLib sangoObject = sangoObjects[LastSel[i]] as PersonLib;
                        if (sangoObject != null)
                        {
                            ShortPerson person = scenario.personSet[sangoObject.targetShortPersonId];
                            person.BelongForce = city.BelongForce;
                            person.BelongCity = city.Id;
                            person.state = (int)PersonStateType.Unemployed;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < LastSel.Count; i++)
                    {
                        PersonLib sangoObject = sangoObjects[LastSel[i]] as PersonLib;
                        if (sangoObject != null)
                        {
                            ShortPerson person = scenario.personSet[sangoObject.targetShortPersonId];
                            person.BelongForce = city.BelongForce;
                            person.BelongCity = city.Id;
                            person.state = (int)PersonStateType.Normal;
                        }
                    }
                }
                sureButton.interactable = true;
                LastSel.Clear();
                uiForceView.Init(sangoObjects, SortList);
            }
            uIEditWorldMap.RefreshCity();
        }

        public void OnSure()
        {
            src_scenario.personSet = scenario.personSet;
            src_scenario.citySet = scenario.citySet;
            src_scenario.forceSet = scenario.forceSet;
            Close();
        }
    }
}
