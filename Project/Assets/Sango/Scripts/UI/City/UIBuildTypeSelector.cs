using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIBuildTypeSelector : UGUIWindow
    {
        List<BuildingType> BuildingTypes { get; set; }
        public UIBuildingTypeItem[] buildingTypeItems;
        public Button buildButton;
        public UIPersonItem[] personItems;

        public UITextField buildCountLabel;
        public UITextField cityGoldLabel;
        public UITextField developNumberLabel;
        public UITextField emptyNumberLabel;
        public UITextField buildingTypeDescLabel;
        public UITextField limitLabel;
        public UITextField durabilityLabel;
        public UITextField action_value;

        CityBuildBuilding buildBuildingSys;
        int maxPage;
        int curPage;
        int lastSelectIndex = -1;
        public Text pageLabel;

        public override void OnOpen()
        {
            buildCountLabel.text = "";
            cityGoldLabel.text = "";
            buildCountLabel.text = "";
            buildingTypeDescLabel.text = "";
            limitLabel.text = "";
            developNumberLabel.text = "0所";
            durabilityLabel.text = "";


            buildBuildingSys = GameSystem.GetSystem<CityBuildBuilding>();
            BuildingTypes = buildBuildingSys.canBuildBuildingType;
            curPage = 0;
            maxPage = BuildingTypes.Count / buildingTypeItems.Length;
            if (BuildingTypes.Count % buildingTypeItems.Length != 0)
                maxPage += 1;
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.Build)}/{buildBuildingSys.TargetCity.mBelongCorps.ActionPoint}";

            for (int i = 0; i < buildingTypeItems.Length; i++)
            {
                UIBuildingTypeItem item = buildingTypeItems[i];
                item.onSelected = OnSelectBuildingType;

                if (buildBuildingSys.TargetBuildingType != null && i < BuildingTypes.Count && BuildingTypes[i] == buildBuildingSys.TargetBuildingType)
                    lastSelectIndex = i;
            }
            //buildBuildingSys.SelectBuildingType(BuildingTypes[0]);
            ShowPage(curPage);
            UpdateContent();
            buildButton.interactable = buildBuildingSys.TargetBuildingType != null;

            if (buildBuildingSys.TargetBuildingType != null)
                OnSelectBuildingType(buildBuildingSys.TargetBuildingType);
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
                    int buildedNum = buildBuildingSys.TargetCity.GetBuildingNumber(buildingType.kind);

                    item.SetValid(buildingType.cost <= buildBuildingSys.TargetCity.gold);
                    item.gameObject.SetActive(true);
                    item.SetBuildingType(buildingType).SetIndex(id).SetSelected(buildingType == buildBuildingSys.TargetBuildingType).SetNum(buildedNum);
                    item.nameLabel.text = $"{buildingType.Name}（{buildedNum}）";

                    if (buildingType.buildNumLimit > 0 && buildedNum >= buildingType.buildNumLimit)
                    {
                        item.SetValid(false);
                    }
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
            buildBuildingSys.Done();
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
            buildButton.interactable = true;

            buildBuildingSys.SelectBuildingType(targetBuildingType);
            OnSelectBuildingType(targetBuildingType);
            buildingTypeItem.SetSelected(true);

            UpdateContent();

        }

        public void OnSelectBuildingType(BuildingType targetBuildingType)
        {
            buildCountLabel.text = $"{buildBuildingSys.wonderNumber}0日";
            cityGoldLabel.text = $"{targetBuildingType.cost}/{buildBuildingSys.TargetCity.gold}";
            durabilityLabel.text = targetBuildingType.durabilityLimit.ToString();
            buildingTypeDescLabel.text = targetBuildingType.desc;
            limitLabel.text = targetBuildingType.limitDesc;

            int buildedCount = buildBuildingSys.TargetCity.GetBuildingNumber(targetBuildingType.kind);
            developNumberLabel.text = $"{buildedCount}所";
        }

        public void UpdateContent()
        {
            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < buildBuildingSys.personList.Count)
                    personItems[i].SetPerson(buildBuildingSys.personList[i]);
                else
                    personItems[i].SetPerson(null);
            }

            emptyNumberLabel.text = $"{buildBuildingSys.TargetCity.InteriorCellCount - buildBuildingSys.TargetCity.GetInteriorCellUsedCount()}/{buildBuildingSys.TargetCity.InteriorCellCount}";
        }

        /// <summary>
        /// 新建建筑
        /// </summary>
        public void OnBuild()
        {
            if (buildBuildingSys.TargetCell == null)
            {
                buildBuildingSys.OnSelectCell();
                Close();
            }
            else
            {
                Close();
                buildBuildingSys.DoJob();
                buildBuildingSys.Done();
            }
        }

        public void OnPersonChange(List<Person> personList)
        {
            buildBuildingSys.personList = personList;

            buildBuildingSys.UpdateJobValue();
            buildCountLabel.text = $"{buildBuildingSys.wonderNumber}回";

            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < personList.Count)
                    personItems[i].SetPerson(personList[i]);

                else
                    personItems[i].SetPerson(null);
            }
        }

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(buildBuildingSys.TargetCity.freePersons,
                buildBuildingSys.personList, 3, OnPersonChange, buildBuildingSys.customTitleList, buildBuildingSys.customTitleName);
        }
    }
}
