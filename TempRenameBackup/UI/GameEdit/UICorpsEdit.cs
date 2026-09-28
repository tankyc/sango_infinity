using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 军团编辑窗口 - 编辑军团的信息
    /// 可编辑属性: 军团长、军团城池(添加/移除)、军团编号
    /// 关联窗口: window_edit_corps
    /// 使用快照模式: 军团长与编号修改仅作用于快照,确认时才写入Target
    /// 城市添加/移除属于结构性操作,即时生效
    /// </summary>
    public class UICorpsEdit : UGUIWindow
    {
        #region UI组件引用
        /// <summary>
        /// 军团名称标签
        /// </summary>
        public Text corpsNameText;

        /// <summary>
        /// 所属势力名称标签
        /// </summary>
        public Text forceNameText;

        /// <summary>
        /// 军团长选择下拉框(候选为本势力武将)
        /// </summary>
        public Dropdown comanderDropdown;

        /// <summary>
        /// 军团编号输入框(范围1-8)
        /// </summary>
        public InputField numberInput;

        /// <summary>
        /// 军团城市列表
        /// </summary>
        public UIObjectList cityList;

        /// <summary>
        /// 添加城市选择下拉框(候选为本势力未入团城市)
        /// </summary>
        public Dropdown assignCityDropdown;

        /// <summary>
        /// 添加城市按钮
        /// </summary>
        public Button assignCityButton;

        /// <summary>
        /// 移除选中城市按钮
        /// </summary>
        public Button removeCityButton;

        /// <summary>
        /// 确认按钮 - 保存修改
        /// </summary>
        public Button confirmButton;

        /// <summary>
        /// 取消按钮 - 放弃修改
        /// </summary>
        public Button cancelButton;
        #endregion

        /// <summary>
        /// 目标军团(原始对象,仅在确认时写入)
        /// </summary>
        public Corps Target { get; private set; }

        /// <summary>
        /// 军团长Id快照
        /// </summary>
        private int snapshotComanderId;

        /// <summary>
        /// 军团编号快照
        /// </summary>
        private int snapshotNumber;

        /// <summary>
        /// 触发刷新时的标识 - 防止OnValueChanged循环触发
        /// </summary>
        private bool refreshing;

        /// <summary>
        /// 军团长候选列表(本势力武将)
        /// </summary>
        private List<SangoObject> comanderCandidates;

        /// <summary>
        /// 军团城市列表数据
        /// </summary>
        private List<SangoObject> cityDatas;

        /// <summary>
        /// 可分配城市候选列表(本势力未入团城市)
        /// </summary>
        private List<City> assignableCities;

        /// <summary>
        /// 当前选中的城市
        /// </summary>
        private City selectedCity;

        #region 窗口生命周期
        /// <summary>
        /// 窗口打开 - 接收目标军团并创建编辑快照
        /// </summary>
        /// <param name="objects">参数列表 - objects[0] 为 Corps</param>
        public override void OnOpen(params object[] objects)
        {
            if (objects == null || objects.Length == 0 || !(objects[0] is Corps))
            {
                Log.Error("UICorpsEdit.OnOpen 传入的对象不是 Corps 类型");
                return;
            }
            Target = objects[0] as Corps;

            // 创建快照
            snapshotComanderId = Target.Comander;
            snapshotNumber = Target.number;

            comanderCandidates = new List<SangoObject>();
            cityDatas = new List<SangoObject>();
            RefreshCityDatas();
            cityList.Init(cityDatas, CitySortFunction.SortByName, OnSelectCity);
            if (cityDatas.Count > 0)
            {
                cityList.SelectDefaultObject(cityDatas[0]);
            }

            BindEvents();
            Refresh();
        }

        /// <summary>
        /// 窗口关闭 - 清理监听器和引用
        /// </summary>
        public override void OnClose()
        {
            base.OnClose();
            RemoveListeners();
            Target = null;
            selectedCity = null;
            comanderCandidates = null;
            cityDatas = null;
            assignableCities = null;
        }
        #endregion

        #region 数据构建
        /// <summary>
        /// 刷新军团城市列表数据 - 城市所属军团为本军团
        /// </summary>
        private void RefreshCityDatas()
        {
            if (cityDatas == null)
            {
                return;
            }
            cityDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario == null || scenario.citySet == null)
            {
                return;
            }
            scenario.citySet.ForEach(city =>
            {
                if (city != null && city.mBelongCorps == Target)
                {
                    cityDatas.Add(city);
                }
            });
        }

        /// <summary>
        /// 刷新可分配城市候选列表 - 本势力且未归入本军团的城市
        /// </summary>
        private void RefreshAssignableCities()
        {
            assignableCities = new List<City>();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario == null || scenario.citySet == null)
            {
                return;
            }
            scenario.citySet.ForEach(city =>
            {
                if (city != null && city.mBelongForce == Target.mBelongForce && city.mBelongCorps != Target)
                {
                    assignableCities.Add(city);
                }
            });
        }

        /// <summary>
        /// 刷新军团长候选列表 - 本势力武将
        /// </summary>
        private void RefreshComanderCandidates()
        {
            if (comanderCandidates == null)
            {
                return;
            }
            comanderCandidates.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario == null || scenario.personSet == null)
            {
                return;
            }
            scenario.personSet.ForEach(person =>
            {
                if (person != null && person.mBelongForce == Target.mBelongForce)
                {
                    comanderCandidates.Add(person);
                }
            });
        }
        #endregion

        #region 事件绑定
        /// <summary>
        /// 绑定UI事件 - 所有修改操作直接作用于快照
        /// </summary>
        private void BindEvents()
        {
            if (comanderDropdown != null)
            {
                comanderDropdown.onValueChanged.AddListener(OnComanderChanged);
            }
            if (numberInput != null)
            {
                numberInput.onEndEdit.AddListener(OnNumberEndEdit);
            }
            if (assignCityButton != null) assignCityButton.onClick.AddListener(OnAssignCityClick);
            if (removeCityButton != null) removeCityButton.onClick.AddListener(OnRemoveCityClick);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClick);
        }

        /// <summary>
        /// 清理所有监听器
        /// </summary>
        private void RemoveListeners()
        {
            if (comanderDropdown != null)
            {
                comanderDropdown.onValueChanged.RemoveListener(OnComanderChanged);
            }
            if (numberInput != null)
            {
                numberInput.onEndEdit.RemoveListener(OnNumberEndEdit);
            }
            if (assignCityButton != null) assignCityButton.onClick.RemoveListener(OnAssignCityClick);
            if (removeCityButton != null) removeCityButton.onClick.RemoveListener(OnRemoveCityClick);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClick);
        }

        /// <summary>
        /// 城市列表选中回调
        /// </summary>
        /// <param name="index">选中索引</param>
        private void OnSelectCity(int index)
        {
            if (cityDatas == null || index < 0 || index >= cityDatas.Count)
            {
                selectedCity = null;
                return;
            }
            selectedCity = cityDatas[index] as City;
        }
        #endregion

        #region UI刷新
        /// <summary>
        /// 刷新窗口 - 将快照当前值同步到UI
        /// </summary>
        public override void OnRefresh()
        {
            if (Target == null)
            {
                return;
            }

            refreshing = true;
            try
            {
                if (corpsNameText != null) corpsNameText.text = Target.Name;
                if (forceNameText != null)
                {
                    forceNameText.text = Target.mBelongForce != null ? Target.mBelongForce.Name : "无";
                }

                // 军团长下拉 - 候选为本势力武将,首位为"无"
                RefreshComanderCandidates();
                RefreshComanderDropdown();

                // 军团编号
                if (numberInput != null)
                {
                    numberInput.text = snapshotNumber.ToString();
                }

                // 军团城市列表
                RefreshCityDatas();
                cityList.Init(cityDatas, CitySortFunction.SortByName, OnSelectCity);
                if (selectedCity != null)
                {
                    cityList.SelectDefaultObject(selectedCity);
                }

                // 添加城市下拉
                RefreshAssignCityDropdown();
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// 刷新军团长下拉框 - 根据快照选中当前军团长
        /// </summary>
        private void RefreshComanderDropdown()
        {
            if (comanderDropdown == null)
            {
                return;
            }
            refreshing = true;
            comanderDropdown.ClearOptions();
            comanderDropdown.options.Add(new Dropdown.OptionData("无"));
            int selectIndex = 0;
            for (int i = 0; i < comanderCandidates.Count; i++)
            {
                Person person = comanderCandidates[i] as Person;
                comanderDropdown.options.Add(new Dropdown.OptionData(person != null ? person.Name : ""));
                if (person != null && person.Id == snapshotComanderId)
                {
                    selectIndex = i + 1;
                }
            }
            comanderDropdown.value = selectIndex;
            comanderDropdown.RefreshShownValue();
            refreshing = false;
        }

        /// <summary>
        /// 刷新添加城市下拉框 - 候选为本势力未入团城市
        /// </summary>
        private void RefreshAssignCityDropdown()
        {
            if (assignCityDropdown == null)
            {
                return;
            }
            refreshing = true;
            RefreshAssignableCities();
            assignCityDropdown.ClearOptions();
            if (assignableCities.Count == 0)
            {
                assignCityDropdown.options.Add(new Dropdown.OptionData("无可用城市"));
            }
            else
            {
                for (int i = 0; i < assignableCities.Count; i++)
                {
                    assignCityDropdown.options.Add(new Dropdown.OptionData(assignableCities[i].Name));
                }
            }
            assignCityDropdown.value = 0;
            assignCityDropdown.RefreshShownValue();
            refreshing = false;
        }
        #endregion

        #region 事件处理
        /// <summary>
        /// 军团长下拉改变 - 写入快照
        /// </summary>
        /// <param name="value">选中索引(0为无)</param>
        private void OnComanderChanged(int value)
        {
            if (refreshing)
            {
                return;
            }
            if (value == 0)
            {
                snapshotComanderId = 0;
                return;
            }
            int index = value - 1;
            if (comanderCandidates != null && index >= 0 && index < comanderCandidates.Count)
            {
                Person person = comanderCandidates[index] as Person;
                snapshotComanderId = person != null ? person.Id : 0;
            }
        }

        /// <summary>
        /// 军团编号输入结束 - 验证后写入快照(范围1-8)
        /// </summary>
        /// <param name="text">输入文本</param>
        private void OnNumberEndEdit(string text)
        {
            if (int.TryParse(text, out int v))
            {
                v = System.Math.Max(1, System.Math.Min(8, v));
                snapshotNumber = v;
            }
            if (numberInput != null)
            {
                numberInput.text = snapshotNumber.ToString();
            }
        }

        /// <summary>
        /// 添加城市按钮 - 将下拉框中选中的城市分配给军团
        /// </summary>
        private void OnAssignCityClick()
        {
            if (assignableCities == null || assignableCities.Count == 0)
            {
                Log.Warning("没有可分配的城市");
                return;
            }
            int index = assignCityDropdown.value;
            if (index < 0 || index >= assignableCities.Count)
            {
                return;
            }
            CorpsEdit edit = GameSystem.GetSystem<CorpsEdit>();
            if (edit != null)
            {
                edit.AssignCity(assignableCities[index]);
            }
            Refresh();
        }

        /// <summary>
        /// 移除城市按钮 - 将选中的城市从军团移除(仍属于势力)
        /// </summary>
        private void OnRemoveCityClick()
        {
            if (selectedCity == null)
            {
                Log.Warning("请先在列表中选中要移除的城市");
                return;
            }
            CorpsEdit edit = GameSystem.GetSystem<CorpsEdit>();
            if (edit != null)
            {
                edit.RemoveCity(selectedCity);
            }
            selectedCity = null;
            Refresh();
        }

        /// <summary>
        /// 确认按钮 - 将快照数据同步到Target并关闭窗口
        /// </summary>
        private void OnConfirmClick()
        {
            if (Target == null)
            {
                return;
            }
            CorpsEdit edit = GameSystem.GetSystem<CorpsEdit>();
            if (edit != null)
            {
                // 军团长
                Person comander = null;
                if (snapshotComanderId > 0)
                {
                    ScenarioEdit scenarioEdit = GameSystem.GetSystem<ScenarioEdit>();
                    if (scenarioEdit != null && scenarioEdit.Scenario != null)
                    {
                        comander = scenarioEdit.Scenario.personSet.Get(snapshotComanderId);
                    }
                }
                edit.SetComander(comander);
                edit.SetNumber(snapshotNumber);
            }
            Log.Info("保存军团编辑: " + Target.Name);
            GameSystem.GetSystem<CorpsEdit>()?.Back();
        }

        /// <summary>
        /// 取消按钮 - 放弃修改,直接关闭窗口
        /// 注: 城市添加/移除操作已即时生效
        /// </summary>
        private void OnCancelClick()
        {
            Log.Info("取消军团编辑: " + (Target != null ? Target.Name : ""));
            GameSystem.GetSystem<CorpsEdit>()?.Back();
        }
        #endregion
    }
}
