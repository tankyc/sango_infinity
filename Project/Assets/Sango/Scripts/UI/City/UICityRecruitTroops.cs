using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityRecruitTroops : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem[] personItems;
        public UITextField targetValue;
        public UITextField targetGold;

        public UITextField action_value;

        City TargetCity;
        CityRecruitTroops currentSystem;
        Text populationPreview;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityRecruitTroops>();
            windiwTitle.text = currentSystem.customTitleName;
            TargetCity = currentSystem.TargetCity;
            UpdateContent();
        }

        public void UpdateContent()
        {
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.RecruitTroops)}/{TargetCity.BelongCorps.ActionPoint}";
            sureButton.interactable = currentSystem.personList.Count > 0;
            for (int i = 0; i < 3; i++)
            {
                if (i < currentSystem.personList.Count)
                    personItems[i].SetPerson(currentSystem.personList[i]);
                else
                    personItems[i].SetPerson(null);
            }

            int destValue = TargetCity.troops + currentSystem.wonderNumber;
            if (destValue > TargetCity.TroopsLimit)
                destValue = TargetCity.TroopsLimit;

            targetValue.text = $"{TargetCity.troops}→{destValue}";
            targetGold.text = $"{TargetCity.GetJobCost(CityJobType.RecruitTroops)}/{TargetCity.gold}";
            UpdatePopulationPreview(Math.Max(0, destValue - TargetCity.troops));
        }

        void UpdatePopulationPreview(int recruits)
        {
            if (!HantuPopulation.Enabled)
            {
                if (populationPreview != null) populationPreview.gameObject.SetActive(false);
                return;
            }

            if (populationPreview == null)
            {
                // 沿用资金行字体，在同一区域内新增一行。
                RectTransform goldRow = (RectTransform)targetGold.transform;
                populationPreview = Instantiate(targetGold.label, goldRow.parent, false);
                populationPreview.name = "population_cost";
                populationPreview.alignment = TextAnchor.MiddleCenter;
                populationPreview.fontSize = Math.Min(populationPreview.fontSize, 18);
                populationPreview.raycastTarget = false;

                RectTransform rect = populationPreview.rectTransform;
                const float height = 24f;
                rect.anchorMin = new Vector2(0f, goldRow.anchorMin.y);
                rect.anchorMax = new Vector2(1f, goldRow.anchorMin.y);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(-28f, height);
                rect.anchoredPosition = new Vector2(0f, goldRow.anchoredPosition.y - goldRow.rect.height * goldRow.pivot.y - height / 2f - 6f);
            }

            HantuPopulation.GetRecruitmentCost(TargetCity, recruits, out int urban, out int rural);
            populationPreview.text = $"预扣人口　城市 {urban:N0}　农村 {rural:N0}";
            populationPreview.gameObject.SetActive(true);
        }

        public void OnSure()
        {
            currentSystem.DoJob();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }

        /// <summary>打开征兵选择器，默认使用名声优先、魅力倒序的排序列。</summary>
        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.TargetCity.freePersons,
               currentSystem.personList, 3, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName, 1);
        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = (personList);
            currentSystem.UpdateJobValue();
            UpdateContent();
        }

    }
}
