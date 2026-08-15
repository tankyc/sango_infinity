using Sango.Core;
using UnityEngine;

namespace Sango.Render
{
    public class BuildingAttackEvent : RenderEventBase
    {
        public BuildingBase building;
        public Cell targetCell;
        private bool isAction = false;
        private float time = 0;

        public void Init(BuildingBase building, Cell targetCell)
        {
            this.building = building;
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
                //troop.Render.SetSmokeShow(true);
            }
        }

        public override void Exit(Scenario scenario)
        {
            if (IsVisible())
            {
                //troop.Render.SetSmokeShow(false);
            }
        }

        public override bool IsVisible()
        {
            return building.Render.IsVisible();
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!IsVisible() || Input.GetMouseButtonDown(0))
            {
                Action();
                IsDone = true;
                return IsDone;
            }

            if (time <= 0f)
            {
                building.Render.CastArrow(targetCell.Position);
            }
            if (time > 1.2f)
            {
                Action();
                IsDone = true;
            }
            time += deltaTime;
            return IsDone;
        }


        public void Action()
        {
            if (isAction) return;

            Troop troop = targetCell.troop;
            if (troop != null && building.IsEnemy(troop))
            {
                int dmg = Troop.CalculateSkillDamage(building, troop, building.GetAttack());
                troop.ChangeTroops(-dmg, building, null, 0);
#if SANGO_DEBUG
                Sango.Log.Info($"{building.mForce.Name}的[{building.Name}] 对 {troop.Name} 造成 {dmg} 伤害:, 目标剩余兵力: {troop.GetTroopsNum()}");
#endif
                troop.Render.UpdateRender();
            }
            isAction = true;
        }

    }
}
