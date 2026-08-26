using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UITroopCommandBuild : UGUIWindow
    {
        List<BuildingType> BuildingTypes { get; set; }
        public UIBuildingTypeItem[] buildingTypeItems;
        public RectTransform rootTrans;
        int maxPage;
        int curPage;
        int lastSelectIndex = -1;

        public Text pageLabel;
        public Button buildButtonObj;
        public UITextField buildCountLabel;
        public UITextField cityGoldLabel;

        public UITextField durablityLabel;
        public UITextField featrueLabel;
        public UITextField buildingTypeDescLabel;

        TroopActionBuild troopCommandBuildSys;

        public override void OnOpen()
        {
            buildCountLabel.text = "";
            cityGoldLabel.text = "";
            buildCountLabel.text = "";
            buildButtonObj.interactable = (false);
            troopCommandBuildSys = GameSystem.GetSystem<TroopActionBuild>();
            BuildingTypes = troopCommandBuildSys.canBuildBuildingType;
            curPage = 0;
            maxPage = BuildingTypes.Count / buildingTypeItems.Length;
            if (BuildingTypes.Count % buildingTypeItems.Length != 0)
                maxPage += 1;

            for (int i = 0; i < buildingTypeItems.Length; i++)
            {
                UIBuildingTypeItem item = buildingTypeItems[i];
                item.onSelected = OnSelectBuildingType;
            }

            ShowPage(curPage);
        }

        public void ShowPage(int index)
        {
            if (index < 0 || index >= maxPage)
                return;
            curPage = index;

            pageLabel.text = $"{curPage + 1}/{maxPage}";

            for (int i = 0; i < buildingTypeItems.Length; i++)
            {
                UIBuildingTypeItem item = buildingTypeItems[i];
                int id = curPage * buildingTypeItems.Length + i;
                if (id < BuildingTypes.Count)
                {
                    BuildingType buildingType = BuildingTypes[id];
                    item.gameObject.SetActive(true);
                    item.SetBuildingType(buildingType).SetIndex(id).SetSelected(buildingType == troopCommandBuildSys.targetBuildingType).SetNum(-1);
                    item.SetValid(buildingType.cost <= troopCommandBuildSys.TargetTroop.gold);
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        public void NextPage()
        {
            ShowPage(curPage + 1);
        }

        public void PrevPage()
        {
            ShowPage(curPage - 1);
        }

        /// <summary>
        /// 退出
        /// </summary>
        public void OnCancel()
        {
            troopCommandBuildSys.Back();
        }

        public void OnSelectBuildingType(UIBuildingTypeItem buildingTypeItem)
        {
            if (lastSelectIndex == -1)
                lastSelectIndex = buildingTypeItem.index;
            else
            {
                if (lastSelectIndex >= curPage * buildingTypeItems.Length)
                {
                    int idx = lastSelectIndex - curPage * buildingTypeItems.Length;
                    if (idx < buildingTypeItems.Length)
                        buildingTypeItems[idx].SetSelected(false);
                }
            }

            lastSelectIndex = buildingTypeItem.index;
            buildingTypeItem.SetSelected(true);
            BuildingType targetBuildingType = BuildingTypes[buildingTypeItem.index];
            troopCommandBuildSys.targetBuildingType = targetBuildingType;
            buildButtonObj.interactable = (true);

            int buildAbility = GameUtility.Method_TroopBuildAbility(troopCommandBuildSys.TargetTroop);
            int turnCount = targetBuildingType.durabilityLimit % buildAbility == 0 ? 0 : 1;
            int wonderBuildCounter = targetBuildingType.durabilityLimit / buildAbility + turnCount;

            buildCountLabel.text = $"{wonderBuildCounter}回";
            cityGoldLabel.text = $"{targetBuildingType.cost}/{troopCommandBuildSys.TargetTroop.gold}";

            buildingTypeDescLabel.text = targetBuildingType.desc;
            buildingTypeItem.SetSelected(true);
        }

        /// <summary>
        /// 新建建筑
        /// </summary>
        public void OnBuild()
        {
            troopCommandBuildSys.OnSlectedBuildingType(troopCommandBuildSys.targetBuildingType);
        }
    }
}
