using Sango.UI;

namespace Sango.Core.Player
{
    public class TroopActionBase : GameSystem
    {
        public Troop TargetTroop { get; set; }
        public Cell ActionCell { get; set; }
        public virtual bool IsValid { get; protected set; }

        public string customMenuName;
        public int customMenuOrder;

        public override void Init()
        {
            GameEvent.OnTroopActionContextMenuShow += OnTroopActionContextMenuShow;
        }

        public override void Clear()
        {
            GameEvent.OnTroopActionContextMenuShow -= OnTroopActionContextMenuShow;
        }

        /// <summary>
        /// 处于"仅再移动"状态（<see cref="Troop.MoveRefundCount"/> &gt; 0）时，本命令是否仍允许出现与执行。
        ///
        /// <para>
        /// 默认 false：攻击 / 战法 / 计略 / 单挑 / 建造 / 修补 / 补给全部禁止，
        /// 只保留"移动"（点格子直接走，不走菜单）、"待命"与"取消"。
        /// <see cref="TroopActionStay"/> 与 <see cref="TroopActionReturn"/> 覆写为 true。
        /// </para>
        /// </summary>
        protected virtual bool AllowWhenMoveOnly => false;

        protected virtual void OnTroopActionContextMenuShow(IContextMenuData menuData, Troop troop, Cell actionCell)
        {
            // 【可再移动】仅再移动状态下，非收尾类命令不进入右键菜单
            if (troop != null && troop.MoveRefundCount > 0 && !AllowWhenMoveOnly)
                return;

            if (troop.BelongForce != null && troop.BelongForce.IsPlayer && troop.BelongForce == Scenario.Cur.CurRunForce)
            {
                TargetTroop = troop;
                ActionCell = actionCell;
                menuData.Add(customMenuName, customMenuOrder, actionCell, OnClickMenuItem, IsValid);
            }
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            Start(TargetTroop, ActionCell);
        }

        public virtual void Start(Troop troop, Cell actionCell)
        {
            // 【可再移动】硬保护：菜单隐藏只是"看不见"，这里再拦一次，
            // 保证重写了菜单方法的子类（战法 / 计略 / 补给 / 修补）以及脚本、快捷键发起时同样无法执行。
            if (troop != null && troop.MoveRefundCount > 0 && !AllowWhenMoveOnly)
            {
                Sango.Log.Warning($"部队 {troop.Name} 处于「仅可再移动」状态，命令「{customMenuName}」已被拒绝");
                return;
            }

            TargetTroop = troop;
            ActionCell = actionCell;
            if (troop.IsPlayerControl)
                troop.ClearMission();
            GameSystemManager.Instance.Push(this);
        }
    }
}
