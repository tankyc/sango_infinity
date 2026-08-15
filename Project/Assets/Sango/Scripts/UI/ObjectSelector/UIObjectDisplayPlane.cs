using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UIObjectDisplayPlane : MonoBehaviour
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
            if(OnSelectCall != null)
            {
                if (currentSelect != null)
                {
                    currentSelect.SetSelected(false);
                }
                currentSelect = listItem;
                currentSelect.SetSelected(true);
                OnSelectCall.Invoke(currentSelect.index);
            }
            else if(OnMultiSelectCall != null)
            {
                if(multiSelectList.Contains(listItem.index))
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

            for (int i = sortItems.Count - 1; i < sortButtonPool.Count; i++)
                sortButtonPool[i].gameObject.SetActive(false);
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
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        ObjectSortTitle sortTitle = sortItems[j];
                        listItem.Set(j, sortTitle.GetValueStr(sango));
                    }
                }
                else
                {
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        ObjectSortTitle sortTitle = sortItems[j];
                        listItem.Set(j, "");
                    }
                }
                listItem.index = i + startIndex;
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
