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
                troop.FaceTo(targetCell);
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

            BuildingBase before = targetCell.building;
            bool wasComplete = before != null && before.isComplate;
            bool isNewBuild = false;

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
                {
                    troop.BelongCity.BuildBuilding(targetCell, troop, buildingType);
                    isNewBuild = true;
                }
            }

            GainBuildExp(wasComplete, isNewBuild);

            troop.Render.UpdateRender();
            isAction = true;
        }

        /// <summary>
        /// 建设战果结算（功绩 30 / 政治经验 1 / 技巧点 5）。
        ///
        /// 【原版对齐】s11_sys_anim_unit.cpp 的 build_anim / repair_anim：
        /// <list type="bullet">
        /// <item>新建（<c>build_anim</c>，:1520-1521）：
        ///   <c>unit_add_exp_with_kouseki(self, building->is_trap_type() ? Exp_BuildTrap : Exp_Build, ...)</c></item>
        /// <item>修补（<c>repair_anim</c>，:1552-1558）：
        ///   <c>repaired_anim(...)</c> 返回 true（本次修完）用 Exp_Complete，否则用 Exp_Repair；
        ///   陷阱类型走对应的 *Trap 行。</item>
        /// </list>
        /// 两行数值相同（30 功绩 / 政治 1 / 技巧点 5），但分开登记才能在剧本设置里单独调。
        /// </summary>
        /// <param name="wasComplete">本次动作前该格建筑是否已完工</param>
        /// <param name="isNewBuild">本次是否为"新建"（原版 build_anim）</param>
        void GainBuildExp(bool wasComplete, bool isNewBuild)
        {
            if (troop == null || !troop.IsAlive)
                return;

            BuildingBase after = targetCell.building;
            BuildingType type = buildingType != null ? buildingType : (after != null ? after.BuildingType : null);
            // 等价原版 building->is_trap_type()：只有陷阱类设施才走 *Trap 那三行。
            bool isTrap = type != null && type.IsExplosive;

            CombatExpEvent expEvent;
            if (isNewBuild)
                expEvent = isTrap ? CombatExpEvent.BuildTrap : CombatExpEvent.Build;
            else if (!wasComplete && after != null && after.isComplate)
                expEvent = isTrap ? CombatExpEvent.CompleteTrap : CombatExpEvent.Complete;
            else
                expEvent = isTrap ? CombatExpEvent.RepairTrap : CombatExpEvent.Repair;

            troop.GainEP(expEvent);
        }

    }
}
