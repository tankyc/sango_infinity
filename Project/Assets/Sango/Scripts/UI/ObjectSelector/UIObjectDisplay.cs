using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UIObjectDisplay : UGUIWindow
    {
        public Text title;
        public List<ObjectSortTitle> sortItems;
        public UIToggleItem[] toggleGroup;
        public UIObjectListItem[] uIObjectListItems;
        public UIObjectListItem creatItemObj;
        public Scrollbar scrollbar;
        public Scrollbar scrollbar_h;
        protected List<UISortButton> sortButtonPool = new List<UISortButton>();
        public UISortButton sortTitleItem;
        public GameObject selectSortBtn;
        public RectTransform sorltTitleTransform;
        public RectTransform maskRect;
        public RectTransform[] contentRect;
        ObjectsDisplaySystem objectSelectSystem;
        protected int startIndex = 0;
        protected float itemWidth = 0;
        protected int itemCount = 0;

        public RectTransform btnRoot;
        public UIButtonItem[] buttons;
        public Button sureBtn;

        bool clickMode = false;

        protected override void Awake()
        {
            for (int i = 0; i < toggleGroup.Length; i++)
            {
                toggleGroup[i].toggle.onValueChanged.RemoveAllListeners();
                int btIndex = i;
                toggleGroup[i].toggle.onValueChanged.AddListener((b) => OnSortGroupChanged(btIndex, b));
            }
        }

        protected UISortButton CreateSortButtonItem()
        {
            GameObject btn = GameObject.Instantiate(sortTitleItem.gameObject, sorltTitleTransform);
            UISortButton sortBtn = btn.GetComponent<UISortButton>();
            sortButtonPool.Add(sortBtn);
            return sortBtn;
        }
        public override void OnOpen(params object[] objects)
        {
            this.objectSelectSystem = objects[0] as ObjectsDisplaySystem;
            Init(objectSelectSystem);
        }

        public virtual void Init(ObjectsDisplaySystem objectSelectSystem)
        {
            this.objectSelectSystem = objectSelectSystem;
            // 反向绑定：过滤（搜索 / 勾选联动）后由系统直接驱动本窗口重绘，
            // 不依赖 WindowInterface 是否已赋值
            if (objectSelectSystem != null)
                objectSelectSystem.BindDisplay(this);
            title.text = objectSelectSystem.customSortTitleName;
            sortItems = objectSelectSystem.customSortItems;

            // 点选模式
            clickMode = objectSelectSystem.ClickMode;
            selectSortBtn.SetActive(!clickMode);
            sureBtn?.gameObject.SetActive(!clickMode);

            itemCount = uIObjectListItems.Length;
            itemWidth = GetContentWidth();
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

            toggleGroup[0].SetTitle(objectSelectSystem.customSortTitleName).toggle.isOn = true;
            for (int i = 1; i < toggleGroup.Length; i++)
            {
                string groupName = objectSelectSystem.GetSortTitleGroupName(i);
                if (string.IsNullOrEmpty(groupName))
                {
                    toggleGroup[i].gameObject.SetActive(false);
                }
                else
                {
                    toggleGroup[i].SetTitle(groupName);
                    toggleGroup[i].gameObject.SetActive(true);
                }
            }

            startIndex = 0;
            int dataCount = objectSelectSystem.Objects.Count;
            if (dataCount < itemCount)
            {
                scrollbar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                scrollbar.transform.parent.gameObject.SetActive(true);
                scrollbar.size = System.Math.Max(0.1f, (float)itemCount / (float)dataCount);
                scrollbar.SetValueWithoutNotify(0);
            }

            // 重置状态和位置
            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                Vector2 p = listItem.contentRect.anchoredPosition;
                p.x = 0;
                listItem.contentRect.anchoredPosition = p;
                listItem.selectItem.gameObject.SetActive(!clickMode);
                listItem.SetOver(false);
            }

            UpdateSortContent();
            OnScrollBarValueChange(0);

            foreach (RectTransform r in contentRect)
            {
                Vector2 p = r.anchoredPosition;
                p.x = 0;
                r.anchoredPosition = p;
            }

            bool hasButtons = objectSelectSystem.buttonDatas != null;
            btnRoot.gameObject.SetActive(hasButtons);
            if (hasButtons)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    UIButtonItem buttonItem = buttons[i];
                    if (i >= objectSelectSystem.buttonDatas.Count)
                    {
                        buttonItem.gameObject.SetActive(false);
                        continue;
                    }
                    ObjectsDisplaySystem.ButtonData buttonData = objectSelectSystem.buttonDatas[i];
                    buttonItem.gameObject.SetActive(true);
                    buttonItem.SetTitle(buttonData.title).SetStyle(buttonData.style).BindAction(buttonData.action);
                }
            }

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
            itemWidth = GetContentWidth();
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

            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
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
                    // 排序同时作用于"完整候选集"与"当前显示列表"：
                    // 否则清空搜索后顺序会跳回旧值，与玩家刚看到的排列对不上
                    ObjectSelectSystem selectSystem = objectSelectSystem as ObjectSelectSystem;
                    if (selectSystem != null)
                    {
                        selectSystem.SortObjects(sortTitle.Sort, up);
                    }
                    else
                    {
                        // 非 ObjectSelectSystem 的视图只有显示列表，走统一的确定性排序
                        ObjectSortTitle.SortList(objectSelectSystem.Objects, sortTitle.Sort, up);
                    }
                    scrollbar.SetValueWithoutNotify(0);
                    OnScrollBarValueChange(0);
                };

                if (i > 0)
                {
                    for (int j = 0; j < itemCount; j++)
                    {
                        UIObjectListItem listItem = uIObjectListItems[j];
                        listItem.Add("", sortTitle.ContentMaxWidth, sortTitle.alignment);
                    }
                }
                else
                {
                    for (int j = 0; j < itemCount; j++)
                    {
                        UIObjectListItem listItem = uIObjectListItems[j];
                        listItem.textItem.SetWidth(sortTitle.ContentMaxWidth);
                    }
                }
            }

            // 【负下标防护】sortItems 为空是完全合法的调用：人物创建界面的"官职选择"
            // 就是用 new List<ObjectSortTitle>() 打开选择窗
            // （UIPersonCreateDetail.OpenOfficialSelect，不需要任何排序表头）。
            // 原写法从 sortItems.Count - 1 = -1 起遍历，第一轮就取 sortButtonPool[-1]，
            // 抛 ArgumentOutOfRangeException（实测栈：OpenOfficialSelect → ObjectSelectSystem.Start
            // → UIObjectSelector.Init → UpdateSortContent:249），选官职窗口直接打不开。
            // 空表时从 0 开始、把整排按钮（含首列的 sortTitleItem）全部关掉即可。
            if (sortItems.Count == 0)
            {
                if (sortTitleItem != null)
                    sortTitleItem.gameObject.SetActive(false);
                for (int i = 0; i < sortButtonPool.Count; i++)
                    sortButtonPool[i].gameObject.SetActive(false);
                return;
            }

            for (int i = sortItems.Count - 1; i < sortButtonPool.Count; i++)
                sortButtonPool[i].gameObject.SetActive(false);
        }

        public override void OnRefresh()
        {
            base.OnRefresh();
            // 重置状态和位置
            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                listItem.SetOver(false);
            }
            UpdateItemStartIndex(startIndex);
        }

        /// <summary>
        /// 数据源被过滤后重绘:
        /// 修正滚动条并尽量保持玩家当前的浏览位置
        /// </summary>
        public void RefreshByFilter()
        {
            if (objectSelectSystem == null || objectSelectSystem.Objects == null) return;

            // 搜索状态下强制回到列表顶部：沿用上一处滚动位置会把最前面的命中项
            // 藏在视野之外，玩家会以为"没有列出所有含该字的武将"
            if (objectSelectSystem.HasSearchKeyword)
                startIndex = 0;

            int dataCount = objectSelectSystem.Objects.Count;
            if (itemCount <= 0 || dataCount <= itemCount)
            {
                scrollbar.transform.parent.gameObject.SetActive(false);
                startIndex = 0;
            }
            else
            {
                scrollbar.transform.parent.gameObject.SetActive(true);
                scrollbar.size = System.Math.Max(0.1f, (float)itemCount / (float)dataCount);
                int maxStart = dataCount - itemCount;
                if (startIndex > maxStart) startIndex = maxStart;
                if (startIndex < 0) startIndex = 0;
                scrollbar.SetValueWithoutNotify((float)startIndex / (float)maxStart);
            }

            for (int j = 0; j < uIObjectListItems.Length; j++)
                uIObjectListItems[j].SetOver(false);

            UpdateItemStartIndex(startIndex);
        }

        public void UpShow()
        {
            if (startIndex > 0)
                startIndex--;
            else
                startIndex = 0;

            UpdateItemStartIndex(startIndex);
            ApplyScrollbarValue();
        }

        public void DownShow()
        {
            int maxStart = GetMaxStartIndex();
            if (startIndex < maxStart)
                startIndex++;

            UpdateItemStartIndex(startIndex);
            ApplyScrollbarValue();
        }

        public void OnScrollBarValueChange(float value)
        {
            // 【下标防护】候选数少于可视行数时 (Objects.Count - itemCount) 是负数，
            // Mathf.Lerp(0, 负数, value) 会得到负下标，随后 UpdateItemStartIndex 取
            // Objects[负下标] 直接抛 ArgumentOutOfRangeException
            //（实测栈：UIObjectDisplay.UpShow → UIObjectSelector.UpdateItemStartIndex:98）。
            // 统一按 [0, maxStart] 夹取，maxStart 不足时固定为 0。
            int maxStart = GetMaxStartIndex();
            startIndex = (int)UnityEngine.Mathf.Lerp(0, maxStart, value);
            if (startIndex < 0) startIndex = 0;
            if (startIndex > maxStart) startIndex = maxStart;
            UpdateItemStartIndex(startIndex);
        }

        /// <summary>
        /// 可滚动的最大起始下标（候选不足一屏时为 0，不会是负数）。
        /// </summary>
        /// <returns>最大起始下标</returns>
        int GetMaxStartIndex()
        {
            if (objectSelectSystem == null || objectSelectSystem.Objects == null)
                return 0;
            int maxStart = objectSelectSystem.Objects.Count - itemCount;
            return maxStart > 0 ? maxStart : 0;
        }

        /// <summary>
        /// 把当前 startIndex 同步到滚动条；maxStart 为 0 时直接归零，避免除零得到 NaN。
        /// </summary>
        void ApplyScrollbarValue()
        {
            int maxStart = GetMaxStartIndex();
            if (maxStart <= 0)
            {
                scrollbar.SetValueWithoutNotify(0);
                return;
            }
            scrollbar.SetValueWithoutNotify((float)startIndex / (float)maxStart);
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
                Vector2 p = listItem.contentRect.anchoredPosition;
                p.x = pos;
                listItem.contentRect.anchoredPosition = p;
            }
        }


        public virtual void UpdateItemStartIndex(int startIndex)
        {
            if (objectSelectSystem == null || objectSelectSystem.Objects == null)
                return;

            // 【下标防护】与 UIObjectSelector.UpdateItemStartIndex 同因：
            // 负下标 / 超出数据范围时退化为空行，不能让 Objects[i + startIndex] 抛越界。
            if (startIndex < 0) startIndex = 0;
            int dataCount = objectSelectSystem.Objects.Count;

            for (int i = 0; i < itemCount; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                int destIndex = i + startIndex;
                listItem.index = destIndex;
                if (destIndex < 0 || destIndex >= dataCount)
                    continue;

                SangoObject sango = objectSelectSystem.Objects[destIndex];
                for (int j = 0; j < sortItems.Count; j++)
                {
                    ObjectSortTitle sortTitle = sortItems[j];
                    listItem.Set(j, sortTitle.GetValueStr(sango));
                }
            }
        }


        public override void OnClose()
        {
            base.OnClose();
            for (int i = 0; i < sortButtonPool.Count; i++)
                sortButtonPool[i].gameObject.SetActive(false);
        }

        public void OnCancel()
        {
            objectSelectSystem.OnCancel();
        }

        public void OnSortGroupChanged(int index, bool b)
        {
            if (b)
            {
                sortItems = objectSelectSystem.GetSortTitleGroup(index);
                UpdateSortContent();
                OnScrollBarValueChange(0);
            }

        }

        public virtual void Update()
        {
            Vector2 scrollWheel = Input.mouseScrollDelta;
            if (scrollWheel.y > 0)
            {
                UpShow();
            }
            else if (scrollWheel.y < 0)
            {
                DownShow();
            }
        }
    }
}
