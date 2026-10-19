using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UITextItem : MonoBehaviour
    {
        public Text label;
        public Image image;
        public ObjectSortTitle objectSortTitle;
        public SangoObject obj;
        public UITextItem SetWidth(float width)
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);
            LayoutElement layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.preferredWidth = width;
            return this;
        }

        public void SetObjectSortTitle(ObjectSortTitle objectSortTitle)
        {
            this.objectSortTitle = objectSortTitle;
            // 不再根据"是否可编辑"改变文本颜色，统一使用默认颜色（白色）。
            // 武将属性/适性等需要按数值等级变色的列，由筛选项在 GetValueStr 中直接返回带颜色标签的文本。
            SetColor(Color.white);
        }
        public void SetObject(SangoObject sanObj)
        {
            this.obj = sanObj;
        }

        public UITextItem SetText(string lab)
        {
            label.text = lab;
            return this;
        }

        public UITextItem SetColor(Color c)
        {
            label.color = c;
            return this;
        }

        public UITextItem SetAlignment(TextAnchor c)
        {
            label.alignment = c;
            return this;
        }
    }
}