using Sango.Core.Player;
using UnityEngine;

using Sango.Core;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

namespace Sango.UI
{
    public class UIOfficialSet : UGUIWindow
    {
        public Text title;
        public List<ObjectSortTitle> sortItems;
        public UIObjectListItem[] uIObjectListItems;
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
        public Button sureButton;

        public RectTransform btnRoot;
        public UIButtonItem[] buttons;

        public UIOfficialItem[] uiOfficialItems;
        public Scrollbar official_scrollbar;
        protected int officialIndex = 0;

        public GameObject officialSelectPanel;
        public UIOfficialItem[] uiSelectOfficialItems;
        public UIPersonItem uIPersonItem;
        protected int selectOfficialIndex = 0;
        public Scrollbar select_official_scrollbar;

        public Button select_official_sureButton;

        Person selectPerson;
        Official selectOfficial;
        RectTransform[] uIObjectListItemsRect;
        bool dragFlag = false;
        UIObjectListItem currentSelectItem;
        public UITextField limitText;


        CityUpgradeOfficial cityUpgradeOfficial;

        protected override void Awake()
        {
            base.Awake();
            //uIObjectListItemsRect = new RectTransform[uIObjectListItems.Length];
            //for (int i = 0; i < uIObjectListItems.Length; i++)
            //{
            //    uIObjectListItemsRect[i] = uIObjectListItems[i].GetComponent<RectTransform>();
            //}
        }

        public override void OnOpen()
        {
            base.OnOpen();
            cityUpgradeOfficial = GameSystem.GetSystem<CityUpgradeOfficial>();
            title.text = cityUpgradeOfficial.customTitleName;
            sortItems = cityUpgradeOfficial.customTitleList;

            itemWidth = GetContentWidth();
            itemCount = uIObjectListItems.Length;
            officialSelectPanel.SetActive(false);
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
            int dataCount = cityUpgradeOfficial.targetList.Count;
            if (dataCount < itemCount)
            {
                scrollbar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                scrollbar.transform.parent.gameObject.SetActive(true);
                scrollbar.size = (float)itemCount / (float)dataCount;
                scrollbar.SetValueWithoutNotify(0);
            }

            officialIndex = 0;
            dataCount = cityUpgradeOfficial.upgradeList.Count;
            if (dataCount < uiOfficialItems.Length)
            {
                official_scrollbar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                official_scrollbar.transform.parent.gameObject.SetActive(true);
                official_scrollbar.size = (float)uiOfficialItems.Length / (float)dataCount;
                official_scrollbar.SetValueWithoutNotify(0);
            }

            // 重置状态和位置
            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                Vector2 p = listItem.contentRect.anchoredPosition;
                p.x = 0;
                listItem.contentRect.anchoredPosition = p;
                listItem.SetOver(false);
            }

            UpdateSortContent();
            OnScrollBarValueChange(0);
            UpdateOfficialStartIndex(0);
            foreach (RectTransform r in contentRect)
            {
                Vector2 p = r.anchoredPosition;
                p.x = 0;
                r.anchoredPosition = p;
            }

            for (int j = 0; j < uIObjectListItems.Length; j++)
            {
                UIObjectListItem listItem = uIObjectListItems[j];
                listItem.onSelected = OnPersonListSelected;
            }
            for (int j = 0; j < uiOfficialItems.Length; j++)
            {
                UIOfficialItem listItem = uiOfficialItems[j];
                listItem.onDoubleSelected = OnDoubleSelectOfficialItem;
            }
            for (int j = 0; j < uiSelectOfficialItems.Length; j++)
            {
                UIOfficialItem listItem = uiSelectOfficialItems[j];
                listItem.onSelected = OnSelectPersonOfficialItem;
            }

