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
        bool hasChanged = false;
        /// <summary>
        /// 点击"新建"势力后打开编辑界面时需要隐藏、返回后恢复显示的节点(在 prefab 中手动指派)。
        /// </summary>
        public GameObject hideNodeOnEditForce;

        // ===== 数据 =====
        ShortScenario scenario;

        ScenarioCommonData commonData;
        CreatePool<UICreateForceItem> itemPool;
        List<UICreateForceItem> activeItems = new List<UICreateForceItem>();

        // 空白城市列表
        List<ShortCity> emptyCities = new List<ShortCity>();
        // 可用旗帜列表
        List<Flag> availableFlags = new List<Flag>();

        // 原本存在的势力列表
        List<ShortForce> exsisitForceList = new List<ShortForce>();
        List<ShortCity> exsisitCityList = new List<ShortCity>();

        protected override void Awake()
        {
            BindButtonEvents();
        }

        public override void OnOpen(params object[] args)
        {
            base.OnOpen(args);
            hasChanged = false;
            scenario = new ShortScenario();
            scenario.personSet = ShortScenario.CurSelected.personSet;
            scenario.CommonData = ShortScenario.CurSelected.CommonData;

            ShortScenario.CurSelected.citySet.ForEach(x =>
                scenario.citySet.Add(x.Copy())
            );
            ShortScenario.CurSelected.forceSet.ForEach(x =>
                scenario.forceSet.Add(x.Copy())
            );
            commonData = scenario.CommonData;
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
            //var addData = UIScenarioAddonMenu.AddData;
            //SetCountText(unassignedNewPersonCountText, addData.UnassignedCount);
            //SetCountText(assignedNewPersonCountText, addData.AssignedCount);
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

            // 收集空白城市（BelongForce == 0）
            if (scenario.citySet != null)
            {
                foreach (ShortCity city in scenario.citySet)
                {
                    if (city == null) continue;
                    exsisitCityList.Add(city.Copy());
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

            foreach (ShortForce shortForce in scenario.forceSet)
            {
                if (shortForce != null)
                {
                    exsisitForceList.Add(shortForce.Copy());
                    Flag flag = commonData.Flags.Get(shortForce.Flag);
                    availableFlags.Remove(flag);
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
            if (itemPool != null)
                itemPool.Reset();

            List<ShortForce> shortForces = new List<ShortForce>();
            scenario.forceSet.ForEach(x =>
            {
                if (x.IsAppend)
                {
                    shortForces.Add(x);
                }
            });

            // 1. 显示已建势力
            for (int i = 0; i < shortForces.Count; i++)
            {
                ShortForce force = shortForces[i];
                ShortPerson gov = scenario.personSet[force.Governor];
                Flag flag = scenario.CommonData.Flags.Get(force.Flag);
                ShortCity captain = scenario.citySet.Get(gov.BelongCity);
                UICreateForceItem item = itemPool.Create();
                if (item == null) continue;

                if (item.forceName != null)
                    item.forceName.text = gov.Name;
                if (item.cityName != null)
                    item.cityName.text = captain.Name;
                if (item.flagColor != null)
                    item.flagColor.color = flag.color;
                //if (item.btnName != null)
                //    item.btnName.text = "已建立";
                // 已建势力可删除
                if (item.cancelBtn != null)
                    item.cancelBtn.interactable = true;

                // 已建势力点击删除
                item.target = force;
                item.onClickDelete = OnDeleteForce;
                item.onClickNew = OnClickNewForceSlot;

                activeItems.Add(item);
            }

            // 2. 显示可新建槽位（剩余可建数量）
            int remaining = System.Math.Min(emptyCities.Count, availableFlags.Count);
            if (remaining > 0)
            {
                UICreateForceItem item = itemPool.Create();

                // 无指定势力：君主/本城文本置空,势力颜色为白色,删除按钮禁止点击
                if (item.forceName != null)
                    item.forceName.text = "";
                if (item.cityName != null)
                    item.cityName.text = "";
                if (item.flagColor != null)
                    item.flagColor.color = Color.white;
                if (item.btnName != null)
                    item.btnName.text = "修改";
                if (item.cancelBtn != null)
                    item.cancelBtn.interactable = false;

                // 未建势力点击新建
                item.onClickNew = OnClickNewForceSlot;
                item.onClickDelete = null;

                activeItems.Add(item);
            }
        }
        #endregion

        #region 新建势力流程
        /// <summary>
        /// 点击"新建"按钮：选择城市 → 选择君主 → 打开编辑界面
        /// </summary>
        void OnClickNewForceSlot(ShortForce targetForce)
        {
            // 新建势力
            if (targetForce == null)
            {
                targetForce = new ShortForce();
                targetForce.IsAppend = true;
                targetForce.Flag = scenario.FindEmptyFlag();
                scenario.forceSet.Add(targetForce);
            }

            SetHideNodeActive(false);
            Window.Instance.Open("window_scenario_edit_new_force", uIEditWorldMap, targetForce.Id, scenario, commonData).ugui_instance.OnCloseAction = () =>
            {
                SetHideNodeActive(true);

                // 表明未真正使用
                if (targetForce.Governor == 0)
                {
                    scenario.forceSet.Remove(targetForce);
                }
                hasChanged = true;

                // 如果编辑界面确认建立了势力，刷新本界面数据
                RefreshMap();
                RefreshForceItems();
                RefreshInfo();
            };
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
        /// 删除已建势力
        /// </summary>
        void OnDeleteForce(ShortForce targetForce)
        {
            scenario.forceSet.Remove(targetForce);
            scenario.citySet[targetForce.CapitalCity].BelongForce = 0;
            scenario.personSet[targetForce.Governor].BelongForce = 0;
            scenario.personSet[targetForce.Governor].BelongCity = 0;
            hasChanged = true;

            RefreshMap();
            RefreshForceItems();
            RefreshInfo();

        }

        #endregion

        #region 按钮事件

        void BindButtonEvents()
        {
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
            if (!hasChanged)
            {
                Window.Instance.Close("window_scenario_create_new_force");
                return;
            }

            string content = $"资料尚未储存,返回将丢失所有更改,确定吗?";
            GameDialog.Open(content, () =>
            {
                GameDialog.Close();
                // 清理修改
                scenario.forceSet.ForEach(x =>
                {
                    if (x.IsAppend)
                    {
                        scenario.citySet[x.CapitalCity].BelongForce = 0;
                        scenario.personSet[x.Governor].BelongForce = 0;
                        scenario.personSet[x.Governor].BelongCity = 0;
                        scenario.forceSet.Remove(x);
                    }
                });

                // 还原
                exsisitForceList.ForEach(x =>
                {
                    scenario.forceSet.Add(x);
                    scenario.citySet[x.CapitalCity].BelongForce = x.Id;
                    scenario.personSet[x.Governor].BelongForce = x.Id;
                    scenario.personSet[x.Governor].BelongCity = x.CapitalCity;
                });

                Window.Instance.Close("window_scenario_create_new_force");

            });
        }

        /// <summary>
        /// 确认：关闭本窗口，回到 AddonMenu 并刷新
        /// </summary>
        public void OnConfirm()
        {
            ShortScenario.CurSelected.citySet = scenario.citySet;
            ShortScenario.CurSelected.forceSet = scenario.forceSet;

            // 已配属武将顺理,在野武将无需管理
            scenario.personSet.ForEach(x =>
            {
                if (x.PersonLib != null)
                {
                    if (x.state == (int)PersonStateType.Normal)
                    {
                        ShortForce force = scenario.forceSet[x.BelongForce];
                        ShortCity city = scenario.citySet[x.BelongCity];
                        if (force == null || city == null || city.BelongForce != x.BelongForce)
                        {
                            x.state = 0;
                            x.BelongForce = 0;
                            x.BelongCity = 0;
                        }
                    }
                    else if (x.state == (int)PersonStateType.Governor)
                    {
                        ShortForce force = scenario.forceSet[x.BelongForce];
                        ShortCity city = scenario.citySet[x.BelongCity];
                        if (force == null || city == null || force.Governor != x.Id || city.BelongForce != x.BelongForce)
                        {
                            x.state = 0;
                            x.BelongForce = 0;
                            x.BelongCity = 0;
                        }
                    }
                }
            });

            Window.Instance.Close("window_scenario_create_new_force");
        }

        #endregion
    }
}
