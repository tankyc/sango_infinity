using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 势力编辑窗口 - 编辑势力的属性
    /// 可编辑属性: 势力颜色(旗帜)、势力城市(添加/移除)
    /// 关联窗口: window_edit_force
    /// 使用快照模式: 旗帜颜色修改仅作用于快照,确认时才写入Target
    /// 城市添加/移除属于结构性操作,即时生效
    /// </summary>
    public class UIForceEdit : UGUIWindow
    {
        #region UI组件引用
        /// <summary>
        /// 势力名称标签
        /// </summary>
        public Text forceNameText;

        /// <summary>
        /// 君主名称标签
        /// </summary>
        public Text governorNameText;

        /// <summary>
        /// 势力颜色显示
        /// </summary>
        public Image flagImage;

        /// <summary>
        /// 切换势力颜色按钮
        /// </summary>
        public Button changeFlagButton;

        /// <summary>
        /// 势力城市列表
        /// </summary>
        public UIObjectList cityList;

        /// <summary>
        /// 添加城市选择下拉框(候选为无势力城市)
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
        /// 目标势力(原始对象,仅在确认时写入)
        /// </summary>
        public Force Target { get; private set; }

        /// <summary>
        /// 旗帜快照 - 界面操作仅修改快照值
        /// </summary>
        private Flag snapshotFlag;

        /// <summary>
        /// 触发刷新时的标识 - 防止OnValueChanged循环触发
        /// </summary>
        private bool refreshing;

        /// <summary>
        /// 势力城市列表数据
        /// </summary>
        private List<SangoObject> cityDatas;

        /// <summary>
        /// 旗帜候选列表(未被其他势力使用的旗帜)
        /// </summary>
        private List<Flag> flagCandidates;

        /// <summary>
        /// 无势力城市候选列表(用于添加城市)
        /// </summary>
        private List<City> freeCities;

        /// <summary>
        /// 当前选中的城市
        /// </summary>
        private City selectedCity;

        #region 窗口生命周期
        /// <summary>
        /// 窗口打开 - 接收目标势力并创建编辑快照
        /// </summary>
        /// <param name="objects">参数列表 - objects[0] 为 Force</param>
        public override void OnOpen(params object[] objects)
        {
            if (objects == null || objects.Length == 0 || !(objects[0] is Force))
            {
                Log.Error("UIForceEdit.OnOpen 传入的对象不是 Force 类型");
                return;
            }
            Target = objects[0] as Force;

            // 创建旗帜快照
            snapshotFlag = Target.mFlag;
            flagCandidates = BuildFlagCandidates();

            // 势力城市列表
            cityDatas = new List<SangoObject>();
            RefreshCityDatas();
            if (cityDatas.Count > 0)
            {
                cityList.Init(cityDatas, CitySortFunction.SortByName, OnSelectCity);
                cityList.SelectDefaultObject(cityDatas[0]);
            }
            else
            {
                cityList.Init(cityDatas, CitySortFunction.SortByName, OnSelectCity);
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
            cityDatas = null;
            flagCandidates = null;
            freeCities = null;
        }
        #endregion

        #region 数据构建
        /// <summary>
        /// 构建旗帜候选列表 - 未被其他势力使用的旗帜(包含当前势力的旗帜)
        /// </summary>
        /// <returns>旗帜候选列表</returns>
        private List<Flag> BuildFlagCandidates()
        {
            List<Flag> candidates = new List<Flag>();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Flags == null)
            {
                return candidates;
            }

            List<Flag> usedFlags = new List<Flag>();
            scenario.forceSet.ForEach(force =>
            {
                if (force != null && force != Target && force.mFlag != null)
                {
                    usedFlags.Add(force.mFlag);
                }
            });

            scenario.CommonData.Flags.ForEach(flag =>
            {
                if (flag != null && (!usedFlags.Contains(flag) || flag == Target.mFlag))
                {
                    candidates.Add(flag);
                }
            });
            return candidates;
        }

        /// <summary>
        /// 刷新势力城市列表数据 - 从剧本实时读取
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
                if (city != null && city.mBelongForce == Target)
                {
                    cityDatas.Add(city);
                }
            });
        }
        #endregion

        #region 事件绑定
        /// <summary>
        /// 绑定UI事件
        /// </summary>
        private void BindEvents()
        {
            if (changeFlagButton != null) changeFlagButton.onClick.AddListener(OnChangeFlagClick);
            if (assignCityButton != null) assignCityButton.onClick.AddListener(OnAssignCityClick);
            if (removeCityButton != null) removeCityButton.onClick.AddListener(OnRemoveCityClick);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClick);
        }

        /// <summary>
        /// 清理所有按钮监听器
        /// </summary>
        private void RemoveListeners()
        {
            if (changeFlagButton != null) changeFlagButton.onClick.RemoveListener(OnChangeFlagClick);
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
                // 势力名称 - 势力名为君主名
                if (forceNameText != null) forceNameText.text = Target.Name;
                if (governorNameText != null)
                {
                    governorNameText.text = Target.mGovernor != null ? Target.mGovernor.Name : "无";
                }

                // 势力颜色
                RefreshFlag();

                // 城市列表
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
        /// 刷新势力颜色显示
        /// </summary>
        private void RefreshFlag()
        {
            if (flagImage == null)
            {
                return;
            }
            flagImage.color = snapshotFlag != null ? snapshotFlag.color : new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        /// <summary>
        /// 刷新添加城市下拉框 - 候选为无势力城市
        /// </summary>
        private void RefreshAssignCityDropdown()
        {
            if (assignCityDropdown == null)
            {
                return;
            }
            refreshing = true;
            freeCities = new List<City>();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                freeCities = edit.GetFreeCities();
            }

            assignCityDropdown.ClearOptions();
            if (freeCities == null || freeCities.Count == 0)
            {
                assignCityDropdown.options.Add(new Dropdown.OptionData("无可用城市"));
            }
            else
            {
                for (int i = 0; i < freeCities.Count; i++)
                {
                    assignCityDropdown.options.Add(new Dropdown.OptionData(freeCities[i].Name));
                }
            }
            assignCityDropdown.value = 0;
            assignCityDropdown.RefreshShownValue();
            refreshing = false;
        }
        #endregion

        #region 事件处理
        /// <summary>
        /// 切换势力颜色按钮 - 循环切换到下一个可用旗帜
        /// </summary>
        private void OnChangeFlagClick()
        {
            if (flagCandidates == null || flagCandidates.Count == 0)
            {
                Log.Warning("没有可用的旗帜");
                return;
            }
            int currentIndex = -1;
            for (int i = 0; i < flagCandidates.Count; i++)
            {
                if (flagCandidates[i] == snapshotFlag)
                {
                    currentIndex = i;
                    break;
                }
            }
            int nextIndex = (currentIndex + 1) % flagCandidates.Count;
            snapshotFlag = flagCandidates[nextIndex];
            RefreshFlag();
        }

        /// <summary>
        /// 添加城市按钮 - 将下拉框中选中的无势力城市分配给势力
        /// </summary>
        private void OnAssignCityClick()
        {
            if (freeCities == null || freeCities.Count == 0)
            {
                Log.Warning("没有可分配的城市");
                return;
            }
            int index = assignCityDropdown.value;
            if (index < 0 || index >= freeCities.Count)
            {
                return;
            }
            ForceEdit edit = GameSystem.GetSystem<ForceEdit>();
            if (edit != null)
            {
                edit.AssignCity(freeCities[index]);
            }
            Refresh();
        }

        /// <summary>
        /// 移除城市按钮 - 将选中的城市从势力移除
        /// </summary>
        private void OnRemoveCityClick()
        {
            if (selectedCity == null)
            {
                Log.Warning("请先在列表中选中要移除的城市");
                return;
            }
            ForceEdit edit = GameSystem.GetSystem<ForceEdit>();
            if (edit != null)
            {
                edit.RemoveCity(selectedCity);
            }
            selectedCity = null;
            Refresh();
        }

        /// <summary>
        /// 确认按钮 - 将旗帜快照写入Target并关闭窗口
        /// </summary>
        private void OnConfirmClick()
        {
            if (Target == null)
            {
                return;
            }
            ForceEdit edit = GameSystem.GetSystem<ForceEdit>();
            if (edit != null && snapshotFlag != null && snapshotFlag != Target.mFlag)
            {
                edit.SetFlag(snapshotFlag);
            }
            Log.Info("保存势力编辑: " + Target.Name);
            GameSystem.GetSystem<ForceEdit>()?.Back();
        }

        /// <summary>
        /// 取消按钮 - 放弃旗帜修改,直接关闭窗口
        /// 注: 城市添加/移除操作已即时生效
        /// </summary>
        private void OnCancelClick()
        {
            Log.Info("取消势力编辑: " + (Target != null ? Target.Name : ""));
            GameSystem.GetSystem<ForceEdit>()?.Back();
        }
        #endregion
    }
}
