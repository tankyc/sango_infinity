using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityBuildBuilding : CityBaseSystem
    {
        public Cell TargetCell { get; set; }
        public MapObject SelectBuildingObject { get; set; }
        public List<Cell> buildRangeCell = new List<Cell>();
        public List<BuildingType> canBuildBuildingType = new List<BuildingType>();

        public BuildingType TargetBuildingType { get; set; }
        Cell lastEnterCell;
        public CityBuildBuilding()
        {
            customTitleName = "开发";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByPolitics,
            };
            customMenuName = "都市/开发";
            customMenuOrder = 0;
            windowName = "window_city_trade";
        }

        public override void Init()
        {
            base.Init();
            GameEvent.OnCellContextMenuShow += OnCellContextMenuShow;
        }

        public override void Clear()
        {
            base.Clear();
            GameEvent.OnCellContextMenuShow -= OnCellContextMenuShow;
        }

        protected virtual void OnCellContextMenuShow(IContextMenuData menuData, Cell cell)
        {
            if (cell.IsEmpty() && cell.IsInterior)
            {
                TargetCity = cell.BelongCity;
                if (!TargetCity.IsCity())
                    TargetCity = TargetCity.mBelongCity;
                menuData.Add(customTitleName, customMenuOrder, cell, OnClickCellMenuItem, IsValid);
            }
        }

        protected virtual void OnClickCellMenuItem(IContextMenuItem contextMenuItem)
        {
            TargetCell = contextMenuItem.CustomData as Cell;
            GameSystemManager.Instance.Push(this);
        }

        public override bool IsValid
        {
            get
            {
                InitCanBuildingTypes();

                return TargetCity.freePersons.Count > 0 
                    && TargetCity.gold > 200 
                    && !TargetCity.IsInteriorBuildFull() 
                    && TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Build);
            }
        }
        void InitCanBuildingTypes()
        {
            canBuildBuildingType.Clear();
            Scenario.Cur.CommonData.BuildingTypes.ForEach(x =>
            {
                if (x.IsIntrior && x.level == 1 && x.IsValid(TargetCity) && x.canBuild)
                {
                    canBuildBuildingType.Add(x);
                }
            });
        }

        public override void UpdateJobValue()
        {
            if (personList.Count <= 0)
                return;

            int buildAbility = GameUtility.Method_PersonBuildAbility(personList.ToArray());
            int turnCount = TargetBuildingType.durabilityLimit % buildAbility == 0 ? 0 : 1;
            wonderNumber = Math.Min(Scenario.Cur.Variables.BuildMaxTurn, TargetBuildingType.durabilityLimit / buildAbility + turnCount);
        }

        public override void OnEnter()
        {
            personList.Clear();

            if (TargetBuildingType != null && TargetBuildingType.cost <= TargetCity.gold)
                SelectBuildingType(TargetBuildingType);
            else
            {
                TargetBuildingType = null;

                // 默认选中一个可以建造的类型
                for (int i = 0; i < canBuildBuildingType.Count; i++)
                {
                    BuildingType buildingType = canBuildBuildingType[i];
                    if (buildingType.cost <= TargetCity.gold)
                    {
                        SelectBuildingType(buildingType);
                        break;
                    }
                }
            }

            Window.Instance.Open("window_building_select");
        }

        public void SelectBuildingType(BuildingType buildingType)
        {
            TargetBuildingType = buildingType;
            Person[] ps = ForceAI.CounsellorRecommendBuild(TargetCity.freePersons, TargetBuildingType);
            if (ps != null)
            {
                personList.Clear();
                foreach (Person person in ps)
                {
                    personList.Add(person);
                }
            }
            UpdateJobValue();
        }

        public override void OnDestroy()
        {
            GameController.Instance.RotateViewEnabled = false;
            GameController.Instance.ZoomViewEnabled = false;
            GameController.Instance.KeyboardMoveEnabled = false;
            GameController.Instance.DragMoveViewEnabled = false;
            TargetCell = null;
            if (SelectBuildingObject != null)
            {
                SelectBuildingObject.Clear();
                SelectBuildingObject = null;
            }
            GameController.Instance.onCellOverEnter -= OnCellOverEnter;
            GameController.Instance.onCellOverStay -= OnCellOverStay;
            GameController.Instance.onCellOverExit -= OnCellOverExit;
            Window.Instance.Close("window_building_select");
            ClearShowBuildRange();
            buildRangeCell.Clear();
        }

        public void OnSelectCell()
        {
            buildRangeCell.Clear();
            for (int i = 0; i < TargetCity.interiorCellList.Count; i++)
            {
                Cell n = TargetCity.interiorCellList[i];
                if (n != null)
                {
                    buildRangeCell.Add(n);
                }
            }
            ShowBuildRange();
            UI.ContextMenu.SetVisible(false);
            SelectBuildingObject = MapObject.Create(TargetBuildingType.Name);
            SelectBuildingObject.objType = TargetBuildingType.kind;
            SelectBuildingObject.modelId = TargetBuildingType.Id;
            SelectBuildingObject.modelAsset = TargetBuildingType.model;
            SelectBuildingObject.transform.rotation = Quaternion.Euler(new Vector3(0, GameRandom.Range(0, 10) * 90, 0));
            SelectBuildingObject.transform.localScale = Vector3.one;
            SelectBuildingObject.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
            MapRender.Instance.AddDynamic(SelectBuildingObject);

            GameController.Instance.onCellOverEnter += OnCellOverEnter;
            GameController.Instance.onCellOverExit += OnCellOverExit;
            GameController.Instance.onCellOverStay += OnCellOverStay;

            GameController.Instance.RotateViewEnabled = true;
            GameController.Instance.ZoomViewEnabled = true;
            GameController.Instance.KeyboardMoveEnabled = true;
            GameController.Instance.DragMoveViewEnabled = true;

        }

        void OnCellOverEnter(Cell cell)
        {
            if (cell == null)
                return;

            if (cell.IsInterior && cell.building == null)
            {
                SelectBuildingObject.position = (cell.Position);
                SelectBuildingObject.rotation = cell.interiorModel.rotation;
                cell.SetInteriorModelVisible(false);
                lastEnterCell = cell;
            }
        }

        void OnCellOverExit(Cell cell)
        {
            if (cell == null)
                return;

            if (cell.IsInterior && cell.building == null)
            {
                cell.SetInteriorModelVisible(true);
                lastEnterCell = null;
            }
        }

        void OnCellOverStay(Cell cell, Vector3 point, bool isOverUI)
        {
            if (!isOverUI)
            {
                if (cell.IsInterior && cell.building == null)
                {
                    SelectBuildingObject.position = (cell.Position);
                    SelectBuildingObject.rotation = cell.interiorModel.rotation;
                }
                else
                    SelectBuildingObject.position = (point);
            }
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
                return;
            TargetCity.JobBuildBuilding(TargetCell, personList.ToArray(), TargetBuildingType, wonderNumber);
        }

        protected void ShowBuildRange()
        {
            MapRender mapRender = MapRender.Instance;
            mapRender.SetDarkMask(true);
            if (buildRangeCell.Count == 0) return;
            for (int i = 0, count = buildRangeCell.Count; i < count; ++i)
            {
                Cell c = buildRangeCell[i];
                mapRender.SetGridMaskColor(c.x, c.y, Color.green);
                mapRender.SetDarkMaskColor(c.x, c.y, Color.black);
            }
            mapRender.EndSetGridMask();
            mapRender.EndSetDarkMask();
        }

        protected void ClearShowBuildRange()
        {
            MapRender mapRender = MapRender.Instance;
            mapRender.SetDarkMask(false);
            if (buildRangeCell.Count == 0) return;
            for (int i = 0, count = buildRangeCell.Count; i < count; ++i)
            {
                Cell c = buildRangeCell[i];
                mapRender.SetGridMaskColor(c.x, c.y, Color.black);
                mapRender.SetDarkMaskColor(c.x, c.y, Color.clear);

            }
            mapRender.EndSetGridMask();
            mapRender.EndSetDarkMask();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {

            switch (eventType)
            {
                case CommandEventType.Cancel:                //case CommandEventType.RClick:
                //    {
                        
                //        break;
                //    }


                case CommandEventType.RClick:
                    {
                        if (SelectBuildingObject != null)
                        {
                            Window.Instance.Open("window_building_select");
                            if (SelectBuildingObject != null)
                            {
                                SelectBuildingObject.Clear();
                                SelectBuildingObject = null;
                            }
                            if(lastEnterCell != null)
                            {
                                OnCellOverExit(lastEnterCell);
                            }
                            GameController.Instance.onCellOverEnter -= OnCellOverEnter;
                            GameController.Instance.onCellOverStay -= OnCellOverStay;
                            GameController.Instance.onCellOverExit -= OnCellOverExit;
                        }
                        else
                        {
                            if (SelectBuildingObject == null)
                                GameSystemManager.Instance.Back();
                        }
                        break;
                    }
                case CommandEventType.Click:
                    {
                        if (isOverUI) return;

                        if (buildRangeCell.Contains(cell) && cell.building == null)
                        {
                            TargetCell = cell;
                            DoJob();
                            Done();
                        }
                        break;
                    }
            }
        }

    }
}
