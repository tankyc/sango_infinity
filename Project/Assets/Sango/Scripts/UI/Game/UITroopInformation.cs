using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    public class UITroopInformation : UGUIWindow
    {
        public Text windiwTitle;

        public UIObjectList uIObjectList;

        public UIPersonItem leaderItem;
        public UIPersonItem memberItem1;
        public UIPersonItem memberItem2;
        public UIStatusItem troopStatusItem;

        public UITextField nameLabel;

        public UITextField forceNameLabel;
        public UITextField corpsNameLabel;
        public UITextField belongLabel;
        public UITextField missionLabel;
        public UITextField missionTargetLabel;
        public UITextField actionoverLabel;

        public UITextField troopLvLabel;
        public UITextField captiveCountLabel;
        public UITextField [] skillLabels;

        public UITextField troopsLabel;
        public UITextField moraleLabel;
        public UITextField goldLabel;
        public UITextField foodLabel;
        public UITextField stateLabel;
        public UIScenarioCityMap scenarioCityMap;

        public UIBuildingTypeItem itemObject;
        CreatePool<UIBuildingTypeItem> itemPool;

        Troop Target;
        TroopInformation currentSystem;

        bool items_inited = false;
        bool buildings_inited = false;

        public Button person_btn;
        public Button captive_btn;
        List<Person> personList = new List<Person>();
        protected override void Awake()
        {
            itemPool = new CreatePool<UIBuildingTypeItem>(itemObject);
        }

        public override void OnOpen(params object[] objects)
        {
            currentSystem = objects[0] as TroopInformation;
            Target = currentSystem.Target;
            uIObjectList.Init(currentSystem.all_objects, TroopSortFunction.SortByName, OnObjectSelected);
            uIObjectList.SelectDefaultObject(Target);
            windiwTitle.text = currentSystem.Name;
            Show(Target);
        }

        void OnObjectSelected(int index)
        {
            Show(currentSystem.all_objects[index] as Troop);
        }

        public void Show(Troop troop)
        {
            Target = troop;
            items_inited = false;
            buildings_inited = false;

            nameLabel.text = troop.Name;
            leaderItem.SetPerson(troop.Leader);
            memberItem1.SetPerson(troop.Member1);
            memberItem2.SetPerson(troop.Member2);

            troopStatusItem.SetTroop(troop);

            troopLvLabel.text = TroopSortFunction.SortByAbility.GetValueStr(troop);

            forceNameLabel.text = TroopSortFunction.SortByBelongForce.GetValueStr(troop);
            corpsNameLabel.text = TroopSortFunction.SortByBelongCorps.GetValueStr(troop);
            belongLabel.text = TroopSortFunction.SortByBelongCity.GetValueStr(troop);
            missionLabel.text = TroopSortFunction.SortByCaptiveCount.GetValueStr(troop);
            missionTargetLabel.text = TroopSortFunction.SortByCaptiveCount.GetValueStr(troop);
            actionoverLabel.text = TroopSortFunction.SortByActionOver.GetValueStr(troop);

            captive_btn.interactable = troop.captiveList.Count > 0;
            person_btn.interactable = true;

            troopsLabel.text = TroopSortFunction.SortByTroops.GetValueStr(troop);
            moraleLabel.text = TroopSortFunction.SortByMorale.GetValueStr(troop);
            goldLabel.text = TroopSortFunction.SortByGold.GetValueStr(troop);
            foodLabel.text = TroopSortFunction.SortByFood.GetValueStr(troop);
            stateLabel.text = TroopSortFunction.SortByState.GetValueStr(troop);
            captiveCountLabel.text = TroopSortFunction.SortByCaptiveCount.GetValueStr(troop);

            List<Skill> skillList = new List<Skill>();
            for(int i = 0; i < troop.TroopType.skills.Length; i++)
            {
                Skill skill = Scenario.Cur.GetObject<Skill>(troop.TroopType.skills[i]);
                if(!skill.IsNormalSkill)
                {
                    skillList.Add(skill);
                }
            }
            skillList.Sort((a, b) => a.needAblilityLevel.CompareTo(b.needAblilityLevel));
            for (int i = 0; i < skillLabels.Length; i++)
            {
                if(i < skillList.Count )
                {
                    Skill skill = skillList[i];
                    if(troop.TroopTypeLv >= skill.needAblilityLevel)
                    {
                        skillLabels[i].text = skill.Name;
                    }
                    else
                    {
                        skillLabels[i].text = "";
                    }
                }
                else
                {
                    skillLabels[i].text = "";
                }
            }

            Scenario scenario = Scenario.Cur;
            scenarioCityMap.Show(scenario, troop);
            UpdateItemsContent();
        }

        void UpdateItemsContent()
        {
            if (items_inited) return;
            itemPool.Reset();
            List<ItemType> ItemTypes = Target.BelongForce.createdItemTypes;
            int len = ItemTypes.Count;
            for (int i = 0; i < len; i++)
            {
                ItemType itemType = ItemTypes[i];
                int totalNum = Target.GetItemNumber(itemType.storeKind);
                UIBuildingTypeItem cityBuildingSlot = itemPool.Create();
                cityBuildingSlot.SetItemType(itemType).SetIndex(i).SetNum(totalNum);
            }
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }

        public void OnPersonButton()
        {
            personList.Clear();
            Target.ForEachPerson(x =>
            {
                personList.Add(x);
            });
            List<Person> result_list = new List<Person>();
            PersonSelectSystem personSelectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            personSelectSystem.Start(
                personList,
                result_list, 1, OnPersonSelected, null, null);
            personSelectSystem.donotFinishThisSystem = true;
        }

        void OnPersonSelected(List<Person> person_list)
        {
            if (person_list == null || person_list.Count == 0) return;
            List<SangoObject> object_list = new List<SangoObject>();
            object_list.AddRange(personList);
            GameSystem.GetSystem<PersonInformation>().Start(person_list[0], object_list);
        }

        public void OnCaptiveButton()
        {
            personList.Clear();
            Target.captiveList.ForEach(x =>
            {
                personList.Add(x);
            });
            List<Person> result_list = new List<Person>();
            PersonSelectSystem personSelectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            personSelectSystem.Start(
                personList,
                result_list, 1, OnPersonSelected, null, null);
            personSelectSystem.donotFinishThisSystem = true;
        }

    }
}
