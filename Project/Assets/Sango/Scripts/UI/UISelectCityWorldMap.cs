using Sango.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{

    public class UISelectCityWorldMap : MonoBehaviour
    {
        public RectTransform mapBounds;
        public UIMapCitySelectItem cityObject;
        List<UIMapCitySelectItem> cityToggleList = new List<UIMapCitySelectItem>();
        Scenario scenario;
        CreatePool<UIMapCitySelectItem> createPool;
        public List<City> selecte_list = new List<City>();
        public Action<List<City>> OnSelectCity;
        public int maxSelectCount = 0;

        /// <summary>允许被选择的城市子集；为 null 时表示显示全部城市。</summary>
        private List<City> selectableCities = null;

        /// <summary>当前显示的剧本</summary>
        public Scenario Scenario => scenario;

        /// <summary>
        /// 设置允许被选择的城市子集。
        /// </summary>
        /// <param name="cities">可选城市列表，传 null 表示不限制</param>
        public void SetSelectableCities(List<City> cities)
        {
            selectableCities = cities;
        }

        /// <summary>
        /// 判断某座城市是否在当前可选范围内。
        /// </summary>
        private bool IsCitySelectable(City city)
        {
            if (city == null) return false;
            if (selectableCities == null) return true;
            return selectableCities.Contains(city);
        }

        /// <summary>
        /// 设置要显示的剧本,由外部(如 UIScenarioAddonMenu)在打开时调用。
        /// 数据仅来自 Scenario,不涉及 Scenario.Cur。
        /// </summary>
        public void SetScenario(Scenario value)
        {
            scenario = value;
        }

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

            for (int kk = 0; kk < scenario.citySet.Count; kk++)
            {
                City city = scenario.citySet[kk];
                if (city == null) continue;
                if (city.BuildingType.Id > 1) continue;
                if (city.Id == 0) continue;
                UIMapCitySelectItem toggle = createPool.Create();
                toggle.city = city;
                if (city.BelongForce == 0)
                {
                    toggle.SetInavtive(true);
                    toggle.SetColor(Color.white);
                }
                else
                {
                    Force Force = city.mBelongForce;
                    if (Force == null)
                        continue;
                    Flag flag = Force.mFlag;
                    toggle.SetInavtive(true);
                    toggle.SetColor(flag != null ? flag.color : Color.white);
                }
                toggle.onSelectShortAction = null;
                toggle.ShowName(city.Name);
                cityToggleList.Add(toggle);
                RectTransform rectTransform = toggle.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);
            }
        }

        void OnSelectMapCity(UIMapCitySelectItem item, City city, bool b)
        {
            if (maxSelectCount > 0)
            {
                if (b)
                {
                    if (selecte_list.Count < maxSelectCount)
                    {
                        selecte_list.Remove(city);
                        selecte_list.Add(city);
                    }
                    else
                    {
                        City last = selecte_list[0];
                        cityToggleList.ForEach(x =>
                        {
                            if (x.city == last)
                            {
                                x.SetSelected(false);
                            }
                        });
                        item.SetSelected(true);
                        selecte_list.RemoveAt(0);
                        selecte_list.Add(city);
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
                }
                else
                {
                    selecte_list.Remove(city);
                }
            }
            OnSelectCity.Invoke(selecte_list);
        }

        public void SetSelectEmptyCity(List<City> exsist)
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
                City city = scenario.citySet[kk];
                if (city == null) continue;
                if (city.BuildingType.Id > 1) continue;
                if (city.Id == 0) continue;
                UIMapCitySelectItem toggle = createPool.Create();
                toggle.city = city;
                if (city.BelongForce == 0)
                {
                    toggle.ShowName(city.Name);
                    toggle.SetColor(Color.white);
                    toggle.SetInavtive(false);
                    toggle.onSelectAction = OnSelectMapCity;
                    toggle.SetSelected(false);
                }
                else
                {
                    Force Force = scenario.forceSet.Get(city.BelongForce);
                    if (Force == null)
                        continue;
                    Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[Force.Flag] : null;
                    toggle.SetColor(flag != null ? flag.color : Color.white);
                    bool contarins = selecte_list.Contains(city);
                    toggle.SetInavtive(!contarins);
                    toggle.SetSelected(contarins);
                    if (contarins)
                    {
                        toggle.ShowName(city.Name);
                        if (maxSelectCount != 1)
                            toggle.onSelectAction = OnSelectMapCity;
                        else
                            toggle.onSelectAction = null;
                    }
                    else
                    {
                        toggle.ShowName("");
                        toggle.onSelectAction = null;
                    }
                }
                cityToggleList.Add(toggle);
                RectTransform rectTransform = toggle.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);
            }
        }

        public void SetSelectAllCity(List<City> exsist)
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
                City city = scenario.citySet[kk];
                if (city == null) continue;
                if (city.BuildingType.Id > 1) continue;
                if (city.Id == 0) continue;
                UIMapCitySelectItem toggle = createPool.Create();
                toggle.city = city;
                toggle.ShowName(city.Name);

                // 不在可选范围内的城市仅作展示，不可被选取
                if (!IsCitySelectable(city))
                {
                    Color c = Color.white;
                    if (city.BelongForce != 0)
                    {
                        Force f = scenario.forceSet.Get(city.BelongForce);
                        if (f != null)
                        {
                            Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[f.Flag] : null;
                            c = flag != null ? flag.color : Color.white;
                        }
                    }
                    toggle.SetColor(c);
                    toggle.SetInavtive(true);
                    toggle.onSelectAction = null;
                    toggle.SetSelected(false);
                    cityToggleList.Add(toggle);
                    RectTransform rectTransform_disable = toggle.GetComponent<RectTransform>();
                    float x_disable = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                    float y_disable = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                    rectTransform_disable.anchoredPosition = new Vector2(x_disable, y_disable);
                    continue;
                }

                if (city.BelongForce == 0)
                {
                    toggle.SetColor(Color.white);
                    toggle.SetInavtive(false);
                    toggle.onSelectAction = OnSelectMapCity;
                    toggle.SetSelected(selecte_list.Contains(city));
                }
                else
                {
                    Force Force = scenario.forceSet.Get(city.BelongForce);
                    if (Force == null)
                        continue;
                    Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[Force.Flag] : null;
                    toggle.SetColor(flag != null ? flag.color : Color.white);
                    toggle.SetInavtive(false);
                    toggle.onSelectAction = OnSelectMapCity;
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
