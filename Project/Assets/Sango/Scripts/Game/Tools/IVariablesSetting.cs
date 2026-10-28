using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    public interface IVariablesSetting
    {
        void RefreshSetting();
        void RemoveItem(GameObject item);
        GameObject AddBigTitle(string title);
        GameObject AddTitle(string title);
        GameObject AddNumberItem(string title, int number, int min, int max, System.Action<int> onChange);
        GameObject AddNumberItem(string title, float number, float min, float max, System.Action<float> onChange);
        GameObject AddSliderItem(string title, int value, int min, int max, System.Action<int> onValueChange);
        GameObject AddSliderItem(string title, float value, float min, float max, System.Action<float> onValueChange);
        GameObject AddToggleItem(string title, bool value, System.Action<bool> onValueChange) ;
        GameObject AddDropdownItem(string title, int value, List<string> values, System.Action<int> onValueChange);
        GameObject AddToggleGroupItem(string title, int value, List<string> values, System.Action<int> onValueChange);

        /// <summary>
        /// 一行"可点击的按钮"（外观沿用标题行的排版，加一层半透明底）。
        /// 用于"保存预设"这类一次性动作，不参与参数读写。
        /// </summary>
        /// <param name="title">按钮文字</param>
        /// <param name="onClick">点击回调</param>
        /// <returns>生成的对象</returns>
        GameObject AddButtonItem(string title, System.Action onClick);

        /// <summary>
        /// 一行"文本输入"（标题行 + 可编辑输入框）。
        /// 用于"预设名字"这类需要自由文本的配置项。
        /// </summary>
        /// <param name="title">行标题</param>
        /// <param name="value">初始文本</param>
        /// <param name="onChange">结束编辑时回调（回车或失焦）</param>
        /// <returns>输入行对象</returns>
        GameObject AddInputItem(string title, string value, System.Action<string> onChange);
        void SetItemBehindThis(GameObject item, GameObject t);


    }
}
