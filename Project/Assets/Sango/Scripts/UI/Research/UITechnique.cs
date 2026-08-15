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
            targetForce = techniqueResearchSys.TargetCity.mForce;
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
            actionPointValue.text = $"{JobType.GetJobCostAP((int)CityJobType.Research)}/{techniqueResearchSys.TargetCity.mCorps.ActionPoint}";

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
            {
                sureBtn.interactable = true;
                techniqueResearchSys.SelectTechnique(selectTech);
                techCount.text = $"{techniqueResearchSys.counter * 10}日";
                if (techniqueResearchSys.goldCost <= targetCity.gold)
                    techCost.text = $"{techniqueResearchSys.goldCost}/{targetCity.gold}";
                else
                {
                    techCost.text = $"<color=#ff1010>{techniqueResearchSys.goldCost}</color>/{targetCity.gold}";
                    sureBtn.interactable = false;
                }

                if (techniqueResearchSys.tpCost <= targetCity.mForce.TechniquePoint)
                    techCostTP.text = $"{techniqueResearchSys.tpCost}/{targetCity.mForce.TechniquePoint}";
                else
                {
                    techCostTP.text = $"<color=#ff1010>{techniqueResearchSys.tpCost}</color>/{targetCity.mForce.TechniquePoint}";
                    sureBtn.interactable = false;
                }
            }
            else if (techniqueItem.IsValid())
            {
                sureBtn.interactable = false;
                techCount.text = "--";
                techCost.text = "--";
                techCostTP.text = "--";
            }
            else
            {
                sureBtn.interactable = false;
                techCount.text = $"{selectTech.counter * 10}日";
                if (selectTech.goldCost <= targetCity.gold)
                    techCost.text = $"{selectTech.goldCost}/{targetCity.gold}";
                else
                    techCost.text = $"<color=#ff1010>{selectTech.goldCost}</color>/{targetCity.gold}";

                if (selectTech.techPointCost <= targetCity.mForce.TechniquePoint)
                    techCostTP.text = $"{selectTech.techPointCost}/{targetCity.mForce.TechniquePoint}";
                else
                    techCostTP.text = $"<color=#ff1010>{selectTech.techPointCost}</color>/{targetCity.mForce.TechniquePoint}";
            }
            techNeedAttr.text = Scenario.Cur.Variables.GetAttributeNameWithColor(selectTech.needAttr);

            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < techniqueResearchSys.personList.Count)
                    personItems[i].SetPerson(techniqueResearchSys.personList[i]);

                else
                    personItems[i].SetPerson(null);
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
