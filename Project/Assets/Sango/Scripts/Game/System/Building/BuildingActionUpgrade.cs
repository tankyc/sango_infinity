using Sango.UI;
using System;
using System.Collections.Generic;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    /// <summary>
    /// 建筑升级
    /// </summary>
    [GameSystem]
    public class BuildingActionUpgrade : BuildingActionBase
    {
        public List<Person> personList = new List<Person>();
        public BuildingType TargetUpgradeType;
        public int wonderBuildCounter = 0;
        public string customTitleName;
        public List<ObjectSortTitle> customTitleList;

        public BuildingActionUpgrade()
        {
            customMenuName = "升级";
            customMenuOrder = 0;

            customTitleName = "建筑升级";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByPolitics,
            };

        }
        public override bool IsValid
        {
            get
            {
                TargetUpgradeType = Scenario.Cur.GetObject<BuildingType>(TargetBuilding.BuildingType.nextId);
                return TargetBuilding.mCity.freePersons.Count > 0 && 
                    TargetBuilding.mCity.gold >= TargetUpgradeType.cost &&
                    !TargetBuilding.isWorking && TargetBuilding.mCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.UpgradeBuilding);
            }
        }
        protected override void OnBuildingContextMenuShow(IContextMenuData menuData, BuildingBase building)
        {
            if (building.mForce != null && building.mForce.IsPlayer && building.mForce == Scenario.Cur.CurRunForce)
            {
                TargetBuilding = building;
                if (building.isComplate && !building.isUpgrading && building.BuildingType.nextId > 0)
                {
                    menuData.Add(customMenuName, customMenuOrder, null, OnClickMenuItem, IsValid);
                }
            }
        }

        public void UpdateJobValue()
        {
            if (personList.Count <= 0)
                return;

            int buildAbility = GameUtility.Method_PersonBuildAbility(personList.ToArray());
            int turnCount = TargetUpgradeType.durabilityLimit % buildAbility == 0 ? 0 : 1;
            wonderBuildCounter = Math.Min(Scenario.Cur.Variables.BuildMaxTurn, TargetUpgradeType.durabilityLimit / buildAbility + turnCount);
        }

        public override void OnEnter()
        {
            ContextMenu.CloseAll();
            base.OnEnter();
            //TargetBuilding.OnFall(null);
            //Done();
            personList.Clear();
            TargetUpgradeType = Scenario.Cur.GetObject<BuildingType>(TargetBuilding.BuildingType.nextId);
            Person[] ps = ForceAI.CounsellorRecommendBuild(TargetBuilding.mCity.freePersons, TargetUpgradeType);
            foreach (Person p in ps)
            {
                if (p != null)
                    personList.Add(p);
            }
            UpdateJobValue();
            Window.Instance.Open("window_building_upgrade");

        }


        public override void OnDestroy()
        {
            base.OnDestroy();
            Window.Instance.Close("window_building_upgrade");
        }

        public void DoJob()
        {
            TargetBuilding.mCity.JobUpgradeBuilding(TargetBuilding as Building, personList.ToArray(), TargetUpgradeType, wonderBuildCounter);
            Done();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {

            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        Exit();
                        break;
                    }
            }
        }

    }
}
