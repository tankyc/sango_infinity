using Sango.Loader;
using System;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UISangoObjectSelectItem : MonoBehaviour
    {
        public GameObject selectedObj;
        public GameObject normalObj;
        public GameObject inavtiveObj;
        public GameObject avtiveObj;
        public GameObject[] overObj;
        public Image[] colorImage;
        public SangoObject target;
        public Action<SangoObject> onSelectAction;

        public void OnSelect()
        {
            if (target != null && avtiveObj.activeSelf)
            {
                onSelectAction?.Invoke(target);
            }
        }

        public UISangoObjectSelectItem SetInavtive(bool b)
        {
            inavtiveObj.SetActive(b);
            avtiveObj.SetActive(!b);
            return this;
        }

        public UISangoObjectSelectItem SetSelected(bool b)
        {
            selectedObj.SetActive(b);
            return this;
        }

        public UISangoObjectSelectItem SetColor(Color c)
        {
            foreach (var item in colorImage)
                if (item != null)
                    item.color = c;
            return this;
        }
        public UISangoObjectSelectItem SetOver(bool b)
        {
            foreach (var item in overObj)
                if (item != null)
                    item.SetActive(b);
            return this;
        }
    }
}