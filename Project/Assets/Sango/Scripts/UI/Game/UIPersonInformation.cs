using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core;
using UnityEngine;

namespace Sango.UI
{
    public class UIPersonInformation : UGUIWindow
    {
        public Text windiwTitle;

        public Toggle[] tabs;

        public UIObjectList uIObjectList;

        public UIPersonItem personItems;
        public UIStatusItem statusItem;

        public UITextField nameLabel;

        // state
        public UITextField forceNameLabel;
        public UITextField corpsNameLabel;
        public UITextField belongCityLabel;
        public UITextField whereLabel;
        public UITextField stateLabel;
        public UITextField loyaltyLabel;
        public UITextField meritLabel;
        public UITextField officialLabel;
        public UITextField troopsLimitLabel;
        public UITextField costLabel;
        public UITextField missionLabel;
        public UITextField missionTargetLabel;
        public UITextField dayLabel;
        public UITextField actionoverLabel;
        public UITextField levelLabel;
        public UITextField expLabel;

        // feature
        public UITextField featureLabel;
        CreatePool<UITextField> featureObjectPool;

        // personal
        public UITextField personalityLabel;
        public UITextField ageLabel;
        public UITextField sexLabel;
        public UITextField injuryyLabel;
        public UITextField itemCountLabel;
        public UITextField staminaLabel;
        public UITextField[] troopTypeLvLabel;
        //public UITextField featureLabel;
        //public UITextField featureDescLabel;

        // relationship
        public UITextField fatherLabel;
        public UITextField motherLabel;
        public UIPersonItem spousePersonItem;
        public UIPersonItem[] brotherPersonItem;

        // biographies
        public UITextField descLabel;

        Person Target;
        PersonInformation currentSystem;

        bool state_inited = false;
        bool relationship_inited = false;
        bool personal_inited = false;
        bool biographiess_inited = false;
        bool feature_inited = false;

        public int showTab = 0;
        public Button item_btn;

        protected override void Awake()
        {
            featureObjectPool = new CreatePool<UITextField>(featureLabel);
        }

        public override void OnOpen(params object[] objects)
        {
            showTab = 0;
            currentSystem = objects[0] as PersonInformation;
            Target = currentSystem.Target;
            uIObjectList.Init(currentSystem.all_objects, PersonSortFunction.SortByName, OnObjectSelected);
            uIObjectList.SelectDefaultObject(Target);
            windiwTitle.text = currentSystem.Name;
            tabs[0].isOn = true;
            RectTransform labRect = descLabel.label.GetComponent<RectTransform>();
            Vector2 aPos = labRect.anchoredPosition;
            aPos.y = 0;
            labRect.anchoredPosition = aPos;
            Show(Target);
        }

        void OnObjectSelected(int index)
        {
            Show(currentSystem.all_objects[index] as Person);
        }

        public void Show(Person person)
        {
            state_inited = false;
            relationship_inited = false;
            personal_inited = false;
            biographiess_inited = false;
            feature_inited = false;
            Target = person;
            nameLabel.text = person.Name;
            personItems.SetPerson(person, 1);
            statusItem.SetPerson(person);
            switch (showTab)
            {
                case 0:
                    UpdateStateContent();
                    break;
                case 1:
                    UpdatePersonalContent();
                    break;
                case 2:
                    UpdateRelationshipContent();
                    break;
                case 3:
                    UpdateBiographiesContent();
                    break;
                case 4:
                    UpdateFeatureContent();
                    break;
            }
        }

        void UpdateStateContent()
        {
            if (state_inited) return;
            state_inited = true;
            forceNameLabel.text = PersonSortFunction.SortByBelongForce.GetValueStr(Target);
            corpsNameLabel.text = PersonSortFunction.SortByBelongCorps.GetValueStr(Target);
            belongCityLabel.text = PersonSortFunction.SortByBelongCity.GetValueStr(Target);
            whereLabel.text = PersonSortFunction.SortByCurrentCity.GetValueStr(Target);
            stateLabel.text = PersonSortFunction.SortByState.GetValueStr(Target);
            loyaltyLabel.text = PersonSortFunction.SortByLoyalty.GetValueStr(Target);
            meritLabel.text = PersonSortFunction.SortByMerit.GetValueStr(Target);
            officialLabel.text = PersonSortFunction.SortByCost.GetValueStr(Target);
            troopsLimitLabel.text = PersonSortFunction.SortByTroopsLimit.GetValueStr(Target);
            costLabel.text = PersonSortFunction.SortByBelongCity.GetValueStr(Target);
            missionLabel.text = PersonSortFunction.SortByMissionType.GetValueStr(Target);
            missionTargetLabel.text = PersonSortFunction.SortByMissionTarget.GetValueStr(Target);
            dayLabel.text = "";// PersonSortFunction.SortByBelongCity.GetValueStr(Target);
            actionoverLabel.text = PersonSortFunction.SortByAction.GetValueStr(Target);
            levelLabel.text = PersonSortFunction.SortByLevel.GetValueStr(Target);
            expLabel.text = PersonSortFunction.SortByExp.GetValueStr(Target);
        }

