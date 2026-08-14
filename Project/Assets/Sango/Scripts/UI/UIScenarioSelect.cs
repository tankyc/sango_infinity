using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Sango.Core;
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

        List<UIScenarioItem> selectedItems = new List<UIScenarioItem>();

        public GameObject cityObject;
        public RectTransform mapBounds;
        List<GameObject> cityList = new List<GameObject>();

        List<ShortScenario> show_scenario_list = new List<ShortScenario>();
        CreatePool<UIScenarioItem> CreatePool;

        public GameObject sureButton;
        public Toggle firstToggle;

        protected override void Awake()
        {
            base.Awake();
            CreatePool = new CreatePool<UIScenarioItem>(uIScenarioItem);
        }

        public override void OnOpen()
        {
            firstToggle?.SetIsOnWithoutNotify(true);
            ShowScenarioByType(0);



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
            if(index < selectedItems.Count)
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

            scenarioInfoText.text = scenario.GetDateName();
            scenarioDescText.text = scenarioInfo.description;
            int i = 0;
            foreach (var city in scenario.citySet.Values)
            {

                if (city.BuildingType > 1) continue;
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

                i++;
                RectTransform rectTransform = cityObj.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2((int)(x + 0.5f), (int)(y + 0.5f));

                Image bgImg = cityObj.transform.GetChild(0).GetComponent<Image>();
                if (city.BelongForce > 0)
                {
                    ShortForce shortForce = scenario.forceSet[city.BelongForce];
                    Flag flag = scenario.CommonData.Flags[shortForce.Flag];
                    bgImg.color = flag.color;
                }
                else
                {
                    bgImg.color = Color.white;
                }

                cityObj.SetActive(true);
            }

            for (int j = i; j < cityList.Count; j++)
            {
                cityList[j].SetActive(false);
            }

        }

        public void OnReturn()
        {
            GameMedia.Instance.PlayCancelSfx();
            Clear();
            Window.Instance.Open("window_start");
            Window.Instance.Close("window_scenario_select");
        }

        void ShowScenarioByType(int type)
        {
            show_scenario_list.Clear();

            for (int i = 0; i < ShortScenario.all_scenario_info_list.Count; i++)
            {
                ShortScenario shortScenario = ShortScenario.all_scenario_info_list[i];
                if (type == shortScenario.Info.type)
                    show_scenario_list.Add(shortScenario);
            }



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
                ShowScenarioByType(0);
            }
        }

        public void OnToggleScenarioTypePlayer(bool index)
        {
            if (index)
            {
                ShowScenarioByType(1);
            }
        }

        public void OnNext()
        {
            if (curSelectIndex == -1) return;
            GameMedia.Instance.PlayButtonSfx();

            Clear();
            ShortScenario scenario = show_scenario_list[curSelectIndex];
            ShortScenario.CurSelected = scenario;

            Scenario.CurSelected = new Scenario(show_scenario_list[curSelectIndex].FilePath);

            if(GameCustomEdit.Instance.ModScenarioAddon.PersonLibrary.Count > 0 || GameCustomEdit.Instance.SelfScenarioAddon.PersonLibrary.Count >0)
            {
                Window.Instance.Open("window_scenario_addon_menu");
            }
            else
            {
                Window.Instance.Open("window_scenario_force_select");
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

            ShowScenario(-1);

        }
#endif
    }
}
