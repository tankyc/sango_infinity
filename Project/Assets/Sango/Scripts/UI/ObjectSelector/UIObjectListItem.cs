using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UIObjectListItem : MonoBehaviour
    {
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

        public void Set(string content)
        {
            selectItem.SetVisible(!string.IsNullOrEmpty(content));
            textItem.SetText(content);
        }

        public void Add(string content, float width)
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
        }

        public void Add(string content, float width, int alignment)
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
        }

        public void Set(int index, string content)
        {
            if (index == 0)
                Set(content);
            else
                usedItems[index - 1].SetText(content);
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