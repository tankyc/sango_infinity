using Sango.UI;

namespace Sango.Core.Player
{
    public class BuildingActionBase : GameSystem
    {
        public virtual bool IsValid { get; protected set; }

        public BuildingBase TargetBuilding { get; set; }

        public string customMenuName;
        public int customMenuOrder;

        public override void Init()
        {
            GameEvent.OnBuildingContextMenuShow += OnBuildingContextMenuShow;
        }

        public override void Clear()
        {
            GameEvent.OnBuildingContextMenuShow -= OnBuildingContextMenuShow;
        }

        protected virtual void OnBuildingContextMenuShow(IContextMenuData menuData, BuildingBase building)
        {
            if (building.mBelongForce != null && building.mBelongForce.IsPlayer && building.mBelongForce == Scenario.Cur.CurRunForce)
            {
                TargetBuilding = building;
                menuData.Add(customMenuName, customMenuOrder, null, OnClickMenuItem, IsValid);
            }
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            Start(TargetBuilding);
        }

        public virtual void Start(BuildingBase building)
        {
            TargetBuilding = building;
            GameSystemManager.Instance.Push(this);
        }
    }
}
