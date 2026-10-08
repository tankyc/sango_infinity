using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIObjectList : MonoBehaviour
    {
        public Scrollbar scrollbar;
        public ObjectSortTitle targetObjectSortTitle;
        public List<SangoObject> objectDatas = new List<SangoObject>();
        public UIObjectListItem[] uIObjectListItems;
        UIObjectListItem currentSelectItem;
        RectTransform[] uIObjectListItemsRect;

        public RectTransform sliderRect;
        public RectTransform contentRect;
        float content_width;
        public delegate void OnObjectSelected(int index);
        protected OnObjectSelected onObjectSelected;
        protected int startIndex = 0;
        protected int itemCount = 0;
        protected int totalCount = 0;
        protected int selectedIndex = 0;

        private void Awake()
        {
            itemCount = uIObjectListItems.Length;
            content_width = contentRect.rect.width;
            uIObjectListItemsRect = new RectTransform[uIObjectListItems.Length];
            for (int i = 0; i < uIObjectListItems.Length; i++)
            {
                uIObjectListItemsRect[i] = uIObjectListItems[i].GetComponent<RectTransform>();
            }
        }

        public void Init(List<SangoObject> objectDatas, ObjectSortTitle objectSortTitle, OnObjectSelected onObjectSelected)
        {
            this.objectDatas = objectDatas;
            targetObjectSortTitle = objectSortTitle;
            totalCount = objectDatas.Count;
            if (totalCount < itemCount)
            {
                sliderRect.gameObject.SetActive(false);
                Vector2 size = contentRect.sizeDelta;
                size.x = content_width + 15;
                contentRect.sizeDelta = size;
            }
            else
            {
                sliderRect.gameObject.SetActive(true);
                scrollbar.size = Mathf.Max(0.1f, (float)itemCount / (float)totalCount);
                scrollbar.SetValueWithoutNotify(0);
                Vector2 size = contentRect.sizeDelta;
                size.x = content_width;
                contentRect.sizeDelta = size;
            }
            startIndex = 0;
            selectedIndex = 0;
            this.onObjectSelected = onObjectSelected;
            OnScrollBarValueChange(0);
        }

        public void SelectDefaultObject(SangoObject obj)
        {
            for(int i = 0; i < totalCount; i++)
            {
                if (objectDatas[i] == obj)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if(startIndex + itemCount <= selectedIndex)
            {
                startIndex = selectedIndex - itemCount + 1;
                scrollbar.SetValueWithoutNotify((float)startIndex / (totalCount - itemCount));
            }

            UpdateItemStartIndex(startIndex);
        }

        public void OnScrollBarValueChange(float value)
        {
            startIndex = (int)UnityEngine.Mathf.Lerp(0, totalCount - itemCount, value);
            UpdateItemStartIndex(startIndex);
        }

        public void UpdateItemStartIndex(int startIndex)
        {
            for (int i = 0; i < itemCount; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                int destIndex = i + startIndex;
                listItem.index = destIndex;
                if (destIndex < totalCount)
                {
                    listItem.SetSelected(selectedIndex == destIndex);
                    listItem.textItem.label.text = targetObjectSortTitle.GetValueStr(objectDatas[destIndex]);
                }
                else
                {
                    listItem.SetSelected(false);
                    listItem.textItem.label.text = "";
                }
            }
        }

        public void UpShow()
        {
            if (startIndex > 0)
                startIndex--;
            UpdateItemStartIndex(startIndex);
            scrollbar.SetValueWithoutNotify((float)startIndex / (totalCount - itemCount));
        }

        public void DownShow()
        {
            if (startIndex < totalCount - itemCount)
                startIndex++;

            UpdateItemStartIndex(startIndex);
            scrollbar.SetValueWithoutNotify((float)startIndex / (totalCount - itemCount));
        }


        public void OnObjectListItemPressDown(UIObjectListItem item)
        {
            item.SetPressd(true);
            currentSelectItem = item;
        }

        public void OnObjectListItemPressUp(UIObjectListItem item)
        {
            item.SetPressd(false);
            for (int i = 0; i < itemCount; i++)
            {
                RectTransform itemRect = uIObjectListItemsRect[i];
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem == currentSelectItem && RectTransformUtility.RectangleContainsScreenPoint(itemRect, Input.mousePosition, Sango.Core.Game.Instance.UICamera))
                {
                    OnItemSelected(item);
                    break;
                }
            }
        }

        public void OnObjectListItemPointEnter(UIObjectListItem item)
        {
            item.SetOver(true);
        }
        public void OnObjectListItemPointExit(UIObjectListItem item)
        {
            item.SetOver(false);
        }

        public virtual void OnItemSelected(UIObjectListItem item)
        {
            if (item.index >= totalCount)
                return;

            int destIndex = item.index;
            if (destIndex == selectedIndex)
                return;

            for (int i = 0; i < itemCount; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem.index == selectedIndex)
                {
                    listItem.SetSelected(false);
                    break;
                }
            }
            selectedIndex = destIndex;
            item.SetSelected(true);
            onObjectSelected?.Invoke(selectedIndex);
        }
    }
}