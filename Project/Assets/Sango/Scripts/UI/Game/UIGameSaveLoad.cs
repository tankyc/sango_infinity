using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    /// <summary>
    /// 存档读档界面
    /// </summary>
    public class UIGameSaveLoad : UGUIWindow
    {
        int sysType = 0;
        public int curShowPage = -1;
        public int CountInPage = 8;
        public int curSelectIndex = -1;
        public ShortScenario[] all_saved_scenario_list;

        [Header("分页控制")]
        public Toggle[] pageToggles; // 拖入第1-4页Toggle
        public UIScenarioSaveItem[] selectedItems;

        [Header("详情面板")]
        public Text titleLabel;
        public UITextField id, name, forceName, playYear, playNum, day, time, playTime, desc;
        public RawImage head;

        [Header("地图渲染")]
        public GameObject cityObject;
        public RectTransform mapBounds;

        List<GameObject> cityList = new List<GameObject>();
        ShortScenario newestData;
        UIScenarioSaveItem curSelectedItem;
        Player player;
        bool isSave => sysType == 0;

        public override void OnOpen(params object[] objects)
        {
            player = GameSystem.GetSystem<Player>();
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            curSelectIndex = -1;
#else
            curSelectIndex = 0;
#endif
            sysType = (int)objects[0];
            titleLabel.text = isSave ? "储存" : "读档";
            long t = 0;

            all_saved_scenario_list = player.all_saved_scenario_list;

            // 遍历存档列表，找到最新的存档
            for (int k = 0; k < all_saved_scenario_list.Length; k++)
            {
                ShortScenario scenario = all_saved_scenario_list[k];
                if (scenario != null)
                {
                    if (scenario.Info.dateTime > t)
                    {
                        newestData = scenario;
                        t = scenario.Info.dateTime;
                    }
                }
            }
            // 绑定Toggle翻页事件
            InitPageToggles();

            // 默认显示第1页
            SetToggleSelected(0);
            ShowPage(0);

            if (pageToggles.Length > 4)
                pageToggles[4].interactable = !isSave;
        }

        /// <summary>
        /// 初始化Toggle翻页事件
        /// </summary>
        private void InitPageToggles()
        {
            for (int i = 0; i < pageToggles.Length; i++)
            {
                int pageIndex = i;
                pageToggles[i].onValueChanged.RemoveAllListeners();
                pageToggles[i].onValueChanged.AddListener(isSelected =>
                {
                    if (isSelected)
                    {
                        ShowPage(pageIndex);
                    }
                });
            }
        }

        /// <summary>
        /// 设置Toggle选中状态（单选组互斥）
        /// </summary>
        public void SetToggleSelected(int pageIndex)
        {
            for (int i = 0; i < pageToggles.Length; i++)
            {
                pageToggles[i].isOn = (i == pageIndex);
            }
        }

        /// <summary>
        /// 显示指定页码的存档列表
        /// </summary>
        public void ShowPage(int curShowPage)
        {
            ResetScenarioDetail();
            this.curShowPage = curShowPage;

            if (curShowPage == 4)
                all_saved_scenario_list = player.all_auto_saved_scenario_list;
            else
                all_saved_scenario_list = player.all_saved_scenario_list;


            // 循环生成每页的存档项
            for (int i = 0; i < CountInPage; i++)
            {
                int index = curShowPage * CountInPage + i;
                if (curShowPage == 4)
                {
                    index = i;
                }
                ShortScenario scenario = all_saved_scenario_list[index];
                UIScenarioSaveItem item = selectedItems[i];
                item.SetIsLoad(!isSave);
                item.SetSelected(false);
                item.targetIndex = index;
                if (scenario == null)
                {
                    item.SetId(index + 1)          // 显示存档编号（如1、2、3...）
                        .SetInactive(isSave ? false : true)        // 显示空卡槽
                        .SetName(isSave ? "点击存档" : "")        // 空存档提示
                        .SetSaveTime(-1)           // 未存档标记
                        .SetNew(false);
                }
                else
                {
                    item.SetId(index + 1)
                        .SetInactive(false)
                        .SetName(scenario.Info.curForceName)
                        .SetSaveTime(scenario.Info.dateTime)
                        .SetNew(newestData == scenario);
                }

                // 绑定点击事件（使用临时变量捕获当前index，避免闭包陷阱）
                item.BindCall(() =>
                {
                    if (curSelectedItem != null)
                        curSelectedItem.SetSelected(false);
                    curSelectedItem = null;
                    ShortScenario scenario1 = all_saved_scenario_list[index];
                    if (scenario1 != null)
                    {
                        curSelectedItem = item;
                        //if(!isSave)
                        //    curSelectedItem.SetSelected(true);
                    }
                    ShowScenario(index);
                });
            }
        }

        public void Clear()
        {

        }

        /// <summary>
        /// 重置详情面板为空
        /// </summary>
        public void ResetScenarioDetail()
        {
            curSelectedItem = null;
            curSelectIndex = -1;
            id.SetText("");
            name.SetText("");
            forceName.SetText("");
            playYear.SetText("");
            playNum.SetText("");
            day.SetText("");
            time.SetText("");
            playTime.SetText("");
            desc.SetText("");
            head.enabled = false;
            head.texture = null;
            for (int j = 0; j < cityList.Count; j++)
            {
                cityList[j].SetActive(false);
            }
        }

        public void Save(int index)
        {
            ShortScenario scenario = all_saved_scenario_list[index];
            string content = scenario != null ? $"是否覆盖{index + 1}号存档" : $"是否保存至{index + 1}号存档";
            GameDialog.Open(content, async () =>
            {
                player.Save(index);
                newestData = all_saved_scenario_list[index];
                newestData.LoadContent();
                GameDialog.Close();
                ShowPage(curShowPage);
            });
        }

        public void Load(int index)
        {
            ShortScenario scenario = all_saved_scenario_list[index];
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            GameDialog.Close();
            GameSystemManager.Instance.Done();
            if (all_saved_scenario_list == player.all_auto_saved_scenario_list)
                player.LoadAutoFile(index);
            else
                player.Load(index);
#else
            string content = $"是否加载{index + 1}号存档？";
            GameDialog.Open(content, () =>
            {
                GameDialog.Close();
                GameSystemManager.Instance.Done();
                if (all_saved_scenario_list == player.all_auto_saved_scenario_list)
                    player.LoadAutoFile(index);
                else
                    player.Load(index);
            });
#endif
        }

        public void ShowScenario(int index)
        {


            if (isSave)
            {
                Save(index);
            }

            curSelectIndex = index;
            ShortScenario scenario = all_saved_scenario_list[index];
            if (scenario == null)
            {
                ResetScenarioDetail();
                return;
            }

            scenario.LoadContent();
            if (!isSave)
            {
                Load(index);
            }

            bool flowControl = ShowScenarioDetail(index, scenario);

            if (!flowControl)
            {
                return;
            }

        }

        public bool ShowScenarioDetail(int index, ShortScenario scenario)
        {
            if (index == curSelectIndex)
                return false;

            if (index < 0)
            {
                ResetScenarioDetail();
                return false;
            }

            if (scenario == null)
            {
                ResetScenarioDetail();
                return false;
            }

            scenario.LoadContent();
            ScenarioInfo scenarioInfo = scenario.Info;
            id.SetText((index + 1).ToString());
            name.SetText($"{scenarioInfo.year} 年 {scenarioInfo.month}月 {scenarioInfo.day}日   {scenarioInfo.name}");

            ShortForce force = scenario.forceSet[scenarioInfo.playerForceList[0]];
            head.enabled = true;
            head.texture = GameRenderHelper.LoadHeadIcon(scenario.personSet[force.Governor].headIconID);
            forceName.SetText(force.Name);
            playYear.SetText($"{scenarioInfo.year} 年 {scenarioInfo.month}月 {scenarioInfo.day}日");
            playNum.SetText($"{scenarioInfo.playerForceList.Length.ToString()}人游玩");
            DateTime date = DateTime.FromFileTime(scenarioInfo.dateTime);
            day.SetText(date.ToString("yyyy-MM-dd"));
            time.SetText(date.ToString("HH:mm:ss"));
            playTime.SetText("");

            //desc.SetText(scenarioInfo.description);
            // 是否渲染形势图
            return RenderCities(scenario);
        }

        /// <summary>
        /// 渲染城市形势图标
        /// </summary>
        public bool RenderCities(ShortScenario scenario)
        {
            int i = 0;
            foreach (ShortCity city in scenario.citySet)
            {
                if (city == null) continue;
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

                // 设置城市位置
                RectTransform rectTransform = cityObj.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);

                // 设置城市颜色
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
                i++;
            }

            // 隐藏多余城市
            for (int j = i; j < cityList.Count; j++)
            {
                cityList[j].SetActive(false);
            }

            return true;
        }

        public void OnSure()
        {
            if (isSave) Save(curSelectIndex);
            else Load(curSelectIndex);
        }

        public void OnReturn()
        {
            Clear();

            if (isSave) GameSystem.GetSystem<GameSave>().Done();
            else GameSystem.GetSystem<GameLoad>().Done();

            if (sysType == 2)
            {
                Close();
                return;
            }
        }

        //public void OnPage1(bool select) { if (select) ShowPage(0); }
        //public void OnPage2(bool select) { if (select) ShowPage(1); }
        //public void OnPage3(bool select) { if (select) ShowPage(2); }
        //public void OnPage4(bool select) { if (select) ShowPage(3); }


#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        private void Update()
        {

            for (int i = 0; i < selectedItems.Length; i++)
            {
                UIScenarioSaveItem uIScenarioItem = selectedItems[i];
                if (RectTransformUtility.RectangleContainsScreenPoint(uIScenarioItem.root, Input.mousePosition, Sango.Core.Game.Instance.UICamera))
                {
                    if (uIScenarioItem.targetIndex < all_saved_scenario_list.Length)
                    {
                        ShortScenario scenario = all_saved_scenario_list[uIScenarioItem.targetIndex];
                        ShowScenarioDetail(uIScenarioItem.targetIndex, scenario);
                        return;
                    }
                    else
                    {
                        ShowScenarioDetail(-1, null);
                        return;
                    }
                }
            }

            ShowScenarioDetail(-1, null);

        }
#endif
    }
}
