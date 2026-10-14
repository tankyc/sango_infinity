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
        public RawImage mapBounds_img;

        /// <summary>当前显示的剧本</summary>
        public ShortScenario Scenario => scenario;
        public Slider mapScaleSlider;
        public int mapDefaultSize = 256;
        public int boundsSize = 448;
        public float mapScale = 1;
        /// <summary>
        /// 设置要显示的剧本,由外部(如 UIScenarioAddonMenu)在打开时调用。
        /// 数据仅来自 ShortScenario,不涉及 Scenario.Cur。
        /// </summary>
        public void SetScenario(ShortScenario value)
        {
            ShortScenario last = scenario;
            scenario = value;
            scenarioName.text = scenario.GetDateName();
            if (last != scenario)
            {
                mapBounds_img.texture = Render.MapRender.LoadBoundTex(scenario.Map.WorkContent);
                mapScaleSlider.SetValueWithoutNotify(0);
                UpdateMap(scenario, 0);
            }
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
            cityName.text = "";
            belongForceName.text = "";
            personCount.text = "";
            wildCount.text = "";
            gold.text = "";
            food.text = "";
            troops.text = "";
            duration.text = "";
            leader.text = "";
            goldGain.text = "";
            foodGain.text = "";
            intorCount.text = "";
            tese.text = "";
        }

        public void Init()
        {

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
                ShortCity city = scenario.citySet[kk];
                if (city == null) continue;
                if (city.BuildingType > 1) continue;
                if (city.Id == 0) continue;
                UIMapCitySelectItem toggle = createPool.Create();
                toggle.shortCity = city;
                if (city.BelongForceId == 0)
                {
                    toggle.SetInavtive(true);
                    toggle.SetColor(Color.white);
                }
                else
                {
                    ShortForce shortForce = scenario.forceSet.Get(city.BelongForceId);
                    if (shortForce == null)
                        continue;
                    Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[shortForce.Flag] : null;
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
        public void OnMapScale(float scale)
        {
            UpdateMap(scenario, scale);
        }
        public void UpdateMap(ShortScenario scenario, float scale)
        {
            if (scenario == null)
                return;

            int mapW = scenario.Map.Width;
            int mapH = scenario.Map.Height;

            //int minSize = Math.Min(mapW, mapH);
            int maxSize = Math.Max(mapW, mapH);
            int boundsW = boundsSize * mapW / maxSize;
            int boundsH = boundsSize * mapH / maxSize;

            float targetW = Mathf.LerpUnclamped(boundsW, boundsW * 10, scale);
            float targetH = Mathf.LerpUnclamped(boundsH, boundsH * 10, scale);

            // 【缩放保位】地图框(mapBounds)的轴心与锚点都在中心, 其 anchoredPosition 即"内容中心"相对
            // "视口(map 地图框)中心"的偏移量。缩放时若只改尺寸而保留偏移量, 视口中心对应的地图点会跑掉,
            // 视觉上地图就会跳动。这里先记录缩放前的尺寸与偏移, 反推出视口中心当前对应的那个地图点,
            // 缩放后再把该地图点重新摆回视口中心, 从而保证缩放前后画面位置在视觉上保持不变
            Vector2 oldSize = mapBounds.sizeDelta;
            Vector2 oldPos = mapBounds.anchoredPosition;

            mapBounds.sizeDelta = new Vector2(targetW, targetH);

            if (oldSize.x > 0.001f && oldSize.y > 0.001f)
            {
                // 视口中心对应的地图点(归一化坐标, 取值 0~1, x 自左向右, y 自下向上)
                float centerU = 0.5f - oldPos.x / oldSize.x;
                float centerV = 0.5f - oldPos.y / oldSize.y;
                // 缩放后让同一个地图点依旧落在视口中心
                mapBounds.anchoredPosition = new Vector2((0.5f - centerU) * targetW, (0.5f - centerV) * targetH);
            }

            for (int kk = 0; kk < scenario.citySet.Count; kk++)
            {
               
                UIMapCitySelectItem toggle = createPool.Get(kk);
                if (toggle == null) continue;
                ShortCity city = toggle.shortCity;
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
                        ShortCity last = selecte_list[0];
                        cityToggleList.ForEach(x =>
                        {
                            if (x.shortCity == last)
                            {
                                x.SetSelected(false);
                            }
                        });
                        item.SetSelected(true);
                        selecte_list.RemoveAt(0);
                        selecte_list.Add(city);
                        OnSelectCity.Invoke(selecte_list);
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
                    OnSelectCity.Invoke(selecte_list);
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
                if (city.BelongForceId == 0)
                {
                    toggle.ShowName(city.Name);
                    toggle.SetColor(Color.white);
                    toggle.SetInavtive(false);
                    toggle.onSelectShortAction = OnSelectMapCity;
                    toggle.SetSelected(false);
                }
                else
                {
                    ShortForce shortForce = scenario.forceSet.Get(city.BelongForceId);
                    if (shortForce == null)
                        continue;
                    Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[shortForce.Flag] : null;
                    toggle.SetColor(flag != null ? flag.color : Color.white);
                    bool contarins = selecte_list.Contains(city);
                    toggle.SetInavtive(!contarins);
                    toggle.SetSelected(contarins);
                    if (contarins)
                    {
                        toggle.ShowName(city.Name);
                        if (maxSelectCount != 1)
                            toggle.onSelectShortAction = OnSelectMapCity;
                        else
                            toggle.onSelectShortAction = null;
                    }
                    else
                    {
                        toggle.ShowName("");
                        toggle.onSelectShortAction = null;
                    }
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
                if (city.BelongForceId == 0)
                {
                    toggle.SetColor(Color.white);
                    toggle.SetInavtive(false);
                    toggle.onSelectShortAction = OnSelectMapCity;
                    toggle.SetSelected(selecte_list.Contains(city));
                }
                else
                {
                    ShortForce shortForce = scenario.forceSet.Get(city.BelongForceId);
                    if (shortForce == null)
                        continue;
                    Flag flag = scenario.CommonData != null && scenario.CommonData.Flags != null ? scenario.CommonData.Flags[shortForce.Flag] : null;
                    toggle.SetColor(flag != null ? flag.color : Color.white);
                    toggle.SetInavtive(false);
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
