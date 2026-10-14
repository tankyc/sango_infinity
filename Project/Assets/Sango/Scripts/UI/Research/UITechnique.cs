using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 剧本选择界面
    /// </summary>
    public class UITechnique : UGUIWindow
    {
        public UITechniqueArea techniqueArea;

        public UITechniqueItem techniqueItem;
        List<UITechniqueItem> techniqueItemList = new List<UITechniqueItem>();
        public RectTransform techNode;
        public RectTransform personNode;
        public ScrollRect scrollRect;
        public RectTransform titleNode;

        public Button sureBtn;
        public Button personBtn;

        public UITextField techDesc;
        public UITextField techCount;
        public UITextField techCost;
        public UITextField techCostTP;
        public UITextField techNeedAttr;
        public UITextField actionPointValue;

        public UIPersonItem[] personItems;
        public int itemWidth = 300;
        public int itemHeight = 110;
        public int linwWidth = 80;
        public int linhHeight = 80;

        public int woffset = 40;
        public int hoffset = 50;

        int maxCol = -99;
        int maxRow = -99;
        Force targetForce;
        City targetCity;
        TechniqueResearch techniqueResearchSys;
        UITechniqueItem selectedItem;
        Technique selectTech;

        public void UpdateItems()
        {
            for (int i = 0; i < techniqueItemList.Count; i++)
            {
                UITechniqueItem techItem = techniqueItemList[i];
                Technique technique = techItem.technique;
                techItem.SetValid(technique.IsValid(targetForce)).SetCanResearch(technique.CanResearch(targetForce)).SetSelected(techItem == selectedItem);
            }
        }

        public override void OnOpen()
        {
            sureBtn.interactable = false;
            selectedItem = null;
            techniqueResearchSys = GameSystem.GetSystem<TechniqueResearch>();
            targetForce = techniqueResearchSys.TargetCity.BelongForce;
            targetCity = techniqueResearchSys.TargetCity;
            techniqueArea.CreateTitles(targetForce, titleNode);
            techniqueArea.ShowForceTechnique(targetForce, techniqueItemList);
            for (int i = 0; i < techniqueItemList.Count; i++)
            {
                UITechniqueItem techItem = techniqueItemList[i];
                techItem.onClick = OnSelectTechniqueItem;
                Technique technique = techItem.technique;
                techItem.SetValid(technique.IsValid(targetForce)).SetCanResearch(technique.CanResearch(targetForce)).SetSelected(techItem == selectedItem);
            }
            personBtn.interactable = false;
            for(int i = 0; i < 3; i++)
            {
                personItems[i].SetPerson(null);
            }
            actionPointValue.text = $"{JobType.GetJobCostAP((int)CityJobType.Research)}/{techniqueResearchSys.TargetCity.BelongCorps.ActionPoint}";

        }

        public void OnSure()
        {
            techniqueResearchSys.DoResearch();

        }

        public void OnCancel()
        {
            techniqueResearchSys.Exit();
        }

        public void OnSelectTechniqueItem(UITechniqueItem techniqueItem)
        {
            if (selectedItem == techniqueItem) return;
            if (selectedItem != null)
            {
                selectedItem.SetSelected(false);
            }
            techniqueItem.SetSelected(true);
            personBtn.interactable = true;
            selectedItem = techniqueItem;
            selectTech = techniqueItem.technique;

            techDesc.text = selectTech.desc;

            if (techniqueItem.CanResearch())
                techniqueResearchSys.SelectTechnique(selectTech);

            // 选中技巧后，按"当前技巧 + 当前研究队伍"刷新花费、耗时与确定按钮
            RefreshJobValueView();
            techNeedAttr.text = Scenario.Cur.Variables.GetAttributeNameWithColor(selectTech.needAttr);

            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < techniqueResearchSys.personList.Count)
                    personItems[i].SetPerson(techniqueResearchSys.personList[i]);

                else
                    personItems[i].SetPerson(null);
            }
        }

        /// <summary>
        /// 按"当前选中的技巧 + 当前研究队伍"刷新金钱、技巧点、耗时与确定按钮状态。
        ///
        /// 为什么必须抽成一个方法：玩家手动调整研究队伍（PersonSelectSystem → OnPersonChange）后
        /// 同样要走一遍刷新，否则界面上的数值会停留在改队伍之前 ——
        /// 例如把持"指导"特技的武将放进队伍后，实际研究费用已经减半
        /// （TechniqueResearch.DoResearch → Technique.GetCost 会用新队伍重算），
        /// 但界面依旧显示全额，玩家就会以为"指导特技不生效"。
        /// </summary>
        void RefreshJobValueView()
        {
            if (selectedItem == null || selectTech == null)
                return;

            if (selectedItem.CanResearch())
            {
                // 队伍为空时不显示上一次的残留数值：此时 UpdateJobValue 会直接返回，
                // 数值无从计算，确定按钮点下去也只会因为 JobResearch 返回 null 而毫无反应。
                if (techniqueResearchSys.personList.Count <= 0)
                {
                    sureBtn.interactable = false;
                    techCount.text = "--";
                    techCost.text = "--";
                    techCostTP.text = "--";
                    return;
                }

                sureBtn.interactable = true;
                techCount.text = $"{techniqueResearchSys.counter * 10}日";
                if (techniqueResearchSys.goldCost <= targetCity.gold)
                    techCost.text = $"{techniqueResearchSys.goldCost}/{targetCity.gold}";
                else
                {
                    techCost.text = $"<color=#ff1010>{techniqueResearchSys.goldCost}</color>/{targetCity.gold}";
                    sureBtn.interactable = false;
                }

                if (techniqueResearchSys.tpCost <= targetCity.BelongForce.TechniquePoint)
                    techCostTP.text = $"{techniqueResearchSys.tpCost}/{targetCity.BelongForce.TechniquePoint}";
                else
                {
                    techCostTP.text = $"<color=#ff1010>{techniqueResearchSys.tpCost}</color>/{targetCity.BelongForce.TechniquePoint}";
                    sureBtn.interactable = false;
                }
            }
            else if (selectedItem.IsValid())
            {
                sureBtn.interactable = false;
                techCount.text = "--";
                techCost.text = "--";
                techCostTP.text = "--";
            }
            else
            {
                // 尚无研究条件（前置技巧未完成等）：按技巧本身的基础花费预览
                sureBtn.interactable = false;
                techCount.text = $"{selectTech.counter * 10}日";
                if (selectTech.goldCost <= targetCity.gold)
                    techCost.text = $"{selectTech.goldCost}/{targetCity.gold}";
                else
                    techCost.text = $"<color=#ff1010>{selectTech.goldCost}</color>/{targetCity.gold}";

                if (selectTech.techPointCost <= targetCity.BelongForce.TechniquePoint)
                    techCostTP.text = $"{selectTech.techPointCost}/{targetCity.BelongForce.TechniquePoint}";
                else
                    techCostTP.text = $"<color=#ff1010>{selectTech.techPointCost}</color>/{targetCity.BelongForce.TechniquePoint}";
            }
        }

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(targetCity.freePersons,
                techniqueResearchSys.personList, 3, OnPersonChange, techniqueResearchSys.customTitleList, techniqueResearchSys.customTitleName);
        }

        public void OnPersonChange(List<Person> personList)
        {
            techniqueResearchSys.personList = personList;
            techniqueResearchSys.UpdateJobValue();

            // 队伍会影响研究费用与耗时（例如加入持"指导"特技的武将后费用减半），必须同步刷新界面，
            // 否则玩家看到的花费还停留在改队伍之前，会误判为特技没生效。
            RefreshJobValueView();

            //buildCountLabel.text = $"{buildBuildingSys.wonderBuildCounter}回";

            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < personList.Count)
                    personItems[i].SetPerson(personList[i]);

                else
                    personItems[i].SetPerson(null);
            }
        }
    }
}
