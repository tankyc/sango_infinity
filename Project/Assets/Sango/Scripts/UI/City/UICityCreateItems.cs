using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityCreateItems : UGUIWindow
    {
        public UIBuildingTypeItem objUIBuildingTypeItem;
        CreatePool<UIBuildingTypeItem> itemPool;

        public Text itemInfoLabel;

        public UITextField goldLabel;
        public UITextField dayLabel;
        public UITextField haveLabel;
        public UITextField effectLabel;

        public UITextField action_value;

        public Text windiwTitle;
        public UIPersonItem[] personItems;

        public Scrollbar scrollbar;
        public VerticalLayoutGroup rootLayout;
        CityCreateItems currentSystem;
        int updateNextFrame = 0;

        public Button sureButton;

        protected override void Awake()
        {
            base.Awake();
            itemPool = new CreatePool<UIBuildingTypeItem>(objUIBuildingTypeItem);
        }

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityCreateItems>();
            windiwTitle.text = currentSystem.customTitleName;

            itemInfoLabel.text = "兵装";
            int len = currentSystem.ItemTypes.Count;
            itemPool.Reset();
            for (int i = 0; i < len; i++)
            {
                CityCreateItems.ItemTypeInfo itemType = currentSystem.ItemTypes[i];
                int totalNum = currentSystem.TargetCity.itemStore.GetNumber(itemType.itemType);
                UIBuildingTypeItem cityBuildingSlot = itemPool.Create();
                cityBuildingSlot.onSelected = OnSelectItemType;
                cityBuildingSlot.SetItemType(itemType.itemType).SetIndex(i).SetSelected(itemType == currentSystem.CurSelectedItemType).SetNum(totalNum);
                cityBuildingSlot.titleObj.SetActive(i % 4 == 0);
                cityBuildingSlot.SetValid(itemType.targetBuilding != null);
            }
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.CreateItems)}/{currentSystem.TargetCity.mCorps.ActionPoint}";

            OnSelectItemType(itemPool.Get(currentSystem.CurSelectedItemTypeIndex));
            updateNextFrame = 2;
        }

        private void LateUpdate()
        {
            if (updateNextFrame > 0)
            {
                updateNextFrame--;
                if (updateNextFrame == 0)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rootLayout.GetComponent<RectTransform>());
                }
            }
        }

        public void OnSelectItemType(UIBuildingTypeItem buildingTypeItem)
        {
            if (currentSystem.CurSelectedItemTypeIndex >= 0)
            {
                itemPool.Get(currentSystem.CurSelectedItemTypeIndex).SetSelected(false);
            }

            currentSystem.CurSelectedItemTypeIndex = buildingTypeItem.index;
            CityCreateItems.ItemTypeInfo curItemType = currentSystem.ItemTypes[buildingTypeItem.index];
            currentSystem.CurSelectedItemType = curItemType;
            currentSystem.TargetBuilding = curItemType.targetBuilding;
            Person[] builder = ForceAI.CounsellorRecommendCreateItems(currentSystem.TargetCity.freePersons);
            currentSystem.personList.Clear();
            if (builder == null || builder.Length == 0)
            {
                for (int i = 0; i < personItems.Length; ++i)
                    personItems[i].SetPerson(null);
            }
            else
            {
                for (int i = 0; i < personItems.Length; ++i)
                {
                    if (i < builder.Length)
                    {
                        Person person = builder[i];
                        personItems[i].SetPerson(person);
                        currentSystem.personList.Add(person);
                    }
                    else
                    {
                        personItems[i].SetPerson(null);
                    }
                }
            }

            currentSystem.UpdateJobValue();

            ResetContent();
            buildingTypeItem.SetSelected(true);
        }

        public void ResetContent()
        {
            CityCreateItems.ItemTypeInfo curItemType = currentSystem.CurSelectedItemType;
            dayLabel.text = $"{currentSystem.TurnAndDestNumber[0]}回";
            bool enoughGold = currentSystem.TargetCity.gold >= curItemType.itemType.cost;
            goldLabel.SetColor(enoughGold ? Color.white : Color.red);
            sureButton.interactable = enoughGold;
            goldLabel.text = $"{curItemType.itemType.cost}/{currentSystem.TargetCity.gold}";

            int totalNum = currentSystem.TargetCity.itemStore.GetNumber(curItemType.itemType);
            haveLabel.text = $"{totalNum}→{totalNum + currentSystem.TurnAndDestNumber[1]}";

            effectLabel.text = curItemType.itemType.desc;
        }

        public void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = personList;
            currentSystem.UpdateJobValue();

            ResetContent();

            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < currentSystem.personList.Count)
                    personItems[i].SetPerson(currentSystem.personList[i]);

                else
                    personItems[i].SetPerson(null);
            }
        }

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.TargetCity.freePersons,
                currentSystem.personList, 3, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);
        }

        public void OnSure()
        {
            currentSystem.DoJob();
            OnOpen();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }
    }
}
