using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Sango.Core;
using System;

namespace Sango.UI
{
    /// <summary>
    /// 剧本选择界面
    /// </summary>
    public class UIScenarioSelect : UGUIWindow
    {
        public int curSelectIndex;
        public UIScenarioItem uIScenarioItem;
        public Text scenarioDescText;
        public Text scenarioInfoText;
        public RawImage scenarioPosterImg;
        public Text infoText;
        public RawImage mapBoundsImg;
        public Slider mapScaleSlider;
        public int mapDefaultSize = 256;
        public int boundsSize = 448;
        public float mapScale = 1;

        List<UIScenarioItem> selectedItems = new List<UIScenarioItem>();

        public GameObject cityObject;
        public RectTransform mapBounds;
        List<GameObject> cityList = new List<GameObject>();

        List<ShortScenario> all_scenario_list;
        List<ShortScenario> show_scenario_list = new List<ShortScenario>();
        CreatePool<UIScenarioItem> CreatePool;

        public GameObject sureButton;
        public Toggle firstToggle;
        public Toggle customToggle;
        public Action OnReturnAction;
        public Action<ShortScenario> OnNextAction;

        protected override void Awake()
        {
            base.Awake();
            CreatePool = new CreatePool<UIScenarioItem>(uIScenarioItem);
        }

        public override void OnOpen()
        {
            all_scenario_list = ShortScenario.all_scenario_info_list;
            firstToggle?.SetIsOnWithoutNotify(true);
            customToggle?.SetIsOnWithoutNotify(false);
            ShowScenarioByType(1);
        }

        public override void OnOpen(params object[] objects)
        {
            all_scenario_list = objects[0] as List<ShortScenario>;
            int showType = 0;
            if(objects.Length > 1)
            {
                showType = (int)objects[1];
            }
            firstToggle?.SetIsOnWithoutNotify(false); 
            customToggle?.SetIsOnWithoutNotify(true);
            ShowScenarioByType(showType);
        }

        public void Clear()
        {

        }

        public void OnSelectScenario(int index)
        {
            if (curSelectIndex != index)
            {
                if (curSelectIndex >= 0 && curSelectIndex < selectedItems.Count)
                    selectedItems[curSelectIndex].SetSelected(false);
            }
            if (index < selectedItems.Count)
                selectedItems[index].SetSelected(true);
            ShowScenario(index);
        }

        public void ShowScenario(int index)
        {
            if (index == curSelectIndex)
                return;

            curSelectIndex = index;
            if (index < 0)
            {
                scenarioInfoText.text = "";
                scenarioDescText.text = "";
                for (int j = 0; j < cityList.Count; j++)
                {
                    cityList[j].SetActive(false);
                }
                return;
            }

            if (show_scenario_list.Count == 0)
                return;

            ShortScenario scenario = show_scenario_list[curSelectIndex];
            scenario.LoadContent();
            ScenarioInfo scenarioInfo = scenario.Info;

            mapBoundsImg.texture = Render.MapRender.LoadBoundTex(scenario.Map.WorkContent);

            scenarioInfoText.text = scenario.GetDateName();
            scenarioDescText.text = scenarioInfo.description;
            //int i = 0;
            //foreach (ShortCity city in scenario.citySet)
            //{
            //    if (city == null) continue;
            //    if (!city.IsCity()) continue;
            //    if (city.Id == 0) continue;

            //    GameObject cityObj;
            //    if (i >= cityList.Count)
            //    {
            //        cityObj = GameObject.Instantiate(cityObject, cityObject.transform.parent);
            //        cityList.Add(cityObj);
            //        cityObj.name = city.Id.ToString();
            //    }
            //    else
            //    {
            //        cityObj = cityList[i];
            //        cityObj.name = city.Id.ToString();
            //    }

            //    i++;
            //    RectTransform rectTransform = cityObj.GetComponent<RectTransform>();
            //    float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
            //    float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
            //    rectTransform.anchoredPosition = new Vector2((int)(x + 0.5f), (int)(y + 0.5f));

            //    Image bgImg = cityObj.transform.GetChild(0).GetComponent<Image>();
            //    if (city.BelongForceId > 0 && scenario.forceSet[city.BelongForceId] != null)
            //    {
            //        ShortForce shortForce = scenario.forceSet[city.BelongForceId];
            //        Flag flag = scenario.CommonData.Flags[shortForce.Flag];
            //        bgImg.color = flag.color;
            //    }
            //    else
            //    {
            //        bgImg.color = Color.white;
            //    }

            //    cityObj.SetActive(true);
            //}

            for (int j = 0; j < cityList.Count; j++)
            {
                cityList[j].SetActive(false);
            }
            mapScaleSlider.SetValueWithoutNotify(0);
            UpdateMap(scenario, 0);
        }

        public void OnMapScale(float scale)
        {
            UpdateMap(show_scenario_list[curSelectIndex], scale);
        }

