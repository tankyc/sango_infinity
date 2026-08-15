using Sango;
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
    /// 剧本前"新武将登场"设置界面(三国志11风格)。
    /// 流程: 登场武将 → 建立新势力 → 配属新武将 → 初始化。
    /// 数据仅来自 ShortScenario.CurSelected 与 GameData.Instance.ScenarioCommonData,
    /// 不涉及任何 Scenario.Cur 相关数据; UI 关联全部在代码中手动处理,不读取任何 prefab。
    /// </summary>
    public class UIScenarioAddonMenu : UGUIWindow
    {
        /// <summary>右侧地图组件(已在 prefab 中关联)</summary>
        public UIEditWorldMap uIEditWorldMap;

        // 以下 UI 均为公共属性,请在 prefab 中手动指派并绑定事件
        public Button selectPersonBtn;   // 登场武将
        public Button createForceBtn;    // 建立新势力
        public Button assignBtn;         // 配属新武将
        public Button initBtn;           // 初始化
        public Button returnBtn;         // 返回
        public Button nextBtn;           // 下一步

        // 左下角 5 个数量显示(在 prefab 中手动绑定对应 Text 组件)
        public Text appearedCountText;   // 登场武将数
        public Text newForceCountText;   // 新势力数
        public Text assignedCountText;   // 已配属武将数
        public Text unassignedCountText; // 未配属武将数
        public Text standbyCountText;    // 待机武将数

        /// <summary>
        /// 打开"建立新势力"子界面时需要隐藏、返回后恢复显示的节点(在 prefab 中手动指派)。
        /// </summary>
        public GameObject hideNodeOnCreateForce;

        ShortScenario scenario;
        ScenarioCommonData commonData;
        bool inited;
        bool eventsBound;

        public override void OnOpen()
        {
            base.OnOpen();

            // 按钮对象在 prefab 中手动指派,事件自动绑定(仅绑定一次)
            BindButtonEvents();

            // 确保打开本界面时隐藏节点处于显示状态
            SetHideNodeActive(true);

            scenario = ShortScenario.CurSelected;

            if (scenario == null)
            {
                Debug.LogWarning("UIScenarioAddonMenu: ShortScenario.CurSelected 为空,无法打开新武将登场界面");
                Close();
                return;
            }
            // 加载剧本内容(幂等),填充 forceSet/personSet/citySet/Map/CommonData
            scenario.LoadFullPersonContent();
            commonData = scenario.CommonData != null ? scenario.CommonData : GameData.Instance.ScenarioCommonData;

            // 重置本次附加数据
            inited = false;

            //for(int i = 1; i <= 100; i++)
            //{
            //    if(!scenario.forceSet.ContainsKey(i))
            //        AddData.forceIndexList.Add(i);
            //}

            // 右侧地图: 初始化
            if (uIEditWorldMap != null)
            {
                uIEditWorldMap.SetScenario(scenario);
                uIEditWorldMap.RefreshCity();
            }

            RefreshInfo();
        }

        #region 登场武将


        List<PersonLib> LastSelected;

        /// <summary>
        /// 选择登场武将(从 ModScenarioAddon 与 SelfScenarioAddon 的自建武将库中)
        /// </summary>
        public void OnSelectAppearedPersons()
        {
            List<PersonLib> persons = new List<PersonLib>();
            if (GameCustomEdit.Instance != null)
            {
                CollectPersonLib(GameCustomEdit.Instance.ModScenarioAddon != null ? GameCustomEdit.Instance.ModScenarioAddon.PersonLibrary : null, persons);
                CollectPersonLib(GameCustomEdit.Instance.SelfScenarioAddon != null ? GameCustomEdit.Instance.SelfScenarioAddon.PersonLibrary : null, persons);
            }
            if (persons.Count == 0)
            {
                Debug.Log("没有可登场的自建武将,请先在自建武将界面中创建");
                return;
            }

            // 排除已经部署的
            persons.RemoveAll(x => x.targetShortPerson != null && x.targetShortPerson.BelongCity > 0);

            LastSelected = persons.FindAll(x => x.targetShortPerson != null);

            GameSystemManager.Instance.GetSystem<EditPersonSelectSystem>().Start(
                persons,
                LastSelected,
                persons.Count,
                OnAppearedPersonsSelected,
                PersonLibSortFunction.DefaultSortList,
                "登场武将");
        }

        void OnAppearedPersonsSelected(List<PersonLib> list)
        {
            ShortScenario shortScenario = ShortScenario.CurSelected;
            LastSelected.ForEach(x =>
            {
                if(!list.Contains(x))
                {
                    shortScenario.personSet.Remove(x.targetShortPerson);
                    x.targetShortPerson = null;
                }
            });

            // 只增量添加
            for (int i = 0; i < list.Count; i++)
            {
                PersonLib personLib = list[i];
                if (personLib.targetShortPerson == null)
                {
                    ShortPerson shortPerson = ShortPerson.FormLib(personLib);
                    shortScenario.personSet.Add(shortPerson);
                    personLib.targetShortPerson = shortPerson;
                }
            }

            shortScenario.NeedUpdateAppendInfo();
            RefreshInfo();
        }

        #endregion

        #region 建立新势力

        /// <summary>
        /// 建立新势力：打开 window_scenario_create_new_force 界面,并隐藏指定节点
        /// </summary>
        public void OnCreateNewForce()
        {
            // 打开子界面前隐藏指定节点
            SetHideNodeActive(false);
            Window.Instance.Open("window_scenario_create_new_force", uIEditWorldMap).ugui_instance.OnCloseAction = () =>
            {
                SetHideNodeActive(true);
            };
        }

        /// <summary>
        /// 设置隐藏节点的显示状态
        /// </summary>
        void SetHideNodeActive(bool active)
        {
            if (hideNodeOnCreateForce != null)
                hideNodeOnCreateForce.SetActive(active);

            if (active)
            {
                ShortScenario.CurSelected.NeedUpdateAppendInfo();
                RefreshInfo();
            }
        }

        #endregion

        #region 配属新武将

        /// <summary>
        /// 配属新武将(配属到最近建立的新势力)
        /// </summary>
        public void OnAssignPersons()
        {
            // 打开子界面前隐藏指定节点
            SetHideNodeActive(false);

            Window.Instance.Open("window_scenario_edit_select_city", uIEditWorldMap, "AssignPerson").ugui_instance.
                OnCloseAction = () =>
                {
                    SetHideNodeActive(true);
                };
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化(将登场/新势力/配属数据写入剧本,并进入玩家势力选择)
        /// </summary>
        public void OnResetScenario()
        {
            string content = $"初始化将删除所有已登场武将和新作势力,确定吗?";
            GameDialog.Open(content, () =>
            {
                GameDialog.Close();
                ShortScenario.CurSelected.RemoveAllAppendData();
                createForceBtn.interactable = false;
                assignBtn.interactable = false;
                initBtn.interactable = false;
                RefreshInfo();
            });
        }

        #endregion

        #region 返回 / 下一步

        public void OnReturn()
        {
            if (ShortScenario.CurSelected.AppendForceCount > 0)
            {
                string content = $"返回将丢失所有已登场武将和新作势力,确定吗?";
                GameDialog.Open(content, () =>
                {
                    GameDialog.Close();
                    ShortScenario.CurSelected.RemoveAllAppendData();
                    Window.Instance.Close("window_scenario_addon_menu");
                    Window.Instance.Open("window_scenario_select");
                });
            }
            else
            {
                Window.Instance.Close("window_scenario_addon_menu");
                Window.Instance.Open("window_scenario_select");
            }
        }

        public void OnNext()
        {
            // 进入玩家势力选择
            Window.Instance.Close("window_scenario_addon_menu");
            Window.Instance.Open("window_scenario_force_select");
        }

        #endregion

        #region 数据处理

        void CollectPersonLib(SangoObjectSet<PersonLib> set, List<PersonLib> dest)
        {
            if (set == null)
                return;
            set.ForEach(x =>
            {
                if (x != null && !dest.Contains(x))
                    dest.Add(x);
            });
        }

        ShortCity GetSelectedEmptyCity()
        {
            if (uIEditWorldMap == null || uIEditWorldMap.selecte_list == null || uIEditWorldMap.selecte_list.Count == 0)
                return null;
            ShortCity city = uIEditWorldMap.selecte_list[uIEditWorldMap.selecte_list.Count - 1];
            if (city == null || city.BelongForce != 0)
                return null;
            return city;
        }

        #endregion

        #region UI 关联与信息展示

        /// <summary>
        /// 自动绑定按钮事件: 按钮对象已在 prefab 中手动指派,
        /// 此处统一为其 onClick 挂载对应方法(仅绑定一次)。
        /// </summary>
        void BindButtonEvents()
        {
            if (eventsBound)
                return;
            eventsBound = true;

            BindEvent(selectPersonBtn, OnSelectAppearedPersons);
            BindEvent(createForceBtn, OnCreateNewForce);
            BindEvent(assignBtn, OnAssignPersons);
            BindEvent(initBtn, OnResetScenario);
            BindEvent(returnBtn, OnReturn);
            BindEvent(nextBtn, OnNext);
        }

        void BindEvent(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        void RefreshInfo()
        {
            ShortScenario shortScenario = ShortScenario.CurSelected;
            // 左下角 5 个数量显示
            SetCountText(appearedCountText, shortScenario.AppendPersonCount);
            SetCountText(newForceCountText, shortScenario.AppendForceCount);
            SetCountText(assignedCountText, shortScenario.AssignedPersonCount);
            SetCountText(unassignedCountText, shortScenario.AppendPersonCount - shortScenario.AssignedPersonCount);
            //SetCountText(standbyCountText, AddData.StandbyCount);

            // 按钮可用状态: 登场武将数量 > 0 时,建势力/配属/初始化才可点选
            bool canOperate = shortScenario.AppendPersonCount > 0;
            SetButtonInteractable(createForceBtn, canOperate);
            SetButtonInteractable(assignBtn, canOperate);
            SetButtonInteractable(initBtn, canOperate);
        }

        void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        void SetCountText(Text text, int count)
        {
            if (text != null)
                text.text = count.ToString();
        }

        #endregion
    }
}
