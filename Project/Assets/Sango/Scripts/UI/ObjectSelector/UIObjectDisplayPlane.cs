using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UIObjectDisplayPlane : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// 点选模式
        /// </summary>
        public List<SangoObject> Objects;
        public List<ObjectSortTitle> sortItems;

        public UIObjectListItem[] uIObjectListItems;
        public UIObjectListItem creatItemObj;
        public Scrollbar scrollbar;
        public Scrollbar scrollbar_h;
        protected List<UISortButton> sortButtonPool = new List<UISortButton>();
        public UISortButton sortTitleItem;
        public UISortButton idTitleItem;
        public GameObject selectSortBtn;
        public RectTransform sorltTitleTransform;
        public RectTransform maskRect;
        public RectTransform[] contentRect;
        protected int startIndex = 0;
        protected float itemWidth = 0;
        protected int itemCount = 0;
        UIObjectListItem currentSelect;
        public bool clickMode = false;
        public Action<int> OnSelectCall;
        public Action<List<int>> OnMultiSelectCall;
        List<int> multiSelectList = new List<int>();
        public bool hasId = false;
        RectTransform[] uIObjectListItemsRect;
        bool isMouseOver = false;
        protected void Awake()
        {
            uIObjectListItemsRect = new RectTransform[uIObjectListItems.Length];
            for (int i = 0; i < uIObjectListItems.Length; i++)
            {
                uIObjectListItemsRect[i] = uIObjectListItems[i].GetComponent<RectTransform>();
            }
        }

        protected UISortButton CreateSortButtonItem()
        {
            GameObject btn = GameObject.Instantiate(sortTitleItem.gameObject, sorltTitleTransform);
            UISortButton sortBtn = btn.GetComponent<UISortButton>();
            sortButtonPool.Add(sortBtn);
            return sortBtn;
        }

        public virtual void Init(List<SangoObject> datas, List<ObjectSortTitle> sortItems, bool clickMode = false)
        {
            this.Objects = datas;
            this.sortItems = sortItems;
            multiSelectList.Clear();
            // 点选模式
            this.clickMode = clickMode;

            selectSortBtn.SetActive(!clickMode);

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

            startIndex = 0;
            int dataCount = Objects.Count;
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
                listItem.SetSelected(false);
                listItem.selectItem.gameObject.SetActive(!clickMode);
                listItem.SetOver(false);
                listItem.onSelected = OnSelect;
                listItem.hasId = hasId;
            }

            UpdateSortContent();
            OnScrollBarValueChange(0);

            foreach (RectTransform r in contentRect)
            {
                Vector2 p = r.anchoredPosition;
                p.x = 0;
                r.anchoredPosition = p;
            }
        }

        public void OnSelect(UIObjectListItem listItem)
        {
            if (listItem.index >= Objects.Count)
                return;

            if (OnSelectCall != null)
            {
                if (currentSelect != null)
                {
                    currentSelect.SetSelected(false);
                }
                currentSelect = listItem;
                currentSelect.SetSelected(true);
                OnSelectCall.Invoke(currentSelect.index);
            }
            else if (OnMultiSelectCall != null)
            {
                if (multiSelectList.Contains(listItem.index))
                {
                    listItem.SetSelected(false);
                    multiSelectList.Remove(listItem.index);
                }
                else
                {
                    listItem.SetSelected(true);
                    multiSelectList.Add(listItem.index);
                }
                OnMultiSelectCall.Invoke(multiSelectList);
            }
        }

        public void GetSelectObjects(List<SangoObject> list)
        {
            if (OnSelectCall != null)
            {
                if (currentSelect.index < Objects.Count)
                    list.Add(Objects[currentSelect.index]);
            }
            else if (OnMultiSelectCall != null)
            {
                for (int i = 0; i < multiSelectList.Count; i++)
                {
                    int objIndex = multiSelectList[i];
                    if (objIndex < Objects.Count)
                        list.Add(Objects[objIndex]);
                }
            }
        }

        void Call()
        {
            if (OnSelectCall != null)
            {
                OnSelectCall.Invoke(currentSelect.index);
            }
            else if (OnMultiSelectCall != null)
            {
                OnMultiSelectCall.Invoke(multiSelectList);
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
            idTitleItem?.gameObject.SetActive(hasId);
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

            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                listItem.Clear();
            }
            for (int i = 0; i < sortItems.Count; i++)
            {
                ObjectSortTitle sortTitle = sortItems[i];
                UISortButton uIPersonSortButton;

                if (hasId)
                {
                    if (i == 0)
                    {
                        uIPersonSortButton = idTitleItem;
                    }
                    else if (i == 1)
                    {
                        uIPersonSortButton = sortTitleItem;
                    }
                    else
                    {
                        if (i - 2 < sortButtonPool.Count)
                            uIPersonSortButton = sortButtonPool[i - 2];
                        else
                            uIPersonSortButton = CreateSortButtonItem();
                    }

                    uIPersonSortButton.gameObject.SetActive(true);
                    uIPersonSortButton.Clear().SetWidth(sortTitle.ContentMaxWidth).SetName(sortTitle.name);

                    uIPersonSortButton.onClick = (up) =>
                    {
                        Objects.Sort(sortTitle.Sort);
                        if (!up) Objects.Reverse();
                        scrollbar.SetValueWithoutNotify(0);
                        OnScrollBarValueChange(0);
                    };


                    if (i == 0)
                    {
                        for (int j = 0; j < itemCount; j++)
                        {
                            UIObjectListItem listItem = uIObjectListItems[j];
                            listItem.idItem.SetWidth(sortTitle.ContentMaxWidth);
                        }
                    }
                    else if (i == 1)
                    {
                        for (int j = 0; j < itemCount; j++)
                        {
                            UIObjectListItem listItem = uIObjectListItems[j];
                            listItem.textItem.SetWidth(sortTitle.ContentMaxWidth);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < itemCount; j++)
                        {
                            UIObjectListItem listItem = uIObjectListItems[j];
                            listItem.Add("", sortTitle.ContentMaxWidth, sortTitle.alignment);
                        }
                    }
                }
                else
                {
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
                        Objects.Sort(sortTitle.Sort);
                        if (!up) Objects.Reverse();
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
            }
            if (hasId)
            {
                for (int i = sortItems.Count - 2; i < sortButtonPool.Count; i++)
                    sortButtonPool[i].gameObject.SetActive(false);
            }
            else
            {
                for (int i = sortItems.Count - 1; i < sortButtonPool.Count; i++)
                    sortButtonPool[i].gameObject.SetActive(false);
            }
        }

        public void OnRefresh()
        {
            // 重置状态和位置
            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                listItem.SetOver(false);
            }

            UpdateItemStartIndex(startIndex);
        }

        public void UpShow()
        {
            if (startIndex > 0)
                startIndex--;
            UpdateItemStartIndex(startIndex);
            scrollbar.SetValueWithoutNotify((float)startIndex / (Objects.Count - itemCount));
        }

        public void DownShow()
        {
            if (startIndex < Objects.Count - itemCount)
                startIndex++;

            UpdateItemStartIndex(startIndex);
            scrollbar.SetValueWithoutNotify((float)startIndex / (Objects.Count - itemCount));
        }

        public void OnScrollBarValueChange(float value)
        {
            startIndex = (int)UnityEngine.Mathf.Lerp(0, Objects.Count - itemCount, value);
            UpdateItemStartIndex(startIndex);
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
            for (int i = 0; i < itemCount; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                if (i < Objects.Count)
                {
                    SangoObject sango = Objects[i + startIndex];
                    listItem.Set(sango, sortItems);
                }
                else
                {
                    listItem.Set(null, sortItems);
                }
                listItem.index = i + startIndex;

                // 多选模式下同步选中高亮,避免滚动后选中标记错位
                if (OnMultiSelectCall != null)
                {
                    listItem.SetSelected(multiSelectList.Contains(listItem.index));
                }
            }
        }

        /// <summary>
        /// 程序化选中指定索引的对象 - 取消旧选中、滚动到可视区域并触发选中回调
        /// </summary>
        /// <param name="index">目标索引</param>
        public void SelectIndex(int index)
        {
            if (Objects == null || index < 0 || index >= Objects.Count)
                return;

            // 取消旧选中
            if (currentSelect != null)
            {
                currentSelect.SetSelected(false);
                currentSelect = null;
            }

            // 若目标不在当前可视范围内,滚动到目标位置
            if (index < startIndex || index >= startIndex + itemCount)
            {
                int maxStart = Objects.Count - itemCount;
                if (maxStart < 0) maxStart = 0;
                int targetStart = index - itemCount + 1;
                if (targetStart < 0) targetStart = 0;
                if (targetStart > maxStart) targetStart = maxStart;
                float value = maxStart > 0 ? (float)targetStart / (float)maxStart : 0f;
                scrollbar.SetValueWithoutNotify(value);
                OnScrollBarValueChange(value);
            }

            // 高亮对应列表项
            for (int i = 0; i < uIObjectListItems.Length; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem != null && listItem.index == index)
                {
                    currentSelect = listItem;
                    listItem.SetSelected(true);
                    break;
                }
            }

            if (OnSelectCall != null)
                OnSelectCall.Invoke(index);
        }

        /// <summary>
        /// 同步多选选中状态到可视列表项
        /// </summary>
        protected void SyncMultiSelectVisual()
        {
            for (int i = 0; i < uIObjectListItems.Length; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem != null)
                {
                    listItem.SetSelected(multiSelectList.Contains(listItem.index));
                }
            }
        }

        /// <summary>
        /// 一键全选 - 选中当前列表中的全部对象(仅多选模式有效)
        /// </summary>
        public void SelectAll()
        {
            if (Objects == null || OnMultiSelectCall == null)
                return;

            multiSelectList.Clear();
            for (int i = 0; i < Objects.Count; i++)
            {
                multiSelectList.Add(i);
            }
            SyncMultiSelectVisual();
            OnMultiSelectCall.Invoke(multiSelectList);
        }

        /// <summary>
        /// 一键取消 - 清空当前全部选中(仅多选模式有效)
        /// </summary>
        public void UnSelectAll()
        {
            if (OnMultiSelectCall == null)
                return;

            multiSelectList.Clear();
            SyncMultiSelectVisual();
            OnMultiSelectCall.Invoke(multiSelectList);
        }

        /// <summary>
        /// 程序化设置多选索引 - 用于列表刷新后恢复选中状态
        /// </summary>
        /// <param name="indexes">目标索引集合</param>
        public void SetMultiSelect(List<int> indexes)
        {
            multiSelectList.Clear();
            if (indexes != null)
            {
                for (int i = 0; i < indexes.Count; i++)
                {
                    int index = indexes[i];
                    if (Objects != null && index >= 0 && index < Objects.Count && !multiSelectList.Contains(index))
                    {
                        multiSelectList.Add(index);
                    }
                }
            }
            SyncMultiSelectVisual();
            if (OnMultiSelectCall != null)
            {
                OnMultiSelectCall.Invoke(multiSelectList);
            }
        }

        public void OnClose()
        {
            for (int i = 0; i < sortButtonPool.Count; i++)
                sortButtonPool[i].gameObject.SetActive(false);
        }

        public void OnCancel()
        {

        }

        public void Update()
        {
            if (!isMouseOver) return;

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
        bool dragFlag = false;
        UIObjectListItem currentSelectItem;

        public void OnPersonListItemPressDown(UIObjectListItem item)
        {
            if (OnMultiSelectCall == null)
                return;
            item.SetPressd(true);
            currentSelectItem = item;
            dragFlag = !item.IsSelected();
        }


        public void OnPersonListSelected(UIObjectListItem item)
        {
            OnSelect(item);
        }

        public void OnPersonListItemPressUp(UIObjectListItem item)
        {
            if (OnMultiSelectCall == null)
                return;
            item.SetPressd(false);
            Call();
            if (Input.GetMouseButtonUp(1))
                return;

            //for (int i = 0; i < itemCount; i++)
            //{
            //    RectTransform itemRect = uIObjectListItemsRect[i];
            //    UIObjectListItem listItem = uIObjectListItems[i];
            //    if (listItem == currentSelectItem && RectTransformUtility.RectangleContainsScreenPoint(itemRect, Input.mousePosition, Sango.Core.Game.Instance.UICamera))
            //    {
            //        OnPersonListSelected(item);
            //        break;
            //    }
            //}
        }

        public void OnDragPersonListSelected(UIObjectListItem item)
        {
            if (OnMultiSelectCall == null)
                return;

            if (item.index >= Objects.Count)
                return;

            if (item.IsSelected() && !dragFlag)
            {
                item.SetSelected(false);
                multiSelectList.Remove(item.index);
            }
            else if (!item.IsSelected() && dragFlag)
            {
                if (item.index < Objects.Count)
                {
                    item.SetSelected(true);
                    multiSelectList.Add(item.index);
                }
            }

            for (int i = 0; i < itemCount; i++)
            {
                RectTransform itemRect = uIObjectListItemsRect[i];
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem != item && RectTransformUtility.RectangleContainsScreenPoint(itemRect, Input.mousePosition, Sango.Core.Game.Instance.UICamera))
                {
                    if (listItem.index >= Objects.Count)
                        return;

                    if (listItem.IsSelected() && !dragFlag)
                    {
                        listItem.SetSelected(false);
                        multiSelectList.Remove(listItem.index);
                    }
                    else if (!listItem.IsSelected() && dragFlag)
                    {
                        listItem.SetSelected(true);
                        multiSelectList.Add(listItem.index);
                    }
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isMouseOver = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isMouseOver = false;
        }
    }
}
