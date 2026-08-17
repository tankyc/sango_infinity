using Sango.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 势力颜色选择窗口。
    /// 从 GameData.Instance.ScenarioCommonData.Flags 中动态生成可选颜色项，
    /// 排除已被其他势力占用的颜色，点击后触发选择事件。
    /// </summary>
    public class UIForceColorSelect : UGUIWindow
    {
        #region UI组件

        /// <summary>
        /// 标题文本。
        /// </summary>
        public Text titleText;

        /// <summary>
        /// 颜色项预制体，需挂载 <see cref="UISangoObjectSelectItem"/> 组件。
        /// </summary>
        public UISangoObjectSelectItem colorItemPrefab;

        /// <summary>
        /// 返回按钮，点击后直接关闭窗口。
        /// </summary>
        public Button returnButton;

        #endregion

        #region 常量

        /// <summary>
        /// 最大可选颜色数量。
        /// </summary>
        protected const int MaxColorCount = 120;

        #endregion

        #region 数据

        /// <summary>
        /// 已占用的旗帜ID集合。
        /// </summary>
        protected HashSet<int> usedFlagIds = new HashSet<int>();

        /// <summary>
        /// 选择回调，参数为选中的旗帜ID。
        /// </summary>
        protected Action<Flag> onColorSelected;

        /// <summary>
        /// 已创建的颜色项列表。
        /// </summary>
        protected List<UISangoObjectSelectItem> colorItems = new List<UISangoObjectSelectItem>();

        ShortScenario shortScenario;
        ShortForce shortForce;
        CreatePool<UISangoObjectSelectItem> createPool;
        #endregion

        #region 生命周期

        protected override void Awake()
        {
            createPool = new CreatePool<UISangoObjectSelectItem>(colorItemPrefab);
            base.Awake();
            BindEvents();
        }

        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            ParseOpenArgs(objects);
            RefreshColorItems();
        }

        #endregion

        #region 参数解析

        /// <summary>
        /// 解析打开窗口时传入的参数。
        /// 支持格式：
        /// (List&lt;int&gt; usedFlagIds, Action&lt;int&gt; onColorSelected)
        /// (int[] usedFlagIds, Action&lt;int&gt; onColorSelected)
        /// (IEnumerable&lt;int&gt; usedFlagIds, Action&lt;int&gt; onColorSelected)
        /// (Action&lt;int&gt; onColorSelected)
        /// </summary>
        protected virtual void ParseOpenArgs(object[] objects)
        {
            usedFlagIds.Clear();
            onColorSelected = null;

            shortScenario = (ShortScenario)objects[0];
            shortForce = (ShortForce)objects[1];

            shortScenario.forceSet.ForEach(force =>
            {
                if(force.Id != shortForce.Id)
                    usedFlagIds.Add(force.Flag);
            });
            onColorSelected = (Action<Flag>)objects[2];
            // 选择回调
        }

        #endregion

        #region 事件绑定

        /// <summary>
        /// 绑定返回按钮事件。
        /// 颜色项的点击事件由颜色项自身绑定。
        /// </summary>
        protected virtual void BindEvents()
        {
            if (returnButton != null)
            {
                returnButton.onClick.RemoveAllListeners();
                returnButton.onClick.AddListener(OnReturnClicked);
            }
        }

        #endregion

        #region 颜色项刷新

        /// <summary>
        /// 刷新颜色项显示。
        /// 根据 GameData.Instance.ScenarioCommonData.Flags 中的实际旗帜数量动态创建颜色项，最多不超过120个。
        /// </summary>
        protected virtual void RefreshColorItems()
        {
            createPool.Reset();

            int count = 0;
            GameData.Instance.ScenarioCommonData.Flags.ForEach((Flag flag) =>
            {
                if (count >= MaxColorCount)
                {
                    return;
                }

                if (flag == null)
                {
                    return;
                }

                bool isUsed = usedFlagIds.Contains(flag.Id);
                CreateColorItem(flag, isUsed);
                count++;
            });

            if (titleText != null)
            {
                titleText.text = string.Format("势力颜色 ({0}/{1})", count, MaxColorCount);
            }
        }

        /// <summary>
        /// 创建一个颜色项。
        /// </summary>
        /// <param name="flag">旗帜数据</param>
        /// <param name="isUsed">是否已被占用</param>
        protected virtual void CreateColorItem(Flag flag, bool isUsed)
        {
            UISangoObjectSelectItem item = createPool.Create();
            item.target = flag;
            item.onSelectAction = OnColorItemClicked;
            item.SetInavtive(isUsed);
            item.SetSelected(false);
            item.SetColor(flag.color);
            colorItems.Add(item);
        }

        #endregion

        #region 事件响应

        /// <summary>
        /// 颜色项点击事件。
        /// </summary>
        /// <param name="flagId">选中的旗帜ID</param>
        protected virtual void OnColorItemClicked(SangoObject flag)
        {
            Flag flag1 = flag as Flag;
            if (usedFlagIds.Contains(flag1.Id))
            {
                Sango.Log.Warning(string.Format("势力颜色选择窗口：尝试选择已被占用的颜色，ID={0}。", flag1.Id));
                return;
            }

            onColorSelected?.Invoke(flag1);
            Close();
        }

        /// <summary>
        /// 返回按钮点击事件：直接关闭窗口。
        /// </summary>
        protected virtual void OnReturnClicked()
        {
            Close();
        }

        #endregion
    }
}
