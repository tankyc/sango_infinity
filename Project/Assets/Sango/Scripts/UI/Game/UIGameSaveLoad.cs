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
                // 存档需要的模组有缺失(本机没下载 / 下载了但没启用)时,只把差异和风险讲清楚,
                // 由玩家决定怎么处理。两层确认:
                //   第一层"是否仍要尝试读取" —— 确认 = 强制进入,取消 = 留在界面看存档头部信息;
                //   第二层"要不要先同步模组环境" —— 确认 = 写 modList.txt 后重启,取消 = 直接读档
                //   （见 AskSyncModsThenLoad）。读档过程本身有失败兜底，见 Player.LoadFromFile / OnLoadFailed。
                List<string> notDownloaded;
                List<string> inactive;
                Sango.Mod.ModManager.Instance.CheckSavedModsAvailable(
                    scenario.Info.mods, out notDownloaded, out inactive);

                string warnContent = (scenario.ModsDiffMessage ?? "存档使用的模组与当前不一致。");
                if (inactive != null && inactive.Count > 0)
                    warnContent += "\r\n已下载但未启用的模组: " + string.Join("、", inactive);
                if (notDownloaded != null && notDownloaded.Count > 0)
                    warnContent += "\r\n本机未下载的模组: " + string.Join("、", notDownloaded);
                // 确认后还会再问一次"要不要先同步模组环境"（见 AskSyncModsThenLoad），
                // 文案里先把这个后续选择告诉玩家，避免以为点完就直接读档
                warnContent += "\r\n\r\n是否仍要尝试读取该存档?（确认后可选择先同步模组环境）";

                // 第一层：是否强制进入（不改动模组，风险自担）
                // 第二层：强制进入后再问一次要不要把模组环境同步成存档那一套并重启（见 AskSyncModsThenLoad）
                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, warnContent,
                    () => AskSyncModsThenLoad(index, scenario, notDownloaded),
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

        /// <summary>
        /// 玩家在第一层选择"强制进入"后的第二层询问：要不要先把模组环境同步成存档记录的那一套。
        ///
        /// 【为什么要问第二次】"强制进入"只是带着不匹配的模组硬读存档，存档里由缺失模组提供的事件、
        /// 资源、数据都不会存在，随时可能缺内容或中途报错；真正要让存档回到作者设计的环境，
        /// 必须把启用模组换成存档当时那一套并重启 —— 启用列表只在启动阶段（ModManager.InitMods）
        /// 读一次，进程内改了也不生效。两条路让玩家自己选：
        ///   · 确认 → 同步模组后重启游戏（重启后重新读该存档即是原环境）；
        ///   · 取消 → 不改动模组设置，按当前环境直接进入存档。
        /// </summary>
        /// <param name="index">存档槽位下标</param>
        /// <param name="scenario">目标存档（取 Info.mods 作为同步目标）</param>
        /// <param name="notDownloaded">本机未下载的模组 Id（仅用于提示，可为 null）</param>
        void AskSyncModsThenLoad(int index, ShortScenario scenario, List<string> notDownloaded)
        {
            string content = "是否先把模组环境同步为该存档使用的模组？\r\n"
                           + "同步后需要重启游戏才能生效。";

            if (notDownloaded != null && notDownloaded.Count > 0)
            {
                content += "\r\n\r\n以下模组本机未下载，同步时会被跳过（请先在模组管理器下载）: "
                         + string.Join("、", notDownloaded);
            }

            content += "\r\n\r\n确认 = 同步模组并重启游戏\r\n取消 = 不改动模组，直接进入存档";

            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, content,
                () => SyncModsAndRestart(scenario),
                () => DoLoad(index));
        }

        /// <summary>
        /// 把启用模组列表写成存档记录的那一套，然后重启游戏。
        ///
        /// 只改 Mods/modList.txt：模组加载发生在启动阶段，进程内改列表不会生效，
        /// 必须走 Platform.RestartGame（各平台分别处理，见 Platform.RestartGame）。
        /// 有模组没下载时也照样重启：能同步的部分先落地，缺的那几个玩家需自行在模组管理器补下。
        /// </summary>
        /// <param name="scenario">目标存档</param>
        void SyncModsAndRestart(ShortScenario scenario)
        {
            string[] savedMods = (scenario != null && scenario.Info != null) ? scenario.Info.mods : null;

            // 老存档没有模组记录：无从还原当时的环境，不能拿当前列表去凑数
            if (savedMods == null)
            {
                Sango.Log.Warning("读档：该存档没有记录模组信息，无法自动同步", Sango.Log.LogType.Download);
                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                    "该存档是旧版本保存的，没有记录使用的模组，无法自动同步。\r\n"
                    + "请在模组管理器中手动调整启用列表，然后重启游戏。",
                    () => { });
                return;
            }

            List<string> skipped;
            bool complete = Sango.Mod.ModManager.Instance.SyncModsFromSave(savedMods, out skipped);

            if (complete)
            {
                Sango.Log.Info("读档：模组环境已同步，准备重启游戏", Sango.Log.LogType.Download);
                Sango.Platform.RestartGame();
                return;
            }

            Sango.Log.Warning($"读档：模组同步未完成，缺少 {skipped.Count} 个未下载模组，仍需重启后才能加载已同步的部分",
                Sango.Log.LogType.Download);

            // 【必须把失败原因摊开】同步只写 modList.txt，没写进去的模组重启后依然不会启用，
            // 玩家会再次看到"已下载但未启用的模组"，误以为是同步功能失效。
            // 绝大多数情况是：模组只在市场列表里、本地目录没落地（下载失败 / 被清理 / mod.info 缺失），
            // CheckSavedModsAvailable 已经按同一口径把它归到"本机未下载"，这里负责把 ID 明确列出来。
            string content = "以下模组本机未下载或安装不完整，无法随存档同步：\r\n"
                           + string.Join("、", skipped)
                           + "\r\n\r\n请先在模组管理器中下载 / 重新安装这些模组，再来同步。\r\n"
                           + "确认 = 仍然重启（只生效已同步的部分）\r\n取消 = 留在界面";

            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, content,
                () => Sango.Platform.RestartGame(),
                () => { });
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
            if (scenario.loadFailed)
            {
                Sango.Log.Warning($"存档 {index + 1} 内容损坏，已跳过读取；请选择其他存档或重新保存");
                ResetScenarioDetail();
                return;
            }
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
            if (scenario.loadFailed)
            {
                Sango.Log.Warning($"存档 {index + 1} 内容损坏，已跳过详情渲染；请选择其他存档或重新保存");
                ResetScenarioDetail();
                return false;
            }
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
