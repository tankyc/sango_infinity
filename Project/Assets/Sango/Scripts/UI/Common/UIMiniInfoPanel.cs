using Sango.Loader;
using Sango.Render;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIMiniInfoPanel : MonoBehaviour
    {
        public RectTransform root;
        public Text nameLabel;
        public Image forceColor;
        public Text forceName;
        public Image corpsColor;
        public Text corpsId;

        public UITextField textFieldObject;
        List<UITextField> contentFieldList_pool = new List<UITextField>();
        List<UITextField> contentFieldList_used = new List<UITextField>();

        UITextField Create()
        {
            UITextField textField;
            if (contentFieldList_pool.Count > 0)
            {
                textField = contentFieldList_pool[0];
                contentFieldList_pool.RemoveAt(0);
                contentFieldList_used.Add(textField);
            }
            else
            {
                textField = GameObject.Instantiate(textFieldObject.gameObject, textFieldObject.transform.parent).GetComponent<UITextField>();
                contentFieldList_used.Add(textField);
            }
            textField.gameObject.SetActive(true);
            textField.transform.SetAsLastSibling();
            return textField;
        }

        protected void ResetPool()
        {
            contentFieldList_pool.AddRange(contentFieldList_used);
            foreach (var v in contentFieldList_pool)
                v.gameObject.SetActive(false);
            contentFieldList_used.Clear();
        }

        public void SetCorps(Corps corps)
        {
            if (corps != null)
            {
                forceName.text = corps.BelongForce.Name;
                forceColor.color = corps.BelongForce.Color;
                corpsColor.color = corps.Color;
                corpsId.text = corps.Index.ToString();
            }
            else
            {
                forceName.text = "----";
                forceColor.color = Color.white;
                corpsColor.color = Color.white;
                corpsId.text = "-";
            }
        }
        public UITextField AddInfo(string title, string content)
        {
            UITextField textField = Create();
            textField.text = content;
            textField.titleLabel.text = title;
            textField.label.alignment = TextAnchor.MiddleCenter;
            return textField;
        }
        public UITextField AddInfo(string title, string content, int alignment)
        {
            UITextField textField = Create();
            textField.text = content;
            textField.titleLabel.text = title;
            textField.label.alignment = (TextAnchor)alignment;
            return textField;
        }
    }
}