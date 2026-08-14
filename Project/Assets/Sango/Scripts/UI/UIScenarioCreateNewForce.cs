using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 剧本前"建立新势力"界面。
    /// 可建势力数 = min(空白城市数量, 可用旗帜数量)。
    /// 复用 UICreateForceItem 展示每个可建势力的槽位。
    /// 数据仅来自 ShortScenario.CurSelected 与 GameData.Instance.ScenarioCommonData,
    /// 不涉及任何 Scenario.Cur 相关数据。
    /// </summary>
    public class UIScenarioCreateNewForce : UGUIWindow
    {
        // ===== 公共组件（在 prefab 中手动指派） =====
        public UIEditWorldMap uIEditWorldMap;       // 右侧地图（由打开者传递进来）
        public Transform forceItemParent;            // UICreateForceItem 的父节点
        public UICreateForceItem forceItemTemplate;  // UICreateForceItem 模板（用于 CreatePool）
        public Button returnBtn;                     // 返回
        public Button confirmBtn;                  // 确认建立
        public Text unassignedNewPersonCountText;    // 未配属之新武将数量
        public Text assignedNewPersonCountText;      // 已配属之新武将数量

        /// <summary>
        /// 点击"新建"势力后打开编辑界面时需要隐藏、返回后恢复显示的节点(在 prefab 中手动指派)。
        /// </summary>
        public GameObject hideNodeOnEditForce;

        // ===== 数据 =====
        ShortScenario scenario;
        ScenarioCommonData commonData;
        CreatePool<UICreateForceItem> itemPool;
        List<UICreateForceItem> activeItems = new List<UICreateForceItem>();

        // 可建势力上限
        int maxNewForceCount;
        // 当前已建势力数
        int currentNewForceCount => UIScenarioAddonMenu.AddData.NewForceCount;
        // 空白城市列表
        List<ShortCity> emptyCities = new List<ShortCity>();
        // 可用旗帜列表
        List<Flag> availableFlags = new List<Flag>();
        // 已用旗帜索引
        HashSet<int> usedFlagIndices = new HashSet<int>();
        // 已用城市ID
        HashSet<int> usedCityIds = new HashSet<int>();

        bool eventsBound;
        bool editForceCloseBound;

        public override void OnOpen(params object[] args)
        {
            base.OnOpen(args);

            // 按钮事件自动绑定（仅一次）
            BindButtonEvents();

            scenario = ShortScenario.CurSelected;
            if (scenario == null)
            {
                Debug.LogWarning("UIScenarioCreateNewForce: ShortScenario.CurSelected 为空");
                Close();
                return;
            }

            commonData = scenario.CommonData != null ? scenario.CommonData : GameData.Instance.ScenarioCommonData;

            // 从打开者接收 uIEditWorldMap（通过 args 或 public 字段）
            if (args != null && args.Length > 0 && args[0] is UIEditWorldMap map)
            {
                uIEditWorldMap = map;
            }

            // 初始化对象池
            if (itemPool == null && forceItemTemplate != null && forceItemParent != null)
            {
                itemPool = new CreatePool<UICreateForceItem>(forceItemTemplate);
            }

            // 计算可建势力数
            CalcMaxNewForceCount();

            // 刷新地图：只显示空白城市可选
            RefreshMap();

            // 刷新势力列表
            RefreshForceItems();

            // 刷新数量显示
            RefreshInfo();
        }

        #region 数量显示

        /// <summary>
        /// 刷新"已配属/未配属之新武将"数量显示
        /// </summary>
        void RefreshInfo()
        {
            var addData = UIScenarioAddonMenu.AddData;
            SetCountText(unassignedNewPersonCountText, addData.UnassignedCount);
            SetCountText(assignedNewPersonCountText, addData.AssignedCount);
        }

        void SetCountText(Text text, int count)
        {
            if (text != null)
                text.text = count.ToString();
        }

        #endregion

        #region 计算可建势力数

        void CalcMaxNewForceCount()
        {
            emptyCities.Clear();
            availableFlags.Clear();
            usedFlagIndices.Clear();
            usedCityIds.Clear();

            // 收集空白城市（BelongForce == 0）
            if (scenario.citySet != null)
            {
                foreach (var city in scenario.citySet.Values)
                {
                    if (city != null && city.Id != 0 && city.BuildingType <= 1 && city.BelongForce == 0)
                    {
                        emptyCities.Add(city);
                    }
                }
            }

            // 收集可用旗帜
            if (commonData != null && commonData.Flags != null)
            {
                commonData.Flags.ForEach(flag =>
                {
                    if (flag != null)
                        availableFlags.Add(flag);
                });
            }

            // 可建势力数 = min(空白城市数, 可用旗帜数)
            int emptyCityCount = emptyCities.Count;
            int flagCount = availableFlags.Count;
            maxNewForceCount = Mathf.Min(emptyCityCount, flagCount);

            // 扣除已建势力占用的资源
            var addData = UIScenarioAddonMenu.AddData;
            for (int i = 0; i < addData.NewForces.Count; i++)
            {
                NewForceData force = addData.NewForces[i];
                if (force == null) continue;
                if (force.Flag != null)
                {
                    int flagIdx = availableFlags.IndexOf(force.Flag);
                    if (flagIdx >= 0)
                        usedFlagIndices.Add(flagIdx);
                }
                if (force.CapitalCity != null)
                {
                    usedCityIds.Add(force.CapitalCity.Id);
                }
            }
        }

        #endregion

        #region 刷新地图

        void RefreshMap()
        {
            if (uIEditWorldMap != null)
            {
                uIEditWorldMap.SetScenario(scenario);
                uIEditWorldMap.RefreshCity();
            }
        }

        #endregion

        #region 刷新势力列表（复用 UICreateForceItem）

        void RefreshForceItems()
        {
            // 清空旧项
            foreach (var item in activeItems)
            {
                if (item != null)
                    item.gameObject.SetActive(false);
            }
            activeItems.Clear();
            if (itemPool != null)
                itemPool.Reset();

            var addData = UIScenarioAddonMenu.AddData;

            // 1. 显示已建势力
            for (int i = 0; i < addData.NewForces.Count; i++)
            {
                NewForceData force = addData.NewForces[i];
                if (force == null) continue;
                UICreateForceItem item = CreateForceItem(i, true);
                if (item == null) continue;

                if (item.forceName != null)
                    item.forceName.text = force.ForceName ?? "新势力";
                if (item.cityName != null)
                    item.cityName.text = force.CapitalCity != null ? force.CapitalCity.Name : "-";
                if (item.flagColor != null)
                    item.flagColor.color = force.ForceColor;
                if (item.btnName != null)
                    item.btnName.text = "已建立";
                // 已建势力可删除
                if (item.cancelBtn != null)
                    item.cancelBtn.interactable = true;

                // 已建势力点击删除
                int idx = i;
                item.onClickDelete = (index) => OnDeleteForce(idx);
                item.onClickNew = (index) => OnClickNewForceSlot(idx);

                activeItems.Add(item);
            }

            // 2. 显示可新建槽位（剩余可建数量）
            int remaining = maxNewForceCount - addData.NewForceCount;
            for (int i = 0; i < remaining; i++)
            {
                int slotIndex = addData.NewForces.Count + i;
                UICreateForceItem item = CreateForceItem(slotIndex, false);
                if (item == null) continue;

                // 无指定势力：君主/本城文本置空,势力颜色为白色,删除按钮禁止点击
                if (item.forceName != null)
                    item.forceName.text = "";
                if (item.cityName != null)
                    item.cityName.text = "";
                if (item.flagColor != null)
                    item.flagColor.color = Color.white;
                if (item.btnName != null)
                    item.btnName.text = "新建";
                if (item.cancelBtn != null)
                    item.cancelBtn.interactable = false;

                // 未建势力点击新建
                int idx = slotIndex;
                item.onClickNew = (index) => OnClickNewForceSlot(idx);
                item.onClickDelete = null;

                activeItems.Add(item);
            }
        }

        UICreateForceItem CreateForceItem(int index, bool isCreated)
        {
            if (itemPool == null || forceItemParent == null)
                return null;

            UICreateForceItem item = itemPool.Create();
            item.transform.SetParent(forceItemParent, false);
            item.index = index;
            item.gameObject.SetActive(true);
            return item;
        }

        #endregion

        #region 新建势力流程

        // 当前正在操作的槽位索引
        int pendingSlotIndex = -1;
        NewForceData pendingForceData = null;
        ShortCity pendingCity;
        PersonLib pendingGovernor;
        Flag pendingFlag;

        /// <summary>
        /// 点击"新建"按钮：选择城市 → 选择君主 → 打开编辑界面
        /// </summary>
        void OnClickNewForceSlot(int slotIndex)
        {
            pendingSlotIndex = slotIndex;

            var addData = UIScenarioAddonMenu.AddData;

            NewForceData newForceData;
            if (slotIndex < addData.NewForces.Count)
            {
                newForceData = addData.NewForces[slotIndex];
            }
            else
            {
                newForceData = new NewForceData();
                newForceData.ForceId = slotIndex;
            }
            pendingForceData = newForceData;
            Window.Instance.Open("window_scenario_edit_new_force", uIEditWorldMap, pendingForceData, scenario, commonData).ugui_instance.OnCloseAction = () =>
            {
                SetHideNodeActive(true);
                // 如果编辑界面确认建立了势力，刷新本界面数据
                RefreshMap();
                RefreshForceItems();
                RefreshInfo();
            };
            hideNodeOnEditForce.SetActive(false);
        }

        /// <summary>
        /// 打开"编辑新势力"子界面，隐藏本节点，传递参数（含数据写入位置索引）
        /// </summary>
        void OpenEditNewForce()
        {
            SetHideNodeActive(false);

            // 最后一个参数为数据写入索引位置，用于 UIScenarioCreateEditNewForce 将 NewForceData 保存到 addData.NewForces[pendingSlotIndex]
            var editWin = Window.Instance.Open<UIScenarioCreateEditNewForce>("window_scenario_edit_new_force", uIEditWorldMap, pendingCity, pendingGovernor, pendingFlag, scenario, commonData, pendingSlotIndex);
            if (editWin == null)
            {
                SetHideNodeActive(true);
                return;
            }

            // 订阅关闭事件（仅一次）
            if (!editForceCloseBound)
            {
                editForceCloseBound = true;
                Action prev = editWin.OnCloseAction;
                editWin.OnCloseAction = () =>
                {
                    prev?.Invoke();
                    SetHideNodeActive(true);
                    // 如果编辑界面确认建立了势力，刷新本界面数据
                    RefreshMap();
                    RefreshForceItems();
                    RefreshInfo();
                };
            }
        }

        /// <summary>
        /// 设置隐藏节点的显示状态
        /// </summary>
        void SetHideNodeActive(bool active)
        {
            if (hideNodeOnEditForce != null)
                hideNodeOnEditForce.SetActive(active);
        }

        /// <summary>
        /// 从编辑界面确认建立/修改势力。
        /// 将编辑界面持有的 NewForceData 保存到 addData.NewForces[slotIndex]，
        /// 并写回 ShortScenario、标记资源占用。
        /// </summary>
        public void CreateNewForceFromEdit(int slotIndex, NewForceData forceData)
        {
            if (forceData == null)
                return;

            var addData = UIScenarioAddonMenu.AddData;

            // 若该槽位已有旧势力数据，先释放其占用的资源
            if (slotIndex >= 0 && slotIndex < addData.NewForces.Count && addData.NewForces[slotIndex] != null)
            {
                ReleaseForceResources(addData.NewForces[slotIndex]);
            }

            // 分配势力ID（仅新建/未分配时）
            if (forceData.ForceId <= 0)
                forceData.ForceId = GetNewForceId();

            // 势力名兜底
            if (string.IsNullOrEmpty(forceData.ForceName))
            {
                forceData.ForceName = string.IsNullOrEmpty(forceData.Governor?.familyName)
                    ? $"新势力{forceData.ForceId}"
                    : $"{forceData.Governor.familyName}军";
            }

            // 写入列表对应位置（补齐中间空位）
            while (addData.NewForces.Count <= slotIndex)
                addData.NewForces.Add(null);
            addData.NewForces[slotIndex] = forceData;

            // 配属君主
            if (forceData.Governor != null)
                addData.AssignPersonToForce(forceData.Governor, forceData.ForceId);

            // 写回 ShortScenario
            if (forceData.CapitalCity != null)
            {
                WriteForceToScenario(forceData.ForceId, forceData.ForceName, forceData.Governor, forceData.Flag);
                WritePersonToScenario(forceData.Governor, forceData.ForceId, forceData.CapitalCity.Id);

                // 主城标记归属
                if (scenario.citySet.TryGetValue(forceData.CapitalCity.Id, out ShortCity city))
                    city.BelongForce = forceData.ForceId;
                usedCityIds.Add(forceData.CapitalCity.Id);
            }

            // 标记旗帜已用
            if (forceData.Flag != null)
            {
                int flagIdx = availableFlags.IndexOf(forceData.Flag);
                if (flagIdx >= 0)
                    usedFlagIndices.Add(flagIdx);
            }

            // 刷新
            RefreshMap();
            RefreshForceItems();
            RefreshInfo();
        }

        /// <summary>
        /// 释放一个势力的资源占用（城市、旗帜、武将配属、scenario forceSet），不操作 NewForces 列表
        /// </summary>
        void ReleaseForceResources(NewForceData force)
        {
            if (force == null)
                return;

            var addData = UIScenarioAddonMenu.AddData;

            // 释放城市
            if (force.CapitalCity != null)
            {
                usedCityIds.Remove(force.CapitalCity.Id);
                if (scenario.citySet.TryGetValue(force.CapitalCity.Id, out ShortCity city))
                {
                    city.BelongForce = 0;
                }
            }

            // 释放旗帜
            if (force.Flag != null)
            {
                int flagIdx = availableFlags.IndexOf(force.Flag);
                if (flagIdx >= 0)
                    usedFlagIndices.Remove(flagIdx);
            }

            // 释放武将配属
            for (int i = force.Persons.Count - 1; i >= 0; i--)
            {
                PersonLib person = force.Persons[i];
                if (person != null)
                {
                    addData.UnassignPerson(person);
                    // 从 scenario personSet 中重置
                    if (scenario.personSet.TryGetValue(person.Id, out ShortPerson sp))
                    {
                        sp.BelongForce = 0;
                        sp.BelongCity = 0;
                    }
                }
            }

            // 从 scenario forceSet 中移除
            if (scenario.forceSet.ContainsKey(force.ForceId))
            {
                scenario.forceSet.Remove(force.ForceId);
            }
        }

        /// <summary>
        /// 删除已建势力
        /// </summary>
        void OnDeleteForce(int index)
        {
            var addData = UIScenarioAddonMenu.AddData;
            if (index < 0 || index >= addData.NewForces.Count)
                return;

            NewForceData force = addData.NewForces[index];
            ReleaseForceResources(force);
            addData.RemoveNewForce(force);

            RefreshMap();
            RefreshForceItems();
            RefreshInfo();
        }

        #endregion

        #region 数据查询

        ShortCity GetSelectedEmptyCityFromMap()
        {
            if (uIEditWorldMap == null || uIEditWorldMap.selecte_list == null || uIEditWorldMap.selecte_list.Count == 0)
                return null;
            ShortCity city = uIEditWorldMap.selecte_list[uIEditWorldMap.selecte_list.Count - 1];
            if (city == null || city.BelongForce != 0)
                return null;
            return city;
        }

        List<PersonLib> GetGovernorCandidates()
        {
            List<PersonLib> result = new List<PersonLib>();
            var addData = UIScenarioAddonMenu.AddData;

            // 已登场且未担任其他新势力君主的武将
            HashSet<int> governorIds = new HashSet<int>();
            for (int i = 0; i < addData.NewForces.Count; i++)
            {
                NewForceData nf = addData.NewForces[i];
                if (nf != null && nf.Governor != null)
                    governorIds.Add(nf.Governor.Id);
            }

            for (int i = 0; i < addData.AppearedPersonLibs.Count; i++)
            {
                PersonLib person = addData.AppearedPersonLibs[i];
                if (person != null && !governorIds.Contains(person.Id))
                    result.Add(person);
            }

            return result;
        }

        Flag GetNextAvailableFlag()
        {
            for (int i = 0; i < availableFlags.Count; i++)
            {
                if (!usedFlagIndices.Contains(i))
                    return availableFlags[i];
            }
            return null;
        }

        int GetNewForceId()
        {
            int maxId = 0;
            foreach (var kv in scenario.forceSet)
            {
                if (kv.Key > maxId)
                    maxId = kv.Key;
            }
            var addData = UIScenarioAddonMenu.AddData;
            for (int i = 0; i < addData.NewForces.Count; i++)
            {
                if (addData.NewForces[i].ForceId > maxId)
                    maxId = addData.NewForces[i].ForceId;
            }
            return maxId + 1;
        }

        Title GetTitle(int seed)
        {
            if (commonData == null || commonData.Titles == null)
                return null;
            List<Title> titles = new List<Title>();
            commonData.Titles.ForEach(x => { if (x != null) titles.Add(x); });
            if (titles.Count == 0)
                return null;
            return titles[(seed - 1) % titles.Count];
        }

        void WriteForceToScenario(int forceId, string forceName, PersonLib governor, Flag flag)
        {
            if (scenario.forceSet.ContainsKey(forceId))
                return;
            ShortForce force = new ShortForce();
            force.Id = forceId;
            force.Name = forceName;
            force.Governor = governor != null ? governor.Id : 0;
            force.Counsellor = 0;
            force.Flag = flag != null ? flag.Id : 0;
            force.desc = "新武将势力";
            scenario.forceSet[forceId] = force;
        }

        void WritePersonToScenario(PersonLib person, int forceId, int cityId)
        {
            if (person == null)
                return;
            if (scenario.personSet.TryGetValue(person.Id, out ShortPerson shortPerson))
            {
                shortPerson.BelongForce = forceId;
                shortPerson.BelongCity = cityId;
            }
            else
            {
                ShortPerson newPerson = new ShortPerson();
                newPerson.Id = person.Id;
                newPerson.Name = person.Name;
                newPerson.BelongForce = forceId;
                newPerson.BelongCity = cityId;
                newPerson.headIconID = person.headIconID;
                newPerson.imageID = person.imageID;
                scenario.personSet[person.Id] = newPerson;
            }
        }

        #endregion

        #region 按钮事件

        void BindButtonEvents()
        {
            if (eventsBound)
                return;
            eventsBound = true;

            BindEvent(returnBtn, OnReturn);
            BindEvent(confirmBtn, OnConfirm);
        }

        void BindEvent(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        /// <summary>
        /// 返回：关闭本窗口，回到 AddonMenu
        /// </summary>
        public void OnReturn()
        {
            Window.Instance.Close("window_scenario_create_new_force");
            // 不重新打开 addon_menu，它应该还在下面
        }

        /// <summary>
        /// 确认：关闭本窗口，回到 AddonMenu 并刷新
        /// </summary>
        public void OnConfirm()
        {
            Window.Instance.Close("window_scenario_create_new_force");
        }

        #endregion
    }
}
