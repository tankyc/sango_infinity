using Sango.Loader;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Sango.Core; namespace Sango.UI
{
    public class UIDropdownField : MonoBehaviour
    {
        public Text title;
        public Dropdown dropdown;
        public int value;
        public System.Action<int> onValueChange;

        public void Set(string title, int value, List<string> options, System.Action<int> onValueChange)
        {
            this.title.text = title;
            SetValue(value, options, onValueChange);
        }

        /// <summary>
        /// 只刷新选项与回调，不动左侧标题文本。
        ///
        /// 【使用场景】标题文案由 Prefab 静态给出时（如剧本参数界面的预设下拉框），
        /// 代码里再传一次标题会把美术配置的文案覆盖掉，故拆出这个重载。
        /// </summary>
        /// <param name="value">当前选中项下标</param>
        /// <param name="options">选项文本列表</param>
        /// <param name="onValueChange">选中项变化回调</param>
        public void SetValue(int value, List<string> options, System.Action<int> onValueChange)
        {
            this.value = value;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(value);
            this.onValueChange = onValueChange;
            dropdown.onValueChanged.RemoveAllListeners();
            dropdown.onValueChanged.AddListener(OnValueChanged);
        }

        void OnValueChanged(int v)
        {
            if (v != value)
            {
                value = v;
                onValueChange?.Invoke(v);
            }
        }
    }
}