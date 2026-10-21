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
            // 同时刷新"模组一致性":存档列表是启动时扫描的,那时模组还没启用完,
            // 且玩家可能中途改过模组,所以每次打开读档界面都重新对比一次
            for (int k = 0; k < all_saved_scenario_list.Length; k++)
            {
                ShortScenario scenario = all_saved_scenario_list[k];
                if (scenario != null && scenario.Info != null)
                {
                    scenario.RefreshModsState();
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
                if (scenario == null || scenario.Info == null)
                {
                    item.SetId(index + 1)          // 显示存档编号（如1、2、3...）
                        .SetInactive(isSave ? false : true)        // 显示空卡槽
                        .SetName(isSave ? "点击存档" : "")        // 空存档提示
                        .SetSaveTime(-1)           // 未存档标记
                        .SetNew(false)
                        .SetWarning(false);
                }
                else
                {
                    item.SetId(index + 1)
                        .SetInactive(false)
                        .SetName(scenario.Info.curForceName)
                        .SetSaveTime(scenario.Info.dateTime)
                        .SetNew(newestData == scenario)
                        // 模组与存档不一致:名字置红,提示"这个档不能直接读"
                        .SetWarning(scenario.ModsDiffer);
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
            // window_dialog 的确认/取消:确认才写盘,取消留在界面
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, content, () =>
            {
                player.Save(index);
                newestData = all_saved_scenario_list[index];
                newestData.LoadContent();
                ShowPage(curShowPage);
            }, () => { });
        }

        public void Load(int index)
        {
            ShortScenario scenario = all_saved_scenario_list[index];
            if (scenario == null || scenario.Info == null)
                return;

            if (scenario.ModsDiffer)
            {
                // 第一步:看存档需要的模组是不是"都已经下载好了,只是没激活"。
                // 满足时优先劝玩家把模组环境还原成存档时的样子再重启,
                // 而不是硬读一个大概率崩掉的存档。
                List<string> notDownloaded;
                List<string> inactive;
                bool allDownloaded = Sango.Mod.ModManager.Instance.CheckSavedModsAvailable(
                    scenario.Info.mods, out notDownloaded, out inactive);

                if (allDownloaded)
                {
                    string adoptContent = BuildAdoptModsContent(inactive);
                    GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, adoptContent,
                        () =>
                        {
                            // 确认:按存档记录的模组与顺序重建启用列表(存档里没有的模组会被停用),
                            // 重启后模组环境与存档一致,可以正常读档
                            Sango.Mod.ModManager.Instance.ApplyModListByIds(scenario.Info.mods);
                            Sango.Platform.RestartGame();
                        },
                        () => ShowModsDifferDetail(index, scenario));
                    return;
                }

                // 第二步:有模组本机没下载,只能把风险讲清楚,由玩家决定是否硬读。
                // window_dialog(UIDialog)本身就有"确认(OnSure)/取消(OnCancel)"两个按钮:
                //   · 确认 → 承担风险,真正读档(读档过程有失败兜底,见 Player.LoadFromFile / OnLoadFailed);
                //   · 取消 → 什么都不做,留在读档界面。
                string warnContent = (scenario.ModsDiffMessage ?? "存档使用的模组与当前不一致。");
                if (notDownloaded != null && notDownloaded.Count > 0)
                    warnContent += "\r\n本机未下载的模组: " + string.Join("、", notDownloaded);
                warnContent += "\r\n\r\n继续读取很可能导致加载失败,是否仍要尝试?";

                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, warnContent,
                    () => DoLoad(index),
                    () => ShowModsDifferDetail(index, scenario));
                return;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            DoLoad(index);
#else
            string content = $"是否加载{index + 1}号存档？";
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, content,
                () => DoLoad(index),
                () => { });
