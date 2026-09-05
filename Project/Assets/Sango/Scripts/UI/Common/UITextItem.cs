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
            if(objectSortTitle != null)
            {
                if(!objectSortTitle.CanEdit)
                    SetColor(Color.gray);
                else
                    SetColor(Color.white);
            }
            else
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