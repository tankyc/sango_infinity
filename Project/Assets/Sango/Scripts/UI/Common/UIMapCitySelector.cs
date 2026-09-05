using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 地图城市选择器窗口。
    /// 与 CitySelectSystem 互相切换，选择范围与 CitySelectSystem 当前数据集保持一致。
    /// </summary>
    public class UIMapCitySelector : UGUIWindow
    {
        /// <summary>地图城市选择组件</summary>
        public UISelectCityWorldMap uISelectCityWorldMap;

        /// <summary>返回城市列表选择器的按钮</summary>
        public Button toSelector;

        /// <summary>确定按钮</summary>
        public Button confirmButton;

        /// <summary>取消按钮</summary>
        public Button cancelButton;

        /// <summary>当前绑定的城市选择系统</summary>
        private CitySelectSystem citySelectSystem;

        protected override void Awake()
        {
            base.Awake();
            if (toSelector != null)
                toSelector.onClick.AddListener(OnToSelectorClick);
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClick);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClick);
        }

        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            citySelectSystem = objects != null && objects.Length > 0 ? objects[0] as CitySelectSystem : null;
            if (citySelectSystem == null || uISelectCityWorldMap == null)
            {
                Log.Error("UIMapCitySelector 打开参数错误：未传入 CitySelectSystem 或未绑定地图组件");
                return;
            }

            Scenario cur = Scenario.Cur;
            if (cur == null)
            {
                Log.Error("UIMapCitySelector 打开失败：当前剧本为空");
                return;
            }

            // 同步地图组件数据
            uISelectCityWorldMap.SetScenario(cur);
            uISelectCityWorldMap.SetSelectableCities(GetCityList(citySelectSystem.Objects));
            uISelectCityWorldMap.maxSelectCount = citySelectSystem.selectLimit;
            uISelectCityWorldMap.OnSelectCity = OnSelectCity;

            // 使用 CitySelectSystem 中已选城市初始化地图显示
            List<City> selected = GetCityList(citySelectSystem.selected);
            uISelectCityWorldMap.SetSelectAllCity(selected);

            // 点选模式（单选）下隐藏确定按钮
            if (confirmButton != null)
                confirmButton.gameObject.SetActive(!citySelectSystem.ClickMode);
        }

        /// <summary>
        /// 地图上的城市被点击/取消时回调。
        /// </summary>
        /// <param name="selected">当前选中的城市列表</param>
        private void OnSelectCity(List<City> selected)
        {
            if (citySelectSystem == null) return;

            citySelectSystem.selected.Clear();
            foreach (City city in selected)
            {
                if (city != null)
                    citySelectSystem.selected.Add(city);
            }

            // 单选模式下选中即确认并关闭窗口
            if (citySelectSystem.selectLimit == 1 && citySelectSystem.selected.Count > 0)
            {
                citySelectSystem.OnSure();
                Window.Instance.Close("window_map_city_selector");
            }
        }

        /// <summary>
        /// 返回城市列表选择器。
        /// </summary>
        private void OnToSelectorClick()
        {
            Window.Instance.Close("window_map_city_selector");
            if (citySelectSystem != null)
                Window.Instance.Open("window_object_selector", citySelectSystem);
        }

        /// <summary>
        /// 确定选择：将当前选中的城市提交并关闭窗口。
        /// </summary>
        private void OnConfirmClick()
        {
            if (citySelectSystem == null) return;
            citySelectSystem.OnSure();
            Window.Instance.Close("window_map_city_selector");
        }

        /// <summary>
        /// 取消选择：关闭窗口并取消本次选择。
        /// </summary>
        private void OnCancelClick()
        {
            if (citySelectSystem != null)
                citySelectSystem.OnCancel();
            Window.Instance.Close("window_map_city_selector");
        }

        /// <summary>
        /// 将 SangoObject 列表转换为 City 列表。
        /// </summary>
        /// <param name="objects">源对象列表</param>
        /// <returns>转换后的城市列表</returns>
        private List<City> GetCityList(List<SangoObject> objects)
        {
            List<City> cities = new List<City>();
            if (objects == null) return cities;
            foreach (SangoObject obj in objects)
            {
                if (obj is City city && city != null)
                    cities.Add(city);
            }
            return cities;
        }
    }
}
