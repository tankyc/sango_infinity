using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    /// <summary>
    /// 建筑拆除
    /// </summary>
    [GameSystem]
    public class BuildingActionDestroy : BuildingActionBase
    {
        public BuildingActionDestroy()
        {
            customMenuName = "拆除";
            customMenuOrder = 10000;
        }
        public override bool IsValid
        {
            get
            {
                // 兵临城下禁止拆除 —— 对齐 can_destroy_command
                // （s11_sys_person_order_building.cpp:13-20 的 building_has_enemy 判定）
                City city = TargetBuilding != null ? TargetBuilding.BelongCity : null;
                if (city != null && city.HasEnemyNearby())
                    return false;
                return true;
            }
        }

        public override void OnEnter()
        {
            ContextMenu.CloseAll();
            base.OnEnter();

          

            Force force = TargetBuilding.BelongForce;
            //GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{force.ColorName}大人，\n终于轮到我们了啊。", null);
            Person person = force.mCounsellor;
            if (person == null || person.BelongForce != force)
            {
                int max = TargetBuilding.BelongCity.allPersons.Count;
                if (max > 0)
                {
                    person = TargetBuilding.BelongCity.allPersons.Get(GameRandom.Range(0, max));
                }
                else
                {
                    person = force.mGovernor;
                }
            }
            GameDialog.Instance.Open(GameDialog.DialogStyle.ChoosePersonSay,
               $"拆除{TargetBuilding.ColorName}后,将无法恢复,确定吗?",
                () =>
                {
                    TargetBuilding.OnFall(null);
                    Done();
                },
                () =>
                {
                    Done();
                }, person);
        }
    }
}