#endif
        }

        /// <summary>
        /// 拼"按存档记录还原模组并重启"的提示文案。
        ///
        /// 两种情况都会走到这里:
        ///   · 存档需要的模组已下载但未激活(inactive 非空);
        ///   · 需要的模组都已激活,但还多启用了别的模组 / 顺序不同(inactive 为空)。
        /// </summary>
        /// <param name="inactive">已下载但未激活的模组 Id</param>
        string BuildAdoptModsContent(List<string> inactive)
        {
            string content = "存档使用的模组与当前不一致。\r\n";
            if (inactive != null && inactive.Count > 0)
                content += $"以下模组已下载但未激活: {string.Join("、", inactive)}\r\n";
            else
                content += "存档需要的模组都已启用,但启用项或顺序与存档不同。\r\n";

            content += "是否按存档记录启用模组并重启游戏?\r\n"
                     + "说明:存档里没有启用的模组会被停用;重启后模组环境与存档一致,即可正常读取该存档。";
            return content;
        }

        /// <summary>
        /// 真正执行读档(需要先关闭当前剧本的系统栈)
        /// </summary>
        /// <param name="index">存档槽位下标</param>
        void DoLoad(int index)
        {
            GameSystemManager.Instance.Done();
            if (all_saved_scenario_list == player.all_auto_saved_scenario_list)
                player.LoadAutoFile(index);
            else
                player.Load(index);
        }

        public void ShowScenario(int index)
        {


            if (isSave)
            {
                Save(index);
            }

            curSelectIndex = index;
            ShortScenario scenario = all_saved_scenario_list[index];
            if (scenario == null || scenario.Info == null)
            {
                ResetScenarioDetail();
                return;
            }

            // 模组与存档不一致:不解析地图/势力(解析大概率失败),只展示文件头部的基础信息;
            // 点击后由 Load() 先弹风险提示,玩家确认了才真正加载
            if (scenario.ModsDiffer)
            {
                ShowModsDifferDetail(index, scenario);
                if (!isSave)
                {
                    Load(index);
                }
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

            if (scenario == null || scenario.Info == null)
            {
                ResetScenarioDetail();
                return false;
            }

            // 模组与存档不一致:只展示基础信息,绝不解析地图/势力/城池
            if (scenario.ModsDiffer)
            {
                ShowModsDifferDetail(index, scenario);
                return false;
            }

            scenario.LoadContent();
            ScenarioInfo scenarioInfo = scenario.Info;
            id.SetText((index + 1).ToString());
            name.SetText($"{scenarioInfo.year} 年 {scenarioInfo.month}月 {scenarioInfo.day}日   {scenarioInfo.name}");

            // 上帝放置模式（开局不选势力）存出来的档，playerForceList 是空数组。
            // 这里以前无条件取 [0]，导致"不选势力 → 存一次档 → 再打开存档/读档界面"直接越界崩掉。
            // 无玩家势力是设计内的正常状态，所以按空态渲染：头像沿用 ResetScenarioDetail 的关闭方式，
            // 势力名位置直接写明所处模式，不再输出"0人游玩"这种会被当成数据错误的计数。
            bool hasPlayerForce = scenarioInfo.playerForceList != null && scenarioInfo.playerForceList.Length > 0;
            if (hasPlayerForce)
            {
                ShortForce force = scenario.forceSet[scenarioInfo.playerForceList[0]];
                head.enabled = true;
                head.texture = GameRenderHelper.LoadHeadIcon(scenario.personSet[force.Governor].headIconID);
                forceName.SetText(force.Name);
                playNum.SetText($"{scenarioInfo.playerForceList.Length.ToString()}人游玩");
            }
            else
            {
                head.enabled = false;
                head.texture = null;
                forceName.SetText("上帝放置模式");
                playNum.SetText("无玩家势力");
            }
            playYear.SetText($"{scenarioInfo.year} 年 {scenarioInfo.month}月 {scenarioInfo.day}日");
            DateTime date = DateTime.FromFileTime(scenarioInfo.dateTime);
            day.SetText(date.ToString("yyyy-MM-dd"));
            time.SetText(date.ToString("HH:mm:ss"));
            playTime.SetText("");

            //desc.SetText(scenarioInfo.description);
            // 是否渲染形势图
            return RenderCities(scenario);
        }

        /// <summary>
        /// 展示"模组与存档不一致"的存档基础信息。
        ///
        /// 这类存档只保证文件头部的 <see cref="ScenarioInfo"/> 可用,正文(地图/势力/武将/城池)
        /// 在模组对不上时解析极可能失败,所以这里刻意**不调用** LoadContent:
        ///   · 头像不显示(需要势力/武将数据);
        ///   · 城市形势图不渲染(需要地图与城池数据);
        ///   · 只填日期、势力名、游玩人数这些头部信息。
        /// </summary>
        /// <param name="index">槽位下标</param>
        /// <param name="scenario">目标存档</param>
        void ShowModsDifferDetail(int index, ShortScenario scenario)
        {
            ScenarioInfo scenarioInfo = scenario != null ? scenario.Info : null;
            if (scenarioInfo == null)
            {
                ResetScenarioDetail();
                return;
            }

            id.SetText((index + 1).ToString());
            name.SetText($"{scenarioInfo.year} 年 {scenarioInfo.month}月 {scenarioInfo.day}日   {scenarioInfo.name}");

            bool hasPlayerForce = scenarioInfo.playerForceList != null && scenarioInfo.playerForceList.Length > 0;
            head.enabled = false;
            head.texture = null;
            forceName.SetText(string.IsNullOrEmpty(scenarioInfo.curForceName) ? "（模组不一致）" : scenarioInfo.curForceName);
            playNum.SetText(hasPlayerForce ? $"{scenarioInfo.playerForceList.Length.ToString()}人游玩" : "无玩家势力");

            playYear.SetText($"{scenarioInfo.year} 年 {scenarioInfo.month}月 {scenarioInfo.day}日");
            DateTime date = DateTime.FromFileTime(scenarioInfo.dateTime);
            day.SetText(date.ToString("yyyy-MM-dd"));
            time.SetText(date.ToString("HH:mm:ss"));
            // 详情面板明确写出风险,玩家一眼能看出这个档"只能试着读"
            playTime.SetText("注意:模组与当前不一致");

            // 地图/势力数据没有加载,形势图必须清空,避免显示上一个存档的城市
            for (int j = 0; j < cityList.Count; j++)
            {
                cityList[j].SetActive(false);
            }
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

                // 设置城市位置
                RectTransform rectTransform = cityObj.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);

                // 设置城市颜色
                Image bgImg = cityObj.transform.GetChild(0).GetComponent<Image>();
                if (city.BelongForceId > 0)
                {
                    ShortForce shortForce = scenario.forceSet[city.BelongForceId];
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
