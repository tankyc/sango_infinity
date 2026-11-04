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
        ShortScenario src_scenario;

        ScenarioCommonData commonData;
        CreatePool<UICreateForceItem> itemPool;
        List<UICreateForceItem> activeItems = new List<UICreateForceItem>();

        // 空白城市列表
        List<ShortCity> emptyCities = new List<ShortCity>();
        // 可用旗帜列表
        List<Flag> availableFlags = new List<Flag>();

        protected override void Awake()
        {
            BindButtonEvents();
        }

        public override void OnOpen(params object[] args)
        {
            base.OnOpen(args);
            hasChanged = false;
            confirmBtn.interactable = false;

            // 从打开者接收 uIEditWorldMap（通过 args 或 public 字段）
            if (args != null && args.Length > 0 && args[0] is UIEditWorldMap map)
            {
                uIEditWorldMap = map;
            }

            src_scenario = (ShortScenario)args[1];
            scenario = src_scenario.Copy();
            commonData = src_scenario.CommonData;

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
            // scenario 为空说明界面正在随剧本一起销毁（返回主菜单），不再刷新
            if (scenario == null)
                return;

            // 已配属 = AssignedPersonCount，未配属 = 总数 - 已配属（对齐 UIScenarioAddonMenu 的同名统计口径）
            SetCountText(assignedNewPersonCountText, scenario.AssignedPersonCount);
            SetCountText(unassignedNewPersonCountText, scenario.AppendPersonCount - scenario.AssignedPersonCount);
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

            // 收集空白城市（BelongForceId == 0）
            if (scenario.citySet != null)
            {
                foreach (ShortCity city in scenario.citySet)
                {
                    if (city == null) continue;
                    if (city.Id != 0 && city.IsCity() && city.BelongForceId == 0)
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

            // 已建势力占用的旗帜不再可选
            foreach (ShortForce shortForce in scenario.forceSet)
            {
                if (shortForce == null)
                    continue;

                // 势力数据不完整(编辑器里可能有半成品势力)时跳过,不能让颜色表解析失败中断整个流程
                if (commonData == null || commonData.Flags == null)
                    continue;

                Flag flag = commonData.Flags.Get(shortForce.Flag);
                if (flag != null)
                    availableFlags.Remove(flag);
            }
        }

        #endregion

        #region 刷新地图

        void RefreshMap()
        {
            // scenario 为空说明界面正在随剧本一起销毁（返回主菜单），不再刷新地图
            if (uIEditWorldMap != null && scenario != null)
            {
                uIEditWorldMap.SetScenario(scenario);
                uIEditWorldMap.RefreshCity();
            }
        }

        #endregion

        #region 刷新势力列表（复用 UICreateForceItem）

        void RefreshForceItems()
        {
            // 模板/父节点没在 prefab 里指派时对象池建不起来,此时不显示列表,避免空引用
            if (itemPool == null)
                return;

            // 【关闭期保护】本方法会被编辑窗口的 OnCloseAction 回调（见 OnClickNewForceSlot）。
            // 返回主菜单时 Window.CloseAll 会逐个触发 OnCloseAction，此刻剧本可能已被释放
            //（实战崩栈：Player.QuitToMainMenu → Window.CloseAll → UGUIWindow.OnClose → 本方法）。
            // scenario / forceSet 任一为空都直接跳过，不要试图刷新一个正在销毁的界面。
            if (scenario == null || scenario.forceSet == null
                || scenario.personSet == null || scenario.citySet == null)
                return;

            itemPool.Reset();
            // 池已经回收了全部列表项,这里同步清掉引用,避免反复刷新时越积越多
            activeItems.Clear();

            List<ShortForce> shortForces = new List<ShortForce>();
            scenario.forceSet.ForEach(x =>
            {
                if (x != null && x.IsAppend)
                {
                    shortForces.Add(x);
                }
            });

            // 1. 显示已建势力
            for (int i = 0; i < shortForces.Count; i++)
            {
                ShortForce force = shortForces[i];

                // 本城以势力自身的 CapitalCity 为准:新建势力流程只保证这个字段有值,
                // 君主(武将)的 BelongCityId 可能被"取消配属城市"等操作清成 0 或指向已删除的城市。
                // 以前直接拿 gov.BelongCityId 去查城市,取到 null 后 captain.Name 就会空引用崩溃。
                ShortCity captain = force.CapitalCity > 0 ? scenario.citySet.Get(force.CapitalCity) : null;
                ShortPerson gov = force.Governor > 0 ? scenario.personSet.Get(force.Governor) : null;
                if (captain == null && gov != null && gov.BelongCityId > 0)
                    captain = scenario.citySet.Get(gov.BelongCityId);

                Flag flag = (commonData != null && commonData.Flags != null) ? commonData.Flags.Get(force.Flag) : null;

                UICreateForceItem item = itemPool.Create();
                if (item == null) continue;

                // 数据缺项时用占位符显示,保证列表能正常打开(不能用 null 去取 Name/color)
                if (item.forceName != null)
                    item.forceName.text = gov != null ? gov.Name : "-";
                if (item.cityName != null)
                    item.cityName.text = captain != null ? captain.Name : "-";
                if (item.flagColor != null)
                    item.flagColor.color = flag != null ? flag.color : Color.white;
                //if (item.btnName != null)
                //    item.btnName.text = "已建立";
                // 已建势力可删除
                if (item.cancelBtn != null)
                    item.cancelBtn.interactable = true;

                // 已建势力点击删除
                item.target = force;
                item.onClickDelete = OnDeleteForce;
                item.onClickNew = OnClickNewForceSlot;
                item.transform.SetAsLastSibling();
                activeItems.Add(item);
            }

            // 2. 显示可新建槽位（剩余可建数量）
            int remaining = System.Math.Min(emptyCities.Count, availableFlags.Count);
            if (remaining > 0)
            {
                UICreateForceItem item = itemPool.Create();
                if (item == null)
                    return;

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

                item.target = null;
                // 未建势力点击新建
                item.onClickNew = OnClickNewForceSlot;
                item.onClickDelete = null;
                item.transform.SetAsLastSibling();
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
            //if (targetForce == null)
            //{
            //    targetForce = new ShortForce();
            //    targetForce.IsAppend = true;
            //    targetForce.Flag = scenario.FindEmptyFlag();
            //    scenario.forceSet.Add(targetForce);
            //}

            SetHideNodeActive(false);
            Window.Instance.Open("window_scenario_edit_new_force", uIEditWorldMap, targetForce != null ? targetForce.Id : -1 , scenario, commonData).ugui_instance.OnCloseAction = () =>
            {
                // 【退出到主菜单】该回调会在 Window.CloseAll 时触发（不只是玩家点返回），
                // 那一刻剧本与界面都处于销毁流程中，继续刷新只会空引用。
                if (scenario == null || confirmBtn == null || !isActiveAndEnabled)
                    return;

                SetHideNodeActive(true);

                // 表明未真正使用
                //if (targetForce.Governor == 0)
                //{
                //    scenario.forceSet.Remove(targetForce);
                //}
                hasChanged = true;

                // 如果编辑界面确认建立了势力，刷新本界面数据
                RefreshMap();
                RefreshForceItems();
                RefreshInfo();

                confirmBtn.interactable = true;
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
            if (targetForce == null)
                return;

            scenario.forceSet.Remove(targetForce);

            // 势力数据可能不完整(君主/本城未填或已失效),逐个判空,不能直接下标取值
            ShortCity capital = targetForce.CapitalCity > 0 ? scenario.citySet.Get(targetForce.CapitalCity) : null;
            if (capital != null)
                capital.BelongForceId = 0;

            ShortPerson governor = targetForce.Governor > 0 ? scenario.personSet.Get(targetForce.Governor) : null;
            if (governor != null)
            {
                governor.BelongForceId = 0;
                governor.BelongCityId = 0;
            }
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
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, content, () =>
            {
                Window.Instance.Close("window_scenario_create_new_force");
            });
        }

        /// <summary>
        /// 确认：关闭本窗口，回到 AddonMenu 并刷新
        /// </summary>
        public void OnConfirm()
        {
            src_scenario.citySet = scenario.citySet;
            src_scenario.forceSet = scenario.forceSet;
            src_scenario.personSet = scenario.personSet;
            Window.Instance.Close("window_scenario_create_new_force");
        }

        #endregion
    }
}
