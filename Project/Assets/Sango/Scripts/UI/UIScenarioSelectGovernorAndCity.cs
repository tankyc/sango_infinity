using Sango.Core;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Sango.UI
{

    public class UIScenarioSelectGovernorAndCity : UGUIWindow
    {
        public UIEditWorldMap uIEditWorldMap;
        public UIObjectDisplayPlane uiGovernorSelector;
        public List<SangoObject> sangoObjects = new List<SangoObject>();
        public Button sureButton;
        PersonLib governor;
        ShortScenario scenario;
        ShortForce force;
        ShortCity city;
        Action<PersonLib, ShortCity> OnCreateForce;
        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            sureButton.interactable = false;
            scenario = (ShortScenario)objects[0];
            force = (ShortForce)objects[1];
            uIEditWorldMap = (UIEditWorldMap)objects[2];
            OnCreateForce = (Action<PersonLib, ShortCity>)objects[3];
            sangoObjects.Clear();
            uIEditWorldMap.SetScenario(scenario);
            uIEditWorldMap.RefreshCity();

            scenario.personSet.ForEach(x =>
            {
                if (x.PersonLib != null)
                {
                    if (x.BelongCity == 0)
                        sangoObjects.Add(x.PersonLib);
                }
            });

            uiGovernorSelector.Init(sangoObjects, PersonLibSortFunction.DefaultSortList);
            uiGovernorSelector.OnSelectCall = OnSelectGovernor;
        }

        public void OnSelectGovernor(int index)
        {
            governor = sangoObjects[index] as PersonLib;
            if (governor != null)
            {
                sureButton.interactable = false;
                uIEditWorldMap.SetSelectEmptyCity(null);
                uIEditWorldMap.OnSelectCity = OnSelectCity;
                uIEditWorldMap.maxSelectCount = 1;
            }
        }

        public void OnSelectCity(List<ShortCity> shortCities)
        {
            if (city != null)
            {
                city.BelongForce = 0;
            }

            city = shortCities[0];
            if (city != null)
            {
                city.BelongForce = force.Id;
                uIEditWorldMap.SetSelectEmptyCity(new List<ShortCity> { city });
                sureButton.interactable = true;
            }
        }

        public void OnSure()
        {
            Close();
            // 清理之前的
            if (force.Governor > 0)
            {
                ShortPerson shortPerson = scenario.personSet[force.Governor];
                ShortCity shortCity = scenario.citySet[shortPerson.BelongCity];
                shortCity.BelongForce = 0;

                shortPerson.BelongForce = 0;
                shortPerson.BelongCity = 0;
            }

            force.Governor = governor.targetShortPersonId;
            force.CapitalCity = city.Id;
            ShortPerson governorPerson = scenario.personSet[force.Governor];
            governorPerson.BelongCity = city.Id;
            governorPerson.BelongForce = force.Id;
            governorPerson.state = (int)PersonStateType.Governor;

            OnCreateForce?.Invoke(governor, city);
        }

    }
}