        void UpdatePersonalContent()
        {
            if (personal_inited) return;
            personal_inited = true;

            personalityLabel.text = PersonSortFunction.SortByPersonality.GetValueStr(Target);
            ageLabel.text = PersonSortFunction.SortByAge.GetValueStr(Target);
            injuryyLabel.text = "";// PersonSortFunction.SortBySex.GetValueStr(Target);
            itemCountLabel.text = "0";// PersonSortFunction.SortByPersonality.GetValueStr(Target);
            staminaLabel.text = PersonSortFunction.SortByStamina.GetValueStr(Target);
            sexLabel.text = PersonSortFunction.SortBySex.GetValueStr(Target);

            int index = 0;
            troopTypeLvLabel[index++].SetTitle(PersonSortFunction.SortBySpearLv.GetValueStr(Target));
            troopTypeLvLabel[index++].SetTitle(PersonSortFunction.SortByHalberdLv.GetValueStr(Target));
            troopTypeLvLabel[index++].SetTitle(PersonSortFunction.SortByCrossbowLv.GetValueStr(Target));
            troopTypeLvLabel[index++].SetTitle(PersonSortFunction.SortByRideLv.GetValueStr(Target));
            troopTypeLvLabel[index++].SetTitle(PersonSortFunction.SortByWaterLv.GetValueStr(Target));
            troopTypeLvLabel[index++].SetTitle(PersonSortFunction.SortByMachineLv.GetValueStr(Target));

            index = 0;
            troopTypeLvLabel[index++].text = PersonSortFunction.SortBySpearLv.name;
            troopTypeLvLabel[index++].text = PersonSortFunction.SortByHalberdLv.name;
            troopTypeLvLabel[index++].text = PersonSortFunction.SortByCrossbowLv.name;
            troopTypeLvLabel[index++].text = PersonSortFunction.SortByRideLv.name;
            troopTypeLvLabel[index++].text = PersonSortFunction.SortByWaterLv.name;
            troopTypeLvLabel[index++].text = PersonSortFunction.SortByMachineLv.name;

            //featureLabel.text = PersonSortFunction.SortByFeatureList.GetValueStr(Target);
            //featureDescLabel.text = PersonSortFunction.SortByFeatureDesc.GetValueStr(Target);
        }

        void UpdateFeatureContent()
        {
            if (feature_inited) return;
            feature_inited = true;

            featureObjectPool.Reset();
            if (Target.FeatureList != null)
            {
                for (int i = 0; i < Target.FeatureList.Count; i++)
                {
                    Feature feature = Target.FeatureList[i];
                    if (feature != null)
                    {
                        UITextField uITextField = featureObjectPool.Create();
                        if(uITextField != null)
                        {
                            uITextField.text = feature.Name;
                            uITextField.SetTitle(feature.desc);
                            uITextField.transform.SetAsLastSibling();
                        }
                    }
                }
            }
        }

        void UpdateRelationshipContent()
        {
            if (relationship_inited) return;
            relationship_inited = true;

            fatherLabel.text = PersonSortFunction.SortByFather.GetValueStr(Target);
            motherLabel.text = PersonSortFunction.SortByMother.GetValueStr(Target);
            if (Target.SpouseList != null)
                spousePersonItem.SetPerson(Target.SpouseList.Count > 0 ? Target.SpouseList[0] : null);
            else
                spousePersonItem.SetPerson(null);
            for (int i = 0; i < brotherPersonItem.Length; i++)
            {
                if (Target.BrotherList == null)
                    brotherPersonItem[i].SetPerson(null);
                else
                    brotherPersonItem[i].SetPerson(i < Target.BrotherList.Count ? Target.BrotherList[i] : null);
            }
        }

        void UpdateBiographiesContent()
        {
            if (biographiess_inited) return;
            biographiess_inited = true;
            descLabel.text = PersonSortFunction.SortByDescription.GetValueStr(Target);
        }

        public void OnCancel()
        {
            currentSystem.Back();
        }

        public void OnStateTab(bool b)
        {
            if (b)
            {
                showTab = 0;
                UpdateStateContent();
            }
        }

        public void OnPersonalTab(bool b)
        {
            if (b)
            {
                showTab = 1;
                UpdatePersonalContent();
            }
        }

        public void OnFeatrueTab(bool b)
        {
            if (b)
            {
                showTab = 4;
                UpdateFeatureContent();
            }
        }

        public void OnRelationshipTab(bool b)
        {
            if (b)
            {
                showTab = 2;
                UpdateRelationshipContent();
            }
        }

        public void OnBiographiesTab(bool b)
        {
            if (b)
            {
                showTab = 3;
                UpdateBiographiesContent();
            }
        }
        public void OnItemButton()
        {

        }

        public void OnEditButton()
        {
            GameSystem.GetSystem<PersonEdit>().Start(Target);
        }
    }
}
