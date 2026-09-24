using Sango.Core;
using UnityEngine;


namespace Sango.Render
{
    public class TroopBuildBuildingEvent : RenderEventBase
    {
        public Troop troop;
        public BuildingType buildingType;
        public Cell targetCell;
        private bool isAction = false;
        private float time = 0;

        public void Init(Troop troop, BuildingType buildingType, Cell targetCell)
        {
            this.troop = troop;
            this.buildingType = buildingType;
            this.targetCell = targetCell;
            this.isAction = false;
            this.time = 0;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            isAction = false;
            time = 0;
            if (IsVisible())
            {
                if(troop.IsPlayer)
                {
                    GameMedia.Instance.PlayPersonSay(troop.Leader, GameRandom.Chance(50) ? 3090 : 3104);
                }
                troop.Render.SetSmokeShow(true);
            }
        }

        public override void Exit(Scenario scenario)
        {
            if (IsVisible())
            {
                troop.Render.SetSmokeShow(false);
            }
        }

        public override bool IsVisible()
        {
            return troop.Render.IsVisible();
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!IsVisible() || Input.GetMouseButtonDown(0))
            {
                Action();
                troop?.Render?.SetAniShow(0);
                IsDone = true;
                return IsDone;
            }

            if (time <= 0f)
            {
                troop.Render.FaceTo(targetCell.Position);
                troop.Render.SetAniShow(1);
            }
            if (time > 1f)
                Action();
            if (time > 2.5f)
            {
                troop.Render.SetAniShow(0);
                IsDone = true;
            }
            time += deltaTime;
            return IsDone;
        }


        public void Action()
        {
            if (isAction) return;
            if (buildingType == null)
            {
                BuildingBase building = targetCell.building;
                if (building != null && !building.IsEnemy(troop))
                    building.ChangeDurability(GameUtility.Method_TroopBuildAbility(troop), troop);
            }
            else
            {
                BuildingBase building = targetCell.building;
                if (building != null && !building.isComplate)
                    building.ChangeDurability(GameUtility.Method_TroopBuildAbility(troop), troop);
                else
                    troop.mBelongCity.BuildBuilding(targetCell, troop, buildingType);
            }

            troop.Render.UpdateRender();
            isAction = true;
        }

    }
}