        public void UpdateMap(ShortScenario scenario, float scale)
        {
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

            int i = 0;
            foreach (ShortCity city in scenario.citySet)
            {
                if (city == null) continue;
                if (!city.IsCity()) continue;
                if (city.Id == 0) continue;
                GameObject cityObj;
                if (i >= cityList.Count)
                {
                    cityObj = GameObject.Instantiate(cityObject, cityObject.transform.parent);
                    cityList.Add(cityObj);
                    cityObj.name = city.Id.ToString();
                }
                else
                {
                    cityObj = cityList[i];
                    cityObj.name = city.Id.ToString();
                }

                if (city.BuildingType == 1)
                    cityObj.transform.localScale = Vector3.one;
                else
                    cityObj.transform.localScale = Vector3.one * 0.75f;

                i++;
                UIMapCitySelectItem toggle = cityObj.GetComponent<UIMapCitySelectItem>();
                if (toggle != null)
                {
                    toggle.shortCity = city;
                    if (city.BelongForceId == 0 || scenario.forceSet[city.BelongForceId] == null)
                    {
                        toggle.SetSelected(false).SetInavtive(true).SetColor(Color.white);
                    }
                    else
                    {
                        ShortForce shortForce = scenario.forceSet[city.BelongForceId];
                        Flag flag = scenario.CommonData.Flags[shortForce.Flag];
                        toggle.SetSelected(false).SetInavtive(true).SetColor(flag.color);
                    }
                    toggle.ShowName(city.Name);
                }

                RectTransform rectTransform = cityObj.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);

                cityObj.SetActive(true);
            }
        }

        public void OnReturn()
        {
            GameMedia.Instance.PlayCancelSfx();
            Clear();
            if (OnReturnAction != null)
            {
                OnReturnAction.Invoke();
                OnReturnAction = null;
            }
            else
            {
                Window.Instance.Open("window_start");
            }
            Window.Instance.Close("window_scenario_select");
        }

        void ShowScenarioByType(int type)
        {
            show_scenario_list.Clear();

            for (int i = 0; i < all_scenario_list.Count; i++)
            {
                ShortScenario shortScenario = all_scenario_list[i];
                if (type == shortScenario.Info.type)
                    show_scenario_list.Add(shortScenario);
            }
           // show_scenario_list.Sort((a, b) =>
           //{
           //    if (a.Info.year == b.Info.year)
           //    {
           //        return a.Info.month.CompareTo(b.Info.month);
           //    }
           //    else
           //        return a.Info.year.CompareTo(b.Info.year);

           // });
            infoText.enabled = show_scenario_list.Count == 1;
            curSelectIndex = -1;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            sureButton.SetActive(false);
#else
            sureButton.SetActive(true);
#endif
            selectedItems.Clear();
            CreatePool.Reset();
            int count = show_scenario_list.Count;
            for (int i = 0; i < count; i++)
            {
                ShortScenario scenario = show_scenario_list[i];
                UIScenarioItem item = CreatePool.Create();
                item.transform.SetAsLastSibling();
                item.gameObject.SetActive(true);
                int selIndex = i;
                item.targetIndex = selIndex;
                item.SetName(scenario.GetIDName()).SetSelected(i == curSelectIndex);
                selectedItems.Add(item);
                if (!string.IsNullOrEmpty(scenario.ModName))
                    item.SetModName(scenario.ModName);
                else
                    item.SetModName("");

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
                item.BindCall(() => { OnNext(); });
#else
                item.BindCall(() => { OnSelectScenario(selIndex); });
#endif
            }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            ShowScenario(curSelectIndex);
#else
            ShowScenario(0);
#endif
        }

        public void OnToggleScenarioType(bool index)
        {
            if (index)
            {
                ShowScenarioByType(1);
            }
        }

        public void OnToggleScenarioTypePlayer(bool index)
        {
            if (index)
            {
                ShowScenarioByType(0);
            }
        }

        public void OnToggleScenarioTypeCustom(bool index)
        {
            if (index)
            {
                ShowScenarioByType(2);
            }
        }

        public void OnNext()
        {
            if (curSelectIndex == -1) return;
            GameMedia.Instance.PlayButtonSfx();

            Clear();
            ShortScenario scenario = show_scenario_list[curSelectIndex];
            ShortScenario.CurSelected = scenario;
            if (OnNextAction != null)
            {
                OnNextAction.Invoke(scenario);
                OnNextAction = null;
            }
            else
            {
                scenario.LoadFullPersonContent();
                Scenario.CurSelected = new Scenario(show_scenario_list[curSelectIndex].FilePath);
                if (GameCustomEdit.Instance.ModScenarioAddon.PersonLibrary.Count > 0 || GameCustomEdit.Instance.SelfScenarioAddon.PersonLibrary.Count > 0)
                {
                    Window.Instance.Open("window_scenario_addon_menu", scenario);
                }
                else
                {
                    Window.Instance.Open("window_scenario_force_select", scenario);
                }
            }


            Window.Instance.Close("window_scenario_select");
        }
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        private void Update()
        {
            for (int i = 0; i < selectedItems.Count; i++)
            {
                UIScenarioItem uIScenarioItem = selectedItems[i];
                if (RectTransformUtility.RectangleContainsScreenPoint(uIScenarioItem.root, Input.mousePosition, Game.Instance.UICamera))
                {
                    ShowScenario(uIScenarioItem.targetIndex);
                    return;
                }
            }

            //ShowScenario(-1);

        }
#endif
    }
}
