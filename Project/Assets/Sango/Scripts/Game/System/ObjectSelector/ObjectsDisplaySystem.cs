using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class ObjectsDisplaySystem : GameSystem
    {
        /// <summary>
        /// 当前显示列表的实际存储（被过滤后就地增删）。
        /// </summary>
        List<SangoObject> displayObjects;

        /// <summary>
        /// 未经过滤的完整候选集，用于取消过滤后恢复显示。
        /// **由 <see cref="Objects"/> 的赋值自动刷新** —— 各选择器的 Start 只需给出候选集，
        /// 不必再各自记得维护一份快照（历史上只有 PersonSelectSystem 记得，其余全都漏了，
        /// 导致搜索/过滤会把列表清空且清空搜索也回不来）。
        /// </summary>
        protected List<SangoObject> allObjects = new List<SangoObject>();

        /// <summary>
        /// 当前显示列表。
        /// 【约定】给本属性赋值 = "更换候选集"，会同步刷新完整候选集快照；
        /// 过滤逻辑只用 getter 就地增删（Clear / AddRange），因此不会误刷新快照。
        /// </summary>
        public List<SangoObject> Objects
        {
            get { return displayObjects; }
            set
            {
                displayObjects = value;
                allObjects = value != null ? new List<SangoObject>(value) : new List<SangoObject>();
            }
        }

        public string customSortTitleName;
        public List<ObjectSortTitle> customSortItems;
        public string windowName = "window_object_selector";

        public struct ButtonData
        {
            public string title;
            public int style;
            public System.Action action;
        }

        public List<ButtonData> buttonDatas;

        /// <summary>
        /// 点选模式
        /// </summary>
        public bool ClickMode { get; set; }

        public void Start(List<SangoObject> sangoObjects, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Objects = new List<SangoObject>(sangoObjects);
            customSortItems = customSortTitles;
            this.customSortTitleName = cutomSortTitleName;
            Push();
        }

        /// <summary>
        /// 搜索关键词（已去掉首尾空白）。为空表示不按关键词过滤。
        /// 由选择窗口的搜索框写入（见 UIObjectSelector），只影响"显示列表"，
        /// 不改动候选集本身，因此不会影响玩家最终勾选的结果。
        /// </summary>
        public string searchKeyword { get; private set; }

        /// <summary>当前是否处于关键词搜索状态</summary>
        public bool HasSearchKeyword { get { return !string.IsNullOrEmpty(searchKeyword); } }

        /// <summary>
        /// 显示层实例（窗口打开时由 UIObjectDisplay.Init 绑定）。
        /// 数据被过滤后直接用它重绘，不再依赖 WindowInterface 是否已赋值。
        /// </summary>
        protected Sango.UI.UIObjectDisplay boundDisplay;

        /// <summary>
        /// 绑定显示层实例，供过滤后重绘使用。
        /// </summary>
        /// <param name="display">窗口上的显示层组件</param>
        public void BindDisplay(Sango.UI.UIObjectDisplay display)
        {
            boundDisplay = display;
        }

        /// <summary>
        /// 设置搜索关键词。与当前值相同（忽略首尾空白差异）时直接返回，
        /// 避免搜索框每次敲键都白重建一遍列表。
        /// </summary>
        /// <param name="keyword">搜索框里的原始文本，可为空</param>
        public void SetSearchKeyword(string keyword)
        {
            string normalized = keyword == null ? string.Empty : keyword.Trim();
            if (normalized == (searchKeyword ?? string.Empty))
                return;
            searchKeyword = normalized;
            OnSearchKeywordChanged();
        }

        /// <summary>
        /// 清空搜索关键词（打开选择窗口时调用），避免上一次的搜索条件串到下一次使用。
        /// 原本处于搜索状态时会触发一次列表还原。
        /// </summary>
        public void ResetSearchKeyword()
        {
            if (!HasSearchKeyword)
                return;
            searchKeyword = null;
            OnSearchKeywordChanged();
        }

        /// <summary>
        /// 关键词变化后的回调。默认什么都不做 —— 没有显示列表的派生类无需实现。
        /// </summary>
        protected virtual void OnSearchKeywordChanged()
        {
        }

        /// <summary>
        /// 单个候选对象是否命中搜索关键词：按对象名做不区分大小写的"包含"匹配。
        /// 关键词为空时恒为 true。派生类可重写以扩大匹配范围
        ///（例如把所属势力、所在城池也纳入匹配）。
        /// </summary>
        /// <param name="obj">候选对象</param>
        /// <returns>命中返回 true</returns>
        public virtual bool MatchSearchKeyword(SangoObject obj)
        {
            if (!HasSearchKeyword)
                return true;
            if (obj == null)
                return false;
            string name = obj.Name;
            if (string.IsNullOrEmpty(name))
                return false;
            return name.IndexOf(searchKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void OnCancel()
        {
            Back();
        }

        /// <summary>
        /// 进入当前命令的时候触发
        /// </summary>
        public override void OnEnter()
        {
            Window.WindowInterface win = Window.Instance.Open(windowName);
            if (win != null)
            {
                UIObjectSelector uIObjectSelector = win.ugui_instance as UIObjectSelector;
                if (uIObjectSelector != null)
                {
                    uIObjectSelector.Init(this);
                }
            }
        }

        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            Window.Instance.SetVisible(windowName, true);
            if(ClickMode)
            {
                Window.Instance.GetWindow(windowName).Refresh();
            }
        }

        public override void OnExit()
        {
            Window.Instance.SetVisible(windowName, false);
        }

        public virtual List<ObjectSortTitle> GetSortTitleGroup(int index)
        {
            return customSortItems;
        }

        public virtual string GetSortTitleGroupName(int index)
        {
            return "";
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    OnCancel(); break;
            }

        }
    }
}
