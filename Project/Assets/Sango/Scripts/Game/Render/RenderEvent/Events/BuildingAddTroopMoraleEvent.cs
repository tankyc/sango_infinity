using Sango.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Render
{
    public class BuildingAddTroopMoraleEvent : RenderEventBase
    {
        public Building building;
        public List<Troop> target_list;
        private bool isAction = false;
        private float time = 0;
        private int value = 0;
        private GameObject effect;

        public void Init(Building building, List<Troop> target_list, int value)
        {
            this.building = building;
            this.target_list = new List<Troop>(target_list);
            this.isAction = false;
            this.time = 0;
            this.value = value;
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

            if (effect != null)
            {
                PoolManager.Recycle(effect);
                effect = null;
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
                effect = building.Render.PlayEffect("Assets/Effect/Prefab/ef_denglu_ms_shenti.prefab");
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


            if (target_list == null) return;

            for (int i = 0; i < target_list.Count; i++)
            {
                Troop troop = target_list[i];
                troop.ChangeMorale(value);
#if SANGO_DEBUG
                Sango.Log.Info($"{building.mForce.Name}的[{building.Name}] 对 {troop.Name} 恢复 {value} 气力:, 目标剩余气力: {troop.morale}");
#endif
            }

            isAction = true;
        }

    }
}
