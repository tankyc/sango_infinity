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

        /// <summary>
        /// 第 1 个排序组的显示标题（分组标签文字）。
        ///
        /// 【不要因为"看起来没人用"就删掉】它由各选择器的 <c>Start(...)</c> 写入
        /// （<c>this.customSortTitleName = cutomSortTitleName ?? defualtTitleName</c>），
        /// 由 <see cref="Sango.UI.UIObjectDisplay"/> 读取并显示为分组标签。
        /// 曾在一处重构中被误删，直接导致 8 个选择器与显示层共 10 处 CS1061 编译错误。
        /// </summary>
        public string customSortTitleName;

        /// <summary>
        /// 第 1 个排序组（标题组）的列定义。
        ///
        /// 【不可被"当前显示列"覆盖】各选择器的 <c>GetSortTitleGroup(0)</c> 直接返回本字段。
        /// 历史实现里，界面切换分组时把"当前显示列"也回写到这里，玩家点别的分组
        ///（所属 / 能力 / 特技…）就会把第 1 组的列顶掉，再点回来拿到的仍是上一组的列 ——
        /// 表现就是"切换 title 后切不回来"。现在界面只维护自己的列缓存，不再回写本字段。
        /// </summary>
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

        /// <summary>
        /// 是否隐藏"排序组表卡"（只保留第 1 张卡当标题，其余卡位全部隐藏）。
        ///
        /// 【什么时候用】列定义由调用方固定、不存在"换一组列看"的场景（如国库预览）。
        /// 窗口的表卡数量取自 prefab 上的 toggleGroup 数组长度，不关掉就会平白多出一排标签；
        /// 关掉之后玩家只看到"这一个列表就是这些列"。
        ///
        /// 【生命周期】与 <see cref="BrowseMode"/> 一样由各选择器的 Start 显式写入、
        /// 在 <see cref="OnDestroy"/> 统一清掉，避免串到下一次打开。
        /// </summary>
        public bool HideSortGroups { get; set; }

        /// <summary>
        /// 纯浏览模式：只看数据，不能勾选，也没有「确定」按钮，只能返回。
        ///
        /// 【与 ClickMode 的区别】ClickMode 是"点一行就等于选完并退出"，仍然是一次**选择**；
        /// 本模式根本不产生选择结果（如外交情报这类只读列表）。界面侧据此隐藏
        /// 勾选列 / 全选按钮 / 确定按钮，并屏蔽行点击的勾选逻辑。
        ///
        /// 【生命周期】由各选择器的 Start 显式写入，在 <see cref="OnDestroy"/> 里统一清掉：
        /// 窗口关闭即失效，避免下一次用同一个系统实例做真实选择时还带着浏览态。
        /// </summary>
        public bool BrowseMode { get; set; }

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
        /// 「按特技搜索」的关键词前缀：<c>t:特技名</c>（同时接受全角冒号 <c>t：</c>）。
        ///
        /// 【为什么要有前缀】特技搜索与"按名字搜索"是两套判定：前者要遍历每名武将
        /// 身上的特技表，后者只比一个字符串。混在同一个关键词里做全字段模糊匹配，
        /// 会让每次输入都把候选集里所有人 × 所有字段扫一遍；用前缀显式区分后，
        /// 两条路径各自只走自己能命中的那一层（详见 PersonSelectSystem.MatchSearchKeyword）。
        /// </summary>
        public const string FeatureSearchPrefix = "t:";

        /// <summary>当前关键词是否走"按特技搜索"（<c>t:</c> 前缀）</summary>
        public bool IsFeatureSearch { get; private set; }

        /// <summary>
        /// 去掉 <c>t:</c> 前缀后的特技关键词；非特技搜索时为 null。
        /// 可能为空串（玩家只输入了"t:"），此时按"不过滤"处理，见 PersonSelectSystem 的匹配实现。
        /// </summary>
        public string featureSearchText { get; private set; }

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
            ParseSearchPrefix();
            OnSearchKeywordChanged();
        }

        /// <summary>
        /// 解析搜索关键词的前缀语法（<c>t:</c> = 按特技名搜索）。
        /// 每次关键词变化都要重新解析：从 "t:神算" 退成 "神算" 时必须回到普通搜索。
        /// </summary>
        void ParseSearchPrefix()
        {
            IsFeatureSearch = false;
            featureSearchText = null;

            // 前缀至少两字符（t 或 T + 冒号）；"t" 单独输入仍是普通搜索。
            // 冒号后可以没有内容（"t:"）：此时 IsFeatureSearch 仍为 true，但关键词为空，
            // 由匹配侧按"不过滤"处理（见 PersonSelectSystem.MatchSearchKeyword）。
            if (string.IsNullOrEmpty(searchKeyword) || searchKeyword.Length < 2)
                return;

            char head = searchKeyword[0];
            char colon = searchKeyword[1];
            if ((head != 't' && head != 'T') || (colon != ':' && colon != '：'))
                return;

            IsFeatureSearch = true;
            featureSearchText = searchKeyword.Substring(2).Trim();
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
            // 前缀状态必须一起清：否则下一次普通搜索会带着上一次的"特技搜索"口径
            IsFeatureSearch = false;
            featureSearchText = null;
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

            // 【t: 前缀的降级口径】本方法只做"按名字包含"匹配。带 t: 的关键词由
            // PersonSelectSystem 重写成"按特技名搜索"（人物是唯一拥有特技表的对象）；
            // 其余选择器（势力 / 城池 / 特技表…）不认识这个前缀，这里剥掉前缀后
            // 仍按名字匹配，避免玩家在别的窗口输入 t: 时得到"怎么搜都是空"的错觉。
            string keyword = IsFeatureSearch ? featureSearchText : searchKeyword;
            if (string.IsNullOrEmpty(keyword))
                return true;

            string name = obj.Name;
            if (string.IsNullOrEmpty(name))
                return false;
            return name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
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
            // 浏览态 / 单表卡只属于本次打开：关窗即清，下一次打开默认回到"可勾选 + 多表卡"的选择器
            BrowseMode = false;
            HideSortGroups = false;
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