            bool hasButtons = cityUpgradeOfficial.buttonDatas != null;
            btnRoot.gameObject.SetActive(hasButtons);
            if (hasButtons)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    UIButtonItem buttonItem = buttons[i];
                    if (i >= cityUpgradeOfficial.buttonDatas.Count)
                    {
                        buttonItem.gameObject.SetActive(false);
                        continue;
                    }
                    ObjectsDisplaySystem.ButtonData buttonData = cityUpgradeOfficial.buttonDatas[i];
                    buttonItem.gameObject.SetActive(true);
                    buttonItem.SetTitle(buttonData.title).SetStyle(buttonData.style).BindAction(() =>
                    {

                        buttonData.action?.Invoke();
                        UpdateContent();
                    });
                }
            }
            sureButton.interactable = cityUpgradeOfficial.upgradeList.Count > 0;
        }

        public void UpdateContent()
        {
            int dataCount = cityUpgradeOfficial.targetList.Count;
            if (dataCount < itemCount)
            {
                scrollbar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                scrollbar.transform.parent.gameObject.SetActive(true);
                scrollbar.size = (float)itemCount / (float)dataCount;
            }

            dataCount = cityUpgradeOfficial.upgradeList.Count;
            if (dataCount < uiOfficialItems.Length)
            {
                official_scrollbar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                official_scrollbar.transform.parent.gameObject.SetActive(true);
                official_scrollbar.size = (float)uiOfficialItems.Length / (float)dataCount;
            }

            //// 重置状态和位置
            //for (int j = 0; j < uIObjectListItems.Length; j++)
            //{
            //    UIObjectListItem listItem = uIObjectListItems[j];
            //    Vector2 p = listItem.contentRect.anchoredPosition;
            //    p.x = 0;
            //    listItem.contentRect.anchoredPosition = p;
            //    listItem.SetOver(false);
            //}
            //UpdateSortContent();
            UpdateItemStartIndex(startIndex);
            UpdateOfficialStartIndex(officialIndex);
            sureButton.interactable = cityUpgradeOfficial.upgradeList.Count > 0;
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
                    cityUpgradeOfficial.targetList.Sort(sortTitle.Sort);
                    if (!up) cityUpgradeOfficial.targetList.Reverse();
                    scrollbar.SetValueWithoutNotify(0);
                    OnScrollBarValueChange(0);
                };

                if (i > 0)
                {
                    for (int j = 0; j < itemCount; j++)
                    {
                        UIObjectListItem listItem = uIObjectListItems[j];
                        listItem.Add("", sortTitle.ContentMaxWidth);
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

        protected UISortButton CreateSortButtonItem()
        {
            GameObject btn = GameObject.Instantiate(sortTitleItem.gameObject, sorltTitleTransform);
            UISortButton sortBtn = btn.GetComponent<UISortButton>();
            sortButtonPool.Add(sortBtn);
            return sortBtn;
        }

        public void UpdateItemStartIndex(int startIndex)
        {
            for (int i = 0; i < itemCount; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                int destIndex = i + startIndex;
                listItem.index = destIndex;
                if (destIndex < cityUpgradeOfficial.targetList.Count)
                {
                    SangoObject sango = cityUpgradeOfficial.targetList[destIndex];
                    //bool isSelected = objectSelectSystem.selected.Contains(sango);
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        ObjectSortTitle sortTitle = sortItems[j];
                        listItem.Set(j, sortTitle.GetValueStr(sango));
                    }
                    listItem.SetSelected(false);
                }
                else
                {
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        ObjectSortTitle sortTitle = sortItems[j];
                        listItem.Set(j, "");
                    }
                    listItem.SetSelected(false);
                }

            }
        }

        public void UpdateOfficialStartIndex(int startIndex)
        {
            for (int i = 0; i < uiOfficialItems.Length; i++)
            {
                UIOfficialItem listItem = uiOfficialItems[i];
                int destIndex = i + startIndex;
                listItem.index = destIndex;
                if (destIndex < cityUpgradeOfficial.upgradeList.Count)
                {
                    CityUpgradeOfficial.UpgradeOfficial data = cityUpgradeOfficial.upgradeList[destIndex];
                    listItem.SetPerson(data.person, data.official);
                }
                else
                {
                    listItem.SetPerson(null, null);
                }

            }
        }


        public void OnSure()
        {
            cityUpgradeOfficial.DoJob();
        }

        public void OnCancel()
        {
            cityUpgradeOfficial.Done();
        }

        public void OnPersonListItemPressDown(UIObjectListItem item)
        {
            item.SetPressd(true);
            //dragFlag = !item.IsSelected();
        }

        public void OnPersonListItemPressUp(UIObjectListItem item)
        {
            item.SetPressd(false);
            OnPersonListSelected(item);
        }

        public void OnPersonListItemPointEnter(UIObjectListItem item)
        {
            item.SetOver(true);
        }
        public void OnPersonListItemPointExit(UIObjectListItem item)
        {
            item.SetOver(false);
        }

        public void OnPersonListSelected(UIObjectListItem item)
        {
            if (item.index >= cityUpgradeOfficial.targetList.Count)
                return;

            officialSelectPanel.SetActive(true);
            currentSelectItem = item;
            selectPerson = cityUpgradeOfficial.targetList[currentSelectItem.index];
            UpdateOfficialSelectPanel();
        }

        public void UpdateOfficialSelectPanel()
        {
            limitText.text = "";
            lastSelectPersonOfficialItem?.SetSelected(false);
            lastSelectPersonOfficialItem = null;
            select_official_sureButton.interactable = false;

            if (currentSelectItem == null) return;
            selectOfficialIndex = 0;
            Official[] officials = selectPerson.Official.NextOfficials;
            if (officials.Length < uiSelectOfficialItems.Length)
            {
                select_official_scrollbar.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                select_official_scrollbar.transform.parent.gameObject.SetActive(true);
                select_official_scrollbar.size = (float)uiSelectOfficialItems.Length / (float)officials.Length;
                select_official_scrollbar.SetValueWithoutNotify(0);
            }
            uIPersonItem.SetPerson(selectPerson);
            UpdateSelectOfficialStartIndex(0);
        }

        public void UpdateSelectOfficialStartIndex(int startIndex)
        {
            Person person = cityUpgradeOfficial.targetList[currentSelectItem.index];
            Official[] officials = selectPerson.Official.NextOfficials;
            for (int i = 0; i < uiSelectOfficialItems.Length; i++)
            {
                UIOfficialItem listItem = uiSelectOfficialItems[i];
                int destIndex = i + startIndex;
                listItem.index = destIndex;
                if (destIndex < officials.Length)
                {
                    Official data = officials[destIndex];
                    listItem.SetPerson(person, data);
                    listItem.SetDisable(!data.CheckPerson(person));
                }
                else
                {
                    listItem.SetPerson(null, null);
                }

            }
        }

        public void OnScrollBarValueChange(float value)
        {
            startIndex = (int)UnityEngine.Mathf.Lerp(0, cityUpgradeOfficial.targetList.Count - itemCount, value);
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

        public void OnOfficial_ScrollBarValueChange(float value)
        {
            officialIndex = (int)UnityEngine.Mathf.Lerp(0, cityUpgradeOfficial.upgradeList.Count - uiOfficialItems.Length, value);
            UpdateOfficialStartIndex(officialIndex);
        }

        public void OnSelectOfficial_ScrollBarValueChange(float value)
        {
            if (selectPerson == null) return;
            Official[] officials = selectPerson.Official.NextOfficials;
            selectOfficialIndex = (int)UnityEngine.Mathf.Lerp(0, officials.Length - uiSelectOfficialItems.Length, value);
            UpdateSelectOfficialStartIndex(selectOfficialIndex);
        }

        public void OnSureSelectOfficial()
        {
            cityUpgradeOfficial.AddSetPerson(selectPerson, selectOfficial);
            officialSelectPanel.SetActive(false);
            UpdateContent();
        }

        public void OnCancelSelectOfficial()
        {
            officialSelectPanel.SetActive(false);
        }

        public void OnDoubleSelectOfficialItem(UIOfficialItem item)
        {
            int destIndex = item.index;
            if (destIndex < cityUpgradeOfficial.upgradeList.Count)
            {
                cityUpgradeOfficial.RemoveUpgrade(destIndex);
                UpdateContent();
            }
        }

        UIOfficialItem lastSelectPersonOfficialItem;
        public void OnSelectPersonOfficialItem(UIOfficialItem item)
        {
            Official[] officials = selectPerson.Official.NextOfficials;
            int index = item.index;
            if (index < 0 || index >= officials.Length) return;
            Official dst = officials[index];

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("需要: ");

            if (dst.levelNeed > 0)
            {
                if (selectPerson.Level.Id < dst.levelNeed)
                {
                    stringBuilder.Append($"等级<color=#ff1111>{selectPerson.Level.Id}</color>/{dst.levelNeed}");
                }
                else
                {
                    stringBuilder.Append($"等级{dst.levelNeed}");
                }
            }
            stringBuilder.Append("  ");

            if (dst.commandNeed > 0) 
            {
                if(selectPerson.Command < dst.commandNeed)
                {
                    stringBuilder.Append($"统率<color=#ff1111>{selectPerson.Command}</color>/{dst.commandNeed}");
                }
                else
                {
                    stringBuilder.Append($"统率{dst.commandNeed}");
                }
            }
            stringBuilder.Append("  ");

            if (dst.strengthNeed > 0)
            {
                if (selectPerson.Strength < dst.strengthNeed)
                {
                    stringBuilder.Append($"武力<color=#ff1111>{selectPerson.Strength}</color>/{dst.strengthNeed}");
                }
                else
                {
                    stringBuilder.Append($"武力{dst.strengthNeed}");
                }
            }
            stringBuilder.Append("  ");

            if (dst.intelligenceNeed > 0)
            {
                if (selectPerson.Intelligence < dst.intelligenceNeed)
                {
                    stringBuilder.Append($"智力<color=#ff1111>{selectPerson.Intelligence}</color>/{dst.intelligenceNeed}");
                }
                else
                {
                    stringBuilder.Append($"智力{dst.intelligenceNeed}");
                }
            }
            stringBuilder.Append("  ");

            if (dst.politicsNeed > 0)
            {
                if (selectPerson.Politics < dst.politicsNeed)
                {
                    stringBuilder.Append($"政治<color=#ff1111>{selectPerson.Politics}</color>/{dst.politicsNeed}");
                }
                else
                {
                    stringBuilder.Append($"政治{dst.politicsNeed}");
                }
            }
            stringBuilder.Append("  ");

            if (dst.glamourNeed > 0)
            {
                if (selectPerson.Glamour < dst.glamourNeed)
                {
                    stringBuilder.Append($"魅力<color=#ff1111>{selectPerson.Glamour}</color>/{dst.glamourNeed}");
                }
                else
                {
                    stringBuilder.Append($"魅力{dst.glamourNeed}");
                }
            }
            limitText.text = stringBuilder.ToString();
            if (!dst.CheckPerson(selectPerson))
            {
                return;
            }

            lastSelectPersonOfficialItem?.SetSelected(false);
            item.SetSelected(true);
            lastSelectPersonOfficialItem = item;
            selectOfficial = officials[item.index];
            select_official_sureButton.interactable = true;
        }
    }
}
