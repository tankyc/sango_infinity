using Sango.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UIObjectListItem : MonoBehaviour
    {
        public UITextItem idItem;
        public UITextItem textItem;
        public UISelectItem selectItem;
        List<UITextItem> pool = new List<UITextItem>();
        List<UITextItem> usedItems = new List<UITextItem>();
        public int index;
        public delegate void OnSelect(UIObjectListItem item);
        public delegate void OnShow(UIObjectListItem item);
        public OnSelect onSelected;
        public OnShow onShow;
        public RectTransform contentRect;
        public Image selectImg;
        public Image overImg;
        public Image pressImg;
        public bool hasId = false;

        void ScrollCellIndex(int idx)
        {
            index = idx;
            onShow?.Invoke(this);
        }
        public void OnClick()
        {
            onSelected?.Invoke(this);
        }

        public void Clear()
        {
            selectItem.SetVisible(false);
            textItem.SetText("");
            for (int i = 0; i < usedItems.Count; i++)
            {
                usedItems[i].gameObject.SetActive(false);
                pool.Add(usedItems[i]);
            }
            usedItems.Clear();
        }

        public void Set(string content, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            selectItem.SetVisible(!string.IsNullOrEmpty(content));
            textItem.SetText(content);
            textItem.SetObjectSortTitle(objectSortTitle);
            textItem.SetObject(sangoObject);
        }
        public void SetId(string content, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            if (idItem == null) return;
            idItem.gameObject.SetActive(hasId);
            idItem.SetText(content);
            idItem.SetObjectSortTitle(objectSortTitle);
            idItem.SetObject(sangoObject);
        }



        public void Add(string content, float width, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            UITextItem item;
            if (pool.Count == 0)
            {
                GameObject obj = GameObject.Instantiate(textItem.gameObject, contentRect);
                item = obj.GetComponent<UITextItem>();
            }
            else
            {
                item = pool[0];
                pool.RemoveAt(0);
            }
            usedItems.Add(item);
            item.gameObject.SetActive(true);
            item.SetWidth(width).SetText(content);
            item.transform.SetAsLastSibling();
            item.SetObjectSortTitle(objectSortTitle);
            item.SetObject(sangoObject);
        }

        public void Add(string content, float width, int alignment, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            UITextItem item;
            if (pool.Count == 0)
            {
                GameObject obj = GameObject.Instantiate(textItem.gameObject, contentRect);
                item = obj.GetComponent<UITextItem>();
            }
            else
            {
                item = pool[0];
                pool.RemoveAt(0);
            }
            usedItems.Add(item);
            item.gameObject.SetActive(true);
            item.SetWidth(width).SetText(content).SetAlignment((TextAnchor)alignment);
            item.transform.SetAsLastSibling();
            item.SetObjectSortTitle(objectSortTitle);
            item.SetObject(sangoObject);
        }

        public void Set(int index, string content, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            if(hasId)
            {
                if (index == 0)
                {
                    SetId(content, objectSortTitle, sangoObject);
                }
                else if (index == 1)
                {
                    Set(content, objectSortTitle, sangoObject);
                }
                else
                {
                    UITextItem item = usedItems[index - 2];
                    item.SetText(content);
                    item.SetObjectSortTitle(objectSortTitle);
                    item.SetObject(sangoObject);
                }
            }
            else
            {
                if (index == 0)
                    Set(content, objectSortTitle, sangoObject);
                else
                {
                    UITextItem item = usedItems[index - 1];
                    item.SetText(content);
                    item.SetObjectSortTitle(objectSortTitle);
                    item.SetObject(sangoObject);
                }
            }
           
        }

        public void Set(SangoObject sangoObject, List<ObjectSortTitle> objectSorts)
        {
            for (int i = 0; i < objectSorts.Count; i++)
            {
                ObjectSortTitle sortTitle = objectSorts[i];
                string content = sangoObject == null ? "" : sortTitle.GetValueStr(sangoObject);
                if(hasId )
                {
                    if (i == 0)
                    {
                        SetId(content, sortTitle, sangoObject);
                        continue;
                    }
                    else if (i == 1)
                    {
                        Set(content, sortTitle, sangoObject);
                        continue;
                    }

                    UITextItem item = usedItems[i - 2];
                    item.SetText(content);
                    item.SetObjectSortTitle(sortTitle);
                    item.SetObject(sangoObject);
                }
                else
                {
                    if (i == 0)
                    {
                        Set(content, sortTitle, sangoObject);
                        continue;
                    }

                    UITextItem item = usedItems[i - 1];
                    item.SetText(content);
                    item.SetObjectSortTitle(sortTitle);
                    item.SetObject(sangoObject);
                }
               
            }
        }

        public void SetSelected(bool b)
        {
            selectItem?.SetSelected(b);
            selectImg.enabled = b;
        }

        public bool IsSelected()
        {
            return selectImg.enabled;
        }

        public void SetOver(bool b)
        {
            overImg.enabled = b;
        }

        public void SetPressd(bool b)
        {
            pressImg.enabled = b;
        }

    }
}