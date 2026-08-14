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
        ShortCity city;
        Action<PersonLib, ShortCity> OnCreateForce;
        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            sureButton.interactable = false;
            uIEditWorldMap = (UIEditWorldMap)objects[0];
            OnCreateForce = (Action<PersonLib, ShortCity>)objects[1];
            sangoObjects.Clear();
            sangoObjects.AddRange(UIScenarioAddonMenu.AddData.UnassignedPersonLibs);
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
            city = shortCities[0];
            if (city != null)
            {
                sureButton.interactable = true;
            }
        }

        public void OnSure()
        {
            Close();
            OnCreateForce?.Invoke(governor, city);
        }

    }
}
