using Sango.Loader;
using System;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIMapCitySelectItem : MonoBehaviour
    {
        public GameObject selectedObj;
        public GameObject normalObj;
        public Text cityName;
        public GameObject inavtiveObj;
        public GameObject[] overObj;
        public Image[] colorImage;
        public City city;
        public Action<City, bool> onSelectAction;
        public Action<City, ShortCity, bool> onOverAction;

        public ShortCity shortCity;
        public Action<UIMapCitySelectItem, ShortCity, bool> onSelectShortAction;

        public void ShowName(string name)
        {
            if (cityName == null) return;
            cityName.enabled = true;
            cityName.text = name;
        }
        
        public void OnSelect()
        {
            if (city != null)
            {
                if (onSelectAction != null)
                {
                    selectedObj.SetActive(!selectedObj.activeSelf);
                    onSelectAction.Invoke(city, selectedObj.activeSelf);
                }
            }
            if (shortCity != null)
            {
                if (onSelectShortAction != null)
                {
                    selectedObj.SetActive(!selectedObj.activeSelf);
                    onSelectShortAction.Invoke(this, shortCity, selectedObj.activeSelf);
                }
            }
        }

        public UIMapCitySelectItem SetInavtive(bool b)
        {
            inavtiveObj.SetActive(b);
            return this;
        }

        public UIMapCitySelectItem SetSelected(bool b)
        {
            selectedObj.SetActive(b);
            return this;
        }

        public UIMapCitySelectItem SetColor(Color c)
        {
            foreach (var item in colorImage)
                if (item != null)
                    item.color = c;
            return this;
        }
        public void SetOver(bool b)
        {
            foreach (var item in overObj)
                if (item != null)
                    item.SetActive(b);
            onOverAction?.Invoke(city, shortCity, b);
        }
    }
}