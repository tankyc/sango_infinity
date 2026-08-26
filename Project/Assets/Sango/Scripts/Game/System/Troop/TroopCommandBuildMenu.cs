//using Sango.Render;
//using Sango.UI;
//using Sango.Render;
//using System.Collections.Generic;
//using UnityEngine;
//using ContextMenu = Sango.UI.ContextMenu;

//namespace Sango.Core.Player
//{
//    [GameSystem]
//    public class TroopCommandBuildMenu : TroopActionBase
//    {
//        protected List<Cell> buildRangeCell = new List<Cell>();
//        protected List<BuildingType> canBuildBuildingType = new List<BuildingType>();

//        protected Cell targetBuildCell;
//        protected BuildingType targetBuildingType;

//        public TroopCommandBuildMenu()
//        {
//            customMenuName = "建造";
//            customMenuOrder = 1;
//        }

//        void InitCanBuildingTypes()
//        {
//            canBuildBuildingType.Clear();
//            Scenario.Cur.CommonData.BuildingTypes.ForEach(x =>
//            {
//                if (x.IsOutside && x.canBuild && x.IsValid(TargetTroop.BelongForce))
//                {
//                    canBuildBuildingType.Add(x);
//                }
//            });
//        }

//        public override bool IsValid
//        {
//            get
//            {
//                if (TargetTroop.gold <= 0) return false;

//                InitCanBuildingTypes();

//                bool hasEnoughGold = false;
//                for (int j = 0; j < canBuildBuildingType.Count; j++)
//                {
//                    if (canBuildBuildingType[j].cost <= TargetTroop.gold)
//                    {
//                        hasEnoughGold = true;
//                        break;
//                    }
//                }

//                if (!hasEnoughGold) return false;

//                // 周围一格是可以建造的地块
//                Cell stayCell = ActionCell;
//                buildRangeCell.Clear();
//                for (int i = 0; i < stayCell.Neighbors.Length; i++)
//                {
//                    Cell n = stayCell.Neighbors[i];
//                    if (n != null)
//                    {
//                        for (int j = 0; j < canBuildBuildingType.Count; j++)
//                        {
//                            if (canBuildBuildingType[j].CanBuildToHere(n))
//                            {
//                                buildRangeCell.Add(n);
//                                break;
//                            }
//                        }
//                    }
//                }

//                return buildRangeCell.Count > 0;
//            }
//        }

//        public override void OnEnter()
//        {
//            base.OnEnter();
//            ContextMenu.SetVisible(false);
//            Window.Instance.Open("window_troop_build", TargetTroop);
//        }

//        public override void OnDestroy()
//        {
//            Window.Instance.Close("window_troop_build");
//        }

//        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
//        {
//            switch (eventType)
//            {
//                case CommandEventType.Cancel:
//                case CommandEventType.RClick:
//                    {
//                        Back();
//                        break;
//                    }
//            }
//        }
//    }
//}
