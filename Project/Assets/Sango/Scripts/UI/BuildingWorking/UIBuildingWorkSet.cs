using TKNewtonsoft.Json.Utilities.LinqBridge;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    public class UIBuildingWorkSet : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem[] personItems;
        public UIBuildingTypeItem[] productItems;
        public UIBuildingTypeItem autoProductItem;

        public UITextField value_target;
        public UITextField value_attr;
        public UITextField value_product;
        public UITextField value_factor;

        public UITextField action_value;

        BuildingWorking currentSystem;
        Building TargetBuilding { get; set; }

        List<Person> selectedPersonList = new List<Person>();
        List<ItemType> product_items_list = new List<ItemType>();
        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<BuildingWorking>();
            windiwTitle.text = currentSystem.customTitleName;
            TargetBuilding = currentSystem.TargetBuilding;
            BuildingType targetBuildingType = TargetBuilding.BuildingType;
            selectedPersonList.Clear();
            if (TargetBuilding.Workers != null)
                TargetBuilding.Workers.ForEach(p =>
                {
                    selectedPersonList.Add(p);
                });

            product_items_list.Clear();
            if (targetBuildingType.productItems != null && targetBuildingType.productItems.Length > 0)
            {
                autoProductItem.onSelected = OnSelectAutoProduct;
                autoProductItem.SetSelected(TargetBuilding.ProductItemId == 0);

                for (int i = 0; i < targetBuildingType.productItems.Length; i++)
                {
                    int itemId = targetBuildingType.productItems[i];
                    ItemType itemType = Scenario.Cur.GetObject<ItemType>(itemId);
                    if (itemType != null && itemType.IsValid(TargetBuilding.mForce))
                    {
                        product_items_list.Add(itemType);
                    }
                }

                for (int i = 0; i < productItems.Length; i++)
                {
                    UIBuildingTypeItem item = productItems[i];
                    if (i < product_items_list.Count)
                    {
                        item.gameObject.SetActive(true);
                        ItemType itemType = product_items_list[i];
                        item.SetItemType(itemType).SetNum(TargetBuilding.mCity.itemStore.GetNumber(itemType.storeKind)).SetSelected(itemType.Id == TargetBuilding.ProductItemId);
                        item.onSelected = OnSelectProduct;
                    }
                    else
                    {
                        item.gameObject.SetActive(false);
                    }
                }
            }
            ShowContent();
        }

        void OnSelectAutoProduct(UIBuildingTypeItem item)
        {
            TargetBuilding.ProductItemId = 0;
            for (int i = 0; i < productItems.Length; i++)
            {
                productItems[i].SetSelected(false);
            }
            autoProductItem.SetSelected(true);
        }

        void OnSelectProduct(UIBuildingTypeItem item)
        {
            autoProductItem.SetSelected(false); 
            TargetBuilding.ProductItemId = item.obj.Id;
            for (int i = 0; i < productItems.Length; i++)
            {
                productItems[i].SetSelected(false);
            }
            item.SetSelected(true);
        }

        void ShowContent()
        {
            BuildingType targetBuildingType = TargetBuilding.BuildingType;
            for (int i = 0; i < personItems.Length; ++i)
            {
                UIPersonItem personItem = personItems[i];
                personItem.gameObject.SetActive(i < targetBuildingType.workerLimit);
                if (i < selectedPersonList.Count)
                {
                    personItems[i].SetPerson(selectedPersonList[i]);
                }
                else
                {
                    personItems[i].SetPerson(null);
                }
            }

            value_target.text = TargetBuilding.Name;
            value_attr.text = Scenario.Cur.Variables.GetAttributeNameWithColor(targetBuildingType.effectAttrType);

        }


        public void OnSure()
        {
            currentSystem.SetBuildingWorker(TargetBuilding, selectedPersonList);
            TargetBuilding.Render?.UpdateRender();
            currentSystem.Done();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }

        public virtual void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(TargetBuilding.mCity.freePersons,
               selectedPersonList, TargetBuilding.BuildingType.workerLimit, OnPersonChange, currentSystem.customTitleList[TargetBuilding.BuildingType.effectAttrType], currentSystem.customTitleName);

        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            selectedPersonList = personList;
            ShowContent();
        }
    }
}
