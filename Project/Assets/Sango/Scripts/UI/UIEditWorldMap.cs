using Sango.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{

    public class UIEditWorldMap : MonoBehaviour
    {
        public RectTransform mapBounds;
        public UIMapCitySelectItem cityObject;
        List<UIMapCitySelectItem> cityToggleList = new List<UIMapCitySelectItem>();
        ShortScenario scenario;
        CreatePool<UIMapCitySelectItem> createPool;
        public List<ShortCity> selecte_list = new List<ShortCity>();
        public Action<List<ShortCity>> OnSelectCity;
        public int maxSelectCount = 0;

        /// <summary>当前显示的剧本</summary>
        public ShortScenario Scenario => scenario;

        /// <summary>
        /// 设置要显示的剧本,由外部(如 UIScenarioAddonMenu)在打开时调用。
        /// 数据仅来自 ShortScenario,不涉及 Scenario.Cur。
        /// </summary>
        public void SetScenario(ShortScenario value)
        {
            scenario = value;
        }

        public Text scenarioName;
        public Text cityName;
        public Text belongForceName;
        public Text personCount;
        public Text wildCount;
        public Text gold;
        public Text food;
        public Text troops;
        public Text duration;
        public Text leader;
        public Text goldGain;
        public Text foodGain;
        public Text intorCount;
        public Text tese;

        private void Awake()
        {
            createPool = new CreatePool<UIMapCitySelectItem>(cityObject);
        }

        public void RefreshCity()
        {
            if (scenario == null)
                return;
            selecte_list.Clear();
            cityToggleList.Clear();
            createPool.Reset();

            for(int kk = 0; kk < scenario.citySet.Count; kk++)
            {
                ShortCity city = scenario.citySet[kk];
                if (city == null) continue;
                if (city.BuildingType > 1) continue;
                if (city.Id == 0) continue;
                UIMapCitySelectItem toggle = createPool.Create();
                toggle.shortCity = city;
                if (city.BelongForce == 0)
                {
                    toggle.SetInavtive(true);
                    toggle.SetColor(Color.white);
                }
                else
                {
                    ShortForce shortForce = scenario.forceSet.Get(city.BelongForce);
                    if (shortForce == null)
                        continue;
                    Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[shortForce.Flag] : null;
                    toggle.SetInavtive(true);
                    toggle.SetColor(flag != null ? flag.color : Color.white);
                }
                toggle.ShowName(city.Name);
                cityToggleList.Add(toggle);
                RectTransform rectTransform = toggle.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);
            }
        }

        void OnSelectMapCity(UIMapCitySelectItem item, ShortCity city, bool b)
        {
            if (maxSelectCount > 0)
            {


                if (b)
                {
                    if (selecte_list.Count < maxSelectCount)
                    {
                        selecte_list.Remove(city);
                        selecte_list.Add(city);
                        OnSelectCity.Invoke(selecte_list);
                    }
                    else
                    {
                        item.SetSelected(false);
                    }
                }
                else
                {
                    selecte_list.Remove(city);
                }
            }
            else
            {
                if (b)
                {
                    selecte_list.Remove(city);
                    selecte_list.Add(city);
                    OnSelectCity.Invoke(selecte_list);
                }
                else
                {
                    selecte_list.Remove(city);
                }
            }
        }

        public void SetSelectEmptyCity(List<ShortCity> exsist)
        {
            if (scenario == null)
                return;
            selecte_list.Clear();
            if (exsist != null)
                selecte_list.AddRange(exsist);
            cityToggleList.Clear();
            createPool.Reset();
            for (int kk = 0; kk < scenario.citySet.Count; kk++)
            {
                ShortCity city = scenario.citySet[kk];
                if (city == null) continue;
                if (city.BuildingType > 1) continue;
                if (city.Id == 0) continue;
                UIMapCitySelectItem toggle = createPool.Create();
                toggle.shortCity = city;
                toggle.ShowName("");
                if (city.BelongForce == 0)
                {
                    toggle.ShowName(city.Name);
                    toggle.SetColor(Color.white);
                    toggle.SetInavtive(false);
                    toggle.onSelectShortAction = OnSelectMapCity;
                    toggle.SetSelected(selecte_list.Contains(city));
                }
                else
                {
                    ShortForce shortForce = scenario.forceSet.Get(city.BelongForce);
                    if (shortForce == null)
                        continue;
                    Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[shortForce.Flag] : null;
                    toggle.SetColor(flag != null ? flag.color : Color.white);
                    toggle.SetInavtive(true);
                    toggle.SetSelected(false);
                }
                cityToggleList.Add(toggle);
                RectTransform rectTransform = toggle.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);
            }
        }

        public void SetSelectAllCity(List<ShortCity> exsist)
        {
            if (scenario == null)
                return;
            selecte_list.Clear();
            if (exsist != null)
                selecte_list.AddRange(exsist);
            cityToggleList.Clear();
            createPool.Reset();
            for (int kk = 0; kk < scenario.citySet.Count; kk++)
            {
                ShortCity city = scenario.citySet[kk];
                if (city == null) continue;
                if (city.BuildingType > 1) continue;
                if (city.Id == 0) continue;
                UIMapCitySelectItem toggle = createPool.Create();
                toggle.shortCity = city;
                toggle.ShowName(city.Name);
                if (city.BelongForce == 0)
                {
                    toggle.SetColor(Color.white);
                    toggle.SetInavtive(false);
                    toggle.onSelectShortAction = OnSelectMapCity;
                    toggle.SetSelected(selecte_list.Contains(city));
                }
                else
                {
                    ShortForce shortForce = scenario.forceSet.Get(city.BelongForce);
                    if (shortForce == null)
                        continue;
                    Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[shortForce.Flag] : null;
                    toggle.SetColor(flag != null ? flag.color : Color.white);
                    toggle.SetInavtive(true);
                    toggle.onSelectShortAction = OnSelectMapCity;
                    toggle.SetSelected(selecte_list.Contains(city));
                }
                cityToggleList.Add(toggle);
                RectTransform rectTransform = toggle.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);
            }
        }
    }
}
