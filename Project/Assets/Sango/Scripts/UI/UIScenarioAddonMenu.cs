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

        /// <summary>
        /// 新武将登场附加数据,供后续功能界面使用。
        /// 登场武将来源于 GameCustomEdit.ModScenarioAddon / SelfScenarioAddon。
        /// </summary>
        public static ScenarioPersonAddData AddData = new ScenarioPersonAddData();

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
        bool createForceCloseBound;

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
            AddData = new ScenarioPersonAddData();
            inited = false;

            // 右侧地图: 初始化
            if (uIEditWorldMap != null)
            {
                uIEditWorldMap.SetScenario(scenario);
                uIEditWorldMap.RefreshCity();
            }

            RefreshInfo();
        }

        #region 登场武将

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

            GameSystemManager.Instance.GetSystem<EditPersonSelectSystem>().Start(
                persons,
                AddData.AppearedPersonLibs,
                persons.Count,
                OnAppearedPersonsSelected,
                PersonLibSortFunction.DefaultSortList,
                "登场武将");
        }

        void OnAppearedPersonsSelected(List<PersonLib> list)
        {
            AddData.AppearedPersonLibs.Clear();
            AddData.UnassignedPersonLibs.Clear();
            if (list != null)
            {
                AddData.AppearedPersonLibs.AddRange(list);
                AddData.UnassignedPersonLibs.AddRange(list);
            }
            AddData.UpdateStandbyList();
            RefreshInfo();
        }

        #endregion

        #region 建立新势力

        /// <summary>
        /// 建立新势力：打开 window_scenario_create_new_force 界面,并隐藏指定节点
        /// </summary>
        public void OnCreateNewForce()
        {
            if (AddData.AppearedCount == 0)
            {
                Debug.Log("请先选择登场武将,再建立新势力");
                return;
            }

            // 打开子界面前隐藏指定节点
            SetHideNodeActive(false);

            UIScenarioCreateNewForce createForceWin =
                Window.Instance.Open<UIScenarioCreateNewForce>("window_scenario_create_new_force", uIEditWorldMap);
            if (createForceWin == null)
            {
                // 打开失败,立即恢复节点显示
                SetHideNodeActive(true);
                return;
            }

            // 子界面关闭(返回)后恢复节点显示,并刷新数量(仅订阅一次)
            if (!createForceCloseBound)
            {
                createForceCloseBound = true;
                Action prev = createForceWin.OnCloseAction;
                createForceWin.OnCloseAction = () =>
                {
                    prev?.Invoke();
                    SetHideNodeActive(true);
                    RefreshInfo();
                };
            }
        }

        /// <summary>
        /// 设置隐藏节点的显示状态
        /// </summary>
        void SetHideNodeActive(bool active)
        {
            if (hideNodeOnCreateForce != null)
                hideNodeOnCreateForce.SetActive(active);
        }

        #endregion

        #region 配属新武将

        /// <summary>
        /// 配属新武将(配属到最近建立的新势力)
        /// </summary>
        public void OnAssignPersons()
        {
            if (AddData.AppearedCount == 0)
            {
                Debug.Log("请先选择登场武将,再配属新武将");
                return;
            }
            if (AddData.NewForceCount == 0)
            {
                Debug.Log("请先建立新势力,再配属新武将");
                return;
            }

            List<PersonLib> standby = new List<PersonLib>(AddData.StandbyPersonLibs);
            if (standby.Count == 0)
            {
                Debug.Log("所有登场武将都已配属");
                return;
            }

            GameSystemManager.Instance.GetSystem<EditPersonSelectSystem>().Start(
                standby,
                new List<PersonLib>(),
                standby.Count,
                OnAssignPersonsSelected,
                PersonLibSortFunction.DefaultSortList,
                "配属武将");
        }

        void OnAssignPersonsSelected(List<PersonLib> list)
        {
            if (list == null || list.Count == 0)
                return;

            // 配属到最近建立的新势力
            NewForceData target = AddData.NewForces[AddData.NewForces.Count - 1];
            for (int i = 0; i < list.Count; i++)
            {
                PersonLib person = list[i];
                if (person == null)
                    continue;

                AddData.AssignPersonToForce(person, target.ForceId);
                if (!target.Persons.Contains(person))
                    target.Persons.Add(person);

                WritePersonToScenario(person, target.ForceId, target.CapitalCity != null ? target.CapitalCity.Id : 0);
            }

            RefreshInfo();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化(将登场/新势力/配属数据写入剧本,并进入玩家势力选择)
        /// </summary>
        public void OnInitScenario()
        {
            if (AddData.AppearedCount == 0)
            {
                Debug.Log("请先选择登场武将,再初始化");
                return;
            }

            // 所有已登场武将写入 personSet(未配属 → 在野)
            for (int i = 0; i < AddData.AppearedPersonLibs.Count; i++)
            {
                PersonLib person = AddData.AppearedPersonLibs[i];
                if (person == null)
                    continue;

                int forceId = 0;
                int cityId = 0;
                if (AddData.AssignedPersons.TryGetValue(person.Id, out int assignedForceId))
                {
                    forceId = assignedForceId;
                    NewForceData force = AddData.NewForces.Find(x => x.ForceId == forceId);
                    if (force != null && force.CapitalCity != null)
                        cityId = force.CapitalCity.Id;
                }
                WritePersonToScenario(person, forceId, cityId);
            }

            // 所有新势力写入 forceSet,主城归属标记
            for (int i = 0; i < AddData.NewForces.Count; i++)
            {
                NewForceData force = AddData.NewForces[i];
                WriteForceToScenario(force.ForceId, force.ForceName, force.Governor, force.Flag);
                if (force.CapitalCity != null && scenario.citySet.TryGetValue(force.CapitalCity.Id, out ShortCity shortCity))
                {
                    shortCity.BelongForce = force.ForceId;
                }
            }

            inited = true;
            RefreshInfo();

            // 进入玩家势力选择
            Window.Instance.Close("window_scenario_addon_menu");
            Window.Instance.Open("window_scenario_force_select");
        }

        #endregion

        #region 返回 / 下一步

        public void OnReturn()
        {
            Window.Instance.Close("window_scenario_addon_menu");
            Window.Instance.Open("window_scenario_select");
        }

        public void OnNext()
        {
            OnInitScenario();
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
            BindEvent(initBtn, OnInitScenario);
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
            // 左下角 5 个数量显示
            SetCountText(appearedCountText, AddData.AppearedCount);
            SetCountText(newForceCountText, AddData.NewForceCount);
            SetCountText(assignedCountText, AddData.AssignedCount);
            SetCountText(unassignedCountText, AddData.UnassignedCount);
            SetCountText(standbyCountText, AddData.StandbyCount);

            // 按钮可用状态: 登场武将数量 > 0 时,建势力/配属/初始化才可点选
            bool canOperate = AddData.AppearedCount > 0;
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
