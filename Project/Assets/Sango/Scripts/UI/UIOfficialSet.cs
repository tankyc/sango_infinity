using Sango.Core.Player;
using UnityEngine;

using Sango.Core;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Sango.UI
{
    /// <summary>
    /// 官职任命界面（原版"官职"对话框）。
    ///
    /// 布局（window_official_set_org.prefab）：
    /// <list type="bullet">
    /// <item>左表 official_list —— 官职表：一行一个官职，末列显示现任者。
    ///   一个官职只可能有一名任职者（槽位模型，见 <see cref="CityUpgradeOfficial.upgradeList"/>）。</item>
    /// <item>右表 peron_list —— 空闲武将表：**已经有官职的人不在这里**；
    ///   被顶替 / 被免职的人会自动回到这张表（<see cref="CityUpgradeOfficial.targetList"/>）。</item>
    /// <item>btns —— 自动设定 / 全解除 / 恢复原状。</item>
    /// </list>
    ///
    /// 操作（两种等价的入册方式）：
    /// <list type="number">
    /// <item>左表单击选中一个官职 → 右表双击某个武将 → 入册。</item>
    /// <item>右表把武将**横向拖到**左表某个官职行上 → 入册。</item>
    /// </list>
    /// 反馈：选中官职时右表所有**不满足条件**的武将变灰；拖动武将时左表所有**不满足条件**的官职变灰。
    /// 左表双击 = 免职（该人回到右侧空闲列表）。
    ///
    /// 所有改动只存在于内存（槽位），点"决定"才真正写入人物数据（<see cref="CityUpgradeOfficial.DoJob"/>），
    /// "恢复原状"随时可以把槽位还原成打开界面时的样子。
    /// </summary>
    public class UIOfficialSet : UGUIWindow
    {
        public Text title;
        public List<ObjectSortTitle> sortItems;
        public UIObjectListItem[] uIObjectListItems;
        public Scrollbar scrollbar;
        public Scrollbar scrollbar_h;
        protected List<UISortButton> sortButtonPool = new List<UISortButton>();
        public UISortButton sortTitleItem;
        public GameObject selectSortBtn;
        public RectTransform sorltTitleTransform;
        public RectTransform maskRect;
        public RectTransform[] contentRect;
        protected int startIndex = 0;
        protected float itemWidth = 0;
        protected int itemCount = 0;
        public Button sureButton;

        public RectTransform btnRoot;
        public UIButtonItem[] buttons;

        public UIOfficialItem[] uiOfficialItems;
        public Scrollbar official_scrollbar;
        protected int officialIndex = 0;

        // ↓ 旧版"选择官职"面板的预制体绑定，逻辑已废弃（改为左选官职 + 右双击/拖动），
        //   字段保留是为了不破坏 prefab 的序列化引用。
        public GameObject officialSelectPanel;
        public UIOfficialItem[] uiSelectOfficialItems;
        public UIPersonItem uIPersonItem;
        protected int selectOfficialIndex = 0;
        public Scrollbar select_official_scrollbar;
        public Button select_official_sureButton;
        public UITextField limitText;
        public UITextField officialEffectText;
        public UITextField officialNeedText;

        CityUpgradeOfficial cityUpgradeOfficial;

        /// <summary>左表当前选中的官职行（任命目标）</summary>
        UIOfficialItem currentOfficialItem;

        /// <summary>右表当前选中的武将行</summary>
        UIObjectListItem currentPersonItem;

        /// <summary>正在被拖动的武将（拖动期间左侧官职按此人置灰）</summary>
        Person dragPerson;

        /// <summary>
        /// 拖动武将时跟随指针的浮动名字（预制体根下的 dragTextItem 节点）。
        /// 节点由美术/策划在预制体里摆好（bg 底图 + name 文本），运行时只负责显示/隐藏与跟手。
        /// </summary>
        public RectTransform dragTextItem;

        /// <summary>dragTextItem 上的文本组件（文本在子节点 name 上）</summary>
        UITextItem dragTextComp;

        /// <summary>dragTextItem 的底图（随文字宽度一起拉伸）</summary>
        RectTransform dragTextBg;

        /// <summary>dragTextItem 所在画布（惰性缓存，拖动时每帧都要用）</summary>
        Canvas dragTextCanvas;

        /// <summary>浮动文本左上角相对指针的偏移（放在指针右下方，避免压住光标/手指）</summary>
        static readonly Vector2 DragTextOffset = new Vector2(18f, -18f);

        protected override void Awake()
        {
            base.Awake();
            // 预制体里 officialSelectPanel 的引用是断的（绑定 fileID 在 prefab 中不存在），
            // 这里按节点名兜底拿到 ofnode，用于隐藏这个已废弃的面板。
            if (officialSelectPanel == null)
            {
                Transform panel = transform.Find("ofnode");
                if (panel != null)
                    officialSelectPanel = panel.gameObject;
            }

            InitDragTextItem();
        }

        /// <summary>
        /// 初始化拖拽浮动文本：按节点名兜底取引用（避免必须在 Inspector 里手工拖），
        /// 并在窗口打开前就隐藏 —— 预制体里该节点是常显的，隐藏交给运行时。
        /// </summary>
        void InitDragTextItem()
        {
            if (dragTextItem == null)
            {
                Transform node = transform.Find("dragTextItem");
                if (node != null)
                    dragTextItem = node as RectTransform;
            }

            if (dragTextItem == null)
                return;

            dragTextComp = dragTextItem.GetComponent<UITextItem>();
            Transform bg = dragTextItem.Find("bg");
            if (bg != null)
                dragTextBg = bg as RectTransform;
            dragTextCanvas = dragTextItem.GetComponentInParent<Canvas>();

            // 浮动标签始终贴在指针下方，不能参与射线：否则会把指针所在列表行的
            // 悬停高亮 / 拖拽落点判定挡掉（预制体里 bg、name 的 raycastTarget 都是勾上的）。
            Graphic[] graphics = dragTextItem.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;

            HideDragText();
        }

        public override void OnOpen()
        {
            base.OnOpen();

            cityUpgradeOfficial = GameSystem.GetSystem<CityUpgradeOfficial>();
            if (cityUpgradeOfficial == null)
                return;

            title.text = cityUpgradeOfficial.customTitleName;
            sortItems = cityUpgradeOfficial.customTitleList;

            // 旧版选择面板不再使用，直接隐藏（预制体节点保留）
            if (officialSelectPanel != null)
                officialSelectPanel.SetActive(false);

            InitItemColumns();
            BindStateIcons();

            itemWidth = GetContentWidth();
            itemCount = uIObjectListItems.Length;
            bool show_scrollbar_h = maskRect.rect.width < itemWidth;
            scrollbar_h.gameObject.SetActive(show_scrollbar_h);
            if (show_scrollbar_h)
            {
                itemCount--;
                scrollbar_h.size = (float)maskRect.rect.width / (float)itemWidth;
                scrollbar_h.SetValueWithoutNotify(0);
                uIObjectListItems[uIObjectListItems.Length - 1].gameObject.SetActive(false);
            }
            else
            {
                uIObjectListItems[uIObjectListItems.Length - 1].gameObject.SetActive(true);
            }

            startIndex = 0;
            officialIndex = 0;
            currentOfficialItem = null;
            currentPersonItem = null;
            dragPerson = null;

            // 拖拽浮动文本：Awake 时嵌套预制体可能尚未展开（实测 Find 会拿到 null），
            // 这里在窗口真正打开时再兜一次引用
            if (dragTextItem == null)
                InitDragTextItem();
            else
                HideDragText();

            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                if (listItem == null) continue;
                Vector2 p = listItem.contentRect.anchoredPosition;
                p.x = 0;
                listItem.contentRect.anchoredPosition = p;
                listItem.SetOver(false);
                listItem.SetSelected(false);
                listItem.SetDisable(false);
            }

            for (int j = 0; j < uiOfficialItems.Length; j++)
            {
                UIOfficialItem item = uiOfficialItems[j];
                if (item == null) continue;
                item.SetSelected(false);
                item.SetDisable(false);
                // 预制体里 disable / up 两个节点是常显的，这里先无条件收起，
                // 具体状态等 UpdateContent 按数据刷新（不能只依赖行的 Awake，
                // 预制体作为嵌套实例加载时 Awake 的时序不一定可靠）
                item.SetState(UIOfficialItem.OfficialStateType.None);
            }

            UpdateSortContent();
            BindItemEvents();
            BindButtons();
            UpdateContent();

            OnScrollBarValueChange(0);
            foreach (RectTransform r in contentRect)
            {
                Vector2 p = r.anchoredPosition;
                p.x = 0;
                r.anchoredPosition = p;
            }
        }

        /// <summary>
        /// 行内文本槽按显示顺序重排（预制体里 textItem_1 不一定在第一列，
        /// 且 Awake 阶段嵌套实例的 siblingIndex 还没稳定，必须等彻底实例化后再排）。
        /// </summary>
        void InitItemColumns()
        {
            if (uiOfficialItems != null)
            {
                for (int i = 0; i < uiOfficialItems.Length; i++)
                {
                    UIOfficialItem item = uiOfficialItems[i];
                    if (item == null) continue;
                    item.isSelectPanel = false;
                    item.InitColumns();
                }
            }

            if (uiSelectOfficialItems != null)
            {
                for (int i = 0; i < uiSelectOfficialItems.Length; i++)
                {
                    UIOfficialItem item = uiSelectOfficialItems[i];
                    if (item == null) continue;
                    item.isSelectPanel = true;
                    item.InitColumns();
                }
            }
        }

        /// <summary>
        /// 把界面右上角图例（root/ferld）的三张图标注入到每一行，作为"可晋升 / 晋升 / 降格"状态图。
        /// 图例本身是原版界面的操作说明，图标就是同一份美术资源；取它的好处是不用在代码里
        /// 硬编码图集里的 sprite 名（sprite 名是 4846-4_39 这类内部编号，美术换图后必然失效）。
        /// </summary>
        void BindStateIcons()
        {
            if (uiOfficialItems == null)
                return;

            UnityEngine.Sprite canPromote = null;
            UnityEngine.Sprite promote = null;
            UnityEngine.Sprite demote = null;

            Transform ferld = transform.Find("root/ferld");
            if (ferld != null)
            {
                canPromote = GetLegendSprite(ferld, "icon");     // 图例①"可晋升"
                demote = GetLegendSprite(ferld, "icon_1");       // 图例②"降格"
                promote = GetLegendSprite(ferld, "icon_2");      // 图例③"晋升"
            }
            else
            {
                Sango.Log.Warning("官职界面找不到图例节点 root/ferld，晋升/降格图标将无法显示");
            }

            for (int i = 0; i < uiOfficialItems.Length; i++)
            {
                UIOfficialItem item = uiOfficialItems[i];
                if (item == null) continue;
                item.SetStateSprites(canPromote, promote, demote);
            }
        }

        /// <summary>取图例里某个图标节点的图片</summary>
        static UnityEngine.Sprite GetLegendSprite(Transform ferld, string nodeName)
        {
            Transform node = ferld.Find(nodeName);
            if (node == null)
                return null;
            Image img = node.GetComponent<Image>();
            return img != null ? img.sprite : null;
        }

        void BindItemEvents()
        {
            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                if (listItem == null) continue;

                listItem.onSelected = OnPersonListSelected;
                listItem.onDoubleSelected = OnPersonDoubleSelected;

                // 拖动转发（往行自带的 EventTrigger 里追加条目，不改预制体）
                UIDragBinder binder = listItem.GetComponent<UIDragBinder>();
                if (binder == null)
                    binder = listItem.gameObject.AddComponent<UIDragBinder>();
                binder.onBeginDrag = OnBeginDragPerson;
                binder.onDragging = OnDraggingPerson;
                binder.onEndDrag = OnEndDragPerson;
                binder.Bind(listItem);
            }

            for (int j = 0; j < uiOfficialItems.Length; j++)
            {
                UIOfficialItem listItem = uiOfficialItems[j];
                if (listItem == null) continue;
                listItem.onSelected = OnSelectOfficialItem;
                listItem.onDoubleSelected = OnDoubleSelectOfficialItem;
            }
        }

        void BindButtons()
        {
            bool hasButtons = cityUpgradeOfficial.buttonDatas != null;
            btnRoot.gameObject.SetActive(hasButtons);
            if (!hasButtons)
                return;

            for (int i = 0; i < buttons.Length; i++)
            {
                UIButtonItem buttonItem = buttons[i];
                if (buttonItem == null) continue;

                if (i >= cityUpgradeOfficial.buttonDatas.Count)
                {
                    buttonItem.gameObject.SetActive(false);
                    continue;
                }

                ObjectsDisplaySystem.ButtonData buttonData = cityUpgradeOfficial.buttonDatas[i];
                buttonItem.gameObject.SetActive(true);
                buttonItem.SetTitle(buttonData.title).SetStyle(buttonData.style).BindAction(() =>
                {
                    buttonData.action?.Invoke();
                    UpdateContent();
                });
            }
        }

        /// <summary>刷新两侧列表、滚动条与置灰状态</summary>
        public void UpdateContent()
        {
            if (cityUpgradeOfficial == null)
                return;

            // 右表（空闲武将）
            int personCount = cityUpgradeOfficial.targetList.Count;
            int maxStart = System.Math.Max(0, personCount - itemCount);
            if (startIndex > maxStart)
            {
                startIndex = maxStart;
                // 有人入册后列表变短，滚动条要跟着回退，否则滑块停在越界位置看起来像"卡住"
                if (scrollbar != null && maxStart > 0)
                    scrollbar.SetValueWithoutNotify((float)startIndex / (float)maxStart);
            }

            if (personCount < itemCount)
            {
                scrollbar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                scrollbar.transform.parent.gameObject.SetActive(true);
                scrollbar.size = System.Math.Max(0.1f, (float)itemCount / (float)personCount);
            }

            // 左表（官职）
            int officialCount = cityUpgradeOfficial.upgradeList.Count;
            if (officialCount < uiOfficialItems.Length)
            {
                official_scrollbar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                official_scrollbar.transform.parent.gameObject.SetActive(true);
                official_scrollbar.size = System.Math.Max(0.1f, (float)uiOfficialItems.Length / (float)officialCount);
            }

            UpdateItemStartIndex(startIndex);
            UpdateOfficialStartIndex(officialIndex);
            RefreshDisableState();
            UpdateSureButtonState();
            RefreshOfficialEffectText();
        }

        /// <summary>
        /// 刷新列表下方的官职说明（预制体节点 root/official_list/offical_effect）。
        /// 左表每行只显示 5 字以内的短描述（<see cref="Official.effect_short"/>），
        /// 完整说明（任职条件 + 加成明细，<see cref="Official.effect_desc"/>）在这里展开，选中哪行说明哪行。
        /// </summary>
        void RefreshOfficialEffectText()
        {
            if (officialEffectText == null)
                return;

            Official official = currentOfficialItem != null ? currentOfficialItem.official : null;
            officialEffectText.SetText(official == null || string.IsNullOrEmpty(official.effect_desc)
                ? ""
                : official.effect_desc);
        }

        /// <summary>
        /// 刷新官职能力要求（预制体节点 root/official_list/offical_need）。
        /// 只列该官职真正要求的项（Need &gt; 0），没要求的项不占位；
        /// 若当前有参照武将（拖动中的优先，其次右表选中的），他不达标的项标红，
        /// 免得玩家自己拿属性表逐项心算。
        ///
        /// 【为什么挂在 RefreshDisableState 里】参照武将在"右表选中 / 开始拖动 / 结束拖动 /
        /// 左表换行 / 列表重建"时都会变，这些时机全部会走 RefreshDisableState；
        /// 单独再造一份调用链容易漏刷，索性跟着它一起刷。
        /// </summary>
        void RefreshOfficialNeedText()
        {
            if (officialNeedText == null)
                return;

            Official official = currentOfficialItem != null ? currentOfficialItem.official : null;
            if (official == null)
            {
                officialNeedText.SetText("");
                return;
            }

            // 不达标项靠富文本标红；节点若被关掉 richText，会把 <color=...> 原样显示成一串标签
            if (officialNeedText.label != null && !officialNeedText.label.supportRichText)
                officialNeedText.label.supportRichText = true;

            Person refPerson = GetReferencePerson();
            // 等级优先取解析后的对象，老存档 / 未解析时退回 LevelId 数值镜像
            int levelValue = 0;
            if (refPerson != null)
                levelValue = refPerson.Level != null ? refPerson.Level.Id : refPerson.LevelId;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            AppendNeed(sb, "统率", official.commandNeed, refPerson, refPerson != null ? refPerson.Command : 0);
            AppendNeed(sb, "武力", official.strengthNeed, refPerson, refPerson != null ? refPerson.Strength : 0);
            AppendNeed(sb, "智力", official.intelligenceNeed, refPerson, refPerson != null ? refPerson.Intelligence : 0);
            AppendNeed(sb, "政治", official.politicsNeed, refPerson, refPerson != null ? refPerson.Politics : 0);
            AppendNeed(sb, "魅力", official.glamourNeed, refPerson, refPerson != null ? refPerson.Glamour : 0);
            AppendNeed(sb, "等级", official.levelNeed, refPerson, levelValue);

            officialNeedText.SetText(sb.Length == 0 ? "无任职要求" : sb.ToString());
        }

        /// <summary>
        /// 追加一项任职要求；<paramref name="refPerson"/> 不为空且数值不达标时用红字标出。
        /// </summary>
        static void AppendNeed(System.Text.StringBuilder sb, string label, int need, Person refPerson, int value)
        {
            if (need <= 0)
                return;

            if (sb.Length > 0)
                sb.Append("  ");

            if (refPerson != null && value < need)
                sb.Append("<color=#FF6A6A>").Append(label).Append(' ').Append(need).Append("</color>");
            else
                sb.Append(label).Append(' ').Append(need);
        }

        void UpdateSureButtonState()
        {
            if (sureButton != null && cityUpgradeOfficial != null)
                sureButton.interactable = cityUpgradeOfficial.upgradeList.Count > 0;
        }

        /// <summary>
        /// 刷新"能不能任 / 任了是升还是降"的表现：
        /// ① 左表 —— 按参照武将（拖动中的优先，其次右表选中的）逐行判定：
        ///    不满足任职条件 → 亮 disable 遮罩；满足条件 → 显 晋升/降格/可晋升 图标。
        ///    遮罩只在**拖动中**出现（与原有交互一致），仅选中武将时只给图标提示不遮灰。
        /// ② 右表 —— 选中左侧官职时，不满足该官职条件的武将文字变灰。
        /// </summary>
        void RefreshDisableState()
        {
            if (cityUpgradeOfficial == null)
                return;

            Official targetOfficial = currentOfficialItem != null ? currentOfficialItem.official : null;
            Person refPerson = GetReferencePerson();

            // 左表
            for (int i = 0; i < uiOfficialItems.Length; i++)
            {
                UIOfficialItem item = uiOfficialItems[i];
                if (item == null) continue;

                bool canAssign = refPerson != null && item.official != null
                    && cityUpgradeOfficial.CanAssign(refPerson, item.official);

                item.SetDisable(dragPerson != null && item.official != null && !canAssign);
                item.SetCompare(refPerson, canAssign);
            }

            // 右表
            List<Person> list = cityUpgradeOfficial.targetList;
            for (int i = 0; i < itemCount && i < uIObjectListItems.Length; i++)
            {
                UIObjectListItem item = uIObjectListItems[i];
                if (item == null) continue;

                bool disable = false;
                if (dragPerson == null && targetOfficial != null)
                {
                    int destIndex = i + startIndex;
                    if (destIndex >= 0 && destIndex < list.Count)
                        disable = !cityUpgradeOfficial.CanAssign(list[destIndex], targetOfficial);
                }
                item.SetDisable(disable);
            }

            // 参照武将 / 当前选中官职都在这里变化，所以能力要求文本跟着一起刷新
            RefreshOfficialNeedText();
        }

        /// <summary>
        /// 左表"晋升 / 降格 / 可晋升"的参照武将：正在拖动的武将优先，其次右表选中的武将。
        /// 两者都没有时左表不显示任何状态图标（否则打开界面就是满屏箭头，没有比较对象毫无意义）。
        /// </summary>
        Person GetReferencePerson()
        {
            if (dragPerson != null)
                return dragPerson;
            return GetPersonByItem(currentPersonItem);
        }

        public float GetContentWidth()
        {
            float w = 0;
            for (int i = 0; i < sortItems.Count; i++)
            {
                ObjectSortTitle sortTitle = sortItems[i];
                w += sortTitle.ContentMaxWidth;
            }
            return w + 24;
        }

        public void UpdateSortContent()
        {
            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                if (listItem != null)
                    listItem.Clear();
            }

            for (int i = 0; i < sortItems.Count; i++)
            {
                ObjectSortTitle sortTitle = sortItems[i];
                UISortButton uIPersonSortButton;

                if (i == 0)
                {
                    uIPersonSortButton = sortTitleItem;
                }
                else
                {
                    if (i - 1 < sortButtonPool.Count)
                        uIPersonSortButton = sortButtonPool[i - 1];
                    else
                        uIPersonSortButton = CreateSortButtonItem();
                }

                uIPersonSortButton.gameObject.SetActive(true);
                uIPersonSortButton.Clear().SetWidth(sortTitle.ContentMaxWidth).SetName(sortTitle.name);

                uIPersonSortButton.onClick = (up) =>
                {
                    // 排序依据记在系统里：右表会因为"入册 / 被顶替"反复重建，重建后仍按这个依据排
                    cityUpgradeOfficial.SetSort(sortTitle, up);
                    scrollbar.SetValueWithoutNotify(0);
                    startIndex = 0;
                    UpdateContent();
                };

                if (i > 0)
                {
                    for (int j = 0; j < itemCount; j++)
                    {
                        UIObjectListItem listItem = uIObjectListItems[j];
                        if (listItem != null)
                            listItem.Add("", sortTitle.ContentMaxWidth);
                    }
                }
                else
                {
                    for (int j = 0; j < itemCount; j++)
                    {
                        UIObjectListItem listItem = uIObjectListItems[j];
                        if (listItem != null)
                            listItem.textItem.SetWidth(sortTitle.ContentMaxWidth);
                    }
                }
            }

            for (int i = sortItems.Count - 1; i < sortButtonPool.Count; i++)
                sortButtonPool[i].gameObject.SetActive(false);
        }

        protected UISortButton CreateSortButtonItem()
        {
            GameObject btn = GameObject.Instantiate(sortTitleItem.gameObject, sorltTitleTransform);
            UISortButton sortBtn = btn.GetComponent<UISortButton>();
            sortButtonPool.Add(sortBtn);
            return sortBtn;
        }

        /// <summary>刷新右表（空闲武将）</summary>
        public void UpdateItemStartIndex(int startIndex)
        {
            List<Person> list = cityUpgradeOfficial.targetList;

            for (int i = 0; i < itemCount; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem == null) continue;

                int destIndex = i + startIndex;
                listItem.index = destIndex;
                if (destIndex >= 0 && destIndex < list.Count)
                {
                    Person sango = list[destIndex];
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        ObjectSortTitle sortTitle = sortItems[j];
                        listItem.Set(j, sortTitle.GetValueStr(sango));
                    }
                    listItem.SetSelected(listItem == currentPersonItem);
                }
                else
                {
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        ObjectSortTitle sortTitle = sortItems[j];
                        listItem.Set(j, "");
                    }
                    listItem.SetSelected(false);
                }
            }
        }

        /// <summary>刷新左表（官职）</summary>
        public void UpdateOfficialStartIndex(int startIndex)
        {
            List<CityUpgradeOfficial.UpgradeOfficial> list = cityUpgradeOfficial.upgradeList;
            Person refPerson = GetReferencePerson();

            for (int i = 0; i < uiOfficialItems.Length; i++)
            {
                UIOfficialItem listItem = uiOfficialItems[i];
                if (listItem == null) continue;

                int destIndex = i + startIndex;
                listItem.index = destIndex;
                if (destIndex >= 0 && destIndex < list.Count)
                {
                    CityUpgradeOfficial.UpgradeOfficial data = list[destIndex];
                    listItem.SetOfficial(data.person, data.official);

                    // 行数据换了就必须重算一次晋升/降格：本行是 Loop 列表的复用行，
                    // 不重算的话滚出再滚回来会沿用上一任官职留下的图标与颜色
                    bool canAssign = refPerson != null && data.official != null
                        && cityUpgradeOfficial.CanAssign(refPerson, data.official);
                    listItem.SetCompare(refPerson, canAssign);

                    listItem.SetSelected(listItem == currentOfficialItem);
                }
                else
                {
                    listItem.SetOfficial(null, null);
                    listItem.SetCompare(null, false);
                    listItem.SetSelected(false);
                }
            }
        }

        #region 右表（空闲武将）交互

        /// <summary>单击右表武将：只做选中（高亮），不任命</summary>
        public void OnPersonListSelected(UIObjectListItem item)
        {
            if (item == null || cityUpgradeOfficial == null)
                return;

            currentPersonItem = item;
            for (int i = 0; i < itemCount && i < uIObjectListItems.Length; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem != null)
                    listItem.SetSelected(listItem == item);
            }

            // 参照武将变了，左表要重算一遍"可晋升 / 晋升 / 降格"图标
            RefreshDisableState();
        }

        /// <summary>
        /// 双击右表武将 = 任命到左表当前选中的官职。
        /// 没选官职 / 不满足条件时给提示，不做任何改动。
        /// </summary>
        public void OnPersonDoubleSelected(UIObjectListItem item)
        {
            if (item == null || cityUpgradeOfficial == null)
                return;

            Person person = GetPersonByItem(item);
            if (person == null)
                return;

            Official official = currentOfficialItem != null ? currentOfficialItem.official : null;
            if (official == null)
            {
                Sango.Log.Warning("请先在左侧选择一个官职，再双击武将");
                return;
            }

            if (!cityUpgradeOfficial.CanAssign(person, official))
            {
                Sango.Log.Warning($"【{person.Name}】不满足【{official.Name}】的任职条件");
                return;
            }

            cityUpgradeOfficial.Assign(person, official);
            currentPersonItem = null;
            UpdateContent();
        }

        Person GetPersonByItem(UIObjectListItem item)
        {
            if (item == null || cityUpgradeOfficial == null)
                return null;
            List<Person> list = cityUpgradeOfficial.targetList;
            if (item.index < 0 || item.index >= list.Count)
                return null;
            return list[item.index];
        }

        #endregion

        #region 左表（官职）交互

        /// <summary>单击官职行 = 选中为任命目标（同时刷新右表置灰）</summary>
        public void OnSelectOfficialItem(UIOfficialItem item)
        {
            if (item == null || cityUpgradeOfficial == null)
                return;

            currentOfficialItem = item;
            for (int i = 0; i < uiOfficialItems.Length; i++)
            {
                UIOfficialItem listItem = uiOfficialItems[i];
                if (listItem != null)
                    listItem.SetSelected(listItem == item && item.official != null);
            }

            RefreshDisableState();
            // 单击不改列表数据，走不到 UpdateContent，这里单独把详细说明刷出来
            RefreshOfficialEffectText();
        }

        /// <summary>双击官职行 = 免职（该人回到右侧空闲列表）</summary>
        public void OnDoubleSelectOfficialItem(UIOfficialItem item)
        {
            if (item == null || item.official == null || cityUpgradeOfficial == null)
                return;
            if (item.person == null)
                return;

            cityUpgradeOfficial.RemoveOfficial(item.official);
            UpdateContent();
        }

        #endregion

        #region 拖动

        /// <summary>开始拖动右侧武将：左侧所有不满足条件的官职置灰，并弹出跟随指针的名字标签</summary>
        public void OnBeginDragPerson(UIObjectListItem item)
        {
            dragPerson = GetPersonByItem(item);
            if (dragPerson != null)
                ShowDragText(dragPerson, Input.mousePosition);
            RefreshDisableState();
        }

        /// <summary>拖动中：名字标签跟着指针走（Unity 的拖拽事件在指针离开行之后仍会持续派发）</summary>
        public void OnDraggingPerson(UIObjectListItem item, Vector2 screenPosition)
        {
            MoveDragText(screenPosition);
        }

        /// <summary>拖动结束：收起名字标签；落在某个官职行上就入册</summary>
        public void OnEndDragPerson(UIObjectListItem item, Vector2 screenPosition)
        {
            HideDragText();

            Person person = dragPerson;
            dragPerson = null;
            RefreshDisableState();

            if (person == null || cityUpgradeOfficial == null)
                return;

            UIOfficialItem target = FindOfficialItemAt(screenPosition);
            if (target == null || target.official == null)
                return;

            if (!cityUpgradeOfficial.CanAssign(person, target.official))
            {
                Sango.Log.Warning($"【{person.Name}】不满足【{target.official.Name}】的任职条件");
                return;
            }

            cityUpgradeOfficial.Assign(person, target.official);
            currentOfficialItem = target;
            currentPersonItem = null;
            UpdateContent();
        }

        /// <summary>
        /// 屏幕坐标命中左侧哪一行官职。
        /// 用 RectTransform 矩形判断而不是 Raycast：左表是 Loop 列表，行上不一定有 raycastTarget，
        /// 且拖动时鼠标往往压在行的空白处。
        ///
        /// 【必须先过遮罩】官职行的 RectTransform 宽 947，而可视区只有 374（rectmask_2 裁剪），
        /// 直接按行矩形判断会让"拖到右侧武将表"也命中左五行，所以先用 RectMask2D 的矩形裁一道。
        /// </summary>
        UIOfficialItem FindOfficialItemAt(Vector2 screenPosition)
        {
            Camera cam = null;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;

            RectTransform clipRect = GetOfficialClipRect();
            if (clipRect != null
                && !RectTransformUtility.RectangleContainsScreenPoint(clipRect, screenPosition, cam))
                return null;

            for (int i = 0; i < uiOfficialItems.Length; i++)
            {
                UIOfficialItem item = uiOfficialItems[i];
                if (item == null || item.official == null)
                    continue;
                if (!item.gameObject.activeInHierarchy)
                    continue;

                RectTransform rt = item.transform as RectTransform;
                if (rt == null)
                    continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPosition, cam))
                    return item;
            }
            return null;
        }

        /// <summary>左表的可视裁剪矩形（rectmask_2），找不到就退化为不限裁剪</summary>
        RectTransform GetOfficialClipRect()
        {
            if (uiOfficialItems == null)
                return null;
            for (int i = 0; i < uiOfficialItems.Length; i++)
            {
                UIOfficialItem item = uiOfficialItems[i];
                if (item == null) continue;
                RectMask2D mask = item.GetComponentInParent<RectMask2D>();
                if (mask != null)
                    return mask.transform as RectTransform;
            }
            return null;
        }

        #endregion

        #region 拖拽浮动文本（dragTextItem）

        /// <summary>
        /// 弹出跟随指针的名字标签（预制体节点 dragTextItem 的子节点 name 显示武将名）。
        /// 宽度按文字实际宽度撑开、底图 bg 同步拉伸，避免四字以上姓名溢出错位。
        /// </summary>
        void ShowDragText(Person person, Vector2 screenPosition)
        {
            if (dragTextItem == null)
                InitDragTextItem();   // 兜底：正常流程 OnOpen 已初始化过

            if (dragTextItem == null || person == null)
                return;

            if (dragTextComp == null)
                dragTextComp = dragTextItem.GetComponent<UITextItem>();

            if (dragTextComp != null && dragTextComp.label != null)
            {
                dragTextComp.label.text = person.Name;

                float width = dragTextComp.label.preferredWidth + 16f;
                if (width < 76f)
                    width = 76f;   // 预制体原始宽度，短名字不缩得比底框还小
                dragTextComp.SetWidth(width);
                if (dragTextBg != null)
                    dragTextBg.sizeDelta = new Vector2(width + 1f, dragTextBg.sizeDelta.y);
            }

            if (!dragTextItem.gameObject.activeSelf)
                dragTextItem.gameObject.SetActive(true);

            // 浮在左右两个列表之上，否则拖动到列表边缘时会被行盖住
            dragTextItem.SetAsLastSibling();
            MoveDragText(screenPosition);
        }

        /// <summary>把名字标签摆到指针位置（标签左上角对齐，再往右下偏移一点，不压住光标）</summary>
        void MoveDragText(Vector2 screenPosition)
        {
            if (dragTextItem == null || !dragTextItem.gameObject.activeSelf)
                return;

            RectTransform parentRect = dragTextItem.parent as RectTransform;
            if (parentRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPosition, ResolveDragTextCamera(), out Vector2 local))
                return;

            // local 以父级 pivot 为原点，anchoredPosition 以"锚点参考点"为原点，两者相差一个锚点偏移。
            // 少了这一步，窗口根 rect 不是 0 尺寸时会整体偏出去（预制体里根节点尺寸是运行时才定的）。
            Vector2 anchorRef = Vector2.Scale(parentRect.rect.size, dragTextItem.anchorMin - parentRect.pivot);
            dragTextItem.anchoredPosition = local - anchorRef + DragTextOffset;
        }

        /// <summary>收起名字标签</summary>
        void HideDragText()
        {
            if (dragTextItem != null && dragTextItem.gameObject.activeSelf)
                dragTextItem.gameObject.SetActive(false);
        }

        /// <summary>
        /// 取屏幕坐标换算所需的相机。
        /// 用<b>根 Canvas</b> 的模式判定：窗口自带的 Canvas 是嵌套 Canvas，它的 renderMode 不生效，
        /// 真正决定渲染方式的是 UIRoot 上的根 Canvas。
        /// Overlay 必须传 null 相机，否则换算结果会整体偏移。
        /// </summary>
        Camera ResolveDragTextCamera()
        {
            if (dragTextCanvas == null && dragTextItem != null)
                dragTextCanvas = dragTextItem.GetComponentInParent<Canvas>();
            if (dragTextCanvas == null)
                return null;

            Canvas root = dragTextCanvas.rootCanvas;
            if (root == null)
                root = dragTextCanvas;

            if (root.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            if (root.worldCamera != null)
                return root.worldCamera;
            if (Sango.Core.Game.Instance != null && Sango.Core.Game.Instance.UICamera != null)
                return Sango.Core.Game.Instance.UICamera;
            return Camera.main;
        }

        #endregion

        #region 滚动条

        public void OnScrollBarValueChange(float value)
        {
            int count = cityUpgradeOfficial.targetList.Count;
            startIndex = (int)UnityEngine.Mathf.Lerp(0, System.Math.Max(0, count - itemCount), value);
            UpdateItemStartIndex(startIndex);
            RefreshDisableState();
        }

        public void OnScrollBar_H_ValueChange(float value)
        {
            float dis = (float)itemWidth - (float)maskRect.rect.width;
            float pos = -(int)UnityEngine.Mathf.Lerp(0, dis, value);
            foreach (RectTransform r in contentRect)
            {
                Vector2 p = r.anchoredPosition;
                p.x = pos;
                r.anchoredPosition = p;
            }

            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                if (listItem == null) continue;
                Vector2 p = listItem.contentRect.anchoredPosition;
                p.x = pos;
                listItem.contentRect.anchoredPosition = p;
            }
        }

        public void OnOfficial_ScrollBarValueChange(float value)
        {
            int count = cityUpgradeOfficial.upgradeList.Count;
            officialIndex = (int)UnityEngine.Mathf.Lerp(0, System.Math.Max(0, count - uiOfficialItems.Length), value);
            UpdateOfficialStartIndex(officialIndex);
            RefreshDisableState();
        }

        #endregion

        #region 已废弃的选择面板（预制体按钮仍绑着这两个方法，保留空实现）

        /// <summary>旧版"选择官职面板·决定"，已被"双击武将入册"取代</summary>
        public void OnSureSelectOfficial()
        {
            if (officialSelectPanel != null)
                officialSelectPanel.SetActive(false);
        }

        /// <summary>旧版"选择官职面板·返回"</summary>
        public void OnCancelSelectOfficial()
        {
            if (officialSelectPanel != null)
                officialSelectPanel.SetActive(false);
        }

        /// <summary>
        /// 旧版"选择官职面板"滚动条回调。预制体里 ofnode 的滑块仍绑着这个方法名，
        /// 面板虽然已经不用了，但保留空实现可以避免运行时点滑块报"方法找不到"。
        /// </summary>
        public void OnSelectOfficial_ScrollBarValueChange(float value)
        {
        }

        #endregion

        #region 决定 / 返回

        public void OnSure()
        {
            HideDragText();
            cityUpgradeOfficial.DoJob();
        }

        public void OnCancel()
        {
            HideDragText();
            cityUpgradeOfficial.Done();
        }

        #endregion
    }
}
